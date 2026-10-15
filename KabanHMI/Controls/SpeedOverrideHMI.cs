using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// THE MACHINE'S SPEED in percent, as the old program's track bar set it - one
    /// compact control (14 October: "it should have a thing to drag left / right to
    /// lower and higher the value, and it should open the numerical display to set
    /// it directly to a specific value ... compact, the current one looks each part
    /// disconnected on the screen"):
    ///
    ///   [ Speed  ━━━━━━━━━●───────  %60 ]
    ///
    /// one frame: its caption, a slider dragged (or tapped) through the old track
    /// bar's steps - 0, 1, 2, 4, 6, 8, 10, 15, 20, 30 ... 90, 95, 100 % - and the
    /// percentage, which opens the keypad to type any value. Written when the
    /// slider is let go (not at every step of the drag) and when the keypad is
    /// closed with OK; the percentage in force is read back from the PLC's word, so
    /// a value set elsewhere is shown as it is. Left / right arrow keys step it.
    /// </summary>
    [ToolboxItem(true)]
    [TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class SpeedOverrideHMI : Control, IPlcBoundControl, ILocalizable
    {
        private int? _current;            // read from the PLC (null: not read yet)
        private int? _dragging;           // the value shown while the slider is held
        private bool _softEnabled = true;
        private bool _hoverValue, _hoverTrack;

        /// <summary>The old track bar's steps, in percent.</summary>
        public static readonly int[] DefaultSteps = { 0, 1, 2, 4, 6, 8, 10, 15, 20, 30, 40, 50, 60, 70, 80, 90, 95, 100 };

        [Category("Device")] [Description("The machine's own PLC number: 1 = its main PLC.")]
        public int PlcNumber { get; set; } = 1;
        [Category("Device")] [Description("The word the speed is written to and read from, in percent (FA2010: D408; the axes' manual speed: D409).")]
        public string Address { get; set; } = "";
        [Category("Input")] [Description("The steps the slider goes through, in percent (the old track bar's by default). The keypad takes any value between the first and the last.")]
        public int[] Steps { get; set; } = (int[])DefaultSteps.Clone();
        [Category("Security")] [Description("The lowest level allowed to change the speed.")]
        public SecurityLevel RequiredSecurity { get; set; } = SecurityLevel.Operator;
        [Category("Security")] [Description("true: every change goes to the log with the user's name.")]
        public bool LogAction { get; set; } = true;
        private string _key = "Lbl_Speed", _fallback = "Speed";
        [Category("Display")] [Description("The key of its caption (Lbl_Speed: Speed).")]
        public string TranslationKey { get { return _key; } set { _key = value; ApplyTranslation(); } }
        [Category("Display")] [Description("Its caption when the key has no translation.")]
        public string FallbackText { get { return _fallback; } set { _fallback = value; ApplyTranslation(); } }

        /// <summary>Asks for a value with the keypad (tests answer instead): the value now, the answer or null.</summary>
        internal static Func<SpeedOverrideHMI, int, int?> EnterValue;

        public SpeedOverrideHMI()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            Size = new Size(248, 42);
            TabStop = true;
            Cursor = Cursors.Hand;
            ApplyTranslation();
            LanguageManager.LanguageChanged += OnLanguage;
        }

        protected override Size DefaultSize { get { return new Size(248, 42); } }

        private void OnLanguage(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnLanguage), sender, e); } catch { } return; }
            ApplyTranslation();
        }

        private string _caption = "";

        public void ApplyTranslation()
        {
            _caption = LanguageManager.GetString(TranslationKey ?? "", FallbackText ?? "");
            if (!_tipWired)
            {
                _tipWired = true;
                UiStyle.HoverTip(UiStyle.Tips(), this, TipText);
            }
            Invalidate();
        }

        private bool _tipWired;

        /// <summary>What it is, its device, and how it is used - built as the tip shows.</summary>
        internal string TipText()
        {
            return _caption + (string.IsNullOrWhiteSpace(Address) ? "" : "\n" + PlcAddressHelper.StationHeader(PlcNumber) + PlcAddressHelper.Describe(PlcNumber, Address)) + "\n" +
                   (_softEnabled ? LanguageManager.GetString("Tip_SpeedSlider", "Drag the slider, or tap the percentage to type it.")
                                 : ConnectionMonitor.IsKnownDown(PlcNumber) ? ConnectionMonitor.ReasonFor(PlcNumber)
                                 : LanguageManager.GetString("Sys_ReqSec", "Requires Security Level:") + " " + RequiredSecurity);
        }

        /// <summary>The percentage shown (tests): null before the first read.</summary>
        [Browsable(false)]
        public int? Current { get { return _current; } }

        /// <summary>Whether it may be changed now (the level signed in, the PLC answering) - tests.</summary>
        [Browsable(false)]
        public bool CanChange { get { return _softEnabled; } }

        private int[] SortedSteps
        {
            get
            {
                var s = (Steps ?? DefaultSteps).Distinct().OrderBy(v => v).ToArray();
                return s.Length >= 2 ? s : DefaultSteps;
            }
        }

        // ---- the parts, from the control's size (any screen scale) -------------------

        private int Pad { get { return Math.Max(6, Height / 6); } }
        private int ValueWidth { get { return Math.Max(48, Math.Min(Width / 4, TextRenderer.MeasureText("%100", ValueFont).Width + Pad * 2)); } }
        private Font _valueFont;
        private int _valueFontFor = -1;
        /// <summary>The percentage's font, as large as the control is high (kept while the height stays).</summary>
        private Font ValueFont
        {
            get
            {
                if (_valueFont == null || _valueFontFor != Height)
                {
                    if (_valueFont != null) _valueFont.Dispose();
                    _valueFont = new Font(Font.FontFamily, Math.Max(9f, Height * 0.34f), FontStyle.Bold, GraphicsUnit.Pixel);
                    _valueFontFor = Height;
                }
                return _valueFont;
            }
        }
        private int CaptionWidth { get { return string.IsNullOrEmpty(_caption) ? 0 : Math.Min(Width / 3, TextRenderer.MeasureText(_caption, Font).Width + Pad); } }

        /// <summary>The value cell at the right end (tap: the keypad).</summary>
        internal Rectangle ValueArea { get { return new Rectangle(Width - ValueWidth - 1, 1, ValueWidth, Height - 2); } }

        /// <summary>The slider's track, between the caption and the value.</summary>
        internal Rectangle TrackArea
        {
            get
            {
                int r = Thumb / 2;
                int left = Pad + CaptionWidth + r, right = ValueArea.Left - Pad - r;
                int h = Math.Max(4, Height / 8);
                return new Rectangle(left, (Height - h) / 2, Math.Max(10, right - left), h);
            }
        }

        private int Thumb { get { return Math.Max(12, (int)(Height * 0.46)); } }

        /// <summary>Where a value stands on the track: the steps equally apart, a value between two between them.</summary>
        internal int XOf(int value)
        {
            var s = SortedSteps;
            var t = TrackArea;
            double pos;
            if (value <= s[0]) pos = 0;
            else if (value >= s[s.Length - 1]) pos = s.Length - 1;
            else
            {
                int i = 0;
                while (i < s.Length - 1 && s[i + 1] < value) i++;
                pos = i + (value - s[i]) / (double)Math.Max(1, s[i + 1] - s[i]);
            }
            return t.Left + (int)Math.Round(t.Width * pos / (s.Length - 1));
        }

        /// <summary>The step nearest a point of the track.</summary>
        internal int ValueAt(int x)
        {
            var s = SortedSteps;
            var t = TrackArea;
            double pos = (x - t.Left) / (double)Math.Max(1, t.Width) * (s.Length - 1);
            int i = (int)Math.Round(Math.Max(0, Math.Min(s.Length - 1, pos)));
            return s[i];
        }

        // ---- drawing --------------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);
            bool live = _softEnabled && _current.HasValue;
            var frame = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Round(frame, Math.Max(4, Height / 7)))
            {
                using (var back = new SolidBrush(Color.White)) g.FillPath(back, path);
                // the value cell: shaded, the keypad's place
                var v = ValueArea;
                g.SetClip(path);
                using (var cell = new SolidBrush(_hoverValue && live ? Color.FromArgb(232, 238, 246) : Color.FromArgb(244, 245, 247)))
                    g.FillRectangle(cell, v);
                g.ResetClip();
                using (var line = new Pen(Color.FromArgb(214, 218, 224))) g.DrawLine(line, v.Left, 1, v.Left, Height - 2);
                using (var pen = new Pen(Focused ? UiStyle.HoverBorderColor : Color.FromArgb(190, 196, 204))) g.DrawPath(pen, path);
            }

            // the caption
            if (CaptionWidth > 0)
                TextRenderer.DrawText(g, _caption, Font, new Rectangle(Pad, 0, CaptionWidth, Height),
                    _softEnabled ? Color.FromArgb(35, 40, 48) : Color.FromArgb(150, 156, 166),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            // the track, filled up to the value, and its thumb
            var t = TrackArea;
            int shown = _dragging ?? _current ?? 0;
            using (var empty = new SolidBrush(Color.FromArgb(222, 226, 231)))
            using (var path = Round(t, t.Height / 2)) g.FillPath(empty, path);
            if (_current.HasValue || _dragging.HasValue)
            {
                int x = XOf(shown);
                var fill = new Rectangle(t.Left, t.Top, Math.Max(t.Height, x - t.Left), t.Height);
                using (var full = new SolidBrush(live ? Color.FromArgb(76, 175, 80) : Color.FromArgb(178, 184, 192)))
                using (var path = Round(fill, t.Height / 2)) g.FillPath(full, path);
                int d = Thumb;
                var thumb = new Rectangle(x - d / 2, (Height - d) / 2, d, d);
                using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(shadow, thumb.X, thumb.Y + 1, d, d);
                using (var face = new SolidBrush(_dragging.HasValue || _hoverTrack && live ? Color.FromArgb(250, 251, 252) : Color.White))
                    g.FillEllipse(face, thumb);
                using (var rim = new Pen(live ? Color.FromArgb(96, 104, 116) : Color.FromArgb(190, 196, 204), 1.5f)) g.DrawEllipse(rim, thumb);
            }

            // the percentage
            string text = (_current.HasValue || _dragging.HasValue)
                ? "%" + shown.ToString(System.Globalization.CultureInfo.InvariantCulture) : "%-";
            TextRenderer.DrawText(g, text, ValueFont, ValueArea, live ? Color.FromArgb(25, 28, 34) : Color.FromArgb(150, 156, 166),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ---- the mouse and the keys -----------------------------------------------------

        private bool OnTrack(Point p) { return p.X >= Pad + CaptionWidth && p.X < ValueArea.Left; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !_softEnabled || !_current.HasValue) return;
            if (CanFocus) Focus();
            if (ValueArea.Contains(e.Location)) { AskValue(); return; }
            if (!OnTrack(e.Location)) return;
            Capture = true;
            _dragging = ValueAt(e.X);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hv = ValueArea.Contains(e.Location), ht = OnTrack(e.Location);
            if (hv != _hoverValue || ht != _hoverTrack) { _hoverValue = hv; _hoverTrack = ht; Invalidate(); }
            Cursor = _softEnabled && (hv || ht) ? Cursors.Hand : Cursors.Default;
            if (!_dragging.HasValue) return;
            int v = ValueAt(e.X);
            if (v != _dragging.Value) { _dragging = v; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_dragging.HasValue) return;
            int v = _dragging.Value;
            _dragging = null;
            Capture = false;
            SetTo(v);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverValue || _hoverTrack) { _hoverValue = _hoverTrack = false; Invalidate(); }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus) { Step(-1); e.Handled = true; }
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus) { Step(+1); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { AskValue(); e.Handled = true; }
        }

        /// <summary>The keypad: any whole percentage between the first step and the last.</summary>
        private void AskValue()
        {
            if (!_softEnabled || !_current.HasValue) return;
            var s = SortedSteps;
            int? typed;
            if (EnterValue != null) typed = EnterValue(this, _current.Value);
            else
            {
                typed = null;
                using (var keypad = new Kaban.UI.Forms.NumpadForm(_caption, _current.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                                                  s[0], s[s.Length - 1], allowNegative: false, allowDecimal: false))
                {
                    keypad.MaxDigits = 3;
                    keypad.PlaceBeside(this);
                    int v;
                    if (keypad.ShowDialog(FindForm()) == DialogResult.OK &&
                        int.TryParse(keypad.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v))
                        typed = v;
                }
            }
            if (typed.HasValue) SetTo(Math.Max(s[0], Math.Min(s[s.Length - 1], typed.Value)));
        }

        // ---- the PLC ----------------------------------------------------------------------

        public void UpdateState(Func<string, int> lookup, Func<SystemCondition, bool> checkGlobal)
        {
            if (string.IsNullOrWhiteSpace(Address) || lookup == null) return;
            int v = lookup(Address);
            if (_current != v) { _current = v; Invalidate(); }
            bool en = SecurityManager.HasAccess(RequiredSecurity) && !ConnectionMonitor.IsKnownDown(PlcNumber);
            if (en != _softEnabled)
            {
                _softEnabled = en;
                if (!en) { _dragging = null; Capture = false; }
                Invalidate();
            }
        }

        /// <summary>The step after (or before) the percentage in force.</summary>
        public int NextValue(int direction)
        {
            var steps = SortedSteps;
            int now = _current ?? steps[0];
            if (direction > 0) { foreach (var s in steps) if (s > now) return s; return steps[steps.Length - 1]; }
            for (int i = steps.Length - 1; i >= 0; i--) if (steps[i] < now) return steps[i];
            return steps[0];
        }

        /// <summary>One step down (-1) or up (+1), written to the PLC.</summary>
        public bool Step(int direction)
        {
            if (!_softEnabled || !_current.HasValue) return false;
            return SetTo(NextValue(direction));
        }

        /// <summary>A percentage written to the PLC (the slider let go, the keypad's OK).</summary>
        public bool SetTo(int target)
        {
            if (!_softEnabled || string.IsNullOrWhiteSpace(Address)) return false;
            if (_current.HasValue && target == _current.Value) return false;
            var drv = PlcManager.Instance.GetDriverForWrite(PlcNumber);
            if (drv == null)
            {
                MessageBox.Show(this, PlcManager.Instance.DescribeAccess(PlcNumber), "PLC Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            int rc;
            using (Kaban.UI.Helpers.WriteSource.For(this, "speed " + target + "%"))
                rc = drv.Write(Address, target);
            if (rc != 0) return false;
            _current = target;
            Invalidate();
            if (LogAction) AuditLogger.Log("speed set to " + target + "% (" + Address + ", PLC " + PlcNumber + ").");
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LanguageManager.LanguageChanged -= OnLanguage;
                if (_valueFont != null) { _valueFont.Dispose(); _valueFont = null; }
            }
            base.Dispose(disposing);
        }
    }
}
