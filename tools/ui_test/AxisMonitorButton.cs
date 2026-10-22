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

// 22 October: "the Axis Monitor's navigation button should only be visible when there is at least an axis
// set on the settings pages - not when there is no axis to be monitored (e.g. the H500 project)".
class AMB {
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
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }

    /// <summary>A machine of pages without an axis (H500's kind) - or, with axes, an Axis Settings page of one row.</summary>
    class Mod : IMachineModule {
        public string Id; public bool Axes;
        public string ModuleId { get { return Id; } }
        public string DisplayNameKey { get { return Id; } }
        public void RegisterPlc(IMachineMountContext ctx) { ctx.Station(1); }
        public void RegisterConditions(IMachineMountContext ctx) { }
        public void RegisterData(IMachineMountContext ctx) { }
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx) {
            yield return new MachineScreen { TitleKey = "Home", TitleFallback = "Home", Role = ScreenRoles.Home, SortOrder = 1,
                                             CreateView = () => { var p = new UserControl(); p.Controls.Add(new LampHMI { PlcNumber = 1, Address = "M1" }); return p; } };
            if (Axes)
                yield return new MachineScreen { TitleKey = Id + " Axes", TitleFallback = Id + " Axes", Role = ScreenRoles.Manual, SortOrder = 2,
                                                 CreateView = () => { var p = new UserControl(); p.Controls.Add(new AxisSettingHMI { AxisNameKey = "X", PlcNumber = 1, PositionAddress = "D0" }); return p; } };
            if (ctx.IsMainMachine)
                yield return new MachineScreen { TitleKey = "HMI Settings", TitleFallback = "HMI Settings", Role = ScreenRoles.HmiSettings, SortOrder = 9, CreateView = () => new UserControl() };
        }
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "amb_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        UnitManager.EnsureDefaults();
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod { Id = "NOAX" }, null, dir);
        PlcManager.Instance.RegisterDriver(1, new Kaban.Drivers.Simulation.SimulationDriver(1));

        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show();
        Pump(300);
        AlarmHelpLinks.DiscoverAll();
        Pump(300);
        var buttons = (Dictionary<string, Button>)Get(form, "_sideButtons");
        Check("a machine with no axis (H500's kind): no Axis Monitor button - Inputs / Outputs and the PLC Monitor still",
              AlarmHelpLinks.IsDiscovered && AlarmHelpLinks.AllAxes.Count == 0 && !buttons.ContainsKey("AxisMonitor") && buttons.ContainsKey("IoMonitor") && buttons.ContainsKey("PlcMonitor"),
              string.Join(",", buttons.Keys));

        MachineRegistry.MountSub(new Mod { Id = "AX", Axes = true }, new[] { 1 }, dir);
        AlarmHelpLinks.DiscoverAll();
        Pump(400);
        buttons = (Dictionary<string, Button>)Get(form, "_sideButtons");
        Check("an axis on an Axis Settings page: the button, once the pages were searched", AlarmHelpLinks.AllAxes.Count == 1 && buttons.ContainsKey("AxisMonitor"),
              AlarmHelpLinks.AllAxes.Count + " axes; " + string.Join(",", buttons.Keys));

        form.Close();
        MachineRegistry.Reset();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "AXIS MONITOR BUTTON ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
