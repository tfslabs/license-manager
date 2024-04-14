using HGM.Hotbird64.LicenseManager;
using HGM.Hotbird64.LicenseManager.Contracts;
using HGM.Hotbird64.LicenseManager.Extensions;
using LicenseManager.Annotations;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

// ReSharper disable PartialTypeWithSinglePart

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.Vlmcs
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Xml;
    using System.Xml.Schema;
    using System.Xml.Serialization;

    [XmlType(AnonymousType = true)]
    [XmlRoot(Namespace = "", IsNullable = false)]
    public partial class KmsData
    {
        public static ReaderWriterLock Lock = new ReaderWriterLock();

        [XmlArray("WinBuilds", Form = XmlSchemaForm.Unqualified), XmlArrayItem("WinBuild", Form = XmlSchemaForm.Unqualified)]
        public List<WinBuild> WinBuilds { get; set; }

        [XmlIgnore]
        public IReadOnlyList<WinBuild> EpidBuilds => WinBuilds.Where(b => b.UseForEpid).OrderBy(b => b.BuildNumber).ToArray();

        //[XmlIgnore]
        //public IReadOnlyList<int> EpidBuildNumbers => EpidBuilds.Select(b => b.BuildNumber).ToArray();

        [XmlArray("CsvlkItems", Form = XmlSchemaForm.Unqualified), XmlArrayItem("CsvlkItem", Form = XmlSchemaForm.Unqualified)]
        public KmsProductCollection<CsvlkItem> CsvlkItems { get; set; }

        [XmlArray("AppItems", Form = XmlSchemaForm.Unqualified), XmlArrayItem("AppItem", Form = XmlSchemaForm.Unqualified)]
        public KmsProductCollection<AppItem> Items { get; set; }

        [XmlAttribute(AttributeName = "Version")]
        public string VersionString { get; set; }

        [XmlIgnore]
        public ProtocolVersion Version
        {
            get => (ProtocolVersion)VersionString;
            set => VersionString = value.ToString();
        }

        [XmlAttribute]
        public string Author { get; set; }

        [XmlAttribute(AttributeName = "noNamespaceSchemaLocation", Namespace = "http://www.w3.org/2001/XMLSchema-instance")]
        [DefaultValue(null)]
        // ReSharper disable once InconsistentNaming
        public string xsi_noNamespaceSchemaLocation { get; set; }
    }

    [XmlType(AnonymousType = true)]
    public class WinBuild : PropertyChangeBase
    {
        private int buildNumber, platformId;
        private string displayName;
        private bool mayBeServer, useForEpid, usesNDR64;
        private DateTime releaseDate;

        [XmlAttribute]
        public int BuildNumber
        {
            get => buildNumber;
            set => this.SetProperty(ref buildNumber, value);
        }

        [XmlAttribute]
        public DateTime ReleaseDate
        {
            get => releaseDate;
            set => this.SetProperty(ref releaseDate, value);
        }

        [XmlAttribute]
        public string DisplayName
        {
            get => displayName;
            set => this.SetProperty(ref displayName, value);
        }

        [XmlAttribute]
        public int PlatformId
        {
            get => platformId;
            set => this.SetProperty(ref platformId, value);
        }

        [XmlAttribute]
        [DefaultValue(false)]
        public bool MayBeServer
        {
            get => mayBeServer;
            set => this.SetProperty(ref mayBeServer, value);
        }

        [XmlAttribute]
        [DefaultValue(false)]
        public bool UseForEpid
        {
            get => useForEpid;
            set => this.SetProperty(ref useForEpid, value);
        }

        [XmlAttribute]
        [DefaultValue(false)]
        public bool UsesNDR64
        {
            get => usesNDR64;
            set => this.SetProperty(ref usesNDR64, value);
        }

        public override string ToString() => DisplayName;
    }

    [XmlType(AnonymousType = true)]
    public class CsvlkItem : KmsProduct, IHaveNotifyOfPropertyChange
    {
        private bool isRandom;
        private string ePid;

        [XmlAttribute, DefaultValue(false)] public bool IsLab { get; set; }
        [XmlAttribute, DefaultValue((sbyte)-1)] public int VlmcsdIndex { get; set; } = -1;
        [XmlAttribute, DefaultValue(false)] public bool IsPreview { get; set; }
        [XmlAttribute, DefaultValue(-1)] public int GroupId { get; set; } = -1;
        [XmlAttribute, DefaultValue(-1)] public int MinKeyId { get; set; } = -1;
        [XmlAttribute, DefaultValue(-1)] public int MaxKeyId { get; set; } = -1;
        [XmlAttribute] public string IniFileName { get; set; }
        [XmlAttribute] public DateTime ReleaseDate { get; set; }


        [XmlAttribute]
        public string EPid
        {
            get => ePid;
            set => this.SetProperty(ref ePid, value, postAction: () => NotifyOfPropertyChange(nameof(IsValidEpid)));
        }

        [XmlIgnore]
        public bool IsRandom
        {
            get => isRandom;
            set => this.SetProperty(ref isRandom, value);
        }

        [XmlIgnore]
        public bool IsValidEpid => new EPid(EPid).IsValidEpidFormat;

        [XmlAttribute]
        public string Id
        {
            get => Guid.ToString();
            set => Guid = new KmsGuid(value);
        }

        [XmlElement("Activate", Form = XmlSchemaForm.Unqualified)] public ActivateList Activates { get; set; }

        [XmlIgnore] public bool Export => VlmcsdIndex >= 0;
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        public virtual void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [XmlType(AnonymousType = true)]
    public class Activate
    {
        [XmlAttribute("KmsItem", Form = XmlSchemaForm.Unqualified)]
        public string Id
        {
            get => Guid.ToString();
            set => Guid = new KmsGuid(value);
        }

        [XmlIgnore] public KmsGuid Guid { get; set; }

        public override string ToString() => KmsLists.KmsItemList[Guid].DisplayName;
    }


    [XmlType(AnonymousType = true)]
    public partial class AppItem : KmsProduct, INotifyPropertyChanged
    {
        private int maxActiveClients = 50, minActiveClients = 50;

        [XmlElement("KmsItem", Form = XmlSchemaForm.Unqualified)] public KmsProductCollection<KmsItem> KmsItems { get; set; }
        [XmlAttribute] public sbyte VlmcsdIndex { get; set; }

        [XmlAttribute]
        public string Id
        {
            get => Guid.ToString();
            set => Guid = new KmsGuid(value);
        }
        [XmlAttribute, DefaultValue(50)]
        public int MinActiveClients
        {
            get => minActiveClients;
            set
            {
                minActiveClients = MaxActiveClients = value;
                NotifyOfPropertyChange();
            }
        }

        [XmlIgnore]
        public ConcurrentQueue<KmsGuid> Queue { get; } = new ConcurrentQueue<KmsGuid>();

        [XmlIgnore]
        public int MaxActiveClients
        {
            get => maxActiveClients;
            set
            {
                maxActiveClients = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(ClientStatus));
                NotifyOfPropertyChange(nameof(IsValidClientStatus));
            }
        }

        [XmlIgnore]
        public string ClientStatus => $"{Queue.Count} / {MaxActiveClients}";
        [XmlIgnore]
        public bool? IsValidClientStatus
            => (MaxActiveClients > MinActiveClients || Queue.Count == 0) && Queue.Count < 671 ? (bool?)null : Queue.Count >= MinActiveClients >> 1 && Queue.Count < 671;

        public void Reset()
        {
            while (!Queue.IsEmpty)
            {
                Queue.TryDequeue(out _);
            }

            MaxActiveClients = MinActiveClients;
            NotifyOfPropertyChange(nameof(Queue));
        }

        public void AddClient(KmsGuid guid)
        {
            if (Queue.Contains(guid))
            {
                return;
            }

            Queue.Enqueue(guid);

            if (Queue.Count > MaxActiveClients)
            {
                Queue.TryDequeue(out _);
            }

            NotifyOfPropertyChange(nameof(Queue));
            NotifyOfPropertyChange(nameof(ClientStatus));
            NotifyOfPropertyChange(nameof(IsValidClientStatus));
        }

        public void PreCharge(bool preCharge)
        {
            Reset();

            if (!preCharge)
            {
                return;
            }

            for (int i = 0; i < (MaxActiveClients >> 1) - 1; i++)
            {
                AddClient(KmsGuid.NewGuid());
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [XmlType(AnonymousType = true)]
    public partial class KmsItem : KmsProduct
    {
        public KmsItem()
        {
            IsPreview = false;
            IsRetail = false;
            DefaultKmsProtocolString = "6.0";
            NCountPolicy = 25;
        }

        [XmlIgnore]
        public AppItem App { get; set; }

        [XmlElement("SkuItem", Form = XmlSchemaForm.Unqualified)]
        public KmsProductCollection<SkuItem> SkuItems { get; set; }

        [XmlAttribute]
        public string Id
        {
            get => Guid.ToString();
            set => Guid = new KmsGuid(value);
        }

        [XmlAttribute]
        [DefaultValue(true)]
        public bool IsPreview { get; set; }

        [XmlAttribute]
        [DefaultValue(true)]
        public bool CanMapToDefaultCsvlk { get; set; } = true;

        [XmlAttribute]
        [DefaultValue(false)]
        public bool IsRetail { get; set; }

        [XmlAttribute(AttributeName = "DefaultKmsProtocol")]
        public string DefaultKmsProtocolString { get; set; }

        [XmlIgnore]
        public ProtocolVersion DefaultKmsProtocol
        {
            get => (ProtocolVersion)DefaultKmsProtocolString;
            set => DefaultKmsProtocolString = value.ToString();
        }

        [XmlAttribute]
        [DefaultValue(25)]
        public int NCountPolicy { get; set; }

    }

    [XmlType(AnonymousType = true)]
    public partial class SkuItem : KmsProduct
    {
        public SkuItem()
        {
            IsGeneratedGvlk = false;
        }

        [XmlIgnore]
        public KmsItem KmsItem { get; set; }

        [XmlAttribute]
        public string Id
        {
            get => Guid.ToString();
            set => Guid = new KmsGuid(value);
        }

        [XmlAttribute]
        public string Gvlk { get; set; }

        [XmlAttribute]
        [DefaultValue(false)]
        public bool IsGeneratedGvlk { get; set; }

        [XmlIgnore]
        public int KeyGroup { get; set; }
        [XmlIgnore]
        public int KeyId { get; set; }
    }


    public abstract class KmsProduct : GuidItem
    {
        [XmlAttribute, DefaultValue(null)] public string DisplayName { get; set; }
        public override string ToString() => DisplayName;
    }

    public abstract class GuidItem : IEquatable<KmsGuid>, IEquatable<Guid>
    {
        [XmlIgnore] public virtual KmsGuid Guid { get; set; }

        // ReSharper disable once NonReadonlyMemberInGetHashCode
        public override int GetHashCode() => Guid.GetHashCode();

        public override bool Equals(object obj)
        {
            KmsGuid other;

            switch (obj)
            {
                case GuidItem product:
                    other = product.Guid;
                    break;
                case KmsGuid _:
                    other = (KmsGuid)obj;
                    break;
                case Guid _:
                    other = new KmsGuid((Guid)obj);
                    break;
                default:
                    // ReSharper disable once BaseObjectEqualsIsObjectEquals
                    return base.Equals(obj);
            }

            return Guid == other;
        }

        public bool Equals(KmsGuid other) => Guid == other;
        public bool Equals(Guid other) => this == other;

        public static bool operator ==(GuidItem a, GuidItem b) => a?.Equals(b) ?? b is null;
        public static bool operator !=(GuidItem a, GuidItem b) => !(a == b);
        public static bool operator ==(GuidItem a, KmsGuid b) => !(a is null) && a.Equals(b);
        public static bool operator !=(GuidItem a, KmsGuid b) => !(a == b);
        public static bool operator ==(KmsGuid a, GuidItem b) => b == a;
        public static bool operator !=(KmsGuid a, GuidItem b) => b != a;
        public static bool operator ==(GuidItem a, Guid b) => !(a is null) && a.Equals(b);
        public static bool operator !=(GuidItem a, Guid b) => !(a == b);
        public static bool operator ==(Guid a, GuidItem b) => b == a;
        public static bool operator !=(Guid a, GuidItem b) => b != a;
    }

    public interface IKmsProductCollection<T> : IList<T> where T : GuidItem
    {
        T this[KmsGuid index] { get; set; }
        T this[Guid index] { get; set; }
        T this[string guidString] { get; set; }
    }

    public class ActivateList : List<Activate>
    {
        public Activate this[KmsGuid guid]
        {
            get { return this.FirstOrDefault(p => p.Guid == guid); }

            set
            {
                int index = IndexOf(this[guid]);
                if (index < 0)
                {
                    throw new IndexOutOfRangeException($"The guid {guid} was not found in the list");
                }

                this[index] = value;
            }
        }

        public Activate this[Guid guid]
        {
            get => this[(KmsGuid)guid];
            set => this[(KmsGuid)guid] = value;
        }

        public Activate this[string guidString]
        {
            get => this[new KmsGuid(guidString)];
            set => this[new KmsGuid(guidString)] = value;
        }
    }

    public class KmsProductCollection<T> : ObservableCollection<T>, IKmsProductCollection<T> where T : GuidItem
    {
        public KmsProductCollection(IEnumerable<T> x) : base(x) { }
        public KmsProductCollection() { }

        //public T Find(KmsGuid guid) => this.FirstOrDefault(p => p.Guid == guid);
        //public T Find(Guid guid) => this.FirstOrDefault(p => p.Guid == guid);

        public T this[KmsGuid guid]
        {
            get { return this.FirstOrDefault(p => p.Guid == guid); }

            set
            {
                int index = IndexOf(this[guid]);
                if (index < 0)
                {
                    throw new IndexOutOfRangeException($"The guid {guid} was not found in the list");
                }

                this[index] = value;
            }
        }

        public T this[Guid guid]
        {
            get => this[(KmsGuid)guid];
            set => this[(KmsGuid)guid] = value;
        }

        public T this[string guidString]
        {
            get => this[new KmsGuid(guidString)];
            set => this[new KmsGuid(guidString)] = value;
        }
    }

    public static class KmsLists
    {
        public static Action LoadDatabase = null;
        public static Func<Stream> GetXsdValidationStream = null;
        public static IKmsProductCollection<SkuItem> SkuItemList => new KmsProductCollection<SkuItem>(KmsItemList.SelectMany(k => k.SkuItems));
        public static IKmsProductCollection<KmsItem> KmsItemList => new KmsProductCollection<KmsItem>(AppItemList.SelectMany(a => a.KmsItems));
        public static IKmsProductCollection<AppItem> AppItemList => KmsData.Items;
        public static IKmsProductCollection<CsvlkItem> CsvlkItemList => KmsData.CsvlkItems;
        private static KmsData kmsData;
        private static int buildIndex = -1;

        public static KmsData KmsData
        {
            get
            {
                try
                {
                    KmsData.Lock.AcquireReaderLock(1000);

                    try
                    {
                        if (kmsData != null)
                        {
                            return kmsData;
                        }
                    }
                    finally
                    {
                        KmsData.Lock.ReleaseReaderLock();
                    }
                }
                catch (ApplicationException ex)
                {
                    throw new InvalidOperationException("The Product and Key database is currently being updated and cannot be accessed", ex);
                }

                if (LoadDatabase == null)
                {
                    throw new InvalidOperationException("No KMS Database is loaded and no on-demand loader has been set up.");
                }

                LoadDatabase();
                if (kmsData?.Items == null)
                {
                    throw new InvalidOperationException("The on-demand loader did not load a KMS Database.");
                }

                return kmsData;
            }

            set => kmsData = value;
        }

        public static void ReadDatabase(Stream stream, bool validate = true)
        {
            if (validate)
            {
                if (GetXsdValidationStream == null)
                {
                    throw new InvalidOperationException("You must implement a method that retrieves an open System.IO.Stream containing the XSD file for the database.");
                }

                using (Stream xsdStream = GetXsdValidationStream())
                {
                    StringBuilder errors = new StringBuilder();
                    XmlSchema schema = XmlSchema.Read(xsdStream, (s, e) => throw e.Exception);
                    XmlReaderSettings settings = new XmlReaderSettings
                    {
                        ValidationType = ValidationType.Schema,
                    };

                    settings.ValidationEventHandler += (s, e) => { errors.AppendLine($"Line {e.Exception.LineNumber}, position {e.Exception.LinePosition}: {e.Exception.Message}"); };
                    settings.Schemas.Add(schema);
                    XmlReader xmlFile = XmlReader.Create(stream, settings);
                    while (xmlFile.Read()) { }
                    if (errors.Length != 0)
                    {
                        throw new InvalidDataException(errors.ToString());
                    }
                }

                stream.Seek(0, SeekOrigin.Begin);
            }

            XmlSerializer serializer = new XmlSerializer(typeof(KmsData));

            KmsData.Lock.AcquireWriterLock(2000);

            try
            {
                KmsData = (KmsData)serializer.Deserialize(XmlReader.Create(stream));
                IReadOnlyList<WinBuild> epidBuilds = KmsData.EpidBuilds;
                int buildCount = epidBuilds.Count;

                while
                (
                    buildIndex < 0 ||
                    buildIndex >= buildCount ||
                    (
                        epidBuilds.Any(b => b.UsesNDR64) &&
                        !epidBuilds[buildIndex].UsesNDR64
                    )
                )
                {
                    buildIndex = KmsServer.Rand.Next(0, buildCount);
                }

                foreach (AppItem appItem in KmsData.Items)
                {
                    foreach (KmsItem kmsItem in appItem.KmsItems)
                    {
                        kmsItem.App = appItem;
                        foreach (SkuItem skuItem in kmsItem.SkuItems)
                        {
                            skuItem.KmsItem = kmsItem;
                            if (skuItem.Gvlk == null || !skuItem.Gvlk.Contains('N'))
                            {
                                continue;
                            }

                            BinaryProductKey binaryKey = (BinaryProductKey)skuItem.Gvlk;
                            skuItem.KeyGroup = (int)binaryKey.Group;
                            skuItem.KeyId = (int)binaryKey.Id;
                        }
                    }
                }

                CheckForDuplicateIds(KmsItemList, nameof(KmsItemList));
                CheckForDuplicateIds(SkuItemList, nameof(SkuItemList));
                CheckForDuplicateIds(AppItemList, nameof(AppItemList));

                foreach (CsvlkItem csvlkItem in CsvlkItemList)
                {
                    if (csvlkItem.GroupId < 0)
                    {
                        int? groupId = ProductBrowser.KeyConfigs.FirstOrDefault(k => k.ActConfigGuid == csvlkItem.Guid && k.RefGroupIdSpecified)?.RefGroupId;

                        if (!groupId.HasValue)
                        {
                            throw new InvalidDataException($"CsvlkItem \"{csvlkItem.DisplayName}\" has no {nameof(csvlkItem.GroupId)} and no corresponding pkeyconfig file was found");
                        }

                        csvlkItem.GroupId = groupId.Value;
                    }

                    if (csvlkItem.MinKeyId < 0 || csvlkItem.MaxKeyId < 0)
                    {
                        ProductKeyConfigurationKeyRangesKeyRange keyRange = ProductBrowser.KeyRanges.Where(r => r.RefActConfigGuid == csvlkItem.Guid && r.StartSpecified && r.EndSpecified).OrderByDescending(r => r.KeysAvailable).FirstOrDefault();

                        if (keyRange == null)
                        {
                            throw new InvalidDataException($"{nameof(CsvlkItem)} \"{csvlkItem.DisplayName}\" has no {nameof(csvlkItem.MinKeyId)} or no {nameof(csvlkItem.MaxKeyId)} and no corresponding pkeyconfig file was found");
                        }

                        csvlkItem.MinKeyId = keyRange.Start;
                        csvlkItem.MaxKeyId = keyRange.End;
                    }

                    if (csvlkItem.MinKeyId > csvlkItem.MaxKeyId)
                    {
                        throw new InvalidDataException($"{nameof(csvlkItem.MinKeyId)} is greater than {nameof(csvlkItem.MaxKeyId)}");
                    }

                    if (csvlkItem.EPid == null && csvlkItem.Export)
                    {
                        GetRandomEPid(csvlkItem);
                    }

                    if (!csvlkItem.Activates.All(a => KmsItemList.Select(k => k.Guid).Contains(a.Guid)))
                    {
                        throw new InvalidDataException($"{nameof(CsvlkItem)} \"{csvlkItem.DisplayName}\" contains an {nameof(Activate)} with an invalid {nameof(KmsItem)}");
                    }
                }

                CsvlkItem[] exportedCsvlk = CsvlkItemList.Where(c => c.Export).OrderBy(c => c.VlmcsdIndex).ToArray();
                Activate[] exportedActivates = exportedCsvlk.SelectMany(c => c.Activates).ToArray();
                IReadOnlyList<string> exportedIniFileNames = exportedCsvlk.Select(e => e.IniFileName.ToUpperInvariant()).ToArray();

                if (exportedIniFileNames.Count != exportedIniFileNames.Distinct().Count())
                {
                    throw new InvalidDataException($"Case-insensitive {nameof(CsvlkItem.IniFileName)} must be unique for all {nameof(CsvlkItem)}s that are exported to vlmcsd.kmd");
                }

                if
                (
                    exportedCsvlk.Length != exportedCsvlk.Distinct().Count() ||
                    exportedCsvlk[0].VlmcsdIndex != 0 ||
                    exportedCsvlk.Last().VlmcsdIndex != exportedCsvlk.Length - 1
                )
                {
                    // ReSharper disable once RedundantAssignment
                    throw new InvalidDataException($"'{nameof(CsvlkItem.VlmcsdIndex)}es' must be subsequent numbers starting from 0. You have " +
                                                   $"{exportedCsvlk.Aggregate(string.Empty, (current, next) => current += (current == string.Empty ? string.Empty : ", ") + next.VlmcsdIndex)}");
                }

                if (exportedActivates.Length != exportedActivates.Distinct().Count())
                {
                    throw new InvalidDataException($"'{nameof(CsvlkItem.VlmcsdIndex)}' has been set for {nameof(CsvlkItem)}s with overlapping {nameof(KmsItem)}s");
                }

                foreach (AppItem appItem in AppItemList)
                {
                    if (!CsvlkItemList.Select(c => c.VlmcsdIndex).Contains(appItem.VlmcsdIndex))
                    {
                        throw new InvalidDataException($"{nameof(AppItem)} \"{appItem.DisplayName}\" has a {nameof(appItem.VlmcsdIndex)} that is not defined in a {nameof(CsvlkItem)}");
                    }
                }
            }
            finally
            {
                KmsData.Lock.ReleaseWriterLock();
            }
        }

        public static void GetRandomEPid(CsvlkItem csvlkItem)
        {
            int buildNumber = KmsData.EpidBuilds[buildIndex].BuildNumber;
            int keyId = KmsServer.Rand.Next(csvlkItem.MinKeyId, csvlkItem.MaxKeyId + 1);
            csvlkItem.EPid = $"{GetPlatformId(buildNumber):D5}-{csvlkItem.GroupId:D5}-{keyId / 1000000:D3}-{keyId % 1000000:D6}-03-{KmsServer.Lcid}-{buildNumber}.0000-{DateTime.Now.ToEpidPart()}";
        }

        public static int GetPlatformId(int buildNumber)
        {
            IReadOnlyList<WinBuild> winBuilds = KmsData.WinBuilds.OrderByDescending(b => b.BuildNumber).ToArray() as IReadOnlyList<WinBuild>;

            foreach (WinBuild winBuild in winBuilds)
            {
                if (buildNumber >= winBuild.BuildNumber)
                {
                    return winBuild.PlatformId;
                }
            }

            return winBuilds[winBuilds.Count - 1].PlatformId;
        }

        private static void CheckForDuplicateIds<T>(ICollection<T> list, string listName) where T : KmsProduct
        {
            if (list.Count == list.Distinct().Count())
            {
                return;
            }

            string duplicates = string.Empty;
            IEnumerable<KmsGuid> duplicateIds = list.GroupBy(k => k.Guid).Where(g => g.Count() > 1).Select(k => k.Key).Distinct();
            duplicates = duplicateIds.Aggregate(duplicates, (current, id) => current + $"{id}\n");
            KmsData = null;
            throw new InvalidDataException($"The following GUIDs are used more than once in {listName}:\n\n{duplicates}");
        }
    }
}
