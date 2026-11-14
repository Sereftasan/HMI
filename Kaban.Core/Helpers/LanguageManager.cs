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

        /// <summary>A language was added or deleted (HMI Settings > Translation): the language lists again.</summary>
        public static event EventHandler LanguagesChanged;

        // ---- where the texts come from (HMI Settings > Translation, 21 October) ----------

        /// <summary>
        /// One file (or the alarms of one machine) the texts were read from, as read: the
        /// Translation page lists every key of every one, with whose it is.
        /// </summary>
        public sealed class Source
        {
            /// <summary>The file read ("" for texts registered while running).</summary>
            public string Path = "";
            /// <summary>The common (general) layer; otherwise a machine's.</summary>
            public bool Common;
            /// <summary>Whose they are when the registration said so (an alarm's machine); else made from Path.</summary>
            public string Owner = "";
            /// <summary>What it is: "CommonTranslations.csv", "Translations.csv", "Alarms.json".</summary>
            public string FileName = "";
            public readonly List<string> Languages = new List<string>();
            /// <summary>The keys in the file's order.</summary>
            public readonly List<string> Keys = new List<string>();
            /// <summary>key - language - text.</summary>
            public readonly Dictionary<string, Dictionary<string, string>> Texts =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            internal void Put(string key, string lang, string text)
            {
                Dictionary<string, string> row;
                if (!Texts.TryGetValue(key, out row))
                {
                    row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    Texts[key] = row;
                    Keys.Add(key);
                }
                row[lang] = text;
                if (!Languages.Contains(lang)) Languages.Add(lang);
            }
        }

        private static readonly List<Source> _sources = new List<Source>();

        /// <summary>Every file the texts were read from, common ones first, in the order read.</summary>
        public static IList<Source> Sources
        {
            get { lock (_sources) return _sources.OrderByDescending(s => s.Common).ToList().AsReadOnly(); }
        }

        /// <summary>The languages deleted (HMI Settings > Translation): not offered, though a file has them.</summary>
        private static readonly HashSet<string> _deleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static Source SourceFor(string path, bool common, bool fresh)
        {
            lock (_sources)
            {
                var full = string.IsNullOrEmpty(path) ? "" : SafeFull(path);
                _sources.RemoveAll(s => s.Common == common && fresh && full.Length > 0 && string.Equals(s.Path, full, StringComparison.OrdinalIgnoreCase));
                var src = new Source { Path = full, Common = common, FileName = full.Length > 0 ? System.IO.Path.GetFileName(full) : "" };
                _sources.Add(src);
                return src;
            }
        }

        private static string SafeFull(string path)
        {
            try { return System.IO.Path.GetFullPath(path); } catch { return path; }
        }

        /// <summary>
        /// Loads the COMMON (shared) translation file - plant-wide texts used by
        /// every machine (Start/Stop, confirm dialogs, unit names, etc.).
        /// </summary>
        public static void LoadCommonTranslations(string filePath)
        {
            LoadInto(_common, filePath, clearFirst: true);
        }

        private static void ForgetSources(bool common)
        {
            // the machines' alarm texts go with them: the alarm loader registers them again
            lock (_sources) _sources.RemoveAll(s => s.Common == common);
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
                    if (clearFirst) ForgetSources(true);
                    var src = SourceFor("", true, false);
                    src.FileName = "CommonTranslations.csv";
                    ParseInto(_common, all.ToArray(), clearFirst, src);
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
                bool common = ReferenceEquals(target, _common);
                if (clearFirst) ForgetSources(common);
                ParseInto(target, lines, clearFirst, SourceFor(filePath, common, true));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Translation Load Error ({filePath}): {ex.Message}");
            }
        }

        private static void ParseInto(Dictionary<string, Dictionary<string, string>> target, string[] lines, bool clearFirst, Source source = null)
        {
            if (clearFirst) target.Clear();

            try
            {
                if (lines.Length == 0) return;

                var headers = lines[0].Split(';');
                var langs = new List<string>();
                for (int i = 1; i < headers.Length; i++)
                {
                    string langCode = headers[i].Trim().ToUpperInvariant();
                    langs.Add(langCode);
                    if (!target.ContainsKey(langCode))
                        target[langCode] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (!AvailableLanguages.Contains(langCode) && !_deleted.Contains(langCode) && langCode.Length > 0)
                        AvailableLanguages.Add(langCode);
                    if (source != null && langCode.Length > 0 && !source.Languages.Contains(langCode)) source.Languages.Add(langCode);
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
                    {
                        var text = columns[c].Trim().Replace("\\r\\n", "\r\n").Replace("\\n", "\n");
                        target[langs[c - 1]][key] = text;
                        if (source != null) source.Put(key, langs[c - 1], text);
                    }
                    if (source != null && !source.Texts.ContainsKey(key)) source.Put(key, langs.Count > 0 ? langs[0] : "EN", "");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Translation parse error: {ex.Message}");
            }
        }

        /// <summary>
        /// True when a machine's translation FILE has a text of the key in the language (an alarm's
        /// text in its Translations.csv - an older Alarms.json's is not put over it, 30 October).
        /// </summary>
        public static bool InMachineFile(string key, string langCode)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(langCode)) return false;
            lock (_sources)
                foreach (var s in _sources)
                {
                    Dictionary<string, string> row;
                    string t;
                    if (!s.Common && s.Path.Length > 0 && s.Texts.TryGetValue(key, out row) && row.TryGetValue(langCode.Trim(), out t) && !string.IsNullOrEmpty(t))
                        return true;
                }
            return false;
        }

        /// <summary>Runtime single-key registration (AlarmDefinitionLoader). Machine layer.</summary>
        /// <param name="owner">Whose text it is (an alarm's machine) - listed on the Translation page; null: not listed (a note's text).</param>
        /// <param name="fileName">Where it is kept ("Alarms.json").</param>
        public static void AddTranslation(string langCode, string key, string text, string owner = null, string fileName = null)
        {
            if (string.IsNullOrWhiteSpace(langCode) || string.IsNullOrWhiteSpace(key)) return;
            langCode = langCode.Trim().ToUpperInvariant();
            if (!_machine.ContainsKey(langCode))
            {
                _machine[langCode] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!AvailableLanguages.Contains(langCode) && !_deleted.Contains(langCode)) AvailableLanguages.Add(langCode);
            }
            _machine[langCode][key] = text;
            if (owner == null) return;
            lock (_sources)
            {
                var src = _sources.FirstOrDefault(s => !s.Common && s.Path.Length == 0 && string.Equals(s.Owner, owner, StringComparison.OrdinalIgnoreCase) &&
                                                       string.Equals(s.FileName, fileName ?? "", StringComparison.OrdinalIgnoreCase));
                if (src == null) { src = new Source { Owner = owner, FileName = fileName ?? "" }; _sources.Add(src); }
                src.Put(key, langCode, text ?? "");
            }
        }

        /// <summary>
        /// A text put in force (HMI Settings > Translation): into the common layer or the
        /// machines', as the text it replaces was; blank: the file's text again.
        /// </summary>
        public static void SetText(string langCode, string key, string text, bool common)
        {
            if (string.IsNullOrWhiteSpace(langCode) || string.IsNullOrWhiteSpace(key)) return;
            langCode = langCode.Trim().ToUpperInvariant();
            var layer = common ? _common : _machine;
            Dictionary<string, string> dict;
            if (!layer.TryGetValue(langCode, out dict))
            {
                dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                layer[langCode] = dict;
            }
            dict[key] = text ?? "";
        }

        /// <summary>A language offered again, or for the first time (its texts: English until given).</summary>
        public static void AddLanguage(string langCode)
        {
            if (string.IsNullOrWhiteSpace(langCode)) return;
            langCode = langCode.Trim().ToUpperInvariant();
            _deleted.Remove(langCode);
            if (!_common.ContainsKey(langCode)) _common[langCode] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!AvailableLanguages.Contains(langCode)) AvailableLanguages.Add(langCode);
            LanguagesChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>A language no longer offered (English stays: it is every text's last resort); the screen in English when it was shown.</summary>
        public static void RemoveLanguage(string langCode)
        {
            if (string.IsNullOrWhiteSpace(langCode)) return;
            langCode = langCode.Trim().ToUpperInvariant();
            if (langCode == "EN") return;
            _deleted.Add(langCode);
            AvailableLanguages.RemoveAll(l => string.Equals(l, langCode, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(CurrentLanguage, langCode, StringComparison.OrdinalIgnoreCase)) ChangeLanguage("EN");
            LanguagesChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>A deleted language (its texts still in the files).</summary>
        public static bool IsDeleted(string langCode) { return !string.IsNullOrEmpty(langCode) && _deleted.Contains(langCode.Trim()); }

        /// <summary>Tells every page the texts changed (a translation put in force while the program runs).</summary>
        public static void RaiseLanguageChanged()
        {
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void ChangeLanguage(string langCode)
        {
            langCode = langCode.ToUpperInvariant();
            if (_deleted.Contains(langCode)) return;
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

        /// <summary>The key whose text in <paramref name="lang"/> is <paramref name="text"/> (the machine's keys first); null: none.</summary>
        public static string KeyOfTextIn(string text, string lang)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(lang)) return null;
            text = text.Trim();
            lang = lang.Trim().ToUpperInvariant();
            foreach (var source in new[] { _machine, _common })
            {
                Dictionary<string, string> dict;
                if (!source.TryGetValue(lang, out dict)) continue;
                foreach (var kv in dict)
                    if (string.Equals((kv.Value ?? "").Trim(), text, StringComparison.Ordinal)) return kv.Key;
            }
            return null;
        }

        /// <summary>A key's text in one language (machine, then common), or null - no English in its place.</summary>
        public static string GetStringIn(string langCode, string key)
        {
            string value;
            return !string.IsNullOrEmpty(langCode) && TryLookupIn(langCode.Trim().ToUpperInvariant(), key, out value) ? value : null;
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

