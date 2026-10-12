using System;
using System.Collections.Generic;
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

// The Axis Monitor side page, on the real FA2010 program: every axis of the
// Axis Settings pages, nothing added by hand; name, position, current as the
// row draws them; hidden while the row's conditions are not met.
class AM {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "am_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 }) PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));

        Console.WriteLine("--- a side page for everyone ---");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show();
        Pump(300);
        var page = SidePages.Find("AxisMonitor");
        Check("registered: 'Axis Monitor', no level needed (watching only)", page != null && page.Title == "Axis Monitor" && page.RequiredLevel == SecurityLevel.None,
              page == null ? "none" : page.Title + " " + page.RequiredLevel);
        var view = form.ShowSidePage("AxisMonitor") as AxisMonitorPage;
        Pump(100);
        Check("opens beside the page", view != null && view.Visible);

        Console.WriteLine("--- the axes come from the Axis Settings pages ---");
        var rows = (IList<AxisMonitorRow>)typeof(AxisMonitorPage).GetProperty("Rows", Any).GetValue(view, null);
        var fa = rows.Where(r => r.PlcNumber == 1 || r.PlcNumber == 2).ToList();
        Check("the 17 FA2010 axes, in the page's order", fa.Count == 17 && fa[0].NameKey == "Axis_RobotX", fa.Count + " first " + (fa.Count > 0 ? fa[0].NameKey : ""));
        Check("  and H800's six (station 4)", rows.Count(r => r.PlcNumber == 4) == 6, rows.Count(r => r.PlcNumber == 4).ToString());
        var x = fa[0];
        var pos = (NumericDisplayHMI)typeof(AxisMonitorRow).GetField("Position", Any).GetValue(x);
        var cur = (MeterHMI)typeof(AxisMonitorRow).GetField("Current", Any).GetValue(x);
        Check("Robot X: position W0 as on the row (divisor, decimals, mm)", pos.Address == "W0" && pos.Divisor == 1000 && pos.DecimalPlaces == 2 && pos.Suffix == "mm" &&
              pos.Width == AxisSettingHMI.WPosition && pos.Height == AxisSettingHMI.RowHeight - 8, pos.Address + " " + pos.Divisor + " " + pos.Size);
        Check("  its current W0A, the row's meter", cur != null && cur.Address == "W0A" && cur.Width == AxisSettingHMI.WMeter && cur.Left == pos.Right + AxisSettingHMI.Gap);
        var h800 = rows.First(r => r.PlcNumber == 4);
        Check("an axis without a current: no meter", typeof(AxisMonitorRow).GetField("Current", Any).GetValue(h800) == null);
        var headings = view.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).Concat(view.Controls.Cast<Control>())
                           .SelectMany(c => c.Controls.OfType<Label>()).Select(l => l.Text).ToList();
        Check("grouped by machine: 'Processing Center' and '3D Hinge Drilling' headings",
              headings.Contains("Processing Center") && headings.Contains("3D Hinge Drilling"), string.Join(" | ", headings.Take(8)));

        Console.WriteLine("--- shown while the row's conditions are met ---");
        var info = new AlarmHelpLinks.AxisInfo { Station = 1, NameKey = "Axis_RobotX", Position = "W0", Divisor = 1, Visibility = SystemCondition.SafetyOK };
        var row = new AxisMonitorRow(info);
        Check("a row is an IConditionalControl on its own station", row is IConditionalControl && ((IConditionalControl)row).PlcNumber == 1);
        row.UpdateState(a => 0, c => false);
        Check("conditions not met: hidden", !row.Visible);
        row.UpdateState(a => 0, c => true);
        Check("met: shown", row.Visible);
        var axisRow = new AxisSettingHMI { AxisNameKey = "X", PositionAddress = "W0", VisibilityConditions = SystemCondition.SafetyOK };
        axisRow.UpdateState(a => 0, c => false);
        Check("the Axis Settings row follows the same conditions", !axisRow.Visible);

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nAXIS MONITOR ALL PASS" : "\nAXIS MONITOR " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
