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
// What is the best way to show this control set on all of these pages without copy / pasting everywhere one by one
// (e.g. create one for each machine and load anywhere needed)?" - and the Axis page's caption line mixed with its
// first row. One bar per machine (FA2010: Screens\Shared\AxisJogBar), held by the pages, declared for the monitor.
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

        Console.WriteLine("--- the manual pages hold it: one bar, no copies ---");
        foreach (var page in new UserControl[] { new Kaban.Machines.FA2010.Screens.Manual.ManualRobot(), new Kaban.Machines.FA2010.Screens.Manual.ManualMilling(),
                                                 new Kaban.Machines.FA2010.Screens.Manual.ManualCutting(), new Kaban.Machines.FA2010.Screens.Manual.ManualExit() })
        {
            var bars = All(page).OfType<Kaban.Machines.FA2010.Screens.Shared.AxisJogBar>().ToList();
            var loose = All(page).Where(c => !(c.Parent is Kaban.Machines.FA2010.Screens.Shared.AxisJogBar))
                                 .Count(c => c is SpeedOverrideHMI || (c is ButtonHMI && ((ButtonHMI)c).BitActions.Any(a => a.Address.ToUpper() == "M175" || a.Address.ToUpper() == "M174")));
            Check(page.GetType().Name + ": the bar at its top, nothing of it copied loose", bars.Count == 1 && loose == 0 && bars[0].Top < 30, bars.Count + " bars, " + loose + " loose");
            page.Dispose();
        }

        Console.WriteLine("--- Machine Parameters > Axis: the bar above, the caption line, then the rows ---");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show(); Pump(300);
        var axes = new Kaban.Machines.FA2010.Screens.Parameters.AxisSettings { Dock = DockStyle.Fill };
        var host = new Form { ClientSize = new Size(1300, 700), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        host.Controls.Add(axes); host.Show(); Pump(1200);
        var frame = AxisPageFrame.Of(axes);
        var jb = All(axes).OfType<Kaban.Machines.FA2010.Screens.Shared.AxisJogBar>().FirstOrDefault();
        Check("the page a table, the bar in its heading - staying while the rows scroll", frame != null && jb != null && jb.Parent == frame.Head,
              jb == null ? "no bar" : jb.Parent.Name);
        var rows = frame.Body.Controls.OfType<AxisSettingHMI>().OrderBy(r => r.Top).ToList();
        var heads = All(frame.Head).OfType<Label>().Where(l => (l.Name ?? "").StartsWith("lblHead")).ToList();
        Func<Control, Rectangle> scr = c => c.RectangleToScreen(c.ClientRectangle);
        Check("the column names below the bar, above the first row (Robot X) - not mixed with it",
              heads.Count > 0 && heads.All(h => scr(h).Top >= scr(jb).Bottom && scr(h).Bottom <= scr(rows[0]).Top) && rows[0].AxisNameKey == "Axis_RobotX",
              "bar " + scr(jb) + " head " + (heads.Count > 0 ? scr(heads[0]).ToString() : "-") + " first " + scr(rows[0]) + " " + rows[0].AxisNameKey);
        Check("  the rows one under the other, none over another", rows.Zip(rows.Skip(1), (a, c) => c.Top - a.Bottom).All(g => g >= 0),
              string.Join(" ", rows.Take(4).Select(r => r.Top + "-" + r.Bottom)));
        Shot(host, "axis_page");
        host.Close();

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
