using System;
using System.Drawing;
using System.Drawing.Imaging;
using Kaban.UI.Controls;

class G {
    [STAThread] static void Main(){
        // Render at the real button size, on the real caption-bar black, in both
        // blink phases - so the check is what the operator sees, not the code.
        Draw("warn_red.png",  34, Color.FromArgb(232, 60, 60), Color.Black, true);
        Draw("warn_white.png",34, Color.White,                 Color.Black, true);
        Draw("reset_blue.png",30, Color.FromArgb(40,110,190),  Color.White, false);
        Console.WriteLine("rendered");
    }
    static void Draw(string name, int size, Color ink, Color back, bool warning){
        using (var bmp = new Bitmap(size + 24, size + 8))
        using (var g = Graphics.FromImage(bmp)) {
            g.Clear(back);
            var r = new Rectangle(12, 4, size, size);
            if (warning) HmiGlyphs.Warning(g, r, ink); else HmiGlyphs.Reset(g, r, ink);
            bmp.Save("/tmp/glyph/" + name, ImageFormat.Png);

            // Count ink pixels: a glyph that failed to draw is easy to miss by eye
            // in a thumbnail but obvious as a zero here.
            int lit = 0;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    if (bmp.GetPixel(x, y).ToArgb() != back.ToArgb()) lit++;
            Console.WriteLine("{0,-16} {1} ink pixels", name, lit);
        }
    }
}

