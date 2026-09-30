using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// THE REPORT FILES - the Industry 4 dashboard's CSV files, the same names and
    /// columns, every field in quotes, so whatever reads them today still does:
    ///
    ///     event_log_&lt;start&gt;_&lt;end&gt;.csv   Timestamp, Description, Details
    ///     alarm_log_...                   Timestamp, Details
    ///     state_log_...                   Timestamp, EndTime, Description, Duration
    ///     main_log_... / log_details_...  the database's own rows (when asked for)
    ///
    /// with &lt;start&gt; and &lt;end&gt; as yyyyMMdd_HHmm.
    /// </summary>
    public static class DashboardReports
    {
        public const string StampFormat = "yyyyMMdd_HHmm";
        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const string ExportDateFormat = "dd.MM.yyyy HH:mm:ss";

        public static string Stamp(DateTime t) { return t.ToString(StampFormat, CultureInfo.InvariantCulture); }

        /// <summary>A report made from the page ends a second before a whole minute (as the old program's did).</summary>
        public static string ManualEndStamp(DateTime end)
        {
            return Stamp(end.Second == 0 ? end.AddSeconds(-1) : end);
        }

        /// <summary>One CSV field in quotes.</summary>
        private static string Q(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }

        private static void WriteCsv(string path, string[] head, IEnumerable<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", head.Select(Q))).Append("\r\n");
            foreach (var r in rows) sb.Append(string.Join(",", r.Select(Q))).Append("\r\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static string T(DateTime t) { return t.ToString(TimeFormat, CultureInfo.InvariantCulture); }

        /// <summary>
        /// Writes the report files the settings ask for into the export folder; the
        /// files made (a file that could not be written is left out).
        /// </summary>
        public static List<string> Save(DashboardConfig cfg, DashboardView v, string startStamp, string endStamp,
                                        StateMap map, string databasePath, TimeZoneInfo zone)
        {
            var files = new List<string>();
            string dir = cfg.ExportDirectory;
            try { Directory.CreateDirectory(dir); } catch { return files; }
            string tail = "_" + startStamp + "_" + endStamp + ".csv";

            Action<string, Action<string>> make = (name, write) =>
            {
                string path = Path.Combine(dir, name + tail);
                try { write(path); files.Add(path); } catch { }
            };

            if (cfg.Get("event_logs_report", true))
                make("event_log", p => WriteCsv(p, new[] { "Timestamp", "Description", "Details" },
                    v.Events.Select(e => new[] { T(e.Time), e.Description, e.Details })));
            if (cfg.Get("alarm_logs_report", true))
                make("alarm_log", p => WriteCsv(p, new[] { "Timestamp", "Details" },
                    v.Alarms.Select(a => new[] { T(a.Time), a.Details })));
            if (cfg.Get("state_logs_report", true))
                make("state_log", p => WriteCsv(p, new[] { "Timestamp", "EndTime", "Description", "Duration" },
                    v.States.Select(s => new[] { T(s.Start), T(s.End), s.Description, DashboardData.PyFloat(s.Minutes) })));
            if (cfg.Get("database_table_report", false) && v.Main.Count > 0)
            {
                make("main_log", p => WriteCsv(p, new[] { "id", "state_code", "state_date" },
                    v.Main.Select(r => new[]
                    {
                        r.Id.ToString(CultureInfo.InvariantCulture), map.RawDescription(r.Code),
                        r.Time.ToString(ExportDateFormat, CultureInfo.InvariantCulture)
                    })));
                make("log_details", p => WriteCsv(p, new[] { "id", "main_log_id", "log_description", "deger", "str_deger" },
                    DetailRows(databasePath, v.Main.Select(r => r.Id).ToList())));
            }
            return files;
        }

        /// <summary>The log_details rows of the given main_log rows, as the database-tables report writes them.</summary>
        private static List<string[]> DetailRows(string path, List<long> ids)
        {
            var rows = new List<string[]>();
            using (var c = DashboardData.Open(path))
                for (int i = 0; i < ids.Count; i += 500)
                {
                    var chunk = ids.Skip(i).Take(500).ToList();
                    using (var cmd = c.CreateCommand())
                    {
                        var names = new List<string>();
                        for (int k = 0; k < chunk.Count; k++)
                        {
                            var p = cmd.CreateParameter();
                            p.ParameterName = "@p" + k;
                            p.Value = chunk[k];
                            cmd.Parameters.Add(p);
                            names.Add("@p" + k);
                        }
                        cmd.CommandText = "SELECT id, main_log_id, log_description, deger, str_deger FROM log_details WHERE main_log_id IN (" +
                                          string.Join(",", names) + ") ORDER BY main_log_id, id";
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                            {
                                object num = r.GetValue(3), txt = r.GetValue(4), d = r.GetValue(2);
                                double n = num is DBNull || num == null ? 0 : Convert.ToDouble(num, CultureInfo.InvariantCulture);
                                string deger = n == Math.Floor(n) && Math.Abs(n) < 1e15
                                    ? ((long)n).ToString(CultureInfo.InvariantCulture) : DashboardData.PyFloat(n);
                                string s = txt is DBNull || txt == null ? "" : Convert.ToString(txt, CultureInfo.InvariantCulture);
                                // the lines of a barcode record ("Rec;...") are left out, as before
                                s = string.Join("\n", s.Replace("\r\n", "\n").Split('\n').Where(l => !l.StartsWith("Rec;", StringComparison.Ordinal)));
                                rows.Add(new[]
                                {
                                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                                    Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                                    d is DBNull || d == null ? "" : Convert.ToString(d, CultureInfo.InvariantCulture),
                                    deger, s
                                });
                            }
                    }
                }
            return rows;
        }

        // ------------------------------------------------------------------ automatic reports

        /// <summary>What an automatic run did (for the log and the tests).</summary>
        public sealed class RunResult
        {
            public List<string> Files = new List<string>();
            public List<string> Notes = new List<string>();
            public DateTime? NextDue;
        }

        /// <summary>
        /// One report ending at <paramref name="end"/>: from the scheduled time before it
        /// (not before the first log). False when the range holds no log or no file was made.
        /// </summary>
        public static bool MakeReport(DashboardConfig cfg, StateMap map, DateTime end, RunResult result)
        {
            var zone = DashboardTime.Find(cfg.TimeZoneName);
            var sched = cfg.Schedule;
            var start = sched.StartOf(end);
            var range = DashboardData.Range(cfg.DatabasePath, zone);
            if (range != null && start < range[0]) start = range[0];
            start = DashboardConfig.Minute(start);
            end = DashboardConfig.Minute(end);
            DashboardData.Loaded data;
            try { data = DashboardData.Load(cfg.DatabasePath, start, end, zone, map, cfg.ReportLanguage); }
            catch (Exception ex)
            {
                result.Notes.Add("the report ending " + end.ToString(DashboardConfig.DateFormatUi, CultureInfo.InvariantCulture) +
                                 " could not be made: " + ex.Message);
                return false;
            }
            if (data == null || data.Rows.Count == 0)
            {
                result.Notes.Add("no log from " + start.ToString(DashboardConfig.DateFormatUi, CultureInfo.InvariantCulture) +
                                 " to " + end.ToString(DashboardConfig.DateFormatUi, CultureInfo.InvariantCulture));
                return false;
            }
            var v = DashboardData.Build(data, cfg.BarcodedCuttingOnly, null);
            var files = Save(cfg, v, Stamp(start), Stamp(end), map, cfg.DatabasePath, zone);
            result.Files.AddRange(files);
            return files.Count > 0;
        }

        /// <summary>
        /// THE AUTOMATIC REPORTS, as the old program's report run: the report of the
        /// last scheduled time; before it, when the one due (required_report_end_date)
        /// was missed - the panel was off - that one too; then the next one due is
        /// kept. Nothing happens while the database cannot be read.
        /// </summary>
        public static RunResult RunDue(DashboardConfig cfg, StateMap map, DateTime now)
        {
            var result = new RunResult();
            var zone = DashboardTime.Find(cfg.TimeZoneName);
            DateTime[] range;
            try { range = DashboardData.Range(cfg.DatabasePath, zone); }
            catch (Exception ex) { result.Notes.Add("the database cannot be read: " + ex.Message); return result; }
            if (range == null) { result.Notes.Add("the database holds no log"); return result; }

            var sched = cfg.Schedule;
            var required = cfg.RequiredReportEnd;
            var latest = sched.Previous(now);
            DateTime? done = null;

            if (required.HasValue && required.Value < latest.AddMinutes(-10))
            {
                result.Notes.Add("the report due " + required.Value.ToString(DashboardConfig.DateFormatUi, CultureInfo.InvariantCulture) + " was missed: made now");
                if (MakeReport(cfg, map, required.Value, result)) done = required.Value;
            }
            if (MakeReport(cfg, map, latest, result)) done = latest;
            else if (!done.HasValue) done = latest;

            result.NextDue = sched.Next(DashboardConfig.Minute(done.Value));
            cfg.RequiredReportEnd = result.NextDue;
            return result;
        }

        /// <summary>True when the reports are switched on and one is due.</summary>
        public static bool Due(DashboardConfig cfg, DateTime now)
        {
            if (!cfg.AutoReports) return false;
            var r = cfg.RequiredReportEnd;
            return !r.HasValue || now >= r.Value;
        }
    }
}
