// ReSharper disable RedundantUsingDirective
using HGM.Hotbird64.LicenseManager.Extensions;
using HGM.Hotbird64.Vlmcs;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.LicenseManager
{
    public partial class ProductKeys
    {
        public static IList<ProductKey> ProductKeyList => productKeyList;
        private static readonly ProductKey[] productKeyList =
        {
            new ProductKey ("Windows 10/11 Professional", "VK7JG-NPHTM-C97JM-9MPGT-3V66T", KeyType.StoreLicense ),
            new ProductKey ("Windows 10/11 Home", "YTMG3-N6DKC-DKB77-7M9GH-8HVX7", KeyType.StoreLicense ),
            new ProductKey ("Windows 8 Preview Standard Server", "YNBF9-GPVTG-FFHQC-MJR4B-B4CQX", KeyType.StoreLicense),
            new ProductKey ("Microsoft Visual Studio 2019 Professional", "NYWVH-HT4XC-R2WYW-9Y3CM-X4V3Y", KeyType.StoreLicense ),
            new ProductKey ("Microsoft Visual Studio 2019 Enterprise", "BF8Y8-GN2QH-T84XB-QVY3B-RC4DF", KeyType.StoreLicense ),
            new ProductKey ("Microsoft Visual Studio 2022 Professional", "TD244-P4NB7-YQ6XK-Y8MMM-YWV2J", KeyType.StoreLicense ),
            new ProductKey ("Microsoft Visual Studio 2022 Enterprise", "VHF9H-NXBBB-638P6-6JHCY-88JWH", KeyType.StoreLicense ),
            new ProductKey ("Microsoft SQL Server 2022 Enterprise", "J4V48-P8MM4-9N3J9-HD97X-DYMRM", KeyType.StoreLicense ),
            new ProductKey ("Microsoft SCCM 2022 Enterprise", "BXH69-M62YX-QQD6R-3GPWX-8WMFY", KeyType.StoreLicense ),
            new ProductKey ("Windows Server 2022 KMS Host", "R47NX-MVMYR-98PV9-XYVXY-XBXCH", KeyType.StoreLicense)
        };

        private void AddKeysToTreeViewItem(TreeViewItem treeViewItem, IEnumerable<ProductKey> keys)
        {
            foreach (ProductKey key in keys)
            {
                TreeViewItem keyItem = new TreeViewItem { Header = key, ToolTip = key.Key };
                treeViewItem.Items.Add(keyItem);
            }
        }

        private static readonly KmsGuid winBetaGuid = new KmsGuid("5f94a0bb-d5a0-4081-a685-5819418b2fe0");

        public ProductKeys(MainWindow mainWindow) : base(mainWindow)
        {
            InitializeComponent();
            TopElement.LayoutTransform = Scaler;
            DataContext = this;

            Loaded += (s, e) => Icon = this.GenerateImage(new Icons.InstallKey(), 16, 16);

            TreeViewItem treeViewItem = new TreeViewItem { Header = "Store license keys" };
            ProductTree.Items.Add(treeViewItem);
            AddKeysToTreeViewItem(treeViewItem, ProductKeyList.Where(k => k.KeyType == KeyType.StoreLicense));

            treeViewItem = new TreeViewItem { Header = "User-generated GVLKs" };
            ProductTree.Items.Add(treeViewItem);
            AddKeysToTreeViewItem(treeViewItem, KmsLists.SkuItemList.Where(s => s.IsGeneratedGvlk && s.Gvlk != null).Select(s => new ProductKey(s.ToString(), s.Gvlk, s.IsGeneratedGvlk ? KeyType.GvlkGenerated : KeyType.Gvlk)));

            treeViewItem = new TreeViewItem { Header = "Genuine GVLKs" };
            ProductTree.Items.Add(treeViewItem);

            foreach (AppItem app in KmsLists.AppItemList)
            {
                TreeViewItem appitem = new TreeViewItem { Header = app };
                treeViewItem.Items.Add(appitem);

                foreach (KmsItem kmsId in app.KmsItems.OrderBy(k => k.DisplayName))
                {
                    TreeViewItem kmsItem = new TreeViewItem { Header = kmsId };
                    AddKeysToTreeViewItem(kmsItem, kmsId.SkuItems.Where(s => !s.IsGeneratedGvlk && s.Gvlk != null).OrderBy(s => s.DisplayName).Select(s => new ProductKey(s.ToString(), s.Gvlk, s.IsGeneratedGvlk ? KeyType.GvlkGenerated : KeyType.Gvlk)));

                    if (kmsId == winBetaGuid)
                    {
                        AddKeysToTreeViewItem(kmsItem, ProductKeyList.Where(k => k.KeyType == KeyType.Gvlk).OrderBy(k => k.Name));
                    }

                    appitem.Items.Add(kmsItem);
                }
            }

            ProductTree.SelectedItemChanged += (sender, eventArgs) =>
            {
                InstallButton.IsEnabled = ((TreeViewItem)eventArgs.NewValue).ToolTip is string;

                ProductKey key = ((TreeViewItem)eventArgs.NewValue).Header as ProductKey;

                if (key != null)
                {
                    TextBlockGenerated.Visibility = key.KeyType == KeyType.GvlkGenerated || key.KeyType == KeyType.RetailGenerated ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    TextBlockGenerated.Visibility = Visibility.Collapsed;
                }
            };
        }

        private void TreeViewItem_DoubleClick(object sender, MouseButtonEventArgs args)
        {
            TreeViewItem treeViewItem = ProductTree.SelectedItem as TreeViewItem;
            if (treeViewItem == null || !treeViewItem.HasHeader || !(treeViewItem.ToolTip is string)) return;
            AnalyzeButton_Click(null, null);
        }

        private void TreeViewItem_Collapse(object sender, RoutedEventArgs e) => ((ItemsControl)e.Source).ExpandAll(false);

        private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            new ProductBrowser(MainWindow, GvlkKey.Text).Show();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void CancelButton_Clicked(object sender, RoutedEventArgs e) => Close();
    }
}
