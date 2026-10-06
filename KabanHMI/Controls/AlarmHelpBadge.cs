using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Enums;
using Kaban.Core.Models;
using Kaban.UI.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// The "?" on a unit's frame: shown, blinking, while there is something to
    /// look up; a click opens the help for it.
    ///
    /// On the unit itself because that is where the operator is looking when a
    /// cylinder fails to move - the alarm list is a page away. On the TOP frame
    /// line, at its right end, beside the unit's "i" (its caption holds the left
    /// end): outside the buttons, so opening help can never be mistaken for
    /// moving the unit. Hidden while there is nothing to look up: a permanent
    /// "?" on every unit is clutter that trains the eye to skip it.
    ///
    /// TWO WAYS TO BE SHOWN
    /// --------------------
    /// A cylinder: while its error bit is set, and the menu is the unit's
    /// troubleshooting help.
    ///
    /// An axis (SetCodeWords): while any of its three drive codes is not zero -
    /// servo alarm, axis alarm, axis warning - with ONE MENU ENTRY PER CODE,
    /// each opening only that code's entry of the manual. All three zero, no
    /// "?", whatever the error bit says: there is nothing to look up. The error
    /// bit still drives the red border on the position (IsErrorSet). Only
    /// warnings: drawn amber, as the Axis Settings row draws a warning code.
    /// </summary>
    [ToolboxItem(false)]
    public class AlarmHelpBadge : Control, IPlcExtraAddresses, UiStyle.IDrawsOwnHoverBorder
    {
        public int PlcNumber { get; set; } = 1;
        public string Address { get; set; } = "";

        private bool _active, _error, _warningOnly;

        /// <summary>
        /// Watches its bit and codes but never shows itself: a function unit shows
        /// ONE "?" (UnitHelpBadge) for both of its error bits and its own help,
        /// and follows its two watchers through ActiveChanged.
        /// </summary>
        public bool Watcher { get; set; }

        /// <summary>True while there is something to look up - the "?" of a stand-alone badge is shown.</summary>
        public bool IsActive { get { return _active; } }

        /// <summary>True when every code set is a warning (an axis that still runs).</summary>
        public bool IsWarningOnly { get { return _warningOnly; } }

        /// <summary>What is wrong, one line per code or the alarm's name - for a tooltip.</summary>
        public string TipText { get; private set; } = "";

        /// <summary>True while the error bit is set - what the unit's red border follows.</summary>
        public bool IsErrorSet { get { return _error; } }

        public const int BadgeSize = 16;

        // ---- the axis's code words ----------------------------------------

        private sealed class CodeWord
        {
            public AxisCodeKind Kind;
            public string Address;
            public int Value;
        }

        private readonly List<CodeWord> _codes = new List<CodeWord>();

        /// <summary>The unit's name, for the help titles.</summary>
        public string UnitName { get; set; } = "";

        /// <summary>
        /// Makes this an axis's "?": shown from the three code words instead of
        /// the error bit. Empty addresses are left out; all empty returns the
        /// badge to the error bit.
        /// </summary>
        public void SetCodeWords(string servoAlarm, string axisError, string axisWarning)
        {
            _codes.Clear();
            Action<AxisCodeKind, string> add = (k, a) =>
            {
                if (!string.IsNullOrWhiteSpace(a)) _codes.Add(new CodeWord { Kind = k, Address = a.Trim() });
            };
            // The order of the menu: the drive's own alarm first - it is usually
            // the root of the other two - then the module's alarm, then its warning.
            add(AxisCodeKind.ServoAlarm, servoAlarm);
            add(AxisCodeKind.AxisError, axisError);
            add(AxisCodeKind.AxisWarning, axisWarning);
        }

        /// <summary>True when the "?" follows code words (an axis), not the error bit.</summary>
        public bool UsesCodeWords { get { return _codes.Count > 0; } }

        public IEnumerable<string> ExtraAddresses
        {
            get { return _codes.Select(c => c.Address).Concat(SignalList(true)).ToList(); }
        }

        /// <summary>The axis's code words alone (the signals are the unit's own devices, listed with it).</summary>
        public IEnumerable<string> CodeAddresses { get { return _codes.Select(c => c.Address).ToList(); } }

        // ---- the movement's signals ----------------------------------------

        /// <summary>
        /// The devices of the movement this error bit watches - its command, the
        /// valve outputs, the sensors of both ends - supplied by the unit. They are
        /// read on every scan (ExtraAddresses), and their states JUST BEFORE the
        /// error bit came on are kept (AtAlarm): what was missing when the
        /// movement failed is what the troubleshooting starts from.
        /// </summary>
        public Func<IEnumerable<string>> Signals;

        private List<string> _signals;
        private Dictionary<string, int> _last;
        private bool _bitSeen;

        private List<string> SignalList(bool refresh)
        {
            if (_signals == null || refresh)
            {
                IEnumerable<string> list = null;
                try { list = Signals != null ? Signals() : null; } catch { list = null; }
                _signals = (list ?? Enumerable.Empty<string>())
                           .Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim().ToUpperInvariant())
                           .Distinct().ToList();
            }
            return _signals;
        }

        /// <summary>The signals' states when the error bit came on, or null.</summary>
        public SignalSnapshot AtAlarm { get; private set; }

        /// <summary>
        /// The alarm the HMI raised itself for this movement (AlarmGeneration), asked
        /// on every scan; null when there is none. Such an alarm sets the error like
        /// the PLC's bit, and its states when the time ran out are what the help
        /// starts from.
        /// </summary>
        public Func<AlarmItem> Generated;

        /// <summary>The HMI's own alarm on show for this movement, or null.</summary>
        public AlarmItem GeneratedItem { get; private set; }

        /// <summary>The signals' states at the last scan (1 on, 0 off), or null before one.</summary>
        public IDictionary<string, int> LiveSignals { get { return _last; } }

        /// <summary>What the movement's devices were doing when its error bit came on.</summary>
        public sealed class SignalSnapshot
        {
            /// <summary>The scan before the error bit was first seen: the movement as it failed.</summary>
            public Dictionary<string, int> Before;
            public DateTime Time;
            /// <summary>
            /// False when the bit was already set at the first scan of the page (or
            /// the station): then Before is that first scan, after the fact.
            /// </summary>
            public bool SeenRising;

            public int? State(string address)
            {
                int v;
                return Before != null && !string.IsNullOrWhiteSpace(address) &&
                       Before.TryGetValue(address.Trim().ToUpperInvariant(), out v) ? v : (int?)null;
            }
        }

        private void WatchSignals(Func<string, int> lookup, bool error)
        {
            var list = SignalList(false);
            Dictionary<string, int> now = null;
            if (list.Count > 0)
            {
                now = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in list) now[a] = lookup(a) != 0 ? 1 : 0;
            }
            if (error && !_bitSeen)
                AtAlarm = new SignalSnapshot { Before = _last ?? now, Time = DateTime.Now, SeenRising = _last != null };
            _bitSeen = error;
            if (now != null) _last = now;
        }

        // One timer for every badge: a page of twenty units must not run twenty.
        private static readonly List<AlarmHelpBadge> _blinking = new List<AlarmHelpBadge>();
        private static Timer _blink;
        private static bool _phase;

        public AlarmHelpBadge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Size = new Size(BadgeSize, BadgeSize);
            Visible = false;
            TabStop = false;
            Cursor = Cursors.Hand;             // this one IS something to click
        }

        public void UpdateState(Func<string, int> lookup, Func<SystemCondition, bool> checkGlobal)
        {
            // A station that stopped answering cannot say anything has cleared, or
            // what the code is now: shown as nothing rather than as a stale fault.
            bool live = lookup != null && !ConnectionMonitor.IsKnownDown(PlcNumber);
            bool error = live && !string.IsNullOrWhiteSpace(Address) && lookup(Address) != 0;
            if (live) WatchSignals(lookup, error);

            AlarmItem gen = null;
            if (live && Generated != null && _codes.Count == 0)
                try { gen = Generated(); } catch { gen = null; }
            if (!ReferenceEquals(gen, GeneratedItem))
            {
                GeneratedItem = gen;
                // its states when the time ran out (a PLC bit's stay its own)
                if (gen != null && gen.Snapshot != null)
                    AtAlarm = new SignalSnapshot { Before = new Dictionary<string, int>(gen.Snapshot, StringComparer.OrdinalIgnoreCase), Time = gen.TriggerTime, SeenRising = true };
                if (_active) RefreshTip();
                if (Watcher && _active) { var h0 = ActiveChanged; if (h0 != null) h0(this, EventArgs.Empty); }
            }
            if (gen != null) error = true;

            if (_codes.Count == 0) { Set(error, error, false); return; }

            bool codesChanged = false;
            foreach (var c in _codes)
            {
                // A code word is one 16-bit word; the high half of the pair the
                // scanner may have read beside it is not part of it.
                int v = live ? lookup(c.Address) & 0xFFFF : 0;
                if (v != c.Value) { c.Value = v; codesChanged = true; }
            }
            bool any = _codes.Any(c => c.Value != 0);
            // Looked up only when a code CHANGES, not on every 100 ms scan.
            bool warningOnly = codesChanged
                ? any && _codes.Where(c => c.Value != 0).All(c => AxisCodeHelp.IsWarning(c.Kind, c.Value))
                : _warningOnly;
            bool warnChanged = warningOnly != _warningOnly;
            Set(any, error, warningOnly);
            if (codesChanged && _active) RefreshTip();
            // A watcher's unit repaints its "?" amber or red as the codes change.
            if (Watcher && _active && (codesChanged || warnChanged)) { var h = ActiveChanged; if (h != null) h(this, EventArgs.Empty); }
        }

        /// <summary>
        /// Raised when the "?" comes or goes, or the error bit changes, so the
        /// unit can place the badge and blink the border of the part that failed.
        /// </summary>
        public event EventHandler ActiveChanged;

        /// <summary>Shows or hides the badge for an error bit; public so a test can drive it.</summary>
        public void SetActive(bool on)
        {
            Set(on, on, false);
        }

        private void Set(bool shown, bool error, bool warningOnly)
        {
            bool changed = shown != _active || error != _error;
            bool repaint = warningOnly != _warningOnly;
            _warningOnly = warningOnly;
            if (!changed) { if (repaint) Invalidate(); return; }

            bool wasShown = _active;
            _active = shown;
            _error = error;
            if (shown && !wasShown) RefreshTip();
            if (Watcher)
            {
                Visible = false;
                var h = ActiveChanged;
                if (h != null) h(this, EventArgs.Empty);
                return;
            }
            Visible = shown;
            var handler = ActiveChanged;
            if (handler != null) handler(this, EventArgs.Empty);
            if (shown && !wasShown) StartBlinking(this);
            else if (!shown && wasShown) StopBlinking(this);
        }

        /// <summary>The codes currently set, one line each, or the alarm's name.</summary>
        private void RefreshTip()
        {
            string text;
            if (_codes.Count > 0)
            {
                text = string.Join("\r\n", _codes.Where(c => c.Value != 0).Select(c => AxisCodeHelp.Describe(c.Kind, c.Value)));
            }
            else if (GeneratedItem != null)
            {
                text = GeneratedItem.ShownCode + " " + GeneratedItem.Description;
            }
            else
            {
                var def = AlarmManager.FindByStationAndAddress(PlcNumber, Address);
                text = def != null ? LanguageManager.GetString(def.TranslationKey, def.AlarmCode) : "";
            }
            TipText = text;
            if (Watcher) return;
            UiStyle.SetTip(UiStyle.Tips(), this, (text.Length > 0 ? text + "\r\n" : "") +
                                            LanguageManager.GetString("Tip_HelpBadge", "Click for help"));
        }

        private static void StartBlinking(AlarmHelpBadge b)
        {
            if (!_blinking.Contains(b)) _blinking.Add(b);
            if (_blink == null)
            {
                _blink = new Timer { Interval = 500 };
                _blink.Tick += (s, e) =>
                {
                    _phase = !Kaban.Core.Helpers.ViewPreferences.BlinkingOn || !_phase;     // blinking off: the filled mark, steady
                    foreach (var x in _blinking.ToArray())
                    {
                        // A station that stopped answering cannot say the error has
                        // cleared; hide rather than blink a reading nobody can trust.
                        if (x.IsDisposed || ConnectionMonitor.IsKnownDown(x.PlcNumber)) { x.Set(false, false, false); continue; }
                        x.Invalidate();
                    }
                };
            }
            _blink.Start();
        }

        private static void StopBlinking(AlarmHelpBadge b)
        {
            _blinking.Remove(b);
            if (_blinking.Count == 0 && _blink != null) _blink.Stop();
        }

        private Font _font;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopBlinking(this);
                if (_font != null) { _font.Dispose(); _font = null; }
            }
            base.Dispose(disposing);
        }

        /// <summary>Red for a fault; amber when every code set is a warning (the axis still runs).</summary>
        public Color MarkColor
        {
            get { return _warningOnly ? Color.FromArgb(214, 150, 20) : Color.FromArgb(214, 36, 36); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(1, 1, Width - 3, Height - 3);

            // ALTERNATING with white, as the caption bar's alarm mark does.
            var mark = MarkColor;
            using (var fill = new SolidBrush(_phase ? mark : Color.White))
                g.FillEllipse(fill, r);
            using (var pen = new Pen(mark, 1.6f))
                g.DrawEllipse(pen, r);

            // Cached: this paints twice a second on every active badge.
            if (_font == null || Math.Abs(_font.Size - Math.Max(7f, Height * 0.5f)) > 0.1f)
            {
                if (_font != null) _font.Dispose();
                _font = new Font("Segoe UI", Math.Max(7f, Height * 0.5f), FontStyle.Bold, GraphicsUnit.Pixel);
            }
            TextRenderer.DrawText(g, "?", _font, r, _phase ? Color.White : mark,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        /// <summary>
        /// The documents behind the "?" right now: one per code set (an axis), or
        /// the error bit's troubleshooting help (a cylinder).
        /// </summary>
        public List<TroubleshootingCatalog.HelpDocument> Documents()
        {
            if (_codes.Count > 0)
                return _codes.Where(c => c.Value != 0)
                             // No axis name in the entry: the "?" is on that axis's own unit.
                             .Select(c => AxisCodeHelp.Document(c.Kind, c.Value, ""))
                             .Where(d => d != null).ToList();
            var def = AlarmManager.FindByStationAndAddress(PlcNumber, Address);
            return AlarmHelpLinks.DocumentsFor(PlcNumber, Address, def != null ? def.HelperKeys : null);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            var docs = Documents();
            if (docs.Count == 0) return;

            // The SAME list the alarm grid shows, so the two routes to help agree.
            var menu = Kaban.UI.Screens.MachineAlarmScreen.BuildHelpMenu(docs, d => AlarmHelpLinks.Open(FindForm(), d));
            UiStyle.ShowPopup(menu, this, new Point(0, Height));
        }
    }
}

namespace Kaban.UI.Controls
{
    /// <summary>
    /// A function unit's ONE "?", on the right end of its top frame line.
    ///
    /// It used to be two marks: an "i" for the unit's own help and a red "?"
    /// beside it while an error bit was set - two small circles side by side
    /// that read as the same thing. Now one, and always BLUE: shown while there
    /// is help to offer (the unit's own files, the cylinder manual for its valve
    /// and feedback, the fault's own entries while an error is set), hidden when
    /// there is nothing to open. One document opens straight away; several offer
    /// the list used everywhere else.
    ///
    /// It never turns RED (or amber) and never blinks: the buttons and the
    /// position display of what failed show the error already - a second red
    /// mark beside them said nothing new. The State still tells an error from
    /// plain help (the tooltip and the list follow it); only the colour does not.
    /// </summary>
    [System.ComponentModel.ToolboxItem(false)]
    public class UnitHelpBadge : Control, UiStyle.IDrawsOwnHoverBorder
    {
        public enum BadgeState { Hidden, Info, Error, Warning }

        public const int BadgeSize = 16;

        /// <summary>Supplied by the unit that owns the badge.</summary>
        public Func<List<TroubleshootingCatalog.HelpDocument>> Documents;

        private BadgeState _state = BadgeState.Hidden;
        private bool _hover;
        private Font _font;


        public UnitHelpBadge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Size = new Size(BadgeSize, BadgeSize);
            Visible = false;
            TabStop = false;
            Cursor = Cursors.Hand;
        }

        public BadgeState State
        {
            get { return _state; }
            set
            {
                if (value == _state) return;
                _state = value;
                Visible = value != BadgeState.Hidden;
                Invalidate();
            }
        }

        public static readonly Color Blue = Color.FromArgb(40, 110, 190);

        /// <summary>The colour of the mark: blue in every state.</summary>
        public Color MarkColor
        {
            get { return Blue; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _font != null) { _font.Dispose(); _font = null; }
            base.Dispose(disposing);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            var mark = MarkColor;

            // White with a blue "?" (it covers the frame line under it), filled on
            // hover - the same in every state.
            bool filled = _hover;
            using (var fill = new SolidBrush(filled ? mark : Color.White))
                g.FillEllipse(fill, r);
            using (var pen = new Pen(mark, 1.4f))
                g.DrawEllipse(pen, r);

            if (_font == null || Math.Abs(_font.Size - Math.Max(7f, Height * 0.5f)) > 0.1f)
            {
                if (_font != null) _font.Dispose();
                _font = new Font("Segoe UI", Math.Max(7f, Height * 0.5f), FontStyle.Bold, GraphicsUnit.Pixel);
            }
            TextRenderer.DrawText(g, "?", _font, r, filled ? Color.White : mark,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            var docs = Documents != null ? Documents() : null;
            if (docs == null || docs.Count == 0) return;

            if (docs.Count == 1) { AlarmHelpLinks.Open(FindForm(), docs[0]); return; }

            // The SAME list the alarm grid shows, so the two routes to help agree.
            var menu = Kaban.UI.Screens.MachineAlarmScreen.BuildHelpMenu(docs, d => AlarmHelpLinks.Open(FindForm(), d));
            UiStyle.ShowPopup(menu, this, new Point(0, Height));
        }
    }
}
