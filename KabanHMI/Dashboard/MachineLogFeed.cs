using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kaban.Core.Helpers;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// A MACHINE'S LOG DATABASE AS LOG ENTRIES - the rows Logs and Reports lists
    /// beside the HMI's own (2 October): each state the machine was in (with how
    /// long), each event, each alarm with the bits that were on, as the dashboard
    /// reads them.
    ///
    /// Read on a thread of its own, never on the page's: a period is read once and
    /// kept; a period that runs up to now is read again every RefreshSeconds. While
    /// a read runs, what was read last stays on show; <see cref="Changed"/> says
    /// when the new one is in.
    /// </summary>
    public sealed class MachineLogFeed
    {
        /// <summary>A period holding more logs than this is not listed (the status says so).</summary>
        public static int MaxRows = 200000;
        public static int RefreshSeconds = 15;

        private static readonly Dictionary<string, MachineLogFeed> _feeds =
            new Dictionary<string, MachineLogFeed>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The one feed of a machine's log (shared by every page that lists it).</summary>
        public static MachineLogFeed For(MachineDashboard machine)
        {
            lock (_feeds)
            {
                MachineLogFeed f;
                if (!_feeds.TryGetValue(machine.ModuleId ?? "", out f) || !ReferenceEquals(f._machine, machine))
                    _feeds[machine.ModuleId ?? ""] = f = new MachineLogFeed(machine);
                return f;
            }
        }

        private readonly MachineDashboard _machine;
        private readonly object _sync = new object();
        private List<AuditEntry> _entries = new List<AuditEntry>();
        private string _key = "", _reading = "";
        private DateTime _readAt = DateTime.MinValue;
        private int _version, _seq, _accepted;

        private MachineLogFeed(MachineDashboard machine) { _machine = machine; }

        public string ModuleId { get { return _machine.ModuleId; } }

        /// <summary>Why nothing (or not everything) is listed - "" when all is well.</summary>
        public string Problem { get; private set; } = "";

        /// <summary>True while a read is running.</summary>
        public bool Reading { get { lock (_sync) return _reading.Length > 0; } }

        /// <summary>Goes up each time a read brings new entries in.</summary>
        public int Version { get { lock (_sync) return _version; } }

        /// <summary>Raised on the reading thread when a read has finished.</summary>
        public event EventHandler Changed;

        /// <summary>The station the entries are put on (the machine's first PLC) - the PLC filter finds them with its writes.</summary>
        public int Station = 1;

        /// <summary>
        /// The user a machine's log entries are put on. The log database does not
        /// record who was signed in yet; until it does, its entries count as the
        /// administrator's, so the User filter takes them in or leaves them out
        /// like any other entry (2 October).
        /// </summary>
        public const string MachineUser = "admin";

        /// <summary>
        /// The entries of a period as far as they are read; starts a read when the
        /// period is new, or - when it runs up to now - when the last read is older
        /// than RefreshSeconds.
        /// </summary>
        public List<AuditEntry> Entries(DateTime from, DateTime to, bool followsNow)
        {
            var cfg = _machine.Config;
            string path = cfg.DatabasePath ?? "";
            string column = StateMap.ColumnOf(LanguageManager.CurrentLanguage);
            string key = path + "|" + from.Ticks + "|" + (followsNow ? "now" : to.Ticks.ToString(CultureInfo.InvariantCulture)) +
                         "|" + column + "|" + cfg.TimeZoneName;
            List<AuditEntry> have;
            bool start;
            lock (_sync)
            {
                have = _entries;
                bool stale = _key != key || (followsNow && (DateTime.Now - _readAt).TotalSeconds >= RefreshSeconds);
                start = stale && _reading != key;
                if (start) _reading = key;
            }
            if (start) Read(key, path, from, to, column, cfg.TimeZoneName);
            return have.Where(e => e.Time >= from && e.Time <= to).ToList();
        }

        private void Read(string key, string path, DateTime from, DateTime to, string column, string zoneName)
        {
            var map = _machine.Map;
            int station = Station;
            int seq;
            lock (_sync) seq = ++_seq;
            string source = _machine.ModuleId ?? "";
            Task.Run(() =>
            {
                var list = new List<AuditEntry>();
                string problem = "";
                try
                {
                    var zone = DashboardTime.Find(zoneName);
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        problem = DashboardData.Check(path, zone).Problem;
                    else
                    {
                        long n;
                        using (var c = DashboardData.Open(path))
                            n = DashboardData.Count(c, DashboardTime.ToUtc(from, zone), DashboardTime.ToUtc(to, zone));
                        if (n > MaxRows)
                            problem = string.Format(CultureInfo.CurrentCulture, LanguageManager.GetString("Log_MachineTooMany",
                                "{0:#,0} machine logs in this period - too many to list: choose a shorter period."), n);
                        else
                        {
                            var data = DashboardData.Load(path, from, to, zone, map, column);
                            list = ToEntries(DashboardData.Build(data, false, null), from, to, station, source);
                        }
                    }
                }
                catch (Exception ex)
                {
                    problem = LanguageManager.GetString("Dsh_DatabaseError", "The database could not be read:") + " " + ex.Message;
                }
                lock (_sync)
                {
                    if (_reading == key) _reading = "";
                    if (seq < _accepted) return;           // a newer read is already in
                    _accepted = seq;
                    _entries = list;
                    _key = key;
                    _readAt = DateTime.Now;
                    _version++;
                    Problem = problem;
                }
                var h = Changed;
                if (h != null) try { h(this, EventArgs.Empty); } catch { }
            });
        }

        /// <summary>What the dashboard shows for a range, as log entries (public for the tests).</summary>
        public static List<AuditEntry> ToEntries(DashboardView v, DateTime from, DateTime to, int station, string source)
        {
            var list = new List<AuditEntry>();
            string min = LanguageManager.GetString("Dsh_Min", "min");
            var last = v.States.Count > 0 ? v.States[v.States.Count - 1] : null;
            foreach (var s in v.States)
            {
                // the last state lasts on past the range's end: "still"
                bool still = ReferenceEquals(s, last) && s.End >= to;
                var end = s.End > to ? to : s.End;
                double minutes = Math.Max(0, (end - s.Start).TotalSeconds / 60.0);
                list.Add(new AuditEntry
                {
                    Time = s.Start, Kind = AuditKind.MachineState, Station = station, Source = source, User = MachineUser,
                    Text = s.Description + "  (" + minutes.ToString("0.0", CultureInfo.CurrentCulture) + " " + min +
                           (still ? ", " + LanguageManager.GetString("Log_MachineStill", "still") : "") + ")"
                });
            }
            foreach (var e in v.Events)
                list.Add(new AuditEntry
                {
                    Time = e.Time, Kind = AuditKind.MachineEvent, Station = station, Source = source, User = MachineUser,
                    Text = e.Description + (string.IsNullOrEmpty(e.Details) ? "" : " - " + e.Details)
                });
            foreach (var a in v.Alarms)
                list.Add(new AuditEntry
                {
                    Time = a.Time, Kind = AuditKind.MachineAlarm, Station = station, Source = source, User = MachineUser,
                    Text = (string.IsNullOrEmpty(a.Details) ? a.Description : a.Details)
                });
            return list.OrderBy(e => e.Time).ToList();
        }

        /// <summary>Forgets every feed (tests).</summary>
        public static void ResetForTests()
        {
            lock (_feeds) _feeds.Clear();
        }
    }
}
