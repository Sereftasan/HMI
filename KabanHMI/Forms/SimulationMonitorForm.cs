using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// LIVE MONITOR for the machine's PLC memory - the equivalent of watching a
    /// CPU's device memory in GX Works, for the stations where that is not possible.
    ///
    /// ONE TAB PER CONTROLLER, named the way the engineer names it: "FA2010 PLC1",
    /// "FA2010 PLC2", "Screwing PLC1". Previously the tabs were "Station 1",
    /// "Station 2"..., which is the DEPLOYMENT's numbering - on a machine with a
    /// sub-machine mounted on station 3 there was nothing on screen connecting
    /// "Station 3" to the sub-machine that owns it. The tab list now comes from the
    /// mounted modules, so it follows whatever is actually mounted and a machine
    /// with no sub-machines simply has fewer tabs.
    ///
    /// The grid is the admin's WATCH LIST for that controller: Label, DataType,
    /// Address and Value are all editable, the trailing blank line adds a device,
    /// and the list can be exported to (or seeded from) a CSV in the same
    /// Label;DataType;Address shape as the machine's PlcLabels.csv.
    ///
    /// A VALUE TYPED HERE IS WRITTEN STRAIGHT THROUGH, on a simulated station and
    /// on a real one alike. The page is admin-only and that is the point of it: on
    /// the simulator it is how a sensor is turned on to exercise a screen, and on a
    /// real CPU it is the same job GX Works does. Writes to a real station are
    /// still logged through AuditLogger, and the tab carries a banner saying which
    /// kind of station it is, so nobody types into a live machine thinking it is
    /// the bench.
    ///
    /// Opened from the user menu (admin) or F12.
    /// </summary>
    public class SimulationMonitorForm : Form
    {
        private readonly TabControl _tabs = new TabControl();
        private readonly Timer _refresh = new Timer();
        private readonly List<MonitorTab> _monitors = new List<MonitorTab>();
        private readonly Label _help = new Label();

        // ==================================================================
        // MODEL
        // ==================================================================

        /// <summary>
        /// One line of a controller's watch list. Label and DataType default to the
        /// machine's PlcLabels.csv but may be OVERRIDDEN here - on the bench the
        /// engineer routinely watches a device the generated label list has not
        /// caught up with yet, and being unable to name it is what made the old
        /// grid hard to read.
        /// </summary>
        private class WatchRow
        {
            public string Address = "";
            /// <summary>The station of the tab this row is on - labels differ per machine.</summary>
            public int Station;
            public string LabelOverride = "";
            public string DataTypeOverride = "";

            /// <summary>True for the trailing blank line that exists to be typed into.</summary>
            public bool IsBlank { get { return string.IsNullOrWhiteSpace(Address); } }

            public string EffectiveLabel
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(LabelOverride)) return LabelOverride;
                    var e = PlcLabelTable.ByAddress(Station, Address);
                    return e != null && e.Label.Length > 0 ? e.Label : "";
                }
            }

            public string EffectiveDataType
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(DataTypeOverride)) return DataTypeOverride;
                    var e = PlcLabelTable.ByAddress(Station, Address);
                    return e != null ? e.DataType : "";
                }
            }

            /// <summary>True when the label list does not know this address.</summary>
            public bool IsUnknownToLabelTable
            {
                get
                {
                    return string.IsNullOrWhiteSpace(LabelOverride) &&
                           PlcLabelTable.ByAddress(Station, Address) == null;
                }
            }
        }

        private class MonitorTab
        {
            public PlcConnectionSettings.Endpoint Endpoint;
            public bool IsSimulated;
            public DataGridView Grid;
            public Label Banner;

            public readonly List<WatchRow> Rows = new List<WatchRow>();

            /// <summary>Addresses removed from the VIEW (never from the PLC image).</summary>
            public readonly HashSet<string> Hidden =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Latest values, by address. Filled by the refresh.</summary>
            public readonly Dictionary<string, int> Values =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            public bool Editing;
            public int DragFrom = -1;

            /// <summary>A background read is outstanding (real stations only).</summary>
            public volatile bool ReadInFlight;

            public int Station { get { return Endpoint != null ? Endpoint.StationId : 0; } }

            /// <summary>Read access. Deliberately not permission-gated - watching is not commanding.</summary>
            public IPlcDriver ReadDriver
            {
                get { return PlcManager.Instance.GetDriverForMonitoring(Station); }
            }

            /// <summary>Write access, which IS permission-gated.</summary>
            public IPlcDriver WriteDriver
            {
                get { return PlcManager.Instance.GetDriver(Station); }
            }
        }

        // ==================================================================
        // CONSTRUCTION
        // ==================================================================

        public SimulationMonitorForm()
        {
            Text = LanguageManager.GetString("Ttl_SimMonitor", "PLC Monitor - live device memory");
            Size = new Size(720, 660);
            MinimumSize = new Size(560, 400);
            StartPosition = FormStartPosition.Manual;
            MinimizeBox = true;
            ShowInTaskbar = true;
            BackColor = Color.White;

            _help.Dock = DockStyle.Top;
            _help.Height = 44;
            _help.Padding = new Padding(8, 6, 8, 6);
            _help.ForeColor = Color.FromArgb(60, 60, 60);
            _help.Text = LanguageManager.GetString("Msg_SimMonitorHelp",
                "One tab per machine controller. Values update live, edit a cell (or double-click a row) "
              + "to change one.\r\nType into the empty line at the bottom of the list to watch another device.");

            _tabs.Dock = DockStyle.Fill;
            Controls.Add(_tabs);
            Controls.Add(_help);

            BuildTabs();

            _refresh.Interval = 250;
            _refresh.Tick += (s, e) => RefreshValues();
            _refresh.Start();
        }

        /// <summary>True when there is any controller worth opening the monitor for.</summary>
        public static bool HasMonitorableStations()
        {
            return PlcConnectionSettings.Endpoints()
                   .Any(ep => PlcManager.Instance.GetDriverForMonitoring(ep.StationId) != null);
        }

        /// <summary>
        /// True when at least one controller is SIMULATED. The caption-bar shortcut
        /// stays tied to this: a bench tool button on a production machine's title
        /// bar would be noise, while the admin menu entry and F12 remain available
        /// everywhere.
        /// </summary>
        public static bool HasSimulatedStations()
        {
            return PlcConnectionSettings.Endpoints().Any(ep => IsSimulatedStation(ep.StationId));
        }

        /// <summary>
        /// The UI layer must not reference the Simulation driver assembly, so the
        /// simulator is recognised by type name.
        /// </summary>
        private static bool IsSimulatedStation(int station)
        {
            var drv = PlcManager.Instance.GetDriverForMonitoring(station);
            return drv != null && drv.GetType().Name == "SimulationDriver";
        }

        private void BuildTabs()
        {
            var endpoints = PlcConnectionSettings.Endpoints()
                            .Where(ep => PlcManager.Instance.GetDriverForMonitoring(ep.StationId) != null)
                            .ToList();

            foreach (var endpoint in endpoints)
                _tabs.TabPages.Add(BuildTab(endpoint));

            if (_monitors.Count == 0)
            {
                var none = new TabPage(LanguageManager.GetString("Tab_NoStations", "No controllers"));
                none.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(14),
                    Text = LanguageManager.GetString("Msg_NoStations",
                        "No PLC driver is registered.\r\n\r\n"
                      + "Check Settings -> HMI Settings -> PLC Connection, or register a driver in the "
                      + "machine's Program.cs, e.g.\r\n"
                      + "    PlcManager.Instance.RegisterDriver(2, new SimulationDriver(2));")
                });
                _tabs.TabPages.Add(none);
            }
        }

        private TabPage BuildTab(PlcConnectionSettings.Endpoint endpoint)
        {
            var page = new TabPage(PlcEndpointNaming.Caption(endpoint));
            page.BackColor = Color.White;
            page.ToolTipText = PlcEndpointNaming.CaptionWithStation(endpoint);

            var tab = new MonitorTab
            {
                Endpoint = endpoint,
                IsSimulated = IsSimulatedStation(endpoint.StationId)
            };

            tab.Grid = BuildGrid();
            WireGrid(tab);

            // ---- the two bottom buttons -------------------------------------
            // These REPLACE the old "Address / Value / Set-Add" bar. That bar could
            // only introduce one device at a time and had no way to keep a list
            // between sessions, so a bench setup was rebuilt by hand every run.
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 42, BackColor = Color.White };
            var tips = Kaban.UI.Controls.UiStyle.Tips();

            var btnExport = UiStyle.Round(new Button
            {
                Text = LanguageManager.GetString("Btn_ExportCsv", "Export CSV..."),
                Width = 130,
                Height = 30,
                Left = 8,
                Top = 6,
                BackColor = Color.White
            });
            var btnImport = UiStyle.Round(new Button
            {
                Text = LanguageManager.GetString("Btn_ImportCsv", "Import CSV..."),
                Width = 130,
                Height = 30,
                Left = 146,
                Top = 6,
                BackColor = Color.White
            });
            tips.SetToolTip(btnExport, LanguageManager.GetString("Tip_ExportCsv",
                "Save this tab's list as Label / DataType / Address / Value"));
            tips.SetToolTip(btnImport, LanguageManager.GetString("Tip_ImportCsv",
                "Add devices from a CSV. A PlcLabels.csv (Label / DataType / Address) can be used as-is."));

            var captured = tab;
            btnExport.Click += (s, e) => ExportTab(captured);
            btnImport.Click += (s, e) => ImportTab(captured);
            bottom.Controls.Add(btnExport);
            bottom.Controls.Add(btnImport);

            // ---- which kind of station is this? -----------------------------
            // Not a lock, a LABEL. Writes go through either way; this is what stops
            // a real CPU being mistaken for the bench simulator at a glance.
            if (!tab.IsSimulated)
            {
                tab.Banner = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 24,
                    Padding = new Padding(8, 4, 8, 4),
                    BackColor = Color.FromArgb(255, 232, 232),
                    ForeColor = Color.FromArgb(150, 40, 40),
                    Text = LanguageManager.GetString("Msg_LiveStation",
                        "LIVE PLC - a value typed here is written to the machine immediately.")
                };
            }

            LoadWatchList(tab);
            EnsureBlankRow(tab);

            page.Controls.Add(tab.Grid);
            if (tab.Banner != null) page.Controls.Add(tab.Banner);
            page.Controls.Add(bottom);

            _monitors.Add(tab);
            return page;
        }

        private static DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                // The blank line at the end is a row we own, not the DataGridView's
                // own "new row". Its placeholder row fights a 250ms refresh - it is
                // committed the moment a key lands, so a half-typed address became a
                // real row that the next refresh then trimmed away underneath the
                // operator's hands.
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true
            };

            // Label first: an engineer thinks in names, not addresses.
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Label", HeaderText = LanguageManager.GetString("Col_Label", "Label"), FillWeight = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "DataType", HeaderText = LanguageManager.GetString("Col_DataType", "DataType"), FillWeight = 45 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Address", HeaderText = LanguageManager.GetString("Col_Address", "Address"), FillWeight = 35 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Value", HeaderText = LanguageManager.GetString("Col_Value", "Value"), FillWeight = 35 });
            Kaban.UI.Controls.UiStyle.EnableAutoFit(grid);
            UiStyle.EnableTableTools(grid, "SimulationMonitor");

            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 246, 248);
            return grid;
        }

        // ==================================================================
        // GRID BEHAVIOUR
        // ==================================================================

        private void WireGrid(MonitorTab tab)
        {
            var grid = tab.Grid;

            // ---- remove a row from the view ----
            var rowMenu = new ContextMenuStrip();
            var miEdit = new ToolStripMenuItem(LanguageManager.GetString("Mnu_EditRow", "Edit this device..."));
            var miRemove = new ToolStripMenuItem(LanguageManager.GetString("Mnu_RemoveRow", "Remove from this list"));
            var miRestore = new ToolStripMenuItem(LanguageManager.GetString("Mnu_RestoreRows", "Show everything again"));
            rowMenu.Items.Add(miEdit);
            rowMenu.Items.Add(new ToolStripSeparator());
            rowMenu.Items.Add(miRemove);
            rowMenu.Items.Add(miRestore);
            grid.ContextMenuStrip = rowMenu;

            miEdit.Click += (s, e) =>
            {
                if (grid.CurrentRow != null) EditRowDialog(tab, grid.CurrentRow.Index);
            };
            miRemove.Click += (s, e) => RemoveSelected(tab);
            miRestore.Click += (s, e) =>
            {
                tab.Hidden.Clear();
                SaveWatchList(tab);
                RefreshValues();
            };

            grid.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete && !tab.Editing) { RemoveSelected(tab); e.Handled = true; }
            };

            // ---- the popup editor ----
            grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) EditRowDialog(tab, e.RowIndex);
            };

            // ---- hold and drag a row to re-order ----
            grid.AllowDrop = true;
            grid.MouseDown += (s, e) =>
            {
                var hit = grid.HitTest(e.X, e.Y);
                tab.DragFrom = hit.Type == DataGridViewHitTestType.Cell ? hit.RowIndex : -1;
            };
            grid.MouseMove += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || tab.DragFrom < 0) return;
                grid.DoDragDrop(tab.DragFrom, DragDropEffects.Move);
            };
            grid.DragOver += (s, e) => e.Effect = DragDropEffects.Move;
            grid.DragDrop += (s, e) =>
            {
                var pt = grid.PointToClient(new Point(e.X, e.Y));
                int to = grid.HitTest(pt.X, pt.Y).RowIndex;
                int from = tab.DragFrom;
                tab.DragFrom = -1;
                if (from < 0 || to < 0 || from == to) return;
                if (from >= tab.Rows.Count || to >= tab.Rows.Count) return;

                // The blank line stays last: it is where the next device is typed,
                // and letting it be dragged into the middle would split the list.
                if (tab.Rows[from].IsBlank || tab.Rows[to].IsBlank) return;

                var moved = tab.Rows[from];
                tab.Rows.RemoveAt(from);
                tab.Rows.Insert(to, moved);
                SaveWatchList(tab);
                RefreshValues();
                if (to < grid.Rows.Count) grid.Rows[to].Selected = true;
            };

            // pause the refresh while the user types, so edits are not overwritten
            grid.CellBeginEdit += (s, e) => tab.Editing = true;
            grid.CellEndEdit += (s, e) =>
            {
                tab.Editing = false;
                CommitCellEdit(tab, e.RowIndex, e.ColumnIndex);
            };
        }

        private void RemoveSelected(MonitorTab tab)
        {
            var addresses = tab.Grid.SelectedRows.Cast<DataGridViewRow>()
                            .Select(r => Convert.ToString(r.Cells["Address"].Value))
                            .Where(a => !string.IsNullOrWhiteSpace(a))
                            .ToList();
            if (addresses.Count == 0) return;

            foreach (var a in addresses) tab.Hidden.Add(a);
            tab.Rows.RemoveAll(r => !r.IsBlank && addresses.Contains(r.Address, StringComparer.OrdinalIgnoreCase));
            EnsureBlankRow(tab);
            SaveWatchList(tab);
            RefreshValues();
        }

        /// <summary>
        /// The list always ends in exactly one blank line - the line the operator
        /// types the next device into. Asked for in place of the old add bar: the
        /// bar was a second, differently-shaped way to do what the grid already
        /// looked like it could do.
        /// </summary>
        private static void EnsureBlankRow(MonitorTab tab)
        {
            tab.Rows.RemoveAll(r => r.IsBlank);
            tab.Rows.Add(new WatchRow());
        }

        private void CommitCellEdit(MonitorTab tab, int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= tab.Rows.Count) return;
            if (columnIndex < 0 || columnIndex >= tab.Grid.Columns.Count) return;

            var row = tab.Rows[rowIndex];
            var grid = tab.Grid;
            string column = grid.Columns[columnIndex].Name;

            string typed = Convert.ToString(grid.Rows[rowIndex].Cells[columnIndex].Value);
            typed = (typed ?? "").Trim();

            switch (column)
            {
                case "Label":
                    row.LabelOverride = typed;
                    break;

                case "DataType":
                    row.DataTypeOverride = typed;
                    break;

                case "Address":
                {
                    string wanted = typed.ToUpperInvariant();
                    if (wanted.Length == 0)
                    {
                        // clearing an address retires the line
                        if (!row.IsBlank) tab.Rows.RemoveAt(rowIndex);
                        break;
                    }
                    if (tab.Rows.Any(r => r != row &&
                                          string.Equals(r.Address, wanted, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show(this,
                            LanguageManager.GetString("Msg_AddressAlreadyListed",
                                "That address is already in this list."),
                            Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    }
                    row.Address = wanted;
                    // typing into the blank line un-hides the address, or it would
                    // disappear again on the very next refresh
                    tab.Hidden.Remove(wanted);
                    break;
                }

                case "Value":
                    WriteValue(tab, row, typed);
                    break;
            }

            EnsureBlankRow(tab);
            SaveWatchList(tab);
            RefreshValues();
        }

        private void EditRowDialog(MonitorTab tab, int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= tab.Rows.Count) return;
            var row = tab.Rows[rowIndex];

            int current;
            string valueText = !row.IsBlank && tab.Values.TryGetValue(row.Address, out current)
                ? current.ToString(CultureInfo.InvariantCulture) : "";

            using (var dlg = new PlcRowEditorForm(
                       PlcEndpointNaming.Caption(tab.Endpoint),
                       row.EffectiveLabel, row.EffectiveDataType, row.Address, valueText))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string wanted = dlg.RowAddress;
                if (tab.Rows.Any(r => r != row &&
                                      string.Equals(r.Address, wanted, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show(this,
                        LanguageManager.GetString("Msg_AddressAlreadyListed",
                            "That address is already in this list."),
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                row.Address = wanted;
                tab.Hidden.Remove(wanted);

                // Store a label only when it differs from what the label list already
                // says, so a corrected PlcLabels.csv still flows through later.
                var known = PlcLabelTable.ByAddress(tab.Station, wanted);
                row.LabelOverride =
                    known != null && string.Equals(known.Label, dlg.RowLabel, StringComparison.Ordinal)
                    ? "" : dlg.RowLabel;
                row.DataTypeOverride =
                    known != null && string.Equals(known.DataType, dlg.RowDataType, StringComparison.Ordinal)
                    ? "" : dlg.RowDataType;

                if (dlg.RowValue.HasValue)
                    WriteValue(tab, row, dlg.RowValue.Value.ToString(CultureInfo.InvariantCulture));

                EnsureBlankRow(tab);
                SaveWatchList(tab);
                RefreshValues();
            }
        }

        /// <summary>
        /// Writes one device. Refuses on a locked live station and reports WHY
        /// rather than doing nothing - a silently ignored write on a machine is the
        /// kind of thing that gets diagnosed as a broken PLC.
        /// </summary>
        private void WriteValue(MonitorTab tab, WatchRow row, string text)
        {
            if (row == null || row.IsBlank) return;

            long typed;
            if (!long.TryParse((text ?? "").Trim(), NumberStyles.Integer,
                               CultureInfo.InvariantCulture, out typed)) return;

            bool dword = IsDoubleWord(row.EffectiveDataType);
            if (!dword && (typed < short.MinValue || typed > ushort.MaxValue)) return;
            if (dword && (typed < int.MinValue || typed > uint.MaxValue)) return;
            int value = (int)typed;

            var driver = tab.WriteDriver;
            if (driver == null)
            {
                MessageBox.Show(this, PlcManager.Instance.DescribeAccess(tab.Station),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (dword)
            {
                // Both words, low first - writing the low word alone (which is what
                // happened) changed a 32-bit value into nonsense.
                int low = (int)(typed & 0xFFFF), high = (int)((typed >> 16) & 0xFFFF);
                var next = PlcAddressHelper.GetNextAddress(row.Address);
                driver.Write(row.Address, low);
                driver.Write(next, high);
                tab.Values[row.Address] = low;
                tab.Values[next] = high;
            }
            else
            {
                driver.Write(row.Address, value);
                tab.Values[row.Address] = value;
            }

            if (!tab.IsSimulated)
                AuditLogger.Log("monitor wrote " + value + " to " + row.Address + " on " +
                                PlcEndpointNaming.CaptionWithStation(tab.Endpoint) + ".");
        }

        // ==================================================================
        // REFRESH
        // ==================================================================

        /// <summary>True for a data type held in two consecutive words.</summary>
        internal static bool IsDoubleWord(string dataType)
        {
            var d = (dataType ?? "").ToLowerInvariant();
            return d.Contains("double word") || d.Contains("dword") || d.Contains("bin_32") || d.Contains("32");
        }

        internal static bool IsUnsigned(string dataType)
        {
            return (dataType ?? "").IndexOf("unsigned", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Low and high words as the 32-bit value they make together.</summary>
        internal static long CombineDouble(int low, int high, bool unsigned)
        {
            uint raw = ((uint)(high & 0xFFFF) << 16) | (uint)(low & 0xFFFF);
            return unsigned ? (long)raw : (long)(int)raw;
        }

        private void RefreshValues()
        {
            foreach (var tab in _monitors)
            {
                if (tab.Editing) continue;

                if (tab.IsSimulated) ReadSimulated(tab);
                else BeginReadReal(tab);

                Render(tab);
            }
        }

        /// <summary>
        /// The simulator's whole image, straight out of memory. New addresses it has
        /// seen are APPENDED to the watch list, which is how a device a screen wrote
        /// to shows up without anyone having to add it by hand.
        /// </summary>
        private void ReadSimulated(MonitorTab tab)
        {
            var driver = tab.ReadDriver;
            if (driver == null) return;

            var dumpMethod = driver.GetType().GetMethod("Dump");
            if (dumpMethod == null) return;

            var dump = dumpMethod.Invoke(driver, null) as System.Collections.IDictionary;
            if (dump == null) return;

            tab.Values.Clear();
            foreach (System.Collections.DictionaryEntry kv in dump)
                tab.Values[Convert.ToString(kv.Key)] = Convert.ToInt32(kv.Value);

            var known = new HashSet<string>(
                tab.Rows.Where(r => !r.IsBlank).Select(r => r.Address),
                StringComparer.OrdinalIgnoreCase);

            var discovered = tab.Values.Keys
                             .Where(a => !known.Contains(a) && !tab.Hidden.Contains(a))
                             .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                             .ToList();

            if (discovered.Count == 0) return;

            // insert before the trailing blank line
            int at = tab.Rows.FindIndex(r => r.IsBlank);
            if (at < 0) at = tab.Rows.Count;
            foreach (var a in discovered) tab.Rows.Insert(at++, new WatchRow { Address = a });
            EnsureBlankRow(tab);
        }

        /// <summary>
        /// A REAL station has no memory dump, so the listed addresses are read as
        /// one batch - and on a WORKER THREAD. A PLC round trip is about 15ms, so
        /// batching a screenful of devices onto the 250ms UI timer would stall the
        /// whole HMI, which is why every other read path in this program works the
        /// same way.
        /// </summary>
        private void BeginReadReal(MonitorTab tab)
        {
            if (tab.ReadInFlight) return;

            var driver = tab.ReadDriver;
            if (driver == null) return;

            var addresses = tab.Rows.Where(r => !r.IsBlank).Select(r => r.Address).ToArray();
            if (addresses.Length == 0) return;

            tab.ReadInFlight = true;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                int[] values = null;
                bool ok = false;
                try { ok = driver.TryReadMulti(addresses, out values); }
                catch (Exception ex) { AuditLogger.Log("monitor read failed: " + ex.Message); }

                bool succeeded = ok;
                int[] read = values;
                try
                {
                    if (IsDisposed || !IsHandleCreated) { tab.ReadInFlight = false; return; }
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (succeeded && read != null && read.Length == addresses.Length)
                            for (int i = 0; i < addresses.Length; i++)
                                tab.Values[addresses[i]] = read[i];
                        tab.ReadInFlight = false;
                    });
                }
                catch (ObjectDisposedException) { tab.ReadInFlight = false; }   // form closed mid-read
                catch (InvalidOperationException) { tab.ReadInFlight = false; } // handle gone mid-read
            });
        }

        private static readonly Color ColLabelKnown = Color.FromArgb(35, 40, 48);
        private static readonly Color ColLabelUnknown = Color.FromArgb(150, 150, 150);
        private static readonly Color ColValueSet = Color.FromArgb(0, 120, 40);
        private static readonly Color ColValueClear = Color.FromArgb(90, 90, 90);

        private static void Render(MonitorTab tab)
        {
            foreach (var r in tab.Rows) r.Station = tab.Station;   // before any label is looked up
            var grid = tab.Grid;

            while (grid.Rows.Count > tab.Rows.Count) grid.Rows.RemoveAt(grid.Rows.Count - 1);
            while (grid.Rows.Count < tab.Rows.Count) grid.Rows.Add();

            for (int i = 0; i < tab.Rows.Count; i++)
            {
                var model = tab.Rows[i];
                var row = grid.Rows[i];

                Set(row.Cells["Address"], model.Address);

                if (model.IsBlank)
                {
                    // the invitation line: no label, no type, no value
                    Set(row.Cells["Label"], "");
                    Set(row.Cells["DataType"], "");
                    Set(row.Cells["Value"], "");
                    continue;
                }

                // An address the label list does not know reads NULL, which is itself
                // useful: it means a screen is bound to something outside that list.
                string label = model.EffectiveLabel;
                if (label.Length == 0) label = "NULL";
                Set(row.Cells["Label"], label);

                var wantLabelColour = model.IsUnknownToLabelTable ? ColLabelUnknown : ColLabelKnown;
                if (row.Cells["Label"].Style.ForeColor != wantLabelColour)
                    row.Cells["Label"].Style.ForeColor = wantLabelColour;

                Set(row.Cells["DataType"], model.EffectiveDataType);

                int value;
                if (tab.Values.TryGetValue(model.Address, out value))
                {
                    // A DOUBLE WORD IS ONE VALUE over two devices: D318 holds the
                    // low word, D319 the high one. Showing D318 alone printed 3296
                    // for a limit of 22,220,000 - the number the machine actually
                    // uses, and the one the axis page shows as 2222.00.
                    long shown = value;
                    if (IsDoubleWord(model.EffectiveDataType))
                    {
                        int high;
                        tab.Values.TryGetValue(PlcAddressHelper.GetNextAddress(model.Address), out high);
                        shown = CombineDouble(value, high, IsUnsigned(model.EffectiveDataType));
                    }
                    Set(row.Cells["Value"], shown.ToString(CultureInfo.InvariantCulture));
                    // a set bit stays obvious - the VALUE cell itself goes green
                    var want = shown != 0 ? ColValueSet : ColValueClear;
                    if (row.Cells["Value"].Style.ForeColor != want)
                        row.Cells["Value"].Style.ForeColor = want;
                }
                else
                {
                    // not read yet, or the read failed - an honest gap, not a zero
                    Set(row.Cells["Value"], "-");
                    if (row.Cells["Value"].Style.ForeColor != ColLabelUnknown)
                        row.Cells["Value"].Style.ForeColor = ColLabelUnknown;
                }
            }
        }

        /// <summary>Assigns only on change - writing every cell every 250ms flickers.</summary>
        private static void Set(DataGridViewCell cell, string text)
        {
            if (Convert.ToString(cell.Value) != text) cell.Value = text;
        }

        // ==================================================================
        // EXPORT / IMPORT
        // ==================================================================

        private const string CsvHeader = "Label;DataType;Address;Value";

        private void ExportTab(MonitorTab tab)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = LanguageManager.GetString("Ttl_ExportCsv", "Export this controller's list");
                dlg.Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*";
                dlg.FileName = SafeFileName(PlcEndpointNaming.Caption(tab.Endpoint)) + ".csv";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var lines = new List<string>
                    {
                        "# " + PlcEndpointNaming.CaptionWithStation(tab.Endpoint) +
                        " - exported " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        CsvHeader
                    };

                    foreach (var r in tab.Rows.Where(r => !r.IsBlank))
                    {
                        int v;
                        string value = tab.Values.TryGetValue(r.Address, out v)
                            ? v.ToString(CultureInfo.InvariantCulture) : "";
                        lines.Add(string.Join(";", Csv(r.EffectiveLabel), Csv(r.EffectiveDataType),
                                                   Csv(r.Address), Csv(value)));
                    }

                    // UTF-8 WITH BOM, matching PlcLabels.csv and the translation files -
                    // Excel misreads a BOM-less file's non-ASCII labels.
                    File.WriteAllLines(dlg.FileName, lines, new UTF8Encoding(true));
                    AuditLogger.Log("exported " + tab.Rows.Count(r => !r.IsBlank) + " monitor rows for " +
                                    PlcEndpointNaming.Caption(tab.Endpoint) + ".");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        LanguageManager.GetString("Msg_ExportFailed", "Could not write the file:") +
                        "\r\n" + ex.Message,
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportTab(MonitorTab tab)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = LanguageManager.GetString("Ttl_ImportCsv", "Add devices from a CSV");
                dlg.Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                List<WatchRow> parsed;
                Dictionary<string, int> values;
                try
                {
                    parsed = ParseCsv(dlg.FileName, out values);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        LanguageManager.GetString("Msg_ImportFailed", "Could not read the file:") +
                        "\r\n" + ex.Message,
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (parsed.Count == 0)
                {
                    MessageBox.Show(this,
                        LanguageManager.GetString("Msg_ImportNothing",
                            "No usable rows found. Expected Label / DataType / Address and optionally a Value column."),
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var existing = new HashSet<string>(
                    tab.Rows.Where(r => !r.IsBlank).Select(r => r.Address),
                    StringComparer.OrdinalIgnoreCase);

                int at = tab.Rows.FindIndex(r => r.IsBlank);
                if (at < 0) at = tab.Rows.Count;

                int added = 0;
                foreach (var r in parsed)
                {
                    if (existing.Contains(r.Address)) continue;
                    tab.Hidden.Remove(r.Address);
                    tab.Rows.Insert(at++, r);
                    existing.Add(r.Address);
                    added++;
                }
                EnsureBlankRow(tab);

                // Importing must never command the machine as a SIDE EFFECT of
                // "add these to my list", so the values in the file are applied only
                // if the admin says so when asked.
                int applied = 0;
                if (values.Count > 0)
                {
                    var answer = MessageBox.Show(this,
                        string.Format(LanguageManager.GetString("Msg_ImportApplyValues",
                            "The file also carries {0} values. Write them to this controller as well?"),
                            values.Count),
                        Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (answer == DialogResult.Yes)
                    {
                        var driver = tab.WriteDriver;
                        if (driver == null)
                        {
                            MessageBox.Show(this, PlcManager.Instance.DescribeAccess(tab.Station),
                                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            foreach (var kv in values)
                            {
                                driver.Write(kv.Key, kv.Value);
                                tab.Values[kv.Key] = kv.Value;
                                applied++;
                            }
                            if (!tab.IsSimulated)
                                AuditLogger.Log("monitor imported and WROTE " + applied + " values to " +
                                                PlcEndpointNaming.CaptionWithStation(tab.Endpoint) + ".");
                        }
                    }
                }

                SaveWatchList(tab);
                RefreshValues();

                AuditLogger.Log("imported " + added + " monitor rows for " +
                                PlcEndpointNaming.Caption(tab.Endpoint) + " from " +
                                Path.GetFileName(dlg.FileName) + ".");

                MessageBox.Show(this,
                    string.Format(LanguageManager.GetString("Msg_ImportDone",
                        "Added {0} device(s). {1} were already listed."),
                        added, parsed.Count - added) +
                    (applied > 0 ? "\r\n" + string.Format(
                        LanguageManager.GetString("Msg_ImportWrote", "Wrote {0} value(s)."), applied) : ""),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Reads Label;DataType;Address with an optional Value. A three-column
        /// PlcLabels.csv is therefore a valid import file as it stands, which is the
        /// point - the engineer's device list is the readiest "ready file" there is.
        /// Tabs are tolerated as separators for the same reason PlcLabelTable does:
        /// these lists arrive pasted out of GX Works and Excel.
        /// </summary>
        private static List<WatchRow> ParseCsv(string path, out Dictionary<string, int> values)
        {
            var rows = new List<WatchRow>();
            values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var f = line.Split(line.Contains(";") ? ';' : '\t');
                if (f.Length < 3) continue;
                if (f[0].Trim().Equals("Label", StringComparison.OrdinalIgnoreCase)) continue;   // header

                string address = f[2].Trim().ToUpperInvariant();
                if (address.Length == 0 || !seen.Add(address)) continue;

                rows.Add(new WatchRow
                {
                    Address = address,
                    LabelOverride = f[0].Trim(),
                    DataTypeOverride = f[1].Trim()
                });

                int v;
                if (f.Length > 3 && int.TryParse(f[3].Trim(), NumberStyles.Integer,
                                                 CultureInfo.InvariantCulture, out v))
                    values[address] = v;
            }
            return rows;
        }

        private static string Csv(string field)
        {
            // the separator is ';' and these are device names, so stripping is safer
            // than quoting - a quoted field would break PlcLabelTable's own parser
            return (field ?? "").Replace(";", ",").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string SafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (var c in name ?? "")
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString().Trim();
        }

        // ==================================================================
        // THE WATCH LIST BETWEEN SESSIONS
        // ==================================================================

        private static string WatchFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "KabanHMI", "Monitor");
            }
        }

        /// <summary>
        /// Keyed by MODULE and the module's own PLC number, never by station id -
        /// the station id is what the PLC Connection page is allowed to change,
        /// so keying on it would throw the list away whenever it was.
        /// </summary>
        private static string WatchPath(PlcConnectionSettings.Endpoint endpoint)
        {
            string key = SafeFileName((string.IsNullOrEmpty(endpoint.ModuleId) ? "Station" : endpoint.ModuleId)
                                      + "_PLC" + endpoint.LocalPlcNumber);
            return Path.Combine(WatchFolder, key + ".csv");
        }

        private static void SaveWatchList(MonitorTab tab)
        {
            try
            {
                Directory.CreateDirectory(WatchFolder);
                var lines = new List<string>
                {
                    "# Kaban HMI monitor watch list - " + PlcEndpointNaming.CaptionWithStation(tab.Endpoint),
                    "# Row order is the order below. Values are a snapshot and are NOT restored.",
                    CsvHeader
                };

                foreach (var r in tab.Rows.Where(r => !r.IsBlank))
                    lines.Add(string.Join(";", Csv(r.LabelOverride), Csv(r.DataTypeOverride), Csv(r.Address), ""));

                foreach (var a in tab.Hidden.OrderBy(a => a, StringComparer.OrdinalIgnoreCase))
                    lines.Add("#hidden:" + a);

                File.WriteAllLines(WatchPath(tab.Endpoint), lines, new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not save the monitor watch list: " + ex.Message);
            }
        }

        private static void LoadWatchList(MonitorTab tab)
        {
            try
            {
                string path = WatchPath(tab.Endpoint);
                if (!File.Exists(path)) return;

                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("#hidden:", StringComparison.OrdinalIgnoreCase))
                    {
                        tab.Hidden.Add(line.Substring("#hidden:".Length).Trim());
                        continue;
                    }
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    var f = line.Split(';');
                    if (f.Length < 3) continue;
                    if (f[0].Trim().Equals("Label", StringComparison.OrdinalIgnoreCase)) continue;

                    string address = f[2].Trim().ToUpperInvariant();
                    if (address.Length == 0) continue;

                    tab.Rows.Add(new WatchRow
                    {
                        Address = address,
                        LabelOverride = f[0].Trim(),
                        DataTypeOverride = f[1].Trim()
                    });
                }
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not read the monitor watch list: " + ex.Message);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _refresh.Stop();
            foreach (var tab in _monitors) SaveWatchList(tab);
            base.OnFormClosing(e);
        }
    }
}

