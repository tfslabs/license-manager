using HGM.Hotbird64.Vlmcs;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.LicenseManager
{
    public partial class ConnectForm
    {
        private readonly LicenseMachine machine;
        private bool showUI;
        private readonly NativeMethods.AuthPrompt auth = new NativeMethods.AuthPrompt();
        private NativeMethods.AuthPrompt.CredUiReturnCodes rc;
        private readonly MainWindow parent;

        private string ComputerName
        {
            get { return Dispatcher.Invoke(() => Kms.Idn.GetAscii(TextBoxComputername.Text)); }
            set { Dispatcher.InvokeAsync(() => TextBoxComputername.Text = Kms.Idn.GetUnicode(value)); }
        }

        public ConnectForm(MainWindow parent)
        {
            this.parent = parent;
            machine = this.parent.Machine;
            InitializeComponent();
            TopElement.LayoutTransform = Scaler;
            parent.LabelStatus.Text = "Getting Computer Name";
            ButtonConnectLocal.IsEnabled = machine.ComputerName != ".";
        }

        private async Task Connect_Worker()
        {
            try
            {
                MainGrid.IsEnabled = false;

                await Task.Run(() =>
                {
                    machine.Connect(ComputerName, auth.User, auth.SecurePassword, machine.IncludeInactiveLicenses);
                });

                DialogResult = true;
                rc = auth.ConfirmCredentials(true);
            }
            catch (UnauthorizedAccessException ex)
            {
                parent.IsProgressBarRunning = false;
                rc = auth.ConfirmCredentials(false);

                if (showUI)
                {
                    parent.LabelStatus.Text = "Access denied";
                    MessageBox.Show(ex.Message, "Access denied", MessageBoxButton.OK, MessageBoxImage.Error);
                    parent.LabelStatus.Text = "Getting Computer Name";
                }
                else
                {
                    showUI = true;
                }

                MainGrid.IsEnabled = true;
            }
            catch (COMException ex)
            {
                parent.IsProgressBarRunning = false;
                parent.LabelStatus.Text = "DCOM Error";
                switch ((uint)ex.ErrorCode)
                {
                    case 0x80070776:
                        MessageBox.Show(ex.Message, "Microsoft Is Lame", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    default:
                        MessageBox.Show(ex.Message, "DCOM Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
                parent.LabelStatus.Text = "Getting Computer Name";
                MainGrid.IsEnabled = true;
            }
            catch (Exception ex)
            {
                parent.IsProgressBarRunning = false;
                parent.LabelStatus.Text = "Error";
                MessageBox.Show("The following error occured: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                parent.LabelStatus.Text = "Getting Computer Name";
                MainGrid.IsEnabled = true;
            }
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        public async void button_Connect_Click(object sender, RoutedEventArgs e)
        {
            if (ComputerName == "")
            {
                MessageBox.Show("Try entering something in the field Computername!", "Noob Alert!!!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            parent.LabelStatus.Text = "Connecting...";
            MainGrid.IsEnabled = false;

            if (ComputerName != ".")
            {
                auth.ServerName = ComputerName;
                if (!showUI) showUI = CheckBoxShowUi.IsChecked.Value;
                MainGrid.IsEnabled = false;

                rc = auth.PromptForPassword(showUI,
                                            "Enter username and password of an account with administrative privileges on "
                                            + ComputerName,
                                            "Authentication Required");

                parent.IsProgressBarRunning = true;

                if (rc == NativeMethods.AuthPrompt.CredUiReturnCodes.ERROR_CANCELLED)
                {
                    DialogResult = false;
                    return;
                }
            }

            await Connect_Worker();
        }

        private void button_Local_Click(object sender, RoutedEventArgs e)
        {
            ComputerName = ".";
            button_Connect_Click(sender, e);
        }

    }
}
