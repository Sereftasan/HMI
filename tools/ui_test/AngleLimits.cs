using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Forms;

// ANGLE LIMITS: 0 - 360 always, and a minimum above the maximum is a range
// THROUGH zero (270 ... 360 / 0 ... 45) - for the keypad, the field's warning
// and an angle axis row alike.
class A {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    static bool Pad(double? min, double? max, bool angle, string typed, out string range)
    {
        var pad = new NumpadForm("Input", "", min, max, false, true, false);
        pad.DecimalPlaces = 1;
        pad.Angle = angle;
        range = ((Label)typeof(NumpadForm).GetField("_range", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pad)).Text;
        var press = typeof(NumpadForm).GetMethod("Press", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var ch in typed) press.Invoke(pad, new object[] { ch.ToString() });
        typeof(NumpadForm).GetMethod("Accept", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pad, null);
        bool ok = pad.DialogResult == DialogResult.OK;
        if (!pad.IsDisposed) pad.Dispose();
        return ok;
    }

    [STAThread] static void Main(){
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine("--- the rule ---");
        Check("270 ... 45 is a range through zero", NumericLimits.Wraps(270, 45, true));
        Check("  but only for an angle", !NumericLimits.Wraps(270, 45, false));
        Check("  and not when the minimum is below the maximum", !NumericLimits.Wraps(10, 90, true));
        foreach (var v in new[] { 270.0, 300, 360, 0, 10, 45 })
            Check("  " + v + " is inside 270 ... 45", NumericLimits.Allows(v, 270, 45, true));
        foreach (var v in new[] { 46.0, 100, 269.9, 361, -1 })
            Check("  " + v + " is outside 270 ... 45", !NumericLimits.Allows(v, 270, 45, true));
        Check("an angle with min below max: a plain range (10 ... 90: 50 in, 100 out)",
              NumericLimits.Allows(50, 10, 90, true) && !NumericLimits.Allows(100, 10, 90, true) && !NumericLimits.Allows(5, 10, 90, true));
        Check("an angle with no limits: 0 - 360 still (359 in, 361 and -1 out)",
              NumericLimits.Allows(359, null, null, true) && !NumericLimits.Allows(361, null, null, true) && !NumericLimits.Allows(-1, null, null, true));
        Check("a length is unchanged: min above max refuses everything",
              !NumericLimits.Allows(300, 270, 45, false) && !NumericLimits.Allows(10, 270, 45, false));
        Check("a length with no limits takes anything", NumericLimits.Allows(-5000, null, null, false));

        Console.WriteLine("--- the keypad ---");
        string range;
        Check("300 accepted on 270 ... 45", Pad(270, 45, true, "300", out range));
        Check("  the limits read 'min <= ... or ... <= max'", range == "270.0 ≤ …  or  … ≤ 45.0", range);
        Check("10 accepted on 270 ... 45", Pad(270, 45, true, "10", out range));
        Check("100 refused on 270 ... 45", !Pad(270, 45, true, "100", out range));
        Check("361 refused on an angle with no limits", !Pad(null, null, true, "361", out range));
        Check("  whose line reads 0 ... 360", range == "0.0 ≤ … ≤ 360.0", range);
        Check("an angle 10 ... 90 is a plain range: 50 in", Pad(10, 90, true, "50", out range));
        Check("  100 out", !Pad(10, 90, true, "100", out range));
        Check("  its line as usual", range == "10.0 ≤ … ≤ 90.0", range);
        Check("a length keeps its own line", Pad(10, 90, false, "50", out range) && range.StartsWith("10.0") && !range.Contains("or"), range);
        LanguageManager.ChangeLanguage("TR");
        Pad(270, 45, true, "300", out range);
        Check("'or' is translated", range.Contains("veya"), range);
        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine("--- the field's own warning ---");
        int raw = 0;
        Func<string, int> plc = a => a == "D1" ? raw : 0;
        var f = new NumericInputHMI { Address = "D1", DataType = DataTypeEnum.Bin_16, Digits = 5, IsAngle = true,
                                      MinLimit = new DynamicValue(LimitSourceType.Static, 270, ""),
                                      MaxLimit = new DynamicValue(LimitSourceType.Static, 45, "") };
        foreach (var v in new[] { 300, 10, 100, 361 })
        {
            raw = v; f.UpdateState(plc, c => true);
            bool expectOut = v == 100 || v == 361;
            Check("  " + v + (expectOut ? " warns" : " does not warn"), f.IsOutOfLimits == expectOut);
        }
        var g = new NumericInputHMI { Address = "D1", DataType = DataTypeEnum.Bin_16, Digits = 5, IsAngle = true };
        raw = 400; g.UpdateState(plc, c => true);
        Check("an angle with no limits warns above 360", g.IsOutOfLimits);
        raw = 200; g.UpdateState(plc, c => true);
        Check("  and not within the turn", !g.IsOutOfLimits);
        var len = new NumericInputHMI { Address = "D1", DataType = DataTypeEnum.Bin_16, Digits = 5,
                                        MinLimit = new DynamicValue(LimitSourceType.Static, 10, ""),
                                        MaxLimit = new DynamicValue(LimitSourceType.Static, 90, "") };
        raw = 100; len.UpdateState(plc, c => true);
        Check("a length still warns outside its range", len.IsOutOfLimits);
        var deg = new NumericInputHMI { Address = "D1", DataType = DataTypeEnum.Bin_16, Digits = 7, Suffix = "°" };
        raw = 2048; deg.UpdateState(plc, c => true);
        Check("a degree SUFFIX alone is no angle (an encoder's 2048 pulses/turn stays valid)", !deg.IsAngle && !deg.IsOutOfLimits);

        Console.WriteLine("--- an angle axis row ---");
        var ax = new AxisSettingHMI { AxisNameKey = "A", Suffix = "°", PositionAddress = "W0",
                                      MinLimitAddress = "D320", MaxLimitAddress = "D318",
                                      ReferenceAddress = "D322", CalibrationAddress = "D582" };
        ax.BuildRow();
        var by = ax.Controls.OfType<NumericInputHMI>().ToDictionary(n => n.Address);
        Check("every setting is an angle", by.Values.All(n => n.IsAngle), string.Join(",", by.Keys));
        Check("min and max do not bound each other (270 above 45 is allowed)",
              by["D320"].MinLimit.Type == LimitSourceType.None && by["D320"].MaxLimit.Type == LimitSourceType.None &&
              by["D318"].MinLimit.Type == LimitSourceType.None && by["D318"].MaxLimit.Type == LimitSourceType.None);
        Check("reference bounded by the travel words",
              by["D322"].MinLimit.Type == LimitSourceType.Tag && by["D322"].MinLimit.TagAddress == "D320" &&
              by["D322"].MaxLimit.Type == LimitSourceType.Tag && by["D322"].MaxLimit.TagAddress == "D318");
        Check("calibration likewise", by["D582"].MinLimit.TagAddress == "D320" && by["D582"].MaxLimit.TagAddress == "D318");
        Check("and they warn", by["D322"].OutOfLimitsWarning && by["D582"].OutOfLimitsWarning);
        var lin = new AxisSettingHMI { AxisNameKey = "X", Suffix = "mm", PositionAddress = "W0",
                                       MinLimitAddress = "D1", MaxLimitAddress = "D2", ReferenceAddress = "D3" };
        lin.BuildRow();
        var lby = lin.Controls.OfType<NumericInputHMI>().ToDictionary(n => n.Address);
        Check("a length axis is no angle, and its min/max still bound each other",
              lby.Values.All(n => !n.IsAngle) && lby["D1"].MaxLimit.TagAddress == "D2" && lby["D2"].MinLimit.TagAddress == "D1");

        Console.WriteLine(fail==0?"\nANGLE LIMITS ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}
