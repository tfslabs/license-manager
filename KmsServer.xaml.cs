using HGM.Hotbird64.LicenseManager.Extensions;
using HGM.Hotbird64.Vlmcs;
using LicenseManager.Annotations;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HGM.Hotbird64.LicenseManager
{
    public struct ActivationInterval
    {
        public uint Interval;

        public static bool TryParse(string intervalString, out uint interval)
        {
            interval = 0;
            if (string.IsNullOrWhiteSpace(intervalString))
            {
                return false;
            }

            if (uint.TryParse(intervalString, NumberStyles.AllowThousands | NumberStyles.AllowTrailingSign, CultureInfo.CurrentCulture, out interval))
            {
                return true;
            }

            char multiplierChar = intervalString.ToUpperInvariant().Last();
            string numberString = intervalString.Substring(0, intervalString.Length - 1);

            if (!uint.TryParse(numberString, NumberStyles.AllowThousands | NumberStyles.AllowTrailingSign, CultureInfo.CurrentCulture, out uint rawValue))
            {
                return false;
            }

            switch (multiplierChar)
            {
                case 'S':
                    interval = (rawValue + 30) / 60;
                    break;
                case 'M':
                    interval = rawValue;
                    break;
                case 'H':
                    interval = rawValue * 60;
                    break;
                case 'D':
                    interval = rawValue * 60 * 24;
                    break;
                case 'W':
                    interval = rawValue * 60 * 24 * 7;
                    break;
                default:
                    return false;
            }

            return true;
        }

        public static explicit operator uint(ActivationInterval activationInterval) => activationInterval.Interval;
        public static explicit operator ActivationInterval(uint uInt) => new ActivationInterval { Interval = uInt };
    }

    public partial class KmsServer : INotifyPropertyChanged
    {
        private static readonly string nl = Environment.NewLine;
        public static Random Rand = new Random(unchecked((int)DateTime.UtcNow.Ticks));
        public ushort Port { get; private set; }
        public static int Lcid = 1033; //1033 is EN-US (locale)
        private static ObservableCollection<CsvlkItem> csvlks = new ObservableCollection<CsvlkItem>(KmsLists.CsvlkItemList.Where(c => c.Export).OrderBy(c => c.VlmcsdIndex));

        public ObservableCollection<CsvlkItem> Csvlks
        {
            get => csvlks;
            set
            {
                csvlks = value;
                NotifyOfPropertyChange();
            }
        }

        public IKmsProductCollection<AppItem> AppItems => KmsLists.AppItemList;

        private static bool isRunning;
        public bool IsRunning
        {
            get { return isRunning; }

            private set
            {
                isRunning = value;

                Dispatcher.InvokeAsync(() =>
                {

                    ButtonStartStop.Content = (value ? "Stop" : "Start") + " Server";
                    TextBoxPort.IsEnabled = !value;
                    CheckBoxPreCharge.IsEnabled = !value;
                    if (!string.IsNullOrEmpty(TextBoxInfoText.Text))
                    {
                        TextBoxInfoText.AppendText(nl);
                    }

                    string statusText = value ? "started" : "stopped";
                    TextBoxInfoText.AppendText($"vlmcsd {Kms.EmulatorVersion}, API level {Kms.ApiVersion.Major}.{Kms.ApiVersion.Minor} has been {statusText}.");
                    TextBoxInfoText.ScrollToEnd();

                    if (value)
                    {
                        UseTap.IsEnabled = false;

                        try
                        {
                            if (!UseTap.IsChecked.Value)
                            {
                                return;
                            }

                            Version version = TapMirror.Start(TextBoxTapIp.Text, ((TapMirror.TapDevice)ComboBoxTap.Items[ComboBoxTap.SelectedIndex]).Name);
                            ComboBoxTap.IsEnabled = false;
                            TextBoxInfoText.AppendText($"{nl}TAP {version} device \"{ComboBoxTap.SelectionBoxItem}\" has been started.");
                        }
                        catch (Exception ex)
                        {
                            TextBoxInfoText.AppendText($"{nl}TAP device \"{ComboBoxTap.SelectionBoxItem}\" could not be started: {ex.Message}");
                            try
                            {
                                Kms.StopServer();
                            }
                            catch
                            {
                                /**/
                            }
                        }
                    }
                    else
                    {
                        TapMirror.Stop();
                        UseTap.IsEnabled = TapMirror.GetTapDevices().Any();
                        ComboBoxTap.IsEnabled = true;
                        foreach (AppItem appItem in KmsLists.AppItemList)
                        {
                            appItem.Reset();
                        }
                    }
                });
            }
        }

        public KmsServer(MainWindow parentWindow) : base(parentWindow)
        {
            InitializeComponent();
            DataContext = this;
            TopElement.LayoutTransform = Scaler;

            TapMirror.TapDevice[] tapList = TapMirror.GetTapDevices().ToArray();

            UseTap.IsChecked = UseTap.IsEnabled = tapList.Any();
            ComboBoxTap.Visibility = UseTap.IsEnabled ? Visibility.Visible : Visibility.Collapsed;

            if (UseTap.IsEnabled)
            {
                ComboBoxTap.ItemsSource = tapList;
                ComboBoxTap.SelectedIndex = 0;
            }

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

            Title = $" KMS Server Powered By vlmcsd {Kms.EmulatorVersion}";

            Closed += (s, e) =>
            {
                TapMirror.Stop();

                try
                {
                    Kms.StopServer();
                }
                catch (KmsException)
                {
                }

                foreach (CsvlkItem csvlkItem in Csvlks)
                {
                    csvlkItem.PropertyChanged -= CsvlkItem_PropertyChanged;
                }
            };

            Loaded += (s, e) =>
            {
                Icon = this.GenerateImage(new Icons.KmsServerIcon(), 16, 16);

                foreach (CsvlkItem csvlkItem in Csvlks)
                {
                    csvlkItem.PropertyChanged += CsvlkItem_PropertyChanged;
                }
            };
        }

        private void CsvlkItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(sender is CsvlkItem csvlkItem))
            {
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(csvlkItem.IsRandom):

                    if (csvlkItem.IsRandom)
                    {
                        KmsLists.GetRandomEPid(csvlkItem);
                    }

                    break;
            }
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        public int ProcessKmsRequest(IntPtr requestPtr, IntPtr responsePtr, IntPtr hwIdPtr, IntPtr clientIpAddressPtr)
        {
            KmsRequest request = (KmsRequest)Marshal.PtrToStructure(requestPtr, typeof(KmsRequest));
            string clientIpAddress = Marshal.PtrToStringAnsi(clientIpAddressPtr);
            KmsResponse response = new KmsResponse();
            HwId hwId;
            int result = 0;
            CsvlkItem csvlkItem = Csvlks.SingleOrDefault(c => c.Activates.Select(a => a.Guid).Contains(request.KmsID)) ?? Csvlks[KmsLists.AppItemList[request.ID]?.VlmcsdIndex ?? 0];
            AppItem appItem = KmsLists.AppItemList[request.ApplicationID] ?? KmsLists.AppItemList.SingleOrDefault(a => a.VlmcsdIndex == 0) ?? KmsLists.AppItemList.First();

            Dispatcher.Invoke(() =>
            {
                try
                {
                    hwId.Text = TextBoxHwId.Text;
                }
                catch (Exception ex)
                {
                    TextBoxInfoText.AppendText(nl + ex.Message);
                }
            });

            response.ClientMachineID = request.ClientMachineID;
            response.TimeStamp = request.TimeStamp;
            response.Version = request.Version;
            bool allowUnknownProducts = false;
            bool allowRetailAndBetaProducts = false;
            bool checkTime = false;

            Dispatcher.Invoke(() =>
            {
                response.KmsPid.Text = csvlkItem.EPid;
                response.KmsPIDLen = (uint)(response.KmsPid.Text.Length + 1) << 1;
                if (!ActivationInterval.TryParse(TextBoxVlActivation.Text, out response.VLActivationInterval))
                {
                    response.VLActivationInterval = 120;
                }

                if (!ActivationInterval.TryParse(TextBoxVlRenewal.Text, out response.VLRenewalInterval))
                {
                    response.VLRenewalInterval = 7 * 24 * 60;
                }

                allowUnknownProducts = CheckBoxAllowUnknownProducts.IsChecked.Value;
                checkTime = CheckBoxCheckTime.IsChecked.Value;
                allowRetailAndBetaProducts = CheckBoxAllowNonVl.IsChecked.Value;
            });

            try
            {
                KmsData.Lock.AcquireReaderLock(500);

                try
                {
                    string product = KmsLists.SkuItemList[request.ID]?.ToString() ??
                                                KmsLists.KmsItemList[request.KmsID]?.ToString() ??
                                                KmsLists.AppItemList[request.ApplicationID]?.ToString() ??
                                                "Unknown";

                    KmsItem kmsItem = KmsLists.KmsItemList[request.KmsID];

                    if (!allowRetailAndBetaProducts && kmsItem != null && (kmsItem.IsPreview || kmsItem.IsRetail))
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            TextBoxInfoText.AppendText($"{nl}V{request.Version} request from {clientIpAddress} for {product} declined: ");
                            TextBoxInfoText.AppendText($"{(kmsItem.IsRetail ? "Retail" : "Beta")} product \"{product}\" not allowed.");
                        });

                        result = unchecked((int)0xc004f042);
                        return result;
                    }

                    if (!allowUnknownProducts &&
                            (KmsLists.KmsItemList[request.KmsID] == null ||
                             KmsLists.KmsItemList[request.KmsID].App.Guid != request.ApplicationID))
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            TextBoxInfoText.AppendText($"{nl}V{request.Version} request from {clientIpAddress} for {product} declined: ");
                            TextBoxInfoText.AppendText(KmsLists.KmsItemList[request.KmsID] == null ? $"Unknown KMS ID {request.KmsID}." : $"Incorrect App ID {request.ApplicationID}.");
                        });

                        result = unchecked((int)0xc004f042);
                        return result;
                    }

                    if (request.RequiredClientCount > 1000)
                    {
                        Dispatcher.InvokeAsync(() => TextBoxInfoText.AppendText($"{nl}V{request.Version} request from {clientIpAddress} for {product} declined: Required clients > 1000."));
                        result = unchecked((int)0x8007000d);
                        return result;
                    }

                    DateTime clientDate = DateTime.FromFileTime(request.TimeStamp).ToUniversalTime();

                    if (checkTime && Math.Abs((clientDate - DateTime.UtcNow).TotalHours) > 4.0)
                    {
                        Dispatcher.InvokeAsync(
                            () =>
                                TextBoxInfoText.AppendText(
                                    $"{nl}V{request.Version} request from {clientIpAddress} for {product} declined: Client time \"{clientDate.ToLocalTime()} {KmsClientWindow.CurrentTimeZone}\" > ± 4 hours from server time \"{DateTime.Now} {KmsClientWindow.CurrentTimeZone}\"."));
                        result = unchecked((int)0xc004f06c);
                        return result;
                    }

                    appItem.MaxActiveClients = Math.Max((int)request.RequiredClientCount << 1, appItem.MaxActiveClients);
                    appItem.AddClient(request.ClientMachineID);
                    response.KMSCurrentCount = (uint)appItem.Queue.Count;

                    if (response.KMSCurrentCount > 671)
                    {
                        Dispatcher.InvokeAsync(() => TextBoxInfoText.AppendText($"{nl}V{request.Version} request from {clientIpAddress} for {product} declined: > 671 clients."));
                        result = unchecked((int)0xc004d104);
                        return result;
                    }


                    Dispatcher.InvokeAsync(() =>
                    {
                        TextBoxInfoText.AppendText($"{nl}V{response.Version.Major}.{response.Version.Minor} response for {product} sent to {request.WorkstationName.Text} ({clientIpAddress}).");
                    });
                }
                finally
                {
                    KmsData.Lock.ReleaseReaderLock();

                    Dispatcher.InvokeAsync(() =>
                    {
                        if (result != 0)
                        {
                            TextBoxInfoText.AppendText($" Error 0x{unchecked((uint)result):X8}: {Kms.StatusMessage(unchecked((uint)result))}");
                        }
                        TextBoxInfoText.ScrollToEnd();
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    TextBoxInfoText.AppendText($"{nl}Request not logged: {ex.Message}");
                    TextBoxInfoText.ScrollToEnd();
                });
            }

            Marshal.StructureToPtr(response, responsePtr, true);
            if (request.Version.Major > 5 && hwIdPtr != IntPtr.Zero)
            {
                Marshal.StructureToPtr(hwId, hwIdPtr, true);
            }

            return 0;
        }

        private void KmsServerThread()
        {
            try
            {
                Kms.StartServer(Port, ProcessKmsRequest);
                IsRunning = false;
            }
            catch (KmsException ex)
            {
                IsRunning = false;
                Dispatcher.Invoke(() => MessageBox.Show(this, ex.Message, "KMS server error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private void Button_StartStop_Click(object sender, RoutedEventArgs e)
        {
            if (!IsRunning)
            {
                if (TextBoxPort.Background.Equals(Brushes.OrangeRed))
                {
                    MessageBox.Show(TextBoxPort.ToolTip.ToString(), "Incorrect TCP-Port", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Port = ushort.Parse(TextBoxPort.Text, NumberStyles.None, CultureInfo.CurrentCulture);

                if (TextBoxTapIp.Background.Equals(Brushes.OrangeRed))
                {
                    MessageBox.Show("TAP IPv4/CIDR is invalid", null, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                IsRunning = true;
                KmsLists.LoadDatabase();
                foreach (AppItem appItem in KmsLists.AppItemList)
                {
                    appItem.PreCharge(CheckBoxPreCharge.IsChecked.Value);
                }
                new Thread(KmsServerThread).Start();
            }
            else
            {
                Kms.StopServer();
            }
        }

        private void ActivationInterval_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;

            TextBox parsedTextBox = ((Grid)textBox.Parent).Children.OfType<TextBox>().FirstOrDefault(uiElement => uiElement.Name == textBox.Name + "Parsed");
            bool isValidInterval = ActivationInterval.TryParse(textBox.Text, out uint validationInterval);

            if (!isValidInterval)
            {
                textBox.Background = Brushes.OrangeRed;

                if (parsedTextBox != null)
                {
                    validationInterval = parsedTextBox.Name == nameof(TextBoxVlRenewalParsed) ? 10080U : 120U;
                }
            }
            else
            {
                textBox.Background = Brushes.LightGreen;
            }

            TimeSpan timeSpan = TimeSpan.FromMinutes(validationInterval);
            if (parsedTextBox != null)
            {
                parsedTextBox.Text = $"{timeSpan.Days} days, {timeSpan.Hours} hours, {timeSpan.Minutes} minutes";
            }
        }

        private void UseTap_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            TextBoxTapIp.Visibility = ComboBoxTap.Visibility = checkBox.IsChecked.Value ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TextBoxTapIp_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            bool isValid = TapMirror.IsValidSubnet(textBox.Text, out string errorReason);
            textBox.ToolTip = errorReason;
            textBox.Background = isValid ? Brushes.LightGreen : Brushes.OrangeRed;
        }

        private void TextBoxHwId_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            HwId hwid;

            try
            {
                hwid.Text = textBox.Text;
                textBox.Background = Brushes.LightGreen;
                textBox.ToolTip = null;
            }
            catch (Exception ex)
            {
                textBox.Background = Brushes.OrangeRed;
                textBox.ToolTip = ex.Message;
            }
        }

        private void TextBoxPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;

            if (!ushort.TryParse(textBox.Text, NumberStyles.None, CultureInfo.CurrentCulture, out ushort port) || port == 0)
            {
                textBox.Background = Brushes.OrangeRed;
                textBox.ToolTip = "Port must be an integer number between 1 and 65535";
                return;
            }

            textBox.Background = Brushes.LightGreen;
            textBox.ToolTip = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
