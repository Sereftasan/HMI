using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// Brings a previous install's settings into this one.
    ///
    /// The upgrade an engineer actually performs is: copy the new build onto the
    /// panel, then spend an hour putting back everything that was configured on
    /// the old one. This page is that hour.
    ///
    /// It works in two steps on purpose - LOOK, then APPLY. The list shows every
    /// file found, its format version, how many rows it holds and whether it needs
    /// converting, and nothing is written until the engineer has read that and
    /// pressed Import. A settings transfer that silently does the wrong thing is
    /// discovered when the machine behaves oddly days later.
    /// </summary>
    public class ImportSettingsControl : UserControl
    {
        private readonly TextBox _source = new TextBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _status = new Label();
        private readonly DirtyTracker _dirty = new DirtyTracker();
        private Control _btnImport;
        private List<SettingsMigration.Finding> _found = new List<SettingsMigration.Finding>();

        public ImportSettingsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Padding = new Padding(8, 6, 8, 8);

            var tips = UiStyle.Tips();

            // ---- source folder ----
            var top = new Panel { Dock = DockStyle.Top, Height = 76 };
            Controls.Add(top);

            top.Controls.Add(new Label
            {
                Text = LanguageManager.GetString("Imp_Source", "Previous installation folder:"),
                Location = new Point(2, 8), Size = new Size(210, 22),
                TextAlign = ContentAlignment.MiddleRight, Font = UiStyle.Body
            });

            _source.SetBounds(218, 6, 560, 26);
            _source.Font = UiStyle.Body;
            top.Controls.Add(_source);

            var browse = UiStyle.Round(new Button
            {
                Text = LanguageManager.GetString("Btn_Browse", "Browse..."),
                Location = new Point(786, 5), Size = new Size(96, 28), BackColor = Color.White
            });
            browse.Click += (s, e) => Browse();
            top.Controls.Add(browse);

            var scan = UiStyle.Round(new Button
            {
                Text = LanguageManager.GetString("Btn_Scan", "Look for settings"),
                Location = new Point(218, 40), Size = new Size(160, 30), BackColor = Color.White
            });
            scan.Click += (s, e) => Scan();
            top.Controls.Add(scan);

            _btnImport = UiStyle.Round(new Button
            {
                Text = LanguageManager.GetString("Btn_Import", "Import selected"),
                Location = new Point(386, 40), Size = new Size(160, 30), BackColor = Color.White
            });
            _btnImport.Click += (s, e) => Apply();
            top.Controls.Add(_btnImport);
            _dirty.Guard(_btnImport);       // nothing to import until something is found

            // ---- what was found ----
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = Color.White;
            _grid.BorderStyle = BorderStyle.FixedSingle;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            { Name = "Use", HeaderText = LanguageManager.GetString("Imp_Use", "Import"), FillWeight = 8 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Kind", HeaderText = LanguageManager.GetString("Imp_Kind", "Settings"), FillWeight = 22, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "File", HeaderText = LanguageManager.GetString("Imp_File", "File"), FillWeight = 20, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Rows", HeaderText = LanguageManager.GetString("Imp_Rows", "Entries"), FillWeight = 8, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Ver", HeaderText = LanguageManager.GetString("Imp_Version", "Format"), FillWeight = 10, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Note", HeaderText = LanguageManager.GetString("Imp_Note", "What will happen"), FillWeight = 32, ReadOnly = true });

            UiStyle.EnableAutoFit(_grid);
            UiStyle.EnableTableTools(_grid, "ImportSettings");
            Controls.Add(_grid);
            _grid.BringToFront();

            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.ForeColor = Color.FromArgb(110, 116, 124);
            _status.Text = LanguageManager.GetString("Imp_Hint",
                "Choose the folder of the previous version, then look for settings.");
            Controls.Add(_status);

            Kaban.UI.Controls.UiStyle.SetTip(tips, _source, LanguageManager.GetString("Imp_SourceTip",
                "The folder the old program ran from - its own folder or the install root."));
        }

        private void Browse()
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = LanguageManager.GetString("Imp_Source", "Previous installation folder:")
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _source.Text = dlg.SelectedPath;
                Scan();
            }
        }

        private void Scan()
        {
            _grid.Rows.Clear();
            _found = SettingsMigration.Inspect(_source.Text, AppDomain.CurrentDomain.BaseDirectory);

            foreach (var f in _found)
                _grid.Rows.Add(f.Selected, f.Kind, f.FileName, f.Rows,
                               "v" + f.DetectedVersion,
                               string.IsNullOrEmpty(f.Note)
                                   ? LanguageManager.GetString("Imp_Replace", "replaces the current file")
                                   : f.Note);

            if (_found.Count == 0)
            {
                _status.Text = LanguageManager.GetString("Imp_NoneFound",
                    "No settings files found in that folder.");
                _dirty.Reset();
                return;
            }

            int upgrades = _found.Count(f => f.NeedsUpgrade && f.Selected);
            _status.Text = string.Format(
                LanguageManager.GetString("Imp_Found", "{0} files found, {1} need converting."),
                _found.Count, upgrades);
            _dirty.MarkDirty();
        }

        private void Apply()
        {
            if (_found.Count == 0) return;

            for (int i = 0; i < _found.Count && i < _grid.Rows.Count; i++)
                _found[i].Selected = Convert.ToBoolean(_grid.Rows[i].Cells["Use"].Value ?? false);

            var chosen = _found.Where(f => f.Selected).ToList();
            if (chosen.Count == 0) return;

            // A LAST, PLAIN QUESTION. This overwrites the running machine's
            // configuration, and the operator should see the count before it does.
            var answer = MessageBox.Show(this,
                string.Format(LanguageManager.GetString("Imp_Confirm",
                    "Import {0} settings files into this installation?\r\n\r\n" +
                    "The current files are backed up first."), chosen.Count),
                LanguageManager.GetString("Ttl_ImportSettings", "Import settings"),
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (answer != DialogResult.OK) return;

            try
            {
                var backup = SettingsMigration.Apply(chosen, AppDomain.CurrentDomain.BaseDirectory);
                _status.Text = string.Format(
                    LanguageManager.GetString("Imp_Done",
                        "{0} files imported. Previous files saved in {1}. Restart to load them."),
                    chosen.Count, string.IsNullOrEmpty(backup) ? "-" : Path.GetFileName(backup));
                _dirty.Reset();

                // The program reads these at start-up, so a restart is not a
                // suggestion - saying "done" without it invites an engineer to test
                // against settings that are not loaded yet.
                MessageBox.Show(this,
                    LanguageManager.GetString("Imp_Restart",
                        "The imported settings take effect after the program restarts."),
                    LanguageManager.GetString("Ttl_ImportSettings", "Import settings"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _status.Text = LanguageManager.GetString("Imp_Failed", "Import failed: ") + ex.Message;
                AuditLogger.Log("settings import failed: " + ex.Message);
            }
        }
    }
}

