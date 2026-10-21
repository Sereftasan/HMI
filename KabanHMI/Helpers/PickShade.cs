using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// THE SHADE WHILE THE NOTES PAGE PICKS (21 October: "make the rest of the screen darker when
    /// Picking is on, so the user understands that it is for selecting the control for feedback,
    /// not to command"): the parts of the window a tap picks in - the page on show and the caption
    /// bar, not the menu (its buttons work as ever) nor the notes page - darker, the control under
    /// the pointer (or the one picked) left bright in it.
    ///
    /// A see-through window over them that takes no click (WS_EX_LAYERED | WS_EX_TRANSPARENT,
    /// HTTRANSPARENT): the taps go to the controls below, as they did - the picking is the notes
    /// page's, unchanged. It follows the window and the page (a side panel opened narrows the page)
    /// and goes with the picking. Shown on Windows; elsewhere (the tests) only worked out.
    /// </summary>
    public sealed class PickShade : IDisposable
    {
        /// <summary>How dark (the window's opacity over the page).</summary>
        public const double Darkness = 0.38;
        /// <summary>The window is made (Windows); otherwise the shade is only worked out (the tests).</summary>
        public static bool CanShow = Environment.OSVersion.Platform == PlatformID.Win32NT;

        private readonly Func<IEnumerable<Control>> _areas;
        private readonly Timer _timer = new Timer { Interval = 150 };
        private ShadeWindow _window;
        private Control _hole;
        private bool _on;
        private string _drawn = "";

        public PickShade(Func<IEnumerable<Control>> areas)
        {
            _areas = areas ?? (() => new Control[0]);
            _timer.Tick += (s, e) => Sync();
        }

        /// <summary>Shading (the notes page picking).</summary>
        public bool On
        {
            get { return _on; }
            set
            {
                if (_on == value) return;
                _on = value;
                if (value) { _timer.Start(); Sync(); }
                else { _timer.Stop(); Shaded = new Rectangle[0]; HideWindow(); }
            }
        }

        /// <summary>The control left bright: under the pointer, or picked.</summary>
        public Control Hole
        {
            get { return _hole; }
            set { if (ReferenceEquals(_hole, value)) return; _hole = value; if (_on) Sync(); }
        }

        /// <summary>The parts shaded, on the screen (tests).</summary>
        public IList<Rectangle> Shaded { get; private set; } = new Rectangle[0];
        /// <summary>The bright place in it, on the screen (empty: none).</summary>
        public Rectangle HoleRect { get; private set; }
        /// <summary>The window on the screen.</summary>
        public bool WindowShown { get { return _window != null && !_window.IsDisposed && _window.Visible; } }

        private static bool InSight(Control c)
        {
            if (c == null || c.IsDisposed || !c.IsHandleCreated) return false;
            for (var p = c; p != null; p = p.Parent) if (!p.Visible) return false;
            return true;
        }

        /// <summary>The shade as the window and the page are now.</summary>
        public void Sync()
        {
            List<Control> areas;
            try { areas = (_areas() ?? new Control[0]).Where(InSight).ToList(); }
            catch { areas = new List<Control>(); }
            var owner = areas.Select(a => a.FindForm()).FirstOrDefault(f => f != null);
            if (!_on || owner == null || !owner.Visible || owner.WindowState == FormWindowState.Minimized || areas.Count == 0)
            {
                Shaded = new Rectangle[0];
                HoleRect = Rectangle.Empty;
                HideWindow();
                return;
            }
            var rects = areas.Select(a => a.RectangleToScreen(a.ClientRectangle)).Where(r => r.Width > 0 && r.Height > 0).ToList();
            Shaded = rects;
            HoleRect = InSight(_hole) && !(_hole is Form) ? Rectangle.Inflate(_hole.RectangleToScreen(_hole.ClientRectangle), 5, 5) : Rectangle.Empty;
            if (!CanShow || rects.Count == 0) return;

            var bounds = rects.Aggregate(Rectangle.Union);
            string drawn = bounds + "|" + string.Join(";", rects) + "|" + HoleRect;
            if (_window == null || _window.IsDisposed) { _window = new ShadeWindow(); _drawn = ""; }
            if (drawn != _drawn)
            {
                _drawn = drawn;
                var region = new Region(Rectangle.Empty);
                foreach (var r in rects) region.Union(new Rectangle(r.X - bounds.X, r.Y - bounds.Y, r.Width, r.Height));
                if (!HoleRect.IsEmpty) region.Exclude(new Rectangle(HoleRect.X - bounds.X, HoleRect.Y - bounds.Y, HoleRect.Width, HoleRect.Height));
                if (_window.Bounds != bounds) _window.Bounds = bounds;
                var old = _window.Region;
                _window.Region = region;
                if (old != null) old.Dispose();
            }
            try
            {
                if (!_window.Visible) _window.Show(owner);
                else if (!ReferenceEquals(_window.Owner, owner)) _window.Owner = owner;
            }
            catch { }
        }

        private void HideWindow()
        {
            if (_window != null && !_window.IsDisposed && _window.Visible) _window.Hide();
            _drawn = "";
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
            if (_window != null && !_window.IsDisposed) _window.Dispose();
            _window = null;
        }

        /// <summary>The darker glass: no border, no taskbar button, never active, never a target of the mouse.</summary>
        private sealed class ShadeWindow : Form
        {
            private const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
            private const int WM_NCHITTEST = 0x84, HTTRANSPARENT = -1, WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;

            public ShadeWindow()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                ControlBox = false;
                Text = "";
                StartPosition = FormStartPosition.Manual;
                BackColor = Color.Black;
                Opacity = Darkness;
                Name = "notesPickShade";
            }

            protected override bool ShowWithoutActivation { get { return true; } }

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                    return cp;
                }
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_NCHITTEST) { m.Result = (IntPtr)HTTRANSPARENT; return; }
                if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)MA_NOACTIVATE; return; }
                base.WndProc(ref m);
            }
        }
    }
}
