using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

// The machine's drawings in the help: a failed movement names where its
// sensor, output, relay, valve coil, air valve, hoses and cylinder are drawn.
class WH {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        Console.WriteLine("--- Wiring.csv, beside the program ---");
        WiringTable.Clear();
        int n = WiringTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wiring.csv"), plc => plc);
        Check("the FA2010 drawings' points are read", n > 150, n + " points");
        var x34 = WiringTable.Find(1, "M212");
        Check("found by the relay the page reads (M212): X34, sensor SN46, wire 119, sheet 12.6",
              x34 != null && x34.Address == "X34" && x34.Tag == "SN46" && x34.Wire == "119" && x34.Sheet == "12.6",
              x34 == null ? "none" : x34.Electrical());
        Check("  and by its own address", WiringTable.Find(1, "X34") == x34);
        Check("  written for a person: X34 - sensor SN46, wire 119, sheet 12.6", x34 != null && x34.Electrical() == "X34 - sensor SN46, wire 119, sheet 12.6",
              x34 == null ? "" : x34.Electrical());
        var y12 = WiringTable.OutputFor(1, "M688");
        Check("the command M688 drives Y12: wire 210, relay K13, coil Y.7 on wire 308",
              y12 != null && y12.Address == "Y12" && y12.Electrical() == "Y12 - wire 210 - relay K13 (sheet 18.6) - coil Y.7, wire 308 (sheet 25.5)",
              y12 == null ? "none" : y12.Electrical());
        Check("  its air: valve Y20/Y21, hoses 16 / 96, cylinder P12, pneumatic 2 E3",
              y12 != null && y12.Pneumatic().Contains("valve Y20/Y21") && y12.Pneumatic().Contains("hoses 16 (A) / 96 (B)") &&
              y12.Pneumatic().Contains("P12 Input Conveyor 6. Clamping Cylinder") && y12.Pneumatic().EndsWith("pneumatic sheet 2 E3"),
              y12 == null ? "" : y12.Pneumatic());
        Check("another station has none of it", WiringTable.Find(2, "M212") == null && WiringTable.OutputFor(3, "M688") == null);
        Check("a point the drawings do not show: none", WiringTable.Find(1, "X31") == null);

        Console.WriteLine("--- the help of a failed movement ---");
        var unit = new CylinderUnitHMI {
            PlcNumber = 1, TitleTextKey = "Front Clamping",
            CommandAAddress = "M646", ButtonATextKey = "Release", Lamp1AAddress = "M225", Lamp1ATipKey = "Clamping back",
            CommandBAddress = "M645", ButtonBTextKey = "Clamp", Lamp1BAddress = "M240", Lamp1BTipKey = "Pressure switch",
            ErrorAAddress = "M845", ErrorBAddress = "M844",
            ValveType = CylinderValve.DoubleSolenoid_3Way, FeedbackType = CylinderFeedback.MagneticPlusPressureSwitch };
        var doc = new TC.HelpDocument { FileName = TC.DocumentForType[TC.ErrorType.CylinderMovement], Valve = 3, Feedback = 5, Movement = unit.MovementFor("A") };
        Check("the cylinder manual is there", doc.Exists, doc.FullPath);
        string html = File.ReadAllText(TC.Tailor(doc));
        Func<string, bool> has = t => html.Contains(t);
        Check("the valve output found from the command (M646 -> Y33), with its relay and coil",
              has("<code>Y33</code>") && has("relay K30 (sheet 19.6)") && has("coil Y.24, wire 325 (sheet 26.7)"));
        Check("the valve and cylinder: Y28/Y29 5/3, hoses 23 / 24, P16, pneumatic sheet 3 E8",
              has("Valve and cylinder:") && has("valve Y28/Y29 (1/8 5/3 open centre)") && has("hoses 23 (A) / 24 (B)") &&
              has("P16 Profile Recognition Front Clamping Cylinder") && has("pneumatic sheet 3 E8"));
        Check("the other coil (Y32, must be off) with its own", has("<code>Y32</code>") && has("relay K29 (sheet 19.6)"));
        Check("the sensor that must come on: M225 = X51, SN59, wire 132, sheet 13.5",
              has("<code>M225</code>") && has("X51 - sensor SN59, wire 132, sheet 13.5"));
        Check("the one that must go off: M240 = X70, SN83 (the clamping's empty switch)",
              has("X70 - sensor SN83, wire 147, sheet 14.4") && has("Profile Recognition Front Clamping Empty Switch"));
        Check("  and what the sheet numbers mean", has("page 12, column 6 of the electrical drawing") && has("zone E3 of the pneumatic drawing"));

        Console.WriteLine("--- no drawings: the box as before ---");
        WiringTable.Clear();
        var bare = new TC.HelpDocument { FileName = doc.FileName, Valve = 3, Feedback = 5, Movement = unit.MovementFor("B") };
        string plain = File.ReadAllText(TC.Tailor(bare));
        Check("the devices still listed, no references, no legend",
              plain.Contains("<code>M645</code>") && plain.Contains("<code>M240</code>") && !plain.Contains("class=\"ref\"") && !plain.Contains("Valve and cylinder:"));

        Console.WriteLine(fail == 0 ? "\nWIRING HELP ALL PASS" : "\nWIRING HELP " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
