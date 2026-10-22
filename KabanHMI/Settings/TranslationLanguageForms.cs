using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using Kaban.UI.Profiles;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// ADD A LANGUAGE (HMI Settings > Translation, ➕): its code, and the texts it begins with -
    /// none (the screen shows English until a text is typed) or a language's copied into it (22
    /// October: "it should also ask if the available columns are to be copied when a new language
    /// is added, on the Add a language dialog"). The copies are typed texts until Save.
    /// Its name too (22 October: "we only enter 2-3 letters - how will the program know which
    /// language it is, to ask the AI to translate into it?"): the system's name of the code
    /// offered, to correct - kept with the language, the AI asked by it.
    /// </summary>
    public class TranslationLanguageForm : Form
    {
        private readonly TextBox _code = new TextBox();
        private readonly TextBox _name = new TextBox();
        private bool _nameGiven, _naming;
        private readonly ComboBox _from = new ComboBox();
        private readonly Label _error = new Label();
        private readonly IList<string> _existing;

        /// <summary>The code given (upper case).</summary>
        public string Code { get; private set; }
        /// <summary>The language whose texts are copied into it (null: none).</summary>
        public string CopyFrom { get; private set; }
        /// <summary>Its name, in English.</summary>
        public string LanguageName { get; private set; }

        private static string T(string key, string fallback) { return LanguageManager.GetString(key, fallback); }
        internal static string Title { get { return T("Tip_TrnAddLang", "Add a language"); } }
        internal static string CodePrompt { get { return T("Msg_TrnLangCode", "The language's code - 2 or 3 letters (FR for French, PL for Polish):"); } }
        internal static string FromPrompt { get { return T("Msg_TrnLangFrom", "Its texts to begin with - a language's copied into it (its code; empty: none):"); } }
        internal static string NamePrompt { get { return T("Msg_TrnLangName", "Its name, in English - the AI translates into it by this name, the lists show it:"); } }

        /// <param name="languages">The languages there (the code may be none of them).</param>
        /// <param name="texts">A language - the number of keys with a text in it (what a copy brings).</param>
        public TranslationLanguageForm(IList<string> languages, Func<string, int> texts)
        {
            _existing = languages ?? new List<string>();
            UiScale.ApplyOnLoad(this);
            Text = Title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = UiStyle.Body;
            ClientSize = new Size(500, 404);

            var prompt = new Label { Text = CodePrompt, Left = 14, Top = 12, Width = 470, Height = 22 };
            _code.SetBounds(14, 38, 90, 26);
            _code.CharacterCasing = CharacterCasing.Upper;
            _code.MaxLength = 3;
            _code.TextChanged += (s, e) => { _error.Text = ""; ShowName(); };
            var namePrompt = new Label { Text = NamePrompt, Left = 14, Top = 76, Width = 470, Height = 40 };
            _name.SetBounds(14, 118, 300, 26);
            _name.TextChanged += (s, e) => { _error.Text = ""; if (!_naming) _nameGiven = _name.Text.Trim().Length > 0; };
            var codes = new Label
            {
                Text = T("Lbl_TrnLangCodes", "The code is the language's own (ISO 639-1), not its country's: Swedish SV (not SE), Chinese ZH, Japanese JA, " +
                                             "Greek EL, Czech CS, Danish DA, Ukrainian UK."),
                Left = 14, Top = 150, Width = 470, Height = 58, ForeColor = Color.FromArgb(90, 96, 106)
            };

            var begin = new Label { Text = T("Lbl_TrnLangBegin", "Its texts to begin with:"), Left = 14, Top = 214, Width = 470, Height = 22 };
            _from.SetBounds(14, 238, 470, 28);
            _from.DropDownStyle = ComboBoxStyle.DropDownList;
            _from.Items.Add(new Option(null, T("Lbl_TrnLangNone", "None - the screen shows English until a text is typed")));
            foreach (var l in _existing)
                _from.Items.Add(new Option(l, string.Format(T("Lbl_TrnLangCopyOf", "{0} - {1}: its {2} text(s) copied"), l, AlarmAiPrompt.LanguageName(l),
                                                            texts != null ? texts(l) : 0)));
            _from.SelectedIndex = 0;
            var note = new Label
            {
                Text = T("Lbl_TrnCopyNote", "The copies are typed texts (orange): check or translate them, then Save writes them into the translation files."),
                Left = 14, Top = 272, Width = 470, Height = 40, ForeColor = Color.FromArgb(90, 96, 106)
            };
            _error.SetBounds(14, 318, 470, 22);
            _error.ForeColor = Color.FromArgb(190, 30, 30);

            var ok = UiStyle.DialogButton(T("Btn_Ok", "OK"), DialogResult.None);
            var cancel = UiStyle.DialogButton(T("Btn_Cancel", "Cancel"), DialogResult.Cancel);
            ok.Location = new Point(ClientSize.Width - 14 - cancel.Width - 8 - ok.Width, 354);
            cancel.Location = new Point(ClientSize.Width - 14 - cancel.Width, 354);
            ok.Click += (s, e) => Accept();
            Controls.AddRange(new Control[] { prompt, _code, namePrompt, _name, codes, begin, _from, note, _error, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private sealed class Option
        {
            public readonly string Code, Text;
            public Option(string code, string text) { Code = code; Text = text; }
            public override string ToString() { return Text; }
        }

        /// <summary>The code's name offered while the user has not written one.</summary>
        private void ShowName()
        {
            if (_nameGiven) return;
            var c = (_code.Text ?? "").Trim().ToUpperInvariant();
            _naming = true;
            _name.Text = c.Length >= 2 ? NameFor(c) ?? "" : "";
            _naming = false;
        }

        /// <summary>The name a code is offered with: the one kept for it, else the system's (null: none known).</summary>
        internal static string NameFor(string code)
        {
            return TranslationEdits.NameOf(code) ?? AlarmAiPrompt.SystemName(code);
        }

        /// <summary>What is wrong with a code and a name (null: it can be added).</summary>
        internal static string Problem(string code, IEnumerable<string> existing, string name = "-")
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (!Regex.IsMatch(code, "^[A-Z]{2,3}$")) return T("Msg_TrnLangBad", "A language's code is 2 or 3 letters (FR, PL ...).");
            if (existing != null && existing.Contains(code, StringComparer.OrdinalIgnoreCase)) return string.Format(T("Msg_TrnLangThere", "{0} is there already."), code);
            if (string.IsNullOrWhiteSpace(name)) return T("Msg_TrnLangNoName", "Write its name too, in English - the AI translates into it by its name.");
            return null;
        }

        // the tests: the dialog filled in without the mouse
        internal string CodeText { get { return _code.Text; } set { _code.Text = (value ?? "").ToUpperInvariant(); } }
        internal string NameText { get { return _name.Text; } set { _name.Text = value ?? ""; } }
        internal ComboBox FromBox { get { return _from; } }
        internal string ErrorText { get { return _error.Text; } }

        /// <summary>OK: closed when the code can be added, else the reason shown.</summary>
        internal bool Accept()
        {
            var why = Problem(_code.Text, _existing, _name.Text);
            if (why != null) { _error.Text = why; (Problem(_code.Text, _existing) == null ? _name : _code).Focus(); return false; }
            Code = _code.Text.Trim().ToUpperInvariant();
            LanguageName = TranslationEdits.Clean(_name.Text);
            var o = _from.SelectedItem as Option;
            CopyFrom = o != null ? o.Code : null;
            DialogResult = DialogResult.OK;
            return true;
        }

        /// <summary>The dialog (a test's Ask.Hook answers the code, the name and the language to copy instead); false: cancelled.</summary>
        public static bool Run(IWin32Window owner, IList<string> languages, Func<string, int> texts, out string code, out string copyFrom, out string name)
        {
            code = copyFrom = name = null;
            if (Ask.Hook != null)
            {
                var c = Ask.Text(owner, Title, CodePrompt);
                if (c == null) return false;
                var n = Ask.Text(owner, Title, NamePrompt, NameFor(c.Trim().ToUpperInvariant()) ?? "");
                var why = Problem(c, languages, n);
                if (why != null) { Ask.Tell(owner, Title, why); return false; }
                code = c.Trim().ToUpperInvariant();
                name = TranslationEdits.Clean(n);
                var f = (Ask.Text(owner, Title, FromPrompt) ?? "").Trim().ToUpperInvariant();
                copyFrom = languages.Contains(f, StringComparer.OrdinalIgnoreCase) ? f : null;
                return true;
            }
            using (var dlg = new TranslationLanguageForm(languages, texts))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return false;
                code = dlg.Code;
                copyFrom = dlg.CopyFrom;
                name = dlg.LanguageName;
                return true;
            }
        }
    }

    /// <summary>
    /// COPY A LANGUAGE'S TEXTS (HMI Settings > Translation, 📋; 22 October: "we should be able to
    /// copy any language column to paste to any column's missing cells"): the texts of one language
    /// into the EMPTY cells of the languages ticked - of every row or of the rows shown. A cell with
    /// a text is left as it is; the copies are typed texts until Save.
    /// </summary>
    public class TranslationCopyForm : Form
    {
        private readonly ComboBox _from = new ComboBox();
        private readonly CheckedListBox _to = new CheckedListBox();
        private readonly CheckBox _shownOnly = new CheckBox();
        private readonly Button _ok;
        private readonly IList<string> _langs;
        private readonly Func<string, string, bool, int> _fillable;
        private bool _filling;

        public string From { get; private set; }
        public List<string> To { get; private set; }
        public bool ShownOnly { get; private set; }

        private static string T(string key, string fallback) { return LanguageManager.GetString(key, fallback); }

        /// <param name="languages">The table's languages.</param>
        /// <param name="from">The language offered first (the current cell's).</param>
        /// <param name="shown">The rows shown and all of them (the filters: fewer shown).</param>
        /// <param name="fillable">(from, to, rows shown only) - the empty cells a copy fills.</param>
        public TranslationCopyForm(IList<string> languages, string from, int shown, int total, Func<string, string, bool, int> fillable)
        {
            _langs = languages ?? new List<string>();
            _fillable = fillable;
            UiScale.ApplyOnLoad(this);
            Text = T("Ttl_TrnCopyLang", "Copy a language's texts into empty cells");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = UiStyle.Body;
            ClientSize = new Size(500, 380);

            var l1 = new Label { Text = T("Lbl_TrnCopyFrom", "Copy the texts of:"), Left = 14, Top = 12, Width = 470, Height = 22 };
            _from.SetBounds(14, 36, 300, 28);
            _from.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var l in _langs) _from.Items.Add(new Lang(l, l + " - " + AlarmAiPrompt.LanguageName(l)));
            var l2 = new Label { Text = T("Lbl_TrnCopyTo", "into the empty cells of:"), Left = 14, Top = 76, Width = 470, Height = 22 };
            _to.SetBounds(14, 100, 470, 132);
            _to.CheckOnClick = true;
            _to.IntegralHeight = false;
            _shownOnly.Text = string.Format(T("Lbl_TrnCopyShown", "Only the {0} rows shown (the search and the filters), not all {1}"), shown, total);
            _shownOnly.SetBounds(14, 240, 470, 24);
            _shownOnly.Checked = shown < total;
            _shownOnly.Enabled = shown < total;
            var note = new Label
            {
                Text = T("Lbl_TrnCopyKeep", "A cell with a text is left as it is. The copies are typed texts (orange) until Save writes them into the translation files."),
                Left = 14, Top = 272, Width = 470, Height = 40, ForeColor = Color.FromArgb(90, 96, 106)
            };

            _ok = UiStyle.DialogButton(T("Btn_TrnCopy", "Copy"), DialogResult.None);
            var cancel = UiStyle.DialogButton(T("Btn_Cancel", "Cancel"), DialogResult.Cancel);
            _ok.Location = new Point(ClientSize.Width - 14 - cancel.Width - 8 - _ok.Width, 330);
            cancel.Location = new Point(ClientSize.Width - 14 - cancel.Width, 330);
            _ok.Click += (s, e) => Accept();
            Controls.AddRange(new Control[] { l1, _from, l2, _to, _shownOnly, note, _ok, cancel });
            AcceptButton = _ok;
            CancelButton = cancel;

            _from.SelectedIndexChanged += (s, e) => FillTargets();
            _shownOnly.CheckedChanged += (s, e) => FillTargets();
            _to.ItemCheck += (s, e) => { if (!_filling) BeginInvoke((Action)UpdateOk); };
            int at = _langs.ToList().FindIndex(l => string.Equals(l, from, StringComparison.OrdinalIgnoreCase));
            _from.SelectedIndex = _langs.Count == 0 ? -1 : Math.Max(0, at);
            FillTargets();
        }

        private sealed class Lang
        {
            public readonly string Code, Text;
            public Lang(string code, string text) { Code = code; Text = text; }
            public override string ToString() { return Text; }
        }

        private string FromCode { get { var l = _from.SelectedItem as Lang; return l != null ? l.Code : null; } }

        /// <summary>The other languages, each with the empty cells the copy would fill (the ticks kept).</summary>
        private void FillTargets()
        {
            var ticked = new HashSet<string>(Ticked(), StringComparer.OrdinalIgnoreCase);
            _filling = true;
            _to.Items.Clear();
            var from = FromCode;
            foreach (var l in _langs.Where(x => !string.Equals(x, from, StringComparison.OrdinalIgnoreCase)))
            {
                int n = _fillable != null && from != null ? _fillable(from, l, _shownOnly.Checked) : 0;
                _to.Items.Add(new Lang(l, string.Format(T("Lbl_TrnCopyCount", "{0} - {1}: {2} empty cell(s) to fill"), l, AlarmAiPrompt.LanguageName(l), n)),
                              ticked.Contains(l));
            }
            _filling = false;
            UpdateOk();
        }

        private IEnumerable<string> Ticked()
        {
            return _to.CheckedItems.OfType<Lang>().Select(x => x.Code).ToList();
        }

        private void UpdateOk() { if (!IsDisposed) _ok.Enabled = FromCode != null && _to.CheckedItems.Count > 0; }

        // the tests: the dialog filled in without the mouse
        internal ComboBox FromBox { get { return _from; } }
        internal CheckedListBox ToBox { get { return _to; } }
        internal CheckBox ShownOnlyBox { get { return _shownOnly; } }
        internal void Tick(string code, bool on)
        {
            for (int i = 0; i < _to.Items.Count; i++)
                if (string.Equals(((Lang)_to.Items[i]).Code, code, StringComparison.OrdinalIgnoreCase)) _to.SetItemChecked(i, on);
            UpdateOk();
        }

        internal bool Accept()
        {
            var to = Ticked().ToList();
            if (FromCode == null || to.Count == 0) return false;
            From = FromCode;
            To = to;
            ShownOnly = _shownOnly.Checked;
            DialogResult = DialogResult.OK;
            return true;
        }
    }
}

