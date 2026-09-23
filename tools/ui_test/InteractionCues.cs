using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.UI.Controls;

class C {
    static int fail = 0;
    static void Check(string what, bool ok, string d="") {
        Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+what+(d.Length>0?"   ["+d+"]":""));
        if (!ok) fail++;
    }
    static void Raise(Control c, string ev) {
        // MouseEnter/Leave are protected OnX - invoke them the way WinForms would
        var m = typeof(Control).GetMethod("On"+ev, BindingFlags.NonPublic|BindingFlags.Instance);
        m.Invoke(c, new object[]{ EventArgs.Empty });
    }

    static void Main() {
        // A page shaped like a real one: a group box containing a text box,
        // a combo, a standard button and an HMI button.
        var page = new UserControl { Size = new Size(400, 300) };
        var group = new GroupBoxHMI { Size = new Size(380, 200), Location = new Point(5,5) };
        var txt   = new TextBox   { Size = new Size(120, 24), Location = new Point(10, 30) };
        var combo = new ComboBox  { Size = new Size(120, 24), Location = new Point(10, 60) };
        var std   = new Button    { Size = new Size(80, 30),  Location = new Point(10, 90) };
        var hmi   = new ButtonHMI { Size = new Size(120, 36), Location = new Point(10, 130) };
        group.Controls.Add(txt); group.Controls.Add(combo);
        group.Controls.Add(std); group.Controls.Add(hmi);
        page.Controls.Add(group);

        UiStyle.ApplyInteractionCues(page);

        Console.WriteLine("--- cursor follows Enabled ---");
        Check("enabled button shows a hand", std.Cursor == Cursors.Hand, std.Cursor.ToString());
        Check("enabled combo shows a hand", combo.Cursor == Cursors.Hand, combo.Cursor.ToString());
        Check("text box keeps the I-beam, not a hand", txt.Cursor == Cursors.IBeam, txt.Cursor.ToString());

        std.Enabled = false;
        Check("DISABLED button falls back to the arrow", std.Cursor == Cursors.Default, std.Cursor.ToString());
        txt.Enabled = false;
        Check("DISABLED text box falls back to the arrow", txt.Cursor == Cursors.Default, txt.Cursor.ToString());
        std.Enabled = true;
        Check("re-enabled button gets the hand back", std.Cursor == Cursors.Hand, std.Cursor.ToString());

        Console.WriteLine("--- the container must NOT claim the pointer ---");
        // Render the group with the pointer 'over' the combo, and confirm no blue
        // appears on the group's own frame line.
        Raise(combo, "MouseEnter");
        Check("group box itself is never wired for hover",
              !HasBlueOnFrame(group), "no blue on the group frame");

        Console.WriteLine("--- standard controls get an outline drawn by the parent ---");
        Check("hovering a combo paints blue on its parent",
              ParentPaintsBlueAround(group, combo), "outline found around the combo");
        Raise(combo, "MouseLeave");
        Check("leaving removes it", !ParentPaintsBlueAround(group, combo));

        Raise(txt, "MouseEnter");
        Check("hovering a text box paints blue too", ParentPaintsBlueAround(group, txt));
        Raise(txt, "MouseLeave");

        Console.WriteLine("--- controls that draw their own border are not outlined twice ---");
        Check("ButtonHMI declares it draws its own",
              hmi is UiStyle.IDrawsOwnHoverBorder);
        Raise(hmi, "MouseEnter");
        Check("no parent outline around ButtonHMI", !ParentPaintsBlueAround(group, hmi));
        Raise(hmi, "MouseLeave");

        Console.WriteLine("--- re-applying does not stack handlers ---");
        for (int i = 0; i < 5; i++) UiStyle.ApplyInteractionCues(page);
        Raise(combo, "MouseEnter");
        Check("outline still exactly one pixel-run thick",
              ParentPaintsBlueAround(group, combo), "no doubling");

        Console.WriteLine("--- lamp clears the ButtonHMI border ---");
        var lit = new ButtonHMI { Size = new Size(160, 40), Lamp1Address = "X10", Lamp2Address = "X11" };
        int gap = LampGapFromBorder(lit);
        Check("lamp is clear of the button frame", gap >= 3, gap + "px of background between them");
        Check("but still reads as INSIDE the button, not floating", gap <= 8, gap + "px");

        Console.WriteLine(fail==0 ? "\nCUES ALL PASS" : "\nCUES " + fail + " FAILED");
        Environment.Exit(fail==0?0:1);
    }

    // Render the parent and look for the hover blue immediately outside a child.
    static bool ParentPaintsBlueAround(Control parent, Control child) {
        using (var bmp = new Bitmap(parent.Width, parent.Height))
        using (var g = Graphics.FromImage(bmp)) {
            g.Clear(Color.White);
            RaisePaint(parent, g);
            var r = child.Bounds; r.Inflate(2,2);
            for (int x = Math.Max(0,r.Left); x < Math.Min(bmp.Width, r.Right); x++) {
                if (Near(bmp.GetPixel(x, Math.Max(0,r.Top)))) return true;
            }
            return false;
        }
    }
    static bool HasBlueOnFrame(Control group) {
        using (var bmp = new Bitmap(group.Width, group.Height))
        using (var g = Graphics.FromImage(bmp)) {
            g.Clear(Color.White);
            RaisePaint(group, g);
            for (int x = 0; x < bmp.Width; x++)
                for (int y = 0; y < bmp.Height; y += Math.Max(1, bmp.Height-1))
                    if (Near(bmp.GetPixel(x,y))) return true;
            return false;
        }
    }
    /// Measures the clear background between the button's frame and the lamp.
    /// Scanned at ONE QUARTER height, not the midline: with two lamps configured
    /// the midline falls exactly in the split between them, which is empty and
    /// made this read 0px.
    static int LampGapFromBorder(ButtonHMI b) {
        using (var bmp = new Bitmap(b.Width, b.Height))
        using (var g = Graphics.FromImage(bmp)) {
            g.Clear(Color.Magenta);                       // unpainted = magenta
            RaisePaint(b, g);
            int y = b.Height / 4;
            int x = 0;
            while (x < b.Width && IsBare(bmp.GetPixel(x, y))) x++;   // up to the frame
            while (x < b.Width && !IsBare(bmp.GetPixel(x, y))) x++;  // across the frame
            int gap = 0;
            while (x < b.Width && IsBare(bmp.GetPixel(x, y))) { gap++; x++; }
            return x >= b.Width ? -1 : gap;               // -1 = never found the lamp
        }
    }
    static bool IsBare(Color c) { return c.R > 240 && c.G < 40 && c.B > 240; }

    static void RaisePaint(Control c, Graphics g) {
        var m = typeof(Control).GetMethod("OnPaint", BindingFlags.NonPublic|BindingFlags.Instance);
        m.Invoke(c, new object[]{ new PaintEventArgs(g, c.ClientRectangle) });
    }
    static bool Near(Color c) {
        var t = UiStyle.HoverBorderColor;
        return Math.Abs(c.R-t.R)<60 && Math.Abs(c.G-t.G)<60 && Math.Abs(c.B-t.B)<60 && c.B > c.R + 40;
    }
}

