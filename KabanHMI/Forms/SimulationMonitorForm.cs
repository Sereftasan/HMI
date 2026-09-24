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
    public class SimulationMonitorForm : Form
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
        private Button _btnUndo;

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

            public bool IsComment { get { return string.IsNullOrWhiteSpace(Address) && !string.IsNullOrWhiteSpace(Comment); } }

            /// <summary>True for the trailing empty line.</summary>
            public bool IsBlank { get { return string.IsNullOrWhiteSpace(Address) && string.IsNullOrWhiteSpace(Comment); } }

            public bool IsDevice { get { return !string.IsNullOrWhiteSpace(Address); } }

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

            public bool IsUnknownToLabelTable
            {
                get { return string.IsNullOrWhiteSpace(LabelOverride) && PlcLabelTable.ByAddress(Station, Address) == null; }
            }

            public WatchRow Clone()
            {
                return new WatchRow { Address = Address, Station = Station, LabelOverride = LabelOverride,
                                      DataTypeOverride = DataTypeOverride, Comment = Comment };
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

        public SimulationMonitorForm()
        {
            Text = LanguageManager.GetString("Ttl_SimMonitor", "PLC Monitor - live device memory");
            Size = new Size(820, 700);
            MinimumSize = new Size(620, 420);
            StartPosition = FormStartPosition.Manual;
            MinimizeBox = true;
            ShowInTaskbar = true;
            BackColor = Color.White;
            Font = UiStyle.Body;
            KeyPreview = true;

            var bar = BuildToolbar();

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

        /// <summary>The UI layer must not reference the Simulation driver assembly, so the simulator is recognised by type name.</summary>
        private static bool IsSimulatedStation(int station)
        {
            var drv = PlcManager.Instance.GetDriverForMonitoring(station);
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
            btnAdd = UiStyle.ToolButton(E(0x2795), T("Tip_MonAdd", "Add a device or a comment line (Insert)"), _tips,
                (s, e) => ShowMenu(btnAdd, AddMenu()));
            add(btnAdd);
            add(UiStyle.ToolButton(E(0x1F4CB), T("Tip_MonCopy", "Copy the selected rows (Ctrl+C)"), _tips, (s, e) => CopySelected()));
            Button btnPaste = null;
            btnPaste = UiStyle.ToolButton(E(0x1F4CC), T("Tip_MonPaste", "Paste the copied rows (Ctrl+V: above the selected row)"), _tips,
                (s, e) => ShowMenu(btnPaste, PasteMenu()));
            add(btnPaste);
            Button btnDelete = null;
            btnDelete = UiStyle.ToolButton(E(0x1F5D1), T("Tip_MonDelete",
                "Delete: the selected rows (Del), this tab, every tab of this controller or of every controller"), _tips,
                (s, e) => ShowMenu(btnDelete, DeleteMenu()));
            add(btnDelete);
            // Up / down BUTTONS, not arrows: the thin arrows read as two sticks.
            add(UiStyle.ToolButton(E(0x1F53C), T("Tip_MonUp", "Move the selected rows up (Ctrl+Up)"), _tips, (s, e) => MoveSelected(-1)));
            add(UiStyle.ToolButton(E(0x1F53D), T("Tip_MonDown", "Move the selected rows down (Ctrl+Down)"), _tips, (s, e) => MoveSelected(+1)));
            _btnUndo = UiStyle.ToolButton(E(0x21A9), T("Tip_MonUndo", "Undo the last change (Ctrl+Z)"), _tips, (s, e) => Undo());
            add(_btnUndo);
            gap();
            Button btnFrom = null;
            btnFrom = UiStyle.ToolButton(E(0x1F5C3), T("Tip_MonCollect", "Add the devices of this screen, of the whole program or of the PLC label list"), _tips,
                (s, e) => ShowMenu(btnFrom, CollectMenu()));
            add(btnFrom);
            add(UiStyle.ToolButton(E(0x1F50D), T("Tip_MonFind", "Find where a device is used in the program, and open that page (Ctrl+F)"), _tips,
                (s, e) => OpenFind(null)));
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
            return m;
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
            return m;
        }

        private ContextMenuStrip SheetMenu()
        {
            var m = NewMenu();
            var mon = CurrentMonitor;
            m.Items.Add(Item(T("Mnu_SheetNew", "New tab (before this one)..."), (s, e) => NewSheet(), mon != null, keyText: "Ins"));
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
            if (ask && MessageBox.Show(this, question, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
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
                _tips.SetToolTip(_btnUndo, _undo.Count > 0
                    ? string.Format(T("Tip_MonUndoWhat", "Undo: {0} (Ctrl+Z)"), _undo[_undo.Count - 1].What)
                    : T("Tip_MonUndo", "Undo the last change (Ctrl+Z)"));
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
        // ==================================================================

        private sealed class UndoStep
        {
            public string What = "";
            public readonly List<KeyValuePair<MonitorTab, List<string>>> States = new List<KeyValuePair<MonitorTab, List<string>>>();
        }

        private readonly List<UndoStep> _undo = new List<UndoStep>();
        private const int UndoDepth = 50;

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
            foreach (var kv in step.States) RestoreState(kv.Key, kv.Value);
            UpdateToolbar();
            SetStatus(string.Format(T("Msg_Undone", "Undone: {0}."), step.What));
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
            mon.SheetTabs.DrawItem += (s, e) => DrawSheetTab(mon, e);
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
                Text = T("Msg_MonNoTabs", "No tabs. Add one with the tabs button (" + E(0x1F4D1) + "), or add a device and a tab 'Main' is made for it."),
                Visible = false
            };
            page.Controls.Add(mon.EmptyHint);
            UpdateEmptyState(mon);


            page.Controls.Add(mon.SheetTabs);

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

        private static DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                // The blank line at the end is a row we own, not the DataGridView's
                // own "new row", whose placeholder fights a 250ms refresh.
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(228, 231, 235),
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                Font = UiStyle.Body
            };
            grid.RowTemplate.Height = 26;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 232, 250);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(25, 28, 34);
            grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);

            // Label first: an engineer thinks in names, not addresses.
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Label", HeaderText = T("Col_Label", "Label") });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DataType", HeaderText = T("Col_DataType", "DataType") });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Address", HeaderText = T("Col_Address", "Address") });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = T("Col_Value", "Value") });
            grid.Columns["Value"].DefaultCellStyle.Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);
            grid.Columns["Value"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

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
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 246, 248);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 5);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            return grid;
        }

        // ==================================================================
        // GRID BEHAVIOUR
        // ==================================================================

        private void WireGrid(MonitorTab mon, Sheet sh)
        {
            var grid = sh.Grid;

            var rowMenu = NewMenu();
            rowMenu.Opening += (s, e) =>
            {
                rowMenu.Items.Clear();
                bool sel = SelectedIndices(sh).Count > 0;
                bool any = _clip.Count > 0 || ClipboardHasText();
                rowMenu.Items.Add(Item(T("Mnu_EditRow", "Edit..."), (s2, e2) => { if (grid.CurrentRow != null) EditRow(mon, sh, grid.CurrentRow.Index, null); }, grid.CurrentRow != null, keyText: "Enter"));
                var at = grid.CurrentRow != null && grid.CurrentRow.Index < sh.Rows.Count ? sh.Rows[grid.CurrentRow.Index] : null;
                rowMenu.Items.Add(Item(at != null && at.IsDevice ? string.Format(T("Mnu_FindUses", "Where is {0} used?"), at.Address) : T("Mnu_FindUsesNone", "Where is it used?"),
                                       (s2, e2) => OpenFind(at != null ? at.Address : null), at != null && at.IsDevice, keyText: "Ctrl+F"));
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
                if (from < sh.Rows.Count && !sh.Rows[from].IsBlank) grid.DoDragDrop(from, DragDropEffects.Move);
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
                sh.Rows.RemoveAt(from);
                sh.Rows.Insert(Math.Min(to, sh.Rows.Count - 1), moved);
                EnsureBlankRow(sh);
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
            if (row.IsComment || (column == "Value" && row.IsDevice))
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
            if (_find != null && !_find.IsDisposed) _find.Close();
            _find = new DeviceFindForm(mon.Station, PlcEndpointNaming.CaptionWithStation(mon.Endpoint), query, OpenUse);
            _find.Show(this);
            return _find;
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
                if (keyData == (Keys.Control | Keys.F)) { OpenFind(null); return true; }
            }
            // The tab strip of the controller's tabs, when it has the focus.
            if (monK != null && monK.SheetTabs.Focused)
            {
                switch (keyData)
                {
                    case Keys.Delete: DeleteSheet(); return true;
                    case Keys.F2: RenameSheet(); return true;
                    case Keys.Insert: NewSheet(); return true;
                    case Keys.Control | Keys.Left: MoveSheet(-1); return true;
                    case Keys.Control | Keys.Right: MoveSheet(+1); return true;
                }
            }
            if (shK != null && !editing && shK.Grid.ContainsFocus && keyData == Keys.Enter && shK.Grid.CurrentCell != null)
            {
                OpenCell(monK, shK, shK.Grid.CurrentCell.RowIndex, shK.Grid.CurrentCell.OwningColumn.Name);
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

            int current;
            string valueText = row.IsDevice && mon.Values.TryGetValue(row.Address, out current)
                ? current.ToString(CultureInfo.InvariantCulture) : "";

            using (var dlg = new PlcRowEditorForm(PlcEndpointNaming.Caption(mon.Endpoint),
                       row.IsComment ? row.Comment : row.EffectiveLabel, row.EffectiveDataType, row.Address, valueText,
                       focusField, isNew: false, isComment: row.IsComment))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Remember(T("Undo_Edit", "edit a row"), mon);

                if (row.IsComment) row.Comment = dlg.RowLabel;
                else
                {
                    string wanted = dlg.RowAddress;
                    row.Address = wanted;
                    mon.Hidden.Remove(wanted);
                    ApplyNames(mon, row, dlg.RowLabel, dlg.RowDataType);
                    if (dlg.RowValue.HasValue && dlg.RowValue.Value.ToString(CultureInfo.InvariantCulture) != valueText)
                        WriteValue(mon, row, dlg.RowValue.Value.ToString(CultureInfo.InvariantCulture));
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
                suggestAddress = PlcRowEditorForm.ElementAddress(above.Address, suggestType, 1);
            }

            using (var dlg = new PlcRowEditorForm(PlcEndpointNaming.Caption(mon.Endpoint), "", suggestType, suggestAddress, "",
                                                  comment ? "Label" : "Address", isNew: true, isComment: comment))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Remember(dlg.IsComment ? T("Undo_AddComment", "add a comment line") : T("Undo_AddDevice", "add a device"), mon);
                if (sh == null) { sh = EnsureSheet(mon); at = InsertPoint(sh, true); }

                var added = new List<WatchRow>();
                if (dlg.IsComment) added.Add(new WatchRow { Station = mon.Station, Comment = dlg.RowLabel });
                else
                {
                    int n = dlg.ArrayCount;
                    for (int i = 0; i < n; i++)
                    {
                        var r = new WatchRow { Station = mon.Station, Address = PlcRowEditorForm.ElementAddress(dlg.RowAddress, dlg.RowDataType, i) };
                        string label = dlg.RowLabel.Length == 0 ? "" : n > 1 ? dlg.RowLabel + "[" + i + "]" : dlg.RowLabel;
                        ApplyNames(mon, r, label.Length == 0 ? (PlcLabelTable.ByAddress(mon.Station, r.Address) ?? new PlcLabelTable.Entry()).Label : label,
                                   dlg.RowDataType);
                        mon.Hidden.Remove(r.Address);
                        added.Add(r);
                    }
                }
                sh.Rows.InsertRange(at, added);
                EnsureBlankRow(sh);
                if (dlg.RowValue.HasValue && added.Count == 1 && added[0].IsDevice)
                    WriteValue(mon, added[0], dlg.RowValue.Value.ToString(CultureInfo.InvariantCulture));
                SaveWatchList(mon);
                Render(mon, sh);
                Select(sh, Enumerable.Range(at, added.Count));
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
            foreach (var i in sel)
            {
                var r = sh.Rows[i];
                var copy = r.Clone();
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
            Remember(string.Format(T("Undo_Remove", "remove {0} row(s)"), sel.Count), mon);

            for (int k = sel.Count - 1; k >= 0; k--)
            {
                var r = sh.Rows[sel[k]];
                sh.Rows.RemoveAt(sel[k]);
                if (r.IsDevice) HideIfUnlisted(mon, r.Address, null, -1);
            }
            EnsureBlankRow(sh);
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
            int last = sh.Rows.Count - 1;                        // the empty line stays last
            if (direction < 0 && sel[0] == 0) return;
            if (direction > 0 && sel[sel.Count - 1] >= last - 1) return;
            Remember(T("Undo_Move", "move rows"), mon);

            var order = direction < 0 ? sel : Enumerable.Reverse(sel).ToList();
            foreach (var i in order)
            {
                var r = sh.Rows[i];
                sh.Rows.RemoveAt(i);
                sh.Rows.Insert(i + direction, r);
            }
            SaveWatchList(mon);
            Render(mon, sh);
            Select(sh, sel.Select(i => i + direction));
        }

        // ==================================================================
        // COLUMNS: widths, order, sorting
        // ==================================================================

        private static readonly string[] ColumnNames = { "Label", "DataType", "Address", "Value" };
        private static readonly int[] DefaultWidths = { 280, 170, 90, 120 };
        private static Dictionary<string, int> _colWidth, _colOrder;
        private static bool _applyingLayout;
        private bool _layoutPending;

        private static string ColumnsPath { get { return Path.Combine(WatchFolder, "Columns.cfg"); } }

        private static void LoadColumnLayout()
        {
            if (_colWidth != null) return;
            _colWidth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _colOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(ColumnsPath)) return;
                foreach (var line in File.ReadAllLines(ColumnsPath))
                {
                    // Label=280;0   (width;position)
                    int eq = line.IndexOf('=');
                    if (eq <= 0 || line.StartsWith("#")) continue;
                    var f = line.Substring(eq + 1).Split(';');
                    int w, o;
                    if (int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out w)) _colWidth[line.Substring(0, eq)] = w;
                    if (f.Length > 1 && int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out o)) _colOrder[line.Substring(0, eq)] = o;
                }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the monitor's column layout: " + ex.Message); }
        }

        private static void SaveColumnLayout()
        {
            try
            {
                Directory.CreateDirectory(WatchFolder);
                var lines = new List<string> { "# PLC Monitor columns: <column>=<width>;<position>" };
                foreach (var n in ColumnNames)
                {
                    int w, o;
                    if (!_colWidth.TryGetValue(n, out w)) continue;
                    _colOrder.TryGetValue(n, out o);
                    lines.Add(n + "=" + w.ToString(CultureInfo.InvariantCulture) + ";" + o.ToString(CultureInfo.InvariantCulture));
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
            }
            catch { }
            finally { _applyingLayout = false; }
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
                LoadColumnLayout();
                foreach (DataGridViewColumn c in source.Columns) { _colWidth[c.Name] = c.Width; _colOrder[c.Name] = c.DisplayIndex; }
                SaveColumnLayout();
                foreach (var m in _monitors)
                    foreach (var other in m.Sheets)
                        if (other.Grid != null && other.Grid != source) ApplyColumnLayout(other.Grid);
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
            foreach (var r in sh.Rows.Where(r => !r.IsBlank))
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
                case "Value":
                {
                    long? va = ValueOf(mon, a), vb = ValueOf(mon, b);
                    if (va == null || vb == null) return va == null ? (vb == null ? 0 : 1) : -1;   // unread last
                    return va.Value.CompareTo(vb.Value);
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
            if (!r.IsDevice || !mon.Values.TryGetValue(r.Address, out v)) return null;
            if (!IsDoubleWord(r.EffectiveDataType)) return v;
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

        private void NewSheet()
        {
            var mon = CurrentMonitor;
            if (mon == null) return;
            string name = AskText(T("Ttl_SheetNew", "New tab"), T("Lbl_SheetName", "Name of the tab:"),
                                  UniqueName(mon, T("Tab_MonNew", "Tab") + " " + (mon.Sheets.Count + 1)), mon, null);
            if (name == null) return;
            Remember(string.Format(T("Undo_NewTab", "new tab '{0}'"), name), mon);
            // BEFORE the tab on display, as asked; at the start when there is none.
            AddSheet(mon, name, Math.Max(0, mon.SheetTabs.SelectedIndex));
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
            SaveWatchList(mon);
        }

        private void DeleteSheet()
        {
            var mon = CurrentMonitor;
            var sh = mon != null ? mon.Current : null;
            if (sh == null) return;
            int n = sh.Rows.Count(r => !r.IsBlank);
            if (n > 0 && MessageBox.Show(this,
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
                return f.ShowDialog(this) == DialogResult.OK ? box.Text.Trim() : null;
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
            MessageBox.Show(this,
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
                MessageBox.Show(this, string.Format(T("Msg_MonScreenNone", "The screen on display uses no device of {0}."),
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
                MessageBox.Show(this, string.Format(T("Msg_MonProgramNone", "No page of the program uses a device of {0}."),
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
                MessageBox.Show(this, T("Msg_MonLabelsNone", "This controller has no PLC label list (PlcLabels.csv)."),
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

        // ==================================================================
        // WRITE
        // ==================================================================

        /// <summary>
        /// Writes one device, and reports WHY when it cannot - a silently ignored
        /// write on a machine is the kind of thing that gets diagnosed as a broken PLC.
        /// </summary>
        private void WriteValue(MonitorTab mon, WatchRow row, string text)
        {
            if (row == null || !row.IsDevice) return;

            long typed;
            if (!long.TryParse((text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out typed))
            {
                SetStatus(T("Msg_MonNotNumber", "Not a whole number - nothing written."));
                return;
            }

            bool dword = IsDoubleWord(row.EffectiveDataType);
            if ((!dword && (typed < short.MinValue || typed > ushort.MaxValue)) ||
                (dword && (typed < int.MinValue || typed > uint.MaxValue)))
            {
                SetStatus(T("Msg_MonOutOfRange", "Out of range for this data type - nothing written."));
                return;
            }
            int value = (int)typed;

            var driver = mon.WriteDriver;
            if (driver == null)
            {
                MessageBox.Show(this, PlcManager.Instance.DescribeAccess(mon.Station), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (dword)
            {
                // Both words, low first - the low word alone turns a 32-bit value into nonsense.
                int low = (int)(typed & 0xFFFF), high = (int)((typed >> 16) & 0xFFFF);
                var next = PlcAddressHelper.GetNextAddress(row.Address);
                driver.Write(row.Address, low);
                driver.Write(next, high);
                mon.Values[row.Address] = low;
                mon.Values[next] = high;
            }
            else
            {
                driver.Write(row.Address, value);
                mon.Values[row.Address] = value;
            }

            if (!mon.IsSimulated)
                AuditLogger.Log("monitor wrote " + typed + " to " + row.Address + " on " + PlcEndpointNaming.CaptionWithStation(mon.Endpoint) + ".");
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
        }

        /// <summary>
        /// The simulator's whole image, straight out of memory. Addresses it holds
        /// that no list has are APPENDED to the first tab - which is how a device a
        /// screen wrote to shows up without anyone adding it by hand.
        /// </summary>
        private void ReadSimulated(MonitorTab mon)
        {
            var driver = mon.ReadDriver;
            if (driver == null) return;
            var dumpMethod = driver.GetType().GetMethod("Dump");
            if (dumpMethod == null) return;
            var dump = dumpMethod.Invoke(driver, null) as System.Collections.IDictionary;
            if (dump == null) return;

            mon.Values.Clear();
            foreach (System.Collections.DictionaryEntry kv in dump)
                mon.Values[Convert.ToString(kv.Key)] = Convert.ToInt32(kv.Value);

            // A double word's second device is part of its row, not a device of
            // its own: D101 under a D100 double word is D100's high half.
            var highWords = new HashSet<string>(
                mon.Sheets.SelectMany(s => s.Rows).Where(r => r.IsDevice && IsDoubleWord(r.EffectiveDataType))
                   .Select(r => PlcAddressHelper.GetNextAddress(r.Address)), StringComparer.OrdinalIgnoreCase);
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
            foreach (var r in sh.Rows.Where(r => r.IsDevice))
            {
                addresses.Add(r.Address);
                if (IsDoubleWord(r.EffectiveDataType)) addresses.Add(PlcAddressHelper.GetNextAddress(r.Address));
            }
            var batch = addresses.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (batch.Length == 0) return;

            mon.ReadInFlight = true;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                int[] values = null;
                bool ok = false;
                try { ok = driver.TryReadMulti(batch, out values); }
                catch (Exception ex) { AuditLogger.Log("monitor read failed: " + ex.Message); }

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

                if (model.IsComment)
                {
                    // A heading: its text across the label cell, the row tinted.
                    Set(row.Cells["Label"], model.Comment);
                    Set(row.Cells["DataType"], ""); Set(row.Cells["Address"], ""); Set(row.Cells["Value"], "");
                    Style(row, ColCommentBack, ColCommentText, UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold | FontStyle.Italic));
                    continue;
                }
                Style(row, Color.Empty, Color.Empty, null);

                Set(row.Cells["Address"], model.Address);
                if (model.IsBlank)
                {
                    Set(row.Cells["Label"], ""); Set(row.Cells["DataType"], ""); Set(row.Cells["Value"], "");
                    continue;
                }

                // An address the label list does not know reads NULL, which is itself
                // useful: a screen is bound to something outside that list.
                string label = model.EffectiveLabel;
                Set(row.Cells["Label"], label.Length == 0 ? "NULL" : label);
                var wantLabelColour = model.IsUnknownToLabelTable ? ColLabelUnknown : ColLabelKnown;
                if (row.Cells["Label"].Style.ForeColor != wantLabelColour) row.Cells["Label"].Style.ForeColor = wantLabelColour;

                Set(row.Cells["DataType"], model.EffectiveDataType);

                int value;
                if (mon.Values.TryGetValue(model.Address, out value))
                {
                    // A DOUBLE WORD IS ONE VALUE over two devices.
                    long shown = value;
                    if (IsDoubleWord(model.EffectiveDataType))
                    {
                        int high;
                        mon.Values.TryGetValue(PlcAddressHelper.GetNextAddress(model.Address), out high);
                        shown = CombineDouble(value, high, IsUnsigned(model.EffectiveDataType));
                    }
                    Set(row.Cells["Value"], shown.ToString(CultureInfo.InvariantCulture));
                    var want = shown != 0 ? ColValueSet : ColValueClear;
                    if (row.Cells["Value"].Style.ForeColor != want) row.Cells["Value"].Style.ForeColor = want;
                }
                else
                {
                    // not read yet, or the read failed - an honest gap, not a zero
                    Set(row.Cells["Value"], "-");
                    if (row.Cells["Value"].Style.ForeColor != ColLabelUnknown) row.Cells["Value"].Style.ForeColor = ColLabelUnknown;
                }
            }
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
                                sh.Rows.Count(r => r.IsDevice), sh.Rows.Count(r => r.IsComment), where);
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
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var lines = new List<string>
                    {
                        "# " + PlcEndpointNaming.CaptionWithStation(mon.Endpoint) + " / " + sh.Name +
                        " - exported " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        CsvHeader
                    };
                    foreach (var r in sh.Rows.Where(r => !r.IsBlank))
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
                    MessageBox.Show(this, T("Msg_ExportFailed", "Could not write the file:") + "\r\n" + ex.Message,
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
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                List<WatchRow> parsed;
                Dictionary<string, int> values;
                try { parsed = ParseCsv(dlg.FileName, out values); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, T("Msg_ImportFailed", "Could not read the file:") + "\r\n" + ex.Message,
                                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (parsed.Count(r => r.IsDevice) == 0)
                {
                    MessageBox.Show(this, T("Msg_ImportNothing", "No usable rows found. Expected Label / DataType / Address and optionally a Value column."),
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
                if (values.Count > 0 && MessageBox.Show(this,
                        string.Format(T("Msg_ImportApplyValues", "The file also carries {0} values. Write them to this controller as well?"), values.Count),
                        Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    var driver = mon.WriteDriver;
                    if (driver == null)
                        MessageBox.Show(this, PlcManager.Instance.DescribeAccess(mon.Station), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else
                    {
                        foreach (var kv in values) { driver.Write(kv.Key, kv.Value); mon.Values[kv.Key] = kv.Value; applied++; }
                        if (!mon.IsSimulated)
                            AuditLogger.Log("monitor imported and WROTE " + applied + " values to " + PlcEndpointNaming.CaptionWithStation(mon.Endpoint) + ".");
                    }
                }

                SaveWatchList(mon);
                Render(mon, sh);
                AuditLogger.Log("imported " + added + " monitor rows for " + PlcEndpointNaming.Caption(mon.Endpoint) + " from " + Path.GetFileName(dlg.FileName) + ".");
                MessageBox.Show(this,
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
                foreach (var r in sh.Rows.Where(r => !r.IsBlank))
                    lines.Add(r.IsComment ? Csv(r.Comment) + ";;;"
                                          : string.Join(";", Csv(r.LabelOverride), Csv(r.DataTypeOverride), Csv(r.Address), ""));
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
                current.Rows.Add(new WatchRow { Station = mon.Station, Address = address, LabelOverride = f[0].Trim(), DataTypeOverride = f[1].Trim() });
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _refresh.Stop();
            foreach (var mon in _monitors) SaveWatchList(mon);
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _refresh.Dispose(); _tips.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
