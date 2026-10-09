using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// 9 October: "All the controls at FA2010 / Manual Movement / Clamping Moulds are same type of lines.
// I want you to create another control (called PneumaticPositioningHMI) ... just like AxisSettingHMI
// (the control name on the left end, then Manual movement, then Target position (TargetPosition,
// PositioningStartAddress), then Calibration (CalibrationAddress, CalibrateAddress), then Positioning
// Tolerance (PositioningToleranceAddress) and then Direction Ref - seen as Stopping Offset at the
// current project - (PositioningDirectionReferenceAddress)) ... re-create this page with the new
// control's instances ... only the Front Vertical ones are fixed, the Back Vertical and Back Horizontal
// ones are mounted only when the Clamping Mould option (L27) is True: set VisibilityConditions
// accordingly and move the next one up when the previous control is not visible".
class PPT {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }

    const long ClampingMould = 1L << 19;

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "ppt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        NotesStore.Folder = Path.Combine(dir, "Notes");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.LoadMachineTranslations("Translations.csv");
        UnitManager.EnsureDefaults();

        Console.WriteLine("--- the machine's condition for the Clamping Mould option ---");
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        var bits = MachineConditionMap.GetAllMachineBits().ToList();
        var opt = bits.FirstOrDefault(b => (b.Value.TagName ?? "").EndsWith("ClampingMouldOption"));
        Check("FA2010 registers it: L27 on its PLC 1, on when 1", opt.Value != null && opt.Value.Address == "L27" && opt.Value.PlcNumber == 1 &&
              opt.Value.RequiredValue == 1, opt.Value == null ? "none" : opt.Value.Address + " plc " + opt.Value.PlcNumber);
        Check("  its bit is 1 << 19 - the number the page stores", opt.Key == ClampingMould, opt.Key.ToString());
        Check("  named in the conditions list ('Clamping Mould option is on')",
              new Kaban.UI.Design.ConditionTextConverter().ConvertTo(null, null, (SystemCondition)ClampingMould, typeof(string)) as string == "Clamping Mould option is on");
        Check("  read from L27: 1 met, 0 not", MachineConditionMap.CheckMachineBit(ClampingMould, a => a == "L27" ? 1 : 0) &&
              !MachineConditionMap.CheckMachineBit(ClampingMould, a => 0));

        Console.WriteLine("--- the control ---");
        var row = new PneumaticPositioningHMI();
        Check("a UserControl the scanner updates (its conditions)", row is IConditionalControl);
        Check("its manual movement is a cylinder unit inside, as a row's", row.Unit != null && row.Unit.Parent == row && row.Unit.InRow);
        Check("  no field without an address (none set yet)", row.TargetField.Parent == null && row.StartButton.Parent == null &&
              row.CalibrationField.Parent == null && row.CalibrateButton.Parent == null && row.ToleranceField.Parent == null && row.DirectionRefField.Parent == null);
        row.TargetPosition = "D150"; row.PositioningStartAddress = "M50";
        row.CalibrationAddress = "D130"; row.CalibrateAddress = "M30";
        row.PositioningToleranceAddress = "D750"; row.PositioningDirectionReferenceAddress = "D790";
        row.Divisor = 100; row.DecimalPlaces = 2;
        Check("  with addresses, on the row: the fields and the two SET buttons",
              row.TargetField.Parent == row && row.TargetField.Address == "D150" && row.StartButton.Parent == row &&
              row.StartButton.BitActions.Count == 1 && row.StartButton.BitActions[0].Address == "M50" && row.StartButton.BitActions[0].Action == BitActionType.SET &&
              row.CalibrateButton.BitActions[0].Address == "M30" && row.CalibrateButton.BitActions[0].Action == BitActionType.SET &&
              row.CalibrationField.Address == "D130" && row.ToleranceField.Address == "D750" && row.DirectionRefField.Address == "D790");
        Check("  the fields' scaling (raw / 100, 2 decimals), the unit not drawn", row.TargetField.Divisor == 100 && row.ToleranceField.DecimalPlaces == 2 &&
              !row.DirectionRefField.ShowSuffix);
        Check("  security: target and start Operator, the rest Maintenance", row.TargetField.RequiredSecurity == SecurityLevel.Operator &&
              row.StartButton.RequiredSecurity == SecurityLevel.Operator && row.CalibrationField.RequiredSecurity == SecurityLevel.Maintenance &&
              row.CalibrateButton.RequiredSecurity == SecurityLevel.Maintenance && row.ToleranceField.RequiredSecurity == SecurityLevel.Maintenance &&
              row.DirectionRefField.RequiredSecurity == SecurityLevel.Maintenance && row.Unit.RequiredSecurity == SecurityLevel.Operator);
        Check("  every write logged", row.TargetField.LogAction && row.StartButton.LogAction && row.CalibrateButton.LogAction && row.Unit.LogAction);
        row.CommandBackAddress = "M541"; row.CommandForwardAddress = "M540"; row.ErrorBackAddress = "M915"; row.ErrorForwardAddress = "M916";
        row.PositionAddress = "D110"; row.PositionDivisor = 10; row.PositionDecimalPlaces = 2;
        row.ValveType = CylinderValve.ThreeWayClosedCenter; row.FeedbackType = CylinderFeedback.AnalogueMagnetic;
        row.TitleTextKey = "Left Profile Recognition Front Vertical";
        var u = row.Unit;
        Check("the manual movement is the unit's: back = A, forward = B, errors, position, types, title",
              u.CommandAAddress == "M541" && u.CommandBAddress == "M540" && u.ErrorAAddress == "M915" && u.ErrorBAddress == "M916" &&
              u.PositionAddress == "D110" && u.PositionDivisor == 10 && u.PositionDecimalPlaces == 2 &&
              u.ValveType == CylinderValve.ThreeWayClosedCenter && u.FeedbackType == CylinderFeedback.AnalogueMagnetic &&
              u.TitleTextKey == "Left Profile Recognition Front Vertical" && row.TypeCheck == "OK", row.TypeCheck);
        row.VerticalMovement = true;
        Check("  a vertical one: down and up", u.ButtonATextKey == "▼" && u.ButtonBTextKey == "▲", u.ButtonATextKey + u.ButtonBTextKey);
        Check("  its help link: the error bit names the row's unit, its valve and feedback",
              u.HelpLinks().Any(l => l.Address == "M915" && l.Unit == "Left Profile Recognition Front Vertical" && l.Valve == (int)CylinderValve.ThreeWayClosedCenter));
        row.TargetPosition = "";
        Check("an address cleared: its field off the row", row.TargetField.Parent == null && row.StartButton.Parent == row);
        row.TargetPosition = "D150";

        Console.WriteLine("--- the Properties window ---");
        var props = System.ComponentModel.TypeDescriptor.GetProperties(row).Cast<System.ComponentModel.PropertyDescriptor>().Where(p => p.IsBrowsable).ToList();
        Func<string, string> cat = n => { var p = props.FirstOrDefault(x => x.Name == n); return p == null ? "(none)" : p.Category.TrimStart('\t'); };
        Check("the user's properties in their sections", cat("TargetPosition") == "Target position" && cat("PositioningStartAddress") == "Target position" &&
              cat("CalibrationAddress") == "Calibration" && cat("CalibrateAddress") == "Calibration" &&
              cat("PositioningToleranceAddress") == "Positioning tolerance" && cat("PositioningDirectionReferenceAddress") == "Direction ref",
              cat("TargetPosition") + "/" + cat("PositioningToleranceAddress") + "/" + cat("PositioningDirectionReferenceAddress"));
        Check("  VisibilityConditions with the conditions editor", cat("VisibilityConditions") == "Conditions" &&
              props.First(p => p.Name == "VisibilityConditions").Converter is Kaban.UI.Design.ConditionTextConverter);
        Check("  the inner parts not in it (nor in the designer file)", props.All(p => p.Name != "Unit" && p.Name != "TargetField" && p.Name != "Columns"));

        Console.WriteLine("--- FA2010 > Manual Movement > Clamping Moulds ---");
        var form = new Form { ClientSize = new Size(1270, 860), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var page = new Kaban.Machines.FA2010.Screens.Manual.ManualClamping_Moulds { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        form.Show(); Pump(800);
        var rows = PneumaticPositioningHMI.RowsOf(page);
        Check("16 moulds, each a PneumaticPositioningHMI", rows.Count == 16, rows.Count.ToString());
        Check("  nothing else of the old lines on the page (units, fields, buttons)",
              !page.Controls.OfType<CylinderUnitHMI>().Any() && !page.Controls.OfType<NumericInputHMI>().Any() && !page.Controls.OfType<ButtonHMI>().Any());
        var heads = page.Controls.OfType<Label>().Where(l => (l.Tag as string ?? "").StartsWith(PneumaticPositioningHMI.HeadingTag)).ToList();
        Check("  five headings: Manual movement, Target position, Calibration, Positioning tolerance, Direction ref",
              string.Join(",", heads.OrderBy(l => l.Left).Select(l => l.Text)) == "Manual movement,Target position,Calibration,Positioning tolerance,Direction ref",
              string.Join(",", heads.OrderBy(l => l.Left).Select(l => l.Text)));
        var first = rows[0];
        bool over = true; string why = "";
        foreach (var h in heads)
        {
            var key = ((string)h.Tag).Substring(PneumaticPositioningHMI.HeadingTag.Length);
            var col = first.Columns[key];
            if (h.Left != first.Left + col.X || h.Width != col.Y || h.Bottom > first.Top) { over = false; why += key + " " + h.Bounds + " col " + col + "; "; }
        }
        Check("  each over its column, above the first row", over, why);
        var f = rows[0];
        Check("the first: Left Profile Recognition Front Vertical, its addresses (from the old page)",
              f.TitleTextKey == "Left Profile Recognition Front Vertical" && f.CommandBackAddress == "M541" && f.CommandForwardAddress == "M540" &&
              f.PositionAddress == "D110" && f.PositionDivisor == 10 && f.TargetPosition == "D150" && f.PositioningStartAddress == "M50" &&
              f.CalibrationAddress == "D130" && f.CalibrateAddress == "M30" && f.PositioningToleranceAddress == "D750" &&
              f.PositioningDirectionReferenceAddress == "D790" && f.Divisor == 100 && f.DecimalPlaces == 2);
        Check("  its error bits, valve and feedback kept from its unit", f.ErrorBackAddress == "M915" && f.ErrorForwardAddress == "M916" &&
              f.ValveType == CylinderValve.ThreeWayClosedCenter && f.FeedbackType == CylinderFeedback.AnalogueMagnetic);
        var last = rows[15];
        Check("the last: Right Front Vertical Clamping (M585 / M584, D128, D167 M67, D147 M47, D846, D807)",
              last.TitleTextKey == "Right Front Vertical Clamping" && last.CommandBackAddress == "M585" && last.CommandForwardAddress == "M584" &&
              last.PositionAddress == "D128" && last.TargetPosition == "D167" && last.PositioningStartAddress == "M67" &&
              last.CalibrationAddress == "D147" && last.CalibrateAddress == "M47" && last.PositioningToleranceAddress == "D846" &&
              last.PositioningDirectionReferenceAddress == "D807");
        var front = rows.Where(r => !r.TitleTextKey.Contains("Back")).ToList();
        var back = rows.Where(r => r.TitleTextKey.Contains("Back")).ToList();
        Check("the 6 front vertical moulds always shown (no condition)", front.Count == 6 && front.All(r => r.VisibilityConditions == SystemCondition.None),
              string.Join(", ", front.Select(r => r.TitleTextKey)));
        Check("the 10 back moulds (vertical and horizontal) only with the Clamping Mould option", back.Count == 10 &&
              back.All(r => (long)r.VisibilityConditions == ClampingMould));
        Check("vertical ones down / up, horizontal ones back / forward",
              rows.All(r => r.VerticalMovement == r.TitleTextKey.Contains("Vertical")));
        int nameW = first.Columns["Name"].Y;
        Check("one names' column for the page, as wide as its widest name",
              rows.All(r => r.Columns["Name"].Y == nameW) &&
              rows.All(r => TextRenderer.MeasureText(r.NameLabel.Text, r.NameLabel.Font).Width <= r.NameLabel.Width), nameW.ToString());
        Check("  the rows line up: every column at the same x", rows.All(r => r.Left == first.Left && r.Columns["Target"] == first.Columns["Target"]));
        Check("  as tall as an axis row; 4 px between rows", rows.All(r => r.Height == AxisSettingHMI.RowHeight) &&
              rows.Zip(rows.Skip(1), (a, b) => b.Top - a.Bottom).All(g => g == 4));
        Check("  the position at the axis rows' width, its buttons the axis jogs'", u.ButtonA.Width == AxisSettingHMI.WJog || f.Unit.ButtonA.Width == AxisSettingHMI.WJog,
              f.Unit.ButtonA.Width + " " + f.Unit.PositionDisplay.Width);
        var inside = rows.SelectMany(r => r.Controls.Cast<Control>()).All(c => c.Left >= 0 && c.Right <= c.Parent.Width && c.Top >= 0 && c.Bottom <= c.Parent.Height);
        Check("  every part inside its row", inside);

        Console.WriteLine("--- the option off: the back moulds out, the rest moved up ---");
        var places = rows.Select(r => r.Location).ToList();
        Func<SystemCondition, bool> off = c => ((long)c & ClampingMould) == 0;
        Func<SystemCondition, bool> on = c => true;
        Func<string, int> read = a => 0;
        foreach (var r in rows) r.UpdateState(read, off);
        Pump(100);
        var shown = rows.Where(r => r.Visible).OrderBy(r => r.Top).ToList();
        Check("6 shown, the front vertical ones", shown.Count == 6 && shown.All(r => !r.TitleTextKey.Contains("Back")),
              string.Join(", ", shown.Select(r => r.TitleTextKey)));
        Check("  in the first six places, in the page's order (no empty lines)",
              shown.Select(r => r.Location).SequenceEqual(places.Take(6)) &&
              shown.Select(r => r.TitleTextKey).SequenceEqual(front.Select(r => r.TitleTextKey)),
              string.Join(" ", shown.Select(r => r.Top)));
        Check("  the headings stay over them", heads.All(h => h.Bottom <= shown[0].Top));
        Console.WriteLine("--- the option on again ---");
        foreach (var r in rows) r.UpdateState(read, on);
        Pump(100);
        Check("all 16 shown, each in its own place again", rows.All(r => r.Visible) && rows.Select(r => r.Location).SequenceEqual(places));

        Console.WriteLine("--- what the program says about a part of a row ---");
        Check("a field: the row's name and its column", NoteTarget.NameOf(f.TargetField) == "Left Profile Recognition Front Vertical - Target position",
              NoteTarget.NameOf(f.TargetField));
        Check("  the name itself: the row's name", NoteTarget.NameOf(f.NameLabel) == "Left Profile Recognition Front Vertical", NoteTarget.NameOf(f.NameLabel));
        Check("  its translation key: TitleTextKey", NoteTarget.KeysOf(f.NameLabel).Count == 1 && NoteTarget.KeysOf(f.NameLabel)[0].Key == "Left Profile Recognition Front Vertical");
        Check("  the manual movement: the row's name and 'Manual movement'", NoteTarget.NameOf(f.Unit) == "Left Profile Recognition Front Vertical - Manual movement",
              NoteTarget.NameOf(f.Unit));
        var tap = NoteTarget.Notable(f.ToleranceField);
        Check("  a tap on a field is about the field, not the row", tap == f.ToleranceField, tap == null ? "null" : tap.Name);
        var headKeys = NoteTarget.KeysOf(heads.First(h => h.Text == "Target position"));
        Check("  a heading's key: Col_PpTarget", headKeys.Any(k => k.Key == "Col_PpTarget"));
        var devices = DeviceCollector.FromControl(page, 1);
        var d150 = devices.FirstOrDefault(d => d.Address == "D150");
        Check("the page's devices: D150 used as the first mould's setting", d150 != null && d150.UsedAs.StartsWith("Left Profile Recognition Front Vertical"),
              d150 == null ? "missing" : d150.UsedAs);
        Check("  and its command, position and error bit", new[] { "M541", "M540", "D110", "M50", "M30", "D130", "D750", "D790", "M915" }
              .All(a => devices.Any(d => d.Address == a)));
        var scanner = new Kaban.UI.Plc.PlcScanner(page);
        typeof(Kaban.UI.Plc.PlcScanner).GetMethod("FindControls", Any).Invoke(scanner, new object[] { page });
        var others = (System.Collections.IList)typeof(Kaban.UI.Plc.PlcScanner).GetField("_others", Any).GetValue(scanner);
        var numerics = (System.Collections.IList)typeof(Kaban.UI.Plc.PlcScanner).GetField("_numerics", Any).GetValue(scanner);
        Check("the scanner finds the 16 rows (their conditions) and their 64 fields + 16 positions",
              others.Cast<object>().Count(o => o is PneumaticPositioningHMI) == 16 && numerics.Count == 16 * 5, numerics.Count.ToString());

        Console.WriteLine("--- in Turkish ---");
        LanguageManager.ChangeLanguage("TR");
        Pump(200);
        var target = heads.First(h => (string)h.Tag == PneumaticPositioningHMI.HeadingTag + "Target");
        Check("the headings translated", target.Text == "Hedef pozisyon", target.Text);
        Check("  the names translated, the column as wide as the widest of them",
              rows.All(r => r.NameLabel.Text == LanguageManager.GetString(r.TitleTextKey, r.TitleTextKey)) &&
              rows.All(r => TextRenderer.MeasureText(r.NameLabel.Text, r.NameLabel.Font).Width <= r.NameLabel.Width) &&
              rows.All(r => r.Columns["Target"] == rows[0].Columns["Target"]));
        LanguageManager.ChangeLanguage("EN");
        Pump(100);
        form.Close();

        Console.WriteLine(fail == 0 ? "\nPNEUMATIC POSITIONING ALL PASS" : "\nPNEUMATIC POSITIONING " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
