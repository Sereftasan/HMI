using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Design;
using Kaban.Core.Enums;
using Kaban.Core.Models;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.UI.Plc;
using Kaban.UI.Forms;
using NCalc;

namespace Kaban.UI.Controls
{
    [ToolboxBitmap(typeof(TextBox))]
    [TypeConverter(typeof(NumericInputSorter))]
    public class NumericInputHMI : BaseNumericHMI
    {
        [Category("   Limits")] [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)] public DynamicValue MinLimit { get; set; } = new DynamicValue();
        [Category("   Limits")] [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)] public DynamicValue MaxLimit { get; set; } = new DynamicValue();

        /// <summary>
        /// The value is an ANGLE in degrees (see NumericLimits): never below 0 or
        /// above 360, and a MinLimit above the MaxLimit is a range THROUGH zero -
        /// 270 ... 360 / 0 ... 45.
        ///
        /// Set, not guessed from a "°" suffix: some pages carry that suffix on
        /// fields that are no angle at all (an encoder's pulses per turn, a
        /// profile length), and holding those to 0 - 360 would refuse their
        /// settings. AxisSettingHMI sets it on the rows of an angle axis.
        /// </summary>
        [Category("   Limits")]
        [DefaultValue(false)]
        [Description("An angle in degrees: 0 - 360 always, and a MinLimit above the MaxLimit is a range through 0 " +
                     "(270 ... 360 / 0 ... 45).")]
        public bool IsAngle { get; set; }

        // THE FORMULAS ARE THE TWO DIRECTIONS OF ONE SCALING.
        //
        // ReadFormula turns the PLC's raw value into the number shown, WriteFormula
        // turns the number typed into the raw value written - "(A - 1) / B" and its
        // inverse "(A * B) + 1". Either one set REPLACES the Divisor for its
        // direction. ReadFormula used to touch only a Tag-based limit, so a field
        // given one still showed raw / Divisor, and WriteFormula's result was then
        // multiplied by the Divisor as well - two scalings where one was meant.
        [Category("   Math")]
        [DefaultValue("val")]
        [RefreshProperties(RefreshProperties.All)]
        [Description("How the PLC's raw value becomes the number shown, e.g. '(A - 1) / B'. A (or val, x) is the raw value; " +
                     "B, C ... are MathVariables. Set, it replaces the Divisor for the value AND for Tag-based limits - " +
                     "give WriteFormula the inverse. NCalc syntax. Default 'val' = raw / Divisor.")]
        public string ReadFormula { get; set; } = "val";

        [Category("   Math")]
        [DefaultValue("val")]
        [RefreshProperties(RefreshProperties.All)]
        [Description("How the number typed becomes the raw value written to the PLC, e.g. '(A * B) + 1'. A (or val, x) is " +
                     "the number typed (in the reference unit); B, C ... are MathVariables. Set, it replaces the Divisor: " +
                     "the result is written as it is. NCalc syntax. Default 'val' = typed x Divisor.")]
        public string WriteFormula { get; set; } = "val";

        [Category("   Math")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [RefreshProperties(RefreshProperties.All)]
        [Description("The other names the formulas use - B, C ... - each a constant or a PLC device to read. " +
                     "A (val, x) is the value itself and needs no variable.")]
        public List<MathVariable> MathVariables { get; } = new List<MathVariable>();

        /// <summary>
        /// What is wrong with the formulas, checked as they are typed - "B is not
        /// defined: add a MathVariable named B". NCalc only finds a missing name
        /// when the formula runs: at the operator's ENT, as "Parameter was not
        /// defined".
        /// </summary>
        [Category("   Math")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Description("The formulas checked: every name defined, each formula readable, the two the inverse of each other.")]
        public string FormulaCheck
        {
            get
            {
                var p = FormulaProblems();
                return p.Count == 0 ? "OK" : string.Join("  |  ", p);
            }
        }

        /// <summary>The last read-formula failure, for the admin tooltip (empty when it worked).</summary>
        private string _readFormulaError = "";

        [Category("   Triggers")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(UITypeEditor))]
        public SystemCondition EnableConditions { get; set; } = SystemCondition.None;

        [Category("   Triggers")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(PlcTriggerSorter))] // <--- THIS FIXES THE CRASH & ALLOWS EXPANSION
        public PlcTrigger EnableTrigger { get; set; } = new PlcTrigger();

        [Category("  System")] public bool ConfirmWrite { get; set; } = false;
        [Category("  System")] public bool OutOfLimitsWarning { get; set; } = false;

        /// <summary>
        /// Draws a slider on the left and the value on the right.
        ///
        /// For the percentages an operator adjusts while watching the machine - a
        /// speed override, a feed rate. Dragging is one gesture where typing is a
        /// keypad, a number and a confirm, and the bar also shows at a glance how
        /// far through its range the value sits, which a number alone never does.
        ///
        /// The numeric part stays exactly what it was: clicking it opens the same
        /// entry as any other field. The bar is an addition, not a replacement.
        /// </summary>
        [Category("   Appearance"), Description("Show a draggable bar beside the value.")]
        public bool BarView
        {
            get { return _barView; }
            set { if (_barView == value) return; _barView = value; Invalidate(); }
        }
        private bool _barView;

        // No BarMinimum / BarMaximum. The field already has MinLimit and MaxLimit,
        // and a second pair of bounds is a second thing to keep in step - set the
        // travel in one place and the bar moves between the same ends the keypad
        // will accept.

        /// <summary>How much one arrow press moves the value.</summary>
        [Category("   Appearance")] public double BarStep { get; set; } = 5;


        // A step button at each END with the value between them - the shape used
        // on the machines already. The previous version put a slider, the number
        // and two stacked arrows in one row: at any realistic width the number was
        // unreadable and the arrows were too small to hit with a gloved finger.
        private int StepW { get { return Math.Max(26, Math.Min(40, Height + 4)); } }

        private Rectangle MinusArea { get { return new Rectangle(1, 1, StepW, Height - 2); } }
        private Rectangle PlusArea  { get { return new Rectangle(Width - StepW - 1, 1, StepW, Height - 2); } }
        private Rectangle ValueArea
        {
            get { return new Rectangle(StepW + 3, 1, Math.Max(10, Width - StepW * 2 - 6), Height - 2); }
        }

        private bool _softEnabled = false; // FAIL-CLOSED until the scanner evaluates it
        private string _disableReason = "";

        /// <summary>ADMIN view: base wiring plus the write-specific detail.</summary>
        protected override string BuildAdminTooltip()
        {
            var sb = new System.Text.StringBuilder(base.BuildAdminTooltip());
            sb.Append("\nWrite Condition:\t\t").Append(EnableConditions);

            string trig = (EnableTrigger != null && !string.IsNullOrEmpty(EnableTrigger.MonitorAddress))
                          ? EnableTrigger.ToString() : "None";
            sb.Append("\nEnable Trigger:\t\t").Append(trig);

            if (MinLimit != null && MinLimit.Type != LimitSourceType.None)
                sb.Append("\nMinimum Value:\t\t").Append(MinLimit);
            if (MaxLimit != null && MaxLimit.Type != LimitSourceType.None)
                sb.Append("\nMaximum Value:\t\t").Append(MaxLimit);
            if (IsAngle)
                sb.Append("\nAngle:\t\t\t0 - 360 (a minimum above the maximum: through 0)");
            if (!NumericFormula.IsIdentity(ReadFormula))
                sb.Append("\nRead Formula:\t\t").Append(ReadFormula);
            if (!NumericFormula.IsIdentity(WriteFormula))
                sb.Append("\nWrite Formula:\t\t").Append(WriteFormula);
            if (MathVariables.Count > 0)
                sb.Append("\nMath Variables:\t\t").Append(string.Join(", ", MathVariables.Where(v => v != null)));
            var problems = FormulaProblems();
            // What went wrong at the last scan - unless the check above already says it.
            if (_readFormulaError.Length > 0 && !problems.Any(p => p.StartsWith("ReadFormula")))
                problems.Insert(0, _readFormulaError);
            foreach (var p in problems)
                sb.Append("\nFormula problem:\t\t").Append(p);

            if (!_softEnabled && !string.IsNullOrEmpty(_disableReason))
                sb.Append("\n\n").Append(_disableReason);

            return sb.ToString();
        }

        /// <summary>OPERATOR view: the translated reason the input is blocked.</summary>
        protected override string BuildOperatorTooltip()
        {
            if (!_softEnabled && this.Visible && !string.IsNullOrEmpty(_disableReason))
            {
                string disabled = LanguageManager.GetString("Sys_Disabled", "Action Disabled.");
                string reason = LanguageManager.GetString("Sys_Reason", "Reason:");
                return $"{disabled}\n{reason} {_disableReason}";
            }
            // What may be typed: the operator's first question at a field with limits.
            string tip = base.BuildOperatorTooltip() ?? "";
            string range = AllowedRangeText();
            if (range.Length > 0) tip = tip.Length > 0 ? tip + "\n" + range : range;
            return tip;
        }

        /// <summary>"Allowed: 0.00 - 6500.00 mm" from the limits as they read now; "" when there are none.</summary>
        internal string AllowedRangeText()
        {
            bool hasMin = MinLimit != null && MinLimit.Type != LimitSourceType.None;
            bool hasMax = MaxLimit != null && MaxLimit.Type != LimitSourceType.None;
            if (!hasMin && !hasMax) return IsAngle ? LanguageManager.GetString("Tip_Allowed", "Allowed") + ": 0 - 360" : "";
            var read = _lastReadPlc;
            if (read == null && ((hasMin && MinLimit.Type == LimitSourceType.Tag) || (hasMax && MaxLimit.Type == LimitSourceType.Tag)))
                return "";                                    // the limits are devices not read yet
            try
            {
                double min = ResolveLimit(MinLimit, read, double.MinValue);
                double max = ResolveLimit(MaxLimit, read, double.MaxValue);
                string lo = min == double.MinValue ? "" : FormatValue(min);
                string hi = max == double.MaxValue ? "" : FormatValue(max);
                return LanguageManager.GetString("Tip_Allowed", "Allowed") + ": " +
                       (lo.Length > 0 ? lo : "...") + " - " + (hi.Length > 0 ? hi : "...");
            }
            catch { return ""; }
        }
        private Func<string, int> _lastReadPlc;

        public NumericInputHMI()
        {
            // No default caption: every control carries a DescriptionText, so a
            // second built-in label would only have to be deleted on every drop.
            this.Text = "";
            this.BackColor = Color.White;
            _originalBackColor = this.BackColor;
            this.Cursor = Cursors.Hand;
        }

        public override void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            base.UpdateState(readPlc, checkGlobal);
            _lastReadPlc = readPlc; // cache so OnClick can re-resolve tag-based limits/variables on demand

            if (!this.Visible) return;

            bool secOk = SecurityManager.HasAccess(RequiredSecurity);
            bool condOk = checkGlobal(EnableConditions);
            bool trigOk = !(EnableTrigger != null && !string.IsNullOrEmpty(EnableTrigger.MonitorAddress))
                          || EnableTrigger.Check(readPlc);

            // See ButtonHMI: the connection is part of the soft enable.
            bool connOk = !ConnectionMonitor.IsKnownDown(PlcNumber);

            bool en = secOk && condOk && trigOk && connOk;
            _softEnabled = en;

            var wantCursor = en ? Cursors.Hand : Cursors.Default;
            if (this.Cursor != wantCursor) this.Cursor = wantCursor;

            // Reason shown to operators when they hover a blocked input.
            if (!connOk) _disableReason = ConnectionMonitor.ReasonFor(PlcNumber);
            else if (en) _disableReason = "";
            else if (!secOk)
                _disableReason = LanguageManager.GetString("Sys_ReqSec", "Requires Security Level:") + " " + RequiredSecurity;
            else if (!condOk)
                // Only the conditions NOT met, one per line.
                _disableReason = LanguageManager.GetString("Sys_ReqState", "Required Machine State:") +
                                 string.Concat(MachineConditionMap.Unmet(EnableConditions, checkGlobal).Select(n => "\n  \u2022 " + n));
            else
            {
                string trigName = (EnableTrigger != null && !string.IsNullOrWhiteSpace(EnableTrigger.DescriptionKey))
                                  ? LanguageManager.Localize(EnableTrigger.DescriptionKey)
                                  : (EnableTrigger != null ? EnableTrigger.MonitorAddress : "");
                _disableReason = LanguageManager.GetString("Sys_WaitSignal", "Waiting for Signal:") + " '" + trigName + "'";
            }

            // A CONFIGURED LIMIT IS ITSELF THE REQUEST.
            //
            // OutOfLimitsWarning defaults to false, so a field given a Min/Max -
            // whether a constant or a device tag - checked nothing unless someone
            // also found and ticked a separate property. Setting a limit and
            // getting no warning is the opposite of what setting a limit means.
            bool hasLimit = (MinLimit != null && MinLimit.Type != LimitSourceType.None)
                         || (MaxLimit != null && MaxLimit.Type != LimitSourceType.None);

            bool angle = IsAngle;
            if (OutOfLimitsWarning || hasLimit || angle)
            {
                double min = ResolveLimit(MinLimit, readPlc, double.MinValue);
                double max = ResolveLimit(MaxLimit, readPlc, double.MaxValue);
                double? minN = min == double.MinValue ? (double?)null : min;
                double? maxN = max == double.MaxValue ? (double?)null : max;

                // The bar spans exactly these limits - one set of bounds for the
                // keypad, the warning and the slider alike. An angle through zero
                // spans min ... max + 360 (see DrawBarView).
                _barWraps = NumericLimits.Wraps(minN, maxN, angle);
                _barLow = minN ?? 0;
                _barHigh = _barWraps ? maxN.Value + NumericLimits.FullTurn : (maxN ?? (angle ? NumericLimits.FullTurn : 100));

                // ONE RULE with the keypad (NumericLimits): an angle is 0 - 360, and
                // a minimum above the maximum is a range through zero - not a
                // range nothing can be in.
                if (!NumericLimits.Allows(_lastNumericValue, minN, maxN, angle))
                {
                    // The BORDER and the DIGITS blink, not the whole background.
                    // A field flooding red drowns the value it is complaining
                    // about, and on a page of sixteen axes several of them doing it
                    // at once is unreadable.
                    _outOfLimits = true;
                    this.BackColor = Color.White;
                    this.ForeColor = BlinkHelper.IsBlinkActive
                                     ? Color.FromArgb(200, 30, 30) : Color.Black;
                    Invalidate();
                    return;
                }
                if (_outOfLimits) { _outOfLimits = false; Invalidate(); }
            }

            this.BackColor = en ? Color.White : SystemColors.ControlLight;
            this.ForeColor = en ? SystemColors.ControlText : SystemColors.GrayText;
        }

        // Cheap resolver used for the on-screen "out of limits" blink warning - uses the
        // scanner's already-cached tag values (up to one scan interval old), not a fresh
        // PLC read, since it just needs to be visually close enough to flag the operator.
        /// <summary>True while the value sits outside its limits.</summary>
        private bool _outOfLimits;

        /// <summary>
        /// Whether the value is outside its limits. Exposed so this can be
        /// asserted in a test rather than judged from a screenshot.
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        public bool IsOutOfLimits { get { return _outOfLimits; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_barView) DrawBarView(e.Graphics);
            if (!_outOfLimits || !BlinkHelper.IsBlinkActive) return;
            using (var pen = new Pen(Color.FromArgb(200, 30, 30), 2f))
                e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
        }

        /// <summary>
        /// The slider, the value and the two nudge arrows.
        ///
        /// Drawn rather than composed from child controls: this is one field with
        /// one value, and three child controls would each want their own focus,
        /// tab stop and enabled state to keep in step with it.
        /// </summary>
        private void DrawBarView(Graphics g)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            DrawStep(g, MinusArea, "-");
            DrawStep(g, PlusArea, "+");

            var v = ValueArea;
            using (var back = new SolidBrush(Color.White)) g.FillRectangle(back, v);

            // A pale fill showing where the value sits between its limits. Behind
            // the number and light enough to read through - the figure is what the
            // operator needs, the fill is only the sense of "how far along".
            double lo = _barLow, hi = _barHigh;
            if (hi > lo)
            {
                // Through zero: 10 degrees on a 270 ... 45 range is 370 on the bar.
                double at = _barWraps && _lastNumericValue < lo ? _lastNumericValue + NumericLimits.FullTurn : _lastNumericValue;
                double f = Math.Max(0, Math.Min(1, (at - lo) / (hi - lo)));
                int w = (int)(v.Width * f);
                if (w > 0)
                    using (var b = new SolidBrush(Color.FromArgb(206, 228, 246)))
                        g.FillRectangle(b, v.X, v.Y, w, v.Height);
            }

            using (var pen = new Pen(Color.FromArgb(168, 176, 186)))
                g.DrawRectangle(pen, v.X, v.Y, v.Width - 1, v.Height - 1);

            TextRenderer.DrawText(g, this.Text, this.Font, v, this.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private void DrawStep(Graphics g, Rectangle r, string sign)
        {
            using (var b = new SolidBrush(Enabled ? Color.FromArgb(240, 241, 243)
                                                  : Color.FromArgb(248, 249, 250)))
                g.FillRectangle(b, r);
            using (var pen = new Pen(Color.FromArgb(168, 176, 186)))
                g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);

            using (var f = new Font(this.Font.FontFamily, Math.Max(10f, r.Height * 0.45f),
                                    FontStyle.Bold))
                TextRenderer.DrawText(g, sign, f, r,
                    Enabled ? Color.FromArgb(40, 44, 52) : Color.FromArgb(176, 182, 190),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
        }

        /// <summary>The resolved limits, refreshed on every scan.</summary>
        private double _barLow, _barHigh = 100;
        private bool _barWraps;

        /// <summary>Decimal places actually shown, including the unit's extra ones.</summary>
        private int EffectiveDecimals
        {
            get
            {
                return Math.Max(0, DecimalPlaces +
                    (UnitType != UnitType.None ? UnitManager.GetActiveExtraDecimals(UnitType) : 0));
            }
        }

        /// <summary>
        /// The value handed to the keypad: the number on screen, in the unit on
        /// screen, written the way the keypad reads it - "." decimal for decimal
        /// fields, plain hex digits for hex fields (a hex field used to be handed
        /// its DECIMAL value, which ENT then read back as hex: 255 became 0x255).
        /// </summary>
        private string KeypadText()
        {
            double shown = UnitType != UnitType.None
                ? UnitManager.ToDisplay(UnitType, _lastNumericValue)
                : _lastNumericValue;
            if (DataFormat == DisplayFormatEnum.Hexadecimal)
                return ((long)shown).ToString("X", System.Globalization.CultureInfo.InvariantCulture);
            return shown.ToString("F" + EffectiveDecimals, System.Globalization.CultureInfo.InvariantCulture);
        }

        private double ResolveLimit(DynamicValue limit, Func<string, int> readPlc, double defaultVal)
        {
            if (limit == null || limit.Type == LimitSourceType.None) return defaultVal;
            if (limit.Type == LimitSourceType.Static) return limit.StaticValue;
            // Same scaling as the keypad path: the tag holds a raw value, the
            // comparison is against a display value.
            if (limit.Type == LimitSourceType.Tag)
            {
                // Same 32-bit pair read as the keypad path, for the same reason.
                int low = readPlc(limit.TagAddress);
                int high = DataType == DataTypeEnum.Bin_32
                           ? readPlc(PlcAddressHelper.GetNextAddress(limit.TagAddress))
                           : 0;
                double raw = PlcValueConverter.ConvertToDouble(low, high, DataType, DataFormat, 0);
                // A limit word holds a raw value like the field's own: read the same way.
                try { return ToShownValue(raw, readPlc); }
                catch { return defaultVal; }   // the tooltip says why; the keypad refuses

            }
            if (limit.Type == LimitSourceType.HmiVariable) return HmiInternalVariables.Get(limit.TagAddress);
            return defaultVal;
        }

        // --- RESTORED: real keypad + limit validation + formula write, going through the PLC driver ---
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            if (!_softEnabled) return;
            if (string.IsNullOrEmpty(Address))
            {
                MessageBox.Show("This control has no Address configured.", "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var driver = PlcManager.Instance.GetDriverForWrite(PlcNumber);
            if (driver == null)
            {
                MessageBox.Show(PlcManager.Instance.DescribeAccess(PlcNumber),
                    "PLC Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 1. CALCULATE LIVE LIMITS
            // Reads Min/Max fresh from the PLC at click-time (not the cached scan value),
            // since these gate a write and must be as current as possible. If a Tag-based
            // limit can't be read right now, we fail closed rather than silently allowing
            // an unverified value through.
            bool commsOk = true;

            Func<string, int> liveTagReader = (address) =>
            {
                int val;
                if (!driver.TryRead(address, out val)) commsOk = false;
                return val;
            };

            Func<string, double, double> formulaEvaluator = (formula, rawValue) => EvaluateFormula(formula, rawValue, liveTagReader);

            // A formula that cannot work is found BEFORE the keypad opens, not
            // after the operator has typed a value it can then not write.
            var formulaProblem = FormulaProblems(errorsOnly: true).FirstOrDefault();
            if (formulaProblem != null)
            {
                ShowFormulaError(formulaProblem);
                return;
            }

            InputLimitsCalculation.EffectiveLimits effectiveLimits;
            try
            {
                effectiveLimits = InputLimitsCalculation.GetEffectiveLimits(
                    this.DataType, this.DataFormat, this.MinLimit, this.MaxLimit, this.ReadFormula,
                    liveTagReader, formulaEvaluator,
                    this.Divisor);
            }
            catch (Exception ex)
            {
                ShowFormulaError("Read formula (for a limit): " + ex.Message);
                return;
            }

            if (!commsOk)
            {
                MessageBox.Show("Could not read a live value from the PLC needed to verify safe limits. Write cancelled.",
                    "PLC Communication Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 2. OPEN KEYPAD
            // Limits are resolved in the PLC's REFERENCE unit, but the operator types
            // in the DISPLAYED unit. They must be converted, otherwise the keypad
            // validates against the wrong scale: with limits 0-1000 mm shown as
            // inches, typing 500 would pass (500 <= 1000) and then be written as
            // 12700 mm - far outside the real limit.
            bool hasRealMin = effectiveLimits.Min != double.MinValue &&
                              !double.IsNegativeInfinity(effectiveLimits.Min);
            bool hasRealMax = effectiveLimits.Max != double.MaxValue &&
                              !double.IsPositiveInfinity(effectiveLimits.Max);

            double kMin = effectiveLimits.Min;
            double kMax = effectiveLimits.Max;
            bool angle = IsAngle;
            if (UnitType != UnitType.None)
            {
                kMin = UnitManager.ToDisplay(UnitType, effectiveLimits.Min);
                kMax = UnitManager.ToDisplay(UnitType, effectiveLimits.Max);
                // Guard inverted scales - but not an angle's range through zero,
                // where a minimum above the maximum is the point.
                if (kMin > kMax && !angle) { double t = kMin; kMin = kMax; kMax = t; }
            }


            // THE TOUCH NUMPAD, not a text box. These machines are operated on a
            // panel with no keyboard, and the limits are shown across the top so a
            // refused entry is never a surprise.
            using (var keypad = new Kaban.UI.Forms.NumpadForm(
                       LanguageManager.GetString("Sys_InputTitle", "Input"),
                       // THE NUMBER ALONE, IN THE UNIT ON SCREEN.
                       //
                       // _lastNumericValue is kept in the PLC's reference unit (mm),
                       // so handing it straight to the keypad showed millimetres
                       // while the field showed inches - and with the millimetre
                       // decimal count, which is a different number of places. The
                       // write path converts back, so what goes in must be what the
                       // operator is looking at.
                       //
                       // INVARIANT, "." decimal. The field shows the number in the
                       // panel's regional format ("12,50" on a Turkish or German
                       // Windows) but the keypad only knows ".", so the value it
                       // was handed could not be edited or even accepted there.
                       KeypadText(),
                       // TESTED BEFORE CONVERSION. "No limit" is double.MinValue;
                       // dividing that by 25.4 for inches gives a merely enormous
                       // number that is no longer equal to MinValue, so the guard
                       // missed it and the pad printed -70775320270169900000000.
                       hasRealMin ? (double?)kMin : null,
                       hasRealMax ? (double?)kMax : null,
                       // An angle is 0 - 360: no minus sign to offer.
                       allowNegative: !angle && ((DataFormat == DisplayFormatEnum.Signed_Decimal) ||
                                                 (DataFormat == DisplayFormatEnum.Real)),
                       allowDecimal: EffectiveDecimals > 0,
                       hex: DataFormat == DisplayFormatEnum.Hexadecimal))
            {
                // The field's own shape, so the keypad cannot accept something the
                // control could never display.
                keypad.MaxDigits = Digits;
                keypad.DecimalPlaces = EffectiveDecimals;
                keypad.Angle = angle;
                keypad.PlaceBeside(this);

                // THE FIELD SHOWS IT IS THE ONE BEING EDITED.
                //
                // The keypad opens beside the control, but on a page of twenty
                // identical boxes "beside" is not enough to say which - and the
                // operator has to be sure they are changing the axis they meant.
                // Held highlighted rather than blinking: a blink competes with the
                // caret in the pad itself, and two things flashing at once is
                // harder to read than one.
                IsEditing = true;
                try
                {
                    if (keypad.ShowDialog() != DialogResult.OK) return;
                }
                finally { IsEditing = false; }
                ProcessWrite(keypad.Value, driver, liveTagReader);
            }
        }

        private void ProcessWrite(string input, IPlcDriver driver, Func<string, int> liveTagReader)
        {
            // THE KEYPAD'S FORMAT, not the panel's. This parsed with the regional
            // settings AND thousands separators allowed: on a Turkish or German
            // Windows "." is the thousands separator, so "12.5" would have been
            // written as 125. Hex fields come back as hex digits.
            double val;
            bool parsed;
            if (DataFormat == DisplayFormatEnum.Hexadecimal)
            {
                long hexVal;
                parsed = long.TryParse(input, System.Globalization.NumberStyles.HexNumber,
                                       System.Globalization.CultureInfo.InvariantCulture, out hexVal);
                val = hexVal;
            }
            else
            {
                parsed = double.TryParse(input, System.Globalization.NumberStyles.Float,
                                         System.Globalization.CultureInfo.InvariantCulture, out val);
            }
            if (!parsed)
            {
                MessageBox.Show("Please enter a valid number.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // UNIT CONVERSION (safety-relevant): the operator types in the unit
            // currently displayed, but the PLC stores the REFERENCE unit. Without
            // this, entering "10" while the display shows inches wrote 10 - i.e.
            // 10 mm instead of 254 mm - silently commanding the wrong position.
            if (UnitType != UnitType.None)
            {
                val = UnitManager.ToReference(UnitType, val);
            }

            // THE RAW VALUE TO WRITE. Reading is  shown = raw / Divisor  (the
            // decimals are a DISPLAY format, not a second scaling), so writing is
            // the single inverse  raw = typed * Divisor. A WriteFormula replaces
            // that: its result IS the raw value - multiplying it by the Divisor as
            // well scaled it twice.
            //
            // (With Divisor 100 and 2 decimals, typing 2500 once wrote 25 - the
            // decimals removed instead of the divisor undone - and later
            // 25,000,000, which wrapped inside a 16-bit word to 30784.)
            bool custom = !NumericFormula.IsIdentity(WriteFormula);
            double rawValue;
            try
            {
                rawValue = custom ? EvaluateFormula(WriteFormula, val, liveTagReader)
                                  : (Divisor != 0 ? val * Divisor : val);
                if (double.IsNaN(rawValue) || double.IsInfinity(rawValue))
                    throw new FormulaException("It gives no number for " + val + " (a division by 0?)" + VariableValues(liveTagReader) + ".");
            }
            catch (Exception ex)
            {
                ShowFormulaError("Write formula: " + ex.Message);
                return;
            }

            if (ConfirmWrite)
            {
                // The PLC device number (e.g. "D210") is engineering detail an operator
                // must not see. Only Admin gets the address in the prompt; lesser users
                // get a plain, translated confirmation with just the value.
                string prompt;
                if (SecurityManager.HasAccess(SecurityLevel.Admin))
                {
                    prompt = string.Format(
                        LanguageManager.GetString("Sys_ConfirmChangeAddr", "Write {0} to {1}?"),
                        val, Address);
                }
                else
                {
                    prompt = string.Format(
                        LanguageManager.GetString("Sys_ConfirmChange",
                            "Are you sure you want to change this value to {0}?"),
                        val);
                }

                var result = MessageBox.Show(prompt,
                    LanguageManager.GetString("Sys_ConfirmTitle", "Confirm"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes) return;
            }

            try
            {
                short low, high;
                PlcValueConverter.ConvertFromDouble(rawValue, DataType, DataFormat, 0, out low, out high);

                // Write to the *effective* address (base offset applied), matching how the
                // value is read/displayed - the previous version wrote to the raw Address,
                // which could silently disagree with what was on screen when an offset was set.
                string effAddr = GetEffectiveAddress(_lastReadPlc ?? liveTagReader);

                int writeResult = driver.Write(effAddr, low);
                bool ok = (writeResult == 0);

                if (DataType == DataTypeEnum.Bin_32)
                {
                    int writeResult2 = driver.Write(PlcAddressHelper.GetNextAddress(effAddr), high);
                    ok &= (writeResult2 == 0);
                }

                if (!ok)
                {
                    string rejMsg = SecurityManager.HasAccess(SecurityLevel.Admin)
                        ? string.Format(LanguageManager.GetString("Sys_WriteRejectedAddr", "The PLC rejected the write to {0}."), effAddr)
                        : LanguageManager.GetString("Sys_WriteRejected", "The PLC rejected the write.");
                    MessageBox.Show(rejMsg, LanguageManager.GetString("Sys_WriteFailedTitle", "Write Failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (LogAction)
                {
                    AuditLogger.Log($"wrote {val}{(custom ? " (raw " + rawValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")" : "")} to {effAddr} (PLC {PlcNumber}, control address '{Address}').");
                }
            }
            catch (Exception ex)
            {
                string writeErr = SecurityManager.HasAccess(SecurityLevel.Admin)
                    ? $"Write error.\n{ex.Message}"
                    : LanguageManager.GetString("Sys_WriteError", "The value could not be written.");
                MessageBox.Show(writeErr, LanguageManager.GetString("Sys_WriteErrorTitle", "Write Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Works out a formula for one value: A (val, x, value) is the value, every
        /// other name one of the MathVariables - a constant, or a PLC device read
        /// through <paramref name="tagReader"/>. Names are matched in any case and
        /// bound as written (NCalc matches them exactly). A name that is neither
        /// is reported by name, not as NCalc's "Parameter was not defined".
        /// </summary>
        internal double EvaluateFormula(string formula, double value, Func<string, int> tagReader)
        {
            if (NumericFormula.IsIdentity(formula)) return value;

            var missing = NumericFormula.UndefinedMessage(formula, MathVariables);
            if (missing != null) throw new FormulaException(missing);

            var exp = new Expression(formula, EvaluateOptions.IgnoreCase);
            foreach (var name in NumericFormula.Names(formula))
            {
                if (NumericFormula.IsValueName(name)) { exp.Parameters[name] = value; continue; }
                var v = NumericFormula.Find(MathVariables, name);
                exp.Parameters[name] = v.IsConstant
                    ? v.GetConstantValue()
                    : (string.IsNullOrWhiteSpace(v.AddressOrValue) || tagReader == null ? 0 : (double)tagReader(v.AddressOrValue.Trim()));
            }

            object result;
            try { result = exp.Evaluate(); }
            catch (Exception ex) { throw new FormulaException("'" + formula + "' cannot be worked out: " + ex.Message, ex); }
            try { return Convert.ToDouble(result, System.Globalization.CultureInfo.InvariantCulture); }
            catch (Exception ex) { throw new FormulaException("'" + formula + "' does not give a number (" + result + ").", ex); }
        }

        /// <summary>The raw value as shown: ReadFormula when there is one, raw / Divisor otherwise.</summary>
        protected override double ToShownValue(double raw, Func<string, int> readPlc)
        {
            if (NumericFormula.IsIdentity(ReadFormula))
            {
                _readFormulaError = "";
                return base.ToShownValue(raw, readPlc);
            }
            try
            {
                double v = EvaluateFormula(ReadFormula, raw, readPlc);
                if (double.IsNaN(v) || double.IsInfinity(v))
                    throw new FormulaException("It gives no number for " + raw + " (a division by 0?)" + VariableValues(readPlc) + ".");
                _readFormulaError = "";
                return v;
            }
            catch (Exception ex)
            {
                // The field shows "Err"; the admin tooltip says why.
                string msg = "Read formula: " + ex.Message;
                if (msg != _readFormulaError) { _readFormulaError = msg; InvalidateTooltip(); }
                throw;
            }
        }

        /// <summary>" with B = 0" - the variables' values, for a message.</summary>
        private string VariableValues(Func<string, int> tagReader)
        {
            var parts = MathVariables.Where(v => v != null && !string.IsNullOrWhiteSpace(v.Name)).Select(v =>
            {
                double x = v.IsConstant ? v.GetConstantValue()
                         : (string.IsNullOrWhiteSpace(v.AddressOrValue) || tagReader == null ? 0 : tagReader(v.AddressOrValue.Trim()));
                return v.Name.Trim() + " = " + x.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }).ToList();
            return parts.Count == 0 ? "" : " with " + string.Join(", ", parts);
        }

        /// <summary>
        /// Everything wrong with the formulas, in plain words - what FormulaCheck
        /// shows in the designer, the admin tooltip lists, and the keypad refuses
        /// to open over. Empty when both work.
        ///
        /// Tried with each variable at its constant (a device's at 1): a name not
        /// defined, a formula NCalc cannot read, one that gives no number (B still
        /// at its default 0 under a division), and a pair that is not each other's
        /// inverse - what is written would not read back as what was typed.
        ///
        /// <paramref name="errorsOnly"/>: only what makes a formula fail - the
        /// keypad refuses to open over those. A pair that does not match is a
        /// mistake to fix, but the value can still be written.
        /// </summary>
        public List<string> FormulaProblems(bool errorsOnly = false)
        {
            var problems = new List<string>(NumericFormula.VariableProblems(MathVariables));
            Func<string, int> sample = a => 1;
            bool readSet = !NumericFormula.IsIdentity(ReadFormula);
            bool writeSet = !NumericFormula.IsIdentity(WriteFormula);
            bool readOk = true, writeOk = true;

            Action<string, string, Action> check = (label, formula, failed) =>
            {
                string missing = NumericFormula.UndefinedMessage(formula, MathVariables);
                if (missing != null) { problems.Add(label + ": " + missing); failed(); return; }
                try
                {
                    double r = EvaluateFormula(formula, 10, sample);
                    if (double.IsNaN(r) || double.IsInfinity(r))
                    {
                        problems.Add(label + ": it gives no number (a division by 0?)" + VariableValues(sample) + ".");
                        failed();
                    }
                }
                catch (Exception ex) { problems.Add(label + ": " + ex.Message); failed(); }
            };
            if (readSet) check("ReadFormula", ReadFormula, () => readOk = false);
            if (writeSet) check("WriteFormula", WriteFormula, () => writeOk = false);

            if (errorsOnly) return problems;
            if (readSet != writeSet)
                problems.Add(readSet
                    ? "ReadFormula is set but WriteFormula is not: a value typed would be written as typed x Divisor. Give WriteFormula the inverse of ReadFormula."
                    : "WriteFormula is set but ReadFormula is not: the value would be shown as raw / Divisor. Give ReadFormula the inverse of WriteFormula.");
            else if (readSet && readOk && writeOk)
            {
                // Typed, written, read back: the number the operator typed again?
                foreach (double typed in new[] { 0.0, 1.0, 10.0, 123.0 })
                {
                    double raw, back;
                    try { raw = EvaluateFormula(WriteFormula, typed, sample); back = EvaluateFormula(ReadFormula, raw, sample); }
                    catch { continue; }
                    if (Math.Abs(back - typed) > 1e-6 * Math.Max(1.0, Math.Abs(typed)))
                    {
                        problems.Add("The formulas are not each other's inverse: " + typed + " typed is written as " + raw +
                                     " and read back as " + back + VariableValues(sample) + ".");
                        break;
                    }
                }
            }
            return problems;
        }

        /// <summary>A formula that failed: the reason for an Admin, the plain "could not be written" for others.</summary>
        private void ShowFormulaError(string detail)
        {
            AuditLogger.Log($"could not write {Address} (PLC {PlcNumber}): formula problem - {detail}");
            string msg = SecurityManager.HasAccess(SecurityLevel.Admin)
                ? "Formula problem.\n\n" + detail + "\n\nRead formula:\t" + (ReadFormula ?? "") +
                  "\nWrite formula:\t" + (WriteFormula ?? "") +
                  (MathVariables.Count > 0 ? "\nVariables:\t" + string.Join(", ", MathVariables.Where(v => v != null)) : "")
                : LanguageManager.GetString("Sys_WriteError", "The value could not be written.");
            MessageBox.Show(msg, LanguageManager.GetString("Sys_WriteErrorTitle", "Write Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public class NumericInputSorter : ExpandableObjectConverter
    {
        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
        {
            var props = TypeDescriptor.GetProperties(value, attributes);

            string[] list = {
                "PlcNumber", "Address", "DataType", "AddressOffset",
                "Divisor", "Digits", "DecimalPlaces", "FillZero", "DataFormat", "Suffix", "UnitType", "TranslationKey",
                "MinLimit", "MaxLimit", "OutOfLimitsWarning", "ReadFormula", "WriteFormula", "MathVariables", "FormulaCheck",
                "VisibilityConditions", "EnableConditions", "EnableTrigger",
                "ConfirmWrite"
            };
            return props.Sort(list);
        }
    }
}

