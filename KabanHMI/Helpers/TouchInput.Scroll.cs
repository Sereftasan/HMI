using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kaban.UI.Helpers
{
    // ======================================================================
    // A FINGER DRAG SCROLLS (29 October, the plan's 5: "finger scrolling: a
    // drag scrolls, a tap selects")
    // ----------------------------------------------------------------------
    // In touch mode a finger that moves more than about 2 mm drags what is
    // under it the way it moves - a table's rows and columns, a list, a page
    // too tall or wide for the screen, a long text - the press it started
    // with let go where it clicks nothing (a button the drag began on is not
    // pressed). A table's or a list's row is chosen on the lift, so a drag
    // does not choose the row it started on.
    // Not from where a drag is the work: a slider, a jog button, a splitter,
    // a chart, the drawing, a table's headings (their widths and order), a
    // tab strip (its tabs' order).
    // ======================================================================
    public static partial class TouchInput
    {
        /// <summary>What a finger drags (tests: the last drag's).</summary>
        public static string LastScrolled { get; private set; }

        private abstract class Scroller
        {
            public Control Control;
            public abstract bool Can(bool vertical);
            public abstract void Start();
            /// <summary>The finger this far from where the drag started (screen pixels).</summary>
            public abstract void Move(int dx, int dy);
        }

        static partial void DefersPart(Control window, Point client, ref bool defers)
        {
            var grid = window as DataGridView;
            if (grid != null)
            {
                var hit = grid.HitTest(client.X, client.Y);
                defers = hit.Type == DataGridViewHitTestType.Cell || hit.Type == DataGridViewHitTestType.RowHeader || hit.Type == DataGridViewHitTestType.None;
                return;
            }
            defers = window is ListBox;          // (Windows' own tree and list view handle a press themselves: not kept back)
        }

        static partial void StartPanPart(Press p, Point screen, ref bool started)
        {
            var target = p.Target;
            if (target == null || target.IsDisposed || Excluded(target)) return;
            int dx = screen.X - p.Screen.X, dy = screen.Y - p.Screen.Y;
            bool vertical = Math.Abs(dy) >= Math.Abs(dx);
            var s = FindScroller(target, vertical, ToClient(target, p.Screen));
            if (s == null) return;
            p.Scroller = s;
            p.Panning = true;
            if (!p.Deferred) Send(p.Hwnd, p.Window ?? p.Owner, WmLButtonUp, 0, Far);    // the press it began with: let go, nothing clicked
            s.Start();
            LastScrolled = s.Control.Name.Length > 0 ? s.Control.Name : s.Control.GetType().Name;
            s.Move(dx, dy);
            started = true;
        }

        static partial void PanToPart(Press p, Point screen)
        {
            var s = p.Scroller as Scroller;
            if (s == null || s.Control.IsDisposed) return;
            try { s.Move(screen.X - p.Screen.X, screen.Y - p.Screen.Y); } catch { /* the list changed under the finger */ }
        }

        /// <summary>The first control from the finger outwards that can scroll the way it moves.</summary>
        private static Scroller FindScroller(Control target, bool vertical, Point client)
        {
            for (var c = target; c != null; c = c.Parent)
            {
                if (!ReferenceEquals(c, target) && DragsItself(c)) return null;
                Scroller s = null;
                var grid = c as DataGridView;
                if (grid != null)
                {
                    if (ReferenceEquals(c, target) && grid.HitTest(client.X, client.Y).Type == DataGridViewHitTestType.ColumnHeader) return null;
                    s = new GridScroller { Control = grid };
                }
                else if (c is ListBox) s = new ListScroller { Control = c };
                else if (IsWindows && (c is TreeView || c is ListView || (c is TextBoxBase && ((TextBoxBase)c).Multiline))) s = new NativeScroller { Control = c };
                else if (c is TabControl)
                {
                    var tabs = (TabControl)c;
                    if (ReferenceEquals(c, target) && client.Y < tabs.DisplayRectangle.Top) return null;   // the tabs: their own drag
                }
                else
                {
                    var sc = c as ScrollableControl;
                    if (sc != null && sc.AutoScroll && !(c is ToolStrip)) s = new PageScroller { Control = c };
                }
                if (s != null && s.Can(vertical)) return s;
                if (c is Form) break;
            }
            return null;
        }

        /// <summary>A part around the finger that takes a drag itself (a drawing, a chart): nothing under it scrolls.</summary>
        private static bool DragsItself(Control c)
        {
            if (c is IHandlesTouch) return true;
            string n = c.GetType().Name;
            return n == "DxfViewerControl" || n == "DashboardTimeline" || n.EndsWith("Chart");
        }

        // ---- a table ---------------------------------------------------------------------------------

        private sealed class GridScroller : Scroller
        {
            private int _firstRow, _offset, _rowHeight;
            private DataGridView G { get { return (DataGridView)Control; } }

            public override bool Can(bool vertical)
            {
                var g = G;
                var bar = g.Controls.OfType<ScrollBar>().FirstOrDefault(b => vertical ? b is VScrollBar : b is HScrollBar);
                if (bar != null) return bar.Visible && bar.Enabled;
                if (vertical) return g.DisplayedRowCount(false) < g.Rows.GetRowCount(DataGridViewElementStates.Visible);
                return ColumnsWidth(g) > g.ClientSize.Width;
            }

            public override void Start()
            {
                var g = G;
                _firstRow = Math.Max(0, g.FirstDisplayedScrollingRowIndex);
                _offset = g.HorizontalScrollingOffset;
                int i = g.FirstDisplayedScrollingRowIndex;
                _rowHeight = Math.Max(1, i >= 0 && i < g.Rows.Count ? g.Rows[i].Height : g.RowTemplate.Height);
            }

            public override void Move(int dx, int dy)
            {
                var g = G;
                if (g.RowCount > 0)
                {
                    // whole rows, as the table scrolls: the finger up, the rows up
                    int steps = (int)Math.Round(-dy / (double)_rowHeight);
                    int row = _firstRow;
                    for (; steps > 0; steps--) { int next = g.Rows.GetNextRow(row, DataGridViewElementStates.Visible); if (next < 0) break; row = next; }
                    for (; steps < 0; steps++) { int prev = g.Rows.GetPreviousRow(row, DataGridViewElementStates.Visible); if (prev < 0) break; row = prev; }
                    if (row != g.FirstDisplayedScrollingRowIndex)
                        try { g.FirstDisplayedScrollingRowIndex = row; } catch { }
                }
                int max = Math.Max(0, ColumnsWidth(g) - g.DisplayRectangle.Width);
                int to = Math.Max(0, Math.Min(max, _offset - dx));
                if (to != g.HorizontalScrollingOffset)
                    try { g.HorizontalScrollingOffset = to; } catch { }
            }
        }

        /// <summary>The columns' widths side by side, the row headings' with them.</summary>
        private static int ColumnsWidth(DataGridView g)
        {
            int w = g.RowHeadersVisible ? g.RowHeadersWidth : 0;
            foreach (DataGridViewColumn c in g.Columns) if (c.Visible) w += c.Width;
            return w;
        }

        // ---- a list ----------------------------------------------------------------------------------

        private sealed class ListScroller : Scroller
        {
            private int _top;
            private ListBox L { get { return (ListBox)Control; } }

            public override bool Can(bool vertical)
            {
                var l = L;
                return vertical && l.Items.Count > Math.Max(1, l.ClientSize.Height / Math.Max(1, l.ItemHeight));
            }

            public override void Start() { _top = L.TopIndex; }

            public override void Move(int dx, int dy)
            {
                var l = L;
                int to = Math.Max(0, Math.Min(l.Items.Count - 1, _top - (int)Math.Round(dy / (double)Math.Max(1, l.ItemHeight))));
                if (to != l.TopIndex) l.TopIndex = to;
            }
        }

        // ---- a page ------------------------------------------------------------------------------------

        private sealed class PageScroller : Scroller
        {
            private Point _origin;
            private ScrollableControl S { get { return (ScrollableControl)Control; } }

            public override bool Can(bool vertical)
            {
                var s = S;
                var shown = s.DisplayRectangle;
                return vertical ? shown.Height > s.ClientSize.Height + 1 : shown.Width > s.ClientSize.Width + 1;
            }

            public override void Start() { _origin = new Point(-S.AutoScrollPosition.X, -S.AutoScrollPosition.Y); }

            public override void Move(int dx, int dy)
            {
                var to = new Point(Math.Max(0, _origin.X - dx), Math.Max(0, _origin.Y - dy));
                if (to != new Point(-S.AutoScrollPosition.X, -S.AutoScrollPosition.Y)) S.AutoScrollPosition = to;
            }
        }

        // ---- a tree, a list view, a long text (Windows' own scroll bars) ---------------------------------

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);

        private sealed class NativeScroller : Scroller
        {
            private int _lines, _pixels, _lineHeight;

            public override bool Can(bool vertical)
            {
                if (!vertical) return false;
                try { return (GetWindowLong(Control.Handle, -16) & 0x00200000) != 0; } catch { return false; }   // GWL_STYLE, WS_VSCROLL
            }

            public override void Start() { _lines = 0; _pixels = 0; _lineHeight = Math.Max(1, Control.Font.Height); }

            public override void Move(int dx, int dy)
            {
                if (Control is ListView)
                {
                    int by = -dy - _pixels;                 // LVM_SCROLL: pixels
                    if (by != 0) { SendMessage(Control.Handle, 0x1014, IntPtr.Zero, (IntPtr)by); _pixels = -dy; }
                    return;
                }
                int want = (int)Math.Round(-dy / (double)_lineHeight);
                for (; _lines < want; _lines++) SendMessage(Control.Handle, 0x0115, (IntPtr)1, IntPtr.Zero);   // WM_VSCROLL, SB_LINEDOWN
                for (; _lines > want; _lines--) SendMessage(Control.Handle, 0x0115, IntPtr.Zero, IntPtr.Zero); // SB_LINEUP
            }
        }
    }
}
