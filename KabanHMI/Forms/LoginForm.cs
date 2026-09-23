using System;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// Real login dialog. Replaces the previous hardcoded
    /// SecurityManager.Login("Dev", SecurityLevel.Operator) call that let
    /// anyone use the HMI as an Operator with no credentials at all.
    /// </summary>
    public class LoginForm : Form
    {
        private TextBox txtUsername;
        private TextBox txtPassword;
        private Label lblError;
        private Button btnOk;
        private Button btnCancel;

        public LoginForm()
        {
            this.Text = "HMI Login";
            this.Size = new Size(360, 260);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            var lblUser = Kaban.UI.Controls.UiStyle.CaptionLabel(
                LanguageManager.GetString("Lbl_Username", "Username"), 20, 20, 100);
            lblUser.Font = new Font("Segoe UI", 10);
            txtUsername = new TextBox { Left = 20, Top = 45, Width = 300, Font = new Font("Segoe UI", 12) };

            var lblPass = Kaban.UI.Controls.UiStyle.CaptionLabel(
                LanguageManager.GetString("Lbl_Password", "Password"), 20, 80, 100);
            lblPass.Font = new Font("Segoe UI", 10);
            txtPassword = new TextBox { Left = 20, Top = 105, Width = 300, Font = new Font("Segoe UI", 12), UseSystemPasswordChar = true };

            lblError = new Label
            {
                Left = 20,
                Top = 140,
                Width = 300,
                Height = 40,
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9),
                Text = ""
            };

            btnOk = new Button { Text = "Login", Left = 130, Top = 185, Width = 90, Height = 35, DialogResult = DialogResult.None };
            btnCancel = new Button { Text = "Cancel", Left = 230, Top = 185, Width = 90, Height = 35, DialogResult = DialogResult.Cancel };

            btnOk.Click += BtnOk_Click;
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnOk_Click(s, e); };

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            this.Controls.Add(lblUser);
            this.Controls.Add(txtUsername);
            this.Controls.Add(lblPass);
            this.Controls.Add(txtPassword);
            this.Controls.Add(lblError);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.Shown += (s, e) => txtUsername.Focus();
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            string error;
            if (SecurityManager.TryLogin(txtUsername.Text.Trim(), txtPassword.Text, out error))
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                lblError.Text = error;
                txtPassword.SelectAll();
                txtPassword.Focus();
            }
        }
    }
}

