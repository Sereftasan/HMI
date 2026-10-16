using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// THE MACHINE'S SPEED in percent, as the old program's track bar set it - one
    /// compact control (14 October: "it should have a thing to drag left / right to
    /// lower and higher the value, and it should open the numerical display to set
    /// it directly to a specific value ... compact, the current one looks each part
    /// disconnected on the screen"):
    ///
    ///   [ Speed  ━━━━━━━━━●───────  %60 ]
    ///
    /// one frame: its caption, a slider dragged (or tapped) through the old track
    /// bar's steps - 0, 1, 2, 4, 6, 8, 10, 15, 20, 30 ... 90, 95, 100 % - and the
    /// percentage, which opens the keypad to type any value. Written when the
    /// slider is let go (not at every step of the drag) and when the keypad is
    /// closed with OK; the percentage in force is read back from the PLC's word, so
    /// a value set elsewhere is shown as it is. Left / right arrow keys step it.
    /// </summary>
    [ToolboxItem(true)]
    [TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class SpeedOverrideHMI : PercentSlider, IPlcBoundControl, ILocalizable
    {
        /// <summary>The old track bar's steps, in percent.</summary>
        public static readonly int[] DefaultSteps = { 0, 1, 2, 4, 6, 8, 10, 15, 20, 30, 40, 50, 60, 70, 80, 90, 95, 100 };

        [Category("Device")] [Description("The machine's own PLC number: 1 = its main PLC.")]
        public int PlcNumber { get; set; } = 1;
        [Category("Device")] [Description("The word the speed is written to and read from, in percent (FA2010: D408; the axes' manual speed: D409).")]
        public string Address { get; set; } = "";
        [Category("Input")] [Description("The steps the slider goes through, in percent (the old track bar's by default). The keypad takes any value between the first and the last.")]
        public int[] Steps { get; set; } = (int[])DefaultSteps.Clone();
        [Category("Security")] [Description("The lowest level allowed to change the speed.")]
        public SecurityLevel RequiredSecurity { get; set; } = SecurityLevel.Operator;
        [Category("Security")] [Description("true: every change goes to the log with the user's name.")]
        public bool LogAction { get; set; } = true;
        private string _key = "Lbl_Speed", _fallback = "Speed";
        [Category("Display")] [Description("The key of its caption (Lbl_Speed: Speed).")]
        public string TranslationKey { get { return _key; } set { _key = value; ApplyTranslation(); } }
        [Category("Display")] [Description("Its caption when the key has no translation.")]
        public string FallbackText { get { return _fallback; } set { _fallback = value; ApplyTranslation(); } }

        /// <summary>Asks for a value with the keypad (tests answer instead): the value now, the answer or null.</summary>
        internal static Func<SpeedOverrideHMI, int, int?> EnterValue;

        public SpeedOverrideHMI()
        {
            Size = new Size(248, 42);
            ApplyTranslation();
            LanguageManager.LanguageChanged += OnLanguage;
        }

        protected override Size DefaultSize { get { return new Size(248, 42); } }

        protected override int[] StepList { get { return Steps ?? DefaultSteps; } }
        protected override string CaptionText { get { return _caption; } }
        protected override bool Commit(int value) { return SetTo(value); }

        protected override int? AskTyped(int now, int min, int max)
        {
            if (EnterValue != null) return EnterValue(this, now);
            return base.AskTyped(now, min, max);
        }

        private void OnLanguage(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnLanguage), sender, e); } catch { } return; }
            ApplyTranslation();
        }

        private string _caption = "";

        public void ApplyTranslation()
        {
            _caption = LanguageManager.GetString(TranslationKey ?? "", FallbackText ?? "");
            if (!_tipWired)
            {
                _tipWired = true;
                UiStyle.HoverTip(UiStyle.Tips(), this, TipText);
            }
            Invalidate();
        }

        private bool _tipWired;

        /// <summary>What it is, its device, and how it is used - built as the tip shows.</summary>
        internal string TipText()
        {
            return _caption + (string.IsNullOrWhiteSpace(Address) ? "" : "\n" + PlcAddressHelper.StationHeader(PlcNumber) + PlcAddressHelper.Describe(PlcNumber, Address)) + "\n" +
                   (_softEnabled ? LanguageManager.GetString("Tip_SpeedSlider", "Drag the slider, or tap the percentage to type it.")
                                 : ConnectionMonitor.IsKnownDown(PlcNumber) ? ConnectionMonitor.ReasonFor(PlcNumber)
                                 : LanguageManager.GetString("Sys_ReqSec", "Requires Security Level:") + " " + RequiredSecurity);
        }

        /// <summary>The percentage shown (tests): null before the first read.</summary>
        [Browsable(false)]
        public int? Current { get { return _current; } }

        /// <summary>Whether it may be changed now (the level signed in, the PLC answering) - tests.</summary>
        [Browsable(false)]
        public bool CanChange { get { return _softEnabled; } }

        // ---- the PLC ----------------------------------------------------------------------

        public void UpdateState(Func<string, int> lookup, Func<SystemCondition, bool> checkGlobal)
        {
            if (string.IsNullOrWhiteSpace(Address) || lookup == null) return;
            int v = lookup(Address);
            if (_current != v) { _current = v; Invalidate(); }
            bool en = SecurityManager.HasAccess(RequiredSecurity) && !ConnectionMonitor.IsKnownDown(PlcNumber);
            if (en != _softEnabled)
            {
                _softEnabled = en;
                if (!en) CancelDrag();
                Invalidate();
            }
        }

        /// <summary>A percentage written to the PLC (the slider let go, the keypad's OK).</summary>
        public bool SetTo(int target)
        {
            if (!_softEnabled || string.IsNullOrWhiteSpace(Address)) return false;
            if (_current.HasValue && target == _current.Value) return false;
            var drv = PlcManager.Instance.GetDriverForWrite(PlcNumber);
            if (drv == null)
            {
                MessageBox.Show(this, PlcManager.Instance.DescribeAccess(PlcNumber), "PLC Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            int rc;
            using (Kaban.UI.Helpers.WriteSource.For(this, "speed " + target + "%"))
                rc = drv.Write(Address, target);
            if (rc != 0) return false;
            _current = target;
            Invalidate();
            if (LogAction) AuditLogger.Log("speed set to " + target + "% (" + Address + ", PLC " + PlcNumber + ").");
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) LanguageManager.LanguageChanged -= OnLanguage;
            base.Dispose(disposing);
        }
    }
}
