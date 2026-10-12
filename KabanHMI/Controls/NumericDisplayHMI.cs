using System.Drawing;
using System.Windows.Forms;

namespace Kaban.UI.Controls
{
    [ToolboxBitmap(typeof(Label))]
    public class NumericDisplayHMI : BaseNumericHMI
    {
        public NumericDisplayHMI()
        {
            // No default caption: every control carries a DescriptionText, so a
            // second built-in label would only have to be deleted on every drop.
            this.Text = "";
            this.BackColor = Color.Gainsboro; // Read-Only Color
        }

        // No need to override UpdateState! 
        // It inherits the perfect logic (Visibility + Read Formula) from BaseNumericHMI.
    }
}
