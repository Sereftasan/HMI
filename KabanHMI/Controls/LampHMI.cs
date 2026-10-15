using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;

namespace Kaban.UI.Controls
{

    // The Properties window in the order of the work (see PropertyOrder).
    [System.ComponentModel.TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class LampHMI : Label, IHmiControl, UiStyle.IDrawsOwnHoverBorder
    {
        /// <summary>Uniform standard height with the rest of the control family.</summary>
        /// <summary>Square, and exactly as tall as a ButtonHMI, so a lamp lines up
        /// with the buttons on its row.</summary>
        protected override System.Drawing.Size DefaultSize => new System.Drawing.Size(36, 36);

        // ---- optional attached description (label hugging the LEFT edge) ----
        private AttachedDescription _descr;
        private AttachedDescription Descr { get { return _descr ?? (_descr = new AttachedDescription(this)); } }

        [System.ComponentModel.Category("   Appearance")]
        [System.ComponentModel.Description("Optional description attached to the control's left edge. Enter a TRANSLATION KEY (rendered in the active language) or plain text (shown as-is when the key is not in the translation file). Empty = no label.")]
        public string DescriptionText { get { return _descr?.Text ?? ""; } set { Descr.Text = value; } }

        [Category(" System")] public int PlcNumber { get; set; } = 1;
        [Category(" System")] public string Address { get; set; }
        // Written into the page's code as its parts (Type, TagAddress, StaticValue),
        // like a unit's PositionOffset - not into a .resx as a serialized object.
        [Category(" System")] [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)] public DynamicValue AddressOffset { get; set; } = new DynamicValue();

        [Category(" Visuals")] public Image ImageActive { get; set; }
        [Category(" Visuals")] public Image ImagePassive { get; set; }

        // Flat "Kaban table" palette (was hardcoded Lime/Gray).
        /// <summary>Rounded corners, to match the rest of the control family.</summary>
        private int _cornerRadius = 4;

        /// <summary>Rounded corners, to match the rest of the control family.</summary>
        [Category(" Visuals")]
        public int CornerRadius
        {
            get { return _cornerRadius; }
            set { _cornerRadius = Math.Max(0, value); ApplyCornerRegion(); Invalidate(); }
        }

        /// <summary>Caption on the RIGHT by default, so the lamp lines up with the
        /// left edge of the other controls on the page.</summary>
        [Category("   Appearance")]
        public DescriptionSide DescriptionSide
        {
            get { return Descr.Side; }
            set { Descr.Side = value; }
        }

        private void ApplyCornerRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            if (_cornerRadius <= 0) { Region = null; return; }
            int d = _cornerRadius * 2;
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

        /// <summary>True while the bound bit is on.</summary>
        private bool _isOn;

        /// <summary>The lamp is lit now (the AI briefing says what the page shows).</summary>
        [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsOn { get { return _isOn; } }

        /// <summary>
        /// A RING, not a filled tile.
        ///
        /// The state colour lives inside the "O" and nowhere else, and the
        /// background stays clean in both states. A lamp that floods its whole
        /// rectangle dominates every control beside it, and a row of them becomes a
        /// block of colour with no shape to read. Keeping the outline constant and
        /// changing only what it contains means the SHAPE is what the eye finds and
        /// the colour is what it then reads.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Color.White);

            // JUST THE CIRCLE. No square around it: the box was a second shape
            // competing with the one that carries the meaning, and on a row beside
            // square buttons and checkboxes it read as another thing to press.
            // 60% of the control, so the lamp reads as a mark sitting in its space
            // rather than filling it. At full size it drew more attention than the
            // buttons around it, which is backwards for a status indicator.
            int side = (int)(Math.Min(Width, Height) * 0.6f);
            if (side < 6) return;
            var ring = new Rectangle((Width - side) / 2, (Height - side) / 2, side, side);

                        // OFF is GREY, not white: the same unlit colour a button's lamp bar
            // uses, so a lamp and a lit button on one row read as the same family.
            using (var fill = new SolidBrush(_isOn ? OnColor : OffColor))
                g.FillEllipse(fill, ring);

            bool hover = Enabled && _hovering != null && _hovering();
            using (var pen = new Pen(hover ? UiStyle.HoverBorderColor
                                     : Enabled ? Color.FromArgb(25, 28, 34)
                                               : Color.FromArgb(175, 181, 190),
                                     Math.Max(hover ? 2f : 1.5f, side / (hover ? 9f : 12f))))
                g.DrawEllipse(pen, ring);

            if (Image != null)
            {
                int s = (int)(side * 0.55f);
                g.DrawImage(Image, ring.X + (side - s) / 2, ring.Y + (side - s) / 2, s, s);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyCornerRegion();
        }

        [Category(" Visuals")] public Color OnColor { get; set; } = Color.FromArgb(76, 175, 80);
        [Category(" Visuals")] public Color OffColor { get; set; } = Color.FromArgb(225, 225, 225);

        [Category(" Triggers")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(UITypeEditor))]
        public SystemCondition VisibilityConditions { get; set; } = SystemCondition.None;

        [Category(" Visuals")]
        [Description("Tooltip shown to operators (translation key or literal text).")]
        public string ToolTipTextKey { get; set; } = "";

        private readonly ToolTip _toolTip = UiStyle.Tips();

        /// <summary>Pointer over the lamp: its ring turns blue, like every other control's border.</summary>
        private readonly Func<bool> _hovering;

        public LampHMI()
        {
            // No default caption: every control carries a DescriptionText, so a
            // second built-in label would only have to be deleted on every drop.
            this.Text = "";
            this.AutoSize = false;
            // Same footprint as a command button and a checkbox, so the three
            // line up when they share a row.
            // Square, matching the checkbox beside it. Must agree with
            // DefaultSize, or a dropped control differs from a coded one.
            // Smaller than the buttons and checkboxes it sits beside: a lamp is a
            // status mark, not something to aim at, and at the same size it drew
            // more attention than the controls the operator actually uses.
            this.Size = new Size(22, 22);
            this.BackColor = Color.Transparent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            Descr.Side = DescriptionSide.Right;   // control left-aligned, caption beside it
            ApplyCornerRegion();
            // NO BORDER. The control draws its own circle; a Label border put a
            // square back around it, which is the shape this was meant to remove.
            this.BorderStyle = BorderStyle.None;
            this.TextAlign = ContentAlignment.MiddleCenter;
            // ALIVE UNDER THE POINTER: the ring turns blue and the tip comes, as on
            // every other control. A lamp that did neither looked dead.
            _hovering = UiStyle.HoverBorder(this);
            UiStyle.HoverTip(_toolTip, this, BuildTooltip);
        }

        /// <summary>
        /// ROLE-BASED TOOLTIP. Lamps take no input, so operators get the configured
        /// description - or the lamp's caption and state when there is none -
        /// while admins get the diagnostic wiring detail as well. Built on every
        /// visit (UiStyle.HoverTip).
        /// </summary>
        private string BuildTooltip()
        {
            bool isAdmin = SecurityManager.HasAccess(SecurityLevel.Admin);
            string text = LanguageManager.Localize(ToolTipTextKey);
            if (string.IsNullOrWhiteSpace(text))
            {
                // No description of its own: its caption (when it has one) and what it shows now.
                string caption = string.IsNullOrWhiteSpace(DescriptionText) ? "" : LanguageManager.GetString(DescriptionText, DescriptionText).TrimEnd(':', ' ');
                string state = _isOn ? LanguageManager.GetString("Tip_LampOn", "On") : LanguageManager.GetString("Tip_LampOff", "Off");
                text = caption.Length > 0 ? caption + ": " + state : state;
            }
            if (!isAdmin) return text;

            var sb = new System.Text.StringBuilder();
            sb.Append(text).Append("\n\n");
            sb.Append("Address:\t\t\t").Append(string.IsNullOrWhiteSpace(Address)
                ? "None" : PlcAddressHelper.Describe(PlcNumber, Address));
            if (AddressOffset != null && AddressOffset.Type != LimitSourceType.None)
                sb.Append("\nAddress Offset:\t\t").Append(AddressOffset);
            sb.Append("\nVisibility Condition:\t").Append(Kaban.UI.Plc.PlcScanner.DescribeConditions(VisibilityConditions));
            return sb.ToString();
        }

        /// <summary>For tests: the tip the lamp would show now.</summary>
        internal string TooltipText() { return BuildTooltip(); }

        public void UpdateState(Func<string, int> lookup, Func<SystemCondition, bool> checkGlobal)
        {
            bool vis = checkGlobal(VisibilityConditions);
            if (this.Visible != vis) this.Visible = vis;
            if (!vis || string.IsNullOrEmpty(Address)) return;

            int val = lookup(Address); // Simplification: Apply offset here as needed
            bool active = (val != 0);

            if (_isOn != active) { _isOn = active; Invalidate(); }
            if (ImageActive != null || ImagePassive != null)
                this.Image = active ? ImageActive : ImagePassive;
        }
    }
}

