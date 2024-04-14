using HGM.Hotbird64.LicenseManager.Extensions;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.LicenseManager
{
    public partial class ServiceConfiguration
    {
        private LicenseMachine.LicenseProvider licenseProvider;
        private readonly LicenseMachine machine;
        private bool kmsHostDirty;
        private bool serverParametersDirty;
        public bool MainDialogRefreshRequired;
        private MainWindow owner;
        ManagementObject serviceParameters;

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        private bool KmsHostDirty
        {
            get
            {
                return kmsHostDirty;
            }
            set
            {
                kmsHostDirty = value;
                ButtonSave.IsEnabled = value;
            }
        }

        /*private void CancelButton_Click(object sender, CancelEventArgs e)
		{
			if (!groupBox_Outer.Enabled)
			{
				e.Cancel = true;
				MessageBox.Show(this, "Please wait for current action to complete.", "Be Patient", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			_dlg.label_Status.Text = "Ready";
		}*/

        private async void GetParameters()
        {
            try
            {
                await Task.Run(() => serviceParameters = machine.GetLicenseProviderParameters(licenseProvider.LicenseClassName));
            }
            catch (Exception ex)
            {
                MessageBox.Show
                (
                    this,
                    ex.Message,
                    "Unable to Get Parameters for " + licenseProvider.FriendlyName + " " + licenseProvider.Version,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                DialogResult = false;
            }

            RefreshWindow();
        }

        private void RefreshWindow()
        {
            WmiProperty w = new WmiProperty("Version " + licenseProvider.Version, serviceParameters, MenuItemShowAllFields.IsChecked);

            w.DisplayProperty(LabelClientMachineId, TextBoxClientMachineID, "ClientMachineID");

            w.DisplayProperty(LabelKeyManagementServiceLookupDomain, TextBoxKeyManagementServiceLookupDomain, "KeyManagementServiceLookupDomain");
            w.DisplayProperty(LabelKeyManagementServiceMachine, TextBoxKeyManagementServiceMachine, "KeyManagementServiceMachine");
            w.DisplayProperty(LabelRemainingWindowsReArmCount, TextBoxRemainingWindowsReArmCount, "RemainingWindowsReArmCount");
            w.DisplayPropertyAsPort(TextBoxKeyManagementServicePort, "KeyManagementServicePort");
            w.DisplayPropertyAsPort(TextBoxDiscoveredKeyManagementServiceMachinePort, "DiscoveredKeyManagementServiceMachinePort");
            w.DisplayProperty(LabelDiscoveredKeyManagementServiceMachineIpAddress, TextBoxDiscoveredKeyManagementServiceMachineIpAddress, "DiscoveredKeyManagementServiceMachineIpAddress");

            w.DisplayProperty
            (
                new Control[]
                {
                    LabelDiscoveredKeyManagementServiceMachineName,
                    TextBoxDiscoveredKeyManagementServiceMachinePort,
                    LabelColon1,
                },
                TextBoxDiscoveredKeyManagementServiceMachineName,
                "DiscoveredKeyManagementServiceMachineName"
            );

            w.Property = "IsKeyManagementServiceMachine";
            bool kmsServerEnabled = false;

            try
            {
                switch ((uint)w.Value)
                {
                    case 1:
                        TextBoxIsKeyManagementServiceMachine.Text = "Running";
                        kmsServerEnabled = true;
                        break;
                    case 0:
                        TextBoxIsKeyManagementServiceMachine.Text = "Disabled";
                        break;
                    default:
                        TextBoxIsKeyManagementServiceMachine.Text = "N/A";
                        break;
                }
            }
            catch
            {
                TextBoxIsKeyManagementServiceMachine.Text = "Error";
            }

            //WMIProperty.Show(groupBox_KmsServer, KmsServerEnabled, menuItem_ShowAllFields.IsChecked);
            WmiProperty.Show(GroupBoxHost, kmsServerEnabled, MenuItemShowAllFields.IsChecked);

            if (GroupBoxHost.Visibility == Visibility.Collapsed)
            {
                RightColumn.Width = new GridLength(1, GridUnitType.Star);
                LeftColumn.Width = GridLength.Auto;
            }
            else
            {
                RightColumn.Width = new GridLength(90, GridUnitType.Star);
                LeftColumn.Width = new GridLength(120, GridUnitType.Star);
            }

            w.Property = "KeyManagementServiceDnsPublishing";
            CheckBoxKeyManagementServiceDnsPublishing.IsEnabled = w.Value != null;
            CheckBoxKeyManagementServiceDnsPublishing.IsChecked = (bool?)w.Value;

            w.Property = "KeyManagementServiceLowPriority";
            CheckBoxKeyManagementServiceLowPriority.IsChecked = (bool?)w.Value;
            CheckBoxKeyManagementServiceLowPriority.IsEnabled = (bool?)w.Value != null;

            w.DisplayPropertyAsPort(TextBoxKeyManagementServiceListeningPort, "KeyManagementServiceListeningPort");
            w.DisplayProperty(LabelVlActivationInterval, TextBoxVlActivationInterval, "VLActivationInterval");
            w.DisplayProperty(LabelVlRenewalInterval, TextBoxVlRenewalInterval, "VLRenewalInterval");
            //if (w.Value == null) button_Rearm.Enabled = false;
            w.Property = "KeyManagementServiceHostCaching";
            CheckBoxKeyManagementServiceHostCaching.IsChecked = (bool?)w.Value ?? false;
            CheckBoxKeyManagementServiceHostCaching.IsEnabled = w.Value != null;

            w.DisplayProperty(LabelRequiredClientCount, TextBoxRequiredClientCount, "RequiredClientCount");
            uint requiredClientCount = (uint)w.Value;

            w.DisplayProperty(LabelKeyManagementServiceCurrentCount, TextBoxKeyManagementServiceCurrentCount, "KeyManagementServiceCurrentCount");
            uint currentCount = (uint)w.Value;

            if (kmsServerEnabled)
            {
                if (currentCount < requiredClientCount)
                    TextBoxKeyManagementServiceCurrentCount.Background = Brushes.OrangeRed;
                else
                    TextBoxKeyManagementServiceCurrentCount.Background = Brushes.LightGreen;
            }
            else
            {
                TextBoxKeyManagementServiceCurrentCount.Background = App.DefaultTextBoxBackground;
            }


            w.DisplayProperty(LabelKeyManagementServiceTotalRequests, TextBoxKeyManagementServiceTotalRequests, "KeyManagementServiceTotalRequests");
            w.DisplayProperty(LabelKeyManagementServiceFailedRequests, TextBoxKeyManagementServiceFailedRequests, "KeyManagementServiceFailedRequests");
            w.DisplayProperty(LabelKeyManagementServiceUnlicensedRequests, TextBoxKeyManagementServiceUnlicensedRequests, "KeyManagementServiceUnlicensedRequests");
            w.DisplayProperty(LabelKeyManagementServiceLicensedRequests, TextBoxKeyManagementServiceLicensedRequests, "KeyManagementServiceLicensedRequests");


            w.DisplayProperty(LabelKeyManagementServiceNonGenuineGraceRequests, TextBoxKeyManagementServiceNonGenuineGraceRequests, "KeyManagementServiceNonGenuineGraceRequests");
            w.DisplayProperty(LabelKeyManagementServiceNotificationRequests, TextBoxKeyManagementServiceNotificationRequests, "KeyManagementServiceNotificationRequests");
            w.DisplayProperty(LabelKeyManagementServiceOobGraceRequests, TextBoxKeyManagementServiceOobGraceRequests, "KeyManagementServiceOOBGraceRequests");
            w.DisplayProperty(LabelKeyManagementServiceOotGraceRequests, TextBoxKeyManagementServiceOotGraceRequests, "KeyManagementServiceOOTGraceRequests");

            KmsHostDirty = false;
            serverParametersDirty = false;
            owner.ControlsEnabled = true;
            LabelServiceStatus.Text = "";
        }

        public ServiceConfiguration(LicenseMachine machine, LicenseMachine.LicenseProvider licenseProvider, UIElement icon)
        {
            this.licenseProvider = licenseProvider;
            this.machine = machine;
            InitializeComponent();
            MainGrid.LayoutTransform = Scaler;
            Title = $"{licenseProvider.FriendlyName} {licenseProvider.Version} Settings";
            Loaded += (s, e) => Icon = this.GenerateImage(icon, 16, 16);
        }

        private void ServiceConfiguration_Load(object sender, RoutedEventArgs e)
        {
            owner = (MainWindow)Owner;
            owner.LabelStatus.Text = "Configuring " + licenseProvider.FriendlyName + " " + licenseProvider.Version;
            LabelServiceStatus.Text = "Gathering Data";
            MenuItemShowAllFields.IsChecked = true;
            GetParameters();
        }

        private void KmsParameter_TextChanged_ReloadRequired(object sender, RoutedEventArgs e)
        {
            KmsHostDirty = true;
            serverParametersDirty = true;
        }

        private void KmsParameters_TextChanged(object sender, RoutedEventArgs e)
        {
            KmsHostDirty = true;
        }

        private void menuItem_ShowAllFields_Click(object sender, RoutedEventArgs e)
        {
            RefreshWindow();
        }

        private static T GetControlContent<T>(Control control, T t)
        {
            if (control.IsEnabled && control.Visibility == Visibility.Visible)
            {
                return t;
            }

            return default(T);
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private async void button_Save_Click(object sender, RoutedEventArgs e)
        {
            LabelServiceStatus.Text = "Saving KMS settings";

            string domain = GetControlContent(TextBoxKeyManagementServiceLookupDomain, TextBoxKeyManagementServiceLookupDomain.Text);
            string host = GetControlContent(TextBoxKeyManagementServiceMachine, TextBoxKeyManagementServiceMachine.Text);
            string port = GetControlContent(TextBoxKeyManagementServicePort, TextBoxKeyManagementServicePort.Text);

            bool hostCaching = (bool)CheckBoxKeyManagementServiceHostCaching.IsChecked;
            string listenPort = GetControlContent(TextBoxKeyManagementServiceListeningPort, TextBoxKeyManagementServiceListeningPort.Text);
            string clientActivationInterval = GetControlContent(TextBoxVlActivationInterval, TextBoxVlActivationInterval.Text);
            string clientRenewalInterval = GetControlContent(TextBoxVlRenewalInterval, TextBoxVlRenewalInterval.Text);
            bool? enableDnsPublishing = GetControlContent(CheckBoxKeyManagementServiceDnsPublishing, CheckBoxKeyManagementServiceDnsPublishing.IsChecked);
            bool? runWithLowPriority = GetControlContent(CheckBoxKeyManagementServiceLowPriority, CheckBoxKeyManagementServiceLowPriority.IsChecked);
            MainDialogRefreshRequired = true;

            MainGrid.IsEnabled = false;

            try
            {
                await Task.Run(() =>
                {
                    machine.SetKeyManagementOverrides_Service
                    (
                        licenseProvider,
                        domain,
                        host,
                        port
                    );

                    machine.SetKeyManagementServiceHostCaching(licenseProvider, hostCaching);

                    machine.SetKmsHostParameters(licenseProvider,
                                                  listenPort,
                                                  clientActivationInterval,
                                                  clientRenewalInterval,
                                                  enableDnsPublishing,
                                                  runWithLowPriority);
                });
            }
            catch (Exception ex)
            {
                LabelServiceStatus.Text = "Error saving settings";

                MessageBox.Show
                (
                    this,
                    ex.Message,
                    "Not All Settings Could Be Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                LabelServiceStatus.Text = "";
                MainGrid.IsEnabled = true;
            }

            if
            (
                serverParametersDirty &&
                MessageBox.Show
                (
                    this,
                    $"Some parameters you changed require that {licenseProvider.FriendlyName} {licenseProvider.Version} will be reloaded. Dow you want to do that now?",
                    $"Reload {licenseProvider.FriendlyName} {licenseProvider.Version}",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes
                ) == MessageBoxResult.Yes
            )
            {
                reloadServiceToolStripMenuItem_Click(sender, e);
            }

            DialogResult = true;
        }

        private async void MenuItemInstallLicenseFiles_Click(object sender, RoutedEventArgs e)
        {
            MainDialogRefreshRequired = true;

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "License files (*.xrm-ms)|*.xrm-ms|All files (*.*)|*",
                Multiselect = true,
                Title = "License Files to Install on "
                      + (machine.ComputerName == "." ? "the Local Machine" : machine.ComputerName)
            };


            LabelServiceStatus.Text = "Installing License Files";
            openFileDialog.ShowDialog(this);

            IEnumerable<string> fileNames = openFileDialog.FileNames;

            MainGrid.IsEnabled = false;
            string errorstring = "";

            await Task.Run(() =>
            {
                foreach (string fileName in fileNames)
                {
                    Dispatcher.Invoke(() => LabelServiceStatus.Text = "Installing " + Path.GetFileName(fileName));

                    try
                    {
                        machine.InstallLicenseFile(licenseProvider, fileName);
                    }
                    catch (XmlException ex)
                    {
                        errorstring += Environment.NewLine + "The file " + Path.GetFileName(fileName) + " is not a valid XML file. " + ex.Message;
                        continue;
                    }
                    catch (IOException ex)
                    {
                        errorstring += Environment.NewLine + "Error reading file " + Path.GetFileName(fileName) + ": " + ex.Message;
                        continue;
                    }
                    catch (Exception ex)
                    {
                        errorstring += Environment.NewLine + "Error installing license file " + Path.GetFileName(fileName) + ": " + ex.Message;
                        continue;
                    }
                }
            });

            if (errorstring != "")
            {
                LabelServiceStatus.Text = "Error while Installing Files";

                MessageBox.Show
                (
                    this,
                    errorstring,
                    "Error while Installing Files",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }

            MainGrid.IsEnabled = true;
            LabelServiceStatus.Text = "";
        }

        private void HandleServiceError(Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Service Control Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                MainDialogRefreshRequired = true;
                uint result = 0;
                LabelServiceStatus.Text = "Starting " + licenseProvider.FriendlyName + " " + licenseProvider.Version;
                MainGrid.IsEnabled = false;

                await Task.Run(() => result = machine.StartService(licenseProvider));

                switch (result)
                {
                    case 0:
                        MessageBox.Show(this, licenseProvider.FriendlyName + " was successfully started.", "Success",
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    case 10:
                        MessageBox.Show(this, licenseProvider.FriendlyName + " had already been started.", "No Effect",
                                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        break;
#if DEBUG
                    default:
                        MessageBox.Show(this, licenseProvider.FriendlyName +
                                        " returned an impossible value that should have thrown an exception.",
                                        "Stupid Developer", MessageBoxButton.OK, MessageBoxImage.Stop);
                        break;
#endif
                }
            }
            catch (Exception ex)
            {
                HandleServiceError(ex);
            }

            LabelServiceStatus.Text = "";
            MainGrid.IsEnabled = true;
        }

        private async void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                MainDialogRefreshRequired = true;
                uint result = 0;
                LabelServiceStatus.Text = "Stopping " + licenseProvider.FriendlyName + " " + licenseProvider.Version;
                MainGrid.IsEnabled = false;

                await Task.Run(() => result = machine.StopService(licenseProvider));

                switch (result)
                {
                    case 0:
                        MessageBox.Show(this, licenseProvider.FriendlyName + " was successfully stopped.", "Success",
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    case 5:
                    case 10:
                        MessageBox.Show(this, licenseProvider.FriendlyName + " had already been stopped.", "No Effect",
                                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        break;
#if DEBUG
                    default:
                        MessageBox.Show(this, licenseProvider.FriendlyName +
                                        " returned an impossible value that should have thrown an exception.",
                                        "Stupid Developer", MessageBoxButton.OK, MessageBoxImage.Stop);
                        break;
#endif
                }
            }
            catch (Exception ex)
            {
                HandleServiceError(ex);
            }

            LabelServiceStatus.Text = "";
            MainGrid.IsEnabled = true;
        }

        private async void restartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                MainDialogRefreshRequired = true;
                LabelServiceStatus.Text = "Restarting " + licenseProvider.FriendlyName + " " + licenseProvider.Version;
                MainGrid.IsEnabled = false;

                await Task.Run(() =>
                {
                    machine.StopService(licenseProvider);
                    machine.StartService(licenseProvider);
                });

                MessageBox.Show(this, licenseProvider.FriendlyName + " has been restarted.", "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HandleServiceError(ex);
            }

            LabelServiceStatus.Text = "";
            MainGrid.IsEnabled = true;
        }

        private async void reloadServiceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                MainDialogRefreshRequired = true;
                LabelServiceStatus.Text = "Reloading " + licenseProvider.FriendlyName + " " + licenseProvider.Version;
                MainGrid.IsEnabled = false;

                await Task.Run(() => machine.RefreshLicenseStatus(licenseProvider));

                MessageBox.Show(this, licenseProvider.FriendlyName + " has been reloaded.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error Reloading Service", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            LabelServiceStatus.Text = "";
            MainGrid.IsEnabled = true;
        }

        private async void rearmToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainDialogRefreshRequired = true;
            MainGrid.IsEnabled = false;

            if (MessageBox.Show(this, "By rearming your service, you will loose all " +
                            "licenses and activations associated with it." + Environment.NewLine + Environment.NewLine +
                            "Are you sure, you want to do this?", "Rearm Warning",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                try
                {
                    LabelServiceStatus.Text = "Rearming " + licenseProvider.FriendlyName + " " + licenseProvider.Version;

                    await Task.Run(() => machine.ReArmWindows(licenseProvider));

                    MessageBox.Show(this, licenseProvider.FriendlyName +
                                    " has been rearmed. Reboot your machine as soon as possible.",
                                    "Rearm Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Error Rearming " + licenseProvider.FriendlyName,
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }

                LabelServiceStatus.Text = "";
            }

            MainGrid.IsEnabled = true;
        }
    }
}
