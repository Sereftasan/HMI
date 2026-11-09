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

// A MACHINE CONDITION OF SEVERAL DEVICES (7 November: "Each machine condition might have multiple device address so it should be able to
// show all of their values somehow (either side by side or adding more line to show the following device value"): the further devices
// registered with it (MachineConditionMap.AddDevice / AddMachineBitDevice) - met while all of them are (AnyDevice: one), read by the
// scanner, judged by the running program - and on the PLC Monitor a line for each further device under the condition's: its value, what
// it asks, whether it is met; the condition's line the whole condition. Kept with the list, put right for a list made before.
class MCD {
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

    static void Register() {
        MachineConditionMap.Clear();
        MachineConditionMap.Register(SystemCondition.ManualMode, 21, "M100", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        MachineConditionMap.AddDevice(SystemCondition.ManualMode, "X5", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 1);
        MachineConditionMap.RegisterMachineBit(1L << 16, "T.MillingUnitInside", "Milling Unit is NOT Inside", 21, "M22", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        MachineConditionMap.AddMachineBitDevice(1L << 16, "M23", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        MachineConditionMap.AddMachineBitDevice(1L << 16, "D400", DataTypeEnum.Bin_32, ComparisonOperator.Greater, 100);
        MachineConditionMap.RegisterMachineBit(1L << 17, "T.RobotIsOutside", "Robot is at Outside", 21, "X50");
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "moncondev_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        Register();

        Console.WriteLine("--- the model ---");
        var milling = MachineConditionMap.Find("B:T.MillingUnitInside");
        var manual = MachineConditionMap.Find("S:ManualMode");
        Check("a further device registered after its condition: on the condition's PLC", milling.Rule.Also.Count == 2 && milling.Rule.Also.All(d => d.PlcNumber == 21) &&
              manual.Rule.Also.Count == 1 && !MachineConditionMap.AddMachineBitDevice(1L << 40, "M1", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 1),
              milling.Rule.Also.Count + " " + manual.Rule.Also.Count);
        Check("  in words: every device's test (\"and\")", MachineConditionMap.RuleText(milling.Rule) == "M22 = 0 and M23 = 0 and D400 (32-bit) > 100" &&
              MachineConditionMap.DeviceText(milling.Rule) == "M22 = 0", MachineConditionMap.RuleText(milling.Rule));
        var mem = new Dictionary<string, int> { { "M22", 0 }, { "M23", 0 }, { "D400", 0 }, { "D401", 1 }, { "M100", 0 }, { "X5", 1 }, { "X50", 0 } };
        Func<string, int?> read = a => mem.ContainsKey(a) ? mem[a] : (int?)null;
        Func<string, int> look = a => mem.ContainsKey(a) ? mem[a] : 0;
        double v;
        Check("met while all are (D400/D401 = 65536 > 100): the value its first device's", MachineConditionMap.Evaluate(milling.Rule, read, out v) == true && v == 0 &&
              MachineConditionMap.CheckMachineBit(1L << 16, look) && MachineConditionMap.Check(milling.Rule, look));
        mem["M23"] = 1;
        Check("  one not: not met (the running program's test too)", MachineConditionMap.Evaluate(milling.Rule, read, out v) == false && !MachineConditionMap.CheckMachineBit(1L << 16, look));
        Check("  that device's own test", MachineConditionMap.EvaluateDevice(milling.Rule.Also[0], read, out v) == false && v == 1 &&
              MachineConditionMap.EvaluateDevice(milling.Rule, read, out v) == true);
        mem.Remove("D401");
        Check("  one not met and one not read: not met (it cannot be)", MachineConditionMap.Evaluate(milling.Rule, read, out v) == false);
        mem["M23"] = 0;
        Check("  the rest met, one not read: not known", MachineConditionMap.Evaluate(milling.Rule, read, out v) == null);
        mem["D401"] = 1;
        milling.Rule.AnyDevice = true;
        mem["M22"] = 1; mem["M23"] = 1; mem["D401"] = 0; mem["D400"] = 50;
        Check("AnyDevice: none met - not met, \"or\" in words", MachineConditionMap.Evaluate(milling.Rule, read, out v) == false && !MachineConditionMap.Check(milling.Rule, look) &&
              MachineConditionMap.RuleText(milling.Rule) == "M22 = 0 or M23 = 0 or D400 (32-bit) > 100");
        mem["D400"] = 101;
        Check("  one met: met", MachineConditionMap.Evaluate(milling.Rule, read, out v) == true && MachineConditionMap.Check(milling.Rule, look));
        milling.Rule.AnyDevice = false;
        mem["M22"] = 0; mem["M23"] = 0; mem["D400"] = 0; mem["D401"] = 1;

        Console.WriteLine("--- the running program ---");
        var scanner = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Kaban.UI.Plc.PlcScanner));
        var check = typeof(Kaban.UI.Plc.PlcScanner).GetMethod("CheckGlobalCondition", Any);
        Func<SystemCondition, bool> judged = c => (bool)check.Invoke(scanner, new object[] { c, (Func<int, Func<string, int>>)(st => st == 21 ? look : null) });
        Check("a control's condition: the system condition of two devices met (M100 = 0, X5 = 1)", judged(SystemCondition.ManualMode));
        mem["X5"] = 0;
        Check("  X5 off: not met", !judged(SystemCondition.ManualMode));
        mem["X5"] = 1;
        Check("  a machine bit of three: met; M23 on - not", judged((SystemCondition)(1L << 16)) && ((Func<bool>)(() => { mem["M23"] = 1; bool r = judged((SystemCondition)(1L << 16)); mem["M23"] = 0; return !r; }))());
        MachineConditionMap.ReadAlways(SystemCondition.ManualMode | (SystemCondition)(1L << 16));
        var page = new UserControl();
        var sc = new Kaban.UI.Plc.PlcScanner(page);
        sc.RefreshTags();
        var batch = (Dictionary<int, string[]>)typeof(Kaban.UI.Plc.PlcScanner).GetField("_plcBatch", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sc);
        Check("  the scanner reads every device (D400 with D401)", batch.ContainsKey(21) && new[] { "M100", "X5", "M22", "M23", "D400", "D401" }.All(a => batch[21].Contains(a)),
              batch.ContainsKey(21) ? string.Join(",", batch[21]) : "no station 21");
        sc.Dispose();

        Console.WriteLine("--- the PLC Monitor ---");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(21);
        PlcManager.Instance.RegisterDriver(21, sim);
        PlcManager.Instance.RegisterStation(21, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(21, true);
        foreach (var kv in mem) sim.Write(kv.Key, kv.Value);
        sim.Write("M23", 1);

        var form = new SimulationMonitorForm();
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 21);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        Application.DoEvents();
        WithMessagesClosed(() => Call(form, "CollectConditions"));
        var sh = Get(mon, "Current");
        Func<List<object>> rowsNow = () => ((IList)Get(sh, "Rows")).Cast<object>().ToList();
        var rows = rowsNow();
        var cond = rows.Where(r => ((string)Get(r, "ConditionKey")).Length > 0).ToList();
        Func<object, string> desc = r => Get(r, "Address") + ":" + Get(r, "ConditionPart") + ":" + Get(r, "LabelOverride");
        Check("each condition's line, a line under it for each further device (manual mode: X5; milling: M23, D400)",
              cond.Select(r => (string)Get(r, "Address") + "/" + Get(r, "ConditionPart")).SequenceEqual(new[] { "M100/0", "X5/1", "M22/0", "M23/1", "D400/2", "X50/0" }),
              string.Join(" | ", cond.Select(desc)));
        Check("  named ↳ and the condition's name and number (no label of its own), in its data type",
              (string)Get(cond[3], "LabelOverride") == "↳ Milling Unit is NOT Inside (2)" && (string)Get(cond[4], "LabelOverride") == "↳ Milling Unit is NOT Inside (3)" &&
              (string)Get(cond[4], "DataTypeOverride") == "Double Word [Signed]" && (string)Get(cond[3], "DataTypeOverride") == "Bit",
              string.Join(" | ", cond.Select(r => Get(r, "LabelOverride") + ":" + Get(r, "DataTypeOverride"))));

        Call(form, "RefreshValues"); Pump(100); Call(form, "RefreshValues"); Pump(50);
        var grid = (DataGridView)Get(sh, "Grid");
        rows = rowsNow();
        Func<string, string, string> cell = (addr, col) => {
            int i = rows.FindIndex(r => (string)Get(r, "Address") == addr && ((string)Get(r, "ConditionKey")).Length > 0);
            return Convert.ToString(grid.Rows[i].Cells[col].Value);
        };
        Check("every device's value on its line (M22 0, M23 1, D400 65536)", cell("M22", "Value") == "0" && cell("M23", "Value") == "1" && cell("D400", "Value") == "65536",
              cell("M22", "Value") + " | " + cell("M23", "Value") + " | " + cell("D400", "Value"));
        Check("Met when: the condition's line every device's, a device's line its own",
              cell("M22", "Rule") == "M22 OFF and M23 OFF and D400 (32-bit) > 100" && cell("M23", "Rule") == "M23 OFF" && cell("D400", "Rule") == "D400 (32-bit) > 100",
              cell("M22", "Rule") + " | " + cell("M23", "Rule") + " | " + cell("D400", "Rule"));
        Check("the Condition column: the whole condition not met (M23 is ON); each device's own - M23 not, D400 met",
              cell("M22", "Condition") == "✖ not met" && cell("M23", "Condition") == "✖ not met" && cell("D400", "Condition") == "✔ met",
              cell("M22", "Condition") + " | " + cell("M23", "Condition") + " | " + cell("D400", "Condition"));
        int iM22 = rows.FindIndex(r => (string)Get(r, "Address") == "M22");
        string tip = grid.Rows[iM22].Cells["Condition"].ToolTipText;
        Check("  the condition's tip: each device met or not, what is read", tip.Contains("Met while all its devices are") && tip.Contains("✔ M22 = 0") &&
              tip.Contains("✖ M23 = 0") && tip.Contains("Now: 1"), tip);
        int iM23 = rows.FindIndex(r => (string)Get(r, "Address") == "M23");
        Check("  a device's tip: its condition, which device of how many", grid.Rows[iM23].Cells["Condition"].ToolTipText.Contains("its device 2 of 3"),
              grid.Rows[iM23].Cells["Condition"].ToolTipText);
        sim.Write("M23", 0);
        Call(form, "RefreshValues"); Pump(50);
        Check("M23 off: all met", cell("M22", "Condition") == "✔ met" && cell("M23", "Condition") == "✔ met");
        WithMessagesClosed(() => Call(form, "CollectConditions"));
        Check("added again: no line twice", rowsNow().Count(r => ((string)Get(r, "ConditionKey")).Length > 0) == 6);
        var sent = (HashSet<string>)Call(MonType, "MachineConditionAddresses", 21);
        Check("the devices the conditions read (kept off the tab HMI -> PLC): the further ones too", new[] { "M100", "X5", "M22", "M23", "D400", "X50" }.All(sent.Contains),
              string.Join(",", sent));

        var lines = ((IEnumerable<string>)Call(MonType, "Serialize", mon)).ToList();
        Check("kept with the list (part=)", lines.Any(l => l.Contains("D400") && l.Contains("cond=B:T.MillingUnitInside") && l.Contains("part=2")),
              string.Join(" / ", lines.Where(l => l.Contains("D400"))));
        form.Close(); Application.DoEvents();

        // opened again: the same lines
        var form2 = new SimulationMonitorForm();
        form2.Show(); Application.DoEvents();
        var mon2 = ((IList)Get(form2, "_monitors")).Cast<object>().First(m => (int)Get(m, "Station") == 21);
        var sh2 = Get(mon2, "Current");
        var cond2 = ((IList)Get(sh2, "Rows")).Cast<object>().Where(r => ((string)Get(r, "ConditionKey")).Length > 0).ToList();
        Check("opened again: the same lines", cond2.Select(r => (string)Get(r, "Address") + "/" + Get(r, "ConditionPart")).SequenceEqual(new[] { "M100/0", "X5/1", "M22/0", "M23/1", "D400/2", "X50/0" }),
              string.Join(" | ", cond2.Select(desc)));
        form2.Close(); Application.DoEvents();

        // the machine now registers another device of the robot's condition, none of manual mode's: a list made before is put right
        MachineConditionMap.Clear();
        MachineConditionMap.Register(SystemCondition.ManualMode, 21, "M100", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        MachineConditionMap.RegisterMachineBit(1L << 16, "T.MillingUnitInside", "Milling Unit is NOT Inside", 21, "M22", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        MachineConditionMap.AddMachineBitDevice(1L << 16, "M23", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        MachineConditionMap.AddMachineBitDevice(1L << 16, "D400", DataTypeEnum.Bin_32, ComparisonOperator.Greater, 100);
        MachineConditionMap.RegisterMachineBit(1L << 17, "T.RobotIsOutside", "Robot is at Outside", 21, "X50");
        MachineConditionMap.AddMachineBitDevice(1L << 17, "X51", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 1);
        var form3 = new SimulationMonitorForm();
        form3.Show(); Application.DoEvents();
        var mon3 = ((IList)Get(form3, "_monitors")).Cast<object>().First(m => (int)Get(m, "Station") == 21);
        var sh3 = Get(mon3, "Current");
        var cond3 = ((IList)Get(sh3, "Rows")).Cast<object>().Where(r => ((string)Get(r, "ConditionKey")).Length > 0).ToList();
        Check("a list made before: manual mode's X5 line off (no such device now), the robot's X51 line put under it",
              cond3.Select(r => (string)Get(r, "Address") + "/" + Get(r, "ConditionPart")).SequenceEqual(new[] { "M100/0", "M22/0", "M23/1", "D400/2", "X50/0", "X51/1" }),
              string.Join(" | ", cond3.Select(desc)));
        form3.Close();

        MachineConditionMap.Clear();
        Console.WriteLine(fail == 0 ? "\nMONITOR CONDITION DEVICES ALL PASS" : "\nMONITOR CONDITION DEVICES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
