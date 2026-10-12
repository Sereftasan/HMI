using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

// 6 October, the FA2010 axis page at a larger screen scale (1.5):
// - "leave some distance in between 'PLC axis items' and 'Motion monitor' because they get
//   almost mixed when the screen scale gets higher" - the Motion list's place was kept in the
//   pixels of factor 1 and stood over the items list (235 px over it at 1.5);
// - the heading, measured on the scaled page, was scaled once more: a band of white between the
//   column names and the rows;
// - "keep the axis name column on the left side as well".
class APS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }

    [STAThread]
    static void Main() {
        UiScale.Override(1.5);
        var dir = Path.Combine(Path.GetTempPath(), "aps_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        ViewPreferences.SetMotionColumnKeys(new[] { "AxisError", "OperationStatus", "Busy", "RealCurrentValue" });
        var form = new Form { ClientSize = new Size(1270, 700), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var page = new Kaban.Machines.FA2010.Screens.Parameters.AxisSettings { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        UiScale.ApplyOnLoad(form);
        form.Show(); Pump(1000);
        form.ClientSize = new Size(1270, 700); Pump(300);

        var frame = AxisPageFrame.Of(page);
        var items = page.Controls.Find("axisItemsBar", true).FirstOrDefault() as AxisPickBar;
        var motion = page.Controls.Find("axisMotionBar", true).FirstOrDefault() as AxisPickBar;
        Check("the page framed, both lists there", frame != null && items != null && motion != null && motion.Visible);
        if (frame == null || items == null || motion == null) { Environment.Exit(1); return; }

        Console.WriteLine("--- the two lists at 1.5 ---");
        int itemsRight = items.Right + items.Parent.Left;          // in the heading's terms
        Check("Motion monitor items right of PLC axis items, never over it", motion.Left >= itemsRight, motion.Left + " >= " + itemsRight);
        Check("  with the room between them at the scale (28 at factor 1)", motion.Left - itemsRight >= UiScale.Px(AxisPickBar.GapAfterItems) - 1,
              (motion.Left - itemsRight) + " px");
        Check("  PLC axis items no wider than what it holds", items.Width == items.NaturalWidth, items.Width + " / " + items.NaturalWidth);
        Check("  named 'Motion monitor items' - by its own first line, no caption beside it",
              motion.MotionPick.Shown == "Motion monitor items" && !motion.Controls.OfType<Label>().Any() && !items.Controls.OfType<Label>().Any());
        Check("  its list's arrow in sight (narrowed to the window)", motion.Right <= frame.Head.ClientSize.Width &&
              motion.MotionPick.Right <= motion.ClientSize.Width, motion.MotionPick.Right + " <= " + motion.ClientSize.Width + ", bar " + motion.Right);

        Console.WriteLine("--- the heading ---");
        var headLabels = frame.HeadContent.Controls.OfType<Label>().Where(l => l.Visible).ToList();
        int namesBottom = headLabels.Max(l => l.Bottom);
        Check("no band of white between the column names and the rows", frame.Head.Height - namesBottom <= UiScale.Px(12),
              "heading " + frame.Head.Height + ", names end at " + namesBottom);

        Console.WriteLine("--- scrolled sideways ---");
        var rows = frame.Body.Controls.OfType<AxisSettingHMI>().OrderBy(r => r.Top).ToList();
        Func<AxisSettingHMI, Label> nameOf = r => (Label)typeof(AxisSettingHMI).GetProperty("NameLabel", Any).GetValue(r, null);
        int right0 = nameOf(rows[0]).PointToScreen(Point.Empty).X + nameOf(rows[0]).Width;
        frame.Body.AutoScrollPosition = new Point(350, 0);
        Pump(300);
        var name = nameOf(rows[0]);
        Check("the axis names stay at the left", rows.Take(5).All(r => AxisSettingHMI.IsStuck(nameOf(r)) &&
              nameOf(r).PointToScreen(Point.Empty).X + nameOf(r).Width - nameOf(r).Padding.Right == right0),
              name.PointToScreen(Point.Empty).X + "+" + name.Width + "-" + name.Padding.Right + " vs " + right0);
        Check("  as tall as the rows at the scale", rows.Take(5).All(r => nameOf(r).Height == r.Height && nameOf(r).Top == 0));

        form.Close();
        Console.WriteLine(fail == 0 ? "\nAXIS PAGE SCALE ALL PASS" : "\nAXIS PAGE SCALE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
