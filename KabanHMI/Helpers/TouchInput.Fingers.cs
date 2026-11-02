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
            if (m.Msg != WmPointerEnter && m.Msg != WmPointerDown) return false;
            var b = Control.FromHandle(m.HWnd) as ButtonHMI;
            if (b == null || !b.TakesFinger || !FingerPointer(id)) return false;
            _fingers[id] = b;
            if (m.Msg == WmPointerDown) b.FingerDown();
            return true;
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
