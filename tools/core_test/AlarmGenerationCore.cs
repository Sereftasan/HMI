using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

// THE ALARMS THE HMI RAISES ITSELF (1 October): a cylinder movement timed from
// the output that commands it and confirmed by its feedback within its delay -
// or its alarm with the signal that failed (M840.1 ...) - and an axis's alarm
// while its servo alarm or axis alarm word shows a code. Switched on per machine.
class AG {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static Dictionary<string, int> mem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    static int Read(string a) { int v; return mem.TryGetValue(a, out v) ? v : 0; }
    static DateTime t0 = new DateTime(2026, 10, 1, 8, 0, 0);
    static DateTime now = t0;
    // as the page scanner does: the HMI's own alarms first, then the PLC's bits
    static void Scan(double seconds = 0.1) { now = now.AddSeconds(seconds); AlarmGeneration.Evaluate(1, Read, now); AlarmManager.Evaluate(1, Read); }
    static void Run(double seconds) { for (double s = 0; s < seconds - 1e-9; s += 0.1) Scan(); }
    static List<AlarmItem> Gen() { return AlarmManager.ActiveAlarms.Where(a => a.Generated).ToList(); }
    static string Codes() { return string.Join(",", AlarmManager.ActiveAlarms.Select(a => a.ShownCode)); }

