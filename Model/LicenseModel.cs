using HGM.Hotbird64.LicenseManager.Contracts;
using HGM.Hotbird64.LicenseManager.Extensions;
using System.Management;

namespace HGM.Hotbird64.LicenseManager.Model
{
    public class LicenseModel : PropertyChangeBase, IWmiProperty
    {
        public LicenseMachine.ProductLicense SelectedLicense
        {
            get;
            set => this.SetProperty(ref field, value);
        }
        public bool DeveloperMode
        {
            get;
            set => this.SetProperty(ref field, value);
        }
        public bool ShowAllFields
        {
            get;
            set => this.SetProperty(ref field, value);
        }

        public ManagementObject Property => SelectedLicense?.License;
        public LicenseMachine.LicenseProvider LicenseProvider { get; set; }
    }
}
