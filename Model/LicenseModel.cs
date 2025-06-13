using HGM.Hotbird64.LicenseManager.Contracts;
using HGM.Hotbird64.LicenseManager.Extensions;
using System.Management;

namespace HGM.Hotbird64.LicenseManager.Model
{
    public class LicenseModel : PropertyChangeBase, IWmiProperty
    {
        private LicenseMachine.ProductLicense selectedLicense;
        public LicenseMachine.ProductLicense SelectedLicense
        {
            get => selectedLicense;
            set => this.SetProperty(ref selectedLicense, value);
        }

        private bool developerMode;
        public bool DeveloperMode
        {
            get => developerMode;
            set => this.SetProperty(ref developerMode, value);
        }

        private bool showAllFields;
        public bool ShowAllFields
        {
            get => showAllFields;
            set => this.SetProperty(ref showAllFields, value);
        }

        public ManagementObject Property => SelectedLicense?.License;
        public LicenseMachine.LicenseProvider LicenseProvider { get; set; }
    }
}
