using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// The right side of the dashboard: THE TABS (2 October) -
    ///   Machine states      the time distribution, the timeline, the state log
    ///   Operations          the occurrences, the cuts, the event log
    ///   Alarms              the alarm log (the tab is the table: no heading)
    ///   the HMI's own log, a tab per kind (logins, connections, PLC writes,
    ///   settings, problems, other)
    ///   All logs            every entry of the range in one table: the HMI's and
    ///                       this machine's states, events and alarms
    /// The tabs' names carry no picture (as the other pages' tabs). A section's
    /// heading is a button as wide as its words, on the left, with a round sign
    /// that says it opens and closes; its filter is under it. The log tabs carry
    /// their search above the table; the user, controller and source are on the
    /// left with the range and the shift.
    /// </summary>
    public partial class DashboardScreen
    {
        private readonly TabControl _tabs = new TabControl();
        private readonly Dictionary<string, Panel> _flows = new Dictionary<string, Panel>();
        private readonly Dictionary<string, TabPage> _pages = new Dictionary<string, TabPage>();
        private readonly Label _info = new Label();

        private readonly PieChart _pieChart = new PieChart();
        private readonly TimelineChart _timelineChart = new TimelineChart();
        private readonly BarChart _barChart = new BarChart();
        private TableGrid _stateGrid, _cutGrid, _eventGrid, _alarmGrid;
        private FilterButton _eventFilter, _alarmFilter;
        private readonly Dictionary<string, Section> _sections = new Dictionary<string, Section>();

        /// <summary>The log tabs: their key, their caption and the kinds they list (none: every one).</summary>
        private sealed class LogTab
        {
            public string Key, TitleKey, Title;
            public Kaban.Core.Helpers.AuditKind[] Kinds;
        }

        private static readonly LogTab[] LogTabs =
        {
            new LogTab { Key = "log_signin", TitleKey = "Log_KindSignIn", Title = "Logins", Kinds = new[] { Kaban.Core.Helpers.AuditKind.SignIn } },
            new LogTab { Key = "log_connection", TitleKey = "Log_KindConnection", Title = "Connection", Kinds = new[] { Kaban.Core.Helpers.AuditKind.Connection } },
            new LogTab { Key = "log_writes", TitleKey = "Log_KindPlcWrite", Title = "PLC writes",
                         Kinds = new[] { Kaban.Core.Helpers.AuditKind.PlcWrite, Kaban.Core.Helpers.AuditKind.DeviceWrite } },
            new LogTab { Key = "log_settings", TitleKey = "Log_KindSettings", Title = "Settings", Kinds = new[] { Kaban.Core.Helpers.AuditKind.Settings } },
            new LogTab { Key = "log_problems", TitleKey = "Log_KindProblem", Title = "Problems", Kinds = new[] { Kaban.Core.Helpers.AuditKind.Problem } },
            new LogTab { Key = "log_other", TitleKey = "Log_KindOther", Title = "Other", Kinds = new[] { Kaban.Core.Helpers.AuditKind.Other } },
            new LogTab { Key = "log_all", TitleKey = "Dsh_TabAll", Title = "All logs", Kinds = null }
        };

        /// <summary>The three tabs of the machine's own log, and the sections each holds.</summary>
        private static readonly string[][] MachineTabs =
        {
            new[] { "states", "Dsh_TabStates", "Machine states", "state_pie", "state_timeline", "state_logs" },
            new[] { "events", "Dsh_TabEvents", "Operations", "event_quantity", "cutting_details", "event_logs" },
            new[] { "alarms", "Dsh_TabAlarms", "Alarms", "alarm_logs" }
        };

        /// <summary>One table for every log tab: moved to the tab chosen, its kinds set by it.</summary>
        private Kaban.UI.Settings.LogViewerControl _log;
        private bool _logStale = true;

        /// <summary>A tab of sections, one under the other, scrolling up and down.</summary>
        private FlowLayoutPanel NewFlow()
        {
            var f = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                Padding = new Padding(16, 10, 16, 16), BackColor = Color.White
            };
            // up and down only: the sections follow the page's width
            f.AutoScroll = false;
            f.HorizontalScroll.Enabled = false;
            f.HorizontalScroll.Visible = false;
            f.HorizontalScroll.Maximum = 0;
            f.AutoScroll = true;
            f.Layout += (s, e) => FitWidths();
            return f;
        }

        private void BuildMain()
        {
            _main.Dock = DockStyle.Fill;
            _main.BackColor = Color.White;

            // what the range holds when there is nothing to show: above the tabs
            _info.Dock = DockStyle.Top;
            _info.AutoSize = false;
            _info.Height = 48;
            _info.TextAlign = ContentAlignment.MiddleCenter;
            _info.BackColor = Color.FromArgb(232, 242, 252);
            _info.ForeColor = Color.FromArgb(0, 66, 128);
            _info.Visible = false;

            _tabs.Dock = DockStyle.Fill;
            UiStyle.StyleTabs(_tabs);
            _tabs.SelectedIndexChanged += (s, e) => TabChosen();

            _pieChart.Height = 300;
            _timelineChart.Height = 300;
            _barChart.Height = 340;

            _stateGrid = new TableGrid(320, "DashboardStates");
            _cutGrid = new TableGrid(300, "DashboardCuts");
            _eventGrid = new TableGrid(420, "DashboardEvents");
            _alarmGrid = new TableGrid(380, "DashboardAlarms");

            _eventFilter = new FilterButton(_tips);
            _alarmFilter = new FilterButton(_tips);
            _eventFilter.Changed += (s, e) => { OpenSection("event_logs"); FillEvents(); };
            _alarmFilter.Changed += (s, e) => FillAlarms();

            foreach (var t in MachineTabs)
            {
                // the alarms tab is its table: no headings to open, nothing to scroll
                Panel body = t[0] == "alarms" ? new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 10), BackColor = Color.White }
                                              : (Panel)NewFlow();
                _flows[t[0]] = body;
                var page = new TabPage { BackColor = Color.White, Tag = t[0] };
                page.Controls.Add(body);
                _pages[t[0]] = page;
            }
            AddSection("states", "state_pie", "\u23f1\ufe0f", "Dsh_StatePie", "Machine State Time Distribution", _pieChart, null);
            AddSection("states", "state_timeline", "\U0001F552", "Dsh_StateTimeline", "Machine State Timeline", _timelineChart, null);
            AddSection("states", "state_logs", "\U0001F4DC", "Dsh_StateLogs", "State Logs", _stateGrid, null);
            AddSection("events", "event_quantity", "\U0001F4C8", "Dsh_EventQuantity", "Event Occurrences (log scale)", _barChart, null);
            AddSection("events", "cutting_details", "\u2699\ufe0f", "Dsh_Cutting", "Cutting Details", _cutGrid, null);
            AddSection("events", "event_logs", "\U0001F4CB", "Dsh_EventLogs", "Event Logs", _eventGrid, _eventFilter);
            AddSection("alarms", "alarm_logs", "\U0001F6A8", "Dsh_AlarmLogs", "Alarm Logs", _alarmGrid, _alarmFilter, headless: true);

            foreach (var t in LogTabs)
                _pages[t.Key] = new TabPage { BackColor = Color.White, Tag = t.Key, Padding = new Padding(12, 8, 12, 6) };

            // the log tabs' one table: the HMI's log and this machine's
            _log = new Kaban.UI.Settings.LogViewerControl(null, Kaban.UI.Settings.LogViewerControl.Period.Today, machines: false, embedded: true);
            _log.Dock = DockStyle.Fill;
            _log.AddSource(_machine.ModuleId, MachineName);
            _log.ExtraEntries = MachineEntries;
            // the shift chosen: the HMI's entries of its hours too (the machine's are read for it already)
            _log.ExtraFilter = e =>
            {
                var shift = SelectedShift;
                return shift == null || !string.IsNullOrEmpty(e.Source) || DashboardData.InShift(e.Time.TimeOfDay, shift);
            };
            _log.StatusChanged += (s, e) => { if (LogTabs.Any(t => t.Key == SelectedTabKey)) UpdateBar(); };

            _main.Controls.Add(_tabs);
            _main.Controls.Add(_info);
            RebuildTabs();
        }

        /// <summary>This machine's name as the HMI shows it.</summary>
        private string MachineName
        {
            get
            {
                var l = Kaban.UI.Helpers.MachineLinks.Find(_machine.ModuleId);
                return l != null ? l.Name : _machine.ModuleId;
            }
        }

        /// <summary>This machine's states, events and alarms of the range, as log entries - from what the page read.</summary>
        private List<Kaban.Core.Helpers.AuditEntry> MachineEntries(DateTime from, DateTime to)
        {
            var v = _view;
            if (v == null) return new List<Kaban.Core.Helpers.AuditEntry>();
            var l = Kaban.UI.Helpers.MachineLinks.Find(_machine.ModuleId);
            return MachineLogFeed.ToEntries(v, from, to, l != null ? l.MainStation : 1, _machine.ModuleId);
        }

        private void AddSection(string tab, string key, string icon, string titleKey, string title, Control body, Control extra, bool headless = false)
        {
            var s = new Section(key, icon, titleKey, title, body, extra, headless);
            s.Toggled += (o, e) =>
            {
                _machine.Change(c => c.SetSectionOpen(key, s.Open));
                FitWidths();
            };
            _sections[key] = s;
            _flows[tab].Controls.Add(s);
        }

        /// <summary>A section of the page by its settings key (state_pie, event_logs ...), for tests.</summary>
        public Section SectionOf(string key)
        {
            Section s;
            return _sections.TryGetValue(key, out s) ? s : null;
        }

        /// <summary>The section opened (a filter ticked in it).</summary>
        private void OpenSection(string key)
        {
            var s = SectionOf(key);
            if (s != null && !s.Open) s.SetOpen(true, true);
        }

        /// <summary>The tabs, in their order: a machine tab only while one of its sections is switched on in the settings.</summary>
        private void RebuildTabs()
        {
            var chosen = SelectedTabKey;
            var want = new List<TabPage>();
            foreach (var t in MachineTabs)
                if (t.Skip(3).Any(k => _sections.ContainsKey(k) && _sections[k].Shown)) want.Add(_pages[t[0]]);
            foreach (var t in LogTabs) want.Add(_pages[t.Key]);
            if (_tabs.TabPages.Cast<TabPage>().SequenceEqual(want)) return;
            _settingTabs = true;
            try
            {
                _tabs.TabPages.Clear();
                foreach (var p in want) _tabs.TabPages.Add(p);
                var again = want.FirstOrDefault(p => (string)p.Tag == chosen);
                if (again != null) _tabs.SelectedTab = again;
            }
            finally { _settingTabs = false; }
            RetranslateTabs();
            TabChosen();
        }

        private bool _settingTabs;

        /// <summary>The chosen tab's key (states, events, alarms, log_signin ... log_all).</summary>
        public string SelectedTabKey { get { return _tabs.SelectedTab != null ? (string)_tabs.SelectedTab.Tag : ""; } }

        /// <summary>Chooses a tab by its key (tests).</summary>
        public void SelectTab(string key)
        {
            TabPage p;
            if (_pages.TryGetValue(key, out p) && _tabs.TabPages.Contains(p)) _tabs.SelectedTab = p;
        }

        public LogTabInfo[] TabKeys { get { return _tabs.TabPages.Cast<TabPage>().Select(p => new LogTabInfo { Key = (string)p.Tag, Text = p.Text }).ToArray(); } }
        public sealed class LogTabInfo { public string Key, Text; }

        /// <summary>The log table (tests).</summary>
        public Kaban.UI.Settings.LogViewerControl LogTable { get { return _log; } }

        private void TabChosen()
        {
            if (_settingTabs) return;
            var key = SelectedTabKey;
            var tab = LogTabs.FirstOrDefault(t => t.Key == key);
            if (tab != null)
            {
                if (_log.Parent != _pages[key]) _pages[key].Controls.Add(_log);
                _log.SetKinds(tab.Kinds);
                if (_logStale) RefreshLog();
            }
            FitWidths();
            UpdateBar();
        }

        /// <summary>The log table read again for the page's range (when a log tab is on show; else when one is chosen).</summary>
        private void RefreshLog()
        {
            if (_log == null) return;
            if (LogTabs.All(t => t.Key != SelectedTabKey)) { _logStale = true; return; }
            _logStale = false;
            // the files are read here (the first time a period is asked for): say so first
            _logReading = true;
            UpdateBar();
            _barText.Refresh();
            try { _log.SetRange(_from, _to); }
            finally { _logReading = false; }
            UpdateBar();
        }

        private void RetranslateTabs()
        {
            foreach (var t in MachineTabs) _pages[t[0]].Text = T(t[1], t[2]);
            foreach (var t in LogTabs) _pages[t.Key].Text = T(t.TitleKey, t.Title);
            _tabs.Invalidate();
        }

        /// <summary>Which sections are there (settings) and which are open (as they were left).</summary>
        private void ApplySections()
        {
            var cfg = Config;
            foreach (var kv in _sections)
            {
                kv.Value.Shown = cfg.SectionActive(kv.Key);
                kv.Value.Visible = kv.Value.Shown;
                kv.Value.SetOpen(cfg.SectionOpen(kv.Key), false);
            }
            RebuildTabs();
            FitWidths();
        }

        private void FitWidths()
        {
            foreach (var f in _flows.Values)
            {
                if (!(f is FlowLayoutPanel)) continue;
                // room for the scroll bar kept, so it never pushes the sections under a horizontal one
                int w = f.ClientSize.Width - f.Padding.Horizontal - 2 - (f.VerticalScroll.Visible ? 0 : SystemInformation.VerticalScrollBarWidth);
                if (w < 50) continue;
                foreach (Control c in f.Controls)
                {
                    int cw = w - c.Margin.Horizontal;
                    if (c.Width != cw) c.Width = cw;
                }
            }
        }

        private void RetranslateMain()
        {
            foreach (var s in _sections.Values) s.Retranslate();
            RetranslateTabs();
            _stateGrid.SetColumns(new TableGrid.Col("#", 50, "0"), new TableGrid.Col(T("Dsh_Timestamp", "Timestamp"), 170, "yyyy-MM-dd HH:mm:ss"),
                                  new TableGrid.Col(T("Dsh_Description", "Description"), 0, null), new TableGrid.Col(T("Dsh_Duration", "Duration (min)"), 140, "0.000"));
            _cutGrid.SetColumns(new TableGrid.Col("#", 50, "0"), new TableGrid.Col(T("Dsh_Length", "Length") + " (mm)", 0, "0.0"),
                                new TableGrid.Col(T("Dsh_LeftAngle", "Left Angle") + " (\u00b0)", 0, "0.0"),
                                new TableGrid.Col(T("Dsh_RightAngle", "Right Angle") + " (\u00b0)", 0, "0.0"),
                                new TableGrid.Col(T("Dsh_Quantity", "Quantity"), 0, "0"));
            _eventGrid.SetColumns(new TableGrid.Col("#", 60, "0"), new TableGrid.Col(T("Dsh_Timestamp", "Timestamp"), 170, "yyyy-MM-dd HH:mm:ss"),
                                  new TableGrid.Col(T("Dsh_Description", "Description"), 260, null), new TableGrid.Col(T("Dsh_Details", "Details"), 0, null));
            _alarmGrid.SetColumns(new TableGrid.Col("#", 60, "0"), new TableGrid.Col(T("Dsh_Timestamp", "Timestamp"), 170, "yyyy-MM-dd HH:mm:ss"),
                                  new TableGrid.Col(T("Dsh_Details", "Details"), 0, null));
            _eventFilter.Placeholder = T("Dsh_FilterEvents", "Select Events to Filter...");
            _alarmFilter.Placeholder = T("Dsh_FilterAlarms", "Select Alarms to Filter...");
            _eventFilter.ClearText = T("Dsh_AllEvents", "All Events");
            _alarmFilter.ClearText = T("Dsh_AllAlarms", "All Alarms");
            _timelineChart.EmptyText = T("Dsh_NoTimeline", "No state data to show timeline!");
            _pieChart.EmptyText = T("Dsh_NoPie", "No state data to show pie chart!");
            _barChart.EmptyText = T("Dsh_NoEvents", "No events to show!");
            _timelineChart.Hint = T("Dsh_TipTimeline", "Ctrl + wheel: zoom around the pointer - right-drag: zoom to an interval - drag: move along - double click: the whole range");
            _pieChart.Unit = " " + T("Dsh_Min", "min");
            if (_view != null) Show(_view, null);
        }

        // ==================================================================
        // SHOWING WHAT WAS READ
        // ==================================================================

        /// <summary>Shows a range's tables (null: none, with <paramref name="message"/> in their place).</summary>
        private void Show(DashboardView v, string message)
        {
            _view = v;
            if (message == null && (v == null || v.Empty)) message = T("Dsh_NoLogs", "No logs in selected range!");
            _info.Text = message ?? "";
            _info.Visible = !string.IsNullOrEmpty(message);

            v = v ?? new DashboardView { Start = _from, End = _to };
            FillStates(v);
            FillEventChart(v);
            FillCutting(v);
            _eventFilter.SetOptions(v.EventCounts);
            _alarmFilter.SetOptions(v.AlarmCounts);
            FillEvents();
            FillAlarms();
            // the log tabs: the same range, this machine's entries as just read
            RefreshLog();
            UpdateBar();
        }

        private void FillStates(DashboardView v)
        {
            // pie: the minutes of each state, in the order they first appear
            var slices = new List<PieChart.Slice>();
            foreach (var s in v.States)
            {
                var slice = slices.FirstOrDefault(x => x.Label == s.Description);
                if (slice == null) slices.Add(slice = new PieChart.Slice { Label = s.Description, Color = StateMap.ColorOf(s.Code) });
                slice.Value += s.Minutes;
            }
            _pieChart.Slices = slices;

            string min = T("Dsh_Min", "min");
            _timelineChart.Bars = v.States.Select(s => new TimelineChart.Bar
            {
                Row = s.Description, Start = s.Start, End = s.End, Color = StateMap.ColorOf(s.Code),
                Tip = s.Description + "\n" + s.Start.ToString(DashboardConfig.DateFormatUi, CultureInfo.InvariantCulture) + " - " +
                      s.End.ToString(DashboardConfig.DateFormatUi, CultureInfo.InvariantCulture) + "\n" +
                      s.Minutes.ToString("0.000", CultureInfo.CurrentCulture) + " " + min
            }).ToList();

            int n = 0;
            _stateGrid.SetRows(v.States.Select(s => new object[] { ++n, s.Start, s.Description, s.Minutes }).ToList(),
                               T("Dsh_NoStateLogs", "No machine state logs in selected time range!"));
        }

        private void FillEventChart(DashboardView v)
        {
            _barChart.Items = v.EventCounts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                               .Select(kv => new BarChart.Item { Label = kv.Key, Value = kv.Value }).ToList();
        }

        private void FillCutting(DashboardView v)
        {
            double m = v.TotalLengthMm / 1000.0;
            var s = SectionOf("cutting_details");
            if (s != null)
                s.Suffix = " (" + T("Dsh_TotalLength", "Total Length") + ": " + m.ToString("0.00", CultureInfo.CurrentCulture) + " m)";
            int n = 0;
            _cutGrid.SetRows(v.Cuts.Select(c => new object[] { ++n, c.Length, c.LeftAngle, c.RightAngle, c.Quantity }).ToList(),
                             m == 0.0 ? T("Dsh_NoCutting", "No cutting logs in selected range!")
                                      : T("Dsh_NoCuttingFormat", "No valid cutting details found (Check data format)."));
        }

        private void FillEvents()
        {
            var v = _view ?? new DashboardView();
            var pick = _eventFilter.Selected;
            var rows = pick.Count == 0 ? v.Events : v.Events.Where(e => pick.Contains(e.Description)).ToList();
            int n = 0;
            _eventGrid.SetRows(rows.Select(e => new object[] { ++n, e.Time, e.Description, e.Details }).ToList(),
                               T("Dsh_NoEventMatch", "No events match the selected filter!"));
            UpdateBar();
        }

        private void FillAlarms()
        {
            var v = _view ?? new DashboardView();
            var pick = _alarmFilter.Selected;
            var rows = pick.Count == 0 ? v.Alarms : v.Alarms.Where(a => DashboardData.AlarmNames(a.Details).Any(pick.Contains)).ToList();
            int n = 0;
            _alarmGrid.SetRows(rows.Select(a => new object[] { ++n, a.Time, a.Details }).ToList(),
                               T("Dsh_NoAlarms", "No alarm logs in selected range!"));
            UpdateBar();
        }

        // ---- for tests
        public PieChart Pie { get { return _pieChart; } }
        public TimelineChart Timeline { get { return _timelineChart; } }
        public BarChart EventBars { get { return _barChart; } }
        public TableGrid StateGrid { get { return _stateGrid; } }
        public TableGrid CutGrid { get { return _cutGrid; } }
        public TableGrid EventGrid { get { return _eventGrid; } }
        public TableGrid AlarmGrid { get { return _alarmGrid; } }
        public FilterButton EventFilter { get { return _eventFilter; } }
        public FilterButton AlarmFilter { get { return _alarmFilter; } }
        public ComboBox LatestBox { get { return _latest; } }
        public ComboBox HistoryBox { get { return _history; } }
        public ComboBox ShiftBox { get { return _shift; } }
        public string InfoText { get { return _info.Visible ? _info.Text : ""; } }
        /// <summary>The left bar's width and where Export Report is (tests).</summary>
        public int FilterBarWidth { get { return _sideHost.Width; } }
        public Control ExportButton { get { return _export; } }
        public Control FolderButton { get { return _openFolder; } }

        // ==================================================================
        // THE PARTS
        // ==================================================================

        /// <summary>
        /// A section: its heading - a button as wide as its words, on the left, with a
        /// round sign that opens and closes it - its filter under it (when it has one),
        /// then what it shows. A HEADLESS section is a tab on its own (the alarms): no
        /// heading, always open, its filter at the top left, its table filling the tab.
        /// </summary>
        public sealed class Section : Panel
        {
            public readonly string Key;
            private readonly string _icon, _titleKey, _title;
            private readonly SectionHead _head;
            private readonly Panel _headRow, _extraRow, _spacer;
            private readonly Control _body;
            public readonly bool Headless;
            public bool Open { get; private set; }
            /// <summary>Switched on in the settings (Visible also needs its tab on show).</summary>
            public bool Shown { get; set; } = true;
            public event EventHandler Toggled;
            private string _suffix = "";

            public Section(string key, string icon, string titleKey, string title, Control body, Control extra, bool headless = false)
            {
                Key = key;
                _icon = icon;
                _titleKey = titleKey;
                _title = title;
                _body = body;
                Headless = headless;
                BackColor = Color.White;
                if (extra != null)
                {
                    _extraRow = new Panel { Dock = DockStyle.Top, Height = UiScale.Px(40), BackColor = Color.White };
                    extra.Location = new Point(0, UiScale.Px(3));
                    extra.Width = UiScale.Px(420);
                    extra.Height = UiScale.Px(32);
                    _extraRow.Controls.Add(extra);
                }
                if (headless)
                {
                    Dock = DockStyle.Fill;
                    Open = true;
                    body.Dock = DockStyle.Fill;
                    Controls.Add(body);
                    if (_extraRow != null) Controls.Add(_extraRow);
                    return;
                }
                Margin = new Padding(0, 2, 0, UiScale.Px(18));
                _headRow = new Panel { Dock = DockStyle.Top, Height = UiScale.Px(36), BackColor = Color.White };
                _head = new SectionHead { Location = new Point(0, UiScale.Px(2)) };
                _head.Click += (s, e) => SetOpen(!Open, true);
                _headRow.Controls.Add(_head);
                _spacer = new Panel { Dock = DockStyle.Top, Height = UiScale.Px(4), BackColor = Color.White };
                body.Dock = DockStyle.Top;
                Controls.Add(body);
                Controls.Add(_spacer);
                if (_extraRow != null) Controls.Add(_extraRow);
                Controls.Add(_headRow);
                Height = _headRow.Height;
                Retranslate();
            }

            /// <summary>Written after the title (the cut length's total).</summary>
            public string Suffix
            {
                get { return _suffix; }
                set { _suffix = value ?? ""; Retranslate(); }
            }

            /// <summary>The heading's words (the picture, the title, the suffix).</summary>
            public string Title { get { return _icon + " " + LanguageManager.GetString(_titleKey, _title) + _suffix; } }

            public void Retranslate()
            {
                if (_head == null) return;
                _head.Caption = Title;
                _head.Open = Open;
            }

            public void SetOpen(bool open, bool byUser)
            {
                if (Headless) return;
                Open = open;
                _body.Visible = open;
                if (_extraRow != null) _extraRow.Visible = open;
                Height = _headRow.Height + (open ? _body.Height + _spacer.Height + (_extraRow != null ? _extraRow.Height : 0) : 0);
                Retranslate();
                if (byUser)
                {
                    var h = Toggled;
                    if (h != null) h(this, EventArgs.Empty);
                }
            }

            /// <summary>Clicks the heading (tests).</summary>
            public void ClickHead() { if (!Headless) SetOpen(!Open, true); }

            /// <summary>The heading's control (tests: as wide as its words, on the left).</summary>
            public Control Head { get { return _head; } }
        }

        /// <summary>
        /// A section's heading: its words in bold after a round blue sign with a white
        /// arrow - right when closed, down when open - so it reads as a button; no
        /// frame; a light background under the pointer. As wide as its words.
        /// </summary>
        public sealed class SectionHead : Control
        {
            private string _caption = "";
            private bool _open, _hover;

            public SectionHead()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
                Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);
                BackColor = Color.White;
                Cursor = Cursors.Hand;
                TabStop = true;
            }

            public string Caption
            {
                get { return _caption; }
                set { _caption = value ?? ""; Text = _caption; Fit(); Invalidate(); }
            }

            public bool Open
            {
                get { return _open; }
                set { if (_open != value) { _open = value; Invalidate(); } }
            }

            private int Sign { get { return UiScale.Px(20); } }

            private void Fit()
            {
                var size = TextRenderer.MeasureText(_caption, Font);
                Width = UiScale.Px(8) + Sign + UiScale.Px(8) + size.Width + UiScale.Px(10);
                Height = Math.Max(size.Height + UiScale.Px(10), UiScale.Px(32));
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(BackColor);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (_hover || Focused)
                    using (var b = new SolidBrush(Color.FromArgb(236, 242, 250)))
                    using (var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), UiScale.Px(5)))
                        g.FillPath(b, path);
                int d = Sign, x = UiScale.Px(8), y = (Height - d) / 2;
                using (var b = new SolidBrush(_hover ? Color.FromArgb(20, 70, 135) : Color.FromArgb(31, 95, 168)))
                    g.FillEllipse(b, x, y, d, d);
                float cx = x + d / 2f, cy = y + d / 2f, a = d * 0.2f;
                using (var pen = new Pen(Color.White, Math.Max(2f, d / 9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    if (_open) g.DrawLines(pen, new[] { new PointF(cx - a, cy - a / 2), new PointF(cx, cy + a / 2), new PointF(cx + a, cy - a / 2) });
                    else g.DrawLines(pen, new[] { new PointF(cx - a / 2, cy - a), new PointF(cx + a / 2, cy), new PointF(cx - a / 2, cy + a) });
                }
                var text = new Rectangle(x + d + UiScale.Px(8), 0, Width - (x + d + UiScale.Px(8)), Height);
                TextRenderer.DrawText(g, _caption, Font, text, UiStyle.GridInk, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            private static GraphicsPath Rounded(Rectangle r, int radius)
            {
                var p = new GraphicsPath();
                int d = radius * 2;
                p.AddArc(r.Left, r.Top, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                return p;
            }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
            protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
            protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
            }
        }

        /// <summary>
        /// A read-only table that is filled with thousands of rows at once: the grid
        /// asks for the cells it shows (virtual mode). A click on a heading sorts by that
        /// column; the # stays with its row. Empty, it says why.
        /// </summary>
        public sealed class TableGrid : Panel
        {
            public sealed class Col
            {
                public string Text; public int Width; public string Format;
                public Col(string text, int width, string format) { Text = text; Width = width; Format = format; }
            }

            public readonly DataGridView Grid = new DataGridView();
            private readonly Label _empty = new Label();
            private List<object[]> _rows = new List<object[]>();
            private int _sortColumn = -1;
            private bool _descending;
            /// <summary>The name its columns are kept under (the program's column menu); null: none.</summary>
            private readonly string _key;
            private bool _tools;

            public TableGrid(int height, string key = null)
            {
                _key = key;
                Height = height;
                Grid.Dock = DockStyle.Fill;
                Grid.VirtualMode = true;
                Grid.ReadOnly = true;
                Grid.AllowUserToOrderColumns = false;
                Grid.MultiSelect = true;
                UiStyle.StyleGrid(Grid);
                Grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
                Grid.CellValueNeeded += (s, e) =>
                {
                    if (e.RowIndex >= 0 && e.RowIndex < _rows.Count && e.ColumnIndex < _rows[e.RowIndex].Length)
                        e.Value = _rows[e.RowIndex][e.ColumnIndex];
                };
                // a LEFT click sorts; the right one opens the columns' menu
                Grid.ColumnHeaderMouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Sort(e.ColumnIndex); };
                _empty.Dock = DockStyle.Fill;
                _empty.TextAlign = ContentAlignment.MiddleCenter;
                _empty.BackColor = Color.FromArgb(232, 242, 252);
                _empty.ForeColor = Color.FromArgb(0, 66, 128);
                _empty.Visible = false;
                Controls.Add(Grid);
                Controls.Add(_empty);
            }

            public void SetColumns(params Col[] cols)
            {
                Grid.Columns.Clear();
                int n = 0;
                foreach (var c in cols)
                {
                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "c" + (n++), HeaderText = c.Text, SortMode = DataGridViewColumnSortMode.Programmatic,
                        AutoSizeMode = c.Width > 0 ? DataGridViewAutoSizeColumnMode.None : DataGridViewAutoSizeColumnMode.Fill
                    };
                    if (c.Width > 0) col.Width = UiScale.For(this, c.Width);
                    if (c.Format != null) col.DefaultCellStyle.Format = c.Format;
                    if (c.Format != null && c.Format.StartsWith("0", StringComparison.Ordinal))
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    Grid.Columns.Add(col);
                }
                Grid.RowCount = _rows.Count;
                // The program's column menu, and the columns as they were left.
                if (_key != null && !_tools) { UiStyle.EnableTableTools(Grid, _key, sorting: false); _tools = true; }
                else if (_tools) UiStyle.ReapplyLayout(Grid, _key);
            }

            public void SetRows(List<object[]> rows, string emptyText)
            {
                _rows = rows ?? new List<object[]>();
                _sortColumn = -1;
                foreach (DataGridViewColumn c in Grid.Columns) c.HeaderCell.SortGlyphDirection = SortOrder.None;
                Grid.ClearSelection();
                UiStyle.SetRowCount(Grid, _rows.Count);
                Grid.Invalidate();
                _empty.Text = emptyText ?? "";
                _empty.Visible = _rows.Count == 0;
                Grid.Visible = _rows.Count > 0;
            }

            public int RowCount { get { return _rows.Count; } }
            public object Cell(int row, int col) { return _rows[row][col]; }
            public string EmptyText { get { return _empty.Visible ? _empty.Text : ""; } }

            public void Sort(int column)
            {
                if (column < 0 || column >= Grid.Columns.Count) return;
                _descending = column == _sortColumn ? !_descending : false;
                _sortColumn = column;
                var cmp = Comparer<object>.Create((a, b) =>
                {
                    if (a == null) return b == null ? 0 : -1;
                    if (b == null) return 1;
                    var ca = a as IComparable;
                    if (ca != null && a.GetType() == b.GetType()) return ca.CompareTo(b);
                    return string.Compare(Convert.ToString(a, CultureInfo.CurrentCulture), Convert.ToString(b, CultureInfo.CurrentCulture), StringComparison.CurrentCulture);
                });
                // stable: rows that compare equal keep their order
                var keyed = _rows.Select((r, i) => new { r, i }).ToList();
                keyed.Sort((x, y) =>
                {
                    int c = cmp.Compare(x.r[column], y.r[column]);
                    if (_descending) c = -c;
                    return c != 0 ? c : x.i.CompareTo(y.i);
                });
                _rows = keyed.Select(k => k.r).ToList();
                foreach (DataGridViewColumn c in Grid.Columns) c.HeaderCell.SortGlyphDirection = SortOrder.None;
                Grid.Columns[column].HeaderCell.SortGlyphDirection = _descending ? SortOrder.Descending : SortOrder.Ascending;
                Grid.Invalidate();
            }
        }

        /// <summary>
        /// A filter of kinds: a button that opens a list of the kinds found, each with
        /// how many there are, to tick; nothing ticked shows everything.
        /// </summary>
        public sealed class FilterButton : Button
        {
            private readonly ToolTip _tips;
            private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.Ordinal);
            private Dictionary<string, int> _counts = new Dictionary<string, int>();
            public string Placeholder = "";
            public string ClearText = "";
            public event EventHandler Changed;

            public FilterButton(ToolTip tips)
            {
                _tips = tips;
                TextAlign = ContentAlignment.MiddleLeft;
                AutoEllipsis = true;
                Font = UiStyle.Body;
                Click += (s, e) => Drop();
                UiStyle.Round(this);
            }

            public HashSet<string> Selected { get { return _selected; } }

            /// <summary>The kinds found and their counts; ticks of kinds no longer there are dropped.</summary>
            public void SetOptions(Dictionary<string, int> counts)
            {
                _counts = counts ?? new Dictionary<string, int>();
                _selected.RemoveWhere(k => !_counts.ContainsKey(k));
                Caption();
            }

            /// <summary>The list as the old filter showed it: "name (count)", by name.</summary>
            public List<string> Options
            {
                get { return _counts.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => k + " (" + _counts[k] + ")").ToList(); }
            }

            public void Select(IEnumerable<string> kinds)
            {
                _selected.Clear();
                foreach (var k in kinds ?? new string[0]) if (_counts.ContainsKey(k)) _selected.Add(k);
                Caption();
                var h = Changed;
                if (h != null) h(this, EventArgs.Empty);
            }

            private void Caption()
            {
                Text = (_selected.Count == 0 ? Placeholder : string.Join(", ", _selected.OrderBy(k => k, StringComparer.Ordinal))) + "  \u25be";
                if (_tips != null) _tips.SetToolTip(this, _selected.Count == 0 ? Placeholder : string.Join("\n", _selected.OrderBy(k => k, StringComparer.Ordinal)));
            }

            private void Drop()
            {
                var keys = _counts.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
                var list = new CheckedListBox
                {
                    CheckOnClick = true, BorderStyle = BorderStyle.None, Font = UiStyle.Body, IntegralHeight = false,
                    Width = Math.Max(Width, UiScale.Px(320)),
                    Height = Math.Min(UiScale.Px(360), Math.Max(UiScale.Px(40), keys.Count * UiScale.Px(22) + UiScale.Px(8)))
                };
                foreach (var k in keys) list.Items.Add(k + " (" + _counts[k] + ")", _selected.Contains(k));
                var clear = new Button { Text = ClearText, Width = list.Width, Height = UiScale.Px(30), Font = UiStyle.Body, FlatStyle = FlatStyle.Flat };
                var panel = new Panel { Width = list.Width, Height = list.Height + clear.Height, BackColor = Color.White };
                clear.Dock = DockStyle.Bottom;
                list.Dock = DockStyle.Fill;
                panel.Controls.Add(list);
                panel.Controls.Add(clear);
                var host = new ToolStripControlHost(panel) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = panel.Size };
                var drop = new ToolStripDropDown { Padding = Padding.Empty };
                drop.Items.Add(host);
                list.ItemCheck += (s, e) => BeginInvoke(new Action(() =>
                {
                    _selected.Clear();
                    foreach (int i in list.CheckedIndices) _selected.Add(keys[i]);
                    Caption();
                    var h = Changed;
                    if (h != null) h(this, EventArgs.Empty);
                }));
                clear.Click += (s, e) =>
                {
                    for (int i = 0; i < list.Items.Count; i++) list.SetItemChecked(i, false);
                    drop.Close();
                };
                drop.Closed += (s, e) => BeginInvoke(new Action(() => { drop.Dispose(); }));
                UiStyle.ShowPopup(drop, this, new Point(0, Height));
            }
        }
    }
}
