using System.Reflection;
using System.Windows;

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.LicenseManager
{
    public partial class AboutBox
    {
        public AboutBox(MainWindow mainWindow) : base(mainWindow)
        {
            InitializeComponent();
            TopElement.LayoutTransform = Scaler;
            System.Version version = Assembly.GetCallingAssembly().GetName().Version;
            LabelVersion.Content = "Version " + version.ToString(3) + (version.MinorRevision < 2300 ? $" Beta {version.MinorRevision}" : "");
        }

        private void button_Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