namespace Kaban.UI.Settings
{
    /// <summary>
    /// THE CHANGES TO CONFIRM (HMI Settings > Translation: an import, the AI's answer, the texts
    /// into the project's files; 22 October: "apply the translations to the file with a
    /// confirmation system for the changes of any cell - only the empty cells copied directly"):
    /// each text that would change a cell - its key, whose, the language, the text now, the new
    /// one (corrected in its cell when wanted), why (the AI's note, who entered it) - ticked to be
    /// taken. The button says how many are written: the empty cells' and the ticked changes.
    /// </summary>
    public class TranslationReviewForm : Form
    {
        public sealed class Item
        {
            public TranslationEdits.Row Row;
            public string Lang = "", Now = "", New = "", Why = "";
            public bool Take;
        }

        /// <summary>The tests: given the form instead of showing it - true for OK.</summary>
        public static Func<TranslationReviewForm, bool> Hook;

        private readonly List<Item> _items;
        private readonly DataGridView _grid = new DataGridView();
        private readonly Button _ok;
        private readonly int _direct;
        private readonly string _okFormat;
        private bool _filling;

        private static string T(string key, string fallback) { return LanguageManager.GetString(key, fallback); }

        /// <param name="direct">The empty cells written with them (for the button's count).</param>
        /// <param name="okFormat">The button: {0} the empty cells, {1} the changes ticked.</param>
        public TranslationReviewForm(string title, string intro, List<Item> items, int direct, string okFormat)
        {
            _items = items ?? new List<Item>();
            _direct = direct;
            _okFormat = okFormat ?? "{0} + {1}";
            UiScale.ApplyOnLoad(this);
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1150, 640);
            MinimumSize = new Size(760, 400);
            ShowInTaskbar = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = UiStyle.Body;

