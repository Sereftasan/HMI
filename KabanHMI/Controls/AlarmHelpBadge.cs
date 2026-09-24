using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Enums;
using Kaban.Core.Models;
using Kaban.UI.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// The "?" on a unit's frame: shown, blinking red, while its error bit is
    /// set; a click opens the help for it.
    ///
    /// On the unit itself because that is where the operator is looking when a
    /// cylinder fails to move - the alarm list is a page away. On the TOP frame
    /// line, at its right end, beside the unit's "i" (its caption holds the left
    /// end): outside the buttons, so opening help can never be mistaken for
    /// moving the unit. It used to sit on the SIDE lines, which cost the unit a
    /// strip of its width whenever an error bit was set. Hidden while there is
    /// no error: a permanent "?" on every unit is clutter that trains the eye to
    /// skip it.
    /// </summary>
    [ToolboxItem(false)]
    public class AlarmHelpBadge : Control, IPlcBoundControl, UiStyle.IDrawsOwnHoverBorder
    {
        public int PlcNumber { get; set; } = 1;
        public string Address { get; set; } = "";

        private bool _active;
        public bool IsActive { get { return _active; } }

        public const int BadgeSize = 16;

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
            bool on = !string.IsNullOrWhiteSpace(Address) &&
                      !ConnectionMonitor.IsKnownDown(PlcNumber) &&
                      lookup != null && lookup(Address) != 0;
            SetActive(on);
        }

        /// <summary>
        /// Raised when the error comes or goes, so the unit can place the badge and
        /// blink the border of the part that failed.
        /// </summary>
        public event EventHandler ActiveChanged;

        /// <summary>Shows or hides the badge; public so a test can drive it.</summary>
        public void SetActive(bool on)
        {
            if (on == _active) return;
            _active = on;
            Visible = on;
            var changed = ActiveChanged;
            if (changed != null) changed(this, EventArgs.Empty);
            if (on)
            {
                var def = AlarmManager.FindByStationAndAddress(PlcNumber, Address);
                UiStyle.Tips().SetToolTip(this,
                    (def != null ? LanguageManager.GetString(def.TranslationKey, def.AlarmCode) + "\r\n" : "") +
                    LanguageManager.GetString("Tip_HelpBadge", "Click for help"));
                StartBlinking(this);
            }
            else
            {
                StopBlinking(this);
            }
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
                        if (x.IsDisposed || ConnectionMonitor.IsKnownDown(x.PlcNumber)) { x.SetActive(false); continue; }
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(1, 1, Width - 3, Height - 3);

            // RED AND WHITE ALTERNATING, as the caption bar's alarm mark does.
            var red = Color.FromArgb(214, 36, 36);
            using (var fill = new SolidBrush(_phase ? red : Color.White))
                g.FillEllipse(fill, r);
            using (var pen = new Pen(red, 1.6f))
                g.DrawEllipse(pen, r);

            // Cached: this paints twice a second on every active badge.
            if (_font == null || Math.Abs(_font.Size - Math.Max(7f, Height * 0.5f)) > 0.1f)
            {
                if (_font != null) _font.Dispose();
                _font = new Font("Segoe UI", Math.Max(7f, Height * 0.5f), FontStyle.Bold, GraphicsUnit.Pixel);
            }
            TextRenderer.DrawText(g, "?", _font, r, _phase ? Color.White : red,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            var def = AlarmManager.FindByStationAndAddress(PlcNumber, Address);
            var docs = AlarmHelpLinks.DocumentsFor(PlcNumber, Address, def != null ? def.HelperKeys : null);
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

