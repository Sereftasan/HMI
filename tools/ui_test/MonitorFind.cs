using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;

// The PLC Monitor's Find on the REAL FA2010 program: every use of a device,
// by address, label or name - and a click opens the page with the unit framed.
class MF {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }

    [STAThread]
    static void Main() {
        // FA2010's own start-up, as Program.Main runs it (see RealMachinesDiscovery).
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        // Simulated controllers: the real connection starts MX Component's own
        // message loop, which Mono's WinForms does not survive beside a window.
        foreach (int st in new[] { 1, 2, 3, 4 })
            PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));
        PlcLabelTable.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));
        SecurityManager.Login("admin", SecurityLevel.Admin);

        Console.WriteLine("--- every use of a device, from the pages themselves ---");
        var t0 = DateTime.Now;
        var uses = DeviceCollector.UsesOf(1);
        Console.WriteLine("      {0} uses on station 1, {1} ms", uses.Count, (int)(DateTime.Now - t0).TotalMilliseconds);
        var m672 = DeviceFindForm.Match(uses, 1, "m672");
        Check("M672 (typed in lower case) is found on the Cutting page", m672.Count >= 1 && m672.All(u => u.PageTitle.Contains("Cutting")),
              string.Join(" | ", m672.Select(u => u.PageTitle + ": " + u.UsedAs)));
        Check("  named by its unit", m672.Any(u => u.UsedAs.StartsWith("Left Top Clamping")));
        Check("  with the way to it: the unit's path on the page", m672.All(u => u.ControlPath.Count > 0 && u.Screen != null),
              string.Join(" / ", m672.First().ControlPath));
        var byLabel = DeviceFindForm.Match(uses, 1, "MAN_KesimSol_UstAC");
        Check("its PLC label finds the same", byLabel.Count == m672.Count);
        var byName = DeviceFindForm.Match(uses, 1, "clamping");
        Check("part of a unit's name finds every use it has", byName.Count > m672.Count && byName.All(u => u.UsedAs.ToLower().Contains("clamping") ||
              (PlcLabelTable.ByAddress(1, u.Address) != null && PlcLabelTable.ByAddress(1, u.Address).Label.ToLower().Contains("clamping"))),
              byName.Count + " uses");
        var twice = uses.GroupBy(u => u.PageTitle + "|" + u.Address).FirstOrDefault(g => g.Count() > 1);
        Check("a device used twice on one page is listed twice", twice != null, twice == null ? "" : twice.Key);
        Check("nothing found: an empty list, not an error", DeviceFindForm.Match(uses, 1, "M99999").Count == 0);

        Console.WriteLine("--- a click opens the page and frames the unit ---");
        var form = new MainForm();
        form.BuildNavigation();
        form.Show();
        // The form starts signed out and queues the login dialog; signed in
        // before the queue runs, the dialog is not offered.
        SecurityManager.Login("admin", SecurityLevel.Admin);
        Application.DoEvents();
        string why;
        var target = form.ShowDeviceUse(m672.First(), out why);
        Application.DoEvents();
        Check("the page opens and the unit is found", target != null && target.Name == "unitLeft_Top_Clamping", why);
        Check("  it is on show", target != null && target.Visible && target.FindForm() == form);
        Check("  the Cutting page is the one on display",
              target != null && IsInside(target, form.CurrentPage));
        ControlHighlight.Flash(target, 1500);
        Application.DoEvents();
        Check("  framed", ControlHighlight.IsShowing && ControlHighlight.Target == target);
        var until = DateTime.Now.AddMilliseconds(2200);
        while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }
        Check("  and the frame goes by itself", !ControlHighlight.IsShowing);

        var elsewhere = DeviceFindForm.Match(uses, 1, "clamping").FirstOrDefault(u => !u.PageTitle.Contains("Cutting") && !u.IsAlarm);
        if (elsewhere != null) {
            var t2 = form.ShowDeviceUse(elsewhere, out why);
            Check("another page: " + elsewhere.PageTitle, t2 != null && t2.Visible, why);
        }
        var alarm = uses.FirstOrDefault(u => u.IsAlarm);
        if (alarm != null) {
            Check("an alarm definition's bit is listed but has no page to open", form.ShowDeviceUse(alarm, out why) == null && why.Length > 0, why);
        }

        Console.WriteLine("--- the real Robot Top Clamping: its manual is ONE chart for its valve and feedback ---");
        var m854 = DeviceFindForm.Match(uses, 1, "M854").FirstOrDefault(u => !u.IsAlarm);
        var rtc = m854 == null ? null : form.ShowDeviceUse(m854, out why) as CylinderUnitHMI;
        Check("found on the Exit page", rtc != null && rtc.Name == "unitRobot_Top_Clamping", why);
        if (rtc != null) {
            Check("  nothing failed: its '?' offers no troubleshooting", !rtc.HelpDocuments().Any(d => d.FileName == "Cylinder_Troubleshooting.html"));
            // Its error bit comes on (the manual is a fault's help only, 30 September).
            var watch = (AlarmHelpBadge)typeof(CylinderUnitHMI).GetField("_helpA", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(rtc);
            watch.UpdateState(a => a == "M854" ? 1 : 0, c => true);
            var docs = rtc.HelpDocuments();
            var manual = docs.FirstOrDefault(d => d.FileName == "Cylinder_Troubleshooting.html" && !d.StepByStep);
            Check("  M854 on: its '?' offers the manual for its drawn types: 5/3 valve + encoder and reference sensor",
                  manual != null && manual.Valve == 3 && manual.Feedback == 6, string.Join(" | ", docs.Select(d => d.Title + " v" + d.Valve + " f" + d.Feedback)));
            var html = System.IO.File.ReadAllText(TroubleshootingCatalog.ExtractSection(manual));
            int heads = System.Text.RegularExpressions.Regex.Matches(html, "<thead>").Count;
            int d14 = System.Text.RegularExpressions.Regex.Matches(html, "data-detail=\"1.4\"").Count;
            int d17 = System.Text.RegularExpressions.Regex.Matches(html, "data-detail=\"1.7\"").Count;
            Check("  opened: one heading row, 1.4 once, 1.7 once", heads == 1 && d14 == 1 && d17 == 1, heads + " headings, 1.4 x" + d14 + ", 1.7 x" + d17);
        }

        form.Close();
        Console.WriteLine(fail == 0 ? "\nMONITOR FIND ALL PASS" : "\nMONITOR FIND " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    static bool IsInside(Control c, Control root) {
        for (var p = c; p != null; p = p.Parent) if (p == root) return true;
        return false;
    }
}
