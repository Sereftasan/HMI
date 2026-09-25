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
    /// </summary>
    public class DeviceFindForm : Form
    {
        private readonly int _station;
        private readonly Func<DeviceCollector.Use, string> _open;
        private readonly TextBox _query = new TextBox();
        private readonly ListView _list = new ListView();
        private readonly Label _status = new Label();
        private readonly ToolTip _tips = UiStyle.CreateTips();
        private List<DeviceCollector.Use> _found = new List<DeviceCollector.Use>();

        /// <param name="open">Opens a use's page; returns "" or why it could not.</param>
        public DeviceFindForm(int station, string caption, string query, Func<DeviceCollector.Use, string> open)
        {
            _station = station;
            _open = open;
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

            var top = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 8, 10, 4) };
            var caption1 = new Label { Text = T("Lbl_FindWhat", "Address, label or name:"), AutoSize = true, Location = new Point(10, 12) };
            top.Controls.Add(caption1);
            _query.Text = query ?? "";
            _query.Location = new Point(caption1.Right + 170, 9);
            _query.Width = 260;
            _query.Font = UiStyle.Body;
            top.Controls.Add(_query);
            var go = UiStyle.ToolButton(char.ConvertFromUtf32(0x1F50E), T("Tip_FindGo", "Find (Enter)"), _tips, (s, e) => Search());
            top.Controls.Add(go);
            top.Layout += (s, e) =>
            {
                _query.Left = caption1.Right + 8;
                _query.Width = Math.Max(80, Math.Min(260, top.ClientSize.Width - _query.Left - go.Width - 16));
                go.Location = new Point(_query.Right + 6, 5);
            };

            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.HideSelection = false;
            _list.MultiSelect = false;
            _list.BorderStyle = BorderStyle.None;
            _list.AllowColumnReorder = true;               // the headings drag, as on the monitor
            _list.Columns.Add(T("Col_FindPage", "Page"), 300);
            _list.Columns.Add(T("Col_FindWhere", "Used by"), 250);
            _list.Columns.Add(T("Col_Address", "Address"), 80);
            _list.Columns.Add(T("Col_DataType", "DataType"), 150);

            // As it was closed last time.
            var kept = WindowLayout.Read(SimulationMonitorForm.WindowsPath);
            WindowLayout.Apply(this, LayoutName, kept);
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
            _list.MouseClick += (s, e) =>
            {
                var item = _list.GetItemAt(e.X, e.Y);
                if (e.Button == MouseButtons.Left && item != null) OpenItem(item);
            };
            _list.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && _list.SelectedItems.Count > 0) { OpenItem(_list.SelectedItems[0]); e.Handled = true; }
                else if (e.KeyCode == Keys.Up && _list.SelectedIndices.Count > 0 && _list.SelectedIndices[0] == 0) { _query.Focus(); e.Handled = true; }
            };
            _query.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { Search(); e.Handled = e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down && _list.Items.Count > 0)
                {
                    _list.Focus();
                    _list.Items[0].Selected = _list.Items[0].Focused = true;
                    e.Handled = true;
                }
            };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            Shown += (s, e) =>
            {
                _query.Focus();
                _query.SelectAll();
                if (_query.Text.Trim().Length > 0) Search();
                else _status.Text = T("Msg_FindHint", "Type an address (M604), a label, or part of a unit's name, then Enter.");
            };
        }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private const string LayoutName = "Find";

        /// <summary>Keeps the place, the size and the columns for next time.</summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            var values = new Dictionary<string, string>();
            WindowLayout.Store(this, LayoutName, values);
            values[LayoutName + ".Columns"] = WindowLayout.ColumnsOf(_list);
            WindowLayout.Write(SimulationMonitorForm.WindowsPath, values);
        }

        /// <summary>The uses matching the text typed (public for tests).</summary>
        public List<DeviceCollector.Use> Search()
        {
            var q = _query.Text.Trim();
            _list.Items.Clear();
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
                var item = new ListViewItem(new[] { u.PageTitle, u.UsedAs, u.Address, u.DataType }) { Tag = u };
                if (u.IsAlarm) item.ForeColor = Color.FromArgb(120, 126, 136);
                _list.Items.Add(item);
            }
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

        private void OpenItem(ListViewItem item)
        {
            var use = item.Tag as DeviceCollector.Use;
            if (use == null) return;
            if (use.IsAlarm)
            {
                _status.Text = T("Msg_FindAlarm", "An alarm definition's bit - see HMI Settings > Alarm Settings.");
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
