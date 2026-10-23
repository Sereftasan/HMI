using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// COMPARE (Config Import, 23 October: "We need a compare tool here I think, so the user could see what is
    /// different in between the old and new one of the files"): this program's file beside what the import would
    /// write - the older installation's, converted when it needs it - line by line as a compare program shows them:
    /// a line changed (yellow), one only in this program's (red), one only in the older one's (green). The
    /// differences alone with two lines round each, or every line; the next and the previous difference.
    /// </summary>
    public sealed class SettingsCompareForm : Form
    {
        private const int Context = 2;
        private readonly List<TextDiff.Row> _all;
        private List<TextDiff.Row> _shown = new List<TextDiff.Row>();
        private readonly DataGridView _grid = new DataGridView { Name = "cmpGrid" };
        private readonly CheckBox _onlyDiff = new CheckBox { Name = "cmpOnlyDiff", AutoSize = true, Checked = true };
        private readonly Label _summary = new Label { Name = "cmpSummary", AutoSize = false };

        private static readonly Color ChangedBack = Color.FromArgb(255, 244, 196), LeftBack = Color.FromArgb(255, 221, 221),
                                      RightBack = Color.FromArgb(214, 245, 214), NoneBack = Color.FromArgb(242, 243, 245);

        private static string T(string key, string fallback) { return LanguageManager.GetString(key, fallback); }

        /// <summary>The rows shown (tests).</summary>
        internal List<TextDiff.Row> Shown { get { return _shown; } }
        internal TextDiff.Summary Counted { get; private set; }
        internal string SummaryText { get { return _summary.Text; } }

        /// <summary>Tests: the window given to it instead of shown.</summary>
        public static Action<SettingsCompareForm> Showing;

        public static void Run(IWin32Window owner, SettingsMigration.Finding f)
        {
            List<TextDiff.Row> rows;
            try { rows = SettingsMigration.Compare(f); }
            catch (Exception ex)
            {
                MessageBox.Show(owner, string.Format(T("Msg_CmpFailed", "The files could not be compared: {0}"), ex.Message),
                                T("Ttl_Compare", "Compare"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (var form = new SettingsCompareForm(f, rows))
            {
                if (Showing != null) { Showing(form); return; }
                form.ShowDialog(owner);
            }
        }

        public SettingsCompareForm(SettingsMigration.Finding f, List<TextDiff.Row> rows)
        {
            _all = rows ?? new List<TextDiff.Row>();
            Counted = TextDiff.Count(_all);
            UiScale.ApplyOnLoad(this);
            Text = string.Format(T("Ttl_CmpFile", "Compare - {0} ({1})"), f.Kind, f.FileName);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1200, 700);
            MinimumSize = new Size(640, 360);
            ShowInTaskbar = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = UiStyle.Body;
            KeyPreview = true;

            // ---- the two files ----
            var heads = new TableLayoutPanel { Dock = DockStyle.Top, Height = 62, ColumnCount = 2, Padding = new Padding(8, 6, 8, 2) };
            heads.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            heads.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            Func<string, string, Label> head = (title, path) => new Label
            {
                Dock = DockStyle.Fill, AutoEllipsis = true,
                Text = title + "\r\n" + path, ForeColor = Color.FromArgb(50, 56, 66)
            };
            string left = f.TargetExists ? f.TargetPath + "  (" + File.GetLastWriteTime(f.TargetPath).ToString("yyyy-MM-dd HH:mm") + ")"
                                         : T("Lbl_CmpNone", "(none - the import adds it)");
            string right = f.SourcePath + (f.NeedsUpgrade ? "  " + string.Format(T("Lbl_CmpConverted", "(converted from v{0}, as it would be written)"), f.DetectedVersion) : "");
            heads.Controls.Add(head(T("Lbl_CmpThis", "This program's file - replaced by the import:"), left), 0, 0);
            heads.Controls.Add(head(T("Lbl_CmpOld", "The older installation's - what the import writes:"), right), 1, 0);

            // ---- what differs, the buttons ----
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 4, 8, 2), WrapContents = true, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            _onlyDiff.Text = T("Chk_CmpOnlyDiff", "Only the differences");
            _onlyDiff.Margin = new Padding(3, 7, 12, 3);
            _onlyDiff.CheckedChanged += (s, e) => Fill();
            var prev = UiStyle.TextButton(T("Btn_CmpPrev", "Previous difference"), 0, (s, e) => Step(-1));
            prev.Name = "cmpPrev";
            var next = UiStyle.TextButton(T("Btn_CmpNext", "Next difference"), 0, (s, e) => Step(+1));
            next.Name = "cmpNext";
            _summary.Width = 520; _summary.Height = 28;
            _summary.Margin = new Padding(12, 8, 3, 3);
            bar.Controls.AddRange(new Control[] { _onlyDiff, prev, next, _summary });

            // ---- side by side ----
            _grid.Dock = DockStyle.Fill;
            _grid.VirtualMode = true;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.Font = new Font(FontFamily.GenericMonospace, 9F);
            _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LNo", HeaderText = "#", Width = 56, SortMode = DataGridViewColumnSortMode.NotSortable });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LText", HeaderText = T("Col_CmpThis", "This program's"), Width = 520, SortMode = DataGridViewColumnSortMode.NotSortable });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RNo", HeaderText = "#", Width = 56, SortMode = DataGridViewColumnSortMode.NotSortable });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RText", HeaderText = T("Col_CmpOld", "The older installation's"), Width = 520, SortMode = DataGridViewColumnSortMode.NotSortable });
            _grid.Columns["LNo"].DefaultCellStyle.ForeColor = _grid.Columns["RNo"].DefaultCellStyle.ForeColor = Color.FromArgb(130, 136, 146);
            _grid.Columns["LNo"].DefaultCellStyle.Alignment = _grid.Columns["RNo"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.CellValueNeeded += OnValueNeeded;
            _grid.CellFormatting += OnFormatting;
            _grid.Resize += (s, e) => FitColumns();

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var close = UiStyle.DialogButton(T("Btn_Close", "Close"), DialogResult.Cancel);
            bottom.Controls.Add(close);
            CancelButton = close;

            Controls.Add(_grid);
            Controls.Add(bar);
            Controls.Add(heads);
            Controls.Add(bottom);

            var c = Counted;
            _summary.Text = c.Identical
                ? (f.TargetExists ? T("Msg_CmpSame", "The two are the same - the import changes nothing in it.") : T("Msg_CmpNew", "This program has no such file - the import adds it."))
                : string.Format(T("Msg_CmpCount", "{0} line(s) changed, {1} only in this program's, {2} only in the older one's - {3} the same."),
                                c.Changed, c.OnlyLeft, c.OnlyRight, c.Same);
            if (c.Identical) _onlyDiff.Checked = false;
            Fill();
        }

        /// <summary>The rows shown: every one, or the differences with <see cref="Context"/> lines round them.</summary>
        internal void Fill()
        {
            if (!_onlyDiff.Checked) _shown = _all.ToList();
            else
            {
                var keep = new bool[_all.Count];
                for (int i = 0; i < _all.Count; i++)
                    if (_all[i].Kind != TextDiff.Kind.Same)
                        for (int k = Math.Max(0, i - Context); k <= Math.Min(_all.Count - 1, i + Context); k++) keep[k] = true;
                _shown = new List<TextDiff.Row>();
                for (int i = 0; i < _all.Count; i++)
                {
                    if (!keep[i]) continue;
                    // a gap left out: one empty row marks it
                    if (_shown.Count > 0 && i > 0 && !keep[i - 1]) _shown.Add(null);
                    _shown.Add(_all[i]);
                }
            }
            _grid.RowCount = 0;
            _grid.RowCount = _shown.Count;
            FitColumns();
        }

        private void FitColumns()
        {
            int numbers = _grid.Columns["LNo"].Width + _grid.Columns["RNo"].Width + SystemInformation.VerticalScrollBarWidth + 4;
            int w = Math.Max(160, (_grid.ClientSize.Width - numbers) / 2);
            _grid.Columns["LText"].Width = w;
            _grid.Columns["RText"].Width = w;
        }

        /// <summary>The next (+1) or previous (-1) difference after the row selected, selected and in sight.</summary>
        internal int Step(int direction)
        {
            Func<int, bool> differs = k => k >= 0 && k < _shown.Count && _shown[k] != null && _shown[k].Kind != TextDiff.Kind.Same;
            int from = _grid.CurrentCell != null ? _grid.CurrentCell.RowIndex : (direction > 0 ? -1 : _shown.Count);
            // the first row of the next (previous) run of differing lines
            for (int i = from + direction; i >= 0 && i < _shown.Count; i += direction)
            {
                if (!differs(i) || differs(i - 1)) continue;
                _grid.CurrentCell = _grid.Rows[i].Cells[1];
                try { _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, i - 3); } catch { }
                return i;
            }
            return -1;
        }

        private void OnValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _shown.Count) return;
            var r = _shown[e.RowIndex];
            if (r == null) { e.Value = e.ColumnIndex == 1 ? "⋯" : ""; return; }
            switch (e.ColumnIndex)
            {
                case 0: e.Value = r.Left >= 0 ? (r.Left + 1).ToString(CultureInfo.InvariantCulture) : ""; break;
                case 1: e.Value = r.LeftText; break;
                case 2: e.Value = r.Right >= 0 ? (r.Right + 1).ToString(CultureInfo.InvariantCulture) : ""; break;
                default: e.Value = r.RightText; break;
            }
        }

        private void OnFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _shown.Count) return;
            var r = _shown[e.RowIndex];
            bool left = e.ColumnIndex <= 1;
            Color back;
            if (r == null) back = NoneBack;
            else switch (r.Kind)
            {
                case TextDiff.Kind.Changed: back = ChangedBack; break;
                case TextDiff.Kind.OnlyLeft: back = left ? LeftBack : NoneBack; break;
                case TextDiff.Kind.OnlyRight: back = left ? NoneBack : RightBack; break;
                default: back = Color.White; break;
            }
            e.CellStyle.BackColor = back;
            e.CellStyle.SelectionBackColor = ControlPaint.Dark(back, 0.08f);
            e.CellStyle.SelectionForeColor = Color.Black;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F8 || keyData == (Keys.Alt | Keys.Down)) { Step(+1); return true; }
            if (keyData == (Keys.Shift | Keys.F8) || keyData == (Keys.Alt | Keys.Up)) { Step(-1); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
