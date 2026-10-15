using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

// 9 October: the Clamping Moulds page of PneumaticPositioningHMI rows at the magnifier's 1.5 - the
// rows, their parts and the headings scaled together, the rows still moving up when the back moulds
// are taken out. SHOT_DIR set: a picture of the page (the option on, then off).
class PPS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Shot(string name, int w, int h) {
        var dir = Environment.GetEnvironmentVariable("SHOT_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("import", "-window root -crop " + w + "x" + h + "+0+0 " + Path.Combine(dir, name)) { UseShellExecute = false });
        p.WaitForExit(10000);
    }

    [STAThread]
    static void Main() {
        double factor = double.Parse(Environment.GetEnvironmentVariable("SHOT_FACTOR") ?? "1.5", System.Globalization.CultureInfo.InvariantCulture);
        UiScale.Override(factor);
        var dir = Path.Combine(Path.GetTempPath(), "pps_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.LoadMachineTranslations("Translations.csv");
        UnitManager.EnsureDefaults();
        int fw = 1270, fh = 1000;
        var form = new Form { ClientSize = new Size(fw, fh), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0), FormBorderStyle = FormBorderStyle.None };
        var page = new Kaban.Machines.FA2010.Screens.Manual.ManualClamping_Moulds { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        UiScale.ApplyOnLoad(form);
        form.Show(); Pump(1000);
        form.ClientSize = new Size(fw, fh); Pump(300);

        var rows = PneumaticPositioningHMI.RowsOf(page);
        // the page a table once shown (AxisPageFrame): the headings in its heading, the rows in its body
        var heads = CylinderRowHMI.HeadingsHost(page).Controls.OfType<Label>().Where(l => (l.Tag as string ?? "").StartsWith(PneumaticPositioningHMI.HeadingTag)).ToList();
        Func<Control, Rectangle> scr = c => c.RectangleToScreen(c.ClientRectangle);
        Console.WriteLine("--- at " + factor + " ---");
        Check("16 rows, each as tall as an axis row at the scale", rows.Count == 16 && rows.All(r => r.Height == UiScale.Px(AxisSettingHMI.RowHeight)),
              rows.Count + " rows, " + rows[0].Height);
        var f = rows[0];
        Check("  the parts at the scale: the jog " + UiScale.Px(AxisSettingHMI.WJog) + " wide, the fields " + UiScale.Px(AxisSettingHMI.WSetting),
              Math.Abs(f.Unit.ButtonA.Width - UiScale.Px(AxisSettingHMI.WJog)) <= 2 && Math.Abs(f.TargetField.Width - UiScale.Px(AxisSettingHMI.WSetting)) <= 1,
              f.Unit.ButtonA.Width + " / " + f.TargetField.Width);
        Check("  every part inside its row", rows.SelectMany(r => r.Controls.Cast<Control>()).All(c => c.Left >= 0 && c.Right <= c.Parent.Width + 1 && c.Top >= 0 && c.Bottom <= c.Parent.Height + 1));
        Check("  the names fit their column", rows.All(r => TextRenderer.MeasureText(r.NameLabel.Text, r.NameLabel.Font).Width <= r.NameLabel.Width + 1));
        bool over = heads.All(h => {
            var key = ((string)h.Tag).Substring(PneumaticPositioningHMI.HeadingTag.Length);
            var col = f.Columns[key];
            return Math.Abs(scr(h).Left - (scr(f).Left + UiScale.Px(col.X))) <= 1 && Math.Abs(h.Width - UiScale.Px(col.Y)) <= 1 && scr(h).Bottom <= scr(f).Top;
        });
        Check("  the five headings over their columns, above the rows", heads.Count == 5 && over,
              string.Join(" ", heads.Select(h => h.Text + "@" + h.Left)));
        Check("  the parts of a column line up down the page", rows.All(r => r.TargetField.Left == f.TargetField.Left && r.Unit.Left == f.Unit.Left && r.DirectionRefField.Right == f.DirectionRefField.Right));
        Shot("moulds_on.png", fw, fh);

        Func<SystemCondition, bool> off = c => ((long)c & (1L << 19)) == 0;
        foreach (var r in rows) r.UpdateState(a => 0, off);
        Pump(200);
        var shown = rows.Where(r => r.Visible).OrderBy(r => r.Top).ToList();
        var slots = rows.Select(r => r.Top).OrderBy(t => t).Take(6).ToList();
        Check("the option off: the 6 front moulds in the first six places", shown.Count == 6 && shown.Select(r => r.Top).SequenceEqual(slots),
              string.Join(" ", shown.Select(r => r.Top)));
        Shot("moulds_off.png", fw, Math.Min(fh, shown.Last().Bottom + UiScale.Px(24)));
        form.Close();
        Console.WriteLine(fail == 0 ? "\nPNEUMATIC POSITIONING SCALE ALL PASS" : "\nPNEUMATIC POSITIONING SCALE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
