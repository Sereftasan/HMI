using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// THE MACHINE SETTINGS PAGES' ROWS (3 November: "make the height of the controls in 'Machine Settings / Options' page a little
// less and make all the other machine setting pages controls just same as the ones at this page (the controls are divided with
// a light line in between them). I want you to make this divider line a little less visible though").
// PAGE_SHOTS: a picture of each page.
class SPL {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in All(k)) yield return d; } }
    static System.Reflection.Assembly fa;
    static Control Page(string name) { return (Control)Activator.CreateInstance(fa.GetTypes().First(x => x.Name == name)); }

    static void Shot(Form form, string name) {
        var dir = Environment.GetEnvironmentVariable("PAGE_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir); Pump(200);
        var at = form.PointToScreen(Point.Empty);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("python3", "-c \"from PIL import ImageGrab; ImageGrab.grab(bbox=(" + at.X + "," + at.Y + "," +
            (at.X + form.ClientSize.Width) + "," + (at.Y + form.ClientSize.Height) + "), xdisplay=':99').save('" + Path.Combine(dir, name + ".png") + "')\"") { UseShellExecute = false }).WaitForExit(10000);
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "spl_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.LoadMachineTranslations("Translations.csv");
        LanguageManager.ChangeLanguage("EN");
        UnitManager.EnsureDefaults();
        SecurityManager.Login("admin", SecurityLevel.Admin);
        fa = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly;
        AlarmHelpLinks.EnsureDiscovered();

        Console.WriteLine("--- the line between the rows: lighter than a table's ---");
        Func<Color, int> light = c => c.R + c.G + c.B;
        Check("the rows' line (" + UiStyle.RowRule + ") lighter than the tables' (" + UiStyle.GridRule + "), still a line on white",
              light(UiStyle.RowRule) > light(UiStyle.GridRule) && light(UiStyle.RowRule) < 3 * 245);

        var pages = new[] { "Options", "AxisSettings", "ParamConveyor", "ParamRobot", "ParamMilling", "ParamCutting", "ParamEncoders", "ParamOther" };
        foreach (var n in pages) {
            var form = new Form { ClientSize = new Size(1270, 760), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0), BackColor = Color.White };
            var page = Page(n);
            // (the Options page was saved by Visual Studio with AutoScaleMode Font: Mono's font scaled it - the program sets it
            // to None as it scales its pages itself, UiScale)
            if (page is ContainerControl) ((ContainerControl)page).AutoScaleMode = AutoScaleMode.None;
            page.Dock = DockStyle.Fill;
            form.Controls.Add(page);
            form.Show(); Pump(500);
            Shot(form, "settings_" + n);
            if (n == "Options") {
                var rows = All(page).OfType<CheckBoxHMI>().ToList();
                Check("Options: every switch a row " + UiStyle.SettingRowHeight + " high (46 before: 'a little less')", rows.Count == 23 &&
                      rows.All(r => r.DrawnAsRow && r.Height == UiStyle.SettingRowHeight), string.Join(" ", rows.Select(r => r.Height).Distinct()));
                // each column's rows touching: one under the other, none apart, none over another
                var apart = new List<string>();
                foreach (var col in rows.GroupBy(r => new { r.Parent, r.Left })) {
                    var ordered = col.OrderBy(r => r.Top).ToList();
                    for (int i = 1; i < ordered.Count; i++) if (ordered[i].Top != ordered[i - 1].Bottom) apart.Add(ordered[i - 1].Name + "/" + ordered[i].Name);
                }
                Check("  the rows of a column touching", apart.Count == 0, string.Join(" ", apart));
                var group = All(page).OfType<GroupBoxHMI>().First();
                var inGroup = rows.Where(r => r.Parent == group).ToList();
                var below = rows.Where(r => r.Parent == page && r.Left == group.Left).OrderBy(r => r.Top).ToList();
                Check("  the Mullion Notching frame round its five rows, the right column's rows under it", inGroup.Count == 5 &&
                      group.Height == inGroup.Max(r => r.Bottom) + 8 && below.Count == 6 && below[0].Top == group.Bottom + 16,
                      group.Height + " / " + below.Select(r => r.Top).FirstOrDefault());
            }
            form.Close(); form.Dispose(); Pump(100);
        }
        Console.WriteLine(fail == 0 ? "\nSETTINGS PAGES LOOK ALL PASS" : "\nSETTINGS PAGES LOOK " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
