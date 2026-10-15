using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// The axis row: the current beside the position, the Z distance beside the
// reference selection, narrow tall meters; the real position in the position's
// tip; travel per revolution and backlash at the end; the page's headings over
// the columns. And the hover tips of numeric fields and lamps, shown on every visit.
class AR {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static void Raise(Control c, string method, EventArgs e) { typeof(Control).GetMethod(method, Any).Invoke(c, new object[] { e }); }
    // The tip is SHOWN by the control once the pointer rests (UiStyle.HoverTip);
    // what it showed, and where, is kept for the tests.
    static void Hover(Control c) {
        typeof(UiStyle).GetField("LastShownTip", Any).SetValue(null, null);
        typeof(UiStyle).GetField("LastShownOn", Any).SetValue(null, null);
        Raise(c, "OnMouseEnter", EventArgs.Empty);
        Raise(c, "OnMouseMove", new MouseEventArgs(MouseButtons.None, 0, 3, 3, 0));
        Pump(UiStyle.TooltipInitialDelayMs + 250);
    }
    static string Tip(Control c) {
        return typeof(UiStyle).GetField("LastShownOn", Any).GetValue(null) == c
             ? (string)typeof(UiStyle).GetField("LastShownTip", Any).GetValue(null) : null;
    }
    static void Leave(Control c) { Raise(c, "OnMouseLeave", EventArgs.Empty); }

    static AxisSettingHMI Full(string name, string extra = "") {
        var a = new AxisSettingHMI {
            AxisNameKey = name, CommandBackAddress = "M1", CommandForwardAddress = "M2", PositionAddress = "W0",
            MinLimitAddress = "D2", MaxLimitAddress = "D4", ReferenceAddress = "D6", CalibrationAddress = "D8",
            CalibrateAddress = "M3", GoToReferenceAddress = "M4", CurrentAddress = "W0A", ZDistanceAddress = "D73",
            AxisAlarmAddress = "W4", AxisWarningAddress = "W5", ServoAlarmAddress = "W0B" };
        if (extra.Contains("real")) a.LoadSideEncoderAddress = "W10";
        if (extra.Contains("travel")) a.TravelPerRevolutionAddress = "D100";
        if (extra.Contains("backlash")) a.BacklashAddress = "D102";
        return a;
    }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        SecurityManager.Login("admin", SecurityLevel.Admin);

        Console.WriteLine("--- the row, as asked ---");
        var row = Full("Axis_RobotX");
        row.BuildRow();
        var col = row.Columns;
        var pos = row.Controls.OfType<NumericDisplayHMI>().Single();
        var meters = row.Controls.OfType<MeterHMI>().OrderBy(m => m.Left).ToList();
        var jogs = row.Controls.OfType<ButtonHMI>().Where(b => b.BitActions.Any(x => x.Action == BitActionType.Momentary)).OrderBy(b => b.Left).ToList();
        var refBox = row.Controls.OfType<CheckBoxHMI>().Single();
        Check("current right beside the position, before the forward jog",
              meters.Count == 2 && meters[0].Left == pos.Right + AxisSettingHMI.Gap && meters[0].Right < jogs[1].Left,
              pos.Bounds + " | " + (meters.Count > 0 ? meters[0].Bounds.ToString() : "-") + " | " + jogs[1].Bounds);
        Check("Z distance right beside the reference selection",
              meters.Count == 2 && meters[1].Left == refBox.Right + AxisSettingHMI.Gap,
              refBox.Bounds + " | " + (meters.Count > 1 ? meters[1].Bounds.ToString() : "-"));
        Check("meters narrower (76) and as tall as the position and the checkbox",
              meters.All(m => m.Width == 76 && m.Height == pos.Height && m.Top == pos.Top && m.Height == refBox.Height),
              string.Join(" ", meters.Select(m => m.Size.ToString())) + " vs " + pos.Size);
        var order = col.OrderBy(kv => kv.Value.X).Select(kv => kv.Key).ToList();
        Check("column order", string.Join(",", order) ==
              "Axis,Position,Current,Min,Max,Reference,Calibration,Cal,Ref,ZDistance,AxisAlarm,AxisWarning,ServoAlarm", string.Join(",", order));

