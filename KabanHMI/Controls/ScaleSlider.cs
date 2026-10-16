using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// THE SCREEN SCALE AS THE SPEED IS SET (16 October: "change the Screen Scale setting
    /// dropdown menu to our new Speed setting control, it looks much neater"): the slider
    /// through the usual sizes - 80%, 90%, 100% ... 150% - and the percentage, which opens
    /// the keypad for any size between them (1% at a time, as the old row of the menu).
    /// Set when the slider is let go or the keypad's OK. The caption bar's menu and the
    /// Users and Preferences page (ScaleField) hold it.
    /// </summary>
    [ToolboxItem(false)]
    public sealed class ScaleSlider : PercentSlider
    {
        private double? _value;
        private double _noneFactor = 1.0;

        /// <summary>Asks for a size with the keypad (tests answer instead): the percent now, the answer or null.</summary>
        internal static Func<ScaleSlider, int, int?> EnterValue;

        public ScaleSlider()
        {
            Size = new Size(260, 30);
            AccessibleName = "Screen scale";
            _current = PercentOf(_noneFactor);
            UiStyle.HoverTip(UiStyle.Tips(), this, TipText);
        }

        protected override Size DefaultSize { get { return new Size(260, 30); } }

        /// <summary>The usual sizes, in percent: the slider's steps.</summary>
        public static int[] PresetSteps
        {
            get { return Kaban.UI.Forms.MainForm.FactorChoices.Select(PercentOf).ToArray(); }
        }

        private static int PercentOf(double factor) { return (int)Math.Round(DisplaySettings.Clamp(factor) * 100); }

        /// <summary>A caption at its left end ("" - the default: the field's caption is beside it).</summary>
        [Browsable(false)]
        public string Caption { get; set; } = "";

        /// <summary>The size shown, grey, while there is no request (a user's: the panel's).</summary>
        [Browsable(false)]
        public double NoneFactor
        {
            get { return _noneFactor; }
            set { _noneFactor = DisplaySettings.Clamp(value); if (!_value.HasValue) { _current = PercentOf(_noneFactor); Invalidate(); } }
        }

        /// <summary>The size asked for; null: no specific request (shown grey at NoneFactor).</summary>
        [Browsable(false)]
        public double? Value
        {
            get { return _value; }
            set
            {
                var v = value.HasValue ? DisplaySettings.Clamp(value.Value) : (double?)null;
                bool changed = v.HasValue != _value.HasValue || (v.HasValue && Math.Abs(v.Value - _value.Value) > 0.0005);
                _value = v;
                _current = PercentOf(v ?? _noneFactor);
                Invalidate();
                if (changed) { var h = ValueChanged; if (h != null) h(this, EventArgs.Empty); }
            }
        }

        /// <summary>The size as text: "125%", or "No specific request".</summary>
        public override string Text
        {
            get { return _value.HasValue ? DisplaySettings.Percent(_value.Value) : Kaban.UI.Forms.MainForm.NoScaleRequest; }
            set { }
        }

        /// <summary>It may be changed (the page's rights).</summary>
        [Browsable(false)]
        public bool CanChange
        {
            get { return _softEnabled; }
            set { if (_softEnabled == value) return; _softEnabled = value; if (!value) CancelDrag(); Invalidate(); }
        }

        /// <summary>The value changed (by the user, or set).</summary>
        public event EventHandler ValueChanged;
        /// <summary>The user set a size (let go, the keypad's OK, an arrow key).</summary>
        public event Action<object, double> Chosen;
        /// <summary>The slider held at a size (null: let go) - the page previews it.</summary>
        public event Action<object, double?> Previewing;

        protected override int[] StepList { get { return PresetSteps; } }
        protected override string CaptionText { get { return Caption ?? ""; } }
        protected override bool Muted { get { return !_value.HasValue; } }
        protected override string ValueText(int value) { return DisplaySettings.Percent(value / 100.0); }

        protected override bool Commit(int value)
        {
            double f = DisplaySettings.Clamp(value / 100.0);
            Value = f;
            var h = Chosen;
            if (h != null) h(this, f);
            return true;
        }

        protected override int? AskTyped(int now, int min, int max)
        {
            if (EnterValue != null) return EnterValue(this, now);
            return base.AskTyped(now, min, max);
        }

        protected override void OnDragging(int? value)
        {
            var h = Previewing;
            if (h != null) h(this, value.HasValue ? value.Value / 100.0 : (double?)null);
        }

        /// <summary>A size set from the program (tests: as the slider let go there, or the keypad).</summary>
        internal void ChooseFromUser(int percent) { Commit(percent); }

        internal string TipText()
        {
            return LanguageManager.GetString("Ttl_Magnifier", "Screen scale") + ": " + Text + "\n" +
                   LanguageManager.GetString("Tip_ScaleSlider", "Drag the slider through the usual sizes, or tap the percentage to type any size from 80% to 150%.");
        }
    }

    /// <summary>
    /// A user's screen scale on Users and Preferences: the slider, and - for a user, who may
    /// ask for no size of their own - "x", back to no specific request (the panel's size,
    /// shown grey on the slider). Everyone's (the panel's) always has a size.
    /// </summary>
    [ToolboxItem(false)]
    public sealed class ScaleField : Panel
    {
        private readonly ScaleSlider _slider = new ScaleSlider { Dock = DockStyle.Fill };
        private readonly Button _none = new Button
        {
            Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, Text = "×", TabStop = false, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold), BackColor = Color.White, ForeColor = Color.FromArgb(90, 96, 106),
            AccessibleName = "No specific request"
        };
        private bool _offerNone = true;

        public ScaleField()
        {
            Height = 30;
            BackColor = Color.White;
            _none.FlatAppearance.BorderColor = Color.FromArgb(190, 196, 204);
            _none.Width = 30;
            _none.Margin = Padding.Empty;
            Controls.Add(_slider);
            Controls.Add(new Panel { Dock = DockStyle.Right, Width = 4, BackColor = Color.Transparent, Name = "gap" });
            Controls.Add(_none);
            _none.Click += (s, e) => { if (_slider.CanChange) Value = null; };
            _slider.ValueChanged += (s, e) => { ShowNone(); var h = ValueChanged; if (h != null) h(this, EventArgs.Empty); };
            _slider.Previewing += (s, f) => { var h = Previewing; if (h != null) h(this, f); };
            UiStyle.SetTip(UiStyle.Tips(), _none, LanguageManager.GetString("Tip_ScaleNone", "No specific request: this user gets the panel's screen scale."));
            Resize += (s, e) => { _none.Width = Math.Max(20, Height); };
            ShowNone();
        }

        /// <summary>The slider (tests).</summary>
        public ScaleSlider Slider { get { return _slider; } }

        /// <summary>The "x" (tests).</summary>
        public Button NoneButton { get { return _none; } }

        public event EventHandler ValueChanged;
        public event Action<object, double?> Previewing;

        /// <summary>"No specific request" may be chosen (not for everyone's: the panel always has a size).</summary>
        public bool OfferNone
        {
            get { return _offerNone; }
            set { _offerNone = value; ShowNone(); }
        }

        /// <summary>The size asked for; null: no specific request.</summary>
        public double? Value
        {
            get { return _slider.Value; }
            set { _slider.Value = value; ShowNone(); }
        }

        /// <summary>The size shown grey while there is no request.</summary>
        public double NoneFactor { get { return _slider.NoneFactor; } set { _slider.NoneFactor = value; } }

        /// <summary>"125%", or "No specific request".</summary>
        public override string Text { get { return _slider.Text; } set { } }

        /// <summary>The size as the field shows it (the same as Text).</summary>
        public string Caption { get { return _slider.Text; } }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _slider.CanChange = Enabled;
            ShowNone();
        }

        private void ShowNone()
        {
            _none.Visible = _offerNone;
            _none.Enabled = Enabled && _slider.Value.HasValue;
            var gap = Controls["gap"];
            if (gap != null) gap.Visible = _offerNone;
        }
    }
}
