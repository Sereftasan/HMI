using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// THE SCREEN ABOVE THE KEYBOARD (2 November: "It should cover the bottom side of the screen totally so the vertical
    /// scrolls on all the navigation side panel, main page and second page should be positioned and shaped as if the screen
    /// height pixel is as much lower as it's height").
    ///
    /// While a keyboard is along the bottom of a screen - the HMI's own (TouchKeyboard) or Windows' On-Screen Keyboard
    /// (WindowsKeyboard) - the program's windows that fill the screen (maximised: the main window, a page in a window of its
    /// own) end at its top: the navigation panel, the page and the side page laid out, and scrolling, as on a screen that much
    /// lower. The box being typed in is scrolled into sight above the keyboard; a dialog it would cover goes up above it. All
    /// of it given back when the keyboard goes.
    /// </summary>
    public static class KeyboardRoom
    {
        private static Rectangle _band;

        /// <summary>The keyboard's part of the screen (screen coordinates); empty while none is along the bottom of a screen.</summary>
        public static Rectangle Band { get { return _band; } }

        /// <summary>Raised when a keyboard comes, goes or moves - after the windows were fitted above it.</summary>
        public static event EventHandler Changed;

        /// <summary>How far above the keyboard the box typed in is kept, at factor 1.</summary>
        public const int RoomAbove = 12;

        /// <summary>A screen's working area above the keyboard (all of it while no keyboard is on that screen).</summary>
        public static Rectangle WorkArea(Screen screen) { return WorkArea(screen.WorkingArea, _band); }

        /// <summary>A working area above a keyboard's place: at least a third of it stays (a keyboard taller than the rest covers).</summary>
        public static Rectangle WorkArea(Rectangle area, Rectangle band)
        {
            if (band.IsEmpty || !band.IntersectsWith(area)) return area;
            int bottom = Math.Max(area.Top + area.Height / 3, Math.Min(area.Bottom, band.Top));
            return new Rectangle(area.X, area.Y, area.Width, bottom - area.Y);
        }

        /// <summary>
        /// The keyboard's place (an empty rectangle: gone): the program's windows that fill the screen fitted above it - or
        /// given the whole screen back.
        /// </summary>
        public static void SetBand(Rectangle band)
        {
            if (band == _band) return;
            var was = _band;
            _band = band;
            foreach (var f in Windows()) Refit(f, was);
            var h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }

        private static List<Form> Windows()
        {
            var list = new List<Form>();
            try { foreach (Form f in Application.OpenForms) list.Add(f); } catch { }
            return list;
        }

        /// <summary>A window that fills its screen: maximised (tests: one the test bench, without a window manager, cannot maximise).</summary>
        public static Func<Form, bool> FillsScreen = f => f.WindowState == FormWindowState.Maximized;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        private const uint SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200;

        /// <summary>A maximised window's bottom at the keyboard's top (or the screen's again); its other edges where they are.</summary>
        private static void Refit(Form f, Rectangle was)
        {
            try
            {
                if (f == null || f.IsDisposed || !f.IsHandleCreated || !FillsScreen(f)) return;
                if (f.Name == TouchInput.TouchKeyboardName) return;
                var area = Screen.FromControl(f).WorkingArea;
                var before = WorkArea(area, was);
                var after = WorkArea(area, _band);
                if (before == after) return;
                var b = f.Bounds;
                // a window in Windows' own frame stands a few pixels past the area: that much past the new one too
                int height = Math.Max(f.MinimumSize.Height, b.Height + after.Bottom - before.Bottom);
                if (TouchInput.IsWindows)
                {
                    try
                    {
                        // Windows' own call: WinForms would take a size given to a maximised window as the one to restore it to
                        SetWindowPos(f.Handle, IntPtr.Zero, b.X, b.Y, b.Width, height, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
                        return;
                    }
                    catch (DllNotFoundException) { }
                    catch (EntryPointNotFoundException) { }
                }
                f.Bounds = new Rectangle(b.X, b.Y, b.Width, height);                // elsewhere (the tests' Mono)
            }
            catch (Exception ex) { Kaban.Core.Helpers.AuditLogger.Log("keyboard: window not fitted above it (" + ex.Message + ")."); }
        }

        // ---- the box above the keyboard ------------------------------------------------------------
        //
        // The box being typed in is brought into sight above the keyboard: a dialog the keyboard would cover moved up above it
        // (all of it when it fits); the scrolling parts around the box scrolled until it shows above the keyboard - a page the
        // keyboard still covers (a window that does not fill the screen) given room below its last control first, a page whose
        // parts are docked kept above the keyboard; a table's rows scrolled to the cell being edited. All of it given back when
        // the keyboard goes.

        private sealed class Room
        {
            public ScrollableControl Host;
            public Size MinSize;
            public Point Scroll;
            public Padding Pad;
            public bool Padded, Sized;
            public Form Moved;
            public Point WasAt;
        }

        private static readonly List<Room> _rooms = new List<Room>();

        private static Rectangle BoxOf(Control c) { return c.RectangleToScreen(c.ClientRectangle); }

        private static bool UnderBand(Rectangle r) { return !_band.IsEmpty && r.Right > _band.Left && r.Left < _band.Right; }

        /// <summary>How far the keyboard covers the box, with the finger's room above it (0: not, or no keyboard).</summary>
        public static int Covered(Control target)
        {
            if (_band.IsEmpty || target == null || target.IsDisposed || !target.IsHandleCreated || !target.Visible) return 0;
            Rectangle box;
            try { box = BoxOf(target); } catch { return 0; }
            if (!UnderBand(box)) return 0;
            return Math.Max(0, box.Bottom + UiScale.Px(RoomAbove) - _band.Top);
        }

        private static bool DocksItsParts(Control host)
        {
            foreach (Control c in host.Controls)
                if (c.Visible && (c.Dock == DockStyle.Fill || c.Dock == DockStyle.Bottom || c.Dock == DockStyle.Left || c.Dock == DockStyle.Right)) return true;
            return false;
        }

        /// <summary>The box brought into sight above the keyboard (the room made before given back first).</summary>
        public static void Make(Control target)
        {
            GiveBack();
            if (_band.IsEmpty || target == null || target.IsDisposed || !target.IsHandleCreated) return;
            try
            {
                int above = UiScale.Px(RoomAbove);
                var form = target.FindForm();
                bool dialog = form != null && form.WindowState == FormWindowState.Normal && !FillsScreen(form) && form.Name != TouchInput.TouchKeyboardName && !form.IsMdiChild;
                // a dialog the keyboard covers: above it, all of it when it fits (its buttons too - the login's)
                if (dialog && UnderBand(form.Bounds) && form.Bottom > _band.Top)
                {
                    var area = WorkArea(Screen.FromControl(form));
                    if (form.Height <= area.Height)
                    {
                        _rooms.Add(new Room { Moved = form, WasAt = form.Location });
                        form.Location = new Point(form.Left, Math.Max(area.Top, area.Bottom - form.Height));
                    }
                }
                // the scrolling parts around the box, innermost first: each scrolled until the box shows above the keyboard
                for (var c = target.Parent; c != null && !(c is Form); c = c.Parent)
                {
                    var sc = c as ScrollableControl;
                    if (sc == null || !sc.AutoScroll || !sc.IsHandleCreated || !sc.Visible) continue;
                    var view = BoxOf(sc);
                    var box = BoxOf(target);
                    bool covered = UnderBand(view) && view.Bottom > _band.Top - above;
                    int seenBottom = covered ? Math.Min(view.Bottom, _band.Top - above) : view.Bottom;
                    int down = box.Bottom - seenBottom, up = view.Top - box.Top;
                    if (down <= 0 && up <= 0) continue;
                    var room = new Room { Host = sc, MinSize = sc.AutoScrollMinSize, Scroll = new Point(-sc.AutoScrollPosition.X, -sc.AutoScrollPosition.Y), Pad = sc.Padding };
                    _rooms.Add(room);
                    if (down <= 0)
                    {
                        sc.AutoScrollPosition = new Point(room.Scroll.X, Math.Max(0, room.Scroll.Y - up));
                        continue;
                    }
                    int under = covered ? view.Bottom - _band.Top : 0;            // how much of it the keyboard covers
                    if (under > 0 && DocksItsParts(sc))
                    {
                        // a page of docked parts: they keep above the keyboard
                        room.Padded = true;
                        sc.Padding = new Padding(room.Pad.Left, room.Pad.Top, room.Pad.Right, room.Pad.Bottom + under);
                        sc.PerformLayout();
                        continue;
                    }
                    int to = room.Scroll.Y + Math.Min(down, Math.Max(0, box.Top - view.Top));
                    int most = Math.Max(0, sc.DisplayRectangle.Height - sc.ClientSize.Height);
                    if (to > most && under > 0)
                    {
                        // room below its last control as tall as the keyboard covers of it
                        room.Sized = true;
                        int content = Math.Max(sc.DisplayRectangle.Height, room.MinSize.Height);
                        sc.AutoScrollMinSize = new Size(room.MinSize.Width, content + under);
                    }
                    sc.AutoScrollPosition = new Point(room.Scroll.X, to);
                }
                // a table's cell being edited: its rows scrolled to it
                var grid = target as DataGridView ?? (target is IDataGridViewEditingControl ? ((IDataGridViewEditingControl)target).EditingControlDataGridView : null);
                if (grid != null && Covered(target) > 0 && grid.CurrentCell != null)
                {
                    int shown = Math.Max(1, grid.DisplayedRowCount(false) - 1);
                    int first = Math.Max(0, grid.CurrentCell.RowIndex - shown + 1);
                    try { if (grid.FirstDisplayedScrollingRowIndex < first) grid.FirstDisplayedScrollingRowIndex = first; } catch { }
                }
                // a window too tall to go above the keyboard whole, the box still under it: up as far as the box needs
                int still = Covered(target);
                if (still > 0 && dialog && !form.IsDisposed)
                {
                    var screen = Screen.FromControl(form).WorkingArea;
                    int up = Math.Min(still, Math.Max(0, form.Top - screen.Top));
                    if (up > 0)
                    {
                        bool movedAlready = _rooms.Exists(r => r.Moved == form);
                        if (!movedAlready) _rooms.Add(new Room { Moved = form, WasAt = form.Location });
                        form.Location = new Point(form.Left, form.Top - up);
                    }
                }
            }
            catch (Exception ex) { Kaban.Core.Helpers.AuditLogger.Log("keyboard: room for the box not made (" + ex.Message + ")."); }
        }

        /// <summary>The pages scrolled back and the windows moved back where they were.</summary>
        public static void GiveBack()
        {
            for (int i = _rooms.Count - 1; i >= 0; i--)
            {
                var r = _rooms[i];
                try
                {
                    if (r.Moved != null) { if (!r.Moved.IsDisposed) r.Moved.Location = r.WasAt; continue; }
                    if (r.Host == null || r.Host.IsDisposed) continue;
                    if (r.Padded) { r.Host.Padding = r.Pad; continue; }
                    if (r.Sized) r.Host.AutoScrollMinSize = r.MinSize;
                    r.Host.AutoScrollPosition = r.Scroll;
                }
                catch { }
            }
            _rooms.Clear();
        }
    }
}
