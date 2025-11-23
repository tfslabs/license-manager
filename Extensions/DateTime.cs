using System;

namespace HGM.Hotbird64.LicenseManager.Extensions
{
    public static class DateTimeExtensions
    {
        public static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static string ToEpidPart(this DateTime date)
        {
            return $"{date.DayOfYear:D3}{date.Year:D4}";
        }

        public static long ToUnixTime(this DateTime date)
        {
            return (long)Math.Round((date - Epoch).TotalSeconds);
        }
    }
}
