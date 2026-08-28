using System;
using System.Windows;
using System.Windows.Input;
using Traductor.Services;

namespace Traductor
{
    public partial class QuickPopup : Window
    {
        private readonly TranslationService _translationService;
        private string _translatedText = "";
        private string _replyTranslatedText = "";
        private string _detectedSourceLang = "";
        private string _targetLang = "es";
        private string _originalText = "";

        public QuickPopup()
        {
            InitializeComponent();
            _translationService = new TranslationService();
            Loaded += QuickPopup_Loaded;
            txtReply.TextChanged += TxtReply_TextChanged;
        }

        private void QuickPopup_Loaded(object sender, RoutedEventArgs e)
        {
            Activate();
            Focus();
        }

        public void SetPosition(double x, double y)
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            Left = Math.Min(x, screenWidth - 520);
            Top = Math.Min(y, screenHeight - 520);
        }

        public async void TranslateText(string text, string targetLang = "es")
        {
            _originalText = text;
            _targetLang = targetLang;
            txtOriginal.Text = text.Length > 150 ? text.Substring(0, 150) + "..." : text;

            // Actualizar botones de idioma para mostrar cual esta seleccionado
            UpdateLanguageButtons(targetLang);

            try
            {
                txtTranslation.Text = "Traduciendo...";
                txtLangInfo.Text = "";

                var result = await _translationService.TranslateAsync(text, "auto", targetLang);

                _translatedText = result.TranslatedText;
                _detectedSourceLang = result.DetectedLanguage ?? "auto";
                txtTranslation.Text = _translatedText;
                txtLangInfo.Text = $"{_detectedSourceLang} → {GetLangName(targetLang)}";

                // Actualizar placeholder para responder en el idioma detectado
                if (_detectedSourceLang.ToLower().Contains("ru") || _detectedSourceLang.ToLower().Contains("russian"))
                {
                    placeholderReply.Text = "Escribe tu respuesta (se traduce a ruso)...";
                }
                else if (_detectedSourceLang.ToLower().Contains("en") || _detectedSourceLang.ToLower().Contains("english"))
                {
                    placeholderReply.Text = "Escribe tu respuesta (se traduce a inglés)...";
                }
                else
                {
                    placeholderReply.Text = $"Escribe tu respuesta (se traduce a {_detectedSourceLang})...";
                }
            }
            catch (Exception ex)
            {
                txtTranslation.Text = $"Error: {ex.Message}";
            }
        }

        private void UpdateLanguageButtons(string selectedLang)
        {
            // Resetear todos los botones a color gris
            btnLangES.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(85, 85, 85));
            btnLangEN.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(85, 85, 85));
            btnLangRU.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(85, 85, 85));
            btnLangPT.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(85, 85, 85));
            btnLangDE.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(85, 85, 85));

            // Marcar el idioma seleccionado en azul
            var blueColor = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0, 120, 212));

            switch (selectedLang.ToLower())
            {
                case "es":
                    btnLangES.Background = blueColor;
                    break;
                case "en":
                    btnLangEN.Background = blueColor;
                    break;
                case "ru":
                    btnLangRU.Background = blueColor;
                    break;
                case "pt":
                    btnLangPT.Background = blueColor;
                    break;
                case "de":
                    btnLangDE.Background = blueColor;
                    break;
            }
        }

        private void BtnLang_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string langCode)
            {
                // Re-traducir al nuevo idioma seleccionado
                TranslateText(_originalText, langCode);
            }
        }

        private string GetLangName(string code)
        {
            return code switch
            {
                "es" => "ES",
                "en" => "EN",
                "ru" => "RU",
                "pt" => "PT",
                _ => code.ToUpper()
            };
        }

        private string GetLangCode(string detected)
        {
            string lower = detected.ToLower();
            if (lower.Contains("ru") || lower.Contains("russian")) return "ru";
            if (lower.Contains("en") || lower.Contains("english")) return "en";
            if (lower.Contains("pt") || lower.Contains("portuguese")) return "pt";
            if (lower.Contains("es") || lower.Contains("spanish")) return "es";
            return "en"; // default
        }

        private void TxtReply_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            placeholderReply.Visibility = string.IsNullOrEmpty(txtReply.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void TxtReply_GotFocus(object sender, RoutedEventArgs e)
        {
            placeholderReply.Visibility = Visibility.Collapsed;
        }

        private async void TxtReply_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Enter para traducir respuesta
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                await TranslateReply();
                e.Handled = true;
            }
        }

        private async void BtnTranslateReply_Click(object sender, RoutedEventArgs e)
        {
            await TranslateReply();
        }

        private async System.Threading.Tasks.Task TranslateReply()
        {
            string replyText = txtReply.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(replyText)) return;

            try
            {
                btnTranslateReply.Content = "...";
                btnTranslateReply.IsEnabled = false;

                // Traducir del español al idioma original detectado
                string replyTargetLang = GetLangCode(_detectedSourceLang);
                var result = await _translationService.TranslateAsync(replyText, "es", replyTargetLang);

                _replyTranslatedText = result.TranslatedText;

                // Mostrar resultado y copiar automaticamente
                txtReplyTranslated.Text = _replyTranslatedText;
                txtReplyTranslated.Visibility = Visibility.Visible;
                pnlButtons.Visibility = Visibility.Collapsed;

                // Copiar al portapapeles
                Clipboard.SetText(_replyTranslatedText);

                btnTranslateReply.Content = "✓";
                btnTranslateReply.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(16, 124, 16));

                // NO cerrar automaticamente - dejar que el usuario cierre cuando quiera
                // El usuario puede cerrar con Escape o haciendo click fuera
            }
            catch (Exception ex)
            {
                txtReplyTranslated.Text = $"Error: {ex.Message}";
                txtReplyTranslated.Visibility = Visibility.Visible;
                btnTranslateReply.Content = "!";
            }
            finally
            {
                btnTranslateReply.IsEnabled = true;
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            // Copiar la respuesta traducida si existe, sino la traduccion principal
            string textToCopy = !string.IsNullOrEmpty(_replyTranslatedText)
                ? _replyTranslatedText
                : _translatedText;

            if (!string.IsNullOrEmpty(textToCopy))
            {
                Clipboard.SetText(textToCopy);
                btnCopy.Content = "Copiado!";
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                // Doble click: copiar y cerrar
                if (e.ClickCount == 2)
                {
                    if (!string.IsNullOrEmpty(_translatedText))
                    {
                        Clipboard.SetText(_translatedText);
                    }
                    Close();
                }
                // Click simple: permitir arrastrar la ventana
                else if (e.ClickCount == 1)
                {
                    try
                    {
                        DragMove();
                    }
                    catch { }
                }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Solo Escape cierra (no Enter porque lo usamos para responder)
            if (e.Key == Key.Escape)
            {
                Close();
            }
            // Ctrl+C copia traduccion principal
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && !txtReply.IsFocused)
            {
                if (!string.IsNullOrEmpty(_translatedText))
                {
                    Clipboard.SetText(_translatedText);
                    btnCopy.Content = "Copiado!";
                }
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // NO cerrar al perder foco para poder escribir respuesta
            // Close();
        }
    }
}
