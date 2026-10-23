using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Kaban.Core.Helpers;
using Kaban.Drivers.MitsubishiActUtl;

// 23 October: on GX Simulator3 the backup's long counters (LCN0 - LCN63) were found one short and refused as an
// area, the long index registers too: MX Component's ReadDeviceBlock2 counts a 32-bit device as a device, not as
// words - so a call that answered would have written twice the words asked. The driver reads and writes them one
// by one (the random read, SetDevice); the other devices stay block calls.
//
// Runs against the test stand-in for the interop (a scripted controller that counts its calls).
class MxBlocks {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags S = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    static Type Ctl = typeof(ActUtlTypeLib.ActUtlType);
    static FieldInfo F(string n) { return Ctl.GetField(n, S); }
    static object V(string n) { return F(n).GetValue(null); }
    static bool Until(Func<bool> c, int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { if (c()) return true; Thread.Sleep(10); } return c(); }

    [STAThread]
    static void Main() {
        if (F("BlockReads") == null) { Console.WriteLine("SKIP: needs the scripted ActUtlType stand-in\n\nMX BLOCKS ALL PASS"); return; }
        string dir = Path.Combine(Path.GetTempPath(), "mxb_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        typeof(MxDiagnostics).GetField("_scanned", S).SetValue(null, true);
        typeof(MxDiagnostics).GetField("_reported", S).SetValue(null, true);
        Ctl.GetMethod("Reset").Invoke(null, null);
        F("Registered").SetValue(null, true);
        var drv = new MitsubishiActUtl(1);
        drv.Connect();
        Check("connected", Until(() => drv.IsConnected, 3000));

        short[] words;
        Check("D0 x 960: one block call", drv.TryReadBlock("D0", 960, out words) && words.Length == 960 && (int)V("BlockReads") == 1 && (int)V("RandomReads") == 0 &&
              (string)V("LastBlock") == "D0");
        Check("LCN0 x 128 words (64 counters): no block call - the random read, each counter's 32 bits",
              drv.TryReadBlock("LCN0", 128, out words) && words.Length == 128 && (int)V("BlockReads") == 1 && (int)V("RandomReads") >= 1 &&
              ((string)V("LastRandom")).StartsWith("LCN0\nLCN1\n"), (int)V("BlockReads") + " block, " + (int)V("RandomReads") + " random");
        Check("LZ0 x 4 words: the same", drv.TryReadBlock("LZ0", 4, out words) && words.Length == 4 && (int)V("BlockReads") == 1 && ((string)V("LastRandom")) == "LZ0\nLZ1");
        int sets = (int)V("Sets");
        Check("written: LCN2 - LCN3 with SetDevice (two of them), no block call", drv.WriteBlock("LCN2", new short[] { 1, 2, 3, 4 }) == 0 &&
              (int)V("BlockWrites") == 0 && (int)V("Sets") == sets + 2);
        Check("  D100 x 2: a block call", drv.WriteBlock("D100", new short[] { 5, 6 }) == 0 && (int)V("BlockWrites") == 1);

        drv.Dispose();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nMX BLOCKS ALL PASS" : "\nMX BLOCKS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
