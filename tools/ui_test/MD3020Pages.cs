using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.Machines.MD3020.Screens.Manual;
using Kaban.Machines.MD3020.Screens.Parameters;
using Kaban.Machines.MD3020.Screens.Home;

// 11 November: "Can you fulfil "Kaban.Machines.MD3020" project using our current operating program "_MD3030" project
// as well" - the old program's MD3030 pages (tools/legacy_md3030_pages.py, its --check against tools/legacy/MD3030):
// Manual - General, Manual Movement, Servos; Parameters - Axis Calibration, General, Position Definition, Speed,
// Process, Offset; the manual pages' bar. The old controls' scale, read in KabanDevBase: a KabanLedLabel shows
// raw / (Divisor * 10), a KabanNumericButton raw / Divisor, a KabanProgressBar |raw| on 0 - 100.
class MDP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }
    static T Named<T>(Control page, string name) where T : Control { return Tree(page).OfType<T>().FirstOrDefault(c => c.Name == name); }
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
        AuditLogger.LogDirectory = Path.Combine(Path.GetTempPath(), "mdp_" + Guid.NewGuid().ToString("N"));
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();

        Console.WriteLine("--- Home (the old Job Preparations: ucMainControlPanel, ucJobDimentions, ucActiveJobScreen) ---");
        // 11 November: "can you update this main page as well" - the sample's Cycle / Job / Axis positions page replaced
        var home = new HomeJob();
        var start = Tree(home).OfType<ButtonHMI>().FirstOrDefault(b => b.FallbackText == "Start");
        var pause = Tree(home).OfType<ButtonHMI>().FirstOrDefault(b => b.FallbackText == "Pause");
        var cancel = Tree(home).OfType<ButtonHMI>().FirstOrDefault(b => b.FallbackText == "Cancel");
        Check("Start sets M83 (the machine works), lit by M316; Pause M111 (alternate, its lamp) and M84; Cancel sets M85 after a question",
              start != null && Bits(start) == "M83:SET" && start.Lamp1Address == "M316" && pause != null && Bits(pause) == "M111:Alternate,M84:SET" &&
              pause.Lamp1Address == "M111" && cancel != null && Bits(cancel) == "M85:SET" && cancel.ConfirmationNeeded,
              (start == null ? "-" : Bits(start)) + " " + (pause == null ? "-" : Bits(pause)));
        Check("  the speed D408 on a slider", Tree(home).OfType<SpeedOverrideHMI>().Single().Address == "D408");
        var fx = Tree(home).OfType<NumericInputHMI>().FirstOrDefault(n => n.Address == "D1200");
        var fy = Tree(home).OfType<NumericInputHMI>().FirstOrDefault(n => n.Address == "D1202");
        Check("the frame's width D1200 (at most 4100) and depth D1202 (at most 2300), 32-bit / 10 - an operator's",
              fx != null && fx.Divisor == 10 && fx.MaxLimit.StaticValue == 4100 && fy != null && fy.MaxLimit.StaticValue == 2300 &&
              fx.RequiredSecurity == SecurityLevel.Operator);
        var hd = Tree(home).OfType<NumericDisplayHMI>().ToList();
        Check("  the heads' thermocouples D4 ... D11, the axes D320 / D340 / D360, the profile's temperature D349, width D346, melting D2408, welding D2414, online time D1854",
              new[] { "D4", "D7", "D8", "D11", "D320", "D340", "D360", "D349", "D346", "D2408", "D2414", "D1854" }.All(a => hd.Any(d => d.Address == a)));

        // 12 November: "We gotta update these controls to axis setting controls first and change their manual movements to axis unit
        // controls in the manual movement pages ... change "ButtonHMI" controls (and the related LampHMI controls as the feedback and
        // LabelHMI controls as the description text ...) to cylinder unit HMI controls" - "enter the axis unit control motion numbers
        // according to the image and cylinder unit control's valve and feedback types according to the drawings"
        Console.WriteLine("--- Axis Calibration: an axis settings row per servo ---");
        var cal = new ParamAxisCalibration();
        var rows = Tree(cal).OfType<AxisSettingHMI>().ToList();
        Func<int, AxisSettingHMI> row = n => rows.FirstOrDefault(r => r.HMIAxisIndex == n);
        Check("24 rows, numbered 1 - 24: the body's X1 X2 Y1 Y2, then each head's X Y1 Y2 Z RZ", rows.Count == 24 &&
              rows.Select(r => r.HMIAxisIndex).OrderBy(n => n).SequenceEqual(Enumerable.Range(1, 24)) &&
              row(1).PositionAddress == "D400" && row(2).PositionAddress == "D320" && row(5).PositionAddress == "D20" && row(9).PositionAddress == "D420",
              string.Join(",", rows.Select(r => r.HMIAxisIndex + ":" + r.PositionAddress)));
        // the image: module 1 Y11 Y12 Y21 Y22 Y31 Y32 Y41 Y42; module 2 X1 Z1 X2 Z2 GX1 GX2 RZ1 RZ2; module 3 RZ3 GY1 GY2 RZ4 X3 Z3 X4 Z4
        var motion = new Dictionary<int, string> { { 1, "2.5" }, { 2, "2.6" }, { 3, "3.2" }, { 4, "3.3" } };
        int[][] headMotion = { new[] { 2, 1, 1, 1, 1, 2, 2, 2, 2, 7 }, new[] { 2, 3, 1, 3, 1, 4, 2, 4, 2, 8 },
                               new[] { 3, 5, 1, 5, 1, 6, 3, 6, 3, 1 }, new[] { 3, 7, 1, 7, 1, 8, 3, 8, 3, 4 } };
        for (int h = 0; h < 4; h++)
            for (int a = 0; a < 5; a++) motion[5 + 5 * h + a] = headMotion[h][2 * a] + "." + headMotion[h][2 * a + 1];
        var wrong = rows.Where(r => r.MotionNumber + "." + r.MotionAxisIndex != motion[r.HMIAxisIndex]).Select(r => r.AxisNameKey + " " + r.MotionNumber + "." + r.MotionAxisIndex).ToList();
        Check("  each on its motion module and axis as the image shows them (X1 2.1, Y11 1.1, Y42 1.8, GX1 2.5, RZ3 3.1, Z4 3.8 ...)", wrong.Count == 0 &&
              row(6).MotionNumber == 1 && row(6).MotionAxisIndex == 1 && row(22).MotionNumber == 1 && row(22).MotionAxisIndex == 8, string.Join(", ", wrong));
        var rz3 = row(19);
        Check("Head 3 RZ (the old Z2): jog M238 / M239, D492, limits D536 / D534, measured value D7048 with Calibrate M81, sensor reference distance D6621",
              rz3.AxisNameKey == "Axis_MD3020_H3RZ" && rz3.CommandBackAddress == "M238" && rz3.CommandForwardAddress == "M239" && rz3.PositionAddress == "D492" &&
              rz3.MinLimitAddress == "D536" && rz3.MaxLimitAddress == "D534" && rz3.CalibrationAddress == "D7048" && rz3.CalibrateAddress == "M81" &&
              rz3.ZDistanceAddress == "D6621" && rz3.Divisor == 10000 && rz3.DecimalPlaces == 2 && rz3.PositionDataType == DataTypeEnum.Bin_32);
        Check("  its reference sensor W4.4 on Forward (the head's island input), its servo alarm M759", rz3.ReferenceSensorAddress == "W4.4" &&
              rz3.ReferenceSensorSide == AxisReferenceSide.Forward && rz3.ErrorAddress == "M759");
        Check("  the servo alarms: Head 1 Z / X / Y1 / Y2 M605 - M608 ... Head 4 Y2 M620, the body X1 M621 Y1 M622 Y2 M623 X2 M752",
              row(8).ErrorAddress == "M605" && row(5).ErrorAddress == "M606" && row(6).ErrorAddress == "M607" && row(7).ErrorAddress == "M608" &&
              row(22).ErrorAddress == "M620" && row(1).ErrorAddress == "M621" && row(3).ErrorAddress == "M622" && row(4).ErrorAddress == "M623" &&
              row(2).ErrorAddress == "M752" && row(5).ReferenceSensorAddress == "W0.0" && row(5).ReferenceSensorSide == AxisReferenceSide.Back);
        var nums = Tree(cal).OfType<NumericInputHMI>().ToList();
        Check("  the old page's Encoder column kept (Head 3 RZ D2042), the body's Encoder Channel D2816 whole and Offset D2808",
              nums.Any(n => n.Address == "D2042" && n.Divisor == 10000 && n.DecimalPlaces == 2 && n.DataType == DataTypeEnum.Bin_32) &&
              nums.Any(n => n.Address == "D2816" && n.DecimalPlaces == 0 && n.DataType == DataTypeEnum.Bin_16) && nums.Any(n => n.Address == "D2808"));
        Check("  Send Reference (M100) once; the heads' X2 rows as the old page's (Head 3: M248 / M249, D2830, D7100, Calibrate M93)",
              Tree(cal).OfType<ButtonHMI>().Count(b => Bits(b) == "M100:Momentary") == 1 && Btn(cal, "M248") != null && Btn(cal, "M93") != null &&
              nums.Any(n => n.Address == "D2830") && nums.Any(n => n.Address == "D7100"));

        Console.WriteLine("--- Manual Movement: an axis unit per servo ---");
        Kaban.UI.Helpers.AlarmHelpLinks.DiscoverAll();
        Kaban.UI.Helpers.AlarmHelpLinks.Harvest(cal);              // the program finds the rows on Axis Calibration
        var mm = new ManualMovement();
        var units = Tree(mm).OfType<AxisUnitHMI>().ToList();
        Check("24 axis units, each an axis of Axis Calibration (its buttons, position, reference sensor and servo alarm come from its row)",
              units.Count == 24 && units.Select(u => u.AxisNumber).OrderBy(n => n).SequenceEqual(Enumerable.Range(1, 24)) && units.All(u => u.IsResolved),
              units.Count + " " + string.Join(",", units.Where(u => !u.IsResolved).Select(u => u.AxisNumber)));
        var hx = units.First(u => u.AxisNumber == 5).Unit;
        Check("  Head 1 X from its row: Back M45 / Forward M26, position D20 (/ 10000), its reference W0.0 lit on Back, its servo alarm M606",
              hx.TitleTextKey == "Axis_MD3020_H1X" && hx.CommandAAddress == "M45" && hx.CommandBAddress == "M26" && hx.PositionAddress == "D20" &&
              hx.PositionDivisor == 10000 && hx.Lamp1AAddress == "W0.0" && hx.ErrorBAddress == "M606", hx.TitleTextKey + " " + hx.CommandAAddress);
        var meters = Tree(mm).OfType<MeterHMI>().ToList();
        Check("  the heads' torques beside them (Head 1 X D6641 ... Head 4 RZ D7191), 20 - the stripping knife's not", meters.Count == 20 &&
              meters.Any(m => m.Address == "D6641") && meters.Any(m => m.Address == "D7191") && !meters.Any(m => m.Address == "D6656"), meters.Count.ToString());
        Check("  no loose jog buttons or lamps left", mm.Controls.OfType<LampHMI>().Count() == 0 &&
              !mm.Controls.OfType<ButtonHMI>().Any(b => b.BitActions.Any(a => a.Address == "M45" || a.Address == "M201")));
        var milling = Btn(mm, "M200");
        Check("the units that are no cylinders: Milling Cutters M200, Conveyor Up/Down M207 / M206, Conveyor Run M208, the thermocouples D5 / D9",
              milling != null && milling.FallbackText == "Milling Cutters" && Btn(mm, "M207") != null && Btn(mm, "M206") != null &&
              Btn(mm, "M208").FallbackText == "Conveyor Run" && Tree(mm).OfType<NumericDisplayHMI>().Any(d => d.Address == "D5" && d.Divisor == 10 && d.Suffix == "°C"));

        Console.WriteLine("--- Cylinders: a cylinder unit each, head by head ---");
        var cyl = new ManualCylinders();
        var cu = Tree(cyl).OfType<CylinderUnitHMI>().Where(u => u.Kind == UnitKind.Cylinder).ToList();
        Func<string, CylinderUnitHMI> unit = k => cu.FirstOrDefault(u => u.TitleTextKey == k);
        Check("44 units: per head the top presses, front clamps, small vertical and horizontal presses (left, right), support plate, stripping knife, profile emptying",
              cu.Count == 44 && new[] { 1, 2, 3, 4 }.All(h => cu.Count(u => u.TitleTextKey.StartsWith("Unit_MD3020_H" + h)) == 11), cu.Count.ToString());
        var tp = unit("Unit_MD3020_H1TopPressL");
        Check("Head 1 Left Top Press: Island3WayOpenCenter (Y1.1 / Y2.1), its encoder SD4500 (/ 100) and up sensor W0.6 - EncoderPlusReference",
              tp != null && tp.ValveType == CylinderValve.IslandThreeWayOpenCenter && tp.FeedbackType == CylinderFeedback.EncoderPlusReference &&
              tp.CommandAAddress == "M201" && tp.CommandBAddress == "M202" && tp.Lamp1BAddress == "W0.6" && tp.PositionAddress == "SD4500" &&
              tp.PositionDivisor == 100 && tp.Arrows == ButtonArrowStyle.DownUp && tp.ErrorAAddress == "M660" && tp.ErrorBAddress == "M668" && tp.TypeCheck == "OK",
              tp == null ? "-" : tp.TypeCheck);
        var fc = unit("Unit_MD3020_H2FrontClampR");
        Check("Head 2 Right Front Clamp: IslandDoubleSolenoid (Y5.2 / Y6.2), back sensor W3.1 - SingleMagnetic; open failed M728, clamp failed M720",
              fc != null && fc.ValveType == CylinderValve.IslandDoubleSolenoid && fc.FeedbackType == CylinderFeedback.SingleMagnetic &&
              fc.CommandAAddress == "M230" && fc.CommandBAddress == "M231" && fc.Lamp1AAddress == "W3.1" && fc.ErrorAAddress == "M728" && fc.ErrorBAddress == "M720");
        var sv = unit("Unit_MD3020_H3SmallVerticalL");
        Check("Head 3 Left Small Vertical (the old Ear Clamping): down M218 / up M217, down sensor W5.2 - SingleInductive; M745 / M737",
              sv != null && sv.FeedbackType == CylinderFeedback.SingleInductive && sv.CommandAAddress == "M218" && sv.Lamp1AAddress == "W5.2" &&
              sv.ErrorAAddress == "M745" && sv.ErrorBAddress == "M737");
        var sk = unit("Unit_MD3020_H4StrippingKnife");
        Check("Head 4 Stripping Knife (the old X2): its own M250 / M251, back W7.6 and forward W7.7 - TwoEndMagnetic on IslandSingleSolenoid; M635 / M631",
              sk != null && sk.ValveType == CylinderValve.IslandSingleSolenoid && sk.FeedbackType == CylinderFeedback.TwoEndMagnetic &&
              sk.CommandAAddress == "M250" && sk.CommandBAddress == "M251" && sk.Lamp1AAddress == "W7.6" && sk.Lamp1BAddress == "W7.7" &&
              sk.ErrorAAddress == "M635" && sk.ErrorBAddress == "M631");
        var pe = unit("Unit_MD3020_H1ProfileEmptying");
        Check("Head 1 Profile Emptying: no sensor on the drawings - NoSensor, no lamp, no error bit", pe != null && pe.FeedbackType == CylinderFeedback.NoSensor &&
              string.IsNullOrEmpty(pe.Lamp1AAddress) && string.IsNullOrEmpty(pe.ErrorAAddress) && pe.TypeCheck.IndexOf("NoSensor", StringComparison.Ordinal) < 0);
        var odd = cu.Where(u => CylinderValves.Base(u.ValveType) != CylinderValve.SingleSolenoid && u.TypeCheck != "OK").Select(u => u.TitleTextKey + ": " + u.TypeCheck).ToList();
        Check("  every unit on a valve island; the two-coil ones' actions, lamps and positions as their valve and feedback ask", odd.Count == 0 &&
              cu.All(u => CylinderValves.OnIsland(u.ValveType)), string.Join(" | ", odd.Take(3)));
        var alarmsJson = File.Exists("Alarms.json") ? File.ReadAllText("Alarms.json") : File.ReadAllText(Path.Combine("MD3020", "Alarms.json"));
        var noAlarm = cu.SelectMany(u => new[] { u.ErrorAAddress, u.ErrorBAddress }).Concat(rows.Select(r => r.ErrorAddress))
                        .Where(a => !string.IsNullOrEmpty(a) && !alarmsJson.Contains("\"" + a + "\"")).ToList();
        Check("  every error bit on the machine's alarm list (108 alarms)", noAlarm.Count == 0 && System.Text.RegularExpressions.Regex.Matches(alarmsJson, "PlcAddress").Count == 108,
              string.Join(",", noAlarm.Take(5)));
        Check("  an operator's, their movements logged", cu.All(u => u.RequiredSecurity == SecurityLevel.Operator && u.LogAction));

        Console.WriteLine("--- General (ucGeneral), Servos (the Servo Situations window) ---");
        var gen = new ManualGeneral();
        var ga = Tree(gen).OfType<AxisUnitHMI>().Select(u => u.AxisNumber).ToList();
        var brakes = Tree(gen).OfType<CylinderUnitHMI>().Where(u => u.Kind == UnitKind.Cylinder).ToList();
        Check("General: the body's X1 X2 Y1 Y2 as axis units, their overloads D6634 / D6636 / D6630 / D6632",
              ga.SequenceEqual(new[] { 1, 2, 3, 4 }) && new[] { "D6634", "D6636", "D6630", "D6632" }.All(a => Tree(gen).OfType<MeterHMI>().Any(m => m.Address == a)));
        Check("  the brakes M260 (X, Y14.4) / M261 (Y1) / M262 (Y2): IslandSingleSolenoid, no sensor, one button",
              brakes.Count == 3 && brakes.All(b => b.ValveType == CylinderValve.IslandSingleSolenoid && b.FeedbackType == CylinderFeedback.NoSensor &&
              string.IsNullOrEmpty(b.CommandBAddress) && b.TypeCheck == "OK") && brakes.Select(b => b.CommandAAddress).SequenceEqual(new[] { "M260", "M261", "M262" }),
              string.Join(" | ", brakes.Select(b => b.CommandAAddress + " " + b.TypeCheck)));
        Check("  Conveyor Up/Down M207 / M206, Conveyor Run M208", Btn(gen, "M207") != null && Btn(gen, "M206") != null && Btn(gen, "M208") != null);
        var servos = new ManualServos();
        Check("Servos: 28 servos' position, torque and encoder (Head 1 - 4 and the body)",
              Tree(servos).OfType<MeterHMI>().Count() == 28 && Tree(servos).OfType<NumericDisplayHMI>().Count() == 56 &&
              Tree(servos).OfType<NumericDisplayHMI>().Any(d => d.Address == "D48") && Tree(servos).OfType<MeterHMI>().Any(m => m.Address == "D7188"));

        Console.WriteLine("--- Parameters ---");
        var off = new ParamOffset();
        var offNums = Tree(off).OfType<NumericInputHMI>().ToList();
        Check("Offset: 8 x 6 positions and 4 x 6 of the heads (D2052 ... D2670), the temperature and encoder offsets",
              offNums.Count == 8 * 6 + 4 * 6 + 5 + 8, offNums.Count.ToString());
        Check("  the encoders' actual SD4500 ... SD4710 (raw / 100), the heads' temperatures D4 - D7",
              Tree(off).OfType<NumericDisplayHMI>().Count(d => d.Address.StartsWith("SD") && d.Divisor == 100) == 8 &&
              Tree(off).OfType<NumericDisplayHMI>().Count(d => new[] { "D4", "D5", "D6", "D7" }.Contains(d.Address)) == 4);
        Check("  Upper Press M201 down / M202 up, lit by themselves", Btn(off, "M201").Lamp1Address == "M201" && Btn(off, "M202").Lamp1Address == "M202" &&
              Btn(off, "M201").Direction == ButtonGlyphs.Direction.Down && Btn(off, "M202").Direction == ButtonGlyphs.Direction.Up);
        var proc = new ParamProcess();
        var opts = Tree(proc).OfType<CheckBoxHMI>().ToList();
        Check("Process Parameters: the options L3 L0 L5 L6 L13", string.Join(",", opts.Select(o => o.Address)) == "L3,L0,L5,L6,L13",
              string.Join(",", opts.Select(o => o.Address)));
        var speed = new ParamSpeed();
        var spd = Tree(speed).OfType<NumericInputHMI>().ToList();
        Check("Speed Parameters: 16 speeds (the heads, body X, body Y), D2418 / 100", spd.Count == 16 && spd.Any(n => n.Address == "D2418" && n.Divisor == 100));
        var gp = new ParamGeneral();
        Check("General: the frame sizes D514 ... D520 (/ 10), the maintenance values, Conveyor Stop M500, Milling Thickness D502 and its Send M105",
              Tree(gp).OfType<NumericInputHMI>().Count() == 16 && Btn(gp, "M500") != null && Btn(gp, "M105") != null,
              Tree(gp).OfType<NumericInputHMI>().Count().ToString());
        var pos = new ParamPositionDefinition();
        Check("Position Definition: the starting position D1940 / D1944 / D1942", Tree(pos).OfType<NumericInputHMI>().Select(n => n.Address).SequenceEqual(new[] { "D1940", "D1944", "D1942" }));
        var prm = new Control[] { cal, gp, pos, speed, proc, off };
        Check("  a Maintenance's to change", prm.SelectMany(Tree).Where(c => c is NumericInputHMI || c is CheckBoxHMI || c is ButtonHMI)
              .All(c => (SecurityLevel)c.GetType().GetProperty("RequiredSecurity").GetValue(c, null) == SecurityLevel.Maintenance));

        Check("  the axis rows a Maintenance's to change", rows.All(r => r.RequiredSecurity == SecurityLevel.Maintenance));
        var all = new Control[] { home, mm, cyl, gen, servos, cal, gp, pos, speed, proc, off };
        Check("every control on the machine's PLC 1", all.SelectMany(Tree).Where(c => c.GetType().GetProperty("PlcNumber") != null)
              .All(c => (int)c.GetType().GetProperty("PlcNumber").GetValue(c, null) == 1));
        var overlaps = new List<string>();
        foreach (var pg in all) {
            var cs = pg.Controls.Cast<Control>().ToList();
            for (int i = 0; i < cs.Count; i++)
                for (int j = i + 1; j < cs.Count; j++)
                    if (cs[i].Bounds.IntersectsWith(cs[j].Bounds)) overlaps.Add(pg.Name + ": " + cs[i].Name + " / " + cs[j].Name);
        }
        Check("no two controls of a page on each other", overlaps.Count == 0, string.Join("; ", overlaps.Take(5)));
        Check("the widest page fits a 1280 screen beside the bar (Offset, " + off.Width + " px)", all.All(p => p.Width <= 1180),
              string.Join(" ", all.Select(p => p.Name + " " + p.Width)));

        Console.WriteLine("--- the manual pages' bar ---");
        var bar = new Kaban.Machines.MD3020.Screens.Shared.JogBar();
        Check("JOG M90, 1 mm M2, 0.1 mm M1, 0.01 mm M0 - each SETs its own bit, lit by it; the speed D409",
              new[] { "M90", "M2", "M1", "M0" }.All(a => Btn(bar, a) != null && Bits(Btn(bar, a)) == a + ":SET" && Btn(bar, a).Lamp1Address == a) &&
              Tree(bar).OfType<SpeedOverrideHMI>().Single().Address == "D409");

        Console.WriteLine("--- in Turkish ---");
        LanguageManager.LoadMachineTranslations(Path.Combine("MD3020", "Translations.csv"));
        var keys = new List<string>();
        foreach (var pg in all.Concat(new Control[] { bar }))
            foreach (var c in Tree(pg).Where(k => !(k.Parent is AxisUnitHMI)))          // an axis unit's name is its row's
                foreach (var pn in new[] { "TranslationKey", "TitleTextKey", "AxisNameKey" }) {
                    var pi = c.GetType().GetProperty(pn);
                    var v = pi == null ? null : pi.GetValue(c, null) as string;
                    if (!string.IsNullOrWhiteSpace(v) && !System.Text.RegularExpressions.Regex.IsMatch(v, "^[XYZ][12]?$")) keys.Add(v);
                }
        keys.AddRange(new[] { "Tab_MD3020_ManualGeneral", "Tab_MD3020_ManualMovement", "Tab_MD3020_ManualCylinders", "Tab_MD3020_ManualServos", "Tab_MD3020_ParamAxisCalibration",
                              "Tab_MD3020_ParamGeneral", "Tab_MD3020_ParamPositionDefinition", "Tab_MD3020_ParamSpeed", "Tab_MD3020_ParamProcess",
                              "Tab_MD3020_ParamOffset" });
        var missing = keys.Distinct().Where(k => LanguageManager.GetStringIn("TR", k) == null).ToList();
        Check("every caption of the pages and their tabs has its Turkish (" + keys.Distinct().Count() + ")", missing.Count == 0, string.Join(", ", missing));
        LanguageManager.ChangeLanguage("TR");
        Check("  shown: Kafa 1, Gerçek Tork, Kalibre Et, Eritme Yaklaşma Hızı", LanguageManager.GetString("Head 1", "") == "Kafa 1" &&
              LanguageManager.GetString("Actual Torque", "") == "Gerçek Tork" && LanguageManager.GetString(Btn(cal, "M93").TranslationKey, "") == "Kalibre Et" &&
              LanguageManager.GetString("Melting Approach Speed", "") == "Eritme Yaklaşma Hızı");
        LanguageManager.ChangeLanguage("EN");

        if (args.Length > 0) {
            Shot(new HomeJob(), Path.Combine(args[0], "md_home.png"));
            Shot(new ManualMovement(), Path.Combine(args[0], "md_manual.png"));
            Shot(new ParamAxisCalibration(), Path.Combine(args[0], "md_calib.png"));
            Shot(new ParamOffset(), Path.Combine(args[0], "md_offset.png"));
            Shot(new ManualGeneral(), Path.Combine(args[0], "md_general.png"));
            Shot(new ManualCylinders(), Path.Combine(args[0], "md_cylinders.png"));
        }
        Console.WriteLine(fail == 0 ? "\nMD3020 PAGES ALL PASS" : "\nMD3020 PAGES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
