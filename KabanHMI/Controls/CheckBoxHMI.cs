using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// How a CheckBoxHMI is drawn (30 October: "Let me see a few of them in switch row so I can see how it looks
    /// ... You can make others in some different mode you think it looks nice as well so we decide which one to use").
    /// </summary>
    public enum CheckBoxLook
    {
        /// <summary>A tick box, its caption beside it (DescriptionText, DescriptionSide).</summary>
        Box = 0,
        /// <summary>An on / off switch in place of the box, its caption beside it.</summary>
        Switch = 1,
        /// <summary>A row as wide as the control: its caption on the left, a switch at the right end, a line under it.</summary>
        SwitchRow = 2,
        /// <summary>A card: its caption, On / Off under it, a small switch; tinted blue while on.</summary>
        Tile = 3
    }

    /// <summary>
    /// What of a field or a switch answers the pointer and the finger (2 November: "this checkbox HMI control render (making
    /// the description area as the control area as well so the click or hovering on there acts same as the control itself)
    /// could nice to use at the numerical controls as well. Is it possible to add another property to make numerical and
    /// binary controls act these two ways according to the selection of the property").
    /// </summary>
    public enum ActiveArea
    {
        /// <summary>The whole row: its caption is part of it - a click or a tap on the caption is one on the control, the pointer
        /// over either lights both.</summary>
        Row = 0,
        /// <summary>The control alone (the switch, the value box): its caption is only words.</summary>
        Control = 1
    }

    /// <summary>
    /// PLC-bound CHECKBOX: reflects one bit and writes it on click, with the same
    /// security gating, audit logging and attached-description support as the rest
    /// of the family. Ticking is reverted when the user lacks permission.
    /// </summary>
    // The Properties window in the order of the work (see PropertyOrder).
    [System.ComponentModel.TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class CheckBoxHMI : CheckBox, UiStyle.IExplainsDisabled, Kaban.UI.Helpers.TouchInput.ISwipes
    {
        // 30 October: the user chose the rows ("this one look nice on your photo, you can make all the checkbox and the
        // checkbox HMI default render this way as well") - a row by default; one too narrow for its name (a tick box in a
        // table, a 44 px box with its caption beside it) is the switch alone, its caption beside it as before.
        private CheckBoxLook _look = CheckBoxLook.SwitchRow;

        /// <summary>The least width a row takes, at factor 1: narrower, it is the switch alone (its caption beside it).</summary>
        public const int RowMinWidth = 120;

        /// <summary>Drawn as a row now: the row look, and room for its name.</summary>
        [Browsable(false)]
        public bool DrawnAsRow { get { return _look == CheckBoxLook.SwitchRow && Width >= UiScale.Px(RowMinWidth); } }

        /// <summary>A row and a card carry their caption themselves; the switch and the tick box have it beside them.</summary>
        private void PlaceCaption() { Descr.Suppressed = DrawnAsRow || _look == CheckBoxLook.Tile; }

        /// <summary>How it is drawn - a row with a switch (the default), a switch, a card, a tick box (CheckBoxLook).</summary>
        [Category("   Appearance"), DefaultValue(CheckBoxLook.SwitchRow)]
        [Description("How it is drawn. Box: a tick box, its caption beside it. Switch: an on / off switch, its caption beside it. " +
                     "SwitchRow: a row - the caption on the left, the switch at the right end, a line under it (make it as wide as the column). " +
                     "Tile: a card - the caption, On / Off, a small switch, tinted while on.")]
        public CheckBoxLook Look
        {
            get { return _look; }
            set
            {
                _look = value;
                PlaceCaption();
                Cursor = DisplayOnly ? Cursors.Default : Cursors.Hand;
                ApplyCornerRegion();
                Invalidate();
            }
        }

        /// <summary>Why it cannot be ticked now (a tap in touch mode shows it); null while it can.</summary>
        string UiStyle.IExplainsDisabled.WhyDisabled
        {
            get
            {
                if (DisplayOnly || string.IsNullOrEmpty(Address)) return null;
                if (IsStale) return ConnectionMonitor.ReasonFor(PlcNumber);
                return !_softEnabled && !string.IsNullOrEmpty(_disabledReason) ? _disabledReason : null;
            }
        }

        // ---- optional attached description (label hugging the LEFT edge) ----
        private AttachedDescription _descr;
        private AttachedDescription Descr => _descr ?? (_descr = new AttachedDescription(this));

        [Category("   Appearance")]
        [Description("Optional description attached to the control's left edge. Enter a TRANSLATION KEY or plain text. Empty = no label.")]
        public string DescriptionText { get { return _descr?.Text ?? ""; } set { Descr.Text = value; } }

        /// <summary>
        /// Text for the TOOLTIP only, never drawn on the control.
        ///
        /// A checkbox in a table column often needs describing without a visible
        /// label: the row and the column heading already say what it is, and a
        /// caption wide enough to be useful would cover the controls beside it.
        /// </summary>
        [Category("   Appearance")]
        public string ToolTipCaption { get; set; }

        [Category("   Data")] public int PlcNumber { get; set; } = 1;
        [Category("   Data")] public string Address { get; set; } = "";
        [Category("   Behavior")] public SecurityLevel RequiredSecurity { get; set; } = SecurityLevel.Operator;
        [Category("   Behavior")] public bool LogAction { get; set; } = true;

        // ---- a change asked for (2 November: "We should add 'ConfirmWrite' property to 'CheckBoxHMI' controls and turn all
        // true at the ones on 'Machine Setting / Options' page because it might be dangerous to change their value accidently.
        // Is it possible if the confirming requirement gets cancelled when the checkbox value changed dragging to left to turn
        // OFF or right to turn ON btw, it confirms the deliberate action as well") ----

        /// <summary>
        /// A tap or a click asks first ("Switch "Cooling" ON?"); the switch SLID across - right to ON, left to OFF, by a finger
        /// or the mouse - changes it at once: sliding it is deliberate already.
        /// </summary>
        [Category("  System"), DefaultValue(false)]
        [Description("A tap or click asks before the bit is written. Sliding the switch across (right: ON, left: OFF) changes it without asking - " +
                     "the slide is the deliberate action.")]
        public bool ConfirmWrite { get; set; }

        /// <summary>How far, at factor 1, the switch is slid before it changes.</summary>
        public const int SwipeDistance = 18;

        private ActiveArea _activeArea = ActiveArea.Row;

        /// <summary>
        /// Row (the default): its caption is part of it - a row's whole width, or the caption beside a switch or a box: a click
        /// or a tap there changes it, the pointer there lights it. Control: only the switch (the box) itself answers; the caption
        /// is only words.
        /// </summary>
        [Category("  System"), DefaultValue(ActiveArea.Row)]
        [Description("Row: the caption is part of it - clicking or hovering the caption acts as on the switch. Control: only the switch (the box) answers.")]
        public ActiveArea ActiveArea
        {
            get { return _activeArea; }
            set { _activeArea = value; Descr.ActsAsOwner = value == ActiveArea.Row; Invalidate(); }
        }

        /// <summary>A point on the switch (the box) itself - with a little room round it for a finger.</summary>
        private bool OnSwitchItself(Point p)
        {
            var look = _look == CheckBoxLook.SwitchRow && !DrawnAsRow ? CheckBoxLook.Switch : _look;
            if (look != CheckBoxLook.SwitchRow && look != CheckBoxLook.Tile) return true;      // the control IS the switch (the box)
            var t = SwitchTrack;
            t.Inflate(UiScale.Px(8), UiScale.Px(6));
            return t.Contains(p);
        }

        private bool _pressByPointer, _pressOnSwitch, _captionOver;

        /// <summary>The question before a tap's change: the box, and the state it would get - true: changed (tests answer it themselves).</summary>
        public static Func<CheckBoxHMI, bool, bool> Confirm = (box, on) => box.AskToChange(on);

        /// <summary>How the last change was made: "tap" (asked), "slide" (not asked), "refused" (tests).</summary>
        [Browsable(false)]
        public string LastChange { get; private set; }

        private bool _down, _slid;
        private Point _downAt;

        /// <summary>A sideways drag on it is its own while it can be changed (TouchInput: no page scrolls under it then).</summary>
        bool Kaban.UI.Helpers.TouchInput.ISwipes.SwipesNow { get { return Swipeable; } }

        private bool Swipeable
        {
            get { return _look != CheckBoxLook.Box && Enabled && !IsStale && !DisplayOnly && _softEnabled && !string.IsNullOrWhiteSpace(Address); }
        }

        /// <summary>The question itself: its name and the state it would get; an Admin also reads the bit and the value.</summary>
        private bool AskToChange(bool on)
        {
            string name = CaptionShown;
            if (string.IsNullOrWhiteSpace(name)) name = string.IsNullOrWhiteSpace(ToolTipCaption) ? Address : LanguageManager.GetString(ToolTipCaption, ToolTipCaption);
            string q = string.Format(on ? LanguageManager.GetString("Chk_ConfirmOn", "Switch \u201C{0}\u201D ON?")
                                        : LanguageManager.GetString("Chk_ConfirmOff", "Switch \u201C{0}\u201D OFF?"), name);
            if (SecurityManager.HasAccess(SecurityLevel.Admin))
                q += "\r\n" + string.Format(LanguageManager.GetString("Chk_ConfirmWrites", "It writes {0} to {1}."), on ? 1 : 0, Address);
            q += "\r\n\r\n" + LanguageManager.GetString("Chk_ConfirmSlide", "Sliding the switch across changes it without this question.");
            var answer = MessageBox.Show(FindForm(), q, LanguageManager.GetString("Sys_ConfirmTitle", "Confirm"),
                                         MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            return answer == DialogResult.Yes;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) Pressed(e.Location, OnSwitchItself(e.Location));
        }

        /// <summary>A press on it - or on the caption beside it, part of it (ActiveArea.Row): the start of a tap or a slide.</summary>
        private void Pressed(Point at, bool onSwitch)
        {
            _down = true;
            _slid = false;
            _downAt = at;
            _pressByPointer = true;
            _pressOnSwitch = onSwitch;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            // only the switch answers (ActiveArea.Control): the hand over it alone
            if (_activeArea == ActiveArea.Control && !_down)
            {
                var hand = !DisplayOnly && _softEnabled && OnSwitchItself(e.Location) ? Cursors.Hand : Cursors.Default;
                if (Cursor != hand) Cursor = hand;
            }
            SlideBy(e);
        }

        /// <summary>The pointer held down and moved (on it or on its caption): far enough sideways, the switch slides.</summary>
        private void SlideBy(MouseEventArgs e)
        {
            if (!_down || _slid || (e.Button & MouseButtons.Left) == 0 || !Swipeable) return;
            if (_activeArea == ActiveArea.Control && !_pressOnSwitch) return;
            int dx = e.X - _downAt.X, dy = e.Y - _downAt.Y;
            if (Math.Abs(dx) < UiScale.Px(SwipeDistance) || Math.Abs(dx) <= 2 * Math.Abs(dy)) return;
            // slid across: right ON, left OFF - no question (the click its lift makes changes nothing more)
            _slid = true;
            bool want = dx > 0;
            if (want == Checked) return;
            LastChange = "slide";
            Checked = want;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Released();
        }

        private void Released()
        {
            _down = false;
            // the click of this lift (before or after this, as the framework has it) is the slide's: then it is over
            if (IsHandleCreated) BeginInvoke(new Action(() => { _slid = false; _pressByPointer = false; }));
        }

        // ---- conditions and trigger, as a ButtonHMI's (14 October: "CheckBoxHMI should
        // have visibility condition, enabling condition and trigger as well") ----

        [Category(" Triggers"), DefaultValue(SystemCondition.None)]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("The box is shown only while these are met.")]
        public SystemCondition VisibilityConditions { get; set; } = SystemCondition.None;

        [Category(" Triggers"), DefaultValue(SystemCondition.None)]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        [Description("The box can be ticked only while these are met (greyed otherwise, the reason in its tooltip).")]
        public SystemCondition EnableConditions { get; set; } = SystemCondition.None;

        [Category(" Triggers")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(Kaban.Core.Models.PlcTriggerSorter))]
        [Description("A PLC signal that must be true before the box can be ticked.")]
        public Kaban.Core.Models.PlcTrigger EnableTrigger { get; set; } = new Kaban.Core.Models.PlcTrigger();

        private bool _softEnabled = true;
        private string _disabledReason = "";

        /// <summary>Whether it may be ticked now (its conditions and trigger) - tests.</summary>
        [Browsable(false)]
        public bool CanTick { get { return _softEnabled; } }

        /// <summary>
        /// A bit that asks for this box: while it is TRUE the box's border blinks
        /// red. The axis row's Ref box uses it for the PLC's home (reference)
        /// request - the axis wants referencing, and the box to tick is the one
        /// blinking.
        /// </summary>
        [Category("   Data"), DefaultValue("")]
        [Description("A bit asking for this box: while it is TRUE the border blinks red (e.g. the PLC's home request on the axis row's Ref box).")]
        public string AlertAddress { get; set; } = "";

        /// <summary>True while AlertAddress reads TRUE (tests, and the paint).</summary>
        [Browsable(false)]
        public bool IsAlerting { get { return _alert; } }
        private bool _alert;
        private bool _alertBlinkOn;

        private bool _suppress;

        /// <summary>Caption on the RIGHT, as a checkbox normally reads.</summary>
        [Category("   Appearance")]
        public DescriptionSide DescriptionSide
        {
            get { return Descr.Side; }
            set { Descr.Side = value; }
        }

        public CheckBoxHMI()
        {
            // No default caption: every control carries a DescriptionText, so a
            // second built-in label would only have to be deleted on every drop.
            this.Text = "";
            Descr.Side = Kaban.Core.Enums.DescriptionSide.Right;
            FlatStyle = FlatStyle.Flat;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            AutoSize = false;
            // 44 x 28 - the same footprint as a command button, so a checkbox and
            // a button standing in adjacent columns line up instead of one looking
            // shrunken beside the other.
            // SQUARE, and the same height as a command button. A tick box is a square
            // shape by convention; stretching it to a button's width made it read as
            // a wide empty field rather than a checkbox.
            Size = new Size(44, 32);
            CheckAlign = ContentAlignment.MiddleCenter;
            // its caption beside it part of it (ActiveArea.Row): lit with it, a click on it is a click on the switch
            Descr.ActsAsOwner = true;
            Descr.CaptionHover += on => { _captionOver = on; Descr.SetRowHover(_over || on); if (_look != CheckBoxLook.Box) Invalidate(); };
            Descr.CaptionClick += (s, e) => { _pressByPointer = false; OnClick(EventArgs.Empty); };
            // a small switch slid from the caption beside it as well (3 November: "on the small switches as well") - the caption
            // keeps the pointer while held, so the slide is measured in its own coordinates
            Descr.CaptionMouseDown += (s, e) => { if (e.Button == MouseButtons.Left) Pressed(e.Location, true); };
            Descr.CaptionMouseMove += (s, e) => SlideBy(e);
            Descr.CaptionMouseUp += (s, e) => Released();
        }

        /// <summary>The caption beside it (tests).</summary>
        internal AttachedDescription CaptionPart { get { return Descr; } }

        /// <summary>Lit as under the pointer now: the row's light (a row) or the caption's (beside it) - tests.</summary>
        [Browsable(false)]
        public bool RowLit { get { return (_over || _captionOver) && (_activeArea == ActiveArea.Row || !DrawnAsRow); } }

        /// <summary>
        /// The same footprint as a command button and a lamp, so the three line up
        /// when they share a row. DefaultSize is what the designer uses when the
        /// control is dropped, so it has to agree with the constructor.
        /// </summary>
        protected override Size DefaultSize => new Size(44, 32);

        private int _cornerRadius = 4;

        /// <summary>Same 4px rounding as every other control.</summary>
        [Category("   Appearance")]
        public int CornerRadius
        {
            get { return _cornerRadius; }
            set { _cornerRadius = Math.Max(0, value); ApplyCornerRegion(); Invalidate(); }
        }

        private void ApplyCornerRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            if (_cornerRadius <= 0 || DrawnAsRow) { Region = null; return; }
            int d = (_look == CheckBoxLook.Tile ? Math.Max(_cornerRadius, 8) : _cornerRadius) * 2;
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                var r = new Rectangle(0, 0, Width, Height);
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
                path.CloseFigure();
                Region = new Region(path);
            }
        }

        /// <summary>
        /// Drawn, not left to the framework.
        ///
        /// A WinForms CheckBox renders a FIXED ~13px glyph however large the
        /// control is, so setting the size to 44x28 left a tiny tick floating in a
        /// big empty box - which is what made it look shrunken beside the buttons.
        /// Owner-drawing lets the box actually fill its control and match the
        /// command buttons on the same row.
        /// </summary>
        /// <summary>
        /// Drawn, not left to the framework.
        ///
        /// A WinForms CheckBox renders a FIXED ~13px glyph however large the
        /// control is, so a 28px box left a tiny tick floating in empty space.
        ///
        /// The BORDER and BACKGROUND are identical in both states - only the tick
        /// appears or disappears. A box that also changes colour is saying the same
        /// thing twice, and on a page of sixteen rows the colour change is what the
        /// eye follows instead of the tick.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Color.White);
            if (_look != CheckBoxLook.Box) { PaintLook(g); return; }

            int side = Math.Min(Width, Height) - 2;
            var box = new Rectangle((Width - side) / 2, (Height - side) / 2, side, side);

            using (var back = new SolidBrush(Color.White))
                g.FillRectangle(back, box);
            bool live = Enabled && !IsStale && _softEnabled;
            using (var pen = new Pen(live ? Color.FromArgb(120, 128, 140)
                                          : Color.FromArgb(205, 210, 217)))
                g.DrawRectangle(pen, box);
            // ASKED FOR: the border blinks red while AlertAddress is TRUE - the
            // same red and the same 2 px as a numeric field out of its limits.
            if (_alert && _alertBlinkOn)
                using (var pen = new Pen(Color.FromArgb(200, 30, 30), 2f))
                    g.DrawRectangle(pen, box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2);

            if (!Checked) return;

            // U+2714 HEAVY CHECK MARK, sized to the box.
            using (var f = new Font("Segoe UI Symbol", side * 0.62f, FontStyle.Bold,
                                    GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, "\u2714", f, box,
                    live ? Color.FromArgb(25, 28, 34) : Color.FromArgb(170, 176, 184),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PlaceCaption();
            ApplyCornerRegion();
        }

        private bool _over;
        protected override void OnMouseEnter(EventArgs e) { _over = true; Descr.SetRowHover(true); if (_look != CheckBoxLook.Box) Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _over = false; Descr.SetRowHover(_captionOver); if (_look != CheckBoxLook.Box) Invalidate(); base.OnMouseLeave(e); }

        /// <summary>Its caption as shown (DescriptionText: a translation key or the text itself).</summary>
        [Browsable(false)]
        public string CaptionShown
        {
            get { var t = DescriptionText; return string.IsNullOrEmpty(t) ? "" : LanguageManager.GetString(t, t); }
        }

        /// <summary>The switch's track in the control (Switch, SwitchRow, Tile) - tests and the drawing.</summary>
        [Browsable(false)]
        public RectangleF SwitchTrack
        {
            get
            {
                var look = _look == CheckBoxLook.SwitchRow && !DrawnAsRow ? CheckBoxLook.Switch : _look;
                switch (look)
                {
                    case CheckBoxLook.SwitchRow:
                    {
                        float h = Math.Max(14f, Math.Min(Height - 16f, 24f)), w = h * 1.75f;
                        return new RectangleF(Width - w - 10f, (Height - h) / 2f, w, h);
                    }
                    case CheckBoxLook.Tile:
                    {
                        float h = Math.Max(12f, Math.Min(Height / 4f, 20f)), w = h * 1.75f;
                        return new RectangleF(Width - w - 12f, Height - h - 10f, w, h);
                    }
                    case CheckBoxLook.Switch:
                    {
                        float h = Math.Max(8f, Math.Min(Math.Min(Height - 8f, 24f), (Width - 4f) / 1.75f)), w = h * 1.75f;
                        return new RectangleF((Width - w) / 2f, (Height - h) / 2f, w, h);
                    }
                    default: return RectangleF.Empty;
                }
            }
        }

        /// <summary>The switch looks: a switch alone, a row with one, a card.</summary>
        private void PaintLook(Graphics g)
        {
            bool live = Enabled && !IsStale && _softEnabled;
            var ink = live ? Color.FromArgb(32, 33, 36) : Color.FromArgb(160, 166, 174);
            var grey = live ? Color.FromArgb(95, 99, 104) : Color.FromArgb(170, 176, 184);
            var red = Color.FromArgb(200, 30, 30);
            bool alert = _alert && _alertBlinkOn;
            var font = UiStyle.Body;
            var look = _look == CheckBoxLook.SwitchRow && !DrawnAsRow ? CheckBoxLook.Switch : _look;
            switch (look)
            {
                case CheckBoxLook.Switch:
                    ToggleSwitch.Draw(g, SwitchTrack, Checked, live);
                    if (alert) using (var path = ToggleSwitch.Pill(RectangleF.Inflate(SwitchTrack, 2f, 2f))) using (var pen = new Pen(red, 2f)) g.DrawPath(pen, path);
                    break;

                case CheckBoxLook.SwitchRow:
                {
                    // the row lit under the pointer - while the whole row answers (ActiveArea.Row)
                    if (_over && live && _activeArea == ActiveArea.Row) using (var back = new SolidBrush(UiStyle.RowHoverFill)) g.FillRectangle(back, 0, 0, Width, Height - 1);
                    var t = SwitchTrack;
                    var text = new Rectangle(8, 0, Math.Max(10, (int)t.Left - 16), Height - 1);
                    TextRenderer.DrawText(g, CaptionShown, font, text, ink,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                    ToggleSwitch.Draw(g, t, Checked, live);
                    if (alert) using (var path = ToggleSwitch.Pill(RectangleF.Inflate(t, 2f, 2f))) using (var pen = new Pen(red, 2f)) g.DrawPath(pen, path);
                    using (var pen = new Pen(UiStyle.RowRule)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);     // (lighter than a table's rule)
                    break;
                }

                case CheckBoxLook.Tile:
                {
                    int rad = Math.Max(_cornerRadius, 8);
                    var card = new Rectangle(0, 0, Width - 1, Height - 1);
                    using (var path = Rounded(card, rad))
                    {
                        var fill = Checked && live ? Color.FromArgb(232, 240, 254) : _over && live ? Color.FromArgb(246, 248, 251) : Color.White;
                        using (var back = new SolidBrush(fill)) g.FillPath(back, path);
                        var edge = alert ? red : Checked && live ? ToggleSwitch.OnColor : Color.FromArgb(205, 210, 217);
                        using (var pen = new Pen(edge, alert || (Checked && live) ? 2f : 1f)) g.DrawPath(pen, path);
                    }
                    var t = SwitchTrack;
                    var text = new Rectangle(12, 8, Width - 24, Math.Max(10, (int)t.Top - 10));
                    TextRenderer.DrawText(g, CaptionShown, font, text, ink,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    string state = Checked ? LanguageManager.GetString("Chk_On", "On") : LanguageManager.GetString("Chk_Off", "Off");
                    var stateRect = new Rectangle(12, (int)t.Top - 2, Math.Max(10, (int)t.Left - 16), (int)t.Height + 4);
                    TextRenderer.DrawText(g, state, UiStyle.FontVariant(UiStyle.Small, FontStyle.Bold), stateRect, Checked && live ? ToggleSwitch.OnColor : grey,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                    ToggleSwitch.Draw(g, t, Checked, live);
                    break;
                }
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle r, int radius)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            int d = Math.Max(1, radius * 2);
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>
        /// True while this box's PLC is known not to be answering: it is drawn
        /// grey and a click does nothing - but it stays ENABLED, so the pointer
        /// still gets its tooltip (which then says why). The scanner used to set
        /// Enabled = false, and a disabled control has no tooltip at all.
        /// </summary>
        [Browsable(false)]
        public bool IsStale { get { return !string.IsNullOrWhiteSpace(Address) && ConnectionMonitor.IsKnownDown(PlcNumber); } }

        /// <summary>
        /// Shown, never ticked: the box follows its bit and writes nothing (the
        /// Axis Monitor's reference activation - it watches).
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool DisplayOnly
        {
            get { return _displayOnly; }
            set { _displayOnly = value; if (value) Cursor = Cursors.Default; }
        }
        private bool _displayOnly;

        protected override void OnClick(EventArgs e)
        {
            if (IsStale || DisplayOnly || !_softEnabled) return;          // not ticked: nothing can be written
            if (_slid) return;                                            // the switch was slid: changed already, no question
            // only the switch answers (ActiveArea.Control): a press beside it on the row changes nothing (a key still does)
            if (_activeArea == ActiveArea.Control && _pressByPointer && !_pressOnSwitch) return;
            if (ConfirmWrite && !string.IsNullOrWhiteSpace(Address) && AutoCheck &&
                SecurityManager.HasAccess(RequiredSecurity) && PlcManager.Instance.CheckPermission(PlcNumber))
            {
                if (!Confirm(this, !Checked)) { LastChange = "refused"; return; }
                LastChange = "tap";
            }
            base.OnClick(e);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            ConnectionMonitor.ConnectionChanged -= OnConnectionChanged;
            base.OnHandleDestroyed(e);
        }

        private void OnConnectionChanged(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke((Action)(() => OnConnectionChanged(sender, e))); return; }
            Invalidate();
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            if (_suppress || string.IsNullOrWhiteSpace(Address)) return;

            if (!SecurityManager.HasAccess(RequiredSecurity) || !PlcManager.Instance.CheckPermission(PlcNumber))
            {
                _suppress = true; Checked = !Checked; _suppress = false;   // revert
                return;
            }
            var drv = PlcManager.Instance.GetDriverForWrite(PlcNumber);
            if (drv == null) return;
            using (Kaban.UI.Helpers.WriteSource.For(this, Checked ? "ticked" : "unticked")) drv.Write(Address, Checked ? 1 : 0);
            if (LogAction)
                AuditLogger.Log($"set bit {Address} (PLC {PlcNumber}) to {(Checked ? 1 : 0)} via checkbox.");
        }

        private readonly ToolTip _tip = UiStyle.Tips();
        private bool _tipBuilt, _tipWired;

        /// <summary>
        /// The device and what it is, on hover.
        ///
        /// A bare checkbox in a column says nothing about which bit it drives, and
        /// "which device is that?" is the first question anyone asks of a control
        /// that looks wrong.
        ///
        /// SHOWN BY THE CONTROL ITSELF (UiStyle.HoverTip), built as it shows: it
        /// was registered with the program's one tooltip, and a registration is
        /// dropped when the window the tooltip belongs to closes - after a keypad
        /// or a message box the checkboxes had no tip at all.
        /// </summary>
        public void RefreshTooltip()
        {
            _tipBuilt = true;
            if (_tipWired) return;
            _tipWired = true;
            UiStyle.HoverTip(_tip, this, TooltipText);
        }

        /// <summary>The tip as it is now (tests).</summary>
        internal string TooltipText()
        {
            if (string.IsNullOrEmpty(Address)) return "";
            // Device only. A caption here repeats the row and the column heading.
            string tip = PlcAddressHelper.StationHeader(PlcNumber) + PlcAddressHelper.Describe(PlcNumber, Address);
            // Not answering: why, first (the box is grey and does nothing).
            if (IsStale) tip = ConnectionMonitor.ReasonFor(PlcNumber) + "\r\n" + tip;
            // held back by its conditions or trigger: what it waits for
            else if (!_softEnabled && !string.IsNullOrEmpty(_disabledReason)) tip = _disabledReason + "\r\n" + tip;
            if (!string.IsNullOrWhiteSpace(AlertAddress))
                tip += "\n" + LanguageManager.GetString("Tip_AlertBit", "Asked for by") + ": " +
                       PlcAddressHelper.Describe(PlcNumber, AlertAddress.Trim()) +
                       (_alert ? "  (" + LanguageManager.GetString("Tip_AlertOn", "now") + ")" : "");
            return tip;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_tipBuilt) RefreshTooltip();
            ConnectionMonitor.ConnectionChanged += OnConnectionChanged;
        }

        /// <summary>
        /// Hidden by its page, whatever its conditions say (5 November, found on the panel: the Industry 4 Logging frame hid the PLC's
        /// switch of a record the HMI takes on a new machine - MachineLogSettings.Arrange - and the next scan showed it again, ON TOP of
        /// the rows moved up into its place: "Profile changed" twice, "Cutting done" gone). The page sets it, never the designer.
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool HiddenByPage
        {
            get { return _hiddenByPage; }
            set { _hiddenByPage = value; if (value && Visible) Visible = false; }
        }
        private bool _hiddenByPage;

        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            if (!_tipBuilt) RefreshTooltip();
            if (_hiddenByPage) { if (Visible) Visible = false; return; }
            if (checkGlobal != null)
            {
                // shown or hidden by its conditions only when it has some: without, its page decides (it was shown again on every scan)
                bool vis = VisibilityConditions == SystemCondition.None || checkGlobal(VisibilityConditions);
                if (VisibilityConditions != SystemCondition.None && Visible != vis) Visible = vis;
                if (!vis) return;
                bool condOk = EnableConditions == SystemCondition.None || checkGlobal(EnableConditions);
                bool trigOk = EnableTrigger == null || string.IsNullOrEmpty(EnableTrigger.MonitorAddress) || EnableTrigger.Check(readPlc);
                bool en = condOk && trigOk;
                if (en != _softEnabled)
                {
                    _softEnabled = en;
                    Cursor = en && !DisplayOnly ? Cursors.Hand : Cursors.Default;
                    Invalidate();
                }
                if (en) _disabledReason = "";
                else
                {
                    var sb = new System.Text.StringBuilder(LanguageManager.GetString("Sys_Disabled", "Action Disabled."));
                    if (!condOk)
                    {
                        sb.Append("\n").Append(LanguageManager.GetString("Sys_ReqState", "Required Machine State:"));
                        foreach (var name in MachineConditionMap.Unmet(EnableConditions, checkGlobal)) sb.Append("\n  \u2022 ").Append(name);
                    }
                    if (!trigOk) sb.Append("\n").Append(LanguageManager.GetString("Sys_WaitSignal", "Waiting for Signal:")).Append(" ")
                                   .Append(string.IsNullOrWhiteSpace(EnableTrigger.DescriptionKey) ? EnableTrigger.MonitorAddress : LanguageManager.Localize(EnableTrigger.DescriptionKey));
                    _disabledReason = sb.ToString();
                }
            }
            bool alert = !string.IsNullOrWhiteSpace(AlertAddress) && readPlc(AlertAddress.Trim()) != 0;
            bool blinkOn = alert && BlinkHelper.IsBlinkActive;
            if (alert != _alert || blinkOn != _alertBlinkOn)
            {
                if (alert != _alert) { _alert = alert; RefreshTooltip(); }
                _alertBlinkOn = blinkOn;
                Invalidate();
            }
            if (string.IsNullOrWhiteSpace(Address)) return;
            bool on = readPlc(Address) != 0;
            if (Checked != on) { _suppress = true; Checked = on; _suppress = false; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _descr?.Dispose();
            base.Dispose(disposing);
        }
    }
}

