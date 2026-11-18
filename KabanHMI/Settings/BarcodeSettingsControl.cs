using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Barcodes;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// HMI SETTINGS > BARCODES (17 November: "take barcode reading and writing from KabanMachineSystem as well") - the old
    /// machines' Settings > Barcode Reader Settings (a reader's port, baud rate, parity, stop bits, Active), its Test page (a code
    /// read shown) and MD3030's barcode fields (the parts of a code: from where, how long):
    ///   the readers      a row each: serial (a COM port) or keyboard (a USB scanner that types), its machine, on / off, its state;
    ///   a code's parts   the profile code, the width, the height ... - from where (1 the first character), how long;
    ///   trying           a code typed or scanned into the box and Read: its parts as the machine would take them;
    ///   read last        the last codes read, what took each.
    /// </summary>
    public sealed class BarcodeSettingsControl : UserControl, IRebuildForLanguage
    {
        object IRebuildForLanguage.Place { get { return null; } }
        void IRebuildForLanguage.ReturnTo(object place) { }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        private readonly Panel _body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Name = "barcodeBody" };
        private readonly DataGridView _readers = new DataGridView { Name = "barcodeReaders" };
        private readonly DataGridView _fields = new DataGridView { Name = "barcodeFields" };
        private readonly DataGridView _recent = new DataGridView { Name = "barcodeRecent" };
        private readonly TextBox _prefix = new TextBox { Name = "barcodePrefix", Width = 120 };
        private readonly TextBox _suffix = new TextBox { Name = "barcodeSuffix", Width = 120 };
        private readonly TextBox _try = new TextBox { Name = "barcodeTry", Width = 320 };
        private readonly Label _tryResult = new Label { Name = "barcodeTryResult", AutoSize = false, Height = 44, Width = 760, ForeColor = Color.FromArgb(60, 64, 70) };
        private readonly Label _status = new Label { Name = "barcodeStatus", AutoSize = true, ForeColor = Color.FromArgb(160, 40, 40) };
        private readonly DirtyTracker _dirty = new DirtyTracker();
        private readonly Timer _clock = new Timer { Interval = 1000 };
        private readonly Action<BarcodeScan> _onScan;
        private bool _filling;

        public BarcodeSettingsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;
            var tips = UiStyle.Tips();

            // ---- the bar ----
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false, Padding = new Padding(4, 4, 0, 0), BackColor = Color.White };
            var save = UiStyle.ToolButton("💾", T("Bc_Save", "Keep the barcode settings (the readers open again with them)"), tips, (s, e) => Keep());
            var revert = UiStyle.ToolButton("↺", T("Bc_Revert", "Throw the changes away and show the kept settings"), tips, (s, e) => Fill());
            save.Name = "barcodeSave"; revert.Name = "barcodeRevert";
            foreach (var b in new Control[] { save, revert }) { b.Margin = new Padding(2, 0, 2, 0); bar.Controls.Add(b); }
            _status.Margin = new Padding(14, 9, 0, 0);
            bar.Controls.Add(_status);
            _dirty.Guard(save, revert);

            int y = 6;
            // ---- the readers ----
            y = Heading(T("Bc_Readers", "Barcode readers"), y);
            var rbar = Bar(y, tips,
                UiStyle.ToolButton("➕", T("Bc_AddReader", "Add a reader"), tips, (s, e) => { AddReader(new BarcodeReaderConfig { Name = "Reader " + (_readers.Rows.Count + 1) }); _dirty.MarkDirty(); }),
                UiStyle.ToolButton("🗑", T("Bc_RemoveReader", "Remove the selected reader"), tips, (s, e) => RemoveRow(_readers)));
            y += rbar.Height + 2;
            SetupGrid(_readers, y, 150);
            _readers.Columns.Add(Col("name", T("Bc_Name", "Name"), 16));
            _readers.Columns.Add(Combo("kind", T("Bc_Kind", "Kind"), 12, Enum.GetNames(typeof(BarcodeReaderKind))));
            _readers.Columns.Add(Col("port", T("Bc_Port", "Port"), 9));
            _readers.Columns.Add(Combo("baud", T("Bc_Baud", "Baud rate"), 10, new[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" }));
            _readers.Columns.Add(Combo("bits", T("Bc_DataBits", "Data bits"), 7, new[] { "7", "8" }));
            _readers.Columns.Add(Combo("parity", T("Bc_Parity", "Parity"), 9, Enum.GetNames(typeof(Parity))));
            _readers.Columns.Add(Combo("stop", T("Bc_StopBits", "Stop bits"), 9, new[] { "One", "OnePointFive", "Two" }));
            _readers.Columns.Add(Combo("machine", T("Bc_Machine", "Machine"), 12, new[] { "" }.Concat(Kaban.UI.Helpers.MachineLinks.All().Select(m => m.ModuleId)).Distinct().ToArray()));
            _readers.Columns.Add(new DataGridViewCheckBoxColumn { Name = "on", HeaderText = T("Bc_On", "On"), FillWeight = 5 });
            var st = Col("state", T("Bc_State", "State"), 20); st.ReadOnly = true;
            _readers.Columns.Add(st);
            _body.Controls.Add(_readers);
            y += _readers.Height + 6;
            var hint = Note(T("Bc_ReadersNote", "A serial reader: its COM port (3 or COM3) as the old program. A keyboard reader: a USB scanner that types - its codes are read while no box to type into has the focus. Machine: the machine its codes are for (empty: any)."), y);
            y += hint.Height + 4;

            // the prefix and suffix the scanner adds
            _body.Controls.Add(Caption(T("Bc_Prefix", "Taken off the front"), 16, y + 3));
            _prefix.Location = new Point(170, y); _body.Controls.Add(_prefix);
            _body.Controls.Add(Caption(T("Bc_Suffix", "Taken off the end"), 310, y + 3));
            _suffix.Location = new Point(460, y); _body.Controls.Add(_suffix);
            _prefix.TextChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };
            _suffix.TextChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };
            y += 34;

            // ---- the parts of a code ----
            y = Heading(T("Bc_Fields", "The parts of a code"), y);
            var fbar = Bar(y, tips,
                UiStyle.ToolButton("➕", T("Bc_AddField", "Add a part"), tips, (s, e) => { AddField(new BarcodeFieldConfig { Start = 1, Length = 1 }); _dirty.MarkDirty(); }),
                UiStyle.ToolButton("🗑", T("Bc_RemoveField", "Remove the selected part"), tips, (s, e) => RemoveRow(_fields)),
                UiStyle.ToolButton("↻", T("Bc_UsualFields", "The usual parts: the profile code 1 - 10, the width 11 - 14, the height 15 - 18 (the old MK2010's)"), tips,
                    (s, e) => { _fields.Rows.Clear(); foreach (var f in BarcodeReaders.ProfileSizeLayout()) AddField(f); _dirty.MarkDirty(); }));
            y += fbar.Height + 2;
            SetupGrid(_fields, y, 150);
            _fields.Columns.Add(Col("name", T("Bc_FieldName", "Name"), 22));
            _fields.Columns.Add(Combo("kind", T("Bc_FieldKind", "What it is"), 18, Enum.GetNames(typeof(BarcodeFieldKind))));
            _fields.Columns.Add(Col("start", T("Bc_Start", "From (1 the first)"), 14));
            _fields.Columns.Add(Col("length", T("Bc_Length", "Length"), 10));
            _fields.Columns.Add(Col("divisor", T("Bc_Divisor", "Divided by"), 10));
            _body.Controls.Add(_fields);
            y += _fields.Height + 6;
            var fnote = Note(T("Bc_FieldsNote", "None set: the machine's own (MK2010, MD3020: the profile code 1 - 10, the width 11 - 14, the height 15 - 18 in mm). A width and a height read go into the machine's job values."), y);
            y += fnote.Height + 4;

            // ---- trying a code ----
            y = Heading(T("Bc_Try", "Try a code"), y);
            _try.Location = new Point(16, y);
            _body.Controls.Add(_try);
            var read = new Button { Text = T("Bc_Read", "Read"), Location = new Point(346, y - 1), Width = 90, Height = 28, Name = "barcodeRead" };
            UiStyle.Round(read);
            read.Click += (s, e) => TryCode();
            _try.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; TryCode(); } };
            _body.Controls.Add(read);
            y += 34;
            _tryResult.Location = new Point(16, y);
            _body.Controls.Add(_tryResult);
            y += _tryResult.Height + 6;

            // ---- read last ----
            y = Heading(T("Bc_Recent", "Read last"), y);
            SetupGrid(_recent, y, 220);
            _recent.ReadOnly = true;
            _recent.Columns.Add(Col("at", T("Bc_At", "Time"), 12));
            _recent.Columns.Add(Col("source", T("Bc_Source", "Reader"), 12));
            _recent.Columns.Add(Col("code", T("Bc_Code", "Code"), 26));
            _recent.Columns.Add(Col("parts", T("Bc_Parts", "Parts"), 30));
            _recent.Columns.Add(Col("taken", T("Bc_TakenBy", "Taken by"), 20));
            _body.Controls.Add(_recent);

            Controls.Add(_body);
            Controls.Add(bar);

            _readers.CellValueChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };
            _fields.CellValueChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };
            _onScan = scan => { try { if (IsHandleCreated) BeginInvoke((Action)ShowRecent); } catch { } };
            BarcodeReaders.Scanned += _onScan;
            _clock.Tick += (s, e) => ShowState();
            VisibleChanged += (s, e) => { if (Visible) _clock.Start(); else _clock.Stop(); };
            Disposed += (s, e) => { BarcodeReaders.Scanned -= _onScan; _clock.Dispose(); };
            Fill();
        }

        // ------------------------------------------------------------------ building

        private int Heading(string text, int y)
        {
            _body.Controls.Add(new Label { Text = text, Font = UiStyle.Title, AutoSize = true, Location = new Point(12, y), ForeColor = Color.FromArgb(32, 33, 36) });
            return y + 30;
        }

        private Label Note(string text, int y)
        {
            var l = new Label { Text = text, AutoSize = false, Width = 760, Location = new Point(16, y), ForeColor = Color.FromArgb(110, 116, 124), Font = UiStyle.Small };
            l.Height = TextRenderer.MeasureText(text, l.Font, new Size(l.Width, 0), TextFormatFlags.WordBreak).Height + 4;
            _body.Controls.Add(l);
            return l;
        }

        private static Label Caption(string text, int x, int y)
        {
            return new Label { Text = text, AutoSize = true, Location = new Point(x, y) };
        }

        private FlowLayoutPanel Bar(int y, ToolTip tips, params Button[] buttons)
        {
            var p = new FlowLayoutPanel { Location = new Point(12, y), Height = 36, Width = 400, WrapContents = false };
            foreach (var b in buttons) { b.Margin = new Padding(2, 0, 2, 0); p.Controls.Add(b); }
            _body.Controls.Add(p);
            return p;
        }

        private static void SetupGrid(DataGridView g, int y, int height)
        {
            g.Location = new Point(16, y);
            g.Size = new Size(860, height);
            g.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.RowHeadersVisible = false;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.CellSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.BackgroundColor = Color.White;
            UiStyle.GridLook(g);
            g.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (g.IsCurrentCellDirty && (g.CurrentCell is DataGridViewCheckBoxCell || g.CurrentCell is DataGridViewComboBoxCell))
                    g.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            g.DataError += (s, e) => { e.ThrowException = false; };
        }

        private static DataGridViewTextBoxColumn Col(string name, string head, float weight)
        {
            return new DataGridViewTextBoxColumn { Name = name, HeaderText = head, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable };
        }

        private static DataGridViewComboBoxColumn Combo(string name, string head, float weight, string[] items)
        {
            var c = new DataGridViewComboBoxColumn { Name = name, HeaderText = head, FillWeight = weight, FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox };
            c.Items.AddRange(items.Cast<object>().ToArray());
            return c;
        }

        // ------------------------------------------------------------------ the values

        private void Fill()
        {
            _filling = true;
            try
            {
                _readers.Rows.Clear();
                foreach (var r in BarcodeReaders.Readers) AddReader(r);
                _fields.Rows.Clear();
                foreach (var f in BarcodeReaders.Fields) AddField(f);
                _prefix.Text = BarcodeReaders.Prefix;
                _suffix.Text = BarcodeReaders.Suffix;
            }
            finally { _filling = false; }
            _dirty.Reset();
            _status.Text = "";
            ShowState();
            ShowRecent();
        }

        private void AddReader(BarcodeReaderConfig r)
        {
            var machines = ((DataGridViewComboBoxColumn)_readers.Columns["machine"]).Items;
            if (!machines.Contains(r.Machine)) machines.Add(r.Machine);
            string stop = r.StopBits == StopBits.None ? "One" : r.StopBits.ToString();
            _readers.Rows.Add(r.Name, r.Kind.ToString(), r.Port, r.BaudRate.ToString(CI), r.DataBits.ToString(CI), r.Parity.ToString(), stop, r.Machine, r.Enabled, "");
        }

        private void AddField(BarcodeFieldConfig f)
        {
            _fields.Rows.Add(f.Name, f.Kind.ToString(), f.Start.ToString(CI), f.Length.ToString(CI), f.Divisor.ToString("0.###", CI));
        }

        private void RemoveRow(DataGridView g)
        {
            if (g.CurrentCell == null) return;
            g.Rows.RemoveAt(g.CurrentCell.RowIndex);
            _dirty.MarkDirty();
        }

        private static string Cell(DataGridViewRow r, string col) { var v = r.Cells[col].Value; return v == null ? "" : v.ToString().Trim(); }

        /// <summary>The rows as settings; why not, when one is wrong.</summary>
        private bool Read(out List<BarcodeReaderConfig> readers, out List<BarcodeFieldConfig> fields, out string problem)
        {
            readers = new List<BarcodeReaderConfig>();
            fields = new List<BarcodeFieldConfig>();
            problem = "";
            foreach (DataGridViewRow row in _readers.Rows)
            {
                var r = new BarcodeReaderConfig { Name = Cell(row, "name"), Port = Cell(row, "port"), Machine = Cell(row, "machine"), Enabled = Equals(row.Cells["on"].Value, true) };
                BarcodeReaderKind k; Parity p; StopBits sb; int n;
                if (Enum.TryParse(Cell(row, "kind"), out k)) r.Kind = k;
                if (int.TryParse(Cell(row, "baud"), NumberStyles.Integer, CI, out n) && n > 0) r.BaudRate = n;
                if (int.TryParse(Cell(row, "bits"), NumberStyles.Integer, CI, out n)) r.DataBits = n;
                if (Enum.TryParse(Cell(row, "parity"), out p)) r.Parity = p;
                if (Enum.TryParse(Cell(row, "stop"), out sb)) r.StopBits = sb;
                if (r.Name.Length == 0) r.Name = "Reader " + (readers.Count + 1);
                if (r.Kind == BarcodeReaderKind.Serial && r.Enabled && !r.PortName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                {
                    problem = string.Format(T("Bc_BadPort", "{0}: the port is not a COM port (3 or COM3)."), r.Name);
                    return false;
                }
                readers.Add(r);
            }
            foreach (DataGridViewRow row in _fields.Rows)
            {
                var f = new BarcodeFieldConfig { Name = Cell(row, "name") };
                BarcodeFieldKind k; int n; double d;
                if (Enum.TryParse(Cell(row, "kind"), out k)) f.Kind = k;
                if (!int.TryParse(Cell(row, "start"), NumberStyles.Integer, CI, out n) || n < 1) { problem = string.Format(T("Bc_BadStart", "{0}: where it starts is 1 or more."), f.Name); return false; }
                f.Start = n;
                if (!int.TryParse(Cell(row, "length"), NumberStyles.Integer, CI, out n) || n < 1) { problem = string.Format(T("Bc_BadLength", "{0}: its length is 1 or more."), f.Name); return false; }
                f.Length = n;
                if (double.TryParse(Cell(row, "divisor").Replace(',', '.'), NumberStyles.Float, CI, out d) && d > 0) f.Divisor = d;
                fields.Add(f);
            }
            return true;
        }

        private void Keep()
        {
            List<BarcodeReaderConfig> readers; List<BarcodeFieldConfig> fields; string problem;
            if (!Read(out readers, out fields, out problem)) { _status.ForeColor = Color.FromArgb(160, 40, 40); _status.Text = problem; return; }
            BarcodeReaders.Save(readers, fields, _prefix.Text, _suffix.Text);
            Fill();
            _status.ForeColor = Color.FromArgb(30, 110, 50);
            _status.Text = T("Bc_Saved", "Kept - the readers opened again.");
        }

        private void TryCode()
        {
            List<BarcodeReaderConfig> readers; List<BarcodeFieldConfig> fields; string problem;
            Read(out readers, out fields, out problem);
            var scan = BarcodeReaders.Parse(_try.Text, fields.Count > 0 ? fields : BarcodeReaders.FieldsInUse, "Test");
            _tryResult.Text = T("Bc_Code", "Code") + ": " + scan.Code + "\n" + Parts(scan);
        }

        private static string Parts(BarcodeScan s)
        {
            var parts = s.Fields.Select(kv => kv.Key + " = " + kv.Value).ToList();
            if (s.Width.HasValue) parts.Add("W " + s.Width.Value.ToString("0.##", CI));
            if (s.Height.HasValue) parts.Add("H " + s.Height.Value.ToString("0.##", CI));
            var text = string.Join(", ", parts);
            if (s.Problems.Count > 0) text += (text.Length > 0 ? " - " : "") + string.Join("; ", s.Problems);
            return text;
        }

        private void ShowState()
        {
            if (IsDisposed) return;
            var state = BarcodeReaders.State;
            foreach (DataGridViewRow row in _readers.Rows)
            {
                string name = Cell(row, "name"), kind = Cell(row, "kind");
                string s;
                if (!Equals(row.Cells["on"].Value, true)) s = T("Bc_Off", "off");
                else if (kind == BarcodeReaderKind.Keyboard.ToString()) s = T("Bc_KeyboardState", "read from the keyboard");
                else s = state.FirstOrDefault(x => x.StartsWith(name + " (", StringComparison.Ordinal)) ?? T("Bc_NotOpen", "not open - keep the settings");
                if (!Equals(row.Cells["state"].Value, s)) row.Cells["state"].Value = s;
            }
        }

        private void ShowRecent()
        {
            if (IsDisposed) return;
            _recent.Rows.Clear();
            foreach (var s in BarcodeReaders.Recent)
                _recent.Rows.Add(s.At.ToString("HH:mm:ss", CI), s.Source, s.Code, Parts(s), s.TakenBy.Length > 0 ? s.TakenBy : "-");
        }

        /// <summary>The rows (tests).</summary>
        public DataGridView ReadersGrid { get { return _readers; } }
        public DataGridView FieldsGrid { get { return _fields; } }
        public DataGridView RecentGrid { get { return _recent; } }
        public string TryResult { get { return _tryResult.Text; } }
        public void TryText(string code) { _try.Text = code; TryCode(); }
        public void Save() { Keep(); }
        public string Status { get { return _status.Text; } }
    }

    /// <summary>HMI Settings > Barcodes: a Maintenance user's (the readers' ports are the PC's).</summary>
    public class BarcodeSettingsPage : ISettingsPage
    {
        public string TitleKey { get { return "Tab_Barcodes"; } }
        public string TitleFallback { get { return "Barcodes"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Maintenance; } }
        public UserControl GetControl() { return new BarcodeSettingsControl(); }
    }
}
