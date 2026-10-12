using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Dashboard;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 12 October: "User setting, Dashboard and H500 pages Turkish translations are missing" (two screenshots:
// Users and Preferences and the Dashboard in English, the flag on TR). Their texts were there: the pages
// had been built in English - BuildNavigation built them before the window, loading, read the shared
// texts and put the kept language on the screen - and a page that puts its texts on when it is built
// kept them. The language now comes before the pages; such a page is built again after a change (the
// one on show at once, on the user it showed), and the Dashboard follows one before it is first shown.
class LS {
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
    static UserControl Nav(MainForm f, string role) {
        var groups = (System.Collections.IDictionary)Get(f, "_screenGroups");
        return groups.Keys.Cast<UserControl>().First(k => ((MachineRegistry.RoleGroup)groups[k]).Role == role);
    }
    static T Find<T>(Control root) where T : Control { return root is T ? (T)root : All(root).OfType<T>().FirstOrDefault(); }
    static string Header(UsersPreferencesControl p) { return p.Users.Columns.Count > 0 ? p.Users.Columns[0].HeaderText : ""; }
    static string Side(DashboardScreen d) {
        var caps = (System.Collections.IEnumerable)Get(d, "_sideCaptions");
        return string.Join(" | ", caps.Cast<Label>().Select(l => l.Text));
    }
    static SettingsScreen HmiTabs(MainForm f) {
        // the HMI Settings button's page: its tabs (Users and Preferences, Logs and Reports ...)
        return All(Nav(f, ScreenRoles.HmiSettings)).OfType<SettingsScreen>().LastOrDefault() ?? Nav(f, ScreenRoles.HmiSettings) as SettingsScreen;
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "ls_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        ViewPreferences.Reload();
        // everyone's language kept from the last run (the flag on the top bar): Turkish
        ViewPreferences.SetPanel(ViewPreferences.Language, "TR");
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 }) PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));

        Console.WriteLine("--- the program starting, the kept language Turkish ---");
        Check("before the window: the shared texts not read yet, English on the screen", !LanguageManager.HasCommonTranslations() && LanguageManager.CurrentLanguage == "EN");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        Check("the pages built: in Turkish - the texts read and the language put in force first",
              LanguageManager.HasCommonTranslations() && LanguageManager.CurrentLanguage == "TR", LanguageManager.CurrentLanguage);
        var dash = Find<DashboardScreen>(Nav(form, ScreenRoles.Dashboard));
        Check("  the Dashboard's side in Turkish (Otomatik-Rapor Geçmişi ...)", dash != null && Side(dash).StartsWith("Otomatik-Rapor Geçmişi"), dash == null ? "no dashboard" : Side(dash));
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);   // the shell starts signed out
        form.Show();
        Pump(500);
        Check("the window loaded: still Turkish", LanguageManager.CurrentLanguage == "TR");
        typeof(MainForm).GetMethod("LoadScreen", Any).Invoke(form, new object[] { Nav(form, ScreenRoles.HmiSettings) });
        Pump(300);
        var tabs = HmiTabs(form);
        var users = tabs == null ? null : tabs.ActiveView as UsersPreferencesControl;
        Check("HMI Settings > Users and Preferences in Turkish: Kullanıcı, Herkes (bu panel)", users != null && Header(users) == "Kullanıcı" &&
              users.Users.Rows.Cast<DataGridViewRow>().Any(r => (r.Cells[0].Value as string) == "Herkes (bu panel)"),
              users == null ? "no page" : Header(users));
        var dashHandle = dash.IsHandleCreated;

        Console.WriteLine("--- the flag: English, with Users and Preferences on show ---");
        // the one looked at: Everyone's
        foreach (DataGridViewRow r in users.Users.Rows)
            if ((r.Tag as string) == UsersPreferencesControl.Everyone) { users.Users.ClearSelection(); r.Selected = true; }
        Pump(100);
        Check("  Everyone's selected", users.Selected == UsersPreferencesControl.Everyone, users.Selected);
        UserLanguage.ChooseForPanel("EN");
        Pump(300);
        var again = tabs.ActiveView as UsersPreferencesControl;
        Check("the page on show built again, in English: User, Everyone (this panel)", again != null && !ReferenceEquals(again, users) && users.IsDisposed &&
              Header(again) == "User" && again.Users.Rows.Cast<DataGridViewRow>().Any(r => (r.Cells[0].Value as string) == "Everyone (this panel)"),
              again == null ? "none" : Header(again));
        Check("  on the one that was looked at (Everyone's)", again != null && again.Selected == UsersPreferencesControl.Everyone, again == null ? "" : again.Selected);
        Check("the Dashboard, never on the screen yet: in English too", !dashHandle && Side(dash).StartsWith("Auto-Report History"), Side(dash));

        Console.WriteLine("--- a tab not on show follows when it is opened ---");
        UserLanguage.ChooseForPanel("TR");
        Pump(300);
        tabs.ActivateByIndex(1);
        Pump(300);
        var logs = tabs.ActiveView as LogSettingsControl;
        var enabled = logs == null ? null : (CheckBox)Get(logs, "_chkEnabled");
        Check("Logs and Reports, first opened now: in Turkish",
              logs != null && enabled != null && enabled.Text == LanguageManager.GetStringIn("TR", "Log_Enabled"), enabled == null ? "none" : enabled.Text);
        UserLanguage.ChooseForPanel("EN");
        Pump(300);
        tabs.ActivateByIndex(0);
        Pump(200);
        UserLanguage.ChooseForPanel("TR");
        Pump(300);
        tabs.ActivateByIndex(1);
        Pump(300);
        var logs2 = tabs.ActiveView as LogSettingsControl;
        var enabled2 = logs2 == null ? null : (CheckBox)Get(logs2, "_chkEnabled");
        Check("  English while it was not on show, then Turkish: opened, it is in Turkish", logs2 != null && enabled2 != null &&
              enabled2.Text == LanguageManager.GetStringIn("TR", "Log_Enabled"), enabled2 == null ? "none" : enabled2.Text);
        tabs.ActivateByIndex(0);
        Pump(300);
        var users3 = tabs.ActiveView as UsersPreferencesControl;
        Check("Users and Preferences, English when it was left: Turkish on the way back (built again)", users3 != null && Header(users3) == "Kullanıcı", users3 == null ? "" : Header(users3));

        Console.WriteLine("--- the HMI's own controls follow by themselves: a machine page is not built again ---");
        var manual = Nav(form, ScreenRoles.Manual);
        typeof(MainForm).GetMethod("LoadScreen", Any).Invoke(form, new object[] { manual });
        Pump(300);
        var mtabs = Find<SettingsScreen>(manual);
        var before = mtabs == null ? null : mtabs.ActiveView;
        UserLanguage.ChooseForPanel("EN");
        Pump(300);
        Check("  Manual Movement's page kept as it was (its controls translate themselves)", before != null && ReferenceEquals(mtabs.ActiveView, before) && !before.IsDisposed);
        UserLanguage.ChooseForPanel("TR");
        Pump(200);

        form.Close();
        Console.WriteLine(fail == 0 ? "\nLANGUAGE AT START ALL PASS" : "\nLANGUAGE AT START " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
