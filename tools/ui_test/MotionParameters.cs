using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;

// 22 October: "Simple motion write device should be changed to numerical input instead of display so the
// admin user could change them (some parameters need EPROM write as well so we need to check it on it's
// attached manual)" - the FX5 Simple Motion Module User's Manual (Application): a parameter written to the
// buffer memory is used at the next start or when PLC READY turns on again, and lost at power off unless
// the flash ROM is written (Cd.1, G5900 = 1, only while PLC READY - Cd.190, G5950 - is off).
class MP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        d = d ?? "";
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        return t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length).Invoke(o is Type ? null : o, a);
    }
    static SimpleMotion.Item Item(string key) { return SimpleMotion.Find(key); }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        typeof(WorkForm).GetField("Inline", Any).SetValue(null, true);
        var answers = new List<string>();
        DialogResult answer = DialogResult.No;
        typeof(MotionFlash).GetField("Ask", Any).SetValue(null, (Func<string, MessageBoxButtons, DialogResult>)((text, buttons) =>
        {
            answers.Add(text);
            return buttons == MessageBoxButtons.YesNo ? answer : DialogResult.OK;
        }));

        Console.WriteLine("--- the parameters, from the manual ---");
        var parameters = SimpleMotion.Items.Where(i => i.Group == "Parameters").ToList();
        Check("every parameter of the Axis Settings page can be written", parameters.Count == 11 && parameters.All(i => i.Writable) &&
              SimpleMotion.Items.Where(i => i.Group != "Parameters").All(i => !i.Writable));
        Check("  their ranges: Pr.2 1 - 200000000, Pr.9 1 - 8388608 ms, Pr.11 0 - 65535, Pr.12 the whole 32 bits",
              Item("PulsesPerRevolution").Min == 1 && Item("PulsesPerRevolution").Max == 200000000 && Item("AccelerationTime").Max == 8388608 &&
              Item("Backlash").Max == 65535 && Item("SoftLimitUpper").Min == int.MinValue && Item("SoftLimitUpper").Max == int.MaxValue);
        Check("  used at PLC READY (area (a): Pr.1 - 3, 11 - 13, 45, 46), at the next start (area (b): Pr.8 - 10)",
              new[] { "UnitSetting", "PulsesPerRevolution", "TravelPerRevolution", "Backlash", "SoftLimitUpper", "SoftLimitLower", "HomeAddress", "HomingSpeed" }.All(k => Item(k).AtPlcReady) &&
              new[] { "SpeedLimit", "AccelerationTime", "DecelerationTime" }.All(k => !Item(k).AtPlcReady));
        long raw;
        Check("a position typed in mm: 12.5 mm = 125000 (0.1 um)", SimpleMotion.TryToRaw(Item("TravelPerRevolution"), 12.5, 0, SimpleMotion.RowUnit.Module, out raw) && raw == 125000, raw.ToString());
        Check("  in degrees: 0.25 = 25000 (0.00001 degree)", SimpleMotion.TryToRaw(Item("SoftLimitUpper"), 0.25, 2, SimpleMotion.RowUnit.Module, out raw) && raw == 25000, raw.ToString());
        Check("  a speed in mm/min: 6000.5 = 600050 (0.01 mm/min)", SimpleMotion.TryToRaw(Item("SpeedLimit"), 6000.5, 0, SimpleMotion.RowUnit.Module, out raw) && raw == 600050, raw.ToString());
        Check("  shown and typed again: the same", SimpleMotion.Shown(Item("SpeedLimit"), 600050, 0, SimpleMotion.RowUnit.Module) == 6000.5);
        Check("  out of range: refused (Pr.9 0 ms; Pr.11 7 mm = 70000 > 65535)",
              !SimpleMotion.TryToRaw(Item("AccelerationTime"), 0, 0, SimpleMotion.RowUnit.Module, out raw) && raw == 1 &&
              !SimpleMotion.TryToRaw(Item("Backlash"), 7, 0, SimpleMotion.RowUnit.Module, out raw));
        int lo, hi;
        SimpleMotion.ToWords(Item("SoftLimitLower"), -2, out lo, out hi);
        Check("a 32-bit value into its words, low first: -2 = FFFE FFFF", (lo & 0xFFFF) == 0xFFFE && (hi & 0xFFFF) == 0xFFFF);

        Console.WriteLine("--- a parameter cell ---");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(23);
        PlcManager.Instance.RegisterDriver(23, sim);
        PlcManager.Instance.RegisterStation(23, "Bench", SecurityLevel.Operator);
        ConnectionMonitor.ReportRead(23, true);
        // axis 2 of module 1: its parameters at 150 ...
        sim.Write(@"U1\G150", 0);                               // Pr.1 mm
        sim.Write(@"U1\G154", 100000 & 0xFFFF); sim.Write(@"U1\G155", 100000 >> 16); // Pr.3 10 mm
        sim.Write(@"U1\G162", 1000); sim.Write(@"U1\G163", 0);   // Pr.9 1000 ms
        var form = new Form { Width = 600, Height = 200 };
        var travel = new MotionValueHMI(23, Item("TravelPerRevolution"), 1, 2, 2) { Bounds = new Rectangle(10, 10, 140, 26) };
        var accel = new MotionValueHMI(23, Item("AccelerationTime"), 1, 2, 2) { Bounds = new Rectangle(160, 10, 100, 26) };
        var unit = new MotionValueHMI(23, Item("UnitSetting"), 1, 2, 2) { Bounds = new Rectangle(270, 10, 70, 26) };
        var status = new MotionValueHMI(23, Item("OperationStatus"), 1, 2, 2) { Bounds = new Rectangle(350, 10, 140, 26) };
        form.Controls.AddRange(new Control[] { travel, accel, unit, status });
        form.Show(); Application.DoEvents();
        Func<string, int> read = a => { int v; sim.TryRead(a, out v); return v; };
        foreach (var c in new[] { travel, accel, unit, status }) c.UpdateState(read, x => true);
        Check("shown as before: 10.0000 mm, 1000 ms", travel.ShownText == "10.0000 mm" && accel.ShownText == "1000 ms", travel.ShownText + " | " + accel.ShownText);
        SecurityManager.Login("op", SecurityLevel.Operator);
        Check("an Operator: a display", !travel.Editable && !accel.Editable);
        SecurityManager.Login("boss", SecurityLevel.Admin);
        Check("an Admin: a field (a monitor value stays a display)", travel.Editable && accel.Editable && unit.Editable && !status.Editable);
        Check("  the keypad starts at the number shown: 10.0000", (string)Call(travel, "KeypadText") == "10.0000", (string)Call(travel, "KeypadText"));

        answer = DialogResult.No;
        answers.Clear();
        bool ok = (bool)Call(travel, "ApplyTyped", "12.5");
        Check("12.5 typed: U1\\G154/155 = 125000, the cell shows it", ok && (ushort)read(@"U1\G154") + (read(@"U1\G155") << 16) == 125000 &&
              travel.ShownText == "12.5000 mm", read(@"U1\G154") + " " + read(@"U1\G155") + " " + travel.ShownText);
        Check("  then: when the module takes it (PLC READY), that it is lost at power off - the flash ROM offered",
              answers.Count == 1 && answers[0].Contains("12.5000 mm written") && answers[0].Contains("PLC READY signal (Cd.190) turns on again") &&
              answers[0].Contains("flash ROM") && answers[0].Contains("Write the module's flash ROM now?"), answers.FirstOrDefault());
        Check("  No: the flash ROM not asked for", read(@"U1\G5900") == 0 && read(@"U1\G4224") == 0);
        var log = PlcWriteLog.Read(DateTime.Now.AddMinutes(-1), DateTime.Now.AddMinutes(1));
        PlcWriteLog.Flush();
        log = PlcWriteLog.Read(DateTime.Now.AddMinutes(-1), DateTime.Now.AddMinutes(1));
        Check("  in the PLC write log as the Admin's", log.Any(e => e.Device == @"U1\G154" && e.User == "boss" && (e.Source ?? "").Contains("Pr.3 typed in")),
              string.Join(" | ", log.Where(e => e.Device.StartsWith("U1")).Select(e => e.Device + " " + e.User + " " + e.Source)));

        answers.Clear();
        ok = (bool)Call(accel, "ApplyTyped", "0");
        Check("0 ms typed: out of range - not written, said", !ok && read(@"U1\G162") == 1000 && MotionFlash.LastMessage.Contains("between 1 and 8388608"), MotionFlash.LastMessage);

        Console.WriteLine("--- the flash ROM ---");
        sim.Write(@"U1\G5950", 1);       // PLC READY on
        answer = DialogResult.Yes;
        answers.Clear();
        ok = (bool)Call(accel, "ApplyTyped", "750");
        Check("750 ms typed, Yes to the flash ROM - PLC READY on: not written, the way out said",
              ok && read(@"U1\G162") == 750 && answers.Count == 2 && answers[0].Contains("from the axis's next start") &&
              answers[1].Contains("only while its PLC READY signal (Cd.190, U1\\G5950) is off") && read(@"U1\G4224") == 0,
              string.Join(" // ", answers));
        sim.Write(@"U1\G5950", 0);
        answers.Clear();
        var r = MotionFlash.Write(form, 23, 1);
        Check("PLC READY off: G5900 = 1, the module answers (back to 0), Md.19 = 1", r == SimpleMotion.FlashResult.Written &&
              read(@"U1\G5900") == 0 && read(@"U1\G4224") == 1 && answers.Last().Contains("flash ROM written (1 of 25"), answers.LastOrDefault());
        sim.Write(@"U1\G4224", 25);
        r = MotionFlash.Write(form, 23, 1);
        Check("25 writes since power on: not asked again", r == SimpleMotion.FlashResult.TooManyWrites && answers.Last().Contains("Switch it off and on first"), answers.Last());
        int writes;
        sim.Write(@"U1\G4224", 0);
        var stuck = new SimFlashStuck(sim);
        r = SimpleMotion.WriteFlashRom(stuck, 1, out writes, ms => { }, 500);
        Check("a module that does not answer: timed out", r == SimpleMotion.FlashResult.TimedOut);

        Console.WriteLine("--- the unit setting ---");
        answer = DialogResult.No;
        Call(unit, "WriteRaw", 2L);
        unit.UpdateState(read, x => true);
        Check("the unit setting chosen from the list: degree (2)", read(@"U1\G150") == 2 && unit.ShownText == "°", unit.ShownText);

        SecurityManager.Logout();
        form.Close();
        Console.WriteLine(fail == 0 ? "\nMOTION PARAMETERS ALL PASS" : "\nMOTION PARAMETERS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    /// <summary>A module that takes the request and never answers it.</summary>
    class SimFlashStuck : Kaban.Core.Interfaces.IPlcDriver {
        readonly Kaban.Drivers.Simulation.SimulationDriver _s;
        public SimFlashStuck(Kaban.Drivers.Simulation.SimulationDriver s) { _s = s; }
        public bool IsConnected { get { return true; } }
        public bool IsHealthy { get { return true; } }
        public bool Connect() { return true; }
        public void Disconnect() { }
        public int Read(string a) { return _s.Read(a); }
        public int[] ReadMulti(string[] a) { return _s.ReadMulti(a); }
        public bool TryRead(string a, out int v) { if (a.EndsWith("G5900")) { v = 1; return true; } return _s.TryRead(a, out v); }
        public bool TryReadMulti(string[] a, out int[] v) { return _s.TryReadMulti(a, out v); }
        public int Write(string a, int v) { return 0; }
    }
}
