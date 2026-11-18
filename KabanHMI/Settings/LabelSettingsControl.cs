using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Labels;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using Kaban.UI.Profiles;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// HMI SETTINGS > LABELS (17 November: "take barcode reading and writing from KabanMachineSystem as well") - the old
    /// KabanLabelDesigner (its label list, the label's header - size, COM port / printer, baud rate, language, main / leftover,
    /// ribbon - and its fields on the drawing area) and the machines' printing:
    ///   the labels      a list on the left; the label picked: its printer and size, its fields as a table (the value, where,
    ///                   the font ...), drawn under it as it will be printed (a click on a field picks its row);
    ///   test print      the label with sample values, on its printer; or its commands saved as a file (a printer's own tool sends it);
    ///   the PLC asks    the machines' label requests (FA2010: a piece cut, a leftover) - each off until switched on, its devices;
    ///   printed last    the last labels, printed or why not.
    /// </summary>
    public sealed class LabelSettingsControl : UserControl, IRebuildForLanguage
    {
        object IRebuildForLanguage.Place { get { return null; } }
        void IRebuildForLanguage.ReturnTo(object place) { }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        private readonly Panel _body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Name = "labelBody" };
        private readonly ListBox _list = new ListBox { Name = "labelList", IntegralHeight = false };
        private readonly TextBox _name = new TextBox { Name = "labelName", Width = 180 };
        private readonly ComboBox _use = new ComboBox { Name = "labelUse", DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        private readonly CheckBox _on = new CheckBox { Name = "labelOn", AutoSize = true };
        private readonly ComboBox _machine = new ComboBox { Name = "labelMachine", DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        private readonly NumericUpDown _w = new NumericUpDown { Name = "labelWidth", Minimum = 10, Maximum = 104, Width = 64 };
        private readonly NumericUpDown _h = new NumericUpDown { Name = "labelHeight", Minimum = 5, Maximum = 300, Width = 64 };
        private readonly ComboBox _printer = new ComboBox { Name = "labelPrinter", DropDownStyle = ComboBoxStyle.DropDown, Width = 216 };
        private readonly ComboBox _baud = new ComboBox { Name = "labelBaud", DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        private readonly ComboBox _language = new ComboBox { Name = "labelLanguage", DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        private readonly ComboBox _printerLanguage = new ComboBox { Name = "labelPrinterLanguage", DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
        private readonly CheckBox _ribbon = new CheckBox { Name = "labelRibbon", AutoSize = true };
        private readonly DataGridView _fields = new DataGridView { Name = "labelFields" };
        private readonly LabelPreview _preview = new LabelPreview();
        private readonly DataGridView _triggers = new DataGridView { Name = "labelTriggers" };
        private readonly DataGridView _recent = new DataGridView { Name = "labelRecent" };
        private readonly Label _status = new Label { Name = "labelStatus", AutoSize = true };
        private readonly DirtyTracker _dirty = new DirtyTracker();
        private readonly Action<LabelPrint> _onPrinted;
        private List<LabelTemplate> _labels = new List<LabelTemplate>();
        private LabelTemplate _current;
        private string _currentOldName;
        private bool _filling;

        private static readonly Color Bad = Color.FromArgb(160, 40, 40), Good = Color.FromArgb(30, 110, 50);

        public LabelSettingsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;
            var tips = UiStyle.Tips();

            // ---- the bar ----
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false, Padding = new Padding(4, 4, 0, 0), BackColor = Color.White };
            var save = UiStyle.ToolButton("💾", T("Lbl_Save", "Keep the label and the PLC's label requests"), tips, (s, e) => Keep());
            var revert = UiStyle.ToolButton("↺", T("Lbl_Revert", "Throw the changes away and show the kept labels"), tips, (s, e) => Fill(_current != null ? _current.Name : null));
            var add = UiStyle.ToolButton("➕", T("Lbl_New", "A new label for the pieces (the usual fields on it)"), tips, (s, e) => New(LabelUse.Piece));
            var addLeft = UiStyle.ToolButton("✂", T("Lbl_NewLeftover", "A new label for the leftovers"), tips, (s, e) => New(LabelUse.Leftover));
            var copy = UiStyle.ToolButton("📋", T("Lbl_Copy", "A copy of the label"), tips, (s, e) => CopyLabel());
            var del = UiStyle.ToolButton("🗑", T("Lbl_Delete", "Delete the label"), tips, (s, e) => DeleteLabel());
            var test = UiStyle.ToolButton("🖨", T("Lbl_Test", "Print the label with sample values (on its printer)"), tips, (s, e) => TestPrint());
            var file = UiStyle.ToolButton("📄", T("Lbl_SaveFile", "Save the label's printer commands as a file"), tips, (s, e) => SaveFile());
            save.Name = "labelSave"; revert.Name = "labelRevert"; add.Name = "labelNew"; addLeft.Name = "labelNewLeftover"; copy.Name = "labelCopy";
            del.Name = "labelDelete"; test.Name = "labelTest"; file.Name = "labelFile";
            foreach (var b in new Control[] { save, revert, add, addLeft, copy, del, test, file }) { b.Margin = new Padding(2, 0, 2, 0); bar.Controls.Add(b); }
            _status.Margin = new Padding(14, 9, 0, 0);
            bar.Controls.Add(_status);
            _dirty.Guard(save, revert);

            // ---- the list ----
            _list.Location = new Point(12, 10);
            _list.Size = new Size(190, 520);
            _list.SelectedIndexChanged += (s, e) => Pick();
            _body.Controls.Add(_list);

            // ---- the label's header ----
            int x0 = 216, y = 10;
            const int c2 = 340, c3 = 590;
            Row(T("Lbl_Name", "Name"), _name, x0, y); Row(T("Lbl_Use", "Printed for"), _use, x0 + c2, y); Row(T("Lbl_On", "On"), _on, x0 + c3, y);
            y += 34;
            // its own size, not a panel's 200 x 100 (it covered the printer and the language under it)
            var size = new FlowLayoutPanel { Size = new Size(210, 28), WrapContents = false, Margin = Padding.Empty };
            size.Controls.Add(_w); size.Controls.Add(new Label { Text = "x", AutoSize = true, Margin = new Padding(4, 6, 4, 0) }); size.Controls.Add(_h);
            size.Controls.Add(new Label { Text = "mm", AutoSize = true, Margin = new Padding(4, 6, 0, 0) });
            Row(T("Lbl_Size", "Size"), size, x0, y); Row(T("Lbl_Machine", "Machine"), _machine, x0 + c2, y);
            y += 34;
            Row(T("Lbl_Printer", "Printer"), _printer, x0, y); Row(T("Lbl_Baud", "Baud rate"), _baud, x0 + c2, y);
            y += 34;
            Row(T("Lbl_Language", "Language"), _language, x0, y); Row(T("Lbl_PrinterLanguage", "Printer language"), _printerLanguage, x0 + c2, y);
            Row(T("Lbl_Ribbon", "Ribbon"), _ribbon, x0 + c3, y);
            y += 36;
            var pnote = new Label
            {
                AutoSize = false, Location = new Point(x0, y), Size = new Size(660, 34), ForeColor = Color.FromArgb(110, 116, 124), Font = UiStyle.Small,
                Text = T("Lbl_PrinterNote", "Printer: COM3 (a serial printer), a Windows printer's name, 192.168.1.50:9100 (a network printer) or file:C:\\Labels\\label.prn. EPL: the old program's Zebra printers - ZPL: the newer ones.")
            };
            _body.Controls.Add(pnote);
            y += 38;
            _use.Items.AddRange(new object[] { T("Lbl_UsePiece", "a piece"), T("Lbl_UseLeftover", "a leftover") });
            _machine.Items.Add(T("Lbl_AnyMachine", "every machine"));
            foreach (var m in MachineLinks.All()) _machine.Items.Add(m.ModuleId);
            _baud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
            foreach (var l in LabelWriter.Languages) _language.Items.Add(l);
            _printerLanguage.Items.AddRange(new object[] { "EPL", "ZPL" });
            _printer.DropDown += (s, e) => FillPrinters();
            FillPrinters();

            // ---- the fields ----
            var fbar = new FlowLayoutPanel { Location = new Point(x0, y), Size = new Size(400, 36), WrapContents = false };
            var fadd = UiStyle.ToolButton("➕", T("Lbl_AddField", "Add a field"), tips, (s, e) => AddField());
            var fdel = UiStyle.ToolButton("🗑", T("Lbl_RemoveField", "Remove the selected field"), tips, (s, e) => RemoveField());
            var fup = UiStyle.ToolButton("▲", T("Lbl_FieldUp", "Up the list"), tips, (s, e) => MoveField(-1));
            var fdown = UiStyle.ToolButton("▼", T("Lbl_FieldDown", "Down the list"), tips, (s, e) => MoveField(1));
            foreach (var b in new Control[] { fadd, fdel, fup, fdown }) { b.Margin = new Padding(2, 0, 2, 0); fbar.Controls.Add(b); }
            _body.Controls.Add(fbar);
            y += 38;
            Grid(_fields, x0, y, 660, 190);
            var keys = new List<string> { "" };
            keys.AddRange(LabelKeys.All.Select(k => k.Key));
            var keyCol = new DataGridViewComboBoxColumn { Name = "key", HeaderText = T("Lbl_Value", "Value"), FillWeight = 20, FlatStyle = FlatStyle.Flat, DisplayMember = "Text", ValueMember = "Key" };
            foreach (var k in keys) keyCol.Items.Add(new KeyItem(k));
            _fields.Columns.Add(keyCol);
            _fields.Columns.Add(TextCol("text", T("Lbl_Text", "Text (in front)"), 16));
            var typeCol = new DataGridViewComboBoxColumn { Name = "type", HeaderText = T("Lbl_Type", "As"), FillWeight = 11, FlatStyle = FlatStyle.Flat };
            typeCol.Items.AddRange(Enum.GetNames(typeof(LabelFieldType)).Cast<object>().ToArray());
            _fields.Columns.Add(typeCol);
            _fields.Columns.Add(TextCol("x", "X mm", 6));
            _fields.Columns.Add(TextCol("y", "Y mm", 6));
            var fontCol = new DataGridViewComboBoxColumn { Name = "font", HeaderText = T("Lbl_Font", "Font"), FillWeight = 6, FlatStyle = FlatStyle.Flat };
            fontCol.Items.AddRange(new object[] { "1", "2", "3", "4", "5" });
            _fields.Columns.Add(fontCol);
            _fields.Columns.Add(TextCol("w", T("Lbl_W", "W mm"), 6));
            _fields.Columns.Add(TextCol("h", T("Lbl_H", "H mm"), 6));
            _fields.Columns.Add(TextCol("yes", T("Lbl_Yes", "Yes / date format"), 12));
            _fields.Columns.Add(TextCol("no", T("Lbl_No", "No"), 8));
            _fields.CellValueChanged += (s, e) => { if (_filling) return; if (e.ColumnIndex == 0) KeyChosen(e.RowIndex); _dirty.MarkDirty(); Redraw(); };
            _fields.CurrentCellChanged += (s, e) => Redraw();
            y += 196;

            // ---- the drawing ----
            _preview.Location = new Point(x0, y);
            _preview.Size = new Size(660, 300);
            _preview.FieldClicked += i => { if (i < _fields.Rows.Count) _fields.CurrentCell = _fields.Rows[i].Cells[0]; };
            _body.Controls.Add(_preview);
            y += 308;

            // ---- the PLC's requests ----
            _body.Controls.Add(new Label { Text = T("Lbl_Triggers", "The PLC asks for a label"), Font = UiStyle.Title, AutoSize = true, Location = new Point(12, y) });
            y += 30;
            Grid(_triggers, 16, y, 860, 110);
            _triggers.Columns.Add(new DataGridViewCheckBoxColumn { Name = "on", HeaderText = T("Lbl_On", "On"), FillWeight = 5 });
            var w1 = TextCol("what", T("Lbl_Request", "Request"), 26); w1.ReadOnly = true; _triggers.Columns.Add(w1);
            _triggers.Columns.Add(TextCol("bit", T("Lbl_Bit", "Its bit"), 10));
            _triggers.Columns.Add(TextCol("value", T("Lbl_ValueWord", "Its value"), 10));
            _triggers.Columns.Add(TextCol("divisor", T("Lbl_Divisor", "Divided by"), 9));
            var w2 = TextCol("note", T("Lbl_TriggerNote", "Note"), 40); w2.ReadOnly = true; _triggers.Columns.Add(w2);
            _triggers.CellValueChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };
            y += 116;
            var tnote = new Label
            {
                AutoSize = false, Location = new Point(16, y), Size = new Size(860, 34), ForeColor = Color.FromArgb(110, 116, 124), Font = UiStyle.Small,
                Text = T("Lbl_TriggersNote", "On: the PLC sets the bit with the piece's ID (a leftover's length) in its value - the HMI turns the bit off and prints the label on for it. Off until switched on: check the devices with the PLC program first.")
            };
            _body.Controls.Add(tnote);
            y += 40;

            // ---- printed last ----
            _body.Controls.Add(new Label { Text = T("Lbl_Recent", "Printed last"), Font = UiStyle.Title, AutoSize = true, Location = new Point(12, y) });
            y += 30;
            Grid(_recent, 16, y, 860, 180);
            _recent.ReadOnly = true;
            _recent.Columns.Add(TextCol("at", T("Lbl_At", "Time"), 10));
            _recent.Columns.Add(TextCol("label", T("Lbl_Label", "Label"), 14));
            _recent.Columns.Add(TextCol("source", T("Lbl_Source", "For"), 30));
            _recent.Columns.Add(TextCol("values", T("Lbl_Values", "Values"), 26));
            _recent.Columns.Add(TextCol("result", T("Lbl_Result", "Result"), 20));

            Controls.Add(_body);
            Controls.Add(bar);

            foreach (var c in new Control[] { _name, _printer })
                c.TextChanged += (s, e) => { if (!_filling) { _dirty.MarkDirty(); Redraw(); } };
            foreach (var c in new ComboBox[] { _use, _machine, _baud, _language, _printerLanguage })
                c.SelectedIndexChanged += (s, e) => { if (!_filling) { _dirty.MarkDirty(); Redraw(); } };
            foreach (var c in new NumericUpDown[] { _w, _h })
                c.ValueChanged += (s, e) => { if (!_filling) { _dirty.MarkDirty(); Redraw(); } };
            foreach (var c in new CheckBox[] { _on, _ribbon })
                c.CheckedChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };
            _onPrinted = r => { try { if (IsHandleCreated) BeginInvoke((Action)ShowRecent); } catch { } };
            LabelService.Done += _onPrinted;
            Disposed += (s, e) => LabelService.Done -= _onPrinted;
            Fill(null);
        }

        private sealed class KeyItem
        {
            public string Key { get; private set; }
            public string Text { get; private set; }
            public KeyItem(string k) { Key = k; Text = LabelKeys.Caption(k); }
            public override string ToString() { return Text; }
        }

        private void Row(string caption, Control c, int x, int y)
        {
            var l = new Label { Text = caption, AutoSize = false, Location = new Point(x, y + 4), Size = new Size(c is CheckBox ? 60 : 112, 22), AutoEllipsis = true };
            _body.Controls.Add(l);
            c.Location = new Point(x + l.Width + 4, y);
            _body.Controls.Add(c);
        }

        private void Grid(DataGridView g, int x, int y, int w, int h)
        {
            g.Location = new Point(x, y);
            g.Size = new Size(w, h);
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
            _body.Controls.Add(g);
        }

        private static DataGridViewTextBoxColumn TextCol(string name, string head, float weight)
        {
            return new DataGridViewTextBoxColumn { Name = name, HeaderText = head, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable };
        }

        private void FillPrinters()
        {
            var text = _printer.Text;
            _printer.Items.Clear();
            foreach (var p in LabelPrinters.SerialPorts) _printer.Items.Add(p);
            try { foreach (string p in System.Drawing.Printing.PrinterSettings.InstalledPrinters) _printer.Items.Add(p); } catch { }
            _printer.Text = text;
        }

        // ------------------------------------------------------------------ the labels

        private void Fill(string select)
        {
            _filling = true;
            try
            {
                _labels = LabelStore.All();
                _list.Items.Clear();
                foreach (var t in _labels) _list.Items.Add(Line(t));
                int i = select != null ? _labels.FindIndex(t => string.Equals(t.Name, select, StringComparison.OrdinalIgnoreCase)) : -1;
                if (i < 0 && _labels.Count > 0) i = 0;
                _current = i >= 0 ? _labels[i].Clone() : null;
                _currentOldName = _current != null ? _current.Name : null;
                if (i >= 0) _list.SelectedIndex = i;
                Show(_current);
                FillTriggers();
            }
            finally { _filling = false; }
            _dirty.Reset();
            ShowRecent();
        }

        private static string Line(LabelTemplate t)
        {
            return t.Name + "  (" + (t.Use == LabelUse.Leftover ? LanguageManager.GetString("Lbl_UseLeftover", "a leftover") : LanguageManager.GetString("Lbl_UsePiece", "a piece")) +
                   (t.Enabled ? "" : ", " + LanguageManager.GetString("Bc_Off", "off")) + ")";
        }

        private void Pick()
        {
            if (_filling || _list.SelectedIndex < 0 || _list.SelectedIndex >= _labels.Count) return;
            var next = _labels[_list.SelectedIndex];
            if (_current != null && string.Equals(next.Name, _currentOldName, StringComparison.OrdinalIgnoreCase)) return;
            if (_dirty.IsDirty && !Ask.YesNo(FindForm(), T("Tab_Labels", "Labels"), T("Lbl_Unsaved", "The label's changes are not kept. Throw them away?")))
            {
                _filling = true;
                _list.SelectedIndex = Math.Max(0, _labels.FindIndex(t => string.Equals(t.Name, _currentOldName, StringComparison.OrdinalIgnoreCase)));
                _filling = false;
                return;
            }
            _filling = true;
            try { _current = next.Clone(); _currentOldName = _current.Name; Show(_current); }
            finally { _filling = false; }
            _dirty.Reset();
        }

        private void Show(LabelTemplate t)
        {
            bool has = t != null;
            foreach (Control c in new Control[] { _name, _use, _on, _machine, _w, _h, _printer, _baud, _language, _printerLanguage, _ribbon, _fields }) c.Enabled = has;
            _fields.Rows.Clear();
            if (!has) { _preview.Show(null, null); return; }
            _name.Text = t.Name;
            _use.SelectedIndex = t.Use == LabelUse.Leftover ? 1 : 0;
            _on.Checked = t.Enabled;
            int mi = t.Machine.Length == 0 ? 0 : _machine.Items.IndexOf(t.Machine);
            if (mi < 0) { _machine.Items.Add(t.Machine); mi = _machine.Items.Count - 1; }
            _machine.SelectedIndex = mi;
            _w.Value = Math.Max(_w.Minimum, Math.Min(_w.Maximum, t.WidthMm));
            _h.Value = Math.Max(_h.Minimum, Math.Min(_h.Maximum, t.HeightMm));
            _printer.Text = t.Printer;
            var baud = t.BaudRate.ToString(CI);
            if (!_baud.Items.Contains(baud)) _baud.Items.Add(baud);
            _baud.SelectedItem = baud;
            var lang = (t.Language ?? "").ToLowerInvariant();
            if (!_language.Items.Contains(lang)) _language.Items.Add(lang);
            _language.SelectedItem = lang;
            _printerLanguage.SelectedItem = t.PrinterLanguage.ToString();
            _ribbon.Checked = t.HasRibbon;
            foreach (var f in t.Fields) AddRow(f);
            Redraw();
        }

        private void AddRow(LabelField f)
        {
            _fields.Rows.Add(f.Key ?? "", f.Text, f.Type.ToString(), f.X.ToString(CI), f.Y.ToString(CI), f.FontSize.ToString(CI),
                             f.Width.ToString(CI), f.Height.ToString(CI), f.ValueOnTrue, f.ValueOnFalse);
        }

        private static string Cell(DataGridViewRow r, string col) { var v = r.Cells[col].Value; return v == null ? "" : v.ToString(); }
        private static int Int(DataGridViewRow r, string col, int fallback)
        {
            int n;
            return int.TryParse(Cell(r, col).Trim(), NumberStyles.Integer, CI, out n) ? n : fallback;
        }

        /// <summary>The label as the page shows it now.</summary>
        private LabelTemplate FromPage()
        {
            if (_current == null) return null;
            var t = new LabelTemplate
            {
                Name = _name.Text.Trim().Length > 0 ? _name.Text.Trim() : "Label",
                Use = _use.SelectedIndex == 1 ? LabelUse.Leftover : LabelUse.Piece,
                Enabled = _on.Checked,
                Machine = _machine.SelectedIndex > 0 ? _machine.SelectedItem.ToString() : "",
                WidthMm = (int)_w.Value, HeightMm = (int)_h.Value,
                Printer = _printer.Text.Trim(),
                Language = _language.SelectedItem != null ? _language.SelectedItem.ToString() : "turkish",
                PrinterLanguage = Equals(_printerLanguage.SelectedItem, "ZPL") ? LabelPrinterLanguage.ZPL : LabelPrinterLanguage.EPL,
                HasRibbon = _ribbon.Checked
            };
            int baud;
            if (_baud.SelectedItem != null && int.TryParse(_baud.SelectedItem.ToString(), NumberStyles.Integer, CI, out baud)) t.BaudRate = baud;
            foreach (DataGridViewRow r in _fields.Rows)
            {
                var f = new LabelField { Key = Cell(r, "key"), Text = Cell(r, "text"), X = Int(r, "x", 0), Y = Int(r, "y", 0),
                                         FontSize = Math.Max(1, Math.Min(5, Int(r, "font", 2))), Width = Int(r, "w", 20), Height = Int(r, "h", 10),
                                         ValueOnTrue = Cell(r, "yes"), ValueOnFalse = Cell(r, "no") };
                LabelFieldType ft;
                if (Enum.TryParse(Cell(r, "type"), out ft)) f.Type = ft;
                t.Fields.Add(f);
            }
            return t;
        }

        private void Redraw()
        {
            var t = FromPage();
            int sel = _fields.CurrentCell != null ? _fields.CurrentCell.RowIndex : -1;
            _preview.Show(t, LabelKeys.Sample(), sel);
        }

        /// <summary>A value picked for a field: shown as that value usually is (a barcode, a date ...).</summary>
        private void KeyChosen(int row)
        {
            if (row < 0 || row >= _fields.Rows.Count) return;
            var key = Cell(_fields.Rows[row], "key");
            var kt = LabelKeys.All.FirstOrDefault(k => k.Key == key);
            if (kt.Key != null) _fields.Rows[row].Cells["type"].Value = kt.Value.ToString();
        }

        private void AddField()
        {
            if (_current == null) return;
            AddRow(new LabelField { Key = LabelKeys.ProfileCode, X = 2, Y = 2 + 6 * _fields.Rows.Count % 40 });
            _fields.CurrentCell = _fields.Rows[_fields.Rows.Count - 1].Cells[0];
            _dirty.MarkDirty();
            Redraw();
        }

        private void RemoveField()
        {
            if (_fields.CurrentCell == null) return;
            _fields.Rows.RemoveAt(_fields.CurrentCell.RowIndex);
            _dirty.MarkDirty();
            Redraw();
        }

        private void MoveField(int by)
        {
            if (_fields.CurrentCell == null) return;
            int i = _fields.CurrentCell.RowIndex, j = i + by, c = _fields.CurrentCell.ColumnIndex;
            if (j < 0 || j >= _fields.Rows.Count) return;
            var row = _fields.Rows[i];
            _fields.Rows.RemoveAt(i);
            _fields.Rows.Insert(j, row);
            _fields.CurrentCell = _fields.Rows[j].Cells[c];
            _dirty.MarkDirty();
            Redraw();
        }

        private string FreeName(string name)
        {
            var n = name;
            for (int i = 2; _labels.Any(t => string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase)); i++) n = name + " " + i;
            return n;
        }

        private void New(LabelUse use)
        {
            var t = LabelTemplate.Sample(use);
            t.Name = FreeName(t.Name);
            t.Enabled = !LabelStore.All().Any(x => x.Use == use && x.Enabled);
            Store(t, null);
        }

        private void CopyLabel()
        {
            var t = FromPage();
            if (t == null) return;
            t.Name = FreeName(t.Name);
            t.Enabled = false;
            Store(t, null);
        }

        private void DeleteLabel()
        {
            if (_current == null) return;
            if (!Ask.YesNo(FindForm(), T("Tab_Labels", "Labels"), string.Format(T("Lbl_AskDelete", "Delete the label {0}?"), _currentOldName))) return;
            LabelStore.Delete(_currentOldName);
            Fill(null);
            Say(T("Lbl_Deleted", "Deleted."), true);
        }

        private void Store(LabelTemplate t, string oldName)
        {
            try { LabelStore.Save(t, oldName); }
            catch (Exception ex) { Say(ex.Message, false); return; }
            Fill(t.Name);
            Say(T("Lbl_Kept", "Kept."), true);
        }

        private void Keep()
        {
            var t = FromPage();
            if (t != null)
            {
                if (!string.Equals(t.Name, _currentOldName, StringComparison.OrdinalIgnoreCase) &&
                    _labels.Any(x => string.Equals(x.Name, t.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    Say(string.Format(T("Lbl_NameTaken", "There is a label named {0} already."), t.Name), false);
                    return;
                }
                try { LabelStore.Save(t, _currentOldName); }
                catch (Exception ex) { Say(ex.Message, false); return; }
            }
            KeepTriggers();
            Fill(t != null ? t.Name : null);
            Say(T("Lbl_Kept", "Kept."), true);
        }

        private void Say(string text, bool ok)
        {
            _status.ForeColor = ok ? Good : Bad;
            _status.Text = text;
        }

        private void TestPrint()
        {
            var t = FromPage();
            if (t == null) return;
            Cursor = Cursors.WaitCursor;
            try
            {
                var r = LabelService.PrintNow(t, LabelKeys.Sample(), T("Lbl_TestSource", "Test print (HMI Settings > Labels)"));
                Say(r.Ok ? string.Format(T("Lbl_TestDone", "The test label went to {0}."), t.Printer) : r.Message, r.Ok);
            }
            finally { Cursor = Cursors.Default; }
        }

        private void SaveFile()
        {
            var t = FromPage();
            if (t == null) return;
            using (var d = new SaveFileDialog { FileName = t.Name + ".prn", Filter = T("Lbl_PrnFiles", "Printer files") + " (*.prn)|*.prn|" + T("Lbl_AllFiles", "All files") + " (*.*)|*.*" })
            {
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    File.WriteAllBytes(d.FileName, LabelWriter.Build(t, LabelKeys.Sample()));
                    Say(string.Format(T("Lbl_FileSaved", "Saved as {0}."), d.FileName), true);
                }
                catch (Exception ex) { Say(ex.Message, false); }
            }
        }

        // ------------------------------------------------------------------ the PLC's requests

        private void FillTriggers()
        {
            _triggers.Rows.Clear();
            foreach (var t in LabelTriggers.All)
            {
                string what = (t.Name.Length > 0 ? t.Name : t.ModuleId) + ": " + (t.Use == LabelUse.Leftover ? T("Lbl_UseLeftover", "a leftover") : T("Lbl_UsePiece", "a piece"));
                string note = string.Format(T("Lbl_TriggerDefault", "The machine's: {0} with {1}"), t.DefaultBitText(), t.DefaultValueText());
                if (LabelTriggers.HandledElsewhere(t)) note += " - " + T("Lbl_TriggerRecord", "the bit is a record's handshake too: the record turns it off");
                int i = _triggers.Rows.Add(t.IsOn, what, t.Bit, t.Value, t.Divisor.ToString("0.###", CI), note);
                _triggers.Rows[i].Tag = t;
            }
        }

        private void KeepTriggers()
        {
            foreach (DataGridViewRow r in _triggers.Rows)
            {
                var t = r.Tag as LabelTrigger;
                if (t == null) continue;
                double d;
                if (!double.TryParse(Cell(r, "divisor").Replace(',', '.'), NumberStyles.Float, CI, out d) || d <= 0) d = 1;
                bool on = Equals(r.Cells["on"].Value, true);
                string bit = Cell(r, "bit").Trim(), value = Cell(r, "value").Trim();
                if (on != t.IsOn || !string.Equals(bit, t.Bit, StringComparison.OrdinalIgnoreCase) || !string.Equals(value, t.Value, StringComparison.OrdinalIgnoreCase) || d != t.Divisor)
                    LabelTriggers.Set(t, on, bit, value, d);
            }
        }

        private void ShowRecent()
        {
            if (IsDisposed) return;
            _recent.Rows.Clear();
            foreach (var p in LabelService.Recent)
                _recent.Rows.Add(p.At.ToString("HH:mm:ss", CI), p.Template, p.Source, p.Summary, p.Ok ? T("Lbl_Printed", "printed") : p.Message);
        }

        /// <summary>The page's parts (tests).</summary>
        public ListBox List { get { return _list; } }
        public DataGridView FieldsGrid { get { return _fields; } }
        public DataGridView TriggersGrid { get { return _triggers; } }
        public DataGridView RecentGrid { get { return _recent; } }
        public LabelPreview Preview { get { return _preview; } }
        public string Status { get { return _status.Text; } }
        public TextBox NameBox { get { return _name; } }
        public ComboBox PrinterBox { get { return _printer; } }
        public void NewLabel(LabelUse use) { New(use); }
        public void Save() { Keep(); }
        public void Test() { TestPrint(); }
        public LabelTemplate Current { get { return FromPage(); } }
    }

    /// <summary>HMI Settings > Labels: a Maintenance user's.</summary>
    public class LabelSettingsPage : ISettingsPage
    {
        public string TitleKey { get { return "Tab_Labels"; } }
        public string TitleFallback { get { return "Labels"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Maintenance; } }
        public UserControl GetControl() { return new LabelSettingsControl(); }
    }
}
