using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Forms
{
    // ======================================================================
    // LABELS SHOWN AS TABLES (22 October: "create a 2-dimensional table view for the structural
    // array data type monitor (or same-dimensional multiple array labels): the structure members as
    // the columns (the data type with the label name as the column header), a line per element - the
    // whole recipe seen like an Excel table ('<> KESİM ADIMLARI'). A button changes the values shown.
    // Columns can be added at the end as new labels of the same array quantity, to watch them
    // together - the 'İŞ LİSTESİ' kind, where the same index of each label belongs together".
    //
    // And that evening: "make the table view and the list view label based: 'Show this label in table
    // view' on a label of a structure or an array moves it on top of the tab as a table; turned back
    // to the list view it gets back to the list at the bottom. The tables are always at the top (their
    // order changed among them with up / down), the list always at the bottom - one list in a tab and
    // as many tables above it as the user likes. The label's description is the table's caption, the
    // view buttons just under it, the table under them."
    //
    // A table is a label of the tab's (Sheet.Tables) - kept, read, undone and exported as its list's
    // rows are - and the labels joined to it (WatchRow.Joined): their columns at its end.
    // ======================================================================
    public partial class SimulationMonitorForm
    {
        private const string TablePrefix = "#table:";

        /// <summary>A column of a table: an array's elements, or one member of each of them.</summary>
        internal sealed class TableColumn
        {
            /// <summary>The array (a label of the table).</summary>
            public WatchRow Source;
            /// <summary>The structure's member (-1: the element itself, an array of a simple type).</summary>
            public int Member = -1;
            public string Header = "";
            public string DataType = "";
        }

        /// <summary>A table drawn: its label (and the ones joined to it), its caption, its buttons, its grid.</summary>
        internal sealed class TableBlock
        {
            public WatchRow Head;
            public readonly List<WatchRow> Sources = new List<WatchRow>();
            public List<TableColumn> Columns = new List<TableColumn>();
            public int Count;
            public Panel Panel;
            public Label Caption, Note;
            public FlowLayoutPanel Bar;
            public Button ShowHmi, ShowPlc, Add, Up, Down, ToList;
            public DataGridView Grid;
            public string Key = "";
        }

        // ---- the model ------------------------------------------------------------------

        /// <summary>The tab's tables: each a label and the labels joined to it, in their order.</summary>
        internal static List<List<WatchRow>> TableGroups(Sheet sh)
        {
            EnsureElements(sh.Tables);
            var groups = new List<List<WatchRow>>();
            foreach (var r in Items(sh.Tables).Where(r => r.IsDevice))
            {
                if (groups.Count == 0 || !r.Joined) groups.Add(new List<WatchRow>());
                groups[groups.Count - 1].Add(r);
            }
            return groups;
        }

        /// <summary>A table's columns after "#": each label's members (a structure's), or the label itself.</summary>
        internal static List<TableColumn> TableColumnsOf(List<WatchRow> group)
        {
            var cols = new List<TableColumn>();
            for (int i = 0; i < group.Count; i++)
            {
                var a = group[i];
                string label = a.EffectiveLabel.Length > 0 ? a.EffectiveLabel : a.Address;
                string type = StructuredTypes.BaseName(a.EffectiveDataType);
                var st = StructuredTypes.Find(type, a.Station);
                if (st != null)
                {
                    // the table's own label: its members' names; a label joined to it: "Label.Member"
                    // (a structure of one member: the label alone)
                    for (int j = 0; j < st.Members.Count; j++)
                        cols.Add(new TableColumn { Source = a, Member = j, DataType = st.Members[j].DataType,
                                                   Header = i == 0 ? st.Members[j].Name : st.Members.Count == 1 ? label : label + "." + st.Members[j].Name });
                }
                else cols.Add(new TableColumn { Source = a, DataType = type, Header = label });
            }
            return cols;
        }

        /// <summary>The row a cell shows: element index of its label (and its member) - null when there is none.</summary>
        internal static WatchRow TableCellRow(TableColumn c, int index)
        {
            if (c == null || c.Source == null || c.Source.Elements == null || index < 0 || index >= c.Source.Elements.Count) return null;
            var e = c.Source.Elements[index];
            if (c.Member < 0) return e;
            return e.Members != null && c.Member < e.Members.Count ? e.Members[c.Member] : null;
        }

        /// <summary>A cell's text: as the HMI shows it (a number's divided by its divisor) or as the PLC holds it; "-" not read yet.</summary>
        internal static string TableText(MonitorTab mon, bool raw, TableColumn c, int index, out bool nonZero)
        {
            nonZero = false;
            var x = TableCellRow(c, index);
            if (x == null) return "";
            string text = !raw && HasCalculated(x) ? FormatCalculated(mon, x, out nonZero) : FormatValue(mon, x, out nonZero);
            return text ?? "-";
        }

        /// <summary>A label's name for a caption or a menu (its address without one).</summary>
        private static string NameOf(WatchRow r) { return r.EffectiveLabel.Length > 0 ? r.EffectiveLabel : r.Address; }

        // ---- moving labels between the list and the tables -------------------------------

        /// <summary>"Show this label in table view": the label off the list, a table at the end of the tables.</summary>
        internal void ShowInTable(MonitorTab mon, Sheet sh, WatchRow label)
        {
            if (mon == null || sh == null || label == null || !label.IsArray || !sh.Rows.Contains(label)) return;
            Remember(string.Format(T("Undo_ShowInTable", "'{0}' in table view"), NameOf(label)), mon);
            sh.Rows.Remove(label);
            label.Joined = false;
            sh.Tables.Rows.Add(label);
            sh.Grid.ClearSelection();
            sh.WantView = "tables";                       // its table in sight
            EnsureBlankRow(sh);
            SaveWatchList(mon);
            Render(mon, sh);
        }

        /// <summary>A table back to the list view: its labels at the end of the list, in their order.</summary>
        internal void TableToList(MonitorTab mon, Sheet sh, WatchRow head)
        {
            var group = TableGroups(sh).FirstOrDefault(g => g.Contains(head));
            if (group == null) return;
            Remember(string.Format(T("Undo_TableToList", "'{0}' in list view"), NameOf(group[0])), mon);
            foreach (var r in group)
            {
                sh.Tables.Rows.Remove(r);
                r.Joined = false;
                sh.Rows.Insert(InsertPoint(sh, true), r);
            }
            sh.WantView = "list";                         // where they went
            EnsureBlankRow(sh);
            SaveWatchList(mon);
            Render(mon, sh);
        }

        /// <summary>One label of a table back to the list (the table's own: the next one leads it).</summary>
        internal void TakeOffTable(MonitorTab mon, Sheet sh, WatchRow label)
        {
            var group = TableGroups(sh).FirstOrDefault(g => g.Contains(label));
            if (group == null) return;
            if (group.Count == 1) { TableToList(mon, sh, label); return; }
            Remember(string.Format(T("Undo_TakeOffTable", "'{0}' off the table"), NameOf(label)), mon);
            if (group[0] == label) { group[1].Joined = false; group[1].TableRaw = label.TableRaw; }
            sh.Tables.Rows.Remove(label);
            label.Joined = false;
            sh.Rows.Insert(InsertPoint(sh, true), label);
            EnsureBlankRow(sh);
            SaveWatchList(mon);
            Render(mon, sh);
        }

        /// <summary>A table up or down among the tables (the list stays under them).</summary>
        internal void MoveTable(MonitorTab mon, Sheet sh, WatchRow head, int by)
        {
            var groups = TableGroups(sh);
            int i = groups.FindIndex(g => g.Contains(head)), j = i + by;
            if (i < 0 || j < 0 || j >= groups.Count) return;
            Remember(T("Undo_MoveTable", "move a table"), mon);
            var g0 = groups[i];
            groups[i] = groups[j];
            groups[j] = g0;
            sh.Tables.Rows.Clear();
            foreach (var g in groups) sh.Tables.Rows.AddRange(g);
            SaveWatchList(mon);
            Render(mon, sh);
        }

        /// <summary>
        /// A label joined to a table: its columns at the table's end (from the tab's list: moved; from
        /// elsewhere: a copy). One of another length is put in the list instead, and said so.
        /// </summary>
        internal void JoinTable(MonitorTab mon, Sheet sh, WatchRow head, WatchRow label)
        {
            var group = TableGroups(sh).FirstOrDefault(g => g.Contains(head));
            if (group == null || label == null) return;
            Remember(string.Format(T("Undo_JoinTable", "'{0}' added to a table"), NameOf(label)), mon);
            label.Station = mon.Station;
            if (sh.Rows.Contains(label)) sh.Rows.Remove(label);
            if (label.EffectiveCount != group[0].EffectiveCount)
            {
                sh.Rows.Insert(InsertPoint(sh, true), label);
                SetStatus(string.Format(T("Msg_MonTableOtherLength", "{0} has {1} elements, the table {2}: it is in the tab's list, not in the table."),
                                        NameOf(label), label.EffectiveCount, group[0].EffectiveCount));
            }
            else
            {
                label.Joined = true;
                sh.Tables.Rows.Insert(sh.Tables.Rows.IndexOf(group[group.Count - 1]) + 1, label);
            }
            EnsureBlankRow(sh);
            SaveWatchList(mon);
            Render(mon, sh);
        }

        /// <summary>A tab of before shown whole as a table: its arrays of its first array's length a table, the rest its list.</summary>
        internal static void TakeLegacyTable(Sheet sh)
        {
            var arrays = sh.Rows.Where(r => r.IsArray).ToList();
            if (arrays.Count > 0)
            {
                int n = arrays[0].EffectiveCount;
                bool first = true;
                foreach (var a in arrays.Where(a => a.EffectiveCount == n))
                {
                    sh.Rows.Remove(a);
                    a.Joined = !first;
                    if (first) a.TableRaw = sh.LegacyTable == "raw";
                    first = false;
                    sh.Tables.Rows.Add(a);
                }
            }
            sh.LegacyTable = null;
        }

        // ---- the view -----------------------------------------------------------------------

        /// <summary>
        /// A panel that does not scroll to the control given the focus: a click into a table taller than the room
        /// scrolled the tables, its caption out of sight (23 October: the caption cut at its top).
        /// </summary>
        private sealed class TablesPanel : Panel
        {
            protected override Point ScrollToControl(Control activeControl) { return DisplayRectangle.Location; }
        }

        /// <summary>
        /// Where the tables go, and the two tabs under the tab ("List viewed labels", "Table viewed labels") its list
        /// and its tables are on while it has both (see ArrangeViews).
        /// </summary>
        private void BuildTablesHost(MonitorTab mon, Sheet sh)
        {
            sh.TablesHost = new TablesPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Name = "tablesHost" };
            sh.TablesHost.Resize += (s, e) => LayoutTables(sh);
            sh.ViewTabs = new TabControl
            {
                Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed, SizeMode = TabSizeMode.Normal,
                Padding = new Point(14, 4), Font = UiStyle.Body, Name = "viewTabs"
            };
            sh.ViewTabs.DrawItem += (s, e) => DrawFlatTab(sh.ViewTabs, e);
            sh.ListPage = new TabPage(T("Tab_MonListLabels", "List viewed labels")) { BackColor = Color.White, Name = "listPage" };
            sh.TablesPage = new TabPage(T("Tab_MonTableLabels", "Table viewed labels")) { BackColor = Color.White, Name = "tablesPage" };
            sh.ViewTabs.TabPages.Add(sh.ListPage);
            sh.ViewTabs.TabPages.Add(sh.TablesPage);
            sh.TablesPage.Controls.Add(sh.TablesHost);
            sh.ViewTabs.SelectedIndexChanged += (s, e) => { UpdateToolbar(); RefreshValues(); };
            sh.ViewMode = 0;                                // the list alone: on the tab itself (AttachSheet put it there)
            sh.Blocks.Clear();
            sh.TablesKey = null;
        }

        /// <summary>The tables drawn anew: made again when their labels changed, their values every refresh.</summary>
        internal void RenderTables(MonitorTab mon, Sheet sh)
        {
            if (sh.TablesHost == null) return;
            foreach (var r in sh.Tables.Rows) r.Station = mon.Station;
            var groups = TableGroups(sh);
            string key = string.Join(";", groups.Select(g => string.Join("+", g.Select(r =>
                r.Address + "~" + r.EffectiveDataType + "~" + r.EffectiveCount + "~" + NameOf(r) + "~" + r.TableRaw))));
            if (key != sh.TablesKey && !sh.TableEditing)
            {
                sh.TablesKey = key;
                foreach (var b in sh.Blocks) { sh.TablesHost.Controls.Remove(b.Panel); b.Panel.Dispose(); }
                sh.Blocks.Clear();
                for (int i = 0; i < groups.Count; i++) sh.Blocks.Add(BuildBlock(mon, sh, groups[i], i, groups.Count));
                foreach (var b in sh.Blocks) sh.TablesHost.Controls.Add(b.Panel);
                LayoutTables(sh);
            }
            ArrangeViews(sh);
            foreach (var b in sh.Blocks)
                if (!b.Grid.IsCurrentCellInEditMode) b.Grid.Invalidate();
        }

        /// <summary>
        /// The list and the tables on the tab: the list alone, the tables alone - or, the tab having both, each on
        /// its own tab under it: "List viewed labels" first, "Table viewed labels" second (gone again when either is
        /// empty). The one that just came is shown; ShowInTable / TableToList ask for theirs.
        /// </summary>
        internal static void ArrangeViews(Sheet sh)
        {
            if (sh.ViewTabs == null || sh.Page == null || sh.Grid == null) return;
            bool tables = sh.Blocks.Count > 0;
            bool list = sh.Rows.Any(r => !r.IsBlank);
            int mode = tables && list ? 2 : tables ? 1 : 0;
            if (mode != sh.ViewMode)
            {
                int was = sh.ViewMode;
                sh.Page.SuspendLayout();
                if (mode == 2)
                {
                    MoveInto(sh.Grid, sh.ListPage);
                    MoveInto(sh.TablesHost, sh.TablesPage);
                    MoveInto(sh.ViewTabs, sh.Page);
                    // the side that was not there before is the one shown (opened with both: the list, the first)
                    if (sh.WantView == null && sh.ViewArranged) sh.WantView = was == 1 ? "list" : "tables";
                }
                else
                {
                    if (sh.ViewTabs.Parent != null) sh.ViewTabs.Parent.Controls.Remove(sh.ViewTabs);
                    if (mode == 1) { MoveInto(sh.Grid, sh.ListPage); MoveInto(sh.TablesHost, sh.Page); }
                    else { MoveInto(sh.TablesHost, sh.TablesPage); MoveInto(sh.Grid, sh.Page); }
                }
                sh.ViewMode = mode;
                sh.Page.ResumeLayout(true);
                LayoutTables(sh);
            }
            if (mode == 2 && sh.WantView != null)
            {
                var want = sh.WantView == "list" ? sh.ListPage : sh.TablesPage;
                if (sh.ViewTabs.SelectedTab != want) sh.ViewTabs.SelectedTab = want;
            }
            sh.WantView = null;
            sh.ViewArranged = true;
        }

        private static void MoveInto(Control c, Control into)
        {
            if (c.Parent == into) return;
            if (c.Parent != null) c.Parent.Controls.Remove(c);
            into.Controls.Add(c);
        }

        /// <summary>The list in sight: on the tab itself, or its "List viewed labels" tab the one chosen.</summary>
        internal static bool ListShown(Sheet sh)
        {
            if (sh == null || sh.ViewTabs == null) return true;
            return sh.ViewMode == 0 || (sh.ViewMode == 2 && sh.ViewTabs.SelectedTab == sh.ListPage);
        }

        /// <summary>The list brought in sight (its tab chosen) - for what is done to its rows.</summary>
        internal static void ShowListView(Sheet sh)
        {
            if (sh == null || sh.ViewTabs == null) return;
            if (sh.ViewMode == 2 && sh.ViewTabs.SelectedTab != sh.ListPage) sh.ViewTabs.SelectedTab = sh.ListPage;
            else if (sh.ViewMode == 1) sh.WantView = "list";
        }

        /// <summary>
        /// The tables one under the other, each as tall as ten of its lines; room left over goes to the tables with
        /// more lines, in their order (a table alone fills the tab) - then they scroll.
        /// </summary>
        private static void LayoutTables(Sheet sh)
        {
            var host = sh.TablesHost;
            if (host == null || sh.Blocks.Count == 0) return;
            const int gap = 6;
            var heights = sh.Blocks.Select(b => BlockHeight(b, 10)).ToList();
            int room = host.Height - 2;
            int extra = room - heights.Sum() - gap * heights.Count;
            for (int i = 0; i < sh.Blocks.Count && extra > 0; i++)
            {
                int add = Math.Min(extra, BlockHeight(sh.Blocks[i], sh.Blocks[i].Count) - heights[i]);
                if (add > 0) { heights[i] += add; extra -= add; }
            }
            int total = heights.Sum() + gap * heights.Count;
            // as wide as the room, the scroll bar's taken when they are taller than it (no sideways scroll of the tables)
            int w = Math.Max(200, host.Width - (total > host.Height ? SystemInformation.VerticalScrollBarWidth : 0) - 2);
            int y = 0;
            for (int i = 0; i < sh.Blocks.Count; i++)
            {
                sh.Blocks[i].Panel.SetBounds(0, y + host.AutoScrollPosition.Y, w, heights[i]);
                y += heights[i] + gap;
            }
        }

        /// <summary>A table's height showing up to <paramref name="lines"/> of its lines (its caption and buttons over them).</summary>
        private static int BlockHeight(TableBlock b, int lines)
        {
            int rows = Math.Max(1, Math.Min(lines, b.Count));
            int grid = b.Grid.ColumnHeadersHeight + rows * b.Grid.RowTemplate.Height + 4 + SystemInformation.HorizontalScrollBarHeight;
            return b.Caption.Height + b.Bar.Height + grid;
        }

        /// <summary>One table: its label as the caption, the view buttons under it, the table under them.</summary>
        private TableBlock BuildBlock(MonitorTab mon, Sheet sh, List<WatchRow> group, int index, int count)
        {
            var b = new TableBlock { Head = group[0], Count = group[0].EffectiveCount };
            b.Sources.AddRange(group);
            b.Columns = TableColumnsOf(group);
            b.Panel = new Panel { BackColor = Color.White, Name = "table" + index };

            var captionFont = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);
            b.Caption = new Label
            {
                // as tall as its font needs (a fixed 26 cut a larger screen scale's caption)
                Dock = DockStyle.Top, AutoEllipsis = true, Padding = new Padding(6, 6, 6, 2),
                Height = TextRenderer.MeasureText("Xg", captionFont).Height + 10,
                Font = captionFont, ForeColor = Color.FromArgb(35, 40, 48), Name = "tableCaption",
                Text = string.Join("  +  ", group.Select(NameOf))
            };
            UiStyle.SetTip(_tips, b.Caption, string.Join("\r\n", group.Select(r => NameOf(r) + "   " + r.Address + "   " + r.EffectiveDataType + " [" + r.EffectiveCount + "]")));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(6, 4, 6, 2), WrapContents = false, BackColor = Color.White, Name = "tableBar" };
            b.Bar = bar;
            // WHICH VALUES: two buttons side by side, the one shown dark
            b.ShowHmi = new Button { Text = T("Col_ValueHmi", "Value (HMI)"), AutoSize = true, Margin = new Padding(0), FlatStyle = FlatStyle.Flat, Name = "tableHmi" };
            b.ShowPlc = new Button { Text = T("Col_ValuePlc", "Value (PLC)"), AutoSize = true, Margin = new Padding(0, 0, 10, 0), FlatStyle = FlatStyle.Flat, Name = "tablePlc" };
            UiStyle.SetTip(_tips, b.ShowHmi, T("Tip_MonTableHmi", "The values as the HMI shows them: divided by their divisors, with their decimal places"));
            UiStyle.SetTip(_tips, b.ShowPlc, T("Tip_MonTablePlc", "The values as the PLC holds them"));
            Action paint = () =>
            {
                foreach (var btn in new[] { b.ShowHmi, b.ShowPlc })
                {
                    bool on = (btn == b.ShowPlc) == b.Head.TableRaw;
                    btn.BackColor = on ? Color.FromArgb(60, 66, 76) : Color.White;
                    btn.ForeColor = on ? Color.White : Color.FromArgb(60, 66, 76);
                    btn.FlatAppearance.BorderColor = Color.FromArgb(60, 66, 76);
                    btn.Cursor = Cursors.Hand;
                }
            };
            Action<bool> show = plc =>
            {
                if (b.Head.TableRaw == plc) return;
                b.Head.TableRaw = plc;
                paint();
                SaveWatchList(mon);
                b.Grid.Invalidate();
            };
            b.ShowHmi.Click += (s, e) => show(false);
            b.ShowPlc.Click += (s, e) => show(true);
            paint();
            b.Add = UiStyle.Round(new Button { Text = T("Btn_MonTableAdd", "Add an array..."), AutoSize = true, Margin = new Padding(0, 0, 6, 0), Name = "tableAdd" });
            UiStyle.SetTip(_tips, b.Add, T("Tip_MonTableAdd", "Columns at the end of the table: a label as long as the table's"));
            b.Add.Click += (s, e) => ShowMenu(b.Add, TableAddMenu(mon, sh, b.Head));
            b.Up = UiStyle.IconButton(ToolGlyphs.MoveUp, "Move up", T("Tip_MonTableUp", "This table above the one before it"), _tips);
            b.Down = UiStyle.IconButton(ToolGlyphs.MoveDown, "Move down", T("Tip_MonTableDown", "This table under the one after it"), _tips);
            b.Up.Name = "tableUp"; b.Down.Name = "tableDown";
            b.Up.Size = b.Down.Size = new Size(30, 26);
            b.Up.Margin = new Padding(0, 0, 2, 0); b.Down.Margin = new Padding(0, 0, 6, 0);
            b.Up.Enabled = index > 0;
            b.Down.Enabled = index < count - 1;
            b.Up.Click += (s, e) => MoveTable(mon, sh, b.Head, -1);
            b.Down.Click += (s, e) => MoveTable(mon, sh, b.Head, +1);
            b.ToList = UiStyle.Round(new Button { Text = T("Btn_MonTableList", "List view"), AutoSize = true, Margin = new Padding(0, 0, 12, 0), Name = "tableList" });
            UiStyle.SetTip(_tips, b.ToList, T("Tip_MonTableList", "Its labels back to the list, at its end"));
            b.ToList.Click += (s, e) => TableToList(mon, sh, b.Head);
            b.Note = new Label { AutoSize = true, ForeColor = Color.FromArgb(90, 96, 106), Margin = new Padding(0, 6, 0, 0), Name = "tableNote" };
            var odd = group.Skip(1).Where(r => r.EffectiveCount != b.Count).ToList();
            b.Note.Text = string.Format(T("Msg_MonTableLines", "{0} lines"), b.Count) +
                          (odd.Count == 0 ? "" : "  ·  " + string.Format(T("Msg_MonTableLeft", "another length: {0}"),
                                                                           string.Join(", ", odd.Select(a => NameOf(a) + " [" + a.EffectiveCount + "]"))));
            foreach (Control c in new Control[] { b.ShowHmi, b.ShowPlc, b.Add, b.Up, b.Down, b.ToList, b.Note }) bar.Controls.Add(c);
            // as tall as its tallest button (their font's)
            bar.Height = Math.Max(36, bar.Controls.Cast<Control>().Max(c => c.GetPreferredSize(Size.Empty).Height + c.Margin.Vertical) + bar.Padding.Vertical);

            b.Grid = BuildTableGrid(mon, sh, b);
            b.Panel.Controls.Add(b.Grid);
            b.Panel.Controls.Add(bar);
            b.Panel.Controls.Add(b.Caption);
            return b;
        }

        /// <summary>A table's grid: a line per element (virtual: only the lines in sight are asked for), "#" frozen at the left.</summary>
        private DataGridView BuildTableGrid(MonitorTab mon, Sheet sh, TableBlock b)
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                VirtualMode = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = true,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ShowCellToolTips = true,
                Name = "tableGrid"
            };
            UiStyle.GridLook(g);
            // the member's name over its data type, as the sheet's two-line headings
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            g.ColumnHeadersHeight = 2 * TextRenderer.MeasureText("Xg", g.ColumnHeadersDefaultCellStyle.Font ?? UiStyle.Body).Height + 16;
            g.DefaultCellStyle.Font = UiStyle.FontVariant(UiStyle.TableFont, FontStyle.Bold);
            g.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            var index = new DataGridViewTextBoxColumn { Name = "Index", HeaderText = "#", ReadOnly = true, Frozen = true, Width = 56, SortMode = DataGridViewColumnSortMode.NotSortable };
            index.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            index.DefaultCellStyle.Font = UiStyle.TableFont;
            index.DefaultCellStyle.BackColor = Color.FromArgb(246, 247, 249);
            index.DefaultCellStyle.ForeColor = Color.FromArgb(90, 96, 106);
            g.Columns.Add(index);
            var font = UiStyle.FontVariant(UiStyle.TableFont, FontStyle.Bold);
            for (int k = 0; k < b.Columns.Count; k++)
            {
                var c = b.Columns[k];
                int w = Math.Max(TextRenderer.MeasureText(c.Header, font).Width, TextRenderer.MeasureText("(" + c.DataType + ")", font).Width) + 22;
                var col = new DataGridViewTextBoxColumn
                {
                    Name = "c" + k, HeaderText = c.Header + "\n(" + c.DataType + ")", Tag = c, Width = Math.Max(80, Math.Min(260, w)),
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                g.Columns.Add(col);
            }
            g.RowCount = Math.Max(0, b.Count);

            g.CellValueNeeded += (s, e) =>
            {
                if (e.ColumnIndex == 0) { e.Value = e.RowIndex.ToString(CultureInfo.InvariantCulture); return; }
                bool nz;
                e.Value = TableText(mon, b.Head.TableRaw, g.Columns[e.ColumnIndex].Tag as TableColumn, e.RowIndex, out nz);
            };
            g.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex <= 0) return;
                bool nz;
                string text = TableText(mon, b.Head.TableRaw, g.Columns[e.ColumnIndex].Tag as TableColumn, e.RowIndex, out nz);
                e.CellStyle.ForeColor = text == "-" ? ColLabelUnknown : nz ? ColValueSet : ColValueClear;
            };
            g.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex <= 0) return;
                var x = TableCellRow(g.Columns[e.ColumnIndex].Tag as TableColumn, e.RowIndex);
                if (x == null) return;
                bool nz;
                string raw = FormatValue(mon, x, out nz) ?? "-";
                string tip = x.EffectiveLabel + "   " + x.ShownAddress + "   " + x.EffectiveDataType;
                if (HasCalculated(x) && x.IsScaled)
                    tip += "\r\n" + string.Format(T("Tip_MonTableScaled", "Value (PLC) {0} ÷ {1}, {2} decimal place(s): {3}"),
                                                  raw, x.EffectiveDivisor.ToString("0.#########", CultureInfo.InvariantCulture), x.EffectiveDecimals,
                                                  FormatCalculated(mon, x, out nz) ?? "-");
                e.ToolTipText = tip;
            };
            g.CellBeginEdit += (s, e) =>
            {
                var x = e.ColumnIndex > 0 ? TableCellRow(g.Columns[e.ColumnIndex].Tag as TableColumn, e.RowIndex) : null;
                if (x == null || !x.IsDevice || x.IsGroup) { e.Cancel = true; return; }
                sh.Editing = true;
            };
            g.CellValuePushed += (s, e) =>
            {
                var x = e.ColumnIndex > 0 ? TableCellRow(g.Columns[e.ColumnIndex].Tag as TableColumn, e.RowIndex) : null;
                string typed = (Convert.ToString(e.Value) ?? "").Trim();
                if (x == null || typed.Length == 0) return;
                // shown as the HMI shows it: typed so too (times the divisor); else as the PLC holds it
                WriteValue(mon, x, typed, "typed in the table", calculated: !b.Head.TableRaw && IsNumber(x));
            };
            g.CellEndEdit += (s, e) => { sh.Editing = false; g.InvalidateCell(e.ColumnIndex, e.RowIndex); };
            g.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex <= 0) return;
                g.CurrentCell = g.Rows[e.RowIndex].Cells[e.ColumnIndex];
                g.BeginEdit(true);
            };
            g.DataError += (s, e) => { e.ThrowException = false; };

            // the column's label: edited, taken off the table; the table's own choices
            var menu = NewMenu();
            menu.Opening += (s, e) =>
            {
                menu.Items.Clear();
                var at = g.PointToClient(Cursor.Position);
                var hit = g.HitTest(at.X, at.Y);
                var c = hit.ColumnIndex > 0 ? g.Columns[hit.ColumnIndex].Tag as TableColumn : null;
                var label = c != null ? c.Source : b.Head;
                menu.Items.Add(Item(string.Format(T("Mnu_MonTableEdit", "Edit '{0}'..."), NameOf(label)), (s2, e2) => EditRow(mon, sh, label, "Label")));
                menu.Items.Add(Item(string.Format(T("Mnu_MonTableRemove", "Take '{0}' off the table"), NameOf(label)), (s2, e2) => TakeOffTable(mon, sh, label)));
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(Item(T("Btn_MonTableAdd", "Add an array..."), (s2, e2) => ShowMenu(b.Add, TableAddMenu(mon, sh, b.Head))));
                menu.Items.Add(Item(b.Head.TableRaw ? T("Mnu_MonTableHmi", "Show the values as the HMI shows them") : T("Mnu_MonTablePlc", "Show the values as the PLC holds them"),
                                    (s2, e2) => (b.Head.TableRaw ? b.ShowHmi : b.ShowPlc).PerformClick()));
                menu.Items.Add(Item(T("Btn_MonTableList", "List view"), (s2, e2) => TableToList(mon, sh, b.Head)));
                e.Cancel = false;
            };
            g.ContextMenuStrip = menu;
            b.Caption.ContextMenuStrip = menu;
            return g;
        }

        /// <summary>
        /// What can be joined to a table: the labels of its length in the tab's list (moved), on this
        /// controller's other tabs (copied), in the label list, or a new one (the device editor, its length set).
        /// </summary>
        private ContextMenuStrip TableAddMenu(MonitorTab mon, Sheet sh, WatchRow head)
        {
            var m = NewMenu();
            var group = TableGroups(sh).FirstOrDefault(gr => gr.Contains(head)) ?? new List<WatchRow> { head };
            int count = group[0].EffectiveCount;
            var mine = sh.AllRows.Where(r => !r.IsElement && r.IsDevice).ToList();
            Func<string, string, bool> taken = (address, type) => mine.Any(r => !sh.Rows.Contains(r) &&
                string.Equals(r.Address, address, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(StructuredTypes.BaseName(r.EffectiveDataType), StructuredTypes.BaseName(type), StringComparison.OrdinalIgnoreCase));
            var offers = new List<KeyValuePair<string, Func<WatchRow>>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // the tab's own list: moved into the table
            foreach (var r in Items(sh).Where(r => r.IsArray && r.EffectiveCount == count))
            {
                var from = r;
                seen.Add(r.Address + "|" + StructuredTypes.BaseName(r.EffectiveDataType));
                offers.Add(new KeyValuePair<string, Func<WatchRow>>(NameOf(r) + "   " + r.Address + "   " + StructuredTypes.BaseName(r.EffectiveDataType) +
                                                                     " [" + r.EffectiveCount + "]   (" + T("Lbl_MonThisList", "this tab's list") + ")", () => from));
            }
            foreach (var other in mon.Sheets.Where(s => s != sh))
                foreach (var r in other.AllRows.Where(r => !r.IsElement && r.IsArray && r.EffectiveCount == count))
                {
                    string type = StructuredTypes.BaseName(r.EffectiveDataType);
                    if (taken(r.Address, type) || !seen.Add(r.Address + "|" + type)) continue;
                    var from = r;
                    offers.Add(new KeyValuePair<string, Func<WatchRow>>(
                        NameOf(r) + "   " + r.Address + "   " + type + " [" + r.EffectiveCount + "]   (" + other.Name + ")",
                        () => { var c = from.Clone(); if (c.LabelOverride.Length == 0) c.LabelOverride = from.EffectiveLabel; return c; }));
                }
            foreach (var e in PlcLabelTable.EntriesFor(mon.Station))
            {
                int n = StructuredTypes.CountIn(e.DataType);
                if (n != count) continue;
                string type = StructuredTypes.BaseName(e.DataType);
                if (taken(e.Address, type) || !seen.Add(e.Address + "|" + type)) continue;
                var entry = e;
                offers.Add(new KeyValuePair<string, Func<WatchRow>>(
                    e.Label + "   " + e.Address + "   " + type + " [" + n + "]   (" + T("Lbl_MonLabelList", "label list") + ")",
                    () => FromLabelList(mon, entry)));
            }
            m.Items.Add(new ToolStripMenuItem(string.Format(T("Mnu_MonTableArraysOf", "Labels of {0} elements:"), count)) { Enabled = false });
            if (offers.Count == 0)
                m.Items.Add(new ToolStripMenuItem(T("Mnu_MonTableNone", "(none in the lists or in the label list)")) { Enabled = false });
            foreach (var o in offers.Take(40))
            {
                var make = o.Value;
                m.Items.Add(Item(o.Key, (s, e) => JoinTable(mon, sh, head, make())));
            }
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Item(string.Format(T("Mnu_MonTableNew", "New array of {0}..."), count), (s, e) => NewTableArray(mon, sh, head, count)));
            return m;
        }

        /// <summary>A row for a label of the label list - its own label kept (two labels may share an address).</summary>
        internal static WatchRow FromLabelList(MonitorTab mon, PlcLabelTable.Entry entry)
        {
            var r = new WatchRow
            {
                Station = mon.Station, Address = (entry.Address ?? "").Trim().ToUpperInvariant(),
                DataTypeOverride = StructuredTypes.BaseName(entry.DataType), ArrayCount = Math.Max(1, StructuredTypes.CountIn(entry.DataType))
            };
            var first = PlcLabelTable.ByAddress(mon.Station, r.Address);
            if (first == null || !string.Equals(first.Label, entry.Label, StringComparison.Ordinal)) r.LabelOverride = entry.Label;
            return r;
        }

        /// <summary>A new array typed in the device editor - its length the table's - joined to the table.</summary>
        private void NewTableArray(MonitorTab mon, Sheet sh, WatchRow head, int count)
        {
            using (var dlg = new PlcRowEditorForm(PlcEndpointNaming.Caption(mon.Endpoint), "", "Word [Signed]", "", "", "Address", isNew: true,
                                                  arrayCount: Math.Max(2, count), station: mon.Station))
            {
                if (dlg.ShowDialog(DialogOwner) != DialogResult.OK || dlg.IsComment) return;
                var r = new WatchRow
                {
                    Station = mon.Station, Address = dlg.RowAddress, ArrayCount = dlg.ArrayCount,
                    DisplayFormat = dlg.RowDisplayFormat == FormatDecimal ? "" : dlg.RowDisplayFormat,
                    StringLength = dlg.RowStringLength, Divisor = dlg.RowDivisor, Decimals = dlg.RowDecimals
                };
                ApplyNames(mon, r, dlg.RowLabel.Length == 0 ? (PlcLabelTable.ByAddress(mon.Station, r.Address) ?? new PlcLabelTable.Entry()).Label : dlg.RowLabel,
                           dlg.RowDataType);
                JoinTable(mon, sh, head, r);
            }
        }
    }
}
