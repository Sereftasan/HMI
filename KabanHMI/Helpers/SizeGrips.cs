using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// GRIPS A FINGER CAN HOLD ON THE PAGES' BORDERS (2 November: "holding the border of the pages to extend or reduce the
    /// dimension of the sub pages is quite hard to do, should we add a controls to the center and corner of each side of the
    /// pages for the user to able to hold it with the finger touch easier").
    ///
    /// In touch mode:
    ///   - a border between two parts of the window (a splitter: the side page's) is wider and carries three grips - at its
    ///     middle and near each end; dragged, the part beside it is sized as it goes;
    ///   - a page in a window of its own has a wider frame with a grip at each corner (two arms along the frame: an L) and at
    ///     the middle of each side; dragged, the window is sized from that side or corner (the corner: both of its sides).
    /// A grip along a side is thicker than the border it is on - a little of the page beside it is under it.
    /// With a mouse the grips are not there and the borders are as thin as before - the pointer finds them.
    /// </summary>
    public static class SizeGrips
    {
        [Flags]
        public enum Edge { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8 }

        /// <summary>A splitter's width in touch mode, at factor 1.</summary>
        public const int TouchSplitter = 12;
        /// <summary>A window's frame in touch mode, at factor 1.</summary>
        public const int TouchFrame = 18;
        /// <summary>A grip across its border, along it, a corner's arms - at factor 1.</summary>
        public const int Across = 28, Along = 76, CornerArm = 60;

        private static readonly List<WeakReference> _attached = new List<WeakReference>();
        private static bool _following;

        private static void Follow()
        {
            if (_following) return;
            _following = true;
            TouchInput.ModeChanged += (s, e) =>
            {
                foreach (var w in _attached.ToArray())
                {
                    var a = w.Target as Attached;
                    if (a == null) { _attached.Remove(w); continue; }
                    a.Refresh();
                }
            };
        }

        private abstract class Attached
        {
            public readonly List<Grip> Grips = new List<Grip>();
            public abstract void Refresh();
        }

        /// <summary>The grips of a splitter (tests): empty while none were made for it.</summary>
        public static IList<Grip> Of(Control c)
        {
            foreach (var w in _attached)
            {
                var a = w.Target as Attached;
                var s = a as SplitterGrips;
                if (s != null && ReferenceEquals(s.Splitter, c)) return s.Grips;
                var f = a as WindowGrips;
                if (f != null && ReferenceEquals(f.Window, c)) return f.Grips;
            }
            return new Grip[0];
        }

        // ---- a splitter ---------------------------------------------------------------------------

        /// <summary>
        /// A splitter's grips: in touch mode it is <see cref="TouchSplitter"/> wide with three grips on it; with a mouse as it
        /// was (<paramref name="mouseWidth"/>, at factor 1). Dragging one moves the splitter as dragging it does.
        /// </summary>
        public static void ForSplitter(Splitter splitter, int mouseWidth)
        {
            if (splitter == null) return;
            Follow();
            var a = new SplitterGrips(splitter, mouseWidth);
            _attached.Add(new WeakReference(a));
            splitter.Disposed += (s, e) => _attached.RemoveAll(w => ReferenceEquals(w.Target, a));
            a.Refresh();
        }

        private sealed class SplitterGrips : Attached
        {
            public readonly Splitter Splitter;
            private readonly int _mouseWidth;
            private readonly bool _across;           // a splitter docked left or right: dragged across (x)
            private int _start;

            public SplitterGrips(Splitter s, int mouseWidth)
            {
                Splitter = s;
                _mouseWidth = mouseWidth;
                _across = s.Dock == DockStyle.Left || s.Dock == DockStyle.Right;
                var edge = _across ? Edge.Left : Edge.Top;
                for (int i = 0; i < 3; i++)
                {
                    var g = new Grip(edge, false) { Name = "sizeGrip" + i, Visible = false };
                    g.Begin += () => _start = Splitter.SplitPosition;
                    g.Drag += (dx, dy, last) => Apply(dx, dy, last);
                    Grips.Add(g);
                }
                EventHandler place = (o, e) => Place();
                s.LocationChanged += place;
                s.SizeChanged += place;
                s.VisibleChanged += place;
                s.ParentChanged += (o, e) => Refresh();
            }

            private DateTime _applied = DateTime.MinValue;

            private void Apply(int dx, int dy, bool last)
            {
                if (Splitter.IsDisposed) return;
                // the page laid out (and the side page's width kept) no more than 25 times a second; where the finger lifts, always
                if (!last && (DateTime.Now - _applied).TotalMilliseconds < 40) return;
                _applied = DateTime.Now;
                int d = _across ? dx : dy;
                if (Splitter.Dock == DockStyle.Right || Splitter.Dock == DockStyle.Bottom) d = -d;
                int want = Math.Max(0, _start + d);
                if (want != Splitter.SplitPosition) Splitter.SplitPosition = want;     // the splitter's own limits; it says it moved
                Place();
            }

            public override void Refresh()
            {
                if (Splitter.IsDisposed) return;
                bool touch = TouchInput.On;
                int width = UiScale.For(Splitter, touch ? TouchSplitter : _mouseWidth);
                if (_across) { if (Splitter.Width != width) Splitter.Width = width; }
                else if (Splitter.Height != width) Splitter.Height = width;
                var parent = Splitter.Parent;
                foreach (var g in Grips)
                    if (parent != null && !ReferenceEquals(g.Parent, parent)) parent.Controls.Add(g);
                Place();
            }

            private void Place()
            {
                if (Splitter.IsDisposed || Splitter.Parent == null) return;
                bool show = TouchInput.On && Splitter.Visible;
                int across = UiScale.For(Splitter, Across), along = UiScale.For(Splitter, Along), end = UiScale.For(Splitter, 24);
                var r = Splitter.Bounds;
                for (int i = 0; i < Grips.Count; i++)
                {
                    var g = Grips[i];
                    if (!show) { if (g.Visible) g.Visible = false; continue; }
                    Rectangle b;
                    if (_across)
                    {
                        int room = r.Height;
                        int y = i == 0 ? r.Top + (room - along) / 2 : i == 1 ? r.Top + end : r.Bottom - end - along;
                        b = new Rectangle(r.Left + (r.Width - across) / 2, y, across, along);
                        // too short for three: the middle one only
                        if (i > 0 && room < along * 4) { if (g.Visible) g.Visible = false; continue; }
                    }
                    else
                    {
                        int room = r.Width;
                        int x = i == 0 ? r.Left + (room - along) / 2 : i == 1 ? r.Left + end : r.Right - end - along;
                        b = new Rectangle(x, r.Top + (r.Height - across) / 2, along, across);
                        if (i > 0 && room < along * 4) { if (g.Visible) g.Visible = false; continue; }
                    }
                    if (g.Bounds != b) g.Bounds = b;
                    if (!g.Visible) g.Visible = true;
                    g.BringToFront();
                }
            }
        }

        // ---- a window -----------------------------------------------------------------------------

        /// <summary>
        /// A borderless window's grips (it is sized from the frame round it, its Padding): in touch mode the frame is
        /// <see cref="TouchFrame"/> wide with a grip at each corner and at the middle of each side; while the window is
        /// maximised none. <paramref name="sized"/>: told when a drag ends (the window keeps its size).
        /// </summary>
        public static void ForWindow(Form window, int mouseFrame, Action sized)
        {
            if (window == null) return;
            Follow();
            var a = new WindowGrips(window, mouseFrame, sized);
            _attached.Add(new WeakReference(a));
            window.Disposed += (s, e) => _attached.RemoveAll(w => ReferenceEquals(w.Target, a));
            a.Refresh();
        }

        /// <summary>A borderless window's frame now: wider in touch mode (a finger sizes it), none while maximised.</summary>
        public static int FrameOf(Form window, int mouseFrame)
        {
            if (window == null || window.WindowState == FormWindowState.Maximized) return 0;
            return UiScale.For(window, TouchInput.On ? TouchFrame : mouseFrame);
        }

        private sealed class WindowGrips : Attached
        {
            public readonly Form Window;
            private readonly Action _sized;
            private readonly int _mouseFrame;
            private Rectangle _start;

            private static readonly Edge[] Edges =
            {
                Edge.Left | Edge.Top, Edge.Top, Edge.Right | Edge.Top, Edge.Right,
                Edge.Right | Edge.Bottom, Edge.Bottom, Edge.Left | Edge.Bottom, Edge.Left
            };

            public WindowGrips(Form f, int mouseFrame, Action sized)
            {
                Window = f;
                _sized = sized;
                _mouseFrame = mouseFrame;
                foreach (var edge in Edges)
                {
                    bool corner = (edge & (Edge.Left | Edge.Right)) != 0 && (edge & (Edge.Top | Edge.Bottom)) != 0;
                    string name = "sizeGrip" + edge.ToString().Replace(", ", "");
                    // a corner: its two arms along the frame (an L) - the caption's buttons and the page inside clear of it
                    foreach (var arm in corner ? new[] { "H", "V" } : new[] { "" })
                    {
                        var g = new Grip(edge, corner) { Name = name + arm, Visible = false };
                        g.Begin += () => _start = Window.Bounds;
                        g.Drag += (dx, dy, last) => Apply(g.Edges, dx, dy, last);
                        Grips.Add(g);
                        f.Controls.Add(g);
                    }
                }
                f.Resize += (o, e) => Place();
                f.Layout += (o, e) => Place();
            }

            private void Apply(Edge edge, int dx, int dy, bool last)
            {
                if (Window.IsDisposed || Window.WindowState != FormWindowState.Normal) return;
                var min = Window.MinimumSize;
                int minW = Math.Max(min.Width, UiScale.For(Window, 160)), minH = Math.Max(min.Height, UiScale.For(Window, 120));
                int l = _start.Left, t = _start.Top, r = _start.Right, b = _start.Bottom;
                if ((edge & Edge.Left) != 0) l = Math.Min(_start.Left + dx, r - minW);
                if ((edge & Edge.Right) != 0) r = Math.Max(_start.Right + dx, l + minW);
                if ((edge & Edge.Top) != 0) t = Math.Min(_start.Top + dy, b - minH);
                if ((edge & Edge.Bottom) != 0) b = Math.Max(_start.Bottom + dy, t + minH);
                var want = Rectangle.FromLTRB(l, t, r, b);
                if (Window.Bounds != want) Window.Bounds = want;
                if (last && _sized != null) _sized();
            }

            public override void Refresh()
            {
                if (Window.IsDisposed) return;
                var want = new Padding(FrameOf(Window, _mouseFrame));
                if (Window.Padding != want) Window.Padding = want;
                Place();
                Window.Invalidate();
            }

            private void Place()
            {
                if (Window.IsDisposed) return;
                bool show = TouchInput.On && Window.WindowState == FormWindowState.Normal;
                int frame = Math.Max(1, Window.Padding.Left);
                int across = Math.Max(frame, UiScale.For(Window, Across)), along = UiScale.For(Window, Along), arm = UiScale.For(Window, CornerArm);
                var c = Window.ClientSize;
                foreach (var g in Grips)
                {
                    if (!show) { if (g.Visible) g.Visible = false; continue; }
                    var e = g.Edges;
                    Rectangle b;
                    if (g.IsCorner)
                    {
                        bool left = (e & Edge.Left) != 0, top = (e & Edge.Top) != 0;
                        b = g.Name.EndsWith("H")
                            ? new Rectangle(left ? 0 : c.Width - arm, top ? 0 : c.Height - frame, arm, frame)
                            : new Rectangle(left ? 0 : c.Width - frame, top ? 0 : c.Height - arm, frame, arm);
                    }
                    else if (e == Edge.Left || e == Edge.Right)
                        b = new Rectangle(e == Edge.Left ? 0 : c.Width - across, (c.Height - along) / 2, across, along);
                    else
                        b = new Rectangle((c.Width - along) / 2, e == Edge.Top ? 0 : c.Height - across, along, across);
                    if (g.Bounds != b) g.Bounds = b;
                    if (!g.Visible) g.Visible = true;
                    g.BringToFront();
                }
            }
        }
    }

    /// <summary>
    /// A grip on a border (SizeGrips): a pill along a side, an arm of a corner's L. Pressed and dragged - by a finger or the
    /// mouse - it tells how far from where it was pressed (screen pixels); the owner sizes what it belongs to.
    /// </summary>
    public sealed class Grip : Control, TouchInput.IHandlesTouch
    {
        public readonly SizeGrips.Edge Edges;
        public readonly bool IsCorner;

        public event Action Begin;
        /// <summary>How far from the press (x, y), and whether it is the last of the drag (the finger lifted).</summary>
        public event Action<int, int, bool> Drag;

        private bool _down;
        private Point _from, _last;

        private static readonly Color Fill = Color.FromArgb(214, 220, 229), Line = Color.FromArgb(140, 150, 166),
                                      Dots = Color.FromArgb(70, 80, 96), Pressed = Color.FromArgb(31, 95, 168);

        public Grip(SizeGrips.Edge edges, bool corner)
        {
            Edges = edges;
            IsCorner = corner;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            bool lr = (edges & (SizeGrips.Edge.Left | SizeGrips.Edge.Right)) != 0, tb = (edges & (SizeGrips.Edge.Top | SizeGrips.Edge.Bottom)) != 0;
            bool nwse = edges == (SizeGrips.Edge.Left | SizeGrips.Edge.Top) || edges == (SizeGrips.Edge.Right | SizeGrips.Edge.Bottom);
            Cursor = corner ? (nwse ? Cursors.SizeNWSE : Cursors.SizeNESW) : lr ? Cursors.SizeWE : tb ? Cursors.SizeNS : Cursors.Default;
            AccessibleName = "Size";
            AccessibleRole = AccessibleRole.Grip;
        }

        /// <summary>Pressed now (tests).</summary>
        public bool IsPressed { get { return _down; } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            _down = true;
            _from = _last = PointToScreen(e.Location);
            Capture = true;
            if (Begin != null) Begin();
            Invalidate();
        }

        private bool _busy;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_down || _busy) return;
            var at = PointToScreen(e.Location);
            if (at == _last) return;
            _last = at;
            _busy = true;                                 // a splitter lays its page out as it moves: not again inside that
            try { if (Drag != null) Drag(at.X - _from.X, at.Y - _from.Y, false); }
            finally { _busy = false; }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_down) return;
            _down = false;
            Capture = false;
            var at = PointToScreen(e.Location);
            if (e.Location.X > -10000) _last = at;       // a press let go far away (TouchInput) ends where it was
            if (Drag != null) Drag(_last.X - _from.X, _last.Y - _from.Y, true);
            Invalidate();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (_down && !Capture) { _down = false; if (Drag != null) Drag(_last.X - _from.X, _last.Y - _from.Y, true); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var back = _down ? Pressed : Fill;
            var ink = _down ? Color.White : Dots;
            if (IsCorner)
            {
                // an arm of the corner's L: dots from the corner along it (the upright arm leaves the corner's own to the other)
                using (var b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);
                bool flat = Width >= Height, left = (Edges & SizeGrips.Edge.Left) != 0, top = (Edges & SizeGrips.Edge.Top) != 0;
                float t = Math.Min(Width, Height), d = Math.Max(3f, UiScale.Px(4)), step = d * 2.4f;
                using (var dot = new SolidBrush(ink))
                    for (int i = flat ? 0 : 1; i < (flat ? 3 : 4); i++)
                    {
                        float off = t / 2f + i * step;
                        float x = flat ? (left ? off : Width - off) : Width / 2f, y = flat ? Height / 2f : (top ? off : Height - off);
                        g.FillEllipse(dot, x - d / 2, y - d / 2, d, d);
                    }
                return;
            }
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float radius = Math.Min(r.Width, r.Height) / 2f;
            using (var path = Pill(r, radius))
            {
                using (var b = new SolidBrush(back)) g.FillPath(b, path);
                using (var p = new Pen(_down ? Pressed : Line)) g.DrawPath(p, path);
            }
            // three dots along it
            float dd = Math.Max(3f, UiScale.Px(4)), gap = dd * 2.2f, cx = Width / 2f, cy = Height / 2f;
            bool upright = Height >= Width;
            using (var dot = new SolidBrush(ink))
                for (int i = -1; i <= 1; i++)
                {
                    float x = upright ? cx : cx + i * gap, y = upright ? cy + i * gap : cy;
                    g.FillEllipse(dot, x - dd / 2, y - dd / 2, dd, dd);
                }
        }

        private static GraphicsPath Pill(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            if (d <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
