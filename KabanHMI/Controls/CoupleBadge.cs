using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// THE CHAIN ON A UNIT'S FRAME (UnitCoupling): a click couples the unit with
    /// the other coupled units of its kind, or uncouples it. Grey and open while
    /// uncoupled; blue and closed while coupled; blinking light blue while units of
    /// its kind are coupled and it could join them (16 October: "make the clutching
    /// possible controls' clutching button blink when any clutching is activated").
    /// </summary>
    public class CoupleBadge : Control, UiStyle.IDrawsOwnHoverBorder
    {
        public const int BadgeSize = 16;
        private bool _on, _hover, _inviting;

        /// <summary>The blink's phase, shared by every chain (UnitCoupling's timer turns it).</summary>
        internal static bool Phase = true;

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

        /// <summary>It could join the units coupled now: blinking (steady light blue with blinking off).</summary>
        public bool Inviting
        {
            get { return _inviting; }
            set { if (_inviting == value) return; _inviting = value; Invalidate(); }
        }

        /// <summary>Drawn lit now (inviting, in the blink's bright phase).</summary>
        internal bool Lit { get { return _inviting && !_on && (Phase || !ViewPreferences.BlinkingOn); } }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // a white disc behind it, as the "?": it sits on the frame's line
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            bool lit = Lit;
            using (var back = new SolidBrush(_on ? Color.FromArgb(0, 120, 215) : lit ? Color.FromArgb(204, 228, 250) : Color.White)) g.FillEllipse(back, r);
            var ink = _on ? Color.White : _hover || lit ? Color.FromArgb(0, 120, 215) : Color.FromArgb(120, 126, 134);
            using (var ring = new Pen(_on ? Color.FromArgb(0, 120, 215) : ink, lit ? 1.6f : 1.2f)) g.DrawEllipse(ring, r);
            // two links of a chain, slanted; apart while uncoupled
            float s = Width / 16f;
            var state = g.Save();
            g.TranslateTransform(Width / 2f, Height / 2f);
            g.RotateTransform(-45);
            float gap = _on ? 0f : 1.2f * s;
            using (var pen = new Pen(ink, 1.5f * s))
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
                float r = h / 2;
                path.AddArc(cx - w / 2, cy - h / 2, h, h, 90, 180);
                path.AddArc(cx + w / 2 - h, cy - h / 2, h, h, 270, 180);
                path.CloseFigure();
                g.DrawPath(pen, path);
            }
        }
    }
}
