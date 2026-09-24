using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Settings;

// HMI Settings: no "Manual" tab any more; About opens Help\Kaban_HMI_Manual.<lang>.html.
class AM {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static System.Collections.Generic.IEnumerable<Control> All(Control r) {
        foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; }
    }
    [STAThread] static void Main() {
        var hmi = (SettingsScreen)new HmiSettingsPage().GetControl();
        var keys = hmi.AllPages.Select(p => p.TitleKey).ToList();
        Check("no Manual tab", !keys.Contains("Set_Manual"), string.Join(",", keys));
        Check("About is there", keys.Contains("Set_About"));

        var about = new AboutControl();
        var btn = All(about).OfType<Button>().FirstOrDefault(b => b.Text.Contains("manual"));
        Check("About has the button that opens the manual", btn != null, btn == null ? "" : btn.Text);

        string path = AboutControl.ManualPath();
        Check("the manual resolves to the English file", path.EndsWith("Kaban_HMI_Manual.en.html"), path);
        Check("  which the build put in the Help folder", File.Exists(path), path);
        Check("  and is HTML", File.Exists(path) && File.ReadAllText(path).Contains("<html"));

        LanguageManager.ChangeLanguage("TR");
        string tr = AboutControl.ManualPath();
        Check("Turkish, with no Turkish file yet: falls back to English", File.Exists(tr) && tr.EndsWith(".en.html"), tr);

        Console.WriteLine(fail == 0 ? "\nABOUT ALL PASS" : "\nABOUT " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
