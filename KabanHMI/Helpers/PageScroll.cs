using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// EVERY PAGE SCROLLS WHEN IT DOES NOT FIT.
    ///
    /// A page is shown docked to fill the room it is given, and a docked page is
    /// never larger than that room - so the content area's own scroll bars never
    /// appear, whatever it is set to. A page designed for the whole screen simply
    /// lost its right-hand side when a side page narrowed it, or when the
    /// magnifying factor enlarged it: no scroll bar, nothing to reach it with, and
    /// a unit found by Find could sit out of sight behind the edge.
    ///
    /// The scrolling has to be the PAGE's own: set on the page when it is shown
    /// (MainForm.LoadScreen, SettingsScreen.ShowPage, the side panel), it gives the
    /// page scroll bars as soon as its controls reach past its edge. Pages built of
    /// docked parts (a table filling the page) are unaffected - a docked part never
    /// reaches past the edge.
    /// </summary>
    /// <summary>
    /// A part of a page that scrolls itself (a page of axis rows, whose heading
    /// stays while its rows scroll): the page around it is left unscrolled -
    /// two scroll bars for one thing would be one too many.
    /// </summary>
    public interface IScrollsItself { }

    public static class PageScroll
    {
        /// <summary>
        /// The least room a page made of DOCKED parts is given before it scrolls,
        /// at factor 1. Docked parts never reach past the page's edge, so such a
        /// page never scrolled: squeezed by a side page or enlarged by the
        /// magnifier, its table lost its last columns and its bar its last
        /// buttons with no scroll bar to reach them. Below this it scrolls.
        /// </summary>
        public const int MinDockedWidth = 900, MinDockedHeight = 450;

        /// <summary>
        /// Lets a page scroll. Forms and tab hosts are left as they are. A page
        /// laid out by docking gets a least size (MinDockedWidth x MinDockedHeight)
        /// - <paramref name="minimum"/> false leaves it out (the side panel, whose
        /// pages are made for its narrow width).
        /// </summary>
        public static void Enable(Control view, bool minimum = true)
        {
            var sc = view as ScrollableControl;
            if (sc == null || view is Form || view is Kaban.UI.Settings.SettingsScreen) return;
            foreach (Control c in view.Controls)
                if (c is IScrollsItself)
                {
                    sc.AutoScroll = false;
                    sc.AutoScrollMinSize = Size.Empty;
                    return;
                }
            if (!sc.AutoScroll) sc.AutoScroll = true;
            if (!minimum || !sc.AutoScrollMinSize.IsEmpty) return;
            bool docked = false;
            foreach (Control c in view.Controls)
                if (c.Dock != DockStyle.None && c.Visible) { docked = true; break; }
            if (docked)
            {
                // 23 October: "Alarms and HMI Setting pages (apart from Users and Preferences tab) don't get narrowed
                // to the active page area so some part of these screens can't be seen without closing the second
                // page": a page of a table and a bar was given the 900 pixels whatever it holds - beside a side page
                // it went on under it, the table's last columns and its own scroll bars with it. The room it needs
                // now: its table scrolls itself, its bar's buttons wrap or overflow - the most of its parts' least
                // widths, never more than 900.
                int most = Kaban.UI.Controls.UiScale.Px(MinDockedWidth);
                int need;
                try { need = Math.Min(most, NeededWidth(view, 0)); }
                catch { need = most; }
                sc.AutoScrollMinSize = new Size(need, Kaban.UI.Controls.UiScale.Px(MinDockedHeight));
            }
        }

        /// <summary>The least room a part keeps for itself: a table, a list, a text box scroll themselves.</summary>
        private static int Least { get { return Kaban.UI.Controls.UiScale.Px(240); } }

        /// <summary>
        /// The width a control needs to show all of itself, laid out as it is: its own scrolling parts (a table, a
        /// list, a panel that scrolls) their least, a bar that wraps its widest button, one that does not all of
        /// them, controls placed in it as far right as they reach. Deep pages: the deepest parts as they are.
        /// </summary>
        internal static int NeededWidth(Control c, int depth)
        {
            if (c == null || !c.Visible && depth > 0) return 0;
            int own = Math.Max(c.MinimumSize.Width, 0);
            if (depth > 12) return Math.Max(own, c.Width);
            if (c is DataGridView || c is ListView || c is TreeView || c is ListBox || c is TextBoxBase || c is WebBrowser || c is IScrollsItself)
                return Math.Max(own, Least);
            if (c is ToolStrip) return Math.Max(own, ((ToolStrip)c).CanOverflow ? Least : PreferredWidthOf(c));
            if (c is Label && (((Label)c).AutoEllipsis || !((Label)c).AutoSize) && c.Dock != DockStyle.None) return Math.Max(own, Least / 2);
            var sc = c as ScrollableControl;
            // a part that scrolls what is placed in it: its least - what is docked in it it does not scroll
            if (depth > 0 && sc != null && sc.AutoScroll && !(c is Form) &&
                !c.Controls.Cast<Control>().Any(k => k.Visible && k.Dock != DockStyle.None))
                return Math.Max(own, Math.Max(Least, Math.Min(sc.AutoScrollMinSize.Width, Least)));
            var tabs = c as TabControl;
            if (tabs != null)
            {
                int w = 0;
                foreach (TabPage p in tabs.TabPages) w = Math.Max(w, NeededWidth(p, depth + 1));
                return Math.Max(own, w + 8);
            }
            var split = c as SplitContainer;
            if (split != null)
            {
                int a = NeededWidth(split.Panel1, depth + 1), b = NeededWidth(split.Panel2, depth + 1);
                return Math.Max(own, split.Orientation == Orientation.Vertical ? a + b + split.SplitterWidth : Math.Max(a, b));
            }
            var flow = c as FlowLayoutPanel;
            if (flow != null)
            {
                var items = flow.Controls.Cast<Control>().Where(k => k.Visible).ToList();
                bool across = flow.FlowDirection == FlowDirection.LeftToRight || flow.FlowDirection == FlowDirection.RightToLeft;
                int w = !across || flow.WrapContents
                    ? items.Select(k => (k.AutoSize || k.Dock != DockStyle.None ? NeededWidth(k, depth + 1) : k.Width) + k.Margin.Horizontal).DefaultIfEmpty(0).Max()
                    : items.Sum(k => k.Width + k.Margin.Horizontal);
                return Math.Max(own, w + flow.Padding.Horizontal);
            }
            var table = c as TableLayoutPanel;
            if (table != null)
            {
                int w = 0;
                for (int col = 0; col < table.ColumnCount; col++)
                {
                    var style = col < table.ColumnStyles.Count ? table.ColumnStyles[col] : null;
                    if (style != null && style.SizeType == SizeType.Absolute) { w += (int)style.Width; continue; }
                    int most = 0;
                    foreach (Control k in table.Controls)
                    {
                        if (!k.Visible || table.GetColumn(k) != col || table.GetColumnSpan(k) != 1) continue;
                        int kw = k.Dock == DockStyle.Fill || (k.Anchor & (AnchorStyles.Left | AnchorStyles.Right)) == (AnchorStyles.Left | AnchorStyles.Right)
                            ? NeededWidth(k, depth + 1) : k.Width;
                        most = Math.Max(most, kw + k.Margin.Horizontal);
                    }
                    w += most;
                }
                return Math.Max(own, w + table.Padding.Horizontal);
            }
            if (c.Controls.Count == 0) return Math.Max(own, c.Width);

            // a container: its docked parts round what fills it; the rest where it is placed
            int sides = 0, middle = 0, placed = 0;
            foreach (Control k in c.Controls)
            {
                if (!k.Visible) continue;
                switch (k.Dock)
                {
                    case DockStyle.Left: case DockStyle.Right: sides += k.Width; break;
                    case DockStyle.Fill: case DockStyle.Top: case DockStyle.Bottom: middle = Math.Max(middle, NeededWidth(k, depth + 1)); break;
                    default:
                        bool stretches = (k.Anchor & (AnchorStyles.Left | AnchorStyles.Right)) == (AnchorStyles.Left | AnchorStyles.Right);
                        int left = sc != null ? k.Left - sc.AutoScrollPosition.X : k.Left;
                        placed = Math.Max(placed, left + (stretches ? Math.Min(k.Width, NeededWidth(k, depth + 1)) : k.Width));
                        break;
                }
            }
            var pad = c is ScrollableControl ? ((ScrollableControl)c).Padding.Horizontal : 0;
            return Math.Max(own, Math.Max(sides + middle, placed) + pad);
        }

        private static int PreferredWidthOf(Control c)
        {
            try { return c.GetPreferredSize(Size.Empty).Width; } catch { return c.Width; }
        }

        /// <summary>
        /// Scrolls every scrolling container between the control and <paramref name="stop"/>
        /// so the control is in sight - centred in the view when it was not in it,
        /// left where it is when it was. Innermost first, so each outer one sees the
        /// control where the inner one put it.
        /// </summary>
        public static void Reveal(Control target, Control stop)
        {
            if (target == null) return;
            for (Control p = target.Parent; p != null && p != stop; p = p.Parent)
            {
                var sc = p as ScrollableControl;
                if (sc == null || !sc.AutoScroll || !sc.IsHandleCreated || target.Parent == null) continue;
                sc.PerformLayout();
                var r = sc.RectangleToClient(target.Parent.RectangleToScreen(target.Bounds));
                var client = sc.ClientRectangle;
                if (client.Width <= 0 || client.Height <= 0 || client.Contains(r)) continue;

                // Centred - or, for a control wider (taller) than the view, its
                // left (top) edge at the view's, where its caption and first button are.
                int x = -sc.AutoScrollPosition.X, y = -sc.AutoScrollPosition.Y;
                if (r.Left < client.Left || r.Right > client.Right)
                    x = Math.Max(0, r.Width >= client.Width ? x + r.Left - client.Left
                                                            : x + r.Left + r.Width / 2 - client.Width / 2);
                if (r.Top < client.Top || r.Bottom > client.Bottom)
                    y = Math.Max(0, r.Height >= client.Height ? y + r.Top - client.Top
                                                              : y + r.Top + r.Height / 2 - client.Height / 2);
                sc.AutoScrollPosition = new Point(x, y);
            }
        }
    }
}
