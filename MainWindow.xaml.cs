using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Traductor.Helpers;
using Traductor.Services;

namespace Traductor
{
    public partial class MainWindow : Window
    {
        private readonly TranslationService _translationService;
        private string _ctl;
        private readonly System.Collections.Generic.HashSet<string> _ctlSeen = new();
        private IntPtr _windowHandle;
        private HwndSource? _source;

        // Auto-translate debounce
        private CancellationTokenSource? _autoTranslateCts;
        private const int AUTO_TRANSLATE_DELAY_MS = 500;

        // Clipboard/Selection monitoring
        private string _lastClipboardText = "";
        private string _lastSelectedText = "";
        private bool _isMonitoringClipboard = false;
        private bool _ingesting = false;   // true mientras IngestSelection pone el texto (evita doble traducción/voz)
        private string _lastSpoken = "";   // último texto pronunciado (para verificar por --ctl)
        private DispatcherTimer? _clipboardTimer;

        // Floating button for text selection
        private FloatingButton? _floatingButton;
        private MouseHook? _mouseHook;
        private QuickPopup? _currentPopup;

        // Hotkey registration
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_T = 0x54;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public MainWindow()
        {
            InitializeComponent();
            _translationService = new TranslationService();
            InitializeLanguages();
            InitializeClipboardMonitor();
            InitializeFloatingButton();
            _uiReady = true;
            ApplyUiLanguage();   // arranca en espanol (Loc.Ui = "es")
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;

            // Canal de control --ctl (tests desde terminal, como Hekatan LISP/Fortran)
            _ctl = CtlValueAfter(Environment.GetCommandLineArgs(), "--ctl");
            if (_ctl != null) Loaded += (_, _) => StartCtl();
        }

        // ---------- --ctl: la terminal maneja la ventana viva ----------
        private void StartCtl()
        {
            System.IO.Directory.CreateDirectory(_ctl);
            var t = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(150) };
            t.Tick += async (_, _) => await PollCtl();
            t.Start();
        }

        private async System.Threading.Tasks.Task PollCtl()
        {
            foreach (var f in System.IO.Directory.GetFiles(_ctl, "cmd-*.json"))
            {
                if (!_ctlSeen.Add(f)) continue;
                string resp;
                try { resp = await HandleCtl(System.IO.File.ReadAllText(f)); }
                catch (Exception ex) { resp = "{\"error\":\"" + ex.Message.Replace("\"", "'") + "\"}"; }
                System.IO.File.WriteAllText(f.Replace("cmd-", "resp-"), resp);
            }
        }

