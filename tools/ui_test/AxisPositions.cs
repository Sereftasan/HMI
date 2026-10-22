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
using Kaban.UI.Screens;

// 22 October: "change the 'Position' column caption of AxisSettingHMI and AxisUnitHMI to a multiple
// combo selection so the user can see every kind of position side by side: the Position (the axis's
// command position), the 'Servo Encoder Value' (Real current value, listed only with a simple motion
// axis), the 'Load Position' (listed only when an axis has a load side encoder). The tooltip about the
// load side encoder and the deviation removed, and Feed current value and Real current value removed
// from the Motion monitor items - they show the same values."
class APS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static string Keys(AxisSettingHMI r) { return string.Join(",", r.Columns.OrderBy(kv => kv.Value.X).Select(kv => kv.Key)); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "aps_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();

        Console.WriteLine("--- the Motion monitor list: not the axis's positions ---");
        var mbar = new AxisPickBar(ViewPreferences.AxisItems, ViewPreferences.MotionColumns, items: false, motion: true);
        var mitems = mbar.MotionPick.Items.Select(i => i.Key).ToList();
        Check("no Feed current value, no Real current value - the Deviation counter and the rest still", !mitems.Contains("FeedCurrentValue") && !mitems.Contains("RealCurrentValue") &&
              mitems.Contains("DeviationCounter") && mitems.Count == SimpleMotion.Items.Count - 2);

