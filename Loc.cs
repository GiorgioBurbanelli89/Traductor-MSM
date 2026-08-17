using System.Collections.Generic;

namespace Traductor
{
    /// <summary>
    /// Idioma de la INTERFAZ (no de la traduccion). El "por que": un usuario de habla
    /// inglesa abre la app y no entiende los botones en espanol. Aqui viven todos los
    /// textos en ES y EN; Loc.L(clave) devuelve el del idioma actual (Loc.Ui).
    /// </summary>
    public static class Loc
    {
        public static string Ui = "es";   // "es" | "en"

        private static readonly Dictionary<string, (string es, string en)> T = new()
        {
            ["title"]        = ("Traductor MSM — Ctrl+Shift+T para traducir", "MSM Translator — Ctrl+Shift+T to translate"),
            ["srcLabel"]     = ("Idioma origen", "Source language"),
            ["tgtLabel"]     = ("Idioma destino", "Target language"),
            ["phSource"]     = ("Escribe o pega el texto a traducir...", "Type or paste the text to translate..."),
            ["phResult"]     = ("La traducción aparecerá aquí...", "The translation will appear here..."),
            ["chars"]        = (" caracteres", " characters"),
            ["monOn"]        = ("🟢 MONITOR ON", "🟢 MONITOR ON"),
            ["monOff"]       = ("🔴 MONITOR OFF", "🔴 MONITOR OFF"),
            ["clipboard"]    = ("📋 Portapapeles", "📋 Clipboard"),
            ["translate"]    = ("Traducir", "Translate"),
            ["clear"]        = ("Limpiar", "Clear"),
            ["copy"]         = ("Copiar", "Copy"),
            ["copied"]       = ("¡Copiado!", "Copied!"),
            ["listen"]       = ("🔊 Escuchar", "🔊 Listen"),
            ["female"]       = ("👩 Mujer", "👩 Female"),
            ["male"]         = ("👨 Hombre", "👨 Male"),
            ["auto"]         = ("Auto", "Auto"),
            ["autoVoice"]    = ("🔊 Auto-voz", "🔊 Auto-voice"),
            ["idle"]         = ("Listo. Presiona Ctrl+Shift+T para traducir texto seleccionado.",
                                "Ready. Press Ctrl+Shift+T to translate selected text."),
            ["translating"]  = ("Traduciendo...", "Translating..."),
            ["translatedOk"] = ("Traducido correctamente.", "Translated successfully."),
            ["detected"]     = ("Detectado", "Detected"),
            ["captured"]     = ("Texto capturado — traduciendo...", "Text captured — translating..."),
            ["copiedStatus"] = ("Resultado copiado al portapapeles.", "Result copied to clipboard."),
            ["monActive"]    = ("Monitor ACTIVO — selecciona texto en cualquier app para traducir",
                                "Monitor ON — select text in any app to translate"),
            ["monInactive"]  = ("Monitor desactivado", "Monitor off"),
            ["clipEmpty"]    = ("El portapapeles está vacío.", "The clipboard is empty."),
            ["clipNone"]     = ("No hay texto en el portapapeles. Copia algo primero.",
                                "No text in the clipboard. Copy something first."),
            ["noAudio"]      = ("No se pudo reproducir el audio: ", "Could not play the audio: "),
            ["errTranslate"] = ("Error al traducir: ", "Translation error: "),
            ["emptyText"]    = ("Por favor, ingresa texto para traducir.", "Please enter text to translate."),
            ["sameLangs"]    = ("El idioma origen y destino son iguales.", "Source and target languages are the same."),
            ["hotkeyErr"]    = ("Error: no se pudo registrar el atajo Ctrl+Shift+T",
                                "Error: could not register the Ctrl+Shift+T hotkey"),
            // tooltips
            ["tipMonitor"]   = ("Actívalo para traducir automáticamente el texto que selecciones en cualquier app",
                                "Turn on to auto-translate text you select in any app"),
            ["tipClipboard"] = ("Traduce el texto del portapapeles (Ctrl+Shift+T)", "Translate the clipboard text (Ctrl+Shift+T)"),
            ["tipListen"]    = ("Pronuncia la traducción (voz neural, para aprender)", "Speak the translation (neural voice, to learn)"),
            ["tipVoice"]     = ("Voz de la pronunciación (neural, Microsoft Edge)", "Pronunciation voice (neural, Microsoft Edge)"),
            ["tipAuto"]      = ("Traducir automáticamente al escribir", "Auto-translate as you type"),
            ["tipAutoVoice"] = ("Pronuncia la traducción automáticamente al traducir", "Speak the translation automatically"),
            ["tipSwap"]      = ("Intercambiar idiomas", "Swap languages"),
            ["tipUiLang"]    = ("Idioma de la interfaz", "Interface language"),
        };

        private static readonly Dictionary<string, (string es, string en)> Langs = new()
        {
            ["auto"] = ("Detectar automático", "Auto-detect"),
            ["es"] = ("Español", "Spanish"),
            ["en"] = ("Inglés", "English"),
            ["de"] = ("Alemán", "German"),
            ["ru"] = ("Ruso", "Russian"),
            ["pt"] = ("Portugués", "Portuguese"),
            ["fr"] = ("Francés", "French"),
            ["it"] = ("Italiano", "Italian"),
            ["zh"] = ("Chino", "Chinese"),
            ["ja"] = ("Japonés", "Japanese"),
            ["ko"] = ("Coreano", "Korean"),
            ["ar"] = ("Árabe", "Arabic"),
            ["nl"] = ("Holandés", "Dutch"),
            ["pl"] = ("Polaco", "Polish"),
            ["tr"] = ("Turco", "Turkish"),
            ["hi"] = ("Hindi", "Hindi"),
        };

        public static string L(string key)
            => T.TryGetValue(key, out var v) ? (Ui == "en" ? v.en : v.es) : key;

        public static string LangName(string code)
            => Langs.TryGetValue(code, out var v) ? (Ui == "en" ? v.en : v.es) : code;
    }
}
