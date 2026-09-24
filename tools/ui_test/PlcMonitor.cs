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
        return t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length).Invoke(o is Type ? null : o, a);
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

        Console.WriteLine("--- removing from the simulator's list keeps it removed ---");
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

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nPLC MONITOR ALL PASS" : "\nPLC MONITOR " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
