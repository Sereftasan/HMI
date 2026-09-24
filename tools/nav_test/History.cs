using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;
using Kaban.UI.Screens;
using Kaban.UI.Settings;

// BACK / FORWARD on the REAL MainForm (FA2010 + H500 + H800 mounted).
//
// What was reported: "< / >" sometimes did not load the page, and the machine
// strip marked the MAIN machine while a sub-machine's page was on screen.
// Causes: a history step held only the outer page (showing the page already on
// screen is a no-op), a tab step found its window through FindForm() (null once
// navigated away), and the strip was always reset to the first machine.
class H {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    class Mod : IMachineModule, IMachineDisplayName {
        string _id; public Mod(string id) { _id = id; }
        public string ModuleId { get { return _id; } }
        public string DisplayNameKey { get { return "Machine_" + _id; } }
        public string DisplayName { get { return LanguageManager.GetString(DisplayNameKey, ModuleId); } }
        public void RegisterPlc(IMachineMountContext c) { PlcManager.Instance.RegisterStation(c.Station(1), _id, SecurityLevel.Operator, c.ModuleId, 1); }
        public void RegisterConditions(IMachineMountContext c) { }
        public void RegisterData(IMachineMountContext c) { }
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext c) {
            return StandardScreens.Build(c, new StandardScreens.Options {
                Home = () => new UserControl { Name = "Home_" + _id },
                ManualPages = new ISettingsPage[0],
                MachineSettings = new ISettingsPage[0] });
        }
    }

    static IEnumerable<Control> All(Control root) {
        foreach (Control c in root.Controls) { yield return c; foreach (var d in All(c)) yield return d; }
    }

    static UserControl Current(MainForm f) {
        return (UserControl)typeof(MainForm).GetField("_currentScreen", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(f);
    }

    static string Shown(MainForm f) {
        var cur = Current(f);
        var ss = cur as SettingsScreen;
        if (ss == null) return cur == null ? "null" : cur.GetType().Name;
        var view = All(ss).OfType<UserControl>().FirstOrDefault(u => u.Name.StartsWith("Home_") && u.Parent != null);
        return view != null ? view.Name : "tabs:" + ss.ActiveIndex;
    }

    [STAThread]
    static void Main() {
        Environment.CurrentDirectory = "/tmp/real";
        Application.EnableVisualStyles();

        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod("FA2010"));
        MachineRegistry.MountSub(new Mod("H500"), stationMap: new[] { 3 });
        MachineRegistry.MountSub(new Mod("H800"), stationMap: new[] { 4 });
        SecurityManager.Login("op", SecurityLevel.Operator);

        var form = new MainForm();
        form.BuildNavigation();
        form.Show();
        form.CreateControl();
        NavigationHistory.Clear();

        var strip = All(form).OfType<MachineSelectorStrip>().First();
        var homeBtn = All(form).OfType<Kaban.UI.Controls.ButtonHMI>().First(b => b.TranslationKey == "Btn_Home");
        var alarmsBtn = All(form).OfType<Kaban.UI.Controls.ButtonHMI>().First(b => b.TranslationKey == "Btn_Alarms");
        var menu = (Dictionary<Kaban.UI.Controls.ButtonHMI, UserControl>)typeof(MainForm)
            .GetField("_menuButtons", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
        var home = menu[homeBtn];
        var alarms = menu[alarmsBtn];

        Console.WriteLine("--- walk: Home/FA2010 -> Home/H500 -> Alarms ---");
        form.LoadScreen(alarms); form.LoadScreen(home);           // start clean on Home
        NavigationHistory.Clear();
        form.RecordLocation();
        Check("starts on Home / FA2010", Shown(form) == "Home_FA2010", Shown(form));

        strip.Select(1, notify: true);                            // operator picks H500
        Check("strip click shows H500", Shown(form) == "Home_H500", Shown(form));

        strip.Select(1, notify: true);                            // same machine again
        form.LoadScreen(alarms);
        Check("on Alarms", ReferenceEquals(Current(form), alarms));

        Console.WriteLine("--- Back returns to Home WITH H500, and the strip says so ---");
        NavigationHistory.Back();
        Check("Back: Home is shown", ReferenceEquals(Current(form), home));
        Check("Back: it shows H500 (the machine left on)", Shown(form) == "Home_H500", Shown(form));
        Check("Back: the strip marks H500, not the main machine", strip.SelectedIndex == 1, "index=" + strip.SelectedIndex);

        Console.WriteLine("--- Back again reaches Home / FA2010 (used to do nothing) ---");
        NavigationHistory.Back();
        Check("Back: Home / FA2010", Shown(form) == "Home_FA2010", Shown(form));
        Check("Back: the strip marks FA2010", strip.SelectedIndex == 0, "index=" + strip.SelectedIndex);
        Check("re-picking the machine on show added no step", !NavigationHistory.CanBack);

        Console.WriteLine("--- Forward replays the same path ---");
        NavigationHistory.Forward();
        Check("Forward: Home / H500", Shown(form) == "Home_H500", Shown(form));
        Check("Forward: strip on H500", strip.SelectedIndex == 1, "index=" + strip.SelectedIndex);
        NavigationHistory.Forward();
        Check("Forward: Alarms", ReferenceEquals(Current(form), alarms));
        Check("end of history", !NavigationHistory.CanForward);

        Console.WriteLine("--- a Home button click returns to the machine Home was left on, strip in step ---");
        form.LoadScreen(home);
        Check("Home shows H500", Shown(form) == "Home_H500", Shown(form));
        Check("strip marks H500", strip.SelectedIndex == 1, "index=" + strip.SelectedIndex);

        Console.WriteLine("--- page security is per PAGE, not per class ---");
        var levels = (System.Collections.IDictionary)typeof(MainForm)
            .GetField("_screenLevels", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
        var paramsBtn = All(form).OfType<Kaban.UI.Controls.ButtonHMI>().First(b => b.TranslationKey == "Btn_MachineParameters");
        var hmiBtn = All(form).OfType<Kaban.UI.Controls.ButtonHMI>().First(b => b.TranslationKey == "Btn_HmiSettings");
        Check("Machine Parameters keeps Maintenance", (SecurityLevel)levels[menu[paramsBtn]] == SecurityLevel.Maintenance,
              levels[menu[paramsBtn]].ToString());
        Check("Home keeps its own (lower) level", (SecurityLevel)levels[home] < SecurityLevel.Maintenance,
              levels[home].ToString());
        Check("HMI Settings keeps its own level", (SecurityLevel)levels[menu[hmiBtn]] == SecurityLevel.None,
              levels[menu[hmiBtn]].ToString());

        Console.WriteLine("--- a login rebuild adds no history ---");
        bool couldBack = NavigationHistory.CanBack, couldFwd = NavigationHistory.CanForward;
        int before = HistoryCount();
        SecurityManager.Login("op2", SecurityLevel.Operator);
        LanguageManager.ChangeLanguage(LanguageManager.CurrentLanguage);
        Check("no steps added by the tab rebuilds", HistoryCount() == before, before + " -> " + HistoryCount());

        Console.WriteLine(fail == 0 ? "\nHISTORY ALL PASS" : "\nHISTORY " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    static int HistoryCount() {
        var f = typeof(NavigationHistory).GetField("_entries", BindingFlags.NonPublic | BindingFlags.Static);
        return ((System.Collections.ICollection)f.GetValue(null)).Count;
    }
}
