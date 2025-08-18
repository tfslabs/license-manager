using HGM.Hotbird64.LicenseManager.Contracts;
using HGM.Hotbird64.LicenseManager.Controls;
using HGM.Hotbird64.LicenseManager.Extensions;
using HGM.Hotbird64.LicenseManager.Model;
using HGM.Hotbird64.Vlmcs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HGM.Hotbird64.LicenseManager
{
    public partial class GetCID : Window, IHaveNotifyOfPropertyChange
    {
        internal LicenseMachine Machine;
        public bool IsClosed { get; private set; }
        public static ProductKeyConfiguration PKeyConfig => ProductBrowser.PKeyConfig;
        public static IList<PKeyConfigFile> PKeyConfigFiles => ProductBrowser.PKeyConfigFiles;
        public static ISet<ProductKeyConfigurationConfigurationsConfiguration> KeyConfigs => ((ProductKeyConfigurationConfigurations)PKeyConfig.Items.Single(i => i is ProductKeyConfigurationConfigurations)).Configuration;
        public static ISet<ProductKeyConfigurationPublicKeysPublicKey> PublicKeys => ((ProductKeyConfigurationPublicKeys)PKeyConfig.Items.Single(i => i is ProductKeyConfigurationPublicKeys)).PublicKey;
        public static ISet<ProductKeyConfigurationKeyRangesKeyRange> KeyRanges => ((ProductKeyConfigurationKeyRanges)PKeyConfig.Items.Single(i => i is ProductKeyConfigurationKeyRanges)).KeyRange;
        public static RoutedUICommand CheckEpid, AutoSizeWindow;
        public static CultureInfo OsSystemLocale;
        public event PropertyChangedEventHandler PropertyChanged;

        private int selectedProductIndex = -1;

        private readonly GetCID_WebService WebService_Handler = new();

        public void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void License_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyOfPropertyChange(nameof(License));
        }

        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            string text = (sender as WmiPropertyBox)?.Box.Text;

            if (text == null || (!Regex.IsMatch(text, PidGen.EpidPattern) && !Regex.IsMatch(text, App.GuidPattern)))
            {
                return;
            }

            e.CanExecute = true;
        }


        public int SelectedProductIndex
        {
            get => selectedProductIndex;
            set => this.SetProperty(ref selectedProductIndex, value, postAction: () =>
            {
                try
                {
                    License.LicenseProvider = Machine.LicenseProvidersList[Machine.ProductLicenseList[value].ServiceIndex];
                    License.SelectedLicense = Machine.ProductLicenseList[value];
                }
                catch (Exception)
                {
                    //ignored because of the useless of that
                }
            });
        }

        static GetCID()
        {
            CheckEpid = new RoutedUICommand("Get Info", nameof(CheckEpid), typeof(ScalableWindow));
            AutoSizeWindow = new RoutedUICommand("Auto Size Window", nameof(AutoSizeWindow), typeof(ScalableWindow));
        }

        public GetCID()
        {
            InitializeComponent();
            if (!WebService_Handler.CheckConnection())
            {
                MessageBox.Show(
                    "Your Internet connection to the Microsoft Customer Service is not stable. Confirmation ID may not get.",
                    "Error while connecting to Microsoft",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
            }

            Loaded += (s, e) =>
            {
                License.PropertyChanged += License_PropertyChanged;
            };

            Unloaded += (s, e) =>
            {
                License.PropertyChanged -= License_PropertyChanged;
            };
        }

        public bool ControlsEnabled
        {
            set
            {
                EpidBox.IsEnabled =
                    ComboBoxProductId.IsEnabled =
                        TextBoxLicenseStatusReason.IsEnabled =
                            PhoneInstallationIdBox.IsEnabled = value;

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
                IsProgressBarRunning = true;
                GetCIDLabelStatus.Text = "Processing...";
                CIDCode.Text = Regex.Replace(WebService_Handler.CallWebService(installationId, epid), ".{6}", "$0-").TrimEnd('-');
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

            IsProgressBarRunning = false;
            GetCIDLabelStatus.Text = "Completed";
        }

        private async Task Refresh()
        {
            ControlsEnabled = false;
            try
            {
                IsProgressBarRunning = true;
                await Task.Run(() =>
                {
                    Machine.RefreshLicenses();
                });

                FillLicenseComboBox();
                IsProgressBarRunning = false;
                GetCIDLabelStatus.Text = "Ready";
            }
            catch (Exception ex)
            {
                GetCIDLabelStatus.Text = "Error";
                IsProgressBarRunning = false;
                MessageBox.Show(this, ex.Message, "Error getting License information", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ControlsEnabled = true;
            }
        }

        private async void GetCID_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => Machine = new LicenseMachine());
            OsSystemLocale = (Machine?.SysInfo?.OsInfo.Locale != null) ? Machine.SysInfo.OsInfo.Locale : OsSystemLocale;
            GetCIDLabelStatus.Text = "Gathering Data...";
            await Refresh();
        }

        private void SelectedProductChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxProductId.SelectedIndex == -1)
            {
                CIDCode.Clear();
                CIDCode.Background = new SolidColorBrush(Colors.Transparent);
                GetCIDLabelStatus.Text = "Error";
                return;
            } //-1 for unselected

            PhoneInstallationIdBox.IsReadOnly = EpidBox.IsReadOnly = true;
            LicenseMachine.ProductLicense l = Machine.ProductLicenseList[ComboBoxProductId.SelectedIndex];
            WmiProperty w = new("Version " + Machine.LicenseProvidersList[l.ServiceIndex].Version, l.License, false);
            w.DisplayPropertyAsLicenseStatus([TextBoxLicenseStatusReason], TextBoxLicenseStatusReason);

            CIDCode.Clear();
            CIDCode.Background = new SolidColorBrush(Colors.Transparent);
            GetCIDLabelStatus.Text = "Ready";
        }

        private LicenseModel license = new();
        public LicenseModel License
        {
            get => license;
            set => this.SetProperty(ref license, value);
        }

        private void FillLicenseComboBox()
        {
            bool foundItem = false;
            int index = 0;

            ComboBoxProductId.Items.Clear();

            foreach (LicenseMachine.ProductLicense l in Machine.ProductLicenseList)
            {
                string description = l.License["Description"].ToString();
                string name = l.License["Name"].ToString();
                ComboBoxProductId.Items.Add(
                    description.Substring(0, Math.Min(100, description.Length)) +
                    ": " +
                    name.Substring(0, Math.Min(100, name.Length))
                );
                index++;
            }

            if (!foundItem)
            {
                ComboBoxProductId.SelectedIndex = 0;
            }
        }

        private async void Refresh_Click(object sender, EventArgs e)
        {
            GetCIDLabelStatus.Text = "Gathering Data...";
            await Refresh();
        }
    }
}