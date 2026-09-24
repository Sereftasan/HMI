using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// Per-alarm settings the ENGINEER owns, kept beside the alarm list rather
    /// than inside it: the severity an alarm is reported with, and a HELPER LINK -
    /// a document, drawing or page the operator can open when that alarm fires.
    ///
    /// Stored next to the machine (AlarmSettings.cfg) rather than in Alarms.json,
    /// so regenerating the alarm list from the customer's workbook never discards
    /// what the engineer has tuned on site.
    /// </summary>
    public class AlarmSettingsControl : UserControl
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _status = new Label();

        public AlarmSettingsControl() : this(null) { }

        private readonly string _moduleId;

        /// <summary>Codes are scoped "Module.Name" by the loader; unscoped ones show everywhere.</summary>
        private bool Mine(AlarmDefinition d)
        {
            if (string.IsNullOrEmpty(_moduleId) || d == null) return true;
            var code = d.AlarmCode ?? "";
            int dot = code.IndexOf('.');
            return dot <= 0 || string.Equals(code.Substring(0, dot), _moduleId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// THIS MACHINE'S ALARMS ONLY. Built with no machine, the page listed every
        /// alarm in the program - H500's and H800's Alarm Settings showed FA2010's.
        /// </summary>
        public AlarmSettingsControl(string moduleId)
        {
            _moduleId = moduleId;
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var tips = Kaban.UI.Controls.UiStyle.Tips();
            var bar = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                CanOverflow = true,
                LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
                AutoSize = false,
                Height = 40,
                BackColor = Color.White,
                Renderer = new ToolStripProfessionalRenderer()
            };
            Action<Control> add = c => bar.Items.Add(new ToolStripControlHost(c) { AutoSize = false, Size = c.Size });
            // Greyed until an edit, like the PLC Connection page.
            _btnSave = UiStyle.ToolButton("\uD83D\uDCBE", "Save the alarm settings", tips, (s, e) => Save());
            add(_btnSave);
            _dirty.Guard(_btnSave);
            add(UiStyle.ToolButton("\uD83D\uDD04", "Reload from disk", tips, (s, e) => LoadRows()));
            add(UiStyle.ToolButton("\uD83D\uDCC2", "Open the helper document of the selected alarm",
                                   tips, (s, e) => OpenHelper()));

            _grid.Dock = DockStyle.Fill;
            // Column captions need room UNDER the text: a header sized to the
            // cap height slices through g, p and y.
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 5);
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            // FILL, not fixed widths: the columns share whatever width the panel
            // actually has, so the grid neither overflows a narrow panel nor leaves
            // a dead strip on a wide one. FillWeight sets the PROPORTIONS.
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.AllowUserToResizeColumns = true;
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Address", HeaderText = "Address", FillWeight = 12, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Text", HeaderText = "Alarm", FillWeight = 42, ReadOnly = true });

            var sev = new DataGridViewComboBoxColumn
            { Name = "Severity", HeaderText = "Severity", FillWeight = 12 };
            sev.Items.AddRange(Enum.GetNames(typeof(AlarmSeverity)));
            _grid.Columns.Add(sev);


            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Helper",
                HeaderText = "Helper link (file, page or URL)",
                FillWeight = 31
            });

            // THE MISSED CALL. Every other grid in the program got this; the sweep
            // that added it silently skipped this file, so its headings stayed
            // clipped no matter how many times the helper itself was rewritten.
            UiStyle.EnableAutoFit(_grid);
            UiStyle.EnableTableTools(_grid, "AlarmSettings");
            _dirty.Watch(_grid);

            // A column must never start narrower than its own HEADING. The default
            // MinimumWidth is 5px, so "Helper link (file, page or URL)" and even
            // "Severity" opened clipped to an ellipsis and the operator had to drag
            // every column before the grid could be read at all.
            FitColumnsToHeadings(_grid);
            _grid.CellDoubleClick += (s, e) => { if (e.ColumnIndex == _grid.Columns["Helper"].Index) BrowseHelper(e.RowIndex); };

            // A combo cell does not commit until focus leaves it, which makes the
            // code column look one edit behind. Committing on change fixes that.
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.Padding = new Padding(8, 4, 0, 0);
            _status.Font = new Font("Segoe UI", 9F);

            Controls.Add(_grid);
            Controls.Add(_status);
            Controls.Add(bar);

            LoadRows();
        }


        private void LoadRows()
        {
            _grid.Rows.Clear();
            foreach (var def in AlarmManager.GetAllDefinitions().Where(Mine).OrderBy(d => d.Address))
            {
                // Straight from the definition - the only place these live now
                // that AlarmSettings.cfg is gone.
                string sev = def.Severity.ToString();
                string helper = def.HelperKeys != null && def.HelperKeys.Count > 0
                              ? string.Join(";", def.HelperKeys) : "";
                int row = _grid.Rows.Add(def.Address, LanguageManager.Localize(def.TranslationKey), sev, helper);
                // The DEFINITION rides on the row. Save used to look alarms up by
                // address, and FA2010, H500 and H800 all use M801...: an edit on one
                // machine's page could land on another machine's alarm.
                _grid.Rows[row].Tag = def;
            }
            // What discovery found, so a unit whose error bits were set in the designer
            // can be checked without raising an alarm.
            _status.Text = _grid.Rows.Count + " alarms. " + Kaban.UI.Helpers.AlarmHelpLinks.Count +
                           " linked to units on the machine pages" +
                           (Kaban.UI.Helpers.AlarmHelpLinks.Conflicts.Count > 0
                               ? " - " + Kaban.UI.Helpers.AlarmHelpLinks.Conflicts.Count + " conflict(s), see the log" : "") + ".";
            _dirty.Reset();   // filling the grid is not an edit
        }


        /// <summary>
        /// Writes the machine's own Alarms.json. AlarmSettings.cfg is gone - see
        /// AlarmDefinitionLoader.SaveLoadedEntries for why.
        /// </summary>
        private readonly DirtyTracker _dirty = new DirtyTracker();
        private Control _btnSave;

        private void Save()
        {
            try
            {
                foreach (DataGridViewRow r in _grid.Rows)
                {
                    if (r.IsNewRow) continue;
                    var def = r.Tag as AlarmDefinition;
                    if (def == null) continue;

                    AlarmSeverity sev;
                    if (Enum.TryParse(Convert.ToString(r.Cells["Severity"].Value), out sev))
                        def.Severity = sev;

                    // Helper files only: which unit an alarm belongs to now comes
                    // from the unit itself (FunctionUnitHMI / AxisSettingHMI).
                    string helper = Convert.ToString(r.Cells["Helper"].Value);
                    def.HelperKeys = string.IsNullOrWhiteSpace(helper)
                        ? new List<string>() : new List<string> { helper.Trim() };
                }

                // THIS machine's file only (all files when the page has no machine).
                string path = AlarmDefinitionLoader.SaveLoadedEntries(_moduleId);
                if (string.IsNullOrEmpty(path))
                {
                    _status.Text = "Nothing to save.";
                    return;
                }
                _status.Text = "Saved to " + path;
                AuditLogger.Log("alarm settings saved to " + path + ".");
                _dirty.Reset();
            }
            catch (Exception ex) { _status.Text = "Save failed: " + ex.Message; }
        }

        /// <summary>
        /// Raises every column's MinimumWidth to fit its heading text, so no caption
        /// is ever clipped at the width the grid opens with. Shared shape: any grid
        /// in this program with named headings wants this.
        /// </summary>
        private static void FitColumnsToHeadings(DataGridView grid)
        {
            using (var g = grid.CreateGraphics())
            {
                var font = grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font;
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    var size = TextRenderer.MeasureText(g, col.HeaderText ?? "", font);
                    // + sort glyph, padding and both grid lines
                    col.MinimumWidth = Math.Max(col.MinimumWidth, size.Width + 28);
                }
            }
        }


        private void BrowseHelper(int row)
        {
            if (row < 0) return;
            using (var dlg = new OpenFileDialog
            { Filter = "Documents (*.pdf;*.png;*.jpg;*.txt;*.html)|*.pdf;*.png;*.jpg;*.txt;*.html|All files (*.*)|*.*" })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string chosen = dlg.FileName;
                // SEVERAL FILES, joined with ";". The alarm screen splits them
                // again, so one alarm can offer a manual, a drawing and a note.
                _grid.Rows[row].Cells["Helper"].Value =
                    chosen.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase)
                        ? chosen.Substring(baseDir.Length)      // portable
                        : chosen;
            }
        }

        private void OpenHelper()
        {
            if (_grid.SelectedRows.Count == 0) return;
            string helper = Convert.ToString(_grid.SelectedRows[0].Cells["Helper"].Value);
            if (string.IsNullOrWhiteSpace(helper)) { _status.Text = "No helper link on that alarm."; return; }
            string path = Path.IsPathRooted(helper) || helper.StartsWith("http")
                ? helper
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, helper);
            try { System.Diagnostics.Process.Start(path); }
            catch (Exception ex) { _status.Text = "Could not open: " + ex.Message; }
        }
    }

    /// <summary>Alarm severity and helper links - a tab of Machine settings.</summary>
    public class AlarmSettingsPage : ISettingsPage
    {
        private readonly string _moduleId;
        public AlarmSettingsPage() : this(null) { }
        public AlarmSettingsPage(Kaban.Core.Machines.IMachineMountContext ctx) { _moduleId = ctx == null ? null : ctx.ModuleId; }
        public string TitleKey { get { return "Set_Alarms"; } }
        public string TitleFallback { get { return "Alarm Settings"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Maintenance; } }
        public UserControl GetControl() { return new AlarmSettingsControl(_moduleId); }
    }
}

