using System;
using System.Drawing;
using System.Windows.Forms;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// A blinking blue frame around a control, for a few seconds: "here it is".
    ///
    /// Used by the PLC Monitor's Find after it has opened the page a device is
    /// used on - a page of twenty units is a lot to search by eye. A frame of its
    /// own, drawn AROUND the control, rather than asking the control to blink its
    /// border: every kind of control is pointed at the same way, whether or not it
    /// knows how to blink, and nothing about the control itself is changed.
    ///
    /// The frame lets clicks through and never takes the focus, so the unit under
    /// it can be used at once.
    /// </summary>
    public static class ControlHighlight
    {
        public static readonly Color Blue = Color.FromArgb(0, 120, 215);
        private static Frame _current;

        /// <summary>Points at a control; the previous frame, if any, goes.</summary>
        public static void Flash(Control target, int milliseconds = 5000)
        {
            if (_current != null && !_current.IsDisposed) _current.Close();
            _current = null;
            if (target == null || target.IsDisposed || !target.IsHandleCreated) return;
            _current = new Frame(target, milliseconds);
            _current.Start();
        }

        /// <summary>True while a frame is shown (for tests).</summary>
        public static bool IsShowing { get { return _current != null && !_current.IsDisposed; } }

        /// <summary>The control pointed at (for tests).</summary>
        public static Control Target { get { return IsShowing ? _current.TargetControl : null; } }

        private sealed class Frame : Form
        {
            private const int Thickness = 3, Gap = 3;
            private readonly Control _target;
            private readonly Timer _timer = new Timer { Interval = 350 };
            private DateTime _until;
            private bool _on = true;

            public Control TargetControl { get { return _target; } }

            public Frame(Control target, int milliseconds)
            {
                _target = target;
                _until = DateTime.Now.AddMilliseconds(milliseconds);
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                TopMost = true;
                BackColor = Blue;
                _timer.Tick += (s, e) => Tick();
            }

            protected override bool ShowWithoutActivation { get { return true; } }

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    // Tool window, never activated, clicks go through to the page.
                    cp.ExStyle |= 0x00000080 | 0x08000000 | 0x00080000 | 0x00000020;
                    return cp;
                }
            }

            public void Start()
            {
                Place();
                Opacity = 0.95;
                Show();
                _timer.Start();
            }

            /// <summary>Around the control, wherever it is now - the page may still be settling.</summary>
            private void Place()
            {
                if (_target.IsDisposed || !_target.IsHandleCreated) return;
                var r = _target.RectangleToScreen(_target.ClientRectangle);
                r.Inflate(Gap + Thickness, Gap + Thickness);
                if (Bounds != r) Bounds = r;
                using (var outer = new Region(new Rectangle(0, 0, r.Width, r.Height)))
                {
                    outer.Exclude(new Rectangle(Thickness, Thickness, r.Width - 2 * Thickness, r.Height - 2 * Thickness));
                    var old = Region;
                    Region = outer.Clone();
                    if (old != null) old.Dispose();
                }
            }

            private void Tick()
            {
                if (DateTime.Now > _until || _target.IsDisposed || !_target.Visible) { Close(); return; }
                _on = !_on;
                Place();
                Opacity = _on ? 0.95 : 0.0;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) { _timer.Stop(); _timer.Dispose(); }
                base.Dispose(disposing);
            }
        }
    }
}
