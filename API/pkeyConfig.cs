
using System.Diagnostics.CodeAnalysis;
namespace HGM.Hotbird64.Vlmcs
{
    using System;
    using System.Collections.Generic;
    using System.Xml.Serialization;

    public interface IPKeyConfigFile
    {
        string DisplayName { get; }
    }

    [Serializable]
    [XmlType(AnonymousType = true, Namespace = "http://www.microsoft.com/DRM/PKEY/Configuration/2.0")]
    [XmlRoot(Namespace = "http://www.microsoft.com/DRM/PKEY/Configuration/2.0", IsNullable = false)]
    public partial class ProductKeyConfiguration
    {
        [XmlElement("Configurations", typeof(ProductKeyConfigurationConfigurations))]
        [XmlElement("KeyRanges", typeof(ProductKeyConfigurationKeyRanges))]
        [XmlElement("PublicKeys", typeof(ProductKeyConfigurationPublicKeys))]
        public object[] Items { get; set; }
    }

    [Serializable]
    [XmlType(AnonymousType = true, Namespace = "http://www.microsoft.com/DRM/PKEY/Configuration/2.0")]
    public partial class ProductKeyConfigurationConfigurations
    {
        [XmlElement("Configuration")]
        public HashSet<ProductKeyConfigurationConfigurationsConfiguration> Configuration { get; set; }
    }

    [Serializable]
    [XmlType(AnonymousType = true, Namespace = "http://www.microsoft.com/DRM/PKEY/Configuration/2.0")]
    public partial class ProductKeyConfigurationConfigurationsConfiguration : IEquatable<ProductKeyConfigurationConfigurationsConfiguration>
    {
        [XmlIgnore]
        public IPKeyConfigFile Source { get; set; }

        public string ActConfigId { get; set; }

        [XmlIgnore]
        public KmsGuid ActConfigGuid
        {
            get => new KmsGuid(ActConfigId);
            set => ActConfigId = value.ToString();
        }

        public int RefGroupId { get; set; }

        [XmlIgnore]
        public bool RefGroupIdSpecified { get; set; }

        public string EditionId { get; set; }
        public string ProductDescription { get; set; }
        public string ProductKeyType { get; set; }

        [XmlIgnore]
        public int MsKeyType
        {
            get
            {
                if (ProductKeyType == null) return -1;
                string[] split = ProductKeyType.Split(':');

                switch (split[0].ToUpperInvariant())
                {
                    case "VOLUME":
                        return 3;
                    case "RETAIL":
                        return 0;
                    case "OEM":
                        return 2;
                    default:
                        return -1;
                }
            }
        }

        public override string ToString()
        {
            switch (ProductKeyType)
            {
                case "Volume:CSVLK":
                    try
                    {
                        return KmsLists.CsvlkItemList[ActConfigGuid].DisplayName;
                    }
                    catch
                    {
                        // ignored
                    }

                    break;

                case "Volume:GVLK":
                    string kmsDataBaseName = KmsLists.SkuItemList[ActConfigGuid]?.DisplayName;
                    if (kmsDataBaseName != null) return kmsDataBaseName;
                    break;
            }

            return ProductDescription;
        }

        public bool Equals(ProductKeyConfigurationConfigurationsConfiguration other)
        {
            if (other == null) return false;
            return ActConfigGuid == other.ActConfigGuid;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ProductKeyConfigurationConfigurationsConfiguration other)) return false;
            return ActConfigGuid == other.ActConfigGuid;
        }

        public override int GetHashCode() => ActConfigGuid.GetHashCode();

        public bool IsRandomized { get; set; }

        [XmlIgnore]
        public bool IsRandomizedSpecified { get; set; }
    }

    [Serializable]
    [XmlType(AnonymousType = true, Namespace = "http://www.microsoft.com/DRM/PKEY/Configuration/2.0")]
    public partial class ProductKeyConfigurationKeyRanges
    {
        [XmlElement("KeyRange")]
        public HashSet<ProductKeyConfigurationKeyRangesKeyRange> KeyRange { get; set; }
    }

    [Serializable]
    [XmlType(AnonymousType = true, Namespace = "http://www.microsoft.com/DRM/PKEY/Configuration/2.0")]
    public partial class ProductKeyConfigurationKeyRangesKeyRange
    {
        public string RefActConfigId { get; set; }

        [XmlIgnore]
        public KmsGuid RefActConfigGuid
        {
            get => new KmsGuid(RefActConfigId);
            set => RefActConfigId = value.ToString();
        }

        public string PartNumber { get; set; }
        public string EulaType { get; set; }
        public bool IsValid { get; set; }

        [XmlIgnore]
        public bool IsValidSpecified { get; set; }

        public int Start { get; set; }

        [XmlIgnore]
        public bool StartSpecified { get; set; }

        public int End { get; set; }

        [XmlIgnore]
        public bool EndSpecified { get; set; }

        [XmlIgnore]
        public int KeysAvailable => End - Start + 1;

        [XmlIgnore]
        public string FileName { get; set; }

        public override bool Equals(object obj)
        {
            ProductKeyConfigurationKeyRangesKeyRange other = obj as ProductKeyConfigurationKeyRangesKeyRange;
            if (other == null) return false;

            return
              Start == other.Start &&
              End == other.End &&
              RefActConfigGuid == other.RefActConfigGuid;
        }

        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode() => Start ^ End;
    }

    [Serializable]
    [XmlType(AnonymousType = true, Namespace = "http://www.microsoft.com/DRM/PKEY/Configuration/2.0")]
    public partial class ProductKeyConfigurationPublicKeys
    {
        [XmlElement("PublicKey")]
        public HashSet<ProductKeyConfigurationPublicKeysPublicKey> PublicKey { get; set; }
    }

    /// <remarks/>
    [Serializable]
    [XmlType(AnonymousType = true, Namespace = "http://www.microsoft.com/DRM/PKEY/Configuration/2.0")]
    public partial class ProductKeyConfigurationPublicKeysPublicKey
    {
        public int GroupId { get; set; }

        [XmlIgnore]
        public bool GroupIdSpecified { get; set; }

        public string AlgorithmId { get; set; }

        public string PublicKeyValue { get; set; }

        // ReSharper disable once NonReadonlyMemberInGetHashCode
        public override int GetHashCode() => GroupId;

        public override bool Equals(object obj)
        {
            ProductKeyConfigurationPublicKeysPublicKey other = obj as ProductKeyConfigurationPublicKeysPublicKey;
            return GroupId == other?.GroupId;
        }
    }
}
