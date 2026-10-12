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

// THE MACHINE CONDITIONS on the PLC Monitor (30 September): the conditions the
// machines register (MachineConditionMap) added to a tab - each one's device,
// named after it - with a Condition column saying whether it is met now.
class MC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static Type MonType = typeof(SimulationMonitorForm);
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
        var p = t.GetProperty(n, Any); return p.GetValue(o is Type ? null : o, null);
    }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length >= a.Length &&
                                              m.GetParameters().Skip(a.Length).All(p => p.IsOptional));
        var args = a.Concat(mi.GetParameters().Skip(a.Length).Select(p => p.DefaultValue)).ToArray();
        return mi.Invoke(o is Type ? null : o, args);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }

    /// <summary>Closes the "Added n device(s)" message the collection shows, and keeps its text.</summary>
    static string lastMessage = "";
    static void WithMessagesClosed(Action a) {
        var t = new Timer { Interval = 150 };
        t.Tick += (s, e) => {
            foreach (Form f in Application.OpenForms.Cast<Form>().ToList())
                if (f.GetType().Name.Contains("MessageBox")) {
                    lastMessage = string.Join(" ", f.Controls.Cast<Control>().Select(c => c.Text));
                    f.Close();
                }
        };
        t.Start();
        try { a(); } finally { t.Stop(); t.Dispose(); }
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "moncond_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        MachineConditionMap.Clear();
        MachineConditionMap.Register(SystemCondition.ManualMode, 21, "M100", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        MachineConditionMap.Register(SystemCondition.AutoMode, 21, "D300", DataTypeEnum.Bin_16, ComparisonOperator.LessOrEqual, 3);
        MachineConditionMap.Register(SystemCondition.MachineIsRunning, 21, "D200", DataTypeEnum.Bin_32, ComparisonOperator.Greater, 0);
        MachineConditionMap.RegisterMachineBit(1L << 16, "T.RobotIsOutside", "Robot is at Outside", 21, "X50");
        MachineConditionMap.Register(SystemCondition.RobotIsSafe, 22, "X20");      // another controller's

        Console.WriteLine("--- the list of conditions ---");
        var all = MachineConditionMap.All();
        Check("every registered condition, system ones first", all.Count == 5 && all[0].Key == "S:ManualMode" && all.Last().Key == "B:T.RobotIsOutside",
              string.Join(", ", all.Select(e => e.Key)));
        Check("a rule in words", MachineConditionMap.RuleText(MachineConditionMap.Find("S:AutoMode").Rule) == "D300 \u2264 3" &&
              MachineConditionMap.RuleText(MachineConditionMap.Find("S:MachineIsRunning").Rule) == "D200 (32-bit) > 0");
        double v;
        var reads = new Dictionary<string, int> { { "D200", 0 }, { "D201", 1 } };
        Check("evaluated as the program does: D200/D201 = 65536 > 0", MachineConditionMap.Evaluate(MachineConditionMap.Find("S:MachineIsRunning").Rule,
              a => reads.ContainsKey(a) ? reads[a] : (int?)null, out v) == true && v == 65536, v.ToString());
        Check("a device not read: not known", MachineConditionMap.Evaluate(MachineConditionMap.Find("S:AutoMode").Rule, a => null, out v) == null);

        Console.WriteLine("--- into the monitor ---");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(21);
        PlcManager.Instance.RegisterDriver(21, sim);
        PlcManager.Instance.RegisterStation(21, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(21, true);
        sim.Write("M100", 0); sim.Write("D300", 5); sim.Write("D200", 7); sim.Write("D201", 0); sim.Write("X50", 0);

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
        Check("a heading, then a row per condition of this controller (4 of 5)",
              (string)Get(rows[0], "Comment") == "Machine conditions" && cond.Count == 4, string.Join(" | ", rows.Select(r => Get(r, "Address") + "/" + Get(r, "Comment"))));
        Check("  named after the condition, in its data type", (string)Get(cond[2], "LabelOverride") == "Machine is Running" &&
              (string)Get(cond[2], "DataTypeOverride") == "Double Word [Signed]" && (string)Get(cond[3], "DataTypeOverride") == "Bit" &&
              (string)Get(cond[1], "DataTypeOverride") == "Word [Signed]",
              string.Join(" | ", cond.Select(r => Get(r, "LabelOverride") + ":" + Get(r, "DataTypeOverride"))));
        Check("  and it said how many (a message)", lastMessage.Length > 0, lastMessage);

        Call(form, "RefreshValues"); Pump(100); Call(form, "RefreshValues"); Pump(50);
        var grid = (DataGridView)Get(sh, "Grid");
        Func<string, string> state = addr => {
            int i = rows.FindIndex(r => (string)Get(r, "Address") == addr && ((string)Get(r, "ConditionKey")).Length > 0);
            return Convert.ToString(grid.Rows[i].Cells["Condition"].Value);
        };
        Check("the Condition column is there", grid.Columns["Condition"].Visible);
        Check("manual mode (M100 = 0): met", state("M100") == "\u2714 met", state("M100"));
        Check("auto mode (D300 = 5, asks <= 3): not met", state("D300") == "\u2716 not met", state("D300"));
        Check("running (D200 = 7 > 0): met", state("D200") == "\u2714 met", state("D200"));
        Check("robot outside (X50 = 0, asks 1): not met", state("X50") == "\u2716 not met", state("X50"));
        int iAuto = rows.FindIndex(r => (string)Get(r, "Address") == "D300");
        string tip = grid.Rows[iAuto].Cells["Condition"].ToolTipText;
        Check("  the tooltip: the name, what it asks, what is read", tip.Contains("Auto Mode") && tip.Contains("D300 \u2264 3") && tip.Contains("5"), tip);
        sim.Write("D300", 2);
        Call(form, "RefreshValues"); Pump(50);
        Check("D300 set to 2: met", state("D300") == "\u2714 met", state("D300"));

        WithMessagesClosed(() => Call(form, "CollectConditions"));
        int again = ((IList)Get(sh, "Rows")).Cast<object>().Count(r => ((string)Get(r, "ConditionKey")).Length > 0);
        Check("added again: no row twice", again == 4, again.ToString());

        Call(form, "SortSheet", mon, sh, "Condition");
        var sorted = ((IList)Get(sh, "Rows")).Cast<object>().Where(r => ((string)Get(r, "ConditionKey")).Length > 0).Select(r => (string)Get(r, "Address")).ToList();
        Check("sorted on Condition: met first", sorted.Take(3).All(a => a != "X50") && sorted.Last() == "X50", string.Join(",", sorted));

        var lines = ((IEnumerable<string>)Call(MonType, "Serialize", mon)).ToList();
        Check("kept with the list (cond=)", lines.Any(l => l.Contains("cond=S:AutoMode")), string.Join(" / ", lines.Where(l => l.Contains("D300"))));
        form.Close(); Application.DoEvents();

        var form2 = new SimulationMonitorForm();
        form2.Show(); Application.DoEvents();
        var mon2 = ((IList)Get(form2, "_monitors")).Cast<object>().First(m => (int)Get(m, "Station") == 21);
        ((TabControl)Get(form2, "_tabs")).SelectedIndex = ((IList)Get(form2, "_monitors")).IndexOf(mon2);
        Call(form2, "RefreshValues"); Pump(100);
        var sh2 = Get(mon2, "Current");
        var grid2 = (DataGridView)Get(sh2, "Grid");
        int k = ((IList)Get(sh2, "Rows")).Cast<object>().ToList().FindIndex(r => (string)Get(r, "Address") == "M100");
        Check("opened again: the conditions and their state", grid2.Columns["Condition"].Visible && k >= 0 &&
              Convert.ToString(grid2.Rows[k].Cells["Condition"].Value) == "\u2714 met");

        // a tab without conditions has no Condition column
        Call(form2, "AddSheet", mon2, "Plain", ((IList)Get(mon2, "Sheets")).Count);
        Application.DoEvents();
        var plain = Get(mon2, "Current");
        Call(form2, "RefreshValues"); Pump(50);
        Check("a tab without conditions: no Condition column", (string)Get(plain, "Name") == "Plain" && !((DataGridView)Get(plain, "Grid")).Columns["Condition"].Visible,
              (string)Get(plain, "Name"));

        MachineConditionMap.Clear();
        Call(form2, "RefreshValues"); Pump(50);
        ((TabControl)Get(mon2, "SheetTabs")).SelectedIndex = 0; Application.DoEvents();
        Call(form2, "RefreshValues"); Pump(50);
        Check("a condition no machine registers any more: '?'", Convert.ToString(grid2.Rows[k].Cells["Condition"].Value) == "?");
        form2.Close();

        Console.WriteLine(fail == 0 ? "\nMONITOR CONDITIONS ALL PASS" : "\nMONITOR CONDITIONS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
