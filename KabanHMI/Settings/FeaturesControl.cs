using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// HMI SETTINGS > FEATURES (30 October: "We need to make the Industry 4 service and touchscreen
    /// mode optional, and we need to add some buttons to active and passive them on HMI setting").
    ///
    /// The HMI's optional parts, each with On / Off and a line on what it does, and what it does now:
    ///   - the Industry 4 logging in the HMI: the machines' logs taken from their PLCs, the HMI's own
    ///     log beside them in the same database (off: the Windows service logs, the HMI's log is in
    ///     its files);
    ///   - the touch screen: the mouse / finger button on the top bar and the "Screen used with"
    ///     preference (off: a mouse).
    /// Both off until an Admin switches them on; taken at once, written in the log.
    /// </summary>
    public sealed class FeaturesControl : UserControl, IRebuildForLanguage
    {
        object IRebuildForLanguage.Place { get { return null; } }
        void IRebuildForLanguage.ReturnTo(object place) { }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private readonly FlowLayoutPanel _flow = new FlowLayoutPanel();
        private readonly Dictionary<string, Button[]> _switches = new Dictionary<string, Button[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Label _loggingNow = new Label { Name = "loggingNow" };
        private readonly Label _touchNow = new Label { Name = "touchNow" };
        private readonly Timer _clock = new Timer { Interval = 1000 };
        private readonly EventHandler _onChanged;

        private const int RowW = 760;

        public FeaturesControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;
            _flow.Dock = DockStyle.Fill;
            _flow.FlowDirection = FlowDirection.TopDown;
            _flow.WrapContents = false;
            _flow.AutoScroll = true;
            _flow.Padding = new Padding(16, 10, 16, 16);
            Controls.Add(_flow);

            Intro();
            Feature(HmiFeatures.Industry4Logging, "\U0001F4E5", T("Ftr_Logging", "Industry 4 logging in the HMI"),
                    T("Ftr_LoggingText", "The HMI takes each machine's states, events and alarms from its PLC into the machine's log database - the Industry 4 service's job - and keeps its own log (sign-ins, writes, settings) in the same database. Off: the Industry 4 service logs, the HMI's log is in its files. Each machine's PLC log area: Machine Parameters > Alarms and Logs."),
                    T("Ftr_LoggingNote", "While the Industry 4 service runs on this PC the HMI waits: stop it (or uninstall it) once the HMI logs."),
                    _loggingNow);
            Feature(HmiFeatures.TouchScreen, TouchEmoji, T("Ftr_Touch", "Touch screen"),
                    T("Ftr_TouchText", "The screen may be used by touch: the mouse / finger button on the top bar switches between them, and Users and Preferences has 'Screen used with' for everyone or a user. By touch: larger menus, rows and buttons, a long press for a menu, a finger drag scrolls, a keyboard on the screen. Off: a mouse, neither shown."),
                    null, _touchNow);

            _onChanged = (s, e) => { try { if (IsHandleCreated && InvokeRequired) BeginInvoke((Action)Fill); else Fill(); } catch { } };
            HmiFeatures.Changed += _onChanged;
            ViewPreferences.Changed += _onChanged;
            _clock.Tick += (s, e) => ShowNow();
            VisibleChanged += (s, e) => { if (Visible) { Fill(); _clock.Start(); } else _clock.Stop(); };
            Disposed += (s, e) =>
            {
                HmiFeatures.Changed -= _onChanged;
                ViewPreferences.Changed -= _onChanged;
                _clock.Dispose();
            };
            Fill();
        }

        private static string TouchEmoji { get { return Kaban.UI.Helpers.TouchInput.TouchEmoji; } }

        private void Intro()
        {
            _flow.Controls.Add(new Label
            {
                Text = T("Ftr_Intro", "The HMI's optional parts: each one is switched on for this panel, for everyone, and taken at once."),
                AutoSize = false, Width = RowW, Height = 26, ForeColor = Color.FromArgb(90, 96, 106), Margin = new Padding(0, 0, 0, 6)
            });
        }

        /// <summary>A feature: its name and On / Off on one line, what it does under it, what it does now.</summary>
        private void Feature(string key, string icon, string name, string text, string note, Label now)
        {
            var head = new Panel { Width = RowW, Height = 40, Margin = new Padding(0, 14, 0, 2), Name = "feature:" + key };
            var title = new Label
            {
                Text = name, AutoSize = false, Location = new Point(0, 0), Size = new Size(RowW - 230, 40),
                Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(34, 0, 0, 0)
            };
            title.Paint += (s, e) => EmojiArt.Draw(e.Graphics, new Rectangle(2, (title.Height - 26) / 2, 26, 26), icon, true, 26);
            head.Controls.Add(title);
            var on = Switch(T("Ftr_On", "On"), true);
            var off = Switch(T("Ftr_Off", "Off"), false);
            on.Name = key + ":on";
            off.Name = key + ":off";
            on.Location = new Point(RowW - 220, 4);
            off.Location = new Point(RowW - 110, 4);
            on.Click += (s, e) => HmiFeatures.Set(key, true);
            off.Click += (s, e) => HmiFeatures.Set(key, false);
            head.Controls.Add(on);
            head.Controls.Add(off);
            _switches[key] = new[] { on, off };
            _flow.Controls.Add(head);

            _flow.Controls.Add(Wrapped(text, Color.FromArgb(40, 46, 56)));
            if (!string.IsNullOrEmpty(note)) _flow.Controls.Add(Wrapped(note, Color.FromArgb(150, 90, 0)));
            now.AutoSize = false;
            now.Width = RowW;
            now.Height = 22;
            now.ForeColor = Color.FromArgb(30, 100, 190);
            now.Margin = new Padding(0, 2, 0, 4);
            _flow.Controls.Add(now);
            var rule = new Label { AutoSize = false, Width = RowW, Height = 1, BackColor = UiStyle.GridRule, Margin = new Padding(0, 8, 0, 0) };
            _flow.Controls.Add(rule);
        }

        private Label Wrapped(string text, Color ink)
        {
            var l = new Label { Text = text, AutoSize = false, Width = RowW, ForeColor = ink, Margin = new Padding(0, 2, 0, 2) };
            l.Height = TextRenderer.MeasureText(text, UiStyle.Body, new Size(RowW, 0), TextFormatFlags.WordBreak).Height + 4;
            return l;
        }

        private static Button Switch(string text, bool on)
        {
            var b = UiStyle.TextButton(text, 100);
            b.FlatStyle = FlatStyle.Flat;
            b.Tag = on;
            return b;
        }

        /// <summary>The buttons as the features are: the one in force filled (green On, grey Off).</summary>
        private void Fill()
        {
            if (IsDisposed) return;
            foreach (var kv in _switches)
            {
                bool isOn = HmiFeatures.IsOn(kv.Key);
                Look(kv.Value[0], isOn, Color.FromArgb(46, 140, 70));
                Look(kv.Value[1], !isOn, Color.FromArgb(110, 116, 124));
            }
            ShowNow();
        }

        private static void Look(Button b, bool chosen, Color fill)
        {
            b.BackColor = chosen ? fill : Color.White;
            b.ForeColor = chosen ? Color.White : Color.FromArgb(40, 46, 56);
            b.Font = chosen ? UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold) : UiStyle.Body;
            b.FlatAppearance.BorderColor = chosen ? fill : Color.FromArgb(196, 201, 209);
        }

        /// <summary>What each does now: the machines' loggers, the way the screen is used.</summary>
        private void ShowNow()
        {
            if (IsDisposed) return;
            if (!HmiFeatures.LoggingOn) _loggingNow.Text = T("Ftr_LoggingOff", "Now: the Industry 4 service logs (when it is installed); the HMI's log is in its files.");
            else
            {
                var lines = new List<string>();
                foreach (var m in Kaban.UI.Helpers.MachineLinks.WithLogs())
                {
                    var l = Kaban.UI.Dashboard.MachineLoggers.Of(m.ModuleId);
                    lines.Add(m.Name + ": " + (l != null ? l.Status : T("Plg_NotStarted", "Not running in this program (it starts with the main window).")));
                }
                if (lines.Count == 0) lines.Add(T("Ftr_NoLogMachine", "No machine of this panel keeps a log database (its Dashboard settings)."));
                _loggingNow.Text = string.Join("\n", lines);
            }
            _loggingNow.Height = Math.Max(22, TextRenderer.MeasureText(_loggingNow.Text, UiStyle.Body, new Size(RowW, 0), TextFormatFlags.WordBreak).Height + 4);
            _touchNow.Text = !HmiFeatures.TouchOn ? T("Ftr_TouchOff", "Now: used with a mouse.")
                           : ViewPreferences.TouchMode ? T("Ftr_TouchNowTouch", "Now: used by touch.")
                           : T("Ftr_TouchNowMouse", "Now: used with a mouse - the button on the top bar switches it.");
        }

        /// <summary>The line under a feature (tests).</summary>
        public string NowText(string feature) { return feature == HmiFeatures.TouchScreen ? _touchNow.Text : _loggingNow.Text; }

        /// <summary>A feature's On or Off button (tests).</summary>
        public Button SwitchOf(string feature, bool on)
        {
            Button[] b;
            return _switches.TryGetValue(feature, out b) ? b[on ? 0 : 1] : null;
        }
    }

    /// <summary>HMI Settings > Features: an Admin's (they change how the panel works for everyone).</summary>
    public class FeaturesPage : ISettingsPage
    {
        public string TitleKey { get { return "Set_Features"; } }
        public string TitleFallback { get { return "Features"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Admin; } }
        public UserControl GetControl() { return new FeaturesControl(); }
    }
}
