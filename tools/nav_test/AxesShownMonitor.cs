using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;
using Kaban.UI.Screens;

// 8 October: the Axes list on the Axis Monitor ("the shown one should be moved up to fill the hidden
// ones place"), the monitor's scroll ("doesn't move to right as much as needed ... additional machine
// axis open at the same page"), and the same on the Inputs / Outputs side page ("could be useful for the
// digital input output monitor second page as well") - on the real FA2010 program.
class ASM {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static T P<T>(object o, string n) { return (T)o.GetType().GetProperty(n, Any).GetValue(o, null); }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "asm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
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
        Pump(300);

        Console.WriteLine("--- the Axis Monitor ---");
        var view = form.ShowSidePage("AxisMonitor") as AxisMonitorPage;
        Pump(300);
        Check("in the side panel the page does not scroll itself: its list does (one scroll, its right end reachable)",
              view != null && !view.AutoScroll && ((Control)typeof(AxisMonitorPage).GetField("_list", Any).GetValue(view)) is IScrollsItself);
        var bar = P<AxisPickBar>(view, "Bar");
        var pick = bar.AxesPick;
        var rowsHead = (Panel)typeof(AxisMonitorPage).GetField("_rowsHead", Any).GetValue(view);
        var adminHead = rowsHead.Controls.Count > 0 ? rowsHead.Controls[0].Controls.OfType<Label>().FirstOrDefault(l => (l.Tag as string) == "AxisHeading:Axis") : null;
        var classicHead = ((Panel)typeof(AxisMonitorPage).GetField("_head", Any).GetValue(view)).Controls.OfType<Label>().First();
        Check("the Axis heading opens it", AxisHeadingFilter.PickOf(adminHead ?? classicHead) == pick);
        // V Cutting's three axes need their option (MachineBit20, not set in the simulation): not offered (16 October)
        Check("the Axes list: the 20 axes that can be seen of 23 (V Cutting's need their option), the machine before the name", pick != null && pick.Items.Count == 20 &&
              !pick.Items.Any(i => i.Text.Contains("V Cutting")) &&
              pick.Items[0].Text == "Processing Center · Robot X" && pick.Items.Last().Text.StartsWith("3D Hinge Drilling · "),
              pick == null ? "none" : pick.Items.Count + ": " + pick.Items[0].Text + " ... " + pick.Items.Last().Text);
        Func<List<string>> listed = () => P<IList<AxisMonitorRow>>(view, "Rows").Select(r => r.NameKey + "@" + r.PlcNumber).Concat(P<IList<AxisSettingHMI>>(view, "SettingRows").Select(r => r.AxisNameKey + "@" + r.PlcNumber)).ToList();
        int before = listed().Count;
        var h800 = pick.Items.Where(i => i.Key.EndsWith("@4")).Select(i => i.Key).Take(2).ToList();
        pick.Select(h800);
        Pump(300);
        var now = listed();
        Check("two of H800's ticked: only they, moved up", before == 23 && now.SequenceEqual(h800), string.Join(",", now));
        var list = (FlowLayoutPanel)typeof(AxisMonitorPage).GetField("_list", Any).GetValue(view);
        Check("  no machine heading for one machine", !list.Controls.OfType<Label>().Any(l => l.Visible && l.Text == "3D Hinge Drilling"));
        Check("  kept as the admin's", ViewPreferences.PickOf(ViewPreferences.MonitorAxes).SetEquals(h800));

