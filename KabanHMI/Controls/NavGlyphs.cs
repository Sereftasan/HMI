using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// DRAWN PICTURES FOR THE LEFT BAR: HMI Settings and the machines.
    ///
    /// The gear is the Machine Parameters picture in every other Kaban HMI, so it
    /// moved there, and HMI Settings - the settings of the PROGRAM on this panel,
    /// not of a machine - became a screen with a small gear on it.
    ///
    /// A machine used to be the first letter of its name on a coloured tile. The
    /// tile stays (the colour tells the machines apart at a glance, the bar
    /// collapsed to pictures only) and the letter became what the machine does:
    /// a saw blade and a milling cutter for the processing centre, a screw for
    /// screwing and holing, a drill bit for hinge drilling.
    ///
    /// Drawn four times larger and reduced, so the small picture is smooth; each
    /// is made once and kept.
    /// </summary>
    public static class NavGlyphs
    {
        /// <summary>The ink of the other bar pictures (the PNG icons are this dark grey).</summary>
        public static readonly Color Ink = Color.FromArgb(31, 31, 31);

        private static readonly Dictionary<string, Image> _cache = new Dictionary<string, Image>();

        private const int Super = 4;   // drawn at 4x, reduced

        private static Image Make(string key, int size, Action<Graphics, float> draw)
        {
            string k = key + "|" + size;
            Image img;
            if (_cache.TryGetValue(k, out img)) return img;
            int big = size * Super;
            using (var large = new Bitmap(big, big))
            {
                using (var g = Graphics.FromImage(large))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);
                    // Every picture is designed on a 24 x 24 grid.
                    float u = big / 24f;
                    g.ScaleTransform(u, u);
                    draw(g, 1f);
                }
                var small = new Bitmap(size, size);
                using (var g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.DrawImage(large, new Rectangle(0, 0, size, size));
                }
                img = small;
            }
            _cache[k] = img;
            return img;
        }

        // ------------------------------------------------------------------
        // HMI SETTINGS: a screen on its stand, a gear on its lower right corner.
        // ------------------------------------------------------------------
        public static Image HmiSettings(int size = 24)
        {
            return Make("hmi", size, (g, _) =>
            {
                using (var pen = new Pen(Ink, 2f) { LineJoin = LineJoin.Round })
                {
                    // the screen - open at the lower right, where the gear sits
                    var path = new GraphicsPath();
                    path.AddLine(13.5f, 16f, 4.5f, 16f);
                    path.AddArc(2.5f, 12f, 4f, 4f, 90, 90);
                    path.AddLine(2.5f, 14f, 2.5f, 5.5f);
                    path.AddArc(2.5f, 3.5f, 4f, 4f, 180, 90);
                    path.AddLine(4.5f, 3.5f, 18.5f, 3.5f);
                    path.AddArc(16.5f, 3.5f, 4f, 4f, 270, 90);
                    path.AddLine(20.5f, 5.5f, 20.5f, 10.5f);
                    g.DrawPath(pen, path);
                }
                using (var pen = new Pen(Ink, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(pen, 8f, 20.5f, 13f, 20.5f);     // the foot
                    g.DrawLine(pen, 10.5f, 16.5f, 10.5f, 20f);  // the stand
                }
                Gear(g, 18.2f, 17.2f, 5.3f, 3.7f, 1.6f, 8, Ink);
            });
        }

        /// <summary>A gear: teeth between the two radii, a hole in the middle.</summary>
        private static void Gear(Graphics g, float cx, float cy, float rOut, float rIn, float rHole, int teeth, Color ink)
        {
            var pts = new List<PointF>();
            int n = teeth * 4;
            for (int i = 0; i < n; i++)
            {
                // tooth: two points out, two in
                double a0 = 2 * Math.PI * i / n - Math.PI / n;
                float r = (i % 4 == 0 || i % 4 == 1) ? rOut : rIn;
                pts.Add(new PointF(cx + (float)(r * Math.Cos(a0)), cy + (float)(r * Math.Sin(a0))));
            }
            using (var path = new GraphicsPath { FillMode = FillMode.Alternate })
            {
                path.AddPolygon(pts.ToArray());
                path.AddEllipse(cx - rHole, cy - rHole, rHole * 2, rHole * 2);
                using (var b = new SolidBrush(ink)) g.FillPath(b, path);
            }
        }

        // ------------------------------------------------------------------
        // THE MACHINES: a white picture on the machine's coloured tile.
        // ------------------------------------------------------------------

        /// <summary>The processing centre: a circular saw blade, and a milling cutter above it.</summary>
        public static Image ProcessingCenter(Color tile, int size = 24)
        {
            return Make("pc" + tile.ToArgb(), size, (g, _) =>
            {
                Tile(g, tile);
                // saw blade, lower left: teeth all round, the arbor hole
                var teeth = new List<PointF>();
                float cx = 8.6f, cy = 14.6f, ro = 6.4f, ri = 5.0f;
                int n = 14;
                for (int i = 0; i < n; i++)
                {
                    double a = 2 * Math.PI * i / n;
                    double b = a + 2 * Math.PI / n * 0.72;
                    teeth.Add(new PointF(cx + (float)(ri * Math.Cos(a)), cy + (float)(ri * Math.Sin(a))));
                    teeth.Add(new PointF(cx + (float)(ro * Math.Cos(b)), cy + (float)(ro * Math.Sin(b))));
                }
                using (var path = new GraphicsPath { FillMode = FillMode.Alternate })
                {
                    path.AddPolygon(teeth.ToArray());
                    path.AddEllipse(cx - 1.6f, cy - 1.6f, 3.2f, 3.2f);
                    g.FillPath(Brushes.White, path);
                }
                // milling cutter, right: shank, fluted body, flat end
                using (var b = new SolidBrush(Color.White))
                {
                    g.FillRectangle(b, 16.4f, 2.8f, 2.8f, 4.4f);                 // shank
                    g.FillRectangle(b, 15.4f, 7.0f, 4.8f, 9.4f);                 // cutting body
                }
                using (var flute = new Pen(tile, 1.0f))
                {
                    g.DrawLine(flute, 15.4f, 9.6f, 20.2f, 8.0f);
                    g.DrawLine(flute, 15.4f, 12.4f, 20.2f, 10.8f);
                    g.DrawLine(flute, 15.4f, 15.2f, 20.2f, 13.6f);
                }
            });
        }

        /// <summary>Screwing and holing: a screw, head up, the thread down to its point.</summary>
        public static Image ScrewingHoling(Color tile, int size = 24)
        {
            return Make("sh" + tile.ToArgb(), size, (g, _) =>
            {
                Tile(g, tile);
                var state = g.Save();
                g.TranslateTransform(12f, 12f);
                g.RotateTransform(35f);
                using (var b = new SolidBrush(Color.White))
                {
                    // head: a wide dome
                    using (var head = new GraphicsPath())
                    {
                        head.AddArc(-5f, -10.5f, 10f, 6f, 180, 180);
                        head.AddLine(5f, -7.5f, 5f, -6.4f);
                        head.AddLine(5f, -6.4f, -5f, -6.4f);
                        head.CloseFigure();
                        g.FillPath(b, head);
                    }
                    // shank
                    g.FillRectangle(b, -1.7f, -6.6f, 3.4f, 12f);
                    // point
                    g.FillPolygon(b, new[] { new PointF(-1.7f, 5.3f), new PointF(1.7f, 5.3f), new PointF(0f, 9.4f) });
                }
                // the slot in the head
                using (var slot = new Pen(tile, 1.2f)) g.DrawLine(slot, 0f, -10.2f, 0f, -7.2f);
                // the thread: slanted ridges beyond the shank
                using (var th = new Pen(Color.White, 1.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    for (float y = -4.6f; y <= 4.6f; y += 2.3f)
                        g.DrawLine(th, -3.3f, y + 0.8f, 3.3f, y - 0.8f);
                g.Restore(state);
            });
        }

        /// <summary>Hinge drilling: a twist drill bit, point down, over the hole it makes.</summary>
        public static Image HingeDrilling(Color tile, int size = 24)
        {
            return Make("hd" + tile.ToArgb(), size, (g, _) =>
            {
                Tile(g, tile);
                using (var b = new SolidBrush(Color.White))
                {
                    g.FillRectangle(b, 10.3f, 2.6f, 3.4f, 5.2f);                           // shank
                    g.FillRectangle(b, 9.6f, 7.4f, 4.8f, 7.6f);                            // fluted part
                    g.FillPolygon(b, new[] { new PointF(9.6f, 15f), new PointF(14.4f, 15f), new PointF(12f, 18.2f) }); // point
                }
                using (var flute = new Pen(tile, 1f))
                {
                    g.DrawLine(flute, 9.6f, 9.4f, 14.4f, 7.8f);
                    g.DrawLine(flute, 9.6f, 12.2f, 14.4f, 10.6f);
                    g.DrawLine(flute, 9.6f, 15f, 14.4f, 13.4f);
                }
                // the hole: a shallow cup under the point
                using (var hole = new Pen(Color.White, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(hole, 4.6f, 19.6f, 8.2f, 19.6f);
                    g.DrawLine(hole, 15.8f, 19.6f, 19.4f, 19.6f);
                    g.DrawArc(hole, 8.2f, 17.4f, 7.6f, 4.4f, 0, 180);
                }
            });
        }

        /// <summary>The machine's rounded tile - the same shape and size the initial had.</summary>
        private static void Tile(Graphics g, Color colour)
        {
            using (var brush = new SolidBrush(colour))
            using (var path = new GraphicsPath())
            {
                var r = new RectangleF(1, 1, 22, 22);
                float d = 8;
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }
    }
}
