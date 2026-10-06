using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.Core.Models;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// What a unit IS. A cylinder's errors open the cylinder manual for its valve
    /// and feedback; an axis's errors open the servo drive manual. An axis on a
    /// manual page is the same two buttons and position display as a cylinder -
    /// its reference sensor is simply the lamp on ONE of its buttons.
    /// </summary>
    public enum UnitKind
    {
        Cylinder = 0,
        Axis = 1
    }

    /// <summary>
    /// The valve fitted to a cylinder - detail 1.4 of the cylinder manual. The
    /// numbers are the manual's variant numbers and must not be changed (a new
    /// valve gets a new number; Unspecified stays 0).
    ///
    /// THE VALVE DECIDES THE ACTIONS: a single solenoid drives ONE movement (its
    /// spring makes the other), so the unit has one action - A or B; a double
    /// solenoid drives both, so it has both. See CylinderUnitHMI.TypeProblems.
    /// </summary>
    public enum CylinderValve
    {
        Unspecified = 0,
        DoubleSolenoid = 1,            // (1) double solenoid 5/2, impulse / memory: both actions
        SingleSolenoid = 2,            // (2) single solenoid 5/2, spring return: ONE action
        DoubleSolenoid_3Way = 3        // (3) double solenoid 5/3, centre position: both actions
    }

    /// <summary>
    /// The cylinder's feedback - detail 1.7 of the cylinder manual. The numbers
    /// are the manual's variant numbers (Unspecified stays 0).
    ///
    /// THE FEEDBACK DECIDES THE LAMPS AND THE POSITION: one sensor is one lamp,
    /// a sensor at each end is a lamp on each action, an analogue sensor is a
    /// position and no lamp, an encoder is a position and its reference lamp.
    /// </summary>
    public enum CylinderFeedback
    {
        Unspecified = 0,
        SingleMagnetic = 1,            // single magnetic sensor, short stroke: one lamp
        TwoEndMagnetic = 2,            // magnetic sensor at each end: a lamp on each action
        SingleInductive = 3,           // single inductive sensor, short stroke: one lamp
        AnalogueMagnetic = 4,          // 4-20 mA position: no lamp, the Position
        MagneticPlusPressureSwitch = 5,// back-end sensor + pressure switch (clamping): a lamp on each action
        EncoderPlusReference = 6,      // incremental encoder + reference sensor: one lamp and the Position
        NoSensor = 7                   // no sensor on the drawings: nothing confirms the movement - no lamp, no error bit
    }

    /// <summary>How a unit's position reading is drawn.</summary>
    public enum UnitPositionStyle
    {
        /// <summary>Red digits on black - the encoder readout of a function unit.</summary>
        Readout = 0,

        /// <summary>
        /// Black bold digits on the read-only grey, exactly as the axis rows of
        /// Axis Settings show a position - what an axis unit on a manual page uses, so an axis
        /// reads the same on both pages.
        /// </summary>
        Standard = 1
    }

    /// <summary>
    /// COMPOSITE "FUNCTION UNIT" - per the sketch:
    ///
    ///    +- Title -----------------------+
    ///    |  +----------+  +----------+   |
    ///    |  | oo Btn A |  | Btn B oo |   |
    ///    |  +----------+  +----------+   |
    ///    +-------------------------------+
    ///
    /// Title sits ON the group frame (GroupBox caption). Button A is LEFT,
    /// button B is RIGHT. With a single command the other slot stays EMPTY
    /// (reserved) so glyphs/texts align with neighboring units.
    ///
    /// ALL per-button state lives inside ButtonHMI itself:
    ///   - relay bit (M)  ON -> button shades slightly darker
    ///   - output bit (Y) ON -> darker still, text turns white
    ///   - two small embedded sensor lamps (X) on the outer corner, each with
    ///     its own tooltip
    /// This unit only wires the addresses into the buttons.
    ///
    /// ADDRESSING: BaseOffset is a ROW INDEX over column-organized symbol
    /// tables - configure unit #1's absolute addresses, copies change only
    /// BaseOffset (X/Y step octal, M/D decimal via PlcAddressHelper).
    /// Empty address = element hidden/not wired.
    /// Optional encoder readout (red-on-black) appears below when
    /// PositionAddress is set.
    /// </summary>
    [ToolboxBitmap(typeof(GroupBox))]
    // The Properties window in the order of the work (see PropertyOrder).
    [System.ComponentModel.TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class CylinderUnitHMI : UserControl, ILocalizable
    {
        /// <summary>Toolbox-proportioned default (matches a hand-placed unit).</summary>
        protected override Size DefaultSize => new Size(360, 64);

        private readonly RoundedGroupBox _grp = new RoundedGroupBox();
        private readonly ButtonHMI _btnA = new ButtonHMI();
        private readonly ButtonHMI _btnB = new ButtonHMI();
        private readonly NumericDisplayHMI _numPosition = new NumericDisplayHMI();
        private readonly Panel _blankA = new Panel();  // filler so a 1-button unit looks like a 2-button one
        private readonly Panel _blankB = new Panel();
        private readonly ToolTip _toolTip = UiStyle.Tips();

        /// <summary>
        /// The LEFT button. Exposed so EVERY ButtonHMI property (colors, images,
        /// WordActions, blink, triggers, etc.) is editable directly in the designer.
        /// The Action A convenience fields on this unit set the common wiring
        /// (command address / relay / output / sensor lamps); anything else you set
        /// here on the button is preserved.
        /// </summary>
        [Category(" Buttons")]
        [Description("Left button - full ButtonHMI property set.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [TypeConverter(typeof(Kaban.UI.Design.CylinderUnitChildConverter))]
        public ButtonHMI ButtonA { get { return _btnA; } }

        /// <summary>The RIGHT button. See ButtonA.</summary>
        [Category(" Buttons")]
        [Description("Right button - full ButtonHMI property set.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [TypeConverter(typeof(Kaban.UI.Design.CylinderUnitChildConverter))]
        public ButtonHMI ButtonB { get { return _btnB; } }

        /// <summary>The encoder position display. Exposed for full styling access.</summary>
        [Category(" Buttons")]
        [Description("Position readout - full NumericDisplayHMI property set.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [TypeConverter(typeof(Kaban.UI.Design.CylinderUnitChildConverter))]
        public NumericDisplayHMI PositionDisplay { get { return _numPosition; } }

        public CylinderUnitHMI()
        {
            this.Size = new Size(360, 64);
            this.BackColor = Color.White;

            // Title sits ON the frame: classic GroupBox caption, per the sketch.
            _grp.Dock = DockStyle.Fill;
            _grp.BackColor = Color.White;
            _grp.ForeColor = Color.Black;
            _grp.Font = CaptionFont;   // caption: normal weight, shared by every unit

            // outer-corner lamps, per the sketch (A = LEFT button, B = RIGHT button)
            _btnA.LampDirection = ButtonHMI.LampDirectionPosition.Left;   // outer edge
            _btnB.LampDirection = ButtonHMI.LampDirectionPosition.Right;  // outer edge

            // Buttons use normal weight (the GroupBox font is bold for the caption).
            _btnA.Font = UiStyle.Body;
            _btnB.Font = UiStyle.Body;

            _numPosition.TextAlign = ContentAlignment.MiddleCenter; // center the digits in the box
            _numPosition.Visible = false; // only shown when a position device is configured
            // The Axis Settings look - black bold on the read-only grey - unless a
            // page asks for the red readout (see PositionStyle).
            _numPosition.BackColor = Color.Gainsboro;
            _numPosition.ForeColor = Color.Black;
            _numPosition.Font = StandardPositionFont;
            _numPosition.Padding = new Padding(0);
            _numPosition.CornerRadius = 4;      // matches the buttons beside it
            _btnA.CornerRadius = 4;
            _btnB.CornerRadius = 4;

            foreach (var blank in new[] { _blankA, _blankB })
            {
                // Rounded like every other cell in the unit; a square-cornered filler
                // stood out against the buttons beside it.
                blank.BackColor = Color.FromArgb(238, 240, 242);
                blank.BorderStyle = BorderStyle.None;
                blank.Visible = false;
                blank.Paint += (s2, e2) => PaintRoundedCell((Panel)s2, e2.Graphics);
                blank.Resize += (s2, e2) => RoundCell((Panel)s2);
                _grp.Controls.Add(blank);
            }

            _grp.Controls.Add(_btnA);
            _grp.Controls.Add(_btnB);
            _grp.Controls.Add(_numPosition);
            Controls.Add(_grp);

            // ONE "?" on the top frame line, at its right end - see LayoutBadges.
            // The two error bits are watched by badges that never show themselves
            // (the scanner reads them like any bound control); the "?" follows them.
            _helpA.Watcher = _helpB.Watcher = true;
            // Each watcher also reads its movement's devices, so what was missing
            // when the movement failed is known (MovementFor, the manual's top).
            _helpA.Signals = () => SignalsFor("A");
            _helpB.Signals = () => SignalsFor("B");
            // ...and the alarm the HMI raised itself for that movement, if any.
            _helpA.Generated = () => GeneratedFor("A");
            _helpB.Generated = () => GeneratedFor("B");
            Controls.Add(_helpA);
            Controls.Add(_helpB);
            Controls.Add(_help);
            _help.BringToFront();
            _help.Documents = HelpDocuments;
            _grp.Resize += (s2, e2) => LayoutChildren();

            // An error coming or going turns the "?" red and blinks the border of
            // the part that failed.
            _helpA.ActiveChanged += (s2, e2) => { UpdateHelpBadge(); UpdateErrorBlink(); };
            _helpB.ActiveChanged += (s2, e2) => { UpdateHelpBadge(); UpdateErrorBlink(); };

            // The unit's own buttons blink a HEAVIER red than the alarm menu entry.
            _btnA.AlertBorderWidth = _btnB.AlertBorderWidth = 2.4f;

            LanguageManager.LanguageChanged += OnLangChanged;
            this.Disposed += (s, e) => LanguageManager.LanguageChanged -= OnLangChanged;

            ApplySettings();
        }

        private void OnLangChanged(object s, EventArgs e) => ApplyTranslation();

        // Narrower digits so the readout takes less width. Tries condensed faces,
        // falls back to Consolas if none are installed.
        /// <summary>Applies the family's 4px corner radius to a plain panel.</summary>
        private static void RoundCell(Control c)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            const int d = 8;   // radius 4
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                var r = new Rectangle(0, 0, c.Width, c.Height);
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
                path.CloseFigure();
                c.Region = new Region(path);
            }
        }

        private static void PaintRoundedCell(Control c, Graphics g)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            const int d = 6;
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            using (var pen = new Pen(Color.FromArgb(216, 220, 226), 1.4f))
            {
                var r = new Rectangle(1, 1, c.Width - 3, c.Height - 3);
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                g.DrawPath(pen, path);
            }
        }

        /// <summary>The unit caption font - one for every unit, not one each.</summary>
        private static readonly Font CaptionFont = UiScale.NewFont("Segoe UI", 9.5f, FontStyle.Regular);

        /// <summary>The axis-row position font (see AxisSettingHMI), for UnitPositionStyle.Standard.</summary>
        private static readonly Font StandardPositionFont = UiScale.NewFont("Segoe UI", 12F, FontStyle.Bold);

        private static readonly Dictionary<int, Font> _positionFonts = new Dictionary<int, Font>();

        /// <summary>
        /// The readout font at a size, SHARED. The layout asks for a new size as a
        /// unit is resized, and each request used to create a font that was never
        /// disposed - plus one or two more for every face tried and rejected.
        /// Sizes are kept to the half point.
        /// </summary>
        private static Font PositionFontFor(float size)
        {
            int key = (int)Math.Round(size * 2);
            lock (_positionFonts)
            {
                Font cached;
                if (_positionFonts.TryGetValue(key, out cached)) return cached;
                var f = MakePositionFont(key / 2f);
                _positionFonts[key] = f;
                return f;
            }
        }

        private static Font MakePositionFont(float size)
        {
            foreach (var name in new[] { "DejaVu Sans Mono", "Consolas", "Courier New" })
            {
                Font f = null;
                try
                {
                    f = new Font(name, size, FontStyle.Bold);
                    if (f.Name == name || name == "Consolas") return f;
                }
                catch { }
                if (f != null) f.Dispose();          // a face this machine does not have
            }
            return new Font(FontFamily.GenericMonospace, size, FontStyle.Bold);
        }

        private UnitPositionStyle _positionStyle = UnitPositionStyle.Standard;

        /// <summary>
        /// The black bold reading of the axis rows - the default for EVERY unit,
        /// so a function unit's position, an axis unit's and the Axis Settings
        /// row's all read alike - or the old red-on-black readout, kept for a
        /// page that wants it.
        ///
        /// Standard became the default because a function unit next to an axis
        /// unit showed the same kind of number in two different styles, and red
        /// digits read as an alarm on a page where red means exactly that.
        /// </summary>
        [Category(" Position"), DefaultValue(UnitPositionStyle.Standard)]
        [Description("Standard: black bold digits on grey, like the Axis Settings rows. Readout: red digits on black.")]
        public UnitPositionStyle PositionStyle
        {
            get { return _positionStyle; }
            set
            {
                _positionStyle = value;
                if (value == UnitPositionStyle.Standard)
                {
                    // Exactly the Axis Settings row: a NumericDisplayHMI's own
                    // read-only grey, black text, Segoe UI 12 bold.
                    _numPosition.BackColor = Color.Gainsboro;
                    _numPosition.ForeColor = Color.Black;
                    _numPosition.Font = StandardPositionFont;
                }
                else
                {
                    _numPosition.BackColor = Color.Black;
                    _numPosition.ForeColor = Color.Red;
                }
                LayoutChildren();
            }
        }

        // ==================================================================
        // FUNCTION-LEVEL PROPERTIES
        // ==================================================================

        private int _plcNumber = 1;
        [Category(" Function")]
        public int PlcNumber { get { return _plcNumber; } set { _plcNumber = value; ApplySettings(); } }

        private int _baseOffset = 0;
        [Category(" Function")]
        [Description("ROW INDEX added to every address in its own number system (M decimal, X/Y octal).")]
        public int BaseOffset { get { return _baseOffset; } set { _baseOffset = value; ApplySettings(); } }

        private string _translationKey = "";
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string TranslationKey { get { return _translationKey; } set { _translationKey = value; ApplyTranslation(); } }

        private string _titleText = "Function";
        [Category(" Function")]
        [Description("Frame title. Used as a translation KEY; if not found in the language file, shown as-is.")]
        public string TitleTextKey { get { return _titleText; } set { _titleText = value; ApplyTranslation(); } }

        private string _titleTip = "";
        [Category(" Function")]
        public string TitleTipKey { get { return _titleTip; } set { _titleTip = value; ApplyTranslation(); } }

        // ======================= ACTION A (top button) =======================

        private string _commandAAddress = "";
        [Category(" Action A")]
        [Description("Bit address of command A (M). Empty hides the button.")]
        public string CommandAAddress { get { return _commandAAddress; } set { _commandAAddress = value; ApplySettings(); } }

        private BitActionType _commandAAction = BitActionType.Momentary;
        [Category(" Action A")]
        public BitActionType CommandAAction { get { return _commandAAction; } set { _commandAAction = value; ApplySettings(); } }

        private string _relayAAddress = "";
        [Category(" Action A")]
        [Description("Internal relay bit (M): shades the button slightly darker when ON. Empty = uses CommandAAddress.")]
        public string RelayAAddress { get { return _relayAAddress; } set { _relayAAddress = value; ApplySettings(); } }

        private string _outputAAddress = "";
        [Category(" Action A")]
        [Description("Physical output bit (Y): shades darker still and turns text white when ON.")]
        public string OutputAAddress { get { return _outputAAddress; } set { _outputAAddress = value; ApplySettings(); } }

        private string _lamp1AAddress = "";
        [Category(" Action A")]
        [Description("Input sensor lamp 1 on button A (X). Empty hides it.")]
        public string Lamp1AAddress { get { return _lamp1AAddress; } set { _lamp1AAddress = value; ApplySettings(); } }

        private string _lamp1ATip = "";
        [Category(" Action A")]
        public string Lamp1ATipKey { get { return _lamp1ATip; } set { _lamp1ATip = value; ApplySettings(); } }

        private string _lamp2AAddress = "";
        [Category(" Action A")]
        [Description("Input sensor lamp 2 on button A (X). Empty hides it.")]
        public string Lamp2AAddress { get { return _lamp2AAddress; } set { _lamp2AAddress = value; ApplySettings(); } }

        private string _lamp2ATip = "";
        [Category(" Action A")]
        public string Lamp2ATipKey { get { return _lamp2ATip; } set { _lamp2ATip = value; ApplySettings(); } }

        private string _buttonAKey = "";
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string ButtonATranslationKey { get { return _buttonAKey; } set { _buttonAKey = value; ApplyTranslation(); } }

        private string _buttonAText = ButtonGlyphs.Left;
        [Category(" Action A")]
        [Description("Button A caption. Translation KEY with literal fallback.")]
        public string ButtonATextKey { get { return _buttonAText; } set { _buttonAText = value; ApplyTranslation(); } }

        private ButtonArrowStyle _arrows = ButtonArrowStyle.LeftRight;

        /// <summary>
        /// Sets both button captions to a matching pair of arrows. Most manual
        /// movements are back/forward, but a lift or a clamp reads better with
        /// up/down - one property instead of typing two glyphs.
        /// Set Custom (or edit either caption) to keep your own text.
        /// </summary>
        [Category(" Buttons")]
        [Description("Arrow pair used on the two buttons. Custom keeps whatever captions you type.")]
        public ButtonArrowStyle Arrows
        {
            get { return _arrows; }
            set
            {
                _arrows = value;
                switch (value)
                {
                    case ButtonArrowStyle.LeftRight: _buttonAText = "\u25C0"; _buttonBText = "\u25B6"; break;
                    case ButtonArrowStyle.UpDown:    _buttonAText = "\u25B2"; _buttonBText = "\u25BC"; break;
                    case ButtonArrowStyle.DownUp:    _buttonAText = "\u25BC"; _buttonBText = "\u25B2"; break;
                    case ButtonArrowStyle.OpenClose: _buttonAText = "\u21E4"; _buttonBText = "\u21E5"; break;
                }
                if (value != ButtonArrowStyle.Custom) ApplyTranslation();
            }
        }

        private string _buttonATip = "";
        [Category(" Action A")]
        public string ButtonATipKey { get { return _buttonATip; } set { _buttonATip = value; ApplySettings(); } }

        // --- Per-button EXTRA conditions -------------------------------------
        // These are ADDED to the unit-level VisibilityConditions/EnableConditions.
        // The unit's conditions always apply and cannot be removed here (they are
        // OR-ed in), so a button can only ever be MORE restricted than its unit -
        // never less. Set these to give one button an extra requirement without
        // touching the other button in the same unit.
        private SystemCondition _visibilityAExtra = SystemCondition.None;
        [Category(" Action A")]
        [Description("EXTRA visibility conditions for button A, added on top of the unit's (inherited conditions always apply).")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        public SystemCondition ButtonAVisibilityConditions
        {
            get { return _visibilityAExtra; }
            set { _visibilityAExtra = value; ApplySettings(); }
        }

        private SystemCondition _enableAExtra = SystemCondition.None;
        [Category(" Action A")]
        [Description("EXTRA enable conditions for button A, added on top of the unit's (inherited conditions always apply).")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        public SystemCondition ButtonAEnableConditions
        {
            get { return _enableAExtra; }
            set { _enableAExtra = value; ApplySettings(); }
        }

        private PlcTrigger _enableTriggerA = new PlcTrigger();
        [Category(" Action A")]
        [Description("PLC signal that must be true before button A can be pressed.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(PlcTriggerSorter))]
        public PlcTrigger ButtonAEnableTrigger
        {
            get { return _enableTriggerA; }
            set { _enableTriggerA = value; ApplySettings(); }
        }

        // ======================= ACTION B (bottom button) =======================

        private string _commandBAddress = "";
        [Category(" Action B")]
        [Description("Bit address of command B (M). Empty = single-command unit; button A fills the frame.")]
        public string CommandBAddress { get { return _commandBAddress; } set { _commandBAddress = value; ApplySettings(); } }

        private BitActionType _commandBAction = BitActionType.Momentary;
        [Category(" Action B")]
        public BitActionType CommandBAction { get { return _commandBAction; } set { _commandBAction = value; ApplySettings(); } }

        private string _relayBAddress = "";
        [Category(" Action B")]
        public string RelayBAddress { get { return _relayBAddress; } set { _relayBAddress = value; ApplySettings(); } }

        private string _outputBAddress = "";
        [Category(" Action B")]
        public string OutputBAddress { get { return _outputBAddress; } set { _outputBAddress = value; ApplySettings(); } }

        private string _lamp1BAddress = "";
        [Category(" Action B")]
        public string Lamp1BAddress { get { return _lamp1BAddress; } set { _lamp1BAddress = value; ApplySettings(); } }

        private string _lamp1BTip = "";
        [Category(" Action B")]
        public string Lamp1BTipKey { get { return _lamp1BTip; } set { _lamp1BTip = value; ApplySettings(); } }

        private string _lamp2BAddress = "";
        [Category(" Action B")]
        public string Lamp2BAddress { get { return _lamp2BAddress; } set { _lamp2BAddress = value; ApplySettings(); } }

        private string _lamp2BTip = "";
        [Category(" Action B")]
        public string Lamp2BTipKey { get { return _lamp2BTip; } set { _lamp2BTip = value; ApplySettings(); } }

        private string _buttonBKey = "";
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string ButtonBTranslationKey { get { return _buttonBKey; } set { _buttonBKey = value; ApplyTranslation(); } }

        private string _buttonBText = ButtonGlyphs.Right;
        [Category(" Action B")]
        [Description("Button B caption. Translation KEY with literal fallback.")]
        public string ButtonBTextKey { get { return _buttonBText; } set { _buttonBText = value; ApplyTranslation(); } }

        private string _buttonBTip = "";
        [Category(" Action B")]
        public string ButtonBTipKey { get { return _buttonBTip; } set { _buttonBTip = value; ApplySettings(); } }

        private SystemCondition _visibilityBExtra = SystemCondition.None;
        [Category(" Action B")]
        [Description("EXTRA visibility conditions for button B, added on top of the unit's (inherited conditions always apply).")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        public SystemCondition ButtonBVisibilityConditions
        {
            get { return _visibilityBExtra; }
            set { _visibilityBExtra = value; ApplySettings(); }
        }

        private SystemCondition _enableBExtra = SystemCondition.None;
        [Category(" Action B")]
        [Description("EXTRA enable conditions for button B, added on top of the unit's (inherited conditions always apply).")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        public SystemCondition ButtonBEnableConditions
        {
            get { return _enableBExtra; }
            set { _enableBExtra = value; ApplySettings(); }
        }

        private PlcTrigger _enableTriggerB = new PlcTrigger();
        [Category(" Action B")]
        [Description("PLC signal that must be true before button B can be pressed.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(PlcTriggerSorter))]
        public PlcTrigger ButtonBEnableTrigger
        {
            get { return _enableTriggerB; }
            set { _enableTriggerB = value; ApplySettings(); }
        }

        // ================= optional position display (encoder) =================

        private string _positionAddress = "";
        [Category(" Position")]
        [Description("Word address of the encoder value. Empty hides the readout.")]
        public string PositionAddress { get { return _positionAddress; } set { _positionAddress = value; ApplySettings(); } }

        private DataTypeEnum _positionDataType = DataTypeEnum.Bin_16;
        [Category(" Position")]
        public DataTypeEnum PositionDataType { get { return _positionDataType; } set { _positionDataType = value; ApplySettings(); } }

        private double _positionDivisor = 1.0;
        [Category(" Position")]
        public double PositionDivisor { get { return _positionDivisor; } set { _positionDivisor = value; ApplySettings(); } }

        private int _positionDecimalPlaces = 0;
        [Category(" Position")]
        public int PositionDecimalPlaces { get { return _positionDecimalPlaces; } set { _positionDecimalPlaces = value; ApplySettings(); } }

        private int _positionDigits = 5;
        [Category(" Position")]
        [Description("Total digit count shown (like NumericDisplayHMI.Digits). The readout width auto-fits this so it doesn't waste space.")]
        public int PositionDigits { get { return _positionDigits; } set { _positionDigits = Math.Max(1, value); ApplySettings(); } }

        private string _positionSuffix = "";
        [Category(" Position")]
        public string PositionSuffix { get { return _positionSuffix; } set { _positionSuffix = value; ApplySettings(); } }

        private UnitType _positionUnitType = UnitType.None;
        [Category(" Position")]
        [Description("Unit family for the position readout. When set (e.g. Length), the value follows " +
                     "the unit chosen in Unit Settings and the suffix changes with it; PositionSuffix is " +
                     "then only used as the fallback when no unit is active.")]
        public UnitType PositionUnitType
        {
            get { return _positionUnitType; }
            set { _positionUnitType = value; ApplySettings(); }
        }

        private DynamicValue _positionOffset = new DynamicValue();
        [Category(" Position")]
        [Description("Address offset for the position tag (Static / Tag / HmiVariable), same as NumericDisplayHMI.AddressOffset.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public DynamicValue PositionOffset { get { return _positionOffset; } set { _positionOffset = value; ApplySettings(); } }

        private string _positionTip = "Current position";
        [Category(" Position")]
        public string PositionTipKey { get { return _positionTip; } set { _positionTip = value; ApplyTranslation(); } }

        // ================= security / behavior passthrough =================

        private SecurityLevel _requiredSecurity = SecurityLevel.Operator;
        [Category(" Security")]
        public SecurityLevel RequiredSecurity { get { return _requiredSecurity; } set { _requiredSecurity = value; ApplySettings(); } }

        private bool _confirmationNeeded = false;
        [Category(" Security")]
        public bool ConfirmationNeeded { get { return _confirmationNeeded; } set { _confirmationNeeded = value; ApplySettings(); } }

        private bool _logAction = true;
        [Category(" Security")]
        public bool LogAction { get { return _logAction; } set { _logAction = value; ApplySettings(); } }

        private SystemCondition _visibilityConditions = SystemCondition.None;
        // A SET of conditions, ticked in a list, like the buttons' own - it was
        // the plain enum drop-down here, which picks only one.
        [Category(" Triggers")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        public SystemCondition VisibilityConditions { get { return _visibilityConditions; } set { _visibilityConditions = value; ApplySettings(); } }

        private SystemCondition _enableConditions = SystemCondition.None;
        [Category(" Triggers")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        public SystemCondition EnableConditions { get { return _enableConditions; } set { _enableConditions = value; ApplySettings(); } }

        // --- Back-compat aliases (old property names without the Key suffix) ---
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string TitleText { get { return _titleText; } set { _titleText = value; ApplyTranslation(); } }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string TitleTip { get { return _titleTip; } set { _titleTip = value; ApplyTranslation(); } }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string ButtonAText { get { return _buttonAText; } set { _buttonAText = value; ApplyTranslation(); } }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string ButtonATip { get { return _buttonATip; } set { _buttonATip = value; ApplySettings(); } }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string ButtonBText { get { return _buttonBText; } set { _buttonBText = value; ApplyTranslation(); } }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string ButtonBTip { get { return _buttonBTip; } set { _buttonBTip = value; ApplySettings(); } }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Lamp1ATip { get { return _lamp1ATip; } set { _lamp1ATip = value; ApplySettings(); } }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Lamp2ATip { get { return _lamp2ATip; } set { _lamp2ATip = value; ApplySettings(); } }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Lamp1BTip { get { return _lamp1BTip; } set { _lamp1BTip = value; ApplySettings(); } }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Lamp2BTip { get { return _lamp2BTip; } set { _lamp2BTip = value; ApplySettings(); } }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string PositionTip { get { return _positionTip; } set { _positionTip = value; ApplyTranslation(); } }

        // ==================================================================
        // WIRING
        // ==================================================================

        private string Abs(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return "";
            return _baseOffset == 0 ? address.Trim() : PlcAddressHelper.OffsetAddress(address.Trim(), _baseOffset);
        }

        // ---- error help ------------------------------------------------------
        //
        // The unit carries its own error bits and its valve and feedback, so every
        // alarm it raises links to the right part of the cylinder manual without
        // anyone typing a code per alarm. See AlarmHelpLinks.

        private readonly AlarmHelpBadge _helpA = new AlarmHelpBadge();
        private readonly AlarmHelpBadge _helpB = new AlarmHelpBadge();
        private string _errorAAddress = "", _errorBAddress = "";
        private CylinderValve _valve = CylinderValve.Unspecified;
        private CylinderFeedback _feedback = CylinderFeedback.Unspecified;

        // THE ERROR OF A MOVEMENT BELONGS WITH THE MOVEMENT: under its action, next
        // to its command, output and lamps - not in a Help group of its own.
        [Category(" Action A"), DefaultValue(""),
         Description("Error bit set when the A (left button) movement fails. Its '?' shows on the top frame line, and button A's border blinks red. " +
                     "A single solenoid's spring-return movement can fail too: it may have an error with no command.")]
        public string ErrorAAddress { get { return _errorAAddress; } set { _errorAAddress = (value ?? "").Trim(); ApplySettings(); } }

        [Category(" Action B"), DefaultValue(""),
         Description("Error bit set when the B (right button) movement fails. Its '?' shows on the top frame line, and button B's border blinks red. " +
                     "A single solenoid's spring-return movement can fail too: it may have an error with no command.")]
        public string ErrorBAddress { get { return _errorBAddress; } set { _errorBAddress = (value ?? "").Trim(); ApplySettings(); } }

        /// <summary>
        /// Cylinder, or Axis - set by AxisUnitHMI for the unit inside it. Not in
        /// the Properties window: an axis on a page is an AxisUnitHMI, so a
        /// function unit placed by hand is always a cylinder.
        /// </summary>
        [Browsable(false), DefaultValue(UnitKind.Cylinder), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public UnitKind Kind { get { return _kind; } set { _kind = value; UpdateErrorBlink(); } }
        private UnitKind _kind = UnitKind.Cylinder;

        /// <summary>
        /// This unit's own help - photos of the real mechanism, its drawing - for
        /// EACH MOVEMENT: the two directions of a unit can be different movements
        /// with their own valve coil, sensor and error, so what to check when one
        /// fails is not what to check when the other does. Listed with the manual
        /// when that movement's error is set, and all of them behind the "?" at
        /// any time. File names in the machine's Help folder, ";"-separated.
        /// </summary>
        [Category(" Action A"), DefaultValue(""),
         Editor(typeof(Kaban.UI.Design.HelpFilesEditor), typeof(System.Drawing.Design.UITypeEditor)),
         Description("Help files for the A movement (in the machine's Help folder): shown with the manual when ErrorA is set, and behind the unit's '?' at any time.")]
        public string HelpAFiles { get { return _helpAFiles; } set { _helpAFiles = (value ?? "").Trim(); ApplySettings(); } }
        private string _helpAFiles = "";

        [Category(" Action B"), DefaultValue(""),
         Editor(typeof(Kaban.UI.Design.HelpFilesEditor), typeof(System.Drawing.Design.UITypeEditor)),
         Description("Help files for the B movement (in the machine's Help folder): shown with the manual when ErrorB is set, and behind the unit's '?' at any time.")]
        public string HelpBFiles { get { return _helpBFiles; } set { _helpBFiles = (value ?? "").Trim(); ApplySettings(); } }
        private string _helpBFiles = "";

        /// <summary>
        /// The former single list, kept so pages that set it still load: it gives
        /// its files to BOTH movements. Read, it is every file of the unit.
        /// </summary>
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never),
         DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string HelpFiles
        {
            get { return string.Join(";", HelpFileNames()); }
            set { _helpAFiles = _helpBFiles = (value ?? "").Trim(); ApplySettings(); }
        }

        private static List<string> SplitFiles(string files)
        {
            return (files ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(f => f.Trim()).Where(f => f.Length > 0).ToList();
        }

        /// <summary>One movement's help files ("A" or "B"), split, as the names were entered.</summary>
        public List<string> HelpFileNames(string side)
        {
            return SplitFiles(side == "A" ? _helpAFiles : _helpBFiles);
        }

        /// <summary>Every help file of the unit - A's, then B's not already listed.</summary>
        public List<string> HelpFileNames()
        {
            return HelpFileNames("A").Concat(HelpFileNames("B")).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<TroubleshootingCatalog.HelpDocument> Present(IEnumerable<string> files)
        {
            return files
                .Select(f => new TroubleshootingCatalog.HelpDocument
                {
                    FileName = f,
                    Title = System.IO.Path.GetFileNameWithoutExtension(f)
                })
                .Where(d => d.Exists).ToList();
        }

        /// <summary>The unit's help files that are actually present.</summary>
        public List<TroubleshootingCatalog.HelpDocument> UnitHelpDocuments()
        {
            return Present(HelpFileNames());
        }

        /// <summary>One movement's help files that are actually present.</summary>
        public List<TroubleshootingCatalog.HelpDocument> UnitHelpDocuments(string side)
        {
            return Present(HelpFileNames(side));
        }

        private readonly UnitHelpBadge _help = new UnitHelpBadge();

        /// <summary>Exact button width (0 = share the width). An axis unit sets the axis row's.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ButtonSlotWidth { get; set; }

        /// <summary>Exact position display width (0 = fit the number).</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int PositionSlotWidth { get; set; }

        // ==================================================================
        // THE VALVE AND THE FEEDBACK: WHAT EVERYTHING ELSE DEPENDS ON
        // ------------------------------------------------------------------
        // The valve decides the ACTIONS - how many buttons, commands, relays and
        // outputs the unit has; the feedback decides the LAMPS and the POSITION.
        // They head the Properties window, and TypeCheck says what the rest of
        // the unit does not match. Nothing is hidden or changed for a mismatch:
        // the page keeps working exactly as wired, and the designer is told.
        // ==================================================================

        /// <summary>
        /// The valve fitted to this cylinder. The "?" opens the cylinder manual with
        /// only this valve's part of detail 1.4 - see TroubleshootingCatalog.Tailor.
        /// </summary>
        [Category("  Valve & Feedback"), DefaultValue(CylinderValve.Unspecified), RefreshProperties(RefreshProperties.All),
         Description("The valve: it decides the ACTIONS. SingleSolenoid - one coil, one action (A or B; the spring makes the other movement). " +
                     "DoubleSolenoid / DoubleSolenoid_3Way - a coil for each movement, both actions. " +
                     "On an error the '?' opens the cylinder manual with only this valve's part.")]
        public CylinderValve ValveType { get { return _valve; } set { _valve = value; ApplySettings(); } }

        /// <summary>The feedback fitted to this cylinder: only its part of detail 1.7 is shown.</summary>
        [Category("  Valve & Feedback"), DefaultValue(CylinderFeedback.Unspecified), RefreshProperties(RefreshProperties.All),
         Description("The feedback: it decides the LAMPS and the POSITION. SingleMagnetic / SingleInductive - one lamp (A or B). " +
                     "TwoEndMagnetic / MagneticPlusPressureSwitch - a lamp on each action. AnalogueMagnetic - no lamp, the Position. " +
                     "EncoderPlusReference - one lamp (the reference) and the Position. NoSensor - none drawn: no lamp, no error bit. " +
                     "On an error the '?' opens the cylinder manual with only this feedback's part.")]
        public CylinderFeedback FeedbackType { get { return _feedback; } set { _feedback = value; ApplySettings(); } }

        /// <summary>What the unit's actions, lamps and position do not match of its valve and feedback - "OK" when all do.</summary>
        [Category("  Valve & Feedback"), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
         Description("The unit checked against its ValveType and FeedbackType: the actions the valve drives, the lamps and position the feedback gives.")]
        public string TypeCheck
        {
            get
            {
                var p = TypeProblems();
                return p.Count == 0 ? "OK" : string.Join("  |  ", p);
            }
        }

        private static bool Has(string address) { return !string.IsNullOrWhiteSpace(address); }

        /// <summary>
        /// The unit against its valve and feedback, in plain words - empty when it
        /// matches (or when neither is set: a motor, a blower, a unit not yet typed).
        ///
        /// Valve: a single solenoid drives one movement - one command, one output;
        /// a double solenoid (5/2 or 5/3) drives both - a command on each action.
        /// Feedback: one sensor is one lamp; a sensor at each end (or a sensor and
        /// the pressure switch) is a lamp on each action; an analogue sensor is the
        /// position and no lamp; an encoder is the position and its reference lamp.
        /// A position belongs only to the last two.
        ///
        /// A type added to the enums later has no rule here yet - it is reported
        /// as such, so it is not taken as checked.
        /// </summary>
        public List<string> TypeProblems()
        {
            var p = new List<string>();
            if (_kind == UnitKind.Axis) return p;
            bool cmdA = Has(_commandAAddress), cmdB = Has(_commandBAddress);
            int lampsA = (Has(_lamp1AAddress) ? 1 : 0) + (Has(_lamp2AAddress) ? 1 : 0);
            int lampsB = (Has(_lamp1BAddress) ? 1 : 0) + (Has(_lamp2BAddress) ? 1 : 0);
            int lamps = lampsA + lampsB;
            bool pos = Has(_positionAddress);

            switch (_valve)
            {
                case CylinderValve.Unspecified:
                    if (_feedback != CylinderFeedback.Unspecified)
                        p.Add("ValveType is not set: choose the valve - it decides the actions (and the manual shows every valve).");
                    break;
                case CylinderValve.SingleSolenoid:
                    if (cmdA && cmdB)
                        p.Add("SingleSolenoid: one coil drives ONE movement, the spring the other - keep one action: clear CommandAAddress or CommandBAddress.");
                    else if (!cmdA && !cmdB)
                        p.Add("SingleSolenoid: set the command of the driven movement - CommandAAddress or CommandBAddress.");
                    if (Has(_outputAAddress) && Has(_outputBAddress))
                        p.Add("SingleSolenoid: one coil is one output - set OutputAAddress or OutputBAddress, not both.");
                    if (cmdA != cmdB)
                    {
                        string idle = cmdA ? "B" : "A";
                        if (Has(cmdA ? _outputBAddress : _outputAAddress) || Has(cmdA ? _relayBAddress : _relayAAddress))
                            p.Add("SingleSolenoid: Action " + idle + " has no command, so its output and relay are not used - clear them.");
                    }
                    break;
                case CylinderValve.DoubleSolenoid:
                case CylinderValve.DoubleSolenoid_3Way:
                    if (!cmdA || !cmdB)
                        p.Add(_valve + ": a coil for each movement - set both CommandAAddress and CommandBAddress" +
                              (!cmdA && !cmdB ? "." : " (" + (cmdA ? "B" : "A") + " is missing)."));
                    break;
                default:
                    p.Add("ValveType " + (int)_valve + " has no check yet - check the actions by hand.");
                    break;
            }

            string noPosition = ": no position is read - PositionAddress is for AnalogueMagnetic and EncoderPlusReference feedback.";
            switch (_feedback)
            {
                case CylinderFeedback.Unspecified:
                    if (_valve != CylinderValve.Unspecified)
                        p.Add("FeedbackType is not set: choose the feedback - it decides the lamps and the position.");
                    break;
                case CylinderFeedback.SingleMagnetic:
                case CylinderFeedback.SingleInductive:
                    if (lamps != 1)
                        p.Add(_feedback + ": one sensor at one end - one lamp, on Action A or B" + (lamps == 0 ? " (none set)." : " (" + lamps + " set)."));
                    if (pos) p.Add(_feedback + noPosition);
                    break;
                case CylinderFeedback.TwoEndMagnetic:
                case CylinderFeedback.MagneticPlusPressureSwitch:
                    if (lampsA != 1 || lampsB != 1)
                        p.Add(_feedback + (_feedback == CylinderFeedback.TwoEndMagnetic
                                  ? ": a sensor at each end - one lamp on Action A and one on Action B"
                                  : ": the end sensor and the pressure switch - one lamp on Action A and one on Action B") +
                              " (A has " + lampsA + ", B has " + lampsB + ").");
                    // A CLAMP MAY MEASURE: the pressure switch says it has clamped,
                    // and an encoder on it may show where - the profile's width on
                    // FA2010's profile recognition. That position is a reading, not
                    // the movement's feedback, so it is not a mismatch here.
                    if (pos && _feedback == CylinderFeedback.TwoEndMagnetic) p.Add(_feedback + noPosition);
                    break;
                case CylinderFeedback.AnalogueMagnetic:
                    if (lamps > 0) p.Add("AnalogueMagnetic: the position is the feedback - no lamps (" + lamps + " set).");
                    if (!pos) p.Add("AnalogueMagnetic: set PositionAddress - the analogue position.");
                    break;
                case CylinderFeedback.EncoderPlusReference:
                    if (lamps != 1) p.Add("EncoderPlusReference: one lamp - the reference sensor" + (lamps == 0 ? " (none set)." : " (" + lamps + " set)."));
                    if (!pos) p.Add("EncoderPlusReference: set PositionAddress - the encoder position.");
                    break;
                case CylinderFeedback.NoSensor:
                    if (lamps > 0) p.Add("NoSensor: the cylinder has no sensor - no lamps (" + lamps + " set).");
                    if (pos) p.Add("NoSensor" + noPosition);
                    if (Has(_errorAAddress) || Has(_errorBAddress))
                        p.Add("NoSensor: nothing confirms the movement, so nothing can fail it - an error bit needs a sensor (choose the feedback).");
                    break;
                default:
                    p.Add("FeedbackType " + (int)_feedback + " has no check yet - check the lamps and position by hand.");
                    break;
            }
            return p;
        }

        /// <summary>The former name of ValveType, kept for code that uses it.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public CylinderValve Valve { get { return ValveType; } set { ValveType = value; } }

        /// <summary>The former name of FeedbackType, kept for code that uses it.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public CylinderFeedback Feedback { get { return FeedbackType; } set { FeedbackType = value; } }

        /// <summary>
        /// Makes the "?" an AXIS's: shown while any of the axis's three drive
        /// codes is not zero, with one menu entry per code that opens only that
        /// code's entry of its manual (see AlarmHelpBadge). The error bit keeps
        /// driving the red border. Called by AxisUnitHMI with its axis row's
        /// code words; empty addresses leave the "?" on the error bit.
        /// </summary>
        public void SetAxisCodes(string servoAlarmAddress, string axisAlarmAddress, string axisWarningAddress)
        {
            _codeWords = new[] { servoAlarmAddress ?? "", axisAlarmAddress ?? "", axisWarningAddress ?? "" };
            _helpA.SetCodeWords(null, null, null);
            _helpB.SetCodeWords(servoAlarmAddress, axisAlarmAddress, axisWarningAddress);
            ApplyAlarmTips();
            UpdateHelpBadge();
        }

        private string[] _codeWords = { "", "", "" };

        /// <summary>
        /// Each button's tooltip names its alarm bit - its own side's error bit
        /// (an axis: the axis's, on both) - and an axis's drive code words.
        /// </summary>
        private void ApplyAlarmTips()
        {
            if (_kind == UnitKind.Axis)
            {
                var error = Abs(!string.IsNullOrWhiteSpace(_errorBAddress) ? _errorBAddress : _errorAAddress);
                var codes = new List<KeyValuePair<string, string>>();
                if (!string.IsNullOrWhiteSpace(_codeWords[0])) codes.Add(new KeyValuePair<string, string>(LanguageManager.GetString("Tip_CodeServoAlarm", "Servo alarm"), _codeWords[0]));
                if (!string.IsNullOrWhiteSpace(_codeWords[1])) codes.Add(new KeyValuePair<string, string>(LanguageManager.GetString("Tip_CodeAxisAlarm", "Axis alarm"), _codeWords[1]));
                if (!string.IsNullOrWhiteSpace(_codeWords[2])) codes.Add(new KeyValuePair<string, string>(LanguageManager.GetString("Tip_CodeAxisWarning", "Axis warning"), _codeWords[2]));
                foreach (var b in new[] { _btnA, _btnB })
                {
                    b.ErrorAddress = error;
                    b.AlarmCodeAddresses.Clear();
                    b.AlarmCodeAddresses.AddRange(codes);
                }
            }
            else
            {
                _btnA.ErrorAddress = Abs(_errorAAddress);
                _btnB.ErrorAddress = Abs(_errorBAddress);
                _btnA.AlarmCodeAddresses.Clear();
                _btnB.AlarmCodeAddresses.Clear();
            }
        }

        /// <summary>The unit's frame tooltip: its own tip, then its alarm bits (and an axis's code words).</summary>
        internal string FrameTip()
        {
            var lines = new List<string>();
            var tip = Tr(_titleTip);
            if (!string.IsNullOrWhiteSpace(tip)) lines.Add(tip);
            Func<string, string> named = a =>
            {
                var def = AlarmManager.FindByStationAndAddress(_plcNumber, Abs(a));
                var text = ButtonHMI.AlarmText(def);
                return Abs(a) + (string.IsNullOrWhiteSpace(text) ? "" : " " + text);
            };
            string alarm = LanguageManager.GetString("Tip_AlarmBit", "Alarm bit");
            if (_kind == UnitKind.Axis)
            {
                var error = !string.IsNullOrWhiteSpace(_errorBAddress) ? _errorBAddress : _errorAAddress;
                if (!string.IsNullOrWhiteSpace(error)) lines.Add(alarm + ": " + named(error));
                foreach (var kv in _btnB.AlarmCodeAddresses) lines.Add(kv.Key + ": " + kv.Value);
            }
            else
            {
                // each movement by its name ("Clamp"), as the step-by-step help calls it
                foreach (var side in new[] { "A", "B" })
                {
                    var bit = side == "A" ? _errorAAddress : _errorBAddress;
                    if (string.IsNullOrWhiteSpace(bit)) continue;
                    string move;
                    try { move = TroubleshootingCatalog.MovementName(MovementFor(side)); } catch { move = side; }
                    lines.Add(alarm + " (" + (string.IsNullOrWhiteSpace(move) ? side : move) + "): " + named(bit));
                }
            }
            return string.Join("\r\n", lines);
        }

        /// <summary>This unit's error bits, for AlarmHelpLinks.</summary>
        public IEnumerable<Kaban.UI.Helpers.AlarmHelpLinks.Link> HelpLinks()
        {
            var name = string.IsNullOrEmpty(_titleText) ? Name : LanguageManager.GetString(_titleText, _titleText);
            foreach (var address in new[] { _errorAAddress, _errorBAddress })
                if (!string.IsNullOrWhiteSpace(address))
                    yield return new Kaban.UI.Helpers.AlarmHelpLinks.Link
                    {
                        // An axis faults as a servo; its valve and feedback mean nothing.
                        Kind = _kind == UnitKind.Axis ? TroubleshootingCatalog.ErrorType.ServoFault
                                                      : TroubleshootingCatalog.ErrorType.CylinderMovement,
                        Station = _plcNumber,
                        Address = address,
                        Valve = _kind == UnitKind.Axis ? 0 : (int)_valve,
                        Feedback = _kind == UnitKind.Axis ? 0 : (int)_feedback,
                        Unit = name,
                        // That movement's own files: the other direction's are about another movement.
                        HelpFiles = HelpFileNames(address == _errorAAddress ? "A" : "B"),
                        ServoAlarm = _kind == UnitKind.Axis ? _codeWords[0] : "",
                        AxisAlarm = _kind == UnitKind.Axis ? _codeWords[1] : "",
                        AxisWarning = _kind == UnitKind.Axis ? _codeWords[2] : "",
                        // Which side's error bit this is: the alarm list's help names
                        // that movement's devices, like the unit's own "?".
                        Movement = _kind == UnitKind.Axis ? null : MovementFor(address == _errorAAddress ? "A" : "B"),
                        MovementNow = MovementNowFor(_kind == UnitKind.Axis ? null : address == _errorAAddress ? "A" : "B")
                    };
        }

        private Func<TroubleshootingCatalog.MovementInfo> MovementNowFor(string side)
        {
            if (side == null) return null;
            return () => IsDisposed ? null : MovementFor(side);
        }

        /// <summary>
        /// The valve output a side drives: its own OutputAddress, or - as the
        /// pages rarely set one - the output its command bit drives on the
        /// machine's drawings (WiringTable: MAN_x on M.. -> OUT_x on Y..).
        /// </summary>
        private string DrivenOutput(string side)
        {
            bool a = side == "A";
            var own = a ? _outputAAddress : _outputBAddress;
            if (!string.IsNullOrWhiteSpace(own)) return Abs(own);
            var cmd = a ? _commandAAddress : _commandBAddress;
            if (string.IsNullOrWhiteSpace(cmd) || DesignMode) return "";
            try
            {
                var w = WiringTable.OutputFor(_plcNumber, Abs(cmd));
                return w != null ? w.Address : "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// The alarm the HMI raised itself for one of this unit's movements
        /// (AlarmGeneration), while it is on the list - or null. Found by the
        /// devices the movement is named by: the unit on the page is not the one
        /// discovery built its watch from.
        /// </summary>
        private AlarmItem GeneratedFor(string side)
        {
            if (_kind == UnitKind.Axis || DesignMode) return null;
            bool a = side == "A";
            return AlarmGeneration.ActiveFor(_plcNumber, side,
                Abs(a ? _errorAAddress : _errorBAddress), Abs(a ? _commandAAddress : _commandBAddress),
                DrivenOutput(side), DrivenOutput(a ? "B" : "A"));
        }

        /// <summary>
        /// The unit's movements for the alarms the HMI raises itself: what drives
        /// each one (its output, or the spring's when the coil's output goes OFF),
        /// what confirms it (its valve and feedback decide), what names its alarm.
        /// Null for an axis unit (its drive's codes are the axis row's) or a unit
        /// with nothing to confirm a movement.
        /// </summary>
        public AlarmGeneration.Cylinder BuildWatch()
        {
            if (_kind == UnitKind.Axis) return null;
            var name = string.IsNullOrEmpty(_titleText) ? Name : Tr(_titleText);
            return AlarmGeneration.Build(_plcNumber, "", name, (int)_valve, (int)_feedback, MovementFor("A"), MovementFor("B"));
        }

        /// <summary>The devices one side's error bit is judged by: command, both coils, the sensors of both ends.</summary>
        private IEnumerable<string> SignalsFor(string side)
        {
            if (_kind == UnitKind.Axis) return Enumerable.Empty<string>();
            bool a = side == "A";
            return new[]
            {
                Abs(a ? _commandAAddress : _commandBAddress), DrivenOutput(side), DrivenOutput(a ? "B" : "A"),
                Abs(_lamp1AAddress), Abs(_lamp2AAddress), Abs(_lamp1BAddress), Abs(_lamp2BAddress)
            }.Where(x => !string.IsNullOrWhiteSpace(x));
        }

        /// <summary>
        /// The devices of one side's movement, for the top of the tailored manual:
        /// A is the left button's (ErrorA), B the right button's (ErrorB) - each
        /// with its state just before that side's error bit came on, and now.
        /// </summary>
        public TroubleshootingCatalog.MovementInfo MovementFor(string side)
        {
            bool a = side == "A";
            var watcher = a ? _helpA : _helpB;
            var snap = watcher.AtAlarm;
            var live = watcher.LiveSignals;
            Func<string, string, TroubleshootingCatalog.MovementInfo.Device> dev = (address, name) =>
            {
                if (string.IsNullOrWhiteSpace(address)) return null;
                var d = new TroubleshootingCatalog.MovementInfo.Device { Address = Abs(address), Name = Tr(name) };
                int v;
                if (snap != null) d.AtAlarm = snap.State(d.Address);
                if (live != null && live.TryGetValue(d.Address.Trim().ToUpperInvariant(), out v)) d.Now = v;
                return d;
            };
            var mv = new TroubleshootingCatalog.MovementInfo
            {
                Station = _plcNumber,
                Unit = string.IsNullOrEmpty(_titleText) ? Name : Tr(_titleText),
                Side = a ? "A" : "B",
                Name = Tr(a ? _buttonAText : _buttonBText),
                // a single-command unit has no other button: its other movement is the spring's
                OtherName = string.IsNullOrWhiteSpace(a ? _commandBAddress : _commandAAddress) && string.IsNullOrWhiteSpace(DrivenOutput(a ? "B" : "A"))
                            ? "" : Tr(a ? _buttonBText : _buttonAText),
                Command = dev(a ? _commandAAddress : _commandBAddress, a ? _buttonATip : _buttonBTip),
                Output = dev(DrivenOutput(side), ""),
                OtherCommand = dev(a ? _commandBAddress : _commandAAddress, a ? _buttonBTip : _buttonATip),
                OtherOutput = dev(DrivenOutput(a ? "B" : "A"), ""),
                Error = Abs(a ? _errorAAddress : _errorBAddress),
                Position = Abs(_positionAddress),
                AlarmTime = snap != null ? snap.Time : (DateTime?)null,
                SeenRising = snap != null && snap.SeenRising
            };
            foreach (var d in a ? new[] { dev(_lamp1AAddress, _lamp1ATip), dev(_lamp2AAddress, _lamp2ATip) }
                                : new[] { dev(_lamp1BAddress, _lamp1BTip), dev(_lamp2BAddress, _lamp2BTip) })
                if (d != null) mv.Feedback.Add(d);
            // What its button waits for (the unit's and the button's own enable conditions).
            var set = _enableConditions | (a ? ButtonAEnableConditions : ButtonBEnableConditions);
            var unmet = (a ? _btnA : _btnB).UnmetConditions;
            foreach (var part in MachineConditionMap.Parts(set))
            {
                var name = MachineConditionMap.NameOf(part);
                ConditionRule rule = Enum.IsDefined(typeof(SystemCondition), part) ? MachineConditionMap.GetRule(part)
                    : MachineConditionMap.GetAllMachineBits().Where(kv => kv.Key == (long)part).Select(kv => (ConditionRule)kv.Value).FirstOrDefault();
                mv.Conditions.Add(new TroubleshootingCatalog.MovementInfo.Condition
                {
                    Name = name,
                    Rule = rule != null ? MachineConditionMap.RuleText(rule) : "",
                    Met = unmet == null ? (bool?)null : !unmet.Contains(name)
                });
            }
            foreach (var d in a ? new[] { dev(_lamp1BAddress, _lamp1BTip), dev(_lamp2BAddress, _lamp2BTip) }
                                : new[] { dev(_lamp1AAddress, _lamp1ATip), dev(_lamp2AAddress, _lamp2ATip) })
                if (d != null) mv.OppositeFeedback.Add(d);
            return mv;
        }

        /// <summary>
        /// The step-by-step troubleshooter for the movement that failed (the unit's
        /// devices, its valve and feedback, what was seen at the alarm) - what the
        /// "?" offers first for a fault; the manual follows as the general view.
        /// </summary>
        private TroubleshootingCatalog.HelpDocument StepsDocument(string side)
        {
            var manual = CylinderDocument(side);
            if (manual == null) return null;
            manual.StepByStep = true;
            var mv = manual.Movement;
            // An alarm the HMI raised itself knows which signal failed: the page
            // asks only about that one.
            var gen = (side == "A" ? _helpA : _helpB).GeneratedItem;
            manual.Failure = AlarmGeneration.FailureOf(gen);
            manual.Title = LanguageManager.GetString("Hlp_CylinderSteps", "Step by step") +
                           (mv == null ? "" : " - " + TroubleshootingCatalog.MovementName(mv)) +
                           (manual.Failure > 0 ? " (" + gen.ShownCode + ")" : "");
            return manual;
        }

        /// <summary>The cylinder manual for this unit - its valve and feedback, and the movement that failed when given.</summary>
        private TroubleshootingCatalog.HelpDocument CylinderDocument(string side)
        {
            string file;
            if (!TroubleshootingCatalog.DocumentForType.TryGetValue(TroubleshootingCatalog.ErrorType.CylinderMovement, out file) ||
                string.IsNullOrEmpty(file)) return null;
            var mv = side == null ? null : MovementFor(side);
            return new TroubleshootingCatalog.HelpDocument
            {
                FileName = file,
                // No unit name: the list opens from the unit's own "?". The
                // movement is named - it is what tells two entries apart.
                Title = LanguageManager.GetString("Hlp_Cylinder", "Cylinder troubleshooting") +
                        (mv == null ? "" : " - " + TroubleshootingCatalog.MovementName(mv)),
                Valve = TroubleshootingCatalog.CanonicalValve((int)_valve),
                Feedback = TroubleshootingCatalog.CanonicalFeedback((int)_feedback),
                Movement = mv
            };
        }

        /// <summary>
        /// Everything behind the "?" right now. While an error is set: the help for
        /// what failed first - the manual tailored to this cylinder and to the
        /// movement whose error bit is set (an axis: the entry of each drive code
        /// it shows) - then the alarm's own files. With nothing failed there is
        /// no troubleshooting to offer (30 September). The unit's own files
        /// always come last.
        /// </summary>
        public List<TroubleshootingCatalog.HelpDocument> HelpDocuments()
        {
            var docs = new List<TroubleshootingCatalog.HelpDocument>();
            bool errA = _helpA.IsActive, errB = _helpB.IsActive;

            if (_kind == UnitKind.Axis)
            {
                if (errB) docs.AddRange(_helpB.Documents());           // the drive codes (or the error bit)
                if (errA) docs.AddRange(_helpA.Documents());
                // The alarm list names the unit in its entries; its own "?" need not.
                var unit = string.IsNullOrEmpty(_titleText) ? "" : Tr(_titleText);
                if (unit.Length > 0)
                    foreach (var d in docs)
                        d.Title = d.Title.Replace(" - " + unit, "").Replace("  (" + unit + ")", "");
            }
            else
            {
                foreach (var side in new[] { errA ? "A" : null, errB ? "B" : null })
                {
                    if (side == null) continue;
                    var steps = StepsDocument(side);
                    if (steps != null) docs.Add(steps);
                    var d = CylinderDocument(side);
                    if (d != null) docs.Add(d);
                    // The failed movement's own files, then the alarm's.
                    docs.AddRange(UnitHelpDocuments(side));
                    // The alarm's own helper files, as the alarm list shows them.
                    var bit = side == "A" ? _errorAAddress : _errorBAddress;
                    var def = AlarmManager.FindByStationAndAddress(_plcNumber, bit);
                    if (def != null && def.HelperKeys != null)
                        foreach (var entry in def.HelperKeys.Where(f => !string.IsNullOrWhiteSpace(f)))
                            foreach (var n in entry.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                                docs.Add(new TroubleshootingCatalog.HelpDocument { FileName = n.Trim(), Title = System.IO.Path.GetFileNameWithoutExtension(n.Trim()) });
                }
                // THE CYLINDER MANUAL ONLY FOR A FAULT (30 September): with nothing
                // failed, the "?" offers the unit's own files and no troubleshooting.
            }
            docs.AddRange(UnitHelpDocuments());

            // One entry per file - except the tailored and coded ones, which differ by what they show.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return docs.Where(d => d.Exists && (d.Movement != null || d.Valve != 0 || d.Feedback != 0 ||
                                                !string.IsNullOrEmpty(d.Code) || seen.Add(d.FileName))).ToList();
        }

        /// <summary>
        /// The "?"'s state: Error while an error bit is set (Warning when an axis
        /// shows only warnings), Info while the unit has its own files to offer -
        /// the cylinder manual is offered for a fault only - and hidden otherwise:
        /// a mark that opens nothing teaches the operator to ignore it. It is blue
        /// in every state (UnitHelpBadge); the state picks its tooltip.
        /// </summary>
        private void UpdateHelpBadge()
        {
            bool errA = _helpA.IsActive, errB = _helpB.IsActive;
            UnitHelpBadge.BadgeState state;
            if (errA || errB)
                state = (!errA || _helpA.IsWarningOnly) && (!errB || _helpB.IsWarningOnly)
                        ? UnitHelpBadge.BadgeState.Warning : UnitHelpBadge.BadgeState.Error;
            else if (DesignMode)
                state = _helpAFiles.Length > 0 || _helpBFiles.Length > 0
                        ? UnitHelpBadge.BadgeState.Info : UnitHelpBadge.BadgeState.Hidden;
            else
                state = UnitHelpDocuments().Count > 0 ? UnitHelpBadge.BadgeState.Info : UnitHelpBadge.BadgeState.Hidden;
            _help.State = state;

            if (!DesignMode && state != UnitHelpBadge.BadgeState.Hidden)
            {
                string tip;
                if (state == UnitHelpBadge.BadgeState.Info)
                    tip = LanguageManager.GetString("Tip_UnitHelp", "Help for this unit") + "\r\n" +
                          string.Join("\r\n", HelpDocuments().Select(d => d.Title));
                else
                    tip = string.Join("\r\n", new[] { errA ? _helpA.TipText : "", errB ? _helpB.TipText : "" }
                                                   .Where(t => !string.IsNullOrEmpty(t))) +
                          "\r\n" + LanguageManager.GetString("Tip_HelpBadge", "Click for help");
                Kaban.UI.Controls.UiStyle.SetTip(UiStyle.Tips(), _help, tip.Trim());
            }
            LayoutBadges();
        }

        // NO ROOM IS MADE FOR THE "?" ANY MORE. It used to sit on the side lines,
        // and a unit with an error bit narrowed its frame by half a badge on that
        // side to make room - so an axis unit (which always has one) rendered a
        // few pixels narrower than a function unit of the very same width. The
        // badges now sit on the top line, which the frame has anyway.

        private void ApplySettings()
        {
            _helpA.PlcNumber = _plcNumber; _helpA.Address = _errorAAddress;
            _helpB.PlcNumber = _plcNumber; _helpB.Address = _errorBAddress;
            if (this.Padding != Padding.Empty) this.Padding = Padding.Empty;

            // Relay fallback: RelayAddress/RelayBAddress are an EDGE case - when a
            // unit has no separate internal relay bit, the command bit itself is the
            // relay for the darker-on-press feedback. So default them to the command.
            string relayA = string.IsNullOrWhiteSpace(_relayAAddress) ? _commandAAddress : _relayAAddress;
            string relayB = string.IsNullOrWhiteSpace(_relayBAddress) ? _commandBAddress : _relayBAddress;

            ConfigureButton(_btnA, _commandAAddress, _commandAAction, relayA, _outputAAddress,
                _lamp1AAddress, _lamp1ATip, _lamp2AAddress, _lamp2ATip, _buttonATip,
                _visibilityAExtra, _enableAExtra, _enableTriggerA);
            ConfigureButton(_btnB, _commandBAddress, _commandBAction, relayB, _outputBAddress,
                _lamp1BAddress, _lamp1BTip, _lamp2BAddress, _lamp2BTip, _buttonBTip,
                _visibilityBExtra, _enableBExtra, _enableTriggerB);
            ApplyAlarmTips();

            bool hasPosition = !string.IsNullOrWhiteSpace(_positionAddress);
            _numPosition.Visible = hasPosition;
            if (!hasPosition)
            {
                // Park it off-screen and collapsed so a stale/early-return layout
                // can never leave a black box floating over the caption.
                _numPosition.SetBounds(-1000, -1000, 1, 1);
            }
            _numPosition.PlcNumber = _plcNumber;
            _numPosition.Address = Abs(_positionAddress);
            _numPosition.AddressOffset = _positionOffset;
            _numPosition.DataType = _positionDataType;
            _numPosition.Divisor = _positionDivisor;
            _numPosition.DecimalPlaces = _positionDecimalPlaces;
            _numPosition.Digits = _positionDigits;
            _numPosition.Suffix = _positionSuffix;
            _numPosition.UnitType = _positionUnitType;   // follows Unit Settings when set
            _numPosition.VisibilityConditions = _visibilityConditions;

            ApplyTranslation();
            LayoutChildren();
            // IN THE DESIGNER, a unit that does not match its valve and feedback is
            // framed amber - seen on the page, not only in its TypeCheck line.
            if (DesignMode)
            {
                var frame = TypeProblems().Count > 0 ? TypeMismatchFrame : RoundedGroupBox.NormalBorder;
                if (_grp.BorderColor != frame) { _grp.BorderColor = frame; _grp.Invalidate(); }
            }
            // Not while the page is being built: every property the designer code
            // sets runs this, and the "?" looks its files up on disk. Done once the
            // unit has a window (OnHandleCreated), and on every change after that.
            if (IsHandleCreated || DesignMode) UpdateHelpBadge();
        }

        /// <summary>The designer's frame for a unit whose wiring does not match its types.</summary>
        internal static readonly Color TypeMismatchFrame = Color.FromArgb(230, 150, 30);

        private void ConfigureButton(ButtonHMI btn, string cmdAddress, BitActionType action,
            string relay, string output, string lamp1, string lamp1Tip, string lamp2, string lamp2Tip, string btnTip,
            SystemCondition extraVisibility, SystemCondition extraEnable, PlcTrigger enableTrigger)
        {
            btn.Visible = !string.IsNullOrWhiteSpace(cmdAddress);
            btn.SlotDisabled = string.IsNullOrWhiteSpace(cmdAddress); // reserved empty slot stays hidden
            btn.PlcNumber = _plcNumber;
            btn.RequiredSecurity = _requiredSecurity;
            btn.ConfirmationNeeded = _confirmationNeeded;
            btn.LogAction = _logAction;

            // INHERITED + EXTRA. The unit's conditions are always OR-ed in, so a
            // per-button setting can only ADD a requirement - it can never remove
            // one the whole unit depends on, and it never affects the other button.
            btn.VisibilityConditions = _visibilityConditions | extraVisibility;
            btn.EnableConditions = _enableConditions | extraEnable;
            btn.EnableTrigger = enableTrigger ?? new PlcTrigger();

            btn.BitActions.Clear();
            if (!btn.Visible) return;

            string absCmd = Abs(cmdAddress);
            btn.BitActions.Add(new BitAction { Address = absCmd, Action = action });

            // Everything visual lives inside the button (per the sketch):
            btn.RelayAddress = string.IsNullOrWhiteSpace(relay) ? absCmd : Abs(relay);   // M -> a bit darker
            btn.OutputAddress = Abs(output);                                             // Y -> darker + white text
            btn.Lamp1Address = Abs(lamp1);                                               // X sensors
            btn.Lamp1Tip = lamp1Tip;
            btn.Lamp2Address = Abs(lamp2);
            btn.Lamp2Tip = lamp2Tip;
            btn.ToolTipText = btnTip;
        }

        private bool _frameTipWired;

        public void ApplyTranslation()
        {
            // Uses GetString(key, fallback) - present in every Core build - instead
            // of the newer Localize(), so a stale Core DLL in the VS designer can't
            // cause a MissingMethodException. Passing the text as BOTH key and
            // fallback gives the same "translate if key exists, else show literally"
            // behavior.
            _grp.Text = Tr(_titleText);
            _helpA.UnitName = _helpB.UnitName = _grp.Text;     // named in the help titles
            _btnA.Text = Tr(_buttonAText);
            _btnB.Text = Tr(_buttonBText);

            // The frame's tip is shown by the frame itself, built as it shows (its alarm bits with it).
            if (!_frameTipWired && !DesignMode) { _frameTipWired = true; UiStyle.HoverTip(_toolTip, _grp, FrameTip); }
            _toolTip.SetToolTip(_numPosition, Tr(_positionTip));
        }

        // Local helper: translation key with literal fallback. Kept here (not in
        // Core) so this control never depends on a Core method the designer's
        // cached DLL might predate.
        private static string Tr(string keyOrText)
        {
            if (string.IsNullOrEmpty(keyOrText)) return "";
            return LanguageManager.GetString(keyOrText, keyOrText);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutChildren();
            ApplyUnitCornerRegion();
        }

        // Slightly rounded outer corners for a softer look (radius small enough
        // not to clip the GroupBox caption).
        private void ApplyUnitCornerRegion()
        {
            int radius = 4;
            if (this.Width < radius * 2 || this.Height < radius * 2) return;
            int d = radius * 2;
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                var r = new Rectangle(0, 0, this.Width, this.Height);
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                this.Region = new Region(path);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateHelpBadge();
            // Re-run layout once the control is realized. This makes instances
            // that were placed on a form with an OLDER version of this control
            // (stale serialized child bounds) correct themselves at load/design
            // time instead of showing overflowing buttons or a floating readout.
            LayoutChildren();
            ApplyUnitCornerRegion();
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            LayoutChildren();
        }

        /// <summary>
        /// Forces an immediate re-layout. Called by MainForm after a screen is
        /// swapped in: navigating away removes the whole screen and navigating back
        /// re-adds it, which fires no resize/parent/visible event on the units
        /// themselves, so they would otherwise keep the bounds they had while
        /// detached (the broken buttons after a page change).
        /// </summary>
        public void Relayout()
        {
            LayoutChildren();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (this.Visible) LayoutChildren();
        }

        /// <summary>
        /// Horizontal layout:
        ///   no position -> [ A | B ]  (each half width, full height)
        ///   with position -> [ A | position | B ]  (buttons shrink to fit it in the center)
        /// Empty command slots render as a blank filler so a 1-button unit looks
        /// like a 2-button one. Buttons fill the full frame height (the position
        /// readout no longer steals a strip when there's no device).
        /// </summary>
        private bool _inLayout = false;
        // Set env var KABAN_LAYOUT_DEBUG=1 to trace layout geometry to stderr.
        private static readonly bool LayoutDebug =
            Environment.GetEnvironmentVariable("KABAN_LAYOUT_DEBUG") == "1";
        private void LayoutTrace(string msg)
        {
            if (LayoutDebug) Console.Error.WriteLine("[LAYOUT] " + msg);
        }

        private void LayoutChildren()
        {
            if (_grp == null) return;
            if (_inLayout) return;
            _inLayout = true;
            try
            {
            // AT THE MAGNIFYING FACTOR: the unit is scaled with its page, so its
            // gaps and its button height are too (UiScale.For - factor 1 before
            // the page is scaled). Fixed at factor 1 they left, at 2, a thin row of
            // buttons in a tall frame and a readout too short for its enlarged
            // digits, which then showed nothing at all.
            Func<int, int> px = v => UiScale.For(this, v);
            int gap = px(6);

            // ROOT-CAUSE FIX: _grp is Dock.Fill but during the initial
            // OnHandleCreated/OnParentChanged pass it has NOT been resized to the
            // unit yet (it still reports its default 200x100). Using its ClientSize
            // then made the buttons overflow the unit. Compute from the UNIT's own
            // client area instead - _grp fills it exactly, so this is correct and
            // available immediately.
            int leftPad = px(8);
            int rightPad = px(8);
            // SYMMETRIC spacing: the caption is DRAWN inside the panel (no reserved
            // band), so the visual gap under the caption and above the bottom border
            // must both equal the side gap (6). topPad therefore clears the caption
            // height plus one side-gap; the button row is anchored, not centred.
            int capH = (int)Math.Ceiling(_grp.Font.GetHeight());
            int topPad = capH + px(6);
            int botPad = px(6);

            int areaX = leftPad;
            int areaY = topPad;
            // The full width, whatever error bits the unit has (see ApplySettings).
            int areaW = this.ClientSize.Width - leftPad - rightPad;
            int areaH = this.ClientSize.Height - topPad - botPad;

            LayoutTrace($"unit={this.Width}x{this.Height} unitClient={this.ClientSize.Width}x{this.ClientSize.Height} grpClient={_grp.ClientSize.Width}x{_grp.ClientSize.Height} area={areaW}x{areaH}");

            if (areaW < px(30) || areaH < px(8))
            {
                LayoutTrace($"EARLY-RETURN area too small ({areaW}x{areaH})");
                // Too small to lay out yet - make sure the readout isn't left
                // floating over the caption.
                if (string.IsNullOrWhiteSpace(_positionAddress)) _numPosition.Visible = false;
                return;
            }

            bool hasA = !string.IsNullOrWhiteSpace(_commandAAddress);
            bool hasB = !string.IsNullOrWhiteSpace(_commandBAddress);
            bool hasPos = !string.IsNullOrWhiteSpace(_positionAddress);

            // Cap button height so tall frames don't produce oversized buttons;
            // center the row vertically in the available area.
            int maxBtnH = px(36);   // uniform standard height (matches every standalone control)
            int btnH = Math.Min(areaH, maxBtnH);
            int rowY = areaY;                       // anchored: top gap == side gap

            // Partition the row width into: [A] gap [pos] gap [B], or [A] gap [B].
            // Auto-fit the position box to the digit count so it doesn't hog space.
            // ~ per-digit width from the readout font + padding for sign/decimal/suffix.
            // EXACT SLOTS when asked for (an axis unit copies the axis row's
            // widths); otherwise the buttons and display share the width as before.
            // EXACT SLOTS SCALE WITH THE UNIT. They are the axis row's widths at the
            // generated 248 px unit; kept fixed on a wider unit they left a strip of
            // empty frame on the right, so the same axis looked narrower than the
            // function units beside it. Same proportions, whatever the width.
            int slotBtn = 0, slotPos = 0;
            if (ButtonSlotWidth > 0)
            {
                int nominal = 2 * ButtonSlotWidth + (hasPos ? PositionSlotWidth + 2 * gap : gap);
                double k = nominal > 0 ? Math.Max(1.0, (double)areaW / nominal) : 1.0;
                slotBtn = (int)(ButtonSlotWidth * k);
                slotPos = hasPos ? areaW - 2 * slotBtn - 2 * gap : 0;
            }

            int posW = 0;
            if (hasPos && slotPos > 0) posW = slotPos;
            else if (hasPos && PositionSlotWidth > 0) posW = Math.Min(px(PositionSlotWidth), areaW - 2 * gap - px(20));
            else if (hasPos)
            {
                // ONE WIDTH FOR EVERY UNIT: the display fits "000000.0 mm" in its
                // font, whatever this unit's own digits, decimals or unit. Sized
                // to its own digits, a unit with one decimal and one with none -
                // or an axis with three - came out a little different each, and
                // so did the buttons beside them, down a page of units that should
                // line up. A reading rarely fills every digit; this fits them all.
                const string sample = "000000.0 mm";
                int textW = 0;
                try
                {
                    if (this.IsHandleCreated)
                    {
                        using (var g = this.CreateGraphics())
                            textW = (int)Math.Ceiling(g.MeasureString(sample, _numPosition.Font).Width);
                    }
                }
                catch { textW = 0; }
                if (textW <= 0)
                    textW = (int)(sample.Length * _numPosition.Font.Size * 0.62f); // fallback estimate

                int wanted = textW + px(14); // small padding
                // Never let the readout dominate: the two buttons keep a fair share.
                int maxPos = (int)(areaW * 0.42f);
                posW = Math.Min(wanted, maxPos);
                posW = Math.Min(posW, areaW - 2 * gap - px(60)); // hard floor for buttons
                if (posW < 0) posW = 0;
            }

            int totalGaps = hasPos && posW > 0 ? 2 * gap : gap;
            int buttonsW = areaW - posW - totalGaps;
            int halfW = slotBtn > 0 ? Math.Min(slotBtn, buttonsW / 2) : buttonsW / 2;
            int wA = halfW;
            int wB = ButtonSlotWidth > 0 ? halfW : buttonsW - halfW; // remainder to B unless slots are fixed

            int xA = areaX;
            int xPos = xA + wA + gap;
            int xB = (hasPos && posW > 0) ? xPos + posW + gap : xA + wA + gap;

            PlaceSlot(_btnA, _blankA, hasA, xA, rowY, wA, btnH);
            PlaceSlot(_btnB, _blankB, hasB, xB, rowY, wB, btnH);

            LayoutBadges();

            LayoutTrace($"btnH={btnH} rowY={rowY} btnBottom={rowY + btnH} unitClientH={this.ClientSize.Height} overflow={(rowY + btnH) - this.ClientSize.Height}");

            _numPosition.Visible = hasPos && posW > 0;
            if (_numPosition.Visible)
            {
                // Font scales to the row height so the digits fill the box nicely
                // (about 55% of height as point size works well for a 7-seg look).
                // The Standard style keeps the axis rows' fixed font instead.
                float fontSize = Math.Max(UiScale.Pt(9f), btnH * 0.36f);
                if (_positionStyle == UnitPositionStyle.Readout &&
                    Math.Abs(_numPosition.Font.Size - fontSize) > 0.5f)
                    _numPosition.Font = PositionFontFor(fontSize);

                // Height ALWAYS equals the button row height -> aligned with the buttons.
                _numPosition.SetBounds(xPos, rowY, posW, btnH);
                _numPosition.BringToFront();
            }
            }
            finally { _inLayout = false; }
        }

        /// <summary>
        /// The "?" ON THE TOP FRAME LINE, at its right end - the caption holds the
        /// left end of the same line, so together they read as the unit's heading.
        /// </summary>
        private void LayoutBadges()
        {
            if (_grp == null) return;
            int frameTop = (int)(_grp.Font.GetHeight() / 2f);
            _help.Location = new Point(this.ClientSize.Width - UiScale.For(this, 12) - _help.Width,
                                       Math.Max(0, frameTop - _help.Height / 2));
        }

        /// <summary>
        /// While an error bit is set, the border of what failed blinks red: the
        /// position display of an axis (its reading is what the operator watches),
        /// otherwise the button of the movement that failed - A for ErrorA, B for
        /// ErrorB. A slot with no button falls back to the display.
        /// </summary>
        private void UpdateErrorBlink()
        {
            // The ERROR BIT, not whether the "?" is shown: an axis shows its "?"
            // for its drive codes, and a warning code must not blink the border.
            bool errA = _helpA.IsErrorSet, errB = _helpB.IsErrorSet;
            bool hasPos = !string.IsNullOrWhiteSpace(_positionAddress);
            bool hasA = !string.IsNullOrWhiteSpace(_commandAAddress);
            bool hasB = !string.IsNullOrWhiteSpace(_commandBAddress);

            bool onDisplay = hasPos && (_kind == UnitKind.Axis || (errA && !hasA) || (errB && !hasB));
            _numPosition.AlertBlink = onDisplay && (errA || errB);
            _btnA.AlertBlink = !(_kind == UnitKind.Axis && hasPos) && errA && hasA;
            _btnB.AlertBlink = !(_kind == UnitKind.Axis && hasPos) && errB && hasB;
        }

        private void PlaceSlot(ButtonHMI btn, Panel blank, bool hasCommand, int x, int y, int w, int h)
        {
            btn.SetBounds(x, y, w, h);
            blank.SetBounds(x, y, w, h);
            btn.Visible = hasCommand;
            blank.Visible = !hasCommand; // reserved empty slot -> blank filler
        }
    }

    /// <summary>
    /// GroupBox with rounded corners and a caption drawn on the top border,
    /// so the CylinderUnitHMI frame actually looks rounded (the stock GroupBox
    /// draws square corners that hide any rounded region on the parent).
    /// </summary>
    internal class RoundedGroupBox : Panel
    {
        public int CornerRadius { get; set; } = 4;
        public static readonly Color NormalBorder = System.Drawing.Color.FromArgb(180, 180, 180);
        public Color BorderColor { get; set; } = NormalBorder;

        public RoundedGroupBox()
        {
            this.DoubleBuffered = true;
            // No Padding: CylinderUnitHMI.LayoutChildren positions children absolutely
            // using ClientSize, so panel padding would double-offset them.
            this.Padding = new Padding(0);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var captionSize = string.IsNullOrEmpty(this.Text)
                ? SizeF.Empty
                : e.Graphics.MeasureString(this.Text, this.Font);

            // The frame's TOP edge runs through the vertical MIDDLE of the caption,
            // so the label straddles the border line like a classic GroupBox.
            int frameTop = (int)(captionSize.Height / 2f);
            int r = CornerRadius, d = r * 2;
            var rect = new Rectangle(0, frameTop, this.Width - 1, this.Height - 1 - frameTop);

            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                // Neutral on hover: the buttons and the position display inside
                // take the blue border, not the frame around them.
                using (var pen = new Pen(BorderColor, 1))
                    e.Graphics.DrawPath(pen, path);
            }

            // Caption straddling the top border: clear the border under it, then draw.
            if (!string.IsNullOrEmpty(this.Text))
            {
                int tx = 12;
                using (var bg = new SolidBrush(this.BackColor))
                    e.Graphics.FillRectangle(bg, tx - 4, 0, captionSize.Width + 8, captionSize.Height);
                using (var br = new SolidBrush(this.ForeColor))
                    e.Graphics.DrawString(this.Text, this.Font, br, tx, 0);
            }
        }
    }

    /// <summary>
    /// THE OLD NAME. The cylinder unit was called FunctionUnitHMI until 29
    /// September, when it became CylinderUnitHMI - it is only for cylinders; a
    /// one-button function (a motor, a cooling, a horn) is a ButtonHMI. The pages
    /// shipped with the program use the new name; this keeps a page that still
    /// says FunctionUnitHMI compiling (with a warning) until it is renamed. Not
    /// in the toolbox.
    /// </summary>
    [ToolboxItem(false), EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("FunctionUnitHMI is now CylinderUnitHMI: replace the name in the page's Designer.cs.")]
    public class FunctionUnitHMI : CylinderUnitHMI { }
}
