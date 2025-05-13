using HGM.Hotbird64.LicenseManager.Contracts;
using HGM.Hotbird64.LicenseManager.Controls;
using HGM.Hotbird64.LicenseManager.Extensions;
using HGM.Hotbird64.LicenseManager.Model;
using HGM.Hotbird64.Vlmcs;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Schema;

namespace HGM.Hotbird64.LicenseManager
{
    public delegate void BusyHandler(object sender, BusyEventArgs e);

    public class BusyEventArgs : EventArgs
    {
        public bool Busy;
    }

    public partial class MainWindow : IHaveNotifyOfPropertyChange
    {
        internal LicenseMachine Machine;
        private static readonly KmsGuid zeroGuid = new KmsGuid(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        private KmsGuid lastSkuId = zeroGuid;
        //internal bool ShowAllFields;
        private bool kmsHostDirtyField;
        private KmsServer kmsServer;
        public bool IsClosed { get; private set; }
        public event BusyHandler BusyStatusChanged;
        private OwnKeyWindow ownKeyWindow;
        private ProductKeys gvlkDialog;
        private ExportIds exportIds;
        private readonly bool isInitiallySized = false;
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


        static MainWindow()
        {
            CsvlkConfigs = KeyConfigs.Where(c => c.ProductKeyType == "Volume:CSVLK").ToList();
            IEnumerable<KmsGuid> csvlkConfigIds = CsvlkConfigs.Select(c => c.ActConfigGuid);
            CsvlkRanges = KeyRanges.Where(r => csvlkConfigIds.Contains(r.RefActConfigGuid)).ToList();
            CtrlE.Add(new KeyGesture(Key.E, ModifierKeys.Control));
            CtrlW.Add(new KeyGesture(Key.W, ModifierKeys.Control));
            CheckEpid = new RoutedUICommand("Get Info", nameof(CheckEpid), typeof(ScalableWindow), CtrlE);
            AutoSizeWindow = new RoutedUICommand("Auto Size Window", nameof(AutoSizeWindow), typeof(ScalableWindow), CtrlW);
        }

        public MainWindow()
        {
            InitializeComponent();

            Closing += MainWindow_Closing;

            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess && Environment.OSVersion.Version.Build < 9600)
            {
                MessageBox.Show
                (
                  "You are using a 32-bit version of License Manager on 64-bit system. \n" +
                  "Please download the 64-bit version due the lack of stabillity and security.",
                  $"Bug in Windows {Environment.OSVersion}",
                  MessageBoxButton.OK,
                  MessageBoxImage.Exclamation
                ); ;
            }

            TopElement.LayoutTransform = Scaler;

            Loaded += (s, e) =>
            {
                License.PropertyChanged += License_PropertyChanged;
            };

            Unloaded += (s, e) =>
            {
                License.PropertyChanged -= License_PropertyChanged;
            };
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

        private LicenseModel license = new LicenseModel();
        public LicenseModel License
        {
            get => license;
            set => this.SetProperty(ref license, value);
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
#if DEBUG
            if (Application.Current.Windows.Count < 3)
            {
                return;
            }
#else
            if (Application.Current.Windows.Count < 2) return;
#endif

            if (MessageBox.Show(
                  "There are other License Manager Windows open. Are you sure, you want to exit them all?",
                  "Exiting License Manager", MessageBoxButton.YesNo, MessageBoxImage.Question
                ) == MessageBoxResult.Yes)
            {
                Closed += (s, f) => Application.Current.Shutdown(0);
            }
            else
            {
                e.Cancel = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            IsClosed = true;
            ControlsEnabled = false;
            base.OnClosed(e);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ControlsEnabled = false;
            Icon = this.GenerateImage(new Icons.KeyIcon { Angle = 90d }, 16, 16);

            if (!App.HaveLibKms)
            {
                KmsClientMenuItem.Visibility = Visibility.Collapsed;
                KmsServerMenuItem.Visibility = Visibility.Collapsed;
                LoadExtensionMenuItem.Visibility = Visibility.Visible;
            }

            try
            {
                await Task.Run(() => Machine = new LicenseMachine());
                OsSystemLocale = (Machine?.SysInfo?.OsInfo.Locale != null) ? Machine.SysInfo.OsInfo.Locale : OsSystemLocale;
                Button_Refresh_Clicked(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Unable to get data from license provider", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool ControlsEnabled
        {
            get => ButtonRefresh.IsEnabled;
            set
            {
                ComboBoxProductId.IsEnabled =
                  ButtonActivate.IsEnabled =
                    ButtonSave.IsEnabled =
                      ButtonRefresh.IsEnabled =
                        TextBoxKeyManagementServiceLookupDomain.IsEnabled =
                          TextBoxKeyManagementServiceMachine.IsEnabled =
                            TextBoxKeyManagementServicePort.IsEnabled =
                              MenuItemFile.IsEnabled =
                                MenuItemUninstallKey.IsEnabled =
                                  MenuItemActivate.IsEnabled =
                                    MenuItemService.IsEnabled =
                                      MenuItemShowAllLicenses.IsEnabled =
                                        MenuItemShowAllFields.IsEnabled =
                                          MenuItemInstallKmsKeys.IsEnabled =
                                            MenuItemDeveloperMode.IsEnabled = value;

                BusyStatusChanged?.Invoke(this, new BusyEventArgs { Busy = !value });
            }
        }

        private bool KmsHostDirtyField
        {
            get => kmsHostDirtyField;
            set
            {
                kmsHostDirtyField = value;
                WmiProperty.Show(ButtonSave, kmsHostDirtyField, License.ShowAllFields);
            }
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

                try
                {
                    if (new KmsGuid((string)l.License["ID"]) == lastSkuId)
                    {
                        ComboBoxProductId.SelectedIndex = index;
                        foundItem = true;
                    }
                }
                catch
                {
                }

                index++;
            }

            if (!foundItem)
            {
                ComboBoxProductId.SelectedIndex = 0;
            }
        }

        internal async void Button_Refresh_Clicked(object sender, RoutedEventArgs e)
        {
            LabelStatus.Text = "Gathering Data...";
            await Refresh();
        }

        internal bool IsProgressBarRunning
        {
            get => ProgressBar.IsIndeterminate;
            set
            {
                ProgressBar.IsIndeterminate = value;
                ProgressBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ServiceConfig_Click(object sender, RoutedEventArgs e)
        {
            LicenseMachine.LicenseProvider provider = (LicenseMachine.LicenseProvider)((FrameworkElement)sender).Tag;

            FrameworkElement icon;

            switch (provider.ServiceName)
            {
                case "sppsvc":
                    icon = new Icons.Windows();
                    break;

                case "osppsvc":
                    icon = new Icons.Office2013();
                    break;

                default:
                    icon = new Icons.KeyIcon { Angle = 90 };
                    break;
            }

            ServiceConfiguration serviceConfig = new ServiceConfiguration(Machine, provider, icon)
            {
                Owner = this,
                Icon = Icon
            };

            serviceConfig.ShowDialog();

            if (serviceConfig.MainDialogRefreshRequired)
            {
                Button_Refresh_Clicked(sender, e);
            }
            else
            {
                LabelStatus.Text = "Ready";
            }
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
                        Machine.GetSystemInfo();
                    });

                    UpdateOsInfo();
                    UpdateNicInfo();
                    UpdateBiosInfo();
                    FillLicenseComboBox();
                    IsProgressBarRunning = false;
                }
                catch (Exception ex)
                {
                    LabelStatus.Text = "Error";
                    IsProgressBarRunning = false;
                    MessageBox.Show(this, ex.Message, "Error getting License information", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                LabelStatus.Text = "Ready";
                MenuItemService.Items.Clear();

                foreach (LicenseMachine.LicenseProvider provider in Machine.LicenseProvidersList)
                {
                    if (provider.Version != null)
                    {
                        string description = $"Configure {provider.FriendlyName} {provider.Version}";

                        MenuItem m = new MenuItem
                        {
                            Header = description,
                            Tag = provider
                        };

                        m.Click += ServiceConfig_Click;

                        void SetButton(FrameworkElement button)
                        {
                            button.Visibility = Visibility.Visible;
                            button.Tag = provider;
                            button.ToolTip = description;
                        }

                        switch (provider.ServiceName)
                        {
                            case "sppsvc":
                                m.Icon = new Icons.Windows() { Width = 16 };
                                SetButton(WindowsServiceButton);
                                break;

                            case "osppsvc":
                                m.Icon = new Icons.Office2013() { Width = 16 };
                                SetButton(OfficeServiceButton);
                                break;
                        }

                        MenuItemService.Items.Add(m);
                    }
                }

                if (Machine.ComputerName == ".")
                {
                    TextBoxComputerName.Background = App.DefaultTextBoxBackground;
                    TextBoxComputerName.Text = "";
                    ConnectToLocalComputerToolStripMenuItem.IsEnabled = false;
                }
                else
                {
                    TextBoxComputerName.Background = App.DefaultTextBoxBackground;
                    TextBoxComputerName.Text = Machine.ComputerName + " ";
                    ConnectToLocalComputerToolStripMenuItem.IsEnabled = true;
                }

                TextBoxComputerName.Text += "(" + Machine.SysInfo.OsInfo.CsName + ")";

                if (Machine.UserName == null)
                {
                    TextBoxUserName.Background = App.DefaultTextBoxBackground;
                    TextBoxUserName.Text = "(" + WindowsIdentity.GetCurrent().Name + ")";
                }
                else
                {
                    TextBoxUserName.Background = App.DefaultTextBoxBackground;
                    TextBoxUserName.Text = Machine.UserName;
                }
            }
            finally
            {
                ControlsEnabled = true;

                if (!isInitiallySized)
                {
                    LeftColumn.Width = RightColumn.Width = new GridLength(0, GridUnitType.Auto);
                }
            }
        }

        public static CultureInfo OsSystemLocale;

        private void UpdateOsInfo()
        {
            LicenseMachine.OsInfo osInfo = Machine.SysInfo.OsInfo;

            TextBoxOsCaption.Text = (osInfo.Caption != null ? osInfo.Caption + " " : "") +
                                    (osInfo.Version != null ? osInfo.Version : "");

            WmiProperty.Show(LabelOsInstallDate, TextBoxOsInstallDate, osInfo.InstallDate != null, License.ShowAllFields);

            TextBoxOsInstallDate.Text = osInfo.InstallDate != null
              ? osInfo.InstallDate.Value.ToLongDateString() + " " +
                osInfo.InstallDate.Value.ToLongTimeString()
              : "N/A";

            WmiProperty.Show(LabelOsLocalDateTime, TextBoxOsLocalDateTime, osInfo.LocalDateTime != null, License.ShowAllFields);

            TextBoxOsLocalDateTime.Text = osInfo.LocalDateTime != null
              ? osInfo.LocalDateTime.Value.ToLongDateString() + " " +
                osInfo.LocalDateTime.Value.ToLongTimeString()
              : "N/A";

            WmiProperty.Show(LabelOsLanguage, TextBoxOsLanguage, osInfo.OsLanguage != null, License.ShowAllFields);

            TextBoxOsLanguage.Text = osInfo.OsLanguage != null
              ? osInfo.OsLanguage.DisplayName + " [" +
                osInfo.OsLanguage + "]"
              : "N/A";

            WmiProperty.Show(LabelOsLocale, TextBoxOsLocale, osInfo.Locale != null, License.ShowAllFields);

            TextBoxOsLocale.Text = osInfo.Locale != null
              ? osInfo.Locale.DisplayName + " [" +
                osInfo.Locale + "]"
              : "N/A";
        }

        private void UpdateNicInfo()
        {
            ComboBoxNic.Items.Clear();

            if (Machine.SysInfo.NicInfos == null || Machine.SysInfo.NicInfos.Count == 0)
            {
                WmiProperty.Hide(ComboBoxNic, License.ShowAllFields);
                WmiProperty.Hide(LabelNic, License.ShowAllFields);
                ComboBoxNic.Items.Add("N/A");
            }
            else
            {
                foreach (LicenseMachine.NicInfo ni in Machine.SysInfo.NicInfos)
                {
                    ComboBoxNic.Items.Add(ni.MacAddress +
                                          " " +
                                          ni.NetConnectionId);
                }
            }

            try
            {
                ComboBoxNic.SelectedIndex = 0;
            }
            catch
            {
            }
        }

        private void UpdateBiosInfo()
        {
            LicenseMachine.CsProductInfo systemInfo = Machine.SysInfo.CsProductInfo;

            WmiProperty.Show(LabelHardwareVendor, TextBoxHardwareVendor, systemInfo.Vendor != null, License.ShowAllFields);
            TextBoxHardwareVendor.Text = systemInfo.Vendor ?? "N/A";

            WmiProperty.Show(LabelHardwareName, TextBoxHardwareName, systemInfo.Name != null, License.ShowAllFields);
            TextBoxHardwareName.Text = systemInfo.Name ?? "N/A";

            WmiProperty.Show(LabelCsIdentifyingNumber, TextBoxCsIdentifyingNumber, systemInfo.IdentifyingNumber != null, License.ShowAllFields);
            TextBoxCsIdentifyingNumber.Text = systemInfo.IdentifyingNumber ?? "N/A";

            WmiProperty.Show(LabelBiosSerialNumber, TextBoxBiosSerialNumber, systemInfo.IdentifyingNumber != null, License.ShowAllFields);
            TextBoxBiosSerialNumber.Text = Machine.SysInfo.BiosSerialNumber ?? "N/A";

            WmiProperty.Show(LabelChassisManufacturer, TextBoxChassisManufacturer, Machine.SysInfo.ChassisInfo.Manufacturer != null, License.ShowAllFields);
            TextBoxChassisManufacturer.Text = Machine.SysInfo.ChassisInfo.Manufacturer ?? "N/A";

            WmiProperty.Show(LabelChassisSerialNumber, TextBoxChassisSerialNumber, Machine.SysInfo.ChassisInfo.SerialNumber != null, License.ShowAllFields);
            TextBoxChassisSerialNumber.Text = Machine.SysInfo.ChassisInfo.SerialNumber ?? "N/A";

            WmiProperty.Show(LabelChassisSmbiosAssetTag, TextBoxChassisSmbiosAssetTag, Machine.SysInfo.ChassisInfo.SmbiosAssetTag != null, License.ShowAllFields);
            TextBoxChassisSmbiosAssetTag.Text = Machine.SysInfo.ChassisInfo.SmbiosAssetTag ?? "N/A";

            WmiProperty.Show(LabelMbManufacturer, TextBoxMbManufacturer, Machine.SysInfo.MotherboardInfo.Manufacturer != null, License.ShowAllFields);
            TextBoxMbManufacturer.Text = Machine.SysInfo.MotherboardInfo.Manufacturer ?? "N/A";

            WmiProperty.Show(LabelMbSerialNumber, TextBoxMbSerialNumber, Machine.SysInfo.MotherboardInfo.SerialNumber != null, License.ShowAllFields);
            TextBoxMbSerialNumber.Text = Machine.SysInfo.MotherboardInfo.SerialNumber ?? "N/A";

            WmiProperty.Show(LabelMbProduct, TextBoxMbProduct, Machine.SysInfo.MotherboardInfo.Product != null, License.ShowAllFields);
            TextBoxMbProduct.Text = Machine.SysInfo.MotherboardInfo.Product ?? "N/A";

            WmiProperty.Show(LabelMbVersion, TextBoxMbVersion, Machine.SysInfo.MotherboardInfo.Version != null, License.ShowAllFields);
            TextBoxMbVersion.Text = Machine.SysInfo.MotherboardInfo.Version ?? "N/A";

            string vmName = GetHypervisor(systemInfo);
            UpdateMachineUuid(systemInfo, vmName);
            UpdateDiskUuid(vmName);

        }

        private void UpdateDiskUuid(string vmName)
        {
            WmiProperty.Show(LabelDiskSerialNumberVConfig, TextBoxDiskSerialNumberVConfig, true, License.ShowAllFields);
            {
                string diskId = Machine.SysInfo.DiskSerialNumber;

                switch (vmName)
                {
                    case "Vbox":
                        try
                        {
                            string part1String = diskId.Substring(2, 8);
                            uint part1 = uint.Parse(part1String, NumberStyles.AllowHexSpecifier);
                            ByteSwap part2String = new ByteSwap(uint.Parse(diskId.Substring(11, 8), NumberStyles.AllowHexSpecifier), 32);

                            TextBoxDiskSerialNumberVConfig.Text = part1String +
                                                                  "-XXXX-XXXX-XXXX-XXXX" +
                                                                  part2String;
                        }
                        catch
                        {
                            WmiProperty.Hide(LabelDiskSerialNumberVConfig, TextBoxDiskSerialNumberVConfig, License.ShowAllFields);
                            TextBoxDiskSerialNumberVConfig.Text = "(unrecognized Virtual Box disk)";
                        }
                        break;

                    case "QEMU":
                    case "VMware":
                    case "Parallels":
                    case "Hyper-V":
                        WmiProperty.Hide(LabelDiskSerialNumberVConfig, TextBoxDiskSerialNumberVConfig, License.ShowAllFields);
                        TextBoxDiskSerialNumberVConfig.Text = "(not supported by " + vmName + ")";
                        break;

                    default:
                        WmiProperty.Hide(LabelDiskSerialNumberVConfig, TextBoxDiskSerialNumberVConfig, License.ShowAllFields);
                        TextBoxDiskSerialNumberVConfig.Text = "(unknown virtual machine or real disk)";
                        break;
                }
                if (diskId == null)
                {
                    WmiProperty.Hide(LabelDiskSerialNumber, TextBoxDiskSerialNumber, License.ShowAllFields);
                    TextBoxDiskSerialNumber.Text = "N/A";
                }
                else
                {
                    WmiProperty.Show(LabelDiskSerialNumber, TextBoxDiskSerialNumber, true, License.ShowAllFields);
                    TextBoxDiskSerialNumber.Text = diskId;
                }
            }
        }

        private static string GetHypervisor(LicenseMachine.CsProductInfo biosInfo)
        {
            string vendor, name, version, vmName;
            try
            {
                vendor = biosInfo.Vendor.Trim().ToLower();
            }
            catch
            {
                vendor = "";
            }
            try
            {
                name = biosInfo.Name.Trim().ToLower();
            }
            catch
            {
                name = "";
            }
            try
            {
                version = biosInfo.Version.Trim().ToLower();
            }
            catch
            {
                version = "";
            }

            if (vendor == "innotek gmbh")
            {
                vmName = "VirtualBox";
            }
            else if (vendor == "intel" && name == "bochs")
            {
                vmName = "QEMU";
            }
            else if (vendor == "vmware, inc.")
            {
                vmName = "VMware";
            }
            else if (vendor.StartsWith("parallels") || name.StartsWith("parallels") || version.StartsWith("parallels"))
            {
                vmName = "Parallels";
            }

            else if (vendor.StartsWith("microsoft") && name.StartsWith("virtual"))
            {
                vmName = "Micrsoft";
            }

            else
            {
                vmName = "config";
            }

            return vmName;
        }

        private void UpdateMachineUuid(LicenseMachine.CsProductInfo biosInfo, string vmName)
        {
            LabelCsUuidVConfig.Content = "UUID (" + vmName + ")";
            LabelDiskSerialNumberVConfig.Content = "OS Disk (" + vmName + ")";
            WmiProperty.Show(LabelCsUuidVConfig, TextBoxCsUuidVConfig, true, License.ShowAllFields);

            if (biosInfo.Uuid != null)
            {
                WmiProperty.Show(LabelCsUuid, TextBoxCsUuid, true, License.ShowAllFields);
                TextBoxCsUuid.Text = biosInfo.Uuid;
            }
            else
            {
                WmiProperty.Hide(LabelCsUuid, TextBoxCsUuid, License.ShowAllFields);
                TextBoxCsUuid.Text = "N/A";
            }

            try
            {
                uint part1 = uint.Parse(biosInfo.Uuid.Substring(0, 8), NumberStyles.AllowHexSpecifier);
                ushort part2 = ushort.Parse(biosInfo.Uuid.Substring(9, 4), NumberStyles.AllowHexSpecifier);
                ushort part3 = ushort.Parse(biosInfo.Uuid.Substring(14, 4), NumberStyles.AllowHexSpecifier);
                ushort part4 = ushort.Parse(biosInfo.Uuid.Substring(19, 4), NumberStyles.AllowHexSpecifier);
                ulong part5 = ulong.Parse(biosInfo.Uuid.Substring(24, 12), NumberStyles.AllowHexSpecifier);

                if (new[] { "Vbox", "QEMU", "Parallels" }.Contains(vmName))
                {
                    TextBoxCsUuidVConfig.Text = new ByteSwap(part1, 32) + "-" +
                                                new ByteSwap(part2, 16) + "-" +
                                                new ByteSwap(part3, 16) + "-" +
                                                part4.ToString("x4") + "-" +
                                                part5.ToString("x12");
                }
                else
                {
                    switch (vmName)
                    {
                        case "VMware":
                            TextBoxCsUuidVConfig.Text = GetByteSwappedHexStringWithTrailingSpace(part1, 32) +
                                                        GetByteSwappedHexStringWithTrailingSpace(part2, 16) +
                                                        GetByteSwappedHexStringWithTrailingSpace(part3, 16).TrimEnd() + "-" +
                                                        GetHexStringWithTrailingSpace(part4, 16) +
                                                        GetHexStringWithTrailingSpace(part5, 48).TrimEnd();
                            break;
                        case "Hyper-V":
                            TextBoxCsUuidVConfig.Text = biosInfo.Uuid;
                            break;
                        default:
                            throw new Exception();
                    }
                }
            }
            catch
            {
                TextBoxCsUuidVConfig.Text = "unknown virtual machine or bare metal";
                WmiProperty.Hide(LabelCsUuidVConfig, TextBoxCsUuidVConfig, License.ShowAllFields);
            }
        }

        private struct ByteSwap
        {
            private readonly ulong swapped;
            private readonly int size;

            public ByteSwap(ulong number, int size)
            {
#if DEBUG
                if (size > 64)
                {
                    throw new ArgumentException("Size must be between 0 and 64", nameof(number));
                }

                if ((size & 7) != 0)
                {
                    throw new ArgumentException("Size must be a multiple of 8", nameof(number));
                }
#endif
                this.size = size;
                swapped = 0;

                for (int i = 0; i < size; i += 8)
                {
                    swapped += ((number >> i) & 0xff) << (size - i - 8);
                }

            }

            public static implicit operator ulong(ByteSwap bs)
            {
                return bs.swapped;
            }

            public static implicit operator uint(ByteSwap bs)
            {
                return (uint)(bs.swapped & 0xffffffff);
            }

            public static implicit operator ushort(ByteSwap bs)
            {
                return (ushort)(bs.swapped & 0xffff);
            }

            public static implicit operator string(ByteSwap bs)
            {
                return bs.swapped.ToString("x" + (bs.size >> 3).ToString());
            }
        }

        private string GetByteSwappedHexStringWithTrailingSpace(ulong number, int size)
        {
            string result = "";
            for (int i = 0; i < size; i += 8)
            {
                result += (number & 0xff).ToString("x2") + " ";
                number >>= 8;
            }
            return result;
        }

        private string GetHexStringWithTrailingSpace(ulong number, int size)
        {
            return GetByteSwappedHexStringWithTrailingSpace(new ByteSwap(number, size), size);
        }

        private async void SelectedProductChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxProductId.SelectedIndex < 0)
            {
                return;
            }

            ButtonActivate.ToolTip = $"Activate {Machine.ProductLicenseList[ComboBoxProductId.SelectedIndex].License["Name"]}";

            if
            (
              KmsHostDirtyField &&
              selectedProductIndex != ComboBoxProductId.SelectedIndex &&

              MessageBox.Show
              (
                this,
                "You made changes to the KMS host override parameters. Do you want to save them?",
                "Unsaved changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes
              ) == MessageBoxResult.Yes
            )
            {
                await SaveKmsParameters(sender, e);
                await Refresh();
                return;
            }

            LicenseMachine.ProductLicense l = Machine.ProductLicenseList[ComboBoxProductId.SelectedIndex];

            WmiProperty w = new WmiProperty("Version " + Machine.LicenseProvidersList[l.ServiceIndex].Version, l.License, License.ShowAllFields);

            try
            {
                lastSkuId = new KmsGuid((string)w.Value);
            }
            catch
            {
                lastSkuId = zeroGuid;
            }

            w.DisplayPropertyAsPeriodRemaining(new Control[] { LabelGracePeriodRemaining }, TextBoxGracePeriodRemaining, "GracePeriodRemaining");
            w.DisplayPropertyAsLicenseStatus(new Control[] { LabelLicenseStatusReason }, TextBoxLicenseStatusReason);
            w.SetCheckBox(CheckBoxGenuineStatus, "GenuineStatus");
            w.DisplayPropertyAsDate(new Control[] { LabelEvaluationEndDate }, TextBoxEvaluationEndDate, "EvaluationEndDate");
            w.DisplayPropertyAsPort(TextBoxKeyManagementServicePort, "KeyManagementServicePort");
            ComboBoxVlActivationTypeEnabled.Items.Clear();

            if (w.Value != null && (uint)w.Value == 0)
            {
                GroupBox2.Visibility = Visibility.Collapsed;

                SetText("N/A",
                  TextBoxKeyManagementServiceLookupDomain,
                  TextBoxKeyManagementServiceMachine,
                  TextBoxDiscoveredKeyManagementServiceMachineName,
                  TextBoxVlActivationInterval,
                  TextBoxVlRenewalInterval,
                  TextBoxKmsOs,
                  TexBoxKmsActivationDate,
                  TextBoxKeyManagementServiceProductKeyId,
                  TextBoxKeyManagementServicePort,
                  TextBoxDiscoveredKeyManagementServiceMachinePort);

                ComboBoxVlActivationTypeEnabled.Items.Add("N/A");
                ComboBoxVlActivationTypeEnabled.SelectedIndex = 0;
            }
            else
            {
                GroupBox2.Visibility = Visibility.Visible;
                w.DisplayProperty(LabelKeyManagementServiceLookupDomain, TextBoxKeyManagementServiceLookupDomain, "KeyManagementServiceLookupDomain");
                w.DisplayProperty(LabelKeyManagementServiceMachine, TextBoxKeyManagementServiceMachine, "KeyManagementServiceMachine");

                w.DisplayPropertyAsPort(TextBoxDiscoveredKeyManagementServiceMachinePort, "DiscoveredKeyManagementServiceMachinePort");

                w.DisplayProperty
                (
                  new Control[] { LabelDiscoveredKeyManagementServiceMachineName, TextBoxDiscoveredKeyManagementServiceMachinePort, LabelColon1 },
                  TextBoxDiscoveredKeyManagementServiceMachineName,
                  "DiscoveredKeyManagementServiceMachineName"
                );

                w.DisplayProperty(LabelDiscoveredKeyManagementServiceMachineIpAddress, TextBoxDiscoveredKeyManagementServiceMachineIpAddress, "DiscoveredKeyManagementServiceMachineIpAddress");

                w.DisplayPid
                (
                  LabelKeyManagementServiceProductKeyId, TextBoxKeyManagementServiceProductKeyId,
                  LabelKmsOs, TextBoxKmsOs,
                  LabelKmsActivationDate, TexBoxKmsActivationDate,
                  "KeyManagementServiceProductKeyID"
                );

                TextBoxCsvlk.Text = "N/A";
                DataGrid.ItemsSource = null;
                DataGridToolTip.Text = null;
                TextBoxCsvlk.ToolTip = null;
                DataGrid.Visibility = License.ShowAllFields ? Visibility.Visible : Visibility.Collapsed;
                TextBoxCsvlk.Visibility = License.ShowAllFields ? Visibility.Visible : Visibility.Collapsed;
                TextBoxCsvlk.IsEnabled = false;

                try
                {
                    EPid pid = new EPid(w.Value);
                    w.Property = "ID";

                    try
                    {
                        if (pid.KeyId > 999999999 || pid.GroupId > 99999)
                        {
                            throw new FormatException();
                        }
                        else
                        {
                            TextBoxCsvlk.Visibility = Visibility.Visible;
                            TextBoxCsvlk.IsEnabled = true;
                        }
                    }
                    catch
                    {
                        TextBoxCsvlk.Visibility = License.ShowAllFields ? Visibility.Visible : Visibility.Collapsed;
                        TextBoxCsvlk.Text = "N/A";
                        TextBoxCsvlk.IsEnabled = false;
                        TextBoxCsvlk.Background = App.DefaultTextBoxBackground;
                    }

                    if (TextBoxCsvlk.IsEnabled)
                    {
                        IOrderedEnumerable<KmsItem> kmsItems;
                        ProductKeyConfigurationConfigurationsConfiguration pkConfig = pid.TryGetEpidPkConfig(out kmsItems, out CsvlkItem csvlkRule);
                        KmsItem currentKmsItem = ProductList[w.Value.ToString()]?.KmsItem;
                        TextBoxCsvlk.Text = csvlkRule?.DisplayName ?? pkConfig?.ProductDescription ?? "(Unknown CSVLK name)";

                        if (pid.KeyTypeString != "03")
                        {
                            TextBoxCsvlk.ToolTip = "The extended PID is not from a Volume License Key";
                            TextBoxCsvlk.Text = "(KMS host does not have a CSVLK)";
                        }
                        else if (!CsvlkConfigs.Select(g => g.RefGroupId).Contains((int)pid.GroupId))
                        {
                            TextBoxCsvlk.ToolTip = "The extended PID contains a Group ID that never appears in a CSVLK";
                            TextBoxCsvlk.Text = "(KMS has an invalid CSVLK)";
                        }
                        else if (currentKmsItem == null)
                        {
                            TextBoxCsvlk.ToolTip = "This active product is not in the License Manager Database and cannot be checked.";
                        }
                        else if (pkConfig == null | csvlkRule == null)
                        {
                            TextBoxCsvlk.ToolTip = "This CSVLK is not in the License Manager Database and cannot be checked.";
                        }
                        else
                        {
                            DataGrid.ItemsSource = kmsItems;

                            if (csvlkRule.Activates[currentKmsItem.Guid] != null)
                            {
                                TextBoxCsvlk.Background = csvlkRule.IsLab || csvlkRule.IsPreview ? Brushes.Yellow : App.DefaultTextBoxBackground;
                                TextBoxCsvlk.ToolTip = $"KMS ID \"{currentKmsItem}\" is covered by your KMS host's CSVLK";
                                if (csvlkRule.IsLab)
                                {
                                    TextBoxCsvlk.ToolTip += " but the CSVLK is used only in Microsoft Labs";
                                }

                                if (csvlkRule.IsPreview)
                                {
                                    TextBoxCsvlk.ToolTip += " but the CSVLK is a beta/preview CSVLK";
                                }
                            }
                            else
                            {
                                TextBoxCsvlk.ToolTip = $"KMS ID \"{currentKmsItem}\" is not covered by your KMS host's CSVLK. You have been activated by an emulator";
                                if (currentKmsItem.IsRetail || currentKmsItem.IsPreview)
                                {
                                    TextBoxCsvlk.ToolTip += " but you are using a retail-only or preview product. So this is the best you can get.";
                                }
                            }

                            DataGridToolTip.Text =
                                "The ePID reveals the CSVLK of your KMS host. This is a list of KMS IDs a genuine KMS server would activate. If you have been granted activation without your product being in this list, you have been activated by an emulator with an incorrect extended PID.";
                            DataGrid.Visibility = (!Equals(TextBoxCsvlk.Background, Brushes.YellowGreen) && !Equals(TextBoxCsvlk.Background, App.DefaultTextBoxBackground)) || License.ShowAllFields ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is XmlSchemaException)
                {
                    MessageBox.Show
                    (
                        this,
                        $"{ex.GetType().Name}: {ex.Message}",
                        "Error in KmsDataBase.xml",
                        MessageBoxButton.OK, MessageBoxImage.Error
                    );

                    Application.Current.Shutdown(3847);
                }
                catch { }

                w.Property = "VLRenewalInterval";
                WmiProperty.Show(LabelVlRenewalInterval, TextBoxVlRenewalInterval, w.Value != null, License.ShowAllFields);
                TextBoxVlRenewalInterval.Text = w.Value != null ? new VlInterval((uint)w.Value).ToString() : "N/A";

                w.Property = "VLActivationInterval";
                WmiProperty.Show(LabelVlActivationInterval, TextBoxVlActivationInterval, w.Value != null, License.ShowAllFields);
                TextBoxVlActivationInterval.Text = w.Value != null ? new VlInterval((uint)w.Value).ToString() : "N/A";

                w.Property = "VLActivationTypeEnabled";
                if (w.Value != null && (uint)w.Value < LicenseMachine.ActivationTypes.Length)
                {
                    foreach (string activationType in LicenseMachine.ActivationTypes)
                    {
                        ComboBoxVlActivationTypeEnabled.Items.Add(activationType);
                    }
                    WmiProperty.Show(ComboBoxVlActivationTypeEnabled);
                    WmiProperty.Show(LabelVlActivationTypeEnabled);
                    ComboBoxVlActivationTypeEnabled.SelectedIndex = (int)(uint)w.Value;
                }
                else
                {
                    ComboBoxVlActivationTypeEnabled.Items.Add(w.Value != null ? "(unknown Activation Type)" : "(unsupported)");
                    WmiProperty.Hide(ComboBoxVlActivationTypeEnabled, License.ShowAllFields);
                    WmiProperty.Hide(LabelVlActivationTypeEnabled, License.ShowAllFields);
                    ComboBoxVlActivationTypeEnabled.SelectedIndex = 0;
                }
            }
            KmsHostDirtyField = false;

            // KMS server fields
            w.Property = "IsKeyManagementServiceMachine";
            if (w.Value != null && (uint)w.Value == 1)
            {
                GroupBox3.Visibility = Visibility.Visible;

                w.DisplayProperty(LabelRequiredClientCount, TextBoxRequiredClientCount, "RequiredClientCount");
                uint requiredClientCount = (uint)w.Value;

                w.DisplayProperty(LabelKeyManagementServiceCurrentCount, TextBoxKeyManagementServiceCurrentCount, "KeyManagementServiceCurrentCount");
                uint currentCount = (uint)w.Value;

                TextBoxKeyManagementServiceCurrentCount.Background = currentCount < requiredClientCount ? Brushes.OrangeRed : App.DefaultTextBoxBackground;

                w.DisplayProperty(LabelKeyManagementServiceTotalRequests, TextBoxKeyManagementServiceTotalRequests, "KeyManagementServiceTotalRequests");
                w.DisplayProperty(LabelKeyManagementServiceFailedRequests, TextBoxKeyManagementServiceFailedRequests, "KeyManagementServiceFailedRequests");
                w.DisplayProperty(LabelKeyManagementServiceUnlicensedRequests, TextBoxKeyManagementServiceUnlicensedRequests, "KeyManagementServiceUnlicensedRequests");
                w.DisplayProperty(LabelKeyManagementServiceLicensedRequests, TextBoxKeyManagementServiceLicensedRequests, "KeyManagementServiceLicensedRequests");


                w.DisplayProperty(LabelKeyManagementServiceNonGenuineGraceRequests, TextBoxKeyManagementServiceNonGenuineGraceRequests, "KeyManagementServiceNonGenuineGraceRequests");
                w.DisplayProperty(LabelKeyManagementServiceNotificationRequests, TextBoxKeyManagementServiceNotificationRequests, "KeyManagementServiceNotificationRequests");
                w.DisplayProperty(LabelKeyManagementServiceOobGraceRequests, TextBoxKeyManagementServiceOobGraceRequests, "KeyManagementServiceOOBGraceRequests");
                w.DisplayProperty(LabelKeyManagementServiceOotGraceRequests, TextBoxKeyManagementServiceOotGraceRequests, "KeyManagementServiceOOTGraceRequests");
                w.DisplayProperty(LabelExtendedGrace, TextBoxExtendedGrace, "ExtendedGrace");
            }
            else
            {
                GroupBox3.Visibility = Visibility.Collapsed;

                SetText("N/A",
                  TextBoxRequiredClientCount,
                  TextBoxKeyManagementServiceCurrentCount,
                  TextBoxKeyManagementServiceTotalRequests,
                  TextBoxKeyManagementServiceFailedRequests,
                  TextBoxKeyManagementServiceUnlicensedRequests,
                  TextBoxKeyManagementServiceLicensedRequests,
                  TextBoxKeyManagementServiceNonGenuineGraceRequests,
                  TextBoxKeyManagementServiceNotificationRequests,
                  TextBoxKeyManagementServiceOobGraceRequests,
                  TextBoxKeyManagementServiceOotGraceRequests,
                  TextBoxExtendedGrace);

                TextBoxKeyManagementServiceCurrentCount.Background = App.DefaultTextBoxBackground;
            }
        }

        private void SetText(string text, params TextBox[] boxes)
        {
            foreach (TextBox t in boxes)
            {
                t.Text = text;
            }
        }

        private async void Connect()
        {
            ControlsEnabled = false;
            try
            {
                await Task.Run(() => Machine.RefreshLicenses());
                Button_Refresh_Clicked(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Unable to get data from license provider", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowAllLicenses_Clicked(object sender, RoutedEventArgs e)
        {
            IsProgressBarRunning = true;
            LabelStatus.Text = "Gathering Data ...";
            Machine.IncludeInactiveLicenses = ((MenuItem)sender).IsChecked;
            Connect();
        }

        private void ShowAllFields_Clicked(object sender, RoutedEventArgs e)
        {
            License.ShowAllFields = ((MenuItem)sender).IsChecked;
            UpdateOsInfo();
            UpdateNicInfo();
            UpdateBiosInfo();
            SelectedProductChanged(ComboBoxProductId, null);
        }

        private void DeveloperMode_Clicked(object sender, RoutedEventArgs e)
        {
            SelectedProductChanged(ComboBoxProductId, null);
        }

        private class VlInterval
        {
            private readonly uint minutes;

            public VlInterval(uint minutes)
            {
                this.minutes = minutes;
            }

            public override string ToString()
            {
                string result = minutes
                             + " minutes ("
                             + ((float)minutes / 1440).ToString(CultureInfo.CurrentCulture)
                             + " days)";
                return result;
            }
        }

        private void KmsParameters_TextChanged(object sender, object e)
        {
            KmsHostDirtyField = true;
        }

        private void ConnectToAnotherComputerToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ConnectForm connectForm = new ConnectForm(this)
            {
                Icon = Icon,
                Owner = this
            };

            bool? showDialog = connectForm.ShowDialog();
            bool result = showDialog != null && (bool)showDialog;

            if (result)
            {
                Button_Refresh_Clicked(sender, e);
            }
        }

        internal async void ConnectToLocalComputerToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ControlsEnabled = false;
            IsProgressBarRunning = true;
            LabelStatus.Text = "Connecting ...";
            bool showAllLicenses = MenuItemShowAllLicenses.IsChecked;

            await Task.Run(() => Machine.Connect(null, null, (string)null, showAllLicenses));
            Button_Refresh_Clicked(sender, e);
        }


        private void MenuItem_AboutBox_Clicked(object sender, RoutedEventArgs e)
        {
            AboutBox aboutBox = new AboutBox(this)
            {
                Owner = this,
                Icon = Icon
            };
            aboutBox.ShowDialog();
        }

        private async Task SaveKmsParameters(object sender, RoutedEventArgs e)
        {

            ControlsEnabled = false;
            LabelStatus.Text = "Saving KMS settings";
            IsProgressBarRunning = true;

            string domain = TextBoxKeyManagementServiceLookupDomain.Text;
            string host = TextBoxKeyManagementServiceMachine.Text;
            string port = TextBoxKeyManagementServicePort.Text;
            uint activationType = (uint)ComboBoxVlActivationTypeEnabled.SelectedIndex;

            try
            {
                await Task.Run(() =>
                {
                    Machine.SetKeyManagementOverrides_Product(SelectedProductIndex, domain, host, port);
                    Machine.SetVlActivationTypeEnabled(SelectedProductIndex, activationType);
                }
                );
            }
            catch (Exception ex)
            {
                Machine.SetKeyManagementOverrides_Product(SelectedProductIndex, "kms.loli.beer", "kms.loli.beer", "1688");
                Machine.SetVlActivationTypeEnabled(SelectedProductIndex, activationType);
                MessageBox.Show(
                    this,
                    "Not all settings could be saved. Will be saved as the default setting: \n" +
                    "KMS Server: kms.loli.beer\n" +
                    "Port: 1688\n" +
                    "\nFor further information about the bug, see here: \n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            if (sender == ButtonSave)
            {
                Button_Refresh_Clicked(sender, e);
            }
        }

        private async void Button_Save_Clicked(object sender, RoutedEventArgs e)
        {
            await SaveKmsParameters(sender, e);
        }

        private async void Button_Activate_Clicked(object sender, RoutedEventArgs e)
        {
            ControlsEnabled = false;

            if (KmsHostDirtyField)
            {
                await SaveKmsParameters(sender, e);
            }

            try
            {
                LabelStatus.Text = "Activating";
                IsProgressBarRunning = true;
                int index = ComboBoxProductId.SelectedIndex;
                await Task.Run(() => Machine.Activate(index));
            }
            catch (Exception ex)
            {
                IsProgressBarRunning = false;
                LabelStatus.Text = "Activation Error";

                int hResult = 0;
                COMException exception = ex as COMException;
                if (exception != null)
                {
                    hResult = exception.ErrorCode;
                }

                Win32Exception win32Exception = ex as Win32Exception;
                if (win32Exception != null)
                {
                    hResult = win32Exception.NativeErrorCode;
                }

                MessageBox.Show
                (
                  this,
                  ex.Message + (hResult < 0 ? $"\n\nHRESULT = 0x{hResult:X8}" : (hResult > 0 ? $"Error = {hResult}" : "")),
                  "Activation Error",
                  MessageBoxButton.OK,
                  MessageBoxImage.Error
                );

                LabelStatus.Text = "Ready";
                ControlsEnabled = true;
                return;
            }

            new Thread(() => Dispatcher.Invoke(() => MessageBox.Show
            (
              this,
              "The Product " + ComboBoxProductId.Text + " has been activated successfully.",
              "Success",
              MessageBoxButton.OK,
              MessageBoxImage.Information
            ))).Start();

            Button_Refresh_Clicked(sender, e);
        }

        private void LoadExtensionDll(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                CheckFileExists = true,
                CheckPathExists = true,
                ShowReadOnly = false,
                AddExtension = true,
                Filter = $"License Manager Extension|libkms{IntPtr.Size << 3}.dll",
            };

            bool? result = dialog.ShowDialog(this);
            if (result.Value == false)
            {
                return;
            }

            string fileName = Path.GetFileName(dialog.FileName);
            if (fileName != null && fileName.ToUpperInvariant() != $"LIBKMS{IntPtr.Size << 3}.DLL")
            {
                MessageBox.Show($"The extension DLL must be named libkms{IntPtr.Size << 3}.dll.", "Incorrect filename", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            App.LoadLibKms(dialog.FileName);

            if (App.HaveLibKms)
            {
                KmsServerMenuItem.Visibility = Visibility.Visible;
                KmsClientMenuItem.Visibility = Visibility.Visible;
                LoadExtensionMenuItem.Visibility = Visibility.Collapsed;
            }
        }

        private void StartKmsServer_Clicked(object sender, RoutedEventArgs e)
        {
            if (kmsServer != null)
            {
                kmsServer.Close();
                return;
            }

            kmsServer = new KmsServer(this);
            kmsServer.Closed += (s, f) =>
            {
                KmsServerMenuItem.Header = "Start KMS _Server";
                kmsServer = null;
            };

            //kmsServer.Owner = this;
            KmsServerMenuItem.Header = "Close KMS _Server";
            kmsServer.Show();
        }

        internal async void InstallProductKey(string productKey)
        {
            LabelStatus.Text = "Installing Key...";
            ControlsEnabled = false;
            IsProgressBarRunning = true;

            try
            {
                string licenseProvider = null;
                await Task.Run(() => licenseProvider = Machine.InstallProductKey(productKey));

                new Thread(() => Dispatcher.Invoke(() => MessageBox.Show
                (
                  this,
                  "Product Key \"" + productKey + "\" was successfully installed by " + licenseProvider + ".",
                  "Success",
                  MessageBoxButton.OK,
                  MessageBoxImage.Information
                ))).Start();

                Button_Refresh_Clicked(null, null);
            }
            catch (Exception ex)
            {
                IsProgressBarRunning = false;
                LabelStatus.Text = "Key Installation Error";
                MessageBox.Show(this, "Error: " + ex.Message, "Key Installation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LabelStatus.Text = "Ready";
                ControlsEnabled = true;
            }
        }

        private async void MenuItemUninstallKey_Click(object sender, RoutedEventArgs e)
        {
            int index = ComboBoxProductId.SelectedIndex;

            if (MessageBox.Show
                (
                  this,
                  "Are you sure you want to uninstall the Product Key for " + Machine.ProductLicenseList[index].License["Description"] + "?",
                  "Warning",
                  MessageBoxButton.YesNo,
                  MessageBoxImage.Warning
                )
                != MessageBoxResult.Yes)
            {
                e.Handled = false;
                return;
            }

            IsProgressBarRunning = true;
            ControlsEnabled = false;
            LabelStatus.Text = "Uninstalling key";

            try
            {
                await Task.Run(() => Machine.UninstallProductKey(index));

                new Thread(() => Dispatcher.Invoke(() => MessageBox.Show
                (
                  this,
                  "Product key has been uninstalled successfully",
                  "Success",
                  MessageBoxButton.OK,
                  MessageBoxImage.Information
                ))).Start();

                Button_Refresh_Clicked(sender, e);
            }
            catch (Exception ex)
            {
                LabelStatus.Text = "Key uninstall error";
                IsProgressBarRunning = false;
                MessageBox.Show(this, "The Product key could not be uninstalled: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ControlsEnabled = true;
                LabelStatus.Text = "Ready";
            }
        }

        private void AdjustWindowSize(object sender, RoutedEventArgs e)
        {
            if (!AutoSize.IsChecked)
            {
                GridSplitter.IsEnabled = false;
                RightColumn.Width = LeftColumn.Width = new GridLength(0, GridUnitType.Auto);
                SizeToContent = SizeToContent.WidthAndHeight;
                AutoSize.IsChecked = true;
            }
            else
            {
                SizeToContent = SizeToContent.Manual;
                LeftColumn.Width = new GridLength(LeftColumn.ActualWidth, GridUnitType.Star);
                AutoSize.IsChecked = false;
                GridSplitter.IsEnabled = true;
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (SizeToContent != SizeToContent.WidthAndHeight)
            {
                LeftColumn.Width = new GridLength(LeftColumn.ActualWidth, GridUnitType.Star);
                RightColumn.Width = new GridLength(RightColumn.ActualWidth, GridUnitType.Star);
                AutoSize.IsChecked = false;
                GridSplitter.IsEnabled = true;
            }
        }

        private void AutoSize_CtrlW(object sender, ExecutedRoutedEventArgs e)
        {
            if (!AutoSize.IsChecked)
            {
                AdjustWindowSize(sender, e);
            }
        }

        private void Thumb_OnDragStarted(object sender, DragStartedEventArgs e)
        {
            AutoSize.IsChecked = false;
            SizeToContent = SizeToContent.Manual;
            LeftColumn.Width = new GridLength(LeftColumn.ActualWidth, GridUnitType.Star);
            RightColumn.Width = new GridLength(RightColumn.ActualWidth, GridUnitType.Star);
        }

        private void KmsClient_Click(object sender, RoutedEventArgs e)
        {
            KmsClientWindow kmsClient = new KmsClientWindow(this);
            kmsClient.Show();
        }

        private void MenuItem_ExportIds_Click(object sender, RoutedEventArgs e)
        {
            if (exportIds == null)
            {
                exportIds = new ExportIds(this);
                exportIds.Closed += (s, f) => exportIds = null;
                exportIds.Show();
            }
            else
            {
                exportIds.Focus();
            }
        }

        private void MenuItem_Browse_Click(object sender, RoutedEventArgs e)
        {
            ProductBrowser productBrowser = new ProductBrowser(this) { Icon = this.GenerateImage(new Icons.DatabaseBrowse(), 16, 16) };
            productBrowser.Show();
        }

        private void installAGenericVolumeLicenseKeyToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (gvlkDialog == null)
            {
                gvlkDialog = new ProductKeys(this);
                gvlkDialog.Closed += (s, f) => gvlkDialog = null;
                gvlkDialog.Icon = Icon;
                gvlkDialog.Show();
            }
            else
            {
                gvlkDialog.Focus();
            }
        }

        private void MenuItem_CheckEpid_Click(object sender, RoutedEventArgs e)
        {
            ProductBrowser productBrowser = new ProductBrowser(this, null) { Icon = this.GenerateImage(new Icons.QueryKey(), 16, 16) };
            productBrowser.Show();
        }

        private void MenuItem_GetCID_Click(object sender, RoutedEventArgs e)
        {
            GetCID getCIDWindow = new GetCID();
            getCIDWindow.Show();
        }

        private void MenuItemInstallKmsKey_Click(object sender, RoutedEventArgs e)
        {
            LabelStatus.Text = "Selecting GVLKs";

            try
            {
                InstallKmsKeys installKmsKeys = new InstallKmsKeys(this, Machine) { Icon = Icon };
                installKmsKeys.ShowDialog();
            }
            finally
            {
                if (ControlsEnabled)
                {
                    LabelStatus.Text = "Ready";
                }
            }
        }

        private void MenuItem_GetOwnKey_Click(object sender, RoutedEventArgs e)
        {
            if (ownKeyWindow == null)
            {
                ownKeyWindow = new OwnKeyWindow(this);
                ownKeyWindow.Closed += (s, f) => ownKeyWindow = null;
                ownKeyWindow.Icon = Icon;
                ownKeyWindow.Show();
            }
            else
            {
                ownKeyWindow.Focus();
            }
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Background = App.DatagridBackgroundBrushes[e.Row.GetIndex() % App.DatagridBackgroundBrushes.Count];
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

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ProductBrowser productBrowser = new ProductBrowser(this, ((WmiPropertyBox)sender).Box.Text) { Icon = this.GenerateImage(new Icons.QueryKey(), 16, 16) };
            productBrowser.Show();
        }

        private void textBox_Epid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            string text = (sender as TextBox)?.Text;
            if (text == null || (!Regex.IsMatch(text, PidGen.EpidPattern) && !Regex.IsMatch(text, App.GuidPattern)))
            {
                return;
            }

            CommandBinding_Executed(sender, null);
        }

        private void LoadDataBaseMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*",
                AddExtension = false,
                CheckFileExists = true,
                CheckPathExists = true,
                ShowReadOnly = false,
                DereferenceLinks = true,
                Multiselect = false,
                ValidateNames = true,
                Title = "Load a Custom KMS Database"
            };
            if (!dialog.ShowDialog().Value)
            {
                return;
            }

            App.DatabaseFileName = dialog.FileName;
            KmsLists.KmsData = null;
            App.IsDatabaseLoaded = false;
            KmsLists.LoadDatabase();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}