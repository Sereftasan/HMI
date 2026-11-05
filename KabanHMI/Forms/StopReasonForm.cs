using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Dashboard;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// WHY DID THE MACHINE STOP? (5 November: "Yes, we could ask it to enter the reason" - StopReasons.) Shown over the main window
    /// once a stop has lasted its limit, as the PLC's messages are (PlcPopupForm: a band, its sign, its title, the machine) - not
    /// modal, the pages go on working behind it: how long the machine has stood (or stood) still and in which state, the reasons as
    /// large buttons (a finger's), "Later". Anyone may answer: the answer is written with the stop, with who gave it.
    /// </summary>
    public class StopReasonForm : Form
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        public StopReasons.Stop Stop { get; private set; }

        /// <summary>A reason chosen; "Later".</summary>
        public event Action<StopReasons.Reason> Answered;
        public event Action LaterAsked;

        private readonly Label _message = new Label { Name = "message" };
        private readonly Label _machine = new Label { Name = "machine" };
        private readonly Timer _timer = new Timer { Interval = 5000 };
        private static readonly Color BandBlue = Color.FromArgb(25, 113, 194);

        public StopReasonForm(StopReasons.Stop stop)
        {
            Stop = stop;
            Text = T("Stp_Title", "Why did the machine stop?");
            Name = "stopReason";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.White;
            Font = UiStyle.Body;
            AutoScaleMode = AutoScaleMode.None;
            Width = UiScale.Px(620);

            // ---- the band: the sign, the title, the machine ----
            var band = new Panel { Dock = DockStyle.Top, Height = UiScale.Px(54), BackColor = BandBlue, Name = "band" };
            var sign = new Panel { Bounds = new Rectangle(UiScale.Px(14), UiScale.Px(9), UiScale.Px(36), UiScale.Px(36)), BackColor = BandBlue };
            sign.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var c = Rectangle.Inflate(sign.ClientRectangle, -2, -2);
                using (var white = new SolidBrush(Color.White)) e.Graphics.FillEllipse(white, c);
                using (var f = new Font("Segoe UI", Math.Max(8f, c.Height * 0.5f), FontStyle.Bold, GraphicsUnit.Pixel))
                    TextRenderer.DrawText(e.Graphics, "?", f, c, BandBlue, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            };
            var title = new Label
            {
                Text = Text, AutoSize = false, ForeColor = Color.White, Name = "title",
                Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(UiScale.Px(60), UiScale.Px(4), Width - UiScale.Px(80), UiScale.Px(26))
            };
            _machine.AutoSize = false;
            _machine.ForeColor = Color.FromArgb(230, 236, 244);
            _machine.Font = UiStyle.Small;
            _machine.TextAlign = ContentAlignment.MiddleLeft;
            _machine.Bounds = new Rectangle(UiScale.Px(60), UiScale.Px(28), Width - UiScale.Px(80), UiScale.Px(20));
            band.Controls.Add(sign);
            band.Controls.Add(title);
            band.Controls.Add(_machine);

            // ---- how long, the reasons ----
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Name = "body" };
            int textWidth = Width - UiScale.Px(48);
            _message.Font = UiScale.NewFont("Segoe UI", 11.5F, FontStyle.Bold);
            _message.ForeColor = Color.FromArgb(25, 28, 34);
            _message.AutoSize = false;
            _message.Bounds = new Rectangle(UiScale.Px(24), UiScale.Px(16), textWidth, UiScale.Px(30));
            body.Controls.Add(_message);

            var grid = new TableLayoutPanel
            {
                Name = "reasons", ColumnCount = 3, Location = new Point(UiScale.Px(18), _message.Bottom + UiScale.Px(8)),
                Width = textWidth + UiScale.Px(12), GrowStyle = TableLayoutPanelGrowStyle.AddRows
            };
            for (int i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            int rows = (StopReasons.All.Count + 2) / 3;
            grid.RowCount = rows;
            for (int i = 0; i < rows; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, UiScale.Px(58)));
            grid.Height = rows * UiScale.Px(58);
            var tips = UiStyle.Tips();
            foreach (var r in StopReasons.All)
            {
                var reason = r;
                var b = UiStyle.TextButton(r.Shown, 0, (s, e) => { var h = Answered; if (h != null) h(reason); });
                b.Name = "reason" + r.Number;
                b.Tag = r;
                b.Dock = DockStyle.Fill;
                b.Margin = new Padding(UiScale.Px(6));
                b.AutoEllipsis = true;
                UiStyle.SetTip(tips, b, StopReasons.CategoryText(r.Category));
                grid.Controls.Add(b);
            }
            body.Controls.Add(grid);
            var note = new Label
            {
                Name = "note", AutoSize = false, ForeColor = Color.FromArgb(110, 116, 124), Font = UiStyle.Small,
                Text = T("Stp_Note", "Written in the machine's log with the stop."),
                Bounds = new Rectangle(UiScale.Px(24), grid.Bottom + UiScale.Px(6), textWidth, UiScale.Px(20))
            };
            body.Controls.Add(note);

            // ---- Later ----
            var buttons = new FlowLayoutPanel
            {
                Name = "buttons", FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Height = UiScale.Px(46),
                Padding = new Padding(0, UiScale.Px(6), UiScale.Px(14), 0), Dock = DockStyle.Bottom
            };
            var later = UiStyle.TextButton(T("Stp_Later", "Later"), 120, (s, e) => { var h = LaterAsked; if (h != null) h(); });
            later.Name = "btnLater";
            buttons.Controls.Add(later);

            Controls.Add(body);
            Controls.Add(buttons);
            Controls.Add(band);
            ClientSize = new Size(ClientSize.Width, band.Height + note.Bottom + UiScale.Px(10) + buttons.Height);

            ShowStop(stop);
            _timer.Tick += (s, e) => ShowStop(Stop);
            Shown += (s, e) => _timer.Start();
            FormClosed += (s, e) => { _timer.Stop(); _timer.Dispose(); tips.Dispose(); };
        }

        /// <summary>The stop as it is now: going on (how long so far) or over (from - to).</summary>
        public void ShowStop(StopReasons.Stop stop)
        {
            if (stop == null || IsDisposed) return;
            Stop = stop;
            var board = DashboardService.Find(stop.ModuleId);
            var zone = DashboardTime.Find(board != null ? board.Config.TimeZoneName : "");
            string state = "";
            if (board != null)
            {
                var map = board.Map;
                state = map.Translate(map.RawDescription(stop.State), StateMap.ColumnOf(LanguageManager.CurrentLanguage));
            }
            string name = Kaban.UI.Helpers.MachineLinks.NameOf(stop.ModuleId);
            _machine.Text = (string.IsNullOrEmpty(name) ? stop.ModuleId : name) + (state.Length > 0 ? "  ·  " + state : "");
            Func<DateTime, string> hm = t => DashboardTime.ToLocal(t, zone).ToString("HH:mm", CultureInfo.InvariantCulture);
            int minutes = (int)Math.Round(stop.Minutes(StopReasons.Clock()));
            _message.Text = stop.End.HasValue
                ? string.Format(T("Stp_StoodEnded", "It stood still {0} - {1} ({2} min)."), hm(stop.Start), hm(stop.End.Value), minutes)
                : string.Format(T("Stp_Standing", "It has stood still since {0} - {1} min so far."), hm(stop.Start), minutes);
        }

        /// <summary>The message's words (tests).</summary>
        public string MessageText { get { return _message.Text; } }
        public string MachineText { get { return _machine.Text; } }

        public void PlaceOver(Form owner)
        {
            if (owner == null) return;
            var area = owner.WindowState == FormWindowState.Minimized ? Screen.PrimaryScreen.WorkingArea : owner.Bounds;
            Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2), area.Top + Math.Max(0, (area.Height - Height) / 3));
        }
    }
}
