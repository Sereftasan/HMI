using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Kaban.Profiles.Data;

namespace Kaban.UI.Dashboard
{
    /// <summary>One row of main_log, in the shown time zone, with its details as one text.</summary>
    public sealed class LogRow
    {
        public long Id;
        public DateTime Time;
        public int Code;
        /// <summary>The description in the chosen language.</summary>
        public string Description = "";
        /// <summary>"desc: value; desc; ..." - an alarm's with the ": 1" of each bit left out.</summary>
        public string Details = "";
    }

    /// <summary>A machine state from its start to the next state, cut to the range.</summary>
    public sealed class StateSpan
    {
        public DateTime Start, End;
        public int Code;
        public string Description = "";
        /// <summary>Seconds / 60, as the old dashboard reckoned it (TimeSpan's own TotalMinutes differs in the last digit).</summary>
        public double Minutes { get { return (End - Start).TotalSeconds / 60.0; } }
    }

    /// <summary>Pieces cut with one length and one pair of angles.</summary>
    public sealed class CutGroup
    {
        public double Length, LeftAngle, RightAngle;
        public int Quantity;
    }

    /// <summary>
    /// WHAT THE PAGE SHOWS for a range (and a shift): the machine states, the events,
    /// the alarms, the cuts and the counts the filters offer. Built by
    /// <see cref="DashboardData.Build"/> exactly as the Industry 4 dashboard built its
    /// tables, so the two show the same numbers for the same database.
    /// </summary>
    public sealed class DashboardView
    {
        public DateTime Start, End;
        public DashboardConfig.Shift Shift;
        public List<StateSpan> States = new List<StateSpan>();
        public List<LogRow> Events = new List<LogRow>();
        public List<LogRow> Alarms = new List<LogRow>();
        public List<CutGroup> Cuts = new List<CutGroup>();
        public double TotalLengthMm;
        /// <summary>Event description -> how many, alarm name (the part of each detail before ':') -> how many.</summary>
        public Dictionary<string, int> EventCounts = new Dictionary<string, int>();
        public Dictionary<string, int> AlarmCounts = new Dictionary<string, int>();
        /// <summary>The main_log rows of the range (the state in force at its start too) - the "database tables" report.</summary>
        public List<LogRow> Main = new List<LogRow>();
        public bool Empty { get { return Main.Count == 0; } }
    }

    /// <summary>
    /// READS THE INDUSTRY 4 LOG - the SQLite file the logging service writes
    /// (main_log and log_details, times in UTC) - for a range of the shown zone.
    ///
    /// Opened read-only, through the SQLite the operating system has (see
    /// NativeSqliteConnection): the service keeps writing while the page reads.
    /// The indexes the old dashboard added are added once per file when the file
    /// can be written, as the old dashboard did - without them a month takes long.
    /// </summary>
    public static class DashboardData
    {
        /// <summary>The most main_log rows one range may hold: the panel program is 32-bit, a year of axis data would not fit.</summary>
        public static int MaxRows = 1500000;

        public const string DbFormat = "yyyy-MM-dd HH:mm:ss";
        private static readonly string[] DbFormats =
            { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.FFFFFFF", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm" };

        /// <summary>Removes the ": 1" / ": 1.0" of each bit of an alarm.</summary>
        private static readonly Regex ColonOne = new Regex(@": 1\.0|: 1(?!\d)", RegexOptions.Compiled);

        private static readonly HashSet<string> _indexed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public sealed class TooManyRowsException : Exception
        {
            public long Rows { get; private set; }
            public TooManyRowsException(long rows) : base("the range holds " + rows + " records") { Rows = rows; }
        }

        /// <summary>The raw rows of a range, before they are sorted into states, events and alarms.</summary>
        public sealed class Loaded
        {
            public DateTime Start, End;
            public List<LogRow> Rows = new List<LogRow>();
            /// <summary>main_log ids with a "Profile Data" detail (the barcoded cuts).</summary>
            public HashSet<long> Barcoded = new HashSet<long>();
            /// <summary>main_log id -> its "Profile Data" texts.</summary>
            public Dictionary<long, List<string>> ProfileData = new Dictionary<long, List<string>>();
        }

        // ------------------------------------------------------------------ the file

        public static DbConnection Open(string path, bool readOnly = true)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new FileNotFoundException("no database is set");
            if (!File.Exists(path)) throw new FileNotFoundException("the database is not found", path);
            var c = new NativeSqliteConnection { ConnectionString = "Data Source=" + path + ";Mode=" + (readOnly ? "ReadOnly" : "ReadWrite") };
            c.Open();
            return c;
        }

