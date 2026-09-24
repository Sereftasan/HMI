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
    /// A NEW ROW can be an ARRAY: "M100, 10 of them" adds M100..M109, all with the
    /// same data type; "D200 as double words, 4 of them" adds D200, D202, D204,
    /// D206 - a double word takes two devices, so the next one starts two further
    /// on. Addresses count in the device's own number system (X and Y octal, W
    /// and B hexadecimal), as everywhere else in the program.
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
        private readonly NumericUpDown _count = new NumericUpDown();
        private readonly CheckBox _comment = new CheckBox();
        private readonly Label _preview = new Label();
        private readonly Label _labelCaption = new Label();
        private readonly bool _isNew;

        /// <summary>The device types the engineer's list uses (see PlcLabels.csv).</summary>
        public static readonly string[] KnownDataTypes =
        {
            "Bit", "Word [Signed]", "Double Word [Signed]"
        };

        public string RowLabel { get { return _label.Text.Trim(); } }
        public string RowDataType { get { return _dataType.Text.Trim(); } }
        public string RowAddress { get { return _address.Text.Trim().ToUpperInvariant(); } }

        /// <summary>True when the row is a comment line (no address, no value).</summary>
        public bool IsComment { get { return _comment.Checked; } }

        /// <summary>How many consecutive devices to add (1 for an edit).</summary>
        public int ArrayCount { get { return _isNew && !IsComment ? (int)_count.Value : 1; } }

        /// <summary>The typed value, or null when the box was left empty.</summary>
        public int? RowValue
        {
            get
            {
                int v;
                return _value.Enabled && int.TryParse(_value.Text.Trim(), NumberStyles.Integer,
                                                      CultureInfo.InvariantCulture, out v) ? (int?)v : null;
            }
        }

        /// <summary>How far apart consecutive devices of a type are: 2 for a double word.</summary>
        public static int StepFor(string dataType)
        {
            return Kaban.UI.Forms.SimulationMonitorForm.IsDoubleWord(dataType) ? 2 : 1;
        }

        /// <summary>The address of element i of an array starting at first.</summary>
        public static string ElementAddress(string first, string dataType, int i)
        {
            return PlcAddressHelper.OffsetAddress((first ?? "").Trim().ToUpperInvariant(), i * StepFor(dataType));
        }

        /// <param name="focusField">"Label", "DataType", "Address" or "Value": the field to start in.</param>
        /// <param name="isNew">A new row: offers the array count and the comment-line choice.</param>
        public PlcRowEditorForm(string caption, string label, string dataType, string address,
                                string value, string focusField = null, bool isNew = false, bool isComment = false)
        {
            _isNew = isNew;
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
            _dataType.Items.AddRange(KnownDataTypes);
            AddRow(LanguageManager.GetString("Col_DataType", "DataType"), _dataType, ref y);
            AddRow(LanguageManager.GetString("Col_Address", "Address"), _address, ref y);

            if (isNew)
            {
                _count.Minimum = 1; _count.Maximum = 1000; _count.Value = 1;
                AddRow(LanguageManager.GetString("Lbl_ArrayCount", "Array (how many)"), _count, ref y);
                _count.Width = 90;
                _preview.AutoSize = false;
                _preview.ForeColor = Color.FromArgb(90, 96, 106);
                _preview.SetBounds(214, y - 30, ClientSize.Width - 230, 22);
                Controls.Add(_preview);
            }
            AddRow(LanguageManager.GetString("Col_Value", "Value"), _value, ref y);

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

            _comment.CheckedChanged += (s, e) => UpdateState();
            _count.ValueChanged += (s, e) => UpdateState();
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
                if (IsComment && _label.Text.Trim().Length == 0)
                    problem = LanguageManager.GetString("Msg_CommentRequired", "Type the comment's text.");
                else if (!IsComment && _address.Text.Trim().Length == 0)
                    problem = LanguageManager.GetString("Msg_AddressRequired", "An address is required.");
                if (problem == null) return;
                MessageBox.Show(this, problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
            };
        }

        private void UpdateState()
        {
            bool comment = _comment.Checked;
            _dataType.Enabled = _address.Enabled = !comment;
            _count.Enabled = !comment;
            _labelCaption.Text = (comment ? LanguageManager.GetString("Col_Comment", "Comment")
                                          : LanguageManager.GetString("Col_Label", "Label")) + ":";
            // A value typed for a run of devices would be written to all of them -
            // the kind of side effect that is easy to cause without meaning to.
            _value.Enabled = !comment && ArrayCount == 1;

            if (!_isNew) return;
            int n = ArrayCount;
            string first = RowAddress;
            _preview.Text = comment || n <= 1 || first.Length == 0 ? ""
                : first + " … " + ElementAddress(first, RowDataType, n - 1) +
                  (StepFor(RowDataType) == 2 ? "  (" + LanguageManager.GetString("Lbl_Step2", "every 2nd device") + ")" : "");
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
