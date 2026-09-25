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
        [Category("   Limits")] public DynamicValue MinLimit { get; set; } = new DynamicValue();
        [Category("   Limits")] public DynamicValue MaxLimit { get; set; } = new DynamicValue();

        // --- RESTORED: formula-driven read/write engine (was stripped out) ---
        [Category("   Math")]
        [Description("Formula applied to a Tag-based MinLimit/MaxLimit's raw PLC value before comparing " +
                      "it against the entered value, e.g. 'val / 10'. Supports NCalc syntax. Default 'val' = no change.")]
        public string ReadFormula { get; set; } = "val";

        [Category("   Math")]
        [Description("Formula applied to the value the operator types before it is written to the PLC, " +
                      "e.g. 'val * 10' or 'A + B'. Supports NCalc syntax. Default 'val' = write as-is.")]
        public string WriteFormula { get; set; } = "val";

        [Category("   Math")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Description("Extra named variables (B, C, ...) usable inside ReadFormula/WriteFormula. " +
                      "'val'/'A' (the entered value) is always available automatically.")]
        public List<MathVariable> MathVariables { get; } = new List<MathVariable>();

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
            if (!string.IsNullOrWhiteSpace(WriteFormula))
                sb.Append("\nWrite Formula:\t\t").Append(WriteFormula);

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
            return base.BuildOperatorTooltip();
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

            if (OutOfLimitsWarning || hasLimit)
            {
                double min = ResolveLimit(MinLimit, readPlc, double.MinValue);
                double max = ResolveLimit(MaxLimit, readPlc, double.MaxValue);

                // The bar spans exactly these limits - one set of bounds for the
                // keypad, the warning and the slider alike.
                _barLow = min == double.MinValue ? 0 : min;
                _barHigh = max == double.MaxValue ? 100 : max;

                if (_lastNumericValue < min || _lastNumericValue > max)
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
                double f = Math.Max(0, Math.Min(1, (_lastNumericValue - lo) / (hi - lo)));
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
                bool identity = string.IsNullOrWhiteSpace(ReadFormula) ||
                                ReadFormula.Trim().ToLowerInvariant() == "val";
                return (Divisor != 0 && identity) ? raw / Divisor : raw;
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

            var effectiveLimits = InputLimitsCalculation.GetEffectiveLimits(
                this.DataType, this.DataFormat, this.MinLimit, this.MaxLimit, this.ReadFormula,
                liveTagReader, formulaEvaluator,
                this.Divisor);

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
            if (UnitType != UnitType.None)
            {
                kMin = UnitManager.ToDisplay(UnitType, effectiveLimits.Min);
                kMax = UnitManager.ToDisplay(UnitType, effectiveLimits.Max);
                if (kMin > kMax) { double t = kMin; kMin = kMax; kMax = t; } // guard inverted scales
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
                       allowNegative: (DataFormat == DisplayFormatEnum.Signed_Decimal) ||
                                      (DataFormat == DisplayFormatEnum.Real),
                       allowDecimal: EffectiveDecimals > 0,
                       hex: DataFormat == DisplayFormatEnum.Hexadecimal))
            {
                // The field's own shape, so the keypad cannot accept something the
                // control could never display.
                keypad.MaxDigits = Digits;
                keypad.DecimalPlaces = EffectiveDecimals;
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

            try
            {
                // Apply the write formula (default "val" = no change).
                if (!string.IsNullOrWhiteSpace(WriteFormula) && WriteFormula.Trim() != "val")
                {
                    val = EvaluateFormula(WriteFormula, val, liveTagReader);
                }
            }
            catch (Exception ex)
            {
                string formulaMsg = SecurityManager.HasAccess(SecurityLevel.Admin)
                    ? $"Write formula error.\nFormula: {WriteFormula}\n{ex.Message}"
                    : LanguageManager.GetString("Sys_WriteError", "The value could not be written.");
                MessageBox.Show(formulaMsg, LanguageManager.GetString("Sys_WriteErrorTitle", "Write Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                // THE DIVISOR MUST BE UNDONE HERE.
                // Reading does  raw / Divisor  then shows DecimalPlaces digits, but
                // writing only ever removed the decimal point - so with Divisor 100
                // and 2 decimals, typing 2500 wrote 2500*100/10000 = 25 to the PLC.
                // Multiply by the divisor first, then let the converter strip the
                // decimals, and the two directions agree.
                // Reading is exactly  displayed = raw / Divisor  (the decimals are a
                // DISPLAY format, not a second scaling), so writing is the single
                // inverse: raw = typed * Divisor. Passing DecimalPlaces on as well
                // scaled a second time - typing 2500 with Divisor 100 produced
                // 25,000,000, which then wrapped inside a 16-bit word to 30784.
                double rawValue = Divisor != 0 ? val * Divisor : val;

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
                    AuditLogger.Log($"wrote {val} to {effAddr} (control address '{Address}').");
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
        /// Evaluates an NCalc formula with "val"/"A" bound to rawValue, plus any configured
        /// MathVariables (B, C, ... - constants or live PLC tags read via tagReader).
        /// </summary>
        private double EvaluateFormula(string formula, double rawValue, Func<string, int> tagReader)
        {
            if (string.IsNullOrWhiteSpace(formula) || formula.Trim() == "val") return rawValue;

            var exp = new Expression(formula, EvaluateOptions.IgnoreCase);
            exp.Parameters["val"] = rawValue;
            exp.Parameters["A"] = rawValue;
            exp.Parameters["x"] = rawValue;

            if (MathVariables != null)
            {
                foreach (var v in MathVariables)
                {
                    if (v == null || string.IsNullOrEmpty(v.Name)) continue;
                    if (string.Equals(v.Name, "A", StringComparison.OrdinalIgnoreCase)) continue;

                    double paramVal = v.IsConstant
                        ? v.GetConstantValue()
                        : (string.IsNullOrEmpty(v.AddressOrValue) ? 0 : tagReader(v.AddressOrValue));

                    exp.Parameters[v.Name] = paramVal;
                }
            }

            object result = exp.Evaluate();
            return Convert.ToDouble(result);
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
                "MinLimit", "MaxLimit", "OutOfLimitsWarning", "ReadFormula", "WriteFormula", "MathVariables",
                "VisibilityConditions", "EnableConditions", "EnableTrigger",
                "ConfirmWrite"
            };
            return props.Sort(list);
        }
    }
}

