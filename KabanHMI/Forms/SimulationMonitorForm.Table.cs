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
            /// <summary>A comment line between the tables (24 October): its caption only.</summary>
            public bool IsComment;
            /// <summary>The texts of the cells in sight when last drawn (a refresh that changes none draws nothing).</summary>
            public string Drawn;
        }

        // ---- the model ------------------------------------------------------------------

        /// <summary>
        /// The "Table viewed labels" tab's items in their order (24 October: "It should be possible to add comment or
        /// another table viewed label in between before/after any label on the tab"): a comment line (one row), or a
        /// table - its label and the labels joined to it.
        /// </summary>
        internal static List<List<WatchRow>> TableItems(Sheet sh)
        {
            EnsureElements(sh.Tables);
            var items = new List<List<WatchRow>>();
            foreach (var r in Items(sh.Tables))
            {
                if (r.IsComment) { items.Add(new List<WatchRow> { r }); continue; }
                if (!r.IsDevice) continue;
                if (r.Joined && items.Count > 0 && !items[items.Count - 1][0].IsComment) items[items.Count - 1].Add(r);
                else items.Add(new List<WatchRow> { r });
            }
            return items;
        }

        /// <summary>The tab's tables: each a label and the labels joined to it, in their order.</summary>
        internal static List<List<WatchRow>> TableGroups(Sheet sh)
        {
            return TableItems(sh).Where(i => !i[0].IsComment).ToList();
        }

        /// <summary>The tables' tab made of these items, in this order (a table's own label first, its joined ones after it).</summary>
        private static void SetTableItems(Sheet sh, IEnumerable<List<WatchRow>> items)
        {
            sh.Tables.Rows.Clear();
            foreach (var it in items)
            {
                for (int k = 0; k < it.Count; k++) if (it[k].IsDevice) it[k].Joined = k > 0;
                sh.Tables.Rows.AddRange(it);
            }
            EnsureElements(sh.Tables);
        }

        /// <summary>The tables' tab is the one in sight (on the tab itself, or its "Table viewed labels" tab chosen).</summary>
        internal static bool TablesShown(Sheet sh)
        {
            if (sh == null || sh.TablesHost == null) return false;
            return sh.ViewMode == 1 || (sh.ViewMode == 2 && sh.ViewTabs != null && sh.ViewTabs.SelectedTab == sh.TablesPage);
        }

        /// <summary>The item chosen on the tables' tab (null: none) and where it is among them.</summary>
        internal static List<WatchRow> SelectedTableItem(Sheet sh, out int index)
        {
            index = -1;
            if (sh == null || sh.TableSelected == null) return null;
            var items = TableItems(sh);
            index = items.FindIndex(i => i.Contains(sh.TableSelected));
            return index >= 0 ? items[index] : null;
        }

        /// <summary>The chosen item can move up (-1) / down (+1) among the tables' tab's items.</summary>
        internal static bool CanMoveTableItem(Sheet sh, int by)
        {
            int i;
            var item = SelectedTableItem(sh, out i);
            return item != null && i + by >= 0 && i + by < TableItems(sh).Count;
        }

        /// <summary>A table's columns after "#": each label's members (a structure's), or the label itself.</summary>
        internal static List<TableColumn> TableColumnsOf(List<WatchRow> group)
        {
            var cols = new List<TableColumn>();
            for (int i = 0; i < group.Count; i++)
            {
                var a = group[i];
                string label = NameOf(a);
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
        private static string NameOf(WatchRow r) { return r.EffectiveLabel.Length > 0 ? r.ShownLabel : r.Address; }

        // ---- moving labels between the list and the tables -------------------------------

        /// <summary>
        /// "Show this label in table view": the label off the list, a table at the end of the tables - or (24 October)
        /// above (-1) or below (+1) the table chosen on the tables' tab.
        /// </summary>
        internal void ShowInTable(MonitorTab mon, Sheet sh, WatchRow label, int where = 0)
        {
            if (mon == null || sh == null || label == null || !label.IsArray || !sh.Rows.Contains(label)) return;
            Remember(string.Format(T("Undo_ShowInTable", "'{0}' in table view"), NameOf(label)), mon);
            sh.Rows.Remove(label);
            label.Joined = false;
            PutOnTables(sh, new List<List<WatchRow>> { new List<WatchRow> { label } }, where);
            sh.Grid.ClearSelection();
            sh.WantView = "tables";                       // its table in sight
            EnsureBlankRow(sh);
            SaveWatchList(mon);
            Render(mon, sh);
        }

        /// <summary>Items put on the tables' tab: above (-1) / below (+1) the one chosen, or at the end (0, or none chosen); the first chosen.</summary>
        private static void PutOnTables(Sheet sh, List<List<WatchRow>> made, int where)
        {
            if (made.Count == 0) return;
            var items = TableItems(sh);
            int sel = sh.TableSelected == null ? -1 : items.FindIndex(i => i.Contains(sh.TableSelected));
            int at = where == 0 || sel < 0 ? items.Count : where < 0 ? sel : sel + 1;
            items.InsertRange(at, made);
            SetTableItems(sh, items);
            sh.TableSelected = made[0][0];
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

        /// <summary>A table (or a comment line) up or down among the tables' tab's items (the list stays under them).</summary>
        internal void MoveTable(MonitorTab mon, Sheet sh, WatchRow head, int by)
        {
            var items = TableItems(sh);
            int i = items.FindIndex(g => g.Contains(head)), j = i + by;
            if (i < 0 || j < 0 || j >= items.Count) return;
            Remember(items[i][0].IsComment ? T("Undo_MoveComment", "move a comment line") : T("Undo_MoveTable", "move a table"), mon);
            var g0 = items[i];
            items[i] = items[j];
            items[j] = g0;
            SetTableItems(sh, items);
            sh.TableSelected = g0[0];
            SaveWatchList(mon);
            Render(mon, sh);
            UpdateToolbar();
        }

        // ---- the toolbar on the tables' tab (24 October: "The labels in 'Table viewed labels' tab doesn't move up/down on
        // PLC Monitor page for some reason and both of these up/down button look enabled even when the first or last
        // label selected on the tab. Other buttons at this page functions on 'List viewed labels' tab as well actually,
        // this tab doesn't function very well") ----------------------------------------------------------------------

        /// <summary>A table or a comment line chosen on the tables' tab: marked, the toolbar's buttons as it allows.</summary>
        internal void SelectTable(Sheet sh, WatchRow item)
        {
            if (sh == null) return;
            if (sh.TableSelected != item)
            {
                sh.TableSelected = item;
                PaintTableSelection(sh);
            }
            UpdateToolbar();
        }

        private static readonly Color TableChosenBack = Color.FromArgb(222, 234, 250);

        /// <summary>The chosen table's (comment line's) caption marked, the others not.</summary>
        private static void PaintTableSelection(Sheet sh)
        {
            foreach (var b in sh.Blocks)
            {
                bool on = b.Head == sh.TableSelected;
                var back = on ? TableChosenBack : b.IsComment ? Color.FromArgb(246, 247, 249) : Color.White;
                if (b.Caption.BackColor != back) b.Caption.BackColor = back;
                if (b.Bar != null && b.Bar.BackColor != back) b.Bar.BackColor = back;
            }
        }

        /// <summary>The chosen table (with the labels joined to it) or comment line taken off the tab - undone with Ctrl+Z.</summary>
        internal void RemoveTableItem(MonitorTab mon, Sheet sh)
        {
            int i;
            var item = SelectedTableItem(sh, out i);
            if (item == null) return;
            Remember(item[0].IsComment ? T("Undo_RemoveComment", "remove a comment line")
                                       : string.Format(T("Undo_RemoveTable", "remove the table '{0}'"), NameOf(item[0])), mon);
            var items = TableItems(sh);
            items.RemoveAt(i);
            SetTableItems(sh, items);
            foreach (var r in item.Where(r => r.IsDevice))
                foreach (var a in r.IsGroup ? GroupWords(r) : new[] { r.Address }) HideIfUnlisted(mon, a, null, -1);
            sh.TableSelected = items.Count == 0 ? null : items[Math.Min(i, items.Count - 1)][0];
            SaveWatchList(mon);
            Render(mon, sh);
            UpdateToolbar();
        }

        /// <summary>
        /// A new table (an array typed in the device editor - its own length) or a comment line on the tables' tab:
        /// above (-1) / below (+1) the one chosen, or at the end (0). A single device typed goes to the list (said).
        /// </summary>
        internal void AddTableItem(MonitorTab mon, Sheet sh, bool comment, int where)
        {
            if (mon == null || sh == null) return;
            using (var dlg = new PlcRowEditorForm(PlcEndpointNaming.Caption(mon.Endpoint), "", "Word [Signed]", "", "", comment ? "Label" : "Address",
                                                  isNew: true, isComment: comment, arrayCount: comment ? 1 : 10, station: mon.Station))
            {
                if (dlg.ShowDialog(DialogOwner) != DialogResult.OK) return;
                AddTableItem(mon, sh, where, dlg.IsComment, dlg.RowLabel, dlg.RowAddress, dlg.RowDataType, dlg.ArrayCount,
                             dlg.RowDisplayFormat, dlg.RowStringLength, dlg.RowDivisor, dlg.RowDecimals);
            }
        }

        /// <summary>What the device editor gave, put on the tables' tab (tests call it with the editor's answers).</summary>
        internal void AddTableItem(MonitorTab mon, Sheet sh, int where, bool comment, string label, string address, string dataType, int count,
                                   string format = "", int stringLength = 16, double divisor = 1, int decimals = 0)
        {
            WatchRow r;
            if (comment) r = new WatchRow { Station = mon.Station, Comment = (label ?? "").Trim() };
            else
            {
                r = new WatchRow
                {
                    Station = mon.Station, Address = (address ?? "").Trim().ToUpperInvariant(), ArrayCount = Math.Max(1, count),
                    DisplayFormat = format == FormatDecimal ? "" : format ?? "", StringLength = stringLength, Divisor = divisor, Decimals = decimals
                };
                ApplyNames(mon, r, string.IsNullOrEmpty(label) ? (PlcLabelTable.ByAddress(mon.Station, r.Address) ?? new PlcLabelTable.Entry()).Label : label, dataType);
            }
            if (!r.IsComment && !r.IsArray)
            {
                // one device is no table: it goes in the list
                Remember(T("Undo_AddDevice", "add a device"), mon);
                sh.Rows.Insert(InsertPoint(sh, true), r);
                EnsureBlankRow(sh);
                SaveWatchList(mon);
                Render(mon, sh);
                SetStatus(string.Format(T("Msg_MonTableOne", "{0} is a single device, not an array - it is in the tab's list."), NameOf(r)));
                return;
            }
            Remember(r.IsComment ? T("Undo_AddComment", "add a comment line") : string.Format(T("Undo_AddTable", "add the table '{0}'"), NameOf(r)), mon);
            if (r.IsDevice)
                foreach (var a in GroupWords(Expanded(r))) mon.Hidden.Remove(a);
            PutOnTables(sh, new List<List<WatchRow>> { new List<WatchRow> { r } }, where);
            sh.WantView = "tables";
            SaveWatchList(mon);
            Render(mon, sh);
            UpdateToolbar();
        }

        /// <summary>A row with its elements made (for its devices).</summary>
        private static WatchRow Expanded(WatchRow r)
        {
            var tmp = new Sheet();
            tmp.Rows.Add(r);
            EnsureElements(tmp);
            return r;
        }

        /// <summary>A comment line of the tables' tab typed again.</summary>
        internal void EditTableComment(MonitorTab mon, Sheet sh, WatchRow comment)
        {
            if (comment == null || !comment.IsComment) return;
            using (var dlg = new PlcRowEditorForm(PlcEndpointNaming.Caption(mon.Endpoint), comment.Comment, "", "", "", "Label", isComment: true, station: mon.Station))
            {
                if (dlg.ShowDialog(DialogOwner) != DialogResult.OK || dlg.RowLabel.Trim().Length == 0) return;
                Remember(T("Undo_EditComment", "edit a comment line"), mon);
                comment.Comment = dlg.RowLabel.Trim();
                SaveWatchList(mon);
                Render(mon, sh);
            }
        }

        /// <summary>The chosen table (its labels, as joined) or comment line copied - pasted on the tables' tab or into a list.</summary>
        internal void CopyTableItem(MonitorTab mon, Sheet sh)
        {
            int i;
            var item = SelectedTableItem(sh, out i);
            if (item == null) return;
            _clip.Clear();
            var text = new System.Text.StringBuilder();
            foreach (var r in item)
            {
                var copy = r.Clone();
                if (copy.IsDevice) { copy.LabelOverride = r.EffectiveLabel; copy.DataTypeOverride = r.EffectiveDataType; }
                copy.Joined = r.Joined;
                copy.TableRaw = r.TableRaw;
                _clip.Add(copy);
                text.AppendLine(copy.IsComment ? Csv(copy.Comment) + ";;"
                                               : string.Join(";", Csv(copy.LabelOverride), Csv(copy.DataTypeOverride), Csv(copy.Address)));
            }
            _clipText = text.ToString();
            try { Clipboard.SetText(_clipText); } catch { }
            SetStatus(item[0].IsComment ? T("Msg_MonCommentCopied", "The comment line copied.")
                                        : string.Format(T("Msg_MonTableCopied", "The table '{0}' copied ({1} label(s))."), NameOf(item[0]), item.Count));
        }

        /// <summary>
        /// The rows copied put on the tables' tab: above (-1) / below (+1) the one chosen, or at the end (0) - an array a
        /// table (one joined to a table copied with it: joined again), a comment line a comment line; a single device
        /// goes in the list (said).
        /// </summary>
        internal void PasteTables(MonitorTab mon, Sheet sh, int where)
        {
            List<WatchRow> rows;
            string text = "";
            try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch { }
            if (_clip.Count > 0 && (text.Length == 0 || text == _clipText))
                rows = _clip.Select(r => { var c = r.Clone(); c.Joined = r.Joined; c.TableRaw = r.TableRaw; return c; }).ToList();
            else rows = ParseLines(text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));
            if (rows.Count == 0) return;
            Remember(T("Undo_Paste", "paste"), mon);
            var made = new List<List<WatchRow>>();
            var toList = new List<WatchRow>();
            foreach (var r in rows)
            {
                r.Station = mon.Station;
                if (r.IsDevice) { mon.Hidden.Remove(r.Address); ApplyNames(mon, r, r.LabelOverride, r.DataTypeOverride); }
                if (r.IsComment) { r.Joined = false; made.Add(new List<WatchRow> { r }); continue; }
                if (!r.IsArray) { r.Joined = false; toList.Add(r); continue; }
                var last = made.Count > 0 ? made[made.Count - 1] : null;
                if (r.Joined && last != null && !last[0].IsComment && last[0].EffectiveCount == r.EffectiveCount) last.Add(r);
                else { r.Joined = false; made.Add(new List<WatchRow> { r }); }
            }
            PutOnTables(sh, made, where);
            if (toList.Count > 0)
            {
                sh.Rows.InsertRange(InsertPoint(sh, true), toList);
                EnsureBlankRow(sh);
            }
            SaveWatchList(mon);
            Render(mon, sh);
            UpdateToolbar();
            SetStatus(toList.Count == 0 ? string.Format(T("Msg_MonPasted", "{0} row(s) pasted."), rows.Count)
                                        : string.Format(T("Msg_MonPastedTables", "{0} row(s) pasted - {1} single device(s) (no array) in the tab's list."), rows.Count, toList.Count));
        }

        /// <summary>
        /// What the toolbar's Add offers on the tables' tab: a new table or a comment line above / below the one chosen or
        /// at the end, a label of the tab's list as a table there.
        /// </summary>
        internal void AddTableMenuItems(ToolStripItemCollection items, MonitorTab mon, Sheet sh)
        {
            int i;
            bool chosen = SelectedTableItem(sh, out i) != null;
            items.Add(Item(T("Mnu_MonTableNewAbove", "New table above the chosen one..."), (s, e) => AddTableItem(mon, sh, false, -1), chosen, keyText: "Ins"));
            items.Add(Item(T("Mnu_MonTableNewBelow", "New table below the chosen one..."), (s, e) => AddTableItem(mon, sh, false, +1), chosen));
            items.Add(Item(T("Mnu_MonTableNewEnd", "New table at the end..."), (s, e) => AddTableItem(mon, sh, false, 0)));
            items.Add(new ToolStripSeparator());
            items.Add(Item(T("Mnu_MonTableCommentAbove", "Comment line above the chosen one..."), (s, e) => AddTableItem(mon, sh, true, -1), chosen));
            items.Add(Item(T("Mnu_MonTableCommentBelow", "Comment line below the chosen one..."), (s, e) => AddTableItem(mon, sh, true, +1), chosen));
            items.Add(Item(T("Mnu_MonTableCommentEnd", "Comment line at the end..."), (s, e) => AddTableItem(mon, sh, true, 0)));
            items.Add(new ToolStripSeparator());
            // an array of the tab's list made a table here
            var fromList = new ToolStripMenuItem(T("Mnu_MonTableFromList", "A label of this tab's list as a table"));
            var arrays = Items(sh).Where(r => r.IsArray).ToList();
            foreach (var a in arrays.Take(40))
            {
                var label = a;
                fromList.DropDownItems.Add(Item(NameOf(a) + "   " + a.Address + " [" + a.EffectiveCount + "]",
                                                (s, e) => ShowInTable(mon, sh, label, chosen ? +1 : 0)));
            }
            fromList.Enabled = arrays.Count > 0;
            if (arrays.Count == 0) fromList.ToolTipText = T("Tip_MonTableFromListNone", "The tab's list has no array label.");
            items.Add(fromList);
        }

        /// <summary>The entries every item's menu has: move, put something next to it, copy, paste, delete.</summary>
        private void AddItemEntries(ToolStripItemCollection items, MonitorTab mon, Sheet sh, WatchRow head)
        {
            var all = TableItems(sh);
            int i = all.FindIndex(x => x.Contains(head));
            bool any = _clip.Count > 0 || ClipboardHasText();
            items.Add(new ToolStripSeparator());
            items.Add(Item(T("Mnu_MonTableUp", "Move up"), (s, e) => MoveTable(mon, sh, head, -1), i > 0, keyText: "Ctrl+Up"));
            items.Add(Item(T("Mnu_MonTableDown", "Move down"), (s, e) => MoveTable(mon, sh, head, +1), i >= 0 && i < all.Count - 1, keyText: "Ctrl+Down"));
            items.Add(new ToolStripSeparator());
            items.Add(Item(T("Mnu_MonTableNewAbove", "New table above the chosen one..."), (s, e) => { SelectTable(sh, head); AddTableItem(mon, sh, false, -1); }));
            items.Add(Item(T("Mnu_MonTableNewBelow", "New table below the chosen one..."), (s, e) => { SelectTable(sh, head); AddTableItem(mon, sh, false, +1); }));
            items.Add(Item(T("Mnu_MonTableCommentAbove", "Comment line above the chosen one..."), (s, e) => { SelectTable(sh, head); AddTableItem(mon, sh, true, -1); }));
            items.Add(Item(T("Mnu_MonTableCommentBelow", "Comment line below the chosen one..."), (s, e) => { SelectTable(sh, head); AddTableItem(mon, sh, true, +1); }));
            items.Add(new ToolStripSeparator());
            items.Add(Item(T("Mnu_MonCopy", "Copy"), (s, e) => { SelectTable(sh, head); CopyTableItem(mon, sh); }, true, keyText: "Ctrl+C"));
            items.Add(Item(T("Mnu_MonTablePasteAbove", "Paste above the chosen one"), (s, e) => { SelectTable(sh, head); PasteTables(mon, sh, -1); }, any, keyText: "Ctrl+V"));
            items.Add(Item(T("Mnu_MonTablePasteBelow", "Paste below the chosen one"), (s, e) => { SelectTable(sh, head); PasteTables(mon, sh, +1); }, any));
            items.Add(new ToolStripSeparator());
            items.Add(Item(head.IsComment ? T("Mnu_MonCommentRemove", "Delete this comment line") : T("Mnu_MonTableDelete", "Delete this table"),
                           (s, e) => { SelectTable(sh, head); RemoveTableItem(mon, sh); }, true, keyText: "Del"));
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
        /// scrolled the tables, its caption out of sight (22 October: the caption cut at its top).
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
            var items = TableItems(sh);
            if (sh.TableSelected != null && !items.Any(i => i.Contains(sh.TableSelected))) sh.TableSelected = null;
            string key = string.Join(";", items.Select(g => g[0].IsComment ? "#c:" + g[0].Comment : string.Join("+", g.Select(r =>
                r.Address + "~" + r.EffectiveDataType + "~" + r.EffectiveCount + "~" + NameOf(r) + "~" + r.TableRaw))));
            if (key != sh.TablesKey && !sh.TableEditing)
            {
                sh.TablesKey = key;
                foreach (var b in sh.Blocks) { sh.TablesHost.Controls.Remove(b.Panel); b.Panel.Dispose(); }
                sh.Blocks.Clear();
                for (int i = 0; i < items.Count; i++)
                    sh.Blocks.Add(items[i][0].IsComment ? BuildCommentBlock(mon, sh, items[i][0]) : BuildBlock(mon, sh, items[i], i, items.Count));
                foreach (var b in sh.Blocks) sh.TablesHost.Controls.Add(b.Panel);
                LayoutTables(sh);
            }
            PaintTableSelection(sh);
            ArrangeViews(sh);
            // 25 October ("The PLC monitor table blinks sometimes ... when the scroll is moved to the right end of the
            // table"): each refresh (4 a second) drew every table whole - now a table is drawn again only when a cell in
            // sight shows something else, and into a buffer (no flicker)
            foreach (var b in sh.Blocks)
            {
                if (b.Grid == null || b.Grid.IsCurrentCellInEditMode) continue;
                string now = ShownTexts(mon, b);
                if (now == b.Drawn) continue;
                b.Drawn = now;
                b.Grid.Invalidate();
            }
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
            if (b.IsComment) return b.Caption.Height;
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
            UiStyle.SetTip(_tips, b.Caption, string.Join("\r\n", group.Select(r => (r.EffectiveLabel.Length > 0 ? r.EffectiveLabel : r.Address) + "   " + r.Address + "   " +
                                                                                   r.EffectiveDataType + " [" + r.EffectiveCount + "]")));

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
            b.Up.Enabled = index > 0;                       // among the tab's items, comment lines too
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
            // a click anywhere on it (its caption, its bar, a cell) chooses it for the toolbar
            MouseEventHandler choose = (s, e) => SelectTable(sh, b.Head);
            foreach (Control c in new Control[] { b.Panel, b.Caption, bar, b.Note }) c.MouseDown += choose;
            b.Grid.Enter += (s, e) => SelectTable(sh, b.Head);
            b.Grid.CellMouseDown += (s, e) => SelectTable(sh, b.Head);
            return b;
        }

        /// <summary>A comment line between the tables: its text as a heading; a double click types it again.</summary>
        private TableBlock BuildCommentBlock(MonitorTab mon, Sheet sh, WatchRow comment)
        {
            var b = new TableBlock { Head = comment, IsComment = true };
            b.Sources.Add(comment);
            b.Panel = new Panel { BackColor = Color.White, Name = "tableComment" };
            var font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold | FontStyle.Italic);
            b.Caption = new Label
            {
                Dock = DockStyle.Fill, AutoEllipsis = true, Padding = new Padding(8, 4, 6, 4), Font = font, Name = "tableCommentText",
                ForeColor = Color.FromArgb(60, 66, 76), BackColor = Color.FromArgb(246, 247, 249), TextAlign = ContentAlignment.MiddleLeft,
                Height = TextRenderer.MeasureText("Xg", font).Height + 14, Text = comment.Comment, UseMnemonic = false
            };
            b.Panel.Controls.Add(b.Caption);
            b.Caption.MouseDown += (s, e) => SelectTable(sh, comment);
            b.Caption.DoubleClick += (s, e) => EditTableComment(mon, sh, comment);
            var menu = NewMenu();
            menu.Opening += (s, e) =>
            {
                SelectTable(sh, comment);
                menu.Items.Clear();
                menu.Items.Add(Item(T("Mnu_MonCommentEdit", "Edit the comment line..."), (s2, e2) => EditTableComment(mon, sh, comment)));
                AddItemEntries(menu.Items, mon, sh, comment);
                e.Cancel = false;
            };
            b.Caption.ContextMenuStrip = menu;
            return b;
        }

        /// <summary>What the cells of a table in sight show now (and its view: HMI / PLC, where it is scrolled).</summary>
        private static string ShownTexts(MonitorTab mon, TableBlock b)
        {
            var g = b.Grid;
            if (g.RowCount == 0) return "";
            int top = Math.Max(0, g.FirstDisplayedScrollingRowIndex), n = Math.Max(1, g.DisplayedRowCount(true));
            var sb = new System.Text.StringBuilder(b.Head.TableRaw ? "P" : "H");
            for (int r = top; r < Math.Min(g.RowCount, top + n); r++)
                for (int c = 1; c < g.Columns.Count; c++)
                {
                    if (!g.Columns[c].Visible) continue;
                    bool nz;
                    sb.Append(TableText(mon, b.Head.TableRaw, g.Columns[c].Tag as TableColumn, r, out nz)).Append(nz ? '\u0002' : '\u0001');
                }
            return sb.ToString();
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
            // drawn into a buffer: no flicker when it is drawn again (25 October)
            try { typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(g, true, null); } catch { }
            // scrolled: the cells now in sight drawn (and remembered as drawn)
            g.Scroll += (s, e) => b.Drawn = null;
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
                SelectTable(sh, b.Head);
                AddItemEntries(menu.Items, mon, sh, b.Head);
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
            // 24 October: "should disable 'Arrays (how' value edit because this value has to be just same as the label to be attached"
            using (var dlg = new PlcRowEditorForm(PlcEndpointNaming.Caption(mon.Endpoint), "", "Word [Signed]", "", "", "Address", isNew: true,
                                                  arrayCount: Math.Max(2, count), station: mon.Station, lockArrayCount: true))
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
