using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// ONE PNEUMATIC POSITIONING, ONE LINE (9 October: "we use this type of
    /// pneumatic positioning set all over our machines - as an axis, just like
    /// servomotors - so we should create this HMI control to create them properly
    /// on the screens"). A cylinder with a position sensor and a 5/3 valve that the
    /// PLC drives to a target, laid out the way an axis row is - a CylinderRowHMI:
    ///
    ///   name | ◀ position ▶ ⛓? ‖ target ▶| ‖ calibration ▶| ‖ tolerance ‖ direction ref
    ///
    ///   - TARGET POSITION: the position to go to (TargetPosition) and the button
    ///     that starts the positioning (PositioningStartAddress, SET) - the operator's;
    ///   - CALIBRATION: the calibration position (CalibrationAddress) and the
    ///     button that calibrates (CalibrateAddress, SET);
    ///   - POSITIONING TOLERANCE (PositioningToleranceAddress);
    ///   - DIRECTION REF (PositioningDirectionReferenceAddress - the old pages'
    ///     "Stopping Offset"): the PLC compares the target with it to choose the
    ///     direction - a target above it is approached the way the position
    ///     increases, a target below it the other way (9 October).
    ///
    /// The name, the manual movement, the table, the headings (labels tagged
    /// "PositioningHeading:&lt;column&gt;") and the rows moving up are CylinderRowHMI's.
    /// </summary>
    [ToolboxItem(true)]
    // The Properties window in the order of the work (see PropertyOrder).
    [TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class PneumaticPositioningHMI : CylinderRowHMI
    {
        /// <summary>The tag of a heading label: this prefix and the column's key.</summary>
        public const string HeadingTag = "PositioningHeading:";

        /// <summary>The columns with a heading: key, translation key, English.</summary>
        private static readonly string[][] Headings =
        {
            new[] { "Manual", "Col_PpManual", "Manual movement" },
            new[] { "Target", "Col_PpTarget", "Target position" },
            new[] { "Calibration", "Col_PpCalibration", "Calibration" },
            new[] { "Tolerance", "Col_PpTolerance", "Positioning tolerance" },
            new[] { "DirectionRef", "Col_PpDirectionRef", "Direction ref" },
        };

        /// <summary>The keys of the columns with a heading, in the row's order.</summary>
        public static IEnumerable<string> HeadingKeys { get { return Headings.Select(h => h[0]); } }

        public PneumaticPositioningHMI() : base(HeadingTag, "pp")
        {
            AddSection("Target", "Col_PpTarget", "Target position", "Target", "Tip_PpTarget", "Start", "Tip_PpStart", operatorLevel: true);
            AddSection("Calibration", "Col_PpCalibration", "Calibration", "Calibration", "Tip_PpCalibration", "Calibrate", "Tip_PpCalibrate");
            AddSection("Tolerance", "Col_PpTolerance", "Positioning tolerance", "Tolerance", "Tip_PpTolerance");
            AddSection("DirectionRef", "Col_PpDirectionRef", "Direction ref", "DirectionRef", "Tip_PpDirectionRef");
            SectionsDone();
        }

        [Category(" Target position"), DefaultValue(""), Description("Word of the target position - where the positioning goes.")]
        public string TargetPosition { get { return FieldAddress("Target"); } set { SetFieldAddress("Target", value); } }

        [Category(" Target position"), DefaultValue(""), Description("Bit that starts the positioning to the target (SET: the PLC resets it).")]
        public string PositioningStartAddress { get { return CommandAddress("Target"); } set { SetCommandAddress("Target", value); } }

        [Category(" Calibration"), DefaultValue(""), Description("Word of the calibration position - what the position reads after a calibration.")]
        public string CalibrationAddress { get { return FieldAddress("Calibration"); } set { SetFieldAddress("Calibration", value); } }

        [Category(" Calibration"), DefaultValue(""), Description("Bit that calibrates: the position read now becomes the calibration position (SET: the PLC resets it).")]
        public string CalibrateAddress { get { return CommandAddress("Calibration"); } set { SetCommandAddress("Calibration", value); } }

        [Category(" Positioning tolerance"), DefaultValue(""), Description("Word of the positioning tolerance - how far from the target still counts as there.")]
        public string PositioningToleranceAddress { get { return FieldAddress("Tolerance"); } set { SetFieldAddress("Tolerance", value); } }

        [Category(" Direction ref"), DefaultValue(""), Description("Word of the direction reference: the PLC compares the target with it to choose the direction - a target above it is approached the way the position increases, a target below it the other way (the old pages' 'Stopping Offset').")]
        public string PositioningDirectionReferenceAddress { get { return FieldAddress("DirectionRef"); } set { SetFieldAddress("DirectionRef", value); } }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NumericInputHMI TargetField { get { return FieldOf("Target"); } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ButtonHMI StartButton { get { return CommandOf("Target"); } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NumericInputHMI CalibrationField { get { return FieldOf("Calibration"); } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ButtonHMI CalibrateButton { get { return CommandOf("Calibration"); } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NumericInputHMI ToleranceField { get { return FieldOf("Tolerance"); } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NumericInputHMI DirectionRefField { get { return FieldOf("DirectionRef"); } }

        /// <summary>A column's heading, translated ("Target position").</summary>
        public static string HeadingText(string columnKey)
        {
            foreach (var h in Headings)
                if (h[0] == columnKey) return LanguageManager.GetString(h[1], h[2]);
            return columnKey ?? "";
        }

        /// <summary>A column heading's translation key ("Col_PpTarget"), or null.</summary>
        public static string HeadingKey(string columnKey)
        {
            foreach (var h in Headings)
                if (h[0] == columnKey) return h[1];
            return null;
        }

        /// <summary>A page's pneumatic positionings, in the designer's order.</summary>
        public static List<PneumaticPositioningHMI> RowsOf(Control host)
        {
            return RowsOfKind(host, typeof(PneumaticPositioningHMI)).Cast<PneumaticPositioningHMI>().ToList();
        }

        /// <summary>The page's pneumatic positionings as one table (see CylinderRowHMI.LayoutPage).</summary>
        public static void LayoutPage(Control host) { LayoutPage(host, typeof(PneumaticPositioningHMI)); }

        /// <summary>The page's pneumatic positionings shown moved up together (see CylinderRowHMI.Restack).</summary>
        public static void Restack(Control host) { Restack(host, typeof(PneumaticPositioningHMI)); }
    }
}