        Console.WriteLine("--- the real position in the position's tip ---");
        var r2 = Full("Axis_RobotX", "real");
        r2.BuildRow();
        var p2 = r2.Controls.OfType<NumericDisplayHMI>().Single();
        Check("the position compares with LoadSideEncoderAddress", p2.ComparedAddress == "W10", p2.ComparedAddress);
        var mem = new Dictionary<string, int> { { "W0", 81849000 & 0xFFFF }, { "W1", 81849000 >> 16 }, { "W10", 81848988 & 0xFFFF }, { "W11", 81848988 >> 16 } };
        p2.UpdateState(a => mem.ContainsKey(a) ? mem[a] : 0, c => true);
        var build = typeof(BaseNumericHMI).GetMethod("BuildTooltip", Any);
        var tip = (string)build.Invoke(p2, null);
        var lines = tip.Split('\n');
        // 30 September: in mm, to the last count (divisor 10000: 4 decimals), not raw counts.
        Check("first line: the load encoder value in mm, to its last count: 8184.8988 mm", lines[0].StartsWith("Load encoder value:") && lines[0].EndsWith("\t8184.8988 mm"), lines[0]);
        Check("second line: the difference in mm: +0.0012 mm (12 counts)", lines.Length > 1 && lines[1].StartsWith("Difference:") && lines[1].EndsWith("\t+0.0012 mm"),
              lines.Length > 1 ? lines[1] : "");
        Check("  then the usual notes", tip.Contains("Address:"), tip.Replace("\n", " | "));
        Check("no load encoder, no such lines", !((string)build.Invoke(pos, null)).Contains("Load encoder"));
        var devs = DeviceCollector.FromControl(r2, 1);
        Check("the device list names it", devs.Any(d => d.Address == "W10" && d.UsedAs.Contains("load-side encoder")),
              string.Join(" ", devs.Select(d => d.Address)));

        // 15 October: the load-side encoder is read by the PLC itself, in its own count - its own divisor
        // (FA2010's Robot X: D22 in 0.001 mm), while the position is divided by the row's Divisor (0.0001 mm)
        Console.WriteLine("--- the load-side encoder in its own count ---");
        var ls3 = Full("Axis_RobotX", "real");
        ls3.LoadSideEncoderDivisor = 1000;
        ls3.BuildRow();
        var lp3 = ls3.Controls.OfType<NumericDisplayHMI>().Single();
        Check("the position divided by the row's Divisor (10000), the encoder by its own (1000)", lp3.Divisor == 10000 && lp3.ComparedDivisor == 1000,
              lp3.Divisor + " / " + lp3.ComparedDivisor);
        var lmem3 = new Dictionary<string, int> { { "W0", 81849000 & 0xFFFF }, { "W1", 81849000 >> 16 }, { "W10", 8184899 & 0xFFFF }, { "W11", 8184899 >> 16 } };
        lp3.UpdateState(a => lmem3.ContainsKey(a) ? lmem3[a] : 0, c => true);
        var ll3 = ((string)build.Invoke(lp3, null)).Split('\n');
        Check("  8184899 (0.001 mm) reads 8184.8990 mm - to the finer one's last count", ll3[0].EndsWith("\t8184.8990 mm"), ll3[0]);
        Check("  against the position's 8184.9000 mm: +0.0010 mm", ll3.Length > 1 && ll3[1].EndsWith("\t+0.0010 mm"), ll3.Length > 1 ? ll3[1] : "");
        Check("  the position itself as before: 8184.90 mm", lp3.Text == "8184.90 mm", lp3.Text);
        Check("a divisor of its own does not change the position's", new AxisSettingHMI { Divisor = 100000, LoadSideEncoderDivisor = 1000 }.PositionScale == 100000);

        // 15 October: "make LoadSideEncoderDivisor get its value from Divisor when it is null, so the user
        // wouldn't have to enter it again when they are the same"
        Console.WriteLine("--- the encoder's divisor follows the Divisor until it is set ---");
        var fol = new AxisSettingHMI { Divisor = 10000 };
        var lsd = TypeDescriptor.GetProperties(fol)["LoadSideEncoderDivisor"];
        Check("not set: it reads the Divisor (10000) - not written into the page, not bold",
              fol.LoadSideEncoderDivisor == 10000 && fol.LoadSideEncoderScale == 10000 && !lsd.ShouldSerializeValue(fol) && !fol.HasOwnLoadSideEncoderDivisor);
        fol.Divisor = 100000;
        Check("  and follows it when the Divisor changes (100000)", fol.LoadSideEncoderDivisor == 100000, fol.LoadSideEncoderDivisor.ToString());
        fol.LoadSideEncoderDivisor = 1000;
        Check("set: its own (1000), written into the page", fol.LoadSideEncoderDivisor == 1000 && lsd.ShouldSerializeValue(fol) && fol.Divisor == 100000);
        Check("  the Properties window can reset it", lsd.CanResetValue(fol));
        lsd.ResetValue(fol);
        Check("  reset: the Divisor again", fol.LoadSideEncoderDivisor == 100000 && !lsd.ShouldSerializeValue(fol), fol.LoadSideEncoderDivisor.ToString());
        fol.LoadSideEncoderDivisor = 1000; fol.LoadSideEncoderDivisor = 0;
        Check("  0: the Divisor again", fol.LoadSideEncoderDivisor == 100000 && !fol.HasOwnLoadSideEncoderDivisor);
        var flr = Full("Axis_RobotX", "real");
        flr.BuildRow();
        Check("a row with an encoder and no divisor of its own: the tooltip divides it by the Divisor",
              flr.Controls.OfType<NumericDisplayHMI>().Single().ComparedDivisor == flr.Divisor);

