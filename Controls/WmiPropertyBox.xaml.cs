using HGM.Hotbird64.LicenseManager.Contracts;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HGM.Hotbird64.LicenseManager.Controls
{
    using WmiProperty = WPF.Converters.WmiProperty;

    public partial class WmiPropertyBox
    {
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(WmiPropertyBox), new PropertyMetadata(true, OnIsReadOnlyChanged));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (WmiPropertyBox)d;
            self.Box.IsReadOnly = (bool)e.NewValue;
        }

        public static DependencyProperty WmiPropertyProperty = DependencyProperty.Register
        (
            nameof(WmiProperty),
            typeof(IWmiProperty),
            typeof(WmiPropertyBox),
            new PropertyMetadata(null, WmiPropertyChanged)
        );

        public static DependencyProperty PropertyNameProperty = DependencyProperty.Register
        (
            nameof(PropertyName),
            typeof(string),
            typeof(WmiPropertyBox),
            new PropertyMetadata(null, WmiPropertyChanged)
        );

        public static DependencyProperty ConverterProperty = DependencyProperty.Register
        (
            nameof(Converter),
            typeof(WmiProperty),
            typeof(WmiPropertyBox),
            new PropertyMetadata(new WmiProperty(), WmiPropertyChanged)
        );

        public WmiPropertyBox()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                if (ContextMenu == null)
                {
                    Box.ContextMenu = new ContextMenu();
                }
                else
                {
                    Box.ContextMenu = ContextMenu;
                    ContextMenu = null;
                }

                Box.ContextMenu.Items.Add(new MenuItem { Command = ApplicationCommands.Cut });
                Box.ContextMenu.Items.Add(new MenuItem { Command = ApplicationCommands.Copy });
                Box.ContextMenu.Items.Add(new MenuItem { Command = ApplicationCommands.Undo });
                Box.ContextMenu.Items.Add(new MenuItem { Command = ApplicationCommands.Redo });
                Box.ContextMenu.Items.Add(new MenuItem { Command = ApplicationCommands.SelectAll });
            };
        }

        public WmiProperty Converter
        {
            get => (WmiProperty)GetValue(ConverterProperty);
            set => SetValue(ConverterProperty, value);
        }

        public IWmiProperty WmiProperty
        {
            get => (IWmiProperty)GetValue(WmiPropertyProperty);
            set => SetValue(WmiPropertyProperty, value);
        }

        public string PropertyName
        {
            get => (string)GetValue(PropertyNameProperty);
            set => SetValue(PropertyNameProperty, value);
        }

        private static void WmiPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            WmiPropertyBox self = (WmiPropertyBox)d;

            if (e.Property.Name == nameof(WmiProperty))
            {
                if (e.NewValue is IWmiProperty newValue)
                {
                    newValue.PropertyChanged += self.WmiPropertyChanged;
                }

                if (e.OldValue is IWmiProperty oldValue)
                {
                    oldValue.PropertyChanged -= self.WmiPropertyChanged;
                }
            }
            Update(self);
        }

        private void WmiPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Update(this);
        }

        private static void Update(WmiPropertyBox self)
        {
            if (self.PropertyName == null || self.Converter == null || self.WmiProperty == null) return;
            self.Box.IsReadOnly = self.IsReadOnly;
            self.Box.Text = (string)self.Converter.Convert(self.WmiProperty, typeof(string), null, CultureInfo.CurrentCulture) ?? string.Empty;
            self.Converter.PropertyName = self.PropertyName;
            self.Visibility = self.Converter.IsUnsupported && !self.Converter.ShowAllFields ? Visibility.Collapsed : Visibility.Visible;
            self.IsEnabled = !self.Converter.IsUnsupported;
        }
    }
}
