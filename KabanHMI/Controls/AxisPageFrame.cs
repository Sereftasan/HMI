using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// A PAGE OF AXIS ROWS AS A TABLE: its heading - the two lists (PLC axis
    /// items, Motion monitor) and the column names - stays at the top while the
    /// rows scroll up and down under it, and follows them sideways.
    ///
    /// A page of cylinder rows too (CylinderRowHMI: PneumaticPositioningHMI,
    /// CylinderEncoderSettingHMI - 9 October, "150% zoom: should I do the same
    /// here? Yes"): its column names stay at the top, and the rows' names at the
    /// left, as an axis page's.
    ///
    /// The designer's page is the same as ever (labels above, rows below); when
    /// it is shown its controls are put into this frame (Freeze): what is above
    /// the first row into the heading, the rows into the body, which scrolls.
    /// The page itself then no longer scrolls (IScrollsItself): its frame does.
    /// </summary>
    public sealed class AxisPageFrame : Panel, Kaban.UI.Helpers.IScrollsItself
    {
        public const string FrameName = "axisPageFrame";

        /// <summary>The heading: the lists and the column names. Not scrolled up and down.</summary>
        public Panel Head { get; private set; }
        /// <summary>In the heading, as wide as the rows: the column names and PLC axis items, moved sideways with the rows.</summary>
        public Panel HeadContent { get; private set; }
        /// <summary>The rows: scrolling both ways.</summary>
        public Panel Body { get; private set; }

        /// <summary>The body scrolled (sideways or up and down), or was laid out again.</summary>
        public event EventHandler Scrolled;

        public AxisPageFrame()
        {
            Name = FrameName;
            Dock = DockStyle.Fill;
            Head = new HeadPanel { Dock = DockStyle.Top, Name = "axisPageHead" };
            HeadContent = new Panel { Location = Point.Empty, Name = "axisPageHeadContent" };
            Head.Controls.Add(HeadContent);
            Body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Name = "axisPageBody" };
            Controls.Add(Body);
            Controls.Add(Head);

            ScrollFollow.Follow(Body, Sync);
            Head.Resize += (s, e) => Sync();
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            Head.BackColor = HeadContent.BackColor = Body.BackColor = BackColor;
        }

        /// <summary>The heading put where the rows are: sideways with them, as wide as they are.</summary>
        private void Sync()
        {
            if (IsDisposed) return;
            int x = Body.AutoScrollPosition.X;
            int w = Math.Max(Body.DisplayRectangle.Width, Head.ClientSize.Width);
            int h = Math.Max(0, Head.ClientSize.Height - 1);
            if (HeadContent.Left != x || HeadContent.Width != w || HeadContent.Height != h) HeadContent.SetBounds(x, 0, w, h);
            StickNames();
            var handler = Scrolled;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// THE AXIS NAMES STAY AT THE LEFT while the rows are scrolled sideways, and
        /// the Axis heading over them (6 October): which axis a parameter is of is
        /// always in sight.
        /// </summary>
        public void StickNames()
        {
            if (IsDisposed) return;
            int s = -Body.AutoScrollPosition.X;
            AxisSettingHMI top = null;
            foreach (var row in Body.Controls.OfType<AxisSettingHMI>())
            {
                if (row.MonitorMode) continue;
                row.StickName(s, row.Left + s);
                if (top == null || row.Top < top.Top) top = row;
            }
            if (top != null) top.StickHeading(HeadContent, s, top.Left + s);

            // the cylinder rows' names, and the room over them in the heading
            CylinderRowHMI topRow = null;
            foreach (var row in Body.Controls.OfType<CylinderRowHMI>())
            {
                row.StickName(s, row.Left + s);
                if (row.Visible && (topRow == null || row.Top < topRow.Top)) topRow = row;
            }
            if (topRow != null) topRow.StickHeading(HeadContent, s, topRow.Left + s);
        }

        /// <summary>A row the frame is made for: an axis row (not the Axis Monitor's), a cylinder row.</summary>
        private static bool IsRow(Control c)
        {
            var axis = c as AxisSettingHMI;
            return axis != null ? !axis.MonitorMode : c is CylinderRowHMI;
        }

        /// <summary>The frame a page of rows is in - from the page, the body or a row - or null.</summary>
        public static AxisPageFrame Of(Control c)
        {
            if (c == null) return null;
            var inPage = c.Controls[FrameName] as AxisPageFrame;
            if (inPage != null) return inPage;
            for (var p = c; p != null; p = p.Parent)
            {
                var f = p as AxisPageFrame;
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>The frame whose body <paramref name="body"/> is, or null.</summary>
        public static AxisPageFrame OfBody(Control body)
        {
            var f = body != null ? body.Parent as AxisPageFrame : null;
            return f != null && f.Body == body ? f : null;
        }

        /// <summary>
        /// Puts a page of axis rows into a frame: what is above the first row (the
        /// lists, the column names) into the heading, the rest into the body, which
        /// scrolls; the page no longer scrolls itself. Nothing to do for a page that
        /// is framed already, has no rows, or is not one that scrolls.
        /// <paramref name="keepScroll"/> false: the rows shown from the left top
        /// whatever the page was scrolled to - a page of cylinder rows, scrolled
        /// only by the focus its first row got as it was shown (the row, wider
        /// than the window, put at the left edge: its margin out of sight).
        /// </summary>
        public static AxisPageFrame Freeze(Control page, bool keepScroll = true)
        {
            var sc = page as ScrollableControl;
            if (sc == null || page.IsDisposed || page is AxisPageFrame || OfBody(page) != null) return null;
            if (page.Controls[FrameName] is AxisPageFrame) return (AxisPageFrame)page.Controls[FrameName];
            var rows = page.Controls.Cast<Control>().Where(IsRow).ToList();
            if (rows.Count == 0) return null;

            // measured unscrolled, where the designer put them
            var scrolled = sc.AutoScrollPosition;
            if (sc.AutoScroll) sc.AutoScrollPosition = Point.Empty;
            int line = rows.Min(r => r.Top);

            var frame = new AxisPageFrame { BackColor = page.BackColor };
            var originals = page.Controls.Cast<Control>().ToList();
            page.SuspendLayout();
            try
            {
                // the page stops scrolling FIRST: taking its rows away must not
                // scroll what is left of it (Mono did, moving the lists up)
                var at0 = originals.ToDictionary(c => c, c => c.Location);
                sc.AutoScroll = false;
                sc.AutoScrollMinSize = Size.Empty;
                // The frame added EMPTY, its heading's height set after: on a page
                // at a larger screen scale a control added is scaled, and a heading
                // measured on the scaled page was scaled once more - a band of white
                // between the column names and the rows (6 October).
                page.Controls.Add(frame);
                frame.Head.Height = line;
                frame.SuspendLayout();
                foreach (var c in originals)
                {
                    var was = at0[c];
                    if (IsRow(c) || c is AxisSettingHMI || was.Y >= line - 2)
                    {
                        frame.Body.Controls.Add(c);
                        c.Location = new Point(was.X, was.Y - line);
                    }
                    else
                    {
                        (c.Name == AxisSettingHMI.MotionBarName ? (Control)frame.Head : frame.HeadContent).Controls.Add(c);
                        c.Location = was;
                    }
                }
                frame.HeadContent.SendToBack();
            }
            finally
            {
                frame.ResumeLayout(true);
                page.ResumeLayout(true);
            }
            frame.Sync();
            if (keepScroll && scrolled != Point.Empty) frame.Body.AutoScrollPosition = new Point(-scrolled.X, -scrolled.Y);
            return frame;
        }

        /// <summary>The heading's panel: a thin rule under the column names, as a table's.</summary>
        private sealed class HeadPanel : Panel
        {
            public HeadPanel()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var pen = new Pen(UiStyle.GridRule)) e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
        }
    }

    /// <summary>
    /// Something told whenever a scrolling container moves its content: a
    /// scroll bar, the wheel, a scroll set in code (every child is moved then),
    /// a layout (the content's size).
    /// </summary>
    public static class ScrollFollow
    {
        public static void Follow(ScrollableControl scroller, Action moved)
        {
            var hooked = new HashSet<Control>();
            // Told at once, and once more when the scroll is over: while the
            // children are being moved the scroll position may not be the new one
            // yet (Mono moves them first).
            bool pending = false;
            Action tell = () =>
            {
                moved();
                if (pending || !scroller.IsHandleCreated || scroller.IsDisposed) return;
                pending = true;
                try { scroller.BeginInvoke(new Action(() => { pending = false; if (!scroller.IsDisposed) moved(); })); }
                catch { pending = false; }
            };
            EventHandler onMove = (s, e) => tell();
            Action<Control> hook = c => { if (hooked.Add(c)) c.LocationChanged += onMove; };
            foreach (Control c in scroller.Controls) hook(c);
            scroller.ControlAdded += (s, e) => hook(e.Control);
            scroller.ControlRemoved += (s, e) => { if (hooked.Remove(e.Control)) e.Control.LocationChanged -= onMove; };
            scroller.Scroll += (s, e) => tell();
            scroller.Layout += (s, e) => tell();
            scroller.Resize += (s, e) => tell();
        }
    }
}
