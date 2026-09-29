using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.UI.Controls;

// The axis unit and the function unit on a manual page must read as ONE family:
//   * the same default size renders the same frame, with or without a position
//     display and whatever error bits either has;
//   * every unit's position reads like the Axis Settings row: black bold on
//     the read-only grey, not the red-on-black readout;
//   * an error puts a red "?" on the TOP frame line, right end, and blinks the
//     border of what failed - the display on an axis, the button on a cylinder.
// Renders to /tmp/unitlook/*.png so a human can look as well.
class U {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static T Field<T>(object o, string n) { return (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o); }

    // From the SCREEN (the test runs on Xvfb): Mono's DrawToBitmap leaves child
    // controls out, and the unit is nothing but child controls.
    static Bitmap Shot(Control c) {
        for (int i = 0; i < 5; i++) { c.FindForm().Refresh(); Application.DoEvents(); System.Threading.Thread.Sleep(30); }
        var b = new Bitmap(c.Width, c.Height);
        var p = c.PointToScreen(Point.Empty);
        using (var g = Graphics.FromImage(b)) g.CopyFromScreen(p, Point.Empty, c.Size);
        return b;
    }

    // Right-most column holding a frame-grey pixel on the unit's middle row: where the frame ends.
    static int FrameRight(Bitmap b) {
        int y = b.Height / 2;
        for (int x = b.Width - 1; x >= 0; x--) {
            var p = b.GetPixel(x, y);
            if (p.R < 225 && Math.Abs(p.R - p.G) < 12 && Math.Abs(p.G - p.B) < 12) return x;
        }
        return -1;
    }

    // Freezes both blinks in their RED half so the picture shows the border.
    static void ForceRedPhase() {
        foreach (var t in new[] { "_alertTimer" }) {
            var tm = typeof(UiStyle).GetField(t, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) as Timer;
            if (tm != null) tm.Stop();
            var bt = typeof(ButtonHMI).GetField(t, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) as Timer;
            if (bt != null) bt.Stop();
        }
        typeof(UiStyle).GetProperty("AlertPhase").GetSetMethod(true).Invoke(null, new object[] { true });
        typeof(ButtonHMI).GetField("_alertPhase", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, true);
    }

    // Paints the control straight into a bitmap and counts clearly red pixels.
    static int RedPixels(Control c) {
        using (var b = new Bitmap(c.Width, c.Height))
        {
            using (var g = Graphics.FromImage(b))
                typeof(Control).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(c, new object[] { new PaintEventArgs(g, new Rectangle(0, 0, c.Width, c.Height)) });
            int red = 0;
            for (int x = 0; x < b.Width; x++) for (int y = 0; y < b.Height; y++)
            { var p = b.GetPixel(x, y); if (p.R > 180 && p.G < 80 && p.B < 80) red++; }
            return red;
        }
    }

    // Visible marks on a unit: its "?" - the two error-bit watchers never show.
    static int CountMarks(Control unit) {
        int n = 0;
        foreach (Control c in unit.Controls) if ((c is UnitHelpBadge || c is AlarmHelpBadge) && c.Visible) n++;
        return n;
    }

