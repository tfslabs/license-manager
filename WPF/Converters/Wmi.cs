using HGM.Hotbird64.LicenseManager.Contracts;
using System;
using System.Globalization;
using System.Management;

namespace HGM.Hotbird64.LicenseManager.WPF.Converters
{
    public abstract class WmiPropertyBase : ConverterBase
    {
        protected ManagementObject WmiManagementObject => WmiProperty.Property;
        public bool DeveloperMode => WmiProperty.DeveloperMode;
        public bool ShowAllFields => WmiProperty.ShowAllFields;

        public string PropertyName { get; set; }
        public bool IsShortField { get; set; } = false;

        public IWmiProperty WmiProperty { get; protected set; }

        public virtual string UnsupportedText => IsUnsupported
            ? IsShortField || WmiProperty?.LicenseProvider?.Version == null ? "N/A" : $"Unsupported in Version {WmiProperty.LicenseProvider.Version} (Field: \"{PropertyName}\")"
            : null;

        public virtual bool IsUnsupported
        {
            get
            {
                if (WmiManagementObject == null)
                {
                    return false;
                }

                try
                {
                    object _ = WmiManagementObject[PropertyName];
                    return false;
                }
                catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotFound)
                {
                    return true;
                }
            }
        }
    }

    public class WmiProperty : WmiPropertyBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            WmiProperty = value as IWmiProperty;

            if (IsUnsupported)
            {
                return UnsupportedText;
            }

            if (WmiManagementObject?[PropertyName] == null)
            {
                return null;
            }

            return System.Convert.ChangeType(WmiManagementObject[PropertyName], targetType, culture);
        }
    }

    public class WmiLicenseClassName : WmiEpidPropertyBase
    {
        public override bool IsUnsupported => false;

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            WmiProperty = (IWmiProperty)value;
            LicenseMachine.LicenseProvider provider = WmiProperty?.LicenseProvider;
            return $"{provider?.FriendlyName} {provider?.Version}";
        }
    }

    public abstract class WmiEpidPropertyBase : WmiProperty
    {
        protected string EPidPropertyName;
        protected object EPidPropertyValue { get; private set; }
        protected CultureInfo Culture { get; private set; }
        protected Func<string> DisplayFunc;

        public override bool IsUnsupported
        {
            get
            {
                if (base.IsUnsupported)
                {
                    return true;
                }

                try
                {
                    EPid ePid = new EPid(WmiManagementObject[PropertyName]);
                    EPidPropertyValue = typeof(EPid).GetProperty(EPidPropertyName)?.GetValue(ePid);
                }
                catch
                {
                    return true;
                }

                return false;
            }
        }

        public override string UnsupportedText => base.IsUnsupported ? base.UnsupportedText : "Invalid Extended PID";

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            WmiProperty = value as IWmiProperty;
            Culture = culture;
            return IsUnsupported ? UnsupportedText : DisplayFunc();
        }
    }

    public class WmiEpidDate : WmiEpidPropertyBase
    {
        public WmiEpidDate()
        {
            EPidPropertyName = nameof(EPid.Date);
            DisplayFunc = () => string.Format(Culture, "{0:D}", EPidPropertyValue);
        }
    }

    public class WmiEpidOs : WmiEpidPropertyBase
    {
        public WmiEpidOs()
        {
            EPidPropertyName = nameof(EPid.LongOsName);
            DisplayFunc = () => string.Format(Culture, "{0}", EPidPropertyValue);
        }
    }

    public class WmiGuid : WmiProperty
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string stringValue = (string)base.Convert(value, targetType, parameter, culture);
            bool developerMode = WmiProperty.DeveloperMode;

            if (IsUnsupported || !developerMode || stringValue == null)
            {
                return stringValue;
            }

            byte[] guidBytes;

            try
            {
                guidBytes = new Guid(stringValue).ToByteArray();
            }
            catch
            {
                return stringValue;
            }

            uint data1 = (uint)(guidBytes[3] << 24 |
                               guidBytes[2] << 16 |
                               guidBytes[1] << 8 |
                               guidBytes[0]);

            ushort data2 = (ushort)(guidBytes[5] << 8 |
                                 guidBytes[4]);

            ushort data3 = (ushort)(guidBytes[7] << 8 |
                                 guidBytes[6]);

            string byteList = "";
            string cGuid = $" / {{ 0x{data1:x8}, 0x{data2:x4}, 0x{data3:x4}, {{ ";

            for (int i = 8; i < 16; i++)
            {
                byteList += $"0x{guidBytes[i]:x2}{(i == 15 ? "" : ",")} ";
            }

            cGuid += byteList + "} } / ";
            cGuid += $"new Guid( 0x{data1:x8}, 0x{data2:x4}, 0x{data3:x4}, {byteList} )";
            stringValue += cGuid;


            return stringValue;
        }
    }

    public class WmiPhoneInstallationId : WmiProperty
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                string result = "";
                string rawValue = (base.Convert(value, targetType, parameter, culture) as IConvertible)?.ToString(culture);

                if (rawValue == null)
                {
                    return null;
                }

                if (rawValue.Length % 9 != 0)
                {
                    throw new Exception();
                }

                for (int i = 0; i < 9; i++)
                {
                    result += rawValue.Substring(i * rawValue.Length / 9, rawValue.Length / 9) + (i == 8 ? "" : "-");
                }

                return result;
            }
            catch
            {
                return value?.ToString();
            }
        }
    }

    public class WmiPropertyAddLicenseType : WmiProperty
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string result = (base.Convert(value, targetType, parameter, culture) as IConvertible)?.ToString(culture);

            if (result == null) { return null; }

            try
            {
                result += " (" + WmiManagementObject?["ProductKeyChannel"] + ")";
            }
            catch
            {
                // Could not retrieve
            }

            return result;
        }
    }
}
