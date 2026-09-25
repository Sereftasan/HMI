using System;
using System.ComponentModel;
using Kaban.Core.Enums;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// Container for interrelated controls, drawn like FunctionUnitHMI's frame:
    /// a rounded border with the caption straddling the top border line.
    ///
    /// The caption uses a NORMAL font by default (bold looked too prominent).
    /// Setting the Emoji property prefixes the caption with it AND switches the
    /// caption to bold - the way to make a group deliberately stand out.
    /// </summary>
    [Designer("System.Windows.Forms.Design.ParentControlDesigner, System.Design", typeof(System.ComponentModel.Design.IDesigner))]
    public class GroupBoxHMI : Panel, IHmiControl
    {
        private string _titleText = "Group";
        private string _titleTextKey = "";
        private string _emoji = "";

        public GroupBoxHMI()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Padding = new Padding(10, 22, 10, 8);   // room for the caption's inner half
            Size = new Size(360, 120);
            LanguageManager.LanguageChanged += OnLanguageChanged;
            VisibilityConditions = SystemCondition.None;
            EnableConditions = SystemCondition.None;
            EnableAddress = "";
            VisibleAddress = "";
            PlcNumber = 1;
        }

        /// <summary>
        /// Applies the conditions to the group and, through it, to every control
        /// inside. Disabling the CONTAINER is what makes this worth having: it
        /// takes one setting to lock a whole option's worth of controls, and none
        /// of them has to know the condition exists.
        /// </summary>
        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            bool visible = checkGlobal == null || checkGlobal(VisibilityConditions);
            if (visible && !string.IsNullOrEmpty(VisibleAddress) && readPlc != null)
                visible = readPlc(VisibleAddress) != 0;
            if (Visible != visible) Visible = visible;
            if (!visible) return;

            bool enabled = checkGlobal == null || checkGlobal(EnableConditions);
            if (enabled && !string.IsNullOrEmpty(EnableAddress) && readPlc != null)
                enabled = readPlc(EnableAddress) != 0;
            if (Enabled != enabled) Enabled = enabled;
        }

        /// <summary>
        /// The caption. A translation KEY if one exists for it, otherwise the text
        /// itself.
        ///
        /// There used to be two properties, TitleText and TitleTextKey, and you
        /// were right that it made no sense: the second was only ever the fallback
        /// for the first, which is what LanguageManager already does. One property
        /// is enough - look the string up, and if no language has it, draw it as
        /// written. That also means a caption typed straight into the designer
        /// works with no translation file at all, and starts being translated the
        /// moment somebody adds a row for it.
        /// </summary>
        [Category("   Appearance")]
        [Description("Caption. Used as a translation key; shown as-is when not translated.")]
        public string TitleTextKey { get { return _titleTextKey; } set { _titleTextKey = value ?? ""; Invalidate(); } }

        /// <summary>
        /// Kept so existing designer files keep compiling; it forwards to
        /// TitleTextKey. New pages should set TitleTextKey only.
        /// </summary>
        [Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public string TitleText { get { return _titleTextKey; } set { TitleTextKey = value; } }

        /// <summary>
        /// Hides the whole group - and everything in it - when the condition is
        /// not met. A machine without a given option should not show its settings
        /// greyed out; it should not show them.
        /// </summary>
        [Category("  Conditions")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        public SystemCondition VisibilityConditions { get; set; }

        /// <summary>
        /// Greys the group and everything inside it. Use this where the controls
        /// should stay VISIBLE - so the operator can see the setting exists - but
        /// must not be changed yet, such as while the machine is running.
        /// </summary>
        [Category("  Conditions")]
        [TypeConverter(typeof(Kaban.UI.Design.ConditionTextConverter))]
        [Editor("Kaban.UI.Design.UnifiedConditionEditor, Kaban.UI", typeof(System.Drawing.Design.UITypeEditor))]
        public SystemCondition EnableConditions { get; set; }

        /// <summary>Bit that must be ON for the group to be enabled. Optional.</summary>
        [Category("  Conditions")]
        public string EnableAddress { get; set; }

        /// <summary>Bit that must be ON for the group to be visible. Optional.</summary>
        [Category("  Conditions")]
        public string VisibleAddress { get; set; }

        [Category("  PLC")] public int PlcNumber { get; set; }

        /// <summary>Not used: the group binds through its condition addresses.</summary>
        [Browsable(false)]
        public string Address { get; set; }

        [Category("   Appearance")]
        [Description("Optional emoji/symbol shown before the caption. When set, the caption becomes bold to stand out.")]
        public string Emoji { get { return _emoji; } set { _emoji = value ?? ""; Invalidate(); } }

        private void OnLanguageChanged(object s, EventArgs e) { if (!IsDisposed) Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // The key IS the fallback: if no language has a row for it, the text
            // as typed is shown. LanguageManager already falls back to English
            // when the selected language has no entry.
            string caption = LanguageManager.GetString(_titleTextKey, _titleTextKey);
            if (!string.IsNullOrEmpty(_emoji)) caption = _emoji + " " + caption;

            // Normal font by default; bold only when an emoji marks it important.
            using (var font = new Font("Segoe UI", UiScale.Pt(9.5F),
                       string.IsNullOrEmpty(_emoji) ? FontStyle.Regular : FontStyle.Bold))
            {
                SizeF ts = g.MeasureString(caption, font);
                int capX = 12;
                int capH = (int)Math.Ceiling(ts.Height);
                int borderY = capH / 2;                 // border runs through caption middle

                // rounded border
                var rect = new Rectangle(0, borderY, Width - 1, Height - borderY - 1);
                // Stays neutral on hover ON PURPOSE: the blue border marks the
                // control the click will land on, and a group box is only the box
                // around them. Outlining it competed with the control inside.
                using (var gp = Rounded(rect, 4))
                using (var pen = new Pen(Color.FromArgb(170, 176, 184)))
                    g.DrawPath(pen, gp);

                // caption background gap + text ON the border line
                using (var bg = new SolidBrush(BackColor))
                    g.FillRectangle(bg, capX - 4, 0, ts.Width + 8, capH);
                using (var fg = new SolidBrush(Color.FromArgb(40, 44, 52)))
                    g.DrawString(caption, font, fg, capX, 0);
            }
        }

        private static GraphicsPath Rounded(Rectangle r, int rad)
        {
            var gp = new GraphicsPath();
            gp.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            gp.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            gp.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            gp.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            gp.CloseFigure();
            return gp;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) LanguageManager.LanguageChanged -= OnLanguageChanged;
            base.Dispose(disposing);
        }
    }
}

