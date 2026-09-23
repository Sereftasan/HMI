using System;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Forms;

class U {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    [STAThread] static void Main(){
        Console.WriteLine("--- an axis row invents NO limits ---");
        var ax = new AxisSettingHMI {
            AxisNameKey="X", PositionAddress="W0",
            ReferenceAddress="D4", CalibrationAddress="D5" };   // no MIN/MAX words
        ax.BuildRow();
        var fields = ax.Controls.OfType<NumericInputHMI>().ToList();
        Check("reference and calibration exist", fields.Count == 2, fields.Count.ToString());
        Check("neither carries a made-up minimum",
              fields.All(f => f.MinLimit == null || f.MinLimit.Type == LimitSourceType.None),
              string.Join(", ", fields.Select(f => f.MinLimit.Type.ToString())));
        Check("nor a made-up maximum",
              fields.All(f => f.MaxLimit == null || f.MaxLimit.Type == LimitSourceType.None));

        Console.WriteLine("--- an ANGLE row likewise ---");
        var an = new AxisSettingHMI { AxisNameKey="A", Suffix="\u00B0",
                                      ReferenceAddress="D9" };
        an.BuildRow();
        var af = an.Controls.OfType<NumericInputHMI>().First();
        Check("no +/-360 invented", af.MinLimit.Type == LimitSourceType.None &&
                                    af.MaxLimit.Type == LimitSourceType.None);

        Console.WriteLine("--- the keypad is bounded by DIGITS instead ---");
        var pad = new NumpadForm("Input", "", null, null, true, true, false);
        pad.MaxDigits = 5; pad.DecimalPlaces = 2;
        var press = pad.GetType().GetMethod("Press",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var k in new[]{"1","2","3","4","5","6","7"}) press.Invoke(pad, new object[]{ k });
        Check("a 5-digit field stops at five", pad.Value.Length == 5, pad.Value);

        var pad2 = new NumpadForm("Input", "", null, null, true, true, false);
        pad2.MaxDigits = 6; pad2.DecimalPlaces = 2;
        foreach (var k in new[]{"1","2",".","3","4","5"}) press.Invoke(pad2, new object[]{ k });
        Check("and at two decimal places", pad2.Value == "12.34", pad2.Value);

        Console.WriteLine("--- decimals follow the UNIT ---");
        var f2 = new NumericInputHMI { Address="D1", UnitType=UnitType.Length, DecimalPlaces=1, Digits=6 };
        var eff = f2.GetType().GetProperty("EffectiveDecimals",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int mmDec = (int)eff.GetValue(f2, null);
        UnitManager.SetActive(UnitType.Length, "inch");
        int inDec = (int)eff.GetValue(f2, null);
        UnitManager.SetActive(UnitType.Length, "mm");
        Check("inches show more places than millimetres", inDec > mmDec, mmDec + " -> " + inDec);

        Console.WriteLine(fail==0?"\nUNITS ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

