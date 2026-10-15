using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// A MOTOR DRIVEN BY AN INVERTER, as one control (14 October: "We should have a
    /// control for inverter as well, just like CylinderUnitHMI, so we could
    /// automatically set its error and open a proper helper for it using its error
    /// code. unitConveyor_Motor, lampConveyor_Motor_Run and dispConveyor_Inverter_Error
    /// should be combined as one control. The error should not be displayed on the
    /// screen: there will be an error helper when the error occurs, and it shows the
    /// error code anyway").
    ///
    /// A cylinder unit's frame, buttons and value, and:
    ///   * FaultCodeAddress - the word the PLC copies the inverter's fault into: its
    ///     DATA CODE (FR-A800 manual, 6.4 "List of fault displays": 16 = E.OC1). Never
    ///     shown as a number. While it is not zero the buttons' borders blink red, the
    ///     unit's "?" lists the code's entry of the inverter's fault list (6.5 "Causes
    ///     and corrective actions", in the servo codes' format) and the whole manual -
    ///     there even for a code the list does not have - and the HMI raises the
    ///     inverter's alarm itself (AlarmGeneration, with the axes' switch), SETting
    ///     ErrorAddress when there is one.
    ///   * RunLampAddress - the inverter running: a lamp before the unit's name.
    ///   * The Position... properties are its VALUE (a speed, a frequency): shown
    ///     between the buttons when PositionAddress is set.
    /// A valve and a feedback mean nothing for a motor: not shown. A one-button motor
    /// sets one command (A or B), as a one-button cylinder unit does.
    /// </summary>
    [ToolboxItem(true)]
    [Description("A motor driven by an inverter: its buttons, a run lamp before its name, a value, and its fault code - the fault's help and alarm come from it.")]
    public class InverterUnitHMI : CylinderUnitHMI
    {
        private string _faultCode = "", _runLamp = "", _runLampTip = "Running";
        private readonly LampHMI _lamp = new LampHMI();

        public InverterUnitHMI()
        {
            Kind = UnitKind.Inverter;
            TitleTextKey = "Motor";
            PositionTipKey = "";
            _lamp.Size = new Size(16, 16);
            _lamp.CornerRadius = 0;
            _lamp.TabStop = false;
            _lamp.ToolTipTextKey = _runLampTip;
        }

        // ---- the inverter -------------------------------------------------------

        [Category(" Inverter"), DefaultValue("")]
        [Description("The word holding the inverter's fault as its DATA CODE (FR-A800 manual 6.4: 16 = E.OC1; 0 = no fault). " +
                     "Not shown: while it is not zero the buttons blink red, the '?' opens the code's troubleshooting and the manual, " +
                     "and the HMI raises the alarm (setting ErrorAddress).")]
        public string FaultCodeAddress
        {
            get { return _faultCode; }
            set { _faultCode = (value ?? "").Trim(); ApplyFaultCode(); }
        }

        [Category(" Inverter"), DefaultValue("")]
        [Description("The alarm bit of the inverter's fault (optional): SET by the HMI when the fault comes, like an axis's error bit; " +
                     "the PLC's alarm for it is then not listed twice.")]
        public string ErrorAddress
        {
            get { return ErrorBAddress; }
            set { base.ErrorBAddress = value; }
        }

        [Category(" Inverter"), DefaultValue("")]
        [Description("The inverter running (its RUN output, or the PLC's bit for it): a lamp before the unit's name. Empty: no lamp.")]
        public string RunLampAddress
        {
            get { return _runLamp; }
            set { _runLamp = (value ?? "").Trim(); ApplyRunLamp(); }
        }

        [Category(" Inverter"), DefaultValue("Running")]
        [Description("The run lamp's tooltip (translation key or text).")]
        public string RunLampTipKey
        {
            get { return _runLampTip; }
            set { _runLampTip = value ?? ""; _lamp.ToolTipTextKey = _runLampTip; }
        }

        /// <summary>The run lamp (tests).</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public LampHMI RunLamp { get { return _lamp; } }

        // ---- a cylinder's, not a motor's: hidden -----------------------------------

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new CylinderValve ValveType { get { return base.ValveType; } set { base.ValveType = value; } }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new CylinderFeedback FeedbackType { get { return base.FeedbackType; } set { base.FeedbackType = value; } }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new string TypeCheck { get { return "OK"; } }

        /// <summary>The fault's alarm bit is ErrorAddress (one for the motor, not one per movement).</summary>
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new string ErrorAAddress { get { return base.ErrorAAddress; } set { base.ErrorAAddress = value; } }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new string ErrorBAddress { get { return base.ErrorBAddress; } set { base.ErrorBAddress = value; } }

        // ---- the alarm the HMI raises itself -----------------------------------------

        /// <summary>The watch of the fault code (AlarmHelpLinks registers it as the pages are discovered); null without a code word.</summary>
        public AlarmGeneration.Inverter BuildInverterWatch()
        {
            if (string.IsNullOrWhiteSpace(_faultCode)) return null;
            return new AlarmGeneration.Inverter
            {
                Station = PlcNumber,
                Name = string.IsNullOrEmpty(TitleTextKey) ? Name : LanguageManager.GetString(TitleTextKey, TitleTextKey),
                CodeAddress = Absolute(_faultCode),
                ErrorAddress = Absolute(ErrorAddress ?? "")
            };
        }

        // ---- the run lamp, before the name ----------------------------------------------

        protected override void OnSettingsApplied()
        {
            base.OnSettingsApplied();
            ApplyFaultCode();
            ApplyRunLamp();
        }

        private string _appliedCode;

        /// <summary>The code word to the "?" - again when the BaseOffset moved it.</summary>
        private void ApplyFaultCode()
        {
            var code = Absolute(_faultCode ?? "");
            if (code == _appliedCode) return;
            _appliedCode = code;
            SetInverterCode(code);
        }

        private void ApplyRunLamp()
        {
            var frame = FrameBox;
            if (frame == null) return;
            bool shown = _runLamp.Length > 0;
            _lamp.PlcNumber = PlcNumber;
            _lamp.Address = shown ? Absolute(_runLamp) : "";
            _lamp.VisibilityConditions = VisibilityConditions;
            // in the frame only when there is one: the scanner shows every lamp it finds
            if (shown && _lamp.Parent != frame) { frame.Controls.Add(_lamp); _lamp.BringToFront(); }
            else if (!shown && _lamp.Parent == frame) frame.Controls.Remove(_lamp);
            PlaceRunLamp();
        }

        protected override void OnBadgesLaidOut()
        {
            base.OnBadgesLaidOut();
            PlaceRunLamp();
        }

        private void PlaceRunLamp()
        {
            var frame = FrameBox;
            if (frame == null) return;
            bool shown = _runLamp.Length > 0;
            int cap = (int)Math.Ceiling(frame.Font.GetHeight());
            int lead = shown ? cap : 0;
            if (frame.CaptionLead != lead) { frame.CaptionLead = lead; frame.Invalidate(); }
            if (!shown) return;
            if (_lamp.Width != cap || _lamp.Height != cap) _lamp.Size = new Size(cap, cap);
            _lamp.Location = new Point(10, 0);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _lamp.Parent == null) _lamp.Dispose();
            base.Dispose(disposing);
        }
    }
}
