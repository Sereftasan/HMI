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
    /// On the left, the filters (2 October: no pictures on their captions, a
    /// narrower bar): the range - an automatic report's (Auto-Report History), the
    /// latest 15 minutes to a year (Latest; choosing it again moves it up to now)
    /// or any start and end -, the shift, then the user, the controller and the
    /// source of the log tabs. At the foot of the bar, wherever the filters end:
    /// Export Report and, beside it, the folder the reports go to.
    ///
    /// On the right, the tabs (DashboardScreen.Sections.cs) and, at the foot of
    /// the page, the info bar: "Data loading..." in red while the log is read,
    /// then the log interval, the shift and the entries the tab on show lists.
    /// The shifts and the rest of the settings are set in HMI Settings > Logs and
    /// Reports.
    ///
    /// The log is read on a thread of its own; the page stays usable meanwhile.
    /// Descriptions are in the HMI's language (the machine's Translation_JSON.csv).
    /// </summary>
    public partial class DashboardScreen : UserControl
    {
        private readonly MachineDashboard _machine;
        private readonly ToolTip _tips = UiStyle.Tips();

        // ---- the left side: the filters, the report at its foot
        private readonly Panel _sideHost = new Panel();
        private readonly FlowLayoutPanel _side = new FlowLayoutPanel();
        private readonly Panel _sideFoot = new Panel();
        private readonly Panel _main = new Panel();
        private readonly ComboBox _history = new ComboBox();
        private readonly ComboBox _latest = new ComboBox();
        private int _latestOpenedAt = -1;
        private readonly DateTimePicker _start = new DateTimePicker();
        private readonly DateTimePicker _end = new DateTimePicker();
        private readonly Label _rangeError = new Label();
        private readonly ComboBox _shift = new ComboBox();
        private Button _export, _openFolder;
        private readonly List<Label> _sideCaptions = new List<Label>();

        // ---- the info bar at the foot of the page
        private readonly Panel _bar = new Panel();
        private readonly Label _barText = new Label();
        private readonly Label _barNote = new Label();

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
        private bool _logReading;            // the log tab's files are being read

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
            BuildBar();
            Controls.Add(_main);
            Controls.Add(_sideHost);

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

        private const int SideWidth = 228;
        private const int SideInner = SideWidth - 22;

        /// <summary>A caption of the left side, in the HMI's language (no picture: none of them has one).</summary>
        private Label SideCaption(string key, string text)
        {
            var l = new Label
            {
                AutoSize = false, Width = SideInner, Height = 22, Tag = new[] { key, text },
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

        private ComboBox SideBox(ComboBox box)
        {
            box.DropDownStyle = ComboBoxStyle.DropDownList;
            box.Width = SideInner;
            box.Font = UiStyle.Body;
            box.Margin = new Padding(0, 2, 0, 2);
            return box;
        }

        private void BuildSide()
        {
            var back = Color.FromArgb(240, 242, 246);
            _sideHost.Dock = DockStyle.Left;
            _sideHost.Width = SideWidth;
            _sideHost.BackColor = back;

            _side.Dock = DockStyle.Fill;
            _side.FlowDirection = FlowDirection.TopDown;
            _side.WrapContents = false;
            _side.AutoScroll = true;
            _side.Padding = new Padding(10, 6, 6, 8);
            _side.BackColor = back;

            // the range: an automatic report's, the latest, or any start and end
            SideBox(_history).SelectedIndexChanged += (s, e) => { if (!_setting) FromHistory(); };
            _side.Controls.Add(SideCaption("Dsh_AutoHistory", "Auto-Report History"));
            _side.Controls.Add(_history);

            SideBox(_latest).SelectedIndexChanged += (s, e) => { if (!_setting) FromLatest(); };
            // the same period chosen again: up to now (there is no refresh button beside it)
            _latest.DropDown += (s, e) => _latestOpenedAt = _latest.SelectedIndex;
            _latest.DropDownClosed += (s, e) =>
            {
                if (_latest.SelectedIndex > 0 && _latest.SelectedIndex == _latestOpenedAt) BeginInvoke(new Action(FromLatest));
                _latestOpenedAt = -1;
            };
            _side.Controls.Add(SideCaption("Dsh_Latest", "Latest"));
            _side.Controls.Add(_latest);

            foreach (var p in new[] { _start, _end })
            {
                p.Format = DateTimePickerFormat.Custom;
                p.CustomFormat = "yyyy-MM-dd  HH:mm";
                p.Width = SideInner;
                p.Font = UiStyle.Body;
                p.Margin = new Padding(0, 2, 0, 2);
                p.ValueChanged += (s, e) => { if (!_setting) FromPickers(); };
            }
            _side.Controls.Add(SideCaption("Dsh_Start", "Start Date"));
            _side.Controls.Add(_start);
            _side.Controls.Add(SideCaption("Dsh_End", "End Date"));
            _side.Controls.Add(_end);
            _rangeError.AutoSize = false;
            _rangeError.Width = SideInner;
            _rangeError.Height = 36;
            _rangeError.ForeColor = Color.Firebrick;
            _rangeError.Visible = false;
            _side.Controls.Add(_rangeError);

            // the shift: one combo (the list is set in HMI Settings > Logs and Reports)
            SideBox(_shift).SelectedIndexChanged += (s, e) => { if (!_setting) Reload(false); };
            _side.Controls.Add(SideCaption("Dsh_ShiftSelection", "Shift Selection"));
            _side.Controls.Add(_shift);

            // who and where: on every tab (the log tabs filter by them)
            _side.Controls.Add(Rule());
            foreach (var f in new[] { new object[] { "Log_User", "User", _log.UserBox }, new object[] { "Log_Plc", "PLC", _log.PlcBox },
                                      new object[] { "Log_Source", "Source", _log.SourceBox } })
            {
                var caption = SideCaption((string)f[0], (string)f[1]);
                caption.Margin = new Padding(0, 6, 0, 2);
                _side.Controls.Add(caption);
                _side.Controls.Add(SideBox((ComboBox)f[2]));
            }

            // the foot of the bar: Export Report and its folder, wherever the filters end
            _sideFoot.Dock = DockStyle.Bottom;
            _sideFoot.Height = 52;
            _sideFoot.BackColor = back;
            _openFolder = UiStyle.ToolButton("\U0001F4C2", T("Dsh_ShowInFolder", "Show in folder"), _tips, (s, e) => OpenFolder());
            _openFolder.Height = 34;
            _export = new Button
            {
                Height = 34, Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold),
                BackColor = Color.FromArgb(255, 75, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
            };
            _export.FlatAppearance.BorderSize = 0;
            _export.Click += (s, e) => ExportNow();
            UiStyle.Round(_export);
            _export.Location = new Point(10, 9);
            _export.Width = SideInner - _openFolder.Width - 6;
            _openFolder.Location = new Point(_export.Right + 6, 9);
            _sideFoot.Controls.Add(_export);
            _sideFoot.Controls.Add(_openFolder);
            _sideFoot.Paint += (s, e) => { using (var pen = new Pen(UiStyle.BorderColor)) e.Graphics.DrawLine(pen, 10, 0, _sideFoot.Width - 8, 0); };

            _sideHost.Controls.Add(_side);
            _sideHost.Controls.Add(_sideFoot);

            FillShifts();
        }

        /// <summary>The info bar at the foot of the page: what is shown, or that it is being read.</summary>
        private void BuildBar()
        {
            _bar.Dock = DockStyle.Bottom;
            _bar.Height = 28;
            _bar.BackColor = Color.FromArgb(248, 249, 251);
            _bar.Padding = new Padding(12, 0, 12, 0);
            _bar.Paint += (s, e) => { using (var pen = new Pen(UiStyle.GridRule)) e.Graphics.DrawLine(pen, 0, 0, _bar.Width, 0); };
            _barText.Dock = DockStyle.Fill;
            _barText.TextAlign = ContentAlignment.MiddleLeft;
            _barText.AutoEllipsis = true;
            _barText.ForeColor = UiStyle.GridInk;
            _barNote.Dock = DockStyle.Right;
            _barNote.AutoSize = true;
            _barNote.TextAlign = ContentAlignment.MiddleRight;
            _barNote.ForeColor = Color.FromArgb(110, 116, 126);
            _barNote.Padding = new Padding(0, 6, 0, 0);
            _bar.Controls.Add(_barText);
            _bar.Controls.Add(_barNote);
            _main.Controls.Add(_bar);
        }

        private void Retranslate()
        {
            foreach (var l in _sideCaptions)
            {
                var k = (string[])l.Tag;
                l.Text = T(k[0], k[1]);
            }
            _rangeError.Text = T("Dsh_EndAfterStart", "End date must be after start date!");
            _export.Text = T("Dsh_Export", "Export Report");
            _tips.SetToolTip(_openFolder, T("Dsh_ShowInFolder", "Show in folder") + "\n" + Config.ExportDirectory);
            FillLatest();
            FillHistoryItems();
            FillShifts();
            RetranslateMain();
            UpdateBar();
        }

        private void ApplyAccess()
        {
            _openFolder.Enabled = SecurityManager.HasAccess(SecurityLevel.Maintenance);
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
            if (i < 0 || i >= _periods.Count) return;
            var end = DashboardConfig.Minute(Now());
            SetRange(_periods[i].StartBefore(end), end, latest: _periods[i].Label);
        }

        /// <summary>The Latest period chosen again: moved up to now (tests).</summary>
        public void ChooseLatestAgain() { FromLatest(); }

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

        // ==================================================================
        // THE INFO BAR
        // ==================================================================

        /// <summary>
        /// "Data loading..." in red while the log is read; then, in black, the log
        /// interval, the shift chosen and the entries the tab on show lists.
        /// </summary>
        private void UpdateBar()
        {
            if (_loading || _logReading)
            {
                _barText.ForeColor = Color.FromArgb(200, 30, 30);
                _barText.Text = T("Dsh_Loading", "Data loading...");
                return;
            }
            var parts = new List<string>
            {
                T("Dsh_Interval", "Log interval:") + "  " + _from.ToString(DashboardConfig.DateFormatUi, CultureInfo.InvariantCulture) +
                "  -  " + _to.ToString(DashboardConfig.DateFormatUi, CultureInfo.InvariantCulture)
            };
            var shift = SelectedShift;
            if (shift != null) parts.Add(T("Dsh_ActiveShift", "Active Shift") + ": " + shift.Text);
            var shown = ShownText();
            if (shown.Length > 0) parts.Add(shown);
            _barText.ForeColor = UiStyle.GridInk;
            _barText.Text = string.Join("     \u00b7     ", parts);
        }

        /// <summary>What the tab on show lists: "12 entries shown, 40 in the period".</summary>
        private string ShownText()
        {
            var key = SelectedTabKey;
            if (LogTabs.Any(t => t.Key == key)) return _log != null ? _log.StatusText : "";
            var v = _view;
            if (v == null) return "";
            string format = T("Log_Status", "{0} entries shown, {1} in the period");
            switch (key)
            {
                case "states": return string.Format(CultureInfo.CurrentCulture, format, _stateGrid.RowCount, v.States.Count);
                case "events": return string.Format(CultureInfo.CurrentCulture, format, _eventGrid.RowCount, v.Events.Count);
                case "alarms": return string.Format(CultureInfo.CurrentCulture, format, _alarmGrid.RowCount, v.Alarms.Count);
            }
            return "";
        }

        /// <summary>A note at the right of the info bar: how long the read took, a report written, a problem (red).</summary>
        private void SetStatus(string text, bool problem = false)
        {
            _barNote.Text = (text ?? "").Replace("\n", "  -  ");
            _barNote.ForeColor = problem ? Color.Firebrick : Color.FromArgb(110, 116, 126);
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
                           (SecurityManager.HasAccess(SecurityLevel.Maintenance) ? "\n" + T("Dsh_SetDatabase", "Choose it in HMI Settings > Logs and Reports.") : ""));
                return;
            }

            var cached = !fromDatabase && _loaded != null && _loadedKey == key ? _loaded : null;
            _loading = true;
            SetStatus("");
            UpdateBar();
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
                UpdateBar();
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
                                        _loaded.Rows.Count, watch.Elapsed.TotalSeconds));
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>True while the log is being read (tests wait for it).</summary>
        public bool Loading { get { return _loading; } }

        /// <summary>What the page shows now (tests).</summary>
        public DashboardView View { get { return _view; } }

        /// <summary>The info bar's text (tests).</summary>
        public string BarText { get { return _barText.Text; } }
        public bool BarIsRed { get { return _barText.ForeColor.R > 150 && _barText.ForeColor.G < 100; } }
        /// <summary>The note at the right of the info bar (tests).</summary>
        public string StatusText { get { return _barNote.Text; } }

        // ==================================================================
        // THE REPORT FILES AND THEIR FOLDER
        // ==================================================================

        /// <summary>Writes the report files of what is shown; the files made.</summary>
        public List<string> ExportNow()
        {
            var v = _view;
            if (v == null) { SetStatus(T("Dsh_NoLogs", "No logs in selected range!"), true); return new List<string>(); }
            List<string> files;
            try
            {
                files = DashboardReports.Save(Config, v, DashboardReports.Stamp(v.Start), DashboardReports.ManualEndStamp(v.End),
                                              _machine.Map, Config.DatabasePath, Zone);
            }
            catch (Exception ex) { SetStatus(ex.Message, true); return new List<string>(); }
            SetStatus(files.Count > 0
                ? string.Format(CultureInfo.CurrentCulture, T("Dsh_Exported", "{0} report files exported!"), files.Count) + "\n" + Config.ExportDirectory
                : T("Dsh_ExportNone", "No report file could be written - check the export folder in the settings."), files.Count == 0);
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
            catch (Exception ex) { SetStatus(T("Dsh_FolderError", "The folder could not be opened:") + " " + ex.Message, true); }
        }
    }
}
