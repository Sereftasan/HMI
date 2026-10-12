using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// THE HELP MENU'S PICTURES, drawn as the user menu's are (12 October: "modify
    /// Help dropdown menu emojis so the menu gets to the same format as login
    /// dropdown"): plain black line pictures in the menu's picture column, the
    /// entry's words beside them - not coloured emojis inside the words.
    ///
    /// Drawn at the screen's scale (the menu's picture size), on a 24-unit grid
    /// with the user menu's stroke: a thick round-ended line, no fill but the dots.
    /// </summary>
    public static class MenuGlyphs
    {
        /// <summary>The size of a menu picture on this screen (the menus' ImageScalingSize).</summary>
        public static int Size { get { return UiScale.Px(22); } }

        /// <summary>"i" in a ring: About.</summary>
        public static Image Info()
        {
            return Draw((g, pen, ink, k) =>
            {
                g.DrawEllipse(pen, 3 * k, 3 * k, 18 * k, 18 * k);
                g.FillEllipse(ink, 10.6f * k, 6.6f * k, 2.8f * k, 2.8f * k);
                g.DrawLine(pen, 12 * k, 11.5f * k, 12 * k, 17 * k);
            });
        }

        /// <summary>An open book: the manual.</summary>
        public static Image Book()
        {
            return Draw((g, pen, ink, k) =>
            {
                // two pages from the spine, their lower edges bent towards it
                using (var left = new GraphicsPath())
                {
                    left.AddLine(12 * k, 7 * k, 12 * k, 20 * k);
                    left.AddBezier(12 * k, 20 * k, 9 * k, 18 * k, 5.5f * k, 18 * k, 2.5f * k, 18.5f * k);
                    left.AddLine(2.5f * k, 18.5f * k, 2.5f * k, 5.5f * k);
                    left.AddBezier(2.5f * k, 5.5f * k, 5.5f * k, 5 * k, 9 * k, 5 * k, 12 * k, 7 * k);
                    g.DrawPath(pen, left);
                }
                using (var right = new GraphicsPath())
                {
                    right.AddBezier(12 * k, 7 * k, 15 * k, 5 * k, 18.5f * k, 5 * k, 21.5f * k, 5.5f * k);
                    right.AddLine(21.5f * k, 5.5f * k, 21.5f * k, 18.5f * k);
                    right.AddBezier(21.5f * k, 18.5f * k, 18.5f * k, 18 * k, 15 * k, 18 * k, 12 * k, 20 * k);
                    g.DrawPath(pen, right);
                }
            });
        }

        /// <summary>A clipboard: the prompt put on the clipboard.</summary>
        public static Image Clipboard()
        {
            return Draw((g, pen, ink, k) =>
            {
                using (var board = Rounded(4.5f * k, 4.5f * k, 15 * k, 17 * k, 2 * k)) g.DrawPath(pen, board);
                // the clip at the top, over the board's edge
                using (var clip = Rounded(8.5f * k, 2.5f * k, 7 * k, 4 * k, 1.2f * k))
                {
                    using (var white = new SolidBrush(Color.White)) g.FillPath(white, clip);
                    g.DrawPath(pen, clip);
                }
                g.DrawLine(pen, 8 * k, 11.5f * k, 16 * k, 11.5f * k);
                g.DrawLine(pen, 8 * k, 15.5f * k, 13.5f * k, 15.5f * k);
            });
        }

        /// <summary>A speech bubble: ask an AI chat.</summary>
        public static Image Chat()
        {
            return Draw((g, pen, ink, k) =>
            {
                using (var bubble = new GraphicsPath())
                {
                    float x = 2.5f * k, y = 3.5f * k, w = 19 * k, h = 13.5f * k, r = 3 * k;
                    bubble.AddArc(x, y, 2 * r, 2 * r, 180, 90);
                    bubble.AddArc(x + w - 2 * r, y, 2 * r, 2 * r, 270, 90);
                    bubble.AddArc(x + w - 2 * r, y + h - 2 * r, 2 * r, 2 * r, 0, 90);
                    // the tail, from the lower edge down to the left
                    bubble.AddLine(x + w - 2 * r, y + h, 11 * k, y + h);
                    bubble.AddLine(11 * k, y + h, 6.5f * k, 21 * k);
                    bubble.AddLine(6.5f * k, 21 * k, 7 * k, y + h);
                    bubble.AddArc(x, y + h - 2 * r, 2 * r, 2 * r, 90, 90);
                    bubble.CloseFigure();
                    g.DrawPath(pen, bubble);
                }
                // three dots: someone answering
                for (int i = 0; i < 3; i++) g.FillEllipse(ink, (7.3f + 3.8f * i) * k, 9 * k, 2.2f * k, 2.2f * k);
            });
        }

        private static GraphicsPath Rounded(float x, float y, float w, float h, float r)
        {
            var p = new GraphicsPath();
            p.AddArc(x, y, 2 * r, 2 * r, 180, 90);
            p.AddArc(x + w - 2 * r, y, 2 * r, 2 * r, 270, 90);
            p.AddArc(x + w - 2 * r, y + h - 2 * r, 2 * r, 2 * r, 0, 90);
            p.AddArc(x, y + h - 2 * r, 2 * r, 2 * r, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>A picture of the menu's size: <paramref name="draw"/> on a 24-unit grid (k: pixels per unit).</summary>
        private static Image Draw(Action<Graphics, Pen, Brush, float> draw)
        {
            int size = Size;
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float k = size / 24f;
                using (var pen = new Pen(Color.Black, Math.Max(1.6f, 2.1f * k)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                using (var ink = new SolidBrush(Color.Black))
                    draw(g, pen, ink, k);
            }
            return bmp;
        }
    }
}
