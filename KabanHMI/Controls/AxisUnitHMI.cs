using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// An axis on a manual-movement page, defined by ONE property: its number.
    ///
    /// Everything else - jog bits, position and its scaling, name, error bit,
    /// reference sensor - already exists on the matching AxisSettingHMI row, and
    /// is taken from there. Entering the same addresses and divisors on two pages
    /// is how the two end up disagreeing; now there is nothing to disagree about.
    ///
    /// It looks exactly like the CylinderUnitHMI units around it, because it IS
    /// one inside: the same frame, two jog buttons, position display, the
    /// reference sensor lamp on one button, and the "?" on its top frame line
    /// while the drive reports a code (servo alarm, axis alarm, axis warning).
    /// </summary>
    [ToolboxItem(true)]
    // The Properties window in the order of the work (see PropertyOrder).
    [System.ComponentModel.TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class AxisUnitHMI : UserControl
    {
        private readonly CylinderUnitHMI _unit = new CylinderUnitHMI { Dock = DockStyle.Fill };
        private int _axisNumber;
        private int _plcNumber = 1;

        public AxisUnitHMI()
        {
            // The axis reading as the Axis Settings rows show it - black bold on
            // the read-only grey (every unit's default now; set here as well so an
            // axis can never be switched to the red readout). An axis error blinks
            // this display's border red (see CylinderUnitHMI.UpdateErrorBlink).
            _unit.PositionStyle = UnitPositionStyle.Standard;
            _unit.Kind = UnitKind.Axis;

            // White, like the unit inside: the unit rounds its own corners, and the
            // default grey showed through them as four dots.
            BackColor = Color.White;
            Padding = Padding.Empty;

            Controls.Add(_unit);
            Size = DefaultSize;
        }

        protected override Size DefaultSize { get { return new Size(360, 64); } }

        [Category(" Axis"), DefaultValue(0),
         Description("The axis number set on its AxisSettingHMI row. Everything else is taken from that row.")]
        public int AxisNumber
        {
            get { return _axisNumber; }
            set { _axisNumber = value; Apply(); }
        }

        /// <summary>The controller the axis is on - 1 unless the machine has several.</summary>
        [Category(" Axis"), DefaultValue(1), Description("Controller of the axis (1 unless the machine has several).")]
        public int PlcNumber
        {
            get { return _plcNumber; }
            set { _plcNumber = value; Apply(); }
        }

        /// <summary>The unit inside, for tests and for code that needs its parts.</summary>
        [Browsable(false)]
        public CylinderUnitHMI Unit { get { return _unit; } }

        /// <summary>True when the axis row was found and applied.</summary>
        [Browsable(false)]
        public bool IsResolved { get; private set; }

        private bool InDesigner
        {
            get { return DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime; }
        }

        /// <summary>
        /// Looks the axis up and becomes it. Done as the number is SET - while the
        /// page is being built - so the scanner, which collects addresses when the
        /// page is loaded, already sees the jog bits and the position.
        /// </summary>
        private void Apply()
        {
            IsResolved = false;

            // CLEARED FIRST, every time. While a page is built the station is still
            // the machine's local "1" - on a mounted H800 that first lookup found
            // FA2010's axis of the same number, and when the right lookup then came
            // up empty that configuration stayed: an H800 button would have jogged
            // an FA2010 axis. A unit that cannot find its axis is now inert.
            _unit.CommandAAddress = _unit.CommandBAddress = "";
            _unit.PositionAddress = "";
            _unit.Lamp1AAddress = _unit.Lamp1BAddress = "";
            _unit.ErrorAAddress = _unit.ErrorBAddress = "";
            _unit.SetAxisCodes("", "", "");
            if (InDesigner || _axisNumber <= 0)
            {
                _unit.TitleTextKey = _axisNumber > 0 ? "Axis " + _axisNumber : "Axis";
                return;
            }

            // Discovery builds pages only to search them; resolving then would ask
            // for discovery again from inside it.
            if (AlarmHelpLinks.IsDiscovering) return;
            AlarmHelpLinks.EnsureDiscovered();

            var a = AlarmHelpLinks.FindAxis(_plcNumber, _axisNumber);
            if (a == null)
            {
                // Said on the control, not left blank: an axis unit that silently
                // does nothing looks like a machine that will not move.
                _unit.TitleTextKey = string.Format(
                    LanguageManager.GetString("Axis_NotFound", "Axis {0}: no axis settings row"), _axisNumber);
                return;
            }

            _unit.PlcNumber = a.Station;
            _unit.TitleTextKey = a.NameKey;
            _unit.Kind = UnitKind.Axis;

            _unit.CommandAAddress = a.Back;
            _unit.CommandAAction = BitActionType.Momentary;
            _unit.CommandBAddress = a.Forward;
            _unit.CommandBAction = BitActionType.Momentary;

            // NO slot widths of its own: the function units' layout, so an axis
            // unit's buttons and position are the same widths as a cylinder
            // unit's beside it. (It used the axis row's widths scaled up, and its
            // position display came out a third wider than every unit around it.)

            _unit.PositionAddress = a.Position;
            _unit.PositionDataType = a.PositionType;
            _unit.PositionDivisor = a.Divisor <= 0 ? 1 : a.Divisor;
            _unit.PositionDecimalPlaces = a.Decimals;
            _unit.PositionDigits = a.Digits;
            _unit.PositionSuffix = a.Suffix;

            // The reference sensor on ONE side only, as on the axis row.
            bool back = a.ReferenceSide == AxisReferenceSide.Back;
            _unit.Lamp1AAddress = back ? a.ReferenceSensor : "";
            _unit.Lamp1BAddress = back ? "" : a.ReferenceSensor;

            // The error bit blinks the position display's border red while set.
            // The "?" on the top frame line follows the axis row's three DRIVE
            // CODES instead: shown while any is not zero, one menu entry per code,
            // each opening just that code's entry of its manual. All three zero,
            // no "?" - there is nothing to look up.
            _unit.ErrorAAddress = "";
            _unit.ErrorBAddress = a.Error;
            _unit.SetAxisCodes(a.ServoAlarm, a.AxisAlarm, a.AxisWarning);

            _unit.ButtonA.Direction = a.Vertical ? ButtonGlyphs.Direction.Down : ButtonGlyphs.Direction.Left;
            _unit.ButtonB.Direction = a.Vertical ? ButtonGlyphs.Direction.Up : ButtonGlyphs.Direction.Right;

            IsResolved = true;
        }
    }
}

