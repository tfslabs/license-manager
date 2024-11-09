using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HGM.Hotbird64.LicenseManager.Contracts;
using HGM.Hotbird64.LicenseManager.Controls;
using HGM.Hotbird64.LicenseManager.Extensions;
using HGM.Hotbird64.LicenseManager.Model;
using HGM.Hotbird64.Vlmcs;

namespace HGM.Hotbird64.LicenseManager
{
    public partial class GetCID : IHaveNotifyOfPropertyChange
    {
        internal LicenseMachine Machine;
        public bool IsClosed { get; private set; }
        public static ProductKeyConfiguration PKeyConfig => ProductBrowser.PKeyConfig;
        public static IList<PKeyConfigFile> PKeyConfigFiles => ProductBrowser.PKeyConfigFiles;
        public static ISet<ProductKeyConfigurationConfigurationsConfiguration> KeyConfigs => ((ProductKeyConfigurationConfigurations)PKeyConfig.Items.Single(i => i is ProductKeyConfigurationConfigurations)).Configuration;
        public static ISet<ProductKeyConfigurationPublicKeysPublicKey> PublicKeys => ((ProductKeyConfigurationPublicKeys)PKeyConfig.Items.Single(i => i is ProductKeyConfigurationPublicKeys)).PublicKey;
        public static ISet<ProductKeyConfigurationKeyRangesKeyRange> KeyRanges => ((ProductKeyConfigurationKeyRanges)PKeyConfig.Items.Single(i => i is ProductKeyConfigurationKeyRanges)).KeyRange;
        public static IList<ProductKeyConfigurationConfigurationsConfiguration> CsvlkConfigs;
        public static IList<ProductKeyConfigurationKeyRangesKeyRange> CsvlkRanges;
        public static IKmsProductCollection<SkuItem> ProductList => KmsLists.SkuItemList;
        public static IKmsProductCollection<AppItem> ApplicationList => KmsLists.AppItemList;
        public static IKmsProductCollection<KmsItem> KmsProductList => KmsLists.KmsItemList;
        public static RoutedUICommand CheckEpid, AutoSizeWindow;
        public static InputGestureCollection CtrlE = new InputGestureCollection();
        public static InputGestureCollection CtrlW = new InputGestureCollection();
        public static CultureInfo OsSystemLocale;

        static GetCID()
        {
            CsvlkConfigs = KeyConfigs.Where(c => c.ProductKeyType == "Volume:CSVLK").ToList();
            IEnumerable<KmsGuid> csvlkConfigIds = CsvlkConfigs.Select(c => c.ActConfigGuid);
            CsvlkRanges = KeyRanges.Where(r => csvlkConfigIds.Contains(r.RefActConfigGuid)).ToList();
            CtrlE.Add(new KeyGesture(Key.E, ModifierKeys.Control));
            CtrlW.Add(new KeyGesture(Key.W, ModifierKeys.Control));
            CheckEpid = new RoutedUICommand("Get Info", nameof(CheckEpid), typeof(ScalableWindow), CtrlE);
            AutoSizeWindow = new RoutedUICommand("Auto Size Window", nameof(AutoSizeWindow), typeof(ScalableWindow), CtrlW);
        }

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

            Loaded += (s, e) =>
            {
                License.PropertyChanged += License_PropertyChanged;
            };

            Unloaded += (s, e) =>
            {
                License.PropertyChanged -= License_PropertyChanged;
            };
        }

        private static bool CheckConnection()
        {
            bool isInternetTrouble = false;
            string[] GetCIDTargets = { "www.microsoft.com", "activation.sls.microsoft.com" };
            foreach (var target in GetCIDTargets)
            {
                try
                {
                    IPHostEntry hostEntry = Dns.GetHostEntry(target);
                    if (hostEntry.AddressList.Length > 0)
                    {
                        using (var ping = new Ping())
                        {
                            var reply = ping.Send(hostEntry.AddressList[0]);
                            if (reply.Status == IPStatus.Success)
                            {
                                isInternetTrouble = true;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    isInternetTrouble = false;
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

        private void Button_GetCID_Clicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string installationId = PhoneInstallationIdBox.ToString().Replace("HGM.Hotbird64.LicenseManager.Controls.WmiPropertyBox: ", "").Replace(" ", "").Trim();
                string epid = EpidBox.ToString().Replace("HGM.Hotbird64.LicenseManager.Controls.WmiPropertyBox: ", "").Trim();
#if DEBUG
                TextBoxInfoText.AppendText("Debugging mode is enabled.\n" +
                                           $"Your selected Installation ID: {installationId}\n" +
                                           $"Your selected Extended Product ID: {epid}\n\n");
#endif
                string confirmationId = ActivationHelper.CallWebService(1, installationId, epid);
                TextBoxInfoText.AppendText($"Your confirmation ID is: {confirmationId}");

            }
            catch (Exception ex)
            {
#if DEBUG
                MessageBox.Show("Error:\n\n" + ex.ToString(), "Get CID Error", MessageBoxButton.OK, MessageBoxImage.Error);
#else
                MessageBox.Show("Error:\n\n" + ex.HResult, "Get CID Error", MessageBoxButton.OK, MessageBoxImage.Error);
#endif
            }
        }

        public bool ControlsEnabled
        {
            set => EpidBox.IsEnabled =
                     TextBoxLicenseStatusReason.IsEnabled =
                        ComboBoxProductId.IsEnabled =
                            PhoneInstallationIdBox.IsEnabled = value;
        }

        private async Task Refresh()
        {
            ControlsEnabled = false;
            try
            {
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
#if DEBUG
            GetCIDLabelStatus.Text = "Fetching...";
#endif
            if (ComboBoxProductId.SelectedIndex < 0)
            {
                return;
            }

            LicenseMachine.ProductLicense l = Machine.ProductLicenseList[ComboBoxProductId.SelectedIndex];

            WmiProperty w = new WmiProperty("Version " + Machine.LicenseProvidersList[l.ServiceIndex].Version, l.License, false);
            w.DisplayPropertyAsLicenseStatus(new Control[] { TextBoxLicenseStatusReason }, TextBoxLicenseStatusReason);

            GetCIDLabelStatus.Text = "Ready";
        }

        private LicenseModel license = new LicenseModel();
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
                ComboBoxProductId.Items.Add
                (
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

        private void License_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyOfPropertyChange(nameof(License));
        }

        private int selectedProductIndex = -1;
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

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
    }
}
