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

    /// <summary>The row's back and forward jog buttons' arrows, in that order (left to right on the row).</summary>
    static string RowArrows(AxisSettingHMI r)
    {
        var jogs = All<ButtonHMI>(r).Where(b => b.BitActions.Count > 0 && b.BitActions[0].Address != null &&
                                                (b.BitActions[0].Address == r.CommandBackAddress || b.BitActions[0].Address == r.CommandForwardAddress))
                                    .OrderBy(b => b.BitActions[0].Address == r.CommandBackAddress ? 0 : 1).ToList();
        return string.Join(" ", jogs.Select(b => b.DisplayText));
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
        AlarmHelpLinks.DiscoverAll();
        AlarmHelpLinks.Harvest(page);
        var pageForm = new Form { ClientSize = new Size(1400, 900) };
        page.Dock = DockStyle.Fill;
        pageForm.Controls.Add(page);
        pageForm.Show(); Application.DoEvents();
        Func<string, Kaban.Core.Helpers.AlarmGeneration.Axis> watch = n =>
            Kaban.Core.Helpers.AlarmGeneration.Axes.FirstOrDefault(a => a.ServoAlarm == rows[n].ServoAlarmAddress.Trim());
        Check("an angle axis is no axis with a brake for the help; Cutting Z is",
              watch("axCutting_Angle") != null && !watch("axCutting_Angle").Vertical && watch("axCutting_Z") != null && watch("axCutting_Z").Vertical);

        Console.WriteLine("--- a Manual Movement page's axis unit: its row's arrows ---");
        var host = new Form { ClientSize = new Size(800, 200) };
        var robotZ = new AxisUnitHMI { AxisNumber = rows["axRobot_Z"].HMIAxisIndex };
        var robotX = new AxisUnitHMI { AxisNumber = rows["axRobot_X"].HMIAxisIndex };
        var angle = new AxisUnitHMI { AxisNumber = rows["axCutting_Angle"].HMIAxisIndex };
        robotZ.SetBounds(10, 10, 360, 64); robotX.SetBounds(10, 80, 360, 64); angle.SetBounds(400, 10, 360, 64);
        host.Controls.AddRange(new Control[] { robotZ, robotX, angle });
        host.Show(); Application.DoEvents();
        Func<AxisUnitHMI, string> shown = u => u.Unit.ButtonA.DisplayText + " " + u.Unit.ButtonB.DisplayText;
        Check("all three found their rows", robotZ.IsResolved && robotX.IsResolved && angle.IsResolved);
        Check("Robot Z (up / down on its row): back DOWN, forward UP", shown(robotZ) == ButtonGlyphs.Down + " " + ButtonGlyphs.Up, shown(robotZ));
        Check("Robot X: back LEFT, forward RIGHT", shown(robotX) == ButtonGlyphs.Left + " " + ButtonGlyphs.Right, shown(robotX));
        Check("Cutting Angle: left / right now", shown(angle) == ButtonGlyphs.Left + " " + ButtonGlyphs.Right, shown(angle));
        Check("  the same as the rows' own jog buttons", RowArrows(rows["axRobot_Z"]) == shown(robotZ) && RowArrows(rows["axRobot_X"]) == shown(robotX),
              RowArrows(rows["axRobot_Z"]) + " | " + RowArrows(rows["axRobot_X"]));

        Console.WriteLine(fail == 0 ? "AXIS ARROWS ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
