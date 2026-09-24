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

        /// <summary>True while the "?" is shown.</summary>
        public bool IsActive { get { return _active; } }

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

        public IEnumerable<string> ExtraAddresses { get { return _codes.Select(c => c.Address); } }

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
            Set(any, error, warningOnly);
            if (codesChanged && _active) RefreshTip();
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
            Visible = shown;
            var handler = ActiveChanged;
            if (handler != null) handler(this, EventArgs.Empty);
            if (shown && !wasShown) { RefreshTip(); StartBlinking(this); }
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
            else
            {
                var def = AlarmManager.FindByStationAndAddress(PlcNumber, Address);
                text = def != null ? LanguageManager.GetString(def.TranslationKey, def.AlarmCode) : "";
            }
            UiStyle.Tips().SetToolTip(this, (text.Length > 0 ? text + "\r\n" : "") +
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
                    _phase = !_phase;
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
                             .Select(c => AxisCodeHelp.Document(c.Kind, c.Value, UnitName))
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
    /// The "i" on a unit's top frame line: always there when the unit has help,
    /// opening it directly - photos of the real mechanism, its drawing, its notes.
    ///
    /// Where the alarm "?" is on the SIDE lines and only while something is
    /// wrong, this one is on the TOP line and always available: "what is this
    /// unit" is a question worth answering before anything has failed. One file
    /// opens straight away; several offer the same list as everywhere else.
    /// </summary>
    [System.ComponentModel.ToolboxItem(false)]
    public class UnitInfoBadge : System.Windows.Forms.Control, UiStyle.IDrawsOwnHoverBorder
    {
        public const int BadgeSize = 16;

        /// <summary>Supplied by the unit that owns the badge.</summary>
        public Func<System.Collections.Generic.List<Kaban.Core.Helpers.TroubleshootingCatalog.HelpDocument>> Documents;

        private bool _hover;

        public UnitInfoBadge()
        {
            SetStyle(System.Windows.Forms.ControlStyles.UserPaint | System.Windows.Forms.ControlStyles.AllPaintingInWmPaint |
                     System.Windows.Forms.ControlStyles.OptimizedDoubleBuffer | System.Windows.Forms.ControlStyles.SupportsTransparentBackColor, true);
            BackColor = System.Drawing.Color.Transparent;
            Size = new System.Drawing.Size(BadgeSize, BadgeSize);
            Visible = false;
            TabStop = false;
            Cursor = System.Windows.Forms.Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = new System.Drawing.Rectangle(1, 1, Width - 3, Height - 3);
            var blue = System.Drawing.Color.FromArgb(40, 110, 190);

            // White by default so it covers the frame line under it; filled on hover.
            using (var fill = new System.Drawing.SolidBrush(_hover ? blue : System.Drawing.Color.White))
                g.FillEllipse(fill, r);
            using (var pen = new System.Drawing.Pen(blue, 1.4f))
                g.DrawEllipse(pen, r);
            using (var f = new System.Drawing.Font("Georgia", Math.Max(7f, Height * 0.55f),
                                                   System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic,
                                                   System.Drawing.GraphicsUnit.Pixel))
                System.Windows.Forms.TextRenderer.DrawText(g, "i", f, r, _hover ? System.Drawing.Color.White : blue,
                    System.Windows.Forms.TextFormatFlags.HorizontalCenter | System.Windows.Forms.TextFormatFlags.VerticalCenter |
                    System.Windows.Forms.TextFormatFlags.NoPadding);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            var docs = Documents != null ? Documents() : null;
            if (docs == null || docs.Count == 0) return;

            if (docs.Count == 1) { Kaban.UI.Helpers.AlarmHelpLinks.Open(FindForm(), docs[0]); return; }

            var menu = Kaban.UI.Screens.MachineAlarmScreen.BuildHelpMenu(docs,
                d => Kaban.UI.Helpers.AlarmHelpLinks.Open(FindForm(), d));
            UiStyle.ShowPopup(menu, this, new System.Drawing.Point(0, Height));
        }
    }
}

