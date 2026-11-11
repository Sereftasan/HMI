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

        Console.WriteLine("--- Manual Movement (ucManualMovement) ---");
        var mm = new ManualMovement();
        var back = Btn(mm, "M45");
        var fwd = Btn(mm, "M26");
        Check("Head 1 X: Back M45 momentary with its lamp W0.0 (a bit of W0), an arrow; Forward M26",
              back != null && Bits(back) == "M45:Momentary" && back.Lamp1Address == "W0.0" && back.Direction == ButtonGlyphs.Direction.Left &&
              fwd != null && fwd.Direction == ButtonGlyphs.Direction.Right && string.IsNullOrEmpty(fwd.Lamp1Address));
        var x = Tree(mm).OfType<NumericDisplayHMI>().FirstOrDefault(d => d.Address == "D20");
        Check("  its position D20, 32-bit / 10000, 2 decimals (the old KabanLedLabel: Divisor 1000 x 10)",
              x != null && x.DataType == DataTypeEnum.Bin_32 && x.Divisor == 10000 && x.DecimalPlaces == 2);
        var meters = Tree(mm).OfType<MeterHMI>().ToList();
        var t = meters.FirstOrDefault(m => m.Address == "D6641");
        Check("  its torque D6641 on a 0 - 100 % bar, its sign only a direction; 24 axes' torques", t != null && t.ScaleMaximum == 100 && t.Signed &&
              t.Divisor == 1 && meters.Count == 24, meters.Count.ToString());
        var z1 = Btn(mm, "M25");
        Check("Head 1 Z1: its lamp W0.3 on Forward (M25), Back M44 without one", z1 != null && z1.Lamp1Address == "W0.3" &&
              string.IsNullOrEmpty(Btn(mm, "M44").Lamp1Address));
        Check("Head 4 Z2: Back M240, Forward M241 / W6.4, position D380, torque D7191", Btn(mm, "M240") != null && Btn(mm, "M241").Lamp1Address == "W6.4" &&
              Tree(mm).OfType<NumericDisplayHMI>().Any(d => d.Address == "D380") && meters.Any(m => m.Address == "D7191"));
        var lamps = Tree(mm).OfType<LampHMI>().Select(l => l.Address).ToList();
        Check("the piston sensors: 40 lamps, Head 1 - 4 left and right (W0.6 ... W7.A)", lamps.Count == 40 &&
              new[] { "W0.6", "W0.7", "W1.A", "W3.5", "W5.0", "W7.A", "W6.5" }.All(lamps.Contains), lamps.Count.ToString());
        var milling = Btn(mm, "M200");
        Check("the units: Milling Cutters M200 (a button with its name), Resistance Lower Down/Up M233 / M234, Conveyor Run M208",
              milling != null && milling.FallbackText == "Milling Cutters" && Btn(mm, "M233").Direction == ButtonGlyphs.Direction.Left &&
              Btn(mm, "M234") != null && Btn(mm, "M208").FallbackText == "Conveyor Run");
        var th = Tree(mm).OfType<NumericDisplayHMI>().FirstOrDefault(d => d.Address == "D5");
        Check("  the thermocouples D5 / D9 in °C (raw / 10, as the old Divisor 1 x 10)", th != null && th.Divisor == 10 && th.Suffix == "°C" &&
              Tree(mm).OfType<NumericDisplayHMI>().Any(d => d.Address == "D9"));
        Check("  the jogs momentary, an operator's", Tree(mm).OfType<ButtonHMI>().All(b => b.BitActions.All(a => a.Action == BitActionType.Momentary) &&
              b.RequiredSecurity == SecurityLevel.Operator));

        Console.WriteLine("--- General (ucGeneral), Servos (the Servo Situations window) ---");
        var gen = new ManualGeneral();
        var brake = Btn(gen, "M260");
        Check("General: X1 / X2 on M41 / M22 (both, as the old page), overloads D6634 / D6636, positions D400 / D320",
              Tree(gen).OfType<ButtonHMI>().Count(b => Bits(b) == "M41:Momentary") == 2 && Tree(gen).OfType<MeterHMI>().Any(m => m.Address == "D6636") &&
              Tree(gen).OfType<NumericDisplayHMI>().Any(d => d.Address == "D400"));
        Check("  Brake Cylinder M260 / M261 / M262 across three columns", brake != null && brake.FallbackText == "Brake Cylinder" && brake.Width > 200 &&
              Btn(gen, "M261") != null && Btn(gen, "M262") != null);
        Check("  Conveyor Up/Down M207 / M206, Conveyor Run M208", Btn(gen, "M207") != null && Btn(gen, "M206") != null && Btn(gen, "M208") != null);
        var servos = new ManualServos();
        Check("Servos: 28 servos' position, torque and encoder (Head 1 - 4 and the body)",
              Tree(servos).OfType<MeterHMI>().Count() == 28 && Tree(servos).OfType<NumericDisplayHMI>().Count() == 56 &&
              Tree(servos).OfType<NumericDisplayHMI>().Any(d => d.Address == "D48") && Tree(servos).OfType<MeterHMI>().Any(m => m.Address == "D7188"));

        Console.WriteLine("--- Parameters ---");
        var cal = new ParamAxisCalibration();
        var nums = Tree(cal).OfType<NumericInputHMI>().ToList();
        Check("Axis Calibration: Head 3 Z2 - encoder D2042, measured value D7048, Calibrate M81 (32-bit / 10000, 2 decimals)",
              nums.Any(n => n.Address == "D2042" && n.Divisor == 10000 && n.DecimalPlaces == 2 && n.DataType == DataTypeEnum.Bin_32) &&
              nums.Any(n => n.Address == "D7048") && Btn(cal, "M81") != null && Btn(cal, "M81").FallbackText == "Calibrate");
        Check("  the heads' Send Reference (M100, 24 rows), Starting / End Limit D528 / D526 ...", Tree(cal).OfType<ButtonHMI>().Count(b => Bits(b) == "M100:Momentary") == 24 &&
              nums.Any(n => n.Address == "D528") && nums.Any(n => n.Address == "D526"));
        Check("  General: Encoder Channel D2816 whole, Offset D2808", nums.Any(n => n.Address == "D2816" && n.DecimalPlaces == 0 && n.DataType == DataTypeEnum.Bin_16) &&
              nums.Any(n => n.Address == "D2808"));
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

        var all = new Control[] { mm, gen, servos, cal, gp, pos, speed, proc, off };
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
            foreach (var c in Tree(pg))
                foreach (var pn in new[] { "TranslationKey", "TitleTextKey" }) {
                    var pi = c.GetType().GetProperty(pn);
                    var v = pi == null ? null : pi.GetValue(c, null) as string;
                    if (!string.IsNullOrWhiteSpace(v) && !System.Text.RegularExpressions.Regex.IsMatch(v, "^[XYZ][12]?$")) keys.Add(v);
                }
        keys.AddRange(new[] { "Tab_MD3020_ManualGeneral", "Tab_MD3020_ManualMovement", "Tab_MD3020_ManualServos", "Tab_MD3020_ParamAxisCalibration",
                              "Tab_MD3020_ParamGeneral", "Tab_MD3020_ParamPositionDefinition", "Tab_MD3020_ParamSpeed", "Tab_MD3020_ParamProcess",
                              "Tab_MD3020_ParamOffset" });
        var missing = keys.Distinct().Where(k => LanguageManager.GetStringIn("TR", k) == null).ToList();
        Check("every caption of the pages and their tabs has its Turkish (" + keys.Distinct().Count() + ")", missing.Count == 0, string.Join(", ", missing));
        LanguageManager.ChangeLanguage("TR");
        Check("  shown: Kafa 1, Gerçek Tork, Kalibre Et, Eritme Yaklaşma Hızı", LanguageManager.GetString("Head 1", "") == "Kafa 1" &&
              LanguageManager.GetString("Actual Torque", "") == "Gerçek Tork" && LanguageManager.GetString(Btn(cal, "M81").TranslationKey, "") == "Kalibre Et" &&
              LanguageManager.GetString("Melting Approach Speed", "") == "Eritme Yaklaşma Hızı");
        LanguageManager.ChangeLanguage("EN");

        if (args.Length > 0) {
            Shot(new ManualMovement(), Path.Combine(args[0], "md_manual.png"));
            Shot(new ParamAxisCalibration(), Path.Combine(args[0], "md_calib.png"));
            Shot(new ParamOffset(), Path.Combine(args[0], "md_offset.png"));
            Shot(new ManualGeneral(), Path.Combine(args[0], "md_general.png"));
        }
        Console.WriteLine(fail == 0 ? "\nMD3020 PAGES ALL PASS" : "\nMD3020 PAGES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
