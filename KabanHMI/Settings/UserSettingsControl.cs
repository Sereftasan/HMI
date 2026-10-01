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
    /// Also a user's own SCREEN SCALE (see DisplaySettings) - a parameter of the
    /// user, offered as a restart when they sign in; "No specific request" leaves
    /// them the panel's, which is set under the glass on the caption bar. Chosen
    /// with the caption bar's menu (the usual sizes, and any percent 1% at a time).
    /// </summary>
    public class UserSettingsControl : UserControl
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _txtUser = new TextBox();
        private readonly TextBox _txtPass = new TextBox();
        private readonly ComboBox _cmbLevel = new ComboBox();
        private readonly ScaleChooser _scale = new ScaleChooser();
        private readonly Label _lblStatus = new Label();


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
            _grid.Columns.Add("factor", LanguageManager.GetString("Usr_Factor", "Screen scale"));
            Kaban.UI.Controls.UiStyle.EnableAutoFit(_grid);
            Kaban.UI.Controls.UiStyle.EnableTableTools(_grid, "Users");
            _grid.SelectionChanged += (s, e) =>
            {
                if (_grid.SelectedRows.Count == 0) return;
                _txtUser.Text = Convert.ToString(_grid.SelectedRows[0].Cells[0].Value);
                var lvl = Convert.ToString(_grid.SelectedRows[0].Cells[1].Value);
                _cmbLevel.SelectedItem = lvl;
                _txtPass.Text = "";
                // The user's OWN request, from where it is kept - never the scale on screen.
                _scale.Value = DisplaySettings.UserFactor(_txtUser.Text);
            };
            // Shown again after a change elsewhere: the list as it is now.
            VisibleChanged += (s, e) => { if (Visible) RefreshList(); };

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
                    Font = Kaban.UI.Controls.UiStyle.Small
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
            row(LanguageManager.GetString("Usr_FactorOwn", "Screen scale") + ":", _scale);

            var tips = Kaban.UI.Controls.UiStyle.Tips();
            Func<string, string, int, Button> tool = (emoji, tip, left) =>
            {
                var b = Kaban.UI.Controls.UiStyle.ToolButton(emoji, tip, tips);
                b.Left = left;
                b.Top = y + 4;
                pn.Controls.Add(b);
                return b;
            };
            var btnSave = tool("\uD83D\uDCBE", LanguageManager.GetString("Tip_UsrSave", "Add the user, or update level/password of an existing one"), 150);
            var btnDelete = tool("\uD83D\uDDD1", LanguageManager.GetString("Tip_UsrDelete", "Delete the selected user"), 200);

            btnSave.Click += (s, e) => SaveUser();
            btnDelete.Click += (s, e) => DeleteUser();
            tips.SetToolTip(_scale, LanguageManager.GetString("Tip_UsrFactor",
                "The screen scale this user asks for - offered when they sign in."));

            _lblStatus.Dock = DockStyle.Bottom;
            _lblStatus.Height = 26;
            _lblStatus.Font = Kaban.UI.Controls.UiStyle.Small;
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
                _grid.Rows.Add(u.Username, u.Level.ToString(), f.HasValue ? DisplaySettings.Percent(f.Value) : "");
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
            double? factor = _scale.Value;

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
                AuditLogger.Log($"screen scale of user '{name}': {(factor.HasValue ? DisplaySettings.Percent(factor.Value) : "no specific request")}.");
            _txtPass.Text = "";
            RefreshList();
            _lblStatus.Text = $"User '{name}' saved.";

            // Your own factor changed: offered at once, not at the next sign-in.
            if (factor.HasValue && string.Equals(name, SecurityManager.CurrentUser, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(factor.Value - Kaban.UI.Controls.UiScale.Factor) > 0.001)
                OfferRestart(factor.Value, keepSignedIn: true);
        }

        private void OfferRestart(double factor, bool keepSignedIn)
        {
            var shell = FindForm() as Kaban.UI.Forms.MainForm;
            string question = string.Format(LanguageManager.GetString("Msg_FactorRestartNow",
                "Restart the HMI now with the screen scale {0}? The machine keeps running, the screen is back in a few seconds, and you stay signed in."),
                DisplaySettings.Percent(factor));
            if (shell == null ||
                MessageBox.Show(this, question, LanguageManager.GetString("Ttl_Magnifier", "Screen scale"),
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

    /// <summary>
    /// A user's screen scale, chosen as on the caption bar: looks like the other
    /// fields' boxes; a click opens the same menu - "No specific request", the
    /// usual sizes, and the row that sets any percent 1% at a time.
    /// </summary>
    internal sealed class ScaleChooser : Button
    {
        private double? _value;

        public ScaleChooser()
        {
            FlatStyle = FlatStyle.Flat;
            BackColor = Color.White;
            TextAlign = ContentAlignment.MiddleLeft;
            Height = 26;
            Cursor = Cursors.Hand;
            AccessibleName = "Screen scale";
            FlatAppearance.BorderColor = Color.FromArgb(160, 166, 176);
            Text = Caption;
        }

        /// <summary>The user's request; null: no specific request (the panel's).</summary>
        public double? Value
        {
            get { return _value; }
            set
            {
                _value = value.HasValue ? DisplaySettings.Clamp(value.Value) : (double?)null;
                Text = Caption;
                Invalidate();
            }
        }

        public string Caption
        {
            get { return _value.HasValue ? DisplaySettings.Percent(_value.Value) : Kaban.UI.Forms.MainForm.NoScaleRequest; }
        }

        /// <summary>The menu a click opens (tests use it as the operator would).</summary>
        internal ContextMenuStrip BuildMenu()
        {
            return Kaban.UI.Forms.MainForm.BuildScaleMenu(null, _value, true, v => Value = v);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Kaban.UI.Controls.UiStyle.ShowPopup(BuildMenu(), this, new Point(0, Height));
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            // The drop-down caret, as on a combo box.
            float cx = Width - Kaban.UI.Controls.UiScale.For(this, 12), cy = Height / 2f;
            float d = Kaban.UI.Controls.UiScale.For(this, 4);
            using (var b = new SolidBrush(Color.FromArgb(60, 66, 76)))
                pevent.Graphics.FillPolygon(b, new[] { new PointF(cx - d, cy - d / 2), new PointF(cx + d, cy - d / 2), new PointF(cx, cy + d / 2 + 1) });
        }
    }
}
