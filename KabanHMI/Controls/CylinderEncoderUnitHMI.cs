using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// A CYLINDER WITH AN ENCODER on a Manual Movement page, defined by ONE
    /// property: its encoder's number (9 October: "CylinderEncoderUnitHMI controls
    /// at Manual Movement pages will take values from CylinderEncoderSettingHMI
    /// controls at Machine Parameters / Encoders, so we wouldn't have to enter the
    /// same address values on both pages") - as an AxisUnitHMI takes its axis from
    /// its AxisSettingHMI row.
    ///
    /// Its buttons and their sensors, its position and scaling, its valve and
    /// feedback, its error bits and its helpers all come from the
    /// CylinderEncoderSettingHMI row with the same EncoderNumber (on its station).
    /// It looks exactly like the cylinder units around it, because it IS one
    /// inside: the same frame, buttons, position, "?" and chain.
    ///
    /// Its own, as a page wants it: the title shown here (empty: the row's name -
    /// "Top Clamping" on the Milling page, "Milling Top" on the Encoders page),
    /// and the conditions it is shown and used under on this page.
    /// </summary>
    [ToolboxItem(true)]
    // The Properties window in the order of the work (see PropertyOrder).
    [TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class CylinderEncoderUnitHMI : UserControl
    {
        private readonly CylinderUnitHMI _unit = new CylinderUnitHMI { Dock = DockStyle.Fill };
        private int _number;
        private int _plcNumber = 1;
        private string _title = "";
        private SystemCondition _visibility = SystemCondition.None, _enable = SystemCondition.None;

        public CylinderEncoderUnitHMI()
        {
            _unit.PositionStyle = UnitPositionStyle.Standard;
            _unit.Kind = UnitKind.Cylinder;
            _unit.PartOfComposite = true;
            _unit.CommandAAction = _unit.CommandBAction = BitActionType.Momentary;
            _unit.RequiredSecurity = SecurityLevel.Operator;
            // White, like the unit inside: the unit rounds its own corners, and the
            // default grey showed through them as four dots.
            BackColor = Color.White;
            Padding = Padding.Empty;
            Controls.Add(_unit);
            Size = DefaultSize;
            Apply();
        }

        protected override Size DefaultSize { get { return new Size(360, 64); } }

        [Category(" Encoder"), DefaultValue(0),
         Description("The encoder's number: the EncoderNumber of its CylinderEncoderSettingHMI row on Machine Parameters > Encoders. Everything else is taken from that row.")]
        public int EncoderNumber
        {
            get { return _number; }
            set { _number = value; Apply(); }
        }

        /// <summary>The controller of the encoder's row - 1 unless the machine has several.</summary>
        [Category(" Encoder"), DefaultValue(1), Description("Controller of the encoder (1 unless the machine has several).")]
        public int PlcNumber
        {
            get { return _plcNumber; }
            set { _plcNumber = value; Apply(); }
        }

        /// <summary>The title on this page (a translation key); empty: the encoder row's name.</summary>
        [Category(" Unit"), DefaultValue(""),
         Description("The title on this page - a translation key, usually its English text. Empty: the name of its encoder's row.")]
        public string TitleTextKey
        {
            get { return _title; }
            set { _title = (value ?? "").Trim(); Apply(); }
        }

        /// <summary>Shown only while these are met (its buttons and its position), as a cylinder unit's.</summary>
        [Category(" Triggers"), DefaultValue(SystemCondition.None)]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("Its buttons and position are shown only while these are met (on this page).")]
        public SystemCondition VisibilityConditions
        {
            get { return _visibility; }
            set { _visibility = value; _unit.VisibilityConditions = value; }
        }

        /// <summary>Usable only while these are met, as a cylinder unit's.</summary>
        [Category(" Triggers"), DefaultValue(SystemCondition.None)]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("Its buttons can be used only while these are met (on this page).")]
        public SystemCondition EnableConditions
        {
            get { return _enable; }
            set { _enable = value; _unit.EnableConditions = value; }
        }

        /// <summary>The unit inside, for tests and for code that needs its parts.</summary>
        [Browsable(false)]
        public CylinderUnitHMI Unit { get { return _unit; } }

        /// <summary>True when the encoder's row was found and applied.</summary>
        [Browsable(false)]
        public bool IsResolved { get; private set; }

        /// <summary>The row taken (a copy), or null.</summary>
        [Browsable(false)]
        public AlarmHelpLinks.EncoderInfo Row { get; private set; }

        private bool InDesigner
        {
            get { return DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime; }
        }

        /// <summary>
        /// Looks the encoder up and becomes it. Done as the number is SET - while the
        /// page is being built - so the scanner, which collects addresses when the
        /// page is loaded, already sees its bits and its position.
        /// </summary>
        private void Apply()
        {
            IsResolved = false;
            Row = null;

            // CLEARED FIRST, every time (as an axis unit): a unit that cannot find its
            // encoder is inert, never left with another machine's addresses.
            _unit.CommandAAddress = _unit.CommandBAddress = "";
            _unit.PositionAddress = "";
            _unit.Lamp1AAddress = _unit.Lamp1BAddress = "";
            _unit.ErrorAAddress = _unit.ErrorBAddress = "";
            _unit.HelpAFiles = _unit.HelpBFiles = "";
            _unit.ValveType = CylinderValve.Unspecified;
            _unit.FeedbackType = CylinderFeedback.Unspecified;
            if (InDesigner || _number <= 0)
            {
                _unit.TitleTextKey = _title.Length > 0 ? _title : (_number > 0 ? "Encoder " + _number : "Encoder");
                return;
            }

            // Discovery builds pages only to search them; resolving then would ask
            // for discovery again from inside it.
            if (AlarmHelpLinks.IsDiscovering) { _unit.TitleTextKey = _title; return; }
            AlarmHelpLinks.EnsureDiscovered();

            var e = AlarmHelpLinks.FindEncoder(_plcNumber, _number);
            if (e == null)
            {
                // Said on the control, not left blank: a unit that silently does
                // nothing looks like a machine that will not move.
                _unit.TitleTextKey = string.Format(
                    LanguageManager.GetString("Enc_NotFound", "Encoder {0}: no encoder settings row"), _number);
                return;
            }

            _unit.PlcNumber = e.Station;
            _unit.TitleTextKey = _title.Length > 0 ? _title : e.NameKey;
            _unit.ValveType = e.Valve;
            _unit.FeedbackType = e.Feedback;
            _unit.CommandAAddress = e.Back;
            _unit.CommandBAddress = e.Forward;
            _unit.Lamp1AAddress = e.BackSensor;
            _unit.Lamp1BAddress = e.ForwardSensor;
            _unit.ErrorAAddress = e.ErrorBack;
            _unit.ErrorBAddress = e.ErrorForward;
            _unit.HelpAFiles = e.HelpBack;
            _unit.HelpBFiles = e.HelpForward;
            _unit.PositionAddress = e.Position;
            _unit.PositionDataType = e.PositionType;
            _unit.PositionDivisor = e.Divisor <= 0 ? 1 : e.Divisor;
            _unit.PositionDecimalPlaces = e.Decimals;
            _unit.PositionDigits = e.Digits;
            _unit.PositionSuffix = e.Suffix;
            _unit.PositionUnitType = e.UnitType;
            _unit.RequiredSecurity = e.Security;
            _unit.Arrows = e.Vertical ? ButtonArrowStyle.DownUp : ButtonArrowStyle.LeftRight;
            Row = e;
            IsResolved = true;
        }
    }
}
