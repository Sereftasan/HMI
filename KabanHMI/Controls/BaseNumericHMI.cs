using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.Core.Models;
using System.Drawing.Design;

namespace Kaban.UI.Controls
{
    [TypeConverter(typeof(BaseNumericSorter))]
    public abstract class BaseNumericHMI : Label, IHmiControl, ILocalizable, UiStyle.IDrawsOwnHoverBorder
    {
        /// <summary>Uniform standard height so mixed rows line up (buttons and
        /// numeric inputs/displays all default to 46).</summary>
        protected override System.Drawing.Size DefaultSize => new System.Drawing.Size(170, 36);

        private bool _showValueBorder = true;

        /// <summary>
        /// Draws a full border around the value. A numeric box with no outline
        /// disappeared into the page; the reading is the point of the control, so
        /// it should look like a field.
        /// </summary>
        [Category("   Appearance")]
        public bool ShowValueBorder
        {
            get { return _showValueBorder; }
            set { _showValueBorder = value; Invalidate(); }
        }

        [Category("   Appearance")]
        public System.Drawing.Color ValueBorderColor { get; set; } = System.Drawing.Color.FromArgb(150, 158, 170);

        private int _cornerRadius = 4;

        /// <summary>Rounded corners, matching the buttons and lamps around it.</summary>
        [Category("   Appearance")]
        public int CornerRadius
        {
            get { return _cornerRadius; }
            set { _cornerRadius = System.Math.Max(0, value); ApplyCornerRegion(); Invalidate(); }
        }

