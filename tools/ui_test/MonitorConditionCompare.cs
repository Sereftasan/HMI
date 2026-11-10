using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Forms;

// A CONDITION COMPARING TWO DEVICES (10 November: "Sometime the conditions could be entered as D100 >= D700 maybe (an axis position is at one
// side of the parameter or not). Can you write a condition this way in FA2010 for me so I can see how to introduce it this way"): a device
// compared with another device's value instead of a number - in words, judged by the running program and on the PLC Monitor (a line for
// the device compared with, its value), read by the scanner; FA2010's example (the Robot X axis's position W0 >= its reference D316).
class MCC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static Type MonType = typeof(SimulationMonitorForm);
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        while (t != null) {
            var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o is Type ? null : o, null);
            t = t.BaseType;
        }
        throw new MissingMemberException(n);
    }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length >= a.Length &&
                                              m.GetParameters().Skip(a.Length).All(p => p.IsOptional));
        var args = a.Concat(mi.GetParameters().Skip(a.Length).Select(p => p.DefaultValue)).ToArray();
        return mi.Invoke(o is Type ? null : o, args);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static void WithMessagesClosed(Action a) {
        var t = new Timer { Interval = 150 };
        t.Tick += (s, e) => {
            foreach (Form f in Application.OpenForms.Cast<Form>().ToList())
                if (f.GetType().Name.Contains("MessageBox")) f.Close();
        };
        t.Start();
        try { a(); } finally { t.Stop(); t.Dispose(); }
    }
    static string Desc(object r) { return Get(r, "Address") + "/" + Get(r, "ConditionPart") + ((bool)Get(r, "ConditionCompare") ? "c" : ""); }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "moncmp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        Console.WriteLine("--- FA2010's example ---");
        var fa = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly;
        var prog = fa.GetType("Kaban.Machines.FA2010.Program");
        prog.GetMethod("RegisterDriverKinds", Any).Invoke(null, null);
        prog.GetMethod("MountMachines", Any).Invoke(null, null);
        var robot = MachineConditionMap.GetAllMachineBits().FirstOrDefault(kv => kv.Value.TagName != null && kv.Value.TagName.EndsWith("RobotXPastReference"));
        Check("FA2010: Robot X at or past its Reference Position - bit 27, after the others; W0 (32-bit) >= D316",
              robot.Key == 1L << 27 && robot.Value.Address == "W0" && robot.Value.CompareAddress == "D316" && robot.Value.DataType == DataTypeEnum.Bin_32 &&
              robot.Value.Operator == ComparisonOperator.GreaterOrEqual && MachineConditionMap.RuleText(robot.Value) == "W0 (32-bit) ≥ D316" &&
              MachineConditionMap.GetAllMachineBits().Max(kv => kv.Key) == 1L << 27,
              robot.Key + " " + MachineConditionMap.RuleText(robot.Value));
        Check("  the earlier ones keep their numbers (Air Exhaust 26)", MachineConditionMap.GetAllMachineBits().First(kv => kv.Value.TagName.EndsWith("CuttingLeftFrontAirExhaust")).Key == 1L << 26);
        var mem = new Dictionary<string, int> { { "W0", 2000 }, { "W1", 0 }, { "D316", 1500 }, { "D317", 0 } };
        Func<string, int?> read = a => mem.ContainsKey(a) ? mem[a] : (int?)null;
        Func<string, int> look = a => mem.ContainsKey(a) ? mem[a] : 0;
        double v;
        Check("  W0 2000, D316 1500: met (the value W0's)", MachineConditionMap.Evaluate(robot.Value, read, out v) == true && v == 2000 && MachineConditionMap.CheckMachineBit(robot.Key, look));
        mem["W0"] = 1000;
        Check("  W0 1000: not met", MachineConditionMap.Evaluate(robot.Value, read, out v) == false && !MachineConditionMap.CheckMachineBit(robot.Key, look));
        mem["W0"] = 1500;
        Check("  W0 = D316: met (>=)", MachineConditionMap.CheckMachineBit(robot.Key, look));
        mem["D317"] = 1;                       // D316/D317 = 65536 + 1500
        Check("  a double word: D317 the high word of the parameter - 67036, W0 1500 not past it", !MachineConditionMap.CheckMachineBit(robot.Key, look) &&
              MachineConditionMap.ReadValue(robot.Value, "D316", read) == 67036);
        mem.Remove("D316");
        Check("  the parameter not read yet: not known", MachineConditionMap.Evaluate(robot.Value, read, out v) == null);
        Check("  read: W0 W1 D316 D317", MachineConditionMap.AddressesOf(robot.Value).SequenceEqual(new[] { "W0", "W1", "D316", "D317" }),
              string.Join(",", MachineConditionMap.AddressesOf(robot.Value)));

        Console.WriteLine("--- the forms of it ---");
        MachineConditionMap.Clear();
        MachineConditionMap.Register(SystemCondition.ManualMode, 21, "D100", DataTypeEnum.Bin_16, ComparisonOperator.GreaterOrEqual, "D700");
        MachineConditionMap.RegisterMachineBit(1L << 16, "T.Window", "Axis Inside its Window", 21, "D110", DataTypeEnum.Bin_16, ComparisonOperator.GreaterOrEqual, "D710");
        MachineConditionMap.AddMachineBitDevice(1L << 16, "D110", DataTypeEnum.Bin_16, ComparisonOperator.LessOrEqual, "D712");
        MachineConditionMap.RegisterMachineBit(1L << 17, "T.RobotIsOutside", "Robot is at Outside", 21, "X50");
        var manual = MachineConditionMap.Find("S:ManualMode").Rule;
        var window = MachineConditionMap.Find("B:T.Window").Rule;
        Check("a system condition D100 >= D700; a machine's of two tests D710 <= D110 <= D712", MachineConditionMap.RuleText(manual) == "D100 ≥ D700" &&
              MachineConditionMap.RuleText(window) == "D110 ≥ D710 and D110 ≤ D712", MachineConditionMap.RuleText(window));
        var mem2 = new Dictionary<string, int> { { "D100", 5 }, { "D700", 7 }, { "D110", 50 }, { "D710", 40 }, { "D712", 60 }, { "X50", 1 } };
        Func<string, int> look2 = a => mem2.ContainsKey(a) ? mem2[a] : 0;
        var scanner = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Kaban.UI.Plc.PlcScanner));
        var check = typeof(Kaban.UI.Plc.PlcScanner).GetMethod("CheckGlobalCondition", Any);
        Func<SystemCondition, bool> judged = c => (bool)check.Invoke(scanner, new object[] { c, (Func<int, Func<string, int>>)(st => st == 21 ? look2 : null) });
        Check("the running program: D100 5 < D700 7 - not met; 50 between 40 and 60 - met", !judged(SystemCondition.ManualMode) && judged((SystemCondition)(1L << 16)));
        mem2["D700"] = 5; mem2["D110"] = 61;
        Check("  D700 5: met; D110 61 past D712 - not", judged(SystemCondition.ManualMode) && !judged((SystemCondition)(1L << 16)));
        MachineConditionMap.ReadAlways(SystemCondition.ManualMode | (SystemCondition)(1L << 16));
        var sc = new Kaban.UI.Plc.PlcScanner(new UserControl());
        sc.RefreshTags();
        var batch = (Dictionary<int, string[]>)typeof(Kaban.UI.Plc.PlcScanner).GetField("_plcBatch", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sc);
        Check("  the scanner reads the devices compared with too", batch.ContainsKey(21) && new[] { "D100", "D700", "D110", "D710", "D712" }.All(a => batch[21].Contains(a)),
              batch.ContainsKey(21) ? string.Join(",", batch[21]) : "none");
        sc.Dispose();

        Console.WriteLine("--- the PLC Monitor ---");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(21);
        PlcManager.Instance.RegisterDriver(21, sim);
        PlcManager.Instance.RegisterStation(21, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(21, true);
        foreach (var kv in mem2) sim.Write(kv.Key, kv.Value);
        var form = new SimulationMonitorForm();
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 21);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        Application.DoEvents();
        WithMessagesClosed(() => Call(form, "CollectConditions"));
        var sh = Get(mon, "Current");
        var rows = ((IList)Get(sh, "Rows")).Cast<object>().ToList();
        var cond = rows.Where(r => ((string)Get(r, "ConditionKey")).Length > 0).ToList();
        Check("a line for the device each test compares with, under the device's",
              cond.Select(Desc).SequenceEqual(new[] { "D100/0", "D700/0c", "D110/0", "D710/0c", "D110/1", "D712/1c", "X50/0" }), string.Join(" | ", cond.Select(Desc)));
        Check("  named ↳ ... compared with", (string)Get(cond[1], "LabelOverride") == "↳ Manual Mode Active - compared with" ||
              ((string)Get(cond[1], "LabelOverride")).StartsWith("↳ ") && ((string)Get(cond[1], "LabelOverride")).EndsWith("compared with"),
              (string)Get(cond[1], "LabelOverride"));
        Call(form, "RefreshValues"); Pump(100); Call(form, "RefreshValues"); Pump(50);
        var grid = (DataGridView)Get(sh, "Grid");
        rows = ((IList)Get(sh, "Rows")).Cast<object>().ToList();
        Func<string, string, string> cell = (d, col) => {
            int i = rows.FindIndex(r => Desc(r) == d);
            return Convert.ToString(grid.Rows[i].Cells[col].Value);
        };
        Check("the values: D100 5, D700 5 on its line", cell("D100/0", "Value") == "5" && cell("D700/0c", "Value") == "5", cell("D100/0", "Value") + " " + cell("D700/0c", "Value"));
        Check("Met when: D100 ≥ D700; the line compared with: compared with D100", cell("D100/0", "Rule") == "D100 ≥ D700" && cell("D700/0c", "Rule") == "compared with D100",
              cell("D100/0", "Rule") + " | " + cell("D700/0c", "Rule"));
        Check("Condition: met; nothing on the line compared with", cell("D100/0", "Condition") == "✔ met" && cell("D700/0c", "Condition") == "" &&
              cell("D110/0", "Condition") == "✖ not met" && cell("D110/1", "Condition") == "✖ not met" && cell("D712/1c", "Condition") == "",
              cell("D100/0", "Condition") + " | " + cell("D700/0c", "Condition") + " | " + cell("D110/0", "Condition") + " | " + cell("D110/1", "Condition"));
        sim.Write("D712", 70);
        Call(form, "RefreshValues"); Pump(50);
        Check("  D712 set to 70: the window met", cell("D110/0", "Condition") == "✔ met" && cell("D110/1", "Condition") == "✔ met" && cell("D712/1c", "Value") == "70");
        var lines = ((IEnumerable<string>)Call(MonType, "Serialize", mon)).ToList();
        Check("kept with the list (cmp=1)", lines.Any(l => l.Contains("D712") && l.Contains("part=1") && l.Contains("cmp=1")), string.Join(" / ", lines.Where(l => l.Contains("D712"))));
        var sent = (HashSet<string>)Call(MonType, "MachineConditionAddresses", 21);
        Check("  the devices compared with kept off the tab HMI -> PLC too", sent.Contains("D700") && sent.Contains("D712"));
        form.Close(); Application.DoEvents();

        // the machine changes: the window's upper limit now D714, manual mode a number again - the list made before put right as it opens
        MachineConditionMap.Clear();
        MachineConditionMap.Register(SystemCondition.ManualMode, 21, "D100", DataTypeEnum.Bin_16, ComparisonOperator.GreaterOrEqual, 3);
        MachineConditionMap.RegisterMachineBit(1L << 16, "T.Window", "Axis Inside its Window", 21, "D110", DataTypeEnum.Bin_16, ComparisonOperator.GreaterOrEqual, "D710");
        MachineConditionMap.AddMachineBitDevice(1L << 16, "D110", DataTypeEnum.Bin_16, ComparisonOperator.LessOrEqual, "D714");
        MachineConditionMap.RegisterMachineBit(1L << 17, "T.RobotIsOutside", "Robot is at Outside", 21, "X50");
        var form2 = new SimulationMonitorForm();
        form2.Show(); Application.DoEvents();
        var mon2 = ((IList)Get(form2, "_monitors")).Cast<object>().First(m => (int)Get(m, "Station") == 21);
        var cond2 = ((IList)Get(Get(mon2, "Current"), "Rows")).Cast<object>().Where(r => ((string)Get(r, "ConditionKey")).Length > 0).ToList();
        Check("a list made before: D700's line off (a number now), D712's line the new D714",
              cond2.Select(Desc).SequenceEqual(new[] { "D100/0", "D110/0", "D710/0c", "D110/1", "D714/1c", "X50/0" }), string.Join(" | ", cond2.Select(Desc)));
        form2.Close();

        MachineConditionMap.Clear();
        Console.WriteLine(fail == 0 ? "\nMONITOR CONDITION COMPARE ALL PASS" : "\nMONITOR CONDITION COMPARE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
