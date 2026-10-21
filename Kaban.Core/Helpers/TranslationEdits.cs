using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Kaban.Core.Machines;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// THE TRANSLATIONS EDITED ON THE PANEL (HMI Settings > Translation, 21 October: "a table
    /// of all the translations for Maintenance and above, with what each is owned by - the
    /// general application or a machine - the key and all the languages; a language added or
    /// deleted; and the edit history kept, so I can check the user edited ones and put them in
    /// my translation files when I find them useful").
    ///
    /// The translation files are not written: an edit is kept beside them, in the program's
    /// data folder (an update of the program replaces its files, not the edits), and put in
    /// force over them at every start:
    ///
    ///     Translations\UserTranslations.csv     Owner;Key;Lang;Text;User;Time  - the edits in force
    ///     Translations\Languages.cfg            +FR / -RU                      - languages added, deleted
    ///     Translations\TranslationHistory.csv   Time;User;Owner;Key;Lang;Old;New - every change, oldest first
    ///
    /// The owner is "General" for the common file (CommonTranslations.csv), a machine's id
    /// for its own files (its Translations.csv, its Alarms.json).
    /// </summary>
    public static class TranslationEdits
    {
        /// <summary>The common file's owner.</summary>
        public const string General = "General";

        /// <summary>Where the edits are kept (replaceable for the tests).</summary>
        public static string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Translations");

        public static string EditsFile { get { return Path.Combine(Folder, "UserTranslations.csv"); } }
        public static string LanguagesFile { get { return Path.Combine(Folder, "Languages.cfg"); } }
        public static string HistoryFile { get { return Path.Combine(Folder, "TranslationHistory.csv"); } }

        /// <summary>A text put in force on the panel.</summary>
        public sealed class Edit
        {
            public string Owner = "", Key = "", Lang = "", Text = "", User = "";
            public DateTime Time;
        }

        /// <summary>One change, as the history keeps it (a language added or deleted: Key "", Old/New "added"/"deleted").</summary>
        public sealed class Change
        {
            public DateTime Time;
            public string User = "", Owner = "", Key = "", Lang = "", Old = "", New = "";
        }

        /// <summary>A key of one owner, as its file has it.</summary>
        public sealed class Row
        {
            public string Key = "", Owner = "", File = "";
            public bool Common;
            /// <summary>language - the file's text.</summary>
            public readonly Dictionary<string, string> FileTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private static readonly object _lock = new object();
        private static List<Edit> _edits;
        private static List<string> _added, _deleted;
        private static string _loadedFrom;

        // ---- the table -------------------------------------------------------------------

        /// <summary>Whose a file's texts are: General for the common one; a machine's id - the mounted machine whose folder it is in, else the main machine.</summary>
        public static string OwnerOf(LanguageManager.Source s)
        {
            if (s == null) return "";
            if (s.Common) return General;
            if (!string.IsNullOrEmpty(s.Owner)) return s.Owner;
            string dir = "";
            try { dir = Path.GetFileName(Path.GetDirectoryName(s.Path) ?? "") ?? ""; } catch { }
            foreach (var m in MachineRegistry.Modules)
                if (m.Context != null && string.Equals(m.Context.ModuleId, dir, StringComparison.OrdinalIgnoreCase)) return m.Context.ModuleId;
            var main = MachineRegistry.Main;
            return main != null && main.Context != null && !string.IsNullOrEmpty(main.Context.ModuleId) ? main.Context.ModuleId : "Machine";
        }

        /// <summary>Every key of every file read, with its owner - general ones first, each file's keys in its order.</summary>
        public static List<Row> Rows()
        {
            var rows = new List<Row>();
            var at = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in LanguageManager.Sources)
            {
                string owner = OwnerOf(s);
                foreach (var key in s.Keys)
                {
                    Row row;
                    string id = owner + "\u0001" + key;
                    if (!at.TryGetValue(id, out row))
                    {
                        row = new Row { Key = key, Owner = owner, File = s.FileName, Common = s.Common };
                        at[id] = row;
                        rows.Add(row);
                    }
                    Dictionary<string, string> texts;
                    if (!s.Texts.TryGetValue(key, out texts)) continue;
                    foreach (var kv in texts)
                        if (!row.FileTexts.ContainsKey(kv.Key) || string.IsNullOrEmpty(row.FileTexts[kv.Key])) row.FileTexts[kv.Key] = kv.Value ?? "";
                }
            }
            return rows;
        }

        /// <summary>The languages: English first, the files' in their order, the ones added - none deleted.</summary>
        public static List<string> Languages()
        {
            var list = new List<string> { "EN" };
            foreach (var s in LanguageManager.Sources)
                foreach (var l in s.Languages)
                    if (!list.Contains(l, StringComparer.OrdinalIgnoreCase)) list.Add(l.ToUpperInvariant());
            foreach (var l in AddedLanguages)
                if (!list.Contains(l, StringComparer.OrdinalIgnoreCase)) list.Add(l);
            var deleted = DeletedLanguages.ToList();
            return list.Where(l => l == "EN" || !deleted.Contains(l, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        // ---- the edits -------------------------------------------------------------------

        public static IList<Edit> Edits { get { lock (_lock) { EnsureLoaded(); return _edits.ToList().AsReadOnly(); } } }

        public static IEnumerable<string> AddedLanguages { get { lock (_lock) { EnsureLoaded(); return _added.ToList(); } } }
        public static IEnumerable<string> DeletedLanguages { get { lock (_lock) { EnsureLoaded(); return _deleted.ToList(); } } }

        /// <summary>The edit in force for a key's text in a language (null: the file's text).</summary>
        public static Edit EditOf(string owner, string key, string lang)
        {
            lock (_lock)
            {
                EnsureLoaded();
                return _edits.FirstOrDefault(e => Same(e, owner, key, lang));
            }
        }

        private static bool Same(Edit e, string owner, string key, string lang)
        {
            return string.Equals(e.Owner, owner ?? "", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(e.Key, key ?? "", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(e.Lang, lang ?? "", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A text as it is in force: the edit, else the file's.</summary>
        public static string TextOf(Row row, string lang)
        {
            if (row == null) return "";
            var e = EditOf(row.Owner, row.Key, lang);
            if (e != null) return e.Text;
            string t;
            return row.FileTexts.TryGetValue(lang ?? "", out t) ? t ?? "" : "";
        }

        /// <summary>A change asked for: a key's text in a language.</summary>
        public sealed class Wanted
        {
            public Row Row;
            public string Lang = "", Text = "";
        }

        /// <summary>
        /// Puts the texts in force, keeps them, and writes each change in the history (who,
        /// when, the text before and after). A text made the file's again is no edit any more.
        /// The number of texts changed.
        /// </summary>
        public static int Save(IEnumerable<Wanted> wanted, string user = null)
        {
            user = string.IsNullOrEmpty(user) ? CurrentUser() : user;
            var now = DateTime.Now;
            var history = new List<Change>();
            lock (_lock)
            {
                EnsureLoaded();
                foreach (var w in wanted ?? Enumerable.Empty<Wanted>())
                {
                    if (w == null || w.Row == null || string.IsNullOrEmpty(w.Lang)) continue;
                    string lang = w.Lang.Trim().ToUpperInvariant();
                    string text = Clean(w.Text);
                    string old = TextOf(w.Row, lang);
                    if (old == text) continue;
                    string fileText;
                    w.Row.FileTexts.TryGetValue(lang, out fileText);
                    _edits.RemoveAll(e => Same(e, w.Row.Owner, w.Row.Key, lang));
                    if (text != (fileText ?? ""))
                        _edits.Add(new Edit { Owner = w.Row.Owner, Key = w.Row.Key, Lang = lang, Text = text, User = user, Time = now });
                    LanguageManager.SetText(lang, w.Row.Key, text, w.Row.Common);
                    history.Add(new Change { Time = now, User = user, Owner = w.Row.Owner, Key = w.Row.Key, Lang = lang, Old = old, New = text });
                }
                if (history.Count == 0) return 0;
                WriteEdits();
                AppendHistory(history);
            }
            LanguageManager.RaiseLanguageChanged();
            AuditLogger.Log("translations changed on the panel: " + history.Count + " text(s) (" +
                            string.Join(", ", history.Take(5).Select(h => h.Key + " " + h.Lang)) + (history.Count > 5 ? " ..." : "") + ").");
            return history.Count;
        }

        /// <summary>A language offered (a new one: its texts English until given; a deleted one: back with its files' texts).</summary>
        public static void AddLanguage(string code, string user = null)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) return;
            lock (_lock)
            {
                EnsureLoaded();
                bool wasDeleted = _deleted.Remove(code);
                if (!wasDeleted && !_added.Contains(code) && !LanguageManager.Sources.Any(s => s.Languages.Contains(code, StringComparer.OrdinalIgnoreCase)))
                    _added.Add(code);
                WriteLanguages();
                AppendHistory(new[] { new Change { Time = DateTime.Now, User = string.IsNullOrEmpty(user) ? CurrentUser() : user, Lang = code, New = "added" } });
            }
            LanguageManager.AddLanguage(code);
            AuditLogger.Log("language " + code + " added on the panel.");
        }

        /// <summary>A language no longer offered (not English). Its texts stay in the files - and its edits here - for when it is added again.</summary>
        public static bool RemoveLanguage(string code, string user = null)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0 || code == "EN") return false;
            lock (_lock)
            {
                EnsureLoaded();
                if (_added.Remove(code)) { }
                else if (!_deleted.Contains(code)) _deleted.Add(code);
                WriteLanguages();
                AppendHistory(new[] { new Change { Time = DateTime.Now, User = string.IsNullOrEmpty(user) ? CurrentUser() : user, Lang = code, New = "deleted" } });
            }
            LanguageManager.RemoveLanguage(code);
            AuditLogger.Log("language " + code + " deleted on the panel.");
            return true;
        }

        /// <summary>
        /// At the start, after the translation files (and the alarms', the notes'): the
        /// languages added and deleted, the edits in force.
        /// </summary>
        public static void Apply()
        {
            List<Edit> edits;
            List<string> added, deleted;
            lock (_lock) { EnsureLoaded(); edits = _edits.ToList(); added = _added.ToList(); deleted = _deleted.ToList(); }
            foreach (var l in added) LanguageManager.AddLanguage(l);
            foreach (var l in deleted) LanguageManager.RemoveLanguage(l);
            foreach (var e in edits) LanguageManager.SetText(e.Lang, e.Key, e.Text, string.Equals(e.Owner, General, StringComparison.OrdinalIgnoreCase));
            if (edits.Count > 0) LanguageManager.RaiseLanguageChanged();
        }

        /// <summary>Every change kept, oldest first.</summary>
        public static List<Change> History()
        {
            var list = new List<Change>();
            try
            {
                if (!File.Exists(HistoryFile)) return list;
                foreach (var line in File.ReadAllLines(HistoryFile, Encoding.UTF8).Skip(1))
                {
                    var f = line.Split(';');
                    if (f.Length < 7) continue;
                    DateTime t;
                    DateTime.TryParseExact(f[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t);
                    list.Add(new Change { Time = t, User = f[1], Owner = f[2], Key = f[3], Lang = f[4], Old = Unescape(f[5]), New = Unescape(string.Join(";", f.Skip(6))) });
                }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the translation history: " + ex.Message); }
            return list;
        }

        /// <summary>Reads the files again (the tests).</summary>
        public static void Reload() { lock (_lock) _edits = null; }

        // ---- the files -------------------------------------------------------------------

        private static void EnsureLoaded()
        {
            if (_edits != null && string.Equals(_loadedFrom, Folder, StringComparison.OrdinalIgnoreCase)) return;
            _loadedFrom = Folder;
            _edits = new List<Edit>();
            _added = new List<string>();
            _deleted = new List<string>();
            try
            {
                if (File.Exists(EditsFile))
                    foreach (var line in File.ReadAllLines(EditsFile, Encoding.UTF8).Skip(1))
                    {
                        var f = line.Split(';');
                        if (f.Length < 4 || f[1].Trim().Length == 0) continue;
                        DateTime t = DateTime.MinValue;
                        if (f.Length > 5) DateTime.TryParseExact(f[5], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t);
                        _edits.Add(new Edit { Owner = f[0].Trim(), Key = f[1].Trim(), Lang = f[2].Trim().ToUpperInvariant(), Text = Unescape(f[3]),
                                              User = f.Length > 4 ? f[4] : "", Time = t });
                    }
                if (File.Exists(LanguagesFile))
                    foreach (var raw in File.ReadAllLines(LanguagesFile, Encoding.UTF8))
                    {
                        var line = raw.Trim().TrimStart('﻿');
                        if (line.Length < 2 || line.StartsWith("#")) continue;
                        var code = line.Substring(1).Trim().ToUpperInvariant();
                        if (line[0] == '+' && !_added.Contains(code)) _added.Add(code);
                        if (line[0] == '-' && !_deleted.Contains(code)) _deleted.Add(code);
                    }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the translation edits: " + ex.Message); }
        }

        private static void WriteEdits()
        {
            var lines = new List<string> { "Owner;Key;Lang;Text;User;Time" };
            foreach (var e in _edits.OrderBy(e => e.Owner, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Key, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Lang))
                lines.Add(string.Join(";", e.Owner, e.Key, e.Lang, Escape(e.Text), Clean(e.User), e.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(Folder);
            SafeFile.WriteAllText(EditsFile, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(true));
        }

        private static void WriteLanguages()
        {
            var lines = new List<string> { "# Kaban HMI: languages added (+) and deleted (-) on the panel (HMI Settings > Translation)" };
            lines.AddRange(_added.Select(l => "+" + l));
            lines.AddRange(_deleted.Select(l => "-" + l));
            Directory.CreateDirectory(Folder);
            SafeFile.WriteAllText(LanguagesFile, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(true));
        }

        private static void AppendHistory(IEnumerable<Change> changes)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var sb = new StringBuilder();
                bool fresh = !File.Exists(HistoryFile);
                if (fresh) sb.Append("Time;User;Owner;Key;Lang;Old;New\r\n");
                foreach (var c in changes)
                    sb.Append(string.Join(";", c.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), Clean(c.User), c.Owner, c.Key, c.Lang,
                                          Escape(c.Old), Escape(c.New))).Append("\r\n");
                File.AppendAllText(HistoryFile, sb.ToString(), fresh ? new UTF8Encoding(true) : new UTF8Encoding(false));
            }
            catch (Exception ex) { AuditLogger.Log("could not write the translation history: " + ex.Message); }
        }

        /// <summary>A text as the files keep it: no ";" (the column separator), line breaks as \r\n.</summary>
        public static string Clean(string text) { return (text ?? "").Replace(";", ",").Trim(); }

        private static string Escape(string text) { return Clean(text).Replace("\r\n", "\\r\\n").Replace("\n", "\\n"); }

        private static string Unescape(string text) { return (text ?? "").Replace("\\r\\n", "\r\n").Replace("\\n", "\n"); }

        private static string CurrentUser()
        {
            try { return string.IsNullOrEmpty(SecurityManager.CurrentUser) ? "?" : SecurityManager.CurrentUser; } catch { return "?"; }
        }
    }
}
