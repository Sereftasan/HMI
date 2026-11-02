using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    // ======================================================================
    // A HOLD-TO-RUN BUTTON BY A FINGER (2 November, on a real touch screen: "the momentary action
    // buttons ... not working well with the touch mode" - "It rarely reacts, mostly no action")
    // ----------------------------------------------------------------------
    // Windows makes a finger's mouse press only once it knows the finger is not being held for a
    // right-click: the press came when the finger was lifted, together with its release - a jog
    // or a cylinder's button held by a finger was on for no time at all, and the program's own
    // long press (a tooltip, a menu) never began. Two things now:
    //   - every window of the program has ALL of Windows' gestures off in touch mode (its
    //     press-and-hold with them - Raymond Chen, "How do I disable the press-and-hold gesture
    //     for my window?": SetGestureConfig, all gestures blocked), so a finger's press comes
    //     at the touch;
    //   - a hold-to-run button (ButtonHMI with a Momentary action) takes the finger's own pointer
    //     messages (WM_POINTERDOWN ... UP), which Windows sends at the touch whatever it decides
    //     about the mouse: its bits on at the touch, off at the lift - Windows makes no mouse
    //     press of that finger then (a pointer message the program takes is not turned into one).
    // A mouse - and a finger on anything else - goes the way it went.
    //
    // A FINGER HELD STILL (2 November, the real touch screen again: the log had a finger's press given 304 ms late, so
    // Windows still held some presses back): the long press for a menu is timed from the finger's own WM_POINTERDOWN too.
    // When Windows has given the press by then (TouchInput.Gestures times it from that), it goes as it went; when it still
    // holds it back, the finger's own timer opens the menu, and the finger's lift is kept from Windows - it makes no click,
    // no right-click of it. No tooltip is shown for a press (2 November: "we should never press the control for the
    // tooltip") - a tap on something greyed says why; the tooltips are read on the Notes and feedback page.
    // ======================================================================
    public static partial class TouchInput
    {
        internal const int WmPointerUpdate = 0x0245, WmPointerDown = 0x0246, WmPointerUp = 0x0247,
                           WmPointerEnter = 0x0249, WmPointerLeave = 0x024A, WmPointerCaptureChanged = 0x024C;
        private const int PtTouch = 2, PtPen = 3;

        [DllImport("user32.dll")] private static extern bool GetPointerType(uint pointerId, out int pointerType);

        private static bool _noPointers;

        /// <summary>
        /// The pointer is a finger (or a pen) - not the mouse. Tests answer it themselves (the bench has no
        /// pointers).
        /// </summary>
        public static Func<int, bool> FingerPointer = id =>
        {
            if (!IsWindows || _noPointers) return false;
            try
            {
                int type;
                return GetPointerType((uint)id, out type) && (type == PtTouch || type == PtPen);
            }
            catch (EntryPointNotFoundException) { _noPointers = true; return false; }   // before Windows 8
            catch (DllNotFoundException) { _noPointers = true; return false; }
        };

        // the fingers on a hold-to-run button now, by their pointer id
        private static readonly Dictionary<int, ButtonHMI> _fingers = new Dictionary<int, ButtonHMI>();
        // a finger lifted from one: its last message (it leaves) is the button's too
        private static readonly HashSet<int> _lifted = new HashSet<int>();

        /// <summary>The hold-to-run buttons a finger holds now (tests).</summary>
        public static int FingersHolding { get { return _fingers.Count; } }

        /// <summary>A finger's pointer message: true when a hold-to-run button took it (the program goes no further with it).</summary>
        private static bool Pointer(ref Message m)
        {
            int id = (int)((long)m.WParam & 0xFFFF);
            ButtonHMI held;
            if (_fingers.TryGetValue(id, out held))
            {
                // a new contact with an id seen before (its last message lost): not that button's
                bool other = m.Msg == WmPointerEnter || (m.Msg == WmPointerDown && !ReferenceEquals(Control.FromHandle(m.HWnd), held));
                if (!other)
                {
                    switch (m.Msg)
                    {
                        case WmPointerDown:
                            held.FingerDown();
                            break;
                        case WmPointerUp:
                            _fingers.Remove(id);
                            _lifted.Add(id);
                            held.FingerUp(Inside(held, m.LParam));
                            break;
                        case WmPointerCaptureChanged:
                        case WmPointerLeave:
                            _fingers.Remove(id);
                            if (m.Msg == WmPointerCaptureChanged) _lifted.Add(id);
                            held.FingerUp(false);
                            break;
                    }
                    return true;
                }
                _fingers.Remove(id);
                held.FingerUp(false);
            }
            if (_lifted.Count > 0 && _lifted.Contains(id))
            {
                if (m.Msg == WmPointerLeave) { _lifted.Remove(id); return true; }
                if (m.Msg == WmPointerEnter || m.Msg == WmPointerDown) _lifted.Remove(id);   // a new contact
                else return true;
            }
            if (m.Msg != WmPointerEnter && m.Msg != WmPointerDown) return Touching(ref m, id);
            var b = Control.FromHandle(m.HWnd) as ButtonHMI;
            if (b == null || !b.TakesFinger || !FingerPointer(id)) return Touching(ref m, id);
            StopTouchHold();
            _touch = null;
            _fingers[id] = b;
            if (m.Msg == WmPointerDown) b.FingerDown();
            return true;
        }

        // ---- two fingers spread or pinched: a zoom ------------------------------------------------
        //
        // (2 November: "zoom in / out function does not work on Dashboard / Machine Performance / Timeline chart") Windows'
        // own zoom gesture is off in touch mode (every gesture is, for the press to come at the touch); a control that zooms
        // (IPinchZoom) gets two fingers on it from their own pointer messages: how much nearer or further apart they are since
        // the last move, and the point between them.

        /// <summary>A control two fingers zoom: spread apart in (a factor below 1), pinched together out.</summary>
        public interface IPinchZoom
        {
            /// <summary>The fingers moved: <paramref name="factor"/> = how far apart they were / are now; the point between them (client).</summary>
            void PinchZoom(Point center, double factor);
            /// <summary>One of them lifted.</summary>
            void PinchEnd();
        }

        private static readonly Dictionary<int, Point> _contacts = new Dictionary<int, Point>();
        private static Control _pinch;
        private static int _pinchA, _pinchB;
        private static double _pinchDistance;

        /// <summary>Zoomed by two fingers since the program started (tests).</summary>
        public static int Pinches { get; private set; }

        private static double Apart(Point a, Point b) { return Math.Sqrt((double)(a.X - b.X) * (a.X - b.X) + (double)(a.Y - b.Y) * (a.Y - b.Y)); }

        /// <summary>The control under a screen point that two fingers zoom (the point's own or one it is in), or null.</summary>
        private static Control PinchTarget(IntPtr hwnd, Point screen)
        {
            var window = Control.FromHandle(hwnd) ?? Control.FromChildHandle(hwnd);
            if (window == null || window.IsDisposed) return null;
            Control c;
            try { c = Deepest(window, window.PointToClient(screen)); } catch { c = window; }
            for (; c != null; c = c.Parent) if (c is IPinchZoom) return c;
            return null;
        }

        /// <summary>A finger's pointer message for a two-finger zoom (true: taken - the second finger's moves are the zoom's).</summary>
        private static bool Pinching(ref Message m, int id)
        {
            var at = PointOf(m.LParam);
            switch (m.Msg)
            {
                case WmPointerDown:
                    if (!FingerPointer(id)) return false;
                    // the first finger on the glass (POINTER_MESSAGE_FLAG_PRIMARY): no other is there - one whose lift was lost forgotten
                    if (((((long)m.WParam) >> 16) & 0x2000) != 0) { _contacts.Clear(); _pinch = null; }
                    _contacts[id] = at;
                    if (_pinch == null && _contacts.Count == 2)
                    {
                        int other = -1;
                        foreach (var k in _contacts.Keys) if (k != id) other = k;
                        var target = PinchTarget(m.HWnd, at);
                        if (target != null && ReferenceEquals(target, PinchTarget(m.HWnd, _contacts[other])))
                        {
                            _pinch = target;
                            _pinchA = other;
                            _pinchB = id;
                            _pinchDistance = Math.Max(1, Apart(_contacts[other], at));
                            StopTouchHold();
                            _touch = null;              // two fingers: no long press
                        }
                    }
                    return false;
                case WmPointerUpdate:
                    if (!_contacts.ContainsKey(id)) return false;
                    _contacts[id] = at;
                    if (_pinch == null || (id != _pinchA && id != _pinchB) || !_contacts.ContainsKey(_pinchA) || !_contacts.ContainsKey(_pinchB)) return false;
                    double d = Math.Max(1, Apart(_contacts[_pinchA], _contacts[_pinchB]));
                    if (Math.Abs(d - _pinchDistance) >= UiScale.Px(3) && !_pinch.IsDisposed)
                    {
                        var a = _contacts[_pinchA]; var b = _contacts[_pinchB];
                        var center = _pinch.PointToClient(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2));
                        try { ((IPinchZoom)_pinch).PinchZoom(center, _pinchDistance / d); Pinches++; }
                        catch (Exception ex) { AuditLogger.Log("touch: a zoom by two fingers failed (" + ex.Message + ")."); }
                        _pinchDistance = d;
                    }
                    return id == _pinchB;                // the second finger's moves: the zoom's only
                case WmPointerUp:
                case WmPointerCaptureChanged:
                    _contacts.Remove(id);
                    if (_pinch != null && (id == _pinchA || id == _pinchB))
                    {
                        var was = _pinch;
                        _pinch = null;
                        try { if (!was.IsDisposed) ((IPinchZoom)was).PinchEnd(); } catch { }
                    }
                    return false;
            }
            return false;
        }

        // ---- a finger held still, timed from its own messages -------------------------------------

        private sealed class Touch
        {
            public int Id;
            public IntPtr Hwnd;
            public Point Screen;
            public DateTime At;
            public bool Moved;
            public bool MouseSeen;   // Windows gave its press: the long press is TouchInput.Gestures' (timed from that)
            public bool Done;        // its tooltip shown or its menu opened here: its lift kept from Windows
        }

        private static Touch _touch;
        private static Timer _touchHold;

        /// <summary>What the last long press timed from the finger's own messages did ("tip", "menu"), and how many there were (tests).</summary>
        public static string LastFingerHold { get; private set; }
        public static int FingerHolds { get; private set; }

        /// <summary>A finger's pointer message on anything but a hold-to-run button: true when it is kept from Windows.</summary>
        private static bool Touching(ref Message m, int id)
        {
            if (Pinching(ref m, id)) return true;
            var t = _touch;
            switch (m.Msg)
            {
                case WmPointerDown:
                    StopTouchHold();
                    _touch = null;
                    if (!FingerPointer(id)) return false;
                    _touch = new Touch { Id = id, Hwnd = m.HWnd, Screen = PointOf(m.LParam), At = DateTime.Now };
                    if (_touchHold == null)
                    {
                        _touchHold = new Timer { Interval = LongPressMs };
                        _touchHold.Tick += (s, e) => { StopTouchHold(); TouchHeld(); };
                    }
                    _touchHold.Start();
                    return false;
                case WmPointerUpdate:
                    if (t != null && t.Id == id && !t.Moved && !t.Done && Distance(PointOf(m.LParam), t.Screen) > UiScale.Px(Slop))
                    {
                        t.Moved = true;
                        StopTouchHold();
                    }
                    return false;
                case WmPointerUp:
                case WmPointerCaptureChanged:
                    if (t == null || t.Id != id) return false;
                    StopTouchHold();
                    _touch = null;
                    return t.Done && m.Msg == WmPointerUp;      // shown already: Windows makes no click of the lift
            }
            return false;
        }

        private static void StopTouchHold() { if (_touchHold != null) _touchHold.Stop(); }

        /// <summary>
        /// A mouse press while a finger is on the glass: Windows gave the finger's press - its long press is timed from it
        /// (true: the finger's long press was done here already - the press it gave late is not the control's).
        /// </summary>
        private static bool TouchPressGiven()
        {
            var t = _touch;
            if (t == null || (DateTime.Now - t.At).TotalSeconds > 30) return false;
            if (t.Done) return true;
            t.MouseSeen = true;
            StopTouchHold();
            return false;
        }

        /// <summary>The finger held still LongPressMs with no press from Windows: its tooltip, or its menu, from here.</summary>
        private static void TouchHeld()
        {
            var t = _touch;
            if (t == null || t.Moved || t.MouseSeen || t.Done) return;
            var window = Control.FromHandle(t.Hwnd);
            var owner = window ?? Control.FromChildHandle(t.Hwnd);
            if (owner == null || owner.IsDisposed || IsOwnWindow(owner)) return;
            var at = window ?? owner;
            var client = at.PointToClient(t.Screen);
            if (window is ToolStrip) return;                       // a tool bar: no menu to hold for (and no tooltip by a press)
            var target = window != null ? Deepest(window, client) : owner;
            // a menu only - never a tooltip: a press is a press (the lift goes as Windows makes it)
            if (!CanHold(target, ToClient(target, t.Screen))) return;
            t.Done = true;
            LastHeld = target;
            LastFingerHold = "menu";
            FingerHolds++;
            RightClick(t.Hwnd, at, client, false);
        }

        /// <summary>A pointer message's point (screen) on the button.</summary>
        private static bool Inside(Control c, IntPtr lParam)
        {
            try
            {
                if (c.IsDisposed || !c.IsHandleCreated) return false;
                int v = (int)(long)lParam;
                var screen = new Point((short)(v & 0xFFFF), (short)((v >> 16) & 0xFFFF));
                return c.RectangleToScreen(c.ClientRectangle).Contains(screen);
            }
            catch { return false; }
        }

        // ---- how late Windows gives a finger's press (a note for the log, the first few times) -----------

        [DllImport("user32.dll")] private static extern IntPtr GetMessageExtraInfo();
        private const long FromTouch = 0xFF515700, TouchSignatureMask = 0xFFFFFF00;
        private static DateTime _pointerDown = DateTime.MinValue;
        private static int _lateNotes;

        /// <summary>A finger touched (WM_POINTERDOWN): the time, for the note of a late press.</summary>
        private static void PointerTouched(ref Message m)
        {
            if (m.Msg == WmPointerDown) _pointerDown = DateTime.Now;
        }

        /// <summary>A press made of a finger, much later than the touch: said in the log (5 times a run at most).</summary>
        private static void NoteLatePress()
        {
            if (!IsWindows || _lateNotes >= 5 || _pointerDown == DateTime.MinValue) return;
            try
            {
                long extra = (long)GetMessageExtraInfo();
                if ((extra & TouchSignatureMask) != FromTouch) return;
                double ms = (DateTime.Now - _pointerDown).TotalMilliseconds;
                _pointerDown = DateTime.MinValue;
                if (ms < 250 || ms > 10000) return;
                _lateNotes++;
                AuditLogger.Log("touch: Windows gave a finger's press " + (int)ms + " ms after the touch.");
            }
            catch { _lateNotes = 5; }
        }
    }
}
