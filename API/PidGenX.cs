using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace HGM.Hotbird64.Vlmcs
{

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct DigitalProductId2
    {
        private fixed char pid[24];
        public string Pid { get { fixed (char* c = pid) return Marshal.PtrToStringUni((IntPtr)c); } }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    public unsafe struct DigitalProductId3
    {
        public uint size;
        private readonly uint version;
        private fixed byte pid[24];
        public uint KeyGroup;
        private fixed byte editionId[16];
        public BinaryProductKey BinaryKey;
        public uint CloneStatus;
        public uint Time;
        public uint Random;
        public uint LicenseType;
        public ulong LicenseData;
        private fixed byte oemId[8];
        public uint BundleId;
        private fixed byte hardwareIdStatic[8];
        public uint HardwareIdTypeStatic;
        public uint BiosChecksumStatic;
        public uint VolumeSerialNumberStatic;
        public uint TotalRamStatic;
        public uint VideoBiosChecksumStatic;
        private fixed byte hardwareIdDynamic[8];
        public uint HardwareIdTypeDynamic;
        public uint BiosChecksumDynamic;
        public uint VolumeSerialNumberDynamic;
        public uint TotalRamDynamic;
        public uint VideoBiosChecksumDynamic;
        public uint CRC32;

        public static uint Size => (uint)sizeof(DigitalProductId3);
        public ProtocolVersion Version => new ProtocolVersion { Major = (ushort)(version & 0xffff), Minor = (ushort)(version >> 16) };
        public string Pid { get { fixed (byte* c = pid) return Marshal.PtrToStringAnsi((IntPtr)c); } }
        public string EditionId { get { fixed (byte* c = editionId) return Marshal.PtrToStringAnsi((IntPtr)c); } }
        public string OemId { get { fixed (byte* c = oemId) return Marshal.PtrToStringAnsi((IntPtr)c); } }
        public byte[] HardwareIdStatic { get { fixed (byte* b = hardwareIdStatic) return PidGen.GetBytes(b, 16); } }
        public byte[] HardwareIdDynamic { get { fixed (byte* b = hardwareIdDynamic) return PidGen.GetBytes(b, 16); } }

        public static explicit operator DigitalProductId3(byte[] bytes)
        {
            if (bytes.Length < sizeof(DigitalProductId3)) throw new ArgumentException($"Digital Product Id 3 must have {sizeof(DigitalProductId3)} bytes", nameof(bytes));

            fixed (byte* b = bytes)
            {
                return *(DigitalProductId3*)b;
            }
        }

        public override string ToString() => BinaryKey.ToString();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct DigitalProductId4
    {
        public uint size;
        private readonly uint version;
        private fixed char ePid[64];
        private fixed char skuId[64];
        private fixed char oemId[8];
        private fixed char editionType[260];
        public byte IsUpgrade;
        public byte reserved1;
        public byte reserved2;
        public byte reserved3;
        public byte reserved4;
        public byte reserved5;
        public byte reserved6;
        public byte reserved7;
        public BinaryProductKey BinaryKey;
        private fixed byte keyHash[32];
        private fixed byte hash[32];
        private fixed char editionId[64];
        private fixed char keyType[64];
        private fixed char eula[64];

        public static uint Size => (uint)sizeof(DigitalProductId4);
        public string EPid { get { fixed (char* c = ePid) return Marshal.PtrToStringUni((IntPtr)c); } }
        public string OemId { get { fixed (char* c = oemId) return Marshal.PtrToStringUni((IntPtr)c); } }
        public string EditionType { get { fixed (char* c = editionType) return Marshal.PtrToStringUni((IntPtr)c); } }
        public string EditionId { get { fixed (char* c = editionId) return Marshal.PtrToStringUni((IntPtr)c); } }
        public string KeyType { get { fixed (char* c = keyType) return Marshal.PtrToStringUni((IntPtr)c); } }
        public string Eula { get { fixed (char* c = eula) return Marshal.PtrToStringUni((IntPtr)c); } }
        public byte[] KeyHash { get { fixed (byte* b = keyHash) return PidGen.GetBytes(b, 32); } }
        public byte[] Hash { get { fixed (byte* b = hash) return PidGen.GetBytes(b, 32); } }
        public ProtocolVersion Version => new ProtocolVersion { Major = (ushort)(version & 0xffff), Minor = (ushort)(version >> 16) };
        public override string ToString() => BinaryKey.ToString();

        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public Guid SkuId
        {
            get
            {
                string skuIdString;
                fixed (char* c = skuId) skuIdString = Marshal.PtrToStringUni((IntPtr)c);
                return new Guid(skuIdString);
            }
        }

        public static explicit operator DigitalProductId4(byte[] bytes)
        {
            if (bytes.Length < sizeof(DigitalProductId4)) throw new ArgumentException($"Digital Product Id 4 must have {sizeof(DigitalProductId4)} bytes", nameof(bytes));

            fixed (byte* b = bytes)
            {
                return *(DigitalProductId4*)b;
            }
        }
    }

    public class EPidQueryException : Exception
    {
        public int ErrorCode;
        public string EPid;

        public EPidQueryException(string message, int errorCode, string epid) : base(message)
        {
            ErrorCode = errorCode;
            EPid = epid;
        }
    }

    public static class PidGen
    {
        public const string EpidPattern = @"^[0-9]{5}-[0-9]{5}-[0-9]{3}-[0-9]{6}-0[0123]-[0-9]{4,5}-[0-9]{4,5}\.0000-(36[0-6]|3[0-5][0-9]|[0-2][0-9]{2})20[0-9]{2}$";

        public static readonly byte[] MSActivationServerHmacKey =
        {
            0xfe, 0x31, 0x98, 0x75, 0xfb, 0x48, 0x84, 0x86, 0x9c, 0xf3, 0xf1, 0xce, 0x99, 0xa8, 0x90, 0x64,
            0xab, 0x57, 0x1f, 0xca, 0x47, 0x04, 0x50, 0x58, 0x30, 0x24, 0xe2, 0x14, 0x62, 0x87, 0x79, 0xa0,
        };

        [DllImport("pidgenx.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
        private static extern uint PidGenX
        (
          string key,
          string fileName,
          string pidStart,
          IntPtr oemId,
          out DigitalProductId2 digitalProductId2,
          ref DigitalProductId3 digitalProductId3,
          ref DigitalProductId4 digitalProductId4
        );

        internal static unsafe byte[] GetBytes(byte* b, int len)
        {
            byte[] result = new byte[len];

            for (int i = 0; i < len; i++)
            {
                result[i] = b[i];
            }

            return result;
        }

        public static int GetRemainingActivationsOnline(DigitalProductId4 id4) => GetRemainingActivationsOnline(id4.EPid);

        public static int GetRemainingActivationsOnline(string ePid)
        {
            int GetNumber(string s)
            {
                if (s == null) return 0;

                return s.StartsWith("0x")
                    ? unchecked((int)uint.Parse(s.Substring(2), NumberStyles.AllowHexSpecifier | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture))
                    : int.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
            }

            if (!Regex.IsMatch(ePid, EpidPattern))
            {
                throw new ArgumentException($"\"{ePid}\" is not a valid EPID", nameof(ePid));
            }

            byte[] activationRequest = Encoding.Unicode.GetBytes
            (
                "<ActivationRequest xmlns=\"http://www.microsoft.com/DRM/SL/BatchActivationRequest/1.0\">" +
                    "<VersionNumber>2.0</VersionNumber>" +
                    "<RequestType>2</RequestType>" +
                    "<Requests>" +
                        "<Request>" +
                            "<PID>" + ePid + "</PID>" +
                        "</Request>" +
                    "</Requests>" +
                "</ActivationRequest>"
            );

            byte[] digest = new HMACSHA256(MSActivationServerHmacKey).ComputeHash(activationRequest, 0, activationRequest.Length);

            byte[] soapRequest = Encoding.UTF8.GetBytes
            (
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<s:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" " +
                            "xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" " +
                            "xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\"" +
                ">" +
                    "<s:Body>" +
                        "<BatchActivate xmlns = \"http://www.microsoft.com/BatchActivationService\">" +
                            "<request>" +
                                "<Digest>" + Convert.ToBase64String(digest, Base64FormattingOptions.None) + "</Digest>" +
                                "<RequestXml>" + Convert.ToBase64String(activationRequest, Base64FormattingOptions.None) + "</RequestXml>" +
                            "</request>" +
                        "</BatchActivate>" +
                    "</s:Body>" +
                "</s:Envelope>"
            );

            HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create("https://activation.sls.microsoft.com/BatchActivation/BatchActivation.asmx");

            httpRequest.Method = "POST";
            httpRequest.ContentType = "application/soap+xml; charset=utf-8";
            httpRequest.ContentLength = soapRequest.Length;
            httpRequest.ProtocolVersion = new Version(1, 1);
            httpRequest.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; MS Web Services Client Protocol 4.0.30319.1)";
            httpRequest.Host = "activation.sls.microsoft.com";

            using (Stream requestStream = httpRequest.GetRequestStream())
            {
                requestStream.Write(soapRequest, 0, soapRequest.Length);
            }

            HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();

            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new WebException($"Error while communicating with activation.sls.microsoft.com. Http status: {httpResponse.StatusCode} ({(int)httpResponse.StatusCode})", WebExceptionStatus.ProtocolError);
            }

            XmlDocument soapResponseDocument = new XmlDocument();

            using (Stream soapResponse = httpResponse.GetResponseStream())
            {
                if (soapResponse != null) soapResponseDocument.Load(soapResponse);
            }

            XmlDocument activationResponseDocument = new XmlDocument();
            activationResponseDocument.LoadXml(soapResponseDocument.LastChild.FirstChild.FirstChild.FirstChild.FirstChild.InnerText);

            string responseError = activationResponseDocument.SelectSingleNode("/*[local-name()='ActivationResponse']/*[local-name()='ErrorInfo']/*[local-name()='ErrorCode']")?.InnerText;

            if (responseError != null)
            {
                throw new EPidQueryException($"EPID \"{ePid}\" is in an unknown format.", GetNumber(responseError), ePid);
            }

            XmlNode payLoadNode = activationResponseDocument.SelectSingleNode("/*[local-name()='ActivationResponse']/*[local-name()='Responses']/*[local-name()='Response']");
            string errorCodeText = payLoadNode?.SelectSingleNode("//*[local-name()='ErrorInfo']/*[local-name()='ErrorCode']")?.InnerText;

            if (errorCodeText != null)
            {
                int errorCode = GetNumber(errorCodeText);

                switch (errorCode)
                {
                    case 0x67:
                        throw new EPidQueryException("The EPID is blocked", errorCode, ePid);
                    case 0x86:
                        throw new EPidQueryException("This is not an EPID that has multiple online activations", errorCode, ePid);
                    default:
                        throw new EPidQueryException("Unknown error", errorCode, ePid);
                }
            }

            string responsePid = payLoadNode?.SelectSingleNode("//*[local-name()='PID']")?.InnerText;

            if (responsePid == null)
            {
                throw new EPidQueryException("EPID is in an unknown format.", -1, ePid);
            }

            if (responsePid != ePid)
            {
                throw new EPidQueryException($"Requested info for EPID \"{ePid}\" but got answer for EPID \"{responsePid}\"", -1, ePid);
            }

            string activationsRemainingText = payLoadNode.SelectSingleNode("//*[local-name()='ActivationRemaining']")?.InnerText;

            if (activationsRemainingText == null)
            {
                throw new EPidQueryException("activation.sls.microsoft.com did not return the number of remaining activations.", -1, ePid);
            }

            return GetNumber(activationsRemainingText);
        }


        public static void CheckKey(string key, string pkeyConfigFileName, out DigitalProductId2 id2, out DigitalProductId3 id3, out DigitalProductId4 id4)
        {
            int osBuild = Environment.OSVersion.Version.Build;
            string ePidStart;

            if (osBuild >= 10000)
            {
                ePidStart = "03612";
            }
            else if (osBuild >= 9600)
            {
                ePidStart = "06401";
            }
            else if (osBuild >= 9200)
            {
                ePidStart = "05426";
            }
            else
            {
                ePidStart = "55041";
            }

            id3 = new DigitalProductId3();
            id4 = new DigitalProductId4();
            id3.size = DigitalProductId3.Size;
            id4.size = DigitalProductId4.Size;

            uint hResult = PidGenX(key, pkeyConfigFileName, ePidStart, IntPtr.Zero, out id2, ref id3, ref id4);

            if (hResult != 0)
            {
                Win32Exception innerException = (hResult & 0xffff0000) == 0x80070000 ? new Win32Exception(unchecked((int)hResult)) : null;
                switch (hResult)
                {
                    case 0x80070002:
                        throw new FileNotFoundException("pkeyconfig database file not found", pkeyConfigFileName, innerException);

                    default:
                        throw new KmsException(Kms.StatusMessage(hResult), innerException);
                }
            }
        }
    }
}
