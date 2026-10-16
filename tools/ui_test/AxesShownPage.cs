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

// 8 October: "It could be very useful if we add a multiple selection combo to the top of axis setting
// and axis monitor pages for the user to be able to hide/show the axis. The shown one should be moved
// up to fill the hidden ones place as well then btw so the user could see all of shown ones together
// (just like filtered tables do)." - the FA2010 Axis Settings page.
class ASP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }

    static Form Open(out Control page, out AxisPageFrame frame, out AxisPickBar items, out AxisPickBar motion) {
        var form = new Form { ClientSize = new Size(1270, 700), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        page = new Kaban.Machines.FA2010.Screens.Parameters.AxisSettings { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        form.Show(); Pump(1000);
        frame = AxisPageFrame.Of(page);
        items = page.Controls.Find("axisItemsBar", true).FirstOrDefault() as AxisPickBar;
        motion = page.Controls.Find("axisMotionBar", true).FirstOrDefault() as AxisPickBar;
        return form;
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "asp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        ViewPreferences.SetMotionColumnKeys(new[] { "Busy" });
        Control page; AxisPageFrame frame; AxisPickBar items, motion;
        var form = Open(out page, out frame, out items, out motion);
        Check("the page framed, its lists there", frame != null && items != null && motion != null);
        if (frame == null || items == null) { Environment.Exit(1); return; }
        var rows = frame.Body.Controls.OfType<AxisSettingHMI>().OrderBy(r => r.Top).ToList();
        var slots = rows.Select(r => r.Top).ToList();
        var names = rows.Select(r => r.AxisNameKey).ToList();

        Console.WriteLine("--- the Axes list ---");
        var pick = items.AxesPick;
        var axisHead = frame.HeadContent.Controls.OfType<Label>().FirstOrDefault(l => (l.Tag as string) == "AxisHeading:Axis");
        Check("the Axis column's heading opens the Axes list ('All axes' with nothing ticked)", pick != null && axisHead != null &&
              AxisHeadingFilter.PickOf(axisHead) == pick && axisHead.Cursor == Cursors.Hand && pick.Shown == "All axes" && pick.Parent == null);
        Check("  the heading's lists no wider for it (three in a line did not fit at 1.5)", !items.Controls.Contains(pick));
        typeof(Control).GetMethod("OnClick", Any).Invoke(axisHead, new object[] { EventArgs.Empty });
        Pump(200);
        Check("  a click on the heading: the list opens under it", pick.IsOpen && pick.OpenList != null && pick.OpenList.Items.Count == 17);
        if (pick.OpenDrop != null) pick.OpenDrop.Close();
        Pump(200);
        Check("  the page's 17 axes, in the page's order, by their names", pick.Items.Count == 17 && pick.Items[0].Text == "Robot X" && pick.Items[1].Text == "Robot Y" &&
              pick.Items.Last().Text == "V Cutting X", string.Join(", ", pick.Items.Take(4).Select(i => i.Text)));
        Check("  the Motion monitor list still right of the items list", motion.Left >= items.Right + items.Parent.Left, motion.Left + " >= " + (items.Right + items.Parent.Left));

        Console.WriteLine("--- two axes ticked ---");
        var keep = new[] { pick.Items[1].Key, pick.Items[13].Key };       // Robot Y, Exit X (the old program's order since 8 October)
        pick.Select(keep);
        Pump(200);
        var shown = rows.Where(r => r.Visible).OrderBy(r => r.Top).ToList();
        Check("only they are shown", shown.Count == 2 && shown[0].AxisNameKey == names[1] && shown[1].AxisNameKey == names[13],
              string.Join(",", shown.Select(r => r.AxisNameKey)));
        Check("  moved up together: the first two places of the page", shown[0].Top == slots[0] && shown[1].Top == slots[1],
              shown[0].Top + "," + shown[1].Top + " vs " + slots[0] + "," + slots[1]);
        Check("  the page scrolls no further than them", frame.Body.DisplayRectangle.Height <= Math.Max(frame.Body.ClientSize.Height, shown[1].Bottom + 20),
              frame.Body.DisplayRectangle.Height + " vs " + shown[1].Bottom);
        Check("  the heading's Axis column over the first one shown (scrolled sideways too)", ((Func<bool>)(() => {
            frame.Body.AutoScrollPosition = new Point(300, 0); Pump(200);
            var stuck = (Label)typeof(AxisSettingHMI).GetProperty("NameLabel", Any).GetValue(shown[0], null);
            bool ok = AxisSettingHMI.IsStuck(stuck);
            frame.Body.AutoScrollPosition = Point.Empty; Pump(100);
            return ok; }))());
        Check("  kept as the user's view", ViewPreferences.PickOf(ViewPreferences.AxesShown) != null && ViewPreferences.PickOf(ViewPreferences.AxesShown).SetEquals(keep),
              ViewPreferences.Get(ViewPreferences.AxesShown));

        Console.WriteLine("--- an axis whose conditions are not met ---");
        pick.Select(new string[0]);
        Pump(200);
        Check("nothing ticked: all again, in the designer's places", rows.All(r => r.Visible) && rows.Select(r => r.Top).SequenceEqual(slots) &&
              ViewPreferences.PickOf(ViewPreferences.AxesShown) == null);
        var opt = rows[2];
        opt.VisibilityConditions = SystemCondition.SafetyOK;
        opt.UpdateState(a => 0, c => false);
        Pump(100);
        Check("hidden by its conditions: the rows below move up into its place", !opt.Visible && rows[3].Top == slots[2] && rows.Last().Top == slots[slots.Count - 2],
              rows[3].Top + " vs " + slots[2]);
        var optKey = opt.AxisNameKey + "@" + opt.PlcNumber;
        Check("  and not offered in the Axes list (16 October: ticked, it could not be seen)", pick.Items.Count == 16 && !pick.Items.Any(i => i.Key == optKey),
              pick.Items.Count + " offered");
        opt.UpdateState(a => 0, c => true);
        Pump(100);
        Check("  met again: back in its place", opt.Visible && rows.Select(r => r.Top).SequenceEqual(slots));
        Check("  and offered again, in its place in the list", pick.Items.Count == 17 && pick.Items[2].Key == optKey);
        pick.Select(new[] { optKey, keep[0] });
        Pump(100);
        opt.UpdateState(a => 0, c => false);
        Pump(100);
        shown = rows.Where(r => r.Visible).ToList();
        Check("ticked, then its conditions not met: the other tick shows alone, its own tick kept",
              shown.Count == 1 && shown[0].AxisNameKey == names[1] && pick.Selected.SetEquals(new[] { keep[0] }) &&
              ViewPreferences.PickOf(ViewPreferences.AxesShown).Contains(optKey), string.Join(",", shown.Select(r => r.AxisNameKey)));
        opt.UpdateState(a => 0, c => true);
        Pump(100);
        shown = rows.Where(r => r.Visible).ToList();
        Check("  met again: ticked and shown again", shown.Count == 2 && pick.Selected.SetEquals(new[] { optKey, keep[0] }) && opt.Visible,
              string.Join(",", shown.Select(r => r.AxisNameKey)));
        pick.Select(new string[0]);
        Pump(100);
        pick.Select(keep);
        Pump(100);
        opt.UpdateState(a => 0, c => true);
        Check("  a scan of a row the list hides leaves it hidden", !opt.Visible);
        form.Close();

        Console.WriteLine("--- the page opened again ---");
        Control page2; AxisPageFrame frame2; AxisPickBar items2, motion2;
        var form2 = Open(out page2, out frame2, out items2, out motion2);
        var rows2 = frame2.Body.Controls.OfType<AxisSettingHMI>().ToList();
        Check("the same two axes shown, ticked", rows2.Count(r => r.Visible) == 2 && items2.AxesPick.Selected.SetEquals(keep), items2.AxesPick.Shown);
        Check("  in the first two places", rows2.Where(r => r.Visible).OrderBy(r => r.Top).Select(r => r.Top).SequenceEqual(slots.Take(2)));
        // another page's ticks (H800's rows) stay when this page changes its own
        ViewPreferences.SetPick(ViewPreferences.AxesShown, keep.Concat(new[] { "Axis_Other@4" }));
        items2.AxesPick.Select(new[] { keep[0] });
        Pump(100);
        Check("another page's tick stays when this page's change", ViewPreferences.PickOf(ViewPreferences.AxesShown).SetEquals(new[] { keep[0], "Axis_Other@4" }),
              ViewPreferences.Get(ViewPreferences.AxesShown));
        items2.AxesPick.Select(new string[0]);
        Pump(100);
        Check("  this page's All leaves it alone too", ViewPreferences.PickOf(ViewPreferences.AxesShown).SetEquals(new[] { "Axis_Other@4" }));
        Check("the texts in every language", new[] { "AxShow_All", "Tip_AxShow", "Lbl_IoShow", "IoShow_All", "IoShow_Inputs", "IoShow_Outputs" }
              .All(k => new[] { "EN", "TR", "RU", "DE", "IT", "ES" }.All(l => !string.IsNullOrEmpty(LanguageManager.GetStringIn(l, k)))));
        form2.Close();
        Console.WriteLine(fail == 0 ? "\nAXES SHOWN (PAGE) ALL PASS" : "\nAXES SHOWN (PAGE) " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
