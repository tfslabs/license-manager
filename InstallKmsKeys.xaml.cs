using HGM.Hotbird64.Vlmcs;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

// ReSharper disable once CheckNamespace

namespace HGM.Hotbird64.LicenseManager
{
    public partial class InstallKmsKeys
    {
        private readonly LicenseMachine machine;
        private readonly ObservableCollection<KmsLicense> kmsLicenses = new ObservableCollection<KmsLicense>();
        public bool NeedsRefresh { get; private set; }

        public InstallKmsKeys(MainWindow mainWindow, LicenseMachine machine) : base(mainWindow)
        {
            bool isWindowsActivated;
            this.machine = machine;
            InitializeComponent();
            DataContext = this;
            TopElement.LayoutTransform = Scaler;

            Loaded += (s, e) =>
            {
                machine.GetKmsLicenses(out isWindowsActivated, kmsLicenses);
                DataGrid.ItemsSource = kmsLicenses;
                DataGrid.Items.SortDescriptions.Add(new SortDescription(nameof(KmsLicense.DisplayName), ListSortDirection.Ascending));

                if (!isWindowsActivated)
                {
                    KmsLicense firstWindowsLicense = kmsLicenses.FirstOrDefault(l => l.ApplicationID == Kms.WinGuid);
                    if (firstWindowsLicense != null) firstWindowsLicense.IsRadioButtonChecked = true;
                }

                foreach (KmsLicense kmsLicense in kmsLicenses.Where(l => l.ApplicationID != Kms.WinGuid))
                {
                    kmsLicense.IsCheckBoxChecked = kmsLicense.IsNotActivated;
                }

                Install_Click(s, e);
            };
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Background = App.DatagridBackgroundBrushes[e.Row.GetIndex() % App.DatagridBackgroundBrushes.Count];
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (ProgressBar.IsIndeterminate)
            {
                e.Cancel = true;
                MessageBox.Show(this, "Please wait for the current action to complete", "Be Patient", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }

            base.OnClosing(e);
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Visibility = Visibility.Visible;
            InstallButton.Visibility = Visibility.Collapsed;
            ResultColumn.Visibility = Visibility.Visible;
            LicenseStatusColumn.Visibility = Visibility.Collapsed;
            PartialProductKeyColumn.Visibility = Visibility.Collapsed;
            CancelButton.IsEnabled = false;
            LabelStatus.Text = "Installing GVLKs...";

            try
            {
                foreach (KmsLicense kmsLicense in kmsLicenses) kmsLicense.IsControlEnabled = false;

                foreach (KmsLicense kmsLicense in kmsLicenses.Where(l => l.ApplicationID == Kms.WinGuid ? !l.IsRadioButtonChecked : !l.IsCheckBoxChecked))
                {
                    kmsLicense.InstallMessage = "N/A";
                    kmsLicense.InstallToolTip = "The GVLK has not been selected for installation";
                    kmsLicense.InstallSuccess = false;
                }

                await Task.Run(() =>
                {
                    Parallel.ForEach(kmsLicenses.Where(l => l.ApplicationID == Kms.WinGuid ? l.IsRadioButtonChecked : l.IsCheckBoxChecked), kmsLicense =>
            {
                try
                {
                    string licenseProvider = machine.InstallProductKey((string)kmsLicense.Gvlk);
                    NeedsRefresh = true;

                    Dispatcher.InvokeAsync(() =>
            {
                kmsLicense.InstallMessage = "Success";
                kmsLicense.InstallToolTip = $"Installed by {licenseProvider}";
                kmsLicense.InstallSuccess = true;
            });
                }
                catch (Exception ex)
                {
                    Dispatcher.InvokeAsync(() =>
            {
                kmsLicense.InstallMessage = "Failure";
                kmsLicense.InstallToolTip = ex.Message;
                kmsLicense.InstallSuccess = false;
            });
                }
            });
                });
            }
            finally
            {
                CancelButton.IsEnabled = true;
                LabelStatus.Text = "Finished";
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressBar.IsIndeterminate = false;
                if (NeedsRefresh) MainWindow.Button_Refresh_Clicked(null, null);
            }
        }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            InstallButton.IsEnabled = kmsLicenses.Any(l => l.IsCheckBoxChecked || l.IsRadioButtonChecked);
        }
    }
}
