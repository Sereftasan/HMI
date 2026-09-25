using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// Real login dialog. Replaces the previous hardcoded
    /// SecurityManager.Login("Dev", SecurityLevel.Operator) call that let
    /// anyone use the HMI as an Operator with no credentials at all.
    ///
    /// THE USER IS PICKED, NOT TYPED
    /// -----------------------------
    /// The name is chosen from the machine's accounts, the one who signed in last
    /// already selected, and the dialog opens in the password box - on a panel PC
    /// that is one field typed on the on-screen keyboard instead of two.
    ///
    /// Why that is acceptable here when a website would not do it: listing the
    /// names gives away half of what a web login asks for, which matters to a
    /// login anyone on the internet can try. On a machine the names are known on
    /// the shop floor anyway, the person has to stand at the panel, the password
    /// is still required, and five wrong passwords lock the account for a minute
    /// (SecurityManager). Windows' own sign-in screen lists its users the same way.
    /// What it does make plain is an account still on its factory password - the
    /// seeded operator / maintenance / admin with 1234 - so those must be changed.
    ///
    /// "Change user" opens the dialog with the list dropped down: the one thing
    /// to do there is to pick somebody else.
    /// </summary>
    public class LoginForm : Form
    {
        private readonly ComboBox cmbUser;
        private readonly TextBox txtPassword;
        private readonly Label lblError;
        private readonly Button btnOk;
        private readonly Button btnCancel;

        public LoginForm() : this(false) { }

        /// <param name="chooseUser">Open with the user list dropped down (Change user).</param>
        public LoginForm(bool chooseUser)
        {
            Kaban.UI.Controls.UiScale.ApplyOnLoad(this);       // at the magnifying factor
            this.Text = LanguageManager.GetString("Ttl_Login", "HMI Login");
            this.Size = new Size(360, 260);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            var lblUser = Kaban.UI.Controls.UiStyle.CaptionLabel(
                LanguageManager.GetString("Lbl_Username", "Username"), 20, 20, 100);
            lblUser.Font = new Font("Segoe UI", 10);
            cmbUser = new ComboBox
            {
                Left = 20, Top = 45, Width = 300,
                Font = new Font("Segoe UI", 12),
                DropDownStyle = ComboBoxStyle.DropDownList,   // a name that is not an account cannot log in anyway
                MaxDropDownItems = 12
            };
            foreach (var name in UserStore.GetAllUsers().Select(u => u.Username)
                                          .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
                cmbUser.Items.Add(name);

            string last = UserStore.LastUser;
            int at = string.IsNullOrEmpty(last) ? -1
                   : cmbUser.Items.Cast<string>().ToList().FindIndex(n => string.Equals(n, last, StringComparison.OrdinalIgnoreCase));
            if (at >= 0) cmbUser.SelectedIndex = at;
            else if (cmbUser.Items.Count == 1) cmbUser.SelectedIndex = 0;

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

            btnOk = new Button { Text = LanguageManager.GetString("Btn_Login", "Login"), Left = 130, Top = 185, Width = 90, Height = 35, DialogResult = DialogResult.None };
            btnCancel = new Button { Text = LanguageManager.GetString("Btn_Cancel", "Cancel"), Left = 230, Top = 185, Width = 90, Height = 35, DialogResult = DialogResult.Cancel };

            btnOk.Click += BtnOk_Click;
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnOk_Click(s, e); };
            // A name picked from the list: on to its password.
            cmbUser.SelectionChangeCommitted += (s, e) =>
            {
                lblError.Text = "";
                txtPassword.Clear();
                BeginInvoke((MethodInvoker)(() => txtPassword.Focus()));
            };

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            this.Controls.Add(lblUser);
            this.Controls.Add(cmbUser);
            this.Controls.Add(lblPass);
            this.Controls.Add(txtPassword);
            this.Controls.Add(lblError);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.Shown += (s, e) =>
            {
                if (chooseUser || cmbUser.SelectedIndex < 0)
                {
                    cmbUser.Focus();
                    cmbUser.DroppedDown = cmbUser.Items.Count > 0;
                }
                else txtPassword.Focus();
            };
        }

        /// <summary>The name selected in the list ("" when none).</summary>
        public string SelectedUser { get { return cmbUser.SelectedItem as string ?? ""; } }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (SelectedUser.Length == 0)
            {
                lblError.Text = LanguageManager.GetString("Msg_PickUser", "Choose a user.");
                cmbUser.Focus();
                cmbUser.DroppedDown = true;
                return;
            }
            string error;
            if (SecurityManager.TryLogin(SelectedUser, txtPassword.Text, out error))
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
