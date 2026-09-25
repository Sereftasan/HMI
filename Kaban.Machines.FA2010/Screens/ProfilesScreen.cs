using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Data;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Dxf;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// PROFILES page, v3 - the database workbench.
    ///
    /// LEFT  - toolbar (add / delete / group-by) + filter row + the profile table.
    ///         Filters are contains-match per column; header click sorts and
    ///         SHIFT+click adds sort columns; "Group by" pulls a column to the
    ///         front of the sort and paints group bands in the grid.
    /// RIGHT - TABS (they did not fit on one page):
    ///         [Details] identity fields + UNIT-AWARE parameters (stored in the
    ///                   database in reference units - mm / s / degC - and shown
    ///                   in the operator's active units, exactly like the PLC
    ///                   numeric controls; captions carry the active suffix) with
    ///                   a DXF file browse and an emoji Save.
    ///         [Drawing] the DXF graph with the emoji tool bar.
    ///
    /// PostgreSQL `profiles` when reachable (INSERT/UPDATE/DELETE); built-in
    /// sample rows otherwise (edits stay in memory).
    /// </summary>
    public class ProfilesScreen : UserControl
    {
        private readonly DataTable _table = new DataTable();
        private readonly DataView _view;
        private readonly DataGridView _grid = new DataGridView();
        private readonly DxfViewerControl _viewer = new DxfViewerControl();
        private readonly Label _lblStatus = new Label();
        private readonly List<KeyValuePair<string, bool>> _sortKeys = new List<KeyValuePair<string, bool>>();
        private readonly Dictionary<string, TextBox> _filters = new Dictionary<string, TextBox>();
        private readonly Dictionary<string, NumericUpDown> _detailInputs = new Dictionary<string, NumericUpDown>();
        private readonly Dictionary<string, Label> _detailLabels = new Dictionary<string, Label>();
        private readonly Dictionary<string, Label> _detailSuffix = new Dictionary<string, Label>();
        private readonly Dictionary<string, TextBox> _identityInputs = new Dictionary<string, TextBox>();
        private Button _btnGroup;
        private readonly List<string> _groupCols = new List<string>();
        private readonly List<ToolStripMenuItem> _groupItems = new List<ToolStripMenuItem>();
        private TextBox _txtDxf;
        private TextBox _txtGcode;
        private NumericUpDown _numToolRadius, _numCutDepth;
        private Button _btnUndo, _btnRedo, _btnDelSel, _btnToolpath;
        private Kaban.UI.Dxf.DxfDocument.GCodeProgram _program;   // line <-> geometry map
        private readonly List<int> _pathLineIndex = new List<int>();   // drawn point -> code line
        private Panel _canvas;
        private readonly Dictionary<string, Kaban.UI.Controls.FloatingPaneHMI> _panes
            = new Dictionary<string, Kaban.UI.Controls.FloatingPaneHMI>();
        private ComboBox _cmbLayout;
        private bool _loadingLayout;

        /// <summary>
        /// True once the saved arrangement has actually been applied. Nothing is
        /// SAVED before that: the shell builds every screen up front, so this page
        /// received a VisibleChanged (and a save) with default pane bounds long
        /// before its layout was restored - which is what wiped the arrangement and
        /// made the page reopen collapsed.
        /// </summary>
        private bool _layoutRestored;
        private string _pendingSelectCode;                        // survives regrouping/sorting
        private bool _repopulating;
        private string _loadedDxfPath = "";                        // for "revert to original"
        private HmiDatabase _db;
        private bool _fromDb;
        private EventHandler _onUnitChanged;

        private class Field
        {
            public string Col, Caption; public UnitType Unit;
            public Field(string col, string caption, UnitType unit) { Col = col; Caption = caption; Unit = unit; }
        }

        private static readonly Field[] DetailFields =
        {
            new Field("width_mm", "Width", UnitType.Length),
            new Field("height_mm", "Height", UnitType.Length),
            new Field("melting_s", "Melting Time", UnitType.Time),
            new Field("welding_s", "Welding Time", UnitType.Time),
            new Field("temperature_c", "Temperature", UnitType.Temperature),
            new Field("clamping_mm", "Clamping Height", UnitType.Length)
        };

        public ProfilesScreen()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            // Air between the panes and the navigation bar: this page is shown on
            // its own, with no tab strip around it to inset it (see SettingsScreen).
            Padding = new Padding(8, 0, 8, 8);

            foreach (var c in new[] { "profile_code", "company_name", "serial", "profile_type", "dxf_file", "gcode" })
                _table.Columns.Add(c, typeof(string));
            foreach (var f in DetailFields) _table.Columns.Add(f.Col, typeof(decimal));
            _view = new DataView(_table);

            // FREE-FLOATING panes on a canvas: each area can be moved, resized,
            // overlapped and minimised, and whole arrangements can be saved by name
            // so an operator can switch to the layout that suits the job in hand.
            var layoutBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.White };
            var tips = Kaban.UI.Controls.UiStyle.Tips();
            layoutBar.Controls.Add(new Label
            { Text = "Layout :", AutoSize = true, Left = 8, Top = 11, Font = new Font("Segoe UI", 9F) });
            _cmbLayout = new ComboBox
            { Left = 62, Top = 7, Width = 190, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cmbLayout.SelectedIndexChanged += (s, e) =>
            {
                if (_loadingLayout) return;
                ApplyLayout(Convert.ToString(_cmbLayout.SelectedItem));
            };
            layoutBar.Controls.Add(_cmbLayout);

            int lx = 262;
            Action<string, string, EventHandler> addTool = (emoji, tip, click) =>
            {
                var b = Kaban.UI.Controls.UiStyle.ToolButton(emoji, tip, tips, click);
                b.Left = lx; b.Top = 3;
                layoutBar.Controls.Add(b);
                lx += 50;
            };
            addTool("\uD83D\uDCBE", "Save changes into the arrangement selected above", (s, e) => SaveCurrentLayout());
            addTool("\uD83D\uDCC4", "Save the current arrangement as a NEW named one", (s, e) => SaveLayoutAs());
            addTool("\uD83D\uDDD1", "Delete the selected saved arrangement", (s, e) => DeleteLayout());

            _canvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(246, 247, 249) };

            Controls.Add(_canvas);
            Controls.Add(layoutBar);

            BuildLeft();
            BuildRight();
            LoadRows();

            // live unit switching: captions, suffixes and values follow the active
            // units exactly like the PLC numeric controls
            _onUnitChanged = (s, e) => RefreshUnitPresentation();
            UnitManager.UnitChanged += _onUnitChanged;

            // navigating away hides the page rather than disposing it, so save then too
            VisibleChanged += (s, e) => { if (!Visible) SaveWorkingState(); };

            // Restore only once the canvas has a REAL size. HandleCreated can fire
            // while the page is still 0-sized (it is built before it is shown), and
            // restoring then placed every pane against a zero canvas - which is how
            // the page came back looking collapsed. Waiting for the first usable
            // layout pass fixes that regardless of when the handle appears.
            _canvas.Resize += (s, e) => TryRestoreLayout();
            HandleCreated += (s, e) => BeginInvoke(new Action(TryRestoreLayout));
        }

        /// <summary>Applies the saved arrangement the first time the page is really laid out.</summary>
        private void TryRestoreLayout()
        {
            if (_layoutRestored) return;
            if (_canvas == null || _canvas.ClientSize.Width < 200 || _canvas.ClientSize.Height < 200) return;

            LoadLayoutPresets();
            if (!ApplyLastUsedLayout())
            {
                Dictionary<string, string> working;
                if (_layouts.TryGetValue(WorkingStateName, out working)) RestoreArrangement(working);
                else ResetLayout();
            }
            _layoutRestored = true;      // from here on it is safe to SAVE
        }

        /// <summary>Creates a pane on the canvas and returns its content host.</summary>
        private Panel AddPane(string key, string caption)
        {
            var pane = new Kaban.UI.Controls.FloatingPaneHMI(key, caption);
            if (key == "table")
            {
                // a reset button on the table's own title bar, beside minimise
                pane.ShowReset = true;
                pane.ResetRequested += (s, e) => ResetTableView();
            }
            _panes[key] = pane;
            _canvas.Controls.Add(pane);
            return pane.Content;
        }

        // ---------------- layouts ----------------
        private static string LayoutFilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                    "KabanHMI", "ProfileLayouts.cfg");
            }
        }

        private readonly Dictionary<string, Dictionary<string, string>> _layouts
            = new Dictionary<string, Dictionary<string, string>>();
        private string _lastLayoutName = "";

        /// <summary>Default tiling - also what the reset button restores.</summary>
        public void ResetLayout()
        {
            int w = Math.Max(600, _canvas.ClientSize.Width), h = Math.Max(400, _canvas.ClientSize.Height);
            Action<string, int, int, int, int> place = (key, x, y, pw, ph) =>
            {
                Kaban.UI.Controls.FloatingPaneHMI pane;
                if (!_panes.TryGetValue(key, out pane)) return;
                pane.Minimized = false;
                pane.Bounds = new Rectangle(x, y, pw, ph);
            };
            place("table", 0, 0, (int)(w * 0.40), h);
            place("details", (int)(w * 0.40), 0, (int)(w * 0.28), (int)(h * 0.55));
            place("program", (int)(w * 0.68), 0, (int)(w * 0.32), (int)(h * 0.55));
            place("drawing", (int)(w * 0.40), (int)(h * 0.55), (int)(w * 0.60), (int)(h * 0.45));

            // Default is a COMPLETE arrangement: grouping, sorting, filters and the
            // columns go back to their starting state as well, otherwise choosing it
            // left the table exactly as the operator had rearranged it.
            _groupCols.Clear();
            foreach (var it in _groupItems) it.Checked = false;
            _btnGroup.Text = "Group \u25BE";
            _sortKeys.Clear();
            foreach (var kv in _filters) kv.Value.Text = "";
            SetFiltersVisible(true);
            int order = 0;
            foreach (var name in new[] { "profile_code", "company_name", "serial", "profile_type" })
            {
                var col = _grid.Columns[name];
                if (col == null) continue;
                col.Visible = true;
                col.Width = 110;
                col.DisplayIndex = order++;
            }
            UpdateHeaderArrows();
            ApplyFilterAndSort();
        }

        /// <summary>The built-in arrangement; always first in the list and never edited.</summary>
        public const string DefaultLayoutName = "Default";

        /// <summary>
        /// The LIVE working state, saved automatically. Named arrangements are
        /// deliberate snapshots, but whatever the operator has on screen right now
        /// - panes, grouping, sorting, column widths - must also come back after
        /// navigating away or restarting, even when no arrangement was ever saved.
        /// Hidden from the chooser (the '#' prefix marks internal entries).
        /// </summary>
        private const string WorkingStateName = "#working";

        private Dictionary<string, string> CaptureArrangement()
        {
            var entry = new Dictionary<string, string>();
            foreach (var kv in _panes) entry[kv.Key] = kv.Value.Serialize();
            // the table's own state belongs to the arrangement too
            entry["#groups"] = string.Join(",", _groupCols);
            var sorts = new List<string>();
            foreach (var k in _sortKeys) sorts.Add(k.Key + ":" + (k.Value ? "A" : "D"));
            entry["#sorts"] = string.Join(",", sorts);
            var cols = new List<string>();
            foreach (DataGridViewColumn c in _grid.Columns)
                cols.Add(c.Name + ":" + (c.Visible ? 1 : 0) + ":" + c.Width + ":" + c.DisplayIndex);
            entry["#columns"] = string.Join(",", cols);
            entry["#filters"] = _filtersVisible ? "1" : "0";
            return entry;
        }

        private void RestoreArrangement(Dictionary<string, string> entry)
        {
            // SELF-HEAL: an arrangement where EVERY pane is minimised is not something
            // anyone sets on purpose - it is what an older build wrote when it derived
            // "minimised" from Control.Visible. Rather than restore a useless
            // workspace, fall back to the default tiling.
            int paneEntries = 0, minimisedEntries = 0;
            foreach (var kv in entry)
            {
                if (kv.Key.StartsWith("#")) continue;
                paneEntries++;
                var f = (kv.Value ?? "").Split(',');
                if (f.Length >= 5 && f[4].Trim() == "1") minimisedEntries++;
            }
            if (paneEntries > 0 && minimisedEntries == paneEntries)
            {
                AuditLogger.Log("profile layout was entirely minimised - restoring the default arrangement.");
                ResetLayout();
                return;
            }

            foreach (var kv in entry)
            {
                if (kv.Key.StartsWith("#")) continue;
                Kaban.UI.Controls.FloatingPaneHMI pane;
                if (_panes.TryGetValue(kv.Key, out pane)) pane.Deserialize(kv.Value);
            }
            string v;
            if (entry.TryGetValue("#columns", out v) && v.Length > 0)
                foreach (var part in v.Split(','))
                {
                    var f = part.Split(':');
                    if (f.Length != 4) continue;
                    var col = _grid.Columns[f[0]];
                    if (col == null) continue;
                    col.Visible = f[1] == "1";
                    int w, di;
                    if (int.TryParse(f[2], out w) && w > 10) col.Width = w;
                    if (int.TryParse(f[3], out di) && di >= 0 && di < _grid.Columns.Count) col.DisplayIndex = di;
                }
            if (entry.TryGetValue("#filters", out v)) SetFiltersVisible(v != "0");
            if (entry.TryGetValue("#groups", out v))
            {
                _groupCols.Clear();
                foreach (var it in _groupItems) it.Checked = false;
                foreach (var g in v.Split(','))
                {
                    if (g.Length == 0) continue;
                    _groupCols.Add(g);
                    foreach (var it in _groupItems) if (GroupColumnOf(it) == g) it.Checked = true;
                }
                _btnGroup.Text = _groupCols.Count == 0 ? "Group \u25BE" : "Group (" + _groupCols.Count + ") \u25BE";
            }
            if (entry.TryGetValue("#sorts", out v))
            {
                _sortKeys.Clear();
                foreach (var srt in v.Split(','))
                {
                    var f = srt.Split(':');
                    if (f.Length == 2) _sortKeys.Add(new KeyValuePair<string, bool>(f[0], f[1] == "A"));
                }
            }
            ApplyFilterAndSort();
        }

        /// <summary>Overwrites the arrangement currently chosen in the combo.</summary>
        private void SaveCurrentLayout()
        {
            string name = Convert.ToString(_cmbLayout.SelectedItem);
            if (string.IsNullOrEmpty(name) || name == DefaultLayoutName)
            { SaveLayoutAs(); return; }                 // the built-in one is never overwritten
            _layouts[name] = CaptureArrangement();
            _lastLayoutName = name;
            SaveLayoutPresets();
            _lblStatus.Text = "Arrangement '" + name + "' updated.";
        }

        private void SaveLayoutAs()
        {
            string name = PromptForName();
            if (string.IsNullOrWhiteSpace(name) || name == DefaultLayoutName) return;
            _layouts[name] = CaptureArrangement();
            _lastLayoutName = name;
            SaveLayoutPresets();
            RefreshLayoutList();
            _lblStatus.Text = "Arrangement '" + name + "' saved.";
        }

        private void DeleteLayout()
        {
            string name = Convert.ToString(_cmbLayout.SelectedItem);
            if (string.IsNullOrEmpty(name) || !_layouts.ContainsKey(name)) return;
            if (MessageBox.Show("Delete arrangement '" + name + "'?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _layouts.Remove(name);
            if (_lastLayoutName == name) _lastLayoutName = "";
            SaveLayoutPresets();
            RefreshLayoutList();
        }

        private void ApplyLayout(string name)
        {
            if (name == DefaultLayoutName) { ResetLayout(); _lastLayoutName = name; SaveLayoutPresets(); return; }
            Dictionary<string, string> entry;
            if (string.IsNullOrEmpty(name) || !_layouts.TryGetValue(name, out entry)) return;
            RestoreArrangement(entry);
            _lastLayoutName = name;
            SaveLayoutPresets();
            _lblStatus.Text = "Arrangement '" + name + "' applied.";
        }

        private bool ApplyLastUsedLayout()
        {
            if (string.IsNullOrEmpty(_lastLayoutName) || _lastLayoutName == DefaultLayoutName ||
                !_layouts.ContainsKey(_lastLayoutName)) return false;
            _loadingLayout = true;
            _cmbLayout.SelectedItem = _lastLayoutName;
            _loadingLayout = false;
            ApplyLayout(_lastLayoutName);
            return true;
        }

        private void RefreshLayoutList()
        {
            _loadingLayout = true;
            _cmbLayout.Items.Clear();
            _cmbLayout.Items.Add(DefaultLayoutName);        // built-in, always first
            foreach (var key in _layouts.Keys)
                if (key != DefaultLayoutName && !key.StartsWith("#")) _cmbLayout.Items.Add(key);
            if (!string.IsNullOrEmpty(_lastLayoutName) && _cmbLayout.Items.Contains(_lastLayoutName))
                _cmbLayout.SelectedItem = _lastLayoutName;
            else _cmbLayout.SelectedItem = DefaultLayoutName;
            _loadingLayout = false;
        }

        /// <summary>Small inline prompt - no extra form file needed.</summary>
        private string PromptForName()
        {
            using (var dlg = new Form
            { Text = "Save arrangement", Width = 320, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog,
              StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false })
            {
                Kaban.UI.Controls.UiScale.ApplyOnLoad(dlg);
                var tb = new TextBox { Left = 12, Top = 16, Width = 280, Font = new Font("Segoe UI", 10F) };
                var ok = new Button { Text = "OK", Left = 212, Top = 56, Width = 80, DialogResult = DialogResult.OK };
                dlg.Controls.Add(tb);
                dlg.Controls.Add(ok);
                dlg.AcceptButton = ok;
                return dlg.ShowDialog(FindForm()) == DialogResult.OK ? tb.Text.Trim() : null;
            }
        }

        /// <summary>Stores the live arrangement so it survives navigation and restart.</summary>
        private void SaveWorkingState()
        {
            try
            {
                if (!_layoutRestored) return;              // never save a layout we have not restored yet
                if (_panes.Count == 0 || _grid.Columns.Count == 0) return;
                if (_canvas == null || _canvas.ClientSize.Width < 50) return;   // not laid out yet
                _layouts[WorkingStateName] = CaptureArrangement();
                SaveLayoutPresets();
            }
            catch { }
        }

        private void SaveLayoutPresets()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LayoutFilePath));
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("last=" + _lastLayoutName);
                foreach (var layout in _layouts)
                {
                    sb.AppendLine("[" + layout.Key + "]");
                    foreach (var pane in layout.Value) sb.AppendLine(pane.Key + "=" + pane.Value);
                }
                File.WriteAllText(LayoutFilePath, sb.ToString());
            }
            catch { }
        }

        private void LoadLayoutPresets()
        {
            try
            {
                _layouts.Clear();
                if (File.Exists(LayoutFilePath))
                {
                    string current = null;
                    foreach (var raw in File.ReadAllLines(LayoutFilePath))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0) continue;
                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            current = line.Substring(1, line.Length - 2);
                            _layouts[current] = new Dictionary<string, string>();
                        }
                        else
                        {
                            int eq = line.IndexOf('=');
                            if (eq <= 0) continue;
                            string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                            if (k == "last" && current == null) _lastLayoutName = v;
                            else if (current != null) _layouts[current][k] = v;
                        }
                    }
                }
            }
            catch { }
            RefreshLayoutList();
        }

        // ==================== LEFT ====================
        private void BuildLeft()
        {
            var left = new Panel { Dock = DockStyle.Fill };

            var tips = Kaban.UI.Controls.UiStyle.Tips();
            var tools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(4, 3, 4, 3) };
            Func<string, string, EventHandler, Button> Tool = (emoji, tip, onClick) =>
                Kaban.UI.Controls.UiStyle.ToolButton(emoji, tip, tips, onClick);
            tools.Controls.Add(Tool("\u2795", "Add a new profile", (s, e) => AddProfile()));
            tools.Controls.Add(Tool("\uD83D\uDDD1", "Delete the selected profile", (s, e) => DeleteProfile()));
            // MULTI-LEVEL grouping: tick any combination; the tick ORDER is the
            // grouping order, and the list renders group-caption rows above the
            // members (like the legacy HMI's grouped table).
            _btnGroup = new Button { Text = "Group \u25BE", Width = 110, Height = 30, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F) };
            var groupMenu = new ContextMenuStrip();
            foreach (var col0 in DefaultColumnOrder)
            {
                if (col0 == "profile_code") continue;         // unique per row: grouping is pointless
                var col = col0;
                string cap;
                if (!ColumnCaptions.TryGetValue(col, out cap)) cap = col;
                var item = new ToolStripMenuItem(cap) { CheckOnClick = true };
                item.CheckedChanged += (s, e) =>
                {
                    if (item.Checked) { if (!_groupCols.Contains(col)) _groupCols.Add(col); }
                    else _groupCols.Remove(col);
                    _btnGroup.Text = _groupCols.Count == 0 ? "Group \u25BE" : "Group (" + _groupCols.Count + ") \u25BE";
                    ApplyFilterAndSort();
                };
                groupMenu.Items.Add(item);
                _groupItems.Add(item);
            }
            _btnGroup.Click += (s, e) => groupMenu.Show(_btnGroup, new Point(0, _btnGroup.Height));
            tools.Controls.Add(_btnGroup);
            tools.Controls.Add(Tool("\u21BA", "Reset grouping, sorting and filters", (s, e) => ResetView()));

            // The filter row sits INSIDE the column-header band, directly beneath the
            // captions: the header is made tall enough to hold it and the boxes are
            // children of the grid, so they track the columns and never cover a row.
            foreach (var col in DefaultColumnOrder)
            {
                var tb = new TextBox { Font = new Font("Segoe UI", 9F), BorderStyle = BorderStyle.FixedSingle };
                tb.TextChanged += (s, e) => ApplyFilterAndSort();
                _grid.Controls.Add(tb);
                _filters[col] = tb;
            }

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            // A narrow EMPTY row header indents the data columns so the group caption
            // can start to their left. It is only shown WHILE grouping is active
            // (see Repopulate) - ungrouped, the table needs no indent.
            _grid.RowHeadersVisible = false;
            _grid.RowHeadersWidth = 20;
            _grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = HeaderTextHeight + FilterRowHeight;
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(2, 3, 0, 0);
            _grid.RowHeadersDefaultCellStyle.BackColor = Color.White;
            _grid.EnableHeadersVisualStyles = false;
            _grid.AutoGenerateColumns = false;
            // EXPLICIT widths, not Fill: Fill recomputes every width from weights, so a
            // width the operator set (and a saved arrangement) could never be restored
            // exactly. Columns stay user-resizable.
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.AllowUserToResizeColumns = true;
            _grid.AllowUserToOrderColumns = true;   // drag a header next to another to swap
            // Identity columns are shown by default; every numeric PARAMETER is added
            // as a hidden column so it can be switched on, filtered, grouped and
            // sorted like any other. File paths and the G-code are deliberately NOT
            // columns - they are documents, useless to filter or group by.
            foreach (var name in DefaultColumnOrder)
            {
                string caption;
                if (!ColumnCaptions.TryGetValue(name, out caption)) caption = name;
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = name,
                    HeaderText = caption,
                    Width = 110,
                    Visible = DefaultVisibleColumns.Contains(name),
                    SortMode = DataGridViewColumnSortMode.Programmatic
                });
                _baseHeaders[name] = caption;
            }
            _grid.ColumnHeaderMouseClick += OnHeaderClick;
            _grid.SelectionChanged += (s, e) =>
            {
                if (_repopulating) return;          // rows are being rebuilt, not chosen
                // ONLY a real selected row counts: SelectedRow falls back to the first
                // row, and during a rebuild that fallback used to overwrite the
                // operator's chosen profile.
                if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is DataRowView rv)
                    _pendingSelectCode = Convert.ToString(rv["profile_code"]);
                ShowSelected();
            };
            _grid.RowPrePaint += PaintCaptionRow;
            _grid.ColumnWidthChanged += (s, e) => AlignFilters();
            _grid.ColumnDisplayIndexChanged += (s, e) => AlignFilters();
            _grid.Scroll += (s, e) => AlignFilters();
            _grid.Resize += (s, e) => AlignFilters();

            left.Controls.Add(_grid);
            left.Controls.Add(tools);
            AddPane("table", "Profiles").Controls.Add(left);
        }

        // ==================== RIGHT: details | drawing | program ====================
        private void BuildRight()
        {
            var tips = Kaban.UI.Controls.UiStyle.Tips();
            Func<string, string, EventHandler, Button> Tool = (emoji, tip, onClick) =>
                Kaban.UI.Controls.UiStyle.ToolButton(emoji, tip, tips, onClick);

            // ---------------- DETAILS ----------------
            var details = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10), BackColor = Color.White };
            int y = 10;
            var idCols = new[,] { { "profile_code", "Profile Code" }, { "company_name", "Company" },
                                  { "serial", "Serial" }, { "profile_type", "Type" } };
            for (int i = 0; i < idCols.GetLength(0); i++)
            {
                details.Controls.Add(new Label { Text = idCols[i, 1] + ":", Left = 10, Top = y + 5, Width = 130, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F) });
                var tb = new TextBox { Left = 144, Top = y + 2, Width = 170, Font = new Font("Segoe UI", 9F) };
                details.Controls.Add(tb);
                _identityInputs[idCols[i, 0]] = tb;
                y += 30;
            }
            foreach (var f in DetailFields)
            {
                var lbl = new Label { Left = 10, Top = y + 5, Width = 130, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F) };
                details.Controls.Add(lbl);
                _detailLabels[f.Col] = lbl;
                var num = new NumericUpDown { Left = 144, Top = y + 2, Width = 90, Maximum = 100000, Minimum = -1000,
                                              Font = new Font("Segoe UI", 9F), TextAlign = HorizontalAlignment.Right };
                details.Controls.Add(num);
                _detailInputs[f.Col] = num;
                var suf = new Label { Left = 238, Top = y + 5, AutoSize = true, Font = new Font("Segoe UI", 9F), ForeColor = Color.DimGray };
                details.Controls.Add(suf);
                _detailSuffix[f.Col] = suf;
                y += 30;
            }
            details.Controls.Add(new Label { Text = "DXF File :", Left = 10, Top = y + 5, Width = 130, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F) });
            _txtDxf = new TextBox { Left = 144, Top = y + 2, Width = 190, Font = new Font("Segoe UI", 9F) };
            details.Controls.Add(_txtDxf);
            var btnBrowse = new Button { Text = "\u2026", Left = 338, Top = y + 1, Width = 34, Height = 24 };
            Kaban.UI.Controls.UiStyle.Round(btnBrowse);
            tips.SetToolTip(btnBrowse, "Browse for a DXF file");
            btnBrowse.Click += (s, e) => BrowseDxf();
            details.Controls.Add(btnBrowse);
            y += 36;
            var btnSaveDetails = Tool("\uD83D\uDCBE", "Save the profile (insert or update)", (s, e) => SaveProfile());
            btnSaveDetails.Left = 144; btnSaveDetails.Top = y;
            details.Controls.Add(btnSaveDetails);
            AddPane("details", "Details").Controls.Add(details);

            // ---------------- DRAWING (toolbar belongs to THIS pane only) ----------------
            var drawPane = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            // A ToolStrip, not a wrapping panel: when the pane is squeezed the extra
            // buttons move into the standard ">>" overflow menu instead of vanishing.
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
            Action<Control> addToBar = c => bar.Items.Add(new ToolStripControlHost(c) { AutoSize = false, Size = c.Size });
            addToBar(Tool("\u27F3", "Rotate 90\u00B0 clockwise", (s, e) => _viewer.Rotate90CW()));
            addToBar(Tool("\u27F2", "Rotate 90\u00B0 counter-clockwise", (s, e) => _viewer.Rotate90()));
            addToBar(Tool("\u21CB", "Mirror", (s, e) => _viewer.Mirror()));
            var btnMeasure = Tool("\uD83D\uDCCF", "Measure - RIGHT click two points; LEFT click copies X / Z", null);
            btnMeasure.Click += (s, e) =>
            {
                _viewer.MeasureMode = !_viewer.MeasureMode;
                _viewer.Cursor = _viewer.MeasureMode ? Cursors.Cross : Cursors.Default;
                btnMeasure.BackColor = _viewer.MeasureMode ? Kaban.UI.Controls.UiStyle.AccentBack : SystemColors.Control;
            };
            addToBar(btnMeasure);
            var btnRoute = Tool("\u27B0", "Show the border route (red)", null);
            btnRoute.Click += (s, e) =>
            {
                _viewer.ShowBorderRoute = !_viewer.ShowBorderRoute;
                btnRoute.BackColor = _viewer.ShowBorderRoute ? Kaban.UI.Controls.UiStyle.AccentBack : SystemColors.Control;
                _viewer.Invalidate();
            };
            addToBar(btnRoute);
            var btnLine = Tool("\u2215", "Add a line - two clicks, each snapping to the nearest element end", null);
            btnLine.Click += (s, e) =>
            {
                _viewer.AddLineMode = !_viewer.AddLineMode;
                btnLine.BackColor = _viewer.AddLineMode ? Color.FromArgb(210, 240, 210) : SystemColors.Control;
            };
            addToBar(btnLine);
            _btnDelSel = Tool("\uD83D\uDDD1", "Delete the SELECTED elements (drag right-to-left to catch everything a box touches; Del works too)", (s, e) => DeleteSelection());
            addToBar(_btnDelSel);
            addToBar(Tool("\uD83D\uDD0D", "Zoom in (the wheel zooms too)", (s, e) => _viewer.ZoomIn()));
            addToBar(Tool("\uD83D\uDD0E", "Zoom out", (s, e) => _viewer.ZoomOut()));
            addToBar(Tool("\u2317", "Fit to window", (s, e) => _viewer.ZoomFit()));
            _btnUndo = Tool("\u21B6", "Undo (Ctrl+Z)", (s, e) => { _viewer.Undo(); UpdateEditButtons(); });
            _btnRedo = Tool("\u21B7", "Redo (Ctrl+Y)", (s, e) => { _viewer.Redo(); UpdateEditButtons(); });
            addToBar(_btnUndo);
            addToBar(_btnRedo);
            _btnToolpath = Tool("\uD83D\uDFE2", "Show the tool path (green, drawn at the real tool width)", null);
            _btnToolpath.Click += (s, e) =>
            {
                _viewer.ShowToolpath = !_viewer.ShowToolpath;
                _btnToolpath.BackColor = _viewer.ShowToolpath ? Kaban.UI.Controls.UiStyle.AccentBack : SystemColors.Control;
                _viewer.Invalidate();
            };
            addToBar(_btnToolpath);
            addToBar(Tool("\u2705", "Use the SELECTED elements as the border route (explicit control over what is cut)",
                          (s, e) => UseSelectionAsBorder()));
            addToBar(Tool("\u26D3", "Select the whole CONNECTED run from what is selected",
                          (s, e) => { int n = _viewer.SelectConnected();
                                      _lblStatus.Text = n + " element(s) selected."; }));
            addToBar(Tool("\u2194", "Select everything BETWEEN the two selected elements",
                          (s, e) => { int n = _viewer.SelectBetween();
                                      _lblStatus.Text = n > 0 ? n + " element(s) selected."
                                          : "Select exactly two elements first."; }));
            addToBar(Tool("\u26D4", "Do NOT cut the selected elements (bypass them)",
                          (s, e) => { int n = _viewer.SetSelectedIncluded(false);
                                      _lblStatus.Text = n + " element(s) will not be cut."; GenerateGcode(); }));
            addToBar(Tool("\u2714", "Cut the selected elements again",
                          (s, e) => { int n = _viewer.SetSelectedIncluded(true);
                                      _lblStatus.Text = n + " element(s) back in the cut."; GenerateGcode(); }));
            addToBar(Tool("\u2702", "Split the selected element at the point you click next",
                          (s, e) => { _viewer.SplitMode = true; _lblStatus.Text = "Click where the element should be split."; }));
            addToBar(Tool("\u2699", "Cut settings for the SELECTED elements (depth, offsets, skip)",
                          (s, e) => EditElementCut()));
            addToBar(Tool("\u21BA", "Revert to the ORIGINAL DXF file", (s, e) => RevertDrawing()));

            _viewer.Dock = DockStyle.Fill;
            _viewer.GeometryChanged += (s, e) =>
            {
                UpdateEditButtons();
                // Rebuild the program straight away: adding or deleting geometry with
                // no visible effect on the program looked like the edit was ignored.
                if (_program != null && !string.IsNullOrEmpty(_txtGcode.Text)) GenerateGcode();
                else _lblStatus.Text = "Drawing edited - press the gear to build the program.";
            };
            _viewer.SelectionChanged += (s, e) => UpdateEditButtons();
            _viewer.ToolpathPicked += (s, pathIndex) => SelectGcodeLineFor(pathIndex);
            drawPane.Controls.Add(_viewer);
            drawPane.Controls.Add(bar);
            AddPane("drawing", "Drawing").Controls.Add(drawPane);

            // ---------------- PROGRAM ----------------
            var gPane = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var gbar = new ToolStrip
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
            Action<Control> addToGbar = c => gbar.Items.Add(new ToolStripControlHost(c) { AutoSize = false, Size = c.Size });

            // A caption and its input are ONE hosted control, so they can never be
            // separated - or have the label pushed into the overflow on its own.
            Func<string, NumericUpDown, Panel> field = (caption, num) =>
            {
                var holder = new Panel { Width = 152, Height = 30 };
                holder.Controls.Add(new Label
                { Text = caption, AutoSize = false, Width = 82, Height = 24, Top = 4, Left = 0,
                  TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F) });
                num.Left = 86; num.Top = 3; num.Width = 62;
                holder.Controls.Add(num);
                return holder;
            };
            _numToolRadius = new NumericUpDown { DecimalPlaces = 2, Minimum = 0, Maximum = 100, Value = 3, TextAlign = HorizontalAlignment.Right };
            _numToolRadius.ValueChanged += (s, e) => RegenerateIfProgramExists();
            addToGbar(field("Tool radius (mm) :", _numToolRadius));
            // "Z plunge" is gone: the depth IS the cut, and a second vertical figure
            // only invited the question of which one mattered.
            _numCutDepth = new NumericUpDown { DecimalPlaces = 2, Minimum = 0, Maximum = 500, Value = 5, TextAlign = HorizontalAlignment.Right };
            _numCutDepth.ValueChanged += (s, e) => RegenerateIfProgramExists();
            addToGbar(field("Cut depth (mm) :", _numCutDepth));
            addToGbar(Tool("\u2699", "Generate the program from the CURRENT drawing", (s, e) => GenerateGcode()));
            addToGbar(Tool("\uD83D\uDCBE", "Save the program with the profile (database)", (s, e) => SaveProfile()));
            addToGbar(Tool("\uD83D\uDCCB", "Copy the program", (s, e) =>
            { try { Clipboard.SetText(_txtGcode.Text ?? ""); _lblStatus.Text = "G-code copied."; } catch { } }));

            _txtGcode = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                HideSelection = false,
                Font = new Font("Consolas", 9F),
                BackColor = Color.FromArgb(28, 30, 34),
                ForeColor = Color.FromArgb(190, 230, 190)
            };
            _txtGcode.TextChanged += (s, e) => RefreshToolpath();
            _txtGcode.KeyUp += (s, e) => SyncFromGcodeCaret();
            _txtGcode.Click += (s, e) => SyncFromGcodeCaret();
            _txtGcode.MouseUp += (s, e) => SyncFromGcodeCaret();
            gPane.Controls.Add(_txtGcode);
            gPane.Controls.Add(gbar);
            AddPane("program", "G-Code").Controls.Add(gPane);

            _lblStatus.Dock = DockStyle.Bottom;
            _lblStatus.Height = 24;
            _lblStatus.Font = new Font("Segoe UI", 9F);
            _viewer.MeasureTaken += (s, d) => _lblStatus.Text = string.Format("Measured distance: {0:F2} units", d);
            Controls.Add(_lblStatus);
        }

        /// <summary>
        /// Per-element cut settings for whatever is selected: how deep to cut at the
        /// start and (optionally) the end, where to begin and stop along the element,
        /// and whether to cut it at all. Applied to every selected element, so a run
        /// of elements can be set in one go.
        /// </summary>
        private void EditElementCut()
        {
            var doc = _viewer.Document;
            if (doc == null || _viewer.SelectionCount == 0)
            { _lblStatus.Text = "Select the element(s) first."; return; }

            doc.EnsureMeta();
            int firstSel = -1;
            foreach (var i in _viewer.SelectedElements) { firstSel = i; break; }
            var current = doc.Meta[firstSel];

            using (var dlg = new Form
            {
                Text = "Cut settings (" + _viewer.SelectionCount + " element(s))",
                Width = 340, Height = 250,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false
            })
            {
                Kaban.UI.Controls.UiScale.ApplyOnLoad(dlg);
                int y = 14;
                Func<string, decimal, decimal, NumericUpDown> num = (caption, val, min) =>
                {
                    dlg.Controls.Add(new Label { Text = caption, Left = 12, Top = y + 4, Width = 170,
                                                 TextAlign = ContentAlignment.MiddleRight });
                    var n = new NumericUpDown
                    { Left = 190, Top = y, Width = 110, DecimalPlaces = 2, Minimum = min, Maximum = 500,
                      Value = val, TextAlign = HorizontalAlignment.Right };
                    dlg.Controls.Add(n);
                    y += 30;
                    return n;
                };

                var nDepth = num("Cut depth (mm) :", (decimal)(current.Depth ?? (double)_numCutDepth.Value), 0);
                var nDepthEnd = num("Depth at the end (mm) :",
                                    (decimal)(current.DepthEnd ?? current.Depth ?? (double)_numCutDepth.Value), 0);
                var nStart = num("Start offset (mm) :", (decimal)current.StartOffset, -500);
                var nEnd = num("End offset (mm) :", (decimal)current.EndOffset, -500);

                var chkSkip = new CheckBox
                { Text = "Do not cut this element", Left = 190, Top = y, Width = 200, Checked = !current.Included };
                dlg.Controls.Add(chkSkip);
                y += 30;

                var ok = new Button { Text = "OK", Left = 190, Top = y, Width = 110, Height = 30,
                                      DialogResult = DialogResult.OK };
                Kaban.UI.Controls.UiStyle.Round(ok);
                dlg.Controls.Add(ok);
                dlg.AcceptButton = ok;
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

                foreach (var i in _viewer.SelectedElements)
                {
                    if (i < 0 || i >= doc.Meta.Count) continue;
                    var m = doc.Meta[i];
                    m.Depth = (double)nDepth.Value;
                    m.DepthEnd = Math.Abs((double)nDepthEnd.Value - (double)nDepth.Value) < 0.001
                                 ? (double?)null : (double)nDepthEnd.Value;
                    m.StartOffset = (double)nStart.Value;
                    m.EndOffset = (double)nEnd.Value;
                    m.Included = !chkSkip.Checked;
                }
            }
            GenerateGcode();
            _viewer.Invalidate();
            _lblStatus.Text = "Cut settings applied to " + _viewer.SelectionCount + " element(s).";
        }

        /// <summary>Rebuilds the program when the tool numbers change, if one exists.</summary>
        private void RegenerateIfProgramExists()
        {
            _viewer.ToolDiameter = (double)_numToolRadius.Value * 2;
            // Rebuild whenever there IS a drawing: requiring a previously generated
            // program meant a profile whose G-code came from the database ignored
            // every change of tool radius or depth.
            if (_viewer.Document != null && _viewer.Document.Polylines.Count > 0) GenerateGcode();
            else _viewer.Invalidate();
        }

        /// <summary>
        /// Takes the CURRENT selection as the border route. Chaining heuristics
        /// cannot know which contour the operator means once a drawing has been
        /// edited, so this gives explicit control: select the elements to cut,
        /// press the tick, and the program follows exactly those.
        /// </summary>
        private void UseSelectionAsBorder()
        {
            if (_viewer.Document == null) return;
            int n = _viewer.SetBorderFromSelection();
            if (n > 0)
            {
                _viewer.ShowBorderRoute = true;
                if (_btnToolpath != null) _btnToolpath.BackColor = Kaban.UI.Controls.UiStyle.AccentBack;
                GenerateGcode();                 // the program must follow the new border at once
                _lblStatus.Text = n + " element(s) fixed as the border route - program rebuilt.";
            }
            else _lblStatus.Text = "Select the elements that form the border first (click, or drag a box).";
            _viewer.Invalidate();
        }

        // ==================== unit-aware presentation ====================
        private void RefreshUnitPresentation()
        {
            foreach (var f in DetailFields)
            {
                // clean parameter name; the unit sits AFTER the value instead
                _detailLabels[f.Col].Text = f.Caption + ":";
                _detailSuffix[f.Col].Text = UnitManager.GetActiveSuffix(f.Unit);
                _detailInputs[f.Col].DecimalPlaces = 1 + Math.Max(0, UnitManager.GetActiveExtraDecimals(f.Unit));
            }
            ShowSelected();   // re-convert the displayed values
        }

        // ==================== data operations ====================
        private void OnHeaderClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // column chooser + filter visibility, like the legacy HMI's header menu
                var menu = new ContextMenuStrip();
                foreach (DataGridViewColumn c in _grid.Columns)
                {
                    var captured = c;
                    var item = new ToolStripMenuItem(c.HeaderText) { Checked = c.Visible, CheckOnClick = true };
                    item.CheckedChanged += (s2, e2) => { captured.Visible = item.Checked; AlignFilters(); };
                    menu.Items.Add(item);
                }
                menu.Items.Add(new ToolStripSeparator());
                var fi = new ToolStripMenuItem("Show filters") { Checked = _filtersVisible, CheckOnClick = true };
                fi.CheckedChanged += (s2, e2) => SetFiltersVisible(fi.Checked);
                menu.Items.Add(fi);
                menu.Show(Cursor.Position);
                return;
            }

            if (e.ColumnIndex < 0) return;
            string col = _grid.Columns[e.ColumnIndex].Name;
            bool additive = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

            int existing = _sortKeys.FindIndex(k => k.Key == col);
            bool wasAscending = existing >= 0 && _sortKeys[existing].Value;

            // The clicked column becomes the HIGHEST priority and the previous ones
            // slide down to second, third ... instead of being thrown away. Clicking
            // the column that is already first just flips its direction.
            bool ascending = (existing == 0) ? !wasAscending : (existing > 0 ? wasAscending : true);
            if (existing >= 0) _sortKeys.RemoveAt(existing);
            _sortKeys.Insert(0, new KeyValuePair<string, bool>(col, ascending));
            if (!additive && _sortKeys.Count > 4) _sortKeys.RemoveRange(4, _sortKeys.Count - 4);

            UpdateHeaderArrows();

            ApplyFilterAndSort();
        }

        private void ApplyFilterAndSort()
        {
            var parts = new List<string>();
            foreach (var kv in _filters)
                if (!string.IsNullOrWhiteSpace(kv.Value.Text))
                    parts.Add(string.Format("{0} LIKE '%{1}%'", kv.Key, kv.Value.Text.Replace("'", "''")));
            _view.RowFilter = string.Join(" AND ", parts);

            var keys = new List<string>();
            foreach (var g in _groupCols)
            {
                // group columns LEAD the sort (so groups stay intact); their own
                // direction is honoured when the user clicks that header
                int gi = _sortKeys.FindIndex(k => k.Key == g);
                bool gAsc = gi < 0 || _sortKeys[gi].Value;
                keys.Add(g + (gAsc ? " ASC" : " DESC"));
            }
            foreach (var k in _sortKeys)
                if (!_groupCols.Contains(k.Key)) keys.Add(k.Key + (k.Value ? " ASC" : " DESC"));
            _view.Sort = string.Join(", ", keys);
            _grid.RowHeadersVisible = _groupCols.Count > 0;   // indent only while grouped
            UpdateHeaderArrows();
            Repopulate();
        }

        /// <summary>
        /// Shows the sort direction in the column caption itself (up/down arrow),
        /// which reads better on a touch panel than the small native glyph.
        /// </summary>
        private void UpdateHeaderArrows()
        {
            // Only the MOST RECENTLY sorted column carries an arrow. The earlier
            // sorts are still applied underneath, but marking them all made it
            // impossible to see which click had just taken effect.
            string leading = _sortKeys.Count > 0 ? _sortKeys[0].Key : null;
            bool leadingAsc = _sortKeys.Count > 0 && _sortKeys[0].Value;

            foreach (DataGridViewColumn c in _grid.Columns)
            {
                string baseText;
                if (!_baseHeaders.TryGetValue(c.Name, out baseText)) continue;
                c.HeaderText = (c.Name == leading)
                    ? baseText + (leadingAsc ? "  \u25B2" : "  \u25BC")
                    : baseText;
            }
        }

        private readonly Dictionary<string, string> _baseHeaders = new Dictionary<string, string>();

        /// <summary>Testability hook: set the grouping columns programmatically.</summary>
        internal void SetGroupColumns(IEnumerable<string> cols)
        {
            _groupCols.Clear();
            _groupCols.AddRange(cols);
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Fills the grid manually (unbound): group-CAPTION rows carry the joined
        /// group values ("Company: X - Serial: Y") and the member rows follow.
        /// A data row's Tag is its DataRowView; captions have a null Tag.
        /// </summary>
        private void Repopulate()
        {
            // remember what was selected: regrouping/sorting rebuilds every row, and
            // the operator's current profile must not be lost when they reorganise
            if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is DataRowView keep)
                _pendingSelectCode = Convert.ToString(keep["profile_code"]);

            _repopulating = true;
            _grid.SuspendLayout();
            _grid.Rows.Clear();
            string lastKey = null;
            var capStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(232, 236, 241),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 44, 52)
            };
            // use the PLAIN caption: HeaderText may carry a sort arrow, which must
            // not leak into the group caption text ("Company  \u25B2: DECEUNINCK")
            var headerByCol = new Dictionary<string, string>();
            foreach (DataGridViewColumn c in _grid.Columns)
            {
                string plain;
                headerByCol[c.Name] = _baseHeaders.TryGetValue(c.Name, out plain) ? plain : c.HeaderText;
            }

            foreach (DataRowView rv in _view)
            {
                if (_groupCols.Count > 0)
                {
                    var partsCap = new List<string>();
                    foreach (var g in _groupCols)
                        partsCap.Add(headerByCol[g] + ": " + Convert.ToString(rv[g]));
                    string key = string.Join(" | ", partsCap);
                    if (key != lastKey)
                    {
                        var capCells = new object[_grid.Columns.Count];
                        capCells[0] = key;
                        int ci = _grid.Rows.Add(capCells);
                        var cap = _grid.Rows[ci];
                        cap.Tag = null;
                        cap.ReadOnly = true;
                        cap.DefaultCellStyle = capStyle;
                        lastKey = key;
                    }
                }
                var cells = new object[_grid.Columns.Count];
                for (int c = 0; c < _grid.Columns.Count; c++)
                    cells[c] = CellText(rv, _grid.Columns[c].Name);
                int ri = _grid.Rows.Add(cells);
                _grid.Rows[ri].Tag = rv;
            }
            _grid.ResumeLayout();
            _repopulating = false;
            AlignFilters();
            RestoreSelection();
        }

        /// <summary>Re-selects the remembered profile after the rows are rebuilt.</summary>
        private void RestoreSelection()
        {
            if (string.IsNullOrEmpty(_pendingSelectCode)) return;
            foreach (DataGridViewRow gr in _grid.Rows)
            {
                var rv = gr.Tag as DataRowView;
                if (rv == null) continue;
                if (Convert.ToString(rv["profile_code"]) == _pendingSelectCode)
                {
                    gr.Selected = true;
                    if (gr.Index >= 0 && _grid.Rows.Count > gr.Index)
                        try { _grid.FirstDisplayedScrollingRowIndex = gr.Index; } catch { }
                    return;
                }
            }
        }

        /// <summary>
        /// Draws a group-caption row as ONE band across the full grid width.
        /// This runs in RowPrePaint, NOT CellPainting: cell painting is clipped to
        /// each cell, which is why the caption previously appeared only inside the
        /// first column and vanished when another column was clicked.
        /// </summary>
        private void PaintCaptionRow(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            var row = _grid.Rows[e.RowIndex];
            if (row.Tag != null) return;                       // data row: normal painting

            var band = new Rectangle(0, e.RowBounds.Top, _grid.Width, e.RowBounds.Height);
            using (var back = new SolidBrush(Color.FromArgb(226, 231, 238)))
                e.Graphics.FillRectangle(back, band);
            using (var line = new Pen(Color.FromArgb(150, 158, 170)))
                e.Graphics.DrawLine(line, band.Left, band.Bottom - 1, band.Right, band.Bottom - 1);

            TextRenderer.DrawText(e.Graphics, Convert.ToString(row.Cells[0].Value), _captionFont,
                new Rectangle(band.Left + 6, band.Top, band.Width - 12, band.Height),
                Color.FromArgb(25, 30, 38),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            e.Handled = true;                                  // no cells, no borders, no selection wash
        }

        private readonly Font _captionFont = new Font("Segoe UI", 9F, FontStyle.Bold);

        /// <summary>
        /// Resets ONLY the table: grouping, sorting, filters and the columns go back
        /// to their defaults. The pane positions are left alone - that is what the
        /// Default arrangement is for.
        /// </summary>
        private void ResetTableView()
        {
            _groupCols.Clear();
            foreach (var it in _groupItems) it.Checked = false;
            _btnGroup.Text = "Group \u25BE";
            _sortKeys.Clear();
            foreach (var kv in _filters) kv.Value.Text = "";
            SetFiltersVisible(true);
            int order = 0;
            foreach (var name in DefaultColumnOrder)
            {
                var col = _grid.Columns[name];
                if (col == null) continue;
                col.Visible = DefaultVisibleColumns.Contains(name);
                col.Width = 110;
                col.DisplayIndex = order++;
            }
            UpdateHeaderArrows();
            ApplyFilterAndSort();
            SaveWorkingState();
            _lblStatus.Text = "Table view reset.";
        }

        /// <summary>Clears grouping, sorting and filters in one click.</summary>
        private void ResetView()
        {
            foreach (var it in _groupItems) it.Checked = false;   // also clears _groupCols
            _groupCols.Clear();
            _sortKeys.Clear();
            foreach (var kv in _filters) kv.Value.Text = "";
            _btnGroup.Text = "Group \u25BE";
            ApplyFilterAndSort();                              // also hides the indent column
            _lblStatus.Text = "View reset.";
        }

        /// <summary>
        /// Text for one cell. Parameters are shown in the operator's ACTIVE units,
        /// like everywhere else in the HMI, while the database keeps reference units.
        /// </summary>
        private string CellText(DataRowView rv, string column)
        {
            foreach (var f in DetailFields)
                if (f.Col == column)
                {
                    decimal reference = rv[column] is decimal dv ? dv : 0;
                    double shown = UnitManager.ToDisplay(f.Unit, (double)reference);
                    int extra = Math.Max(0, UnitManager.GetActiveExtraDecimals(f.Unit));
                    return shown.ToString("F" + (1 + extra));
                }
            return Convert.ToString(rv[column]);
        }

        /// <summary>Places each filter box exactly under its (visible) column.</summary>
        /// <summary>Column order offered by the table (documents excluded on purpose).</summary>
        private static readonly string[] DefaultColumnOrder =
        {
            "profile_code", "company_name", "serial", "profile_type",
            "width_mm", "height_mm", "melting_s", "welding_s", "temperature_c", "clamping_mm"
        };

        private static readonly System.Collections.Generic.HashSet<string> DefaultVisibleColumns =
            new System.Collections.Generic.HashSet<string>
            { "profile_code", "company_name", "serial", "profile_type" };

        private static readonly Dictionary<string, string> ColumnCaptions = new Dictionary<string, string>
        {
            { "profile_code", "Profile Code" }, { "company_name", "Company" },
            { "serial", "Serial" }, { "profile_type", "Type" },
            { "width_mm", "Width" }, { "height_mm", "Height" },
            { "melting_s", "Melting Time" }, { "welding_s", "Welding Time" },
            { "temperature_c", "Temperature" }, { "clamping_mm", "Clamping Height" }
        };

        // 24 was the cap height only - g, p and y were being clipped. The extra
        // pixels sit UNDER the text, where the filter row then starts.
        private const int HeaderTextHeight = 30;
        private const int FilterRowHeight = 26;

        private void AlignFilters()
        {
            foreach (var kv in _filters)
            {
                var col = _grid.Columns[kv.Key];
                if (col == null || !col.Visible || !_filtersVisible) { kv.Value.Visible = false; continue; }
                var rect = _grid.GetColumnDisplayRectangle(col.Index, false);
                kv.Value.Visible = rect.Width > 0;
                kv.Value.SetBounds(rect.Left + 1, HeaderTextHeight + 1,
                                   Math.Max(20, rect.Width - 3), FilterRowHeight - 4);
                kv.Value.BringToFront();
            }
        }

        private bool _filtersVisible = true;

        /// <summary>Shows or hides the in-header filter row, shrinking the band with it.</summary>
        private void SetFiltersVisible(bool show)
        {
            _filtersVisible = show;
            _grid.ColumnHeadersHeight = HeaderTextHeight + (show ? FilterRowHeight : 0);
            AlignFilters();
        }

        private DataRowView SelectedRow
        {
            get
            {
                if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is DataRowView rv)
                    return rv;                                   // caption rows have a null Tag
                return _view.Count > 0 ? _view[0] : null;
            }
        }

        private void ShowSelected()
        {
            var row = SelectedRow;
            if (row == null) return;

            foreach (var kv in _identityInputs) kv.Value.Text = Convert.ToString(row[kv.Key]);
            _txtDxf.Text = Convert.ToString(row["dxf_file"]);
            if (_txtGcode != null) _txtGcode.Text = Convert.ToString(row["gcode"]);

            foreach (var f in DetailFields)
            {
                decimal reference = row[f.Col] is decimal d ? d : 0;
                // DB stores REFERENCE units (mm / s / degC); show the ACTIVE unit,
                // exactly like the PLC numeric controls.
                decimal display = (decimal)UnitManager.ToDisplay(f.Unit, (double)reference);
                var input = _detailInputs[f.Col];
                input.Value = Math.Min(input.Maximum, Math.Max(input.Minimum, display));
            }

            LoadDxfIntoViewer(_txtDxf.Text);
        }

        private void LoadDxfIntoViewer(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return;
            string path = Path.IsPathRooted(file) ? file
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
            if (File.Exists(path))
            {
                try
                {
                    _viewer.LoadFile(path);
                    _loadedDxfPath = path;               // original, for Revert
                    if (_numToolRadius != null) _viewer.ToolDiameter = (double)_numToolRadius.Value * 2;
                }
                catch (Exception ex) { _lblStatus.Text = "DXF load error: " + ex.Message; }
            }
            else _lblStatus.Text = "DXF not found: " + file;
        }

        private void BrowseDxf()
        {
            using (var dlg = new OpenFileDialog { Filter = "DXF drawings (*.dxf)|*.dxf|All files (*.*)|*.*" })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string chosen = dlg.FileName;
                // keep paths portable when the file already lives under the app
                _txtDxf.Text = chosen.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase)
                    ? chosen.Substring(baseDir.Length)
                    : chosen;
                LoadDxfIntoViewer(_txtDxf.Text);
            }
        }

        private void AddProfile()
        {
            var row = _table.NewRow();
            row["profile_code"] = "NEW-" + DateTime.Now.ToString("HHmmss");
            row["company_name"] = "";
            row["serial"] = "";
            row["profile_type"] = "";
            row["dxf_file"] = "";
            row["gcode"] = "";
            foreach (var f in DetailFields) row[f.Col] = 0m;
            _table.Rows.Add(row);

            // select it so the Details tab edits the new profile
            Repopulate();
            foreach (DataGridViewRow gr in _grid.Rows)
                if ((gr.Tag as DataRowView)?.Row == row) { gr.Selected = true; break; }
            ShowSelected();
            _lblStatus.Text = "New profile added - fill the details and press Save.";
        }

        private void DeleteProfile()
        {
            var row = SelectedRow;
            if (row == null) return;
            string code = Convert.ToString(row["profile_code"]);
            if (MessageBox.Show(string.Format("Delete profile {0}?", code), "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            if (_fromDb)
            {
                try { _db.Execute("DELETE FROM profiles WHERE profile_code=@p", ("@p", code)); }
                catch (Exception ex) { _lblStatus.Text = "Delete failed: " + ex.Message; return; }
            }
            row.Row.Delete();
            _table.AcceptChanges();
            Repopulate();
            _lblStatus.Text = string.Format("Profile {0} deleted.", code);
        }

        private void SaveProfile()
        {
            var row = SelectedRow;
            if (row == null) return;

            string oldCode = Convert.ToString(row["profile_code"]);
            foreach (var kv in _identityInputs) row[kv.Key] = kv.Value.Text.Trim();
            row["dxf_file"] = _txtDxf.Text.Trim();
            if (_txtGcode != null) row["gcode"] = _txtGcode.Text;

            foreach (var f in DetailFields)
            {
                // ACTIVE unit back to REFERENCE unit for the database
                double display = (double)_detailInputs[f.Col].Value;
                row[f.Col] = (decimal)UnitManager.ToReference(f.Unit, display);
            }

            if (_fromDb)
            {
                try
                {
                    _db.Execute(@"INSERT INTO profiles (profile_code, company_name, serial, profile_type, dxf_file,
                                    width_mm, height_mm, melting_s, welding_s, temperature_c, clamping_mm, gcode)
                                  VALUES (@p,@c,@s,@t,@d,@w,@h,@m,@we,@tc,@cl,@g)
                                  ON CONFLICT (profile_code) DO UPDATE SET company_name=@c, serial=@s,
                                    profile_type=@t, dxf_file=@d, width_mm=@w, height_mm=@h, melting_s=@m,
                                    welding_s=@we, temperature_c=@tc, clamping_mm=@cl, gcode=@g",
                        ("@p", Convert.ToString(row["profile_code"])), ("@c", Convert.ToString(row["company_name"])),
                        ("@s", Convert.ToString(row["serial"])), ("@t", Convert.ToString(row["profile_type"])),
                        ("@d", Convert.ToString(row["dxf_file"])),
                        ("@w", row["width_mm"]), ("@h", row["height_mm"]), ("@m", row["melting_s"]),
                        ("@we", row["welding_s"]), ("@tc", row["temperature_c"]), ("@cl", row["clamping_mm"]),
                        ("@g", Convert.ToString(row["gcode"])));
                    if (oldCode != Convert.ToString(row["profile_code"]) && oldCode.StartsWith("NEW-") == false)
                        _db.Execute("DELETE FROM profiles WHERE profile_code=@p", ("@p", oldCode));
                    _lblStatus.Text = "Saved to database.";
                }
                catch (Exception ex) { _lblStatus.Text = "Save failed: " + ex.Message; }
            }
            else _lblStatus.Text = "Saved (in memory - database not connected).";

            // The grid is UNBOUND (it renders group captions), so the edited values
            // do not appear until it is rebuilt - previously they only showed up
            // after a grouping or sorting action.
            Repopulate();
        }

        /// <summary>
        /// Throws away every edit and reloads the ORIGINAL DXF from disk. Edits only
        /// ever live in memory - the source file is never written - so the original
        /// is always available to come back to.
        /// </summary>
        private void RevertDrawing()
        {
            if (string.IsNullOrEmpty(_loadedDxfPath) || !File.Exists(_loadedDxfPath))
            { _lblStatus.Text = "No original file to revert to."; return; }
            if (MessageBox.Show("Discard all drawing edits and reload the original DXF?",
                    "Revert", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _viewer.LoadFile(_loadedDxfPath);
            _viewer.ToolDiameter = (double)_numToolRadius.Value * 2;
            UpdateEditButtons();
            _lblStatus.Text = "Reverted to the original DXF.";
        }

        /// <summary>
        /// Line/char conversions done on OUR line array. Mixing TextBox's
        /// GetFirstCharIndexFromLine with GetLineFromCharIndex produced an
        /// off-by-one (they disagree about line terminators), which sent every
        /// click to the neighbouring program line.
        /// </summary>
        private static int LineStartIndex(string[] lines, int line)
        {
            int idx = 0;
            for (int i = 0; i < line && i < lines.Length; i++) idx += lines[i].Length + Environment.NewLine.Length;
            return idx;
        }

        private static int LineFromIndex(string[] lines, int charIndex)
        {
            int idx = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                int next = idx + lines[i].Length + Environment.NewLine.Length;
                if (charIndex < next) return i;
                idx = next;
            }
            return Math.Max(0, lines.Length - 1);
        }

        /// <summary>DRAWING -> G-CODE: selects the program line that cut that point.</summary>
        private void SelectGcodeLineFor(int pathIndex)
        {
            if (_txtGcode == null || pathIndex < 0 || pathIndex >= _pathLineIndex.Count) return;
            int line = _pathLineIndex[pathIndex];

            string[] all = _txtGcode.Lines;
            if (line < 0 || line >= all.Length) return;
            int start = LineStartIndex(all, line);
            int len = all[line].Length;
            _txtGcode.Select(start, len);
            _txtGcode.ScrollToCaret();
            _lblStatus.Text = "Program line " + (line + 1) + ": " + (line < all.Length ? all[line] : "");
        }

        /// <summary>G-CODE -> DRAWING: highlights the geometry for the caret's line.</summary>
        private void SyncFromGcodeCaret()
        {
            if (_txtGcode == null || _pathLineIndex.Count == 0) return;
            int line = LineFromIndex(_txtGcode.Lines, _txtGcode.SelectionStart);
            int idx = _pathLineIndex.IndexOf(line);
            if (idx < 0)                                   // header/comment: use the next move
                for (int i = 0; i < _pathLineIndex.Count; i++)
                    if (_pathLineIndex[i] >= line) { idx = i; break; }
            if (idx < 0) return;
            _viewer.HighlightedRoutePoint = idx;
            _viewer.ShowToolpath = true;
            if (_btnToolpath != null) _btnToolpath.BackColor = Kaban.UI.Controls.UiStyle.AccentBack;
            _viewer.Invalidate();
        }

        private void UpdateEditButtons()
        {
            if (_btnUndo != null) _btnUndo.Enabled = _viewer.CanUndo;
            if (_btnRedo != null) _btnRedo.Enabled = _viewer.CanRedo;
            // The delete button stays ENABLED: disabling it made it look broken when
            // selection was not working. It now says what to do instead.
            if (_btnDelSel != null)
                _btnDelSel.BackColor = _viewer.SelectionCount > 0
                    ? Color.FromArgb(250, 214, 214) : SystemColors.Control;
        }

        private void DeleteSelection()
        {
            int n = _viewer.DeleteSelected();
            _lblStatus.Text = n > 0 ? n + " element(s) deleted." : "Select elements first (click, or drag a region).";
        }

        /// <summary>
        /// Builds the drawn tool path from the program TEXT, remembering which code
        /// line produced each point. The two used to live in different index spaces
        /// (373 route points vs ~100 program lines), which is why clicking the path
        /// jumped to the wrong line - now one array indexes both.
        /// </summary>
        private void RefreshToolpath()
        {
            _pathLineIndex.Clear();
            var pts = new List<Kaban.UI.Dxf.DxfDocument.PointD>();
            if (_txtGcode != null)
            {
                string[] lines = _txtGcode.Lines;
                double x = 0, y = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("(")) continue;
                    if (!(line.StartsWith("G0") || line.StartsWith("G1") ||
                          line.StartsWith("G2") || line.StartsWith("G3"))) continue;
                    bool moved = false;
                    foreach (var tok in line.Split(' '))
                    {
                        if (tok.Length < 2) continue;
                        double v;
                        if (!double.TryParse(tok.Substring(1), System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out v)) continue;
                        if (tok[0] == 'X') { x = v; moved = true; }
                        else if (tok[0] == 'Y') { y = v; moved = true; }
                    }
                    if (!moved) continue;
                    pts.Add(new Kaban.UI.Dxf.DxfDocument.PointD(x, y));
                    _pathLineIndex.Add(i);                 // drawn point i  <->  code line
                }
            }
            // Show where the cutter EDGE runs, not its centre line: the program uses
            // G41 so the controller offsets by the radius - the drawing now does the
            // same, which is what makes the tool "step into" the border on screen.
            // The programmed contour is already offset INTO the profile by the cut
            // depth, and G41 puts the cutter centre one radius back OUT again - so the
            // drawn band bites exactly "cut into profile" deep instead of always
            // removing a full tool diameter.
            double radius = _numToolRadius != null ? (double)_numToolRadius.Value : 0;
            _viewer.Toolpath = radius > 0.0001
                ? Kaban.UI.Dxf.DxfDocument.OffsetPolyline(pts, -radius)
                : pts;
            _viewer.HighlightedRoutePoint = -1;
            _viewer.Invalidate();
        }

        /// <summary>
        /// Builds the border program from the current drawing and puts it in the
        /// G-Code tab (and, on Save, in the profile row) - the program belongs WITH
        /// the profile in the database rather than in a loose output file.
        /// </summary>
        private void GenerateGcode()
        {
            if (_viewer.Document == null) { _lblStatus.Text = "Load a DXF first."; return; }
            // generated from the CURRENT (edited) drawing, so added/removed lines count
            // every figure on this page is in MILLIMETRES - the drawing, the
            // parameters and the program all share the DXF's own units
            _program = _viewer.Document.GenerateBorderProgram(
                (double)_numToolRadius.Value, (double)_numCutDepth.Value, 800, 5, true);
            _txtGcode.Text = _program.Code;              // TextChanged rebuilds path + line map
            _viewer.ToolDiameter = (double)_numToolRadius.Value * 2;
            _viewer.ShowToolpath = true;
            if (_btnToolpath != null) _btnToolpath.BackColor = Kaban.UI.Controls.UiStyle.AccentBack;
            _viewer.Invalidate();
            int arcCount = 0, moveCount = 0;
            foreach (var l in _program.Code.Split('\n'))
            {
                var t = l.TrimStart();
                if (t.StartsWith("G02") || t.StartsWith("G03")) arcCount++;
                else if (t.StartsWith("G1 X") || t.StartsWith("G0 X")) moveCount++;
            }
            _lblStatus.Text = string.Format(
                "Program rebuilt: tool radius {0:F2} mm, cut depth {1:F2} mm, {2} arcs, {3} moves.",
                _numToolRadius.Value, _numCutDepth.Value, arcCount, moveCount);
        }

        private void LoadRows()
        {
            _db = new HmiDatabase("localhost", "kaban", "kaban", "kaban");
            _fromDb = false;
            try
            {
                if (_db.IsAvailable)
                {
                    foreach (var r in _db.Query(@"SELECT profile_code, company_name, serial, profile_type, dxf_file,
                                                  width_mm, height_mm, melting_s, welding_s, temperature_c, clamping_mm,
                                                  gcode FROM profiles ORDER BY profile_code"))
                    {
                        var row = _table.NewRow();
                        foreach (DataColumn c in _table.Columns)
                            row[c.ColumnName] = r.TryGetValue(c.ColumnName, out var v) && v != null ? v : (object)DBNull.Value;
                        _table.Rows.Add(row);
                        _fromDb = true;
                    }
                }
            }
            catch { }

            if (!_fromDb)
            {
                Action<string, string, string, string, decimal, decimal> add = (code, comp, ser, type, w, h) =>
                    _table.Rows.Add(code, comp, ser, type, @"Profiles\82_Disa_Kapi.dxf", "", w, h, 20m, 30m, 240m, 12m);
                add("0001000007", "DECEUNINCK", "Elegant", "KASA", 72.0m, 76.5m);
                add("0001000104", "DECEUNINCK", "Elegant", "KANAT", 70.0m, 78.0m);
                add("0001020307", "DECEUNINCK", "TecnoCorr2", "Hoja Puerta", 82.0m, 115.0m);
                _lblStatus.Text = "Database not connected - showing built-in sample profiles.";
            }

            LoadViewSettings();     // grouping / sorting / filters / last profile
            ApplyFilterAndSort();   // fills the grid (unbound) incl. group captions
            RefreshUnitPresentation();
            RestoreSelection();
            if (SelectedRow == null && _grid.Rows.Count > 0) _grid.Rows[0].Selected = true;
            ShowSelected();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SaveWorkingState();
                SaveViewSettings();
                if (_onUnitChanged != null) UnitManager.UnitChanged -= _onUnitChanged;
            }
            base.Dispose(disposing);
        }

        // ---------------- view settings persistence ----------------
        private static string ViewSettingsPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                    "KabanHMI", "ProfilesView.cfg");
            }
        }

        /// <summary>Keeps grouping, sorting, filters and the open profile between sessions.</summary>
        private void SaveViewSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ViewSettingsPath));
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("groups=" + string.Join(",", _groupCols));
                var sorts = new List<string>();
                foreach (var k in _sortKeys) sorts.Add(k.Key + ":" + (k.Value ? "A" : "D"));
                sb.AppendLine("sorts=" + string.Join(",", sorts));
                foreach (var kv in _filters)
                    if (!string.IsNullOrWhiteSpace(kv.Value.Text)) sb.AppendLine("filter." + kv.Key + "=" + kv.Value.Text);
                sb.AppendLine("selected=" + (_pendingSelectCode ?? ""));
                File.WriteAllText(ViewSettingsPath, sb.ToString());
            }
            catch { /* a saved view is a convenience, never a failure */ }
        }

        private void LoadViewSettings()
        {
            try
            {
                if (!File.Exists(ViewSettingsPath)) return;
                foreach (var line in File.ReadAllLines(ViewSettingsPath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq), val = line.Substring(eq + 1);
                    if (key == "groups" && val.Length > 0)
                    {
                        foreach (var g in val.Split(','))
                        {
                            if (g.Length == 0) continue;
                            _groupCols.Add(g);
                            foreach (var it in _groupItems)
                                if (GroupColumnOf(it) == g) it.Checked = true;
                        }
                    }
                    else if (key == "sorts" && val.Length > 0)
                    {
                        foreach (var srt in val.Split(','))
                        {
                            var parts = srt.Split(':');
                            if (parts.Length == 2) _sortKeys.Add(new KeyValuePair<string, bool>(parts[0], parts[1] == "A"));
                        }
                    }
                    else if (key.StartsWith("filter."))
                    {
                        TextBox tb;
                        if (_filters.TryGetValue(key.Substring(7), out tb)) tb.Text = val;
                    }
                    else if (key == "selected" && val.Length > 0) _pendingSelectCode = val;
                }
                if (_groupCols.Count > 0) _btnGroup.Text = "Group (" + _groupCols.Count + ") \u25BE";
            }
            catch { }
        }

        private string GroupColumnOf(ToolStripMenuItem item)
        {
            foreach (var kv in ColumnCaptions) if (kv.Value == item.Text) return kv.Key;
            return "";
        }
    }
}

