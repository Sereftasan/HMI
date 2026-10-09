using System;
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
using Kaban.UI.Settings;

// 9 October: the Clamping Moulds page of pneumatic positionings in the real program - "only the Front
// Vertical ones are used on the machine as fixed but the rest of them (Back Vertical and Back Horizontal
// ones) are only mounted to the machine when Clamping Mould (L27) option is True ... move the next one up
// when the previous control on the page is not visible so the page doesn't have empty lines all over".
// The simulated PLC's L27, the scanner reading it, the rows following it.
class MO {
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
    static bool WaitFor(Func<bool> cond, int ms) {
        var until = DateTime.Now.AddMilliseconds(ms);
        while (DateTime.Now < until) { if (cond()) return true; Application.DoEvents(); System.Threading.Thread.Sleep(20); }
        return cond();
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "mo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        AlarmGeneration.SettingsPath = Path.Combine(dir, "AlarmGeneration.cfg");
        NotesStore.Folder = Path.Combine(dir, "Notes");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        ViewPreferences.Reload();
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        var sim = new Dictionary<int, Kaban.Drivers.Simulation.SimulationDriver>();
        foreach (int st in new[] { 1, 2, 3, 4 }) { sim[st] = new Kaban.Drivers.Simulation.SimulationDriver(st); PlcManager.Instance.RegisterDriver(st, sim[st]); }
        sim[1].Write("L27", 0);
        SecurityManager.Login("op", SecurityLevel.Operator);
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s0, e0) => SecurityManager.Login("op", SecurityLevel.Operator);
        form.Show();
        Pump(400);

        Console.WriteLine("--- Manual Movement > Clamping Moulds, the option off (L27 = 0) ---");
        form.LoadScreen(Nav(form, ScreenRoles.Manual));
        Pump(100);
        var tabs = All(form).OfType<SettingsScreen>().First(sc => sc.AllPages.Any(p => p.TitleKey == "Tab_ManualClamping_Moulds"));
        typeof(SettingsScreen).GetMethod("ShowPage", Any).Invoke(tabs, new object[] { tabs.AllPages.First(p => p.TitleKey == "Tab_ManualClamping_Moulds") });
        Pump(200);
        var page = All(form).OfType<Kaban.Machines.FA2010.Screens.Manual.ManualClamping_Moulds>().FirstOrDefault();
        Check("the page is open", page != null && page.Visible);
        if (page == null) { Environment.Exit(1); return; }
        var rows = PneumaticPositioningHMI.RowsOf(page);
        // the places the designer gave the rows (the scanner may have moved them already)
        var places = rows.Select(r => r.Location).OrderBy(p => p.Y).ToList();
        Func<List<PneumaticPositioningHMI>> shown = () => rows.Where(r => r.Visible).OrderBy(r => r.Top).ToList();
        bool hid = WaitFor(() => shown().Count == 6, 4000);
        var s = shown();
        Check("the scanner read L27 = 0: the 6 front vertical moulds shown", hid && s.All(r => !r.TitleTextKey.Contains("Back")),
              s.Count + ": " + string.Join(", ", s.Select(r => r.TitleTextKey)));
        Check("  moved up together - the first six places, no empty lines", s.Select(r => r.Location).SequenceEqual(places.Take(6)),
              string.Join(" ", s.Select(r => r.Location)) + " / " + string.Join(" ", places.Take(6)));
        Check("  the page scrolls no further than they reach", page.DisplayRectangle.Height <= s.Last().Bottom + 40 || !page.VerticalScroll.Visible,
              page.DisplayRectangle.Height + " vs " + s.Last().Bottom);

        Console.WriteLine("--- the option on (L27 = 1) ---");
        sim[1].Write("L27", 1);
        bool all = WaitFor(() => shown().Count == 16, 4000);
        Check("all 16 shown, each in its place", all && rows.Select(r => r.Location).SequenceEqual(places));

        Console.WriteLine("--- off again ---");
        sim[1].Write("L27", 0);
        Check("the back ones out again, the front ones up", WaitFor(() => shown().Count == 6, 4000) &&
              shown().Select(r => r.Location).SequenceEqual(places.Take(6)));

        Console.WriteLine("--- the moulds' error bits are known to the program ---");
        AlarmHelpLinks.EnsureDiscovered();
        var link = AlarmHelpLinks.Find(1, "M915");
        Check("M915: the Left Profile Recognition Front Vertical's back movement, a 5/3 closed centre with an analogue sensor",
              link != null && link.Unit == "Left Profile Recognition Front Vertical" && link.Valve == (int)CylinderValve.ThreeWayClosedCenter &&
              link.Feedback == (int)CylinderFeedback.AnalogueMagnetic, link == null ? "none" : link.Unit);
        var link2 = AlarmHelpLinks.Find(1, "M937");
        Check("M937: Cutting Left Back Horizontal (an encoder)", link2 != null && link2.Unit == "Cutting Left Back Horizontal" &&
              link2.Feedback == (int)CylinderFeedback.EncoderPlusReference, link2 == null ? "none" : link2.Unit);

        Console.WriteLine("--- an operator can move them ---");
        var front = shown()[0];
        Check("the manual movement buttons are there for an operator", front.Unit.ButtonA.Visible && front.Unit.ButtonB.Visible && front.Unit.ButtonA.Enabled);

        form.Close();
        Pump(200);
        Console.WriteLine(fail == 0 ? "\nMOULDS OPTION ALL PASS" : "\nMOULDS OPTION " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
