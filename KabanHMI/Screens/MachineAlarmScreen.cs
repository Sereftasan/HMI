using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.Core.Models;
using Kaban.UI.Controls;

namespace Kaban.UI.Screens
{
    /// <summary>
    /// The alarm page for ONE machine: the alarms it currently has, its own reset,
    /// and its own history. Shared by every machine (main and sub) so the page is
    /// identical wherever it appears - the shell shows one Alarms nav entry with a
    /// tab per machine.
    ///
    /// On the "one table vs a tab per machine" question: a tab per machine wins for
    /// day-to-day work, because acknowledging and resetting are per-machine actions
    /// and mixing several machines' alarms in one grid makes it easy to reset the
    /// wrong one. The HISTORY tab still shows everything together with a Machine
    /// column, which is what you want when tracing a fault across machines.
    /// </summary>
    public class MachineAlarmScreen : UserControl
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly System.ComponentModel.BindingList<AlarmItem> _view =
            new System.ComponentModel.BindingList<AlarmItem>();
        // Multi-level sort: the newest click LEADS and the earlier ones stay in
        // effect underneath, so an operator can sort by severity and still have
        // equal severities ordered by time. Only the leading column shows an arrow -
        // the same convention as the profile table.
        private readonly List<KeyValuePair<string, bool>> _sortKeys =
            new List<KeyValuePair<string, bool>>();
        private readonly IMachineMountContext _ctx;
        private readonly Label _empty = new Label();