    static TC.MovementInfo.Device D(string a) { return string.IsNullOrEmpty(a) ? null : new TC.MovementInfo.Device { Address = a }; }
    // One movement as a unit's help describes it.
    static TC.MovementInfo Mv(string side, string cmd, string output, string otherCmd, string otherOutput, string error, string[] on, string[] off, string position = "") {
        var mv = new TC.MovementInfo { Station = 1, Unit = "Test unit", Side = side, Name = side == "A" ? "Back" : "Forward", OtherName = side == "A" ? "Forward" : "Back",
                                       Command = D(cmd), Output = D(output), OtherCommand = D(otherCmd), OtherOutput = D(otherOutput), Error = error ?? "", Position = position };
        foreach (var a in on) mv.Feedback.Add(D(a));
        foreach (var a in off) mv.OppositeFeedback.Add(D(a));
        return mv;
    }
    static AlarmGeneration.Cylinder Unit(int valve, int feedback, string errA, string errB, string[] lampsA, string[] lampsB,
                                         bool cmdA = true, bool cmdB = true, string position = "") {
        var a = Mv("A", cmdA ? "M10" : "", cmdA ? "Y10" : "", cmdB ? "M11" : "", cmdB ? "Y11" : "", errA, lampsA, lampsB, position);
        var b = Mv("B", cmdB ? "M11" : "", cmdB ? "Y11" : "", cmdA ? "M10" : "", cmdA ? "Y10" : "", errB, lampsB, lampsA, position);
        return AlarmGeneration.Build(1, "T", "Test unit", valve, feedback, a, b);
    }
    static void Fresh(AlarmGeneration.Cylinder c, params string[] on) {
        AlarmGeneration.ResetForTests();
        AlarmManager.ActiveAlarms.Clear();
        mem.Clear();
        foreach (var a in on) mem[a] = 1;
        now = t0;
        if (c != null) AlarmGeneration.Register(c);
        Scan();   // the first scan only learns the states
    }

    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "AlarmGenTest"); Directory.CreateDirectory(dir);
        AlarmGeneration.SettingsPath = Path.Combine(dir, "AlarmGeneration.cfg");
        if (File.Exists(AlarmGeneration.SettingsPath)) File.Delete(AlarmGeneration.SettingsPath);
        SecurityManager.Login("m", Kaban.Core.Enums.SecurityLevel.Maintenance);
        AlarmManager.RegisterDefinition("T.Back", "Unit did not go back", AlarmSeverity.Warning, plcNumber: 1, address: "M840");
        AlarmManager.RegisterDefinition("T.Fwd", "Unit did not go forward", AlarmSeverity.Fault, plcNumber: 1, address: "M841");
        AlarmManager.RegisterDefinition("T.Axis", "Axis 1 error", AlarmSeverity.Critical, plcNumber: 1, address: "M801");

        Console.WriteLine("--- switched on per machine ---");
        var c = Unit(3, 2, "M840", "M841", new[] { "X1" }, new[] { "X2" });
        Check("a 5/3 unit with a sensor at each end: both movements watched", c != null && c.Sides.Count == 2);
        Fresh(c);
        Check("off by default: nothing read for it", !AlarmGeneration.EnabledFor("T") && !AlarmGeneration.Addresses().Any());
        mem["Y10"] = 1; mem["X2"] = 1; Run(6);
        Check("  and nothing raised however long it takes", Gen().Count == 0);
        AlarmGeneration.Save("T", new AlarmGeneration.Settings { Enabled = true, DelaySeconds = 5 });
        var addrs = AlarmGeneration.Addresses().Select(k => k.Value).ToList();
        Check("on: the outputs and the sensors are read", new[] { "Y10", "Y11", "X1", "X2" }.All(addrs.Contains), string.Join(",", addrs));
        AlarmGeneration.ResetForTests();
        Check("  kept: read back after a restart", AlarmGeneration.EnabledFor("T") && AlarmGeneration.For("T").DelaySeconds == 5 &&
              File.ReadAllText(AlarmGeneration.SettingsPath).Contains("T.Enabled=1"));
        Check("  another machine stays off", !AlarmGeneration.EnabledFor("H500"));

        Console.WriteLine("--- 1: the start sensor stayed ON ---");
        Fresh(c, "Y10", "X2");
        Scan();
        Check("an output already ON when the HMI starts is not timed (it was commanded before)", !c.Sides[0].Timing);
        mem["Y10"] = 0; Scan(); mem["Y10"] = 1; Scan();
        Check("the output going ON starts the time", c.Sides[0].Timing);
        Run(4.7);
        Check("  nothing before the delay", Gen().Count == 0, Codes());
        Run(0.4);
        var g = Gen().FirstOrDefault();
        Check("after it: the movement's alarm, by its error bit and the failed signal: M840.1", g != null && g.ShownCode == "M840.1" && g.Address == "M840" && g.SubCode == "1", Codes());
        Check("  its text and what failed", g != null && g.Description.StartsWith("Unit did not go back - ") && g.Description.Contains("X2 stayed ON"), g == null ? "" : g.Description);
        Check("  the alarm's severity, its machine (for the filters) and the states when the time ran out",
              g != null && g.Severity == AlarmSeverity.Warning && g.AlarmCode.StartsWith("T.") && g.Snapshot["Y10"] == 1 && g.Snapshot["X2"] == 1 && g.Snapshot["X1"] == 0);
        Check("  in the history", AlarmManager.History.Contains(g));
        mem["M840"] = 1; Scan();
        Check("the PLC's own bit for it: not listed twice", AlarmManager.ActiveAlarms.Count == 1, Codes());
        Check("  asked by the unit (by any of its devices)", AlarmGeneration.ActiveFor(1, "A", "M999", "M840") == g && AlarmGeneration.ActiveFor(1, "B", "M840") == null);
        mem["X2"] = 0; mem["X1"] = 1; Scan();
        Check("it gets there after all: the HMI's alarm goes, the PLC's bit still on shows", Gen().Count == 0 && Codes() == "T.Back", Codes());
        mem["M840"] = 0; Scan();
        Check("  and goes with its bit", AlarmManager.ActiveAlarms.Count == 0);

        Console.WriteLine("--- 2, 3, 4 ---");
        Fresh(c);
        mem["X2"] = 1; Scan(); mem["Y10"] = 1; Scan(); mem["X2"] = 0; Run(5.2);
        Check("left its start, the target did not come ON: M840.2", Gen().Select(a => a.ShownCode).SingleOrDefault() == "M840.2", Codes());
        Fresh(c);
        mem["X2"] = 1; Scan(); mem["Y10"] = 1; Scan(); mem["X1"] = 1; Run(5.2);
        Check("the target came ON, the start stayed ON: M840.3", Gen().Select(a => a.ShownCode).SingleOrDefault() == "M840.3", Codes());
        mem["X2"] = 0; Scan();
        Check("  the start sensor goes OFF: confirmed, the alarm goes", Gen().Count == 0);
        Fresh(c);
        Scan(); mem["Y10"] = 1; mem["Y11"] = 1; Run(5.2);
        var both = Gen().Select(a => a.ShownCode).ToList();
        Check("both coils ON: M840.4 (and M841.4 for the other movement)", both.Contains("M840.4") && both.Contains("M841.4"), Codes());

        Console.WriteLine("--- the valve decides what commands and what gives up ---");
        Fresh(c);
        mem["X2"] = 1; Scan(); mem["Y10"] = 1; Run(2); mem["Y10"] = 0; Run(5);
        Check("5/3: the output let go of before it arrived is not a failure", Gen().Count == 0 && !c.Sides[0].Timing, Codes());
        var dbl = Unit(1, 2, "M840", "M841", new[] { "X1" }, new[] { "X2" });
        Fresh(dbl);
        mem["X2"] = 1; Scan(); mem["Y10"] = 1; Run(0.3); mem["Y10"] = 0; Run(5);
        Check("5/2 double: a short pulse - it goes on, and is timed on", Gen().Select(a => a.ShownCode).SingleOrDefault() == "M840.1", Codes());
        Fresh(dbl);
        mem["X2"] = 1; Scan(); mem["Y10"] = 1; Run(0.3); mem["Y10"] = 0; Run(1); mem["Y11"] = 1; Run(5);
        Check("  reversed by the other coil: not this movement's failure", !Gen().Any(a => a.ShownCode.StartsWith("M840")), Codes());

        var spring = Unit(2, 2, "M840", "M841", new[] { "X1" }, new[] { "X2" }, cmdA: false);
        Check("single solenoid, coil on B: B by Y11 ON, A (the spring's) by Y11 OFF",
              spring.Sides.Count == 2 && spring.Sides.Single(s => s.Side == "A").Drive == "Y11" && !spring.Sides.Single(s => s.Side == "A").DriveOn &&
              spring.Sides.Single(s => s.Side == "B").DriveOn);
        Fresh(spring);
        mem["X1"] = 1; Run(6);
        Check("  the spring's movement is not timed from the first scan (the coil was OFF before)", Gen().Count == 0);
        mem["Y11"] = 1; Scan(); mem["X1"] = 0; mem["X2"] = 1; Scan();
        mem["Y11"] = 0; Run(5.2);
        Check("  the coil's output going OFF times the spring's return: M840.1 when X2 stays ON", Gen().Select(a => a.ShownCode).SingleOrDefault() == "M840.1", Codes());
        Fresh(spring);
        mem["X1"] = 1; Scan(); mem["Y11"] = 1; Run(2); mem["Y11"] = 0; Run(5);
        Check("  the coil's movement given up when its output drops (the spring takes it back)", !Gen().Any(a => a.ShownCode.StartsWith("M841")), Codes());

        Console.WriteLine("--- the feedback decides what confirms ---");
        var single = Unit(3, 1, "M840", "M841", new string[0], new[] { "X5" });
        Fresh(single);
        Check("one sensor (at B): A is confirmed by it going OFF, B by it coming ON",
              single.Sides.Single(s => s.Side == "A").MustOn.Count == 0 && single.Sides.Single(s => s.Side == "A").MustOff.Single().Address == "X5" &&
              single.Sides.Single(s => s.Side == "B").MustOn.Single().Address == "X5");
        mem["X5"] = 1; Scan(); mem["Y10"] = 1; Run(1); mem["X5"] = 0; Run(5);
        Check("  away from it: the sensor OFF is enough", Gen().Count == 0, Codes());
        mem["Y10"] = 0; Scan(); mem["Y11"] = 1; Run(5.2);
        Check("  towards it, not seen: M841.2", Gen().Select(a => a.ShownCode).SingleOrDefault() == "M841.2", Codes());

        var clamp = Unit(3, 5, "M840", "M841", new[] { "X6" }, new[] { "X7" });
        Fresh(clamp);
        mem["X6"] = 1; Scan(); mem["Y11"] = 1; Scan(); mem["X6"] = 0; Run(5.2);
        Check("clamp: the back sensor OFF but the pressure switch not ON: M841.2", Gen().Select(a => a.ShownCode).SingleOrDefault() == "M841.2", Codes());
        mem["X7"] = 1; Scan();
        Check("  the switch ON: confirmed", Gen().Count == 0);
        mem["Y11"] = 0; Scan(); mem["Y10"] = 1; Scan(); mem["X6"] = 1; Run(5.2);
        Check("release: the back sensor ON but the switch still ON: M840.3", Gen().Select(a => a.ShownCode).SingleOrDefault() == "M840.3", Codes());

        var ana = Unit(3, 4, "M840", "M841", new string[0], new string[0], position: "D10");
        Fresh(ana);
        Check("analogue: both movements by the position", ana.Sides.All(s => s.ByPosition && s.Position == "D10"));
        mem["D10"] = 100; Scan(); mem["Y11"] = 1; Scan();
        for (int i = 1; i <= 10; i++) { mem["D10"] = 100 + i * 50; Scan(); }
        Run(1); Run(5);
        Check("  moved and settled: confirmed", Gen().Count == 0, Codes());
        mem["Y11"] = 0; Scan(); mem["Y10"] = 1; Run(5.2);
        Check("  did not change: M840.5", Gen().Select(a => a.ShownCode).SingleOrDefault() == "M840.5", Codes());
        Fresh(ana);
        mem["D10"] = 100; Scan(); mem["Y11"] = 1; Scan();
        for (int i = 1; i <= 52; i++) { mem["D10"] = 100 + i; Scan(); }
        Check("  still changing when the time ran out: M841.6", Gen().Select(a => a.ShownCode).SingleOrDefault() == "M841.6", Codes());

        var enc = Unit(3, 6, "M840", "M841", new[] { "X8" }, new string[0], position: "D12");
        Check("encoder: towards the reference by the sensor, away by the position (and the reference going OFF)",
              !enc.Sides.Single(s => s.Side == "A").ByPosition && enc.Sides.Single(s => s.Side == "B").ByPosition &&
              enc.Sides.Single(s => s.Side == "B").MustOff.Single().Address == "X8");
        Check("no sensor: nothing to watch", Unit(3, 7, "M840", "M841", new string[0], new string[0]) == null);
        var noErr = Unit(3, 2, "", "", new[] { "X1" }, new[] { "X2" });
        Check("no error bit: named by the command", noErr.Sides.Single(s => s.Side == "A").Code == "M10");

        Console.WriteLine("--- each alarm's own delay ---");
        AlarmManager.FindByCode("T.Back").GenerationDelay = 2;
        Fresh(c);
        mem["X2"] = 1; Scan(); mem["Y10"] = 1; Run(2.2);
        Check("2 s for M840 (the table), not the general 5", Gen().Select(a => a.ShownCode).SingleOrDefault() == "M840.1", Codes());
        AlarmManager.FindByCode("T.Back").GenerationDelay = null;
        AlarmManager.AcknowledgeAll();
        Run(6);
        Check("acknowledged: not raised again for the same movement", Gen().Count == 0);

        Console.WriteLine("--- the help page for it ---");
        Fresh(c);
        mem["X2"] = 1; Scan(); mem["Y10"] = 1; Run(5.2);
        g = Gen().Single();
        Check("which signal failed, as a number", AlarmGeneration.FailureOf(g) == 1);
        var mv = AlarmGeneration.MovementAt(g);
        Check("the movement with its states when the time ran out", mv != null && mv.Output.AtAlarm == 1 && mv.OppositeFeedback[0].AtAlarm == 1 && mv.Feedback[0].AtAlarm == 0);
        var page1 = TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.TwoMagnetic, 1);
        var all = TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.TwoMagnetic, 0);
        Func<string, List<string>> titles = html => Regex.Matches(html, "<h3>(.*?)</h3>").Cast<Match>().Select(m => Regex.Replace(m.Groups[1].Value, "<[^>]+>", "")).ToList();
        var t1 = titles(page1);
        Check("1: did it leave? then the card onwards, or that sensor - not the PLC's output, not the way, not the target sensor",
              t1[0].StartsWith("Has the cylinder left its start?") && !t1.Any(x => x.StartsWith("Does the PLC switch on")) &&
              t1.Any(x => x.StartsWith("Does the output card")) && t1.Any(x => x.Contains("at the other end gone OFF")) &&
              !t1.Any(x => x.Contains("stay ON the whole way")) && !t1.Any(x => x.Contains("see it?")) && titles(all).Count > t1.Count,
              string.Join(" | ", t1));
        Check("  'raised by the HMI', its code, where to start", page1.Contains("M840.1") && page1.Contains("raised by the HMI") && page1.Contains("X2</code>") &&
              Regex.IsMatch(page1, "Where to start:</b> The HMI timed"));
        var t4 = titles(TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.TwoMagnetic, 4));
        Check("4: the two outputs only", t4.Count == 3 && t4[0].StartsWith("Does the PLC switch on") && t4[1].StartsWith("Why does the PLC"), string.Join(" | ", t4));
        var t2 = titles(TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.TwoMagnetic, 2));
        Check("2: where is it? then the way, or the target sensor - not the valve (it left)",
              t2[0].StartsWith("Where is the cylinder now?") && t2.Any(x => x.Contains("stay ON the whole way")) && t2.Any(x => x.Contains("see it?")) &&
              !t2.Any(x => x.StartsWith("Does the output card")) && !t2.Any(x => x.Contains("at the other end gone OFF")), string.Join(" | ", t2));
        var t3 = titles(TC.BuildSteps(mv, TC.ValveType.ClosedCentre, TC.FeedbackType.TwoMagnetic, 3));
        Check("3: only the start sensor", t3.Count == 2 && t3[0].Contains("at the other end gone OFF"), string.Join(" | ", t3));
        Check("  no link to a step left out", !Regex.IsMatch(page1, "&rarr; \\.") && !page1.Contains("&rarr; </td>"));

        Console.WriteLine("--- an axis ---");
        Fresh(null);
        AlarmGeneration.Register(new AlarmGeneration.Axis { Station = 1, ModuleId = "T", Number = 1, Name = "X axis", ErrorAddress = "M801", ServoAlarm = "D100", AxisAlarm = "D101" });
        addrs = AlarmGeneration.Addresses().Select(k => k.Value).ToList();
        Check("its servo alarm and axis alarm words are read (not the warning)", addrs.Contains("D100") && addrs.Contains("D101") && addrs.Count == 2, string.Join(",", addrs));
        Scan();
        Check("  zero: nothing", Gen().Count == 0);
        mem["D100"] = 0x163; Scan();
        g = Gen().SingleOrDefault();
        Check("a servo alarm: its alarm at once, the code as the sub-number (M801.16.3)", g != null && g.ShownCode == "M801.16.3" && g.Severity == AlarmSeverity.Critical, Codes());
        mem["M801"] = 1; Scan();
        Check("  the PLC's bit is not listed beside it", AlarmManager.ActiveAlarms.Count == 1, Codes());
        mem["D101"] = 0x1904; Scan();
        Check("an axis alarm too: its own line (M801.1904H)", Gen().Select(a => a.ShownCode).OrderBy(x => x).SequenceEqual(new[] { "M801.16.3", "M801.1904H" }), Codes());
        mem["D100"] = 0x502; Scan();
        Check("the code changes: the line follows", Gen().Any(a => a.ShownCode == "M801.50.2") && !Gen().Any(a => a.ShownCode == "M801.16.3"), Codes());
        AlarmManager.AcknowledgeAll(); Scan();
        Check("acknowledged while the drive still shows it: back, like a PLC bit that is still on (and the bit still not beside it)",
              Gen().Count == 2 && AlarmManager.ActiveAlarms.Count == 2, Codes());
        mem["D100"] = 0; mem["D101"] = 0; Scan();
        Check("the codes clear: the lines go (the PLC bit shows on its own)", Gen().Count == 0 && Codes() == "T.Axis", Codes());
        AlarmGeneration.Save("T", new AlarmGeneration.Settings { Enabled = false, DelaySeconds = 5 });
        mem["D100"] = 0x163; Scan();
        Check("switched off: nothing raised", Gen().Count == 0);

        Console.WriteLine(fail == 0 ? "\nALARM GENERATION ALL PASS" : "\nALARM GENERATION " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
