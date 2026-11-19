using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// HMI SETTINGS > BACKUP (19 November - the old program's Backup Settings): the HMI's data (the profile library, the users, the
    /// settings, the labels, the notes, the production log) backed up as one zip - now, or daily by itself - into a folder (a USB stick,
    /// a network folder), the newest kept; a backup restored (the current data backed up first, the program restarted after).
    /// </summary>
    public sealed class DataBackupControl : UserControl, IRebuildForLanguage
    {
        object IRebuildForLanguage.Place { get { return null; } }
        void IRebuildForLanguage.ReturnTo(object place) { }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        private readonly TextBox _folder = new TextBox { Name = "bkFolder", Width = 420 };
        private readonly CheckBox _daily = new CheckBox { Name = "bkDaily", AutoSize = true };
        private readonly NumericUpDown _keep = new NumericUpDown { Name = "bkKeep", Minimum = 1, Maximum = 365, Width = 70 };
        private readonly DataGridView _grid = new DataGridView { Name = "bkGrid" };
        private readonly Label _status = new Label { Name = "bkStatus", AutoSize = true };
        private bool _filling;

        /// <summary>Asked before a restore (tests answer it).</summary>
        public static Func<IWin32Window, string, bool> Confirm = (owner, text) =>
            MessageBox.Show(owner, text, LanguageManager.GetString("Tab_Backup", "Backup"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

        public DataBackupControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;
            var tips = UiStyle.Tips();

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false, Padding = new Padding(4, 4, 0, 0), BackColor = Color.White };
            var now = UiStyle.TextButton("💾 " + T("Bk_Now", "Back up now"), 0, (s, e) => BackUpNow());
            var restore = UiStyle.TextButton("↺ " + T("Bk_Restore", "Restore the selected"), 0, (s, e) => RestoreSelected());
            var open = UiStyle.ToolButton("📂", T("Bk_Open", "Open the backup folder"), tips, (s, e) =>
            {
                try { System.IO.Directory.CreateDirectory(DataBackup.Folder); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = DataBackup.Folder, UseShellExecute = true }); } catch { }
            });
            now.Name = "bkNow"; restore.Name = "bkRestore";
            foreach (var b in new Control[] { now, restore, open }) { b.Margin = new Padding(2, 0, 4, 0); bar.Controls.Add(b); }
            _status.Margin = new Padding(12, 9, 0, 0);
            bar.Controls.Add(_status);

            var settings = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false, Padding = new Padding(8, 6, 0, 0) };
            var browse = UiStyle.ToolButton("📁", T("Bk_Browse", "Choose the folder"), tips, (s, e) =>
            {
                using (var d = new FolderBrowserDialog { SelectedPath = _folder.Text })
                    if (d.ShowDialog(FindForm()) == DialogResult.OK) { _folder.Text = d.SelectedPath; Keep(); }
            });
            settings.Controls.Add(new Label { Text = T("Bk_Folder", "Backups in"), AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
            settings.Controls.Add(_folder);
            settings.Controls.Add(browse);
            _daily.Text = T("Bk_Daily", "Daily by itself");
            _daily.Margin = new Padding(16, 5, 6, 0);
            settings.Controls.Add(_daily);
            settings.Controls.Add(new Label { Text = T("Bk_Keep", "the newest kept"), AutoSize = true, Margin = new Padding(10, 6, 4, 0) });
            settings.Controls.Add(_keep);
            _folder.Leave += (s, e) => { if (!_filling) Keep(); };
            _daily.CheckedChanged += (s, e) => { if (!_filling) Keep(); };
            _keep.ValueChanged += (s, e) => { if (!_filling) Keep(); };

            var note = new Label
            {
                Dock = DockStyle.Top, Height = 36, Padding = new Padding(8, 2, 8, 0), ForeColor = Color.FromArgb(110, 116, 124), Font = UiStyle.Small,
                Text = T("Bk_Note", "One zip a backup: the profile library, the users, the settings, the labels, the notes, the production log (C:\\ProgramData\\KabanHMI). Not in it: the audit logs, the PLC backups and safety copies.")
            };

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = Color.White;
            UiStyle.GridLook(_grid);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "at", HeaderText = T("Bk_At", "Made"), FillWeight = 20 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = T("Bk_File", "File"), FillWeight = 55 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "size", HeaderText = T("Bk_Size", "Size"), FillWeight = 12 });

            Controls.Add(_grid);
            Controls.Add(note);
            Controls.Add(settings);
            Controls.Add(bar);
            restore.Enabled = SecurityManager.HasAccess(SecurityLevel.Admin) || SecurityManager.HasAccess(SecurityLevel.Maintenance);
            Fill();
        }

        private void Fill()
        {
            _filling = true;
            try
            {
                _folder.Text = DataBackup.Folder;
                _daily.Checked = DataBackup.Daily;
                _keep.Value = Math.Max(_keep.Minimum, Math.Min(_keep.Maximum, DataBackup.Keep));
                _grid.Rows.Clear();
                foreach (var b in DataBackup.List())
                {
                    int i = _grid.Rows.Add(b.At.ToString("yyyy-MM-dd HH:mm", CI), b.Name, (b.Bytes / 1024.0 / 1024.0).ToString("0.0", CI) + " MB");
                    _grid.Rows[i].Tag = b;
                }
            }
            finally { _filling = false; }
        }

        private void Keep()
        {
            DataBackup.SetSettings(_folder.Text, _daily.Checked, (int)_keep.Value);
            Fill();
        }

        private void Say(string text, bool ok)
        {
            _status.ForeColor = ok ? Color.FromArgb(30, 110, 50) : Color.FromArgb(160, 40, 40);
            _status.Text = text;
        }

        private void BackUpNow()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                var path = DataBackup.Create(SecurityManager.CurrentUser);
                Fill();
                Say(string.Format(T("Bk_Made", "Backed up: {0}"), System.IO.Path.GetFileName(path)), true);
            }
            catch (Exception ex) { Say(ex.Message, false); }
            finally { Cursor = Cursors.Default; }
        }

        private void RestoreSelected()
        {
            var b = _grid.CurrentRow != null ? _grid.CurrentRow.Tag as DataBackupFile : null;
            if (b == null) { Say(T("Bk_Pick", "Pick the backup to restore first."), false); return; }
            if (!Confirm(FindForm(), string.Format(T("Bk_Ask", "Restore the HMI's data from {0} ({1})? The data as it is now is backed up first - the program restarts after."),
                                                   b.Name, b.At.ToString("yyyy-MM-dd HH:mm", CI)))) return;
            Cursor = Cursors.WaitCursor;
            try
            {
                int n = DataBackup.Restore(b.Path);
                Fill();
                Say(string.Format(T("Bk_Restored", "{0} file(s) restored - restart the program to use them."), n), true);
                try { Kaban.UI.Forms.MainForm.NotifyRestartRequired(FindForm(), T("Bk_RestartWhat", "the HMI's data restored")); } catch { }
            }
            catch (Exception ex) { Say(ex.Message, false); }
            finally { Cursor = Cursors.Default; }
        }

        /// <summary>The page's parts (tests).</summary>
        public DataGridView Grid { get { return _grid; } }
        public string Status { get { return _status.Text; } }
        public void Now() { BackUpNow(); }
        public void RestoreSelectedNow() { RestoreSelected(); }
    }

    /// <summary>HMI Settings > Backup: a Maintenance user's.</summary>
    public class DataBackupPage : ISettingsPage
    {
        public string TitleKey { get { return "Tab_Backup"; } }
        public string TitleFallback { get { return "Backup"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Maintenance; } }
        public UserControl GetControl() { return new DataBackupControl(); }
    }
}
