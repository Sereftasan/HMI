using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// ONE CONTROL OR TWO?
    ///
    /// Two, sharing this base - and the reason is what a wrong answer costs.
    ///
    /// A single control with an "Editable" switch would carry Minimum, Maximum,
    /// Step and the arrow buttons on every instance, including the hundred that
    /// only ever display a servo load. Half its property grid would do nothing,
    /// and a property that does nothing on most instances is one somebody
    /// eventually sets on the wrong one. Worse, an editable meter WRITES TO THE
    /// PLC: making that a checkbox away from a read-only monitor means one
    /// mis-set flag turns a display into a control surface on a live machine.
    ///
    /// So: MeterHMI monitors, MeterInputHMI commands, and this base holds the
    /// part that genuinely is common - the scale, the colour bands and the
    /// drawing. They look identical because they should; they differ where it
    /// matters, which is whether they can move anything.
    /// </summary>

    // The Properties window in the order of the work (see PropertyOrder).
    [System.ComponentModel.TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public abstract class MeterBaseHMI : Control, IPlcBoundControl, UiStyle.IDrawsOwnHoverBorder
    {
        [Category("  PLC")] public int PlcNumber { get; set; }
        [Category("  PLC")] public string Address { get; set; }

        [Category(" Value"), Description("Raw-to-display divisor.")]
        public double Divisor { get; set; }

        [Category(" Value")] public int DecimalPlaces { get; set; }

        [Category(" Value"), Description("Text after the value: %, mm, bar ...")]
        public string Suffix { get; set; }

        [Category(" Value"), Description("Display value at the left end of the bar.")]
        public double ScaleMinimum { get; set; }

        [Category(" Value"), Description("Display value that fills the bar.")]
        public double ScaleMaximum { get; set; }

        // ---- colour bands -------------------------------------------------
        // TWO DIFFERENT SHAPES OF JUDGEMENT, not one threshold pair.
        //
        //   Rising   a servo current: more is worse. Green to 60, amber to 80,
        //            red beyond.
        //   Window   a reference Z-signal distance: the value should sit in the
        //            MIDDLE of a motor revolution. Both ends are bad - stopping
        //            at 2% or at 98% means the next reference could land on the
        //            other side of the pulse - so red 0-10 and 90-100, amber
        //            10-20 and 80-90, green 20-80.
        //
        // A single "warning/alarm" pair cannot express Window at all, and forcing
        // it to would put green at one end of a scale where one end is the worst
        // place to be.
        public enum BandShape
        {
            /// <summary>No banding: one neutral colour.</summary>
            None,
            /// <summary>High is bad. Uses HighWarning and HighAlarm.</summary>
            Rising,
            /// <summary>Low is bad. Uses LowWarning and LowAlarm.</summary>
            Falling,
            /// <summary>Both ends are bad; the middle is the target.</summary>
            Window
        }

        [Category(" Bands"), Description("Which way the value is judged.")]
        public BandShape Bands { get; set; }

        [Category(" Bands"), Description("Window/Falling: at or below this it is RED.")]
        public double LowAlarm { get; set; }

        [Category(" Bands"), Description("Window/Falling: at or below this it is AMBER.")]
        public double LowWarning { get; set; }

        [Category(" Bands"), Description("Window/Rising: at or above this it is AMBER.")]
        public double HighWarning { get; set; }

        [Category(" Bands"), Description("Window/Rising: at or above this it is RED.")]
        public double HighAlarm { get; set; }

        /// <summary>Servo current / load: green to 60, amber to 80, red beyond.</summary>
        public void UseCurrentBands()
        {
            Bands = BandShape.Rising;
            HighWarning = 60; HighAlarm = 80;
            ScaleMinimum = 0; ScaleMaximum = 100;
            Suffix = "%"; DecimalPlaces = 0;
        }

        /// <summary>
        /// Reference Z-signal distance: the target is the middle of the
        /// revolution, so both ends are red.
        /// </summary>
        public void UseZDistanceBands()
        {
            Bands = BandShape.Window;
            LowAlarm = 10; LowWarning = 20; HighWarning = 80; HighAlarm = 90;
            ScaleMinimum = 0; ScaleMaximum = 100;
            Suffix = "%"; DecimalPlaces = 0;
        }

        [Category("Appearance")] public string Caption { get; set; }
        [Category("Appearance"), Description("Show the numeric value inside the bar.")]
        public bool ShowValue { get; set; }

        protected double _display;
        protected bool _hasValue;

        private readonly ToolTip _tip = UiStyle.Tips();

        protected MeterBaseHMI()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            PlcNumber = 1;
            Address = "";
            Divisor = 1;
            DecimalPlaces = 0;
            Suffix = "%";
            ScaleMinimum = 0;
            ScaleMaximum = 100;
            Bands = BandShape.None;
            LowAlarm = 10; LowWarning = 20;
            HighWarning = 80; HighAlarm = 90;
            Caption = "";
            ShowValue = true;
            Size = new Size(120, 26);
            Font = UiStyle.Small;
        }

        /// <summary>The value as the operator reads it, after the divisor.</summary>
        [Browsable(false)]
        public double DisplayValue { get { return _display; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_wired)
            {
                // HOVER, like every other control: the outline turns blue under the
                // mouse. Meters had none, so beside fields that answered the mouse
                // they read as painted-on rather than as something with a tooltip.
                _hovering = UiStyle.HoverBorder(this);
                _wired = true;
            }
            ConnectionMonitor.ConnectionChanged += OnConnectionChanged;
            RefreshTip();          // present before the first scan, not after it
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            ConnectionMonitor.ConnectionChanged -= OnConnectionChanged;
            base.OnHandleDestroyed(e);
        }

        private bool _wired;
        private Func<bool> _hovering = () => false;

        /// <summary>True while this meter's PLC is known not to be answering.</summary>
        protected bool IsStale { get { return ConnectionMonitor.IsKnownDown(PlcNumber); } }

        private void OnConnectionChanged(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke((Action)(() => OnConnectionChanged(sender, e))); return; }
            RefreshTip();
            Invalidate();
        }

        public virtual void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            if (readPlc == null || string.IsNullOrEmpty(Address)) return;
            double v = readPlc(Address) / (Divisor <= 0 ? 1 : Divisor);
            if (_hasValue && Math.Abs(v - _display) < 1e-9) return;
            _display = v;
            _hasValue = true;
            RefreshTip();
            Invalidate();
        }

        /// <summary>
        /// The device and its live value, on hover.
        ///
        /// A standard progress bar cannot say what it is showing, so a bar reading
        /// 60% is a number with no provenance - and the first question anyone asks
        /// about a meter that looks wrong is "which device is that?".
        /// </summary>
        protected void RefreshTip()
        {
            // SHOWN BY THE METER ITSELF, built as it shows (UiStyle.HoverTip): a
            // tip registered with the program's one tooltip is dropped when the
            // window that tooltip belongs to closes - after a keypad or a message
            // box the meters had none.
            if (_tipWired) return;
            _tipWired = true;
            UiStyle.HoverTip(_tip, this, TooltipText);
        }

        private bool _tipWired;

        /// <summary>The tip as it is now (tests).</summary>
        internal string TooltipText()
        {
            if (string.IsNullOrEmpty(Address)) return "";
            // DEVICE AND VALUE, nothing else. The row already names the axis and
            // the column heading already says what the reading is; repeating both
            // in the tooltip is text to read past before reaching the one thing
            // the operator hovered for.
            // ADDRESS ONLY. The value is printed inside the bar already, and a
            // tooltip that repeats it is telling the operator something they are
            // looking at while it covers the thing beside it.
            string text = PlcAddressHelper.StationHeader(PlcNumber) +
                          PlcAddressHelper.Describe(PlcNumber, Address);
            // A stale reading says so first: the address alone would suggest the
            // number beside it is current.
            if (IsStale) text = ConnectionMonitor.ReasonFor(PlcNumber) + "\r\n" + text;
            return text;
        }

        protected string Text_Value()
        {
            if (!_hasValue) return "--";
            return _display.ToString("F" + Math.Max(0, DecimalPlaces)) +
                   (string.IsNullOrEmpty(Suffix) ? "" : " " + Suffix);
        }

        protected double Fraction
        {
            get
            {
                double lo = ScaleMinimum, hi = ScaleMaximum;
                if (Math.Abs(hi - lo) < 1e-9) return 0;
                double f = (_display - lo) / (hi - lo);
                return Math.Max(0, Math.Min(1, f));
            }
        }

        public static readonly Color GreenBand = Color.FromArgb(0, 150, 80);
        public static readonly Color AmberBand = Color.FromArgb(215, 165, 40);
        public static readonly Color RedBand   = Color.FromArgb(200, 60, 60);

        /// <summary>The band a value falls in. Public so it can be tested.</summary>
        public Color BandColourAt(double v) { return ColourAt(v); }

        protected static readonly Color Green = Color.FromArgb(0, 150, 80);
        protected static readonly Color Amber = Color.FromArgb(215, 165, 40);
        protected static readonly Color Red   = Color.FromArgb(200, 60, 60);
        protected static readonly Color Plain = Color.FromArgb(30, 120, 220);

        /// <summary>The band a display value falls in.</summary>
        protected Color ColourAt(double v)
        {
            switch (Bands)
            {
                case BandShape.Rising:
                    if (v >= HighAlarm)   return Red;
                    if (v >= HighWarning) return Amber;
                    return Green;

                case BandShape.Falling:
                    if (v <= LowAlarm)   return Red;
                    if (v <= LowWarning) return Amber;
                    return Green;

                case BandShape.Window:
                    if (v <= LowAlarm || v >= HighAlarm)     return Red;
                    if (v <= LowWarning || v >= HighWarning) return Amber;
                    return Green;

                default:
                    return Plain;
            }
        }

        protected Color BarColour() { return ColourAt(_display); }

        /// <summary>Mixes a colour toward white by the given amount.</summary>
        private static Color Lighten(Color c, float amount)
        {
            return Color.FromArgb(
                (int)(c.R + (255 - c.R) * amount),
                (int)(c.G + (255 - c.G) * amount),
                (int)(c.B + (255 - c.B) * amount));
        }

        /// <summary>Mixes a colour toward black, for text that must stay legible.</summary>
        private static Color Darken(Color c, float amount)
        {
            return Color.FromArgb((int)(c.R * (1 - amount)),
                                  (int)(c.G * (1 - amount)),
                                  (int)(c.B * (1 - amount)));
        }

        protected void DrawBar(Graphics g, Rectangle area)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var back = new SolidBrush(Color.FromArgb(242, 244, 247)))
                g.FillRectangle(back, area);

            var band = BarColour();

            if (_hasValue)
            {
                // A LIGHT wash, not the solid band colour: the number sits on top of
                // this and has to stay readable once the bar covers it.
                int w = (int)(area.Width * Fraction);
                if (w > 0)
                    using (var fill = new SolidBrush(Lighten(band, 0.70f)))
                        g.FillRectangle(fill, area.X, area.Y, w, area.Height);
            }

            // THE WHOLE BAR carries the colour, not a zone behind it. The colour
            // is the warning: an operator glancing across sixteen axes should see
            // one red bar, not judge a fill against a painted background.

            // A NEUTRAL border in every state. Colouring it as well as the number
            // put two warnings on one control, and across a page of them the screen
            // became a field of colour with nothing standing out.
            if (IsStale) UiStyle.DrawStale(g, area);
            using (var pen = new Pen(UiStyle.BorderFor(_hovering(), Color.FromArgb(205, 210, 217))))
                g.DrawRectangle(pen, area.X, area.Y, area.Width - 1, area.Height - 1);

            // ...and so does the NUMBER, but only while the bar is too short to sit
            // behind it. Once the fill reaches the digits a coloured number on a
            // coloured wash is harder to read than a black one.
            // 10%, not 30%. Below a tenth there is effectively no bar to read the
            // colour from, so the number carries it; past that the bar says it
            // already and a coloured number is one warning too many.
            var textColour = (_hasValue && Bands != BandShape.None && Fraction < 0.10)
                             ? Darken(band, 0.25f) : Color.FromArgb(35, 40, 48);

            if (ShowValue)
                TextRenderer.DrawText(g, Text_Value(), Font, area, textColour,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
        }
    }

    /// <summary>
    /// A read-only meter: servo load, motor current, Z-signal distance, air
    /// pressure. Shows a proportional bar, the real value, optional colour bands
    /// and a tooltip naming the device.
    /// </summary>
    [ToolboxItem(true)]
    public class MeterHMI : MeterBaseHMI, UiStyle.IDisplayOnly
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            DrawBar(e.Graphics, new Rectangle(0, 0, Width, Height));
        }
    }

    /// <summary>
    /// A meter the operator can SET - a speed override, a feed percentage.
    ///
    /// Same bar, plus up/down arrows and typed entry. The arrows exist because on
    /// a panel, nudging a speed by 5% should be one tap: the alternative is an
    /// on-screen keyboard over the value the operator is trying to watch.
    ///
    /// Writes are clamped to Minimum/Maximum before they leave, so a stuck arrow
    /// or a mistyped number cannot command a speed the machine cannot take.
    /// </summary>
    [ToolboxItem(true)]
    public class MeterInputHMI : MeterBaseHMI
    {
        [Category(" Limits"), Description("Lowest value the operator may set.")]
        public double Minimum { get; set; }

        [Category(" Limits"), Description("Highest value the operator may set.")]
        public double Maximum { get; set; }

        [Category(" Limits"), Description("How much one arrow press moves the value.")]
        public double Step { get; set; }

        [Category(" Security")] public SecurityLevel RequiredSecurity { get; set; }
        [Category(" Security")] public bool LogAction { get; set; }

        private const int ArrowW = 26;
        private Rectangle _up, _down;
        private bool _hoverUp, _hoverDown;

        public MeterInputHMI()
        {
            Minimum = 0;
            Maximum = 100;
            Step = 5;
            RequiredSecurity = SecurityLevel.Operator;
            LogAction = true;
            Size = new Size(200, 30);
            Cursor = Cursors.Hand;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _down = new Rectangle(0, 0, ArrowW, Height);
            _up = new Rectangle(Width - ArrowW, 0, ArrowW, Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            DrawBar(g, new Rectangle(ArrowW, 0, Math.Max(1, Width - ArrowW * 2), Height));
            DrawArrow(g, _down, false, _hoverDown);
            DrawArrow(g, _up, true, _hoverUp);
        }

        private void DrawArrow(Graphics g, Rectangle r, bool up, bool hover)
        {
            using (var back = new SolidBrush(hover ? Color.FromArgb(232, 238, 246)
                                                   : Color.FromArgb(246, 247, 249)))
                g.FillRectangle(back, r);
            using (var pen = new Pen(hover ? UiStyle.HoverBorderColor : Color.FromArgb(205, 210, 217)))
                g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);

            // Triangles rather than "+/-": the same shape language as every other
            // direction control in the program.
            int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, s = Math.Min(6, r.Height / 4);
            var pts = up
                ? new[] { new Point(cx, cy - s), new Point(cx - s, cy + s), new Point(cx + s, cy + s) }
                : new[] { new Point(cx, cy + s), new Point(cx - s, cy - s), new Point(cx + s, cy - s) };
            using (var brush = new SolidBrush(Color.FromArgb(60, 66, 74)))
                g.FillPolygon(brush, pts);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool u = _up.Contains(e.Location), d = _down.Contains(e.Location);
            if (u != _hoverUp || d != _hoverDown) { _hoverUp = u; _hoverDown = d; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverUp || _hoverDown) { _hoverUp = _hoverDown = false; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (_up.Contains(e.Location)) Nudge(+Step);
            else if (_down.Contains(e.Location)) Nudge(-Step);
            else TypeValue();
        }

        private void Nudge(double delta)
        {
            Write(_display + delta);
        }

        private void TypeValue()
        {
            using (var dlg = new Forms.InputForm(
                       string.IsNullOrEmpty(Caption) ? Address : Caption,
                       "(" + Minimum + " .. " + Maximum + ")",
                       _display.ToString("F" + Math.Max(0, DecimalPlaces))))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                double v;
                if (double.TryParse(dlg.InputText, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.CurrentCulture, out v))
                    Write(v);
            }
        }

        private void Write(double displayValue)
        {
            if (!SecurityManager.HasAccess(RequiredSecurity))
            {
                MessageBox.Show(this,
                    LanguageManager.GetString("Msg_NoPermission",
                        "Your user level is not high enough for this action."),
                    Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // CLAMPED BEFORE IT LEAVES. A held arrow or a mistyped figure must not
            // reach the machine as a speed it cannot take.
            double v = Math.Max(Minimum, Math.Min(Maximum, displayValue));

            var drv = PlcManager.Instance.GetDriver(PlcNumber);
            if (drv == null)
            {
                MessageBox.Show(this, PlcManager.Instance.DescribeAccess(PlcNumber),
                    Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int raw = (int)Math.Round(v * (Divisor <= 0 ? 1 : Divisor));
            using (Kaban.UI.Helpers.WriteSource.For(this, "set to " + v.ToString(System.Globalization.CultureInfo.InvariantCulture))) drv.Write(Address, raw);
            _display = v; _hasValue = true;
            Invalidate();

            if (LogAction)
                AuditLogger.Log("set " + (string.IsNullOrEmpty(Caption) ? Address : Caption) +
                                " to " + v.ToString("F" + Math.Max(0, DecimalPlaces)) + " " + Suffix + ".");
        }
    }

    /// <summary>
    /// A drive alarm / warning code, shown the way the drive manual prints it.
    ///
    /// These words are HEXADECIMAL with the last digit as a sub-code, so 0x502
    /// is alarm 50 detail 2 and prints "50.2"; 0x1A5 is "1A.5". Showing the raw
    /// decimal (1282, 421) means the operator cannot find the entry in the
    /// manual at all, which is the only thing the code is for.
    ///
    /// Zero is drawn muted rather than as "0.0" - no fault is the normal state
    /// and should not draw the eye across a page of sixteen axes.
    /// </summary>
    [ToolboxItem(true)]
    // The Properties window in the order of the work (see PropertyOrder).
    [System.ComponentModel.TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class HexCodeHMI : Control, IPlcBoundControl, UiStyle.IDisplayOnly, UiStyle.IManagesOwnCursor, UiStyle.IDrawsOwnHoverBorder
    {
        [Category("  PLC")] public int PlcNumber { get; set; }
        [Category("  PLC")] public string Address { get; set; }
        [Category("Appearance")] public string Caption { get; set; }

        /// <summary>
        /// What a non-zero code MEANS here, which decides its colour.
        ///
        /// An alarm has stopped the machine; a warning has not. Colouring both the
        /// same makes an operator walk to a machine that is still running, and -
        /// worse - makes a real alarm look like something they have learned to
        /// ignore.
        /// </summary>
        public enum CodeSeverity { Alarm, Warning }

        [Category("Appearance"), Description("Alarm codes show red, warnings amber.")]
        public CodeSeverity Severity { get; set; }

        /// <summary>
        /// Which code this is, which decides how it is printed and which manual
        /// answers it. None prints the servo way ("50.2") with no help behind it.
        /// </summary>
        [Category("Appearance"), DefaultValue(AxisCodeKind.None),
         Description("Servo alarm: printed 16.3, help from the servo manual. Axis alarm / warning: printed 1904H, help from the motion module's manual.")]
        public AxisCodeKind CodeKind { get; set; }

        /// <summary>The axis's name, for the help's title.</summary>
        [Browsable(false)]
        public string UnitName { get; set; }

        /// <summary>
        /// True while the code is not zero and its manual has an entry to open:
        /// then the cell is a link - hand pointer, the DIGITS BLINKING, and a click
        /// opens that code's entry. A zero code is inert, as before. (It had a
        /// small "?" in its corner: too small to read, it was noise, not a sign.)
        /// </summary>
        [Browsable(false)]
        public bool HasHelp { get { return _raw > 0 && _help != null; } }

        private TroubleshootingCatalog.HelpDocument _help;
        private bool _warningCode;

        private int _raw = -1;
        private readonly ToolTip _tip = UiStyle.Tips();

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_wired) { _hovering = UiStyle.HoverBorder(this); _wired = true; }
            ConnectionMonitor.ConnectionChanged += OnConnectionChanged;
            RefreshTip();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            ConnectionMonitor.ConnectionChanged -= OnConnectionChanged;
            base.OnHandleDestroyed(e);
        }

        private bool _wired;
        private Func<bool> _hovering = () => false;
        private bool IsStale { get { return ConnectionMonitor.IsKnownDown(PlcNumber); } }

        private void OnConnectionChanged(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke((Action)(() => OnConnectionChanged(sender, e))); return; }
            RefreshTip();
            Invalidate();
        }

        private void RefreshTip()
        {
            // Shown by the cell itself, built as it shows - see MeterBaseHMI.
            if (_tipWired) return;
            _tipWired = true;
            UiStyle.HoverTip(_tip, this, TooltipText);
        }

        private bool _tipWired;

        /// <summary>The tip as it is now (tests).</summary>
        internal string TooltipText()
        {
            // ADDRESS ONLY. The code is printed in the cell already. When the PLC
            // is not answering, the reason comes first - see MeterBaseHMI.
            var text = PlcAddressHelper.StationHeader(PlcNumber) +
                       (string.IsNullOrEmpty(Address) ? "(no device)"
                                                      : PlcAddressHelper.Describe(PlcNumber, Address));
            // What the code means, and that a click explains it - above the device.
            if (HasHelp)
                text = _help.Title + "\r\n" + LanguageManager.GetString("Tip_HelpBadge", "Click for help") + "\r\n\r\n" + text;
            if (IsStale) text = ConnectionMonitor.ReasonFor(PlcNumber) + "\r\n" + text;
            return text;
        }

        public HexCodeHMI()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            PlcNumber = 1;
            Address = "";
            Caption = "";
            Size = new Size(72, 28);
            Font = UiStyle.Mono;
        }

        /// <summary>
        /// "50.2" for 0x502, "1A.5" for 0x1A5.
        ///
        /// ZERO IS BLANK, not "0.0". Zero means there is no fault, and a page of
        /// sixteen axes printing "0.0" forty-eight times is forty-eight things to
        /// read past before finding the one code that matters. An empty cell says
        /// "nothing here" faster than any number can.
        /// </summary>
        public static string Format(int raw)
        {
            return AxisCodeHelp.Format(AxisCodeKind.None, raw);
        }

        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            if (readPlc == null || string.IsNullOrEmpty(Address)) return;
            int v = readPlc(Address);
            // ONE 16-bit word: the scanner reads a pair for widths it cannot know,
            // and the neighbouring word must not turn 1904H into 51904H.
            if (CodeKind != AxisCodeKind.None) v &= 0xFFFF;
            if (v == _raw) return;
            _raw = v;
            // Looked up when the code CHANGES, not on every scan.
            _help = CodeKind != AxisCodeKind.None && v > 0 ? AxisCodeHelp.Document(CodeKind, v, UnitName) : null;
            // A servo WARNING (E6.1 forced stop...) in the servo column is amber
            // too: the manual says the axis has not been stopped by it.
            _warningCode = Severity == CodeSeverity.Warning || AxisCodeHelp.IsWarning(CodeKind, v);
            Cursor = HasHelp ? Cursors.Hand : Cursors.Default;
            if (HasHelp) StartBlinking(this); else StopBlinking(this);
            RefreshTip();
            Invalidate();
        }

        // ONE timer for every blinking code cell: a page of sixteen axes has
        // forty-eight of them.
        private static readonly List<HexCodeHMI> _blinking = new List<HexCodeHMI>();
        private static Timer _blink;
        private static bool _phase = true;

        /// <summary>True in the half of the blink the digits are drawn strong (for tests).</summary>
        public static bool BlinkPhase { get { return _phase; } set { _phase = value; } }

        private static void StartBlinking(HexCodeHMI c)
        {
            if (!_blinking.Contains(c)) _blinking.Add(c);
            if (_blink == null)
            {
                _blink = new Timer { Interval = 500 };
                _blink.Tick += (s, e) =>
                {
                    _phase = !Kaban.Core.Helpers.ViewPreferences.BlinkingOn || !_phase;     // blinking off: strong, steady
                    foreach (var x in _blinking.ToArray())
                    {
                        if (x.IsDisposed) { _blinking.Remove(x); continue; }
                        x.Invalidate();
                    }
                };
            }
            _blink.Start();
        }

        private static void StopBlinking(HexCodeHMI c)
        {
            _blinking.Remove(c);
            if (_blinking.Count == 0 && _blink != null) { _blink.Stop(); _phase = true; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) StopBlinking(this);
            base.Dispose(disposing);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!HasHelp || IsStale) return;
            // what the axis was doing when the code came (16 October): that page first, the code's entry second
            var moment = AlarmGeneration.AxisStepsForCode(PlcNumber, Address, _raw);
            // the code's entry with the moment's hint at its top (17 October)
            _help.MomentHint = moment != null ? moment.AxisMoment : null;
            if (moment != null)
            {
                var docs = new System.Collections.Generic.List<TroubleshootingCatalog.HelpDocument> { moment, _help };
                var menu = Kaban.UI.Screens.MachineAlarmScreen.BuildHelpMenu(docs, d => Kaban.UI.Helpers.AlarmHelpLinks.Open(FindForm(), d), null, FindForm());
                UiStyle.ShowPopup(menu, this, new Point(0, Height));
                return;
            }
            Kaban.UI.Helpers.AlarmHelpLinks.Open(FindForm(), _help);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            bool faulted = _raw > 0;

            // A plain white cell in every state. Only the BORDER and the DIGITS
            // carry the colour: a filled cell is a block of colour that dominates
            // the row it sits in, and with three code columns per axis a page of
            // them becomes unreadable long before anyone finds the fault.
            // GREYED when the code is zero, the same way a disabled entry field is
            // greyed: there is no fault, so there is nothing to read and no helper
            // document to open. It looks inert because it is.
            using (var back = new SolidBrush(faulted ? Color.White
                                                     : Color.FromArgb(243, 244, 246)))
                g.FillRectangle(back, r);
            // The border stays neutral; only the DIGITS carry the severity. The
            // number changing colour is enough to catch the eye, and a bordered
            // cell as well made three code columns per axis shout at once.
            if (IsStale) UiStyle.DrawStale(g, r);
            using (var pen = new Pen(UiStyle.BorderFor(_hovering(), Color.FromArgb(205, 210, 217))))
                g.DrawRectangle(pen, r);

            // Zero is BLACK and quiet - no fault is the normal state and must not
            // draw the eye across a page of sixteen axes.
            var colour = !faulted ? Color.FromArgb(25, 28, 34)
                       : (Severity == CodeSeverity.Warning || _warningCode) ? Color.FromArgb(190, 140, 20)
                                                          : Color.FromArgb(190, 40, 40);
            // A code with an entry to open BLINKS - strong, then faint - so the
            // row says "look here, click me" without a mark beside the number.
            // Faint = blended into the white cell: text drawing ignores transparency.
            if (HasHelp && !IsStale && !_phase)
                colour = Color.FromArgb((colour.R + 3 * 255) / 4, (colour.G + 3 * 255) / 4, (colour.B + 3 * 255) / 4);
            TextRenderer.DrawText(g, AxisCodeHelp.Format(CodeKind, _raw), Font, r, colour,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }
    }
}

