using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// ONE PNEUMATIC POSITIONING, ONE LINE (9 October: "we use this type of
    /// pneumatic positioning set all over our machines - as an axis, just like
    /// servomotors - so we should create this HMI control to create them properly
    /// on the screens"). A cylinder with a position sensor and a 5/3 valve that the
    /// PLC drives to a target, laid out the way an axis row is (AxisSettingHMI):
    ///
    ///   name | ◀ position ▶ ⛓? ‖ target ▶| ‖ calibration ▶| ‖ tolerance ‖ direction ref
    ///
    ///   - THE NAME at the left end (TitleTextKey);
    ///   - MANUAL MOVEMENT: the two buttons and the position - a CylinderUnitHMI
    ///     inside (InRow), so its valve and feedback, its error bits, its "?" and
    ///     its helpers, the alarms the HMI raises for it, its coupling and its
    ///     simulated movement are every cylinder unit's;
    ///   - TARGET POSITION: the position to go to (TargetPosition) and the button
    ///     that starts the positioning (PositioningStartAddress, SET);
    ///   - CALIBRATION: the calibration position (CalibrationAddress) and the
    ///     button that calibrates (CalibrateAddress, SET);
    ///   - POSITIONING TOLERANCE (PositioningToleranceAddress);
    ///   - DIRECTION REF (PositioningDirectionReferenceAddress - the old pages'
    ///     "Stopping Offset").
    ///
    /// The page's rows line up as a table: the names' column is as wide as the
    /// page's widest name, the headings stand over the columns of the first row
    /// (labels tagged "PositioningHeading:&lt;column&gt;" - made when the page has
    /// none), and a row whose VisibilityConditions are not met (a positioning a
    /// machine may not have fitted) is taken out and the rows under it move up,
    /// so the page has no empty lines.
    ///
    /// EMPTY ADDRESS = NO FIELD, its column kept so the rows still line up.
    /// </summary>
    [ToolboxItem(true)]
    // The Properties window in the order of the work (see PropertyOrder).
    [TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class PneumaticPositioningHMI : UserControl, IConditionalControl
    {
        // ==================================================================
        // LAYOUT - the axis rows' widths, so the two kinds of row look alike
        // ==================================================================
        public const int RowHeight = AxisSettingHMI.RowHeight, Gap = AxisSettingHMI.Gap, GroupGap = AxisSettingHMI.GroupGap;
        public const int WSetting = AxisSettingHMI.WSetting, WCommand = AxisSettingHMI.WCommand;
        /// <summary>The narrowest and the widest the names' column is made (factor 1).</summary>
        public const int MinNameWidth = AxisSettingHMI.WAxis, MaxNameWidth = 420;
        /// <summary>The height of the headings over the columns (two lines of 8 pt).</summary>
        public const int HeadingHeight = 32;

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

        private static readonly Font NameFont = UiScale.NewFont("Segoe UI", 10F);

        private readonly Label _name = new Label();
        private readonly CylinderUnitHMI _unit = new CylinderUnitHMI();
        private readonly NumericInputHMI _target = new NumericInputHMI();
        private readonly ButtonHMI _start = new ButtonHMI();
        private readonly NumericInputHMI _calibration = new NumericInputHMI();
        private readonly ButtonHMI _calibrate = new ButtonHMI();
        private readonly NumericInputHMI _tolerance = new NumericInputHMI();
        private readonly NumericInputHMI _direction = new NumericInputHMI();

        /// <summary>Where each column of this row is, at factor 1: key -> (left, width).</summary>
        private readonly Dictionary<string, Point> _columns = new Dictionary<string, Point>();

        public PneumaticPositioningHMI()
        {
            BackColor = Color.White;
            Size = DefaultSize;

            _name.Name = "ppName";
            _name.Font = NameFont;
            _name.AutoEllipsis = true;
            _name.UseMnemonic = false;
            _name.BackColor = Color.Transparent;
            Controls.Add(_name);

            _unit.Name = "ppManual";
            _unit.InRow = true;
            _unit.PositionStyle = UnitPositionStyle.Standard;
            _unit.Arrows = ButtonArrowStyle.LeftRight;
            _unit.CommandAAction = _unit.CommandBAction = BitActionType.Momentary;
            _unit.PositionDataType = DataTypeEnum.Bin_16;
            _unit.PositionDigits = 6;
            _unit.PositionSuffix = "mm";
            _unit.PositionUnitType = UnitType.Length;
            _unit.RequiredSecurity = SecurityLevel.Operator;
            Controls.Add(_unit);

            _target.Name = "ppTarget";
            _calibration.Name = "ppCalibration";
            _tolerance.Name = "ppTolerance";
            _direction.Name = "ppDirectionRef";
            _start.Name = "ppStart";
            _calibrate.Name = "ppCalibrate";
            _target.ToolTipTextKey = "Tip_PpTarget";
            _calibration.ToolTipTextKey = "Tip_PpCalibration";
            _tolerance.ToolTipTextKey = "Tip_PpTolerance";
            _direction.ToolTipTextKey = "Tip_PpDirectionRef";
            _start.ToolTipText = "Tip_PpStart";
            _calibrate.ToolTipText = "Tip_PpCalibrate";
            foreach (var b in new[] { _start, _calibrate })
            {
                // No caption: the SET mark is drawn - the axis rows' Cal button.
                b.FallbackText = "";
                b.BitActions.Add(new Kaban.Core.Models.BitAction { Address = "", Action = BitActionType.SET });
            }

            ApplyFields();
            LayoutRow(MinNameWidth);
        }

        protected override Size DefaultSize { get { return new Size(1000, RowHeight); } }

        // ==================================================================
        // THE NAME AND THE STATION
        // ==================================================================
        private int _plcNumber = 1;

        [Category(" Device"), DefaultValue(1), Description("The controller of the positioning (1 unless the machine has several).")]
        public int PlcNumber
        {
            get { return _plcNumber; }
            set { _plcNumber = value; _unit.PlcNumber = value; ApplyFields(); }
        }

        private string _title = "";

        /// <summary>The name at the left end, a translation key (its English text): "Left Profile Recognition Front Vertical".</summary>
        [Category(" Unit"), DefaultValue(""), Description("The name at the left end of the row - a translation key, usually its English text.")]
        public string TitleTextKey
        {
            get { return _title; }
            set
            {
                _title = value ?? "";
                _unit.TitleTextKey = _title;      // the help, the alarms and the coupling name it so
                _name.Text = Tr(_title);
                if (Parent != null && IsHandleCreated) LayoutPage(Parent);
            }
        }

        /// <summary>The names' column: 0 is as wide as the page's widest name.</summary>
        [Category(" Unit"), DefaultValue(0), Description("The width of the names' column at 100 %: 0 fits the page's widest name (every row of the page uses the widest of theirs).")]
        public int NameWidth { get; set; }

        // ==================================================================
        // MANUAL MOVEMENT - the cylinder unit inside
        // ==================================================================
        [Category(" Valve & Feedback"), DefaultValue(CylinderValve.Unspecified), RefreshProperties(RefreshProperties.All),
         Description("The valve: it decides the actions (a pneumatic positioning: 3WayClosedCenter, a 5/3 closed centre). On an error the '?' opens the cylinder manual with only this valve's part.")]
        public CylinderValve ValveType { get { return _unit.ValveType; } set { _unit.ValveType = value; } }

        [Category(" Valve & Feedback"), DefaultValue(CylinderFeedback.Unspecified), RefreshProperties(RefreshProperties.All),
         Description("The feedback: it decides the lamps and the position (a pneumatic positioning: AnalogueMagnetic, or EncoderPlusReference). On an error the '?' opens the cylinder manual with only this feedback's part.")]
        public CylinderFeedback FeedbackType { get { return _unit.FeedbackType; } set { _unit.FeedbackType = value; } }

        [Category(" Valve & Feedback"), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
         Description("The movement checked against its ValveType and FeedbackType.")]
        public string TypeCheck { get { return _unit.TypeCheck; } }

        [Category(" Movement"), DefaultValue(""), Description("Command bit of the BACK (or DOWN) movement - the left button, momentary.")]
        public string CommandBackAddress { get { return _unit.CommandAAddress; } set { _unit.CommandAAddress = (value ?? "").Trim(); } }

        [Category(" Movement"), DefaultValue(""), Description("Command bit of the FORWARD (or UP) movement - the right button, momentary.")]
        public string CommandForwardAddress { get { return _unit.CommandBAddress; } set { _unit.CommandBAddress = (value ?? "").Trim(); } }

        private bool _vertical;

        [Category(" Movement"), DefaultValue(false), Description("A vertical movement: the buttons show down and up instead of back and forward.")]
        public bool VerticalMovement
        {
            get { return _vertical; }
            set { _vertical = value; _unit.Arrows = value ? ButtonArrowStyle.DownUp : ButtonArrowStyle.LeftRight; }
        }

        [Category(" Movement"), DefaultValue(""), Description("The sensor at the back (down) end - a lamp on the left button. Empty for an analogue sensor.")]
        public string BackSensorAddress { get { return _unit.Lamp1AAddress; } set { _unit.Lamp1AAddress = (value ?? "").Trim(); } }

        [Category(" Movement"), DefaultValue(""), Description("The sensor at the forward (up) end - a lamp on the right button. Empty for an analogue sensor.")]
        public string ForwardSensorAddress { get { return _unit.Lamp1BAddress; } set { _unit.Lamp1BAddress = (value ?? "").Trim(); } }

        [Category(" Movement"), DefaultValue(""), Description("Error bit set when the back (down) movement fails: the '?' turns red and the left button blinks.")]
        public string ErrorBackAddress { get { return _unit.ErrorAAddress; } set { _unit.ErrorAAddress = value; } }

        [Category(" Movement"), DefaultValue(""), Description("Error bit set when the forward (up) movement fails: the '?' turns red and the right button blinks.")]
        public string ErrorForwardAddress { get { return _unit.ErrorBAddress; } set { _unit.ErrorBAddress = value; } }

        [Category(" Movement"), DefaultValue(""),
         Editor(typeof(Kaban.UI.Design.HelpFilesEditor), typeof(System.Drawing.Design.UITypeEditor)),
         Description("Help files of the back movement (in the machine's Help folder): shown with the manual when its error is set, and behind the '?' at any time.")]
        public string HelpBackFiles { get { return _unit.HelpAFiles; } set { _unit.HelpAFiles = value; } }

        [Category(" Movement"), DefaultValue(""),
         Editor(typeof(Kaban.UI.Design.HelpFilesEditor), typeof(System.Drawing.Design.UITypeEditor)),
         Description("Help files of the forward movement (in the machine's Help folder): shown with the manual when its error is set, and behind the '?' at any time.")]
        public string HelpForwardFiles { get { return _unit.HelpBFiles; } set { _unit.HelpBFiles = value; } }

        [Category(" Position"), DefaultValue(""), Description("Word of the position read (the analogue sensor, the encoder).")]
        public string PositionAddress { get { return _unit.PositionAddress; } set { _unit.PositionAddress = (value ?? "").Trim(); } }

        [Category(" Position"), DefaultValue(DataTypeEnum.Bin_16)]
        public DataTypeEnum PositionDataType { get { return _unit.PositionDataType; } set { _unit.PositionDataType = value; } }

        [Category(" Position"), DefaultValue(1.0), Description("The position's raw value is divided by this (0.1 mm: 10).")]
        public double PositionDivisor { get { return _unit.PositionDivisor; } set { _unit.PositionDivisor = value; } }

        [Category(" Position"), DefaultValue(0)]
        public int PositionDecimalPlaces { get { return _unit.PositionDecimalPlaces; } set { _unit.PositionDecimalPlaces = value; } }

        [Category(" Position"), DefaultValue(6)]
        public int PositionDigits { get { return _unit.PositionDigits; } set { _unit.PositionDigits = value; } }

        [Category(" Position"), DefaultValue("mm")]
        public string PositionSuffix { get { return _unit.PositionSuffix; } set { _unit.PositionSuffix = value ?? ""; } }

        [Category(" Position"), DefaultValue(UnitType.Length), Description("The unit family: the position follows the unit chosen in Unit Settings.")]
        public UnitType PositionUnitType { get { return _unit.PositionUnitType; } set { _unit.PositionUnitType = value; } }

        // ==================================================================
        // TARGET POSITION, CALIBRATION, TOLERANCE, DIRECTION REF
        // ==================================================================
        private string _targetAddress = "", _startAddress = "", _calibrationAddress = "", _calibrateAddress = "",
                       _toleranceAddress = "", _directionAddress = "";

        [Category(" Target position"), DefaultValue(""), Description("Word of the target position - where the positioning goes.")]
        public string TargetPosition { get { return _targetAddress; } set { _targetAddress = (value ?? "").Trim(); ApplyFields(); } }

        [Category(" Target position"), DefaultValue(""), Description("Bit that starts the positioning to the target (SET: the PLC resets it).")]
        public string PositioningStartAddress { get { return _startAddress; } set { _startAddress = (value ?? "").Trim(); ApplyFields(); } }

        [Category(" Calibration"), DefaultValue(""), Description("Word of the calibration position - what the position reads after a calibration.")]
        public string CalibrationAddress { get { return _calibrationAddress; } set { _calibrationAddress = (value ?? "").Trim(); ApplyFields(); } }

        [Category(" Calibration"), DefaultValue(""), Description("Bit that calibrates: the position read now becomes the calibration position (SET: the PLC resets it).")]
        public string CalibrateAddress { get { return _calibrateAddress; } set { _calibrateAddress = (value ?? "").Trim(); ApplyFields(); } }

        [Category(" Positioning tolerance"), DefaultValue(""), Description("Word of the positioning tolerance - how far from the target still counts as there.")]
        public string PositioningToleranceAddress { get { return _toleranceAddress; } set { _toleranceAddress = (value ?? "").Trim(); ApplyFields(); } }

        [Category(" Direction ref"), DefaultValue(""), Description("Word of the positioning's direction reference (the old pages' 'Stopping Offset').")]
        public string PositioningDirectionReferenceAddress { get { return _directionAddress; } set { _directionAddress = (value ?? "").Trim(); ApplyFields(); } }

        private DataTypeEnum _settingType = DataTypeEnum.Bin_16;
        private double _divisor = 1;
        private int _decimals, _digits = 6;
        private string _suffix = "mm";
        private UnitType _unitType = UnitType.Length;

        [Category(" Value"), DefaultValue(DataTypeEnum.Bin_16), Description("The target, calibration, tolerance and direction ref words: one word (Bin_16) or two (Bin_32).")]
        public DataTypeEnum SettingDataType { get { return _settingType; } set { _settingType = value; ApplyFields(); } }

        [Category(" Value"), DefaultValue(1.0), Description("Their raw values are divided by this (0.01 mm: 100).")]
        public double Divisor { get { return _divisor; } set { _divisor = value; ApplyFields(); } }

        [Category(" Value"), DefaultValue(0)]
        public int DecimalPlaces { get { return _decimals; } set { _decimals = value; ApplyFields(); } }

        [Category(" Value"), DefaultValue(6)]
        public int Digits { get { return _digits; } set { _digits = value; ApplyFields(); } }

        [Category(" Value"), DefaultValue("mm"), Description("Their unit (not drawn: the headings say what they are, the position shows the unit).")]
        public string Suffix { get { return _suffix; } set { _suffix = value ?? ""; ApplyFields(); } }

        [Category(" Value"), DefaultValue(UnitType.Length), Description("The unit family: the values follow the unit chosen in Unit Settings, as the position does.")]
        public UnitType UnitType { get { return _unitType; } set { _unitType = value; ApplyFields(); } }

        // ==================================================================
        // CONDITIONS, SECURITY
        // ==================================================================
        /// <summary>
        /// The row is shown only while these are met - a positioning a machine may
        /// not have fitted (FA2010's back moulds: the Clamping Mould option). The
        /// rows under a row taken out move up.
        /// </summary>
        [Category(" Triggers"), DefaultValue(SystemCondition.None)]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("The row is shown only while these are met (an option the machine may not have); the rows under it move up.")]
        public SystemCondition VisibilityConditions { get; set; }

        private SystemCondition _enable = SystemCondition.None;

        [Category(" Triggers"), DefaultValue(SystemCondition.None)]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("The buttons and the fields can be used only while these are met.")]
        public SystemCondition EnableConditions
        {
            get { return _enable; }
            set { _enable = value; _unit.EnableConditions = value; ApplyFields(); }
        }

        private SecurityLevel _security = SecurityLevel.Operator, _settingsSecurity = SecurityLevel.Maintenance;
        private bool _log = true;

        [Category(" Security"), DefaultValue(SecurityLevel.Operator), Description("Who may move it and send it to a target.")]
        public SecurityLevel RequiredSecurity
        {
            get { return _security; }
            set { _security = value; _unit.RequiredSecurity = value; ApplyFields(); }
        }

        [Category(" Security"), DefaultValue(SecurityLevel.Maintenance), Description("Who may change the calibration, calibrate, and change the tolerance and the direction ref.")]
        public SecurityLevel SettingsSecurity { get { return _settingsSecurity; } set { _settingsSecurity = value; ApplyFields(); } }

        [Category(" Security"), DefaultValue(true), Description("Every press and every value written is in the log (who did what).")]
        public bool LogAction
        {
            get { return _log; }
            set { _log = value; _unit.LogAction = value; ApplyFields(); }
        }

        // ==================================================================
        // THE PARTS
        // ==================================================================
        /// <summary>The manual movement: the cylinder unit inside (tests, the help, the alarms).</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public CylinderUnitHMI Unit { get { return _unit; } }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NumericInputHMI TargetField { get { return _target; } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ButtonHMI StartButton { get { return _start; } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NumericInputHMI CalibrationField { get { return _calibration; } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ButtonHMI CalibrateButton { get { return _calibrate; } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NumericInputHMI ToleranceField { get { return _tolerance; } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NumericInputHMI DirectionRefField { get { return _direction; } }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Label NameLabel { get { return _name; } }

        /// <summary>The columns of this row at factor 1 (for the headings and the tests): key -> (left, width).</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDictionary<string, Point> Columns { get { return _columns; } }

        private static string Tr(string key) { return string.IsNullOrEmpty(key) ? "" : LanguageManager.GetString(key, key); }

        /// <summary>The fields and the two buttons as the properties say; a field with no address is not on the row.</summary>
        private void ApplyFields()
        {
            Field(_target, _targetAddress, _security);
            Field(_calibration, _calibrationAddress, _settingsSecurity);
            Field(_tolerance, _toleranceAddress, _settingsSecurity);
            Field(_direction, _directionAddress, _settingsSecurity);
            Command(_start, _startAddress, _security);
            Command(_calibrate, _calibrateAddress, _settingsSecurity);
        }

        private void Field(NumericInputHMI n, string address, SecurityLevel level)
        {
            n.PlcNumber = _plcNumber;
            n.Address = address;
            n.DataType = _settingType;
            n.Divisor = _divisor <= 0 ? 1 : _divisor;
            n.DecimalPlaces = _decimals;
            n.Digits = _digits;
            n.Suffix = _suffix;
            // not drawn: the heading says what it is, and the position beside it the unit
            n.ShowSuffix = false;
            n.UnitType = _unitType;
            n.TextAlign = ContentAlignment.MiddleRight;
            n.RequiredSecurity = level;
            n.EnableConditions = _enable;
            n.LogAction = _log;
            Present(n, address);
        }

        private void Command(ButtonHMI b, string address, SecurityLevel level)
        {
            b.PlcNumber = _plcNumber;
            b.BitActions[0].Address = address;
            b.RequiredSecurity = level;
            b.EnableConditions = _enable;
            b.LogAction = _log;
            Present(b, address);
        }

        /// <summary>
        /// On the row only with an address: off it, it is not read, and nothing
        /// can show it again (a field's own conditions would). Its column stays.
        /// </summary>
        private void Present(Control c, string address)
        {
            bool has = !string.IsNullOrWhiteSpace(address);
            if (has && c.Parent != this) Controls.Add(c);
            else if (!has && c.Parent == this) Controls.Remove(c);
        }

        // ==================================================================
        // LAYING THE ROW OUT
        // ==================================================================
        private void Column(string key, int x, int w) { _columns[key] = new Point(x, w); }

        private string _laidOut = "";
        private int _laidWidth = -1, _laidHeight = -1;

        /// <summary>The width of this row's name, at factor 1.</summary>
        private int MeasuredName()
        {
            var text = Tr(_title);
            if (text.Length == 0) return 0;
            int px = TextRenderer.MeasureText(text, NameFont).Width;
            return (int)Math.Ceiling(px / UiScale.Factor) + 8;
        }

        /// <summary>
        /// Lays the row out with the names' column <paramref name="nameWidth"/> wide
        /// (factor 1): the name, then each section with a group gap before it.
        /// </summary>
        public void LayoutRow(int nameWidth)
        {
            // laid out already at this width and scale: nothing to do (every row of a
            // page lays the page out as it gets its window)
            string state = nameWidth + "|" + UiScale.IsScaled(this) + "|" + _title + "|" + ViewPreferences.AxisNamesLeft;
            if (state == _laidOut && Width == _laidWidth && Height == _laidHeight) return;
            _laidOut = state;
            Func<int, int> px = v => UiScale.For(this, v);
            _columns.Clear();
            int x = 0;

            _name.Text = Tr(_title);
            // against the controls it names, as an axis row's (or left, as View Preferences may say)
            _name.TextAlign = ViewPreferences.AxisNamesLeft ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            _name.SetBounds(px(x), px(8), px(nameWidth), px(24));
            Column("Name", x, nameWidth);
            x += nameWidth + Gap + GroupGap;

            int wManual = CylinderUnitHMI.RowWidth;
            _unit.SetBounds(px(x), 0, px(wManual), px(RowHeight));
            Column("Manual", x, wManual);
            x += wManual + GroupGap;

            Column("Target", x, WSetting + Gap + WCommand);
            _target.SetBounds(px(x), px(6), px(WSetting), px(RowHeight - 12));
            _start.SetBounds(px(x + WSetting + Gap), px(4), px(WCommand), px(RowHeight - 8));
            x += WSetting + Gap + WCommand + GroupGap;

            Column("Calibration", x, WSetting + Gap + WCommand);
            _calibration.SetBounds(px(x), px(6), px(WSetting), px(RowHeight - 12));
            _calibrate.SetBounds(px(x + WSetting + Gap), px(4), px(WCommand), px(RowHeight - 8));
            x += WSetting + Gap + WCommand + GroupGap;

            Column("Tolerance", x, WSetting);
            _tolerance.SetBounds(px(x), px(6), px(WSetting), px(RowHeight - 12));
            x += WSetting + GroupGap;

            Column("DirectionRef", x, WSetting);
            _direction.SetBounds(px(x), px(6), px(WSetting), px(RowHeight - 12));
            x += WSetting;

            // As wide as its columns; in the designer it only grows, so a page is not changed by opening it.
            bool design = DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
            int w = px(x), h = px(RowHeight);
            if (Width != w && !(design && Width > w)) Width = w;
            if (Height != h) Height = h;
            _laidWidth = Width; _laidHeight = Height;
        }

        /// <summary>The column a part of this row is in ("Target", "Manual"), or null.</summary>
        public string ColumnOf(Control child)
        {
            for (var c = child; c != null; c = c.Parent)
            {
                if (c.Parent != this) continue;
                if (c == _name) return "Name";
                if (c == _unit) return "Manual";
                if (c == _target || c == _start) return "Target";
                if (c == _calibration || c == _calibrate) return "Calibration";
                if (c == _tolerance) return "Tolerance";
                if (c == _direction) return "DirectionRef";
                return null;
            }
            return null;
        }

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

        // ==================================================================
        // THE PAGE: the rows together
        // ==================================================================
        private int _rank = -1;
        private bool _conditionsMet = true;
        [ThreadStatic] private static bool _restacking;

        /// <summary>The row as the page wants it: its conditions met.</summary>
        internal bool Wanted { get { return _conditionsMet; } }

        /// <summary>A page's rows, in the designer's order (kept the first time: the rows move up and down later).</summary>
        public static List<PneumaticPositioningHMI> RowsOf(Control host)
        {
            if (host == null) return new List<PneumaticPositioningHMI>();
            var rows = host.Controls.OfType<PneumaticPositioningHMI>().ToList();
            if (rows.Any(r => r._rank < 0))
            {
                int next = rows.Where(r => r._rank >= 0).Select(r => r._rank + 1).DefaultIfEmpty(0).Max();
                foreach (var r in rows.Where(r => r._rank < 0).OrderBy(r => r.Top).ThenBy(r => r.Left)) r._rank = next++;
            }
            return rows.OrderBy(r => r._rank).ToList();
        }

        /// <summary>
        /// THE PAGE AS ONE TABLE: every row with the same names' column (the widest
        /// name of the page, or the first row's NameWidth when set), the headings
        /// over the columns, the rows shown moved up.
        /// </summary>
        public static void LayoutPage(Control host)
        {
            var rows = RowsOf(host);
            if (rows.Count == 0) return;
            int fixedWidth = rows[0].NameWidth;
            int w = fixedWidth > 0 ? fixedWidth
                  : Math.Min(MaxNameWidth, Math.Max(MinNameWidth, rows.Max(r => r.MeasuredName())));
            foreach (var r in rows) r.LayoutRow(w);
            rows[0].AlignHeadings();
            Restack(host);
        }

        /// <summary>
        /// THE HEADINGS OVER THE COLUMNS: the labels of the page tagged
        /// "PositioningHeading:&lt;column&gt;" are put over their columns and
        /// translated; at run time, a page without them gets them, in the axis
        /// pages' style, above its first row.
        /// </summary>
        public void AlignHeadings()
        {
            var host = Parent;
            if (host == null) return;
            var rows = RowsOf(host);
            if (rows.Count == 0) return;
            int left = rows.Min(r => r.Left);
            int top = rows.Min(r => r.Top);
            var byKey = new Dictionary<string, Label>();
            foreach (var l in host.Controls.OfType<Label>())
            {
                var tag = l.Tag as string;
                if (tag != null && tag.StartsWith(HeadingTag)) byKey[tag.Substring(HeadingTag.Length)] = l;
            }
            bool design = DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
            foreach (var h in Headings)
            {
                Point col;
                if (!_columns.TryGetValue(h[0], out col)) continue;
                Label l;
                if (!byKey.TryGetValue(h[0], out l))
                {
                    // Made at run time only: one made in the designer would not be in its file.
                    if (design) continue;
                    l = NewHeading(h[0]);
                    // at factor 1: a page already magnified magnifies what is added to it
                    int topLogical = UiScale.IsScaled(this) ? UiScale.Logical(top) : top;
                    l.SetBounds(0, Math.Max(0, topLogical - HeadingHeight - 4), col.Y, HeadingHeight);
                    host.Controls.Add(l);
                }
                l.Left = left + UiScale.For(this, col.X);
                l.Width = UiScale.For(this, col.Y);
                l.Text = LanguageManager.GetString(h[1], h[2]);
            }
        }

        /// <summary>A heading label in the axis pages' style.</summary>
        public static Label NewHeading(string key)
        {
            return new Label
            {
                Name = "lblPpHead_" + key,
                Tag = HeadingTag + key,
                AutoSize = false,
                TextAlign = ContentAlignment.BottomCenter,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(110, 116, 124),
                BackColor = Color.Transparent,
                UseMnemonic = false
            };
        }

        /// <summary>
        /// THE ROWS SHOWN MOVED UP TOGETHER: the places the designer gave the rows
        /// are kept, and taken in the designer's order by the rows shown first,
        /// then by the hidden ones - no empty line where a row was taken out.
        /// What stands below the rows follows the last row shown.
        /// </summary>
        public static void Restack(Control host)
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
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> _followers =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();

        private static void FollowRows(Control host, List<PneumaticPositioningHMI> rows, List<Point> places, int shown)
        {
            int h = rows.Max(r => r.Height);
            int slotsBottom = places[places.Count - 1].Y + h;
            int shownBottom = shown > 0 ? places[shown - 1].Y + h : places[0].Y;
            foreach (Control c in host.Controls)
            {
                if (c is PneumaticPositioningHMI) continue;
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

        // ==================================================================
        // LIFE
        // ==================================================================
        private bool _hooked;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_hooked)
            {
                _hooked = true;
                LanguageManager.LanguageChanged += OnLanguageChanged;
                ViewPreferences.Changed += OnViewPreferencesChanged;
            }
            if (Parent != null) LayoutPage(Parent);
        }

        /// <summary>The names, their column and the headings in the new language - laid out once, by the first row.</summary>
        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Parent == null) return;
            _name.Text = Tr(_title);
            var rows = RowsOf(Parent);
            if (rows.Count > 0 && rows[0] == this) LayoutPage(Parent);
        }

        private void OnViewPreferencesChanged(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnViewPreferencesChanged), sender, e); } catch { } return; }
            _name.TextAlign = ViewPreferences.AxisNamesLeft ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_hooked) { LanguageManager.LanguageChanged -= OnLanguageChanged; ViewPreferences.Changed -= OnViewPreferencesChanged; }
                // the fields with no address are not on the row: disposed here
                foreach (var c in new Control[] { _target, _start, _calibration, _calibrate, _tolerance, _direction })
                    if (c.Parent != this && !c.IsDisposed) c.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// The scanner drives the parts (they are ordinary HMI controls); the row
        /// itself follows its visibility conditions, and the rows move up or down.
        /// </summary>
        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            bool met = VisibilityConditions == SystemCondition.None || checkGlobal(VisibilityConditions);
            bool changed = met != _conditionsMet;
            _conditionsMet = met;
            if (Visible != met) Visible = met;
            if (changed && Parent != null) Restack(Parent);
        }
    }
}
