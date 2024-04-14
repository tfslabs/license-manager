using HGM.Hotbird64.LicenseManager.Extensions;
using HGM.Hotbird64.Vlmcs;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.LicenseManager
{
    public partial class ProductSelector
    {
        public SkuItem SelectedProduct => ((TreeViewItem)ProductTree.SelectedItem)?.Header as SkuItem;

        public ProductSelector()
        {
            InitializeComponent();
            TopElement.LayoutTransform = Scaler;

            foreach (AppItem product in KmsLists.AppItemList)
            {
                TreeViewItem appitem = new TreeViewItem { Header = product };
                ProductTree.Items.Add(appitem);

                foreach (KmsItem kmsProduct in KmsLists.KmsItemList.Where(k => k.App == product))
                {
                    TreeViewItem kmsItem = new TreeViewItem { Header = kmsProduct };

                    foreach (SkuItem skuProduct in KmsLists.SkuItemList.Where(p => p.KmsItem == kmsProduct).OrderBy(p => p.DisplayName))
                    {
                        TreeViewItem skuItem = new TreeViewItem { Header = skuProduct };
                        kmsItem.Items.Add(skuItem);
                    }

                    appitem.Items.Add(kmsItem);
                }
            }

            ProductTree.SelectedItemChanged += (sender, eventArgs) => ButtonOk.IsEnabled = ((TreeViewItem)eventArgs.NewValue).Header is SkuItem;
        }

        private void TreeViewItem_DoubleClick(object sender, MouseButtonEventArgs args)
        {
            if (((TreeViewItem)ProductTree.SelectedItem)?.Header is SkuItem) DialogResult = true;
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void TreeViewItem_Collapse(object sender, RoutedEventArgs e) => ((ItemsControl)e.Source).ExpandAll(false);
    }
}
