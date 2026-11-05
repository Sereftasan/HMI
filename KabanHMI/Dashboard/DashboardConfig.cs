using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// THE DASHBOARD'S SETTINGS - the Industry 4 dashboard's config.json, key for key,
    /// so a file made by the old program can be copied over and one made here can be
    /// read by it. Kept per machine in %ProgramData%\KabanHMI\Dashboard\&lt;machine&gt;.json;
    /// the first time, it is made from the machine's shipped Dashboard\config.json.
    ///
    /// Keys the HMI does not know are kept as they are (the file is held as its
    /// parsed tree and written back in the old program's layout).
    /// </summary>
    public sealed class DashboardConfig
    {
        public const string DateFormatUi = "yyyy-MM-dd HH:mm";

        /// <summary>The settings in the file's own order (what is written back).</summary>
        private readonly Dictionary<string, object> _raw;

        /// <summary>Where it is kept ("" = not kept, a test's).</summary>
        public string FilePath { get; private set; }

        public DashboardConfig() : this(new Dictionary<string, object>(StringComparer.Ordinal), "") { }

        private DashboardConfig(Dictionary<string, object> raw, string path)
        {
            _raw = raw;
            FilePath = path ?? "";
            Normalise();
        }

        /// <summary>Another folder for the per-machine settings (tests).</summary>
        public static string FolderOverride;

        /// <summary>The folder of the per-machine settings.</summary>
        public static string Folder
        {
            get
            {
                if (!string.IsNullOrEmpty(FolderOverride)) return FolderOverride;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                    "KabanHMI", "Dashboard");
            }
        }

        /// <summary>The settings of a machine: its own file, else the shipped one, else the defaults.</summary>
        public static DashboardConfig Load(string moduleId, string shippedDefault)
        {
            string own = Path.Combine(Folder, (string.IsNullOrEmpty(moduleId) ? "Machine" : moduleId) + ".json");
            var c = FromFile(own);
            if (c != null) return c;
            c = FromFile(shippedDefault) ?? new DashboardConfig();
            c.FilePath = own;
            return c;
        }

        /// <summary>A file read as it is (null when it is missing or not JSON).</summary>
        public static DashboardConfig FromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var tree = Json.Parse(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                return tree == null ? null : new DashboardConfig(tree, path);
            }
            catch { return null; }
        }

        public static DashboardConfig FromText(string json)
        {
            var tree = Json.Parse(json) as Dictionary<string, object>;
            return new DashboardConfig(tree ?? new Dictionary<string, object>(StringComparer.Ordinal), "");
        }

        /// <summary>The defaults the old program put in a file that lacked them.</summary>
        private void Normalise()
        {
            if (!(_raw.ContainsKey("shifts") && _raw["shifts"] is List<object>))
                _raw["shifts"] = new List<object>
                {
                    Pair("start", "08:00", "end", "16:00"),
                    Pair("start", "16:00", "end", "00:00"),
                    Pair("start", "00:00", "end", "08:00")
                };
            if (!(_raw.ContainsKey("periods") && _raw["periods"] is List<object>))
                _raw["periods"] = new List<object>
                {
                    Pair("label", "Daily", "delta_days", 1.0),
                    Pair("label", "Weekly", "delta_days", 7.0),
                    Pair("label", "Monthly", "delta_months", 1.0),
                    Pair("label", "Annual", "delta_years", 1.0)
                };
            if (!_raw.ContainsKey("export_directory")) _raw["export_directory"] = "exports";
            if (!_raw.ContainsKey("time_zone")) _raw["time_zone"] = "UTC";
            if (!_raw.ContainsKey("required_report_end_date")) _raw["required_report_end_date"] = null;
            if (!(_raw.ContainsKey("auto_report_schedule") && _raw["auto_report_schedule"] is IDictionary<string, object>))
                _raw["auto_report_schedule"] = new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static Dictionary<string, object> Pair(string k1, object v1, string k2, object v2)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal) { { k1, v1 }, { k2, v2 } };
        }

        // ------------------------------------------------------------------ values

        public string DatabasePath
        {
            get { return Json.Text(_raw, "database_path", ""); }
            set { _raw["database_path"] = value ?? ""; }
        }

        public string ExportDirectory
        {
            get { return Json.Text(_raw, "export_directory", "exports"); }
            set { _raw["export_directory"] = value ?? ""; }
        }

        public string TimeZoneName
        {
            get { return Json.Text(_raw, "time_zone", "UTC"); }
            set { _raw["time_zone"] = string.IsNullOrWhiteSpace(value) ? "UTC" : value.Trim(); }
        }

        /// <summary>The language the reports are written in ("English", "T\u00fcrk\u00e7e" ...).</summary>
        public string ReportLanguage
        {
            get { return Json.Text(_raw, "report_language", Json.Text(_raw, "dashboard_language", "English")); }
            set { _raw["report_language"] = value ?? "English"; }
        }

        public bool Get(string key, bool fallback) { return Json.Bool(_raw, key, fallback); }
        public void Set(string key, bool value) { _raw[key] = value; }

        /// <summary>The sections of the page (key prefix: state_pie, state_timeline ...).</summary>
        public static readonly string[] Sections =
            { "state_pie", "state_timeline", "state_logs", "event_quantity", "cutting_details", "event_logs", "alarm_logs" };

        public bool SectionActive(string section) { return Get(section + "_active", true); }
        public bool SectionOpen(string section) { return Get(section + "_show", false); }
        public void SetSectionOpen(string section, bool open) { Set(section + "_show", open); }

        public bool BarcodedCuttingOnly
        {
            get { return Get("barcoded_cutting_logs_only", false); }
            set { Set("barcoded_cutting_logs_only", value); }
        }

        public bool AutoReports
        {
            get { return Get("auto_reports", false); }
            set { Set("auto_reports", value); }
        }

        // ---- the machine's log, written by the HMI (29 October: the Industry 4 service's job) ----

        /// <summary>The HMI reads the machine's log records from its PLC and writes them into the database (on unless switched off).</summary>
        public bool PlcLogging
        {
            get { return Get("plc_logging", true); }
            set { Set("plc_logging", value); }
        }

        /// <summary>A record's "Process ID" looked up in KMSystem's PostgreSQL database: its Profile Code and Profile Data (the service's Postgresql setting).</summary>
        public bool KmSystemLookup
        {
            get { return Get("kmsystem_lookup", false); }
            set { Set("kmsystem_lookup", value); }
        }

        public const string DefaultKmSystemConnection = "Server=localhost;Port=5432;Database=KMSystem;User Id=postgres;Password=123456;";

        /// <summary>KMSystem's PostgreSQL connection string (the service's PostgreConnectionString).</summary>
        public string KmSystemConnection
        {
            get { return Json.Text(_raw, "kmsystem_connection", DefaultKmSystemConnection); }
            set { _raw["kmsystem_connection"] = value ?? ""; }
        }

        /// <summary>The report due next ("required_report_end_date"), null when none is set.</summary>
        public DateTime? RequiredReportEnd
        {
            get
            {
                DateTime d;
                string s = Json.Text(_raw, "required_report_end_date", "");
                return DateTime.TryParseExact(s, DateFormatUi, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
                    ? d : (DateTime?)null;
            }
            set
            {
                _raw["required_report_end_date"] = value.HasValue
                    ? value.Value.ToString(DateFormatUi, CultureInfo.InvariantCulture) : null;
            }
        }

        // ------------------------------------------------------------------ shifts

        public sealed class Shift
        {
            public TimeSpan Start, End;
            public string Text { get { return Hm(Start) + " - " + Hm(End); } }
            /// <summary>As the screen writes times (View Preferences: "6:00 AM - 2:00 PM" on 12 hours); Text stays the key.</summary>
            public string Shown
            {
                get
                {
                    string p = Kaban.Core.Helpers.ViewPreferences.TimePattern(false);
                    return (DateTime.Today + Start).ToString(p, CultureInfo.InvariantCulture) + " - " +
                           (DateTime.Today + End).ToString(p, CultureInfo.InvariantCulture);
                }
            }
            public override string ToString() { return Text; }
        }

        public static string Hm(TimeSpan t)
        {
            return ((int)t.TotalHours % 24).ToString("00", CultureInfo.InvariantCulture) + ":" +
                   t.Minutes.ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>"8:5", "08:05" -> 08:05; null when it is not a time of day.</summary>
        public static TimeSpan? ParseHm(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var parts = s.Trim().Split(':');
            int h, m;
            if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out h) ||
                !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out m)) return null;
            if (h < 0 || h > 23 || m < 0 || m > 59) return null;
            return new TimeSpan(h, m, 0);
        }

        public List<Shift> Shifts
        {
            get
            {
                var list = new List<Shift>();
                var raw = Json.List(_raw, "shifts");
                if (raw == null) return list;
                foreach (var o in raw)
                {
                    var d = o as IDictionary<string, object>;
                    var s = ParseHm(Json.Text(d, "start", ""));
                    var e = ParseHm(Json.Text(d, "end", ""));
                    if (s.HasValue && e.HasValue) list.Add(new Shift { Start = s.Value, End = e.Value });
                }
                return list;
            }
            set
            {
                var raw = new List<object>();
                foreach (var s in value ?? new List<Shift>())
                    raw.Add(Pair("start", Hm(s.Start), "end", Hm(s.End)));
                _raw["shifts"] = raw;
            }
        }

        // ------------------------------------------------------------------ periods

        /// <summary>One "Latest" period: the last N minutes / hours / days / months / years.</summary>
        public sealed class Period
        {
            public string Label = "";
            public int Minutes, Hours, Days, Months, Years;

            /// <summary>Length for sorting (a month as 30 days, a year as 365).</summary>
            public long SortMinutes
            {
                get { return Minutes + Hours * 60L + Days * 1440L + Months * 43200L + Years * 525600L; }
            }

            /// <summary>From <paramref name="end"/> back by this period, both on the minute.</summary>
            public DateTime StartBefore(DateTime end)
            {
                DateTime start;
                if (Minutes > 0) start = end.AddMinutes(-Minutes);
                else if (Hours > 0) start = end.AddHours(-Hours);
                else if (Days > 0) start = end.AddDays(-Days);
                else if (Months > 0) start = end.AddMonths(-Months);   // 31 March - 1 month = 28/29 February, as the old program
                else if (Years > 0) start = end.AddYears(-Years);
                else start = end;
                return Minute(start);
            }
        }

        public static DateTime Minute(DateTime t) { return new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Kind); }

        /// <summary>The five the old program always added to the file's own.</summary>
        private static readonly Period[] Extra =
        {
            new Period { Label = "15-Minute", Minutes = 15 },
            new Period { Label = "30-Minute", Minutes = 30 },
            new Period { Label = "Hourly", Hours = 1 },
            new Period { Label = "8-Hour", Hours = 8 },
            new Period { Label = "3-Month", Months = 3 }
        };

        /// <summary>The file's periods (Daily, Weekly, Monthly, Annual) - the auto-report period choices.</summary>
        public List<Period> OwnPeriods
        {
            get
            {
                var list = new List<Period>();
                var raw = Json.List(_raw, "periods");
                if (raw == null) return list;
                foreach (var o in raw)
                {
                    var d = o as IDictionary<string, object>;
                    string label = Json.Text(d, "label", "");
                    if (label.Length == 0) continue;
                    list.Add(new Period
                    {
                        Label = label,
                        Minutes = (int)(Json.Number(d, "delta_minutes") ?? 0),
                        Hours = (int)(Json.Number(d, "delta_hours") ?? 0),
                        Days = (int)(Json.Number(d, "delta_days") ?? 0),
                        Months = (int)(Json.Number(d, "delta_months") ?? 0),
                        Years = (int)(Json.Number(d, "delta_years") ?? 0)
                    });
                }
                return list;
            }
        }

        /// <summary>Every "Latest" choice, shortest first.</summary>
        public List<Period> AllPeriods
        {
            get
            {
                var list = OwnPeriods;
                foreach (var e in Extra)
                    if (!list.Any(p => p.Label == e.Label)) list.Add(e);
                return list.OrderBy(p => p.SortMinutes).ToList();
            }
        }

        // ------------------------------------------------------------------ languages

        /// <summary>The language names of the file ("English", "T\u00fcrk\u00e7e" ...).</summary>
        public List<string> LanguageNames
        {
            get
            {
                var list = new List<string>();
                var raw = Json.List(_raw, "languages");
                if (raw != null)
                    foreach (var o in raw)
                    {
                        string n = Json.Text(o as IDictionary<string, object>, "name", "");
                        if (n.Length > 0) list.Add(n);
                    }
                if (list.Count == 0) list.AddRange(StateMap.LanguageColumns.Values);
                return list;
            }
        }

        // ------------------------------------------------------------------ schedule

        public ReportSchedule Schedule
        {
            get { return ReportSchedule.From(Json.Object(_raw, "auto_report_schedule")); }
            set { _raw["auto_report_schedule"] = value.ToJson(); }
        }

        // ------------------------------------------------------------------ writing

        /// <summary>The order the old settings editor wrote the keys in (others follow in their own order).</summary>
        private static readonly string[] KeyOrder =
        {
            "database_path", "periods", "shifts", "languages", "dashboard_language",
            "state_pie_active", "state_timeline_active", "event_quantity_active",
            "cutting_details_active", "state_logs_active", "event_logs_active", "alarm_logs_active",
            "state_pie_show", "state_timeline_show", "event_quantity_show",
            "cutting_details_show", "state_logs_show", "event_logs_show", "alarm_logs_show",
            "barcoded_cutting_logs_only", "auto_reports", "export_directory", "report_language", "time_zone",
            "auto_report_schedule", "required_report_end_date",
            "state_logs_report", "event_logs_report", "alarm_logs_report", "database_table_report"
        };

        /// <summary>
        /// The text of the file as the old program wrote it: one key a line, a list's
        /// items one a line, an object's keys one a line - so the file is still easy to
        /// read and edit by hand.
        /// </summary>
        public string ToText()
        {
            var keys = KeyOrder.Where(k => _raw.ContainsKey(k)).ToList();
            keys.AddRange(_raw.Keys.Where(k => !keys.Contains(k)));
            var sb = new StringBuilder("{\n");
            for (int i = 0; i < keys.Count; i++)
            {
                string k = keys[i];
                object v = _raw[k];
                string comma = i < keys.Count - 1 ? "," : "";
                var list = v as List<object>;
                var dict = v as IDictionary<string, object>;
                if (list != null)
                {
                    sb.Append("    ").Append(Json.Write(k)).Append(": [\n");
                    for (int j = 0; j < list.Count; j++)
                        sb.Append("        ").Append(Json.Write(list[j])).Append(j < list.Count - 1 ? "," : "").Append('\n');
                    sb.Append("    ]").Append(comma).Append('\n');
                }
                else if (dict != null)
                {
                    sb.Append("    ").Append(Json.Write(k)).Append(": {\n");
                    int j = 0;
                    foreach (var kv in dict)
                    {
                        sb.Append("        ").Append(Json.Write(kv.Key)).Append(": ").Append(Json.Write(kv.Value))
                          .Append(j < dict.Count - 1 ? "," : "").Append('\n');
                        j++;
                    }
                    sb.Append("    }").Append(comma).Append('\n');
                }
                else
                    sb.Append("    ").Append(Json.Write(k)).Append(": ").Append(Json.Write(v)).Append(comma).Append('\n');
            }
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Writes the file (false when it could not be written; the settings stay in memory).</summary>
        public bool Save()
        {
            if (string.IsNullOrEmpty(FilePath)) return false;
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                Kaban.Core.Helpers.SafeFile.WriteAllText(FilePath, ToText(), new UTF8Encoding(false));
                return true;
            }
            catch { return false; }
        }

        public DashboardConfig Clone()
        {
            var c = FromText(ToText());
            c.FilePath = FilePath;
            return c;
        }
    }

    /// <summary>
    /// WHEN THE AUTOMATIC REPORTS ARE MADE: every day at the given times, on given
    /// weekdays, days of the month or dates of the year at one time. A report covers
    /// the time from the scheduled time before it to its own.
    /// </summary>
    public sealed class ReportSchedule
    {
        public static readonly string[] Types = { "Daily", "Weekly", "Monthly", "Annual" };
        public static readonly string[] WeekdayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        public string Type = "Daily";
        public List<string> Times = new List<string> { "00:00" };
        public string Time = "00:00";
        public List<string> Weekdays = new List<string> { "Sunday" };
        public List<int> MonthDays = new List<int> { 1 };
        public List<string> AnnualDates = new List<string> { "01-01" };
        public int HistoryDays = 730;
        public int HistoryLimit = 100;

        /// <summary>
        /// Read as the old dashboard read it: a key that is missing takes the old
        /// dashboard's default (weekly, Monday 00:00, 400 days, 200 reports); entries
        /// that cannot be read are dropped as the old settings editor dropped them.
        /// </summary>
        public static ReportSchedule From(IDictionary<string, object> raw)
        {
            var s = new ReportSchedule
            {
                Type = "Weekly", Weekdays = new List<string> { "Monday" }, HistoryDays = 400, HistoryLimit = 200
            };
            if (raw == null) return s;
            string type = Json.Text(raw, "type", "Weekly").Trim();
            s.Type = Types.Contains(type) ? type : "Weekly";

            var times = Json.List(raw, "times") ?? Json.List(raw, "daily_times");
            var clean = HmList(times != null ? times.Select(o => Convert.ToString(o, CultureInfo.InvariantCulture))
                                             : new[] { Json.Text(raw, "time", "00:00") });
            s.Times = clean.Count > 0 ? clean : new List<string> { "00:00" };

            var one = HmList(new[] { Json.Text(raw, "time", "00:00") });
            s.Time = one.Count > 0 ? one[0] : "00:00";

            var wd = Json.List(raw, "weekdays");
            var wds = wd == null ? new List<string>() :
                wd.Select(o => Convert.ToString(o, CultureInfo.InvariantCulture).Trim()).Where(x => WeekdayNames.Contains(x)).ToList();
            s.Weekdays = wds.Count > 0 ? wds : new List<string> { "Monday" };

            var md = Json.List(raw, "month_days");
            var mds = new List<int>();
            if (md != null)
                foreach (var o in md)
                {
                    int d;
                    if (int.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out d) && d >= 1 && d <= 31)
                        mds.Add(d);
                }
            s.MonthDays = mds.Count > 0 ? mds.Distinct().OrderBy(x => x).ToList() : new List<int> { 1 };

            var ad = Json.List(raw, "annual_dates");
            var ads = MdList(ad == null ? new string[0] : ad.Select(o => Convert.ToString(o, CultureInfo.InvariantCulture)));
            s.AnnualDates = ads.Count > 0 ? ads : new List<string> { "01-01" };

            s.HistoryDays = Math.Max(1, (int)(Json.Number(raw, "history_days") ?? 400));
            s.HistoryLimit = Math.Max(1, (int)(Json.Number(raw, "history_limit") ?? 200));
            return s;
        }

        /// <summary>"8:00, 18:00" -> ["08:00", "18:00"], sorted, no repeats, bad ones left out.</summary>
        public static List<string> HmList(IEnumerable<string> items)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var raw in items ?? new string[0])
                foreach (var part in (raw ?? "").Split(','))
                {
                    var t = DashboardConfig.ParseHm(part);
                    if (t.HasValue) set.Add(DashboardConfig.Hm(t.Value));
                }
            return set.ToList();
        }

        /// <summary>"1-1, 04-01" -> ["01-01", "04-01"]; 02-29 is allowed, 02-30 is not.</summary>
        public static List<string> MdList(IEnumerable<string> items)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var raw in items ?? new string[0])
                foreach (var part in (raw ?? "").Split(','))
                {
                    var p = part.Trim().Split('-');
                    int m, d;
                    if (p.Length != 2 || !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out m) ||
                        !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out d)) continue;
                    if (m < 1 || m > 12 || d < 1 || d > DateTime.DaysInMonth(2000, m)) continue;
                    set.Add(m.ToString("00", CultureInfo.InvariantCulture) + "-" + d.ToString("00", CultureInfo.InvariantCulture));
                }
            return set.ToList();
        }

        public Dictionary<string, object> ToJson()
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "type", Type },
                { "times", Times.Cast<object>().ToList() },
                { "weekdays", Weekdays.Cast<object>().ToList() },
                { "month_days", MonthDays.Select(d => (object)(double)d).ToList() },
                { "annual_dates", AnnualDates.Cast<object>().ToList() },
                { "time", Time },
                { "history_days", (double)HistoryDays },
                { "history_limit", (double)HistoryLimit }
            };
        }

        // ------------------------------------------------------------------ the calendar

        public bool HitsOn(DateTime day)
        {
            switch (Type)
            {
                case "Daily": return true;
                case "Weekly":
                    // Monday = 0 ... Sunday = 6, as the names are listed
                    int i = ((int)day.DayOfWeek + 6) % 7;
                    return Weekdays.Contains(WeekdayNames[i]);
                case "Monthly": return MonthDays.Contains(day.Day);
                case "Annual":
                    return AnnualDates.Contains(day.Month.ToString("00", CultureInfo.InvariantCulture) + "-" +
                                                day.Day.ToString("00", CultureInfo.InvariantCulture));
            }
            return false;
        }

        /// <summary>The report times on one day (none when the schedule does not fall on it).</summary>
        public List<DateTime> On(DateTime day)
        {
            var list = new List<DateTime>();
            day = day.Date;
            if (!HitsOn(day)) return list;
            if (Type == "Daily")
            {
                foreach (var t in Times)
                {
                    var hm = DashboardConfig.ParseHm(t);
                    if (hm.HasValue) list.Add(day + hm.Value);
                }
            }
            else
            {
                var hm = DashboardConfig.ParseHm(Time);
                list.Add(day + (hm ?? TimeSpan.Zero));
            }
            return list;
        }

        /// <summary>The last report time before <paramref name="t"/> (a day before it when none is found in 400 days).</summary>
        public DateTime Previous(DateTime t, int maxDaysBack = 400)
        {
            var today = On(t.Date).Where(x => x < t).ToList();
            if (today.Count > 0) return today.Max();
            for (int i = 1; i <= maxDaysBack; i++)
            {
                var c = On(t.Date.AddDays(-i));
                if (c.Count > 0) return c.Max();
            }
            return t.AddDays(-1);
        }

        /// <summary>The first report time after <paramref name="t"/> (a day after it when none is found in 400 days).</summary>
        public DateTime Next(DateTime t, int maxDays = 400)
        {
            DateTime? best = null;
            for (int i = 0; i <= maxDays; i++)
            {
                foreach (var c in On(t.Date.AddDays(i)))
                    if (c > t && (best == null || c < best.Value)) best = c;
                if (best != null) break;      // a later day's times are all later
            }
            return best ?? t.AddDays(1);
        }

        /// <summary>The range a report ending at <paramref name="end"/> covers: from the report time before it.</summary>
        public DateTime StartOf(DateTime end)
        {
            var s = Previous(end);
            return s >= end ? end.AddDays(-1) : s;
        }

        /// <summary>
        /// The report times between the first and last log, newest first - the
        /// "Auto-Report History" list: at most HistoryLimit, no older than HistoryDays
        /// before the last log.
        /// </summary>
        public List<DateTime> History(DateTime first, DateTime last)
        {
            var start = first.Date > last.AddDays(-HistoryDays).Date ? first.Date : last.AddDays(-HistoryDays).Date;
            var found = new SortedSet<DateTime>();
            for (var d = last.Date; d >= start && found.Count < HistoryLimit * 5; d = d.AddDays(-1))
                foreach (var c in On(d))
                    if (c >= first && c <= last) found.Add(c);
            return found.Reverse().Take(HistoryLimit).ToList();
        }
    }
}
