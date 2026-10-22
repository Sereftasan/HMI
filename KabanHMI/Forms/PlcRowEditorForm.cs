using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// Edits ONE monitor row - Label, DataType, Address, Value - in a dialog; or,
    /// for a NEW row, a run of them.
    ///
    /// The grid on the monitor is editable in place, which is quicker with a
    /// keyboard. This exists for the panel PC: a 26px-high grid cell is a poor
    /// touch target, and typing an address into it with the on-screen keyboard
    /// while a 250ms refresh runs is unpleasant. Double-clicking a row opens this
    /// with the field of the cell that was double-clicked already selected -
    /// the one the operator meant to change.
    ///
    /// A ROW can be an ARRAY: "M100, 10 of them" is one item that expands to
    /// M100..M109, all with the same data type; "D200 as double words, 4 of
    /// them" to D200, D202, D204, D206 - a double word (or a float) takes two
    /// devices, so the next one starts two further on; a string as many words as
    /// its characters need. Addresses count in the device's own number system (X and Y octal, W
    /// and B hexadecimal), as everywhere else in the program.
    ///
    /// A ROW can be a STRUCTURE (19 October): a structured data type defined in "Structures..."
    /// is chosen like a simple one; as an array its elements are its length apart (R0, R16 ...).
    ///
    /// A COMMENT LINE has no address and no value: a heading in the list
    /// ("--- Robot clamps ---") so a long watch list can be read in parts.
    ///
    /// It edits a COPY. Nothing is written to the PLC until OK, so an abandoned
    /// edit cannot half-apply - which matters when the address is being changed.
    /// </summary>
    public class PlcRowEditorForm : Form
    {
        private readonly TextBox _label = new TextBox();
        private readonly ComboBox _dataType = new ComboBox();
        private readonly TextBox _address = new TextBox();
        private readonly TextBox _value = new TextBox();
        private readonly ComboBox _format = new ComboBox();
        private readonly NumericUpDown _chars = new NumericUpDown();
        private readonly NumericUpDown _count = new NumericUpDown();
        private readonly TextBox _divisor = new TextBox();
        private readonly NumericUpDown _decimals = new NumericUpDown();
        private readonly CheckBox _comment = new CheckBox();
        private readonly Label _preview = new Label();
        private readonly Label _labelCaption = new Label();
        private readonly Button _structures = new Button();
        private readonly bool _isNew;
        /// <summary>Filling the fields in from the label list (not the user typing); what the user typed himself.</summary>
        private bool _auto, _labelTyped, _typeTyped, _addressTyped;
        /// <summary>What a label list's entry filled in, and what the fields held before (put back when the text no longer names one).</summary>
        private bool _filled;
        private string _filledLabel, _filledAddress, _filledType, _keptLabel, _keptAddress, _keptType;
        private decimal _filledCount, _keptCount;
        /// <summary>The tab's station: its machine's structured data types are offered.</summary>
        private readonly int _station;

        /// <summary>The device types the engineer's list uses (see PlcLabels.csv).</summary>
        public static readonly string[] KnownDataTypes =
        {
            "Bit", "Word [Signed]", "Word [Unsigned]", "Double Word [Signed]", "Double Word [Unsigned]",
            "Float [Single Precision]", "String"
        };

        /// <summary>Decimal, Hexadecimal or Binary: how a whole number is shown and typed.</summary>
        public string RowDisplayFormat { get { return _format.Enabled ? (_format.SelectedItem as string ?? SimulationMonitorForm.FormatDecimal) : ""; } }

        /// <summary>A String's length in characters.</summary>
        public int RowStringLength { get { return (int)_chars.Value; } }

        /// <summary>The calculated value's divisor (22 October): 1 when not a number or not typed as one.</summary>
        public double RowDivisor
        {
            get
            {
                double d;
                return _divisor.Enabled && TryDivisor(_divisor.Text, out d) ? d : 1;
            }
        }

        /// <summary>The calculated value's decimal places (0 when not a number).</summary>
        public int RowDecimals { get { return _decimals.Enabled ? (int)_decimals.Value : 0; } }

        private static bool TryDivisor(string text, out double d)
        {
            return double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d) &&
                   d != 0 && !double.IsNaN(d) && !double.IsInfinity(d);
        }

        public string RowLabel { get { return _label.Text.Trim(); } }
        public string RowDataType { get { return _dataType.Text.Trim(); } }
        public string RowAddress { get { return _address.Text.Trim().ToUpperInvariant(); } }

        /// <summary>True when the row is a comment line (no address, no value).</summary>
        public bool IsComment { get { return _comment.Checked; } }

        /// <summary>How many elements: 1 for a single device, more for an ARRAY (one item that expands).</summary>
        public int ArrayCount { get { return !IsComment ? (int)_count.Value : 1; } }

        /// <summary>The value typed (in the data type and display format chosen), or null when the box was left empty.</summary>
        public string RowValueText { get { return _value.Enabled && _value.Text.Trim().Length > 0 ? _value.Text.Trim() : null; } }

        /// <summary>The typed value as a whole number, or null (kept for callers that write one word).</summary>
        public int? RowValue
        {
            get
            {
                int v;
                return _value.Enabled && int.TryParse(_value.Text.Trim(), NumberStyles.Integer,
                                                      CultureInfo.InvariantCulture, out v) ? (int?)v : null;
            }
        }

        /// <summary>How far apart consecutive values of a type are: 2 for a double word or a float, a string's words.</summary>
        public static int StepFor(string dataType, int stringLength = 16, int station = 0)
        {
            if (SimulationMonitorForm.IsBit(dataType)) return 1;
            return SimulationMonitorForm.WordCount(dataType, stringLength, station);
        }

        /// <summary>The address of element i of an array starting at first.</summary>
        public static string ElementAddress(string first, string dataType, int i, int stringLength = 16, int station = 0)
        {
            return PlcAddressHelper.OffsetAddress((first ?? "").Trim().ToUpperInvariant(), i * StepFor(dataType, stringLength, station));
        }

        /// <param name="focusField">"Label", "DataType", "Address" or "Value": the field to start in.</param>
        /// <param name="isNew">A new row: offers the array count and the comment-line choice.</param>
        /// <param name="displayFormat">Decimal, Hexadecimal or Binary.</param>
        /// <param name="arrayCount">Elements of an array (1: a single device).</param>
        /// <param name="stringLength">A String's characters.</param>
        /// <param name="station">The tab's station: its machine's structured data types are offered.</param>
        /// <param name="divisor">A number's calculated value: the value divided by this.</param>
        /// <param name="decimals">The calculated value's decimal places.</param>
        public PlcRowEditorForm(string caption, string label, string dataType, string address,
                                string value, string focusField = null, bool isNew = false, bool isComment = false,
                                string displayFormat = "", int arrayCount = 1, int stringLength = 16, int station = 0,
                                double divisor = 1, int decimals = 0)
        {
            Kaban.UI.Controls.UiScale.ApplyOnLoad(this);       // at the magnifying factor
            _isNew = isNew;
            _station = station;
            Text = LanguageManager.GetString(isNew ? "Ttl_AddDevice" : "Ttl_EditDevice", isNew ? "Add device" : "Edit device")
                   + " - " + caption;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = UiStyle.Body;
            ClientSize = new Size(440, 10);

            int y = 16;
            _comment.Text = LanguageManager.GetString("Lbl_CommentLine", "Comment line (no address, no value)");
            _comment.AutoSize = true;
            _comment.Location = new Point(118, y);
            _comment.Checked = isComment;
            Controls.Add(_comment);
            y += 32;

            _labelCaption.Text = LanguageManager.GetString("Col_Label", "Label") + ":";
            AddRow(_labelCaption, _label, ref y);

            _dataType.DropDownStyle = ComboBoxStyle.DropDown;   // free text: a machine may use its own wording
            FillDataTypes();
            AddRow(LanguageManager.GetString("Col_DataType", "DataType"), _dataType, ref y);
            // the structured data types: defined here, chosen like a simple type
            _dataType.Width -= 112;
            _structures.Text = LanguageManager.GetString("Btn_Structures", "Structures...");
            _structures.SetBounds(_dataType.Right + 6, _dataType.Top - 1, 106, 26);
            UiStyle.Round(_structures);
            Controls.Add(_structures);
            _structures.Click += (s, e) => EditStructures();
            _format.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var f in SimulationMonitorForm.DisplayFormats)
                _format.Items.Add(f);
            _format.SelectedItem = string.IsNullOrEmpty(displayFormat) ? SimulationMonitorForm.FormatDecimal : displayFormat;
            if (_format.SelectedIndex < 0) _format.SelectedIndex = 0;
            AddRow(LanguageManager.GetString("Col_DisplayFormat", "Display Format"), _format, ref y);
            _format.Width = 160;
            // THE CALCULATED VALUE: the value divided by the divisor, with the decimal places
            _divisor.Text = (divisor != 0 ? divisor : 1).ToString("0.#########", CultureInfo.InvariantCulture);
            AddRow(LanguageManager.GetString("Col_Divisor", "Divisor"), _divisor, ref y);
            _divisor.Width = 90;
            _decimals.Minimum = 0; _decimals.Maximum = StructuredTypes.MaxDecimals;
            _decimals.Value = Math.Max(0, Math.Min(StructuredTypes.MaxDecimals, decimals));
            var decimalsCaption = new Label { Text = LanguageManager.GetString("Col_Decimals", "Decimal Places") + ":", AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
            decimalsCaption.SetBounds(_divisor.Right + 8, _divisor.Top + 2, 130, 22);
            _decimals.SetBounds(decimalsCaption.Right + 6, _divisor.Top, 70, 24);
            Controls.Add(decimalsCaption);
            Controls.Add(_decimals);
            _chars.Minimum = 1; _chars.Maximum = 256; _chars.Value = Math.Max(1, Math.Min(256, stringLength));
            AddRow(LanguageManager.GetString("Lbl_StringChars", "String (characters)"), _chars, ref y);
            _chars.Width = 90;
            AddRow(LanguageManager.GetString("Col_Address", "Address"), _address, ref y);

            // AN ARRAY is one item of the list, expanded to its elements under it;
            // its count can be changed later too.
            _count.Minimum = 1; _count.Maximum = 1000; _count.Value = Math.Max(1, Math.Min(1000, arrayCount));
            AddRow(LanguageManager.GetString("Lbl_ArrayCount", "Array (how many)"), _count, ref y);
            _count.Width = 90;
            _preview.AutoSize = false;
            _preview.ForeColor = Color.FromArgb(90, 96, 106);
            _preview.SetBounds(214, y - 30, ClientSize.Width - 230, 22);
            Controls.Add(_preview);
            AddRow(LanguageManager.GetString("Col_ValuePlc", "Value (PLC)"), _value, ref y);

            _label.Text = label ?? "";
            _dataType.Text = string.IsNullOrWhiteSpace(dataType) ? "Bit" : dataType;
            _address.Text = address ?? "";
            _value.Text = value ?? "";

            // Only a NEW row can become a comment: turning a watched device into a
            // heading would silently drop it from the list.
            _comment.Visible = isNew || isComment;
            _comment.Enabled = isNew;

            var ok = UiStyle.Round(new Button { Text = LanguageManager.GetString("Btn_Ok", "OK"), DialogResult = DialogResult.OK, Width = 96, Height = 30 });
            var cancel = UiStyle.Round(new Button { Text = LanguageManager.GetString("Btn_Cancel", "Cancel"), DialogResult = DialogResult.Cancel, Width = 96, Height = 30 });
            ClientSize = new Size(ClientSize.Width, y + 52);
            ok.Location = new Point(ClientSize.Width - 212, y + 8);
            cancel.Location = new Point(ClientSize.Width - 108, y + 8);
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            // A NEW ROW FROM THE LABEL LIST (22 October): a label's name or its address typed - the
            // rest filled in from PlcLabels.csv (its data type, an array's length), what was not typed by hand
            if (isNew)
            {
                _address.TextChanged += (s, e) => { if (_auto) return; _addressTyped = true; FillFromLabelList(PlcLabelTable.ByAddress(_station, RowAddress), true); };
                _label.TextChanged += (s, e) => { if (_auto) return; _labelTyped = true; FillFromLabelList(PlcLabelTable.ByLabel(_station, RowLabel), false); };
                _dataType.TextChanged += (s, e) => { if (!_auto) _typeTyped = true; };
            }
            _comment.CheckedChanged += (s, e) => UpdateState();
            _count.ValueChanged += (s, e) => UpdateState();
            _chars.ValueChanged += (s, e) => UpdateState();
            _dataType.TextChanged += (s, e) => UpdateState();
            _address.TextChanged += (s, e) => UpdateState();
            UpdateState();

            // Start in the field of the cell that was double-clicked, its text
            // selected so typing replaces it.
            Shown += (s, e) =>
            {
                Control start = _address;
                switch (focusField ?? "")
                {
                    case "Label": start = _label; break;
                    case "DataType": start = _dataType; break;
                    case "Address": start = _address; break;
                    case "Value": start = _value.Enabled ? (Control)_value : _address; break;
                    default: start = IsComment ? (Control)_label : string.IsNullOrEmpty(_address.Text) ? _address : _label; break;
                }
                start.Focus();
                var tb = start as TextBox;
                if (tb != null) tb.SelectAll();
                var cb = start as ComboBox;
                if (cb != null) cb.SelectAll();
            };

            // An address is the one field that cannot be guessed, so refuse an empty
            // one rather than silently creating a nameless device - unless the row is
            // a comment, which is nothing BUT its text.
            FormClosing += (s, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                string problem = null;
                double d;
                if (IsComment && _label.Text.Trim().Length == 0)
                    problem = LanguageManager.GetString("Msg_CommentRequired", "Type the comment's text.");
                else if (!IsComment && _address.Text.Trim().Length == 0)
                    problem = LanguageManager.GetString("Msg_AddressRequired", "An address is required.");
                else if (_divisor.Enabled && !TryDivisor(_divisor.Text, out d))
                    problem = LanguageManager.GetString("Msg_MonBadDivisorShort", "The divisor is a number other than 0.");
                if (problem == null) return;
                MessageBox.Show(this, problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
            };
        }

        private void UpdateState()
        {
            bool comment = _comment.Checked;
            _dataType.Enabled = _address.Enabled = !comment;
            _structures.Enabled = !comment;
            _count.Enabled = !comment;
            string type = RowDataType;
            var structure = StructuredTypes.Find(type, _station);
            // A display format for the whole numbers (a structure's members); a length for a string.
            _format.Enabled = !comment && (structure != null ||
                              !SimulationMonitorForm.IsBit(type) && !SimulationMonitorForm.IsFloat(type) && !SimulationMonitorForm.IsString(type));
            _chars.Enabled = !comment && structure == null && SimulationMonitorForm.IsString(type);
            // a number's calculated value (a structure's members have their own: "Structures...")
            _divisor.Enabled = _decimals.Enabled = !comment && structure == null &&
                               !SimulationMonitorForm.IsBit(type) && !SimulationMonitorForm.IsString(type);
            _labelCaption.Text = (comment ? LanguageManager.GetString("Col_Comment", "Comment")
                                          : LanguageManager.GetString("Col_Label", "Label")) + ":";
            // A value typed for a run of devices would be written to all of them -
            // the kind of side effect that is easy to cause without meaning to.
            // A structure's values are its members', each on its own row.
            _value.Enabled = !comment && ArrayCount == 1 && structure == null;

            int n = ArrayCount;
            string first = RowAddress;
            int step = StepFor(RowDataType, RowStringLength, _station);
            if (!comment && structure != null && n <= 1 && first.Length > 0)
            {
                // one structure: the devices it takes
                _preview.Text = first + " … " + PlcAddressHelper.OffsetAddress(first, Math.Max(1, structure.Size) - 1) + "  (" +
                                string.Format(LanguageManager.GetString("Lbl_StructPreview", "{0} members, {1} devices"), structure.Members.Count, structure.Size) + ")";
                return;
            }
            _preview.Text = comment || n <= 1 || first.Length == 0 ? ""
                : first + " … " + ElementAddress(first, RowDataType, n - 1, RowStringLength, _station) +
                  (step == 2 ? "  (" + LanguageManager.GetString("Lbl_Step2", "every 2nd device") + ")"
                   : step > 2 ? "  (" + string.Format(LanguageManager.GetString("Lbl_StepN", "every {0}th device"), step) + ")" : "");
        }

        /// <summary>
        /// A label of the label list met (its address or its name typed): the other of the two, its data
        /// type and an array's length ("Cutting Step(0..29)": 30) filled in - not what the user typed himself.
        /// </summary>
        private void FillFromLabelList(PlcLabelTable.Entry entry, bool fromAddress)
        {
            if (IsComment || (entry == null && !_filled)) return;
            _auto = true;
            try
            {
                if (entry == null)
                {
                    // "D8" filled PC_ProfilGenisligi in - "D80" names nothing: what was filled (and not changed since) goes back
                    if (fromAddress && _label.Text == _filledLabel) _label.Text = _keptLabel;
                    if (!fromAddress && _address.Text == _filledAddress) _address.Text = _keptAddress;
                    if (_dataType.Text == _filledType) _dataType.Text = _keptType;
                    if (_count.Value == _filledCount) _count.Value = _keptCount;
                    _filled = false;
                }
                else
                {
                    if (!_filled)
                    {
                        _keptLabel = _label.Text; _keptAddress = _address.Text; _keptType = _dataType.Text; _keptCount = _count.Value;
                    }
                    if (fromAddress && (!_labelTyped || _label.Text.Trim().Length == 0)) _label.Text = entry.Label ?? "";
                    if (!fromAddress && (!_addressTyped || _address.Text.Trim().Length == 0)) _address.Text = (entry.Address ?? "").Trim().ToUpperInvariant();
                    if (!_typeTyped && !string.IsNullOrWhiteSpace(entry.DataType))
                    {
                        _dataType.Text = StructuredTypes.BaseName(entry.DataType);
                        _count.Value = Math.Max(_count.Minimum, Math.Min(_count.Maximum, Math.Max(1, StructuredTypes.CountIn(entry.DataType))));
                    }
                    _filledLabel = _label.Text; _filledAddress = _address.Text; _filledType = _dataType.Text; _filledCount = _count.Value;
                    _filled = true;
                }
            }
            finally { _auto = false; }
            UpdateState();
        }

        /// <summary>The simple types, then the structured ones saved.</summary>
        private void FillDataTypes()
        {
            string keep = _dataType.Text;
            _dataType.Items.Clear();
            _dataType.Items.AddRange(KnownDataTypes);
            foreach (var name in StructuredTypes.Names(_station)) _dataType.Items.Add(name);
            _dataType.Text = keep;
        }

        /// <summary>The structured data types' editor, on the type chosen; the list offers them as they are after it.</summary>
        private void EditStructures()
        {
            using (var dlg = new StructuredTypesForm(RowDataType, _station))
                dlg.ShowDialog(this);
            FillDataTypes();
            UpdateState();
        }

        private void AddRow(string caption, Control field, ref int y)
        {
            AddRow(new Label { Text = caption + ":" }, field, ref y);
        }

        private void AddRow(Label lbl, Control field, ref int y)
        {
            lbl.AutoSize = false;
            lbl.SetBounds(16, y + 4, 100, 22);
            field.SetBounds(118, y, ClientSize.Width - 134, 24);
            Controls.Add(lbl);
            Controls.Add(field);
            y += 34;
        }
    }
}
