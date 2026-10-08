using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// ONE MESSAGE OF THE PLC (PlcPopups, 8 October): its title on a band of its
    /// kind's colour, its sign, its text, its values (read while it is open), its
    /// buttons. Not modal: the machine's pages go on working behind it; it stays
    /// above the main window until the operator answers or the PLC takes it back.
    /// A message without buttons (the PLC's own state: "Axes are referencing...")
    /// only has Hide, which writes nothing.
    /// </summary>
    public class PlcPopupForm : Form
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        public PlcPopups.Popup Popup { get; private set; }
        public int Station { get; private set; }
        public int Number { get; private set; }

        /// <summary>The operator chose a button (its answer).</summary>
        public event Action<PlcPopups.Button> Answered;
        /// <summary>Sign in, asked from the message (nobody signed in may answer).</summary>
        public event Action SignInAsked;

        private readonly Label _message = new Label();
        private readonly Label _note = new Label();
        private readonly FlowLayoutPanel _buttons = new FlowLayoutPanel();
        private readonly TableLayoutPanel _fields = new TableLayoutPanel();
        private readonly List<Action<IPlcRead>> _refresh = new List<Action<IPlcRead>>();
        private readonly Timer _timer = new Timer { Interval = 400 };
        private Button _signIn;
        private readonly Color _band;

        /// <summary>Reads a device of the message's station (the form's values; replaceable for tests).</summary>
        public interface IPlcRead { bool TryRead(string address, out int value); }

        public static Func<int, IPlcRead> ReaderFor = station =>
        {
            var d = PlcManager.Instance.GetDriverForMonitoring(station) ?? PlcManager.Instance.GetDriver(station);
            return d == null ? null : new DriverRead(d);
        };

        private sealed class DriverRead : IPlcRead
        {
            private readonly Kaban.Core.Interfaces.IPlcDriver _d;
            public DriverRead(Kaban.Core.Interfaces.IPlcDriver d) { _d = d; }
            public bool TryRead(string address, out int value) { return _d.TryRead(address, out value); }
            /// <summary>All the form's devices in one request (not one per device every 0.4 s).</summary>
            public IPlcRead Batch(IList<string> addresses)
            {
                int[] values;
                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var list = addresses.ToArray();
                if (list.Length > 0 && _d.TryReadMulti(list, out values) && values != null && values.Length == list.Length)
                    for (int i = 0; i < list.Length; i++) map[list[i]] = values[i];
                else return this;
                return new MapRead(map);
            }
        }

        private sealed class MapRead : IPlcRead
        {
            private readonly Dictionary<string, int> _map;
            public MapRead(Dictionary<string, int> map) { _map = map; }
            public bool TryRead(string address, out int value) { return _map.TryGetValue(address ?? "", out value); }
        }

        /// <summary>The devices the form shows.</summary>
        private readonly List<string> _wanted = new List<string>();

        private void Want(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            address = address.Trim().ToUpperInvariant();
            if (!_wanted.Contains(address)) _wanted.Add(address);
        }

        /// <summary>A button kind's caption: OK, Cancel, Yes, No, Continue, Restart, Retry.</summary>
        public static string Caption(PlcPopups.Button b)
        {
            var own = PlcPopups.In(b.Text, LanguageManager.CurrentLanguage);
            if (own.Length > 0) return own;
            switch ((b.Kind ?? "").Trim().ToLowerInvariant())
            {
                case "cancel": return T("Pop_Cancel", "Cancel");
                case "yes": return T("Pop_Yes", "Yes");
                case "no": return T("Pop_No", "No");
                case "continue": return T("Pop_Continue", "Continue");
                case "restart": return T("Pop_Restart", "Restart");
                case "retry": return T("Pop_Retry", "Retry");
                default: return T("Pop_OK", "OK");
            }
        }

        public PlcPopupForm(PlcPopups.Popup popup, int station, int number)
        {
            Popup = popup;
            Station = station;
            Number = number;
            string lang = LanguageManager.CurrentLanguage;

            Text = popup.TitleIn(lang);
            Name = "plcPopup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.White;
            Font = UiStyle.Body;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            Width = UiScale.Px(560);

            _band = BandColor(popup.Icon);

            // ---- the band: the sign, the title, the machine ----
            var band = new Panel { Dock = DockStyle.Top, Height = UiScale.Px(54), BackColor = _band, Name = "band" };
            var sign = new Panel { Bounds = new Rectangle(UiScale.Px(14), UiScale.Px(9), UiScale.Px(36), UiScale.Px(36)), BackColor = _band };
            sign.Paint += (s, e) => DrawSign(e.Graphics, sign.ClientRectangle, popup.Icon);
            var title = new Label
            {
                Text = popup.TitleIn(lang), AutoSize = false, ForeColor = Color.White, Name = "title",
                Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(UiScale.Px(60), UiScale.Px(4), Width - UiScale.Px(80), UiScale.Px(26))
            };
            var machine = new Label
            {
                Text = MachineCaption(popup, number), AutoSize = false, ForeColor = Color.FromArgb(230, 236, 244), Name = "machine",
                Font = UiStyle.Small, TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(UiScale.Px(60), UiScale.Px(28), Width - UiScale.Px(80), UiScale.Px(20))
            };
            band.Controls.Add(sign);
            band.Controls.Add(title);
            band.Controls.Add(machine);

            // ---- the text ----
            _message.Text = popup.TextIn(lang).Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            _message.Name = "message";
            _message.Font = UiScale.NewFont("Segoe UI", 11.5F, FontStyle.Bold);
            _message.ForeColor = Color.FromArgb(25, 28, 34);
            _message.AutoSize = false;
            int textWidth = Width - UiScale.Px(48);
            var size = TextRenderer.MeasureText(_message.Text, _message.Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak);
            _message.Bounds = new Rectangle(UiScale.Px(24), 0, textWidth, size.Height + UiScale.Px(8));

            // ---- the values ----
            BuildFields(popup, lang, textWidth);

            // ---- who may answer ----
            _note.AutoSize = false;
            _note.Name = "note";
            _note.ForeColor = Color.FromArgb(150, 60, 20);
            _note.Font = UiStyle.Small;
            _note.Text = T("Pop_SignIn", "Sign in to answer the machine.");
            _note.Visible = false;

            // ---- the buttons ----
            _buttons.FlowDirection = FlowDirection.RightToLeft;
            _buttons.WrapContents = false;
            _buttons.Name = "buttons";
            _buttons.Height = UiScale.Px(44);
            _buttons.Padding = new Padding(0, UiScale.Px(6), UiScale.Px(14), 0);
            var list = popup.Buttons.Count > 0 ? popup.Buttons : null;
            if (list != null)
            {
                // right to left: the last first (a Yes / No pair reads Yes, No)
                foreach (var b in list.AsEnumerable().Reverse())
                {
                    var btn = UiStyle.TextButton(Caption(b), 120);
                    btn.Name = "btn" + b.Kind;
                    btn.Tag = b;
                    btn.Margin = new Padding(UiScale.Px(8), 0, 0, 0);
                    var answer = b;
                    btn.Click += (s, e) => { var h = Answered; if (h != null) h(answer); };
                    _buttons.Controls.Add(btn);
                }
            }
            else
            {
                // the PLC's own state: only hidden here, nothing written
                var hide = UiStyle.TextButton(T("Pop_Hide", "Hide"), 120, (s, e) => Hide());
                hide.Name = "btnHide";
                _buttons.Controls.Add(hide);
            }
            _signIn = UiStyle.TextButton(T("Btn_Login", "Login"), 120, (s, e) => { var h = SignInAsked; if (h != null) h(); });
            _signIn.Name = "btnSignIn";
            _signIn.Visible = false;
            _signIn.Margin = new Padding(UiScale.Px(8), 0, UiScale.Px(16), 0);
            _buttons.Controls.Add(_signIn);

            // ---- laid out top to bottom ----
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Name = "body" };
            int y = UiScale.Px(18);
            _message.Top = y;
            body.Controls.Add(_message);
            y = _message.Bottom + UiScale.Px(8);
            if (_fields.Controls.Count > 0)
            {
                _fields.Location = new Point(UiScale.Px(24), y);
                body.Controls.Add(_fields);
                y = _fields.Bottom + UiScale.Px(10);
            }
            _note.Bounds = new Rectangle(UiScale.Px(24), y, textWidth, UiScale.Px(20));
            body.Controls.Add(_note);
            y = _note.Bottom + UiScale.Px(4);
            _buttons.Dock = DockStyle.Bottom;
            Controls.Add(body);
            Controls.Add(_buttons);
            Controls.Add(band);
            // a long list (16 axes, a large magnifier): the body scrolls, the buttons stay in sight
            int wanted = band.Height + y + _buttons.Height + UiScale.Px(6);
            int most = (int)(Screen.PrimaryScreen.WorkingArea.Height * 0.85) - (Height - ClientSize.Height);
            body.AutoScroll = wanted > most;
            ClientSize = new Size(ClientSize.Width, Math.Min(wanted, Math.Max(UiScale.Px(240), most)));

            // Enter: the first answer; Esc: Cancel / No when there is one
            KeyDown += (s, e) =>
            {
                PlcPopups.Button pick = null;
                if (e.KeyCode == Keys.Escape) pick = popup.Buttons.FirstOrDefault(b => b.Answer == 0);
                else if (e.KeyCode == Keys.Enter) pick = popup.Buttons.FirstOrDefault();
                if (pick == null || !CanAnswer) return;
                e.Handled = true;
                var h = Answered;
                if (h != null) h(pick);
            };

            _timer.Tick += (s, e) => RefreshValues();
            Shown += (s, e) => { RefreshValues(); FollowSignIn(); _timer.Start(); };
            SecurityManager.UserChanged += OnUserChanged;
            FormClosed += (s, e) => { _timer.Stop(); _timer.Dispose(); SecurityManager.UserChanged -= OnUserChanged; };
        }

        private void OnUserChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new Action(FollowSignIn)); } catch { } return; }
            FollowSignIn();
        }

        /// <summary>True when the one signed in may command the station (the answer is a command).</summary>
        public bool CanAnswer { get { return PlcManager.Instance.CheckPermission(Station); } }

        /// <summary>The buttons usable only for someone who may command the machine; else Sign in.</summary>
        public void FollowSignIn()
        {
            bool can = CanAnswer;
            foreach (Control c in _buttons.Controls)
                if (c.Tag is PlcPopups.Button) c.Enabled = can;
            bool asks = !can && Popup.Buttons.Count > 0;
            _note.Visible = asks;
            _signIn.Visible = asks;
        }

        /// <summary>Says why an answer did not reach the PLC (the message stays).</summary>
        public void ShowError(string text)
        {
            _note.Text = T("Pop_NotSent", "The answer did not reach the PLC") + ": " + text;
            _note.Visible = true;
        }

        private static string MachineCaption(PlcPopups.Popup popup, int number)
        {
            var machine = string.IsNullOrEmpty(popup.ModuleId) ? "" : LanguageManager.GetString("Machine_" + popup.ModuleId, popup.ModuleId);
            return (machine.Length > 0 ? machine + "  ·  " : "") + T("Pop_Number", "PLC message") + " " + number;
        }

        public static Color BandColor(string icon)
        {
            switch ((icon ?? "").ToLowerInvariant())
            {
                case "error": return Color.FromArgb(201, 42, 42);
                case "warning": return Color.FromArgb(214, 120, 0);
                case "question": return Color.FromArgb(25, 113, 194);
                default: return Color.FromArgb(31, 78, 121);
            }
        }

        private static void DrawSign(Graphics g, Rectangle r, string icon)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var c = Rectangle.Inflate(r, -2, -2);
            using (var white = new SolidBrush(Color.White)) g.FillEllipse(white, c);
            string s;
            switch ((icon ?? "").ToLowerInvariant())
            {
                case "error": s = "✖"; break;
                case "warning": s = "!"; break;
                case "question": s = "?"; break;
                default: s = "i"; break;
            }
            using (var f = new Font("Segoe UI", Math.Max(8f, c.Height * 0.5f), FontStyle.Bold, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, s, f, c, BandColor(icon), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        // ---- the values --------------------------------------------------------------------------

        private void BuildFields(PlcPopups.Popup popup, string lang, int width)
        {
            _fields.Name = "fields";
            _fields.AutoSize = true;
            _fields.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _fields.ColumnCount = 3;
            _fields.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
            var heads = popup.HeadsIn(lang);
            var valueFields = popup.Fields.Where(f => !string.Equals(f.Kind, "Axes", StringComparison.OrdinalIgnoreCase)).ToList();
            bool two = valueFields.Any(f => !string.IsNullOrWhiteSpace(f.Address2));
            if (heads.Length > 0 && valueFields.Count > 0)
            {
                var parts = heads.Split('/').Select(p => p.Trim()).ToArray();
                AddRow(new Label { Text = "", AutoSize = true },
                       Head(parts.Length > 0 ? parts[0] : ""), two ? Head(parts.Length > 1 ? parts[1] : "") : new Label { AutoSize = true });
            }
            foreach (var f in popup.Fields)
            {
                var kind = (f.Kind ?? "Value").ToLowerInvariant();
                if (kind == "axes") { BuildAxes(popup, width); continue; }
                var label = new Label
                {
                    Text = PlcPopups.In(f.Label, lang) + (kind == "lamp" ? "" : ":"), AutoSize = true, Margin = new Padding(0, UiScale.Px(6), UiScale.Px(12), 0),
                    Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), ForeColor = Color.FromArgb(52, 58, 64)
                };
                if (kind == "lamp")
                {
                    var lamp = Lamp(f.Address);
                    AddRow(label, lamp, new Label { AutoSize = true });
                    continue;
                }
                var v1 = Value(f, f.Address);
                var v2 = two ? Value(f, f.Address2) : (Control)new Label { AutoSize = true };
                AddRow(label, v1, v2);
            }
        }

        private Label Head(string text)
        {
            return new Label
            {
                Text = text, AutoSize = false, Width = UiScale.Px(120), TextAlign = ContentAlignment.MiddleCenter,
                Font = UiStyle.FontVariant(UiStyle.Small, FontStyle.Bold), ForeColor = Color.FromArgb(110, 116, 124)
            };
        }

        private void AddRow(Control a, Control b, Control c)
        {
            int row = _fields.RowCount++;
            _fields.Controls.Add(a, 0, row);
            _fields.Controls.Add(b, 1, row);
            _fields.Controls.Add(c, 2, row);
        }

        /// <summary>A number read from the PLC, in a grey box, with its unit.</summary>
        private Control Value(PlcPopups.Field f, string address)
        {
            var box = new Label
            {
                AutoSize = false, Width = UiScale.Px(120), Height = UiScale.Px(28), TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.FromArgb(233, 236, 239), Margin = new Padding(UiScale.Px(4), UiScale.Px(2), UiScale.Px(4), UiScale.Px(2)),
                Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), Text = "--", Name = "value" + address, Padding = new Padding(0, 0, UiScale.Px(6), 0)
            };
            Want(address);
            address = (address ?? "").Trim().ToUpperInvariant();
            string unit = string.IsNullOrEmpty(f.Unit) ? "" : " " + f.Unit;
            double divisor = f.Divisor == 0 ? 1 : f.Divisor;
            _refresh.Add(read =>
            {
                int v;
                box.Text = read != null && !string.IsNullOrWhiteSpace(address) && read.TryRead(address, out v)
                    ? ((short)v / divisor).ToString("F" + Math.Max(0, f.Decimals)) + unit : "--";
            });
            return box;
        }

        /// <summary>A bit read from the PLC, as a lamp.</summary>
        private Control Lamp(string address, Color? on = null)
        {
            var lamp = new Panel { Width = UiScale.Px(22), Height = UiScale.Px(22), Margin = new Padding(UiScale.Px(4), UiScale.Px(4), UiScale.Px(4), UiScale.Px(2)), Name = "lamp" + address };
            bool lit = false, known = false;
            lamp.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = Rectangle.Inflate(lamp.ClientRectangle, -2, -2);
                using (var b = new SolidBrush(!known ? Color.FromArgb(222, 226, 230) : lit ? (on ?? Color.FromArgb(201, 42, 42)) : Color.FromArgb(206, 212, 218)))
                    e.Graphics.FillEllipse(b, r);
                using (var p = new Pen(Color.FromArgb(134, 142, 150))) e.Graphics.DrawEllipse(p, r);
            };
            lamp.Tag = new Func<bool>(() => lit);
            Want(address);
            address = (address ?? "").Trim().ToUpperInvariant();
            _refresh.Add(read =>
            {
                int v = 0;
                bool k = read != null && !string.IsNullOrWhiteSpace(address) && read.TryRead(address, out v);
                bool l = k && v != 0;
                if (k != known || l != lit) { known = k; lit = l; lamp.Invalidate(); }
            });
            return lamp;
        }

        /// <summary>
        /// The machine's axes (popup 5: "out of operation limits or need
        /// referencing"): each with its home request - it needs referencing - and
        /// its error bit, as on the Axis Settings rows. The ones lit stand out.
        /// </summary>
        private void BuildAxes(PlcPopups.Popup popup, int width)
        {
            var axes = Kaban.UI.Helpers.AlarmHelpLinks.AllAxes.Where(a => a.Station == popup.Station).ToList();
            if (axes.Count == 0) return;
            string lang = LanguageManager.CurrentLanguage;
            var head = popup.HeadsIn(lang).Split('/').Select(p => p.Trim()).ToArray();
            AddRow(new Label { Text = "", AutoSize = true }, Head(head.Length > 0 ? head[0] : T("Pop_Reference", "Reference")),
                   Head(head.Length > 1 ? head[1] : T("Pop_Fault", "Fault")));
            foreach (var a in axes)
            {
                object home;
                string homeAddress = a.Row != null && a.Row.TryGetValue("HomeRequestAddress", out home) ? Convert.ToString(home) : "";
                var name = new Label
                {
                    Text = string.IsNullOrEmpty(a.NameKey) ? "Axis " + a.Number : LanguageManager.GetString(a.NameKey, a.NameKey),
                    AutoSize = true, Margin = new Padding(0, UiScale.Px(4), UiScale.Px(12), 0)
                };
                var refLamp = Lamp(homeAddress, Color.FromArgb(240, 140, 0));
                var errLamp = Lamp(a.Error);
                refLamp.Anchor = errLamp.Anchor = AnchorStyles.None;
                AddRow(name, refLamp, errLamp);
            }
        }

        /// <summary>The values again (every 0.4 s while open; once more by a test).</summary>
        public void RefreshValues()
        {
            if (_refresh.Count == 0) return;
            IPlcRead read = null;
            try { read = ReaderFor(Station); } catch { read = null; }
            var batch = read as DriverRead;
            if (batch != null) try { read = batch.Batch(_wanted); } catch { }
            foreach (var r in _refresh) try { r(read); } catch { }
        }

        /// <summary>Centred over the window it belongs to.</summary>
        public void PlaceOver(Form owner)
        {
            if (owner == null) return;
            var area = owner.WindowState == FormWindowState.Minimized ? Screen.PrimaryScreen.WorkingArea : owner.Bounds;
            Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2), area.Top + Math.Max(0, (area.Height - Height) / 3));
        }
    }
}
