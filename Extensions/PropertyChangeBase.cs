using HGM.Hotbird64.LicenseManager.Contracts;
using System;
using System.Runtime.CompilerServices;

namespace HGM.Hotbird64.LicenseManager.Extensions
{
    public static class PropertyChangeBaseExtensions
    {
        public static void SetProperty<T>(this IHaveNotifyOfPropertyChange property, ref T propertyField, T value, [CallerMemberName] string propertyName = null, Action preAction = null, Action postAction = null)
        {
            if (ReferenceEquals(property, propertyField) || (propertyField != null && propertyField.Equals(value)))
            {
                return;
            }

            preAction?.Invoke();
            propertyField = value;
            property.NotifyOfPropertyChange(propertyName);
            postAction?.Invoke();
        }
    }
}
