using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// THE MACHINE'S LOG, TAKEN FROM ITS PLC BY THE HMI (29 October: "the form in Industry4Test
    /// reads the logs from PLC and writes them to database which we currently read to show them
    /// on the dashboard ... create the same logger tool in our application as well so we won't
    /// need the windows service anymore"). What Industry4Library's ServiceClass did, on the HMI's
    /// own connection to the machine's first PLC:
    ///
    /// The PLC's log area (its DeviceGroup + RefDevice: R27000):
    ///     +0   how many records are waiting           (the PLC)
    ///     +1   0 = the records may be read;  the HMI writes how many it took (the PLC then moves the rest up)
    ///     +10  the records, ten words each: the state code, year, month, day, hour, minute, second,
    ///          its values (the state map's Parameters: a word, or two for a Bit32) - a record whose code
    ///          CONTINUES another (PreviousCode) follows it, its values from its second word.
    ///
    /// At the start (and after every reconnection) each state of the map that starts a record has
    /// its ActiveDevice (L1000 ...) set to its Active: the PLC logs only what is switched on.
    ///
    /// A record goes into main_log (its code, the PLC's date as "yyyy-MM-dd HH:mm:ss") with its
    /// values in log_details (each value divided by its multiplier; a value of named bits: a row
    /// for each bit that is on, its name, 1). A "Process ID" can be looked up in KMSystem
    /// (PostgreSQL): its Profile Code and Profile Data join it. The answer (+1 = how many were
    /// taken) is written before the rows are committed - not answered, they are not kept, and the
    /// PLC sends them again. A record whose date cannot be one, or whose code the map does not
    /// know, is answered (taken) and left out, as the service did.
    ///
    /// The service checked every 150 ms; this every <see cref="PollMs"/>, on a thread of its own,
    /// its block reads giving way to the screen's (the driver's polite calls).
    /// </summary>
    public sealed class MachineLogger
    {
        public const string DefaultArea = "R27000";
        public const int RecordWords = 10;

        public static int PollMs = 250;

        /// <summary>False: nothing is started (the page designer; a test starts its own).</summary>
        public static bool AutoStart = true;

        /// <summary>
        /// The Industry 4 service (or its test form) running on this PC: the two would take each
        /// other's records, so the HMI waits while it runs (replaceable for the tests).
        /// </summary>
        public static Func<bool> ServiceRunning = DefaultServiceRunning;

        public static readonly string[] ServiceProcesses = { "Industry4Service", "Industry4Test" };

        private static bool DefaultServiceRunning()
        {
            try
            {
                foreach (var name in ServiceProcesses)
                {
                    var found = Process.GetProcessesByName(name);
                    bool any = found.Length > 0;
                    foreach (var p in found) p.Dispose();
                    if (any) return true;
                }
            }
            catch { }
            return false;
        }

        // ------------------------------------------------------------------ one machine

        public readonly MachineDashboard Machine;
        public readonly int Station;

        private Thread _thread;
        private volatile bool _stop;
        private readonly object _sync = new object();
        private string _status = "";
        private bool _activeWritten;
        private DbConnection _db;
        private string _dbPath = "";
        private KmSystem _km;

        public MachineLogger(MachineDashboard machine, int station)
        {
            Machine = machine;
            Station = station;
        }

        /// <summary>What the logger is doing, in words - for Logs and Reports.</summary>
        public string Status { get { lock (_sync) return _status; } }

        /// <summary>The records written since the start.</summary>
        public int Records { get; private set; }

        /// <summary>The date (as the PLC sent it) of the last record written.</summary>
        public DateTime? LastRecord { get; private set; }

        public bool Running { get { return _thread != null && _thread.IsAlive; } }

        private void Say(string text)
        {
            lock (_sync)
            {
                if (_status == text) return;
                _status = text;
            }
            var h = StatusChanged;
            if (h != null) try { h(this, EventArgs.Empty); } catch { }
        }

        public event EventHandler StatusChanged;

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        public void Start()
        {
            if (Running) return;
            _stop = false;
            _thread = new Thread(Loop) { IsBackground = true, Name = "MachineLogger " + Machine.ModuleId };
            _thread.Start();
        }

        public void Stop()
        {
            _stop = true;
            var t = _thread;
            if (t != null && t.IsAlive && !t.Join(3000)) { }
            CloseDb();
        }

        private void CloseDb()
        {
            try { if (_db != null) _db.Dispose(); } catch { }
            _db = null;
            _dbPath = "";
            try { if (_km != null) _km.Dispose(); } catch { }
            _km = null;
        }

        private void Loop()
        {
            int serviceCheck = 0;
            bool serviceThere = false;
            string lastProblem = "";
            while (!_stop)
            {
                int wait = PollMs;
                try
                {
                    var cfg = Machine.Config;
                    var map = Machine.Map;
                    string area = Area(cfg, Machine);
                    if (!cfg.PlcLogging)
                    {
                        Say(T("Plg_Off", "Switched off: the machine's log is not taken from its PLC."));
                        _activeWritten = false;
                        CloseDb();
                        wait = 1000;
                    }
                    else if (map.Count == 0)
                    {
                        Say(T("Plg_NoMap", "The machine has no state map (Dashboard\\StateMap.json): nothing to log."));
                        wait = 5000;
                    }
                    else
                    {
                        if (serviceCheck-- <= 0) { serviceThere = ServiceRunning(); serviceCheck = 40; }
                        if (serviceThere)
                        {
                            Say(T("Plg_Service", "Waiting: the Industry 4 service runs on this PC and takes the records - stop it (or uninstall it) and the HMI takes them."));
                            _activeWritten = false;
                            CloseDb();
                            wait = 1000;
                        }
                        else
                        {
                            var link = new DriverLink(Station);
                            if (!link.Connected)
                            {
                                Say(T("Plg_NotConnected", "Waiting for the PLC: not connected."));
                                _activeWritten = false;
                                wait = 1000;
                            }
                            else
                            {
                                if (!_activeWritten)
                                {
                                    var written = WriteActive(link, map);
                                    _activeWritten = true;
                                    AuditLogger.Log("Machine log (" + Machine.ModuleId + "): taken from PLC station " + Station + ", log area " + area +
                                                    " - the PLC told which records to log (" + written + ").");
                                }
                                string path = cfg.DatabasePath ?? "";
                                if (_db != null && !string.Equals(_dbPath, path, StringComparison.OrdinalIgnoreCase)) CloseDb();
                                // opened (made, the first time) when a record comes
                                Func<DbConnection> db = () =>
                                {
                                    if (_db == null) { _db = LogDatabase.OpenForWriting(path); _dbPath = path; }
                                    return _db;
                                };
                                Func<int, KmSystem.Label> lookup = null;
                                if (cfg.KmSystemLookup)
                                {
                                    if (_km == null || _km.Connection != cfg.KmSystemConnection) { if (_km != null) _km.Dispose(); _km = new KmSystem(cfg.KmSystemConnection); }
                                    var km = _km;
                                    lookup = id => km.Find(id);
                                }
                                var r = PollOnce(link, db, map, area, lookup);
                                if (r.Written > 0)
                                {
                                    Records += r.Written;
                                    LastRecord = r.Date;
                                    wait = 20;              // more may be waiting
                                }
                                if (!string.IsNullOrEmpty(r.Problem) && r.Problem != lastProblem)
                                    AuditLogger.Log("Machine log (" + Machine.ModuleId + "): " + r.Problem);
                                lastProblem = r.Problem ?? "";
                                Say(string.Format(CultureInfo.CurrentCulture, T("Plg_Running",
                                    "Taking the records from PLC station {0} ({1}): {2} written since the start{3}."), Station, area, Records,
                                    LastRecord.HasValue ? ", " + T("Plg_Last", "the last of") + " " + LastRecord.Value.ToString(DashboardData.DbFormat, CultureInfo.InvariantCulture) + " (UTC)" : ""));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Say(T("Plg_Error", "Could not take the records:") + " " + ex.Message);
                    string p = "could not take the records: " + ex.Message;
                    if (p != lastProblem) AuditLogger.Log("Machine log (" + Machine.ModuleId + "): " + p);
                    lastProblem = p;
                    CloseDb();
                    wait = 2000;
                }
                for (int slept = 0; slept < wait && !_stop; slept += 50) Thread.Sleep(Math.Min(50, wait - slept));
            }
            CloseDb();
        }

        /// <summary>The log area of the settings, else the machine's own, else R27000.</summary>
        public static string Area(DashboardConfig cfg, MachineDashboard machine)
        {
            string a = cfg != null ? cfg.PlcLogArea : "";
            if (string.IsNullOrWhiteSpace(a) && machine != null) a = machine.LogArea;
            return string.IsNullOrWhiteSpace(a) ? DefaultArea : a.Trim().ToUpperInvariant();
        }

        // ------------------------------------------------------------------ the PLC

        /// <summary>What the logger reads and writes through (a test gives its own).</summary>
        public interface ILink
        {
            bool Connected { get; }
            /// <summary><paramref name="count"/> words from <paramref name="first"/>.</summary>
            bool Read(string first, int count, out short[] words);
            /// <summary>0 when written.</summary>
            int Write(string device, int value);
        }

        /// <summary>
        /// The HMI's driver of a station: its block reads (polite to the screen's); its writes
        /// past the PLC write log - the records' answers are the logger's handshake with the PLC,
        /// one for every record, not something a user did (the audit log says when it starts).
        /// </summary>
        public sealed class DriverLink : ILink
        {
            private readonly IPlcDriver _driver, _writer;
            private readonly IPlcBlockAccess _block;

            public DriverLink(int station)
            {
                try { _driver = PlcManager.Instance.GetDriverForMonitoring(station); } catch { _driver = null; }
                _block = _driver as IPlcBlockAccess;
                _writer = PlcManager.Unwrap(_driver) ?? _driver;
            }

            public bool Connected
            {
                get { try { return _driver != null && _driver.IsConnected; } catch { return false; } }
            }

            public bool Read(string first, int count, out short[] words)
            {
                words = null;
                try
                {
                    if (_block != null) return _block.TryReadBlock(first, count, out words) && words != null && words.Length == count;
                    if (_driver == null) return false;
                    var names = Enumerable.Range(0, count).Select(i => PlcAddressHelper.OffsetAddress(first, i)).ToArray();
                    int[] v;
                    if (!_driver.TryReadMulti(names, out v) || v == null || v.Length != count) return false;
                    words = v.Select(x => unchecked((short)x)).ToArray();
                    return true;
                }
                catch { words = null; return false; }
            }

            public int Write(string device, int value)
            {
                try { return _writer == null ? -1 : _writer.Write(device, value); }
                catch { return -1; }
            }
        }

        /// <summary>Each state that starts a record: its ActiveDevice set to its Active. How many were written, in words.</summary>
        public static string WriteActive(ILink link, StateMap map)
        {
            int ok = 0, failed = 0;
            foreach (var e in map.Entries)
            {
                if (e.PreviousCode.HasValue || string.IsNullOrWhiteSpace(e.ActiveDevice)) continue;
                if (link.Write(e.ActiveDevice.Trim(), e.Active ? 1 : 0) == 0) ok++; else failed++;
            }
            return ok + " written" + (failed > 0 ? ", " + failed + " refused" : "");
        }

        // ------------------------------------------------------------------ one look at the PLC

        /// <summary>What one look found and did.</summary>
        public sealed class PollResult
        {
            /// <summary>Records written into the database.</summary>
            public int Written;
            /// <summary>Records answered and left out (an unknown code, a date that cannot be).</summary>
            public int Skipped;
            /// <summary>The date of the record written.</summary>
            public DateTime? Date;
            /// <summary>What went wrong, in words ("" when nothing did).</summary>
            public string Problem = "";
        }

        /// <summary>The chain of a record: its state, then each one that continues the one before.</summary>
        public static List<StateMap.Entry> Chain(StateMap map, int firstCode)
        {
            var chain = new List<StateMap.Entry>();
            var first = map.Find(firstCode);
            if (first == null) return chain;
            chain.Add(first);
            while (chain.Count < 32)
            {
                int last = chain[chain.Count - 1].Code;
                var next = map.Entries.FirstOrDefault(e => e.PreviousCode.HasValue && e.PreviousCode.Value == last && !chain.Contains(e));
                if (next == null) break;
                chain.Add(next);
            }
            return chain;
        }

        /// <summary>
        /// One look at the log area: a record (with the ones continuing it) taken into the
        /// database when the PLC has one ready. Public for the tests.
        /// </summary>
        public static PollResult PollOnce(ILink link, Func<DbConnection> openDb, StateMap map, string area, Func<int, KmSystem.Label> lookup = null)
        {
            var r = new PollResult();
            string answer = PlcAddressHelper.OffsetAddress(area, 1);
            string records = PlcAddressHelper.OffsetAddress(area, RecordWords);
            short[] head;
            if (!link.Read(area, RecordWords + 1, out head)) { r.Problem = "the log area " + area + " could not be read"; return r; }
            int waiting = head[0], busy = head[1], firstCode = head[RecordWords];
            if (waiting <= 0) return r;

            var chain = Chain(map, firstCode);
            if (chain.Count == 0)
            {
                // a code the map does not know: answered, so the PLC moves on (as the service did)
                link.Write(answer, 1);
                r.Skipped = 1;
                r.Problem = "a record of code " + firstCode + " - not in the state map - left out";
                return r;
            }
            int rows = chain.Count;
            if (waiting < rows || busy != 0) return r;          // not all of it there yet, or the PLC busy

            short[] data;
            if (!link.Read(records, RecordWords * rows, out data)) { r.Problem = "the records at " + records + " could not be read"; return r; }
            int taken = Math.Min(rows, waiting);

            DateTime date;
            bool dateOk = TryDate(data, out date);
            if (!dateOk || chain[0].PreviousCode.HasValue)
            {
                link.Write(answer, taken);
                r.Skipped = taken;
                r.Problem = dateOk ? "a record continuing another came first - left out"
                                   : "a record of code " + firstCode + " with no real date (" + string.Join("-", data.Skip(1).Take(6)) + ") - left out";
                return r;
            }

            var details = Details(chain, data);
            if (lookup != null)
            {
                var pid = details.FirstOrDefault(d => d.Description.Trim() == "Process ID" && d.Value.HasValue);
                if (pid != null)
                {
                    KmSystem.Label label = null;
                    try { label = lookup((int)pid.Value.Value); } catch { label = null; }
                    if (label != null)
                    {
                        details.Add(new LogDatabase.Detail("Profile Code", null, label.ProfileCode));
                        details.Add(new LogDatabase.Detail("Profile Data", null, label.ProfileData));
                    }
                }
            }

            var db = openDb();
            using (var tx = db.BeginTransaction())
            {
                LogDatabase.Insert(db, firstCode, LogDatabase.ToDb(date), details);
                int rc = link.Write(answer, taken);
                if (rc != 0)
                {
                    tx.Rollback();
                    r.Problem = "the answer to the PLC (" + answer + " = " + taken + ") was refused - the record is taken again";
                    return r;
                }
                tx.Commit();
            }
            r.Written = 1;
            r.Date = date;
            return r;
        }

        /// <summary>The record's date from its words 1 - 6 (year ... second).</summary>
        public static bool TryDate(short[] data, out DateTime date)
        {
            date = DateTime.MinValue;
            if (data == null || data.Length < 7) return false;
            try
            {
                date = new DateTime(data[1], data[2], data[3], data[4], data[5], data[6]);
                return true;
            }
            catch { return false; }
        }

        /// <summary>The values of a record, as the service wrote them.</summary>
        public static List<LogDatabase.Detail> Details(List<StateMap.Entry> chain, short[] data)
        {
            var list = new List<LogDatabase.Detail>();
            for (int i = 0; i < chain.Count; i++)
            {
                int at = i * RecordWords + (i == 0 ? 7 : 1);
                foreach (var p in chain[i].Params)
                {
                    int value = 0;
                    if (p.Bit32)
                    {
                        // as the service read them: past the words read, 0
                        if (at + 1 < data.Length) value = (data[at] & 0xFFFF) | (data[at + 1] << 16);
                        at += 2;
                    }
                    else
                    {
                        if (at < data.Length) value = data[at];
                        at += 1;
                    }
                    if (p.Bits == null || p.Bits.Count == 0)
                    {
                        list.Add(new LogDatabase.Detail(p.Description, (double)value / (p.Multiplier == 0 ? 1 : p.Multiplier), null));
                        continue;
                    }
                    // named bits: one row for each bit that is on (16 of the word, or 32 of a Bit32's two)
                    int width = p.Bits.Count == 32 ? 32 : 16;
                    for (int k = 0; k < width; k++)
                    {
                        string name = k < p.Bits.Count ? p.Bits[k] ?? "" : "";
                        if (name.Length == 0) continue;
                        if (((value >> k) & 1) != 0) list.Add(new LogDatabase.Detail(name, 1.0, null));
                    }
                }
            }
            return list;
        }

        // ------------------------------------------------------------------ KMSystem

        /// <summary>
        /// KMSYSTEM'S CUTTING LISTS (PostgreSQL): a Process ID's profile code and its label's data,
        /// as the service looked them up. Npgsql is not part of the HMI: its Npgsql.dll (and what
        /// it needs) beside the program, as the service had them, is used when it is there.
        /// </summary>
        public sealed class KmSystem : IDisposable
        {
            public sealed class Label { public string ProfileCode = "", ProfileData = ""; }

            public readonly string Connection;
            private DbConnection _c;
            private DateTime _retry = DateTime.MinValue;

            public KmSystem(string connection) { Connection = connection ?? ""; }

            /// <summary>Why it cannot look up ("" when it can).</summary>
            public string Problem { get; private set; } = "";

            private DbConnection Open()
            {
                if (_c != null && _c.State == System.Data.ConnectionState.Open) return _c;
                if (DateTime.Now < _retry) return null;
                try
                {
                    var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Npgsql");
                    if (asm == null)
                    {
                        string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Npgsql.dll");
                        if (!File.Exists(file)) { Problem = "Npgsql.dll is not beside the program"; _retry = DateTime.Now.AddMinutes(1); return null; }
                        asm = Assembly.LoadFrom(file);
                    }
                    var t = asm.GetType("Npgsql.NpgsqlConnection", true);
                    _c = (DbConnection)Activator.CreateInstance(t, Connection);
                    _c.Open();
                    Problem = "";
                    return _c;
                }
                catch (Exception ex)
                {
                    Problem = "KMSystem: " + ex.GetBaseException().Message;
                    try { if (_c != null) _c.Dispose(); } catch { }
                    _c = null;
                    _retry = DateTime.Now.AddSeconds(30);
                    return null;
                }
            }

            public Label Find(int processId)
            {
                var c = Open();
                if (c == null) return null;
                try
                {
                    var label = new Label();
                    label.ProfileData = Scalar(c, "select c2.\"Line\" from cuttinglistitems c join cuttingprofiles c2 on c.\"CuttingProfileId\" = c2.\"Id\" where c.\"No\" = @no", processId);
                    label.ProfileCode = Scalar(c, "select j.\"ProfileCode\" from cuttinglistheaders clh join Jobs j on j.\"Id\" = clh.\"JobFileId\" " +
                                                  "where clh.\"Id\" in ( select \"HeaderId\" from cuttinglistitems where \"No\" = @no )", processId);
                    return label;
                }
                catch (Exception ex)
                {
                    Problem = "KMSystem: " + ex.GetBaseException().Message;
                    try { c.Dispose(); } catch { }
                    _c = null;
                    return null;
                }
            }

            private static string Scalar(DbConnection c, string sql, int no)
            {
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = sql;
                    var p = cmd.CreateParameter();
                    p.ParameterName = "no";
                    p.Value = no;
                    cmd.Parameters.Add(p);
                    var v = cmd.ExecuteScalar();
                    return v == null || v is DBNull ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
                }
            }

            public void Dispose()
            {
                try { if (_c != null) _c.Dispose(); } catch { }
                _c = null;
            }
        }
    }

    /// <summary>The machines' loggers: one for each machine with a log database and a state map, started with the main window.</summary>
    public static class MachineLoggers
    {
        private static readonly List<MachineLogger> _all = new List<MachineLogger>();

        public static List<MachineLogger> All { get { lock (_all) return _all.ToList(); } }

        public static MachineLogger Of(string moduleId)
        {
            lock (_all) return _all.FirstOrDefault(l => string.Equals(l.Machine.ModuleId, moduleId ?? "", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Starts a logger for each machine that has a log database and a state map (once).</summary>
        public static void Start()
        {
            // only while the HMI does the Industry 4 service's job (HMI Settings > Options, 30 October)
            if (!MachineLogger.AutoStart || !HmiFeatures.LoggingOn) return;
            foreach (var m in Kaban.UI.Helpers.MachineLinks.WithLogs())
            {
                if (m.Dashboard == null || Of(m.ModuleId) != null) continue;
                var logger = new MachineLogger(m.Dashboard, m.MainStation);
                lock (_all) _all.Add(logger);
                logger.Start();
            }
        }

        public static void StopAll()
        {
            foreach (var l in All) l.Stop();
            lock (_all) _all.Clear();
        }
    }
}
