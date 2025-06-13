using System.Windows;
using System.Windows.Media;

namespace HGM.Hotbird64.LicenseManager.Controls
{
    public partial class RibbonMenuButton
    {
        public static DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(RibbonMenuButton), new PropertyMetadata(null, TextChanged));
        public static DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(ImageSource), typeof(RibbonMenuButton), new PropertyMetadata(null, ImageChanged));
        public static DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(object), typeof(RibbonMenuButton), new PropertyMetadata(null, IconChanged));

        public RibbonMenuButton()
        {
            InitializeComponent();
        }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public ImageSource Image
        {
            get { return (ImageSource)GetValue(ImageProperty); }
            set { SetValue(ImageProperty, value); }
        }

        public object Icon
        {
            get { return GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public static void TextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            RibbonMenuButton ribbonMenuButton = (RibbonMenuButton)sender;
            ribbonMenuButton.TextBlock.Text = ((string)e.NewValue).Replace("_", "");
        }

        public static void ImageChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            RibbonMenuButton ribbonMenuButton = (RibbonMenuButton)sender;
            ribbonMenuButton.ImageSource.Source = (ImageSource)e.NewValue;
        }

        public static void IconChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            RibbonMenuButton ribbonMenuButton = (RibbonMenuButton)sender;
            ribbonMenuButton.IconContent.Content = e.NewValue;
        }
    }
}