        Console.WriteLine("--- the FA2010 Axis Settings page ---");
        ViewPreferences.SetMotionColumnKeys(new[] { "Busy", "RealCurrentValue" });   // a list of before
        var form = new Form { ClientSize = new Size(1270, 700), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var page = new Kaban.Machines.FA2010.Screens.Parameters.AxisSettings { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        form.Show(); Pump(1000);
        var frame = AxisPageFrame.Of(page);
        var items = page.Controls.Find("axisItemsBar", true).FirstOrDefault() as AxisPickBar;
        var rows = frame.Body.Controls.OfType<AxisSettingHMI>().OrderBy(r => r.Top).ToList();
        var robotX = rows.First(r => r.AxisNameKey == "Axis_RobotX");
        var other = rows.First(r => string.IsNullOrWhiteSpace(r.LoadSideEncoderAddress) && !r.HasMotion);
        Check("a Motion monitor list of before: its Real current value no column", !robotX.Columns.ContainsKey("Mot:RealCurrentValue") && robotX.Columns.ContainsKey("Mot:Busy"),
              Keys(robotX));
        Func<string, Label> head = k => frame.HeadContent.Controls.OfType<Label>().FirstOrDefault(l => (l.Tag as string) == "AxisHeading:" + k);
        var pick = items.PositionsPick;
        Check("nothing picked: the Position alone - its heading opens the Positions list", pick != null && head("Position") != null &&
              AxisHeadingFilter.PickOf(head("Position")) == pick && !robotX.Columns.ContainsKey("ServoEncoder") && !robotX.Columns.ContainsKey("LoadPosition"),
              Keys(robotX));
        Check("  the position's tooltip compares nothing any more", robotX.Controls.OfType<NumericDisplayHMI>().All(d => string.IsNullOrEmpty(d.ComparedAddress)));
        typeof(Control).GetMethod("OnClick", Any).Invoke(head("Position"), new object[] { EventArgs.Empty });
        Pump(200);
        Check("the list offers: Position, Servo Encoder Value (axes on a module), Load Position (Robot X's D22)",
              string.Join(",", pick.Items.Select(i => i.Text)) == "Position,Servo Encoder Value,Load Position" && pick.IsOpen,
              string.Join(",", pick.Items.Select(i => i.Text)));
        if (pick.OpenDrop != null) pick.OpenDrop.Close();
        Pump(100);

        Console.WriteLine("--- all three, side by side ---");
        pick.Select(new[] { "Position", "ServoEncoder", "LoadPosition" });
        Pump(300);
        Check("kept: the three", ViewPreferences.ListOf(ViewPreferences.AxisPositions).SequenceEqual(new[] { "Position", "ServoEncoder", "LoadPosition" }),
              string.Join(",", ViewPreferences.ListOf(ViewPreferences.AxisPositions)));
        var cols = Keys(robotX);
        Check("Robot X's columns: Position, Servo Encoder Value, Load Position between the jogs", cols.Contains("Position,ServoEncoder,LoadPosition"), cols);
        var servo = robotX.Controls["posServoEncoder"] as MotionValueHMI;
        var load = robotX.Controls["posLoad"] as NumericDisplayHMI;
        var pos = robotX.Controls.OfType<NumericDisplayHMI>().First(d => d.Address == robotX.PositionAddress);
        Check("  its servo encoder: the module's real current value (Md.101, U1\\G2450 + 100 per axis)", servo != null && servo.Item.Key == "RealCurrentValue" &&
              servo.Address == SimpleMotion.Device(robotX.MotionNumber, SimpleMotion.AddressOf(SimpleMotion.ServoEncoderItem, robotX.MotionAxisIndex)),
              servo == null ? "none" : servo.Address);
        Check("  its load position: D22, its divisor, to its last count", load != null && load.Address == "D22" && load.Divisor == robotX.LoadSideEncoderScale &&
              load.DecimalPlaces == BaseNumericHMI.ResolutionDecimals(robotX.LoadSideEncoderScale, robotX.DecimalPlaces), load == null ? "none" : load.Address + " " + load.DecimalPlaces);
        Check("  the three as wide and tall as one another, in a line", servo.Width == pos.Width && load.Width == pos.Width && servo.Top == pos.Top && load.Top == pos.Top &&
              servo.Left > pos.Right && load.Left > servo.Right);
        Check("an axis with neither: the space (the columns in line), nothing in it", Keys(other) == cols.Replace(",Mot:Busy", other.Columns.ContainsKey("Mot:Busy") ? ",Mot:Busy" : "") &&
              other.Controls["posServoEncoder"] == null && other.Controls["posLoad"] == null && other.Columns["LoadPosition"].X == robotX.Columns["LoadPosition"].X,
              Keys(other));
        Check("a heading over each: 'Servo encoder' (short: the list and its tooltip name it in full), 'Load Position' - each opening the list", head("ServoEncoder") != null && head("ServoEncoder").Visible &&
              head("ServoEncoder").Text == "Servo encoder" && head("LoadPosition").Text == "Load Position" &&
              AxisHeadingFilter.PickOf(head("LoadPosition")) == pick && head("LoadPosition").Left == head("Position").Left + (load.Left - pos.Left),
              head("LoadPosition") == null ? "none" : head("LoadPosition").Bounds + " vs " + head("Position").Bounds);
        pick.Select(new[] { "LoadPosition" });
        Pump(300);
        Check("only the Load Position: the Position's column gone", !robotX.Columns.ContainsKey("Position") && robotX.Columns.ContainsKey("LoadPosition") &&
              !head("Position").Visible && head("LoadPosition").Visible, Keys(robotX));
        pick.Select(new string[0]);
        Pump(300);
        Check("nothing ticked: the Position again, kept empty", Keys(robotX).Contains("Position") && !robotX.Columns.ContainsKey("LoadPosition") &&
              ViewPreferences.ListOf(ViewPreferences.AxisPositions).Count == 0);
        form.Close();

        Console.WriteLine("--- a page without a module or a load-side encoder ---");
        var plain = new Panel { Size = new Size(900, 200) };
        var a1 = new AxisSettingHMI { AxisNameKey = "A", PositionAddress = "D0", Location = new Point(0, 30) };
        plain.Controls.Add(new Label { Text = "Axis", Location = new Point(0, 0), Size = new Size(100, 20) });
        plain.Controls.Add(new Label { Text = "Position", Location = new Point(120, 0), Size = new Size(100, 20) });
        plain.Controls.Add(a1);
        a1.BuildRow();
        Check("only the Position offered", string.Join(",", a1.OfferedPositions()) == "Position");
        ViewPreferences.SetList(ViewPreferences.AxisPositions, new[] { "ServoEncoder", "LoadPosition" });
        a1.BuildRow();
        Check("  picked elsewhere, not here: the Position alone", Keys(a1).Contains("Position") && !Keys(a1).Contains("ServoEncoder") && !Keys(a1).Contains("LoadPosition"), Keys(a1));

        Console.WriteLine("--- an axis unit: the positions picked, side by side in its slot ---");
        var info = new AlarmHelpLinks.AxisInfo { Station = 1, Number = 1, NameKey = "Axis_RobotX", Position = "W0", Divisor = 10000, Decimals = 2, Digits = 6,
                                                  Suffix = "mm", PositionType = DataTypeEnum.Bin_32, LoadSideEncoder = "D22", LoadSideEncoderDivisor = 10000,
                                                  Row = new Dictionary<string, object> { { "MotionNumber", 1 }, { "MotionAxisIndex", 1 } } };
        Check("picked servo and load: the axis's three (the Position first)", string.Join(",", AxisUnitHMI.PositionsOf(info)) == "Position,ServoEncoder,LoadPosition",
              string.Join(",", AxisUnitHMI.PositionsOf(info)));
        info.Row = new Dictionary<string, object>();
        info.LoadSideEncoder = "";
        Check("  an axis without them: the Position", string.Join(",", AxisUnitHMI.PositionsOf(info)) == "Position");
        var uform = new Form { ClientSize = new Size(500, 120) };
        var unit = new CylinderUnitHMI { Size = new Size(360, 64), Location = new Point(10, 10), Kind = UnitKind.Axis, CommandAAddress = "M1", CommandBAddress = "M2", PositionAddress = "W0" };
        uform.Controls.Add(unit);
        uform.Show(); Pump(150);
        var single = unit.PositionDisplay.Bounds;
        var e1 = new NumericDisplayHMI { Name = "posLoad" };
        var e2 = new NumericDisplayHMI { Name = "posOther" };
        unit.SetExtraPositions(new Control[] { e1, e2 });
        Pump(100);
        var p0 = unit.PositionDisplay.Bounds;
        Check("three positions: the slot shared out, side by side, as tall as the position", unit.ExtraPositions.Count == 2 && e1.Visible && e2.Visible &&
              e1.Width == p0.Width && e2.Width == p0.Width && e1.Top == p0.Top && e1.Height == p0.Height && e1.Left > p0.Right && e2.Left > e1.Right &&
              e2.Right - p0.Left > single.Width, p0 + " " + e1.Bounds + " " + e2.Bounds + " (alone " + single + ")");
        Check("  the buttons narrower, still there", unit.ButtonA.Visible && unit.ButtonB.Visible && unit.ButtonA.Right < p0.Left && unit.ButtonB.Left > e2.Right && unit.ButtonA.Width >= 40,
              unit.ButtonA.Bounds + " " + unit.ButtonB.Bounds);
        unit.SetExtraPositions(null);
        Pump(100);
        Check("none again: the position as it was", unit.PositionDisplay.Bounds == single && e1.IsDisposed, unit.PositionDisplay.Bounds + " vs " + single);
        uform.Close();

        Console.WriteLine("--- the Axis Monitor's rows ---");
        info.Row = new Dictionary<string, object> { { "MotionNumber", 1 }, { "MotionAxisIndex", 2 } };
        info.LoadSideEncoder = "D22";
        var mrow = new AxisMonitorRow(info, new[] { "Position", "ServoEncoder", "LoadPosition" });
        var mPos = (NumericDisplayHMI)typeof(AxisMonitorRow).GetField("Position", Any).GetValue(mrow);
        var mServo = (MotionValueHMI)typeof(AxisMonitorRow).GetField("ServoEncoder", Any).GetValue(mrow);
        var mLoad = (NumericDisplayHMI)typeof(AxisMonitorRow).GetField("LoadPosition", Any).GetValue(mrow);
        Check("its three positions side by side, the servo encoder of axis 2 (U1\\G2550)", mPos != null && mServo != null && mLoad != null &&
              mServo.Address == @"U1\G2550" && mLoad.Address == "D22" && mServo.Left == mPos.Right + AxisSettingHMI.Gap && mLoad.Left == mServo.Right + AxisSettingHMI.Gap &&
              string.IsNullOrEmpty(mPos.ComparedAddress), mServo == null ? "none" : mServo.Address);

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "AXIS POSITIONS ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