        private async System.Threading.Tasks.Task<string> HandleCtl(string json)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var op = doc.RootElement.GetProperty("op").GetString();
            switch (op)
            {
                case "settext":
                    txtSource.Text = doc.RootElement.GetProperty("text").GetString();
                    return "{\"ok\":true}";
                case "settarget":
                    CtlSelectLang(cmbTargetLang, doc.RootElement.GetProperty("code").GetString());
                    return "{\"ok\":true}";
                case "translate":
                    await TranslateTextAsync();
                    return System.Text.Json.JsonSerializer.Serialize(new { result = txtResult.Text });
                case "getresult":
                    return System.Text.Json.JsonSerializer.Serialize(new { result = txtResult.Text });
                case "getvoice":
                    return System.Text.Json.JsonSerializer.Serialize(new
                    {
                        index = cmbVoice.SelectedIndex,
                        name = (cmbVoice.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString()
                    });
                case "click":   // pulsa cualquier boton/checkbox por su x:Name
                {
                    var name = doc.RootElement.GetProperty("name").GetString();
                    var el = FindName(name);
                    if (el is System.Windows.Controls.Primitives.ToggleButton tb)
                        tb.IsChecked = !(tb.IsChecked ?? false);   // dispara Checked/Unchecked
                    else if (el is System.Windows.Controls.Primitives.ButtonBase b)
                        b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    else
                        return "{\"error\":\"no es boton: " + name + "\"}";
                    return "{\"ok\":true}";
                }
                case "getstate":
                    return System.Text.Json.JsonSerializer.Serialize(new
                    {
                        source = txtSource.Text,
                        result = txtResult.Text,
                        status = txtStatus.Text,
                        autoTranslate = chkAutoTranslate.IsChecked,
                        autoVoice = chkAutoVoice.IsChecked,
                        monitor = btnMonitor.IsChecked,
                        voice = cmbVoice.SelectedIndex,
                        srcLang = (cmbSourceLang.SelectedItem as LanguageItem)?.Code,
                        tgtLang = (cmbTargetLang.SelectedItem as LanguageItem)?.Code
                    });
                case "tts":   // genera el audio con la voz elegida y confirma que salió MP3
                {
                    var lang = (cmbTargetLang.SelectedItem as LanguageItem)?.Code ?? "en";
                    var gender = cmbVoice?.SelectedIndex == 1 ? "hombre" : "mujer";
                    var files = await Services.TtsService.SynthesizeAsync(txtResult.Text, lang, gender);
                    return System.Text.Json.JsonSerializer.Serialize(new
                    { ok = files.Count > 0, files = files.Count, voice = Services.TtsService.PickVoice(lang, gender) });
                }
                case "selection":   // simula texto seleccionado en otra app (LinkedIn/Telegram)
                {
                    _isMonitoringClipboard = true;   // como si el Monitor estuviera ON
                    _lastSpoken = "";
                    IngestSelection(doc.RootElement.GetProperty("text").GetString());
                    await Task.Delay(1500);          // deja que traduzca
                    return System.Text.Json.JsonSerializer.Serialize(new
                    { source = txtSource.Text, result = txtResult.Text });
                }
                case "spoken":   // último texto pronunciado (para verificar el audio de la selección)
                    return System.Text.Json.JsonSerializer.Serialize(new { spoken = _lastSpoken });
                case "uilang":   // cambia el idioma de la INTERFAZ (es/en)
                    cmbUiLang.SelectedIndex = doc.RootElement.GetProperty("code").GetString() == "en" ? 1 : 0;
                    return "{\"ok\":true}";
                case "getui":    // lee las etiquetas visibles para verificar el bilingue
                    return System.Text.Json.JsonSerializer.Serialize(new
                    {
                        ui = Loc.Ui,
                        title = Title,
                        src = lblSource.Text,
                        tgt = lblTarget.Text,
                        translate = btnTranslate.Content?.ToString(),
                        clear = btnClear.Content?.ToString(),
                        copy = btnCopyResult.Content?.ToString(),
                        listen = btnSpeak.Content?.ToString(),
                        monitor = btnMonitor.Content?.ToString(),
                        phResult = placeholderResult.Text,
                        voice1 = ((System.Windows.Controls.ComboBoxItem)cmbVoice.Items[1]).Content?.ToString(),
                        tgtEn = (cmbTargetLang.Items[1] as LanguageItem)?.Name
                    });
                case "quit":
                    Application.Current.Shutdown();
                    return "{\"ok\":true}";
                default:
                    return "{\"error\":\"op desconocida\"}";
            }
        }

        private static void CtlSelectLang(System.Windows.Controls.ComboBox cmb, string code)
        {
            for (int i = 0; i < cmb.Items.Count; i++)
                if ((cmb.Items[i] as LanguageItem)?.Code == code) { cmb.SelectedIndex = i; return; }
        }

