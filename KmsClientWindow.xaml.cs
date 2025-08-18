using HGM.Hotbird64.LicenseManager.Extensions;
using HGM.Hotbird64.Vlmcs;
using LicenseManager.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace HGM.Hotbird64.LicenseManager
{
    public enum WindowStatus
    {
        Ready = 0,
        Error = 1,
        Connecting = 1024,
        Sending = 1025,
        MinBusy = 1024,
    }

    public class LicenseStatusModel
    {
        public LicenseStatus LicenseStatus { get; set; }
        public string FriendlyName { get; set; }
        public override string ToString() => FriendlyName;
    }

    public class AddressFamilyModel
    {
        public AddressFamily AddressFamily { get; set; }
        public string FriendlyName { get; set; }
        public override string ToString() => FriendlyName;
    }

    public class RemainingTimeModel
    {
        public uint MinutesRemaining { get; set; }
        public string FriendlyName { get; set; }
        public override string ToString() => FriendlyName;
    }

    public class CsvlkRule
    {
        public bool IsLab;
        public bool IsPreview;
        public ISet<KmsGuid> KmsGuids;
        public string DisplayName;
        public override string ToString() => DisplayName;
    }

    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    public partial class KmsClientWindow : INotifyPropertyChanged
    {
        public class ServerTestResult
        {
            public string DisplayName { get; set; }
            public string ToolTip { get; set; }
            public string ResultText { get; private set; }
            public SolidColorBrush ResultBrush { get; private set; }
            public int Severity { get; set; }
            private bool hasPassed;

            public ServerTestResult()
            {
                HasPassed = default;
            }

            public bool HasPassed
            {
                get => hasPassed;
                set
                {
                    hasPassed = value;
                    if (value)
                    {
                        ResultText = "True";
                        ResultBrush = Brushes.Green;
                    }
                    else
                    {
                        ResultText = "False";
                        ResultBrush = Brushes.Red;
                    }
                }
            }
        }

        public ObservableCollection<ServerTestResult> ServerTests;

        public static ProductKeyConfiguration PKeyConfig => ProductBrowser.PKeyConfig;
        public static IList<PKeyConfigFile> PKeyConfigFiles => ProductBrowser.PKeyConfigFiles;

        public static ISet<ProductKeyConfigurationConfigurationsConfiguration> KeyConfigs
          => ((ProductKeyConfigurationConfigurations)PKeyConfig.Items.Single(i => i is ProductKeyConfigurationConfigurations)).Configuration;

        public static ISet<ProductKeyConfigurationPublicKeysPublicKey> PublicKeys
          => ((ProductKeyConfigurationPublicKeys)PKeyConfig.Items.Single(i => i is ProductKeyConfigurationPublicKeys)).PublicKey;

        public static ISet<ProductKeyConfigurationKeyRangesKeyRange> KeyRanges
          => ((ProductKeyConfigurationKeyRanges)PKeyConfig.Items.Single(i => i is ProductKeyConfigurationKeyRanges)).KeyRange;

        public static IKmsProductCollection<SkuItem> ProductList => KmsLists.SkuItemList;
        public IKmsProductCollection<AppItem> ApplicationList => KmsLists.AppItemList;
        public IKmsProductCollection<KmsItem> KmsProductList => KmsLists.KmsItemList;
        public IList<LicenseStatusModel> LicenseStatusList => licenseStatusList;
        public IList<AddressFamilyModel> AddressFamilyList => addressFamilyList;
        public IList<RemainingTimeModel> RemainingTimeList => remainingTimeList;
        public AddressFamilyModel SelectedAddressFamily { get; set; }
        public LicenseStatusModel SelectedLicenseStatus { get; set; }
        public KmsItem SelectedKmsProduct { get; set; }
        public SkuItem SelectedProduct { get; set; }
        public AppItem SelectedApplication { get; set; }
        public RemainingTimeModel SelectedRemainingTime { get; set; }
        public ProtocolVersion SelectedVersion { get; set; }
        public ProtocolVersion RequestVersion;
        public IList<ProtocolVersion> VersionList => versionList;
        public KmsGuid SkuGuid { get; private set; }
        public KmsGuid KmsGuid { get; private set; }
        public KmsGuid AppGuid { get; private set; }
        public KmsGuid ClientGuid { get; private set; }
        public KmsGuid PreviousClientGuid { get; private set; }
        public DateTime RequestTime;
        public uint NCountPolicy, MinutesRemaining;

        public static string CurrentTimeZone
          =>
          TimeZone.CurrentTimeZone.IsDaylightSavingTime(DateTime.Now)
            ? TimeZone.CurrentTimeZone.DaylightName
            : TimeZone.CurrentTimeZone.StandardName;

        private static readonly IdnMapping idn = new();
        private bool noTextTrigger;

        private static readonly Random random =
          new(unchecked((int)(DateTime.UtcNow.Ticks & 0xfffffff)) ^ unchecked((int)(DateTime.UtcNow.Ticks >> 32)));

        private ushort kmsPort;

        //private WindowStatus windowStatus;
        private WindowStatus WindowStatus
        {
            //get { return windowStatus; }
            set
            {
                IsProgressBarRunning = value >= WindowStatus.MinBusy;
                StackPanelButtons.IsEnabled = GroupBoxRequest.IsEnabled = !IsProgressBarRunning;
                LabelStatus.Text = Enum.GetName(typeof(WindowStatus), value) + (IsProgressBarRunning ? "..." : string.Empty);
                //windowStatus = value;
            }
        }

        private static readonly RemainingTimeModel[] remainingTimeList =
        {
            new() {MinutesRemaining = 0, FriendlyName = "Forever"},
            new() {MinutesRemaining = 15*60*24, FriendlyName = "15 days"},
            new() {MinutesRemaining = 30*60*24, FriendlyName = "30 days"},
            new() {MinutesRemaining = 45*60*24, FriendlyName = "45 days"},
            new() {MinutesRemaining = 180*60*24, FriendlyName = "180 days"}
        };

        private static readonly ProtocolVersion[] versionList =
        {
            new() {Major = 4},
            new() {Major = 5},
            new() {Major = 6},
        };

        private static readonly AddressFamilyModel[] addressFamilyList =
        {
            new() {AddressFamily = AddressFamily.InterNetwork, FriendlyName = "IPv4"},
            new() {AddressFamily = AddressFamily.InterNetworkV6, FriendlyName = "IPv6"},
            new() {AddressFamily = AddressFamily.Unspecified, FriendlyName = "IPv4 and IPv6"}
        };

        private static readonly LicenseStatusModel[] licenseStatusList =
        {
            new() {
                LicenseStatus = LicenseStatus.Unlicensed,
                FriendlyName = Model.LicenseStatus.GetText(LicenseStatus.Unlicensed),
            },
            new()
            {
                LicenseStatus = LicenseStatus.Licensed,
                FriendlyName = Model.LicenseStatus.GetText(LicenseStatus.Licensed)
            },
            new()
            {
                LicenseStatus = LicenseStatus.GraceOob,
                FriendlyName = Model.LicenseStatus.GetText(LicenseStatus.GraceOob)
            },
            new()
            {
                LicenseStatus = LicenseStatus.GraceOot,
                FriendlyName = Model.LicenseStatus.GetText(LicenseStatus.GraceOot)
            },
            new()
            {
                LicenseStatus = LicenseStatus.GraceNonGenuine,
                FriendlyName = Model.LicenseStatus.GetText(LicenseStatus.GraceNonGenuine)
            },
            new()
            {
                LicenseStatus = LicenseStatus.Notification,
                FriendlyName = Model.LicenseStatus.GetText(LicenseStatus.Notification)
            },
            new()
            {
                LicenseStatus = LicenseStatus.GraceExtended,
                FriendlyName = Model.LicenseStatus.GetText(LicenseStatus.GraceExtended)
            },
        };


        private static readonly string[][] dnsNames =
        {
            [
                "www", "ftp", "kms", "hack-me", "smtp", "ns1", "mx1", "ns1", "pop3", "imap", "mail", "dns", "headquarter",
                "we-love", "_vlmcs._tcp", "ceo-laptop"
            ],
            [
                ".microsoft", ".apple", ".amazon", ".samsung", ".adobe", ".google", ".yahoo", ".facebook", ".ubuntu", ".oracle",
                ".borland", ".htc", ".acer", ".windows", ".linux", ".sony", ".ibm", ".tesla", ".debian", ".bing", ".e-corp",
                ".starfleet",
            ],
            [
                ".com", ".net", ".org", ".cn", ".co.uk", ".de", ".com.tw", ".us", ".fr", ".it", ".me", ".info", ".biz",
                ".co.jp", ".ua", ".at", ".es", ".pro", ".ch", ".by", ".ru", ".pl", ".kr", ".mx", ".nl", ".li", ".dk"
            ],
        };

        private static readonly string[] netBiosNames =
        [
            "MAIL1", "SERVER", "FILESERVER7", "EXCHANGE", "PC0201", "CLIENT17", "ALDEBARAN", "CHARLY", "JOESMITH1", "HGM1",
            "MYWORKSTATION", "WIN-08154711", "PRINTSRV21", "MOBILE5",
            "LAPTOP", "NOTEBOOK", "MYTABLET56", "WINDOWS3", "WINXPSP3", "CUDA12", "MSDOS-PC", "DELL1", "LENOVO17", "MACBOOK2",
            "IMAC19", "VMWARE12", "VBOX18", "CORE_I7", "INTEL1",
            "SURFACEPRO4",
        ];

        private readonly IReadOnlyList<TextBox> responseTextBoxes;
        private readonly IReadOnlyList<CheckBox> responseCheckBoxes;
        private readonly Brush readonlyTextBoxBrush;

        private bool IsValidInput =>
          SkuGuid != KmsGuid.InvalidGuid &&
          AppGuid != KmsGuid.InvalidGuid &&
          KmsGuid != KmsGuid.InvalidGuid &&
          ClientGuid != KmsGuid.InvalidGuid &&
          PreviousClientGuid != KmsGuid.InvalidGuid &&
          RequestTime != DateTime.MinValue &&
          kmsPort != 0 &&
          NCountPolicy != ~0U &&
          MinutesRemaining != ~0U &&
          RequestVersion.Full != 0 &&
          IsValidKmsAddress(TextBoxAddress.Text);

        private bool IsProgressBarRunning
        {
            get => ProgressBar.IsIndeterminate;
            set
            {
                ProgressBar.IsIndeterminate = value;
                ProgressBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }


        static KmsClientWindow()
        {
            idn.AllowUnassigned = true;
            idn.UseStd3AsciiRules = false;
        }

        public KmsClientWindow(MainWindow mainWindow) : base(mainWindow)
        {
            ProtocolVersion dllVersion = Kms.ApiVersion;

            if (dllVersion < Kms.RequiredDllVersion)
            {
                MessageBox.Show
                (
                  this,
                  $"libkms32.dll version {Kms.RequiredDllVersion} or greater required. You have version {dllVersion}.",
                  "Incorrect DLL version",
                  MessageBoxButton.OK, MessageBoxImage.Error
                );

                Loaded += (s, e) => Close();
                return;
            }

            InitializeComponent();
            App.DataBaseLoaded += DataBaseLoaded;
            Closed += (s, e) => App.DataBaseLoaded -= DataBaseLoaded;

            Loaded += (s, e) => Icon = this.GenerateImage(new Icons.ClientComputer(), 16, 16);
            Title = $"KMS Client Powered By vlmcs {Kms.EmulatorVersion}";
            TopElement.LayoutTransform = Scaler;
            DataContext = this;
            SetInitialDefaults();
            Loaded += (s, e) => SetInitialDefaults();

            responseTextBoxes =
            [
                TextBoxCorrectResponseSize, TextBoxActualResponseSize, TextBoxResponseVersion, TextBoxEPid, TextBoxOs,
                TextBoxInstallDate, TextBoxResponseClientId, TextBoxResponseTimeStamp, TextBoxResponseTimeStampUtc,
                TextBoxActiveClients, TextBoxRenewalInterval, TextBoxRetryInterval, TextBoxCsvlk, TextBoxHwId,
            ];

            responseCheckBoxes =
            [
                CheckBoxDecryptSuccess, CheckBoxIsValidHash, CheckBoxIsValidHmac, CheckBoxIsValidInitializationVector,
                CheckBoxIsValidProtocolVersion, CheckBoxIsValidClientMachineId, CheckBoxIsValidTimeStamp,
                CheckBoxIsValidPidLength,
                CheckBoxIsValidResponseSize,
            ];

            readonlyTextBoxBrush = responseTextBoxes[0].Background;
        }

        private void DataBaseLoaded()
        {
            ComboBoxProduct.ItemsSource = ProductList;
            ComboBoxKmsProduct.ItemsSource = KmsProductList;
            ComboBoxApplication.ItemsSource = ApplicationList;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            DataGridProtocolConformance.MaxHeight =
              DataGridEmulatorDetection.MaxHeight =
              SizeToContent == SizeToContent.Manual ? double.MaxValue : 150;

            AutoSize.IsChecked = SizeToContent == SizeToContent.Height;

            base.OnRenderSizeChanged(sizeInfo);
        }

        private void SetInitialDefaults()
        {
            ComboBoxProduct.SelectedIndex = random.Next(ProductList.Count);
            TextBoxClientGuid.Text = Guid.NewGuid().ToString();
            TextBoxPreviousClientGuid.Text = new Guid().ToString();
            RandomDnsButton_Click(null, null);
            ComboBoxLicenseStatus.SelectedIndex = random.Next(LicenseStatusList.Count);
            TextBoxRequestTime.Text = DateTime.Now.ToString(CultureInfo.CurrentCulture);
            TextBoxHost.Text = "127.0.0.1";
            TextBoxPort.Text = "1688";
            SetTextColorFromCheckBox(TextBoxWorktstationName, true);
            SetTextColorFromCheckBox(TextBoxClientGuid, true);
            SetTextColorFromCheckBox(TextBoxRequestTime, true);
            WindowStatus = WindowStatus.Ready;
            ComboBoxRemainingTime.SelectedIndex = 2;
        }

        private void ResetResponseControls()
        {
            TextBoxWarnings.Visibility = Visibility.Collapsed;
            TextBoxWarnings.Text = string.Empty;
            //TextBox_HwId.Visibility = request.Version.Major > 5 ? Visibility.Visible : Visibility.Collapsed;
            DataGridEmulatorDetection.Visibility = DataGridProtocolConformance.Visibility = Visibility.Collapsed;

            foreach (TextBox textBox in responseTextBoxes)
            {
                textBox.Text = null;
                textBox.Background = readonlyTextBoxBrush;
                textBox.Visibility = Visibility.Visible;
            }

            foreach (CheckBox checkBox in responseCheckBoxes)
            {
                checkBox.IsChecked = false;
                checkBox.Foreground = SystemColors.ControlTextBrush;
                checkBox.Visibility = Visibility.Visible;
            }
        }

        private void ValidateGuid(TextBox textBox, out KmsGuid kmsGuid)
        {
            bool isValid = Guid.TryParse(textBox.Text, out Guid guid);
            textBox.Background = isValid && !KmsGuid.InvalidGuid.Equals(guid) ? Brushes.LightGreen : Brushes.OrangeRed;
            kmsGuid = isValid ? new KmsGuid(guid) : KmsGuid.InvalidGuid;
            ButtonSendRequest.IsEnabled = IsValidInput;
        }

        private void ValidateUint(TextBox textBox, out uint uint32)
        {
            bool isValid = uint.TryParse(textBox.Text, out uint32);
            if (!isValid)
            {
                uint32 = ~0U;
            }

            textBox.Background = isValid && uint32 != ~0U ? Brushes.LightGreen : Brushes.OrangeRed;
            ButtonSendRequest.IsEnabled = IsValidInput;
        }

        private void ValidateUShort(TextBox textBox, out ushort ushort16)
        {
            bool isValid = ushort.TryParse(textBox.Text, out ushort16);
            textBox.Background = isValid && ushort16 != 0 ? Brushes.LightGreen : Brushes.OrangeRed;
            ButtonSendRequest.IsEnabled = IsValidInput;
        }

        [SuppressMessage("ReSharper", "ReturnValueOfPureMethodIsNotUsed")]
        private void ValidateTime(TextBox textBox, out DateTime time)
        {
            bool isValid = DateTime.TryParse(textBox.Text, out time);

            try
            {
                time.ToUniversalTime().ToFileTimeUtc();
            }
            catch
            {
                isValid = false;
                time = DateTime.MinValue;
            }

            textBox.Background = isValid ? Brushes.LightGreen : Brushes.OrangeRed;
            ButtonSendRequest.IsEnabled = IsValidInput;
        }

        private void ComboBox_Product_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxProduct.SelectedIndex < 0)
            {
                return;
            }

            try
            {
                noTextTrigger = true;

                if (SelectedProduct == null)
                {
                    SelectedProduct = KmsLists.SkuItemList[ComboBoxProduct.SelectedIndex];
                }

                TextBoxSkuGuid.Text = SelectedProduct.Guid.ToString();
                ComboBoxKmsProduct.SelectedIndex = KmsProductList.IndexOf(SelectedProduct.KmsItem);
            }
            finally
            {
                noTextTrigger = false;
            }
        }

        private void ComboBox_Application_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxApplication.SelectedIndex < 0)
            {
                return;
            }

            try
            {
                noTextTrigger = true;

                if (SelectedApplication == null)
                {
                    SelectedApplication = KmsLists.AppItemList[ComboBoxApplication.SelectedIndex];
                }

                TextBoxAppGuid.Text = SelectedApplication.Guid.ToString();
            }
            finally
            {
                noTextTrigger = false;
            }
        }

        private void ComboBox_KmsProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxKmsProduct.SelectedIndex < 0)
            {
                return;
            }

            try
            {
                noTextTrigger = true;

                if (SelectedKmsProduct == null)
                {
                    SelectedKmsProduct = KmsLists.KmsItemList[ComboBoxKmsProduct.SelectedIndex];
                }

                TextBoxKmsGuid.Text = SelectedKmsProduct.Guid.ToString();
                ComboBoxApplication.SelectedIndex = ApplicationList.IndexOf(SelectedKmsProduct.App);
                ComboBoxVersion.SelectedIndex = VersionList.IndexOf(SelectedKmsProduct.DefaultKmsProtocol);
                TextBoxVersion.Text = SelectedKmsProduct.DefaultKmsProtocol.ToString();
                TextBoxMinimumClients.Text = SelectedKmsProduct.NCountPolicy.ToString();
            }
            finally
            {
                noTextTrigger = false;
            }
        }

        private void ComboBox_RemainingTime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            if (comboBox.SelectedIndex < 0)
            {
                return;
            }

            try
            {
                noTextTrigger = true;
                TextBoxRemainingTime.Text = $"{((RemainingTimeModel)comboBox.SelectedItem).MinutesRemaining}";
            }
            finally
            {
                noTextTrigger = false;
            }
        }

        private void TextBox_RemainingTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            ValidateUint(textBox, out MinutesRemaining);
            if (noTextTrigger)
            {
                return;
            }

            ComboBoxRemainingTime.SelectedIndex = -1;
        }

        private void ValidateVersion(out ProtocolVersion version)
        {
            version.Full = 0;

            try
            {
                string[] split = TextBoxVersion.Text.Split('.');
                if (split.Length != 2)
                {
                    return;
                }

                if (!ushort.TryParse(split[0], out ushort major))
                {
                    return;
                }

                if (!ushort.TryParse(split[1], out ushort minor))
                {
                    return;
                }

                version.Full = ((uint)major) << 16 | minor;
            }
            finally
            {
                TextBoxVersion.Background = version.Full == 0 ? Brushes.OrangeRed : Brushes.LightGreen;
                ButtonSendRequest.IsEnabled = IsValidInput;
            }
        }

        private void TextBox_Version_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateVersion(out RequestVersion);
            if (noTextTrigger)
            {
                return;
            }

            try
            {
                noTextTrigger = true;
                ComboBoxVersion.SelectedIndex = VersionList.IndexOf(RequestVersion);
            }
            finally
            {
                noTextTrigger = false;
            }
        }

        private void ComboBox_Version_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (noTextTrigger)
            {
                return;
            }

            try
            {
                noTextTrigger = true;
                TextBoxVersion.Text = SelectedVersion.ToString();
            }
            finally
            {
                noTextTrigger = false;
            }
        }

        private void TextBox_RequestTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateTime((TextBox)sender, out RequestTime);
        }

        private void SetKmsAddress()
        {
            if (noTextTrigger)
            {
                return;
            }

            try
            {
                noTextTrigger = true;
                string punycode;

                try
                {
                    punycode = idn.GetAscii(TextBoxHost.Text);
                }
                catch (ArgumentException)
                {
                    punycode = TextBoxHost.Text;
                }

                TextBoxAddress.Text = kmsPort == 1688
                  ? punycode
                  : punycode.Contains(':') ? $"[{punycode}]:{kmsPort}" : $"{punycode}:{kmsPort}";
            }
            finally
            {
                noTextTrigger = false;
            }
        }

        private static bool IsValidKmsAddress(string address)
        {
            if (address.Length < 1)
            {
                return false;
            }

            if (address.Length > 1 && address.Substring(1).Contains('['))
            {
                return false;
            }

            if (address[0] != '[')
            {
                return true;
            }

            if (address.Length < 2)
            {
                return false;
            }

            if (address.Substring(1).Count(c => c == ']') != 1)
            {
                return false;
            }

            int closingBracketPosition = address.LastIndexOf(']');
            if (address.Length == closingBracketPosition + 2)
            {
                return false;
            }

            if (address.Length > closingBracketPosition + 2 && address[closingBracketPosition + 1] != ':')
            {
                return false;
            }

            return true;
        }

        private static void SplitKmsAddress(string address, out string host, out string port)
        {
            port = "1688";
            if (address[0] == '[')
            {
                int closingBracketPosition = address.LastIndexOf(']');
                host = address.Substring(1, closingBracketPosition - 1);

                if (address.Length > closingBracketPosition + 2)
                {
                    port = address.Substring(closingBracketPosition + 2);
                }
            }
            else
            {
                if (address.Count(c => c == ':') != 1)
                {
                    host = address;
                }
                else
                {
                    string[] split = address.Split(':');
                    host = split[0];
                    port = split[1];
                }
            }
        }

        private void TextBox_Address_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            bool isValid = IsValidKmsAddress(textBox.Text);
            ButtonSendRequest.IsEnabled = IsValidInput;
            textBox.Background = isValid ? Brushes.LightGreen : Brushes.OrangeRed;
            if (noTextTrigger)
            {
                return;
            }

            try
            {
                noTextTrigger = true;
                if (!isValid)
                {
                    return;
                }

                SplitKmsAddress(textBox.Text, out string host, out string port);

                try
                {
                    host = idn.GetUnicode(host);
                }
                catch
                {
                }

                TextBoxHost.Text = host;
                TextBoxPort.Text = port;
            }
            finally
            {
                noTextTrigger = false;
            }
        }

        private void TextBox_Host_TextChanged(object sender, TextChangedEventArgs e) => SetKmsAddress();

        private void TextBox_Port_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateUShort((TextBox)sender, out kmsPort);
            SetKmsAddress();
        }

        private void TextBox_SkuGuid_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateGuid(sender as TextBox, out KmsGuid guid);
            SkuGuid = guid;
            ButtonSendRequest.IsEnabled = IsValidInput;
            if (noTextTrigger)
            {
                return;
            }

            ComboBoxProduct.SelectedIndex = -1;
        }

        private void TextBox_KmsGuid_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateGuid(sender as TextBox, out KmsGuid guid);
            KmsGuid = guid;
            ButtonSendRequest.IsEnabled = IsValidInput;
            if (noTextTrigger)
            {
                return;
            }

            ComboBoxKmsProduct.SelectedIndex = -1;
        }

        private void TextBox_AppGuid_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateGuid(sender as TextBox, out KmsGuid guid);
            AppGuid = guid;
            ButtonSendRequest.IsEnabled = IsValidInput;
            if (noTextTrigger)
            {
                return;
            }

            ComboBoxApplication.SelectedIndex = -1;
        }

        private void TextBox_MinimumClients_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateUint(sender as TextBox, out NCountPolicy);
        }

        private void TextBox_ClientGuid_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateGuid((TextBox)sender, out KmsGuid guid);
            ClientGuid = guid;
            ButtonSendRequest.IsEnabled = IsValidInput;
        }

        private void TextBox_PreviousClientGuid_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateGuid((TextBox)sender, out KmsGuid guid);
            PreviousClientGuid = guid;
            ButtonSendRequest.IsEnabled = IsValidInput;
        }

        private void TextBox_WorkStationName_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBoxWorktstationName.Background =
              Regex.IsMatch
              (
                TextBoxWorktstationName.Text,
                @"^(([a-zA-Z0-9_]|[a-zA-Z0-9_][a-zA-Z0-9\-_]*[a-zA-Z0-9_])\.)*([A-Za-z0-9_]|[A-Za-z0-9_][A-Za-z0-9\-_]*[A-Za-z0-9_])$"
              )
                ? Brushes.LightGreen
                : Brushes.LightYellow;
        }

        private void RandomGuidButton_Click(object sender, RoutedEventArgs e)
        {
            UIElementCollection children = ((Grid)((Button)sender).Parent).Children;
            TextBox textBox = children.OfType<TextBox>().First();
            textBox.Text = KmsGuid.NewGuid().ToString();
        }

        private void ZeroGuidButton_Click(object sender, RoutedEventArgs e)
          => TextBoxPreviousClientGuid.Text = Guid.Empty.ToString();

        private void RandomNetbiosButton_Click(object sender, RoutedEventArgs e)
          => TextBoxWorktstationName.Text = netBiosNames[random.Next(netBiosNames.Length)];

        private void RandomDnsButton_Click(object sender, RoutedEventArgs e)
        {
            TextBoxWorktstationName.Text = "";

            foreach (string[] names in dnsNames)
            {
                TextBoxWorktstationName.Text += names[random.Next(names.Length)];
            }
        }

        private void SetTextColorFromCheckBox(TextBox textBox, bool isChecked)
        {
            textBox.IsReadOnly = isChecked;
            textBox.Foreground = isChecked ? Brushes.Gray : Brushes.Black;
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private void CheckBoxUseCurrentTime_Click(object sender, RoutedEventArgs e)
        {
            TextBoxRequestTime.Text = DateTime.Now.ToString(CultureInfo.CurrentCulture);
            SetTextColorFromCheckBox(TextBoxRequestTime, ((CheckBox)sender).IsChecked.Value);
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private void CheckBoxAutoRandomClientId_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = ((CheckBox)sender).IsChecked.Value;
            ButtonRandomClientId.Visibility = isChecked ? Visibility.Collapsed : Visibility.Visible;
            SetTextColorFromCheckBox(TextBoxClientGuid, isChecked);
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private void CheckBoxAutoRandomClientName_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = ((CheckBox)sender).IsChecked.Value;
            ButtonRandomDns.Visibility = isChecked ? Visibility.Collapsed : Visibility.Visible;
            SetTextColorFromCheckBox(TextBoxWorktstationName, isChecked);
        }

        private void Button_PickProduct_Click(object sender, RoutedEventArgs e)
        {
            ProductSelector productSelector = new ProductSelector
            {
                Icon = Icon,
                Owner = this
            };

            bool? showDialog = productSelector.ShowDialog();
            if (showDialog != null && showDialog.Value && productSelector.SelectedProduct != null)
            {
                ComboBoxProduct.SelectedIndex = KmsLists.SkuItemList.IndexOf(productSelector.SelectedProduct);
            }
        }

        private static void SetResultCheckBox(CheckBox checkBox, bool? isCorrect)
        {
            if (!isCorrect.HasValue)
            {
                checkBox.Visibility = Visibility.Collapsed;
                return;
            }

            checkBox.IsChecked = isCorrect;
            checkBox.Foreground = isCorrect.Value ? Brushes.Green : Brushes.Red;
            checkBox.Visibility = Visibility.Visible;
        }

        private void ResponseCheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            checkBox.IsChecked = !checkBox.IsChecked;
            e.Handled = true;
        }

        private void ShowActivationError(Exception ex)
        {
            WindowStatus = WindowStatus.Error;
            string errorType = null;
            Win32Exception win32Exception = null;

            if (ex.InnerException is Win32Exception inner)
            {
                win32Exception = inner;
                errorType = unchecked((uint)win32Exception.NativeErrorCode) < 0x10000 ? "RPC STATUS" : "KMS HRESULT";
            }

            MessageBox.Show
            (
              ex.Message +
              (errorType != null
                ? $"\n{errorType} = 0x{win32Exception.NativeErrorCode:X8} ({win32Exception.NativeErrorCode})"
                : ""),
              $"Error In Request To {TextBoxHost.Text} Port {kmsPort}",
              MessageBoxButton.OK, MessageBoxImage.Error
            );
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
        private async void Button_SendRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WindowStatus = WindowStatus.Connecting;

                ServerTests = new ObservableCollection<ServerTestResult>();
                DataGridEmulatorDetection.ItemsSource = ServerTests;
                DataGridEmulatorDetection.Items.SortDescriptions.Clear();
                DataGridEmulatorDetection.Items.SortDescriptions.Add(new SortDescription(DataGridEmulatorDetection.Columns[1].SortMemberPath, ListSortDirection.Ascending));
                DataGridEmulatorDetection.Items.SortDescriptions.Add(new SortDescription(DataGridEmulatorDetection.Columns[2].SortMemberPath, ListSortDirection.Descending));

                if (CheckBoxUseCurrentTime.IsChecked.Value)
                {
                    TextBoxRequestTime.Text = DateTime.Now.ToString(CultureInfo.CurrentCulture);
                }

                if (CheckBoxAutoRandomClientId.IsChecked.Value)
                {
                    TextBoxClientGuid.Text = Guid.NewGuid().ToString();
                }

                if (CheckBoxAutoRandomClientName.IsChecked.Value)
                {
                    RandomDnsButton_Click(null, null);
                }

                long requestTime = RequestTime.ToUniversalTime().ToFileTimeUtc();

                KmsRequest kmsRequest = new KmsRequest
                {
                    ApplicationID = AppGuid,
                    ClientMachineID = ClientGuid,
                    GracePeriodRemaining = MinutesRemaining,
                    ID = SkuGuid,
                    IsClientVM = CheckBoxVirtualMachine.IsChecked.Value ? 1U : 0U,
                    LicenseStatus = SelectedLicenseStatus.LicenseStatus,
                    KmsID = KmsGuid,
                    RequiredClientCount = NCountPolicy,
                    PreviousClientMachineID = PreviousClientGuid,
                    Version = RequestVersion,
                    TimeStamp = requestTime,
                    WorkstationName = { Text = TextBoxWorktstationName.Text },
                };

                ResetResponseControls();

                byte[] hwId = null;
                KmsResponse kmsResponse = default(KmsResponse);
                KmsResult kmsResult = default(KmsResult);
                RpcDiag rpcDiag = default(RpcDiag);

                using (KmsClient kmsClient = new KmsClient(TextBoxHost.Text, kmsPort))
                {
                    string warnings = null;
                    bool useMultiplexedRpc, useNdr64, useBtfn;

                    try
                    {
                        useMultiplexedRpc = CheckBoxUseMultiplexedRpc.IsChecked.Value;
                        useNdr64 = CheckBoxNdr64.IsChecked.Value;
                        useBtfn = CheckBoxBtfn.IsChecked.Value;

                        await Task.Run(() => warnings = kmsClient.Connect(SelectedAddressFamily.AddressFamily, out rpcDiag, useMultiplexedRpc, useNdr64, useBtfn));
                        TextBoxWarnings.Text = warnings;
                    }
                    catch (Exception ex)
                    {
                        WindowStatus = WindowStatus.Error;
                        MessageBox.Show(ex.Message, $"Error Connecting To {TextBoxHost.Text} Port {kmsPort}", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }


                    try
                    {
                        WindowStatus = WindowStatus.Sending;
                        await Task.Run(() => kmsResult = kmsClient.SendRequest(out _, out warnings, out kmsResponse, kmsRequest, out hwId, false, false));
                        if (!string.IsNullOrWhiteSpace(warnings))
                        {
                            TextBoxWarnings.AppendText(warnings);
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowActivationError(ex);
                        return;
                    }

                    TextBoxHwId.Text = $"{hwId[0]:X2} {hwId[1]:X2} {hwId[2]:X2} {hwId[3]:X2} {hwId[4]:X2} {hwId[5]:X2} {hwId[6]:X2} {hwId[7]:X2}";
                    EPid pid = new EPid(kmsResponse.KmsPid);

                    CheckEpidForErrors(pid);
                    AnalyzeRpc(rpcDiag, pid, kmsRequest.KmsID);
                    AnalyzeKeyRanges(pid, kmsRequest);
                    WarnIfIncorrectActivation(kmsRequest);
                    SetTextBoxFromKmsResponse(pid, kmsRequest, kmsResponse, kmsResult);
                    SetCheckBoxesFromResponseResult(kmsResult, rpcDiag);

                    TextBoxWarnings.Visibility = string.IsNullOrWhiteSpace(TextBoxWarnings.Text) ? Visibility.Collapsed : Visibility.Visible;
                    TextBoxHwId.Visibility = kmsResponse.Version.Major > 5 ? Visibility.Visible : Visibility.Collapsed;

                    if (!CheckBoxMultipleTest.IsChecked.Value)
                    {
                        return;
                    }

                    if (!await AnalyzeSecondRequest(kmsClient, kmsRequest, useMultiplexedRpc, useNdr64, useBtfn, kmsResponse))
                    {
                        return;
                    }

                    KmsRequest testRequest = kmsRequest;
                    testRequest.TimeStamp = DateTime.FromFileTime(kmsRequest.TimeStamp).AddHours(5).ToFileTime();

                    if (!await AnalyzeExpectedError(
                      kmsClient, testRequest, useMultiplexedRpc, useNdr64, useBtfn,
                      displayName: "Client and server time is +/- 4 hours",
                      severity: 10,
                      expectedHResult: 0xC004F06C,
                      baseToolTip:
                          "A genuine KMS server checks if the client time does deviate from its own time by more than 4 hours.\n" +
                          "Two requests have been sent with time stamps that differ by 5 hours.\n"
                    ))
                    {
                        return;
                    }

                    testRequest = kmsRequest;
                    testRequest.Version.Major = 4;
                    testRequest.Version.Minor = 1;

                    if (!await AnalyzeExpectedError(
                      kmsClient, testRequest, useMultiplexedRpc, useNdr64, useBtfn,
                      displayName: "Refuse if incorrect protocol version",
                      severity: 20,
                      expectedHResult: 0x8007000D,
                      baseToolTip:
                          "A genuine KMS server returns HRESULT 0x8007000D if it encounters an incorrect protocol version.\n" +
                          "Some emulators return a different HRESULT or don't check the minor protocol version.\n" +
                          "This test tries to activate via the non-existing protocol 4.1.\n"
                    ))
                    {
                        return;
                    }

                    testRequest = kmsRequest;
                    testRequest.RequiredClientCount = 1001;

                    if (!await AnalyzeExpectedError(
                      kmsClient, testRequest, useMultiplexedRpc, useNdr64, useBtfn,
                      displayName: "Refuse requests with clients > 1000",
                      severity: 20,
                      expectedHResult: 0x8007000D,
                      baseToolTip:
                          "A genuine KMS server returns HRESULT 0x8007000D on requests with more than 1000 required clients.\n" +
                          "Many emulators do not check if the number of required clients is 1000 or less\n"
                    ))
                    {
                        return;
                    }

                    testRequest = kmsRequest;
                    testRequest.ApplicationID = KmsGuid.NewGuid();

                    if (!await AnalyzeExpectedError(
                      kmsClient, testRequest, useMultiplexedRpc, useNdr64, useBtfn,
                      displayName: "Refuse if incorrect App ID",
                      severity: 10,
                      expectedHResult: 0xC004F042,
                      baseToolTip:
                          "A genuine KMS server never activates unknown App IDs.\n" +
                          "Many emulators don't check the App ID.\n" +
                          $"This test tries to activate with random App ID {testRequest.ApplicationID}.\n"
                    ))
                    {
                        return;
                    }

                    testRequest = kmsRequest;
                    testRequest.KmsID = KmsGuid.NewGuid();

                    if (!await AnalyzeExpectedError(
                      kmsClient, testRequest, useMultiplexedRpc, useNdr64, useBtfn,
                      displayName: "Refuse if unknown Kms ID",
                      severity: 10,
                      expectedHResult: 0xC004F042,
                      baseToolTip:
                          "A genuine KMS server never activates unknown Kms IDs.\n" +
                          "Many emulators don't check the Kms ID.\n" +
                          $"This test tries to activate with random Kms ID {testRequest.KmsID}.\n"
                    ))
                    {
                        return;
                    }

                    testRequest = kmsRequest;
                    testRequest.KmsID = new KmsGuid("bbb97b3b-8ca4-4a28-9717-89fabd42c4ac");
                    testRequest.Version.Major = 4;
                    testRequest.RequiredClientCount = 25;
                    testRequest.ApplicationID = Kms.WinGuid;
                    testRequest.ID = new KmsGuid("c04ed6bf-55c8-4b47-9f8e-5a1f31ceee60");

                    if (!await AnalyzeExpectedError(
                      kmsClient, testRequest, useMultiplexedRpc, useNdr64, useBtfn,
                      displayName: "Refuse if Non-VL Windows",
                      severity: 30,
                      expectedHResult: 0xC004F042,
                      baseToolTip:
                          "A genuine KMS does not activate retail editions of Windows.\n" +
                          "Most emulators allow this.\n" +
                          "This test tries to activate Windows 8 Core (Home).\n"
                    ))
                    {
                        return;
                    }

                    testRequest = kmsRequest;
                    testRequest.KmsID = new KmsGuid("5f94a0bb-d5a0-4081-a685-5819418b2fe0");
                    testRequest.Version.Major = 4;
                    testRequest.RequiredClientCount = 25;
                    testRequest.ApplicationID = Kms.WinGuid;
                    testRequest.ID = new KmsGuid("2b9c337f-7a1d-4271-90a3-c6855a2b8a1c");

                    if (!await AnalyzeExpectedError(
                      kmsClient, testRequest, useMultiplexedRpc, useNdr64, useBtfn,
                      displayName: "Refuse if Windows beta",
                      severity: 30,
                      expectedHResult: 0xC004F042,
                      baseToolTip:
                          "A genuine KMS does not activate beta or preview editions of Windows.\n" +
                          "Most emulators allow this.\n" +
                          "This test tries to activate Windows 8.x Preview.\n"
                    ))
                    {
                        return;
                    }

                    await AnalyzeUnknownSkuId(kmsClient, kmsRequest, useMultiplexedRpc, useNdr64, useBtfn);
                }
            }
            finally
            {
                ShowEmulatorDetectionTests();
                WindowStatus = WindowStatus.Ready;
            }
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private async Task<bool> AnalyzeSecondRequest(KmsClient kmsClient, KmsRequest kmsRequest, bool useMultiplexedRpc, bool useNdr64, bool useBtfn, KmsResponse kmsResponse)
        {
            string warnings, errors;
            RpcDiag rpcDiag;
            KmsResponse testResponse = default(KmsResponse);
            byte[] testHwId;

            ServerTestResult serverTestResult = new ServerTestResult
            {
                DisplayName = "Multiple requests over a single connection",
                HasPassed = true,
                Severity = 10,

                ToolTip =
                "All genuine KMS servers and emulators that use MS RPC accept\nmultiple KMS requests over a single TCP connection.\n" +
                "Some emulators with their own RPC implementation do not."
            };

            try
            {
                ServerTestResult result = serverTestResult;

                await Task.Run(() =>
                {
                    kmsRequest.ClientMachineID = KmsGuid.NewGuid();

                    if (!kmsClient.Connected)
                    {
                        warnings = kmsClient.Connect(SelectedAddressFamily.AddressFamily, out rpcDiag, useMultiplexedRpc, useNdr64, useBtfn);
                        result.HasPassed = false;
                    }

                    kmsClient.SendRequest(out errors, out warnings, out testResponse, kmsRequest, out testHwId);
                });

                ServerTests.Add(serverTestResult);
            }
            catch (Exception ex)
            {
                ShowActivationError(ex);
                return false;
            }

            ServerTests.Add(new ServerTestResult
            {
                DisplayName = "Paranoid EPID randomization",
                HasPassed = testResponse.KmsPid.Text == kmsResponse.KmsPid.Text,
                Severity = 20,

                ToolTip =
                "This test fails if an emulator randomizes EPIDs between requests.\nGenuine KMS servers always return the same EPID for each CSVLK.\n" +
                $"The first response reported \"{kmsResponse.KmsPid.Text}\".\n A second response reported \"{testResponse.KmsPid.Text}\"."
            });

            uint lastReportedClients = testResponse.KMSCurrentCount;
            uint oldRequiredClientCount = kmsRequest.RequiredClientCount;

            if (CheckBoxDangerousTests.IsChecked.Value)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        serverTestResult = new ServerTestResult
                        {
                            DisplayName = "Constant active clients on same CMID",
                            Severity = 20,

                            ToolTip = "A genuine KMS server never increases the number of active clients\nif it receives requests with identical Client Machine IDs.\n"
                        };

                        kmsRequest.RequiredClientCount = kmsRequest.ApplicationID == Kms.WinGuid ? 52U : 12U;
                        uint minClients = testResponse.KMSCurrentCount + 2;
                        if (minClients > kmsRequest.RequiredClientCount)
                        {
                            kmsRequest.RequiredClientCount = minClients + 1;
                        }

                        if (kmsRequest.RequiredClientCount > 1000)
                        {
                            kmsRequest.RequiredClientCount = 1000;
                        }

                        if (!kmsClient.Connected)
                        {
                            kmsClient.Connect(SelectedAddressFamily.AddressFamily, out rpcDiag, useMultiplexedRpc, useNdr64, useBtfn);
                        }

                        kmsClient.SendRequest(out errors, out warnings, out KmsResponse testResponse2, kmsRequest, out testHwId);
                        serverTestResult.HasPassed = testResponse2.KMSCurrentCount == testResponse.KMSCurrentCount;
                        lastReportedClients = testResponse2.KMSCurrentCount;
                        serverTestResult.ToolTip += $"The first request returned {testResponse.KMSCurrentCount} and the second request returned {testResponse2.KMSCurrentCount}";
                    });
                }
                catch (Exception ex)
                {
                    ShowActivationError(ex);
                    return false;
                }

                ServerTests.Add(serverTestResult);
            }

            serverTestResult = new ServerTestResult
            {
                DisplayName = "Increases active clients if below maximum",
                Severity = 20,

                HasPassed =
                kmsResponse.KMSCurrentCount >= (oldRequiredClientCount << 1) ||
                (kmsResponse.KMSCurrentCount < testResponse.KMSCurrentCount),

                ToolTip =
                $"The request asked for {kmsRequest.RequiredClientCount} active clients.\nThus the server must increase the active clients until it reaches {kmsRequest.RequiredClientCount << 1}.\n" +
                $"The first response reported {kmsResponse.KMSCurrentCount} and a second response reported {lastReportedClients}"
            };

            serverTestResult.ToolTip += serverTestResult.HasPassed ? "." : $" but it should be {kmsResponse.KMSCurrentCount + 1}.";

            if (serverTestResult.HasPassed && CheckBoxDangerousTests.IsChecked.Value)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        kmsRequest.ClientMachineID = KmsGuid.NewGuid();
                        if (!kmsClient.Connected)
                        {
                            kmsClient.Connect(SelectedAddressFamily.AddressFamily, out rpcDiag, useMultiplexedRpc, useNdr64, useBtfn);
                        }

                        kmsClient.SendRequest(out errors, out warnings, out KmsResponse testResponse2, kmsRequest, out testHwId);
                        serverTestResult.HasPassed = (testResponse.KMSCurrentCount + 1 == testResponse2.KMSCurrentCount);

                        serverTestResult.ToolTip += $"\nA third request that asked for {kmsRequest.RequiredClientCount} active clients returned {testResponse2.KMSCurrentCount}";
                        serverTestResult.ToolTip += serverTestResult.HasPassed ? "." : $" but it should be {testResponse.KMSCurrentCount + 1}.";
                    });
                }
                catch (Exception ex)
                {
                    ShowActivationError(ex);
                    return false;
                }
            }

            ServerTests.Add(serverTestResult);
            return true;
        }

        private async Task AnalyzeUnknownSkuId(KmsClient kmsClient, KmsRequest kmsRequest, bool useMultiplexedRpc, bool useNdr64, bool useBtfn)
        {

            kmsRequest.ID = KmsGuid.NewGuid();

            ServerTestResult serverTestResult = new ServerTestResult
            {
                DisplayName = "Success on random SKU ID",
                HasPassed = true,
                Severity = 20,

                ToolTip =
                "All genuine KMS servers do not care about the SKU ID.\n" +
                $"This test tries to activate with random SKU ID {kmsRequest.ID}."
            };

            try
            {
                await Task.Run(() =>
                {
                    kmsRequest.ClientMachineID = KmsGuid.NewGuid();

                    if (!kmsClient.Connected)
                    {
                        kmsClient.Connect(SelectedAddressFamily.AddressFamily, out RpcDiag rpcDiag, useMultiplexedRpc, useNdr64, useBtfn);
                    }

                    kmsClient.SendRequest(out string errors, out string warnings, out KmsResponse testResponse, kmsRequest, out byte[] testHwId);
                });

            }
            catch (Exception ex)
            {
                serverTestResult.ToolTip += $"\nRequest failed: {ex.Message}";
                serverTestResult.HasPassed = false;
            }

            ServerTests.Add(serverTestResult);
        }

        private async Task<bool> AnalyzeExpectedError
        (
          KmsClient kmsClient, KmsRequest kmsRequest, bool useMultiplexedRpc, bool useNdr64, bool useBtfn,
          string displayName, int severity, string baseToolTip, uint expectedHResult
        )
        {
            if (ServerTests.Count(t => !t.HasPassed && t.DisplayName == displayName) > 0)
            {
                return true;
            }

            string warnings;

            ServerTestResult serverTestResult = new ServerTestResult
            {
                DisplayName = displayName,
                Severity = severity,
                ToolTip = baseToolTip,
            };

            try
            {
                await Task.Run(() =>
                {
                    if (!kmsClient.Connected)
                    {
                        warnings = kmsClient.Connect(SelectedAddressFamily.AddressFamily, out RpcDiag rpcDiag, useMultiplexedRpc, useNdr64, useBtfn);
                    }

                    kmsClient.SendRequest(out string errors, out warnings, out KmsResponse testResponse, kmsRequest, out byte[] testHwId);
                    serverTestResult.ToolTip += "The request succeeded which is wrong.";
                });
            }
            catch (Exception ex)
            {
                if (ex.InnerException is Win32Exception win32Exception)
                {
                    if (unchecked((uint)win32Exception.NativeErrorCode) == expectedHResult)
                    {
                        serverTestResult.HasPassed = true;
                        serverTestResult.ToolTip += $"The request correctly returned HRESULT 0x{expectedHResult:X}.";
                    }
                    else
                    {
                        serverTestResult.ToolTip +=
                          $"The request returned unexpected error code 0x{win32Exception.NativeErrorCode:X} instead of 0x{expectedHResult:X}.";
                    }
                }
                else
                {
                    ShowActivationError(ex);
                    return false;
                }
            }

            ServerTests.Add(serverTestResult);
            return true;
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private void AnalyzeRpc(RpcDiag rpcDiag, EPid pid, KmsGuid kmsId)
        {
            uint osBuild;
            ServerTestResult serverTestResult;

            CheckBoxHasBtfn.Foreground = CheckBoxHasNdr64.Foreground = Brushes.Black;
            if (!rpcDiag.HasRpcDiag)
            {
                return;
            }

            try
            {
                osBuild = pid.OsBuild;
            }
            catch
            {
                return;
            }

            if (CheckBoxBtfn.IsChecked.Value)
            {
                serverTestResult = new ServerTestResult
                {
                    DisplayName = "BTFN support in RPC",
                    Severity = 20,

                    ToolTip =
                    "MS RPC supports BTFN since build 3790 SP1.\nEmulators using its own RPC should support it if the build number in\n" +
                    "the EPID is 6000 or greater and not support it if the build number is 2600 or less."
                };

                if (osBuild >= 3790 && !rpcDiag.HasBTFN)
                {
                    serverTestResult.ToolTip += $"\n\nThe KMS server is an emulator because it claims to be Windows build {osBuild}\nbut does not support Bind Time Feature Negotiation.";
                    CheckBoxHasBtfn.Foreground = Brushes.Red;
                }
                else if (osBuild <= 2600 && rpcDiag.HasBTFN)
                {
                    serverTestResult.ToolTip += $"\n\nThe KMS server is an emulator because it claims to be Windows build {osBuild}\nbut supports Bind Time Feature Negotiation.";
                    CheckBoxHasBtfn.Foreground = Brushes.Red;
                }
                else
                {
                    serverTestResult.HasPassed = true;
                    CheckBoxHasBtfn.Foreground = Brushes.Green;
                }

                ServerTests.Add(serverTestResult);
            }

            if (!CheckBoxNdr64.IsChecked.Value)
            {
                return;
            }

            serverTestResult = new ServerTestResult
            {
                DisplayName = "NDR64 support in RPC",
                Severity = 20,

                ToolTip =
                  "MS RPC supports NDR64 since build 3790 but only in 64-bit editions.\nGenuine KMS servers started using NDR64 with build 9200.\nEmulators using its own RPC should not support it if the build number in\n" +
                  "the EPID is 7601 or less and support it if the build number is 9200 or greater\nand they activate using a Windows Server CSVLK.\nIn all other cases it is perfectly ok to support or not support NDR64."
            };

            if (osBuild >= 9200 && App.ServerKmsGuids.Contains(kmsId) && !rpcDiag.HasNDR64)
            {
                serverTestResult.ToolTip += $"\n\nThe KMS server is an emulator because it activated \"{KmsProductList[kmsId]}\"\nand claims to be build {osBuild} but does not support NDR64.";
                CheckBoxHasNdr64.Foreground = Brushes.Red;
                ServerTests.Add(serverTestResult);
            }
            else if
            (
              (osBuild >= 9200 && App.ServerKmsGuids.Contains(kmsId) && rpcDiag.HasNDR64) ||
              (osBuild <= 7699 && !rpcDiag.HasNDR64)
            )
            {
                CheckBoxHasNdr64.Foreground = Brushes.Green;
                serverTestResult.HasPassed = true;
                ServerTests.Add(serverTestResult);
            }
            else if (osBuild <= 7699 && rpcDiag.HasNDR64)
            {
                serverTestResult.ToolTip += $"\n\nThe KMS server is an emulator because it claims to be Windows build {osBuild} but supports NDR64.";
                CheckBoxHasNdr64.Foreground = Brushes.Red;
                ServerTests.Add(serverTestResult);
            }
        }

        private void AnalyzeKeyRanges(EPid pid, KmsRequest request)
        {
            ProductKeyConfigurationConfigurationsConfiguration pkConfig = pid.TryGetEpidPkConfig(out IOrderedEnumerable<KmsItem> kmsItems, out CsvlkItem csvlkItem);
            DataGridProtocolConformance.ItemsSource = kmsItems;

            DataGridProtocolConformance.Visibility = kmsItems == null ? Visibility.Collapsed : Visibility.Visible;

            ServerTestResult serverTestResult = new ServerTestResult
            {
                DisplayName = "KMS ID matches CSVLK",
                Severity = 40,

                ToolTip =
                "The Group ID and the Key ID in the EPID reveal the CSVLK.\n" +
                "Each CSVLK activates specific KMS IDs only."
            };

            if (pkConfig == null)
            {
                serverTestResult.ToolTip += $"\n\nThe KMS host returned an unknown CSVLK.";
                TextBoxCsvlk.Background = Brushes.OrangeRed;
                TextBoxCsvlk.Text = "(Error)";
                ServerTests.Add(serverTestResult);
                return;
            }

            if (kmsItems == null || csvlkItem == null)
            {
                serverTestResult.ToolTip += $"\n\nThe KMS host's CSVLK is not in the License Manager Database.";
                TextBoxCsvlk.Background = Brushes.Yellow;
                TextBoxCsvlk.Text = pkConfig.ProductDescription;
                ServerTests.Add(serverTestResult);
                return;
            }

            TextBoxCsvlk.Text = csvlkItem.DisplayName;

            if (csvlkItem.Activates[request.KmsID] == null)
            {
                TextBoxCsvlk.Background = Brushes.OrangeRed;
                serverTestResult.ToolTip += $"\n\nThe KMS server is an emulator because it activated \"{KmsProductList[request.KmsID]?.DisplayName ?? $"KMS ID {request.KmsID}"}\"\nwhich is not covered by its \"{csvlkItem.DisplayName}\" CSVLK.";
            }
            else
            {
                TextBoxCsvlk.Background = csvlkItem.IsLab | csvlkItem.IsPreview ? Brushes.Yellow : Brushes.LightGreen;
                serverTestResult.HasPassed = Equals(TextBoxCsvlk.Background, Brushes.LightGreen);
            }

            if (csvlkItem.IsLab)
            {
                serverTestResult.ToolTip += $"\n\nThe KMS server has a CSVLK that is used in Microsoft's Labs only.";
            }

            if (csvlkItem.IsPreview)
            {
                serverTestResult.ToolTip += $"\n\nThe KMS server has a CSVLK that is only used in betas and previews.";
            }

            ServerTests.Add(serverTestResult);
        }

        private void WarnIfIncorrectActivation(KmsRequest request)
        {
#if DEBUG
            Debug.Assert(CheckBoxMultipleTest.IsChecked != null, $"{nameof(CheckBoxMultipleTest.IsChecked)} != null");
#endif

            if (CheckBoxMultipleTest.IsChecked.Value)
            {
                return;
            }

            AppItem application = ApplicationList[request.ApplicationID];
            KmsItem product = KmsProductList[request.KmsID];

            ServerTestResult serverTestResult = new ServerTestResult
            {
                DisplayName = "Refuse if unknown Kms ID",
                Severity = 10,
                HasPassed = true,

                ToolTip =
                "A genuine KMS server never activates unknown KMS IDs.\n" +
                "Many emulators don't check the Kms ID."
            };

            if (product == null)
            {
                serverTestResult.HasPassed = false;
                serverTestResult.ToolTip += $"\n\nThe KMS server activated a product with an unknown KMS ID.";
            }

            ServerTests.Add(serverTestResult);

            serverTestResult = new ServerTestResult
            {
                DisplayName = "Refuse if incorrect App ID",
                Severity = 10,
                HasPassed = true,

                ToolTip =
                "A genuine KMS server never activates unknown App IDs.\n" +
                "Many emulators don't check the App ID.",

            };

            if (application == null)
            {
                serverTestResult.HasPassed = false;
                serverTestResult.ToolTip += $"\n\nThe KMS server is an emulator because it activated an application other than ";

                foreach (AppItem app in ApplicationList)
                {
                    serverTestResult.ToolTip += app == ApplicationList.First() ? $"{app}" : app == ApplicationList.Last() ? $" or {app}." : $", {app}";
                }
            }
            else if (product != null && product.App != application)
            {
                serverTestResult.HasPassed = false;
                serverTestResult.ToolTip += $"\n\nThe KMS server is an emulator because it activated\n\"{product}\" with application \"{application}\".";
            }

            ServerTests.Add(serverTestResult);

            serverTestResult = new ServerTestResult
            {
                DisplayName = "Client and server time is +/- 4 hours",
                HasPassed = true,
                Severity = 10,

                ToolTip =
                "A genuine KMS server checks if the client time does deviate from its own time by more than 4 hours."
            };

            if ((DateTime.UtcNow - DateTime.FromFileTimeUtc(request.TimeStamp).ToUniversalTime()).Duration() > new TimeSpan(4, 5, 0)) //4 hours plus additional tolerance of 5 minutes.
            {
                serverTestResult.HasPassed = false;
                serverTestResult.ToolTip += "\n\nThe KMS server activated despite a time difference of more than 4 hours. If the system clocks are set correctly, the server is an emulator.";
            }

            ServerTests.Add(serverTestResult);

            serverTestResult = new ServerTestResult
            {
                DisplayName = "Refuse if incorrect protocol version",
                Severity = 20,
                HasPassed = true,
                ToolTip = "A genuine KMS server returns HRESULT 0x8007000D if it encounters an incorrect protocol version."
            };

            if (!VersionList.Contains(request.Version))
            {
                serverTestResult.HasPassed = false;
                serverTestResult.ToolTip += $"\n\nThe KMS server is an emulator because it activated using unknown KMS protocol {request.Version}.";
            }

            ServerTests.Add(serverTestResult);
        }

        private void ValidateEpidField(Action action, ServerTestResult serverTestResult)
        {
            try
            {
                serverTestResult.Severity = 40;
                serverTestResult.HasPassed = true;
                action();
            }
            catch (Exception ex)
            {
                serverTestResult.ToolTip += ($"\n\n{ex.Message}");
                serverTestResult.HasPassed = false;
            }

            ServerTests.Add(serverTestResult);
        }

        [SuppressMessage("ReSharper", "ReturnValueOfPureMethodIsNotUsed")]
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
        private void CheckEpidForErrors(EPid pid)
        {
            if (!pid.IsValidEpidFormat)
            {
                TextBoxEPid.Background = Brushes.LightYellow;
            }

            ServerTestResult serverTestResult = new ServerTestResult
            {
                DisplayName = "EPID has valid LCID",
                ToolTip = "The LCID in the EPID must be valid."
            };

            ValidateEpidField(() => pid.Culture.ToString(), serverTestResult);

            serverTestResult = new ServerTestResult
            {
                DisplayName = "EPID has valid Key ID",
                ToolTip = "The Key ID of the EPID must be valid."
            };

            ValidateEpidField(() => pid.KeyId.ToString(), serverTestResult);

            serverTestResult = new ServerTestResult
            {
                DisplayName = "EPID has valid OS build",
                ToolTip = "The OS build number must be valid."
            };

            // ReSharper disable once RedundantToStringCall
            ValidateEpidField(() => pid.OsName.ToString(), serverTestResult);

            serverTestResult = new ServerTestResult
            {
                DisplayName = "EPID has valid date",
                ToolTip = "The date in the EPID must be valid."
            };

            ValidateEpidField(() =>
            {
                DateTime thresholdDate = new DateTime(2009, 1, 1);
                if (pid.Date >= thresholdDate)
                {
                    return;
                }

                serverTestResult.ToolTip += $"\n\nThe KMS server is an emulator because the date in the ePID \"{pid.DateString}\" is before {thresholdDate.ToShortDateString()}.";
                serverTestResult.HasPassed = false;
            }, serverTestResult);

            serverTestResult = new ServerTestResult
            {
                DisplayName = "EPID has key type 03",
                ToolTip = "EPIDs from a KMS server must contain a key type of 03 (Volume License)"
            };

            ValidateEpidField(() =>
            {
                if (pid.KeyType == 3)
                {
                    return;
                }

                serverTestResult.ToolTip += $"\n\nThe KMS server is an emulator because the key type in the ePID must be 03 (KMS key) but is {pid.KeyTypeString}.";
                serverTestResult.HasPassed = false;
            }, serverTestResult);

            serverTestResult = new ServerTestResult
            {
                DisplayName = "EPID has valid Platform ID",
                ToolTip = "The Platform ID in the EPID must be \"55041\", \"05426\", \"06401\" or \"03612\" and correspond to the OS build number."
            };

            ValidateEpidField(() =>
            {
                if (KmsLists.GetPlatformId((int)pid.OsBuild) == (int)pid.OsId)
                {
                    return;
                }

                serverTestResult.ToolTip += $"\n\nThe Platform ID of the EPID \"{pid.OsIdString}\" does not correspond to the OS build number \"{pid.OsBuild}\" and should be {KmsLists.GetPlatformId((int)pid.OsBuild):D5}.";
                serverTestResult.HasPassed = false;
            }, serverTestResult);
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private static void SetRpcFeature(ToggleButton requestCheckBox, ToggleButton displayCheckBox, bool hasRpcDiag, bool hasFeature)
        {
            if (!hasRpcDiag || !requestCheckBox.IsChecked.Value)
            {
                displayCheckBox.Visibility = Visibility.Collapsed;
                return;
            }

            displayCheckBox.Visibility = Visibility.Visible;
            displayCheckBox.IsChecked = hasFeature;
        }

        private void ShowEmulatorDetectionTests()
        {
            if (DataGridEmulatorDetection.Items == null || ServerTests == null)
            {
                DataGridEmulatorDetection.Visibility = Visibility.Collapsed;
                return;
            }

            DataGridEmulatorDetection.Visibility = ServerTests.Count(t => ShowAllDetectionTests.IsChecked || !t.HasPassed) > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetCheckBoxesFromResponseResult(KmsResult kmsResult, RpcDiag rpcDiag)
        {
            SetResultCheckBox(CheckBoxDecryptSuccess, kmsResult.IsDecryptSuccess);
            SetResultCheckBox(CheckBoxIsValidClientMachineId, kmsResult.IsValidClientMachineId);
            SetResultCheckBox(CheckBoxIsValidHash, kmsResult.IsValidHash);
            SetResultCheckBox(CheckBoxIsValidHmac, kmsResult.IsValidHmac);
            SetResultCheckBox(CheckBoxIsValidInitializationVector, kmsResult.IsValidInitializationVector);
            SetResultCheckBox(CheckBoxIsValidPidLength, kmsResult.IsValidPidLength);
            SetResultCheckBox(CheckBoxIsValidProtocolVersion, kmsResult.IsValidProtocolVersion);
            SetResultCheckBox(CheckBoxIsValidTimeStamp, kmsResult.IsValidTimeStamp);
            SetResultCheckBox(CheckBoxIsValidResponseSize, kmsResult.IsValidResponseSize);

            SetRpcFeature(CheckBoxBtfn, CheckBoxHasBtfn, rpcDiag.HasRpcDiag, rpcDiag.HasBTFN);
            SetRpcFeature(CheckBoxNdr64, CheckBoxHasNdr64, rpcDiag.HasRpcDiag, rpcDiag.HasNDR64);
        }

        private void SetTextBoxFromPidProperty(TextBox textBox, EPid pid, string propertyName)
        {
            PropertyInfo p = pid.GetType().GetProperty(propertyName);

            try
            {
                textBox.Text = p?.GetValue(pid).ToString();
                textBox.Background = Brushes.LightGreen;
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                {
                    textBox.Text = $"Error: {ex.InnerException.Message} ({ex.InnerException.GetType().Name})";
                }

                textBox.Background = Brushes.LightYellow;
            }
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private void SetTextBoxFromKmsResponse(EPid pid, KmsRequest request, KmsResponse response, KmsResult kmsResult)
        {
            TextBoxCorrectResponseSize.Text = kmsResult.CorrectResponseSize.ToString();
            TextBoxActualResponseSize.Text = kmsResult.EffectiveResponseSize.ToString();
            TextBoxActualResponseSize.Background = kmsResult.IsValidResponseSize.Value ? Brushes.LightGreen : Brushes.LightYellow;

            TextBoxResponseVersion.Text = $"{response.Version}";
            TextBoxResponseVersion.Background = kmsResult.IsValidProtocolVersion.Value ? Brushes.LightGreen : Brushes.OrangeRed;
            TextBoxEPid.Text = $"{pid}";

            SetTextBoxFromPidProperty(TextBoxOs, pid, nameof(pid.LongOsName));
            SetTextBoxFromPidProperty(TextBoxInstallDate, pid, nameof(pid.LongDateString));

            TextBoxResponseClientId.Text = $"{response.ClientMachineID}";
            TextBoxResponseClientId.Background = kmsResult.IsValidClientMachineId.Value ? Brushes.LightGreen : Brushes.OrangeRed;
            DateTime timeStampUtc = DateTime.FromFileTimeUtc(response.TimeStamp).ToUniversalTime();

            DateTime timeStampLocal = timeStampUtc.ToLocalTime();
            TextBoxResponseTimeStamp.Text = $"{timeStampLocal.ToLongDateString()} {timeStampLocal.ToLongTimeString()} {CurrentTimeZone}";
            TextBoxResponseTimeStampUtc.Text = $"{timeStampUtc.ToLongDateString()} {timeStampUtc.ToLongTimeString()} UTC";
            TextBoxResponseTimeStampUtc.Background = kmsResult.IsValidTimeStamp.Value ? Brushes.LightGreen : Brushes.OrangeRed;

            TextBoxActiveClients.Text = $"{response.KMSCurrentCount}";

            if (response.KMSCurrentCount < request.RequiredClientCount)
            {
                TextBoxActiveClients.Background = Brushes.OrangeRed;
            }
            else if (response.KMSCurrentCount > (request.ApplicationID == Kms.WinGuid ? 50 : 10))
            {
                TextBoxActiveClients.Background = Brushes.Yellow;
            }
            else
            {
                TextBoxActiveClients.Background = Brushes.LightGreen;
            }

            if (response.KMSCurrentCount > (request.ApplicationID == Kms.WinGuid ? 50 : 10))
            {
                TextBoxWarnings.AppendText("The KMS server has been \"overcharged\".\n");
            }

            TimeSpan timeSpan = new TimeSpan(0, (int)response.VLRenewalInterval, 0);
            TextBoxRenewalInterval.Text = $"{timeSpan.Days} days, {timeSpan.Hours} hours, {timeSpan.Minutes} minutes";
            timeSpan = new TimeSpan(0, (int)response.VLActivationInterval, 0);
            TextBoxRetryInterval.Text = $"{timeSpan.Days} days, {timeSpan.Hours} hours, {timeSpan.Minutes} minutes";
        }

        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            string text = (sender as TextBox)?.Text;
            if (text == null || (!Regex.IsMatch(text, PidGen.EpidPattern) && !Regex.IsMatch(text, App.GuidPattern)))
            {
                return;
            }

            e.CanExecute = true;
        }

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            new ProductBrowser(MainWindow, ((TextBox)sender).Text).Show();
        }

        private void CheckableTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            string text = (sender as TextBox)?.Text;
            if (text == null || (!Regex.IsMatch(text, PidGen.EpidPattern) && !Regex.IsMatch(text, App.GuidPattern)))
            {
                return;
            }

            CommandBinding_Executed(sender, null);
        }

        private void CheckBoxMultipleTest_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;

#if DEBUG
            Debug.Assert(checkBox.IsChecked != null);
#endif

            if (checkBox.IsChecked.Value)
            {
                CheckBoxDangerousTests.Visibility = Visibility.Visible;
                ButtonSendRequest.Content = "Server _Test";
            }
            else
            {
                CheckBoxDangerousTests.Visibility = Visibility.Collapsed;
                ButtonSendRequest.Content = "Send _Request";
                ButtonSendRequest.Foreground = Brushes.Black;
                CheckBoxDangerousTests.IsChecked = false;
            }
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private void CheckBoxDangerousTests_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            ButtonSendRequest.Content = checkBox.IsChecked.Value ? "Dangerous Server _Test" : "Server _Test";
            ButtonSendRequest.Foreground = checkBox.IsChecked.Value ? Brushes.Red : Brushes.Black;
        }

        private void AutoSize_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;
            menuItem.IsChecked = !menuItem.IsChecked;
            SizeToContent = menuItem.IsChecked ? SizeToContent.Height : SizeToContent.Manual;
        }

        private void ShowAllDetectionTests_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;
            menuItem.IsChecked = !menuItem.IsChecked;
            ShowEmulatorDetectionTests();
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Background = App.DatagridBackgroundBrushes[e.Row.GetIndex() % App.DatagridBackgroundBrushes.Count];
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
