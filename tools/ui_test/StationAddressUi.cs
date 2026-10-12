using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Drivers.Simulation;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// 12 October: a device of another controller in a property ("X12@2"), on the real page it was made
// for - FA2010 > Manual Movement > Clamping Moulds, whose three back horizontal moulds with an encoder
// have their reference sensors on PLC 2 (X12, X13, X14) and whose ten back moulds' valves are PLC 2's
// (Y0 - Y23): read in PLC 2's batch, lit from PLC 2, moved by the simulation there, listed as PLC 2's
// devices, the movement's output named on PLC 2.
class SAU {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static bool Lit(ButtonHMI b) { return (bool)typeof(ButtonHMI).GetProperty("Lamp1On", Any).GetValue(b, null); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "sau_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.LoadMachineTranslations("Translations.csv");
        UnitManager.EnsureDefaults();
        WiringTable.Clear();
        WiringTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wiring.csv"), plc => plc);
        PlcManager.Instance.RegisterDriver(1, new SimulationDriver(1));
        PlcManager.Instance.RegisterDriver(2, new SimulationDriver(2));
        var sim1 = (SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(1));
        var sim2 = (SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(2));
        SecurityManager.Login("m", SecurityLevel.Maintenance);

        var form = new Form { ClientSize = new Size(1270, 800), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var page = new Kaban.Machines.FA2010.Screens.Manual.ManualClamping_Moulds { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        form.Show(); Pump(600);
        var rows = PneumaticPositioningHMI.RowsOf(page);
        Func<string, PneumaticPositioningHMI> row = t => rows.First(r => r.TitleTextKey == t);
        var pr = row("Profile Recognition Back Horizontal");

        Console.WriteLine("--- the page ---");
        Check("the three back horizontal moulds with an encoder: their reference sensor X12, X13, X14 of PLC 2",
              pr.BackSensorAddress == "X12@2" && row("Cutting Left Back Horizontal").BackSensorAddress == "X13@2" &&
              row("Cutting Right Back Horizontal").BackSensorAddress == "X14@2" && pr.PlcNumber == 1);
        Check("  their manual movement's one lamp: the reference (EncoderPlusReference) - nothing left open",
              pr.Unit.TypeCheck == "OK" && row("Cutting Left Back Horizontal").Unit.TypeCheck == "OK", pr.Unit.TypeCheck);

        Console.WriteLine("--- read in PLC 2's batch ---");
        var scanner = new Kaban.UI.Plc.PlcScanner(page);
        scanner.RefreshTags();
        var batches = (Dictionary<int, string[]>)typeof(Kaban.UI.Plc.PlcScanner).GetField("_plcBatch", Any).GetValue(scanner);
        string[] b1, b2;
        batches.TryGetValue(1, out b1); batches.TryGetValue(2, out b2);
        Check("X12, X13, X14 in PLC 2's batch", b2 != null && new[] { "X12", "X13", "X14" }.All(x => b2.Contains(x)),
              b2 == null ? "no PLC 2 batch" : string.Join(" ", b2.Take(12)));
        Check("  none in PLC 1's, no '@' read anywhere", b1 != null && !b1.Contains("X12") && !b1.Concat(b2 ?? new string[0]).Any(a => a.Contains("@")));
        sim2.Write("X12", 1);
        sim1.Write("X12", 0);
        Pump(800);
        Check("PLC 2's X12 on: Profile Recognition Back Horizontal's back lamp lit", Lit(pr.Unit.ButtonA));
        sim2.Write("X12", 0);
        sim1.Write("X12", 1);           // PLC 1's own X12 (an encoder input) says nothing about it
        Pump(800);
        Check("  off again - PLC 1's X12 does not light it", !Lit(pr.Unit.ButtonA));
        sim1.Write("X12", 0);
        Check("  its tooltip: 'X12 (PLC 2)'", PlcAddressHelper.Describe(pr.PlcNumber, pr.BackSensorAddress) == "X12 (PLC 2)");
        scanner.Stop();

        Console.WriteLine("--- the movement: its valve on PLC 2 ---");
        var back = pr.Unit.MovementFor("A");
        Check("back (M557 on PLC 1) drives Y13 of PLC 2", back.Output != null && back.Output.Address == "Y13@2" && back.Command.Address == "M557",
              back.Output == null ? "none" : back.Output.Address);
        var w = WiringTable.Find(back.Station, back.Output.Address);
        Check("  the drawings' point: PLC 2's Y13 (OUT_ProfilTanima_ArkaYatay_Geri)", w != null && w.Station == 2 && w.Text.Contains("ArkaYatay_Geri"),
              w == null ? "none" : w.Text);
        Check("  its feedback the reference sensor X12@2", back.Feedback.Count == 1 && back.Feedback[0].Address == "X12@2");
        var watch = pr.Unit.BuildWatch();
        var side = watch == null ? null : watch.Sides.FirstOrDefault(s => s.Side == "A");
        Check("the HMI's own alarm times it from Y13@2, the sensor named 'X12 (PLC 2)'",
              side != null && side.Drive == "Y13@2" && side.MustOn.Any(s => s.Address == "X12@2" && s.Name.Contains("X12 (PLC 2)")),
              side == null ? "no watch" : side.Drive + " " + string.Join(",", side.MustOn.Select(s => s.Address + "=" + s.Name)));
        var html = Kaban.Core.Helpers.TroubleshootingCatalog.BuildSteps(back, (Kaban.Core.Helpers.TroubleshootingCatalog.ValveType)(int)pr.ValveType,
            (Kaban.Core.Helpers.TroubleshootingCatalog.FeedbackType)Kaban.Core.Helpers.TroubleshootingCatalog.CanonicalFeedback((int)pr.FeedbackType), 0, "EN");
        int atMark = html.IndexOf("@2");
        Check("  its step-by-step page names them on PLC 2, never as 'X12@2'", html.Contains("Y13") && html.Contains("X12") && atMark < 0 &&
              html.Contains("PLC 2"), atMark < 0 ? "" : html.Substring(Math.Max(0, atMark - 120), Math.Min(200, html.Length - Math.Max(0, atMark - 120))));
        File.WriteAllText(Path.Combine(dir, "steps.html"), html);
        Console.WriteLine("    (" + Path.Combine(dir, "steps.html") + ")");
        var vert = row("Conveyor Back Vertical").Unit.MovementFor("B");
        Check("  a back vertical mould's up (M548) drives Y4 of PLC 2", vert.Output != null && vert.Output.Address == "Y4@2",
              vert.Output == null ? "none" : vert.Output.Address);
        var front = row("Conveyor Front Vertical").Unit.MovementFor("B");
        Check("  a front one's still PLC 1's (M546 -> Y133)", front.Output != null && front.Output.Address == "Y133", front.Output == null ? "none" : front.Output.Address);

        Console.WriteLine("--- the device lists ---");
        var on2 = DeviceCollector.FromControl(page, 2);
        var on1 = DeviceCollector.FromControl(page, 1);
        Check("PLC 2's devices on the page: X12, X13, X14 (the moulds' reference)", new[] { "X12", "X13", "X14" }.All(x => on2.Any(d => d.Address == x && d.Station == 2)),
              string.Join(" ", on2.Select(d => d.Address)));
        Check("  PLC 1's: not X12, the moulds' commands still there", !on1.Any(d => d.Address.StartsWith("X12")) && on1.Any(d => d.Address == "M557"));
        Check("  named by its row", on2.First(d => d.Address == "X12").UsedAs.StartsWith("Profile Recognition Back Horizontal"), on2.First(d => d.Address == "X12").UsedAs);

        Console.WriteLine("--- the simulation moves it there ---");
        var simMotion = typeof(SimulationMotion);
        SimulationMotion.Clear();
        SimulationMotion.Register(pr.Unit);
        sim1.Write("M557", 1);
        var t0 = DateTime.Now;
        SimulationMotion.Tick(t0);
        SimulationMotion.Tick(t0.AddSeconds(5));
        Check("back pressed on PLC 1: the reference sensor on PLC 2 comes on, PLC 1's X12 untouched", sim2.Read("X12") == 1 && sim1.Read("X12") == 0,
              "PLC 2 X12 " + sim2.Read("X12") + ", PLC 1 X12 " + sim1.Read("X12"));
        sim1.Write("M557", 0);
        sim1.Write("M556", 1);
        SimulationMotion.Tick(t0.AddSeconds(6));
        Check("  forward: it lets go", sim2.Read("X12") == 0);
        sim1.Write("M556", 0);

        Console.WriteLine("--- the help links ---");
        AlarmHelpLinks.Register(new AlarmHelpLinks.Link { Station = 1, Address = "M999@2", Unit = "Test" });
        Check("an error bit named M999@2 on a PLC 1 unit is found as PLC 2's M999", AlarmHelpLinks.Find(2, "M999") != null &&
              AlarmHelpLinks.Find(1, "M999") == null);

        form.Close();
        Console.WriteLine(fail == 0 ? "\nSTATION ADDRESS UI ALL PASS" : "\nSTATION ADDRESS UI " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