        private void ApplyCornerRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            if (_cornerRadius <= 0) { Region = null; return; }
            int d = _cornerRadius * 2;
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                var r = new System.Drawing.Rectangle(0, 0, Width, Height);
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
                path.CloseFigure();
                Region = new System.Drawing.Region(path);
            }
        }

        protected override void OnResize(System.EventArgs e)
        {
            base.OnResize(e);
            ApplyCornerRegion();
        }

        /// <summary>
        /// True while an entry keypad is open for this field.
        ///
        /// On a page of twenty identical boxes the operator has to be certain which
        /// one they are about to change, and "the keypad opened near it" is not
        /// certainty.
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        public bool IsEditing
        {
            get { return _isEditing; }
            set { if (_isEditing == value) return; _isEditing = value; Invalidate(); Update(); }
        }
        private bool _isEditing;

        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            base.OnPaint(e);

            // BEING EDITED: a thick blue frame round the field the keypad is open
            // for. Setting BackColor did nothing visible, because this control
            // paints its own face - which is why there was no sign at all of which
            // box was being changed.
            if (IsEditing)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new System.Drawing.Pen(
                           System.Drawing.Color.FromArgb(30, 120, 220), 3f))
                    e.Graphics.DrawRectangle(pen, 2, 2, Width - 5, Height - 5);
            }

            if (!_showValueBorder) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int d = System.Math.Max(2, (_cornerRadius - 1) * 2);
            var r = new System.Drawing.Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            using (var pen = new System.Drawing.Pen(
                       UiStyle.BorderFor(_hovering(), ValueBorderColor), 1.6f))
            {
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                e.Graphics.DrawPath(pen, path);
            }
        }

        // ---- optional attached description (label hugging the LEFT edge) ----
        private AttachedDescription _descr;
        private AttachedDescription Descr { get { return _descr ?? (_descr = new AttachedDescription(this)); } }

        [System.ComponentModel.Category("   Appearance")]
        [System.ComponentModel.Description("Optional description attached to the control's left edge. Enter a TRANSLATION KEY (rendered in the active language) or plain text (shown as-is when the key is not in the translation file). Empty = no label.")]
        public string DescriptionText { get { return _descr?.Text ?? ""; } set { Descr.Text = value; } }

        protected Color _originalBackColor;
        protected ToolTip _toolTip = UiStyle.Tips();

        // Last scanned value, cached so NumericInputHMI can format and range-check
        // without a second PLC read.
        protected double _lastNumericValue;

        [Category("   Device")] public int PlcNumber { get; set; } = 1;
        [Category("   Device")] public string Address { get; set; }
        [Category("   Device")] public DataTypeEnum DataType { get; set; } = DataTypeEnum.Bin_16;
        [Category("   Device")] public DynamicValue AddressOffset { get; set; } = new DynamicValue();

        [Category("   Formatting")] public DisplayFormatEnum DataFormat { get; set; } = DisplayFormatEnum.Signed_Decimal;
        [Category("   Formatting")] public double Divisor { get; set; } = 1.0;
        [Category("   Formatting")] public int Digits { get; set; } = 5;
        [Category("   Formatting")] public int DecimalPlaces { get; set; } = 0;
        [Category("   Formatting")] public bool FillZero { get; set; } = false;
        [Category("   Formatting")] public string Suffix { get; set; } = "";

        /// <summary>
        /// Whether the unit suffix is DRAWN. Conversion is unaffected.
        ///
        /// The two are separate concerns and used to be tangled: setting a
        /// UnitType forced the unit's suffix onto the display, so a limit field
        /// that shares the position's unit could not be shown without "mm" on it.
        /// On a row where four fields carry the same unit as the position beside
        /// them, repeating it four times is noise - the value still converts with
        /// the mm/inch setting, it just does not restate the unit.
        /// </summary>
        [Category("   Formatting")] public bool ShowSuffix { get; set; } = true;
        /// <summary>
        /// OBSOLETE: DescriptionText is itself the translation key (it falls back to
        /// its own text when no translation exists), so a second property could only
        /// drift out of step with it. Kept so existing designer files still load.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Category("   Formatting")]
        public string TranslationKey
        {
            get { return DescriptionText; }
            set { if (!string.IsNullOrEmpty(value) && string.IsNullOrEmpty(DescriptionText)) DescriptionText = value; }
        }

        /// <summary>Appends " :" to the description automatically (see AttachedDescription).</summary>
        [Category("   Appearance")]
        public bool DescriptionColon
        {
            get { return Descr.AppendColon; }
            set { Descr.AppendColon = value; }
        }
        [Category("   Formatting")] public UnitType UnitType { get; set; } = UnitType.None;

        [Category("   Triggers")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(UITypeEditor))]
        public SystemCondition VisibilityConditions { get; set; } = SystemCondition.None;

        [Category(" Security")] public SecurityLevel RequiredSecurity { get; set; } = SecurityLevel.None;
        [Category(" Security")] public bool LogAction { get; set; } = false;

        /// <summary>Pointer-over state, so the border can follow the shared blue rule.</summary>
        private readonly System.Func<bool> _hovering;

        public BaseNumericHMI()
        {
            _hovering = UiStyle.HoverBorder(this);
            this.AutoSize = false;
            this.Size = new Size(100, 30);
            this.BorderStyle = BorderStyle.Fixed3D;
            this.TextAlign = ContentAlignment.MiddleRight;
            _originalBackColor = this.BackColor;
            // THE SAME HANDLER Dispose removes. This subscribed a lambda while
            // Dispose unsubscribed OnLanguageChanged - a different delegate - so
            // the removal did nothing and the static event held every numeric field
            // ever created (and the page around it) for the life of the program.
            LanguageManager.LanguageChanged += OnLanguageChanged;
            // Re-render immediately when the operator changes units, instead of
            // waiting for the next PLC scan (which never comes when offline).
            UnitManager.UnitChanged += OnUnitChanged;
        }

        private void OnUnitChanged(object sender, EventArgs e)
        {
            if (this.IsDisposed) return;
            RenderLastValue();
        }

        /// <summary>
        /// Re-formats the most recent PLC value using the CURRENT unit selection.
        /// Used when the unit changes so the text updates at once.
        /// </summary>
        protected void RenderLastValue()
        {
            if (string.IsNullOrEmpty(Address)) return;
            try
            {
                double val = _lastNumericValue;
                double shown = val;
                int decimals = DecimalPlaces;
                string suffix = Suffix;

                if (UnitType != UnitType.None)
                {
                    shown = UnitManager.ToDisplay(UnitType, val);
                    decimals = DecimalPlaces + UnitManager.GetActiveExtraDecimals(UnitType);
                    suffix = UnitManager.GetActiveSuffix(UnitType);
                }
                if (!ShowSuffix) suffix = "";

                string formatted = PlcValueConverter.FormatString(shown, DataType, DataFormat, decimals, Digits, FillZero);
                if (!string.IsNullOrEmpty(suffix)) formatted += " " + suffix;
                this.Text = formatted;
            }
            catch { /* leave the previous text */ }
        }

        public virtual void ApplyTranslation() { }

        [Category(" Visuals")]
        [Description("Tooltip shown to operators (translation key or literal text).")]
        public string ToolTipTextKey { get; set; } = "";

        private bool _ttWasAdmin = false;
        private bool _ttSet = false;

        /// <summary>
        /// ROLE-BASED TOOLTIP. Admins see the full wiring (address, offset,
        /// conditions, security); operators see the description, or - when the
        /// control is blocked - the translated reason. Derived controls override
        /// BuildAdminTooltip/BuildOperatorTooltip to add their own detail.
        /// </summary>
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);

            bool isAdmin = SecurityManager.HasAccess(SecurityLevel.Admin);
            if (_ttSet && isAdmin == _ttWasAdmin) return;
            _ttWasAdmin = isAdmin;
            _ttSet = true;

            string tip = isAdmin ? BuildAdminTooltip() : BuildOperatorTooltip();
            _toolTip.SetToolTip(this, tip ?? "");
        }

        /// <summary>Forces the tooltip to be rebuilt on the next hover (e.g. after login).</summary>
        protected void InvalidateTooltip() { _ttSet = false; }

        protected virtual string BuildAdminTooltip()
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(ToolTipTextKey))
                sb.Append(LanguageManager.Localize(ToolTipTextKey)).Append("\n\n");

            sb.Append("Address:\t\t\t").Append(string.IsNullOrWhiteSpace(Address)
                ? "None" : PlcAddressHelper.Describe(PlcNumber, Address));
            if (AddressOffset != null && AddressOffset.Type != LimitSourceType.None)
                sb.Append("\nAddress Offset:\t\t").Append(AddressOffset);
            sb.Append("\nVisibility Condition:\t").Append(VisibilityConditions);
            sb.Append("\nSecurity Level:\t\t").Append(RequiredSecurity);
            return sb.ToString();
        }

        protected virtual string BuildOperatorTooltip()
        {
            if (!this.Enabled && this.Visible)
            {
                string disabled = LanguageManager.GetString("Sys_Disabled", "Action Disabled.");
                string reason = LanguageManager.GetString("Sys_Reason", "Reason:");
                string reqSec = LanguageManager.GetString("Sys_ReqSec", "Requires Security Level:");
                return $"{disabled}\n{reason} {reqSec} {RequiredSecurity}";
            }
            return LanguageManager.Localize(ToolTipTextKey);
        }

        // UPGRADED: Base now performs the reading and math once
        public virtual void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            bool vis = checkGlobal(VisibilityConditions);
            if (this.Visible != vis) this.Visible = vis;
            if (!vis || string.IsNullOrEmpty(Address)) return;

            try
            {
                string eff = GetEffectiveAddress(readPlc);
                int low = readPlc(eff);
                int high = DataType == DataTypeEnum.Bin_32 ? readPlc(PlcAddressHelper.GetNextAddress(eff)) : 0;

                // Perform conversion and store it for child classes.
                // _lastNumericValue stays in the PLC's REFERENCE unit (mm, gr, C...)
                // so limit checks and formulas keep working in one consistent unit.
                double val = PlcValueConverter.ConvertToDouble(low, high, DataType, DataFormat, 0);
                if (Divisor != 0) val /= Divisor;
                _lastNumericValue = val;

                // UNIT CONVERSION: when a UnitType is set, the value is shown in the
                // unit currently chosen in Unit Settings, and the suffix follows it.
                // Previously UnitType was declared but never used, so changing the
                // unit had no visible effect anywhere.
                double shown = val;
                int decimals = DecimalPlaces;
                string suffix = Suffix;

                if (UnitType != UnitType.None)
                {
                    shown = UnitManager.ToDisplay(UnitType, val);
                    decimals = DecimalPlaces + UnitManager.GetActiveExtraDecimals(UnitType);
                    suffix = UnitManager.GetActiveSuffix(UnitType);
                }
                if (!ShowSuffix) suffix = "";

                string formatted = PlcValueConverter.FormatString(shown, DataType, DataFormat, decimals, Digits, FillZero);
                if (!string.IsNullOrEmpty(suffix)) formatted += " " + suffix;
                this.Text = formatted;
            }
            catch { this.Text = "Err"; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _descr?.Dispose();
                UnitManager.UnitChanged -= OnUnitChanged;
                LanguageManager.LanguageChanged -= OnLanguageChanged;
                // UNREGISTER, never dispose: the tooltip is the program's shared one.
                // Disposing it here killed every tooltip in the program the first
                // time any numeric field was disposed - the login's
                // NullReferenceException in ToolTip.CreateHandle, and pages whose
                // fields showed no tips at all.
                _toolTip?.SetToolTip(this, null);
            }
            base.Dispose(disposing);
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (!IsDisposed) ApplyTranslation();
        }

        protected string GetEffectiveAddress(Func<string, int> readPlc)
        {
            if (string.IsNullOrEmpty(Address)) return "";
            int offsetValue = 0;

            // ADDED NULL CHECK HERE: Safely verify AddressOffset exists before checking its Type
            if (AddressOffset != null)
            {
                if (AddressOffset.Type == LimitSourceType.Static) offsetValue = (int)AddressOffset.StaticValue;
                else if (AddressOffset.Type == LimitSourceType.Tag) offsetValue = readPlc(AddressOffset.TagAddress);
                else if (AddressOffset.Type == LimitSourceType.HmiVariable) offsetValue = HmiInternalVariables.Get(AddressOffset.TagAddress);
            }

            return PlcAddressHelper.OffsetAddress(Address, offsetValue);
        }
    }

    public class BaseNumericSorter : ExpandableObjectConverter
    {
        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
        {
            // Use TypeDescriptor to ensure we get all properties for sorting
            var props = TypeDescriptor.GetProperties(value, attributes);
            string[] order = { "PlcNumber", "Address", "DataType", "AddressOffset", "Divisor", "Digits", "DecimalPlaces", "FillZero", "DataFormat", "VisibilityConditions" };
            return props.Sort(order);
        }
    }
}
