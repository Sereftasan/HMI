using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.Machines.MK2010.Screens.Home;
using Kaban.Machines.MK2010.Screens.Manual;
using Kaban.Machines.MK2010.Screens.Parameters;

// 11 November: "can you add "MK2010" project from "_MK2010" as well" - the old program's MK2010 pages
// (tools/legacy_mk2010_pages.py, its --check against tools/legacy/MK2010): Home (the job), Manual Control,
// References and the Machine Parameters tabs; the project builds its own MK2010 program.
class MKP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }
    static string Bits(ButtonHMI b) { return string.Join(",", b.BitActions.Select(a => a.Address + ":" + a.Action)); }
    static ButtonHMI Btn(Control page, string addr) { return Tree(page).OfType<ButtonHMI>().FirstOrDefault(b => b.BitActions.Any(a => a.Address == addr)); }

    static void Shot(Control page, string path) {
        var f = new Form { ClientSize = new Size(Math.Min(1270, page.Width), Math.Min(1000, page.Height)), StartPosition = FormStartPosition.Manual,
                           Location = new Point(0, 0) };
        f.Controls.Add(page);
        f.Show();
        var until = DateTime.Now.AddMilliseconds(500);
        while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
        using (var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height)) {
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(f.PointToScreen(Point.Empty), Point.Empty, bmp.Size);
            bmp.Save(path);
        }
        f.Controls.Remove(page);
        f.Close();
    }

    [STAThread]
    static void Main(string[] args) {
        AuditLogger.LogDirectory = Path.Combine(Path.GetTempPath(), "mkp_" + Guid.NewGuid().ToString("N"));
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();

        Console.WriteLine("--- Home (ucJobNew) ---");
        var home = new HomeJob();
        var disp = Tree(home).OfType<NumericDisplayHMI>().ToList();
        Check("the heads' temperatures D215 ... D218 and their actual D381 ... D384 (raw / 10, °C)",
              new[] { "D215", "D218", "D381", "D384" }.All(a => disp.Any(d => d.Address == a && d.Divisor == 10 && d.Suffix == "°C")));
        Check("  the axes' target D32 / D34 / D36 and actual D50 / D52 / D54; melting D349 / TN58, welding D350 / TN6",
              new[] { "D32", "D34", "D36", "D50", "D52", "D54", "D349", "TN58", "D350", "TN6" }.All(a => disp.Any(d => d.Address == a)));
        var nums = Tree(home).OfType<NumericInputHMI>().ToList();
        var wx = nums.FirstOrDefault(n => n.Address == "D1200");
        Check("the job's Width X D1200 (32-bit / 10), its limits the PLC's D144 / D146; Width Y D1202 (D148 / D150); Quantity D334",
              wx != null && wx.Divisor == 10 && wx.DataType == DataTypeEnum.Bin_32 && wx.MinLimit.Type == LimitSourceType.Tag && wx.MinLimit.TagAddress == "D144" &&
              wx.MaxLimit.TagAddress == "D146" && nums.Any(n => n.Address == "D1202" && n.MaxLimit.TagAddress == "D150") && nums.Any(n => n.Address == "D334"));
        Check("  an operator's to type", nums.All(n => n.RequiredSecurity == SecurityLevel.Operator));

        // 12 November: "We gotta update these controls to axis setting controls first and change their manual movements to axis unit
        // controls in the manual movement pages" (the user, beside MK2010's Calibration page)
        Console.WriteLine("--- Calibration: an axis settings row per axis ---");
        var cal = new ParamCalibration();
        var rows = Tree(cal).OfType<AxisSettingHMI>().OrderBy(r => r.HMIAxisIndex).ToList();
        Check("X, Y1, Y2 (1 - 3): their jog M41 / M40, M43 / M42, M45 / M44, positions D50 / D52 / D54 (raw / 10, 1 decimal as the old page)",
              rows.Count == 3 && rows.Select(r => r.CommandBackAddress + "/" + r.CommandForwardAddress + " " + r.PositionAddress).SequenceEqual(
                  new[] { "M41/M40 D50", "M43/M42 D52", "M45/M44 D54" }) && rows.All(r => r.Divisor == 10 && r.DecimalPlaces == 1),
              string.Join(" | ", rows.Select(r => r.CommandBackAddress + "/" + r.CommandForwardAddress + " " + r.PositionAddress)));
        Check("  the real values D154 / D156 / D158 their calibration, Calibrate M91 / M92 / M93; a Maintenance's",
              rows.Select(r => r.CalibrationAddress + " " + r.CalibrateAddress).SequenceEqual(new[] { "D154 M91", "D156 M92", "D158 M93" }) &&
              rows.All(r => r.RequiredSecurity == SecurityLevel.Maintenance));
        // 17 November: each one's send to reference (M30 ... M32, the old References page's)
        Check("  their send to reference M30 / M31 / M32", rows.Select(r => r.GoToReferenceAddress).SequenceEqual(new[] { "M30", "M31", "M32" }));
        var calAll = Tree(cal).OfType<ButtonHMI>().FirstOrDefault(b => b.FallbackText == "Calibrate All");
        Check("  Calibrate All sets the three", calAll != null && Bits(calAll) == "M91:SET,M92:SET,M93:SET");

        Console.WriteLine("--- Manual Control, References ---");
        Kaban.UI.Helpers.AlarmHelpLinks.DiscoverAll();
        Kaban.UI.Helpers.AlarmHelpLinks.Harvest(cal);
        var man = new ManualControl();
        var axes = Tree(man).OfType<AxisUnitHMI>().OrderBy(u => u.AxisNumber).ToList();
        Check("X, Y1, Y2 as axis units (1 - 3), each from its row: Axis X Back M41 / Forward M40, position D50 / 10",
              axes.Count == 3 && axes.All(u => u.IsResolved) && axes[0].Unit.CommandAAddress == "M41" && axes[0].Unit.CommandBAddress == "M40" &&
              axes[0].Unit.PositionAddress == "D50" && axes[0].Unit.PositionDivisor == 10 && axes[0].Unit.TitleTextKey == "Axis_MK2010_X",
              axes.Count + " " + string.Join(",", axes.Select(u => u.Unit.TitleTextKey)));
        var lim = Tree(man).OfType<LampHMI>().FirstOrDefault(l => l.Address == "X0006");
        Check("  its limit X0006 lit while the input is OFF (the old negative lamp)", lim != null && lim.OffColor.G > lim.OffColor.R && lim.OnColor.R == lim.OnColor.G);
        // 12 November: MK2010's drawings and PLC program (its device comments) - a cylinder unit per head and cylinder
        Console.WriteLine("--- Manual Control: the cylinders, head by head ---");
        var cu = Tree(man).OfType<CylinderUnitHMI>().Where(u => u.Kind == UnitKind.Cylinder).ToList();
        Func<string, CylinderUnitHMI> unit = k => cu.FirstOrDefault(u => u.TitleTextKey == k);
        Check("38 units: per head the supporting strip, side tables 1 / 2, heater plate, top press, profile lifting and emptying, sliding sash press; "
              + "the gasket presses of heads 1-4 and 2-3", cu.Count == 38 && new[] { 1, 2, 3, 4 }.All(h => cu.Count(u => u.TitleTextKey.StartsWith("Unit_MK2010_H" + h)) == 8),
              cu.Count.ToString());
        var ss = unit("Unit_MK2010_H3SupportingStrip");
        Check("Head 3 Supporting Strip: Y1.3 single 5/2 - its coil down: M101 set, reset back (the spring); back X0013 / forward X0033; M602 / M600",
              ss != null && ss.ValveType == CylinderValve.SingleSolenoid && ss.FeedbackType == CylinderFeedback.TwoEndMagnetic &&
              ss.CommandAAddress == "M101" && ss.CommandAAction == BitActionType.RESET && ss.CommandBAddress == "M101" && ss.CommandBAction == BitActionType.SET &&
              ss.Lamp1AAddress == "X0013" && ss.Lamp1BAddress == "X0033" && ss.ErrorAAddress == "M602" && ss.ErrorBAddress == "M600" && ss.TypeCheck == "OK",
              ss == null ? "-" : ss.TypeCheck);
        var hp = unit("Unit_MK2010_H4HeaterPlate");
        Check("Head 4 Heater Plate: Y5.4 / Y6.4 double 5/2 - M104 reset back, set forward; back X0030, forward X0040; M604 / M603",
              hp != null && hp.ValveType == CylinderValve.DoubleSolenoid && hp.CommandAAddress == "M104" && hp.CommandAAction == BitActionType.RESET &&
              hp.CommandBAddress == "M104" && hp.CommandBAction == BitActionType.SET && hp.Lamp1AAddress == "X0030" && hp.Lamp1BAddress == "X0040" &&
              hp.ErrorAAddress == "M604" && hp.ErrorBAddress == "M603" && hp.TypeCheck == "OK", hp == null ? "-" : hp.TypeCheck);
        var t2 = unit("Unit_MK2010_H2SideTable2");
        Check("Head 2 Side Table 2: single valves forward / back (as a 5/3 open centre), its back sensor X0022 (the old Back Plate's), M601",
              t2 != null && t2.ValveType == CylinderValve.ThreeWayOpenCenter && t2.FeedbackType == CylinderFeedback.SingleMagnetic &&
              t2.Lamp1AAddress == "X0022" && t2.ErrorAAddress == "M601" && t2.TypeCheck == "OK", t2 == null ? "-" : t2.TypeCheck);
        var bad = cu.Where(u => u.TypeCheck != "OK").Select(u => u.TitleTextKey + ": " + u.TypeCheck).ToList();
        Check("  every unit as its valve and feedback ask (the sliding sash press and the gasket presses: not on the drawings, not set)",
              bad.Count == 0 && cu.Count(u => u.ValveType == CylinderValve.Unspecified) == 10, string.Join(" | ", bad.Take(3)));
        var lamps = cu.SelectMany(u => new[] { u.Lamp1AAddress, u.Lamp1BAddress }).Where(a => !string.IsNullOrEmpty(a)).ToList();
        Check("  the old page's sensors on them (X0011 ... X0040, X0054 ... X0057, X0060 ... X0065)",
              new[] { "X0011", "X0034", "X0015", "X0020", "X0021", "X0024", "X0025", "X0040", "X0054", "X0057", "X0060", "X0061", "X0062", "X0065" }.All(lamps.Contains));
        // 17 November: the old table's sensor rows (the cylinders' sensors again) left to the units
        var loose = Tree(man).OfType<LampHMI>().Where(l => !(l.Parent is CylinderUnitHMI) && l.Parent == man).Select(l => l.Address).ToList();
        Check("  the sensors only on their units (no lamp rows X0031 / X0011 ... beside them); Gasket Pressing M316 once",
              !loose.Any(a => new[] { "X0031", "X0011", "X0035", "X0025", "X0064", "X0065", "X0062", "X0063", "X0060", "X0061" }.Contains(a)) &&
              loose.Count(a => a == "M316") == 1, string.Join(",", loose));
        Check("  Automatic On/Off M34 and Conveyor M106 stay buttons (no cylinders)",
              Bits(Btn(man, "M34")) == "M34:Alternate" && Bits(Btn(man, "M106")) == "M106:Alternate");
        // one alarm bit, the four heads' units: the alarm's help offers each one's page
        Kaban.UI.Helpers.AlarmHelpLinks.Harvest(man);
        var docs = Kaban.UI.Helpers.AlarmHelpLinks.DocumentsFor(1, "M600", null).Where(d => d.StepByStep).ToList();
        Check("the alarm M600 (the strips did not go down - one bit): its help a page for each head's strip",
              docs.Count == 4 && docs.Select(d => d.Movement.Unit).Distinct().Count() == 4, string.Join(" | ", docs.Select(d => d.Title)));
        var alarms = File.Exists("Alarms.json") ? File.ReadAllText("Alarms.json") : File.ReadAllText(Path.Combine("MK2010", "Alarms.json"));
        var noAlarm = cu.SelectMany(u => new[] { u.ErrorAAddress, u.ErrorBAddress }).Where(a => !string.IsNullOrEmpty(a) && !alarms.Contains("\"" + a + "\"")).ToList();
        Check("the alarm list from the PLC program (M600 ... M635, 34): every unit's bit on it", noAlarm.Count == 0 &&
              System.Text.RegularExpressions.Regex.Matches(alarms, "PlcAddress").Count == 34, string.Join(",", noAlarm));
        var refs = new ManualReference();
        var all = Tree(refs).OfType<ButtonHMI>().FirstOrDefault(b => b.FallbackText == "Reference All");
        Check("References: X / Y1 / Y2 M30 / M31 / M32, Reference All SETs the three, OK lamps M22 / M20 / M21",
              Btn(refs, "M30") != null && all != null && Bits(all) == "M30:SET,M31:SET,M32:SET" &&
              new[] { "M22", "M20", "M21" }.All(a => Tree(refs).OfType<LampHMI>().Any(l => l.Address == a)), all == null ? "-" : Bits(all));
        // 17 November: an axis unit each; the reference switches instead of the old Limit lamps (Manual Control's toggles)
        var refAxes = Tree(refs).OfType<AxisUnitHMI>().OrderBy(u => u.AxisNumber).ToList();
        var refLamps = Tree(refs).OfType<LampHMI>().Where(l => l.Parent == refs).Select(l => l.Address).ToList();
        Check("  X / Y1 / Y2 as axis units (their jog and position), their reference switches X0006 / X0007 / X0010; no M100 / M102 / M104",
              refAxes.Count == 3 && refAxes.All(u => u.IsResolved) && refAxes[1].Unit.CommandBAddress == "M42" &&
              new[] { "X0006", "X0007", "X0010" }.All(refLamps.Contains) && !refLamps.Any(a => a == "M100" || a == "M102" || a == "M104"),
              string.Join(",", refLamps));

        Console.WriteLine("--- Machine Parameters ---");
        var sys = new ParamSystem();
        var opts = Tree(sys).OfType<CheckBoxHMI>().Select(o => o.Address).ToList();
        Check("System Parameters: 16 values, the options M1022 M1000 M1026 M1002 M1032 M1024 M1016 M1028",
              Tree(sys).OfType<NumericInputHMI>().Count() == 16 && string.Join(",", opts) == "M1022,M1000,M1026,M1002,M1032,M1024,M1016,M1028", string.Join(",", opts));
        var sys2 = new ParamSystem2();
        Check("System Parameters 2: 24 X / Y values, an Admin's (the old password page)", Tree(sys2).OfType<NumericInputHMI>().Count() == 24 &&
              Tree(sys2).OfType<NumericInputHMI>().All(n => n.RequiredSecurity == SecurityLevel.Admin), Tree(sys2).OfType<NumericInputHMI>().Count().ToString());
        var rep = new ParamReports();
        Check("Reports: the hours D296 / D226 as they are, the minutes D298 / D228 (raw / 60)",
              Tree(rep).OfType<NumericDisplayHMI>().Any(d => d.Address == "D296" && d.Divisor == 1) &&
              Tree(rep).OfType<NumericDisplayHMI>().Any(d => d.Address == "D298" && d.Divisor == 60));
        var prod = new ParamProduction();
        Check("Production Parameters: the temperatures D206, D211 ... D214, the options M1008 M1001 M1004 M1034 M1030",
              Tree(prod).OfType<NumericInputHMI>().Count() == 6 && Tree(prod).OfType<CheckBoxHMI>().Count() == 5);

        var pages = new Control[] { home, man, refs, prod, cal, sys, sys2, new ParamDemo(), rep };
        var overlaps = new List<string>();
        foreach (var pg in pages) {
            var cs = pg.Controls.Cast<Control>().ToList();
            for (int i = 0; i < cs.Count; i++)
                for (int j = i + 1; j < cs.Count; j++)
                    if (cs[i].Bounds.IntersectsWith(cs[j].Bounds)) overlaps.Add(pg.Name + ": " + cs[i].Name + " / " + cs[j].Name);
        }
        Check("no two controls of a page on each other; every page fits the window", overlaps.Count == 0 && pages.All(p => p.Width <= 1180),
              string.Join("; ", overlaps.Take(4)) + " " + string.Join(" ", pages.Select(p => p.Name + " " + p.Width)));

        Console.WriteLine("--- in Turkish ---");
        LanguageManager.LoadMachineTranslations(Path.Combine("MK2010", "Translations.csv"));
        var keys = new List<string>();
        foreach (var pg in pages)
            foreach (var c in Tree(pg).Where(k => !(k.Parent is AxisUnitHMI)))          // an axis unit's name is its row's
                foreach (var pn in new[] { "TranslationKey", "AxisNameKey" }) {
                    var pi = c.GetType().GetProperty(pn);
                    var v = pi == null ? null : pi.GetValue(c, null) as string;
                    if (!string.IsNullOrWhiteSpace(v) && !System.Text.RegularExpressions.Regex.IsMatch(v, "^[XYZ][12]?$")) keys.Add(v);
                }
        keys.AddRange(new[] { "Machine_MK2010", "Tab_MK2010_ManualControl", "Tab_MK2010_ManualReference", "Tab_MK2010_ParamProduction",
                              "Tab_MK2010_ParamCalibration", "Tab_MK2010_ParamSystem", "Tab_MK2010_ParamSystem2", "Tab_MK2010_ParamDemo",
                              "Tab_MK2010_ParamReports" });
        var missing = keys.Distinct().Where(k => LanguageManager.GetStringIn("TR", k) == null).ToList();
        Check("every caption of the pages, their tabs and the machine's name has its Turkish (" + keys.Distinct().Count() + ")", missing.Count == 0,
              string.Join(", ", missing));

        if (args.Length > 0) {
            Shot(new HomeJob(), Path.Combine(args[0], "mk_home.png"));
            Shot(new ManualControl(), Path.Combine(args[0], "mk_manual.png"));
            Shot(new ParamCalibration(), Path.Combine(args[0], "mk_calib.png"));
            Shot(new ManualReference(), Path.Combine(args[0], "mk_refs.png"));
        }
        Console.WriteLine(fail == 0 ? "\nMK2010 PAGES ALL PASS" : "\nMK2010 PAGES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
