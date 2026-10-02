using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

// The step-by-step page's drawings as the machine is (2 October, evening):
// "There is no LED on our valve coil sockets. The Y.14 coil is on the 96
// (unclamping) side. A clamp clamps when the rod goes out and releases when it
// comes in: the Release arrow and the sensors the other way round. Our clamping
// hoses are black, only the unclamping sides yellow. The digital I/O monitor is
// on the right of the screen. A meter needs two leads. The left side's step
// numbers should follow the right side. More about the sensors: another magnet,
// the cable, the 24 V."
class CD {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static string Text(string html) { return System.Net.WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", "")); }
    // The step whose id is given (A6, C1 ...), as HTML.
    static string Step(string html, string id) {
        var m = Regex.Match(html, "<div class=\"step[^\"]*\" id=\"" + id + "\">.*?</div></div>", RegexOptions.Singleline);
        return m.Success ? m.Value : "";
    }
    static string Svg(string html, string step, int nth = 0) {
        var all = Regex.Matches(Step(html, step), "<svg class=\"pic\".*?</svg>", RegexOptions.Singleline);
        return nth < all.Count ? all[nth].Value : "";
    }

    [STAThread]
    static void Main() {
        TC.AdminView = () => false;
        TC.Clock = () => new DateTime(2026, 10, 2, 18, 0, 0);
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        WiringTable.Clear();
        WiringTable.Load(Path.Combine(dir, "Wiring.csv"), plc => plc);

        Console.WriteLine("--- the hose each output fills (Wiring.csv) ---");
        Check("clamp 6: Y12 (Clamp) fills 16, Y21 (Release) 96", WiringTable.Find(1, "Y12").Hose == "16" && WiringTable.Find(1, "Y21").Hose == "96",
              WiringTable.Find(1, "Y12").Hose + " / " + WiringTable.Find(1, "Y21").Hose);
        Check("  a single solenoid's coil the A hose (Y116: 72)", WiringTable.Find(1, "Y116").Hose == "72");

        var fa = Assembly.LoadFrom("Kaban.Machines.FA2010.exe");
        var entry = (Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens.Manual.ManualEntry_Conveyor"));
        var clamp6 = entry.Controls.OfType<CylinderUnitHMI>().First(u => u.Name == "unitProfile_Clamping_6");
        Func<CylinderUnitHMI, string, string, string> page = (u, side, lang) =>
            TC.BuildSteps(u.MovementFor(side), (TC.ValveType)TC.CanonicalValve((int)u.ValveType), (TC.FeedbackType)TC.CanonicalFeedback((int)u.FeedbackType), 0, lang);
        var release = page(clamp6, "A", "EN");
        var clampP = page(clamp6, "B", "EN");
        Check("clamp 6: the left button releases (Y21), the right clamps (Y12)", clamp6.MovementFor("A").Output.Address == "Y21" &&
              clamp6.MovementFor("B").Output.Address == "Y12");

        Console.WriteLine("--- Release: the B side, the rod comes in ---");
        var valve = Svg(release, "A6");
        Check("the valve: Y.7 on the left (A) end, Y.14 on the right (B) end - the 96 side",
              valve.Contains("<text x=\"51\" y=\"148\" text-anchor=\"middle\" font-size=\"9\" font-weight=\"bold\" fill=\"#111\">Y.7</text>") &&
              valve.Contains("<text x=\"299\" y=\"148\" text-anchor=\"middle\" font-size=\"9\" font-weight=\"bold\" fill=\"#111\">Y.14</text>"));
        Check("  its end framed, its override pressed there", valve.Contains("<rect x=\"264\" y=\"88\"") && valve.Contains("<path d=\"M299 62 v30\"") &&
              !valve.Contains("<rect x=\"16\" y=\"88\""));
        Check("  hose 16 black, 96 yellow; 96 marked as the one filled",
              valve.Contains("<path d=\"M118 0 C 118 40, 132 60, 132 92\" fill=\"none\" stroke=\"#2b2f33\"") &&
              valve.Contains("<path d=\"M228 0 C 228 40, 214 60, 214 92\" fill=\"none\" stroke=\"#f5c400\"") &&
              Regex.IsMatch(valve, "<rect x=\"210\" y=\"22\"[^>]*stroke=\"#1971c2\"[^>]*/><text[^>]*>96</text>"));
        Check("no lamp on a coil's connector anywhere on the page (no yellow LED, no 'connector's LED')",
              !release.Contains("#ffd43b") && !Text(Step(release, "A5")).Contains("LED"), Text(Step(release, "A5")).Substring(0, 120));
        Check("  the coil's step: its connector pulled, its two pins measured, the coil itself on ohms",
              Text(Step(release, "A5")).Contains("pull its connector off the coil") && Text(Step(release, "A5")).Contains("Across its two pins") &&
              Text(Step(release, "A5")).Contains("24 V there, the coil reads open"));
        var cyl = Svg(release, "look");
        Check("the cylinder: the arrow to the left (the rod comes in)", cyl.Contains("<path d=\"M230 196 h-90\"") && !cyl.Contains("<path d=\"M130 196 h90\""));
        Check("  SN46 (back sensor), the one it reaches, lit on the left; SN53 on the right",
              cyl.Contains("<text x=\"125\" y=\"162\" text-anchor=\"middle\" font-size=\"11\" font-weight=\"bold\" fill=\"#111\">SN46</text>") &&
              cyl.Contains("<text x=\"217\" y=\"162\" text-anchor=\"middle\" font-size=\"11\" font-weight=\"bold\" fill=\"#495057\">SN53</text>") &&
              WiringTable.Find(1, "X34").Text.Contains("Back Sensor"));
        Check("  hose 16 black on the cap end, 96 yellow on the rod end",
              cyl.Contains("<path d=\"M96 0 C 96 30, 92 50, 92 78\" fill=\"none\" stroke=\"#2b2f33\"") &&
              cyl.Contains("<path d=\"M246 0 C 246 30, 250 50, 250 78\" fill=\"none\" stroke=\"#f5c400\""));
        Func<string, int> blackLeads = s => Regex.Matches(s, "<path d=\"[^\"]*\" fill=\"none\" stroke=\"#212529\" stroke-width=\"2.5\"/>").Count;
        Func<string, int> redLeads = s => Regex.Matches(s, "<path d=\"[^\"]*\" fill=\"none\" stroke=\"#c92a2a\" stroke-width=\"2.5\"/>").Count;
        Check("every meter has its two leads: relay, coil, terminal", new[] { Svg(release, "A4"), Svg(release, "A5"), Svg(release, "A3", 1) }
              .All(s => s.Contains("{{") == false && blackLeads(s) + (s.Contains("stroke=\"#212529\" stroke-width=\"3\"") ? 1 : 0) >= 1 &&
                        redLeads(s) + (s.Contains("stroke=\"#c92a2a\" stroke-width=\"3\"") ? 1 : 0) >= 1),
              string.Join(",", new[] { Svg(release, "A4"), Svg(release, "A5"), Svg(release, "A3", 1) }.Select(s => redLeads(s) + "/" + blackLeads(s))));
        var io = Svg(release, "A1");
        Check("the Inputs / Outputs page beside the machine's page, its button on the bar at the right edge",
              io.Contains(">Inputs / Outputs</text>") && io.Contains("<rect x=\"316\" y=\"26\"") && io.Contains("<rect x=\"318\" y=\"29\""));
        Check("  Y21's row with its lamp lit (an output: amber)", io.Contains("#ffaa00") && io.Contains(">Y21</text>"));
        Check("  also beside 'Does Y21 stay ON the whole way?'", Svg(release, "B1").Contains(">Inputs / Outputs</text>"));

        Console.WriteLine("--- Clamp: the A side, the rod goes out ---");
        var cv = Svg(clampP, "A6");
        Check("the valve: Y.7's end framed, its override pressed", cv.Contains("<rect x=\"16\" y=\"88\"") && cv.Contains("<path d=\"M51 62 v30\"") &&
              !cv.Contains("<rect x=\"264\" y=\"88\""));
        Check("  16 marked as the one filled", Regex.IsMatch(cv, "<rect x=\"98\" y=\"22\"[^>]*stroke=\"#1971c2\"[^>]*/><text[^>]*>16</text>"));
        var cc = Svg(clampP, "look");
        Check("the cylinder: the arrow to the right, SN53 lit on the right, SN46 on the left",
              cc.Contains("<path d=\"M130 196 h90\"") &&
              cc.Contains("<text x=\"217\" y=\"162\" text-anchor=\"middle\" font-size=\"11\" font-weight=\"bold\" fill=\"#111\">SN53</text>") &&
              cc.Contains("<text x=\"125\" y=\"162\" text-anchor=\"middle\" font-size=\"11\" font-weight=\"bold\" fill=\"#495057\">SN46</text>"));

        Console.WriteLine("--- a spring valve ---");
        var cutting = (Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens.Manual.ManualCutting"));
        var roller = cutting.Controls.OfType<CylinderUnitHMI>().First(u => u.Name == "unitProfile_Press_Roller_Up");
        string coilSide = roller.MovementFor("B").Output != null ? "B" : "A";
        var up = page(roller, coilSide, "EN");
        var uv = Svg(up, "A6");
        Check("the coil's movement (Y116): the coil on the A end, framed and pressed; the spring on the B end",
              uv.Contains("<rect x=\"16\" y=\"88\"") && uv.Contains("<path d=\"M51 62 v30\"") && uv.Contains("<path d=\"M276 129 l6 -10"));
        var springSide = coilSide == "B" ? "A" : "B";
        var mvSpring = roller.MovementFor(springSide);
        if (mvSpring != null) {
            var down = page(roller, springSide, "EN");
            var dv = Svg(down, "A6");
            Check("  the spring's movement: the spring framed, nothing pressed", dv.Contains("<rect x=\"264\" y=\"98\"") && !dv.Contains("v30\" stroke=\"#c92a2a\""));
            Check("  its cylinder: the arrow to the left", Svg(down, "look").Contains("<path d=\"M230 196 h-90\""));
        }

        Console.WriteLine("--- the sensors: a magnet, the plug, the cable ---");
        var c1 = Step(release, "C1");
        Check("the sensor's step draws the sensor, a magnet held on it, its plug measured",
              Regex.Matches(c1, "<svg class=\"pic\"").Count == 3 && c1.Contains(">magnet</text>") && c1.Contains(">+24 V</text>") && c1.Contains(">signal</text>"));
        Check("  and asks it: the magnet, the 24 V at the plug, the plug and the cable",
              Text(c1).Contains("Hold a magnet on the sensor") && Text(c1).Contains("+24 V (brown) against 0 V (blue) = 24 V") &&
              Text(c1).Contains("move the cable gently"));
        Check("  each answer: wrong place, no supply, broken sensor, no signal, a loose cable",
              new[] { "It lights with the magnet, not with the cylinder.", "No 24 V at its plug.", "24 V there, the LED stays off even with the magnet.",
                      "LED on, X34 stays OFF on the HMI.", "The input comes and goes when the cable moves." }.All(Text(c1).Contains));
        var c2 = Step(release, "C2");
        Check("the sensor that must go off: its plug pulled tells the sensor from the cable",
              Text(c2).Contains("Pull its plug: the input goes OFF") && Text(c2).Contains("The input stays ON with the plug pulled."));

        Console.WriteLine("--- the left side follows the right ---");
        Check("the page carries its script: the box of the step on show marked, blinking",
              release.Contains("nav.path a.box") && release.Contains("classList.add(\"now\")") && release.Contains(".box.now .sn{background:#e8590c;animation:snblink"));
        Check("  the path's boxes carry their step numbers (what the script compares)",
              Regex.Matches(release, "<a class=\"box\" href=\"#[A-Z0-9]+\"[^>]*><span class=\"sn\">\\d+</span>").Count >= 6);

        Console.WriteLine("--- in Turkish ---");
        var tr = page(clamp6, "A", "TR");
        Check("the same drawings, the words in Turkish", Step(tr, "C1").Contains(">m\u0131knat\u0131s</text>") && Step(tr, "C1").Contains(">sinyal</text>") &&
              Svg(tr, "A6").Contains("<rect x=\"264\" y=\"88\""));
        Check("  the coil's and the sensor's checks", Text(Step(tr, "A5")).Contains("Konnekt\u00f6rde 24 V yok.") && Text(Step(tr, "C1")).Contains("Soketinde 24 V yok.") &&
              !Text(Step(tr, "A5")).Contains("LED"));
        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine("--- a machine without its wiring ---");
        WiringTable.Clear();
        Func<string, TC.MovementInfo> bare = side => new TC.MovementInfo { Station = 7, Unit = "Gate", Side = side, Name = "Open", OtherName = "Close",
            Output = new TC.MovementInfo.Device { Address = "Y3" }, Error = "M900" };
        var left = TC.BuildSteps(bare("A"), TC.ValveType.DoubleSolenoid, TC.FeedbackType.TwoMagnetic);
        var right = TC.BuildSteps(bare("B"), TC.ValveType.DoubleSolenoid, TC.FeedbackType.TwoMagnetic);
        Check("the two buttons' movements drawn the two ways (left button: in, as the machine's units mostly are)",
              Svg(left, "look").Contains("M230 196 h-90") && Svg(right, "look").Contains("M130 196 h90"));
        Check("  no hose numbers made up", !Svg(left, "A6").Contains("<text x=\"117\" y=\"39\""));
        var ind = bare("B");
        ind.Feedback.Add(new TC.MovementInfo.Device { Address = "X5", AtAlarm = 0 });
        var ip = TC.BuildSteps(ind, TC.ValveType.DoubleSolenoid, TC.FeedbackType.SingleInductive);
        Check("an inductive sensor is tried with a steel tool, not a magnet", Text(Step(ip, "C1")).Contains("Hold a steel tool a few millimetres in front of its face") &&
              Step(ip, "C1").Contains(">steel tool</text>") && !Step(ip, "C1").Contains(">magnet</text>"));

        Console.WriteLine(fail == 0 ? "\nCYLINDER DRAWINGS ALL PASS" : "\nCYLINDER DRAWINGS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
