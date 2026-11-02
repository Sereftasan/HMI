using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// THE KEYBOARD ON THE SCREEN (29 October, the plan's 4: "an on-screen keyboard matching the
    /// number pad, with Turkish letters; it opens when a text box is tapped").
    ///
    /// The Turkish Q layout - ı and i, ğ ü ş ö ç on their own keys, İ and I as Turkish writes them -
    /// with the digits across the top, a page of signs (?123), the caret's arrows and a key that puts
    /// it away. The keypad's look: light grey, white keys, the word keys darker.
    ///
    /// IT NEVER TAKES THE FOCUS: the text box tapped keeps it, its caret, its selection; a key is the
    /// character typed into it (Windows: posted to it as a key of a keyboard, so what the box does with
    /// a key - Enter to search, a filter of the characters allowed - it does with these too).
    ///
    /// Opened by TouchInput on a tap on a text box in touch mode; it goes when the focus leaves the
    /// text boxes, when ✕ is tapped, after Enter in a one-line box. At the bottom of the screen, at
    /// its top when the box tapped is down there.
    /// </summary>
    public sealed class TouchKeyboard : Form
    {
        private static TouchKeyboard _current;

        /// <summary>The keyboard on screen (tests); null when none was opened.</summary>
        public static TouchKeyboard Current { get { return _current != null && !_current.IsDisposed ? _current : null; } }

        /// <summary>True while the keyboard is on the screen.</summary>
        public static bool IsShown { get { return Current != null && Current.Visible; } }

        private readonly KeyPad _pad;
        private readonly Timer _watch = new Timer { Interval = 300 };
        private Control _target;

        /// <summary>The control it types into now (tests).</summary>
        public Control Target { get { return _target; } }

        /// <summary>Shift for the next letter, held (tapped twice), or the page of signs (tests).</summary>
        public bool Shift { get { return _pad.ShiftOn; } }
        public bool CapsLock { get { return _pad.Caps; } }
        public bool Signs { get { return _pad.SignsOn; } }

        /// <summary>The keyboard for a text control: shown (or moved to it), never activated.</summary>
        public static TouchKeyboard ShowFor(Control target)
        {
            if (target == null || target.IsDisposed) return null;
            var k = Current;
            if (k == null) _current = k = new TouchKeyboard();
            k._target = target;
            k._away = 0;
            k.PlaceFor(target);
            if (!k.Visible) k.Show();
            EnableNative(k);
            GiveBack(k, target);
            k.MakeRoom(target);
            k._watch.Start();
            return k;
        }

        /// <summary>Puts it away (✕, the focus gone from the text boxes).</summary>
        public static void HideNow()
        {
            var k = Current;
            if (k == null) return;
            k._watch.Stop();
            k._pad.Reset();
            if (k.Visible) k.Hide();
            k.GiveRoomBack();
            k._target = null;
        }

        private TouchKeyboard()
        {
            Name = TouchInput.TouchKeyboardName;
            Text = "Keyboard";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            // ON TOP BY ITS STYLE ONLY (WS_EX_TOPMOST, CreateParams) - not TopMost = true: WinForms applies that
            // with a SetWindowPos that ACTIVATES the window, and the keyboard, made the active window, took the
            // focus from the text box; 0.3 s later it saw no text box with the focus and went (30 October: "the
            // keyboard disappears as soon as appear, doesn't stay on the screen")
            BackColor = Color.FromArgb(232, 234, 238);
            _pad = new KeyPad(this) { Dock = DockStyle.Fill };
            Controls.Add(_pad);
            ClientSize = _pad.WantedSize;
            _watch.Tick += (s, e) => Watch();
            FormClosed += (s, e) => { _watch.Stop(); _watch.Dispose(); if (_current == this) _current = null; };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008;   // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            // a tap on a key: the window it is in is not made the active one
            if (m.Msg == 0x0021) { m.Result = (IntPtr)3; return; }           // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            base.WndProc(ref m);
        }

        /// <summary>
        /// ALWAYS IN ONE PLACE: along the bottom of the screen, as wide as it, the keys in its middle (2 November: "The user's
        /// fingers can't get used to the keyboard which appears at the different part of the screen each time ... we should
        /// open the control at the fixed position in the bottom of the screen (not like the numerical input pop-up) and move
        /// the page down until the edited control gets to the above of it (using the vertical scrolling function)"). It
        /// stood at the bottom of the box's screen - at its top when it would have covered the box.
        /// </summary>
        private void PlaceFor(Control target)
        {
            Rectangle box;
            try { box = new Rectangle(target.PointToScreen(Point.Empty), target.Size); }
            catch { box = Rectangle.Empty; }
            var screen = (box.IsEmpty ? Screen.PrimaryScreen : Screen.FromRectangle(box)).WorkingArea;
            var keys = _pad.WantedSize;
            Bounds = new Rectangle(screen.Left, screen.Bottom - keys.Height, Math.Max(keys.Width, screen.Width), keys.Height);
        }

        // ---- the box above the keyboard ------------------------------------------------------------
        //
        // The page under the box scrolls until the box is above the keyboard, with a finger's room to spare: the page
        // given room below its last control as tall as the keyboard covers of it (a page whose parts are docked: kept
        // above the keyboard instead), a table's rows scrolled to the cell being edited, a window that does not scroll
        // (a dialog) moved up. All of it given back when the keyboard goes.

        private sealed class Room
        {
            public ScrollableControl Host;
            public Size MinSize;
            public Point Scroll;
            public Padding Pad;
            public bool Padded;
            public Form Moved;
            public Point WasAt;
        }

        private readonly List<Room> _rooms = new List<Room>();

        /// <summary>How far above the keyboard the box is kept, at factor 1.</summary>
        public const int RoomAbove = 12;

        /// <summary>The box on the screen, and how far the keyboard covers it (0: not).</summary>
        private int Covered(Control target)
        {
            if (target == null || target.IsDisposed || !target.IsHandleCreated || !Visible) return 0;
            Rectangle box;
            try { box = target.RectangleToScreen(target.ClientRectangle); } catch { return 0; }
            return Math.Max(0, box.Bottom + UiScale.Px(RoomAbove) - Top);
        }

        private static bool DocksItsParts(Control host)
        {
            foreach (Control c in host.Controls)
                if (c.Visible && (c.Dock == DockStyle.Fill || c.Dock == DockStyle.Bottom || c.Dock == DockStyle.Left || c.Dock == DockStyle.Right)) return true;
            return false;
        }

        /// <summary>The box brought above the keyboard (the room made before given back first).</summary>
        internal void MakeRoom(Control target)
        {
            GiveRoomBack();
            try
            {
                if (Covered(target) <= 0) return;
                // the scrolling parts around the box, innermost first: each one the keyboard covers scrolls (or keeps
                // its docked parts above the keyboard) until the box is clear of it
                for (var c = target.Parent; c != null && !(c is Form) && Covered(target) > 0; c = c.Parent)
                {
                    var sc = c as ScrollableControl;
                    if (sc == null || !sc.AutoScroll || !sc.IsHandleCreated) continue;
                    var view = sc.RectangleToScreen(sc.ClientRectangle);
                    int under = view.Bottom - Top;                 // how much of it the keyboard covers
                    if (under <= 0) continue;
                    var room = new Room { Host = sc, MinSize = sc.AutoScrollMinSize, Scroll = new Point(-sc.AutoScrollPosition.X, -sc.AutoScrollPosition.Y), Pad = sc.Padding };
                    _rooms.Add(room);
                    if (DocksItsParts(sc))
                    {
                        // a page of docked parts: they keep above the keyboard
                        room.Padded = true;
                        sc.Padding = new Padding(room.Pad.Left, room.Pad.Top, room.Pad.Right, room.Pad.Bottom + under);
                        sc.PerformLayout();
                    }
                    else
                    {
                        // room below its last control as tall as the keyboard covers of it, then scrolled up by what covers the box
                        int content = Math.Max(sc.DisplayRectangle.Height, room.MinSize.Height);
                        sc.AutoScrollMinSize = new Size(room.MinSize.Width, content + under);
                        int need = Covered(target);
                        sc.AutoScrollPosition = new Point(room.Scroll.X, room.Scroll.Y + need);
                    }
                }
                // a table's cell being edited: its rows scrolled to it
                var grid = target as DataGridView ?? (target is IDataGridViewEditingControl ? ((IDataGridViewEditingControl)target).EditingControlDataGridView : null);
                if (grid != null && Covered(target) > 0 && grid.CurrentCell != null)
                {
                    int shown = Math.Max(1, grid.DisplayedRowCount(false) - 1);
                    int first = Math.Max(0, grid.CurrentCell.RowIndex - shown + 1);
                    try { if (grid.FirstDisplayedScrollingRowIndex < first) grid.FirstDisplayedScrollingRowIndex = first; } catch { }
                }
                // a window that does not scroll (a dialog): up, as far as the screen lets it
                var form = target.FindForm();
                int still = Covered(target);
                if (still > 0 && form != null && form.WindowState == FormWindowState.Normal && !(form is TouchKeyboard))
                {
                    var screen = Screen.FromControl(form).WorkingArea;
                    int up = Math.Min(still, Math.Max(0, form.Top - screen.Top));
                    if (up > 0)
                    {
                        _rooms.Add(new Room { Moved = form, WasAt = form.Location });
                        form.Location = new Point(form.Left, form.Top - up);
                    }
                }
            }
            catch (Exception ex) { Kaban.Core.Helpers.AuditLogger.Log("keyboard: room for the box not made (" + ex.Message + ")."); }
        }

        /// <summary>The pages scrolled back and the windows moved back where they were.</summary>
        internal void GiveRoomBack()
        {
            for (int i = _rooms.Count - 1; i >= 0; i--)
            {
                var r = _rooms[i];
                try
                {
                    if (r.Moved != null) { if (!r.Moved.IsDisposed) r.Moved.Location = r.WasAt; continue; }
                    if (r.Host == null || r.Host.IsDisposed) continue;
                    if (r.Padded) r.Host.Padding = r.Pad;
                    else
                    {
                        r.Host.AutoScrollMinSize = r.MinSize;
                        r.Host.AutoScrollPosition = r.Scroll;
                    }
                }
                catch { }
            }
            _rooms.Clear();
        }

        /// <summary>How far the keyboard covers the box it types into now (tests: 0 - above it).</summary>
        public int CoversTarget { get { return Covered(_target); } }

        private int _away;

        /// <summary>
        /// Gone when the focus is no longer in a text box of the program (a button tapped, a page left) - seen
        /// so twice running (0.6 s), not at a moment between two windows. The focus in the keyboard itself (Windows
        /// made it the active window after all): given back to the box, the keyboard stays.
        /// </summary>
        private void Watch()
        {
            if (!Visible) { _watch.Stop(); return; }
            var focused = TouchInput.FocusedTextControl();
            if (focused != null)
            {
                // another box now (the next field tapped, Tab): the room made for it instead
                if (!ReferenceEquals(focused, _target)) { _target = focused; MakeRoom(focused); }
                _away = 0;
                return;
            }
            bool targetGone = _target == null || _target.IsDisposed || !_target.Visible;
            if (!targetGone && HasFocusHere()) { GiveBack(this, _target); _away = 0; return; }
            if (!TouchInput.IsWindows && !targetGone) return;
            if (targetGone || ++_away >= 2) HideNow();
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        /// <summary>The keyboard is the active window, or has the focus.</summary>
        private bool HasFocusHere()
        {
            if (ContainsFocus || ActiveForm == this) return true;
            if (!TouchInput.IsWindows || !IsHandleCreated) return false;
            try { return GetForegroundWindow() == Handle; } catch { return false; }
        }

        /// <summary>The window of the text box made the active one again, the box given the focus (when the keyboard took them).</summary>
        private static void GiveBack(TouchKeyboard k, Control target)
        {
            if (target == null || target.IsDisposed || !k.HasFocusHere()) return;
            try
            {
                var f = target.FindForm();
                if (f != null && !f.IsDisposed) f.Activate();
                if (target.CanFocus) target.Focus();
            }
            catch { }
        }

        // ---- typing ---------------------------------------------------------------------------------

        private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

        /// <summary>A letter as the Turkish keyboard writes it upper case: i - İ, ı - I.</summary>
        public static string Upper(string s) { return s.ToUpper(Turkish); }

        private const int WmKeyDown = 0x0100, WmKeyUp = 0x0101, WmChar = 0x0102;
        private const int VkBack = 0x08, VkTab = 0x09, VkReturn = 0x0D, VkLeft = 0x25, VkRight = 0x27, VkDelete = 0x2E;

        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr GetFocus();
        [DllImport("user32.dll")] private static extern bool EnableWindow(IntPtr hWnd, bool enable);

        private static void EnableNative(Form f)
        {
            // a dialog opened while it was on the screen left it disabled (WinForms' modal loop)
            if (!TouchInput.IsWindows) return;
            try { EnableWindow(f.Handle, true); } catch { }
        }

        /// <summary>The window that has the keyboard's focus: Windows' own when it is the program's, else the target.</summary>
        private IntPtr FocusWindow()
        {
            if (TouchInput.IsWindows)
            {
                try
                {
                    var h = GetFocus();
                    if (h != IntPtr.Zero && Control.FromChildHandle(h) != null) return h;
                }
                catch { }
            }
            return _target != null && !_target.IsDisposed && _target.IsHandleCreated ? _target.Handle : IntPtr.Zero;
        }

        /// <summary>Characters typed into the control.</summary>
        internal void Type(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var h = FocusWindow();
            if (TouchInput.IsWindows && h != IntPtr.Zero)
            {
                foreach (char ch in text) PostMessage(h, WmChar, (IntPtr)ch, (IntPtr)1);
                return;
            }
            // elsewhere (the tests' Mono): into the box itself
            var tb = TextOf(_target);
            if (tb != null) { tb.SelectedText = text; return; }
            var combo = _target as ComboBox;
            if (combo != null) { combo.SelectedText = text; }
        }

        /// <summary>A key that is no character: backspace, the arrows, Enter.</summary>
        internal void Key(int vk)
        {
            var h = FocusWindow();
            if (TouchInput.IsWindows && h != IntPtr.Zero)
            {
                PostMessage(h, WmKeyDown, (IntPtr)vk, (IntPtr)1);
                PostMessage(h, WmKeyUp, (IntPtr)vk, unchecked((IntPtr)(int)0xC0000001));
                return;
            }
            var tb = TextOf(_target);
            if (tb == null) return;
            int at = tb.SelectionStart, len = tb.SelectionLength;
            switch (vk)
            {
                case VkBack:
                    if (len > 0) tb.SelectedText = "";
                    else if (at > 0) { tb.Select(at - 1, 1); tb.SelectedText = ""; }
                    break;
                case VkLeft: tb.Select(Math.Max(0, at - 1), 0); break;
                case VkRight: tb.Select(Math.Min(tb.TextLength, at + len + (len == 0 ? 1 : 0)), 0); break;
                case VkReturn: if (tb.Multiline) tb.SelectedText = "\r\n"; break;
            }
        }

        private static TextBoxBase TextOf(Control c) { return c as TextBoxBase; }

        /// <summary>Enter typed: a one-line box is done with (it searches, the table takes the cell) - the keyboard goes.</summary>
        internal void Enter()
        {
            var tb = _target as TextBoxBase;
            bool stays = tb != null && tb.Multiline && (!(tb is TextBox) || ((TextBox)tb).AcceptsReturn);
            Key(VkReturn);
            if (!stays) HideNow();
        }

        internal const int KeyBack = VkBack, KeyLeft = VkLeft, KeyRight = VkRight;

        // ---- the keys ---------------------------------------------------------------------------------

        /// <summary>The keys, painted: a key is no control (a control tapped would take the focus).</summary>
        private sealed class KeyPad : Control
        {
            private sealed class K
            {
                public string Face;      // what it types ("" for a word key)
                public string Word;      // a word key: Back, Shift, Enter, Signs, Space, Left, Right, Hide
                public float Units;
                public RectangleF Bounds;
            }

            private readonly TouchKeyboard _kb;
            private readonly List<K> _letters = new List<K>(), _signs = new List<K>();
            private K _down;
            private readonly Timer _repeat = new Timer();
            private DateTime _shiftAt = DateTime.MinValue;
            public bool ShiftOn, Caps, SignsOn;

            const int Unit = 64, Tall = 58, Gap = 5, Pad = 8;
            const float Columns = 13.5f;

            public KeyPad(TouchKeyboard kb)
            {
                _kb = kb;
                Name = "keys";
                SetStyle(ControlStyles.Selectable, false);
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.FromArgb(232, 234, 238);
                Build(_letters, false);
                Build(_signs, true);
                _repeat.Tick += (s, e) => { _repeat.Interval = 70; if (_down != null && _down.Word == "Back") _kb.Key(KeyBack); };
            }

            private static int S(int v) { return UiScale.Px(v); }

            public Size WantedSize
            {
                get { return new Size(S(Pad) * 2 + (int)(Columns * S(Unit) + (Columns - 1) * S(Gap)), S(Pad) * 2 + 5 * S(Tall) + 4 * S(Gap)); }
            }

            private void Build(List<K> keys, bool signs)
            {
                var rows = new List<List<K>>();
                Func<string, List<K>> chars = s => { var l = new List<K>(); foreach (var part in s.Split(' ')) l.Add(new K { Face = part, Units = 1 }); return l; };
                var r0 = chars("1 2 3 4 5 6 7 8 9 0 * -"); r0.Add(new K { Word = "Back", Units = 1.5f });
                var r1 = chars(signs ? "! \" # $ % & / ( ) = ? _" : "q w e r t y u ı o p ğ ü");
                var r2 = chars(signs ? "@ ' + : ; < > [ ] { } \\" : "a s d f g h j k l ş i ,"); r2.Add(new K { Word = "Enter", Units = 1.5f });
                var r3 = chars(signs ? "| ~ ^ ` € £ ° § ½ ×" : "z x c v b n m ö ç .");
                if (!signs) { r3.Insert(0, new K { Word = "Shift", Units = 1.5f }); r3.Add(new K { Word = "Shift", Units = 1.5f }); }
                var r4 = new List<K>
                {
                    new K { Word = "Signs", Units = 2f }, new K { Word = "Space", Units = 7.5f },
                    new K { Word = "Left", Units = 1f }, new K { Word = "Right", Units = 1f }, new K { Word = "Hide", Units = 2f }
                };
                rows.Add(r0); rows.Add(r1); rows.Add(r2); rows.Add(r3); rows.Add(r4);
                float unit = S(Unit), gap = S(Gap), pad = S(Pad), tall = S(Tall);
                float full = Columns * unit + (Columns - 1) * gap;
                for (int r = 0; r < rows.Count; r++)
                {
                    float units = 0; foreach (var k in rows[r]) units += k.Units;
                    float w = units * unit + (units - 1) * gap;
                    float x = pad + (full - w) / 2f, y = pad + r * (tall + gap);
                    foreach (var k in rows[r])
                    {
                        float kw = k.Units * unit + (k.Units - 1) * gap;
                        k.Bounds = new RectangleF(x, y, kw, tall);
                        x += kw + gap;
                        keys.Add(k);
                    }
                }
            }

            private List<K> Shown { get { return SignsOn ? _signs : _letters; } }

            /// <summary>The keys' left edge in the bar (they are laid out from 0): the bar as wide as the screen, they in its middle.</summary>
            private int Dx { get { return Math.Max(0, (ClientSize.Width - WantedSize.Width) / 2); } }

            public void Reset() { ShiftOn = Caps = SignsOn = false; _down = null; _repeat.Stop(); Invalidate(); }

            private K At(Point p)
            {
                p = new Point(p.X - Dx, p.Y);
                foreach (var k in Shown) if (k.Bounds.Contains(p)) return k;
                return null;
            }

            /// <summary>A key by what it types or its word (tests).</summary>
            public bool Press(string faceOrWord)
            {
                foreach (var k in Shown)
                    if (k.Face == faceOrWord || k.Word == faceOrWord) { Act(k); return true; }
                return false;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                _down = At(e.Location);
                if (_down != null && _down.Word == "Back") { _kb.Key(KeyBack); _repeat.Interval = 450; _repeat.Start(); }
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                _repeat.Stop();
                var k = _down;
                _down = null;
                Invalidate();
                if (k == null || e.Button != MouseButtons.Left || !k.Bounds.Contains(new Point(e.X - Dx, e.Y))) return;
                if (k.Word == "Back") return;                         // typed as it went down
                Act(k);
            }

            private void Act(K k)
            {
                switch (k.Word)
                {
                    case null:
                        string s = k.Face;
                        if (ShiftOn || Caps) s = Upper(s);
                        _kb.Type(s);
                        if (ShiftOn && !Caps) { ShiftOn = false; Invalidate(); }
                        return;
                    case "Back": _kb.Key(KeyBack); return;
                    case "Space": _kb.Type(" "); return;
                    case "Left": _kb.Key(KeyLeft); return;
                    case "Right": _kb.Key(KeyRight); return;
                    case "Enter": _kb.Enter(); return;
                    case "Hide": HideNow(); return;
                    case "Signs": SignsOn = !SignsOn; Invalidate(); return;
                    case "Shift":
                        // once: the next letter; twice quickly: all of them, until tapped again
                        var now = DateTime.Now;
                        if (Caps) { Caps = ShiftOn = false; }
                        else if (ShiftOn && (now - _shiftAt).TotalMilliseconds < 450) { Caps = true; }
                        else ShiftOn = !ShiftOn;
                        _shiftAt = now;
                        Invalidate();
                        return;
                }
            }

            private string FaceOf(K k)
            {
                switch (k.Word)
                {
                    case null: return ShiftOn || Caps ? Upper(k.Face) : k.Face;
                    case "Back": return "⌫";
                    case "Shift": return Caps ? "⇪" : "⇧";
                    case "Enter": return "↵";
                    case "Signs": return SignsOn ? "ABC" : "?123";
                    case "Space": return "";
                    case "Left": return "◀";
                    case "Right": return "▶";
                    case "Hide": return "✕";
                    default: return k.Word;
                }
            }

            private static readonly Color Ink = Color.FromArgb(28, 30, 34);

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(BackColor);
                using (var top = new Pen(Color.FromArgb(196, 201, 209))) g.DrawLine(top, 0, 0, ClientSize.Width, 0);   // the page's edge above it
                // the keys in the middle of the bar along the bottom of the screen
                g.TranslateTransform(Dx, 0);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                using (var big = new Font("Segoe UI", 17f * (float)UiScale.Factor, GraphicsUnit.Point))
                using (var word = new Font("Segoe UI", 12f * (float)UiScale.Factor, FontStyle.Bold, GraphicsUnit.Point))
                using (var edge = new Pen(Color.FromArgb(196, 201, 209)))
                using (var lit = new Pen(UiStyle.HoverBorderColor, 2f))
                    foreach (var k in Shown)
                    {
                        bool action = k.Word != null && k.Word != "Space";
                        bool on = (k.Word == "Shift" && (ShiftOn || Caps)) || (k.Word == "Signs" && SignsOn);
                        var r = Rectangle.Round(k.Bounds);
                        var back = ReferenceEquals(k, _down) ? Color.FromArgb(200, 214, 236) : on ? Color.FromArgb(214, 228, 248) : action ? Color.FromArgb(222, 226, 232) : Color.White;
                        using (var path = Rounded(r, S(5)))
                        {
                            using (var b = new SolidBrush(back)) g.FillPath(b, path);
                            g.DrawPath(on ? lit : edge, path);
                        }
                        string face = FaceOf(k);
                        bool letter = k.Word == null || k.Word == "Back" || k.Word == "Shift" || k.Word == "Enter" || k.Word == "Left" || k.Word == "Right" || k.Word == "Hide";
                        TextRenderer.DrawText(g, face, letter ? big : word, r, Ink,
                                              TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                    }
            }

            private static GraphicsPath Rounded(Rectangle r, int radius)
            {
                var p = new GraphicsPath();
                int d = radius * 2;
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
                p.CloseFigure();
                return p;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _repeat.Dispose();
                base.Dispose(disposing);
            }
        }

        /// <summary>A key pressed by what it types or its word: "a", "ş", "Shift", "Back", "Enter", "Signs", "Space", "Hide" (tests).</summary>
        public bool Press(string faceOrWord) { return _pad.Press(faceOrWord); }

        /// <summary>The keys' place on the screen, by what they show (tests, pictures).</summary>
        public Size KeysSize { get { return _pad.WantedSize; } }
    }
}