        private static string CtlValueAfter(string[] a, string flag)
        {
            for (int i = 1; i < a.Length - 1; i++)
                if (string.Equals(a[i], flag, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        private void InitializeFloatingButton()
        {
            _floatingButton = new FloatingButton();
            _floatingButton.TranslateRequested += OnFloatingButtonClicked;

            _mouseHook = new MouseHook();
            _mouseHook.SelectionDrag += OnSelectionDrag;
        }

        private void ShowTranslationPopup(string text, string targetLang, double x, double y)
        {
            // Cerrar popup anterior si existe
            CloseCurrentPopup();

            _currentPopup = new QuickPopup();
            _currentPopup.SetPosition(x, y);
            _currentPopup.TranslateText(text, targetLang);

            // Pausar el monitor Y el mouse hook mientras el popup esta abierto
            bool wasMonitoring = _isMonitoringClipboard;
            if (wasMonitoring)
            {
                _clipboardTimer?.Stop();
                _mouseHook?.Stop();
            }

            // Reactivar el monitor y mouse hook cuando el popup se cierre
            _currentPopup.Closed += (s, e) =>
            {
                if (wasMonitoring && _isMonitoringClipboard)
                {
                    _clipboardTimer?.Start();
                    _mouseHook?.Start();
                }
            };

            _currentPopup.Show();
        }

        private void CloseCurrentPopup()
        {
            if (_currentPopup != null)
            {
                try { _currentPopup.Close(); } catch { }
                _currentPopup = null;
            }
        }

        private int _lastMouseX = 0;
        private int _lastMouseY = 0;

        private async void OnSelectionDrag(int x, int y)
        {
            if (!_isMonitoringClipboard) return;

            _lastMouseX = x;
            _lastMouseY = y;

            // Pequeño delay para que la selección se complete
            await Task.Delay(150);

            // Guardar clipboard anterior
            string oldClipboard = "";
            try
            {
                if (Clipboard.ContainsText())
                    oldClipboard = Clipboard.GetText();
            }
            catch { }

            // Simular Ctrl+C para copiar la selección
            await KeyboardHelper.SimulateCopyAsync();
            await Task.Delay(100);

            // Verificar si hay nuevo texto
            try
            {
                if (Clipboard.ContainsText())
                {
                    string newText = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(newText) && newText != oldClipboard && newText.Length >= 3)
                    {
                        // Cualquier idioma -> a la caja principal y traducir al idioma destino
                        IngestSelection(newText);
                    }
                }
            }
            catch { }
        }

        private async void OnFloatingButtonClicked()
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    GetCursorPos(out POINT cursorPos);
                    string targetLang = DetectTargetLanguage(text);

                    ShowTranslationPopup(text, targetLang, cursorPos.X + 10, cursorPos.Y + 10);
                }
            }
        }

        private void InitializeClipboardMonitor()
        {
            _clipboardTimer = new DispatcherTimer();
            _clipboardTimer.Interval = TimeSpan.FromMilliseconds(300);
            _clipboardTimer.Tick += ClipboardTimer_Tick;
        }

        private void ClipboardTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Primero intentar obtener texto seleccionado via UI Automation
                string selectedText = GetSelectedTextFromFocusedElement();

                if (!string.IsNullOrWhiteSpace(selectedText) && selectedText != _lastSelectedText && selectedText.Length >= 3)
                {
                    _lastSelectedText = selectedText;
                    ProcessDetectedText(selectedText, "Seleccion");
                    return;
                }

                // Si no hay seleccion, revisar clipboard
                if (!Clipboard.ContainsText()) return;

                string currentText = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(currentText)) return;
                if (currentText == _lastClipboardText) return;

                _lastClipboardText = currentText;
                ProcessDetectedText(currentText, "Clipboard");
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Monitor: {ex.Message}";
            }
        }

        private void ProcessDetectedText(string text, string source)
        {
            string preview = text.Length > 30 ? text.Substring(0, 30) + "..." : text;
            bool isOther = IsOtherLanguage(text);

            txtStatus.Text = $"{source}: \"{preview}\" - OtroIdioma: {isOther}";

            // Cualquier idioma -> a la caja principal y traducir al idioma destino.
            // (IngestSelection decide: si destino=espanol y el texto ya es espanol, lo ignora.)
            IngestSelection(text);
        }

        // Texto seleccionado en cualquier app (LinkedIn, Telegram, ...) -> cae en la
        // caja "texto a traducir", se traduce al idioma DESTINO elegido y se pronuncia.
        // El "por que": el Monitor deja de mostrar un boton aparte; el flujo entra
        // directo a la ventana que ya sabe traducir + hablar (una sola fuente de verdad).
        private string _lastIngested = "";
        private void IngestSelection(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length < 3) return;

            Dispatcher.Invoke(async () =>
            {
                if (text == _lastIngested) return;
                var tgt = (cmbTargetLang.SelectedItem as LanguageItem)?.Code ?? "es";
                // Si el destino es espanol y el texto YA es espanol, no traducir (es tu idioma).
                // Con destino distinto, traduce CUALQUIER idioma que selecciones (aprender).
                if (tgt == "es" && !IsOtherLanguage(text)) return;

                _lastIngested = text;
                _lastClipboardText = text;   // evita que el timer del portapapeles lo repita

                _ingesting = true;           // TextChanged NO auto-traducirá (evita doble traducción/voz)
                try
                {
                    if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                    txtSource.Text = text;
                    placeholderSource.Visibility = Visibility.Collapsed;
                    txtStatus.Text = Loc.L("captured");

                    await TranslateTextAsync();   // UNA sola traducción (al idioma destino: Español por defecto)

                    // Monitor ON = responde en AUDIO por defecto (habla la traducción, una sola vez).
                    if (!string.IsNullOrWhiteSpace(txtResult.Text))
                        await SpeakAsync(txtResult.Text, tgt);
                }
                finally { _ingesting = false; }
            });
        }

        private string GetSelectedTextFromFocusedElement()
        {
            try
            {
                AutomationElement? focusedElement = AutomationElement.FocusedElement;
                if (focusedElement == null) return string.Empty;

                // Intentar obtener el patron de texto
                if (focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out object? textPatternObj))
                {
                    var textPattern = (TextPattern)textPatternObj;
                    var selection = textPattern.GetSelection();
                    if (selection.Length > 0)
                    {
                        return selection[0].GetText(-1);
                    }
                }

                // Fallback: intentar con ValuePattern
                if (focusedElement.TryGetCurrentPattern(ValuePattern.Pattern, out object? valuePatternObj))
                {
                    var valuePattern = (ValuePattern)valuePatternObj;
                    // No podemos obtener solo la seleccion, solo el valor completo
                }
            }
            catch
            {
                // Silenciar errores de UI Automation
            }

            return string.Empty;
        }

        /// <summary>
        /// Detecta si el texto es en otro idioma (no espanol)
        /// </summary>
        private bool IsOtherLanguage(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 3)
                return false;

            int nonLatinForeign = 0;     // cirilico, CJK, kana, hangul, arabe, griego, hebreo
            bool hasSpanishAccent = false;
            bool hasForeignLatinAccent = false;
            bool hasLatin = false;

            foreach (char c in text)
            {
                if (c >= 'Ѐ' && c <= 'ӿ') nonLatinForeign++;        // cirilico (ruso)
                else if (c >= '一' && c <= '鿿') nonLatinForeign++;   // CJK (chino / kanji)
                else if (c >= '぀' && c <= 'ヿ') nonLatinForeign++;   // kana japones
                else if (c >= '가' && c <= '힯') nonLatinForeign++;   // hangul coreano
                else if (c >= '؀' && c <= 'ۿ') nonLatinForeign++;   // arabe
                else if (c >= 'Ͱ' && c <= 'Ͽ') nonLatinForeign++;   // griego
                else if (c >= '֐' && c <= '׿') nonLatinForeign++;   // hebreo
                else if ("ñÑáéíóúÁÉÍÓÚüÜ¿¡".IndexOf(c) >= 0) hasSpanishAccent = true;
                else if ("àâäçèêëîïôûùßãõÀÂÄÇÈÊËÎÏÔÛÙÃÕ".IndexOf(c) >= 0) hasForeignLatinAccent = true;
                else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) hasLatin = true;
            }

            // 1) Cualquier escritura no latina => idioma extranjero
            if (nonLatinForeign >= 2) return true;
            // 2) Diacriticos propios de frances/aleman/portugues => extranjero
            if (hasForeignLatinAccent) return true;
            // 3) Acentos/signos del espanol => es espanol, no traducir
            if (hasSpanishAccent) return false;

            // 4) Texto latino sin acentos: votar palabras-funcion ingles vs espanol
            string lower = " " + text.ToLower().Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ') + " ";

            string[] englishWords = { " the ", " and ", " you ", " that ", " have ", " for ", " are ", " with ",
                " this ", " will ", " your ", " from ", " they ", " what ", " about ", " which ", " when ",
                " is ", " of ", " to ", " in ", " on ", " it ", " be ", " was ", " can ", " not ", " we ", " he " };
            string[] spanishWords = { " de ", " la ", " el ", " que ", " en ", " los ", " las ", " una ", " uno ",
                " con ", " para ", " por ", " como ", " del ", " al ", " se ", " su ", " es ", " un ", " lo ",
                " mas ", " pero ", " este ", " esta ", " esto ", " hola ", " gracias ", " muy ", " si ", " ya " };

            int en = 0; foreach (var w in englishWords) if (lower.Contains(w)) en++;
            int es = 0; foreach (var w in spanishWords) if (lower.Contains(w)) es++;

            // Senal clara de espanol y no domina el ingles => no traducir
            if (es >= 1 && es >= en) return false;
            // Domina el ingles (u otra lengua latina con palabras-funcion EN) => traducir
            if (en >= 1 && hasLatin) return true;

            // 5) Sin palabras-funcion: morfologia inglesa (sufijos tipicos no espanoles)
            if (hasLatin)
            {
                string[] enSuffix = { "ing ", "tion ", "sion ", "ness ", "ment ", "ould ", "ght ", "tive ", "ity " };
                foreach (var s in enSuffix) if (lower.Contains(s)) return true;
            }

            // Ambiguo (sin senal reconocible) => no disparar para no molestar
            return false;
        }

        public void ToggleClipboardMonitor(bool enable)
        {
            _isMonitoringClipboard = enable;
            if (enable)
            {
                _lastClipboardText = Clipboard.ContainsText() ? Clipboard.GetText() : "";
                _clipboardTimer?.Start();
                _mouseHook?.Start();
                txtStatus.Text = Loc.L("monActive");
            }
            else
            {
                _clipboardTimer?.Stop();
                _mouseHook?.Stop();
                _floatingButton?.Hide();
                txtStatus.Text = Loc.L("monInactive");
            }
        }

        private void InitializeLanguages()
        {
            cmbSourceLang.DisplayMemberPath = "Name";
            cmbSourceLang.SelectedValuePath = "Code";
            cmbTargetLang.DisplayMemberPath = "Name";
            cmbTargetLang.SelectedValuePath = "Code";
            RebuildLangCombos("auto", "es");   // nombres segun el idioma de interfaz (Loc)
        }

        // Codigos de idioma; los NOMBRES visibles salen de Loc.LangName (ES/EN).
        private static readonly string[] SrcCodes =
            { "auto", "es", "en", "de", "ru", "pt", "fr", "it", "zh", "ja", "ko", "ar", "nl", "pl", "tr", "hi" };
        private static readonly string[] TgtCodes =
            { "es", "en", "de", "ru", "pt", "fr", "it", "zh", "ja", "ko", "ar", "nl", "pl", "tr", "hi" };

        private bool _suppressLangChange = false;

        // Reconstruye los combos con los nombres en el idioma de interfaz actual,
        // conservando los codigos seleccionados. El "por que": cambiar ES<->EN debe
        // renombrar "Ingles"<->"English" sin perder que estabas traduciendo a ese idioma.
        private void RebuildLangCombos(string src = null, string tgt = null)
        {
            src ??= (cmbSourceLang.SelectedItem as LanguageItem)?.Code ?? "auto";
            tgt ??= (cmbTargetLang.SelectedItem as LanguageItem)?.Code ?? "es";
            _suppressLangChange = true;
            cmbSourceLang.ItemsSource = System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.Select(SrcCodes, c => new LanguageItem(c, Loc.LangName(c))));
            cmbTargetLang.ItemsSource = System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.Select(TgtCodes, c => new LanguageItem(c, Loc.LangName(c))));
            CtlSelectLang(cmbSourceLang, src);
            CtlSelectLang(cmbTargetLang, tgt);
            _suppressLangChange = false;
        }

        // Aplica el idioma de interfaz (ES/EN) a TODOS los textos visibles.
        private bool _uiReady = false;
        private void ApplyUiLanguage()
        {
            Title = Loc.L("title");
            lblSource.Text = Loc.L("srcLabel");
            lblTarget.Text = Loc.L("tgtLabel");
            placeholderSource.Text = Loc.L("phSource");
            placeholderResult.Text = Loc.L("phResult");
            lblChars.Text = Loc.L("chars");
            btnMonitor.Content = (btnMonitor.IsChecked == true) ? Loc.L("monOn") : Loc.L("monOff");
            btnQuickTranslate.Content = Loc.L("clipboard");
            btnTranslate.Content = Loc.L("translate");
            btnClear.Content = Loc.L("clear");
            btnCopyResult.Content = Loc.L("copy");
            btnSpeak.Content = Loc.L("listen");
            ((System.Windows.Controls.ComboBoxItem)cmbVoice.Items[0]).Content = Loc.L("female");
            ((System.Windows.Controls.ComboBoxItem)cmbVoice.Items[1]).Content = Loc.L("male");
            chkAutoTranslate.Content = Loc.L("auto");
            chkAutoVoice.Content = Loc.L("autoVoice");
            btnMonitor.ToolTip = Loc.L("tipMonitor");
            btnQuickTranslate.ToolTip = Loc.L("tipClipboard");
            btnSpeak.ToolTip = Loc.L("tipListen");
            cmbVoice.ToolTip = Loc.L("tipVoice");
            chkAutoTranslate.ToolTip = Loc.L("tipAuto");
            chkAutoVoice.ToolTip = Loc.L("tipAutoVoice");
            btnSwap.ToolTip = Loc.L("tipSwap");
            cmbUiLang.ToolTip = Loc.L("tipUiLang");
            RebuildLangCombos();
            txtStatus.Text = Loc.L("idle");
        }

        private void OnUiLangChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            Loc.Ui = cmbUiLang.SelectedIndex == 1 ? "en" : "es";
            ApplyUiLanguage();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _windowHandle = new WindowInteropHelper(this).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source?.AddHook(HwndHook);

            // Registrar Ctrl+Shift+T
            if (!RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_T))
            {
                txtStatus.Text = Loc.L("hotkeyErr");
            }

            // Monitor ENCENDIDO por defecto: al seleccionar cualquier texto, lo traduce y lo LEE.
            // (Se hace aquí, en Loaded, cuando el timer y el mouse hook ya están listos.)
            btnMonitor.IsChecked = true;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _source?.RemoveHook(HwndHook);
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _mouseHook?.Stop();
            _floatingButton?.Close();
            CloseCurrentPopup();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                OnHotKeyPressed();
                handled = true;
            }

            return IntPtr.Zero;
        }

        private async void OnHotKeyPressed()
        {
            try
            {
                // Simular Ctrl+C para copiar la seleccion actual
                await SimulateCopyAsync();

                if (Clipboard.ContainsText())
                {
                    string clipboardText = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(clipboardText))
                    {
                        // Obtener posicion del cursor
                        GetCursorPos(out POINT cursorPos);

                        // Detectar automaticamente a que idioma traducir
                        string targetLang = DetectTargetLanguage(clipboardText);

                        // Mostrar popup instantaneo con traduccion automatica
                        ShowTranslationPopup(clipboardText, targetLang, cursorPos.X, cursorPos.Y + 15);
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Detecta automaticamente a que idioma traducir.
        /// Ruso/Ingles/Portugues -> Espanol
        /// Espanol -> Ruso (para responder)
        /// </summary>
        private string DetectTargetLanguage(string text)
        {
            // Detectar caracteres cirilicos (ruso) -> traducir a espanol
            foreach (char c in text)
            {
                if (c >= '\u0400' && c <= '\u04FF')
                    return "es";
            }

            // Detectar si es espanol -> traducir a ruso (para responder)
            foreach (char c in text)
            {
                if ("ñÑáéíóúÁÉÍÓÚüÜ¿¡".Contains(c))
                    return "ru";
            }

            // Ingles u otro -> traducir a espanol
            return "es";
        }

        private async Task SimulateCopyAsync()
        {
            await KeyboardHelper.SimulateCopyAsync();
        }

        private async void BtnTranslate_Click(object sender, RoutedEventArgs e)
        {
            await TranslateTextAsync();
        }

        private async Task TranslateTextAsync()
        {
            string sourceText = txtSource.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                txtStatus.Text = Loc.L("emptyText");
                return;
            }

            var sourceLang = (cmbSourceLang.SelectedItem as LanguageItem)?.Code ?? "auto";
            var targetLang = (cmbTargetLang.SelectedItem as LanguageItem)?.Code ?? "en";

            if (sourceLang == targetLang && sourceLang != "auto")
            {
                txtStatus.Text = Loc.L("sameLangs");
                return;
            }

            try
            {
                btnTranslate.IsEnabled = false;
                txtStatus.Text = Loc.L("translating");
                txtResult.Text = "";
                placeholderResult.Visibility = Visibility.Collapsed;

                var result = await _translationService.TranslateAsync(sourceText, sourceLang, targetLang);

                txtResult.Text = result.TranslatedText;

                string detectedLang = !string.IsNullOrEmpty(result.DetectedLanguage)
                    ? $" ({Loc.L("detected")}: {result.DetectedLanguage})"
                    : "";

                txtStatus.Text = $"{Loc.L("translatedOk")}{detectedLang}  ·  Match: {result.MatchPercentage}%";

                // Auto-voz: pronunciar la traduccion al escribir/traducir. Durante una selección
                // (_ingesting) NO habla aquí: IngestSelection ya pronuncia una vez (evita doble).
                if (chkAutoVoice.IsChecked == true && !_ingesting && !string.IsNullOrWhiteSpace(result.TranslatedText))
                    _ = SpeakAsync(result.TranslatedText, targetLang);
            }
            catch (Exception ex)
            {
                txtStatus.Text = Loc.L("errTranslate") + ex.Message;
                txtResult.Text = "";
                placeholderResult.Visibility = Visibility.Visible;
            }
            finally
            {
                btnTranslate.IsEnabled = true;
            }
        }

        private async void BtnSwap_Click(object sender, RoutedEventArgs e)
        {
            // Obtener idiomas actuales
            var targetItem = cmbTargetLang.SelectedItem as LanguageItem;
            var sourceItem = cmbSourceLang.SelectedItem as LanguageItem;

            if (targetItem == null) return;

            // El texto traducido será el nuevo origen
            string newSourceText = !string.IsNullOrWhiteSpace(txtResult.Text)
                ? txtResult.Text
                : txtSource.Text;

            // Nuevo idioma origen = antiguo destino
            string newSourceCode = targetItem.Code;

            // Nuevo idioma destino = antiguo origen (si era auto, usar español)
            string newTargetCode = (sourceItem?.Code == "auto") ? "es" : sourceItem?.Code ?? "es";

            // Buscar índices
            int newSourceIndex = -1;
            int newTargetIndex = -1;

            for (int i = 0; i < cmbSourceLang.Items.Count; i++)
            {
                var item = cmbSourceLang.Items[i] as LanguageItem;
                if (item?.Code == newSourceCode) { newSourceIndex = i; break; }
            }

            for (int i = 0; i < cmbTargetLang.Items.Count; i++)
            {
                var item = cmbTargetLang.Items[i] as LanguageItem;
                if (item?.Code == newTargetCode) { newTargetIndex = i; break; }
            }

            // Aplicar cambios
            if (newSourceIndex >= 0) cmbSourceLang.SelectedIndex = newSourceIndex;
            if (newTargetIndex >= 0) cmbTargetLang.SelectedIndex = newTargetIndex;

            // Poner texto traducido como nuevo origen
            txtSource.Text = newSourceText;
            txtResult.Text = "";
            placeholderResult.Visibility = Visibility.Visible;

            UpdatePlaceholder();

            // Traducir automáticamente al nuevo idioma
            if (!string.IsNullOrWhiteSpace(txtSource.Text))
            {
                await TranslateTextAsync();
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            txtSource.Text = "";
            txtResult.Text = "";
            txtCharCount.Text = "0";
            placeholderSource.Visibility = Visibility.Visible;
            placeholderResult.Visibility = Visibility.Visible;
            txtStatus.Text = Loc.L("idle");
        }

        private void BtnCopyResult_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtResult.Text))
            {
                Clipboard.SetText(txtResult.Text);
                txtStatus.Text = Loc.L("copiedStatus");
            }
        }

        // ── Pronunciacion (TTS) ─────────────────────────────────────────────
        private System.Windows.Media.MediaPlayer _ttsPlayer;
        private readonly System.Collections.Generic.Queue<string> _ttsQueue = new System.Collections.Generic.Queue<string>();

        private async void BtnSpeak_Click(object sender, RoutedEventArgs e)
        {
            var lang = (cmbTargetLang.SelectedItem as LanguageItem)?.Code ?? "en";
            await SpeakAsync(txtResult.Text, lang);
        }

        /// <summary>Descarga y reproduce el audio de `text` en el idioma `lang` (voz nativa).</summary>
        private async System.Threading.Tasks.Task SpeakAsync(string text, string lang)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                // voz elegida en el combo: 0 = Mujer, 1 = Hombre
                var gender = (cmbVoice?.SelectedIndex == 1) ? "hombre" : "mujer";
                var files = await Services.TtsService.SynthesizeAsync(text, lang, gender);
                if (files.Count == 0) return;
                _lastSpoken = text;   // para verificar por --ctl que sí pronunció
                if (_ttsPlayer == null)
                {
                    _ttsPlayer = new System.Windows.Media.MediaPlayer();
                    _ttsPlayer.MediaEnded += (s, a) => PlayNextTts();
                }
                _ttsPlayer.Stop();
                _ttsQueue.Clear();
                foreach (var f in files) _ttsQueue.Enqueue(f);
                PlayNextTts();
            }
            catch (Exception ex)
            {
                txtStatus.Text = Loc.L("noAudio") + ex.Message;
            }
        }

        private void PlayNextTts()
        {
            if (_ttsQueue.Count == 0) return;
            var f = _ttsQueue.Dequeue();
            try { _ttsPlayer.Open(new Uri(f, UriKind.Absolute)); _ttsPlayer.Play(); }
            catch { }
        }

        private void BtnMonitor_Click(object sender, RoutedEventArgs e)
        {
            bool isActive = btnMonitor.IsChecked == true;
            ToggleClipboardMonitor(isActive);
            btnMonitor.Content = isActive ? Loc.L("monOn") : Loc.L("monOff");
        }

        private void BtnQuickTranslate_Click(object sender, RoutedEventArgs e)
        {
            // Simula Ctrl+Shift+T - traduce el contenido del portapapeles
            if (Clipboard.ContainsText())
            {
                string clipboardText = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(clipboardText))
                {
                    string targetLang = DetectTargetLanguage(clipboardText);

                    ShowTranslationPopup(clipboardText, targetLang, Left + Width / 2 - 200, Top + Height / 2 - 100);
                }
                else
                {
                    txtStatus.Text = Loc.L("clipEmpty");
                }
            }
            else
            {
                txtStatus.Text = Loc.L("clipNone");
            }
        }

        private async void TxtSource_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdatePlaceholder();

            // Si el texto lo puso IngestSelection (selección con Monitor), NO auto-traducir aquí:
            // IngestSelection ya traduce y pronuncia una sola vez (evita que se pisen dos voces).
            if (_ingesting) return;

            // Auto-traducir si esta activado
            if (chkAutoTranslate.IsChecked == true && !string.IsNullOrWhiteSpace(txtSource.Text))
            {
                // Cancelar traduccion anterior pendiente
                _autoTranslateCts?.Cancel();
                _autoTranslateCts = new CancellationTokenSource();

                try
                {
                    // Esperar un momento antes de traducir (debounce)
                    await Task.Delay(AUTO_TRANSLATE_DELAY_MS, _autoTranslateCts.Token);
                    await TranslateTextAsync();
                }
                catch (TaskCanceledException)
                {
                    // Ignorar - fue cancelada porque el usuario sigue escribiendo
                }
            }
        }

        private void UpdatePlaceholder()
        {
            placeholderSource.Visibility = string.IsNullOrEmpty(txtSource.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            txtCharCount.Text = (txtSource.Text?.Length ?? 0).ToString();
        }

        private async void OnLanguageChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppressLangChange) return;   // no traducir mientras se reconstruyen los combos
            // Traducir automaticamente al cambiar idioma si hay texto
            if (chkAutoTranslate.IsChecked == true && !string.IsNullOrWhiteSpace(txtSource.Text))
            {
                await TranslateTextAsync();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Ctrl+Enter para traducir
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnTranslate_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }

            // Escape para minimizar
            if (e.Key == Key.Escape)
            {
                WindowState = WindowState.Minimized;
                e.Handled = true;
            }
        }
    }

    public class LanguageItem
    {
        public string Code { get; set; }
        public string Name { get; set; }

        public LanguageItem(string code, string name)
        {
            Code = code;
            Name = name;
        }
    }
}
