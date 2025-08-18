using HGM.Hotbird64.Vlmcs;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace HGM.Hotbird64.LicenseManager
{
    public class KmsLicense : PropertyChangeBase
    {
        private KmsGuid id;
        public IEnumerable<KmsLicense> List;

        private string installMessage;
        public string InstallMessage
        {
            get => installMessage;
            set
            {
                if (value == installMessage) return;
                installMessage = value;
                NotifyOfPropertyChange();
            }
        }

        private string installToolTip;
        public string InstallToolTip
        {
            get => installToolTip;
            set
            {
                if (value == installToolTip) return;
                installToolTip = value;
                NotifyOfPropertyChange();
            }
        }

        public BinaryProductKey? Gvlk { get; private set; }
        public KmsGuid ApplicationID { get; set; }
        public string PartialProductKey { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public LicenseStatus LicenseStatus { get; set; }
        public bool? IsGeneratedGvlk;

        private bool installSuccess;
        public bool InstallSuccess
        {
            get => installSuccess;
            set
            {
                if (value == installSuccess) return;
                installSuccess = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(InstallMessageColor));
            }
        }

        private bool isControlEnabled = true;
        public bool IsControlEnabled
        {
            get => isControlEnabled;
            set
            {
                if (isControlEnabled == value) return;
                isControlEnabled = value;
                NotifyOfPropertyChange();
            }
        }

        private bool isRadioButtonChecked;
        public bool IsRadioButtonChecked
        {
            get => isRadioButtonChecked;
            set
            {
                if (value)
                {
                    if (isRadioButtonChecked && !List.Any(l => l != this && l.IsRadioButtonChecked))
                    {
                        IsRadioButtonChecked = false;
                        return;
                    }

                    foreach (KmsLicense kmsLicense in List.Where(l => l != this)) kmsLicense.IsRadioButtonChecked = false;
                }

                if (isRadioButtonChecked == value) return;
                isRadioButtonChecked = value;
                NotifyOfPropertyChange();
            }
        }

        private bool isCheckBoxChecked;
        public bool IsCheckBoxChecked
        {
            get => isCheckBoxChecked;
            set
            {
                if (isCheckBoxChecked == value) return;
                isCheckBoxChecked = value;
                NotifyOfPropertyChange();
            }
        }

        public KmsGuid ID
        {
            get => id;
            set
            {
                if (id == value) return;
                id = value;
                SkuItem skuItem = KmsLists.SkuItemList[value];
                Gvlk = skuItem?.Gvlk == null ? null : (BinaryProductKey?)skuItem.Gvlk;
                IsGeneratedGvlk = skuItem?.IsGeneratedGvlk;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(Gvlk));
                NotifyOfPropertyChange(nameof(GvlkColor));
                NotifyOfPropertyChange(nameof(GvlkToolTip));
            }
        }

        public override string ToString() => DisplayName;

        public string DisplayName
        {
            get
            {
                SkuItem skuItem = KmsLists.SkuItemList[ID];
                return skuItem?.ToString() ?? Name ?? Description;
            }
        }

        public bool HasProductKey => PartialProductKey != null;
        public bool IsActivated => LicenseStatus == LicenseStatus.Licensed;
        public bool IsNotActivated => !IsActivated;
        public Visibility CheckBoxVisibility => IsGeneratedGvlk == null || ApplicationID == Kms.WinGuid ? Visibility.Collapsed : Visibility.Visible;
        public Visibility RadioButtonVisibility => IsGeneratedGvlk == null || ApplicationID != Kms.WinGuid ? Visibility.Collapsed : Visibility.Visible;
        public string LicenseStatusText => Model.LicenseStatus.GetText(LicenseStatus);
        public Brush LicenseColor => IsActivated ? Brushes.Green : Brushes.Red;
        public Brush InstallMessageColor => !IsRadioButtonChecked && !IsCheckBoxChecked ? SystemColors.WindowTextBrush : InstallSuccess ? Brushes.Green : Brushes.Red;
        public Brush GvlkColor => !IsGeneratedGvlk.HasValue ? Brushes.Red : IsGeneratedGvlk.Value ? Brushes.Yellow : Brushes.Transparent;
        public string GvlkToolTip => !IsGeneratedGvlk.HasValue ? "No GVLK in database" : IsGeneratedGvlk.Value ? "This GVLK is user-generated" : "This GVLK is genuine";
    }
}