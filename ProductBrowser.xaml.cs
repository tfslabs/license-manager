using HGM.Hotbird64.LicenseManager.Contracts;
using HGM.Hotbird64.LicenseManager.Extensions;
using HGM.Hotbird64.Vlmcs;
using LicenseManager.Annotations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Serialization;

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.LicenseManager
{
    public class PKeyConfigFile : IPKeyConfigFile
    {
        private string tempFileName;
        public string DisplayName { get; set; }
        public string BaseFileName;
        public bool IsExternal => ExternalFileName != null;
        public string ExternalFileName;
        public string ZippedFileName => BaseFileName + ".xrm-ms.gz";
        public Uri Uri => new Uri("pack://application:,,,/LicenseManager;component/Data/PKeyConfig/" + ZippedFileName);
        public bool IsOnFileSystem => tempFileName != null || IsUnzippedExternal;
        public bool IsOldKeyFormat;
        public bool IsUnzippedExternal => IsExternal && !ExternalFileName.ToUpperInvariant().EndsWith(".GZ");

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        public string TempFileName
        {
            get
            {
                if (tempFileName != null)
                {
                    return tempFileName;
                }
                if (IsUnzippedExternal)
                {
                    return ExternalFileName;
                }

                string tempName = Path.GetTempFileName();

                using (Stream compressedStream = Application.GetResourceStream(Uri).Stream)
                using (GZipStream stream = new GZipStream(compressedStream, CompressionMode.Decompress, false))
                using (FileStream file = new FileStream(tempName, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(file);
                }

                tempFileName = tempName;
                return tempFileName;
            }

            internal set => tempFileName = value;
        }

        public override string ToString() => DisplayName;
    }

    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    public partial class ProductBrowser : IHaveNotifyOfPropertyChange
    {
        public static ProductKeyConfiguration PKeyConfig { get; set; }
        public static ISet<ProductKeyConfigurationConfigurationsConfiguration> KeyConfigs => PKeyConfig.Items.OfType<ProductKeyConfigurationConfigurations>().Single().Configuration;
        public static ISet<ProductKeyConfigurationPublicKeysPublicKey> PublicKeys => PKeyConfig.Items.OfType<ProductKeyConfigurationPublicKeys>().Single().PublicKey;
        public static ISet<ProductKeyConfigurationKeyRangesKeyRange> KeyRanges => PKeyConfig.Items.OfType<ProductKeyConfigurationKeyRanges>().Single().KeyRange;
        private readonly Random random = new Random(unchecked((int)DateTime.Now.Ticks));
        private IEnumerable<ProductKeyConfigurationKeyRangesKeyRange> keyRanges;
        private ProductKeyConfigurationConfigurationsConfiguration keyConfig;
        private bool? isUsageAccepted = false;
        private SkuItem skuItem;
        public static RoutedUICommand InstallGeneratedKey;
        public static RoutedUICommand InstallGvlk;
        public static InputGestureCollection CtrlI = new InputGestureCollection();
        private readonly bool isManualEpid;
        private readonly bool isInputChanging;
        private readonly object lookupLockObject = new object();

        public static IList<PKeyConfigFile> PKeyConfigFiles = new List<PKeyConfigFile>
        {
            new PKeyConfigFile {BaseFileName="22621-pkeyconfig-csvlk", DisplayName="Windows 11 22621 / Server 2022 KMS Host" },
            new PKeyConfigFile {BaseFileName="22621-pkeyconfig", DisplayName="Windows 11 22621 / Server 2022" },
            new PKeyConfigFile {BaseFileName="pkeyconfig-office-kmshost", DisplayName="Office 2016/2019/2021 KMS Host" },
            new PKeyConfigFile {BaseFileName="pkeyconfig-office", DisplayName="Office 2016/2019/2021" },
            new PKeyConfigFile {BaseFileName="19041-pkeyconfig-csvlk", DisplayName="Windows 10 19041 / Server 2019 KMS Host" },
            new PKeyConfigFile {BaseFileName="19041-pkeyconfig", DisplayName="Windows 10 19041 / Server 2019" },
            new PKeyConfigFile {BaseFileName="18362-pkeyconfig", DisplayName="Windows 10 18362 / Server 2019" },
            new PKeyConfigFile {BaseFileName="16299-pkeyconfig-csvlk", DisplayName="Windows 10 16299 / Server 2016 KMS Host" },
            new PKeyConfigFile {BaseFileName="16299-pkeyconfig", DisplayName="Windows 10 16299 / Server 2016" },
            new PKeyConfigFile {BaseFileName="pkconfig_win10_anniversary", DisplayName="Windows 10 Pre-Release / Server Next" },
            new PKeyConfigFile {BaseFileName="pkconfig_win10_anniversary-csvlk", DisplayName="Windows 10 Pre-Release / Server Next KMS Host" },
            new PKeyConfigFile {BaseFileName="pkconfig_win10", DisplayName="Windows 10 10586 / Server 2012 R2" },
            new PKeyConfigFile {BaseFileName="pkconfig_win10-csvlk", DisplayName="Windows 10 10586 / Server 2012 R2 KMS Host" },
            new PKeyConfigFile {BaseFileName="pkconfig_win8.1Update", DisplayName="Windows 8.1" },
            new PKeyConfigFile {BaseFileName="pkconfig_win8.1-csvlk", DisplayName="Windows 8.1 / Server 2012 R2 KMS Host" },
            new PKeyConfigFile {BaseFileName="pkconfig_win8", DisplayName="Windows 8 / Server 2012" },
            new PKeyConfigFile {BaseFileName="pkconfig_win8-csvlk", DisplayName="Windows 8 / Server 2012 KMS Host" },
            new PKeyConfigFile {BaseFileName="pkconfig_winemb8", DisplayName="Windows 8 Embedded" },
            new PKeyConfigFile {BaseFileName="pkconfig_win7", DisplayName="Windows 7 / Server 2008 R2", IsOldKeyFormat=true },
            new PKeyConfigFile {BaseFileName="pkconfig_winThinPC", DisplayName="Windows 7 Thin PC", IsOldKeyFormat=true },
            new PKeyConfigFile {BaseFileName="pkconfig_winPosReady7", DisplayName="Windows 7 POS Ready", IsOldKeyFormat=true },
            new PKeyConfigFile {BaseFileName="pkconfig_vista", DisplayName="Windows Vista / Server 2008", IsOldKeyFormat=true },
            new PKeyConfigFile {BaseFileName="pkconfig_Office15KMSHost", DisplayName="Office 2013 KMS Host" },
            new PKeyConfigFile {BaseFileName="pkconfig_office15", DisplayName="Office 2013" },
            new PKeyConfigFile {BaseFileName="pkconfig_office15pre", DisplayName="Office 2013 Preview" },
            new PKeyConfigFile {BaseFileName="pkconfig_office14", DisplayName="Office 2010", IsOldKeyFormat=true },
            new PKeyConfigFile {BaseFileName="pkconfig-vs2022", DisplayName="Visual Studio 2022" },
            new PKeyConfigFile {BaseFileName="pkconfig-vs2019", DisplayName="Visual Studio 2019" },
            new PKeyConfigFile {BaseFileName="pkconfig-vs2017", DisplayName="Visual Studio 2017" },
            new PKeyConfigFile {BaseFileName="pkconfig-vs2015", DisplayName="Visual Studio 2015" },
            new PKeyConfigFile {BaseFileName="pkconfig-vs2013", DisplayName="Visual Studio 2013", IsOldKeyFormat=true },
            new PKeyConfigFile {BaseFileName="pkconfig-vs2012", DisplayName="Visual Studio 2012", IsOldKeyFormat=true },
            new PKeyConfigFile {BaseFileName="pkconfig-vs2010", DisplayName="Visual Studio 2010", IsOldKeyFormat=true },
            new PKeyConfigFile {BaseFileName="19041-pkeyconfig-downlevel", DisplayName="Windows 7 SP1 OEM/Retail", IsOldKeyFormat=true},
        };

        public event PropertyChangedEventHandler PropertyChanged;

        private string buildNumber = Environment.OSVersion.Version.Build.ToString(CultureInfo.InvariantCulture) + ".0000";
        public string BuildNumber
        {
            get => buildNumber;

            set => this.SetProperty(ref buildNumber, value, postAction: () =>
            {
                NotifyOfPropertyChange(nameof(IsValidBuildNumber));
                NotifyOfPropertyChange(nameof(BuildNumberInt));
                NotifyOfPropertyChange(nameof(FullEPid));
                NotifyOfPropertyChange(nameof(PlatformId));
            });
        }

        public string FullEPid
        {
            get
            {
                string fullEpid = null;

                Dispatcher.Invoke(() =>
                {
                    fullEpid = $"{PlatformId}-{TextBoxGroupId.Text}-{TextBoxKeyId1.Text}-{TextBoxKeyId2.Text}-" +
                               $"{TextBoxType.Text}-{TextBoxLcid.Text}-{BuildNumber}-{TextBoxDate.Text}";
                });

                return fullEpid;
            }
        }

        public string PlatformId => $"{KmsLists.GetPlatformId(BuildNumberInt):D5}";


        public bool IsValidBuildNumberFormat => BuildNumber != null && Regex.IsMatch(BuildNumber, @"^[1-9][0-9]{0,8}\.0000$");
        public bool? IsValidBuildNumber
        {
            get
            {
                if (IsValidBuildNumberFormat)
                {
                    if (BuildNumberInt < 3790 || !KmsLists.KmsData.WinBuilds.Select(b => b.BuildNumber).Contains(BuildNumberInt))
                    {
                        return null;
                    }
                }

                return IsValidBuildNumberFormat;
            }
        }

        public int BuildNumberInt => IsValidBuildNumberFormat ? int.Parse(BuildNumber.Split('.')[0], CultureInfo.InvariantCulture) : -1;

        public void ConstructorCommon()
        {
            InitializeComponent();
            TopElement.LayoutTransform = Scaler;
            DataGridRanges.MouseDoubleClick += NewCellSelected;
            MainWindow.BusyStatusChanged += OnMainWindowStatusChange;

            Closed += (s, e) =>
            {
                MainWindow.BusyStatusChanged -= OnMainWindowStatusChange;
            };
        }

        static ProductBrowser()
        {
            LoadPkeyConfigDatabase();
            CtrlI.Add(new KeyGesture(Key.I, ModifierKeys.Control));
            InstallGeneratedKey = new RoutedUICommand("Check or Install Key", nameof(InstallGeneratedKey), typeof(ScalableWindow), CtrlI);
            InstallGvlk = new RoutedUICommand("Check or Install Key", nameof(InstallGvlk), typeof(ScalableWindow), CtrlI);
        }

        public ProductBrowser(MainWindow parent, string searchString) : base(parent)
        {
            ConstructorCommon();
            SizeToContent = SizeToContent.Height;
            GroupBoxProductTree.Visibility = Visibility.Collapsed;
            EpidInput.Visibility = Visibility.Visible;
            GroupBoxKeyRanges.Header = "Key ID Ranges";

            try
            {
                isInputChanging = true;
                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    TextBoxEpidInput.Text = searchString;
                }
                else
                {
                    TextBoxEpidInput.Background = Brushes.Transparent;
                    ShowControls();
                }
            }
            finally
            {
                isInputChanging = false;
            }

            TopElement.ColumnDefinitions[0].Width = new GridLength(0);
            Width = 640;
            isManualEpid = true;
            Title = "License Manager Product Finder";
            textBox_KeyId_TextChanged(null, null);
        }

        public ProductBrowser(MainWindow parent) : base(parent)
        {
            ConstructorCommon();
            GroupBoxProductTree.Visibility = Visibility.Visible;
            EpidInput.Visibility = Visibility.Collapsed;

            TreeViewItem rootItem = new TreeViewItem { Header = "All SKUs by PkConfig file" };

            {
                IEnumerable<IGrouping<IPKeyConfigFile, ProductKeyConfigurationConfigurationsConfiguration>> treeGroupings = KeyConfigs.GroupBy(c => c.Source);

                foreach (IGrouping<IPKeyConfigFile, ProductKeyConfigurationConfigurationsConfiguration> treeGrouping in treeGroupings.OrderBy(t => t.Key.DisplayName))
                {
                    TreeViewItem pKeyConfigItem = new TreeViewItem { Header = treeGrouping.Key.DisplayName, };
                    IEnumerable<IGrouping<string, ProductKeyConfigurationConfigurationsConfiguration>> licenseGroupings = treeGrouping.GroupBy(t => t.ProductKeyType);

                    foreach (IGrouping<string, ProductKeyConfigurationConfigurationsConfiguration> licenseGrouping in licenseGroupings)
                    {
                        TreeViewItem licenseItem = new TreeViewItem { Header = licenseGrouping.Key, };

                        foreach (ProductKeyConfigurationConfigurationsConfiguration product in licenseGrouping)
                        {
                            TreeViewItem productItem = new TreeViewItem { Header = product, };
                            licenseItem.Items.Add(productItem);
                        }

                        pKeyConfigItem.Items.Add(licenseItem);
                    }

                    rootItem.Items.Add(pKeyConfigItem);
                }
            }

            ProductTree.Items.Add(rootItem);

            rootItem = new TreeViewItem { Header = "All SKUs by Group ID" };

            {
                IOrderedEnumerable<IGrouping<int, ProductKeyConfigurationConfigurationsConfiguration>> treeGroupings = KeyConfigs.GroupBy(c => c.RefGroupId).OrderBy(c => c.Key);

                foreach (IGrouping<int, ProductKeyConfigurationConfigurationsConfiguration> treeGrouping in treeGroupings)
                {
                    TreeViewItem pKeyConfigItem = new TreeViewItem { Header = $"{treeGrouping.Key:00000}", };

                    foreach (ProductKeyConfigurationConfigurationsConfiguration product in treeGrouping)
                    {
                        TreeViewItem productItem = new TreeViewItem { Header = product, };
                        pKeyConfigItem.Items.Add(productItem);
                    }

                    rootItem.Items.Add(pKeyConfigItem);
                }
            }

            ProductTree.Items.Add(rootItem);

            rootItem = new TreeViewItem { Header = "CSVLK SKUs only" };

            foreach (ProductKeyConfigurationConfigurationsConfiguration csvlkConfig in MainWindow.CsvlkConfigs.OrderBy(c => c.ToString()))
            {
                rootItem.Items.Add(new TreeViewItem { Header = csvlkConfig, });
            }

            ProductTree.Items.Add(rootItem);
            ProductTree.SelectedItemChanged += ProductTree_SelectedItemChanged;
        }

        [NotifyPropertyChangedInvocator]
        public void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void OnMainWindowStatusChange(object sender, BusyEventArgs e)
        {
            InstallButton.IsEnabled = !MainWindow.IsClosed && MainWindow.ControlsEnabled;
        }

        public static void LoadPkeyConfigDatabase()
        {
            if (PKeyConfig != null)
            {
                return;
            }

            List<string> fileNames = Directory.EnumerateFiles(App.ExeDirectoryName, "*.xrm-ms").ToList();
            fileNames.AddRange(Directory.EnumerateFiles(App.ExeDirectoryName, "*.xrm-ms.gz"));
            fileNames = new List<string>(fileNames.OrderByDescending(f => f));

            foreach (string fileName in fileNames)
            {
                PKeyConfigFiles.Insert(0, new PKeyConfigFile
                {
                    ExternalFileName = fileName,
                    DisplayName = GetNameWithoutExtension(fileName),
                    IsOldKeyFormat = Regex.IsMatch(fileName.ToUpperInvariant(), "^.*OLDFORMAT.*$")
                });

                string GetNameWithoutExtension(string name)
                {
                    while (true)
                    {
                        string upperName = name.ToUpperInvariant();

                        if (!upperName.EndsWith(".GZ") && !upperName.EndsWith(".XRM-MS"))
                        {
                            return name;
                        }

                        name = Path.GetFileNameWithoutExtension(name);
                    }
                }
            }

            foreach (PKeyConfigFile pKeyConfigFile in PKeyConfigFiles)
            {
                if (ReferenceEquals(pKeyConfigFile, PKeyConfigFiles.First()))
                {
                    PKeyConfig = ReadPkeyConfig(pKeyConfigFile);
                }
                else
                {
                    AddPkeyConfig(pKeyConfigFile);
                }
            }
        }

        private static void AddPkeyConfig(PKeyConfigFile pKeyConfigFile)
        {
            ProductKeyConfiguration pKeyConfig = ReadPkeyConfig(pKeyConfigFile);

            ISet<ProductKeyConfigurationConfigurationsConfiguration> keyConfigs = ((ProductKeyConfigurationConfigurations)pKeyConfig.Items.Single(i => i is ProductKeyConfigurationConfigurations)).Configuration;
            ISet<ProductKeyConfigurationPublicKeysPublicKey> publicKeys = ((ProductKeyConfigurationPublicKeys)pKeyConfig.Items.Single(i => i is ProductKeyConfigurationPublicKeys)).PublicKey;
            ISet<ProductKeyConfigurationKeyRangesKeyRange> keyRanges = ((ProductKeyConfigurationKeyRanges)pKeyConfig.Items.Single(i => i is ProductKeyConfigurationKeyRanges)).KeyRange;

            foreach (ProductKeyConfigurationConfigurationsConfiguration keyConfig in keyConfigs)
            {
                KeyConfigs.Add(keyConfig);
            }

            foreach (ProductKeyConfigurationPublicKeysPublicKey publicKey in publicKeys)
            {
                PublicKeys.Add(publicKey);
            }

            foreach (ProductKeyConfigurationKeyRangesKeyRange keyRange in keyRanges)
            {
                KeyRanges.Add(keyRange);
            }
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        private static ProductKeyConfiguration ReadPkeyConfig(PKeyConfigFile pKeyConfigFile)
        {
            using
            (
                Stream stream = !pKeyConfigFile.IsExternal
                    ? Application.GetResourceStream(pKeyConfigFile.Uri).Stream
                    : new FileStream(pKeyConfigFile.ExternalFileName, FileMode.Open, FileAccess.Read, FileShare.Read)
            )
            {
                ProductKeyConfiguration pKeyConfig;

                using
                (
                    Stream unzipStream = (!pKeyConfigFile.IsExternal || pKeyConfigFile.ExternalFileName.ToUpperInvariant().EndsWith(".GZ"))
                        ? new GZipStream(stream, CompressionMode.Decompress, false)
                        : null
                )
                {
                    XmlDocument xmlDocument = new XmlDocument();
                    xmlDocument.Load(unzipStream ?? stream);

                    try
                    {
                        byte[] data = Convert.FromBase64String(xmlDocument
                            .SelectSingleNode("/*[local-name()='licenseGroup']/*[local-name()='license']/*[local-name()='otherInfo']/*[local-name()='infoTables']/*[local-name()='infoList']/*[@name='pkeyConfigData']").InnerText);

                        using (MemoryStream memoryStream = new MemoryStream(data))
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(ProductKeyConfiguration));
                            pKeyConfig = (ProductKeyConfiguration)serializer.Deserialize(memoryStream);
                        }
                    }
                    catch (Exception e)
                    {
                        Dispatcher.CurrentDispatcher.InvokeAsync(() => MessageBox.Show
                        (
                            $"The file \"{(pKeyConfigFile.IsExternal ? pKeyConfigFile.ExternalFileName : pKeyConfigFile.ZippedFileName)}\" " +
                            $"could not be loaded into the pkeyconfig database\n\n{e.GetType().Name}: {e.Message}",
                            "PKeyConfig Database Load Error",
                            MessageBoxButton.OK, MessageBoxImage.Error
                        ));

                        throw;
                    }
                }

                Parallel.ForEach(pKeyConfig.Items.OfType<ProductKeyConfigurationConfigurations>().Single().Configuration, config =>
                {
                    config.Source = pKeyConfigFile;
                });

                Parallel.ForEach(pKeyConfig.Items.OfType<ProductKeyConfigurationKeyRanges>().Single().KeyRange, keyRange =>
                {
                    keyRange.FileName = pKeyConfigFile.DisplayName;
                });

                return pKeyConfig;
            }
        }

        private void ProductTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (!((e.NewValue as TreeViewItem)?.Header is ProductKeyConfigurationConfigurationsConfiguration keyConf))
            {
                return;
            }

            Task.Run(() => UpdatePkConfig(keyConf));
        }

        private void UpdatePkConfig(ProductKeyConfigurationConfigurationsConfiguration keyConf)
        {
            keyConfig = keyConf;

            Dispatcher.Invoke(() =>
            {
                GroupBoxGenerated.Visibility = Visibility.Collapsed;
                TextBoxPartNumber.Text = null;

                if (!isManualEpid && !isInputChanging)
                {
                    TextBoxKeyId1.Background = TextBoxKeyId2.Background = Brushes.Transparent;
                    TextBoxKeyId2.Text = TextBoxKeyId1.Text = null;
                }

                TextBoxEdition.Text = keyConfig.EditionId;
                TextBoxProductName.Text = keyConfig.ProductDescription;
                TextBoxSkuId.Text = keyConfig.ActConfigGuid.ToString();
                TextBoxLicense.Text = keyConfig.ProductKeyType;
                TextBoxSource.Text = keyConfig.Source.ToString();
            });

            lock (lookupLockObject)
            {
                keyRanges = KeyRanges.Where(r => r.RefActConfigGuid == keyConfig.ActConfigGuid).OrderBy(r => r.Start);
            }

            Dispatcher.Invoke(() =>
            {
                DataGridRanges.ItemsSource = keyRanges;
                DataGridRanges.Columns[0].SortDirection = ListSortDirection.Ascending;
                DataGridRanges.SelectedCells.Clear();
            });

            lock (lookupLockObject)
            {
                skuItem = KmsLists.SkuItemList.FirstOrDefault(s => s.Guid == keyConfig.ActConfigGuid && !s.IsGeneratedGvlk);
            }

            Dispatcher.Invoke(() =>
            {
                if (skuItem != null)
                {
                    TextBoxGvlk.Text = skuItem.Gvlk;

                    if (skuItem.KeyGroup != 0 && !isManualEpid && !isInputChanging)
                    {
                        TextBoxKeyId1.Text = $"{skuItem.KeyId / 1000000:000}";
                        TextBoxKeyId2.Text = $"{skuItem.KeyId % 1000000:000000}";
                    }
                    TextBoxGvlk.Visibility = Visibility.Visible;
                }
                else
                {
                    TextBoxGvlk.Visibility = Visibility.Collapsed;
                }
            });

            ProductKeyConfigurationPublicKeysPublicKey publicKey = PublicKeys.FirstOrDefault(p => keyConfig.RefGroupId == p.GroupId);
            if (publicKey == null)
            {
                return;
            }

            string lic = keyConfig.ProductKeyType.Split(':')[0].ToUpperInvariant();
            string platformId = lic == "RETAIL" ? "00" : lic == "VOLUME" ? "03" : lic == "OEM" ? "02" : "XX";

            Dispatcher.Invoke(() =>
            {
                TextBoxGroupId.Text = $"{publicKey.GroupId:00000}";
                TextBoxType.Text = platformId;
                TextBoxLcid.Text = $"{CultureInfo.InstalledUICulture.LCID}";
                TextBoxDate.Text = $"{DateTime.UtcNow.DayOfYear:000}{DateTime.UtcNow.Year:0000}";

                try
                {
                    CsvlkItem csvlkRule = KmsLists.CsvlkItemList[keyConfig.ActConfigGuid];
                    DataGridCsvlk.ItemsSource = csvlkRule.Activates.Select(r => KmsLists.KmsItemList[r.Guid]).Where(k => k != null).OrderBy(k => k.DisplayName);
                    GroupBoxCsvlk.Visibility = Visibility.Visible;
                }
                catch
                {
                    GroupBoxCsvlk.Visibility = Visibility.Collapsed;
                    DataGridCsvlk.ItemsSource = null;
                }
            });
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            try
            {
                if (TextBoxKeyId1.Visibility != Visibility.Visible)
                {
                    return;
                }

                if (Keyboard.IsKeyUp(Key.LeftCtrl) && Keyboard.IsKeyUp(Key.RightCtrl))
                {
                    return;
                }

                if (Keyboard.IsKeyUp(Key.LeftAlt) && Keyboard.IsKeyUp(Key.RightAlt))
                {
                    return;
                }

                if (e.Key == Key.G)
                {
                    isUsageAccepted = null;
                    textBox_KeyId_TextChanged(null, null);
                    e.Handled = true;
                }
            }
            finally
            {
                base.OnPreviewKeyDown(e);
            }
        }

        private void NewCellSelected(object sender, EventArgs e)
        {
            if (Regex.IsMatch(TextBoxEpidInput.Text, PidGen.EpidPattern) || Regex.IsMatch(TextBoxEpidInput.Text.ToUpperInvariant(), BinaryProductKey.KeyPattern))
            {
                return;
            }

            ProductKeyConfigurationKeyRangesKeyRange keyRange = (DataGridRanges?.SelectedCells.FirstOrDefault())?.Item as ProductKeyConfigurationKeyRangesKeyRange ??
                           (DataGridRanges != null && DataGridRanges.HasItems ? DataGridRanges.Items[0] as ProductKeyConfigurationKeyRangesKeyRange : null);

            if (keyRange == null)
            {
                MessageBox.Show
                (
                  "There are no valid Key Id ranges",
                  "Database Error",
                  MessageBoxButton.OK,
                  MessageBoxImage.Error
                );

                return;
            }

            int randomId = random.Next(keyRange.Start, keyRange.End + 1);
            TextBoxKeyId1.Text = $"{randomId / 1000000:000}";
            TextBoxKeyId2.Text = $"{randomId % 1000000:000000}";
            GroupBoxGenerated.Visibility = Visibility.Collapsed;
        }

        private void TreeViewItem_Collapse(object sender, RoutedEventArgs e) => ((ItemsControl)e.Source).ExpandAll(false);

        private void textBox_KeyId_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInputChanging)
            {
                return;
            }

            NotifyOfPropertyChange(nameof(FullEPid));
            TextBoxPartNumber.Text = null;
            ProductKeyConfigurationKeyRangesKeyRange validKeyRange = null;
            TextBoxKeyId2.Background = TextBoxKeyId1.Background = Brushes.Yellow;

            if (!Regex.IsMatch(TextBoxKeyId1.Text, "^[0-9]{1,3}$"))
            {
                TextBoxKeyId1.Background = Brushes.OrangeRed;
            }

            if (!Regex.IsMatch(TextBoxKeyId2.Text, "^[0-9]{1,6}$"))
            {
                TextBoxKeyId2.Background = Brushes.OrangeRed;
            }

            if (!Equals(TextBoxKeyId1.Background, Brushes.OrangeRed) && !Equals(TextBoxKeyId2.Background, Brushes.OrangeRed))
            {
                uint left = uint.Parse(TextBoxKeyId1.Text);
                uint right = uint.Parse(TextBoxKeyId2.Text);
                uint keyId = left * 1000000 + right;

                if (keyRanges != null)
                {
                    foreach (ProductKeyConfigurationKeyRangesKeyRange keyRange in keyRanges)
                    {
                        if (keyId >= keyRange.Start && keyId <= keyRange.End)
                        {
                            TextBoxKeyId2.Background = TextBoxKeyId1.Background = Brushes.LightGreen;
                            validKeyRange = keyRange;
                            break;
                        }
                    }
                }
            }

            GroupBoxGenerated.Visibility = Visibility.Collapsed;

            if (validKeyRange == null)
            {
                GenerateButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                TextBoxPartNumber.Text = validKeyRange.PartNumber;
                GenerateButton.Visibility = (!isUsageAccepted.HasValue || isUsageAccepted.Value) && keyConfig != null && !((PKeyConfigFile)keyConfig.Source).IsOldKeyFormat ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (skuItem?.Gvlk != null)
            {
                if (MessageBox.Show
                (
                  $"You can use Microsoft's genuine GVLK \"{skuItem.Gvlk}\". Are you sure, you want to generate non-genuine GVLKs?",
                  "Please Press No",
                  MessageBoxButton.YesNo,
                  MessageBoxImage.Warning) == MessageBoxResult.No
                )
                {
                    return;
                }
            }

            if (!isUsageAccepted.HasValue)
            {
                if (MessageBox.Show
                (
                  $"I hereby confirm that I have a legal license for \"{keyConfig.ProductDescription}\".",
                  "Confirmation Required",
                  MessageBoxButton.YesNo,
                  MessageBoxImage.Exclamation
                ) == MessageBoxResult.Yes)
                {
                    isUsageAccepted = true;
                }
                else
                {
                    isUsageAccepted = false;
                    GenerateButton.Visibility = Visibility.Collapsed;
                    return;
                }
            }

            string[] keys = new string[10];

            for (int i = 0; i < keys.Length; i++)
            {
                uint left = uint.Parse(TextBoxKeyId1.Text);
                uint right = uint.Parse(TextBoxKeyId2.Text);
                uint keyId = left * 1000000 + right;

                ulong randomSecret = (ulong)unchecked((uint)random.Next(int.MinValue, int.MaxValue));
                randomSecret |= (ulong)random.Next(0x200000) << 32;

                BinaryProductKey binaryKey = new BinaryProductKey((uint)keyConfig.RefGroupId, keyId, randomSecret);
                keys[i] = (string)binaryKey;
            }

            DataGridKeys.ItemsSource = keys;
            GroupBoxGenerated.Visibility = Visibility.Visible;
        }

        private void InstallGenerated_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            object item = DataGridKeys.SelectedCells.FirstOrDefault().Item;
            if (item == null)
            {
                return;
            }

            e.CanExecute = true;
        }

        private void InstallGenerated_Executed(object sender, ExecutedRoutedEventArgs e)
          => new ProductBrowser(MainWindow, DataGridKeys.SelectedCells.FirstOrDefault().Item.ToString()).Show();

        private void GvlkInstall_CanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;

        private void GvlkInstall_Executed(object sender, ExecutedRoutedEventArgs e)
          => new ProductBrowser(MainWindow, TextBoxGvlk.Text).Show();

        private void textBox_Gvlk_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                InstallGvlk.Execute(null, this);
                e.Handled = true;
            }
        }

        private void DataGrid_Keys_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                InstallGeneratedKey.Execute(null, this);
                e.Handled = true;
            }
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Background = App.DatagridBackgroundBrushes[e.Row.GetIndex() % App.DatagridBackgroundBrushes.Count];
        }

        private void ShowControls()
        {
            if (Equals(TextBoxEpidInput.Background, Brushes.LightGreen))
            {
                GroupBoxProductDetail.Visibility =
                TextBoxSource.Visibility =
                GridEpid.Visibility =
                GroupBoxKeyRanges.Visibility =
                    Visibility.Visible;
            }
            else if (Equals(TextBoxEpidInput.Background, Brushes.Yellow))
            {
                TextBoxSource.Visibility =
                GroupBoxGenerated.Visibility =
                GroupBoxCsvlk.Visibility =
                GroupBoxKeyRanges.Visibility =
                  Visibility.Collapsed;

                GroupBoxProductDetail.Visibility =
                GridEpid.Visibility =
                  !Regex.IsMatch(TextBoxEpidInput.Text, App.GuidPattern) ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (Equals(TextBoxEpidInput.Background, Brushes.OrangeRed) || Equals(TextBoxEpidInput.Background, Brushes.Transparent))
            {
                GroupBoxProductDetail.Visibility =
                GridEpid.Visibility =
                GroupBoxGenerated.Visibility =
                GroupBoxCsvlk.Visibility =
                GroupBoxKeyRanges.Visibility =
                  Visibility.Collapsed;
            }
        }

        private void PassWordBoxEpidInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // ReSharper disable once PossibleInvalidOperationException
            if (!CheckBoxHideInput.IsChecked.Value)
            {
                return;
            }

            TextBoxEpidInput.Text = PassWordBoxEpidInput.Password;
        }

        private async void textBox_EpidInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // ReSharper disable once PossibleInvalidOperationException
            if (!CheckBoxHideInput.IsChecked.Value)
            {
                PassWordBoxEpidInput.Password = TextBoxEpidInput.Text;
            }

            SetReady();
            TextBoxGvlk.Visibility = Visibility.Collapsed;
            TextBoxKeyId1.Text = TextBoxKeyId2.Text = null;
            TextBox textBox = (TextBox)sender;
            ProductKeyConfigurationConfigurationsConfiguration localKeyConfig = null;
            InstallButton.Visibility = Visibility.Collapsed;

            if (Regex.IsMatch(textBox.Text, PidGen.EpidPattern))
            {
                StatusPanel.Visibility = Visibility.Collapsed;
                InstallButton.Visibility = CheckButton.Visibility = Visibility.Collapsed;
                TextBoxKeyId1.IsReadOnly = TextBoxKeyId2.IsReadOnly = true;
                EPid epid = new EPid(textBox.Text);

                try
                {
                    IEnumerable<ProductKeyConfigurationKeyRangesKeyRange> localKeyRanges;

                    await Task.Run(() =>
                    {
                        lock (lookupLockObject)
                        {
                            localKeyRanges = KeyRanges.Where(r => r.Start <= (int)epid.KeyId && (int)epid.KeyId <= r.End);
                            localKeyConfig = KeyConfigs.FirstOrDefault(s => s.RefGroupId == (int)epid.GroupId && localKeyRanges.Select(r => r.RefActConfigGuid).Contains(s.ActConfigGuid));
                        }
                    });

                    if (localKeyConfig == null)
                    {
                        textBox.Background = Brushes.Yellow;
                        TextBlockInputErrors.Text = "The EPID does not correspond to a known Product";
                        TextBlockInputErrors.Visibility = Visibility.Visible;
                        return;
                    }

                    textBox.Background = Brushes.LightGreen;
                    TextBlockInputErrors.Visibility = Visibility.Collapsed;
                    await Task.Run(() => UpdatePkConfig(localKeyConfig));
                }
                finally
                {
                    TextBoxKeyId1.Text = $"{epid.KeyId / 1000000:000}";
                    TextBoxKeyId2.Text = $"{epid.KeyId % 1000000:000000}";
                    ShowControls();
                }
            }
            else if
            (
              (textBox.Text.Length == 38 || textBox.Text.Length == 36) &&
              (Regex.IsMatch(textBox.Text, App.GuidPattern) ||
              (textBox.Text[0] == '{' && textBox.Text.Last() == '}' && Regex.IsMatch(textBox.Text.Substring(1, textBox.Text.Length - 2), App.GuidPattern))
            ))
            {
                StatusPanel.Visibility = Visibility.Collapsed;
                CheckButton.Visibility = InstallButton.Visibility = Visibility.Collapsed;
                TextBoxKeyId1.IsReadOnly = TextBoxKeyId2.IsReadOnly = false;
                KmsGuid guid = new KmsGuid(textBox.Text);

                await Task.Run(() =>
                {
                    lock (lookupLockObject)
                    {
                        localKeyConfig = KeyConfigs.FirstOrDefault(s => s.ActConfigGuid == guid);
                    }
                });

                if (localKeyConfig == null)
                {
                    textBox.Background = Brushes.Yellow;
                    TextBlockInputErrors.Visibility = Visibility.Visible;

                    KmsItem kmsItem = KmsLists.KmsItemList.FirstOrDefault(k => k.Guid == guid);
                    AppItem appItem = KmsLists.AppItemList.FirstOrDefault(a => a.Guid == guid);

                    if (kmsItem != null)
                    {
                        TextBlockInputErrors.Text = $"This is not an SKU GUID but the KMS GUID for {kmsItem}";
                    }
                    else if (appItem != null)
                    {
                        TextBlockInputErrors.Text = $"This is not an SKU GUID but the Application GUID for {appItem}";
                    }
                    else
                    {
                        TextBlockInputErrors.Text = "The SKU GUID is unknown.";
                    }

                    ShowControls();
                }
                else
                {
                    textBox.Background = Brushes.LightGreen;
                    TextBlockInputErrors.Visibility = Visibility.Collapsed;
                    await Task.Run(() => UpdatePkConfig(localKeyConfig));
                }

                ShowControls();
            }
            else if (textBox.Text.Length == 29 && Regex.IsMatch(textBox.Text.ToUpperInvariant(), BinaryProductKey.KeyPattern))
            {
                InstallButton.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
                CheckButton.Visibility = Visibility.Visible;

                BinaryProductKey key;

                try
                {
                    key = (BinaryProductKey)textBox.Text;
                }
                catch
                {
                    textBox.Background = Brushes.OrangeRed;
                    TextBlockInputErrors.Text = "The CRC-32 hash of the Key is invalid.";
                    TextBlockInputErrors.Visibility = Visibility.Visible;
                    ShowControls();
                    return;
                }

                TextBoxKeyId1.IsReadOnly = TextBoxKeyId2.IsReadOnly = true;

                if (!key.IsNewKey)
                {
                    CheckButton.Visibility = Visibility.Visible;
                    textBox.Background = Brushes.Yellow;
                    ShowControls();
                    GroupBoxProductDetail.Visibility = GridEpid.Visibility = Visibility.Collapsed;
                    TextBlockInputErrors.Text = "This Key may take long to check. Press the check button to start.";
                    TextBlockInputErrors.Visibility = Visibility.Visible;
                    return;
                }

                CheckButton.Visibility = Visibility.Collapsed;

                try
                {
                    await Task.Run(() =>
                    {
                        lock (lookupLockObject)
                        {
                            keyRanges = KeyRanges.Where(r => r.Start <= (int)key.Id && (int)key.Id <= r.End);
                            localKeyConfig = KeyConfigs.FirstOrDefault(s => s.RefGroupId == (int)key.Group && keyRanges.Select(r => r.RefActConfigGuid).Contains(s.ActConfigGuid));
                        }
                    });

                    if (localKeyConfig == null)
                    {
                        textBox.Background = Brushes.Yellow;
                        TextBlockInputErrors.Text = "The SKU ID for this Key is not in the database.";
                        TextBlockInputErrors.Visibility = Visibility.Visible;
                        return;
                    }

                    textBox.Background = Brushes.LightGreen;
                    TextBlockInputErrors.Visibility = Visibility.Collapsed;
                    await Task.Run(() => UpdatePkConfig(localKeyConfig));
                }
                finally
                {
                    TextBoxKeyId1.Text = $"{key.Id / 1000000:000}";
                    TextBoxKeyId2.Text = $"{key.Id % 1000000:000000}";
                    ShowControls();
                }
            }
            else
            {
                StatusPanel.Visibility = Visibility.Collapsed;
                InstallButton.Visibility = CheckButton.Visibility = Visibility.Collapsed;
                textBox.Background = Brushes.OrangeRed;
                TextBlockInputErrors.Text = "This is neither a valid EPID, Product Key or GUID.";
                TextBlockInputErrors.Visibility = Visibility.Visible;
                ShowControls();
                return;
            }
        }

        private async void Button_Check_Click(object sender, RoutedEventArgs e)
        {
            TextBlockInputErrors.Text = null;
            BinaryProductKey key = (BinaryProductKey)TextBoxEpidInput.Text;
            ProductKeyConfigurationConfigurationsConfiguration localKeyConfig;

            //DigitalProductId2 id2;
            //DigitalProductId3 id3;
            DigitalProductId4 id4 = default(DigitalProductId4);

            PKeyConfigFile[] oldPKeyConfigFiles = PKeyConfigFiles.Where(f => f.IsOldKeyFormat).ToArray();
            SetBusy("Querying PidGenX.dll");

            try
            {
                CheckButton.Visibility = Visibility.Collapsed;

                foreach (PKeyConfigFile pkeyConfigFile in oldPKeyConfigFiles)
                {
                    try
                    {
                        await Task.Run(() => PidGen.CheckKey(key.ToString(), pkeyConfigFile.TempFileName, out _, out _, out id4));
                        break;
                    }
                    catch
                    {
                        if (!ReferenceEquals(pkeyConfigFile, oldPKeyConfigFiles.Last()))
                        {
                            continue;
                        }

                        TextBoxEpidInput.Background = Brushes.OrangeRed;
                        TextBlockInputErrors.Text = "The Key is invalid";
                        return;
                    }
                }

                string[] split = id4.EPid.Split('-');
                uint keyId = uint.Parse(split[2] + split[3]);
                TextBlockInputErrors.Visibility = Visibility.Collapsed;
                TextBoxEpidInput.Background = Brushes.LightGreen;

                await Task.Run(() =>
                {
                    lock (lookupLockObject)
                    {
                        localKeyConfig = KeyConfigs.First(c => c.ActConfigGuid == id4.SkuId);
                    }

                    UpdatePkConfig(localKeyConfig);
                });

                TextBoxKeyId1.Text = $"{keyId / 1000000:000}";
                TextBoxKeyId2.Text = $"{keyId % 1000000:000000}";
            }
            finally
            {
                StatusPanel.Visibility = Visibility.Collapsed;
                ShowControls();
                SetReady();
            }
        }
        private void SetReady()
        {
            Status.Text = "Ready";
            MainGrid.IsEnabled = true;
            ProgressBar.IsIndeterminate = false;
        }

        private void SetBusy(string text)
        {
            Status.Text = text;
            MainGrid.IsEnabled = false;
            ProgressBar.IsIndeterminate = true;
        }

        private void Button_Install_Click(object sender, RoutedEventArgs e)
        {
            if (!MainWindow.ControlsEnabled)
            {
                MessageBox.Show
                (
                  "The main window is currently busy. Try again in a few seconds.",
                  "Main Window Busy",
                  MessageBoxButton.OK, MessageBoxImage.Warning
                );

                return;
            }

            MainWindow.InstallProductKey(TextBoxEpidInput.Text);
        }

        private void CheckBoxHideInput_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxHideInput.IsChecked.Value)
            {
                PassWordBoxEpidInput.Visibility = Visibility.Visible;
                TextBoxEpidInput.Visibility = Visibility.Collapsed;
                TextColumn.Width = new GridLength(0);
                PasswordColumn.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                PassWordBoxEpidInput.Visibility = Visibility.Collapsed;
                TextBoxEpidInput.Visibility = Visibility.Visible;
                PasswordColumn.Width = new GridLength(0);
                TextColumn.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        private async void CheckOnlineButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                int activationsRemaining = 0;
                IsEnabled = false;

                try
                {
                    await Task.Run(() =>
                    {
                        activationsRemaining = PidGen.GetRemainingActivationsOnline(FullEPid);
                    });
                }
                finally
                {
                    IsEnabled = true;
                }

                MessageBox.Show
                (
                    this, $"EPID \"{FullEPid}\" has {activationsRemaining} activation{(activationsRemaining == 1 ? "" : "s")} remaining.",
                    "Info from activation.sls.microsoft.com", MessageBoxButton.OK, MessageBoxImage.Information
                );
            }
            catch (EPidQueryException ex)
            {
                MessageBox.Show
                (
                    this, $"EPID \"{ex.EPid}\" check returned: \"{ex.Message}\"",
                    $"EPID Online Check Error {ex.ErrorCode}", MessageBoxButton.OK, MessageBoxImage.Error
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show
                (
                    this, ex.Message, "EPID Online Check Error", MessageBoxButton.OK, MessageBoxImage.Error
                );
            }
        }

        private void TextBoxEPid_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (CheckOnlineButton != null)
            {
                CheckOnlineButton.Visibility = Regex.IsMatch(((TextBox)sender).Text, PidGen.EpidPattern) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
