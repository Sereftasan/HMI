using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;
using Kaban.UI.Screens;

// 12 October: "Axis monitor second page down scroll doesn't move to right end again now" - at a larger
// screen scale (1.5) an Admin's Axis Monitor rows (Axis Settings rows with the Motion monitor's columns)
// were built on the scaled side page with the width of factor 1: the list's scroll ended a third short of
// their right end. The same for an Axis Settings row built again on a scaled page (its picks changed).
class AMS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static int ContentRight(Control row) { return row.Controls.Cast<Control>().Where(k => k.Visible).Max(k => k.Right); }

    [STAThread]
    static void Main() {
        UiScale.Override(1.5);
        string dir = Path.Combine(Path.GetTempPath(), "ams_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 }) PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(0, 0); form.Size = new Size(1280, 1000);
        form.Show();
        Pump(500);

        Console.WriteLine("--- the Axis Monitor in the side panel at 1.5, an Admin's Motion monitor columns ---");
        var view = form.ShowSidePage("AxisMonitor") as AxisMonitorPage;
        Pump(400);
        ViewPreferences.SetList(ViewPreferences.MonitorMotion, new[] { "AxisError", "OperationStatus", "Busy", "RealCurrentValue" });
        Pump(700);
        var list = (FlowLayoutPanel)typeof(AxisMonitorPage).GetField("_list", Any).GetValue(view);
        var rows = list.Controls.OfType<AxisSettingHMI>().ToList();
        Check("the rows are the Axis Settings rows, wider than the panel", rows.Count > 0 && rows[0].Width > list.ClientSize.Width,
              rows.Count + " rows, " + (rows.Count > 0 ? rows[0].Width : 0) + " in " + list.ClientSize.Width);
        int widest = rows.Max(r => ContentRight(r));
        Check("  as wide as their columns at the scale (not the width of factor 1), all alike", rows.All(r => r.Width == rows[0].Width && r.Width >= ContentRight(r)) &&
              rows[0].Width - widest <= UiScale.Px(8), rows[0].Width + " for " + widest);
        list.AutoScrollPosition = new Point(100000, 0);
        Pump(400);
        var first = rows[0];
        int right = first.RectangleToScreen(new Rectangle(0, 0, ContentRight(first), 1)).Right;
        int visible = list.RectangleToScreen(list.ClientRectangle).Right;
        Check("scrolled to the end: the last column in sight", right <= visible, right + " <= " + visible);
        var head = ((Panel)typeof(AxisMonitorPage).GetField("_rowsHead", Any).GetValue(view)).Controls[0];
        Check("  its heading as wide as the rows, following them", head.Width == first.Width && head.Left == first.Left, head.Bounds + " / " + first.Bounds);

        Console.WriteLine("--- an Axis Settings row built again on the scaled page ---");
        var page = new Kaban.Machines.FA2010.Screens.Parameters.AxisSettings { Dock = DockStyle.Fill };
        var host = new Form { ClientSize = new Size(1270, 700) };
        host.Controls.Add(page);
        UiScale.ApplyOnLoad(host);
        host.Show(); Pump(800);
        var frame = AxisPageFrame.Of(page);
        var axis = frame.Body.Controls.OfType<AxisSettingHMI>().First();
        int before = axis.Width;
        axis.BuildRow();
        Pump(200);
        Check("the same width as before, its columns' at the scale", axis.Width == before && axis.Width >= ContentRight(axis), before + " -> " + axis.Width);
        Check("  the frame scrolls as far as its right end", frame.Body.DisplayRectangle.Width >= axis.Left - frame.Body.AutoScrollPosition.X + axis.Width - 1,
              frame.Body.DisplayRectangle.Width + " for " + axis.Width);
        host.Close();

        form.Close();
        Console.WriteLine(fail == 0 ? "\nAXIS MONITOR SCALE ALL PASS" : "\nAXIS MONITOR SCALE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
