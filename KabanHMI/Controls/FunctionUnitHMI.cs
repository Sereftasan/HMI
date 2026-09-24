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
    /// numbers are the manual's variant numbers and must not be changed.
    /// </summary>
    public enum CylinderValve
    {
        Unspecified = 0,
        DoubleSolenoid_5_2 = 1,        // (1) double solenoid 5/2, impulse / memory
        SingleSolenoid_5_2 = 2,        // (2) single solenoid 5/2, spring return
        DoubleSolenoid_5_3 = 3         // (3) double solenoid 5/3, centre position
    }

    /// <summary>The cylinder's feedback - detail 1.7 of the cylinder manual.</summary>
    public enum CylinderFeedback
    {
        Unspecified = 0,
        SingleMagnetic = 1,            // single magnetic sensor, short stroke
        TwoEndMagnetic = 2,            // magnetic sensor at each end
        SingleInductive = 3,           // single inductive sensor, short stroke
        AnalogueMagnetic = 4,          // 4-20 mA position
        MagneticPlusPressureSwitch = 5,// back-end sensor + pressure switch (clamping)
        EncoderPlusReference = 6       // incremental encoder + reference sensor
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
    public class FunctionUnitHMI : UserControl, ILocalizable
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
        [TypeConverter(typeof(Kaban.UI.Design.FunctionUnitChildConverter))]
        public ButtonHMI ButtonA { get { return _btnA; } }

        /// <summary>The RIGHT button. See ButtonA.</summary>
        [Category(" Buttons")]
        [Description("Right button - full ButtonHMI property set.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [TypeConverter(typeof(Kaban.UI.Design.FunctionUnitChildConverter))]
        public ButtonHMI ButtonB { get { return _btnB; } }

        /// <summary>The encoder position display. Exposed for full styling access.</summary>
        [Category(" Buttons")]
        [Description("Position readout - full NumericDisplayHMI property set.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [TypeConverter(typeof(Kaban.UI.Design.FunctionUnitChildConverter))]
        public NumericDisplayHMI PositionDisplay { get { return _numPosition; } }

        public FunctionUnitHMI()
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
        private static readonly Font CaptionFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        /// <summary>The axis-row position font (see AxisSettingHMI), for UnitPositionStyle.Standard.</summary>
        private static readonly Font StandardPositionFont = new Font("Segoe UI", 12F, FontStyle.Bold);

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
        [Category(" Triggers")]
        public SystemCondition VisibilityConditions { get { return _visibilityConditions; } set { _visibilityConditions = value; ApplySettings(); } }

        private SystemCondition _enableConditions = SystemCondition.None;
        [Category(" Triggers")]
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

        [Category("Help"), DefaultValue(""),
         Description("Error bit set when the A (left button) movement fails. Its '?' shows on the top frame line, and button A's border blinks red.")]
        public string ErrorAAddress { get { return _errorAAddress; } set { _errorAAddress = (value ?? "").Trim(); ApplySettings(); } }

        [Category("Help"), DefaultValue(""),
         Description("Error bit set when the B (right button) movement fails. Its '?' shows on the top frame line, and button B's border blinks red.")]
        public string ErrorBAddress { get { return _errorBAddress; } set { _errorBAddress = (value ?? "").Trim(); ApplySettings(); } }

        [Category("Help"), DefaultValue(UnitKind.Cylinder),
         Description("Cylinder: errors open the cylinder manual for its Valve and Feedback. " +
                     "Axis: errors open the servo drive manual, and Valve and Feedback are not used - " +
                     "put the reference sensor on ONE button's lamp (Lamp1A or Lamp1B).")]
        public UnitKind Kind { get { return _kind; } set { _kind = value; UpdateErrorBlink(); } }
        private UnitKind _kind = UnitKind.Cylinder;

        /// <summary>
        /// This unit's own help - photos of the real mechanism, its drawing -
        /// available at any time through the "i" on the top frame line, and listed
        /// with the manual when one of its alarms is active. File names in the
        /// machine's Help folder, ";"-separated.
        /// </summary>
        [Category("Help"), DefaultValue(""),
         Editor(typeof(Kaban.UI.Design.HelpFilesEditor), typeof(System.Drawing.Design.UITypeEditor)),
         Description("Help files about this unit (in the machine's Help folder). Shown as an 'i' on the top frame line.")]
        public string HelpFiles { get { return _helpFiles; } set { _helpFiles = (value ?? "").Trim(); ApplySettings(); } }
        private string _helpFiles = "";

        /// <summary>The unit's help files, split, as the names were entered.</summary>
        public List<string> HelpFileNames()
        {
            return _helpFiles.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(f => f.Trim()).Where(f => f.Length > 0).ToList();
        }

        /// <summary>The unit's help files that are actually present.</summary>
        public List<TroubleshootingCatalog.HelpDocument> UnitHelpDocuments()
        {
            return HelpFileNames()
                .Select(f => new TroubleshootingCatalog.HelpDocument
                {
                    FileName = f,
                    Title = System.IO.Path.GetFileNameWithoutExtension(f)
                })
                .Where(d => d.Exists).ToList();
        }

        private readonly UnitHelpBadge _help = new UnitHelpBadge();

        /// <summary>Exact button width (0 = share the width). An axis unit sets the axis row's.</summary>
        [Browsable(false)]
        public int ButtonSlotWidth { get; set; }

        /// <summary>Exact position display width (0 = fit the number).</summary>
        [Browsable(false)]
        public int PositionSlotWidth { get; set; }

        /// <summary>
        /// The valve fitted to this cylinder. The "?" opens the cylinder manual with
        /// only this valve's part of detail 1.4 - see TroubleshootingCatalog.Tailor.
        /// </summary>
        [Category("Help"), DefaultValue(CylinderValve.Unspecified),
         Description("Cylinder units: the valve fitted. The '?' opens the cylinder troubleshooting manual with only this valve's part of detail 1.4.")]
        public CylinderValve ValveType { get { return _valve; } set { _valve = value; ApplySettings(); } }

        /// <summary>The feedback fitted to this cylinder: only its part of detail 1.7 is shown.</summary>
        [Category("Help"), DefaultValue(CylinderFeedback.Unspecified),
         Description("Cylinder units: the feedback fitted. The '?' opens the cylinder troubleshooting manual with only this feedback's part of detail 1.7.")]
        public CylinderFeedback FeedbackType { get { return _feedback; } set { _feedback = value; ApplySettings(); } }

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
            UpdateHelpBadge();
        }

        private string[] _codeWords = { "", "", "" };

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
                        HelpFiles = HelpFileNames(),
                        ServoAlarm = _kind == UnitKind.Axis ? _codeWords[0] : "",
                        AxisAlarm = _kind == UnitKind.Axis ? _codeWords[1] : "",
                        AxisWarning = _kind == UnitKind.Axis ? _codeWords[2] : "",
                        // Which side's error bit this is: the alarm list's help names
                        // that movement's devices, like the unit's own "?".
                        Movement = _kind == UnitKind.Axis ? null : MovementFor(address == _errorAAddress ? "A" : "B")
                    };
        }

        /// <summary>
        /// The devices of one side's movement, for the top of the tailored manual:
        /// A is the left button's (ErrorA), B the right button's (ErrorB).
        /// </summary>
        public TroubleshootingCatalog.MovementInfo MovementFor(string side)
        {
            bool a = side == "A";
            Func<string, string, TroubleshootingCatalog.MovementInfo.Device> dev = (address, name) =>
                string.IsNullOrWhiteSpace(address) ? null
                    : new TroubleshootingCatalog.MovementInfo.Device { Address = Abs(address), Name = Tr(name) };
            var mv = new TroubleshootingCatalog.MovementInfo
            {
                Station = _plcNumber,
                Unit = string.IsNullOrEmpty(_titleText) ? Name : Tr(_titleText),
                Side = a ? "A" : "B",
                Name = Tr(a ? _buttonAText : _buttonBText),
                Command = dev(a ? _commandAAddress : _commandBAddress, a ? _buttonATip : _buttonBTip),
                Output = dev(a ? _outputAAddress : _outputBAddress, ""),
                OtherCommand = dev(a ? _commandBAddress : _commandAAddress, a ? _buttonBTip : _buttonATip),
                OtherOutput = dev(a ? _outputBAddress : _outputAAddress, "")
            };
            foreach (var d in a ? new[] { dev(_lamp1AAddress, _lamp1ATip), dev(_lamp2AAddress, _lamp2ATip) }
                                : new[] { dev(_lamp1BAddress, _lamp1BTip), dev(_lamp2BAddress, _lamp2BTip) })
                if (d != null) mv.Feedback.Add(d);
            foreach (var d in a ? new[] { dev(_lamp1BAddress, _lamp1BTip), dev(_lamp2BAddress, _lamp2BTip) }
                                : new[] { dev(_lamp1AAddress, _lamp1ATip), dev(_lamp2AAddress, _lamp2ATip) })
                if (d != null) mv.OppositeFeedback.Add(d);
            return mv;
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
                        (mv == null ? "" : " - " + (string.IsNullOrEmpty(mv.Name) ? mv.Side : mv.Name)),
                Valve = TroubleshootingCatalog.CanonicalValve((int)_valve),
                Feedback = TroubleshootingCatalog.CanonicalFeedback((int)_feedback),
                Movement = mv
            };
        }

        /// <summary>True when the unit's cylinder manual is worth offering before anything has failed.</summary>
        private bool OffersManual
        {
            get
            {
                return _kind == UnitKind.Cylinder &&
                       (_valve != CylinderValve.Unspecified || _feedback != CylinderFeedback.Unspecified);
            }
        }

        /// <summary>
        /// Everything behind the "?" right now. While an error is set: the help for
        /// what failed first - the manual tailored to this cylinder and to the
        /// movement whose error bit is set (an axis: the entry of each drive code
        /// it shows) - then the alarm's own files. Otherwise the cylinder manual
        /// for this valve and feedback. The unit's own files always come last.
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
                    var d = CylinderDocument(side);
                    if (d != null) docs.Add(d);
                    // The alarm's own helper files, as the alarm list shows them.
                    var bit = side == "A" ? _errorAAddress : _errorBAddress;
                    var def = AlarmManager.FindByStationAndAddress(_plcNumber, bit);
                    if (def != null && def.HelperKeys != null)
                        foreach (var entry in def.HelperKeys.Where(f => !string.IsNullOrWhiteSpace(f)))
                            foreach (var n in entry.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                                docs.Add(new TroubleshootingCatalog.HelpDocument { FileName = n.Trim(), Title = System.IO.Path.GetFileNameWithoutExtension(n.Trim()) });
                }
                if (!errA && !errB && (OffersManual || _errorAAddress.Length > 0 || _errorBAddress.Length > 0))
                {
                    var d = CylinderDocument(null);
                    if (d != null) docs.Add(d);
                }
            }
            docs.AddRange(UnitHelpDocuments());

            // One entry per file - except the tailored and coded ones, which differ by what they show.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return docs.Where(d => d.Exists && (d.Movement != null || d.Valve != 0 || d.Feedback != 0 ||
                                                !string.IsNullOrEmpty(d.Code) || seen.Add(d.FileName))).ToList();
        }

        /// <summary>
        /// The "?"'s look: red and blinking while an error bit is set (amber when an
        /// axis shows only warnings), blue while there is help to offer - the
        /// unit's own files, or the manual for its valve and feedback - and hidden
        /// otherwise: a mark that opens nothing teaches the operator to ignore it.
        /// </summary>
        private void UpdateHelpBadge()
        {
            bool errA = _helpA.IsActive, errB = _helpB.IsActive;
            UnitHelpBadge.BadgeState state;
            if (errA || errB)
                state = (!errA || _helpA.IsWarningOnly) && (!errB || _helpB.IsWarningOnly)
                        ? UnitHelpBadge.BadgeState.Warning : UnitHelpBadge.BadgeState.Error;
            else if (DesignMode)
                state = _helpFiles.Length > 0 || OffersManual || _errorAAddress.Length > 0 || _errorBAddress.Length > 0
                        ? UnitHelpBadge.BadgeState.Info : UnitHelpBadge.BadgeState.Hidden;
            else
                state = UnitHelpDocuments().Count > 0 || (OffersManual && CylinderManualExists())
                        ? UnitHelpBadge.BadgeState.Info : UnitHelpBadge.BadgeState.Hidden;
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
                UiStyle.Tips().SetToolTip(_help, tip.Trim());
            }
            LayoutBadges();
        }

        private static bool CylinderManualExists()
        {
            string file;
            return TroubleshootingCatalog.DocumentForType.TryGetValue(TroubleshootingCatalog.ErrorType.CylinderMovement, out file) &&
                   new TroubleshootingCatalog.HelpDocument { FileName = file }.Exists;
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
            // Not while the page is being built: every property the designer code
            // sets runs this, and the "?" looks its files up on disk. Done once the
            // unit has a window (OnHandleCreated), and on every change after that.
            if (IsHandleCreated || DesignMode) UpdateHelpBadge();
        }

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

            _toolTip.SetToolTip(_grp, Tr(_titleTip));
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
            int gap = 6;

            // ROOT-CAUSE FIX: _grp is Dock.Fill but during the initial
            // OnHandleCreated/OnParentChanged pass it has NOT been resized to the
            // unit yet (it still reports its default 200x100). Using its ClientSize
            // then made the buttons overflow the unit. Compute from the UNIT's own
            // client area instead - _grp fills it exactly, so this is correct and
            // available immediately.
            int leftPad = 8;
            int rightPad = 8;
            // SYMMETRIC spacing: the caption is DRAWN inside the panel (no reserved
            // band), so the visual gap under the caption and above the bottom border
            // must both equal the side gap (6). topPad therefore clears the caption
            // height plus one side-gap; the button row is anchored, not centred.
            int capH = (int)Math.Ceiling(_grp.Font.GetHeight());
            int topPad = capH + 6;
            int botPad = 6;

            int areaX = leftPad;
            int areaY = topPad;
            // The full width, whatever error bits the unit has (see ApplySettings).
            int areaW = this.ClientSize.Width - leftPad - rightPad;
            int areaH = this.ClientSize.Height - topPad - botPad;

            LayoutTrace($"unit={this.Width}x{this.Height} unitClient={this.ClientSize.Width}x{this.ClientSize.Height} grpClient={_grp.ClientSize.Width}x{_grp.ClientSize.Height} area={areaW}x{areaH}");

            if (areaW < 30 || areaH < 8)
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
            int maxBtnH = 36;   // uniform standard height (matches every standalone control)
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
            else if (hasPos && PositionSlotWidth > 0) posW = Math.Min(PositionSlotWidth, areaW - 2 * gap - 20);
            else if (hasPos)
            {
                // Build a representative widest string: all digits + optional decimal
                // point + decimals + suffix, so the measured width covers everything
                // that will actually be shown (fixes the clipped "12.35 mm").
                int intDigits = Math.Max(1, _positionDigits);
                string sample = new string('0', intDigits);
                if (_positionDecimalPlaces > 0)
                    sample += "." + new string('0', _positionDecimalPlaces);
                if (!string.IsNullOrEmpty(_positionSuffix))
                    sample += " " + _positionSuffix; // matches the " suffix" the display now appends

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

                int wanted = textW + 14; // small padding
                // Never let the readout dominate: cap it so the two button slots
                // keep a fair share of the row (buttons were getting squeezed).
                int maxPos = (int)(areaW * 0.42f);
                posW = Math.Min(wanted, maxPos);
                posW = Math.Min(posW, areaW - 2 * gap - 60); // hard floor for buttons
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
                float fontSize = Math.Max(9f, btnH * 0.36f);
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
            _help.Location = new Point(this.ClientSize.Width - 12 - UnitHelpBadge.BadgeSize,
                                       Math.Max(0, frameTop - UnitHelpBadge.BadgeSize / 2));
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
    /// so the FunctionUnitHMI frame actually looks rounded (the stock GroupBox
    /// draws square corners that hide any rounded region on the parent).
    /// </summary>
    internal class RoundedGroupBox : Panel
    {
        public int CornerRadius { get; set; } = 4;
        public Color BorderColor { get; set; } = System.Drawing.Color.FromArgb(180, 180, 180);

        public RoundedGroupBox()
        {
            this.DoubleBuffered = true;
            // No Padding: FunctionUnitHMI.LayoutChildren positions children absolutely
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
}

