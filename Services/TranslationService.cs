using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;

namespace Traductor.Services
{
    public class TranslationResult
    {
        public string TranslatedText { get; set; } = string.Empty;
        public string DetectedLanguage { get; set; } = string.Empty;
        public double MatchPercentage { get; set; }
    }

    /// <summary>
    /// Motor con RESPALDO:
    ///  1) Google free (translate_a) -> mejor calidad, auto-detecta el idioma,
    ///     SIN limite diario desde una IP residencial (tu PC). Es la opcion buena.
    ///  2) MyMemory -> respaldo si Google falla. Detecta su aviso de "sin cuota".
    /// </summary>
    public class TranslationService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const string GoogleUrl = "https://translate.googleapis.com/translate_a/single";
        private const string MyMemoryUrl = "https://api.mymemory.translated.net/get";
        private const int MAX_CHARS = 4500; // Google aguanta mucho mas que MyMemory

        public TranslationService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            // El endpoint gratis de Google espera un User-Agent de navegador
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        }

        public async Task<TranslationResult> TranslateAsync(string text, string sourceLang, string targetLang)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("El texto no puede estar vacio.", nameof(text));

            if (text.Length > MAX_CHARS)
                return await TranslateLongTextAsync(text, sourceLang, targetLang);

            return await TranslateChunkAsync(text, sourceLang, targetLang);
        }

        private async Task<TranslationResult> TranslateLongTextAsync(string text, string sourceLang, string targetLang)
        {
            var chunks = SplitTextIntoChunks(text, MAX_CHARS);
            var translatedChunks = new List<string>();
            string detectedLang = sourceLang;

            foreach (var chunk in chunks)
            {
                var result = await TranslateChunkAsync(chunk, sourceLang, targetLang);
                translatedChunks.Add(result.TranslatedText);
                if (!string.IsNullOrEmpty(result.DetectedLanguage))
                    detectedLang = result.DetectedLanguage;
            }

            return new TranslationResult
            {
                TranslatedText = string.Join(" ", translatedChunks),
                DetectedLanguage = detectedLang,
                MatchPercentage = 100
            };
        }

        private List<string> SplitTextIntoChunks(string text, int maxLength)
        {
            var chunks = new List<string>();
            var sentences = text.Split(new[] { ". ", ".\n", "! ", "? " }, StringSplitOptions.None);

            string currentChunk = "";
            foreach (var sentence in sentences)
            {
                if (currentChunk.Length + sentence.Length + 2 <= maxLength)
                {
                    currentChunk += (currentChunk.Length > 0 ? ". " : "") + sentence;
                }
                else
                {
                    if (currentChunk.Length > 0)
                        chunks.Add(currentChunk);

                    if (sentence.Length > maxLength)
                    {
                        for (int i = 0; i < sentence.Length; i += maxLength)
                            chunks.Add(sentence.Substring(i, Math.Min(maxLength, sentence.Length - i)));
                        currentChunk = "";
                    }
                    else
                    {
                        currentChunk = sentence;
                    }
                }
            }
            if (currentChunk.Length > 0)
                chunks.Add(currentChunk);

            return chunks;
        }

        // ---- Selector de motor: Google primero, MyMemory de respaldo ----
        private async Task<TranslationResult> TranslateChunkAsync(string text, string sourceLang, string targetLang)
        {
            try
            {
                var g = await TranslateGoogleAsync(text, sourceLang, targetLang);
                if (g != null && !string.IsNullOrWhiteSpace(g.TranslatedText))
                    return g;
            }
            catch
            {
                // Google fallo/bloqueado -> caemos a MyMemory
            }

            return await TranslateMyMemoryAsync(text, sourceLang, targetLang);
        }

        // ---- 1) Google free (auto-detecta, sin limite desde tu PC) ----
        private async Task<TranslationResult> TranslateGoogleAsync(string text, string sourceLang, string targetLang)
        {
            string sl = string.IsNullOrEmpty(sourceLang) || sourceLang == "auto" ? "auto" : sourceLang;
            string url = $"{GoogleUrl}?client=gtx&sl={sl}&tl={targetLang}&dt=t&q={HttpUtility.UrlEncode(text)}";

            var response = await _httpClient.GetStringAsync(url);
            var data = JArray.Parse(response);

            // data[0] = lista de segmentos [ [traducido, original, ...], ... ]
            var sb = new StringBuilder();
            if (data.Count > 0 && data[0] is JArray segs)
            {
                foreach (var seg in segs)
                {
                    var t = (seg as JArray)?[0]?.Value<string>();
                    if (!string.IsNullOrEmpty(t)) sb.Append(t);
                }
            }

            // data[2] = idioma de origen detectado por Google
            string detected = data.Count > 2 ? (data[2]?.Value<string>() ?? sourceLang) : sourceLang;

            return new TranslationResult
            {
                TranslatedText = sb.ToString(),
                DetectedLanguage = GetLanguageName((detected ?? "").Split('-')[0]),
                MatchPercentage = 100
            };
        }

        // ---- 2) Respaldo: MyMemory (detecta el aviso de "sin cuota") ----
        private async Task<TranslationResult> TranslateMyMemoryAsync(string text, string sourceLang, string targetLang)
        {
            string src = string.IsNullOrEmpty(sourceLang) || sourceLang == "auto" ? DetectLanguage(text) : sourceLang;
            string url = $"{MyMemoryUrl}?q={HttpUtility.UrlEncode(text)}&langpair={src}|{targetLang}";

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                var status = json["responseStatus"]?.ToString() ?? "0";
                var translated = json["responseData"]?["translatedText"]?.Value<string>() ?? string.Empty;
                var up = translated.ToUpperInvariant();

                // MyMemory a veces responde 200 PERO mete el aviso de limite en el texto
                if (status != "200" ||
                    up.StartsWith("MYMEMORY WARNING") || up.StartsWith("QUERY LENGTH") || up.StartsWith("INVALID"))
                {
                    var detail = json["responseDetails"]?.Value<string>();
                    if (string.IsNullOrEmpty(detail)) detail = translated;
                    throw new Exception($"MyMemory sin cuota o error: {detail}");
                }

                var match = json["responseData"]?["match"]?.Value<double>() ?? 0;
                string detectedLang = src;
                if (json["matches"] is JArray matches && matches.Count > 0)
                {
                    var sourceLangFromMatch = matches[0]?["source"]?.Value<string>();
                    if (!string.IsNullOrEmpty(sourceLangFromMatch))
                        detectedLang = sourceLangFromMatch.Split('-')[0];
                }

                return new TranslationResult
                {
                    TranslatedText = translated,
                    DetectedLanguage = GetLanguageName(detectedLang),
                    MatchPercentage = Math.Round(match * 100, 1)
                };
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de conexion: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("La solicitud excedio el tiempo de espera.");
            }
        }

        /// <summary>Detección de idioma por PALABRAS CLAVE (fiable para de/en/es/fr/it/pt).
        /// Antes devolvía "en" para todo lo latino -> el alemán se traducía como inglés y no traducía.
        /// Ahora vota las palabras función más comunes de cada idioma y gana el que más acierte.</summary>
        public string DetectLanguage(string text)
        {
            foreach (char c in text)
            {
                if (c >= 0x0400 && c <= 0x04FF) return "ru";   // cirilico
                if (c >= 0x4E00 && c <= 0x9FFF) return "zh";   // chino
                if ((c >= 0x3040 && c <= 0x309F) || (c >= 0x30A0 && c <= 0x30FF)) return "ja"; // japones
                if (c >= 0xAC00 && c <= 0xD7AF) return "ko";   // coreano
                if (c >= 0x0600 && c <= 0x06FF) return "ar";   // arabe
            }
            string t = " " + text.ToLowerInvariant().Replace(".", " ").Replace(",", " ") + " ";
            // señales fuertes por caracter
            if (t.Contains("ñ") || t.Contains("¿") || t.Contains("¡")) return "es";
            if (t.Contains("ß")) return "de";
            var langs = new (string code, string[] words)[]
            {
                ("de", new[]{"der","die","das","und","ist","mit","ich","ein","eine","nicht","sich","sie","für","von","zu","dem","den","auch","wo","im","auf","aus","bei","habe","haben","sind","wird","war","noch","schon","oder","aber","wie","was","über","einen","zum","werden"}),
                ("en", new[]{"the","and","is","are","of","to","in","that","it","for","with","you","this","on","be","at","have","has","was","not","but","which","from","by","as","or","an","they","will"}),
                ("es", new[]{"el","la","los","las","de","que","en","un","una","es","son","con","por","para","del","al","se","su","como","pero","más","este","esta","muy","también","hay","donde"}),
                ("fr", new[]{"le","les","des","une","et","est","dans","pour","que","qui","avec","sur","ne","pas","ce","cette","vous","nous","mais","ou","du","au"}),
                ("it", new[]{"il","lo","di","che","è","un","una","per","con","non","sono","gli","questo","questa","anche","come","più","della","nel"}),
                ("pt", new[]{"os","as","de","que","em","um","uma","não","com","por","para","do","da","se","mais","como","mas","também","uma","você"}),
            };
            string best = "en"; int bestScore = 0;
            foreach (var (code, words) in langs)
            {
                int s = 0;
                foreach (var w in words) if (t.Contains(" " + w + " ")) s++;
                if (s > bestScore) { bestScore = s; best = code; }
            }
            if (bestScore == 0 && (t.Contains("ä") || t.Contains("ö") || t.Contains("ü"))) return "de";
            return best;
        }

        private string GetLanguageName(string code)
        {
            return code.ToLower() switch
            {
                "es" => "Espanol",
                "en" => "English",
                "ru" => "Ruso",
                "pt" => "Portugues",
                "fr" => "Frances",
                "de" => "Aleman",
                "it" => "Italiano",
                "ar" => "Arabe",
                "zh" or "zh-cn" => "Chino",
                "ja" => "Japones",
                "ko" => "Coreano",
                "fa" => "Persa",
                _ => code
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
