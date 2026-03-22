using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace neTiPx.Services
{
    /// <summary>
    /// Singleton-Dienst für die Mehrsprachigkeit der Anwendung.
    /// Lädt JSON-Sprachdateien aus dem Ordner "lang/" neben der Exe
    /// und stellt Texte per Schlüssel bereit, mit Fallback auf Englisch.
    /// </summary>
    public sealed class LanguageManager
    {
        // ── Singleton ────────────────────────────────────────────────────────
        private static readonly Lazy<LanguageManager> _instance =
            new(() => new LanguageManager());

        public static LanguageManager Instance => _instance.Value;

        // ── Felder ───────────────────────────────────────────────────────────
        private Dictionary<string, string> _current = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _fallback = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Aktuell geladener Sprachcode, z.B. "de" oder "en".</summary>
        public string CurrentLanguageCode { get; private set; } = "de";

        // ── Ereignis ─────────────────────────────────────────────────────────
        /// <summary>Wird ausgelöst, nachdem eine neue Sprache geladen wurde.</summary>
        public event EventHandler? LanguageChanged;

        // ── Konstruktor ───────────────────────────────────────────────────────
        private LanguageManager() { }

        // ── Öffentliche API ───────────────────────────────────────────────────

        /// <summary>
        /// Lädt die Sprache passend zum übergebenen Code oder – bei "System" –
        /// anhand von <see cref="CultureInfo.CurrentUICulture"/>.
        /// </summary>
        /// <param name="languageCodeOrSystem">
        /// Sprachcode (z.B. "de", "en", "fr") oder "System" für automatische Erkennung.
        /// </param>
        public void LoadLanguage(string languageCodeOrSystem)
        {
            var code = ResolveLanguageCode(languageCodeOrSystem);
            CurrentLanguageCode = code;

            // Fallback-Wörterbuch (immer Englisch, sofern vorhanden)
            _fallback = LoadFile("en") ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Zielsprache laden; wenn nicht gefunden → Fallback als primary
            _current = LoadFile(code) ?? _fallback;

            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gibt den lokalisierten Text für den Schlüssel zurück.
        /// Wenn der key in der aktuellen Sprache fehlt, wird der englische Fallback verwendet.
        /// Wenn auch der fehlt, wird der key selbst zurückgegeben.
        /// </summary>
        public string Lang(string key)
        {
            if (_current.TryGetValue(key, out var value))
                return value;

            if (_fallback.TryGetValue(key, out var fallbackValue))
                return fallbackValue;

            return key; // Letzter Ausweg: Key direkt anzeigen
        }

        /// <summary>Kurzform für <see cref="Lang"/>.</summary>
        public string this[string key] => Lang(key);

        /// <summary>
        /// Gibt alle verfügbaren Sprachcodes zurück, die im lang-Ordner liegen.
        /// Z.B. ["de", "en", "fr"]
        /// </summary>
        public IReadOnlyList<string> GetAvailableLanguages()
        {
            var dir = GetLangDirectory();
            if (!Directory.Exists(dir))
                return Array.Empty<string>();

            return Directory
                .GetFiles(dir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f).ToLowerInvariant())
                .OrderBy(c => c)
                .ToList();
        }

        // ── Hilfsmethoden ─────────────────────────────────────────────────────

        private string ResolveLanguageCode(string codeOrSystem)
        {
            if (string.IsNullOrWhiteSpace(codeOrSystem)
                || string.Equals(codeOrSystem, "System", StringComparison.OrdinalIgnoreCase))
            {
                // System-Sprache ermitteln
                var culture = CultureInfo.CurrentUICulture;
                var twoLetter = culture.TwoLetterISOLanguageName.ToLowerInvariant();
                var available = GetAvailableLanguages();

                // Exakter Match (z.B. "de")
                if (available.Contains(twoLetter))
                    return twoLetter;

                // Fallback auf Englisch
                if (available.Contains("en"))
                    return "en";

                // Erstes verfügbares
                if (available.Count > 0)
                    return available[0];

                return "de";
            }

            return codeOrSystem.Trim().ToLowerInvariant();
        }

        private Dictionary<string, string>? LoadFile(string code)
        {
            var dir = GetLangDirectory();
            var path = Path.Combine(dir, code + ".json");

            if (!File.Exists(path))
                return null;

            try
            {
                var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return dict != null
                    ? new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase)
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetLangDirectory()
        {
            // 1. Bevorzugt: neben der Exe
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var exeLang = Path.Combine(exeDir, "lang");
            if (Directory.Exists(exeLang))
                return exeLang;

            // 2. Debug/Dev-Fallback: in Elternverzeichnissen nach "lang" suchen
            // (z.B. wenn aus bin/.. gestartet wurde und CopyToOutput fehlt)
            try
            {
                var dirInfo = new DirectoryInfo(exeDir);
                for (var i = 0; i < 8 && dirInfo != null; i++)
                {
                    var candidate = Path.Combine(dirInfo.FullName, "lang");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    dirInfo = dirInfo.Parent;
                }
            }
            catch
            {
            }

            // 3. Fallback: %APPDATA%\neTiPx\lang
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "neTiPx", "lang");
        }
    }
}
