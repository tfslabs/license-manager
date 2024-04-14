using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HGM.Hotbird64.LicenseManager.Contracts
{
    public interface IHaveNotifyOfPropertyChange : INotifyPropertyChanged
    {
        void NotifyOfPropertyChange([CallerMemberName] string propertyName = null);
    }
}
