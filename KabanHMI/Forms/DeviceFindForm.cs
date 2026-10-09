using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// The PLC Monitor's FIND: where in the program is a device used?
    ///
    /// Type an address (M604), a label from the PLC label list, or part of a
    /// unit's name ("clamp"). Every use is listed - a device on two buttons of one
    /// page is there twice - with the page and what uses it. Click a line and the
    /// page opens with that unit framed in blinking blue (see ControlHighlight).
    ///
    /// Searching is instant after the first time: the pages are built once per
    /// controller to find the uses (DeviceCollector.UsesOf), which takes a moment
    /// on a big machine, and kept.
    ///
    /// It opens as it was closed: the same place and size, the columns as wide
    /// and in the order they were dragged to (WindowLayout, "Find.*" in the
    /// monitor's Windows.cfg).
    ///
    /// The list is a table in THE MONITOR'S LOOK (SimulationMonitorForm.StyleGrid):
    /// the same heading, fonts and rows as the lists above it. It was a Windows
    /// list view, whose own heading and font sat oddly under the monitor's.
    ///
    /// With the device's VALUE, its DISPLAY FORMAT (a right-click on a line
    /// changes it) and when it was last MODIFIED, as the monitor's lists show
    /// them - the monitor reads the devices found (see Live). A right-click on a
    /// heading takes columns off and on, types their widths (ColumnChooser).
    /// </summary>
    public class DeviceFindForm : Form
    {
        private readonly int _station;
        private readonly Func<DeviceCollector.Use, string> _open;
        private readonly TextBox _query = new TextBox();
        private readonly DataGridView _list = new DataGridView();
        private Func<int> _searchWidth = () => 0;

        /// <summary>
        /// The width at which nothing of Find is cut off: every column of its
        /// list with room for the scroll bar, and its search line (the side
        /// panel's border double-clicked while Find is open in the monitor).
        /// </summary>
        public int PreferredWidth()
        {
            int columns = _list.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width) +
                          (_list.RowHeadersVisible ? _list.RowHeadersWidth : 0) + SystemInformation.VerticalScrollBarWidth + 4;
            int chrome = Math.Max(0, ClientSize.Width - _list.ClientSize.Width);
            return Math.Max(columns + chrome, _searchWidth());
        }
        private readonly Label _status = new Label();
        private readonly ToolTip _tips = UiStyle.CreateTips();
        private List<DeviceCollector.Use> _found = new List<DeviceCollector.Use>();

        /// <summary>What the Value and Modified cells show for a device found.</summary>
        public sealed class LiveCells
        {
            public string Value = "", ValueTip = "", Modified = "", ModifiedTip = "";
            public Color ModifiedColor = Color.Empty;
        }

        /// <summary>
        /// The device's value in a display format, and its last change - given by
        /// the monitor, which reads the devices found (null: the cells stay empty).
        /// </summary>
        public Func<DeviceCollector.Use, string, LiveCells> Live;

        /// <summary>A line's display format, by address (Decimal until changed; the monitor's row's own when it lists the device).</summary>
        private readonly Dictionary<string, string> _formats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>The format of a device the user has not chosen one for (the monitor's row's, when it lists the device).</summary>
        public Func<DeviceCollector.Use, string> DefaultFormat;

        /// <summary>The uses listed (the monitor reads their devices).</summary>
        public IReadOnlyList<DeviceCollector.Use> Found { get { return _found; } }

        private readonly bool _embedded;
        private readonly Action _close;

        /// <summary>The controller searched.</summary>
        public int Station { get { return _station; } }

        /// <param name="open">Opens a use's page; returns "" or why it could not.</param>
        /// <param name="embedded">Shown inside the PLC Monitor's side page, not as a window of its own.</param>
        /// <param name="close">What its close button (and Esc) does when embedded.</param>
        public DeviceFindForm(int station, string caption, string query, Func<DeviceCollector.Use, string> open,
                              bool embedded = false, Action close = null)
        {
            _station = station;
            _open = open;
            _embedded = embedded;
            _close = close;
            Text = T("Ttl_Find", "Find where a device is used") + " - " + caption;
            Size = new Size(820, 440);
            // As narrow as the user likes: the search box gives way, the list scrolls.
            MinimumSize = new Size(360, 220);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = UiStyle.Body;
            KeyPreview = true;

            if (embedded)
            {
                TopLevel = false;
                FormBorderStyle = FormBorderStyle.None;
                MinimumSize = Size.Empty;
            }
            else UiScale.ApplyOnLoad(this);

            var top = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 8, 10, 4) };
            var caption1 = new Label { Text = T("Lbl_FindWhat", "Address, label or name:"), AutoSize = true, Location = new Point(10, 12) };
            top.Controls.Add(caption1);
            _query.Text = query ?? "";
            _query.Location = new Point(caption1.Right + 170, 9);
            _query.Width = 260;
            _query.Font = UiStyle.Body;
            top.Controls.Add(_query);
            var go = UiStyle.ToolButton(char.ConvertFromUtf32(0x1F50E), T("Tip_FindGo", "Find"), _tips, (s, e) => Search());
            // Enter in the box finds (the window's own): shown first in the tooltip
            Kaban.UI.Helpers.Shortcuts.Set(go, Keys.Enter, _tips, T("Tip_FindGo", "Find"), dispatch: false);
            top.Controls.Add(go);
            Button closeButton = null;
            if (embedded)
            {
                // Inside the monitor: its own close, as there is no window frame.
                closeButton = new Button { Size = new Size(30, 30), FlatStyle = FlatStyle.Flat, Text = "", TabStop = false, Cursor = Cursors.Hand, AccessibleName = "Close" };
                closeButton.FlatAppearance.BorderSize = 0;
                closeButton.Paint += (s, e) => MainForm.DrawCloseGlyph(e.Graphics, closeButton.ClientRectangle, Color.FromArgb(50, 56, 66));
                closeButton.Click += (s, e) => CloseEmbedded();
                Kaban.UI.Controls.UiStyle.SetTip(_tips, closeButton, T("Tip_FindClose", "Close Find (Esc)"));
                top.Controls.Add(closeButton);
            }
            // The search line's own width: the caption, the box at its usual
            // width, the button (and the close inside the monitor).
            _searchWidth = () => caption1.Right + 8 + UiScale.For(_query, 260) + 6 + go.Width + 16 +
                                 (closeButton != null ? closeButton.Width + 14 : 0) + top.Padding.Horizontal;
            top.Layout += (s, e) =>
            {
                int right = top.ClientSize.Width - (closeButton != null ? closeButton.Width + 8 : 0);
                if (closeButton != null) closeButton.Location = new Point(top.ClientSize.Width - closeButton.Width - 6, (top.ClientSize.Height - closeButton.Height) / 2);
                _query.Left = caption1.Right + 8;
                _query.Width = Math.Max(80, Math.Min(UiScale.For(_query, 260), right - _query.Left - go.Width - 16));
                go.Location = new Point(_query.Right + 6, (top.ClientSize.Height - go.Height) / 2);
            };

            _list.Dock = DockStyle.Fill;
            SimulationMonitorForm.StyleGrid(_list);
            _list.ReadOnly = true;
            _list.MultiSelect = false;
            _list.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _list.AllowUserToResizeColumns = true;
            _list.AllowUserToOrderColumns = true;          // the headings drag, as on the monitor
            _list.StandardTab = true;
            Action<string, string, int> column = (name, header, width) =>
                _list.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = name, HeaderText = header, Width = width,
                    SortMode = DataGridViewColumnSortMode.Automatic      // a click on a heading sorts
                });
            column("Page", T("Col_FindPage", "Page"), 300);
            column("Where", T("Col_FindWhere", "Used by"), 250);
            column("Address", T("Col_Address", "Address"), 80);
            column("DataType", T("Col_DataType", "DataType"), 150);
            column("Value", T("Col_Value", "Value"), 120);
            column("Format", T("Col_DisplayFormat", "Display Format"), 110);
            column("Modified", T("Col_LastChange", "Modified"), 140);
            _list.Columns["Value"].DefaultCellStyle.Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);
            _list.Columns["Value"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _list.ShowCellToolTips = true;

            // A right-click on a heading: the columns (ColumnChooser); on a line: its display format.
            var chooser = ColumnChooser.Attach(_list);
            chooser.DefaultKey = "PlcMonitorFind";
            chooser.Changed = SaveColumns;
            chooser.Reset = () =>
            {
                var widths = new[] { 300, 250, 80, 150, 120, 110, 140 };
                for (int i = 0; i < _list.Columns.Count && i < widths.Length; i++)
                {
                    _list.Columns[i].Width = UiScale.For(_list, widths[i]);
                    _list.Columns[i].DisplayIndex = i;
                    _list.Columns[i].Visible = true;
                }
            };
            var rowMenu = UiStyle.Menu();
            rowMenu.Opening += (s, e) =>
            {
                if (chooser.OnHeadings(_list.PointToClient(Cursor.Position)))
                {
                    e.Cancel = true;
                    UiStyle.ShowPopupAt(chooser.Menu, _list, Cursor.Position);
                    return;
                }
                rowMenu.Items.Clear();
                var use = _list.CurrentRow != null ? _list.CurrentRow.Tag as DeviceCollector.Use : null;
                if (use == null || SimulationMonitorForm.IsBit(use.DataType)) { e.Cancel = true; return; }
                var fmt = new ToolStripMenuItem(T("Col_DisplayFormat", "Display Format"));
                foreach (var f in SimulationMonitorForm.DisplayFormats)
                {
                    var name = f;
                    fmt.DropDownItems.Add(new ToolStripMenuItem(T("Fmt_" + name, name), null, (s2, e2) => { _formats[use.Address] = name; RefreshLive(); })
                                          { Checked = FormatOf(use) == name });
                }
                rowMenu.Items.Add(fmt);
                e.Cancel = false;           // it opened empty: WinForms had set Cancel (the first right-click)
            };
            _list.ContextMenuStrip = rowMenu;
            // A right-click chooses the line first (not a page opened: that is a left click).
            _list.CellMouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
                _list.CurrentCell = _list.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
            };

            // As it was closed last time (the place and size only as a window).
            var kept = WindowLayout.Read(SimulationMonitorForm.WindowsPath);
            if (!embedded) WindowLayout.Apply(this, LayoutName, kept);
            string columns;
            if (kept.TryGetValue(LayoutName + ".Columns", out columns)) WindowLayout.ApplyColumns(_list, columns);

            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.Padding = new Padding(8, 4, 8, 2);
            _status.ForeColor = Color.FromArgb(90, 96, 106);
            _status.Font = UiStyle.Small;

            Controls.Add(_list);
            Controls.Add(top);
            Controls.Add(_status);

            // A CLICK opens the page: the list is there to be clicked.
            _list.CellMouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && e.RowIndex >= 0) OpenItem(_list.Rows[e.RowIndex]);
            };
            _list.KeyDown += (s, e) =>
            {
                var row = _list.CurrentRow;
                if (e.KeyCode == Keys.Enter && row != null) { OpenItem(row); e.Handled = e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Up && row != null && row.Index == 0) { _query.Focus(); e.Handled = true; }
            };
            _query.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { Search(); e.Handled = e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down && _list.Rows.Count > 0)
                {
                    _list.Focus();
                    _list.CurrentCell = _list.Rows[0].Cells[_list.Columns.Cast<DataGridViewColumn>().OrderBy(c => c.DisplayIndex).First().Index];
                    _list.Rows[0].Selected = true;
                    e.Handled = true;
                }
            };
            KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Escape) return;
                if (_embedded) CloseEmbedded(); else Close();
            };

            if (!embedded) Shown += (s, e) => Begin(null);
        }

        /// <summary>Focus in the box - and the search, when there is something to find (a new query replaces the text).</summary>
        public void Begin(string query)
        {
            if (query != null) _query.Text = query;
            _query.Focus();
            _query.SelectAll();
            if (_query.Text.Trim().Length > 0) Search();
            else _status.Text = T("Msg_FindHint", "Type an address (M604), a label, or part of a unit's name, then Enter.");
        }

        private void CloseEmbedded()
        {
            SaveColumns();
            if (_close != null) _close();
        }

        private void SaveColumns()
        {
            WindowLayout.Write(SimulationMonitorForm.WindowsPath, new Dictionary<string, string> { { LayoutName + ".Columns", WindowLayout.ColumnsOf(_list) } });
        }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private const string LayoutName = "Find";

        /// <summary>Keeps the place, the size and the columns for next time.</summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            var values = new Dictionary<string, string>();
            if (!_embedded) WindowLayout.Store(this, LayoutName, values);
            values[LayoutName + ".Columns"] = WindowLayout.ColumnsOf(_list);
            WindowLayout.Write(SimulationMonitorForm.WindowsPath, values);
        }

        /// <summary>The uses matching the text typed (public for tests).</summary>
        public List<DeviceCollector.Use> Search()
        {
            var q = _query.Text.Trim();
            _list.Rows.Clear();
            _found = new List<DeviceCollector.Use>();
            if (q.Length == 0) { _status.Text = T("Msg_FindHint", "Type an address (M604), a label, or part of a unit's name, then Enter."); return _found; }

            List<DeviceCollector.Use> uses;
            var cursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try { uses = DeviceCollector.UsesOf(_station); }
            finally { Cursor.Current = cursor; }

            _found = Match(uses, _station, q);
            foreach (var u in _found)
            {
                int i = _list.Rows.Add(u.PageTitle, u.UsedAs, u.Address, u.DataType, "", "", "");
                _list.Rows[i].Tag = u;
                if (u.IsAlarm) _list.Rows[i].DefaultCellStyle.ForeColor = Color.FromArgb(120, 126, 136);
            }
            RefreshLive();
            // Nothing chosen until the user chooses: a selected first line read
            // as "this one was opened".
            _list.ClearSelection();
            try { _list.CurrentCell = null; } catch (InvalidOperationException) { }
            int pages = _found.Select(u => u.PageTitle).Distinct().Count();
            _status.Text = _found.Count == 0
                ? string.Format(T("Msg_FindNone", "'{0}' is used nowhere on this controller's pages."), q)
                : string.Format(T("Msg_FindCount", "{0} use(s) on {1} page(s). Click a line to open its page."), _found.Count, pages);
            return _found;
        }

        /// <summary>
        /// What the text finds. An ADDRESS finds that device - and a double word
        /// whose second word it is; a LABEL of the PLC label list finds its
        /// device; any other text finds the uses whose name or label contains it.
        /// </summary>
        public static List<DeviceCollector.Use> Match(IEnumerable<DeviceCollector.Use> uses, int station, string text)
        {
            var q = (text ?? "").Trim();
            if (q.Length == 0) return new List<DeviceCollector.Use>();
            string address = q.ToUpperInvariant();
            var byLabel = PlcLabelTable.ByLabel(station, q);
            if (byLabel != null && !string.IsNullOrWhiteSpace(byLabel.Address)) address = byLabel.Address.Trim().ToUpperInvariant();

            bool looksLikeAddress = Regex.IsMatch(address, "^[A-Z]{1,3}[0-9A-F]+$") && address.Any(char.IsDigit);
            List<DeviceCollector.Use> found;
            if (looksLikeAddress || byLabel != null)
                found = uses.Where(u => u.Address == address ||
                                        (SimulationMonitorForm.IsDoubleWord(u.DataType) && PlcAddressHelper.GetNextAddress(u.Address) == address))
                            .ToList();
            else
                found = uses.Where(u =>
                {
                    var label = PlcLabelTable.ByAddress(station, u.Address);
                    return u.UsedAs.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                           (label != null && label.Label.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0);
                }).ToList();
            // Page by page, the alarm definitions last.
            return found.OrderBy(u => u.IsAlarm).ThenBy(u => u.PageTitle, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(u => u.UsedAs, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>A line's display format: the one chosen here, else the monitor's for that device, else Decimal.</summary>
        public string FormatOf(DeviceCollector.Use use)
        {
            string f;
            if (_formats.TryGetValue(use.Address, out f)) return f;
            f = DefaultFormat != null ? DefaultFormat(use) : null;
            return string.IsNullOrEmpty(f) ? SimulationMonitorForm.FormatDecimal : f;
        }

        /// <summary>The Value, Display Format and Modified cells brought up to date (the monitor calls it as it reads).</summary>
        public void RefreshLive()
        {
            foreach (DataGridViewRow row in _list.Rows)
            {
                var use = row.Tag as DeviceCollector.Use;
                if (use == null) continue;
                bool bit = SimulationMonitorForm.IsBit(use.DataType);
                string format = FormatOf(use);
                SetCell(row.Cells["Format"], bit ? "" : T("Fmt_" + format, format), "");
                var live = Live != null ? Live(use, format) : null;
                if (live == null) { SetCell(row.Cells["Value"], "", ""); SetCell(row.Cells["Modified"], "", ""); continue; }
                SetCell(row.Cells["Value"], live.Value, live.ValueTip);
                SetCell(row.Cells["Modified"], live.Modified, live.ModifiedTip);
                if (!live.ModifiedColor.IsEmpty && row.Cells["Modified"].Style.ForeColor != live.ModifiedColor)
                    row.Cells["Modified"].Style.ForeColor = live.ModifiedColor;
            }
        }

        private static void SetCell(DataGridViewCell cell, string text, string tip)
        {
            if (!Equals(cell.Value as string ?? "", text ?? "")) cell.Value = text ?? "";
            if ((cell.ToolTipText ?? "") != (tip ?? "")) cell.ToolTipText = tip ?? "";
        }

        /// <summary>The lines listed, as shown (tests).</summary>
        public int RowCount { get { return _list.Rows.Count; } }

        private void OpenItem(DataGridViewRow item)
        {
            var use = item.Tag as DeviceCollector.Use;
            if (use == null) return;
            if (use.IsAlarm)
            {
                _status.Text = T("Msg_FindAlarm", "An alarm definition's bit - see Machine Parameters > Alarms.");
                return;
            }
            var why = _open != null ? _open(use) : "";
            _status.Text = string.IsNullOrEmpty(why)
                ? string.Format(T("Msg_FindOpened", "Opened: {0} - {1}"), use.PageTitle, use.UsedAs)
                : why;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }
    }
}
