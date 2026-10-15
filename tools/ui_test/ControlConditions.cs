using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.Drivers.Simulation;
using Kaban.UI.Controls;

// 14 October: "We need EnableCondition and EnableTrigger for each AxisSettingHMI button (just like
// CylinderUnitHMI) working with AxisUnitHMI too. CylinderUnitHMI's visibility condition only hides the
// buttons inside, the border and the description stay - it should hide the whole control. AxisSettingHMI's
// visibility condition doesn't work for AxisUnitHMI. And CheckBoxHMI should have visibility condition,
// enabling condition and trigger as well (just like ButtonHMI)."
class CN {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static object Get(object o, string n) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, Any); if (f != null) return f.GetValue(o); var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o, null); } return null; }
    static bool Soft(ButtonHMI b) { return (bool)Get(b, "_softEnabled"); }

    [STAThread]
    static void Main() {
        AuditLogger.LogDirectory = Path.Combine(Path.GetTempPath(), "cn_" + Guid.NewGuid().ToString("N"));
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        PlcManager.Instance.RegisterDriver(1, new SimulationDriver(1));
        var sim = (SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(1));
        SecurityManager.Login("admin", SecurityLevel.Admin);
        MachineConditionMap.Clear();
        MachineConditionMap.Register(SystemCondition.ManualMode, 1, "M100", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        var optA = (SystemCondition)(1L << 16);
        MachineConditionMap.RegisterMachineBit(1L << 16, "T.A", "Option A", 1, "L27", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 1);

        var form = new Form { ClientSize = new Size(1500, 500), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var page = new UserControl { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        var cyl = new CylinderUnitHMI { Name = "cyl", TitleTextKey = "Clamp", CommandAAddress = "M30", CommandBAddress = "M31", VisibilityConditions = optA, Location = new Point(10, 10) };
        page.Controls.Add(cyl);
        var cb = new CheckBoxHMI { Name = "cb", Address = "M40", DescriptionText = "Option", VisibilityConditions = optA, EnableConditions = SystemCondition.ManualMode,
                                   Location = new Point(500, 10) };
        cb.EnableTrigger = new PlcTrigger { TriggerType = TriggerType.ON, MonitorAddress = "M41" };
        page.Controls.Add(cb);
        var row = new AxisSettingHMI { Name = "ax", HMIAxisIndex = 7, PlcNumber = 1, AxisNameKey = "Test X", CommandBackAddress = "M50", CommandForwardAddress = "M51",
                                       PositionAddress = "D50", CalibrateAddress = "M52", VisibilityConditions = optA, EnableConditions = SystemCondition.ManualMode,
                                       ForwardEnableConditions = optA, Location = new Point(10, 120) };
        row.BackEnableTrigger = new PlcTrigger { TriggerType = TriggerType.ON, MonitorAddress = "M53" };
        page.Controls.Add(row);
        var scanner = new Kaban.UI.Plc.PlcScanner(page);
        scanner.RefreshTags();
        form.Show(); Pump(500);
        row.BuildRow(); scanner.RefreshTags(); Pump(300);

        Console.WriteLine("--- CylinderUnitHMI: the whole unit follows its visibility conditions ---");
        sim.Write("M100", 0); sim.Write("L27", 0); sim.Write("M41", 1); sim.Write("M53", 1); Pump(800);
        Check("Option A off: the unit hidden - frame, title and all (it hid only its buttons)", !cyl.Visible && cyl.HiddenByConditions);
        sim.Write("L27", 1); Pump(800);
        Check("  on: shown again, its buttons with it", cyl.Visible && ((Control)Get(cyl, "_btnA")).Visible);
        var plain = new CylinderUnitHMI { CommandAAddress = "M32" };
        plain.Visible = false;
        plain.UpdateState(a => 0, c => false);
        Check("  a unit without conditions is left as its page has it", !plain.Visible && !plain.HiddenByConditions);

        Console.WriteLine("--- CheckBoxHMI: visibility, enable conditions and trigger, as a ButtonHMI's ---");
        Check("the properties, in the designer's Conditions", TypeDescriptorHas(typeof(CheckBoxHMI), "VisibilityConditions") &&
              TypeDescriptorHas(typeof(CheckBoxHMI), "EnableConditions") && TypeDescriptorHas(typeof(CheckBoxHMI), "EnableTrigger"));
        sim.Write("L27", 1); sim.Write("M100", 0); sim.Write("M41", 1); Pump(800);
        Check("Option A on, manual, M41 on: shown and can be ticked", cb.Visible && cb.CanTick);
        sim.Write("M100", 1); Pump(800);
        Check("  not in manual: greyed, cannot be ticked", cb.Visible && !cb.CanTick);
        Check("  its tooltip says what it waits for", cb.TooltipTextForTests().Contains("Manual Mode Active"), cb.TooltipTextForTests());
        sim.Write("M40", 0); Pump(400);
        typeof(CheckBox).GetMethod("OnClick", Any).Invoke(cb, new object[] { EventArgs.Empty }); Pump(300);
        Check("  a click writes nothing", sim.Read("M40") == 0 && !cb.Checked);
        sim.Write("M100", 0); sim.Write("M41", 0); Pump(800);
        Check("  the trigger M41 off: cannot be ticked, waits for the signal", !cb.CanTick && cb.TooltipTextForTests().Contains("M41"), cb.TooltipTextForTests());
        sim.Write("M41", 1); Pump(800);
        Check("  M41 on: can", cb.CanTick);
        sim.Write("L27", 0); Pump(800);
        Check("  Option A off: hidden", !cb.Visible);

        Console.WriteLine("--- AxisSettingHMI: the row's and each button's own conditions and triggers ---");
        sim.Write("L27", 1); sim.Write("M100", 0); sim.Write("M53", 1); Pump(900);
        var back = (ButtonHMI)Get(row, "_back"); var fwd = (ButtonHMI)Get(row, "_forward"); var cal = (ButtonHMI)Get(row, "_calibrate");
        Check("the jogs and Calibrate carry the row's EnableConditions, each its own added", back.EnableConditions == SystemCondition.ManualMode &&
              fwd.EnableConditions == (SystemCondition.ManualMode | optA) && cal.EnableConditions == SystemCondition.ManualMode && back.EnableTrigger.MonitorAddress == "M53");
        Check("  all met: usable", Soft(back) && Soft(fwd) && Soft(cal));
        sim.Write("M100", 1); Pump(900);
        Check("  not in manual: none usable", !Soft(back) && !Soft(fwd) && !Soft(cal));
        sim.Write("M100", 0); sim.Write("M53", 0); Pump(900);
        Check("  the back jog's trigger M53 off: only the back jog held back", !Soft(back) && Soft(fwd) && Soft(cal));
        sim.Write("M53", 1);

        Console.WriteLine("--- AxisUnitHMI: its row's visibility and the jogs' conditions ---");
        typeof(Kaban.UI.Helpers.AlarmHelpLinks).GetField("_discovered", Any).SetValue(null, true);
        typeof(Kaban.UI.Helpers.AlarmHelpLinks).GetMethod("RegisterAxis", Any).Invoke(null, new object[] { row });
        var unit = new AxisUnitHMI { Name = "unitAx", Location = new Point(10, 200) };
        unit.AxisNumber = 7;
        page.Controls.Add(unit);
        scanner.RefreshTags();
        Check("resolved from the row", unit.IsResolved && unit.Unit.CommandAAddress == "M50");
        Check("  its jogs: the row's conditions and each jog's own, the back jog's trigger",
              unit.Unit.ButtonAEnableConditions == SystemCondition.ManualMode && unit.Unit.ButtonBEnableConditions == (SystemCondition.ManualMode | optA) &&
              unit.Unit.ButtonAEnableTrigger.MonitorAddress == "M53");
        sim.Write("L27", 1); sim.Write("M100", 0); Pump(900);
        Check("  all met: shown, its jogs usable", unit.Visible && Soft(unit.Unit.ButtonA) && Soft(unit.Unit.ButtonB));
        sim.Write("M100", 1); Pump(900);
        Check("  not in manual: its jogs held back", unit.Visible && !Soft(unit.Unit.ButtonA) && !Soft(unit.Unit.ButtonB));
        sim.Write("L27", 0); Pump(900);
        Check("  the row's visibility (Option A) off: the axis unit hidden, whole", !unit.Visible);
        sim.Write("L27", 1); Pump(900);
        Check("  on again: shown", unit.Visible);

        scanner.Stop();
        Console.WriteLine(fail == 0 ? "\nCONTROL CONDITIONS ALL PASS" : "\nCONTROL CONDITIONS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
    static bool TypeDescriptorHas(Type t, string name) {
        var p = System.ComponentModel.TypeDescriptor.GetProperties(t)[name];
        return p != null && p.IsBrowsable && p.Category.TrimStart('\t') == "Conditions";
    }
}
static class CbExt {
    public static string TooltipTextForTests(this CheckBoxHMI cb) {
        return (string)typeof(CheckBoxHMI).GetMethod("TooltipText", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(cb, null);
    }
}