        public MachineAlarmScreen(IMachineMountContext ctx)
        {
            _ctx = ctx;
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
            add(UiStyle.ToolButton("\u2714", LanguageManager.GetString("Btn_Ack", "Acknowledge the listed alarms"),
                                   tips, (s, e) => Acknowledge()));
            // THE MACHINE'S OWN RESET BIT, as its module declares it (see
            // AlarmReset). It was M102 for every machine; that is FA2010 PLC1's
            // reset only, and on a mounted H500 it started the lubrication.
            int station = _ctx != null ? _ctx.Station(1) : 1;
            string why;
            string resetBit = Kaban.UI.Helpers.AlarmReset.AddressFor(station, out why);
            if (resetBit != null)
            {
                // A REAL ButtonHMI, not a toolbar button calling the driver directly.
                //
                // The hand-rolled version wrote the bit itself, which meant it missed
                // everything a bound button does for free: the station mapping for a
                // mounted sub-machine, the security check, the audit entry, the
                // disabled look when the PLC is unreachable - and the tooltip that
                // tells an admin WHICH DEVICE it drives.
                _btnReset = new ButtonHMI
                {
                    // The machine's OWN controller: station 3 for an H500 mounted under FA2010.
                    PlcNumber = station,
                    FallbackText = "\uD83D\uDD04",
                    Width = 36,
                    Height = 30,
                    RequiredSecurity = SecurityLevel.Operator,
                    LogAction = true
                };
                // As the machine's module declares it: FA2010's M102 is SET (the
                // PLC switches it off when the reset is done), others a pulse.
                _btnReset.BitActions.Add(new Kaban.Core.Models.BitAction
                {
                    Address = resetBit,
                    Action = PlcManager.Instance.AlarmResetAction(station)
                });
                _btnReset.ApplyTranslation();
                // The button's OWN tooltip: "Reset alarms" - and, for an admin, the
                // device it drives and how (a tooltip set over it hid that).
                _btnReset.ToolTipText = "Btn_AlarmReset";
                add(_btnReset);
            }
            else
            {
                // No bit declared: a button that SAYS so, rather than one that
                // pulses somebody else's device or silently does nothing.
                var none = UiStyle.ToolButton("\uD83D\uDD04", LanguageManager.GetString("Btn_AlarmReset", "Reset alarms") +
                                              " - " + why, tips, (s, e) =>
                    MessageBox.Show(FindForm(), LanguageManager.GetString("Btn_AlarmReset", "Reset alarms") + ": " + why + ".",
                                    LanguageManager.GetString("Ttl_AlarmReset", "Alarm reset"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Information));
                none.ForeColor = Color.FromArgb(150, 156, 166);
                add(none);
            }

            _grid.Dock = DockStyle.Fill;
            // Column captions need room UNDER the text: a header sized to the
            // cap height slices through g, p and y.
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 5);
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoGenerateColumns = false;
            // EXPLICIT widths, saved and restored - a Fill grid recomputes every
            // width from weights, so a width the operator sets could never survive
            // a restart.
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.AllowUserToResizeColumns = true;
            // 25% wider. The headings were being clipped to an ellipsis, so the
            // operator had to drag every column before the table could be read -
            // and a table that needs setting up before it can be read is one that
            // gets ignored during the minutes when it matters most.
            // The code as it is shown: "M840.2" for an alarm the HMI raised itself
            // (its device and which signal failed), the alarm's code otherwise.
            AddColumn("AlarmCode", "Code", 138, "ShownCode");
            AddColumn("Description", "Alarm", 575);
            AddColumn("Severity", "Severity", 138);
            AddColumn("TriggerTime", "Time", 200);
            AddHelpColumn();

            // Fitted AFTER every column exists, and on the grid that has them. The
            // earlier call sat in the history-grid builder, so the main table was
            // never touched.
            Kaban.UI.Controls.UiStyle.EnableAutoFit(_grid);
            UiStyle.EnableTableTools(_grid, "AlarmList");
            _grid.DataSource = _view;
            _grid.ColumnWidthChanged += (s, e) => SaveColumnWidths();
            _grid.ColumnHeaderMouseClick += (s, e) => SortBy(_grid.Columns[e.ColumnIndex].Name);
            // Named and removed on dispose: the list is static and outlives the page.
            AlarmManager.ActiveAlarms.ListChanged += OnActiveAlarmsChanged;
            Disposed += (s, e) => AlarmManager.ActiveAlarms.ListChanged -= OnActiveAlarmsChanged;
            Refresh_();

            _empty.Text = LanguageManager.GetString("Alarm_None", "No active alarms.");
            _empty.Dock = DockStyle.Top;
            _empty.Height = 28;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.ForeColor = Color.FromArgb(120, 128, 138);

            var active = new Panel { Dock = DockStyle.Fill };
            active.Controls.Add(_grid);
            active.Controls.Add(_empty);
            active.Controls.Add(bar);

            // ACTIVE and HISTORY as tabs: the operator wants what is wrong NOW, the
            // engineer wants what has been wrong. Same page, same machine.
            var tabs = new Kaban.UI.Settings.SettingsScreen(new Kaban.UI.Settings.ISettingsPage[]
            {
                new Kaban.UI.Settings.SimplePage("Tab_AlarmsActive", "Active",
                    SecurityLevel.None, () => Wrap(active)),
                new Kaban.UI.Settings.SimplePage("Tab_AlarmsHistory", "History",
                    SecurityLevel.None, () => BuildHistory(_ctx == null ? null : _ctx.ModuleId))
            });
            tabs.Dock = DockStyle.Fill;
            Controls.Add(tabs);

            HandleCreated += (s, e) => LoadColumnWidths();
            UpdateEmpty();
        }

        private static UserControl Wrap(Control content)
        {
            var host = new UserControl { Dock = DockStyle.Fill };
            content.Dock = DockStyle.Fill;
            host.Controls.Add(content);
            return host;
        }

