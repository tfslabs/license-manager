using HGM.Hotbird64.Vlmcs;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace HGM.Hotbird64.LicenseManager
{
    public abstract class ConverterBase : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);

        public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public abstract class Bool2Anything<T> : ConverterBase
    {
        public T True { get; set; }
        public T False { get; set; }
        public T Null { get; set; }

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? True : False;
            }

            return Null;
        }
    }

    public class Bool2Brush : Bool2Anything<Brush>
    {
        public Bool2Brush()
        {
            True = Brushes.LightGreen;
            False = Brushes.OrangeRed;
            Null = Brushes.Yellow;
        }
    }

    public class ValidEpid2Visible : ConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string stringValue = value?.ToString();

            if (stringValue != null && Regex.IsMatch(stringValue, PidGen.EpidPattern))
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }
    }
}
