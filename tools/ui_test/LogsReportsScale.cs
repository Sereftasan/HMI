using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Dashboard;
using Kaban.UI.Settings;

// LOGS AND REPORTS AT ANOTHER SCREEN SCALE (3 November: "This page breaks when the screen scale changed" - at 120% the
// frames stayed as wide as at 100%, their rows scaled past them: the fields ran out of the frames, the switches at the
// rows' right ends were cut off). The scale: LR_SCALE (1.2 when not set); PAGE_SHOTS: a picture of the page.
class LRS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in All(k)) yield return d; } }
    static Rectangle Screen(Control c) { return c.Parent.RectangleToScreen(c.Bounds); }

    [STAThread]
    static void Main() {
        double factor = 1.2;
        var env = Environment.GetEnvironmentVariable("LR_SCALE");
        if (!string.IsNullOrEmpty(env)) factor = double.Parse(env, System.Globalization.CultureInfo.InvariantCulture);
        UiScale.Override(factor);
        string pct = ((int)Math.Round(factor * 100)) + "%";
        var dir = Path.Combine(Path.GetTempPath(), "lrs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        DashboardConfig.FolderOverride = Path.Combine(dir, "settings");
        DashboardService.AutoReports = false;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        DashboardService.Register("LOGTEST", Path.Combine("Dashboard", "config.json"),
                                  Path.Combine("Dashboard", "StateMap.json"), Path.Combine("Dashboard", "Translation_JSON.csv"));

        Console.WriteLine("--- Logs and Reports at " + pct + " ---");
        var form = new Form { ClientSize = new Size(1200, 760), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var ls = (LogSettingsControl)new LogSettingsPage().GetControl();
        form.Controls.Add(ls);
        UiScale.ApplyOnLoad(form);
        form.Show(); Pump(300);
        Kaban.UI.Helpers.TouchInput.Sweep();                       // (the plain tick boxes drawn as switches, as in the program)
        foreach (var c in All(form)) c.Invalidate();
        Pump(600);
        var settings = ls.MachineSettings;
        var audit = ls.Controls.Find("auditGroup", true).OfType<GroupBoxHMI>().First();
        var frames = new List<GroupBoxHMI> { audit };
        frames.AddRange(settings.Groups);
        Check("the frames: the audit log's and the log database's (" + frames.Count + ")", frames.Count >= 5);

        // every part of a frame inside it - its right end left of the frame's line
        var outside = new List<string>();
        foreach (var g in frames) {
            var gr = Screen(g);
            foreach (var c in All(g).Where(c => c.Visible && !(c is FlowLayoutPanel) && !(c is TableLayoutPanel))) {
                var r = Screen(c);
                if (r.Right > gr.Right - 2 || r.Left < gr.Left + 2)
                    outside.Add(g.Name + ":" + (c.Name.Length > 0 ? c.Name : c.GetType().Name + " '" + c.Text + "'") + " " + r.Left + ".." + r.Right + " in " + gr.Left + ".." + gr.Right);
            }
        }
        Check("every field, button and switch inside its frame (none past its right side)", outside.Count == 0, outside.Count + ": " + string.Join(" | ", outside.Take(5)));
        var lefts = frames.Select(g => Screen(g).Left).Distinct().ToList();
        var widths = frames.Select(g => g.Width).Distinct().ToList();
        Check("  the frames in line and as wide as each other, scaled with the page", lefts.Count == 1 && widths.Count == 1 &&
              Math.Abs(widths[0] - UiScale.Px(DashboardSettingsControl.GroupWidth)) <= 2,
              string.Join(" ", lefts) + " / " + string.Join(" ", widths) + " / want " + UiScale.Px(DashboardSettingsControl.GroupWidth));
        var zone = settings.ZoneBox;
        var switches = new[] { "state_pie", "state_timeline", "state_logs", "event_quantity", "cutting_details", "event_logs", "alarm_logs" }.Select(settings.SectionBox).ToList();
        Check("  the switches end where the time zone's list ends", switches.All(c => Math.Abs(Screen(c).Right - Screen(zone).Right) <= 2),
              Screen(zone).Right + " / " + string.Join(" ", switches.Select(c => Screen(c).Right)));

        var shotDir = Environment.GetEnvironmentVariable("PAGE_SHOTS");
        if (!string.IsNullOrEmpty(shotDir)) {
            Directory.CreateDirectory(shotDir); Pump(300);
            var at = form.PointToScreen(Point.Empty);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("python3", "-c \"from PIL import ImageGrab; ImageGrab.grab(bbox=(" + at.X + "," + at.Y + "," +
                (at.X + form.ClientSize.Width) + "," + (at.Y + form.ClientSize.Height) + "), xdisplay=':99').save('" +
                Path.Combine(shotDir, "logs_reports_" + pct.TrimEnd('%') + ".png") + "')\"") { UseShellExecute = false }).WaitForExit(10000);
        }
        Console.WriteLine(fail == 0 ? "\nLOGS REPORTS SCALE ALL PASS" : "\nLOGS REPORTS SCALE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
