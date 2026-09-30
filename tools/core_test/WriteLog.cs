using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.Drivers.Simulation;

// THE PLC WRITE LOG (30 September): every write through a driver PlcManager hands
// out is logged device by device - value, value before, result, user and source -
// and DeviceHistory tells a change the HMI made from one the PLC program made.
class W {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }

    // A controller that refuses every write, or throws.
    class Refusing : IPlcDriver, INonBlockingDriver {
        public bool Throw;
        public bool IsConnected { get { return true; } }
        public bool IsHealthy { get { return true; } }
        public bool Connect() { return true; }
        public void Disconnect() { }
        public int Read(string a) { return 0; }
        public int[] ReadMulti(string[] a) { return new int[a.Length]; }
        public int Write(string a, int v) { if (Throw) throw new InvalidOperationException("cable"); return 0x1234; }
        public bool TryRead(string a, out int v) { v = 0; return true; }
        public bool TryReadMulti(string[] a, out int[] v) { v = new int[a.Length]; return true; }
    }

    static List<string> Lines() {
        PlcWriteLog.Flush();                     // written on its own thread
        var f = PlcWriteLog.FileFor(DateTime.Now);
        return File.Exists(f) ? File.ReadAllLines(f).ToList() : new List<string>();
    }
    static string[] Last(int back = 1) { var l = Lines(); return l[l.Count - back].Split(';'); }

    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "writelog_" + Guid.NewGuid().ToString("N"));
        AuditLogger.LogDirectory = dir;
        DeviceHistory.Clear();
        var seen = new List<PlcWriteLog.Entry>();
        PlcWriteLog.Written += e => seen.Add(e);

        Console.WriteLine("--- every registered driver writes through the log ---");
        PlcManager.Instance.RegisterDriver(1, new SimulationDriver(1));
        var drv = PlcManager.Instance.GetDriverForMonitoring(1);
        Check("the registered driver is the logging one", drv is LoggedDriver);
        Check("Unwrap gives the controller's own driver", PlcManager.Unwrap(drv) is SimulationDriver);
        var sim = (SimulationDriver)PlcManager.Unwrap(drv);

        SecurityManager.Login("mehmet", SecurityLevel.Operator);
        int rc;
        using (PlcWriteLog.Source("ManualCutting.btnSaw [Saw]: SET")) rc = drv.Write("M645", 1);
        Check("the write itself still happens", rc == 0 && sim.Read("M645") == 1);
        var lines = Lines();
        Check("a daily CSV with its header", lines.Count == 2 && lines[0] == PlcWriteLog.Header, string.Join(" | ", lines));
        var f = Last();
        Check("ten fields", f.Length == 10, f.Length.ToString());
        Check("time with milliseconds", f[0].Length == 23 && f[0][19] == '.', f[0]);
        Check("station, device, value, hex", f[1] == "1" && f[3] == "M645" && f[4] == "1" && f[5] == "0x0001", string.Join(";", f));
        Check("before: not read yet", f[6] == "");
        Check("result OK", f[7] == "OK");
        Check("the user signed in", f[8] == "mehmet");
        Check("the source the control named", f[9] == "ManualCutting.btnSaw [Saw]: SET", f[9]);
        Check("the Written event", seen.Count == 1 && seen[0].Device == "M645");

        Console.WriteLine("--- the value before, from what the HMI read ---");
        DeviceHistory.Observe(1, new[] { "D100" }, new[] { 5 }, DateTime.Now, DateTime.Now);
        using (PlcWriteLog.Source("page.numSpeed [Speed]: entered 7")) drv.Write("D100", 7);
        f = Last();
        Check("before is the last value read", f[3] == "D100" && f[4] == "7" && f[6] == "5", string.Join(";", f));

        Console.WriteLine("--- no source named: the calling code is ---");
        drv.Write("D101", 300);
        f = Last();
        Check("the caller's class and method", f[9] == "W.Main", f[9]);
        Check("a value over 9 in hex too", f[5] == "0x012C", f[5]);

        Console.WriteLine("--- a 32-bit value is two words: two lines ---");
        int before = Lines().Count;
        using (PlcWriteLog.Source("page.numLength: entered 100000")) { drv.Write("D200", 100000 & 0xFFFF); drv.Write("D201", 100000 >> 16); }
        Check("two lines, one per word", Lines().Count == before + 2 && Last(2)[3] == "D200" && Last()[3] == "D201");

        Console.WriteLine("--- a digit-specified write is every bit it changes ---");
        before = Lines().Count;
        using (PlcWriteLog.Source("test: K4M100")) drv.Write("K4M100", 0x00F3);
        var bits = Lines().Skip(before).Select(l => l.Split(';')).ToList();
        Check("sixteen lines, M100 to M115", bits.Count == 16 && bits[0][3] == "M100" && bits[15][3] == "M115",
              string.Join(",", bits.Select(q => q[3])));
        Check("each bit's own value", bits[0][4] == "1" && bits[1][4] == "1" && bits[2][4] == "0" && bits[4][4] == "1" && bits[8][4] == "0",
              string.Join("", bits.Select(q => q[4])));
        Check("the whole write named in the source", bits[0][9].StartsWith("(K4M100 = 243) test: K4M100"), bits[0][9]);

        Console.WriteLine("--- a bit of a word is the bit and the word ---");
        DeviceHistory.Observe(1, new[] { "D300" }, new[] { 0x10 }, DateTime.Now, DateTime.Now);
        before = Lines().Count;
        drv.Write("D300.3", 1);
        var bw = Lines().Skip(before).Select(l => l.Split(';')).ToList();
        Check("two lines", bw.Count == 2, bw.Count.ToString());
        Check("the bit", bw.Count == 2 && bw[0][3] == "D300.3" && bw[0][4] == "1");
        Check("the word, from its value before", bw.Count == 2 && bw[1][3] == "D300" && bw[1][6] == "16" && bw[1][4] == "24",
              bw.Count == 2 ? string.Join(";", bw[1]) : "");

        Console.WriteLine("--- a refused write is logged as refused ---");
        PlcManager.Instance.RegisterDriver(7, new Refusing());
        var bad = PlcManager.Instance.GetDriverForMonitoring(7);
        rc = bad.Write("D10", 3);
        f = Last();
        Check("the driver's answer is passed on", rc == 0x1234);
        Check("the result names the code", f[1] == "7" && f[7] == "FAILED 0x00001234", f[7]);
        ((Refusing)PlcManager.Unwrap(bad)).Throw = true;
        bool threw = false;
        try { bad.Write("D11", 4); } catch (InvalidOperationException) { threw = true; }
        f = Last();
        Check("a write that throws still throws", threw);
        Check("and is logged as failed", f[3] == "D11" && f[7].StartsWith("FAILED"), f[7]);
        using (PlcWriteLog.Source("a;b")) drv.Write("D12", 1);
        Check("a semicolon in a source cannot break a line", Last().Length == 10 && Last()[9] == "a,b", Last()[9]);

        Console.WriteLine("--- the PLC program's writes are not the HMI's ---");
        before = Lines().Count;
        sim.Write("D400", 9);                    // the simulator's own driver: the PLC stand-in
        Check("a write past the HMI's drivers is not logged", Lines().Count == before);

        Console.WriteLine("--- who changed it: DeviceHistory ---");
        var t0 = DateTime.Now;
        DeviceHistory.Observe(1, new[] { "D500", "D501" }, new[] { 0, 0 }, t0, t0);
        System.Threading.Thread.Sleep(20);
        using (PlcWriteLog.Source("page.numA: entered 11")) drv.Write("D500", 11);
        sim.Write("D501", 22);                   // the ladder
        System.Threading.Thread.Sleep(20);
        var t1 = DateTime.Now;
        int[] vals; drv.TryReadMulti(new[] { "D500", "D501" }, out vals);
        DeviceHistory.Observe(1, new[] { "D500", "D501" }, vals, t1, DateTime.Now);
        var a = DeviceHistory.Get(1, "D500");
        var b = DeviceHistory.Get(1, "D501");
        Check("the HMI's change is the HMI's", a != null && a.Changed.HasValue && a.ByHmi && a.From == 0 && a.Value == 11);
        Check("  with its source and user", a != null && a.WriteSource == "page.numA: entered 11" && a.WriteUser == "mehmet", a == null ? "" : a.WriteSource);
        Check("the ladder's change is the PLC's", b != null && b.Changed.HasValue && !b.ByHmi && b.Value == 22);

        // The HMI writes 1, the ladder puts 0 back before the next reading and
        // then 5: 5 is the PLC's, not the HMI's.
        drv.Write("D500", 1); sim.Write("D500", 5);
        DeviceHistory.Observe(1, "D500", 5);
        a = DeviceHistory.Get(1, "D500");
        Check("a value the HMI did not write is the PLC's, even right after an HMI write", a != null && !a.ByHmi && a.Value == 5);
        Check("  and the HMI's last write is still known", a != null && a.LastWriteValue == 1 && a.LastWrite.HasValue);

        // A slow reading that finishes after a newer one must not flip it back.
        var old = DateTime.Now.AddSeconds(-5);
        DeviceHistory.Observe(1, new[] { "D500" }, new[] { 1 }, old, old);
        Check("an older reading is ignored", DeviceHistory.Value(1, "D500") == 5);

        // Bits of a digit-specified device read as a word.
        DeviceHistory.Observe(1, new[] { "K1M600" }, new[] { 0 }, DateTime.Now, DateTime.Now);
        using (PlcWriteLog.Source("t")) drv.Write("M602", 1);
        DeviceHistory.Observe(1, new[] { "K1M600" }, new[] { 4 }, DateTime.Now, DateTime.Now);
        var m602 = DeviceHistory.Get(1, "M602");
        Check("a word of bits read: each bit's change is known", m602 != null && m602.Changed.HasValue && m602.ByHmi);

        Console.WriteLine("--- the log viewer reads it ---");
        var entries = PlcWriteLog.Read(DateTime.Today, DateTime.Now.AddMinutes(1));
        Check("read back", entries.Count == Lines().Count - 1, entries.Count + " vs " + (Lines().Count - 1));
        Check("  with milliseconds and the values", entries.Any(e => e.Device == "D100" && e.Value == 7 && e.Before == 5 && e.Time.Millisecond >= 0));
        AuditLogger.Log("set bit M645 (PLC 1, control tag 'M645') to 1.");
        var all = AuditLogReader.Read(DateTime.Today, DateTime.Now.AddMinutes(1), true);
        var dw = all.Where(e => e.Kind == AuditKind.DeviceWrite).ToList();
        Check("the viewer gets them as PLC device writes", dw.Count == entries.Count, dw.Count.ToString());
        Check("  with the controller filled in", dw.All(e => e.Station.HasValue));
        Check("  and the audit entries too", all.Any(e => e.Kind == AuditKind.PlcWrite));
        Check("  in time order", all.Zip(all.Skip(1), (x, y) => x.Time <= y.Time || (x.Time - y.Time).TotalSeconds < 1).All(q => q));
        var again = AuditLogReader.Read(DateTime.Today, DateTime.Now.AddMinutes(1), true);
        Check("the same entries on the next refresh (the viewer sees nothing new)",
              ReferenceEquals(again.Last(e => e.Kind == AuditKind.DeviceWrite), dw.Last()));
        Check("without asking, only the audit log (PLC Diagnostics)",
              AuditLogReader.Read(DateTime.Today, DateTime.Now.AddMinutes(1)).All(e => e.Kind != AuditKind.DeviceWrite));
        var text = dw.First(e => e.Text.StartsWith("D100 ")).Text;
        Check("an entry says what, before, where and from what", text.Contains("D100 = 7") && text.Contains("was 5") && text.Contains("(PLC 1)") && text.Contains("numSpeed"), text);

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nWRITELOG ALL PASS" : "\n" + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
