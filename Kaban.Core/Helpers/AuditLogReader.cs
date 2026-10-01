using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Kaban.Core.Helpers
{
    /// <summary>What an audit entry is about - for the log viewer's filter.</summary>
    public enum AuditKind
    {
        Other = 0,
        /// <summary>Signing in and out, failed attempts, user accounts.</summary>
        SignIn,
        /// <summary>Controllers lost and back, connection diagnostics.</summary>
        Connection,
        /// <summary>Values and bits written to a controller from the HMI.</summary>
        PlcWrite,
        /// <summary>Something that went wrong: failed, refused, could not.</summary>
        Problem,
        /// <summary>Settings, files, layouts and restarts.</summary>
        Settings,
        /// <summary>
        /// One device written to a controller, as the driver was asked for it - the
        /// PLC write log (PlcWriteLog), not the audit log: every write, from every
        /// screen, with the value before it and what wrote it.
        /// </summary>
        DeviceWrite,
        /// <summary>A machine's own log (its log database, read by the HMI): a state it was in.</summary>
        MachineState,
        /// <summary>A machine's own log: an event (a cut, a program, a door...).</summary>
        MachineEvent,
        /// <summary>A machine's own log: an alarm it recorded.</summary>
        MachineAlarm
    }

    /// <summary>One entry of the audit log.</summary>
    public sealed class AuditEntry
    {
        public DateTime Time;
        public string User = "";
        /// <summary>The entry itself - several lines for a report such as a station scan.</summary>
        public string Text = "";
        public AuditKind Kind;

        /// <summary>The controller (HMI station number) the entry is about, when it names one.</summary>
        public int? Station;

        /// <summary>
        /// Where the entry comes from: "" for the HMI's own log, a machine's id for
        /// its log database (MachineState / MachineEvent / MachineAlarm).
        /// </summary>
        public string Source = "";

        /// <summary>The line as the log file holds it (the first line for a report).</summary>
        public string ToLogLine()
        {
            return "[" + Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "] - User '" + User + "' " + Text;
        }
    }

    /// <summary>
    /// READS the audit log back: the entries of a time range, from however many
    /// daily files it spans, for the Log Settings viewer and the PLC Diagnostics
    /// events. AuditLogger only ever wrote; the viewer used to show the last 200
    /// raw lines of today's file and nothing else.
    ///
    /// Files are read with sharing for writing (the logger appends while the
    /// viewer reads - see AuditLogger.Tail), and each file's entries are kept
    /// until it changes; today's file, the only one that grows, is read on from
    /// where the last read stopped rather than from the start every refresh.
    /// </summary>
    public static class AuditLogReader
    {
        // [2026-09-25 14:03:11] - User 'admin' wrote 120 to D210 ...
        private static readonly Regex LineRx = new Regex(
            @"^\[(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)\] - User '(.*?)' ?(.*)$", RegexOptions.Compiled);

        private sealed class FileCache
        {
            public long Offset;              // bytes parsed so far (whole lines only)
            public DateTime Written;
            public readonly List<AuditEntry> Entries = new List<AuditEntry>();
        }

        private static readonly Dictionary<string, FileCache> _cache =
            new Dictionary<string, FileCache>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        /// <summary>
        /// Every entry from <paramref name="from"/> to <paramref name="to"/>
        /// (both included), oldest first.
        /// </summary>
        public static List<AuditEntry> Read(DateTime from, DateTime to)
        {
            return Read(from, to, false);
        }

        /// <summary>
        /// The same, with the PLC write log's device writes among them when
        /// <paramref name="withDeviceWrites"/> (the log viewer): each one an entry of
        /// kind DeviceWrite, in time order with the rest.
        /// </summary>
        public static List<AuditEntry> Read(DateTime from, DateTime to, bool withDeviceWrites)
        {
            var result = ReadAudit(from, to);
            if (!withDeviceWrites) return result;
            var writes = PlcWriteLog.Read(from, to).Select(ToEntry).ToList();
            if (writes.Count == 0) return result;
            // The audit log keeps whole seconds, the write log milliseconds: an entry
            // of the same second as a write goes first (it is usually the action
            // that made it: "set bit M645 ...").
            var merged = new List<AuditEntry>(result.Count + writes.Count);
            int i = 0, j = 0;
            while (i < result.Count || j < writes.Count)
            {
                if (j >= writes.Count || (i < result.Count && result[i].Time <= writes[j].Time)) merged.Add(result[i++]);
                else merged.Add(writes[j++]);
            }
            return merged;
        }

        /// <summary>A device write as a log entry: "M645 = 1 (was 0) (PLC 1) - ManualCutting.btnSaw [Saw]: SET".</summary>
        public static AuditEntry ToEntry(PlcWriteLog.Entry w)
        {
            // The same entry every time it is read (the write log keeps its entries):
            // the viewer tells "nothing new" by the last entry being the same one.
            return _asEntry.GetValue(w, MakeEntry);
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PlcWriteLog.Entry, AuditEntry> _asEntry =
            new System.Runtime.CompilerServices.ConditionalWeakTable<PlcWriteLog.Entry, AuditEntry>();

        private static AuditEntry MakeEntry(PlcWriteLog.Entry w)
        {
            string text = w.Device + " = " + (w.Value.HasValue ? w.Value.Value.ToString(CultureInfo.InvariantCulture) : "?") +
                          (w.Value > 9 || w.Value < 0 ? " (" + w.Hex + ")" : "") +
                          ", " + (w.Before.HasValue ? "was " + w.Before.Value.ToString(CultureInfo.InvariantCulture) : "not read before") +
                          " (PLC " + w.Station.ToString(CultureInfo.InvariantCulture) + ")" +
                          (w.Ok ? "" : " " + w.ResultText) +
                          (string.IsNullOrEmpty(w.Source) ? "" : " - " + w.Source);
            return new AuditEntry { Time = w.Time, User = w.User, Text = text, Kind = AuditKind.DeviceWrite, Station = w.Station };
        }

        private static List<AuditEntry> ReadAudit(DateTime from, DateTime to)
        {
            var result = new List<AuditEntry>();
            if (to < from) return result;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var day in Days(from, to))
            {
                string file = AuditLogger.FileFor(day);
                used.Add(file);
                foreach (var e in EntriesOf(file))
                    if (e.Time >= from && e.Time <= to) result.Add(e);
            }
            // Only the files of the range asked for are kept: months of a busy
            // log held for a period nobody is looking at any more is memory the
            // panel PC needs for other things.
            lock (_lock)
                foreach (var stale in _cache.Keys.Where(k => !used.Contains(k)).ToList()) _cache.Remove(stale);
            return result;
        }

        /// <summary>The first day any log file is kept for (null when there is none).</summary>
        public static DateTime? OldestDay()
        {
            try
            {
                if (!Directory.Exists(AuditLogger.LogDirectory)) return null;
                DateTime? oldest = null;
                foreach (var f in Directory.GetFiles(AuditLogger.LogDirectory, AuditLogger.FilePrefix + "*.txt"))
                {
                    DateTime d;
                    string stem = Path.GetFileNameWithoutExtension(f).Substring(AuditLogger.FilePrefix.Length);
                    if (DateTime.TryParseExact(stem, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                        if (!oldest.HasValue || d < oldest.Value) oldest = d;
                }
                return oldest;
            }
            catch { return null; }
        }

        private static IEnumerable<DateTime> Days(DateTime from, DateTime to)
        {
            // A range reaching back before the oldest file need not look for
            // thousands of files that were never written.
            var oldest = OldestDay();
            var first = from.Date;
            if (oldest.HasValue && oldest.Value > first) first = oldest.Value;
            if (!oldest.HasValue) yield break;
            for (var d = first; d <= to.Date; d = d.AddDays(1)) yield return d;
        }

        private static List<AuditEntry> EntriesOf(string file)
        {
            lock (_lock)
            {
                FileCache c;
                if (!File.Exists(file)) { _cache.Remove(file); return new List<AuditEntry>(); }
                var info = new FileInfo(file);
                if (!_cache.TryGetValue(file, out c) || info.Length < c.Offset)
                {
                    c = new FileCache();            // new, or replaced by a shorter one
                    _cache[file] = c;
                }
                if (info.Length != c.Offset || info.LastWriteTimeUtc != c.Written)
                {
                    ReadOn(file, c);
                    c.Written = info.LastWriteTimeUtc;
                }
                return c.Entries;
            }
        }

        /// <summary>Parses what was appended since the last read - whole lines only.</summary>
        private static void ReadOn(string file, FileCache c)
        {
            try
            {
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length <= c.Offset) return;
                    fs.Seek(c.Offset, SeekOrigin.Begin);
                    var bytes = new byte[fs.Length - c.Offset];
                    int got = 0;
                    while (got < bytes.Length)
                    {
                        int n = fs.Read(bytes, got, bytes.Length - got);
                        if (n <= 0) break;
                        got += n;
                    }
                    // A line still being written is left for the next read.
                    int end = Array.LastIndexOf(bytes, (byte)'\n', got - 1);
                    if (end < 0) return;
                    string text = new UTF8Encoding(false).GetString(bytes, 0, end + 1);
                    c.Offset += end + 1;
                    foreach (var raw in text.Split('\n'))
                    {
                        string line = raw.TrimEnd('\r');
                        if (line.Length == 0) continue;
                        var m = LineRx.Match(line);
                        DateTime t;
                        if (m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd HH:mm:ss",
                                                                  CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                        {
                            c.Entries.Add(new AuditEntry
                            {
                                Time = t,
                                User = m.Groups[2].Value,
                                Text = m.Groups[3].Value,
                                Kind = Classify(m.Groups[3].Value),
                                Station = StationOf(m.Groups[3].Value)
                            });
                        }
                        else if (c.Entries.Count > 0)
                        {
                            // The next line of a report (a station scan writes
                            // several): part of the entry above it.
                            var last = c.Entries[c.Entries.Count - 1];
                            last.Text += "\n" + line;
                        }
                    }
                }
            }
            catch { /* a file being rotated away - the next refresh reads what is there */ }
        }

        /// <summary>Forgets what was read - for a test that rewrites its files.</summary>
        public static void ClearCache()
        {
            lock (_lock) _cache.Clear();
        }

        /// <summary>
        /// What an entry is about, from its wording. The log is free text written
        /// all over the program, so this goes by the phrases those places use;
        /// what matches none of them is Other - still found by the text search.
        /// </summary>
        public static AuditKind Classify(string text)
        {
            string t = (text ?? "").ToLowerInvariant();
            if (t.StartsWith("[plc diag]") || Regex.IsMatch(t, @"^station \d+ (lost|reconnected)") ||
                t.Contains("could not open plc station") || t.StartsWith("attempted to connect") ||
                t.StartsWith("attempted to disconnect") || t.StartsWith("opened connection help"))
                return AuditKind.Connection;
            if (t.StartsWith("logged in") || t.StartsWith("logged out") || t.Contains("login") ||
                t.StartsWith("signed in") || t.Contains(" user '") || t.StartsWith("deleted user") ||
                t.StartsWith("changed level of user") || t.StartsWith("added user") || t.StartsWith("created user"))
                return AuditKind.SignIn;
            if (t.Contains("unhandled") || t.Contains("failed") || t.Contains("could not") ||
                t.Contains("refused") || t.Contains(" not sent") || t.Contains("error"))
                return AuditKind.Problem;
            if (t.StartsWith("wrote ") || t.StartsWith("set bit") || t.StartsWith("set option") ||
                t.StartsWith("monitor wrote") || t.StartsWith("monitor imported and wrote") ||
                t.StartsWith("momentary release") || t.StartsWith("alarm reset") || t.StartsWith("speed override") ||
                t.StartsWith("pressed ") || t.StartsWith("released ") || t.StartsWith("set "))
                return AuditKind.PlcWrite;
            if (t.Contains("setting") || t.StartsWith("saved") || t.StartsWith("imported") || t.StartsWith("exported") ||
                t.StartsWith("loaded") || t.Contains("magnif") || t.StartsWith("restart") || t.Contains("layout") ||
                t.Contains("audit logging") || t.StartsWith("kept the"))
                return AuditKind.Settings;
            return AuditKind.Other;
        }

        // "(PLC 2)", "(PLC 2, control tag ...)", "PLC station 3", "station 1 lost",
        // "(station 2)" - and the same word in the other languages, as a controller's
        // caption writes it. Not "logical station" / "Mitsubishi station": those
        // are MX Component's numbers, not the HMI's.
        private static readonly Regex[] StationRx =
        {
            new Regex(@"\(PLC (\d+)[,)]", RegexOptions.Compiled),
            new Regex(@"\bPLC (?:station )?(\d+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"(?<!logical |Mitsubishi |logischen |mantıksal )\b(?:station|istasyon|станция|stazione|estación) (\d+)\b",
                      RegexOptions.Compiled | RegexOptions.IgnoreCase)
        };

        /// <summary>
        /// The controller an entry is about, from its wording - "(PLC 2)" as a
        /// write writes it, "station 3 lost", "(station 2)" in a controller's
        /// caption; null when it names none (a sign-in, a setting).
        /// </summary>
        public static int? StationOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (var rx in StationRx)
            {
                var m = rx.Match(text);
                int n;
                if (m.Success && int.TryParse(m.Groups[1].Value, out n)) return n;
            }
            return null;
        }

        /// <summary>
        /// The search box's rule: every word must appear, in the entry or its
        /// user's name, in any case. "D210 admin" finds admin's writes to D210.
        /// </summary>
        public static bool Matches(AuditEntry e, string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            string hay = (e.User + " " + e.Text).ToLowerInvariant();
            foreach (var w in search.ToLowerInvariant().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                if (!hay.Contains(w)) return false;
            return true;
        }
    }
}
