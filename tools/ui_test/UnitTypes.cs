using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

// A function unit's valve decides its actions, its feedback its lamps and
// position: what does not match is said (TypeCheck) and, in the designer, framed.
// Each movement has its own help files.
class UT {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static T Field<T>(object o, string n) { return (T)o.GetType().GetField(n, Any).GetValue(o); }

    class DesignSite : ISite {
        public IComponent Component { get; set; }
        public IContainer Container { get { return null; } }
        public bool DesignMode { get { return true; } }
        public string Name { get; set; }
        public object GetService(Type t) { return null; }
    }

    static CylinderUnitHMI Unit(CylinderValve v, CylinderFeedback f, string cmdA, string cmdB, string l1A = "", string l1B = "", string pos = "", string l2A = "") {
        return new CylinderUnitHMI { ValveType = v, FeedbackType = f, CommandAAddress = cmdA, CommandBAddress = cmdB,
                                     Lamp1AAddress = l1A, Lamp1BAddress = l1B, Lamp2AAddress = l2A, PositionAddress = pos };
    }
    static string P(CylinderUnitHMI u) { return string.Join(" | ", u.TypeProblems()); }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        Console.WriteLine("--- the valve decides the actions ---");
        var u = Unit(CylinderValve.SingleSolenoid, CylinderFeedback.SingleMagnetic, "M1", "M2", "X1");
        Check("SingleSolenoid with both commands: one action only", u.TypeProblems().Any(p => p.StartsWith("SingleSolenoid") && p.Contains("one action")), P(u));
        u.CommandAAddress = "";
        Check("  B alone: OK", u.TypeCheck == "OK", u.TypeCheck);
        u.OutputAAddress = "Y1";
        Check("  an output left on the idle action A is pointed out", u.TypeProblems().Any(p => p.Contains("Action A has no command")), P(u));
        u.OutputAAddress = "";
        u.ErrorAAddress = "M900";
        Check("  but an error on the spring-return movement is fine", u.TypeCheck == "OK", u.TypeCheck);
        u.CommandBAddress = "";
        Check("  no command at all: set the driven one", u.TypeProblems().Any(p => p.Contains("set the command")), P(u));

        foreach (var v in new[] { CylinderValve.DoubleSolenoid, CylinderValve.ThreeWayClosedCenter }) {
            var d = Unit(v, CylinderFeedback.TwoEndMagnetic, "M1", "", "X1", "X2");
            string shown = Kaban.UI.Design.CylinderValveConverter.Name(v);   // 3WayClosedCenter (8 October)
            Check(shown + " with one command: both needed (B missing)", d.TypeProblems().Any(p => p.StartsWith(shown) && p.Contains("B is missing")), P(d));
            d.CommandBAddress = "M2";
            Check("  both: OK", d.TypeCheck == "OK", d.TypeCheck);
        }

        Console.WriteLine("--- the feedback decides the lamps and the position ---");
        foreach (var f in new[] { CylinderFeedback.SingleMagnetic, CylinderFeedback.SingleInductive }) {
            var s = Unit(CylinderValve.DoubleSolenoid, f, "M1", "M2", "X1", "X2");
            Check(f + " with a lamp on each side: one lamp only", s.TypeProblems().Any(p => p.StartsWith(f.ToString()) && p.Contains("one lamp")), P(s));
            s.Lamp1AAddress = "";
            Check("  one lamp (on B): OK", s.TypeCheck == "OK", s.TypeCheck);
            s.PositionAddress = "D10";
            Check("  a position is pointed out (not this feedback's)", s.TypeProblems().Any(p => p.Contains("no position is read")), P(s));
        }
        foreach (var f in new[] { CylinderFeedback.TwoEndMagnetic, CylinderFeedback.MagneticPlusPressureSwitch }) {
            var t = Unit(CylinderValve.DoubleSolenoid, f, "M1", "M2", "X1", "", "", "X3");
            Check(f + " with both lamps on A: one on each action", t.TypeProblems().Any(p => p.StartsWith(f.ToString()) && p.Contains("A has 2, B has 0")), P(t));
            t.Lamp2AAddress = ""; t.Lamp1BAddress = "X2";
            Check("  one on each: OK", t.TypeCheck == "OK", t.TypeCheck);
            t.PositionAddress = "D10";
            if (f == CylinderFeedback.TwoEndMagnetic)
                Check("  a position is pointed out (not this feedback's)", t.TypeProblems().Any(p => p.Contains("no position is read")), P(t));
            else
                Check("  a clamp with a pressure switch may also measure (a position): OK", t.TypeCheck == "OK", t.TypeCheck);
        }
        var an = Unit(CylinderValve.ThreeWayClosedCenter, CylinderFeedback.AnalogueMagnetic, "M1", "M2", "X1");
        // 9 October: a cylinder unit placed by hand has no position of its own any more - the check
        // names the control a position belongs to; the unit inside one says to set it.
        Check("AnalogueMagnetic with a lamp and no position: both said (the position: a PneumaticPositioningHMI's)",
              an.TypeProblems().Any(p => p.Contains("no lamps")) && an.TypeProblems().Any(p => p.Contains("PneumaticPositioningHMI")), P(an));
        an.PartOfComposite = true;
        Check("  inside a control that owns its position: set PositionAddress", an.TypeProblems().Any(p => p.Contains("set PositionAddress")), P(an));
        an.PartOfComposite = false;
        var en0 = Unit(CylinderValve.DoubleSolenoid, CylinderFeedback.EncoderPlusReference, "M1", "M2", "X1", "", "");
        Check("EncoderPlusReference with no position: a CylinderEncoderUnitHMI's", en0.TypeProblems().Any(p => p.Contains("CylinderEncoderUnitHMI")), P(en0));
        an.Lamp1AAddress = ""; an.PositionAddress = "D16";
        Check("  the position alone: OK", an.TypeCheck == "OK", an.TypeCheck);
        var en = Unit(CylinderValve.DoubleSolenoid, CylinderFeedback.EncoderPlusReference, "M1", "M2", "", "", "D110");
        Check("EncoderPlusReference without the reference lamp", en.TypeProblems().Any(p => p.Contains("the reference sensor")), P(en));
        en.Lamp1BAddress = "X9";
        Check("  the position and one lamp: OK", en.TypeCheck == "OK", en.TypeCheck);

