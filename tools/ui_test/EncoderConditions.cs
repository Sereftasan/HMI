using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 30 October: "Entry Conveyor > Profile Clamping 7 ... X10-X11 Left profile recognition and profile recognition
// (right side is default) encoders are read from the same PLC input (selected which one to use with K77 (U14 Y0
// output)). The left front profile recognition and conveyor clamping 7 is the same cylinder, use the encoder to
// measure the profile at the beginning of the process and stop reading the encoder afterwards) / Milling > Front
// Clamping ... This cylinder used to have encoder at the old machines but doesn't have anymore. The option
// selection bit addresses are given at the list below, you can add these option selections to 'Machine
// Parameters / Options' page as well btw. / Cutting > Right Front Vert. Clamping ... This is an option ...
// PC_KesimSolOn_Enkoderli L33, PC_KesimSagOnDikey_Enkodersiz L34, PC_KesimSolOn_HavaBosaltmali L35".
class ECT {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static Assembly fa;
    static Control Page(string folder, string name) { return (Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens." + folder + "." + name)); }
    static IEnumerable<Control> All(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var x in All(k)) yield return x; } }

    static Dictionary<string, int> mem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    static int Read(string a) { int v; return mem.TryGetValue(a, out v) ? v : 0; }
    // as the scanner judges a set: every part met (the machine bits by their devices)
    static bool Met(SystemCondition set) {
        return MachineConditionMap.Parts(set).All(p => ConditionBits.IsShared(p) || MachineConditionMap.CheckMachineBit((long)p, Read));
    }
    // the scanner's pass over a page: every control with a state
    static void Scan(Control page) {
        foreach (var c in new[] { page }.Concat(All(page)).OfType<IHmiControl>().ToList()) c.UpdateState(Read, Met);
        Pump(60);
    }
    static long Bit(string tag) {
        return MachineConditionMap.GetAllMachineBits().Where(kv => kv.Value.TagName != null && kv.Value.TagName.EndsWith(tag)).Select(kv => kv.Key).FirstOrDefault();
    }
    static double? Pos(CylinderUnitHMI u) { return (double?)typeof(CylinderUnitHMI).GetProperty("PositionValue", Any).GetValue(u, null); }
    static string Tip(BaseNumericHMI n) { return (string)typeof(BaseNumericHMI).GetMethod("TooltipLead", Any).Invoke(n, null); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "ect_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        NotesStore.Folder = Path.Combine(dir, "Notes");
        AlarmGeneration.SettingsPath = Path.Combine(dir, "AlarmGeneration.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.LoadMachineTranslations("Translations.csv");
        UnitManager.EnsureDefaults();
        fa = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly;
        var prog = fa.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        WiringTable.Clear();
        WiringTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wiring.csv"), plc => plc);
        AlarmHelpLinks.EnsureDiscovered();
        SecurityManager.Login("m", SecurityLevel.Maintenance);

        Console.WriteLine("--- the machine's conditions ---");
        long l33 = Bit("CuttingLeftFrontEncoder"), l34 = Bit("CuttingRightFrontVerticalEncoder"),
             left = Bit("LeftRecognitionEncoders"), right = Bit("RightRecognitionEncoders");
        Check("four new ones after Conveyor With Clamps (bit 21): bits 22 - 25, the earlier ones keep their numbers",
              l33 == 1L << 22 && l34 == 1L << 23 && left == 1L << 24 && right == 1L << 25 && Bit("ConveyorWithClamps") == 1L << 21,
              string.Join(" ", new[] { l33, l34, left, right }));
        Func<long, string> rule = b => { var d = MachineConditionMap.GetAllMachineBits().First(kv => kv.Key == b).Value; return d.Name + " " + d.Address + d.Operator + d.RequiredValue; };
        Check("  L33 = 1, L34 = 0, M450 (Y120) = 1, M450 = 0",
              rule(l33) == "Cutting Left Front with Encoder L33Equal1" && rule(l34) == "Cutting Right Front Vertical with Encoder L34Equal0" &&
              rule(left) == "Left Profile Recognition Encoders Selected M450Equal1" && rule(right) == "Right Profile Recognition Encoders Selected M450Equal0",
              string.Join(" | ", new[] { l33, l34, left, right }.Select(rule)));

        Console.WriteLine("--- Machine Parameters > Options: L33, L34, L35 ---");
        var form = new Form { ClientSize = new Size(1270, 840), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var opt = Page("Parameters", "Options");
        form.Controls.Add(opt); form.Show(); Pump(400);
        var boxes = All(opt).OfType<CheckBoxHMI>().ToList();
        Func<string, CheckBoxHMI> box = a => boxes.FirstOrDefault(b => b.Address == a);
        Check("three option boxes: L33 with Encoder, L34 without Encoder, L35 with Air Exhaust",
              box("L33") != null && box("L33").DescriptionText == "Cutting Left Front with Encoder" &&
              box("L34") != null && box("L34").DescriptionText == "Cutting Right Front Vertical without Encoder" &&
              box("L35") != null && box("L35").DescriptionText == "Cutting Left Front with Air Exhaust");
        var cross = box("L32");
        Check("  under Cross Roller (L32), the same column, size and user level, as far apart as the boxes above",
              new[] { "L33", "L34", "L35" }.Select(box).All(b => b.Left == cross.Left && b.Size == cross.Size && b.PlcNumber == 1 &&
                  b.RequiredSecurity == SecurityLevel.Maintenance && b.LogAction) &&
              // (the page scaled to the screen: a pixel either way)
              new[] { box("L33").Top - cross.Top, box("L34").Top - box("L33").Top, box("L35").Top - box("L34").Top }
                  .All(g => Math.Abs(g - (cross.Top - box("L27").Top)) <= 1),
              string.Join(" ", new[] { "L27", "L32", "L33", "L34", "L35" }.Select(a => a + "@" + box(a).Left + "," + box(a).Top + " " + box(a).Size.Width + "x" + box(a).Size.Height)));
        Check("  inside the page, overlapping nothing",
              new[] { "L33", "L34", "L35" }.Select(box).All(b => b.Bottom <= opt.Height &&
                  !boxes.Any(o => o != b && o.Parent == b.Parent && o.Bounds.IntersectsWith(b.Bounds))));
        LanguageManager.ChangeLanguage("TR");
        Check("  in Turkish: Kesim Sol Ön Enkoderli, Kesim Sağ Ön Dikey Enkodersiz, Kesim Sol Ön Hava Boşaltmalı",
              LanguageManager.Localize("Cutting Left Front with Encoder") == "Kesim Sol Ön Enkoderli" &&
              LanguageManager.Localize("Cutting Right Front Vertical without Encoder") == "Kesim Sağ Ön Dikey Enkodersiz" &&
              LanguageManager.Localize("Cutting Left Front with Air Exhaust") == "Kesim Sol Ön Hava Boşaltmalı" &&
              LanguageManager.Localize("Left Profile Recognition Encoders Selected") == "Sol Profil Tanıma Enkoderleri Seçili");
        LanguageManager.ChangeLanguage("EN");
        string shot = Environment.GetEnvironmentVariable("TOUCH_SHOTS");
        if (!string.IsNullOrEmpty(shot)) {
            Directory.CreateDirectory(shot);
            using (var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) {
                using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, bmp.Size);
                bmp.Save(Path.Combine(shot, "options_l33.png"));
            }
        }
        form.Controls.Remove(opt);

        Console.WriteLine("--- Machine Parameters > Encoders ---");
        var enc = Page("Parameters", "ParamEncoders");
        enc.Dock = DockStyle.Fill;
        form.Controls.Add(enc); Pump(500);
        var rows = CylinderEncoderSettingHMI.RowsOf(enc);
        Func<int, CylinderEncoderSettingHMI> row = n => rows.First(r => r.EncoderNumber == n);
        Check("Milling-Cutting Left Front (5): the machine has it with L33; Cutting Front Right Vertical (9): without L34",
              row(5).EncoderConditions == (SystemCondition)l33 && row(9).EncoderConditions == (SystemCondition)l34 &&
              rows.Count(r => r.EncoderConditions != SystemCondition.None) == 2);
        Check("  the profile recognition (2, 3) read while the right unit is selected, the left one's (10, 11) while the left",
              row(2).MeasuredConditions == (SystemCondition)right && row(3).MeasuredConditions == (SystemCondition)right &&
              row(10).MeasuredConditions == (SystemCondition)left && row(11).MeasuredConditions == (SystemCondition)left &&
              rows.Count(r => r.MeasuredConditions != SystemCondition.None) == 4);
        Check("  the scanner reads their devices (the rows' conditions as theirs)",
              ((IConditionalControl)row(5)).VisibilityConditions == (SystemCondition)l33 && ((IConditionalControl)row(1)).VisibilityConditions == SystemCondition.None);
        var places = rows.Select(r => r.Location).ToList();
        mem["L33"] = 0; mem["L34"] = 1; mem["M450"] = 0;
        Scan(enc);
        var shown = rows.Where(r => r.Visible).OrderBy(r => r.Top).ToList();
        Check("a machine without both: rows 5 and 9 not on the page, the others moved up (no empty lines)",
              shown.Count == 9 && !shown.Contains(row(5)) && !shown.Contains(row(9)) &&
              shown.Select(r => r.Location).SequenceEqual(places.Take(9)),
              string.Join(", ", shown.Select(r => r.EncoderNumber + "@" + r.Top)));
        mem["L33"] = 1; mem["L34"] = 0;
        Scan(enc);
        Check("  with them: all 11 in their own places again", rows.All(r => r.Visible) && rows.Select(r => r.Location).SequenceEqual(places));
        Check("  the right unit selected (M450 0): rows 2 and 3 show their position, 10 and 11 '–'",
              !row(2).Unit.PositionNotMeasuredNow && !row(3).Unit.PositionNotMeasuredNow &&
              row(10).Unit.PositionNotMeasuredNow && row(11).Unit.PositionNotMeasuredNow && row(10).Unit.PositionDisplay.Text == "–",
              row(10).Unit.PositionDisplay.Text);
        mem["M450"] = 1; mem["D4"] = 1234;
        Scan(enc);
        Check("  the left selected (M450 1): the other way round, the left one's D4 read",
              row(2).Unit.PositionNotMeasuredNow && row(3).Unit.PositionNotMeasuredNow &&
              !row(10).Unit.PositionNotMeasuredNow && !row(11).Unit.PositionNotMeasuredNow && row(2).Unit.PositionDisplay.Text == "–" &&
              row(10).Unit.PositionDisplay.Text.Replace(',', '.').StartsWith("123.4"), row(10).Unit.PositionDisplay.Text);
        form.Controls.Remove(enc);

        Console.WriteLine("--- on the manual pages: the cylinders follow their encoder's row ---");
        var milling = Page("Manual", "ManualMilling");
        var mfc = milling.Controls.Find("unitFront_Clamping", true).OfType<CylinderEncoderUnitHMI>().First();
        form.Controls.Add(milling); Pump(300);
        Check("Milling > Front Clamping: its position only with L33", mfc.IsResolved && mfc.Unit.PositionConditions == (SystemCondition)l33 &&
              mfc.Unit.PositionMeasuredConditions == SystemCondition.None);
        mem["L33"] = 0; Scan(milling);
        Check("  L33 OFF: a cylinder without a position - no readout, nothing judged by it",
              mfc.Unit.PositionOff && !mfc.Unit.PositionDisplay.Visible && Pos(mfc.Unit) == null, mfc.Unit.PositionDisplay.Visible.ToString());
        mem["L33"] = 1; mem["D20"] = 456; Scan(milling);
        Check("  L33 ON: its position (D20) again", !mfc.Unit.PositionOff && mfc.Unit.PositionDisplay.Visible && Pos(mfc.Unit) != null);
        form.Controls.Remove(milling);

        var cutting = Page("Manual", "ManualCutting");
        var rfv = cutting.Controls.Find("unitRight_Front_Vert_Clamping", true).OfType<CylinderEncoderUnitHMI>().First();
        form.Controls.Add(cutting); Pump(300);
        mem["L34"] = 1; Scan(cutting);
        Check("Cutting > Right Front Vert. Clamping: L34 ON (without encoder) no position", rfv.Unit.PositionConditions == (SystemCondition)l34 &&
              rfv.Unit.PositionOff && !rfv.Unit.PositionDisplay.Visible);
        mem["L34"] = 0; Scan(cutting);
        Check("  L34 OFF: its position (D7)", !rfv.Unit.PositionOff && rfv.Unit.PositionDisplay.Visible);
        form.Controls.Remove(cutting);

        var entry = Page("Manual", "ManualEntry_Conveyor");
        var pc7 = entry.Controls.Find("unitProfile_Clamping_7", true).OfType<CylinderEncoderUnitHMI>().First();
        form.Controls.Add(entry); Pump(300);
        Check("Entry Conveyor > Profile Clamping 7 (encoder 10): measured while the left unit is selected",
              pc7.Unit.PositionMeasuredConditions == (SystemCondition)left && pc7.Unit.PositionConditions == SystemCondition.None);
        mem["M450"] = 0; Scan(entry);
        BaseNumericHMI num = pc7.Unit.PositionDisplay;
        Check("  the right unit selected: '–', not the right unit's width", pc7.Unit.PositionNotMeasuredNow && pc7.Unit.PositionDisplay.Visible &&
              pc7.Unit.PositionDisplay.Text == "–" && Pos(pc7.Unit) == null, pc7.Unit.PositionDisplay.Text);
        Check("  its tip says why and when it is read", num != null && Tip(num).StartsWith("Not measured now") &&
              Tip(num).Contains("Left Profile Recognition Encoders Selected"), num == null ? "" : Tip(num));
        LanguageManager.ChangeLanguage("TR");
        Check("  in Turkish", num != null && Tip(num).StartsWith("Şu an ölçülmüyor") && Tip(num).Contains("Sol Profil Tanıma Enkoderleri Seçili"),
              num == null ? "" : Tip(num));
        LanguageManager.ChangeLanguage("EN");
        mem["M450"] = 1; mem["D4"] = 875; Scan(entry);
        Check("  the left selected: its D4", !pc7.Unit.PositionNotMeasuredNow && pc7.Unit.PositionDisplay.Text.Replace(',', '.').StartsWith("87.5"),
              pc7.Unit.PositionDisplay.Text);
        form.Controls.Remove(entry);

        Console.WriteLine("--- the HMI's own alarms: the position judged only while it is this cylinder's ---");
        var rec = Page("Manual", "ManualProfile_Recognition");
        var top = rec.Controls.Find("unitTop_Clamping", true).OfType<CylinderEncoderUnitHMI>().First();
        form.Controls.Add(rec); Pump(300);
        var w = top.Unit.BuildWatch();
        Check("Profile Recognition > Top Clamping (encoder 3): its watch judged by the position only while the right unit is selected",
              w != null && w.PositionWhile == right && w.Sides.Any(s => s.ByPosition), w == null ? "null" : w.PositionWhile + "");
        var mw = mfc.Unit.BuildWatch();
        Check("  Milling Front Clamping's: while L33", mw != null && mw.PositionWhile == l33);
        w.ModuleId = "ECT";
        AlarmGeneration.Clear();
        AlarmGeneration.Register(w);
        AlarmGeneration.Save("ECT", new AlarmGeneration.Settings { Enabled = true, DelaySeconds = 1 });
        Check("  M450 among the devices the watches read", AlarmGeneration.Addresses().Any(kv => kv.Key == 1 && kv.Value == "M450"));
        var side = w.Sides.First(s => s.ByPosition);
        var now = new DateTime(2026, 10, 30, 9, 0, 0);
        Action<double> run = secs => { for (double t = 0; t < secs - 1e-9; t += 0.1) { now = now.AddSeconds(0.1); AlarmGeneration.Evaluate(1, Read, now); } };
        Action<int> drive = v => { mem[side.Drive] = side.DriveOn ? v : 1 - v; };
        mem["D6"] = 300; drive(0); run(0.3);
        mem["M450"] = 1;                                          // the left unit's encoders selected
        drive(1); run(2);
        Check("the left unit selected: the right top clamping commanded, D6 not moving (it reads the left one) - no alarm",
              !AlarmManager.ActiveAlarms.Any(a => a.Generated), string.Join(", ", AlarmManager.ActiveAlarms.Select(a => a.ShownCode)));
        drive(0); run(0.3);
        mem["M450"] = 0;                                          // the right one's
        drive(1); run(2);
        Check("  the right one selected: D6 not moving is the clamp's failure", AlarmManager.ActiveAlarms.Any(a => a.Generated),
              side.Drive + " " + side.Code);
        AlarmGeneration.Clear();

        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
