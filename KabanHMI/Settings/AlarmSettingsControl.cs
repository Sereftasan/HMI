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
    ///
    /// THE ALARMS THE HMI RAISES ITSELF (1 October): above the table, switched on
    /// per machine, with the general delay; in the table, a delay of its own for
    /// a cylinder movement's alarm (blank: the general one). See AlarmGeneration.
    /// </summary>
    public class AlarmSettingsControl : UserControl, IRebuildForLanguage
    {
        // its texts are put on when it is built: built again in another language (SettingsScreen)
        object IRebuildForLanguage.Place { get { return null; } }
        void IRebuildForLanguage.ReturnTo(object place) { }

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
            _btnSave = UiStyle.ToolButton("\uD83D\uDCBE", LanguageManager.GetString("Tip_AlarmSetSave", "Save the alarm settings"), tips, (s, e) => Save());
            add(_btnSave);
            _dirty.Guard(_btnSave);
            add(UiStyle.ToolButton("\u21BA", LanguageManager.GetString("Tip_AlarmSetRevert", "Throw the changes away and read the saved settings again"), tips, (s, e) => LoadRows()));
            add(UiStyle.ToolButton("\u270F\uFE0F", LanguageManager.GetString("Tip_AlarmSetLinks", "Edit the helper links of the selected alarm (several documents)"),
                                   tips, (s, e) => { if (_grid.CurrentRow != null) EditHelpers(_grid.CurrentRow.Index); }));
            _btnOpen = UiStyle.ToolButton("\uD83D\uDCC2", LanguageManager.GetString("Tip_AlarmSetOpen", "Open a helper document of the selected alarm"),
                                          tips, (s, e) => OpenHelper());
            add(_btnOpen);

            // ---- the alarms the HMI raises itself ----
            var gen = AlarmGeneration.For(GenerationModule);
            _genPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, BackColor = Color.FromArgb(246, 248, 251),
                Padding = new Padding(6, 6, 6, 4), WrapContents = true, AutoScroll = false,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            _genOn = new CheckBox
            {
                Name = "GenerationOn", AutoSize = true, Checked = gen.Enabled, Margin = new Padding(3, 3, 18, 0),
                Text = LanguageManager.GetString("Set_AlarmGenOn", "The HMI raises the cylinder alarms itself")
            };
            Kaban.UI.Controls.UiStyle.SetTip(tips, _genOn, LanguageManager.GetString("Tip_AlarmGenOn",
                "On: every cylinder unit's movement is timed from its output and must be confirmed by its sensors within its delay, " +
                "or the HMI raises its alarm with the signal that failed (M840.1, M840.2...). The PLC's own alarm for the same device " +
                "is then not listed twice."));
            // The axis alarms: their own switch, on unless switched off; at once, no delay (8 October)
            _genAxes = new CheckBox
            {
                Name = "GenerationAxes", AutoSize = true, Checked = gen.AxisAlarms, Margin = new Padding(3, 3, 18, 0),
                Text = LanguageManager.GetString("Set_AlarmGenAxes", "The HMI raises the axis and inverter alarms itself")
            };
            Kaban.UI.Controls.UiStyle.SetTip(tips, _genAxes, LanguageManager.GetString("Tip_AlarmGenAxes",
                "On: an axis row raises its alarm as soon as its Axis Alarm or Servo Alarm shows a code other than 0 (no delay) - " +
                "its Axis Warning is only a warning. An inverter's unit raises its alarm as soon as its fault code is not 0. " +
                "The PLC's own alarm for the same axis or inverter is then not listed twice."));
            var delayLabel = new Label
            {
                AutoSize = true, Margin = new Padding(3, 6, 3, 0),
                Text = LanguageManager.GetString("Set_AlarmGenDelay", "Cylinder alarm delay (s):")
            };
            _genDelay = new NumericUpDown
            {
                Name = "GenerationDelay", Width = 70, DecimalPlaces = 1, Increment = 0.5M,
                Minimum = (decimal)AlarmGeneration.MinimumDelaySeconds, Maximum = 600,
                Value = (decimal)Math.Max(AlarmGeneration.MinimumDelaySeconds, Math.Min(600, gen.DelaySeconds))
            };
            Kaban.UI.Controls.UiStyle.SetTip(tips, _genDelay, LanguageManager.GetString("Tip_AlarmGenDelay",
                "How long a movement may take before the HMI raises its alarm - for every alarm whose Delay cell is blank."));
            var hint = new Label
            {
                AutoSize = true, Margin = new Padding(12, 6, 3, 0), ForeColor = Color.FromArgb(100, 108, 120),
                Text = LanguageManager.GetString("Set_AlarmGenHint", "A cylinder alarm's own delay: the Cylinder delay column (blank = this one).")
            };
            _genPanel.Controls.Add(_genAxes);
            _genPanel.Controls.Add(_genOn);
            _genPanel.Controls.Add(delayLabel);
            _genPanel.Controls.Add(_genDelay);
            _genPanel.Controls.Add(hint);
            _genOn.CheckedChanged += (s, e) => { _dirty.MarkDirty(); ShowGeneration(); };
            _genAxes.CheckedChanged += (s, e) => _dirty.MarkDirty();
            _genDelay.ValueChanged += (s, e) => _dirty.MarkDirty();

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
            { Name = "Address", HeaderText = LanguageManager.GetString("Col_Address", "Address"), FillWeight = 12, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Text", HeaderText = LanguageManager.GetString("Alarm_Col_Description", "Alarm"), FillWeight = 42, ReadOnly = true });

            var sev = new DataGridViewComboBoxColumn
            { Name = "Severity", HeaderText = LanguageManager.GetString("Alarm_Col_Severity", "Severity"), FillWeight = 12 };
            sev.Items.AddRange(Enum.GetNames(typeof(AlarmSeverity)));
            _grid.Columns.Add(sev);

            // A cylinder movement's own delay for the alarm the HMI raises (blank:
            // the general delay above). Only for an alarm a unit's movement reports.
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Delay",
                HeaderText = LanguageManager.GetString("Set_AlarmColDelay", "Cylinder delay (s)"),
                FillWeight = 8,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });


            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Helper",
                // SEVERAL: a manual, a drawing, a page - "; " between them. Typed
                // here or kept as a list (double click).
                HeaderText = LanguageManager.GetString("Col_HelperLinks", "Helper links (files, pages or URLs, double click for the list)"),
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
            _grid.CellDoubleClick += (s, e) => { if (e.ColumnIndex == _grid.Columns["Helper"].Index) EditHelpers(e.RowIndex); };
            // A delay is a number of seconds, or nothing.
            _grid.CellValidating += (s, e) =>
            {
                if (e.ColumnIndex != _grid.Columns["Delay"].Index || _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ReadOnly) return;
                double d;
                var text = Convert.ToString(e.FormattedValue).Trim();
                if (text.Length == 0 || (TryDelay(text, out d) && d >= AlarmGeneration.MinimumDelaySeconds && d <= 600)) { _status.Text = ""; return; }
                _status.Text = string.Format(LanguageManager.GetString("Set_AlarmDelayBad", "A delay is a number of seconds from {0} to 600, or blank for the general delay."),
                                             AlarmGeneration.MinimumDelaySeconds.ToString(System.Globalization.CultureInfo.CurrentCulture));
                e.Cancel = true;
            };

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
            _status.Font = Kaban.UI.Controls.UiStyle.Small;

            // ---- the machine's log from its PLC (30 October: "visible when HMI industry 4 logging
            // activated ... change the alarms setting page name to 'Alarms and Logs' and put the setting
            // on the page"): under the alarms the HMI raises, while HMI Settings > Features has it on
            _log = new Kaban.UI.Dashboard.MachineLogSettings(string.IsNullOrEmpty(moduleId) ? AlarmGeneration.ModuleOfStation(1) : moduleId);
            _log.Edited += (s, e) => _dirty.MarkDirty();

            Controls.Add(_grid);
            Controls.Add(_status);
            Controls.Add(_log);
            Controls.Add(_genPanel);
            Controls.Add(bar);

            LoadRows();
        }

        private readonly Kaban.UI.Dashboard.MachineLogSettings _log;

        /// <summary>The machine's log settings on the page (tests).</summary>
        public Kaban.UI.Dashboard.MachineLogSettings MachineLog { get { return _log; } }


        private readonly FlowLayoutPanel _genPanel;
        private readonly CheckBox _genOn, _genAxes;
        private readonly NumericUpDown _genDelay;

        /// <summary>The machine the generation settings are kept for (the main machine's for a page with none).</summary>
        private string GenerationModule
        {
            get { return !string.IsNullOrEmpty(_moduleId) ? _moduleId : AlarmGeneration.ModuleOfStation(1); }
        }

        private static bool TryDelay(string text, out double d)
        {
            return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out d) ||
                   double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d);
        }

        /// <summary>A cylinder unit's movement reports this alarm: the HMI can time it.</summary>
        private static bool Timed(AlarmDefinition def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.Address)) return false;
            int st = AlarmManager.StationOf(def);
            return AlarmGeneration.Cylinders.Any(c => c.Station == st &&
                c.Sides.Any(x => string.Equals(x.ErrorAddress, def.Address.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>The delay cells: editable on a movement's alarm, grey on the others; dimmed while the generation is off.</summary>
        private void ShowGeneration()
        {
            _genDelay.Enabled = true;
            var col = _grid.Columns["Delay"];
            if (col == null) return;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var cell = r.Cells["Delay"];
                bool timed = Timed(r.Tag as AlarmDefinition);
                cell.ReadOnly = !timed;
                cell.Style.BackColor = timed ? (_genOn.Checked ? Color.White : Color.FromArgb(248, 248, 248)) : Color.FromArgb(236, 238, 241);
                cell.Style.ForeColor = timed && _genOn.Checked ? _grid.DefaultCellStyle.ForeColor : Color.FromArgb(130, 136, 146);
                cell.ToolTipText = timed
                    ? LanguageManager.GetString("Tip_AlarmDelayCell", "This movement's delay before the HMI raises the alarm (seconds, blank = the general delay).")
                    : LanguageManager.GetString("Tip_AlarmDelayNone", "No cylinder unit's movement reports this alarm: the HMI does not time it.");
            }
        }

        private void LoadRows()
        {
            _grid.Rows.Clear();
            // Which alarms a unit's movement reports: known once the pages were searched.
            try { Kaban.UI.Helpers.AlarmHelpLinks.EnsureDiscovered(); } catch { }
            foreach (var def in AlarmManager.GetAllDefinitions().Where(Mine).OrderBy(d => d.Address))
            {
                // Straight from the definition - the only place these live now
                // that AlarmSettings.cfg is gone.
                string sev = def.Severity.ToString();
                string helper = JoinLinks(SplitLinks(def.HelperKeys));
                string delay = def.GenerationDelay.HasValue ? def.GenerationDelay.Value.ToString(System.Globalization.CultureInfo.CurrentCulture) : "";
                int row = _grid.Rows.Add(def.Address, LanguageManager.Localize(def.TranslationKey), sev, delay, helper);
                // The DEFINITION rides on the row. Save used to look alarms up by
                // address, and FA2010, H500 and H800 all use M801...: an edit on one
                // machine's page could land on another machine's alarm.
                _grid.Rows[row].Tag = def;
            }
            var gen = AlarmGeneration.For(GenerationModule);
            _genOn.Checked = gen.Enabled;
            _genAxes.Checked = gen.AxisAlarms;
            _genDelay.Value = (decimal)Math.Max(AlarmGeneration.MinimumDelaySeconds, Math.Min(600, gen.DelaySeconds));
            ShowGeneration();
            if (_log != null) _log.Fill();
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
                // the machine's log first: a log area that is no word device stops the save, said
                string logProblem;
                if (_log != null && HmiFeatures.LoggingOn && !_log.Save(out logProblem))
                {
                    _status.Text = logProblem;
                    return;
                }
                foreach (DataGridViewRow r in _grid.Rows)
                {
                    if (r.IsNewRow) continue;
                    var def = r.Tag as AlarmDefinition;
                    if (def == null) continue;

                    AlarmSeverity sev;
                    if (Enum.TryParse(Convert.ToString(r.Cells["Severity"].Value), out sev))
                        def.Severity = sev;

                    // Helper files only: which unit an alarm belongs to now comes
                    // from the unit itself (CylinderUnitHMI / AxisSettingHMI).
                    // A LIST: each link its own entry in Alarms.json. It used to be
                    // stored as ONE entry, so "a.pdf;b.pdf" was a file name nobody had.
                    def.HelperKeys = SplitLinks(new[] { Convert.ToString(r.Cells["Helper"].Value) });

                    // The movement's own delay for the HMI's alarm (blank: the general one).
                    double d;
                    var text = Convert.ToString(r.Cells["Delay"].Value ?? "").Trim();
                    def.GenerationDelay = text.Length > 0 && TryDelay(text, out d) && d > 0 ? Math.Max(AlarmGeneration.MinimumDelaySeconds, d) : (double?)null;
                }

                // The activation and the general delay, per machine.
                AlarmGeneration.Save(GenerationModule, new AlarmGeneration.Settings
                {
                    Enabled = _genOn.Checked, DelaySeconds = (double)_genDelay.Value, AxisAlarms = _genAxes.Checked
                });
                AuditLogger.Log("alarm generation for " + GenerationModule + ": cylinders " + (_genOn.Checked ? "on" : "off") + ", delay " + _genDelay.Value +
                                " s, axes " + (_genAxes.Checked ? "on" : "off") + ".");

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


        private Control _btnOpen;

        /// <summary>The links as a list: split at ";", trimmed, blanks and repeats dropped.</summary>
        public static List<string> SplitLinks(IEnumerable<string> entries)
        {
            var list = new List<string>();
            if (entries == null) return list;
            foreach (var e in entries)
                foreach (var part in (e ?? "").Split(';'))
                {
                    var t = part.Trim();
                    if (t.Length > 0 && !list.Contains(t, StringComparer.OrdinalIgnoreCase)) list.Add(t);
                }
            return list;
        }

        /// <summary>The list as the cell shows it: "manual.pdf; drawing.png".</summary>
        public static string JoinLinks(IEnumerable<string> links) { return string.Join("; ", links ?? new string[0]); }

        /// <summary>A path under the program's folder is stored relative to it (portable); others as chosen.</summary>
        public static string Portable(string path)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return path.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase) ? path.Substring(baseDir.Length) : path;
        }

        /// <summary>The row's helper links in a list: add files or links, remove, reorder.</summary>
        private void EditHelpers(int row)
        {
            if (row < 0 || row >= _grid.Rows.Count) return;
            var cell = _grid.Rows[row].Cells["Helper"];
            using (var dlg = new HelperLinksForm(Convert.ToString(_grid.Rows[row].Cells["Text"].Value),
                                                 SplitLinks(new[] { Convert.ToString(cell.Value) })))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                cell.Value = JoinLinks(dlg.Links);
            }
        }

        private void OpenHelper()
        {
            if (_grid.SelectedRows.Count == 0) return;
            var links = SplitLinks(new[] { Convert.ToString(_grid.SelectedRows[0].Cells["Helper"].Value) });
            if (links.Count == 0) { _status.Text = "No helper link on that alarm."; return; }
            if (links.Count == 1) { Open(links[0]); return; }
            // SEVERAL: which one.
            var menu = UiStyle.Menu();
            foreach (var l in links) { var link = l; menu.Items.Add(link, null, (s, e) => Open(link)); }
            menu.Show(_btnOpen, new Point(0, _btnOpen.Height));
        }

        private void Open(string helper)
        {
            string path = Path.IsPathRooted(helper) || helper.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? helper
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, helper);
            try { System.Diagnostics.Process.Start(path); }
            catch (Exception ex) { _status.Text = "Could not open: " + ex.Message; }
        }
    }

    /// <summary>
    /// An alarm's helper links as a list: files (stored relative to the program
    /// when under it), pages or URLs, in the order the alarm offers them.
    /// </summary>
    public sealed class HelperLinksForm : Form
    {
        private readonly ListBox _list = new ListBox();
        private readonly TextBox _link = new TextBox();

        public List<string> Links { get { return _list.Items.Cast<string>().ToList(); } }

        public HelperLinksForm(string alarm, IEnumerable<string> links)
        {
            Text = LanguageManager.GetString("Ttl_HelperLinks", "Helper links") + (string.IsNullOrWhiteSpace(alarm) ? "" : " - " + alarm);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 320);
            MinimumSize = new Size(420, 260);
            ShowInTaskbar = false;
            Font = UiStyle.Body;

            var note = new Label
            {
                Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 6, 8, 0),
                Text = LanguageManager.GetString("Lbl_HelperLinksNote", "The documents this alarm offers, in this order: files (a file in the program's folder is kept " +
                       "relative to it), pages or web addresses.")
            };
            _list.Dock = DockStyle.Fill;
            _list.IntegralHeight = false;
            _list.SelectionMode = SelectionMode.MultiExtended;
            foreach (var l in links ?? new string[0]) _list.Items.Add(l);

            var side = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 118, FlowDirection = FlowDirection.TopDown, Padding = new Padding(6) };
            Func<string, EventHandler, Button> btn = (text, click) =>
            {
                var b = UiStyle.TextButton(text, 104, click);
                b.Margin = new Padding(0, 0, 0, 6);
                side.Controls.Add(b);
                return b;
            };
            btn(LanguageManager.GetString("Btn_AddFiles", "Add files..."), (s, e) => AddFiles());
            btn(LanguageManager.GetString("Btn_Remove", "Remove"), (s, e) => RemoveSelected());
            btn(LanguageManager.GetString("Btn_Up", "Up"), (s, e) => Move(-1));
            btn(LanguageManager.GetString("Btn_Down", "Down"), (s, e) => Move(+1));

            var linkRow = new Panel { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(8, 6, 8, 4) };
            var addLink = UiStyle.TextButton(LanguageManager.GetString("Btn_AddLink", "Add link"), 104);
            addLink.Dock = DockStyle.Right;
            _link.Dock = DockStyle.Fill;
            addLink.Click += (s, e) => AddLink();
            _link.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { AddLink(); e.SuppressKeyPress = true; } };
            linkRow.Controls.Add(_link);
            linkRow.Controls.Add(addLink);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var cancel = UiStyle.DialogButton(LanguageManager.GetString("Btn_Cancel", "Cancel"), DialogResult.Cancel);
            var ok = UiStyle.DialogButton(LanguageManager.GetString("Btn_Ok", "OK"), DialogResult.OK);
            bottom.Controls.Add(cancel);
            bottom.Controls.Add(ok);
            CancelButton = cancel;

            Controls.Add(_list);
            Controls.Add(side);
            Controls.Add(note);
            Controls.Add(linkRow);
            Controls.Add(bottom);
            _list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };
        }

        /// <summary>Appends links not listed yet (tests, the file dialog and the link box).</summary>
        public void Add(IEnumerable<string> links)
        {
            foreach (var l in links)
            {
                var t = (l ?? "").Trim();
                if (t.Length > 0 && !_list.Items.Cast<string>().Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
                    _list.Items.Add(t);
            }
        }

        private void AddFiles()
        {
            using (var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Documents (*.pdf;*.png;*.jpg;*.txt;*.html)|*.pdf;*.png;*.jpg;*.txt;*.html|All files (*.*)|*.*"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Add(dlg.FileNames.Select(AlarmSettingsControl.Portable));
            }
        }

        private void AddLink()
        {
            // SEVERAL AT ONCE: "a.pdf; b.pdf" adds both.
            Add(AlarmSettingsControl.SplitLinks(new[] { _link.Text }));
            _link.Clear();
        }

        private void RemoveSelected()
        {
            foreach (var i in _list.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList()) _list.Items.RemoveAt(i);
        }

        private void Move(int d)
        {
            if (_list.SelectedIndices.Count != 1) return;
            int i = _list.SelectedIndex, j = i + d;
            if (j < 0 || j >= _list.Items.Count) return;
            var item = _list.Items[i];
            _list.Items.RemoveAt(i);
            _list.Items.Insert(j, item);
            _list.SelectedIndex = j;
        }
    }

    /// <summary>Alarm severity and helper links - a tab of Machine settings.</summary>
    public class AlarmSettingsPage : ISettingsPage
    {
        private readonly string _moduleId;
        public AlarmSettingsPage() : this(null) { }
        public AlarmSettingsPage(Kaban.Core.Machines.IMachineMountContext ctx) { _moduleId = ctx == null ? null : ctx.ModuleId; }
        // 30 October: "We can change the alarms setting page name to 'Alarms and Logs'"
        public string TitleKey { get { return "Set_AlarmsAndLogs"; } }
        public string TitleFallback { get { return "Alarms and Logs"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Maintenance; } }
        public UserControl GetControl() { return new AlarmSettingsControl(_moduleId); }
    }
}

