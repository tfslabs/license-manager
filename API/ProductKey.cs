using System.Globalization;

namespace HGM.Hotbird64.Vlmcs
{
    public interface IProductKey
    {
        string Key { get; }
        KeyType KeyType { get; }
    }

    public class KeyBase : IProductKey
    {
        public int MsKeyType { get; set; } = -1;
        public KeyType KeyType { get; }

        public string Key
        {
            get => BinaryKey.ToString();
            set => BinaryKey = (BinaryProductKey)value.ToUpperInvariant();
        }

        public BinaryProductKey BinaryKey { get; private set; }
        public string EPid => BinaryProductKey.GetEpid(BinaryKey, MsKeyType);
        private static CultureInfo osSystemLocale;

        public static CultureInfo OsSystemLocale
        {
            get => osSystemLocale ?? CultureInfo.InstalledUICulture;
            set => osSystemLocale = value;
        }

        public KeyBase(string key, KeyType keyType)
        {
            Key = key;
            KeyType = keyType;
            BinaryKey = (BinaryProductKey)key;
            CommonConstructorTasks();
        }

        public KeyBase(BinaryProductKey binaryProductKey, KeyType keyType)
        {
            BinaryKey = binaryProductKey;
            KeyType = keyType;
            Key = (string)BinaryKey;
            CommonConstructorTasks();
        }

        public void CommonConstructorTasks()
        {
            switch (KeyType)
            {
                case KeyType.Retail:
                case KeyType.RetailGenerated:
                case KeyType.StoreLicense:
                case KeyType.StoreLicenseGenerated:
                    MsKeyType = 0;
                    break;

                case KeyType.Gvlk:
                case KeyType.GvlkGenerated:
                    MsKeyType = 3;
                    break;
            }
        }

        public string GetEpid(BinaryProductKey binaryKey)
        {
            return BinaryProductKey.GetEpid(binaryKey, MsKeyType);
        }

        public string GetEpid(string key)
        {
            return BinaryProductKey.GetEpid(key, MsKeyType);
        }


        public string KeyTypeString
        {
            get
            {
                switch (KeyType)
                {
                    case KeyType.Gvlk: return "GVLK";
                    case KeyType.GvlkGenerated: return "user-generated GVLK";
                    case KeyType.StoreLicense: return "Store License";
                    default: return "unknown";
                }
            }
        }

        public override bool Equals(object obj)
        {
            IProductKey other = obj as IProductKey;
            if (other == null) return false;
            return Key == other.Key;
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }

        public override string ToString() => Key;
    }

    public class ProductKey : KeyBase
    {
        public string Name { get; set; }
        public override string ToString() => Name;

        public ProductKey(string name, string key, KeyType keytype) : base(key, keytype)
        {
            Name = name;
        }

        public ProductKey(string name, BinaryProductKey key, KeyType keytype) : base(key, keytype)
        {
            Name = name;
        }
    };

    public enum KeyType
    {
        Gvlk = 1,
        GvlkGenerated = 1025,
        Retail = 2,
        RetailGenerated = 1026,
        StoreLicense = 3,
        StoreLicenseGenerated = 1027,
        Unknown = 0,
    }
}
