using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Settings;

// 22 October: "we should add an activation checkbox for each PLC in HMI Settings / PLC Connection. The machine
// pages are only shown (on the navigation bar, the PLC Monitor ...) when any PLC of the related machine is activated".
class PA {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { d = d ?? ""; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) { var t = o.GetType(); var pr = t.GetProperty(n, Any); return pr != null ? pr.GetValue(o, null) : t.GetField(n, Any).GetValue(o); }

    class Mod : IMachineModule {
        public string Id; public int Plcs = 1;
        public string ModuleId { get { return Id; } }
        public string DisplayNameKey { get { return Id; } }
        public void RegisterPlc(IMachineMountContext ctx) { for (int i = 1; i <= Plcs; i++) ctx.Station(i); }
        public void RegisterConditions(IMachineMountContext ctx) { }
        public void RegisterData(IMachineMountContext ctx) { }
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx)
        {
            yield return new MachineScreen { TitleKey = "Home", TitleFallback = "Home", Role = ScreenRoles.Home, SortOrder = 1, CreateView = () => new UserControl() };
            yield return new MachineScreen { TitleKey = Id + " Manual", TitleFallback = Id + " Manual", Role = ScreenRoles.Manual, SortOrder = 2, CreateView = () => new UserControl() };
            if (ctx.IsMainMachine)
                yield return new MachineScreen { TitleKey = "HMI Settings", TitleFallback = "HMI Settings", Role = ScreenRoles.HmiSettings, SortOrder = 9, CreateView = () => new UserControl() };
        }
    }

    static List<string> Screens() { return MachineRegistry.AllScreens().Select(kv => kv.Key.Context.ModuleId + ":" + kv.Value.TitleFallback).ToList(); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "pa_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        PlcConnectionSettings.SettingsPath = Path.Combine(dir, "PlcConnections.cfg");
        PlcDriverCatalog.RegisterNone();
        PlcDriverCatalog.Register(PlcDriverCatalog.SimulationId, "Drv_Simulation", "Simulation (in-memory)",
                                  (station, endpoint) => new Kaban.Drivers.Simulation.SimulationDriver(station), usesLogicalStation: false);
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod { Id = "MAIN", Plcs = 2 }, null, dir);
        MachineRegistry.MountSub(new Mod { Id = "SUB" }, new[] { 3 }, dir);
        SecurityManager.Login("admin", SecurityLevel.Admin);

        Console.WriteLine("--- saved: a PLC not active ---");
        var eps = PlcConnectionSettings.Endpoints();
        Func<string, PlcConnectionSettings.Endpoint> ep = k => eps.First(e => e.Key == k);
        Check("every PLC active at first", eps.Count == 3 && eps.All(PlcConnectionSettings.IsActive), string.Join(", ", eps.Select(e => e.Key)));
        PlcConnectionSettings.Set(ep("MAIN|1"), PlcDriverCatalog.SimulationId, 1, "");
        PlcConnectionSettings.Set(ep("MAIN|2"), PlcDriverCatalog.SimulationId, 2, "", active: false);
        PlcConnectionSettings.Set(ep("SUB|1"), PlcDriverCatalog.SimulationId, 3, "", active: false);
        Check("saved: ';off' after the address", PlcConnectionSettings.Save() && File.ReadAllLines(PlcConnectionSettings.SettingsPath).Contains("SUB|1=Simulation;3;;off") &&
              File.ReadAllLines(PlcConnectionSettings.SettingsPath).Contains("MAIN|1=Simulation;1;"));
        PlcConnectionSettings.Load();
        Check("  read back", !PlcConnectionSettings.IsActive(ep("SUB|1")) && !PlcConnectionSettings.IsActive(ep("MAIN|2")) && PlcConnectionSettings.IsActive(ep("MAIN|1")) &&
              PlcConnectionSettings.For(ep("SUB|1")).LogicalStation == 3 && PlcConnectionSettings.For(ep("SUB|1")).Endpoint == "");
        File.WriteAllText(PlcConnectionSettings.SettingsPath, "SUB|1=Siemens;1;192.168.0.1:0:1;off\r\nMAIN|2=Siemens;1;192.168.0.2:0:1\r\n");
        PlcConnectionSettings.Load();
        Check("  an address kept with it; a line of before (no 'off') active", !PlcConnectionSettings.IsActive(ep("SUB|1")) && PlcConnectionSettings.For(ep("SUB|1")).Endpoint == "192.168.0.1:0:1" &&
              PlcConnectionSettings.IsActive(ep("MAIN|2")) && PlcConnectionSettings.For(ep("MAIN|2")).Endpoint == "192.168.0.2:0:1");

        Console.WriteLine("--- the start: not connected, its machine's pages not shown ---");
        PlcConnectionSettings.Set(ep("MAIN|1"), PlcDriverCatalog.SimulationId, 1, "");
        PlcConnectionSettings.Set(ep("MAIN|2"), PlcDriverCatalog.SimulationId, 2, "", active: false);
        PlcConnectionSettings.Set(ep("SUB|1"), PlcDriverCatalog.SimulationId, 3, "", active: false);
        PlcConnectionSettings.Save();
        Check("before the start: every page", Screens().Count == 5, string.Join(", ", Screens()));
        int opened = PlcConnectionSettings.ApplyAll();
        Check("only the active PLC connected", opened == 1 && PlcManager.Instance.GetDriverForMonitoring(1) != null && PlcManager.Instance.GetDriverForMonitoring(2) == null &&
              PlcManager.Instance.GetDriverForMonitoring(3) == null && PlcConnectionSettings.RunsInactive(2) && PlcConnectionSettings.RunsInactive(3) && !PlcConnectionSettings.RunsInactive(1));
        Check("SUB (its one PLC not active): not shown - MAIN (one of its two active) shown",
              MachineRegistry.IsHidden(MachineRegistry.Modules.First(m => m.Context.ModuleId == "SUB")) && !MachineRegistry.IsHidden(MachineRegistry.Main) &&
              MachineRegistry.ShownModules.Count() == 1);
        Check("  its pages gone from every list: Home a page of MAIN alone (no machine tabs), no SUB Manual",
              string.Join(", ", Screens()) == "MAIN:Home, MAIN:MAIN Manual, MAIN:HMI Settings" &&
              !MachineRegistry.NavigationGroups().First(g => g.Role == ScreenRoles.Home).IsTabbed, string.Join(", ", Screens()));
        Check("  the PLC Monitor: the active PLCs' tabs", PlcConnectionSettings.ActiveEndpoints().Select(e => e.Key).SequenceEqual(new[] { "MAIN|1" }));

        // every PLC of the main machine not active: the HMI's settings stay
        PlcConnectionSettings.Set(ep("MAIN|1"), PlcDriverCatalog.SimulationId, 1, "", active: false);
        PlcConnectionSettings.ApplyAll();
        Check("no PLC active at all: only the HMI's own settings", string.Join(", ", Screens()) == "MAIN:HMI Settings", string.Join(", ", Screens()));
        PlcConnectionSettings.Set(ep("MAIN|1"), PlcDriverCatalog.SimulationId, 1, "");
        PlcConnectionSettings.Save();
        PlcConnectionSettings.ApplyAll();

        Console.WriteLine("--- HMI Settings > PLC Connection ---");
        var form = new Form { Width = 1300, Height = 500 };
        var page = new StationConnectionControl();
        form.Controls.Add(page);
        form.Show(); Application.DoEvents();
        var rows = (System.Collections.IList)Get(page, "_rows");
        Func<int, CheckBox> active = i => (CheckBox)Get(rows[i], "Active");
        Func<int, ComboBox> driver = i => (ComboBox)Get(rows[i], "Driver");
        Func<int, Label> state = i => (Label)Get(rows[i], "Status");
        var save = (Button)Get(page, "_btnSave");
        Check("each PLC's name ticked when active; a PLC not active: its driver greyed, 'Not active'",
              rows.Count == 3 && active(0).Checked && !active(1).Checked && !active(2).Checked && active(0).Text.Contains("MAIN") &&
              driver(0).Enabled && !driver(2).Enabled && state(2).Text == "Not active" && !save.Enabled, state(2).Text + " " + save.Enabled);
        active(2).Checked = true; Application.DoEvents();
        Check("ticked: its driver to choose, Save offered", driver(2).Enabled && save.Enabled);
        active(2).Checked = false; Application.DoEvents();
        Check("  back as saved: nothing to save", !save.Enabled);
        active(1).Checked = true; Application.DoEvents();
        typeof(StationConnectionControl).GetMethod("Reload", Any).Invoke(page, null); Application.DoEvents();
        Check("  Reload: as saved again", !active(1).Checked && !save.Enabled);
        active(1).Checked = true; Application.DoEvents();
        Kaban.UI.Forms.MainForm.RestartNotice = (owner, text) => { };
        typeof(StationConnectionControl).GetMethod("Save", Any).Invoke(page, null);
        PlcConnectionSettings.Load();
        Check("Save: kept (from the next start)", PlcConnectionSettings.IsActive(ep("MAIN|2")) && !PlcConnectionSettings.IsActive(ep("SUB|1")) &&
              File.ReadAllLines(PlcConnectionSettings.SettingsPath).Contains("MAIN|2=Simulation;2;") && PlcConnectionSettings.RunsInactive(2));

        form.Close();
        MachineRegistry.Reset();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "PLC ACTIVE ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
