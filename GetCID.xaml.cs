using System;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HGM.Hotbird64.LicenseManager
{
    public partial class GetCID : Window
    {
        internal LicenseMachine Machine;

        public GetCID()
        {
            InitializeComponent();

            if (CheckConnection() == false)
            {
                MessageBox.Show(
                    "Your Internet connection to the Microsoft Customer Service is not stable. Confirmation ID may not get.",
                    "Error while connecting to Microsoft",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
            }
        }

        private static void OpenUrlContributor(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/anhvlt-2k6",
                UseShellExecute = true
            });
        }

        private static bool CheckConnection()
        {
            bool isInternetTrouble = true;
            string[] GetCIDTargets =
{
                "http://www.microsoft.com/BatchActivationService/BatchActivate",
                "https://activation.sls.microsoft.com/BatchActivation/BatchActivation.asmx",
                "http://schemas.xmlsoap.org/soap/envelope/",
                "http://www.microsoft.com/BatchActivationService",
                "http://www.microsoft.com/DRM/SL/BatchActivationRequest/1.0",
                "http://www.microsoft.com/DRM/SL/BatchActivationResponse/1.0"
            };

            foreach (var Target in GetCIDTargets)
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Target);
                    request.Method = "HEAD";
                    request.Timeout = 1000;

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        isInternetTrouble |= response.StatusCode == HttpStatusCode.OK;
                    }
                }
                catch
                {
                    isInternetTrouble &= false;
                }
            }

            return isInternetTrouble;
        }


        internal bool IsProgressBarRunning
        {
            get => GetCIDProgressBar.IsIndeterminate;
            set
            {
                GetCIDProgressBar.IsIndeterminate = value;
                GetCIDProgressBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ProductList(object sender, RoutedEventArgs e) { }

        public bool ControlsEnabled
        {
            set => EpidBox.IsEnabled =
                     ActStatus.IsEnabled =
                        ProductListComboxBox.IsEnabled =
                            PhoneInstallationIdBox.IsEnabled = value;
        }


        private async void GetCID_Loaded(object sender, RoutedEventArgs e)
        {
            ControlsEnabled = false;
            TextBoxInfoText.Text = string.Empty;
            try
            {
                try
                {
                    IsProgressBarRunning = true;
                    await Task.Run(() =>
                    {
                        Machine.RefreshLicenses();
                        Machine.GetSystemInfo();
                    });

                    IsProgressBarRunning = false;
                }
                catch (Exception ex)
                {
                    GetCIDLabelStatus.Text = "Error";
                    IsProgressBarRunning = false;
                    MessageBox.Show(this, ex.Message, "Error getting License information", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                GetCIDLabelStatus.Text = "Ready";
            }
            finally
            {
                ControlsEnabled = true;
            }
        }

        private void SelectedProductChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProductListComboxBox.SelectedIndex < 0)
            {
                return;
            }

            LicenseMachine.ProductLicense l = Machine.ProductLicenseList[ProductListComboxBox.SelectedIndex];

            WmiProperty w = new WmiProperty("Version " + Machine.LicenseProvidersList[l.ServiceIndex].Version, l.License, false);

            w.DisplayPropertyAsLicenseStatus(new Control[] { ActStatus }, ActStatus);
        }
    }
}
