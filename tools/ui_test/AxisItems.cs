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
// 5 October (later):
// - "The screen area is not calculated properly: the scroll goes far too far (the Motion
//   monitor combo with it)."
// - "The numerical motion monitor items on grey, not white - and not bold."
// - "The page loads the selected members while the PLC axis items / Motion monitor members
//   are being ticked, so the list gets unresponsive: load nothing while either is open."
// 5 October (night):
// - "The Motion monitor combo should stay still [with the page] until the scroll gets to its
//   original X, then the difference added to it, so it gets aligned to the page's left side."
// - "The simple motion values have a unit setting as well, but the pages show all of them in mm."
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
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }
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
        var bar = page.Controls.Find("axisItemsBar", true).FirstOrDefault() as AxisPickBar;
        var mot = page.Controls.Find("axisMotionBar", true).FirstOrDefault() as AxisPickBar;
        var frame = AxisPageFrame.Of(page);
        var body = frame != null ? frame.Body : page;
        Check("the page as a table: the lists and the column names in its heading, the rows in its body", frame != null && bar.Parent == frame.HeadContent &&
              mot.Parent == frame.Head && r1.Parent == frame.Body && Tree(page).OfType<Label>().Where(l => l.Text == "Position").All(l => l.Parent == frame.HeadContent) &&
              !page.AutoScroll && frame.Head.Height == 34 + 40 && r1.Top == 0, frame == null ? "no frame" : frame.Head.Height + " " + r1.Top);
        Check("two lists at the top of the page", bar != null && bar.Visible && mot != null && mot.Visible);
        var offered = bar.ItemsPick.Items.Select(i => i.Key).ToList();
        Check("  PLC axis items: what the rows have, in the row's order",
              string.Join(",", offered) == "Manual,Torque,Settings,RefActivation,ZDistance,Codes,Travel", string.Join(",", offered));
        Check("  their names", bar.ItemsPick.Items.Select(i => i.Text).SequenceEqual(new[] { "Manual movement", "Torque monitor", "Axis setting parameters",
              "Reference activation", "Reference sensor - encoder Z pulse distance", "Alarm codes", "Travel per revolution" }));
        Check("  'All' when nothing is ticked (the last line of the list)", bar.ItemsPick.AllText == "All" && bar.ItemsPick.State == "All");
        // 8 October: "remove these two controls' descriptions ... show them as the combo's first member
        // (not to select, just for the user to see when the combo is both open or closed)"
        Check("  no captions beside the lists: each names itself, closed", !bar.Controls.OfType<Label>().Any() && !mot.Controls.OfType<Label>().Any() &&
              bar.ItemsPick.Shown == "PLC axis items" && mot.MotionPick.Shown == "Motion monitor items", bar.ItemsPick.Shown + " / " + mot.MotionPick.Shown);
        Check("  left of Motion monitor items ('None' its last line)", bar.Right <= mot.Left && mot.MotionPick.AllText == "None" && mot.MotionPick.State == "None",
              bar.Bounds + " " + mot.Bounds);
        bar.ItemsPick.DropUnder(bar.ItemsPick);
        Pump(150);
        var openHead = bar.ItemsPick.OpenHeading;
        Check("  open: its name the first line, not one to tick", bar.ItemsPick.IsOpen && openHead != null && openHead.Text == "PLC axis items" &&
              openHead.Bottom <= bar.ItemsPick.OpenList.Top && bar.ItemsPick.OpenList.Items.Count == offered.Count,
              openHead == null ? "no heading" : openHead.Text + " " + openHead.Bounds + " / " + bar.ItemsPick.OpenList.Bounds);
        bar.ItemsPick.OpenDrop.Close();
        Pump(150);
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
        Func<string, Label> head = t => Tree(page).OfType<Label>().FirstOrDefault(l => l.Text == t && !(l.Parent is AxisPickBar));
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
        int home = mot.HomeX, stick = mot.StickX, itemsLeft = bar.Left, widthBefore = body.DisplayRectangle.Width;
        Check("the Motion monitor right of PLC axis items, sticking where the page's content starts", home == mot.Left && home >= bar.Right &&
              stick == bar.Left && stick == 10, home + " / " + stick + " / items " + bar.Left + "-" + bar.Right);
        // 6 October: "leave some distance in between 'PLC axis items' and 'Motion monitor'"
        Check("  with room between the two lists", mot.Left - (bar.Right + frame.HeadContent.Left) >= AxisPickBar.GapAfterItems &&
              AxisPickBar.GapAfterItems >= 24, (mot.Left - (bar.Right + frame.HeadContent.Left)) + " px");
        Check("the page is wider than its window", r1.Width > body.ClientSize.Width, r1.Width + " > " + body.ClientSize.Width);
        body.AutoScrollPosition = new Point(100, 0);
        Pump(100);
        Check("scrolled less than its place: it moves with the page", r1.Left == 10 - 100 && mot.Left == home - 100,
              "row " + r1.Left + ", motion " + mot.Left + " (home " + home + ")");
        body.AutoScrollPosition = new Point(home - stick, 0);
        Pump(100);
        Check("  to the page's left side", mot.Left == stick, mot.Left.ToString());
        body.AutoScrollPosition = new Point(home - stick + 150, 0);
        Pump(100);
        Check("scrolled further: it stays at the left side (its place plus the scroll beyond it), the rows and PLC axis items go on",
              mot.Left == stick && -body.AutoScrollPosition.X > home - stick && r1.Left == 10 + body.AutoScrollPosition.X &&
              bar.Left + frame.HeadContent.Left < itemsLeft,
              "scroll " + (-body.AutoScrollPosition.X) + ", row " + r1.Left + ", items " + (bar.Left + frame.HeadContent.Left) + ", motion " + mot.Left);
        Check("  and the page grew no wider for it", body.DisplayRectangle.Width == widthBefore, widthBefore + " -> " + body.DisplayRectangle.Width);
        Check("  the Motion monitor no wider than the window", mot.Right <= frame.Head.ClientSize.Width, mot.Right + " <= " + frame.Head.ClientSize.Width);
        body.AutoScrollPosition = new Point(int.MaxValue / 2, 0);
        Pump(100);
        Check("  at the far right too", mot.Left == stick && body.DisplayRectangle.Width == widthBefore);
        Snap(page, page.ClientSize.Width, page.ClientSize.Height, "/tmp/axis_items_scrolled.png");
        body.AutoScrollPosition = new Point(150, 0);
        Pump(100);
        Check("  back to a small scroll: with the page again", mot.Left == home - 150, mot.Left.ToString());
        body.AutoScrollPosition = new Point(0, 0);
        Pump(100);
        Check("back at the left", mot.Left == home && bar.Left == itemsLeft && r1.Left == 10, mot.Left + " " + bar.Left + " " + r1.Left);
        Func<int> contentRight = () => body.Controls.Cast<Control>().Where(c => c.Visible).Max(c => c.Right - body.AutoScrollPosition.X);
        int lastCol = r1.Columns.Values.Max(c => c.X + c.Y);   // (left, width)
        Check("the page scrolls as far as its columns, no further", body.DisplayRectangle.Width <= contentRight() + body.AutoScrollMargin.Width + 2 &&
              r1.Width - lastCol >= 0 && r1.Width - lastCol < 40, body.DisplayRectangle.Width + " vs " + contentRight() + ", row " + r1.Width + " last column " + lastCol);
        var motValue = r1.Controls.OfType<MotionValueHMI>().FirstOrDefault(v => v.Item.Key == "RealCurrentValue");
        Check("the motion monitor's numbers on light grey, not bold", motValue != null && MotionValueHMI.DisplayBack.R < 245 && MotionValueHMI.DisplayBack.R > 220 &&
              !motValue.Font.Bold, motValue == null ? "none" : MotionValueHMI.DisplayBack + " " + motValue.Font);
        int wideAll = r1.Width;
        bar.ItemsPick.Select(new[] { "Codes" });
        Pump(150);
        Check("fewer items: the rows narrower, the page no wider than they are", r1.Width < wideAll && body.DisplayRectangle.Width <= Math.Max(body.ClientSize.Width, contentRight() + 2),
              r1.Width + " < " + wideAll + ", page " + body.DisplayRectangle.Width + " / content " + contentRight());
        var wide = Full("Robot Z", 2 * AxisSettingHMI.RowHeight, "W40", 1, 2);
        wide.Width = 3000;                          // as a designer may have left it
        body.Controls.Add(wide);
        Pump(150);
        Check("a row the designer left 3000 wide: as wide as its columns at run time", wide.Width == r1.Width && wide.Width < 3000, wide.Width.ToString());
        body.Controls.Remove(wide); wide.Dispose();
        bar.ItemsPick.Select(new string[0]);
        Pump(150);

        Console.WriteLine("--- the lists apply when they close ---");
        Check("both lists apply on close", bar.ItemsPick.ApplyOnClose && mot.MotionPick.ApplyOnClose);
        var onClick = typeof(Control).GetMethod("OnClick", Any);
        onClick.Invoke(bar.ItemsPick, new object[] { EventArgs.Empty });
        Pump(100);
        var list = bar.ItemsPick.OpenList;
        string rowBefore = Keys(r1);
        int builds = 0;
        var firstField = r1.Controls.OfType<NumericInputHMI>().FirstOrDefault();
        Check("PLC axis items open", bar.ItemsPick.IsOpen && list != null);
        if (list != null)
        {
            for (int i = 0; i < list.Items.Count; i++) if ((string)list.Items[i] == "Alarm codes" || (string)list.Items[i] == "Manual movement") list.SetItemChecked(i, true);
            Pump(150);
            Check("  two ticks: the rows untouched while it is open", Keys(r1) == rowBefore && firstField != null && !firstField.IsDisposed &&
                  ViewPreferences.PickOf(ViewPreferences.AxisItems) == null && bar.ItemsPick.Selected.Count == 2, Keys(r1));
            bar.ItemsPick.OpenDrop.Close();
            Pump(200);
            Check("  closed: laid out once, with both", !bar.ItemsPick.IsOpen && Keys(r1) == "Axis,Position,AxisAlarm,AxisWarning,ServoAlarm,Mot:Busy,Mot:RealCurrentValue" &&
                  ViewPreferences.PickOf(ViewPreferences.AxisItems).SetEquals(new[] { "Manual", "Codes" }), Keys(r1));
        }
        onClick.Invoke(mot.MotionPick, new object[] { EventArgs.Empty });
        Pump(100);
        var mlist = mot.MotionPick.OpenList;
        if (mlist != null)
        {
            mlist.SetItemChecked(0, !mlist.GetItemChecked(0));
            Pump(150);
            Check("Motion monitor: a tick while open, nothing yet", ViewPreferences.ListOf(ViewPreferences.MotionColumns).SequenceEqual(new[] { "Busy", "RealCurrentValue" }));
            mot.MotionPick.OpenDrop.Close();
            Pump(200);
            Check("  closed: taken", !ViewPreferences.ListOf(ViewPreferences.MotionColumns).SequenceEqual(new[] { "Busy", "RealCurrentValue" }),
                  string.Join(",", ViewPreferences.ListOf(ViewPreferences.MotionColumns)));
        }
        else Check("Motion monitor open", false);
        ViewPreferences.SetMotionColumnKeys(new[] { "Busy", "RealCurrentValue" });
        ViewPreferences.SetPick(ViewPreferences.AxisItems, new string[0]);
        Pump(150);
        bar.ItemsPick.Select(new[] { "Manual", "Torque", "Codes" });
        Pump(150);
        Snap(page, page.ClientSize.Width, r2.Bottom + 8, "/tmp/axis_items_page.png");
        ViewPreferences.SetPick(ViewPreferences.AxisItems, new string[0]);
        ViewPreferences.SetMotionColumnKeys(new string[0]);
        form.Controls.Remove(page);

        Console.WriteLine("--- a page that holds the focus ---");
        var uc = new UserControl { Bounds = new Rectangle(0, 0, 1000, 260), AutoScroll = true, BackColor = Color.White };
        form.Controls.Add(uc);
        var u1 = Full("Robot X", 34, "W0", 1, 1);
        uc.Controls.Add(u1);
        Pump(250);
        var focused = u1.Controls.OfType<NumericInputHMI>().First();
        focused.Focus();
        Pump(50);
        var umot = uc.Controls.Find("axisMotionBar", true).FirstOrDefault() as AxisPickBar;
        var ubody = AxisPageFrame.Of(uc) != null ? (ScrollableControl)AxisPageFrame.Of(uc).Body : uc;
        ubody.AutoScrollPosition = new Point(600, 0);
        Pump(200);
        Check("scrolled sideways it stays scrolled (the Motion monitor kept in sight without laying the page out)",
              -ubody.AutoScrollPosition.X > 0 && umot != null && umot.Left == umot.PlaceX && umot.Left < umot.HomeX,
              (-ubody.AutoScrollPosition.X) + " " + (umot == null ? "" : umot.Left + "/" + umot.PlaceX));
        form.Controls.Remove(uc);

        Console.WriteLine("--- more axes than the page shows: the heading stays, the rows scroll under it ---");
        var tall = new UserControl { Bounds = new Rectangle(0, 0, 900, 260), AutoScroll = true, BackColor = Color.White };
        form.Controls.Add(tall);
        foreach (var h in new[] { new[] { "Axis", "10" }, new[] { "Position", "200" }, new[] { "Axis alarm", "900" } })
            tall.Controls.Add(new Label { Text = h[0], Location = new Point(int.Parse(h[1]), 8), Size = new Size(80, 22) });
        var tallRows = Enumerable.Range(0, 6).Select(i => Full("Axis " + i, 34 + i * AxisSettingHMI.RowHeight, "W" + (i * 20), 1, i + 1)).ToList();
        foreach (var r in tallRows) tall.Controls.Add(r);
        Pump(300);
        Kaban.UI.Helpers.PageScroll.Enable(tall);
        var tf = AxisPageFrame.Of(tall);
        Check("framed; the page itself does not scroll (PageScroll leaves it so)", tf != null && !tall.AutoScroll && tf.Body.VerticalScroll.Visible);
        Func<string, Label> tallHead = t => Tree(tall).OfType<Label>().FirstOrDefault(l => l.Text == t && !(l.Parent is AxisPickBar));
        int headY = tallHead("Position").PointToScreen(Point.Empty).Y, rowY = tallRows[0].PointToScreen(Point.Empty).Y;
        var tbar = tall.Controls.Find("axisItemsBar", true).FirstOrDefault();
        int barY = tbar.PointToScreen(Point.Empty).Y;
        tf.Body.AutoScrollPosition = new Point(0, 120);
        Pump(150);
        Check("scrolled down: the rows went up, the column names and the lists did not", tallRows[0].PointToScreen(Point.Empty).Y < rowY &&
              tallHead("Position").PointToScreen(Point.Empty).Y == headY && tbar.PointToScreen(Point.Empty).Y == barY,
              "row " + rowY + " -> " + tallRows[0].PointToScreen(Point.Empty).Y + ", heading " + headY + " -> " + tallHead("Position").PointToScreen(Point.Empty).Y);
        Check("  a row scrolled under the heading is hidden by it (the body starts under it)", tf.Body.PointToScreen(Point.Empty).Y >= tallHead("Position").PointToScreen(Point.Empty).Y + tallHead("Position").Height);
        // 6 October: "keep the axis name column on the left side as well so we can see it even when
        // the bottom scroll moved to right, for the user could see the parameters owned by whom"
        Func<AxisSettingHMI, Label> nameOf = r => (Label)typeof(AxisSettingHMI).GetProperty("NameLabel", Any).GetValue(r, null);
        Func<Label, int> textRight = l => l.PointToScreen(Point.Empty).X + l.Width - l.Padding.Right;
        int nameRight0 = textRight(nameOf(tallRows[3])), headRight0 = textRight(tallHead("Axis"));
        int nameY0 = nameOf(tallRows[3]).PointToScreen(new Point(0, nameOf(tallRows[3]).Height / 2)).Y - tallRows[3].PointToScreen(Point.Empty).Y;
        Check("  unscrolled: the names as built", tallRows.All(r => !AxisSettingHMI.IsStuck(nameOf(r)) && nameOf(r).Left == 0 && nameOf(r).Padding.Left == 0));
        tf.Body.AutoScrollPosition = new Point(300, 120);
        Pump(150);
        var seen = tallRows.Where(r => r.Bottom > 0 && r.Top < tf.Body.ClientSize.Height).ToList();
        int bodyX = tf.Body.PointToScreen(Point.Empty).X;
        Check("scrolled sideways: every axis's name where it was, the columns passing under it",
              seen.Count >= 3 && seen.All(r => textRight(nameOf(r)) == nameRight0 && AxisSettingHMI.IsStuck(nameOf(r)) &&
                                         r.Controls.GetChildIndex(nameOf(r)) == 0),
              string.Join(",", seen.Select(r => textRight(nameOf(r)) + (AxisSettingHMI.IsStuck(nameOf(r)) ? "s" : ""))) + " vs " + nameRight0);
        Check("  covering from the page's left edge over the whole row (nothing scrolled shows beside it)",
              seen.All(r => nameOf(r).PointToScreen(Point.Empty).X <= bodyX && nameOf(r).Top == 0 && nameOf(r).Height == r.Height &&
                            nameOf(r).BackColor.A == 255),
              nameOf(seen[0]).PointToScreen(Point.Empty).X + " <= " + bodyX + ", " + nameOf(seen[0]).Bounds);
        Check("  its text at the same height in the row", nameOf(tallRows[3]).PointToScreen(new Point(0, nameOf(tallRows[3]).Height / 2)).Y -
              tallRows[3].PointToScreen(Point.Empty).Y == nameY0);
        Check("  the Axis heading stays over the names", textRight(tallHead("Axis")) == headRight0 && AxisSettingHMI.IsStuck(tallHead("Axis")) &&
              tallHead("Axis").PointToScreen(Point.Empty).X <= tf.Head.PointToScreen(Point.Empty).X,
              textRight(tallHead("Axis")) + " vs " + headRight0);
        Check("  the name still 'Axis' for the notes page", tallRows[3].ColumnOf(nameOf(tallRows[3])) == "Axis");
        int colX = tallRows[1].PointToScreen(Point.Empty).X + tallRows[1].Columns["AxisAlarm"].X;
        Check("  and sideways: the column names stay over their columns", tf.HeadContent.Left == tf.Body.AutoScrollPosition.X && tf.Body.AutoScrollPosition.X < 0 &&
              tallHead("Axis alarm").PointToScreen(Point.Empty).X == colX, tallHead("Axis alarm").PointToScreen(Point.Empty).X + " vs " + colX);
        Snap(tall, tall.Width, tall.Height, "/tmp/axis_items_frozen.png");
        tf.Body.AutoScrollPosition = new Point(0, 120);
        Pump(150);
        Check("back at the left: the names and the heading as built", tallRows.All(r => !AxisSettingHMI.IsStuck(nameOf(r)) && nameOf(r).Left == 0 &&
              nameOf(r).Padding.Left == 0 && nameOf(r).Padding.Right == 0) && !AxisSettingHMI.IsStuck(tallHead("Axis")) && textRight(tallHead("Axis")) == headRight0);
        form.Controls.Remove(tall);

        Console.WriteLine("--- the module's values in the row's unit ---");
        Func<string, SimpleMotion.Item> itemOf = k => SimpleMotion.Find(k);
        int raw = 1234567;                          // 123.4567 in the module's mm (0.1 um)
        Check("a length row: mm as the HMI's length unit is mm",
              SimpleMotion.Format(itemOf("RealCurrentValue"), raw & 0xFFFF, raw >> 16, 0, 1, 2, SimpleMotion.RowUnit.Length) == "123.46 mm");
        Check("an angle row, the module set to mm (its figures the degrees): degrees",
              SimpleMotion.Format(itemOf("RealCurrentValue"), raw & 0xFFFF, raw >> 16, 0, 1, 3, SimpleMotion.RowUnit.Angle) == "123.457 \u00B0",
              SimpleMotion.Format(itemOf("RealCurrentValue"), raw & 0xFFFF, raw >> 16, 0, 1, 3, SimpleMotion.RowUnit.Angle));
        Check("  set to degree: degrees (0.00001 each)",
              SimpleMotion.Format(itemOf("SoftLimitUpper"), 36000000 & 0xFFFF, 36000000 >> 16, 2, 1, 3, SimpleMotion.RowUnit.Angle) == "360.000 \u00B0");
        Check("  a speed: degrees a minute", SimpleMotion.Format(itemOf("CurrentSpeed"), 150000 & 0xFFFF, 150000 >> 16, 0, 1, 2, SimpleMotion.RowUnit.Angle) == "1500.00 \u00B0/min");
        Check("  pulses stay pulses", SimpleMotion.Format(itemOf("RealCurrentValue"), 1500, 0, 3, 1, 2, SimpleMotion.RowUnit.Angle) == "1500 pulse");
        UnitManager.SetActive(UnitType.Length, "inch");
        Check("a length row with inch chosen: 25.4 mm is 1.0000 inch", SimpleMotion.Format(itemOf("RealCurrentValue"), 254000 & 0xFFFF, 254000 >> 16, 0, 1, 2, SimpleMotion.RowUnit.Length) == "1.0000 inch",
              SimpleMotion.Format(itemOf("RealCurrentValue"), 254000 & 0xFFFF, 254000 >> 16, 0, 1, 2, SimpleMotion.RowUnit.Length));
        Check("  a speed: inch a minute", SimpleMotion.Format(itemOf("CurrentSpeed"), 254000 & 0xFFFF, 254000 >> 16, 0, 1, 2, SimpleMotion.RowUnit.Length) == "100.0000 inch/min",
              SimpleMotion.Format(itemOf("CurrentSpeed"), 254000 & 0xFFFF, 254000 >> 16, 0, 1, 2, SimpleMotion.RowUnit.Length));
        Check("  travel per rotation to its last count", SimpleMotion.Format(itemOf("TravelPerRevolution"), 1399880 & 0xFFFF, 1399880 >> 16, 0, 1, 2, SimpleMotion.RowUnit.Length) == "5.511339 inch",
              SimpleMotion.Format(itemOf("TravelPerRevolution"), 1399880 & 0xFFFF, 1399880 >> 16, 0, 1, 2, SimpleMotion.RowUnit.Length));
        UnitManager.SetActive(UnitType.Length, "mm");
        var lenRow = Full("Robot X", 34, "W0", 1, 1);
        var angRow = Full("Milling 1 Angle", 34 + AxisSettingHMI.RowHeight, "W20", 2, 1);
        angRow.Suffix = "\u00B0";
        angRow.DecimalPlaces = 3;
        var unitPage = new Panel { Bounds = new Rectangle(0, 0, 1000, 260), AutoScroll = true };
        form.Controls.Add(unitPage);
        ViewPreferences.SetMotionColumnKeys(new[] { "RealCurrentValue", "SoftLimitUpper", "CurrentSpeed" });
        unitPage.Controls.Add(lenRow); unitPage.Controls.Add(angRow);
        Pump(250);
        Func<AxisSettingHMI, string, MotionValueHMI> cell = (r, k) => r.Controls.OfType<MotionValueHMI>().FirstOrDefault(v => v.Item.Key == k);
        Check("on the page: the length row's values as lengths, the angle row's as angles",
              cell(lenRow, "RealCurrentValue") != null && cell(lenRow, "RealCurrentValue").RowUnit == SimpleMotion.RowUnit.Length &&
              cell(angRow, "SoftLimitUpper") != null && cell(angRow, "SoftLimitUpper").RowUnit == SimpleMotion.RowUnit.Angle);
        Func<string, int> words = a => a.EndsWith("\\G2450") ? (raw & 0xFFFF) : a.EndsWith("\\G2451") ? (raw >> 16) : 0;   // the module set to mm
        foreach (var v in lenRow.Controls.OfType<MotionValueHMI>().Concat(angRow.Controls.OfType<MotionValueHMI>())) v.UpdateState(words, c => true);
        Check("  the module set to mm: 123.46 mm, and the angle 123.457 degrees", cell(lenRow, "RealCurrentValue").ShownText == "123.46 mm" &&
              cell(angRow, "RealCurrentValue").ShownText == "123.457 \u00B0", cell(lenRow, "RealCurrentValue").ShownText + " / " + cell(angRow, "RealCurrentValue").ShownText);
        UnitManager.SetActive(UnitType.Length, "inch");
        Pump(50);
        Check("  inch chosen: the length row's in inch at once, the angle's as it was", cell(lenRow, "RealCurrentValue").ShownText == "4.8605 inch" &&
              cell(angRow, "RealCurrentValue").ShownText == "123.457 \u00B0", cell(lenRow, "RealCurrentValue").ShownText);
        UnitManager.SetActive(UnitType.Length, "mm");
        Check("  back to mm", cell(lenRow, "RealCurrentValue").ShownText == "123.46 mm");
        form.Controls.Remove(unitPage);
        ViewPreferences.SetMotionColumnKeys(new string[0]);

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
        Check("  headings above, out of the list (they stay while it scrolls), in line with the rows", heads != null && heads.Parent.Name == "monitorRowsHead" &&
              heads.Parent.Visible && heads.Left == srows[0].Left && heads.Parent.Bottom <= srows[0].Parent.Top + 1 &&
              heads.Controls.OfType<Label>().Any(l => l.Text == "BUSY") && heads.Controls.OfType<Label>().Any(l => l.Text == "Min"));
        Check("kept apart from the page's", ViewPreferences.PickOf(ViewPreferences.MonitorItems).SetEquals(new[] { "Torque", "Settings", "Codes" }) &&
              ViewPreferences.PickOf(ViewPreferences.AxisItems) == null && ViewPreferences.ListOf(ViewPreferences.MotionColumns).Count == 0);
        Snap(side, side.Width, side.Height, "/tmp/axis_items_monitor.png");
        side.Width = 700;
        Pump(200);
        var monList = (ScrollableControl)srows[0].Parent;
        Func<AxisSettingHMI, Label> mname = r => (Label)typeof(AxisSettingHMI).GetProperty("NameLabel", Any).GetValue(r, null);
        var mAxisHead = heads.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "Axis");
        int mRight0 = mname(srows[0]).PointToScreen(Point.Empty).X + mname(srows[0]).Width;
        monList.AutoScrollPosition = new Point(250, 0);
        Pump(200);
        Check("the monitor scrolled sideways: the names stay at the left too, the Axis heading over them",
              -monList.AutoScrollPosition.X > 0 && srows.All(r => AxisSettingHMI.IsStuck(mname(r)) && mname(r).PointToScreen(Point.Empty).X <= monList.PointToScreen(Point.Empty).X &&
                                                               mname(r).PointToScreen(Point.Empty).X + mname(r).Width - mname(r).Padding.Right == mRight0) &&
              mAxisHead != null && AxisSettingHMI.IsStuck(mAxisHead),
              (-monList.AutoScrollPosition.X) + ": " + string.Join(",", srows.Select(r => mname(r).PointToScreen(Point.Empty).X + "/" + mname(r).Width)) + " vs " + mRight0);
        monList.AutoScrollPosition = Point.Empty;
        side.Width = 1000;
        Pump(200);
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
        Check("  'Motion monitor items'", LanguageManager.GetString("Mot_Pick", "") == "Motion monitor items");

        SecurityManager.Logout();
        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nAXIS ITEMS ALL PASS" : "\nAXIS ITEMS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
