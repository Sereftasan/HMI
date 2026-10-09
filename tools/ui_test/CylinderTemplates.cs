using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

// THE CYLINDER HELPERS AS TEMPLATES (2 October): one template per valve and
// feedback pair, per language (Help/Templates/EN, TR), filled with the unit's
// devices and the drawings' numbers (Wiring.csv) when the help is opened.
// (That each pair's file is the master decided for its pair is checked by
// tools/build_cylinder_templates.py --check; that the English pages are the old
// builder's, page for page, was checked when the templates were made.)
class CT {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    static IEnumerable<Control> All(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var x in All(k)) yield return x; } }
    static string Text(string html)
    {
        var t = Regex.Replace(html, "<style.*?</style>", "", RegexOptions.Singleline);
        t = Regex.Replace(t, "</(li|h[1-3]|p|td|div|tr)>", " ");
        return Regex.Replace(System.Net.WebUtility.HtmlDecode(Regex.Replace(t, "<[^>]+>", "")), "\\s+", " ");
    }
    static string Norm(string html) { return Regex.Replace(Regex.Replace(html, ">\\s+<", "><"), "\\s+", " ").Trim(); }
    static string StepTr(string html, string title) {
        foreach (Match m in Regex.Matches(html, "<div class=\"step.*?</table>", RegexOptions.Singleline))
            if (Text(m.Value).Contains(title)) return Text(m.Value);
        return "";
    }
    static List<string> Steps(string html) { return Regex.Matches(html, "<div class=\"step[^\"]*\" id=\"([^\"]+)\">").Cast<Match>().Select(m => m.Groups[1].Value).ToList(); }

    [STAThread]
    static void Main() {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        WiringTable.Clear();
        WiringTable.Load(Path.Combine(baseDir, "Wiring.csv"), plc => plc);

        Console.WriteLine("--- the templates beside the program ---");
        var valves = new[] { TC.ValveType.Unspecified, TC.ValveType.DoubleSolenoid, TC.ValveType.SingleSolenoidSpring, TC.ValveType.ClosedCentre };
        var feedbacks = new[] { TC.FeedbackType.Unspecified, TC.FeedbackType.SingleMagnetic, TC.FeedbackType.TwoMagnetic, TC.FeedbackType.SingleInductive,
                                TC.FeedbackType.AnalogueMagnetic, TC.FeedbackType.PressureSwitch, TC.FeedbackType.EncoderReference, TC.NoSensor };
        var names = valves.SelectMany(v => feedbacks.Select(f => TC.TemplateName(v, f))).ToList();
        Check("one per valve and feedback pair: 4 x 8", names.Distinct().Count() == 32, names.Count + "");
        Check("  named after them", TC.TemplateName(TC.ValveType.ClosedCentre, TC.FeedbackType.PressureSwitch) == "Cylinder_5-3_PressureSwitch.html" &&
              TC.TemplateName(TC.ValveType.OpenCentre, TC.FeedbackType.None) == "Cylinder_5-3_Feedback-NotSet.html" &&
              TC.TemplateName((TC.ValveType)2, TC.NoSensor) == "Cylinder_5-2-Spring_NoSensor.html");
        foreach (var lang in new[] { "EN", "TR" }) {
            var missing = names.Where(n => !File.Exists(Path.Combine(TC.TemplateFolder, lang, n))).ToList();
            Check("  every one in " + lang, missing.Count == 0, string.Join(", ", missing));
        }
        var required = new[] { "SensorName", "And", "DrawnAs", "Wire", "Sheet", "SheetParen", "ReadThrough", "SensorTag", "Relay", "CoilWord", "ContinuesOn",
                               "ValveWord", "HoseWord", "HosesWord", "PnSheetWord", "On", "Off", "Met", "NotMet", "StepLink", "StepsRange", "StartHere",
                               "ThisUnit", "LeftMove", "RightMove", "GlyphWords", "MoveWords", "DataWords" };
        var lacking = new List<string>();
        foreach (var lang in new[] { "EN", "TR" })
            foreach (var n in names) {
                var p = Path.Combine(TC.TemplateFolder, lang, n);
                if (!File.Exists(p)) continue;
                var t = TC.LoadTemplate(p);
                foreach (var rq in required) if (!t.HasPhrase(rq)) lacking.Add(lang + "/" + n + ":" + rq);
            }
        Check("  each has the phrases the program builds its pieces from", lacking.Count == 0, string.Join(", ", lacking.Take(5)));
        // 2 October: the drawings and the page's look, shared by the languages (Drawings.html), and each language's words in them
        var drawn = new[] { "Style", "DrawIoPage", "DrawCard", "DrawTerminal", "DrawRelay", "DrawCoil", "DrawValve", "DrawAir", "DrawCylinder", "DrawSensor",
                            "W_IoPage", "W_Press", "W_ValveShort", "W_FlowControl", "W_AirSwitch" };
        var noDrawing = new List<string>();
        foreach (var lang in new[] { "EN", "TR" })
            foreach (var n in names) {
                var p = Path.Combine(TC.TemplateFolder, lang, n);
                if (!File.Exists(p)) continue;
                var t = TC.LoadTemplate(p);
                foreach (var rq in drawn) if (!t.HasPhrase(rq)) noDrawing.Add(lang + "/" + n + ":" + rq);
            }
        Check("  each carries the drawings, the page's look and its language's words in the drawings", noDrawing.Count == 0, string.Join(", ", noDrawing.Take(5)));

        Console.WriteLine("--- the FA2010 units, filled in ---");
        var fa = Assembly.LoadFrom("Kaban.Machines.FA2010.exe");
        var units = new List<KeyValuePair<string, CylinderUnitHMI>>();
        // a cylinder with an encoder takes itself from its row on Encoders (9 October): found first
        Kaban.UI.Helpers.AlarmHelpLinks.EnsureDiscovered();
        Kaban.UI.Helpers.AlarmHelpLinks.Harvest((Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens.Parameters.ParamEncoders")));
        foreach (var p in new[] { "ManualProfile_Recognition", "ManualCutting", "ManualExit", "ManualClamping_Moulds", "ManualMilling" }) {
            var page = (Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens.Manual." + p));
            foreach (var c in All(page))
            {
                var e = c as CylinderEncoderUnitHMI;
                if (e != null) { units.Add(new KeyValuePair<string, CylinderUnitHMI>(e.Name, e.Unit)); continue; }
                var u = c as CylinderUnitHMI;
                if (u != null && u.Kind == UnitKind.Cylinder && !(u.Parent is CylinderEncoderUnitHMI)) units.Add(new KeyValuePair<string, CylinderUnitHMI>(u.Name, u));
            }
        }
        Func<string, CylinderUnitHMI> unit = n => units.First(u => u.Key == n).Value;
        var fc = unit("unitFront_Clamping");
        var mv = fc.MovementFor("B");
        Check("the front clamping's clamp: 5/3 with a pressure switch", (int)fc.ValveType == 3 && (int)fc.FeedbackType == 5 && mv.Error == "M844");
        var en = TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.PressureSwitch, 0, "EN");
        var tr = TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.PressureSwitch, 0, "TR");
        var tEn = Text(en); var tTr = Text(tr);
        Check("English: the page as before - the chain, the relay, the coil, the pressure switch", tEn.Contains("Front Clamping") && tEn.Contains("did not complete") &&
              tEn.Contains("relay K29") && tEn.Contains("coil Y.23") && tEn.Contains("Does pressure switch SN83 (X70) switch?") && !en.Contains("{{"), "");
        Check("Turkish: the same page in Turkish", tTr.Contains("hareketi tamamlanmad\u0131") && tTr.Contains("K29 r\u00f6lesi") && tTr.Contains("Y.23 bobini") &&
              tTr.Contains("SN83 (X70) bas\u0131n\u00e7 anahtar\u0131 A\u00c7IK oluyor mu?") && tTr.Contains("Ad\u0131m 1") && !tr.Contains("{{"), "");
        Check("  the same steps, in the same order", Steps(en).SequenceEqual(Steps(tr)), string.Join(",", Steps(en)) + " / " + string.Join(",", Steps(tr)));
        Check("  the movement's word translated (the button shows only an arrow)", tTr.Contains("\u201cS\u0131kma\u201d") && Regex.IsMatch(tEn, "\u201cClamp\u201d"));
        Check("  the drawings' texts in Turkish where Wiring.csv has them (Pressure.TR)", tTr.Contains("s\u0131kma bas\u0131nc\u0131 6 / 2 bar") && tEn.Contains("clamping pressure 6 / 2 bar"));
        Check("  the drawings' own labels kept, marked as the drawing's", tTr.Contains("\u00e7izimde: Profile Recognition Front Clamping Empty Switch"));
        Check("  the valve type in Turkish (DataWords)", tTr.Contains("5/3 orta konumu kapal\u0131") && tEn.Contains("5/3 closed centre"));
        Func<string, string> nav = h => Text(Regex.Match(h, "<nav class=\"path\">.*?</nav>", RegexOptions.Singleline).Value);
        Check("the signal path on the left in both, without the HMI's command", nav(en).Contains("PLC output ON") && !nav(en).Contains("HMI command") &&
              nav(tr).Contains("PLC \u00e7\u0131k\u0131\u015f\u0131 A\u00c7IK") && !nav(tr).Contains("HMI komutu") && !tEn.Contains("M645") && !tTr.Contains("M645"), nav(tr));
        Check("  the date line in Turkish", Regex.IsMatch(tr, "<p class=\"dates\">Sayfa tarihi: \\d{4}-\\d\\d-\\d\\d \\d\\d:\\d\\d</p>"),
              Regex.Match(tr, "<p class=\"dates\">.*?</p>").Value);
        Check("  the card's LED in Turkish: 'U4 kart\u0131nda OUT s\u0131ras\u0131ndaki 32 numaral\u0131 LED'", tTr.Contains("U4 kart\u0131nda OUT s\u0131ras\u0131ndaki 32 numaral\u0131 LED"),
              StepTr(tr, "\u00c7\u0131k\u0131\u015f kart\u0131"));
        Check("  the same drawings in both, their few words in the page's language", Regex.Matches(en, "<svg class=\"pic\"").Count == Regex.Matches(tr, "<svg class=\"pic\"").Count &&
              Regex.Matches(en, "<svg class=\"pic\"").Count >= 8 && tr.Contains(">bas\u0131n</text>") && en.Contains(">press</text>") &&
              tr.Contains("Giri\u015fler / \u00c7\u0131k\u0131\u015flar</text>"), Regex.Matches(en, "<svg class=\"pic\"").Count + " / " + Regex.Matches(tr, "<svg class=\"pic\"").Count);

        LanguageManager.ChangeLanguage("DE");
        var de = TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.PressureSwitch);
        LanguageManager.ChangeLanguage("TR");
        var trNow = TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.PressureSwitch);
        Check("a language without templates: English", Norm(de) == Norm(en));
        Check("  the HMI's language: its own", Norm(trNow) == Norm(tr));
        Check("  the movement's name in the help menus too", TC.MovementName(mv) == "S\u0131kma", TC.MovementName(mv));
        LanguageManager.ChangeLanguage("EN");
        Check("  and in English", TC.MovementName(mv) == "Clamp");

        Console.WriteLine("--- an alarm the HMI raised itself: only its steps ---");
        var f1 = TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.PressureSwitch, 2, "TR");
        Check("Turkish, the target did not come on: where it is, the way, that switch, the time", Steps(f1).SequenceEqual(new[] { "look", "B1", "B2", "B3", "B4", "C1", "C3", "end" }) &&
              Text(f1).Contains("s\u00fcre a\u015f\u0131m\u0131 alarm\u0131 verdi"), string.Join(",", Steps(f1)));
        Check("  its answers point at the steps kept", !Regex.IsMatch(f1, "href=\"#(A[0-9]|C2)\"") && !f1.Contains("data-optional") && !f1.Contains("href=\"#@"));

        Console.WriteLine("--- an edited template is used at once ---");
        var tmp = Path.Combine(Path.GetTempPath(), "cyltpl_" + Guid.NewGuid().ToString("N"));
        foreach (var lang in new[] { "EN", "TR" }) {
            Directory.CreateDirectory(Path.Combine(tmp, "Templates", lang));
            foreach (var n in names) File.Copy(Path.Combine(TC.TemplateFolder, lang, n), Path.Combine(tmp, "Templates", lang, n));
        }
        var keep = TC.HelpFolder;
        TC.HelpFolder = tmp;
        var file = Path.Combine(tmp, "Templates", "TR", "Cylinder_5-3_PressureSwitch.html");
        File.WriteAllText(file, File.ReadAllText(file).Replace("\u00d6nce g\u00fcvenlik.", "\u00d6NCE G\u00dcVENL\u0130K!"));
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(5));
        Check("a word changed in the TR file shows on the next page", Text(TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.PressureSwitch, 0, "TR")).Contains("\u00d6NCE G\u00dcVENL\u0130K!"));
        File.Delete(file);
        Check("  a pair's file missing in Turkish: the English one", Text(TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.PressureSwitch, 0, "TR")).Contains("Safety first."));
        File.Delete(Path.Combine(tmp, "Templates", "EN", "Cylinder_5-3_PressureSwitch.html"));
        Check("  none at all: a page that says so", TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.PressureSwitch, 0, "TR").Contains("not installed"));
        TC.HelpFolder = keep;
        try { Directory.Delete(tmp, true); } catch { }

        Console.WriteLine("--- the template syntax ---");
        var s = new StepTemplate("<!--doc a note -->\n<!--def W-->[{{V}}]<!--/def-->\n{{#L}}\n<i>{{N}}</i>\n{{/L}}\n{{^E}}none{{/E}}|{{[, }}{{#A}}a{{/A}}{{|}}{{#B}}b{{/B}}{{|}}c{{]}}|{{>W}}");
        var r = s.Render(new Dictionary<string, object> {
            { "L", new List<IDictionary<string, object>> { new Dictionary<string, object> { { "N", "1" } }, new Dictionary<string, object> { { "N", "2" } } } },
            { "E", "" }, { "A", true }, { "B", false }, { "V", "v" } });
        Check("lists, not, joins, phrases; a line of tags leaves no line", r == "<i>1</i>\n<i>2</i>\nnone|a, c|[v]", r.Replace("\n", "\\n"));
        Check("  a name not given is noted (for the tests)", s.Missing.Count == 0 && new Func<bool>(() => { s.Render(new Dictionary<string, object>()); return s.Missing.Contains("L"); })());
        bool threw = false;
        try { new StepTemplate("{{#A}}x{{/B}}"); } catch (FormatException) { threw = true; }
        Check("  a section closed by the wrong name: refused, with where", threw);

        Console.WriteLine("--- the drawings in Turkish ---");
        var y131 = WiringTable.Find(1, "Y131");
        Check("Wiring.csv: Measure.TR beside Measure", y131 != null && y131.Localized("Measure", "TR").StartsWith("analog sens\u00f6r SN110") && y131.Localized("Measure", "DE") == "");

        Console.WriteLine(fail == 0 ? "\nCYLINDER TEMPLATES ALL PASS" : "\nCYLINDER TEMPLATES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
