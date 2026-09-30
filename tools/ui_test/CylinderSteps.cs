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
// the cylinder manual offered only for a fault; what was missing when the
// movement failed decides where to start; every check of 1.1 - 1.8 says what
// to measure on this machine.
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
        var d = u.HelpDocuments().FirstOrDefault(x => x.Movement != null && x.Movement.Side == side);
        return d == null ? "" : File.ReadAllText(TC.Tailor(d));
    }
    // The body of one detail's block (the first one, or the variant given).
    static string Block(string html, string detail, string variant = null) {
        var m = Regex.Match(html, "<tbody class=\"block\" data-detail=\"" + Regex.Escape(detail) + "\"" +
                                  (variant == null ? "" : " data-variant=\"" + variant + "\"") + "[^>]*>(.*?)</tbody>", RegexOptions.Singleline);
        return m.Success ? m.Value : "";
    }
    static string Text(string html) { return System.Net.WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", "")); }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        WiringTable.Clear();
        WiringTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wiring.csv"), plc => plc);
        var host = new Form { ClientSize = new Size(900, 400) };

        Console.WriteLine("--- the FA2010 pages against the drawings ---");
        var fa = Assembly.LoadFrom("Kaban.Machines.FA2010.exe");
        var pages = new[] { "ManualClamping_Moulds", "ManualCutting", "ManualEntry_Conveyor", "ManualExit", "ManualMilling",
                            "ManualOther", "ManualProfile_Recognition", "ManualRobot", "ManualYZ_Cross_Rollers" };
        var units = new List<KeyValuePair<string, CylinderUnitHMI>>();
        foreach (var p in pages) {
            var page = (Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens.Manual." + p));
            foreach (var u in page.Controls.OfType<CylinderUnitHMI>().Where(x => x.Kind == UnitKind.Cylinder))
                units.Add(new KeyValuePair<string, CylinderUnitHMI>(p + "." + u.Name, u));
        }
        Func<string, CylinderUnitHMI> unit = n => units.First(k => k.Key.EndsWith("." + n) || k.Key == n).Value;
        Func<CylinderUnitHMI, IEnumerable<string>> lamps = u => new[] { u.Lamp1AAddress, u.Lamp2AAddress, u.Lamp1BAddress, u.Lamp2BAddress }.Where(a => !string.IsNullOrEmpty(a));

        // Left for a decision (REVIEW 4r): their titles are not what their command
        // bits drive on this machine (the mullion milling unit, its blade change).
        var undecided = new[] { "ManualCutting.unitFront_Height_Setting", "ManualMilling.unitFront_Height_Setting",
                                "ManualProfile_Recognition.unitRigth_Top_FW_Back_SA3030" };
        Check("the units left for a decision are the ones whose titles do not match their bits (mullion milling)",
              undecided.All(n => units.Any(k => k.Key == n) &&
                                 new[] { WiringTable.OutputFor(1, units.First(k => k.Key == n).Value.CommandAAddress),
                                         WiringTable.OutputFor(1, units.First(k => k.Key == n).Value.CommandBAddress) }
                                 .Any(o => o != null && o.Text.StartsWith("Mullion Milling"))));
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
        var ga = unit("unitGripper_Angle");
        Check("the gripper's 0 degree button shows the 0 degree sensor (SN95, X122 = M262), 90 the 90 (M263)",
              ga.Lamp1AAddress == "M262" && ga.Lamp1BAddress == "M263" && WiringTable.Find(1, "M262").Address == "X122");
        var pr = unit("ManualYZ_Cross_Rollers.unitProfile_Recognition");
        Check("the 1. cross roller (Y143 / Y144) shows ITS back sensor, SN.101 on X126 (M266)",
              pr.Lamp1AAddress == "M266" && WiringTable.OutputFor(1, pr.CommandAAddress).Address == "Y143");
        Check("the milling front clamping shows its pressure switch (X73 = M243), not M349",
              unit("ManualMilling.unitFront_Clamping").Lamp1BAddress == "M243");
        Check("a mold: 5/3 and its analogue sensor, no lamps",
              unit("unitConveryor_Front_Vertical").ValveType == CylinderValve.DoubleSolenoid_3Way &&
              unit("unitConveryor_Front_Vertical").FeedbackType == CylinderFeedback.AnalogueMagnetic && !lamps(unit("unitConveryor_Front_Vertical")).Any());
        Check("the milling roller: no sensor on the drawings (X55 free), no lamp", unit("ManualMilling.unitRoller_Up_Down").FeedbackType == CylinderFeedback.NoSensor &&
              !lamps(unit("ManualMilling.unitRoller_Up_Down")).Any());
        Console.WriteLine("  what TypeCheck still says (for the review):");
        foreach (var k in units.Where(x => x.Value.TypeCheck != "OK"))
            Console.WriteLine("      " + k.Key + ": " + k.Value.TypeCheck);

        Console.WriteLine("--- the manual: for a fault only ---");
        var u1 = new CylinderUnitHMI {
            PlcNumber = 1, TitleTextKey = "Front Clamping", Name = "unitFC",
            CommandAAddress = "M646", ButtonATextKey = "Release", Lamp1AAddress = "M225",
            CommandBAddress = "M645", ButtonBTextKey = "Clamp", Lamp1BAddress = "M240",
            ErrorAAddress = "M845", ErrorBAddress = "M844", PositionAddress = "D4",
            ValveType = CylinderValve.DoubleSolenoid_3Way, FeedbackType = CylinderFeedback.MagneticPlusPressureSwitch };
        u1.SetBounds(10, 10, 360, 64); host.Controls.Add(u1); host.Show(); Application.DoEvents();
        var extra = Field<AlarmHelpBadge>(u1, "_helpB").ExtraAddresses.ToList();
        Check("each side's watcher reads the movement's devices: command, both coils (from the drawings), both ends' sensors",
              new[] { "M645", "Y32", "Y33", "M225", "M240" }.All(extra.Contains), string.Join(",", extra));
        mem.Clear(); Scan(u1); Application.DoEvents();
        Check("no fault: the '?' offers no troubleshooting", !u1.HelpDocuments().Any(d => d.Movement != null || d.Valve != 0) &&
              !Field<UnitHelpBadge>(u1, "_help").Visible);

        Console.WriteLine("--- clamp (B) failed, the valve output was OFF: start at the output ---");
        mem["M645"] = 1; mem["Y32"] = 0; mem["M225"] = 1; Scan(u1);
        mem["M844"] = 1; Scan(u1); Application.DoEvents();
        var html = Chart(u1, "B");
        Check("a fault: the '?' offers the tailored manual for that movement", html.Length > 0 && Field<UnitHelpBadge>(u1, "_help").Visible);
        var box = Text(html.Substring(0, html.IndexOf("<section")));
        Check("  the box names the alarm bit and the states at the alarm", box.Contains("M844") && box.Contains("at the alarm OFF"), box.Substring(0, Math.Min(200, box.Length)));
        Check("  where to start: the output was OFF - 1.6, 1.5, 1.3", box.Contains("Where to start") && box.Contains("was OFF") &&
              box.Contains("Check 1.6, 1.5, 1.3 first"), Regex.Match(box, "Where to start:[^.]*\\.[^.]*\\.").Value);
        Check("  1.6 marked 'start here', 1.5 '2nd'", Block(html, "1.6").Contains("start here") && Block(html, "1.5").Contains(">2nd<"));

        Console.WriteLine("--- every check says what to measure ---");
        var b11 = Text(Block(html, "1.1")); var b12 = Text(Block(html, "1.2")); var b13 = Text(Block(html, "1.3"));
        var b14 = Text(Block(html, "1.4")); var b15 = Text(Block(html, "1.5")); var b16 = Text(Block(html, "1.6"));
        var b17 = Text(Block(html, "1.7")); var b18 = Text(Block(html, "1.8"));
        Check("1.1: the cylinder P16, pneumatic 3 E8, exhaust hoses 23 (A) / 24 (B) at valve Y28/Y29",
              b11.Contains("P16 Profile Recognition Front Clamping Cylinder") && b11.Contains("pneumatic sheet 3 E8") && b11.Contains("hoses 23 (A) / 24 (B) at valve Y28/Y29"), b11);
        Check("1.2: main air X21 (SW3, wire 90, sheet 12.1, shown at M201) and the clamping pressure valve Y30 by Y67",
              b12.Contains("X21 SW3, wire 90, sheet 12.1") && b12.Contains("M201") && b12.Contains("valve Y30") && b12.Contains("Y67"), b12);
        Check("1.3: coil Y.23 on wire 324 (sheet 26.7): 24 V between 324 and 0 V (21) with M645 held; override on Y28/Y29",
              b13.Contains("Coil Y.23") && b13.Contains("wire 324") && b13.Contains("26.7") && b13.Contains("between wire 324 and 0 V (wire 21)") &&
              b13.Contains("M645") && b13.Contains("Manual override: on valve Y28/Y29"), b13);
        Check("1.4 (5/3 only): this coil Y.23 from Y32, the other Y.24 from Y33 must be OFF",
              b14.Contains("Y.23 from Y32") && b14.Contains("Y.24 from Y33") && b14.Contains("must be OFF") &&
              Regex.Matches(html, "data-detail=\"1.4\"").Count == 1, b14);
        Check("1.5: relay K29 (sheet 19.6) fed by Y32 on wire 226; its contact feeds wire 324",
              b15.Contains("Relay K29 (sheet 19.6)") && b15.Contains("wire 226") && b15.Contains("feeds wire 324"), b15);
        Check("1.6: output Y32 (OFF at the alarm), commanded by M645; card U4, terminal Y32",
              b16.Contains("Output Y32") && b16.Contains("at the alarm OFF") && b16.Contains("M645") && b16.Contains("U4 FX5U-64MT-ESS, terminal Y32"), b16);
        Check("1.7 (pressure switch only): SW1 = SN83 on X70; the sensors with their wires",
              b17.Contains("SW1") && b17.Contains("X70") && b17.Contains("SN83") && b17.Contains("wire 147") &&
              Regex.Matches(html, "data-detail=\"1.7\"").Count == 1, b17);
        Check("1.8: the alarm M844, the stroke timed from Y32 ON to X70 ON", b18.Contains("M844") && b18.Contains("from Y32 ON to X70 ON"), b18);

        Console.WriteLine("--- the output ON but the cylinder never left: relay, coil, air, mechanics ---");
        mem["M844"] = 0; Scan(u1);
        mem["Y32"] = 1; mem["M225"] = 1; mem["M240"] = 0; Scan(u1);
        mem["M844"] = 1; Scan(u1);
        box = Text(Chart(u1, "B"));
        Check("still ON at the start (X51): 1.5, 1.3, 1.2, 1.1", box.Contains("still ON") && box.Contains("Check 1.5, 1.3, 1.2, 1.1 first"),
              Regex.Match(box, "Where to start:.*?first").Value);

        Console.WriteLine("--- it left its start but the switch did not come on: the sensor first ---");
        mem["M844"] = 0; Scan(u1);
        mem["Y32"] = 1; mem["M225"] = 0; mem["M240"] = 0; Scan(u1);
        mem["M844"] = 1; Scan(u1);
        box = Text(Chart(u1, "B"));
        Check("left its start (X51 OFF), X70 not ON: 1.7, 1.1, 1.2, 1.8", box.Contains("left its start") && box.Contains("Check 1.7, 1.1, 1.2, 1.8 first"),
              Regex.Match(box, "Where to start:.*?first").Value);

        Console.WriteLine("--- both coils ---");
        mem["M844"] = 0; Scan(u1);
        mem["Y32"] = 1; mem["Y33"] = 1; Scan(u1);
        mem["M844"] = 1; Scan(u1);
        box = Text(Chart(u1, "B"));
        Check("both coils ON: 1.4 first", box.Contains("Both coils") && box.Contains("Check 1.4, 1.6 first"), Regex.Match(box, "Where to start:.*?first").Value);

        Console.WriteLine("--- already on when the page started reading ---");
        var u2 = new CylinderUnitHMI {
            PlcNumber = 1, TitleTextKey = "Top Clamping", CommandAAddress = "M648", Lamp1AAddress = "M226", CommandBAddress = "M647",
            ErrorAAddress = "M847", ErrorBAddress = "M846", PositionAddress = "D6",
            ValveType = CylinderValve.DoubleSolenoid_3Way, FeedbackType = CylinderFeedback.EncoderPlusReference };
        u2.SetBounds(10, 90, 360, 64); host.Controls.Add(u2); Application.DoEvents();
        mem.Clear(); mem["M847"] = 1; mem["Y35"] = 0; Scan(u2);
        html = Chart(u2, "A");
        box = Text(html.Substring(0, html.IndexOf("<section")));
        Check("  said so: the states are from just after it", box.Contains("already on when this page started reading"));
        Check("  the encoder and the position in 1.7 (X12/X13 through K78, D6)", Text(Block(html, "1.7")).Contains("K78") && Text(Block(html, "1.7")).Contains("D6"),
              Text(Block(html, "1.7")));
        Check("  and the reference sensor SN60 on X52", Text(Block(html, "1.7")).Contains("SN60") && Text(Block(html, "1.7")).Contains("X52"));

        Console.WriteLine("--- from the alarm list: the same states ---");
        Kaban.UI.Helpers.AlarmHelpLinks.Clear();
        foreach (var l in u1.HelpLinks()) typeof(Kaban.UI.Helpers.AlarmHelpLinks).GetMethod("Register", Any, null, new[] { l.GetType() }, null).Invoke(null, new object[] { l });
        var docs = Kaban.UI.Helpers.AlarmHelpLinks.DocumentsFor(1, "M844", null);
        var mv = docs.Select(d => d.Movement).FirstOrDefault(m => m != null);
        Check("the alarm list's help carries the movement with its states at the alarm",
              mv != null && mv.Output != null && mv.Output.Address == "Y32" && mv.Output.AtAlarm == 1, mv == null ? "none" : mv.Output == null ? "no output" : mv.Output.Address + " " + mv.Output.AtAlarm);

        host.Close();
        Console.WriteLine(fail == 0 ? "\nCYLINDER STEPS ALL PASS" : "\nCYLINDER STEPS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
