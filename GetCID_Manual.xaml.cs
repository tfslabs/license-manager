using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;

namespace HGM.Hotbird64.LicenseManager
{
    public partial class GetCID_Manual : Window
    {
        private readonly GetCID_WebService webService = new();

        public GetCID_Manual()
        {
            InitializeComponent();
            if (!webService.CheckConnection())
            {
                MessageBox.Show(
                    "Your Internet connection to the Microsoft Customer Service is not stable. Confirmation ID may not get.",
                    "Error while connecting to Microsoft",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
            }
        }

        private async void GetCID_Manual_Loaded(object sender, RoutedEventArgs e)
        {
            ControlsEnabled = true;
            IsProgressBarRunning = false;
            GetCIDLabelStatus.Text = "Ready";
        }

        public bool ControlsEnabled
        {
            set
            {
                EpidBox.IsEnabled = PhoneInstallationIdBox.IsEnabled = GetCIDButton.IsEnabled = value;
            }
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

        private void Button_GetCID_Clicked(object sender, RoutedEventArgs e)
        {
            string installationId = string.Concat(PhoneInstallationIdBox.ToString().Replace("HGM.Hotbird64.LicenseManager.Controls.WmiPropertyBox: ", "").Replace("-", "").Trim());
            string epid = string.Concat(EpidBox.ToString().Replace("HGM.Hotbird64.LicenseManager.Controls.WmiPropertyBox: ", "").Where(c => char.IsDigit(c) || c == '.' || c == '-')).Trim();

            try
            {
                ControlsEnabled = false;
                IsProgressBarRunning = true;
                GetCIDLabelStatus.Text = "Processing...";
                CIDCode.Text = Regex.Replace(webService.CallWebService(installationId, epid), ".{6}", "$0-").TrimEnd('-');
            }
            catch (WebException webEx)
            {
                MessageBox.Show(this, "Error connecting to the web service: " + webEx.Message, "Web Service Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CIDCode.Text = $"Error: WebException {webEx.Message}";
            }
            catch (IOException ioEx)
            {
                MessageBox.Show(this, "I/O error: " + ioEx.Message, "I/O Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CIDCode.Text = $"Error: IOException {ioEx.Message}";
            }
            catch (NullReferenceException nullPtr)
            {
                MessageBox.Show(this, "Null reference error: " + nullPtr.Message, "Null Reference Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CIDCode.Text = $"Error: NullReferenceException {nullPtr.Message}";
            }
            catch (InvalidOperationException ioExp)
            {
                MessageBox.Show(this, "Invalid operation error: " + ioExp.Message, "Invalid Operation", MessageBoxButton.OK, MessageBoxImage.Error);
                CIDCode.Text = $"Error: InvalidOperationException {ioExp.Message}";
            }
            finally
            {
                ControlsEnabled = true;
                IsProgressBarRunning = false;
                GetCIDLabelStatus.Text = "Completed";
            }
        }
    }
}
