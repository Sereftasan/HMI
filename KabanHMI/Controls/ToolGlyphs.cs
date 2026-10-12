using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// Toolbar pictures DRAWN rather than typed, for the commands whose emoji
    /// does not say what the button does.
    ///
    /// The arrow characters (the curls of undo and redo, the arrows to a bar of
    /// move up and down) come out as thin long sticks in every font the machine
    /// PCs have, and read as nothing at a glance; the emoji clipboard and pin
    /// were not the copy and paste pictures people know from every office
    /// program. These are the same shapes, short and heavy, in the colours of
    /// those programs: blue arrows, white pages, the amber clipboard.
    ///
    /// Each draws into the button's rectangle, centred, about 20 pixels across,
    /// and in grey when the button is disabled (nothing to undo).
    /// </summary>
    public static class ToolGlyphs
    {
        public static readonly Color Ink = Color.FromArgb(28, 96, 184);
        public static readonly Color Dark = Color.FromArgb(52, 60, 74);
        public static readonly Color Muted = Color.FromArgb(176, 182, 190);
        public static readonly Color Amber = Color.FromArgb(230, 165, 58);
        public static readonly Color AmberEdge = Color.FromArgb(166, 110, 28);

        /// <summary>Undo: a heavy curl over the top, the head down on the left (U+21B6).</summary>
        public static void Undo(Graphics g, Rectangle r, bool enabled) { Curl(g, r, enabled, false); }

        /// <summary>Redo: the same curl the other way (U+21B7).</summary>
        public static void Redo(Graphics g, Rectangle r, bool enabled) { Curl(g, r, enabled, true); }

        /// <summary>Move up: a short heavy arrow up to a bar (U+2912).</summary>
        public static void MoveUp(Graphics g, Rectangle r, bool enabled) { ToBar(g, r, enabled, true); }

        /// <summary>Move down: a short heavy arrow down to a bar (U+2913).</summary>
        public static void MoveDown(Graphics g, Rectangle r, bool enabled) { ToBar(g, r, enabled, false); }

        private static PointF Centre(Rectangle r) { return new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f); }

        private static void Curl(Graphics g, Rectangle r, bool enabled, bool mirror)
        {
            var c = Centre(r);
            var state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (mirror) { g.TranslateTransform(2 * c.X, 0); g.ScaleTransform(-1, 1); }
                var ink = enabled ? Ink : Muted;
                const float radius = 6.5f;
                float ax = c.X + 2f, ay = c.Y + 1f;             // the arc's centre
                using (var pen = new Pen(ink, 3.6f) { StartCap = LineCap.Round, EndCap = LineCap.Flat })
                    // From the right end, over the top, to the left end.
                    g.DrawArc(pen, ax - radius, ay - radius, 2 * radius, 2 * radius, 0f, -180f);
                float lx = ax - radius, ly = ay - 0.5f;          // the left end: the head points DOWN from it
                using (var brush = new SolidBrush(ink))
                    g.FillPolygon(brush, new[]
                    {
                        new PointF(lx - 5f, ly), new PointF(lx + 5f, ly), new PointF(lx, ly + 6.5f)
                    });
            }
            finally { g.Restore(state); }
        }

        private static void ToBar(Graphics g, Rectangle r, bool enabled, bool up)
        {
            var c = Centre(r);
            var state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (!up) { g.TranslateTransform(0, 2 * c.Y); g.ScaleTransform(1, -1); }
                using (var bar = new SolidBrush(enabled ? Dark : Muted))
                    g.FillRectangle(bar, c.X - 8f, c.Y - 9f, 16f, 3.2f);
                using (var arrow = new SolidBrush(enabled ? Ink : Muted))
                {
                    // Short and heavy: a wide head, a thick stub of a shaft.
                    g.FillPolygon(arrow, new[]
                    {
                        new PointF(c.X, c.Y - 4f), new PointF(c.X + 7.5f, c.Y + 3f), new PointF(c.X - 7.5f, c.Y + 3f)
                    });
                    g.FillRectangle(arrow, c.X - 2.4f, c.Y + 2.5f, 4.8f, 6.5f);
                }
            }
            finally { g.Restore(state); }
        }

        /// <summary>Copy: two pages, one over the other, as in the office programs.</summary>
        public static void Copy(Graphics g, Rectangle r, bool enabled)
        {
            var c = Centre(r);
            var state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Page(g, new RectangleF(c.X - 9.5f, c.Y - 10f, 12f, 15f), enabled, Color.FromArgb(234, 241, 251), 2);
                Page(g, new RectangleF(c.X - 3f, c.Y - 5f, 12.5f, 15f), enabled, Color.White, 3);
            }
            finally { g.Restore(state); }
        }

        /// <summary>Paste: the amber clipboard with a page on it.</summary>
        public static void Paste(Graphics g, Rectangle r, bool enabled)
        {
            var c = Centre(r);
            var state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var board = new RectangleF(c.X - 10f, c.Y - 8f, 15f, 18.5f);
                using (var path = RoundedRect(board, 2f))
                {
                    using (var fill = new SolidBrush(enabled ? Amber : Color.FromArgb(222, 225, 230))) g.FillPath(fill, path);
                    using (var edge = new Pen(enabled ? AmberEdge : Muted, 1.3f)) g.DrawPath(edge, path);
                }
                // The clip at the top.
                using (var clip = RoundedRect(new RectangleF(c.X - 7f, c.Y - 10.5f, 9f, 4.5f), 1.5f))
                using (var fill = new SolidBrush(enabled ? Dark : Muted))
                    g.FillPath(fill, clip);
                Page(g, new RectangleF(c.X - 1f, c.Y - 3f, 11f, 13.5f), enabled, Color.White, 3);
            }
            finally { g.Restore(state); }
        }

        /// <summary>A page with its top right corner turned down, and lines of text.</summary>
        private static void Page(Graphics g, RectangleF p, bool enabled, Color paper, int lines)
        {
            const float fold = 3.6f;
            var outline = new[]
            {
                new PointF(p.Left, p.Top), new PointF(p.Right - fold, p.Top), new PointF(p.Right, p.Top + fold),
                new PointF(p.Right, p.Bottom), new PointF(p.Left, p.Bottom)
            };
            using (var fill = new SolidBrush(enabled ? paper : Color.White)) g.FillPolygon(fill, outline);
            using (var edge = new Pen(enabled ? Dark : Muted, 1.3f) { LineJoin = LineJoin.Round })
            {
                g.DrawPolygon(edge, outline);
                g.DrawLines(edge, new[] { new PointF(p.Right - fold, p.Top), new PointF(p.Right - fold, p.Top + fold), new PointF(p.Right, p.Top + fold) });
            }
            using (var text = new Pen(enabled ? Ink : Muted, 1.4f))
                for (int i = 0; i < lines; i++)
                {
                    float y = p.Top + fold + 2.6f + i * 2.8f;
                    if (y > p.Bottom - 2f) break;
                    g.DrawLine(text, p.Left + 2.4f, y, p.Right - (i == lines - 1 ? 4.5f : 2.4f), y);
                }
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
