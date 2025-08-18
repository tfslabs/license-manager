using System.Windows;
using System.Windows.Input;
using System.Windows.Media;


// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.LicenseManager
{
    public class ScalableWindow : Window
    {
        public ScaleTransform Scaler = new ScaleTransform(1, 1);
        public MainWindow MainWindow { get; protected set; }

        public ScalableWindow()
        {
            ContentRendered += window_Rendered;
        }

        public ScalableWindow(MainWindow mainWindow)
        {
            Icon = mainWindow.Icon;
            MainWindow = mainWindow;
            ContentRendered += window_Rendered;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            e.Handled = false;
            if (Keyboard.IsKeyUp(Key.LeftCtrl) && Keyboard.IsKeyUp(Key.RightCtrl)) return;

            e.Handled = true;

            switch (e.Key)
            {
                case Key.OemPlus:
                    ZoomIn(null, null);
                    break;
                case Key.OemMinus:
                    ZoomOut(null, null);
                    break;
                case Key.D0:
                    Zoom0(null, null);
                    break;
                default:
                    e.Handled = false;
                    break;
            }
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle || (Keyboard.IsKeyUp(Key.LeftCtrl) && Keyboard.IsKeyUp(Key.RightCtrl)))
            {
                base.OnPreviewMouseDown(e);
            }
            else
            {
                Zoom0(null, null);
            }
        }


        private void window_Rendered(object sender, object e)
        {
            if (MainWindow == null)
            {
                Scaler.ScaleX = Scaler.ScaleY = 1;
                return;
            }

            Width *= MainWindow.Scaler.ScaleX;
            Height *= MainWindow.Scaler.ScaleY;
            Scaler.ScaleX = Scaler.ScaleY = MainWindow.Scaler.ScaleX;
        }

        protected void ZoomIn(object sender, RoutedEventArgs e)
        {
            Scaler.ScaleX *= App.ZoomFactor;
            Scaler.ScaleY *= App.ZoomFactor;
            Height *= App.ZoomFactor;
            Width *= App.ZoomFactor;
        }

        protected void ZoomOut(object sender, RoutedEventArgs e)
        {
            Scaler.ScaleX /= App.ZoomFactor;
            Scaler.ScaleY /= App.ZoomFactor;
            Height /= App.ZoomFactor;
            Width /= App.ZoomFactor;
        }

        protected void Zoom0(object sender, RoutedEventArgs e)
        {
            Height /= Scaler.ScaleY;
            Width /= Scaler.ScaleX;
            Scaler.ScaleX = 1;
            Scaler.ScaleY = 1;
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyUp(Key.LeftCtrl) && Keyboard.IsKeyUp(Key.RightCtrl))
            {
                e.Handled = false;
                return;
            }

            e.Handled = true;

            double tempScale;

            if (e.Delta > 0)
            {
                tempScale = App.ZoomFactor;
            }
            else
            {
                tempScale = 1 / App.ZoomFactor;
            }

            Height *= tempScale;
            Width *= tempScale;
            Scaler.ScaleX *= tempScale;
            Scaler.ScaleY = Scaler.ScaleX;
        }
    }
}
