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
    /// <summary>HMI Settings > View Preferences.</summary>
    public class ViewPreferencesPage : ISettingsPage
    {
        public string TitleKey => "Set_ViewPrefs";
        public string TitleFallback => "View Preferences";
        // Anyone signed in may set their own; everyone's needs Maintenance (see the control).
        public SecurityLevel RequiredLevel => SecurityLevel.Operator;
        public UserControl GetControl() => new ViewPreferencesControl();
    }

    /// <summary>
    /// HOW THE SCREEN SHOWS THINGS, for everyone or for one user (ViewPreferences):
    /// a combo per preference, grouped as Captions, Numbers and dates, Tables,
    /// Sign-in and alerts. For a user, each one may follow everyone's ("As for
    /// everyone") or be their own - taken when they sign in, as their Screen scale.
    ///
    /// Who may change what: their own, anyone signed in; everyone's, Maintenance
    /// and up; another user's, an Admin.
    /// </summary>
    public class ViewPreferencesControl : UserControl
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private const string Everyone = "\u0001everyone";
        private readonly ComboBox _whose = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Panel _body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
        private readonly Dictionary<string, ComboBox> _combos = new Dictionary<string, ComboBox>();
        private readonly Dictionary<string, Label> _samples = new Dictionary<string, Label>();
        private readonly DirtyTracker _dirty = new DirtyTracker();
        private readonly Label _note = new Label { AutoSize = true, ForeColor = Color.FromArgb(110, 116, 124) };
        private Button _save;
        private bool _filling;

        /// <summary>For the tests: the combo of a preference, the "whose" combo, Save.</summary>
        public ComboBox ComboOf(string key) { ComboBox c; return _combos.TryGetValue(key, out c) ? c : null; }
        public ComboBox Whose { get { return _whose; } }
        public Button SaveButton { get { return _save; } }

        public ViewPreferencesControl()
        {
            BackColor = Color.White;
            Font = UiStyle.Body;
            var tips = UiStyle.Tips();

            var bar = new ToolStrip
            {
                Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, AutoSize = false, Height = 40,
                BackColor = Color.White, Renderer = new ToolStripProfessionalRenderer()
            };
            Action<Control> add = c => bar.Items.Add(new ToolStripControlHost(c) { AutoSize = false, Size = c.Size, Margin = new Padding(2, 4, 2, 0) });
            _save = UiStyle.ToolButton("\uD83D\uDCBE", T("Tip_ViewSave", "Keep these preferences"), tips, (s, e) => Save());
            add(_save);
            _dirty.Guard(_save);
            add(UiStyle.ToolButton("\u21BA", T("Tip_ViewRevert", "Throw the changes away and show the kept preferences"), tips, (s, e) => Fill()));
            bar.Items.Add(new ToolStripLabel(UiStyle.Caption(T("VP_Whose", "Preferences for"))) { Margin = new Padding(16, 4, 4, 0), Font = UiStyle.Body });
            _whose.Width = 240;
            add(_whose);
            // Another person's preferences: an unsaved change of the one shown is dropped.
            _whose.SelectedIndexChanged += (s, e) => { if (!_filling) Fill(); };

            Controls.Add(_body);
            Controls.Add(bar);
            BuildRows();
            FillWhose();
            Fill();
            SecurityManager.UserChanged += OnUserChanged;
            Disposed += (s, e) => SecurityManager.UserChanged -= OnUserChanged;
        }

        private void OnUserChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new EventHandler(OnUserChanged), sender, e); return; }
            FillWhose();
            Fill();
        }

        // ---- the rows -----------------------------------------------------------------

        private static readonly string[][] Sections =
        {
            new[] { "Captions", "VP_SecCaptions", "Captions" },
            new[] { "Numbers", "VP_SecNumbers", "Numbers and dates" },
            new[] { "Tables", "VP_SecTables", "Tables" },
            new[] { "SignIn", "VP_SecSignIn", "Sign-in and alerts" }
        };

        /// <summary>A preference's name on the page.</summary>
        internal static string NameOf(string key)
        {
            switch (key)
            {
                case ViewPreferences.CaptionAlign: return T("VP_CaptionAlign", "Field captions");
                case ViewPreferences.AxisNameAlign: return T("VP_AxisNameAlign", "Axis names (Axis Settings)");
                case ViewPreferences.DecimalSeparator: return T("VP_DecimalSeparator", "Decimal separator");
                case ViewPreferences.DateFormat: return T("VP_DateFormat", "Date");
                case ViewPreferences.TimeFormat: return T("VP_TimeFormat", "Time");
                case ViewPreferences.TableRows: return T("VP_TableRows", "Table rows");
                case ViewPreferences.TableFont: return T("VP_TableFont", "Table text");
                case ViewPreferences.StartPage: return T("VP_StartPage", "After sign-in");
                case ViewPreferences.AlarmSound: return T("VP_AlarmSound", "Sound for a new alarm");
                case ViewPreferences.Blinking: return T("VP_Blinking", "Attention marks");
                case ViewPreferences.TooltipDelay: return T("VP_TooltipDelay", "Tooltips appear after");
                default: return key;
            }
        }

        /// <summary>A value's name on the page.</summary>
        internal static string ValueText(string key, string value)
        {
            switch (key + "." + value)
            {
                case "CaptionAlign.Right": return T("VP_CapRight", "Right, against the field");
                case "CaptionAlign.Left": return T("VP_CapLeft", "Left, in a column");
                case "AxisNameAlign.Right": return T("VP_Right", "Right");
                case "AxisNameAlign.Left": return T("VP_Left", "Left");
                case "DecimalSeparator.Windows": return T("VP_DecWindows", "As Windows");
                case "DecimalSeparator.Point": return T("VP_DecPoint", "Point") + "  (1234.56)";
                case "DecimalSeparator.Comma": return T("VP_DecComma", "Comma") + "  (1234,56)";
                case "DateFormat.Iso": return "2026-10-02";
                case "DateFormat.European": return "02.10.2026";
                case "DateFormat.Us": return "10/02/2026";
                case "TimeFormat.H24": return T("VP_H24", "24 hours") + "  (14:31)";
                case "TimeFormat.H12": return T("VP_H12", "12 hours") + "  (2:31 PM)";
                case "TableRows.Compact": case "TableFont.Small": return value == "Small" ? T("VP_Small", "Small") : T("VP_Compact", "Compact");
                case "TableRows.Normal": case "TableFont.Normal": return T("VP_Normal", "Normal");
                case "TableRows.Large": case "TableFont.Large": return T("VP_Large", "Large");
                case "StartPage.Stay": return T("VP_Stay", "Stay where the screen is");
                case "StartPage.Home": return T("VP_Home", "Home");
                case "StartPage.Last": return T("VP_Last", "My last page");
                case "AlarmSound.Off": return T("VP_Off", "Off");
                case "AlarmSound.On": return T("VP_On", "On");
                case "Blinking.On": return T("VP_Blink", "Blink");
                case "Blinking.Off": return T("VP_Steady", "Steady");
                case "TooltipDelay.Short": return T("VP_Short", "Short") + "  (0.4 s)";
                case "TooltipDelay.Normal": return T("VP_Normal", "Normal") + "  (1 s)";
                case "TooltipDelay.Long": return T("VP_Long", "Long") + "  (2 s)";
                default: return value;
            }
        }

        private sealed class Choice
        {
            public string Value, Text;
            public override string ToString() { return Text; }
        }

        private void BuildRows()
        {
            int y = 12;
            foreach (var sec in Sections)
            {
                var head = new Label
                {
                    Text = T(sec[1], sec[2]), AutoSize = false, Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold),
                    ForeColor = Color.FromArgb(40, 46, 56), Location = new Point(16, y), Size = new Size(560, 24)
                };
                _body.Controls.Add(head);
                y += 30;
                foreach (var p in ViewPreferences.All.Where(x => x.Group == sec[0] && !x.Hidden))
                {
                    var caption = UiStyle.CaptionLabel(NameOf(p.Key), 16, y + 2, 260);
                    _body.Controls.Add(caption);
                    var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(282, y), Width = 260, Name = "vp" + p.Key };
                    string key = p.Key;
                    combo.SelectedIndexChanged += (s, e) => { if (!_filling) { _dirty.MarkDirty(); Sample(key); } };
                    _body.Controls.Add(combo);
                    _combos[p.Key] = combo;
                    var sample = new Label { AutoSize = true, Location = new Point(554, y + 4), ForeColor = Color.FromArgb(110, 116, 124) };
                    _body.Controls.Add(sample);
                    _samples[p.Key] = sample;
                    y += UiStyle.RowPitch;
                }
                y += 10;
            }
            _note.Text = T("VP_Note", "Screen scale is set in User Settings. A user's own values are taken when they sign in, 'As for everyone' follows the panel's.");
            _note.AutoSize = false;
            _note.SetBounds(16, y + 4, 760, 44);
            _body.Controls.Add(_note);
        }

        // ---- whose ---------------------------------------------------------------

        private void FillWhose()
        {
            _filling = true;
            try
            {
                var keep = SelectedWhose();
                _whose.Items.Clear();
                var me = ViewPreferences.ActiveUser;
                if (SecurityManager.HasAccess(SecurityLevel.Maintenance) || me == null)
                    _whose.Items.Add(new Choice { Value = Everyone, Text = T("VP_Everyone", "Everyone (this panel)") });
                if (me != null) _whose.Items.Add(new Choice { Value = me, Text = me + " " + T("VP_Me", "(me)") });
                if (SecurityManager.HasAccess(SecurityLevel.Admin))
                    foreach (var u in UserStore.GetAllUsers().Select(a => a.Username).Where(n => !string.Equals(n, me, StringComparison.OrdinalIgnoreCase))
                                               .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                        _whose.Items.Add(new Choice { Value = u, Text = u });
                int at = -1;
                for (int i = 0; i < _whose.Items.Count; i++)
                    if (((Choice)_whose.Items[i]).Value == keep) at = i;
                if (at < 0)
                    for (int i = 0; i < _whose.Items.Count; i++)
                        if (((Choice)_whose.Items[i]).Value == (me ?? Everyone)) at = i;
                _whose.SelectedIndex = at >= 0 ? at : (_whose.Items.Count > 0 ? 0 : -1);
            }
            finally { _filling = false; }
        }

        private string SelectedWhose()
        {
            var c = _whose.SelectedItem as Choice;
            return c != null ? c.Value : null;
        }

        /// <summary>The user being edited, or null for everyone's.</summary>
        private string EditedUser { get { var w = SelectedWhose(); return w == null || w == Everyone ? null : w; } }

        /// <summary>May the one signed in change what is shown?</summary>
        private bool CanEdit
        {
            get
            {
                var w = SelectedWhose();
                if (w == null) return false;
                if (w == Everyone) return SecurityManager.HasAccess(SecurityLevel.Maintenance);
                if (string.Equals(w, ViewPreferences.ActiveUser, StringComparison.OrdinalIgnoreCase)) return true;
                return SecurityManager.HasAccess(SecurityLevel.Admin);
            }
        }

        // ---- the values ---------------------------------------------------------------

        private void Fill()
        {
            _filling = true;
            try
            {
                var user = EditedUser;
                bool edit = CanEdit;
                foreach (var p in ViewPreferences.All.Where(x => !x.Hidden))
                {
                    var combo = _combos[p.Key];
                    combo.Items.Clear();
                    if (user != null)
                        combo.Items.Add(new Choice
                        {
                            Value = null,
                            Text = T("VP_AsEveryone", "As for everyone") + "  (" + ValueText(p.Key, ViewPreferences.PanelValue(p.Key)) + ")"
                        });
                    foreach (var v in p.Values) combo.Items.Add(new Choice { Value = v, Text = ValueText(p.Key, v) });
                    string current = user != null ? ViewPreferences.UserValue(user, p.Key) : ViewPreferences.PanelValue(p.Key);
                    int at = 0;
                    for (int i = 0; i < combo.Items.Count; i++)
                        if (((Choice)combo.Items[i]).Value == current) at = i;
                    combo.SelectedIndex = at;
                    combo.Enabled = edit;
                    Sample(p.Key);
                }
                _dirty.Reset();
            }
            finally { _filling = false; }
        }

        /// <summary>What a value looks like, beside its combo (a date, a number).</summary>
        private void Sample(string key)
        {
            Label l;
            if (!_samples.TryGetValue(key, out l)) return;
            var c = _combos[key].SelectedItem as Choice;
            string v = c != null ? (c.Value ?? ViewPreferences.PanelValue(key)) : null;
            var t = new DateTime(2026, 10, 2, 14, 31, 7);
            switch (key)
            {
                case ViewPreferences.DateFormat:
                    l.Text = v == "European" ? "02.10.2026" : v == "Us" ? "10/02/2026" : "2026-10-02";
                    break;
                case ViewPreferences.TimeFormat:
                    l.Text = v == "H12" ? "2:31:07 PM" : "14:31:07";
                    break;
                case ViewPreferences.DecimalSeparator:
                    l.Text = (v == "Comma" ? "1234,56" : v == "Point" ? "1234.56" : 1234.56.ToString("F2", System.Globalization.CultureInfo.CurrentCulture)) + " mm";
                    break;
                default:
                    l.Text = "";
                    break;
            }
        }

        private void Save()
        {
            if (!CanEdit) return;
            var user = EditedUser;
            var values = new Dictionary<string, string>();
            foreach (var kv in _combos)
            {
                var c = kv.Value.SelectedItem as Choice;
                values[kv.Key] = c != null ? c.Value : null;
            }
            ViewPreferences.SetMany(user, values);
            AuditLogger.Log("view preferences " + (user == null ? "for everyone" : "of " + user) + " kept: " +
                            string.Join(", ", values.Select(kv => kv.Key + "=" + (kv.Value ?? "everyone's"))));
            Fill();
        }
    }
}
