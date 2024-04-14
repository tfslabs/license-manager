using System.Collections.Generic;

namespace HGM.Hotbird64.LicenseManager.Model
{
    //public enum LicenseStatusEnum
    //{
    //    Unknown = -1,
    //    Unlicensed = 0,
    //    Licensed = 1,
    //    InitialGrace = 2,
    //    AdditionalGrace = 3,
    //    NonGenuineGrace = 4,
    //    Notification = 5,
    //    ExtendedGrace = 6,
    //}

    public static class LicenseStatus
    {
        public static IDictionary<Vlmcs.LicenseStatus, string> Text = new Dictionary<HGM.Hotbird64.Vlmcs.LicenseStatus, string>
        {
            { Vlmcs.LicenseStatus.Unknown, "Unknown" },
            { Vlmcs.LicenseStatus.GraceOot, "Additional Grace" },
            { Vlmcs.LicenseStatus.GraceExtended, "Extended Grace" },
            { Vlmcs.LicenseStatus.GraceOob, "Initial Grace" },
            { Vlmcs.LicenseStatus.Licensed, "Licensed" },
            { Vlmcs.LicenseStatus.GraceNonGenuine, "Non-genuine Grace" },
            { Vlmcs.LicenseStatus.Notification, "Notification" },
            { Vlmcs.LicenseStatus.Unlicensed, "Unlicensed" },
        };

        public static string GetText(Vlmcs.LicenseStatus licenseStatus)
        {
            try
            {
                return Text[licenseStatus];
            }
            catch (KeyNotFoundException)
            {
                return Text[Vlmcs.LicenseStatus.Unknown];
            }
        }

        public static string GetText(uint licenseStatus) => GetText(unchecked((Vlmcs.LicenseStatus)licenseStatus));
    }
}
