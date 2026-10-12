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

// 8 October: "It can be useful to create a way to open any main page in the second page if it is not
// too complex or unsafe." - on the real FA2010 program.
class SC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static object Get(object o, string n) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, Any); if (f != null) return f.GetValue(o); } return null; }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "sc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        string cfg = Path.Combine(dir, "SidePanel.cfg");
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, cfg);
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
        form.Show();
        Pump(400);
        AlarmHelpLinks.EnsureDiscovered();
        int cylinders = AlarmGeneration.Cylinders.Count, axes = AlarmHelpLinks.AllAxes.Count;

        Console.WriteLine("--- which pages ---");
        var groups = MachineRegistry.NavigationGroups();
        Func<string, MachineRegistry.RoleGroup> role = r => groups.First(g => g.Role == r);
        Check("Home, Manual Movement, Machine Parameters, Alarms, Dashboard may be opened there",
              new[] { ScreenRoles.Home, ScreenRoles.Manual, ScreenRoles.MachineParameters, ScreenRoles.Alarms }.All(r => MainForm.CanOpenInSide(role(r))));
        Check("  not HMI Settings, not a page without a role (Profiles: a library edited and saved)",
              !MainForm.CanOpenInSide(role(ScreenRoles.HmiSettings)) && groups.Where(g => string.IsNullOrEmpty(g.Role)).All(g => !MainForm.CanOpenInSide(g)),
              string.Join(",", groups.Where(MainForm.CanOpenInSide).Select(g => g.Role)));

        Console.WriteLine("--- a right-click on Manual Movement ---");
        var manual = role(ScreenRoles.Manual);
        var menu = form.SideMenuFor(manual);
        var items = menu.Items.OfType<ToolStripMenuItem>().ToList();
        Check("'Open in the side panel', per machine", menu.Items[0].Text == "Open in the side panel" && items.Count == manual.Pages.Count && items.Count >= 1,
              string.Join(" | ", menu.Items.Cast<ToolStripItem>().Select(i => i.Text)));
        var main = form.Controls.Find("pnlContent", true).FirstOrDefault();
        items[0].PerformClick();
        Pump(500);
        var open = form.OpenSidePage;
        string key = MainForm.MainPageKey(manual, manual.Pages[0].Key);
        Check("opened in the side panel, named as in the menu", open != null && open.Key == key && open.Title.Contains("Manual Movement"), open == null ? "none" : open.Key + " / " + open.Title);
        var view = form.SidePageView(key);
        var host = (Panel)Get(form, "_sideHost");
        Check("  its own copy of the page, in the panel the scanner reads", view != null && view.Parent == host && view.Visible &&
              !((System.Collections.IDictionary)Get(form, "_screenGroups")).Keys.Cast<object>().Contains(view));
        Check("  a page with its units", view.Controls.Count > 0 && Descendants(view).OfType<CylinderUnitHMI>().Any(), Descendants(view).OfType<CylinderUnitHMI>().Count() + " units");
        Check("  nothing registered twice (alarms, axes)", AlarmGeneration.Cylinders.Count == cylinders && AlarmHelpLinks.AllAxes.Count == axes);
        var buttons = (Dictionary<string, Button>)Get(form, "_sideButtons");
        Check("its button on the side bar, and the + button", buttons.ContainsKey(key) && buttons.ContainsKey("+"), string.Join(",", buttons.Keys));
        // 8 October: "the added pages' buttons should be located under the 'Open a main page in the side panel'
        // button so the user can see them separated from the naturally existing ones"
        var plus = buttons["+"];
        var nativeKeys = buttons.Keys.Where(k => k != "+" && k != key && !k.StartsWith("Main:")).ToList();
        Check("  under the + button; the program's own pages above it", buttons[key].Top > plus.Top &&
              nativeKeys.Count > 0 && nativeKeys.All(k => buttons[k].Top < plus.Top),
              "+ at " + plus.Top + ", " + key + " at " + buttons[key].Top + "; " + string.Join(", ", nativeKeys.Select(k => k + "@" + buttons[k].Top)));
        Check("  kept for the next run", File.ReadAllText(cfg).Contains("MainPages=" + key), File.ReadAllText(cfg).Replace("\n", " "));
        Check("the + button lists the pages, this one ticked", form.SidePagesMenu().Items.OfType<ToolStripMenuItem>().Any(i => i.Name == "side:" + key && i.Checked) &&
              form.SidePagesMenu().Items.OfType<ToolStripMenuItem>().Any(i => i.Name.StartsWith("side:Main:Home")));

        Console.WriteLine("--- the next run ---");
        SidePages.Unregister(key);
        var form2 = new MainForm();
        // what Load does: the panel's state read, the kept main pages registered
        typeof(MainForm).GetMethod("LoadSidePanelState", Any).Invoke(form2, null);
        typeof(MainForm).GetMethod("RegisterKeptMainPages", Any).Invoke(form2, new object[] { Get(form2, "_keptMainPages") });
        Check("put on the side bar again from the kept file", SidePages.Find(key) != null && SidePages.Find(key).Title == "Processing Center - Manual Movement");
        form2.Dispose();

        Console.WriteLine("--- taken off ---");
        form.RemoveMainPageFromSide(key);
        Pump(200);
        Check("closed, its copy let go, its button gone", form.OpenSidePage == null && view.IsDisposed && SidePages.Find(key) == null && !buttons.ContainsKey(key));
        Check("  and not kept", !File.ReadAllText(cfg).Contains(key));
        Check("the texts in every language", new[] { "Side_OpenHere", "Side_Remove", "Tip_SideAddPage" }
              .All(k => new[] { "EN", "TR", "RU", "DE", "IT", "ES" }.All(l => !string.IsNullOrEmpty(LanguageManager.GetStringIn(l, k)))));
        form.Close();
        Console.WriteLine(fail == 0 ? "\nSIDE COPIES ALL PASS" : "\nSIDE COPIES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
    static IEnumerable<Control> Descendants(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Descendants(k)) yield return d; } }
}