        /// <summary>
        /// Everything that HAS been active, newest first, with the time it appeared.
        /// Read-only: history is a record, not something to edit.
        /// </summary>
        private static UserControl BuildHistory(string moduleId)
        {
            var host = new UserControl { Dock = DockStyle.Fill, BackColor = Color.White };
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AllowUserToResizeColumns = true
            };
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 5);
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Time", Width = 160 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", HeaderText = "Code", Width = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Alarm", HeaderText = "Alarm", Width = 460 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Severity", HeaderText = "Severity", Width = 138 });
            Kaban.UI.Controls.UiStyle.EnableAutoFit(grid);
            // The history table was left out: it is the one people scroll through
            // looking for a pattern, so sorting and hiding matter there most.
            Kaban.UI.Controls.UiStyle.EnableTableTools(grid, "AlarmHistory");

            var empty = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(120, 128, 138),
                Text = LanguageManager.GetString("Alarm_NoHistory", "No alarms have been recorded yet.")
            };

            Action fill = () =>
            {
                grid.Rows.Clear();
                // This machine's history only - the same rule as the active list.
                var items = new List<AlarmItem>(AlarmManager.History.Where(a =>
                {
                    if (string.IsNullOrEmpty(moduleId) || a == null || string.IsNullOrEmpty(a.AlarmCode)) return true;
                    int dot = a.AlarmCode.IndexOf('.');
                    return dot <= 0 || string.Equals(a.AlarmCode.Substring(0, dot), moduleId, StringComparison.OrdinalIgnoreCase);
                }));
                items.Reverse();                                  // newest first
                foreach (var a in items)
                    grid.Rows.Add(a.TriggerTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                  a.ShownCode, a.Description, a.Severity);
                empty.Visible = grid.Rows.Count == 0;
                grid.Visible = grid.Rows.Count > 0;
            };

            var tips = Kaban.UI.Controls.UiStyle.Tips();
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, BackColor = Color.White };
            bar.Controls.Add(UiStyle.ToolButton("\uD83D\uDD04",
                LanguageManager.GetString("Btn_Refresh", "Refresh the history"), tips, (s, e) => fill()));
            bar.Controls.Add(UiStyle.ToolButton("\uD83D\uDCBE",
                LanguageManager.GetString("Btn_Export", "Save the history to a file"), tips, (s, e) =>
                {
                    using (var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "alarm_history.csv" })
                        if (dlg.ShowDialog(host.FindForm()) == DialogResult.OK)
                            try
                            {
                                var sb = new System.Text.StringBuilder("Time;Code;Alarm;Severity\r\n");
                                foreach (DataGridViewRow r in grid.Rows)
                                    sb.AppendFormat("{0};{1};{2};{3}\r\n", r.Cells[0].Value, r.Cells[1].Value,
                                                    r.Cells[2].Value, r.Cells[3].Value);
                                System.IO.File.WriteAllText(dlg.FileName, sb.ToString());
                            }
                            catch { }
                }));

            host.Controls.Add(grid);
            host.Controls.Add(empty);
            host.Controls.Add(bar);
            fill();
            return host;
        }

        /// <summary>
        /// A button per row that opens the documents for that alarm.
        ///
        /// A column rather than a menu or a detail pane: the operator is standing
        /// at a stopped machine, and the shortest path from "this alarm" to "what
        /// do I do" is a button on the same line. Rows with no document show
        /// nothing, so the column does not promise help that is not there.
        /// </summary>
        private void AddHelpColumn()
        {
            // A TEXT CELL THAT OPENS A LIST, not a grid combo.
            //
            // This grid is read-only - it has to be, nobody should be able to type
            // into an alarm - and a combo cell in a read-only grid never opens. So
            // choosing a servo or cylinder document was simply impossible, and
            // nothing on screen said why. The cell now shows the first document
            // with a dropdown arrow, and a click opens the same kind of list every
            // other dropdown in the program uses. It reads as a combo and behaves
            // like one, without depending on the grid being editable.
            var col = new DataGridViewTextBoxColumn
            {
                Name = "Help",
                HeaderText = LanguageManager.GetString("Alarm_Col_Help", "Help"),
                Width = 240,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Padding = new Padding(0, 0, 18, 0);   // room for the arrow
            _grid.Columns.Add(col);
            _baseHeaders["Help"] = col.HeaderText;

            // What the cell shows: the first document, and how many more there are.
            _grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (_grid.Columns[e.ColumnIndex].Name != "Help") return;
                var docs = HelpDocumentsFor(e.RowIndex);
                e.Value = docs.Count == 0 ? "" :
                          docs[0].Title + (docs.Count > 1 ? "  (+" + (docs.Count - 1) + ")" : "");
                e.FormattingApplied = true;
            };

            // The dropdown arrow - only on rows that actually have something to open.
            _grid.CellPainting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (_grid.Columns[e.ColumnIndex].Name != "Help") return;
                if (HelpDocumentsFor(e.RowIndex).Count == 0) return;

                e.Paint(e.CellBounds, DataGridViewPaintParts.All);
                var r = e.CellBounds;
                int cx = r.Right - 11, cy = r.Top + r.Height / 2;
                using (var b = new SolidBrush(Color.FromArgb(90, 98, 110)))
                    e.Graphics.FillPolygon(b, new[]
                    {
                        new Point(cx - 4, cy - 2), new Point(cx + 4, cy - 2), new Point(cx, cy + 3)
                    });
                e.Handled = true;
            };

            // CellClick fires in a read-only grid; editing never did.
            _grid.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (_grid.Columns[e.ColumnIndex].Name != "Help") return;
                ShowHelpFor(e.RowIndex, e.ColumnIndex);
            };

            // A hand over a cell that opens something, an arrow everywhere else.
            _grid.CellMouseEnter += (s, e) =>
            {
                bool clickable = e.RowIndex >= 0 && e.ColumnIndex >= 0 &&
                                 _grid.Columns[e.ColumnIndex].Name == "Help" &&
                                 HelpDocumentsFor(e.RowIndex).Count > 0;
                _grid.Cursor = clickable ? Cursors.Hand : Cursors.Default;
            };
            _grid.MouseLeave += (s, e) => _grid.Cursor = Cursors.Default;
        }

        /// <summary>Every document configured for the alarm on a row.</summary>
        private List<TroubleshootingCatalog.HelpDocument> HelpDocumentsFor(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count) return new List<TroubleshootingCatalog.HelpDocument>();

            // The grid holds RAISED alarms (AlarmItem); what the alarm is linked to
            // lives on its DEFINITION. Its unit's manual comes from the unit on the
            // machine's pages, found at start-up; its own helper files follow.
            var item = _grid.Rows[rowIndex].DataBoundItem as AlarmItem;
            if (item == null) return new List<TroubleshootingCatalog.HelpDocument>();

            // CACHED BRIEFLY. This is asked by CellFormatting, CellPainting and
            // CellMouseEnter - for every row, on every repaint - and each answer
            // probes the disk several times per document (language variants).
            // Two seconds is short enough that a document copied in by hand shows
            // up almost at once, and long enough that painting never touches disk.
            string key = (item.AlarmCode ?? "") + "|" + LanguageManager.CurrentLanguage;
            CachedHelp hit;
            int now = Environment.TickCount;
            if (_helpCache.TryGetValue(key, out hit) && now - hit.Tick < HelpCacheMs) return hit.Docs;

            var docs = Kaban.UI.Helpers.AlarmHelpLinks.DocumentsFor(item);
            if (_helpCache.Count > 200) _helpCache.Clear();     // bounded
            _helpCache[key] = new CachedHelp { Tick = now, Docs = docs };
            return docs;
        }

        private const int HelpCacheMs = 2000;
        private sealed class CachedHelp { public int Tick; public List<TroubleshootingCatalog.HelpDocument> Docs; }
        private readonly Dictionary<string, CachedHelp> _helpCache = new Dictionary<string, CachedHelp>();

        /// <summary>Opens the document list under the cell, like a combo.</summary>
        private void ShowHelpFor(int rowIndex, int columnIndex)
        {
            var docs = HelpDocumentsFor(rowIndex);
            if (docs.Count == 0) return;

            var menu = BuildHelpMenu(docs, OpenDocument);
            var cell = _grid.GetCellDisplayRectangle(columnIndex, rowIndex, false);
            UiStyle.ShowPopup(menu, _grid, new Point(cell.Left, cell.Bottom));
        }

        /// <summary>
        /// The document list for one alarm, built like every other dropdown.
        ///
        /// Always a list, even for one document: the cell looks like a combo, and
        /// a combo that sometimes opens a file and sometimes opens a list is two
        /// behaviours to learn. A PDF opens at page one - a section cannot be
        /// passed to the reader through the shell - so the section is named in the
        /// entry, telling the operator where to turn.
        /// </summary>
        public static ContextMenuStrip BuildHelpMenu(IList<TroubleshootingCatalog.HelpDocument> docs,
                                                     Action<TroubleshootingCatalog.HelpDocument> open)
        {
            var menu = UiStyle.Menu();
            menu.Items.Add(UiStyle.MenuHeader(LanguageManager.GetString("Ttl_HelpDocs", "Help Documents")));
            menu.Items.Add(new ToolStripSeparator());

            foreach (var doc in docs)
            {
                var d = doc;
                var item = new ToolStripMenuItem(doc.Title +
                    (string.IsNullOrEmpty(doc.Section) ? "" : "   \u00A7" + doc.Section));
                item.Click += (s, e) => open(d);
                menu.Items.Add(item);
            }
            return menu;
        }

        private void OpenDocument(TroubleshootingCatalog.HelpDocument doc)
        {
            // One way to open help, shared with the "?" on the units.
            Kaban.UI.Helpers.AlarmHelpLinks.Open(this, doc);
        }

        private void AddColumn(string prop, string caption, int width, string dataProperty = null)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = prop,
                DataPropertyName = dataProperty ?? prop,
                HeaderText = LanguageManager.GetString("Alarm_Col_" + prop, caption),
                Width = width,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            _baseHeaders[prop] = LanguageManager.GetString("Alarm_Col_" + prop, caption);
        }

        /// <summary>Click a header to sort; click again to reverse.</summary>
        private void SortBy(string column)
        {
            if (string.IsNullOrEmpty(column)) return;

            int existing = _sortKeys.FindIndex(k => k.Key == column);
            bool ascending = (existing == 0) ? !_sortKeys[0].Value
                                             : (existing > 0 ? _sortKeys[existing].Value : true);
            if (existing >= 0) _sortKeys.RemoveAt(existing);
            _sortKeys.Insert(0, new KeyValuePair<string, bool>(column, ascending));
            if (_sortKeys.Count > 4) _sortKeys.RemoveRange(4, _sortKeys.Count - 4);

            Refresh_();
            UpdateHeaderArrows();
        }

        /// <summary>Only the column sorted LAST carries the arrow.</summary>
        private void UpdateHeaderArrows()
        {
            string leading = _sortKeys.Count > 0 ? _sortKeys[0].Key : null;
            bool asc = _sortKeys.Count > 0 && _sortKeys[0].Value;
            foreach (DataGridViewColumn c in _grid.Columns)
            {
                string plain;
                if (!_baseHeaders.TryGetValue(c.Name, out plain)) continue;
                c.HeaderText = (c.Name == leading) ? plain + (asc ? "  \u25B2" : "  \u25BC") : plain;
            }
        }

        private readonly Dictionary<string, string> _baseHeaders =
            new Dictionary<string, string>();

        /// <summary>
        /// True when an alarm was raised by the module this page belongs to.
        ///
        /// Matches the "ModuleId." prefix the loader puts on every code. An alarm
        /// with no prefix at all predates scoping and is shown everywhere rather
        /// than hidden from every page, because an unattributed fault the operator
        /// cannot see is worse than one shown twice.
        /// </summary>
        private bool BelongsToThisMachine(AlarmItem alarm)
        {
            if (alarm == null || string.IsNullOrEmpty(alarm.AlarmCode)) return true;
            int dot = alarm.AlarmCode.IndexOf('.');
            if (dot <= 0) return true;
            return string.Equals(alarm.AlarmCode.Substring(0, dot),
                                 _ctx.ModuleId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Rebuilds the visible list from the live alarms, in sort order.</summary>
        private void OnActiveAlarmsChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
        {
            if (!IsDisposed) Refresh_();
        }

        private void Refresh_()
        {
            // ONLY THIS MACHINE'S ALARMS.
            //
            // Alarm codes are already scoped by module ("FA2010.Emergency_Stop"),
            // but the screen listed every active alarm in the program - so the
            // screwing unit's page showed the machining centre's faults and an
            // operator at H500 was reading about a machine across the shop.
            //
            // A page with no module context (a standalone diagnostic view) still
            // shows everything, which is what it is for.
            var items = new List<AlarmItem>(
                _ctx == null || string.IsNullOrEmpty(_ctx.ModuleId)
                    ? AlarmManager.ActiveAlarms
                    : AlarmManager.ActiveAlarms.Where(a => BelongsToThisMachine(a)));
            if (_sortKeys.Count > 0)
            {
                // compare on the leading key, falling back to the earlier ones -
                // that is what makes the previous sort survive
                items.Sort((a, b) =>
                {
                    foreach (var key in _sortKeys)
                    {
                        int r = CompareBy(key.Key, a, b);
                        if (!key.Value) r = -r;
                        if (r != 0) return r;
                    }
                    return 0;
                });
            }
            _view.RaiseListChangedEvents = false;
            _view.Clear();
            foreach (var it in items) _view.Add(it);
            _view.RaiseListChangedEvents = true;
            _view.ResetBindings();
            UpdateEmpty();
        }

        private static int CompareBy(string column, AlarmItem a, AlarmItem b)
        {
            switch (column)
            {
                case "AlarmCode": return string.Compare(a.ShownCode, b.ShownCode, StringComparison.OrdinalIgnoreCase);
                case "Severity": return a.Severity.CompareTo(b.Severity);
                case "TriggerTime": return a.TriggerTime.CompareTo(b.TriggerTime);
                default: return string.Compare(a.Description, b.Description, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void UpdateEmpty()
        {
            // Hide the GRID as well, not just show a message beside it: an empty
            // table with headers and no rows reads as something being broken.
            bool none = _view.Count == 0;
            _empty.Visible = none;
            _grid.Visible = !none;
        }

        private void Acknowledge()
        {
            if (!SecurityManager.HasAccess(SecurityLevel.Operator)) return;
            AlarmManager.AcknowledgeAll();
            AuditLogger.Log("alarms acknowledged" + (_ctx == null ? "" : " on " + _ctx.ModuleId) + ".");
        }

        private ButtonHMI _btnReset;

        // ---- column widths survive a restart ----
        private string WidthsPath
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "KabanHMI", "AlarmColumns.cfg");
            }
        }

        private void SaveColumnWidths()
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(WidthsPath));
                var parts = _grid.Columns.Cast<DataGridViewColumn>()
                                 .Select(c => c.Name + "=" + c.Width);
                System.IO.File.WriteAllText(WidthsPath, string.Join(";", parts));
            }
            catch { }
        }

        private void LoadColumnWidths()
        {
            try
            {
                if (!System.IO.File.Exists(WidthsPath)) return;
                foreach (var part in System.IO.File.ReadAllText(WidthsPath).Split(';'))
                {
                    var kv = part.Split('=');
                    int w;
                    if (kv.Length != 2 || !int.TryParse(kv[1], out w) || w < 30) continue;
                    var col = _grid.Columns[kv[0]];
                    if (col != null) col.Width = w;
                }
            }
            catch { }
        }
    }
}

