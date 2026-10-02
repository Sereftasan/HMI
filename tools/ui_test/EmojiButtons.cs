using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.UI.Controls;

// "Change export emojis to the outbox tray (the tray blue, the arrow red) - the
// current one looks too blurred in only grey - the save emojis to the floppy disk
// colours, make all Undo and Redo buttons the same as the one on the PLC Monitor
// page. All textless button emojis colourful (the pale ones, not too sparkling)."
class EB {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }

    static Bitmap Shot(Control c) {
        var bmp = new Bitmap(c.Width, c.Height);
        c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
        return bmp;
    }

    // Pixels of a colour family inside the button (its frame left out).
    static int Count(Bitmap b, Func<Color, bool> f) {
        int n = 0;
        for (int y = 4; y < b.Height - 4; y++)
            for (int x = 4; x < b.Width - 4; x++) if (f(b.GetPixel(x, y))) n++;
        return n;
    }
    static bool Saturated(Color c) { return c.GetSaturation() > 0.35f && c.GetBrightness() > 0.2f && c.GetBrightness() < 0.92f; }
    static bool Red(Color c) { return Saturated(c) && (c.GetHue() < 20 || c.GetHue() > 335); }
    static bool Blue(Color c) { return Saturated(c) && c.GetHue() > 190 && c.GetHue() < 235; }
    static bool DarkGrey(Color c) { return c.GetSaturation() < 0.12f && c.GetBrightness() < 0.45f; }

    [STAThread]
    static void Main() {
        Console.WriteLine("--- the pictures ---");
        Check("the export tray has its picture", EmojiArt.Has("\uD83D\uDCE4"));
        Check("the floppy disk too", EmojiArt.Has("\uD83D\uDCBE"));
        Check("with or without the emoji selector (gear)", EmojiArt.Has("\u2699") && EmojiArt.Has("\u2699\uFE0F"), EmojiArt.KeyOf("\u2699\uFE0F"));
        Check("Stop's black square drawn as the stop button", EmojiArt.Has("\u25A0"));
        Check("a letter, a word: no picture", !EmojiArt.Has("A") && !EmojiArt.Has("Save") && !EmojiArt.Has(""));
        Check("the mirror arrow is no emoji: no picture", !EmojiArt.Has("\u21CB"));
        Check("every picture of every toolbar emoji of the program",
              new[] { "\uD83D\uDCC2", "\uD83D\uDCBE", "\u270F\uFE0F", "\u2699", "\uD83D\uDCCB", "\uD83D\uDCE4", "\u25B6", "\uD83D\uDD0D",
                      "\u25A0", "\uD83D\uDD04", "\u2716", "\uD83D\uDDD1", "\uD83D\uDCDA", "\u2714", "\u2795", "\uD83D\uDCC4", "\uD83D\uDC41",
                      "\uD83E\uDDEE", "\uD83D\uDD58", "\uD83C\uDFAF", "\uD83D\uDCCD", "\uD83D\uDD29", "\uD83D\uDCCF", "\u27B0", "\uD83D\uDFE2",
                      "\u2705", "\u26D3", "\u26D4", "\u2702", "\uD83D\uDD0E", "\uD83D\uDCD1" }.All(EmojiArt.Has));
        Check("the sizes", EmojiArt.Sizes.SequenceEqual(new[] { 16, 20, 24, 28, 32, 40, 48 }), string.Join(",", EmojiArt.Sizes));
        Check("about a line of the toolbar's font (Segoe UI 12)", Math.Abs(EmojiArt.SizeFor(new Font("Segoe UI", 12F), 26) - 20) <= 3,
              EmojiArt.SizeFor(new Font("Segoe UI", 12F), 26).ToString());

        Console.WriteLine("--- the buttons ---");
        var form = new Form { ClientSize = new Size(600, 120), BackColor = Color.White };
        var export = UiStyle.ToolButton("\uD83D\uDCE4", "Export", null);
        var save = UiStyle.ToolButton("\uD83D\uDCBE", "Save", null);
        var revert = UiStyle.ToolButton("\u21BA", "Throw the changes away", null);
        var undo = UiStyle.ToolButton("\u21B6", "Undo", null);
        var redo = UiStyle.ToolButton("\u21B7", "Redo", null);
        var mirror = UiStyle.ToolButton("\u21CB", "Mirror", null);
        var off = UiStyle.ToolButton("\uD83D\uDCE4", "Export", null);
        off.Enabled = false;
        var all = new Button[] { export, save, revert, undo, redo, mirror, off };
        for (int i = 0; i < all.Length; i++) { all[i].Location = new Point(8 + i * 50, 8); form.Controls.Add(all[i]); }
        form.Show(); Application.DoEvents();

        Check("a toolbar button is an EmojiButton, its text the emoji as before", export is EmojiButton && export.Text == "\uD83D\uDCE4");
        Check("  the picture, not the letter", ((EmojiButton)export).Look == "art");
        using (var b = Shot(export))
            Check("  the tray blue, the arrow red: in colour", Count(b, Red) > 6 && Count(b, Blue) > 10 && Count(b, DarkGrey) < 6,
                  "red " + Count(b, Red) + ", blue " + Count(b, Blue) + ", dark grey " + Count(b, DarkGrey));
        using (var b = Shot(save))
            Check("the floppy disk in colour (its red label)", Count(b, Red) > 3, "red " + Count(b, Red));
        using (var b = Shot(off))
            Check("disabled: grey and faint", Count(b, Saturated) == 0, Count(b, Saturated).ToString());
        Check("Undo, Redo and throw-away: the PLC Monitor's curls",
              ((EmojiButton)undo).Look == "glyph" && ((EmojiButton)redo).Look == "glyph" && ((EmojiButton)revert).Look == "glyph"
              && EmojiArt.GlyphFor("\u21B6") == (Action<Graphics, Rectangle, bool>)ToolGlyphs.Undo
              && EmojiArt.GlyphFor("\u21B7") == (Action<Graphics, Rectangle, bool>)ToolGlyphs.Redo
              && EmojiArt.GlyphFor("\u21BA") == (Action<Graphics, Rectangle, bool>)ToolGlyphs.Undo
              && EmojiArt.GlyphFor("\u21A9") == (Action<Graphics, Rectangle, bool>)ToolGlyphs.Undo);
        Func<Color, bool> ink = c => Math.Abs(c.R - ToolGlyphs.Ink.R) < 40 && Math.Abs(c.G - ToolGlyphs.Ink.G) < 40 && Math.Abs(c.B - ToolGlyphs.Ink.B) < 40;
        using (var b = Shot(undo)) using (var m = new Bitmap(44, 32))
        {
            using (var g = Graphics.FromImage(m)) { g.Clear(Color.White); ToolGlyphs.Undo(g, new Rectangle(0, 0, 44, 32), true); }
            Check("  drawn in the PLC Monitor's blue", Count(b, ink) > 20 && Math.Abs(Count(b, ink) - Count(m, ink)) < Count(m, ink) / 2,
                  Count(b, ink) + " / " + Count(m, ink));
        }
        Check("a sign that is no emoji (mirror): in the toolbar's blue", ((EmojiButton)mirror).Look == "text" && mirror.ForeColor == ToolGlyphs.Ink);

        Console.WriteLine("--- a PLC button with an emoji caption (the alarm page's reset) ---");
        var reset = new ButtonHMI { FallbackText = "\uD83D\uDD04", Width = 36, Height = 30, Location = new Point(8, 60) };
        form.Controls.Add(reset); reset.ApplyTranslation(); Application.DoEvents();
        // (washed over here: no PLC answers in the test, the button is soft-disabled)
        Func<Color, bool> bluish = c => c.GetSaturation() > 0.15f && c.GetHue() > 180 && c.GetHue() < 240;
        using (var b = Shot(reset))
            Check("its picture: the blue arrows button", Count(b, bluish) > 40, "blue " + Count(b, bluish));

        // A picture of the toolbars for the review.
        var row = new Form { ClientSize = new Size(31 * 48 + 16, 48), BackColor = Color.White };
        string[] each = { "\uD83D\uDCC2", "\uD83D\uDCBE", "\uD83D\uDCE4", "\u270F\uFE0F", "\u2699", "\uD83D\uDCCB", "\u25B6", "\u25A0", "\uD83D\uDD0D",
                          "\uD83D\uDD04", "\u2716", "\u2714", "\u2795", "\uD83D\uDDD1", "\uD83D\uDCDA", "\uD83D\uDCC4", "\uD83D\uDC41", "\uD83E\uDDEE",
                          "\uD83D\uDD58", "\uD83C\uDFAF", "\uD83D\uDCCD", "\uD83D\uDD29", "\uD83D\uDCCF", "\u21B6", "\u21B7", "\u21BA", "\u27F3", "\u21CB",
                          "\u2702", "\u26D4", "\u2705" };
        for (int i = 0; i < each.Length; i++) { var t = UiStyle.ToolButton(each[i], "", null); t.Location = new Point(8 + i * 48, 8); row.Controls.Add(t); }
        row.Show(); Application.DoEvents();
        using (var b = new Bitmap(row.ClientSize.Width, row.ClientSize.Height))
        {
            using (var g = Graphics.FromImage(b)) g.Clear(Color.White);
            foreach (Control t in row.Controls) t.DrawToBitmap(b, t.Bounds);
            b.Save("/tmp/emoji_buttons.png");
        }
        row.Close();
        form.Close();

        Console.WriteLine(fail == 0 ? "\nEMOJI BUTTONS ALL PASS" : "\nEMOJI BUTTONS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
