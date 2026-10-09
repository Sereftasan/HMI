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
        // 9 October: About is the caption bar's "?" now ("not a setting thing at all")
        Check("About is no tab of HMI Settings any more", !keys.Contains("Set_About"), string.Join(",", keys));

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

        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine("--- the manual describes ONLY the machines this program runs ---");
        string template = File.ReadAllText(path);
        var ids = new[] { "FA2010", "H500", "H800", "MD3020", "SB3012", "SB3015" };
        var named = ids.Where(i => template.Contains(i)).ToList();
        Check("the manual file itself names no machine", named.Count == 0, string.Join(",", named));
        Check("  and says nothing about Profiles (an FA2010 page)", !template.Contains("Profiles"));

        string chapterFile = TroubleshootingCatalog.ResolveLanguageFile("Manual_FA2010.html");
        Check("FA2010's own chapter is in the Help folder", File.Exists(chapterFile), chapterFile);
        var body = AboutControl.ChapterBody(chapterFile);
        Check("  its body is the chapter, not its notes", body.TrimStart().StartsWith("<h4") && !body.Contains("-->"), body.Substring(0, Math.Min(40, body.Length)));

        var fa = new System.Collections.Generic.List<AboutControl.ManualMachine> {
            new AboutControl.ManualMachine { Id = "FA2010", Name = "Processing Center", Station = 1, IsMain = true, Chapter = body,
                                             Pages = { "Home", "Manual Movement", "Profiles" } },
            new AboutControl.ManualMachine { Id = "H500", Name = "Screwing_Holing", Station = 3, Pages = { "Home" } },
            new AboutControl.ManualMachine { Id = "H800", Name = "3D_HingeDrilling", Station = 4, Pages = { "Home" } } };
        string forFa = AboutControl.ComposeManual(template, fa);
        Check("FA2010's manual names FA2010 and its sub-machines",
              forFa.Contains("FA2010") && forFa.Contains("H500") && forFa.Contains("H800"));
        Check("  with FA2010's own chapter (Profiles)", forFa.Contains("id=\"profiles\""));
        var others = new[] { "MD3020", "SB3012", "SB3015" }.Where(i => forFa.Contains(i)).ToList();
        Check("  and no other machine", others.Count == 0, string.Join(",", others));
        Check("  the drawing's caption bar is FA2010's", forFa.Contains("KABAN &nbsp; FA2010"));
        Check("  the markers are kept (a later fill still finds them)", forFa.Contains("<!--machine-chapter-->") && forFa.Contains("<!--/machines-->"));
        Check("  everything filled in is in the BODY (the head's note about the markers untouched)",
              forFa.IndexOf("For <b>FA2010") > forFa.IndexOf("<body") && forFa.Substring(0, forFa.IndexOf("<body")) == template.Substring(0, template.IndexOf("<body")));

        var md = new System.Collections.Generic.List<AboutControl.ManualMachine> {
            new AboutControl.ManualMachine { Id = "MD3020", Name = "MD3020", Station = 1, IsMain = true, Pages = { "Home" } } };
        string forMd = AboutControl.ComposeManual(template, md);
        var foreign = new[] { "FA2010", "H500", "H800", "SB3012", "SB3015", "Profiles", "Processing" }.Where(i => forMd.Contains(i)).ToList();
        Check("MD3020's manual names MD3020", forMd.Contains("For <b>MD3020</b>"));
        Check("  and nothing of FA2010 or the others", foreign.Count == 0, string.Join(",", foreign));
        Check("  and no sub-machine sentence", !forMd.Contains("sub-machine "));

        Check("with no machine mounted (a test), the plain manual is opened", AboutControl.PrepareManual() == path, AboutControl.PrepareManual());

        Console.WriteLine(fail == 0 ? "\nABOUT ALL PASS" : "\nABOUT " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
