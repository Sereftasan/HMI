using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// 19 October: "we only use brakes for vertical axes - the up / down arrow axes. Change
// Milling 1 Angle, Milling 2 Angle and Cutting Angle's buttons to left / right arrows."
class AA {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }

    static IEnumerable<T> All<T>(Control c) where T : Control
    {
        foreach (Control k in c.Controls)
        {
            if (k is T) yield return (T)k;
            foreach (var d in All<T>(k)) yield return d;
        }
    }

    [STAThread] static void Main() {
        Console.WriteLine("--- FA2010's Axis page: up / down only for the vertical axes ---");
        var page = new Kaban.Machines.FA2010.Screens.Parameters.AxisSettings();
        var rows = All<AxisSettingHMI>(page).ToDictionary(r => r.Name);
        var vertical = rows.Values.Where(r => r.VerticalMovement).Select(r => r.Name).OrderBy(n => n).ToList();
        Check("the angle axes have left / right arrows", !rows["axMilling_1_Angle"].VerticalMovement && !rows["axMilling_2_Angle"].VerticalMovement &&
              !rows["axCutting_Angle"].VerticalMovement);
        Check("up / down: Robot Z, Milling 1Z, Milling 2Z, Cutting Z, V Cutting Z", vertical.SequenceEqual(new[] { "axCutting_Z", "axMilling_1Z", "axMilling_2Z", "axRobot_Z", "axV_Cutting_Z" }),
              string.Join(",", vertical));
        AlarmHelpLinks.Harvest(page);
        Func<string, Kaban.Core.Helpers.AlarmGeneration.Axis> watch = n =>
            Kaban.Core.Helpers.AlarmGeneration.Axes.FirstOrDefault(a => a.ServoAlarm == rows[n].ServoAlarmAddress.Trim());
        Check("an angle axis is no axis with a brake for the help; Cutting Z is",
              watch("axCutting_Angle") != null && !watch("axCutting_Angle").Vertical && watch("axCutting_Z") != null && watch("axCutting_Z").Vertical);

        Console.WriteLine(fail == 0 ? "AXIS ARROWS ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
