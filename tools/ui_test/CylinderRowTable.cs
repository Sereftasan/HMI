using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// 9 October, "150% zoom: the Clamping Moulds page scrolls sideways - on the axis page the names and
// headings stay in view. Should I do the same here? Yes": a page of cylinder rows (PneumaticPositioningHMI,
// CylinderEncoderSettingHMI) is a table once shown, as an axis page is (AxisPageFrame) - its headings stay at
// the top while the rows scroll up and down, the rows' names stay at the left while they scroll sideways.
class CRT {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static Rectangle Scr(Control c) { return c.RectangleToScreen(c.ClientRectangle); }
    static void Shot(string name, int w, int h) {
        var dir = Environment.GetEnvironmentVariable("SHOT_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("import", "-window root -crop " + w + "x" + h + "+0+0 " + Path.Combine(dir, name)) { UseShellExecute = false });
        p.WaitForExit(10000);
    }

    [STAThread]
    static void Main() {
        UiScale.Override(1.5);
        var dir = Path.Combine(Path.GetTempPath(), "crt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.LoadMachineTranslations("Translations.csv");
        UnitManager.EnsureDefaults();
        int fw = 1270, fh = 700;

        Console.WriteLine("--- Manual Movement > Clamping Moulds at 1.5, in a window narrower and lower than it ---");
        var form = new Form { ClientSize = new Size(fw, fh), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0), FormBorderStyle = FormBorderStyle.None };
        var page = new Kaban.Machines.FA2010.Screens.Manual.ManualClamping_Moulds { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        UiScale.ApplyOnLoad(form);
        form.Show(); Pump(1000);
        form.ClientSize = new Size(fw, fh); Pump(300);

        var frame = AxisPageFrame.Of(page);
        Check("opened at its left top, its margin in sight (not scrolled by the focus its first row got)", frame != null &&
              frame.Body.AutoScrollPosition == Point.Empty && PneumaticPositioningHMI.RowsOf(page).All(r => !r.NameStuck),
              frame == null ? "" : frame.Body.AutoScrollPosition.ToString());
        Check("the page a table (AxisPageFrame): the rows in its body, the headings in its heading", frame != null &&
              PneumaticPositioningHMI.RowsOf(page).All(r => r.Parent == frame.Body) &&
              frame.HeadContent.Controls.OfType<Label>().Count(l => (l.Tag as string ?? "").StartsWith(PneumaticPositioningHMI.HeadingTag)) == 5);
        if (frame == null) { Environment.Exit(1); return; }
        Check("  the page itself no longer scrolls - its body does, both ways", !page.AutoScroll && frame.Body.AutoScroll &&
              frame.Body.DisplayRectangle.Width > frame.Body.ClientSize.Width && frame.Body.DisplayRectangle.Height > frame.Body.ClientSize.Height,
              frame.Body.DisplayRectangle.Size + " in " + frame.Body.ClientSize);
        var rows = PneumaticPositioningHMI.RowsOf(page);
        var heads = frame.HeadContent.Controls.OfType<Label>().Where(l => (l.Tag as string ?? "").StartsWith(PneumaticPositioningHMI.HeadingTag)).ToList();
        var first = rows[0];
        Check("  the headings over their columns, right above the first row", heads.All(h => {
                  var col = first.Columns[((string)h.Tag).Substring(PneumaticPositioningHMI.HeadingTag.Length)];
                  return Math.Abs(Scr(h).Left - (Scr(first).Left + UiScale.Px(col.X))) <= 1 && Scr(h).Bottom <= Scr(first).Top &&
                         Scr(first).Top - Scr(h).Bottom <= UiScale.Px(12); }),
              string.Join(" ", heads.Select(h => h.Text + "@" + Scr(h).Left)));
        Check("  no heading made twice (the page's own five, moved)", page.Controls.Find("lblHead_Target", true).Length == 1 &&
              heads.Count == 5);
        Shot("moulds_150_top.png", fw, fh);

        Console.WriteLine("--- scrolled down ---");
        var headTops = heads.Select(h => Scr(h).Top).ToList();
        int firstTop = Scr(first).Top;
        frame.Body.AutoScrollPosition = new Point(0, UiScale.Px(200));
        Pump(300);
        Check("the headings stay where they were", heads.Select(h => Scr(h).Top).SequenceEqual(headTops));
        Check("  the rows went up under them", Scr(first).Top < firstTop, firstTop + " -> " + Scr(first).Top);
        frame.Body.AutoScrollPosition = Point.Empty;
        Pump(200);

        Console.WriteLine("--- scrolled sideways ---");
        var nameRight0 = rows.Select(r => Scr(r.NameLabel).Right).ToList();
        int target0 = Scr(heads.First(h => (string)h.Tag == PneumaticPositioningHMI.HeadingTag + "Target")).Left;
        int s = UiScale.Px(200);
        frame.Body.AutoScrollPosition = new Point(s, 0);
        Pump(300);
        int scrolled = -frame.Body.AutoScrollPosition.X;
        Check("scrolled " + s + " px", scrolled == s, scrolled.ToString());
        Check("  the headings went along with the rows",
              Scr(heads.First(h => (string)h.Tag == PneumaticPositioningHMI.HeadingTag + "Target")).Left == target0 - scrolled);
        bool kept = true; string why = "";
        for (int i = 0; i < rows.Count; i++)
        {
            var n = rows[i].NameLabel;
            int textRight = Scr(n).Right - n.Padding.Right;
            if (!rows[i].NameStuck || textRight != nameRight0[i]) { kept = false; why += rows[i].Name + " " + textRight + " vs " + nameRight0[i] + "; "; }
        }
        Check("  every name stays where it was, at the left", kept, why);
        Check("  over the whole height of its row, from the page's left edge", rows.All(r => r.NameLabel.Top == 0 && r.NameLabel.Height == r.Height &&
              Scr(r.NameLabel).Left == Scr(frame.Body).Left));
        Check("  over the columns passing under it (in front, opaque)", rows.All(r => r.Controls.GetChildIndex(r.NameLabel) == 0 && r.NameLabel.BackColor.A == 255));
        var cover = frame.HeadContent.Controls[CylinderRowHMI.NameCoverName] as Label;
        Check("  the room over the names kept clear in the heading: no column name passing over them",
              cover != null && cover.Visible && Scr(cover).Left == Scr(frame.Head).Left && Scr(cover).Right >= Scr(first.NameLabel).Right - 1 &&
              frame.HeadContent.Controls.GetChildIndex(cover) == 0,
              cover == null ? "none" : Scr(cover) + " names end at " + Scr(first.NameLabel).Right);
        Check("  nothing to note on it (a tap there is about no control)", cover == null || NoteTarget.Notable(cover) != cover);
        Shot("moulds_150_sideways.png", fw, fh);

        Console.WriteLine("--- the back moulds out (the option off), still scrolled ---");
        Func<SystemCondition, bool> off = c => ((long)c & (1L << 19)) == 0;
        foreach (var r in rows) r.UpdateState(a => 0, off);
        Pump(300);
        var shown = rows.Where(r => r.Visible).OrderBy(r => r.Top).ToList();
        Check("the 6 front moulds moved up in the body", shown.Count == 6 && Math.Abs(Scr(shown[0]).Top - Scr(first).Top) <= 1 &&
              shown.Zip(shown.Skip(1), (a, b) => b.Top - a.Bottom).All(g => g == UiScale.Px(4)),
              string.Join(" ", shown.Select(r => r.Top)));
        Check("  their names still at the left", shown.All(r => r.NameStuck && Scr(r.NameLabel).Left == Scr(frame.Body).Left));
        foreach (var r in rows) r.UpdateState(a => 0, c => true);
        Pump(200);

        Console.WriteLine("--- back to the left ---");
        frame.Body.AutoScrollPosition = Point.Empty;
        Pump(300);
        Check("the names back in their place, nothing stuck", rows.All(r => !r.NameStuck && r.NameLabel.Left == 0 &&
              r.NameLabel.Top == UiScale.Px(8) && Scr(r.NameLabel).Right == nameRight0[rows.IndexOf(r)]));
        Check("  the room over them shown no more", cover == null || !cover.Visible);

        Console.WriteLine("--- what the program says about a heading in the table ---");
        string key;
        var targetHead = heads.First(h => (string)h.Tag == PneumaticPositioningHMI.HeadingTag + "Target");
        Check("a heading knows its rows (in the body) and its column", CylinderRowHMI.RowOfHeading(targetHead, out key) != null && key == "Target");
        Check("  its key: Col_PpTarget", NoteTarget.KeysOf(targetHead).Any(k => k.Key == "Col_PpTarget"));
        Check("  a field's place the same as on a plain page (the frame's panels not in it)",
              !NoteTarget.KeyOf(first.TargetField).Contains("axisPage") && NoteTarget.KeyOf(first.TargetField).EndsWith("ppLeft_Profile_Recognition_Front_Vertical/ppTarget"), NoteTarget.KeyOf(first.TargetField));

        Console.WriteLine("--- in Turkish ---");
        LanguageManager.ChangeLanguage("TR");
        Pump(300);
        heads = frame.HeadContent.Controls.OfType<Label>().Where(l => (l.Tag as string ?? "").StartsWith(PneumaticPositioningHMI.HeadingTag)).ToList();
        Check("the headings translated where they are, none made again", heads.Count == 5 && targetHead.Text == "Hedef pozisyon" &&
              !CylinderRowHMI.RowsHost(page).Controls.OfType<Label>().Any(), targetHead.Text);
        first = rows[0];
        Check("  still over their columns (the names' column as wide as the Turkish names)", heads.All(h => {
                  var col = first.Columns[((string)h.Tag).Substring(PneumaticPositioningHMI.HeadingTag.Length)];
                  return Math.Abs(Scr(h).Left - (Scr(first).Left + UiScale.Px(col.X))) <= 1; }));
        LanguageManager.ChangeLanguage("EN");
        Pump(200);
        form.Close();

        Console.WriteLine("--- Machine Parameters > Encoders at 1.5 ---");
        var form2 = new Form { ClientSize = new Size(fw, fh), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0), FormBorderStyle = FormBorderStyle.None };
        var enc = new Kaban.Machines.FA2010.Screens.Parameters.ParamEncoders { Dock = DockStyle.Fill };
        form2.Controls.Add(enc);
        UiScale.ApplyOnLoad(form2);
        form2.Show(); Pump(1000);
        form2.ClientSize = new Size(fw, fh); Pump(300);
        var f2 = AxisPageFrame.Of(enc);
        var erows = CylinderEncoderSettingHMI.RowsOf(enc);
        Check("opened at its left top", f2 != null && f2.Body.AutoScrollPosition == Point.Empty && erows.All(r => !r.NameStuck));
        Check("a table too: 11 rows in its body, 5 headings in its heading", f2 != null && erows.Count == 11 && erows.All(r => r.Parent == f2.Body) &&
              f2.HeadContent.Controls.OfType<Label>().Count(l => (l.Tag as string ?? "").StartsWith(CylinderEncoderSettingHMI.HeadingTag)) == 5);
        if (f2 != null)
        {
            var right0 = erows.Select(r => Scr(r.NameLabel).Right).ToList();
            f2.Body.AutoScrollPosition = new Point(UiScale.Px(200), 0);
            Pump(300);
            Check("  scrolled sideways: the names stay at the left", -f2.Body.AutoScrollPosition.X > 0 &&
                  erows.Select((r, i) => r.NameStuck && Scr(r.NameLabel).Right - r.NameLabel.Padding.Right == right0[i]).All(b => b),
                  (-f2.Body.AutoScrollPosition.X) + " px");
            Shot("encoders_150_sideways.png", fw, fh);
        }
        form2.Close();

        Console.WriteLine(fail == 0 ? "\nCYLINDER ROW TABLE ALL PASS" : "\nCYLINDER ROW TABLE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
