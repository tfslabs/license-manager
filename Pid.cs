using HGM.Hotbird64.Vlmcs;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.LicenseManager
{
    public class EPid : IEquatable<EPid>, IEquatable<string>
    {
        public static readonly DateTime IllegalDate = new DateTime(1601, 1, 1, 23, 59, 59);
        public readonly string Id;
        public string[] Split { get; private set; }
        public string DateString => Split.Length < 8 ? null : Split[7];
        public string OsBuildString => Split.Length < 7 ? null : Split[6];
        public string LcidString => Split.Length < 6 ? null : Split[5];
        public string KeyTypeString => Split.Length < 5 ? null : Split[4];
        public string KeyIdString => Split.Length < 4 ? null : Split[2] + "-" + Split[3];
        public string GroupIdString => Split.Length < 2 ? null : Split[1];
        public string OsIdString => Split[0];

        public override string ToString() => Id;

        public bool Equals(string other)
        {
            return Id.Equals(other);
        }

        public bool Equals(EPid other)
        {
            return (object)other != null && Equals(other.Id);
        }

        [SuppressMessage("ReSharper", "CanBeReplacedWithTryCastAndCheckForNull")]
        [SuppressMessage("ReSharper", "BaseObjectEqualsIsObjectEquals")]
        public override bool Equals(object other)
        {
            if (other is EPid)
            {
                return Id == ((EPid)other).Id;
            }
            else if (other is string)
            {
                return Id == (string)other;
            }
            else
            {
                return base.Equals(other);
            }
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(EPid left, EPid right) => left?.Id == right?.Id;
        public static bool operator !=(EPid left, EPid right) => !(left == right);
        public static bool operator ==(EPid left, string right) => left?.Id == right;
        public static bool operator !=(EPid left, string right) => !(left == right);
        public static bool operator ==(string left, EPid right) => left == right?.Id;
        public static bool operator !=(string left, EPid right) => !(left == right);

        public EPid(object pid)
        {
            Id = pid as string;
            CheckId();
        }

        public EPid(CharBuffer64 buffer)
        {
            Id = buffer.Text;
            CheckId();
        }

        [SuppressMessage("ReSharper", "ReturnValueOfPureMethodIsNotUsed")]
        public bool IsValidEpidFormat
        {
            get
            {
                try
                {
                    if (Split.Length < 8)
                    {
                        return false;
                    }

                    OsId.ToString();
                    GroupId.ToString();
                    KeyId.ToString();
                    KeyType.ToString();
                    Date.ToString(CultureInfo.CurrentCulture);
                    OsName.ToString();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public ProductKeyConfigurationConfigurationsConfiguration TryGetEpidPkConfig(out IOrderedEnumerable<KmsItem> kmsItems, out CsvlkItem csvlkRule)
        {
            return TryGetEpidPkConfig(this, out kmsItems, out csvlkRule);
        }

        public static ProductKeyConfigurationConfigurationsConfiguration TryGetEpidPkConfig(EPid pid, out IOrderedEnumerable<KmsItem> kmsItems, out CsvlkItem csvlkRule)
        {
            kmsItems = null;
            csvlkRule = null;

            try
            {
                if (pid.KeyId > 999999999 || pid.GroupId > 99999)
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }

            ProductKeyConfigurationConfigurationsConfiguration[] configs = MainWindow.CsvlkConfigs.Where(c => c.RefGroupId == pid.GroupId).ToArray();
            IEnumerable<KmsGuid> configIds = configs.Select(c => c.ActConfigGuid);
            ProductKeyConfigurationKeyRangesKeyRange range = MainWindow.CsvlkRanges.SingleOrDefault(r => configIds.Contains(r.RefActConfigGuid) && r.Start <= pid.KeyId && pid.KeyId <= r.End);
            if (range == null)
            {
                return null;
            }

            ProductKeyConfigurationConfigurationsConfiguration config = configs.First(c => c.ActConfigGuid == range.RefActConfigGuid);
            if (KmsLists.CsvlkItemList[config.ActConfigGuid] == null)
            {
                return config;
            }

            csvlkRule = KmsLists.CsvlkItemList[config.ActConfigGuid];
            kmsItems = csvlkRule.Activates.Select(g => MainWindow.KmsProductList[g.Guid]).Where(k => k != null).OrderBy(k => k.DisplayName);

            return config;
        }

        private void CheckId()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new ArgumentNullException(nameof(Id), "must be a non-empty string");
            }

            Split = Id.Split(new[] { '-' }, 8);
        }

        public uint OsId
        {
            get
            {
                if (!Regex.IsMatch(OsIdString, "^[0-9]{5}$"))
                {
                    throw new FormatException($"The OS id \"{OsIdString}\" in the ePID is not in the format #####");
                }

                return uint.Parse(OsIdString, CultureInfo.InvariantCulture);
            }
        }

        public uint GroupId
        {
            get
            {
                if (Split.Length < 2)
                {
                    throw new FormatException("The ePid has no group id");
                }

                if (!Regex.IsMatch(GroupIdString, "^[0-9]{5}$"))
                {
                    throw new FormatException($"The group id \"{GroupIdString}\" in the ePID is not in the format #####");
                }

                return uint.Parse(GroupIdString, CultureInfo.InvariantCulture);
            }
        }

        public uint KeyId
        {
            get
            {
                if (Split.Length < 4)
                {
                    throw new FormatException("The ePid has no key serial number");
                }

                if (!Regex.IsMatch(KeyIdString, "^[0-9]{3}\x2D[0-9]{6}$"))
                {
                    throw new FormatException($"The key serial number \"{KeyIdString}\" in the ePID is not in the format ###-######");
                }

                return uint.Parse(Split[2] + Split[3], CultureInfo.InvariantCulture);
            }
        }

        public byte KeyType
        {
            get
            {
                if (Split.Length < 5)
                {
                    throw new FormatException("The ePID has no key type");
                }

                if (!Regex.IsMatch(Split[4], "^[0-9]{2}$"))
                {
                    throw new FormatException($"key type \"{Split[4]}\" in ePID is not in the format ##");
                }

                return byte.Parse(Split[4], CultureInfo.InvariantCulture);
            }
        }

        public CultureInfo Culture
        {
            get
            {
                if (Split.Length < 6)
                {
                    throw new FormatException("The ePID has no LCID");
                }

                if (!Regex.IsMatch(Split[5], "^[0-9]{1,5}$"))
                {
                    throw new FormatException($"LCID \"{Split[5]}\" in ePID is not in the format #####");
                }

                int tempInt = int.Parse(Split[5], CultureInfo.InvariantCulture);
                CultureInfo culture;

                try
                {
                    culture = new CultureInfo(tempInt);
                }
                catch (CultureNotFoundException)
                {
                    throw new CultureNotFoundException("The LCID in the ePID is unsupported", tempInt, (Exception)null);
                }

                if (!Regex.IsMatch(culture.Name.ToUpper(), @"^[A-Z\x2D]{2,}\x2D[A-Z,0-9]{2,}$"))
                {
                    throw new CultureNotFoundException("The LCID in the ePID is unsupported", tempInt, (Exception)null);
                }

                return culture;
            }
        }

        public DateTime Date
        {
            get
            {
                if (Split.Length < 8)
                {
                    throw new FormatException("The ePID has no date");
                }

                if (!Regex.IsMatch(Split[7], "^[0-9]{7}$"))
                {
                    throw new FormatException($"The ePID date \"{Split[7]}\" is not in the format DDDYYYY");
                }

                DateTime date = IllegalDate;
                uint dayOfYear = uint.Parse(Split[7].Substring(0, 3), CultureInfo.InvariantCulture);
                uint year = uint.Parse(Split[7].Substring(3, 4), CultureInfo.InvariantCulture);
                if (year == 0)
                {
                    throw new ArgumentOutOfRangeException("There is no year 0 in the proleptic gregorian calendar", (Exception)null);
                }

                date = date.AddYears(Convert.ToInt32(Split[7].Substring(3, 4)) - 1601);
                date = date.AddDays(dayOfYear - 1);

                if (year != date.Year || dayOfYear != date.DayOfYear)
                {
                    throw new ArgumentOutOfRangeException("The date in the ePID is not valid", (Exception)null);
                }

                return date;
            }
        }

        public uint OsBuild
        {
            get
            {
                if (Split.Length < 7)
                {
                    throw new FormatException("The ePID has no OS build number");
                }

                string[] os = Split[6].Split('.');
                if (os.Length != 2 || os[1] != "0000" || !Regex.IsMatch(os[0], "^[0-9]{4,5}$"))
                {
                    throw new FormatException($"The ePID OS build number \"{Split[6]}\" is not in the format #####.0000");
                }

                uint osBuild = uint.Parse(os[0], CultureInfo.InvariantCulture);
                return osBuild;
            }
        }

        public string OsName
        {
            get
            {
                IReadOnlyList<WinBuild> winBuilds = KmsLists.KmsData.WinBuilds.OrderBy(b => b.BuildNumber).ToArray() as IReadOnlyList<WinBuild>;

                if (OsBuild < (uint)winBuilds[0].BuildNumber)
                {
                    return $"Older than {winBuilds[0].DisplayName}";
                }

                if (OsBuild > (uint)winBuilds[winBuilds.Count - 1].BuildNumber)
                {
                    return $"Newer than {winBuilds[winBuilds.Count - 1].DisplayName}";
                }

                for (int i = 0; i < winBuilds.Count; i++)
                {
                    if (OsBuild == (uint)winBuilds[i].BuildNumber)
                    {
                        return winBuilds[i].DisplayName;
                    }

                    if (i > 0 && OsBuild > (uint)winBuilds[i - 1].BuildNumber && OsBuild < (uint)winBuilds[i].BuildNumber)
                    {
                        return $"Beta/Preview of {winBuilds[i].DisplayName}";
                    }
                }

                throw new ApplicationException("Error in determining Windows build number");
            }
        }

        public int GetRemainingActivationsOnline()
        {
            return PidGen.GetRemainingActivationsOnline(Id);
        }

        public string LongDateString => Date.ToLongDateString();
        public string LongOsName => OsName + (Culture != null ? " " + Culture.DisplayName : "");
    }

}
