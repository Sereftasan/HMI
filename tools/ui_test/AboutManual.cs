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

        // 9 October: "just a clean message box is enough" - the text alone
        var about = HmiManual.AboutText();
        Check("About's text: the program, its version, the HMI library", about.StartsWith("Program: ") && about.Contains("HMI library: "), about);

        string path = HmiManual.ManualPath();
        Check("the manual resolves to the English file", path.EndsWith("Kaban_HMI_Manual.en.html"), path);
        Check("  which the build put in the Help folder", File.Exists(path), path);
        Check("  and is HTML", File.Exists(path) && File.ReadAllText(path).Contains("<html"));

        // 30 October: "Answer to the Question 2: Yes" - a Turkish manual, and FA2010's chapter in Turkish.
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("TR");
        string tr = HmiManual.ManualPath();
        Check("Turkish: the Turkish manual", File.Exists(tr) && tr.EndsWith("Kaban_HMI_Manual.tr.html"), tr);
        string trChapter = TroubleshootingCatalog.ResolveLanguageFile("Manual_FA2010.html");
        Check("  and FA2010's Turkish chapter", File.Exists(trChapter) && trChapter.EndsWith("Manual_FA2010.tr.html"), trChapter);
        LanguageManager.ChangeLanguage("DE");
        string de = HmiManual.ManualPath();
        Check("German, with no German file: falls back to English", File.Exists(de) && de.EndsWith(".en.html"), de);

        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine("--- the manual describes ONLY the machines this program runs ---");
        string template = File.ReadAllText(path);
        var ids = new[] { "FA2010", "H500", "H800", "MD3020", "SB3012", "SB3015" };
        var named = ids.Where(i => template.Contains(i)).ToList();
        Check("the manual file itself names no machine", named.Count == 0, string.Join(",", named));
        Check("  and says nothing about Profiles (an FA2010 page)", !template.Contains("Profiles"));

        string chapterFile = TroubleshootingCatalog.ResolveLanguageFile("Manual_FA2010.html");
        Check("FA2010's own chapter is in the Help folder", File.Exists(chapterFile), chapterFile);
        var body = HmiManual.ChapterBody(chapterFile);
        Check("  its body is the chapter, not its notes", body.TrimStart().StartsWith("<h4") && !body.Contains("-->"), body.Substring(0, Math.Min(40, body.Length)));

        var fa = new System.Collections.Generic.List<HmiManual.ManualMachine> {
            new HmiManual.ManualMachine { Id = "FA2010", Name = "Processing Center", Station = 1, IsMain = true, Chapter = body,
                                             Pages = { "Home", "Manual Movement", "Profiles" } },
            new HmiManual.ManualMachine { Id = "H500", Name = "Screwing_Holing", Station = 3, Pages = { "Home" } },
            new HmiManual.ManualMachine { Id = "H800", Name = "3D_HingeDrilling", Station = 4, Pages = { "Home" } } };
        string forFa = HmiManual.ComposeManual(template, fa);
        Check("FA2010's manual names FA2010 and its sub-machines",
              forFa.Contains("FA2010") && forFa.Contains("H500") && forFa.Contains("H800"));
        Check("  with FA2010's own chapter (Profiles)", forFa.Contains("id=\"profiles\""));
        var others = new[] { "MD3020", "SB3012", "SB3015" }.Where(i => forFa.Contains(i)).ToList();
        Check("  and no other machine", others.Count == 0, string.Join(",", others));
        Check("  the drawing's caption bar is FA2010's", forFa.Contains("KABAN &nbsp; FA2010"));
        Check("  the markers are kept (a later fill still finds them)", forFa.Contains("<!--machine-chapter-->") && forFa.Contains("<!--/machines-->"));
        Check("  everything filled in is in the BODY (the head's note about the markers untouched)",
              forFa.IndexOf("For <b>FA2010") > forFa.IndexOf("<body") && forFa.Substring(0, forFa.IndexOf("<body")) == template.Substring(0, template.IndexOf("<body")));

        var md = new System.Collections.Generic.List<HmiManual.ManualMachine> {
            new HmiManual.ManualMachine { Id = "MD3020", Name = "MD3020", Station = 1, IsMain = true, Pages = { "Home" } } };
        string forMd = HmiManual.ComposeManual(template, md);
        var foreign = new[] { "FA2010", "H500", "H800", "SB3012", "SB3015", "Profiles", "Processing" }.Where(i => forMd.Contains(i)).ToList();
        Check("MD3020's manual names MD3020", forMd.Contains("For <b>MD3020</b>"));
        Check("  and nothing of FA2010 or the others", foreign.Count == 0, string.Join(",", foreign));
        Check("  and no sub-machine sentence", !forMd.Contains("sub-machine "));

        Check("with no machine mounted (a test), the plain manual is opened", HmiManual.PrepareManual() == path, HmiManual.PrepareManual());

        // 14 October: "HMI manual left side CONTENTS navigation is broken, all the navigation is linked to the
        // document directory instead of the manual sections" - the copy's <base href> sent "#alarms" to the folder.
        string copy = TroubleshootingCatalog.AbsoluteLinks(forFa);
        var toc = System.Text.RegularExpressions.Regex.Match(copy, "<nav class=\"toc\">(.*?)</nav>", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
        var hrefs = System.Text.RegularExpressions.Regex.Matches(toc, "href=\"([^\"]*)\"").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).ToList();
        Check("the copy has no <base>, and every Contents link is still the page's own section (#...)", !copy.Contains("<base") && hrefs.Count >= 13 &&
              hrefs.All(h => h.StartsWith("#")), string.Join(" ", hrefs.Take(4)));
        var missingIds = hrefs.Where(h => !copy.Contains("id=\"" + h.Substring(1) + "\"")).ToList();
        Check("  each one has its section", missingIds.Count == 0, string.Join(",", missingIds));
        var sample = TroubleshootingCatalog.AbsoluteLinks("<img src=\"pic.png\"><a href=\"#alarms\">a</a><a href=\"https://x.y/z.pdf\">b</a><a href=\"Manual.pdf#page=3\">c</a>", "/opt/hmi/Help");
        Check("  a picture or file named by the manual still resolves from the copy; '#', http links untouched",
              sample.Contains("src=\"file:///opt/hmi/Help/pic.png\"") && sample.Contains("href=\"#alarms\"") && sample.Contains("href=\"https://x.y/z.pdf\"") &&
              sample.Contains("href=\"file:///opt/hmi/Help/Manual.pdf#page=3\""), sample);
        Check("the section on screen is bold in the Contents (its script and style)", template.Contains("nav.toc a.active{font-weight:700") &&
              template.Contains("addEventListener('scroll', mark"));

        Console.WriteLine("--- the Turkish manual: the same sections, its words filled in Turkish ---");
        string trTemplate = File.ReadAllText(tr);
        Func<string, System.Collections.Generic.List<string>> idsOf = s =>
            System.Text.RegularExpressions.Regex.Matches(s, "id=\"([^\"]+)\"").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).ToList();
        var enIds = idsOf(template);
        var trIds = idsOf(trTemplate);
        Check("the Turkish manual has every section of the English one, in its order (a section added to one is added to the other)",
              enIds.SequenceEqual(trIds), string.Join(",", enIds.Except(trIds).Concat(trIds.Except(enIds).Select(x => "+" + x))));
        Check("  its language is Turkish, it keeps the markers and names no machine", HmiManual.LanguageOf(trTemplate) == "TR" &&
              trTemplate.Contains("<!--machine-chapter-->") && trTemplate.Contains("<!--svg-machines-->") && trTemplate.Contains("<!--machines-->") &&
              !ids.Any(i => trTemplate.Contains(i)));
        Check("  the English one is English", HmiManual.LanguageOf(template) == "EN");
        string trChapterBody = HmiManual.ChapterBody(trChapter);
        Check("  FA2010's Turkish chapter has the English one's sections", idsOf(trChapterBody).SequenceEqual(idsOf(body)),
              string.Join(",", idsOf(trChapterBody)));
        var faTr = new System.Collections.Generic.List<HmiManual.ManualMachine> {
            new HmiManual.ManualMachine { Id = "FA2010", Name = "Processing Center", Station = 1, IsMain = true, Chapter = trChapterBody, Pages = { "Ana Sayfa" } },
            new HmiManual.ManualMachine { Id = "H500", Name = "Screwing_Holing", Station = 3, Pages = { "Ana Sayfa" } },
            new HmiManual.ManualMachine { Id = "H800", Name = "3D_HingeDrilling", Station = 4, Pages = { "Ana Sayfa" } } };
        string forFaTr = HmiManual.ComposeManual(trTemplate, faTr);
        Func<HmiManual.ManualMachine, string> enc = m => System.Net.WebUtility.HtmlEncode(m.Label);
        Check("  composed: the line under the title in Turkish (the template's language, not the screen's)",
              forFaTr.Contains("<b>" + enc(faTr[0]) + "</b> ve ona bağlı <b>" + enc(faTr[1]) + "</b> ile <b>" + enc(faTr[2]) + "</b> alt makineleri için."),
              System.Text.RegularExpressions.Regex.Match(forFaTr, "<!--machines-->(.*?)<!--/machines-->").Groups[1].Value);
        Check("  chapter 13's table in Turkish", forFaTr.Contains("<th>Makine</th><th>Kontrolör</th><th>Sayfaları</th>") &&
              forFaTr.Contains("<i>(alt makine)</i>") && forFaTr.Contains("PLC istasyonu 3") && forFaTr.Contains("Bu program <b>FA2010"));
        Check("  the drawing's caption bar's user in Turkish", forFaTr.Contains("KABAN &nbsp; " + enc(faTr[0]) + " &nbsp;&middot;&nbsp; kullanıcı"));
        Check("  with FA2010's Turkish chapter", forFaTr.Contains("id=\"fa-home\"") && forFaTr.Contains("Kalıplı Sıkmalar"));
        LanguageManager.ChangeLanguage("TR");
        string enOnTr = HmiManual.ComposeManual(template, fa);
        LanguageManager.ChangeLanguage("EN");
        Check("  the English one composed in English as before - with the screen in Turkish too (a language with no manual of its own opens it)",
              forFa.Contains("<th>Machine</th><th>Controller</th><th>Its pages</th>") && forFa.Contains("with the sub-machines <b>H500") &&
              forFa.Contains("PLC station 3") && enOnTr == forFa);

        Console.WriteLine(fail == 0 ? "\nABOUT ALL PASS" : "\nABOUT " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
