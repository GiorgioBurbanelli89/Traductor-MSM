using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Traductor
{
    public partial class FloatingButton : Window
    {
        public event Action? TranslateRequested;
        private DispatcherTimer? _hideTimer;

        public FloatingButton()
        {
            InitializeComponent();

            // Timer para auto-ocultar despues de 3 segundos
            _hideTimer = new DispatcherTimer();
            _hideTimer.Interval = TimeSpan.FromSeconds(3);
            _hideTimer.Tick += (s, e) => { Hide(); _hideTimer.Stop(); };
        }

        public void ShowAt(double x, double y)
        {
            // Ajustar para no salir de la pantalla
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            Left = Math.Min(x, screenWidth - 50);
            Top = Math.Min(y, screenHeight - 50);

            Show();
            _hideTimer?.Stop();
            _hideTimer?.Start();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _hideTimer?.Stop();
            Hide();
            TranslateRequested?.Invoke();
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            _hideTimer?.Stop();
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            _hideTimer?.Start();
        }
    }
}
