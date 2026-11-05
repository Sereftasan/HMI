using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Forms;

// 5 November: "It had deleted the labels I created on the PLC Monitor page when I changed the view scale the other day, there
// should be a bug for it". What the panel's log and the lists' files showed: the screen scale restarts the program; a name
// typed and not yet confirmed (its cell still open - the top bar's scale menu and its question do not take the focus from the
// cell) was not kept, and a second instance (one restarted outside Visual Studio, another started from it) wrote the list it
// had over the other's.
class MLK {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
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
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length >= a.Length &&
                                              m.GetParameters().Skip(a.Length).All(p => p.IsOptional));
        var args = a.Concat(mi.GetParameters().Skip(a.Length).Select(p => p.DefaultValue)).ToArray();
        return mi.Invoke(o is Type ? null : o, args);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "monkept_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(21);
        PlcManager.Instance.RegisterDriver(21, sim);
        PlcManager.Instance.RegisterStation(21, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(21, true);
        sim.Write("D10", 1234);
        var ep = PlcConnectionSettings.Endpoints().First(e => e.StationId == 21);
        string path = (string)Call(MonType, "WatchPath", ep);
        File.WriteAllLines(path, new[] { "Label;DataType;Address;Value", "#tabs:1", "#sheet:Main", "Start;Bit;M5", "Speed;Word [Signed];D10" });

        var form = new SimulationMonitorForm();
        form.Show(); Pump(200);
        var mon = ((IList)Get(form, "_monitors")).Cast<object>().First(m => (int)Get(m, "Station") == 21);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = ((IList)Get(form, "_monitors")).IndexOf(mon);
        var sh = ((IList)Get(mon, "Sheets"))[0];
        var grid = (DataGridView)Get(sh, "Grid");
        Pump(100);

        Console.WriteLine("--- a name typed, its cell still open, when the program restarts ---");
        grid.Focus();
        grid.CurrentCell = grid.Rows[0].Cells["Label"];
        grid.BeginEdit(true); Pump(50);
        if (grid.EditingControl != null) grid.EditingControl.Text = "Start button";
        Check("the name being typed (its cell open)", grid.IsCurrentCellInEditMode);
        form.SaveLists(); Pump(50);
        var text = File.ReadAllText(path);
        Check("kept as Enter would have: the list's file has it", text.Contains("Start button;Bit;M5"), text);

        Console.WriteLine("--- a value typed, its cell still open ---");
        grid.CurrentCell = grid.Rows[1].Cells["Value"];
        grid.BeginEdit(true); Pump(50);
        if (grid.EditingControl != null) grid.EditingControl.Text = "9999";
        form.SaveLists(); Pump(50);
        int d10; sim.TryRead("D10", out d10);
        Check("dropped: nothing written to the PLC by closing", d10 == 1234 && !grid.IsCurrentCellInEditMode, d10.ToString());

        Console.WriteLine("--- another instance wrote the list meanwhile ---");
        var copies = Directory.GetFiles(dir, Path.GetFileNameWithoutExtension(path) + ".*.csv");
        Check("none kept aside so far (only this monitor wrote it)", copies.Length == 0, string.Join(", ", copies.Select(Path.GetFileName)));
        System.Threading.Thread.Sleep(1100);
        File.WriteAllLines(path, new[] { "Label;DataType;Address;Value", "#tabs:1", "#sheet:Main", "Other's label;Bit;M77" });
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
        form.SaveLists(); Pump(50);
        copies = Directory.GetFiles(dir, Path.GetFileNameWithoutExtension(path) + ".*.csv");
        Check("this monitor's list written - the other's kept beside it first, not lost",
              File.ReadAllText(path).Contains("Start button;Bit;M5") && copies.Length == 1 && File.ReadAllText(copies[0]).Contains("Other's label;Bit;M77"),
              string.Join(", ", copies.Select(Path.GetFileName)));
        form.SaveLists(); Pump(50);
        Check("  saved again (nobody else wrote it): no other copy", Directory.GetFiles(dir, Path.GetFileNameWithoutExtension(path) + ".*.csv").Length == 1);
        form.Close();
        Console.WriteLine(fail == 0 ? "MONITOR LISTS KEPT ALL PASS" : "MONITOR LISTS KEPT " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
