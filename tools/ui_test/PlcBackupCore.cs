using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;

// 22 October: "The read button should read all the PLC data (and connected simple motion data when available)
// and save it with the machine name, PLC description, PLC serial number, user name, date and the user's read
// description ... Write is gonna open the read backup list and selection of the devices to be written to the PLC
// (and another column for the entry if the read device interval and write device interval is different, e.g
// writing to D2500 to D3499 of the read data from R1000 to R1999 if required (leaving this side empty is gonna
// mean that reading and writing interval is same))" - the engine under the two buttons.
class PBC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        d = d ?? "";
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }

    /// <summary>
    /// A CPU whose parameters give D 12000, M 8192, B 1024 - it refuses a block reaching beyond, as a real one does;
    /// it has no link special relays (SB), and knows the retentive timers by MX Component's names (SS, SC, SN).
    /// It can lose its link after so many block calls (DropAfter), and get it back after so many Connects.
    /// </summary>
    class Sized : IPlcDriver, IPlcBlockAccess {
        readonly Kaban.Drivers.Simulation.SimulationDriver _s;
        public Sized(Kaban.Drivers.Simulation.SimulationDriver s) { _s = s; }
        public int Asked, Blocks, Connects;
        public int DropAfter = -1, BackAfterConnects = 2;
        bool _up = true;
        bool Inside(string a, long devices) {
            string p; long n;
            if (!PlcAddressHelper.TrySplit(a, out p, out n)) return false;
            if (p == "SB" || p == "STS" || p == "STC" || p == "STN") return false;
            var k = PlcBackup.KindOf(p);
            int size = p == "D" ? 12000 : p == "M" ? 8192 : p == "B" ? 1024 : k != null ? k.Default : 100000;
            return n + devices <= size;
        }
        public bool IsConnected { get { return _up; } }
        public bool IsHealthy { get { return _up; } }
        public bool Connect() { if (!_up && ++Connects >= BackAfterConnects && BackAfterConnects > 0) { _up = true; Connects = 0; } return _up; }
        public void Disconnect() { }
        public int Read(string a) { return _s.Read(a); }
        public int[] ReadMulti(string[] a) { return _s.ReadMulti(a); }
        public bool TryRead(string a, out int v) { Asked++; v = 0; return _up && Inside(a, 1) && _s.TryRead(a, out v); }
        public bool TryReadMulti(string[] a, out int[] v) { return _s.TryReadMulti(a, out v); }
        public int Write(string a, int v) { return _up ? _s.Write(a, v) : -1; }
        public bool RefusesOutOfRange { get { return true; } }
        bool Call() { Blocks++; if (DropAfter >= 0 && Blocks > DropAfter && _up) { _up = false; DropAfter = -1; } return _up; }
        public bool TryReadBlock(string first, int count, out short[] values) {
            values = null;
            if (!Call()) return false;
            if (!first.StartsWith("U")) {
                long devices = PlcAddressHelper.IsBitDevice(first) ? 16L * count : PlcAddressHelper.IsDoubleWordDevice(first) ? count / 2 : count;
                if (!Inside(first, devices)) return false;
            }
            return _s.TryReadBlock(first, count, out values);
        }
        public int WriteBlock(string first, short[] values) { return Call() ? _s.WriteBlock(first, values) : -1; }
    }

    /// <summary>A driver without block calls: the logged driver reads and writes it device by device.</summary>
    class Plain : IPlcDriver {
        readonly Kaban.Drivers.Simulation.SimulationDriver _s;
        public Plain(Kaban.Drivers.Simulation.SimulationDriver s) { _s = s; }
        public bool IsConnected { get { return true; } }
        public bool IsHealthy { get { return true; } }
        public bool Connect() { return true; }
        public void Disconnect() { }
        public int Read(string a) { return _s.Read(a); }
        public int[] ReadMulti(string[] a) { return _s.ReadMulti(a); }
        public bool TryRead(string a, out int v) { return _s.TryRead(a, out v); }
        public bool TryReadMulti(string[] a, out int[] v) { return _s.TryReadMulti(a, out v); }
        public int Write(string a, int v) { return _s.Write(a, v); }
    }

    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "pbc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        PlcBackup.Folder = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        Console.WriteLine("--- devices as letters and numbers ---");
        string p; long n;
        Check("X17 is X 15 (octal), WFF is W 255 (hex), STN3, U1\\G6000", PlcAddressHelper.TrySplit("X17", out p, out n) && p == "X" && n == 15 &&
              PlcAddressHelper.TrySplit("WFF", out p, out n) && p == "W" && n == 255 && PlcAddressHelper.TrySplit("STN3", out p, out n) && p == "STN" && n == 3 &&
              PlcAddressHelper.TrySplit(@"U1\G6000", out p, out n) && p == @"U1\G" && n == 6000);
        Check("  and back: X 15 = X17, B 31 = B1F, SB 16 = SB10", PlcAddressHelper.Compose("X", 15) == "X17" && PlcAddressHelper.Compose("B", 31) == "B1F" &&
              PlcAddressHelper.Compose("SB", 16) == "SB10");
        Check("  bits: M, X, B; words: D, R, TN, U1\\G0", PlcAddressHelper.IsBitDevice("M5") && PlcAddressHelper.IsBitDevice("X7") && PlcAddressHelper.IsBitDevice("B1F") &&
              !PlcAddressHelper.IsBitDevice("D5") && !PlcAddressHelper.IsBitDevice("R0") && !PlcAddressHelper.IsBitDevice("TN3") && !PlcAddressHelper.IsBitDevice(@"U1\G0"));

        var sim = new Kaban.Drivers.Simulation.SimulationDriver(23);
        PlcManager.Instance.RegisterDriver(23, sim);
        PlcManager.Instance.RegisterStation(23, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("boss", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(23, true);
        sim.Write("D0", 1); sim.Write("D7999", -5);
        for (int i = 0; i < 1000; i++) sim.Write("R" + (1000 + i), i * 3 - 7);
        sim.Write("M5", 1);
        for (int i = 0; i < 10; i++) sim.Write("M" + (100 + i), i % 3 == 0 ? 1 : 0);
        sim.Write("M210", 1);
        sim.Write("X17", 1); sim.Write("TN3", 77); sim.Write("W1F", 31);
        sim.Write(@"U1\G4", 0x86A0); sim.Write(@"U1\G5", 1);       // axis 1 Pr.3 = 100000
        sim.Write(@"U1\G16", 1234);                                // a maker setting between Pr.10 and Pr.11
        sim.Write(@"U1\G6000", 0x1100); sim.Write(@"U1\G6006", 5000);
        sim.Write(@"U1\G71000", 2); sim.Write(@"U1\G71002", 99);   // 71002 is no named word
        sim.Write("LCN3", 100000); sim.Write("LZ1", -70000); sim.Write("SM400", 1); sim.Write("SD210", 2026); sim.Write("TS5", 1);
        sim.Write(@"U1\G32800", 1); sim.Write(@"U1\G36400", 3);   // synchronous control: servo input axis type, main input axis
        sim.Write(@"U1\G36403", 77); sim.Write(@"U1\G36513", 9);   // a word the manual does not name; the last named one

        Console.WriteLine("--- what is read ---");
        var logged = (LoggedDriver)PlcManager.Instance.GetDriverForMonitoring(23);
        var plan = PlcBackup.PlanDevices(logged, logged);
        // 23 October: "I need all the device data of the moment" - the contacts and coils, LC, Z, LZ, SB, SW, SM, SD too
        Check("the simulator: GX Works3's default sizes, every device of an FX5",
              string.Join(" ", plan.Select(a => a.Range)) == "X0 - X1777 Y0 - Y1777 M0 - M7679 L0 - L7679 B0 - BFF F0 - F127 SB0 - SB1FF S0 - S4095 " +
              "TS0 - TS511 TC0 - TC511 TN0 - TN511 STS0 - STS15 STC0 - STC15 STN0 - STN15 CS0 - CS255 CC0 - CC255 CN0 - CN255 LCS0 - LCS63 LCC0 - LCC63 LCN0 - LCN63 " +
              "D0 - D7999 W0 - W1FF SW0 - SW1FF R0 - R32767 Z0 - Z19 LZ0 - LZ1 SM0 - SM9999 SD0 - SD11999",
              string.Join(" ", plan.Select(a => a.Range)));
        var sized = new Sized(sim);
        var notAnswered = new List<string>();
        var real = PlcBackup.PlanDevices(new PlcBackup.Link(sized, sized), notAnswered);
        Check("a CPU that refuses beyond its range: D 12000, M 8192, B 1024 found by asking", real.First(a => a.Prefix == "D").Points == 12000 &&
              real.First(a => a.Prefix == "M").Points == 8192 && real.First(a => a.Prefix == "B").Points == 1024 && real.First(a => a.Prefix == "R").Points == 32768,
              string.Join(" ", real.Select(a => a.Range)) + " - " + sized.Blocks + " asked");
        // 23 October: the sizes were asked with single reads (GetDevice) - a refused one counts against MX Component's link
        Check("  asked with block calls only, few of them (GX Works3's default kept: three questions)", sized.Asked == 0 && sized.Blocks < 150, sized.Blocks + " block calls");
        Check("  the retentive timers by the names the CPU answers (MX Component: SS, SC, SN)",
              real.Any(a => a.Range == "SS0 - SS15") && real.Any(a => a.Range == "SN0 - SN15") && PlcBackup.KindOf("SN").Prefix == "STN" &&
              new PlcBackup.Area { First = "SS0" }.WhyNotWritten.Contains("Contacts and coils") && new PlcBackup.Area { First = "SN0" }.Writable);
        Check("  a device the CPU does not answer is named, not left out silently", !real.Any(a => a.Prefix == "SB") &&
              notAnswered.Count == 1 && notAnswered[0].StartsWith("Link special relays (SB)"), string.Join(" | ", notAnswered));

        Console.WriteLine("--- the link lost while reading (23 October: the first backup on GX Simulator3 held none of the CPU's devices) ---");
        var flaky = new Sized(sim) { DropAfter = 40 };
        var flakyLink = new PlcBackup.Link(flaky, flaky) { ReconnectWaitMs = 3000 };
        var fb = new PlcBackup.Backup();
        var fplan = PlcBackup.PlanDevices(flakyLink, fb.Skipped);
        bool fdone = fplan != null && PlcBackup.ReadAreas(flakyLink, fb, fplan.Concat(PlcBackup.ModuleAreas(1, 4)).ToList(), null);
        Check("dropped while the sizes were asked: waited for, asked again - every device read, nothing skipped",
              fdone && !flakyLink.Lost && fb.Skipped.Count == 1 && fb.Skipped[0].StartsWith("Link special relays (SB)") && fplan.Count == real.Count && fb.Areas.Any(a => a.First == "D0" && a.Points == 12000),
              fdone + " " + flakyLink.Lost + " " + fb.Skipped.Count + " " + string.Join(" | ", fb.Skipped.Take(3)));
        var flaky2 = new Sized(sim);
        var flaky2Link = new PlcBackup.Link(flaky2, flaky2) { ReconnectWaitMs = 3000 };
        var fb2 = new PlcBackup.Backup();
        var fplan2 = PlcBackup.PlanDevices(flaky2Link, fb2.Skipped);
        flaky2.DropAfter = flaky2.Blocks + 5;
        bool fdone2 = PlcBackup.ReadAreas(flaky2Link, fb2, fplan2, null);
        Check("dropped while the areas were read: the same", fdone2 && fb2.Skipped.Count == 1 && fb2.Areas.Count == fplan2.Count &&
              fb2.Areas.First(a => a.First == "D0").Value(7999) == -5, fb2.Skipped.Count + " skipped");
        var dead = new Sized(sim) { DropAfter = 30, BackAfterConnects = 0 };
        var deadLink = new PlcBackup.Link(dead, dead) { ReconnectWaitMs = 600 };
        var db = new PlcBackup.Backup();
        var dplan = PlcBackup.PlanDevices(deadLink, db.Skipped);
        Check("not back in time: the work stops (Lost) - not a backup of what happened to be read", dplan == null && deadLink.Lost);
        var mod = PlcBackup.ModuleAreas(1, 4);
        Check("simple motion module 1 with 4 axes: 2 common areas, 9 a axis, 4 synchronous encoder axes, the memo and mark areas", mod.Count == 2 + 4 * 9 + 4 + 2,
              mod.Count.ToString());
        // 23 October: the FX5 Simple Motion Module manual (Advanced Synchronous Control), IB-0300255, Appendix 1
        Check("  synchronous control: the servo input axis parameters 32800 - 32805 + 10n, the synchronous parameters 36400 - 36513 + 200n, the encoder axes 34720 - 34735 + 20j",
              mod.Any(a => a.First == @"U1\G32810" && a.Points == 6) && mod.Any(a => a.First == @"U1\G36600" && a.Points == 114) &&
              mod.Any(a => a.First == @"U1\G34780" && a.Points == 16));
        var syncArea = mod.First(a => a.First == @"U1\G36400");
        Check("  all written back (the manual read), the words it names: not 36403, 36424 - 36429, 36471 (the FX5-SSC-G's), 36475, 36505",
              mod.All(a => a.Writable) && string.Join(" ", syncArea.Pieces.Select(x => x[0] + "+" + x[1])) == "0+3 4+20 30+22 60+6 70+1 72+3 76+2 82+5 100+5 106+8" &&
              string.Join(" ", mod.First(a => a.First == @"U1\G34720").Pieces.Select(x => x[0] + "+" + x[1])) == "0+9 10+6",
              string.Join(" ", syncArea.Pieces.Select(x => x[0] + "+" + x[1])));
        var par = mod.First(a => a.First == @"U1\G150");
        Check("  axis 2's parameters: U1\\G150 - U1\\G299, the words the manual names (0 - 15, 17 - 91, 100 - 149)", par.Points == 150 &&
              string.Join(" ", par.Pieces.Select(x => x[0] + "+" + x[1])) == "0+16 17+75 100+50" && par.Title.Contains("axis 2"), par.Title);

        var backup = new PlcBackup.Backup { Machine = "Bench machine", Plc = "Bench PLC", Cpu = "FX5U-64MT/ES", Serial = "", User = "boss",
                                            Date = new DateTime(2026, 10, 22, 18, 30, 12), Station = 23, Description = "before the new program;\r\nsecond line" };
        backup.Modules.Add(new KeyValuePair<int, int>(1, 4));
        var reports = new List<string>();
        bool done = PlcBackup.ReadAreas(logged, backup, plan.Concat(mod).ToList(), (d, t, what) => { reports.Add(what); return true; });
        Check("read: every area, the progress told", done && backup.Areas.Count == plan.Count + mod.Count && backup.Skipped.Count == 0 &&
              reports.Any(r => r.Contains("Data registers (D)")), backup.Areas.Count + " / " + backup.Skipped.Count);
        Func<string, PlcBackup.Area> area = first => backup.Areas.First(a => a.First == first);
        Check("  the values: D0 1, D7999 -5, R1999 2990, M5 on, X17 on, TN3 77, W1F 31",
              area("D0").Value(0) == 1 && area("D0").Value(7999) == -5 && area("R0").Value(1999) == 2990 && area("M0").Value(5) == 1 &&
              area("X0").Value(15) == 1 && area("TN0").Value(3) == 77 && area("W0").Value(31) == 31);
        Check("  32-bit devices two words each: LCN3 100000, LZ1 -70000", area("LCN0").WordsPerPoint == 2 && area("LCN0").Words.Length == 128 &&
              area("LCN0").Value(3) == 100000 && area("LZ0").Value(1) == -70000, area("LCN0").Value(3) + " " + area("LZ0").Value(1));
        Check("  the system's: SM400 on, SD210 2026; a timer's contact TS5 on", area("SM0").Value(400) == 1 && area("SD0").Value(210) == 2026 && area("TS0").Value(5) == 1);
        Check("  the module's: Pr.3 of axis 1 = 100000 in its two words, positioning data 1", (ushort)area(@"U1\G0").Value(4) + (area(@"U1\G0").Value(5) << 16) == 100000 &&
              area(@"U1\G6000").Value(6) == 5000);

        Console.WriteLine("--- the file ---");
        var path = PlcBackup.FileFor(backup);
        PlcBackup.Save(backup, path);
        Check("saved in the backups' folder, named by date, machine and PLC", File.Exists(path) && Path.GetFileName(path) == "2026-10-22 183012 Bench machine Bench PLC.plcbak", path);
        var text = File.ReadAllText(path);
        Check("  readable: its facts, then [D0;8000;Words;Data registers (D)] and the words", text.Contains("Machine;Bench machine") && text.Contains("User;boss") &&
              text.Contains("Description;before the new program;\\nsecond line") && text.Contains("[D0;8000;Words;Data registers (D)]") &&
              text.Contains("[M0;7680;Bits;Internal relays (M)]") && text.Contains(@"[U1\G150;150;Words;") && text.Contains(";0+16 17+75 100+50]"),
              text.Substring(0, Math.Min(600, text.Length)));
        var listed = PlcBackup.List();
        Check("  in the list: its facts alone", listed.Count == 1 && listed[0].Machine == "Bench machine" && listed[0].Areas.Count == 0 &&
              listed[0].Date == backup.Date && listed[0].Description == "before the new program;\r\nsecond line" && listed[0].Modules[0].Value == 4);
        var back = PlcBackup.Load(path);
        Check("  read again: the same areas and words", back.Areas.Count == backup.Areas.Count &&
              back.Areas.Zip(backup.Areas, (a, b) => a.First == b.First && a.Points == b.Points && a.Bits == b.Bits && a.Words.SequenceEqual(b.Words) &&
                                                      (a.Pieces == null) == (b.Pieces == null)).All(x => x));

        Console.WriteLine("--- what is written, and where ---");
        Func<string, PlcBackup.Area> barea = first => back.Areas.First(a => a.First == first);
        int from, count; string error, target;
        Check("R1000 - R1999 of R0 - R32767: from 1000, 1000 devices", PlcBackup.TryParseRange(barea("R0"), "R1000 - R1999", out from, out count, out error) && from == 1000 && count == 1000);
        Check("  also R1000..R1999, R1000 to R1999, one device R1000", PlcBackup.TryParseRange(barea("R0"), "R1000..R1999", out from, out count, out error) && count == 1000 &&
              PlcBackup.TryParseRange(barea("R0"), "r1000 to r1999", out from, out count, out error) && count == 1000 &&
              PlcBackup.TryParseRange(barea("R0"), "R1000", out from, out count, out error) && count == 1);
        Check("  outside the area, or another device: refused", !PlcBackup.TryParseRange(barea("D0"), "D7000 - D9000", out from, out count, out error) && error.Contains("D0 - D7999") &&
              !PlcBackup.TryParseRange(barea("D0"), "R0 - R5", out from, out count, out error), error);
        Check("written to D2500 (or D2500 - D3499)", PlcBackup.TryParseTarget(barea("R0"), 1000, 1000, "D2500", out target, out error) && target == "D2500" &&
              PlcBackup.TryParseTarget(barea("R0"), 1000, 1000, "D2500 - D3499", out target, out error));
        Check("  as many devices as read", !PlcBackup.TryParseTarget(barea("R0"), 1000, 1000, "D2500 - D2999", out target, out error) && error.Contains("1000"), error);
        Check("  words to words, bits to bits", !PlcBackup.TryParseTarget(barea("R0"), 0, 10, "M0", out target, out error) &&
              !PlcBackup.TryParseTarget(barea("M0"), 0, 10, "D0", out target, out error), error);
        Check("  never to X or Y; X itself not written back", !PlcBackup.TryParseTarget(barea("M0"), 0, 16, "Y0", out target, out error) &&
              !PlcBackup.TryParseTarget(barea("X0"), 0, 16, "", out target, out error) && error.Contains("Inputs and outputs"), error);
        Check("  not to the system's devices, nor a timer's contact; SM itself not written back", !PlcBackup.TryParseTarget(barea("D0"), 0, 10, "SD0", out target, out error) &&
              !PlcBackup.TryParseTarget(barea("M0"), 0, 16, "TS0", out target, out error) && error.Contains("Contacts and coils") &&
              !PlcBackup.TryParseTarget(barea("SM0"), 0, 16, "", out target, out error) && error.Contains("system's devices"), error);
        Check("  32-bit values to 32-bit devices only", !PlcBackup.TryParseTarget(barea("LCN0"), 0, 4, "D0", out target, out error) && error.Contains("32-bit") &&
              PlcBackup.TryParseTarget(barea("LCN0"), 0, 2, "LZ0", out target, out error), error);
        // 23 October: "write inputs and outputs as well if the selected PLC driver is simulator so we could make the simulator more useful"
        Check("  the simulator: X, Y and the system's devices too", PlcBackup.TryParseTarget(barea("X0"), 0, 16, "", out target, out error, simulator: true) &&
              PlcBackup.TryParseTarget(barea("M0"), 0, 16, "Y0", out target, out error, simulator: true) &&
              PlcBackup.TryParseTarget(barea("SD0"), 0, 16, "", out target, out error, simulator: true) &&
              PlcBackup.TryParseTarget(barea(@"U1\G36400"), 0, 200, "", out target, out error, simulator: true), error);
        Check("  a module's data to a module's buffer memory only", !PlcBackup.TryParseTarget(barea(@"U1\G6000"), 0, 10, "D0", out target, out error) &&
              PlcBackup.TryParseTarget(barea(@"U1\G6000"), 0, 1000, @"U1\G7000", out target, out error), error);

        // the PLC changed since the backup
        sim.Write("D0", 555);
        for (int i = 0; i < 10; i++) sim.Write("M" + (100 + i), 0);
        var parts = new List<PlcBackup.WriteRange>
        {
            new PlcBackup.WriteRange { Source = barea("R0"), From = 1000, Count = 1000, Target = "D2500" },
            new PlcBackup.WriteRange { Source = barea("D0"), From = 0, Count = 1, Target = "D0" },
            new PlcBackup.WriteRange { Source = barea("M0"), From = 100, Count = 10, Target = "M200" },
            new PlcBackup.WriteRange { Source = barea("M0"), From = 100, Count = 10, Target = "M100" },
            new PlcBackup.WriteRange { Source = barea(@"U1\G0"), From = 0, Count = 150, Target = @"U1\G150" },
            new PlcBackup.WriteRange { Source = barea(@"U1\G71000"), From = 0, Count = 1000, Target = @"U1\G72000" },
            new PlcBackup.WriteRange { Source = barea("LCN0"), From = 2, Count = 2, Target = "LCN10" },
            new PlcBackup.WriteRange { Source = barea(@"U1\G36400"), From = 0, Count = 114, Target = @"U1\G36600" }
        };
        sim.Write("LCN3", 0);
        var result = PlcBackup.Write(logged, logged, parts, null);
        var wflaky = new Sized(sim) { DropAfter = 3 };
        var wresult = PlcBackup.Write(new PlcBackup.Link(wflaky, wflaky) { ReconnectWaitMs = 3000 }, parts.Take(1).ToList(), null);
        Check("a write whose link drops: waited for, the block written again", wresult.Ok && !wresult.LinkLost && wresult.Points == 1000, string.Join(" | ", wresult.Failed));
        Check("written: " + result.Points + " devices", result.Ok && result.Points == 1000 + 1 + 10 + 10 + 150 + 1000 + 2 + 114, string.Join(" | ", result.Failed));
        Check("  axis 1's synchronous parameters into axis 2's: the named words (36600 3, 36713 9), not 36603",
              sim.Read(@"U1\G36600") == 3 && sim.Read(@"U1\G36713") == 9 && sim.Read(@"U1\G36603") == 0);
        Check("  LCN2 - LCN3 in LCN10 - LCN11: 100000 in LCN11 whole (both its words)", sim.Read("LCN11") == 100000 && sim.Read("LCN3") == 0, sim.Read("LCN11").ToString());
        Check("  R1000 - R1999's values in D2500 - D3499", Enumerable.Range(0, 1000).All(i => sim.Read("D" + (2500 + i)) == (short)(i * 3 - 7)) &&
              sim.Read("D2499") == 0 && sim.Read("D3500") == 0);
        Check("  D0 back to 1", sim.Read("D0") == 1);
        Check("  M100 - M109 in M200 - M209 - M210 after them untouched (not a whole word)",
              Enumerable.Range(0, 10).All(i => sim.Read("M" + (200 + i)) == (i % 3 == 0 ? 1 : 0)) && sim.Read("M210") == 1 &&
              Enumerable.Range(0, 10).All(i => sim.Read("M" + (100 + i)) == (i % 3 == 0 ? 1 : 0)));
        Check("  axis 1's parameters into axis 2's: Pr.3 100000 - but not the maker's word 16",
              (ushort)sim.Read(@"U1\G154") + (sim.Read(@"U1\G155") << 16) == 100000 && sim.Read(@"U1\G166") == 0);
        Check("  the interpolation axes: two words of each 10 (71002 no)", sim.Read(@"U1\G72000") == 2 && sim.Read(@"U1\G72002") == 0);
        PlcWriteLog.Flush();
        var log = PlcWriteLog.Read(DateTime.Now.AddMinutes(-1), DateTime.Now.AddMinutes(1));
        Check("the write log: one line a block (960 words at most) - D2500 - D3459, D3460 - D3499",
              log.Any(e => e.Device == "D2500 - D3459" && e.User == "boss" && e.Source.EndsWith("(960 words)")) && log.Any(e => e.Device == "D3460 - D3499") &&
              !log.Any(e => e.Device == "D2600"), string.Join(" | ", log.Where(e => e.Device.StartsWith("D")).Select(e => e.Device + " " + e.Source).Take(5)));
        Check("  the last bits of a part one by one (M208, M209)", log.Any(e => e.Device == "M208") && log.Any(e => e.Device == "M209"));

        // a backup made before the synchronous parameters' words were known: written back by the same words
        var oldFile = Path.Combine(dir, "old" + PlcBackup.Extension);
        File.WriteAllText(oldFile, "Machine;Old\r\nDate;2026-10-23 09:48:55\r\n[U1\\G36400;200;Words;old synchronous parameters]\r\n" +
                                   string.Join("\r\n", Enumerable.Range(0, 200 / 16 + 1).Select(i => string.Join(",", Enumerable.Repeat("1", Math.Min(16, 200 - 16 * i)))).Where(l => l.Length > 0)) + "\r\n");
        var old = PlcBackup.Load(oldFile);
        Check("an older backup's synchronous area (200 words, no runs saved): the manual's words only",
              old != null && old.Areas.Count == 1 && old.Areas[0].Words.Length == 200 && old.Areas[0].Pieces != null && old.Areas[0].Pieces.Last()[0] == 106,
              old == null ? "not loaded" : old.Areas.Count + " " + (old.Areas.Count > 0 ? old.Areas[0].Words.Length.ToString() : ""));

        Console.WriteLine("--- a driver without block calls ---");
        var plainLogged = new LoggedDriver(23, new Plain(sim));
        short[] words;
        Check("read device by device: M0's first word (M5 on), R1001", plainLogged.TryReadBlock("M0", 1, out words) && words[0] == 1 << 5 &&
              plainLogged.TryReadBlock("R1001", 1, out words) && words[0] == -4);
        Check("  written so: M300 - M315", plainLogged.WriteBlock("M300", new short[] { 0x0003 }) == 0 && sim.Read("M300") == 1 && sim.Read("M301") == 1 && sim.Read("M302") == 0);
        // 23 October: a driver on a thread of its own (Siemens, Delta): a block is ONE turn on that thread
        var bounded = new BoundedDriver(new Plain(sim));
        bounded.Connect();
        Check("  on its own thread (BoundedDriver): a block in one turn there", bounded.TryReadBlock("R1001", 2, out words) && words[0] == -4 && words[1] == -1 &&
              !bounded.RefusesOutOfRange && bounded.WriteBlock("D4000", new short[] { 7, 8 }) == 0 && sim.Read("D4001") == 8);

        SecurityManager.Logout();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nPLC BACKUP CORE ALL PASS" : "\nPLC BACKUP CORE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
