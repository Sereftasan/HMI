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
using Kaban.UI.Screens;

// 14 October: "the servo manual movement speed and movement interval set - I copied it to all the manual movement
// pages which have AxisUnitHMI; copy it to the top of Machine Parameters / Axis as well, and to the Axis Monitor.
// What is the best way to show this control set on all of these pages without copy / pasting everywhere one by one?"
// 16 October, the bar held by the pages could not be loaded by Visual Studio from the machine's program, and a
// library of its own was one project too many: "put this control set somewhere shared by all Manual Movement pages
// (this set and the sub page tabs just under it) and copy it to Machine Parameters / Axis manually" - and on the
// Axis page "AxisJogBar at the top of the page; the item selection just before the column header line".
// One bar (FA2010: Screens\Shared\AxisJogBar) above the Manual Movement tabs and on the Axis Monitor, held by no
// page; its controls copied onto the Axis page.
class AJ {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static IEnumerable<Control> All(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; } }
    static string Bits(ButtonHMI b) { return string.Join(",", b.BitActions.Select(a => a.Action + " " + a.Address)); }

    static void Shot(Control c, string name) {
        var dirs = Environment.GetEnvironmentVariable("SHOT_DIR");
        if (dirs == null) return;
        Pump(300);
        var o = c.PointToScreen(Point.Empty);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("import", "-window root -crop " + c.Width + "x" + c.Height + "+" + o.X + "+" + o.Y + " " +
            Path.Combine(dirs, name + ".png")) { UseShellExecute = false }).WaitForExit(10000);
    }
    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "aj_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg"); ViewPreferences.Reload();
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 }) PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));

        Console.WriteLine("--- the bar, designed once ---");
        var bar = new Kaban.Machines.FA2010.Screens.Shared.AxisJogBar();
        var b = bar.Controls.OfType<ButtonHMI>().ToDictionary(x => x.Name);
        var sp = bar.Controls.OfType<SpeedOverrideHMI>().Single();
        Check("the speed of the axes' manual movement (D409), JOG, 10 / 1 / 0.1 / 0.01 mm, Safety Bypass", sp.Address == "D409" &&
              new[] { "BtnJog", "Btn10mm", "Btn1mm", "Btn01mm", "Btn001mm", "BtnSafetyBypass" }.All(b.ContainsKey), string.Join(",", b.Keys));
        Check("  the steps SET M170 - M173, JOG resets them (lit by M174), Safety Bypass alternates M175 - as the user set them",
              Bits(b["Btn10mm"]) == "SET M170" && Bits(b["Btn001mm"]) == "SET M173" && Bits(b["BtnJog"]) == "RESET M170,RESET M171,RESET M172,RESET M173" &&
              b["BtnJog"].Lamp1Address == "M174" && Bits(b["BtnSafetyBypass"]) == "Alternate M175", Bits(b["BtnJog"]));
        Check("  usable in manual mode", b.Values.All(x => x.EnableConditions == SystemCondition.ManualMode));
        Check("FA2010 declares it (ScreenRoles.AxisBar), not in the menu", MachineRegistry.AxisBars().Count() == 1 &&
              !MachineRegistry.NavigationGroups().Any(g => g.Pages.Any(p => p.Value.Role == ScreenRoles.AxisBar)));

        Console.WriteLine("--- no page holds it: the manual pages start at the top ---");
        foreach (var page in new UserControl[] { new Kaban.Machines.FA2010.Screens.Manual.ManualRobot(), new Kaban.Machines.FA2010.Screens.Manual.ManualMilling(),
                                                 new Kaban.Machines.FA2010.Screens.Manual.ManualCutting(), new Kaban.Machines.FA2010.Screens.Manual.ManualExit() })
        {
            var bars = All(page).OfType<MachineBarHMI>().Count();
            var loose = All(page).Count(c => c is SpeedOverrideHMI || (c is ButtonHMI && ((ButtonHMI)c).BitActions.Any(a => a.Address.ToUpper() == "M175" || a.Address.ToUpper() == "M174")));
            int first = page.Controls.Cast<Control>().Min(c => c.Top);
            Check(page.GetType().Name + ": no bar, nothing of it copied, its first row where the bar was", bars == 0 && loose == 0 && first <= 20,
                  bars + " bars, " + loose + " loose, first at " + first);
            page.Dispose();
        }

        Console.WriteLine("--- Manual Movement: the bar above its tabs, over every one of them ---");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show(); Pump(300);
        var nav = ((System.Collections.IDictionary)typeof(MainForm).GetField("_menuButtons", Any).GetValue(form)).Keys.Cast<ButtonHMI>()
                  .First(x => (x.FallbackText ?? "").Contains("Manual"));
        nav.PerformClick(); Pump(700);
        var manual = All(form).OfType<Kaban.UI.Settings.SettingsScreen>().FirstOrDefault(x => x.TopBar is Kaban.Machines.FA2010.Screens.Shared.AxisJogBar && x.Visible);
        Func<Control, Rectangle> scr = c => c.RectangleToScreen(c.ClientRectangle);
        Check("the Manual Movement page has FA2010's bar", manual != null);
        if (manual != null) {
            var mbar = manual.TopBar;
            var strip = manual.Controls.OfType<FlowLayoutPanel>().First();
            var host = manual.Controls.Cast<Control>().First(c => c.Dock == DockStyle.Fill);
            Check("  at the top, the tabs just under it, the page under them", scr(mbar).Top >= scr(manual).Top && scr(mbar).Bottom <= scr(strip).Top + 1 &&
                  scr(strip).Bottom <= scr(host).Top + 1 && mbar.Visible, scr(mbar) + " / " + scr(strip) + " / " + scr(host));
            Check("  lined up with the tabs, all of it in sight", mbar.Left > 0 && mbar.Controls.Cast<Control>().All(c => c.Right <= mbar.Width) &&
                  scr(mbar).Right <= scr(manual).Right, mbar.Bounds.ToString());
            var sim = (Kaban.Drivers.Simulation.SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(1));
            sim.Write("D409", 60);
            var spd = mbar.Controls.OfType<SpeedOverrideHMI>().Single();
            var until = DateTime.Now.AddSeconds(4);
            while (DateTime.Now < until && spd.Current != 60) Pump(100);
            Check("  read by the scanner like the page's own controls (D409 = 60)", spd.Current == 60, spd.Current.ToString());
            var tabs = strip.Controls.OfType<Button>().ToList();
            var moulds = tabs.FirstOrDefault(t => t.Text.Contains("Clamping Moulds"));
            if (moulds != null) { moulds.PerformClick(); Pump(400); }
            Check("  another tab (Clamping Moulds): the same bar, in the same place", moulds != null && manual.TopBar == mbar && mbar.Visible &&
                  scr(mbar).Bottom <= scr(strip).Top + 1, tabs.Count + " tabs");
            Shot(form, "manual_movement");
        }

        Console.WriteLine("--- Machine Parameters > Axis: its own copy at the top, the lists, the column names, the rows ---");
        var axes = new Kaban.Machines.FA2010.Screens.Parameters.AxisSettings { Dock = DockStyle.Fill };
        var copy = axes.Controls.OfType<ButtonHMI>().Where(x => x.Name.StartsWith("Btn")).ToDictionary(x => x.Name);
        var cs = axes.Controls.OfType<SpeedOverrideHMI>().SingleOrDefault();
        Check("the page holds a copy of the bar's controls, set as the bar's (D409, M170 - M175)", cs != null && cs.Address == "D409" &&
              new[] { "BtnJog", "Btn10mm", "Btn1mm", "Btn01mm", "Btn001mm", "BtnSafetyBypass" }.All(n => copy.ContainsKey(n) && Bits(copy[n]) == Bits(b[n])),
              string.Join(",", copy.Keys));
        var win = new Form { ClientSize = new Size(1300, 700), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        win.Controls.Add(axes); win.Show(); Pump(1200);
        var frame = AxisPageFrame.Of(axes);
        var rows = frame.Body.Controls.OfType<AxisSettingHMI>().OrderBy(r => r.Top).ToList();
        var heads = All(frame.Head).OfType<Label>().Where(l => (l.Name ?? "").StartsWith("lblHead")).ToList();
        var items = All(frame.Head).OfType<AxisPickBar>().FirstOrDefault(x => x.Name == "axisItemsBar");
        var top = new Control[] { cs }.Concat(copy.Values).ToList();
        Check("the speed and steps in the page's heading, at its top - staying while the rows scroll sideways", frame != null && top.All(c => c.Parent == frame.Head),
              string.Join(",", top.Select(c => c.Parent == null ? "-" : c.Parent.Name).Distinct()));
        Check("  then the PLC axis items, just before the column names (16 October)", items != null && top.All(c => scr(c).Bottom <= scr(items).Top) &&
              heads.Count > 0 && heads.All(h => scr(h).Top >= scr(items).Bottom),
              (items == null ? "no lists" : "lists " + scr(items)) + " speed " + scr(cs) + " names " + (heads.Count > 0 ? scr(heads[0]).ToString() : "-"));
        Check("  the column names above the first row (Robot X), the rows one under the other",
              heads.All(h => scr(h).Bottom <= scr(rows[0]).Top) && rows[0].AxisNameKey == "Axis_RobotX" && rows.Zip(rows.Skip(1), (a, c) => c.Top - a.Bottom).All(g => g >= 0),
              (heads.Count > 0 ? scr(heads[0]).ToString() : "-") + " first " + scr(rows[0]));
        var far = heads.OrderBy(h => h.Left).LastOrDefault();
        int speedX = scr(cs).Left, farX = far == null ? 0 : scr(far).Left;
        frame.Body.AutoScrollPosition = new Point(300, 0); Pump(300);
        int moved = frame.Body.AutoScrollPosition.X;
        Check("  scrolled sideways: the speed stays, the column names follow the rows", moved < 0 && scr(cs).Left == speedX &&
              far != null && scr(far).Left == farX + moved, "scrolled " + moved + ", speed " + speedX + "->" + scr(cs).Left + ", " + (far == null ? "-" : far.Name + " " + farX + "->" + scr(far).Left));
        Shot(win, "axis_page");
        win.Close();

        Console.WriteLine("--- the Axis Monitor: the bar on top, its parts in rows in the narrow side page ---");
        var view = form.ShowSidePage("AxisMonitor") as AxisMonitorPage;
        Pump(600);
        var mbars = (IList<MachineBarHMI>)typeof(AxisMonitorPage).GetProperty("MachineBars", Any).GetValue(view, null);
        Check("FA2010's bar shown", mbars.Count == 1 && mbars[0] is Kaban.Machines.FA2010.Screens.Shared.AxisJogBar, mbars.Count.ToString());
        if (mbars.Count == 1) {
            var mb = mbars[0];
            Check("  as wide as the side page, its parts inside it, in rows", mb.Width <= view.Width && mb.Controls.Cast<Control>().All(c => c.Right <= mb.Width) &&
                  mb.Controls.Cast<Control>().Select(c => c.Top).Distinct().Count() >= 2, mb.Width + " in " + view.Width);
            Check("  above the rows", scr(mb).Bottom <= scr(view.Controls.OfType<Control>().First(c => c.Dock == DockStyle.Fill && c.Visible)).Top + 1);
            Shot(form, "axis_monitor");
            int wide = mb.FitTo(2000);
            Check("  given room: as designed again, one line", mb.Controls.Cast<Control>().Select(c => c.Top).Distinct().Count() == 1 && wide == 42, wide.ToString());
        }
        form.Close();
        Console.WriteLine(fail == 0 ? "\nAXIS JOG BAR ALL PASS" : "\nAXIS JOG BAR " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
