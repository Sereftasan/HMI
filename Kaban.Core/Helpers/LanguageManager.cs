using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kaban.Core.Helpers
{
    public static class LanguageManager
    {
        // TWO layers of translations. A key is looked up in the MACHINE dictionary
        // first, then falls back to the COMMON dictionary, then to the literal text.
        private static Dictionary<string, Dictionary<string, string>> _machine =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, Dictionary<string, string>> _common =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static string CurrentLanguage { get; private set; } = "EN";
        public static List<string> AvailableLanguages { get; private set; } = new List<string>();

        public static event EventHandler LanguageChanged;

        /// <summary>
        /// Loads the COMMON (shared) translation file - plant-wide texts used by
        /// every machine (Start/Stop, confirm dialogs, unit names, etc.).
        /// </summary>
        public static void LoadCommonTranslations(string filePath)
        {
            LoadInto(_common, filePath, clearFirst: true);
        }

        /// <summary>
        /// Loads the MACHINE-SPECIFIC file, checked BEFORE the common one so a
        /// machine can override or add its own texts.
        /// </summary>
        public static void LoadMachineTranslations(string filePath)
        {
            LoadInto(_machine, filePath, clearFirst: true);
        }

        /// <summary>
        /// Adds a machine-layer translation file WITHOUT clearing what is already
        /// loaded. Used when several machine modules (a main machine plus mounted
        /// sub-machines) each ship their own Translations.csv - loading them with
        /// LoadMachineTranslations would make the last one wipe the others.
        /// Later files win on duplicate keys, so a host can override a sub-machine's
        /// wording without editing the sub-machine.
        /// </summary>
        public static void AddMachineTranslations(string filePath)
        {
            LoadInto(_machine, filePath, clearFirst: false);
        }

        /// <summary>Back-compat: single-CSV callers -> treated as the machine file.</summary>
        public static void LoadTranslations(string filePath)
        {
            LoadMachineTranslations(filePath);
        }

        /// <summary>
        /// Loads COMMON translations from a stream - used for the copy embedded in
        /// Kaban.UI, so every machine that references the UI library has the shared
        /// keys even when the .csv is missing from its output folder.
        /// </summary>
        public static void LoadCommonTranslationsFromStream(Stream stream, bool clearFirst = true)
        {
            if (stream == null) return;
            try
            {
                using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                {
                    var all = new List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null) all.Add(line);
                    ParseInto(_common, all.ToArray(), clearFirst);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Embedded translation load error: {ex.Message}");
            }
        }

        /// <summary>True when the common layer has any entries loaded.</summary>
        public static bool HasCommonTranslations()
        {
            foreach (var kv in _common) if (kv.Value.Count > 0) return true;
            return false;
        }

        private static void LoadInto(Dictionary<string, Dictionary<string, string>> target, string filePath, bool clearFirst)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                if (!string.IsNullOrEmpty(filePath))
                    Console.WriteLine($"Translation file not found: {filePath}");
                return;
            }

            try
            {
                var lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
                ParseInto(target, lines, clearFirst);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Translation Load Error ({filePath}): {ex.Message}");
            }
        }

        private static void ParseInto(Dictionary<string, Dictionary<string, string>> target, string[] lines, bool clearFirst)
        {
            if (clearFirst) target.Clear();

            try
            {
                if (lines.Length == 0) return;

                var headers = lines[0].Split(';');
                var langs = new List<string>();
                for (int i = 1; i < headers.Length; i++)
                {
                    string langCode = headers[i].Trim().ToUpper();
                    langs.Add(langCode);
                    if (!target.ContainsKey(langCode))
                        target[langCode] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (!AvailableLanguages.Contains(langCode))
                        AvailableLanguages.Add(langCode);
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    var columns = lines[i].Split(';');
                    string key = columns[0].Trim();
                    if (string.IsNullOrEmpty(key)) continue;

                    // A line break is written "\r\n" in the file (one row per key);
                    // shown as a line break, not as the four characters.
                    for (int c = 1; c < columns.Length && c - 1 < langs.Count; c++)
                        target[langs[c - 1]][key] = columns[c].Trim().Replace("\\r\\n", "\r\n").Replace("\\n", "\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Translation parse error: {ex.Message}");
            }
        }

        /// <summary>Runtime single-key registration (AlarmDefinitionLoader). Machine layer.</summary>
        public static void AddTranslation(string langCode, string key, string text)
        {
            if (string.IsNullOrWhiteSpace(langCode) || string.IsNullOrWhiteSpace(key)) return;
            langCode = langCode.Trim().ToUpper();
            if (!_machine.ContainsKey(langCode))
            {
                _machine[langCode] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!AvailableLanguages.Contains(langCode)) AvailableLanguages.Add(langCode);
            }
            _machine[langCode][key] = text;
        }

        /// <summary>Tells every page the texts changed (a translation put in force while the program runs).</summary>
        public static void RaiseLanguageChanged()
        {
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void ChangeLanguage(string langCode)
        {
            langCode = langCode.ToUpper();
            bool known = _machine.ContainsKey(langCode) || _common.ContainsKey(langCode);
            if (known && CurrentLanguage != langCode)
            {
                CurrentLanguage = langCode;
                LanguageChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        // Machine first, then common - and English as the last resort.
        private static bool TryLookup(string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(key)) return false;
            if (TryLookupIn(CurrentLanguage, key, out value)) return true;

            // FALL BACK TO ENGLISH when a language has no text for this key (or the
            // cell is blank). With six languages in the files, most machine texts are
            // translated but not all; showing English is far better than showing a
            // blank caption or a raw key.
            if (!string.Equals(CurrentLanguage, "EN", StringComparison.OrdinalIgnoreCase) &&
                TryLookupIn("EN", key, out value)) return true;
            return false;
        }

        private static bool TryLookupIn(string lang, string key, out string value)
        {
            value = null;
            Dictionary<string, string> dict;
            if (_machine.TryGetValue(lang, out dict) && dict.TryGetValue(key, out value) &&
                !string.IsNullOrWhiteSpace(value)) return true;
            if (_common.TryGetValue(lang, out dict) && dict.TryGetValue(key, out value) &&
                !string.IsNullOrWhiteSpace(value)) return true;
            value = null;
            return false;
        }

        /// <summary>True when the key has a text in some language.</summary>
        public static bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            foreach (var d in _machine.Values) if (d.ContainsKey(key)) return true;
            foreach (var d in _common.Values) if (d.ContainsKey(key)) return true;
            return false;
        }

        /// <summary>
        /// The key whose text on the screen (the language shown, else English) is
        /// <paramref name="text"/> - a table's heading made from a key, found again
        /// by its words (the notes page). Null when none; the machine's keys first.
        /// </summary>
        public static string KeyOfText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            foreach (var lang in new[] { CurrentLanguage, "EN" })
                foreach (var source in new[] { _machine, _common })
                {
                    Dictionary<string, string> dict;
                    if (!source.TryGetValue(lang ?? "EN", out dict)) continue;
                    foreach (var kv in dict)
                        if (string.Equals((kv.Value ?? "").Trim(), text, StringComparison.Ordinal)) return kv.Key;
                }
            return null;
        }

        /// <summary>A key's text in one language (machine, then common), or null - no English in its place.</summary>
        public static string GetStringIn(string langCode, string key)
        {
            string value;
            return !string.IsNullOrEmpty(langCode) && TryLookupIn(langCode.Trim().ToUpper(), key, out value) ? value : null;
        }

        public static string GetString(string key, string fallbackText = "")
        {
            if (string.IsNullOrEmpty(key)) return fallbackText;
            string translated;
            if (TryLookup(key, out translated)) return translated;
            return string.IsNullOrEmpty(fallbackText) ? key : fallbackText;
        }

        /// <summary>Single-field localization: text IS the key. Machine, then common, then literal.</summary>
        public static string Localize(string keyOrText)
        {
            if (string.IsNullOrEmpty(keyOrText)) return "";
            string translated;
            if (TryLookup(keyOrText, out translated)) return translated;
            return keyOrText;
        }
    }
}

