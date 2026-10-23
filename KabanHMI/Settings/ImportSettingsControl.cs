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
    public class ImportSettingsControl : UserControl, IRebuildForLanguage
    {
        // its texts are put on when it is built: built again in another language (SettingsScreen)
        object IRebuildForLanguage.Place { get { return null; } }
        void IRebuildForLanguage.ReturnTo(object place) { }

        private readonly TextBox _source = new TextBox { Name = "impSource" };
        private readonly DataGridView _grid = new DataGridView { Name = "impGrid" };
        private readonly Label _status = new Label { Name = "impStatus" };
        private readonly DirtyTracker _dirty = new DirtyTracker();
        private Control _btnImport;
        private Button _btnCompare;
        private List<SettingsMigration.Finding> _found = new List<SettingsMigration.Finding>();

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>The files found (tests).</summary>
        internal List<SettingsMigration.Finding> Found { get { return _found; } }
        internal string StatusText { get { return _status.Text; } }
        internal DataGridView Grid { get { return _grid; } }

        /// <summary>The program's folder the files are imported into (replaceable for the tests).</summary>
        public static string TargetFolder = AppDomain.CurrentDomain.BaseDirectory;

        public ImportSettingsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Padding = new Padding(8, 6, 8, 8);

            var tips = UiStyle.Tips();

            // ---- what the page does (23 October: "I couldn't quite understand what 'Look for settings' button do") ----
            var about = new Label
            {
                Dock = DockStyle.Top, AutoSize = false, Height = 64, Name = "impAbout", Font = UiStyle.Body, ForeColor = Color.FromArgb(60, 66, 76),
                Text = T("Imp_About", "Brings an older installation's settings into this program: its PLC connections (PlcSettings.cfg), its alarm " +
                                      "definitions (Alarms.json) and its PLC label list (PlcLabels.csv). 1. Choose the older program's folder - its " +
                                      "settings files are listed, each with how it differs from this program's. 2. Compare to see the lines. " +
                                      "3. Tick the files to take and Import: this program's files are kept first (Backup_<date> in its folder).")
            };
            about.Padding = new Padding(2, 2, 2, 6);
            // as tall as its lines at the page's width
            EventHandler fit = (s, e) =>
            {
                int w = Math.Max(100, about.Width - about.Padding.Horizontal);
                int h = TextRenderer.MeasureText(about.Text, about.Font, new Size(w, 0), TextFormatFlags.WordBreak).Height;
                about.Height = h + about.Padding.Vertical + 2;
            };
            about.Resize += fit;
            about.FontChanged += fit;

            // ---- the older installation's folder ----
            var folderRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 36, ColumnCount = 3 };
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            folderRow.Controls.Add(new Label
            {
                Text = T("Imp_Source", "Previous installation folder:"), AutoSize = true, Font = UiStyle.Body, Margin = new Padding(2, 8, 6, 0)
            }, 0, 0);
            _source.Font = UiStyle.Body;
            _source.Dock = DockStyle.Fill;
            _source.Margin = new Padding(0, 4, 6, 0);
            _source.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { Scan(); e.Handled = e.SuppressKeyPress = true; } };
            folderRow.Controls.Add(_source, 1, 0);
            var browse = UiStyle.Round(new Button
            {
                Text = T("Btn_Browse", "Browse..."), Size = new Size(96, 28), BackColor = Color.White, Margin = new Padding(0, 3, 0, 0)
            });
            browse.Click += (s, e) => Browse();
            folderRow.Controls.Add(browse, 2, 0);

            // ---- the steps ----
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(0, 4, 0, 4) };
            var scan = UiStyle.Round(new Button
            {
                Text = T("Btn_ImpList", "List its settings files"), Size = new Size(190, 30), BackColor = Color.White, Name = "impScan"
            });
            scan.Click += (s, e) => Scan();
            UiStyle.SetTip(tips, scan, T("Tip_ImpList", "Looks in the folder (and its folders) for the older program's settings files and lists them, " +
                                                       "each compared with this program's - nothing is changed yet. Browse... does it by itself."));
            _btnCompare = UiStyle.Round(new Button
            {
                Text = T("Btn_ImpCompare", "Compare..."), Size = new Size(130, 30), BackColor = Color.White, Name = "impCompare", Enabled = false
            });
            _btnCompare.Click += (s, e) => CompareSelected();
            UiStyle.SetTip(tips, _btnCompare, T("Tip_ImpCompare", "This program's file beside the older one's, line by line - what the import would change (a double click on a row too)"));
            _btnImport = UiStyle.Round(new Button
            {
                Text = T("Btn_Import", "Import selected"), Size = new Size(160, 30), BackColor = Color.White, Name = "impImport"
            });
            _btnImport.Click += (s, e) => Apply();
            _dirty.Guard(_btnImport);       // nothing to import until something is found
            var backups = UiStyle.Round(new Button
            {
                Text = T("Btn_ImpBackups", "Backups..."), Size = new Size(120, 30), BackColor = Color.White, Name = "impBackups"
            });
            backups.Click += (s, e) => ShowBackups();
            UiStyle.SetTip(tips, backups, T("Tip_ImpBackups", "The program's files as they were before each import (Backup_<date> in the program's folder)"));
            foreach (var b in new Control[] { scan, _btnCompare, _btnImport, backups }) { b.Margin = new Padding(0, 0, 8, 0); buttons.Controls.Add(b); }

            // ---- what was found ----
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = Color.White;
            _grid.BorderStyle = BorderStyle.FixedSingle;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            { Name = "Use", HeaderText = T("Imp_Use", "Import"), FillWeight = 8 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Kind", HeaderText = T("Imp_Kind", "Settings"), FillWeight = 18, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "File", HeaderText = T("Imp_File", "File"), FillWeight = 20, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Rows", HeaderText = T("Imp_Rows", "Entries"), FillWeight = 8, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Ver", HeaderText = T("Imp_Version", "Format"), FillWeight = 8, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Diff", HeaderText = T("Imp_Diff", "Against this program's"), FillWeight = 26, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Note", HeaderText = T("Imp_Note", "What will happen"), FillWeight = 24, ReadOnly = true });
            _grid.SelectionChanged += (s, e) => _btnCompare.Enabled = SelectedFinding() != null;
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0 && e.ColumnIndex != 0) Compare(e.RowIndex); };

            UiStyle.EnableAutoFit(_grid);
            UiStyle.EnableTableTools(_grid, "ImportSettings");

            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.AutoEllipsis = true;
            _status.ForeColor = Color.FromArgb(110, 116, 124);
            _status.Text = T("Imp_HintList", "Choose the folder of the previous version: its settings files are listed.");

            Controls.Add(_grid);
            Controls.Add(buttons);
            Controls.Add(folderRow);
            Controls.Add(about);
            Controls.Add(_status);

            Kaban.UI.Controls.UiStyle.SetTip(tips, _source, T("Imp_SourceTipList",
                "The folder the old program ran from - its own folder or the install root. Enter lists its settings files."));
        }

        /// <summary>The folder to look in, as typed (tests).</summary>
        internal string Source { get { return _source.Text; } set { _source.Text = value ?? ""; } }

        private SettingsMigration.Finding SelectedFinding()
        {
            var row = _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0] : _grid.CurrentRow;
            return row != null && row.Index >= 0 && row.Index < _found.Count ? _found[row.Index] : null;
        }

        private void CompareSelected()
        {
            var row = _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0] : _grid.CurrentRow;
            if (row != null) Compare(row.Index);
        }

        /// <summary>A file found compared with this program's (SettingsCompareForm).</summary>
        internal void Compare(int row)
        {
            if (row < 0 || row >= _found.Count) return;
            var f = _found[row];
            if (f.Diff == null && !string.IsNullOrEmpty(f.Note) && !f.Selected)
            {
                _status.Text = string.Format(T("Imp_NoCompare", "{0}: not compared - {1}."), f.FileName, f.Note);
                return;
            }
            SettingsCompareForm.Run(FindForm() ?? (IWin32Window)this, f);
        }

        /// <summary>"3 lines changed, 1 added" - how a file found differs from this program's.</summary>
        internal static string DiffText(SettingsMigration.Finding f)
        {
            if (f.Diff == null) return "-";
            if (!f.TargetExists) return T("Imp_DiffNew", "new here - this program has none");
            if (f.Diff.Identical) return T("Imp_DiffSame", "the same - nothing changes");
            var parts = new List<string>();
            if (f.Diff.Changed > 0) parts.Add(string.Format(T("Imp_DiffChanged", "{0} line(s) changed"), f.Diff.Changed));
            if (f.Diff.OnlyRight > 0) parts.Add(string.Format(T("Imp_DiffAdded", "{0} added"), f.Diff.OnlyRight));
            if (f.Diff.OnlyLeft > 0) parts.Add(string.Format(T("Imp_DiffRemoved", "{0} taken away"), f.Diff.OnlyLeft));
            return string.Join(", ", parts);
        }

        /// <summary>The backups of the files imports replaced: their folder opened (the newest one's).</summary>
        private void ShowBackups()
        {
            string newest = null;
            try
            {
                newest = Directory.GetDirectories(TargetFolder, "Backup_*").OrderByDescending(d => Directory.GetCreationTime(d)).FirstOrDefault();
            }
            catch { }
            if (newest == null)
            {
                _status.Text = T("Imp_NoBackups", "No import has replaced a file yet - no backups.");
                return;
            }
            try { System.Diagnostics.Process.Start("explorer.exe", "\"" + newest + "\""); }
            catch (Exception ex) { _status.Text = newest + " - " + ex.Message; return; }
            _status.Text = string.Format(T("Imp_BackupsShown", "The newest backup: {0}."), newest);
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

        internal void Scan()
        {
            _grid.Rows.Clear();
            var cursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try { _found = SettingsMigration.Inspect(_source.Text, TargetFolder); }
            finally { Cursor.Current = cursor; }

            foreach (var f in _found)
            {
                // the same as this program's: nothing to import - left unticked
                if (f.Diff != null && f.Diff.Identical && f.TargetExists) f.Selected = false;
                _grid.Rows.Add(f.Selected, f.Kind, f.FileName, f.Rows,
                               "v" + f.DetectedVersion, DiffText(f),
                               !string.IsNullOrEmpty(f.Note) ? f.Note
                                   : f.Diff != null && f.Diff.Identical && f.TargetExists ? T("Imp_Nothing", "nothing to import")
                                   : f.TargetExists ? T("Imp_Replace", "replaces the current file") : T("Imp_Adds", "added"));
            }
            _btnCompare.Enabled = _found.Count > 0;

            if (_found.Count == 0)
            {
                _status.Text = LanguageManager.GetString("Imp_NoneFound",
                    "No settings files found in that folder.");
                _dirty.Reset();
                return;
            }

            int upgrades = _found.Count(f => f.NeedsUpgrade && f.Selected);
            int differ = _found.Count(f => f.Diff != null && (!f.Diff.Identical || !f.TargetExists));
            _status.Text = string.Format(
                T("Imp_FoundCompared", "{0} file(s) found, {1} different from this program's, {2} to convert - Compare shows the lines."),
                _found.Count, differ, upgrades);
            _dirty.MarkDirty();
        }

        internal void Apply()
        {
            if (_found.Count == 0) return;

            for (int i = 0; i < _found.Count && i < _grid.Rows.Count; i++)
                _found[i].Selected = Convert.ToBoolean(_grid.Rows[i].Cells["Use"].Value ?? false);

            var chosen = _found.Where(f => f.Selected).ToList();
            if (chosen.Count == 0) return;

            // A LAST, PLAIN QUESTION. This overwrites the running machine's
            // configuration, and the operator should see the count before it does.
            if (!Kaban.UI.Profiles.Ask.YesNo(FindForm() ?? (IWin32Window)this, T("Ttl_ImportSettings", "Import settings"),
                    string.Format(T("Imp_Confirm", "Import {0} settings files into this installation?\r\n\r\n" +
                                                   "The current files are backed up first."), chosen.Count) + "\r\n\r\n" +
                    string.Join("\r\n", chosen.Select(f => "  " + f.FileName + ": " + DiffText(f))))) return;

            try
            {
                var backup = SettingsMigration.Apply(chosen, TargetFolder);
                Scan();                     // listed again: the files imported are this program's now
                _status.Text = string.Format(
                    LanguageManager.GetString("Imp_Done",
                        "{0} files imported. Previous files saved in {1}. Restart to load them."),
                    chosen.Count, string.IsNullOrEmpty(backup) ? "-" : Path.GetFileName(backup));
                _dirty.Reset();
                _btnImport.Enabled = false;

                // The program reads these at start-up, so a restart is not a
                // suggestion - saying "done" without it invites an engineer to test
                // against settings that are not loaded yet.
                Kaban.UI.Profiles.Ask.Tell(FindForm() ?? (IWin32Window)this, T("Ttl_ImportSettings", "Import settings"),
                    T("Imp_Restart", "The imported settings take effect after the program restarts."));
            }
            catch (Exception ex)
            {
                _status.Text = LanguageManager.GetString("Imp_Failed", "Import failed: ") + ex.Message;
                AuditLogger.Log("settings import failed: " + ex.Message);
            }
        }
    }
}

