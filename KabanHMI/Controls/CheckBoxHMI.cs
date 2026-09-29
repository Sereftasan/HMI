using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// PLC-bound CHECKBOX: reflects one bit and writes it on click, with the same
    /// security gating, audit logging and attached-description support as the rest
    /// of the family. Ticking is reverted when the user lacks permission.
    /// </summary>

    // The Properties window in the order of the work (see PropertyOrder).
    [System.ComponentModel.TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class CheckBoxHMI : CheckBox
    {
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
        }

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

            int side = Math.Min(Width, Height) - 2;
            var box = new Rectangle((Width - side) / 2, (Height - side) / 2, side, side);

            using (var back = new SolidBrush(Color.White))
                g.FillRectangle(back, box);
            using (var pen = new Pen(Enabled ? Color.FromArgb(120, 128, 140)
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
                    Enabled ? Color.FromArgb(25, 28, 34) : Color.FromArgb(170, 176, 184),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyCornerRegion();
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
            drv.Write(Address, Checked ? 1 : 0);
            if (LogAction)
                AuditLogger.Log($"set bit {Address} (PLC {PlcNumber}) to {(Checked ? 1 : 0)} via checkbox.");
        }

        /// <summary>Scanner hook - reflects the PLC bit without writing back.</summary>
        private readonly ToolTip _tip = UiStyle.Tips();
        private bool _tipBuilt;

        /// <summary>
        /// The device and what it is, on hover.
        ///
        /// A bare checkbox in a column says nothing about which bit it drives, and
        /// "which device is that?" is the first question anyone asks of a control
        /// that looks wrong. Built once the address is known rather than only when
        /// the value changes, so it is there before the first scan arrives.
        /// </summary>
        public void RefreshTooltip()
        {
            _tipBuilt = true;
            if (string.IsNullOrEmpty(Address)) { _tip.SetToolTip(this, ""); return; }
            // Device only. A caption here repeats the row and the column heading.
            string tip = PlcAddressHelper.StationHeader(PlcNumber) + PlcAddressHelper.Describe(PlcNumber, Address);
            if (!string.IsNullOrWhiteSpace(AlertAddress))
                tip += "\n" + LanguageManager.GetString("Tip_AlertBit", "Asked for by") + ": " +
                       PlcAddressHelper.Describe(PlcNumber, AlertAddress.Trim()) +
                       (_alert ? "  (" + LanguageManager.GetString("Tip_AlertOn", "now") + ")" : "");
            _tip.SetToolTip(this, tip);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_tipBuilt) RefreshTooltip();
        }

        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            if (!_tipBuilt) RefreshTooltip();
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

