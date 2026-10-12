using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

class U {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}
    static T Field<T>(object o, string n){ return (T)o.GetType().GetField(n, BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o); }

    [STAThread] static void Main(){
        // the unit's own files, in the Help folder (a run of its own has none)
        System.IO.Directory.CreateDirectory(TC.HelpFolder);
        foreach (var f in new[] { "Clamp_Photo.png", "Clamp_Drawing.pdf" })
            if (!System.IO.File.Exists(System.IO.Path.Combine(TC.HelpFolder, f))) System.IO.File.WriteAllBytes(System.IO.Path.Combine(TC.HelpFolder, f), new byte[] { 1 });
        var host = new Form { ClientSize = new System.Drawing.Size(420, 260) };
        Func<string, CylinderUnitHMI> unit = files => { var u = new CylinderUnitHMI { PlcNumber = 5, TitleTextKey = "Robot Clamping", HelpFiles = files }; u.SetBounds(10, 10, 360, 64); host.Controls.Add(u); return u; };
        var withHelp = unit("Clamp_Photo.png");
        var missing  = unit("Not_There.png");
        var none     = unit("");
        host.Show(); Application.DoEvents();

        Console.WriteLine("--- the blue '?': there when the unit has help that exists ---");
        var info = Field<UnitHelpBadge>(withHelp, "_help");
        Check("shown for a unit with a help file", info.Visible && info.State == UnitHelpBadge.BadgeState.Info);
        Check("hidden when the file is not in the Help folder", !Field<UnitHelpBadge>(missing, "_help").Visible);
        Check("hidden for a unit with no help", !Field<UnitHelpBadge>(none, "_help").Visible);
        int frameTop = (int)(withHelp.Font.GetHeight() / 2f);
        Check("ON the top frame line", Math.Abs(info.Top + info.Height / 2 - frameTop) <= 2, info.Top + " vs line " + frameTop);
        Check("  at its right end", info.Right <= withHelp.ClientSize.Width && info.Left > withHelp.ClientSize.Width / 2, info.Left.ToString());
        Check("no alarm needed: one file opens directly", withHelp.UnitHelpDocuments().Count == 1);
        Check("a unit with help but no error bits gives no alarm links", !withHelp.HelpLinks().Any());

        Console.WriteLine("--- with an alarm active: the step-by-step page, the manual, then unit help, then alarm files ---");
        AlarmHelpLinks.Clear();
        var clamp = new CylinderUnitHMI { PlcNumber = 5, TitleTextKey = "Robot Clamping", ErrorBAddress = "M900",
                                          Valve = CylinderValve.DoubleSolenoid, Feedback = CylinderFeedback.TwoEndMagnetic,
                                          HelpFiles = "Clamp_Photo.png;Clamp_Drawing.pdf" };
        AlarmHelpLinks.Harvest(clamp);
        var docs = AlarmHelpLinks.DocumentsFor(5, "M900", new[] { "MR_J4_TroubleShooting.pdf;Clamp_Photo.png" });
        var names = docs.Select(d => d.FileName).ToList();
        Console.WriteLine("      " + string.Join("  |  ", docs.Select(d => d.Title)));
        Check("the step-by-step page first, then the tailored manual", docs[0].StepByStep && names[1] == "Cylinder_Troubleshooting.html" && !docs[1].StepByStep && docs[1].Valve == 1);
        Check("then the unit's own help", names.Count > 3 && names[2] == "Clamp_Photo.png" && names[3] == "Clamp_Drawing.pdf", string.Join(",", names));
        Check("then the alarm's files", names.Count > 4 && names[4] == "MR_J4_TroubleShooting.pdf");
        Check("a file given twice is listed once", names.Count(n => n == "Clamp_Photo.png") == 1, names.Count.ToString());

        Console.WriteLine("--- an AXIS unit on a manual page ---");
        AlarmHelpLinks.Clear();
        var x1 = new CylinderUnitHMI { PlcNumber = 5, TitleTextKey = "Robot X", Kind = UnitKind.Axis, ErrorAAddress = "M901",
                                       Valve = CylinderValve.DoubleSolenoid, HelpFiles = "Clamp_Drawing.pdf" };
        var link = x1.HelpLinks().Single();
        Check("its error is a servo fault", link.Kind == TC.ErrorType.ServoFault);
        Check("  and a stray valve setting is ignored", link.Valve == 0 && link.Feedback == 0);
        AlarmHelpLinks.Harvest(x1);
        var ax = AlarmHelpLinks.DocumentsFor(5, "M901", null);
        Check("opens the servo manual named for the axis, then its help",
              ax.Count == 2 && ax[0].FileName.StartsWith("MR_J4") && ax[0].Title.Contains("Robot X") && ax[1].FileName == "Clamp_Drawing.pdf",
              string.Join(" | ", ax.Select(d => d.Title)));
        Check("the servo manual is not tailored", ax[0].Valve == 0 && ax[0].Feedback == 0);

        host.Close();
        Console.WriteLine(fail==0?"\nUNIT HELP ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

