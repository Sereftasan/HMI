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
    /// deleted; and the edit history kept, so I can check the user edited ones").
    ///
    /// 22 October: "Save writes the edits directly into the translation files": a text saved
    /// is written into the file it was read from - the common CommonTranslations.csv, a
    /// machine's Translations.csv (its line of the key, its language's column; a language new
    /// to the file gets its column) or its Alarms.json (the alarm's Translations) - and, when the
    /// program runs from its Visual Studio build, into the project's copy of that file too, so
    /// it is kept with the source. Every change is written in the history (who, when, before,
    /// after); a key is never added (only the files' keys are edited).
    ///
    ///     Translations\TranslationHistory.csv   Time;User;Owner;Key;Lang;Old;New - every change, oldest first
    ///     Translations\Languages.cfg            +FR / -RU                      - languages added, deleted
    ///     (Translations\UserTranslations.csv: the first version's edits, written into the files once)
    ///
    /// The owner is "General" for the common file, a machine's id for its own files.
    /// </summary>
    public static class TranslationEdits
    {
        /// <summary>The common file's owner.</summary>
        public const string General = "General";

        /// <summary>Where the history and the languages are kept (replaceable for the tests).</summary>
        public static string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Translations");

        public static string EditsFile { get { return Path.Combine(Folder, "UserTranslations.csv"); } }
        public static string LanguagesFile { get { return Path.Combine(Folder, "Languages.cfg"); } }
        public static string HistoryFile { get { return Path.Combine(Folder, "TranslationHistory.csv"); } }

        /// <summary>
        /// The project's copy of a row's file when the program runs from its build (null: none).
        /// Replaceable for the tests.
        /// </summary>
        public static Func<Row, string> ProjectFileOf = DefaultProjectFile;

        /// <summary>A text edited on the panel, as the history last has it.</summary>
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
            /// <summary>The file it was read from ("" for an alarm's text: its Alarms.json, kept by the alarm loader).</summary>
            public string Path = "";
            public bool Common;
            /// <summary>language - the file's text.</summary>
            public readonly Dictionary<string, string> FileTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            internal readonly List<LanguageManager.Source> From = new List<LanguageManager.Source>();
        }

        private static readonly object _lock = new object();
        private static List<string> _added, _deleted;
        private static Dictionary<string, string> _names;
        private static string _loadedFrom;

        // ---- the table -------------------------------------------------------------------

        /// <summary>Whose a file's texts are: General for the common one; a machine's id - the mounted machine whose folder it is in, else the main machine.</summary>
        public static string OwnerOf(LanguageManager.Source s)
        {
            if (s == null) return "";
            if (s.Common) return General;
            if (!string.IsNullOrEmpty(s.Owner)) return s.Owner;
            string dir = "";
            try { dir = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(s.Path) ?? "") ?? ""; } catch { }
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
                        row = new Row { Key = key, Owner = owner, File = s.FileName, Common = s.Common, Path = s.Path };
                        at[id] = row;
                        rows.Add(row);
                    }
                    if (!row.From.Contains(s)) row.From.Add(s);
                    Dictionary<string, string> texts;
                    if (!s.Texts.TryGetValue(key, out texts)) continue;
                    foreach (var kv in texts)
                        if (!row.FileTexts.ContainsKey(kv.Key) || string.IsNullOrEmpty(row.FileTexts[kv.Key])) row.FileTexts[kv.Key] = kv.Value ?? "";
                }
            }
            return rows;
        }

        /// <summary>
        /// The files' records a row's key is in, as read NOW (a row kept by a table open while the
        /// files were read again has the earlier ones too) - a text saved is put in all of them.
        /// </summary>
        private static IEnumerable<LanguageManager.Source> SourcesOf(Row row)
        {
            var list = new List<LanguageManager.Source>(row.From);
            foreach (var s in LanguageManager.Sources)
                if (!list.Contains(s) && string.Equals(OwnerOf(s), row.Owner, StringComparison.OrdinalIgnoreCase) &&
                    (s.Texts.ContainsKey(row.Key) || s.Keys.Contains(row.Key, StringComparer.OrdinalIgnoreCase)))
                {
                    list.Add(s);
                    row.From.Add(s);
                }
            return list;
        }

        /// <summary>The rows of a key, the machine's first (the text on the screen is the machine's).</summary>
        public static List<Row> RowsOf(string key)
        {
            return Rows().Where(r => string.Equals(r.Key, (key ?? "").Trim(), StringComparison.OrdinalIgnoreCase)).OrderBy(r => r.Common ? 1 : 0).ToList();
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

        // ---- what was edited here ---------------------------------------------------------

        /// <summary>Every text edited on the panel - the history's last change of each (owner, key, language).</summary>
        public static IList<Edit> Edits
        {
            get
            {
                var last = new Dictionary<string, Edit>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in History().Where(c => c.Key.Length > 0))
                    last[c.Owner + "\u0001" + c.Key + "\u0001" + c.Lang] = new Edit { Owner = c.Owner, Key = c.Key, Lang = c.Lang, Text = c.New, User = c.User, Time = c.Time };
                return last.Values.ToList().AsReadOnly();
            }
        }

        public static IEnumerable<string> AddedLanguages { get { lock (_lock) { EnsureLoaded(); return _added.ToList(); } } }
        public static IEnumerable<string> DeletedLanguages { get { lock (_lock) { EnsureLoaded(); return _deleted.ToList(); } } }

        /// <summary>A text as the files have it now.</summary>
        public static string TextOf(Row row, string lang)
        {
            if (row == null) return "";
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
        /// Puts the texts in force and writes them into their files (and the project's copies),
        /// each change in the history (who, when, the text before and after). The number of texts
        /// changed; <paramref name="notWritten"/> the ones no file could take (put in force only).
        /// </summary>
        public static int Save(IEnumerable<Wanted> wanted, string user = null, bool raise = true)
        {
            int notWritten;
            return Save(wanted, out notWritten, user, raise);
        }

        /// <param name="done">Given: the changes made are added to it (the translation page's undo keeps them).</param>
        public static int Save(IEnumerable<Wanted> wanted, out int notWritten, string user = null, bool raise = true, List<Change> done = null)
        {
            notWritten = 0;
            user = string.IsNullOrEmpty(user) ? CurrentUser() : user;
            var now = DateTime.Now;
            var history = new List<Change>();
            lock (_lock)
            {
                var writes = new Writes();
                foreach (var w in wanted ?? Enumerable.Empty<Wanted>())
                {
                    if (w == null || w.Row == null || string.IsNullOrEmpty(w.Lang)) continue;
                    string lang = w.Lang.Trim().ToUpperInvariant();
                    string text = Clean(w.Text);
                    string old = TextOf(w.Row, lang);
                    if (old == text) continue;
                    writes.Add(w.Row, lang, text);
                    w.Row.FileTexts[lang] = text;
                    foreach (var s in SourcesOf(w.Row)) s.Put(w.Row.Key, lang, text);
                    LanguageManager.SetText(lang, w.Row.Key, text, w.Row.Common);
                    history.Add(new Change { Time = now, User = user, Owner = w.Row.Owner, Key = w.Row.Key, Lang = lang, Old = old, New = text });
                }
                if (history.Count == 0) return 0;
                notWritten = writes.Flush().Count(ok => !ok);
                AppendHistory(history);
                if (done != null) done.AddRange(history);
            }
            if (raise) LanguageManager.RaiseLanguageChanged();
            AuditLogger.Log("translations changed on the panel: " + history.Count + " text(s) written into the translation files (" +
                            string.Join(", ", history.Take(5).Select(h => h.Key + " " + h.Lang)) + (history.Count > 5 ? " ..." : "") + ")" +
                            (notWritten > 0 ? ", " + notWritten + " in force only (no file to write)" : "") + ".");
            return history.Count;
        }

        /// <summary>A row's text in a language written into its file (false: no file could take it).</summary>
        private static bool Write(Row row, string lang, string text)
        {
            var w = new Writes();
            w.Add(row, lang, text);
            return w.Flush()[0];
        }

        /// <summary>The program's folder - where its translation files are (the tests: theirs).</summary>
        public static string ProgramFolder = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// The file a row's texts are in, and an alarm's TagCode (its entry in that Alarms.json):
        /// the file it was read from (the common texts read from the program's own copy: the one
        /// beside the program). Null: none known.
        /// </summary>
        public static string FileOf(Row row, out string tagCode)
        {
            tagCode = null;
            if (row == null) return null;
            if (string.Equals(row.File, "Alarms.json", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(row.Path))
                return AlarmDefinitionLoader.FileOf(row.Key, out tagCode);
            if (!string.IsNullOrEmpty(row.Path)) return row.Path;
            if (row.Common)
            {
                var beside = System.IO.Path.Combine(ProgramFolder ?? "", "CommonTranslations.csv");
                if (File.Exists(beside)) return beside;
            }
            return null;
        }

        /// <summary>
        /// The texts of a Save written file by file - each file read and written once, however many of
        /// its keys changed (a language's texts copied into all of them): a row's file; an alarm's text
        /// into the Alarms.json it was read from (and its entry loaded). The project's copy is not
        /// written (22 October: the project's files take the panel's texts through the page's tool,
        /// each one confirmed - WriteIntoProject).
        /// </summary>
        private sealed class Writes
        {
            private readonly Dictionary<string, List<TranslationFiles.Item>> _csv = new Dictionary<string, List<TranslationFiles.Item>>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, List<AlarmDefinitionLoader.TextItem>> _json = new Dictionary<string, List<AlarmDefinitionLoader.TextItem>>(StringComparer.OrdinalIgnoreCase);
            private readonly List<List<Func<bool>>> _changes = new List<List<Func<bool>>>();

            public void Add(Row row, string lang, string text)
            {
                var done = new List<Func<bool>>();
                _changes.Add(done);
                if (string.Equals(row.File, "Alarms.json", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(row.Path))
                {
                    string tag;
                    var file = AlarmDefinitionLoader.SetLoadedTranslation(row.Key, lang, text, out tag);
                    if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(file)) return;
                    var item = new AlarmDefinitionLoader.TextItem { TagCode = tag, Lang = lang, Text = text };
                    Of(_json, file).Add(item);
                    done.Add(() => item.Written);
                    return;
                }
                string unused;
                var path = FileOf(row, out unused);
                if (string.IsNullOrEmpty(path)) return;
                var csv = new TranslationFiles.Item { Key = row.Key, Lang = lang, Text = text };
                Of(_csv, path).Add(csv);
                done.Add(() => csv.Written);
            }

            /// <summary>Into another copy of the files (the project's): a CSV's text, an alarm's by its TagCode.</summary>
            public void AddTo(string path, Row row, string tagCode, string lang, string text)
            {
                var done = new List<Func<bool>>();
                _changes.Add(done);
                if (string.IsNullOrEmpty(path)) return;
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(tagCode)) return;
                    var item = new AlarmDefinitionLoader.TextItem { TagCode = tagCode, Lang = lang, Text = text };
                    Of(_json, path).Add(item);
                    done.Add(() => item.Written);
                }
                else
                {
                    var item = new TranslationFiles.Item { Key = row.Key, Lang = lang, Text = text };
                    Of(_csv, path).Add(item);
                    done.Add(() => item.Written);
                }
            }

            private static List<T> Of<T>(Dictionary<string, List<T>> d, string path)
            {
                string key;
                try { key = System.IO.Path.GetFullPath(path); } catch { key = path; }
                List<T> l;
                if (!d.TryGetValue(key, out l)) d[key] = l = new List<T>();
                return l;
            }

            /// <summary>Written; for each change added, whether a file took it.</summary>
            public List<bool> Flush()
            {
                // each file as it was this morning kept first (23 October: the previous data backed up before an import)
                foreach (var kv in _csv) { SafetyCopies.FirstOfDay("Translations", kv.Key); TranslationFiles.SetTexts(kv.Key, kv.Value); }
                foreach (var kv in _json) { SafetyCopies.FirstOfDay("Translations", kv.Key); AlarmDefinitionLoader.SetTranslationsInFile(kv.Key, kv.Value); }
                return _changes.Select(c => c.Any(ok => ok())).ToList();
            }
        }

        // ---- other copies of the files: an older installation's, the project's ----------------

        /// <summary>Other copies of the files read once: a CSV's key - language - text, an Alarms.json's TagCode - language - text.</summary>
        public sealed class FileCache
        {
            private readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> _read =
                new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, List<string>> _languages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, Dictionary<string, string>> Of(string path)
            {
                Dictionary<string, Dictionary<string, string>> d;
                if (_read.TryGetValue(path, out d)) return d;
                List<string> langs;
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    d = AlarmDefinitionLoader.TranslationsInFile(path);
                    langs = d.Values.SelectMany(t => t.Keys).Select(l => l.ToUpperInvariant()).Distinct().ToList();
                }
                else d = TranslationFiles.Read(path, out langs);
                _read[path] = d;
                _languages[path] = langs;
                return d;
            }

            /// <summary>The languages a file has (a CSV's heading, an Alarms.json's texts).</summary>
            public List<string> LanguagesOf(string path) { Of(path); return _languages[path]; }

            /// <summary>A row's texts in that copy (null: the copy has not the key).</summary>
            public Dictionary<string, string> TextsOf(string path, Row row, string tagCode)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                Dictionary<string, string> t;
                return Of(path).TryGetValue(path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? tagCode ?? "" : row.Key, out t) ? t : null;
            }
        }

        /// <summary>
        /// A row's file in an older installation's folder: the same place under it as the file
        /// has under the program's folder (CommonTranslations.csv, H500\Translations.csv,
        /// Alarms.json ...); null when that folder has none.
        /// </summary>
        public static string OldFileOf(Row row, string oldFolder, out string tagCode)
        {
            var file = FileOf(row, out tagCode);
            if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(oldFolder)) return null;
            string rel;
            try
            {
                var full = System.IO.Path.GetFullPath(file);
                var root = System.IO.Path.GetFullPath(ProgramFolder ?? "").TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
                rel = full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : System.IO.Path.GetFileName(full);
            }
            catch { rel = System.IO.Path.GetFileName(file); }
            var old = System.IO.Path.Combine(oldFolder, rel);
            return File.Exists(old) ? old : null;
        }

        /// <summary>
        /// Where an older installation's translation files are under a folder picked: the folder of
        /// its CommonTranslations.csv nearest the top (the install's root picked as often as its
        /// bin\Debug); the folder itself when none.
        /// </summary>
        public static string OldProgramFolder(string picked)
        {
            try
            {
                if (string.IsNullOrEmpty(picked) || !Directory.Exists(picked)) return picked;
                if (File.Exists(System.IO.Path.Combine(picked, "CommonTranslations.csv"))) return picked;
                var hit = Directory.GetFiles(picked, "CommonTranslations.csv", SearchOption.AllDirectories)
                                   .Where(f => !f.Split('\\', '/').Any(part => part.Equals("obj", StringComparison.OrdinalIgnoreCase)))
                                   .OrderBy(f => f.Count(ch => ch == '\\' || ch == '/')).FirstOrDefault();
                return hit != null ? System.IO.Path.GetDirectoryName(hit) : picked;
            }
            catch { return picked; }
        }

        /// <summary>A row's file in the project (the program run from its build), its alarm's TagCode; null: none.</summary>
        public static string ProjectFileFor(Row row, out string tagCode)
        {
            FileOf(row, out tagCode);
            try { return ProjectFileOf != null ? ProjectFileOf(row) : null; } catch { return null; }
        }

        /// <summary>
        /// Texts written into the PROJECT's files (HMI Settings > Translation, "Into the project's
        /// files", each one confirmed; 22 October: "a tool to import these translations to the project
        /// file with a confirmation system of the user entry translations"): a CSV's line, an
        /// Alarms.json's entry, in place. The program's own files and its history are not touched.
        /// The number written.
        /// </summary>
        public static int WriteIntoProject(IEnumerable<Wanted> wanted)
        {
            var writes = new Writes();
            int asked = 0;
            var names = new List<string>();
            foreach (var w in wanted ?? Enumerable.Empty<Wanted>())
            {
                if (w == null || w.Row == null || string.IsNullOrEmpty(w.Lang)) continue;
                string tag;
                var path = ProjectFileFor(w.Row, out tag);
                writes.AddTo(path, w.Row, tag, w.Lang.Trim().ToUpperInvariant(), Clean(w.Text));
                asked++;
                if (names.Count < 5) names.Add(w.Row.Key + " " + w.Lang);
            }
            if (asked == 0) return 0;
            int n = writes.Flush().Count(ok => ok);
            AuditLogger.Log("translations written into the project's files by " + CurrentUser() + ": " + n + " of " + asked + " (" + string.Join(", ", names) + (asked > 5 ? " ..." : "") + ").");
            return n;
        }

        /// <summary>The history's change that put a text into a cell (null: none - not entered on the panel).</summary>
        public static Change EnteredOnPanel(IList<Change> history, Row row, string lang, string text)
        {
            if (history == null || row == null) return null;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var h = history[i];
                if (h.Key.Length > 0 && string.Equals(h.Owner, row.Owner, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(h.Key, row.Key, StringComparison.OrdinalIgnoreCase) && string.Equals(h.Lang, lang, StringComparison.OrdinalIgnoreCase))
                    return h.New == Clean(text) ? h : null;
            }
            return null;
        }

        private static string DefaultProjectFile(Row row)
        {
            if (row == null) return null;
            string folder = null;
            if (row.Common)
            {
                // the UI library's project: KabanHMI\Kaban.UI.csproj above the build
                try
                {
                    var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                    for (int i = 0; dir != null && i < 6 && folder == null; i++, dir = dir.Parent)
                        if (File.Exists(System.IO.Path.Combine(dir.FullName, "KabanHMI", "Kaban.UI.csproj"))) folder = System.IO.Path.Combine(dir.FullName, "KabanHMI");
                }
                catch { }
            }
            else
            {
                var m = MachineRegistry.Modules.FirstOrDefault(x => x.Context != null && string.Equals(x.Context.ModuleId, row.Owner, StringComparison.OrdinalIgnoreCase));
                if (m != null) folder = StructuredTypes.ProjectFolderOf(m.Module, m.Context.BaseFolder);
            }
            if (folder == null) return null;
            var p = System.IO.Path.Combine(folder, string.IsNullOrEmpty(row.File) ? (row.Common ? "CommonTranslations.csv" : "Translations.csv") : row.File);
            return File.Exists(p) && !SamePath(p, row.Path) ? p : null;
        }

        private static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try { return string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        /// <summary>
        /// A language's name, as given when it was added (22 October: "we only enter 2-3 letters - how
        /// will the program know which language it is, to ask the AI to translate into it?"); null: none
        /// given (the system's name of the code is used).
        /// </summary>
        public static string NameOf(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            lock (_lock)
            {
                EnsureLoaded();
                string n;
                return _names.TryGetValue(code.Trim(), out n) && n.Length > 0 ? n : null;
            }
        }

        /// <summary>A language's name kept (Languages.cfg); empty: forgotten.</summary>
        public static void SetLanguageName(string code, string name)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) return;
            name = Clean(name);
            lock (_lock)
            {
                EnsureLoaded();
                if (name.Length == 0) _names.Remove(code); else _names[code] = name;
                WriteLanguages();
            }
        }

        /// <summary>A language offered (a new one: its texts English until given; a deleted one: back with its files' texts) - its name kept when given.</summary>
        public static void AddLanguage(string code, string user = null, string name = null)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) return;
            lock (_lock)
            {
                EnsureLoaded();
                if (!string.IsNullOrWhiteSpace(name)) _names[code] = Clean(name);
                bool wasDeleted = _deleted.Remove(code);
                if (!wasDeleted && !_added.Contains(code) && !LanguageManager.Sources.Any(s => s.Languages.Contains(code, StringComparer.OrdinalIgnoreCase)))
                    _added.Add(code);
                WriteLanguages();
                AppendHistory(new[] { new Change { Time = DateTime.Now, User = string.IsNullOrEmpty(user) ? CurrentUser() : user, Lang = code, New = "added" } });
            }
            LanguageManager.AddLanguage(code);
            AuditLogger.Log("language " + code + " added on the panel.");
        }

        /// <summary>A language no longer offered (not English). Its texts stay in the files, for when it is added again.</summary>
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
        /// At the start, after the translation files (and the alarms', the notes'): the languages
        /// added and deleted - and the first version's edits (kept beside the files) written into
        /// them, once.
        /// </summary>
        public static void Apply()
        {
            List<string> added, deleted;
            lock (_lock) { EnsureLoaded(); added = _added.ToList(); deleted = _deleted.ToList(); }
            foreach (var l in added) LanguageManager.AddLanguage(l);
            foreach (var l in deleted) LanguageManager.RemoveLanguage(l);
            TakeOldEdits();
        }

        /// <summary>The first version's UserTranslations.csv: its texts written into the files (their history is there already), the file renamed.</summary>
        private static void TakeOldEdits()
        {
            if (!File.Exists(EditsFile)) return;
            try
            {
                var rows = Rows();
                int n = 0;
                foreach (var line in File.ReadAllLines(EditsFile, Encoding.UTF8).Skip(1))
                {
                    var f = line.Split(';');
                    if (f.Length < 4 || f[1].Trim().Length == 0) continue;
                    var row = rows.FirstOrDefault(r => string.Equals(r.Owner, f[0].Trim(), StringComparison.OrdinalIgnoreCase) &&
                                                       string.Equals(r.Key, f[1].Trim(), StringComparison.OrdinalIgnoreCase));
                    if (row == null) continue;
                    string lang = f[2].Trim().ToUpperInvariant(), text = Unescape(f[3]);
                    if (TextOf(row, lang) == text) continue;
                    Write(row, lang, text);
                    row.FileTexts[lang] = text;
                    foreach (var s in SourcesOf(row)) s.Put(row.Key, lang, text);
                    LanguageManager.SetText(lang, row.Key, text, row.Common);
                    n++;
                }
                var done = EditsFile + ".written";
                if (File.Exists(done)) File.Delete(done);
                File.Move(EditsFile, done);
                if (n > 0) { AuditLogger.Log(n + " translation edit(s) of the first version written into the translation files."); LanguageManager.RaiseLanguageChanged(); }
            }
            catch (Exception ex) { AuditLogger.Log("could not write the first version's translation edits into the files: " + ex.Message); }
        }

        /// <summary>
        /// A translation note in force (the notes page; 21 October: "the texts proposed and applied
        /// from the Notes page should appear in the table as edits"): its texts written into the
        /// files of its key like an edit here, the history naming the note's author. False when its
        /// key is in no file (a control's text without a key of its own).
        /// </summary>
        public static bool FromNote(Note note)
        {
            if (note == null || note.Kind != NoteKind.Translation || string.IsNullOrWhiteSpace(note.TranslationKey)) return false;
            // the rows a file can take (a text registered while running has none)
            var beside = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CommonTranslations.csv");
            var rows = RowsOf(note.TranslationKey).Where(r => !string.IsNullOrEmpty(r.Path) || string.Equals(r.File, "Alarms.json", StringComparison.OrdinalIgnoreCase) ||
                                                              (r.Common && File.Exists(beside))).ToList();
            if (rows.Count == 0) return false;
            var row = rows[0];
            // a text changed on the Translation page after the note was put in force stays (a note of
            // the version before, written now)
            var when = note.AcceptedAt ?? note.Time;
            var history = History();
            var wanted = (note.Texts ?? new List<NoteText>()).Where(t => t != null && !string.IsNullOrWhiteSpace(t.Lang) && !string.IsNullOrWhiteSpace(t.Text))
                         .Where(t => !history.Any(h => h.Time > when && string.Equals(h.Owner, row.Owner, StringComparison.OrdinalIgnoreCase) &&
                                                        string.Equals(h.Key, row.Key, StringComparison.OrdinalIgnoreCase) &&
                                                        string.Equals(h.Lang, t.Lang.Trim(), StringComparison.OrdinalIgnoreCase)))
                         .Select(t => new Wanted { Row = row, Lang = t.Lang, Text = t.Text.Trim() }).ToList();
            var who = (string.IsNullOrEmpty(note.AcceptedBy) ? note.Author : note.AcceptedBy) ?? "";
            Save(wanted, who + " (note)", raise: false);
            return true;
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
        public static void Reload() { lock (_lock) _added = null; }

        // ---- the files -------------------------------------------------------------------

        private static void EnsureLoaded()
        {
            if (_added != null && string.Equals(_loadedFrom, Folder, StringComparison.OrdinalIgnoreCase)) return;
            _loadedFrom = Folder;
            _added = new List<string>();
            _deleted = new List<string>();
            _names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(LanguagesFile))
                    foreach (var raw in File.ReadAllLines(LanguagesFile, Encoding.UTF8))
                    {
                        var line = raw.Trim().TrimStart('﻿');
                        if (line.Length < 2 || line.StartsWith("#")) continue;
                        if (line[0] == '=')
                        {
                            // a language's name: =FR;French
                            var f = line.Substring(1).Split(new[] { ';' }, 2);
                            if (f.Length == 2 && f[0].Trim().Length > 0) _names[f[0].Trim().ToUpperInvariant()] = f[1].Trim();
                            continue;
                        }
                        var code = line.Substring(1).Trim().ToUpperInvariant();
                        if (line[0] == '+' && !_added.Contains(code)) _added.Add(code);
                        if (line[0] == '-' && !_deleted.Contains(code)) _deleted.Add(code);
                    }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the translation languages: " + ex.Message); }
        }

        private static void WriteLanguages()
        {
            var lines = new List<string> { "# Kaban HMI: languages added (+) and deleted (-) on the panel (HMI Settings > Translation), their names (=code;name)" };
            lines.AddRange(_added.Select(l => "+" + l));
            lines.AddRange(_deleted.Select(l => "-" + l));
            lines.AddRange(_names.Select(kv => "=" + kv.Key + ";" + kv.Value));
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

        internal static string Escape(string text) { return Clean(text).Replace("\r\n", "\\r\\n").Replace("\n", "\\n"); }

        private static string Unescape(string text) { return (text ?? "").Replace("\\r\\n", "\r\n").Replace("\\n", "\n"); }

        /// <summary>
        /// The edit history emptied - an Admin's (22 October: "the admin user should be able to reset
        /// the edit history list as well"): the list put away beside it (TranslationHistory_<time>.bak),
        /// the texts left in the files as they are - the table marks none edited any more. The number
        /// of changes it had; -1: not an Admin's, or it could not be put away.
        /// </summary>
        public static int ResetHistory()
        {
            if (!SecurityManager.HasAccess(Kaban.Core.Enums.SecurityLevel.Admin)) return -1;
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(HistoryFile)) return 0;
                    int n = History().Count;
                    var kept = Path.Combine(Folder, "TranslationHistory_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".bak");
                    if (File.Exists(kept)) File.Delete(kept);
                    File.Move(HistoryFile, kept);
                    AuditLogger.Log("translation edit history reset by " + CurrentUser() + ": " + n + " change(s) put away as " + Path.GetFileName(kept) + ".");
                    return n;
                }
                catch (Exception ex)
                {
                    AuditLogger.Log("could not reset the translation edit history: " + ex.Message);
                    return -1;
                }
            }
        }

        private static string CurrentUser()
        {
            try { return string.IsNullOrEmpty(SecurityManager.CurrentUser) ? "?" : SecurityManager.CurrentUser; } catch { return "?"; }
        }
    }

    /// <summary>
    /// A translation file's text changed IN PLACE: the key's line, the language's column - the
    /// rest of the file as it was (its order, its empty lines, its BOM, its line endings). A
    /// language the file has no column for gets one (its header and that line).
    /// </summary>
    public static class TranslationFiles
    {
        /// <summary>A text for SetTexts (Written: put in the file).</summary>
        public sealed class Item
        {
            public string Key = "", Lang = "", Text = "";
            public bool Written;
        }

        /// <summary>
        /// A translation file read: key - language - text (a new line back from \r\n), and its
        /// heading's languages in their order. Empty when it cannot be read.
        /// </summary>
        public static Dictionary<string, Dictionary<string, string>> Read(string path, out List<string> languages)
        {
            var all = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            languages = new List<string>();
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return all;
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                int head = Array.FindIndex(lines, l => l.Trim().TrimStart('\uFEFF').Length > 0);
                if (head < 0) return all;
                var heads = lines[head].TrimStart('\uFEFF').Split(';').Select(h => h.Trim().ToUpperInvariant()).ToList();
                languages.AddRange(heads.Skip(1).Where(h => h.Length > 0));
                for (int i = head + 1; i < lines.Length; i++)
                {
                    var f = lines[i].Split(';');
                    var key = f[0].Trim();
                    if (key.Length == 0 || key.StartsWith("#") || all.ContainsKey(key)) continue;
                    var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int c = 1; c < f.Length && c < heads.Count; c++)
                        if (heads[c].Length > 0) texts[heads[c]] = f[c].Trim().Replace("\\r\\n", "\r\n").Replace("\\n", "\n");
                    all[key] = texts;
                }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the translation file " + path + ": " + ex.Message); }
            return all;
        }

        /// <summary>False when the file or its key is not there (a key is never added).</summary>
        public static bool SetText(string path, string key, string lang, string text)
        {
            var item = new Item { Key = key, Lang = lang, Text = text };
            SetTexts(path, new[] { item });
            return item.Written;
        }

        /// <summary>
        /// Texts written into a file in place, the file read and written once (a language's texts
        /// copied into every key): the key's line, the language's column (added to the heading when
        /// the file has none) - the rest as it was. The number written (a key not in the file: not).
        /// </summary>
        public static int SetTexts(string path, IList<Item> items)
        {
            if (items == null || items.Count == 0 || string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;
            try
            {
                var bytes = File.ReadAllBytes(path);
                bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                var content = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
                string nl = content.Contains("\r\n") ? "\r\n" : "\n";
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
                int head = lines.FindIndex(l => l.Trim().Length > 0);
                if (head < 0) return 0;
                var heads = lines[head].Split(';').ToList();
                var lineOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = head + 1; i < lines.Count; i++)
                {
                    var k = lines[i].Split(';')[0].Trim();
                    if (k.Length > 0 && !lineOf.ContainsKey(k)) lineOf[k] = i;
                }
                bool headChanged = false;
                int n = 0;
                foreach (var it in items)
                {
                    if (it == null) continue;
                    it.Written = false;
                    int at;
                    if (string.IsNullOrWhiteSpace(it.Key) || string.IsNullOrWhiteSpace(it.Lang) || !lineOf.TryGetValue(it.Key.Trim(), out at)) continue;
                    string lang = it.Lang.Trim().ToUpperInvariant();
                    int col = heads.FindIndex(h => string.Equals(h.Trim(), lang, StringComparison.OrdinalIgnoreCase));
                    if (col < 1) { heads.Add(lang); col = heads.Count - 1; headChanged = true; }
                    var cols = lines[at].Split(';').ToList();
                    while (cols.Count <= col) cols.Add("");
                    cols[col] = TranslationEdits.Escape(it.Text);
                    lines[at] = string.Join(";", cols);
                    it.Written = true;
                    n++;
                }
                if (n == 0) return 0;
                if (headChanged) lines[head] = string.Join(";", heads);
                SafeFile.WriteAllText(path, string.Join(nl, lines), new UTF8Encoding(bom));
                return n;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not write the translations into " + path + ": " + ex.Message);
                foreach (var it in items) if (it != null) it.Written = false;
                return 0;
            }
        }
    }
}
