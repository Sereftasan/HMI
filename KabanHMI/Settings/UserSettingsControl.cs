using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// USER MANAGEMENT (admin page): list of accounts with add / update / delete
    /// and password reset. The last Admin account cannot be deleted (the machine
    /// must never lock everyone out) and accounts persist across restarts.
    ///
    /// Also the MAGNIFYING FACTOR (see DisplaySettings): the panel's, for everyone,
    /// and optionally a user's own - offered as a restart when they sign in.
    /// </summary>
    public class UserSettingsControl : UserControl
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _txtUser = new TextBox();
        private readonly TextBox _txtPass = new TextBox();
        private readonly ComboBox _cmbLevel = new ComboBox();
        private readonly ComboBox _cmbFactor = new ComboBox();
        private readonly ComboBox _cmbPanelFactor = new ComboBox();
        private readonly Label _lblStatus = new Label();

        private static readonly string[] FactorChoices = { "0.80", "0.90", "1.00", "1.10", "1.20", "1.25", "1.30", "1.40", "1.50" };

        public UserSettingsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _grid.Dock = DockStyle.Left;
            _grid.Width = 300;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add("user", LanguageManager.GetString("Usr_Name", "User"));
            _grid.Columns.Add("level", LanguageManager.GetString("Usr_Level", "Level"));
            _grid.Columns.Add("factor", LanguageManager.GetString("Usr_Factor", "Magnifier"));
            Kaban.UI.Controls.UiStyle.EnableAutoFit(_grid);
            Kaban.UI.Controls.UiStyle.EnableTableTools(_grid, "Users");
            _grid.SelectionChanged += (s, e) =>
            {
                if (_grid.SelectedRows.Count == 0) return;
                _txtUser.Text = Convert.ToString(_grid.SelectedRows[0].Cells[0].Value);
                var lvl = Convert.ToString(_grid.SelectedRows[0].Cells[1].Value);
                _cmbLevel.SelectedItem = lvl;
                _txtPass.Text = "";
                _cmbFactor.Text = Convert.ToString(_grid.SelectedRows[0].Cells[2].Value);
            };

            var pn = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), AutoScroll = true };
            int y = 16;
            Action<string, Control> row = (caption, ctl) =>
            {
                // RIGHT-ALIGNED, like every other captioned field in the program. Left
                // aligned, the colons ended up at three different distances from
                // the boxes they belong to.
                pn.Controls.Add(new Label
                {
                    Text = caption, Left = 16, Top = y + 4, Width = 130,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = new Font("Segoe UI", 9F)
                });
                ctl.Left = 150; ctl.Top = y; ctl.Width = 170;
                pn.Controls.Add(ctl);
                y += 34;
            };
            row(LanguageManager.GetString("Usr_Name", "User") + ":", _txtUser);
            _txtPass.UseSystemPasswordChar = true;
            row(LanguageManager.GetString("Usr_Pass", "Password") + ":", _txtPass);
            _cmbLevel.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbLevel.Items.AddRange(new object[] { "Operator", "Maintenance", "Admin" });
            _cmbLevel.SelectedIndex = 0;
            row(LanguageManager.GetString("Usr_Level", "Level") + ":", _cmbLevel);
            _cmbFactor.DropDownStyle = ComboBoxStyle.DropDown;
            _cmbFactor.Items.Add("");
            _cmbFactor.Items.AddRange(FactorChoices);
            row(LanguageManager.GetString("Usr_FactorOwn", "Magnifying factor") + ":", _cmbFactor);

            var tips = Kaban.UI.Controls.UiStyle.Tips();
            Func<string, string, int, Button> tool = (emoji, tip, left) =>
            {
                var b = new Button { Text = emoji, Left = left, Top = y + 4, Width = 44, Height = 32, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F) };
                b.FlatAppearance.BorderColor = Color.FromArgb(180, 186, 194);
                tips.SetToolTip(b, tip);
                pn.Controls.Add(b);
                return b;
            };
            var btnSave = tool("\uD83D\uDCBE", "Add the user, or update level/password of an existing one", 150);
            var btnDelete = tool("\uD83D\uDDD1", "Delete the selected user", 200);

            btnSave.Click += (s, e) => SaveUser();
            btnDelete.Click += (s, e) => DeleteUser();
            tips.SetToolTip(_cmbFactor, LanguageManager.GetString("Tip_UsrFactor",
                "The user's own magnifying factor, 0.8 to 1.5 (1.25 = a quarter larger). Empty: the panel's. " +
                "When they sign in and the screen is at another factor, the HMI offers to restart with theirs."));

            // ---- the panel's factor, for everyone without one of their own ----
            y += 50;
            pn.Controls.Add(new Label
            {
                Text = LanguageManager.GetString("Hdr_PanelFactor", "Magnifying factor of this panel"),
                Left = 16, Top = y, Width = 304, Height = 22,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            });
            y += 28;
            _cmbPanelFactor.DropDownStyle = ComboBoxStyle.DropDown;
            _cmbPanelFactor.Items.AddRange(FactorChoices);
            _cmbPanelFactor.Text = DisplaySettings.Format(DisplaySettings.PanelFactor);
            row(LanguageManager.GetString("Usr_FactorPanel", "For everyone") + ":", _cmbPanelFactor);
            var btnPanel = tool("\uD83D\uDCBE", LanguageManager.GetString("Tip_PanelFactorSave",
                "Keep this factor for everyone without their own - it takes effect when the HMI restarts"), 150);
            btnPanel.Click += (s, e) => SavePanelFactor();
            pn.Controls.Add(new Label
            {
                Text = LanguageManager.GetString("Msg_FactorNote",
                    "0.8 to 1.5. Text, buttons, fields and pages are drawn this much larger (or smaller). " +
                    "Applied when the HMI starts: pages larger than the screen scroll."),
                Left = 16, Top = y + 44, Width = 304, Height = 64, ForeColor = Color.FromArgb(90, 96, 106),
                Font = new Font("Segoe UI", 9F)
            });

            _lblStatus.Dock = DockStyle.Bottom;
            _lblStatus.Height = 26;
            _lblStatus.Font = new Font("Segoe UI", 9F);
            _lblStatus.Padding = new Padding(8, 4, 0, 0);

            Controls.Add(pn);
            Controls.Add(_grid);
            Controls.Add(_lblStatus);
            RefreshList();
        }

        private void RefreshList()
        {
            _grid.Rows.Clear();
            foreach (var u in UserStore.GetAllUsers().OrderBy(u => u.Username))
            {
                var f = DisplaySettings.UserFactor(u.Username);
                _grid.Rows.Add(u.Username, u.Level.ToString(), f.HasValue ? DisplaySettings.Format(f.Value) : "");
            }
        }

        private void SaveUser()
        {
            string name = _txtUser.Text.Trim();
            if (name.Length == 0) { _lblStatus.Text = "Enter a user name."; return; }
            SecurityLevel level;
            Enum.TryParse(Convert.ToString(_cmbLevel.SelectedItem), out level);

            bool exists = UserStore.GetAllUsers().Any(u => string.Equals(u.Username, name, StringComparison.OrdinalIgnoreCase));
            if (!exists && _txtPass.Text.Length == 0)
            { _lblStatus.Text = "A NEW user needs a password."; return; }

            // Checked here so the operator gets a sentence, not an exception: a
            // ";" in a name broke the stored file, and demoting the only Admin
            // left nobody able to manage the HMI.
            if (!UserStore.IsValidUsername(name))
            { _lblStatus.Text = "A user name may not contain ';'."; return; }
            if (UserStore.WouldRemoveLastAdmin(name, level))
            { _lblStatus.Text = "The LAST admin account cannot be given a lower level."; return; }
            double? factor = null;
            if (_cmbFactor.Text.Trim().Length > 0)
            {
                factor = DisplaySettings.Parse(_cmbFactor.Text);
                if (!factor.HasValue) { _lblStatus.Text = FactorHint; return; }
            }

            try
            {
                if (exists && _txtPass.Text.Length == 0)
                {
                    // level-only change: keep the current password hash
                    UserStore.SetLevel(name, level);
                    AuditLogger.Log($"changed level of user '{name}' to {level}.");
                }
                else
                {
                    UserStore.AddOrUpdateUser(name, _txtPass.Text, level);
                    AuditLogger.Log($"{(exists ? "reset password of" : "created")} user '{name}' ({level}).");
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                _lblStatus.Text = ex.Message;
                return;
            }
            var before = DisplaySettings.UserFactor(name);
            DisplaySettings.SetUserFactor(name, factor);
            if (before != factor)
                AuditLogger.Log($"magnifying factor of user '{name}': {(factor.HasValue ? DisplaySettings.Format(factor.Value) : "the panel's")}.");
            _txtPass.Text = "";
            RefreshList();
            _lblStatus.Text = $"User '{name}' saved.";

            // Your own factor changed: offered at once, not at the next sign-in.
            if (factor.HasValue && string.Equals(name, SecurityManager.CurrentUser, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(factor.Value - Kaban.UI.Controls.UiScale.Factor) > 0.001)
                OfferRestart(factor.Value, keepSignedIn: true);
        }

        private static string FactorHint
        {
            get { return LanguageManager.GetString("Msg_FactorRange", "The magnifying factor is a number from 0.8 to 1.5 (1.1, 1.25 ...)."); }
        }

        private void SavePanelFactor()
        {
            var f = DisplaySettings.Parse(_cmbPanelFactor.Text);
            if (!f.HasValue) { _lblStatus.Text = FactorHint; return; }
            DisplaySettings.PanelFactor = f.Value;
            // The next start is at the panel's factor (a user with their own is
            // offered theirs when they sign in).
            DisplaySettings.StartFactor = f.Value;
            _cmbPanelFactor.Text = DisplaySettings.Format(f.Value);
            AuditLogger.Log("magnifying factor of the panel: " + DisplaySettings.Format(f.Value) + ".");
            _lblStatus.Text = LanguageManager.GetString("Msg_PanelFactorSaved", "Kept. It takes effect when the HMI restarts.");
            if (Math.Abs(f.Value - Kaban.UI.Controls.UiScale.Factor) > 0.001) OfferRestart(f.Value, keepSignedIn: true);
        }

        private void OfferRestart(double factor, bool keepSignedIn)
        {
            var shell = FindForm() as Kaban.UI.Forms.MainForm;
            string question = string.Format(LanguageManager.GetString("Msg_FactorRestartNow",
                "Restart the HMI now with the magnifying factor {0}? The machine keeps running, the screen is back in a few seconds, and you stay signed in."),
                DisplaySettings.Format(factor));
            if (shell == null ||
                MessageBox.Show(this, question, LanguageManager.GetString("Ttl_Magnifier", "Magnifying factor"),
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            shell.RestartWithFactor(factor, keepSignedIn);
        }

        private void DeleteUser()
        {
            string name = _txtUser.Text.Trim();
            if (name.Length == 0) return;

            var target = UserStore.GetAllUsers().FirstOrDefault(u => string.Equals(u.Username, name, StringComparison.OrdinalIgnoreCase));
            if (target == null) { _lblStatus.Text = $"No user '{name}'."; return; }
            if (target.Level == SecurityLevel.Admin &&
                UserStore.GetAllUsers().Count(u => u.Level == SecurityLevel.Admin) <= 1)
            { _lblStatus.Text = "Cannot delete the LAST admin account."; return; }
            if (string.Equals(name, SecurityManager.CurrentUser, StringComparison.OrdinalIgnoreCase))
            { _lblStatus.Text = "Cannot delete the account you are logged in with."; return; }

            if (MessageBox.Show($"Delete user '{name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            UserStore.RemoveUser(name);
            AuditLogger.Log($"deleted user '{name}'.");
            RefreshList();
            _lblStatus.Text = $"User '{name}' deleted.";
        }
    }
}

