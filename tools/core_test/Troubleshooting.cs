using System;
using System.Linq;
using Kaban.Core.Helpers;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

class T {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}
    static void Main(){
        Console.WriteLine("--- the three-digit code ---");
        Check("cylinder + double solenoid + two sensors composes to 1.1.2",
              TC.Compose(TC.ValveType.DoubleSolenoid, TC.FeedbackType.TwoMagnetic) == "1.1.2",
              TC.Compose(TC.ValveType.DoubleSolenoid, TC.FeedbackType.TwoMagnetic));
        Check("servo on axis 3 composes to 2.3.0",
              TC.Compose(TC.ErrorType.ServoFault, 3) == "2.3.0");

        TC.ErrorType type; int p1, p2;
        Check("'1.2.1' parses", TC.TryParse("1.2.1", out type, out p1, out p2) &&
              type == TC.ErrorType.CylinderMovement && p1 == 2 && p2 == 1);
        Check("'121' parses the same way", TC.TryParse("121", out type, out p1, out p2) &&
              type == TC.ErrorType.CylinderMovement && p1 == 2 && p2 == 1);
        Check("'1-2-1' too", TC.TryParse("1-2-1", out type, out p1, out p2) && p2 == 1);
        Check("nonsense is refused", !TC.TryParse("", out type, out p1, out p2));

        Console.WriteLine("--- a code explains itself ---");
        Check("1.2.1 reads as words",
              TC.Explain("1.2.1").ToLowerInvariant().Contains("single solenoid") && TC.Explain("1.2.1").ToLowerInvariant().Contains("magnetic"),
              TC.Explain("1.2.1"));
        Check("2.3.0 names the axis", TC.Explain("2.3.0").Contains("axis 3"), TC.Explain("2.3.0"));

        Console.WriteLine("--- a cylinder fault opens ONE document, tailored ---");
        var docs = TC.Resolve("1.2.1").ToList();
        Check("one entry - the parameters tailor it, they do not multiply it", docs.Count == 1, docs.Count.ToString());
        Check("  the converted manual", docs[0].FileName == "Cylinder_Troubleshooting.html", docs[0].FileName);
        Check("  carrying valve 2 and feedback 1", docs[0].Valve == 2 && docs[0].Feedback == 1,
              docs[0].Valve + "/" + docs[0].Feedback);

        var servo = TC.Resolve("2.3.0").ToList();
        Check("a servo fault opens the drive manual",
              servo.Count == 1 && servo[0].FileName.Contains("MR_J4"), servo[0].FileName);

        Console.WriteLine("--- an unparameterised type still resolves ---");
        Check("air pressure resolves", TC.Resolve("3.0.0").Any());
        Check("an unmapped type resolves to nothing", !TC.Resolve("4.0.0").Any());

        Console.WriteLine("--- two generic parameters serve every error type ---");
        var cyl1 = TC.Parameter1(TC.ErrorType.CylinderMovement);
        var cyl2 = TC.Parameter2(TC.ErrorType.CylinderMovement);
        Check("a cylinder fault asks for a valve", cyl1.Caption == "Valve" && cyl1.Options.Length > 1);
        Check("  and a feedback type", cyl2.Caption == "Feedback" && cyl2.Options.Length > 1);

        var srv1 = TC.Parameter1(TC.ErrorType.ServoFault);
        var srv2 = TC.Parameter2(TC.ErrorType.ServoFault);
        Check("a servo fault asks for an axis NUMBER, not a list",
              srv1.Caption == "Axis" && srv1.Options.Length == 0);
        Check("  and nothing for the second parameter", !srv2.Used);

        Check("air pressure asks for neither",
              !TC.Parameter1(TC.ErrorType.AirPressure).Used &&
              !TC.Parameter2(TC.ErrorType.AirPressure).Used);

        Console.WriteLine("--- choices round-trip to digits ---");
        var valve = TC.Describe(TC.ValveType.DoubleSolenoid);
        Check("a chosen valve maps to its digit", TC.ValueOf(cyl1, valve) == (int)TC.ValveType.DoubleSolenoid,
              valve + " -> " + TC.ValueOf(cyl1, valve));
        Check("and back to its text", TC.TextOf(cyl1, (int)TC.ValveType.DoubleSolenoid) == valve);
        Check("a typed axis number is taken as-is", TC.ValueOf(srv1, "3") == 3);
        Check("  and shown back", TC.TextOf(srv1, 3) == "3");
        Check("an unused parameter contributes 0",
              TC.ValueOf(TC.Parameter2(TC.ErrorType.ServoFault), "anything") == 0);

        Console.WriteLine("--- the help documents must actually exist ---");
        Console.WriteLine("      looking in: " + TC.HelpFolder);
        foreach (var kv in TC.DocumentForType)
        {
            if (string.IsNullOrEmpty(kv.Value)) continue;
            var doc = new TC.HelpDocument { FileName = kv.Value };
            Check("  " + kv.Key + " -> " + kv.Value, doc.Exists, doc.FullPath);
        }

        Console.WriteLine(fail==0?"\nTROUBLESHOOTING ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