        Console.WriteLine("--- travel per revolution and backlash ---");
        Check("none given: no fields, no columns", !col.ContainsKey("TravelPerRev") && !col.ContainsKey("Backlash") &&
              row.Controls.OfType<NumericInputHMI>().Count() == 4);
        var r3 = Full("Axis_RobotY", "travel backlash");
        r3.BuildRow();
        var extra = r3.Controls.OfType<NumericInputHMI>().Where(n => n.Address == "D100" || n.Address == "D102").OrderBy(n => n.Left).ToList();
        var servo = r3.Controls.OfType<HexCodeHMI>().Max(h => h.Right);
        Check("both at the end of the row, after the drive codes", extra.Count == 2 && extra.All(n => n.Left > servo), servo + " | " + string.Join(" ", extra.Select(n => n.Left)));
        Check("  in the axis's unit to its last count: its divisor, 4 decimals, mm shown",
              extra.All(n => n.Divisor == r3.Divisor && n.DecimalPlaces == 4 && n.UnitType == r3.PositionUnitType && n.ShowSuffix && n.Suffix == "mm"),
              string.Join(" ", extra.Select(n => n.Divisor + "/" + n.DecimalPlaces + "/" + n.Suffix)));
        var bmem = new Dictionary<string, int> { { "D100", 50000 }, { "D102", 150 } };
        foreach (var n in extra) n.UpdateState(a => bmem.ContainsKey(a) ? bmem[a] : 0, c => true);
        Check("  a backlash of 150 counts reads 0.0150 mm, a travel of 50000 counts 5.0000 mm",
              extra[1].Text == "0.0150 mm" && extra[0].Text == "5.0000 mm", extra[0].Text + " | " + extra[1].Text);
        Check("  their tips say so", extra[0].ToolTipTextKey.Contains("last count") && extra[1].ToolTipTextKey.StartsWith("Backlash"), extra[0].ToolTipTextKey);

        Console.WriteLine("--- a page of rows: the headings follow the columns ---");
        var page = new UserControl { Size = new Size(1500, 300) };
        Func<string, int, Label> head = (t, x) => { var l = new Label { Text = t, Location = new Point(x, 10), Size = new Size(60, 18) }; page.Controls.Add(l); return l; };
        var hAxis = head("Axis", 8); var hPos = head("Position", 200); var hCur = head("Current", 860); var hZ = head("Z distance", 960);
        var hMin = head("Min", 392); var hBack = head("Backlash", 78);
        var a1 = Full("Axis_RobotX"); a1.Location = new Point(0, 34);
        var a2 = Full("Axis_RobotY", "backlash"); a2.Location = new Point(0, 78);
        page.Controls.Add(a1); page.Controls.Add(a2);
        var form = new Form { ClientSize = new Size(1500, 300) }; form.Controls.Add(page); form.Show(); Pump(100);
        if (a1.Controls.Count == 0) a1.BuildRow();
        if (a2.Controls.Count == 0) a2.BuildRow();
        a1.AlignHeaders();
        Func<Label, string, bool> over = (l, key) => a1.Columns.ContainsKey(key) && l.Visible && l.Left == a1.Left + a1.Columns[key].X && l.Width == a1.Columns[key].Y;
        Check("Current, Z distance, Min, Position: each over its column", over(hCur, "Current") && over(hZ, "ZDistance") && over(hMin, "Min") && over(hPos, "Position"),
              hCur.Bounds + " / " + (a1.Columns.ContainsKey("Current") ? a1.Columns["Current"].ToString() : "-"));
        Check("an axis with backlash on the page: every row has its column, the heading over it", a1.Columns.ContainsKey("Backlash") && over(hBack, "Backlash"),
              hBack.Bounds.ToString());
        Check("  the row without it has no field there", !a1.Controls.OfType<NumericInputHMI>().Any(n => n.Address == "D102") &&
              a2.Controls.OfType<NumericInputHMI>().Any(n => n.Address == "D102"));
        Check("  headings translated: 'Z distance' reads 'Z dist'", hZ.Text == LanguageManager.GetString("Col_ZDistance", "Z distance"), hZ.Text);
        var none = new UserControl { Size = new Size(1500, 300) };
        var nCur = new Label { Text = "Current", Location = new Point(860, 10), Size = new Size(60, 18) }; none.Controls.Add(nCur);
        var b1 = new AxisSettingHMI { AxisNameKey = "X1", CommandBackAddress = "M1", CommandForwardAddress = "M2", PositionAddress = "W0", MinLimitAddress = "D2", Location = new Point(0, 34) };
        none.Controls.Add(b1); b1.BuildRow();
        Check("a page with no current (H800): no current column, its heading hidden", !b1.Columns.ContainsKey("Current") && !nCur.Visible);
        var bj = b1.Controls.OfType<ButtonHMI>().OrderBy(b => b.Left).ToList();
        var bp = b1.Controls.OfType<NumericDisplayHMI>().Single();
        Check("  and no gap between the position and the forward jog", bj[1].Left == bp.Right + AxisSettingHMI.Gap, bp.Bounds + " | " + bj[1].Bounds);
        form.Close();

