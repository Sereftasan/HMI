using System;
using System.Linq;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

class H {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    static void Main(){
        Console.WriteLine("help folder: " + TC.HelpFolder);

        // 1. register an alarm the way a machine's Alarms.json does
        AlarmManager.RegisterDefinition("FA2010.Cyl_Fault", "Alm_Cyl", Kaban.Core.Models.AlarmSeverity.Fault,
                                        plcNumber: 1, address: "M801");
        AlarmManager.RegisterDefinition("FA2010.Servo_X", "Alm_Servo", Kaban.Core.Models.AlarmSeverity.Fault,
                                        plcNumber: 1, address: "M802");

        // 2. the settings page writes codes against the ADDRESS
        AlarmManager.SetTroubleshooting("M801", TC.Compose(TC.ValveType.DoubleSolenoid,
                                                           TC.FeedbackType.TwoMagnetic), "");
        AlarmManager.SetTroubleshooting("M802", TC.Compose(TC.ErrorType.ServoFault, 3), "");

        // 3. a screen showing a RAISED alarm looks the definition up by its code
        var cyl = AlarmManager.FindByAddressOrCode("FA2010.Cyl_Fault");
        var srv = AlarmManager.FindByAddressOrCode("FA2010.Servo_X");
        Check("the definition is found by the raised alarm's code", cyl != null && srv != null);
        Check("the cylinder code survived",
              cyl.TroubleshootingKeys != null && cyl.TroubleshootingKeys[0] == "1.1.2",
              cyl == null ? "null" : string.Join(",", cyl.TroubleshootingKeys ?? new System.Collections.Generic.List<string>()));
        Check("the servo code survived",
              srv.TroubleshootingKeys != null && srv.TroubleshootingKeys[0] == "2.3.0");

        // 4. and each resolves to a document that is actually on disk
        var cylDocs = TC.Resolve(cyl.TroubleshootingKeys[0]).ToList();
        Check("cylinder resolves to ONE tailored document", cylDocs.Count == 1, cylDocs.Count.ToString());
        Check("  all pointing at the cylinder manual",
              cylDocs.All(d => d.FileName.Contains("Cylinder")), cylDocs[0].FileName);
        Check("  tailored to double solenoid 5/2 + two end sensors",
              cylDocs[0].Valve == 1 && cylDocs[0].Feedback == 2, cylDocs[0].Valve + "/" + cylDocs[0].Feedback);
        Check("  and every file exists", cylDocs.All(d => d.Exists),
              string.Join(" | ", cylDocs.Select(d => d.FullPath + (d.Exists ? " ok" : " MISSING"))));

        var srvDocs = TC.Resolve(srv.TroubleshootingKeys[0]).ToList();
        Check("servo resolves to the drive manual",
              srvDocs.Count == 1 && srvDocs[0].FileName.Contains("MR_J4"), srvDocs[0].FileName);
        Check("  which exists", srvDocs[0].Exists, srvDocs[0].FullPath);
        Check("  and names the axis", srvDocs[0].Title.Contains("axis 3"), srvDocs[0].Title);

        Console.WriteLine(fail==0?"\nHELP CHAIN ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

