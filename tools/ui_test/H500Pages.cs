using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.Machines.H500.Screens.Manual;
using Kaban.Machines.H500.Screens.Parameters;

// 12 October: "H500 manual control and machine parameters pages should be updated from this program
// totally" - the H500 FX5 pages of the program the machine runs today (tools/legacy_h500_pages.py, its
// --check against tools/legacy/H500): Entry Conveyor, Heads and Parameters. The old controls' scale, read
// in KabanDevBase: a KabanLedLabel shows raw / (Divisor * 10), a KabanNumericButton raw / Divisor.
class H5P {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }
    static T Named<T>(Control page, string name) where T : Control { return Tree(page).OfType<T>().FirstOrDefault(c => c.Name == name); }
    static string Bits(ButtonHMI b) { return string.Join(",", b.BitActions.Select(a => a.Address + ":" + a.Action)); }

    [STAThread]
    static void Main() {
        AuditLogger.LogDirectory = Path.Combine(Path.GetTempPath(), "h5p_" + Guid.NewGuid().ToString("N"));
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();

        Console.WriteLine("--- Manual Movement > Entry Conveyor (ucScrewingEntryConveyorH500FX) ---");
        var entry = new ManualEntry_Conveyor();
        var units = Tree(entry).OfType<CylinderUnitHMI>().ToList();
        Func<string, CylinderUnitHMI> unit = t => units.FirstOrDefault(u => u.TitleTextKey == t);
        var x1 = unit("X1");
        Check("the servo axes X1 X2 Z1 Z2 Y1 Y2 as units, then Clampings Up/Down",
              new[] { "X1", "X2", "Z1", "Z2", "Y1", "Y2", "Clampings Up/Down" }.All(t => unit(t) != null), string.Join(",", units.Select(u => u.TitleTextKey)));
        Check("  X1: Back M101 with its lamp M2017, Forward M100 (L15 off), position D240 32-bit / 100, 1 decimal",
              x1.CommandAAddress == "M101" && x1.Lamp1AAddress == "M2017" && x1.CommandBAddress == "M100" && string.IsNullOrEmpty(x1.Lamp1BAddress) &&
              x1.PositionAddress == "D240" && x1.PositionDataType == DataTypeEnum.Bin_32 && x1.PositionDivisor == 100 && x1.PositionDecimalPlaces == 1);
        var x2 = unit("X2");
        Check("  X2: its lamp M2018 on Forward (M102), Back M103", x2.CommandAAddress == "M103" && x2.CommandBAddress == "M102" && x2.Lamp1BAddress == "M2018");
        var y1 = unit("Y1");
        Check("  Y1: Back M109 / M2021, Forward M108, D250 with 2 decimals", y1.CommandAAddress == "M109" && y1.Lamp1AAddress == "M2021" &&
              y1.CommandBAddress == "M108" && y1.PositionAddress == "D250" && y1.PositionDecimalPlaces == 2);
        Check("  the jogs momentary", units.All(u => u.CommandAAction == BitActionType.Momentary && u.CommandBAction == BitActionType.Momentary));
        var second = Tree(entry).OfType<NumericDisplayHMI>().FirstOrDefault(d => d.Address == "D156");
        Check("X1's second value D156 (raw / 100)", second != null && second.Divisor == 100 && second.DataType == DataTypeEnum.Bin_32);
        var refs = Tree(entry).OfType<ButtonHMI>().Where(b => b.FallbackText.EndsWith("Reference")).ToList();
        Check("the six references B0 - B5, alternate, lit by themselves", refs.Count == 6 &&
              refs.All(b => b.BitActions.Count == 1 && b.BitActions[0].Action == BitActionType.Alternate && b.Lamp1Address == b.BitActions[0].Address) &&
              string.Join(",", refs.Select(b => b.BitActions[0].Address)) == "B0,B1,B2,B3,B4,B5", string.Join(" ", refs.Select(Bits)));
        var home = Tree(entry).OfType<ButtonHMI>().FirstOrDefault(b => b.FallbackText == "Home");
        Check("  Home: M8 SET", home != null && Bits(home) == "M8:SET", home == null ? "none" : Bits(home));
        var c7 = Tree(entry).OfType<ButtonHMI>().FirstOrDefault(b => b.FallbackText == "Clamping 7");
        Check("Clamping 7: M143, lit by M2014 as the old page", c7 != null && Bits(c7) == "M143:Momentary" && c7.Lamp1Address == "M2014");
        var piston = Tree(entry).OfType<ButtonHMI>().FirstOrDefault(b => b.FallbackText == "Piston Forward");
        var measure = Tree(entry).OfType<NumericDisplayHMI>().FirstOrDefault(d => d.Address == "D152");
        Check("Profile Initial Measurement: Piston Forward M136 / M2016 and the measurement D152", piston != null && piston.Lamp1Address == "M2016" &&
              measure != null && measure.Divisor == 100);
        Check("every control on the machine's PLC 1 (a mounted H500's pages are mapped to its station)",
              Tree(entry).Select(c => c.GetType().GetProperty("PlcNumber")).Where(p => p != null).Count() > 0 &&
              Tree(entry).Where(c => c.GetType().GetProperty("PlcNumber") != null).All(c => (int)c.GetType().GetProperty("PlcNumber").GetValue(c, null) == 1));

        Console.WriteLine("--- Manual Movement > Heads (ucScrewingHeadsH500FX) ---");
        var heads = new ManualHeads();
        Func<string, ButtonHMI> hb = n => Named<ButtonHMI>(heads, n);
        Check("Head 1 / Head 2: Engine Start M124 / M125, High Speed M126 / M127 - alternate, lit by themselves",
              Bits(hb("btnHead_1_Engine_Start")) == "M124:Alternate" && hb("btnHead_1_Engine_Start").Lamp1Address == "M124" &&
              Bits(hb("btnHead_2_Engine_Start")) == "M125:Alternate" && Bits(hb("btnHead_1_High_Speed")) == "M126:Alternate" &&
              Bits(hb("btnHead_2_High_Speed")) == "M127:Alternate");
        Check("  Feed Screw M130 / M2035 and M131 / M2036; Warehouse Up M132 / M133; Unload Screw M128 / M129",
              hb("btnHead_1_Feed_Screw").Lamp1Address == "M2035" && hb("btnHead_2_Feed_Screw").Lamp1Address == "M2036" &&
              Bits(hb("btnHead_1_Warehouse_Up")) == "M132:Momentary" && Bits(hb("btnHead_2_Unload_Screw")) == "M129:Momentary");
        var lamps = Tree(heads).OfType<LampHMI>().Select(l => l.Address).ToList();
        Check("  their lamps: torque limit M2033 / M2034, start M2029 / M2030, finished M2031 / M2032",
              new[] { "M2033", "M2034", "M2029", "M2030", "M2031", "M2032" }.All(lamps.Contains), string.Join(",", lamps));
        var s1 = Tree(heads).OfType<CylinderUnitHMI>().FirstOrDefault(u => u.TitleTextKey == "Screwing 1 Forward / Back");
        Check("Screwing 1 Forward / Back: Y1's jogs M109 / M108, its position D250", s1 != null && s1.CommandAAddress == "M109" && s1.PositionAddress == "D250");
        var cal = Named<NumericInputHMI>(heads, "numScrewing_1_Calibration");
        var go = Named<NumericInputHMI>(heads, "numScrewing_1_Position_To_Go");
        Check("  its calibration position D160 and position to go D163 (raw / 100, 2 decimals)", cal != null && cal.Address == "D160" && cal.Divisor == 100 &&
              cal.DecimalPlaces == 2 && go != null && go.Address == "D163");
        var btns = Tree(heads).OfType<ButtonHMI>().ToList();
        var calibrate = btns.FirstOrDefault(b => b.FallbackText == "Calibrate" && b.BitActions.Any(a => a.Address == "M9"));
        var goBtn = btns.FirstOrDefault(b => b.FallbackText == "GO!" && b.BitActions.Any(a => a.Address == "M10"));
        Check("  Calibrate M9 momentary (as the old page - a SET would stay on), GO! M10 SET",
              calibrate != null && Bits(calibrate) == "M9:Momentary" && goBtn != null && Bits(goBtn) == "M10:SET");
        Check("Screwing 2: M111 / M110, D252, D164 + M11, D167 + M12",
              Tree(heads).OfType<CylinderUnitHMI>().Any(u => u.CommandAAddress == "M111" && u.PositionAddress == "D252") &&
              Named<NumericInputHMI>(heads, "numScrewing_2_Calibration").Address == "D164" && btns.Any(b => Bits(b) == "M11:Momentary") &&
              Named<NumericInputHMI>(heads, "numScrewing_2_Position_To_Go").Address == "D167" && btns.Any(b => Bits(b) == "M12:SET"));

        Console.WriteLine("--- Machine Parameters > Parameters (ucScrewingParameterH500FX) ---");
        var prm = new ParamMachine();
        var nums = Tree(prm).OfType<NumericInputHMI>().ToList();
        var opts = Tree(prm).OfType<CheckBoxHMI>().ToList();
        Check("24 values and 6 options, as the old page", nums.Count == 24 && opts.Count == 6, nums.Count + " / " + opts.Count);
        Func<string, NumericInputHMI> num = a => nums.FirstOrDefault(n => n.Address == a);
        Check("  speeds whole m/min (D142 D122 D143 D144), 16-bit", new[] { "D142", "D122", "D143", "D144" }.All(a => num(a) != null &&
              num(a).Divisor == 1 && num(a).DecimalPlaces == 0 && num(a).DataType == DataTypeEnum.Bin_16 && num(a).Suffix == "m/min"));
        Check("  lengths and positions 32-bit / 100, 1 decimal (D100 D102 D110 D118 D116 D124 ... D150)",
              new[] { "D100", "D102", "D110", "D118", "D116", "D124", "D126", "D128", "D130", "D132", "D170", "D200", "D202", "D204", "D212", "D210", "D150" }
                  .All(a => num(a) != null && num(a).Divisor == 100 && num(a).DecimalPlaces == 1 && num(a).DataType == DataTypeEnum.Bin_32));
        Check("  the pressing time D123 / 10 in seconds; the encoders' accuracy D234 D235 16-bit / 100",
              num("D123").Divisor == 10 && num("D123").UnitType == UnitType.Time && num("D234").Divisor == 100 && num("D235").DataType == DataTypeEnum.Bin_16);
        Check("  the options L0 L1 L4 L2 L5 L3", string.Join(",", opts.Select(o => o.Address).OrderBy(a => a)) == "L0,L1,L2,L3,L4,L5",
              string.Join(",", opts.Select(o => o.Address)));
        Check("  a Maintenance's to change", nums.All(n => n.RequiredSecurity == SecurityLevel.Maintenance) && opts.All(o => o.RequiredSecurity == SecurityLevel.Maintenance));

        Console.WriteLine(fail == 0 ? "\nH500 PAGES ALL PASS" : "\nH500 PAGES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
