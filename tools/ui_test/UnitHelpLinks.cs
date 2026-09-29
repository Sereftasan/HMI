using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

class FakeCtx : IMachineMountContext {
    readonly int[] map; public FakeCtx(params int[] m){ map = m; }
    public string ModuleId { get { return "SUB"; } }
    public bool IsMainMachine { get { return false; } }
    public int Station(int local){ int i = local - 1; return i >= 0 && i < map.Length ? map[i] : local; }
    public string Scoped(string id){ return "SUB." + id; }
    public long AllocateMachineBit(string t){ return 0; }
    public string ResolveFile(string f){ return f; }
}
class OnePage : ISettingsPage {
    readonly Func<UserControl> make; public OnePage(Func<UserControl> m){ make = m; }
    public string TitleKey { get { return "T"; } } public string TitleFallback { get { return "T"; } }
    public Kaban.Core.Enums.SecurityLevel RequiredLevel { get { return Kaban.Core.Enums.SecurityLevel.None; } }
    public UserControl GetControl(){ return make(); }
}

class L {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}
    static T Field<T>(object o, string n){ return (T)o.GetType().GetField(n, BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o); }

    static CylinderUnitHMI Clamp(int plc, string a, string b, CylinderValve v, CylinderFeedback f, string title){
        return new CylinderUnitHMI { PlcNumber = plc, ErrorAAddress = a, ErrorBAddress = b, Valve = v, Feedback = f, TitleTextKey = title };
    }

    [STAThread] static void Main(){
        Console.WriteLine("--- a unit and an axis describe their own errors ---");
        var clamp = Clamp(3, "M810", "M811", CylinderValve.DoubleSolenoid, CylinderFeedback.TwoEndMagnetic, "Robot Clamping");
        var links = clamp.HelpLinks().ToList();
        Check("two error bits, one per side", links.Count == 2);
        Check("  carrying the unit's valve and feedback", links.All(l => l.Valve == 1 && l.Feedback == 2 && l.Station == 3));
        var axis = new AxisSettingHMI { PlcNumber = 3, AxisNameKey = "X1", ErrorAddress = "M801" };
        Check("the axis gives a servo link", axis.HelpLink().Kind == TC.ErrorType.ServoFault && axis.HelpLink().Unit == "X1");

        Console.WriteLine("--- discovery finds units on pages, inside settings tabs too ---");
        AlarmHelpLinks.Clear();
        var page = new UserControl();
        page.Controls.Add(clamp); page.Controls.Add(axis);
        var tabs = new SettingsScreen(new ISettingsPage[] {
            new OnePage(() => { var p = new UserControl(); p.Controls.Add(Clamp(3, "M820", "", CylinderValve.SingleSolenoid, CylinderFeedback.SingleMagnetic, "Conveyor Stop")); return p; })
        });
        page.Controls.Add(tabs);
        AlarmHelpLinks.Harvest(page);
        Check("all four error bits found", AlarmHelpLinks.Count == 4, AlarmHelpLinks.Count.ToString());
        Check("the one on an unopened tab too", AlarmHelpLinks.Find(3, "M820") != null);
        Check("station AND address must match", AlarmHelpLinks.Find(1, "M810") == null);

        Console.WriteLine("--- the same bit described two ways is reported, not guessed ---");
        AlarmHelpLinks.Register(Clamp(3, "M810", "", CylinderValve.DoubleSolenoid_3Way, CylinderFeedback.TwoEndMagnetic, "Elsewhere").HelpLinks().First());
        Check("one conflict logged", AlarmHelpLinks.Conflicts.Count == 1, AlarmHelpLinks.Conflicts.FirstOrDefault() ?? "");
        Check("  the first description kept", AlarmHelpLinks.Find(3, "M810").Valve == 1);
        AlarmHelpLinks.Register(Clamp(3, "M810", "", CylinderValve.DoubleSolenoid, CylinderFeedback.TwoEndMagnetic, "Same unit, 2nd page").HelpLinks().First());
        Check("the same unit on a second page is NOT a conflict", AlarmHelpLinks.Conflicts.Count == 1);

        Console.WriteLine("--- an alarm finds its unit's help, then its own files ---");
        AlarmManager.RegisterDefinition("SUB.Clamp_A", "Alm_Clamp", Kaban.Core.Models.AlarmSeverity.Fault,
            plcNumber: 3, address: "M810", helperKeys: new List<string> { "MR_J4_TroubleShooting.pdf" });
        var docs = AlarmHelpLinks.DocumentsFor(AlarmManager.FindByStationAndAddress(3, "M810"));
        Check("unit manual first, the helper file after", docs.Count == 2 && docs[0].FileName == "Cylinder_Troubleshooting.html",
              string.Join(" | ", docs.Select(d => d.Title)));
        Check("  tailored to that cylinder", docs[0].Valve == 1 && docs[0].Feedback == 2);
        Check("  and named after the unit", docs[0].Title.Contains("Robot Clamping"), docs[0].Title);
        AlarmManager.RegisterDefinition("SUB.Axis_X", "Alm_X", Kaban.Core.Models.AlarmSeverity.Fault, plcNumber: 3, address: "M801");
        var servo = AlarmHelpLinks.DocumentsFor(AlarmManager.FindByStationAndAddress(3, "M801"));
        Check("an axis fault opens the servo manual, named for the axis",
              servo.Count == 1 && servo[0].FileName.StartsWith("MR_J4") && servo[0].Title.Contains("X1"), servo.Count > 0 ? servo[0].Title : "none");
        AlarmManager.RegisterDefinition("SUB.EStop", "Alm_E", Kaban.Core.Models.AlarmSeverity.Critical,
            plcNumber: 3, address: "M800", helperKeys: new List<string> { "Cylinder_Troubleshooting_Manual_EN.pdf" });
        var estop = AlarmHelpLinks.DocumentsFor(AlarmManager.FindByStationAndAddress(3, "M800"));
        Check("an alarm with no unit keeps just its helper links", estop.Count == 1 && estop[0].FileName.EndsWith(".pdf"));

        Console.WriteLine("--- the '?' on the unit's TOP frame line ---");
        var host = new Form { ClientSize = new System.Drawing.Size(420, 120) };
        var unit = Clamp(3, "M810", "M811", CylinderValve.DoubleSolenoid, CylinderFeedback.TwoEndMagnetic, "Robot Clamping");
        unit.CommandAAddress = "M700"; unit.CommandBAddress = "M701";     // two movements, two buttons
        unit.SetBounds(10, 10, 360, 64);
        var plain = new CylinderUnitHMI { PlcNumber = 3 }; plain.SetBounds(10, 80, 360, 64);
        host.Controls.Add(unit); host.Controls.Add(plain); host.Show(); Application.DoEvents();
        var helpA = Field<AlarmHelpBadge>(unit, "_helpA"); var helpB = Field<AlarmHelpBadge>(unit, "_helpB");
        var mark = Field<UnitHelpBadge>(unit, "_help");
        // The side badges used to take half a badge of width off each side with an
        // error bit, so a unit with error bits drew narrower than one without.
        Check("NO room taken from the sides: error bits do not narrow the frame",
              unit.Padding == Padding.Empty && plain.Padding == Padding.Empty, unit.Padding.ToString());
        Check("a cylinder with its valve and feedback set: a BLUE '?' - its manual is there to read",
              mark.Visible && mark.State == UnitHelpBadge.BadgeState.Info && mark.MarkColor.ToArgb() == UnitHelpBadge.Blue.ToArgb(),
              mark.State.ToString());
        Check("  the watchers never show themselves", !helpA.Visible && !helpB.Visible);
        Check("  a unit with nothing to open has none", !Field<UnitHelpBadge>(plain, "_help").Visible);
        helpB.UpdateState(a => a == "M811" ? 1 : 0, c => true);
        Check("B's error: the '?' offers its help, still BLUE", mark.State == UnitHelpBadge.BadgeState.Error && helpB.IsActive && !helpB.Visible &&
              mark.MarkColor.ToArgb() == UnitHelpBadge.Blue.ToArgb());
        Check("  on the top frame line", mark.Top <= 4, "top=" + mark.Top);
        Check("  at its RIGHT end", mark.Right >= unit.ClientSize.Width - 16 && mark.Right <= unit.ClientSize.Width,
              "right=" + mark.Right + " of " + unit.ClientSize.Width);
        Check("  and button B's border blinks red, A's does not", unit.ButtonB.AlertBlink && !unit.ButtonA.AlertBlink);
        var bdocs = unit.HelpDocuments();
        Check("  its help: the manual for movement B", bdocs.Count >= 1 && bdocs[0].Movement != null && bdocs[0].Movement.Side == "B" &&
              bdocs[0].Valve == 1 && bdocs[0].Feedback == 2, string.Join(" | ", bdocs.Select(d => d.Title)));
        helpA.UpdateState(a => a == "M810" ? 1 : 0, c => true);
        Check("A's error too: still ONE '?', both movements in its list", mark.State == UnitHelpBadge.BadgeState.Error &&
              unit.HelpDocuments().Count(d => d.Movement != null) == 2);
        Check("  and button A blinks too", unit.ButtonA.AlertBlink);
        helpA.UpdateState(a => 0, c => true);
        Check("A clears: its blink stops", !unit.ButtonA.AlertBlink);
        helpB.UpdateState(a => 0, c => true);
        Check("B clears: its blink stops", !unit.ButtonB.AlertBlink);
        Check("and the '?' is blue again", mark.State == UnitHelpBadge.BadgeState.Info);
        ConnectionMonitor.ReportRead(3, false, "not connected");
        ConnectionMonitor.GraceTime = TimeSpan.Zero;
        helpA.UpdateState(a => 1, c => true);
        Check("a dead station turns nothing red - its reading cannot be trusted", !helpA.IsActive && mark.State != UnitHelpBadge.BadgeState.Error);
        host.Close();

        Console.WriteLine("--- sub-machine pages speak to THEIR controller ---");
        var ctx = new FakeCtx(2, 7);                     // local 1 -> 2, local 2 -> 7
        var raw = new OnePage(() => {
            var p = new UserControl();
            p.Controls.Add(new ButtonHMI { PlcNumber = 1 });
            p.Controls.Add(new CylinderUnitHMI { PlcNumber = 1 });
            p.Controls.Add(new NumericInputHMI { PlcNumber = 2 });
            return p;
        });
        var built = new StationMappedPage(raw, ctx).GetControl();
        var btn = built.Controls.OfType<ButtonHMI>().First();
        var fu = built.Controls.OfType<CylinderUnitHMI>().First();
        var num = built.Controls.OfType<NumericInputHMI>().First();
        Check("a button on PLC 1 goes to host station 2", btn.PlcNumber == 2, btn.PlcNumber.ToString());
        Check("a field on PLC 2 goes to host station 7", num.PlcNumber == 7, num.PlcNumber.ToString());
        Check("a unit goes to 2", fu.PlcNumber == 2);
        Check("  and its own buttons to 2 as well - translated ONCE, not 2 -> 7",
              fu.ButtonA.PlcNumber == 2 && fu.ButtonB.PlcNumber == 2, fu.ButtonA.PlcNumber + "/" + fu.ButtonB.PlcNumber);

        Console.WriteLine(fail==0?"\nLINKS ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

