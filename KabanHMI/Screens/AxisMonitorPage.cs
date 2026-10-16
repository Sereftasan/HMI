using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Screens
{
    /// <summary>
    /// THE AXIS MONITOR (side page): every axis's position and motor current,
    /// beside whatever page is on screen.
    ///
    /// Nothing is added to it by hand. The axes are the rows of the machines'
    /// Axis Settings pages (AxisSettingHMI), found by the same discovery that
    /// lets a manual-page axis unit mirror its row; an axis is listed while its
    /// row's visibility conditions are met. Three columns - name, position,
    /// current - drawn exactly as on the Axis Settings row, so the same axis
    /// looks the same on both.
    /// </summary>
    public class AxisMonitorPage : UserControl
    {
        /// <summary>
        /// The rows - the one thing on the page that scrolls (IScrollsItself). In the
        /// side panel the page around it was made to scroll as well (PageScroll), and
        /// two scrolls for one list hid its right end under the outer one's edge
        /// (8 October: "the scroll doesn't move to right as much as needed").
        /// It stays where it is scrolled to (SteadyScroll), and it takes the focus the
        /// side page is given itself - the first row's jog button had it, and the
        /// list, scrolled back to it again and again, could not be scrolled past it
        /// (12 October) - so the wheel scrolls it.
        /// </summary>
        private sealed class RowList : SteadyScroll.FlowPanel, IScrollsItself
        {
            public RowList()
            {
                SetStyle(ControlStyles.Selectable, true);
                TabStop = true;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (CanFocus && !Focused) Focus();
                base.OnMouseDown(e);
            }
        }
        private readonly FlowLayoutPanel _list = new RowList();
        private readonly Label _empty = new Label();
        private readonly Panel _head = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Color.White };
        // An Admin's rows' headings: above the list, not in it - they stay while the
        // rows scroll up and down, and follow them sideways.
        private readonly Panel _rowsHead = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = Color.White, Visible = false, Name = "monitorRowsHead" };
        /// <summary>An Admin's PLC axis items and Motion monitor for the monitor (5 October).</summary>
        private readonly AxisPickBar _bar = new AxisPickBar(ViewPreferences.MonitorItems, ViewPreferences.MonitorMotion, axesKey: ViewPreferences.MonitorAxes)
                                            { Dock = DockStyle.Top, WrapContents = true };
        private string _built;

        /// <summary>
        /// THE MACHINES' AXES' BARS (14 October: "copy this control set ... to the Axis
        /// Monitor"): each machine's speed and jog step (ScreenRoles.AxisBar), at the top,
        /// for the machines whose axes are listed - made once, its parts in rows when the
        /// side page is narrower than it.
        /// </summary>
        private readonly Panel _machineBars = new Panel { Dock = DockStyle.Top, Height = 0, BackColor = Color.White, Name = "monitorMachineBars", Padding = Padding.Empty };   // the room around a bar's parts is the bar's own
        private readonly Dictionary<string, MachineBarHMI> _barOf = new Dictionary<string, MachineBarHMI>();

        /// <summary>The machines' bars shown (tests).</summary>
        internal IList<MachineBarHMI> MachineBars { get { return _machineBars.Controls.OfType<MachineBarHMI>().Where(_wantedBars.Contains).ToList(); } }
        private readonly HashSet<MachineBarHMI> _wantedBars = new HashSet<MachineBarHMI>();

        /// <summary>Column widths: the Axis Settings row's own.</summary>
        internal const int WName = AxisSettingHMI.WAxis, WPosition = AxisSettingHMI.WPosition, WMeter = AxisSettingHMI.WMeter,
                           Gap = AxisSettingHMI.Gap, Left0 = 4;

        public AxisMonitorPage()
        {
            BackColor = Color.White;
            Font = UiStyle.Body;
            Dock = DockStyle.Fill;

            int x = Left0;
            var axisHead = Heading(LanguageManager.GetString("Col_Axis", "Axis"), x, WName);
            _head.Controls.Add(axisHead);
            // the Axis heading opens the Axes list (which axes the monitor shows)
            AxisHeadingFilter.Attach(axisHead, _bar.AxesPick);
            x += WName + Gap + Gap;
            _head.Controls.Add(Heading(LanguageManager.GetString("Col_AxisPosition", "Position"), x, WPosition));
            x += WPosition + Gap;
            _head.Controls.Add(Heading(LanguageManager.GetString("Col_AxisCurrent", "Current"), x, WMeter));

            _list.Dock = DockStyle.Fill;
            _list.FlowDirection = FlowDirection.TopDown;
            _list.WrapContents = false;
            _list.AutoScroll = true;
            _list.BackColor = Color.White;
            _list.Padding = new Padding(0, 0, 0, 8);

            _empty.Dock = DockStyle.Fill;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.ForeColor = Color.FromArgb(110, 116, 126);
            _empty.Padding = new Padding(16);
            _empty.Visible = false;
            _empty.Text = LanguageManager.GetString("Msg_NoAxes", "No axis is set up on the Axis Settings pages.");

            _bar.Padding = new Padding(4, 0, 0, 2);
            _bar.Resize += (s, e) => FitBar();
            // the axes shown: the list again with them only (moved up together)
            _bar.AxesChanged += (s, e) => { if (!IsDisposed) { Build(); Kaban.UI.Plc.PlcScanner.NotifyContentChanged(); } };

            Controls.Add(_list);
            Controls.Add(_empty);
            Controls.Add(_rowsHead);
            Controls.Add(_head);
            ScrollFollow.Follow(_list, FollowRows);
            Controls.Add(_bar);
            Controls.Add(_machineBars);
            _machineBars.Resize += (s, e) => FitMachineBars();
            Build();

            // A sign-in (an Admin's picks, or none) and a pick changed: the rows again.
            EventHandler again = (s, e) =>
            {
                if (IsDisposed) return;
                if (IsHandleCreated) { try { BeginInvoke(new Action(RebuildIfChanged)); } catch { } }
                else RebuildIfChanged();
            };
            ViewPreferences.Changed += again;
            SecurityManager.UserChanged += again;
            Disposed += (s, e) => { ViewPreferences.Changed -= again; SecurityManager.UserChanged -= again; };
        }

        private static Label Heading(string text, int x, int w)
        {
            return new Label
            {
                Text = text, Location = new Point(x, 4), Size = new Size(w, 18), TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(110, 116, 124)
            };
        }

        /// <summary>The rows listed (tests): the classic rows.</summary>
        internal IList<AxisMonitorRow> Rows { get { return _list.Controls.OfType<AxisMonitorRow>().ToList(); } }

        /// <summary>The rows listed as Axis Settings rows (an Admin's picks; tests).</summary>
        internal IList<AxisSettingHMI> SettingRows { get { return _list.Controls.OfType<AxisSettingHMI>().ToList(); } }

        /// <summary>The Admin's bar (tests).</summary>
        internal AxisPickBar Bar { get { return _bar; } }

        /// <summary>What the rows were built for: an Admin or not, the items, the module values.</summary>
        private static string Signature()
        {
            bool admin = SecurityManager.HasAccess(SecurityLevel.Admin);
            string axes = "|" + (ViewPreferences.Get(ViewPreferences.MonitorAxes) ?? "");
            if (!admin) return "user" + axes;
            var items = ViewPreferences.PickOf(ViewPreferences.MonitorItems);
            return "admin|" + (items == null ? "*" : string.Join(",", AxisSettingHMI.ItemKeys.Where(items.Contains))) + "|" +
                   string.Join(",", ViewPreferences.ListOf(ViewPreferences.MonitorMotion)) + axes;
        }

        /// <summary>An axis's key in the Axes list: its name and station.</summary>
        internal static string AxisKey(AlarmHelpLinks.AxisInfo a) { return (a.NameKey ?? "") + "@" + a.Station; }

        private void RebuildIfChanged()
        {
            if (IsDisposed || Signature() == _built) return;
            Build();
            Kaban.UI.Plc.PlcScanner.NotifyContentChanged();
        }

        /// <summary>
        /// The axes, grouped by machine when more than one machine has axes. As it
        /// was - name, position, current - unless an Admin has picked other PLC
        /// axis items or Motion monitor values: then each axis is its Axis Settings
        /// row again with what was picked (the position always), headings above.
        /// </summary>
        private void Build()
        {
            _built = Signature();
            _list.SuspendLayout();
            try
            {
                foreach (Control c in _list.Controls.Cast<Control>().ToList()) { _list.Controls.Remove(c); c.Dispose(); }
                foreach (Control c in _rowsHead.Controls.Cast<Control>().ToList()) { _rowsHead.Controls.Remove(c); c.Dispose(); }
                _rowsHead.Visible = false;
                AlarmHelpLinks.EnsureDiscovered();
                var axes = AlarmHelpLinks.AllAxes.ToList();
                _empty.Visible = axes.Count == 0;
                _list.Visible = axes.Count > 0;

                bool admin = SecurityManager.HasAccess(SecurityLevel.Admin);
                var copies = axes.Select(a => AxisSettingHMI.FromSnapshot(a.Row)).ToList();

                Func<AlarmHelpLinks.AxisInfo, string> machineOf = a =>
                {
                    var st = PlcManager.Instance.GetStation(a.Station);
                    string id = st != null ? st.OwnerModuleId : "";
                    return string.IsNullOrEmpty(id) ? "" : LanguageManager.GetString("Machine_" + id, id);
                };
                var machines = axes.Select(machineOf).Distinct().ToList();

                // The Axes list (everyone, from the Axis heading): which axes - the
                // machine before the name when more than one machine has axes. The PLC
                // axis items and the Motion monitor: an Admin's.
                _bar.HideLists(!admin);
                _bar.OfferAxes(axes.Select(a =>
                {
                    string name = LanguageManager.GetString(a.NameKey ?? "", a.NameKey ?? "");
                    string m = machineOf(a);
                    return new Kaban.UI.Dashboard.PickList.Item(AxisKey(a), machines.Count > 1 && m.Length > 0 ? m + " \u00B7 " + name : name);
                }));
                if (admin) _bar.Offer(AxisSettingHMI.ItemsOf(copies, monitor: true), copies.Any(c => c.HasMotion));
                _bar.Visible = admin && axes.Count > 0;
                axes = axes.Where(a => _bar.ShowsAxis(AxisKey(a))).ToList();
                machines = axes.Select(machineOf).Distinct().ToList();
                var items = ViewPreferences.PickOf(ViewPreferences.MonitorItems);
                bool classic = !admin || (items != null && items.Count == 1 && items.Contains("Torque") &&
                                          ViewPreferences.ListOf(ViewPreferences.MonitorMotion).Count == 0);
                _head.Visible = classic;
                foreach (var c in copies) c.Dispose();

                var rows = new List<AxisSettingHMI>();
                string current = null;
                foreach (var a in axes)
                {
                    string m = machineOf(a);
                    if (machines.Count > 1 && m != current)
                    {
                        current = m;
                        _list.Controls.Add(new Label
                        {
                            Text = m, AutoSize = false, Size = new Size(WName + WPosition + WMeter + 3 * Gap, 24),
                            Margin = new Padding(Left0, 8, 0, 0), TextAlign = ContentAlignment.BottomLeft,
                            Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(40, 46, 56)
                        });
                    }
                    if (classic)
                    {
                        _list.Controls.Add(new AxisMonitorRow(a) { Margin = new Padding(0, 0, 0, 0) });
                        continue;
                    }
                    var row = AxisSettingHMI.FromSnapshot(a.Row);
                    row.MonitorMode = true;
                    row.Margin = new Padding(0);
                    _list.Controls.Add(row);
                    rows.Add(row);
                }
                // Laid out once they are all there: a column is there when any axis has it.
                foreach (var r in rows) r.BuildRow();
                if (rows.Count > 0) AddHeadings(rows[0]);
                ShowMachineBars(axes);
            }
            finally { _list.ResumeLayout(); }
            FitBar();
        }

        /// <summary>The bars of the machines whose axes are listed - made the first time, kept after.</summary>
        private void ShowMachineBars(IEnumerable<AlarmHelpLinks.AxisInfo> axes)
        {
            var owners = new HashSet<string>(axes.Select(a =>
            {
                var st = PlcManager.Instance.GetStation(a.Station);
                return st != null ? st.OwnerModuleId ?? "" : "";
            }), StringComparer.OrdinalIgnoreCase);
            // one machine only (its stations may not name it): its bar
            var bars = Kaban.Core.Machines.MachineRegistry.AxisBars().ToList();
            foreach (var kv in bars)
            {
                string id = kv.Key.Module.ModuleId ?? "";
                bool wanted = owners.Contains(id) || (bars.Count == 1 && owners.Count > 0 && owners.All(o => o.Length == 0));
                MachineBarHMI bar;
                if (!_barOf.TryGetValue(id, out bar))
                {
                    if (!wanted) continue;
                    try
                    {
                        bar = kv.Value.CreateView() as MachineBarHMI;
                        if (bar == null) continue;
                        Kaban.UI.Helpers.StationMap.Apply(kv.Key.Context, bar);
                    }
                    catch (Exception ex) { AuditLogger.Log("axis bar of " + id + ": " + ex.Message); continue; }
                    bar.Margin = Padding.Empty;
                    _barOf[id] = bar;
                    _machineBars.Controls.Add(bar);
                }
                bar.Visible = wanted;
                if (wanted) _wantedBars.Add(bar); else _wantedBars.Remove(bar);
            }
            FitMachineBars();
        }

        /// <summary>Each bar as wide as the side page (its parts in rows when narrower), one under the other.</summary>
        private void FitMachineBars()
        {
            int y = _machineBars.Padding.Top, w = Math.Max(60, _machineBars.ClientSize.Width - _machineBars.Padding.Horizontal);
            foreach (var bar in _machineBars.Controls.OfType<MachineBarHMI>().Where(_wantedBars.Contains))
            {
                bar.Location = new Point(_machineBars.Padding.Left, y);
                y += bar.FitTo(w) + 4;
            }
            int h = y == _machineBars.Padding.Top ? 0 : y - 4 + _machineBars.Padding.Bottom;
            if (_machineBars.Height != h) _machineBars.Height = h;
        }

        /// <summary>The columns' headings, above the list: staying while the rows scroll up and down, following them sideways.</summary>
        private void AddHeadings(AxisSettingHMI first)
        {
            var head = new Panel { Name = "monitorHeadings", Margin = new Padding(0), BackColor = Color.White };
            _rowsHead.Controls.Add(head);
            _rowsHead.Height = UiScale.For(first, 22) + 1;
            _rowsHead.Visible = true;
            head.Size = new Size(first.Width, UiScale.For(first, 22));
            var font = new Font("Segoe UI", 8F, FontStyle.Bold);
            foreach (var kv in first.Columns.OrderBy(c => c.Value.X))
            {
                var l = new Label
                {
                    AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleCenter, Font = font,
                    ForeColor = Color.FromArgb(110, 116, 124), Tag = "AxisHeading:" + kv.Key
                };
                head.Controls.Add(l);
                l.SetBounds(UiScale.For(first, kv.Value.X), 0, UiScale.For(first, kv.Value.Y), head.Height);
                l.Text = AxisSettingHMI.HeadingText(kv.Key);
                var tip = AxisSettingHMI.HeadingTip(kv.Key);
                if (kv.Key == "Axis") AxisHeadingFilter.Attach(l, _bar.AxesPick);
                else if (tip != null) Kaban.UI.Controls.UiStyle.SetTip(UiStyle.Tips(), l, tip);
            }
            FollowRows();
        }

        /// <summary>The headings where the rows are: sideways with them (the first row's left, in the list's scroll).</summary>
        private void FollowRows()
        {
            var head = _rowsHead.Controls.Count > 0 ? _rowsHead.Controls[0] : null;
            var first = _list.Controls.OfType<AxisSettingHMI>().FirstOrDefault();
            if (head == null || first == null) return;
            if (head.Left != first.Left) head.Left = first.Left;
            // the axis names stay at the left as on the axis page, the Axis heading over them
            int s = -_list.AutoScrollPosition.X;
            foreach (var r in _list.Controls.OfType<AxisSettingHMI>()) r.StickName(s, r.Left + s);
            var axisHead = head.Controls.OfType<Label>().FirstOrDefault(l => (l.Tag as string) == "AxisHeading:Axis");
            if (axisHead != null) first.Stick(axisHead, s, first.Left + s, head.BackColor);
        }

        /// <summary>The bar as tall as its lists need in the panel's width (they wrap in a narrow panel).</summary>
        private void FitBar()
        {
            if (!_bar.Visible || _bar.Width <= 0) return;
            int h = _bar.GetPreferredSize(new Size(_bar.Width, 0)).Height;
            if (h > 0 && _bar.Height != h) _bar.Height = h;
        }
    }

    /// <summary>
    /// One axis on the monitor: its name, its position and its current, as the
    /// Axis Settings row draws them. Hidden while the row's visibility
    /// conditions are not met.
    /// </summary>
    public class AxisMonitorRow : Panel, IConditionalControl
    {
        public int PlcNumber { get; private set; }
        public SystemCondition VisibilityConditions { get; private set; }
        public string NameKey { get; private set; }

        internal readonly NumericDisplayHMI Position;
        internal readonly MeterHMI Current;
        /// <summary>The axis's name - to the side View Preferences' Captions say, as on the Axis Settings row.</summary>
        public readonly Label NameLabel;

        public AxisMonitorRow(AlarmHelpLinks.AxisInfo a)
        {
            PlcNumber = a.Station;
            VisibilityConditions = a.Visibility;
            NameKey = a.NameKey;
            Size = new Size(AxisMonitorPage.Left0 + AxisMonitorPage.WName + AxisMonitorPage.WPosition + AxisMonitorPage.WMeter + 4 * AxisMonitorPage.Gap + 8,
                            AxisSettingHMI.RowHeight);
            BackColor = Color.White;

            int x = AxisMonitorPage.Left0;
            var name = new Label
            {
                Text = LanguageManager.GetString(a.NameKey ?? "", a.NameKey ?? ""),
                Location = new Point(x, 8), Size = new Size(AxisMonitorPage.WName, 24),
                TextAlign = ViewPreferences.AxisNamesLeft ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular)
            };
            Controls.Add(name);
            NameLabel = name;
            ViewPreferences.Changed += OnViewPreferencesChanged;
            x += AxisMonitorPage.WName + AxisMonitorPage.Gap + AxisMonitorPage.Gap;

            Position = new NumericDisplayHMI
            {
                PlcNumber = a.Station,
                Address = a.Position,
                DataType = a.PositionType,
                Divisor = a.Divisor <= 0 ? 1 : a.Divisor,
                DecimalPlaces = a.Decimals,
                Digits = a.Digits,
                Suffix = a.Suffix,
                UnitType = a.Suffix == "mm" ? UnitType.Length : UnitType.None,
                ComparedAddress = a.LoadSideEncoder ?? "",
                ComparedDivisor = a.LoadSideEncoderDivisor,
                ComparedCaptionKey = "Tip_LoadEncoder",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            Position.SetBounds(x, 4, AxisMonitorPage.WPosition, AxisSettingHMI.RowHeight - 8);
            Controls.Add(Position);
            x += AxisMonitorPage.WPosition + AxisMonitorPage.Gap;

            if (!string.IsNullOrWhiteSpace(a.Current))
            {
                Current = new MeterHMI
                {
                    PlcNumber = a.Station,
                    Address = a.Current,
                    Divisor = a.CurrentDivisor <= 0 ? 1 : a.CurrentDivisor,
                    Caption = ""
                };
                Current.UseCurrentBands();
                if (a.CurrentFullScale > 0) Current.ScaleMaximum = a.CurrentFullScale;
                Current.SetBounds(x, 4, AxisMonitorPage.WMeter, AxisSettingHMI.RowHeight - 8);
                Controls.Add(Current);
            }
        }

        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            bool vis = VisibilityConditions == SystemCondition.None || checkGlobal(VisibilityConditions);
            if (Visible != vis) Visible = vis;
        }

        private void OnViewPreferencesChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnViewPreferencesChanged), sender, e); } catch { } return; }
            NameLabel.TextAlign = ViewPreferences.AxisNamesLeft ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ViewPreferences.Changed -= OnViewPreferencesChanged;
            base.Dispose(disposing);
        }
    }
}
