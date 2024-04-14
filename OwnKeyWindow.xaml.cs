using System;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using HGM.Hotbird64.Vlmcs;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;
using HGM.Hotbird64.LicenseManager.Extensions;

namespace HGM.Hotbird64.LicenseManager
{
    public partial class OwnKeyWindow
    {
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
        private class KeyListItem
        {
            public string ProductName { get; set; }
            public DateTime InstallDate { get; set; }
            public string HiveName { get; set; }
            public DigitalProductId3 Id3 { get; set; }
            public DigitalProductId4 Id4 { get; set; }
            public override string ToString() => $"{InstallDate}: {ProductName}";
            public string DisplayDate => $"{(InstallDate != DateTimeExtensions.Epoch ? InstallDate.ToString(CultureInfo.CurrentCulture) : "")}";
        }

        private class HiveItem
        {
            public string HiveName;
            public string DisplayName;
        }

        private static readonly IReadOnlyList<HiveItem> officeList = new[]
        {
      new HiveItem { HiveName=@"SOFTWARE\Microsoft\Office\14.0\Registration", DisplayName="Office2010", },
      new HiveItem { HiveName=@"SOFTWARE\Microsoft\Office\15.0\Registration", DisplayName="Office2013", },
      new HiveItem { HiveName=@"SOFTWARE\Microsoft\Office\16.0\Registration", DisplayName="Office2016", },
    };

