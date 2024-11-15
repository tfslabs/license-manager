using System;
using System.Reflection;
using System.Windows;

namespace HGM.Hotbird64.LicenseManager
{
    public partial class AboutBox
    {
        public AboutBox(MainWindow mainWindow) : base(mainWindow)
        {
            InitializeComponent();
            TopElement.LayoutTransform = Scaler;
            System.Version version = Assembly.GetCallingAssembly().GetName().Version;
            LabelVersion.Content = "Version " + version.ToString() + (version.MinorRevision < 2300 ? $" Beta {version.MinorRevision}" : "") + $" {IntPtr.Size << 3}-bit";
        }

        private void Button_Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
