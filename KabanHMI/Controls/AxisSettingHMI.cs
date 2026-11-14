using System;
using System.Linq;
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

        [Category(" Movement"), Description("Live axis position. Divided by the row's Divisor, as its limits, reference and calibration.")]
        public string PositionAddress { get; set; }

        [Category(" Movement")]
        public DataTypeEnum PositionDataType { get; set; }

        /// <summary>
        /// What the position is divided by: the row's Divisor. The position is kept by
        /// the motion module in the same count as its limits, reference and
        /// calibration (15 October: "the axis positions are all formatted same as the
        /// related parameters"), so it has no divisor of its own any more - the one it
        /// had (PositionDivisor) is the load-side encoder's now.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double PositionScale { get { return Divisor > 0 ? Divisor : 1; } }

        // ---- the load-side encoder ----
        // 15 October: "the load side encoder has different format because it is
        // connected to PLC itself, not to the motion module" - its own category, its
        // own divisor.

        /// <summary>
        /// The LOAD-SIDE ENCODER's value (a linear scale on the axis), read by the PLC
        /// itself - not by the motion module. Its column is the LOAD POSITION (22 October:
        /// "the Position column's caption a multiple selection, so the user can see every
        /// kind of position side by side"), chosen in the Position heading's list beside the
        /// Position and the servo encoder's value (the simple motion module's "Real current
        /// value", Md.101) - in the axis's unit to the last count of its divisor (4
        /// decimals for 10000). (It was LoadEncoderValueAddress, and RealPositionAddress
        /// before that; until 22 October it was shown in the position's tooltip.)
        /// </summary>
        [Category(" Load Side Encoder"), DefaultValue(""),
         Description("The load-side encoder's value (read by the PLC itself, in its own count): the Load Position column, chosen in the Position heading's list.")]
        public string LoadSideEncoderAddress { get; set; }

        private double? _loadSideEncoderDivisor;

        /// <summary>
        /// The load-side encoder's raw-to-display divisor, when the PLC counts it
        /// differently from the motion module. Usually it does not: the PLC multiplies
        /// the encoder's value to the module's count for its own calculations (FA2010's
        /// Robot X: D22 / 10000, as W0). So until it is set it IS the row's Divisor -
        /// shown so in the Properties window (not bold), not written into the page, and
        /// following the Divisor when that changes (15 October: "get its value from
        /// Divisor when it is null, so the user wouldn't have to enter it again").
        /// Right-click > Reset, or 0, goes back to that.
        /// </summary>
        [Category(" Load Side Encoder"),
         Description("Raw-to-display divisor of the load-side encoder's value. Not set: the row's Divisor (the usual case - the PLC scales the encoder to the axis's count). Reset or 0: back to the Divisor.")]
        public double LoadSideEncoderDivisor
        {
            get { return _loadSideEncoderDivisor ?? PositionScale; }
            set { _loadSideEncoderDivisor = value > 0 ? value : (double?)null; }
        }

        /// <summary>The designer writes the encoder's divisor only when one was set (not the Divisor it follows).</summary>
        public bool ShouldSerializeLoadSideEncoderDivisor() { return _loadSideEncoderDivisor.HasValue; }

        /// <summary>Back to following the row's Divisor (the Properties window's Reset).</summary>
        public void ResetLoadSideEncoderDivisor() { _loadSideEncoderDivisor = null; }

        /// <summary>True when the encoder has a divisor of its own (not the row's Divisor).</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HasOwnLoadSideEncoderDivisor { get { return _loadSideEncoderDivisor.HasValue; } }

        /// <summary>What the load-side encoder's value is divided by.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double LoadSideEncoderScale { get { return LoadSideEncoderDivisor > 0 ? LoadSideEncoderDivisor : 1; } }

        /// <summary>Kept so older designer files compile; forwards to LoadSideEncoderAddress.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public string LoadEncoderValueAddress { get { return LoadSideEncoderAddress; } set { LoadSideEncoderAddress = value; } }

        /// <summary>Kept so older designer files compile; forwards to LoadSideEncoderAddress.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public string RealPositionAddress { get { return LoadSideEncoderAddress; } set { LoadSideEncoderAddress = value; } }

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
        [Category(" Value"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
         Description("The lowest value that may be typed into the row's settings: a number (-500), or the device holding it (D318), " +
                     "read live. Empty: no bound but the field's digits.")]
        public Kaban.Core.Models.DynamicValue EntryMinimum { get; set; } = new Kaban.Core.Models.DynamicValue();

        [Category(" Value"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
         Description("The highest value that may be typed into the row's settings: a number (6500), or the device holding it (D320), " +
                     "read live. Empty: no bound but the field's digits.")]
        public Kaban.Core.Models.DynamicValue EntryMaximum { get; set; } = new Kaban.Core.Models.DynamicValue();

        // ONE PROPERTY EACH, a number or a device (30 September). There were two
        // for each bound - EntryMinimum (a number) and EntryMinimumAddress (a
        // device that overrode it) - so a page could say two different things
        // and only one of them counted. A DynamicValue is either, as a numeric
        // field's MinLimit / MaxLimit are: type 250 or D318 in the same box.

        /// <summary>
        /// This axis's number on its machine. A manual-page AxisUnitHMI with the
        /// same number takes everything else from this row - addresses, scaling,
        /// name, error bit, reference sensor - so none of it is entered twice.
        /// </summary>
        [Category(" Device"), DefaultValue(0), Description("The axis's index on the HMI. An AxisUnitHMI with this number (its AxisNumber) mirrors this row.")]
        public int HMIAxisIndex { get; set; }

        /// <summary>Kept so older designer files compile; forwards to HMIAxisIndex.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public int AxisNumber { get { return HMIAxisIndex; } set { HMIAxisIndex = value; } }

        // ==================================================================
        // THE SIMPLE MOTION MODULE (read straight from its buffer memory)
        // ==================================================================

        /// <summary>
        /// The simple motion module this axis is on, numbered as GX Works3 numbers
        /// it - the n of Un\G (U1 for the first module). 0: none. With
        /// MotionAxisIndex, the Axis Settings page can show the module's own values
        /// of this axis (codes, status, flags, positions, speeds, its parameters)
        /// - picked on the page's Motion monitor, read straight from the module.
        /// </summary>
        [Category(" Device"), DefaultValue(0),
         Description("The simple motion module's number as GX Works3 numbers it (the n of Un\\G; 1 for U1). 0: none.")]
        public int MotionNumber { get; set; }

        /// <summary>This axis's number on its simple motion module: 1 - 8 (FX5-80SSC-S), 1 - 4 (FX5-40SSC-S). 0: none.</summary>
        [Category(" Device"), DefaultValue(0),
         Description("This axis's number on the simple motion module: 1 - 8. 0: none.")]
        public int MotionAxisIndex { get; set; }

        /// <summary>Kept so the designer files of 5 October compile; forward to MotionNumber / MotionAxisIndex.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public int MotionModuleNumber { get { return MotionNumber; } set { MotionNumber = value; } }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public int ModuleAxisIndex { get { return MotionAxisIndex; } set { MotionAxisIndex = value; } }

        /// <summary>True when the module and the axis on it are both set.</summary>
        [Browsable(false)]
        public bool HasMotion
        {
            get { return MotionNumber > 0 && MotionAxisIndex >= 1 && MotionAxisIndex <= SimpleMotion.MaxAxes; }
        }

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

        [Category(" Meters"), Description("The reading over this is the percentage: the motion module's motor current (Md.104) is in 0.1 % - 10 (212 is 21.2 %).")]
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

        // ---- WHEN ITS BUTTONS MAY BE USED (14 October: "EnableCondition and EnableTrigger
        // for each AxisSettingHMI button, just like CylinderUnitHMI, working with
        // AxisUnitHMI as well") - the row's for all of them (the jogs, Calibrate, the Ref
        // box), and each button's own ADDED to it: a button is only ever more restricted
        // than its row. An AxisUnitHMI of the axis takes the row's and the jogs' own.

        [Category(" Triggers")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("Its buttons (the jogs, Calibrate, the Ref box) are usable only while these are met - and so are the AxisUnitHMI's of the axis.")]
        public SystemCondition EnableConditions { get; set; } = SystemCondition.None;

        [Category(" Triggers")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("EXTRA conditions of the back (down) jog, added to the row's.")]
        public SystemCondition BackEnableConditions { get; set; } = SystemCondition.None;

        [Category(" Triggers")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(Kaban.Core.Models.PlcTriggerSorter))]
        [Description("A PLC signal that must be true before the back (down) jog can be pressed.")]
        public Kaban.Core.Models.PlcTrigger BackEnableTrigger { get; set; } = new Kaban.Core.Models.PlcTrigger();

        [Category(" Triggers")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("EXTRA conditions of the forward (up) jog, added to the row's.")]
        public SystemCondition ForwardEnableConditions { get; set; } = SystemCondition.None;

        [Category(" Triggers")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(Kaban.Core.Models.PlcTriggerSorter))]
        [Description("A PLC signal that must be true before the forward (up) jog can be pressed.")]
        public Kaban.Core.Models.PlcTrigger ForwardEnableTrigger { get; set; } = new Kaban.Core.Models.PlcTrigger();

        [Category(" Triggers")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("EXTRA conditions of the Calibrate button, added to the row's.")]
        public SystemCondition CalibrateEnableConditions { get; set; } = SystemCondition.None;

        [Category(" Triggers")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(Kaban.Core.Models.PlcTriggerSorter))]
        [Description("A PLC signal that must be true before Calibrate can be pressed.")]
        public Kaban.Core.Models.PlcTrigger CalibrateEnableTrigger { get; set; } = new Kaban.Core.Models.PlcTrigger();

        /// <summary>The row's and each button's own conditions put on the buttons built.</summary>
        private void ApplyButtonConditions()
        {
            foreach (var t in new[] { Tuple.Create(_back, BackEnableConditions, BackEnableTrigger),
                                      Tuple.Create(_forward, ForwardEnableConditions, ForwardEnableTrigger),
                                      Tuple.Create(_calibrate, CalibrateEnableConditions, CalibrateEnableTrigger) })
            {
                if (t.Item1 == null) continue;
                t.Item1.EnableConditions = EnableConditions | t.Item2;
                t.Item1.EnableTrigger = t.Item3 ?? new Kaban.Core.Models.PlcTrigger();
            }
            if (_goRef != null) _goRef.EnableConditions = EnableConditions;
        }

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
        private BaseNumericHMI _min, _max, _reference, _calibration;
        private ButtonHMI _back, _forward, _calibrate;
        private CheckBoxHMI _goRef;
        private NumericDisplayHMI _position;

        /// <summary>Where each column of this row is, at factor 1: key -> (left, width).</summary>
        private readonly System.Collections.Generic.Dictionary<string, Point> _columns =
            new System.Collections.Generic.Dictionary<string, Point>();

        /// <summary>The columns of this row (for the page's headings and for tests).</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public System.Collections.Generic.IDictionary<string, Point> Columns { get { return _columns; } }

        private void Column(string key, int x, int w) { _columns[key] = new Point(x, w); }

        public AxisSettingHMI()
        {
            // a light line between the rows, as between the settings rows (3 November)
            ParentChanged += (s, e) => UiStyle.RulesBetweenRows(Parent, c => c is AxisSettingHMI);
            PlcNumber = 1;
            AxisNameKey = "X";
            CommandBackAddress = CommandForwardAddress = PositionAddress = "";
            MinLimitAddress = MaxLimitAddress = "";
            ReferenceAddress = CalibrationAddress = "";
            CalibrateAddress = GoToReferenceAddress = "";
            CurrentAddress = ZDistanceAddress = "";
            ErrorAddress = "";
            ReferenceSensorAddress = "";
            AxisAlarmAddress = AxisWarningAddress = ServoAlarmAddress = "";
            LoadSideEncoderAddress = TravelPerRevolutionAddress = BacklashAddress = "";
            PositionDataType = DataTypeEnum.Bin_32;
            SettingDataType = DataTypeEnum.Bin_32;
            Divisor = 10000;
            DecimalPlaces = 2;
            Digits = 8;
            Suffix = "mm";
            EntryMinimum = new Kaban.Core.Models.DynamicValue();   // no limit unless one is given
            EntryMaximum = new Kaban.Core.Models.DynamicValue();
            CurrentDivisor = 10;           // the motion module's 0.1 % (19 October: "212 at the address is 21.2 %")
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
            // THE OLD ROW DISPOSED, not just taken off: a control taken off a form
            // keeps its window (parked) and its subscriptions until it is disposed,
            // and the row is built again whenever its picks change - ticking through
            // the lists on a page of 16 axes ran Windows out of window handles
            // ("Error creating window handle").
            foreach (Control old in Controls.Cast<Control>().ToList())
            {
                Controls.Remove(old);
                if (!ReferenceEquals(old, _axis)) old.Dispose();
            }
            _columns.Clear();
            _back = _forward = _calibrate = null;
            _position = null;
            _min = _max = _reference = _calibration = null;
            _goRef = null;
            var items = PickedItems();
            _itemsBuilt = items == null ? "*" : string.Join(",", ItemKeys.Where(items.Contains));
            Func<string, bool> on = k => items == null || items.Contains(k);
            int x = 0;

            // NO COLON. These read as a column of row headings, not as captions in a
            // form - a colon after each one turns a table into a list of labels.
            _axis.Text = LanguageManager.GetString(AxisNameKey ?? "", AxisNameKey ?? "");
            _axis.SetBounds(x, 8, WAxis, 24);
            // RIGHT, so the name sits against the controls it labels. Left-aligned
            // it drifted away from its own row on the wider axis names and read as
            // belonging to the column of names rather than to this axis.
            // (or LEFT, in a column of names, as View Preferences may say)
            _axis.TextAlign = ViewPreferences.AxisNamesLeft ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            _axis.Font = NameFont;
            Controls.Add(_axis);
            Column("Axis", x, WAxis);
            x += WAxis + Gap;

            // THE PLC AXIS ITEMS (picked at the top of the page): each group of
            // columns is laid out only when it is ticked - nothing ticked is all.
            // Hiding is the screen's only: the machine, the manual pages' axis
            // units and the HMI's own alarms keep every address.
            bool manual = on("Manual");
            // On the Axis Monitor the position is always there (it is what the
            // monitor is for); Manual movement adds the jog buttons around it.
            bool position = manual || MonitorMode;
            x += GroupGap;
            if (manual) _back = AddJog(CommandBackAddress, ButtonGlyphs.Left, ref x);

            // THE POSITIONS, SIDE BY SIDE (22 October): the ones ticked in the Position
            // heading's list - the Position (the module's command position), the servo
            // encoder's value, the load-side encoder's - each a column of its own.
            var positions = position ? PickedPositions() : new System.Collections.Generic.List<string>();
            _positionsBuilt = string.Join(",", positions);
            foreach (var kind in positions)
            {
                Control shown = null;
                if (kind == "Position" && !string.IsNullOrEmpty(PositionAddress))
                {
                    _position = new NumericDisplayHMI
                    {
                        PlcNumber = PlcNumber,
                        Address = PositionAddress,
                        DataType = PositionDataType,
                        Divisor = PositionScale,
                        DecimalPlaces = DecimalPlaces,
                        Digits = Digits,
                        Suffix = Suffix,
                        UnitType = PositionUnitType,
                        // The position is the number the operator watches while
                        // jogging, so it is the one thing on the row drawn large.
                        Font = PositionFont
                    };
                    shown = _position;
                }
                else if (kind == "ServoEncoder" && HasMotion && SimpleMotion.ServoEncoderItem != null)
                {
                    // the servo motor's encoder, read from the module (Md.101) - in the row's unit
                    var rowUnit = IsAngle ? SimpleMotion.RowUnit.Angle
                                : PositionUnitType == UnitType.Length ? SimpleMotion.RowUnit.Length : SimpleMotion.RowUnit.Module;
                    shown = new MotionValueHMI(PlcNumber, SimpleMotion.ServoEncoderItem, MotionNumber, MotionAxisIndex, DecimalPlaces, rowUnit)
                            { Name = "posServoEncoder", Font = PositionFont };
                }
                else if (kind == "LoadPosition" && !string.IsNullOrWhiteSpace(LoadSideEncoderAddress))
                {
                    // the load-side encoder, read by the PLC: its own divisor, to its last count
                    int decimals = BaseNumericHMI.ResolutionDecimals(LoadSideEncoderScale, DecimalPlaces);
                    shown = new NumericDisplayHMI
                    {
                        Name = "posLoad",
                        PlcNumber = PlcNumber,
                        Address = LoadSideEncoderAddress.Trim(),
                        DataType = PositionDataType,
                        Divisor = LoadSideEncoderScale,
                        DecimalPlaces = decimals,
                        Digits = Math.Max(Digits, decimals + 4),
                        Suffix = Suffix,
                        UnitType = PositionUnitType,
                        Font = PositionFont
                    };
                }
                if (shown != null)
                {
                    shown.SetBounds(x, 4, WPosition, RowHeight - 8);
                    Controls.Add(shown);
                }
                Column(kind, x, WPosition);
                x += WPosition + Gap;
            }

            // THE CURRENT BESIDE THE POSITION: the two numbers that change while
            // the axis moves, read together. Its column exists when any axis on
            // the page has a current - an axis without one leaves the space.
            if (on("Torque") && PageHas(a => a.CurrentAddress))
            {
                Column("Current", x, WMeter);
                AddMeter(CurrentAddress, CurrentDivisor, CurrentFullScale, ref x, current: true);
            }

            if (manual) _forward = AddJog(CommandForwardAddress, ButtonGlyphs.Right, ref x);
            x += GroupGap;

            // MIN AND MAX BOUND EACH OTHER: the minimum may not be typed above the
            // maximum, nor the maximum below the minimum. They are the two ends of
            // one travel, and a crossed pair is a machine that can reach nothing.
            // EVERY COLUMN BELOW IS THE PAGE'S: there when any axis row on the
            // page has it (a row without it leaves the space, so the columns line
            // up), and not at all when none has - no empty column with a heading.
            if (on("Settings"))
            {
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
                // Not on the Axis Monitor: it watches, and Calibrate is a command.
                if (!MonitorMode && PageHas(a => a.CalibrateAddress))
                {
                    Column("Cal", x, WCommand);
                    _calibrate = AddCommand(CalibrateAddress, BitActionType.SET, ref x);
                }
            }
            if (on("RefActivation") && PageHas(a => a.GoToReferenceAddress))
            {
                x += 8;                  // air between the button and the checkbox
                Column("Ref", x, WCheck);
                _goRef = AddRefCheck(ref x);
            }

            // THE Z DISTANCE BESIDE THE REFERENCE: it says how good the reference
            // is - how far past the Z pulse the axis stopped.
            if (on("ZDistance") && PageHas(a => a.ZDistanceAddress))
            {
                Column("ZDistance", x, WMeter);
                AddMeter(ZDistanceAddress, ZDistanceDivisor, ZDistanceFullScale, ref x, current: false);
            }
            x += GroupGap;

            // Each code cell opens ITS code's entry of the manual while it is not
            // zero - the axis alarm and warning in the motion module's manual
            // (printed "1904H", as that manual does), the servo alarm in the
            // drive's ("16.3", as the amplifier's display does).
            if (on("Codes"))
            {
                if (PageHas(a => a.AxisAlarmAddress))
                {
                    Column("AxisAlarm", x, WCode);
                    AddCode(AxisAlarmAddress, AxisCodeKind.AxisError, ref x);
                }
                if (PageHas(a => a.AxisWarningAddress))
                {
                    Column("AxisWarning", x, WCode);
                    AddCode(AxisWarningAddress, AxisCodeKind.AxisWarning, ref x,
                                           HexCodeHMI.CodeSeverity.Warning);
                }
                if (PageHas(a => a.ServoAlarmAddress))
                {
                    Column("ServoAlarm", x, WCode);
                    AddCode(ServoAlarmAddress, AxisCodeKind.ServoAlarm, ref x);
                }
            }

            // Rarely set from the HMI: last, and only on a page where some axis has them.
            bool travel = on("Travel") && PageHas(a => a.TravelPerRevolutionAddress),
                 backlash = on("Backlash") && PageHas(a => a.BacklashAddress);
            if (travel || backlash) x += GroupGap;
            if (travel)
            {
                Column("TravelPerRev", x, WSetting);
                AddFineSetting(TravelPerRevolutionAddress, "Tip_TravelPerRev",
                    "Travel per motor revolution, in the axis's unit to its last count.", ref x);
            }
            if (backlash)
            {
                Column("Backlash", x, WSetting);
                AddFineSetting(BacklashAddress, "Tip_Backlash",
                    "Backlash, in the axis's unit to its last count.", ref x);
            }

            AddMotionColumns(ref x);
            ApplyButtonConditions();

            // As wide as what it shows - at run time the row is narrower when items
            // are hidden, so the page scrolls no further than its columns (it kept
            // the widest it had ever been, and the Motion monitor ran off into an
            // empty page). In the designer it only grows, so a page is not changed.
            // AT THE SCREEN'S SCALE: x is in the units of factor 1, the parts are
            // scaled as they are added to a scaled row - a row built again on a
            // scaled page (the Axis Monitor's, or its picks changed) kept the width
            // of factor 1, and its right end could not be scrolled to (12 October:
            // "Axis monitor second page down scroll doesn't move to right end again").
            bool design = DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
            int width = UiScale.For(this, x);
            Width = design ? Math.Max(Width, width) : width;
            AlignHeaders();
            // in a frame scrolled sideways: the name where the frame keeps it
            var frame = AxisPageFrame.OfBody(Parent);
            if (frame != null) frame.StickNames();
        }

        // ---- the axis name stays at the left -----------------------------------------

        /// <summary>
        /// THE AXIS NAME STAYS AT THE LEFT while the rows are scrolled sideways (6
        /// October: "so the user could see the parameters owned by whom"): it is
        /// moved along its row by as much as the rows are scrolled, and drawn over
        /// the columns passing under it - and over the margin left of it, so no
        /// scrolled column shows beside it. <paramref name="scrolled"/>: how far the
        /// rows are scrolled (0: as built); <paramref name="margin"/>: where the row
        /// starts, unscrolled, in what scrolls.
        /// </summary>
        public void StickName(int scrolled, int margin)
        {
            if (_axis.Parent != this) return;
            Stick(_axis, scrolled, margin, BackColor.A == 255 ? BackColor : (Parent != null ? Parent.BackColor : Color.White), fullHeight: true);
        }

        /// <summary>
        /// A label of the Axis column on this row's terms - its name, or a heading in
        /// a panel placed as the row is (the Axis Monitor's) - kept at the left as
        /// the name is.
        /// </summary>
        public void Stick(Label l, int scrolled, int margin, Color back, bool fullHeight = false)
        {
            Point col;
            if (l == null || !_columns.TryGetValue("Axis", out col)) return;
            int home = UiScale.For(this, col.X), w = UiScale.For(this, col.Y);
            scrolled = Math.Max(0, scrolled);
            int left = scrolled == 0 ? home : Math.Max(0, scrolled - margin);
            // into the room before the next column: the name not against the field passing under
            int after = scrolled > 0 ? UiScale.For(this, Gap + GroupGap / 2) : 0;
            int right = home + scrolled + w + after;
            // The row's name over the whole height of the row while it covers the
            // columns (a field is taller than the name), as built when not.
            int top = l.Top, height = l.Height;
            if (fullHeight)
            {
                if (scrolled > 0) { top = 0; height = Height; }
                else { top = UiScale.For(this, 8); height = UiScale.For(this, 24); }
            }
            SetStuck(l, left, right, top, height, home + scrolled - left, after, back, scrolled > 0);
        }

        /// <summary>
        /// The Axis heading in the frame's heading (whose content follows the rows
        /// sideways): kept over the names. <paramref name="margin"/>: where the top
        /// row starts, unscrolled.
        /// </summary>
        internal void StickHeading(Control host, int scrolled, int margin)
        {
            Point col;
            if (host == null || !_columns.TryGetValue("Axis", out col)) return;
            var l = host.Controls.OfType<Label>().FirstOrDefault(x => (x.Tag as string) == HeadingTag + "Axis");
            if (l == null || !l.Visible) return;
            int home = margin + UiScale.For(this, col.X), w = UiScale.For(this, col.Y);
            scrolled = Math.Max(0, scrolled);
            int left = scrolled == 0 ? home : scrolled;
            int after = scrolled > 0 ? UiScale.For(this, Gap + GroupGap / 2) : 0;
            SetStuck(l, left, home + scrolled + w + after, l.Top, l.Height, home + scrolled - left, after, host.BackColor, scrolled > 0);
        }

        private sealed class StuckState { public bool On; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Label, StuckState> _stuck =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Label, StuckState>();

        /// <summary>
        /// A label of a frozen column put where it stays (the rows scrolled sideways
        /// by <paramref name="stuck"/>), opaque over what passes under it, with a
        /// thin rule at its right - an axis row's name, a cylinder row's (CylinderRowHMI).
        /// </summary>
        internal static void SetStuck(Label l, int left, int right, int top, int height, int pad, int padRight, Color back, bool stuck)
        {
            StuckState st;
            if (!_stuck.TryGetValue(l, out st))
            {
                st = new StuckState();
                _stuck.Add(l, st);
                // a thin rule at its right while the columns pass under it, as a table's frozen column
                l.Paint += (s, e) =>
                {
                    if (!st.On) return;
                    using (var pen = new Pen(UiStyle.GridRule)) e.Graphics.DrawLine(pen, l.Width - 1, 0, l.Width - 1, l.Height);
                };
            }
            bool changed = st.On != stuck;
            st.On = stuck;
            var padding = new Padding(Math.Max(0, pad), l.Padding.Top, Math.Max(0, padRight), l.Padding.Bottom);
            if (l.Padding != padding) l.Padding = padding;
            int width = Math.Max(1, right - left);
            if (l.Left != left || l.Width != width || l.Top != top || l.Height != height) l.SetBounds(left, top, width, height);
            // opaque over what passes under it
            if (stuck && back.A == 255 && l.BackColor != back) l.BackColor = back;
            if (stuck && l.Parent != null && l.Parent.Controls.GetChildIndex(l) != 0) l.BringToFront();
            if (changed) l.Invalidate();
        }

        /// <summary>True while a label of the Axis column draws its rule (the rows scrolled sideways, tests).</summary>
        public static bool IsStuck(Label l)
        {
            StuckState st;
            return l != null && _stuck.TryGetValue(l, out st) && st.On;
        }

        /// <summary>True when the name is moved along its row - the rows scrolled sideways (tests).</summary>
        internal bool NameStuck { get { return _axis.Padding.Left > 0 || _axis.Left > 0; } }
        /// <summary>The name's label (tests).</summary>
        internal Label NameLabel { get { return _axis; } }

        // ---- the PLC axis items ----------------------------------------------------

        /// <summary>
        /// The row's groups of columns, as the page's PLC axis items pick them
        /// (the order of the row): Manual movement (the jogs and the position),
        /// Torque monitor, Axis setting parameters (min, max, reference,
        /// calibration, calibrate), Reference activation, the Z distance, Alarm
        /// codes, Travel per revolution, Backlash.
        /// </summary>
        public static readonly string[] ItemKeys = { "Manual", "Torque", "Settings", "RefActivation", "ZDistance", "Codes", "Travel", "Backlash" };

        /// <summary>An item's name in the pick list.</summary>
        public static string ItemText(string key)
        {
            switch (key)
            {
                case "Manual": return LanguageManager.GetString("AxIt_Manual", "Manual movement");
                case "Torque": return LanguageManager.GetString("AxIt_Torque", "Torque monitor");
                case "Settings": return LanguageManager.GetString("AxIt_Settings", "Axis setting parameters");
                case "RefActivation": return LanguageManager.GetString("AxIt_RefActivation", "Reference activation");
                case "ZDistance": return LanguageManager.GetString("AxIt_ZDistance", "Reference sensor - encoder Z pulse distance");
                case "Codes": return LanguageManager.GetString("AxIt_Codes", "Alarm codes");
                case "Travel": return LanguageManager.GetString("AxIt_Travel", "Travel per revolution");
                case "Backlash": return LanguageManager.GetString("AxIt_Backlash", "Backlash");
                default: return key;
            }
        }

        // One font each for every row (they were made again on every build).
        private static readonly Font NameFont = UiScale.NewFont("Segoe UI", 10F);
        private static readonly Font PositionFont = UiScale.NewFont("Segoe UI", 12F, FontStyle.Bold);

        /// <summary>
        /// A value of the row on the AXIS MONITOR: shown, not entered - the
        /// monitor watches. In the row's unit, divisor and decimals, as the field
        /// on the page is.
        /// </summary>
        private BaseNumericHMI AddDisplay(string address, ref int x, int w, int decimals, int digits, bool suffix, string tip)
        {
            NumericDisplayHMI n = null;
            if (!string.IsNullOrWhiteSpace(address))
            {
                n = new NumericDisplayHMI
                {
                    PlcNumber = PlcNumber,
                    Address = address.Trim(),
                    DataType = SettingDataType,
                    Divisor = Divisor,
                    DecimalPlaces = decimals,
                    Digits = digits,
                    Suffix = suffix ? Suffix : "",
                    ShowSuffix = suffix,
                    UnitType = PositionUnitType,
                    ToolTipTextKey = tip ?? ""
                };
                n.SetBounds(x, 6, w, RowHeight - 12);
                Controls.Add(n);
            }
            x += w + Gap;
            return n;
        }

        /// <summary>
        /// On the AXIS MONITOR (the side page) rather than an Axis Settings page:
        /// its own picks (an Admin's), no headings or bar of the page's own - the
        /// monitor draws them - and the position always.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool MonitorMode { get; set; }

        private string _itemsBuilt = "*";

        /// <summary>The items in force here: null for all.</summary>
        private System.Collections.Generic.ISet<string> PickedItems()
        {
            if (MonitorMode)
            {
                // The monitor's choices are an Admin's: everyone else sees it as it was.
                if (!SecurityManager.HasAccess(SecurityLevel.Admin))
                    return new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Torque" };
                return ViewPreferences.PickOf(ViewPreferences.MonitorItems);
            }
            return ViewPreferences.PickOf(ViewPreferences.AxisItems);
        }

        /// <summary>The items some row of the page has (the pick list offers these).</summary>
        internal System.Collections.Generic.List<string> PageItems()
        {
            var rows = new System.Collections.Generic.List<AxisSettingHMI> { this };
            if (Parent != null) rows.AddRange(Parent.Controls.OfType<AxisSettingHMI>().Where(r => r != this));
            return ItemsOf(rows, MonitorMode);
        }

        /// <summary>The items these rows have between them (on the monitor the position is always there, so Manual means the jogs).</summary>
        public static System.Collections.Generic.List<string> ItemsOf(System.Collections.Generic.IEnumerable<AxisSettingHMI> rows, bool monitor = false)
        {
            var all = rows.ToList();
            Func<Func<AxisSettingHMI, string>, bool> has = f => all.Any(r => !string.IsNullOrWhiteSpace(f(r)));
            var list = new System.Collections.Generic.List<string>();
            if (has(a => a.CommandBackAddress) || has(a => a.CommandForwardAddress) || (!monitor && has(a => a.PositionAddress))) list.Add("Manual");
            if (has(a => a.CurrentAddress)) list.Add("Torque");
            if (has(a => a.MinLimitAddress) || has(a => a.MaxLimitAddress) || has(a => a.ReferenceAddress) ||
                has(a => a.CalibrationAddress) || has(a => a.CalibrateAddress)) list.Add("Settings");
            if (has(a => a.GoToReferenceAddress)) list.Add("RefActivation");
            if (has(a => a.ZDistanceAddress)) list.Add("ZDistance");
            if (has(a => a.AxisAlarmAddress) || has(a => a.AxisWarningAddress) || has(a => a.ServoAlarmAddress)) list.Add("Codes");
            if (has(a => a.TravelPerRevolutionAddress)) list.Add("Travel");
            if (has(a => a.BacklashAddress)) list.Add("Backlash");
            return list;
        }

        // ---- a copy of the row (the Axis Monitor) ------------------------------------

        private static readonly string[] NotCopied = { "MonitorMode", "AxisName", "AxisNumber", "RealPositionAddress", "LoadEncoderValueAddress", "MotionModuleNumber", "ModuleAxisIndex" };

        /// <summary>The row's settings (every property of its own), to make it again elsewhere.</summary>
        public System.Collections.Generic.Dictionary<string, object> Snapshot()
        {
            var d = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var p in typeof(AxisSettingHMI).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                                                                   System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (!p.CanRead || !p.CanWrite || p.GetIndexParameters().Length > 0 || NotCopied.Contains(p.Name)) continue;
                try { d[p.Name] = p.GetValue(this, null); } catch { }
            }
            return d;
        }

        /// <summary>A row made from a Snapshot.</summary>
        public static AxisSettingHMI FromSnapshot(System.Collections.Generic.IDictionary<string, object> settings)
        {
            var row = new AxisSettingHMI();
            if (settings == null) return row;
            foreach (var kv in settings)
            {
                var p = typeof(AxisSettingHMI).GetProperty(kv.Key);
                if (p == null || !p.CanWrite) continue;
                try { p.SetValue(row, kv.Value, null); } catch { }
            }
            return row;
        }

        /// <summary>A column's heading (the Axis Monitor draws its own): the page's words, the Motion monitor's short names.</summary>
        /// <summary>A column heading's translation key ("Col_MinLimit", "MotH_Busy"), or null.</summary>
        public static string HeadingKey(string columnKey)
        {
            if (columnKey != null && columnKey.StartsWith("Mot:", System.StringComparison.Ordinal))
            {
                var item = SimpleMotion.Find(columnKey.Substring(4));
                return item == null ? null : "MotH_" + item.Key;
            }
            foreach (var h in Headings)
                if (h[0] == columnKey) return h[1];
            return null;
        }

        /// <summary>The column a control of this row is in ("Min", "Mot:Busy"), or null.</summary>
        public string ColumnOf(Control child)
        {
            if (child == null || child.Parent != this) return null;
            if (child == _axis) return _columns.ContainsKey("Axis") ? "Axis" : null;   // moved along when scrolled
            int x = child.Left + child.Width / 2;
            foreach (var kv in _columns)
            {
                int left = UiScale.For(this, kv.Value.X), right = left + UiScale.For(this, kv.Value.Y);
                if (x >= left && x < right) return kv.Key;
            }
            return null;
        }

        public static string HeadingText(string columnKey)
        {
            if (columnKey != null && columnKey.StartsWith("Mot:", System.StringComparison.Ordinal))
            {
                var item = SimpleMotion.Find(columnKey.Substring(4));
                return item == null ? columnKey : LanguageManager.GetString("MotH_" + item.Key, ShortHeading(item));
            }
            foreach (var h in Headings)
                if (h[0] == columnKey) return LanguageManager.GetString(h[1], h[2]);
            return columnKey ?? "";
        }

        /// <summary>A column's full name for the heading's tooltip (Motion monitor columns), or null.</summary>
        public static string HeadingTip(string columnKey)
        {
            if (columnKey == null || !columnKey.StartsWith("Mot:", System.StringComparison.Ordinal)) return null;
            var item = SimpleMotion.Find(columnKey.Substring(4));
            return item == null ? null : LanguageManager.GetString(item.TitleKey, item.Title) + " (" + item.Ref + ")";
        }

        // ---- the simple motion columns -------------------------------------------

        /// <summary>The values picked at the top of the page (View Preferences), that the module has.</summary>
        private System.Collections.Generic.List<SimpleMotion.Item> PickedMotion()
        {
            var list = new System.Collections.Generic.List<SimpleMotion.Item>();
            // The monitor's are an Admin's choice; the page's the signed-in user's.
            if (MonitorMode && !SecurityManager.HasAccess(SecurityLevel.Admin)) return list;
            foreach (var k in ViewPreferences.ListOf(MonitorMode ? ViewPreferences.MonitorMotion : ViewPreferences.MotionColumns))
            {
                var item = SimpleMotion.Find(k);
                // the feed and the real current value: the Position heading's (a list of before may have them)
                if (item != null && !SimpleMotion.IsAxisPosition(item.Key) && !list.Contains(item)) list.Add(item);
            }
            return list;
        }

        // ---- the positions side by side (22 October) -------------------------------------

        /// <summary>The kinds of position a row can show, in their order.</summary>
        public static readonly string[] PositionKinds = { "Position", "ServoEncoder", "LoadPosition" };

        /// <summary>A kind of position's name (the Position heading's list, its column's heading).</summary>
        public static string PositionText(string kind)
        {
            switch (kind)
            {
                case "ServoEncoder": return LanguageManager.GetString("Col_AxisServoEncoder", "Servo Encoder Value");
                case "LoadPosition": return LanguageManager.GetString("Col_AxisLoadPosition", "Load Position");
                default: return LanguageManager.GetString("Col_AxisPosition", "Position");
            }
        }

        private string _positionsBuilt = "";

        /// <summary>
        /// The kinds the rows of the page have: the Position always, the servo encoder's value
        /// with an axis on a simple motion module, the load position with a load-side encoder.
        /// </summary>
        public System.Collections.Generic.List<string> OfferedPositions()
        {
            var list = new System.Collections.Generic.List<string> { "Position" };
            if (PageHasMotion()) list.Add("ServoEncoder");
            if (PageHas(a => a.LoadSideEncoderAddress)) list.Add("LoadPosition");
            return list;
        }

        /// <summary>The kinds of position shown: the ones ticked that the page has - nothing ticked, the Position.</summary>
        public System.Collections.Generic.List<string> PickedPositions()
        {
            // one choice for the Axis Settings pages and the Axis Monitor
            var kept = ViewPreferences.ListOf(ViewPreferences.AxisPositions);
            var offered = OfferedPositions();
            var list = PositionKinds.Where(k => offered.Contains(k) && kept.Contains(k)).ToList();
            if (list.Count == 0) list.Add("Position");
            return list;
        }

        /// <summary>What the Position heading's list says it is (the tooltip of each position heading).</summary>
        internal static string PositionsTip()
        {
            return LanguageManager.GetString("Tip_AxPositions", "The positions shown side by side - a click: tick the ones to show (the Position, the servo encoder's value, the load-side encoder's - the ones the axes have).");
        }

        /// <summary>Whether any axis row of the page is on a simple motion module.</summary>
        private bool PageHasMotion()
        {
            if (HasMotion) return true;
            if (Parent == null) return false;
            foreach (Control c in Parent.Controls)
            {
                var row = c as AxisSettingHMI;
                if (row != null && row.HasMotion) return true;
            }
            return false;
        }

        private string _motionBuilt = "";

        /// <summary>
        /// A column for each value picked, at the end of the row, read from the
        /// module (Un\G): the codes as the code cells beside them (their help
        /// behind them), the rest as MotionValueHMI. A row not on a module leaves
        /// the space, so the columns line up.
        /// </summary>
        private void AddMotionColumns(ref int x)
        {
            var picked = PageHasMotion() ? PickedMotion() : new System.Collections.Generic.List<SimpleMotion.Item>();
            _motionBuilt = string.Join(",", picked.Select(i => i.Key));
            if (picked.Count == 0) return;
            x += GroupGap;
            foreach (var item in picked)
            {
                int w = item.Kind == SimpleMotion.Kind.Code ? WCode : SimpleMotion.ColumnWidth(item);
                Column("Mot:" + item.Key, x, w);
                if (!HasMotion) { x += w + Gap; continue; }
                string device = SimpleMotion.Device(MotionNumber, SimpleMotion.AddressOf(item, MotionAxisIndex));
                if (item.Kind == SimpleMotion.Kind.Code)
                {
                    var kind = item.Key == "ServoAlarm" ? AxisCodeKind.ServoAlarm
                             : item.Key == "AxisWarning" ? AxisCodeKind.AxisWarning : AxisCodeKind.AxisError;
                    var cell = AddCode(device, kind, ref x, item.Key == "AxisWarning" ? HexCodeHMI.CodeSeverity.Warning : HexCodeHMI.CodeSeverity.Alarm);
                    cell.Name = "mot" + item.Key;
                    continue;
                }
                // positions and speeds as the row's position: degrees on an angle row,
                // the HMI's length unit on a length row
                var rowUnit = IsAngle ? SimpleMotion.RowUnit.Angle
                            : PositionUnitType == UnitType.Length ? SimpleMotion.RowUnit.Length : SimpleMotion.RowUnit.Module;
                var v = new MotionValueHMI(PlcNumber, item, MotionNumber, MotionAxisIndex, DecimalPlaces, rowUnit) { Name = "mot" + item.Key };
                v.SetBounds(x, 6, w, RowHeight - 12);
                Controls.Add(v);
                x += w + Gap;
            }
        }

        /// <summary>The motion columns picked changed: the row again, and the page read anew.</summary>
        internal void RefreshMotionColumns()
        {
            var picked = PageHasMotion() ? string.Join(",", PickedMotion().Select(i => i.Key)) : "";
            var items = PickedItems();
            string itemsNow = items == null ? "*" : string.Join(",", ItemKeys.Where(items.Contains));
            bool position = items == null || items.Contains("Manual") || MonitorMode;
            string positionsNow = position ? string.Join(",", PickedPositions()) : "";
            if ((picked == _motionBuilt && itemsNow == _itemsBuilt && positionsNow == _positionsBuilt) || Controls.Count == 0) { SyncMotionBar(); return; }
            BuildRow();
            SyncMotionBar();
            Kaban.UI.Plc.PlcScanner.NotifyContentChanged();
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
        private BaseNumericHMI AddFineSetting(string address, string tipKey, string tipFallback, ref int x)
        {
            if (MonitorMode)
            {
                int d = BaseNumericHMI.ResolutionDecimals(Divisor, DecimalPlaces);
                return AddDisplay(address, ref x, WSetting, d, Math.Max(Digits, d + 4), true, LanguageManager.GetString(tipKey, tipFallback));
            }
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
            // (the column's heading short - the list and the heading's tooltip name it in full)
            new[] { "ServoEncoder", "Col_AxisServoEncoderShort", "Servo encoder", "Servo Encoder Value" },
            new[] { "LoadPosition", "Col_AxisLoadPosition", "Load Position" },
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
            if (MonitorMode) return;                         // the Axis Monitor draws its own
            if (top != this) return;                         // the top row speaks for the page
            // NOT IN VISUAL STUDIO'S DESIGNER: the lists move the page's controls down to
            // make room, and the designer wrote those moved places into the page - 40 more
            // on every opening (16 October: the column names went from 52 to 92, Robot X
            // over Robot Y, the jog bar under the lists).
            if (!InDesigner(page)) EnsureMotionBar(page);

            // The headings: above the first row on the page - or, the page framed
            // (AxisPageFrame: its heading stays while the rows scroll), in its heading.
            var frame = AxisPageFrame.OfBody(page);
            var host = frame != null ? (Control)frame.HeadContent : page;
            // the rows' x in the heading's terms (the frame's heading follows the scroll itself)
            int rowX = frame != null ? Left - frame.Body.AutoScrollPosition.X : Left;
            var labels = new System.Collections.Generic.List<Label>();
            foreach (Control c in host.Controls)
                if (c.GetType() == typeof(Label) && (frame != null || c.Bottom <= Top + 2)) labels.Add((Label)c);
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
                    // the columns no page's designer has a heading for: made in the same style
                    if (!shown || (h[0] != "TravelPerRev" && h[0] != "Backlash" && h[0] != "Current" &&
                                   h[0] != "ServoEncoder" && h[0] != "LoadPosition" && h[0] != "Position")) continue;
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
                    host.Controls.Add(l);
                    byKey[h[0]] = l;
                }
                l.Visible = shown;
                if (!shown) continue;
                l.Left = rowX + UiScale.For(this, col.X);
                l.Width = UiScale.For(this, col.Y);
                l.Text = LanguageManager.GetString(h[1], h[2]);
            }

            // The Axis heading opens the Axes list (which rows the page shows).
            Label axisHead;
            var axesBar = BarOf(page, ItemsBarName);
            if (byKey.TryGetValue("Axis", out axisHead) && axesBar != null && axesBar.AxesPick != null)
                AxisHeadingFilter.Attach(axisHead, axesBar.AxesPick, () => OfferPageAxes(page));
            // each position's heading opens the Positions list (which kinds of position, side by side)
            if (axesBar != null && axesBar.PositionsPick != null)
            {
                var anyRow = this;
                foreach (var kind in PositionKinds)
                {
                    Label posHead;
                    if (byKey.TryGetValue(kind, out posHead))
                        AxisHeadingFilter.Attach(posHead, axesBar.PositionsPick, () => { var r = RowsOf(page).FirstOrDefault() ?? anyRow; axesBar.OfferPositions(r.OfferedPositions()); }, PositionsTip, showCount: false, minItems: 2);
                }
            }

            // THE SIMPLE MOTION COLUMNS: a heading each, in the same style - its
            // short name over the column, the whole name in its tooltip.
            foreach (Control c in host.Controls)
            {
                var tag = c.Tag as string;
                if (c is Label && tag != null && tag.StartsWith(HeadingTag + "Mot:") && !_columns.ContainsKey(tag.Substring(HeadingTag.Length)))
                    c.Visible = false;
            }
            foreach (var kv in _columns)
            {
                if (!kv.Key.StartsWith("Mot:", System.StringComparison.Ordinal)) continue;
                var item = SimpleMotion.Find(kv.Key.Substring(4));
                if (item == null) continue;
                Label l;
                if (!byKey.TryGetValue(kv.Key, out l))
                {
                    l = new Label
                    {
                        Tag = HeadingTag + kv.Key,
                        AutoSize = false,
                        AutoEllipsis = true,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = style.Font,
                        ForeColor = style.ForeColor,
                        BackColor = style.BackColor
                    };
                    host.Controls.Add(l);
                    byKey[kv.Key] = l;
                }
                l.Visible = true;
                l.SetBounds(rowX + UiScale.For(this, kv.Value.X), style.Top, UiScale.For(this, kv.Value.Y), style.Height);
                l.Text = LanguageManager.GetString("MotH_" + item.Key, ShortHeading(item));
                Kaban.UI.Controls.UiStyle.SetTip(UiStyle.Tips(), l, LanguageManager.GetString(item.TitleKey, item.Title) + " (" + item.Ref + ")");
            }
            // laid out afresh: the Axis heading where the frame keeps it, if scrolled
            if (frame != null) frame.StickNames();
        }

        /// <summary>A column's heading in English: the name, shortened where it is long.</summary>
        internal static string ShortHeading(SimpleMotion.Item item)
        {
            switch (item.Key)
            {
                case "AxisError": return "Axis error";
                case "AxisWarning": return "Axis warn";
                case "ServoAlarm": return "Servo alarm";
                case "OperationStatus": return "Status";
                case "HomeRequest": return "Home req.";
                case "HomeComplete": return "Homed";
                case "ErrorDetected": return "Error";
                case "WarningDetected": return "Warning";
                case "PositioningComplete": return "Pos. done";
                case "CommandInPosition": return "Cmd in-pos";
                case "Busy": return "BUSY";
                case "ServoReady": return "Ready";
                case "ServoOn": return "Servo ON";
                case "InPosition": return "In-pos";
                case "TorqueLimiting": return "Torq. lim.";
                case "AbsoluteLost": return "Abs. lost";
                case "ServoWarning": return "Servo warn";
                case "LowerLimit": return "Limit -";
                case "UpperLimit": return "Limit +";
                case "ProximityDog": return "Dog";
                case "FeedCurrentValue": return "Feed value";
                case "RealCurrentValue": return "Real value";
                case "DeviationCounter": return "Deviation";
                case "TargetValue": return "Target";
                case "Feedrate": return "Feedrate";
                case "CurrentSpeed": return "Speed";
                case "MotorSpeed": return "Motor speed";
                case "MotorCurrent": return "Current";
                case "TorqueLimitValue": return "Torque lim.";
                case "RegenerativeLoad": return "Regen. load";
                case "EffectiveLoad": return "Eff. load";
                case "PeakTorque": return "Peak torque";
                case "UnitSetting": return "Unit";
                case "PulsesPerRevolution": return "Pulses / rev";
                case "TravelPerRevolution": return "Travel / rev";
                case "Backlash": return "Backlash";
                case "SoftLimitUpper": return "Soft limit +";
                case "SoftLimitLower": return "Soft limit -";
                case "SpeedLimit": return "Speed limit";
                case "AccelerationTime": return "Acc. time";
                case "DecelerationTime": return "Dec. time";
                case "HomeAddress": return "Home addr.";
                case "HomingSpeed": return "Home speed";
                default: return item.Title;
            }
        }

        // ---- the bar at the top of the page: which of the module's values to show ----

        internal const string MotionBarName = "axisMotionBar", ItemsBarName = "axisItemsBar";

        /// <summary>
        /// THE BAR AT THE TOP OF THE PAGE (AxisPickBar): PLC axis items - which of
        /// the row's groups of columns to show - and, on a page with an axis on a
        /// simple motion module, the Motion monitor - which of the module's values
        /// to add as columns. Kept as the signed-in user's own (View Preferences).
        /// The page's controls move down to make room for it (the designer's page
        /// is not changed), and the bar stays in sight while the page is scrolled
        /// sideways.
        ///
        /// JUST ABOVE THE COLUMN NAMES (16 October: "I need AxisJogBar to be at the top
        /// of the page ... the item selection should be just before the column header
        /// line"): what the page has above its column names - FA2010's speed and jog
        /// step - stays at the top, and only the column names and the rows move down.
        /// </summary>
        private void EnsureMotionBar(Control page)
        {
            var frame = AxisPageFrame.OfBody(page);
            var items = BarOf(page, ItemsBarName);
            var motion = BarOf(page, MotionBarName);
            if (frame != null && (items == null || motion == null)) return;
            if (items == null || motion == null)
            {
                int top = HeadingLine(page), left = int.MaxValue;
                foreach (Control c in page.Controls) if (c.Top >= top) left = Math.Min(left, c.Left);
                if (left == int.MaxValue) left = 0;
                int shift = UiScale.For(page, 40);
                foreach (Control c in page.Controls) if (c.Top >= top) c.Top += shift;

                // PLC axis items: an ordinary part of the page, scrolling with it - and
                // before it the Axes list: which rows the page shows.
                items = new AxisPickBar(ViewPreferences.AxisItems, ViewPreferences.MotionColumns, items: true, motion: false,
                                        axesKey: ViewPreferences.AxesShown, positionsKey: ViewPreferences.AxisPositions)
                        { Name = ItemsBarName, BackColor = page.BackColor };
                var anyRow = this;
                var bar = items;
                items.AxesChanged += (s, e) =>
                {
                    var host = anyRow.Parent;
                    if (host != null) ApplyAxisFilter(host, bar);
                };
                page.Controls.Add(items);
                items.SetBounds(Math.Max(0, left), top, UiScale.For(page, 460), UiScale.For(page, 36));
                items.Width = items.NaturalWidth;

                // The Motion monitor beside it: scrolling with the page until it
                // reaches the page's left side, then staying there (in the page's
                // own, unscrolled terms: where it stands and where the content starts).
                motion = new AxisPickBar(ViewPreferences.AxisItems, ViewPreferences.MotionColumns, items: false, motion: true)
                         { Name = MotionBarName, BackColor = page.BackColor };
                page.Controls.Add(motion);
                var scroll = page as ScrollableControl;
                int offset = scroll != null ? scroll.AutoScrollPosition.X : 0;
                int x = Math.Max(0, items.Right - offset);
                motion.SetBounds(x + offset, top, UiScale.For(page, 640), UiScale.For(page, 36));
                motion.Pin(page, x, Math.Max(0, items.Left - offset));
                // its place taken from the items list, with room between (at any screen scale)
                motion.StandRightOf(items);
                // 23 October: the flash ROM button beside the Motion monitor list, blinking while a module
                // of the page's axes has a parameter changed that its flash ROM does not hold yet
                // the rows' own page asked each time (they move into the page's frame once it is shown)
                var aRow = this;
                motion.Controls.Add(new FlashRomButton(() => PageModules(aRow.Parent)));

                // The page as a table: once it is shown, its heading (these lists and
                // the column names) stays while the rows scroll under it.
                ScheduleFreeze(page);
            }
            items.Visible = true;
            items.OfferAxes(PageAxes(page));
            items.OfferPositions(OfferedPositions());
            items.Offer(PageItems(), false);
            ApplyAxisFilter(page, items);
            bool hasMotion = PageHasMotion();
            motion.Visible = hasMotion;
            motion.Offer(PageItems(), hasMotion);
        }

        /// <summary>The simple motion modules of a page's axes: each one's station and module number.</summary>
        internal static System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<int, int>> PageModules(Control page)
        {
            if (page == null || page.IsDisposed) return new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>>();
            return RowsOf(page).Where(r => r.HasMotion).Select(r => new System.Collections.Generic.KeyValuePair<int, int>(r.PlcNumber, r.MotionNumber))
                               .Distinct().ToList();
        }

        /// <summary>True on a page being designed in Visual Studio (the page or a control of it sited in the designer).</summary>
        internal static bool InDesigner(Control c)
        {
            for (; c != null; c = c.Parent)
                if (c.Site != null && c.Site.DesignMode) return true;
            return false;
        }

        /// <summary>
        /// Where the page's column names start: the top of the plain labels just above
        /// its first row (within a heading's height of it) - or the first row itself on
        /// a page without names. What is above it is the page's own top (a speed bar).
        /// </summary>
        internal static int HeadingLine(Control page)
        {
            var rows = page.Controls.OfType<AxisSettingHMI>().Where(r => !r.MonitorMode).ToList();
            if (rows.Count == 0) return 0;
            int first = rows.Min(r => r.Top), line = first, band = UiScale.For(page, 40);
            foreach (Control c in page.Controls)
                if (c.GetType() == typeof(Label) && c.Bottom <= first + 2 && c.Bottom >= first - band) line = Math.Min(line, c.Top);
            return line;
        }

        /// <summary>The bar's ticks as kept (another user signed in, the pick changed elsewhere).</summary>
        private void SyncMotionBar()
        {
            var page = Parent;
            if (page == null || MonitorMode) return;
            foreach (var name in new[] { ItemsBarName, MotionBarName })
            {
                var bar = BarOf(page, name);
                if (bar != null) bar.Sync();
            }
            // another user signed in, the pick changed elsewhere: the rows they want
            var items = BarOf(page, ItemsBarName);
            if (items != null) ApplyAxisFilter(page, items);
        }

        // ---- the axes shown (8 October) -------------------------------------------------

        private bool _conditionsMet = true;
        /// <summary>Hidden by the page's Axes list (its conditions met or not).</summary>
        internal bool FilteredOut { get; private set; }
        private int _rank = -1;
        [ThreadStatic] private static bool _restacking;

        /// <summary>The row's key in the Axes list: its name and station ("Axis_RobotX@1").</summary>
        internal string FilterKey { get { return (string.IsNullOrEmpty(AxisNameKey) ? Name : AxisNameKey) + "@" + PlcNumber; } }

        /// <summary>The row as the page wants it: its conditions met and not hidden by the Axes list.</summary>
        internal bool Wanted { get { return _conditionsMet && !FilteredOut; } }

        /// <summary>
        /// The page's rows for its Axes list, in the page's order - not the ones whose
        /// visibility conditions are not met (16 October: ticked, they could not be seen
        /// anyway). A tick kept for one of them comes back when its conditions are met.
        /// </summary>
        private static System.Collections.Generic.List<Kaban.UI.Dashboard.PickList.Item> PageAxes(Control host)
        {
            return RowsOf(host).Where(r => r._conditionsMet).Select(r => new Kaban.UI.Dashboard.PickList.Item(r.FilterKey,
                LanguageManager.GetString(r.AxisNameKey ?? "", r.AxisNameKey ?? r.Name))).ToList();
        }

        /// <summary>A page's rows (not the Axis Monitor's), in the designer's order.</summary>
        private static System.Collections.Generic.List<AxisSettingHMI> RowsOf(Control host)
        {
            var rows = host.Controls.OfType<AxisSettingHMI>().Where(r => !r.MonitorMode).ToList();
            // the designer's order, kept the first time: the rows move up and down later
            if (rows.Any(r => r._rank < 0))
            {
                int next = rows.Where(r => r._rank >= 0).Select(r => r._rank + 1).DefaultIfEmpty(0).Max();
                foreach (var r in rows.Where(r => r._rank < 0).OrderBy(r => r.Top).ThenBy(r => r.Left)) r._rank = next++;
            }
            return rows.OrderBy(r => r._rank).ToList();
        }

        /// <summary>The page's Axes list offered again (its rows' conditions changed, or it is opened) and its ticks applied.</summary>
        private static void OfferPageAxes(Control page)
        {
            if (page == null) return;
            var bar = BarOf(page, ItemsBarName);
            if (bar == null || bar.AxesPick == null) return;
            var axes = PageAxes(page);
            if (!axes.Select(a => a.Key).SequenceEqual(bar.AxesPick.Items.Select(i => i.Key))) bar.OfferAxes(axes);
            ApplyAxisFilter(page, bar);
        }

        /// <summary>The Axes list's ticks on the page's rows: the ones it hides out of sight, the rest moved up together.</summary>
        internal static void ApplyAxisFilter(Control host, AxisPickBar bar)
        {
            if (host == null || bar == null || _restacking) return;
            foreach (var r in RowsOf(host))
            {
                bool hide = !bar.ShowsAxis(r.FilterKey);
                if (r.FilteredOut == hide) continue;
                r.FilteredOut = hide;
                r.Visible = r.Wanted;
            }
            Restack(host);
        }

        /// <summary>
        /// THE ROWS SHOWN MOVED UP TOGETHER: the places the designer gave the rows
        /// are kept, and taken in the designer's order by the rows shown first,
        /// then by the hidden ones - so no gap is left where an axis was hidden (by
        /// the Axes list, or by its conditions: an option the machine has not got).
        /// </summary>
        internal static void Restack(Control host)
        {
            if (host == null || _restacking) return;
            var rows = RowsOf(host);
            if (rows.Count < 2) return;
            var places = rows.Select(r => r.Location).OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
            var order = rows.Where(r => r.Wanted).Concat(rows.Where(r => !r.Wanted)).ToList();
            FollowRows(host, rows, places, order.Count(r => r.Wanted));
            bool moves = false;
            for (int i = 0; i < order.Count; i++) if (order[i].Location != places[i]) { moves = true; break; }
            if (!moves) return;
            _restacking = true;
            host.SuspendLayout();
            try { for (int i = 0; i < order.Count; i++) if (order[i].Location != places[i]) order[i].Location = places[i]; }
            finally { host.ResumeLayout(true); _restacking = false; }
            var frame = AxisPageFrame.OfBody(host);
            if (frame != null) frame.StickNames();
        }

        /// <summary>
        /// What stands BELOW the rows (FA2010's "Send Axes to Reference", 8 October)
        /// follows the last row shown, the same distance under it - with a few axes
        /// shown it stood where the last of all the rows ends, and the page scrolled
        /// down to it past nothing.
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> _followers =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();

        private static void FollowRows(Control host, System.Collections.Generic.List<AxisSettingHMI> rows, System.Collections.Generic.List<Point> places, int shown)
        {
            int h = rows.Max(r => r.Height);
            int slotsBottom = places[places.Count - 1].Y + h;
            int shownBottom = shown > 0 ? places[shown - 1].Y + h : places[0].Y;
            foreach (Control c in host.Controls)
            {
                if (c is AxisSettingHMI) continue;
                object gap;
                if (!_followers.TryGetValue(c, out gap))
                {
                    if (c.Top < slotsBottom) continue;          // above or beside the rows: not a follower
                    gap = c.Top - slotsBottom;
                    _followers.Add(c, gap);
                }
                int top = shownBottom + (int)gap;
                if (c.Top != top) c.Top = top;
            }
        }

        /// <summary>A list of the page's heading: on the page, or in its frame's heading.</summary>
        private static AxisPickBar BarOf(Control page, string name)
        {
            var frame = AxisPageFrame.OfBody(page);
            if (frame == null) return page.Controls[name] as AxisPickBar;
            return (frame.HeadContent.Controls[name] ?? frame.Head.Controls[name]) as AxisPickBar;
        }

        /// <summary>The page framed (AxisPageFrame.Freeze) once it is shown and laid out - not while its handles are made.</summary>
        private void ScheduleFreeze(Control page)
        {
            if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime || !(page is ScrollableControl)) return;
            Action freeze = () =>
            {
                if (page.IsDisposed) return;
                var frame = AxisPageFrame.Freeze(page);
                if (frame == null) return;
                var motion = frame.Head.Controls[MotionBarName] as AxisPickBar;
                if (motion != null)
                {
                    motion.Pin(frame.Head, frame.Body, motion.HomeX, motion.StickX);
                    frame.Scrolled += (s, e) => motion.Place();
                }
                // the top row lays the headings out in their new place
                var top = frame.Body.Controls.OfType<AxisSettingHMI>().OrderBy(r => r.Top).FirstOrDefault();
                if (top != null) top.AlignHeaders();
            };
            EventHandler later = null;
            later = (s, e) =>
            {
                page.HandleCreated -= later;
                try { page.BeginInvoke(freeze); } catch { }
            };
            if (page.IsHandleCreated) { try { page.BeginInvoke(freeze); } catch { } }
            else page.HandleCreated += later;
        }

        /// <param name="clampToTravel">
        /// True for reference and calibration, whose sensible range is ALREADY
        /// known: it is the axis's own min and max. Typing them again per field
        /// is work that can only introduce a disagreement, and a reference value
        /// outside the travel limits is a machine that homes into a hard stop.
        /// </param>
        private BaseNumericHMI AddSetting(string address, ref int x, int w,
                                          bool clampToTravel = false,
                                          string lowerTag = null, string upperTag = null)
        {
            // The Axis Monitor shows the settings; they are entered on the page.
            if (MonitorMode) return AddDisplay(address, ref x, w, DecimalPlaces, Digits, false, null);
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
                    // The other bound's device first (a minimum is below the maximum), else the row's entry bound.
                    n.MinLimit = !string.IsNullOrEmpty(lowerTag)
                        ? new Kaban.Core.Models.DynamicValue(LimitSourceType.Tag, 0, lowerTag)
                        : CopyOf(EntryMinimum);

                    n.MaxLimit = !string.IsNullOrEmpty(upperTag)
                        ? new Kaban.Core.Models.DynamicValue(LimitSourceType.Tag, 0, upperTag)
                        : CopyOf(EntryMaximum);
                }
                n.SetBounds(x, 6, w, RowHeight - 12);
                Controls.Add(n);
            }
            x += w + Gap;
            return n;
        }

        /// <summary>A bound for a field: its own copy (none when not set, or a static NaN).</summary>
        private static Kaban.Core.Models.DynamicValue CopyOf(Kaban.Core.Models.DynamicValue v)
        {
            if (v == null || v.Type == LimitSourceType.None) return new Kaban.Core.Models.DynamicValue();
            if (v.Type == LimitSourceType.Static && double.IsNaN(v.StaticValue)) return new Kaban.Core.Models.DynamicValue();
            if (v.Type != LimitSourceType.Static && string.IsNullOrWhiteSpace(v.TagAddress)) return new Kaban.Core.Models.DynamicValue();
            return new Kaban.Core.Models.DynamicValue(v.Type, v.StaticValue, (v.TagAddress ?? "").Trim());
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
                    AlertAddress = (HomeRequestAddress ?? "").Trim(),
                    // On the Axis Monitor: shown, not ticked.
                    DisplayOnly = MonitorMode
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
                ViewPreferences.Changed += OnViewPreferencesChanged;
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

        /// <summary>The name to its side, and the simple motion columns picked (View Preferences changed).</summary>
        private void OnViewPreferencesChanged(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnViewPreferencesChanged), sender, e); } catch { } return; }
            _axis.TextAlign = ViewPreferences.AxisNamesLeft ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            if (!MonitorMode) RefreshMotionColumns();        // the Axis Monitor builds its rows again itself
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _languageHooked) { LanguageManager.LanguageChanged -= OnLanguageChanged; ViewPreferences.Changed -= OnViewPreferencesChanged; }
            base.Dispose(disposing);
        }

        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            // Children are ordinary HMI controls, so the scanner already drives
            // them; the row itself only follows its visibility conditions - and the
            // page's Axes list.
            bool met = VisibilityConditions == SystemCondition.None || checkGlobal(VisibilityConditions);
            bool changed = met != _conditionsMet;
            _conditionsMet = met;
            bool vis = Wanted;
            if (Visible != vis) Visible = vis;
            if (changed && !MonitorMode && Parent != null)
            {
                // the Axes list offers the rows that can be seen (the ticks applied again)
                if (BarOf(Parent, ItemsBarName) != null) OfferPageAxes(Parent);
                Restack(Parent);
            }
        }
    }

}

