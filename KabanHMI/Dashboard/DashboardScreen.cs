using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// THE MACHINE DASHBOARD - the Industry 4 dashboard as a page of the HMI, so
    /// no second program is needed beside it: what the machine did over a range of
    /// time, from the log the logging service keeps (main_log / log_details).
    ///
    /// On the left, the range: an automatic report's (Auto-Report History), the
    /// latest 15 minutes to a year (Latest, \U0001F504 moves it up to now) or any
    /// start and end; a shift to look at alone (\u270f edits the list, \u26a1 compares
    /// them); the report files (\u2b07) and their folder (\U0001F4C2); the settings (\u2699).
    ///
    /// On the right, sections that open and close with their heading (and stay as
    /// they were left): the time in each state (pie), the states on a time line,
    /// the state log, the events by kind, the cuts, the events and the alarms -
    /// the last two with a filter of their kinds.
    ///
    /// The log is read on a thread of its own; the page stays usable meanwhile.
    /// Descriptions are in the HMI's language (the machine's Translation_JSON.csv).
    /// </summary>
    public partial class DashboardScreen : UserControl
    {
        private readonly MachineDashboard _machine;
        private readonly ToolTip _tips = UiStyle.Tips();

        // ---- the left side
        private readonly FlowLayoutPanel _side = new FlowLayoutPanel();
        private readonly Panel _main = new Panel();
        /// <summary>The log tabs' filters on the left: words, user, PLC, source (on show with a log tab).</summary>
        private readonly FlowLayoutPanel _logFilters = new FlowLayoutPanel();
        private Control _logSource, _logSourceCaption;
        private readonly ComboBox _history = new ComboBox();
        private readonly ComboBox _latest = new ComboBox();
        private Button _refresh;
        private readonly DateTimePicker _start = new DateTimePicker();
        private readonly DateTimePicker _end = new DateTimePicker();
        private readonly Label _rangeError = new Label();
        private readonly ComboBox _shift = new ComboBox();
        private Button _editShifts, _compareShifts;
        private readonly Panel _shiftEditor = new Panel();
        private readonly DataGridView _metrics = new DataGridView();
        private readonly Label _metricsEmpty = new Label();
        private Button _export, _openFolder, _settings;
        private readonly Label _status = new Label();
        private readonly List<Label> _sideCaptions = new List<Label>();

        // ---- the range shown
        private DateTime _from, _to;
        private DateTime[] _dbRange;
        private bool _setting;               // the controls are being set by the page, not the user

        // ---- the data
        private int _generation;
        private DashboardData.Loaded _loaded;
        private string _loadedKey = "";
        private DashboardView _view;
        private bool _loading;

        private readonly EventHandler _onLanguage, _onUser, _onConfig;
        private readonly EventHandler<DashboardReports.RunResult> _onReports;

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        public DashboardScreen(MachineDashboard machine)
        {
            _machine = machine ?? new MachineDashboard();
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;

            BuildMain();
            BuildSide();
            Controls.Add(_main);
            Controls.Add(_side);

            // the first range: the last day
            var now = Now();
            _to = DashboardConfig.Minute(now);
            _from = _to.AddDays(-1);

            _onLanguage = (s, e) => { if (IsHandleCreated) BeginInvoke(new Action(() => { Retranslate(); Reload(true); })); };
            _onUser = (s, e) => { if (IsHandleCreated) BeginInvoke(new Action(ApplyAccess)); };
            _onConfig = (s, e) => { if (IsHandleCreated) BeginInvoke(new Action(() => { FillShifts(); ApplySections(); Reload(true); })); };
            _onReports = (s, r) => { if (IsHandleCreated) BeginInvoke(new Action(RefreshHistory)); };
            LanguageManager.LanguageChanged += _onLanguage;
            SecurityManager.UserChanged += _onUser;
            _machine.ConfigChanged += _onConfig;
            _machine.ReportsMade += _onReports;

            Retranslate();
            ApplySections();
            ApplyAccess();
        }

        private DashboardConfig Config { get { return _machine.Config; } }
        private TimeZoneInfo Zone { get { return DashboardTime.Find(Config.TimeZoneName); } }
        private DateTime Now() { return DashboardTime.Now(Zone); }

        /// <summary>The language column of the descriptions (the HMI's language).</summary>
        private static string Column { get { return StateMap.ColumnOf(LanguageManager.CurrentLanguage); } }

        private bool _shown;

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible || _shown || !IsHandleCreated) return;
            _shown = true;
            SetRange(_from, _to, latest: "Daily");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Visible && !_shown) BeginInvoke(new Action(() => OnVisibleChanged(EventArgs.Empty)));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _generation++;
                LanguageManager.LanguageChanged -= _onLanguage;
                SecurityManager.UserChanged -= _onUser;
                _machine.ConfigChanged -= _onConfig;
                _machine.ReportsMade -= _onReports;
                _tips.Dispose();
            }
            base.Dispose(disposing);
        }

        // ==================================================================
        // THE LEFT SIDE
        // ==================================================================

        private const int SideWidth = 310;
        private const int SideInner = SideWidth - 28;

        /// <summary>A caption of the left side: its picture, then its text in the HMI's language.</summary>
        private Label SideCaption(string key, string icon, string text)
        {
            var l = new Label
            {
                AutoSize = false, Width = SideInner, Height = 22, Tag = new[] { key, text, icon },
                Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), Margin = new Padding(0, 10, 0, 2),
                TextAlign = ContentAlignment.BottomLeft
            };
            _sideCaptions.Add(l);
            return l;
        }

        private static Control Rule()
        {
            return new Label { AutoSize = false, Width = SideInner, Height = 1, BackColor = UiStyle.BorderColor, Margin = new Padding(0, 12, 0, 4) };
        }

        private static Panel Row(int height, params Control[] items)
        {
            var p = new Panel { Width = SideInner, Height = height, Margin = new Padding(0, 2, 0, 2) };
            p.Controls.AddRange(items);
            return p;
        }

        private void BuildSide()
        {
            _side.Dock = DockStyle.Left;
            _side.Width = SideWidth;
            _side.FlowDirection = FlowDirection.TopDown;
            _side.WrapContents = false;
            _side.AutoScroll = true;
            _side.Padding = new Padding(12, 8, 8, 8);
            _side.BackColor = Color.FromArgb(240, 242, 246);

            // auto-report history
            _history.DropDownStyle = ComboBoxStyle.DropDownList;
            _history.Width = SideInner;
            _history.Font = UiStyle.Body;
            _history.SelectedIndexChanged += (s, e) => { if (!_setting) FromHistory(); };
            _side.Controls.Add(SideCaption("Dsh_AutoHistory", "\u23f3", "Auto-Report History"));
            _side.Controls.Add(_history);

            // latest
            _latest.DropDownStyle = ComboBoxStyle.DropDownList;
            _latest.Width = SideInner - 50;
            _latest.Font = UiStyle.Body;
            _latest.Location = new Point(0, 0);
            _latest.SelectedIndexChanged += (s, e) => { if (!_setting) FromLatest(); };
            _refresh = UiStyle.ToolButton("\U0001F504", T("Dsh_Refresh", "Refresh the time interval"), _tips, (s, e) => FromLatest());
            _refresh.Location = new Point(SideInner - 44, -3);
            _refresh.Height = 30;
            _side.Controls.Add(SideCaption("Dsh_Latest", "\U0001F4C5", "Latest"));
            _side.Controls.Add(Row(28, _latest, _refresh));

            // start and end
            foreach (var p in new[] { _start, _end })
            {
                p.Format = DateTimePickerFormat.Custom;
                p.CustomFormat = "yyyy-MM-dd   HH:mm";
                p.Width = SideInner;
                p.Font = UiStyle.Body;
                p.ValueChanged += (s, e) => { if (!_setting) FromPickers(); };
            }
            _side.Controls.Add(SideCaption("Dsh_Start", "", "Start Date"));
            _side.Controls.Add(_start);
            _side.Controls.Add(SideCaption("Dsh_End", "", "End Date"));
            _side.Controls.Add(_end);
            _rangeError.AutoSize = false;
            _rangeError.Width = SideInner;
            _rangeError.Height = 22;
            _rangeError.ForeColor = Color.Firebrick;
            _rangeError.Visible = false;
            _side.Controls.Add(_rangeError);

            // the log tabs' filters - the log table's own boxes, laid out down the side
            // (put on the side under the shift, below)
            _logFilters.FlowDirection = FlowDirection.TopDown;
            _logFilters.WrapContents = false;
            _logFilters.AutoSize = true;
            _logFilters.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _logFilters.Margin = new Padding(0);
            _logFilters.Padding = new Padding(0);
            _logFilters.Visible = false;
            _logFilters.Controls.Add(Rule());
            _logFilters.Controls.Add(SideCaption("Dsh_LogSearch", "\uD83D\uDD0D", "Search the log"));
            _log.SearchBox.Width = SideInner;
            _log.SearchBox.Margin = new Padding(0, 2, 0, 2);
            _logFilters.Controls.Add(_log.SearchBox);
            foreach (var f in new[] { new object[] { "Log_User", "User", _log.UserBox }, new object[] { "Log_Plc", "PLC", _log.PlcBox },
                                      new object[] { "Log_Source", "Source", _log.SourceBox } })
            {
                var caption = SideCaption((string)f[0], "", (string)f[1]);
                caption.Margin = new Padding(0, 6, 0, 2);
                var box = (ComboBox)f[2];
                box.Width = SideInner;
                box.Margin = new Padding(0, 2, 0, 2);
                _logFilters.Controls.Add(caption);
                _logFilters.Controls.Add(box);
                if (box == _log.SourceBox) { _logSource = box; _logSourceCaption = caption; }
            }
            _log.ClearButton.Location = new Point(0, 0);
            _log.ExportButton.Location = new Point(50, 0);
            _logFilters.Controls.Add(Row(36, _log.ClearButton, _log.ExportButton));

            _side.Controls.Add(Rule());

            // the shift
            _shift.DropDownStyle = ComboBoxStyle.DropDownList;
            _shift.Width = SideInner - 100;
            _shift.Font = UiStyle.Body;
            _shift.SelectedIndexChanged += (s, e) => { if (!_setting) Reload(false); };
            _editShifts = UiStyle.ToolButton("\u270f\ufe0f", T("Dsh_EditList", "Edit the list"), _tips, (s, e) => ToggleShiftPanel(true));
            _compareShifts = UiStyle.ToolButton("\u26a1", T("Dsh_ShiftMetrics", "Shift metrics"), _tips, (s, e) => ToggleShiftPanel(false));
            _editShifts.Location = new Point(SideInner - 94, -3);
            _compareShifts.Location = new Point(SideInner - 46, -3);
            _editShifts.Height = _compareShifts.Height = 30;
            _side.Controls.Add(SideCaption("Dsh_ShiftSelection", "\u23f1\ufe0f", "Shift Selection (Optional)"));
            _side.Controls.Add(Row(28, _shift, _editShifts, _compareShifts));

            _shiftEditor.Width = SideInner;
            _shiftEditor.Visible = false;
            _shiftEditor.BorderStyle = BorderStyle.FixedSingle;
            _shiftEditor.BackColor = Color.White;
            _side.Controls.Add(_shiftEditor);
            // under the range and the shift, which the log tabs follow too
            _side.Controls.Add(_logFilters);

            _side.Controls.Add(Rule());

            // the reports
            _export = new Button { Width = SideInner, Height = 36, Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), Margin = new Padding(0, 8, 0, 4),
                                   BackColor = Color.FromArgb(255, 75, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            _export.FlatAppearance.BorderSize = 0;
            _export.Click += (s, e) => ExportNow();
            UiStyle.Round(_export);
            _openFolder = new Button { Width = SideInner, Height = 34, Margin = new Padding(0, 4, 0, 4) };
            _openFolder.Click += (s, e) => OpenFolder();
            UiStyle.Round(_openFolder);
            _settings = new Button { Width = SideInner, Height = 34, Margin = new Padding(0, 4, 0, 4) };
            _settings.Click += (s, e) => OpenSettings();
            UiStyle.Round(_settings);
            _side.Controls.Add(_export);
            _side.Controls.Add(_openFolder);
            _side.Controls.Add(_settings);

            _status.AutoSize = false;
            _status.Width = SideInner;
            _status.Height = 90;
            _status.ForeColor = Color.DimGray;
            _status.Margin = new Padding(0, 10, 0, 0);
            _side.Controls.Add(_status);

            FillShifts();
        }

        private void Retranslate()
        {
            foreach (var l in _sideCaptions)
            {
                var k = (string[])l.Tag;
                l.Text = (k[2].Length > 0 ? k[2] + " " : "") + T(k[0], k[1]);
            }
            _rangeError.Text = T("Dsh_EndAfterStart", "End date must be after start date!");
            _export.Text = "\u2b07\ufe0f " + T("Dsh_Export", "Export Report");
            _openFolder.Text = "\U0001F4C2 " + T("Dsh_OpenFolder", "Open Export Folder");
            _settings.Text = "\u2699\ufe0f " + T("Dsh_Settings", "Dashboard settings");
            _tips.SetToolTip(_refresh, T("Dsh_Refresh", "Refresh the time interval"));
            _tips.SetToolTip(_editShifts, T("Dsh_EditList", "Edit the list"));
            _tips.SetToolTip(_compareShifts, T("Dsh_ShiftMetrics", "Shift metrics"));
            _metricsEmpty.Text = T("Dsh_NoCompare", "No data available for comparison");
            _metricsTitle.Text = "\u26a1 " + T("Dsh_ShiftMetrics", "Shift metrics");
            FillLatest();
            FillHistoryItems();
            FillShifts();
            RetranslateMain();
        }

        private void ApplyAccess()
        {
            bool maint = SecurityManager.HasAccess(SecurityLevel.Maintenance);
            _editShifts.Enabled = maint;
            _openFolder.Enabled = maint;
            _settings.Enabled = maint;
            if (!maint && _shiftEditor.Visible) ToggleShiftPanel(true);
        }

        // ---- the period lists

        private static string PeriodKey(string label)
        {
            return "Dsh_P_" + new string((label ?? "").Where(char.IsLetterOrDigit).ToArray());
        }

        /// <summary>A period's name in the HMI's language ("Daily" -> "G\u00fcnl\u00fck").</summary>
        public static string PeriodText(string label) { return T(PeriodKey(label), label); }

        private List<DashboardConfig.Period> _periods = new List<DashboardConfig.Period>();

        private void FillLatest()
        {
            _setting = true;
            try
            {
                string was = _latest.SelectedIndex > 0 && _latest.SelectedIndex - 1 < _periods.Count ? _periods[_latest.SelectedIndex - 1].Label : null;
                _periods = Config.AllPeriods;
                _latest.Items.Clear();
                _latest.Items.Add(T("Dsh_SelectPeriod", "Select Period"));
                foreach (var p in _periods) _latest.Items.Add(PeriodText(p.Label));
                int i = was == null ? 0 : _periods.FindIndex(p => p.Label == was) + 1;
                _latest.SelectedIndex = Math.Max(0, i);
                _refresh.Enabled = _latest.SelectedIndex > 0;
            }
            finally { _setting = false; }
        }

        private List<DateTime> _historyEnds = new List<DateTime>();

        private void FillHistoryItems()
        {
            _setting = true;
            try
            {
                DateTime? was = _history.SelectedIndex > 0 && _history.SelectedIndex - 1 < _historyEnds.Count ? _historyEnds[_history.SelectedIndex - 1] : (DateTime?)null;
                _history.Items.Clear();
                _history.Items.Add(T("Dsh_SelectEnd", "Select End Date"));
                foreach (var d in _historyEnds) _history.Items.Add(d.ToString(DashboardConfig.DateFormatUi, CultureInfo.InvariantCulture));
                int i = was.HasValue ? _historyEnds.IndexOf(was.Value) + 1 : 0;
                _history.SelectedIndex = Math.Max(0, i);
            }
            finally { _setting = false; }
        }

        /// <summary>The report ends the schedule gives between the first and the last log (read on a thread).</summary>
        private void RefreshHistory()
        {
            var cfg = Config;
            var zone = Zone;
            string path = cfg.DatabasePath;
            var sched = cfg.Schedule;
            Task.Run(() =>
            {
                DateTime[] range = null;
                try { range = File.Exists(path) ? DashboardData.Range(path, zone) : null; } catch { }
                var ends = range == null ? new List<DateTime>() : sched.History(range[0], range[1]);
                return new Tuple<DateTime[], List<DateTime>>(range, ends);
            }).ContinueWith(t =>
            {
                if (IsDisposed || t.IsFaulted) return;
                _dbRange = t.Result.Item1;
                _historyEnds = t.Result.Item2;
                FillHistoryItems();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private List<DashboardConfig.Shift> _shifts = new List<DashboardConfig.Shift>();

        private void FillShifts()
        {
            _setting = true;
            try
            {
                string was = _shift.SelectedIndex > 0 ? _shift.SelectedItem as string : null;
                _shifts = Config.Shifts;
                _shift.Items.Clear();
                _shift.Items.Add(T("Dsh_AllShifts", "All Shifts"));
                foreach (var s in _shifts) _shift.Items.Add(s.Text);
                int i = was == null ? 0 : _shift.Items.IndexOf(was);
                _shift.SelectedIndex = Math.Max(0, i);
            }
            finally { _setting = false; }
            if (_shiftEditor.Visible) BuildShiftEditor();
        }

        /// <summary>The shift chosen (null: all).</summary>
        private DashboardConfig.Shift SelectedShift
        {
            get
            {
                int i = _shift.SelectedIndex - 1;
                return i >= 0 && i < _shifts.Count ? _shifts[i] : null;
            }
        }

        // ---- the three ways to choose the range

        private void FromHistory()
        {
            int i = _history.SelectedIndex - 1;
            if (i < 0 || i >= _historyEnds.Count) return;
            var end = _historyEnds[i];
            var start = Config.Schedule.StartOf(end);
            if (_dbRange != null && start < _dbRange[0]) start = _dbRange[0];
            SetRange(DashboardConfig.Minute(start), DashboardConfig.Minute(end), history: true);
        }

        private void FromLatest()
        {
            int i = _latest.SelectedIndex - 1;
            _refresh.Enabled = i >= 0;
            if (i < 0 || i >= _periods.Count) return;
            var end = DashboardConfig.Minute(Now());
            SetRange(_periods[i].StartBefore(end), end, latest: _periods[i].Label);
        }

        private void FromPickers()
        {
            var s = DashboardConfig.Minute(_start.Value);
            var e = DashboardConfig.Minute(_end.Value);
            _rangeError.Visible = e <= s;
            _setting = true;
            try
            {
                _history.SelectedIndex = 0;
                _latest.SelectedIndex = 0;
                _refresh.Enabled = false;
            }
            finally { _setting = false; }
            if (e > s) SetRange(s, e);
        }

        /// <summary>Shows a range (and says which list it came from, the other lists going back to their first line).</summary>
        public void SetRange(DateTime from, DateTime to, string latest = null, bool history = false)
        {
            _from = from;
            _to = to;
            _setting = true;
            try
            {
                if (!history) _history.SelectedIndex = _history.Items.Count > 0 ? 0 : -1;
                if (latest == null) _latest.SelectedIndex = _latest.Items.Count > 0 ? 0 : -1;
                else
                {
                    int i = _periods.FindIndex(p => p.Label == latest);
                    _latest.SelectedIndex = i + 1;
                }
                _refresh.Enabled = _latest.SelectedIndex > 0;
                _start.Value = Clamp(from);
                _end.Value = Clamp(to);
                _rangeError.Visible = false;
            }
            finally { _setting = false; }
            if (_historyEnds.Count == 0) RefreshHistory();
            Reload(false);
        }

        private static DateTime Clamp(DateTime t)
        {
            if (t < DateTimePicker.MinimumDateTime) return DateTimePicker.MinimumDateTime;
            if (t > DateTimePicker.MaximumDateTime) return DateTimePicker.MaximumDateTime;
            return t;
        }

        public DateTime RangeStart { get { return _from; } }
        public DateTime RangeEnd { get { return _to; } }

        // ---- the shift editor and the shift metrics

        /// <summary>
        /// \u270f opens the shift list under the shift choice; \u26a1 shows the shift metrics
        /// at the top of the page (a table wider than the left side).
        /// </summary>
        private void ToggleShiftPanel(bool editor)
        {
            if (editor)
            {
                bool show = !_shiftEditor.Visible;
                _shiftEditor.Visible = show;
                if (show) BuildShiftEditor();
                _editShifts.Text = show ? "\u270f\ufe0f\u25b4" : "\u270f\ufe0f";
            }
            else
            {
                _metricsShown = !_metricsShown;
                _compareShifts.Text = _metricsShown ? "\u26a1\u25b4" : "\u26a1";
                FillMetrics();
                FitWidths();
            }
        }

        private bool _metricsShown;

        private void BuildShiftEditor()
        {
            _shiftEditor.SuspendLayout();
            foreach (Control c in _shiftEditor.Controls.Cast<Control>().ToList()) { _shiftEditor.Controls.Remove(c); c.Dispose(); }
            int y = UiScale.Px(6), rowH = UiScale.Px(34);
            int w = _shiftEditor.ClientSize.Width;
            int pickW = (w - UiScale.Px(70)) / 2;
            var shifts = Config.Shifts;
            Func<TimeSpan, DateTimePicker> picker = t => new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = pickW, Font = UiStyle.Body,
                Value = DateTime.Today + t
            };
            for (int i = 0; i < shifts.Count; i++)
            {
                int index = i;
                var a = picker(shifts[i].Start);
                var b = picker(shifts[i].End);
                a.Location = new Point(UiScale.Px(6), y);
                b.Location = new Point(UiScale.Px(12) + pickW, y);
                EventHandler changed = (s, e) =>
                {
                    var list = Config.Shifts;
                    if (index >= list.Count) return;
                    list[index] = new DashboardConfig.Shift { Start = a.Value.TimeOfDay, End = b.Value.TimeOfDay };
                    list[index].Start = new TimeSpan(list[index].Start.Hours, list[index].Start.Minutes, 0);
                    list[index].End = new TimeSpan(list[index].End.Hours, list[index].End.Minutes, 0);
                    _machine.Change(c => c.Shifts = list);
                    FillShiftsKeepEditor();
                };
                a.ValueChanged += changed;
                b.ValueChanged += changed;
                var del = new Button { Text = "\u274c", Width = UiScale.Px(40), Height = a.Height, Location = new Point(w - UiScale.Px(46), y) };
                _tips.SetToolTip(del, T("Dsh_Delete", "Delete from the list"));
                del.Click += (s, e) =>
                {
                    var list = Config.Shifts;
                    if (index < list.Count) list.RemoveAt(index);
                    _machine.Change(c => c.Shifts = list);
                    FillShifts();
                    Reload(false);
                };
                _shiftEditor.Controls.AddRange(new Control[] { a, b, del });
                y += rowH;
            }
            var na = picker(TimeSpan.Zero);
            var nb = picker(TimeSpan.Zero);
            y += UiScale.Px(8);
            na.Location = new Point(UiScale.Px(6), y);
            nb.Location = new Point(UiScale.Px(12) + pickW, y);
            var add = new Button { Text = "\u2795", Width = UiScale.Px(40), Height = na.Height, Location = new Point(w - UiScale.Px(46), y) };
            _tips.SetToolTip(add, T("Dsh_Add", "Add to the list"));
            add.Click += (s, e) =>
            {
                var list = Config.Shifts;
                list.Add(new DashboardConfig.Shift
                {
                    Start = new TimeSpan(na.Value.Hour, na.Value.Minute, 0),
                    End = new TimeSpan(nb.Value.Hour, nb.Value.Minute, 0)
                });
                _machine.Change(c => c.Shifts = list);
                FillShifts();
            };
            _shiftEditor.Controls.AddRange(new Control[] { na, nb, add });
            _shiftEditor.Height = y + rowH + UiScale.Px(4);
            _shiftEditor.ResumeLayout();
        }

        /// <summary>The shift list changed from the editor: the choices follow, the editor stays as it is.</summary>
        private void FillShiftsKeepEditor()
        {
            _setting = true;
            try
            {
                int was = _shift.SelectedIndex;
                _shifts = Config.Shifts;
                _shift.Items.Clear();
                _shift.Items.Add(T("Dsh_AllShifts", "All Shifts"));
                foreach (var s in _shifts) _shift.Items.Add(s.Text);
                _shift.SelectedIndex = Math.Min(Math.Max(0, was), _shift.Items.Count - 1);
            }
            finally { _setting = false; }
            if (_shift.SelectedIndex > 0) Reload(false);
        }

        private void FillMetrics()
        {
            _metricsTitle.Visible = _metricsShown;
            if (!_metricsShown) { _metrics.Visible = _metricsEmpty.Visible = false; return; }
            var v = _view;
            var shifts = Config.Shifts;
            if (v == null || shifts.Count == 0 || v.Empty)
            {
                _metrics.Visible = false;
                _metricsEmpty.Visible = true;
                return;
            }
            var rows = DashboardData.Metrics(v, shifts);
            var kinds = v.Events.Select(e => e.Description).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            _metrics.Columns.Clear();
            _metrics.Columns.Add("shift", T("Dsh_Shift", "Shift"));
            _metrics.Columns.Add("active", T("Dsh_Active", "Active"));
            foreach (var k in kinds) _metrics.Columns.Add("", k);
            _metrics.Columns.Add("alarm", T("Dsh_Alarm", "Alarm"));
            foreach (DataGridViewColumn c in _metrics.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            foreach (var m in rows)
            {
                var cells = new List<object> { DashboardConfig.Hm(m.Shift.Start) + "-" + DashboardConfig.Hm(m.Shift.End),
                                               m.ActivePercent.ToString("0.0", CultureInfo.CurrentCulture) + "%" };
                foreach (var k in kinds)
                {
                    int n;
                    cells.Add(m.Events.TryGetValue(k, out n) ? n : 0);
                }
                cells.Add(m.Alarms);
                _metrics.Rows.Add(cells.ToArray());
            }
            _metrics.Height = _metrics.ColumnHeadersHeight + _metrics.Rows.Count * _metrics.RowTemplate.Height + UiScale.Px(22);
            _metrics.Visible = true;
            _metricsEmpty.Visible = false;
        }

        // ==================================================================
        // READING
        // ==================================================================

        /// <summary>
        /// Reads the range again (<paramref name="fromDatabase"/>), or only sorts what was
        /// read into the tables again (the shift or the barcode filter changed).
        /// </summary>
        public void Reload(bool fromDatabase)
        {
            if (!_shown) return;
            var cfg = Config;
            string path = cfg.DatabasePath;
            var zone = Zone;
            var map = _machine.Map;
            string column = Column;
            var from = _from;
            var to = _to;
            var shift = SelectedShift;
            bool barcoded = cfg.BarcodedCuttingOnly;
            string key = path + "|" + from.Ticks + "|" + to.Ticks + "|" + column + "|" + cfg.TimeZoneName;
            int gen = ++_generation;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                _loaded = null;
                _loadedKey = "";
                Show(null, T("Dsh_NoDatabase", "The database is not found:") + " " + (string.IsNullOrWhiteSpace(path) ? "-" : path) +
                           (SecurityManager.HasAccess(SecurityLevel.Maintenance) ? "\n" + T("Dsh_SetDatabase", "Choose it in the dashboard settings (\u2699).") : ""));
                return;
            }

            var cached = !fromDatabase && _loaded != null && _loadedKey == key ? _loaded : null;
            _loading = true;
            SetStatus(T("Dsh_Loading", "Loading data..."));
            var watch = Stopwatch.StartNew();
            Task.Run(() =>
            {
                var data = cached ?? DashboardData.Load(path, from, to, zone, map, column, () => gen != _generation);
                if (data == null) return null;
                var view = DashboardData.Build(data, barcoded, shift);
                // Nothing in the range: what the file holds says why (its logs end
                // earlier, it has none, its dates cannot be read).
                var probe = view.Empty ? DashboardData.Check(path, zone) : null;
                return new Tuple<DashboardData.Loaded, DashboardView, DashboardData.Probe>(data, view, probe);
            }).ContinueWith(t =>
            {
                if (IsDisposed || gen != _generation) return;
                _loading = false;
                if (t.IsFaulted)
                {
                    var ex = t.Exception.GetBaseException();
                    var many = ex as DashboardData.TooManyRowsException;
                    Show(null, many != null
                        ? string.Format(CultureInfo.CurrentCulture, T("Dsh_TooMany", "The range holds {0:#,0} records - too many to show at once. Choose a shorter range."), many.Rows)
                        : T("Dsh_DatabaseError", "The database could not be read:") + " " + ex.Message);
                    return;
                }
                if (t.Result == null) return;
                var probe = t.Result.Item3;
                _loaded = t.Result.Item1;
                _loadedKey = key;
                string empty = null;
                if (probe != null)
                    empty = probe.Ok
                        ? T("Dsh_NoLogs", "No logs in selected range!") + "\n" + T("Dsh_DatabaseHolds", "The database holds") + " " + probe.Describe() + "."
                        : probe.Problem;
                Show(t.Result.Item2, empty);
                SetStatus(string.Format(CultureInfo.CurrentCulture, T("Dsh_Records", "{0:#,0} records read in {1:0.0} s"),
                                        _loaded.Rows.Count, watch.Elapsed.TotalSeconds) + "\n" + path);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>True while the log is being read (tests wait for it).</summary>
        public bool Loading { get { return _loading; } }

        /// <summary>What the page shows now (tests).</summary>
        public DashboardView View { get { return _view; } }

        private void SetStatus(string text) { _status.Text = text ?? ""; }

        // ==================================================================
        // THE REPORT FILES, THE FOLDER, THE SETTINGS
        // ==================================================================

        /// <summary>Writes the report files of what is shown; the files made.</summary>
        public List<string> ExportNow()
        {
            var v = _view;
            if (v == null) { SetStatus(T("Dsh_NoLogs", "No logs in selected range!")); return new List<string>(); }
            List<string> files;
            try
            {
                files = DashboardReports.Save(Config, v, DashboardReports.Stamp(v.Start), DashboardReports.ManualEndStamp(v.End),
                                              _machine.Map, Config.DatabasePath, Zone);
            }
            catch (Exception ex) { SetStatus(ex.Message); return new List<string>(); }
            SetStatus(files.Count > 0
                ? "\u2705 " + string.Format(CultureInfo.CurrentCulture, T("Dsh_Exported", "{0} report files exported!"), files.Count) + "\n" + Config.ExportDirectory
                : "\u26a0\ufe0f " + T("Dsh_ExportNone", "No report file could be written - check the export folder in the settings."));
            try
            {
                AuditLogger.Log("Dashboard (" + _machine.ModuleId + "): exported " + files.Count + " report file(s) for " +
                                v.Start.ToString(DashboardConfig.DateFormatUi, CultureInfo.InvariantCulture) + " - " +
                                v.End.ToString(DashboardConfig.DateFormatUi, CultureInfo.InvariantCulture) + " to " + Config.ExportDirectory);
            }
            catch { }
            return files;
        }

        private void OpenFolder()
        {
            string dir = Config.ExportDirectory;
            try
            {
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo { FileName = Path.GetFullPath(dir), UseShellExecute = true });
            }
            catch (Exception ex) { SetStatus(T("Dsh_FolderError", "The folder could not be opened:") + " " + ex.Message); }
        }

        private void OpenSettings()
        {
            DashboardSettingsControl.ShowDialog(FindForm(), _machine);
        }
    }
}
