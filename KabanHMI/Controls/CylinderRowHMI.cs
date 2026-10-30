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
    /// A CYLINDER AS ONE ROW OF A TABLE, laid out the way an axis row is
    /// (AxisSettingHMI): its name at the left end, its MANUAL MOVEMENT (the two
    /// buttons and the position - a CylinderUnitHMI inside, drawn as a row's,
    /// InRow), then its own sections, each a value field and maybe a SET button,
    /// under headings over the columns.
    ///
    ///   name | ◀ position ▶ ⛓? ‖ section ‖ section ‖ ...
    ///
    /// The kinds of row (9 October): PneumaticPositioningHMI (a cylinder the PLC
    /// drives to a target: target, calibration, tolerance, direction ref) and
    /// CylinderEncoderSettingHMI (a cylinder with an encoder: reference,
    /// measuring tolerance, minimum limit, unclamping interval). A kind declares
    /// its sections (AddSection) and its properties over them; everything else is
    /// here:
    ///
    ///   - the manual movement is a cylinder unit's in every way - its valve and
    ///     feedback, error bits, "?" and helpers, the alarms the HMI raises for it,
    ///     coupling, simulated movement;
    ///   - the page's rows of a kind line up as a table: one names' column, as
    ///     wide as the page's widest name; the headings over the columns of the
    ///     first row (labels tagged with the kind's heading tag and the column -
    ///     made at run time when the page has none);
    ///   - a row whose VisibilityConditions are not met is taken out and the rows
    ///     under it move up into its place, so the page has no empty lines;
    ///   - an empty address: no field, its column kept so the rows still line up;
    ///   - once shown, the page is a table as an axis page is (AxisPageFrame): the
    ///     headings stay at the top while the rows scroll up and down, and the
    ///     names stay at the left while they scroll sideways - at a larger screen
    ///     scale a row is wider than the screen (9 October, "150% zoom: should I
    ///     do the same here? Yes").
    /// </summary>
    [ToolboxItem(false)]
    public class CylinderRowHMI : UserControl, IConditionalControl
    {
        // ==================================================================
        // LAYOUT - the axis rows' widths, so the kinds of row look alike
        // ==================================================================
        public const int RowHeight = AxisSettingHMI.RowHeight, Gap = AxisSettingHMI.Gap, GroupGap = AxisSettingHMI.GroupGap;
        public const int WSetting = AxisSettingHMI.WSetting, WCommand = AxisSettingHMI.WCommand;
        /// <summary>The narrowest and the widest the names' column is made (factor 1).</summary>
        public const int MinNameWidth = AxisSettingHMI.WAxis, MaxNameWidth = 420;
        /// <summary>The height of the headings over the columns (two lines of 8 pt).</summary>
        public const int HeadingHeight = 32;

        /// <summary>The manual movement's column and its heading.</summary>
        public const string ManualKey = "Manual";

        private static readonly Font NameFont = UiScale.NewFont("Segoe UI", 10F);

        /// <summary>One section of a row: its heading, its value field, maybe its SET button.</summary>
        protected sealed class Section
        {
            public string Key, TitleKey, Title;
            public NumericInputHMI Field;
            public ButtonHMI Command;
            /// <summary>The operator's (moving it, sending it to a target); else the settings' level.</summary>
            public bool Operator;
            public string FieldAddress = "", CommandAddress = "";
        }

        private readonly List<Section> _sections = new List<Section>();
        private readonly string _headingTag, _prefix;
        private readonly Label _name = new Label();
        private readonly CylinderUnitHMI _unit = new CylinderUnitHMI();

        /// <summary>Where each column of this row is, at factor 1: key -> (left, width).</summary>
        private readonly Dictionary<string, Point> _columns = new Dictionary<string, Point>();

        /// <param name="headingTag">The tag of the kind's heading labels, before the column's key ("PositioningHeading:").</param>
        /// <param name="prefix">The parts' names begin with it ("pp": ppName, ppManual, ppTarget ...).</param>
        protected CylinderRowHMI(string headingTag, string prefix)
        {
            _headingTag = headingTag;
            _prefix = prefix;
            BackColor = Color.White;
            Size = DefaultSize;

            _name.Name = prefix + "Name";
            _name.Font = NameFont;
            _name.AutoEllipsis = true;
            _name.UseMnemonic = false;
            _name.BackColor = Color.Transparent;
            Controls.Add(_name);

            _unit.Name = prefix + "Manual";
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
        }

        protected override Size DefaultSize { get { return new Size(1000, RowHeight); } }

        /// <summary>
        /// A section of the kind, in the row's order: its column's key, its heading
        /// (translation key and English), its field's name and tooltip key, and -
        /// when it has one - its SET button's name and tooltip key.
        /// </summary>
        protected void AddSection(string key, string titleKey, string title, string fieldName, string fieldTipKey,
                                  string commandName = null, string commandTipKey = null, bool operatorLevel = false)
        {
            var s = new Section { Key = key, TitleKey = titleKey, Title = title, Operator = operatorLevel };
            s.Field = new NumericInputHMI { Name = _prefix + fieldName, ToolTipTextKey = fieldTipKey ?? "" };
            if (commandName != null)
            {
                s.Command = new ButtonHMI { Name = _prefix + commandName, ToolTipText = commandTipKey ?? "" };
                // No caption: the SET mark is drawn - the axis rows' Cal button.
                s.Command.FallbackText = "";
                s.Command.BitActions.Add(new Kaban.Core.Models.BitAction { Address = "", Action = BitActionType.SET });
            }
            _sections.Add(s);
        }

        /// <summary>Called by a kind once its sections are added: the fields set up, the row laid out.</summary>
        protected void SectionsDone()
        {
            ApplyFields();
            LayoutRow(MinNameWidth);
        }

        private Section Find(string key) { return _sections.First(s => s.Key == key); }

        protected string FieldAddress(string key) { return Find(key).FieldAddress; }
        protected void SetFieldAddress(string key, string address) { Find(key).FieldAddress = (address ?? "").Trim(); ApplyFields(); }
        protected string CommandAddress(string key) { return Find(key).CommandAddress; }
        protected void SetCommandAddress(string key, string address) { Find(key).CommandAddress = (address ?? "").Trim(); ApplyFields(); }

        /// <summary>A section's value field (on the row only with an address).</summary>
        public NumericInputHMI FieldOf(string key) { return Find(key).Field; }

        /// <summary>A section's SET button (on the row only with an address), or null.</summary>
        public ButtonHMI CommandOf(string key) { return Find(key).Command; }

        /// <summary>The keys of the columns with a heading, in the row's order (the manual movement first).</summary>
        [Browsable(false)]
        public IEnumerable<string> SectionKeys { get { return new[] { ManualKey }.Concat(_sections.Select(s => s.Key)); } }

        /// <summary>The tag of this kind's heading labels, before the column's key.</summary>
        [Browsable(false)]
        public string HeadingTagPrefix { get { return _headingTag; } }

        /// <summary>A column's heading translation key ("Col_PpTarget"), or null.</summary>
        public string SectionTitleKey(string columnKey)
        {
            if (columnKey == ManualKey) return "Col_PpManual";
            var s = _sections.FirstOrDefault(x => x.Key == columnKey);
            return s == null ? null : s.TitleKey;
        }

        /// <summary>A column's heading, translated ("Target position").</summary>
        public string SectionTitle(string columnKey)
        {
            if (columnKey == ManualKey) return LanguageManager.GetString("Col_PpManual", "Manual movement");
            var s = _sections.FirstOrDefault(x => x.Key == columnKey);
            return s == null ? (columnKey ?? "") : LanguageManager.GetString(s.TitleKey, s.Title);
        }

        // ==================================================================
        // THE NAME AND THE STATION
        // ==================================================================
        private int _plcNumber = 1;

        [Category(" Device"), DefaultValue(1), Description("The controller of the cylinder (1 unless the machine has several).")]
        public int PlcNumber
        {
            get { return _plcNumber; }
            set { _plcNumber = value; _unit.PlcNumber = value; ApplyFields(); }
        }

        private string _title = "";

        /// <summary>The name at the left end, a translation key (its English text).</summary>
        [Category(" Unit"), DefaultValue(""), Description("The name at the left end of the row - a translation key, usually its English text.")]
        public string TitleTextKey
        {
            get { return _title; }
            set
            {
                _title = value ?? "";
                _unit.TitleTextKey = _title;      // the help, the alarms and the coupling name it so
                _name.Text = Tr(_title);
                if (Parent != null && IsHandleCreated) LayoutPage(Parent, GetType());
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
         Description("The feedback: it decides the lamps and the position (AnalogueMagnetic, EncoderPlusReference ...). On an error the '?' opens the cylinder manual with only this feedback's part.")]
        public CylinderFeedback FeedbackType { get { return _unit.FeedbackType; } set { _unit.FeedbackType = value; } }

        [Category(" Valve & Feedback"), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
         Description("The movement checked against its ValveType and FeedbackType.")]
        public string TypeCheck { get { return _unit.TypeCheck; } }

        [Category(" Movement"), DefaultValue(""), Description("Command bit of the BACK (or DOWN, or RELEASE) movement - the left button, momentary.")]
        public string CommandBackAddress { get { return _unit.CommandAAddress; } set { _unit.CommandAAddress = (value ?? "").Trim(); } }

        [Category(" Movement"), DefaultValue(""), Description("Command bit of the FORWARD (or UP, or CLAMP) movement - the right button, momentary.")]
        public string CommandForwardAddress { get { return _unit.CommandBAddress; } set { _unit.CommandBAddress = (value ?? "").Trim(); } }

        private bool _vertical;

        [Category(" Movement"), DefaultValue(false), Description("A vertical movement: the buttons show down and up instead of back and forward.")]
        public bool VerticalMovement
        {
            get { return _vertical; }
            set { _vertical = value; _unit.Arrows = value ? ButtonArrowStyle.DownUp : ButtonArrowStyle.LeftRight; }
        }

        [Category(" Movement"), DefaultValue(""), Description("The sensor at the back (down) end, or the reference sensor - a lamp on the left button.")]
        public string BackSensorAddress { get { return _unit.Lamp1AAddress; } set { _unit.Lamp1AAddress = (value ?? "").Trim(); } }

        [Category(" Movement"), DefaultValue(""), Description("The sensor at the forward (up) end, or the pressure switch - a lamp on the right button.")]
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
        // THE SECTIONS' VALUES
        // ==================================================================
        private DataTypeEnum _settingType = DataTypeEnum.Bin_16;
        private double _divisor = 1;
        private int _decimals, _digits = 6;
        private string _suffix = "mm";
        private UnitType _unitType = UnitType.Length;

        [Category(" Value"), DefaultValue(DataTypeEnum.Bin_16), Description("The sections' words: one word (Bin_16) or two (Bin_32).")]
        public DataTypeEnum SettingDataType { get { return _settingType; } set { _settingType = value; ApplyFields(); } }

        [Category(" Value"), DefaultValue(1.0), Description("The sections' raw values are divided by this (0.1 mm: 10, 0.01 mm: 100).")]
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
        /// The row is shown only while these are met - a cylinder a machine may not
        /// have fitted (an option). The rows under a row taken out move up.
        /// </summary>
        [Category(" Triggers"), DefaultValue(SystemCondition.None)]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("The row is shown only while these are met (an option the machine may not have); the rows under it move up.")]
        public SystemCondition VisibilityConditions { get; set; }

        /// <summary>
        /// The conditions the row is shown under: its VisibilityConditions and what a kind of row
        /// adds (an encoder row: the machine has its encoder - CylinderEncoderSettingHMI). The
        /// scanner reads their devices.
        /// </summary>
        protected virtual SystemCondition ShownConditions { get { return VisibilityConditions; } }

        SystemCondition IConditionalControl.VisibilityConditions { get { return ShownConditions; } }

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

        [Category(" Security"), DefaultValue(SecurityLevel.Operator), Description("Who may move it (and send it to a target).")]
        public SecurityLevel RequiredSecurity
        {
            get { return _security; }
            set { _security = value; _unit.RequiredSecurity = value; ApplyFields(); }
        }

        [Category(" Security"), DefaultValue(SecurityLevel.Maintenance), Description("Who may change its settings (and calibrate it).")]
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
        public Label NameLabel { get { return _name; } }

        /// <summary>The columns of this row at factor 1 (for the headings and the tests): key -> (left, width).</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDictionary<string, Point> Columns { get { return _columns; } }

        protected static string Tr(string key) { return string.IsNullOrEmpty(key) ? "" : LanguageManager.GetString(key, key); }

        /// <summary>The fields and the buttons as the properties say; one with no address is not on the row.</summary>
        private void ApplyFields()
        {
            foreach (var s in _sections)
            {
                var level = s.Operator ? _security : _settingsSecurity;
                var n = s.Field;
                n.PlcNumber = _plcNumber;
                n.Address = s.FieldAddress;
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
                Present(n, s.FieldAddress);
                var b = s.Command;
                if (b == null) continue;
                b.PlcNumber = _plcNumber;
                b.BitActions[0].Address = s.CommandAddress;
                b.RequiredSecurity = level;
                b.EnableConditions = _enable;
                b.LogAction = _log;
                Present(b, s.CommandAddress);
            }
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
        /// (factor 1): the name, the manual movement, then each section with a group
        /// gap before it.
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
            Column(ManualKey, x, wManual);
            x += wManual;

            foreach (var s in _sections)
            {
                x += GroupGap;
                int w = WSetting + (s.Command != null ? Gap + WCommand : 0);
                Column(s.Key, x, w);
                s.Field.SetBounds(px(x), px(6), px(WSetting), px(RowHeight - 12));
                if (s.Command != null) s.Command.SetBounds(px(x + WSetting + Gap), px(4), px(WCommand), px(RowHeight - 8));
                x += w;
            }

            // As wide as its columns; in the designer it only grows, so a page is not changed by opening it.
            bool design = DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
            int width = px(x), h = px(RowHeight);
            if (Width != width && !(design && Width > width)) Width = width;
            if (Height != h) Height = h;
            _laidWidth = Width; _laidHeight = Height;
        }

        /// <summary>The column a part of this row is in ("Target", "Manual", "Name"), or null.</summary>
        public string ColumnOf(Control child)
        {
            for (var c = child; c != null; c = c.Parent)
            {
                if (c.Parent != this) continue;
                if (c == _name) return "Name";
                if (c == _unit) return ManualKey;
                var s = _sections.FirstOrDefault(x => x.Field == c || x.Command == c);
                return s == null ? null : s.Key;
            }
            return null;
        }

        // ==================================================================
        // THE NAME STAYS AT THE LEFT (the page a table: AxisPageFrame)
        // ==================================================================

        /// <summary>The name of the label in a frame's heading that covers the room over the names while the rows are scrolled sideways.</summary>
        public const string NameCoverName = "rowNameCover";

        /// <summary>
        /// THE ROW'S NAME STAYS AT THE LEFT while the rows are scrolled sideways, as
        /// an axis row's: moved along its row by as much as the rows are scrolled,
        /// drawn over the columns passing under it (the whole height of the row: a
        /// field is taller than the name), with a thin rule at its right.
        /// <paramref name="scrolled"/>: how far the rows are scrolled (0: as built);
        /// <paramref name="margin"/>: where the row starts, unscrolled.
        /// </summary>
        public void StickName(int scrolled, int margin)
        {
            Point col;
            if (_name.Parent != this || !_columns.TryGetValue("Name", out col)) return;
            int home = UiScale.For(this, col.X), w = UiScale.For(this, col.Y);
            scrolled = Math.Max(0, scrolled);
            bool stuck = scrolled > 0;
            int left = stuck ? Math.Max(0, scrolled - margin) : home;
            // into the room before the manual movement: the name not against the button passing under it
            int after = stuck ? UiScale.For(this, Gap + GroupGap / 2) : 0;
            int top = stuck ? 0 : UiScale.For(this, 8), height = stuck ? Height : UiScale.For(this, 24);
            var back = BackColor.A == 255 ? BackColor : (Parent != null ? Parent.BackColor : Color.White);
            AxisSettingHMI.SetStuck(_name, left, home + scrolled + w + after, top, height, home + scrolled - left, after, back, stuck);
        }

        /// <summary>
        /// Over the names, in the frame's heading (which follows the rows sideways):
        /// the room kept clear while the rows are scrolled - the column names
        /// passing there would stand over the rows' names. <paramref name="margin"/>:
        /// where the top row starts, unscrolled.
        /// </summary>
        internal void StickHeading(Control host, int scrolled, int margin)
        {
            Point col;
            if (host == null || !_columns.TryGetValue("Name", out col)) return;
            scrolled = Math.Max(0, scrolled);
            var cover = host.Controls[NameCoverName] as Label;
            if (cover == null)
            {
                if (scrolled == 0) return;                    // nothing passes there yet
                cover = new Label { Name = NameCoverName, AutoSize = false, Text = "", UseMnemonic = false, BackColor = host.BackColor };
                host.Controls.Add(cover);
            }
            int home = margin + UiScale.For(this, col.X), w = UiScale.For(this, col.Y);
            bool stuck = scrolled > 0;
            int left = stuck ? scrolled : home;
            int after = stuck ? UiScale.For(this, Gap + GroupGap / 2) : 0;
            AxisSettingHMI.SetStuck(cover, left, home + scrolled + w + after, 0, Math.Max(1, host.Height), 0, 0, host.BackColor, stuck);
            if (cover.Visible != stuck) cover.Visible = stuck;
        }

        /// <summary>True when the name is moved along its row - the rows scrolled sideways (tests).</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool NameStuck { get { return AxisSettingHMI.IsStuck(_name); } }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> _freezing =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();

        /// <summary>
        /// The page made a table (AxisPageFrame.Freeze) once it is shown and laid
        /// out - not while its handles are made; once a page, by whichever row is
        /// first. Not in the designer: there the page is the designer's own.
        /// </summary>
        private void ScheduleFreeze(Control page)
        {
            if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;
            if (!(page is ScrollableControl) || page is Form || AxisPageFrame.OfBody(page) != null) return;
            object unused;
            if (_freezing.TryGetValue(page, out unused)) return;
            _freezing.Add(page, this);
            var kind = GetType();
            Action freeze = () =>
            {
                if (page.IsDisposed) return;
                // from the left top: the page was scrolled only by the focus its first row got
                var frame = AxisPageFrame.Freeze(page, keepScroll: false);
                // the headings laid out again in their new place, the names stuck
                if (frame != null) LayoutPage(frame.Body, kind);
            };
            try { page.BeginInvoke(freeze); } catch { }
        }

        // ==================================================================
        // THE PAGE: the rows of a kind together
        // ==================================================================
        private int _rank = -1;
        private bool _conditionsMet = true;
        [ThreadStatic] private static bool _restacking;

        /// <summary>The row as the page wants it: its conditions met.</summary>
        internal bool Wanted { get { return _conditionsMet; } }

        /// <summary>A page's rows of one kind, in the designer's order (kept the first time: the rows move up and down later).</summary>
        public static List<CylinderRowHMI> RowsOfKind(Control host, Type kind)
        {
            host = RowsHost(host);
            if (host == null) return new List<CylinderRowHMI>();
            var rows = host.Controls.OfType<CylinderRowHMI>().Where(r => kind == null || r.GetType() == kind).ToList();
            if (rows.Any(r => r._rank < 0))
            {
                int next = rows.Where(r => r._rank >= 0).Select(r => r._rank + 1).DefaultIfEmpty(0).Max();
                foreach (var r in rows.Where(r => r._rank < 0).OrderBy(r => r.Top).ThenBy(r => r.Left)) r._rank = next++;
            }
            return rows.OrderBy(r => r._rank).ToList();
        }

        /// <summary>
        /// THE PAGE AS ONE TABLE: every row of the kind with the same names' column
        /// (the widest name of the page, or the first row's NameWidth when set), the
        /// headings over the columns, the rows shown moved up.
        /// </summary>
        public static void LayoutPage(Control host, Type kind)
        {
            host = RowsHost(host);
            var rows = RowsOfKind(host, kind);
            if (rows.Count == 0) return;
            int fixedWidth = rows[0].NameWidth;
            int w = fixedWidth > 0 ? fixedWidth
                  : Math.Min(MaxNameWidth, Math.Max(MinNameWidth, rows.Max(r => r.MeasuredName())));
            foreach (var r in rows) r.LayoutRow(w);
            rows[0].AlignHeadings();
            Restack(host, kind);
            // in a frame scrolled sideways: the names where the frame keeps them
            var frame = AxisPageFrame.OfBody(host);
            if (frame != null) frame.StickNames();
        }

        /// <summary>
        /// Where a page's rows are: the page itself - or, once it is a table
        /// (AxisPageFrame), its frame's body.
        /// </summary>
        public static Control RowsHost(Control page)
        {
            var frame = page != null ? page.Controls[AxisPageFrame.FrameName] as AxisPageFrame : null;
            return frame != null ? frame.Body : page;
        }

        /// <summary>
        /// Where a page's headings are: above its rows - or, once it is a table
        /// (AxisPageFrame), in its frame's heading.
        /// </summary>
        public static Control HeadingsHost(Control page)
        {
            var frame = page != null ? page.Controls[AxisPageFrame.FrameName] as AxisPageFrame ?? AxisPageFrame.OfBody(page) : null;
            return frame != null ? frame.HeadContent : page;
        }

        /// <summary>
        /// THE HEADINGS OVER THE COLUMNS: the labels of the page tagged with the
        /// kind's heading tag and a column are put over their columns and
        /// translated; at run time, a page without them gets them, in the axis
        /// pages' style, above its first row.
        /// </summary>
        public void AlignHeadings()
        {
            var host = Parent;
            if (host == null) return;
            var rows = RowsOfKind(host, GetType());
            if (rows.Count == 0) return;
            // A page that is a table: the headings in its frame's heading, which
            // follows the rows sideways by itself - the rows' place in its terms.
            var frame = AxisPageFrame.OfBody(host);
            var heads = frame != null ? (Control)frame.HeadContent : host;
            int left = rows.Min(r => r.Left) - (frame != null ? frame.Body.AutoScrollPosition.X : 0);
            int top = frame != null ? frame.Head.Height : rows.Min(r => r.Top);
            var byKey = new Dictionary<string, Label>();
            foreach (var l in heads.Controls.OfType<Label>())
            {
                var tag = l.Tag as string;
                if (tag != null && tag.StartsWith(_headingTag)) byKey[tag.Substring(_headingTag.Length)] = l;
            }
            bool design = DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
            foreach (var key in SectionKeys)
            {
                Point col;
                if (!_columns.TryGetValue(key, out col)) continue;
                Label l;
                if (!byKey.TryGetValue(key, out l))
                {
                    // Made at run time only: one made in the designer would not be in its file.
                    if (design) continue;
                    l = NewHeading(key);
                    // at factor 1: a page already magnified magnifies what is added to it
                    int topLogical = UiScale.IsScaled(this) ? UiScale.Logical(top) : top;
                    l.SetBounds(0, Math.Max(0, topLogical - HeadingHeight - 4), col.Y, HeadingHeight);
                    heads.Controls.Add(l);
                }
                l.Left = left + UiScale.For(this, col.X);
                l.Width = UiScale.For(this, col.Y);
                l.Text = SectionTitle(key);
            }
        }

        /// <summary>A heading label of this kind in the axis pages' style.</summary>
        public Label NewHeading(string key)
        {
            return new Label
            {
                Name = "lbl" + _prefix + "Head_" + key,
                Tag = _headingTag + key,
                AutoSize = false,
                TextAlign = ContentAlignment.BottomCenter,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(110, 116, 124),
                BackColor = Color.Transparent,
                UseMnemonic = false
            };
        }

        /// <summary>The row of a page whose kind a heading label belongs to, and the label's column; null when it is none.</summary>
        public static CylinderRowHMI RowOfHeading(Label label, out string columnKey)
        {
            columnKey = null;
            var tag = label != null ? label.Tag as string : null;
            if (tag == null || label.Parent == null) return null;
            // in a frame's heading: its rows are in the frame's body
            var frame = AxisPageFrame.Of(label.Parent);
            var rowsHost = frame != null && label.Parent == frame.HeadContent ? frame.Body : label.Parent;
            foreach (var r in rowsHost.Controls.OfType<CylinderRowHMI>())
                if (tag.StartsWith(r._headingTag)) { columnKey = tag.Substring(r._headingTag.Length); return r; }
            return null;
        }

        /// <summary>
        /// THE ROWS SHOWN MOVED UP TOGETHER: the places the designer gave the rows
        /// are kept, and taken in the designer's order by the rows shown first,
        /// then by the hidden ones - no empty line where a row was taken out.
        /// What stands below the rows follows the last row shown.
        /// </summary>
        public static void Restack(Control host, Type kind)
        {
            host = RowsHost(host);
            if (host == null || _restacking) return;
            var rows = RowsOfKind(host, kind);
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

        private static void FollowRows(Control host, List<CylinderRowHMI> rows, List<Point> places, int shown)
        {
            int h = rows.Max(r => r.Height);
            int slotsBottom = places[places.Count - 1].Y + h;
            int shownBottom = shown > 0 ? places[shown - 1].Y + h : places[0].Y;
            foreach (Control c in host.Controls)
            {
                if (c is CylinderRowHMI) continue;
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
            if (Parent != null)
            {
                LayoutPage(Parent, GetType());
                if (Parent.IsHandleCreated) ScheduleFreeze(Parent);
            }
        }

        /// <summary>The names, their column and the headings in the new language - laid out once, by the first row.</summary>
        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Parent == null) return;
            _name.Text = Tr(_title);
            var rows = RowsOfKind(Parent, GetType());
            if (rows.Count > 0 && rows[0] == this) LayoutPage(Parent, GetType());
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
                foreach (var s in _sections)
                    foreach (var c in new Control[] { s.Field, s.Command })
                        if (c != null && c.Parent != this && !c.IsDisposed) c.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// The scanner drives the parts (they are ordinary HMI controls); the row
        /// itself follows its visibility conditions, and the rows move up or down.
        /// </summary>
        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            var shown = ShownConditions;
            bool met = shown == SystemCondition.None || checkGlobal(shown);
            bool changed = met != _conditionsMet;
            _conditionsMet = met;
            if (Visible != met) Visible = met;
            if (changed && Parent != null) Restack(Parent, GetType());
        }
    }
}
