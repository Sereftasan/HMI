using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Kaban.Core.Helpers;
using Kaban.Drivers.MitsubishiActUtl;

// The MX Component connection at a start and a restart (29 September): the
// logs showed a second Open() on a control that had just opened - answered
// "already open" (0xF0000003), taken for a spent control, the link reported
// down and rebuilt - and, while a controller refused, a new Open one to two
// times a second for as long as it refused.
//
// Runs against the test stand-in for the interop (a scripted controller); with
// the real MX Component interop the script is not there and the test says so.
class MxRestart {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags S = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    static Type Ctl = typeof(ActUtlTypeLib.ActUtlType);
    static FieldInfo F(string n) { return Ctl.GetField(n, S); }
    static int N(string n) { return (int)F(n).GetValue(null); }
    static void Set(string n, object v) { F(n).SetValue(null, v); }
    static string Log() { try { return File.ReadAllText(AuditLogger.TodayFile); } catch { return ""; } }
    static bool Until(Func<bool> c, int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { if (c()) return true; Thread.Sleep(10); } return c(); }

    [STAThread]
    static void Main() {
        if (F("Registered") == null) { Console.WriteLine("SKIP: needs the scripted ActUtlType stand-in\n\nMX RESTART ALL PASS"); return; }
        string dir = Path.Combine(Path.GetTempPath(), "mx_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        // The environment report and the station scan run once per process on a
        // first failure; they open controls of their own and would blur the counts.
        typeof(MxDiagnostics).GetField("_scanned", S).SetValue(null, true);
        typeof(MxDiagnostics).GetField("_reported", S).SetValue(null, true);
        Ctl.GetMethod("Reset").Invoke(null, null);
        Set("Registered", true);

        Console.WriteLine("--- the start: opened once, and left open ---");
        Set("OpenDelayMs", 300);                          // slower than the 150 ms the start waits
        var drv = new MitsubishiActUtl(1);
        bool first = drv.Connect();                       // PlcManager.RegisterDriver
        Check("the first Connect returns while Open() is still running", !first);
        Check("  the station opens in the background", Until(() => drv.IsConnected, 3000));
        bool again = PlcManagerLike(drv);                 // MainForm's start-up check
        Check("asked again once open: 'connected', no second Open", again && N("Opens") == 1 && N("AlreadyOpen") == 0,
              "opens " + N("Opens") + " already-open " + N("AlreadyOpen"));
        for (int i = 0; i < 20; i++) drv.Connect();
        Check("  nor on any later ask (the scanner, the connection poll)", N("Opens") == 1 && drv.IsConnected && N("Live") == 1);
        Check("  the log has no 'connect failed' and no 0xF0000003", !Log().Contains("connect failed") && !Log().Contains("F0000003"));

        Console.WriteLine("--- two reconnects at the same moment: one Open ---");
        drv.Disconnect();
        Set("OpenDelayMs", 60);
        int before = N("Opens");
        var go = new ManualResetEvent(false);
        var threads = Enumerable.Range(0, 3).Select(_ => new Thread(() => { go.WaitOne(); drv.Connect(); })).ToList();
        threads.ForEach(t => t.Start());
        go.Set();
        threads.ForEach(t => t.Join());
        Until(() => drv.IsConnected, 2000);
        Check("connected, with ONE more Open and no 'already open'", drv.IsConnected && N("Opens") == before + 1 && N("AlreadyOpen") == 0,
              "opens +" + (N("Opens") - before) + " already-open " + N("AlreadyOpen"));
        Check("  one live connection", N("Live") == 1, N("Live").ToString());

        Console.WriteLine("--- a controller that does not answer: asked at a pace ---");
        drv.Disconnect();
        Set("OpenDelayMs", 0);
        Set("OpenHook", (Func<int, int>)(st => unchecked((int)0x01808301)));
        before = N("Opens");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3200) { drv.Connect(); Thread.Sleep(20); }   // callers as eager as they like
        int tried = N("Opens") - before;
        Check("3.2 s of asking: 3 or 4 Opens (at 0, 0.5, 1.5 s ...), not one per call", tried >= 3 && tried <= 4, tried + " opens");
        Check("  the pauses: 0.5, 1, 2, 4, then 5 s",
              new[] { 1, 2, 3, 4, 5, 9 }.Select(n => (int)typeof(MitsubishiActUtl).GetMethod("RetryDelayMs", S).Invoke(null, new object[] { n }))
                                        .SequenceEqual(new[] { 500, 1000, 2000, 4000, 5000, 5000 }));
        Check("  each failed control closed: nothing left open", N("Live") == 0, N("Live").ToString());
        string log = Log();
        Check("  the log names the code in words", log.Contains("connect failed with 0x01808301 (no answer from the controller while opening"),
              log.Split('\n').LastOrDefault(l => l.Contains("connect failed")) ?? "");

        Console.WriteLine("--- the controller answers again: found within 5 s ---");
        Set("OpenHook", null);
        var back = System.Diagnostics.Stopwatch.StartNew();
        bool up = Until(() => { drv.Connect(); return drv.IsConnected; }, 6000);
        Check("connected again", up && back.ElapsedMilliseconds <= 5200, back.ElapsedMilliseconds + " ms");
        Check("  the log says after how many attempts", Log().Contains("Mitsubishi station 1 opened after"),
              Log().Split('\n').LastOrDefault(l => l.Contains("opened after")) ?? "");
        Check("  the count starts again: the next outage is logged in full", (int)typeof(MitsubishiActUtl).GetField("_failCount", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(drv) == 0);

        Console.WriteLine("--- exit: the connection closed, and the log says so ---");
        drv.Disconnect();
        drv.Dispose();
        log = Log();
        Check("'connection closed (Close returned 0x00000000) and MX Component control released'",
              log.Contains("Mitsubishi station 1: connection closed (Close returned 0x00000000) and MX Component control released."),
              log.Split('\n').LastOrDefault(l => l.Contains("released")) ?? "");
        Check("  nothing left open", N("Live") == 0);

        Console.WriteLine("--- a driver that never connected says so at exit ---");
        Set("OpenHook", (Func<int, int>)(st => unchecked((int)0x01808301)));
        int opensBefore = N("Opens");
        var never = new MitsubishiActUtl(1);
        never.Connect();
        Until(() => N("Opens") > opensBefore, 1000);
        never.Disconnect();
        never.Dispose();
        Check("'not connected; MX Component control released'", Log().Contains("Mitsubishi station 1: not connected; MX Component control released."));

        Ctl.GetMethod("Reset").Invoke(null, null);
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nMX RESTART ALL PASS" : "\nMX RESTART " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    /// <summary>PlcManager.Connect(1) as the start-up check calls it.</summary>
    static bool PlcManagerLike(MitsubishiActUtl drv) {
        PlcManager.Instance.RegisterDriver(91, drv);      // RegisterDriver asks only when not connected
        return PlcManager.Instance.Connect(91);
    }
}
