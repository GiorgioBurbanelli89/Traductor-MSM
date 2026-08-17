using System;
using System.Windows;
using System.Windows.Controls;

namespace Traductor
{
    public partial class PopupWindow : Window
    {
        public string SelectedText { get; set; } = string.Empty;
        public string? TargetLanguage { get; private set; }
        public bool TranslateRequested { get; private set; }

        public PopupWindow()
        {
            InitializeComponent();
        }

        public void SetText(string text)
        {
            SelectedText = text;
            // Mostrar preview truncado
            txtPreview.Text = text.Length > 50 ? text.Substring(0, 50) + "..." : text;
        }

        public void SetPosition(double x, double y)
        {
            Left = x;
            Top = y;
        }

        private void BtnTranslate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string lang)
            {
                TargetLanguage = lang;
                TranslateRequested = true;
                DialogResult = true;
                Close();
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Cerrar cuando pierde el foco
            Close();
        }
    }
}
