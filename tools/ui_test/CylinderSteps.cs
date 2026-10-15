using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

// 30 September: FA2010's cylinder units typed and wired as the drawings show;
// help offered only for a fault. 1 October: the movement's STEP-BY-STEP
// troubleshooter (TroubleshootingCatalog.Steps.cs) first - where the cylinder
// is, then the signal from the PLC to the cylinder, the way, the sensors, each
// step with what to press, look at and measure on this machine and what each
// answer means; the two movements told apart where the valve or the feedback
// is not symmetric; what was seen at the alarm says where to start. The
// manual after it, as the general view, without notes in its checks.
class CS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static T Field<T>(object o, string n) { return (T)o.GetType().GetField(n, Any).GetValue(o); }

    static Dictionary<string, int> mem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    static int Read(string a) { int v; return mem.TryGetValue(a, out v) ? v : 0; }
    static void Scan(CylinderUnitHMI u) {
        Field<AlarmHelpBadge>(u, "_helpA").UpdateState(Read, c => true);
        Field<AlarmHelpBadge>(u, "_helpB").UpdateState(Read, c => true);
    }
    static string Chart(CylinderUnitHMI u, string side) {
        var d = u.HelpDocuments().FirstOrDefault(x => x.Movement != null && x.Movement.Side == side && !x.StepByStep);
        return d == null ? "" : File.ReadAllText(TC.Tailor(d));
    }
    static string Steps(CylinderUnitHMI u, string side) {
        var d = u.HelpDocuments().FirstOrDefault(x => x.StepByStep && x.Movement != null && x.Movement.Side == side);
        return d == null ? "" : File.ReadAllText(TC.ExtractSection(d));
    }
    // A step's box (by its title), as text.
    static string StepText(string html, string title) {
        foreach (Match m in Regex.Matches(html, "<div class=\"step[^\"]*\" id=\"[^\"]+\">.*?</div></div>", RegexOptions.Singleline))
            if (Text(m.Value).Contains(title)) return Text(m.Value);
        return "";
    }
    static string StepNo(string html, string title) {
        var t = StepText(html, title);
        var m = Regex.Match(t, "^\\s*(\\d+)");
        return m.Success ? m.Groups[1].Value : "?";
    }
    static string Hint(string html) { return Text(Regex.Match(html, "<div class=\"hint\">.*?</div>", RegexOptions.Singleline).Value); }
    // The body of one detail's block (the first one, or the variant given).
    static string Block(string html, string detail, string variant = null) {
        var m = Regex.Match(html, "<tbody class=\"block\" data-detail=\"" + Regex.Escape(detail) + "\"" +
                                  (variant == null ? "" : " data-variant=\"" + variant + "\"") + "[^>]*>(.*?)</tbody>", RegexOptions.Singleline);
        return m.Success ? m.Value : "";
    }
    static string Text(string html) { return System.Net.WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", "")); }

    [STAThread]
    static void Main() {
        TC.AdminView = () => false;          // the pages as an operator reads them
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        WiringTable.Clear();
        WiringTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wiring.csv"), plc => plc);
        var host = new Form { ClientSize = new Size(900, 400) };

        Console.WriteLine("--- the FA2010 pages against the drawings ---");
        var fa = Assembly.LoadFrom("Kaban.Machines.FA2010.exe");
        var pages = new[] { "ManualClamping_Moulds", "ManualCutting", "ManualEntry_Conveyor", "ManualExit", "ManualMilling",
                            "ManualOther", "ManualProfile_Recognition", "ManualRobot", "ManualYZ_Cross_Rollers" };
        var units = new List<KeyValuePair<string, CylinderUnitHMI>>();
        // a page's controls and those in its group boxes (14 October: the user put V Cutting in one)
        Func<Control, IEnumerable<Control>> kids = null;
        kids = c => c.Controls.Cast<Control>().SelectMany(k => k is GroupBoxHMI ? new[] { k }.Concat(kids(k)) : new[] { k });
        // (9 October) a cylinder with an encoder is described once, on its row of Machine Parameters >
        // Encoders - its manual pages' units take it from there
        foreach (var p in pages.Concat(new[] { "Parameters.ParamEncoders" })) {
            var page = (Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens." + (p.Contains(".") ? p : "Manual." + p)));
            foreach (var u in kids(page).OfType<CylinderUnitHMI>().Where(x => x.Kind == UnitKind.Cylinder))
                units.Add(new KeyValuePair<string, CylinderUnitHMI>(p + "." + u.Name, u));
            // a cylinder row's manual movement (a pneumatic positioning's, an encoder's): the unit inside, by the row's name
            foreach (var r in kids(page).OfType<CylinderRowHMI>())
                units.Add(new KeyValuePair<string, CylinderUnitHMI>(p + "." + r.Name, r.Unit));
        }
        Func<string, CylinderUnitHMI> unit = n => units.First(k => k.Key.EndsWith("." + n) || k.Key == n).Value;
        Func<CylinderUnitHMI, IEnumerable<string>> lamps = u => new[] { u.Lamp1AAddress, u.Lamp2AAddress, u.Lamp1BAddress, u.Lamp2BAddress }.Where(a => !string.IsNullOrEmpty(a));

        // Left for a decision (REVIEW 4r) until 8 October: three units whose titles were not
        // what their bits drive. The old program's pages settled it - one unit, the
        // Mullion End Milling Unit on Cutting, drives the mullion milling unit.
        var undecided = new string[0];
        var mullion = units.Where(k => new[] { WiringTable.OutputFor(1, k.Value.CommandAAddress), WiringTable.OutputFor(1, k.Value.CommandBAddress) }
                                           .Any(o => o != null && o.Text.StartsWith("Mullion Milling Unit"))).Select(k => k.Key).ToList();
        Check("one unit drives the mullion milling unit, titled so (Cutting's Mullion End Milling Unit)",
              mullion.Count == 1 && mullion[0] == "ManualCutting.unitMullion_End_Milling_Unit", string.Join(",", mullion));
        var drawn = units.Where(k => !undecided.Contains(k.Key) &&
                                     (WiringTable.OutputFor(1, k.Value.CommandAAddress) != null || WiringTable.OutputFor(1, k.Value.CommandBAddress) != null)).ToList();
        Check("every unit whose command drives a drawn output has its valve and feedback",
              drawn.All(k => k.Value.ValveType != CylinderValve.Unspecified && k.Value.FeedbackType != CylinderFeedback.Unspecified),
              string.Join(", ", drawn.Where(k => k.Value.ValveType == CylinderValve.Unspecified || k.Value.FeedbackType == CylinderFeedback.Unspecified).Select(k => k.Key)));
        Check("  no lamp reads a data word (the molds showed tolerance D75x and target D15x)", units.All(k => lamps(k.Value).All(a => !a.StartsWith("D"))),
              string.Join(", ", units.Where(k => lamps(k.Value).Any(a => a.StartsWith("D"))).Select(k => k.Key)));
        // A lamp on a drawn unit is a sensor on the drawings (or read through a module).
        var stray = drawn.SelectMany(k => lamps(k.Value).Where(a => WiringTable.Find(1, a) == null).Select(a => k.Key + ":" + a)).ToList();
        Check("  every lamp of a drawn unit is a sensor the drawings show", stray.Count == 0, string.Join(", ", stray));
        var ga = unit("unitGripper_Angle");   // the drawings' pairs, kept against the old page (8 October)
        Check("the gripper's 0 degree button shows the 0 degree sensor (SN95, X122 = M262), 90 the 90 (M263)",
              ga.Lamp1AAddress == "M262" && ga.Lamp1BAddress == "M263" && WiringTable.Find(1, "M262").Address == "X122");
        var pr = unit("ManualYZ_Cross_Rollers.unitProfile_Recognition");
        Check("the 1. cross roller (Y143 / Y144) shows ITS back sensor, SN.101 on X126 (M266)",
              pr.Lamp1AAddress == "M266" && WiringTable.OutputFor(1, pr.CommandAAddress).Address == "Y143");
        Check("the milling front clamping shows its pressure switch (X73 = M243), not M349",
              unit("encMilling_Cutting_Left_Front").Lamp1BAddress == "M243");
        Check("a mold: 5/3 and its analogue sensor, no lamps",
              unit("ppConveyor_Front_Vertical").ValveType == CylinderValve.ThreeWayClosedCenter &&
              unit("ppConveyor_Front_Vertical").FeedbackType == CylinderFeedback.AnalogueMagnetic && !lamps(unit("ppConveyor_Front_Vertical")).Any());
        Check("the milling roller: no sensor on the drawings (X55 free), no lamp", unit("ManualMilling.unitRoller_Up_Down").FeedbackType == CylinderFeedback.NoSensor &&
              !lamps(unit("ManualMilling.unitRoller_Up_Down")).Any());
        Console.WriteLine("  what TypeCheck still says (for the review):");
        foreach (var k in units.Where(x => x.Value.TypeCheck != "OK"))
            Console.WriteLine("      " + k.Key + ": " + k.Value.TypeCheck);

        Console.WriteLine("--- for a fault only ---");
        var u1 = new CylinderUnitHMI {
            PlcNumber = 1, TitleTextKey = "Front Clamping", Name = "unitFC",
            CommandAAddress = "M646", Lamp1AAddress = "M225",
            CommandBAddress = "M645", Lamp1BAddress = "M240",
            ErrorAAddress = "M845", ErrorBAddress = "M844", PositionAddress = "D4",
            ButtonBEnableConditions = SystemCondition.ManualMode,
            ValveType = CylinderValve.ThreeWayClosedCenter, FeedbackType = CylinderFeedback.MagneticPlusPressureSwitch };
        u1.SetBounds(10, 10, 360, 64); host.Controls.Add(u1); host.Show(); Application.DoEvents();
        var extra = Field<AlarmHelpBadge>(u1, "_helpB").ExtraAddresses.ToList();
        Check("each side's watcher reads the movement's devices: command, both coils (from the drawings), both ends' sensors",
              new[] { "M645", "Y32", "Y33", "M225", "M240" }.All(extra.Contains), string.Join(",", extra));
        mem.Clear(); Scan(u1); Application.DoEvents();
        Check("no fault: the '?' offers no troubleshooting", !u1.HelpDocuments().Any(d => d.Movement != null || d.Valve != 0 || d.StepByStep) &&
              !Field<UnitHelpBadge>(u1, "_help").Visible);

        Console.WriteLine("--- clamp (B) failed, Y32 was OFF ---");
        mem["M645"] = 1; mem["Y32"] = 0; mem["M225"] = 1; Scan(u1);
        mem["M844"] = 1; Scan(u1); Application.DoEvents();
        var docs = u1.HelpDocuments();
        Check("a fault: the '?' offers the step-by-step page first, then the manual", docs.Count >= 2 && docs[0].StepByStep && docs[0].Title == "Step by step - Clamp" &&
              !docs[1].StepByStep && docs[1].Movement != null, string.Join(" | ", docs.Select(d => d.Title)));
        var html = Steps(u1, "B");
        var text = Text(html);
        Check("the page: the unit and the movement, named from the drawing (Clamp, not the button's sign)",
              text.Contains("Front Clamping") && text.Contains("\u201CClamp\u201D did not complete"), Text(Regex.Match(html, "<h1>.*?</h1>").Value));
        var path = Text(Regex.Match(html, "<nav class=\"path\">.*?</nav>", RegexOptions.Singleline).Value);
        Check("  the signal path down the left side: Y32, relay K29, coil Y.23, valve Y28/Y29, P16, pressure switch SN83 (X70) - no HMI command",
              Regex.IsMatch(path, "Y32.*K29.*Y\\.23.*Y28/Y29.*P16.*SN83") && !path.Contains("HMI command"), path);
        Check("  the HMI's command (M645) named nowhere on the page", !text.Contains("M645"));
        // 8 October: "most of the 3 way valves we use are closed center valves but you assumed they were
        // open centered before" - two kinds, the unit's own (3WayClosedCenter for every one there is)
        Check("  a 5/3 valve, its closed centre as the unit says (3WayClosedCenter)", text.Contains("5/3 valve: it moves only while") &&
              text.Contains("(closed centre)") && !text.Contains("(open centre)") && u1.ValveType == CylinderValve.ThreeWayClosedCenter);
        var openPage = Text(TC.BuildSteps(u1.MovementFor("B"), TC.ValveType.OpenCentre, (TC.FeedbackType)TC.CanonicalFeedback((int)u1.FeedbackType), 0, "EN"));
        Check("  an open-centre one (3WayOpenCenter): its own words, the same 5/3 page", openPage.Contains("(open centre)") && !openPage.Contains("(closed centre)") &&
              openPage.Contains("5/3 valve: it moves only while") && TC.TemplateName(TC.ValveType.OpenCentre, TC.FeedbackType.PressureSwitch) == "Cylinder_5-3_PressureSwitch.html");
        Check("  the two kinds named apart", TC.Describe(TC.ValveType.ClosedCentre).Contains("closed center") && TC.Describe(TC.ValveType.OpenCentre).Contains("open center") &&
              TC.CanonicalValve(4) == 4 && TC.ManualValve(4) == 3 && TC.Parameter1(TC.ErrorType.CylinderMovement).Values.Contains(4));
        Check("  what the HMI saw: Y32 OFF at the alarm, in red", Regex.IsMatch(html, "Valve output <code>Y32</code></td><td>ON</td><td><span class=\"st-bad\">OFF"));
        Check("  where to start: Y32 was OFF - the step that asks whether the PLC switches it on",
              Hint(html).Contains("Y32 was OFF") && Hint(html).Contains("Step " + StepNo(html, "Does the PLC switch on Y32?")), Hint(html));
        Check("step 1: where is the cylinder - not moved / stopped / arrived, each to its own part",
              StepNo(html, "Where is the cylinder now?") == "1" && StepText(html, "Where is the cylinder now?").Contains("not moved") &&
              StepText(html, "Where is the cylinder now?").Contains("stopped on the way") && StepText(html, "Where is the cylinder now?").Contains("arrived"));
        var order = new[] { "Does the PLC switch on Y32?", "Why does the PLC not switch it on?", "Does the output card switch Y32?", "Does relay K29 pull in?",
                            "Does coil Y.23 get power?", "Does the valve switch?", "Is there air to move it?", "Is the cylinder free to move?",
                            "stay ON the whole way?", "Does the air pressure hold", "Is something in its way?", "Is it only too slow?",
                            "Does pressure switch SN83", "at the other end gone OFF?", "Did it arrive in time?", "Still not solved?" };
        var numbers = order.Select(t => StepNo(html, t)).ToList();
        Check("the steps in the order of the search (signal, way, sensors, time)", string.Join(",", numbers) == string.Join(",", Enumerable.Range(2, order.Length)),
              string.Join(",", numbers));
        var a1 = StepText(html, "Does the PLC switch on Y32?");
        Check("  the output: press the button (Clamp), watch Y32; Y33 (Release) must stay OFF; both ON is the double-coil fault",
              a1.Contains("Clamp") && a1.Contains("Y33 (Release) stays OFF") && a1.Contains("both ON"), a1);
        var a2 = StepText(html, "Why does the PLC not switch it on?");
        Check("  why not: what the button waits for (Manual Mode)", a2.Contains("Manual Mode") && a2.Contains("Never bypass"), a2);
        var a3 = StepText(html, "Does the output card switch Y32?");
        Check("  the card: LED 32 in the OUT row of card U4; 24 V between wire 226 and 0 V (wire 21)", a3.Contains("LED 32 in the OUT row of card U4") &&
              a3.Contains("Wire 226") && a3.Contains("0 V (wire 21)"), a3);
        var a3html = Regex.Match(html, "<div class=\"step[^\"]*\" id=\"A3\".*?</table>", RegexOptions.Singleline).Value;
        Check("  with its drawings: the card's OUT row around LED 32 (30, 31 | 33, 34), the terminal with wire 226 and the meter",
              Regex.Matches(a3html, "<svg class=\"pic\"").Count == 2 && Regex.IsMatch(Text(a3html), "30\\s*31\\s*33\\s*34") && a3html.Contains(">226<") && a3html.Contains("24 V DC"),
              Regex.Matches(a3html, "<svg class=\"pic\"").Count + "");
        var a5 = StepText(html, "Does coil Y.23 get power?");
        Check("  the coil: on Y28/Y29, wire 324 (sheet 26.7)", a5.Contains("Y28/Y29") && a5.Contains("wire 324") && a5.Contains("26.7"), a5);
        var b1 = StepText(html, "stay ON the whole way?");
        Check("stopped on the way: a 5/3 needs the signal the whole way", b1.Contains("5/3 valve: when the signal ends early it stops where it is"), b1);
        var c1 = StepText(html, "Does pressure switch SN83");
        Check("arrived: the clamp's pressure switch (SN83 on X70, U8 terminal X10), the gauge, the setpoint", c1.Contains("X70") && c1.Contains("terminal X10") &&
              c1.Contains("setpoint") && c1.Contains("gauge"), c1);
        Check("  then the back sensor SN59 (X51) must be OFF", StepText(html, "at the other end gone OFF?").Contains("SN59"));
        Check("every fault answer says: test again, else the next step", Regex.Matches(html, "Still failing").Count >= 15);
        Check("the parts of this movement at the end", text.Contains("The parts of this movement") && text.Contains("0 V is wire 21") && text.Contains("+24 V is wire 20"));

        var chart = Chart(u1, "B");
        Check("the manual after it: the general view, no notes in its checks, a line leading to the steps",
              chart.Length > 0 && !chart.Contains("On this machine") && chart.Contains("step-by-step troubleshooter") && chart.Contains("Steps_FrontClamping_B.html"));

        Console.WriteLine("--- Y32 ON but the cylinder never left its start ---");
        mem["M844"] = 0; Scan(u1);
        mem["Y32"] = 1; mem["M225"] = 1; mem["M240"] = 0; Scan(u1);
        mem["M844"] = 1; Scan(u1);
        html = Steps(u1, "B");
        Check("X51 still ON at the start: from the card on", Hint(html).Contains("still ON") && Hint(html).Contains("Step " + StepNo(html, "Does the output card switch Y32?")), Hint(html));

        Console.WriteLine("--- arrived, not in time ---");
        mem["M844"] = 0; Scan(u1);
        mem["Y32"] = 1; mem["M225"] = 0; mem["M240"] = 1; Scan(u1);
        mem["M844"] = 1; Scan(u1);
        html = Steps(u1, "B");
        Check("X70 ON, X51 OFF at the alarm: the time", Hint(html).Contains("Step " + StepNo(html, "Did it arrive in time?")), Hint(html));

        Console.WriteLine("--- both coils ---");
        mem["M844"] = 0; Scan(u1);
        mem["Y32"] = 1; mem["Y33"] = 1; mem["M240"] = 0; Scan(u1);
        mem["M844"] = 1; Scan(u1);
        html = Steps(u1, "B");
        Check("both coils ON: the output step, saying so", Hint(html).Contains("Both coils") && Hint(html).Contains("Step " + StepNo(html, "Does the PLC switch on Y32?")), Hint(html));

        Console.WriteLine("--- release (A): the back sensor confirms it, the pressure switch must go OFF ---");
        mem.Clear(); Scan(u1);
        mem["M646"] = 1; mem["Y33"] = 1; mem["M240"] = 1; Scan(u1);
        mem["M845"] = 1; Scan(u1);
        html = Steps(u1, "A");
        Check("Release: the back sensor SN59 must come ON - its LED, LED 11 in the IN row of module U7, X51 ON", StepText(html, "Does sensor SN59").Contains("LED 11 in the IN row of card U7") &&
              StepText(html, "Does sensor SN59").Contains("X51 ON"), StepText(html, "Does sensor SN59"));
        Check("  the copy the PLC makes of it (M240) is not named", !Text(html).Contains("M240"));
        var off = StepText(html, "gone OFF?");
        Check("  the pressure switch must go OFF: one stuck ON means the line keeps its pressure", off.Contains("SN83") && off.Contains("keeps its pressure"), off);

        Console.WriteLine("--- a spring valve: its coil's movement and the spring's are not the same search ---");
        WiringTable.EnsureLoaded();
        var foil = new CylinderUnitHMI {
            PlcNumber = 1, TitleTextKey = "Foil Roll", Lamp1AAddress = "M237", CommandBAddress = "M739", ErrorAAddress = "M896", ErrorBAddress = "M897",
            ValveType = CylinderValve.SingleSolenoid, FeedbackType = CylinderFeedback.SingleMagnetic };
        foil.SetBounds(10, 90, 360, 64); host.Controls.Add(foil); Application.DoEvents();
        mem.Clear(); Scan(foil);
        mem["M739"] = 1; mem["Y116"] = 1; mem["M237"] = 0; Scan(foil);
        mem["M897"] = 1; Scan(foil);
        html = Steps(foil, "B");
        text = Text(html);
        Check("Up (the coil): named from the drawing, Y116 must come ON", text.Contains("\u201CUp\u201D did not complete") && StepNo(html, "Does the PLC switch on Y116?") != "?");
        Check("  away from the only sensor: SN91 must go OFF and nothing sees the far end", StepText(html, "Has sensor SN91").Contains("Nothing sees the end of Up"),
              StepText(html, "Has sensor SN91"));
        Check("  a spring valve needs its coil the whole stroke", StepText(html, "stay ON the whole way?").Contains("Spring valve"));
        Check("  the card's own terminal name: LED 16 in the OUT row of module U13 for Y116", StepText(html, "Does the output card switch Y116?").Contains("LED 16 in the OUT row of card U13"),
              StepText(html, "Does the output card switch Y116?"));
        mem.Clear(); Scan(foil);
        mem["Y116"] = 1; mem["M237"] = 0; Scan(foil);
        mem["M896"] = 1; Scan(foil);
        html = Steps(foil, "A");
        text = Text(html);
        Check("Down (the spring): Y116 must go OFF - no button of its own: end Up", text.Contains("\u201CDown\u201D did not complete") &&
              StepText(html, "Does the PLC switch Y116 OFF?").Contains("End Up") && !text.Contains("Press the"), StepText(html, "Does the PLC switch Y116 OFF?"));
        Check("  where to start: Y116 was still ON", Hint(html).Contains("still ON") && Hint(html).Contains("Step " + StepNo(html, "Does the PLC switch Y116 OFF?")), Hint(html));
        Check("  the relay must drop out, the coil lose its power, the valve spring back (a locked override)",
              StepNo(html, "Does relay K75 drop out?") != "?" && StepNo(html, "Is coil Y.91 without power?") != "?" &&
              StepText(html, "Does the valve spring back?").Contains("locked"));
        Check("  towards the sensor: SN91 must come ON", StepText(html, "Does sensor SN91").Contains("LED lit"));

        Console.WriteLine("--- an encoder; an analogue sensor ---");
        var u2 = new CylinderUnitHMI {
            PlcNumber = 1, TitleTextKey = "Top Clamping", CommandAAddress = "M648", Lamp1AAddress = "M226", CommandBAddress = "M647",
            ErrorAAddress = "M847", ErrorBAddress = "M846", PositionAddress = "D6",
            ValveType = CylinderValve.ThreeWayClosedCenter, FeedbackType = CylinderFeedback.EncoderPlusReference };
        u2.SetBounds(10, 170, 360, 64); host.Controls.Add(u2); Application.DoEvents();
        mem.Clear(); mem["M847"] = 1; mem["Y35"] = 0; Scan(u2);
        html = Steps(u2, "A");
        Check("referenced first: the reference sensor SN60 on X52", StepText(html, "Is the unit referenced?").Contains("SN60") && StepText(html, "Is the unit referenced?").Contains("X52"));
        Check("  the count (the encoder through K78; its register D6 is internal: not named), the window", !StepText(html, "Does the position count").Contains("D6") &&
              StepText(html, "Does the position count").Contains("K78") && StepNo(html, "target window") != "?");
        TC.AdminView = () => true;
        var adminHtml = Steps(u2, "A");
        TC.AdminView = () => false;
        Check("  for an Admin: the register D6 and the alarm's bit (M847) named", StepText(adminHtml, "Does the position count").Contains("D6") &&
              Text(Regex.Match(adminHtml, "<h1>.*?</h1>").Value).Contains("M847"), Text(Regex.Match(adminHtml, "<h1>.*?</h1>").Value));
        Check("  already on when the page started reading: said so", Text(html).Contains("already on when the page started reading"));
        var mold = new CylinderUnitHMI {
            PlcNumber = 1, TitleTextKey = "Mold", CommandAAddress = "M541", CommandBAddress = "M540", ErrorAAddress = "M915", PositionAddress = "D110",
            ValveType = CylinderValve.ThreeWayClosedCenter, FeedbackType = CylinderFeedback.AnalogueMagnetic };
        mold.SetBounds(10, 250, 360, 64); host.Controls.Add(mold); Application.DoEvents();
        mem.Clear(); mem["M915"] = 1; Scan(mold);
        html = Steps(mold, "A");
        Check("an analogue sensor: the position reading (D110 not named below Admin), below 4 mA / above 20 mA", !StepText(html, "position reading reach the target").Contains("D110") &&
              StepText(html, "position reading reach the target").Contains("4 mA"));

        Console.WriteLine("--- from the alarm list: the same page ---");
        Kaban.UI.Helpers.AlarmHelpLinks.Clear();
        foreach (var l in u1.HelpLinks()) typeof(Kaban.UI.Helpers.AlarmHelpLinks).GetMethod("Register", Any, null, new[] { l.GetType() }, null).Invoke(null, new object[] { l });
        mem.Clear(); Scan(u1); mem["M645"] = 1; mem["Y32"] = 1; Scan(u1); mem["M844"] = 1; Scan(u1);
        var listDocs = Kaban.UI.Helpers.AlarmHelpLinks.DocumentsFor(1, "M844", null);
        Check("the alarm list's help: the step-by-step page first, with the movement and its states", listDocs.Count >= 2 && listDocs[0].StepByStep &&
              listDocs[0].Movement != null && listDocs[0].Movement.Output != null && listDocs[0].Movement.Output.AtAlarm == 1,
              string.Join(" | ", listDocs.Select(d => d.Title)));
        TC.Clock = () => new DateTime(2026, 10, 2, 10, 20, 0);
        var titled = TC.BuildSteps(listDocs[0].Movement, (TC.ValveType)listDocs[0].Valve, (TC.FeedbackType)listDocs[0].Feedback, 0, "EN",
                                   new DateTime(2026, 10, 2, 10, 14, 5), "Front Clamping Clamp Alarm");
        var h1 = Text(Regex.Match(titled, "<h1>.*?</h1>").Value);
        var dates = Regex.Match(titled, "<p class=\"dates\">.*?</p>").Value;
        Check("opened from an alarm: the alarm is the title; its time and the page's under it (small, italic)", h1 == "Front Clamping Clamp Alarm" &&
              Text(dates) == "Alarm: 2026-10-02 10:14:05 \u00b7 Helper made: 2026-10-02 10:20" && titled.Contains(".dates{") &&
              Regex.IsMatch(titled, "\\.dates\\{[^}]*font-style:italic"), h1 + " / " + Text(dates));
        Check("  its bit (M844) and the HMI's command (M645) named nowhere below Admin", !Text(titled).Contains("M844") && !Text(titled).Contains("M645"));
        TC.AdminView = () => true;
        var titledAdmin = TC.BuildSteps(listDocs[0].Movement, (TC.ValveType)listDocs[0].Valve, (TC.FeedbackType)listDocs[0].Feedback, 0, "EN",
                                        new DateTime(2026, 10, 2, 10, 14, 5), "Front Clamping Clamp Alarm");
        TC.AdminView = () => false;
        Check("  an Admin: the bit after the title, \"(M844)\"; the command still not", Text(Regex.Match(titledAdmin, "<h1>.*?</h1>").Value) == "Front Clamping Clamp Alarm (M844)" &&
              !Text(titledAdmin).Contains("M645"), Text(Regex.Match(titledAdmin, "<h1>.*?</h1>").Value));
        var drawings = Regex.Matches(titled, "<svg class=\"pic\"").Count;
        Check("  the checks are drawn: where the cylinder is, the I/O page, the card and the terminal, relay, coil, valve, air, the sensor ...", drawings >= 10, drawings + "");

        Console.WriteLine("--- a machine without drawings ---");
        WiringTable.Clear();
        var bare = new TC.MovementInfo { Station = 7, Unit = "Gate", Side = "A", Name = "\u25B2", OtherName = "\u25BC",
                                         Command = new TC.MovementInfo.Device { Address = "M10" }, Output = new TC.MovementInfo.Device { Address = "Y3", AtAlarm = 1 },
                                         Error = "M900" };
        bare.Feedback.Add(new TC.MovementInfo.Device { Address = "X5", AtAlarm = 0 });
        bare.OppositeFeedback.Add(new TC.MovementInfo.Device { Address = "X4", AtAlarm = 1 });
        string bp = "";
        try { bp = TC.BuildSteps(bare, TC.ValveType.DoubleSolenoid, TC.FeedbackType.TwoMagnetic); } catch (Exception ex) { bp = "EXCEPTION " + ex.Message; }
        Check("still a whole page: the button's sign as Up, the devices by their addresses", Text(bp).Contains("\u201CUp\u201D did not complete") &&
              StepNo(bp, "Does the PLC switch on Y3?") != "?" && StepNo(bp, "Does sensor X5") == "?" && Text(bp).Contains("X5"), Text(bp).Substring(0, Math.Min(300, Text(bp).Length)));
        Check("  no wire numbers made up", !Text(bp).Contains("wire 21"));

        host.Close();
        Console.WriteLine(fail == 0 ? "\nCYLINDER STEPS ALL PASS" : "\nCYLINDER STEPS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
