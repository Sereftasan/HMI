using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// THE SCREEN USED BY A FINGER (29 October: "add an emoji dropdown button on top of the screen
    /// to switch in between mouse and touch screen mode and add it to the view preferences, just like
    /// the screen scale, and do all of these upgrades for a good touch screen experience. The machines
    /// usually have 17 or 19 inches of screen with 1920x1080 resolution").
    ///
    /// A view preference (View Preferences > Screen > Screen used with: Mouse / Touch), the panel's
    /// and a user's own as the language is; the 🖱 / 👆 button on the top bar switches it. In touch
    /// mode:
    ///   - Windows does not turn a held finger into a right-click: a jog button held is held (its
    ///     LBUTTONDOWN comes at the touch, not after the press-and-hold wait), and its own pan
    ///     gesture is off - a finger drag reaches the program as a mouse drag (see the parts below);
    ///   - menus, table rows, tick boxes, tabs and toolbar buttons are a finger's size (TouchSizes);
    ///   - a long press opens the menu a right-click opens, a table with a row menu has a "⋮" on
    ///     its row, a tap on something greyed says why (TouchGestures);
    ///   - a finger drag scrolls a table, a list or a page, a tap still selects (TouchGestures);
    ///   - a tap on a text box brings a keyboard on the screen, on a number cell the keypad
    ///     (TouchKeyboard).
    /// A mouse works as before in both modes.
    /// </summary>
    public static partial class TouchInput
    {
        /// <summary>🖱 the mouse, 👆 a finger, ⌨ the keyboard on the screen.</summary>
        public const string MouseEmoji = "\U0001F5B1";
        public const string TouchEmoji = "\U0001F446";
        public const string KeyboardEmoji = "⌨";

        /// <summary>A menu entry's height for a finger, at factor 1 (8 - 9 mm on the machines' screens).</summary>
        public const int MenuEntryHeight = 42;

        /// <summary>The screen is used by touch now.</summary>
        public static bool On { get { return ViewPreferences.TouchMode; } }

        /// <summary>Raised (on the UI thread) when the screen goes from mouse to touch or back.</summary>
        public static event EventHandler ModeChanged;

        private static bool _installed, _was;

        /// <summary>Once, at the start (MainForm): the windows prepared as they open, the mode followed.</summary>
        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            _was = On;
            UiStyle.HoverTipsOff = _was;
            ViewPreferences.Changed += (s, e) =>
            {
                bool now = On;
                if (now == _was) return;
                _was = now;
                RaiseModeChanged();
            };
            Application.Idle += (s, e) => Sweep();
            Sweep();
            InstallGestures();
        }

        /// <summary>Tells everyone the mode changed (tests call it after setting the preference).</summary>
        public static void RaiseModeChanged()
        {
            _was = On;
            UiStyle.HoverTipsOff = _was;
            ApplyNativeToAll();
            ApplySizesToAll();
            ApplyGesturesToAll();
            var h = ModeChanged;
            if (h == null) return;
            foreach (EventHandler each in h.GetInvocationList())
            {
                try { each(null, EventArgs.Empty); }
                catch (Exception ex) { AuditLogger.Log("the touch mode could not be applied: " + ex.Message); }
            }
        }

        /// <summary>
        /// The top bar's choice: kept as the signed-in user's own when they keep one (else
        /// choosing would change nothing on their screen), otherwise as the panel's - everyone's.
        /// </summary>
        public static void Choose(bool touch)
        {
            string value = touch ? "Touch" : "Mouse";
            var user = ViewPreferences.ActiveUser;
            bool own = user != null && ViewPreferences.UserValue(user, ViewPreferences.InputMode) != null;
            if (own) ViewPreferences.SetUser(user, ViewPreferences.InputMode, value);
            else ViewPreferences.SetPanel(ViewPreferences.InputMode, value);
            AuditLogger.Log("the screen used with " + (touch ? "touch" : "a mouse") + (own ? " (" + user + "'s own)" : " (the panel's)") + ".");
        }

        /// <summary>Whether the choice on the top bar is the signed-in user's own (else the panel's).</summary>
        public static bool ChoiceIsOwn
        {
            get
            {
                var user = ViewPreferences.ActiveUser;
                return user != null && ViewPreferences.UserValue(user, ViewPreferences.InputMode) != null;
            }
        }

        // ---- every window, as it opens --------------------------------------------------------

        private static readonly HashSet<Control> _watched = new HashSet<Control>(new SameControl());
        private static readonly HashSet<Form> _swept = new HashSet<Form>();

        /// <summary>Each open window not seen yet: its controls followed (public for tests: Mono's DoEvents raises no Idle).</summary>
        public static void Sweep()
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f == null || f.IsDisposed || !_swept.Add(f)) continue;
                var form = f;
                form.Disposed += (s, e) => _swept.Remove(form);
                Watch(form);
            }
        }

        /// <summary>A control and what comes under it: prepared now and when its window is made.</summary>
        public static void Watch(Control c)
        {
            if (c == null || c.IsDisposed || !_watched.Add(c)) return;
            var ctl = c;
            ctl.Disposed += (s, e) => _watched.Remove(ctl);
            ctl.HandleCreated += (s, e) => { ApplyNative(ctl); Prepared(ctl); };
            ctl.ControlAdded += (s, e) => Watch(e.Control);
            if (ctl.IsHandleCreated) ApplyNative(ctl);
            Prepared(ctl);
            foreach (Control child in ctl.Controls) Watch(child);
        }

        /// <summary>A control met: the other parts' preparation (its size for a finger, a table's "⋮").</summary>
        private static void Prepared(Control c)
        {
            PrepareSizes(c);
            PrepareGestures(c);
        }

        /// <summary>Every control of every open window (a mode change reaches them all).</summary>
        public static IEnumerable<Control> AllControls()
        {
            var forms = new List<Form>();
            foreach (Form f in Application.OpenForms) if (f != null && !f.IsDisposed) forms.Add(f);
            var stack = new Stack<Control>(forms);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                yield return c;
                foreach (Control child in c.Controls) stack.Push(child);
            }
        }

        private sealed class SameControl : IEqualityComparer<Control>
        {
            public bool Equals(Control a, Control b) { return ReferenceEquals(a, b); }
            public int GetHashCode(Control c) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(c); }
        }

        // ---- Windows' own handling of a finger ----------------------------------------------------
        //
        // PRESS-AND-HOLD: Windows holds back a touch's LBUTTONDOWN until it knows the finger is not
        // being held for a right-click - a jog button pressed with a finger moved only once the finger
        // left the glass, or not at all (a hold became a right-click). The Tablet PC property
        // "MicrosoftTabletPenServiceProperty" on each window turns that off (and the pen's flicks and
        // its tap circles); the program's own long press opens the menus instead (TouchGestures).
        //
        // PAN: Windows turns a finger drag into its pan gesture - scroll messages to a window with
        // scroll bars, nothing for the program's tables. Blocked (SetGestureConfig), the drag comes
        // as a mouse drag, and the program scrolls what is under the finger itself.
        //
        // 2 November, on a real touch screen ("the momentary action buttons ... rarely reacts, mostly no
        // action"; the long press showed no tooltip): the property alone did not end the wait - since
        // Windows 7 a window's press-and-hold goes with its gestures, so ALL of them are blocked
        // (SetGestureConfig: id 0, every gesture - Raymond Chen, "How do I disable the press-and-hold
        // gesture for my window?"), and a hold-to-run button takes the finger itself (TouchInput.Fingers).

        internal static readonly bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        private const string TabletProperty = "MicrosoftTabletPenServiceProperty";
        private const int TabletDisablePressAndHold = 0x00000001, TabletDisablePenTapFeedback = 0x00000008,
                          TabletDisablePenBarrelFeedback = 0x00000010, TabletDisableFlicks = 0x00010000;
        private const int GidAll = 0, GcAllGestures = 0x01, GidPan = 4, GcPan = 0x01, GcPanSingleFingerVertically = 0x02, GcPanSingleFingerHorizontally = 0x04,
                          GcPanWithGutter = 0x08, GcPanWithInertia = 0x10;
        private const int FeedbackTouchPressAndHold = 9, FeedbackTouchRightTap = 10;

        [StructLayout(LayoutKind.Sequential)]
        private struct GestureConfig { public int Id; public int Want; public int Block; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetProp(IntPtr hWnd, string name, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr RemoveProp(IntPtr hWnd, string name);
        [DllImport("user32.dll")] private static extern bool SetGestureConfig(IntPtr hWnd, int reserved, int count, [In] GestureConfig[] config, int size);
        [DllImport("user32.dll")] private static extern bool SetWindowFeedbackSetting(IntPtr hWnd, int feedback, int flags, int size, ref int value);

        private static bool _noGestures, _noFeedback;

        private static readonly HashSet<IntPtr> _nativeOn = new HashSet<IntPtr>();

        /// <summary>The window's handling of a finger as the mode wants it.</summary>
        internal static void ApplyNative(Control c)
        {
            if (c == null || c.IsDisposed || !c.IsHandleCreated) return;
            IntPtr h;
            try { h = c.Handle; } catch { return; }
            bool on = On;
            lock (_nativeOn) { if (on) _nativeOn.Add(h); else _nativeOn.Remove(h); }
            if (!IsWindows) return;
            try
            {
                if (on) SetProp(h, TabletProperty, (IntPtr)(TabletDisablePressAndHold | TabletDisablePenTapFeedback | TabletDisablePenBarrelFeedback | TabletDisableFlicks));
                else RemoveProp(h, TabletProperty);
            }
            catch { /* no such call here */ }
            if (!_noGestures)
            {
                try
                {
                    int size = Marshal.SizeOf(typeof(GestureConfig));
                    if (on)
                    {
                        // every gesture off - the press-and-hold wait with them: a finger's press comes at the touch
                        SetGestureConfig(h, 0, 1, new[] { new GestureConfig { Id = GidAll, Want = 0, Block = GcAllGestures } }, size);
                    }
                    else
                    {
                        // Windows' own again: every gesture, the pan as Windows has it for a window
                        SetGestureConfig(h, 0, 1, new[] { new GestureConfig { Id = GidAll, Want = GcAllGestures, Block = 0 } }, size);
                        SetGestureConfig(h, 0, 1, new[] { new GestureConfig { Id = GidPan, Want = GcPanSingleFingerVertically | GcPanWithGutter | GcPanWithInertia, Block = GcPanSingleFingerHorizontally } }, size);
                    }
                }
                catch (EntryPointNotFoundException) { _noGestures = true; }
                catch { }
            }
            if (!_noFeedback)
            {
                try
                {
                    // the ring drawn while a finger is held, and the square of a right-tap: not ours to show
                    int show = on ? 0 : 1;
                    SetWindowFeedbackSetting(h, FeedbackTouchPressAndHold, 0, 4, ref show);
                    show = on ? 0 : 1;
                    SetWindowFeedbackSetting(h, FeedbackTouchRightTap, 0, 4, ref show);
                }
                catch (EntryPointNotFoundException) { _noFeedback = true; }
                catch { }
            }
        }

        /// <summary>True when the window's handling of a finger was set for touch (tests).</summary>
        public static bool IsPreparedForTouch(Control c)
        {
            if (c == null || !c.IsHandleCreated) return false;
            lock (_nativeOn) return _nativeOn.Contains(c.Handle);
        }

        private static void ApplyNativeToAll()
        {
            foreach (var c in AllControls()) ApplyNative(c);
        }
    }
}
