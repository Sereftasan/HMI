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
    /// CPU's device memory in GX Works. Admin only: F12, or the user menu.
    ///
    /// ONE TAB PER CONTROLLER, named the way the engineer names it ("Processing
    /// Center (1)", "3D Hinge Drilling"). INSIDE EACH, THE ENGINEER'S OWN TABS -
    /// "Main", "Job List", "Manual", "Parameters"... - each its own watch list,
    /// added, renamed and removed freely: one flat list per controller grew to
    /// hundreds of lines nobody could find anything in.
    ///
    /// A LIST IS BUILT, NOT TYPED: the devices of the screen on display, of every
    /// page of the program (into one tab, or one tab per page), or of the PLC
    /// label list, each with its data type and name, in one action. Rows can be
    /// copied and pasted - above the selected row or at the end - added as an
    /// array of consecutive devices, and a line without address or value is a
    /// COMMENT: a heading that splits a long list into parts.
    ///
    /// DOUBLE-CLICK: on a VALUE it is edited right there in the table - the
    /// value is what is changed nine times out of ten. Anywhere else it opens
    /// the row's editor with that field selected. On the empty last line it
    /// adds a device.
    ///
    /// A VALUE TYPED HERE IS WRITTEN STRAIGHT THROUGH, on a simulated station and
    /// on a real one alike - that is the point of the page. Writes to a real
    /// station are logged (the page is the admin's: no warning banner).
    ///
    /// The lists are kept per controller in ProgramData\KabanHMI\Monitor, one
    /// file each, "#sheet:" lines separating the tabs. A file from before the
    /// tabs is read as a single tab called "Main".
    /// </summary>
    public partial class SimulationMonitorForm : Form, ISidePageWidth
    {
        private readonly TabControl _tabs = new TabControl();
        private readonly Timer _refresh = new Timer();
        private readonly List<MonitorTab> _monitors = new List<MonitorTab>();
        private readonly Label _status = new Label();
        /// <summary>
        /// THIS WINDOW'S OWN tooltip. The program-wide one never shows over an
        /// inactive window, and it counts the main window as its own - so over
        /// this separate window it never showed at all, and the toolbar was a row
        /// of pictures nobody could read.
        /// </summary>
        private readonly ToolTip _tips = UiStyle.CreateTips();
        private ToolStripItem _restoreItem;
        private ToolStrip _bar;
        private Button _btnUndo, _btnRedo;

        /// <summary>Copied rows - shared by every controller, so a list can be copied from one PLC to another.</summary>
        private static readonly List<WatchRow> _clip = new List<WatchRow>();

        private static string E(int codePoint) { return char.ConvertFromUtf32(codePoint); }

        // ==================================================================
        // MODEL
        // ==================================================================

        /// <summary>
        /// One line of a watch list: a device, a COMMENT (text, no address), or
        /// the empty last line that exists to be typed into.
        ///
        /// Label and DataType default to the machine's PlcLabels.csv but may be
        /// OVERRIDDEN here - on the bench the engineer routinely watches a device
        /// the label list has not caught up with yet.
        /// </summary>
        internal class WatchRow
        {
            public string Address = "";
            /// <summary>The station of the tab this row is on - labels differ per machine.</summary>
            public int Station;
            public string LabelOverride = "";
            public string DataTypeOverride = "";
            /// <summary>The text of a comment line.</summary>
            public string Comment = "";

            /// <summary>
            /// A MACHINE CONDITION's row (MachineConditionMap: "S:ManualMode",
            /// "B:FA2010.RobotIsOutside"): its device, and in the Condition column
            /// whether the condition is met now.
            /// </summary>
            public string ConditionKey = "";

            /// <summary>Decimal (""), Hexadecimal or Binary - how a whole number is shown and typed.</summary>
            public string DisplayFormat = "";
            /// <summary>A String's length in characters (when its data type does not say, as "String(32)" does).</summary>
            public int StringLength = 16;
            /// <summary>An ARRAY: how many elements (1 = a single device).</summary>
            public int ArrayCount = 1;
            /// <summary>An array shown with its elements under it.</summary>
            public bool Expanded;

            /// <summary>On an element row: its array; on a member row: its structure. Made from them, never stored.</summary>
            public WatchRow Head;
            public int ElementIndex;
            /// <summary>On an array: its element rows, kept so the same rows stay (and stay selected) between refreshes.</summary>
            public List<WatchRow> Elements;

            // ---- A STRUCTURE (19 October: Mitsubishi's structured data type - a recipe step) ----
            /// <summary>On a member row of a structure: its member.</summary>
            public StructuredTypes.Member Member;
            /// <summary>On a structure: its member rows, kept between refreshes like an array's elements.</summary>
            public List<WatchRow> Members;

            /// <summary>The item this row belongs to: itself, its array, or its structure's array.</summary>
            public WatchRow Root { get { return Head == null ? this : Head.Root; } }
            /// <summary>How deep it is under its item: 0, an element 1, a member of an element 2.</summary>
            public int Depth { get { return Head == null ? 0 : Head.Depth + 1; } }

            /// <summary>Elements: the row's own count, else the label list's "(0..19)" for a structure.</summary>
            public int EffectiveCount
            {
                get
                {
                    if (Head != null) return 1;
                    if (ArrayCount > 1) return ArrayCount;
                    var t = DataTypeOverride.Length > 0 ? DataTypeOverride : LabelDataType;
                    int n = StructuredTypes.CountIn(t);
                    return n > 1 && StructuredTypes.Find(t, Station) != null ? n : 1;
                }
            }

            public bool IsArray { get { return Head == null && EffectiveCount > 1 && IsDevice; } }
            public bool IsElement { get { return Head != null; } }
            public bool IsMember { get { return Member != null; } }

            /// <summary>The structure this row is one of (a structure's own row, or an element of an array of them); null otherwise.</summary>
            public StructuredTypes.Type Structure
            {
                get { return Member == null && IsDevice && !IsArray ? StructuredTypes.Find(EffectiveDataType, Station) : null; }
            }

            /// <summary>A row that holds others and has no value of its own: an array, or a structure.</summary>
            public bool IsGroup { get { return IsArray || Structure != null; } }

            public string EffectiveFormat
            {
                get
                {
                    var f = Root.DisplayFormat;
                    return string.IsNullOrEmpty(f) ? FormatDecimal : f;
                }
            }
            public int EffectiveStringLength { get { return Member != null ? 16 : Head != null ? Head.EffectiveStringLength : StringLength; } }

            /// <summary>A Bit member's bit of its word (-1: none) - the address shows it: "R16.3".</summary>
            public int BitIndex { get { return Member != null && Member.IsBit ? Member.Bit : -1; } }
            public string ShownAddress { get { return BitIndex >= 0 ? Address + "." + BitIndex.ToString("X", CultureInfo.InvariantCulture) : Address; } }

            private string LabelDataType
            {
                get { var e = PlcLabelTable.ByAddress(Station, Address); return e != null ? e.DataType ?? "" : ""; }
            }

            public bool IsComment { get { return string.IsNullOrWhiteSpace(Address) && !string.IsNullOrWhiteSpace(Comment); } }

            /// <summary>True for the trailing empty line.</summary>
            public bool IsBlank { get { return string.IsNullOrWhiteSpace(Address) && string.IsNullOrWhiteSpace(Comment); } }

            public bool IsDevice { get { return !string.IsNullOrWhiteSpace(Address); } }

            public string EffectiveLabel
            {
                get
                {
                    if (Member != null)
                    {
                        var s = Head.EffectiveLabel;
                        return (s.Length > 0 ? s : Head.Address) + "." + Member.Name;
                    }
                    if (Head != null)
                    {
                        var h = Head.EffectiveLabel;
                        return (h.Length > 0 ? h : Head.Address) + "[" + ElementIndex + "]";
                    }
                    if (!string.IsNullOrWhiteSpace(LabelOverride)) return LabelOverride;
                    var e = PlcLabelTable.ByAddress(Station, Address);
                    return e != null && e.Label.Length > 0 ? e.Label : "";
                }
            }

            public string EffectiveDataType
            {
                get
                {
                    if (Member != null) return Member.DataType;
                    // an element: its array's type without the label list's "(0..19)"
                    if (Head != null) return StructuredTypes.BaseName(Head.EffectiveDataType);
                    if (!string.IsNullOrWhiteSpace(DataTypeOverride)) return DataTypeOverride;
                    var e = PlcLabelTable.ByAddress(Station, Address);
                    return e != null ? e.DataType : "";
                }
            }

            public bool IsUnknownToLabelTable
            {
                get { return Head != null ? Root.IsUnknownToLabelTable : string.IsNullOrWhiteSpace(LabelOverride) && PlcLabelTable.ByAddress(Station, Address) == null; }
            }

            public WatchRow Clone()
            {
                return new WatchRow { Address = Address, Station = Station, LabelOverride = LabelOverride,
                                      DataTypeOverride = DataTypeOverride, Comment = Comment,
                                      DisplayFormat = DisplayFormat, StringLength = StringLength,
                                      ArrayCount = Head == null ? ArrayCount : 1, Expanded = Expanded,
                                      ConditionKey = Head == null ? ConditionKey : "" };
            }
        }

        /// <summary>One of the engineer's tabs under a controller.</summary>
        internal class Sheet
        {
            public string Name = "";
            public readonly List<WatchRow> Rows = new List<WatchRow>();
            public DataGridView Grid;
            public TabPage Page;
            public bool Editing;
            public int DragFrom = -1;
            public Point DragStart;
            /// <summary>The column last sorted on, and which way - a second click reverses it.</summary>
            public string SortColumn = "";
            public bool SortDescending;
        }

        internal class MonitorTab
        {
            public PlcConnectionSettings.Endpoint Endpoint;
            public bool IsSimulated;
            public TabPage Page;
            public TabControl SheetTabs;
            public readonly List<Sheet> Sheets = new List<Sheet>();

            /// <summary>The "+" after the last tab: a new tab at the end.</summary>
            public Button AddTabButton;

            /// <summary>
            /// Simulator addresses taken OFF the lists. The simulator shows every
            /// device it holds, appending new ones as screens touch them; one the
            /// engineer removed would otherwise come straight back.
            /// </summary>
            public readonly HashSet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Latest values, by address. Filled by the refresh.</summary>
            public readonly Dictionary<string, int> Values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            /// <summary>A background read is outstanding (real stations only).</summary>
            public volatile bool ReadInFlight;

            public int Station { get { return Endpoint != null ? Endpoint.StationId : 0; } }

            /// <summary>
            /// The tab ON DISPLAY - found from the selected page itself, never by
            /// its position. The list of tabs and the tab strip were kept in step by
            /// index, and TabPages.Insert on a strip that has no window yet adds at
            /// the END whatever index it is given: the two orders parted, the
            /// selected tab's index pointed at its neighbour, and a device added to
            /// an empty tab went into the tab before it - as did a delete.
            /// </summary>
            public Sheet Current
            {
                get
                {
                    if (SheetTabs == null) return Sheets.FirstOrDefault();
                    var page = SheetTabs.SelectedTab;
                    return page != null ? page.Tag as Sheet : null;
                }
            }

            /// <summary>Tabs counted in the file (-1: no file). Zero means the engineer deleted them all.</summary>
            public int SavedTabCount = -1;

            /// <summary>Shown instead of the tab strip when every tab has been deleted.</summary>
            public Label EmptyHint;

            /// <summary>The list of tabs re-read from the strip, in the order shown.</summary>
            public void SyncOrder()
            {
                if (SheetTabs == null) return;
                Sheets.Clear();
                foreach (TabPage p in SheetTabs.TabPages)
                {
                    var sh = p.Tag as Sheet;
                    if (sh != null) Sheets.Add(sh);
                }
            }

            /// <summary>Read access. Deliberately not permission-gated - watching is not commanding.</summary>
            public IPlcDriver ReadDriver { get { return PlcManager.Instance.GetDriverForMonitoring(Station); } }

            /// <summary>Write access, which IS permission-gated.</summary>
            public IPlcDriver WriteDriver { get { return PlcManager.Instance.GetDriver(Station); } }

            public bool Listed(string address)
            {
                return Sheets.Any(s => s.Rows.Any(r => string.Equals(r.Address, address, StringComparison.OrdinalIgnoreCase)));
            }
        }

        private MonitorTab CurrentMonitor
        {
            get
            {
                int i = _tabs.SelectedIndex;
                return i >= 0 && i < _monitors.Count ? _monitors[i] : null;
            }
        }

        // ==================================================================
        // CONSTRUCTION
        // ==================================================================

        /// <summary>
        /// True when the monitor is a SIDE PAGE of the main window (or of a side
        /// page's own window) rather than a window of its own.
        /// </summary>
        public bool Embedded { get; private set; }

        public SimulationMonitorForm() : this(false) { }

        /// <param name="embedded">A side page (see MainForm.SidePanel): no frame, no window place of its own.</param>
        public SimulationMonitorForm(bool embedded)
        {
            Embedded = embedded;
            Text = LanguageManager.GetString("Ttl_SimMonitor", "PLC Monitor - live device memory");
            Size = new Size(820, 700);
            // As narrow as the user likes: the buttons that do not fit go under
            // the toolbar's chevron (>>), as on the Profiles page.
            MinimumSize = embedded ? Size.Empty : new Size(300, 260);
            StartPosition = FormStartPosition.Manual;
            if (embedded)
            {
                TopLevel = false;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
            }
            else UiScale.ApplyOnLoad(this);
            MinimizeBox = true;
            ShowInTaskbar = true;
            BackColor = Color.White;
            Font = UiStyle.Body;
            KeyPreview = true;

            var bar = _bar = BuildToolbar();

            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.Padding = new Padding(8, 4, 8, 2);
            _status.ForeColor = Color.FromArgb(90, 96, 106);
            _status.Font = UiStyle.Small;

            _tabs.Dock = DockStyle.Fill;
            _tabs.Padding = new Point(14, 5);
            _tabs.SelectedIndexChanged += (s, e) => { UpdateToolbar(); RefreshValues(); };

            Controls.Add(_tabs);
            Controls.Add(bar);
            Controls.Add(_status);

            BuildTabs();
            UpdateToolbar();

            // Where it was closed, as big as it was.
            if (!embedded) PlacementRestored = WindowLayout.Apply(this, LayoutName, WindowLayout.Read(WindowsPath));

            _refresh.Interval = 250;
            _refresh.Tick += (s, e) => RefreshValues();
            _refresh.Start();
        }

        private const string LayoutName = "Monitor";

        /// <summary>The window opens where it was closed (false: first time, or that place is off every screen).</summary>
        public bool PlacementRestored { get; private set; }

        /// <summary>The tool windows' places and sizes: this one and Find (see WindowLayout).</summary>
        internal static string WindowsPath { get { return Path.Combine(WatchFolder, "Windows.cfg"); } }

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

        /// <summary>The UI layer must not reference the Simulation driver assembly, so the simulator is recognised by type name.</summary>
        private static bool IsSimulatedStation(int station)
        {
            var drv = PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(station));
            return drv != null && drv.GetType().Name == "SimulationDriver";
        }

        // ---- the toolbar -----------------------------------------------------
        //
        // The program's toolbar: emoji buttons, the words in the tooltip - the same
        // as Alarm Settings and the PLC Connection page. The old monitor had two
        // text buttons at the bottom and everything else hidden in a right-click
        // menu, which is why nobody knew half of it existed.

        private ToolStrip BuildToolbar()
        {
            var bar = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                CanOverflow = true,
                LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
                AutoSize = false,
                Height = 40,
                BackColor = Color.White,
                Padding = new Padding(4, 3, 4, 3),
                Renderer = new ToolStripProfessionalRenderer()
            };
            Action<Button> add = b => bar.Items.Add(new ToolStripControlHost(b) { AutoSize = false, Size = b.Size, Margin = new Padding(2, 0, 2, 0) });
            Action gap = () => bar.Items.Add(new ToolStripSeparator { Margin = new Padding(6, 0, 6, 0) });

            Button btnAdd = null;
            btnAdd = UiStyle.ToolButton(E(0x2795), T("Tip_MonAdd", "Add a device or a comment line"), _tips,
                (s, e) => ShowMenu(btnAdd, AddMenu()));
            add(btnAdd);
            // The monitor's keys are its window's own (ProcessCmdKey: in the table,
            // on the tab strip): its buttons show them, first in their tooltips.
            Action<Control, Keys, string> keyed = (b, k, tip) => Kaban.UI.Helpers.Shortcuts.Set(b, k, _tips, tip, dispatch: false);
            keyed(btnAdd, Keys.Insert, T("Tip_MonAdd", "Add a device or a comment line"));
            // Copy, paste, the moves and undo/redo are DRAWN (ToolGlyphs): the
            // pictures every office program uses, short heavy arrows - their
            // characters and emoji read as thin sticks or as something else.
            var btnCopy = UiStyle.IconButton(ToolGlyphs.Copy, "Copy", T("Tip_MonCopy", "Copy the selected rows"), _tips, (s, e) => CopySelected());
            keyed(btnCopy, Keys.Control | Keys.C, T("Tip_MonCopy", "Copy the selected rows"));
            add(btnCopy);
            Button btnPaste = null;
            btnPaste = UiStyle.IconButton(ToolGlyphs.Paste, "Paste", T("Tip_MonPaste", "Paste the copied rows (with the keys: above the selected row)"), _tips,
                (s, e) => ShowMenu(btnPaste, PasteMenu()));
            keyed(btnPaste, Keys.Control | Keys.V, T("Tip_MonPaste", "Paste the copied rows (with the keys: above the selected row)"));
            add(btnPaste);
            Button btnDelete = null;
            btnDelete = UiStyle.ToolButton(E(0x1F5D1), T("Tip_MonDelete",
                "Delete: the selected rows (with the key), this tab, every tab of this controller or of every controller"), _tips,
                (s, e) => ShowMenu(btnDelete, DeleteMenu()));
            keyed(btnDelete, Keys.Delete, T("Tip_MonDelete", "Delete: the selected rows (with the key), this tab, every tab of this controller or of every controller"));
            add(btnDelete);
            var btnUp = UiStyle.IconButton(ToolGlyphs.MoveUp, "Move up", T("Tip_MonUp", "Move the selected rows up"), _tips, (s, e) => MoveSelected(-1));
            keyed(btnUp, Keys.Control | Keys.Up, T("Tip_MonUp", "Move the selected rows up"));
            add(btnUp);
            var btnDown = UiStyle.IconButton(ToolGlyphs.MoveDown, "Move down", T("Tip_MonDown", "Move the selected rows down"), _tips, (s, e) => MoveSelected(+1));
            keyed(btnDown, Keys.Control | Keys.Down, T("Tip_MonDown", "Move the selected rows down"));
            add(btnDown);
            _btnUndo = UiStyle.IconButton(ToolGlyphs.Undo, "Undo", T("Tip_MonUndo", "Undo the last change"), _tips, (s, e) => Undo());
            keyed(_btnUndo, Keys.Control | Keys.Z, T("Tip_MonUndo", "Undo the last change"));
            add(_btnUndo);
            _btnRedo = UiStyle.IconButton(ToolGlyphs.Redo, "Redo", T("Tip_MonRedo", "Redo the change undone last"), _tips, (s, e) => Redo());
            keyed(_btnRedo, Keys.Control | Keys.Y, T("Tip_MonRedo", "Redo the change undone last"));
            add(_btnRedo);
            gap();
            Button btnFrom = null;
            btnFrom = UiStyle.ToolButton(E(0x1F5C3), T("Tip_MonCollect", "Add the devices of this screen, of the whole program or of the PLC label list"), _tips,
                (s, e) => ShowMenu(btnFrom, CollectMenu()));
            add(btnFrom);
            var btnFind = UiStyle.ToolButton(E(0x1F50E), T("Tip_MonFind", "Find where a device is used in the program, and open that page"), _tips,
                (s, e) => OpenFind(null));
            keyed(btnFind, Keys.Control | Keys.F, T("Tip_MonFind", "Find where a device is used in the program, and open that page"));
            add(btnFind);
            Button btnTabs = null;
            btnTabs = UiStyle.ToolButton(E(0x1F4D1), T("Tip_MonTabs", "Tabs: new, rename, delete"), _tips,
                (s, e) => ShowMenu(btnTabs, SheetMenu()));
            add(btnTabs);
            gap();
            // Out and in, as the pictures say: the floppy and the folder mean Save
            // and Reload everywhere else in the program, and were not found here.
            add(UiStyle.ToolButton(E(0x1F4E4), T("Tip_MonExport", "Export this tab to a CSV file (Label / DataType / Address / Value)"), _tips, (s, e) => ExportSheet()));
            add(UiStyle.ToolButton(E(0x1F4E5), T("Tip_MonImport", "Import devices from a CSV file into this tab (a PlcLabels.csv works as it is)"), _tips, (s, e) => ImportSheet()));

            var restore = UiStyle.ToolButton(E(0x1F441), T("Tip_MonRestore",
                "Show the removed devices again. The simulator lists every device it holds and adds new ones by itself - " +
                "a device removed from a list is kept off it until this is pressed."), _tips, (s, e) => RestoreHidden());
            var host = new ToolStripControlHost(restore) { AutoSize = false, Size = restore.Size, Alignment = ToolStripItemAlignment.Right };
            bar.Items.Add(host);
            _restoreItem = host;
            return bar;
        }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>
        /// The width that shows all of the monitor (the side panel's border
        /// double-clicked): every button of the toolbar, every column of the
        /// tab on display with room for its scroll bar - and, while Find is open
        /// under the lists, every column of Find's list too.
        /// </summary>
        public int PreferredWidth(int available)
        {
            int toolbar = _bar == null ? 0 : _bar.Items.Cast<ToolStripItem>().Where(i => i.Available)
                                              .Sum(i => i.Width + i.Margin.Horizontal) + _bar.Padding.Horizontal + 12;
            int table = 0;
            var mon = CurrentMonitor;
            var sh = mon != null ? mon.Current : null;
            if (sh != null && sh.Grid != null)
            {
                var grid = sh.Grid;
                int columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width) +
                              (grid.RowHeadersVisible ? grid.RowHeadersWidth : 0) + SystemInformation.VerticalScrollBarWidth + 4;
                // what is round the table: the tab strips' frames
                int chrome = Math.Max(0, ClientSize.Width - grid.ClientSize.Width);
                table = columns + chrome;
            }
            int find = 0;
            if (FindShowing && Embedded && _findHost != null)
                find = _find.PreferredWidth() + Math.Max(0, ClientSize.Width - _findHost.ClientSize.Width);
            return Math.Max(toolbar, Math.Max(table, find));
        }

        private void ShowMenu(Control owner, ContextMenuStrip menu)
        {
            UiStyle.ShowPopup(menu, owner, new Point(0, owner.Height));
        }

        private ContextMenuStrip NewMenu()
        {
            var m = new ContextMenuStrip { Font = UiStyle.MenuFont, ShowImageMargin = false };
            m.Renderer = new HmiMenuRenderer();
            return m;
        }

        private static ToolStripMenuItem Item(string text, EventHandler click, bool enabled = true, Keys keys = Keys.None, string keyText = null)
        {
            var i = new ToolStripMenuItem(text, null, click) { Enabled = enabled };
            if (keys != Keys.None) i.ShortcutKeys = keys;
            // Shown only - the window handles the key itself (see ProcessCmdKey).
            if (keyText != null) { i.ShortcutKeyDisplayString = keyText; i.ShowShortcutKeys = true; }
            return i;
        }

        private bool HasSelection
        {
            get
            {
                var sh = CurrentMonitor != null ? CurrentMonitor.Current : null;
                return sh != null && SelectedIndices(sh).Count > 0;
            }
        }

        private ContextMenuStrip AddMenu()
        {
            var m = NewMenu();
            m.Items.Add(Item(T("Mnu_MonAddEnd", "New device at the end..."), (s, e) => AddRows(atEnd: true, comment: false)));
            m.Items.Add(Item(T("Mnu_MonAddAbove", "New device above the selected row..."), (s, e) => AddRows(atEnd: false, comment: false), HasSelection, Keys.Insert));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Item(T("Mnu_MonCommentEnd", "Comment line at the end..."), (s, e) => AddRows(atEnd: true, comment: true)));
            m.Items.Add(Item(T("Mnu_MonCommentAbove", "Comment line above the selected row..."), (s, e) => AddRows(atEnd: false, comment: true), HasSelection));
            m.Items.Add(new ToolStripSeparator());
            // 19 October: Mitsubishi's structured data types - a recipe step, chosen like a data type
            m.Items.Add(Item(T("Mnu_MonStructures", "Structured data types..."), (s, e) => EditStructures()));
            return m;
        }

        /// <summary>The structured data types' editor (the current tab's machine's); every list shows its rows of them anew after it.</summary>
        internal void EditStructures(string select = null)
        {
            var current = CurrentMonitor;
            using (var dlg = new StructuredTypesForm(select, current != null ? current.Station : 0))
                if (dlg.ShowDialog(DialogOwner) != DialogResult.OK) return;
            foreach (var mon in _monitors)
                foreach (var sh in mon.Sheets)
                    if (sh.Grid != null) Render(mon, sh);
        }

        private ContextMenuStrip PasteMenu()
        {
            var m = NewMenu();
            bool any = _clip.Count > 0 || ClipboardHasText();
            m.Items.Add(Item(T("Mnu_MonPasteAbove", "Paste above the selected row"), (s, e) => Paste(atEnd: false), any && HasSelection, Keys.Control | Keys.V));
            m.Items.Add(Item(T("Mnu_MonPasteEnd", "Paste at the end"), (s, e) => Paste(atEnd: true), any));
            return m;
        }

        private ContextMenuStrip CollectMenu()
        {
            var m = NewMenu();
            m.Items.Add(UiStyle.MenuHeader(T("Hdr_MonCollect", "Add every device used by...")));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Item(T("Mnu_MonFromScreen", "the screen on display - into this tab"), (s, e) => CollectFromScreen()));
            m.Items.Add(Item(T("Mnu_MonFromProgram", "the whole program - into this tab, a heading line per page"), (s, e) => CollectFromProgram(perPage: false, headings: true)));
            m.Items.Add(Item(T("Mnu_MonFromProgramList", "the whole program - into this tab, one list (each device once, no headings)"), (s, e) => CollectFromProgram(perPage: false, headings: false)));
            m.Items.Add(Item(T("Mnu_MonFromProgramTabs", "the whole program - one tab per page"), (s, e) => CollectFromProgram(perPage: true, headings: false)));
            m.Items.Add(new ToolStripSeparator());
            var labels = Item(T("Mnu_MonFromLabels", "every device named in the PLC label list (PlcLabels.csv) - into this tab"), (s, e) => CollectFromLabels());
            labels.ToolTipText = T("Tip_MonFromLabels",
                "PlcLabels.csv beside the program: the PLC programmer's names for its devices (Label / DataType / Address, from GX Works). " +
                "It names the devices on the monitor and adds devices the pages do not show.");
            m.ShowItemToolTips = true;
            m.Items.Add(labels);
            var conditions = Item(T("Mnu_MonFromConditions", "the machine condition labels - into this tab"), (s, e) => CollectConditions());
            conditions.ToolTipText = T("Tip_MonFromConditions",
                "The conditions the machines register (manual mode, robot at safe position ...): each one's device, and whether it is met now.");
            m.Items.Add(conditions);
            return m;
        }

        private ContextMenuStrip SheetMenu()
        {
            var m = NewMenu();
            var mon = CurrentMonitor;
            m.Items.Add(Item(T("Mnu_SheetNew", "New tab (before this one)..."), (s, e) => NewSheet(TabPlace.Before), mon != null, keyText: "Ins"));
            m.Items.Add(Item(T("Mnu_SheetNewAfter", "New tab (after this one)..."), (s, e) => NewSheet(TabPlace.After), mon != null && mon.Current != null));
            m.Items.Add(Item(T("Mnu_SheetNewEnd", "New tab at the end (+)..."), (s, e) => NewSheet(TabPlace.End), mon != null));
            m.Items.Add(Item(T("Mnu_SheetRename", "Rename this tab..."), (s, e) => RenameSheet(), mon != null && mon.Current != null, keyText: "F2"));
            m.Items.Add(Item(T("Mnu_SheetDelete", "Delete this tab"), (s, e) => DeleteSheet(), mon != null && mon.Current != null, keyText: "Del"));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Item(T("Mnu_SheetLeft", "Move this tab left"), (s, e) => MoveSheet(-1), mon != null && mon.SheetTabs.SelectedIndex > 0, keyText: "Ctrl+Left"));
            m.Items.Add(Item(T("Mnu_SheetRight", "Move this tab right"), (s, e) => MoveSheet(+1), mon != null && mon.SheetTabs.SelectedIndex < mon.Sheets.Count - 1, keyText: "Ctrl+Right"));
            return m;
        }

        /// <summary>What the delete button offers: a row, a tab, one controller's tabs, everything.</summary>
        private ContextMenuStrip DeleteMenu()
        {
            var m = NewMenu();
            var mon = CurrentMonitor;
            int rows = mon != null && mon.Current != null ? SelectedIndices(mon.Current).Count : 0;
            m.Items.Add(Item(rows > 1 ? string.Format(T("Mnu_DelRows", "The {0} selected rows"), rows)
                                      : T("Mnu_DelRow", "The selected row"), (s, e) => RemoveSelected(), rows > 0, keyText: "Del"));
            m.Items.Add(Item(mon != null && mon.Current != null
                                ? string.Format(T("Mnu_DelTab", "The tab '{0}'"), mon.Current.Name) : T("Mnu_DelTabNone", "This tab"),
                             (s, e) => DeleteSheet(), mon != null && mon.Current != null));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Item(string.Format(T("Mnu_DelAllTabs", "Every tab of {0}..."), mon != null ? PlcEndpointNaming.Caption(mon.Endpoint) : ""),
                             (s, e) => DeleteAllSheets(new[] { mon }), mon != null && mon.Sheets.Count > 0));
            m.Items.Add(Item(T("Mnu_DelEverything", "Every tab of every controller..."),
                             (s, e) => DeleteAllSheets(_monitors.ToArray()), _monitors.Any(x => x.Sheets.Count > 0)));
            return m;
        }

        /// <summary>
        /// Deletes every tab of the given controllers, after asking. Undo brings
        /// them all back in one step.
        /// </summary>
        internal void DeleteAllSheets(MonitorTab[] mons, bool ask = true)
        {
            mons = mons.Where(x => x != null && x.Sheets.Count > 0).ToArray();
            if (mons.Length == 0) return;
            int tabs = mons.Sum(x => x.Sheets.Count), rows = mons.Sum(x => x.Sheets.Sum(sh => sh.Rows.Count(r => !r.IsBlank)));
            string question = mons.Length == 1
                ? string.Format(T("Msg_DelAllTabs", "Delete all {0} tab(s) of {1}, with their {2} row(s)?"), tabs, PlcEndpointNaming.Caption(mons[0].Endpoint), rows)
                : string.Format(T("Msg_DelEverything", "Delete all {0} tab(s) of {1} controllers, with their {2} row(s)?"), tabs, mons.Length, rows);
            question += "\r\n\r\n" + T("Msg_UndoHint", "Undo (Ctrl+Z) brings them back.");
            if (ask && MessageBox.Show(DialogOwner, question, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Remember(mons.Length == 1 ? T("Undo_DelAllTabs", "delete every tab") : T("Undo_DelEverything", "delete every tab of every controller"), mons);
            foreach (var m in mons)
                foreach (var sh in m.Sheets.ToList()) RemoveSheet(m, sh, remember: false);
        }

        private void UpdateToolbar()
        {
            var mon = CurrentMonitor;
            if (_restoreItem != null)
                _restoreItem.Visible = mon != null && mon.IsSimulated && mon.Hidden.Count > 0;
            if (_btnUndo != null)
            {
                _btnUndo.Enabled = _undo.Count > 0;
                Kaban.UI.Controls.UiStyle.SetTip(_tips, _btnUndo, _undo.Count > 0
                    ? string.Format(T("Tip_MonUndoWhat", "Undo: {0} (Ctrl+Z)"), _undo[_undo.Count - 1].What)
                    : T("Tip_MonUndo", "Undo the last change (Ctrl+Z)"));
            }
            if (_btnRedo != null)
            {
                _btnRedo.Enabled = _redo.Count > 0;
                Kaban.UI.Controls.UiStyle.SetTip(_tips, _btnRedo, _redo.Count > 0
                    ? string.Format(T("Tip_MonRedoWhat", "Redo: {0} (Ctrl+Y)"), _redo[_redo.Count - 1].What)
                    : T("Tip_MonRedo", "Redo the change undone last (Ctrl+Y)"));
            }
        }

        // ==================================================================
        // UNDO
        // ------------------------------------------------------------------
        // Every change to the lists - add, paste, remove, move, a sort, an edit
        // of a name or address, a tab created, renamed, moved or deleted - first
        // keeps the state of the controllers it touches. Undo puts the last one
        // back. It is what gets a list back to its order before a sort: no sort
        // can, since the order a list was built in is not a column.
        //
        // Writing a VALUE to the PLC is not undone: that went to the machine.
        //
        // REDO takes back an undo: Undo keeps the state it replaces, Redo puts
        // that back. A new change after an undo ends what can be redone, as in
        // every editor.
        // ==================================================================

        private sealed class UndoStep
        {
            public string What = "";
            public readonly List<KeyValuePair<MonitorTab, List<string>>> States = new List<KeyValuePair<MonitorTab, List<string>>>();
        }

        private readonly List<UndoStep> _undo = new List<UndoStep>();
        private readonly List<UndoStep> _redo = new List<UndoStep>();
        private const int UndoDepth = 50;

        private static UndoStep Snapshot(UndoStep of)
        {
            var now = new UndoStep { What = of.What };
            foreach (var kv in of.States) now.States.Add(new KeyValuePair<MonitorTab, List<string>>(kv.Key, Serialize(kv.Key)));
            return now;
        }

        /// <summary>Keeps the state of the controllers about to change (the one on show by default).</summary>
        private void Remember(string what, params MonitorTab[] mons)
        {
            if (mons == null || mons.Length == 0) mons = new[] { CurrentMonitor };
            var step = new UndoStep { What = what ?? "" };
            foreach (var m in mons.Where(x => x != null))
                step.States.Add(new KeyValuePair<MonitorTab, List<string>>(m, Serialize(m)));
            if (step.States.Count == 0) return;
            _undo.Add(step);
            if (_undo.Count > UndoDepth) _undo.RemoveAt(0);
            _redo.Clear();
            UpdateToolbar();
        }

        /// <summary>Puts back the state kept before the last change.</summary>
        internal void Undo()
        {
            if (_undo.Count == 0) return;
            var mon = CurrentMonitor;
            if (mon != null && mon.Current != null && mon.Current.Grid.IsCurrentCellInEditMode) return;
            var step = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(Snapshot(step));
            foreach (var kv in step.States) RestoreState(kv.Key, kv.Value);
            UpdateToolbar();
            SetStatus(string.Format(T("Msg_Undone", "Undone: {0}."), step.What));
        }

        /// <summary>Puts back what the last undo took away.</summary>
        internal void Redo()
        {
            if (_redo.Count == 0) return;
            var mon = CurrentMonitor;
            if (mon != null && mon.Current != null && mon.Current.Grid.IsCurrentCellInEditMode) return;
            var step = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(Snapshot(step));
            if (_undo.Count > UndoDepth) _undo.RemoveAt(0);
            foreach (var kv in step.States) RestoreState(kv.Key, kv.Value);
            UpdateToolbar();
            SetStatus(string.Format(T("Msg_Redone", "Redone: {0}."), step.What));
        }

        private void RestoreState(MonitorTab mon, List<string> lines)
        {
            var tabs = mon.SheetTabs;
            int selected = tabs.SelectedIndex;
            tabs.SuspendLayout();
            foreach (var page in tabs.TabPages.Cast<TabPage>().ToList()) { tabs.TabPages.Remove(page); page.Dispose(); }
            mon.Sheets.Clear();
            mon.Hidden.Clear();
            mon.SavedTabCount = -1;
            Deserialize(mon, lines);
            foreach (var sh in mon.Sheets.ToList()) AttachSheet(mon, sh, tabs.TabPages.Count);
            tabs.ResumeLayout();
            if (tabs.TabPages.Count > 0) tabs.SelectedIndex = Math.Max(0, Math.Min(selected, tabs.TabPages.Count - 1));
            UpdateEmptyState(mon);
            SaveWatchList(mon);
            foreach (var sh in mon.Sheets) Render(mon, sh);
            if (ReferenceEquals(mon, CurrentMonitor)) RefreshValues();
        }

        // ---- the controllers and their tabs ------------------------------------

        private void BuildTabs()
        {
            var endpoints = PlcConnectionSettings.Endpoints()
                            .Where(ep => PlcManager.Instance.GetDriverForMonitoring(ep.StationId) != null)
                            .ToList();

            foreach (var endpoint in endpoints) _tabs.TabPages.Add(BuildTab(endpoint));

            if (_monitors.Count == 0)
            {
                var none = new TabPage(T("Tab_NoStations", "No controllers"));
                none.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(14),
                    Text = T("Msg_NoStations",
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
            var page = new TabPage(PlcEndpointNaming.Caption(endpoint)) { BackColor = Color.White };
            page.ToolTipText = PlcEndpointNaming.CaptionWithStation(endpoint);

            var mon = new MonitorTab { Endpoint = endpoint, IsSimulated = IsSimulatedStation(endpoint.StationId), Page = page };

            mon.SheetTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                SizeMode = TabSizeMode.Normal,
                Padding = new Point(16, 5),
                Font = UiStyle.Body
            };
            mon.SheetTabs.DrawItem += (s, e) =>
            {
                DrawSheetTab(mon, e);
                // The strip has laid out its tabs (a rename, a scroll): the "+" follows the last.
                if (e.Index == mon.SheetTabs.TabCount - 1 && mon.AddTabButton != null && mon.SheetTabs.IsHandleCreated)
                    mon.SheetTabs.BeginInvoke((MethodInvoker)(() => PlaceAddTab(mon)));
            };
            mon.SheetTabs.SelectedIndexChanged += (s, e) => { UpdateToolbar(); RefreshValues(); };
            mon.SheetTabs.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && SheetAt(mon, e.Location) >= 0) RenameSheet();
            };
            mon.SheetTabs.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                int i = SheetAt(mon, e.Location);
                if (i < 0) return;
                mon.SheetTabs.SelectedIndex = i;
                UiStyle.ShowPopupAt(SheetMenu(), mon.SheetTabs, mon.SheetTabs.PointToScreen(e.Location));
            };

            // NO "LIVE PLC" BANNER: the page is the admin's, who knows a value
            // typed here goes to the machine. Writes to a real PLC are logged.

            LoadWatchList(mon);
            // A first "Main" tab only for a controller that has never had a list;
            // one whose tabs were all deleted stays empty.
            if (mon.Sheets.Count == 0 && mon.SavedTabCount != 0) mon.Sheets.Add(new Sheet { Name = T("Tab_MonMain", "Main") });
            foreach (var sh in mon.Sheets.ToList()) AttachSheet(mon, sh, mon.SheetTabs.TabPages.Count);
            WireTabDrag(mon);

            mon.EmptyHint = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(120, 126, 136),
                Font = UiStyle.Body,
                Text = T("Msg_MonNoTabs", "No tabs. Add one with + (top left) or the tabs button (" + E(0x1F4D1) + "), or add a device and a tab 'Main' is made for it."),
                Visible = false
            };
            page.Controls.Add(mon.EmptyHint);

            page.Controls.Add(mon.SheetTabs);

            // The "+" after the last tab, as in a browser: a new tab at the end.
            mon.AddTabButton = NewTabButton(mon);
            page.Controls.Add(mon.AddTabButton);
            mon.AddTabButton.BringToFront();
            mon.SheetTabs.SizeChanged += (s, e) => PlaceAddTab(mon);
            mon.SheetTabs.HandleCreated += (s, e) => PlaceAddTab(mon);
            mon.SheetTabs.SelectedIndexChanged += (s, e) => PlaceAddTab(mon);
            UpdateEmptyState(mon);

            _monitors.Add(mon);
            return page;
        }

        private static int SheetAt(MonitorTab mon, Point p)
        {
            for (int i = 0; i < mon.SheetTabs.TabCount; i++)
                if (mon.SheetTabs.GetTabRect(i).Contains(p)) return i;
            return -1;
        }

        /// <summary>The engineer's tabs, drawn like the rest of the program: the chosen one white and bold.</summary>
        private static void DrawSheetTab(MonitorTab mon, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= mon.SheetTabs.TabCount) return;
            var r = mon.SheetTabs.GetTabRect(e.Index);
            bool sel = e.Index == mon.SheetTabs.SelectedIndex;
            using (var back = new SolidBrush(sel ? Color.White : Color.FromArgb(238, 240, 243)))
                e.Graphics.FillRectangle(back, r);
            if (sel)
                using (var rule = new Pen(Color.FromArgb(60, 66, 76), 2f))
                    e.Graphics.DrawLine(rule, r.Left + 4, r.Bottom - 2, r.Right - 4, r.Bottom - 2);
            var font = sel ? UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold) : UiStyle.Body;
            TextRenderer.DrawText(e.Graphics, mon.SheetTabs.TabPages[e.Index].Text, font, r,
                sel ? Color.FromArgb(25, 28, 34) : Color.FromArgb(80, 86, 96),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private void AttachSheet(MonitorTab mon, Sheet sh, int at)
        {
            sh.Grid = BuildGrid();
            // Nothing selected on opening: a highlighted first row reads as "this
            // one is special", and it is the row a stray Delete would remove.
            var g = sh.Grid;
            g.HandleCreated += (s, e) => g.BeginInvoke((MethodInvoker)(() => { g.ClearSelection(); g.CurrentCell = null; }));
            sh.Page = new TabPage(sh.Name) { BackColor = Color.White };
            sh.Page.Controls.Add(sh.Grid);
            sh.Page.Tag = sh;                               // the page KNOWS its tab: see Current
            WireGrid(mon, sh);
            EnsureBlankRow(sh);
            InsertPage(mon, sh.Page, at);
            mon.SyncOrder();
            UpdateEmptyState(mon);
        }

        /// <summary>
        /// Puts a page at a position of the strip. The strip's window is created
        /// first: without it WinForms appends the page at the end, whatever the
        /// index.
        /// </summary>
        private static void InsertPage(MonitorTab mon, TabPage page, int at)
        {
            var tabs = mon.SheetTabs;
            at = Math.Max(0, Math.Min(at, tabs.TabPages.Count));
            if (at >= tabs.TabPages.Count) { tabs.TabPages.Add(page); return; }
            if (!tabs.IsHandleCreated) { var unused = tabs.Handle; }
            tabs.TabPages.Insert(at, page);
        }

        private static void UpdateEmptyState(MonitorTab mon)
        {
            bool none = mon.SheetTabs.TabPages.Count == 0;
            mon.SheetTabs.Visible = !none;
            if (mon.EmptyHint != null) mon.EmptyHint.Visible = none;
            PlaceAddTab(mon);
        }

        private Button NewTabButton(MonitorTab mon)
        {
            var b = new Button
            {
                Text = "",
                Size = new Size(30, 24),
                BackColor = Color.FromArgb(238, 240, 243),
                Cursor = Cursors.Hand,
                TabStop = false,
                AccessibleName = "New tab"
            };
            UiStyle.Round(b);
            // The plus DRAWN: a character this small is clipped by the button's
            // own text margins on some fonts and sizes.
            b.Paint += (s, e) =>
            {
                float cx = b.Width / 2f, cy = b.Height / 2f, arm = Math.Min(b.Width, b.Height) * 0.26f;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                using (var ink = new SolidBrush(Color.FromArgb(40, 46, 56)))
                {
                    e.Graphics.FillRectangle(ink, cx - arm, cy - 1.2f, 2 * arm, 2.4f);
                    e.Graphics.FillRectangle(ink, cx - 1.2f, cy - arm, 2.4f, 2 * arm);
                }
            };
            Kaban.UI.Controls.UiStyle.SetTip(_tips, b, T("Tip_MonNewTab", "New tab at the end"));
            b.Click += (s, e) => NewSheet(TabPlace.End, mon);
            return b;
        }

        /// <summary>
        /// Puts the "+" just after the last tab. With more tabs than the strip
        /// shows, it stays at the strip's right end, beside the scroll arrows;
        /// with no tabs at all, top left, where the first would be.
        /// </summary>
        internal static void PlaceAddTab(MonitorTab mon)
        {
            var b = mon.AddTabButton;
            var tabs = mon.SheetTabs;
            if (b == null || tabs == null) return;
            if (!tabs.Visible || tabs.TabCount == 0)
            {
                b.Location = new Point(6, 5);
                return;
            }
            if (!tabs.IsHandleCreated) return;             // placed once the strip has its window
            var first = tabs.GetTabRect(0);
            var last = tabs.GetTabRect(tabs.TabCount - 1);
            int h = Math.Max(18, last.Height - 4);
            if (b.Height != h || b.Width != h + 8) b.Size = new Size(h + 8, h);
            int x = last.Right + 4;
            const int arrows = 44;                          // the strip's own scroll arrows
            if (last.Right - first.Left > tabs.Width - 4 || x + b.Width > tabs.Width - 2)
                x = Math.Max(0, tabs.Width - arrows - b.Width - 2);
            var at = new Point(tabs.Left + x, tabs.Top + last.Top + Math.Max(0, (last.Height - h) / 2));
            if (b.Location != at) b.Location = at;
            if (b.Parent != null && b.Parent.Controls.GetChildIndex(b) != 0) b.BringToFront();
        }

        /// <summary>
        /// The tab on display - or, when every tab has been deleted, a new "Main"
        /// one: adding a device should never fail for want of somewhere to put it.
        /// </summary>
        private Sheet EnsureSheet(MonitorTab mon)
        {
            if (mon == null) return null;
            var sh = mon.Current;
            if (sh != null) return sh;
            sh = AddSheet(mon, UniqueName(mon, T("Tab_MonMain", "Main")), 0);
            return sh;
        }

        /// <summary>A new, empty tab at a position of the strip, selected.</summary>
        internal Sheet AddSheet(MonitorTab mon, string name, int at)
        {
            var sh = new Sheet { Name = name };
            AttachSheet(mon, sh, at);
            mon.SheetTabs.SelectedTab = sh.Page;
            SaveWatchList(mon);
            UpdateToolbar();
            return sh;
        }

        /// <summary>Takes a tab away - any tab, the last one too.</summary>
        internal void RemoveSheet(MonitorTab mon, Sheet sh, bool remember = true)
        {
            if (remember) Remember(string.Format(T("Undo_DelTab", "delete the tab '{0}'"), sh.Name), mon);
            int i = mon.SheetTabs.TabPages.IndexOf(sh.Page);
            foreach (var r in sh.Rows.Where(r => r.IsDevice).ToList()) HideIfUnlisted(mon, r.Address, sh, -2);
            mon.SheetTabs.TabPages.Remove(sh.Page);
            sh.Page.Dispose();
            mon.SyncOrder();
            if (mon.SheetTabs.TabPages.Count > 0)
                mon.SheetTabs.SelectedIndex = Math.Max(0, Math.Min(i, mon.SheetTabs.TabPages.Count - 1));
            UpdateEmptyState(mon);
            SaveWatchList(mon);
            UpdateToolbar();
        }

        /// <summary>Moves a tab to a position of the strip, keeping it selected.</summary>
        internal void MoveSheetTo(MonitorTab mon, Sheet sh, int to)
        {
            var tabs = mon.SheetTabs;
            int from = tabs.TabPages.IndexOf(sh.Page);
            to = Math.Max(0, Math.Min(to, tabs.TabPages.Count - 1));
            if (from < 0 || from == to) return;
            tabs.SuspendLayout();
            tabs.TabPages.Remove(sh.Page);
            InsertPage(mon, sh.Page, to);
            tabs.SelectedTab = sh.Page;
            tabs.ResumeLayout();
            mon.SyncOrder();
            PlaceAddTab(mon);
        }

        // ---- drag a tab along the strip -------------------------------------------
        //
        // Hold a tab and slide it: it moves across the others as the pointer
        // passes them, several places in one gesture where the menu's "move left"
        // goes one at a time. Released, the new order is saved.

        private Sheet _dragSheet;
        private Point _dragSheetFrom;
        private bool _draggingSheet;

        private void WireTabDrag(MonitorTab mon)
        {
            var tabs = mon.SheetTabs;
            tabs.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                int i = SheetAt(mon, e.Location);
                _dragSheet = i >= 0 ? tabs.TabPages[i].Tag as Sheet : null;
                _dragSheetFrom = e.Location;
                _draggingSheet = false;
            };
            tabs.MouseMove += (s, e) =>
            {
                if (_dragSheet == null || e.Button != MouseButtons.Left) return;
                if (!_draggingSheet)
                {
                    if (Math.Abs(e.X - _dragSheetFrom.X) < SystemInformation.DragSize.Width * 2) return;
                    _draggingSheet = true;
                    Remember(string.Format(T("Undo_MoveTab", "move the tab '{0}'"), _dragSheet.Name), mon);
                    tabs.Cursor = Cursors.SizeWE;
                }
                int over = SheetAt(mon, e.Location);
                if (over < 0)
                {
                    // Past the last tab or before the first: to that end.
                    if (tabs.TabCount == 0) return;
                    var last = tabs.GetTabRect(tabs.TabCount - 1);
                    if (e.X > last.Right) over = tabs.TabCount - 1;
                    else if (e.X < tabs.GetTabRect(0).Left) over = 0;
                    else return;
                }
                int from = tabs.TabPages.IndexOf(_dragSheet.Page);
                if (over == from || from < 0) return;
                // Only once the pointer is far enough over the other tab that, moved,
                // the dragged tab is still under it: a narrow tab passing a wide one
                // would otherwise swap back and forth on every pixel.
                var target = tabs.GetTabRect(over);
                int width = tabs.GetTabRect(from).Width;
                if (over > from ? e.X < target.Right - width : e.X > target.Left + width) return;
                MoveSheetTo(mon, _dragSheet, over);
            };
            MouseEventHandler end = (s, e) =>
            {
                if (_draggingSheet) { SaveWatchList(mon); tabs.Cursor = Cursors.Default; }
                _dragSheet = null;
                _draggingSheet = false;
            };
            tabs.MouseUp += end;
            tabs.MouseCaptureChanged += (s, e) => { if (_draggingSheet && Control.MouseButtons == MouseButtons.None) end(s, null); };
        }

        /// <summary>
        /// THE MONITOR'S TABLE LOOK - its lists and Find's list alike, so the two
        /// read as one tool: fonts, heading, row height, lines, selection.
        ///
        /// The HEADING SIZES ITSELF to its font. It was a fixed height (the
        /// resizable mode keeps whatever height the grid was born with), shorter
        /// than the bold heading font with its padding, so on the panel the
        /// captions were cut through the middle.
        /// </summary>
        internal static void StyleGrid(DataGridView grid)
        {
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            UiStyle.GridLook(grid);
        }

        private static DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                // The blank line at the end is a row we own, not the DataGridView's
                // own "new row", whose placeholder fights a 250ms refresh.
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                MultiSelect = true
            };
            StyleGrid(grid);

            // Label first: an engineer thinks in names, not addresses.
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Label", HeaderText = T("Col_Label", "Label") });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DataType", HeaderText = T("Col_DataType", "DataType") });
            // How the value is shown and typed: decimal, hexadecimal, binary.
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Format", HeaderText = T("Col_DisplayFormat", "Display Format"), ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Address", HeaderText = T("Col_Address", "Address") });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = T("Col_Value", "Value") });
            grid.Columns["Value"].DefaultCellStyle.Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);
            grid.Columns["Value"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            // WHEN IT LAST CHANGED, AND WHO CHANGED IT: the HMI (a write of that very
            // value just before) or the PLC program. See DeviceHistory.
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Changed", HeaderText = T("Col_LastChange", "Modified"), ReadOnly = true });
            // A MACHINE CONDITION's row: met or not, now (shown on a tab that has one).
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Condition", HeaderText = T("Col_Condition", "Condition"), ReadOnly = true, Visible = false });
            grid.ShowCellToolTips = true;

            // EXPLICIT WIDTHS, each column its own: narrowing the last column
            // leaves space at the right instead of widening the one before it (a
            // Fill grid always stretches to the edge). Headings can be dragged to
            // re-order the columns, and a click sorts on one - see SortSheet. The
            // widths and order are kept, the same for every tab.
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.AllowUserToResizeColumns = true;
            grid.AllowUserToOrderColumns = true;
            foreach (DataGridViewColumn c in grid.Columns) c.SortMode = DataGridViewColumnSortMode.Programmatic;
            ApplyColumnLayout(grid);
            return grid;
        }

        // ==================================================================
        // GRID BEHAVIOUR
        // ==================================================================

        private void WireGrid(MonitorTab mon, Sheet sh)
        {
            var grid = sh.Grid;

            var chooser = AttachColumnChooser(mon, sh);
            var rowMenu = NewMenu();
            rowMenu.Opening += (s, e) =>
            {
                // On the heading row (beside the last heading too): the columns' menu.
                if (chooser.OnHeadings(grid.PointToClient(Cursor.Position)))
                {
                    e.Cancel = true;
                    UiStyle.ShowPopupAt(chooser.Menu, grid, Cursor.Position);
                    return;
                }
                rowMenu.Items.Clear();
                bool sel = SelectedIndices(sh).Count > 0;
                bool any = _clip.Count > 0 || ClipboardHasText();
                rowMenu.Items.Add(Item(T("Mnu_EditRow", "Edit..."), (s2, e2) => { if (grid.CurrentRow != null) EditRow(mon, sh, grid.CurrentRow.Index, null); }, grid.CurrentRow != null, keyText: "Enter"));
                var at = grid.CurrentRow != null && grid.CurrentRow.Index < sh.Rows.Count ? sh.Rows[grid.CurrentRow.Index] : null;
                rowMenu.Items.Add(Item(at != null && at.IsDevice ? string.Format(T("Mnu_FindUses", "Where is {0} used?"), at.Address) : T("Mnu_FindUsesNone", "Where is it used?"),
                                       (s2, e2) => OpenFind(at != null ? at.Address : null), at != null && at.IsDevice, keyText: "Ctrl+F"));
                // the row itself when it holds others (a structure, an element of an array of them), else its item
                var head = at == null ? null : at.IsGroup ? at : at.Head != null && at.Head.IsGroup ? at.Head : at.Root;
                if (head != null && head.IsGroup)
                    rowMenu.Items.Add(Item(head.IsArray ? (head.Expanded ? T("Mnu_MonCollapse", "Collapse the array") : T("Mnu_MonExpand", "Expand the array"))
                                                        : (head.Expanded ? T("Mnu_MonCollapseStruct", "Collapse the structure") : T("Mnu_MonExpandStruct", "Expand the structure")),
                                           (s2, e2) => ToggleArray(mon, sh, head)));
                // 19 October: 0 into every device of the rows chosen (a structure's members, an array's elements)
                bool devices = SelectedIndices(sh).Any(i => i < sh.Rows.Count && sh.Rows[i].IsDevice);
                rowMenu.Items.Add(Item(T("Mnu_MonReset", "Reset (0 into its devices)..."), (s2, e2) => ResetSelected(mon, sh), devices));
                if (sel)
                {
                    var fmt = new ToolStripMenuItem(T("Col_DisplayFormat", "Display Format"));
                    foreach (var f in DisplayFormats)
                    {
                        var name = f;
                        fmt.DropDownItems.Add(Item(T("Fmt_" + name, name), (s2, e2) => SetDisplayFormat(mon, sh, name)));
                    }
                    rowMenu.Items.Add(fmt);
                }
                rowMenu.Items.Add(new ToolStripSeparator());
                rowMenu.Items.Add(Item(T("Mnu_MonAddAbove", "New device above the selected row..."), (s2, e2) => AddRows(false, false), sel));
                rowMenu.Items.Add(Item(T("Mnu_MonCommentAbove", "Comment line above the selected row..."), (s2, e2) => AddRows(false, true), sel));
                rowMenu.Items.Add(new ToolStripSeparator());
                rowMenu.Items.Add(Item(T("Mnu_MonCopy", "Copy"), (s2, e2) => CopySelected(), sel, Keys.Control | Keys.C));
                rowMenu.Items.Add(Item(T("Mnu_MonPasteAbove", "Paste above the selected row"), (s2, e2) => Paste(false), any && sel, Keys.Control | Keys.V));
                rowMenu.Items.Add(Item(T("Mnu_MonPasteEnd", "Paste at the end"), (s2, e2) => Paste(true), any));
                rowMenu.Items.Add(new ToolStripSeparator());
                rowMenu.Items.Add(Item(T("Mnu_RemoveRow", "Remove from this list"), (s2, e2) => RemoveSelected(), sel, Keys.Delete));
                if (mon.IsSimulated && mon.Hidden.Count > 0)
                    rowMenu.Items.Add(Item(T("Mnu_RestoreRows", "Show removed devices again"), (s2, e2) => RestoreHidden()));
                e.Cancel = false;           // it opened empty: WinForms had set Cancel (the first right-click)
            };
            grid.ContextMenuStrip = rowMenu;

            // A right-click on an UNSELECTED row selects it, as in Explorer. On a
            // selected row it changes nothing: the menu is for the whole selection.
            // (Setting the current cell - which this also did - drops every other
            // selected row, so Copy and Remove saw just one.)
            grid.CellMouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
                if (grid.Rows[e.RowIndex].Selected) return;
                grid.ClearSelection();
                grid.CurrentCell = grid.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
                grid.Rows[e.RowIndex].Selected = true;
            };

            // ---- columns: drag to re-order, resize freely, click to sort ----
            // A heading DRAGGED to another place, or a border dragged to resize,
            // ends in a click on a heading too - that is not a request to sort.
            string layoutAtPress = "";
            Func<string> layout = () => string.Join(",", grid.Columns.Cast<DataGridViewColumn>().Select(c => c.DisplayIndex + ":" + c.Width));
            grid.CellMouseDown += (s, e) => { if (e.RowIndex < 0) layoutAtPress = layout(); };
            grid.ColumnHeaderMouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || e.ColumnIndex < 0) return;
                if (layoutAtPress.Length > 0 && layoutAtPress != layout()) return;
                SortSheet(mon, sh, grid.Columns[e.ColumnIndex].Name);
            };
            grid.ColumnWidthChanged += (s, e) => ColumnLayoutChanged(grid);
            grid.ColumnDisplayIndexChanged += (s, e) => ColumnLayoutChanged(grid);

            grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= sh.Rows.Count) return;
                OpenCell(mon, sh, e.RowIndex, e.ColumnIndex >= 0 ? grid.Columns[e.ColumnIndex].Name : "");
            };
            // AN ARRAY folds and unfolds on the triangle before its label.
            grid.CellMouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || e.RowIndex < 0 || e.RowIndex >= sh.Rows.Count || e.ColumnIndex < 0) return;
                if (grid.Columns[e.ColumnIndex].Name != "Label") return;
                var r = sh.Rows[e.RowIndex];
                if (e.X > FoldMarkRight(r)) return;
                if (r.IsGroup) ToggleArray(mon, sh, r);
            };
            // Tab leaves the table for the next control: stepping cell by cell
            // through a table that selects whole rows shows nothing moving.
            grid.StandardTab = true;

            // ---- drag a row to re-order ----
            // Only once the pointer has really MOVED: starting the drag on the first
            // pixel of movement swallowed the second click of a double-click, and a
            // double-click never opened anything.
            grid.AllowDrop = true;
            grid.MouseDown += (s, e) =>
            {
                var hit = grid.HitTest(e.X, e.Y);
                sh.DragFrom = e.Button == MouseButtons.Left && hit.Type == DataGridViewHitTestType.Cell ? hit.RowIndex : -1;
                sh.DragStart = e.Location;
            };
            grid.MouseMove += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || sh.DragFrom < 0 || sh.Editing) return;
                var d = SystemInformation.DragSize;
                if (Math.Abs(e.X - sh.DragStart.X) < d.Width * 2 && Math.Abs(e.Y - sh.DragStart.Y) < d.Height * 2) return;
                int from = sh.DragFrom;
                sh.DragFrom = -1;
                if (from < sh.Rows.Count && !sh.Rows[from].IsBlank)
                {
                    // An element moves its whole array.
                    if (sh.Rows[from].IsElement) from = sh.Rows.IndexOf(sh.Rows[from].Root);
                    grid.DoDragDrop(from, DragDropEffects.Move);
                }
            };
            grid.MouseUp += (s, e) => sh.DragFrom = -1;
            grid.DragOver += (s, e) => e.Effect = DragDropEffects.Move;
            grid.DragDrop += (s, e) =>
            {
                if (!e.Data.GetDataPresent(typeof(int))) return;
                int from = (int)e.Data.GetData(typeof(int));
                var pt = grid.PointToClient(new Point(e.X, e.Y));
                int to = grid.HitTest(pt.X, pt.Y).RowIndex;
                if (to < 0) to = sh.Rows.Count - 1;
                if (from < 0 || from >= sh.Rows.Count || from == to) return;
                // The blank line stays last: it is where the next device is typed.
                if (sh.Rows[from].IsBlank) return;
                to = Math.Min(to, sh.Rows.Count - 1);
                Remember(T("Undo_Move", "move rows"), mon);
                var moved = sh.Rows[from];
                // Dropped among an array's elements: before that array.
                var target = sh.Rows[to].Root;
                var items = Items(sh);
                int toItem = items.IndexOf(target);
                items.Remove(moved);
                items.Insert(Math.Max(0, Math.Min(toItem, items.Count - 1)), moved);
                sh.Rows.Clear();
                sh.Rows.AddRange(items);
                EnsureBlankRow(sh);
                EnsureElements(sh);
                SaveWatchList(mon);
                Render(mon, sh);
                Select(sh, new[] { sh.Rows.IndexOf(moved) });
            };

            // pause the refresh while the user types, so edits are not overwritten
            grid.CellBeginEdit += (s, e) =>
            {
                var row = e.RowIndex >= 0 && e.RowIndex < sh.Rows.Count ? sh.Rows[e.RowIndex] : null;
                string col = grid.Columns[e.ColumnIndex].Name;
                // A comment is only its text; the empty line has no value to write.
                if (row == null || (row.IsComment && col != "Label") || (row.IsBlank && col == "Value")) { e.Cancel = true; return; }
                // An element is its array's: only its value is its own. An array's
                // value is the list of its elements'.
                if ((row.IsElement && col != "Value") || (row.IsGroup && col == "Value") || col == "Format" || col == "Changed") { e.Cancel = true; return; }
                // The label without the fold mark and the count shown around it.
                if (row.IsGroup && col == "Label") grid.Rows[e.RowIndex].Cells["Label"].Value = row.EffectiveLabel;
                sh.Editing = true;
                // A value goes to the PLC and is not undone; a name or an address is.
                if (col != "Value") Remember(T("Undo_Edit", "edit a row"), mon);
            };
            grid.CellEndEdit += (s, e) =>
            {
                sh.Editing = false;
                CommitCellEdit(mon, sh, e.RowIndex, e.ColumnIndex);
            };
            grid.EditingControlShowing += (s, e) =>
            {
                var tb = e.Control as TextBox;
                if (tb != null) tb.Font = grid.CurrentCell != null && grid.CurrentCell.OwningColumn.Name == "Value"
                                          ? grid.Columns["Value"].DefaultCellStyle.Font : UiStyle.Body;
            };

            // The empty last line says what it is for.
            grid.CellPainting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= sh.Rows.Count || e.ColumnIndex != 0) return;
                if (!sh.Rows[e.RowIndex].IsBlank || grid.IsCurrentCellInEditMode && grid.CurrentCell.RowIndex == e.RowIndex) return;
                e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) != 0);
                TextRenderer.DrawText(e.Graphics, T("Hint_MonNewRow", "Double-click to add a device"),
                    UiStyle.FontVariant(UiStyle.Small, FontStyle.Italic), Rectangle.Inflate(e.CellBounds, -6, 0),
                    Color.FromArgb(150, 156, 166), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.Handled = true;
            };
        }

        /// <summary>
        /// What a double-click - or Enter - does to a cell: a VALUE (or a comment)
        /// is edited right there in the table; any other cell opens the row's
        /// editor on that field; the empty last line adds a device.
        /// </summary>
        private void OpenCell(MonitorTab mon, Sheet sh, int rowIndex, string column)
        {
            if (rowIndex < 0 || rowIndex >= sh.Rows.Count) return;
            var row = sh.Rows[rowIndex];
            if (row.IsBlank) { AddRows(atEnd: true, comment: false); return; }
            if (column == "Label" && row.IsGroup && sh.Grid.PointToClient(Cursor.Position).X -
                sh.Grid.GetCellDisplayRectangle(sh.Grid.Columns["Label"].Index, rowIndex, false).X <= FoldMarkRight(row)) return;   // the fold mark
            if (row.IsComment || (column == "Value" && row.IsDevice && !row.IsGroup))
            {
                // In the table itself: the value is what is changed nine times
                // out of ten, and a dialog for it is a detour.
                sh.Grid.CurrentCell = sh.Grid.Rows[rowIndex].Cells[row.IsComment ? "Label" : "Value"];
                sh.Grid.BeginEdit(true);
                return;
            }
            EditRow(mon, sh, rowIndex, column);
        }

        // ---- find where a device is used -------------------------------------

        private DeviceFindForm _find;

        /// <summary>
        /// Opens Find for the controller on show - with the selected row's address,
        /// or the text given. Clicking a use opens its page and frames the unit.
        /// </summary>
        internal DeviceFindForm OpenFind(string query)
        {
            var mon = CurrentMonitor;
            if (mon == null) return null;
            if (query == null && mon.Current != null)
            {
                var sel = SelectedIndices(mon.Current);
                if (sel.Count > 0 && mon.Current.Rows[sel[0]].IsDevice) query = mon.Current.Rows[sel[0]].Address;
            }
            if (Embedded) return OpenFindInside(mon, query);
            if (_find != null && !_find.IsDisposed) _find.Close();
            _find = new DeviceFindForm(mon.Station, PlcEndpointNaming.CaptionWithStation(mon.Endpoint), query, OpenUse);
            WireFindLive(_find);
            _find.Show(this);
            return _find;
        }

        // In the side panel Find is part of the monitor - a pane under the lists -
        // not a window over the page it is about to open.
        private Panel _findHost;
        private Splitter _findSplit;

        private DeviceFindForm OpenFindInside(MonitorTab mon, string query)
        {
            if (_find != null && !_find.IsDisposed && _find.Station != mon.Station)
            {
                _findHost.Controls.Remove(_find);
                _find.Dispose();
                _find = null;
            }
            if (_findHost == null)
            {
                _findHost = new Panel { Dock = DockStyle.Bottom, Height = 260, BackColor = Color.White };
                _findSplit = new Splitter { Dock = DockStyle.Bottom, Height = 6, BackColor = Color.FromArgb(214, 218, 224), MinSize = 120, MinExtra = 120 };
                // Docked above the status line, below the lists (the higher the index, the earlier it docks).
                int under = Controls.GetChildIndex(_status);
                Controls.Add(_findHost);
                Controls.SetChildIndex(_findHost, under);
                Controls.Add(_findSplit);
                Controls.SetChildIndex(_findSplit, Controls.GetChildIndex(_findHost));
            }
            if (_find == null || _find.IsDisposed)
            {
                _find = new DeviceFindForm(mon.Station, PlcEndpointNaming.CaptionWithStation(mon.Endpoint), query, OpenUse,
                                           embedded: true, close: CloseFindInside);
                WireFindLive(_find);
                _find.Dock = DockStyle.Fill;
                _findHost.Controls.Add(_find);
                _find.Visible = true;
            }
            _findHost.Visible = _findSplit.Visible = true;
            _find.Begin(query);
            return _find;
        }

        /// <summary>
        /// Find's Value and Modified cells: the devices found are read with the
        /// tab's (see RefreshValues) and shown as a list's row would show them -
        /// in the display format of that row when the monitor lists the device.
        /// </summary>
        private void WireFindLive(DeviceFindForm find)
        {
            find.DefaultFormat = use =>
            {
                var m = _monitors.FirstOrDefault(x => x.Station == find.Station);
                var listed = m == null ? null : m.Sheets.SelectMany(x => x.Rows)
                    .FirstOrDefault(r => r.IsDevice && !r.IsGroup && !r.IsElement && string.Equals(r.Address, use.Address, StringComparison.OrdinalIgnoreCase));
                return listed != null ? listed.EffectiveFormat : null;
            };
            find.Live = (use, format) =>
            {
                var m = _monitors.FirstOrDefault(x => x.Station == find.Station);
                if (m == null) return null;
                var row = FindRow(m, use, format);
                bool nonZero;
                var value = FormatValue(m, row, out nonZero);
                var cells = new DeviceFindForm.LiveCells { Value = value ?? "-" };
                var probe = new DataGridViewTextBoxCell();
                RenderLastChange(m, row, probe);
                cells.Modified = Convert.ToString(probe.Value);
                cells.ModifiedTip = probe.ToolTipText ?? "";
                cells.ModifiedColor = probe.Style.ForeColor;
                return cells;
            };
        }

        /// <summary>A device Find found, as a row of the monitor would hold it.</summary>
        private static WatchRow FindRow(MonitorTab mon, DeviceCollector.Use use, string format)
        {
            return new WatchRow { Station = mon.Station, Address = use.Address, DataTypeOverride = use.DataType ?? "", DisplayFormat = format ?? "" };
        }

        /// <summary>The words of the devices Find lists for this controller (read with the tab's).</summary>
        private List<string> FindWords(MonitorTab mon)
        {
            var list = new List<string>();
            if (_find == null || _find.IsDisposed || _find.Station != mon.Station || !FindShowing) return list;
            foreach (var use in _find.Found)
                if (!string.IsNullOrWhiteSpace(use.Address)) list.AddRange(WordsOf(FindRow(mon, use, "")));
            return list;
        }

        private bool FindShowing
        {
            get
            {
                if (_find == null || _find.IsDisposed) return false;
                return Embedded ? _findHost != null && _findHost.Visible : _find.Visible;
            }
        }

        private void CloseFindInside()
        {
            if (_findHost != null) _findHost.Visible = _findSplit.Visible = false;
            var mon = CurrentMonitor;
            if (mon != null && mon.Current != null) mon.Current.Grid.Focus();
        }

        /// <summary>Opens a use's page in the main window and frames the unit; "" or why not.</summary>
        internal string OpenUse(DeviceCollector.Use use)
        {
            var shell = Owner as MainForm ?? Application.OpenForms.OfType<MainForm>().FirstOrDefault();
            if (shell == null) return T("Msg_FindNoShell", "The main window is not open.");
            string why;
            var target = shell.ShowDeviceUse(use, out why);
            if (target == null) return why;
            // Once the page has laid itself out.
            shell.BeginInvoke((MethodInvoker)(() => ControlHighlight.Flash(target)));
            return "";
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var monK = CurrentMonitor;
            var shK = monK != null ? monK.Current : null;
            bool editing = shK != null && shK.Grid.IsCurrentCellInEditMode;
            // Anywhere in the window, unless a cell is being typed into.
            if (!editing)
            {
                if (keyData == (Keys.Control | Keys.Z)) { Undo(); return true; }
                if (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z)) { Redo(); return true; }
                if (keyData == (Keys.Control | Keys.F)) { OpenFind(null); return true; }
            }
            // The tab strip of the controller's tabs, when it has the focus.
            if (monK != null && monK.SheetTabs.Focused)
            {
                switch (keyData)
                {
                    case Keys.Delete: DeleteSheet(); return true;
                    case Keys.F2: RenameSheet(); return true;
                    case Keys.Insert: NewSheet(TabPlace.Before); return true;
                    case Keys.Control | Keys.Left: MoveSheet(-1); return true;
                    case Keys.Control | Keys.Right: MoveSheet(+1); return true;
                }
            }
            if (shK != null && !editing && shK.Grid.ContainsFocus && keyData == Keys.Enter && shK.Grid.CurrentCell != null)
            {
                OpenCell(monK, shK, shK.Grid.CurrentCell.RowIndex, shK.Grid.CurrentCell.OwningColumn.Name);
                return true;
            }
            // SHIFT+ENTER: the value, at once - a bit is switched over, anything else
            // opens its value in the table with the number selected, whichever cell
            // the row was entered by.
            if (shK != null && !editing && shK.Grid.ContainsFocus && keyData == (Keys.Shift | Keys.Enter) && shK.Grid.CurrentCell != null)
            {
                QuickValue(monK, shK, shK.Grid.CurrentCell.RowIndex);
                return true;
            }

            var mon = CurrentMonitor;
            var sh = mon != null ? mon.Current : null;
            bool gridHasFocus = sh != null && sh.Grid.ContainsFocus && !sh.Grid.IsCurrentCellInEditMode;
            if (gridHasFocus)
            {
                switch (keyData)
                {
                    case Keys.Control | Keys.C: CopySelected(); return true;
                    case Keys.Control | Keys.V: Paste(atEnd: SelectedIndices(sh).Count == 0); return true;
                    case Keys.Delete: RemoveSelected(); return true;
                    case Keys.Insert: AddRows(atEnd: SelectedIndices(sh).Count == 0, comment: false); return true;
                    case Keys.Control | Keys.Up: MoveSelected(-1); return true;
                    case Keys.Control | Keys.Down: MoveSelected(+1); return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>Selected rows, top to bottom, the empty last line left out.</summary>
        private static List<int> SelectedIndices(Sheet sh)
        {
            var list = sh.Grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index)
                         .Where(i => i >= 0 && i < sh.Rows.Count && !sh.Rows[i].IsBlank).ToList();
            if (list.Count == 0 && sh.Grid.CurrentCell != null)
            {
                int i = sh.Grid.CurrentCell.RowIndex;
                if (i >= 0 && i < sh.Rows.Count && !sh.Rows[i].IsBlank && sh.Grid.Rows[i].Selected) list.Add(i);
            }
            list.Sort();
            return list;
        }

        /// <summary>Where "above the selected row" is: the first selected row, else the end.</summary>
        private static int InsertPoint(Sheet sh, bool atEnd)
        {
            int end = sh.Rows.FindIndex(r => r.IsBlank);
            if (end < 0) end = sh.Rows.Count;
            if (atEnd) return end;
            var sel = SelectedIndices(sh);
            return sel.Count > 0 ? sel[0] : end;
        }

        private static void Select(Sheet sh, IEnumerable<int> rows)
        {
            rows = rows.ToList();
            // An element asked for (Find, a paste) in a folded array: the array unfolds.
            foreach (var i in rows)
                if (i >= 0 && i < sh.Rows.Count && sh.Rows[i].IsElement && !IsShown(sh.Rows[i]))
                {
                    // every row it is under (an array, an element of structures) unfolds
                    for (var head = sh.Rows[i].Head; head != null; head = head.Head) head.Expanded = true;
                    for (int k = 0; k < sh.Rows.Count && k < sh.Grid.Rows.Count; k++)
                        if (IsShown(sh.Rows[k]) && !sh.Grid.Rows[k].Visible) sh.Grid.Rows[k].Visible = true;
                }
            sh.Grid.ClearSelection();
            int first = -1;
            foreach (var i in rows)
            {
                if (i < 0 || i >= sh.Grid.Rows.Count) continue;
                sh.Grid.Rows[i].Selected = true;
                if (first < 0) first = i;
            }
            if (first >= 0)
            {
                try { sh.Grid.CurrentCell = sh.Grid.Rows[first].Cells[0]; } catch { }
                foreach (var i in rows) if (i >= 0 && i < sh.Grid.Rows.Count) sh.Grid.Rows[i].Selected = true;
                if (first < sh.Grid.FirstDisplayedScrollingRowIndex ||
                    first >= sh.Grid.FirstDisplayedScrollingRowIndex + sh.Grid.DisplayedRowCount(false))
                    try { sh.Grid.FirstDisplayedScrollingRowIndex = first; } catch { }
            }
        }

        /// <summary>
        /// The list always ends in exactly one empty line - the line the operator
        /// types the next device into.
        /// </summary>
        private static void EnsureBlankRow(Sheet sh)
        {
            sh.Rows.RemoveAll(r => r.IsBlank);
            sh.Rows.Add(new WatchRow());
        }

        private void CommitCellEdit(MonitorTab mon, Sheet sh, int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= sh.Rows.Count) return;
            if (columnIndex < 0 || columnIndex >= sh.Grid.Columns.Count) return;

            var row = sh.Rows[rowIndex];
            string column = sh.Grid.Columns[columnIndex].Name;
            string typed = (Convert.ToString(sh.Grid.Rows[rowIndex].Cells[columnIndex].Value) ?? "").Trim();

            switch (column)
            {
                case "Label":
                    if (row.IsComment)
                    {
                        // An emptied comment is removed, like an emptied address.
                        if (typed.Length == 0) sh.Rows.RemoveAt(rowIndex); else row.Comment = typed;
                    }
                    else if (row.IsBlank)
                    {
                        // Text typed into the empty line's Label, with no address, is a comment.
                        if (typed.Length > 0) row.Comment = typed;
                    }
                    else row.LabelOverride = typed;
                    break;

                case "DataType":
                    if (row.IsDevice) row.DataTypeOverride = typed;
                    break;

                case "Address":
                {
                    string wanted = typed.ToUpperInvariant();
                    if (wanted.Length == 0)
                    {
                        if (row.IsDevice) { HideIfUnlisted(mon, row.Address, sh, rowIndex); sh.Rows.RemoveAt(rowIndex); }
                        break;
                    }
                    row.Address = wanted;
                    row.Comment = "";
                    // typing a device un-hides it, or it would vanish on the next refresh
                    mon.Hidden.Remove(wanted);
                    break;
                }

                case "Value":
                    WriteValue(mon, row, typed);
                    break;
            }

            EnsureBlankRow(sh);
            SaveWatchList(mon);
            Render(mon, sh);
        }

        /// <summary>Opens the editor for a row, in the field of the cell that was double-clicked.</summary>
        private void EditRow(MonitorTab mon, Sheet sh, int rowIndex, string focusField)
        {
            if (rowIndex < 0 || rowIndex >= sh.Rows.Count) return;
            var row = sh.Rows[rowIndex];
            if (row.IsBlank) { AddRows(true, false); return; }
            if (row.IsElement) row = row.Root;           // an element (a member) is edited as its array (structure)

            bool nz;
            string valueText = row.IsDevice && !row.IsGroup ? (FormatValue(mon, row, out nz) ?? "") : "";

            using (var dlg = new PlcRowEditorForm(PlcEndpointNaming.Caption(mon.Endpoint),
                       row.IsComment ? row.Comment : row.EffectiveLabel, row.EffectiveDataType, row.Address, valueText,
                       focusField == "Format" ? "DataType" : focusField, isNew: false, isComment: row.IsComment,
                       displayFormat: row.DisplayFormat, arrayCount: row.ArrayCount, stringLength: row.StringLength, station: mon.Station))
            {
                if (dlg.ShowDialog(DialogOwner) != DialogResult.OK) return;
                Remember(T("Undo_Edit", "edit a row"), mon);

                if (row.IsComment) row.Comment = dlg.RowLabel;
                else
                {
                    string wanted = dlg.RowAddress;
                    row.Address = wanted;
                    mon.Hidden.Remove(wanted);
                    ApplyNames(mon, row, dlg.RowLabel, dlg.RowDataType);
                    row.DisplayFormat = dlg.RowDisplayFormat == FormatDecimal ? "" : dlg.RowDisplayFormat;
                    row.StringLength = dlg.RowStringLength;
                    if (dlg.ArrayCount != row.ArrayCount)
                    {
                        if (row.ArrayCount <= 1) row.Expanded = true;     // just made an array: show it
                        row.ArrayCount = dlg.ArrayCount;
                    }
                    if (dlg.RowValueText != null && dlg.RowValueText != valueText && !row.IsGroup)
                        WriteValue(mon, row, dlg.RowValueText);
                }
                EnsureBlankRow(sh);
                SaveWatchList(mon);
                Render(mon, sh);
            }
        }

        /// <summary>
        /// Stores a label and a type only where they differ from the PLC label
        /// list, so a corrected PlcLabels.csv still flows through later.
        /// </summary>
        private static void ApplyNames(MonitorTab mon, WatchRow row, string label, string dataType)
        {
            var known = PlcLabelTable.ByAddress(mon.Station, row.Address);
            row.LabelOverride = known != null && string.Equals(known.Label, label, StringComparison.Ordinal) ? "" : (label ?? "");
            row.DataTypeOverride = known != null && string.Equals(known.DataType, dataType, StringComparison.Ordinal) ? "" : (dataType ?? "");
        }

        // ---- add, copy, paste, remove, move ----------------------------------------

        /// <summary>
        /// A new device - or an ARRAY of them - or a comment line, at the end or
        /// above the selected row.
        /// </summary>
        private void AddRows(bool atEnd, bool comment)
        {
            var mon = CurrentMonitor;
            if (mon == null) return;
            // With every tab deleted, a "Main" one is made - once the device is
            // confirmed, not for a dialog that is then cancelled.
            var sh = mon.Current;
            int at = sh != null ? InsertPoint(sh, atEnd) : 0;

            // Suggest what comes next: the device after the one above the new line.
            string suggestType = "Bit", suggestAddress = "";
            var above = sh != null ? sh.Rows.Take(at).LastOrDefault(r => r.IsDevice) : null;
            if (above != null && !comment)
            {
                suggestType = string.IsNullOrWhiteSpace(above.EffectiveDataType) ? "Bit" : above.EffectiveDataType;
                suggestAddress = PlcRowEditorForm.ElementAddress(above.Address, suggestType, 1, 16, mon.Station);
            }

            using (var dlg = new PlcRowEditorForm(PlcEndpointNaming.Caption(mon.Endpoint), "", suggestType, suggestAddress, "",
                                                  comment ? "Label" : "Address", isNew: true, isComment: comment, station: mon.Station))
            {
                if (dlg.ShowDialog(DialogOwner) != DialogResult.OK) return;
                Remember(dlg.IsComment ? T("Undo_AddComment", "add a comment line") : T("Undo_AddDevice", "add a device"), mon);
                if (sh == null) { sh = EnsureSheet(mon); at = InsertPoint(sh, true); }

                var added = new List<WatchRow>();
                if (dlg.IsComment) added.Add(new WatchRow { Station = mon.Station, Comment = dlg.RowLabel });
                else
                {
                    // ONE ITEM, an array or not: an array expands to its elements under it.
                    int n = dlg.ArrayCount;
                    var r = new WatchRow
                    {
                        Station = mon.Station, Address = dlg.RowAddress, ArrayCount = n, Expanded = n > 1,
                        DisplayFormat = dlg.RowDisplayFormat == FormatDecimal ? "" : dlg.RowDisplayFormat,
                        StringLength = dlg.RowStringLength
                    };
                    ApplyNames(mon, r, dlg.RowLabel.Length == 0 ? (PlcLabelTable.ByAddress(mon.Station, r.Address) ?? new PlcLabelTable.Entry()).Label : dlg.RowLabel,
                               dlg.RowDataType);
                    for (int i = 0; i < n; i++) mon.Hidden.Remove(PlcRowEditorForm.ElementAddress(r.Address, dlg.RowDataType, i, r.StringLength, mon.Station));
                    added.Add(r);
                }
                sh.Rows.InsertRange(at, added);
                EnsureBlankRow(sh);
                if (dlg.RowValueText != null && added.Count == 1 && added[0].IsDevice && !added[0].IsGroup)
                    WriteValue(mon, added[0], dlg.RowValueText);
                SaveWatchList(mon);
                Render(mon, sh);
                Select(sh, new[] { sh.Rows.IndexOf(added[0]) });
            }
        }

        /// <summary>
        /// Copies the selected rows - inside the monitor, and to the clipboard as
        /// Label;DataType;Address lines, so a list can go to Excel and come back.
        /// </summary>
        private void CopySelected()
        {
            var mon = CurrentMonitor;
            var sh = mon != null ? mon.Current : null;
            if (sh == null) return;
            var sel = SelectedIndices(sh);
            if (sel.Count == 0) return;

            _clip.Clear();
            var text = new StringBuilder();
            var chosen = new HashSet<WatchRow>(sel.Select(i => sh.Rows[i]));
            foreach (var i in sel)
            {
                var r = sh.Rows[i];
                // An element of an array copied as well goes with it, not twice.
                if (r.IsElement && (chosen.Contains(r.Head) || chosen.Contains(r.Root))) continue;
                var copy = r.Clone();
                // a structure's member alone: a device of its own type at its place (a bit: "R16.3")
                if (r.IsMember) copy.Address = r.ShownAddress;
                // Keep the names it SHOWS: pasted onto another machine, that
                // machine's label list does not know them.
                if (copy.IsDevice)
                {
                    copy.LabelOverride = r.EffectiveLabel;
                    copy.DataTypeOverride = r.EffectiveDataType;
                }
                _clip.Add(copy);
                text.AppendLine(copy.IsComment ? Csv(copy.Comment) + ";;"
                                               : string.Join(";", Csv(copy.LabelOverride), Csv(copy.DataTypeOverride), Csv(copy.Address)));
            }
            _clipText = text.ToString();
            try { Clipboard.SetText(_clipText); } catch { }
            SetStatus(string.Format(T("Msg_MonCopied", "{0} row(s) copied."), sel.Count));
        }

        private static string _clipText = "";

        private static bool ClipboardHasText()
        {
            try { return Clipboard.ContainsText(); } catch { return false; }
        }

        /// <summary>
        /// Pastes the copied rows above the selected row, or at the end. Rows copied
        /// here come back exactly; text from elsewhere (Excel, a CSV) is read as
        /// Label;DataType;Address lines - a line with text but no address is a comment.
        /// </summary>
        private void Paste(bool atEnd)
        {
            var mon = CurrentMonitor;
            if (mon == null) return;

            List<WatchRow> rows;
            string text = "";
            try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch { }
            if (_clip.Count > 0 && (text.Length == 0 || text == _clipText)) rows = _clip.Select(r => r.Clone()).ToList();
            else rows = ParseLines(text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));
            if (rows.Count == 0) return;

            Remember(T("Undo_Paste", "paste"), mon);
            var sh = EnsureSheet(mon);
            int at = InsertPoint(sh, atEnd);
            foreach (var r in rows)
            {
                r.Station = mon.Station;
                if (r.IsDevice)
                {
                    mon.Hidden.Remove(r.Address);
                    ApplyNames(mon, r, r.LabelOverride, r.DataTypeOverride);
                }
            }
            sh.Rows.InsertRange(at, rows);
            EnsureBlankRow(sh);
            SaveWatchList(mon);
            Render(mon, sh);
            Select(sh, Enumerable.Range(at, rows.Count));
            SetStatus(string.Format(T("Msg_MonPasted", "{0} row(s) pasted."), rows.Count));
        }

        private void RemoveSelected()
        {
            var mon = CurrentMonitor;
            var sh = mon != null ? mon.Current : null;
            if (sh == null) return;
            var sel = SelectedIndices(sh);
            if (sel.Count == 0) return;
            // An element is part of its array: the array is removed as one item.
            var items = sel.Select(i => sh.Rows[i]).Select(r => r.Root).Distinct().ToList();
            Remember(string.Format(T("Undo_Remove", "remove {0} row(s)"), items.Count), mon);

            var gone = new List<string>();
            foreach (var r in items)
            {
                sh.Rows.Remove(r);
                if (r.IsDevice) gone.AddRange(r.IsGroup ? GroupWords(r) : new[] { r.Address });
            }
            EnsureBlankRow(sh);
            EnsureElements(sh);                    // the elements leave with their array - before anything is looked up
            foreach (var a in gone) HideIfUnlisted(mon, a, null, -1);
            SaveWatchList(mon);
            Render(mon, sh);
            UpdateToolbar();
            Select(sh, new[] { Math.Min(sel[0], sh.Rows.Count - 1) });
        }

        /// <summary>
        /// On the simulator, a device removed from every list is kept off them -
        /// the simulator would otherwise add it straight back.
        /// </summary>
        private static void HideIfUnlisted(MonitorTab mon, string address, Sheet skipSheet, int skipRow)
        {
            if (!mon.IsSimulated || string.IsNullOrWhiteSpace(address)) return;
            // skipRow -2: leave the whole of skipSheet out (the tab being deleted).
            bool elsewhere = mon.Sheets.Any(s => s.Rows.Where((r, i) => !(s == skipSheet && (skipRow == -2 || i == skipRow)))
                                                      .Any(r => string.Equals(r.Address, address, StringComparison.OrdinalIgnoreCase)));
            if (!elsewhere) mon.Hidden.Add(address);
        }

        private void MoveSelected(int direction)
        {
            var mon = CurrentMonitor;
            var sh = mon != null ? mon.Current : null;
            if (sh == null) return;
            var sel = SelectedIndices(sh);
            if (sel.Count == 0) return;
            // ITEMS move - an array with its elements - past the item next to them.
            var items = Items(sh);
            var chosen = sel.Select(i => sh.Rows[i]).Select(r => r.IsElement ? r.Head : r).Distinct().ToList();
            var at = chosen.Select(r => items.IndexOf(r)).OrderBy(i => i).ToList();
            int last = items.Count - 1;                          // the empty line stays last
            if (direction < 0 && at[0] == 0) return;
            if (direction > 0 && at[at.Count - 1] >= last - 1) return;
            Remember(T("Undo_Move", "move rows"), mon);

            var order = direction < 0 ? at : Enumerable.Reverse(at).ToList();
            foreach (var i in order)
            {
                var r = items[i];
                items.RemoveAt(i);
                items.Insert(i + direction, r);
            }
            sh.Rows.Clear();
            sh.Rows.AddRange(items);
            EnsureElements(sh);
            SaveWatchList(mon);
            Render(mon, sh);
            Select(sh, chosen.Select(r => sh.Rows.IndexOf(r)));
        }

        // ==================================================================
        // COLUMNS: widths, order, sorting
        // ==================================================================

        private static readonly string[] ColumnNames = { "Label", "DataType", "Format", "Address", "Value", "Changed", "Condition" };
        private static readonly int[] DefaultWidths = { 280, 170, 110, 90, 180, 150, 130 };
        private static Dictionary<string, int> _colWidth, _colOrder;
        /// <summary>The columns the user took off (right-click on a heading); the Condition column shows only on a tab with conditions anyway.</summary>
        private static HashSet<string> _colHidden;

        internal static bool IsColumnHidden(string name) { LoadColumnLayout(); return _colHidden.Contains(name); }
        private static bool _applyingLayout;
        private bool _layoutPending;

        private static string ColumnsPath { get { return Path.Combine(WatchFolder, "Columns.cfg"); } }

        private static void LoadColumnLayout()
        {
            if (_colWidth != null) return;
            _colWidth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _colOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _colHidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(ColumnsPath)) return;
                foreach (var line in File.ReadAllLines(ColumnsPath))
                {
                    // Label=280;0;1   (width;position;shown)
                    int eq = line.IndexOf('=');
                    if (eq <= 0 || line.StartsWith("#")) continue;
                    var f = line.Substring(eq + 1).Split(';');
                    int w, o;
                    if (int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out w)) _colWidth[line.Substring(0, eq)] = w;
                    if (f.Length > 1 && int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out o)) _colOrder[line.Substring(0, eq)] = o;
                    if (f.Length > 2 && f[2].Trim() == "0") _colHidden.Add(line.Substring(0, eq));
                }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the monitor's column layout: " + ex.Message); }
        }

        private static void SaveColumnLayout()
        {
            try
            {
                Directory.CreateDirectory(WatchFolder);
                var lines = new List<string> { "# PLC Monitor columns: <column>=<width>;<position>;<shown 1/0>" };
                for (int i = 0; i < ColumnNames.Length; i++)
                {
                    var n = ColumnNames[i];
                    int w, o;
                    bool hidden = _colHidden.Contains(n);
                    if (!_colWidth.TryGetValue(n, out w))
                    {
                        if (!hidden) continue;
                        w = DefaultWidths[i];
                    }
                    if (!_colOrder.TryGetValue(n, out o)) o = i;
                    lines.Add(n + "=" + w.ToString(CultureInfo.InvariantCulture) + ";" + o.ToString(CultureInfo.InvariantCulture) + ";" + (hidden ? "0" : "1"));
                }
                SafeFile.WriteAllText(ColumnsPath, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(false));
            }
            catch (Exception ex) { AuditLogger.Log("could not save the monitor's column layout: " + ex.Message); }
        }

        /// <summary>The kept widths and order on a grid - every tab looks the same.</summary>
        private static void ApplyColumnLayout(DataGridView grid)
        {
            LoadColumnLayout();
            _applyingLayout = true;
            try
            {
                for (int i = 0; i < ColumnNames.Length; i++)
                {
                    int w;
                    var c = grid.Columns[ColumnNames[i]];
                    c.Width = _colWidth.TryGetValue(ColumnNames[i], out w) ? Math.Max(40, w) : DefaultWidths[i];
                }
                var ordered = ColumnNames.OrderBy(n =>
                {
                    int o;
                    return _colOrder.TryGetValue(n, out o) ? o : Array.IndexOf(ColumnNames, n);
                }).ToList();
                for (int k = 0; k < ordered.Count; k++) grid.Columns[ordered[k]].DisplayIndex = k;
                // The ones taken off (Condition also needs a tab with conditions: see Render).
                foreach (var n in ColumnNames)
                {
                    var c = grid.Columns[n];
                    if (n == "Condition") { if (_colHidden.Contains(n) && c.Visible) c.Visible = false; }
                    else if (c.Visible == _colHidden.Contains(n)) c.Visible = !_colHidden.Contains(n);
                }
            }
            catch { }
            finally { _applyingLayout = false; }
        }

        /// <summary>
        /// The columns' menu on a tab's headings (ColumnChooser): take a column
        /// off or back, type the widths or the table's width, fit them to the
        /// contents, go back to the default columns - kept, and the same on every tab.
        /// </summary>
        private ColumnChooser AttachColumnChooser(MonitorTab mon, Sheet sh)
        {
            var grid = sh.Grid;
            var chooser = ColumnChooser.Attach(grid);
            chooser.DefaultKey = "PlcMonitor";       // one default for every tab, as the columns are one for every tab
            chooser.IsShown = c => !IsColumnHidden(c.Name);
            chooser.SetShown = (c, show) =>
            {
                LoadColumnLayout();
                if (show) _colHidden.Remove(c.Name); else _colHidden.Add(c.Name);
                _applyingLayout = true;
                try
                {
                    if (c.Name == "Condition") c.Visible = show && sh.Rows.Any(r => r.ConditionKey.Length > 0);
                    else c.Visible = show;
                }
                finally { _applyingLayout = false; }
            };
            chooser.Changed = () => { TakeColumnLayout(grid); Render(mon, sh); };
            chooser.Reset = () =>
            {
                LoadColumnLayout();
                _colWidth.Clear(); _colOrder.Clear(); _colHidden.Clear();
                SaveColumnLayout();
                foreach (var m in _monitors)
                    foreach (var other in m.Sheets)
                        if (other.Grid != null) ApplyColumnLayout(other.Grid);
            };
            return chooser;
        }

        /// <summary>A grid's widths and order kept, and put on every other tab.</summary>
        private void TakeColumnLayout(DataGridView source)
        {
            LoadColumnLayout();
            foreach (DataGridViewColumn c in source.Columns) { _colWidth[c.Name] = c.Width; _colOrder[c.Name] = c.DisplayIndex; }
            SaveColumnLayout();
            foreach (var m in _monitors)
                foreach (var other in m.Sheets)
                    if (other.Grid != null && other.Grid != source) ApplyColumnLayout(other.Grid);
        }

        /// <summary>
        /// A column was resized or moved: kept, and done on every other tab too.
        /// Taken once the grid has finished - it renumbers the other columns'
        /// positions one event at a time.
        /// </summary>
        private void ColumnLayoutChanged(DataGridView source)
        {
            if (_applyingLayout || _layoutPending) return;
            MethodInvoker take = () =>
            {
                _layoutPending = false;
                TakeColumnLayout(source);
            };
            if (IsHandleCreated) { _layoutPending = true; BeginInvoke(take); }
            else take();
        }

        /// <summary>
        /// Sorts a tab on a column - a second click on the same heading reverses it.
        ///
        /// SECTION BY SECTION: a comment line heads the rows under it, so each
        /// section is sorted on its own and the comment stays on top of it. Sorting
        /// the whole list at once would pull every device out from under its
        /// heading. The sorted order is the list's order from then on (drag and the
        /// arrows still move rows).
        /// </summary>
        internal void SortSheet(MonitorTab mon, Sheet sh, string column)
        {
            if (sh == null || Array.IndexOf(ColumnNames, column) < 0) return;
            Remember(string.Format(T("Undo_Sort", "sort by {0}"), sh.Grid.Columns[column].HeaderText), mon);
            sh.SortDescending = column == sh.SortColumn && !sh.SortDescending;
            sh.SortColumn = column;

            Comparison<WatchRow> compare = (a, b) => CompareRows(mon, a, b, column);
            var result = new List<WatchRow>();
            var block = new List<WatchRow>();
            Action flush = () =>
            {
                var sorted = block.Select((r, i) => new { r, i }).ToList();
                sorted.Sort((x, y) =>
                {
                    int c = compare(x.r, y.r);
                    if (sh.SortDescending) c = -c;
                    return c != 0 ? c : x.i.CompareTo(y.i);      // stable: equal rows keep their order
                });
                result.AddRange(sorted.Select(x => x.r));
                block.Clear();
            };
            // Arrays as one item: their elements follow them (EnsureElements).
            foreach (var r in sh.Rows.Where(r => !r.IsBlank && !r.IsElement))
            {
                if (r.IsComment) { flush(); result.Add(r); }
                else block.Add(r);
            }
            flush();

            sh.Rows.Clear();
            sh.Rows.AddRange(result);
            EnsureBlankRow(sh);
            foreach (DataGridViewColumn c in sh.Grid.Columns)
                c.HeaderCell.SortGlyphDirection = c.Name != column ? SortOrder.None
                                                : sh.SortDescending ? SortOrder.Descending : SortOrder.Ascending;
            SaveWatchList(mon);
            Render(mon, sh);
            sh.Grid.ClearSelection();
        }

        private static int CompareRows(MonitorTab mon, WatchRow a, WatchRow b, string column)
        {
            switch (column)
            {
                case "Label":
                    return string.Compare(a.EffectiveLabel, b.EffectiveLabel, StringComparison.CurrentCultureIgnoreCase);
                case "DataType":
                {
                    int c = string.Compare(a.EffectiveDataType, b.EffectiveDataType, StringComparison.CurrentCultureIgnoreCase);
                    return c != 0 ? c : CompareAddress(a.Address, b.Address);
                }
                case "Address":
                    return CompareAddress(a.Address, b.Address);
                case "Format":
                {
                    int c = string.Compare(a.EffectiveFormat, b.EffectiveFormat, StringComparison.CurrentCultureIgnoreCase);
                    return c != 0 ? c : CompareAddress(a.Address, b.Address);
                }
                case "Value":
                {
                    long? va = ValueOf(mon, a), vb = ValueOf(mon, b);
                    if (va == null || vb == null) return va == null ? (vb == null ? 0 : 1) : -1;   // unread last
                    return va.Value.CompareTo(vb.Value);
                }
                case "Condition":
                {
                    // met first, then not met, then rows that are no condition
                    Func<WatchRow, int> rank = r =>
                    {
                        var met = ConditionState(mon, r);
                        return met == true ? 0 : met == false ? 1 : string.IsNullOrEmpty(r.ConditionKey) ? 3 : 2;
                    };
                    int c = rank(a).CompareTo(rank(b));
                    return c != 0 ? c : CompareAddress(a.Address, b.Address);
                }
                case "Changed":
                {
                    var ca = LastChangeOf(mon, a); var cb = LastChangeOf(mon, b);
                    DateTime? ta = ca != null ? ca.Changed : null, tb = cb != null ? cb.Changed : null;
                    if (ta == null || tb == null) return ta == null ? (tb == null ? 0 : 1) : -1;   // never changed last
                    return tb.Value.CompareTo(ta.Value);                                        // the latest first
                }
            }
            return 0;
        }

        /// <summary>
        /// Addresses in the PLC's own order: by device, then by number in the
        /// device's own base - M2 before M10, X17 before X20 (octal), W0F before
        /// W10 (hexadecimal) - where plain text order puts M10 before M2.
        /// </summary>
        internal static int CompareAddress(string a, string b)
        {
            string pa, pb; long na, nb;
            SplitAddress(a, out pa, out na);
            SplitAddress(b, out pb, out nb);
            int c = string.Compare(pa, pb, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : na.CompareTo(nb);
        }

        private static void SplitAddress(string address, out string device, out long number)
        {
            string a = (address ?? "").Trim().ToUpperInvariant();
            device = a; number = long.MaxValue;
            // Hexadecimal devices first: their number may start with a letter (WA0).
            foreach (var hex in new[] { "SB", "SW", "B", "W" })
                if (a.Length > hex.Length && a.StartsWith(hex, StringComparison.Ordinal) &&
                    System.Text.RegularExpressions.Regex.IsMatch(a.Substring(hex.Length), "^[0-9A-F]+$"))
                {
                    device = hex;
                    number = Convert.ToInt64(a.Substring(hex.Length), 16);
                    return;
                }
            var m = System.Text.RegularExpressions.Regex.Match(a, "^([A-Z]+)([0-9]+)$");
            if (!m.Success) return;
            device = m.Groups[1].Value;
            int radix = device == "X" || device == "Y" || device == "DX" || device == "DY" ? 8 : 10;   // as PlcAddressHelper
            try { number = Convert.ToInt64(m.Groups[2].Value, radix); }
            catch { number = long.MaxValue; }
        }

        /// <summary>A row's value as shown (a double word as one number), null when not read.</summary>
        private static long? ValueOf(MonitorTab mon, WatchRow r)
        {
            int v;
            if (!r.IsDevice || r.IsGroup || !mon.Values.TryGetValue(r.Address, out v)) return null;
            if (IsFloat(r.EffectiveDataType) || IsString(r.EffectiveDataType)) return null;
            if (!IsDoubleWord(r.EffectiveDataType)) return IsUnsigned(r.EffectiveDataType) ? (long)(ushort)v : (long)(short)v;
            int high;
            mon.Values.TryGetValue(PlcAddressHelper.GetNextAddress(r.Address), out high);
            return CombineDouble(v, high, IsUnsigned(r.EffectiveDataType));
        }

        private void RestoreHidden()
        {
            var mon = CurrentMonitor;
            if (mon == null) return;
            Remember(T("Undo_Restore", "show removed devices again"), mon);
            mon.Hidden.Clear();
            SaveWatchList(mon);
            UpdateToolbar();
            RefreshValues();
        }

        // ---- the engineer's tabs ----------------------------------------------------

        /// <summary>Where a new tab goes: before or after the one on display, or at the end (the "+").</summary>
        internal enum TabPlace { Before, After, End }

        private void NewSheet(TabPlace place, MonitorTab mon = null)
        {
            mon = mon ?? CurrentMonitor;
            if (mon == null) return;
            string name = AskText(T("Ttl_SheetNew", "New tab"), T("Lbl_SheetName", "Name of the tab:"),
                                  UniqueName(mon, T("Tab_MonNew", "Tab") + " " + (mon.Sheets.Count + 1)), mon, null);
            if (name == null) return;
            AddSheetAt(mon, name, place);
        }

        /// <summary>A new tab named so, placed so - undoable.</summary>
        internal Sheet AddSheetAt(MonitorTab mon, string name, TabPlace place)
        {
            Remember(string.Format(T("Undo_NewTab", "new tab '{0}'"), name), mon);
            int selected = mon.SheetTabs.SelectedIndex, count = mon.SheetTabs.TabPages.Count;
            // At the start when there is none on display.
            int at = place == TabPlace.End ? count
                   : place == TabPlace.After ? (selected < 0 ? count : selected + 1)
                   : Math.Max(0, selected);
            return AddSheet(mon, name, at);
        }

        private void RenameSheet()
        {
            var mon = CurrentMonitor;
            var sh = mon != null ? mon.Current : null;
            if (sh == null) return;
            string name = AskText(T("Ttl_SheetRename", "Rename tab"), T("Lbl_SheetName", "Name of the tab:"), sh.Name, mon, sh);
            if (name == null || name == sh.Name) return;
            Remember(string.Format(T("Undo_RenameTab", "rename the tab '{0}'"), sh.Name), mon);
            sh.Name = name;
            sh.Page.Text = name;
            mon.SheetTabs.Invalidate();
            PlaceAddTab(mon);
            SaveWatchList(mon);
        }

        private void DeleteSheet()
        {
            var mon = CurrentMonitor;
            var sh = mon != null ? mon.Current : null;
            if (sh == null) return;
            int n = sh.Rows.Count(r => !r.IsBlank);
            if (n > 0 && MessageBox.Show(DialogOwner,
                    string.Format(T("Msg_SheetDelete", "Delete the tab '{0}' and its {1} row(s)?"), sh.Name, n),
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            RemoveSheet(mon, sh);
        }

        private void MoveSheet(int direction)
        {
            var mon = CurrentMonitor;
            var sh = mon != null ? mon.Current : null;
            if (sh == null) return;
            int to = mon.SheetTabs.TabPages.IndexOf(sh.Page) + direction;
            if (to < 0 || to >= mon.SheetTabs.TabPages.Count) return;
            Remember(string.Format(T("Undo_MoveTab", "move the tab '{0}'"), sh.Name), mon);
            MoveSheetTo(mon, sh, to);
            SaveWatchList(mon);
        }

        private static string UniqueName(MonitorTab mon, string wanted, Sheet except = null)
        {
            string name = wanted;
            for (int k = 2; mon.Sheets.Any(s => s != except && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)); k++)
                name = wanted + " (" + k + ")";
            return name;
        }

        /// <summary>A one-line text question; null when cancelled. Names must be unique per controller.</summary>
        private string AskText(string title, string caption, string initial, MonitorTab mon, Sheet except)
        {
            using (var f = new Form
            {
                Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, BackColor = Color.White,
                Font = UiStyle.Body, ClientSize = new Size(380, 118)
            })
            {
                UiScale.ApplyOnLoad(f);
                var lbl = new Label { Text = caption, AutoSize = true, Location = new Point(16, 14) };
                var box = new TextBox { Text = initial ?? "", Location = new Point(16, 38), Width = 348 };
                var ok = UiStyle.Round(new Button { Text = T("Btn_Ok", "OK"), DialogResult = DialogResult.OK, Width = 96, Height = 30, Location = new Point(164, 76) });
                var cancel = UiStyle.Round(new Button { Text = T("Btn_Cancel", "Cancel"), DialogResult = DialogResult.Cancel, Width = 96, Height = 30, Location = new Point(268, 76) });
                f.Controls.AddRange(new Control[] { lbl, box, ok, cancel });
                f.AcceptButton = ok; f.CancelButton = cancel;
                f.Shown += (s, e) => { box.Focus(); box.SelectAll(); };
                f.FormClosing += (s, e) =>
                {
                    if (f.DialogResult != DialogResult.OK) return;
                    string n = box.Text.Trim();
                    string problem = n.Length == 0 ? T("Msg_SheetNameEmpty", "Type a name.")
                        : mon != null && mon.Sheets.Any(x => x != except && string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase))
                            ? T("Msg_SheetNameTaken", "This controller already has a tab with that name.") : null;
                    if (problem == null) return;
                    MessageBox.Show(f, problem, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Cancel = true;
                };
                return f.ShowDialog(DialogOwner) == DialogResult.OK ? box.Text.Trim() : null;
            }
        }

        // ---- devices from the program ---------------------------------------------

        /// <summary>
        /// Adds devices to a tab: new addresses only - one already on the tab is
        /// not listed twice - each with its data type and a name. A heading goes
        /// in front when given and anything was added. Returns how many were new.
        /// </summary>
        private static int AddDevices(MonitorTab mon, Sheet sh, IEnumerable<DeviceCollector.Device> devices, string heading)
        {
            var have = new HashSet<string>(sh.Rows.Where(r => r.IsDevice).Select(r => r.Address), StringComparer.OrdinalIgnoreCase);
            var rows = new List<WatchRow>();
            foreach (var d in devices)
            {
                if (!have.Add(d.Address)) continue;
                var r = new WatchRow { Station = mon.Station, Address = d.Address };
                var known = PlcLabelTable.ByAddress(mon.Station, d.Address);
                ApplyNames(mon, r, known != null && known.Label.Length > 0 ? known.Label : d.UsedAs, d.DataType);
                mon.Hidden.Remove(d.Address);
                rows.Add(r);
            }
            if (rows.Count == 0) return 0;
            if (!string.IsNullOrEmpty(heading)) rows.Insert(0, new WatchRow { Station = mon.Station, Comment = heading });
            int at = InsertPoint(sh, true);
            sh.Rows.InsertRange(at, rows);
            EnsureBlankRow(sh);
            return rows.Count(r => r.IsDevice);
        }

        private void Report(int added, int offered)
        {
            MessageBox.Show(DialogOwner,
                string.Format(T("Msg_ImportDone", "Added {0} device(s). {1} were already listed."), added, offered - added),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CollectFromScreen()
        {
            var mon = CurrentMonitor;
            if (mon == null) return;
            var shell = Owner as MainForm ?? Application.OpenForms.OfType<MainForm>().FirstOrDefault();
            var page = shell != null ? shell.CurrentPage : null;
            if (page == null) return;

            var devices = DeviceCollector.FromControl(page, mon.Station);
            if (devices.Count == 0)
            {
                MessageBox.Show(DialogOwner, string.Format(T("Msg_MonScreenNone", "The screen on display uses no device of {0}."),
                                PlcEndpointNaming.Caption(mon.Endpoint)), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Remember(T("Undo_Collect", "add the devices"), mon);
            var sh = EnsureSheet(mon);                    // a tab only once there is something to put in it
            int added = AddDevices(mon, sh, devices, null);
            SaveWatchList(mon);
            Render(mon, sh);
            Report(added, devices.Count);
        }

        /// <param name="perPage">One tab per page.</param>
        /// <param name="headings">
        /// Into this tab: a comment line naming each page above its devices - or
        /// ONE list, each device once, which a click on a heading sorts as a whole
        /// (comment lines split a list into sections that are sorted on their own).
        /// </param>
        private void CollectFromProgram(bool perPage, bool headings)
        {
            var mon = CurrentMonitor;
            if (mon == null) return;
            List<DeviceCollector.PageDevices> pages;
            var cursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try { pages = DeviceCollector.FromProgram(mon.Station); }
            finally { Cursor.Current = cursor; }

            if (pages.Count > 0) Remember(T("Undo_Collect", "add the devices"), mon);
            // One tab per page needs no tab to start from.
            var sh = perPage || pages.Count == 0 ? mon.Current : EnsureSheet(mon);

            int added = 0, offered = 0;
            foreach (var p in pages)
            {
                offered += p.Devices.Count;
                if (perPage)
                {
                    // One tab per page, named after it - an existing tab of that
                    // name is added to rather than duplicated.
                    var target = mon.Sheets.FirstOrDefault(s => string.Equals(s.Name, p.Title, StringComparison.OrdinalIgnoreCase));
                    if (target == null)
                    {
                        target = new Sheet { Name = p.Title };
                        AttachSheet(mon, target, mon.SheetTabs.TabPages.Count);
                    }
                    added += AddDevices(mon, target, p.Devices, null);
                }
                else added += AddDevices(mon, sh, p.Devices, headings ? "— " + p.Title + " —" : null);
            }
            SaveWatchList(mon);
            foreach (var s in mon.Sheets) Render(mon, s);
            if (pages.Count == 0)
                MessageBox.Show(DialogOwner, string.Format(T("Msg_MonProgramNone", "No page of the program uses a device of {0}."),
                                PlcEndpointNaming.Caption(mon.Endpoint)), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            else Report(added, offered);
        }

        private void CollectFromLabels()
        {
            var mon = CurrentMonitor;
            if (mon == null) return;
            var devices = DeviceCollector.FromLabelList(mon.Station);
            if (devices.Count == 0)
            {
                MessageBox.Show(DialogOwner, T("Msg_MonLabelsNone", "This controller has no PLC label list (PlcLabels.csv)."),
                                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Remember(T("Undo_Collect", "add the devices"), mon);
            var sh = EnsureSheet(mon);
            int added = AddDevices(mon, sh, devices, null);
            SaveWatchList(mon);
            Render(mon, sh);
            Report(added, devices.Count);
        }

        /// <summary>
        /// THE MACHINE CONDITIONS of this controller into the tab on display: a
        /// heading, then a row per condition - its device, named after the
        /// condition, in its data type - with the Condition column saying whether it
        /// is met. What a control's "Required Machine State" tooltip refers to, seen
        /// working (or not) while the machine runs.
        /// </summary>
        internal void CollectConditions()
        {
            var mon = CurrentMonitor;
            if (mon == null) return;
            var entries = MachineConditionMap.All().Where(e => AlarmManager.StationOf(e.Rule) == mon.Station).ToList();
            if (entries.Count == 0)
            {
                MessageBox.Show(DialogOwner, string.Format(T("Msg_MonConditionsNone", "No machine condition is read from {0}."),
                                PlcEndpointNaming.Caption(mon.Endpoint)), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Remember(T("Undo_Collect", "add the devices"), mon);
            var sh = EnsureSheet(mon);
            var have = new HashSet<string>(sh.Rows.Where(r => !string.IsNullOrEmpty(r.ConditionKey)).Select(r => r.ConditionKey), StringComparer.OrdinalIgnoreCase);
            var rows = new List<WatchRow>();
            foreach (var e in entries)
            {
                if (!have.Add(e.Key)) continue;
                string type = e.Rule.DataType == Kaban.Core.Enums.DataTypeEnum.Bin_32 ? DeviceCollector.DoubleWord
                            : DeviceCollector.IsBitDevice(e.Rule.Address) ? DeviceCollector.Bit : DeviceCollector.Word;
                rows.Add(new WatchRow
                {
                    Station = mon.Station, Address = e.Rule.Address.Trim().ToUpperInvariant(),
                    LabelOverride = e.Name, DataTypeOverride = type, ConditionKey = e.Key
                });
                mon.Hidden.Remove(e.Rule.Address);
            }
            if (rows.Count > 0)
            {
                rows.Insert(0, new WatchRow { Station = mon.Station, Comment = T("Hdr_MonConditions", "Machine conditions") });
                int at = InsertPoint(sh, true);
                sh.Rows.InsertRange(at, rows);
                EnsureBlankRow(sh);
                SaveWatchList(mon);
                Render(mon, sh);
            }
            Report(rows.Count(r => r.IsDevice), entries.Count);
        }

        // ==================================================================
        // WRITE
        // ==================================================================

        /// <summary>
        /// Writes one device, and reports WHY when it cannot - a silently ignored
        /// write on a machine is the kind of thing that gets diagnosed as a broken PLC.
        /// </summary>
        private void WriteValue(MonitorTab mon, WatchRow row, string text, string how = "typed")
        {
            if (row == null || !row.IsDevice || row.IsGroup) return;

            // In the row's type and display format: a float, a string, a word in
            // hexadecimal or binary - checked for its range before anything goes.
            string problem;
            var words = ParseValue(row, text, out problem);
            if (words == null)
            {
                SetStatus(problem == "Msg_MonOutOfRange" ? T("Msg_MonOutOfRange", "Out of range for this data type - nothing written.")
                        : problem == "Msg_MonTooLong" ? T("Msg_MonTooLong", "Longer than the string - nothing written.")
                        : T("Msg_MonNotNumber", "Not a number in this format - nothing written."));
                return;
            }

            var driver = mon.WriteDriver;
            if (driver == null)
            {
                MessageBox.Show(DialogOwner, PlcManager.Instance.DescribeAccess(mon.Station), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // A STRUCTURE'S BIT MEMBER (R16.3) is a bit of a word: the word as last read, that bit changed.
            if (row.BitIndex >= 0)
            {
                int word;
                if (!mon.Values.TryGetValue(row.Address, out word))
                {
                    SetStatus(T("Msg_MonBitNotRead", "Its word is not read yet - nothing written."));
                    return;
                }
                words = new[] { words[0] != 0 ? word | (1 << row.BitIndex) : word & ~(1 << row.BitIndex) };
            }

            // Every word of the value, low first - the low word alone turns a
            // 32-bit value (or a float, or a string) into nonsense.
            var addresses = WordsOf(row).ToList();
            using (Kaban.UI.Helpers.WriteSource.For("PLC Monitor: " + how + " " + (text ?? "").Trim() + " into " + row.ShownAddress +
                                                    (string.IsNullOrWhiteSpace(row.EffectiveLabel) ? "" : " [" + row.EffectiveLabel.Trim() + "]")))
                for (int i = 0; i < words.Length && i < addresses.Count; i++)
                {
                    driver.Write(addresses[i], words[i]);
                    mon.Values[addresses[i]] = words[i];
                }

            if (!mon.IsSimulated)
                AuditLogger.Log("monitor wrote " + (text ?? "").Trim() + " to " + row.Address + " on " + PlcEndpointNaming.CaptionWithStation(mon.Endpoint) + ".");
        }

        /// <summary>
        /// RESET (19 October: "a Reset in the labels' right-click menu - we need to be able to reset
        /// all the values of a structured label and of an array easily"): 0 into every device of
        /// the rows chosen - an array's every element; a structure's whole length, the words no
        /// label uses too ("they should be reset as well"); a Bit label chosen on its own only its
        /// bit, the rest of its word kept - after asking.
        /// </summary>
        internal void ResetSelected(MonitorTab mon, Sheet sh, bool ask = true)
        {
            if (mon == null || sh == null) return;
            var chosen = SelectedIndices(sh).Where(i => i >= 0 && i < sh.Rows.Count).Select(i => sh.Rows[i]).Where(r => r.IsDevice).ToList();
            // a row under another one chosen is reset with it
            chosen = chosen.Where(r => !chosen.Any(o => !ReferenceEquals(o, r) && IsUnder(r, o))).ToList();
            if (chosen.Count == 0) return;

            int notRead;
            var words = ResetWords(mon, chosen, out notRead);
            if (words.Count == 0)
            {
                SetStatus(T("Msg_MonBitNotRead", "Its word is not read yet - nothing written."));
                return;
            }
            string labels = string.Join(", ", chosen.Select(r => string.IsNullOrWhiteSpace(r.EffectiveLabel) ? r.ShownAddress : r.EffectiveLabel.Trim()));
            if (ask)
            {
                string range = words.Count == 1 ? words[0].Key : words[0].Key + " … " + words[words.Count - 1].Key;
                string question = string.Format(T("Msg_MonReset", "Write 0 into the {0} device(s) of {1} ({2})?"), words.Count, labels, range);
                if (notRead > 0)
                    question += "\r\n\r\n" + string.Format(T("Msg_MonResetNotRead", "{0} bit(s) stay as they are: their word is not read yet."), notRead);
                if (!mon.IsSimulated)
                    question += "\r\n\r\n" + T("Msg_MonResetLive", "This is the machine's PLC: it uses the new values at once.");
                if (MessageBox.Show(DialogOwner, question, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            }

            var driver = mon.WriteDriver;
            if (driver == null)
            {
                MessageBox.Show(DialogOwner, PlcManager.Instance.DescribeAccess(mon.Station), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (Kaban.UI.Helpers.WriteSource.For("PLC Monitor: reset (0) " + labels))
                foreach (var w in words)
                {
                    driver.Write(w.Key, w.Value);
                    mon.Values[w.Key] = w.Value;
                }
            if (!mon.IsSimulated)
                AuditLogger.Log("monitor reset " + words.Count + " device(s) of " + labels + " to 0 on " + PlcEndpointNaming.CaptionWithStation(mon.Endpoint) + ".");
            SetStatus(string.Format(T("Msg_MonResetDone", "{0} device(s) set to 0."), words.Count));
            Render(mon, sh);
        }

        /// <summary>A row inside another: an element of its array, a member of its structure (at any depth).</summary>
        private static bool IsUnder(WatchRow r, WatchRow above)
        {
            for (var h = r.Head; h != null; h = h.Head) if (ReferenceEquals(h, above)) return true;
            return false;
        }

        /// <summary>
        /// What a reset takes under a row: an array's elements; a structure itself (its whole
        /// length - see ResetWords); otherwise the row (an element, a label of a structure).
        /// </summary>
        private static IEnumerable<WatchRow> ValueRows(WatchRow r)
        {
            if (r.IsArray && r.Elements != null) return r.Elements.SelectMany(ValueRows);
            return r.IsDevice ? new[] { r } : new WatchRow[0];
        }

        /// <summary>
        /// The words a reset writes, in order: 0 for every word of the rows' values - a structure's
        /// every word of its length, the words and bits no label uses too; a Bit label of its own:
        /// its word as last read with its bit cleared (not read yet: left, and counted).
        /// </summary>
        internal static List<KeyValuePair<string, int>> ResetWords(MonitorTab mon, IEnumerable<WatchRow> rows, out int notRead)
        {
            notRead = 0;
            var value = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            var bits = new List<WatchRow>();
            foreach (var leaf in rows.SelectMany(ValueRows))
            {
                if (leaf.BitIndex >= 0) { bits.Add(leaf); continue; }
                foreach (var a in leaf.Structure != null ? StructureWords(leaf) : WordsOf(leaf))
                    if (!value.ContainsKey(a)) { value[a] = 0; order.Add(a); }
            }
            foreach (var b in bits)
            {
                int now;
                if (value.TryGetValue(b.Address, out now)) { value[b.Address] = now & ~(1 << b.BitIndex); continue; }
                if (mon == null || !mon.Values.TryGetValue(b.Address, out now)) { notRead++; continue; }
                value[b.Address] = now & ~(1 << b.BitIndex);
                order.Add(b.Address);
            }
            return order.Select(a => new KeyValuePair<string, int>(a, value[a])).ToList();
        }

        /// <summary>
        /// Shift+Enter on a row: a Bit is switched over (0 to 1, 1 to 0 - a bit not
        /// read yet is taken as 0); any other device opens its Value cell for typing,
        /// the value selected so the new one replaces it. An array's own row has no
        /// single value: nothing happens.
        /// </summary>
        internal void QuickValue(MonitorTab mon, Sheet sh, int rowIndex)
        {
            if (mon == null || sh == null || rowIndex < 0 || rowIndex >= sh.Rows.Count) return;
            var row = sh.Rows[rowIndex];
            if (!row.IsDevice || row.IsGroup) return;
            if (IsBit(row.EffectiveDataType))
            {
                int now;
                mon.Values.TryGetValue(row.Address, out now);
                if (row.BitIndex >= 0) now = (now >> row.BitIndex) & 1;      // a structure's bit of its word
                WriteValue(mon, row, now != 0 ? "0" : "1", "switched (Shift+Enter) to");
                Render(mon, sh);
                return;
            }
            sh.Grid.CurrentCell = sh.Grid.Rows[rowIndex].Cells["Value"];
            sh.Grid.BeginEdit(true);
        }

        /// <summary>Every device a structure's row takes: its length from its address.</summary>
        private static IEnumerable<string> StructureWords(WatchRow r)
        {
            var st = r.Structure;
            string a = (r.Address ?? "").Trim().ToUpperInvariant();
            for (int i = 0; st != null && i < st.Size; i++) yield return PlcAddressHelper.OffsetAddress(a, i);
        }

        /// <summary>Where a row's fold mark ends in its label cell (pixels): after its indentation.</summary>
        private static int FoldMarkRight(WatchRow r) { return 24 + (r == null ? 0 : r.Depth * 22); }

        /// <summary>Every device under an array or a structure: its elements', its members' (a member's word).</summary>
        private static IEnumerable<string> GroupWords(WatchRow r)
        {
            foreach (var k in (r.Elements ?? new List<WatchRow>()).Concat(r.Members ?? new List<WatchRow>()))
            {
                if (k.IsGroup) { foreach (var a in GroupWords(k)) yield return a; }
                else yield return k.Address;
            }
        }

        /// <summary>An array folded or unfolded - kept with the list.</summary>
        private void ToggleArray(MonitorTab mon, Sheet sh, WatchRow head)
        {
            if (head == null || !head.IsGroup) return;
            head.Expanded = !head.Expanded;
            SaveWatchList(mon);
            Render(mon, sh);
            int at = sh.Rows.IndexOf(head);
            if (at >= 0) Select(sh, new[] { at });
        }

        /// <summary>The selected rows (an element: its array) shown in a display format.</summary>
        private void SetDisplayFormat(MonitorTab mon, Sheet sh, string format)
        {
            var items = SelectedIndices(sh).Select(i => sh.Rows[i]).Select(r => r.Root)
                                           .Where(r => r.IsDevice).Distinct().ToList();
            if (items.Count == 0) return;
            Remember(T("Undo_Format", "display format"), mon);
            foreach (var r in items) r.DisplayFormat = format == FormatDecimal ? "" : format;
            SaveWatchList(mon);
            Render(mon, sh);
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

        /// <summary>
        /// The tab on DISPLAY only: reading and redrawing every list of every
        /// controller four times a second was work nobody could see.
        /// </summary>
        private void RefreshValues()
        {
            // A side page out of sight reads nothing (the panel closed, not the page).
            if (Embedded && !Visible) return;
            var mon = CurrentMonitor;
            if (mon == null) return;
            var sh = mon.Current;
            if (sh == null) { UpdateStatus(mon, null); return; }
            // An edit that ended without CellEndEdit (cancelled from code) must not
            // stop the refresh for good.
            if (sh.Editing && !sh.Grid.IsCurrentCellInEditMode) sh.Editing = false;
            if (sh.Editing) return;

            if (mon.IsSimulated) ReadSimulated(mon);
            else BeginReadReal(mon, sh);
            Render(mon, sh);
            UpdateStatus(mon, sh);
            RefreshFind(mon);
        }

        /// <summary>Find's values: read with the tab's, or on their own when Find is about another controller.</summary>
        private void RefreshFind(MonitorTab current)
        {
            if (!FindShowing) return;
            var fmon = _monitors.FirstOrDefault(m => m.Station == _find.Station);
            if (fmon == null) return;
            if (fmon != current)
            {
                if (fmon.IsSimulated) ReadSimulated(fmon);
                else BeginReadReal(fmon, null);
            }
            _find.RefreshLive();
        }

        /// <summary>
        /// The simulator's whole image, straight out of memory. Addresses it holds
        /// that no list has are APPENDED to the first tab - which is how a device a
        /// screen wrote to shows up without anyone adding it by hand.
        /// </summary>
        private void ReadSimulated(MonitorTab mon)
        {
            // The simulator's own class (the registered driver is the logging one around it).
            var driver = PlcManager.Unwrap(mon.ReadDriver);
            if (driver == null) return;
            var dumpMethod = driver.GetType().GetMethod("Dump");
            if (dumpMethod == null) return;
            var dump = dumpMethod.Invoke(driver, null) as System.Collections.IDictionary;
            if (dump == null) return;

            mon.Values.Clear();
            foreach (System.Collections.DictionaryEntry kv in dump)
                mon.Values[Convert.ToString(kv.Key)] = Convert.ToInt32(kv.Value);
            // The Modified column. A device the simulator has never held reads 0
            // there: seen as 0, so the first write to it is a change.
            var now = DateTime.Now;
            var seenNames = mon.Values.Keys.ToList();
            var seenValues = mon.Values.Values.ToList();
            foreach (var w in mon.Sheets.SelectMany(s => s.Rows).Where(r => r.IsDevice && !r.IsGroup).SelectMany(WordsOf).Concat(FindWords(mon)))
                if (!mon.Values.ContainsKey(w) && !seenNames.Contains(w)) { seenNames.Add(w); seenValues.Add(0); }
            DeviceHistory.Observe(mon.Station, seenNames, seenValues, now, now);

            // An array's elements are listed, even one added a moment ago.
            foreach (var s in mon.Sheets) EnsureElements(s);

            // A double word's second device is part of its row, not a device of
            // its own: D101 under a D100 double word is D100's high half.
            // A structure's devices are its own too, the unused ones between its members included.
            var highWords = new HashSet<string>(
                mon.Sheets.SelectMany(s => s.Rows).Where(r => r.IsDevice).SelectMany(r => WordsOf(r).Skip(1))
                   .Concat(mon.Sheets.SelectMany(s => s.Rows).Where(r => r.Structure != null).SelectMany(StructureWords)),
                StringComparer.OrdinalIgnoreCase);
            var discovered = mon.Values.Keys.Where(a => !mon.Hidden.Contains(a) && !mon.Listed(a) && !highWords.Contains(a))
                                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
            if (discovered.Count == 0 || mon.Sheets.Count == 0) return;
            var first = mon.Sheets[0];
            int at = InsertPoint(first, true);
            foreach (var a in discovered) first.Rows.Insert(at++, new WatchRow { Station = mon.Station, Address = a });
            EnsureBlankRow(first);
        }

        /// <summary>
        /// A REAL station has no memory dump, so the tab's devices are read as one
        /// batch - on a WORKER THREAD: a PLC round trip on the 250ms UI timer would
        /// stall the whole HMI. A double word's high word is read with it.
        /// </summary>
        private void BeginReadReal(MonitorTab mon, Sheet sh)
        {
            if (mon.ReadInFlight) return;
            var driver = mon.ReadDriver;
            if (driver == null) return;

            var addresses = new List<string>();
            if (sh != null)
                foreach (var r in sh.Rows.Where(r => r.IsDevice && !r.IsGroup))
                    addresses.AddRange(WordsOf(r));      // a double word, a float or a string: all its words
            addresses.AddRange(FindWords(mon));          // and what Find lists
            var batch = addresses.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (batch.Length == 0) return;

            mon.ReadInFlight = true;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                int[] values = null;
                bool ok = false;
                var started = DateTime.Now;
                try { ok = driver.TryReadMulti(batch, out values); }
                catch (Exception ex) { AuditLogger.Log("monitor read failed: " + ex.Message); }
                // The Modified column: when each device changed, and whether the HMI did it.
                if (ok && values != null && values.Length == batch.Length)
                    DeviceHistory.Observe(mon.Station, batch, values, started, DateTime.Now);

                try
                {
                    if (IsDisposed || !IsHandleCreated) { mon.ReadInFlight = false; return; }
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (ok && values != null && values.Length == batch.Length)
                            for (int i = 0; i < batch.Length; i++) mon.Values[batch[i]] = values[i];
                        mon.ReadInFlight = false;
                    });
                }
                catch (ObjectDisposedException) { mon.ReadInFlight = false; }   // form closed mid-read
                catch (InvalidOperationException) { mon.ReadInFlight = false; } // handle gone mid-read
            });
        }

        private static readonly Color ColLabelKnown = Color.FromArgb(35, 40, 48);
        private static readonly Color ColLabelUnknown = Color.FromArgb(150, 150, 150);
        private static readonly Color ColValueSet = Color.FromArgb(0, 120, 40);
        private static readonly Color ColValueClear = Color.FromArgb(90, 90, 90);
        private static readonly Color ColCommentBack = Color.FromArgb(242, 245, 249);
        private static readonly Color ColCommentText = Color.FromArgb(60, 80, 110);

        private static void Render(MonitorTab mon, Sheet sh)
        {
            foreach (var r in sh.Rows) r.Station = mon.Station;   // before any label is looked up
            EnsureElements(sh);                                   // the arrays' elements, each under its array
            var grid = sh.Grid;
            bool firstFill = grid.Rows.Count == 0;

            while (grid.Rows.Count > sh.Rows.Count) grid.Rows.RemoveAt(grid.Rows.Count - 1);
            while (grid.Rows.Count < sh.Rows.Count) grid.Rows.Add();

            // Nothing selected when a list first appears: a highlighted first row
            // reads as "this one is special", and it is the row a stray Delete
            // would remove.
            if (firstFill && !grid.IsCurrentCellInEditMode)
            {
                grid.ClearSelection();
                try { grid.CurrentCell = null; } catch { }
            }

            for (int i = 0; i < sh.Rows.Count; i++)
            {
                var model = sh.Rows[i];
                var row = grid.Rows[i];

                // An element shows while its array is unfolded.
                bool visible = IsShown(model);
                if (row.Visible != visible)
                {
                    try { row.Visible = visible; }
                    catch (InvalidOperationException) { grid.CurrentCell = null; row.Visible = visible; }
                }
                if (!visible) continue;

                if (model.IsComment)
                {
                    // A heading: its text across the label cell, the row tinted.
                    Set(row.Cells["Label"], model.Comment);
                    Set(row.Cells["DataType"], ""); Set(row.Cells["Format"], ""); Set(row.Cells["Address"], ""); Set(row.Cells["Value"], "");
                    Set(row.Cells["Changed"], ""); Tip(row.Cells["Changed"], "");
                    Set(row.Cells["Condition"], ""); Tip(row.Cells["Condition"], "");
                    Style(row, ColCommentBack, ColCommentText, UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold | FontStyle.Italic));
                    continue;
                }
                Style(row, Color.Empty, Color.Empty, null);

                Set(row.Cells["Address"], model.ShownAddress);
                if (model.IsBlank)
                {
                    Set(row.Cells["Label"], ""); Set(row.Cells["DataType"], ""); Set(row.Cells["Format"], ""); Set(row.Cells["Value"], "");
                    Set(row.Cells["Changed"], ""); Tip(row.Cells["Changed"], "");
                    Set(row.Cells["Condition"], ""); Tip(row.Cells["Condition"], "");
                    continue;
                }

                // An address the label list does not know reads NULL, which is itself
                // useful: a screen is bound to something outside that list.
                string label = model.EffectiveLabel;
                if (label.Length == 0) label = "NULL";
                // AN ARRAY: the fold mark and its count; its elements indented under it.
                // A STRUCTURE the same: the fold mark, its type's name; its members indented under it.
                string indent = new string(' ', 6 * model.Depth);
                if (model.IsArray) label = (model.Expanded ? "\u25BE " : "\u25B8 ") + label + "   [" + model.EffectiveCount + "]";
                else if (model.Structure != null) label = indent + (model.Expanded ? "\u25BE " : "\u25B8 ") + label;
                else if (model.IsElement) label = indent + label;
                if (!(sh.Editing && grid.CurrentCell != null && grid.CurrentCell.RowIndex == i)) Set(row.Cells["Label"], label);
                var wantLabelColour = model.IsUnknownToLabelTable ? ColLabelUnknown : ColLabelKnown;
                if (row.Cells["Label"].Style.ForeColor != wantLabelColour) row.Cells["Label"].Style.ForeColor = wantLabelColour;

                string type = model.EffectiveDataType;
                // an element's type is its array's; a member's is its own
                Set(row.Cells["DataType"], model.IsElement && !model.IsMember ? "" : type + (IsString(type) && !type.Contains("(") && !model.IsMember ? "(" + model.StringLength + ")" : ""));
                bool whole = !IsBit(type) && !IsFloat(type) && !IsString(type);
                Set(row.Cells["Format"], whole && !model.IsElement ? T("Fmt_" + model.EffectiveFormat, model.EffectiveFormat) : "");

                // One value in the row's type and format: a double word or a float
                // over two devices, a string over its words, an array its elements.
                bool nonZero;
                string shownText = FormatValue(mon, model, out nonZero);
                if (shownText != null)
                {
                    Set(row.Cells["Value"], shownText);
                    var want = nonZero ? ColValueSet : ColValueClear;
                    if (row.Cells["Value"].Style.ForeColor != want) row.Cells["Value"].Style.ForeColor = want;
                }
                else
                {
                    // not read yet, or the read failed - an honest gap, not a zero
                    Set(row.Cells["Value"], "-");
                    if (row.Cells["Value"].Style.ForeColor != ColLabelUnknown) row.Cells["Value"].Style.ForeColor = ColLabelUnknown;
                }

                RenderLastChange(mon, model, row.Cells["Changed"]);
                RenderCondition(mon, model, row.Cells["Condition"]);
            }

            // The Condition column is there on a tab that has a machine condition.
            bool conditions = sh.Rows.Any(r => !string.IsNullOrEmpty(r.ConditionKey));
            conditions &= !IsColumnHidden("Condition");
            if (grid.Columns["Condition"].Visible != conditions) grid.Columns["Condition"].Visible = conditions;
        }

        private static readonly Color ColConditionMet = Color.FromArgb(0, 130, 50);
        private static readonly Color ColConditionNotMet = Color.FromArgb(200, 40, 40);

        /// <summary>A condition row's state now: true met, false not met, null not read (or no condition).</summary>
        internal static bool? ConditionState(MonitorTab mon, WatchRow model)
        {
            if (model == null || string.IsNullOrEmpty(model.ConditionKey)) return null;
            var entry = MachineConditionMap.Find(model.ConditionKey);
            if (entry == null) return null;
            double value;
            return MachineConditionMap.Evaluate(entry.Rule, a => { int v; return mon.Values.TryGetValue(a, out v) ? v : (int?)null; }, out value);
        }

        /// <summary>"\u2714 met" / "\u2716 not met" - the tooltip says what the condition asks and what is read.</summary>
        private static void RenderCondition(MonitorTab mon, WatchRow model, DataGridViewCell cell)
        {
            if (string.IsNullOrEmpty(model.ConditionKey)) { Set(cell, ""); Tip(cell, ""); return; }
            var entry = MachineConditionMap.Find(model.ConditionKey);
            if (entry == null)
            {
                Set(cell, "?");
                Tip(cell, T("Cond_NotRegistered", "This condition is not registered by the machines of this program."));
                if (cell.Style.ForeColor != ColLabelUnknown) cell.Style.ForeColor = ColLabelUnknown;
                return;
            }
            double value;
            var met = MachineConditionMap.Evaluate(entry.Rule, a => { int v; return mon.Values.TryGetValue(a, out v) ? v : (int?)null; }, out value);
            string text = met == true ? "\u2714 " + T("Cond_Met", "met") : met == false ? "\u2716 " + T("Cond_NotMet", "not met") : "-";
            Set(cell, text);
            var colour = met == true ? ColConditionMet : met == false ? ColConditionNotMet : ColLabelUnknown;
            if (cell.Style.ForeColor != colour) cell.Style.ForeColor = colour;
            Tip(cell, entry.Name + "\n" + T("Cond_Asks", "Met when") + ": " + MachineConditionMap.RuleText(entry.Rule) +
                      (met.HasValue ? "\n" + T("Cond_Now", "Now") + ": " + value.ToString(CultureInfo.CurrentCulture) : ""));
        }

        private static readonly Color ColChangedByHmi = Color.FromArgb(20, 90, 190);
        private static readonly Color ColChangedByPlc = Color.FromArgb(35, 40, 48);

        /// <summary>
        /// The latest change of a row's devices (a double word is two, a string
        /// several): null when none was seen since the monitor or a page started
        /// reading it. An array's row has none - its elements do.
        /// </summary>
        internal static DeviceHistory.Info LastChangeOf(MonitorTab mon, WatchRow model)
        {
            if (model == null || !model.IsDevice || model.IsGroup) return null;
            DeviceHistory.Info latest = null;
            foreach (var w in WordsOf(model))
            {
                var i = DeviceHistory.Get(mon.Station, w);
                if (i == null || i.Changed == null) continue;
                if (latest == null || i.Changed.Value > latest.Changed.Value) latest = i;
            }
            return latest;
        }

        /// <summary>"14:32:05.1  HMI" - the tooltip says from what, to what, and what wrote it.</summary>
        private static void RenderLastChange(MonitorTab mon, WatchRow model, DataGridViewCell cell)
        {
            var c = LastChangeOf(mon, model);
            if (c == null)
            {
                Set(cell, model.IsDevice && !model.IsGroup ? "-" : "");
                if (cell.Style.ForeColor != ColLabelUnknown) cell.Style.ForeColor = ColLabelUnknown;
                Tip(cell, model.IsDevice && !model.IsGroup
                    ? T("Tip_MonNoChange", "No change seen since the HMI started reading this device. Changes before that are not known.")
                    : "");
                return;
            }
            var t = c.Changed.Value;
            string when = (t.Date == DateTime.Today ? "" : Kaban.Core.Helpers.ViewPreferences.ShortDateText(t) + " ") +
                          Kaban.Core.Helpers.ViewPreferences.TimeText(t, true, 1);
            string who = c.ByHmi ? T("Mon_ByHmi", "HMI") : T("Mon_ByPlc", "PLC");
            Set(cell, when + "  " + who);
            var want = c.ByHmi ? ColChangedByHmi : ColChangedByPlc;
            if (cell.Style.ForeColor != want) cell.Style.ForeColor = want;

            string tip = string.Format(T("Tip_MonChanged", "Changed from {0} to {1} at {2}."),
                c.From.HasValue ? c.From.Value.ToString(CultureInfo.InvariantCulture) : "?",
                c.Value.HasValue ? c.Value.Value.ToString(CultureInfo.InvariantCulture) : "?",
                Kaban.Core.Helpers.ViewPreferences.DateTimeText(t, true, 3));
            if (c.ByHmi)
                tip += "\r\n" + string.Format(T("Tip_MonChangedByHmi", "Written by the HMI at {0}, user {1}: {2}"),
                    c.WriteTime.HasValue ? Kaban.Core.Helpers.ViewPreferences.TimeText(c.WriteTime.Value, true, 3) : "?",
                    c.WriteUser, c.WriteSource);
            else
                tip += "\r\n" + T("Tip_MonChangedByPlc", "The HMI did not write this value just before: the PLC program (or the machine) changed it.");
            if (c.LastWrite.HasValue && !(c.ByHmi && c.WriteTime == c.LastWrite))
                tip += "\r\n" + string.Format(T("Tip_MonLastWrite", "The HMI last wrote it at {0} ({1})."),
                    Kaban.Core.Helpers.ViewPreferences.DateTimeText(c.LastWrite.Value),
                    c.LastWriteValue.HasValue ? c.LastWriteValue.Value.ToString(CultureInfo.InvariantCulture) : T("Mon_PartWrite", "a bit of it"));
            tip += "\r\n" + T("Tip_MonWriteLog", "Every HMI write is in the PLC write log: HMI Settings > Log Settings, kind PLC device writes.");
            Tip(cell, tip);
        }

        private static void Tip(DataGridViewCell cell, string text)
        {
            if (cell.ToolTipText != text) cell.ToolTipText = text;
        }

        private static void Style(DataGridViewRow row, Color back, Color fore, Font font)
        {
            var s = row.DefaultCellStyle;
            if (s.BackColor != back) s.BackColor = back;
            if (s.ForeColor != fore) s.ForeColor = fore;
            if (s.Font != font) s.Font = font;
        }

        /// <summary>Assigns only on change - writing every cell every 250ms flickers.</summary>
        private static void Set(DataGridViewCell cell, string text)
        {
            if (Convert.ToString(cell.Value) != text) cell.Value = text;
        }

        private string _statusNote = "";
        private DateTime _statusNoteUntil;

        private void SetStatus(string note)
        {
            _statusNote = note ?? "";
            _statusNoteUntil = DateTime.UtcNow.AddSeconds(4);
            var mon = CurrentMonitor;
            if (mon != null && mon.Current != null) UpdateStatus(mon, mon.Current);
        }

        private void UpdateStatus(MonitorTab mon, Sheet sh)
        {
            string where = PlcEndpointNaming.CaptionWithStation(mon.Endpoint) +
                           (mon.IsSimulated ? "  \u00B7  " + T("Lbl_Simulated", "simulated") : "");
            string text = sh == null
                ? T("Msg_MonNoTabsShort", "No tabs") + "  \u00B7  " + where
                : string.Format(T("Msg_MonStatus", "{0} device(s), {1} comment line(s)  \u00B7  {2}"),
                                sh.Rows.Count(r => r.IsDevice && !r.IsElement), sh.Rows.Count(r => r.IsComment), where);
            if (_statusNote.Length > 0 && DateTime.UtcNow < _statusNoteUntil) text = _statusNote + "     " + text;
            if (_status.Text != text) _status.Text = text;
        }

        // ==================================================================
        // EXPORT / IMPORT
        // ==================================================================

        private const string CsvHeader = "Label;DataType;Address;Value";

        private void ExportSheet()
        {
            var mon = CurrentMonitor;
            var sh = mon != null ? mon.Current : null;
            if (sh == null) return;
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = T("Ttl_ExportCsv", "Export this tab's list");
                dlg.Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*";
                dlg.FileName = SafeFileName(PlcEndpointNaming.Caption(mon.Endpoint) + " - " + sh.Name) + ".csv";
                if (dlg.ShowDialog(DialogOwner) != DialogResult.OK) return;
                try
                {
                    var lines = new List<string>
                    {
                        "# " + PlcEndpointNaming.CaptionWithStation(mon.Endpoint) + " / " + sh.Name +
                        " - exported " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        CsvHeader
                    };
                    foreach (var r in sh.Rows.Where(r => !r.IsBlank && !r.IsGroup))
                    {
                        if (r.IsComment) { lines.Add(Csv(r.Comment) + ";;;"); continue; }
                        int v;
                        string value = mon.Values.TryGetValue(r.Address, out v) ? v.ToString(CultureInfo.InvariantCulture) : "";
                        lines.Add(string.Join(";", Csv(r.EffectiveLabel), Csv(r.EffectiveDataType), Csv(r.Address), Csv(value)));
                    }
                    // UTF-8 WITH BOM, matching PlcLabels.csv: Excel misreads a BOM-less file's non-ASCII labels.
                    File.WriteAllLines(dlg.FileName, lines, new UTF8Encoding(true));
                    AuditLogger.Log("exported " + sh.Rows.Count(r => r.IsDevice) + " monitor rows for " + PlcEndpointNaming.Caption(mon.Endpoint) + ".");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(DialogOwner, T("Msg_ExportFailed", "Could not write the file:") + "\r\n" + ex.Message,
                                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportSheet()
        {
            var mon = CurrentMonitor;
            if (mon == null) return;
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = T("Ttl_ImportCsv", "Add devices from a CSV");
                dlg.Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*";
                if (dlg.ShowDialog(DialogOwner) != DialogResult.OK) return;

                List<WatchRow> parsed;
                Dictionary<string, int> values;
                try { parsed = ParseCsv(dlg.FileName, out values); }
                catch (Exception ex)
                {
                    MessageBox.Show(DialogOwner, T("Msg_ImportFailed", "Could not read the file:") + "\r\n" + ex.Message,
                                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (parsed.Count(r => r.IsDevice) == 0)
                {
                    MessageBox.Show(DialogOwner, T("Msg_ImportNothing", "No usable rows found. Expected Label / DataType / Address and optionally a Value column."),
                                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Remember(T("Undo_Import", "import"), mon);
                var sh = EnsureSheet(mon);
                var existing = new HashSet<string>(sh.Rows.Where(r => r.IsDevice).Select(r => r.Address), StringComparer.OrdinalIgnoreCase);
                int at = InsertPoint(sh, true), added = 0, offered = 0;
                foreach (var r in parsed)
                {
                    r.Station = mon.Station;
                    if (r.IsDevice)
                    {
                        offered++;
                        if (!existing.Add(r.Address)) continue;
                        mon.Hidden.Remove(r.Address);
                        added++;
                    }
                    sh.Rows.Insert(at++, r);
                }
                EnsureBlankRow(sh);

                // Importing must never command the machine as a SIDE EFFECT of "add
                // these to my list": the file's values are written only when asked.
                int applied = 0;
                if (values.Count > 0 && MessageBox.Show(DialogOwner,
                        string.Format(T("Msg_ImportApplyValues", "The file also carries {0} values. Write them to this controller as well?"), values.Count),
                        Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    var driver = mon.WriteDriver;
                    if (driver == null)
                        MessageBox.Show(DialogOwner, PlcManager.Instance.DescribeAccess(mon.Station), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else
                    {
                        using (Kaban.UI.Helpers.WriteSource.For("PLC Monitor: values imported from " + Path.GetFileName(dlg.FileName)))
                            foreach (var kv in values) { driver.Write(kv.Key, kv.Value); mon.Values[kv.Key] = kv.Value; applied++; }
                        if (!mon.IsSimulated)
                            AuditLogger.Log("monitor imported and WROTE " + applied + " values to " + PlcEndpointNaming.CaptionWithStation(mon.Endpoint) + ".");
                    }
                }

                SaveWatchList(mon);
                Render(mon, sh);
                AuditLogger.Log("imported " + added + " monitor rows for " + PlcEndpointNaming.Caption(mon.Endpoint) + " from " + Path.GetFileName(dlg.FileName) + ".");
                MessageBox.Show(DialogOwner,
                    string.Format(T("Msg_ImportDone", "Added {0} device(s). {1} were already listed."), added, offered - added) +
                    (applied > 0 ? "\r\n" + string.Format(T("Msg_ImportWrote", "Wrote {0} value(s)."), applied) : ""),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Label;DataType;Address with an optional Value - so a PlcLabels.csv is an
        /// import file as it stands. A line with text and no address is a comment.
        /// Tabs work as separators too: these lists arrive pasted out of GX Works and Excel.
        /// </summary>
        private static List<WatchRow> ParseCsv(string path, out Dictionary<string, int> values)
        {
            values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(path);
            var rows = ParseLines(lines);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var f = line.Split(line.Contains(";") ? ';' : '\t');
                int v;
                if (f.Length > 3 && f[2].Trim().Length > 0 &&
                    int.TryParse(f[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                    values[f[2].Trim().ToUpperInvariant()] = v;
            }
            return rows;
        }

        internal static List<WatchRow> ParseLines(IEnumerable<string> lines)
        {
            var rows = new List<WatchRow>();
            foreach (var raw in lines)
            {
                var line = (raw ?? "").Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var f = line.Split(line.Contains(";") ? ';' : '\t');
                if (f[0].Trim().Equals("Label", StringComparison.OrdinalIgnoreCase)) continue;   // header

                if (f.Length >= 3)
                {
                    string address = f[2].Trim().ToUpperInvariant();
                    if (address.Length == 0)
                    {
                        if (f[0].Trim().Length > 0) rows.Add(new WatchRow { Comment = f[0].Trim() });
                        continue;
                    }
                    rows.Add(new WatchRow { Address = address, LabelOverride = f[0].Trim(), DataTypeOverride = f[1].Trim() });
                }
                else if (f.Length == 1 && System.Text.RegularExpressions.Regex.IsMatch(f[0].Trim(), "^[A-Za-z]{1,2}[0-9A-Fa-f]+$"))
                    rows.Add(new WatchRow { Address = f[0].Trim().ToUpperInvariant() });
                else if (f[0].Trim().Length > 0)
                    rows.Add(new WatchRow { Comment = string.Join(" ", f.Select(x => x.Trim())).Trim() });
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
            foreach (var c in name ?? "") sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString().Trim();
        }

        // ==================================================================
        // THE LISTS BETWEEN SESSIONS
        // ==================================================================

        /// <summary>Where the lists are kept. Replaceable so a test can use its own folder.</summary>
        internal static string WatchFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Monitor");

        /// <summary>
        /// Keyed by MODULE and the module's own PLC number, never by station id -
        /// the station id is what the PLC Connection page is allowed to change.
        /// </summary>
        private static string WatchPath(PlcConnectionSettings.Endpoint endpoint)
        {
            string key = SafeFileName((string.IsNullOrEmpty(endpoint.ModuleId) ? "Station" : endpoint.ModuleId) + "_PLC" + endpoint.LocalPlcNumber);
            return Path.Combine(WatchFolder, key + ".csv");
        }

        private const string SheetPrefix = "#sheet:";
        private const string HiddenPrefix = "#hidden:";
        private const string TabsPrefix = "#tabs:";

        internal static List<string> Serialize(MonitorTab mon)
        {
            var lines = new List<string>
            {
                "# Kaban HMI monitor watch list - " + (mon.Endpoint != null ? PlcEndpointNaming.CaptionWithStation(mon.Endpoint) : ""),
                "# One section per tab (#sheet:). A line with a label and no address is a comment. Values are not kept.",
                CsvHeader,
                // How many tabs there are, so a list whose tabs were ALL deleted
                // opens empty instead of growing a new "Main".
                TabsPrefix + mon.Sheets.Count.ToString(CultureInfo.InvariantCulture)
            };
            foreach (var sh in mon.Sheets)
            {
                lines.Add(SheetPrefix + sh.Name);
                foreach (var r in sh.Rows.Where(r => !r.IsBlank && !r.IsElement))
                    lines.Add(r.IsComment ? Csv(r.Comment) + ";;;"
                                          : string.Join(";", Csv(r.LabelOverride), Csv(r.DataTypeOverride), Csv(r.Address), "", Extras(r)).TrimEnd(';'));
            }
            foreach (var a in mon.Hidden.OrderBy(a => a, StringComparer.OrdinalIgnoreCase)) lines.Add(HiddenPrefix + a);
            return lines;
        }

        /// <summary>Reads the tabs back. Rows before any "#sheet:" line - a file from before the tabs - form the first tab, "Main".</summary>
        internal static void Deserialize(MonitorTab mon, IEnumerable<string> lines)
        {
            Sheet current = null;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith(HiddenPrefix, StringComparison.OrdinalIgnoreCase)) { mon.Hidden.Add(line.Substring(HiddenPrefix.Length).Trim()); continue; }
                if (line.StartsWith(TabsPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    int n;
                    if (int.TryParse(line.Substring(TabsPrefix.Length).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) mon.SavedTabCount = n;
                    continue;
                }
                if (line.StartsWith(SheetPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var name = line.Substring(SheetPrefix.Length).Trim();
                    current = new Sheet { Name = name.Length > 0 ? name : T("Tab_MonMain", "Main") };
                    mon.Sheets.Add(current);
                    continue;
                }
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var f = line.Split(';');
                if (f.Length < 3 || f[0].Trim().Equals("Label", StringComparison.OrdinalIgnoreCase)) continue;
                if (current == null) { current = new Sheet { Name = T("Tab_MonMain", "Main") }; mon.Sheets.Add(current); }
                string address = f[2].Trim().ToUpperInvariant();
                if (address.Length == 0)
                {
                    if (f[0].Trim().Length > 0) current.Rows.Add(new WatchRow { Station = mon.Station, Comment = f[0].Trim() });
                    continue;
                }
                var row = new WatchRow { Station = mon.Station, Address = address, LabelOverride = f[0].Trim(), DataTypeOverride = f[1].Trim() };
                if (f.Length > 4) ReadExtras(row, f[4]);
                current.Rows.Add(row);
            }
        }

        /// <summary>
        /// The fifth field of a kept row: its display format, an array's count and
        /// fold, a string's length - "fmt=Hexadecimal|n=10|open=1|len=32", only what
        /// differs from the default. A list from before reads the same as ever.
        /// </summary>
        internal static string Extras(WatchRow r)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(r.DisplayFormat) && r.DisplayFormat != FormatDecimal) parts.Add("fmt=" + r.DisplayFormat);
            if (r.ArrayCount > 1) { parts.Add("n=" + r.ArrayCount.ToString(CultureInfo.InvariantCulture)); if (r.Expanded) parts.Add("open=1"); }
            if (r.StringLength != 16) parts.Add("len=" + r.StringLength.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(r.ConditionKey)) parts.Add("cond=" + r.ConditionKey);
            return string.Join("|", parts);
        }

        internal static void ReadExtras(WatchRow r, string field)
        {
            foreach (var p in (field ?? "").Split('|'))
            {
                int eq = p.IndexOf('=');
                if (eq <= 0) continue;
                string k = p.Substring(0, eq).Trim(), v = p.Substring(eq + 1).Trim();
                int n;
                switch (k)
                {
                    case "fmt": r.DisplayFormat = DisplayFormats.Contains(v) ? v : ""; break;
                    case "n": if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) r.ArrayCount = Math.Max(1, Math.Min(1000, n)); break;
                    case "open": r.Expanded = v == "1"; break;
                    case "len": if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) r.StringLength = Math.Max(1, Math.Min(256, n)); break;
                    case "cond": r.ConditionKey = v; break;
                }
            }
        }

        private static void SaveWatchList(MonitorTab mon)
        {
            try
            {
                Directory.CreateDirectory(WatchFolder);
                SafeFile.WriteAllText(WatchPath(mon.Endpoint), string.Join("\r\n", Serialize(mon)) + "\r\n", new UTF8Encoding(true));
            }
            catch (Exception ex) { AuditLogger.Log("could not save the monitor watch list: " + ex.Message); }
        }

        private static void LoadWatchList(MonitorTab mon)
        {
            try
            {
                string path = WatchPath(mon.Endpoint);
                if (File.Exists(path)) Deserialize(mon, File.ReadAllLines(path));
            }
            catch (Exception ex) { AuditLogger.Log("could not read the monitor watch list: " + ex.Message); }
        }

        /// <summary>
        /// The window that owns this monitor's questions and dialogs: the monitor
        /// itself, or - as a side page - the window it is in (a dialog owned by a
        /// control inside a window is not something Windows does reliably).
        /// </summary>
        private IWin32Window DialogOwner
        {
            get { return TopLevel ? (IWin32Window)this : (IWin32Window)(TopLevelControl as Form ?? FindForm() ?? (Control)this); }
        }

        /// <summary>Keeps every controller's lists now (a side page is hidden, never closed).</summary>
        public void SaveLists()
        {
            foreach (var mon in _monitors) SaveWatchList(mon);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _refresh.Stop();
            foreach (var mon in _monitors) SaveWatchList(mon);
            var place = new Dictionary<string, string>();
            WindowLayout.Store(this, LayoutName, place);
            WindowLayout.Write(WindowsPath, place);
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _refresh.Dispose(); _tips.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
