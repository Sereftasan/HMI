using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.Core.Models;

namespace Kaban.UI.Controls
{
    [ToolboxBitmap(typeof(Button))]
    [TypeConverter(typeof(ButtonHMISorter))]
    public partial class ButtonHMI : Button, IHmiControl, ILocalizable, UiStyle.IDrawsOwnHoverBorder,
                                     UiStyle.IManagesOwnCursor
    {
        /// <summary>Uniform standard height so mixed rows line up (buttons and
        /// numeric inputs/displays all default to 46).</summary>
        protected override System.Drawing.Size DefaultSize => new System.Drawing.Size(170, 36);

        // ---- optional attached description (label hugging the LEFT edge) ----
        private AttachedDescription _descr;
        private AttachedDescription Descr { get { return _descr ?? (_descr = new AttachedDescription(this)); } }

        [System.ComponentModel.Category("   Appearance")]
        [System.ComponentModel.Description("Optional description attached to the control's left edge. Enter a TRANSLATION KEY (rendered in the active language) or plain text (shown as-is when the key is not in the translation file). Empty = no label.")]
        public string DescriptionText { get { return _descr?.Text ?? ""; } set { Descr.Text = value; } }

        // FAIL-CLOSED: a control that has never been evaluated by the scanner
        // must NOT be operable. Previously this defaulted to true, so if the
        // scan loop never ran (no PLC, no login) every button was clickable
        // and no enable condition or trigger was ever enforced.
        private bool _softEnabled = false;

        [Category("  Actions")] public int PlcNumber { get; set; } = 1;

        [Category("  Actions")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(ActionListConverter))]
        public List<BitAction> BitActions { get; } = new List<BitAction>();

