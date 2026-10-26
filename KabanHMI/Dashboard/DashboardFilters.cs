using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// WHAT THE DASHBOARD'S FILTERS LET THROUGH (2 October) - the shifts, the log
    /// types, the users, the controllers and the sources chosen on the left; each
    /// of them several at once, none meaning all. They hold for every table of the
    /// page: the machine's states, events and alarms as much as the HMI's own log.
    ///
    /// The machine's log database does not record a user, a controller or a source
    /// yet; until it does, its entries are the administrator's (MachineUser), on the
    /// machine's first PLC (MainStation), from the machine (MachineSource). A filter
    /// that leaves those out leaves the machine's whole log out - and it is then not
    /// read at all, as the log types not chosen are not.
    /// </summary>
    public sealed class DashboardFilter
    {
        public List<DashboardConfig.Shift> Shifts = new List<DashboardConfig.Shift>();
        public HashSet<AuditKind> Kinds = new HashSet<AuditKind>();
        public HashSet<string> Users = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<int> Stations = new HashSet<int>();
        /// <summary>"" the HMI's own log, a machine's id its log database.</summary>
        public HashSet<string> Sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public string MachineSource = "";
        public int MainStation = 1;
        public string MachineUser = MachineLogFeed.MachineUser;

        /// <summary>The log types, in the order the Log Type filter lists them: the machine's first, as its tabs.</summary>
        public static readonly AuditKind[] AllKinds =
        {
            AuditKind.MachineState, AuditKind.MachineEvent, AuditKind.MachineAlarm,
            AuditKind.SignIn, AuditKind.Connection, AuditKind.PlcWrite, AuditKind.DeviceWrite,
            AuditKind.Settings, AuditKind.Problem, AuditKind.Other
        };

        /// <summary>The types the HMI's own log holds.</summary>
        public static readonly AuditKind[] HmiKinds =
        {
            AuditKind.SignIn, AuditKind.Connection, AuditKind.PlcWrite, AuditKind.DeviceWrite,
            AuditKind.Settings, AuditKind.Problem, AuditKind.Other
        };

        public bool Kind(AuditKind k) { return Kinds.Count == 0 || Kinds.Contains(k); }

        /// <summary>The machine's entries pass the user, controller and source chosen.</summary>
        public bool MachinePasses
        {
            get
            {
                return (Users.Count == 0 || Users.Contains(MachineUser)) &&
                       (Stations.Count == 0 || Stations.Contains(MainStation)) &&
                       (Sources.Count == 0 || Sources.Contains(MachineSource ?? ""));
            }
        }

        /// <summary>What to read of the machine's log: nothing when it is filtered out.</summary>
        public DashboardData.Parts MachineParts
        {
            get
            {
                if (!MachinePasses) return DashboardData.Parts.None;
                var p = DashboardData.Parts.None;
                if (Kind(AuditKind.MachineState)) p |= DashboardData.Parts.States;
                if (Kind(AuditKind.MachineEvent)) p |= DashboardData.Parts.Events;
                if (Kind(AuditKind.MachineAlarm)) p |= DashboardData.Parts.Alarms;
                return p;
            }
        }

        /// <summary>The HMI's own log is to be read: its source and one of its types are in.</summary>
        public bool HmiWanted
        {
            get { return (Sources.Count == 0 || Sources.Contains("")) && HmiKinds.Any(Kind); }
        }

        /// <summary>An entry of the HMI's log passes every filter.</summary>
        public bool Passes(AuditEntry e)
        {
            return Kind(e.Kind) &&
                   (Users.Count == 0 || Users.Contains(e.User ?? "")) &&
                   (Stations.Count == 0 || (e.Station.HasValue && Stations.Contains(e.Station.Value))) &&
                   (Sources.Count == 0 || Sources.Contains(e.Source ?? "")) &&
                   DashboardData.InShifts(e.Time.TimeOfDay, Shifts);
        }
    }

    /// <summary>
    /// A FILTER OF THE LEFT BAR THAT TAKES SEVERAL VALUES: it looks like the combo
    /// boxes above it; a click opens the list to tick, with "All ..." at its foot to
    /// untick everything. Nothing ticked lets everything through (and reads
    /// "All ..."). Ticks take effect at once.
    /// </summary>
    public sealed class PickList : Control
    {
        public sealed class Item
        {
            public string Key, Text;
            public Item(string key, string text) { Key = key ?? ""; Text = text ?? key ?? ""; }
        }

        private List<Item> _items = new List<Item>();
        private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly ToolTip _tips;
        private bool _hover;
        private string _allText = "";

        public event EventHandler Changed;

        public PickList(ToolTip tips)
        {
            _tips = tips;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.White;
            Font = UiStyle.Body;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        /// <summary>
        /// Ticks taken while the list is open are told (Changed) ONCE, when it closes -
        /// for a list whose change costs a page laid out again (the axis rows): ticking
        /// three values no longer builds the page three times while the list waits.
        /// </summary>
        public bool ApplyOnClose { get; set; }

        /// <summary>True while its list is open (tests).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>The open list and its drop-down (tests: ticked and closed as the user would).</summary>
        public CheckedListBox OpenList { get; private set; }
        public ToolStripDropDown OpenDrop { get; private set; }

        /// <summary>What it reads with nothing ticked ("All users"), and the button that unticks all.</summary>
        public string AllText
        {
            get { return _allText; }
            set { _allText = value ?? ""; Caption(); }
        }

        private string _heading = "";

        /// <summary>
        /// A list named by its own first line instead of a caption beside it (8
        /// October: "remove these two controls' descriptions ... and show them as the
        /// combo's first member - not to select, just for the user to see when the
        /// combo is both open or closed"): closed, it reads its heading, with how many
        /// are ticked ("3/8") or what nothing ticked means ("All", "None") at its
        /// right; open, the heading is the list's first line, greyed, not ticked.
        /// </summary>
        public string Heading
        {
            get { return _heading; }
            set { _heading = (value ?? "").Trim().TrimEnd(':').Trim(); Caption(); }
        }

        private string _lockedWhy = "";

        /// <summary>
        /// Shown greyed with its ticks, not opened, saying why in its tooltip (26 October: the Axis Monitor's
        /// lists for Maintenance - "The maintenance level should see it greyed, not the operator though"). Not
        /// Enabled = false: a disabled control shows no tooltip. Empty: the list as it is.
        /// </summary>
        public string LockedWhy
        {
            get { return _lockedWhy; }
            set { _lockedWhy = value ?? ""; Cursor = Locked ? Cursors.Default : Cursors.Hand; Caption(); }
        }

        public bool Locked { get { return _lockedWhy.Length > 0; } }

        /// <summary>Drawn as a list that can be opened.</summary>
        private bool Live { get { return Enabled && !Locked; } }

        /// <summary>At the right of a list with a heading: how many are ticked, or what none ticked means.</summary>
        public string State { get { return _selected.Count == 0 ? _allText : _selected.Count + "/" + _items.Count; } }

        /// <summary>As wide as a list with a heading needs: its heading, its state, its arrow.</summary>
        public int NaturalWidth
        {
            get
            {
                string widest = _items.Count > 0 ? _items.Count + "/" + _items.Count : "";
                if (TextRenderer.MeasureText(_allText, UiStyle.Small).Width > TextRenderer.MeasureText(widest, UiStyle.Small).Width) widest = _allText;
                return TextRenderer.MeasureText(_heading.Length > 0 ? _heading : Shown, Font).Width + TextRenderer.MeasureText(widest, UiStyle.Small).Width +
                       UiScale.Px(18) + UiScale.Px(22);
            }
        }

        public List<Item> Items { get { return _items.ToList(); } }

        /// <summary>The keys ticked (a copy).</summary>
        public HashSet<string> Selected { get { return new HashSet<string>(_selected, StringComparer.OrdinalIgnoreCase); } }

        public List<string> SelectedTexts { get { return _items.Where(i => _selected.Contains(i.Key)).Select(i => i.Text).ToList(); } }

        /// <summary>
        /// The values to choose from. With <paramref name="keepChosen"/> a value
        /// ticked stays in the list when the new one lacks it (a user the period has
        /// no entry of); otherwise its tick goes.
        /// </summary>
        public void SetItems(IEnumerable<Item> items, bool keepChosen)
        {
            var list = new List<Item>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var i in items ?? new Item[0])
                if (i != null && keys.Add(i.Key)) list.Add(i);
            if (keepChosen)
            {
                foreach (var k in _selected)
                    if (!keys.Contains(k))
                    {
                        var was = _items.FirstOrDefault(i => string.Equals(i.Key, k, StringComparison.OrdinalIgnoreCase));
                        list.Add(was ?? new Item(k, k));
                        keys.Add(k);
                    }
            }
            else _selected.RemoveWhere(k => !keys.Contains(k));
            _items = list;
            Caption();
        }

        /// <summary>Ticks these (of the list) and nothing else - as the user would; raises Changed.</summary>
        public void Select(IEnumerable<string> keys)
        {
            _selected.Clear();
            foreach (var k in keys ?? new string[0])
                if (_items.Any(i => string.Equals(i.Key, k, StringComparison.OrdinalIgnoreCase))) _selected.Add(k);
            Caption();
            Raise();
        }

        /// <summary>What the box reads: its heading, else what is ticked (nothing ticked: AllText).</summary>
        public string Shown { get { return _heading.Length > 0 ? _heading : _selected.Count == 0 ? _allText : string.Join(", ", SelectedTexts); } }

        private void Raise()
        {
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void Caption()
        {
            Text = Shown;
            if (_tips != null) Kaban.UI.Controls.UiStyle.SetTip(_tips, this, (_heading.Length > 0 ? _heading + "\n" : "") +
                                                                       (_selected.Count == 0 ? _allText : string.Join("\n", SelectedTexts)) +
                                                                       (Locked ? "\n" + _lockedWhy : ""));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            if (PaintAsCombo(g)) return;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var pen = new Pen(Live && (_hover || Focused) ? Color.FromArgb(0, 120, 215) : Color.FromArgb(122, 122, 122)))
                g.DrawRectangle(pen, r);
            int arrow = UiScale.Px(18);
            var text = new Rectangle(UiScale.Px(4), 0, Width - arrow - UiScale.Px(6), Height);
            text.Width -= PaintState(g, text.Right);
            TextRenderer.DrawText(g, Shown, Font, text, Live ? UiStyle.GridInk : SystemColors.GrayText,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            // the arrow of a combo box
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = Width - arrow / 2f - UiScale.Px(2), cy = Height / 2f, a = UiScale.Px(4);
            using (var pen = new Pen(Live ? Color.FromArgb(70, 70, 70) : SystemColors.GrayText, Math.Max(1.5f, UiScale.Px(2) * 0.75f)) { LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { new PointF(cx - a, cy - a / 2), new PointF(cx, cy + a / 2), new PointF(cx + a, cy - a / 2) });
        }

        /// <summary>
        /// On Windows: drawn as the theme draws a drop-down list combo box (the
        /// boxes above it), so the two kinds of filter look alike. False where the
        /// theme cannot draw it.
        /// </summary>
        private bool PaintAsCombo(Graphics g)
        {
            try
            {
                if (!Application.RenderWithVisualStyles || !VisualStyleRenderer.IsSupported) return false;
                // COMBOBOX: CP_READONLY = 5 (normal 1, hot 2, pressed 3, disabled 4); CP_DROPDOWNBUTTONRIGHT = 6
                int state = !Live ? 4 : _hover ? 2 : 1;
                var back = VisualStyleElement.CreateElement("COMBOBOX", 5, state);
                var arrow = VisualStyleElement.CreateElement("COMBOBOX", 6, state);
                if (!VisualStyleRenderer.IsElementDefined(back) || !VisualStyleRenderer.IsElementDefined(arrow)) return false;
                new VisualStyleRenderer(back).DrawBackground(g, ClientRectangle);
                int w = SystemInformation.VerticalScrollBarWidth;
                new VisualStyleRenderer(arrow).DrawBackground(g, new Rectangle(Width - w - 1, 1, w, Height - 2));
                var text = new Rectangle(UiScale.Px(4), 0, Width - w - UiScale.Px(6), Height);
                text.Width -= PaintState(g, text.Right);
                TextRenderer.DrawText(g, Shown, Font, text, Live ? SystemColors.WindowText : SystemColors.GrayText,
                                      TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if (Focused && Live) ControlPaint.DrawFocusRectangle(g, new Rectangle(2, 2, Width - w - 4, Height - 4));
                return true;
            }
            catch { return false; }
        }

        /// <summary>A list with a heading: its state at the right, ending at <paramref name="right"/>; the room it took.</summary>
        private int PaintState(Graphics g, int right)
        {
            if (_heading.Length == 0) return 0;
            var state = State;
            if (state.Length == 0) return 0;
            var size = TextRenderer.MeasureText(state, UiStyle.Small);
            var colour = !Live ? SystemColors.GrayText : _selected.Count > 0 ? Color.FromArgb(0, 120, 215) : Color.FromArgb(120, 126, 134);
            TextRenderer.DrawText(g, state, UiStyle.Small, new Rectangle(right - size.Width, 0, size.Width, Height), colour,
                                  TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            return size.Width + UiScale.Px(6);
        }

        /// <summary>The heading line of the open list (tests).</summary>
        public Label OpenHeading { get; private set; }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Focus();
            Drop();
        }

        private void Drop() { DropUnder(this); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter || e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode == Keys.Down))
            {
                Drop();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Opens the list to tick under <paramref name="anchor"/> - itself, or another
        /// control standing for it (a table's column heading: the Axis heading of an
        /// axis page opens its Axes list).
        /// </summary>
        public void DropUnder(Control anchor)
        {
            if (anchor == null || anchor.IsDisposed || Locked) return;
            var items = _items.ToList();
            int widest = items.Count == 0 ? 0 : items.Max(i => TextRenderer.MeasureText(i.Text, Font).Width);
            var list = new CheckedListBox
            {
                CheckOnClick = true, BorderStyle = BorderStyle.None, Font = Font, IntegralHeight = false,
                Width = Math.Max(Math.Max(anchor.Width, UiScale.Px(160)), Math.Min(UiScale.Px(420), widest + UiScale.Px(48))),
                Height = Math.Min(UiScale.Px(360), Math.Max(UiScale.Px(40), items.Count * UiScale.Px(22) + UiScale.Px(8)))
            };
            foreach (var i in items) list.Items.Add(i.Text, _selected.Contains(i.Key));
            var clear = new Button { Text = _allText, Width = list.Width, Height = UiScale.Px(30), Font = Font, FlatStyle = FlatStyle.Flat };
            // a heading: the list's first line, greyed - read, not ticked
            Label heading = null;
            if (_heading.Length > 0)
            {
                list.Width = Math.Max(list.Width, Math.Min(UiScale.Px(420), TextRenderer.MeasureText(_heading, Font).Width + UiScale.Px(24)));
                clear.Width = list.Width;
                heading = new Label
                {
                    Name = "pickHeading", Text = _heading, Dock = DockStyle.Top, Height = UiScale.Px(26), Font = new Font(Font, FontStyle.Bold),
                    BackColor = Color.FromArgb(238, 241, 245), ForeColor = Color.FromArgb(90, 96, 106), TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Padding = new Padding(UiScale.Px(6), 0, 0, 0), AutoEllipsis = true
                };
            }
            var panel = new Panel { Width = list.Width, Height = list.Height + clear.Height + (heading != null ? heading.Height : 0), BackColor = Color.White };
            clear.Dock = DockStyle.Bottom;
            list.Dock = DockStyle.Fill;
            panel.Controls.Add(list);
            if (heading != null) panel.Controls.Add(heading);
            panel.Controls.Add(clear);
            OpenHeading = heading;
            var host = new ToolStripControlHost(panel) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = panel.Size };
            var drop = new ToolStripDropDown { Padding = Padding.Empty };
            drop.Items.Add(host);
            bool pending = false;
            list.ItemCheck += (s, e) => anchor.BeginInvoke(new Action(() =>
            {
                _selected.Clear();
                foreach (int i in list.CheckedIndices) _selected.Add(items[i].Key);
                Caption();
                if (ApplyOnClose) pending = true;
                else Raise();
            }));
            // everything let through again: one change, not one per tick taken off
            clear.Click += (s, e) =>
            {
                if (_selected.Count > 0)
                {
                    _selected.Clear();
                    Caption();
                    pending = false;
                    Raise();
                }
                drop.Close();
            };
            drop.Closed += (s, e) => anchor.BeginInvoke(new Action(() =>
            {
                IsOpen = false;
                OpenList = null; OpenDrop = null; OpenHeading = null;
                drop.Dispose();
                // the ticks of an ApplyOnClose list, told once now
                if (pending) { pending = false; Raise(); }
            }));
            IsOpen = true;
            OpenList = list; OpenDrop = drop;
            UiStyle.ShowPopup(drop, anchor, new Point(0, anchor.Height));
        }
    }
}
