using System;
using System.IO;
using System.Linq;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Drivers.Simulation;

// 12 October: "M216.2 will mean that PLC number is 2 ... good suggestion, create it this way" -
// a property's device on another controller, written "X12@2" (a dot is part of Siemens' M10.3
// and DB10.DBX4.2 and of Mitsubishi's D100.5). Read in one place (PlcAddressHelper); the drivers
// PlcManager hands out send it to that controller, the wiring table finds it there.
class SA {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }

    static string Split(string a) {
        string d; int st;
        return PlcAddressHelper.TrySplitStation(a, out d, out st) ? d + "|" + st : "-";
    }

    static void Main() {
        AuditLogger.LogDirectory = Path.Combine(Path.GetTempPath(), "sa_" + Guid.NewGuid().ToString("N"));

        Console.WriteLine("--- the address ---");
        Check("X12@2: X12 of PLC 2", Split("X12@2") == "X12|2", Split("X12@2"));
        Check("  spaces and lower case kept to the device", Split(" x12 @ 2 ") == "x12|2", Split(" x12 @ 2 "));
        Check("  a module's word: U1\\G2400@2", Split("U1\\G2400@2") == "U1\\G2400|2");
        Check("a dot is NOT a controller: M10.3, DB10.DBX4.2, D100.5 stay as they are",
              Split("M10.3") == "-" && Split("DB10.DBX4.2") == "-" && Split("D100.5") == "-" && Split("M216.2") == "-");
        Check("  nor an @ without a number, a 0, a device missing", Split("X12@") == "-" && Split("X12@0") == "-" && Split("@2") == "-" &&
              Split("X12@a") == "-" && Split("X12") == "-" && Split("") == "-" && Split(null) == "-");
        int st; string dev;
        PlcAddressHelper.Resolve(1, "X12@2", out st, out dev);
        Check("Resolve: its own controller", st == 2 && dev == "X12");
        PlcAddressHelper.Resolve(3, "M100", out st, out dev);
        Check("  else the control's", st == 3 && dev == "M100");
        Check("StationOf / DeviceOf", PlcAddressHelper.StationOf(1, "Y13@2") == 2 && PlcAddressHelper.StationOf(1, "Y13") == 1 &&
              PlcAddressHelper.DeviceOf("Y13@2") == "Y13" && PlcAddressHelper.DeviceOf("Y13") == "Y13");
        Check("WithStation: plain on its own controller, marked on another",
              PlcAddressHelper.WithStation("Y13", 2, 1) == "Y13@2" && PlcAddressHelper.WithStation("Y13", 1, 1) == "Y13");
        Check("the next word on the same controller: D100@2 -> D101@2, X17@2 -> X20@2 (octal), U1\\G2400@2 -> U1\\G2401@2",
              PlcAddressHelper.GetNextAddress("D100@2") == "D101@2" && PlcAddressHelper.GetNextAddress("X17@2") == "X20@2" &&
              PlcAddressHelper.OffsetAddress("U1\\G2400@2", 1) == "U1\\G2401@2",
              PlcAddressHelper.GetNextAddress("D100@2") + " " + PlcAddressHelper.GetNextAddress("X17@2"));
        Check("shown as 'X12 (PLC 2)' on a PLC 1 control, 'X12' on a PLC 2 one",
              PlcAddressHelper.Describe(1, "X12@2") == "X12 (PLC 2)" && PlcAddressHelper.Describe(2, "X12@2") == "X12" &&
              PlcAddressHelper.Describe(1, "M10.3") == "M10.3", PlcAddressHelper.Describe(1, "X12@2"));

        Console.WriteLine("--- the drivers: a PLC 1 driver sends it to PLC 2 ---");
        PlcManager.Instance.RegisterDriver(1, new SimulationDriver(1));
        PlcManager.Instance.RegisterDriver(2, new SimulationDriver(2));
        var one = PlcManager.Instance.GetDriverForMonitoring(1);
        var sim1 = (SimulationDriver)PlcManager.Unwrap(one);
        var sim2 = (SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(2));
        SecurityManager.Login("ali", SecurityLevel.Maintenance);
        int rc = PlcManager.Instance.GetDriverForWrite(1).Write("M5@2", 1);
        Check("written on PLC 2, not on PLC 1", rc == 0 && sim2.Read("M5") == 1 && sim1.Read("M5") == 0, "rc " + rc);
        PlcWriteLog.Flush();
        var last = PlcWriteLog.Read(DateTime.Now.AddMinutes(-1), DateTime.Now.AddMinutes(1)).LastOrDefault();
        Check("  in the write log as PLC 2's M5", last != null && last.Station == 2 && last.Device == "M5",
              last == null ? "none" : last.Station + " " + last.Device);
        sim2.Write("X12", 1);
        int v;
        Check("read through PLC 1's driver: Read, TryRead", one.Read("X12@2") == 1 && one.TryRead("X12@2", out v) && v == 1 && one.Read("X12") == 0);
        sim1.Write("D100", 11); sim2.Write("D100", 22); sim1.Write("D101", 33);
        int[] values;
        bool ok = one.TryReadMulti(new[] { "D100", "D100@2", "D101@1", "X12@2" }, out values);
        Check("  a batch of both: each from its own, in the order asked", ok && values.SequenceEqual(new[] { 11, 22, 33, 1 }),
              ok ? string.Join(",", values) : "failed");
        Check("  ReadMulti the same", one.ReadMulti(new[] { "D100@2", "D100" }).SequenceEqual(new[] { 22, 11 }));
        Check("  its own number is itself: M5@1 on PLC 1 is M5", one.Write("D7@1", 4) == 0 && sim1.Read("D7") == 4 && sim2.Read("D7") == 0);
        Check("  no driver below sees an '@'", !sim1.Dump().Keys.Concat(sim2.Dump().Keys).Any(k => k.Contains("@")),
              string.Join(" ", sim1.Dump().Keys.Concat(sim2.Dump().Keys).Where(k => k.Contains("@"))));
        Check("a controller not registered: read as nothing, a write refused",
              one.Read("M1@9") == 0 && !one.TryRead("M1@9", out v) && one.Write("M1@9", 1) == -1);
        PlcManager.Instance.SetPermissionOverride(1, true);
        PlcManager.Instance.SetPermissionOverride(2, false);
        var w1 = PlcManager.Instance.GetDriverForWrite(1);
        Check("  nor written without that controller's permission (PLC 1 allowed, PLC 2 not)",
              w1 != null && w1.Write("M6@2", 1) == -1 && sim2.Read("M6") == 0 && w1.Write("M6", 1) == 0 && sim1.Read("M6") == 1);
        PlcManager.Instance.ClearPermissionOverrides();

        Console.WriteLine("--- the wiring table: FA2010's PLC 2 points ---");
        WiringTable.Clear();
        int n = WiringTable.Load("Wiring.csv", local => local);
        var y13 = WiringTable.OutputFor(1, "M557");
        Check("M557 (PLC 1, Profile Recognition Back Horizontal back) drives Y13 of PLC 2", y13 != null && y13.Station == 2 && y13.Address == "Y13",
              y13 == null ? "none of " + n : y13.Station + " " + y13.Address);
        Check("  found as Y13@2 from PLC 1, as Y13 on PLC 2", WiringTable.Find(1, "Y13@2") == y13 && WiringTable.Find(2, "Y13") == y13);
        var own = WiringTable.Find(1, "Y13");
        Check("  PLC 1's own Y13 another point (Input Conveyor 7. Clamping)", own != null && own != y13 && own.Station == 1 && own.Text.Contains("7. Clamping"),
              own == null ? "none" : own.Text);
        Check("the reference sensors X12, X13, X14 on PLC 2", new[] { "X12", "X13", "X14" }.All(x => WiringTable.Find(1, x + "@2") != null &&
              WiringTable.Find(1, x + "@2").Text.Contains("Reference Sensor")));
        Check("the ten back moulds' twenty valves: Y0 - Y7, Y10 - Y17, Y20 - Y23, each with its PLC 1 bit",
              new[] { 542, 543, 544, 545, 548, 549, 550, 551, 554, 555, 556, 557, 560, 561, 562, 563, 566, 567, 568, 569 }
                  .Select(m => WiringTable.OutputFor(1, "M" + m)).All(p => p != null && p.Station == 2));
        Check("  and not the first project's X15, X16, Y24 - Y27", new[] { "X15", "X16", "Y24", "Y25", "Y26", "Y27" }.All(x => WiringTable.Find(2, x) == null));
        Check("  the front moulds' valves still PLC 1's (M540 -> Y131)", WiringTable.OutputFor(1, "M540") != null &&
              WiringTable.OutputFor(1, "M540").Station == 1 && WiringTable.OutputFor(1, "M540").Address == "Y131");

        Console.WriteLine("--- the alarms ---");
        AlarmManager.RegisterDefinition("SA1", "SA1", Kaban.Core.Models.AlarmSeverity.Fault, 2, "M915");
        Check("an error bit named M915@2 on a PLC 1 unit is PLC 2's alarm", AlarmManager.FindByStationAndAddress(1, "M915@2") != null &&
              AlarmManager.FindByStationAndAddress(1, "M915") == null);

        Console.WriteLine(fail == 0 ? "\nSTATION ADDRESS ALL PASS" : "\nSTATION ADDRESS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