        /// <summary>The indexes the old dashboard made (once per file; nothing happens when the file is read-only).</summary>
        public static void EnsureIndexes(string path)
        {
            lock (_indexed)
            {
                if (_indexed.Contains(path ?? "")) return;
                _indexed.Add(path ?? "");
            }
            try
            {
                using (var c = Open(path, false))
                    foreach (var sql in new[]
                    {
                        "CREATE INDEX IF NOT EXISTS idx_main_log_date ON main_log(state_date)",
                        "CREATE INDEX IF NOT EXISTS idx_main_log_code_date ON main_log(state_code, state_date)",
                        "CREATE INDEX IF NOT EXISTS idx_log_details_main_id ON log_details(main_log_id)"
                    })
                        try { Exec(c, sql); } catch { }
            }
            catch { }
        }

        private static void Exec(DbConnection c, string sql, params object[] args)
        {
            using (var cmd = Command(c, sql, args)) cmd.ExecuteNonQuery();
        }

        private static DbCommand Command(DbConnection c, string sql, params object[] args)
        {
            var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i < args.Length; i++)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = "@p" + i;
                p.Value = args[i] ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            return cmd;
        }

        public static DateTime? ParseDb(object v)
        {
            if (v == null || v is DBNull) return null;
            DateTime t;
            return DateTime.TryParseExact(Convert.ToString(v, CultureInfo.InvariantCulture).Trim(), DbFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out t) ? t : (DateTime?)null;
        }

        public static string ToDb(DateTime t) { return t.ToString(DbFormat, CultureInfo.InvariantCulture); }

        private static int Code(object v)
        {
            if (v == null || v is DBNull) return -1;
            if (v is long) return (int)(long)v;
            double d;
            return double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out d)
                ? (int)d : -1;
        }

        /// <summary>The first and the last log, in the zone; null when the file has none (or cannot be read).</summary>
        public static DateTime[] Range(string path, TimeZoneInfo zone)
        {
            using (var c = Open(path))
            using (var cmd = Command(c, "SELECT MIN(state_date), MAX(state_date) FROM main_log"))
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                var a = ParseDb(r.GetValue(0));
                var b = ParseDb(r.GetValue(1));
                if (!a.HasValue || !b.HasValue) return null;
                return new[] { DashboardTime.ToLocal(a.Value, zone), DashboardTime.ToLocal(b.Value, zone) };
            }
        }

        /// <summary>How many main_log rows a range holds.</summary>
        public static long Count(DbConnection c, DateTime startUtc, DateTime endUtc)
        {
            using (var cmd = Command(c, "SELECT COUNT(*) FROM main_log WHERE state_date >= @p0 AND state_date < @p1", ToDb(startUtc), ToDb(endUtc)))
                return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A number as Python writes it (the old program's files): the fewest digits
        /// that read back as the same number - 52.55, 1.0, 77.56666666666666, 1e+16,
        /// 1e-05.
        /// </summary>
        public static string PyFloat(double d)
        {
            if (double.IsNaN(d)) return "nan";
            if (double.IsInfinity(d)) return d > 0 ? "inf" : "-inf";
            if (d == 0) return 1 / d < 0 ? "-0.0" : "0.0";
            string sign = d < 0 ? "-" : "";
            double a = Math.Abs(d);
            string e = null;
            for (int p = 1; p <= 17; p++)
            {
                e = a.ToString("E" + (p - 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                if (double.Parse(e, NumberStyles.Float, CultureInfo.InvariantCulture) == a) break;
            }
            int ei = e.IndexOf('E');
            string digits = e.Substring(0, ei).Replace(".", "").TrimEnd('0');
            if (digits.Length == 0) digits = "0";
            int exp = int.Parse(e.Substring(ei + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            if (exp >= 16 || exp < -4)
            {
                string mant = digits.Length > 1 ? digits.Substring(0, 1) + "." + digits.Substring(1) : digits;
                string ex = Math.Abs(exp).ToString("00", CultureInfo.InvariantCulture);
                return sign + mant + "e" + (exp < 0 ? "-" : "+") + ex;
            }
            if (exp >= 0)
            {
                string intPart = digits.Length > exp + 1 ? digits.Substring(0, exp + 1) : digits.PadRight(exp + 1, '0');
                string frac = digits.Length > exp + 1 ? digits.Substring(exp + 1) : "0";
                return sign + intPart + "." + frac;
            }
            return sign + "0." + new string('0', -exp - 1) + digits;
        }

        /// <summary>
        /// Every row of the range, and the state in force at its start. The
        /// descriptions and details in <paramref name="column"/>'s language.
        /// <paramref name="stop"/> is asked now and then; true abandons the read (null).
        /// </summary>
        public static Loaded Load(string path, DateTime startLocal, DateTime endLocal, TimeZoneInfo zone,
                                  StateMap map, string column, Func<bool> stop = null)
        {
            EnsureIndexes(path);
            var result = new Loaded { Start = startLocal, End = endLocal };
            var sUtc = DashboardTime.ToUtc(startLocal, zone);
            var eUtc = DashboardTime.ToUtc(endLocal, zone);
            var tr = map.TableOf(column);
            var descOf = new Dictionary<int, string>();
            Func<int, string> describe = code =>
            {
                string d;
                if (descOf.TryGetValue(code, out d)) return d;
                string raw = map.RawDescription(code);
                string t;
                d = tr.TryGetValue(raw, out t) ? t : raw;
                descOf[code] = d;
                return d;
            };

            using (var c = Open(path))
            {
                long n = Count(c, sUtc, eUtc);
                if (n > MaxRows) throw new TooManyRowsException(n);

                long? prevId = null;
                using (var cmd = Command(c, "SELECT id, state_date, state_code FROM main_log WHERE state_date < @p0 AND state_code IN (0,1,2,3,4,5) ORDER BY state_date DESC LIMIT 1", ToDb(sUtc)))
                using (var r = cmd.ExecuteReader())
                    if (r.Read())
                    {
                        var t = ParseDb(r.GetValue(1));
                        if (t.HasValue)
                        {
                            prevId = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                            int code = Code(r.GetValue(2));
                            result.Rows.Add(new LogRow { Id = prevId.Value, Time = DashboardTime.ToLocal(t.Value, zone), Code = code, Description = describe(code) });
                        }
                    }

                var byId = new Dictionary<long, LogRow>();
                using (var cmd = Command(c, "SELECT id, state_date, state_code FROM main_log WHERE state_date >= @p0 AND state_date < @p1 ORDER BY state_date", ToDb(sUtc), ToDb(eUtc)))
                using (var r = cmd.ExecuteReader())
                {
                    int k = 0;
                    while (r.Read())
                    {
                        if ((++k & 0x3FFF) == 0 && stop != null && stop()) return null;
                        var t = ParseDb(r.GetValue(1));
                        if (!t.HasValue) continue;
                        int code = Code(r.GetValue(2));
                        var row = new LogRow { Id = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture), Time = DashboardTime.ToLocal(t.Value, zone), Code = code, Description = describe(code) };
                        result.Rows.Add(row);
                        byId[row.Id] = row;
                    }
                }
                if (prevId.HasValue && !byId.ContainsKey(prevId.Value)) byId[prevId.Value] = result.Rows[0];
                // Rows of one second keep the order the database gave; states are re-sorted by time below.

                // the details, "desc: value" joined with "; " (a value "" or "0.0" gives the description alone)
                var sb = new StringBuilder();
                long current = long.MinValue;
                Action flush = () =>
                {
                    LogRow row;
                    if (current != long.MinValue && byId.TryGetValue(current, out row)) row.Details = sb.ToString();
                    sb.Length = 0;
                };
                Action<DbCommand> readDetails = cmd =>
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        int k = 0;
                        while (r.Read())
                        {
                            if ((++k & 0x3FFF) == 0 && stop != null && stop()) return;
                            long id = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                            if (id != current) { flush(); current = id; }
                            object dv = r.GetValue(1), num = r.GetValue(2), txt = r.GetValue(3);
                            string desc = dv is DBNull || dv == null ? "" : Convert.ToString(dv, CultureInfo.InvariantCulture);
                            string value;
                            if (!(txt is DBNull) && txt != null) value = Convert.ToString(txt, CultureInfo.InvariantCulture);
                            else if (!(num is DBNull) && num != null) value = PyFloat(Convert.ToDouble(num, CultureInfo.InvariantCulture));
                            else value = "";
                            if (desc == "Profile Data" && (!(txt is DBNull) || !(num is DBNull)))
                            {
                                result.Barcoded.Add(id);
                                List<string> pd;
                                if (!result.ProfileData.TryGetValue(id, out pd)) result.ProfileData[id] = pd = new List<string>();
                                pd.Add(value);
                            }
                            string t;
                            string shown = tr.TryGetValue(desc, out t) ? t : desc;
                            if (sb.Length > 0) sb.Append("; ");
                            sb.Append(shown);
                            if (value.Length > 0 && value != "0.0") sb.Append(": ").Append(value);
                        }
                    }
                };
                using (var cmd = Command(c, "SELECT d.main_log_id, d.log_description, d.deger, d.str_deger FROM log_details d " +
                                            "INNER JOIN main_log m ON d.main_log_id = m.id WHERE m.state_date >= @p0 AND m.state_date < @p1 " +
                                            "ORDER BY d.main_log_id, d.id", ToDb(sUtc), ToDb(eUtc)))
                    readDetails(cmd);
                flush();
                if (stop != null && stop()) return null;
                if (prevId.HasValue)
                {
                    current = long.MinValue;
                    using (var cmd = Command(c, "SELECT main_log_id, log_description, deger, str_deger FROM log_details WHERE main_log_id = @p0 ORDER BY id", prevId.Value))
                        readDetails(cmd);
                    flush();
                }
            }
            return result;
        }

        // ------------------------------------------------------------------ the tables

        /// <summary>
        /// States, events, alarms and cuts of the range - of each day's shift window
        /// when <paramref name="shift"/> is given (a shift ending at or before its start
        /// ends the next day).
        /// </summary>
        public static DashboardView Build(Loaded data, bool barcodedOnly, DashboardConfig.Shift shift)
        {
            var v = new DashboardView { Start = data.Start, End = data.End, Shift = shift };
            if (data.Rows.Count == 0) return v;

            // states: consecutive repeats dropped, each lasting until the next (the last a year past the range)
            var stateRows = data.Rows.Where(r => StateMap.IsState(r.Code)).OrderBy(r => r.Time).ToList();
            var spans = new List<StateSpan>();
            foreach (var r in stateRows)
            {
                if (spans.Count > 0 && spans[spans.Count - 1].Code == r.Code) continue;
                if (spans.Count > 0) spans[spans.Count - 1].End = r.Time;
                spans.Add(new StateSpan { Start = r.Time, End = data.End.AddDays(365), Code = r.Code, Description = r.Description });
            }

            var events = data.Rows.Where(r => !StateMap.IsState(r.Code) &&
                                              (!barcodedOnly || r.Code != StateMap.CuttingDone || data.Barcoded.Contains(r.Id))).ToList();
            var alarms = new List<LogRow>();
            foreach (var r in data.Rows.Where(r => r.Code == StateMap.Error))
                alarms.Add(new LogRow { Id = r.Id, Time = r.Time, Code = r.Code, Description = r.Description, Details = AlarmText(r.Details) });

            var windows = new List<KeyValuePair<DateTime, DateTime>>();
            if (shift == null) windows.Add(new KeyValuePair<DateTime, DateTime>(data.Start, data.End));
            else
                // from the day before: a night shift that began before the range still reaches into it
                for (var day = data.Start.Date.AddDays(-1); day <= data.End.Date; day = day.AddDays(1))
                {
                    var s = day + shift.Start;
                    var e = (shift.End <= shift.Start ? day.AddDays(1) : day) + shift.End;
                    var a = s > data.Start ? s : data.Start;
                    var b = e < data.End ? e : data.End;
                    if (a < b) windows.Add(new KeyValuePair<DateTime, DateTime>(a, b));
                }

            var cutIds = new List<long>();
            foreach (var w in windows)
            {
                foreach (var s in spans)
                {
                    if (!(s.Start < w.Value && s.End > w.Key)) continue;
                    var clip = new StateSpan
                    {
                        Start = s.Start > w.Key ? s.Start : w.Key,
                        End = s.End < w.Value ? s.End : w.Value,
                        Code = s.Code,
                        Description = s.Description
                    };
                    if (clip.End > clip.Start) v.States.Add(clip);
                }
                v.Events.AddRange(events.Where(r => r.Time >= w.Key && r.Time < w.Value));
                v.Alarms.AddRange(alarms.Where(r => r.Time >= w.Key && r.Time < w.Value));
                if (shift != null)
                {
                    var inWindow = data.Rows.Where(r => r.Time >= w.Key && r.Time < w.Value).ToList();
                    v.Main.AddRange(inWindow);
                    cutIds.AddRange(inWindow.Where(r => r.Code == StateMap.CuttingDone).Select(r => r.Id));
                }
            }
            if (shift == null)
            {
                v.Main.AddRange(data.Rows);
                cutIds.AddRange(data.Rows.Where(r => r.Code == StateMap.CuttingDone).Select(r => r.Id));
            }

            Cutting(data, cutIds, v);
            Counts(v);
            return v;
        }

        /// <summary>An alarm's details: each bit's name, without its ": 1".</summary>
        public static string AlarmText(string details)
        {
            return ColonOne.Replace(details ?? "", "").Trim(';', ' ');
        }

        /// <summary>
        /// "Profile Data" of the cuts: split at ';', [1] the length in tenths of a mm,
        /// [3] and [4] the angles in tenths of a degree - grouped by length and angles
        /// to 0.1, longest first.
        /// </summary>
        private static void Cutting(Loaded data, List<long> cutIds, DashboardView v)
        {
            var texts = new List<string>();
            foreach (var id in cutIds)
            {
                List<string> pd;
                if (data.ProfileData.TryGetValue(id, out pd)) texts.AddRange(pd);
            }
            var split = texts.Select(t => (t ?? "").Split(';')).ToList();
            if (split.Count == 0 || split.Max(p => p.Length) <= 4) return;
            var groups = new Dictionary<string, CutGroup>();
            foreach (var p in split)
            {
                double len;
                if (p.Length < 2 || !Num(p[1], out len)) continue;
                len /= 10.0;
                v.TotalLengthMm += len;
                double la, ra;
                if (p.Length < 5 || !Num(p[3], out la) || !Num(p[4], out ra)) continue;
                double l = Math.Round(len, 1), a = Math.Round(la / 10.0, 1), b = Math.Round(ra / 10.0, 1);
                string key = l.ToString("R", CultureInfo.InvariantCulture) + "|" + a.ToString("R", CultureInfo.InvariantCulture) + "|" + b.ToString("R", CultureInfo.InvariantCulture);
                CutGroup g;
                if (!groups.TryGetValue(key, out g)) groups[key] = g = new CutGroup { Length = l, LeftAngle = a, RightAngle = b };
                g.Quantity++;
            }
            v.Cuts = groups.Values.OrderByDescending(g => g.Length).ThenBy(g => g.LeftAngle).ThenBy(g => g.RightAngle).ToList();
        }

        private static bool Num(string s, out double d)
        {
            return double.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d) && !double.IsNaN(d);
        }

        private static void Counts(DashboardView v)
        {
            foreach (var e in v.Events)
            {
                int n;
                v.EventCounts.TryGetValue(e.Description, out n);
                v.EventCounts[e.Description] = n + 1;
            }
            foreach (var a in v.Alarms)
                foreach (var name in AlarmNames(a.Details))
                {
                    int n;
                    v.AlarmCounts.TryGetValue(name, out n);
                    v.AlarmCounts[name] = n + 1;
                }
        }

        /// <summary>The alarm names of an alarm's details (each item's part before ':').</summary>
        public static IEnumerable<string> AlarmNames(string details)
        {
            if (string.IsNullOrEmpty(details)) yield break;
            foreach (var part in details.Split(';'))
            {
                if (part.Trim().Length == 0) continue;
                string k = part.Split(':')[0].Trim();
                if (k.Length > 0) yield return k;
            }
        }

        // ------------------------------------------------------------------ shift metrics

        /// <summary>One row of the shift metrics: the share of time the machine worked, events by kind, alarms.</summary>
        public sealed class ShiftMetric
        {
            public DashboardConfig.Shift Shift;
            public double ActivePercent;
            public int Alarms;
            public Dictionary<string, int> Events = new Dictionary<string, int>();
        }

        public static bool InShift(TimeSpan t, DashboardConfig.Shift s)
        {
            if (s.Start < s.End) return s.Start <= t && t < s.End;
            return s.Start <= t || t < s.End;
        }

        /// <summary>
        /// Per shift, from what the page shows: states, events and alarms counted by
        /// the time of day they began.
        /// </summary>
        public static List<ShiftMetric> Metrics(DashboardView v, IEnumerable<DashboardConfig.Shift> shifts)
        {
            var list = new List<ShiftMetric>();
            foreach (var s in shifts)
            {
                var m = new ShiftMetric { Shift = s };
                double all = 0, active = 0;
                foreach (var st in v.States.Where(x => InShift(x.Start.TimeOfDay, s)))
                {
                    all += st.Minutes;
                    if (st.Code == StateMap.Active) active += st.Minutes;
                }
                m.ActivePercent = all > 0 ? active / all * 100.0 : 0.0;
                m.Alarms = v.Alarms.Count(a => InShift(a.Time.TimeOfDay, s));
                foreach (var e in v.Events.Where(x => InShift(x.Time.TimeOfDay, s)))
                {
                    int n;
                    m.Events.TryGetValue(e.Description, out n);
                    m.Events[e.Description] = n + 1;
                }
                list.Add(m);
            }
            return list;
        }
    }
}
