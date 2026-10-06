using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// "We should add a View Preferences tab to the HMI Settings menu to set the UI
// according to the user's wish (Left / Right alignment of the Description text,
// the axis name ...). Some preferences in the user settings, set automatically
// when the user signs in, just like the screen scale."
// "If MotionModuleNumber and ModuleAxisIndex (since: MotionNumber, MotionAxisIndex) are entered on the axis, we read
// the related buffer addresses (Un\G) - a multiple selection on top of the axis
// setting page for the values to show (axis alarm, warning codes, home request,
// real position, ready / busy flags ...)."
class VPM {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }
    static string Missing = "##missing##";
    // A picture of what is on the (virtual) screen - DrawToBitmap leaves Mono's children out.
    static void Snap(Control c, int height, string path) {
        Pump(150);
        var at = c.PointToScreen(Point.Empty);
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("import",
            "-window root -crop " + c.Width + "x" + height + "+" + at.X + "+" + at.Y + " " + path) { UseShellExecute = false });
        p.WaitForExit(10000);
    }
    static bool Has(string key) { return LanguageManager.GetString(key, Missing) != Missing; }

    static AxisSettingHMI Row(string name, int top, int module, int axis) {
        var a = new AxisSettingHMI {
            AxisNameKey = name, CommandBackAddress = "M1", CommandForwardAddress = "M2", PositionAddress = "W0",
            MinLimitAddress = "D2", MaxLimitAddress = "D4", DecimalPlaces = 2,
            MotionNumber = module, MotionAxisIndex = axis };
        a.Location = new Point(10, top);
        return a;
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "vpm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        ViewPreferences.Reload();
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        SecurityManager.Logout();
        int changed = 0;
        ViewPreferences.Changed += (s, e) => changed++;
        var t = new DateTime(2026, 10, 2, 14, 31, 7, 250);

        Console.WriteLine("--- the defaults: the screen as it was ---");
        Check("every preference at its default", ViewPreferences.All.All(p => ViewPreferences.Get(p.Key) == p.Default));
        Check("captions right, axis names right, blinking, silent",
              !ViewPreferences.CaptionsLeft && !ViewPreferences.AxisNamesLeft && ViewPreferences.BlinkingOn && !ViewPreferences.AlarmSoundOn);
        Check("numbers as Windows writes them",
              ViewPreferences.NumberFormat.NumberDecimalSeparator == System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        Check("dates as before: 2026-10-02 14:31:07", ViewPreferences.DateTimeText(t) == "2026-10-02 14:31:07", ViewPreferences.DateTimeText(t));
        Check("tables 26 px, 9.5 pt; tooltips after 1 s",
              ViewPreferences.TableRowHeight == 26 && Math.Abs(ViewPreferences.TableFontSize - 9.5f) < 0.01f && ViewPreferences.TooltipDelayMs == 1000);
        Check("no file written for the defaults", !File.Exists(ViewPreferences.SettingsPath));

        Console.WriteLine("--- everyone's ---");
        changed = 0;
        ViewPreferences.SetPanel(ViewPreferences.DecimalSeparator, "Comma");
        Check("kept in View.cfg", File.Exists(ViewPreferences.SettingsPath) &&
              File.ReadAllText(ViewPreferences.SettingsPath).Contains("Panel.DecimalSeparator=Comma"));
        Check("  and told (Changed)", changed == 1, changed.ToString());
        Check("a number with a comma", PlcValueConverter.FormatString(12.5, DataTypeEnum.Bin_16, DisplayFormatEnum.Signed_Decimal, 2, 5, false).Contains("12,50"),
              PlcValueConverter.FormatString(12.5, DataTypeEnum.Bin_16, DisplayFormatEnum.Signed_Decimal, 2, 5, false));
        ViewPreferences.SetPanel(ViewPreferences.DecimalSeparator, "Point");
        Check("with a point", PlcValueConverter.FormatString(12.5, DataTypeEnum.Bin_16, DisplayFormatEnum.Signed_Decimal, 2, 5, false).Contains("12.50"));
        ViewPreferences.SetPanel(ViewPreferences.DateFormat, "Bogus");
        Check("a value it does not know: the default", ViewPreferences.Get(ViewPreferences.DateFormat) == "Iso");
        ViewPreferences.SetPanel(ViewPreferences.DecimalSeparator, null);
        Check("null: back to the default", ViewPreferences.PanelValue(ViewPreferences.DecimalSeparator) == "Windows");

        Console.WriteLine("--- a user's own, taken when they sign in ---");
        ViewPreferences.SetMany("ali", new Dictionary<string, string> {
            { ViewPreferences.DateFormat, "European" }, { ViewPreferences.TimeFormat, "H12" }, { ViewPreferences.DecimalSeparator, "Comma" } });
        Check("signed out: everyone's", ViewPreferences.DateText(t) == "2026-10-02" && ViewPreferences.ActiveUser == null);
        changed = 0;
        SecurityManager.Login("ali", SecurityLevel.Operator);
        Check("signing in tells (Changed)", changed >= 1, changed.ToString());
        Check("ali's: 02.10.2026  2:31:07 PM", ViewPreferences.DateText(t) == "02.10.2026" && ViewPreferences.TimeText(t) == "2:31:07 PM",
              ViewPreferences.DateText(t) + " " + ViewPreferences.TimeText(t));
        Check("  with the fraction a log wants", ViewPreferences.DateTimeText(t, true, 3) == "02.10.2026 2:31:07.250 PM", ViewPreferences.DateTimeText(t, true, 3));
        Check("  and the comma", ViewPreferences.DecimalMark == ",");
        SecurityManager.Login("veli", SecurityLevel.Operator);
        Check("another user, without their own: everyone's", ViewPreferences.DateText(t) == "2026-10-02" && ViewPreferences.DecimalMark != "," ||
              System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == ",");
        SecurityManager.Logout();
        Check("signed out: everyone's again", ViewPreferences.DateText(t) == "2026-10-02");
        Check("kept per user in the file", File.ReadAllText(ViewPreferences.SettingsPath).Contains("User=ali;DateFormat=European"));
        ViewPreferences.Reload();
        Check("read back", ViewPreferences.UserValue("ali", ViewPreferences.TimeFormat) == "H12" && ViewPreferences.UsersWithOwn().Contains("ali"));
        ViewPreferences.SetUserPlace("ali", new Dictionary<string, string> { { "Page", "Settings" }, { "Tab", "Users" } });
        Check("the user's last place, kept", ViewPreferences.UserPlace("ali")["Tab"] == "Users");
        ViewPreferences.RemoveUser("ali");
        Check("a user deleted: their values go", ViewPreferences.UserValue("ali", ViewPreferences.DateFormat) == null && !ViewPreferences.UsersWithOwn().Contains("ali"));

        Console.WriteLine("--- captions and axis names to the left ---");
        var form = new Form { ClientSize = new Size(1260, 640), BackColor = Color.White, StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var host = new Panel { Bounds = new Rectangle(0, 0, 600, 200), BackColor = Color.White };
        form.Controls.Add(host);
        var n1 = new NumericInputHMI { Address = "D10", Location = new Point(260, 20), Size = new Size(120, 28), DescriptionText = "Saw diameter" };
        var n2 = new NumericInputHMI { Address = "D12", Location = new Point(260, 60), Size = new Size(120, 28), DescriptionText = "Speed" };
        host.Controls.Add(n1); host.Controls.Add(n2);
        form.Show(); Pump(150);
        Func<string, Label> cap = text => host.Controls.OfType<Label>().FirstOrDefault(l => (l.Text ?? "").StartsWith(text));
        var c1 = cap("Saw diameter"); var c2 = cap("Speed");
        Check("right: each against its field", c1 != null && c2 != null && c1.TextAlign == ContentAlignment.MiddleRight &&
              Math.Abs(c1.Right - n1.Left) <= 10 && Math.Abs(c2.Right - n2.Left) <= 10,
              c1 == null ? "no caption" : c1.Bounds + " " + c2.Bounds);
        ViewPreferences.SetPanel(ViewPreferences.CaptionAlign, "Left");
        Pump(100);
        Check("left: one column, the fields' captions start together",
              c1.TextAlign == ContentAlignment.MiddleLeft && c2.TextAlign == ContentAlignment.MiddleLeft && c1.Left == c2.Left && c1.Right <= n1.Left && c2.Right <= n2.Left,
              c1.Bounds + " " + c2.Bounds);
        ViewPreferences.SetPanel(ViewPreferences.CaptionAlign, null);
        Pump(100);
        Check("back to the right", c1.TextAlign == ContentAlignment.MiddleRight && Math.Abs(c2.Right - n2.Left) <= 10, c2.Bounds.ToString());

        var single = Row("Axis_X", 120, 0, 0);
        host.Controls.Add(single); Pump(100);
        var axisName = (Label)typeof(AxisSettingHMI).GetField("_axis", Any).GetValue(single);
        Check("axis name: right by default", axisName.TextAlign == ContentAlignment.MiddleRight);
        ViewPreferences.SetPanel(ViewPreferences.CaptionAlign, "Left");
        Pump(100);
        Check("  left with the captions (one preference)", axisName.TextAlign == ContentAlignment.MiddleLeft);
        ViewPreferences.SetPanel(ViewPreferences.CaptionAlign, null);
        Pump(50);
        host.Controls.Remove(single);

        Console.WriteLine("--- the keypad ---");
        ViewPreferences.SetPanel(ViewPreferences.DecimalSeparator, "Comma");
        using (var pad = new NumpadForm("x", "12,5", 0, 100)) {
            var keys = new List<Button>();
            Action<Control> walk = null; walk = c => { foreach (Control k in c.Controls) { if (k is Button) keys.Add((Button)k); walk(k); } };
            walk(pad);
            Check("the decimal key shows the comma", keys.Any(b => b.Text == ",") && !keys.Any(b => b.Text == "."), string.Join(" ", keys.Select(b => b.Text)));
            Check("  and still works as the point", pad.Value == "12.5", pad.Value);
        }
        ViewPreferences.SetPanel(ViewPreferences.DecimalSeparator, "Point");
        using (var pad = new NumpadForm("x", "1", 0, 100)) {
            var keys = new List<Button>();
            Action<Control> walk = null; walk = c => { foreach (Control k in c.Controls) { if (k is Button) keys.Add((Button)k); walk(k); } };
            walk(pad);
            Check("with Point: the point", keys.Any(b => b.Text == "."));
        }
        ViewPreferences.SetPanel(ViewPreferences.DecimalSeparator, null);

        Console.WriteLine("--- tables, tooltips, blinking ---");
        var grid = new DataGridView { Bounds = new Rectangle(620, 10, 300, 180) };
        grid.Columns.Add("a", "A");
        grid.Rows.Add("1"); grid.Rows.Add("2");
        form.Controls.Add(grid);
        UiStyle.GridLook(grid);
        Pump(50);
        Check("a table: 26 px rows, the body font", grid.RowTemplate.Height == 26 && grid.Rows[0].Height == 26 && Math.Abs(grid.Font.Size - 9.5f) < 0.1f,
              grid.RowTemplate.Height + " " + grid.Rows[0].Height + " " + grid.Font.Size);
        ViewPreferences.SetMany(null, new Dictionary<string, string> { { ViewPreferences.TableRows, "Large" }, { ViewPreferences.TableFont, "Large" } });
        Pump(50);
        Check("Large: the open table at once - 32 px, 11 pt", grid.RowTemplate.Height == 32 && grid.Rows[0].Height == 32 && Math.Abs(grid.Font.Size - 11f) < 0.1f,
              grid.RowTemplate.Height + " " + grid.Rows[0].Height + " " + grid.Font.Size);
        ViewPreferences.SetMany(null, new Dictionary<string, string> { { ViewPreferences.TableRows, "Compact" }, { ViewPreferences.TableFont, null } });
        Pump(50);
        Check("Compact: 22 px", grid.Rows[1].Height == 22 && Math.Abs(grid.Font.Size - 9.5f) < 0.1f, grid.Rows[1].Height.ToString());
        ViewPreferences.SetPanel(ViewPreferences.TableRows, null);
        var tips = UiStyle.Tips();
        ViewPreferences.SetPanel(ViewPreferences.TooltipDelay, "Long");
        Check("tooltips: 2 s", tips.InitialDelay == 2000 && UiStyle.CreateTips().InitialDelay == 2000, tips.InitialDelay.ToString());
        ViewPreferences.SetPanel(ViewPreferences.TooltipDelay, "Short");
        Check("  0.4 s", tips.InitialDelay == 400, tips.InitialDelay.ToString());
        ViewPreferences.SetPanel(ViewPreferences.TooltipDelay, null);
        Check("  1 s again", tips.InitialDelay == 1000, tips.InitialDelay.ToString());
        bool sawOff = false, sawOn = false;
        for (int i = 0; i < 60; i++) { if (BlinkHelper.IsBlinkActive) sawOn = true; else sawOff = true; System.Threading.Thread.Sleep(25); }
        Check("blinking: on and off within 1.5 s", sawOn && sawOff);
        ViewPreferences.SetPanel(ViewPreferences.Blinking, "Off");
        sawOff = false;
        for (int i = 0; i < 60; i++) { if (!BlinkHelper.IsBlinkActive) sawOff = true; System.Threading.Thread.Sleep(25); }
        Check("Steady: always shown", !sawOff);
        ViewPreferences.SetPanel(ViewPreferences.Blinking, null);

        Console.WriteLine("--- a sound for a new alarm ---");
        int rang = 0;
        AlarmSound.Play = () => rang++;
        var standing = new AlarmItem("T_STAND", "x", AlarmSeverity.Fault);
        AlarmManager.RaiseGenerated(standing);
        AlarmSound.Start();
        var a1 = new AlarmItem("T_ONE", "x", AlarmSeverity.Fault);
        AlarmManager.RaiseGenerated(a1);
        Check("Off (the default): silent", rang == 0, rang.ToString());
        ViewPreferences.SetPanel(ViewPreferences.AlarmSound, "On");
        var a2 = new AlarmItem("T_TWO", "x", AlarmSeverity.Warning);
        AlarmManager.RaiseGenerated(a2);
        Check("On: once for a new alarm", rang == 1, rang.ToString());
        AlarmManager.ClearGenerated(a1);
        AlarmManager.RaiseGenerated(a2);
        Check("  not for one going, nor for one staying", rang == 1, rang.ToString());
        AlarmManager.ClearGenerated(a2); AlarmManager.ClearGenerated(standing);
        ViewPreferences.SetPanel(ViewPreferences.AlarmSound, null);

        Console.WriteLine("--- the simple motion module's memory ---");
        Func<string, SimpleMotion.Item> item = SimpleMotion.Find;
        Check("axis error: G2406 for axis 1, G2606 for axis 3", SimpleMotion.AddressOf(item("AxisError"), 1) == 2406 && SimpleMotion.AddressOf(item("AxisError"), 3) == 2606);
        Check("BUSY: one word for all axes, G31501", SimpleMotion.AddressOf(item("Busy"), 1) == 31501 && SimpleMotion.AddressOf(item("Busy"), 5) == 31501);
        Check("travel per rotation of axis 2: G154 (Pr.3, 150 words an axis)", SimpleMotion.AddressOf(item("TravelPerRevolution"), 2) == 154);
        Check("the device: U1\\G2406", SimpleMotion.Device(1, 2406) == @"U1\G2406");
        Check("the next word of a module address", PlcAddressHelper.GetNextAddress(@"U1\G2400") == @"U1\G2401" &&
              PlcAddressHelper.GetNextAddress(@"U3E0\G10") == @"U3E0\G11" && PlcAddressHelper.GetNextAddress("D100") == "D101",
              PlcAddressHelper.GetNextAddress(@"U1\G2400"));
        Check("every value with its title, short heading, in the 6 languages' file",
              SimpleMotion.Items.All(i => Has(i.TitleKey) && Has("MotH_" + i.Key)), string.Join(",", SimpleMotion.Items.Where(i => !Has(i.TitleKey) || !Has("MotH_" + i.Key)).Select(i => i.Key)));
        Check("the status texts too", Enumerable.Range(-4, 18).All(n => Has("MotSt_" + (n < 0 ? "m" + (-n) : n.ToString()))));
        ViewPreferences.SetPanel(ViewPreferences.DecimalSeparator, "Point");
        Check("a position in mm: 123.46 mm", SimpleMotion.Format(item("RealCurrentValue"), 1234567 & 0xFFFF, 1234567 >> 16, 0, 1, 2) == "123.46 mm",
              SimpleMotion.Format(item("RealCurrentValue"), 1234567 & 0xFFFF, 1234567 >> 16, 0, 1, 2));
        Check("  negative", SimpleMotion.Format(item("RealCurrentValue"), -50000 & 0xFFFF, (-50000 >> 16) & 0xFFFF, 0, 1, 2) == "-5.00 mm",
              SimpleMotion.Format(item("RealCurrentValue"), -50000 & 0xFFFF, (-50000 >> 16) & 0xFFFF, 0, 1, 2));
        Check("travel per rotation to its last count: 5.0000 mm", SimpleMotion.Format(item("TravelPerRevolution"), 50000, 0, 0, 1, 2) == "5.0000 mm",
              SimpleMotion.Format(item("TravelPerRevolution"), 50000, 0, 0, 1, 2));
        Check("in pulses when the axis is set to pulses", SimpleMotion.Format(item("RealCurrentValue"), 1500, 0, 3, 1, 2) == "1500 pulse");
        Check("a speed: 1500.00 mm/min", SimpleMotion.Format(item("CurrentSpeed"), 150000 & 0xFFFF, 150000 >> 16, 0, 1, 2) == "1500.00 mm/min",
              SimpleMotion.Format(item("CurrentSpeed"), 150000 & 0xFFFF, 150000 >> 16, 0, 1, 2));
        Check("an error code in hex: 1A18", SimpleMotion.Format(item("AxisError"), 0x1A18, 0, 0, 1) == "1A18" && SimpleMotion.Format(item("AxisError"), 0, 0, 0, 1) == "0");
        Check("the status in words: Home return (7), Error (-1)",
              SimpleMotion.Format(item("OperationStatus"), 7, 0, 0, 1) == "Home return (7)" && SimpleMotion.Format(item("OperationStatus"), -1 & 0xFFFF, 0, 0, 1) == "Error (-1)");
        Check("BUSY of axis 3: bit 2", SimpleMotion.Format(item("Busy"), 4, 0, 0, 3) == "ON" && SimpleMotion.Format(item("Busy"), 4, 0, 0, 2) == "OFF");
        ViewPreferences.SetPanel(ViewPreferences.DecimalSeparator, "Comma");
        Check("with the comma: 123,46 mm", SimpleMotion.Format(item("RealCurrentValue"), 1234567 & 0xFFFF, 1234567 >> 16, 0, 1, 2) == "123,46 mm");
        ViewPreferences.SetPanel(ViewPreferences.DecimalSeparator, "Point");

        Console.WriteLine("--- the Axis Settings page: the pick and the columns ---");
        form.Controls.Remove(host); form.Controls.Remove(grid);
        var page = new Panel { Bounds = new Rectangle(0, 0, 1260, 300), BackColor = Color.White, AutoScroll = true };
        form.Controls.Add(page);
        var hAxis = new Label { Text = "Axis", Location = new Point(10, 8), Size = new Size(80, 22), BackColor = Color.FromArgb(240, 242, 245) };
        var hPos = new Label { Text = "Position", Location = new Point(100, 8), Size = new Size(80, 22), BackColor = Color.FromArgb(240, 242, 245) };
        page.Controls.Add(hAxis); page.Controls.Add(hPos);
        var r1 = Row("Axis_X", 34, 1, 2);
        var r2 = Row("Axis_Y", 34 + AxisSettingHMI.RowHeight, 0, 0);
        page.Controls.Add(r1); page.Controls.Add(r2);
        Pump(200);
        var bar = page.Controls.Find("axisMotionBar", true).FirstOrDefault();
        var frame = AxisPageFrame.Of(page);
        Check("a page with an axis on a module: the pick at the top", bar != null && bar.Visible && bar.Top == 8, bar == null ? "none" : bar.Bounds.ToString());
        Check("  the page moved down to make room for it - a heading over the rows now", frame != null && hAxis.Top == 48 && frame.Head.Height == 74 &&
              r1.Parent == frame.Body && r1.Top == 0 && hAxis.Parent == frame.HeadContent, hAxis.Top + " " + r1.Top + " " + (frame == null ? "no frame" : frame.Head.Height.ToString()));
        var pick = bar != null ? bar.Controls["motionPick"] as Kaban.UI.Dashboard.PickList : null;
        Check("  every value of the module in it", pick != null && pick.Items.Count == SimpleMotion.Items.Count);
        Check("  nothing picked: no columns", r1.Controls["motAxisError"] == null && !r1.Columns.Keys.Any(k => k.StartsWith("Mot:")));

        SecurityManager.Login("boss", SecurityLevel.Admin);
        Pump(50);
        var picked = new[] { "AxisError", "OperationStatus", "HomeRequest", "Busy", "RealCurrentValue" };
        ViewPreferences.SetMotionColumnKeys(picked);
        Pump(200);
        Check("picked: kept as the signed-in user's own", ViewPreferences.UserValue("boss", ViewPreferences.MotionColumns) == string.Join(",", picked) &&
              ViewPreferences.PanelValue(ViewPreferences.MotionColumns) == "");
        Check("  a column each on the row", picked.All(k => r1.Controls["mot" + k] != null), string.Join(",", r1.Controls.Cast<Control>().Select(c => c.Name).Where(n => n.StartsWith("mot"))));
        Check("  the error code: U1\\G2506 (axis 2)", ((IPlcBoundControl)r1.Controls["motAxisError"]).Address == @"U1\G2506",
              ((IPlcBoundControl)r1.Controls["motAxisError"]).Address);
        var real = r1.Controls["motRealCurrentValue"] as MotionValueHMI;
        Check("  the real position: U1\\G2550, and the unit setting U1\\G150 read with it",
              real != null && real.Address == @"U1\G2550" && real.ExtraAddresses.Contains(@"U1\G150"));
        Check("a row not on a module: the space, the columns in line",
              r2.Controls["motBusy"] == null && r2.Columns.ContainsKey("Mot:Busy") && r2.Columns["Mot:Busy"] == r1.Columns["Mot:Busy"]);
        var mem = new Dictionary<string, int> {
            { @"U1\G2509", 7 }, { @"U1\G31501", 2 }, { @"U1\G2517", 1 << 3 }, { @"U1\G150", 0 },
            { @"U1\G2550", 1234567 & 0xFFFF }, { @"U1\G2551", 1234567 >> 16 } };
        Func<string, int> look = a => mem.ContainsKey(a) ? mem[a] : 0;
        foreach (var v in r1.Controls.OfType<MotionValueHMI>()) v.UpdateState(look, c => true);
        Check("the values: Home return (7), BUSY on, home request on, 123.46 mm",
              ((MotionValueHMI)r1.Controls["motOperationStatus"]).ShownText == "Home return (7)" && ((MotionValueHMI)r1.Controls["motBusy"]).IsOn &&
              ((MotionValueHMI)r1.Controls["motHomeRequest"]).IsOn && real.ShownText == "123.46 mm",
              ((MotionValueHMI)r1.Controls["motOperationStatus"]).ShownText + " | " + real.ShownText);
        Func<string, Label> head = k => Tree(page).OfType<Label>().FirstOrDefault(l => l.Tag as string == "AxisHeading:Mot:" + k);
        Check("a heading over each column, short", head("Busy") != null && head("Busy").Visible && head("Busy").Text == "BUSY" &&
              head("Busy").Left == r1.Left + r1.Columns["Mot:Busy"].X && head("OperationStatus").Text == "Status",
              head("Busy") == null ? "none" : head("Busy").Bounds + " " + head("Busy").Text);
        Check("  the whole name in its tooltip", Kaban.UI.Controls.UiStyle.TipOf(head("HomeRequest")).StartsWith("Home request (Md.31 b3)"), Kaban.UI.Controls.UiStyle.TipOf(head("HomeRequest")));
        Check("the pick shows what is kept", pick.Selected.SetEquals(picked));

        // A picture for the review.
        Snap(page, r2.Bottom + 6, "/tmp/axis_motion.png");

        pick.Select(new[] { "AxisError", "ServoReady" });
        Pump(200);
        Check("ticking in the pick: kept, in the list's order", ViewPreferences.MotionColumnKeys.SequenceEqual(new[] { "AxisError", "ServoReady" }),
              string.Join(",", ViewPreferences.MotionColumnKeys));
        Check("  the row again: BUSY gone, servo READY there", r1.Controls["motBusy"] == null && r1.Controls["motServoReady"] != null);
        Check("  its heading hidden", head("Busy") == null || !head("Busy").Visible);
        SecurityManager.Login("veli", SecurityLevel.Operator);
        Pump(200);
        Check("another user (nothing picked): no columns, nothing ticked", r1.Controls["motAxisError"] == null && pick.Selected.Count == 0);
        SecurityManager.Login("boss", SecurityLevel.Admin);
        Pump(200);
        Check("boss again: his columns back", r1.Controls["motServoReady"] != null && pick.Selected.SetEquals(new[] { "AxisError", "ServoReady" }));

        var plain = new Panel { Bounds = new Rectangle(0, 310, 1260, 120) };
        form.Controls.Add(plain);
        var p1 = Row("Axis_Z", 10, 0, 0);
        plain.Controls.Add(p1);
        Pump(100);
        var plainItems = plain.Controls.Find("axisItemsBar", true).FirstOrDefault() as AxisPickBar;
        var plainMotion = plain.Controls.Find("axisMotionBar", true).FirstOrDefault() as AxisPickBar;
        var plainFrame = AxisPageFrame.Of(plain);
        Check("a page without a module: the PLC axis items only, no Motion monitor, no columns", plainItems != null && plainItems.Visible &&
              (plainMotion == null || !plainMotion.Visible) && p1.Controls["motAxisError"] == null && plainFrame != null && plainFrame.Head.Height == 50 && p1.Top == 0,
              plainItems == null ? "no bar" : p1.Top.ToString());
        form.Controls.Remove(page); form.Controls.Remove(plain);
        SecurityManager.Logout();

        Check("the simple motion texts translated", new[] { "Mot_Pick", "Mot_PickNone" }.All(Has));
        SecurityManager.Logout();
        form.Close();

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nVIEW PREFS AND MOTION ALL PASS" : "\nVIEW PREFS AND MOTION " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
