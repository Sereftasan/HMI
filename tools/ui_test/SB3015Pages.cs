using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.Machines.SB3015.Screens.Home;
using Kaban.Machines.SB3015.Screens.Manual;
using Kaban.Machines.SB3015.Screens.Parameters;

// 17 November: "Can you complete SB3015 and MK2010 projects using KabanMachineSystem project (I want you to use cylinder unit
// HMI and axis unit HMI controls instead of the controls in KabanMachineSystem (same as our other machine projects)" - the old
// program's _SB3015 pages (tools/legacy_sb3015_pages.py, its --check against tools/legacy/SB3015).
class SBP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }
    static string Bits(ButtonHMI b) { return b == null ? "-" : string.Join(",", b.BitActions.Select(a => a.Address + ":" + a.Action)); }
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
        AuditLogger.LogDirectory = Path.Combine(Path.GetTempPath(), "sbp_" + Guid.NewGuid().ToString("N"));
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();

        Console.WriteLine("--- Axis Settings: an axis settings row per axis (the old Axis Parameters, Tool Settings and Servo Status) ---");
        var ax = new ParamAxisSettings();
        var rows = Tree(ax).OfType<AxisSettingHMI>().OrderBy(r => r.HMIAxisIndex).ToList();
        Check("9 axes: Robot X, Milling A / 1 Y / 1 Z / 2 Y / 2 Z, Cutting Z / A, Exit Robot X", rows.Count == 9 &&
              rows.Select(r => r.AxisNameKey).SequenceEqual(new[] { "Axis_SB3015_RobotX", "Axis_SB3015_MillingA", "Axis_SB3015_Milling1Y",
                  "Axis_SB3015_Milling1Z", "Axis_SB3015_Milling2Y", "Axis_SB3015_Milling2Z", "Axis_SB3015_CuttingZ", "Axis_SB3015_CuttingA",
                  "Axis_SB3015_ExitRobotX" }), string.Join(",", rows.Select(r => r.AxisNameKey)));
        var rx = rows[0];
        Check("Robot X: jog M604 / M605, position W0 (raw / 10000), limits D314 / D312, reference D316, calibration D580, its sensor M330 on Forward",
              rx.CommandBackAddress == "M604" && rx.CommandForwardAddress == "M605" && rx.PositionAddress == "W0" && rx.Divisor == 10000 &&
              rx.MinLimitAddress == "D314" && rx.MaxLimitAddress == "D312" && rx.ReferenceAddress == "D316" && rx.CalibrationAddress == "D580" &&
              rx.ReferenceSensorAddress == "M330" && rx.ReferenceSensorSide == AxisReferenceSide.Forward);
        Check("  send to reference B0, load WA, reference request W8.3, Z distance D73, codes W4 / WB / W5, its alarm M801",
              rx.GoToReferenceAddress == "B0" && rx.CurrentAddress == "WA" && rx.HomeRequestAddress == "W8.3" && rx.ZDistanceAddress == "D73" &&
              rx.AxisAlarmAddress == "W4" && rx.ServoAlarmAddress == "WB" && rx.AxisWarningAddress == "W5" && rx.ErrorAddress == "M801");
        var ma = rows[1];
        Check("Milling A in degrees (raw / 100000), its sensor M331 on Back; a Maintenance's", ma.Divisor == 100000 && ma.Suffix == "°" &&
              ma.ReferenceSensorSide == AxisReferenceSide.Back && rows.All(r => r.RequiredSecurity == SecurityLevel.Maintenance));
        Check("Milling 2 Y's reference request W58.3 (the old page's W68.3 is Milling 2 Z's); Cutting A without Z distance, servo and warning codes",
              rows[4].HomeRequestAddress == "W58.3" && rows[5].HomeRequestAddress == "W68.3" && string.IsNullOrEmpty(rows[7].ZDistanceAddress) &&
              string.IsNullOrEmpty(rows[7].ServoAlarmAddress) && string.IsNullOrEmpty(rows[7].AxisWarningAddress));
        var nums = Tree(ax).OfType<NumericInputHMI>().ToList();
        Check("the backlash: D562 ... D575 (raw / 10 mm, the A axes / 100 degrees); Send All to Reference SETs M22",
              nums.Count == 9 && nums.Any(n => n.Address == "D562" && n.Divisor == 10) && nums.Any(n => n.Address == "D563" && n.Divisor == 100) &&
              Bits(Btn(ax, "M22")) == "M22:SET", nums.Count.ToString());

        Console.WriteLine("--- Encoders, Positioning ---");
        var enc = new ParamEncoders();
        var er = Tree(enc).OfType<CylinderEncoderSettingHMI>().OrderBy(r => r.EncoderNumber).ToList();
        Check("3 encoder rows: Profile Recognition Front (D4, reference D477, tolerance D479), Top (D6), Right Top Clamping (D17, D515 / 100)",
              er.Count == 3 && er[0].PositionAddress == "D4" && er[0].ReferenceAddress == "D477" && er[0].MeasuringToleranceAddress == "D479" &&
              er[1].PositionAddress == "D6" && er[2].PositionAddress == "D17" && er[2].ReferenceAddress == "D515" && er[2].Divisor == 100 && er[0].Divisor == 10,
              string.Join(",", er.Select(r => r.PositionAddress)));
        Check("  their movements, sensors and alarms: M646 (M217) / M645, M845 / M844 ...", er[0].CommandBackAddress == "M646" &&
              er[0].BackSensorAddress == "M217" && er[0].ErrorBackAddress == "M845" && er[0].ErrorForwardAddress == "M844" &&
              er[2].BackSensorAddress == "M233" && er[2].ForwardSensorAddress == "M234");
        var tc = er.Where(r => r.TypeCheck != "OK").Select(r => r.TitleTextKey + ": " + r.TypeCheck).ToList();
        Check("  each as its valve and feedback ask", tc.Count == 0, string.Join(" | ", tc));
        var pos = new ParamPositioning();
        var pp = Tree(pos).OfType<PneumaticPositioningHMI>().ToList();
        var lf = pp.FirstOrDefault(r => r.PositionAddress == "D113");
        Check("Positioning: the front verticals D113 / D116 - target D153, send M53, calibration D133, calibrate M33, tolerance D753, direction D793",
              pp.Count == 2 && lf != null && lf.TargetPosition == "D153" && lf.PositioningStartAddress == "M53" && lf.CalibrationAddress == "D133" &&
              lf.CalibrateAddress == "M33" && lf.PositioningToleranceAddress == "D753" && lf.PositioningDirectionReferenceAddress == "D793" &&
              pp.Any(r => r.PositionAddress == "D116" && r.TargetPosition == "D156"));
        Check("  a 5/3 closed centre, its analogue sensor; down Back (M864), up Forward (M863)", lf != null && lf.ValveType == CylinderValve.ThreeWayClosedCenter &&
              lf.FeedbackType == CylinderFeedback.AnalogueMagnetic && lf.VerticalMovement && lf.ErrorBackAddress == "M864" && lf.ErrorForwardAddress == "M863" &&
              pp.All(r => r.TypeCheck == "OK"), lf == null ? "-" : lf.TypeCheck);

        Console.WriteLine("--- Manual pages ---");
        Kaban.UI.Helpers.AlarmHelpLinks.DiscoverAll();
        Kaban.UI.Helpers.AlarmHelpLinks.Harvest(ax);
        Kaban.UI.Helpers.AlarmHelpLinks.Harvest(enc);
        var manual = new Control[] { new ManualEntryConveyor(), new ManualRobot(), new ManualProfileRecognition(), new ManualMilling(), new ManualCutting(),
                                     new ManualExit(), new ManualOther() };
        var axes = manual.SelectMany(p => Tree(p).OfType<AxisUnitHMI>()).ToList();
        Check("9 axis units, each from its row (Robot X: M604 / M605, W0 / 10000)", axes.Count == 9 && axes.All(u => u.IsResolved) &&
              axes.Select(u => u.AxisNumber).Distinct().Count() == 9 && axes.Any(u => u.AxisNumber == 1 && u.Unit.CommandAAddress == "M604" &&
              u.Unit.PositionAddress == "W0" && u.Unit.PositionDivisor == 10000), axes.Count.ToString());
        var eu = manual.SelectMany(p => Tree(p).OfType<CylinderEncoderUnitHMI>()).ToList();
        Check("3 encoder units (Profile Recognition's front and top clamping, Cutting's right top clamping), each from its row",
              eu.Count == 3 && eu.All(u => u.IsResolved) && eu.Any(u => u.EncoderNumber == 1 && u.Unit.CommandAAddress == "M646" && u.Unit.PositionAddress == "D4"),
              string.Join(",", eu.Select(u => u.EncoderNumber + ":" + u.IsResolved)));
        var cu = manual.SelectMany(p => Tree(p).OfType<CylinderUnitHMI>()).Where(u => u.Kind == UnitKind.Cylinder && !(u.Parent is AxisUnitHMI) &&
                                                                                  !(u.Parent is CylinderEncoderUnitHMI)).ToList();
        Func<string, CylinderUnitHMI> unit = k => cu.FirstOrDefault(u => u.TitleTextKey == "Unit_SB3015_" + k);
        var bad = cu.Where(u => u.TypeCheck != "OK").Select(u => u.TitleTextKey + ": " + u.TypeCheck).ToList();
        Check(cu.Count + " cylinder units, each as its valve and feedback ask", cu.Count >= 25 && bad.Count == 0, string.Join(" | ", bad.Take(3)));
        var rollers = unit("EntryRollers");
        Check("Entry Conveyor Rollers: M745 up (M205) / M744 down (M204), double solenoid, two end sensors, M900 / M901",
              rollers != null && rollers.CommandAAddress == "M745" && rollers.CommandBAddress == "M744" && rollers.Lamp1AAddress == "M205" &&
              rollers.ValveType == CylinderValve.DoubleSolenoid && rollers.FeedbackType == CylinderFeedback.TwoEndMagnetic &&
              rollers.ErrorAAddress == "M900" && rollers.ErrorBAddress == "M901");
        var press = unit("PressRollerUp");
        Check("Profile Press Roller Up: one coil M739 (up), its down sensor M241 - a single solenoid, one sensor; M899 / M898",
              press != null && string.IsNullOrEmpty(press.CommandAAddress) && press.CommandBAddress == "M739" && press.ValveType == CylinderValve.SingleSolenoid &&
              press.FeedbackType == CylinderFeedback.SingleMagnetic && press.ErrorAAddress == "M899" && press.ErrorBAddress == "M898");
        var lfv = unit("CuttingLeftFrontVertical");
        Check("Cutting's Left Front Vertical: its positioning's movement and position (D113 / 100), down / up",
              lfv != null && lfv.CommandAAddress == "M547" && lfv.PositionAddress == "D113" && lfv.PositionDivisor == 100 &&
              lfv.FeedbackType == CylinderFeedback.AnalogueMagnetic && lfv.Arrows == ButtonArrowStyle.DownUp);
        var inv = manual.SelectMany(p => Tree(p).OfType<InverterUnitHMI>()).ToList();
        var saw = inv.FirstOrDefault(i => i.TitleTextKey == "Unit_SB3015_SawMotor");
        Check("5 inverter units: the saw M636 on / off, its fault code D669, its alarm M830; the milling motors' speeds D18 / D19 (raw / 10)",
              inv.Count == 5 && saw != null && saw.CommandAAddress == "M636" && saw.CommandAAction == BitActionType.Alternate && saw.FaultCodeAddress == "D669" &&
              saw.ErrorAddress == "M830" && inv.Any(i => i.PositionAddress == "D18" && i.PositionDivisor == 10),
              inv.Count + " " + (saw == null ? "-" : saw.CommandAAddress + ":" + saw.CommandAAction + " " + saw.FaultCodeAddress + " " + saw.ErrorAddress) + " " +
              string.Join(",", inv.Select(i => i.PositionAddress + "/" + i.PositionDivisor)));
        var conv = inv.FirstOrDefault(i => i.TitleTextKey == "Unit_SB3015_EntryConveyorMotor");
        Check("  the entry conveyor: back M640, forward M639 lit by M206, D666 / M827", conv != null && conv.CommandAAddress == "M640" &&
              conv.CommandBAddress == "M639" && conv.Lamp1BAddress == "M206" && conv.FaultCodeAddress == "D666" && conv.ErrorAddress == "M827");
        var mill = manual[3];
        Check("Milling: Change Degree 0° / 90° / 180° M673 / M674 / M675, High Air Pressure M756 on / off",
              Btn(mill, "M673") != null && Btn(mill, "M675") != null && Bits(Btn(mill, "M756")) == "M756:Alternate");

        // the alarms: every unit's bit on the list, and the help of one offering each unit it belongs to
        var alarms = File.Exists("Alarms.json") ? File.ReadAllText("Alarms.json") : File.ReadAllText(Path.Combine("SB3015", "Alarms.json"));
        var bits = cu.SelectMany(u => new[] { u.ErrorAAddress, u.ErrorBAddress }).Concat(inv.Select(i => i.ErrorAddress))
                     .Concat(er.SelectMany(r => new[] { r.ErrorBackAddress, r.ErrorForwardAddress })).Concat(rows.Select(r => r.ErrorAddress))
                     .Where(a => !string.IsNullOrEmpty(a)).ToList();
        var notListed = bits.Where(a => !alarms.Contains("\"" + a + "\"")).ToList();
        Check("every unit's and axis's error bit on the alarm list (" + bits.Distinct().Count() + ", the list 90)", notListed.Count == 0 &&
              System.Text.RegularExpressions.Regex.Matches(alarms, "PlcAddress").Count == 90, string.Join(",", notListed));
        foreach (var p in manual) Kaban.UI.Helpers.AlarmHelpLinks.Harvest(p);
        // (M850: the left front clamp, one cylinder on Milling and on Cutting - one help page; M904: the front clamp's selection,
        // M754 - the Back of Cutting's unit, the Forward of Milling's: one movement, one page)
        var docs = Kaban.UI.Helpers.AlarmHelpLinks.DocumentsFor(1, "M850", null).Where(d => d.StepByStep).ToList();
        var docs2 = Kaban.UI.Helpers.AlarmHelpLinks.DocumentsFor(1, "M904", null).Where(d => d.StepByStep).ToList();
        Check("the alarm M850 (the left front clamp did not release): one help page, the clamp's; M904 (the selection's M754): one",
              docs.Count == 1 && docs2.Count == 1, string.Join(" | ", docs.Concat(docs2).Select(d => d.Title)));

        Console.WriteLine("--- Parameters ---");
        var robot = new ParamRobotAxisCalibration();
        var rn = Tree(robot).OfType<NumericInputHMI>().ToList();
        Check("Robot Axis Calibration: 199 points - position R31200 ... R31596, error R31600 ... R31996 (32-bit / 100, 4 decimals) - and the target D38",
              rn.Count(n => n.Address.StartsWith("R")) == 398 && rn.Any(n => n.Address == "R31596") && rn.Any(n => n.Address == "R31996" &&
              n.DataType == DataTypeEnum.Bin_32 && n.Divisor == 100 && n.DecimalPlaces == 4) && rn.Any(n => n.Address == "D38"));
        Check("  Calc! M23, Go to Position M25 / M26 SET; the options L7 L8 L9", Bits(Btn(robot, "M23")) == "M23:SET" && Bits(Btn(robot, "M26")) == "M26:SET" &&
              string.Join(",", Tree(robot).OfType<CheckBoxHMI>().Select(c => c.Address)) == "L7,L8,L9");
        var other = new ParamOther();
        Check("Other: the bolt heights D848 ... D859, the options L22 L41 L44 L28 L30",
              Enumerable.Range(848, 12).All(i => Tree(other).OfType<NumericInputHMI>().Any(n => n.Address == "D" + i)) &&
              string.Join(",", Tree(other).OfType<CheckBoxHMI>().Select(c => c.Address)) == "L22,L41,L44,L28,L30");
        var speeds = new ParamAxisSpeeds();
        Check("Axis Speeds: Robot X D418 / D420 (32-bit / 100000 m/min), the angles D426 / D430 (/ 1000 °/min)",
              Tree(speeds).OfType<NumericInputHMI>().Any(n => n.Address == "D420" && n.Divisor == 100000) &&
              Tree(speeds).OfType<NumericInputHMI>().Any(n => n.Address == "D430" && n.Divisor == 1000));
        var bar = new Kaban.Machines.SB3015.Screens.Shared.JogBar();
        Check("the manual pages' bar: the speed D409, 10 / 1 / 0.1 / 0.01 mm SET M170 / M173 / M172 / M171, JOG RESETs them (lit by M174)",
              Tree(bar).OfType<SpeedOverrideHMI>().Any(s => s.Address == "D409") &&
              Bits(Tree(bar).OfType<ButtonHMI>().FirstOrDefault(b => b.FallbackText == "1 mm")) == "M173:SET" &&
              Tree(bar).OfType<ButtonHMI>().Any(b => b.Lamp1Address == "M174" && Bits(b) == "M170:RESET,M171:RESET,M172:RESET,M173:RESET"));
        var bypass = Btn(bar, "M175");
        Check("  Reset M102 (lit by M800), Security ByPass M175 on / off - an Admin's", Bits(Btn(bar, "M102")) == "M102:Momentary" &&
              bypass != null && Bits(bypass) == "M175:Alternate" && bypass.RequiredSecurity == SecurityLevel.Admin);

        var home = new HomeOverview();
        var pages = new Control[] { home, ax, enc, pos, speeds, new ParamEntry(), new ParamProcess(), other, robot }.Concat(manual).ToList();
        var overlaps = new List<string>();
        foreach (var pg in pages) {
            var cs = pg.Controls.Cast<Control>().ToList();
            for (int i = 0; i < cs.Count; i++)
                for (int j = i + 1; j < cs.Count; j++)
                    if (cs[i].Bounds.IntersectsWith(cs[j].Bounds)) overlaps.Add(pg.Name + ": " + cs[i].Name + " / " + cs[j].Name);
        }
        Check("no two controls of a page on each other; every page fits the window (1180)", overlaps.Count == 0 && pages.All(p => p.Width <= 1180),
              string.Join("; ", overlaps.Take(4)) + " " + string.Join(" ", pages.Where(p => p.Width > 1180).Select(p => p.Name + " " + p.Width)));

        Console.WriteLine("--- in Turkish ---");
        LanguageManager.LoadMachineTranslations(Path.Combine("SB3015", "Translations.csv"));
        var keys = new List<string>();
        foreach (var pg in pages.Concat(new Control[] { bar }))
            foreach (var c in Tree(pg).Where(k => !(k.Parent is AxisUnitHMI) && !(k.Parent is CylinderEncoderUnitHMI)))
                foreach (var pn in new[] { "TranslationKey", "AxisNameKey", "TitleTextKey" }) {
                    var pi = c.GetType().GetProperty(pn);
                    var v = pi == null ? null : pi.GetValue(c, null) as string;
                    if (!string.IsNullOrWhiteSpace(v) && !System.Text.RegularExpressions.Regex.IsMatch(v, "^[XYZ][12]?$|^[0-9]+$|°$")) keys.Add(v);
                }
        keys.AddRange(new[] { "Machine_SB3015", "Tab_SB3015_ManualEntryConveyor", "Tab_SB3015_ManualRobot", "Tab_SB3015_ManualProfileRecognition",
                              "Tab_SB3015_ManualMilling", "Tab_SB3015_ManualCutting", "Tab_SB3015_ManualExit", "Tab_SB3015_ManualOther",
                              "Tab_SB3015_ParamAxisSettings", "Tab_SB3015_ParamAxisSpeeds", "Tab_SB3015_ParamEncoders", "Tab_SB3015_ParamPositioning",
                              "Tab_SB3015_ParamEntry", "Tab_SB3015_ParamProcess", "Tab_SB3015_ParamOther", "Tab_SB3015_ParamRobotAxisCalibration" });
        var missing = keys.Distinct().Where(k => LanguageManager.GetStringIn("TR", k) == null).ToList();
        Check("every caption of the pages, the bar, their tabs and the machine's name has its Turkish (" + keys.Distinct().Count() + ")",
              missing.Count == 0, string.Join(", ", missing));

        if (args.Length > 0) {
            Shot(new ManualCutting(), Path.Combine(args[0], "sb_cutting.png"));
            Shot(new ManualMilling(), Path.Combine(args[0], "sb_milling.png"));
            Shot(new ParamAxisSettings(), Path.Combine(args[0], "sb_axes.png"));
            Shot(new ParamEncoders(), Path.Combine(args[0], "sb_encoders.png"));
            Shot(new ManualProfileRecognition(), Path.Combine(args[0], "sb_profile.png"));
        }
        Console.WriteLine(fail == 0 ? "\nSB3015 PAGES ALL PASS" : "\nSB3015 PAGES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
