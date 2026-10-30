using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// AN ON / OFF SWITCH, as a browser's settings have them (30 October: "redesign the items (selection
    /// toggles) as the attached image" - a row's name on the left, its switch at the right end):
    /// a rounded track with a knob, blue with a white knob at the right while on, a grey outline with a
    /// small grey knob at the left while off. A click, Space or Enter turns it over.
    /// </summary>
    [DefaultEvent("CheckedChanged")]
    public class ToggleSwitch : Control
    {
        /// <summary>The track's colour while on.</summary>
        public static readonly Color OnColor = Color.FromArgb(11, 87, 208);
        private static readonly Color OffTrack = Color.FromArgb(227, 227, 227);
        private static readonly Color OffInk = Color.FromArgb(116, 119, 117);

        private bool _checked;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Size = new Size(40, 24);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        /// <summary>On (the knob at the right).</summary>
        [DefaultValue(false), Category("Behavior")]
        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when it is turned over - by the user or by the program.</summary>
        public event EventHandler CheckedChanged;

        /// <summary>Turned over, as a click does.</summary>
        public void Toggle() { if (Enabled) Checked = !Checked; }

        protected override void OnClick(EventArgs e)
        {
            Focus();
            Toggle();
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Toggle(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        /// <summary>The track: as wide as the control, at most 3/5 of that high, centred.</summary>
        internal RectangleF Track
        {
            get
            {
                float h = Math.Min(Height - 4f, Width * 0.58f), w = Math.Min(Width - 4f, h * 1.75f);
                return new RectangleF((Width - w) / 2f, (Height - h) / 2f, w, h);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var t = Track;
            Draw(g, t, _checked, Enabled);
            if (Focused && ShowFocusCues)
            {
                var ring = RectangleF.Inflate(t, 2f, 2f);
                using (var path = Pill(ring))
                using (var pen = new Pen(Color.FromArgb(160, OnColor), 1.5f)) g.DrawPath(pen, path);
            }
        }

        /// <summary>
        /// A switch drawn into a track rectangle (twice as wide as high, about): on - a blue track, a white knob at
        /// the right; off - a light grey track with a grey outline, a small grey knob at the left. Faded when it
        /// cannot be turned (CheckBoxHMI's Switch looks draw theirs with it).
        /// </summary>
        public static void Draw(Graphics g, RectangleF t, bool on, bool live)
        {
            var state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float r = t.Height / 2f;
                Func<Color, Color> faded = c => live ? c : Color.FromArgb(110, c);
                using (var track = Pill(t))
                {
                    if (on)
                        using (var fill = new SolidBrush(faded(OnColor))) g.FillPath(fill, track);
                    else
                    {
                        using (var fill = new SolidBrush(faded(OffTrack))) g.FillPath(fill, track);
                        using (var line = new Pen(faded(OffInk), Math.Max(1.2f, t.Height / 13f))) g.DrawPath(line, track);
                    }
                }
                // the knob: large and white at the right while on, small and grey at the left while off
                float d = on ? t.Height - Math.Max(4f, t.Height * 0.22f) : t.Height * 0.55f;
                float cx = on ? t.Right - r : t.Left + r, cy = t.Top + r;
                using (var knob = new SolidBrush(faded(on ? Color.White : OffInk)))
                    g.FillEllipse(knob, cx - d / 2f, cy - d / 2f, d, d);
            }
            finally { g.Restore(state); }
        }

        internal static GraphicsPath Pill(RectangleF r)
        {
            var p = new GraphicsPath();
            float d = r.Height;
            p.AddArc(r.Left, r.Top, d, d, 90, 180);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 180);
            p.CloseFigure();
            return p;
        }
    }
}
