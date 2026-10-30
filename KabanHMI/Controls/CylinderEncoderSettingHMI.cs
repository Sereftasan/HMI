using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// ONE CYLINDER WITH AN ENCODER, ONE LINE (9 October: "create it just like
    /// AxisSettingHMI ... and re-create Machine Parameters / Encoders using this
    /// control, just like AxisSettingHMI in the AxisSetting page"). A clamp or a
    /// stop whose position an encoder counts (its reference sensor sets the count),
    /// laid out the way an axis row is - a CylinderRowHMI:
    ///
    ///   name | ◀ position ▶ ⛓? ‖ reference ‖ measuring tolerance ‖ minimum limit ‖ unclamping interval
    ///
    ///   - REFERENCE (ReferenceAddress): the position the count takes at the
    ///     reference sensor;
    ///   - MEASURING TOLERANCE (MeasuringToleranceAddress);
    ///   - MINIMUM LIMIT (MinimumLimitAddress - the old pages' "Start Limit");
    ///   - UNCLAMPING INTERVAL (UnclampingIntervalAddress).
    ///
    /// AND THE ROW EVERY PAGE TAKES IT FROM: a CylinderEncoderUnitHMI on a Manual
    /// Movement page names only this row's EncoderNumber - its buttons, sensors,
    /// position, valve and feedback, error bits and helpers come from here, as an
    /// AxisUnitHMI's come from its AxisSettingHMI row. One place to enter them.
    ///
    /// The name, the manual movement, the table, the headings (labels tagged
    /// "EncoderHeading:&lt;column&gt;") and the rows moving up are CylinderRowHMI's.
    /// </summary>
    [ToolboxItem(true)]
    // The Properties window in the order of the work (see PropertyOrder).
    [TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class CylinderEncoderSettingHMI : CylinderRowHMI
    {
        /// <summary>The tag of a heading label: this prefix and the column's key.</summary>
        public const string HeadingTag = "EncoderHeading:";

        public CylinderEncoderSettingHMI() : base(HeadingTag, "enc")
        {
            AddSection("Reference", "Col_EncReference", "Reference", "Reference", "Tip_EncReference");
            AddSection("Tolerance", "Col_EncTolerance", "Measuring tolerance", "Tolerance", "Tip_EncTolerance");
            AddSection("MinimumLimit", "Col_EncMinimumLimit", "Minimum limit", "MinimumLimit", "Tip_EncMinimumLimit");
            AddSection("Unclamping", "Col_EncUnclamping", "Unclamping interval", "Unclamping", "Tip_EncUnclamping");
            SectionsDone();
        }

        /// <summary>The number a CylinderEncoderUnitHMI names to show this cylinder on a manual page.</summary>
        [Category(" Device"), DefaultValue(0),
         Description("The encoder's number on the HMI. A CylinderEncoderUnitHMI with this number (its EncoderNumber) on a Manual Movement page shows this cylinder.")]
        public int EncoderNumber { get; set; }

        private SystemCondition _encoderConditions = SystemCondition.None, _measuredConditions = SystemCondition.None;

        /// <summary>
        /// THE MACHINE HAS THIS ENCODER only while these are met - an option (30 October: FA2010's
        /// Milling / Cutting Front Clamping has its encoder only on machines with "Cutting Left Front
        /// with Encoder", L33; the Cutting Right Front Vertical Clamping only without "... without
        /// Encoder", L34). Not met: this row is not on the page (the rows under it move up) and the
        /// cylinder's units show no position - a cylinder without one.
        /// </summary>
        [Category(" Triggers"), DefaultValue(SystemCondition.None)]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("The machine has this encoder only while these are met (an option). Not met: the row is not shown and the cylinder's units show no position.")]
        public SystemCondition EncoderConditions
        {
            get { return _encoderConditions; }
            set { _encoderConditions = value; Unit.PositionConditions = value; }
        }

        /// <summary>
        /// THE ENCODER READS THIS CYLINDER only while these are met - one input switched between two
        /// encoders (30 October: FA2010's profile recognition width and height, "the left or the right
        /// unit through K77 / K78", Y120 choosing the left; "right side is default"). Not met: its
        /// position shows "–" and is not judged.
        /// </summary>
        [Category(" Triggers"), DefaultValue(SystemCondition.None)]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("The encoder reads this cylinder only while these are met (one input switched between two encoders). Not met: its position shows '–'.")]
        public SystemCondition MeasuredConditions
        {
            get { return _measuredConditions; }
            set { _measuredConditions = value; Unit.PositionMeasuredConditions = value; }
        }

        protected override SystemCondition ShownConditions { get { return VisibilityConditions | _encoderConditions; } }

        /// <summary>
        /// The valve an option fits instead (30 October: FA2010's Cutting Left Front clamp is a 5/3 open centre with
        /// "Cutting Left Front with Air Exhaust", L35): its units' OptionValveType / OptionValveConditions.
        /// </summary>
        [Category(" Valve & Feedback"), DefaultValue(CylinderValve.Unspecified)]
        [Description("The valve fitted instead while OptionValveConditions are met (an option of the machine). Unspecified: none.")]
        public CylinderValve OptionValveType
        {
            get { return Unit.OptionValveType; }
            set { Unit.OptionValveType = value; }
        }

        [Category(" Valve & Feedback"), DefaultValue(SystemCondition.None)]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("While these are met the valve is OptionValveType (an option of the machine).")]
        public SystemCondition OptionValveConditions
        {
            get { return Unit.OptionValveConditions; }
            set { Unit.OptionValveConditions = value; }
        }

        [Category(" Reference"), DefaultValue(""), Description("Word of the reference - the position the encoder's count takes at its reference sensor.")]
        public string ReferenceAddress { get { return FieldAddress("Reference"); } set { SetFieldAddress("Reference", value); } }

        [Category(" Measuring tolerance"), DefaultValue(""), Description("Word of the measuring tolerance (the old pages' 'Tolerance').")]
        public string MeasuringToleranceAddress { get { return FieldAddress("Tolerance"); } set { SetFieldAddress("Tolerance", value); } }

        [Category(" Minimum limit"), DefaultValue(""), Description("Word of the minimum limit (the old pages' 'Start Limit').")]
        public string MinimumLimitAddress { get { return FieldAddress("MinimumLimit"); } set { SetFieldAddress("MinimumLimit", value); } }

        [Category(" Unclamping interval"), DefaultValue(""), Description("Word of the unclamping interval.")]
        public string UnclampingIntervalAddress { get { return FieldAddress("Unclamping"); } set { SetFieldAddress("Unclamping", value); } }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NumericInputHMI ReferenceField { get { return FieldOf("Reference"); } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NumericInputHMI ToleranceField { get { return FieldOf("Tolerance"); } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NumericInputHMI MinimumLimitField { get { return FieldOf("MinimumLimit"); } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NumericInputHMI UnclampingField { get { return FieldOf("Unclamping"); } }

        /// <summary>A page's encoder rows, in the designer's order.</summary>
        public static List<CylinderEncoderSettingHMI> RowsOf(Control host)
        {
            return RowsOfKind(host, typeof(CylinderEncoderSettingHMI)).Cast<CylinderEncoderSettingHMI>().ToList();
        }

        /// <summary>
        /// What a CylinderEncoderUnitHMI takes from this row, copied (the page the
        /// row is found on by discovery is disposed straight after).
        /// </summary>
        public Kaban.UI.Helpers.AlarmHelpLinks.EncoderInfo Info()
        {
            return new Kaban.UI.Helpers.AlarmHelpLinks.EncoderInfo
            {
                Station = PlcNumber, Number = EncoderNumber, NameKey = TitleTextKey ?? "",
                Back = CommandBackAddress ?? "", Forward = CommandForwardAddress ?? "",
                BackSensor = BackSensorAddress ?? "", ForwardSensor = ForwardSensorAddress ?? "",
                ErrorBack = ErrorBackAddress ?? "", ErrorForward = ErrorForwardAddress ?? "",
                HelpBack = HelpBackFiles ?? "", HelpForward = HelpForwardFiles ?? "",
                Position = PositionAddress ?? "", PositionType = PositionDataType, Divisor = PositionDivisor,
                Decimals = PositionDecimalPlaces, Digits = PositionDigits, Suffix = PositionSuffix ?? "",
                UnitType = PositionUnitType, Valve = ValveType, Feedback = FeedbackType, Vertical = VerticalMovement,
                Security = RequiredSecurity, EncoderConditions = _encoderConditions, MeasuredConditions = _measuredConditions,
                OptionValve = Unit.OptionValveType, OptionValveConditions = Unit.OptionValveConditions
            };
        }
    }
}