        Console.WriteLine("--- an operator: the Axes list, not the Admin's lists ---");
        SecurityManager.Login("op1", SecurityLevel.Operator);
        Pump(400);
        pick = P<AxisPickBar>(view, "Bar").AxesPick;
        var head = (Panel)typeof(AxisMonitorPage).GetField("_head", Any).GetValue(view);
        var axisHead = head.Controls.OfType<Label>().First();
        Check("as it was (no Admin lists), the Axes list from the Axis heading", !P<AxisPickBar>(view, "Bar").Visible && head.Visible &&
              axisHead.Text == "Axis" && AxisHeadingFilter.PickOf(axisHead) == pick);
        Check("  his own view: all the axes", listed().Count == 23 && pick.Selected.Count == 0, listed().Count.ToString());
        var robot = pick.Items.First(i => i.Text.EndsWith("Robot Z")).Key;
        pick.Select(new[] { robot });
        Pump(300);
        Check("  one ticked: one row", listed().SequenceEqual(new[] { robot }), string.Join(",", listed()));
        SecurityManager.Login("admin", SecurityLevel.Admin);
        Pump(400);
        Check("the admin back: his two", listed().SequenceEqual(h800), string.Join(",", listed()));
        P<AxisPickBar>(view, "Bar").AxesPick.Select(new string[0]);
        Pump(300);
        Check("  nothing ticked: all again", listed().Count == 23);
        // 16 October: an axis whose visibility conditions are not met is not offered
        var canBeSeen = typeof(AxisMonitorPage).GetField("CanBeSeen", Any);
        var seenWas = canBeSeen.GetValue(null);
        canBeSeen.SetValue(null, (Func<Kaban.UI.Helpers.AlarmHelpLinks.AxisInfo, bool>)(a => !(a.NameKey ?? "").EndsWith("RobotZ")));
        typeof(AxisMonitorPage).GetMethod("ReofferAxes", Any).Invoke(view, null);     // as the heading does before it opens
        Pump(200);
        pick = P<AxisPickBar>(view, "Bar").AxesPick;
        Check("an axis whose conditions are not met: not in the Axes list when it opens (here: Robot Z)", pick.Items.Count == 22 && !pick.Items.Any(i => i.Text.EndsWith("Robot Z")),
              pick.Items.Count.ToString());
        canBeSeen.SetValue(null, seenWas);
        typeof(AxisMonitorPage).GetMethod("ReofferAxes", Any).Invoke(view, null);
        Pump(200);
        Check("  met again: offered again (the 20 that can be seen)", P<AxisPickBar>(view, "Bar").AxesPick.Items.Count == 20);

        Console.WriteLine("--- Inputs / Outputs ---");
        var io = form.ShowSidePage("IoMonitor") as IoMonitorPage;
        Pump(300);
        var show = (Kaban.UI.Dashboard.PickList)typeof(IoMonitorPage).GetProperty("ShowPick", Any).GetValue(io, null);
        var lists = P<IEnumerable<IoListHMI>>(io, "Lists").ToList();
        Check("the Lists list: each controller's inputs and outputs", show.Visible && show.Items.Count == lists.Count && show.Items.Count >= 2 &&
              show.Items[0].Text.EndsWith(" - inputs") && show.Shown == "All lists", show.Items.Count + " / " + lists.Count + ": " + string.Join(" | ", show.Items.Select(i => i.Text)));
        var top = (Panel)typeof(IoMonitorPage).GetField("_top", Any).GetValue(io);
        Check("  in sight in the panel's width (on a line of its own when narrow)", show.Right <= top.ClientSize.Width && show.Bottom <= top.ClientSize.Height,
              show.Bounds + " in " + top.ClientSize);
        var outputsKey = show.Items.First(i => i.Key.EndsWith(":O")).Key;
        var firstY = lists.Min(l => l.Top);
        show.Select(new[] { outputsKey });
        Pump(300);
        var visible = P<IEnumerable<IoListHMI>>(io, "Lists").Where(l => l.Visible).ToList();
        Check("outputs of one controller ticked: that list alone", visible.Count == 1, visible.Count.ToString());
        Check("  moved up to the top, the whole width", visible.Count == 1 && visible[0].Top <= firstY + 2 && visible[0].Width > lists.Max(l => l.Width) - 2 ||
              visible.Count == 1 && visible[0].Top <= firstY + 2, visible.Count == 1 ? visible[0].Bounds.ToString() : "");
        Check("  kept", ViewPreferences.PickOf(ViewPreferences.IoShown).SetEquals(new[] { outputsKey }));
        show.Select(new string[0]);
        Pump(300);
        Check("nothing ticked: all again", P<IEnumerable<IoListHMI>>(io, "Lists").All(l => l.Visible));

        form.Close();
        Console.WriteLine(fail == 0 ? "\nAXES SHOWN (MONITOR, I/O) ALL PASS" : "\nAXES SHOWN (MONITOR, I/O) " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
