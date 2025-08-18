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
using System.Threading;
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
        public static InputGestureCollection CtrlE = [];
        public static InputGestureCollection CtrlW = [];
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
            CtrlE.Add(new KeyGesture(Key.E, ModifierKeys.Control));
            CtrlW.Add(new KeyGesture(Key.W, ModifierKeys.Control));
            CheckEpid = new RoutedUICommand("Get Info", nameof(CheckEpid), typeof(ScalableWindow), CtrlE);
            AutoSizeWindow = new RoutedUICommand("Auto Size Window", nameof(AutoSizeWindow), typeof(ScalableWindow), CtrlW);
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

        private async void Button_InstallCID_Clicked(object sender, RoutedEventArgs e)
        {
            int index = ComboBoxProductId.SelectedIndex;

            if (MessageBox.Show(
                    this,
                    "Are you sure you want to install Confirmation ID for " + Machine.ProductLicenseList[index].License["Description"] + "?",
                    "Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                ) !=
                MessageBoxResult.Yes)
            {
                e.Handled = false;
                return;
            }

            string installationId = string.Concat(PhoneInstallationIdBox.ToString()
                .Replace("HGM.Hotbird64.LicenseManager.Controls.WmiPropertyBox: ", "").Replace("-", "").Trim());
            string confirmationId = string.Concat(CIDCode.ToString().Trim());

            IsProgressBarRunning = true;
            ControlsEnabled = false;
            GetCIDLabelStatus.Text = "Installing CID";

            try
            {
                await Task.Run(() => Machine.InstallConfirmationID(index, installationId, confirmationId));

                new Thread(() => Dispatcher.Invoke(() => MessageBox.Show(
                    this,
                    "Confirmation ID has been installed successfully",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                ))).Start();

                Refresh_Click(sender, e);
            }
            catch (Exception ex)
            {
                GetCIDLabelStatus.Text = "Installing Confirmation ID Error";
                IsProgressBarRunning = false;
                MessageBox.Show(this, 
                    "The CID for the following product " + 
                    Machine.ProductLicenseList[index].License["Description"] + 
                    " could not be uninstalled: " + 
                    ex.Message, 
                    "Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                ControlsEnabled = true;
                GetCIDLabelStatus.Text = "Ready";
            }
        }

        private void Button_GetCID_Clicked(object sender, RoutedEventArgs e)
        {
            string installationId = string.Concat(PhoneInstallationIdBox.ToString().Replace("HGM.Hotbird64.LicenseManager.Controls.WmiPropertyBox: ", "").Replace("-", "").Trim());
            string epid = string.Concat(EpidBox.ToString().Replace("HGM.Hotbird64.LicenseManager.Controls.WmiPropertyBox: ", "").Where(c => char.IsDigit(c) || c == '.' || c == '-')).Trim();
            IsProgressBarRunning = true;
            GetCIDLabelStatus.Text = "Processing...";

            try
            {
                CIDCode.Text = WebService_Handler.CallWebService(installationId, epid);
            }
            catch (WebException webEx)
            {
                MessageBox.Show(this, "Error connecting to the web service: " + webEx.Message, "Web Service Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException ioEx)
            {
                MessageBox.Show(this, "I/O error: " + ioEx.Message, "I/O Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (NullReferenceException nullPtr)
            {
                MessageBox.Show(this, "Null reference error: " + nullPtr.Message, "Null Reference Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (InvalidOperationException ioExp)
            {
                MessageBox.Show(this, "Invalid operation error: " +ioExp.Message, "Invalid Operation", MessageBoxButton.OK, MessageBoxImage.Error);
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
                if ((string)ComboBoxProductId.Items.Cast<object>().Last() != "USER_CONTROL: Enter manually")
                {
                    ComboBoxProductId.Items.Add("USER_CONTROL: Enter manually");
                }
                InstallCID.IsEnabled = false;


                ControlsEnabled = true;

                if (ComboBoxProductId.SelectedIndex == ComboBoxProductId.Items.Count - 1)
                {
                    ControlsEnabled = false;
                }

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
            if (ComboBoxProductId.SelectedIndex == -1) return; //-1 for unselected

            if (ComboBoxProductId.SelectedIndex == ComboBoxProductId.Items.Count - 1)
            {
                PhoneInstallationIdBox.IsReadOnly = EpidBox.IsReadOnly = false;
                TextBoxLicenseStatusReason.Text = "Not available in manual mode.";
            }
            else
            {
                PhoneInstallationIdBox.IsReadOnly = EpidBox.IsReadOnly = true;
                LicenseMachine.ProductLicense l = Machine.ProductLicenseList[ComboBoxProductId.SelectedIndex];
                WmiProperty w = new("Version " + Machine.LicenseProvidersList[l.ServiceIndex].Version, l.License, false);
                w.DisplayPropertyAsLicenseStatus([TextBoxLicenseStatusReason], TextBoxLicenseStatusReason);
            }

            CIDCode.Clear();
            CIDCode.Background = new SolidColorBrush(Colors.Transparent);
            InstallCID.IsEnabled = false;
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