using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Settings;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// THE DASHBOARD'S SETTINGS - what the Industry 4 program's settings editor
    /// set: the database and its time zone, the barcoded cuts only, the sections of
    /// the page, and the reports (folder, language, which files, when). Nothing is
    /// kept until \U0001F4BE; \u21ba puts back what is kept.
    ///
    /// On HMI Settings > Logs and Reports (the machine chosen there) - no longer a
    /// tab of each machine's Machine Parameters, nor a window of the dashboard page
    /// (2 October): the machine's log and the HMI's are one subject. The SHIFTS the
    /// dashboard's Shift Selection lists are set here too.
    /// </summary>
    public sealed class DashboardSettingsControl : UserControl
    {
        private readonly MachineDashboard _machine;
        private readonly ToolTip _tips = UiStyle.Tips();
        private readonly FlowLayoutPanel _flow = new FlowLayoutPanel();

        private readonly TextBox _db = new TextBox();
        private readonly ComboBox _zone = new ComboBox();
        private readonly CheckBox _barcoded = new CheckBox();
        private readonly Dictionary<string, CheckBox> _sections = new Dictionary<string, CheckBox>();
        private readonly TextBox _exportDir = new TextBox();
        private readonly ComboBox _reportLanguage = new ComboBox();
        private readonly ComboBox _period = new ComboBox();
        private readonly TextBox _times = new TextBox();
        private readonly CheckedListBox _weekdays = new CheckedListBox();
        private readonly TextBox _monthDays = new TextBox();
        private readonly TextBox _annualDates = new TextBox();
        private readonly TextBox _time = new TextBox();
        private readonly NumericUpDown _historyDays = new NumericUpDown { Minimum = 30, Maximum = 2000 };
        private readonly NumericUpDown _historyLimit = new NumericUpDown { Minimum = 20, Maximum = 2000 };
        private readonly CheckBox _auto = new CheckBox();
        private readonly Dictionary<string, CheckBox> _reports = new Dictionary<string, CheckBox>();
        private readonly Label _next = new Label();
        private readonly Label _problem = new Label();
        private readonly Label _probe = new Label();
        private Panel _timesRow, _weekdaysRow, _monthDaysRow, _annualRow, _timeRow;
        private readonly FlowLayoutPanel _shiftRows = new FlowLayoutPanel();
        private readonly List<DateTimePicker[]> _shiftPickers = new List<DateTimePicker[]>();
        private Button _save, _revert;

        /// <summary>Raised after the settings were kept.</summary>
        public event EventHandler Saved;

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private const int Caption = 220, Field = 420, RowW = Caption + Field + 70;

        public DashboardSettingsControl(MachineDashboard machine)
        {
            _machine = machine ?? new MachineDashboard();
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;
            _flow.Dock = DockStyle.Fill;
            _flow.FlowDirection = FlowDirection.TopDown;
            _flow.WrapContents = false;
            _flow.AutoScroll = true;
            _flow.Padding = new Padding(16, 10, 16, 16);
            Controls.Add(_flow);

            Heading("Dsh_SetDatabaseHead", "\U0001F5C4\ufe0f", "Database");
            _db.Width = Field - 50;
            var browseDb = UiStyle.ToolButton("\U0001F4C2", T("Dsh_Browse", "Browse..."), _tips, (s, e) => BrowseDb());
            FieldRow("Dsh_SetDatabasePath", "Database file", _db, browseDb);
            // What the file holds - the logs it has, or why it cannot be read.
            var test = UiStyle.ToolButton("\U0001F50D", T("Dsh_SetTest", "Look into the database: its logs, first and last"), _tips, (s, e) => TestDatabase());
            _probe.AutoSize = false;
            _probe.Width = Field - 50;
            _probe.Height = 40;
            _probe.ForeColor = Color.DimGray;
            FieldRow("Dsh_SetTestCaption", "What it holds", _probe, test);
            _zone.Width = Field;
            _zone.DropDownStyle = ComboBoxStyle.DropDown;
            foreach (var z in DashboardTime.Zones) _zone.Items.Add(z[0]);
            FieldRow("Dsh_SetTimeZone", "Time zone of the panel", _zone);
            Note("Dsh_SetTimeZoneNote", "The database keeps UTC. The dashboard shows the times of this zone.");

            // The machine's log taken from its PLC by the HMI (29 October): its settings are the machine's -
            // Machine Parameters > Alarms and Logs (30 October), while HMI Settings > Options has it on.
            Note("Plg_WhoNote", "Written by the Industry 4 service or the HMI (HMI Settings > Options).");
            Note("Plg_AreaNote", "Its PLC log area: Machine Parameters > Alarms and Logs.");

            Heading("Dsh_SetLogicHead", "\U0001F9E0", "Data logic");
            CheckRow(_barcoded, "Dsh_SetBarcoded", "Use only barcoded cutting logs");

            // the shifts: each a start and an end; the dashboard's Shift Selection lists them
            Heading("Dsh_SetShiftsHead", "\u23f1\ufe0f", "Shifts");
            _shiftRows.FlowDirection = FlowDirection.TopDown;
            _shiftRows.WrapContents = false;
            _shiftRows.AutoSize = true;
            _shiftRows.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _shiftRows.Margin = new Padding(Caption + 8, 0, 0, 0);
            _flow.Controls.Add(_shiftRows);
            var addShift = UiStyle.TextButton(T("Dsh_SetShiftAdd", "Add a shift"), 0, (s, e) => AddShift(NextShiftStart(), NextShiftStart().Add(TimeSpan.FromHours(8))));
            addShift.Margin = new Padding(Caption + 8, 4, 0, 2);
            _flow.Controls.Add(addShift);
            Note("Dsh_SetShiftsNote", "A shift that ends at or before its start ends the next day.");

            // the sections as the page has them (2 October): under their tab's name,
            // with the section's own title - a tab with none ticked is not shown
            Heading("Dsh_SetSectionsHead", "\U0001F6E0\ufe0f", "Dashboard sections");
            foreach (var tab in DashboardScreen.MachineTabs)
            {
                bool first = true;
                foreach (var key in tab.Skip(3))
                {
                    var cb = new CheckBox();
                    _sections[key] = cb;
                    var title = DashboardScreen.SectionTitles[key];
                    CheckRow(cb, title[0], title[1], first ? T(tab[1], tab[2]) : null);
                    first = false;
                }
            }

            Heading("Dsh_SetReportsHead", "\U0001F4C4", "Exported reports");
            _exportDir.Width = Field - 50;
            var browseDir = UiStyle.ToolButton("\U0001F4C2", T("Dsh_Browse", "Browse..."), _tips, (s, e) => BrowseFolder());
            FieldRow("Dsh_SetExportDir", "Export folder", _exportDir, browseDir);
            _reportLanguage.Width = Field;
            _reportLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
            FieldRow("Dsh_SetReportLanguage", "Report language", _reportLanguage);
            _period.Width = Field;
            _period.DropDownStyle = ComboBoxStyle.DropDownList;
            _period.FormattingEnabled = true;
            _period.Format += (s, e) => e.Value = DashboardScreen.PeriodText(e.ListItem as string);
            _period.SelectedIndexChanged += (s, e) => ShowScheduleRows();
            FieldRow("Dsh_SetPeriod", "Auto-report period", _period);
            _times.Width = Field;
            _timesRow = FieldRow("Dsh_SetTimes", "Times (HH:MM, HH:MM ...)", _times);
            _weekdays.Width = Field;
            _weekdays.Height = 140;
            _weekdays.CheckOnClick = true;
            _weekdays.MultiColumn = false;
            _weekdaysRow = FieldRow("Dsh_SetWeekdays", "Weekdays", _weekdays);
            _weekdaysRow.Height = 146;
            _monthDays.Width = Field;
            _monthDaysRow = FieldRow("Dsh_SetMonthDays", "Days of the month (1, 11, 21 ...)", _monthDays);
            _annualDates.Width = Field;
            _annualRow = FieldRow("Dsh_SetAnnualDates", "Dates (MM-DD, MM-DD ...)", _annualDates);
            _time.Width = 120;
            _timeRow = FieldRow("Dsh_SetTime", "Time (HH:MM)", _time);
            _historyDays.Width = 120;
            FieldRow("Dsh_SetHistoryDays", "History: days", _historyDays);
            _historyLimit.Width = 120;
            FieldRow("Dsh_SetHistoryLimit", "History: most reports listed", _historyLimit);
            CheckRow(_auto, "Dsh_SetAuto", "Create the reports automatically");
            foreach (var r in new[]
            {
                new[] { "event_logs_report", "Dsh_SetEventReport", "Create event logs" },
                new[] { "alarm_logs_report", "Dsh_SetAlarmReport", "Create alarm logs" },
                new[] { "state_logs_report", "Dsh_SetStateReport", "Create state logs" },
                new[] { "database_table_report", "Dsh_SetTableReport", "Create database tables" }
            })
            {
                var cb = new CheckBox();
                _reports[r[0]] = cb;
                CheckRow(cb, r[1], r[2]);
            }
            _next.AutoSize = false;
            _next.Width = RowW;
            _next.Height = 26;
            _next.ForeColor = Color.DimGray;
            _flow.Controls.Add(_next);

            _problem.AutoSize = false;
            _problem.Width = RowW;
            _problem.Height = 40;
            _problem.ForeColor = Color.Firebrick;
            _problem.Visible = false;
            _flow.Controls.Add(_problem);

            _save = UiStyle.ToolButton("\U0001F4BE", T("Dsh_SetSave", "Keep the settings"), _tips, (s, e) => Save());
            _revert = UiStyle.ToolButton("\u21ba", T("Dsh_SetRevert", "Put back what is kept"), _tips, (s, e) => Fill(_machine.Config));
            _save.Location = new Point(Caption + 10, 4);
            _revert.Location = new Point(Caption + 60, 4);
            var buttons = new Panel { Width = RowW, Height = 44, Margin = new Padding(0, 10, 0, 0) };
            buttons.Controls.Add(_save);
            buttons.Controls.Add(_revert);
            _flow.Controls.Add(buttons);

            Fill(_machine.Config);
            // what the database holds, at once
            HandleCreated += (s, e) => { try { BeginInvoke(new Action(TestDatabase)); } catch { } };
            bool can = SecurityManager.HasAccess(SecurityLevel.Maintenance);
            foreach (Control c in _flow.Controls) if (c != _next) c.Enabled = can;
            // View Preferences changed: the shifts' times and the next report in the new format.
            UiStyle.OnViewPreferences(this, () =>
            {
                foreach (var pair in _shiftPickers) foreach (var p in pair) p.CustomFormat = Kaban.Core.Helpers.ViewPreferences.TimePattern(false);
                ShowNext();
            });
        }

        /// <summary>What the machine's logger does now.</summary>
        private DateTime? _nextDue;

        private void ShowNext()
        {
            _next.Text = T("Dsh_SetNext", "Next automatic report:") + " " +
                         (_nextDue.HasValue ? Kaban.Core.Helpers.ViewPreferences.DateTimeText(_nextDue.Value, false) : "-");
        }

        // ---- building

        private void Heading(string key, string icon, string text)
        {
            _flow.Controls.Add(new Label
            {
                Text = icon + " " + T(key, text), AutoSize = false, Width = RowW, Height = 30, Margin = new Padding(0, 14, 0, 2),
                Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft
            });
        }

        private void Note(string key, string text)
        {
            _flow.Controls.Add(new Label
            {
                Text = T(key, text), AutoSize = false, Width = RowW, Height = 22, ForeColor = Color.DimGray,
                Padding = new Padding(Caption + 8, 0, 0, 0)
            });
        }

        private Panel FieldRow(string key, string text, Control field, Control extra = null)
        {
            var row = new Panel { Width = RowW, Height = Math.Max(32, field.Height + 6), Margin = new Padding(0, 2, 0, 2) };
            row.Controls.Add(UiStyle.CaptionLabel(T(key, text), 0, 3, Caption));
            field.Location = new Point(Caption + 8, 3);
            row.Controls.Add(field);
            if (extra != null)
            {
                extra.Location = new Point(Caption + 8 + field.Width + 6, 0);
                row.Controls.Add(extra);
            }
            _flow.Controls.Add(row);
            return row;
        }

        /// <param name="group">A caption on the row's left (the tab the dashboard sections are on); null: none.</param>
        private void CheckRow(CheckBox cb, string key, string text, string group = null)
        {
            cb.Text = T(key, text);
            cb.AutoSize = false;
            cb.Width = RowW - Caption - 8;
            cb.Height = 26;
            if (group == null)
            {
                cb.Margin = new Padding(Caption + 8, 1, 0, 1);
                _flow.Controls.Add(cb);
                return;
            }
            // a group's first row: its caption, then the box where the others are
            var row = new Panel { Width = RowW, Height = 26, Margin = new Padding(0, 8, 0, 1) };
            row.Controls.Add(UiStyle.CaptionLabel(group, 0, 1, Caption));
            cb.Location = new Point(Caption + 8, 0);
            cb.Margin = Padding.Empty;
            row.Controls.Add(cb);
            _flow.Controls.Add(row);
        }

        private void ShowScheduleRows()
        {
            string type = _period.SelectedItem as string ?? "Daily";
            _timesRow.Visible = type == "Daily";
            _weekdaysRow.Visible = type == "Weekly";
            _monthDaysRow.Visible = type == "Monthly";
            _annualRow.Visible = type == "Annual";
            _timeRow.Visible = type != "Daily";
        }

        // ---- the values

        private sealed class Item
        {
            public string Value, Text;
            public override string ToString() { return Text; }
        }

        private void Fill(DashboardConfig c)
        {
            _problem.Visible = false;
            _db.Text = c.DatabasePath;
            _zone.Text = c.TimeZoneName;
            _barcoded.Checked = c.BarcodedCuttingOnly;
            foreach (var row in _shiftRows.Controls.Cast<Control>().ToList()) { _shiftRows.Controls.Remove(row); row.Dispose(); }
            _shiftPickers.Clear();
            foreach (var sh in c.Shifts) AddShift(sh.Start, sh.End);
            foreach (var kv in _sections) kv.Value.Checked = c.SectionActive(kv.Key);
            _exportDir.Text = c.ExportDirectory;

            _reportLanguage.Items.Clear();
            foreach (var n in c.LanguageNames) _reportLanguage.Items.Add(n);
            int li = _reportLanguage.Items.IndexOf(c.ReportLanguage);
            _reportLanguage.SelectedIndex = li >= 0 ? li : (_reportLanguage.Items.Count > 0 ? 0 : -1);

            var sched = c.Schedule;
            _period.Items.Clear();
            // the schedule kinds - the periods of the file that are one (Daily, Weekly, Monthly, Annual)
            var kinds = c.OwnPeriods.Select(p => p.Label).Where(l => ReportSchedule.Types.Contains(l)).ToList();
            if (kinds.Count == 0) kinds.AddRange(ReportSchedule.Types);
            if (!kinds.Contains(sched.Type)) kinds.Insert(0, sched.Type);
            foreach (var k in kinds) _period.Items.Add(k);
            _period.SelectedItem = sched.Type;

            _times.Text = string.Join(", ", sched.Times);
            _weekdays.Items.Clear();
            foreach (var d in ReportSchedule.WeekdayNames)
                _weekdays.Items.Add(new Item { Value = d, Text = WeekdayText(d) }, sched.Weekdays.Contains(d));
            _monthDays.Text = string.Join(", ", sched.MonthDays.Select(d => d.ToString(CultureInfo.InvariantCulture)));
            _annualDates.Text = string.Join(", ", sched.AnnualDates);
            _time.Text = sched.Time;
            _historyDays.Value = Math.Max(_historyDays.Minimum, Math.Min(_historyDays.Maximum, sched.HistoryDays));
            _historyLimit.Value = Math.Max(_historyLimit.Minimum, Math.Min(_historyLimit.Maximum, sched.HistoryLimit));
            _auto.Checked = c.AutoReports;
            foreach (var kv in _reports) kv.Value.Checked = c.Get(kv.Key, kv.Key != "database_table_report");
            var due = c.RequiredReportEnd;
            _nextDue = due;
            ShowNext();
            ShowScheduleRows();
        }

        /// <summary>A weekday's name in the HMI's language.</summary>
        private static string WeekdayText(string english)
        {
            int i = Array.IndexOf(ReportSchedule.WeekdayNames, english);
            if (i < 0) return english;
            try
            {
                var culture = CultureOf(LanguageManager.CurrentLanguage);
                return culture.DateTimeFormat.GetDayName((DayOfWeek)((i + 1) % 7));
            }
            catch { return english; }
        }

        private static CultureInfo CultureOf(string lang)
        {
            switch ((lang ?? "").ToUpperInvariant())
            {
                case "TR": return new CultureInfo("tr-TR");
                case "RU": return new CultureInfo("ru-RU");
                case "DE": return new CultureInfo("de-DE");
                case "IT": return new CultureInfo("it-IT");
                case "ES": return new CultureInfo("es-ES");
            }
            return CultureInfo.InvariantCulture;
        }

        /// <summary>
        /// Reads the fields into a copy of the settings; null (and the problem shown)
        /// when a field cannot be read.
        /// </summary>
        public DashboardConfig Collect()
        {
            var problems = new List<string>();
            var c = _machine.Config.Clone();
            c.DatabasePath = _db.Text.Trim();
            string zone = (_zone.Text ?? "").Trim();
            if (!DashboardTime.Known(zone)) problems.Add(T("Dsh_SetBadZone", "This PC does not know the time zone") + " \"" + zone + "\".");
            c.TimeZoneName = zone;
            c.BarcodedCuttingOnly = _barcoded.Checked;
            var shifts = new List<DashboardConfig.Shift>();
            foreach (var pair in _shiftPickers)
            {
                var sh = new DashboardConfig.Shift { Start = Hm(pair[0].Value), End = Hm(pair[1].Value) };
                if (sh.Start == sh.End) problems.Add(T("Dsh_SetBadShift", "A shift cannot start and end at the same time:") + " " + sh.Text);
                shifts.Add(sh);
            }
            c.Shifts = shifts;
            foreach (var kv in _sections) c.Set(kv.Key + "_active", kv.Value.Checked);
            c.ExportDirectory = _exportDir.Text.Trim();
            if (_reportLanguage.SelectedItem != null) c.ReportLanguage = (string)_reportLanguage.SelectedItem;

            var s = c.Schedule;
            s.Type = _period.SelectedItem as string ?? "Daily";
            var times = ReportSchedule.HmList(new[] { _times.Text });
            if (s.Type == "Daily" && times.Count == 0) problems.Add(T("Dsh_SetBadTimes", "The times cannot be read (HH:MM, HH:MM ...)."));
            if (times.Count > 0) s.Times = times;
            var one = ReportSchedule.HmList(new[] { _time.Text });
            if (s.Type != "Daily" && one.Count != 1) problems.Add(T("Dsh_SetBadTime", "The time cannot be read (HH:MM)."));
            if (one.Count == 1) s.Time = one[0];
            var days = new List<string>();
            foreach (Item it in _weekdays.CheckedItems) days.Add(it.Value);
            if (s.Type == "Weekly" && days.Count == 0) problems.Add(T("Dsh_SetBadWeekdays", "Choose at least one weekday."));
            if (days.Count > 0) s.Weekdays = days;
            var md = new List<int>();
            foreach (var part in _monthDays.Text.Split(','))
            {
                int d;
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out d) && d >= 1 && d <= 31) md.Add(d);
            }
            if (s.Type == "Monthly" && md.Count == 0) problems.Add(T("Dsh_SetBadMonthDays", "The days of the month cannot be read (1 to 31)."));
            if (md.Count > 0) s.MonthDays = md.Distinct().OrderBy(x => x).ToList();
            var ad = ReportSchedule.MdList(new[] { _annualDates.Text });
            if (s.Type == "Annual" && ad.Count == 0) problems.Add(T("Dsh_SetBadDates", "The dates cannot be read (MM-DD, MM-DD ...)."));
            if (ad.Count > 0) s.AnnualDates = ad;
            s.HistoryDays = (int)_historyDays.Value;
            s.HistoryLimit = (int)_historyLimit.Value;
            c.Schedule = s;
            c.AutoReports = _auto.Checked;
            foreach (var kv in _reports) c.Set(kv.Key, kv.Value.Checked);

            _problem.Text = string.Join("\n", problems);
            _problem.Visible = problems.Count > 0;
            return problems.Count > 0 ? null : c;
        }

        /// <summary>Keeps the settings (false when a field cannot be read or the file cannot be written).</summary>
        public bool Save()
        {
            if (!SecurityManager.HasAccess(SecurityLevel.Maintenance)) return false;
            var c = Collect();
            if (c == null) return false;
            bool ok = _machine.Replace(c);
            if (!ok)
            {
                _problem.Text = T("Dsh_SetNotWritten", "The settings could not be written:") + " " + c.FilePath;
                _problem.Visible = true;
            }
            try { AuditLogger.Log("Dashboard (" + _machine.ModuleId + "): settings changed."); } catch { }
            var h = Saved;
            if (h != null && ok) h(this, EventArgs.Empty);
            return ok;
        }

        private static TimeSpan Hm(DateTime t) { return new TimeSpan(t.Hour, t.Minute, 0); }

        /// <summary>A new shift starts where the last one ends (08:00 when there is none).</summary>
        private TimeSpan NextShiftStart()
        {
            if (_shiftPickers.Count == 0) return TimeSpan.FromHours(8);
            return Hm(_shiftPickers[_shiftPickers.Count - 1][1].Value);
        }

        /// <summary>A shift's row: its start, its end, and a button that takes it away (kept with \U0001F4BE).</summary>
        public void AddShift(TimeSpan start, TimeSpan end)
        {
            start = TimeSpan.FromMinutes(((long)start.TotalMinutes % 1440 + 1440) % 1440);
            end = TimeSpan.FromMinutes(((long)end.TotalMinutes % 1440 + 1440) % 1440);
            Func<TimeSpan, DateTimePicker> picker = t => new DateTimePicker
            {
                // As the screen writes times (View Preferences): "14:00" or "2:00 PM".
                Format = DateTimePickerFormat.Custom, CustomFormat = Kaban.Core.Helpers.ViewPreferences.TimePattern(false), ShowUpDown = true, Width = 104, Font = UiStyle.Body,
                Value = DateTime.Today + t
            };
            var a = picker(start);
            var b = picker(end);
            var pair = new[] { a, b };
            var row = new Panel { Width = 320, Height = 38, Margin = new Padding(0, 1, 0, 1) };
            a.Location = new Point(0, 5);
            var dash = new Label { Text = "\u2013", AutoSize = true, Location = new Point(110, 8) };
            b.Location = new Point(128, 5);
            // The program's delete button (it was a cross).
            var remove = UiStyle.ToolButton("\uD83D\uDDD1", T("Dsh_SetShiftRemove", "Remove this shift"), _tips, (s, e) =>
            {
                _shiftPickers.Remove(pair);
                _shiftRows.Controls.Remove(row);
                row.Dispose();
            });
            remove.Location = new Point(242, 2);
            row.Controls.AddRange(new Control[] { a, dash, b, remove });
            row.Enabled = SecurityManager.HasAccess(SecurityLevel.Maintenance);
            _shiftPickers.Add(pair);
            _shiftRows.Controls.Add(row);
        }

        /// <summary>The shifts on the page, "08:00 - 16:00" (tests).</summary>
        public List<string> ShiftTexts
        {
            get { return _shiftPickers.Select(p => new DashboardConfig.Shift { Start = Hm(p[0].Value), End = Hm(p[1].Value) }.Text).ToList(); }
        }

        /// <summary>A shift's start and end pickers (tests).</summary>
        public DateTimePicker[] ShiftPickers(int i) { return _shiftPickers[i]; }

        /// <summary>Takes a shift away (tests: as its button does).</summary>
        public void RemoveShift(int i)
        {
            var pair = _shiftPickers[i];
            var row = pair[0].Parent;
            _shiftPickers.RemoveAt(i);
            _shiftRows.Controls.Remove(row);
            row.Dispose();
        }

        /// <summary>Looks into the file in the box (on a thread; the result under it).</summary>
        public void TestDatabase()
        {
            string path = _db.Text.Trim();
            var zone = DashboardTime.Find((_zone.Text ?? "").Trim());
            _probe.ForeColor = Color.DimGray;
            _probe.Text = T("Dsh_SetTesting", "Looking into the database...");
            System.Threading.Tasks.Task.Run(() => DashboardData.Check(path, zone)).ContinueWith(t =>
            {
                if (IsDisposed || t.IsFaulted) return;
                ShowProbe(t.Result);
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void ShowProbe(DashboardData.Probe p)
        {
            _probe.ForeColor = p.Ok ? Color.FromArgb(30, 110, 50) : Color.Firebrick;
            _probe.Text = p.Describe();
            LastProbe = p;
        }

        /// <summary>The last look into the database (tests).</summary>
        public DashboardData.Probe LastProbe { get; private set; }

        /// <summary>A machine's dashboard settings in a window of their own.</summary>
        public static void ShowDialog(IWin32Window owner, MachineDashboard machine, string title = null)
        {
            if (machine == null || !SecurityManager.HasAccess(SecurityLevel.Maintenance)) return;
            using (var f = new Form
            {
                Text = title ?? T("Dsh_Settings", "Dashboard settings"), Width = 760, Height = 760, StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, ShowInTaskbar = false, Font = UiStyle.Body
            })
            {
                UiScale.ApplyOnLoad(f);
                var page = new DashboardSettingsControl(machine) { Dock = DockStyle.Fill };
                page.Saved += (s, e) => f.DialogResult = DialogResult.OK;
                f.Controls.Add(page);
                f.ShowDialog(owner);
            }
        }

        private void BrowseDb()
        {
            using (var d = new OpenFileDialog { Filter = "SQLite (*.sqlite;*.db)|*.sqlite;*.db|*.*|*.*", Title = T("Dsh_SetDatabasePath", "Database file") })
            {
                try { if (File.Exists(_db.Text)) d.InitialDirectory = Path.GetDirectoryName(_db.Text); } catch { }
                if (d.ShowDialog(FindForm()) == DialogResult.OK) _db.Text = d.FileName;
            }
        }

        private void BrowseFolder()
        {
            using (var d = new FolderBrowserDialog { SelectedPath = _exportDir.Text })
                if (d.ShowDialog(FindForm()) == DialogResult.OK) _exportDir.Text = d.SelectedPath;
        }

        // ---- for tests
        public TextBox DatabaseBox { get { return _db; } }
        public ComboBox ZoneBox { get { return _zone; } }
        public ComboBox PeriodBox { get { return _period; } }
        public TextBox TimesBox { get { return _times; } }
        public TextBox TimeBox { get { return _time; } }
        public CheckedListBox WeekdaysBox { get { return _weekdays; } }
        public CheckBox AutoBox { get { return _auto; } }
        public CheckBox SectionBox(string key) { return _sections[key]; }
        public string ProblemText { get { return _problem.Visible ? _problem.Text : ""; } }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }
    }
}
