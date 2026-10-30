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
    /// HMI SETTINGS > OPTIONS (30 October: "We need to make the Industry 4 service and touchscreen
    /// mode optional, and we need to add some buttons to active and passive them on HMI setting";
    /// then: "rename 'Features' page name in 'HMI setting' to 'Options' to match to the one on machine
    /// settings ... redesign the items (selection toggles) as the attached image as well and remove the
    /// description notes for the items (we should put them into the manuals instead)").
    ///
    /// The HMI's optional parts, a row each as a browser's settings: the name on the left with what it
    /// does now under it in grey, an on / off switch at the right end, a thin line between the rows
    /// (what each one does: the manual, 9b):
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
        private readonly Dictionary<string, ToggleSwitch> _switches = new Dictionary<string, ToggleSwitch>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Panel> _rows = new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase);
        private readonly Label _loggingNow = new Label { Name = "loggingNow" };
        private readonly Label _touchNow = new Label { Name = "touchNow" };
        private readonly Timer _clock = new Timer { Interval = 1000 };
        private readonly EventHandler _onChanged;
        private bool _filling;

        /// <summary>A row's width, its least height, the room its switch takes at the right end.</summary>
        internal const int RowW = 760, RowH = 56, SwitchRoom = 64;
        private static readonly Color Ink = Color.FromArgb(32, 33, 36), Grey = Color.FromArgb(95, 99, 104);

        public FeaturesControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;
            _flow.Dock = DockStyle.Fill;
            _flow.FlowDirection = FlowDirection.TopDown;
            _flow.WrapContents = false;
            _flow.AutoScroll = true;
            _flow.Padding = new Padding(16, 6, 16, 16);
            Controls.Add(_flow);

            Feature(HmiFeatures.Industry4Logging, T("Ftr_Logging", "Industry 4 logging in the HMI"), _loggingNow);
            Feature(HmiFeatures.TouchScreen, T("Ftr_Touch", "Touch screen"), _touchNow);

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

        /// <summary>A feature's row: its name, what it does now under it, its switch at the right end, a line under it.</summary>
        private void Feature(string key, string name, Label now)
        {
            var row = new Panel { Width = RowW, Height = RowH, Margin = Padding.Empty, Name = "feature:" + key, Cursor = Cursors.Hand };
            var title = new Label
            {
                Name = "title", Text = name, AutoSize = false, Location = new Point(4, 9), Size = new Size(RowW - SwitchRoom - 8, 22),
                ForeColor = Ink, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
            };
            now.AutoSize = false;
            now.Location = new Point(4, 31);
            now.Width = RowW - SwitchRoom - 8;
            now.Height = 20;
            now.ForeColor = Grey;
            var sw = new ToggleSwitch { Name = key + ":switch", AccessibleName = name, Size = new Size(48, 28) };
            sw.CheckedChanged += (s, e) => { if (!_filling) HmiFeatures.Set(key, sw.Checked); };
            // the whole row turns it over, as a browser's settings row
            EventHandler click = (s, e) => sw.Toggle();
            row.Click += click; title.Click += click; now.Click += click;
            title.Cursor = now.Cursor = Cursors.Hand;
            row.Controls.Add(title);
            row.Controls.Add(now);
            row.Controls.Add(sw);
            row.Paint += (s, e) =>
            {
                using (var pen = new Pen(UiStyle.GridRule)) e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
            };
            row.Resize += (s, e) => Place(row);
            _switches[key] = sw;
            _rows[key] = row;
            _flow.Controls.Add(row);
            Place(row);
        }

        /// <summary>The row as tall as what it says now (a line per machine for the logging); its switch in the middle of its height.</summary>
        private static void Place(Panel row)
        {
            var sw = row.Controls.OfType<ToggleSwitch>().FirstOrDefault();
            var now = row.Controls.OfType<Label>().FirstOrDefault(l => l.Name != "title");
            if (sw == null || now == null) return;
            int nowH = string.IsNullOrEmpty(now.Text) ? 0
                : Math.Max(20, TextRenderer.MeasureText(now.Text, now.Font, new Size(now.Width, 0), TextFormatFlags.WordBreak).Height + 2);
            now.Height = nowH;
            int h = Math.Max(RowH, 31 + nowH + 7);
            if (row.Height != h) row.Height = h;
            sw.Location = new Point(row.Width - sw.Width - 4, (row.Height - sw.Height) / 2);
            row.Invalidate();
        }

        /// <summary>The switches as the features are.</summary>
        private void Fill()
        {
            if (IsDisposed) return;
            _filling = true;
            try { foreach (var kv in _switches) kv.Value.Checked = HmiFeatures.IsOn(kv.Key); }
            finally { _filling = false; }
            ShowNow();
        }

        /// <summary>What each does now: the machines' loggers, the way the screen is used.</summary>
        private void ShowNow()
        {
            if (IsDisposed) return;
            if (!HmiFeatures.LoggingOn) _loggingNow.Text = T("Ftr_LoggingOff", "Now: the Industry 4 service logs (when it is installed) - the HMI's log is in its files.");
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
            _touchNow.Text = !HmiFeatures.TouchOn ? T("Ftr_TouchOff", "Now: used with a mouse.")
                           : ViewPreferences.TouchMode ? T("Ftr_TouchNowTouch", "Now: used by touch.")
                           : T("Ftr_TouchNowMouse", "Now: used with a mouse - the button on the top bar switches it.");
            foreach (var row in _rows.Values) Place(row);
        }

        /// <summary>The grey line under a feature (tests).</summary>
        public string NowText(string feature) { return feature == HmiFeatures.TouchScreen ? _touchNow.Text : _loggingNow.Text; }

        /// <summary>A feature's switch (tests).</summary>
        public ToggleSwitch SwitchOf(string feature)
        {
            ToggleSwitch sw;
            return _switches.TryGetValue(feature, out sw) ? sw : null;
        }

        /// <summary>A feature's row (tests).</summary>
        public Panel RowOf(string feature)
        {
            Panel p;
            return _rows.TryGetValue(feature, out p) ? p : null;
        }
    }

    /// <summary>
    /// HMI Settings > Options: an Admin's (they change how the panel works for everyone). Named as
    /// Machine Parameters' Options tab, its translation the same row (30 October).
    /// </summary>
    public class FeaturesPage : ISettingsPage
    {
        public string TitleKey { get { return "Tab_Options"; } }
        public string TitleFallback { get { return "Options"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Admin; } }
        public UserControl GetControl() { return new FeaturesControl(); }
    }
}
