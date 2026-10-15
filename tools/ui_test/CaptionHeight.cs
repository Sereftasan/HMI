using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

// 12 October (REQUESTS.md): "The description texts' bottom side doesn't get rendered on the screen" - FA2010's
// Machine Parameters > Others at the screen scale 80%, captions on the left (View Preferences): a caption no
// longer sized by itself kept a height the smaller font needed more than, and the bottom of its letters was cut.
class CH {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static IEnumerable<Control> All(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; } }
    static readonly System.Reflection.MethodInfo OwnerOf =
        typeof(UiScale).Assembly.GetType("Kaban.UI.Controls.AttachedDescription").GetMethod("OwnerOf");

    static List<string> Clipped(Control page) {
        var bad = new List<string>();
        foreach (var l in All(page).OfType<Label>().Where(x => OwnerOf.Invoke(null, new object[] { x }) != null)) {
            int need = Math.Max(l.PreferredHeight, TextRenderer.MeasureText(l.Text, l.Font).Height + l.Padding.Vertical);
            if (l.Height < need) bad.Add(l.Text + " " + l.Height + "<" + need);
        }
        return bad;
    }

    [STAThread]
    static void Main() {
        UiScale.Override(0.8);
        var dir = Path.Combine(Path.GetTempPath(), "ch_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.LoadMachineTranslations("Translations.csv");
        UnitManager.EnsureDefaults();
        // (14 October: the machine's options are on an Options page of their own now - both pages)
        foreach (var side in new[] { "Left", "Right" })
        foreach (var which in new[] { "Others", "Options" }) {
            ViewPreferences.SetPanel(ViewPreferences.CaptionAlign, side);
            var form = new Form { ClientSize = new Size(1270, 800), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
            Control page = which == "Others" ? (Control)new Kaban.Machines.FA2010.Screens.Parameters.ParamOther() : new Kaban.Machines.FA2010.Screens.Parameters.Options();
            page.Dock = DockStyle.Fill;
            form.Controls.Add(page);
            UiScale.ApplyOnLoad(form);
            form.Show(); Pump(800);
            int n = All(page).OfType<Label>().Count(x => OwnerOf.Invoke(null, new object[] { x }) != null);
            var bad = Clipped(page);
            Check("at 80%, captions " + side.ToLower() + ": every caption of " + which + " as high as its letters (" + n + ")", n >= 15 && bad.Count == 0,
                  string.Join(" | ", bad.Take(4)));
            form.Close(); form.Dispose(); Pump(100);
        }
        Console.WriteLine(fail == 0 ? "\nCAPTION HEIGHT ALL PASS" : "\nCAPTION HEIGHT " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
