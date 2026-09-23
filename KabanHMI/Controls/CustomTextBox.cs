using System;
using System.Windows.Forms;
using System.Drawing;

namespace Kaban.UI.Controls
{
    public class CustomTextBox : TextBox
    {
        // ---- optional attached description (label hugging the LEFT edge) ----
        private AttachedDescription _descr;
        private AttachedDescription Descr { get { return _descr ?? (_descr = new AttachedDescription(this)); } }

        [System.ComponentModel.Category("   Appearance")]
        [System.ComponentModel.Description("Optional description attached to the control's left edge. Enter a TRANSLATION KEY (rendered in the active language) or plain text (shown as-is when the key is not in the translation file). Empty = no label.")]
        public string DescriptionText { get { return _descr?.Text ?? ""; } set { Descr.Text = value; } }

        public CustomTextBox()
        {
            // Optional styling
            this.BorderStyle = BorderStyle.FixedSingle;
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            // Add custom logic here if needed (e.g., clear watermark)
        }
    }
}
