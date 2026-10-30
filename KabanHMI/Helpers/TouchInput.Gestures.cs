using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    // ======================================================================
    // WHAT A FINGER DOES (29 October, the plan's 3: "long press opens the
    // menu at once, rows get a '⋮' button, double-click actions get a
    // button, tapping a greyed entry shows its reason")
    // ----------------------------------------------------------------------
    // One message filter, on in touch mode only - it reads the mouse messages
    // Windows makes of a finger before the control under it does:
    //   - HELD STILL 0.6 s on something with a right-click menu (a table, a
    //     side bar button, a PLC Monitor tab, a motion value ...): the left
    //     press is let go where it does nothing, the right-click is given -
    //     the menu opens under the finger; lifting the finger then chooses
    //     nothing. A jog button, a slider, a splitter, a chart are not held
    //     for a menu: there the press is the work.
    //   - HELD STILL 0.6 s on something with a tooltip and no menu (30 October:
    //     "Is there any solution for the tooltips function for the touch mode"):
    //     its tooltip beside the finger, as the greyed tap's reason; the press
    //     let go where it does nothing - lifting the finger clicks nothing.
    //   - A TAP on something greyed: why it is greyed, beside the finger (a
    //     finger has no tooltip) - a greyed menu entry, a control the program
    //     holds back (IExplainsDisabled), a disabled one (its tooltip).
    //   - A TAP on a closed list (a DropDownList combo): its entries as a
    //     menu, a finger's height each, instead of Windows' small list.
    //   - A table with a row menu: a "⋮" at the end of its current row opens
    //     that menu.
    // A mouse's clicks pass as they are.
    // ======================================================================
    public static partial class TouchInput
    {
        /// <summary>How long a finger is held still for a menu, in milliseconds.</summary>
        public const int LongPressMs = 600;
        /// <summary>How far a finger may wander and still be a tap or a hold, at factor 1 (about 2 mm).</summary>
        public const int Slop = 10;
        /// <summary>The most entries a closed list shows as a finger's menu (more: Windows' own list).</summary>
        public const int TouchListMax = 40;

        /// <summary>A control that handles a finger itself: never held for a menu, never scrolled under (a drawing, a dragger).</summary>
        public interface IHandlesTouch { }

        internal const int WmMouseMove = 0x0200, WmLButtonDown = 0x0201, WmLButtonUp = 0x0202, WmLButtonDblClk = 0x0203,
                           WmRButtonDown = 0x0204, WmRButtonUp = 0x0205, MkLButton = 0x0001, MkRButton = 0x0002;

        private sealed class Press
        {
            public IntPtr Hwnd;
            public Control Window;       // the control the message went to (null: a window of Windows' own inside one)
            public Control Owner;        // the program's control it belongs to
            public Control Target;       // the deepest control under the finger (a disabled one too)
            public Point Client, Screen, Last;
            public bool Moved, Held, Panning;
            public bool Deferred;        // a list's press kept back: a tap is given it on the lift, a drag scrolls instead
            public int DownMsg;
            public ToolStripItem GreyItem;
            public bool ForTip;          // held for its tooltip (no menu here)
            public ToolStripItem TipItem;
            public ComboBox List;
            public object Scroller;      // what a drag scrolls (TouchInput.Scroll)
        }

        private static Press _press;
        private static Timer _hold;
        private static readonly ConditionalWeakTable<Control, object> _menus = new ConditionalWeakTable<Control, object>();

        /// <summary>What the last long press opened the menu of, the last greyed tap's reason, the last list shown (tests).</summary>
        public static Control LastHeld { get; private set; }
        /// <summary>The tooltip a long press showed last (tests).</summary>
        public static string LastTip { get; private set; }
        public static string LastReason { get; private set; }
        public static ContextMenuStrip LastList { get; private set; }

        /// <summary>A control whose right-click opens a menu it makes itself (no ContextMenuStrip): a long press opens it too.</summary>
        public static void HasMenu(Control c)
        {
            if (c == null) return;
            object seen;
            if (!_menus.TryGetValue(c, out seen)) _menus.Add(c, true);
        }

        private sealed class Filter : IMessageFilter
        {
            public bool PreFilterMessage(ref Message m) { return Handle(ref m); }
        }

        private static void InstallGestures()
        {
            Application.AddMessageFilter(new Filter());
            _hold = new Timer { Interval = LongPressMs };
            _hold.Tick += (s, e) => { _hold.Stop(); Held(); };
        }

        /// <summary>
        /// One mouse message, as a finger's: true when it is taken (the control does not get it).
        /// Public for tests - they hand it messages made as Windows makes them.
        /// </summary>
        public static bool Handle(ref Message m)
        {
            if (!On) { if (_press != null) { StopHold(); _press = null; } return false; }
            try
            {
                switch (m.Msg)
                {
                    case WmLButtonDown:
                    case WmLButtonDblClk: return Down(ref m);
                    case WmMouseMove: return Moved(ref m);
                    case WmLButtonUp: return Up(ref m);
                }
            }
            catch (Exception ex) { _press = null; StopHold(); AuditLogger.Log("touch: " + ex.Message); }
            return false;
        }

        // ---- the finger down, moved, up ----------------------------------------------------------

        private static bool Down(ref Message m)
        {
            StopHold();
            _press = null;
            var window = Control.FromHandle(m.HWnd);
            var owner = window ?? Control.FromChildHandle(m.HWnd);
            if (owner == null || IsOwnWindow(owner)) return false;
            var client = PointOf(m.LParam);
            var screen = ScreenOf(ref m, window ?? owner, client);
            var target = window != null ? Deepest(window, client) : owner;
            var p = new Press { Hwnd = m.HWnd, Window = window, Owner = owner, Target = target, Client = client, Screen = screen, Last = screen, DownMsg = m.Msg };
            _press = p;
            var strip = window as ToolStrip;
            if (strip != null)
            {
                var item = strip.GetItemAt(client);
                if (item != null && !item.Enabled && item.Available) p.GreyItem = item;
                else if (item != null && item.Available && !string.IsNullOrWhiteSpace(item.ToolTipText) && strip.ShowItemToolTips)
                {
                    p.TipItem = item;           // a tool bar's button: its tip when held
                    p.ForTip = true;
                    _hold.Start();
                }
                return false;
            }
            var combo = owner as ComboBox;
            if (combo != null && UsesTouchList(combo)) { p.List = combo; return true; }    // the list a finger's size, on the tap
            bool hold = CanHold(target, ToClient(target, screen));
            if (!hold && TipOf(target) != null) { p.ForTip = true; hold = true; }       // no menu: its tooltip
            if (hold) _hold.Start();
            // a table's or a list's row is chosen on the lift, not at the touch: a drag scrolls it
            // without choosing the row it started on (the press is given it as it was, on a tap)
            if (window != null && Defers(window, client)) { p.Deferred = true; return true; }
            return false;
        }

        private static bool Moved(ref Message m)
        {
            var p = _press;
            if (p == null || ((int)(long)m.WParam & MkLButton) == 0) return false;
            if (p.Held) return true;                                    // the menu is open: nothing under it moves
            var window = Control.FromHandle(m.HWnd) ?? Control.FromChildHandle(m.HWnd);
            var screen = ScreenOf(ref m, window, PointOf(m.LParam));
            if (p.Panning) { PanTo(p, screen); return true; }
            if (!p.Moved && Distance(screen, p.Screen) > UiScale.Px(Slop))
            {
                p.Moved = true;
                StopHold();
                if (StartPan(p, screen)) return true;
            }
            return p.List != null || p.Deferred;                        // a press kept back: its moves too
        }

        private static bool Up(ref Message m)
        {
            StopHold();
            var p = _press;
            _press = null;
            if (p == null) return false;
            if (p.Held || p.Panning) { EndPan(p); return true; }         // already let go where it does nothing
            if (p.List != null) { if (!p.Moved) ShowTouchList(p.List); return true; }
            if (p.Deferred)
            {
                if (p.Moved) return true;                               // a drag on a list that does not scroll: nothing
                // the tap, as the press and the lift it was (a second tap: a double click)
                Send(p.Hwnd, p.Window, p.DownMsg, MkLButton, LParam(p.Client));
                Send(p.Hwnd, p.Window, WmLButtonUp, 0, LParam(p.Client));
                Later(p.Window, () => Tapped(p));
                return true;
            }
            if (p.Moved) return false;                                  // a drag: the slider's, the splitter's
            var at = p.Window ?? p.Owner;
            Later(at, () => Tapped(p));
            return false;
        }

        private static void StopHold() { if (_hold != null) _hold.Stop(); }

        /// <summary>After the control has had the message (its click done): what a tap adds.</summary>
        private static void Later(Control c, Action a)
        {
            try
            {
                if (c != null && !c.IsDisposed && c.IsHandleCreated) { c.BeginInvoke(a); return; }
            }
            catch { }
            a();
        }

        // ---- a tap ----------------------------------------------------------------------------------

        private static void Tapped(Press p)
        {
            if (p.GreyItem != null) { ShowReason(p.Window, ReasonOf(p.GreyItem), p.Screen); return; }
            var t = p.Target;
            if (t == null || t.IsDisposed) return;
            if (!t.Enabled) { ShowReason(t, ReasonOf(t), p.Screen); return; }
            var explains = t as UiStyle.IExplainsDisabled;
            if (explains != null)
            {
                var why = explains.WhyDisabled;
                if (!string.IsNullOrWhiteSpace(why)) { ShowReason(t, why, p.Screen); return; }
            }
            TappedForTyping(p);
        }

        /// <summary>The keyboard's part of a tap (TouchKeyboard).</summary>
        static partial void TappedForTyping(Press p);

        private static string ReasonOf(ToolStripItem item)
        {
            return !string.IsNullOrWhiteSpace(item.ToolTipText) ? item.ToolTipText : LanguageManager.GetString("Tch_NotNow", "Not available now.");
        }

        private static string ReasonOf(Control c)
        {
            for (var x = c; x != null; x = x.Parent)
            {
                var explains = x as UiStyle.IExplainsDisabled;
                var why = explains != null ? explains.WhyDisabled : null;
                if (string.IsNullOrWhiteSpace(why)) why = UiStyle.TipOf(x);
                if (!string.IsNullOrWhiteSpace(why)) return why;
                if (!x.Enabled && x.Parent != null && x.Parent.Enabled) break;   // greyed by itself: its own words or none
            }
            return LanguageManager.GetString("Tch_NotNow", "Not available now.");
        }

        private static void ShowReason(Control near, string why, Point screen)
        {
            LastReason = why;
            Toast.ShowAt(near, why, screen);
        }

        // ---- held for a menu ------------------------------------------------------------------------

        /// <summary>Whether a finger held here opens a menu (a right-click would).</summary>
        private static bool CanHold(Control target, Point client)
        {
            if (target == null || !target.Enabled || Excluded(target)) return false;
            for (var c = target; c != null; c = c.Parent)
            {
                object seen;
                if (_menus.TryGetValue(c, out seen) || c.ContextMenuStrip != null) return true;
                var grid = c as DataGridView;
                if (grid != null && ReferenceEquals(c, target))
                {
                    var hit = grid.HitTest(client.X, client.Y);
                    if (hit.Type == DataGridViewHitTestType.ColumnHeader && hit.ColumnIndex >= 0 &&
                        grid.Columns[hit.ColumnIndex].HeaderCell.ContextMenuStrip != null) return true;
                }
                if (c is Form) break;
            }
            return false;
        }

        /// <summary>Where a finger's press is the work itself: no menu for holding it, no scrolling from it.</summary>
        internal static bool Excluded(Control c)
        {
            if (c is IHandlesTouch || c is TrackBar || c is ScrollBar || c is Splitter || c is SplitContainer || c is PercentSlider) return true;
            var b = c as ButtonHMI;
            if (b != null && b.HasActions) return true;                 // a jog, a set: held is held
            string n = c.GetType().Name;
            return n == "DashboardTimeline" || n == "DashboardChart" || n == "DxfViewerControl" || n.EndsWith("Chart");
        }

        private static void Held()
        {
            var p = _press;
            if (p == null || p.Moved || p.Panning || p.Held) return;
            p.Held = true;
            if (p.ForTip)
            {
                string tip = p.TipItem != null ? p.TipItem.ToolTipText : TipOf(p.Target);
                // the press let go where it does nothing: lifting the finger clicks nothing
                if (!p.Deferred) Send(p.Hwnd, p.Window ?? p.Owner, WmLButtonUp, 0, Far);
                if (string.IsNullOrWhiteSpace(tip)) return;
                LastTip = tip;
                Toast.ShowAt(p.Window ?? p.Owner ?? p.Target, tip, p.Screen);
                return;
            }
            LastHeld = p.Target;
            RightClick(p.Hwnd, p.Window ?? p.Owner, p.Client, !p.Deferred);
        }

        /// <summary>
        /// The tooltip a finger held on a control shows: the control's own, or the part it is in
        /// (a unit's frame, a row) - a mouse resting there would show it. Null: none (nothing to hold for),
        /// a greyed control (a tap tells why), or one whose press is the work (a jog, a slider).
        /// </summary>
        internal static string TipOf(Control target)
        {
            if (target == null || !target.Enabled || Excluded(target)) return null;
            int depth = 0;
            for (var c = target; c != null && depth < 4; c = c.Parent, depth++)
            {
                if (c is Form) break;
                string tip;
                try { tip = UiStyle.TipOf(c); } catch { tip = null; }
                if (!string.IsNullOrWhiteSpace(tip)) return tip;
            }
            return null;
        }

        /// <summary>
        /// A right-click at a point of a window: the left press there (if any) let go far outside it -
        /// nothing clicked - then the right button down and up; Windows' own answer to that (the
        /// context menu message) opens the control's menu, its handlers open theirs.
        /// </summary>
        internal static void RightClick(IntPtr hwnd, Control c, Point client, bool leftHeld)
        {
            if (leftHeld) Send(hwnd, c, WmLButtonUp, 0, Far);
            Send(hwnd, c, WmRButtonDown, MkRButton, LParam(client));
            Send(hwnd, c, WmRButtonUp, 0, LParam(client));
        }

        // ---- a closed list, a finger's size ---------------------------------------------------------

        private static bool UsesTouchList(ComboBox combo)
        {
            return combo.Enabled && combo.DropDownStyle == ComboBoxStyle.DropDownList && combo.Items.Count > 0 &&
                   combo.Items.Count <= TouchListMax && !(combo is IHandlesTouch);
        }

        private static readonly MethodInfo OnDropDown = typeof(ComboBox).GetMethod("OnDropDown", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo OnDropDownClosed = typeof(ComboBox).GetMethod("OnDropDownClosed", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo OnCommitted = typeof(ComboBox).GetMethod("OnSelectionChangeCommitted", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>A list's entries as a menu under it, the one chosen ticked; choosing one is choosing it in the list.</summary>
        public static ContextMenuStrip ShowTouchList(ComboBox combo)
        {
            if (combo == null || combo.IsDisposed) return null;
            try { combo.Focus(); } catch { }
            // what the list does as it opens (fills itself, shows a preview beside it)
            try { if (OnDropDown != null) OnDropDown.Invoke(combo, new object[] { EventArgs.Empty }); } catch { }
            var menu = UiStyle.Menu();
            menu.Name = "touchList";
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.MinimumSize = new Size(combo.Width, 0);
            for (int i = 0; i < combo.Items.Count; i++)
            {
                int index = i;
                var item = new ToolStripMenuItem(combo.GetItemText(combo.Items[i])) { Checked = i == combo.SelectedIndex, Name = "touchList" + i };
                item.Click += (s, e) =>
                {
                    if (combo.IsDisposed) return;
                    combo.SelectedIndex = index;
                    try { if (OnCommitted != null) OnCommitted.Invoke(combo, new object[] { EventArgs.Empty }); } catch { }
                };
                menu.Items.Add(item);
            }
            menu.Closed += (s, e) => { try { if (!combo.IsDisposed && OnDropDownClosed != null) OnDropDownClosed.Invoke(combo, new object[] { EventArgs.Empty }); } catch { } };
            LastList = menu;
            UiStyle.ShowPopup(menu, combo, new Point(0, combo.Height));
            return menu;
        }

        // ---- the "⋮" of a table's row menu ----------------------------------------------------------

        private static readonly ConditionalWeakTable<DataGridView, RowMenuButton> _rowButtons = new ConditionalWeakTable<DataGridView, RowMenuButton>();

        private static void PrepareGestures(Control c)
        {
            var grid = c as DataGridView;
            if (grid == null) return;
            RowMenuButton b;
            if (_rowButtons.TryGetValue(grid, out b)) return;
            object seen;
            if (grid.ContextMenuStrip == null && !_menus.TryGetValue(grid, out seen))
            {
                // the menu may come later than the table
                EventHandler later = null;
                later = (s, e) => { if (grid.ContextMenuStrip != null) { grid.ContextMenuStripChanged -= later; PrepareGestures(grid); } };
                grid.ContextMenuStripChanged += later;
                return;
            }
            b = new RowMenuButton(grid);
            _rowButtons.Add(grid, b);
        }

        /// <summary>The "⋮" on a table's current row (tests): null when the table has no row menu.</summary>
        public static Control RowMenuOf(DataGridView grid)
        {
            RowMenuButton b;
            return grid != null && _rowButtons.TryGetValue(grid, out b) ? b : null;
        }

        private static void ApplyGesturesToAll()
        {
            foreach (var c in AllControls())
            {
                var grid = c as DataGridView;
                RowMenuButton b;
                if (grid != null && _rowButtons.TryGetValue(grid, out b)) b.Place();
            }
        }

        /// <summary>"⋮" at the end of the current row, in touch mode: its menu, as a right-click on the row.</summary>
        private sealed class RowMenuButton : Control
        {
            private readonly DataGridView _grid;
            private bool _over;

            public RowMenuButton(DataGridView grid)
            {
                _grid = grid;
                Name = "rowMenu";
                Visible = false;
                Cursor = Cursors.Hand;
                AccessibleName = "Row menu";
                SetStyle(ControlStyles.Selectable, false);
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                grid.Controls.Add(this);
                EventHandler place = (s, e) => Place();
                grid.CurrentCellChanged += place;
                grid.SizeChanged += place;
                grid.Scroll += (s, e) => Place();
                grid.ColumnWidthChanged += (s, e) => Place();
                grid.RowsRemoved += (s, e) => Place();
                grid.RowsAdded += (s, e) => Place();
                grid.RowHeightChanged += (s, e) => Place();
                grid.VisibleChanged += place;
                MouseEnter += (s, e) => { _over = true; Invalidate(); };
                MouseLeave += (s, e) => { _over = false; Invalidate(); };
                Place();
            }

            public void Place()
            {
                if (IsDisposed || _grid.IsDisposed) return;
                var cell = _grid.CurrentCell;
                Rectangle row = Rectangle.Empty;
                if (On && cell != null && cell.RowIndex >= 0) row = _grid.GetRowDisplayRectangle(cell.RowIndex, true);
                if (row.Height < UiScale.Px(12) || row.Width < UiScale.Px(80)) { if (Visible) Visible = false; return; }
                int w = UiScale.Px(34);
                var r = new Rectangle(row.Right - w - UiScale.Px(2), row.Top + UiScale.Px(2), w, row.Height - UiScale.Px(4));
                if (Bounds != r) Bounds = r;
                if (!Visible) Visible = true;
                BringToFront();
            }

            protected override void OnClick(EventArgs e)
            {
                base.OnClick(e);
                var cell = _grid.CurrentCell;
                if (cell == null) return;
                // on the row, in its last cell on screen before the "⋮": its handlers see a right-click on that row
                var at = new Point(Math.Max(0, Left - UiScale.Px(4)), Top + Height / 2);
                Rectangle last = Rectangle.Empty;
                foreach (DataGridViewColumn col in _grid.Columns)
                {
                    if (!col.Visible) continue;
                    var r = _grid.GetCellDisplayRectangle(col.Index, cell.RowIndex, true);
                    if (r.Width > 0 && r.Left < Left && (last.IsEmpty || r.Left > last.Left)) last = r;
                }
                if (!last.IsEmpty) at = new Point(Math.Min(at.X, last.Right - UiScale.Px(4)), last.Top + last.Height / 2);
                LastHeld = _grid;
                RightClick(_grid.Handle, _grid, at, false);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var back = new SolidBrush(_over ? Color.FromArgb(226, 236, 250) : Color.FromArgb(245, 246, 248)))
                    g.FillRectangle(back, ClientRectangle);
                using (var pen = new Pen(_over ? UiStyle.HoverBorderColor : Color.FromArgb(196, 201, 209)))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                float d = Math.Max(3f, UiScale.Px(4)), cx = Width / 2f, gap = d * 1.8f, cy = Height / 2f;
                using (var dot = new SolidBrush(Color.FromArgb(60, 66, 76)))
                    for (int i = -1; i <= 1; i++) g.FillEllipse(dot, cx - d / 2, cy + i * gap - d / 2, d, d);
            }
        }

        // ---- the messages -----------------------------------------------------------------------------

        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint p);
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }

        private static readonly MethodInfo WndProcOf = typeof(Control).GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>A point far outside any window: a button let go there clicks nothing.</summary>
        private static readonly IntPtr Far = LParam(new Point(-30000, -30000));

        internal static IntPtr LParam(Point p) { return (IntPtr)(int)(((p.Y & 0xFFFF) << 16) | (p.X & 0xFFFF)); }

        internal static Point PointOf(IntPtr lParam)
        {
            int v = (int)(long)lParam;
            return new Point((short)(v & 0xFFFF), (short)((v >> 16) & 0xFFFF));
        }

        /// <summary>A message to a window, at once (Windows: SendMessage - elsewhere the control's own handling).</summary>
        internal static void Send(IntPtr hwnd, Control c, int msg, int wParam, IntPtr lParam)
        {
            if (IsWindows) { SendMessage(hwnd, msg, (IntPtr)wParam, lParam); return; }
            var target = Control.FromHandle(hwnd) ?? c;
            if (target == null || target.IsDisposed || WndProcOf == null) return;
            var m = Message.Create(target.Handle, msg, (IntPtr)wParam, lParam);
            WndProcOf.Invoke(target, new object[] { m });
        }

        [DllImport("user32.dll")] private static extern int GetMessagePos();

        /// <summary>
        /// Where on the screen a mouse message of the queue happened. Windows: the screen point it was
        /// made at (GetMessagePos) - not its window's point turned into the screen's, which moves when
        /// a drag scrolls the window under the finger (30 October: "the finger page drag makes the
        /// page blinking").
        /// </summary>
        private static Point ScreenOf(ref Message m, Control c, Point client)
        {
            if (IsWindows)
            {
                try
                {
                    int v = GetMessagePos();
                    return new Point((short)(v & 0xFFFF), (short)((v >> 16) & 0xFFFF));
                }
                catch { }
            }
            return ToScreen(m.HWnd, c, client);
        }

        private static Point ToScreen(IntPtr hwnd, Control c, Point client)
        {
            if (IsWindows)
            {
                var p = new NativePoint { X = client.X, Y = client.Y };
                try { if (ClientToScreen(hwnd, ref p)) return new Point(p.X, p.Y); } catch { }
            }
            return c != null && !c.IsDisposed ? c.PointToScreen(client) : client;
        }

        private static Point ToClient(Control c, Point screen)
        {
            return c != null && !c.IsDisposed ? c.PointToClient(screen) : screen;
        }

        private static int Distance(Point a, Point b) { return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)); }

        /// <summary>The deepest visible control at a point (a disabled one too: Windows gives its clicks to the one it is in).</summary>
        private static Control Deepest(Control c, Point client)
        {
            var at = client;
            for (int depth = 0; depth < 32; depth++)
            {
                Control child;
                try { child = c.GetChildAtPoint(at, GetChildAtPointSkip.Invisible); } catch { child = null; }
                if (child == null || ReferenceEquals(child, c)) break;
                at = new Point(at.X - child.Left, at.Y - child.Top);
                c = child;
            }
            return c;
        }

        /// <summary>The program's own touch windows (the keyboard, a note): a finger there is theirs.</summary>
        private static bool IsOwnWindow(Control c)
        {
            var f = c.FindForm() ?? c.TopLevelControl as Form;
            return f is Toast || (f != null && f.Name == TouchKeyboardName);
        }

        internal const string TouchKeyboardName = "touchKeyboard";

        // ---- the parts of a drag (TouchInput.Scroll) ---------------------------------------------------

        static partial void StartPanPart(Press p, Point screen, ref bool started);
        static partial void PanToPart(Press p, Point screen);
        static partial void DefersPart(Control window, Point client, ref bool defers);

        private static bool StartPan(Press p, Point screen) { bool started = false; StartPanPart(p, screen, ref started); return started; }
        private static void PanTo(Press p, Point screen) { PanToPart(p, screen); p.Last = screen; }
        private static void EndPan(Press p) { }
        private static bool Defers(Control window, Point client) { bool d = false; DefersPart(window, client, ref d); return d; }
    }
}
