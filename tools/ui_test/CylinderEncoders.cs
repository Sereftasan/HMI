using System;
using System.Collections.Generic;
using System.ComponentModel;
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

// 9 October: "All the controls at FA2010 / Machine Parameters / Encoders Parameters are same type of lines
// (apart from the first one, Robot Position). Create another control (CylinderEncoderSettingHMI) just like
// AxisSettingHMI (the name on the left end, then Manual movement, then Reference (ReferenceAddress), then
// Measuring Tolerance, then Minimum Limit (Start Limit at the current project) and Unclamping Interval) ...
// re-create Encoders just using this control ... remove Robot Position ... then CylinderEncoderUnitHMI of it
// (just like AxisUnitHMI for AxisSettingHMI) and change all the related cylinder controls on the Manual
// Movement pages to it, so we wouldn't need the position property for the cylinder movement controls any
// more ... Remove 'Parameters' of all the tab titles: Axis, Conveyor and Robot (Entry divided, the right side
// of Robot to the left bottom), Milling and Cutting (Process divided), Encoders, Others and Alarms".
class CET {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static Assembly fa;
    static Control Page(string folder, string name) { return (Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens." + folder + "." + name)); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "cet_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        NotesStore.Folder = Path.Combine(dir, "Notes");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.LoadMachineTranslations("Translations.csv");
        UnitManager.EnsureDefaults();
        fa = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly;
        var prog = fa.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        AlarmHelpLinks.EnsureDiscovered();

        Console.WriteLine("--- Machine Parameters' tabs ---");
        var mp = MachineRegistry.AllScreens().First(kv => kv.Key.IsMain && kv.Value.Role == ScreenRoles.MachineParameters).Value;
        var screen = (SettingsScreen)mp.CreateView();
        var titles = screen.AllPages.Select(p => LanguageManager.GetString(p.TitleKey, p.TitleFallback)).ToList();
        Check("Axis, Conveyor, Robot, Milling, Cutting, Encoders, Others, Alarms - no 'Parameters'",
              string.Join(", ", titles) == "Axis, Conveyor, Robot, Milling, Cutting, Encoders, Others, Alarms", string.Join(", ", titles));
        AlarmHelpLinks.Discard(screen);
        LanguageManager.ChangeLanguage("TR");
        Check("  in Turkish: Eksen ... Enkoderler, Diğerleri, Alarmlar", LanguageManager.GetString("Tab_AxisSettings", "") == "Eksen" &&
              LanguageManager.GetString("Tab_ParamEncoders", "") == "Enkoderler" && LanguageManager.GetString("Tab_ParamOther", "") == "Diğerleri" &&
              LanguageManager.GetString("Set_Alarms", "") == "Alarmlar");
        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine("--- Encoders: a CylinderEncoderSettingHMI row an encoder ---");
        var form = new Form { ClientSize = new Size(1270, 700), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var enc = Page("Parameters", "ParamEncoders");
        enc.Dock = DockStyle.Fill;
        form.Controls.Add(enc);
        form.Show(); Pump(700);
        var rows = CylinderEncoderSettingHMI.RowsOf(enc);
        Check("11 rows, numbered 1 to 11, nothing else on the page but their headings", rows.Count == 11 &&
              rows.Select(r => r.EncoderNumber).SequenceEqual(Enumerable.Range(1, 11)) &&
              // the page a table once shown (AxisPageFrame): its headings in its heading, its rows in its body
              CylinderRowHMI.RowsHost(enc) != enc && CylinderRowHMI.RowsHost(enc).Controls.Cast<Control>().All(c => c is CylinderEncoderSettingHMI) &&
              CylinderRowHMI.HeadingsHost(enc).Controls.Cast<Control>().All(c => c is Label && ((c.Tag as string) ?? "").StartsWith(CylinderEncoderSettingHMI.HeadingTag)),
              string.Join(", ", rows.Select(r => r.EncoderNumber + " " + r.TitleTextKey)));
        Check("  no Robot Position (Robot X: M604 / M605, D22)", !rows.Any(r => r.CommandBackAddress == "M604" || r.PositionAddress == "D22"));
        var heads = CylinderRowHMI.HeadingsHost(enc).Controls.OfType<Label>().OrderBy(l => l.Left).ToList();
        Func<Control, Rectangle> scr = c => c.RectangleToScreen(c.ClientRectangle);
        Check("  headings: Manual movement, Reference, Measuring tolerance, Minimum limit, Unclamping interval",
              string.Join(", ", heads.Select(l => l.Text)) == "Manual movement, Reference, Measuring tolerance, Minimum limit, Unclamping interval",
              string.Join(", ", heads.Select(l => l.Text)));
        var first = rows[0];
        Check("  each over its column", heads.All(h => {
            var key = ((string)h.Tag).Substring(CylinderEncoderSettingHMI.HeadingTag.Length);
            return scr(h).Left == scr(first).Left + first.Columns[key].X && h.Width == first.Columns[key].Y && scr(h).Bottom <= scr(first).Top; }));
        Check("  one names' column, the rows lined up, 4 px apart", rows.All(r => r.Columns["Name"] == first.Columns["Name"] && r.Left == first.Left) &&
              rows.Zip(rows.Skip(1), (a, b) => b.Top - a.Bottom).All(g => g == 4));
        var prf = rows.First(r => r.TitleTextKey == "Profile Recognition Front");
        Check("Profile Recognition Front: reference D477, measuring tolerance D479, minimum limit D483, unclamping interval D51 - all / 10, one decimal",
              prf.ReferenceField.Address == "D477" && prf.ToleranceField.Address == "D479" && prf.MinimumLimitField.Address == "D483" &&
              prf.UnclampingField.Address == "D51" && prf.ReferenceField.Divisor == 10 && prf.UnclampingField.DecimalPlaces == 1);
        Check("  its settings the Maintenance level's, moving it the Operator's", prf.ReferenceField.RequiredSecurity == SecurityLevel.Maintenance &&
              prf.Unit.RequiredSecurity == SecurityLevel.Operator);
        var milling = rows.First(r => r.TitleTextKey == "Milling Top");
        Check("Milling Top: only a reference and an unclamping interval - the other two fields not there, their columns kept",
              milling.ReferenceField.Parent == milling && milling.UnclampingField.Parent == milling && milling.ToleranceField.Parent == null &&
              milling.MinimumLimitField.Parent == null && milling.Columns["Unclamping"] == prf.Columns["Unclamping"]);
        // the two left open for a decision (REVIEW 4r): a clamp with a position its feedback type has none for
        var open = new[] { "Cutting Front Right Vertical", "Left Profile Recognition Front" };
        Check("every row's movement typed and matching it (but the two left open: a position its feedback has none for)",
              rows.All(r => r.ValveType != CylinderValve.Unspecified && r.FeedbackType != CylinderFeedback.Unspecified) &&
              rows.Where(r => !open.Contains(r.TitleTextKey)).All(r => r.TypeCheck == "OK"),
              string.Join(" | ", rows.Where(r => r.TypeCheck != "OK").Select(r => r.TitleTextKey + ": " + r.TypeCheck)));
        var props = TypeDescriptor.GetProperties(prf).Cast<PropertyDescriptor>().Where(p => p.IsBrowsable).ToList();
        Func<string, string> cat = n => { var p = props.FirstOrDefault(x => x.Name == n); return p == null ? "(none)" : p.Category.TrimStart('\t'); };
        Check("its properties in their sections: Reference, Measuring tolerance, Minimum limit, Unclamping interval",
              cat("ReferenceAddress") == "Reference" && cat("MeasuringToleranceAddress") == "Measuring tolerance" && cat("MinimumLimitAddress") == "Minimum limit" &&
              cat("UnclampingIntervalAddress") == "Unclamping interval" && cat("EncoderNumber") == "Device");
        Check("the notes name a field by its row and column", NoteTarget.NameOf(prf.MinimumLimitField) == "Profile Recognition Front - Minimum limit",
              NoteTarget.NameOf(prf.MinimumLimitField));
        form.Close(); Pump(100);

        Console.WriteLine("--- a cylinder with an encoder on a manual page: CylinderEncoderUnitHMI ---");
        var cutting = Page("Manual", "ManualCutting");
        var lt = cutting.Controls.Find("unitLeft_Top_Clamping", true).OfType<CylinderEncoderUnitHMI>().FirstOrDefault();
        Check("Cutting's Left Top Clamping: encoder 6, found", lt != null && lt.EncoderNumber == 6 && lt.IsResolved);
        var row6 = AlarmHelpLinks.FindEncoder(1, 6);
        Check("  everything from its row (Cutting Left Top): buttons, sensor, position and scaling, valve, feedback",
              lt.Unit.CommandAAddress == "M672" && lt.Unit.CommandBAddress == "M671" && lt.Unit.Lamp1AAddress == "M245" && lt.Unit.PositionAddress == "D15" &&
              lt.Unit.PositionDivisor == 10 && lt.Unit.PositionDecimalPlaces == 1 && lt.Unit.ValveType == CylinderValve.ThreeWayClosedCenter &&
              lt.Unit.FeedbackType == CylinderFeedback.EncoderPlusReference && row6 != null && row6.NameKey == "Cutting Left Top");
        Check("  its title the page's own (Left Top Clamping), not the row's", lt.Unit.TitleTextKey == "Left Top Clamping");
        Check("  as a cylinder unit, the same size, its type checked OK", lt.Size == new Size(360, 64) && lt.Unit.TypeCheck == "OK", lt.Unit.TypeCheck);
        var ownProps = TypeDescriptor.GetProperties(lt).Cast<PropertyDescriptor>().Where(p => p.IsBrowsable && p.ComponentType.Assembly == typeof(CylinderEncoderUnitHMI).Assembly)
                                     .Select(p => p.Name).OrderBy(n => n).ToList();
        Check("  its own properties: the encoder's number and station, its title here, its conditions here",
              string.Join(",", ownProps) == "EnableConditions,EncoderNumber,PlcNumber,TitleTextKey,VisibilityConditions", string.Join(",", ownProps));
        var exit = Page("Manual", "ManualExit");
        var rtc = exit.Controls.Find("unitRobot_Top_Clamping", true).OfType<CylinderEncoderUnitHMI>().First();
        Check("Exit's Robot Top Clamping: its error bits and help from its row (Exit Carrying Top), its conditions (manual mode) its own",
              rtc.Unit.ErrorAAddress == "M854" && rtc.Unit.ErrorBAddress == "M840" && rtc.Unit.HelpAFiles == "c# learning structure.png" &&
              rtc.VisibilityConditions == SystemCondition.ManualMode && rtc.Unit.VisibilityConditions == SystemCondition.ManualMode &&
              rtc.Unit.EnableConditions == SystemCondition.ManualMode);
        var all = new[] { "ManualEntry_Conveyor", "ManualRobot", "ManualProfile_Recognition", "ManualMilling", "ManualCutting", "ManualExit" }
            .SelectMany(n => Page("Manual", n).Controls.OfType<CylinderEncoderUnitHMI>()).ToList();
        Check("11 encoder units on the manual pages, each its encoder's once, all found", all.Count == 11 && all.All(u => u.IsResolved) &&
              all.Select(u => u.EncoderNumber).OrderBy(n => n).SequenceEqual(Enumerable.Range(1, 11)),
              string.Join(",", all.Select(u => u.Name + "#" + u.EncoderNumber)));
        var missing = new CylinderEncoderUnitHMI { EncoderNumber = 99 };
        Check("a number with no row: said on the unit, no address", !missing.IsResolved && missing.Unit.TitleTextKey == "Encoder 99: no encoder settings row" &&
              string.IsNullOrEmpty(missing.Unit.CommandAAddress) && string.IsNullOrEmpty(missing.Unit.PositionAddress), missing.Unit.TitleTextKey);
        Check("  the encoder's error bits linked once, to its row's name", AlarmHelpLinks.Find(1, "M845") != null &&
              AlarmHelpLinks.Find(1, "M845").Unit == "Profile Recognition Front", AlarmHelpLinks.Find(1, "M845") == null ? "none" : AlarmHelpLinks.Find(1, "M845").Unit);

        Console.WriteLine("--- a cylinder unit placed by hand has no position of its own ---");
        var cu = TypeDescriptor.GetProperties(new CylinderUnitHMI()).Cast<PropertyDescriptor>().Where(p => p.IsBrowsable).Select(p => p.Name).ToList();
        Check("PositionAddress, its scaling and its display are not in the Properties window",
              !cu.Any(n => n.StartsWith("Position")), string.Join(",", cu.Where(n => n.StartsWith("Position"))));
        var legacy = new CylinderUnitHMI { PositionAddress = "D10", PositionDivisor = 10 };
        Check("  a page that still sets them still shows its position", legacy.PositionDisplay.Address == "D10");

        Console.WriteLine("--- Robot X: the old Robot Position (D22) in its position's tooltip ---");
        var robot = Page("Manual", "ManualRobot");
        var robotX = robot.Controls.OfType<AxisUnitHMI>().First(a => a.AxisNumber == 1);
        Check("the axis row's load-side encoder D22 (Bin_32 / 1000, as W0)", AlarmHelpLinks.FindAxis(1, 1).RealPosition == "D22" &&
              robotX.Unit.PositionDisplay.ComparedAddress == "D22", robotX.Unit.PositionDisplay.ComparedAddress);

        Console.WriteLine("--- the split pages ---");
        Func<Control, List<NumericInputHMI>> fields = p => p.Controls.OfType<NumericInputHMI>().OrderBy(n => n.Top).ToList();
        var robotP = Page("Parameters", "ParamRobot");
        var rf = fields(robotP);
        Check("Robot: 20 values in one column, the old right column under the left (Robot Height after Cross Rollers Robot Y Offset)",
              rf.Count == 20 && rf.Select(n => n.Left).Distinct().Count() == 1 && rf[9].Address == "D782" && rf[10].Address == "D664" && rf.Last().Address == "D784");
        Check("  as high as the old page's two sections (fits a page)", rf.Last().Bottom < 760, rf.Last().Bottom.ToString());
        var conv = fields(Page("Parameters", "ParamConveyor"));
        var mil = fields(Page("Parameters", "ParamMilling"));
        var cut = fields(Page("Parameters", "ParamCutting"));
        Check("Conveyor 8, Milling 13, Cutting 13 - one column each", conv.Count == 8 && mil.Count == 13 && cut.Count == 13 &&
              new[] { conv, mil, cut }.All(f => f.Select(n => n.Left).Distinct().Count() == 1));

        Console.WriteLine(fail == 0 ? "\nCYLINDER ENCODERS ALL PASS" : "\nCYLINDER ENCODERS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