            var top = new Panel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(10, 8, 10, 4) };
            var text = new Label { Text = intro ?? "", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(60, 66, 76) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 260, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var none = new Button { Text = T("Btn_TrnUntickAll", "Untick all"), AutoSize = true, Name = "reviewNone" };
            var all = new Button { Text = T("Btn_TrnTickAll", "Tick all"), AutoSize = true, Name = "reviewAll" };
            none.Click += (s, e) => TickAll(false);
            all.Click += (s, e) => TickAll(true);
            buttons.Controls.Add(none);
            buttons.Controls.Add(all);
            top.Controls.Add(text);
            top.Controls.Add(buttons);

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Take", HeaderText = T("Col_TrnTake", "Take"), Width = 56 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", HeaderText = T("Col_TrnKey", "Key"), Width = 190, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Owner", HeaderText = T("Col_TrnOwner", "Owner"), Width = 80, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Lang", HeaderText = T("Col_TrnLang", "Language"), Width = 76, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Now", HeaderText = T("Col_TrnNow", "Now"), Width = 250, ReadOnly = true,
                                                              DefaultCellStyle = { WrapMode = DataGridViewTriState.True, ForeColor = Color.FromArgb(110, 116, 126) } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "New", HeaderText = T("Col_TrnNew2", "New"), Width = 250,
                                                              DefaultCellStyle = { WrapMode = DataGridViewTriState.True } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Why", HeaderText = T("Col_TrnWhy", "Why"), Width = 220, ReadOnly = true,
                                                              DefaultCellStyle = { WrapMode = DataGridViewTriState.True, ForeColor = Color.FromArgb(90, 96, 106) } });
            _filling = true;
            foreach (var i in _items)
                _grid.Rows.Add(i.Take, i.Row != null ? i.Row.Key : "", i.Row == null ? "" : i.Row.Owner == TranslationEdits.General ? T("Lbl_TrnGeneral", "General") : i.Row.Owner,
                               i.Lang, i.Now, i.New, i.Why);
            _filling = false;
            _grid.CurrentCellDirtyStateChanged += (s, e) => { if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _grid.CellValueChanged += (s, e) =>
            {
                if (_filling || e.RowIndex < 0 || e.RowIndex >= _items.Count) return;
                var item = _items[e.RowIndex];
                if (_grid.Columns[e.ColumnIndex].Name == "Take") item.Take = Convert.ToBoolean(_grid.Rows[e.RowIndex].Cells["Take"].Value);
                if (_grid.Columns[e.ColumnIndex].Name == "New")
                {
                    item.New = TranslationEdits.Clean(Convert.ToString(_grid.Rows[e.RowIndex].Cells["New"].Value));
                    // a text corrected is a text taken
                    if (!item.Take) { item.Take = true; _filling = true; _grid.Rows[e.RowIndex].Cells["Take"].Value = true; _filling = false; }
                }
                UpdateOk();
            };
            UiStyle.EnableTableTools(_grid, "TranslationReview");

            _ok = UiStyle.DialogButton("", DialogResult.OK);
            _ok.AutoSize = true;
            _ok.Name = "reviewOk";
            var cancel = UiStyle.DialogButton(T("Btn_Cancel", "Cancel"), DialogResult.Cancel);
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(6) };
            bottom.Controls.Add(cancel);
            bottom.Controls.Add(_ok);
            CancelButton = cancel;
            Controls.Add(_grid);
            Controls.Add(bottom);
            Controls.Add(top);
            UpdateOk();
        }

        internal DataGridView Grid { get { return _grid; } }
        internal List<Item> Items { get { return _items; } }
        internal string OkText { get { return _ok.Text; } }
        internal int Ticked { get { return _items.Count(i => i.Take); } }

        /// <summary>Every change ticked, or none.</summary>
        internal void TickAll(bool on)
        {
            if (_grid.IsCurrentCellInEditMode) _grid.EndEdit();
            _filling = true;
            for (int r = 0; r < _items.Count; r++) { _items[r].Take = on; _grid.Rows[r].Cells["Take"].Value = on; }
            _filling = false;
            UpdateOk();
        }

        /// <summary>A change ticked or not (the tests: as a click in its box).</summary>
        internal void Tick(int index, bool on)
        {
            if (index < 0 || index >= _items.Count) return;
            _grid.Rows[index].Cells["Take"].Value = on;
            _items[index].Take = on;
            UpdateOk();
        }

        private void UpdateOk()
        {
            _ok.Text = string.Format(_okFormat, _direct, Ticked);
            _ok.Enabled = _direct + Ticked > 0;
        }

        /// <summary>The changes shown to confirm (a test's Hook instead); false: cancelled.</summary>
        public static bool Review(IWin32Window owner, string title, string intro, List<Item> items, int direct, string okFormat)
        {
            using (var f = new TranslationReviewForm(title, intro, items, direct, okFormat))
            {
                if (Hook != null) return Hook(f);
                if (f.ShowDialog(owner) != DialogResult.OK) return false;
                if (f._grid.IsCurrentCellInEditMode) f._grid.EndEdit();
                return true;
            }
        }
    }
}
