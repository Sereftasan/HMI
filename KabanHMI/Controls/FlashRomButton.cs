using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// THE FLASH ROM BUTTON OF AN AXIS SETTINGS PAGE (23 October: "We need to have a button on the
    /// axis setting page (visible when there is any parameter changed that needs flash ROM write),
    /// the user click it after changing the parameters (the button should blink as well to warn the
    /// user to make the user not to forget clicking)").
    ///
    /// Beside the page's Motion monitor list: shown while a simple motion module of the page's axes
    /// has a parameter written since its flash ROM was (MotionFlash), blinking; its tooltip names
    /// what was changed. A click writes each such module's flash ROM (Un\G5900 = 1).
    /// </summary>
    public class FlashRomButton : Button
    {
        private readonly Func<IList<KeyValuePair<int, int>>> _modules;
        private readonly Timer _blink = new Timer { Interval = 500 };
        private readonly EventHandler _onChanged;
        private bool _lit;

        public static readonly Color BlinkBack = Color.FromArgb(220, 53, 69);

        private static string T(string key, string en) { return LanguageManager.GetString(key, en); }

        /// <param name="modules">The page's modules: each axis's station and module number.</param>
        public FlashRomButton(Func<IList<KeyValuePair<int, int>>> modules)
        {
            _modules = modules;
            Name = "flashRomButton";
            Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);
            Height = UiScale.Px(28);
            Margin = new Padding(UiScale.Px(10), UiScale.Px(4), 0, 0);
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderColor = BlinkBack;
            FlatAppearance.BorderSize = 2;
            UseVisualStyleBackColor = false;
            BackColor = Color.White;
            ForeColor = BlinkBack;
            Visible = false;
            _blink.Tick += (s, e) => { _lit = !_lit; Shade(); };
            _onChanged = (s, e) =>
            {
                if (IsDisposed) return;
                if (InvokeRequired) { try { BeginInvoke((Action)UpdateState); } catch { } return; }
                UpdateState();
            };
            MotionFlash.PendingChanged += _onChanged;
            Disposed += (s, e) =>
            {
                MotionFlash.PendingChanged -= _onChanged;
                _blink.Stop();
                _blink.Dispose();
            };
            UpdateState();
        }

        /// <summary>The page's modules with a change waiting for their flash ROM.</summary>
        public IList<KeyValuePair<int, int>> Waiting
        {
            get
            {
                var all = _modules == null ? null : _modules();
                return (all ?? new List<KeyValuePair<int, int>>()).Distinct().Where(m => MotionFlash.IsChanged(m.Key, m.Value)).ToList();
            }
        }

        /// <summary>Lit (red, white words) or not (white, red words) - a test reads it.</summary>
        public bool Lit { get { return _lit; } }

        /// <summary>Shown and blinking while a module of the page waits; its words and tooltip say which and what.</summary>
        public void UpdateState()
        {
            var waiting = Waiting;
            bool show = waiting.Count > 0;
            if (show)
            {
                Text = "💾 " + (waiting.Count == 1
                    ? string.Format(T("Btn_FlashRomOne", "Write module {0}'s flash ROM"), waiting[0].Value)
                    : T("Btn_FlashRomMany", "Write the modules' flash ROM"));
                Width = TextRenderer.MeasureText(Text, Font).Width + UiScale.Px(28);
                var lines = new List<string> { T("Tip_FlashRom", "Parameters changed since the module's flash ROM was written - kept only until the power is switched off:") };
                foreach (var m in waiting)
                {
                    lines.Add(string.Format(T("Tip_FlashRomModule", "Module {0}:"), m.Value));
                    lines.AddRange(MotionFlash.ChangesOf(m.Key, m.Value).Select(c => "   " + c));
                }
                lines.Add(T("Tip_FlashRomClick", "Click: the flash ROM written (Un\\G5900 = 1)."));
                UiStyle.SetTip(UiStyle.Tips(), this, string.Join("\r\n", lines));
                if (!_blink.Enabled) { _lit = true; _blink.Start(); }
            }
            else
            {
                _blink.Stop();
                _lit = false;
            }
            Shade();
            if (Visible != show)
            {
                Visible = show;
                var bar = Parent;
                if (bar != null && show) bar.Width = Math.Max(bar.Width, Right + UiScale.Px(8));
            }
        }

        private void Shade()
        {
            BackColor = _lit ? BlinkBack : Color.White;
            ForeColor = _lit ? Color.White : BlinkBack;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            foreach (var m in Waiting)
                MotionFlash.Write(FindForm(), m.Key, m.Value);
            UpdateState();
        }
    }
}
