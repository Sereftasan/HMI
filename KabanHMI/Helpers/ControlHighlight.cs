using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kaban.UI.Controls;

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
    /// The frame is four thin bars ON THE PAGE, beside the control - not a
    /// window of its own. It was a see-through, always-on-top window laid over
    /// the screen; on the panel PC it did not show at all, and a window over the
    /// page is exactly what the side panel is there to avoid. Bars on the page
    /// are drawn with the page, scroll with it, and cannot end up behind or in
    /// front of the wrong thing.
    /// </summary>
    public static class ControlHighlight
    {
        public static readonly Color Blue = Color.FromArgb(0, 120, 215);
        private static Frame _current;

        /// <summary>Points at a control; the previous frame, if any, goes.</summary>
        public static void Flash(Control target, int milliseconds = 5000)
        {
            if (_current != null) _current.Stop();
            _current = null;
            if (target == null || target.IsDisposed || target.Parent == null) return;
            _current = new Frame(target, milliseconds);
            _current.Start();
        }

        /// <summary>True while a frame is shown (for tests).</summary>
        public static bool IsShowing { get { return _current != null && _current.Running; } }

        /// <summary>The control pointed at (for tests).</summary>
        public static Control Target { get { return IsShowing ? _current.TargetControl : null; } }

        private sealed class Frame
        {
            private readonly Control _target;
            private readonly Control _host;
            private readonly List<Panel> _bars = new List<Panel>();
            private readonly Timer _timer = new Timer { Interval = 350 };
            private readonly DateTime _until;
            private bool _on = true;

            public bool Running { get; private set; }
            public Control TargetControl { get { return _target; } }

            public Frame(Control target, int milliseconds)
            {
                _target = target;
                _until = DateTime.Now.AddMilliseconds(milliseconds);
                // Beside the control, in the nearest container that places its
                // children where they are put: a flow or table layout would take
                // the bars as items of its own and move the controls around.
                _host = target.Parent;
                while (_host != null && (_host is FlowLayoutPanel || _host is TableLayoutPanel) && _host.Parent != null)
                    _host = _host.Parent;
                for (int i = 0; i < 4; i++)
                    _bars.Add(new Panel { BackColor = Blue, Enabled = false, TabStop = false });
                _timer.Tick += (s, e) => Tick();
            }

            public void Start()
            {
                if (_host == null) return;
                foreach (var b in _bars) { _host.Controls.Add(b); }
                Running = true;
                Place();
                _timer.Start();
            }

            /// <summary>Around the control, wherever it is now - the page may still be settling.</summary>
            private void Place()
            {
                if (_target.IsDisposed || _host.IsDisposed) { Stop(); return; }
                int t = Math.Max(2, UiScale.Px(3)), gap = Math.Max(2, UiScale.Px(3));
                var r = _host.RectangleToClient(_target.Parent.RectangleToScreen(_target.Bounds));
                r.Inflate(gap + t, gap + t);
                _bars[0].SetBounds(r.X, r.Y, r.Width, t);
                _bars[1].SetBounds(r.X, r.Bottom - t, r.Width, t);
                _bars[2].SetBounds(r.X, r.Y, t, r.Height);
                _bars[3].SetBounds(r.Right - t, r.Y, t, r.Height);
                foreach (var b in _bars) { b.Visible = _on; b.BringToFront(); }
            }

            private void Tick()
            {
                if (DateTime.Now > _until || _target.IsDisposed || !_target.Visible) { Stop(); return; }
                _on = !_on;
                Place();
            }

            public void Stop()
            {
                if (!Running && _bars.Count == 0) return;
                Running = false;
                _timer.Stop();
                _timer.Dispose();
                foreach (var b in _bars)
                {
                    if (b.Parent != null) b.Parent.Controls.Remove(b);
                    b.Dispose();
                }
                _bars.Clear();
            }
        }
    }
}