        [Category("  Actions")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(ActionListConverter))]
        public List<WordAction> WordActions { get; } = new List<WordAction>();

        [Category("  Display")] public string DisplayAddress { get; set; }
        [Category("  Display")] public DynamicValue DisplayOffset { get; set; } = new DynamicValue();
        [Category("  Display")] public string TranslationKey { get; set; } = "";
        [Category("  Display")] public Image ImageActive { get; set; }
        [Category("  Display")] public Image ImagePassive { get; set; }
        [Category("  Display")] public Color ColorActive { get; set; } = Color.FromArgb(76, 175, 80);   // Kaban green indicator
        [Category("  Display")] public Color ColorPassive { get; set; } = Color.FromArgb(240, 241, 243); // flat light cell
        // Lifted with the navigation panel (249): an inactive row has to stay
        // visible AS a row against the lighter bar, not dissolve into it.

        /// <summary>
        /// Background while the button cannot be pressed. LIGHTER than the normal
        /// state, not darker: the old SystemColors.ControlDark made a disabled
        /// button stand out MORE than an available one.
        /// </summary>
        [Category("  Display")] public Color ColorDisabled { get; set; } = Color.FromArgb(248, 249, 250);

        /// <summary>Caption colour while disabled - washed out to match.</summary>
        [Category("  Display")] public Color ForeDisabled { get; set; } = Color.FromArgb(176, 182, 190);

        // ==================================================================
        // STATE VISUALIZATION (per sketch):
        //   relay (M) ON  -> button gets a little darker
        //   output (Y) ON -> even darker + text turns white
        // Plus two small embedded lamps on a corner for input sensors (X).
        // ==================================================================
        [Category("  State")]
        [Description("Bit of the internal command relay (M). ON = button shades slightly darker. Empty falls back to DisplayAddress.")]
        public string RelayAddress { get; set; } = "";

        [Category("  State")]
        [Description("Bit of the physical output (Y). ON = button shades darker still and the text turns white.")]
        public string OutputAddress { get; set; } = "";

        [Category("  State")]
        [Description("Bit of input sensor lamp 1 (small square drawn on the button). Empty hides it.")]
        public string Lamp1Address { get; set; } = "";

        [Category("  State")]
        [Description("Tooltip for lamp 1 (translation key or literal text).")]
        public string Lamp1Tip { get; set; } = "";

        [Category("  State")]
        [Description("Bit of input sensor lamp 2. Empty hides it.")]
        public string Lamp2Address { get; set; } = "";

        [Category("  State")]
        [Description("Tooltip for lamp 2 (translation key or literal text).")]
        public string Lamp2Tip { get; set; } = "";

        /// <summary>
        /// Which edge the lamp strip sits on.
        ///
        /// Top and Bottom exist because a tall narrow button - a column of jog
        /// commands, say - has almost no width to give a vertical strip, and a
        /// lamp squeezed into it is unreadable. On those, a horizontal strip along
        /// the top or bottom edge is the only shape that works.
        /// </summary>
        public enum LampDirectionPosition { Left, Right, Top, Bottom }

        [Category("  State")]
        [Description("Which corner of the button the two sensor lamps sit in.")]
        public LampDirectionPosition LampDirection { get; set; } = LampDirectionPosition.Left;

        [Category("  Display")]
        [Description("Tooltip for the button body (translation key or literal text).")]
        public string ToolTipText { get; set; } = "";

        /// <summary>Set by composite parents (FunctionUnitHMI) for an empty reserved
        /// slot: forces the button hidden regardless of visibility conditions.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool SlotDisabled { get; set; } = false;

        [Category("  Display")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]

        [Editor("Kaban.UI.Design.FlagEnumUIEditor, Kaban.UI", typeof(UITypeEditor))]
        public SystemCondition BlinkCondition { get; set; } = SystemCondition.None;

        [Category("  Triggers")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        public SystemCondition VisibilityConditions { get; set; } = SystemCondition.None;

        [Category("  Triggers")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        public SystemCondition EnableConditions { get; set; } = SystemCondition.None;

        [Category("  Triggers")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(PlcTriggerSorter))] // <--- THIS FIXES THE CRASH & ALLOWS EXPANSION
        public PlcTrigger EnableTrigger { get; set; } = new PlcTrigger();

        [Category(" System")] public bool ConfirmationNeeded { get; set; } = false;
        [Category(" Security")] public SecurityLevel RequiredSecurity { get; set; } = SecurityLevel.None;
        [Category(" Security")] public bool LogAction { get; set; } = false;

        private Func<string, int> _lastReadPlc;
        private bool _relayOn = false;
        private bool _outputOn = false;
        private bool _lamp1On = false;
        private bool _lamp2On = false;
        private bool _blinkActive = false;
        private bool _hovering = false;
        private readonly ToolTip _tt = UiStyle.Tips();
        private bool _ttLeaveHooked;
        private int _ttRegion = -1; // -1 none, 0 body, 1 lamp1, 2 lamp2
        private bool _ttWasAdmin = false;    // role the tooltip was built for
        // Starts as "no data yet" so a fail-closed button explains itself even
        // before the first successful PLC scan.
        private string _disabledReason = "";
        /// <summary>True when this button is actually bound to the PLC in some way.</summary>
        private static bool HasValue(string v)
        {
            // designer defaults come through as "None"/"NULL" rather than empty
            return !string.IsNullOrWhiteSpace(v) && v != "None" && v != "NULL" && v != "0";
        }

        private bool IsPlcBound =>
            HasValue(RelayAddress) || HasValue(OutputAddress) ||
            HasValue(Convert.ToString(VisibilityConditions)) ||
            HasValue(Convert.ToString(EnableConditions)) ||
            (EnableTrigger != null && HasValue(EnableTrigger.MonitorAddress)) ||
            (BitActions != null && BitActions.Count > 0) ||
            (WordActions != null && WordActions.Count > 0);

        private string DisabledReasonOrDefault
        {
            get
            {
                if (!string.IsNullOrEmpty(_disabledReason)) return _disabledReason;
                // A NAVIGATION button has no PLC binding at all, so "waiting for PLC
                // data" was meaningless noise on every menu entry.
                return IsPlcBound ? LanguageManager.GetString("Sys_NoData", "Waiting for PLC data.") : "";
            }
        }

        private static readonly Timer _alertTimer = new Timer { Interval = 220 };
        private static bool _alertPhase;
        private static readonly List<ButtonHMI> _alerting = new List<ButtonHMI>();

        private bool _alertBlink;

        /// <summary>
        /// Draws a pulsing RED border around the button - used to make the alarm page
        /// visible from across the shop floor while the machine error bit is set.
        /// All alerting buttons share one timer so they pulse together.
        /// </summary>
        [Category("  State")]
        public bool AlertBlink
        {
            get { return _alertBlink; }
            set
            {
                if (_alertBlink == value) return;
                _alertBlink = value;
                if (value)
                {
                    if (!_alerting.Contains(this)) _alerting.Add(this);
                    if (!_alertTimer.Enabled)
                    {
                        _alertTimer.Tick += (s, e) =>
                        {
                            _alertPhase = !_alertPhase;
                            for (int i = _alerting.Count - 1; i >= 0; i--)
                            {
                                if (_alerting[i].IsDisposed) { _alerting.RemoveAt(i); continue; }
                                _alerting[i].Invalidate();
                            }
                        };
                        _alertTimer.Start();
                    }
                }
                else _alerting.Remove(this);
                Invalidate();
            }
        }

        private bool _isActivePage = false;
        /// <summary>Marks this button as the currently-active navigation page.</summary>
        [Category("  State")]
        [Description("When true, the button uses ActiveBackColor (used for the selected nav page).")]
        public bool IsActivePage
        {
            get { return _isActivePage; }
            set
            {
                _isActivePage = value;
                // Nav buttons aren't PLC-scanned, so apply the background right away.
                this.BackColor = value ? ActiveBackColor : ColorPassive;

                // BOLD the active navigation row, the same signal the tab strip
                // uses - one visual language for "you are here".
                var want = value ? FontStyle.Bold : FontStyle.Regular;
                if (this.Font != null && this.Font.Style != want)
                    this.Font = new Font(this.Font.FontFamily, this.Font.Size, want);
                SyncFlatFills();
                this.Invalidate();
            }
        }

        [Category("  State")]
        [Description("Background used when IsActivePage is true (default white, to blend into the form content).")]
        public Color ActiveBackColor { get; set; } = Color.White;

        [Category("  Display")]
        [Description("Round only the LEFT corners (right side square) - used so nav buttons visually connect to the form.")]
        public bool RoundLeftOnly { get; set; } = false;

        [Description("Round only the RIGHT corners (Windows-Explorer-style nav rows: square at the panel edge, softly rounded toward the content).")]
        public bool RoundRightOnly { get; set; } = false;

        [Category("  State")]
        [Description("Border color drawn when OutputAddress is ON.")]
        public Color OutputBorderColor { get; set; } = Color.FromArgb(40, 170, 70);

        [Category("  State")]
        [Description("Border color that appears (thin) while the mouse hovers the button.")]
        public Color HoverBorderColor { get; set; } = UiStyle.HoverBorderColor;

        private int _cornerRadius = 4;   // uniform 4px rounding across the control family
        [Category("  Display")]
        [Description("Rounded-corner radius in px (0 = square). Also draws a slight resting border - used for the nav buttons.")]
        public int CornerRadius
        {
            get { return _cornerRadius; }
            set { _cornerRadius = Math.Max(0, value); ApplyCornerRegion(); this.Invalidate(); }
        }

        [Category("  Display")]
        [Description("Resting border color drawn when CornerRadius > 0 (the subtle nav-button outline).")]
        public Color RestingBorderColor { get; set; } = Color.FromArgb(168, 176, 186);

        public ButtonHMI()
        {
            // No default caption: every control carries a DescriptionText, so a
            // second built-in label would only have to be deleted on every drop.
            this.Text = "";
            this.Size = new Size(100, 40);
            // Flat style, and NEUTRALIZE the default gray hover/press fills so the
            // only hover/press feedback is our blinking border (avoids the gray
            // hover colliding with the pressed state).
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderColor = Color.FromArgb(170, 170, 170);
            this.FlatAppearance.BorderSize = 1;
            this.BackColor = ColorPassive;
            this.ForeColor = Color.Black;
            this.Font = UiStyle.Body;
            LanguageManager.LanguageChanged += (s, e) => ApplyTranslation();
            // ApplyTranslation decides whether this button shows a caption or a
            // sign, and it only ran on a language change. A button placed by the
            // designer and never re-translated therefore kept _glyph null, so the
            // arrow and latch marks were never drawn at all - the button just sat
            // there blank, which is exactly what was reported.
            this.HandleCreated += (s, e) => ApplyTranslation();
        }

        // keep the mouse-over/down fill identical to the current back color
        private void SyncFlatFills()
        {
            this.FlatAppearance.MouseOverBackColor = this.BackColor;
            this.FlatAppearance.MouseDownBackColor = this.BackColor;
        }

        private void ApplyCornerRegion()
        {
            if (_cornerRadius <= 0)
            {
                if (this.Region != null) { this.Region.Dispose(); this.Region = null; }
                return;
            }
            int d = _cornerRadius * 2;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var r = new Rectangle(0, 0, this.Width, this.Height);
            if (RoundLeftOnly)
            {
                // rounded left corners, square right (connects to the form edge)
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddLine(r.Right, r.Y, r.Right, r.Bottom);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
            }
            else if (RoundRightOnly)
            {
                // square left (at the panel edge), rounded right corners
                path.AddLine(r.X, r.Bottom, r.X, r.Y);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.CloseFigure();
            }
            else
            {
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
            }
            this.Region = new Region(path);
        }

        private System.Drawing.Drawing2D.GraphicsPath BuildBorderPath(Rectangle r, int d)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            if (RoundLeftOnly)
            {
                // For the active page we omit the RIGHT edge entirely so the white
                // button merges seamlessly into the white content area (open "{" shape).
                if (_isActivePage)
                {
                    path.AddLine(r.Right, r.Y, r.X + d, r.Y);            // top edge (to left arc)
                    path.AddArc(r.X, r.Y, d, d, 270, -90);               // top-left corner
                    path.AddLine(r.X, r.Y + d, r.X, r.Bottom - d);       // left edge
                    path.AddArc(r.X, r.Bottom - d, d, d, 180, -90);      // bottom-left corner
                    path.AddLine(r.X + d, r.Bottom, r.Right, r.Bottom);  // bottom edge (no right edge)
                }
                else
                {
                    path.AddArc(r.X, r.Y, d, d, 180, 90);
                    path.AddLine(r.Right, r.Y, r.Right, r.Bottom);
                    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                }
            }
            else
            {
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
            }
            return path;
        }

        /// <summary>
        /// Literal caption used when the key is missing from the translation files.
        /// It must be stored SEPARATELY: using this.Text as the fallback breaks as
        /// soon as the caption is temporarily cleared (collapsed menu), because
        /// GetString(key, "") returns the KEY itself - which is exactly how raw keys
        /// appeared in the menu after switching language while collapsed.
        /// </summary>
        [Category("   Appearance")]
        /// <summary>
        /// THE BUTTON'S CAPTION. Set this, not Text.
        ///
        /// It is called Fallback because it is what shows when TranslationKey has
        /// no entry for the current language - but on a button with no key, which
        /// is most of them, it is simply the caption. Writing it here rather than
        /// in Text means one place to look, at the top of the property grid with
        /// the rest of this control's own settings, instead of hunting through the
        /// inherited Button properties.
        ///
        /// ButtonText is the same property under a clearer name; either works.
        /// </summary>
        public string FallbackText { get; set; } = "";

        public void ApplyTranslation()
        {
            if (string.IsNullOrEmpty(FallbackText) && !string.IsNullOrEmpty(this.Text))
                FallbackText = this.Text;                       // capture the designer caption once

            // A caption that is PURELY symbols is not translated at all: an arrow
            // is an arrow in every language, and a translation row for one is a
            // row that can only ever be wrong. A missing key would also fall back
            // to the key name and put "Btn_Left" on a button.
            //
            // THE GLYPH IS DRAWN, NOT SET AS TEXT. An earlier version enlarged
            // this.Font instead, which changed the button's preferred size and so
            // its height on every screen in the program. The control's Font is now
            // never touched: base.Text is cleared and OnPaint draws the symbol at
            // its own size, so layout is exactly what the designer laid out.
            // THE DESIGNER DECIDES, not the caption.
            //
            // An earlier version suppressed the sign whenever a button had text,
            // which meant two properties argued over one outcome. Direction already
            // says whether an arrow is wanted - leave it None and none appears - so
            // inspecting the caption as well only made the behaviour harder to
            // predict.
            //
            // A button may now carry BOTH. When it does the sign is drawn on the
            // LEFT and the caption sits beside it, which is what a labelled jog
            // button should look like.
            _caption = LanguageManager.GetString(TranslationKey, FallbackText);

            if (ButtonGlyphs.IsGlyphOnly(_caption))
            {
                _glyph = _caption;       // the caption IS a symbol
                _caption = "";
            }
            else
            {
                _glyph = "";             // EffectiveGlyph supplies direction/action
            }

            // Text is always painted by this control now: the framework centres its
            // own caption, which would sit on top of a glyph drawn beside it.
            _settingText = true;
            try { base.Text = ""; }
            finally { _settingText = false; }
        }

        /// <summary>
        /// The caption, under the name most people look for first. Identical to
        /// FallbackText - not a second value to keep in step.
        /// </summary>
        [Category("   Appearance")]
        [Description("The button's caption. Same property as FallbackText.")]
        public string ButtonText
        {
            get { return FallbackText; }
            set { FallbackText = value; ApplyTranslation(); }
        }

        /// <summary>
        /// Hidden from the designer. The caption comes from ButtonText /
        /// FallbackText, and this control paints its own text - leaving the
        /// inherited Text visible invited setting a caption in a place that is now
        /// ignored.
        /// </summary>
        [Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public override string Text
        {
            // RETURNS THE EMPTY BASE VALUE, not the caption.
            //
            // This is the whole doubling bug. Button.OnPaint asks for THIS.Text,
            // which is virtual - so an override returning the caption handed the
            // base class the very string this control paints itself, and it drew
            // it centred and wrapped a few pixels from ours. Keeping base.Text
            // empty was never enough while the getter answered with something else.
            //
            // The caption is read through DisplayText or ButtonText.
            get { return base.Text; }
            set
            {
                // ROUTED, not stored. This control paints its own caption, so a
                // value left in base.Text would be drawn by the framework AS WELL -
                // which is exactly the doubled text that appeared on the navigation
                // buttons, because MainForm assigns .Text directly when it collapses
                // and expands the menu.
                //
                // Older code and older designer files can keep setting Text; it
                // becomes the caption and base.Text stays empty.
                if (_settingText) { base.Text = value; return; }
                _settingText = true;
                try
                {
                    FallbackText = value ?? "";
                    ApplyTranslation();
                }
                finally { _settingText = false; }
            }
        }

        private bool _settingText;

        /// <summary>
        /// Catches text arriving by ANY route and turns it into the caption.
        ///
        /// This control paints its own caption, so anything left in base.Text is
        /// drawn a second time by WinForms - centred and wrapped, a few pixels off
        /// from ours. That was the doubled navigation label.
        ///
        /// It is done HERE rather than in OnPaint because assigning Text during a
        /// paint invalidates the control mid-draw: the base class has often already
        /// painted the old value by then, so the doubling survived. OnTextChanged
        /// fires whoever set it and whenever - the designer, a resize, older code
        /// calling .Text - and always outside the paint.
        /// </summary>
        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (_settingText || string.IsNullOrEmpty(base.Text)) return;

            _settingText = true;
            try
            {
                if (string.IsNullOrEmpty(FallbackText)) FallbackText = base.Text;
                _caption = LanguageManager.GetString(TranslationKey, FallbackText);
                base.Text = "";
            }
            finally { _settingText = false; }
            Invalidate();
        }

        /// <summary>
        /// Whether the caption is DRAWN. The caption itself is kept either way.
        ///
        /// The collapsed navigation bar used to blank a button's Text to hide its
        /// label, which worked while Text was a dumb string. Now that setting Text
        /// assigns the caption, blanking it asked for "the translation of this
        /// button's key with an empty fallback" - which is the translated caption
        /// again, so the label came back. Hiding is a display decision and now says
        /// so, leaving the caption untouched to be shown again on expand.
        /// </summary>
        [Category("   Appearance")]
        public bool ShowCaption
        {
            get { return _showCaption; }
            set { if (_showCaption == value) return; _showCaption = value; Invalidate(); }
        }
        private bool _showCaption = true;

        /// <summary>The symbol to draw, or "" when the action/direction supplies it.</summary>
        private string _glyph;

        /// <summary>The translated caption, drawn beside the symbol.</summary>
        private string _caption = "";

        /// <summary>
        /// Which way this button drives its mechanism. Set it and the arrow is
        /// chosen automatically - no caption to type and none to mistranslate.
        /// </summary>
        [Category("   Appearance"), Description("Direction arrow drawn on the button.")]
        public ButtonGlyphs.Direction Direction { get; set; }

        /// <summary>
        /// The symbol this button should be showing right now.
        ///
        /// Priority: an explicit Direction, then the action's own mark (which for
        /// Alternate depends on the CURRENT bit state, so the button always shows
        /// what the next press will do), then any glyph caption the designer set.
        /// </summary>
        private string EffectiveGlyph()
        {
            if (_glyph == null) return null;
            if (!string.IsNullOrEmpty(_glyph)) return _glyph;

            // DIRECTION IS EXPLICIT: the designer asked for an arrow, so it is
            // drawn whether or not the button is also captioned.
            if (Direction != ButtonGlyphs.Direction.None)
                return ButtonGlyphs.ArrowFor(Direction);

            // THE ACTION MARK IS INFERRED, so it defers to a caption. A button
            // labelled "buttonHMI4" with an Alternate action was drawing the latch
            // mark beside its own name, because nothing had been asked for and the
            // mark appeared anyway. A button that says what it does has said it.
            if (string.IsNullOrEmpty(_caption) &&
                BitActions != null && BitActions.Count > 0)
            {
                var mark = ButtonGlyphs.MarkFor(BitActions[0].Action, _relayOn);
                if (!string.IsNullOrEmpty(mark)) return mark;
            }
            return _glyph;
        }

        /// <summary>
        /// What this button shows - the symbol when it has one, otherwise its
        /// translated caption. Callers that want the visible label ask for this
        /// rather than Text, which is deliberately empty for a glyph button.
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        public string DisplayText
        {
            get
            {
                // The EFFECTIVE glyph, not the stored one: a bare button holds ""
                // and takes its sign from its Direction or its bit action, so
                // returning the stored value reported an empty caption for a button
                // that visibly draws an arrow.
                var glyph = EffectiveGlyph();
                if (!string.IsNullOrEmpty(_caption)) return _caption;
                return string.IsNullOrEmpty(glyph) ? this.Text : glyph;
            }
        }

        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            _lastReadPlc = readPlc;

            bool vis = !SlotDisabled && checkGlobal(VisibilityConditions);
            if (this.Visible != vis) this.Visible = vis;
            if (!vis) return;

            bool secOk = SecurityManager.HasAccess(RequiredSecurity);
            bool condOk = checkGlobal(EnableConditions);
            bool trigOk = !(EnableTrigger != null && !string.IsNullOrEmpty(EnableTrigger.MonitorAddress))
                          || EnableTrigger.Check(readPlc);

            // THE CONNECTION IS PART OF THE ENABLE. This control greys itself
            // through _softEnabled rather than Enabled - so tooltips still show on
            // hover - and the earlier fix set Enabled, which it ignores. A button
            // on a dead station now greys out, refuses the press, and says why.
            bool connOk = !ConnectionMonitor.IsKnownDown(PlcNumber);

            bool en = secOk && condOk && trigOk && connOk;
            _softEnabled = en;

            var wantCursor = en ? Cursors.Hand : Cursors.Default;
            if (this.Cursor != wantCursor) this.Cursor = wantCursor;

            // Build the auto-tooltip reason shown when the operator hovers a
            // DISABLED button (uses the Sys_* translation keys). Cleared when enabled.
            // A lost connection comes first: it is the reason nothing else matters.
            if (!connOk)
            {
                _disabledReason = ConnectionMonitor.ReasonFor(PlcNumber);
            }
            else if (en)
            {
                _disabledReason = "";
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(LanguageManager.GetString("Sys_Disabled", "Action Disabled."));
                if (!secOk)
                {
                    sb.Append("\n").Append(LanguageManager.GetString("Sys_ReqSec", "Requires Security Level:"))
                      .Append(" ").Append(RequiredSecurity);
                }
                if (!condOk)
                {
                    sb.Append("\n").Append(LanguageManager.GetString("Sys_ReqState", "Required Machine State:"))
                      .Append(" ").Append(EnableConditions);
                }
                if (!trigOk && EnableTrigger != null && !string.IsNullOrEmpty(EnableTrigger.MonitorAddress))
                {
                    sb.Append("\n").Append(LanguageManager.GetString("Sys_WaitSignal", "Waiting for Signal:"))
                      .Append(" ").Append(EnableTrigger.MonitorAddress);
                }
                _disabledReason = sb.ToString();
            }

            // --- read state bits ---
            // Relay (M): falls back to legacy DisplayAddress so old forms keep working.
            string relayAddr = !string.IsNullOrEmpty(RelayAddress) ? RelayAddress : EffectiveDisplayAddress(readPlc);
            bool relay = !string.IsNullOrEmpty(relayAddr) && readPlc(relayAddr) != 0;
            bool output = !string.IsNullOrEmpty(OutputAddress) && readPlc(OutputAddress) != 0;
            bool l1 = !string.IsNullOrEmpty(Lamp1Address) && readPlc(Lamp1Address) != 0;
            bool l2 = !string.IsNullOrEmpty(Lamp2Address) && readPlc(Lamp2Address) != 0;

            bool blinkCond = BlinkCondition != SystemCondition.None && checkGlobal(BlinkCondition);
            bool blink = blinkCond && BlinkHelper.IsBlinkActive;

            if (relay != _relayOn || output != _outputOn || l1 != _lamp1On || l2 != _lamp2On
                || blink != _blinkActive)
            {
                _relayOn = relay; _outputOn = output; _lamp1On = l1; _lamp2On = l2;
                _blinkActive = blink;
                if (ImageActive != null || ImagePassive != null)
                    this.Image = (_relayOn || _outputOn) ? ImageActive : ImagePassive;
                this.Invalidate();
            }

            // --- TWO-color background scheme ---
            //   normal            -> ColorPassive
            //   relay ON / active -> darker background
            // (Output no longer darkens the bg; it draws a bold border instead, so
            //  hover and click states stay visually distinct.)
            Color baseColor = en ? ColorPassive : ColorDisabled;
            Color back;
            Color fore = en ? Color.Black : ForeDisabled;

            if (_blinkActive) { back = Color.Red; fore = Color.White; }
            else if (IsActivePage) { back = ActiveBackColor; }
            else if (_relayOn) { back = Darken(baseColor, 0.20f); }
            else { back = baseColor; }

            if (this.BackColor != back) this.BackColor = back;
            if (this.ForeColor != fore) this.ForeColor = fore;
            SyncFlatFills();
        }


        private string EffectiveDisplayAddress(Func<string, int> readPlc)
        {
            if (string.IsNullOrEmpty(DisplayAddress)) return "";
            int totalOffset = 0;
            if (DisplayOffset != null)
            {
                if (DisplayOffset.Type == LimitSourceType.Static) totalOffset = (int)DisplayOffset.StaticValue;
                else if (DisplayOffset.Type == LimitSourceType.Tag) totalOffset = readPlc(DisplayOffset.TagAddress);
                else if (DisplayOffset.Type == LimitSourceType.HmiVariable) totalOffset = HmiInternalVariables.Get(DisplayOffset.TagAddress);
            }
            return PlcAddressHelper.OffsetAddress(DisplayAddress, totalOffset);
        }

        private static Color Darken(Color c, float factor)
        {
            return Color.FromArgb(
                (int)(c.R * (1f - factor)),
                (int)(c.G * (1f - factor)),
                (int)(c.B * (1f - factor)));
        }

        // Production-style sensor lamps: tall thin vertical bars on the inner
        // side of the button, vertically centered. Two lamps sit side by side.
        private const int LampBarWidth = 7;

        // Clear of the button's own border, not touching it. The lamp is a SENSOR
        // reading - a separate thing that happens to be shown on the button - so it
        // has to read as sitting inside the button rather than as part of its frame.
        // At 3px it merged into the 1.6px border and looked like a decorated edge.
        // Kept small: any more and it stops reading as the END position of the
        // movement this button commands.
        private const int LampBarInset = 6;   // from the button edge to the lamp

        private const int LampBarGap = 2;     // gap between the two bars (tight, to free text space)

        // Slightly shorter than before for the same reason: it buys the same few
        // pixels of clearance at the TOP and BOTTOM edges that LampBarInset buys at
        // the side, so the lamp is inset on every side rather than only two.
        private const float LampBarHeightRatio = 0.72f; // fraction of button height

        private Rectangle LampRect(int index) // 0 = lamp1, 1 = lamp2
        {
            // The strip runs along the chosen edge: vertical for Left/Right,
            // horizontal for Top/Bottom. With BOTH lamps configured they split
            // along that same strip with a visible gap, so they read as two
            // indicators rather than one long one.
            bool both = !string.IsNullOrEmpty(Lamp1Address) && !string.IsNullOrEmpty(Lamp2Address);
            const int midGap = 4;

            if (LampDirection == LampDirectionPosition.Top ||
                LampDirection == LampDirectionPosition.Bottom)
            {
                int y = LampDirection == LampDirectionPosition.Bottom
                        ? this.Height - LampBarInset - LampBarWidth : LampBarInset;
                int fullW = Math.Max(14, (int)(this.Width * LampBarHeightRatio));
                int left = (this.Width - fullW) / 2;
                if (!both) return new Rectangle(left, y, fullW, LampBarWidth);

                int barW = Math.Max(7, (fullW - midGap) / 2);
                int x2 = index == 0 ? left : left + barW + midGap;
                return new Rectangle(x2, y, barW, LampBarWidth);
            }

            bool right = LampDirection == LampDirectionPosition.Right;
            int x = right ? this.Width - LampBarInset - LampBarWidth : LampBarInset;

            int fullH = Math.Max(14, (int)(this.Height * LampBarHeightRatio));
            int top = (this.Height - fullH) / 2;
            if (!both) return new Rectangle(x, top, LampBarWidth, fullH);

            int barH = Math.Max(7, (fullH - midGap) / 2);
            int yy = index == 0 ? top : top + barH + midGap;
            return new Rectangle(x, yy, LampBarWidth, barH);
        }

        /// <summary>
        /// Draws the sign and the caption together.
        ///
        /// LAYOUT: with both, the sign sits on the LEFT and the caption beside it -
        /// a labelled jog button reads "◀ Back", not a word with a mark floating
        /// somewhere. With only one of them, that one is centred.
        ///
        /// The control's Font is never changed. An earlier version enlarged it for
        /// a symbol, which altered the button's preferred size and so its height on
        /// every screen; the glyph is scaled here at paint time instead.
        /// </summary>
        private void DrawGlyph(Graphics g)
        {
            string glyph = EffectiveGlyph();
            bool hasGlyph = !string.IsNullOrEmpty(glyph);
            bool hasText = _showCaption && !string.IsNullOrEmpty(_caption);
            if (!hasGlyph && !hasText) return;

            var area = GlyphArea();
            if (area.Width <= 4 || area.Height <= 4) return;

            var colour = (_softEnabled || !IsPlcBound) ? this.ForeColor
                                                       : Color.FromArgb(170, 176, 184);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            bool latch = glyph == ButtonGlyphs.SetMark || glyph == ButtonGlyphs.ResetMark;

            if (hasGlyph && !hasText)
            {
                if (latch) DrawLatchMark(g, glyph == ButtonGlyphs.SetMark, area);
                else DrawSymbol(g, glyph, area, colour);
                return;
            }

            if (!hasGlyph)
            {
                // HONOUR TextAlign. This control paints its own caption now, and it
                // was centring unconditionally - so the navigation buttons, which
                // ask for MiddleLeft, came out centred however the property was set.
                //
                // An Image is drawn by the base class on the left, so the text has
                // to start past it or the two overlap.
                var captionArea = area;
                if (this.Image != null)
                {
                    int shift = this.Image.Width + Padding.Left + 6;
                    captionArea = new Rectangle(area.X + shift, area.Y,
                                                Math.Max(10, area.Width - shift), area.Height);
                }
                TextRenderer.DrawText(g, _caption, this.Font, captionArea, colour,
                    AlignmentFlags() | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPrefix);
                return;
            }

            // Both: a fixed gutter for the sign, the rest for the words.
            int gutter = Math.Min(area.Height, area.Width / 3);
            var signArea = new Rectangle(area.X, area.Y, gutter, area.Height);
            var textArea = new Rectangle(area.X + gutter + 4, area.Y,
                                         area.Width - gutter - 6, area.Height);

            if (latch) DrawLatchMark(g, glyph == ButtonGlyphs.SetMark, signArea);
            else DrawSymbol(g, glyph, signArea, colour);

            TextRenderer.DrawText(g, _caption, this.Font, textArea, colour,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.WordEllipsis | TextFormatFlags.NoPrefix);
        }

        /// <summary>TextAlign translated into draw flags.</summary>
        private TextFormatFlags AlignmentFlags()
        {
            TextFormatFlags f;
            switch (this.TextAlign)
            {
                case ContentAlignment.TopLeft:
                case ContentAlignment.MiddleLeft:
                case ContentAlignment.BottomLeft:
                    f = TextFormatFlags.Left; break;
                case ContentAlignment.TopRight:
                case ContentAlignment.MiddleRight:
                case ContentAlignment.BottomRight:
                    f = TextFormatFlags.Right; break;
                default:
                    f = TextFormatFlags.HorizontalCenter; break;
            }
            switch (this.TextAlign)
            {
                case ContentAlignment.TopLeft:
                case ContentAlignment.TopCenter:
                case ContentAlignment.TopRight:
                    return f | TextFormatFlags.Top;
                case ContentAlignment.BottomLeft:
                case ContentAlignment.BottomCenter:
                case ContentAlignment.BottomRight:
                    return f | TextFormatFlags.Bottom;
                default:
                    return f | TextFormatFlags.VerticalCenter;
            }
        }

        private void DrawSymbol(Graphics g, string glyph, Rectangle area, Color colour)
        {
            float size = Math.Min(this.Font.Size * ButtonGlyphs.GlyphScale,
                                  Math.Max(7f, (area.Height - 4) * 72f / 96f));
            using (var f = new Font(this.Font.FontFamily, size, this.Font.Style))
                TextRenderer.DrawText(g, glyph, f, area, colour,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
        }

        /// <summary>
        /// The latch mark: a triangle driven up against a bar. Filled for SET,
        /// outlined for RESET - the same shape either way, so the two read as one
        /// family and neither can be mistaken for a jog arrow.
        /// </summary>
        private void DrawLatchMark(Graphics g, bool filled, Rectangle area)
        {
            if (area.Width <= 6 || area.Height <= 6) return;

            int h = (int)Math.Min(area.Height * 0.52f, area.Width * 0.52f);
            if (h < 6) h = 6;
            int w = (int)(h * 0.86f);
            int barW = Math.Max(2, h / 6);
            int total = w + barW + Math.Max(2, h / 7);

            int left = area.X + (area.Width - total) / 2;
            int top = area.Y + (area.Height - h) / 2;

            var colour = (_softEnabled || !IsPlcBound) ? this.ForeColor
                                                       : Color.FromArgb(170, 176, 184);
            var tri = new[]
            {
                new Point(left, top),
                new Point(left + w, top + h / 2),
                new Point(left, top + h)
            };
            var bar = new Rectangle(left + total - barW, top, barW, h);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (filled)
            {
                using (var b = new SolidBrush(colour))
                {
                    g.FillPolygon(b, tri);
                    g.FillRectangle(b, bar);
                }
            }
            else
            {
                using (var p2 = new Pen(colour, Math.Max(1.4f, h / 9f)))
                {
                    g.DrawPolygon(p2, tri);
                    g.DrawRectangle(p2, bar.X, bar.Y, bar.Width, bar.Height);
                }
            }
        }

        /// <summary>
        /// Where the caption or mark may be drawn: inside the lamp strip, on
        /// whichever edge it occupies.
        /// </summary>
        private Rectangle GlyphArea()
        {
            bool lamps = HasLamp(Lamp1Address) || HasLamp(Lamp2Address);
            int pad = lamps ? LampBarInset + LampBarWidth + 2 : 2;

            switch (lamps ? LampDirection : LampDirectionPosition.Left)
            {
                case LampDirectionPosition.Top:
                    return new Rectangle(2, pad, Width - 4, Height - pad - 2);
                case LampDirectionPosition.Bottom:
                    return new Rectangle(2, 2, Width - 4, Height - pad - 2);
                case LampDirectionPosition.Right:
                    return new Rectangle(2, 2, Width - pad - 2, Height - 4);
                default:
                    return lamps ? new Rectangle(pad, 2, Width - pad - 2, Height - 4)
                                 : new Rectangle(2, 2, Width - 4, Height - 4);
            }
        }

        private static bool HasLamp(string address)
        {
            return !string.IsNullOrEmpty(address);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            pevent.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

            DrawLamp(pevent.Graphics, Lamp1Address, _lamp1On, LampRect(0));
            DrawLamp(pevent.Graphics, Lamp2Address, _lamp2On, LampRect(1));

            DrawGlyph(pevent.Graphics);

            // Green is reserved for the sensor/output LAMPS, never the button border.
            // Border logic:
            //   CornerRadius > 0 (nav buttons): slight rounded resting border,
            //       turning blue on hover.
            //   CornerRadius == 0 (function buttons): no resting border; thin blue
            //       border on hover only.
            if (_cornerRadius > 0)
            {
                pevent.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Color c;
                if (_hovering) c = HoverBorderColor;
                else if (_isActivePage) c = RestingBorderColor;   // clean outline on the active row
                else c = RestingBorderColor;

                // INSET by 1px: a GraphicsPath Region clips on whole pixels, so a
                // border drawn on the boundary loses its right/bottom edges. Drawing
                // just inside (and 2px wide) keeps all four sides visible and makes
                // the rounded corners clearly readable.
                int d = Math.Max(2, (_cornerRadius - 1) * 2);
                var r = new Rectangle(1, 1, this.Width - 3, this.Height - 3);
                // a disabled button gets a faint outline as well as a pale fill;
                // a button with nothing wired to it is never "disabled"
                var borderColour = (_softEnabled || !IsPlcBound) ? c : Color.FromArgb(224, 228, 233);
                using (var path = BuildBorderPath(r, d))
                using (var pen = new Pen(borderColour, _isActivePage ? 2.2f : 1.6f))
                    pevent.Graphics.DrawPath(pen, path);
            }
            else if (_hovering)
            {
                using (var pen = new Pen(HoverBorderColor, 1.6f))
                    pevent.Graphics.DrawRectangle(pen, 1, 1, this.Width - 3, this.Height - 3);
            }

            if (_alertBlink && _alertPhase)
            {
                // THIN: just enough to point the operator at the page. A heavy border
                // dominated the whole menu and made everything else hard to read.
                using (var pen = new Pen(Color.FromArgb(220, 30, 30), 1.4f))
                {
                    var r2 = new Rectangle(1, 1, this.Width - 3, this.Height - 3);
                    if (_cornerRadius > 0)
                    {
                        using (var path = BuildBorderPath(r2, Math.Max(2, (_cornerRadius - 1) * 2)))
                            pevent.Graphics.DrawPath(pen, path);
                    }
                    else pevent.Graphics.DrawRectangle(pen, r2);
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyCornerRegion();
        }

        private void DrawLamp(Graphics g, string address, bool on, Rectangle rect)
        {
            if (string.IsNullOrEmpty(address)) return;

            Color face = on ? Color.FromArgb(80, 200, 90) : Color.FromArgb(200, 200, 200);
            Color light = on ? Color.FromArgb(150, 240, 150) : Color.FromArgb(232, 232, 232);
            Color dark = on ? Color.FromArgb(40, 130, 50) : Color.FromArgb(150, 150, 150);

            using (var fill = new SolidBrush(face))
                g.FillRectangle(fill, rect);

            // simple recessed bevel: light on top/left, dark on bottom/right
            using (var pl = new Pen(light))
            {
                g.DrawLine(pl, rect.Left, rect.Top, rect.Right - 1, rect.Top);
                g.DrawLine(pl, rect.Left, rect.Top, rect.Left, rect.Bottom - 1);
            }
            using (var pd = new Pen(dark))
            {
                g.DrawLine(pd, rect.Left, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);
                g.DrawLine(pd, rect.Right - 1, rect.Top, rect.Right - 1, rect.Bottom - 1);
            }
        }

        // Region-aware tooltip: hovering a lamp shows its tip, otherwise the button tip.
        protected override void OnMouseMove(MouseEventArgs mevent)
        {
            base.OnMouseMove(mevent);

            int region = 0;
            if (!string.IsNullOrEmpty(Lamp1Address) && LampRect(0).Contains(mevent.Location)) region = 1;
            else if (!string.IsNullOrEmpty(Lamp2Address) && LampRect(1).Contains(mevent.Location)) region = 2;

            // The tooltip depends on BOTH the hovered region and the logged-in role,
            // so re-evaluate when either changes (e.g. an admin logs in mid-hover).
            bool isAdmin = SecurityManager.HasAccess(SecurityLevel.Admin);
            if (region == _ttRegion && isAdmin == _ttWasAdmin) return;
            _ttRegion = region;
            _ttWasAdmin = isAdmin;

            string text;
            if (region == 1) text = BuildLampTooltip(isAdmin, 1, Lamp1Address, Lamp1Tip);
            else if (region == 2) text = BuildLampTooltip(isAdmin, 2, Lamp2Address, Lamp2Tip);
            else text = BuildMainTooltip(isAdmin);   // operator layer for everyone; admin extras appended

            _tt.SetToolTip(this, string.IsNullOrEmpty(text) ? "" : text);

            // Re-setting the text while a tip is on screen is what lets it outlive
            // the pointer, so this control has to put it down itself. Hooked once.
            if (!_ttLeaveHooked)
            {
                _ttLeaveHooked = true;
                UiStyle.HideTooltipOnLeave(_tt, this);
            }
        }

        /// <summary>
        /// Tooltip for a hovered SENSOR LAMP only. Admins get that lamp's address -
        /// not the whole button - so the popup stays readable.
        /// </summary>
        private string BuildLampTooltip(bool isAdmin, int index, string address, string tipKey)
        {
            // OPERATOR layer - every user sees the translated lamp tip.
            string label = LanguageManager.Localize(tipKey);
            if (!isAdmin) return label;

            // ADMIN extras under a clear caption, after a BLANK line; empty fields omitted.
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(label)) sb.Append(label).Append('\n');
            sb.Append('\n').Append(AdminCaption).Append('\n');
            sb.Append("Sensor Lamp ").Append(index).Append('\n');
            if (!string.IsNullOrWhiteSpace(address))
                sb.Append("Address:\t").Append(PlcAddressHelper.Describe(PlcNumber, address)).Append('\n');
            sb.Append("State:\t\t").Append((index == 1 ? _lamp1On : _lamp2On) ? "ON" : "OFF");
            return sb.ToString();
        }

        private static string AdminCaption =>
            "----- " + LanguageManager.GetString("Tip_ForAdmin", "For Admin") + " -----";

        /// <summary>
        /// ADMIN diagnostic tooltip: the full wiring of this control, so an engineer
        /// can see what a button is bound to without opening the designer. Operators
        /// never see this - they get the translated disabled-reason instead.
        /// </summary>
        private string BuildMainTooltip(bool isAdmin)
        {
            // LAYOUT (agreed with the operators):
            //   1. the control's LABEL - but only when the screen does not already
            //      show it (an icon-only or unlabelled control)
            //   2. the operator help: what it does, or why it is blocked
            //   3. a blank line, the admin caption, then the wiring - admins only
            var sb = new System.Text.StringBuilder();

            string shownText = (Text ?? "").Trim();
            string label = shownText.Length > 0 ? shownText
                                                : LanguageManager.GetString(TranslationKey, FallbackText);
            if (shownText.Length == 0 && !string.IsNullOrWhiteSpace(label))
                sb.Append(label).Append('\n');      // caption not visible on screen

            string tip = !_softEnabled ? DisabledReasonOrDefault
                                       : LanguageManager.Localize(ToolTipText);
            if (!string.IsNullOrWhiteSpace(tip)) sb.Append(tip).Append('\n');

            if (!isAdmin) return sb.ToString().TrimEnd('\n');

            // ADMIN extras are collected FIRST: the "For Admin" caption is only
            // written when something actually follows it, otherwise every control
            // without wiring showed an empty admin header.
            var admin = new System.Text.StringBuilder();
            Action<string, string> add = (caption, value) =>
            {
                if (string.IsNullOrWhiteSpace(value) || value == "None" || value == "NULL") return;
                admin.Append('\n').Append(caption).Append(value);
            };

            add("Visibility Condition:\t", Convert.ToString(VisibilityConditions));
            add("Enable Condition:\t", Convert.ToString(EnableConditions));
            if (EnableTrigger != null && !string.IsNullOrEmpty(EnableTrigger.MonitorAddress))
                add("Enable Trigger:\t\t", EnableTrigger.ToString());
            add("Security Level:\t\t", RequiredSecurity == SecurityLevel.None ? "" : RequiredSecurity.ToString());
            add("Relay (M):\t\t", RelayAddress);
            add("Output (Y):\t\t", OutputAddress);

            // Actions read as ordinary property lines - "Bit Action:  Momentary M672" -
            // rather than under a heading of their own, which broke the two-column
            // shape of the rest of the block.
            if (BitActions != null)
                foreach (var act in BitActions)
                    if (act != null) add("Bit Action:\t\t", act.ToString());
            if (WordActions != null)
                foreach (var act in WordActions)
                    if (act != null) add("Word Action:\t\t", act.ToString());

            if (admin.Length == 0) return sb.ToString().TrimEnd('\n');   // nothing to show an admin
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(AdminCaption).Append(admin);
            return sb.ToString().TrimEnd('\n');
        }

        private static string Str(string s) => string.IsNullOrWhiteSpace(s) ? "None" : s;

        protected override void OnMouseLeave(EventArgs eventargs)
        {
            base.OnMouseLeave(eventargs);
            _ttRegion = -1;
            _hovering = false;
            this.Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovering = true;
            this.Invalidate();
        }

        // --- RESTORED: actually write to the PLC instead of just logging to the console ---
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            if (!_softEnabled) return;

            if (ConfirmationNeeded)
            {
                var result = MessageBox.Show("Are you sure you want to execute this action?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes) return;
            }

            var driver = PlcManager.Instance.GetDriverForWrite(PlcNumber);
            if (driver == null)
            {
                MessageBox.Show(PlcManager.Instance.DescribeAccess(PlcNumber),
                    "PLC Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool anyFailed = false;

            foreach (var action in BitActions)
            {
                if (action == null) continue;
                // Momentary actions are handled on MouseDown/MouseUp instead (see below) -
                // firing them here too on Click would double-write them.
                if (action.Action == BitActionType.Momentary) continue;

                if (!ExecuteBitAction(driver, action)) anyFailed = true;
            }

            foreach (var action in WordActions)
            {
                if (action == null) continue;
                if (!ExecuteWordAction(driver, action)) anyFailed = true;
            }

            if (anyFailed)
            {
                MessageBox.Show("One or more PLC writes failed. Check the connection and try again.",
                    "Write Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            if (!_softEnabled) return;

            var driver = PlcManager.Instance.GetDriver(PlcNumber);
            if (driver == null) return;

            foreach (var action in BitActions)
            {
                if (action == null || action.Action != BitActionType.Momentary) continue;
                WriteBit(driver, EffectiveAddress(action.Address, action.AddressOffset), true, action.Address);
            }
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);

            var driver = PlcManager.Instance.GetDriver(PlcNumber);
            if (driver == null) return;

            foreach (var action in BitActions)
            {
                if (action == null || action.Action != BitActionType.Momentary) continue;
                WriteBit(driver, EffectiveAddress(action.Address, action.AddressOffset), false, action.Address);
            }
        }

        private bool ExecuteBitAction(IPlcDriver driver, BitAction action)
        {
            string effAddr = EffectiveAddress(action.Address, action.AddressOffset);
            if (string.IsNullOrEmpty(effAddr)) return false;

            switch (action.Action)
            {
                case BitActionType.SET:
                    return WriteBit(driver, effAddr, true, action.Address);

                case BitActionType.RESET:
                    return WriteBit(driver, effAddr, false, action.Address);

                case BitActionType.Alternate:
                    int current;
                    if (!driver.TryRead(effAddr, out current))
                    {
                        return false; // can't safely toggle without knowing the current state
                    }
                    return WriteBit(driver, effAddr, current == 0, action.Address);

                case BitActionType.Momentary:
                    return true; // handled in OnMouseDown/OnMouseUp

                default:
                    return false;
            }
        }

        private bool WriteBit(IPlcDriver driver, string effectiveAddress, bool setOn, string originalAddress)
        {
            int result = driver.Write(effectiveAddress, setOn ? 1 : 0);
            bool ok = (result == 0);

            if (ok && LogAction)
            {
                AuditLogger.Log($"set bit {effectiveAddress} (control tag '{originalAddress}') to {(setOn ? 1 : 0)}.");
            }
            return ok;
        }

        private bool ExecuteWordAction(IPlcDriver driver, WordAction action)
        {
            string effAddr = EffectiveAddress(action.Address, action.AddressOffset);
            if (string.IsNullOrEmpty(effAddr)) return false;

            int value = ResolveWordValue(driver, action.Value);

            int result = driver.Write(effAddr, value);
            bool ok = (result == 0);

            if (ok && LogAction)
            {
                AuditLogger.Log($"wrote {value} to {effAddr} (control tag '{action.Address}').");
            }
            return ok;
        }

        private int ResolveWordValue(IPlcDriver driver, DynamicValue value)
        {
            if (value == null || value.Type == LimitSourceType.None) return 0;
            if (value.Type == LimitSourceType.Static) return (int)value.StaticValue;
            if (value.Type == LimitSourceType.HmiVariable) return HmiInternalVariables.Get(value.TagAddress);

            if (value.Type == LimitSourceType.Tag)
            {
                if (string.IsNullOrEmpty(value.TagAddress)) return 0;
                int v;
                driver.TryRead(value.TagAddress, out v); // best effort; 0 if the read fails
                return v;
            }

            return 0;
        }

        private string EffectiveAddress(string baseAddress, DynamicValue offset)
        {
            if (string.IsNullOrEmpty(baseAddress)) return baseAddress;
            int offsetValue = 0;

            if (offset != null)
            {
                if (offset.Type == LimitSourceType.Static) offsetValue = (int)offset.StaticValue;
                else if (offset.Type == LimitSourceType.Tag && _lastReadPlc != null) offsetValue = _lastReadPlc(offset.TagAddress);
                else if (offset.Type == LimitSourceType.HmiVariable) offsetValue = HmiInternalVariables.Get(offset.TagAddress);
            }

            return PlcAddressHelper.OffsetAddress(baseAddress, offsetValue);
        }
    }

    public class ActionListConverter : TypeConverter
    {
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is IEnumerable collection)
            {
                var list = collection.Cast<object>().ToList();
                if (list.Count == 0) return "None";
                string summary = string.Join(", ", list.Take(2).Select(i => i.ToString()));
                return list.Count > 2 ? summary + "..." : summary;
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    public class ButtonHMISorter : ExpandableObjectConverter
    {
        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
        {
            var props = base.GetProperties(context, value, attributes);
            string[] order = { "PlcNumber", "BitActions", "WordActions",
                                "RelayAddress", "OutputAddress", "Lamp1Address", "Lamp1Tip", "Lamp2Address", "Lamp2Tip", "LampDirection",
                                "DisplayAddress", "DisplayOffset", "TranslationKey", "ToolTipText", "ImageActive", "ImagePassive", "ColorActive", "ColorPassive",
                                "VisibilityConditions", "EnableConditions", "EnableTrigger" };
            return props.Sort(order);
        }
    }
}

