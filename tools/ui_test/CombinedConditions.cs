using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.Drivers.Simulation;
using Kaban.UI.Controls;

// 14 October: "Multiple selected SystemCondition or machine specific condition bits (RegisterMachineBit(...
// DataTypeEnum type, ComparisonOperator op, int val)) are not working". Several conditions together - the
// shared ones and a machine's own, on another PLC, 32-bit, with an operator - on every kind of control: all of
// them must be met. A sub-machine's page names its own conditions as numbered with the machine on its own;
// mounted, they are given the numbers the mounting gave them. An Admin's tooltip says each one in words.
class CC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static object Get(object o, string n) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, Any); if (f != null) return f.GetValue(o); var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o, null); } return null; }
    class Mod : IMachineModule {
        public string Id; public string[] Tags;
        public string ModuleId { get { return Id; } }
        public string DisplayNameKey { get { return Id; } }
        public void RegisterPlc(IMachineMountContext ctx) { }
        public void RegisterConditions(IMachineMountContext ctx) { foreach (var t in Tags) ctx.AllocateMachineBit(t); }
        public void RegisterData(IMachineMountContext ctx) { }
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx) { yield break; }
    }
    [STAThread]
    static void Main() {
        AuditLogger.LogDirectory = Path.GetTempPath();
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        PlcManager.Instance.RegisterDriver(1, new SimulationDriver(1)); PlcManager.Instance.RegisterDriver(2, new SimulationDriver(2));
        var sim = (SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(1));
        SecurityManager.Login("op", SecurityLevel.Admin);
        MachineConditionMap.Clear();
        MachineConditionMap.Register(SystemCondition.ManualMode, 1, "M100", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        MachineConditionMap.RegisterMachineBit(1L << 16, "T.A", "Option A", 1, "L27", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 1);
        MachineConditionMap.RegisterMachineBit(1L << 17, "T.B", "Count B", 1, "D10", DataTypeEnum.Bin_16, ComparisonOperator.Greater, 5);
        MachineConditionMap.RegisterMachineBit(1L << 18, "T.C", "Wide C (PLC2)", 2, "D100", DataTypeEnum.Bin_32, ComparisonOperator.GreaterOrEqual, 70000);
        var sim2 = (SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(2));
        var both = SystemCondition.ManualMode | (SystemCondition)(1L << 16);
        var three = SystemCondition.ManualMode | (SystemCondition)((1L << 16) | (1L << 18));
        var two = (SystemCondition)((1L << 16) | (1L << 17));
        var form = new Form { ClientSize = new Size(900, 600) };
        var page = new UserControl { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        var ctrls = new List<Control>();
        Func<Control, Control> add = c => { page.Controls.Add(c); ctrls.Add(c); return c; };
        var bVis = (ButtonHMI)add(new ButtonHMI { Name = "bVis", FallbackText = "v", VisibilityConditions = both, Location = new Point(0, 0) });
        var bEn = (ButtonHMI)add(new ButtonHMI { Name = "bEn", FallbackText = "e", EnableConditions = two, Location = new Point(0, 50) });
        bEn.BitActions.Add(new Kaban.Core.Models.BitAction { Address = "M5", Action = BitActionType.Momentary });
        var nVis = (NumericInputHMI)add(new NumericInputHMI { Name = "nVis", Address = "D20", VisibilityConditions = both, Location = new Point(200, 0) });
        var nEn = (NumericInputHMI)add(new NumericInputHMI { Name = "nEn", Address = "D22", EnableConditions = two, Location = new Point(200, 50) });
        var lamp = (LampHMI)add(new LampHMI { Name = "lamp", Address = "M7", VisibilityConditions = two, Location = new Point(400, 0) });
        var grp = (GroupBoxHMI)add(new GroupBoxHMI { Name = "grp", VisibilityConditions = both, Location = new Point(400, 100) });
        var cyl = (CylinderUnitHMI)add(new CylinderUnitHMI { Name = "cyl", CommandAAddress = "M30", CommandBAddress = "M31", VisibilityConditions = both, EnableConditions = two, Location = new Point(0, 200) });
        var disp = (NumericDisplayHMI)add(new NumericDisplayHMI { Name = "disp", Address = "D24", VisibilityConditions = three, Location = new Point(600, 0) });
        var b3 = (ButtonHMI)add(new ButtonHMI { Name = "b3", FallbackText = "3", EnableConditions = three, Location = new Point(600, 50) });
        b3.BitActions.Add(new Kaban.Core.Models.BitAction { Address = "M6", Action = BitActionType.Momentary });
        var pp = (PneumaticPositioningHMI)add(new PneumaticPositioningHMI { Name = "pp", CommandBackAddress = "M40", CommandForwardAddress = "M41", PositionAddress = "D30", VisibilityConditions = three, Location = new Point(0, 300) });
        var ax = (AxisSettingHMI)add(new AxisSettingHMI { Name = "ax", HMIAxisIndex = 1, VisibilityConditions = three, Location = new Point(0, 360) });
        var scanner = new Kaban.UI.Plc.PlcScanner(page);
        scanner.RefreshTags();
        form.Show(); Pump(500);
        Console.WriteLine("--- Manual Mode (M100 = 0) + Option A (L27 = 1) + Count B (D10 > 5) + Wide C (PLC 2, D100 32-bit >= 70000) ---");
        foreach (var st in new[] { new[] { 0, 1, 9, 70000 }, new[] { 1, 1, 9, 70000 }, new[] { 0, 0, 9, 70000 }, new[] { 0, 1, 3, 70000 }, new[] { 1, 0, 0, 5 }, new[] { 0, 1, 9, 69999 } }) {
            sim.Write("M100", st[0]); sim.Write("L27", st[1]); sim.Write("D10", st[2]); sim2.Write("D100", st[3] & 0xFFFF); sim2.Write("D101", st[3] >> 16);
            Pump(900);
            bool manual = st[0] == 0, a = st[1] == 1, b = st[2] > 5, c = st[3] >= 70000;
            string at = string.Format("M100={0} L27={1} D10={2} D100={3}", st[0], st[1], st[2], st[3]);
            Check(at + ": shown only with Manual + A (button, field, group, cylinder unit's buttons): " + (manual && a),
                  bVis.Visible == (manual && a) && nVis.Visible == (manual && a) && grp.Visible == (manual && a) && ((Control)Get(cyl, "_btnA")).Visible == (manual && a));
            Check("  usable only with A + B (button, field): " + (a && b), (bool)Get(bEn, "_softEnabled") == (a && b) && (bool)Get(nEn, "_softEnabled") == (a && b));
            Check("  the lamp shown only with A + B: " + (a && b), lamp.Visible == (a && b));
            Check("  with all four (display, button, pneumatic positioning, axis row): " + (manual && a && c),
                  disp.Visible == (manual && a && c) && (bool)Get(b3, "_softEnabled") == (manual && a && c) && pp.Visible == (manual && a && c) && ax.Visible == (manual && a && c),
                  "disp " + disp.Visible + " b3 " + Get(b3, "_softEnabled") + " pp " + pp.Visible + " ax " + ax.Visible);
        }
        Console.WriteLine("--- an Admin's tooltip says each condition, met or not ---");
        var words = Kaban.UI.Plc.PlcScanner.DescribeConditions(three);
        Check("Manual Mode Active (M100 = 0) + Option A (L27 = 1) + Wide C (D100 (32-bit) >= 70000, PLC 2), the last one not met",
              words.Contains("Manual Mode Active (M100 = 0)") && words.Contains("Option A (L27 = 1)") && words.Contains("Wide C (PLC2) (D100 (32-bit) \u2265 70000, PLC 2)") &&
              words.Contains("\u2717 Wide C") && words.Contains("\u2713 Option A"), words);
        Check("  a condition with no rule on this machine says so", MachineConditionMap.Describe(SystemCondition.SafetyOK).Contains("no rule on this machine"),
              MachineConditionMap.Describe(SystemCondition.SafetyOK));
        scanner.Stop();

        Console.WriteLine("--- a sub-machine's page: its own conditions, numbered as the mounting numbered them ---");
        MachineRegistry.Reset();
        MachineConditionMap.Clear();
        var main = MachineRegistry.MountMain(new Mod { Id = "MAIN", Tags = new[] { "a", "b", "c", "d", "e" } });
        var sub = MachineRegistry.MountSub(new Mod { Id = "SUB", Tags = new[] { "x", "y" } }, new[] { 3 });
        Check("the host's five are 16 - 20, the sub-machine's two 21 and 22", main.Context.AllocateMachineBit("a") == 1L << 16 &&
              sub.Context.AllocateMachineBit("x") == 1L << 21 && sub.Context.AllocateMachineBit("y") == 1L << 22);
        var designed = SystemCondition.ManualMode | (SystemCondition)((1L << 16) | (1L << 17));   // its Manual Mode + its x + its y
        var subPage = new UserControl();
        var sb1 = new ButtonHMI { VisibilityConditions = designed, EnableConditions = (SystemCondition)(1L << 17) };
        var scyl = new CylinderUnitHMI { ButtonAEnableConditions = (SystemCondition)(1L << 16) };
        subPage.Controls.Add(sb1); subPage.Controls.Add(scyl);
        Kaban.UI.Helpers.StationMap.Apply(sub.Context, subPage);
        Check("  its page's 16 and 17 (designed on its own) become 21 and 22; Manual Mode stays",
              (long)sb1.VisibilityConditions == ((long)SystemCondition.ManualMode | (1L << 21) | (1L << 22)) && (long)sb1.EnableConditions == (1L << 22),
              ((long)sb1.VisibilityConditions).ToString());
        Check("  a cylinder unit's button's own too", (long)scyl.ButtonAEnableConditions == (1L << 21), ((long)scyl.ButtonAEnableConditions).ToString());
        var mainPage = new ButtonHMI { VisibilityConditions = designed };
        Kaban.UI.Helpers.StationMap.Apply(main.Context, mainPage);
        Check("  the main machine's own page: as designed", mainPage.VisibilityConditions == designed);

        Console.WriteLine(fail == 0 ? "\nCOMBINED CONDITIONS ALL PASS" : "\nCOMBINED CONDITIONS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
