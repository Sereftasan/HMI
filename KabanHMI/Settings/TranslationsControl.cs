using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using Kaban.UI.Profiles;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// HMI SETTINGS > TRANSLATION (21 October: "a page called Translation, a table showing all
    /// the translations, for Maintenance and above, with the same format and functions as the
    /// other tables - the columns shown or hidden, the filter, the emoji buttons on top. Filtered
    /// by whether a text belongs to the general application or to a machine (a column for its
    /// owner), a column for the key and one for each language. A language added or deleted, and
    /// the edit history kept so I can check the edited ones and put them in my translation files").
    ///
    /// Every key of every translation file read (CommonTranslations.csv: General; a machine's
    /// Translations.csv and its alarms' texts: that machine), its text in each language. A text
    /// is typed in its cell (Delete empties it) and put in force with Save - written into its file
    /// (22 October: "write directly"; the project's copy too, when the program runs from its build),
    /// every change in the history. A text edited here is yellow, one typed and not saved orange, an
    /// empty one pink.
    /// </summary>
    public partial class TranslationsControl : UserControl, IRebuildForLanguage
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _search = new TextBox();
        private readonly ComboBox _owner = new ComboBox();
        private readonly CheckBox _onlyEdited = new CheckBox();
        private readonly CheckBox _missing = new CheckBox();
        private readonly Label _status = new Label();
        private readonly ToolTip _tips = UiStyle.Tips();
        private readonly Timer _typing = new Timer { Interval = 300 };
        private readonly DirtyTracker _dirty = new DirtyTracker();
        private readonly Button _btnSave, _btnUndo, _btnRedo, _btnAddLang, _btnDelLang, _btnCopyLang, _btnHistory, _btnExport, _btnAi, _btnImport, _btnFind;
        /// <summary>The row asked for from elsewhere (the notes page): marked until another is chosen.</summary>
        private string _marked;

        private List<TranslationEdits.Row> _rows = new List<TranslationEdits.Row>();
        private List<TranslationEdits.Row> _shown = new List<TranslationEdits.Row>();
        private List<string> _langs = new List<string>();
        private Dictionary<string, TranslationEdits.Edit> _edits = new Dictionary<string, TranslationEdits.Edit>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Typed, not saved: owner|key|language - text.</summary>
        private readonly Dictionary<string, string> _pending = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string _sortColumn = "";
        private bool _sortDescending;
        private bool _filling;

        private static string T(string key, string fallback) { return LanguageManager.GetString(key, fallback); }
        private static string E(int codePoint) { return char.ConvertFromUtf32(codePoint); }
        private static string Id(string owner, string key, string lang) { return owner + "\u0001" + key + "\u0001" + lang; }

        // the texts are put on when it is built: built again in another language (SettingsScreen),
        // where the operator was and what was typed kept
        private sealed class Where
        {
            public string Search = "", Owner, Key, Sort = "";
            public bool OnlyEdited, Missing, Descending;
            public Dictionary<string, string> Pending;
        }

        object IRebuildForLanguage.Place
        {
            get
            {
                var at = CurrentRow;
                return new Where
                {
                    Search = _search.Text, Owner = SelectedOwner, OnlyEdited = _onlyEdited.Checked, Missing = _missing.Checked,
                    Key = at != null ? at.Owner + "\u0001" + at.Key : null, Sort = _sortColumn, Descending = _sortDescending,
                    Pending = new Dictionary<string, string>(_pending, StringComparer.OrdinalIgnoreCase)
                };
            }
        }

        void IRebuildForLanguage.ReturnTo(object place)
        {
            var w = place as Where;
            if (w == null) return;
            _filling = true;
            _search.Text = w.Search ?? "";
            _onlyEdited.Checked = w.OnlyEdited;
            _missing.Checked = w.Missing;
            for (int i = 0; i < _owner.Items.Count; i++)
                if (string.Equals(((Choice)_owner.Items[i]).Value, w.Owner, StringComparison.OrdinalIgnoreCase)) _owner.SelectedIndex = i;
            _filling = false;
            _sortColumn = w.Sort ?? "";
            _sortDescending = w.Descending;
            foreach (var kv in w.Pending ?? new Dictionary<string, string>()) _pending[kv.Key] = kv.Value;
            if (_pending.Count > 0) _dirty.MarkDirty();
            ApplyFilter();
            if (w.Key != null) Show(w.Key);
        }

        private sealed class Choice
        {
            public readonly string Value, Text;
            public Choice(string value, string text) { Value = value; Text = text; }
            public override string ToString() { return Text; }
        }

        public TranslationsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;

            // ---- the buttons and the filters, on top ----
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
                Padding = new Padding(0, 2, 0, 4), BackColor = Color.White
            };
            _btnSave = UiStyle.ToolButton(E(0x1F4BE), T("Tip_TrnSave", "Save: the texts typed in force and written into their translation files - every change in the history"), _tips,
                                          (s, e) => Save());
            // 22 October: "discard the changes after the AI translation file import with Ctrl+Z on top of the screen":
            // the curl that only threw away the texts typed is Undo now - a text typed, an import, a Save - with Redo
            _btnUndo = UiStyle.IconButton(ToolGlyphs.Undo, "Undo", T("Tip_TrnUndo", "Undo the last change: a text typed, an import, a Save"), _tips, (s, e) => Undo());
            _btnRedo = UiStyle.IconButton(ToolGlyphs.Redo, "Redo", T("Tip_TrnRedo", "Redo the change undone last"), _tips, (s, e) => Redo());
            _btnAddLang = UiStyle.ToolButton(E(0x2795), T("Tip_TrnAddLang", "Add a language"), _tips, (s, e) => AddLanguage());
            _btnDelLang = UiStyle.ToolButton(E(0x1F5D1), T("Tip_TrnDelLang", "Delete a language (not English)"), _tips, null);
            _btnDelLang.Click += (s, e) => DeleteLanguageMenu().Show(_btnDelLang, new Point(0, _btnDelLang.Height));
            // 22 October: "copy any language column to paste to any column's missing cells"
            _btnCopyLang = UiStyle.ToolButton(E(0x1F4CB), T("Tip_TrnCopyLang", "Copy a language's texts into the empty cells of other languages (typed until Save)"), _tips,
                                              (s, e) => CopyLanguages());
            // the bin's usual key (Delete) empties a cell of the table here, it does not delete a language
            Kaban.UI.Helpers.Shortcuts.Set(_btnDelLang, Keys.None, _tips, T("Tip_TrnDelLang", "Delete a language (not English)"));
            _btnHistory = UiStyle.ToolButton(E(0x1F552), T("Tip_TrnHistory", "The edit history: every text changed here - who, when, before and after"), _tips,
                                             (s, e) => ShowHistory());
            _btnExport = UiStyle.ToolButton(E(0x1F4E4), T("Tip_TrnExportMenu", "Export: the rows shown as a translation file, or the texts entered on the panel into the project's files (Visual Studio)"),
                                            _tips, null);
            _btnExport.Click += (s, e) => ExportMenu().Show(_btnExport, new Point(0, _btnExport.Height));
            // 22 October: "a tool to use AI to translate the missing cells and to check the other translations ...
            // and an import button to update the translation files with the files the AI makes"
            _btnAi = UiStyle.ToolButton(E(0x1F916), T("Tip_TrnAi", "Ask an AI: translate the missing texts, or check every translation, of the rows shown - " +
                                                     "a briefing with their files and where each text is shown"), _tips, null);
            _btnAi.Click += (s, e) => AiMenu().Show(_btnAi, new Point(0, _btnAi.Height));
            // 22 October: one import, beside the AI: an older installation's translations, translation files, the AI's answer
            _btnImport = UiStyle.ToolButton(E(0x1F4E5), T("Tip_TrnImportMenu", "Import translations: an older installation's, translation files, the AI's answer - " +
                                                         "the empty cells filled, every change confirmed first"), _tips, null);
            _btnImport.Click += (s, e) => ImportMenu().Show(_btnImport, new Point(0, _btnImport.Height));
            // 21 October: "a tool to find where the translation line is seen (on the screen, a message box ...) -
            // a button on top, Ctrl+F, which opens the list of where it is used"
            _btnFind = UiStyle.ToolButton(E(0x1F50E), T("Tip_TrnFind", "Where is it used? The pages, the menu, the alarms and the program's lines using the selected row's key"),
                                          _tips, (s, e) => FindUses());
            Kaban.UI.Helpers.Shortcuts.Set(_btnFind, Keys.Control | Keys.F, _tips,
                                           T("Tip_TrnFind", "Where is it used? The pages, the menu, the alarms and the program's lines using the selected row's key"));
            _dirty.Guard(_btnSave);
            UpdateUndoButtons();
            foreach (var b in new[] { _btnSave, _btnUndo, _btnRedo, _btnAddLang, _btnDelLang, _btnCopyLang, _btnHistory, _btnExport, _btnAi, _btnImport, _btnFind })
            {
                b.Margin = new Padding(2, 1, 2, 1);
                bar.Controls.Add(b);
            }
            bar.Controls.Add(new Label { Width = 14, Height = 8 });

            Func<string, Label> caption = t => new Label { Text = t, AutoSize = true, Margin = new Padding(8, 9, 2, 3), ForeColor = Color.FromArgb(90, 96, 106) };
            _search.Width = 220;
            _search.Margin = new Padding(0, 5, 3, 3);
            UiStyle.SetTip(_tips, _search, T("Tip_TrnSearch", "Every word must appear - in the key, the owner or a text, in any case"));
            _search.TextChanged += (s, e) => { if (_filling) return; _typing.Stop(); _typing.Start(); };
            _typing.Tick += (s, e) => { _typing.Stop(); ApplyFilter(); };
            _owner.DropDownStyle = ComboBoxStyle.DropDownList;
            _owner.Width = 190;
            _owner.Margin = new Padding(0, 5, 3, 3);
            UiStyle.SetTip(_tips, _owner, T("Tip_TrnOwner", "Whose the texts are: the general application's (every machine) or a machine's own"));
            _owner.SelectedIndexChanged += (s, e) => { if (!_filling) ApplyFilter(); };
            _onlyEdited.Text = T("Lbl_TrnOnlyEdited", "Edited here");
            _onlyEdited.AutoSize = true;
            _onlyEdited.Margin = new Padding(10, 8, 3, 3);
            UiStyle.SetTip(_tips, _onlyEdited, T("Tip_TrnOnlyEdited", "Only the keys with a text edited on this panel (in force or typed)"));
            _onlyEdited.CheckedChanged += (s, e) => { if (!_filling) ApplyFilter(); };
            _missing.Text = T("Lbl_TrnMissing", "A text missing");
            _missing.AutoSize = true;
            _missing.Margin = new Padding(6, 8, 3, 3);
            UiStyle.SetTip(_tips, _missing, T("Tip_TrnMissing", "Only the keys without a text in a language shown (the screen shows English there)"));
            _missing.CheckedChanged += (s, e) => { if (!_filling) ApplyFilter(); };
            var clear = UiStyle.ToolButton("✖", T("Tip_TrnClear", "Clear the search and the filters"), _tips, (s, e) => ClearFilters());
            clear.Margin = new Padding(8, 1, 3, 1);
            bar.Controls.Add(Pair(caption("🔍"), _search));
            bar.Controls.Add(Pair(caption(T("Col_TrnOwner", "Owner")), _owner));
            bar.Controls.Add(_onlyEdited);
            bar.Controls.Add(_missing);
            bar.Controls.Add(clear);

            // ---- the table ----
            _grid.Dock = DockStyle.Fill;
            _grid.VirtualMode = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.MultiSelect = false;
            _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            _grid.CellValueNeeded += OnCellValueNeeded;
            _grid.CellValuePushed += OnCellValuePushed;
            _grid.CellFormatting += OnCellFormatting;
            _grid.CellValidating += OnCellValidating;
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0 && IsLanguage(e.ColumnIndex)) _grid.BeginEdit(true); };
            _grid.ColumnHeaderMouseClick += (s, e) => { if (e.Button == MouseButtons.Left && e.ColumnIndex >= 0) SortBy(_grid.Columns[e.ColumnIndex].Name); };
            _grid.KeyDown += OnGridKeyDown;
            _grid.ShowCellToolTips = true;
            _grid.CellToolTipTextNeeded += OnCellToolTipTextNeeded;
            // the row asked for stays marked until another is chosen
            _grid.CellClick += (s, e) =>
            {
                if (_marked == null || e.RowIndex < 0 || e.RowIndex >= _shown.Count) return;
                var r = _shown[e.RowIndex];
                if (!string.Equals(r.Owner + "\u0001" + r.Key, _marked, StringComparison.OrdinalIgnoreCase)) { _marked = null; _grid.Invalidate(); }
            };
            BuildColumns();
            UiStyle.EnableTableTools(_grid, "Translations", sorting: false);

            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.ForeColor = Color.FromArgb(70, 76, 86);
            _status.Padding = new Padding(4, 4, 0, 0);

            Controls.Add(_grid);
            Controls.Add(_status);
            Controls.Add(bar);

            LoadRows();
            ApplyFilter();
            LanguageManager.LanguagesChanged += OnLanguagesChanged;
            LanguageManager.LanguageChanged += OnTextsChanged;
            Disposed += (s, e) => { LanguageManager.LanguagesChanged -= OnLanguagesChanged; LanguageManager.LanguageChanged -= OnTextsChanged; _typing.Dispose(); };
        }

        private static Control Pair(Control a, Control b)
        {
            var p = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0) };
            p.Controls.Add(a);
            p.Controls.Add(b);
            return p;
        }

        /// <summary>The rows as the table lists them now (the tests).</summary>
        internal IList<TranslationEdits.Row> Shown { get { return _shown.AsReadOnly(); } }
        /// <summary>The languages, as the table's columns (the tests).</summary>
        internal IList<string> Languages { get { return _langs.AsReadOnly(); } }
        internal DataGridView Grid { get { return _grid; } }
        internal string SearchText { get { return _search.Text; } set { _search.Text = value; _typing.Stop(); ApplyFilter(); } }
        internal int PendingCount { get { return _pending.Count; } }

        private TranslationEdits.Row CurrentRow
        {
            get { var c = _grid.CurrentCell; return c != null && c.RowIndex >= 0 && c.RowIndex < _shown.Count ? _shown[c.RowIndex] : null; }
        }

        private string SelectedOwner { get { var c = _owner.SelectedItem as Choice; return c != null ? c.Value : null; } }

        // ---- the columns -----------------------------------------------------------------

        private const string LangPrefix = "L_";

        private bool IsLanguage(int column)
        {
            return column >= 0 && column < _grid.Columns.Count && _grid.Columns[column].Name.StartsWith(LangPrefix, StringComparison.Ordinal);
        }

        private static string LangOf(DataGridViewColumn c) { return c.Name.Substring(LangPrefix.Length); }

        private void BuildColumns()
        {
            _langs = TranslationEdits.Languages();
            bool canEdit = SecurityManager.HasAccess(SecurityLevel.Maintenance);
            if (_grid.Columns.Count == 0)
            {
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", HeaderText = T("Col_TrnKey", "Key"), ReadOnly = true, Width = 220 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Owner", HeaderText = T("Col_TrnOwner", "Owner"), ReadOnly = true, Width = 90 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "File", HeaderText = T("Col_TrnFile", "File"), ReadOnly = true, Width = 140 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Changed", HeaderText = T("Col_TrnChanged", "Edited"), ReadOnly = true, Width = 170 });
            }
            // a language deleted: its column goes; one added: a column for it, before Edited
            foreach (var c in _grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Name.StartsWith(LangPrefix) && !_langs.Contains(LangOf(c))).ToList())
                _grid.Columns.Remove(c);
            int at = _grid.Columns["File"].Index + 1;
            foreach (var l in _langs)
            {
                var name = LangPrefix + l;
                if (!_grid.Columns.Contains(name))
                    _grid.Columns.Insert(at, new DataGridViewTextBoxColumn
                    {
                        Name = name, HeaderText = l, Width = 230, ReadOnly = !canEdit, SortMode = DataGridViewColumnSortMode.Programmatic,
                        ToolTipText = Kaban.UI.Helpers.AlarmAiPrompt.LanguageName(l)
                    });
                at = _grid.Columns[name].Index + 1;
            }
            foreach (DataGridViewColumn c in _grid.Columns) c.SortMode = DataGridViewColumnSortMode.Programmatic;
            ShowSortMark();
        }

        // ---- the rows --------------------------------------------------------------------

        private void LoadRows()
        {
            _rows = TranslationEdits.Rows();
            _edits = TranslationEdits.Edits.ToDictionary(e => Id(e.Owner, e.Key, e.Lang), e => e, StringComparer.OrdinalIgnoreCase);
            var owners = _rows.Select(r => r.Owner).Distinct(StringComparer.OrdinalIgnoreCase)
                              .OrderBy(o => o == TranslationEdits.General ? 0 : 1).ThenBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
            string keep = SelectedOwner;
            _filling = true;
            _owner.Items.Clear();
            _owner.Items.Add(new Choice(null, T("Lbl_TrnAllOwners", "All")));
            _owner.Items.Add(new Choice("*machines", T("Lbl_TrnMachines", "Every machine's")));
            foreach (var o in owners) _owner.Items.Add(new Choice(o, OwnerName(o)));
            _owner.SelectedIndex = 0;
            for (int i = 0; i < _owner.Items.Count; i++)
                if (string.Equals(((Choice)_owner.Items[i]).Value, keep, StringComparison.OrdinalIgnoreCase)) _owner.SelectedIndex = i;
            _filling = false;
        }

        private static string OwnerName(string owner)
        {
            return string.Equals(owner, TranslationEdits.General, StringComparison.OrdinalIgnoreCase) ? T("Lbl_TrnGeneral", "General") : owner;
        }

        /// <summary>A text as the cell shows it: typed, else the file's (the edits saved are in the files).</summary>
        private string TextOf(TranslationEdits.Row row, string lang)
        {
            string t;
            if (_pending.TryGetValue(Id(row.Owner, row.Key, lang), out t)) return t;
            return row.FileTexts.TryGetValue(lang, out t) ? t ?? "" : "";
        }

        private bool Edited(TranslationEdits.Row row)
        {
            return _langs.Any(l => _edits.ContainsKey(Id(row.Owner, row.Key, l)) || _pending.ContainsKey(Id(row.Owner, row.Key, l)));
        }

        private void ApplyFilter()
        {
            if (_filling) return;
            var keep = CurrentRow;
            int column = _grid.CurrentCell != null ? _grid.CurrentCell.ColumnIndex : 0;
            var words = (_search.Text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string owner = SelectedOwner;
            IEnumerable<TranslationEdits.Row> q = _rows;
            if (owner == "*machines") q = q.Where(r => !r.Common);
            else if (owner != null) q = q.Where(r => string.Equals(r.Owner, owner, StringComparison.OrdinalIgnoreCase));
            if (_onlyEdited.Checked) q = q.Where(Edited);
            var visibleLangs = _langs.Where(l => _grid.Columns.Contains(LangPrefix + l) && _grid.Columns[LangPrefix + l].Visible).ToList();
            if (_missing.Checked) q = q.Where(r => visibleLangs.Any(l => string.IsNullOrWhiteSpace(TextOf(r, l))));
            if (words.Length > 0)
                q = q.Where(r =>
                {
                    var hay = r.Key + " " + OwnerName(r.Owner) + " " + string.Join(" ", _langs.Select(l => TextOf(r, l)));
                    return words.All(w => hay.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
                });
            _shown = Sorted(q).ToList();
            _grid.RowCount = 0;
            _grid.RowCount = _shown.Count;
            if (keep != null)
            {
                int i = _shown.IndexOf(keep);
                if (i >= 0 && column < _grid.Columns.Count) try { _grid.CurrentCell = _grid.Rows[i].Cells[column]; } catch { }
            }
            _grid.Invalidate();
            UpdateStatus();
        }

        private IEnumerable<TranslationEdits.Row> Sorted(IEnumerable<TranslationEdits.Row> q)
        {
            if (string.IsNullOrEmpty(_sortColumn) || !_grid.Columns.Contains(_sortColumn)) return q;
            Func<TranslationEdits.Row, string> by = r => CellText(r, _sortColumn);
            return _sortDescending ? q.OrderByDescending(by, StringComparer.CurrentCultureIgnoreCase) : q.OrderBy(by, StringComparer.CurrentCultureIgnoreCase);
        }

        /// <summary>A heading clicked: sorted by it - again: the other way round - a third time: as the files have them.</summary>
        private void SortBy(string column)
        {
            if (_sortColumn == column && _sortDescending) { _sortColumn = ""; _sortDescending = false; }
            else if (_sortColumn == column) _sortDescending = true;
            else { _sortColumn = column; _sortDescending = false; }
            ShowSortMark();
            ApplyFilter();
        }

        private void ShowSortMark()
        {
            foreach (DataGridViewColumn c in _grid.Columns)
                c.HeaderCell.SortGlyphDirection = c.Name == _sortColumn ? (_sortDescending ? SortOrder.Descending : SortOrder.Ascending) : SortOrder.None;
        }

        private string CellText(TranslationEdits.Row r, string column)
        {
            switch (column)
            {
                case "Key": return r.Key;
                case "Owner": return OwnerName(r.Owner);
                case "File": return r.File;
                case "Changed": return ChangedText(r);
                default: return column.StartsWith(LangPrefix) ? TextOf(r, column.Substring(LangPrefix.Length)) : "";
            }
        }

        private string ChangedText(TranslationEdits.Row r)
        {
            TranslationEdits.Edit last = null;
            foreach (var l in _langs)
            {
                TranslationEdits.Edit e;
                if (_edits.TryGetValue(Id(r.Owner, r.Key, l), out e) && (last == null || e.Time > last.Time)) last = e;
            }
            if (last == null) return "";
            return last.User + "  " + last.Time.ToString(ViewPreferences.DatePattern + " " + ViewPreferences.TimePattern(false), CultureInfo.CurrentCulture);
        }

        private void OnCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _shown.Count || e.ColumnIndex < 0) return;
            e.Value = CellText(_shown[e.RowIndex], _grid.Columns[e.ColumnIndex].Name);
        }

        private void OnCellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _shown.Count || !IsLanguage(e.ColumnIndex)) return;
            Type(_shown[e.RowIndex], LangOf(_grid.Columns[e.ColumnIndex]), Convert.ToString(e.Value ?? ""));
        }

        /// <summary>A text typed in a cell: kept until Save (typed back as it is in force: nothing to save).</summary>
        internal void Type(TranslationEdits.Row row, string lang, string text)
        {
            text = TranslationEdits.Clean(text);
            string id = Id(row.Owner, row.Key, lang);
            string was;
            if (!_pending.TryGetValue(id, out was)) was = null;
            _pending.Remove(id);
            if (text != TextOf(row, lang)) _pending[id] = text;
            string now;
            if (!_pending.TryGetValue(id, out now)) now = null;
            var cell = TypedCell(id, was, now);
            if (cell != null)
            {
                var step = new UndoStep { What = string.Format(T("Undo_TrnTyped", "the text typed in {0} {1}"), row.Key, lang) };
                step.Cells.Add(cell);
                PushStep(step);
            }
            if (_pending.Count > 0) _dirty.MarkDirty(); else _dirty.Reset();
            _grid.InvalidateRow(_shown.IndexOf(row) >= 0 ? _shown.IndexOf(row) : 0);
            UpdateStatus();
        }

        private void OnCellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (!IsLanguage(e.ColumnIndex) || !_grid.IsCurrentCellInEditMode) return;
            if (Convert.ToString(e.FormattedValue ?? "").Contains(";"))
            {
                e.Cancel = true;
                SetStatus(T("Msg_TrnNoSemicolon", "A text cannot have ; in it - it separates the languages in the translation files."), true);
            }
        }

        /// <summary>Delete on a language's cell empties it (the screen shows English there).</summary>
        private void OnGridKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete || e.Modifiers != Keys.None || _grid.IsCurrentCellInEditMode) return;
            var c = _grid.CurrentCell;
            if (c == null || !IsLanguage(c.ColumnIndex) || _grid.Columns[c.ColumnIndex].ReadOnly || c.RowIndex >= _shown.Count) return;
            Type(_shown[c.RowIndex], LangOf(_grid.Columns[c.ColumnIndex]), "");
            e.Handled = true;
        }

        private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _shown.Count || !IsLanguage(e.ColumnIndex)) return;
            var row = _shown[e.RowIndex];
            string id = Id(row.Owner, row.Key, LangOf(_grid.Columns[e.ColumnIndex]));
            if (_marked != null && string.Equals(_marked, row.Owner + "\u0001" + row.Key, StringComparison.OrdinalIgnoreCase) && !_pending.ContainsKey(id))
                e.CellStyle.BackColor = Color.FromArgb(214, 232, 252);                                  // the row asked for
            if (_pending.ContainsKey(id)) e.CellStyle.BackColor = Color.FromArgb(255, 226, 190);          // typed, not saved
            else if (_edits.ContainsKey(id)) e.CellStyle.BackColor = Color.FromArgb(255, 246, 200);       // edited here, in force
            else if (string.IsNullOrWhiteSpace(Convert.ToString(e.Value))) e.CellStyle.BackColor = Color.FromArgb(253, 235, 235);   // missing
        }

        private void UpdateStatus()
        {
            string text = string.Format(T("Lbl_TrnCount", "{0} of {1} keys, {2} languages"), _shown.Count, _rows.Count, _langs.Count);
            if (_pending.Count > 0) text += "  ·  " + string.Format(T("Lbl_TrnPending", "{0} text(s) typed, not saved"), _pending.Count);
            SetStatus(text, false);
        }

        private void SetStatus(string text, bool warning)
        {
            _status.Text = text;
            _status.ForeColor = warning ? Color.FromArgb(180, 40, 40) : Color.FromArgb(70, 76, 86);
        }

        private void ClearFilters()
        {
            _filling = true;
            _search.Text = "";
            _owner.SelectedIndex = 0;
            _onlyEdited.Checked = false;
            _missing.Checked = false;
            _filling = false;
            ApplyFilter();
        }

        /// <summary>Goes to a key ("owner\u0001key"), filtered or not.</summary>
        private void Show(string ownerKey)
        {
            int i = _shown.FindIndex(r => string.Equals(r.Owner + "\u0001" + r.Key, ownerKey, StringComparison.OrdinalIgnoreCase));
            if (i < 0) return;
            try
            {
                _grid.CurrentCell = _grid.Rows[i].Cells[_grid.CurrentCell != null ? _grid.CurrentCell.ColumnIndex : 0];
                _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, i - 3);
            }
            catch { }
        }

        /// <summary>
        /// A key asked for from elsewhere (21 October: the notes page's texts "a link to open the
        /// translation page - the item highlighted there to edit it directly"): every row listed,
        /// the key's row marked and chosen, in the screen's language's cell (its machine's row
        /// before the general one: the text on the screen is the machine's). False: no such key.
        /// </summary>
        public bool ShowKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            var rows = _rows.Where(r => string.Equals(r.Key, key.Trim(), StringComparison.OrdinalIgnoreCase)).OrderBy(r => r.Common ? 1 : 0).ToList();
            if (rows.Count == 0) return false;
            var row = rows[0];
            ClearFilters();
            _marked = row.Owner + "\u0001" + row.Key;
            int i = _shown.IndexOf(row);
            if (i < 0) return false;
            string lang = (LanguageManager.CurrentLanguage ?? "EN").ToUpperInvariant();
            var column = _grid.Columns.Contains(LangPrefix + lang) ? _grid.Columns[LangPrefix + lang] : _grid.Columns[LangPrefix + "EN"];
            try
            {
                _grid.CurrentCell = _grid.Rows[i].Cells[column.Index];
                _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, i - 3);
            }
            catch { }
            _grid.Invalidate();
            if (IsHandleCreated) BeginInvoke((Action)(() => { if (!IsDisposed && _grid.CanFocus) _grid.Focus(); }));
            SetStatus(string.Format(T("Msg_TrnShown", "{0}: its texts - type in a cell to change one, then Save."), row.Key), false);
            return true;
        }

        /// <summary>The key of the row marked (tests).</summary>
        internal string Marked { get { return _marked; } }

        /// <summary>Where the selected row's key is used: a list, a page of it opened on a double click.</summary>
        private void FindUses()
        {
            var row = CurrentRow;
            if (row == null) { SetStatus(T("Msg_TrnChooseRow", "Choose a row first."), true); return; }
            List<Kaban.UI.Helpers.TextUses.Use> uses;
            var cursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try { uses = Kaban.UI.Helpers.TextUses.Find(row.Key); }
            finally { Cursor.Current = cursor; }
            using (var f = new TranslationUsesForm(row.Key, TextOf(row, (LanguageManager.CurrentLanguage ?? "EN").ToUpperInvariant()), uses))
            {
                if (f.ShowDialog(FindForm()) != DialogResult.OK || f.Chosen == null || f.Chosen.Way == null) return;
                var shell = Application.OpenForms.OfType<Kaban.UI.Forms.MainForm>().FirstOrDefault();
                if (shell == null) return;
                string why;
                var target = shell.ShowDeviceUse(f.Chosen.Way, out why);
                if (target == null) { Ask.Tell(shell, T("Tab_Translations", "Translation"), why); return; }
                shell.BeginInvoke((MethodInvoker)(() => Kaban.UI.Helpers.ControlHighlight.Flash(target)));
            }
        }

        // ---- what the buttons do ---------------------------------------------------------

        /// <summary>The texts typed, in force and kept (the number saved).</summary>
        internal int Save()
        {
            if (_grid.IsCurrentCellInEditMode) _grid.EndEdit();
            if (_pending.Count == 0) { _dirty.Reset(); return 0; }
            var byId = new Dictionary<string, TranslationEdits.Row>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in _rows) foreach (var l in _langs) byId[Id(r.Owner, r.Key, l)] = r;
            var wanted = new List<TranslationEdits.Wanted>();
            foreach (var kv in _pending)
            {
                TranslationEdits.Row row;
                if (!byId.TryGetValue(kv.Key, out row)) continue;
                wanted.Add(new TranslationEdits.Wanted { Row = row, Lang = kv.Key.Substring(kv.Key.LastIndexOf('\u0001') + 1), Text = kv.Value });
            }
            int notWritten;
            var done = new List<TranslationEdits.Change>();
            int n = TranslationEdits.Save(wanted, out notWritten, null, true, done);
            _pending.Clear();
            // the typing is written now: one step, the Save (undone: the texts before it back in their files)
            _undoSteps.RemoveAll(st => !st.Written);
            PushWritten(T("Undo_TrnSave", "the Save"), done);
            _dirty.Reset();
            _edits = TranslationEdits.Edits.ToDictionary(e => Id(e.Owner, e.Key, e.Lang), e => e, StringComparer.OrdinalIgnoreCase);
            ApplyFilter();
            SetStatus(string.Format(T("Msg_TrnSaved", "{0} text(s) saved into the translation files and in force - the history has them."), n) +
                      (notWritten > 0 ? "  " + string.Format(T("Msg_TrnNotWritten", "{0} in force only: no file could take them."), notWritten) : ""), notWritten > 0);
            return n;
        }

        private void Revert()
        {
            if (_pending.Count == 0) return;
            if (!Ask.YesNo(this, T("Tab_Translations", "Translation"),
                           string.Format(T("Msg_TrnRevert", "Throw away the {0} text(s) typed and not saved?"), _pending.Count))) return;
            _pending.Clear();
            _dirty.Reset();
            _undoSteps.RemoveAll(st => !st.Written);         // the typing thrown away: nothing of it to undo
            UpdateUndoButtons();
            ApplyFilter();
        }

        /// <summary>➕: the dialog - the code, and a language whose texts it begins with.</summary>
        internal void AddLanguage()
        {
            string code, from, name;
            if (!TranslationLanguageForm.Run(FindForm() ?? (IWin32Window)this, _langs, TextsIn, out code, out from, out name)) return;
            AddLanguage(code, from, name);
        }

        /// <summary>A language added, its name kept; copyFrom's texts typed into it (null: none - English shown until typed).</summary>
        internal void AddLanguage(string code, string copyFrom, string name = null)
        {
            if (TranslationLanguageForm.Problem(code, _langs) != null) return;
            code = code.Trim().ToUpperInvariant();
            TranslationEdits.AddLanguage(code, null, string.IsNullOrWhiteSpace(name) ? TranslationLanguageForm.NameFor(code) : name);
            RefreshLanguages();
            int n = string.IsNullOrEmpty(copyFrom) ? 0 : CopyLanguage(copyFrom, new[] { code }, false);
            SetStatus(n > 0
                ? string.Format(T("Msg_TrnLangAddedCopied", "{0} added: {1} text(s) of {2} copied into it - typed (orange) until Save writes them into the files."), code, n, copyFrom)
                : string.Format(T("Msg_TrnLangAdded", "{0} added: its texts are English until typed - the language list offers it."), code), false);
        }

        /// <summary>The keys with a text in a language (as the cells show them).</summary>
        private int TextsIn(string lang)
        {
            return _rows.Count(r => TextOf(r, lang).Trim().Length > 0);
        }

        /// <summary>The empty cells of a language a copy of another's texts fills.</summary>
        internal int Fillable(string from, string to, bool shownOnly)
        {
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return 0;
            return (shownOnly ? _shown : _rows).Count(r => TextOf(r, from).Trim().Length > 0 && TextOf(r, to).Trim().Length == 0);
        }

        /// <summary>
        /// A language's texts typed into the EMPTY cells of others (of every row, or of the rows
        /// shown) - a cell with a text left as it is; Save writes them. The number of cells filled.
        /// </summary>
        internal int CopyLanguage(string from, IEnumerable<string> to, bool shownOnly)
        {
            if (_grid.IsCurrentCellInEditMode) _grid.EndEdit();
            from = (from ?? "").Trim().ToUpperInvariant();
            var targets = (to ?? Enumerable.Empty<string>()).Select(t => (t ?? "").Trim().ToUpperInvariant())
                          .Where(t => t != from && _langs.Contains(t, StringComparer.OrdinalIgnoreCase)).Distinct().ToList();
            if (!_langs.Contains(from, StringComparer.OrdinalIgnoreCase) || targets.Count == 0) return 0;
            int n = 0;
            var step = new UndoStep { What = string.Format(T("Undo_TrnCopyLang", "{0} copied into the empty cells of {1}"), from, string.Join(", ", targets)) };
            foreach (var r in (shownOnly ? _shown : _rows).ToList())
            {
                var text = TextOf(r, from);
                if (text.Trim().Length == 0) continue;
                foreach (var t in targets)
                    if (TextOf(r, t).Trim().Length == 0)
                    {
                        string id = Id(r.Owner, r.Key, t), was;
                        if (!_pending.TryGetValue(id, out was)) was = null;
                        _pending[id] = text;
                        n++;
                        var cell = TypedCell(id, was, text);
                        if (cell != null) step.Cells.Add(cell);
                    }
            }
            PushStep(step);
            if (n > 0) _dirty.MarkDirty();
            _grid.Invalidate();
            UpdateStatus();
            return n;
        }

        /// <summary>📋: the dialog - from which language into which ones' empty cells.</summary>
        private void CopyLanguages()
        {
            if (_langs.Count < 2) return;
            string current = _grid.CurrentCell != null && IsLanguage(_grid.CurrentCell.ColumnIndex) ? LangOf(_grid.Columns[_grid.CurrentCell.ColumnIndex]) : "EN";
            using (var dlg = new TranslationCopyForm(_langs, current, _shown.Count, _rows.Count, Fillable))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                int n = CopyLanguage(dlg.From, dlg.To, dlg.ShownOnly);
                SetStatus(string.Format(T("Msg_TrnCopied", "{0} empty cell(s) of {1} filled with the texts of {2} - typed (orange) until Save."), n,
                                        string.Join(", ", dlg.To), dlg.From), n == 0);
            }
        }

        private ContextMenuStrip DeleteLanguageMenu()
        {
            var m = UiStyle.Menu();
            foreach (var l in _langs.Where(l => l != "EN"))
            {
                var code = l;
                m.Items.Add(new ToolStripMenuItem(code + "  –  " + Kaban.UI.Helpers.AlarmAiPrompt.LanguageName(code), null, (s, e) => DeleteLanguage(code)));
            }
            if (m.Items.Count == 0) m.Items.Add(new ToolStripMenuItem(T("Msg_TrnOnlyEnglish", "English only: it stays (every text's last resort)")) { Enabled = false });
            return m;
        }

        internal void DeleteLanguage(string code)
        {
            if (string.IsNullOrEmpty(code) || code == "EN") return;
            int typed = _pending.Keys.Count(k => k.EndsWith("\u0001" + code, StringComparison.OrdinalIgnoreCase));
            var question = string.Format(T("Msg_TrnLangDelete",
                "Delete {0}? It is no longer offered and its column goes; its texts stay in the translation files (and its edits here), so adding {0} again brings them back."), code);
            if (typed > 0) question += "\r\n\r\n" + string.Format(T("Msg_TrnLangDeleteTyped", "The {0} text(s) typed in {1} and not saved are thrown away."), typed, code);
            if (!Ask.YesNo(this, T("Tip_TrnDelLang", "Delete a language (not English)"), question)) return;
            foreach (var k in _pending.Keys.Where(k => k.EndsWith("\u0001" + code, StringComparison.OrdinalIgnoreCase)).ToList()) _pending.Remove(k);
            if (_pending.Count == 0) _dirty.Reset();
            TranslationEdits.RemoveLanguage(code);
            RefreshLanguages();
        }

        /// <summary>Texts put in force elsewhere (a note applied, the files read again): the rows as the files have them now - the texts typed kept.</summary>
        private void OnTextsChanged(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed) return;
                if (_grid.IsCurrentCellInEditMode) _grid.EndEdit();
                LoadRows();
                ApplyFilter();
            }));
        }

        private void OnLanguagesChanged(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)(() => { if (!IsDisposed && !TranslationEdits.Languages().SequenceEqual(_langs)) RefreshLanguages(); }));
        }

        private void RefreshLanguages()
        {
            if (_grid.IsCurrentCellInEditMode) _grid.EndEdit();
            _grid.RowCount = 0;
            BuildColumns();
            UiStyle.ReapplyLayout(_grid, "Translations");
            ApplyFilter();
        }

        private void ShowHistory()
        {
            using (var f = new TranslationHistoryForm())
            {
                var chosen = f.ShowDialog(FindForm());
                if (f.WasReset) { LoadRows(); ApplyFilter(); }
                if (chosen != DialogResult.OK || f.ChosenKey == null) return;
                ClearFilters();
                SearchText = f.ChosenKey;
            }
        }

        // ---- the AI (22 October) -----------------------------------------------------------

        /// <summary>The languages whose columns are shown (the AI is asked about them, an export writes them).</summary>
        private List<string> ShownLanguages()
        {
            return _langs.Where(l => _grid.Columns.Contains(LangPrefix + l) && _grid.Columns[LangPrefix + l].Visible).ToList();
        }

        /// <summary>The rows the AI is asked about: the rows shown - for the missing texts, those with an empty cell.</summary>
        internal List<TranslationEdits.Row> AiRows(TranslationAiPrompt.Mode mode)
        {
            var langs = ShownLanguages();
            return _shown.Where(r => mode == TranslationAiPrompt.Mode.Check || langs.Any(l => TextOf(r, l).Trim().Length == 0)).ToList();
        }

        /// <summary>The briefing for an AI chat (the rows of AiRows, the languages shown).</summary>
        internal string AiPrompt(TranslationAiPrompt.Mode mode)
        {
            return TranslationAiPrompt.Build(AiRows(mode), ShownLanguages(), mode, TextOf);
        }

        private ContextMenuStrip AiMenu()
        {
            var m = UiStyle.Menu();
            int missing = AiRows(TranslationAiPrompt.Mode.Missing).Count;
            m.Items.Add(new ToolStripMenuItem(string.Format(T("Lbl_TrnAiRows", "The {0} rows shown - {1} with a text missing (the search and the filters choose them)"),
                                                            _shown.Count, missing)) { Enabled = false });
            m.Items.Add(new ToolStripSeparator());
            foreach (var mode in new[] { TranslationAiPrompt.Mode.Missing, TranslationAiPrompt.Mode.Check })
            {
                var md = mode;
                var item = new ToolStripMenuItem(md == TranslationAiPrompt.Mode.Missing
                    ? T("Mnu_TrnAiMissing", "Translate the missing texts")
                    : T("Mnu_TrnAiCheck", "Check every translation (and translate the missing)"));
                item.ToolTipText = md == TranslationAiPrompt.Mode.Missing
                    ? T("Tip_TrnAiMissing", "The AI fills the empty cells of the rows shown - the best text for where each one is shown")
                    : T("Tip_TrnAiCheck", "The AI checks every text of the rows shown for where it is shown and gives a better one where it can - the empty ones too");
                item.Enabled = md == TranslationAiPrompt.Mode.Check ? _shown.Count > 0 : missing > 0;
                foreach (var entry in Kaban.UI.Helpers.AiMenu.Items(() => AskedPrompt(md), Kaban.UI.Helpers.AlarmAiPrompt.Subject.Translation, FindForm() ?? (IWin32Window)this))
                    item.DropDownItems.Add(entry);
                m.Items.Add(item);
            }
            return m;
        }

        /// <summary>The prompt as asked from the menu: none when no row is asked about; many keys asked first.</summary>
        private string AskedPrompt(TranslationAiPrompt.Mode mode)
        {
            var rows = AiRows(mode);
            if (rows.Count == 0)
            {
                SetStatus(T("Msg_TrnAiNothing", "Nothing to ask: no row shown has a text missing."), true);
                return null;
            }
            if (rows.Count > TranslationAiPrompt.ManyKeys &&
                !Ask.YesNo(this, T("Tab_Translations", "Translation"), string.Format(T("Msg_TrnAiMany",
                    "{0} keys: an AI answers a few hundred at a time, so it gives them in parts (it asks you to go on). A smaller part - an owner, a search, " +
                    "'A text missing' - is answered better. Ask about all {0}?"), rows.Count)))
                return null;
            return TranslationAiPrompt.Build(rows, ShownLanguages(), mode, TextOf);
        }

        /// <summary>The rows shown, in the translation files' format (Key;EN;TR ...: the languages shown) - the texts as the cells show them.</summary>
        internal void ExportTo(string path)
        {
            var langs = _langs.Where(l => _grid.Columns[LangPrefix + l].Visible).ToList();
            var sb = new StringBuilder();
            sb.Append("Key;").Append(string.Join(";", langs)).Append("\r\n");
            foreach (var r in _shown)
                sb.Append(r.Key).Append(';').Append(string.Join(";", langs.Select(l => TranslationEdits.Clean(TextOf(r, l)).Replace("\r\n", "\\r\\n").Replace("\n", "\\n"))))
                  .Append("\r\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private void Export()
        {
            if (_shown.Count == 0) { SetStatus(T("Msg_TrnNothing", "Nothing to export: no row passes the filters."), true); return; }
            string owner = SelectedOwner;
            using (var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = (owner == null || owner.StartsWith("*") ? "Translations" : owner == TranslationEdits.General ? "CommonTranslations" : owner + "_Translations") +
                           (_onlyEdited.Checked ? "_edited" : "") + ".csv"
            })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    ExportTo(dlg.FileName);
                    SetStatus(string.Format(T("Msg_TrnExported", "{0} rows written to {1}."), _shown.Count, dlg.FileName), false);
                }
                catch (Exception ex) { SetStatus(ex.Message, true); }
            }
        }
    }

    /// <summary>
    /// The translations' EDIT HISTORY: every text changed on HMI Settings > Translation - when,
    /// who, whose, the key, the language, before and after - newest first; a language added or
    /// deleted too. A double click goes to the key in the table.
    /// </summary>
    public class TranslationHistoryForm : Form
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _search = new TextBox();
        private readonly ToolTip _tips = UiStyle.Tips();
        private List<TranslationEdits.Change> _all;
        private readonly Button _reset;

        /// <summary>The key double clicked (null: none).</summary>
        public string ChosenKey { get; private set; }
        /// <summary>The history emptied while open (the table's edited marks to read again).</summary>
        public bool WasReset { get; private set; }

        private static string T(string key, string fallback) { return LanguageManager.GetString(key, fallback); }

        public TranslationHistoryForm()
        {
            UiScale.ApplyOnLoad(this);
            Text = T("Ttl_TrnHistory", "Translation - edit history");
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 600);
            MinimumSize = new Size(700, 380);
            ShowInTaskbar = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = UiStyle.Body;

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(4, 4, 4, 4) };
            bar.Controls.Add(new Label { Text = "🔍", AutoSize = true, Margin = new Padding(4, 9, 2, 3) });
            _search.Width = 240;
            _search.Margin = new Padding(0, 5, 3, 3);
            _search.TextChanged += (s, e) => Fill();
            bar.Controls.Add(_search);
            var export = UiStyle.ToolButton(char.ConvertFromUtf32(0x1F4E4), T("Tip_TrnHistoryExport", "Export the changes shown (CSV)"), _tips, (s, e) => Export());
            export.Margin = new Padding(8, 1, 3, 1);
            bar.Controls.Add(export);
            // 22 October: "the admin user should be able to reset the edit history list as well"
            _reset = UiStyle.ToolButton(char.ConvertFromUtf32(0x1F5D1), T("Tip_TrnHistoryReset",
                "Reset the history (Admin): the list emptied and kept as a .bak file beside it - the texts stay as they are, none marked edited any more"),
                _tips, (s, e) => ResetHistory());
            _reset.Margin = new Padding(3, 1, 3, 1);
            _reset.Name = "trnHistoryReset";
            _reset.Visible = SecurityManager.HasAccess(SecurityLevel.Admin);
            Kaban.UI.Helpers.Shortcuts.Set(_reset, Keys.None, _tips, T("Tip_TrnHistoryReset",
                "Reset the history (Admin): the list emptied and kept as a .bak file beside it - the texts stay as they are, none marked edited any more"));
            bar.Controls.Add(_reset);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = T("Log_ColTime", "Time"), Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "User", HeaderText = T("Log_ColUser", "User"), Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Owner", HeaderText = T("Col_TrnOwner", "Owner"), Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", HeaderText = T("Col_TrnKey", "Key"), Width = 200 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Lang", HeaderText = T("Col_TrnLang", "Language"), Width = 80 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Old", HeaderText = T("Col_TrnOld", "Before"), Width = 230 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "New", HeaderText = T("Col_TrnNew", "After"), Width = 230 });
            UiStyle.EnableTableTools(_grid, "TranslationHistory");
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var key = Convert.ToString(_grid.Rows[e.RowIndex].Cells["Key"].Value);
                if (string.IsNullOrEmpty(key)) return;
                ChosenKey = key;
                DialogResult = DialogResult.OK;
            };

            var close = UiStyle.DialogButton(T("Btn_Close", "Close"), DialogResult.Cancel);
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(6) };
            bottom.Controls.Add(close);
            CancelButton = close;
            Controls.Add(_grid);
            Controls.Add(bottom);
            Controls.Add(bar);

            _all = TranslationEdits.History();
            _all.Reverse();
            Fill();
        }

        internal DataGridView Grid { get { return _grid; } }
        internal Button ResetButton { get { return _reset; } }

        /// <summary>🗑 (an Admin): the history emptied once asked - false when not.</summary>
        internal bool ResetHistory()
        {
            if (!SecurityManager.HasAccess(SecurityLevel.Admin)) return false;
            int count = TranslationEdits.History().Count;
            if (count == 0) return false;
            if (!Ask.YesNo(this, T("Ttl_TrnHistory", "Translation - edit history"), string.Format(T("Msg_TrnHistoryReset",
                "Empty the edit history ({0} change(s))? The texts stay in the translation files as they are - the table marks none as edited any more. " +
                "The list is kept as a TranslationHistory_....bak file beside it."), count))) return false;
            if (TranslationEdits.ResetHistory() < 0)
            {
                Ask.Tell(this, T("Ttl_TrnHistory", "Translation - edit history"), T("Msg_TrnHistoryResetFailed", "The history could not be reset - the event log says why."));
                return false;
            }
            WasReset = true;
            _all = TranslationEdits.History();
            Fill();
            return true;
        }

        private IEnumerable<TranslationEdits.Change> Shown()
        {
            var words = (_search.Text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return _all.Where(c => words.All(w => (c.User + " " + c.Owner + " " + c.Key + " " + c.Lang + " " + c.Old + " " + c.New)
                                                   .IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private void Fill()
        {
            _grid.Rows.Clear();
            foreach (var c in Shown())
                _grid.Rows.Add(c.Time.ToString(ViewPreferences.DatePattern + " " + ViewPreferences.TimePattern(), CultureInfo.CurrentCulture), c.User,
                               c.Key.Length == 0 ? "" : c.Owner == TranslationEdits.General ? T("Lbl_TrnGeneral", "General") : c.Owner, c.Key, c.Lang,
                               c.Key.Length == 0 ? "" : c.Old, c.Key.Length == 0 ? (c.New == "added" ? T("Lbl_TrnLangAdded", "language added") : T("Lbl_TrnLangDeleted", "language deleted")) : c.New);
        }

        private void Export()
        {
            using (var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "TranslationHistory.csv" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var sb = new StringBuilder("Time;User;Owner;Key;Lang;Old;New\r\n");
                foreach (var c in Shown())
                    sb.Append(string.Join(";", c.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), c.User, c.Owner, c.Key, c.Lang,
                                          TranslationEdits.Clean(c.Old).Replace("\r\n", "\\r\\n"), TranslationEdits.Clean(c.New).Replace("\r\n", "\\r\\n"))).Append("\r\n");
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            }
        }
    }

    /// <summary>
    /// WHERE A KEY IS USED (Ctrl+F on the Translation page): the menu's buttons and the tabs it
    /// titles, the pages' controls using it, its alarms, the program's lines naming it; a page's
    /// use opened (and pointed at) with a double click.
    /// </summary>
    public class TranslationUsesForm : Form
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _status = new Label();
        private readonly List<Kaban.UI.Helpers.TextUses.Use> _uses;

        /// <summary>The use chosen to open (null: none).</summary>
        public Kaban.UI.Helpers.TextUses.Use Chosen { get; private set; }

        private static string T(string key, string fallback) { return LanguageManager.GetString(key, fallback); }

        public TranslationUsesForm(string key, string text, List<Kaban.UI.Helpers.TextUses.Use> uses)
        {
            _uses = uses ?? new List<Kaban.UI.Helpers.TextUses.Use>();
            UiScale.ApplyOnLoad(this);
            Text = T("Ttl_TrnUses", "Where is it used?") + " - " + key;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 560);
            MinimumSize = new Size(640, 320);
            ShowInTaskbar = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = UiStyle.Body;

            var head = new Label
            {
                Dock = DockStyle.Top, Height = 34, Padding = new Padding(8, 8, 8, 0), AutoEllipsis = true,
                Text = key + "   \u00B7   \u201C" + (text ?? "").Replace("\r\n", " ") + "\u201D"
            };
            head.Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kind", HeaderText = T("Col_FndKind", "Where"), Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Place", HeaderText = T("Col_FndPlace", "Page or file"), Width = 320 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "What", HeaderText = T("Col_FndWhat", "What uses it"), Width = 560 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Line", HeaderText = T("Col_FndLine", "Line"), Width = 60 });
            foreach (var u in _uses)
                _grid.Rows.Add(KindName(u), u.Place, u.What + (u.ByText ? "  " + T("Fnd_ByText", "(shows the same text)") : ""), u.Line > 0 ? u.Line.ToString(CultureInfo.InvariantCulture) : "");
            UiStyle.EnableTableTools(_grid, "TranslationUses");
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Open(e.RowIndex); };
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && _grid.CurrentRow != null) { Open(_grid.CurrentRow.Index); e.Handled = true; } };

            _status.Dock = DockStyle.Bottom;
            _status.Height = 44;
            _status.Padding = new Padding(8, 4, 8, 0);
            _status.ForeColor = Color.FromArgb(70, 76, 86);
            bool source = Kaban.UI.Helpers.TextUses.LinesFrom() != null;
            _status.Text = (_uses.Count == 0 ? T("Msg_FndNone", "Not used on a page, in the menu or by an alarm.") + "  "
                                             : T("Msg_FndOpen", "A double click opens a page and points at what uses it.") + "  ") +
                           (source ? "" : T("Msg_FndNoSource", "The program's lines (message boxes, status lines ...) are listed from its source or from TranslationUses.csv beside it - neither is here."));

            var close = UiStyle.DialogButton(T("Btn_Close", "Close"), DialogResult.Cancel);
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(6) };
            bottom.Controls.Add(close);
            CancelButton = close;
            Controls.Add(_grid);
            Controls.Add(_status);
            Controls.Add(bottom);
            Controls.Add(head);
        }

        internal DataGridView Grid { get { return _grid; } }

        private static string KindName(Kaban.UI.Helpers.TextUses.Use u)
        {
            switch (u.Kind)
            {
                case Kaban.UI.Helpers.TextUses.KindMenu: return T("Fnd_KindMenu", "Menu");
                case Kaban.UI.Helpers.TextUses.KindTab: return T("Fnd_KindTab", "Tab");
                case Kaban.UI.Helpers.TextUses.KindPage: return T("Fnd_KindPage", "Page");
                case Kaban.UI.Helpers.TextUses.KindAlarm: return T("Fnd_KindAlarm", "Alarm");
                default: return T("Fnd_KindCode", "Program");
            }
        }

        private void Open(int row)
        {
            if (row < 0 || row >= _uses.Count) return;
            var u = _uses[row];
            if (u.Way == null)
            {
                _status.Text = u.Kind == Kaban.UI.Helpers.TextUses.KindCode
                    ? string.Format(T("Msg_FndCodeLine", "A line of the program: {0}, line {1}."), u.Place, u.Line)
                    : T("Msg_FindNoPage", "It is not on a page (an alarm definition).");
                return;
            }
            Chosen = u;
            DialogResult = DialogResult.OK;
        }
    }

    /// <summary>HMI Settings > Translation (Maintenance and above).</summary>
    public class TranslationsPage : ISettingsPage
    {
        public string TitleKey { get { return "Tab_Translations"; } }
        public string TitleFallback { get { return "Translation"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Maintenance; } }
        public UserControl GetControl() { return new TranslationsControl(); }
    }
}