    [STAThread]
    static void Main() {
        System.IO.Directory.CreateDirectory("/tmp/unitlook");
        var host = new Form { ClientSize = new Size(820, 260), BackColor = Color.White,
                              StartPosition = FormStartPosition.Manual, Location = new Point(0, 0),
                              FormBorderStyle = FormBorderStyle.None };

        // An axis unit as the page builds one once its axis is resolved: a
        // CylinderUnitHMI of Kind Axis with the axis row's slot widths, a position
        // and ONE error bit. (AxisUnitHMI itself needs the discovered Axis Settings
        // pages; its constructor settings are checked separately below.)
        var axisHost = new AxisUnitHMI { Location = new Point(10, 10) };      // default size
        var axis = axisHost.Unit;
        axis.TitleTextKey = "Cutting Z";
        axis.CommandAAddress = "M600"; axis.CommandBAddress = "M601";
        axis.ButtonSlotWidth = AxisSettingHMI.WJog; axis.PositionSlotWidth = AxisSettingHMI.WPosition;
        axis.PositionAddress = "D100"; axis.PositionDecimalPlaces = 2; axis.PositionDigits = 8; axis.PositionSuffix = "mm";
        axis.ErrorBAddress = "M890";

        var fuPlain = new CylinderUnitHMI { Location = new Point(10, 90), TitleTextKey = "Saw Motor",
                                            CommandBAddress = "M636" };              // default size, no display
        var fuPos = new CylinderUnitHMI { Location = new Point(400, 90), TitleTextKey = "Left Top Clamping",
                                          CommandAAddress = "M672", CommandBAddress = "M671",
                                          PositionAddress = "D15", PositionDecimalPlaces = 1, PositionDigits = 6,
                                          ErrorAAddress = "M892", ErrorBAddress = "M891" };
        host.Controls.Add(axisHost); host.Controls.Add(fuPlain); host.Controls.Add(fuPos);
        host.Show(); Application.DoEvents();

        Console.WriteLine("--- same default size, same frame ---");
        Check("the axis unit's default size is the function unit's", axisHost.Size == fuPlain.Size,
              axisHost.Size + " vs " + fuPlain.Size);
        Check("no side room taken for error bits (axis)", axis.Padding == Padding.Empty, axis.Padding.ToString());
        Check("no side room taken for error bits (cylinder)", fuPos.Padding == Padding.Empty, fuPos.Padding.ToString());
        using (var a = Shot(axisHost)) using (var f1 = Shot(fuPlain)) using (var f2 = Shot(fuPos)) {
            a.Save("/tmp/unitlook/axis.png", ImageFormat.Png);
            f1.Save("/tmp/unitlook/fu_plain.png", ImageFormat.Png);
            f2.Save("/tmp/unitlook/fu_position.png", ImageFormat.Png);
            int ra = FrameRight(a), r1 = FrameRight(f1), r2 = FrameRight(f2);
            Check("frames end at the same x: axis / plain unit / unit with a display",
                  Math.Abs(ra - r1) <= 1 && Math.Abs(r1 - r2) <= 1, ra + " / " + r1 + " / " + r2);
        }

        Console.WriteLine("--- the axis slots fill the frame at any width ---");
        var btnB = axis.ButtonB;
        int rightGap = axis.ClientSize.Width - btnB.Right;
        Check("the right jog button ends at the frame's inner edge, not short of it", rightGap <= 10,
              "gap=" + rightGap + " (unit " + axis.ClientSize.Width + ")");
        axisHost.Width = 248; Application.DoEvents();
        Check("at the generated 248 px it keeps the axis row's jog width", Math.Abs(axis.ButtonA.Width - AxisSettingHMI.WJog) <= 1,
              axis.ButtonA.Width.ToString());
        axisHost.Width = 360; Application.DoEvents();

        Console.WriteLine("--- the axis reading looks like the Axis Settings row ---");
        var pos = axis.PositionDisplay;
        Check("style is Standard", axis.PositionStyle == UnitPositionStyle.Standard);
        Check("black digits", pos.ForeColor.ToArgb() == Color.Black.ToArgb(), pos.ForeColor.ToString());
        Check("on the read-only grey of the axis row", pos.BackColor.ToArgb() == new NumericDisplayHMI().BackColor.ToArgb(), pos.BackColor.ToString());
        Check("bold, the axis row's size", pos.Font.Bold && Math.Abs(pos.Font.Size - 12f) < 0.1f, pos.Font.ToString());
        Check("a function unit's position reads the same way: black bold on the grey (no more red-on-black)",
              fuPos.PositionDisplay.ForeColor.ToArgb() == Color.Black.ToArgb() &&
              fuPos.PositionDisplay.BackColor.ToArgb() == pos.BackColor.ToArgb() &&
              fuPos.PositionDisplay.Font.Bold && Math.Abs(fuPos.PositionDisplay.Font.Size - 12f) < 0.1f,
              fuPos.PositionDisplay.ForeColor + " on " + fuPos.PositionDisplay.BackColor + ", " + fuPos.PositionDisplay.Font);

        Console.WriteLine("--- an axis error: '?' top right, the DISPLAY's border blinks ---");
        var axisHelp = Field<AlarmHelpBadge>(axis, "_helpB");
        var mark = Field<UnitHelpBadge>(axis, "_help");
        axisHelp.UpdateState(x => x == "M890" ? 1 : 0, c => true);
        Application.DoEvents();
        Check("the '?' shows", mark.Visible && axisHelp.IsActive);
        Check("  on the top frame line", mark.Top <= 4, "top=" + mark.Top);
        Check("  at its right end", mark.Right >= axis.ClientSize.Width - 16, mark.Right + " of " + axis.ClientSize.Width);
        Check("the display's border blinks", pos.AlertBlink);
        Check("the jog buttons do not", !axis.ButtonA.AlertBlink && !axis.ButtonB.AlertBlink);
        Check("ONE mark on the unit - no separate 'i' beside the '?'",
              CountMarks(axis) == 1);
        ForceRedPhase();
        Check("  and in the red half it really DRAWS a red border", RedPixels(pos) > 100, RedPixels(pos) + " red px");
        using (var a = Shot(axisHost)) a.Save("/tmp/unitlook/axis_error.png", ImageFormat.Png);
        axisHelp.UpdateState(x => 0, c => true);
        Check("clears with the error", !pos.AlertBlink && !mark.Visible);

        Console.WriteLine("--- a cylinder error: its BUTTON's border blinks ---");
        var helpA = Field<AlarmHelpBadge>(fuPos, "_helpA");
        helpA.UpdateState(x => x == "M892" ? 1 : 0, c => true);
        Check("button A blinks", fuPos.ButtonA.AlertBlink);
        Check("button B and the display do not", !fuPos.ButtonB.AlertBlink && !fuPos.PositionDisplay.AlertBlink);
        ForceRedPhase();
        using (var f = Shot(fuPos)) f.Save("/tmp/unitlook/fu_error.png", ImageFormat.Png);
        helpA.UpdateState(x => 0, c => true);

        host.Close();
        Console.WriteLine(fail == 0 ? "\nUNIT LOOK ALL PASS" : "\nUNIT LOOK " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
