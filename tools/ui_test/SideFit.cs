using System;
using System.Collections.Generic;
using System.Drawing;
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
using Kaban.UI.Screens;

// "The left side border double-click doesn't work on the PLC Monitor page very
// well: it cannot find the proper required space for the page (it seems to take
// the digital I/O page's limits even when the PLC Monitor page is open). It
// should consider the Find page as well (only if it is open)."
class SF {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
        return t.GetProperty(n, Any).GetValue(o is Type ? null : o, null);
    }
    static IEnumerable<Control> AllOf(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in AllOf(c)) yield return d; } }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "side_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));

        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 })
            PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));
        PlcLabelTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));

        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show();
        Pump(200);
        form.WindowState = FormWindowState.Normal;
        form.Bounds = new Rectangle(0, 0, 1920, 1040);
        Pump(200);
        var panel = (Panel)Get(form, "_sidePanel");
        var split = (Splitter)Get(form, "_sideSplitter");
        Func<DataGridView, int> cols = g => g.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width);

        Console.WriteLine("--- Inputs / Outputs first, then the PLC Monitor at the same width ---");
        form.ShowSidePage("IoMonitor"); Pump(200);
        form.FitSidePanel(); Pump(100);
        int io = panel.Width;
        var monitor = form.ShowSimulationMonitor(); Pump(200);
        Check("the PLC Monitor opens at the width the panel had (Inputs / Outputs')", panel.Width == io, panel.Width + " vs " + io);
        var grid = AllOf(monitor).OfType<DataGridView>().First(g => g.Visible);
        Check("  narrower than its table: columns cut off", grid.ClientSize.Width < cols(grid), grid.ClientSize.Width + " < " + cols(grid));

        Console.WriteLine("--- the border double-clicked: the PLC Monitor's own width ---");
        // (as a double-click event: a press handed to Mono's splitter starts its
        // own split, which moves the wrong neighbour - not what Windows does)
        typeof(Control).GetMethod("OnMouseDoubleClick", Any).Invoke(split, new object[] { new MouseEventArgs(MouseButtons.Left, 2, 2, 300, 0) });
        Pump(200);
        Check("every column of the table in sight, not Inputs / Outputs' width", panel.Width != io && grid.ClientSize.Width >= cols(grid) + SystemInformation.VerticalScrollBarWidth,
              "panel " + panel.Width + " (I/O " + io + "), grid " + grid.ClientSize.Width + " for " + cols(grid));
        Check("  and no wider than that", grid.ClientSize.Width <= cols(grid) + SystemInformation.VerticalScrollBarWidth + 30, grid.ClientSize.Width + " for " + cols(grid));
        int table = panel.Width;
        grid.Columns["Label"].Width += 150; Pump(50);
        form.FitSidePanel(); Pump(100);
        Check("a column widened: the panel follows", Math.Abs(panel.Width - (table + 150)) <= 2, panel.Width + " vs " + (table + 150));
        grid.Columns["Label"].Width -= 150; Pump(50);
        form.FitSidePanel(); Pump(100);

        Console.WriteLine("--- with Find open in the monitor: Find's columns too ---");
        var find = (DeviceFindForm)monitor.GetType().GetMethod("OpenFind", Any).Invoke(monitor, new object[] { "M672" });
        Pump(300);
        var list = AllOf(find).OfType<DataGridView>().First();
        Check("Find's list is wider than the table (the case to fit)", cols(list) > cols(grid), cols(list) + " vs " + cols(grid));
        form.FitSidePanel(); Pump(100);
        Check("  fitted: every column of Find in sight", list.ClientSize.Width >= cols(list) + SystemInformation.VerticalScrollBarWidth && panel.Width > table,
              "panel " + panel.Width + ", Find " + list.ClientSize.Width + " for " + cols(list));
        Check("  and no wider than that", list.ClientSize.Width <= cols(list) + SystemInformation.VerticalScrollBarWidth + 30, list.ClientSize.Width + " for " + cols(list));
        find.GetType().GetMethod("CloseEmbedded", Any).Invoke(find, null);
        Pump(100);
        form.FitSidePanel(); Pump(100);
        Check("Find closed: back to the table's width", Math.Abs(panel.Width - table) <= 2, panel.Width + " vs " + table);

        Console.WriteLine("--- back to Inputs / Outputs: its own width again ---");
        form.ShowSidePage("IoMonitor"); Pump(200);
        form.FitSidePanel(); Pump(100);
        Check("Inputs / Outputs fitted to itself", panel.Width == io, panel.Width + " vs " + io);

        Console.WriteLine(fail == 0 ? "\nSIDE FIT ALL PASS" : "\nSIDE FIT " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
