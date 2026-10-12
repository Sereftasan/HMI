using System;
using System.Drawing;
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

// 12 October, the panel's screenshot (150%, Admin, PLC axis items: Manual movement, nine Motion monitor
// columns): "the bottom scroll doesn't move any more right than that and it resets the right scroll to the
// top as well ... it sometimes works alright with the same screen scale and the same selections". It stopped
// with Robot X's back jog button at the list's left edge: the side page, given the focus as it opened, gave
// it to that button - the first control of the first row - and Windows scrolls a panel back to its focused
// control (sideways to its left edge, up to its row) whenever the focus is given to it again. Only while that
// button had the focus: "sometimes". The list now takes the side page's focus itself, and never jumps back
// to a control focused before; a control newly focused is still brought into sight.
class AMSt {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static Control Focused(Control root) { foreach (Control c in root.Controls) { if (c.Focused) return c; var f = Focused(c); if (f != null) return f; } return null; }
    // what Windows asks the list for when the focus is given to a control in it again
    static Point AskedFor(ScrollableControl list, Control c) {
        return (Point)typeof(ScrollableControl).GetMethod("ScrollToControl", Any).Invoke(list, new object[] { c });
    }

    [STAThread]
    static void Main() {
        UiScale.Override(1.5);
        string dir = Path.Combine(Path.GetTempPath(), "amst_" + Guid.NewGuid().ToString("N"));
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
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(0, 0); form.Size = new Size(1280, 1000);
        form.Show();
        Pump(500);
        ViewPreferences.SetList(ViewPreferences.MonitorItems, new[] { "Manual" });
        ViewPreferences.SetList(ViewPreferences.MonitorMotion, new[] { "ErrorDetected", "CommandInPosition", "ServoReady", "ServoOn", "InPosition",
                                                                       "HomeRequest", "HomeComplete", "AxisError", "AxisWarning" });

        Console.WriteLine("--- the screenshot's monitor: 150%, Manual movement, nine Motion monitor columns ---");
        var view = form.ShowSidePage("AxisMonitor") as AxisMonitorPage;
        Pump(800);
        var list = (FlowLayoutPanel)typeof(AxisMonitorPage).GetField("_list", Any).GetValue(view);
        var rows = list.Controls.OfType<AxisSettingHMI>().ToList();
        var jog = rows.Count > 0 ? rows[0].Controls.OfType<ButtonHMI>().OrderBy(b => b.Left).FirstOrDefault() : null;
        Check("the rows wider and taller than the panel, the first with its back jog button", rows.Count > 8 && jog != null &&
              rows[0].Width > list.ClientSize.Width && list.DisplayRectangle.Height > list.ClientSize.Height,
              rows.Count + " rows " + (rows.Count > 0 ? rows[0].Width : 0) + " wide in " + list.ClientSize);
        var f = Focused(form);
        Check("opened: the list has the side page's focus - not Robot X's jog button", f == list, f == null ? "none" : f.GetType().Name + " " + f.Name);
        view.Focus(); Pump(150);
        Check("  given the focus again: still the list", Focused(form) == list);

        Console.WriteLine("--- a control newly focused: brought into sight ---");
        var lastRow = rows.Where(r => r.Visible).OrderBy(r => r.Top).Last();
        var far = lastRow.Controls.OfType<ButtonHMI>().OrderBy(b => b.Left).FirstOrDefault();
        Console.WriteLine("    last row " + lastRow.Bounds + " visible " + lastRow.Visible + ", its button " + (far == null ? "none" : far.Bounds.ToString()));
        var before = list.DisplayRectangle.Location;
        var at = AskedFor(list, far);
        int expected = before.Y - (lastRow.Top + far.Bottom - list.ClientSize.Height);
        Check("the last row's jog button, out of sight below: scrolled down until its bottom is in sight, not sideways",
              far != null && at.Y == expected && at.X == before.X, far == null ? "no button" : at + " / " + before + ", expected Y " + expected);
        Check("  asked again: the list stays", far != null && AskedFor(list, far) == list.DisplayRectangle.Location);

        Console.WriteLine("--- a control of the first row focused, the list scrolled away from it ---");
        jog.Focus(); Pump(100);
        at = AskedFor(list, jog);
        Check("the jog button tapped, in sight: the list stays", at == list.DisplayRectangle.Location, at + " / " + list.DisplayRectangle.Location);
        list.AutoScrollPosition = new Point(600, 400);
        Pump(300);
        var scrolled = list.DisplayRectangle.Location;
        Check("scrolled right past it and down", scrolled.X <= -600 && scrolled.Y <= -400, scrolled.ToString());
        at = AskedFor(list, jog);
        Check("the focus given to it again: the list stays where it is scrolled to (not back to its left edge, not to the top)",
              at == scrolled, at + " / " + scrolled);
        at = AskedFor(list, rows[0]);
        Check("  nor for its row (wider than the list: it was put at its left edge)", at == scrolled, at.ToString());
        view.Focus(); Pump(200);
        Check("  the side page given the focus: still there", list.DisplayRectangle.Location == scrolled, list.DisplayRectangle.Location.ToString());
        list.AutoScrollPosition = new Point(100000, 400);
        Pump(300);
        int right = -list.DisplayRectangle.X;
        Check("  and the scroll goes on to the right end", right >= list.DisplayRectangle.Width - list.ClientSize.Width - 2 && right > 600,
              right + " of " + (list.DisplayRectangle.Width - list.ClientSize.Width));

        Console.WriteLine("--- an Axis Settings page's rows: the same ---");
        var frame = new AxisPageFrame();
        Check("its body scrolls so too", frame.Body is SteadyScroll.Panel);
        frame.Dispose();

        form.Close();
        Console.WriteLine(fail == 0 ? "\nAXIS MONITOR STEADY ALL PASS" : "\nAXIS MONITOR STEADY " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