        Console.WriteLine("--- untyped, half typed, typed later ---");
        var plain = new CylinderUnitHMI { CommandBAddress = "M660", Lamp1BAddress = "M243" };
        Check("a motor with neither set is not checked", plain.TypeCheck == "OK", plain.TypeCheck);
        plain.ValveType = CylinderValve.SingleSolenoid;
        Check("  a valve without a feedback asks for it", plain.TypeProblems().Any(p => p.StartsWith("FeedbackType is not set")), P(plain));
        var later = Unit((CylinderValve)7, (CylinderFeedback)9, "M1", "M2");
        Check("a type added later is reported as not checked yet",
              later.TypeProblems().Any(p => p.Contains("ValveType 7 has no check")) && later.TypeProblems().Any(p => p.Contains("FeedbackType 9 has no check")), P(later));
        var axis = Unit(CylinderValve.SingleSolenoid, CylinderFeedback.SingleMagnetic, "M1", "M2", "X1", "X2");
        axis.Kind = UnitKind.Axis;
        Check("an axis unit is not a cylinder: not checked", axis.TypeCheck == "OK", axis.TypeCheck);
        var props = TypeDescriptor.GetProperties(typeof(CylinderUnitHMI));
        Check("TypeCheck sits with the types, read only, never written by the designer",
              props["TypeCheck"].Category.TrimStart('\t') == "Valve & Feedback" && props["TypeCheck"].IsReadOnly &&
              props["TypeCheck"].SerializationVisibility == DesignerSerializationVisibility.Hidden);

        Console.WriteLine("--- in the designer, a mismatch is framed ---");
        var host = new Form { ClientSize = new Size(800, 300) };
        var dz = new CylinderUnitHMI();
        dz.Site = new DesignSite { Component = dz, Name = "dz" };
        dz.SetBounds(10, 10, 360, 64); host.Controls.Add(dz); host.Show(); Application.DoEvents();
        dz.ValveType = CylinderValve.DoubleSolenoid; dz.FeedbackType = CylinderFeedback.TwoEndMagnetic;
        dz.CommandAAddress = "M1";
        var grp = dz.Controls.Cast<Control>().First(c => c.GetType().Name == "RoundedGroupBox");
        Func<Color> frame = () => (Color)grp.GetType().GetProperty("BorderColor").GetValue(grp, null);
        Check("amber while it does not match", frame().ToArgb() == Color.FromArgb(230, 150, 30).ToArgb(), frame().ToString());
        dz.CommandBAddress = "M2"; dz.Lamp1AAddress = "X1"; dz.Lamp1BAddress = "X2";
        Check("  the normal frame once it does", frame().ToArgb() == Color.FromArgb(180, 180, 180).ToArgb(), frame().ToString());
        var run = Unit(CylinderValve.DoubleSolenoid, CylinderFeedback.TwoEndMagnetic, "M1", "");
        run.SetBounds(10, 100, 360, 64); host.Controls.Add(run); Application.DoEvents();
        var rgrp = run.Controls.Cast<Control>().First(c => c.GetType().Name == "RoundedGroupBox");
        Check("  at run time nothing changes: frame as always, both slots as wired",
              ((Color)rgrp.GetType().GetProperty("BorderColor").GetValue(rgrp, null)).ToArgb() == Color.FromArgb(180, 180, 180).ToArgb() &&
              run.ButtonA.Visible && !run.ButtonB.Visible);

