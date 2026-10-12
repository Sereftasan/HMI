using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// THE MACHINE'S SPEED, as the old program's main control panel set it (its
    /// KabanTrackBar, 12 October: "take the Start, Pause, Cancel buttons and speed
    /// settings from the home pages"): − and + step through the old steps - 0, 1,
    /// 2, 4, 6, 8, 10, 15, 20, 30, 40, 50, 60, 70, 80, 90, 95, 100 % - and write the
    /// percentage to the PLC's word (FA2010: D408, "Çalışma Hız Yüzdesi"); the
    /// percentage in force is read back from it, so a value the PLC or another panel
    /// set is shown as it is.
    /// </summary>
    [ToolboxItem(true)]
    [TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class SpeedOverrideHMI : UserControl, IPlcBoundControl, ILocalizable
    {
        private readonly Button _down = new Button { Text = "−", Name = "speedDown", TabStop = false };
        private readonly Button _up = new Button { Text = "+", Name = "speedUp", TabStop = false };
        private readonly Label _value = new Label { Text = "%-", TextAlign = ContentAlignment.MiddleCenter, Name = "speedValue" };
        private readonly Label _caption = new Label { TextAlign = ContentAlignment.MiddleLeft, Name = "speedCaption" };
        private int? _current;
        private bool _softEnabled = true;

        /// <summary>The old track bar's steps, in percent.</summary>
        public static readonly int[] DefaultSteps = { 0, 1, 2, 4, 6, 8, 10, 15, 20, 30, 40, 50, 60, 70, 80, 90, 95, 100 };

        [Category("Device")] [Description("The machine's own PLC number: 1 = its main PLC.")]
        public int PlcNumber { get; set; } = 1;
        [Category("Device")] [Description("The word the speed is written to and read from, in percent (FA2010: D408).")]
        public string Address { get; set; } = "";
        [Category("Input")] [Description("The steps - and + go through, in percent (the old track bar's by default).")]
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

        public SpeedOverrideHMI()
        {
            BackColor = Color.White;
            Size = new Size(330, 54);
            _caption.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _value.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            foreach (var b in new[] { _down, _up })
            {
                b.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
                b.FlatStyle = FlatStyle.Flat;
                b.BackColor = Color.FromArgb(240, 241, 243);
                b.FlatAppearance.BorderColor = Color.FromArgb(160, 166, 176);
            }
            _down.Click += (s, e) => Step(-1);
            _up.Click += (s, e) => Step(+1);
            Controls.Add(_caption);
            Controls.Add(_down);
            Controls.Add(_value);
            Controls.Add(_up);
            ApplyTranslation();
            LanguageManager.LanguageChanged += OnLanguage;
            Layout += (s, e) => Arrange();
            Arrange();
        }

        private void OnLanguage(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnLanguage), sender, e); } catch { } return; }
            ApplyTranslation();
        }

        public void ApplyTranslation()
        {
            _caption.Text = LanguageManager.GetString(TranslationKey ?? "", FallbackText ?? "");
            Arrange();
        }

        /// <summary>The percentage shown (tests): null before the first read.</summary>
        [Browsable(false)]
        public int? Current { get { return _current; } }

        private void Arrange()
        {
            int h = ClientSize.Height, w = ClientSize.Width;
            // the caption as wide as its text, - and + square, the percentage between them
            int bw = Math.Max(30, Math.Min(h, w / 4));
            int capw = Math.Min(w / 3, TextRenderer.MeasureText(_caption.Text ?? "", _caption.Font).Width + 12);
            int vw = Math.Max(40, w - capw - 2 * bw);
            _caption.SetBounds(0, 0, capw, h);
            _down.SetBounds(capw, 0, bw, h);
            _value.SetBounds(capw + bw, 0, vw, h);
            _up.SetBounds(capw + bw + vw, 0, bw, h);
        }

        public void UpdateState(Func<string, int> lookup, Func<SystemCondition, bool> checkGlobal)
        {
            if (string.IsNullOrWhiteSpace(Address) || lookup == null) return;
            int v = lookup(Address);
            if (_current != v)
            {
                _current = v;
                _value.Text = "%" + v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            bool en = SecurityManager.HasAccess(RequiredSecurity) && !ConnectionMonitor.IsKnownDown(PlcNumber);
            if (en != _softEnabled)
            {
                _softEnabled = en;
                _down.Enabled = _up.Enabled = en;
            }
        }

        /// <summary>The step after (or before) the percentage in force.</summary>
        public int NextValue(int direction)
        {
            var steps = (Steps ?? DefaultSteps).OrderBy(s => s).ToArray();
            if (steps.Length == 0) return _current ?? 0;
            int now = _current ?? steps[0];
            if (direction > 0) { foreach (var s in steps) if (s > now) return s; return steps[steps.Length - 1]; }
            for (int i = steps.Length - 1; i >= 0; i--) if (steps[i] < now) return steps[i];
            return steps[0];
        }

        /// <summary>One step down (-1) or up (+1), written to the PLC.</summary>
        public bool Step(int direction)
        {
            if (!_softEnabled || string.IsNullOrWhiteSpace(Address)) return false;
            int target = NextValue(direction);
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
            _value.Text = "%" + target.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
