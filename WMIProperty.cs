using HGM.Hotbird64.Vlmcs;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.LicenseManager
{
    internal class WmiProperty
    {
        public string Servicename;
        public ManagementObject ManagementObject;
        public object Value { private set; get; }
        private string property;
        private readonly bool showAllFields;

        public string Property
        {
            get => property;

            set
            {
                property = value;

                try
                {
                    Value = ManagementObject[property] ?? "";
                }
                catch (ManagementException ex)
                {
                    switch (ex.ErrorCode)
                    {
                        case ManagementStatus.NotFound:
                            Value = null;
                            break;
                        default:
                            throw;
                    }
                }
                catch
                {
                    if (Environment.OSVersion.Version.Build > 6002)
                    {
                        throw;
                    }

                    Value = null;
                }
            }
        }

        public WmiProperty(string servicename, ManagementObject managementObject, bool showAllFields)
        {
            Servicename = servicename;
            ManagementObject = managementObject;
            this.showAllFields = showAllFields;
        }

        /*static public implicit operator CheckState(WMIProperty wmiProperty)
		{
			if ((UInt32)wmiProperty.Value == unchecked((UInt32)(-1))) return CheckState.Indeterminate;
			return (CheckState)(UInt32)wmiProperty.Value;
		}*/

        public void DisplayPropertyAsPort(TextBox textBox, string p)
        {
            Property = p;
            if (Value != null)
            {
                textBox.Text = (uint)Value == 0 ? "1688" : Value.ToString();
                Show(textBox);
            }
            else
            {
                Hide(textBox, showAllFields);
                textBox.Text = "N/A";
            }
        }

        public void DisplayAdditionalProperty(TextBox textBox, string p)
        {
            Property = p;
            if (Value != null && Value.ToString() != "")
            {
                textBox.Text += $" ({Value})";
            }
        }

        public static void Show(IEnumerable<Control> controls, TextBox textbox, bool show = true, bool showAllFields = false)
        {
            if (!show)
            {
                Hide(controls, textbox, showAllFields);
                return;
            }

            Show(textbox);

            foreach (Control control in controls)
            {
                Show(control);
            }
        }

        public static void Show(Control control, TextBox textbox, bool show = true, bool showAllFields = false)
        {
            Show(new[] { control }, textbox, show, showAllFields);
        }

        public static void Show(Control control, bool show = true, bool showAllFields = false)
        {
            if (!show)
            {
                Hide(control, showAllFields);
                return;
            }

            control.Visibility = Visibility.Visible;
            control.IsEnabled = true;
        }
        public static void Hide(Control control, bool showAllFields)
        {
            if (showAllFields)
            {
                control.IsEnabled = false;
                control.Visibility = Visibility.Visible;
            }
            else
            {
                control.IsEnabled = true;
                control.Visibility = Visibility.Collapsed;
            }
        }

        public static void Hide(IEnumerable<Control> controls, TextBox textbox, bool showAllFields)
        {
            Hide(textbox, showAllFields);

            foreach (Control control in controls)
            {
                Hide(control, showAllFields);
            }
        }

        public static void Hide(Control control, TextBox textbox, bool showAllFields)
        {
            Hide(new[] { control }, textbox, showAllFields);
        }

        private void Hide(IEnumerable<Control> controls, TextBox textbox)
        {
            Hide(controls, textbox, showAllFields);
        }

        private void Hide(Control control, TextBox textbox)
        {
            Hide(control, textbox, showAllFields);
        }

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        public void DisplayPropertyAsPeriodRemaining(IEnumerable<Control> controls, TextBox textbox, string p)
        {
            Property = p;

            if (Value == null)
            {
                Hide(controls, textbox);
                textbox.Text = "(unsupported in " + Servicename + ")";
                return;
            }

            Show(controls, textbox);

            double minutesRemaining = (double)(uint)Value;
            DateTime tempDate = DateTime.Now.AddMinutes((uint)Value);
            textbox.Text = (minutesRemaining == 0.0)
                    ? ("forever (unless you install a new key or tamper with the license tokens)")
                    : (Math.Round(minutesRemaining / 24.0 / 60.0).ToString(CultureInfo.CurrentCulture)) + " days, until " +
                    tempDate.ToLongDateString() + " " + tempDate.ToShortTimeString();
        }

        public void DisplayPropertyAsLicenseStatus(IEnumerable<Control> controls, TextBox textBox)
        {
            Property = "LicenseStatus";
            if (!(Value is uint))
            {
                Hide(controls, textBox);
                textBox.Text = "N/A";
            }
            try
            {
                uint licenseStatus = (uint)Value;
                string licenseStatusString = Model.LicenseStatus.GetText(licenseStatus);

                switch (licenseStatus)
                {
                    case 0:
                    case 5:
                        textBox.Background = Brushes.OrangeRed;
                        break;
                    case 1:
                        textBox.Background = Brushes.LightGreen;
                        break;
                    default:
                        textBox.Background = Brushes.Yellow;
                        break;
                }

                Property = "LicenseStatusReason";

                if (Value != null)
                {
                    licenseStatusString += ": " + Kms.StatusMessage((uint)Value);
                }

                textBox.Text = licenseStatusString;
                Show(controls, textBox);
            }
            catch
            {
                Hide(controls, textBox);
                textBox.Text = "N/A";
                textBox.Background = App.DefaultTextBoxBackground;
            }
        }

        public void DisplayProperty(IEnumerable<Control> controls, TextBox textbox, string p, bool reportUnsupported = true)
        {
            Property = p;
            if (Value == null)
            {
                Hide(controls, textbox);
                textbox.Text = reportUnsupported ? "(unsupported in " + Servicename + ")" : "";
            }
            else if (Value is uint && (uint)Value == uint.MaxValue)
            {
                textbox.Text = "N/A";
                Hide(controls, textbox);
            }
            else if (Value is string && (string)Value == "" && textbox.IsReadOnly)
            {
                Hide(controls, textbox);
                textbox.Text = "N/A";
            }
            else
            {
                textbox.Text = Value.ToString();
                Show(controls, textbox);
            }
        }

        public void DisplayProperty(Control control, TextBox textbox, string p, bool reportUnsupported = true)
        {
            DisplayProperty(new[] { control }, textbox, p, reportUnsupported);
        }

        public void DisplayPropertyAsDate(IEnumerable<Control> controlEnumerable, TextBox textBox, string p)
        {
            IList<Control> controls = controlEnumerable as IList<Control> ?? controlEnumerable.ToArray();

            Property = p;
            try
            {
                DateTime tempDate = ManagementDateTimeConverter.ToDateTime((string)Value).ToUniversalTime();
                if (tempDate.Year <= 1601)
                {
                    Hide(controls, textBox);
                    textBox.Text = "Never";
                }
                else
                {
                    textBox.Text = tempDate.ToLocalTime().ToLongDateString() + " " + tempDate.ToLongTimeString();
                    Show(controls, textBox);
                }

            }
            catch
            {
                Hide(controls, textBox);
                textBox.Text = "N/A";
            }
        }

        public void DisplayPid
        (
            Control pidControl, TextBox pidBox,
            Control osControl, TextBox osBox,
            Control dateControl, TextBox dateBox,
            string p
        )
        {
            Property = p;

            if (Value == null || (string)Value == "")
            {
                Hide(pidControl, pidBox);
                Hide(osControl, osBox);
                Hide(dateControl, dateBox);
                pidBox.Text = osBox.Text = dateBox.Text = "N/A";
                return;
            }

            EPid pid = new EPid(Value);
            pidBox.Text = pid.Id;
            Show(pidControl, pidBox);

            try
            {
                osBox.Text = pid.LongOsName;
                Show(osControl, osBox);
            }
            catch
            {
                Hide(osControl, osBox);
                osBox.Text = "Unknown OS";
            }

            try
            {
                dateBox.Text = pid.LongDateString;
                Show(dateControl, dateBox);
            }
            catch
            {
                Hide(dateControl, dateBox);
                dateBox.Text = "Unknown Date";
            }
        }

        public void SetCheckBox(CheckBox checkBox, string p)
        {
            Property = p;
            checkBox.IsChecked = (Value == null ? null : (bool?)((uint)Value == 0));
        }
    }
}
