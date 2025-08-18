using System.ComponentModel;
using System.Management;

namespace HGM.Hotbird64.LicenseManager.Contracts
{
    public interface IWmiProperty : INotifyPropertyChanged
    {
        ManagementObject Property { get; }
        bool ShowAllFields { get; }
        bool DeveloperMode { get; }
        LicenseMachine.LicenseProvider LicenseProvider { get; set; }
    }
}
