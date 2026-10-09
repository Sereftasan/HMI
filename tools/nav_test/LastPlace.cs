using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Forms;
using Kaban.UI.Settings;

// The program reopens where it was left: the page, its tabs, the side page.
class LP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        for (var t = o.GetType(); t != null; t = t.BaseType) {
            var f = t.GetField(n, Any); if (f != null) return f.GetValue(o);
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o, null);
        }
        return null;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static IEnumerable<Control> All(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; } }

    static MainForm Start(string user, SecurityLevel level) {
        SecurityManager.Login(user, level);
        var form = new MainForm { RememberPlace = true };
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login(user, level);   // the shell starts signed out
        form.Show();
        Pump(400);
        return form;
    }
    static MachineRegistry.RoleGroup GroupOf(MainForm f) {
        var groups = (System.Collections.IDictionary)Get(f, "_screenGroups");
        var cur = Get(f, "_currentScreen");
        return cur == null ? null : (MachineRegistry.RoleGroup)groups[cur];
    }
    static UserControl Nav(MainForm f, string role) {
        var groups = (System.Collections.IDictionary)Get(f, "_screenGroups");
        return groups.Keys.Cast<UserControl>().First(k => ((MachineRegistry.RoleGroup)groups[k]).Role == role);
    }
    static string Chain(MainForm f) {
        var chain = (System.Collections.IList)typeof(SettingsScreen).GetMethod("CaptureChain", Any).Invoke(null, new object[] { Get(f, "_currentScreen") });
        return string.Join(" > ", chain.Cast<object>().Select(kv => ((ISettingsPage)kv.GetType().GetProperty("Value").GetValue(kv, null)).TitleFallback));
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "lp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        ViewPreferences.Reload();
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 }) PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));

        Console.WriteLine("--- a test's shell remembers nothing ---");
        Check("RememberPlace is off unless the program itself starts", !new MainForm().RememberPlace);

        Console.WriteLine("--- first run: HMI Settings > PLC Connections > Connection events, the monitor open ---");
        var a = Start("admin", SecurityLevel.Admin);
        a.LoadScreen(Nav(a, ScreenRoles.HmiSettings));
        Pump(50);
        var hmi = (SettingsScreen)Get(a, "_currentScreen");
        var tabs = All(hmi).OfType<SettingsScreen>().FirstOrDefault(sc => sc.AllPages.Any(p => p.TitleKey == "Set_StationConn")) ?? hmi;
        typeof(SettingsScreen).GetMethod("ShowPage", Any).Invoke(tabs, new object[] { tabs.AllPages.First(p => p.TitleKey == "Set_StationConn") });
        Pump(50);
        var conn = All(a).OfType<SettingsScreen>().First(sc => sc.AllPages.Any(p => p.TitleFallback == "Connection events"));
        typeof(SettingsScreen).GetMethod("ShowPage", Any).Invoke(conn, new object[] { conn.AllPages.First(p => p.TitleFallback == "Connection events") });
        a.RecordLocation();
        a.ShowSidePage("PlcMonitor");
        Pump(100);
        string before = Chain(a);
        Check("there", before.Contains("PLC Connections > Connection events"), before);
        a.Close();
        Pump(200);
        var file = Path.Combine(dir, "LastPage.cfg");
        Check("kept in LastPage.cfg beside SidePanel.cfg", File.Exists(file), file);

        Console.WriteLine("--- the next start ---");
        var b = Start("admin", SecurityLevel.Admin);
        Check("opens on HMI Settings", GroupOf(b) != null && GroupOf(b).Role == ScreenRoles.HmiSettings, GroupOf(b) == null ? "none" : GroupOf(b).TitleFallback);
        Check("  on the same tabs", Chain(b) == before, Chain(b));
        var open = Get(b, "_sideOpen");
        Check("  with the side page open again", open != null && (string)open.GetType().GetField("Key").GetValue(open) == "PlcMonitor");

        Console.WriteLine("--- a page above the level of the one signed in waits for the sign-in ---");
        b.CloseSidePanel();
        b.LoadScreen(Nav(b, ScreenRoles.MachineParameters));
        Pump(50);
        b.Close();
        Pump(200);
        var c = Start("op", SecurityLevel.Operator);
        Check("an operator starts on the default page, no refusal shown", GroupOf(c) != null && GroupOf(c).Role != ScreenRoles.MachineParameters,
              GroupOf(c) == null ? "none" : GroupOf(c).TitleFallback);
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        Pump(200);
        Check("  Maintenance signs in: Machine Parameters, as left", GroupOf(c) != null && GroupOf(c).Role == ScreenRoles.MachineParameters,
              GroupOf(c) == null ? "none" : GroupOf(c).TitleFallback);
        c.Close();
        Pump(200);

        var d = Start("op", SecurityLevel.Operator);
        d.LoadScreen(Nav(d, ScreenRoles.Alarms));
        Pump(50);
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        Pump(200);
        Check("  but not when the operator has gone elsewhere meanwhile", GroupOf(d).Role == ScreenRoles.Alarms, GroupOf(d).TitleFallback);
        d.Close();
        Pump(100);

        // View Preferences > Sign-in and alerts > After sign-in.
        Console.WriteLine("--- where a sign-in starts (View Preferences) ---");
        ViewPreferences.SetUser("ali", ViewPreferences.StartPage, "Home");
        var e = Start("veli", SecurityLevel.Operator);
        e.LoadScreen(Nav(e, ScreenRoles.Alarms));
        e.RecordLocation();
        Pump(100);
        SecurityManager.Login("ali", SecurityLevel.Operator);
        Pump(300);
        Check("Home: ali signs in, the first page", GroupOf(e) != null && GroupOf(e).Role == ScreenRoles.Home, GroupOf(e) == null ? "none" : GroupOf(e).TitleFallback);
        e.LoadScreen(Nav(e, ScreenRoles.HmiSettings));
        e.RecordLocation();
        Pump(100);
        Check("  where he goes, kept as his", ViewPreferences.UserPlace("ali").Count > 0, string.Join(";", ViewPreferences.UserPlace("ali").Select(kv => kv.Key + "=" + kv.Value)));
        SecurityManager.Login("veli", SecurityLevel.Operator);
        Pump(300);
        Check("Stay (the default): veli signs in, the page stays", GroupOf(e).Role == ScreenRoles.HmiSettings, GroupOf(e).TitleFallback);
        e.LoadScreen(Nav(e, ScreenRoles.Alarms));
        e.RecordLocation();
        Pump(100);
        ViewPreferences.SetUser("ali", ViewPreferences.StartPage, "Last");
        SecurityManager.Login("ali", SecurityLevel.Operator);
        Pump(300);
        Check("My last page: ali back on HMI Settings", GroupOf(e).Role == ScreenRoles.HmiSettings, GroupOf(e).TitleFallback);
        e.Close();
        Pump(100);

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nLAST PLACE ALL PASS" : "\nLAST PLACE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
