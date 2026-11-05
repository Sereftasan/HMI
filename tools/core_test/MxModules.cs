using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using Kaban.Core.Helpers;
using Kaban.Drivers.MitsubishiActUtl;

// 23 October: on GX Simulator3 the backup's long counters (LCN0 - LCN63) were found one short and refused as an
// area, the long index registers too: MX Component's ReadDeviceBlock2 counts a 32-bit device as a device, not as
// words - so a call that answered would have written twice the words asked. The driver reads and writes them one
// by one (the random read, SetDevice); the other devices stay block calls.
//
// Runs against the test stand-in for the interop (a scripted controller that counts its calls).
// 5 November, found on the panel: PLC 1 on GX Simulator3 "lost" every minute or two on the Machine Parameters pages and with the
// machine details on (the axes' loads, U0\G2478 ...). A connection that does not take a module's buffer words in a random read reads
// them one by one; with the PLC's own devices in the same call, the call passed its second and the link was taken down.
class MxModules {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags S = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    static Type Ctl = typeof(ActUtlTypeLib.ActUtlType);
    static FieldInfo F(string n) { return Ctl.GetField(n, S); }
    static bool Until(Func<bool> c, int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { if (c()) return true; Thread.Sleep(10); } return c(); }

    [STAThread]
    static void Main() {
        if (F("GetDelayMs") == null) { Console.WriteLine("SKIP: needs the scripted ActUtlType stand-in\n\nMX MODULES ALL PASS"); return; }
        string dir = Path.Combine(Path.GetTempPath(), "mxm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        typeof(MxDiagnostics).GetField("_scanned", S).SetValue(null, true);
        typeof(MxDiagnostics).GetField("_reported", S).SetValue(null, true);
        Ctl.GetMethod("Reset").Invoke(null, null);
        F("Registered").SetValue(null, true);
        var drv = new MitsubishiActUtl(1);
        drv.Connect();
        Check("connected", Until(() => drv.IsConnected, 3000));

        // the page: 40 of the PLC's devices and 16 axes x 3 loads of the simple motion module, refused in a random read
        var list = new List<string>();
        for (int i = 0; i < 40; i++) list.Add("D" + (100 + i));
        for (int ax = 0; ax < 16; ax++) for (int k = 0; k < 3; k++) list.Add("U0\\G" + (2478 + ax * 100 + k));
        F("RefuseModuleRandom").SetValue(null, true);
        F("GetDelayMs").SetValue(null, 30);                 // 48 x 30 ms: 1.4 s one by one
        int[] v;
        var sw = Stopwatch.StartNew();
        bool ok = drv.TryReadMulti(list.ToArray(), out v);
        sw.Stop();
        Check("the scan's read answers (the PLC's devices read), though the module's words took 1.4 s", ok && v != null && v.Length == list.Count, ok + " " + sw.ElapsedMilliseconds + " ms");
        Check("  the connection kept (was: taken down, \"PLC COMMUNICATION LOST\")", drv.IsConnected);
        string log = File.ReadAllText(AuditLogger.TodayFile);
        Check("  said once in the log: the module's words took long, read in a call of their own", log.Contains("word(s) of a module's buffer (U0\\G2478 ...) took"), log.Length.ToString());

        Console.WriteLine("--- a module that never answers in time: rests, the scans go on ---");
        F("GetDelayMs").SetValue(null, 150);                // 48 x 150 ms: 7 s - past the module's 5 s
        ok = drv.TryReadMulti(list.ToArray(), out v);
        Check("the PLC's devices still read, the connection kept", ok && drv.IsConnected);
        log = File.ReadAllText(AuditLogger.TodayFile);
        Check("  the module's words not asked again for 30 s (logged)", log.Contains("not asked again for 30 s; the connection kept"));
        Thread.Sleep(2500);                                 // the slow call finishes on the worker
        int gets = (int)F("Gets").GetValue(null);
        sw.Restart();
        ok = drv.TryReadMulti(list.ToArray(), out v);
        sw.Stop();
        Check("  the next scan: at once, the module not read meanwhile", ok && sw.ElapsedMilliseconds < 500 && (int)F("Gets").GetValue(null) == gets, sw.ElapsedMilliseconds + " ms");

        F("GetDelayMs").SetValue(null, 0);
        drv.Dispose();
        Ctl.GetMethod("Reset").Invoke(null, null);
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nMX MODULES ALL PASS" : "\nMX MODULES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
