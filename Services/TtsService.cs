using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Traductor.Services
{
    /// <summary>
    /// Texto-a-voz. Primario: voces NEURALES de Microsoft Edge (edge-tts) — muy realistas,
    /// con voz de HOMBRE y de MUJER por idioma. Respaldo: Google Translate TTS (una voz).
    /// </summary>
    public static class TtsService
    {
        private static readonly HttpClient _http = new HttpClient();

        static TtsService()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            _http.Timeout = TimeSpan.FromSeconds(20);
        }

        /// <summary>Voz neural de Edge para (idioma, género). gender: "hombre"/"mujer".</summary>
        public static string PickVoice(string lang, string gender)
        {
            bool m = !string.IsNullOrEmpty(gender) &&
                     (gender.StartsWith("h", StringComparison.OrdinalIgnoreCase) ||   // hombre
                      gender.StartsWith("m", StringComparison.OrdinalIgnoreCase) && gender.Length <= 2); // male/m
            var l = (lang ?? "en").Split('-')[0].ToLowerInvariant();
            return (l, m) switch
            {
                ("es", true) => "es-ES-AlvaroNeural", ("es", false) => "es-ES-ElviraNeural",
                ("en", true) => "en-US-GuyNeural", ("en", false) => "en-US-AriaNeural",
                ("pt", true) => "pt-BR-AntonioNeural", ("pt", false) => "pt-BR-FranciscaNeural",
                ("fr", true) => "fr-FR-HenriNeural", ("fr", false) => "fr-FR-DeniseNeural",
                ("de", true) => "de-DE-ConradNeural", ("de", false) => "de-DE-KatjaNeural",
                ("it", true) => "it-IT-DiegoNeural", ("it", false) => "it-IT-ElsaNeural",
                ("ru", true) => "ru-RU-DmitryNeural", ("ru", false) => "ru-RU-SvetlanaNeural",
                ("zh", true) => "zh-CN-YunxiNeural", ("zh", false) => "zh-CN-XiaoxiaoNeural",
                ("ja", true) => "ja-JP-KeitaNeural", ("ja", false) => "ja-JP-NanamiNeural",
                ("ko", true) => "ko-KR-InJoonNeural", ("ko", false) => "ko-KR-SunHiNeural",
                ("ar", true) => "ar-SA-HamedNeural", ("ar", false) => "ar-SA-ZariyahNeural",
                ("nl", true) => "nl-NL-MaartenNeural", ("nl", false) => "nl-NL-ColetteNeural",
                ("pl", true) => "pl-PL-MarekNeural", ("pl", false) => "pl-PL-ZofiaNeural",
                ("tr", true) => "tr-TR-AhmetNeural", ("tr", false) => "tr-TR-EmelNeural",
                ("hi", true) => "hi-IN-MadhurNeural", ("hi", false) => "hi-IN-SwaraNeural",
                (_, true) => "en-US-GuyNeural", (_, false) => "en-US-AriaNeural",
            };
        }

        /// <summary>Genera el audio del texto y devuelve las rutas MP3 (en orden).
        /// gender = "hombre"/"mujer" (por defecto mujer).</summary>
        public static async Task<List<string>> SynthesizeAsync(string text, string lang, string gender = "hombre")
        {
            var files = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return files;
            if (string.IsNullOrWhiteSpace(lang) || lang == "auto") lang = "en";

            // 1) Voz neural de Edge (edge-tts). Una sola llamada; edge-tts maneja texto largo.
            var edge = await EdgeTtsAsync(text, PickVoice(lang, gender));
            if (edge != null) { files.Add(edge); return files; }

            // 2) Respaldo: Google Translate TTS (voz única, sin elegir).
            foreach (var chunk in Split(text, 190))
            {
                var url = "https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob" +
                          "&tl=" + Uri.EscapeDataString(lang) +
                          "&q=" + Uri.EscapeDataString(chunk);
                try
                {
                    var bytes = await _http.GetByteArrayAsync(url);
                    var path = Path.Combine(Path.GetTempPath(), "msmtts_" + Guid.NewGuid().ToString("N") + ".mp3");
                    File.WriteAllBytes(path, bytes);
                    files.Add(path);
                }
                catch { /* si falla un trozo, seguimos con los demas */ }
            }
            return files;
        }

        /// <summary>Llama a edge-tts (Python) para generar un MP3 neural. null si no está disponible.</summary>
        private static async Task<string> EdgeTtsAsync(string text, string voice)
        {
            var txt = Path.Combine(Path.GetTempPath(), "msmtts_" + Guid.NewGuid().ToString("N") + ".txt");
            var mp3 = Path.ChangeExtension(txt, ".mp3");
            try
            {
                File.WriteAllText(txt, text, new UTF8Encoding(false));
                foreach (var py in new[] { "python", @"C:\Program Files\Python312\python.exe", "py" })
                {
                    try
                    {
                        var psi = new ProcessStartInfo(py,
                            $"-m edge_tts --voice {voice} --file \"{txt}\" --write-media \"{mp3}\"")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        };
                        using var p = Process.Start(psi);
                        if (p == null) continue;
                        await p.WaitForExitAsync();
                        if (File.Exists(mp3) && new FileInfo(mp3).Length > 200)
                            return mp3;
                    }
                    catch { /* prueba el siguiente ejecutable de python */ }
                }
            }
            catch { }
            finally { try { File.Delete(txt); } catch { } }
            return null;   // edge-tts no disponible -> el caller usa Google
        }

        // Parte el texto en trozos <= max caracteres, sin cortar palabras (solo para Google).
        private static List<string> Split(string text, int max)
        {
            var res = new List<string>();
            var sb = new StringBuilder();
            foreach (var w in text.Split(' '))
            {
                if (sb.Length + w.Length + 1 > max && sb.Length > 0) { res.Add(sb.ToString()); sb.Clear(); }
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(w);
            }
            if (sb.Length > 0) res.Add(sb.ToString());
            return res;
        }
    }
}
