using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

// 19 October: "the axis current value is 10 times the percentage (21.2 % is 212 at the address),
// and it can be negative as well (signed decimal - just the direction of the motor's torque), so
// we should show -21.2 % in green as well, just like 21.2 %".
class ACS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { d = d ?? ""; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        for (var t = o.GetType(); t != null; t = t.BaseType) {
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o, null);
            var m = t.GetMethod(n, Any, null, Type.EmptyTypes, null); if (m != null) return m.Invoke(o, null);
        }
        return null;
    }
    static string Reading(MeterHMI m, int raw, out Color colour, out double fraction) {
        m.UpdateState(a => raw, c => true);
        colour = (Color)Get(m, "BarColour");
        fraction = (double)Get(m, "Fraction");
        return (string)Get(m, "Text_Value");
    }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        Check("an axis row's current: 0.1 % by default (divisor 10)", new AxisSettingHMI().CurrentDivisor == 10);

        var row = new AxisSettingHMI { AxisNameKey = "X", PositionAddress = "W0", CurrentAddress = "W0A", CurrentFullScale = 100 };
        row.BuildRow();
        var meter = row.Controls.OfType<MeterHMI>().FirstOrDefault(m => m.Address == "W0A");
        Check("its meter: divided by 10, one decimal, signed", meter != null && meter.Divisor == 10 && meter.DecimalPlaces == 1 && meter.Signed,
              meter == null ? "none" : meter.Divisor + " " + meter.DecimalPlaces + " " + meter.Signed);

        Color c; double f;
        string t = Reading(meter, 212, out c, out f);
        Check("212 at W0A: 21.2 %, green, a fifth of the bar", t == "21.2 %" && c == MeterHMI.GreenBand && Math.Abs(f - 0.212) < 1e-9, t + " " + f);
        t = Reading(meter, 65536 - 212, out c, out f);
        Check("-212 (read as 65324): -21.2 %, green, the same bar", t == "-21.2 %" && c == MeterHMI.GreenBand && Math.Abs(f - 0.212) < 1e-9, t + " " + f);
        t = Reading(meter, -212, out c, out f);
        Check("  -212 read signed already: the same", t == "-21.2 %" && c == MeterHMI.GreenBand && Math.Abs(f - 0.212) < 1e-9, t + " " + f);
        t = Reading(meter, -700, out c, out f);
        Check("-70.0 %: amber, like 70 %", t == "-70.0 %" && c == MeterHMI.AmberBand, t);
        t = Reading(meter, 65536 - 900, out c, out f);
        Check("-90.0 %: red, like 90 %", t == "-90.0 %" && c == MeterHMI.RedBand && f > 0.89, t);
        t = Reading(meter, 900, out c, out f);
        Check("  90.0 %: red", t == "90.0 %" && c == MeterHMI.RedBand, t);

        var z = new MeterHMI { Divisor = 1000 }; z.UseZDistanceBands();
        Check("the Z distance meter is not signed (its bands are a window)", !z.Signed);
        Check("decimals of a divisor: 1 none, 10 one, 100 two, 10000 three at most",
              MeterHMI.DecimalsOf(1) == 0 && MeterHMI.DecimalsOf(10) == 1 && MeterHMI.DecimalsOf(100) == 2 && MeterHMI.DecimalsOf(10000) == 3);

        // FA2010's axes: every current read in 0.1 %
        var type = Type.GetType("Kaban.Machines.FA2010.Screens.Parameters.AxisSettings, Kaban.Machines.FA2010");
        if (type != null)
        {
            using (var page = (Control)Activator.CreateInstance(type))
            {
                Func<Control, IEnumerable<Control>> all = null;
                all = k => k.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(all(x)));
                var axes = all(page).OfType<AxisSettingHMI>().Where(a => !string.IsNullOrEmpty(a.CurrentAddress)).ToList();
                Check("FA2010: its " + axes.Count + " axes' currents divided by 10", axes.Count == 16 && axes.All(a => a.CurrentDivisor == 10),
                      string.Join(",", axes.Where(a => a.CurrentDivisor != 10).Select(a => a.Name)));
            }
        }
        else Check("FA2010's axis page found", false);

        Console.WriteLine(fail == 0 ? "AXIS CURRENT ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
