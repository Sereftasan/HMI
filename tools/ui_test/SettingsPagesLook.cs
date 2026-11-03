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

    static readonly Type Captions = typeof(UiScale).Assembly.GetType("Kaban.UI.Controls.AttachedDescription");
    static Control OwnerOf(Control l) { return (Control)Captions.GetMethod("OwnerOf").Invoke(null, new object[] { l }); }
    static Control ActingOwnerOf(Control l) { return (Control)Captions.GetMethod("ActingOwnerOf").Invoke(null, new object[] { l }); }

    /// <summary>A page's fields as settings rows: each field's caption the row, the rows touching, a light line under each.</summary>
    static void RowsOf(string n, Control page) {
        var fields = All(page).OfType<NumericInputHMI>().ToList();
        var labels = All(page).OfType<Label>().Where(l => OwnerOf(l) != null).ToDictionary(l => OwnerOf(l), l => l);
        var bad = new List<string>();
        foreach (var f in fields) {
            Label l;
            if (f.RowWidth <= 0 || !labels.TryGetValue(f, out l)) { bad.Add(f.Name + " no row"); continue; }
            int mid = f.Top + f.Height / 2;
            if (Math.Abs(l.Width - UiScale.Px(f.RowWidth)) > 1 || Math.Abs(l.Height - UiScale.Px(UiStyle.SettingRowHeight)) > 1 ||
                Math.Abs(l.Right - (f.Right + UiScale.Px(8))) > 1 || Math.Abs(l.Top + l.Height / 2 - mid) > 1)
                bad.Add(f.Name + " " + l.Bounds + " / " + f.Bounds);
            else if (l.Parent.Controls.GetChildIndex(l) < l.Parent.Controls.GetChildIndex(f)) bad.Add(f.Name + " caption in front");
            else if (l.TextAlign != ContentAlignment.MiddleLeft || l.Text.TrimEnd().EndsWith(":")) bad.Add(f.Name + " '" + l.Text + "' " + l.TextAlign);
            else if (ActingOwnerOf(l) != f) bad.Add(f.Name + " row not acting as the field");
        }
        Check(n + ": every field (" + fields.Count + ") a settings row - its caption the row (" + fields.Select(f => f.RowWidth).FirstOrDefault() +
              " wide, " + UiStyle.SettingRowHeight + " high, ending 8 px past the field, behind it, words at the left, no colon)",
              fields.Count > 0 && bad.Count == 0, bad.Count + ": " + string.Join(" | ", bad.Take(3)));
        var apart = new List<string>();
        foreach (var col in labels.Values.Where(l => fields.Contains(OwnerOf(l))).GroupBy(l => new { l.Parent, l.Left })) {
            var ordered = col.OrderBy(l => l.Top).ToList();
            for (int i = 1; i < ordered.Count; i++) if (Math.Abs(ordered[i].Top - ordered[i - 1].Bottom) > 1) apart.Add(ordered[i - 1].Text + "/" + ordered[i].Text);
            if (ordered[0].Parent is GroupBoxHMI && Math.Abs(ordered.Last().Bottom - (ordered[0].Parent.Height - UiScale.Px(8))) > 2)
                apart.Add(ordered[0].Parent.Name + " not round its rows " + ordered.Last().Bottom + "/" + ordered[0].Parent.Height);
        }
        Check("  the rows of a column touching (a frame round its rows, 8 px under the last)", apart.Count == 0, string.Join(" ", apart.Take(4)));
        // the row's line and its light, drawn
        var first = labels[fields.OrderBy(f => f.Top).First()];
        Color line;
        using (var bmp = new Bitmap(first.Width, first.Height)) { first.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height)); line = bmp.GetPixel(bmp.Width / 2, bmp.Height - 1); }
        typeof(Control).GetMethod("OnMouseEnter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(first, new object[] { EventArgs.Empty });
        Pump(30);
        bool lit = first.BackColor == UiStyle.RowHoverFill;
        typeof(Control).GetMethod("OnMouseLeave", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(first, new object[] { EventArgs.Empty });
        Pump(30);
        Check("  a light line under a row (" + UiStyle.RowRule + "); the pointer on the row lights it all, off it - not",
              line.ToArgb() == UiStyle.RowRule.ToArgb() && lit && first.BackColor == Color.Transparent, line + " " + lit);
    }

    [STAThread]
    static void Main() {
        var scale = Environment.GetEnvironmentVariable("SPL_SCALE");      // another screen scale: the rows only
        if (!string.IsNullOrEmpty(scale)) UiScale.Override(double.Parse(scale, System.Globalization.CultureInfo.InvariantCulture));
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
            UiScale.ApplyOnLoad(form);
            form.Show(); Pump(500);
            Shot(form, "settings_" + n + (UiScale.Active ? "_" + (int)Math.Round(UiScale.Factor * 100) : ""));
            if (n == "Options" && !UiScale.Active) {
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
            if (n.StartsWith("Param") && n != "ParamEncoders") RowsOf(n, page);
            if (n == "AxisSettings" || n == "ParamEncoders") {
                // the table's rows: a light line in the gap between two
                // (the axes' rows are moved into the table's own scrolling part as the page is made)
                var rows = All(page).Where(c => c is AxisSettingHMI || c is CylinderEncoderSettingHMI).GroupBy(c => c.Parent)
                                    .OrderByDescending(g => g.Count()).First().OrderBy(c => c.Top).ToList();
                var host = rows[0].Parent;
                var a = rows[0]; var b = rows[1];
                int y = a.Bottom + (b.Top - a.Bottom) / 2, x = a.Left + Math.Min(a.Width, host.ClientSize.Width) / 2;
                Color at, above;
                using (var bmp = new Bitmap(host.Width, host.Height)) {
                    host.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    at = bmp.GetPixel(x, y); above = bmp.GetPixel(x, a.Bottom);
                }
                Check(n + ": a light line between the rows (" + rows.Count + "), in the gap", rows.Count >= 10 && b.Top - a.Bottom >= 2 &&
                      at.ToArgb() == UiStyle.RowRule.ToArgb(), at + " at y " + y + " (" + a.Bottom + ".." + b.Top + "), " + above);
            }
            form.Close(); form.Dispose(); Pump(100);
        }
        Console.WriteLine(fail == 0 ? "\nSETTINGS PAGES LOOK ALL PASS" : "\nSETTINGS PAGES LOOK " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
