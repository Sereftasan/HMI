using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// A table's COLUMNS, as the user wants them: a right-click on a heading lists
    /// them to tick on or off (the menu stays open while ticking), then
    ///   Column widths...           every column's width typed, and the TABLE's
    ///                              width - the columns scaled to it
    ///   Fit the widths to the contents
    ///   Default columns
    /// The PLC Monitor's lists and its Find use it; each keeps what is chosen its
    /// own way (Columns.cfg, Windows.cfg), so a table opens as it was left.
    /// </summary>
    public sealed class ColumnChooser
    {
        private readonly DataGridView _grid;
        /// <summary>Whether the user wants the column (the table may still hide one it has nothing for).</summary>
        public Func<DataGridViewColumn, bool> IsShown;
        public Action<DataGridViewColumn, bool> SetShown;
        /// <summary>Called after anything was changed here (to keep it).</summary>
        public Action Changed;
        /// <summary>Puts the table's own widths, order and columns back; null: no "Default columns".</summary>
        public Action Reset;

        private ColumnChooser(DataGridView grid)
        {
            _grid = grid;
            IsShown = c => c.Visible;
            SetShown = (c, show) => c.Visible = show;
        }

        /// <summary>The menu on every heading of the table (a column added later gets it on the next opening).</summary>
        public static ColumnChooser Attach(DataGridView grid)
        {
            var chooser = new ColumnChooser(grid);
            var menu = UiStyle.Menu();
            bool keepOpen = false;
            menu.Opening += (s, e) =>
            {
                menu.Items.Clear();
                foreach (var item in chooser.MenuItems()) menu.Items.Add(item);
                foreach (DataGridViewColumn c in grid.Columns) c.HeaderCell.ContextMenuStrip = menu;
            };
            // Ticking columns is several choices at once: the menu stays open for it.
            menu.ItemClicked += (s, e) => { var mi = e.ClickedItem as ToolStripMenuItem; keepOpen = mi != null && mi.CheckOnClick; };
            menu.Closing += (s, e) =>
            {
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked && keepOpen) e.Cancel = true;
                keepOpen = false;
            };
            foreach (DataGridViewColumn c in grid.Columns) c.HeaderCell.ContextMenuStrip = menu;
            grid.ColumnAdded += (s, e) => e.Column.HeaderCell.ContextMenuStrip = menu;
            chooser.Menu = menu;
            menu.Tag = chooser;
            return chooser;
        }

        /// <summary>The headings' menu.</summary>
        public ContextMenuStrip Menu { get; private set; }

        /// <summary>True when the point (the grid's own) is on the heading row - a menu there is this one.</summary>
        public bool OnHeadings(Point clientPoint)
        {
            return _grid.ColumnHeadersVisible && clientPoint.Y >= 0 && clientPoint.Y < _grid.ColumnHeadersHeight;
        }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>The menu's items, as it opens (public for tests).</summary>
        public List<ToolStripItem> MenuItems()
        {
            var items = new List<ToolStripItem>();
            foreach (var c in _grid.Columns.Cast<DataGridViewColumn>().OrderBy(c => c.DisplayIndex))
            {
                var col = c;
                var item = new ToolStripMenuItem(col.HeaderText) { Checked = IsShown(col), CheckOnClick = true, Tag = col.Name };
                item.CheckedChanged += (s, e) =>
                {
                    // Never the last one: a table with no heading has nothing to right-click.
                    if (!item.Checked && !_grid.Columns.Cast<DataGridViewColumn>().Any(o => o != col && o.Visible)) { item.Checked = true; return; }
                    if (item.Checked == IsShown(col)) return;
                    SetShown(col, item.Checked);
                    if (Changed != null) Changed();
                };
                items.Add(item);
            }
            items.Add(new ToolStripSeparator());
            items.Add(new ToolStripMenuItem(T("Mnu_ColWidths", "Column widths..."), null, (s, e) => ShowWidths(_grid.FindForm())) { Tag = "widths" });
            items.Add(new ToolStripMenuItem(T("Mnu_ColFit", "Fit the widths to the contents"), null, (s, e) => FitToContents()) { Tag = "fit" });
            if (Reset != null)
                items.Add(new ToolStripMenuItem(T("Mnu_ColDefault", "Default columns"), null, (s, e) => { Reset(); if (Changed != null) Changed(); }) { Tag = "default" });
            return items;
        }

        /// <summary>Each column shown as wide as what is in it (and its heading).</summary>
        public void FitToContents()
        {
            var mode = _grid.Rows.Count > 2000 ? DataGridViewAutoSizeColumnMode.DisplayedCells : DataGridViewAutoSizeColumnMode.AllCells;
            foreach (DataGridViewColumn c in _grid.Columns)
                if (c.Visible) _grid.AutoResizeColumn(c.Index, mode);
            if (Changed != null) Changed();
        }

        /// <summary>The widths dialog: OK applies them.</summary>
        public bool ShowWidths(IWin32Window owner)
        {
            using (var dialog = new WidthsDialog(this))
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK) return false;
                dialog.Apply();
                return true;
            }
        }

        /// <summary>Makes the dialog without showing it (tests).</summary>
        internal Form NewWidthsDialog() { return new WidthsDialog(this); }

        /// <summary>
        /// Every column's width, and whether it shows - with the width of the
        /// whole table, which can be typed too: the shown columns then share it
        /// as they share it now.
        /// </summary>
        internal sealed class WidthsDialog : Form
        {
            private readonly ColumnChooser _owner;
            private readonly List<Tuple<DataGridViewColumn, CheckBox, NumericUpDown>> _rows = new List<Tuple<DataGridViewColumn, CheckBox, NumericUpDown>>();
            private readonly NumericUpDown _total = new NumericUpDown();
            private bool _busy;
            private readonly ToolTip _tips = UiStyle.CreateTips();
            private const int MinWidth = 30;

            protected override void Dispose(bool disposing)
            {
                if (disposing) _tips.Dispose();
                base.Dispose(disposing);
            }

            public WidthsDialog(ColumnChooser owner)
            {
                _owner = owner;
                Text = T("Ttl_ColWidths", "Column widths");
                Font = UiStyle.Body;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MinimizeBox = MaximizeBox = false;
                ShowInTaskbar = false;
                BackColor = Color.White;
                AutoScaleMode = AutoScaleMode.None;

                var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, Padding = new Padding(12, 10, 12, 4) };
                table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                table.Controls.Add(new Label { Text = T("Lbl_ColShown", "Shown"), AutoSize = true, ForeColor = Color.FromArgb(90, 96, 106), Margin = new Padding(3, 6, 12, 6) }, 0, 0);
                table.Controls.Add(new Label { Text = T("Lbl_ColColumn", "Column"), AutoSize = true, ForeColor = Color.FromArgb(90, 96, 106), Margin = new Padding(3, 6, 12, 6) }, 1, 0);
                table.Controls.Add(new Label { Text = T("Lbl_ColWidth", "Width (pixels)"), AutoSize = true, ForeColor = Color.FromArgb(90, 96, 106), Margin = new Padding(3, 6, 3, 6) }, 2, 0);
                int r = 1;
                foreach (var c in owner._grid.Columns.Cast<DataGridViewColumn>().OrderBy(c => c.DisplayIndex))
                {
                    var shown = new CheckBox { Checked = owner.IsShown(c), AutoSize = true, Margin = new Padding(12, 5, 12, 3) };
                    var width = new NumericUpDown { Minimum = MinWidth, Maximum = 3000, Value = Math.Max(MinWidth, Math.Min(3000, c.Width)), Width = 90, Margin = new Padding(3, 3, 3, 3) };
                    table.Controls.Add(shown, 0, r);
                    table.Controls.Add(new Label { Text = c.HeaderText, AutoSize = true, Margin = new Padding(3, 7, 12, 3) }, 1, r);
                    table.Controls.Add(width, 2, r);
                    _rows.Add(Tuple.Create(c, shown, width));
                    shown.CheckedChanged += (s, e) => { width.Enabled = shown.Checked; ShowTotal(); };
                    width.Enabled = shown.Checked;
                    width.ValueChanged += (s, e) => ShowTotal();
                    r++;
                }
                var totalLabel = new Label { Text = T("Lbl_ColTable", "The whole table"), AutoSize = true, Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), Margin = new Padding(3, 12, 12, 3) };
                _total.Minimum = MinWidth; _total.Maximum = 20000; _total.Width = 90; _total.Margin = new Padding(3, 9, 3, 3);
                table.Controls.Add(totalLabel, 1, r);
                table.Controls.Add(_total, 2, r);
                _total.ValueChanged += (s, e) => ScaleToTotal();
                _tips.SetToolTip(_total, T("Tip_ColTable", "Type the table's width: the columns shown share it as they share it now."));

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(8) };
                var cancel = UiStyle.Round(new Button { Text = T("Btn_Cancel", "Cancel"), DialogResult = DialogResult.Cancel, Width = 96, Height = 30 });
                var ok = UiStyle.Round(new Button { Text = T("Btn_Ok", "OK"), DialogResult = DialogResult.OK, Width = 96, Height = 30 });
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                AcceptButton = ok;
                CancelButton = cancel;

                Controls.Add(table);
                Controls.Add(buttons);
                ShowTotal();
                var size = table.GetPreferredSize(Size.Empty);
                ClientSize = new Size(Math.Max(size.Width, 320), size.Height + buttons.Height);
                UiScale.ApplyOnLoad(this);
            }

            /// <summary>The table's width: the columns ticked, and the row headings.</summary>
            private int Extra { get { return _owner._grid.RowHeadersVisible ? _owner._grid.RowHeadersWidth : 0; } }

            internal decimal Total { get { return _total.Value; } set { _total.Value = Math.Max(_total.Minimum, Math.Min(_total.Maximum, value)); } }
            internal NumericUpDown WidthOf(string column) { return _rows.First(t => t.Item1.Name == column).Item3; }
            internal CheckBox ShownOf(string column) { return _rows.First(t => t.Item1.Name == column).Item2; }

            /// <summary>
            /// A column ticked that the table will show: one wanted but kept off by
            /// the table itself (the monitor's Condition on a tab without
            /// conditions) is not part of its width.
            /// </summary>
            private bool Counts(Tuple<DataGridViewColumn, CheckBox, NumericUpDown> t)
            {
                return t.Item2.Checked && (t.Item1.Visible || !_owner.IsShown(t.Item1));
            }

            private void ShowTotal()
            {
                if (_busy) return;
                _busy = true;
                try
                {
                    decimal sum = _rows.Where(Counts).Sum(t => t.Item3.Value) + Extra;
                    _total.Value = Math.Max(_total.Minimum, Math.Min(_total.Maximum, sum));
                }
                finally { _busy = false; }
            }

            private void ScaleToTotal()
            {
                if (_busy) return;
                var shown = _rows.Where(Counts).ToList();
                if (shown.Count == 0) return;
                _busy = true;
                try
                {
                    decimal now = shown.Sum(t => t.Item3.Value);
                    decimal want = Math.Max(shown.Count * MinWidth, _total.Value - Extra);
                    decimal given = 0;
                    for (int i = 0; i < shown.Count; i++)
                    {
                        decimal w = i == shown.Count - 1 ? want - given
                                  : Math.Round(now > 0 ? shown[i].Item3.Value * want / now : want / shown.Count);
                        w = Math.Max(MinWidth, Math.Min(shown[i].Item3.Maximum, w));
                        shown[i].Item3.Value = w;
                        given += w;
                    }
                }
                finally { _busy = false; }
            }

            internal void Apply()
            {
                foreach (var t in _rows)
                {
                    if (t.Item2.Checked != _owner.IsShown(t.Item1)) _owner.SetShown(t.Item1, t.Item2.Checked);
                    t.Item1.Width = (int)t.Item3.Value;
                }
                // never a table with nothing on it
                if (_rows.Count > 0 && !_rows.Any(t => t.Item1.Visible))
                {
                    var first = _rows.FirstOrDefault(t => t.Item1.Name != "Condition") ?? _rows[0];
                    _owner.SetShown(first.Item1, true);
                    first.Item1.Visible = true;
                }
                if (_owner.Changed != null) _owner.Changed();
            }
        }
    }
}
