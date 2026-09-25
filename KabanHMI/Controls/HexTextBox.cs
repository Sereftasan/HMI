using System;
using System.Windows.Forms;
using System.ComponentModel;

namespace Kaban.UI.Controls
{
    public enum IOTypeDef
    {
        INPUT,
        OUTPUT
    }

    public class HexTextBox : TextBox
    {
        private string _prefix = "X";
        private IOTypeDef _inputOutput = IOTypeDef.INPUT;

        [Category(" PLC Settings")]
        public IOTypeDef InputOutput
        {
            get { return _inputOutput; }
            set
            {
                if (value != _inputOutput)
                {
                    _inputOutput = value;
                    _prefix = (_inputOutput == IOTypeDef.INPUT) ? "X" : "Y";

                    // Auto-update text if it contains the old prefix
                    if (!string.IsNullOrEmpty(Text))
                    {
                        Text = _prefix + Text.Substring(1);
                    }
                }
            }
        }

        public int Value
        {
            get
            {
                string s = Text.Replace(_prefix, "").Trim();
                if (string.IsNullOrEmpty(s)) return 0;
                try
                {
                    return Convert.ToInt32(s, 16) + 1; // +1 based on original logic
                }
                catch { return 0; }
            }
            set
            {
                int newValue = value - 1;
                if (value > 0)
                    Text = _prefix + Convert.ToString(newValue, 16).ToUpper();
                else
                    Text = _prefix;
            }
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            if (string.IsNullOrEmpty(this.Text))
            {
                this.Text = _prefix;
            }
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            // Position cursor after prefix
            if (Text.Length >= 1) this.SelectionStart = 1;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            // Allow Backspace (8)
            if (e.KeyChar == 8) return;

            char c = char.ToUpper(e.KeyChar);

            // Allow Hex chars (0-9, A-F)
            bool isHex = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');

            if (!isHex)
            {
                e.Handled = true;
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);

            // Force Prefix
            if (!this.Text.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))
            {
                // If user deleted prefix, put it back
                string clean = this.Text.Replace("X", "").Replace("Y", "").Replace("x", "").Replace("y", "");
                this.Text = _prefix + clean;
                this.SelectionStart = this.Text.Length;
            }

            this.Text = this.Text.ToUpper();
        }
    }
}