        Console.WriteLine("--- tips on every visit (numeric fields, lamps) ---");
        var host = new Form { ClientSize = new Size(400, 200) };
        var num = new NumericInputHMI { Address = "D10", Divisor = 1, MinLimit = new Kaban.Core.Models.DynamicValue(LimitSourceType.Static, 0, ""),
                                        MaxLimit = new Kaban.Core.Models.DynamicValue(LimitSourceType.Static, 100, ""), Location = new Point(10, 10) };
        var disp = new NumericDisplayHMI { Address = "D12", Location = new Point(10, 60) };
        var lamp = new LampHMI { Address = "X1", DescriptionText = "Door closed", Location = new Point(10, 110) };
        host.Controls.AddRange(new Control[] { num, disp, lamp });
        host.Show(); Pump(80);
        var tips = UiStyle.Tips();
        Hover(num);
        Check("numeric input: its tip shown once the pointer rests", (Tip(num) ?? "").Contains("Address:"), Tip(num));
        Leave(num);
        tips.SetToolTip(num, null);                    // what a closed keypad or dialog did to it
        Hover(num);
        Check("  lost meanwhile: shown again on the next visit", (Tip(num) ?? "").Length > 0);
        Leave(num);
        Hover(disp);
        Check("numeric display too", (Tip(disp) ?? "").Contains("Address:"));
        Leave(disp);
        SecurityManager.Login("op", SecurityLevel.Operator);
        Hover(num);
        Check("an operator sees what may be typed", Tip(num) == "Allowed: 0 - 100", Tip(num));
        Leave(num);
        Hover(lamp);
        Check("lamp: its caption and state for an operator", Tip(lamp) == "Door closed: Off", Tip(lamp));
        using (var bmp = new Bitmap(lamp.Width, lamp.Height)) {
            lamp.DrawToBitmap(bmp, new Rectangle(0, 0, lamp.Width, lamp.Height));
            int blue = 0;
            for (int x = 0; x < bmp.Width; x++) for (int y = 0; y < bmp.Height; y++) {
                var c = bmp.GetPixel(x, y); if (c.B > 150 && c.R < 110 && c.G > 80 && c.G < 170) blue++; }
            Check("  its ring turns blue under the pointer", blue > 8, blue + " blue pixels");
        }
        Leave(lamp);
        using (var bmp = new Bitmap(lamp.Width, lamp.Height)) {
            lamp.DrawToBitmap(bmp, new Rectangle(0, 0, lamp.Width, lamp.Height));
            int blue = 0;
            for (int x = 0; x < bmp.Width; x++) for (int y = 0; y < bmp.Height; y++) {
                var c = bmp.GetPixel(x, y); if (c.B > 150 && c.R < 110 && c.G > 80 && c.G < 170) blue++; }
            Check("  and back when it leaves", blue == 0, blue + " blue pixels");
        }
        SecurityManager.Login("admin", SecurityLevel.Admin);
        Hover(lamp);
        Check("  an admin also sees its wiring", (Tip(lamp) ?? "").Contains("Address:"));
        Leave(lamp);
        host.Close();

        Console.WriteLine(fail == 0 ? "\nAXIS ROW ALL PASS" : "\nAXIS ROW " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