        Console.WriteLine("--- each movement its own help ---");
        string help = Path.Combine(Path.GetTempPath(), "ut_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(help);
        foreach (var f in Directory.GetFiles(TC.HelpFolder)) File.Copy(f, Path.Combine(help, Path.GetFileName(f)));
        File.WriteAllText(Path.Combine(help, "Clamp_A_Valve.png"), "x");
        File.WriteAllText(Path.Combine(help, "Clamp_B_Switch.png"), "x");
        File.WriteAllText(Path.Combine(help, "Clamp_Both.pdf"), "x");
        TC.HelpFolder = help;

        var legacy = new CylinderUnitHMI { HelpFiles = "Clamp_Both.pdf" };
        Check("an old page's HelpFiles goes to both movements", legacy.HelpAFiles == "Clamp_Both.pdf" && legacy.HelpBFiles == "Clamp_Both.pdf" && legacy.HelpFiles == "Clamp_Both.pdf",
              legacy.HelpAFiles + " / " + legacy.HelpBFiles);

        var c2 = Unit(CylinderValve.DoubleSolenoid, CylinderFeedback.MagneticPlusPressureSwitch, "M692", "M685", "X1", "X2");
        c2.PlcNumber = 1; c2.TitleTextKey = "Profile Clamping 3";
        c2.ErrorAAddress = "M867"; c2.ErrorBAddress = "M866";
        c2.HelpAFiles = "Clamp_A_Valve.png;Clamp_Both.pdf"; c2.HelpBFiles = "Clamp_B_Switch.png;Clamp_Both.pdf";
        c2.SetBounds(10, 180, 360, 64); host.Controls.Add(c2); Application.DoEvents();
        var links = c2.HelpLinks().ToList();
        Check("the alarm link of each error carries that movement's files",
              links.Count == 2 && links[0].HelpFiles.SequenceEqual(new[] { "Clamp_A_Valve.png", "Clamp_Both.pdf" }) &&
              links[1].HelpFiles.SequenceEqual(new[] { "Clamp_B_Switch.png", "Clamp_Both.pdf" }),
              string.Join(" / ", links.Select(l => string.Join(",", l.HelpFiles))));
        var calm = c2.HelpDocuments().Select(d => d.FileName).ToList();
        Check("nothing failed: every file of the unit once - and no troubleshooting (a fault's only, 30 September)",
              calm.SequenceEqual(new[] { "Clamp_A_Valve.png", "Clamp_Both.pdf", "Clamp_B_Switch.png" }),
              string.Join(" | ", calm));
        // 24 October: no fault - the help list's AI entries there, greyed, saying why (not left out)
        var calmMenu = Kaban.UI.Screens.MachineAlarmScreen.BuildHelpMenu(c2.HelpDocuments(), d => { }, null, null, "For a fault: the AI prompt is about what failed");
        var aiItem = calmMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Name == "aiPrompt");
        Check("no fault: the files, then the AI prompt entry greyed, saying why", aiItem != null && !aiItem.Enabled && aiItem.ToolTipText.StartsWith("For a fault") &&
              calmMenu.Items.OfType<ToolStripMenuItem>().Count(i => i.Enabled) == 3 && calmMenu.ShowItemToolTips,
              string.Join(" | ", calmMenu.Items.Cast<ToolStripItem>().Select(i => i.Text + (i.Enabled ? "" : " (off)"))));
        Field<AlarmHelpBadge>(c2, "_helpB").UpdateState(a => a == "M866" ? 1 : 0, c => true);
        var failedDocs = c2.HelpDocuments();
        var failedB = failedDocs.Select(d => d.FileName).ToList();
        // 24 October: "the additional files should be listed at top, then the automatically created error helper items"
        Check("B failed: the files first - B's own, then A's - the step-by-step page made for it last (the manual inside it)",
              failedB.SequenceEqual(new[] { "Clamp_B_Switch.png", "Clamp_Both.pdf", "Clamp_A_Valve.png", "Cylinder_Troubleshooting.html" }) &&
              failedDocs[3].StepByStep, string.Join(" | ", failedB));
        Check("  no alarm names its error bit: the page named \"Step by step - <the movement>\"",
              failedDocs[3].Title == "Step by step - " + TC.MovementName(c2.MovementFor("B")), failedDocs[3].Title);
        // 24 October: "should be renamed as the error name instead of 'Step by step - Release'"
        AlarmManager.RegisterDefinition("T.PC3Clamp", "Profile Clamping 3 did not clamp", Kaban.Core.Models.AlarmSeverity.Fault, plcNumber: 1, address: "M866");
        var named = c2.HelpDocuments().First(d => d.StepByStep);
        Check("  an alarm for it: the page named as the error", named.Title == "Profile Clamping 3 did not clamp" && c2.ErrorName("B") == named.Title, named.Title);

        host.Close();
        try { Directory.Delete(help, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nUNIT TYPES ALL PASS" : "\nUNIT TYPES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
