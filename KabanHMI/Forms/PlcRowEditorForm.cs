using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// Edits ONE monitor row - Label, DataType, Address, Value - in a dialog.
    ///
    /// The grid on the monitor is editable in place, which is quicker with a
    /// keyboard. This exists for the panel PC: a 36px-high grid cell is a poor
    /// touch target, and typing an address into it with the on-screen keyboard
    /// while a 250ms refresh runs is unpleasant. Double-clicking a row opens this
    /// instead, with real input boxes and no refresh underneath.
    ///
    /// It edits a COPY. Nothing is written to the PLC image until OK, so an
    /// abandoned edit cannot half-apply - which matters when the address field is
    /// one of the things being changed.
    /// </summary>
    public class PlcRowEditorForm : Form
    {
        private readonly TextBox _label = new TextBox();
        private readonly ComboBox _dataType = new ComboBox();
        private readonly TextBox _address = new TextBox();
        private readonly TextBox _value = new TextBox();

        /// <summary>The device types the engineer's list uses (see PlcLabels.csv).</summary>
        private static readonly string[] KnownDataTypes =
        {
            "Bit", "Word [Signed]", "Double Word [Signed]"
        };

        public string RowLabel { get { return _label.Text.Trim(); } }
        public string RowDataType { get { return _dataType.Text.Trim(); } }
        public string RowAddress { get { return _address.Text.Trim().ToUpperInvariant(); } }

        /// <summary>The typed value, or null when the box was left empty.</summary>
        public int? RowValue
        {
            get
            {
                int v;
                return int.TryParse(_value.Text.Trim(), NumberStyles.Integer,
                                    CultureInfo.InvariantCulture, out v) ? (int?)v : null;
            }
        }

        public PlcRowEditorForm(string caption, string label, string dataType, string address,
                                string value)
        {
            Text = LanguageManager.GetString("Ttl_EditDevice", "Edit device") + " - " + caption;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.75F);
            ClientSize = new Size(430, 232);

            int y = 16;
            AddRow(LanguageManager.GetString("Col_Label", "Label"), _label, ref y);

            _dataType.DropDownStyle = ComboBoxStyle.DropDown;   // free text: a machine may use its own wording
            _dataType.Items.AddRange(KnownDataTypes);
            AddRow(LanguageManager.GetString("Col_DataType", "DataType"), _dataType, ref y);

            AddRow(LanguageManager.GetString("Col_Address", "Address"), _address, ref y);
            AddRow(LanguageManager.GetString("Col_Value", "Value"), _value, ref y);

            _label.Text = label ?? "";
            _dataType.Text = string.IsNullOrWhiteSpace(dataType) ? "Bit" : dataType;
            _address.Text = address ?? "";
            _value.Text = value ?? "";

            var ok = UiStyle.Round(new Button
            {
                Text = LanguageManager.GetString("Btn_Ok", "OK"),
                DialogResult = DialogResult.OK,
                Width = 96,
                Height = 30
            });
            var cancel = UiStyle.Round(new Button
            {
                Text = LanguageManager.GetString("Btn_Cancel", "Cancel"),
                DialogResult = DialogResult.Cancel,
                Width = 96,
                Height = 30
            });
            ok.Location = new Point(ClientSize.Width - 212, y + 8);
            cancel.Location = new Point(ClientSize.Width - 108, y + 8);
            Controls.Add(ok);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;

            // An address is the one field that cannot be guessed, so refuse an empty
            // one rather than silently creating a nameless device in the PLC image.
            FormClosing += (s, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                if (_address.Text.Trim().Length > 0) return;
                MessageBox.Show(this,
                    LanguageManager.GetString("Msg_AddressRequired", "An address is required."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
            };
        }

        private void AddRow(string caption, Control field, ref int y)
        {
            var lbl = new Label { Text = caption + ":", AutoSize = false };
            lbl.SetBounds(16, y + 4, 96, 22);
            field.SetBounds(118, y, ClientSize.Width - 134, 24);
            Controls.Add(lbl);
            Controls.Add(field);
            y += 34;
        }
    }
}

