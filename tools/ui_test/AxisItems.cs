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
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using Kaban.UI.Screens;

// 5 October (evening), AxisSettingHMI:
// - "Remove the Simple motion category, rename AxisNumber to HMIAxisIndex, move
//   MotionModuleNumber under Device as MotionNumber, ModuleAxisIndex as MotionAxisIndex."
// - "Rename RealPositionAddress to LoadEncoderValueAddress; Simple motion values
//   becomes Motion monitor, without the read only part."
// - "Another combo left of Motion monitor: PLC Axis items (manual movement, torque
//   monitor, axis setting parameters, reference activation, Z distance, alarm codes,
//   travel per revolution) - the last selection ALL, as NONE in Motion monitor."
// - "Motion monitor should move with the page's bottom scroll bar."
// - "These selections on the Servo Monitor as well, for the admin user."
class AXI {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Snap(Control c, int width, int height, string path) {
        Pump(150);
        var at = c.PointToScreen(Point.Empty);
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("import",
            "-window root -crop " + width + "x" + height + "+" + at.X + "+" + at.Y + " " + path) { UseShellExecute = false });
        p.WaitForExit(10000);
    }

    static AxisSettingHMI Full(string name, int top, string pos, int module, int axis) {
        var a = new AxisSettingHMI {
            AxisNameKey = name, CommandBackAddress = "M1", CommandForwardAddress = "M2", PositionAddress = pos,
            MinLimitAddress = "D2", MaxLimitAddress = "D4", ReferenceAddress = "D6", CalibrationAddress = "D8",
            CalibrateAddress = "M3", GoToReferenceAddress = "M4", CurrentAddress = "W0A", ZDistanceAddress = "D73",
            AxisAlarmAddress = "W4", AxisWarningAddress = "W5", ServoAlarmAddress = "W0B", TravelPerRevolutionAddress = "D100",
            DecimalPlaces = 2, MotionNumber = module, MotionAxisIndex = axis };
        a.Location = new Point(10, top);
        return a;
    }
    static string Keys(AxisSettingHMI r) { return string.Join(",", r.Columns.OrderBy(c => c.Value.X).Select(c => c.Key)); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "axi_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        ViewPreferences.Reload();
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        SecurityManager.Logout();

        Console.WriteLine("--- the properties ---");
        var props = TypeDescriptor.GetProperties(typeof(AxisSettingHMI));
        Func<string, string> cat = n => props[n] == null ? null : props[n].Category.Trim();
        Check("Device: PlcNumber, HMIAxisIndex, MotionNumber, MotionAxisIndex", cat("PlcNumber") == "Device" && cat("HMIAxisIndex") == "Device" &&
              cat("MotionNumber") == "Device" && cat("MotionAxisIndex") == "Device", cat("HMIAxisIndex") + " " + cat("MotionNumber"));
        var order = props.Cast<PropertyDescriptor>().Where(p => p.Category.Trim() == "Device").Select(p => p.Name).ToList();
        Check("  in that order", string.Join(",", order) == "PlcNumber,HMIAxisIndex,MotionNumber,MotionAxisIndex", string.Join(",", order));
        Check("no 'Simple motion' category", !props.Cast<PropertyDescriptor>().Any(p => p.IsBrowsable && p.Category.Contains("Simple motion")));
        Check("LoadEncoderValueAddress in Movement", cat("LoadEncoderValueAddress") == "Movement");
        Check("the old names hidden", new[] { "AxisNumber", "MotionModuleNumber", "ModuleAxisIndex", "RealPositionAddress" }
              .All(n => props[n] == null || !props[n].IsBrowsable));
        var old = new AxisSettingHMI { AxisNumber = 3, MotionModuleNumber = 2, ModuleAxisIndex = 5, RealPositionAddress = "W10" };
        Check("  and still kept by an older designer file", old.HMIAxisIndex == 3 && old.MotionNumber == 2 && old.MotionAxisIndex == 5 &&
              old.LoadEncoderValueAddress == "W10" && old.HasMotion);
        var enc = new AxisSettingHMI { PositionAddress = "W0", LoadEncoderValueAddress = "W10" };
        enc.BuildRow();
        var encPos = enc.Controls.OfType<NumericDisplayHMI>().First();
        Check("the position's tooltip: 'Load encoder value'", encPos.ComparedAddress == "W10" && encPos.ComparedCaptionKey == "Tip_LoadEncoder" &&
              LanguageManager.GetString("Tip_LoadEncoder", "") == "Load encoder value");

        Console.WriteLine("--- PLC axis items ---");
        var form = new Form { ClientSize = new Size(1000, 700), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0), BackColor = Color.White };
        var page = new Panel { Bounds = new Rectangle(0, 0, 1000, 260), BackColor = Color.White, AutoScroll = true };
        form.Controls.Add(page);
        foreach (var h in new[] { new[] { "Axis", "10" }, new[] { "Position", "200" }, new[] { "Min", "500" }, new[] { "Axis alarm", "900" } })
            page.Controls.Add(new Label { Text = h[0], Location = new Point(int.Parse(h[1]), 8), Size = new Size(80, 22) });
        var r1 = Full("Robot X", 34, "W0", 1, 1);
        var r2 = Full("Robot Y", 34 + AxisSettingHMI.RowHeight, "W20", 0, 0);
        page.Controls.Add(r1); page.Controls.Add(r2);
        form.Show(); Pump(250);
        var bar = page.Controls["axisItemsBar"] as AxisPickBar;
        var mot = page.Controls["axisMotionBar"] as AxisPickBar;
        Check("two lists at the top of the page", bar != null && bar.Visible && mot != null && mot.Visible);
        var offered = bar.ItemsPick.Items.Select(i => i.Key).ToList();
        Check("  PLC axis items: what the rows have, in the row's order",
              string.Join(",", offered) == "Manual,Torque,Settings,RefActivation,ZDistance,Codes,Travel", string.Join(",", offered));
        Check("  their names", bar.ItemsPick.Items.Select(i => i.Text).SequenceEqual(new[] { "Manual movement", "Torque monitor", "Axis setting parameters",
              "Reference activation", "Reference sensor - encoder Z pulse distance", "Alarm codes", "Travel per revolution" }));
        Check("  'All' when nothing is ticked (the last line of the list)", bar.ItemsPick.AllText == "All" && bar.ItemsPick.Shown == "All");
        Check("  left of 'Motion monitor:' ('None' its last line)", bar.Right <= mot.Left && mot.MotionPick.AllText == "None" &&
              mot.Controls.OfType<Label>().Any(l => l.Text == "Motion monitor:") && bar.Controls.OfType<Label>().Any(l => l.Text == "PLC axis items:") &&
              !mot.Controls.OfType<Label>().Any(l => l.Text.Contains("read only")), bar.Bounds + " " + mot.Bounds);
        string all = Keys(r1);
        Check("All: the whole row", all == "Axis,Position,Current,Min,Max,Reference,Calibration,Cal,Ref,ZDistance,AxisAlarm,AxisWarning,ServoAlarm,TravelPerRev", all);

        SecurityManager.Login("boss", SecurityLevel.Admin);
        Pump(50);
        var before = r1.Controls.OfType<NumericInputHMI>().First();
        bar.ItemsPick.Select(new[] { "Codes" });
        Pump(150);
        Check("Alarm codes only: the name and the codes", Keys(r1) == "Axis,AxisAlarm,AxisWarning,ServoAlarm" && Keys(r2) == Keys(r1), Keys(r1));
        Check("  kept as his own", ViewPreferences.UserValue("boss", ViewPreferences.AxisItems) == "Codes");
        Check("  the row's old fields disposed, not left holding their windows", before.IsDisposed && before.Parent == null);
        Check("  no jog buttons, no position", !r1.Controls.OfType<ButtonHMI>().Any() && !r1.Controls.OfType<NumericDisplayHMI>().Any());
        Func<string, Label> head = t => page.Controls.OfType<Label>().FirstOrDefault(l => l.Text == t);
        Check("  the headings follow: Min hidden, Axis alarm over its column", !head("Min").Visible && head("Axis alarm").Visible &&
              head("Axis alarm").Left == r1.Left + r1.Columns["AxisAlarm"].X, head("Axis alarm").Left + " vs " + (r1.Left + r1.Columns["AxisAlarm"].X));
        bar.ItemsPick.Select(new[] { "Manual", "Torque" });
        Pump(150);
        Check("Manual movement and Torque monitor: the jogs, the position, the current", Keys(r1) == "Axis,Position,Current" &&
              r1.Controls.OfType<ButtonHMI>().Count() == 2 && r1.Controls.OfType<MeterHMI>().Count() == 1, Keys(r1));
        ViewPreferences.SetPick(ViewPreferences.AxisItems, new[] { "Codes", "Backlash" });
        Pump(150);
        bar.ItemsPick.Select(new[] { "Manual" });
        Pump(100);
        var kept = ViewPreferences.PickOf(ViewPreferences.AxisItems);
        Check("another page's tick (Backlash, not on these rows) stays", kept != null && kept.SetEquals(new[] { "Manual", "Backlash" }),
              kept == null ? "all" : string.Join(",", kept));
        bar.ItemsPick.Select(new string[0]);
        Pump(150);
        Check("All again: the whole row, kept as '*'", Keys(r1) == all && ViewPreferences.UserValue("boss", ViewPreferences.AxisItems) == "*" &&
              ViewPreferences.PickOf(ViewPreferences.AxisItems) == null);
        Check("the Motion monitor on a page with a module", mot.MotionPick.Visible);
        ViewPreferences.SetMotionColumnKeys(new[] { "Busy", "RealCurrentValue" });
        Pump(150);
        Check("  its columns after the PLC's", Keys(r1).EndsWith("TravelPerRev,Mot:Busy,Mot:RealCurrentValue"), Keys(r1));

        Console.WriteLine("--- in sight while the page scrolls sideways ---");
        int pinned = mot.PinnedX, itemsLeft = bar.Left, widthBefore = page.DisplayRectangle.Width;
        Check("the page is wider than its window", r1.Width > page.ClientSize.Width, r1.Width + " > " + page.ClientSize.Width);
        page.AutoScrollPosition = new Point(500, 0);
        Pump(100);
        Check("scrolled: the rows and PLC axis items moved, the Motion monitor did not", r1.Left < 0 && bar.Left < itemsLeft && mot.Left == pinned,
              "row " + r1.Left + ", items " + bar.Left + ", motion " + mot.Left + " / " + pinned);
        Check("  and the page grew no wider for it", page.DisplayRectangle.Width == widthBefore, widthBefore + " -> " + page.DisplayRectangle.Width);
        Check("  the Motion monitor no wider than the window", mot.Right <= page.ClientSize.Width, mot.Right + " <= " + page.ClientSize.Width);
        page.AutoScrollPosition = new Point(int.MaxValue / 2, 0);
        Pump(100);
        Check("  at the far right too", mot.Left == pinned && page.DisplayRectangle.Width == widthBefore);
        Snap(page, page.ClientSize.Width, page.ClientSize.Height, "/tmp/axis_items_scrolled.png");
        page.AutoScrollPosition = new Point(0, 0);
        Pump(100);
        Check("back at the left", mot.Left == pinned && bar.Left == itemsLeft && r1.Left == 10, mot.Left + " " + bar.Left + " " + r1.Left);
        bar.ItemsPick.Select(new[] { "Manual", "Torque", "Codes" });
        Pump(150);
        Snap(page, page.ClientSize.Width, r2.Bottom + 8, "/tmp/axis_items_page.png");
        ViewPreferences.SetPick(ViewPreferences.AxisItems, new string[0]);
        ViewPreferences.SetMotionColumnKeys(new string[0]);
        form.Controls.Remove(page);

        Console.WriteLine("--- the Axis Monitor (servo monitor) ---");
        AlarmHelpLinks.EnsureDiscovered();
        var source = new Panel();
        var m1 = Full("Robot X", 0, "W0", 1, 3);
        var m2 = Full("Robot Y", 40, "W20", 0, 0);
        source.Controls.Add(m1); source.Controls.Add(m2);
        AlarmHelpLinks.Harvest(source);
        Check("the rows' settings kept for the monitor", AlarmHelpLinks.AllAxes.Count >= 2 && AlarmHelpLinks.AllAxes.All(a => a.Row != null) &&
              (int)AlarmHelpLinks.AllAxes.First(a => a.Position == "W0").Row["MotionAxisIndex"] == 3);
        SecurityManager.Login("op", SecurityLevel.Operator);
        var monitor = new AxisMonitorPage { Dock = DockStyle.Fill };
        var side = new Panel { Bounds = new Rectangle(0, 0, 1000, 420) };
        side.Controls.Add(monitor);
        form.Controls.Add(side);
        Pump(200);
        var mrows = (IList<AxisMonitorRow>)typeof(AxisMonitorPage).GetProperty("Rows", Any).GetValue(monitor, null);
        var mbar = (AxisPickBar)typeof(AxisMonitorPage).GetProperty("Bar", Any).GetValue(monitor, null);
        Func<IList<AxisSettingHMI>> settingRows = () => (IList<AxisSettingHMI>)typeof(AxisMonitorPage).GetProperty("SettingRows", Any).GetValue(monitor, null);
        Check("an Operator: as it was - name, position, current - and no lists", mrows.Count == 2 && settingRows().Count == 0 && !mbar.Visible);
        SecurityManager.Login("boss", SecurityLevel.Admin);
        Pump(200);
        mrows = (IList<AxisMonitorRow>)typeof(AxisMonitorPage).GetProperty("Rows", Any).GetValue(monitor, null);
        Check("an Admin: the two lists; at first the monitor as it was (Torque monitor ticked)", mbar.Visible && mrows.Count == 2 &&
              mbar.ItemsPick.Selected.SetEquals(new[] { "Torque" }) && mbar.MotionPick.Visible);
        mbar.ItemsPick.Select(new[] { "Torque", "Settings", "Codes" });
        mbar.MotionPick.Select(new[] { "Busy", "OperationStatus" });
        Pump(300);
        var srows = settingRows();
        Check("his picks: each axis as its Axis Settings row", srows.Count == 2 && srows.All(r => r.MonitorMode), srows.Count.ToString());
        Check("  the position always, then what he ticked, then the module's", srows.Count == 2 &&
              Keys(srows[0]) == "Axis,Position,Current,Min,Max,Reference,Calibration,AxisAlarm,AxisWarning,ServoAlarm,Mot:OperationStatus,Mot:Busy",
              srows.Count > 0 ? Keys(srows[0]) : "");
        Check("  the settings shown, not entered; no Calibrate command", srows.Count == 2 && !srows[0].Controls.OfType<NumericInputHMI>().Any() &&
              srows[0].Controls.OfType<NumericDisplayHMI>().Count() == 5 && !srows[0].Controls.OfType<ButtonHMI>().Any(),
              srows.Count > 0 ? srows[0].Controls.OfType<NumericDisplayHMI>().Count() + " displays" : "");
        Check("  the module's values on the axis on a module: U1\\G2609 (axis 3)", srows.Count == 2 &&
              ((IPlcBoundControl)srows[0].Controls["motOperationStatus"]).Address == @"U1\G2609" && srows[1].Controls["motBusy"] == null);
        var heads = monitor.Controls.Find("monitorHeadings", true).FirstOrDefault();
        Check("  headings above, scrolling sideways with the rows", heads != null && heads.Parent == srows[0].Parent &&
              heads.Controls.OfType<Label>().Any(l => l.Text == "BUSY") && heads.Controls.OfType<Label>().Any(l => l.Text == "Min"));
        Check("kept apart from the page's", ViewPreferences.PickOf(ViewPreferences.MonitorItems).SetEquals(new[] { "Torque", "Settings", "Codes" }) &&
              ViewPreferences.PickOf(ViewPreferences.AxisItems) == null && ViewPreferences.ListOf(ViewPreferences.MotionColumns).Count == 0);
        Snap(side, side.Width, side.Height, "/tmp/axis_items_monitor.png");
        mbar.ItemsPick.Select(new[] { "RefActivation", "Travel" });
        Pump(300);
        srows = settingRows();
        var refBox = srows.Count > 0 ? srows[0].Controls.OfType<CheckBoxHMI>().FirstOrDefault() : null;
        Check("reference activation and travel on the monitor: shown only", refBox != null && refBox.DisplayOnly && refBox.Cursor == Cursors.Default &&
              srows[0].Controls.OfType<NumericDisplayHMI>().Any(n => n.Address == "D100") && !srows[0].Controls.OfType<NumericInputHMI>().Any());
        bool was = refBox != null && refBox.Checked;
        if (refBox != null) typeof(CheckBox).GetMethod("OnClick", Any).Invoke(refBox, new object[] { EventArgs.Empty });
        Check("  a click ticks nothing", refBox != null && refBox.Checked == was);
        mbar.ItemsPick.Select(new[] { "Manual" });
        mbar.MotionPick.Select(new string[0]);
        Pump(300);
        srows = settingRows();
        Check("Manual movement on the monitor: the jogs around the position", srows.Count == 2 && Keys(srows[0]) == "Axis,Position" &&
              srows[0].Controls.OfType<ButtonHMI>().Count() == 2, srows.Count > 0 ? Keys(srows[0]) : "");
        SecurityManager.Login("op", SecurityLevel.Operator);
        Pump(300);
        mrows = (IList<AxisMonitorRow>)typeof(AxisMonitorPage).GetProperty("Rows", Any).GetValue(monitor, null);
        Check("an Operator again: as it was", mrows.Count == 2 && settingRows().Count == 0 && !mbar.Visible);

        Console.WriteLine("--- translations ---");
        var keys = new[] { "AxIt_Pick", "AxIt_All", "AxIt_Manual", "AxIt_Torque", "AxIt_Settings", "AxIt_RefActivation", "AxIt_ZDistance",
                           "AxIt_Codes", "AxIt_Travel", "AxIt_Backlash", "Tip_LoadEncoder", "Mot_Pick", "Mot_PickNone" };
        Check("every text in the file", keys.All(k => LanguageManager.GetString(k, "##") != "##"));
        Check("  'Motion monitor:'", LanguageManager.GetString("Mot_Pick", "") == "Motion monitor:");

        SecurityManager.Logout();
        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nAXIS ITEMS ALL PASS" : "\nAXIS ITEMS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
