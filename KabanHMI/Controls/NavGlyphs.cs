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
    /// A machine used to be the first letter of its name on a coloured tile; now
    /// it is what the machine does, drawn in the plain ink of the other bar
    /// pictures - no tile, no colour: an end mill throwing chips in front of a
    /// circular saw blade for the processing centre, a countersunk cross-head
    /// screw for screwing, a twist drill bit for 3D (hinge) drilling.
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

        /// <summary>
        /// Size 0 (the default): the bar's 24 px at the screen scale, drawn at that
        /// size - sharp, not an enlarged 24 px picture - and marked as at the factor.
        /// </summary>
        private static Image Make(string key, int size, Action<Graphics, float> draw)
        {
            bool atFactor = size <= 0;
            if (atFactor) size = UiScale.Px(24);
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
            return atFactor ? UiScale.MarkedPicture(img) : img;
        }

        // ------------------------------------------------------------------
        // HMI SETTINGS: a screen on its stand, a gear on its lower right corner.
        // ------------------------------------------------------------------
        public static Image HmiSettings(int size = 0)
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
        // THE MACHINES: plain ink pictures, like every other picture in the bar.
        // No tile and no colour: the shape alone tells the machines apart.
        // ------------------------------------------------------------------

        /// <summary>
        /// Draws with "no ink": clears what is under the shape back to
        /// transparent. The gaps between the parts of a picture (the flutes of a
        /// cutter, the cross in a screw's head, the air round a tool in front of
        /// a blade) are cut this way, so the picture stays one colour on any
        /// background.
        /// </summary>
        private static void Cut(Graphics g, Action<Graphics, Brush, Pen> draw, float width)
        {
            var mode = g.CompositingMode;
            g.CompositingMode = CompositingMode.SourceCopy;
            using (var b = new SolidBrush(Color.Transparent))
            using (var p = new Pen(Color.Transparent, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                draw(g, b, p);
            g.CompositingMode = mode;
        }

        /// <summary>
        /// The processing centre: an end mill in its holder with the chips it
        /// throws, in front of a circular saw blade.
        /// </summary>
        public static Image ProcessingCenter(int size = 0)
        {
            return Make("pc", size, (g, _) =>
            {
                using (var ink = new SolidBrush(Ink))
                using (var mill = new GraphicsPath())
                {
                    // the saw blade, right: teeth all round, the arbor hole
                    float cx = 15.2f, cy = 11.8f, ro = 8.4f, ri = 6.8f;
                    var teeth = new List<PointF>();
                    int n = 18;
                    for (int i = 0; i < n; i++)
                    {
                        double a0 = 2 * Math.PI * i / n;
                        double a1 = a0 + 2 * Math.PI / n * 0.78;
                        teeth.Add(new PointF(cx + (float)(ri * Math.Cos(a0)), cy + (float)(ri * Math.Sin(a0))));
                        teeth.Add(new PointF(cx + (float)(ro * Math.Cos(a1)), cy + (float)(ro * Math.Sin(a1))));
                    }
                    using (var blade = new GraphicsPath { FillMode = FillMode.Alternate })
                    {
                        blade.AddPolygon(teeth.ToArray());
                        blade.AddEllipse(cx - 2.0f, cy - 2.0f, 4.0f, 4.0f);
                        g.FillPath(ink, blade);
                    }
                    // the end mill in front of it: holder, collet, fluted cutter
                    mill.AddRectangle(new RectangleF(2.0f, 1.0f, 7.8f, 3.0f));
                    mill.AddPolygon(new[] { new PointF(2.6f, 4.0f), new PointF(9.2f, 4.0f), new PointF(7.9f, 6.6f), new PointF(3.9f, 6.6f) });
                    mill.AddRectangle(new RectangleF(3.9f, 6.6f, 4.0f, 8.8f));
                    // the air round it, so it stands clear of the blade
                    Cut(g, (c, b, p) => { c.FillPath(b, mill); c.DrawPath(p, mill); }, 2.6f);
                    g.FillPath(ink, mill);
                    Cut(g, (c, b, p) =>
                    {
                        c.DrawLine(p, 2.0f, 4.0f, 9.8f, 4.0f);                                  // holder | collet
                        for (float y = 8.6f; y <= 15.0f; y += 2.4f) c.DrawLine(p, 3.6f, y + 1.0f, 8.2f, y - 1.0f);   // flutes
                    }, 0.8f);
                    // the chips flying off, and the work under them
                    using (var pen = new Pen(Ink, 1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        g.DrawLine(pen, 2.8f, 17.0f, 1.4f, 18.8f);
                        g.DrawLine(pen, 5.9f, 17.4f, 5.9f, 19.2f);
                        g.DrawLine(pen, 9.0f, 17.0f, 10.4f, 18.8f);
                        g.DrawLine(pen, 1.0f, 21.8f, 10.8f, 21.8f);
                    }
                }
            });
        }

        /// <summary>Screwing: a countersunk screw with a cross (Phillips) head, point down.</summary>
        public static Image ScrewingHoling(int size = 0)
        {
            return Make("sh", size, (g, _) =>
            {
                using (var ink = new SolidBrush(Ink))
                {
                    // the countersunk head: its top seen a little from above, the cone under it
                    g.FillPolygon(ink, new[] { new PointF(4.6f, 4.4f), new PointF(19.4f, 4.4f), new PointF(14.0f, 9.6f), new PointF(10.0f, 9.6f) });
                    g.FillEllipse(ink, 4.6f, 1.4f, 14.8f, 6.0f);
                    // the rim between the top and the cone, and the cross in the top
                    Cut(g, (c, b, p) => c.DrawArc(p, 4.9f, 1.9f, 14.2f, 5.4f, 20, 140), 0.8f);
                    Cut(g, (c, b, p) =>
                    {
                        c.DrawLine(p, 8.6f, 4.3f, 15.4f, 4.3f);
                        c.DrawLine(p, 12.0f, 2.8f, 12.0f, 5.8f);
                    }, 1.1f);
                    // the shank with its thread, down to the point
                    var edge = new List<PointF> { new PointF(10.6f, 9.4f) };
                    float y = 10.2f;
                    for (int i = 0; i < 4; i++, y += 2.6f)
                    {
                        edge.Add(new PointF(8.4f, y + 1.1f));
                        edge.Add(new PointF(10.6f, y + 1.9f));
                    }
                    edge.Add(new PointF(12.0f, 23.0f));
                    y = 10.2f;
                    var right = new List<PointF>();
                    for (int i = 0; i < 4; i++, y += 2.6f)
                    {
                        right.Add(new PointF(13.4f, y + 0.6f));
                        right.Add(new PointF(15.6f, y + 1.4f));
                    }
                    right.Reverse();
                    edge.AddRange(right);
                    edge.Add(new PointF(13.4f, 9.4f));
                    g.FillPolygon(ink, edge.ToArray());
                }
            });
        }

        /// <summary>3D (hinge) drilling: a twist drill bit, shank up, point down.</summary>
        public static Image HingeDrilling(int size = 0)
        {
            return Make("hd", size, (g, _) =>
            {
                using (var ink = new SolidBrush(Ink))
                {
                    g.FillRectangle(ink, 9.0f, 0.8f, 6.0f, 6.2f);                                   // shank
                    g.FillRectangle(ink, 9.0f, 7.8f, 6.0f, 11.4f);                                  // fluted part
                    g.FillPolygon(ink, new[] { new PointF(9.0f, 19.1f), new PointF(15.0f, 19.1f), new PointF(12f, 23.2f) }); // point
                    // the helical flutes, running on into the point
                    Cut(g, (c, b, p) =>
                    {
                        for (float y = 9.6f; y <= 20.0f; y += 2.8f) c.DrawLine(p, 8.6f, y + 1.8f, 15.4f, y - 1.8f);
                    }, 1.1f);
                }
            });
        }
    
        /// <summary>
        /// The dashboard: a gauge - its dial as an open arc with a few marks, the
        /// needle leaning to the right from its hub.
        /// </summary>
        public static Image Dashboard(int size = 0)
        {
            return Make("dash", size, (g, _) =>
            {
                using (var pen = new Pen(Ink, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    // the dial: from lower left over the top to lower right
                    g.DrawArc(pen, 2.5f, 3.5f, 19f, 19f, 150f, 240f);
                    // the marks inside it
                    for (int i = 0; i <= 4; i++)
                    {
                        double a = (150 + i * 60) * Math.PI / 180.0;
                        float cx = 12f, cy = 13f;
                        g.DrawLine(pen, cx + (float)(Math.Cos(a) * 6.6), cy + (float)(Math.Sin(a) * 6.6),
                                        cx + (float)(Math.Cos(a) * 7.6), cy + (float)(Math.Sin(a) * 7.6));
                    }
                    // the needle
                    g.DrawLine(pen, 12f, 13f, 16.6f, 7.6f);
                }
                using (var ink = new SolidBrush(Ink)) g.FillEllipse(ink, 9.9f, 10.9f, 4.2f, 4.2f);
                using (var pen = new Pen(Ink, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(pen, 7.5f, 20.5f, 16.5f, 20.5f);   // the base
            });
        }
    }
}
