using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// THE CHAIN ON A UNIT'S FRAME (UnitCoupling): a click couples the unit with the
    /// other coupled units of its kind, or uncouples it. Grey and open while uncoupled;
    /// blue and closed while coupled; faded while it cannot be coupled now - the units
    /// coupled do not match it, or nothing tells where it is (17 October: "disable the
    /// clutching button if there is no match ... the possible clutches appear anyway").
    /// A faded chain does nothing; a click says why.
    /// </summary>
    public class CoupleBadge : Control, UiStyle.IDrawsOwnHoverBorder
    {
        public const int BadgeSize = 16;
        private bool _on, _hover, _available = true;

        public static readonly Color Blue = Color.FromArgb(0, 120, 215);
        // 21 October: "not very identifiable on the controls - the chain bold, the circle less
        // apparent": a dark, heavy chain in a faint ring
        private static readonly Color Grey = Color.FromArgb(62, 70, 82);
        private static readonly Color Ring = Color.FromArgb(212, 216, 222);
        private static readonly Color Faded = Color.FromArgb(188, 193, 200);
        private static readonly Color FadedRing = Color.FromArgb(230, 232, 236);
        /// <summary>The chain's strokes on the badge (the caption bar's: 1.5).</summary>
        public const float BadgeWeight = 2.2f;

        public CoupleBadge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Size = new Size(BadgeSize, BadgeSize);
            Cursor = Cursors.Hand;
            TabStop = false;
            Name = "coupleBadge";
        }

        /// <summary>Coupled: blue, closed.</summary>
        public bool On
        {
            get { return _on; }
            set { if (_on == value) return; _on = value; Invalidate(); }
        }

        /// <summary>It may be clicked now: coupled (to uncouple), or it can be coupled. Not: faded, no hand.</summary>
        public bool Available
        {
            get { return _available; }
            set
            {
                if (_available == value) return;
                _available = value;
                Cursor = value ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // a white disc behind it, as the "?": it sits on the frame's line
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            bool faded = !_available && !_on;
            using (var back = new SolidBrush(_on ? Blue : Color.White)) g.FillEllipse(back, r);
            var ink = _on ? Color.White : faded ? Faded : _hover ? Blue : Grey;
            // the ring only marks the button: faint (blue under the mouse), thin
            var ringColour = _on ? Blue : faded ? FadedRing : _hover ? Color.FromArgb(120, 175, 230) : Ring;
            using (var ring = new Pen(ringColour, 1f)) g.DrawEllipse(ring, r);
            DrawChain(g, new RectangleF(0, 0, Width, Height), ink, _on, BadgeWeight);
        }

        /// <summary>Two links of a chain, slanted; apart while open (the caption bar's chain draws it too).</summary>
        /// <param name="weight">The strokes' width at 16 pixels: 1.5 the caption bar's, heavier on a unit's badge.</param>
        public static void DrawChain(Graphics g, RectangleF area, Color ink, bool closed, float weight = 1.5f)
        {
            float s = Math.Min(area.Width, area.Height) / 16f;
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(area.Left + area.Width / 2f, area.Top + area.Height / 2f);
            g.RotateTransform(-45);
            float gap = closed ? 0f : 1.2f * s;
            using (var pen = new Pen(ink, weight * s))
            {
                DrawLink(g, pen, -2.6f * s - gap / 2, 0, s);
                DrawLink(g, pen, 2.6f * s + gap / 2, 0, s);
            }
            g.Restore(state);
        }

        private static void DrawLink(Graphics g, Pen pen, float cx, float cy, float s)
        {
            float w = 6.2f * s, h = 3.6f * s;
            using (var path = new GraphicsPath())
            {
                path.AddArc(cx - w / 2, cy - h / 2, h, h, 90, 180);
                path.AddArc(cx + w / 2 - h, cy - h / 2, h, h, 270, 180);
                path.CloseFigure();
                g.DrawPath(pen, path);
            }
        }
    }
}
