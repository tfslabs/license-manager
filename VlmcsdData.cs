using HGM.Hotbird64.LicenseManager.Extensions;
using HGM.Hotbird64.Vlmcs;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace HGM.Hotbird64.LicenseManager
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VlmcsdData
    {
        public KmsGuid Guid;
        public ulong NameOffset;
        public byte AppIndex;
        public byte KmsIndex;
        public byte ProtocolVersion;
        public byte NCountPolicy;
        public byte IsRetail;
        public byte IsPreview;
        public byte EPidIndex;
        private readonly byte reserved;

        public static ulong Size => (uint)sizeof(VlmcsdData);
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct CsvlkData
    {
        public ulong EPidOffset;
        private long releaseDateUnix;
        public uint GroupId;
        public uint MinKeyId;
        public uint MaxKeyId;
        public byte MinActiveClients;
        private fixed byte reserved[3];

        public static ulong Size => (uint)sizeof(CsvlkData);

        public DateTime ReleaseDate
        {
            get => DateTimeExtensions.Epoch.AddSeconds(releaseDateUnix);
            set => releaseDateUnix = value.ToUnixTime();
        }
    }

    [Flags]
    public enum VlmcsdOption
    {
        None = 0,
        UseNdr64 = 1 << 0,
        UseForEpid = 1 << 1,
        MayBeServer = 1 << 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct HostBuild
    {
        public ulong DisplayNameOffset;
        public long releaseDateUnix;
        public int BuildNumber;
        public int PlatformId;
        [MarshalAs(UnmanagedType.I4)] public VlmcsdOption Flags;
        private fixed byte reserved[4];

        public static ulong Size => unchecked((ulong)sizeof(HostBuild));
        public DateTime ReleaseDate
        {
            get => DateTimeExtensions.Epoch.AddSeconds(releaseDateUnix);
            set => releaseDateUnix = value.ToUnixTime();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VlmcsdHeader
    {
        public fixed byte Magic[4];
        public uint Version;
        public byte CsvlkCount;
        public byte Flags;
        private fixed byte reserved[2];
        public uint AppItemCount;
        public uint KmsItemCount;
        public uint SkuItemCount;
        public uint HostBuildCount;
        private readonly uint reserved2Count;
        public ulong AppItemOffset;
        public ulong KmsItemOffset;
        public ulong SkuItemOffset;
        public ulong HostBuildOffset;
        private readonly ulong reserved2Offset;

        public static ulong Size => (uint)sizeof(VlmcsdHeader);

        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
        public MemoryStream WriteData(bool includeAppItems, bool includeKmsItems, bool includeSkuItems, bool noText, bool includeBetaSkuItem)
        {
            fixed (byte* m = Magic)
            {
                m[0] = 0x4B;
                m[1] = 0x4D;
                m[2] = 0x44;
                m[3] = 0x00;
            }

            IReadOnlyList<CsvlkItem> exportedCsvlks = KmsLists.CsvlkItemList.Where(c => c.VlmcsdIndex >= 0).OrderBy(c => c.VlmcsdIndex).ToArray() as IReadOnlyList<CsvlkItem>;
            CsvlkCount = (byte)exportedCsvlks.Count;
            IReadOnlyList<WinBuild> exportedHostBuilds = KmsLists.KmsData.WinBuilds.Where(b => b.UseForEpid).OrderByDescending(b => b.BuildNumber).ToArray() as IReadOnlyList<WinBuild>;
            HostBuildCount = (uint)exportedHostBuilds.Count;

            try
            {
                EPid ePid = new EPid(exportedCsvlks[0].EPid);
                Flags |= (byte)(ePid.OsBuild > 7601 ? VlmcsdOption.UseNdr64 : VlmcsdOption.None);
            }
            catch
            {
                // No valid host build
            }

            KmsItem[] kmsItemList = includeKmsItems ? KmsLists.KmsItemList.ToArray() : KmsLists.KmsItemList.Where(k => !k.CanMapToDefaultCsvlk).ToArray();

            SkuItem[] skuItemList = KmsLists.SkuItemList.Where(s => includeBetaSkuItem || !s.KmsItem.IsPreview).ToArray();
            ulong csvlkDataSize = CsvlkData.Size * CsvlkCount;
            ulong hostBuildSize = HostBuild.Size * HostBuildCount;

            Version = KmsLists.KmsData.Version.Full;
            AppItemOffset = Size + csvlkDataSize;
            AppItemCount = includeAppItems ? (uint)KmsLists.AppItemList.Count : 0;
            KmsItemOffset = AppItemOffset + AppItemCount * VlmcsdData.Size;
            KmsItemCount = (uint)kmsItemList.Length;
            SkuItemOffset = KmsItemOffset + KmsItemCount * VlmcsdData.Size;
            SkuItemCount = includeSkuItems && includeKmsItems ? (uint)skuItemList.Length : 0;
            HostBuildOffset = SkuItemOffset + SkuItemCount * VlmcsdData.Size;

            VlmcsdData[] idData = new VlmcsdData[AppItemCount + KmsItemCount + SkuItemCount];
            HostBuild[] hostBuilds = new HostBuild[HostBuildCount];
            VlmcsdDataText[] textData = new VlmcsdDataText[idData.Length];
            ulong currentText = (ulong)idData.Length * VlmcsdData.Size + Size + csvlkDataSize + hostBuildSize;
            VlmcsdDataText[] ePids = new VlmcsdDataText[CsvlkCount];
            VlmcsdDataText[] iniFileNames = new VlmcsdDataText[CsvlkCount];
            VlmcsdDataText[] csvlkNames = new VlmcsdDataText[CsvlkCount];
            CsvlkData[] csvlkDataList = new CsvlkData[CsvlkCount];
            VlmcsdDataText[] hostBuildNames = new VlmcsdDataText[HostBuildCount];

            ePids[0] = new VlmcsdDataText(exportedCsvlks[0].EPid, currentText);

            for (int i = 0; i < CsvlkCount; i++)
            {
                if (i != 0)
                {
                    ePids[i] = new VlmcsdDataText(exportedCsvlks[i].EPid, csvlkNames[i - 1].OffsetNext);
                }

                iniFileNames[i] = new VlmcsdDataText(exportedCsvlks[i].IniFileName, ePids[i].OffsetNext);
                csvlkNames[i] = new VlmcsdDataText(noText ? string.Empty : exportedCsvlks[i].DisplayName, iniFileNames[i].OffsetNext);

                csvlkDataList[i] = new CsvlkData
                {
                    EPidOffset = ePids[i].Offset,
                    GroupId = (uint)exportedCsvlks[i].GroupId,
                    MinKeyId = (uint)exportedCsvlks[i].MinKeyId,
                    MaxKeyId = (uint)exportedCsvlks[i].MaxKeyId,
                    MinActiveClients = 0,
                    ReleaseDate = exportedCsvlks[i].ReleaseDate,
                };
            }

            currentText = csvlkNames[CsvlkCount - 1].OffsetNext;
            VlmcsdDataText unknownText = new VlmcsdDataText("Unknown", currentText);

            if (noText)
            {
                currentText = unknownText.OffsetNext;
            }

            for (int i = 0; i < HostBuildCount; i++)
            {
                hostBuilds[i].Flags =
                    (exportedHostBuilds[i].UsesNDR64 ? VlmcsdOption.UseNdr64 : VlmcsdOption.None) |
                    (exportedHostBuilds[i].MayBeServer ? VlmcsdOption.MayBeServer : VlmcsdOption.None) |
                    (exportedHostBuilds[i].UseForEpid ? VlmcsdOption.UseForEpid : VlmcsdOption.None);

                hostBuilds[i].BuildNumber = exportedHostBuilds[i].BuildNumber;
                hostBuilds[i].PlatformId = exportedHostBuilds[i].PlatformId;
                hostBuilds[i].ReleaseDate = exportedHostBuilds[i].ReleaseDate;

                if (noText)
                {
                    hostBuilds[i].DisplayNameOffset = unknownText.Offset;
                }
                else
                {
                    hostBuildNames[i] = new VlmcsdDataText(exportedHostBuilds[i].DisplayName, currentText);
                    hostBuilds[i].DisplayNameOffset = currentText;
                    currentText = hostBuildNames[i].OffsetNext;
                }
            }

            int index = 0;

            if (includeAppItems)
            {
                foreach (AppItem appItem in KmsLists.AppItemList)
                {
                    byte minActiveClients = (byte)appItem.MinActiveClients;

                    if (minActiveClients < 1)
                    {
                        minActiveClients = (byte)(appItem.KmsItems.Select(k => k.NCountPolicy).Max() << 1);
                    }

                    idData[index].Guid = appItem.Guid;
                    idData[index].EPidIndex = (byte)appItem.VlmcsdIndex;
                    //TODO: Find out if client count is really per AppItem
                    idData[index].NCountPolicy = minActiveClients;
                    index = WriteText(noText, idData, index, unknownText, textData, appItem, ref currentText);
                }
            }

            foreach (KmsItem kmsItem in kmsItemList)
            {
                idData[index].Guid = kmsItem.Guid;
                idData[index].IsPreview = (byte)(kmsItem.IsPreview ? 1 : 0);
                idData[index].IsRetail = (byte)(kmsItem.IsRetail ? 1 : 0);
                idData[index].NCountPolicy = (byte)kmsItem.NCountPolicy;
                idData[index].ProtocolVersion = (byte)kmsItem.DefaultKmsProtocol.Major;
                idData[index].AppIndex = (byte)(includeAppItems ? KmsLists.AppItemList.IndexOf(kmsItem.App) : 0);
                CsvlkItem csvlk = exportedCsvlks.SingleOrDefault(c => c.Activates[kmsItem.Guid] != null);

                if (csvlk != null)
                {
                    idData[index].EPidIndex = (byte)csvlk.VlmcsdIndex;
                }

                index = WriteText(noText, idData, index, unknownText, textData, kmsItem, ref currentText);
            }

            if (includeKmsItems && includeSkuItems)
            {
                foreach (SkuItem skuItem in skuItemList)
                {
                    idData[index].Guid = skuItem.Guid;
                    idData[index].KmsIndex = (byte)KmsLists.KmsItemList.IndexOf(skuItem.KmsItem);
                    idData[index].AppIndex = (byte)KmsLists.AppItemList.IndexOf(skuItem.KmsItem.App);
                    idData[index].ProtocolVersion = (byte)skuItem.KmsItem.DefaultKmsProtocol.Major;
                    idData[index].NCountPolicy = (byte)skuItem.KmsItem.NCountPolicy;
                    idData[index].IsPreview = (byte)(skuItem.KmsItem.IsPreview ? 1 : 0);
                    idData[index].IsRetail = (byte)(skuItem.KmsItem.IsRetail ? 1 : 0);
                    CsvlkItem csvlk = exportedCsvlks.SingleOrDefault(c => c.Activates[skuItem.KmsItem.Guid] != null);

                    if (csvlk != null)
                    {
                        idData[index].EPidIndex = (byte)csvlk.VlmcsdIndex;
                    }

                    index = WriteText(noText, idData, index, unknownText, textData, skuItem, ref currentText);
                }
            }

            MemoryStream stream = new MemoryStream();
            stream.SetLength((long)currentText);
            stream.Seek(0, SeekOrigin.Begin);

            fixed (VlmcsdHeader* b = &this)
            {
                for (ulong i = 0; i < Size; i++)
                {
                    stream.WriteByte(((byte*)b)[i]);
                }
            }

            for (int i = 0; i < csvlkDataList.Length; i++)
            {
                fixed (CsvlkData* b = &csvlkDataList[i])
                {
                    for (ulong j = 0; j < CsvlkData.Size; j++)
                    {
                        stream.WriteByte(((byte*)b)[j]);
                    }
                }
            }

            for (int i = 0; i < idData.Length; i++)
            {
                fixed (VlmcsdData* b = &idData[i])
                {
                    for (ulong j = 0; j < VlmcsdData.Size; j++)
                    {
                        stream.WriteByte(((byte*)b)[j]);
                    }
                }
            }

            for (int i = 0; i < hostBuilds.Length; i++)
            {
                fixed (HostBuild* b = &hostBuilds[i])
                {
                    for (ulong j = 0; j < HostBuild.Size; j++)
                    {
                        stream.WriteByte(((byte*)b)[j]);
                    }
                }
            }

            for (int i = 0; i < ePids.Length; i++)
            {
                stream.Write(ePids[i].Data, 0, ePids[i].Data.Length);
                stream.Write(iniFileNames[i].Data, 0, iniFileNames[i].Data.Length);
                stream.Write(csvlkNames[i].Data, 0, csvlkNames[i].Data.Length);
            }

            if (noText)
            {
                stream.Write(unknownText.Data, 0, unknownText.Data.Length);
            }
            else
            {
                foreach (VlmcsdDataText text in hostBuildNames)
                {
                    stream.Write(text.Data, 0, text.Data.Length);
                }

                foreach (VlmcsdDataText text in textData)
                {
                    stream.Write(text.Data, 0, text.Data.Length);
                }

            }

            return stream;
        }

        private static int WriteText(bool noText, VlmcsdData[] idData, int index, VlmcsdDataText unknownText, IList<VlmcsdDataText> textData, KmsProduct item, ref ulong currentText)
        {
            if (noText)
            {
                idData[index].NameOffset = unknownText.Offset;
            }
            else
            {
                textData[index] = new VlmcsdDataText(item.DisplayName, currentText);
                idData[index].NameOffset = currentText;
                currentText = textData[index].OffsetNext;
            }

            return index + 1;
        }
    }

    public class VlmcsdDataText
    {
        public byte[] Data;
        public ulong Offset;
        public ulong OffsetNext => Offset + (ulong)Data.LongLength;

        public VlmcsdDataText(string text, ulong offset)
        {
            Offset = offset;
            Data = new UTF8Encoding(false, false).GetBytes(text + (char)0);
        }
    }
}