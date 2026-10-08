using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

// THE ALARM BITS IN THE TOOLTIPS (1 October): a cylinder unit's buttons name
// their movement's error bit with the alarm's text (and say when it is on),
// an axis unit's buttons the axis's error bit and its drive code words, the
// unit's frame both - and a plain button its own ErrorAddress.
class AT {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static string Tip(ButtonHMI b, bool admin) { return (string)typeof(ButtonHMI).GetMethod("BuildMainTooltip", Any).Invoke(b, new object[] { admin }); }
    static string FrameTip(CylinderUnitHMI u) { return (string)typeof(CylinderUnitHMI).GetMethod("FrameTip", Any).Invoke(u, null); }
    static Dictionary<string, int> mem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    static int Read(string a) { int v; return mem.TryGetValue(a, out v) ? v : 0; }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        WiringTable.Clear();
        WiringTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wiring.csv"), plc => plc);
        AlarmManager.RegisterDefinition("T.Clamp", "Front clamping did not clamp", Kaban.Core.Models.AlarmSeverity.Fault, plcNumber: 1, address: "M844");
        var host = new Form { ClientSize = new Size(800, 300) };

        Console.WriteLine("--- a cylinder unit ---");
        var u = new CylinderUnitHMI {
            PlcNumber = 1, TitleTextKey = "Front Clamping", CommandAAddress = "M646", Lamp1AAddress = "M225",
            CommandBAddress = "M645", Lamp1BAddress = "M240", ErrorAAddress = "M845", ErrorBAddress = "M844",
            ValveType = CylinderValve.DoubleSolenoid_3WayClosedCenter, FeedbackType = CylinderFeedback.MagneticPlusPressureSwitch };
        u.SetBounds(10, 10, 360, 64); host.Controls.Add(u); host.Show(); Application.DoEvents();
        Check("each button carries its own side's error bit", u.ButtonA.ErrorAddress == "M845" && u.ButtonB.ErrorAddress == "M844",
              u.ButtonA.ErrorAddress + " / " + u.ButtonB.ErrorAddress);
        SecurityManager.Logout();
        var tipB = Tip(u.ButtonB, false);
        Check("the button's tip names its alarm bit and the alarm - for everyone, not only an admin", tipB.Contains("Alarm bit: M844 Front clamping did not clamp"), tipB);
        Check("  not on", !tipB.Contains("ON now"));
        mem["M844"] = 1;
        u.ButtonB.UpdateState(Read, c => true);
        Check("  the alarm on: says so", Tip(u.ButtonB, false).Contains("(ON now)"), Tip(u.ButtonB, false));
        Check("  the other button: its own bit, with no text when the alarm list has none", Tip(u.ButtonA, false).Contains("Alarm bit: M845"), Tip(u.ButtonA, false));
        var frame = FrameTip(u);
        Check("the unit's frame: both alarm bits, each named by its movement (from the drawing)", frame.Contains("Alarm bit (Clamp): M844 Front clamping did not clamp") &&
              frame.Contains("Alarm bit (Release): M845"), frame);
        u.ErrorBAddress = "";
        Check("an error bit taken away: gone from its button", u.ButtonB.ErrorAddress == "" && !Tip(u.ButtonB, false).Contains("Alarm bit"));

        Console.WriteLine("--- an axis unit ---");
        var ax = new CylinderUnitHMI { PlcNumber = 1, TitleTextKey = "Robot X", Kind = UnitKind.Axis, CommandAAddress = "M10", CommandBAddress = "M11", ErrorBAddress = "M801" };
        ax.SetBounds(10, 90, 360, 64); host.Controls.Add(ax); Application.DoEvents();
        ax.SetAxisCodes("D120", "D121", "D122");
        var axTip = Tip(ax.ButtonA, false);
        Check("both buttons: the axis's error bit and its drive codes (servo alarm, axis alarm, axis warning)",
              ax.ButtonA.ErrorAddress == "M801" && ax.ButtonB.ErrorAddress == "M801" && axTip.Contains("Servo alarm: D120") &&
              axTip.Contains("Axis alarm: D121") && axTip.Contains("Axis warning: D122"), axTip);
        mem["D120"] = 0x16;
        ax.ButtonA.UpdateState(Read, c => true);
        Check("  a code that is set shows its value", Tip(ax.ButtonA, false).Contains("Servo alarm: D120  (22)"), Tip(ax.ButtonA, false));
        Check("  and the frame", FrameTip(ax).Contains("Alarm bit: M801") && FrameTip(ax).Contains("Axis warning: D122"), FrameTip(ax));

        Console.WriteLine("--- a plain button ---");
        var b = new ButtonHMI { PlcNumber = 1, ErrorAddress = "M844", ToolTipText = "Clamps the profile" };
        SecurityManager.Login("op", SecurityLevel.Operator);
        ConnectionMonitor.ReportRead(1, true);
        mem.Clear();
        b.UpdateState(Read, c => true);
        Check("its own ErrorAddress, after its help", Tip(b, false).StartsWith("Clamps the profile") && Tip(b, false).Contains("Alarm bit: M844"), Tip(b, false));
        Check("  in the Properties window under State, after OutputAddress",
              TypeDescriptorOrder(b).SkipWhile(n => n != "OutputAddress").Skip(1).FirstOrDefault() == "ErrorAddress",
              string.Join(",", TypeDescriptorOrder(b).SkipWhile(n => n != "OutputAddress").Take(3)));
        Check("  with no ErrorAddress: no alarm line", !Tip(new ButtonHMI { ToolTipText = "x" }, false).Contains("Alarm bit"));

        host.Close();
        Console.WriteLine(fail == 0 ? "\nALARM TIPS ALL PASS" : "\nALARM TIPS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    static List<string> TypeDescriptorOrder(object o) {
        return System.ComponentModel.TypeDescriptor.GetProperties(o).Cast<System.ComponentModel.PropertyDescriptor>()
               .Where(p => p.IsBrowsable).Select(p => p.Name).ToList();
    }
}
