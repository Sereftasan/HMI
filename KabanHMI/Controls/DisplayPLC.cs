using System;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;

namespace Kaban.UI.Controls
{
    public enum DisplayType
    {
        Numeric,
        Time,
        Text
    }

    public class DisplayPLC : TextBox
    {
        private DisplayType _presentation = DisplayType.Numeric;
        private string _address;
        private int _currentValue = 0;

        [Category("PLC Settings")]
        public DisplayType Presentation
        {
            get { return _presentation; }
            set { _presentation = value; }
        }

        [Category("PLC Settings")]
        public string Address
        {
            get { return _address; }
            set { _address = value; }
        }

        [Category("PLC Data")]
        public int CurrentValue
        {
            get { return _currentValue; }
            set
            {
                if (_currentValue != value)
                {
                    _currentValue = value;
                    UpdateTextSafe();
                }
            }
        }

        public DisplayPLC()
        {
            this.BackColor = Color.White;
            this.Font = new Font("Arial", 9.75F, FontStyle.Bold);
            this.ReadOnly = true;
            this.Text = "0";
            this.TextAlign = HorizontalAlignment.Right;
        }

        private void UpdateTextSafe()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new MethodInvoker(SetText));
            }
            else
            {
                SetText();
            }
        }

        private void SetText()
        {
            if (Presentation == DisplayType.Numeric)
            {
                this.Text = _currentValue.ToString();
            }
            else if (Presentation == DisplayType.Time)
            {
                // Assuming value is in tenths of a second or similar
                int totalSeconds = _currentValue / 10;
                TimeSpan span = TimeSpan.FromSeconds(totalSeconds);
                this.Text = string.Format("{0:D2}:{1:D2}:{2:D2}", span.Hours, span.Minutes, span.Seconds);
            }
            else
            {
                this.Text = _currentValue.ToString();
            }
        }
    }
}
