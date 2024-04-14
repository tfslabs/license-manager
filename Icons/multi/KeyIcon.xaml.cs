using System.Windows;

namespace HGM.Hotbird64.Icons
{
    /// <summary>
    /// Interaction logic for Key.xaml
    /// </summary>
    public partial class KeyIcon
    {
        public static DependencyProperty AngleProperty = DependencyProperty.Register(nameof(Angle), typeof(double), typeof(KeyIcon), new PropertyMetadata(0d, AngleChanged));

        public KeyIcon()
        {
            InitializeComponent();
        }

        public double Angle
        {
            get => (double)GetValue(AngleProperty);
            set => SetValue(AngleProperty, value);
        }

        public static void AngleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            KeyIcon key = (KeyIcon)sender;
            key.RotateTransform.Angle = (double)e.NewValue;
        }
    }
}