        private static readonly IReadOnlyList<HiveItem> alternateWindowsList = new[]
        {
      new HiveItem { HiveName=@"SOFTWARE\Microsoft\Internet Explorer\Registration",               DisplayName="Current Windows Key from IE", },
      new HiveItem { HiveName=@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\DefaultProductKey",  DisplayName="Default Windows Key 1", },
      new HiveItem { HiveName=@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\DefaultProductKey2", DisplayName="Default Windows Key 2", },
    };

        private static readonly IReadOnlyList<HiveItem> sqlServerList = new[]
        {
      new HiveItem { HiveName = @"SOFTWARE\Microsoft\Microsoft SQL Server\130\Tools\Setup", DisplayName = "SQL Server 2016", },
      new HiveItem { HiveName = @"SOFTWARE\Microsoft\Microsoft SQL Server\120\Tools\Setup", DisplayName = "SQL Server 2014", },
      new HiveItem { HiveName = @"SOFTWARE\Microsoft\Microsoft SQL Server\110\Tools\Setup", DisplayName = "SQL Server 2012", },
      new HiveItem { HiveName = @"SOFTWARE\Microsoft\Microsoft SQL Server\105\Tools\Setup", DisplayName = "SQL Server 2008 R2", },
      new HiveItem { HiveName = @"SOFTWARE\Microsoft\Microsoft SQL Server\100\Tools\Setup", DisplayName = "SQL Server 2008", },
      new HiveItem { HiveName = @"SOFTWARE\Microsoft\Microsoft SQL Server\90\Tools\Setup", DisplayName = "SQL Server 2005", },
    };

        public static RoutedUICommand InstallKey;
        public static InputGestureCollection CtrlE = new InputGestureCollection();

        static OwnKeyWindow()
        {
            CtrlE.Add(new KeyGesture(Key.E, ModifierKeys.Control));
            InstallKey = new RoutedUICommand("Check or Install Key", "Install", typeof(ScalableWindow), CtrlE);
        }

        public unsafe OwnKeyWindow(MainWindow mainWindow) : base(mainWindow)
        {
            InitializeComponent();
            TopElement.LayoutTransform = Scaler;

            List<KeyListItem> productKeyList = new List<KeyListItem>();

            using (RegistryKey sysKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Default))
            {
                DigitalProductId4 id4;
                DigitalProductId3 id3;

                using (RegistryKey registryKey = sysKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (registryKey != null)
                    {
                        GetProductIds(registryKey, out id3, out id4);

                        if (id3.size == sizeof(DigitalProductId3) || id4.size == sizeof(DigitalProductId4))
                        {
                            productKeyList.Add(new KeyListItem
                            {
                                HiveName = registryKey.Name,
                                Id3 = id3,
                                Id4 = id4,
                                InstallDate = DateTimeExtensions.Epoch.AddSeconds(unchecked((uint)(int)registryKey.GetValue("InstallDate", 0))).ToLocalTime(),
                                ProductName = $"{registryKey.GetValue("ProductName", "unknown OS")} ({registryKey.GetValue("CurrentBuild", "N/A")})",
                            });
                        }
                    }
                }

                foreach (HiveItem windowsItem in alternateWindowsList)
                {
                    using (RegistryKey registryKey = sysKey.OpenSubKey(windowsItem.HiveName))
                    {
                        if (registryKey != null)
                        {
                            GetProductIds(registryKey, out id3, out id4, isOffice: false);

                            if (id4.size == sizeof(DigitalProductId4))
                            {
                                productKeyList.Add(new KeyListItem
                                {
                                    HiveName = registryKey.Name,
                                    Id4 = id4,
                                    Id3 = id3,
                                    InstallDate = DateTimeExtensions.Epoch,
                                    ProductName = windowsItem.DisplayName,
                                });
                            }
                        }
                    }
                }

                using (RegistryKey regKey = sysKey.OpenSubKey(@"SYSTEM\Setup"))
                {
                    if (regKey != null)
                    {
                        foreach (string subKeyName in regKey.GetSubKeyNames().Where(n => n.StartsWith("Source OS")))
                        {
                            using (RegistryKey subKey = regKey.OpenSubKey(subKeyName))
                            {
                                GetProductIds(subKey, out id3, out id4, isOffice: false);
                                if (id4.size != sizeof(DigitalProductId4) && id3.size != sizeof(DigitalProductId3))
                                {
                                    continue;
                                }

                                if (subKey != null)
                                {
                                    productKeyList.Add(new KeyListItem
                                    {
                                        HiveName = System.IO.Path.Combine(@"HKEY_LOCAL_MACHINE\SYSTEM\Setup", subKeyName),
                                        Id3 = id3,
                                        Id4 = id4,
                                        InstallDate = DateTimeExtensions.Epoch.AddSeconds(unchecked((uint)(int)subKey.GetValue("InstallDate", 0))).ToLocalTime(),
                                        ProductName = $"{subKey.GetValue("ProductName", "unknown OS")} ({subKey.GetValue("CurrentBuild", "N/A")})",
                                    });
                                }
                            }
                        }
                    }
                }

                foreach (HiveItem officeItem in officeList)
                {
                    using (RegistryKey registryKey = sysKey.OpenSubKey(officeItem.HiveName))
                    {
                        if (registryKey == null)
                        {
                            continue;
                        }

                        foreach (string subKeyName in registryKey.GetSubKeyNames())
                        {
                            using (RegistryKey subKey = registryKey.OpenSubKey(subKeyName))
                            {
                                GetProductIds(subKey, out id3, out id4, isOffice: true);
                                if (id4.size != sizeof(DigitalProductId4))
                                {
                                    continue;
                                }

                                if (subKey != null)
                                {
                                    productKeyList.Add(new KeyListItem
                                    {
                                        HiveName = System.IO.Path.Combine(subKey.Name),
                                        Id4 = id4,
                                        InstallDate = DateTimeExtensions.Epoch,
                                        ProductName =
                                        $"{subKey.GetValue("ProductNameNonQualified", "")} ({officeItem.DisplayName})",
                                    });
                                }
                            }
                        }
                    }
                }

                foreach (HiveItem sqlServerItem in sqlServerList)
                {
                    using (RegistryKey registryKey = sysKey.OpenSubKey(sqlServerItem.HiveName))
                    {
                        object bytes = registryKey?.GetValue("DigitalProductId");
                        if (!(bytes is byte[]) || ((byte[])bytes).Length != 16)
                        {
                            continue;
                        }

                        id3 = default(DigitalProductId3);
                        id3.BinaryKey = new BinaryProductKey((byte[])bytes);

                        productKeyList.Add(new KeyListItem
                        {
                            HiveName = $"{System.IO.Path.Combine("HKEY_LOCAL_MACHINE", sqlServerItem.HiveName)}: DigitalProductId",
                            Id3 = id3,
                            InstallDate = DateTimeExtensions.Epoch,
                            ProductName = sqlServerItem.DisplayName,
                        });
                    }
                }
            }

            DataGridKeys.ItemsSource = productKeyList.OrderBy(p => p.InstallDate);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetSystemFirmwareTable(uint firmwareTableProviderSignature, uint firmwareTableID, IntPtr firmwareTableBuffer, uint bufferSize);

        private void InstallKey_CanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = IsKeyInCurrentCell();

        private bool IsKeyInCurrentCell()
        {
            DataGridCellInfo cell = DataGridKeys.SelectedCells.FirstOrDefault();

            object cellValue = cell.Item.GetType().GetProperties().Single(p => p.Name == cell.Column.SortMemberPath).GetValue(cell.Item);
            BinaryProductKey binaryKey = default(BinaryProductKey);
            if (cellValue is DigitalProductId3 || cellValue is DigitalProductId4)
            {
                binaryKey = ((dynamic)cellValue).BinaryKey;
            }

            return !binaryKey.IsNullKey;
        }

        private void InstallKey_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            DataGridCellInfo cell = DataGridKeys.SelectedCells.FirstOrDefault();
            object cellValue = cell.Item.GetType().GetProperties().Single(p => p.Name == cell.Column.SortMemberPath).GetValue(cell.Item);
            new ProductBrowser(MainWindow, cellValue.ToString()).Show();
        }

        private void DataGrid_Keys_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            InstallKey.Execute(null, this);
            e.Handled = true;
        }

        public static void GetProductIds(RegistryKey regKey, out DigitalProductId3 id3, out DigitalProductId4 id4, bool isOffice = false)
        {
            id3 = new DigitalProductId3();
            id4 = new DigitalProductId4();

            if (!isOffice)
            {
                try
                {
                    if (regKey?.GetValue("DigitalProductId") is byte[] productId3)
                    {
                        id3 = (DigitalProductId3)productId3;
                    }
                }
                catch { }
            }

            try
            {
                if (regKey?.GetValue(isOffice ? "DigitalProductID" : "DigitalProductId4") is byte[] productId4)
                {
                    id4 = (DigitalProductId4)productId4;
                }
            }
            catch { }
        }

        private void DataGrid_Keys_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e) => InstallButton.IsEnabled = IsKeyInCurrentCell();
        private void InstallButton_Click(object sender, RoutedEventArgs e) => InstallKey_Executed(sender, null);
        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private void DataGrid_Keys_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.MouseDoubleClick += DataGrid_Keys_MouseDoubleClick;
            e.Row.Background = App.DatagridBackgroundBrushes[e.Row.GetIndex() % App.DatagridBackgroundBrushes.Count];
        }
    }
}
