using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// THE HMI'S LOG IN THE MACHINE'S LOG DATABASE (29 October: "we should combine the HMI logs
    /// with this database to have a single logging database" - in its own tables, as its own
    /// codes: "we can consider HMI logs as the log in the industry 4 apart from the machine state
    /// code, just like the event codes inside it").
    ///
    /// Every entry of the HMI's log (AuditLogger) and every device of the PLC write log is a
    /// main_log row of the main machine's log database, code 1000 + its kind (LogDatabase), its
    /// time in UTC to the second as the machine's rows; its details:
    ///     an entry:          User (text), Text (text), PLC (number, when it names one)
    ///     a device written:  Write (text: the write log's own line - Time;Station;Controller;
    ///                        Device;Value;Hex;Before;Result;User;Source - its value as the number)
    /// The dashboard, its tables and the reports leave these codes out; the HMI's log lists
    /// (Logs and Reports, the Dashboard's All logs) read them from here.
    ///
    /// Written on a thread of its own, a batch a second. The daily files are still written
    /// first: at the start, what they hold after the database's last HMI row (as far back as
    /// the log is kept) is brought in, so the database has it all - and while the database
    /// cannot be read, the lists read the files. HMI rows older than the days the log is kept
    /// are taken out once a day; the machine's rows are never touched.
    /// </summary>
    public static class HmiLogStore
    {
        public const string DetailUser = "User", DetailText = "Text", DetailPlc = "PLC", DetailWrite = "Write";

        /// <summary>False: not started with the main window (tests start it themselves).</summary>
        public static bool AutoStart = true;

        /// <summary>The database the HMI's log goes into; null: the main machine's (tests set their own).</summary>
        public static Func<string> PathOverride;

        private static readonly object _sync = new object();
        private static readonly List<AuditEntry> _pending = new List<AuditEntry>();
        private static Thread _thread;
        private static volatile bool _stop;
        private static volatile bool _ready;
        private static DateTime _cleaned = DateTime.MinValue;
        private static string _path = "";

        /// <summary>The database the HMI's log is in now ("" when none: the files only).</summary>
        public static string CurrentPath { get { lock (_sync) return _path; } }

        /// <summary>True once what the files held is in the database, and the lists read it from there.</summary>
        public static bool Ready { get { return _ready; } }

        /// <summary>Why the database is not used ("" when it is).</summary>
        public static string Problem { get; private set; } = "";

        /// <summary>The database of the main machine's log - "" when no machine keeps one, or it cannot be made where it is set.</summary>
        public static string TargetPath()
        {
            var o = PathOverride;
            if (o != null) return o() ?? "";
            try
            {
                foreach (var m in Kaban.UI.Helpers.MachineLinks.WithLogs())
                {
                    string p = m.Dashboard.Config.DatabasePath ?? "";
                    if (p.Length == 0) continue;
                    if (File.Exists(p) || LogDatabase.CanMake(p)) return p;
                    return "";
                }
            }
            catch { }
            return "";
        }

        public static void Start()
        {
            lock (_sync)
            {
                if (_thread != null && _thread.IsAlive) return;
                _stop = false;
                _ready = false;
                _pending.Clear();
                _started = DateTime.Now;          // the files hold what came before; what comes after is caught as it comes
                AuditLogger.Logged -= OnLogged;
                AuditLogger.Logged += OnLogged;
                PlcWriteLog.Written -= OnWritten;
                PlcWriteLog.Written += OnWritten;
                AuditLogReader.Store = Read;
                _thread = new Thread(Loop) { IsBackground = true, Name = "HmiLogStore" };
                _thread.Start();
            }
        }

        /// <summary>Writes what is waiting and stops (the window closing).</summary>
        public static void Stop()
        {
            AuditLogger.Logged -= OnLogged;
            PlcWriteLog.Written -= OnWritten;
            _stop = true;
            var t = _thread;
            if (t != null && t.IsAlive) t.Join(5000);
            _thread = null;
            AuditLogReader.Store = null;
            _ready = false;
            ClearCache();
        }

        private static void OnLogged(AuditEntry e)
        {
            lock (_pending) _pending.Add(e);
        }

        private static void OnWritten(PlcWriteLog.Entry w)
        {
            var e = AuditLogReader.ToEntry(w);
            lock (_pending) _pending.Add(e);
            lock (_writes) _writes[e] = w;
        }

        /// <summary>The write log's entries of the device writes waiting (their own line goes into the database).</summary>
        private static readonly Dictionary<AuditEntry, PlcWriteLog.Entry> _writes = new Dictionary<AuditEntry, PlcWriteLog.Entry>();

        // ------------------------------------------------------------------ writing

        private static DateTime _started;

        /// <summary>Stopping now - the catch-up stops between days.</summary>
        private static bool _stopNow { get { return _stop; } }

        private static void Loop()
        {
            DateTime started = _started;
            DbConnection db = null;
            bool caughtUp = false;
            while (true)
            {
                bool stopping = _stop;
                try
                {
                    string path = TargetPath();
                    if (path.Length == 0)
                    {
                        Problem = "no machine keeps a log database";
                        CloseDb(ref db);
                        _ready = false;
                        lock (_sync) _path = "";
                        lock (_pending) { _pending.Clear(); }
                        lock (_writes) _writes.Clear();
                    }
                    else
                    {
                        if (db == null || !string.Equals(CurrentPath, path, StringComparison.OrdinalIgnoreCase))
                        {
                            CloseDb(ref db);
                            db = LogDatabase.OpenForWriting(path);
                            lock (_sync) _path = path;
                            caughtUp = false;
                            ClearCache();
                        }
                        if (!caughtUp)
                        {
                            CatchUp(db, started);
                            caughtUp = true;
                            _ready = true;
                            Problem = "";
                        }
                        WritePending(db);
                        if ((DateTime.Now - _cleaned).TotalHours >= 24) { _cleaned = DateTime.Now; Clean(db); }
                    }
                }
                catch (Exception ex)
                {
                    Problem = ex.Message;
                    _ready = false;
                    CloseDb(ref db);
                    caughtUp = false;
                    for (int i = 0; i < 40 && !_stop; i++) Thread.Sleep(50);    // try again in a couple of seconds
                }
                if (stopping) break;
                for (int i = 0; i < 20 && !_stop; i++) Thread.Sleep(50);
            }
            CloseDb(ref db);
        }

        private static void CloseDb(ref DbConnection db)
        {
            try { if (db != null) db.Dispose(); } catch { }
            db = null;
        }

        private static void WritePending(DbConnection db)
        {
            List<AuditEntry> batch;
            lock (_pending)
            {
                if (_pending.Count == 0) return;
                batch = new List<AuditEntry>(_pending);
            }
            Dictionary<AuditEntry, PlcWriteLog.Entry> writes;
            lock (_writes) writes = new Dictionary<AuditEntry, PlcWriteLog.Entry>(_writes);
            WriteEntries(db, batch, writes);
            lock (_pending) _pending.RemoveRange(0, Math.Min(batch.Count, _pending.Count));
            lock (_writes) foreach (var e in batch) _writes.Remove(e);
        }

        /// <summary>Entries into the database, in one transaction.</summary>
        public static void WriteEntries(DbConnection db, IList<AuditEntry> entries, IDictionary<AuditEntry, PlcWriteLog.Entry> writes = null)
        {
            if (entries.Count == 0) return;
            using (var tx = db.BeginTransaction())
            {
                foreach (var e in entries)
                {
                    PlcWriteLog.Entry w = null;
                    if (writes != null) writes.TryGetValue(e, out w);
                    var details = new List<LogDatabase.Detail>();
                    int code;
                    if (w != null)
                    {
                        code = LogDatabase.HmiCode(AuditKind.DeviceWrite);
                        details.Add(new LogDatabase.Detail(DetailWrite, w.Value.HasValue ? (double?)w.Value.Value : null, w.ToCsv()));
                    }
                    else
                    {
                        code = LogDatabase.HmiCode(e.Kind == AuditKind.DeviceWrite ? AuditKind.Other : e.Kind);
                        details.Add(new LogDatabase.Detail(DetailUser, null, e.User ?? ""));
                        details.Add(new LogDatabase.Detail(DetailText, null, e.Text ?? ""));
                        if (e.Station.HasValue) details.Add(new LogDatabase.Detail(DetailPlc, e.Station.Value, null));
                    }
                    LogDatabase.Insert(db, code, LogDatabase.ToDb(ToUtc(e.Time)), details);
                }
                tx.Commit();
            }
        }

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

        /// <summary>
        /// What the daily files hold after the database's last HMI row (and before the store
        /// started), as far back as the log is kept: the database then has the HMI's log whole.
        /// </summary>
        public static int CatchUp(DbConnection db, DateTime before)
        {
            DateTime? last = null;
            using (var cmd = LogDatabase.Command(db, "SELECT MAX(state_date) FROM main_log WHERE state_code BETWEEN @p0 AND @p1",
                                                 LogDatabase.HmiFirst, LogDatabase.HmiLast))
                last = DashboardData.ParseDb(cmd.ExecuteScalar());
            var from = DateTime.Now.AddDays(-Math.Max(1, AuditLogger.RetainDays));
            if (last.HasValue)
            {
                var lastLocal = ToLocal(last.Value).AddSeconds(1);
                if (lastLocal > from) from = lastLocal;
            }
            var to = new DateTime(before.Year, before.Month, before.Day, before.Hour, before.Minute, before.Second).AddTicks(-1);
            if (to < from) return 0;
            int count = 0;
            // a day at a time: months of a busy write log are not held at once
            for (var day = from.Date; day <= to.Date && !_stopNow; day = day.AddDays(1))
            {
                var a = day < from ? from : day;
                var b = day.AddDays(1).AddTicks(-1) > to ? to : day.AddDays(1).AddTicks(-1);
                var map = new Dictionary<AuditEntry, PlcWriteLog.Entry>();
                var all = new List<AuditEntry>(AuditLogReader.ReadFiles(a, b, false));
                foreach (var w in PlcWriteLog.Read(a, b))
                {
                    if (w.Time < a || w.Time > b) continue;
                    var e = AuditLogReader.ToEntry(w);
                    map[e] = w;
                    all.Add(e);
                }
                all = all.Select((e, i) => new { e, i }).OrderBy(x => x.e.Time).ThenBy(x => x.i).Select(x => x.e).ToList();
                for (int i = 0; i < all.Count; i += 2000)
                    WriteEntries(db, all.Skip(i).Take(2000).ToList(), map);
                count += all.Count;
            }
            if (count > 0)
                AuditLogger.Log("HMI log: " + count + " entries of the log files brought into the log database (" +
                                from.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " - " + to.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ").");
            return count;
        }

        /// <summary>The HMI's rows older than the days the log is kept, taken out (the machine's never).</summary>
        public static int Clean(DbConnection db)
        {
            string cut = LogDatabase.ToDb(ToUtc(DateTime.Now.AddDays(-Math.Max(1, AuditLogger.RetainDays))));
            using (var tx = db.BeginTransaction())
            {
                LogDatabase.Exec(db, "DELETE FROM log_details WHERE main_log_id IN (SELECT id FROM main_log WHERE state_code BETWEEN @p0 AND @p1 AND state_date < @p2)",
                                 LogDatabase.HmiFirst, LogDatabase.HmiLast, cut);
                int n;
                using (var cmd = LogDatabase.Command(db, "DELETE FROM main_log WHERE state_code BETWEEN @p0 AND @p1 AND state_date < @p2",
                                                     LogDatabase.HmiFirst, LogDatabase.HmiLast, cut))
                    n = cmd.ExecuteNonQuery();
                tx.Commit();
                return n;
            }
        }

        /// <summary>Waits until every entry logged so far is in the database (tests); true when it is.</summary>
        public static bool Flush(int timeoutMs = 5000)
        {
            var until = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < until)
            {
                bool empty;
                lock (_pending) empty = _pending.Count == 0;
                if (empty && _ready) return true;
                Thread.Sleep(20);
            }
            return false;
        }

        // ------------------------------------------------------------------ reading

        private sealed class Cache
        {
            public string Key = "";
            public long MaxId;
            public readonly List<AuditEntry> Entries = new List<AuditEntry>();
        }

        private static Cache _cache = new Cache();

        private static void ClearCache() { lock (_sync) _cache = new Cache(); }

        /// <summary>
        /// The HMI's log of a period from the database (AuditLogReader.Store): the entries in time
        /// order, with what is still waiting to be written. Null while the database is not ready
        /// (the lists read the files). Read on from where the last read of the same period's start stopped.
        /// </summary>
        public static List<AuditEntry> Read(DateTime from, DateTime to, bool withDeviceWrites)
        {
            if (!_ready) return null;
            string path = CurrentPath;
            if (path.Length == 0 || !File.Exists(path)) return null;
            string key = path + "|" + from.Ticks + "|" + withDeviceWrites;
            Cache c;
            lock (_sync) c = _cache;
            if (c.Key != key) { c = new Cache { Key = key }; lock (_sync) _cache = c; }
            lock (c)
            {
                var fromUtc = LogDatabase.ToDb(ToUtc(from).AddSeconds(-1));
                var toUtc = LogDatabase.ToDb(ToUtc(to).AddSeconds(1));
                using (var db = new NativeSqliteConnection { ConnectionString = "Data Source=" + path + ";Mode=ReadOnly" })
                {
                    db.Open();
                    string codes = withDeviceWrites ? "" : " AND m.state_code <> " + LogDatabase.HmiCode(AuditKind.DeviceWrite).ToString(CultureInfo.InvariantCulture);
                    using (var cmd = LogDatabase.Command(db,
                        "SELECT m.id, m.state_code, m.state_date, d.log_description, d.deger, d.str_deger FROM main_log m " +
                        "LEFT JOIN log_details d ON d.main_log_id = m.id " +
                        "WHERE m.state_code BETWEEN @p0 AND @p1 AND m.id > @p2 AND m.state_date >= @p3 AND m.state_date <= @p4" + codes +
                        " ORDER BY m.id, d.id", LogDatabase.HmiFirst, LogDatabase.HmiLast, c.MaxId, fromUtc, toUtc))
                    using (var r = cmd.ExecuteReader())
                    {
                        long current = -1;
                        int code = 0;
                        DateTime time = DateTime.MinValue;
                        string user = "", text = "", write = null;
                        int? plc = null;
                        bool any = false;
                        Action flush = () =>
                        {
                            if (!any) return;
                            var e = Entry(code, time, user, text, plc, write);
                            if (e != null) c.Entries.Add(e);
                        };
                        while (r.Read())
                        {
                            long id = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                            if (id != current)
                            {
                                flush();
                                current = id;
                                any = true;
                                if (id > c.MaxId) c.MaxId = id;
                                code = Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture);
                                var t = DashboardData.ParseDb(r.GetValue(2));
                                time = t.HasValue ? ToLocal(t.Value) : DateTime.MinValue;
                                user = ""; text = ""; write = null; plc = null;
                            }
                            object desc = r.GetValue(3), num = r.GetValue(4), str = r.GetValue(5);
                            string d = desc is DBNull || desc == null ? "" : Convert.ToString(desc, CultureInfo.InvariantCulture);
                            string s = str is DBNull || str == null ? null : Convert.ToString(str, CultureInfo.InvariantCulture);
                            if (d == DetailUser) user = s ?? "";
                            else if (d == DetailText) text = s ?? "";
                            else if (d == DetailWrite) write = s;
                            else if (d == DetailPlc && !(num is DBNull) && num != null) plc = Convert.ToInt32(Convert.ToDouble(num, CultureInfo.InvariantCulture));
                        }
                        flush();
                    }
                }
                var list = c.Entries.Where(e => e.Time >= from && e.Time <= to).ToList();
                lock (_pending)
                    foreach (var e in _pending)
                        if (e.Time >= from && e.Time <= to && (withDeviceWrites || e.Kind != AuditKind.DeviceWrite)) list.Add(e);
                // in time order; of one second, as they were written
                return list.Select((e, i) => new { e, i }).OrderBy(x => x.e.Time).ThenBy(x => x.i).Select(x => x.e).ToList();
            }
        }

        private static AuditEntry Entry(int code, DateTime time, string user, string text, int? plc, string write)
        {
            if (code == LogDatabase.HmiCode(AuditKind.DeviceWrite))
            {
                PlcWriteLog.Entry w;
                if (write != null && PlcWriteLog.Entry.TryParse(write, out w)) return AuditLogReader.ToEntry(w);
                return null;
            }
            var kind = (AuditKind)Math.Max(0, Math.Min(code - LogDatabase.HmiFirst, (int)AuditKind.DeviceWrite));
            return new AuditEntry { Time = time, User = user ?? "", Text = text ?? "", Kind = kind, Station = plc };
        }
    }
}
