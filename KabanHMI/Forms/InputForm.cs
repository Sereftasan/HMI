using System;
using System.Drawing;
using System.Windows.Forms;

namespace Kaban.UI.Forms
{
    public class InputForm : Form
    {
        private TextBox txtInput;
        private Button btnOk;
        private Button btnCancel;
        private Label lblPrompt;

        // Public Properties for Configuration
        public double MinValue { get; set; } = double.MinValue;
        public double MaxValue { get; set; } = double.MaxValue;
        public bool AllowNegative { get; set; } = true;

        // Property to get/set the text easily from outside
        public string InputText
        {
            get { return txtInput.Text; }
            set { txtInput.Text = value; }
        }

        // Property to set the Prompt message dynamically
        public string Message
        {
            get { return lblPrompt.Text; }
            set { lblPrompt.Text = value; }
        }

        // 1. Parameterless Constructor (Required for the 'using' block in your main code)
        public InputForm() : this("Input", "Enter Value:", "")
        {
        }

        // Existing Constructor
        public InputForm(string title, string prompt, string defaultValue)
        {
            this.Text = title;
            this.Size = new Size(300, 180); // Slightly taller for error messages
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            lblPrompt = new Label();
            lblPrompt.Text = prompt;
            lblPrompt.Location = new Point(15, 15);
            lblPrompt.AutoSize = true;
            this.Controls.Add(lblPrompt);

            txtInput = new TextBox();
            txtInput.Text = defaultValue;
            txtInput.Location = new Point(15, 40);
            txtInput.Size = new Size(250, 25);
            // Hook up the KeyPress event to block negative signs if needed
            txtInput.KeyPress += TxtInput_KeyPress;
            this.Controls.Add(txtInput);

            btnOk = new Button();
            btnOk.Text = "OK";
            // IMPORTANT: Remove DialogResult here. We set it manually after validation.
            // btnOk.DialogResult = DialogResult.OK; 
            btnOk.Location = new Point(105, 90);
            btnOk.Click += BtnOk_Click; // Hook up Click event
            this.Controls.Add(btnOk);
            this.AcceptButton = btnOk;

            btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.DialogResult = DialogResult.Cancel; // Cancel is always safe
            btnCancel.Location = new Point(190, 90);
            this.Controls.Add(btnCancel);
            this.CancelButton = btnCancel;
        }

        // 2. Validation Logic (Runs when OK is clicked)
        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtInput.Text))
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return;
            }

            // Try to parse the value. NO THOUSANDS SEPARATORS: the plain overload
            // allows them, so on a panel whose group separator is "." (Turkish,
            // German...) "12.5" passed this check as 125 - while the caller, which
            // parses without them, then threw the entry away without a word.
            if (double.TryParse(txtInput.Text, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.CurrentCulture, out double value))
            {
                // CHECK LIMITS
                if (value < MinValue || value > MaxValue)
                {
                    MessageBox.Show($"Value must be between {MinValue} and {MaxValue}.",
                                    "Out of Range",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);

                    // Select all text so user can easily re-type
                    txtInput.SelectAll();
                    txtInput.Focus();
                    return; // Do NOT close the form
                }

                // If valid, close with OK
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please enter a valid number.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 3. Prevent typing negative sign if not allowed
        private void TxtInput_KeyPress(object sender, KeyPressEventArgs e)
        {
            // If AllowNegative is false and the user presses '-', block it.
            if (!AllowNegative && e.KeyChar == '-')
            {
                e.Handled = true; // Stop the character from entering the TextBox
                System.Media.SystemSounds.Beep.Play(); // Optional audio feedback
            }
        }

        // Static Helper (Updates to use new logic if needed strictly for string input)
        public static string Show(string title, string prompt, string defaultValue)
        {
            using (var form = new InputForm(title, prompt, defaultValue))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    return form.txtInput.Text;
                }
                return null;
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // InputForm
            // 
            this.ClientSize = new System.Drawing.Size(282, 253);
            this.Name = "InputForm";
            this.Load += new System.EventHandler(this.InputForm_Load);
            this.ResumeLayout(false);

        }

        private void InputForm_Load(object sender, EventArgs e)
        {

        }
    }
}
