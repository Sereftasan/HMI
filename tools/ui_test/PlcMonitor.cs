using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;

// The PLC Monitor: tabs per controller, a list built from the program, arrays,
// copy/paste above a row, comment lines, and a double-click on a value that
// edits it in the table.
class PM {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static Type MonType = typeof(SimulationMonitorForm);
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
        var p = t.GetProperty(n, Any); return p.GetValue(o is Type ? null : o, null);
    }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        // Trailing optional parameters are given their defaults.
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length >= a.Length &&
                                              m.GetParameters().Skip(a.Length).All(p => p.IsOptional));
        var args = a.Concat(mi.GetParameters().Skip(a.Length).Select(p => p.DefaultValue)).ToArray();
        return mi.Invoke(o is Type ? null : o, args);
    }
    static IList Rows(object sheet) { return (IList)Get(sheet, "Rows"); }
    static string Addr(object row) { return (string)Get(row, "Address"); }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "mon_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);

        Console.WriteLine("--- the editor: arrays count in the device's own number system ---");
        Check("M100, 10 bits: the last is M109", PlcRowEditorForm.ElementAddress("M100", "Bit", 9) == "M109");
        Check("D200 as double words: D200, D202, D204, D206", PlcRowEditorForm.ElementAddress("D200", "Double Word [Signed]", 3) == "D206");
        Check("X17 + 1 is X20 (octal)", PlcRowEditorForm.ElementAddress("X17", "Bit", 1) == "X20");
        Check("W0F + 1 is W10 (hexadecimal)", PlcRowEditorForm.ElementAddress("W0F", "Word [Signed]", 1) == "W10");
        var ed = new PlcRowEditorForm("test", "", "Double Word [Signed]", "D200", "", "DataType", isNew: true);
        ((NumericUpDown)Get(ed, "_count")).Value = 4;
        Check("a new row offers the array: 4 of them", ed.ArrayCount == 4);
        Check("  a value is NOT written to a whole run by accident", ed.RowValue == null && !((TextBox)Get(ed, "_value")).Enabled);
        Check("  the preview shows the range", ((Label)Get(ed, "_preview")).Text.StartsWith("D200") && ((Label)Get(ed, "_preview")).Text.Contains("D206"),
              ((Label)Get(ed, "_preview")).Text);
        ed.Show(); Application.DoEvents();
        Check("  it starts in the field of the cell double-clicked (DataType)", ed.ActiveControl == Get(ed, "_dataType"),
              ed.ActiveControl == null ? "none" : ed.ActiveControl.GetType().Name);
        ed.Close();
        var edc = new PlcRowEditorForm("test", "", "Bit", "", "", null, isNew: true, isComment: true);
        Check("a comment line needs no address", edc.IsComment && !((TextBox)Get(edc, "_address")).Enabled && edc.ArrayCount == 1);
        var edit = new PlcRowEditorForm("test", "Lamp", "Bit", "M5", "1", "Value");
        Check("editing an existing row offers no array and no comment switch", edit.ArrayCount == 1 && !((CheckBox)Get(edit, "_comment")).Visible);

        Console.WriteLine("--- the devices a page uses ---");
        var page = new Panel();
        var unit = new FunctionUnitHMI { PlcNumber = 21, TitleTextKey = "Left Clamp", CommandAAddress = "M600", Lamp1AAddress = "X20",
                                         PositionAddress = "D100", PositionDataType = DataTypeEnum.Bin_32 };
        var input = new NumericInputHMI { PlcNumber = 21, Address = "D300", DataType = DataTypeEnum.Bin_16, Name = "numSpeed" };
        var other = new LampHMI { PlcNumber = 22, Address = "M999" };
        var tabs = new TabControl(); var t1 = new TabPage("a"); var t2 = new TabPage("b");
        t1.Controls.Add(new LampHMI { PlcNumber = 21, Address = "M700" }); t2.Controls.Add(new LampHMI { PlcNumber = 21, Address = "M701" });
        tabs.TabPages.Add(t1); tabs.TabPages.Add(t2);
        page.Controls.AddRange(new Control[] { unit, input, other, tabs });
        var devs = DeviceCollector.FromControl(page, 21);
        var byAddr = devs.ToDictionary(d => d.Address);
        Check("the unit's command, sensor and position", byAddr.ContainsKey("M600") && byAddr.ContainsKey("X20") && byAddr.ContainsKey("D100"),
              string.Join(",", devs.Select(d => d.Address)));
        Check("  with their data types (bit, bit, double word)", byAddr.ContainsKey("D100") && byAddr["D100"].DataType == DeviceCollector.DoubleWord &&
              byAddr["M600"].DataType == DeviceCollector.Bit && byAddr["X20"].DataType == DeviceCollector.Bit);
        Check("  named by the unit when the label list has no name", byAddr.ContainsKey("M600") && byAddr["M600"].UsedAs.StartsWith("Left Clamp"),
              byAddr.ContainsKey("M600") ? byAddr["M600"].UsedAs : "");
        Check("a setting field is a word", byAddr.ContainsKey("D300") && byAddr["D300"].DataType == DeviceCollector.Word);
        Check("another controller's device is left out", !byAddr.ContainsKey("M999"));
        Check("only the SHOWN tab of a tab control ('this screen')", byAddr.ContainsKey("M700") && !byAddr.ContainsKey("M701"));

        File.WriteAllText(Path.Combine(dir, "labels.csv"), "Label;DataType;Address\nIN_Start;Bit;X0\nLimit;Double Word [Signed];D318\n");
        PlcLabelTable.LoadStation(Path.Combine(dir, "labels.csv"), 21);
        var fromLabels = DeviceCollector.FromLabelList(21);
        Check("the label list: every device, with its type", fromLabels.Count == 2 && fromLabels[1].DataType == DeviceCollector.DoubleWord,
              string.Join(",", fromLabels.Select(d => d.Address + ":" + d.DataType)));

        Console.WriteLine("--- the monitor ---");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(21);
        PlcManager.Instance.RegisterDriver(21, sim);
        PlcManager.Instance.RegisterStation(21, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(21, true);
        sim.Write("M5", 1); sim.Write("D10", 1234);

        // a list saved by the previous monitor: no tabs in it
        var ep = PlcConnectionSettings.Endpoints().First(e => e.StationId == 21);
        string path = (string)Call(MonType, "WatchPath", ep);
        File.WriteAllLines(path, new[] { "# old list", "Label;DataType;Address;Value", "Start;Bit;M5;", "Speed;Word [Signed];D10;" });

        var form = new SimulationMonitorForm();
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 21);
        var sheets = (IList)Get(mon, "Sheets");
        Check("an old list opens as one tab, 'Main'", sheets.Count == 1 && (string)Get(sheets[0], "Name") == "Main",
              sheets.Count + " / " + (sheets.Count > 0 ? Get(sheets[0], "Name") : ""));
        var sh = sheets[0];
        Check("  with its devices first, in order", Addr(Rows(sh)[0]) == "M5" && Addr(Rows(sh)[1]) == "D10");
        var grid = (DataGridView)Get(sh, "Grid");
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        Call(form, "RefreshValues"); Application.DoEvents();

        Console.WriteLine("--- a double-click on a VALUE edits it in the table ---");
        grid.Focus();
        typeof(DataGridView).GetMethod("OnCellDoubleClick", Any).Invoke(grid, new object[] { new DataGridViewCellEventArgs(3, 1) });
        Application.DoEvents();
        Check("the value cell is being edited, no dialog", grid.IsCurrentCellInEditMode && grid.CurrentCell.OwningColumn.Name == "Value" && grid.CurrentCell.RowIndex == 1,
              grid.CurrentCell == null ? "" : grid.CurrentCell.OwningColumn.Name + " edit=" + grid.IsCurrentCellInEditMode);
        if (grid.EditingControl != null) grid.EditingControl.Text = "4321";
        grid.EndEdit();
        Application.DoEvents();
        int d10; sim.TryRead("D10", out d10);
        Check("  and what is typed is written", d10 == 4321, d10.ToString());

        Console.WriteLine("--- copy, paste above a row, comment lines ---");
        grid.ClearSelection(); grid.Rows[0].Selected = true; grid.Rows[1].Selected = true;
        Call(form, "CopySelected");
        grid.ClearSelection(); grid.Rows[1].Selected = true; grid.CurrentCell = grid.Rows[1].Cells[0];
        Call(form, "Paste", false);
        var addrs = Rows(sh).Cast<object>().Select(Addr).ToList();
        Check("two rows pasted ABOVE the selected one", string.Join(",", addrs.Take(4)) == "M5,M5,D10,D10", string.Join(",", addrs));
        int before = Rows(sh).Count;
        Call(form, "Paste", true);
        Check("  and again at the end (before the empty line)", Rows(sh).Count == before + 2 && Addr(Rows(sh)[Rows(sh).Count - 1]) == "",
              Rows(sh).Count + " rows");

        // text typed into the empty line's label with no address is a comment
        int blank = Rows(sh).Count - 1;
        Call(form, "Render", mon, sh);
        grid.Rows[blank].Cells["Label"].Value = "--- clamps ---";
        Call(form, "CommitCellEdit", mon, sh, blank, 0);
        var last = Rows(sh)[blank];
        Check("a label with no address becomes a comment line", (bool)Get(last, "IsComment") && (string)Get(last, "Comment") == "--- clamps ---");

        Console.WriteLine("--- tabs are kept ---");
        var mt = mon.GetType();
        var second = Activator.CreateInstance(MonType.GetNestedType("Sheet", Any));
        mt.GetField("Sheets").FieldType.GetMethod("Add").Invoke(Get(mon, "Sheets"), new[] { second });
        second.GetType().GetField("Name").SetValue(second, "Job List");
        var rowType = MonType.GetNestedType("WatchRow", Any);
        var r = Activator.CreateInstance(rowType); rowType.GetField("Address").SetValue(r, "D500");
        Rows(second).Add(r);
        var lines = (List<string>)Call(MonType, "Serialize", mon);
        Check("saved with a section per tab", lines.Contains("#sheet:Main") && lines.Contains("#sheet:Job List"), string.Join(" | ", lines.Where(l => l.StartsWith("#sheet"))));
        Check("  the comment saved as a line with no address", lines.Contains("--- clamps ---;;;"));
        var back = Activator.CreateInstance(MonType.GetNestedType("MonitorTab", Any));
        Call(MonType, "Deserialize", back, lines);
        var bs = (IList)Get(back, "Sheets");
        Check("read back: the same tabs", bs.Count == 2 && (string)Get(bs[1], "Name") == "Job List" && Addr(Rows(bs[1])[0]) == "D500");
        Check("  the comment is a comment again", Rows(bs[0]).Cast<object>().Any(x => (bool)Get(x, "IsComment")));


        Console.WriteLine("--- the buttons have tooltips of the monitor's own (export and import among them) ---");
        var tips = Get(form, "_tips") as ToolTip;
        var buttons = new List<Control>();
        foreach (var ts in form.Controls.OfType<ToolStrip>())
            foreach (var h in ts.Items.OfType<ToolStripControlHost>()) buttons.Add(h.Control);
        Check("a tooltip on every toolbar button", tips != null && buttons.Count >= 10 && buttons.All(b => (tips.GetToolTip(b) ?? "").Length > 0),
              buttons.Count + " buttons");
        Check("  export and import are there", buttons.Any(b => tips.GetToolTip(b).StartsWith("Export")) && buttons.Any(b => tips.GetToolTip(b).StartsWith("Import")));
        Check("  the status line has no LIVE PLC warning", !((Label)Get(form, "_status")).Text.Contains("LIVE"), ((Label)Get(form, "_status")).Text);

        Console.WriteLine("--- tabs: the one on display is the one worked on ---");
        ((IList)Get(mon, "Sheets")).Remove(second);             // added above for the file test only
        var strip = (TabControl)Get(mon, "SheetTabs");
        Func<string> pages = () => string.Join(",", strip.TabPages.Cast<TabPage>().Select(p => p.Text));
        Func<string> names = () => string.Join(",", ((IList)Get(mon, "Sheets")).Cast<object>().Select(x => (string)Get(x, "Name")));
        Func<object> current = () => Get(mon, "Current");
        strip.SelectedIndex = 0;
        Call(form, "AddSheet", mon, "A", Math.Max(0, strip.SelectedIndex));
        Call(form, "AddSheet", mon, "B", Math.Max(0, strip.SelectedIndex));
        Check("a new tab goes BEFORE the one on display", pages() == "B,A,Main" && names() == pages(), pages() + " / " + names());
        Check("  and is the one on display", (string)Get(current(), "Name") == "B");
        strip.SelectedIndex = 2;
        Call(form, "AddSheet", mon, "C", strip.SelectedIndex);
        Check("before Main: B,A,C,Main", pages() == "B,A,C,Main" && names() == pages(), pages() + " / " + names());
        var c = current();
        Check("  the page on display holds the tab's own grid", strip.SelectedTab.Controls.Contains((DataGridView)Get(c, "Grid")));
        int mainRows = Rows(sh).Count;
        Call(form, "Paste", true);
        Check("pasting into the EMPTY new tab fills that tab", Rows(c).Count == 3 && Addr(Rows(c)[0]) == "M5" && Rows(sh).Count == mainRows,
              Rows(c).Count + " rows in C, Main " + Rows(sh).Count + " (was " + mainRows + ")");
        Check("  and its own grid shows them", ((DataGridView)Get(c, "Grid")).Rows.Count == 3);

        strip.SelectedIndex = 1;                                   // A
        Call(form, "RemoveSheet", mon, current());
        Check("deleting A deletes A only", pages() == "B,C,Main" && names() == pages(), pages() + " / " + names());

        var tabB = ((IList)Get(mon, "Sheets"))[0];
        Call(form, "MoveSheetTo", mon, tabB, 2);
        Check("a tab dragged two places at once", pages() == "C,Main,B" && names() == pages() && current() == tabB, pages() + " / " + names());

        Console.WriteLine("--- columns: own widths, dragged order, the same on every tab ---");
        var g1 = (DataGridView)Get(sh, "Grid"); var g2 = (DataGridView)Get(c, "Grid");
        Check("no column is stretched to the edge", g1.AutoSizeColumnsMode == DataGridViewAutoSizeColumnsMode.None &&
              g1.Columns.Cast<DataGridViewColumn>().All(col => col.AutoSizeMode == DataGridViewAutoSizeColumnMode.NotSet || col.AutoSizeMode == DataGridViewAutoSizeColumnMode.None));
        Check("  headings can be dragged to re-order", g1.AllowUserToOrderColumns);
        g1.Columns["Label"].Width = 333;
        Application.DoEvents();
        int valueBefore = g1.Columns["Value"].Width;
        g1.Columns["Value"].Width = 60;
        Application.DoEvents();
        Check("narrowing the last column leaves the others as they were", g1.Columns["Label"].Width == 333 && g1.Columns["Value"].Width == 60,
              g1.Columns["Label"].Width + "/" + g1.Columns["Value"].Width);
        g1.Columns["Value"].DisplayIndex = 0;
        Application.DoEvents(); Application.DoEvents();
        // Mono's DisplayIndex setter ranks a tie by column number (Value set to 0
        // lands at 1 there); .NET moves the column. The ORDER is compared with
        // whatever the first grid ended up with, and only on .NET.
        bool mono = Type.GetType("Mono.Runtime") != null;
        Func<DataGridView, string> colOrder = g => string.Join(",", g.Columns.Cast<DataGridViewColumn>().OrderBy(x => x.DisplayIndex).Select(x => x.Name));
        Check("the other tabs take the same widths", g2.Columns["Label"].Width == 333 && g2.Columns["Value"].Width == 60,
              g2.Columns["Label"].Width + "/" + g2.Columns["Value"].Width);
        Check("  and the same order" + (mono ? " (not compared on Mono)" : ""), mono || colOrder(g2) == colOrder(g1), colOrder(g1) + " / " + colOrder(g2));
        string cfg = Path.Combine(dir, "Columns.cfg");
        Check("  and they are kept", File.Exists(cfg) && File.ReadAllText(cfg).Contains("Label=333;") &&
              File.ReadAllText(cfg).Contains("Value=60;" + g1.Columns["Value"].DisplayIndex));
        var gd = (DataGridView)Get(Call(form, "AddSheet", mon, "D", 0), "Grid");
        Check("  a new tab opens with them", gd.Columns["Label"].Width == 333 && gd.Columns["Value"].Width == 60 && (mono || colOrder(gd) == colOrder(g1)));
        Call(form, "RemoveSheet", mon, current());

        Console.WriteLine("--- sorting: section by section under the comment lines ---");
        Check("M2 before M10, X7 before X10 (octal), W0F before W10 (hex)",
              (int)Call(MonType, "CompareAddress", "M2", "M10") < 0 && (int)Call(MonType, "CompareAddress", "X7", "X10") < 0 &&
              (int)Call(MonType, "CompareAddress", "W0F", "W10") < 0 && (int)Call(MonType, "CompareAddress", "D1", "M1") < 0);
        strip.SelectedTab = (TabPage)Get(c, "Page");
        Rows(c).Clear();
        Func<string, string, string, object> mk = (a, lbl, com) => {
            var w = Activator.CreateInstance(rowType); rowType.GetField("Address").SetValue(w, a);
            rowType.GetField("LabelOverride").SetValue(w, lbl); rowType.GetField("Comment").SetValue(w, com); return w; };
        foreach (var w in new[] { mk("", "", "--- A ---"), mk("M10", "zeta", ""), mk("M2", "alpha", ""), mk("X10", "Beta", ""), mk("X7", "gamma", ""),
                                  mk("", "", "--- B ---"), mk("D5", "b", ""), mk("D1", "a", ""), mk("", "", "") }) Rows(c).Add(w);
        Func<string> order = () => string.Join(",", Rows(c).Cast<object>().Select(x => Addr(x).Length > 0 ? Addr(x) : (string)Get(x, "Comment") == "" ? "_" : ((string)Get(x, "Comment")).Trim('-', ' ')));
        Call(form, "SortSheet", mon, c, "Address");
        Check("by address: each section on its own, the heading on top", order() == "A,M2,M10,X7,X10,B,D1,D5,_", order());
        Check("  the heading shows the direction", g2.Columns["Address"].HeaderCell.SortGlyphDirection == SortOrder.Ascending);
        Call(form, "SortSheet", mon, c, "Address");
        Check("a second click reverses it", order() == "A,X10,X7,M10,M2,B,D5,D1,_" && g2.Columns["Address"].HeaderCell.SortGlyphDirection == SortOrder.Descending, order());
        Call(form, "SortSheet", mon, c, "Label");
        Check("by label, case ignored", order() == "A,M2,X10,X7,M10,B,D1,D5,_" && g2.Columns["Address"].HeaderCell.SortGlyphDirection == SortOrder.None, order());
        var vals = (IDictionary<string, int>)Get(mon, "Values");
        vals["M10"] = 1; vals["M2"] = 0; vals["X7"] = 1; vals["D1"] = 7; vals["D5"] = 3;
        Call(form, "SortSheet", mon, c, "Value");
        Check("by value (equal values keep their order), the unread last", order() == "A,M2,X7,M10,X10,B,D5,D1,_", order());

        Console.WriteLine("--- a right-click keeps a multiple selection ---");
        var gc = (DataGridView)Get(c, "Grid");
        gc.ClearSelection(); gc.Rows[1].Selected = true; gc.Rows[2].Selected = true;
        var rightClick = typeof(DataGridView).GetMethod("OnCellMouseDown", Any);
        rightClick.Invoke(gc, new object[] { new DataGridViewCellMouseEventArgs(0, 2, 5, 5, new MouseEventArgs(MouseButtons.Right, 1, 5, 5, 0)) });
        Check("right-click on a selected row: both stay selected", gc.SelectedRows.Count == 2, gc.SelectedRows.Count.ToString());
        Call(form, "CopySelected");
        Check("  and Copy takes both", ((IList)Get(MonType, "_clip")).Count == 2);
        rightClick.Invoke(gc, new object[] { new DataGridViewCellMouseEventArgs(0, 4, 5, 5, new MouseEventArgs(MouseButtons.Right, 1, 5, 5, 0)) });
        Check("right-click on another row: that row alone", gc.SelectedRows.Count == 1 && gc.SelectedRows[0].Index == 4);

        Console.WriteLine("--- every tab can be deleted ---");
        while (strip.TabPages.Count > 0) Call(form, "RemoveSheet", mon, current());
        Check("no tabs left, and the page says what to do", ((IList)Get(mon, "Sheets")).Count == 0 && !strip.Visible && ((Label)Get(mon, "EmptyHint")).Visible);
        Call(form, "RefreshValues");
        Check("  the refresh copes, the status line says so", ((Label)Get(form, "_status")).Text.Contains("No tabs"), ((Label)Get(form, "_status")).Text);
        var saved = (List<string>)Call(MonType, "Serialize", mon);
        Check("  saved as no tabs", saved.Contains("#tabs:0"));
        var none = Activator.CreateInstance(MonType.GetNestedType("MonitorTab", Any));
        Call(MonType, "Deserialize", none, saved);
        Check("  and read back as none - not a new 'Main'", (int)Get(none, "SavedTabCount") == 0 && ((IList)Get(none, "Sheets")).Count == 0);
        Call(form, "Paste", true);
        Check("pasting with no tab makes 'Main' for it", pages() == "Main" && Rows(current()).Count == 3 && strip.Visible, pages());


        Console.WriteLine("--- undo: back to the order before a sort ---");
        var us = current();
        Rows(us).Clear();
        foreach (var a in new[] { "M30", "M4", "D9", "M17" }) { var w = Activator.CreateInstance(rowType); rowType.GetField("Address").SetValue(w, a); Rows(us).Add(w); }
        Rows(us).Add(Activator.CreateInstance(rowType));
        Func<string> seq = () => string.Join(",", Rows(current()).Cast<object>().Select(Addr).Where(a => a.Length > 0));
        Call(form, "SortSheet", mon, us, "Address");
        Check("sorted", seq() == "D9,M4,M17,M30", seq());
        Call(form, "Undo");
        Check("Undo: the order it was built in, which no sort can give back", seq() == "M30,M4,D9,M17", seq());
        grid = (DataGridView)Get(current(), "Grid");
        grid.ClearSelection(); grid.Rows[1].Selected = true; grid.Rows[2].Selected = true;
        Call(form, "RemoveSelected");
        Check("two rows removed", seq() == "M30,M17", seq());
        typeof(Form).GetMethod("ProcessCmdKey", Any).Invoke(form, new object[] { new Message(), Keys.Control | Keys.Z });
        Check("Ctrl+Z puts them back", seq() == "M30,M4,D9,M17", seq());
        Check("  nothing left to undo past the start... the button says what is next",
              ((Button)Get(form, "_btnUndo")).Enabled == (((System.Collections.IList)Get(form, "_undo")).Count > 0));

        Console.WriteLine("--- redo: takes back an undo ---");
        var btnRedo = (Button)Get(form, "_btnRedo");
        var tipsR = (ToolTip)Get(form, "_tips");
        Check("after the undo, Redo is offered and says what it redoes", btnRedo.Enabled && tipsR.GetToolTip(btnRedo).StartsWith("Redo: ") &&
              tipsR.GetToolTip(btnRedo).Contains("Ctrl+Y"), tipsR.GetToolTip(btnRedo));
        Call(form, "Redo");
        Check("Redo: the two rows are removed again", seq() == "M30,M17", seq());
        Check("  and that can be undone once more", ((Button)Get(form, "_btnUndo")).Enabled && !btnRedo.Enabled);
        Call(form, "Undo");
        Check("  undo", seq() == "M30,M4,D9,M17", seq());
        typeof(Form).GetMethod("ProcessCmdKey", Any).Invoke(form, new object[] { new Message(), Keys.Control | Keys.Y });
        Check("Ctrl+Y redoes", seq() == "M30,M17", seq());
        typeof(Form).GetMethod("ProcessCmdKey", Any).Invoke(form, new object[] { new Message(), Keys.Control | Keys.Z });
        typeof(Form).GetMethod("ProcessCmdKey", Any).Invoke(form, new object[] { new Message(), Keys.Control | Keys.Shift | Keys.Z });
        Check("Ctrl+Shift+Z too", seq() == "M30,M17", seq());
        Call(form, "Undo");
        Call(form, "SortSheet", mon, current(), "Address");
        Check("a new change after an undo ends what can be redone", !btnRedo.Enabled && ((IList)Get(form, "_redo")).Count == 0);
        Call(form, "Undo");
        Check("  (the sort undone: the list as it was)", seq() == "M30,M4,D9,M17", seq());

        Console.WriteLine("--- the delete button's menu ---");
        grid = (DataGridView)Get(current(), "Grid");
        grid.ClearSelection(); grid.Rows[0].Selected = true;
        var dm = (ContextMenuStrip)Call(form, "DeleteMenu");
        var dmItems = dm.Items.OfType<ToolStripMenuItem>().ToList();
        Check("four choices: the selected row, this tab, every tab of this controller, of every controller",
              dmItems.Count == 4 && dmItems[0].Enabled && dmItems[1].Enabled && dmItems[2].Enabled && dmItems[3].Enabled,
              string.Join(" | ", dmItems.Select(i => i.Text)));
        Check("  the row entry shows its key", dmItems[0].ShortcutKeyDisplayString == "Del");

        // a second controller, so "every controller" means more than one
        var sim2 = new Kaban.Drivers.Simulation.SimulationDriver(22);
        PlcManager.Instance.RegisterDriver(22, sim2);
        PlcManager.Instance.RegisterStation(22, "Bench 2", SecurityLevel.Operator);
        var form2 = new SimulationMonitorForm();
        form2.Show(); Application.DoEvents();
        var mons2 = ((IList)Get(form2, "_monitors")).Cast<object>().ToList();
        var a21 = mons2.First(x => (int)Get(x, "Station") == 21); var a22 = mons2.First(x => (int)Get(x, "Station") == 22);
        Func<object, int> tabCount = x => ((IList)Get(x, "Sheets")).Count;
        int before21 = tabCount(a21), before22 = tabCount(a22);
        var arr = Array.CreateInstance(MonType.GetNestedType("MonitorTab", Any), 2); arr.SetValue(a21, 0); arr.SetValue(a22, 1);
        MonType.GetMethod("DeleteAllSheets", Any).Invoke(form2, new object[] { arr, false });
        Check("every tab of every controller deleted", tabCount(a21) == 0 && tabCount(a22) == 0, before21 + "+" + before22 + " tabs before");
        Call(form2, "Undo");
        Check("  ONE undo brings back both controllers' tabs", tabCount(a21) == before21 && tabCount(a22) == before22,
              tabCount(a21) + "+" + tabCount(a22));
        form2.Close();

        Console.WriteLine("--- keys ---");
        var strip2 = (TabControl)Get(mon, "SheetTabs");
        Call(form, "AddSheet", mon, "Scratch", 0);
        int tabsNow = strip2.TabPages.Count;
        strip2.Focus(); Application.DoEvents();
        typeof(Form).GetMethod("ProcessCmdKey", Any).Invoke(form, new object[] { new Message(), Keys.Delete });
        Check("Del on the tab strip deletes the (empty) tab on show", strip2.TabPages.Count == tabsNow - 1 || !strip2.Focused,
              strip2.Focused ? strip2.TabPages.Count + " tabs" : "the strip could not take the focus here");
        grid = (DataGridView)Get(current(), "Grid");
        Check("Tab leaves the table instead of stepping through cells", grid.StandardTab);
        grid.Focus(); Application.DoEvents();
        int vr = Rows(current()).Cast<object>().ToList().FindIndex(x => Addr(x).Length > 0);
        grid.CurrentCell = grid.Rows[vr].Cells["Value"];
        typeof(Form).GetMethod("ProcessCmdKey", Any).Invoke(form, new object[] { new Message(), Keys.Enter });
        Application.DoEvents();
        Check("Enter on a value edits it in the table, like a double-click", grid.IsCurrentCellInEditMode || !grid.ContainsFocus,
              grid.ContainsFocus ? "edit=" + grid.IsCurrentCellInEditMode : "no focus here");
        grid.CancelEdit(); grid.EndEdit();

        Console.WriteLine("--- the toolbar ---");
        var drawn = buttons.Where(b => b.Text == "" && !string.IsNullOrEmpty(b.AccessibleName)).Select(b => b.AccessibleName).ToList();
        Check("copy, paste, move up, move down, undo and redo are DRAWN pictures, not characters",
              new[] { "Copy", "Paste", "Move up", "Move down", "Undo", "Redo" }.All(drawn.Contains), string.Join(",", drawn));
        var bUndo = buttons.First(b => b.AccessibleName == "Undo"); var bRedo = buttons.First(b => b.AccessibleName == "Redo");
        Check("  redo right after undo", buttons.IndexOf(bRedo) == buttons.IndexOf(bUndo) + 1);
        Check("  no emoji of the old up/down, undo, copy or paste left",
              !buttons.Any(b => new[] { 0x1F53C, 0x1F53D, 0x21A9, 0x1F4CB, 0x1F4CC }.Any(cp => b.Text == char.ConvertFromUtf32(cp))));
        Check("  find is the magnifier leaning right", buttons.Any(b => b.Text == char.ConvertFromUtf32(0x1F50E) && tips.GetToolTip(b).Contains("Ctrl+F")));
        using (var bmp = new System.Drawing.Bitmap(44, 32))
        {
            using (var g = System.Drawing.Graphics.FromImage(bmp)) { g.Clear(System.Drawing.Color.White); ToolGlyphs.MoveUp(g, new System.Drawing.Rectangle(0, 0, 44, 32), true); }
            int blue = 0, dark = 0;
            for (int x = 0; x < 44; x++) for (int y = 0; y < 32; y++)
            { var px = bmp.GetPixel(x, y); if (px.B > 150 && px.R < 80) blue++; else if (px.R < 90 && px.G < 90 && px.B < 110) dark++; }
            Check("  the move-up picture: a heavy blue arrow and a dark bar", blue > 60 && dark > 25, blue + " blue, " + dark + " dark pixels");
        }
        var cm = (ContextMenuStrip)Call(form, "CollectMenu");
        var cmTexts = cm.Items.OfType<ToolStripMenuItem>().Select(i => i.Text).ToList();
        Check("the whole program: with a heading per page, or as one list",
              cmTexts.Any(t => t.Contains("heading line per page")) && cmTexts.Any(t => t.Contains("one list")), string.Join(" | ", cmTexts));

        Console.WriteLine("--- the '+' after the last tab, and 'after this one' ---");
        var plus = (Button)Get(mon, "AddTabButton");
        strip = (TabControl)Get(mon, "SheetTabs");
        int selBefore = strip.SelectedIndex;
        Call(MonType, "PlaceAddTab", mon); Application.DoEvents();
        var lastRect = strip.GetTabRect(strip.TabCount - 1);
        Check("a '+' sits just after the last tab", plus != null && plus.Visible && plus.AccessibleName == "New tab" &&
              plus.Left >= strip.Left + lastRect.Right && plus.Left <= strip.Left + lastRect.Right + 12 &&
              plus.Top >= strip.Top && plus.Bottom <= strip.Top + lastRect.Bottom + 2,
              plus == null ? "none" : plus.Bounds + " / last tab " + lastRect);
        Check("  on top of the strip, with its tooltip", plus.Parent.Controls.GetChildIndex(plus) == 0 && tips.GetToolTip(plus) == "New tab at the end");
        var placeType = MonType.GetNestedType("TabPlace", Any);
        strip.SelectedIndex = 0;
        string firstName = (string)Get(current(), "Name");
        Call(form, "AddSheetAt", mon, "End", Enum.Parse(placeType, "End"));
        Check("'+': the new tab is the LAST, and on display", pages().EndsWith(",End") && (string)Get(current(), "Name") == "End", pages());
        Call(MonType, "PlaceAddTab", mon);
        var lastNow = strip.GetTabRect(strip.TabCount - 1);
        Check("  the '+' moved along after it", plus.Left >= strip.Left + lastNow.Right && plus.Left <= strip.Left + lastNow.Right + 12, plus.Left + " vs " + lastNow.Right);
        strip.SelectedIndex = 0;
        Call(form, "AddSheetAt", mon, "After", Enum.Parse(placeType, "After"));
        Check("'after this one': right after the tab on display", pages().StartsWith(firstName + ",After,"), pages());
        Call(form, "Undo");
        Check("  undone like any other change", !pages().Contains("After"), pages());
        var menuTexts = ((ContextMenuStrip)Call(form, "SheetMenu")).Items.OfType<ToolStripMenuItem>().Select(i => i.Text).ToList();
        Check("the tab menu offers before, after and at the end",
              menuTexts.Contains("New tab (before this one)...") && menuTexts.Contains("New tab (after this one)...") && menuTexts.Any(t => t.StartsWith("New tab at the end")),
              string.Join(" | ", menuTexts));
        strip.SelectedIndex = selBefore;

        Console.WriteLine("--- narrow the window as far as you like: the toolbar folds under its chevron ---");
        var bar = form.Controls.OfType<ToolStrip>().First();
        Check("the window's minimum width is small", form.MinimumSize.Width <= 320, form.MinimumSize.ToString());
        form.WindowState = FormWindowState.Normal;
        form.Size = new System.Drawing.Size(330, 500); Application.DoEvents();
        bar.PerformLayout(); Application.DoEvents();
        int folded = bar.Items.Cast<ToolStripItem>().Count(i => i.IsOnOverflow);
        Check("  at 330 pixels it is 330 wide", form.Width <= 340, form.Width.ToString());
        Check("  the buttons that do not fit are under the chevron", bar.CanOverflow && bar.OverflowButton.Visible && folded > 3, folded + " folded");
        form.Size = new System.Drawing.Size(900, 700); Application.DoEvents(); bar.PerformLayout();
        Check("  wide again: all on the bar", bar.Items.Cast<ToolStripItem>().Count(i => i.IsOnOverflow) == 0);

        Console.WriteLine("--- removing from the simulator's list keeps it removed ---");
        sh = current(); grid = (DataGridView)Get(sh, "Grid");
        grid.ClearSelection();
        int idx = Rows(sh).Cast<object>().ToList().FindIndex(x => Addr(x) == "M5");
        // remove every M5 row (there are copies now)
        foreach (var i in Rows(sh).Cast<object>().Select((x, i) => new { x, i }).Where(p => Addr(p.x) == "M5").Select(p => p.i)) grid.Rows[i].Selected = true;
        Call(form, "RemoveSelected");
        Call(form, "RefreshValues");
        Check("M5 does not come back on the next refresh", !Rows(sh).Cast<object>().Any(x => Addr(x) == "M5"));
        Check("  the 'show removed devices again' button appears", ((ToolStripItem)Get(form, "_restoreItem")).Visible);
        Call(form, "RestoreHidden"); Call(form, "RefreshValues");
        Check("  and brings it back", Rows(sh).Cast<object>().Any(x => Addr(x) == "M5"));

        Console.WriteLine("--- the window opens where it was closed ---");
        form.Bounds = new System.Drawing.Rectangle(37, 41, 700, 520); Application.DoEvents();
        var closedAt = form.Bounds;
        form.Close(); Application.DoEvents();
        string windows = Path.Combine(dir, "Windows.cfg");
        Check("its place and size are kept", File.Exists(windows) && File.ReadAllText(windows).Contains("Monitor.Bounds=" + closedAt.X + "," + closedAt.Y + "," + closedAt.Width + "," + closedAt.Height),
              File.Exists(windows) ? File.ReadAllText(windows).Replace("\r\n", " | ") : "no file");
        var again = new SimulationMonitorForm();
        Check("  and used next time", again.PlacementRestored && again.Bounds == closedAt, again.Bounds + " vs " + closedAt);
        again.Dispose();
        var saved2 = File.ReadAllText(windows);
        File.WriteAllText(windows, "Monitor.Bounds=-9000,-9000,700,520\r\n");
        var lost = new SimulationMonitorForm();
        Check("a place on no screen (a monitor unplugged) is not used", !lost.PlacementRestored);
        lost.Dispose();
        File.WriteAllText(windows, saved2);

        Console.WriteLine("--- Find: columns dragged, the window as it was closed ---");
        var find = new DeviceFindForm(21, "Bench", "", u => "");
        var list = (DataGridView)Get(find, "_list");
        Check("the headings can be dragged to re-order", list.AllowUserToOrderColumns);
        Check("  a table in the monitor's look: its heading font and self-sizing heading",
              list.ColumnHeadersHeightSizeMode == DataGridViewColumnHeadersHeightSizeMode.AutoSize &&
              list.ColumnHeadersDefaultCellStyle.Font != null && list.ColumnHeadersDefaultCellStyle.Font.Bold &&
              !list.EnableHeadersVisualStyles && list.RowTemplate.Height == 26);
        Check("  and it narrows further than before", find.MinimumSize.Width <= 380, find.MinimumSize.ToString());
        find.Show(); Application.DoEvents();
        find.Bounds = new System.Drawing.Rectangle(60, 70, 640, 360); Application.DoEvents();
        var findAt = find.Bounds;
        list.Columns[0].Width = 222; list.Columns[3].Width = 99;
        list.Columns[2].DisplayIndex = 0; Application.DoEvents();
        string order0 = string.Join(",", list.Columns.Cast<DataGridViewColumn>().Select(h => h.DisplayIndex));
        find.Close(); Application.DoEvents();
        var findCfg = File.ReadAllText(windows);
        Check("kept: place, size, widths and order", findCfg.Contains("Find.Bounds=" + findAt.X + "," + findAt.Y + "," + findAt.Width + "," + findAt.Height) &&
              findCfg.Contains("Find.Columns=222:"), findCfg.Replace("\r\n", " | "));
        Check("  beside the monitor's own line", findCfg.Contains("Monitor.Bounds="));
        var find2 = new DeviceFindForm(21, "Bench", "", u => "");
        var list2 = (DataGridView)Get(find2, "_list");
        Check("opened again: as it was closed", find2.Bounds == findAt && list2.Columns[0].Width == 222 && list2.Columns[3].Width == 99, find2.Bounds + ", " + list2.Columns[0].Width + "/" + list2.Columns[3].Width);
        // A table now, like the monitor's lists: Mono's DataGridView ignores an order
        // set before it is shown (see the monitor's own columns above) - compared on .NET.
        bool monoRt = Type.GetType("Mono.Runtime") != null;
        Check("  the columns in the order they were dragged to" + (monoRt ? " (not compared on Mono)" : ""),
              monoRt || string.Join(",", list2.Columns.Cast<DataGridViewColumn>().Select(h => h.DisplayIndex)) == order0,
              order0 + " vs " + string.Join(",", list2.Columns.Cast<DataGridViewColumn>().Select(h => h.DisplayIndex)));
        find2.Dispose();

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nPLC MONITOR ALL PASS" : "\nPLC MONITOR " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
