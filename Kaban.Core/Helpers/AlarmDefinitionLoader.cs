using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Kaban.Core.Enums;
using Kaban.Core.Models;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// RESTORED: loads machine alarm definitions from an Alarms.json file
    /// (e.g. Kaban.Machines.FA2010/Alarms.json) and registers them with
    /// AlarmManager + LanguageManager. The JSON file and the System.Text.Json
    /// package reference already existed in the solution, but the loader code
    /// that consumed them had been removed - nothing ever read the file.
    ///
    /// Expected JSON shape (comments and trailing commas are tolerated):
    /// [
    ///   {
    ///     "PlcAddress": "M801",
    ///     "TagCode": "Clamp_Forward_Timeout",
    ///     "Severity": "Fault",                       // optional: Warning|Fault|Critical
    ///     "PlcNumber": 1,                            // optional, default 1
    ///     "Translations": { "EN": "...", "TR": "..." },
    ///     "TroubleshootingKeys": [ "1.2.1" ],        // see TroubleshootingCatalog
    ///     "HelperKeys": [ ... ]                      // kept for future use
    ///   }, ...
    /// ]
    /// </summary>
    public static class AlarmDefinitionLoader
    {
        public class AlarmJsonEntry
        {
            public string PlcAddress { get; set; }
            public string TagCode { get; set; }
            public string Severity { get; set; }
            public int PlcNumber { get; set; } = 1;
            public Dictionary<string, string> Translations { get; set; }
            public List<string> TroubleshootingKeys { get; set; }
            public List<string> HelperKeys { get; set; }

            /// <summary>
            /// Seconds a cylinder movement may take before the HMI raises this alarm
            /// itself (AlarmGeneration); absent: the machine's general delay.
            /// </summary>
            public double? GenerationDelay { get; set; }

            /// <summary>
            /// The engineer's note ("StateCode 4 / First 32 Errors (1) bit 0").
            /// Every shipped Alarms.json carries one; without this property the
            /// loader dropped it and the first Save erased it from the file.
            /// </summary>
            public string Comment { get; set; }
        }

        private static readonly List<AlarmJsonEntry> _loaded = new List<AlarmJsonEntry>();

        // Module id that each loaded entry was scoped with, parallel to _loaded, so
        // ReapplyTranslations derives the SAME "Alarm_<scopedCode>" key the
        // definition was registered under. Without this the definition and its
        // translation used different keys and every alarm showed as its raw key.
        private static readonly List<string> _loadedModuleIds = new List<string>();

        // File each loaded entry came from, parallel to _loaded. Several machines
        // load into one HMI (FA2010 + H500 + H800), so "the file" is per entry:
        // saving must put each machine's alarms back into ITS OWN Alarms.json.
        private static readonly List<string> _loadedFiles = new List<string>();

        internal static string ScopedCode(string moduleId, string tagCode)
            => string.IsNullOrEmpty(moduleId) ? tagCode : moduleId + "." + tagCode;

        // Optional station remapper, set for the duration of a single Load() call so
        // a mounted sub-machine's alarm PLC numbers land on the host-mapped station.
        private static Func<int, int> _stationRemap = null;

        /// <summary>All entries from the last successful Load(), for troubleshooting screens etc.</summary>
        public static IReadOnlyList<AlarmJsonEntry> LoadedEntries => _loaded;

        /// <summary>
        /// Writes the loaded entries back to the files they came from.
        ///
        /// ONE FILE PER MACHINE. AlarmSettings.cfg stored address, severity and
        /// helper only, so a troubleshooting code had nowhere to go. Alarms.json
        /// already carries all three, so the cfg was a lossy duplicate.
        ///
        /// FIXED: the loader overload every module uses never recorded the path,
        /// so Save always answered "Nothing to save" and no edit ever reached the
        /// disk. Had it recorded one path, the next machine to load would have
        /// overwritten it and Save would have written EVERY machine's alarms into
        /// that one file. Entries were also matched back by ADDRESS, which FA2010,
        /// H500 and H800 share (M801...), so one machine's severity could be
        /// written into another's alarm. Each entry now remembers its own file and
        /// is matched by its scoped code.
        ///
        /// moduleId limits the save to one machine's file(s); null or empty saves
        /// every loaded file. Returns the file(s) written, "; "-separated, or "".
        /// </summary>
        public static string SaveLoadedEntries(string moduleId = null)
        {
            if (_loaded.Count == 0) return "";

            // Group by file, keeping the file's own entry order.
            var byFile = new Dictionary<string, List<AlarmJsonEntry>>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            for (int i = 0; i < _loaded.Count; i++)
            {
                string mod = i < _loadedModuleIds.Count ? _loadedModuleIds[i] : "";
                string file = i < _loadedFiles.Count ? _loadedFiles[i] : "";
                if (string.IsNullOrEmpty(file)) continue;
                if (!string.IsNullOrEmpty(moduleId) &&
                    !string.Equals(mod, moduleId, StringComparison.OrdinalIgnoreCase)) continue;

                var entry = _loaded[i];
                var def = AlarmManager.FindByCode(ScopedCode(mod, entry.TagCode));
                if (def != null)
                {
                    entry.Severity = def.Severity.ToString();
                    entry.TroubleshootingKeys = def.TroubleshootingKeys;
                    entry.HelperKeys = def.HelperKeys;
                    entry.GenerationDelay = def.GenerationDelay;
                }

                List<AlarmJsonEntry> list;
                if (!byFile.TryGetValue(file, out list)) { byFile[file] = list = new List<AlarmJsonEntry>(); order.Add(file); }
                list.Add(entry);
            }
            if (order.Count == 0) return "";

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                // Omit empty lists rather than writing "HelperKeys": null.
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                // Keep Turkish, Russian, German... text readable in the file. The
                // default encoder writes every non-ASCII letter as \uXXXX, which
                // turned "Ошибка" into a line nobody on site can check or edit.
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            foreach (var file in order)
                SafeFile.WriteAllText(file, JsonSerializer.Serialize(byFile[file], options),
                                      new System.Text.UTF8Encoding(true));
            return string.Join("; ", order);
        }

        /// <summary>
        /// An alarm's text in a language (HMI Settings > Translation, 22 October: "Save writes
        /// directly"): put on its loaded entry and on the screen, and written into its machine's
        /// Alarms.json IN PLACE - the entry's "Translations", the rest of the file (its comments,
        /// its layout) as it was. False when no loaded alarm has the key. <paramref name="tagCode"/>
        /// is the entry's (for the project's copy of the file).
        /// </summary>
        public static bool SetTranslation(string key, string lang, string text, out string tagCode)
        {
            var file = SetLoadedTranslation(key, lang, text, out tagCode);
            return !string.IsNullOrEmpty(file) && SetTranslationInFile(file, tagCode, lang, text);
        }

        /// <summary>The Alarms.json an alarm's text key was read from, and its entry's TagCode (null: no loaded alarm has the key).</summary>
        public static string FileOf(string key, out string tagCode)
        {
            tagCode = null;
            if (string.IsNullOrWhiteSpace(key)) return null;
            for (int i = 0; i < _loaded.Count; i++)
            {
                string mod = i < _loadedModuleIds.Count ? _loadedModuleIds[i] : "";
                if (!string.Equals("Alarm_" + ScopedCode(mod, _loaded[i].TagCode), key.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                tagCode = _loaded[i].TagCode;
                return i < _loadedFiles.Count ? _loadedFiles[i] : null;
            }
            return null;
        }

        /// <summary>An Alarms.json's texts: TagCode - language - text (an older installation's, the project's); empty when it cannot be read.</summary>
        public static Dictionary<string, Dictionary<string, string>> TranslationsInFile(string path)
        {
            var all = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return all;
                var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, PropertyNameCaseInsensitive = true };
                var entries = JsonSerializer.Deserialize<List<AlarmJsonEntry>>(File.ReadAllText(path, System.Text.Encoding.UTF8), options);
                foreach (var e in entries ?? new List<AlarmJsonEntry>())
                {
                    if (e == null || string.IsNullOrWhiteSpace(e.TagCode) || e.Translations == null) continue;
                    all[e.TagCode.Trim()] = new Dictionary<string, string>(e.Translations, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the alarm texts of " + path + ": " + ex.Message); }
            return all;
        }

        /// <summary>
        /// An alarm's text changed in the alarms loaded (its key: Alarm_ and the scoped TagCode) - the
        /// Alarms.json it was read from returned for writing (null: no alarm has the key).
        /// </summary>
        public static string SetLoadedTranslation(string key, string lang, string text, out string tagCode)
        {
            tagCode = null;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(lang)) return null;
            lang = lang.Trim().ToUpperInvariant();
            for (int i = 0; i < _loaded.Count; i++)
            {
                string mod = i < _loadedModuleIds.Count ? _loadedModuleIds[i] : "";
                var entry = _loaded[i];
                if (!string.Equals("Alarm_" + ScopedCode(mod, entry.TagCode), key.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                tagCode = entry.TagCode;
                if (entry.Translations == null) entry.Translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                entry.Translations[lang] = text ?? "";
                return i < _loadedFiles.Count ? _loadedFiles[i] ?? "" : "";
            }
            return null;
        }

        /// <summary>An alarm's text for SetTranslationsInFile (Written: put in the file).</summary>
        public sealed class TextItem
        {
            public string TagCode = "", Lang = "", Text = "";
            public bool Written;
        }

        public static bool SetTranslationInFile(string path, string tagCode, string lang, string text)
        {
            var item = new TextItem { TagCode = tagCode, Lang = lang, Text = text };
            SetTranslationsInFile(path, new[] { item });
            return item.Written;
        }

        /// <summary>
        /// An alarm's text written into an Alarms.json in place: the entry of the TagCode, its
        /// "Translations" object, the language's value (added when the entry has none). The rest
        /// of the file - // comments, order, indentation - is left as it was.
        /// </summary>
        /// <remarks>The file read and written once for all the texts (the number written).</remarks>
        public static int SetTranslationsInFile(string path, IList<TextItem> items)
        {
            if (items == null || items.Count == 0 || string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;
            try
            {
                var bytes = File.ReadAllBytes(path);
                bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                var json = System.Text.Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
                int n = 0;
                foreach (var it in items)
                {
                    if (it == null) continue;
                    it.Written = false;
                    if (string.IsNullOrWhiteSpace(it.TagCode) || string.IsNullOrWhiteSpace(it.Lang)) continue;
                    var changed = Edit(json, it.TagCode, it.Lang, it.Text);
                    if (changed == null) continue;
                    json = changed;
                    it.Written = true;
                    n++;
                }
                if (n > 0) SafeFile.WriteAllText(path, json, new System.Text.UTF8Encoding(bom));
                return n;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not write the alarm texts into " + path + ": " + ex.Message);
                foreach (var it in items) if (it != null) it.Written = false;
                return 0;
            }
        }

        /// <summary>The JSON with the entry's text set (null: no entry of the TagCode, or it has no Translations).</summary>
        private static string Edit(string json, string tagCode, string lang, string text)
        {
            {
                var value = JsonSerializer.Serialize(text ?? "", new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                var tag = new System.Text.RegularExpressions.Regex("\"TagCode\"\\s*:\\s*\"" + System.Text.RegularExpressions.Regex.Escape(tagCode) + "\"",
                                                                   System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                // each entry of the list: an object of the top-level array
                foreach (var span in Objects(json, 0, json.Length, 2))
                {
                    var body = json.Substring(span.Key, span.Value - span.Key + 1);
                    if (!tag.IsMatch(body)) continue;
                    var tr = new System.Text.RegularExpressions.Regex("\"Translations\"\\s*:\\s*\\{", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Match(json, span.Key, span.Value - span.Key);
                    if (!tr.Success) return null;
                    int open = tr.Index + tr.Length - 1, close = Matching(json, open);
                    if (close < 0 || close > span.Value) return null;
                    var lm = new System.Text.RegularExpressions.Regex("\"" + System.Text.RegularExpressions.Regex.Escape(lang.Trim()) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
                                                                      System.Text.RegularExpressions.RegexOptions.IgnoreCase).Match(json, open, close - open);
                    string result;
                    if (lm.Success)
                        result = json.Substring(0, lm.Groups[1].Index - 1) + value + json.Substring(lm.Groups[1].Index + lm.Groups[1].Length + 1);
                    else
                    {
                        // a language the entry has no text in: after its last one, as it is laid out
                        int last = close - 1;
                        while (last > open && char.IsWhiteSpace(json[last])) last--;
                        bool empty = last == open;
                        var lineStart = json.LastIndexOf('\n', Math.Max(open, last)) + 1;
                        var indent = new string(json.Skip(lineStart).TakeWhile(ch => ch == ' ' || ch == '\t').ToArray());
                        string nl = json.Contains("\r\n") ? "\r\n" : "\n";
                        var add = (empty ? "" : ",") + (empty ? " " : nl + indent) + "\"" + lang.Trim().ToUpperInvariant() + "\": " + value + (empty ? " " : "");
                        result = json.Substring(0, last + 1) + add + json.Substring(last + 1);
                    }
                    return result;
                }
                return null;
            }
        }

        /// <summary>The objects at a depth (2: the top-level array's entries) between from and to: (open, close) indices.</summary>
        private static IEnumerable<KeyValuePair<int, int>> Objects(string s, int from, int to, int depthWanted)
        {
            int depth = 0, start = -1;
            for (int i = from; i < to; i++)
            {
                char c = s[i];
                if (c == '"') { i = EndOfString(s, i); continue; }
                if (c == '/' && i + 1 < to && s[i + 1] == '/') { int e = s.IndexOf('\n', i); i = e < 0 ? to : e; continue; }
                if (c == '{' || c == '[')
                {
                    depth++;
                    if (c == '{' && depth == depthWanted) start = i;
                }
                else if (c == '}' || c == ']')
                {
                    if (c == '}' && depth == depthWanted && start >= 0) { yield return new KeyValuePair<int, int>(start, i); start = -1; }
                    depth--;
                }
            }
        }

        /// <summary>The brace closing the one at open (strings and // comments skipped); -1: none.</summary>
        private static int Matching(string s, int open)
        {
            int depth = 0;
            for (int i = open; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') { i = EndOfString(s, i); continue; }
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { int e = s.IndexOf('\n', i); if (e < 0) return -1; i = e; continue; }
                if (c == '{') depth++;
                else if (c == '}' && --depth == 0) return i;
            }
            return -1;
        }

        private static int EndOfString(string s, int quote)
        {
            for (int i = quote + 1; i < s.Length; i++)
            {
                if (s[i] == '\\') { i++; continue; }
                if (s[i] == '"') return i;
            }
            return s.Length;
        }

        /// <summary>
        /// Loads alarm definitions from the given JSON file. Returns the number of
        /// alarms registered. Safe to call at startup before MainForm exists.
        /// </summary>
        public static int Load(string filePath)
        {
            return Load(filePath, replaceExisting: true, moduleId: "");
        }

        /// <summary>
        /// Loads alarm definitions, optionally APPENDING to what is already loaded.
        /// Appending is what lets several machine modules (a main machine plus its
        /// mounted sub-machines) each ship their own Alarms.json into one HMI -
        /// the old behaviour always cleared the list, so the last module loaded
        /// silently wiped every earlier machine's alarms.
        ///
        /// When moduleId is supplied, alarm codes are namespaced with it so two
        /// machines can both use e.g. code 1 without colliding.
        /// </summary>
        public static int Load(string filePath, bool replaceExisting, string moduleId)
        {
            return Load(filePath, replaceExisting, moduleId, stationRemap: null);
        }

        /// <summary>
        /// Load overload used when mounting a machine module: namespaces alarm
        /// codes with the module id AND remaps each alarm's PLC number through the
        /// module's station map, so a sub-machine's alarms monitor the physical
        /// station it was mounted onto.
        /// </summary>
        public static int Load(string filePath, bool replaceExisting, string moduleId, Func<int, int> stationRemap)
        {
            _stationRemap = stationRemap;
            try
            {
                return LoadInternal(filePath, replaceExisting, moduleId);
            }
            finally
            {
                _stationRemap = null;
            }
        }

        private static int LoadInternal(string filePath, bool replaceExisting, string moduleId)
        {
            if (replaceExisting) { _loaded.Clear(); _loadedModuleIds.Clear(); _loadedFiles.Clear(); }

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                AuditLogger.Log($"alarm definition file not found: '{filePath}'.");
                return 0;
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip, // the shipped file contains // comments
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true
                };

                string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                var entries = JsonSerializer.Deserialize<List<AlarmJsonEntry>>(json, options);
                if (entries == null) return 0;

                int count = 0;
                foreach (var entry in entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.PlcAddress) || string.IsNullOrWhiteSpace(entry.TagCode))
                        continue;

                    AlarmSeverity severity = AlarmSeverity.Fault;
                    if (!string.IsNullOrEmpty(entry.Severity))
                        Enum.TryParse(entry.Severity, true, out severity);

                    // NAMESPACE by module so two mounted machines can each use the
                    // same TagCode (e.g. "1") without the second overwriting the
                    // first in AlarmManager's code-keyed dictionary. Without this the
                    // moduleId argument was accepted but ignored.
                    string scopedCode = ScopedCode(moduleId, entry.TagCode);

                    // Translation key follows the (scoped) code, so a sub-machine's
                    // alarm text can't collide with the host's either.
                    string translationKey = "Alarm_" + scopedCode;

                    // Remap the alarm's PLC number through the station map, so a
                    // sub-machine's "PLC 1" alarm monitors the physical station it
                    // was mounted onto - not the host's PLC 1.
                    int plc = _stationRemap != null ? _stationRemap(entry.PlcNumber) : entry.PlcNumber;

                    AlarmManager.RegisterDefinition(
                        alarmCode: scopedCode,
                        translationKey: translationKey,
                        severity: severity,
                        plcNumber: plc,
                        address: entry.PlcAddress,
                        // These were read from the file and then dropped - the
                        // comment above even said "kept for future use". The Help
                        // column is that use.
                        troubleshootingKeys: entry.TroubleshootingKeys,
                        helperKeys: entry.HelperKeys);
                    var registered = AlarmManager.FindByCode(scopedCode);
                    if (registered != null) registered.GenerationDelay = entry.GenerationDelay;

                    _loaded.Add(entry);
                    _loadedModuleIds.Add(moduleId ?? "");
                    _loadedFiles.Add(Path.GetFullPath(filePath));
                    count++;
                }

                ReapplyTranslations();
                AuditLogger.Log($"loaded {count} alarm definitions from '{Path.GetFileName(filePath)}'.");
                return count;
            }
            catch (Exception ex)
            {
                AuditLogger.Log($"FAILED to load alarm definitions from '{filePath}': {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Re-registers the JSON translations with LanguageManager. Needed because
        /// LanguageManager.LoadTranslations() (called later, in MainForm_Load)
        /// clears the whole dictionary - so MainForm calls this again afterwards.
        /// </summary>
        public static void ReapplyTranslations()
        {
            for (int i = 0; i < _loaded.Count; i++)
            {
                var entry = _loaded[i];
                if (entry.Translations == null) continue;
                string moduleId = i < _loadedModuleIds.Count ? _loadedModuleIds[i] : "";
                string translationKey = "Alarm_" + ScopedCode(moduleId, entry.TagCode);

                foreach (var kvp in entry.Translations)
                {
                    // listed on HMI Settings > Translation as its machine's (its Alarms.json)
                    LanguageManager.AddTranslation(kvp.Key, translationKey, kvp.Value, moduleId ?? "", "Alarms.json");
                }
            }
        }
    }
}

