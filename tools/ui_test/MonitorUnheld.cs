using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.UI.Forms;

// 23 October: "PLC Monitor doesn't show the values for the simulator though" - a structure's members and a bit showed
// "-": the simulator's memory holds only what was written to it since the start (a device never written reads 0, a
// moving one its position now), and only that was shown. And a real controller's batch failed whole for one device it
// did not answer.
class MU {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        d = d ?? "";
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static Type MonType = typeof(SimulationMonitorForm);
    static Type RowType = MonType.GetNestedType("WatchRow", Any);
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
        var p = t.GetProperty(n, Any); return p.GetValue(o is Type ? null : o, null);
    }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, Any).SetValue(o, v); }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length >= a.Length &&
                                              m.GetParameters().Skip(a.Length).All(p => p.IsOptional));
        var args = a.Concat(mi.GetParameters().Skip(a.Length).Select(p => p.DefaultValue)).ToArray();
        return mi.Invoke(o is Type ? null : o, args);
    }
    static IList Rows(object sheet) { return (IList)Get(sheet, "Rows"); }
    static object Row(int station, string address, string type, string label) {
        var r = Activator.CreateInstance(RowType);
        Set(r, "Address", address); Set(r, "DataTypeOverride", type); Set(r, "LabelOverride", label);
        Set(r, "ArrayCount", 1); Set(r, "Station", station);
        return r;
    }
    static string Shown(object mon, object row) {
        var args = new object[] { mon, row, false };
        return (string)MonType.GetMethod("FormatValue", Any).Invoke(null, args);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }

    /// <summary>A real controller (not the simulator) that does not answer D9999 - one device of the batch.</summary>
    class Picky : IPlcDriver {
        public readonly Dictionary<string, int> Mem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public int Batches, Singles;
        public bool IsConnected { get { return true; } }
        public bool IsHealthy { get { return true; } }
        public bool Connect() { return true; }
        public void Disconnect() { }
        public int Read(string a) { int v; TryRead(a, out v); return v; }
        public int[] ReadMulti(string[] a) { int[] v; TryReadMulti(a, out v); return v; }
        public bool TryRead(string a, out int v) { Singles++; v = 0; if (a == "D9999") return false; Mem.TryGetValue(a, out v); return true; }
        public bool TryReadMulti(string[] a, out int[] v) {
            Batches++;
            v = new int[a.Length];
            bool ok = true;
            for (int i = 0; i < a.Length; i++) { if (a[i] == "D9999") { ok = false; continue; } int x; Mem.TryGetValue(a[i], out x); v[i] = x; }
            return ok;
        }
        public int Write(string a, int v) { Mem[a] = v; return 0; }
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "monu_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        StructuredTypes.FilePath = Path.Combine(dir, "StructuredTypes.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        var profile = new StructuredTypes.Type { Name = "Profile Parameters", Size = 50 };
        profile.Members.Add(new StructuredTypes.Member { Name = "Notched", DataType = "Bit", Offset = 0, Bit = 10 });
        profile.Members.Add(new StructuredTypes.Member { Name = "Width", DataType = "Word [Signed]", Offset = 2 });
        profile.Members.Add(new StructuredTypes.Member { Name = "Height", DataType = "Word [Signed]", Offset = 3 });
        StructuredTypes.Save(new[] { profile });

        var sim = new Kaban.Drivers.Simulation.SimulationDriver(23);
        PlcManager.Instance.RegisterDriver(23, sim);
        PlcManager.Instance.RegisterStation(23, "Bench", SecurityLevel.Operator);
        var picky = new Picky();
        PlcManager.Instance.RegisterDriver(24, picky);
        PlcManager.Instance.RegisterStation(24, "Real", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(23, true);
        ConnectionMonitor.ReportRead(24, true);

        var form = new SimulationMonitorForm();
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var tabs = (TabControl)Get(form, "_tabs");

        Console.WriteLine("--- the simulator: devices never written, a moving one ---");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 23);
        tabs.SelectedIndex = monitors.IndexOf(mon);
        var sh = Call(form, "EnsureSheet", mon);
        var bit = Row(23, "M653", "Bit", "NULL");
        var st = Row(23, "R25600", "Profile Parameters", "Profile");
        var moving = Row(23, "D4", "Word [Signed]", "Front Clamping - value");
        sim.AddRamp("D4", 100, 400, 50);
        Rows(sh).Insert(Rows(sh).Count - 1, st);
        Rows(sh).Insert(Rows(sh).Count - 1, bit);
        Rows(sh).Insert(Rows(sh).Count - 1, moving);
        Call(form, "RefreshValues"); Pump(50);
        Call(form, "RefreshValues"); Pump(50);
        Check("a bit never written: 0 (it showed -)", Shown(mon, bit) == "0", Shown(mon, bit));
        Check("  a structure never written: its members 0", Shown(mon, st) != null && Shown(mon, st).StartsWith("{0, 0, 0"), Shown(mon, st));
        int d4;
        Check("  a moving device (a ramp): its value now", Shown(mon, moving) != null && int.TryParse(Shown(mon, moving), out d4) && d4 >= 100 && d4 <= 400, Shown(mon, moving));
        sim.Write("R25602", 1234);
        Call(form, "RefreshValues"); Pump(50);
        var members = ((IList)Get(st, "Members")).Cast<object>().ToList();
        Check("  written: the member's value", Shown(mon, members[1]) == "1234", Shown(mon, members[1]));

        Console.WriteLine("--- a real controller: one device it does not answer ---");
        var real = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 24);
        tabs.SelectedIndex = monitors.IndexOf(real);
        var rsh = Call(form, "EnsureSheet", real);
        picky.Mem["D100"] = 77; picky.Mem["M5"] = 1;
        var good = Row(24, "D100", "Word [Signed]", "Good");
        var on = Row(24, "M5", "Bit", "On");
        var bad = Row(24, "D9999", "Word [Signed]", "Bad");
        Rows(rsh).Insert(Rows(rsh).Count - 1, good);
        Rows(rsh).Insert(Rows(rsh).Count - 1, bad);
        Rows(rsh).Insert(Rows(rsh).Count - 1, on);
        for (int i = 0; i < 3; i++) { Call(form, "RefreshValues"); Pump(150); }
        Check("the others' values shown (the batch failed whole: every row -), that one -", Shown(real, good) == "77" && Shown(real, on) == "1" && Shown(real, bad) == null,
              Shown(real, good) + " / " + Shown(real, on) + " / " + Shown(real, bad));
        var refused = (IDictionary)Get(real, "Refused");
        int batches = picky.Batches, singles = picky.Singles;
        for (int i = 0; i < 3; i++) { Call(form, "RefreshValues"); Pump(150); }
        Check("  then left out of the batch a while: one call a refresh again, no device asked alone", refused.Contains("D9999") &&
              picky.Batches > batches && picky.Singles == singles, picky.Batches + " batches, " + picky.Singles + " single reads");
        picky.Mem["D100"] = 78;
        Call(form, "RefreshValues"); Pump(150); Call(form, "RefreshValues"); Pump(50);
        Check("  and the values go on", Shown(real, good) == "78", Shown(real, good));

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nMONITOR UNHELD ALL PASS" : "\nMONITOR UNHELD " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
