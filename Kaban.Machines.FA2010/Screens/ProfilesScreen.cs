using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.Profiles.Geometry;
using Kaban.Profiles.Machining;
using Kaban.UI.Controls;
using Kaban.UI.Dxf;
using Kaban.UI.Profiles;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// PROFILES page, v4 - the profile and accessory library at work.
    ///
    /// What it is for (the design document "Profiles, accessories and G-code"):
    /// every profile the machine processes, filed vendor -> series -> variant ->
    /// group, with its DXF section; the REFERENCE POINTS on that section (outside
    /// and inside face, hardware axis ...), found on the drawing and confirmed by
    /// a person; the ACCESSORIES its group takes, each operation placed on the
    /// section from a reference plus an offset plus the corrections of its series,
    /// group and profile; a TRIAL mode that moves a placement a little and keeps
    /// the move as a correction; and the G-CODE made from all of it - the side
    /// machining program (with the best mounted tool, and entry speeds that follow
    /// the walls the drawing shows) and the end milling program around the profile.
    ///
    /// Panes, each moved, sized and minimised freely, the arrangement saved by
    /// name: the table of profiles | the drawing | the profile's details and
    /// reference points | its accessories and their placements | its programs.
    ///
    /// NOTHING HERE IS SENT TO THE PLC. The page makes data and program files.
    /// Changing the library needs the Maintenance level; below it the page shows
    /// everything and changes nothing.
    /// </summary>
    public partial class ProfilesScreen : UserControl
    {
        // ---- what is shown ---------------------------------------------------------
        private Library.ProfileInfo _current;
        private Section _section;
        private List<Library.ReferenceInfo> _references = new List<Library.ReferenceInfo>();
        private List<Library.PlacementInfo> _placements = new List<Library.PlacementInfo>();
        private long _instance;
        private bool _libraryOk, _libraryTried;
        private string _libraryWhy = "";

        private readonly ToolTip _tips = UiStyle.Tips();
        private readonly Label _lblStatus = new Label();
        /// <summary>Controls that change the library: enabled at the Maintenance level only.</summary>
        private readonly List<Control> _editControls = new List<Control>();
        private EventHandler _onUserChanged;

        // ---- the table -------------------------------------------------------------------
        private readonly DataTable _table = new DataTable();
        private readonly DataView _view;
        private readonly DataGridView _grid = new DataGridView();
        private readonly List<KeyValuePair<string, bool>> _sortKeys = new List<KeyValuePair<string, bool>>();
        private readonly Dictionary<string, TextBox> _filters = new Dictionary<string, TextBox>();
        private Button _btnGroup, _btnInactive;
        private readonly List<string> _groupCols = new List<string>();
        private readonly List<ToolStripMenuItem> _groupItems = new List<ToolStripMenuItem>();
        private string _pendingSelectId;               // survives regrouping and sorting
        private bool _repopulating, _showInactive;

        // ---- panes and arrangements ----------------------------------------------------------
        private Panel _canvas;
        private readonly Dictionary<string, FloatingPaneHMI> _panes = new Dictionary<string, FloatingPaneHMI>();
        private ComboBox _cmbLayout;
        private bool _loadingLayout;
        /// <summary>
        /// True once the saved arrangement has actually been applied. Nothing is
        /// SAVED before that: the shell builds every screen up front, so the page
        /// receives a VisibleChanged (and a save) with default pane bounds long
        /// before its layout is restored.
        /// </summary>
        private bool _layoutRestored;

        /// <summary>This machine's tool places: two milling units, two tools each.</summary>
        internal static readonly KeyValuePair<string, int>[] ToolPlaces =
        {
            new KeyValuePair<string, int>("U1-T1", 1), new KeyValuePair<string, int>("U1-T2", 1),
            new KeyValuePair<string, int>("U2-T1", 2), new KeyValuePair<string, int>("U2-T2", 2)
        };

        /// <summary>
        /// Which angle of each milling unit machines which face - ASSUMED (0 degree
        /// from above, 90 the outside face, 180 from below, 270 the inside face) and
        /// marked so in the library until someone checks it on the machine.
        /// </summary>
        internal static Face[] MachineFaces()
        {
            var list = new List<Face>();
            foreach (int unit in new[] { 1, 2 })
            {
                list.Add(new Face { Unit = unit, Side = "TOP", Angle = 0 });
                list.Add(new Face { Unit = unit, Side = "OUTSIDE", Angle = 90 });
                list.Add(new Face { Unit = unit, Side = "BOTTOM", Angle = 180 });
                list.Add(new Face { Unit = unit, Side = "INSIDE", Angle = 270 });
            }
            return list.ToArray();
        }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }
        private static bool CanEdit { get { return SecurityManager.HasAccess(SecurityLevel.Maintenance); } }
        private static string User { get { return SecurityManager.CurrentUser ?? ""; } }
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private static string Mm(double v) { return v.ToString("0.##", CI); }

        public ProfilesScreen()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            // Air between the panes and the navigation bar: this page is shown on
            // its own, with no tab strip around it to inset it (see SettingsScreen).
            Padding = new Padding(8, 0, 8, 8);

            foreach (var c in Columns) _table.Columns.Add(c, c == "width" || c == "height" ? typeof(double) : c == "id" ? typeof(long) : typeof(string));
            _table.Columns.Add("statekey", typeof(string));        // the state as a code (its colour)
            _view = new DataView(_table);

            // FREE-FLOATING panes on a canvas: each area can be moved, resized,
            // overlapped and minimised, and whole arrangements saved by name.
            var layoutBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.White };
            layoutBar.Controls.Add(new Label { Text = T("Prf_Layout", "Layout :"), AutoSize = true, Left = 8, Top = 11, Font = UiStyle.Small });
            _cmbLayout = new ComboBox { Left = 62, Top = 7, Width = 190, DropDownStyle = ComboBoxStyle.DropDownList, Font = UiStyle.Small };
            _cmbLayout.SelectedIndexChanged += (s, e) => { if (!_loadingLayout) ApplyLayout(Convert.ToString(_cmbLayout.SelectedItem)); };
            layoutBar.Controls.Add(_cmbLayout);
            int lx = 262;
            Action<Button> place = b => { b.Left = lx; b.Top = 3; layoutBar.Controls.Add(b); lx += 50; };
            place(UiStyle.ToolButton("\uD83D\uDCBE", T("Prf_TipLayoutSave", "Save changes into the arrangement selected above"), _tips, (s, e) => SaveCurrentLayout()));
            place(UiStyle.ToolButton("\uD83D\uDCC4", T("Prf_TipLayoutNew", "Save the current arrangement as a NEW named one"), _tips, (s, e) => SaveLayoutAs()));
            place(UiStyle.ToolButton("\uD83D\uDDD1", T("Prf_TipLayoutDelete", "Delete the selected saved arrangement"), _tips, (s, e) => DeleteLayout()));
            lx += 20;
            // The library's own tables: vendors, series, groups, accessories, macros, this machine's tools.
            var lib = UiStyle.ToolButton("\uD83D\uDCDA", T("Prf_TipLibrary",
                "The profile library: vendors, series, groups, accessories and their operations, macros, this machine's tools, faces and feeds, and what is still to be confirmed"),
                _tips, (s, e) => OpenLibraryEditor(null));
            place(lib);

            _canvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(246, 247, 249) };
            Controls.Add(_canvas);
            Controls.Add(layoutBar);

            BuildTable();
            BuildDetails();
            BuildDrawing();
            BuildAccessories();
            BuildPrograms();

            _lblStatus.Dock = DockStyle.Bottom;
            _lblStatus.Height = 24;
            _lblStatus.Font = UiStyle.Small;
            _lblStatus.ForeColor = Color.FromArgb(70, 76, 86);
            Controls.Add(_lblStatus);

            _onUserChanged = (s, e) => { if (IsHandleCreated) BeginInvoke(new Action(ApplyAccess)); };
            SecurityManager.UserChanged += _onUserChanged;

            // The library is opened when the page is first SHOWN, not when the
            // shell builds it: a new library asks whether to load the examples,
            // and nothing should ask anything while the program starts.
            VisibleChanged += (s, e) =>
            {
                if (Visible) EnsureLibrary();
                else SaveWorkingState();
            };
            _canvas.Resize += (s, e) => TryRestoreLayout();
            HandleCreated += (s, e) => BeginInvoke(new Action(() => { TryRestoreLayout(); if (Visible) EnsureLibrary(); }));
            ApplyAccess();
        }

        // =====================================================================
        // THE LIBRARY
        // =====================================================================

        /// <summary>Opens the library (once) and fills the page. Safe to call again.</summary>
        internal void EnsureLibrary()
        {
            if (_libraryTried) return;
            _libraryTried = true;
            string why;
            _libraryOk = Library.Open(() => Ask.YesNo(FindForm(), T("Prf_Title", "Profiles"),
                T("Prf_AskExamples", "The profile library is new. Load the example catalogue?\r\n\r\n" +
                  "It holds hardware of several makers with EXAMPLE positions, and the three profiles this page had. " +
                  "Nothing in it is confirmed, and nothing unconfirmed goes into a production program.")), out why);
            _libraryWhy = why ?? "";
            if (_libraryOk)
            {
                try { _instance = Library.EnsureMachine("FA2010", "FA2010", ToolPlaces, MachineFaces()); }
                catch (Exception ex) { _libraryWhy = ex.Message; }
            }
            LoadRows();
        }

        /// <summary>The library's tables; afterwards the page is read again (the library may have changed).</summary>
        internal void OpenLibraryEditor(string section)
        {
            EnsureLibrary();
            if (!_libraryOk) { Status(T("Prf_NoLibrary", "The profile library could not be opened:") + " " + _libraryWhy, true); return; }
            long open = 0;
            bool changed;
            using (var f = new LibraryEditorForm(_instance, section))
            {
                f.ShowDialog(FindForm());
                changed = f.Changed;
                open = f.ProfileToOpen;
            }
            if (changed || open > 0)
            {
                if (open > 0) _pendingSelectId = open.ToString(CI);
                ReloadAll();
            }
        }

        /// <summary>Everything read again - the table and the profile on display.</summary>
        internal void ReloadAll()
        {
            long? keep = _current != null ? _current.Id : (long?)null;
            if (!string.IsNullOrEmpty(_pendingSelectId)) { long id; if (long.TryParse(_pendingSelectId, out id)) keep = id; }
            _detailsDirty = false;
            _shownId = -1;                                  // shown again, whatever changed
            LoadRows();
            if (keep.HasValue) SelectProfile(keep.Value);
        }

        internal void Status(string text, bool problem = false)
        {
            _lblStatus.Text = text ?? "";
            _lblStatus.ForeColor = problem ? Color.FromArgb(170, 30, 30) : Color.FromArgb(70, 76, 86);
        }

        /// <summary>A button that changes the library: registered so it follows the user's level.</summary>
        private Button EditTool(string emoji, string tip, EventHandler click)
        {
            var b = UiStyle.ToolButton(emoji, tip, _tips, click);
            _editControls.Add(b);
            return b;
        }

        /// <summary>Editing follows the user's level: below Maintenance the page shows and changes nothing.</summary>
        internal void ApplyAccess()
        {
            if (Disposing || IsDisposed) return;         // the grids empty themselves while the page goes
            bool edit = CanEdit && _libraryOk;
            foreach (var c in _editControls) c.Enabled = edit;
            ApplyDetailsAccess(edit);
            ApplyAccessoriesAccess(edit);
        }

        // =====================================================================
        // PANES AND ARRANGEMENTS
        // =====================================================================

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
            var pane = new FloatingPaneHMI(key, caption);
            if (key == "table")
            {
                pane.ShowReset = true;
                pane.ResetRequested += (s, e) => ResetTableView();
            }
            _panes[key] = pane;
            _canvas.Controls.Add(pane);
            return pane.Content;
        }

        private static string LayoutFilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "ProfileLayouts.cfg"); }
        }

        private readonly Dictionary<string, Dictionary<string, string>> _layouts = new Dictionary<string, Dictionary<string, string>>();
        private string _lastLayoutName = "";

        /// <summary>Default tiling - also what the reset button restores.</summary>
        public void ResetLayout()
        {
            int w = Math.Max(600, _canvas.ClientSize.Width), h = Math.Max(400, _canvas.ClientSize.Height);
            Action<string, double, double, double, double> put = (key, x, y, pw, ph) =>
            {
                FloatingPaneHMI pane;
                if (!_panes.TryGetValue(key, out pane)) return;
                pane.Minimized = false;
                pane.Bounds = new Rectangle((int)(w * x), (int)(h * y), (int)(w * pw), (int)(h * ph));
            };
            put("table", 0, 0, 0.27, 1);
            put("drawing", 0.27, 0, 0.40, 0.58);
            put("details", 0.67, 0, 0.33, 0.58);
            put("accessories", 0.27, 0.58, 0.40, 0.42);
            put("program", 0.67, 0.58, 0.33, 0.42);

            // Default is a COMPLETE arrangement: the table's grouping, sorting,
            // filters and columns go back to their starting state as well.
            ResetTableState();
            ApplyFilterAndSort();
        }

        public const string DefaultLayoutName = "Default";

        /// <summary>The LIVE working state, saved automatically ('#': hidden from the chooser).</summary>
        private const string WorkingStateName = "#working";

        /// <summary>Arrangements are of this page's panes; one of an older page is not restored.</summary>
        private const string LayoutVersion = "4";

        private Dictionary<string, string> CaptureArrangement()
        {
            var entry = new Dictionary<string, string>();
            foreach (var kv in _panes) entry[kv.Key] = kv.Value.Serialize();
            entry["#v"] = LayoutVersion;
            entry["#groups"] = string.Join(",", _groupCols);
            entry["#sorts"] = string.Join(",", _sortKeys.Select(k => k.Key + ":" + (k.Value ? "A" : "D")));
            entry["#columns"] = string.Join(",", _grid.Columns.Cast<DataGridViewColumn>().Select(c => c.Name + ":" + (c.Visible ? 1 : 0) + ":" + c.Width + ":" + c.DisplayIndex));
            entry["#filters"] = _filtersVisible ? "1" : "0";
            return entry;
        }

        private void RestoreArrangement(Dictionary<string, string> entry)
        {
            string v;
            // An arrangement of the old page (other panes, other columns), or one
            // with every pane minimised (what an old build wrote by mistake): the
            // default tiling instead.
            int panes = 0, minimised = 0;
            foreach (var kv in entry)
            {
                if (kv.Key.StartsWith("#")) continue;
                panes++;
                var f = (kv.Value ?? "").Split(',');
                if (f.Length >= 5 && f[4].Trim() == "1") minimised++;
            }
            if (!entry.TryGetValue("#v", out v) || v != LayoutVersion || (panes > 0 && minimised == panes))
            {
                ResetLayout();
                return;
            }
            foreach (var kv in entry)
            {
                if (kv.Key.StartsWith("#")) continue;
                FloatingPaneHMI pane;
                if (_panes.TryGetValue(kv.Key, out pane)) pane.Deserialize(kv.Value);
            }
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
            if (entry.TryGetValue("#groups", out v)) SetGroups(v.Split(','));
            if (entry.TryGetValue("#sorts", out v)) SetSorts(v.Split(','));
            ApplyFilterAndSort();
        }

        private void SaveCurrentLayout()
        {
            string name = Convert.ToString(_cmbLayout.SelectedItem);
            if (string.IsNullOrEmpty(name) || name == DefaultLayoutName) { SaveLayoutAs(); return; }
            _layouts[name] = CaptureArrangement();
            _lastLayoutName = name;
            SaveLayoutPresets();
            Status(string.Format(T("Prf_LayoutUpdated", "Arrangement '{0}' updated."), name));
        }

        private void SaveLayoutAs()
        {
            string name = Ask.Text(FindForm(), T("Prf_LayoutSaveTitle", "Save arrangement"), T("Prf_LayoutName", "Name of the arrangement:"));
            if (string.IsNullOrWhiteSpace(name) || name == DefaultLayoutName || name.StartsWith("#")) return;
            _layouts[name] = CaptureArrangement();
            _lastLayoutName = name;
            SaveLayoutPresets();
            RefreshLayoutList();
            Status(string.Format(T("Prf_LayoutSaved", "Arrangement '{0}' saved."), name));
        }

        private void DeleteLayout()
        {
            string name = Convert.ToString(_cmbLayout.SelectedItem);
            if (string.IsNullOrEmpty(name) || !_layouts.ContainsKey(name)) return;
            if (!Ask.YesNo(FindForm(), T("Prf_Title", "Profiles"), string.Format(T("Prf_LayoutDelete", "Delete arrangement '{0}'?"), name))) return;
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
            Status(string.Format(T("Prf_LayoutApplied", "Arrangement '{0}' applied."), name));
        }

        private bool ApplyLastUsedLayout()
        {
            if (string.IsNullOrEmpty(_lastLayoutName) || _lastLayoutName == DefaultLayoutName || !_layouts.ContainsKey(_lastLayoutName)) return false;
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
            _cmbLayout.Items.Add(DefaultLayoutName);
            foreach (var key in _layouts.Keys)
                if (key != DefaultLayoutName && !key.StartsWith("#")) _cmbLayout.Items.Add(key);
            _cmbLayout.SelectedItem = !string.IsNullOrEmpty(_lastLayoutName) && _cmbLayout.Items.Contains(_lastLayoutName) ? _lastLayoutName : DefaultLayoutName;
            _loadingLayout = false;
        }

        /// <summary>Stores the live arrangement so it survives navigation and restart.</summary>
        private void SaveWorkingState()
        {
            try
            {
                if (!_layoutRestored || _panes.Count == 0 || _grid.Columns.Count == 0) return;
                if (_canvas == null || _canvas.ClientSize.Width < 50) return;
                _layouts[WorkingStateName] = CaptureArrangement();
                SaveLayoutPresets();
                SaveViewSettings();
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

        // =====================================================================
        // THE TABLE
        // =====================================================================

        /// <summary>The table's columns ("id" is never shown).</summary>
        internal static readonly string[] Columns =
        {
            "id", "code", "description", "vendor", "series", "variant", "group", "type", "material", "family",
            "width", "height", "drawing", "refs", "placements", "state"
        };

        private static readonly HashSet<string> DefaultVisibleColumns = new HashSet<string> { "code", "description", "group", "drawing", "placements", "state" };
        private static readonly string[] DefaultGroups = { "vendor", "series" };

        private static string Caption(string col)
        {
            switch (col)
            {
                case "code": return T("Prf_ColCode", "Code");
                case "description": return T("Prf_ColDescription", "Description");
                case "vendor": return T("Prf_ColVendor", "Vendor");
                case "series": return T("Prf_ColSeries", "Series");
                case "variant": return T("Prf_ColVariant", "Variant");
                case "group": return T("Prf_ColGroup", "Group");
                case "type": return T("Prf_ColType", "Type");
                case "material": return T("Prf_ColMaterial", "Material");
                case "family": return T("Prf_ColFamily", "Machining family");
                case "width": return T("Prf_ColWidth", "Width (mm)");
                case "height": return T("Prf_ColHeight", "Height (mm)");
                case "drawing": return T("Prf_ColDrawing", "Drawing");
                case "refs": return T("Prf_ColRefs", "Reference points");
                case "placements": return T("Prf_ColPlacements", "Confirmed");
                case "state": return T("Prf_ColState", "State");
                default: return col;
            }
        }

        // 30: the cap height plus the descenders; the filter row starts under it.
        private const int HeaderTextHeight = 30;
        private const int FilterRowHeight = 26;
        private bool _filtersVisible = true;
        private readonly Dictionary<string, string> _baseHeaders = new Dictionary<string, string>();
        private readonly Font _captionFont = UiStyle.FontVariant(UiStyle.Small, FontStyle.Bold);

        private void BuildTable()
        {
            var left = new Panel { Dock = DockStyle.Fill };
            var tools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(4, 3, 4, 3), WrapContents = false };
            tools.Controls.Add(EditTool("\u2795", T("Prf_TipAdd", "A new profile, in the group of the one selected"), (s, e) => AddProfile()));
            tools.Controls.Add(EditTool("\uD83D\uDCD1", T("Prf_TipCopy", "A new profile BASED ON the selected one: it takes its group, sizes, drawing settings and reference points, and only what differs is entered"), (s, e) => CopyProfile()));
            tools.Controls.Add(EditTool("\uD83D\uDDD1", T("Prf_TipRemove", "Take the selected profile out of use (it stays in the library, with its history)"), (s, e) => DeactivateProfile()));

            // MULTI-LEVEL grouping: tick any combination; the tick ORDER is the grouping order.
            _btnGroup = UiStyle.TextButton(T("Prf_Group", "Group") + " \u25BE", 110);
            var groupMenu = UiStyle.Menu();
            foreach (var col0 in Columns)
            {
                if (col0 == "id" || col0 == "code" || col0 == "description") continue;
                var col = col0;
                var item = new ToolStripMenuItem(Caption(col)) { CheckOnClick = true, Tag = col };
                item.CheckedChanged += (s, e) =>
                {
                    if (item.Checked) { if (!_groupCols.Contains(col)) _groupCols.Add(col); }
                    else _groupCols.Remove(col);
                    UpdateGroupButton();
                    ApplyFilterAndSort();
                };
                groupMenu.Items.Add(item);
                _groupItems.Add(item);
            }
            _btnGroup.Click += (s, e) => groupMenu.Show(_btnGroup, new Point(0, _btnGroup.Height));
            tools.Controls.Add(_btnGroup);
            tools.Controls.Add(UiStyle.ToolButton("\u21BA", T("Prf_TipResetView", "Reset grouping, sorting and filters"), _tips, (s, e) => ResetView()));
            _btnInactive = UiStyle.ToolButton("\uD83D\uDC41", T("Prf_TipInactive", "Show the profiles taken out of use as well"), _tips, (s, e) =>
            {
                _showInactive = !_showInactive;
                _btnInactive.BackColor = _showInactive ? UiStyle.AccentBack : SystemColors.Control;
                LoadRows();
            });
            tools.Controls.Add(_btnInactive);

            // The filter row sits INSIDE the column-header band, under the captions.
            foreach (var col in Columns)
            {
                if (col == "id") continue;
                var tb = new TextBox { Font = UiStyle.Small, BorderStyle = BorderStyle.FixedSingle };
                tb.TextChanged += (s, e) => ApplyFilterAndSort();
                _grid.Controls.Add(tb);
                _filters[col] = tb;
            }

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            // The program's table look (the PLC Monitor's); the heading below keeps
            // its own height for the filter boxes under the captions.
            UiStyle.GridLook(_grid);
            // A narrow EMPTY row header indents the rows while grouping is active.
            _grid.RowHeadersVisible = false;
            _grid.RowHeadersWidth = 20;
            _grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = HeaderTextHeight + FilterRowHeight;
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 4, 0, 0);
            _grid.RowHeadersDefaultCellStyle.BackColor = Color.White;
            _grid.AutoGenerateColumns = false;
            // EXPLICIT widths, not Fill: a width the operator set comes back exactly.
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.AllowUserToResizeColumns = true;
            _grid.AllowUserToOrderColumns = true;
            foreach (var name in Columns)
            {
                if (name == "id") continue;
                var col = new DataGridViewTextBoxColumn
                {
                    Name = name, HeaderText = Caption(name), Width = DefaultWidth(name),
                    Visible = DefaultVisibleColumns.Contains(name), SortMode = DataGridViewColumnSortMode.Programmatic
                };
                if (name == "width" || name == "height") col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                _grid.Columns.Add(col);
                _baseHeaders[name] = Caption(name);
            }
            _grid.ColumnHeaderMouseClick += OnHeaderClick;
            // THE PROGRAM'S COLUMN MENU on the headings (the PLC Monitor's), with the
            // page's own "Show filters"; the named arrangements keep the columns.
            var chooser = Kaban.UI.Helpers.ColumnChooser.Attach(_grid);
            chooser.DefaultKey = "Profiles";
            chooser.Changed = AlignFilters;
            chooser.Reset = ResetColumnsToDefault;
            chooser.ExtraItems = () =>
            {
                var fi = new ToolStripMenuItem(T("Prf_ShowFilters", "Show filters")) { Checked = _filtersVisible, CheckOnClick = true };
                fi.CheckedChanged += (s2, e2) => SetFiltersVisible(fi.Checked);
                return new ToolStripItem[] { fi };
            };
            _grid.SelectionChanged += (s, e) =>
            {
                if (_repopulating) return;          // rows are being rebuilt, not chosen
                if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is DataRowView)
                {
                    var rv = (DataRowView)_grid.SelectedRows[0].Tag;
                    _pendingSelectId = Convert.ToString(rv["id"], CI);
                    ShowProfile((long)rv["id"]);
                }
            };
            _grid.RowPrePaint += PaintCaptionRow;
            _grid.ColumnWidthChanged += (s, e) => AlignFilters();
            _grid.ColumnDisplayIndexChanged += (s, e) => AlignFilters();
            _grid.Scroll += (s, e) => AlignFilters();
            _grid.Resize += (s, e) => AlignFilters();

            left.Controls.Add(_grid);
            left.Controls.Add(tools);
            AddPane("table", T("Prf_PaneProfiles", "Profiles")).Controls.Add(left);
            SetGroups(DefaultGroups);
        }

        private static int DefaultWidth(string col)
        {
            switch (col)
            {
                case "code": return 110;
                case "description": return 190;
                case "group": return 170;
                case "width": case "height": return 80;
                case "state": return 120;
                case "placements": return 90;
                default: return 110;
            }
        }

        private void UpdateGroupButton()
        {
            _btnGroup.Text = T("Prf_Group", "Group") + (_groupCols.Count == 0 ? "" : " (" + _groupCols.Count + ")") + " \u25BE";
        }

        /// <summary>Grouping columns, only those this table has (an old setting may name others).</summary>
        private void SetGroups(IEnumerable<string> cols)
        {
            _groupCols.Clear();
            foreach (var it in _groupItems) it.Checked = false;       // also empties _groupCols
            _groupCols.Clear();
            foreach (var g in cols)
            {
                if (string.IsNullOrEmpty(g) || !_filters.ContainsKey(g) || _groupCols.Contains(g)) continue;
                _groupCols.Add(g);
            }
            foreach (var it in _groupItems) if (_groupCols.Contains((string)it.Tag)) it.Checked = true;
            // Checked's handler added them in the order of the menu: the saved order wins
            var order = cols.Where(g => _groupCols.Contains(g)).Distinct().ToList();
            _groupCols.Clear();
            _groupCols.AddRange(order);
            UpdateGroupButton();
        }

        private void SetSorts(IEnumerable<string> sorts)
        {
            _sortKeys.Clear();
            foreach (var srt in sorts)
            {
                var f = srt.Split(':');
                if (f.Length == 2 && _filters.ContainsKey(f[0])) _sortKeys.Add(new KeyValuePair<string, bool>(f[0], f[1] == "A"));
            }
        }

        /// <summary>Testability hook: the grouping columns.</summary>
        internal void SetGroupColumns(IEnumerable<string> cols) { SetGroups(cols.ToList()); ApplyFilterAndSort(); }

        private void OnHeaderClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;     // the right button: the columns' menu (ColumnChooser)
            if (e.ColumnIndex < 0) return;
            string col = _grid.Columns[e.ColumnIndex].Name;
            bool additive = (ModifierKeys & Keys.Shift) == Keys.Shift;
            int existing = _sortKeys.FindIndex(k => k.Key == col);
            bool wasAscending = existing >= 0 && _sortKeys[existing].Value;
            // The clicked column becomes the HIGHEST priority, the earlier ones slide down.
            bool ascending = existing == 0 ? !wasAscending : (existing > 0 ? wasAscending : true);
            if (existing >= 0) _sortKeys.RemoveAt(existing);
            _sortKeys.Insert(0, new KeyValuePair<string, bool>(col, ascending));
            if (!additive && _sortKeys.Count > 4) _sortKeys.RemoveRange(4, _sortKeys.Count - 4);
            ApplyFilterAndSort();
        }

        private void ApplyFilterAndSort()
        {
            var parts = new List<string>();
            foreach (var kv in _filters)
            {
                if (string.IsNullOrWhiteSpace(kv.Value.Text)) continue;
                string text = kv.Value.Text.Replace("'", "''").Replace("[", "[[]").Replace("*", "[*]").Replace("%", "[%]");
                bool number = _table.Columns[kv.Key].DataType == typeof(double);
                parts.Add(string.Format(number ? "CONVERT([{0}], 'System.String') LIKE '%{1}%'" : "[{0}] LIKE '%{1}%'", kv.Key, text));
            }
            try { _view.RowFilter = string.Join(" AND ", parts); }
            catch { _view.RowFilter = ""; }

            var keys = new List<string>();
            foreach (var g in _groupCols)
            {
                int gi = _sortKeys.FindIndex(k => k.Key == g);
                keys.Add("[" + g + "]" + (gi < 0 || _sortKeys[gi].Value ? " ASC" : " DESC"));
            }
            foreach (var k in _sortKeys)
                if (!_groupCols.Contains(k.Key)) keys.Add("[" + k.Key + "]" + (k.Value ? " ASC" : " DESC"));
            if (!keys.Any(k => k.StartsWith("[code]"))) keys.Add("[code] ASC");
            _view.Sort = string.Join(", ", keys);
            _grid.RowHeadersVisible = _groupCols.Count > 0;
            UpdateHeaderArrows();
            Repopulate();
        }

        /// <summary>Only the most recently sorted column carries an arrow.</summary>
        private void UpdateHeaderArrows()
        {
            string leading = _sortKeys.Count > 0 ? _sortKeys[0].Key : null;
            bool asc = _sortKeys.Count > 0 && _sortKeys[0].Value;
            foreach (DataGridViewColumn c in _grid.Columns)
            {
                string baseText;
                if (!_baseHeaders.TryGetValue(c.Name, out baseText)) continue;
                c.HeaderText = c.Name == leading ? baseText + (asc ? "  \u25B2" : "  \u25BC") : baseText;
            }
        }

        /// <summary>
        /// Fills the grid (unbound): group CAPTION rows carry the joined group
        /// values, the member rows follow. A data row's Tag is its DataRowView.
        /// </summary>
        private void Repopulate()
        {
            _repopulating = true;
            _grid.SuspendLayout();
            _grid.Rows.Clear();
            string lastKey = null;
            foreach (DataRowView rv in _view)
            {
                if (_groupCols.Count > 0)
                {
                    string key = string.Join("  |  ", _groupCols.Select(g => _baseHeaders[g] + ": " + CellText(rv, g)));
                    if (key != lastKey)
                    {
                        var capCells = new object[_grid.Columns.Count];
                        capCells[0] = key;
                        int ci = _grid.Rows.Add(capCells);
                        _grid.Rows[ci].Tag = null;
                        _grid.Rows[ci].ReadOnly = true;
                        lastKey = key;
                    }
                }
                var cells = new object[_grid.Columns.Count];
                for (int c = 0; c < _grid.Columns.Count; c++) cells[c] = CellText(rv, _grid.Columns[c].Name);
                int ri = _grid.Rows.Add(cells);
                var row = _grid.Rows[ri];
                row.Tag = rv;
                string stateKey = Convert.ToString(rv["statekey"] is DBNull ? "" : rv["statekey"]);
                if (stateKey == "inactive") row.DefaultCellStyle.ForeColor = Color.FromArgb(150, 150, 150);
                row.Cells["state"].Style.ForeColor = StateColour(stateKey);
            }
            _grid.ResumeLayout();
            _repopulating = false;
            AlignFilters();
            RestoreSelection();
        }

        private static string CellText(DataRowView rv, string column)
        {
            var v = rv[column];
            if (v == null || v is DBNull) return "";
            if (v is double) return ((double)v).ToString("0.##", CI);
            return Convert.ToString(v, CI);
        }

        private void RestoreSelection()
        {
            if (string.IsNullOrEmpty(_pendingSelectId)) return;
            foreach (DataGridViewRow gr in _grid.Rows)
            {
                var rv = gr.Tag as DataRowView;
                if (rv == null || Convert.ToString(rv["id"], CI) != _pendingSelectId) continue;
                _repopulating = true;
                gr.Selected = true;
                try { _grid.CurrentCell = gr.Cells.Cast<DataGridViewCell>().First(c => c.Visible); } catch { }
                _repopulating = false;
                try { if (gr.Index < _grid.FirstDisplayedScrollingRowIndex || gr.Index >= _grid.FirstDisplayedScrollingRowIndex + _grid.DisplayedRowCount(false)) _grid.FirstDisplayedScrollingRowIndex = gr.Index; } catch { }
                return;
            }
        }

        /// <summary>A group-caption row painted as ONE band across the grid (RowPrePaint: cells are clipped).</summary>
        private void PaintCaptionRow(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            var row = _grid.Rows[e.RowIndex];
            if (row.Tag != null) return;
            var band = new Rectangle(0, e.RowBounds.Top, _grid.Width, e.RowBounds.Height);
            using (var back = new SolidBrush(Color.FromArgb(226, 231, 238))) e.Graphics.FillRectangle(back, band);
            using (var line = new Pen(Color.FromArgb(150, 158, 170))) e.Graphics.DrawLine(line, band.Left, band.Bottom - 1, band.Right, band.Bottom - 1);
            TextRenderer.DrawText(e.Graphics, Convert.ToString(row.Cells[0].Value), _captionFont,
                new Rectangle(band.Left + 6, band.Top, band.Width - 12, band.Height), Color.FromArgb(25, 30, 38),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            e.Handled = true;
        }

        private void ResetTableState()
        {
            SetGroups(DefaultGroups);
            _sortKeys.Clear();
            foreach (var kv in _filters) kv.Value.Text = "";
            SetFiltersVisible(true);
            int order = 0;
            foreach (var name in Columns)
            {
                var col = _grid.Columns[name];
                if (col == null) continue;
                col.Visible = DefaultVisibleColumns.Contains(name);
                col.Width = DefaultWidth(name);
                col.DisplayIndex = order++;
            }
        }

        /// <summary>The table's columns alone back to their own (the column menu's "Original column settings").</summary>
        private void ResetColumnsToDefault()
        {
            int order = 0;
            foreach (var name in Columns)
            {
                var col = _grid.Columns[name];
                if (col == null) continue;
                col.Visible = DefaultVisibleColumns.Contains(name);
                col.Width = DefaultWidth(name);
                col.DisplayIndex = order++;
            }
            AlignFilters();
        }

        /// <summary>The table alone back to its defaults (the panes stay where they are).</summary>
        private void ResetTableView()
        {
            ResetTableState();
            ApplyFilterAndSort();
            SaveWorkingState();
            Status(T("Prf_TableReset", "Table view reset."));
        }

        private void ResetView()
        {
            SetGroups(new string[0]);
            _sortKeys.Clear();
            foreach (var kv in _filters) kv.Value.Text = "";
            ApplyFilterAndSort();
            Status(T("Prf_ViewReset", "Grouping, sorting and filters cleared."));
        }

        private void AlignFilters()
        {
            foreach (var kv in _filters)
            {
                var col = _grid.Columns[kv.Key];
                if (col == null || !col.Visible || !_filtersVisible) { kv.Value.Visible = false; continue; }
                var rect = _grid.GetColumnDisplayRectangle(col.Index, false);
                kv.Value.Visible = rect.Width > 0;
                kv.Value.SetBounds(rect.Left + 1, HeaderTextHeight + 1, Math.Max(20, rect.Width - 3), FilterRowHeight - 4);
                kv.Value.BringToFront();
            }
        }

        private void SetFiltersVisible(bool show)
        {
            _filtersVisible = show;
            _grid.ColumnHeadersHeight = HeaderTextHeight + (show ? FilterRowHeight : 0);
            AlignFilters();
        }

        // ---- the rows ----------------------------------------------------------------------

        private static string StateText(string key)
        {
            switch (key)
            {
                case "inactive": return T("Prf_StInactive", "out of use");
                case "nodrawing": return T("Prf_StNoDrawing", "no drawing");
                case "noaccessory": return T("Prf_StNoAccessory", "no accessories");
                case "ready": return T("Prf_StReady", "ready");
                case "confirm": return T("Prf_StConfirm", "to confirm");
                case "refs": return T("Prf_StRefs", "reference points to check");
                default: return key;
            }
        }

        private static Color StateColour(string key)
        {
            switch (key)
            {
                case "ready": return Color.FromArgb(20, 120, 50);
                case "confirm": case "refs": return Color.FromArgb(190, 110, 0);
                case "nodrawing": return Color.FromArgb(170, 40, 40);
                default: return Color.FromArgb(110, 116, 126);
            }
        }

        /// <summary>Reads every profile of the library into the table.</summary>
        private void LoadRows()
        {
            _table.Rows.Clear();
            if (!_libraryOk)
            {
                Repopulate();
                if (_libraryTried) Status(T("Prf_NoLibrary", "The profile library could not be opened:") + " " + _libraryWhy, true);
                ShowProfile(0);
                return;
            }
            try
            {
                var refs = Library.Rows("SELECT profile_id, COUNT(*) AS n, SUM(CASE WHEN confirmed_by IS NULL THEN 1 ELSE 0 END) AS open FROM profile_reference GROUP BY profile_id")
                                  .ToDictionary(r => Library.Id(r, "profile_id").Value, r => r);
                var expected = Library.Rows(@"SELECT p.id, COUNT(op.id) AS n FROM profile p
                        JOIN v_group_accessory ga ON ga.profile_group_id = p.group_id
                        JOIN accessory_operation op ON op.accessory_id = ga.accessory_id GROUP BY p.id")
                                      .ToDictionary(r => Library.Id(r, "id").Value, r => (int)(Library.Id(r, "n") ?? 0));
                foreach (var p in Library.Profiles(_showInactive))
                {
                    Dictionary<string, object> rr;
                    refs.TryGetValue(p.Id, out rr);
                    int nRefs = rr != null ? (int)(Library.Id(rr, "n") ?? 0) : 0, openRefs = rr != null ? (int)(Library.Id(rr, "open") ?? 0) : 0;
                    int ops; expected.TryGetValue(p.Id, out ops);
                    bool hasFile = Library.DxfFile(p) != null;
                    string drawing = string.IsNullOrWhiteSpace(p.DxfPath) ? T("Prf_DrawNone", "none")
                                   : hasFile ? Path.GetFileName(p.DxfPath) : T("Prf_DrawMissing", "file missing");
                    int toConfirm = Math.Max(0, ops - p.Rejected - p.Confirmed);
                    string key = !p.Active ? "inactive" : !hasFile ? "nodrawing" : ops == 0 ? "noaccessory"
                               : toConfirm > 0 ? "confirm" : openRefs > 0 ? "refs" : "ready";
                    string state = StateText(key) + (key == "confirm" ? " (" + toConfirm + ")" : "");
                    _table.Rows.Add(p.Id, p.Code, p.Description, p.Vendor, p.Series, p.Variant, p.Group, p.Type, p.Material, p.Family,
                                    p.EffectiveWidth > 0 ? (object)p.EffectiveWidth : DBNull.Value, p.EffectiveHeight > 0 ? (object)p.EffectiveHeight : DBNull.Value,
                                    drawing, nRefs == 0 ? "" : nRefs + (openRefs > 0 ? " (" + openRefs + " " + T("Prf_ToCheck", "to check") + ")" : ""),
                                    ops == 0 ? "" : p.Confirmed + " / " + Math.Max(0, ops - p.Rejected), state, key);
                }
                if (string.IsNullOrEmpty(_pendingSelectId)) LoadViewSettings();
                ApplyFilterAndSort();
                if (_grid.SelectedRows.Count == 0 || !(_grid.SelectedRows[0].Tag is DataRowView))
                {
                    var first = _grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Tag is DataRowView);
                    if (first != null) { _pendingSelectId = Convert.ToString(((DataRowView)first.Tag)["id"], CI); RestoreSelection(); }
                }
                if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is DataRowView)
                    ShowProfile((long)((DataRowView)_grid.SelectedRows[0].Tag)["id"]);
                else ShowProfile(0);
                if (_table.Rows.Count == 0)
                    Status(T("Prf_Empty", "The library has no profiles yet: add a series and a group in the library (\uD83D\uDCDA), then a profile (\u2795)."));
            }
            catch (Exception ex)
            {
                Status(T("Prf_ReadFailed", "The library could not be read:") + " " + ex.Message, true);
            }
            ApplyAccess();
        }

        /// <summary>Selects a profile in the table (and shows it).</summary>
        internal void SelectProfile(long id)
        {
            _pendingSelectId = id.ToString(CI);
            if (!_table.Rows.Cast<DataRow>().Any(r => (long)r["id"] == id) && !_showInactive)
            {
                // taken out of use: shown with the others that are
                _showInactive = true;
                _btnInactive.BackColor = UiStyle.AccentBack;
                LoadRows();
            }
            // hidden by a filter: the filters go, the profile shows
            if (!_view.Cast<DataRowView>().Any(rv => (long)rv["id"] == id))
                foreach (var kv in _filters) kv.Value.Text = "";
            ApplyFilterAndSort();
            ShowProfile(id);
        }

        /// <summary>The row of the table for the profile on display, refreshed after a change (not the whole table).</summary>
        private void RefreshRow()
        {
            if (_current == null) return;
            long id = _current.Id;
            LoadRows();
            _pendingSelectId = id.ToString(CI);
            RestoreSelection();
        }

        // =====================================================================
        // THE PROFILE ON DISPLAY
        // =====================================================================

        private long _shownId = -1;

        /// <summary>Shows a profile in every pane (0: none).</summary>
        internal void ShowProfile(long id)
        {
            if (id == _shownId && _current != null) return;
            if (_detailsDirty && _current != null && id != _current.Id)
            {
                if (Ask.YesNo(FindForm(), T("Prf_Title", "Profiles"), string.Format(T("Prf_AskSave", "Save the changes to {0}?"), _current.Code)))
                    SaveDetails();
                _detailsDirty = false;
            }
            _shownId = id;
            LoadProfile(id, true);
        }

        /// <summary>Reads the profile again (after a change) and fills every pane.</summary>
        internal void LoadProfile(long id, bool resolve)
        {
            _current = id > 0 && _libraryOk ? Library.Profile(id) : null;
            _section = null;
            _references = new List<Library.ReferenceInfo>();
            _placements = new List<Library.PlacementInfo>();
            _resolveProblems = new List<string>();
            _pendingDy = _pendingDz = 0;
            if (_current != null)
            {
                try
                {
                    if (resolve) _resolveProblems = Library.Resolve(_current.Id).Problems.ToList();
                    _current = Library.Profile(id);
                }
                catch (Exception ex) { _resolveProblems.Add(ex.Message); }
                try { _section = Library.SectionOf(_current); }
                catch (Exception ex) { Status(T("Prf_DxfFailed", "The drawing could not be read:") + " " + ex.Message, true); }
                _references = Library.References(_current.Id);
                _placements = Library.Placements(_current.Id);
            }
            FillDetails();
            FillReferences();
            ShowDrawing();
            FillAccessories();
            FillPrograms();
            ApplyAccess();
            if (_current != null) Status(ProfileSummary());
        }

        /// <summary>"0001000007 - Elegant frame: 82 x 115 mm, 4 reference points, 3 of 5 placements confirmed."</summary>
        private string ProfileSummary()
        {
            var p = _current;
            int open = _placements.Count(x => x.Status != "REJECTED");
            int conf = _placements.Count(x => x.Status == "CONFIRMED" || x.Status == "OVERRIDDEN");
            return p.Code + (p.Description.Length > 0 ? " - " + p.Description : "") + ":  " +
                   (p.EffectiveWidth > 0 ? Mm(p.EffectiveWidth) + " x " + Mm(p.EffectiveHeight) + " mm,  " : "") +
                   string.Format(T("Prf_Summary", "{0} reference point(s), {1} of {2} placement(s) confirmed"), _references.Count, conf, open) + ".";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SaveWorkingState();
                if (_onUserChanged != null) SecurityManager.UserChanged -= _onUserChanged;
            }
            base.Dispose(disposing);
        }

        // ---- view settings -----------------------------------------------------------------------

        private static string ViewSettingsPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "ProfilesView.cfg"); }
        }

        /// <summary>Keeps the filters and the open profile between sessions (grouping and sorting are in the arrangement).</summary>
        private void SaveViewSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ViewSettingsPath));
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("v=" + LayoutVersion);
                foreach (var kv in _filters)
                    if (!string.IsNullOrWhiteSpace(kv.Value.Text)) sb.AppendLine("filter." + kv.Key + "=" + kv.Value.Text);
                sb.AppendLine("selected=" + (_pendingSelectId ?? ""));
                sb.AppendLine("bar=" + ((double)_numBar.Value).ToString(CI));
                File.WriteAllText(ViewSettingsPath, sb.ToString());
            }
            catch { }
        }

        private void LoadViewSettings()
        {
            try
            {
                if (!File.Exists(ViewSettingsPath)) return;
                var lines = File.ReadAllLines(ViewSettingsPath);
                if (!lines.Contains("v=" + LayoutVersion)) return;       // the old page's: other columns, other ids
                foreach (var line in lines)
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq), val = line.Substring(eq + 1);
                    TextBox tb;
                    decimal d;
                    if (key.StartsWith("filter.") && _filters.TryGetValue(key.Substring(7), out tb)) tb.Text = val;
                    else if (key == "selected" && val.Length > 0) _pendingSelectId = val;
                    else if (key == "bar" && decimal.TryParse(val, NumberStyles.Float, CI, out d)) _numBar.Value = Math.Max(_numBar.Minimum, Math.Min(_numBar.Maximum, d));
                }
            }
            catch { }
        }
    }
}
