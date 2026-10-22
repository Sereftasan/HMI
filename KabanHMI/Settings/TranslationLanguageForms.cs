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
    /// </summary>
    public class TranslationLanguageForm : Form
    {
        private readonly TextBox _code = new TextBox();
        private readonly Label _name = new Label();
        private readonly ComboBox _from = new ComboBox();
        private readonly Label _error = new Label();
        private readonly IList<string> _existing;

        /// <summary>The code given (upper case).</summary>
        public string Code { get; private set; }
        /// <summary>The language whose texts are copied into it (null: none).</summary>
        public string CopyFrom { get; private set; }

        private static string T(string key, string fallback) { return LanguageManager.GetString(key, fallback); }
        internal static string Title { get { return T("Tip_TrnAddLang", "Add a language"); } }
        internal static string CodePrompt { get { return T("Msg_TrnLangCode", "The language's code - 2 or 3 letters (FR for French, PL for Polish):"); } }
        internal static string FromPrompt { get { return T("Msg_TrnLangFrom", "Its texts to begin with - a language's copied into it (its code; empty: none):"); } }

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
            ClientSize = new Size(500, 262);

            var prompt = new Label { Text = CodePrompt, Left = 14, Top = 12, Width = 470, Height = 22 };
            _code.SetBounds(14, 38, 90, 26);
            _code.CharacterCasing = CharacterCasing.Upper;
            _code.MaxLength = 3;
            _name.SetBounds(114, 41, 370, 22);
            _name.ForeColor = Color.FromArgb(90, 96, 106);
            _code.TextChanged += (s, e) => { _error.Text = ""; ShowName(); };

            var begin = new Label { Text = T("Lbl_TrnLangBegin", "Its texts to begin with:"), Left = 14, Top = 82, Width = 470, Height = 22 };
            _from.SetBounds(14, 108, 470, 28);
            _from.DropDownStyle = ComboBoxStyle.DropDownList;
            _from.Items.Add(new Option(null, T("Lbl_TrnLangNone", "None - the screen shows English until a text is typed")));
            foreach (var l in _existing)
                _from.Items.Add(new Option(l, string.Format(T("Lbl_TrnLangCopyOf", "{0} - {1}: its {2} text(s) copied"), l, AlarmAiPrompt.LanguageName(l),
                                                            texts != null ? texts(l) : 0)));
            _from.SelectedIndex = 0;
            var note = new Label
            {
                Text = T("Lbl_TrnCopyNote", "The copies are typed texts (orange): check or translate them, then Save writes them into the translation files."),
                Left = 14, Top = 142, Width = 470, Height = 40, ForeColor = Color.FromArgb(90, 96, 106)
            };
            _error.SetBounds(14, 184, 470, 22);
            _error.ForeColor = Color.FromArgb(190, 30, 30);

            var ok = UiStyle.DialogButton(T("Btn_Ok", "OK"), DialogResult.None);
            var cancel = UiStyle.DialogButton(T("Btn_Cancel", "Cancel"), DialogResult.Cancel);
            ok.Location = new Point(ClientSize.Width - 14 - cancel.Width - 8 - ok.Width, 216);
            cancel.Location = new Point(ClientSize.Width - 14 - cancel.Width, 216);
            ok.Click += (s, e) => Accept();
            Controls.AddRange(new Control[] { prompt, _code, _name, begin, _from, note, _error, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private sealed class Option
        {
            public readonly string Code, Text;
            public Option(string code, string text) { Code = code; Text = text; }
            public override string ToString() { return Text; }
        }

        private void ShowName()
        {
            var c = (_code.Text ?? "").Trim().ToUpperInvariant();
            var n = c.Length >= 2 ? AlarmAiPrompt.LanguageName(c) : "";
            _name.Text = n != c ? n : "";
        }

        /// <summary>What is wrong with a code (null: it can be added).</summary>
        internal static string Problem(string code, IEnumerable<string> existing)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (!Regex.IsMatch(code, "^[A-Z]{2,3}$")) return T("Msg_TrnLangBad", "A language's code is 2 or 3 letters (FR, PL ...).");
            if (existing != null && existing.Contains(code, StringComparer.OrdinalIgnoreCase)) return string.Format(T("Msg_TrnLangThere", "{0} is there already."), code);
            return null;
        }

        // the tests: the dialog filled in without the mouse
        internal string CodeText { get { return _code.Text; } set { _code.Text = (value ?? "").ToUpperInvariant(); } }
        internal ComboBox FromBox { get { return _from; } }
        internal string ErrorText { get { return _error.Text; } }

        /// <summary>OK: closed when the code can be added, else the reason shown.</summary>
        internal bool Accept()
        {
            var why = Problem(_code.Text, _existing);
            if (why != null) { _error.Text = why; _code.Focus(); return false; }
            Code = _code.Text.Trim().ToUpperInvariant();
            var o = _from.SelectedItem as Option;
            CopyFrom = o != null ? o.Code : null;
            DialogResult = DialogResult.OK;
            return true;
        }

        /// <summary>The dialog (a test's Ask.Hook answers the code and the language to copy instead); false: cancelled.</summary>
        public static bool Run(IWin32Window owner, IList<string> languages, Func<string, int> texts, out string code, out string copyFrom)
        {
            code = copyFrom = null;
            if (Ask.Hook != null)
            {
                var c = Ask.Text(owner, Title, CodePrompt);
                if (c == null) return false;
                var why = Problem(c, languages);
                if (why != null) { Ask.Tell(owner, Title, why); return false; }
                code = c.Trim().ToUpperInvariant();
                var f = (Ask.Text(owner, Title, FromPrompt) ?? "").Trim().ToUpperInvariant();
                copyFrom = languages.Contains(f, StringComparer.OrdinalIgnoreCase) ? f : null;
                return true;
            }
            using (var dlg = new TranslationLanguageForm(languages, texts))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return false;
                code = dlg.Code;
                copyFrom = dlg.CopyFrom;
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
