using System;
using System.Collections.Generic;
using System.IO;
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
                    LanguageManager.AddTranslation(kvp.Key, translationKey, kvp.Value);
                }
            }
        }
    }
}

