using System;
using System.Collections;
using System.Collections.Generic;
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
using Kaban.UI.Settings;

// 23 October: "Previous page button doesn't make the control's original page open when translation page opened from
// Notes and Feedback page, the page history should be recorded from any link opens any page. There should also be find
// function (with button and Ctrl+F) at Notes and Feedback page for all the element (device addresses or translation
// keys) so the user can check where they are used to understand what difference their suggestions will make"
class LH {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        d = d ?? "";
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
        var groups = (IDictionary)Get(f, "_screenGroups");
        return groups.Keys.Cast<UserControl>().First(k => ((MachineRegistry.RoleGroup)groups[k]).Role == role);
    }
    static UserControl Current(MainForm f) { return (UserControl)Get(f, "_currentScreen"); }
    static string Place(MainForm f) {
        var cur = Current(f);
        var ss = cur as SettingsScreen;
        string s = cur == null ? "none" : cur.Name + "/" + cur.GetType().Name;
        for (int depth = 0; ss != null && depth < 3; depth++) {
            var active = (ISettingsPage)Get(ss, "_active");
            if (active == null) break;
            s += " > " + active.TitleKey;
            ss = typeof(SettingsScreen).GetMethod("ViewOf", Any).Invoke(ss, new object[] { active }) as SettingsScreen;
        }
        return s;
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "lh_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        NotesStore.Folder = Path.Combine(dir, "Notes");
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
        foreach (int st in new[] { 1, 2, 3, 4 }) { PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st)); ConnectionMonitor.ReportRead(st, true); }

        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show();
        Pump(400);

        Console.WriteLine("--- the notes page's link to the translation table ---");
        var hmi = (SettingsScreen)Nav(form, ScreenRoles.HmiSettings);
        form.LoadScreen(hmi);
        Pump(100);
        var other = hmi.AllPages.First(p => p.TitleKey != "Tab_Translations" && SecurityManager.HasAccess(p.RequiredLevel));
        hmi.ActivateByKey(other.TitleKey);
        Pump(100);
        var manual = Nav(form, ScreenRoles.Manual);
        form.LoadScreen(manual);
        Pump(200);
        var manualTabs = All(manual).OfType<SettingsScreen>().FirstOrDefault() ?? manual as SettingsScreen;
        if (manualTabs != null && manualTabs.AllPages.Count > 1) { manualTabs.ActivateByKey(manualTabs.AllPages[1].TitleKey); Pump(150); }
        string from = Place(form);
        string why;
        var table = form.OpenTranslation("Btn_Home", out why);
        Pump(150);
        Check("the translation table opened on the key", table != null && Place(form).Contains("Tab_Translations"), Place(form) + " " + why);
        NavigationHistory.Back();
        Pump(150);
        Check("Back: the page the link was followed from - its tab as it was (it went to HMI Settings on the tab it was left on)", Place(form) == from,
              Place(form) + " (wanted " + from + ")");
        NavigationHistory.Forward();
        Pump(150);
        Check("  Forward: the translation table again", Place(form).Contains("Tab_Translations"), Place(form));

        Console.WriteLine("--- a device's use opened (the PLC Monitor's Find, the notes page's) ---");
        var alarms = Nav(form, ScreenRoles.Alarms);
        form.LoadScreen(alarms);
        Pump(150);
        string onAlarms = Place(form);
        var use = DeviceCollector.UsesOf(1).FirstOrDefault(u => !u.IsAlarm && u.Screen != null && u.PagePath.Count > 0 && u.PagePath[0] > 0);
        Control target = use == null ? null : form.ShowDeviceUse(use, out why);
        Pump(150);
        Check("a use on another page's other tab: opened", target != null, use == null ? "no use" : use.PageTitle + " " + why);
        NavigationHistory.Back();
        Pump(150);
        Check("  Back: the page the link was followed from (one step)", Place(form) == onAlarms, Place(form) + " (wanted " + onAlarms + ")");

        Console.WriteLine("--- the notes page's Find ---");
        form.LoadScreen(manual);
        Pump(200);
        var notes = (NotesPage)form.ShowSidePage("Notes");
        Pump(200);
        var findButton = All(notes).OfType<Button>().FirstOrDefault(b => b.Name == "notesFind");
        Check("a Find button on the notes page (its tooltip names Ctrl+F)", findButton != null);
        var page = Current(form);
        var unit = All(page).OfType<CylinderUnitHMI>().FirstOrDefault(u => DeviceCollector.UnderControl(u, 1).Count > 1 && u.Visible);
        notes.Pick(unit);
        Pump(100);
        var elements = (List<NotesFindForm.Element>)typeof(NotesPage).GetMethod("Elements", Any).Invoke(notes, null);
        var unitDevices = DeviceCollector.UnderControl(unit, 1).Select(d => d.Address).ToList();
        Check("the picked unit's elements: its devices and its texts' keys", elements.Count(e => !e.IsText) >= unitDevices.Count && elements.Any(e => e.IsText) &&
              unitDevices.All(a => elements.Any(e => !e.IsText && e.Name == a)),
              string.Join(", ", elements.Select(e => e.Shown).Take(12)));

        NotesFindForm seen = null;
        List<NotesFindForm.Found> listed = null;
        string onManual = Place(form);
        NotesPage.FindShowing = f => {
            seen = f;
            listed = (List<NotesFindForm.Found>)Get(f, "Listed");
            // a use of one of its devices somewhere else: opened
            int row = listed.FindIndex(x => !x.Of.IsText && x.Way != null && !ReferenceEquals(x.Way.Screen, null) && x.Place != listed[0].Place);
            if (row < 0) row = listed.FindIndex(x => x.Way != null);
            typeof(NotesFindForm).GetMethod("Open", Any).Invoke(f, new object[] { row });
            return f.DialogResult;
        };
        var msg = new Message { Msg = 0x0100, WParam = (IntPtr)(int)Keys.F, HWnd = notes.Handle };
        var args = new object[] { msg, Keys.Control | Keys.F };
        bool handled = (bool)typeof(NotesPage).GetMethod("ProcessCmdKey", Any).Invoke(notes, args);
        Pump(200);
        Check("Ctrl+F: Find - every use of each device and text of the unit", handled && seen != null && listed != null &&
              listed.Any(x => !x.Of.IsText && x.Kind == "Page") && listed.Any(x => x.Of.IsText),
              listed == null ? "not opened" : listed.Count + " uses: " + string.Join(" | ", listed.Take(5).Select(x => x.Of.Shown + " " + x.Kind + " " + x.Place + " " + x.What)));
        Check("  a use chosen: its page opened, picking off", seen != null && seen.Chosen != null && !notes.Picking, seen == null || seen.Chosen == null ? "none" : seen.Chosen.PageTitle);
        NavigationHistory.Back();
        Pump(150);
        Check("  Back: the page the control was picked on", Place(form) == onManual, Place(form) + " (wanted " + onManual + ")");

        // 26 October: "'Every note' page ... We should add to view, edit and find the related control functions on them"
        Console.WriteLine("--- a note's control found again ---");
        typeof(NotesPage).GetMethod("AddComment", Any).Invoke(notes, new object[] { "find me" });
        Pump(100);
        var kept = NotesStore.All.Last(n => n.Text == "find me");
        Check("a note keeps where its control's page is (the page, its tabs)", (kept.Location ?? "").Contains("Page\t") && (kept.Location ?? "").Contains("Tabs\t"),
              (kept.Location ?? "").Replace("\n", " | "));
        form.LoadScreen(alarms);
        Pump(150);
        string onAlarms2 = Place(form);
        typeof(NotesPage).GetMethod("FindNoted", Any).Invoke(notes, new object[] { kept });
        Pump(200);
        Check("Find from another page: the control's page opened, on its tab, the page says so", Place(form) == onManual &&
              ((string)Get(notes, "StatusText")).Contains("flashing"), Place(form) + " (wanted " + onManual + ") " + (string)Get(notes, "StatusText"));
        NavigationHistory.Back();
        Pump(150);
        Check("  Back: where Find was pressed (one step)", Place(form) == onAlarms2, Place(form) + " (wanted " + onAlarms2 + ")");
        kept.Location = "";
        typeof(NotesPage).GetMethod("FindNoted", Any).Invoke(notes, new object[] { kept });
        Pump(100);
        Check("  a note of before (no place kept), its page not open: said to open it", ((string)Get(notes, "StatusText")).StartsWith("Its page is not open"),
              (string)Get(notes, "StatusText"));

        Console.WriteLine("--- typed ---");
        var typed = new NotesFindForm("", new List<NotesFindForm.Element>(), 1);
        typeof(NotesFindForm).GetMethod("Search", Any).Invoke(typed, new object[] { "Btn_Home" });
        var l1 = (List<NotesFindForm.Found>)Get(typed, "Listed");
        Check("a translation key typed: its uses (the menu's Home)", l1.Count > 0 && l1.All(x => x.Of.IsText) && l1.Any(x => x.Kind == "Menu"),
              string.Join(" | ", l1.Take(4).Select(x => x.Kind + " " + x.Place + " " + x.What)));
        var anyDevice = unitDevices.First();
        typeof(NotesFindForm).GetMethod("Search", Any).Invoke(typed, new object[] { anyDevice.ToLowerInvariant() });
        var l2 = (List<NotesFindForm.Found>)Get(typed, "Listed");
        Check("  a device typed (any case): its uses", l2.Count > 0 && l2.All(x => !x.Of.IsText && x.Of.Name == anyDevice), anyDevice + ": " + l2.Count);
        typeof(NotesFindForm).GetMethod("Search", Any).Invoke(typed, new object[] { anyDevice + "@2" });
        var l3 = (List<NotesFindForm.Found>)Get(typed, "Listed");
        Check("  with @2: PLC 2's", l3.All(x => x.Of.Station == 2), l3.Count + " uses on PLC 2");
        typeof(NotesFindForm).GetMethod("Search", Any).Invoke(typed, new object[] { "no such thing" });
        Check("  neither: said", ((List<NotesFindForm.Found>)Get(typed, "Listed")).Count == 0 && ((string)Get(typed, "Status")).Contains("no translation key and no device address"),
              (string)Get(typed, "Status"));
        typed.Dispose();

        NotesPage.FindShowing = null;
        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nLINK HISTORY ALL PASS" : "\nLINK HISTORY " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
