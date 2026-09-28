using System;
using System.Drawing;
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
    public static class PageScroll
    {
        /// <summary>Lets a page scroll. Forms and tab hosts are left as they are.</summary>
        public static void Enable(Control view)
        {
            var sc = view as ScrollableControl;
            if (sc == null || view is Form || view is Kaban.UI.Settings.SettingsScreen) return;
            if (!sc.AutoScroll) sc.AutoScroll = true;
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
