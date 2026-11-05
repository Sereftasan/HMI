using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Kaban.Core.Helpers;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// THE HMI'S OWN RECORDS OF A MACHINE (5 November: "combine HMI and PLC logging together so we could just take time sensitive
    /// logs from PLC ... and log the rest of them from the HMI itself. So we can create triggers to take logs for each axis unit or
    /// other pneumatic controls as well"; which ones: docs\industry4\HMI_INDUSTRY4_PLAN.md - "Your suggestion").
    ///
    /// Taken while HMI Settings > Options has "Machine details from the HMI (new machines)" on (with the Industry 4 logging in the
    /// HMI), for each machine keeping a log database: written into that database as the machine's own records - main_log rows of
    /// codes 1100 - 1199 (the HMI's named kinds, not the PLC's codes; the HMI's log is 1000 - 1099), their values in log_details -
    /// so the dashboard lists them with the PLC's records, as the machine's events (StateMap names them). Each kind is switched on
    /// or off, and its limits set, in the machine's Industry 4 Logging frame (Machine Parameters > Options) - kept in its dashboard
    /// settings ("hmi_records"). The time of a record: the panel's clock.
    ///
    /// The cylinders - AlarmGeneration's watches, timed from the output driving a movement to the feedback confirming it (read
    /// and timed for the records even while the HMI's own cylinder alarms are off: FA2010's PLC raises them):
    ///   Slow cylinder movement     slower than its normal time by more than the limit (50 %; and by 0.3 s at least - a scan is
    ///                              0.1 s): wear before it is a fault (seals, a sticking valve, low air or flow). Its normal time
    ///                              is learned from its first 20 movements (their median), kept in MovementNormals.cfg - learned
    ///                              again from the frame's button after a repair. One record a movement in 10 minutes at most,
    ///                              with how many were slow since the one before.
    ///   Cylinder movement not confirmed   its feedback not there within its delay (its alarm's): which signal failed.
    ///   Cylinder movement times    at the end of each shift (the dashboard's shifts; midnight when it has none): each movement's
    ///                              count, average and longest time, with its normal - the trend before anything fails.
    /// </summary>
    public static class MachineRecords
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        // ------------------------------------------------------------------ what is recorded

        /// <summary>The first and the last code of the HMI's records of a machine.</summary>
        public const int First = 1100, Last = 1199;

        public const int CylinderSlow = 1101, CylinderNotConfirmed = 1102, CylinderTimes = 1103;

        public static bool IsRecord(int code) { return code >= First && code <= Last; }

        /// <summary>A kind of record: its code, its settings key, its name (a translation key and its English).</summary>
        public sealed class Kind
        {
            public int Code;
            public string Key = "", TextKey = "", Text = "";
            /// <summary>The group it is listed in (the frame's): Cylinders, Axes, Inverters, Stops.</summary>
            public string Group = "";
            public string Shown { get { return T(TextKey, Text); } }
        }

        public static readonly IList<Kind> Kinds = new List<Kind>
        {
            new Kind { Code = CylinderSlow, Key = "cylinder_slow", TextKey = "Rec_CylinderSlow", Text = "Slow cylinder movement", Group = "Cylinders" },
            new Kind { Code = CylinderNotConfirmed, Key = "cylinder_not_confirmed", TextKey = "Rec_CylinderNotConfirmed", Text = "Cylinder movement not confirmed", Group = "Cylinders" },
            new Kind { Code = CylinderTimes, Key = "cylinder_times", TextKey = "Rec_CylinderTimes", Text = "Cylinder movement times", Group = "Cylinders" },
        };

        public static Kind KindOf(int code) { return Kinds.FirstOrDefault(k => k.Code == code); }

        /// <summary>The values' names as the database keeps them (English) and their translation keys - the dashboard shows them translated.</summary>
        public static readonly IList<KeyValuePair<string, string>> DetailNames = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(DUnit, "RecD_Unit"),
            new KeyValuePair<string, string>(DMovement, "RecD_Movement"),
            new KeyValuePair<string, string>(DTime, "RecD_Time"),
            new KeyValuePair<string, string>(DNormal, "RecD_Normal"),
            new KeyValuePair<string, string>(DSlower, "RecD_Slower"),
            new KeyValuePair<string, string>(DSlowSince, "RecD_SlowSince"),
            new KeyValuePair<string, string>(DWaited, "RecD_Waited"),
            new KeyValuePair<string, string>(DFailure, "RecD_Failure"),
            new KeyValuePair<string, string>(DCount, "RecD_Count"),
            new KeyValuePair<string, string>(DAverage, "RecD_Average"),
            new KeyValuePair<string, string>(DLongest, "RecD_Longest"),
            new KeyValuePair<string, string>(DFrom, "RecD_From"),
        };

        public const string DUnit = "Unit", DMovement = "Movement", DTime = "Time (s)", DNormal = "Normal (s)", DSlower = "Slower (%)",
                            DSlowSince = "Slow since the last record", DWaited = "Waited (s)", DFailure = "Failure", DCount = "Movements",
                            DAverage = "Average (s)", DLongest = "Longest (s)", DFrom = "From";

        // ------------------------------------------------------------------ settings, per machine

        public const double DefaultSlowPercent = 50;
        /// <summary>The least a movement must be slower by to be slow (a scan is 0.1 s).</summary>
        public const double SlowAtLeastSeconds = 0.3;
        /// <summary>One slow record a movement in this many minutes at most.</summary>
        public const double SlowGapMinutes = 10;
        /// <summary>Its normal: the median of this many first movements.</summary>
        public const int LearnCount = 20;

        /// <summary>The machine's settings (null: no such machine).</summary>
        private static DashboardConfig ConfigOf(string moduleId)
        {
            var m = DashboardService.Find(moduleId);
            return m == null ? null : m.Config;
        }

        /// <summary>The HMI takes its records of the machine: the option on, the machine keeping a log database.</summary>
        public static bool On(string moduleId)
        {
            if (!HmiFeatures.RecordsOn) return false;
            var c = ConfigOf(moduleId);
            return c != null && !string.IsNullOrWhiteSpace(c.DatabasePath);
        }

        /// <summary>A kind taken for the machine now (switched on in its frame - all are, until switched off).</summary>
        public static bool Taken(string moduleId, int code)
        {
            if (!On(moduleId)) return false;
            var k = KindOf(code);
            var c = ConfigOf(moduleId);
            return k != null && c != null && c.RecordOn(k.Key);
        }

        public static double SlowPercent(string moduleId)
        {
            var c = ConfigOf(moduleId);
            return c == null ? DefaultSlowPercent : c.RecordLimit("slow_percent", DefaultSlowPercent);
        }

        // ------------------------------------------------------------------ installed with the main window

        private static readonly Observer _observer = new Observer();

        /// <summary>Listens to the watches (once; the option decides what is taken).</summary>
        public static void Install() { AlarmGeneration.Records = _observer; }

        /// <summary>The shifts' times written so far, what waits written - the window closing.</summary>
        public static void Stop()
        {
            try { WriteTimes(DateTime.Now, true); } catch { }
            StopWriter();
        }

        private sealed class Observer : AlarmGeneration.IRecordsObserver
        {
            public bool On(string moduleId) { return MachineRecords.On(moduleId); }
            public void MovementDone(AlarmGeneration.Cylinder c, AlarmGeneration.CylinderSide s, double seconds, DateTime now) { Done(c, s, seconds, now); }
            public void MovementTooLong(AlarmGeneration.Cylinder c, AlarmGeneration.CylinderSide s, double seconds, int sub, DateTime now) { TooLong(c, s, seconds, sub, now); }
            public void Scanned(int station, Func<string, int> lookup, DateTime now) { MachineRecords.Scanned(station, lookup, now); }
        }

        /// <summary>Raised (on the scan's thread) for every record taken: its machine, its code, its values (tests, the frame).</summary>
        public static event Action<string, int, IList<LogDatabase.Detail>> Recorded;

        private static LogDatabase.Detail D(string description, double? value, string text = null) { return new LogDatabase.Detail(description, value, text); }

        private static double R(double v, int decimals = 2) { return Math.Round(v, decimals); }

        // ------------------------------------------------------------------ the cylinders

        /// <summary>A movement's key - its station, its error (or command) bit and the output driving it: not its name, which is translated.</summary>
        public static string KeyOf(AlarmGeneration.Cylinder c, AlarmGeneration.CylinderSide s)
        {
            return (c.ModuleId ?? "") + "|cyl|" + c.Station + "|" + (s.Code ?? "").Trim() + "|" + (s.Drive ?? "").Trim() + "|" + (s.DriveOn ? "1" : "0");
        }

        private static string MovementName(AlarmGeneration.CylinderSide s) { return string.IsNullOrEmpty(s.Name) ? s.Side : s.Name; }

        private sealed class SlowState { public DateTime? Last; public int Since; }
        private static readonly Dictionary<string, SlowState> _slow = new Dictionary<string, SlowState>();

        private static void Done(AlarmGeneration.Cylinder c, AlarmGeneration.CylinderSide s, double seconds, DateTime now)
        {
            string key = KeyOf(c, s);
            double? normal = Normals.Of(key);
            if (!normal.HasValue) { Normals.Learn(key, seconds); normal = Normals.Of(key); }

            // the shift's times
            if (Taken(c.ModuleId, CylinderTimes)) AddTime(c, s, key, seconds, now);

            // slow
            if (!normal.HasValue || !Taken(c.ModuleId, CylinderSlow)) return;
            double limit = SlowPercent(c.ModuleId);
            double over = seconds - normal.Value;
            if (over < SlowAtLeastSeconds || seconds <= normal.Value * (1 + limit / 100.0)) return;
            SlowState st;
            if (!_slow.TryGetValue(key, out st)) _slow[key] = st = new SlowState();
            st.Since++;
            if (st.Last.HasValue && (now - st.Last.Value).TotalMinutes < SlowGapMinutes) return;
            Add(c.ModuleId, CylinderSlow, now, new List<LogDatabase.Detail>
            {
                D(DUnit, null, c.Unit), D(DMovement, null, MovementName(s)), D(DTime, R(seconds)), D(DNormal, R(normal.Value)),
                D(DSlower, Math.Round(over / Math.Max(0.001, normal.Value) * 100.0)), D(DSlowSince, st.Since)
            });
            st.Last = now;
            st.Since = 0;
        }

        private static readonly Dictionary<string, DateTime> _tooLong = new Dictionary<string, DateTime>();

        private static void TooLong(AlarmGeneration.Cylinder c, AlarmGeneration.CylinderSide s, double seconds, int sub, DateTime now)
        {
            if (!Taken(c.ModuleId, CylinderNotConfirmed)) return;
            string key = KeyOf(c, s);
            DateTime last;
            // a movement tried again and again: one record a minute
            if (_tooLong.TryGetValue(key, out last) && (now - last).TotalSeconds < 60) return;
            _tooLong[key] = now;
            Add(c.ModuleId, CylinderNotConfirmed, now, new List<LogDatabase.Detail>
            {
                D(DUnit, null, c.Unit), D(DMovement, null, MovementName(s)), D(DWaited, R(seconds, 1)), D(DFailure, sub, AlarmGeneration.Describe(sub, s))
            });
        }

        // ---- the times of a shift ----

        private sealed class Times
        {
            public string Unit = "", Movement = "";
            public int Count;
            public double Sum, Longest;
        }

        private sealed class Period
        {
            public DateTime Start, Next;
            public readonly Dictionary<string, Times> Movements = new Dictionary<string, Times>();
        }

        /// <summary>module -> the period running.</summary>
        private static readonly Dictionary<string, Period> _periods = new Dictionary<string, Period>(StringComparer.OrdinalIgnoreCase);

        private static void AddTime(AlarmGeneration.Cylinder c, AlarmGeneration.CylinderSide s, string key, double seconds, DateTime now)
        {
            Period p;
            if (!_periods.TryGetValue(c.ModuleId ?? "", out p))
                _periods[c.ModuleId ?? ""] = p = new Period { Start = now, Next = NextShiftEnd(ConfigOf(c.ModuleId), now) };
            Times t;
            if (!p.Movements.TryGetValue(key, out t)) p.Movements[key] = t = new Times();
            t.Unit = c.Unit ?? "";
            t.Movement = MovementName(s);
            t.Count++;
            t.Sum += seconds;
            if (seconds > t.Longest) t.Longest = seconds;
        }

        /// <summary>
        /// The end of the shift running at <paramref name="now"/> (the PC's time): the next end of one of the dashboard's shifts, in its
        /// time zone - midnight there when it has none.
        /// </summary>
        public static DateTime NextShiftEnd(DashboardConfig cfg, DateTime now)
        {
            var zone = DashboardTime.Find(cfg != null ? cfg.TimeZoneName : "");
            var z = DashboardTime.ToLocal(ToUtc(now), zone);
            var ends = cfg != null ? cfg.Shifts.Select(x => x.End).Distinct().ToList() : new List<TimeSpan>();
            if (ends.Count == 0) ends.Add(TimeSpan.Zero);
            var best = DateTime.MaxValue;
            foreach (var e in ends)
            {
                var t = z.Date + TimeSpan.FromMinutes(Math.Floor(e.TotalMinutes) % (24 * 60));
                if (t <= z) t = t.AddDays(1);
                if (t < best) best = t;
            }
            return ToLocal(DashboardTime.ToUtc(best, zone));
        }

        /// <summary>The times of the periods ended (all of them when <paramref name="all"/>: the window closing) written.</summary>
        private static void WriteTimes(DateTime now, bool all)
        {
            foreach (var kv in _periods.ToList())
            {
                var p = kv.Value;
                if (!all && now < p.Next) continue;
                DateTime end = all && now < p.Next ? now : p.Next;
                if (Taken(kv.Key, CylinderTimes))
                {
                    var cfg = ConfigOf(kv.Key);
                    var zone = DashboardTime.Find(cfg != null ? cfg.TimeZoneName : "");
                    string from = DashboardTime.ToLocal(ToUtc(p.Start), zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                    foreach (var m in p.Movements.OrderBy(x => x.Value.Unit, StringComparer.CurrentCulture).ThenBy(x => x.Value.Movement))
                    {
                        if (m.Value.Count == 0) continue;
                        var details = new List<LogDatabase.Detail>
                        {
                            D(DUnit, null, m.Value.Unit), D(DMovement, null, m.Value.Movement), D(DCount, m.Value.Count),
                            D(DAverage, R(m.Value.Sum / m.Value.Count)), D(DLongest, R(m.Value.Longest))
                        };
                        var normal = Normals.Of(m.Key);
                        if (normal.HasValue) details.Add(D(DNormal, R(normal.Value)));
                        details.Add(D(DFrom, null, from));
                        Add(kv.Key, CylinderTimes, end, details);
                    }
                }
                if (all) _periods.Remove(kv.Key);
                else _periods[kv.Key] = new Period { Start = end, Next = NextShiftEnd(ConfigOf(kv.Key), end.AddSeconds(1)) };
            }
        }

        // ------------------------------------------------------------------ every scan

        private static DateTime _lastTimesCheck = DateTime.MinValue;

        private static void Scanned(int station, Func<string, int> lookup, DateTime now)
        {
            // the shifts ended: once a second is plenty
            if (Math.Abs((now - _lastTimesCheck).TotalSeconds) >= 1) { _lastTimesCheck = now; WriteTimes(now, false); }
        }

        // ------------------------------------------------------------------ the normal times

        /// <summary>
        /// WHAT IS NORMAL FOR A MOVEMENT (or a motor's load): the median of its first <see cref="LearnCount"/> values, kept in
        /// MovementNormals.cfg ("key=seconds"). Learned again after a repair: the frame's "Learn the normal times again".
        /// </summary>
        public static class Normals
        {
            public static string Path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "MovementNormals.cfg");

            private sealed class Normal
            {
                public double? Value;
                public readonly List<double> Samples = new List<double>();
            }

            private static readonly object Gate = new object();
            private static Dictionary<string, Normal> _all;
            private static string _readFrom;

            private static void Load()
            {
                if (_all != null && _readFrom == Path) return;
                _all = new Dictionary<string, Normal>(StringComparer.OrdinalIgnoreCase);
                _readFrom = Path;
                try
                {
                    if (!File.Exists(Path)) return;
                    foreach (var raw in File.ReadAllLines(Path, Encoding.UTF8))
                    {
                        var line = raw.Trim();
                        int eq = line.LastIndexOf('=');
                        if (line.Length == 0 || line.StartsWith("#") || eq <= 0) continue;
                        double v;
                        if (double.TryParse(line.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0)
                            _all[line.Substring(0, eq)] = new Normal { Value = v };
                    }
                }
                catch (Exception ex) { AuditLogger.Log("machine records: the normal times could not be read: " + ex.Message); }
            }

            private static void Save()
            {
                try
                {
                    var lines = new List<string> { "# Kaban HMI: the normal time of each movement (s) - learned from its first " + LearnCount + " movements" };
                    lines.AddRange(_all.Where(kv => kv.Value.Value.HasValue).OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                                       .Select(kv => kv.Key + "=" + kv.Value.Value.Value.ToString("0.###", CultureInfo.InvariantCulture)));
                    var dir = System.IO.Path.GetDirectoryName(Path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    SafeFile.WriteAllText(Path, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(false));
                }
                catch (Exception ex) { AuditLogger.Log("machine records: the normal times could not be written: " + ex.Message); }
            }

            /// <summary>Its normal (null while it is learned).</summary>
            public static double? Of(string key)
            {
                lock (Gate)
                {
                    Load();
                    Normal n;
                    return _all.TryGetValue(key ?? "", out n) ? n.Value : null;
                }
            }

            /// <summary>How many it has learned from so far (0 once learned).</summary>
            public static int Learning(string key)
            {
                lock (Gate)
                {
                    Load();
                    Normal n;
                    return _all.TryGetValue(key ?? "", out n) && !n.Value.HasValue ? n.Samples.Count : 0;
                }
            }

            /// <summary>One more value learned from; the normal set (and kept) with the last.</summary>
            public static void Learn(string key, double value)
            {
                lock (Gate)
                {
                    Load();
                    Normal n;
                    if (!_all.TryGetValue(key ?? "", out n)) _all[key ?? ""] = n = new Normal();
                    if (n.Value.HasValue) return;
                    n.Samples.Add(value);
                    if (n.Samples.Count < LearnCount) return;
                    var sorted = n.Samples.OrderBy(x => x).ToList();
                    int mid = sorted.Count / 2;
                    n.Value = sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
                    n.Samples.Clear();
                    Save();
                }
            }

            /// <summary>A machine's normals forgotten: learned again from the next movements (after a repair). How many were forgotten.</summary>
            public static int Forget(string moduleId)
            {
                lock (Gate)
                {
                    Load();
                    var keys = _all.Keys.Where(k => k.StartsWith((moduleId ?? "") + "|", StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var k in keys) _all.Remove(k);
                    Save();
                    return keys.Count;
                }
            }

            /// <summary>The machine's normals learned (tests, the frame).</summary>
            public static int CountOf(string moduleId)
            {
                lock (Gate)
                {
                    Load();
                    return _all.Count(kv => kv.Value.Value.HasValue && kv.Key.StartsWith((moduleId ?? "") + "|", StringComparison.OrdinalIgnoreCase));
                }
            }

            /// <summary>Read the file again (tests).</summary>
            public static void Reload() { lock (Gate) { _all = null; } }
        }

        // ------------------------------------------------------------------ into the machine's database

        private sealed class Pending
        {
            public string Path = "";
            public int Code;
            public string Date = "";
            public List<LogDatabase.Detail> Details;
        }

        private static readonly List<Pending> _queue = new List<Pending>();
        private static readonly object _writerGate = new object();
        private static Thread _writer;
        private static volatile bool _stop, _writing;

        /// <summary>Why the last write failed ("" when it did not).</summary>
        public static string Problem { get; private set; } = "";

        /// <summary>The records written since the start.</summary>
        public static int Written { get; private set; }

        /// <summary>A record of the machine taken: written into its database by the writer's thread.</summary>
        public static void Add(string moduleId, int code, DateTime time, IList<LogDatabase.Detail> details)
        {
            var c = ConfigOf(moduleId);
            string path = c == null ? "" : (c.DatabasePath ?? "").Trim();
            if (path.Length == 0) return;
            lock (_queue)
            {
                if (_queue.Count >= 20000) _queue.RemoveAt(0);          // a database gone for days: the oldest go
                _queue.Add(new Pending { Path = path, Code = code, Date = LogDatabase.ToDb(ToUtc(time)), Details = details.ToList() });
            }
            StartWriter();
            var h = Recorded;
            if (h != null) try { h(moduleId, code, details); } catch { }
        }

        private static void StartWriter()
        {
            lock (_writerGate)
            {
                if (_writer != null && _writer.IsAlive) return;
                _stop = false;
                _writer = new Thread(WriteLoop) { IsBackground = true, Name = "MachineRecords" };
                _writer.Start();
            }
        }

        private static void StopWriter()
        {
            _stop = true;
            var t = _writer;
            if (t != null && t.IsAlive) t.Join(5000);
            _writer = null;
        }

        private static void WriteLoop()
        {
            while (true)
            {
                bool stopping = _stop;
                List<Pending> batch;
                lock (_queue) batch = _queue.ToList();
                int wait = 300;
                if (batch.Count > 0)
                {
                    _writing = true;
                    try
                    {
                        foreach (var g in batch.GroupBy(p => p.Path, StringComparer.OrdinalIgnoreCase))
                        {
                            if (!File.Exists(g.Key) && !LogDatabase.CanMake(g.Key)) throw new IOException("the log database cannot be made: " + g.Key);
                            using (var db = LogDatabase.OpenForWriting(g.Key))
                            using (var tx = db.BeginTransaction())
                            {
                                foreach (var p in g) LogDatabase.Insert(db, p.Code, p.Date, p.Details);
                                tx.Commit();
                            }
                            lock (_queue) foreach (var p in g) _queue.Remove(p);
                            Written += g.Count();
                        }
                        Problem = "";
                    }
                    catch (Exception ex)
                    {
                        if (Problem != ex.Message) AuditLogger.Log("machine records: could not be written - " + ex.Message);
                        Problem = ex.Message;
                        wait = 2000;
                    }
                    finally { _writing = false; }
                }
                if (stopping) break;
                for (int slept = 0; slept < wait && !_stop; slept += 50) Thread.Sleep(50);
            }
        }

        /// <summary>Waits until every record taken is written (tests); true when they are.</summary>
        public static bool Flush(int timeoutMs = 5000)
        {
            var until = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < until)
            {
                bool empty;
                lock (_queue) empty = _queue.Count == 0;
                if (empty && !_writing) return true;
                Thread.Sleep(20);
            }
            return false;
        }

        /// <summary>Forgets what was being followed (tests).</summary>
        public static void ResetForTests()
        {
            _slow.Clear();
            _tooLong.Clear();
            _periods.Clear();
            _lastTimesCheck = DateTime.MinValue;
            lock (_queue) _queue.Clear();
        }

        // ------------------------------------------------------------------ time

        private static DateTime ToUtc(DateTime local)
        {
            try { return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local); }
            catch { return local.ToUniversalTime(); }
        }

        private static DateTime ToLocal(DateTime utc)
        {
            try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified), TimeZoneInfo.Local); }
            catch { return utc.ToLocalTime(); }
        }
    }
}
