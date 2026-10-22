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
    // THE TABLE VIEW (22 October: "create a 2-dimensional table view for the structural array
    // data type monitor (or same-dimensional multiple array labels). The structure members are
    // the columns (the data type attached to the label name as the column header), so the table
    // has as many lines as the array has elements and the whole recipe is seen just like an Excel
    // table - like '<> KESİM ADIMLARI' on the 'Profiles & Operations' sheet. A button changes the
    // values shown (the ones calculated with the divisor or the pure ones). Columns can be added
    // at the end as new labels of the same length, to watch two arrays together - the 'İŞ LİSTESİ'
    // kind, where the same index of each label belongs together").
    //
    // A TAB SHOWN AS A TABLE: its list stays what it was - the arrays are its rows, kept, read,
    // undone and exported as ever - and the table shows them side by side: a line per index, a
    // column per member of an array of structures, one for an array of a simple type. The
    // arrays as long as the tab's first are in it; another length is named under the table.
    // ======================================================================
    public partial class SimulationMonitorForm
    {
        private const string TablePrefix = "#table:";

        /// <summary>A column of a table: an array's elements, or one member of each of them.</summary>
        internal sealed class TableColumn
        {
            /// <summary>The array (a row of the tab).</summary>
            public WatchRow Source;
            /// <summary>The structure's member (-1: the element itself, an array of a simple type).</summary>
            public int Member = -1;
            public string Header = "";
            public string DataType = "";
        }

        /// <summary>
        /// The arrays of a tab in its table: as long as its first array (count); the arrays of
        /// another length in left.
        /// </summary>
        internal static List<WatchRow> TableSources(Sheet sh, out int count, out List<WatchRow> left)
        {
            EnsureElements(sh);
            var arrays = sh.Rows.Where(r => !r.IsElement && r.IsArray).ToList();
            count = arrays.Count > 0 ? arrays[0].EffectiveCount : 0;
            int n = count;
            left = arrays.Where(a => a.EffectiveCount != n).ToList();
            return arrays.Where(a => a.EffectiveCount == n).ToList();
        }

        /// <summary>The table's columns after "#": each array's members (a structure's), or the array itself.</summary>
        internal static List<TableColumn> TableColumnsOf(Sheet sh, out int count, out List<WatchRow> left)
        {
            var sources = TableSources(sh, out count, out left);
            var cols = new List<TableColumn>();
            foreach (var a in sources)
            {
                string label = a.EffectiveLabel.Length > 0 ? a.EffectiveLabel : a.Address;
                string type = StructuredTypes.BaseName(a.EffectiveDataType);
                var st = StructuredTypes.Find(type, a.Station);
                if (st != null)
                {
                    // one array of structures: its members' names; with others: "Recipe.X"
                    for (int j = 0; j < st.Members.Count; j++)
                        cols.Add(new TableColumn { Source = a, Member = j, DataType = st.Members[j].DataType,
                                                   Header = (sources.Count > 1 ? label + "." : "") + st.Members[j].Name });
                }
                else cols.Add(new TableColumn { Source = a, DataType = type, Header = label });
            }
            return cols;
        }

        /// <summary>The row a cell shows: element index of its array (and its member) - null when there is none.</summary>
        internal static WatchRow TableCellRow(TableColumn c, int index)
        {
            if (c == null || c.Source == null || c.Source.Elements == null || index < 0 || index >= c.Source.Elements.Count) return null;
            var e = c.Source.Elements[index];
            if (c.Member < 0) return e;
            return e.Members != null && c.Member < e.Members.Count ? e.Members[c.Member] : null;
        }

        /// <summary>A cell's text: calculated (a number's) or as the PLC holds it; "-" not read yet.</summary>
        internal static string TableText(MonitorTab mon, Sheet sh, TableColumn c, int index, out bool nonZero)
        {
            nonZero = false;
            var x = TableCellRow(c, index);
            if (x == null) return "";
            string text = !sh.TableRaw && HasCalculated(x) ? FormatCalculated(mon, x, out nonZero) : FormatValue(mon, x, out nonZero);
            return text ?? "-";
        }

        // ---- the view ---------------------------------------------------------------------

        /// <summary>The tab's page as its table or as its list.</summary>
        private void ApplyView(MonitorTab mon, Sheet sh)
        {
            if (sh.IsTable && sh.TableView == null) BuildTableView(mon, sh);
            if (sh.TableView != null) sh.TableView.Visible = sh.IsTable;
            sh.Grid.Visible = !sh.IsTable;
            if (sh.IsTable)
            {
                // the list's selection is not the table's: copy, remove and move find nothing chosen
                sh.Grid.ClearSelection();
                try { sh.Grid.CurrentCell = null; } catch { }
                sh.TableKey = null;
                if (sh.TableCalc != null) sh.TableCalc.Checked = !sh.TableRaw;
            }
        }

        /// <summary>A tab shown as its table, or as its list again - kept, undone like any change of the list.</summary>
        internal void ShowTable(MonitorTab mon, Sheet sh, bool on)
        {
            if (mon == null || sh == null || sh.IsTable == on) return;
            Remember(on ? T("Undo_TableView", "show the tab as a table") : T("Undo_ListView", "show the tab as a list"), mon);
            sh.IsTable = on;
            ApplyView(mon, sh);
            SaveWatchList(mon);
            Render(mon, sh);
        }

        /// <summary>An array in a new tab of its own, shown as a table.</summary>
        internal Sheet OpenAsTable(MonitorTab mon, WatchRow array)
        {
            if (mon == null || array == null || !array.IsArray) return null;
            string label = array.EffectiveLabel.Length > 0 ? array.EffectiveLabel : array.Address;
            Remember(string.Format(T("Undo_OpenTable", "'{0}' as a table"), label), mon);
            var sh = AddSheet(mon, UniqueName(mon, label), mon.SheetTabs.TabPages.Count);
            var copy = array.Clone();
            copy.Station = mon.Station;
            sh.Rows.Insert(InsertPoint(sh, true), copy);
            sh.IsTable = true;
            EnsureBlankRow(sh);
            ApplyView(mon, sh);
            SaveWatchList(mon);
            Render(mon, sh);
            return sh;
        }

        /// <summary>An array added at the end of the tab: its columns at the end of the table.</summary>
        internal void AddToTable(MonitorTab mon, Sheet sh, WatchRow row)
        {
            if (mon == null || sh == null || row == null) return;
            Remember(T("Undo_TableAdd", "add an array to the table"), mon);
            row.Station = mon.Station;
            sh.Rows.Insert(InsertPoint(sh, true), row);
            EnsureBlankRow(sh);
            SaveWatchList(mon);
            Render(mon, sh);
            int count; List<WatchRow> left;
            TableSources(sh, out count, out left);
            if (left.Contains(row))
                SetStatus(string.Format(T("Msg_MonTableOtherLength", "{0} has {1} elements, the table {2}: it is in the tab's list, not in the table."),
                                        row.EffectiveLabel.Length > 0 ? row.EffectiveLabel : row.Address, row.EffectiveCount, count));
        }

        /// <summary>An array taken off the tab (its columns off the table).</summary>
        internal void RemoveFromTable(MonitorTab mon, Sheet sh, WatchRow source)
        {
            if (mon == null || sh == null || source == null || !sh.Rows.Contains(source)) return;
            Remember(T("Undo_TableRemove", "take an array off the table"), mon);
            int at = sh.Rows.IndexOf(source);
            HideIfUnlisted(mon, source.Address, sh, at);
            sh.Rows.Remove(source);
            EnsureBlankRow(sh);
            SaveWatchList(mon);
            Render(mon, sh);
        }

        /// <summary>The table's bar (the values shown, an array added, the list) over its table.</summary>
        private void BuildTableView(MonitorTab mon, Sheet sh)
        {
            var view = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Visible = false };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(6, 6, 6, 2), WrapContents = false, BackColor = Color.White };
            // WHICH VALUES: two buttons side by side, the one shown dark (a pressed toggle reads poorly)
            var calc = new CheckBox { Checked = !sh.TableRaw, Visible = false };
            var showCalc = new Button { Text = T("Btn_MonTableCalc", "Calculated values"), AutoSize = true, Margin = new Padding(0, 0, 0, 0), FlatStyle = FlatStyle.Flat, Name = "tableCalc" };
            var showRaw = new Button { Text = T("Btn_MonTableRaw", "PLC values"), AutoSize = true, Margin = new Padding(0, 0, 10, 0), FlatStyle = FlatStyle.Flat, Name = "tableRaw" };
            UiStyle.SetTip(_tips, showCalc, T("Tip_MonTableCalc", "The values divided by their divisors, with their decimal places"));
            UiStyle.SetTip(_tips, showRaw, T("Tip_MonTableRaw", "The values as the PLC holds them"));
            Action paint = () =>
            {
                foreach (var b in new[] { showCalc, showRaw })
                {
                    bool on = (b == showCalc) == calc.Checked;
                    b.BackColor = on ? Color.FromArgb(60, 66, 76) : Color.White;
                    b.ForeColor = on ? Color.White : Color.FromArgb(60, 66, 76);
                    b.FlatAppearance.BorderColor = Color.FromArgb(60, 66, 76);
                    b.Cursor = Cursors.Hand;
                }
            };
            calc.CheckedChanged += (s, e) =>
            {
                paint();
                if (sh.TableRaw == !calc.Checked) return;
                sh.TableRaw = !calc.Checked;
                SaveWatchList(mon);
                if (sh.TableGrid != null) sh.TableGrid.Invalidate();
            };
            showCalc.Click += (s, e) => calc.Checked = true;
            showRaw.Click += (s, e) => calc.Checked = false;
            paint();
            var add = UiStyle.Round(new Button { Text = T("Btn_MonTableAdd", "Add an array..."), AutoSize = true, Margin = new Padding(0, 0, 6, 0) });
            UiStyle.SetTip(_tips, add, T("Tip_MonTableAdd", "Columns at the end of the table: an array as long as the table's"));
            add.Click += (s, e) => ShowMenu(add, TableAddMenu(mon, sh));
            var list = UiStyle.Round(new Button { Text = T("Btn_MonTableList", "List view"), AutoSize = true, Margin = new Padding(0, 0, 12, 0) });
            UiStyle.SetTip(_tips, list, T("Tip_MonTableList", "The tab as a list again (its arrays' rows)"));
            list.Click += (s, e) => ShowTable(mon, sh, false);
            var note = new Label { AutoSize = true, ForeColor = Color.FromArgb(90, 96, 106), Margin = new Padding(0, 6, 0, 0) };
            bar.Controls.Add(calc);
            bar.Controls.Add(showCalc);
            bar.Controls.Add(showRaw);
            bar.Controls.Add(add);
            bar.Controls.Add(list);
            bar.Controls.Add(note);

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
                ShowCellToolTips = true
            };
            UiStyle.GridLook(g);
            // the member's name over its data type, as the sheet's two-line headings
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            g.ColumnHeadersHeight = 2 * TextRenderer.MeasureText("Xg", g.ColumnHeadersDefaultCellStyle.Font ?? UiStyle.Body).Height + 16;
            g.DefaultCellStyle.Font = UiStyle.FontVariant(UiStyle.TableFont, FontStyle.Bold);
            g.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            g.CellValueNeeded += (s, e) =>
            {
                if (e.ColumnIndex == 0) { e.Value = e.RowIndex.ToString(CultureInfo.InvariantCulture); return; }
                var c = g.Columns[e.ColumnIndex].Tag as TableColumn;
                bool nz;
                e.Value = TableText(mon, sh, c, e.RowIndex, out nz);
            };
            g.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex <= 0) return;
                var c = g.Columns[e.ColumnIndex].Tag as TableColumn;
                bool nz;
                string text = TableText(mon, sh, c, e.RowIndex, out nz);
                e.CellStyle.ForeColor = text == "-" ? ColLabelUnknown : nz ? ColValueSet : ColValueClear;
            };
            g.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex <= 0) return;
                var c = g.Columns[e.ColumnIndex].Tag as TableColumn;
                var x = TableCellRow(c, e.RowIndex);
                if (x == null) return;
                bool nz;
                string raw = FormatValue(mon, x, out nz) ?? "-";
                string tip = x.EffectiveLabel + "   " + x.ShownAddress + "   " + x.EffectiveDataType;
                if (HasCalculated(x) && x.IsScaled)
                    tip += "\r\n" + string.Format(T("Tip_MonTableScaled", "PLC value {0} ÷ {1}, {2} decimal place(s): {3}"),
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
                // shown calculated: typed calculated (times the divisor); else as the PLC holds it
                WriteValue(mon, x, typed, "typed in the table", calculated: !sh.TableRaw && IsNumber(x));
            };
            g.CellEndEdit += (s, e) => { sh.Editing = false; g.InvalidateCell(e.ColumnIndex, e.RowIndex); };
            g.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex <= 0) return;
                g.CurrentCell = g.Rows[e.RowIndex].Cells[e.ColumnIndex];
                g.BeginEdit(true);
            };
            g.DataError += (s, e) => { e.ThrowException = false; };

            // the column's array: taken off the table, or the table's own choices
            var menu = NewMenu();
            menu.Opening += (s, e) =>
            {
                menu.Items.Clear();
                var hit = g.HitTest(g.PointToClient(Cursor.Position).X, g.PointToClient(Cursor.Position).Y);
                var c = hit.ColumnIndex > 0 ? g.Columns[hit.ColumnIndex].Tag as TableColumn : null;
                if (c != null)
                {
                    string label = c.Source.EffectiveLabel.Length > 0 ? c.Source.EffectiveLabel : c.Source.Address;
                    menu.Items.Add(Item(string.Format(T("Mnu_MonTableRemove", "Take '{0}' off the table"), label), (s2, e2) => RemoveFromTable(mon, sh, c.Source)));
                    menu.Items.Add(new ToolStripSeparator());
                }
                menu.Items.Add(Item(T("Btn_MonTableAdd", "Add an array..."), (s2, e2) => ShowMenu(add, TableAddMenu(mon, sh))));
                menu.Items.Add(Item(sh.TableRaw ? T("Mnu_MonTableCalc", "Show the calculated values") : T("Mnu_MonTableRaw", "Show the PLC's values"),
                                    (s2, e2) => calc.Checked = sh.TableRaw));
                menu.Items.Add(Item(T("Btn_MonTableList", "List view"), (s2, e2) => ShowTable(mon, sh, false)));
                e.Cancel = false;
            };
            g.ContextMenuStrip = menu;

            view.Controls.Add(g);
            view.Controls.Add(bar);
            sh.Page.Controls.Add(view);
            view.BringToFront();
            sh.TableView = view;
            sh.TableGrid = g;
            sh.TableCalc = calc;
            sh.TableNote = note;
            sh.TableAdd = add;
            sh.TableKey = null;
        }

        /// <summary>The table drawn anew: its columns when its arrays changed, its values every refresh.</summary>
        private static void RenderTable(MonitorTab mon, Sheet sh)
        {
            var g = sh.TableGrid;
            if (g == null || !sh.IsTable) return;
            int count;
            List<WatchRow> left;
            var cols = TableColumnsOf(sh, out count, out left);
            string key = count + "#" + string.Join("|", cols.Select(c => c.Header + "~" + c.DataType + "~" + c.Source.Address));
            if (key != sh.TableKey && !g.IsCurrentCellInEditMode)
            {
                sh.TableKey = key;
                sh.TableCols = cols;
                g.SuspendLayout();
                g.Rows.Clear();
                g.Columns.Clear();
                var index = new DataGridViewTextBoxColumn { Name = "Index", HeaderText = "#", ReadOnly = true, Frozen = true, Width = 56, SortMode = DataGridViewColumnSortMode.NotSortable };
                index.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                index.DefaultCellStyle.Font = UiStyle.TableFont;
                index.DefaultCellStyle.BackColor = Color.FromArgb(246, 247, 249);
                index.DefaultCellStyle.ForeColor = Color.FromArgb(90, 96, 106);
                g.Columns.Add(index);
                var font = UiStyle.FontVariant(UiStyle.TableFont, FontStyle.Bold);
                for (int k = 0; k < cols.Count; k++)
                {
                    var c = cols[k];
                    string header = c.Header + "\n(" + c.DataType + ")";
                    int w = Math.Max(TextRenderer.MeasureText(c.Header, font).Width, TextRenderer.MeasureText("(" + c.DataType + ")", font).Width) + 22;
                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "c" + k, HeaderText = header, Tag = c, Width = Math.Max(80, Math.Min(260, w)),
                        SortMode = DataGridViewColumnSortMode.NotSortable
                    };
                    col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    g.Columns.Add(col);
                }
                g.ResumeLayout();
            }
            if (g.RowCount != count && !g.IsCurrentCellInEditMode) UiStyle.SetRowCount(g, count);

            string note = count == 0
                ? T("Msg_MonTableEmpty", "No array on this tab: add one (Add an array...)")
                : string.Format(T("Msg_MonTableNote", "{0} lines  ·  {1} array(s)"), count, TableSources(sh, out count, out left).Count);
            if (left.Count > 0)
                note += "  ·  " + string.Format(T("Msg_MonTableLeft", "not in it (another length): {0}"),
                                               string.Join(", ", left.Select(a => (a.EffectiveLabel.Length > 0 ? a.EffectiveLabel : a.Address) + " [" + a.EffectiveCount + "]")));
            if (sh.TableNote != null && sh.TableNote.Text != note) sh.TableNote.Text = note;
            if (sh.TableCalc != null && sh.TableCalc.Checked == sh.TableRaw) sh.TableCalc.Checked = !sh.TableRaw;
            if (!g.IsCurrentCellInEditMode) g.Invalidate();
        }

        /// <summary>
        /// What can be added to the table: this controller's arrays of the table's length on its
        /// other tabs, the label list's, or a new one (the device editor, its length set).
        /// </summary>
        private ContextMenuStrip TableAddMenu(MonitorTab mon, Sheet sh)
        {
            var m = NewMenu();
            int count;
            List<WatchRow> left;
            var mine = TableSources(sh, out count, out left).Concat(left).ToList();
            Func<string, string, bool> taken = (address, type) => mine.Any(r => string.Equals(r.Address, address, StringComparison.OrdinalIgnoreCase) &&
                                                                               string.Equals(StructuredTypes.BaseName(r.EffectiveDataType), StructuredTypes.BaseName(type), StringComparison.OrdinalIgnoreCase));
            var offers = new List<KeyValuePair<string, Func<WatchRow>>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var other in mon.Sheets.Where(s => s != sh))
                foreach (var r in other.Rows.Where(r => !r.IsElement && r.IsArray && (count == 0 || r.EffectiveCount == count)))
                {
                    string type = StructuredTypes.BaseName(r.EffectiveDataType);
                    if (taken(r.Address, type) || !seen.Add(r.Address + "|" + type)) continue;
                    var from = r;
                    offers.Add(new KeyValuePair<string, Func<WatchRow>>(
                        (r.EffectiveLabel.Length > 0 ? r.EffectiveLabel : r.Address) + "   " + r.Address + "   " + type + " [" + r.EffectiveCount + "]   (" + other.Name + ")",
                        () => { var c = from.Clone(); if (c.LabelOverride.Length == 0) c.LabelOverride = from.EffectiveLabel; return c; }));
                }
            foreach (var e in PlcLabelTable.EntriesFor(mon.Station))
            {
                int n = StructuredTypes.CountIn(e.DataType);
                if (n < 2 || (count > 0 && n != count)) continue;
                string type = StructuredTypes.BaseName(e.DataType);
                if (taken(e.Address, type) || !seen.Add(e.Address + "|" + type)) continue;
                var entry = e;
                offers.Add(new KeyValuePair<string, Func<WatchRow>>(
                    e.Label + "   " + e.Address + "   " + type + " [" + n + "]   (" + T("Lbl_MonLabelList", "label list") + ")",
                    () => new WatchRow { Address = entry.Address.Trim().ToUpperInvariant(), DataTypeOverride = StructuredTypes.BaseName(entry.DataType),
                                         ArrayCount = StructuredTypes.CountIn(entry.DataType) }));
            }
            var head = new ToolStripMenuItem(count > 0 ? string.Format(T("Mnu_MonTableArraysOf", "Arrays of {0}:"), count) : T("Mnu_MonTableArrays", "Arrays:")) { Enabled = false };
            m.Items.Add(head);
            if (offers.Count == 0)
                m.Items.Add(new ToolStripMenuItem(T("Mnu_MonTableNone", "(none on the other tabs or in the label list)")) { Enabled = false });
            foreach (var o in offers.Take(40))
            {
                var make = o.Value;
                m.Items.Add(Item(o.Key, (s, e) => AddToTable(mon, sh, make())));
            }
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Item(count > 0 ? string.Format(T("Mnu_MonTableNew", "New array of {0}..."), count) : T("Mnu_MonTableNewAny", "New array..."),
                             (s, e) => NewTableArray(mon, sh, count)));
            return m;
        }

        /// <summary>A new array typed in the device editor - its length the table's - at the end of the table.</summary>
        private void NewTableArray(MonitorTab mon, Sheet sh, int count)
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
                AddToTable(mon, sh, r);
            }
        }
    }
}
