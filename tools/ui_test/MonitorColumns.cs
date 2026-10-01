using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;

// THE PLC MONITOR'S COLUMNS (30 September), on the real FA2010 program: taken
// off and back from a heading's menu, their widths typed - or the whole
// table's - fitted, back to the defaults; the same on every tab and when the
// monitor opens again. "Last change" is "Modified". Find's table has the
// device's Value, Display Format and Modified too, and the same menu.
class MCol {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static Type MonType = typeof(SimulationMonitorForm);
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        for (; t != null; t = t.BaseType) {
            var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o is Type ? null : o, null);
        }
        return null;
    }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length >= a.Length &&
                                              m.GetParameters().Skip(a.Length).All(p => p.IsOptional));
        var args = a.Concat(mi.GetParameters().Skip(a.Length).Select(p => p.DefaultValue)).ToArray();
        return mi.Invoke(o is Type ? null : o, args);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }

    static ColumnChooser ChooserOf(DataGridView grid) {
        var menu = grid.Columns[0].HeaderCell.ContextMenuStrip;
        return menu == null ? null : menu.Tag as ColumnChooser;
    }
    static ToolStripMenuItem ItemFor(ColumnChooser c, string tag) {
        return c.MenuItems().OfType<ToolStripMenuItem>().FirstOrDefault(i => (i.Tag as string) == tag);
    }
    static string ColumnsFile(string dir) { var p = Path.Combine(dir, "Columns.cfg"); return File.Exists(p) ? File.ReadAllText(p) : ""; }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "moncol_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 }) PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));
        PlcLabelTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));
        SecurityManager.Login("admin", SecurityLevel.Admin);
        foreach (int st in new[] { 1, 2, 3, 4 }) ConnectionMonitor.ReportRead(st, true);
        var sim = (Kaban.Drivers.Simulation.SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(1));
        sim.Write("M672", 1);

        var form = new SimulationMonitorForm();
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(x => (int)Get(x, "Station") == 1);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        Call(form, "RefreshValues"); Pump(100);
        Call(form, "AddSheet", mon, "Second", ((IList)Get(mon, "Sheets")).Count);
        Application.DoEvents();
        var sheets = ((IList)Get(mon, "Sheets")).Cast<object>().ToList();
        var grid = (DataGridView)Get(sheets[0], "Grid");
        var grid2 = (DataGridView)Get(sheets[1], "Grid");
        ((TabControl)Get(mon, "SheetTabs")).SelectedIndex = 0; Application.DoEvents();

        Console.WriteLine("--- the heading ---");
        Check("\"Last change\" is \"Modified\"", grid.Columns["Changed"].HeaderText == "Modified", grid.Columns["Changed"].HeaderText);
        var chooser = ChooserOf(grid);
        Check("a right-click on a heading: the columns' menu (not the rows')", chooser != null && grid.Columns.Cast<DataGridViewColumn>().All(c => c.HeaderCell.ContextMenuStrip == chooser.Menu));
        var items = chooser.MenuItems();
        var names = items.OfType<ToolStripMenuItem>().Where(i => i.CheckOnClick).Select(i => i.Text).ToList();
        Check("  every column to tick, in the order shown", names.Count == 7 && names[0] == "Label" && names.Contains("Modified") && names.Contains("Display Format"), string.Join(",", names));
        Check("  then Column widths..., Fit the widths to the contents, Save as the default, Default column settings",
              ItemFor(chooser, "widths") != null && ItemFor(chooser, "fit") != null && ItemFor(chooser, "savedefault") != null &&
              ItemFor(chooser, "savedefault").Text == "Save as the default" && ItemFor(chooser, "default").Text == "Default column settings" &&
              chooser.DefaultKey == "PlcMonitor");
        var turkish = LanguageManager.GetString("Col_LastChange", "") ;
        Check("  (\"Modified\" in all six languages)", File.ReadAllLines("CommonTranslations.csv").First(l => l.StartsWith("Col_LastChange;")).Split(';').Skip(1).All(t => t.Trim().Length > 0));

        Console.WriteLine("--- a column taken off ---");
        ItemFor(chooser, "DataType").Checked = false;
        Pump(50);
        Check("DataType unticked: gone from this tab", !grid.Columns["DataType"].Visible);
        Check("  and from the other tab", !grid2.Columns["DataType"].Visible);
        Check("  kept (Columns.cfg: DataType=...;0)", ColumnsFile(dir).Contains("DataType=") && ColumnsFile(dir).Split('\n').Any(l => l.StartsWith("DataType=") && l.Trim().EndsWith(";0")),
              ColumnsFile(dir).Replace("\r\n", " | "));
        foreach (var n in new[] { "Label", "Format", "Address", "Value", "Changed" }) if (n != "Value") ItemFor(chooser, n).Checked = false;
        Check("never the last one: Value stays", grid.Columns["Value"].Visible && grid.Columns.Cast<DataGridViewColumn>().Count(c => c.Visible) == 1);
        ItemFor(chooser, "Value").Checked = false;
        Check("  unticking it too is refused", grid.Columns["Value"].Visible);
        foreach (var n in new[] { "Label", "Address", "Format", "Changed" }) ItemFor(chooser, n).Checked = true;

        Console.WriteLine("--- widths typed ---");
        var dialog = (Form)typeof(ColumnChooser).GetMethod("NewWidthsDialog", Any).Invoke(chooser, null);
        var widthOf = dialog.GetType().GetMethod("WidthOf", Any);
        Func<string, NumericUpDown> box = n => (NumericUpDown)widthOf.Invoke(dialog, new object[] { n });
        var totalProp = dialog.GetType().GetProperty("Total", Any);
        int shownSum = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width) + (grid.RowHeadersVisible ? grid.RowHeadersWidth : 0);
        Check("the dialog: each column's width, and the whole table's", (decimal)totalProp.GetValue(dialog, null) == shownSum,
              totalProp.GetValue(dialog, null) + " vs " + shownSum);
        box("Label").Value = 200;
        Check("  a width typed: the table's follows", (decimal)totalProp.GetValue(dialog, null) == shownSum - grid.Columns["Label"].Width + 200);
        decimal labelBefore = box("Label").Value, addressBefore = box("Address").Value;
        totalProp.SetValue(dialog, 1000m, null);
        decimal sum = new[] { "Label", "Format", "Address", "Value", "Changed" }.Sum(n => box(n).Value) + (grid.RowHeadersVisible ? grid.RowHeadersWidth : 0);
        Check("  the table's width typed (1000): the shown columns share it", sum == 1000, sum.ToString());
        Check("    as they shared it (Label to Address in the same ratio)", Math.Abs((double)(box("Label").Value / box("Address").Value) - (double)(labelBefore / addressBefore)) < 0.05,
              box("Label").Value + "/" + box("Address").Value);
        Check("    a hidden column is not in it", box("DataType").Enabled == false);
        dialog.GetType().GetMethod("Apply", Any).Invoke(dialog, null);
        dialog.Dispose();
        int tableWidth = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width) + (grid.RowHeadersVisible ? grid.RowHeadersWidth : 0);
        Check("OK: the table is 1000 wide", tableWidth == 1000, tableWidth.ToString());
        Pump(50);
        Check("  the other tab too", grid2.Columns["Label"].Width == grid.Columns["Label"].Width, grid2.Columns["Label"].Width + " vs " + grid.Columns["Label"].Width);
        Check("  kept", ColumnsFile(dir).Contains("Label=" + grid.Columns["Label"].Width + ";"), ColumnsFile(dir).Replace("\r\n", " | "));
        int label1000 = grid.Columns["Label"].Width;
        form.Close(); Application.DoEvents();

        Console.WriteLine("--- opened again ---");
        MonType.GetField("_colWidth", Any).SetValue(null, null);   // as a new start of the program
        var form2 = new SimulationMonitorForm();
        form2.Show(); Application.DoEvents();
        var mon2 = ((IList)Get(form2, "_monitors")).Cast<object>().First(x => (int)Get(x, "Station") == 1);
        ((TabControl)Get(form2, "_tabs")).SelectedIndex = ((IList)Get(form2, "_monitors")).IndexOf(mon2);
        Application.DoEvents();
        var g2 = (DataGridView)Get(Get(mon2, "Current"), "Grid");
        Check("the columns as they were left: DataType off, the widths", !g2.Columns["DataType"].Visible && g2.Columns["Label"].Width == label1000,
              g2.Columns["DataType"].Visible + " / " + g2.Columns["Label"].Width);

        Console.WriteLine("--- fitted, and the defaults ---");
        var ch2 = ChooserOf(g2);
        g2.Columns["Address"].Width = 300;
        ch2.FitToContents();
        Check("fitted to the contents: Address narrows to what is in it", g2.Columns["Address"].Width < 300, g2.Columns["Address"].Width.ToString());
        ItemFor(ch2, "default").PerformClick();
        Pump(50);
        Check("Default columns: all back, the default widths", g2.Columns["DataType"].Visible && g2.Columns["Label"].Width == 280 && g2.Columns["Address"].Width == 90,
              g2.Columns["Label"].Width + "," + g2.Columns["Address"].Width);
        Check("  and kept so", !ColumnsFile(dir).Split('\n').Any(l => l.Trim().EndsWith(";0")), ColumnsFile(dir).Replace("\r\n", " | "));

        Console.WriteLine("--- the Condition column taken off stays off ---");
        ItemFor(ch2, "Condition").Checked = false;
        MachineConditionMap.Clear();
        MachineConditionMap.Register(SystemCondition.ManualMode, 1, "M100", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        var closer = new Timer { Interval = 150 };
        closer.Tick += (s, e) => { foreach (Form f in Application.OpenForms.Cast<Form>().ToList()) if (f.GetType().Name.Contains("MessageBox")) f.Close(); };
        closer.Start();
        Call(form2, "CollectConditions");
        closer.Stop();
        Call(form2, "RefreshValues"); Pump(50);
        Check("a tab with conditions: no Condition column, as asked", !g2.Columns["Condition"].Visible);
        ItemFor(ch2, "Condition").Checked = true;
        Call(form2, "RefreshValues"); Pump(50);
        Check("  ticked again: there", g2.Columns["Condition"].Visible);

        Console.WriteLine("--- Find: Value, Display Format, Modified ---");
        var uses = DeviceCollector.UsesOf(1);
        var word = uses.FirstOrDefault(u => !u.IsAlarm && Get(typeof(SimulationMonitorForm), "FormatDecimal") != null &&
                                            !(bool)MonType.GetMethod("IsBit", Any).Invoke(null, new object[] { u.DataType }) &&
                                            !u.DataType.Contains("String") && !u.DataType.Contains("Float") && u.Address.StartsWith("D"));
        Check("a word device used on the pages (for the test)", word != null, word == null ? "" : word.Address + " " + word.DataType);
        sim.Write(word.Address, 1234);
        if (word.DataType.Contains("Double")) sim.Write(PlcAddressHelper.GetNextAddress(word.Address), 0);
        var find = (DeviceFindForm)Call(form2, "OpenFind", "M672");
        Pump(100);
        Call(form2, "RefreshValues"); Pump(100);
        var list = (DataGridView)Get(find, "_list");
        var cols = list.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText).ToList();
        Check("the columns: Page, Used by, Address, DataType, Value, Display Format, Modified", string.Join(",", cols) == "Page,Used by,Address,DataType,Value,Display Format,Modified",
              string.Join(",", cols));
        Check("M672 found, its value read: 1", list.Rows.Count > 0 && Convert.ToString(list.Rows[0].Cells["Value"].Value) == "1",
              list.Rows.Count == 0 ? "no rows" : Convert.ToString(list.Rows[0].Cells["Value"].Value));
        Check("  a bit has no display format", Convert.ToString(list.Rows[0].Cells["Format"].Value) == "");
        sim.Write("M672", 0);
        Call(form2, "RefreshValues"); Pump(100);
        Check("  it changes: 0, and Modified says when (PLC)", Convert.ToString(list.Rows[0].Cells["Value"].Value) == "0" &&
              Convert.ToString(list.Rows[0].Cells["Modified"].Value).EndsWith("PLC"), Convert.ToString(list.Rows[0].Cells["Modified"].Value));
        Check("  with the monitor's tooltip on it", (list.Rows[0].Cells["Modified"].ToolTipText ?? "").Contains("Changed from 1 to 0"), list.Rows[0].Cells["Modified"].ToolTipText);

        find.Begin(word.Address);
        Call(form2, "RefreshValues"); Pump(100);
        Check("a word: 1234, Decimal", list.Rows.Count > 0 && Convert.ToString(list.Rows[0].Cells["Value"].Value) == "1234" &&
              Convert.ToString(list.Rows[0].Cells["Format"].Value) == "Decimal",
              list.Rows.Count == 0 ? "no rows" : list.Rows[0].Cells["Value"].Value + " / " + list.Rows[0].Cells["Format"].Value);
        var formats = (Dictionary<string, string>)Get(find, "_formats");
        formats[word.Address] = SimulationMonitorForm.FormatHex;
        find.RefreshLive();
        Check("  Hexadecimal chosen: 0x04D2", Convert.ToString(list.Rows[0].Cells["Value"].Value) == "0x04D2" &&
              Convert.ToString(list.Rows[0].Cells["Format"].Value) == "Hexadecimal", Convert.ToString(list.Rows[0].Cells["Value"].Value));
        Check("  the line's menu offers the format", list.ContextMenuStrip != null);

        var fch = ChooserOf(list);
        Check("Find's headings have the columns' menu", fch != null && fch.MenuItems().OfType<ToolStripMenuItem>().Count(i => i.CheckOnClick) == 7);
        ItemFor(fch, "Format").Checked = false;
        Check("  Display Format taken off", !list.Columns["Format"].Visible);
        find.Close(); Application.DoEvents();
        var kept = WindowLayout.Read(Path.Combine(dir, "Windows.cfg"));
        Check("  kept with Find's columns (\":0\" after it)", kept.ContainsKey("Find.Columns") && kept["Find.Columns"].Split(',')[5].EndsWith(":0"),
              kept.ContainsKey("Find.Columns") ? kept["Find.Columns"] : "none");
        var find2 = (DeviceFindForm)Call(form2, "OpenFind", "M672");
        Pump(50);
        Check("  and Find opens so", !((DataGridView)Get(find2, "_list")).Columns["Format"].Visible);
        find2.Close();

        Console.WriteLine("--- Find's columns kept before the new ones came ---");
        var host = new Form();
        var old = new DataGridView { Dock = DockStyle.Fill };
        host.Controls.Add(old);
        foreach (var n in new[] { "Page", "Where", "Address", "DataType", "Value", "Format", "Modified" }) old.Columns.Add(n, n);
        host.Show(); Application.DoEvents();
        WindowLayout.ApplyColumns(old, "310:3,240:2,90:1,160:0");
        // (the old four's own order is .NET's DisplayIndex; Mono renumbers the columns as it likes)
        Check("four kept widths (the old Find): given to the first four, the new ones after them",
              old.Columns["Page"].Width == 310 && old.Columns["DataType"].Width == 160 && old.Columns["Value"].DisplayIndex == 4 && old.Columns["Modified"].DisplayIndex == 6,
              string.Join(",", old.Columns.Cast<DataGridViewColumn>().Select(c => c.Name + ":" + c.DisplayIndex)));

        host.Close();
        form2.Close();
        Console.WriteLine(fail == 0 ? "\nMONITOR COLUMNS ALL PASS" : "\nMONITOR COLUMNS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
