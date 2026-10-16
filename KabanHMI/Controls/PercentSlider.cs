using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// A PERCENTAGE SET IN ONE COMPACT CONTROL - the machine's speed (SpeedOverrideHMI,
    /// 14 October) and the screen scale (ScaleSlider, 16 October: "change the Screen
    /// Scale setting dropdown menu to our new Speed setting control, it looks much neater"):
    ///
    ///   [ Caption  ━━━━━━━━━●───────  %60 ]
    ///
    /// one frame: its caption, a slider dragged (or tapped) through its steps - equally
    /// apart on the track - and the percentage, which opens the keypad to type any value
    /// between the first step and the last. Set when the slider is let go (not at every
    /// step of the drag) and when the keypad is closed with OK. Left / right arrow keys
    /// step it, Enter opens the keypad. What the value is, where it is kept and who may
    /// change it is the control's own (the derived classes').
    /// </summary>
    [ToolboxItem(false)]
    public abstract class PercentSlider : Control
    {
        /// <summary>The value in force (null: not known yet - nothing to drag).</summary>
        internal int? _current;
        /// <summary>The value shown while the slider is held.</summary>
        internal int? _dragging;
        /// <summary>It may be changed now (the level signed in, the PLC answering ...).</summary>
        internal bool _softEnabled = true;
        private bool _hoverValue, _hoverTrack;

        protected PercentSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        // ---- what the derived control says ----------------------------------------------

        /// <summary>The steps the slider goes through (any order, each once).</summary>
        protected abstract int[] StepList { get; }

        /// <summary>The caption at the left end ("" for none: a field with its caption beside it).</summary>
        protected abstract string CaptionText { get; }

        /// <summary>Sets a value (the slider let go, the keypad's OK, an arrow key); false when it was not.</summary>
        protected abstract bool Commit(int value);

        /// <summary>The percentage as the cell shows it ("%60" - as the old program; the scale: "125%").</summary>
        protected virtual string ValueText(int value) { return "%" + value.ToString(System.Globalization.CultureInfo.InvariantCulture); }

        /// <summary>The value shown is not the control's own (the screen scale's "no specific request"): drawn grey, no fill.</summary>
        protected virtual bool Muted { get { return false; } }

        /// <summary>The keypad's question (null: cancelled). The derived control may answer instead (its tests).</summary>
        protected virtual int? AskTyped(int now, int min, int max)
        {
            using (var keypad = new Kaban.UI.Forms.NumpadForm(CaptionText.Length > 0 ? CaptionText : AccessibleName ?? "",
                                                              now.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                                              min, max, allowNegative: false, allowDecimal: false))
            {
                keypad.MaxDigits = Math.Max(1, max.ToString(System.Globalization.CultureInfo.InvariantCulture).Length);
                keypad.PlaceBeside(this);
                int v;
                if (keypad.ShowDialog(FindForm()) == DialogResult.OK &&
                    int.TryParse(keypad.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v))
                    return v;
            }
            return null;
        }

        /// <summary>The slider held at a value (null: let go) - the screen scale previews it.</summary>
        protected virtual void OnDragging(int? value) { }

        // ---- the steps --------------------------------------------------------------------

        internal int[] SortedSteps
        {
            get
            {
                var s = (StepList ?? new int[0]).Distinct().OrderBy(v => v).ToArray();
                return s.Length >= 2 ? s : new[] { 0, 100 };
            }
        }

        /// <summary>The step after (or before) the value in force.</summary>
        public int NextValue(int direction)
        {
            var steps = SortedSteps;
            int now = _current ?? steps[0];
            if (direction > 0) { foreach (var s in steps) if (s > now) return s; return steps[steps.Length - 1]; }
            for (int i = steps.Length - 1; i >= 0; i--) if (steps[i] < now) return steps[i];
            return steps[0];
        }

        /// <summary>One step down (-1) or up (+1).</summary>
        public bool Step(int direction)
        {
            if (!_softEnabled || !_current.HasValue) return false;
            return Commit(NextValue(direction));
        }

        // ---- the parts, from the control's size (any screen scale) -------------------

        private int Pad { get { return Math.Max(6, Height / 6); } }
        private int ValueWidth
        {
            get
            {
                var s = SortedSteps;
                return Math.Max(48, Math.Min(Width / 4, TextRenderer.MeasureText(ValueText(s[s.Length - 1]), ValueFont).Width + Pad * 2));
            }
        }
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
                    // readable in a field's row too (30 px high: 12 px)
                    _valueFont = new Font(Font.FontFamily, Math.Max(12f, Height * 0.34f), FontStyle.Bold, GraphicsUnit.Pixel);
                    _valueFontFor = Height;
                }
                return _valueFont;
            }
        }
        private int CaptionWidth
        {
            get
            {
                var c = CaptionText;
                return string.IsNullOrEmpty(c) ? 0 : Math.Min(Width / 3, TextRenderer.MeasureText(c, Font).Width + Pad);
            }
        }

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
            bool muted = Muted && !_dragging.HasValue;
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
                TextRenderer.DrawText(g, CaptionText, Font, new Rectangle(Pad, 0, CaptionWidth, Height),
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
                if (!muted)
                {
                    var fill = new Rectangle(t.Left, t.Top, Math.Max(t.Height, x - t.Left), t.Height);
                    using (var full = new SolidBrush(live ? Color.FromArgb(76, 175, 80) : Color.FromArgb(178, 184, 192)))
                    using (var path = Round(fill, t.Height / 2)) g.FillPath(full, path);
                }
                int d = Thumb;
                var thumb = new Rectangle(x - d / 2, (Height - d) / 2, d, d);
                using (var shadow = new SolidBrush(Color.FromArgb(muted ? 18 : 40, 0, 0, 0))) g.FillEllipse(shadow, thumb.X, thumb.Y + 1, d, d);
                using (var face = new SolidBrush(_dragging.HasValue || _hoverTrack && live ? Color.FromArgb(250, 251, 252) : Color.White))
                    g.FillEllipse(face, thumb);
                using (var rim = new Pen(live && !muted ? Color.FromArgb(96, 104, 116) : Color.FromArgb(190, 196, 204), 1.5f)) g.DrawEllipse(rim, thumb);
            }

            // the percentage
            string text = (_current.HasValue || _dragging.HasValue) ? ValueText(shown) : "%-";
            TextRenderer.DrawText(g, text, ValueFont, ValueArea,
                    live && !muted ? Color.FromArgb(25, 28, 34) : Color.FromArgb(150, 156, 166),
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
            OnDragging(_dragging);
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
            if (v != _dragging.Value) { _dragging = v; OnDragging(v); Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_dragging.HasValue) return;
            int v = _dragging.Value;
            _dragging = null;
            Capture = false;
            OnDragging(null);
            Commit(v);
            Invalidate();
        }

        /// <summary>The slider let go without a value (it lost the mouse, or may not be changed any more).</summary>
        protected void CancelDrag()
        {
            if (!_dragging.HasValue) return;
            _dragging = null;
            Capture = false;
            OnDragging(null);
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
        internal void AskValue()
        {
            if (!_softEnabled || !_current.HasValue) return;
            var s = SortedSteps;
            int? typed = AskTyped(_current.Value, s[0], s[s.Length - 1]);
            if (typed.HasValue) Commit(Math.Max(s[0], Math.Min(s[s.Length - 1], typed.Value)));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _valueFont != null) { _valueFont.Dispose(); _valueFont = null; }
            base.Dispose(disposing);
        }
    }
}
