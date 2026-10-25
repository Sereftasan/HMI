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
            // THE SAME COLUMNS AS THE HISTORY (8 October): the alarm's code, its own text,
            // what failed (Cause), severity, time, the PLC device (Admin), its help.
            AddAlarmColumns(_grid, _baseHeaders);
            AddHelpColumnTo(_grid);

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
        /// Read-only: history is a record, not something to edit. The same columns
        /// as the list of active alarms, its help too (8 October).
        /// </summary>
        private UserControl BuildHistory(string moduleId)
        {
            var host = new UserControl { Dock = DockStyle.Fill, BackColor = Color.White };
            var list = new System.ComponentModel.BindingList<AlarmItem>();
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AllowUserToResizeColumns = true,
                AutoGenerateColumns = false,
                Name = "historyGrid"
            };
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 5);
            var headers = new Dictionary<string, string>();
            AddAlarmColumns(grid, headers);
            AddHelpColumnTo(grid);
            Kaban.UI.Controls.UiStyle.EnableAutoFit(grid);
            // The history table was left out: it is the one people scroll through
            // looking for a pattern, so sorting and hiding matter there most.
            Kaban.UI.Controls.UiStyle.EnableTableTools(grid, "AlarmHistory");
            grid.DataSource = list;

            var empty = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(120, 128, 138),
                Text = LanguageManager.GetString("Alarm_NoHistory", "No alarms have been recorded yet.")
            };

            // newest first, or as a heading click sorts it
            var sortKeys = new List<KeyValuePair<string, bool>>();
            Action fill = () =>
            {
                // This machine's history only - the same rule as the active list.
                var items = new List<AlarmItem>(AlarmManager.History.Where(a =>
                {
                    if (string.IsNullOrEmpty(moduleId) || a == null || string.IsNullOrEmpty(a.AlarmCode)) return true;
                    int dot = a.AlarmCode.IndexOf('.');
                    return dot <= 0 || string.Equals(a.AlarmCode.Substring(0, dot), moduleId, StringComparison.OrdinalIgnoreCase);
                }));
                items.Reverse();                                  // newest first
                if (sortKeys.Count > 0)
                {
                    // stable: equal ones stay newest first
                    var order = items.Select((x, i) => new KeyValuePair<AlarmItem, int>(x, i)).ToList();
                    order.Sort((p1, p2) =>
                    {
                        foreach (var key in sortKeys)
                        {
                            int r = CompareBy(key.Key, p1.Key, p2.Key);
                            if (!key.Value) r = -r;
                            if (r != 0) return r;
                        }
                        return p1.Value.CompareTo(p2.Value);
                    });
                    items = order.Select(p => p.Key).ToList();
                }
                list.RaiseListChangedEvents = false;
                list.Clear();
                foreach (var it in items) list.Add(it);
                list.RaiseListChangedEvents = true;
                list.ResetBindings();
                empty.Visible = list.Count == 0;
                grid.Visible = list.Count > 0;
            };
            grid.ColumnHeaderMouseClick += (s, e) =>
            {
                var column = grid.Columns[e.ColumnIndex].Name;
                if (column == "Help") return;
                int existing = sortKeys.FindIndex(k => k.Key == column);
                bool ascending = existing == 0 ? !sortKeys[0].Value : (existing > 0 ? sortKeys[existing].Value : true);
                if (existing >= 0) sortKeys.RemoveAt(existing);
                sortKeys.Insert(0, new KeyValuePair<string, bool>(column, ascending));
                if (sortKeys.Count > 4) sortKeys.RemoveRange(4, sortKeys.Count - 4);
                fill();
                foreach (DataGridViewColumn c in grid.Columns)
                {
                    string plain;
                    if (!headers.TryGetValue(c.Name, out plain)) continue;
                    c.HeaderText = c.Name == column ? plain + (ascending ? "  \u25B2" : "  \u25BC") : plain;
                }
            };

            var tips = Kaban.UI.Controls.UiStyle.Tips();
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, BackColor = Color.White };
            bar.Controls.Add(UiStyle.ToolButton("\uD83D\uDD04",
                LanguageManager.GetString("Btn_Refresh", "Refresh the history"), tips, (s, e) => fill()));
            bar.Controls.Add(UiStyle.ToolButton("\uD83D\uDCE4",
                LanguageManager.GetString("Btn_Export", "Save the history to a file"), tips, (s, e) =>
                {
                    using (var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "alarm_history.csv" })
                        if (dlg.ShowDialog(host.FindForm()) == DialogResult.OK)
                            try { System.IO.File.WriteAllText(dlg.FileName, HistoryCsv(list)); }
                            catch { }
                }));

            host.Controls.Add(grid);
            host.Controls.Add(empty);
            host.Controls.Add(bar);
            fill();
            // Written again in the new date and time format when View Preferences change.
            UiStyle.OnViewPreferences(host, fill);
            return host;
        }

        /// <summary>The history as a file: the table's columns (the Address for an Admin only).</summary>
        public static string HistoryCsv(IEnumerable<AlarmItem> items)
        {
            bool admin = SecurityManager.HasAccess(SecurityLevel.Admin);
            Func<string, string> c = v => (v ?? "").Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
            var sb = new System.Text.StringBuilder("Time;Code;Alarm;Cause;Severity" + (admin ? ";Address" : "") + "\r\n");
            foreach (var a in items)
                sb.Append(c(Kaban.Core.Helpers.ViewPreferences.DateTimeText(a.TriggerTime))).Append(';')
                  .Append(c(a.ListCode)).Append(';').Append(c(a.Text)).Append(';').Append(c(a.Cause)).Append(';')
                  .Append(a.Severity).Append(admin ? ";" + c(a.AddressShown) : "").Append("\r\n");
            return sb.ToString();
        }

        /// <summary>
        /// THE ALARM COLUMNS, the list's and its history's alike (8 October: they had
        /// drifted apart): Code - the alarm's code, the same kind for every alarm;
        /// Alarm - its own text; Cause - what failed, for an alarm the HMI raised
        /// itself (it was put after the text); Severity; Time; Address - the PLC
        /// device and, for a generated alarm, which failure (M840.2) - for an Admin only.
        /// </summary>
        private static void AddAlarmColumns(DataGridView grid, Dictionary<string, string> headers)
        {
            Action<string, string, int, string> add = (name, caption, width, prop) =>
            {
                var text = LanguageManager.GetString("Alarm_Col_" + name, caption);
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = name, DataPropertyName = prop, HeaderText = text, Width = width,
                    SortMode = DataGridViewColumnSortMode.Programmatic
                });
                headers[name] = text;
            };
            add("AlarmCode", "Code", 190, "ListCode");
            add("Description", "Alarm", 420, "Text");
            add("Cause", "Cause", 320, "Cause");
            add("Severity", "Severity", 110, "Severity");
            add("TriggerTime", "Time", 170, "TriggerTime");
            add("Address", "Address", 110, "AddressShown");
            // The date and time as View Preferences say (it was Windows' own), and again when they change.
            UiStyle.DateColumn(grid.Columns["TriggerTime"]);
            // the PLC device: an internal device, for an Admin only - and again at every sign-in
            Action admin = () =>
            {
                var col = grid.Columns["Address"];
                if (col != null && !grid.IsDisposed) col.Visible = SecurityManager.HasAccess(SecurityLevel.Admin);
            };
            admin();
            EventHandler onUser = (s, e) =>
            {
                if (grid.IsDisposed) return;
                if (grid.InvokeRequired) { try { grid.BeginInvoke(admin); } catch { } return; }
                admin();
            };
            SecurityManager.UserChanged += onUser;
            grid.Disposed += (s, e) => SecurityManager.UserChanged -= onUser;
            // after the table's kept layout is put back (it would show it again)
            grid.HandleCreated += (s, e) => { try { grid.BeginInvoke(admin); } catch { } };
        }

        /// <summary>The alarm on a row of either grid.</summary>
        private static AlarmItem ItemAt(DataGridView grid, int rowIndex)
        {
            return rowIndex >= 0 && rowIndex < grid.Rows.Count ? grid.Rows[rowIndex].DataBoundItem as AlarmItem : null;
        }

        /// <summary>
        /// A button per row that opens the documents for that alarm.
        ///
        /// A column rather than a menu or a detail pane: the operator is standing
        /// at a stopped machine, and the shortest path from "this alarm" to "what
        /// do I do" is a button on the same line. Every alarm has one since 8
        /// October: its AI prompt is the list's last entry.
        /// </summary>
        private void AddHelpColumnTo(DataGridView grid)
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
            grid.Columns.Add(col);
            if (grid == _grid) _baseHeaders["Help"] = col.HeaderText;

            // What the cell shows: the first document, and how many more there are.
            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (grid.Columns[e.ColumnIndex].Name != "Help") return;
                var docs = HelpDocumentsFor(ItemAt(grid, e.RowIndex));
                // every alarm has at least its AI prompt (the list's last entry)
                e.Value = docs.Count == 0 ? AiPromptCaption :
                          docs[0].Title + (docs.Count > 1 ? "  (+" + (docs.Count - 1) + ")" : "");
                e.FormattingApplied = true;
            };

            // The dropdown arrow.
            grid.CellPainting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (grid.Columns[e.ColumnIndex].Name != "Help") return;
                if (ItemAt(grid, e.RowIndex) == null) return;

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
            grid.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (grid.Columns[e.ColumnIndex].Name != "Help") return;
                ShowHelpFor(grid, e.RowIndex, e.ColumnIndex);
            };

            // A hand over a cell that opens something, an arrow everywhere else.
            grid.CellMouseEnter += (s, e) =>
            {
                bool clickable = e.RowIndex >= 0 && e.ColumnIndex >= 0 &&
                                 grid.Columns[e.ColumnIndex].Name == "Help" && ItemAt(grid, e.RowIndex) != null;
                grid.Cursor = clickable ? Cursors.Hand : Cursors.Default;
            };
            grid.MouseLeave += (s, e) => grid.Cursor = Cursors.Default;
        }

        /// <summary>Every document configured for an alarm (of either list).</summary>
        private List<TroubleshootingCatalog.HelpDocument> HelpDocumentsFor(AlarmItem item)
        {
            // The grid holds RAISED alarms (AlarmItem); what the alarm is linked to
            // lives on its DEFINITION. Its unit's manual comes from the unit on the
            // machine's pages, found at start-up; its own helper files follow.
            if (item == null) return new List<TroubleshootingCatalog.HelpDocument>();

            // CACHED BRIEFLY. This is asked by CellFormatting, CellPainting and
            // CellMouseEnter - for every row, on every repaint - and each answer
            // probes the disk several times per document (language variants).
            // Two seconds is short enough that a document copied in by hand shows
            // up almost at once, and long enough that painting never touches disk.
            // Per alarm (the history has the same code many times, each its own time).
            CachedHelp hit;
            int now = Environment.TickCount;
            if (_helpCache.TryGetValue(item, out hit) && hit.Language == LanguageManager.CurrentLanguage && now - hit.Tick < HelpCacheMs) return hit.Docs;

            var docs = Kaban.UI.Helpers.AlarmHelpLinks.DocumentsFor(item);
            if (_helpCache.Count > 400) _helpCache.Clear();     // bounded
            _helpCache[item] = new CachedHelp { Tick = now, Docs = docs, Language = LanguageManager.CurrentLanguage };
            return docs;
        }

        private const int HelpCacheMs = 2000;
        private sealed class CachedHelp { public int Tick; public string Language; public List<TroubleshootingCatalog.HelpDocument> Docs; }
        private readonly Dictionary<AlarmItem, CachedHelp> _helpCache = new Dictionary<AlarmItem, CachedHelp>();

        /// <summary>Opens the document list under the cell, like a combo.</summary>
        private void ShowHelpFor(DataGridView grid, int rowIndex, int columnIndex)
        {
            var alarm = ItemAt(grid, rowIndex);
            if (alarm == null) return;
            var docs = HelpDocumentsFor(alarm);
            var menu = BuildHelpMenu(docs, OpenDocument, () => Kaban.UI.Helpers.AlarmAiPrompt.Build(alarm, null, docs), this);
            var cell = grid.GetCellDisplayRectangle(columnIndex, rowIndex, false);
            UiStyle.ShowPopup(menu, grid, new Point(cell.Left, cell.Bottom));
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
                                                     Action<TroubleshootingCatalog.HelpDocument> open,
                                                     Func<string> aiPrompt = null, IWin32Window owner = null, string aiWhyNot = null)
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
            // THE LAST ENTRIES: an AI prompt for this alarm, put on the clipboard (8 October),
            // and the user's AI chat opened with it (9 October)
            if (aiPrompt != null)
            {
                if (docs.Count > 0) menu.Items.Add(new ToolStripSeparator());
                foreach (var item in Kaban.UI.Helpers.AiMenu.Items(aiPrompt, Kaban.UI.Helpers.AlarmAiPrompt.Subject.Alarm, owner))
                    menu.Items.Add(item);
            }
            // no fault to ask about (a unit's "?" with none on): the entries there, greyed, saying why (24 October)
            else if (aiWhyNot != null)
            {
                if (docs.Count > 0) menu.Items.Add(new ToolStripSeparator());
                foreach (var item in Kaban.UI.Helpers.AiMenu.Unavailable(Kaban.UI.Helpers.AlarmAiPrompt.Subject.Alarm, aiWhyNot))
                    menu.Items.Add(item);
            }
            return menu;
        }

        /// <summary>The help list's AI entry (the Help cell of an alarm with no document of its own).</summary>
        public static string AiPromptCaption
        {
            get { return Kaban.UI.Helpers.AiMenu.CreateCaption; }
        }

        private void OpenDocument(TroubleshootingCatalog.HelpDocument doc)
        {
            // One way to open help, shared with the "?" on the units.
            Kaban.UI.Helpers.AlarmHelpLinks.Open(this, doc);
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

        internal static int CompareBy(string column, AlarmItem a, AlarmItem b)
        {
            switch (column)
            {
                case "AlarmCode": return string.Compare(a.ListCode, b.ListCode, StringComparison.OrdinalIgnoreCase);
                case "Cause": return string.Compare(a.Cause, b.Cause, StringComparison.OrdinalIgnoreCase);
                case "Address": return string.Compare(a.AddressShown, b.AddressShown, StringComparison.OrdinalIgnoreCase);
                case "Severity": return a.Severity.CompareTo(b.Severity);
                case "TriggerTime": return a.TriggerTime.CompareTo(b.TriggerTime);
                default: return string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
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

