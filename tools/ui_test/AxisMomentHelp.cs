using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

// AN AXIS AT THE MOMENT OF ITS CODE (16 October): "the AxisSettingHMI helper could be
// extended according to the movement state at the error moment - not moving, homing,
// manual and automatic movement: an overload is probably the reference sensor if it
// starts moving (BUSY ON) and gets stuck afterwards during homing, a mechanical crash or
// an axis limit setting at the other states - with the electrical drawing's details
// (the reference sensor) when useful."
class AMH {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    static Dictionary<string, int> mem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    static int Read(string a) { int v; return mem.TryGetValue(a, out v) ? v : 0; }
    static DateTime now = new DateTime(2026, 10, 16, 9, 0, 0);
    static void Scan(int n = 1) { for (int i = 0; i < n; i++) { now = now.AddSeconds(0.1); AlarmGeneration.Evaluate(1, Read, now); } }
    static void Pos(string w, double mm) {
        int raw = (int)Math.Round(mm * 10000);
        mem[w] = raw & 0xFFFF;
        mem[PlcAddressHelper.GetNextAddress(w)] = (raw >> 16) & 0xFFFF;
    }
    static void Busy(int bit, bool on) { int v = Read("U1\\G31501"); mem["U1\\G31501"] = on ? v | (1 << bit) : v & ~(1 << bit); }
    static string Text(string html) { return Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), @"\s+", " ").Replace("&minus;", "-").Replace("&ndash;", "-"); }
    static List<string> Titles(string html) {
        return Regex.Matches(html, "<h3>(.*?)</h3>").Cast<Match>().Select(m => Regex.Replace(m.Groups[1].Value, "<[^>]+>", "")).ToList();
    }
    static AxisSettingHMI Row(int n, string name, string pos, string code, string err, string back, string fwd, string min, string max, string refs,
                              bool vertical = false) {
        return new AxisSettingHMI { PlcNumber = 1, HMIAxisIndex = n, AxisNameKey = name, MotionNumber = 1, MotionAxisIndex = n,
                                    CommandBackAddress = back, CommandForwardAddress = fwd, PositionAddress = pos,
                                    MinLimitAddress = min, MaxLimitAddress = max, Divisor = 10000, DecimalPlaces = 2, Suffix = "mm",
                                    ReferenceSensorAddress = refs, ReferenceSensorSide = AxisReferenceSide.Forward, VerticalMovement = vertical,
                                    HomeRequestAddress = "M12" + n, ErrorAddress = "M89" + n, ServoAlarmAddress = code, AxisAlarmAddress = err };
    }
    static AlarmGeneration.Axis Watch(string servo) { return AlarmGeneration.Axes.First(a => a.ServoAlarm == servo); }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        WiringTable.Clear();
        WiringTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wiring.csv"), plc => plc);
        var dir = Path.Combine(Path.GetTempPath(), "AxisMoment_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        AlarmGeneration.SettingsPath = Path.Combine(dir, "AlarmGeneration.cfg");
        AlarmGeneration.ResetForTests();
        MachineConditionMap.Register(SystemCondition.MachineIsRunning, 1, "D200", DataTypeEnum.Bin_32, ComparisonOperator.Greater, 0);
        MachineConditionMap.Register(SystemCondition.ManualMode, 1, "M100", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        mem["M100"] = 1;                                  // not in manual mode
        SecurityManager.Login("m", SecurityLevel.Maintenance);

        Console.WriteLine("--- what the axis was doing: the module's state first, then the buttons and the machine ---");
        Check("a jog button held: manual (whatever the module says)", AxisMoment.MotionOf(8, true, false, true, true) == AxisMotion.Manual);
        Check("the module's home return (7): homing", AxisMoment.MotionOf(7, true, false, false, false) == AxisMotion.Homing);
        Check("JOG / manual pulse (3, 4): manual", AxisMoment.MotionOf(3, true, null, null, null) == AxisMotion.Manual &&
                                                   AxisMoment.MotionOf(4, true, null, null, null) == AxisMotion.Manual);
        Check("positioning while the machine runs: automatic", AxisMoment.MotionOf(8, true, false, false, true) == AxisMotion.Automatic);
        Check("positioning in manual mode (a manual page's 'go to'): manual", AxisMoment.MotionOf(8, true, false, false, false, true) == AxisMotion.Manual);
        Check("moving while the axis asks to be referenced: homing (a homing the PLC runs itself)",
              AxisMoment.MotionOf(8, true, false, false, true, false, true) == AxisMotion.Homing);
        Check("BUSY alone, no module state read: automatic", AxisMoment.MotionOf(null, true, null, null, null) == AxisMotion.Automatic);
        Check("standby, not BUSY, no button: standing", AxisMoment.MotionOf(0, false, false, false, true) == AxisMotion.Standing);
        Check("nothing read: not known", AxisMoment.MotionOf(null, null, null, null, null) == AxisMotion.Unknown);
        // 17 October: "moved by hand" with the module in standby and BUSY OFF was not true
        Check("a jog command ON, the module standing (standby, BUSY OFF): standing - the module says what it did",
              AxisMoment.MotionOf(0, false, false, true, false) == AxisMotion.Standing && AxisMoment.MotionOf(0, false, true, true, false) == AxisMotion.Standing);
        Check("both jog commands ON, no module read: no jog", AxisMoment.MotionOf(null, null, true, true, null) == AxisMotion.Standing);
        Check("one jog command, no module read: manual", AxisMoment.MotionOf(null, null, false, true, null) == AxisMotion.Manual);
        Check("the module's home return with a jog command ON: homing", AxisMoment.MotionOf(7, true, true, false, null) == AxisMotion.Homing);
        var idle = new AxisMoment { Unit = "Robot X", Kind = AxisCodeKind.AxisError, Code = 0x1F4, Status = 0, Busy = false, JogBack = true, JogForward = true,
                                    Motion = AxisMoment.MotionOf(0, false, true, true, false), SeenRising = true };
        var idleText = Text(AxisSteps.Build(idle, "EN"));
        Check("  the page: standing still; the jog commands ON / ON - they did not move it (the motion module stood)",
              idle.JogIdle && idle.ModuleStood && idleText.Contains("standing still") && idleText.Contains("ON / ON - they did not move it (the motion module stood)") &&
              !idleText.Contains("moved by hand"), Regex.Match(idleText, "Jog.{0,90}").Value);
        var moving = new AxisMoment { Status = 8, Busy = true, JogBack = true, JogForward = true };
        Check("  both ON while it positioned: they did not move it - but not 'the module stood'", moving.JogIdle && !moving.ModuleStood);
        Check("  one held while it jogged: no note", !new AxisMoment { Status = 3, Busy = true, JogForward = true }.JogIdle);

        Console.WriteLine("--- the kind of fault a code is ---");
        Check("50.1 / 51.1 / 52.1 / E1.1: overload", new[] { 0x501, 0x511, 0x521, 0xE11 }.All(c => AxisSteps.Classify(AxisCodeKind.ServoAlarm, c) == AxisFault.Overload));
        Check("69.1 software limit, 69.3 stroke end", AxisSteps.Classify(AxisCodeKind.ServoAlarm, 0x691) == AxisFault.SoftLimit &&
                                                      AxisSteps.Classify(AxisCodeKind.ServoAlarm, 0x693) == AxisFault.HardLimit);
        Check("1A18H / 1993H software stroke limit, 1904H hardware stroke limit",
              AxisSteps.Classify(AxisCodeKind.AxisError, 0x1A18) == AxisFault.SoftLimit && AxisSteps.Classify(AxisCodeKind.AxisError, 0x1993) == AxisFault.SoftLimit &&
              AxisSteps.Classify(AxisCodeKind.AxisError, 0x1904) == AxisFault.HardLimit);
        Check("1941H dog detection timing: the reference sensor", AxisSteps.Classify(AxisCodeKind.AxisError, 0x1941) == AxisFault.HomeSensor);
        Check("16.3 encoder communication, 25.1 absolute position erased", AxisSteps.Classify(AxisCodeKind.ServoAlarm, 0x163) == AxisFault.Encoder &&
                                                                          AxisSteps.Classify(AxisCodeKind.ServoAlarm, 0x251) == AxisFault.AbsoluteLost);

        Console.WriteLine("--- the reference sensor on FA2010's drawings ---");
        Func<string, string, string> refOf = (addr, name) => { var p = AxisSteps.FindReferenceSensor(1, addr, name); return p == null ? "-" : p.Tag + "@" + p.Address; };
        Check("Robot X (the page reads M330, not on the drawings): by the drawing's words - SN.1 at X100", refOf("M330", "Robot X") == "SN.1@X100", refOf("M330", "Robot X"));
        Check("Milling 1X: SN.4 at X103 (not the 2nd milling's, not the mullion's)", refOf("M337", "Milling 1X") == "SN.4@X103", refOf("M337", "Milling 1X"));
        Check("Milling 2 Angle: SN.11 at X112", refOf("M334", "Milling 2 Angle") == "SN.11@X112", refOf("M334", "Milling 2 Angle"));
        Check("Cutting Angle: 'Cutting A Axis' on the drawing - SN.12 at X113", refOf("M339", "Cutting Angle") == "SN.12@X113", refOf("M339", "Cutting Angle"));
        Check("V Cutting Y reads M257: X115's relay - SN.14", refOf("M257", "V Cutting Y") == "SN.14@X115", refOf("M257", "V Cutting Y"));
        Check("Exit X: the exit's only reference sensor - SN.17 at X116", refOf("M343", "Exit X") == "SN.17@X116", refOf("M343", "Exit X"));
        Check("a name the drawings do not know: none (no guess)", refOf("M999", "Loader Q") == "-", refOf("M999", "Loader Q"));

        Console.WriteLine("--- the watch from the Axis Settings row ---");
        AlarmHelpLinks.DiscoverAll();
        var form = new Form { ClientSize = new Size(900, 300) };
        form.Controls.Add(Row(1, "Robot X", "W0", "W0B", "W4", "M604", "M605", "D314", "D316", "M330"));
        form.Controls.Add(Row(2, "Robot Y", "W10", "W1B", "W14", "M614", "M615", "D324", "D326", "M341"));
        form.Controls.Add(Row(3, "Robot Z", "W20", "W2B", "W24", "M624", "M625", "D334", "D336", "M342", vertical: true));
        AlarmHelpLinks.Harvest(form);
        var x = Watch("W0B");
        Check("the position (two words), its limits, the jog buttons, the reference sensor and its side, the home request",
              x.Position == "W0" && x.PositionDouble && x.MinLimit == "D314" && x.MaxLimit == "D316" && x.JogBack == "M604" && x.JogForward == "M605" &&
              x.RefSensor == "M330" && x.RefForward && x.HomeRequest == "M121" && x.Divisor == 10000 && x.Decimals == 4 && x.Suffix == "mm");   // as the page shows it
        Check("the motion module's state (Md.26) and BUSY (Md.141, its bit)", x.Status == "U1\\G2409" && x.Busy == "U1\\G31501" && x.BusyBit == 0 &&
              Watch("W1B").Status == "U1\\G2509" && Watch("W1B").BusyBit == 1, x.Status + " " + x.Busy + " " + x.BusyBit);
        Check("the machine's running and manual mode conditions", x.Running != null && x.Running.Address == "D200" && x.ManualMode != null && x.ManualMode.Address == "M100");
        Check("only a real vertical axis is one for the brake", Watch("W2B").Vertical && !x.Vertical);
        var reads = AlarmGeneration.Addresses().Where(kv => kv.Key == 1).Select(kv => kv.Value).ToList();
        Check("the scan reads them all (each word of a 32-bit value)",
              new[] { "W0", "W1", "D314", "D315", "D316", "D317", "M604", "M605", "M330", "M121", "U1\\G2409", "U1\\G31501", "D200", "D201", "M100" }.All(reads.Contains),
              string.Join(",", new[] { "W0", "W1", "D314", "D315", "M330", "U1\\G2409", "D200", "D201" }.Where(a => !reads.Contains(a))));

        Console.WriteLine("--- 1. homing: moved, then got stuck - 50.1: the reference sensor ---");
        mem["D314"] = 0; mem["D315"] = 0;
        Pos("D316", 2500);
        Pos("W0", 500); mem["U1\\G2409"] = 0;
        Scan(3);
        mem["U1\\G2409"] = 7; Busy(0, true); mem["M121"] = 1;
        Scan();
        for (int i = 1; i <= 10; i++) { Pos("W0", 500 + 10 * i); Scan(); }
        Scan(8);                                                   // stood still 0.8 s at 600 mm
        mem["W0B"] = 0x501; mem["U1\\G2409"] = -1; Busy(0, false);
        Scan();
        var item = x.RaisedServo;
        var m = item != null ? item.AxisMoment : null;
        Check("the alarm carries the moment", item != null && m != null);
        Check("  homing; it moved forward from 500 to 600 mm, then stood still about 0.8 s",
              m != null && m.Motion == AxisMotion.Homing && m.Moved && m.Direction == 1 && m.Stuck && m.StuckMs >= 700 &&
              Math.Abs(m.Position.Value - 600) < 1e-6 && Math.Abs(m.PositionStart.Value - 500) < 1e-6 && m.SeenRising,
              m == null ? "" : m.Motion + " moved=" + m.Moved + " stuck=" + m.Stuck + " " + m.StuckMs + "ms pos=" + m.Position + " from " + m.PositionStart);
        Check("  read the scan before: Home return (7), BUSY ON - not the error state of the code's scan",
              m != null && m.Status == 7 && m.Busy == true, m == null ? "" : m.StatusText);
        Check("  the reference sensor SN.1 (X100) from the drawings, OFF at the alarm",
              m != null && m.RefPoint != null && m.RefPoint.Address == "X100" && m.RefOn == false);
        Check("  the hint: stuck while homing; the reference sensor checked first",
              m != null && AxisSteps.HintOf(m) == "HomeStuck" && AxisSteps.StepsOf(m).FirstOrDefault() == "RefSensor",
              m == null ? "" : AxisSteps.HintOf(m) + ": " + string.Join(",", AxisSteps.StepsOf(m)));
        var docs = AlarmHelpLinks.DocumentsFor(item);
        Check("the alarm's help: the moment's page first, the code's entry (50.1) second",
              docs.Count >= 2 && docs[0].AxisMoment == m && docs[0].StepByStep && docs[1].Code == "50.1",
              string.Join(" | ", docs.Select(d => d.Title + ":" + d.Code)));
        Check("  titled with the axis and what it was doing", docs.Count > 0 && docs[0].Title.Contains("Robot X") && docs[0].Title.Contains("homing"),
              docs.Count > 0 ? docs[0].Title : "");
        // 17 October: "I don't see a hint according to the error moment state" - on the code's own entry too
        var entryPath = docs.Count > 1 ? TC.ExtractSection(docs[1]) : "";
        var entry = File.Exists(entryPath) ? Text(File.ReadAllText(entryPath)) : "";
        Check("the code's own entry (50.1) carries the moment's hint at its top: homing, where to start, the sensor, a link to the steps",
              docs.Count > 1 && docs[1].MomentHint == m && entry.Contains("When the code came") && entry.Contains("Robot X was homing") &&
              entry.Contains("Where to start:") && entry.Contains("never seen") && entry.Contains("Reference sensor: SN.1 at X100") &&
              entry.Contains("Step by step for this moment") && entry.IndexOf("When the code came") < entry.IndexOf("Thermal overload error 1"),
              entry.Length > 600 ? entry.Substring(0, 600) : entry);
        var link = System.Text.RegularExpressions.Regex.Match(File.Exists(entryPath) ? File.ReadAllText(entryPath) : "", "href=\"(file:[^\"]+Steps_Axis[^\"]+)\"");
        Check("  the link opens the step-by-step page", link.Success && File.Exists(new Uri(link.Groups[1].Value).LocalPath), link.Success ? link.Groups[1].Value : "no link");
        var path = docs.Count > 0 ? TC.ExtractSection(docs[0]) : "";
        var html = File.Exists(path) ? File.ReadAllText(path) : "";
        var text = Text(html);
        Check("  the page is written from the template, nothing left unfilled", html.Contains("<html") && !html.Contains("{{") && !html.Contains("<!--def"),
              path);
        Check("  what the HMI saw: homing, Home return (7), 600 mm from 500 mm, stood still about 0.8 s, the sensor OFF",
              text.Contains("homing (going to its reference)") && text.Contains("Home return (7)") && text.Contains("600.0000 mm (started at 500.0000 mm)") &&
              Regex.IsMatch(text, @"Stood still 0\.[89] s") && Regex.IsMatch(text, @"Reference sensor SN\.1 OFF"), text.Length > 900 ? text.Substring(0, 900) : text);
        Check("  the hint: it was never seen, ran on into the end; the reference sensor is the first suspect",
              text.Contains("never seen") && text.Contains("The reference sensor is the first suspect"));
        var titles = Titles(html);
        Check("  the checks: the sensor, the homing direction, the mechanics",
              titles.Count == 3 && titles[0].Contains("SN.1") && titles[1].Contains("towards its sensor") && titles[2].Contains("free to move"),
              string.Join(" | ", titles));
        Check("  the drawing's details: X100 on U9 FX5-16EX/ES terminal X00, wire 40, sheet 15.1",
              text.Contains("X100 - U9 FX5-16EX/ES, terminal X00") && text.Contains("Wire 40") && text.Contains("sheet 15.1"), "");
        Check("  the PLC's internal relay (M330) is not shown to a maintenance user", !text.Contains("M330"));
        SecurityManager.Login("a", SecurityLevel.Admin);
        var adminText = Text(File.ReadAllText(TC.ExtractSection(docs[0])));
        Check("  an Admin sees where the page reads it (M330)", adminText.Contains("The page reads it at M330"));
        SecurityManager.Login("m", SecurityLevel.Maintenance);
        Check("the row's code cell / '?' finds the same moment while the code is shown",
              AlarmGeneration.AxisStepsForCode(1, "W0B", 0x501) != null && AlarmGeneration.AxisStepsForCode(1, "W0B", 0x501).AxisMoment == m &&
              AlarmGeneration.AxisStepsForCode(1, "W0B", 0x163) == null);
        var badge = new AlarmHelpBadge { PlcNumber = 1, Address = "M891" };
        badge.SetCodeWords("W0B", "W4", "W5");
        badge.UpdateState(Read, c => true);
        var bd = badge.Documents();
        Check("  the axis's '?': the moment first, then 50.1's entry", bd.Count == 2 && bd[0].AxisMoment == m && bd[1].Code == "50.1",
              string.Join(" | ", bd.Select(d => d.Title)));
        var prompt = AlarmAiPrompt.Build(item, null, docs);
        Check("the AI prompt tells the moment: homing, stood still, the sensor and its input",
              prompt.Contains("What the axis was doing when Servo alarm 50.1") && prompt.Contains(": homing") && prompt.Contains("STOOD STILL") &&
              prompt.Contains("Reference (home) sensor SN.1 at X100"), "");
        mem["W0B"] = 0; mem["U1\\G2409"] = 0; mem["M121"] = 0;
        Scan();
        Check("the code gone: no moment any more", x.RaisedServo == null && x.ServoMoment == null && AlarmGeneration.AxisStepsForCode(1, "W0B", 0x501) == null);

        Console.WriteLine("--- 1b. homing the wrong way: the homing direction first ---");
        Pos("W0", 800); Scan(2);
        mem["U1\\G2409"] = 7; Busy(0, true); Scan();
        for (int i = 1; i <= 6; i++) { Pos("W0", 800 - 20 * i); Scan(); }
        Scan(7);
        mem["W0B"] = 0x501; Busy(0, false); Scan();
        m = x.RaisedServo.AxisMoment;
        Check("moved back (away from the forward sensor), stuck: the homing direction, then the sensor",
              m.Direction == -1 && m.AwayFromReference && AxisSteps.StepsOf(m).Take(2).SequenceEqual(new[] { "HomeDirection", "RefSensor" }),
              string.Join(",", AxisSteps.StepsOf(m)));
        Check("  the hint says so", Text(AxisSteps.Build(m, "EN")).Contains("moved away from the sensor's side"));
        mem["W0B"] = 0; mem["U1\\G2409"] = 0; Scan();

        Console.WriteLine("--- 2. jogged into its end (52.1): a mechanical crash or the limits ---");
        var y = Watch("W1B");
        Pos("D324", 0); Pos("D326", 1990);
        Pos("W10", 1000); Scan(2);
        mem["M615"] = 1; mem["U1\\G2509"] = 3; Busy(1, true); Scan();
        for (int i = 1; i <= 9; i++) { Pos("W10", 1000 + 110 * i); Scan(); }      // 1990 mm: its maximum
        Scan(6);
        mem["W1B"] = 0x521; Scan();
        m = y.RaisedServo != null ? y.RaisedServo.AxisMoment : null;
        Check("moved by hand (jog +), stood still at its maximum limit",
              m != null && m.Motion == AxisMotion.Manual && m.JogForward == true && m.Stuck && m.AtMax && !m.AtMin,
              m == null ? "" : m.Motion + " max=" + m.MaxLimit + " pos=" + m.Position);
        Check("  the hint: a crash or the limits; checks: the mechanics, then the limits (no reference sensor)",
              m != null && AxisSteps.HintOf(m) == "ManualCrash" && AxisSteps.StepsOf(m).SequenceEqual(new[] { "Mechanics", "Limits" }),
              m == null ? "" : string.Join(",", AxisSteps.StepsOf(m)));
        text = m == null ? "" : Text(AxisSteps.Build(m, "EN"));
        Check("  the page: jogged forward (+), at its maximum limit (1990 mm), the limits 0 ... 1990 mm",
              text.Contains("jogged forward (+)") && text.Contains("maximum limit (1990.0000 mm)") && text.Contains("minimum 0.0000 mm, maximum 1990.0000 mm"),
              text.Length > 700 ? text.Substring(0, 700) : text);
        Check("  no reference sensor step", !Titles(AxisSteps.Build(m, "EN")).Any(t => t.Contains("switch")));
        mem["W1B"] = 0; mem["M615"] = 0; mem["U1\\G2509"] = 0; Busy(1, false); Scan();

        Console.WriteLine("--- 3. in the automatic cycle (50.1 while still moving) ---");
        var z = Watch("W2B");
        Pos("D334", 0); Pos("D336", 400);
        Pos("W20", 100); Scan(2);
        mem["D200"] = 5; mem["U1\\G2609"] = 8; Busy(2, true); Scan();
        for (int i = 1; i <= 5; i++) { Pos("W20", 100 + 10 * i); Scan(); }
        mem["W2B"] = 0x501; Pos("W20", 160); Scan();
        m = z.RaisedServo.AxisMoment;
        Check("automatic, still moving at the code: not stuck", m.Motion == AxisMotion.Automatic && m.Moved && !m.Stuck, m.Motion + " stuck=" + m.Stuck);
        Check("  checks: the mechanics, the limits, the speed and acceleration - and its brake? no, it was moving",
              AxisSteps.HintOf(m) == "AutoCrash" && AxisSteps.StepsOf(m).SequenceEqual(new[] { "Mechanics", "Limits", "Dynamics" }),
              string.Join(",", AxisSteps.StepsOf(m)));
        Check("  the page says the automatic cycle", Text(AxisSteps.Build(m, "EN")).Contains("moving in the automatic cycle"));
        mem["W2B"] = 0; mem["D200"] = 0; mem["U1\\G2609"] = 0; Busy(2, false); Scan(3);

        Console.WriteLine("--- 4. a vertical axis overloading while standing: its brake ---");
        mem["W2B"] = 0x511; Scan();
        m = z.RaisedServo.AxisMoment;
        Check("standing still, vertical: the brake first", m.Motion == AxisMotion.Standing && AxisSteps.HintOf(m) == "StandingVertical" &&
              AxisSteps.StepsOf(m).First() == "Brake", m.Motion + " " + AxisSteps.HintOf(m));
        Check("  the safety: support the load", Text(AxisSteps.Build(m, "EN")).Contains("support the load"));
        mem["W2B"] = 0; Scan();
        // 19 October: "only create the brake part for the axis with a brake - the up / down arrows"
        mem["W0B"] = 0x511; Scan();
        m = x.RaisedServo.AxisMoment;
        var steps4 = AxisSteps.StepsOf(m);
        Check("  a horizontal axis (Robot X) standing still: no brake check, nor a brake in the hint",
              m.Motion == AxisMotion.Standing && !m.Vertical && !steps4.Contains("Brake") && steps4.Contains("Mechanics") &&
              !Text(AxisSteps.Build(m, "EN")).ToLowerInvariant().Contains("brake drag") && !Titles(AxisSteps.Build(m, "EN")).Any(t => t.Contains("brake")),
              string.Join(",", steps4));
        mem["W0B"] = 0; Scan();
        foreach (var motion in new[] { AxisMotion.Homing, AxisMotion.Standing, AxisMotion.Unknown, AxisMotion.Manual })
        {
            var h = new AxisMoment { Kind = AxisCodeKind.ServoAlarm, Code = 0x501, Motion = motion, Position = 10, MovingMs = 900 };
            var v = new AxisMoment { Kind = AxisCodeKind.ServoAlarm, Code = 0x501, Motion = motion, Position = 10, MovingMs = 900, Vertical = true };
            Check("  " + AxisSteps.HintOf(h) + ": the brake for a vertical axis only",
                  !AxisSteps.StepsOf(h).Contains("Brake") && AxisSteps.StepsOf(v).Contains("Brake"),
                  string.Join(",", AxisSteps.StepsOf(h)) + " | " + string.Join(",", AxisSteps.StepsOf(v)));
        }

        Console.WriteLine("--- 5. a software stroke limit in the cycle (1A18H) ---");
        mem["D200"] = 5; mem["U1\\G2509"] = 8; Busy(1, true); Scan();
        Pos("W10", 1500); Scan();
        mem["W14"] = 0x1A18; Scan();
        m = y.RaisedAxis.AxisMoment;
        Check("the axis error carries its moment too: automatic, a target outside the limits",
              m != null && m.Kind == AxisCodeKind.AxisError && m.Motion == AxisMotion.Automatic && AxisSteps.HintOf(m) == "SoftLimit" &&
              Text(AxisSteps.Build(m, "EN")).Contains("A target of the cycle is outside the limits"));
        var ydocs = AlarmHelpLinks.DocumentsFor(y.RaisedAxis);
        Check("  its help: the moment, then 1A18H's entry", ydocs.Count >= 2 && ydocs[0].AxisMoment == m && ydocs[1].Code == "1A18",
              string.Join(" | ", ydocs.Select(d => d.Title + ":" + d.Code)));
        mem["W14"] = 0; mem["D200"] = 0; mem["U1\\G2509"] = 0; Busy(1, false); Scan();

        Console.WriteLine("--- 5b. the Axis page's limits 0 and 0: the motion module's own (19 October) ---");
        // "the Axis page shows 0 for D312 / D314 too": the module's software stroke limits are read as well
        Check("the watch reads the module's Pr.12 / Pr.13 (two words each) and its unit Pr.1",
              x.SoftUpper == "U1\\G18" && x.SoftLower == "U1\\G20" && x.UnitWord == "U1\\G0" && Watch("W1B").SoftUpper == "U1\\G168" &&
              new[] { "U1\\G18", "U1\\G19", "U1\\G20", "U1\\G21", "U1\\G0" }.All(AlarmGeneration.Addresses().Where(kv => kv.Key == 1).Select(kv => kv.Value).Contains),
              x.SoftUpper + " " + x.SoftLower + " " + x.UnitWord + " " + Watch("W1B").SoftUpper);
        Pos("D314", 0); Pos("D316", 0);
        Pos("U1\\G18", 6500); Pos("U1\\G20", -5); mem["U1\\G0"] = 0;          // 0.1 um: the same 10 000 a millimetre
        mem["D200"] = 0; mem["M100"] = 0; mem["U1\\G2409"] = 8; Busy(0, true); Pos("W0", 6400); Scan();
        for (int i = 1; i <= 5; i++) { Pos("W0", 6400 + 20 * i); Scan(); }
        Scan(7);
        mem["W4"] = 0x1A18; mem["U1\\G2409"] = -1; Busy(0, false); Scan();
        m = x.RaisedAxis != null ? x.RaisedAxis.AxisMoment : null;
        Check("the moment: the page's limits 0 ... 0 (not set), the module's -5 ... 6500 mm",
              m != null && m.MinLimit == 0 && m.MaxLimit == 0 && m.PageLimitsUnset && m.ModuleMin == -5 && m.ModuleMax == 6500 && m.ModuleLimitsOn,
              m == null ? "" : m.MinLimit + " " + m.MaxLimit + " | " + m.ModuleMin + " " + m.ModuleMax);
        text = m == null ? "" : Text(AxisSteps.Build(m, "EN"));
        Check("  the page shows both, the hint the module's (it stops the axis there)",
              text.Contains("Limits ( Axis page) 0.0000 mm … 0.0000 mm - equal: not set".Replace("…", "&hellip;")) &&
              text.Contains("Software stroke limits (motion module) -5.0000 mm &hellip; 6500.0000 mm") &&
              text.Contains("(limits -5.0000 mm &hellip; 6500.0000 mm)"), Regex.Match(text, "Limits.{0,260}").Value);
        Check("  the limits check names the module's limits, on the Axis Monitor",
              text.Contains("software stroke limits (Pr.13 / Pr.12, on the Axis Monitor ): -5.0000 mm &hellip; 6500.0000 mm"), Regex.Match(text, "motion module's own.{0,160}").Value);
        mem["W4"] = 0; mem["U1\\G2409"] = 0; mem["M100"] = 1; Scan(2);
        Pos("U1\\G18", 0); Pos("U1\\G20", 0);
        var off = new AxisMoment { Kind = AxisCodeKind.AxisError, Code = 0x1A18, MinLimit = 0, MaxLimit = 0, ModuleMin = 0, ModuleMax = 0, Position = 10, Decimals = 1, Suffix = "mm",
                                   Motion = AxisMotion.Manual, SeenRising = true };
        var offText = Text(AxisSteps.Build(off, "EN"));
        Check("  the module's limits equal: off in the module - no 'at its limit' from 0 ... 0",
              off.ModuleLimitsOff && !off.AtMin && !off.AtMax && offText.Contains("equal: off in the module") && offText.Contains("software stroke limit is off"),
              Regex.Match(offText, "Software stroke.{0,120}").Value);
        Pos("D314", 0); Pos("D316", 2500);

        Console.WriteLine("--- 6. a code already there at the first scan ---");
        AlarmGeneration.ResetForTests();
        var form2 = new Form();
        form2.Controls.Add(Row(1, "Robot X", "W0", "W0B", "W4", "M604", "M605", "D314", "D316", "M330"));
        AlarmHelpLinks.Harvest(form2);
        mem["W0B"] = 0x163; Scan();
        m = Watch("W0B").RaisedServo.AxisMoment;
        Check("read from just after it, and the page says so", m != null && !m.SeenRising &&
              Text(AxisSteps.Build(m, "EN")).Contains("already there when the HMI started reading"));
        Check("  16.3: the encoder cable", AxisSteps.HintOf(m) == "Encoder" && AxisSteps.StepsOf(m).SequenceEqual(new[] { "EncoderCable" }));

        Console.WriteLine("--- the axis alarms switched off: the help still knows the moment (17 October) ---");
        AlarmGeneration.Save("", new AlarmGeneration.Settings { Enabled = false, AxisAlarms = false });
        var q = Watch("W0B");
        mem["W0B"] = 0; Scan(2);
        Pos("W0", 300); mem["U1\\G2409"] = 7; Busy(0, true); mem["M121"] = 1; Scan();
        for (int i = 1; i <= 5; i++) { Pos("W0", 300 + 10 * i); Scan(); }
        Scan(7);
        mem["W0B"] = 0x521; Busy(0, false); Scan();
        var qm = AlarmGeneration.MomentForCode(1, "W0B", 0x521);
        Check("no alarm raised by the HMI, the moment taken all the same: homing, stuck", q.RaisedServo == null && qm != null && qm.Motion == AxisMotion.Homing && qm.Stuck,
              qm == null ? "no moment" : qm.Motion + " stuck=" + qm.Stuck);
        AlarmHelpLinks.ReadCodeNow = (st, a) => Read(a);
        var plcDocs = AlarmHelpLinks.DocumentsFor(1, "M891", null);
        Check("  the PLC's alarm (the error bit's help): the moment's page first, then 52.1's entry with its hint",
              plcDocs.Count >= 2 && plcDocs[0].AxisMoment == qm && plcDocs[1].Code == "52.1" && plcDocs[1].MomentHint == qm,
              string.Join(" | ", plcDocs.Select(d => d.Title + ":" + d.Code)));
        var qb = new AlarmHelpBadge { PlcNumber = 1, Address = "M891" };
        qb.SetCodeWords("W0B", "W4", "W5");
        qb.UpdateState(Read, c => true);
        var qd = qb.Documents();
        Check("  the axis's '?': the same", qd.Count == 2 && qd[0].AxisMoment == qm && qd[1].MomentHint == qm, string.Join(" | ", qd.Select(d => d.Title)));
        AlarmGeneration.Save("", new AlarmGeneration.Settings { Enabled = false, AxisAlarms = true });
        Scan();
        Check("  switched on again: the code shown now raised, with that moment", q.RaisedServo != null && q.RaisedServo.AxisMoment == qm);
        mem["W0B"] = 0; mem["U1\\G2409"] = 0; mem["M121"] = 0; Scan();

        Console.WriteLine("--- a Help folder without the template: the program's own copy ---");
        var helpWas = TC.HelpFolder;
        var emptyHelp = Path.Combine(Path.GetTempPath(), "nohelp_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(emptyHelp);
        TC.HelpFolder = emptyHelp;
        var builtIn = AxisSteps.TemplatePath("EN");
        Check("the template found all the same (built into the program)", builtIn.Length > 0 && File.Exists(builtIn) && !builtIn.StartsWith(emptyHelp), builtIn);
        Check("  the page made from it", Text(AxisSteps.Build(qm, "EN")).Contains("Where to start"));
        TC.HelpFolder = helpWas;

        Console.WriteLine("--- the template ---");
        var tpl = TC.LoadTemplate(AxisSteps.TemplatePath("EN"));
        var hints = new[] { "HomeStuck", "HomeNoMove", "HomeMoving", "ManualCrash", "AutoCrash", "StandingVertical", "Standing", "Overload", "SoftLimitHoming",
                            "SoftLimit", "HardLimit", "HomeSensor", "HomeFirst", "HomeAtHome", "HomeParameters", "AbsoluteLost", "Encoder", "Supply", "Other" };
        var steps = new[] { "RefSensor", "HomeDirection", "Mechanics", "Brake", "Limits", "Dynamics", "LimitSwitch", "HomeAgain", "Parameters", "Battery", "EncoderCable", "Supply" };
        Check("every hint and check the program can pick has its phrase and caption",
              tpl != null && hints.All(h => tpl.HasPhrase("H_" + h)) && steps.All(s => tpl.HasPhrase("S_" + s) && tpl.HasPhrase("C_" + s)),
              tpl == null ? "no template" : string.Join(",", hints.Where(h => !tpl.HasPhrase("H_" + h)).Concat(steps.Where(s => !tpl.HasPhrase("S_" + s)))));
        Check("  the motions' words too", tpl != null && Enum.GetNames(typeof(AxisMotion)).All(n => tpl.HasPhrase("M_" + n)));
        Check("  a language without its own copy uses English", AxisSteps.TemplatePath("RU") == AxisSteps.TemplatePath("EN"));

        Console.WriteLine(fail == 0 ? "AXIS MOMENT HELP ALL PASS" : "AXIS MOMENT HELP FAILURES: " + fail);
    }
}
