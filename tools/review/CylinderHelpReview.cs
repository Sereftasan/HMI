using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

// THE CYLINDER HELPERS FOR A REVIEW (2 October): every cylinder alarm of a
// machine - each unit's error bit - written as the step-by-step page the HMI
// opens for it, in one language, with a list (manifest.csv) for the index.
//
// Run in the program's folder (bin\Debug of Kaban.Machines.FA2010), one machine
// and one language at a time:
//     mono CylinderHelpReview.exe <out folder> FA2010|H500|H800 EN|TR
// tools/review/build_review.py compiles it, runs every machine and language and
// writes the index pages.
class CylinderHelpReview {
    static IEnumerable<Control> All(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var x in All(k)) yield return x; } }
    // file names in plain letters (a zip opened anywhere): s, i, g, u, o, c for the Turkish ones
    static string Safe(string s)
    {
        const string from = "\u015f\u015e\u0131\u0130\u011f\u011e\u00fc\u00dc\u00f6\u00d6\u00e7\u00c7", to = "sSiIgGuUoOcC";
        var plain = new string((s ?? "").Select(ch => { int i = from.IndexOf(ch); return i >= 0 ? to[i] : ch; }).ToArray());
        return new string(plain.Select(ch => ch < 128 && char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
    }

    static readonly Dictionary<string, string[]> Pages = new Dictionary<string, string[]> {
        { "FA2010", new[] { "Screens.Manual.ManualClamping_Moulds", "Screens.Manual.ManualCutting", "Screens.Manual.ManualEntry_Conveyor", "Screens.Manual.ManualExit",
                            "Screens.Manual.ManualMilling", "Screens.Manual.ManualOther", "Screens.Manual.ManualProfile_Recognition", "Screens.Manual.ManualRobot",
                            "Screens.Manual.ManualYZ_Cross_Rollers" } },
        { "H500", new[] { "Screens.ManualOther", "Screens.ManualProfile_Rotation" } },
        { "H800", new[] { "Screens.ManualDischarge", "Screens.ManualConveyor", "Screens.ManualProfile_Rotation" } },
    };

    [STAThread]
    static int Main(string[] args) {
        string outDir = args[0], machine = args[1], lang = args[2].ToUpperInvariant();
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string own = machine == "FA2010" ? baseDir : Path.Combine(baseDir, machine);
        LanguageManager.LoadCommonTranslations(Path.Combine(baseDir, "CommonTranslations.csv"));
        LanguageManager.LoadMachineTranslations(Path.Combine(own, "Translations.csv"));
        WiringTable.Clear();
        WiringTable.Load(Path.Combine(own, "Wiring.csv"), plc => plc);
        AlarmDefinitionLoader.Load(Path.Combine(own, "Alarms.json"), true, "");
        LanguageManager.ChangeLanguage(lang);
        // as an operator reads them: the PLC's internal devices not named
        TroubleshootingCatalog.AdminView = () => false;

        var asm = Assembly.LoadFrom(Path.Combine(baseDir, "Kaban.Machines." + machine + ".exe"));
        var dir = Path.Combine(outDir, lang, machine);
        Directory.CreateDirectory(dir);
        var manifest = new StringBuilder();
        int n = 0;
        foreach (var p in Pages[machine]) {
            var t = asm.GetType("Kaban.Machines." + machine + "." + p);
            if (t == null) { Console.WriteLine("no page " + p); continue; }
            var page = (Control)Activator.CreateInstance(t);
            foreach (var u in All(page).OfType<CylinderUnitHMI>().Where(x => x.Kind == UnitKind.Cylinder)) {
                foreach (var side in new[] { "A", "B" }) {
                    var error = side == "A" ? u.ErrorAAddress : u.ErrorBAddress;
                    if (string.IsNullOrWhiteSpace(error)) continue;
                    var mv = u.MovementFor(side);
                    if (mv == null) continue;
                    var valve = (TC.ValveType)TC.CanonicalValve((int)u.ValveType);
                    var feedback = (TC.FeedbackType)TC.CanonicalFeedback((int)u.FeedbackType);
                    var html = TC.BuildSteps(mv, valve, feedback, 0, lang);
                    var move = TC.MovementName(mv);
                    var file = Safe(error) + "_" + Safe(mv.Unit) + "_" + Safe(move) + ".html";
                    File.WriteAllText(Path.Combine(dir, file), html, new UTF8Encoding(true));
                    var def = AlarmManager.FindByStationAndAddress(mv.Station, error);
                    var text = def == null ? "" : LanguageManager.GetString(def.TranslationKey, def.TranslationKey);
                    manifest.AppendLine(string.Join("\t", machine, error, text, mv.Unit, move, TC.TemplateName(valve, feedback),
                                                    (int)u.ValveType + "/" + (int)u.FeedbackType, p.Substring(p.LastIndexOf('.') + 1), file));
                    n++;
                }
            }
        }
        File.WriteAllText(Path.Combine(dir, "manifest.tsv"), manifest.ToString(), new UTF8Encoding(false));
        Console.WriteLine(machine + " " + lang + ": " + n + " pages");
        return 0;
    }
}
