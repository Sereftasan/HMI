using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// ONE AXIS, ONE LINE - everything needed to set an axis up, laid out the way
    /// the old program's "Takım Ayarları" page did it.
    ///
    ///   name | ◀ | position | current | ▶ | min | max | reference | calibration |
    ///   calibrate | send-to-reference | Z distance | axis alarm | axis warning |
    ///   servo alarm | [travel per revolution | backlash]
    ///
    /// The CURRENT sits beside the position: both change while the axis moves.
    /// The Z DISTANCE sits beside the reference selection: it is the reference's
    /// quality. Travel per revolution and backlash come last, and only on a page
    /// where some axis has them - they are rarely set from the HMI.
    ///
    /// WHY A COMPOSITE RATHER THAN LOOSE CONTROLS
    /// ------------------------------------------
    /// An axis setup page is the same twelve fields repeated per axis. Placed
    /// individually that is a hundred-odd controls whose addresses have to line
    /// up by hand, and one mistyped device puts a limit on the wrong axis - which
    /// is found by driving into a hard stop. Here an axis is ONE control with one
    /// group of addresses, so a row is either right or obviously wrong.
    ///
    /// EMPTY ADDRESS = HIDDEN FIELD. Not every machine has every part: H800 has
    /// no go-to-reference command and no current or Z-distance words, so those
    /// simply do not appear on its rows. A machine is not obliged to grow fields
    /// it does not own, and a visible control bound to nothing is worse than an
    /// absent one - it invites a press that cannot work.
    ///
    /// The movement half is deliberately identical to CylinderUnitHMI: same
    /// glyphs, same momentary behaviour, same position readout. An operator who
    /// has learned the manual page has already learned this.
    /// </summary>
    [ToolboxItem(true)]
    /// <summary>Which jog direction's button shows an axis's reference sensor.</summary>
    public enum AxisReferenceSide { Back = 0, Forward = 1 }

    // The Properties window in the order of the work (see PropertyOrder).
    [System.ComponentModel.TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class AxisSettingHMI : UserControl, IConditionalControl
    {
        // ==================================================================
        // BINDING
        // ==================================================================
        [Category("  PLC"), Description("Station this axis lives on.")]
        public int PlcNumber { get; set; }

        [Category("  PLC"), Description("Not used directly; the fields carry their own addresses.")]
        public string Address { get; set; }

        /// <summary>
        /// TRANSLATION KEY for the axis name - "Axis_Milling1Angle", not "Milling
        /// 1 Angle".
        ///
        /// The same axis appears on the manual pages, the home screen and here, so
        /// its name belongs in ONE place. Keys live in CommonTranslations because
        /// most machines share the same axes: fixing "Milling 1 Angle" once fixes
        /// it everywhere, and a machine that calls it something else overrides the
        /// key in its own file.
        ///
        /// Shown as typed when no translation exists, so a name entered directly
        /// still works.
        /// </summary>
        [Category(" Axis"), Description("Translation key for the axis name.")]
        public string AxisNameKey { get; set; }

        /// <summary>Kept so older designer files compile; forwards to AxisNameKey.</summary>
        [Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public string AxisName { get { return AxisNameKey; } set { AxisNameKey = value; } }

        // ---- movement ----
        [Category(" Movement"), Description("Jog back / minus.")]
        public string CommandBackAddress { get; set; }

        [Category(" Movement"), Description("Jog forward / plus.")]
        public string CommandForwardAddress { get; set; }

        [Category(" Movement"), Description("Live axis position.")]
        public string PositionAddress { get; set; }

        [Category(" Movement")]
        public DataTypeEnum PositionDataType { get; set; }

        /// <summary>
        /// The axis's REAL (encoder) position, when the PLC keeps it apart from the
        /// position above. Not shown as a field: the position's tooltip starts with
        /// it and with the difference, in the axis's unit to its last count (4
        /// decimals for a divisor of 10000) - at the row's 2 decimals a difference
        /// of a few counts would read 0.00.
        /// </summary>
        [Category(" Movement"), DefaultValue(""), Description("Real (encoder) position. Shown in the position's tooltip with its difference, in the axis's unit to its last count.")]
        public string RealPositionAddress { get; set; }

        // Position, min, max, reference and calibration are all the SAME physical
        // quantity on the same axis, so they share one divisor, one digit count
        // and one decimal place setting. Entering those five times per axis is
        // five chances to mistype, and a divisor that disagrees between the limit
        // and the position is a limit that does not mean what the position means.

        // ---- settings ----
        [Category(" Settings"), Description("Minimum travel limit.")]
        public string MinLimitAddress { get; set; }

        [Category(" Settings"), Description("Maximum travel limit.")]
        public string MaxLimitAddress { get; set; }

        [Category(" Settings"), Description("Reference (home) value.")]
        public string ReferenceAddress { get; set; }

        [Category(" Settings"), Description("Calibration value.")]
        public string CalibrationAddress { get; set; }

        /// <summary>
        /// Travel per motor revolution and backlash - rarely set from the HMI, so
        /// they are fields only when given, at the END of the row (after the drive
        /// codes), where an axis without them leaves no gap. In the axis's unit,
        /// with its divisor, to its last count: 0.0150 mm for 150 counts at a
        /// divisor of 10000 (30 September - they were shown as the raw 150).
        /// </summary>
        [Category(" Settings"), DefaultValue(""), Description("Travel per motor revolution, in the axis's unit (its divisor, to the last count). Empty hides the field.")]
        public string TravelPerRevolutionAddress { get; set; }

        [Category(" Settings"), DefaultValue(""), Description("Backlash, in the axis's unit (its divisor, to the last count). Empty hides the field.")]
        public string BacklashAddress { get; set; }

        [Category(" Value"), Description("Raw-to-display divisor for EVERY value on the row.")]
        public double Divisor { get; set; }

        [Category(" Value"), Description("Decimal places for every value on the row.")]
        public int DecimalPlaces { get; set; }

        [Category(" Value"), Description("Digit count for every value on the row.")]
        public int Digits { get; set; }

        [Category(" Value"), Description("Word width of the setting registers.")]
        public DataTypeEnum SettingDataType { get; set; }

        [Category(" Value"), Description("Unit suffix (mm, deg...).")]
        public string Suffix { get; set; }

        /// <summary>
        /// The unit family of everything on this row that is a machine dimension:
        /// the position AND the four settings. Length converts with the HMI's
        /// mm/inch setting; an angle does not.
        /// </summary>
        [Browsable(false)]
        public UnitType PositionUnitType
        {
            get { return Suffix == "mm" ? UnitType.Length : UnitType.None; }
        }

        /// <summary>
        /// Fixed entry limits, used when the axis has no MIN/MAX words to read.
        /// </summary>
        /// <summary>
        /// Fixed entry bounds. NaN - the default - means NO LIMIT.
        ///
        /// These used to default to -99999 / 99999, which looked harmless and was
        /// not: it is a MILLIMETRE figure invented from a digit count, and the
        /// moment the display switches to inches it means 99999 inches, a limit the
        /// machine could never reach and which no longer relates to anything.
        ///
        /// A field is bounded by what it can DISPLAY - that is the digit count, and
        /// the keypad enforces it directly - or by a real travel limit read from
        /// the machine. An invented number in between is worse than nothing,
        /// because it looks like a decision somebody made.
        /// </summary>
        [Category(" Value")]
        public double EntryMinimum { get; set; }

        [Category(" Value")]
        public double EntryMaximum { get; set; }

        /// <summary>
        /// Optional PLC words holding the entry bounds, instead of the fixed
        /// EntryMinimum / EntryMaximum above.
        ///
        /// A machine whose travel changes with the fitted tooling cannot have its
        /// bounds compiled into a page - and a bound that has to be edited in the
        /// designer is one that will be left wrong.
        /// </summary>
        [Category(" Value"), Description("Device holding the entry minimum. Overrides EntryMinimum.")]
        public string EntryMinimumAddress { get; set; }

        [Category(" Value"), Description("Device holding the entry maximum. Overrides EntryMaximum.")]
        public string EntryMaximumAddress { get; set; }

        /// <summary>
        /// This axis's number on its machine. A manual-page AxisUnitHMI with the
        /// same number takes everything else from this row - addresses, scaling,
        /// name, error bit, reference sensor - so none of it is entered twice.
        /// </summary>
        [Category(" Axis"), DefaultValue(0), Description("Axis number. An AxisUnitHMI with this number mirrors this row.")]
        public int AxisNumber { get; set; }

        /// <summary>The axis's reference (home) sensor.</summary>
        [Category(" Axis"), DefaultValue(""), Description("Reference sensor input. Lit on the jog button of the side below.")]
        public string ReferenceSensorAddress { get; set; }

        /// <summary>Which jog button carries the reference sensor lamp - one side only.</summary>
        [Category(" Axis"), DefaultValue(AxisReferenceSide.Back), Description("The side the reference sensor is on.")]
        public AxisReferenceSide ReferenceSensorSide { get; set; }

        /// <summary>
        /// The axis's error bit. An alarm on this device is this axis's servo
        /// fault, and its help opens the servo drive manual named for this axis.
        /// </summary>
        [Category(" Help"), DefaultValue(""), Description("Error bit set when this axis faults.")]
        public string ErrorAddress { get; set; }

        /// <summary>This axis's error bit, for AlarmHelpLinks (null when none).</summary>
        public Kaban.UI.Helpers.AlarmHelpLinks.Link HelpLink()
        {
            if (string.IsNullOrWhiteSpace(ErrorAddress)) return null;
            return new Kaban.UI.Helpers.AlarmHelpLinks.Link
            {
                Kind = TroubleshootingCatalog.ErrorType.ServoFault,
                Station = PlcNumber,
                Address = ErrorAddress.Trim(),
                Unit = LanguageManager.GetString(AxisNameKey ?? "", AxisNameKey ?? ""),
                // So the alarm's help opens the entry for the code the drive shows.
                ServoAlarm = (ServoAlarmAddress ?? "").Trim(),
                AxisAlarm = (AxisAlarmAddress ?? "").Trim(),
                AxisWarning = (AxisWarningAddress ?? "").Trim()
            };
        }

        /// <summary>
        /// True when this row is an ANGLE, where limits do not behave like travel.
        ///
        /// An angle wraps: an axis that travels from 270 degrees through 360 (= 0)
        /// on to 45 has the minimum 270 and the maximum 45 - a minimum ABOVE the
        /// maximum, which every plain min &lt;= max test rejects. Its limits used
        /// to be switched off for that reason, which left the reference and the
        /// calibration of an angle axis with no bound at all.
        ///
        /// Now its fields are NumericInputHMI.IsAngle (see NumericLimits): every
        /// setting is 0 - 360; the minimum and maximum do not bound each other
        /// (a minimum above the maximum is the range through zero); and the
        /// reference and calibration are held to the travel between them, through
        /// zero when it runs through zero.
        /// </summary>
        [Browsable(false)]
        public bool IsAngle { get { return Suffix == "\u00B0"; } }

        // ---- commands ----
        [Category(" Commands"), Description("Applies the calibration value to the axis.")]
        public string CalibrateAddress { get; set; }

        /// <summary>
        /// Reference ACTIVATION. It is an Alternate bit the operator arms and
        /// leaves armed, not a momentary press, so it is shown as a checkbox -
        /// a button that stays pressed is a button that looks broken.
        /// </summary>
        [Category(" Commands"), Description("Reference activation selection (latched).")]
        public string GoToReferenceAddress { get; set; }

        /// <summary>
        /// The PLC's HOME (reference) REQUEST for this axis: while it is TRUE the
        /// Ref box's border blinks red - the axis needs referencing, and the box
        /// to tick is the one blinking. Read only; the HMI never writes it.
        /// </summary>
        [Category(" Commands"), DefaultValue("")]
        [Description("The PLC's home (reference) request bit: while TRUE the Ref box's border blinks red. Read only.")]
        public string HomeRequestAddress { get; set; } = "";

        // ---- meters ----
        // MOTOR CURRENT is a servo load percentage read from a status word - a
        // different register family from the setting block and a different scale.
        [Category(" Meters"), Description("Servo load / motor current. Empty hides the bar.")]
        public string CurrentAddress { get; set; }

        [Category(" Meters")]
        public double CurrentDivisor { get; set; }

        [Category(" Meters"), Description("Reading that counts as 100% on the bar.")]
        public double CurrentFullScale { get; set; }

        // REFERENCE Z SIGNAL DISTANCE is a millimetre offset, not a percentage:
        // how far past the Z pulse the axis stopped. Shown as a bar against one
        // motor revolution so a marginal reference is visible at a glance.
        [Category(" Meters"), Description("Reference Z signal distance. Empty hides the bar.")]
        public string ZDistanceAddress { get; set; }

        // ---- drive diagnostics --------------------------------------------
        // Three separate codes because the drive reports three separate things,
        // and which one is non-zero is the first question in any servo fault.
        [Category(" Diagnostics"), Description("Axis alarm code word (hexadecimal).")]
        public string AxisAlarmAddress { get; set; }

        [Category(" Diagnostics"), Description("Axis warning code word (hexadecimal).")]
        public string AxisWarningAddress { get; set; }

        [Category(" Diagnostics"), Description("Servo amplifier alarm code word (hexadecimal).")]
        public string ServoAlarmAddress { get; set; }

        [Category(" Meters")]
        public double ZDistanceDivisor { get; set; }

        [Category(" Meters"), Description("Distance that counts as 100% on the bar.")]
        public double ZDistanceFullScale { get; set; }

        [Category(" Security")]
        public SecurityLevel RequiredSecurity { get; set; }

        /// <summary>
        /// The row is shown only while these are met - an axis a machine may or
        /// may not have fitted. The Axis Monitor side page follows the same
        /// conditions: it lists the axes whose conditions are met.
        /// </summary>
        [Category(" Triggers")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        public SystemCondition VisibilityConditions { get; set; } = SystemCondition.None;

        // ==================================================================
        // LAYOUT
        // ==================================================================
        // Column widths, in one place so every row on a page lines up and the
        // header can be drawn from the same numbers.
        // WJog widened 25% (40 -> 50) - easier to hit, and the size every
        // manual-page axis unit copies so all axis controls look alike.
        // Meters narrow (they show "100 %" at most) and as tall as the position
        // and the checkbox they now sit beside.
        public const int WAxis = 132, WJog = 50,
                         WPosition = 120, WSetting = 86, WCommand = 44,
                         WCheck = 44, WMeter = 76;
        public const int RowHeight = 40, Gap = 4;

        /// <summary>
        /// Space between GROUPS of related columns. The row reads as
        ///
        ///   axis ‖ ◀ pos current ▶ ‖ min max reference calibration cal ref Z ‖ codes ‖ travel backlash
        ///
        /// because those are the things an engineer sets together. Sixteen evenly
        /// spaced fields is a row nobody can scan; the gaps do the grouping that a
        /// box around each set would otherwise cost vertical space to draw.
        /// </summary>
        public const int GroupGap = 18;

        /// <summary>Width of one alarm-code monitor.</summary>
        public const int WCode = 72;

        private readonly Label _axis = new Label();
        private NumericInputHMI _min, _max, _reference, _calibration;
        private ButtonHMI _back, _forward, _calibrate;
        private CheckBoxHMI _goRef;
        private NumericDisplayHMI _position;
        private MeterHMI _current, _zdist;
        private HexCodeHMI _axisAlarm, _axisWarning, _servoAlarm;
        private NumericInputHMI _travel, _backlash;

        /// <summary>Where each column of this row is, at factor 1: key -> (left, width).</summary>
        private readonly System.Collections.Generic.Dictionary<string, Point> _columns =
            new System.Collections.Generic.Dictionary<string, Point>();

        /// <summary>The columns of this row (for the page's headings and for tests).</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public System.Collections.Generic.IDictionary<string, Point> Columns { get { return _columns; } }

        private void Column(string key, int x, int w) { _columns[key] = new Point(x, w); }

        public AxisSettingHMI()
        {
            PlcNumber = 1;
            Address = "";
            AxisNameKey = "X";
            CommandBackAddress = CommandForwardAddress = PositionAddress = "";
            MinLimitAddress = MaxLimitAddress = "";
            ReferenceAddress = CalibrationAddress = "";
            CalibrateAddress = GoToReferenceAddress = "";
            CurrentAddress = ZDistanceAddress = "";
            EntryMinimumAddress = EntryMaximumAddress = "";
            ErrorAddress = "";
            ReferenceSensorAddress = "";
            AxisAlarmAddress = AxisWarningAddress = ServoAlarmAddress = "";
            RealPositionAddress = TravelPerRevolutionAddress = BacklashAddress = "";
            PositionDataType = DataTypeEnum.Bin_32;
            SettingDataType = DataTypeEnum.Bin_32;
            Divisor = 10000;
            DecimalPlaces = 2;
            Digits = 8;
            Suffix = "mm";
            EntryMinimum = double.NaN;      // no limit unless one is given
            EntryMaximum = double.NaN;
            CurrentDivisor = 1;
            CurrentFullScale = 100;
            ZDistanceDivisor = 1000;
            ZDistanceFullScale = 10;
            RequiredSecurity = SecurityLevel.Maintenance;

            Height = RowHeight;
            Width = 1000;
            BackColor = Color.White;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        }

        /// <summary>
        /// Builds the row from whatever addresses are currently set.
        ///
        /// Called automatically once the control has a handle, so a control DROPPED
        /// ON A FORM BY THE DESIGNER lays itself out with no code behind it. It is
        /// public as well because a page that sets addresses at run time needs to
        /// ask for a rebuild afterwards.
        ///
        /// Separate from the constructor because which fields exist depends on
        /// which addresses are filled, and at construction time none are.
        /// </summary>
        public void BuildRow()
        {
            Controls.Clear();
            _columns.Clear();
            int x = 0;

            // NO COLON. These read as a column of row headings, not as captions in a
            // form - a colon after each one turns a table into a list of labels.
            _axis.Text = LanguageManager.GetString(AxisNameKey ?? "", AxisNameKey ?? "");
            _axis.SetBounds(x, 8, WAxis, 24);
            // RIGHT, so the name sits against the controls it labels. Left-aligned
            // it drifted away from its own row on the wider axis names and read as
            // belonging to the column of names rather than to this axis.
            _axis.TextAlign = ContentAlignment.MiddleRight;
            _axis.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            Controls.Add(_axis);
            Column("Axis", x, WAxis);
            x += WAxis + Gap;

            x += GroupGap;
            _back = AddJog(CommandBackAddress, ButtonGlyphs.Left, ref x);

            if (!string.IsNullOrEmpty(PositionAddress))
            {
                _position = new NumericDisplayHMI
                {
                    PlcNumber = PlcNumber,
                    Address = PositionAddress,
                    DataType = PositionDataType,
                    Divisor = Divisor,
                    DecimalPlaces = DecimalPlaces,
                    Digits = Digits,
                    Suffix = Suffix,
                    UnitType = PositionUnitType,
                    // The real (encoder) position, in the tooltip with the difference.
                    ComparedAddress = (RealPositionAddress ?? "").Trim(),
                    // The position is the number the operator watches while
                    // jogging, so it is the one thing on the row drawn large.
                    Font = new Font("Segoe UI", 12F, FontStyle.Bold)
                };
                _position.SetBounds(x, 4, WPosition, RowHeight - 8);
                Controls.Add(_position);
            }
            Column("Position", x, WPosition);
            x += WPosition + Gap;

            // THE CURRENT BESIDE THE POSITION: the two numbers that change while
            // the axis moves, read together. Its column exists when any axis on
            // the page has a current - an axis without one leaves the space.
            if (PageHas(a => a.CurrentAddress))
            {
                Column("Current", x, WMeter);
                _current = AddMeter(CurrentAddress, CurrentDivisor, CurrentFullScale, ref x, current: true);
            }

            _forward = AddJog(CommandForwardAddress, ButtonGlyphs.Right, ref x);
            x += GroupGap;

            // MIN AND MAX BOUND EACH OTHER: the minimum may not be typed above the
            // maximum, nor the maximum below the minimum. They are the two ends of
            // one travel, and a crossed pair is a machine that can reach nothing.
            // EVERY COLUMN BELOW IS THE PAGE'S: there when any axis row on the
            // page has it (a row without it leaves the space, so the columns line
            // up), and not at all when none has - no empty column with a heading.
            if (PageHas(a => a.MinLimitAddress))
            {
                Column("Min", x, WSetting);
                _min = AddSetting(MinLimitAddress, ref x, WSetting, upperTag: MaxLimitAddress);
            }
            if (PageHas(a => a.MaxLimitAddress))
            {
                Column("Max", x, WSetting);
                _max = AddSetting(MaxLimitAddress, ref x, WSetting, lowerTag: MinLimitAddress);
            }
            // Reference and calibration are bounded by the axis's own travel.
            if (PageHas(a => a.ReferenceAddress))
            {
                Column("Reference", x, WSetting);
                _reference = AddSetting(ReferenceAddress, ref x, WSetting, clampToTravel: true);
            }
            if (PageHas(a => a.CalibrationAddress))
            {
                Column("Calibration", x, WSetting);
                _calibration = AddSetting(CalibrationAddress, ref x, WSetting, clampToTravel: true);
            }

            // Calibrate rewrites the axis's zero, so it is SET and draws the
            // latched mark - it must not look like the jog buttons beside it.
            if (PageHas(a => a.CalibrateAddress))
            {
                Column("Cal", x, WCommand);
                _calibrate = AddCommand(CalibrateAddress, BitActionType.SET, ref x);
            }
            if (PageHas(a => a.GoToReferenceAddress))
            {
                x += 8;                  // air between the button and the checkbox
                Column("Ref", x, WCheck);
                _goRef = AddRefCheck(ref x);
            }

            // THE Z DISTANCE BESIDE THE REFERENCE: it says how good the reference
            // is - how far past the Z pulse the axis stopped.
            if (PageHas(a => a.ZDistanceAddress))
            {
                Column("ZDistance", x, WMeter);
                _zdist = AddMeter(ZDistanceAddress, ZDistanceDivisor, ZDistanceFullScale, ref x, current: false);
            }
            x += GroupGap;

            // Each code cell opens ITS code's entry of the manual while it is not
            // zero - the axis alarm and warning in the motion module's manual
            // (printed "1904H", as that manual does), the servo alarm in the
            // drive's ("16.3", as the amplifier's display does).
            if (PageHas(a => a.AxisAlarmAddress))
            {
                Column("AxisAlarm", x, WCode);
                _axisAlarm = AddCode(AxisAlarmAddress, AxisCodeKind.AxisError, ref x);
            }
            if (PageHas(a => a.AxisWarningAddress))
            {
                Column("AxisWarning", x, WCode);
                _axisWarning = AddCode(AxisWarningAddress, AxisCodeKind.AxisWarning, ref x,
                                       HexCodeHMI.CodeSeverity.Warning);
            }
            if (PageHas(a => a.ServoAlarmAddress))
            {
                Column("ServoAlarm", x, WCode);
                _servoAlarm = AddCode(ServoAlarmAddress, AxisCodeKind.ServoAlarm, ref x);
            }

            // Rarely set from the HMI: last, and only on a page where some axis has them.
            bool travel = PageHas(a => a.TravelPerRevolutionAddress), backlash = PageHas(a => a.BacklashAddress);
            if (travel || backlash) x += GroupGap;
            if (travel)
            {
                Column("TravelPerRev", x, WSetting);
                _travel = AddFineSetting(TravelPerRevolutionAddress, "Tip_TravelPerRev",
                    "Travel per motor revolution, in the axis's unit to its last count.", ref x);
            }
            if (backlash)
            {
                Column("Backlash", x, WSetting);
                _backlash = AddFineSetting(BacklashAddress, "Tip_Backlash",
                    "Backlash, in the axis's unit to its last count.", ref x);
            }

            Width = Math.Max(Width, x);
            AlignHeaders();
        }

        /// <summary>
        /// Whether this axis - or any other axis row on the same page - has the
        /// address: the page's rows share their columns, so a column exists for
        /// all of them or for none.
        /// </summary>
        private bool PageHas(Func<AxisSettingHMI, string> address)
        {
            if (!string.IsNullOrWhiteSpace(address(this))) return true;
            if (Parent == null) return false;
            foreach (Control c in Parent.Controls)
            {
                var row = c as AxisSettingHMI;
                if (row != null && !string.IsNullOrWhiteSpace(address(row))) return true;
            }
            return false;
        }

        /// <summary>
        /// A small setting of the axis (backlash, travel per revolution): in the
        /// axis's unit and divisor like its limits, but to the LAST COUNT - a
        /// backlash of 150 counts at a divisor of 10000 is 0.0150 mm, which the
        /// row's 2 decimals would show as 0.02 - and with its unit, as it is not
        /// the position's quantity the column beside it names.
        /// </summary>
        private NumericInputHMI AddFineSetting(string address, string tipKey, string tipFallback, ref int x)
        {
            NumericInputHMI n = null;
            if (!string.IsNullOrWhiteSpace(address))
            {
                int decimals = BaseNumericHMI.ResolutionDecimals(Divisor, DecimalPlaces);
                n = new NumericInputHMI
                {
                    PlcNumber = PlcNumber,
                    Address = address.Trim(),
                    DataType = SettingDataType,
                    Divisor = Divisor,
                    DecimalPlaces = decimals,
                    Digits = Math.Max(Digits, decimals + 4),
                    Suffix = Suffix,
                    ShowSuffix = true,
                    // Converted with the position (inch on an inch panel), as the limits are.
                    UnitType = PositionUnitType,
                    RequiredSecurity = RequiredSecurity,
                    LogAction = true,
                    ToolTipTextKey = LanguageManager.GetString(tipKey, tipFallback)
                };
                n.SetBounds(x, 6, WSetting, RowHeight - 12);
                Controls.Add(n);
            }
            x += WSetting + Gap;
            return n;
        }

        // ==================================================================
        // THE PAGE'S COLUMN HEADINGS
        // ==================================================================

        /// <summary>
        /// The column headings of a page of axis rows, known by their designer
        /// text: key, translation key, then the texts a designer may have used.
        /// </summary>
        private static readonly string[][] Headings =
        {
            new[] { "Axis", "Col_Axis", "Axis" },
            new[] { "Position", "Col_AxisPosition", "Position" },
            new[] { "Current", "Col_AxisCurrent", "Current" },
            new[] { "Min", "Col_MinLimit", "Min" },
            new[] { "Max", "Col_MaxLimit", "Max" },
            new[] { "Reference", "Col_Reference", "Reference" },
            new[] { "Calibration", "Col_Calibration", "Calibration" },
            new[] { "Cal", "Col_Calibrate", "Cal" },
            new[] { "Ref", "Col_SendRef", "Ref" },
            new[] { "ZDistance", "Col_ZDistance", "Z distance", "Z dist" },
            new[] { "AxisAlarm", "Col_AxisAlarm", "Axis alarm" },
            new[] { "AxisWarning", "Col_AxisWarning", "Axis warn", "Axis warning" },
            new[] { "ServoAlarm", "Col_ServoAlarm", "Servo alarm" },
            new[] { "TravelPerRev", "Col_TravelPerRev", "Travel / rev" },
            new[] { "Backlash", "Col_Backlash", "Backlash" },
        };

        private const string HeadingTag = "AxisHeading:";

        /// <summary>
        /// PUTS THE PAGE'S COLUMN HEADINGS OVER THE COLUMNS, from the top row.
        ///
        /// The headings were labels placed by hand in each machine's designer, at
        /// the x the columns had when the page was drawn - and they stayed there
        /// when the row changed (H800's and MD3020's still named a "Backlash"
        /// column the row no longer had). Now each heading label above the first
        /// row is recognised by its text, moved over its column, translated, and
        /// hidden when no row has that column; a column with no heading of its own
        /// (travel per revolution, backlash) gets one in the same style.
        /// </summary>
        public void AlignHeaders()
        {
            var page = Parent;
            if (page == null) return;
            AxisSettingHMI top = null;
            foreach (Control c in page.Controls)
            {
                var row = c as AxisSettingHMI;
                if (row != null && (top == null || row.Top < top.Top)) top = row;
            }
            if (top != this) return;                         // the top row speaks for the page

            var labels = new System.Collections.Generic.List<Label>();
            foreach (Control c in page.Controls)
                if (c.GetType() == typeof(Label) && c.Bottom <= Top + 2) labels.Add((Label)c);
            if (labels.Count == 0) return;                   // a page without headings

            Label style = null;
            var byKey = new System.Collections.Generic.Dictionary<string, Label>();
            foreach (var l in labels)
            {
                string key = l.Tag is string && ((string)l.Tag).StartsWith(HeadingTag) ? ((string)l.Tag).Substring(HeadingTag.Length) : null;
                if (key == null)
                    foreach (var h in Headings)
                        for (int i = 2; i < h.Length && key == null; i++)
                            if (string.Equals((l.Text ?? "").Trim(), h[i], StringComparison.OrdinalIgnoreCase)) key = h[0];
                if (key == null || byKey.ContainsKey(key)) continue;
                l.Tag = HeadingTag + key;
                byKey[key] = l;
                if (style == null) style = l;
            }
            if (style == null) return;

            foreach (var h in Headings)
            {
                Point col;
                bool shown = _columns.TryGetValue(h[0], out col);
                Label l;
                if (!byKey.TryGetValue(h[0], out l))
                {
                    if (!shown || (h[0] != "TravelPerRev" && h[0] != "Backlash" && h[0] != "Current")) continue;
                    l = new Label
                    {
                        Tag = HeadingTag + h[0],
                        AutoSize = false,
                        TextAlign = style.TextAlign,
                        Font = style.Font,
                        ForeColor = style.ForeColor,
                        BackColor = style.BackColor,
                        Top = style.Top,
                        Height = style.Height
                    };
                    page.Controls.Add(l);
                    byKey[h[0]] = l;
                }
                l.Visible = shown;
                if (!shown) continue;
                l.Left = Left + UiScale.For(this, col.X);
                l.Width = UiScale.For(this, col.Y);
                l.Text = LanguageManager.GetString(h[1], h[2]);
            }
        }

        /// <param name="clampToTravel">
        /// True for reference and calibration, whose sensible range is ALREADY
        /// known: it is the axis's own min and max. Typing them again per field
        /// is work that can only introduce a disagreement, and a reference value
        /// outside the travel limits is a machine that homes into a hard stop.
        /// </param>
        private NumericInputHMI AddSetting(string address, ref int x, int w,
                                           bool clampToTravel = false,
                                           string lowerTag = null, string upperTag = null)
        {
            NumericInputHMI n = null;
            if (!string.IsNullOrEmpty(address))
            {
                n = new NumericInputHMI
                {
                    PlcNumber = PlcNumber,
                    Address = address,
                    DataType = SettingDataType,
                    Divisor = Divisor,
                    DecimalPlaces = DecimalPlaces,
                    Digits = Digits,
                    Suffix = "",
                    // No suffix DRAWN: the position beside them already shows the
                    // unit, and four fields repeating "mm" is noise.
                    ShowSuffix = false,
                    // The SAME unit as the position, deliberately. A limit and a
                    // reference are lengths on the same axis: switching the HMI to
                    // inches has to convert them too, or the limits silently start
                    // meaning something different from the position they bound.
                    // They carry no suffix because the column heading already says
                    // what they are - but the unit is what drives the conversion,
                    // not the suffix.
                    UnitType = PositionUnitType,
                    RequiredSecurity = RequiredSecurity,
                    LogAction = true
                };
                // OutOfLimitsWarning is what makes the limit visible: the field
                // blinks when its value falls outside, which is the whole point of
                // bounding a reference by the travel it has to live in.
                n.OutOfLimitsWarning = true;

                if (IsAngle)
                {
                    // AN ANGLE: 0 - 360, wrap-aware (see IsAngle).
                    n.IsAngle = true;
                    if (clampToTravel && !string.IsNullOrEmpty(MinLimitAddress)
                                      && !string.IsNullOrEmpty(MaxLimitAddress))
                    {
                        // Reference and calibration within the travel - through
                        // zero when the minimum is above the maximum.
                        n.MinLimit = new Kaban.Core.Models.DynamicValue(LimitSourceType.Tag, 0, MinLimitAddress);
                        n.MaxLimit = new Kaban.Core.Models.DynamicValue(LimitSourceType.Tag, 0, MaxLimitAddress);
                    }
                    else
                    {
                        // The minimum and maximum themselves: anywhere on the
                        // turn, and NOT bounded by each other - 270 above 45 is
                        // how a range through zero is written.
                        n.MinLimit = new Kaban.Core.Models.DynamicValue();
                        n.MaxLimit = new Kaban.Core.Models.DynamicValue();
                    }
                }
                else if (clampToTravel && !string.IsNullOrEmpty(MinLimitAddress)
                                       && !string.IsNullOrEmpty(MaxLimitAddress))
                {
                    // Read the limits LIVE from the axis's own MIN/MAX words, so
                    // raising a travel limit immediately widens what may be typed
                    // into the reference - no second place to remember to change.
                    n.MinLimit = new Kaban.Core.Models.DynamicValue(
                        LimitSourceType.Tag, 0, MinLimitAddress);
                    n.MaxLimit = new Kaban.Core.Models.DynamicValue(
                        LimitSourceType.Tag, 0, MaxLimitAddress);
                }
                else
                {
                    // A device beats a constant: see EntryMinimumAddress.
                    n.MinLimit = !string.IsNullOrEmpty(lowerTag)
                        ? new Kaban.Core.Models.DynamicValue(LimitSourceType.Tag, 0, lowerTag)
                        : !string.IsNullOrEmpty(EntryMinimumAddress)
                            ? new Kaban.Core.Models.DynamicValue(LimitSourceType.Tag, 0, EntryMinimumAddress)
                            : double.IsNaN(EntryMinimum)
                                ? new Kaban.Core.Models.DynamicValue()   // none
                                : new Kaban.Core.Models.DynamicValue(LimitSourceType.Static, EntryMinimum, "");

                    n.MaxLimit = !string.IsNullOrEmpty(upperTag)
                        ? new Kaban.Core.Models.DynamicValue(LimitSourceType.Tag, 0, upperTag)
                        : !string.IsNullOrEmpty(EntryMaximumAddress)
                            ? new Kaban.Core.Models.DynamicValue(LimitSourceType.Tag, 0, EntryMaximumAddress)
                            : double.IsNaN(EntryMaximum)
                                ? new Kaban.Core.Models.DynamicValue()   // none
                                : new Kaban.Core.Models.DynamicValue(LimitSourceType.Static, EntryMaximum, "");
                }
                n.SetBounds(x, 6, w, RowHeight - 12);
                Controls.Add(n);
            }
            x += w + Gap;
            return n;
        }

        [Category(" Movement"), Description("Vertical axis: the jog arrows become up/down.")]
        public bool VerticalMovement { get; set; }

        private ButtonHMI AddJog(string address, string glyph, ref int x)
        {
            ButtonHMI b = null;
            if (!string.IsNullOrEmpty(address))
            {
                bool back = glyph == ButtonGlyphs.Left;
                b = new ButtonHMI
                {
                    PlcNumber = PlcNumber,
                    Direction = VerticalMovement
                        ? (back ? ButtonGlyphs.Direction.Down : ButtonGlyphs.Direction.Up)
                        : (back ? ButtonGlyphs.Direction.Left : ButtonGlyphs.Direction.Right),
                    RequiredSecurity = SecurityLevel.Operator,
                    LogAction = true
                };
                b.BitActions.Add(new Kaban.Core.Models.BitAction
                {
                    Address = address,
                    Action = BitActionType.Momentary
                });

                // The reference sensor on its own side's button, on the outer edge
                // like every unit's lamp - so homing can be watched from this row too.
                if (!string.IsNullOrEmpty(ReferenceSensorAddress) &&
                    (back ? ReferenceSensorSide == AxisReferenceSide.Back : ReferenceSensorSide == AxisReferenceSide.Forward))
                {
                    b.Lamp1Address = ReferenceSensorAddress;
                    b.LampDirection = back ? ButtonHMI.LampDirectionPosition.Left : ButtonHMI.LampDirectionPosition.Right;
                }
                b.SetBounds(x, 4, WJog, RowHeight - 8);
                Controls.Add(b);
                b.ApplyTranslation();
            }
            x += WJog + Gap;
            return b;
        }

        private ButtonHMI AddCommand(string address, BitActionType action, ref int x)
        {
            ButtonHMI b = null;
            if (!string.IsNullOrEmpty(address))
            {
                b = new ButtonHMI
                {
                    PlcNumber = PlcNumber,
                    // No caption: ButtonHMI draws the action's own mark, and for
                    // Alternate it switches with the bit so the button always shows
                    // what the next press will do.
                    FallbackText = "",
                    RequiredSecurity = RequiredSecurity,
                    LogAction = true
                };
                b.BitActions.Add(new Kaban.Core.Models.BitAction
                {
                    Address = address,
                    Action = action
                });
                b.SetBounds(x, 4, WCommand, RowHeight - 8);
                Controls.Add(b);
                b.ApplyTranslation();
            }
            x += WCommand + Gap;
            return b;
        }

        /// <summary>
        /// A meter, banded for what it measures.
        ///
        /// Motor current RISES into trouble; the Z-signal distance wants to sit in
        /// the MIDDLE of the revolution, so both its ends are bad. They are not the
        /// same judgement and cannot share a threshold pair.
        /// </summary>
        private MeterHMI AddMeter(string address, double divisor, double fullScale,
                                  ref int x, bool current)
        {
            MeterHMI m = null;
            if (!string.IsNullOrEmpty(address))
            {
                m = new MeterHMI
                {
                    PlcNumber = PlcNumber,
                    Address = address,
                    Divisor = divisor <= 0 ? 1 : divisor,
                    Caption = ""
                };
                if (current) m.UseCurrentBands(); else m.UseZDistanceBands();
                if (fullScale > 0) m.ScaleMaximum = fullScale;
                // As tall as the position and the checkbox it sits beside.
                m.SetBounds(x, 4, WMeter, RowHeight - 8);
                Controls.Add(m);
            }
            x += WMeter + Gap;
            return m;
        }

        private HexCodeHMI AddCode(string address, AxisCodeKind kind, ref int x,
                                   HexCodeHMI.CodeSeverity severity = HexCodeHMI.CodeSeverity.Alarm)
        {
            HexCodeHMI c = null;
            if (!string.IsNullOrEmpty(address))
            {
                c = new HexCodeHMI
                {
                    PlcNumber = PlcNumber,
                    Address = address,
                    Caption = "",
                    Severity = severity,
                    CodeKind = kind,
                    UnitName = LanguageManager.GetString(AxisNameKey ?? "", AxisNameKey ?? "")
                };
                c.SetBounds(x, 6, WCode, RowHeight - 12);
                Controls.Add(c);
            }
            x += WCode + Gap;
            return c;
        }

        private CheckBoxHMI AddRefCheck(ref int x)
        {
            CheckBoxHMI c = null;
            if (!string.IsNullOrEmpty(GoToReferenceAddress))
            {
                c = new CheckBoxHMI
                {
                    PlcNumber = PlcNumber,
                    Address = GoToReferenceAddress,
                    // NO caption text. The row already carries the axis name and
                    // the column already says "Ref", so a label here repeats both
                    // and - being wider than its column - covers the meters beside
                    // it. The tooltip carries the detail instead.
                    DescriptionText = "",
                    RequiredSecurity = RequiredSecurity,
                    LogAction = true,
                    // The PLC asking for a reference: the box blinks red.
                    AlertAddress = (HomeRequestAddress ?? "").Trim()
                };
                // Exactly the Cal button's footprint: the two sit side by side and
                // a checkbox even slightly smaller reads as an afterthought.
                c.SetBounds(x, 4, WCheck, RowHeight - 8);
                Controls.Add(c);
            }
            x += WCheck + Gap;
            return c;
        }


        /// <summary>
        /// Lays the row out as soon as the control has a handle, so one dropped
        /// on a form BY THE DESIGNER needs no code behind it - which is the point
        /// of this being a control rather than a page generator.
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Controls.Count == 0) BuildRow();
            if (!_languageHooked)
            {
                _languageHooked = true;
                LanguageManager.LanguageChanged += OnLanguageChanged;
            }
        }

        private bool _languageHooked;

        /// <summary>The axis name and the page's headings in the new language.</summary>
        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            _axis.Text = LanguageManager.GetString(AxisNameKey ?? "", AxisNameKey ?? "");
            AlignHeaders();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _languageHooked) LanguageManager.LanguageChanged -= OnLanguageChanged;
            base.Dispose(disposing);
        }

        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            // Children are ordinary HMI controls, so the scanner already drives
            // them; the row itself only follows its visibility conditions.
            bool vis = VisibilityConditions == SystemCondition.None || checkGlobal(VisibilityConditions);
            if (Visible != vis) Visible = vis;
        }
    }

}

