using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using Kaban.UI.Profiles;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// HMI SETTINGS > TRANSLATION - THE IMPORTS AND THE PROJECT'S FILES (22 October):
    ///   📥 an older installation's translations (its folder: "when we run from Visual Studio
    ///      the user's translation file is in the previously created folder ... the import takes
    ///      the translations from the older files for each translation key - the language titles
    ///      from the file and only the keys still used"), translation files, the AI's answer
    ///      (its files, or its answer copied);
    ///   📤 the texts entered on the panel into the project's files ("a tool to import these
    ///      translations to the project file with a confirmation system of the user entry
    ///      translations").
    /// Each the same way ("only the empty cells copied directly, a change of any cell confirmed"):
    /// a text for an empty cell is written at once; a text that changes one is listed for review
    /// (TranslationReviewForm) - ticked when the panel's own (its history) or the AI's - and only
    /// the ticked ones are written. Nothing is written when the review is cancelled.
    /// </summary>
    public partial class TranslationsControl
    {
        /// <summary>A text an import brings: its row and language, the text, why (a note), whether its change is ticked at first.</summary>
        internal sealed class Incoming
        {
            public TranslationEdits.Row Row;
            public string Lang = "", Text = "", Note = "";
            public bool Tick;
            /// <summary>What the cell has now (null: the program's file's text).</summary>
            public string Now;
        }

        /// <summary>What an import did (the tests read it; the user is told).</summary>
        internal sealed class ImportResult
        {
            public int Blocks, Filled, Changed, Taken, Same, Refused, OldKeys, NotWritten;
            public bool Cancelled, NoFiles;
            public readonly List<string> Problems = new List<string>(), NotFound = new List<string>(), Unknown = new List<string>(), Added = new List<string>();
        }

        private static readonly string[] NoteColumns = { "NOTE", "NOTES", "WHY", "REASON", "COMMENT", "CHANGE", "CHANGES" };

        // ---- the menus ---------------------------------------------------------------------

        private ContextMenuStrip ImportMenu()
        {
            var m = UiStyle.Menu();
            m.Items.Add(new ToolStripMenuItem(T("Mnu_TrnImportOld", "From an older installation's folder..."), null, (s, e) => ImportFolderAsked())
            {
                ToolTipText = T("Tip_TrnImportOld", "The folder the program ran from before (a build of Visual Studio, an older version): its translations of the keys still used, its languages")
            });
            m.Items.Add(new ToolStripMenuItem(T("Mnu_TrnImportCsv", "From translation files..."), null, (s, e) => ImportFilesAsked())
            {
                ToolTipText = T("Tip_TrnImportCsv", "CommonTranslations.csv, a machine's Translations.csv or Alarms.json, an export of this page")
            });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem(T("Mnu_TrnImportAiFiles", "The AI's answer: its files..."), EmojiArt.Image(E(0x1F916), MenuGlyphs.Size), (s, e) => ImportAiFilesAsked()));
            m.Items.Add(new ToolStripMenuItem(T("Mnu_TrnImportClipboard", "The AI's answer: copied (the clipboard)"), EmojiArt.Image(E(0x1F916), MenuGlyphs.Size), (s, e) => ImportClipboard()));
            return m;
        }

        private ContextMenuStrip ExportMenu()
        {
            var m = UiStyle.Menu();
            m.Items.Add(new ToolStripMenuItem(T("Mnu_TrnExportCsv", "The rows shown as a translation file..."), null, (s, e) => Export())
            {
                ToolTipText = string.Format(T("Tip_TrnExport", "Export the rows shown as a translation file ({0}) - to copy into the program's files"), "Key;EN;TR ...")
            });
            m.Items.Add(new ToolStripMenuItem(T("Mnu_TrnIntoProject", "Into the project's files (Visual Studio)..."), null, (s, e) => Done(IntoProject(), T("Mnu_TrnIntoProject", "Into the project's files (Visual Studio)..."), true))
            {
                ToolTipText = T("Tip_TrnIntoProject", "The program run from its Visual Studio build: the texts that differ from the project's files listed - " +
                                                     "the ones entered on the panel ticked - and the ticked written into the project's files, to ship with the next build")
            });
            return m;
        }

        // ---- reading the sources -----------------------------------------------------------

        private void ImportFolderAsked()
        {
            using (var dlg = new FolderBrowserDialog { Description = T("Tip_TrnImportOld", "The folder the program ran from before (a build of Visual Studio, an older version): its translations of the keys still used, its languages") })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                Done(ImportFolder(dlg.SelectedPath), T("Mnu_TrnImportOld", "From an older installation's folder..."), false);
            }
        }

        private void ImportFilesAsked()
        {
            var paths = AskFiles(T("Mnu_TrnImportCsv", "From translation files..."), "*.csv;*.json");
            if (paths != null) Done(ImportFiles(paths), T("Mnu_TrnImportCsv", "From translation files..."), false);
        }

        private void ImportAiFilesAsked()
        {
            var paths = AskFiles(T("Mnu_TrnImportAiFiles", "The AI's answer: its files..."), "*.csv;*.txt;*.md");
            if (paths != null) Done(ImportAnswer(ReadFiles(paths)), T("Mnu_TrnImportAiFiles", "The AI's answer: its files..."), false);
        }

        private string[] AskFiles(string title, string pattern)
        {
            using (var dlg = new OpenFileDialog
            {
                Multiselect = true, Title = title,
                Filter = T("Lbl_TrnFiles", "Translation files") + " (" + pattern + ")|" + pattern + "|" + T("Lbl_AllFiles", "All files") + " (*.*)|*.*"
            })
                return dlg.ShowDialog(FindForm()) == DialogResult.OK ? dlg.FileNames : null;
        }

        private static List<KeyValuePair<string, string>> ReadFiles(IEnumerable<string> paths)
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (var p in paths)
                try { list.Add(new KeyValuePair<string, string>(Path.GetFileName(p), File.ReadAllText(p, Encoding.UTF8))); }
                catch (Exception ex) { AuditLogger.Log("could not read the file " + p + ": " + ex.Message); }
            return list;
        }

        private void ImportClipboard()
        {
            var list = new List<KeyValuePair<string, string>>();
            try
            {
                if (Clipboard.ContainsFileDropList()) list.AddRange(ReadFiles(Clipboard.GetFileDropList().Cast<string>()));
                else if (Clipboard.ContainsText()) list.Add(new KeyValuePair<string, string>(null, Clipboard.GetText()));
            }
            catch (Exception ex) { AuditLogger.Log("could not read the clipboard: " + ex.Message); }
            Done(ImportAnswer(list), T("Mnu_TrnImportClipboard", "The AI's answer: copied (the clipboard)"), false);
        }

        private void Done(ImportResult r, string title, bool project)
        {
            if (r == null) return;
            AuditLogger.Log((project ? "translations into the project's files: " : "translations imported: ") + r.Filled + " filled, " + r.Taken + " of " + r.Changed +
                            " change(s) taken" + (r.Cancelled ? " (cancelled)" : "") + ", " + r.Same + " the same, " + r.Refused + " refused, " + r.NotFound.Count + " keys not found.");
            Ask.Tell(this, title.TrimEnd('.'), Report(r, project));
        }

        // ---- the AI's answer -------------------------------------------------------------------

        /// <summary>
        /// The AI's answer - its files (name, content), or its answer copied (name null): its rows read
        /// (CommonTranslations.csv, FA2010_Translations.csv; a row not named: the one row of its key),
        /// an empty cell filled at once, a change listed for review (ticked, its note beside it).
        /// </summary>
        internal ImportResult ImportAnswer(IEnumerable<KeyValuePair<string, string>> sources)
        {
            var res = new ImportResult();
            var owners = _rows.Select(r => r.Owner).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var byKey = _rows.GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var incoming = new List<Incoming>();
            foreach (var src in sources ?? Enumerable.Empty<KeyValuePair<string, string>>())
                foreach (var b in TranslationAiAnswer.Parse(src.Key, src.Value))
                {
                    res.Blocks++;
                    var named = TranslationAiPrompt.OwnerOfFile(b.FileName);
                    string owner = named == null ? null : owners.FirstOrDefault(o => string.Equals(o, named, StringComparison.OrdinalIgnoreCase));
                    int keyCol, noteCol;
                    var langCols = Columns(b.Header, res, out keyCol, out noteCol);
                    if (keyCol < 0) continue;
                    foreach (var cells in b.Rows)
                    {
                        if (keyCol >= cells.Length) continue;
                        var key = cells[keyCol].Trim();
                        if (key.Length == 0) continue;
                        List<TranslationEdits.Row> of;
                        byKey.TryGetValue(key, out of);
                        TranslationEdits.Row row = null;
                        if (of != null)
                            row = owner != null ? of.FirstOrDefault(r => string.Equals(r.Owner, owner, StringComparison.OrdinalIgnoreCase))
                                                : of.Count == 1 ? of[0] : null;
                        if (row == null)
                        {
                            var why = of == null ? key : key + " (" + (owner ?? "?") + ")";
                            if (!res.NotFound.Contains(why)) res.NotFound.Add(why);
                            continue;
                        }
                        string note = noteCol >= 0 && noteCol < cells.Length ? cells[noteCol].Trim() : "";
                        foreach (var lc in langCols)
                            if (lc.Key < cells.Length)
                                incoming.Add(new Incoming { Row = row, Lang = lc.Value, Text = TranslationAiAnswer.FromFile(cells[lc.Key]), Note = note.Length > 0 ? "AI: " + note : "AI", Tick = true });
                    }
                }
            if (res.Blocks == 0) return res;
            return Apply(incoming, res, "AI", T("Ttl_TrnReviewAi", "The AI's translations - the changes to confirm"), T("Undo_TrnAi", "the AI's translations"),
                         T("Msg_TrnReviewAi", "The AI's texts for the empty cells are written at once. These change a text: tick the ones to take (a New cell can be corrected first)."));
        }

        /// <summary>A block's columns: the key's, the note's, the languages' of the table (others: Unknown).</summary>
        private List<KeyValuePair<int, string>> Columns(string[] header, ImportResult res, out int keyCol, out int noteCol)
        {
            keyCol = Array.FindIndex(header, h => string.Equals(h.Trim(), "Key", StringComparison.OrdinalIgnoreCase));
            noteCol = Array.FindIndex(header, h => NoteColumns.Contains(h.Trim().ToUpperInvariant()));
            var langCols = new List<KeyValuePair<int, string>>();
            for (int i = 0; i < header.Length; i++)
            {
                var h = header[i].Trim().ToUpperInvariant();
                if (i == keyCol || i == noteCol || !Regex.IsMatch(h, "^[A-Z]{2,3}$") || h == "USE" || h == "KEY") continue;
                var lang = _langs.FirstOrDefault(l => string.Equals(l, h, StringComparison.OrdinalIgnoreCase));
                if (lang != null) langCols.Add(new KeyValuePair<int, string>(i, lang));
                else if (!res.Unknown.Contains(h)) res.Unknown.Add(h);
            }
            return langCols;
        }

        // ---- an older installation, translation files -------------------------------------------

        /// <summary>
        /// An older installation's translations (the folder picked, or its root: the folder of its
        /// CommonTranslations.csv): each row's file at the same place under it (H500\Translations.csv,
        /// Alarms.json ...), the texts of the keys still used - its keys no longer used left out - and
        /// the languages it has that this one has not (asked first). A change is ticked when this
        /// panel's history says the older text was entered here.
        /// </summary>
        internal ImportResult ImportFolder(string picked)
        {
            var res = new ImportResult();
            var folder = TranslationEdits.OldProgramFolder(picked);
            var cache = new TranslationEdits.FileCache();
            var found = new List<Tuple<TranslationEdits.Row, string, string>>();
            foreach (var r in _rows)
            {
                string tag;
                var old = TranslationEdits.OldFileOf(r, folder, out tag);
                if (old != null) found.Add(Tuple.Create(r, old, tag));
            }
            if (found.Count == 0) { res.NoFiles = true; return res; }
            var files = found.Select(f => f.Item2).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            res.Blocks = files.Count;
            // its keys no longer used
            foreach (var file in files)
            {
                bool json = file.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                var used = new HashSet<string>(found.Where(f => string.Equals(f.Item2, file, StringComparison.OrdinalIgnoreCase)).Select(f => json ? f.Item3 ?? "" : f.Item1.Key),
                                               StringComparer.OrdinalIgnoreCase);
                res.OldKeys += cache.Of(file).Keys.Count(k => !used.Contains(k));
            }
            if (!AddLanguagesOf(files.SelectMany(cache.LanguagesOf), res)) { res.Cancelled = true; return res; }
            var history = TranslationEdits.History();
            var incoming = new List<Incoming>();
            foreach (var f in found)
            {
                var texts = cache.TextsOf(f.Item2, f.Item1, f.Item3);
                if (texts == null) continue;
                foreach (var kv in texts)
                {
                    var lang = _langs.FirstOrDefault(l => string.Equals(l, kv.Key, StringComparison.OrdinalIgnoreCase));
                    if (lang != null) incoming.Add(Panels(new Incoming { Row = f.Item1, Lang = lang, Text = kv.Value ?? "" }, history));
                }
            }
            return Apply(incoming, res, "import", T("Ttl_TrnReviewOld", "The older installation's translations - the changes to confirm"),
                         T("Undo_TrnOld", "the older installation's translations"),
                         T("Msg_TrnReviewOld", "Its texts for the empty cells are written at once. These differ from this program's: the ones entered on this panel " +
                                               "are ticked - the others may be older than this program's own. Tick the ones to take."));
        }

        /// <summary>
        /// Translation files picked: CommonTranslations.csv (the general texts), a machine's
        /// Translations.csv or Alarms.json (whose: its folder's name, else the main machine's), an
        /// export of this page (CommonTranslations.csv, H500_Translations.csv ...).
        /// </summary>
        internal ImportResult ImportFiles(IEnumerable<string> paths)
        {
            var res = new ImportResult();
            var cache = new TranslationEdits.FileCache();
            var owners = _rows.Select(r => r.Owner).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var main = MachineRegistry.Main != null && MachineRegistry.Main.Module != null ? MachineRegistry.Main.Module.ModuleId : null;
            var picked = new List<Tuple<string, string>>();          // path, owner
            foreach (var p in paths ?? Enumerable.Empty<string>())
            {
                if (!File.Exists(p)) continue;
                string owner = TranslationAiPrompt.OwnerOfFile(p);
                if (owner == null)
                {
                    var folder = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(p)) ?? "");
                    owner = owners.FirstOrDefault(o => string.Equals(o, folder, StringComparison.OrdinalIgnoreCase)) ?? main;
                }
                owner = owners.FirstOrDefault(o => string.Equals(o, owner, StringComparison.OrdinalIgnoreCase));
                if (owner == null) { res.NotFound.Add(Path.GetFileName(p)); continue; }
                picked.Add(Tuple.Create(p, owner));
            }
            res.Blocks = picked.Count;
            if (picked.Count == 0) return res;
            if (!AddLanguagesOf(picked.SelectMany(t => cache.LanguagesOf(t.Item1)), res)) { res.Cancelled = true; return res; }
            var history = TranslationEdits.History();
            var incoming = new List<Incoming>();
            foreach (var t in picked)
            {
                bool json = t.Item1.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                // an Alarms.json's texts: the machine's alarms' rows - from its Alarms.json (an older file) or,
                // since 30 October, its Translations.csv (Alarm_<machine>.<TagCode>)
                var rows = _rows.Where(r => string.Equals(r.Owner, t.Item2, StringComparison.OrdinalIgnoreCase) &&
                                            (json ? string.Equals(r.File, "Alarms.json", StringComparison.OrdinalIgnoreCase) || AlarmDefinitionLoader.TagOf(r.Key) != null
                                                  : !string.Equals(r.File, "Alarms.json", StringComparison.OrdinalIgnoreCase))).ToList();
                // an alarm's text by its entry's TagCode, a CSV's by its key
                var byName = new Dictionary<string, TranslationEdits.Row>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in rows)
                {
                    string tag;
                    TranslationEdits.FileOf(r, out tag);
                    if (json && tag == null) tag = AlarmDefinitionLoader.TagOf(r.Key);
                    var name = json ? tag : r.Key;
                    if (!string.IsNullOrEmpty(name) && !byName.ContainsKey(name)) byName[name] = r;
                }
                foreach (var kv in cache.Of(t.Item1))
                {
                    TranslationEdits.Row row;
                    if (!byName.TryGetValue(kv.Key, out row)) { res.OldKeys++; continue; }
                    foreach (var tx in kv.Value)
                    {
                        var lang = _langs.FirstOrDefault(l => string.Equals(l, tx.Key, StringComparison.OrdinalIgnoreCase));
                        if (lang != null) incoming.Add(Panels(new Incoming { Row = row, Lang = lang, Text = tx.Value ?? "" }, history));
                    }
                }
            }
            return Apply(incoming, res, "import", T("Ttl_TrnReviewFiles", "The files' translations - the changes to confirm"),
                         T("Undo_TrnFiles", "the files' translations"),
                         T("Msg_TrnReviewOld", "Its texts for the empty cells are written at once. These differ from this program's: the ones entered on this panel " +
                                               "are ticked - the others may be older than this program's own. Tick the ones to take."));
        }

        /// <summary>Ticked, with who and when, when this panel's history says the text was entered here.</summary>
        private static Incoming Panels(Incoming i, IList<TranslationEdits.Change> history)
        {
            var h = TranslationEdits.EnteredOnPanel(history, i.Row, i.Lang, i.Text);
            i.Tick = h != null;
            i.Note = h != null ? string.Format(T("Lbl_TrnEnteredBy", "entered on the panel: {0}, {1}"), h.User,
                                               h.Time.ToString(ViewPreferences.DatePattern + " " + ViewPreferences.TimePattern(), System.Globalization.CultureInfo.CurrentCulture))
                               : T("Lbl_TrnNotEntered", "not entered on this panel");
            return i;
        }

        /// <summary>The languages files have that the table has not: added when the user says so (false: cancelled).</summary>
        private bool AddLanguagesOf(IEnumerable<string> languages, ImportResult res)
        {
            var deleted = TranslationEdits.DeletedLanguages.ToList();
            var fresh = languages.Select(l => (l ?? "").Trim().ToUpperInvariant()).Where(l => Regex.IsMatch(l, "^[A-Z]{2,3}$") && l != "KEY" && l != "USE")
                                 .Distinct().Where(l => !_langs.Contains(l, StringComparer.OrdinalIgnoreCase)).ToList();
            if (fresh.Count == 0) return true;
            var names = string.Join(", ", fresh.Select(l => l + " (" + (TranslationLanguageForm.NameFor(l) ?? "?") + ")"));
            if (!Ask.YesNo(this, T("Tab_Translations", "Translation"), string.Format(T("Msg_TrnImportLangs",
                    "The files have languages this program has not: {0}. Add them, with their texts?{1}"), names,
                    fresh.Any(l => deleted.Contains(l, StringComparer.OrdinalIgnoreCase)) ? "\r\n\r\n" + T("Msg_TrnImportLangsDeleted", "(Some were deleted here before.)") : "")))
            {
                foreach (var l in fresh) if (!res.Unknown.Contains(l)) res.Unknown.Add(l);
                return true;
            }
            foreach (var l in fresh)
            {
                TranslationEdits.AddLanguage(l, null, TranslationLanguageForm.NameFor(l));
                res.Added.Add(l);
            }
            RefreshLanguages();
            return true;
        }

        // ---- the panel's texts into the project's files -------------------------------------

        /// <summary>
        /// The program run from its Visual Studio build: every text of the program's files that the
        /// project's copy has not (an empty cell there: written at once) or has otherwise (listed -
        /// ticked when entered on this panel, its history says so) - the ticked written into the
        /// project's files. A key the project's file has not is left out (a key is never added).
        /// </summary>
        internal ImportResult IntoProject()
        {
            var res = new ImportResult();
            var cache = new TranslationEdits.FileCache();
            var history = TranslationEdits.History();
            var incoming = new List<Incoming>();
            foreach (var r in _rows)
            {
                string tag;
                var proj = TranslationEdits.ProjectFileFor(r, out tag);
                if (proj == null) continue;
                res.Blocks++;
                var there = cache.TextsOf(proj, r, tag);
                if (there == null) { res.OldKeys++; continue; }
                foreach (var l in _langs)
                {
                    string mine, theirs;
                    if (!r.FileTexts.TryGetValue(l, out mine) || string.IsNullOrWhiteSpace(mine)) continue;
                    there.TryGetValue(l, out theirs);
                    var i = Panels(new Incoming { Row = r, Lang = l, Text = mine, Now = theirs ?? "" }, history);
                    if (!i.Tick) i.Note = T("Lbl_TrnProjectNewer", "not entered on this panel - the project's may be newer");
                    incoming.Add(i);
                }
            }
            if (res.Blocks == 0) { res.NoFiles = true; return res; }
            return Apply(incoming, res, null, T("Ttl_TrnReviewProject", "Into the project's files - the changes to confirm"), null,
                         T("Msg_TrnReviewProject", "The texts for the project's empty cells are written at once. These differ from the project's: the ones entered " +
                                                   "on this panel are ticked - the others may be older than the project's own. Tick the ones to write into the project."),
                         project: true);
        }

        // ---- the one way of taking them --------------------------------------------------------

        /// <summary>
        /// The texts brought: the same as now - nothing; a placeholder of the English lost - refused;
        /// an empty cell - written at once; a change - listed for review, the ticked written. By:
        /// the history's note on who (the user and "AI" / "import"); project: into the project's files.
        /// </summary>
        /// <param name="undoWhat">What the undo step is called ("the AI's translations").</param>
        private ImportResult Apply(List<Incoming> incoming, ImportResult res, string by, string title, string undoWhat, string intro, bool project = false)
        {
            if (_grid.IsCurrentCellInEditMode) _grid.EndEdit();
            var fills = new List<Incoming>();
            var changes = new List<TranslationReviewForm.Item>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var i in incoming)
            {
                var text = TranslationEdits.Clean(i.Text);
                if (text.Length == 0) continue;
                var id = Id(i.Row.Owner, i.Row.Key, i.Lang);
                if (!seen.Add(id)) continue;                         // the first one of a cell
                string now = i.Now;
                if (now == null) { string f; now = i.Row.FileTexts.TryGetValue(i.Lang, out f) ? f ?? "" : ""; }
                if (TranslationEdits.Clean(now) == text) { res.Same++; continue; }
                if (!project)
                {
                    var english = TranslationAiAnswer.Placeholders(i.Lang == "EN" ? now : TextOfFile(i.Row, "EN"));
                    var lost = english.Where(n => !TranslationAiAnswer.Placeholders(text).Contains(n)).ToList();
                    if (english.Count > 0 && lost.Count > 0)
                    {
                        res.Refused++;
                        res.Problems.Add(i.Row.Key + " " + i.Lang + ": " + string.Join(" ", lost.Select(n => "{" + n + "}")));
                        continue;
                    }
                }
                i.Text = text;
                if (now.Trim().Length == 0) fills.Add(i);
                else changes.Add(new TranslationReviewForm.Item { Row = i.Row, Lang = i.Lang, Now = now, New = text, Why = i.Note ?? "", Take = i.Tick });
            }
            res.Changed = changes.Count;
            List<TranslationReviewForm.Item> taken = new List<TranslationReviewForm.Item>();
            if (changes.Count > 0)
            {
                var ok = TranslationReviewForm.Review(FindForm() ?? (IWin32Window)this, title, intro, changes, fills.Count,
                    project ? T("Btn_TrnWriteProject", "Write into the project: {0} empty cell(s) + {1} change(s) ticked")
                            : T("Btn_TrnWriteImport", "Write: {0} empty cell(s) + {1} change(s) ticked"));
                if (!ok) { res.Cancelled = true; return res; }
                taken = changes.Where(c => c.Take && TranslationEdits.Clean(c.New).Length > 0).ToList();
            }
            var wanted = fills.Select(f => new TranslationEdits.Wanted { Row = f.Row, Lang = f.Lang, Text = f.Text })
                              .Concat(taken.Select(c => new TranslationEdits.Wanted { Row = c.Row, Lang = c.Lang, Text = c.New })).ToList();
            res.Filled = fills.Count;
            res.Taken = taken.Count;
            if (wanted.Count == 0) return res;
            if (project)
            {
                int n = TranslationEdits.WriteIntoProject(wanted);
                res.NotWritten = wanted.Count - n;
                return res;
            }
            int notWritten;
            var done = new List<TranslationEdits.Change>();
            TranslationEdits.Save(wanted, out notWritten, CurrentUser() + (by != null ? " (" + by + ")" : ""), true, done);
            res.NotWritten = notWritten;
            // one step: Ctrl+Z (or the undo on top) puts every text it wrote back as it was
            PushWritten(undoWhat ?? title, done);
            // what was typed in those cells and not saved gives way to what was taken
            foreach (var w in wanted) _pending.Remove(Id(w.Row.Owner, w.Row.Key, w.Lang));
            if (_pending.Count == 0) _dirty.Reset();
            LoadRows();
            ApplyFilter();
            return res;
        }

        private static string TextOfFile(TranslationEdits.Row row, string lang)
        {
            string t;
            return row.FileTexts.TryGetValue(lang, out t) ? t ?? "" : "";
        }

        private static string CurrentUser()
        {
            return string.IsNullOrEmpty(SecurityManager.CurrentUser) ? "?" : SecurityManager.CurrentUser;
        }

        /// <summary>What an import did, in a few lines.</summary>
        internal static string Report(ImportResult r, bool project = false)
        {
            if (r.NoFiles)
                return project ? T("Msg_TrnNoProject", "The project's files are not found: this works when the program runs from its Visual Studio build.")
                               : T("Msg_TrnNoOldFiles", "No translation file of this program is in that folder (CommonTranslations.csv, a machine's Translations.csv or Alarms.json).");
            if (r.Blocks == 0)
                return string.Format(T("Msg_TrnAiNone", "Nothing to import: no \"{0}\" heading in it - the AI's translation files, or its answer with them, are what it reads."), "Key;EN;TR ...");
            var sb = new StringBuilder();
            if (r.Cancelled) sb.Append(T("Msg_TrnImportCancelled", "Cancelled: nothing written."));
            else if (r.Filled + r.Taken == 0) sb.Append(project ? T("Msg_TrnProjectSame", "Nothing written: the project's files have the program's texts.")
                                                                : T("Msg_TrnAiNoChange", "Nothing written: no text of the table changes."));
            else
                sb.Append(string.Format(project ? T("Msg_TrnProjectWritten", "Written into the project's files: {0} empty cell(s) filled, {1} of {2} change(s) taken.")
                                                : T("Msg_TrnImported", "Written into the translation files: {0} empty cell(s) filled, {1} of {2} change(s) taken - the history has them."),
                                        r.Filled, r.Taken, r.Changed));
            if (r.NotWritten > 0) sb.Append("\r\n").Append(string.Format(T("Msg_TrnNotWritten", "{0} in force only: no file could take them."), r.NotWritten));
            if (r.Same > 0) sb.Append("\r\n").Append(string.Format(T("Msg_TrnAiSame", "{0} the same as the table's already."), r.Same));
            if (r.Added.Count > 0) sb.Append("\r\n").Append(string.Format(T("Msg_TrnLangsAdded", "Languages added: {0}."), string.Join(", ", r.Added)));
            if (r.OldKeys > 0)
                sb.Append("\r\n").Append(string.Format(project ? T("Msg_TrnProjectNoKey", "{0} key(s) the project's files have not - left out (a key is not added).")
                                                               : T("Msg_TrnOldKeys", "{0} key(s) this program no longer uses - left out."), r.OldKeys));
            if (r.Refused > 0)
                sb.Append("\r\n\r\n").Append(string.Format(T("Msg_TrnAiRefused", "{0} refused - a placeholder of the English missing in them:"), r.Refused))
                  .Append("\r\n").Append(string.Join("\r\n", r.Problems.Take(8))).Append(r.Problems.Count > 8 ? "\r\n..." : "");
            if (r.NotFound.Count > 0)
                sb.Append("\r\n\r\n").Append(string.Format(T("Msg_TrnAiNotFound", "{0} key(s) the table has not (or not for that file) - left out: {1}"), r.NotFound.Count,
                                                              string.Join(", ", r.NotFound.Take(10)) + (r.NotFound.Count > 10 ? " ..." : "")));
            if (r.Unknown.Count > 0)
                sb.Append("\r\n\r\n").Append(string.Format(T("Msg_TrnAiUnknown", "Not a language of the table - left out (add it first with ➕): {0}"), string.Join(", ", r.Unknown)));
            return sb.ToString();
        }

        // ---- a typed cell's tooltip ------------------------------------------------------------

        /// <summary>A typed text's cell: what its file has.</summary>
        private void OnCellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _shown.Count || !IsLanguage(e.ColumnIndex)) return;
            var tip = TipOf(_shown[e.RowIndex], LangOf(_grid.Columns[e.ColumnIndex]));
            if (tip != null) e.ToolTipText = tip;
        }

        /// <summary>A typed cell's tooltip (null: not typed).</summary>
        internal string TipOf(TranslationEdits.Row row, string lang)
        {
            if (!_pending.ContainsKey(Id(row.Owner, row.Key, lang))) return null;
            var before = TextOfFile(row, lang);
            return T("Lbl_TrnInFile", "In the file") + ": " + (before.Length > 0 ? before : "-");
        }
    }
}
