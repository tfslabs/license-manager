using HGM.Hotbird64.LicenseManager.Contracts;
using LicenseManager.Annotations;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HGM.Hotbird64.LicenseManager
{
    public abstract class PropertyChangeBase : IHaveNotifyOfPropertyChange
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        public virtual void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
