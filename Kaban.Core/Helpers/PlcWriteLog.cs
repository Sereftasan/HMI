using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// EVERY WRITE THE HMI MAKES TO A CONTROLLER, device by device (30 September).
    ///
    /// Recorded where every write passes - the driver PlcManager hands out
    /// (LoggedDriver) - so no screen, dialog or helper can write without a line
    /// here, whether or not its control logs its own action. One line per device
    /// changed: a digit-specified write (K4M100) is its sixteen bits, a bit of a
    /// word (D100.3) is that bit and the word.
    ///
    /// Daily files beside the audit log, <c>PlcWrites_yyyy-MM-dd.csv</c> (UTF-8,
    /// ";" - Excel opens it as it is):
    ///
    ///     Time;Station;Controller;Device;Value;Hex;Before;Result;User;Source
    ///     2026-09-30 14:32:05.120;1;Processing Center;M645;1;0x0001;0;OK;admin;ManualProfile_Recognition.unitFront_Clamping [Clamp]: momentary press
    ///
    /// Before is the value the HMI last read there ("" when it was not being
    /// read). Source says what wrote it: the control and page (see Source()), or
    /// the code that called the driver when no control said.
    ///
    /// NOT recorded: the bench simulation's own movement (SimulationMotion), which
    /// plays the PLC program's part on a simulated station and writes past the
    /// HMI's drivers on purpose.
    /// </summary>
    public static class PlcWriteLog
    {
        public const string FilePrefix = "PlcWrites_";
        public const string Header = "Time;Station;Controller;Device;Value;Hex;Before;Result;User;Source";
        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

        /// <summary>The file of a given day (it may not exist).</summary>
        public static string FileFor(DateTime day)
        {
            return Path.Combine(AuditLogger.LogDirectory, FilePrefix + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv");
        }

        /// <summary>One device written.</summary>
        public sealed class Entry
        {
            public DateTime Time;
            public int Station;
            public string Controller = "";
            public string Device = "";
            /// <summary>The new value (null: the word of a bit written as D100.3, when its value before was not known).</summary>
            public int? Value;
            public int? Before;
            /// <summary>The driver's answer: 0 is done, anything else failed (-1: refused, not connected, or the call failed).</summary>
            public int Result;
            public string User = "";
            public string Source = "";

            public bool Ok { get { return Result == 0; } }

            public string Hex
            {
                get
                {
                    if (!Value.HasValue) return "";
                    int v = Value.Value;
                    return "0x" + ((v & ~0xFFFF) == 0 || (v < 0 && v >= short.MinValue) ? (v & 0xFFFF).ToString("X4") : v.ToString("X8"));
                }
            }

            public string ResultText
            {
                get
                {
                    if (Result == 0) return "OK";
                    if (Result == -1) return "FAILED (refused or not connected)";
                    return "FAILED 0x" + Result.ToString("X8");
                }
            }

            public string ToCsv()
            {
                return string.Join(";", new[]
                {
                    Time.ToString(TimeFormat, CultureInfo.InvariantCulture),
                    Station.ToString(CultureInfo.InvariantCulture),
                    Clean(Controller), Clean(Device),
                    Value.HasValue ? Value.Value.ToString(CultureInfo.InvariantCulture) : "", Hex,
                    Before.HasValue ? Before.Value.ToString(CultureInfo.InvariantCulture) : "",
                    ResultText, Clean(User), Clean(Source)
                });
            }

            public static bool TryParse(string line, out Entry e)
            {
                e = null;
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("Time;", System.StringComparison.Ordinal)) return false;
                var f = line.Split(';');
                if (f.Length < 10) return false;
                DateTime t; int st, v, b;
                if (!DateTime.TryParseExact(f[0], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return false;
                if (!int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out st)) return false;
                bool hasValue = int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
                if (!hasValue && f[4].Length > 0) return false;
                e = new Entry
                {
                    Time = t, Station = st, Controller = f[2], Device = f[3], Value = hasValue ? v : (int?)null,
                    Before = int.TryParse(f[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out b) ? b : (int?)null,
                    Result = f[7] == "OK" ? 0 : ParseResult(f[7]),
                    User = f[8],
                    Source = string.Join(";", f.Skip(9))
                };
                return true;
            }

            private static int ParseResult(string text)
            {
                int i = text.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
                int rc;
                if (i >= 0 && int.TryParse(text.Substring(i + 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rc)) return rc;
                return -1;
            }

            private static string Clean(string s)
            {
                return (s ?? "").Replace(';', ',').Replace("\r", " ").Replace("\n", " ").Trim();
            }
        }

        // ---- what is writing ------------------------------------------------

        [ThreadStatic] private static List<string> _sources;

        /// <summary>
        /// Names what the writes made until the result is disposed come from -
        /// "ManualCutting.unitSaw_Motor [Saw Motor]: press" - for this thread:
        ///
        ///     using (PlcWriteLog.Source(...)) driver.Write(...);
        ///
        /// Nested: the innermost names them. Without one, the log names the code
        /// that called the driver.
        /// </summary>
        public static IDisposable Source(string text)
        {
            if (_sources == null) _sources = new List<string>();
            _sources.Add(text ?? "");
            return new Pop(_sources.Count - 1);
        }

        private sealed class Pop : IDisposable
        {
            private readonly int _depth;
            private bool _done;
            public Pop(int depth) { _depth = depth; }
            public void Dispose()
            {
                if (_done) return;
                _done = true;
                if (_sources != null && _sources.Count > _depth) _sources.RemoveRange(_depth, _sources.Count - _depth);
            }
        }

        /// <summary>What this thread's writes come from now, or null.</summary>
        public static string CurrentSource
        {
            get { return _sources != null && _sources.Count > 0 ? _sources[_sources.Count - 1] : null; }
        }

        /// <summary>Raised for each device written, as it is logged (the file follows a moment later) - for a test, or a live view.</summary>
        public static event Action<Entry> Written;

        // ---- recording ------------------------------------------------------

        private static readonly object _lock = new object();
        private static DateTime _lastCleanup = DateTime.MinValue;

        /// <summary>
        /// Runs the logging path once without logging anything, so its first real
        /// use - a button press - does not pay for loading it (tens of milliseconds
        /// on the UI thread). LoggedDriver calls it when a controller is registered.
        /// </summary>
        public static void Warm(int station)
        {
            try
            {
                var u = SecurityManager.CurrentUser;
                var c = CurrentSource ?? Caller();
                ControllerName(station);
                foreach (var d in Devices("K1M0", 0)) DeviceHistory.Value(station, d.Key);
                BitOfWordRx.Match("D0.0");
                new Entry { Time = DateTime.Now, Device = "D0", Value = 0 }.ToCsv();
            }
            catch { }
        }

        /// <summary>
        /// One write as the driver was asked for it: <paramref name="address"/> set to
        /// <paramref name="value"/> at <paramref name="time"/>, answered <paramref name="result"/>.
        /// Never throws.
        /// </summary>
        public static void Record(int station, string address, int value, int result, DateTime time)
        {
            try
            {
                string user = SecurityManager.CurrentUser ?? "Guest";
                string source = CurrentSource ?? Caller();
                string controller = ControllerName(station);
                var devices = Devices(address, value);
                var bitOfWord = BitOfWordRx.Match((address ?? "").Trim().ToUpperInvariant());
                if (devices.Count > 1 && !bitOfWord.Success)
                    source = "(" + (address ?? "").Trim().ToUpperInvariant() + " = " + value.ToString(CultureInfo.InvariantCulture) + ") " + source;
                // The device as it was written, too (K4M100 as a whole), so a reading of it
                // is known to be the HMI's.
                string whole = (address ?? "").Trim().ToUpperInvariant();
                if (result == 0 && devices.Count > 1 && !devices.Any(d => d.Key == whole))
                    DeviceHistory.RecordWrite(station, whole, value, time, user, source);
                foreach (var d in devices)
                {
                    var before = DeviceHistory.Value(station, d.Key);
                    int? now = d.Value;
                    if (!now.HasValue && bitOfWord.Success && before.HasValue)
                    {
                        // The word a bit was written into: its value before, with that bit changed.
                        int bitNo = Convert.ToInt32(bitOfWord.Groups[2].Value, 16);
                        now = value != 0 ? before.Value | (1 << bitNo) : before.Value & ~(1 << bitNo);
                    }
                    var e = new Entry
                    {
                        Time = time, Station = station, Controller = controller, Device = d.Key,
                        Value = now, Before = before,
                        Result = result, User = user, Source = source
                    };
                    if (e.Ok) DeviceHistory.RecordWrite(station, d.Key, d.Value, time, user, source);
                    Append(e);
                    var h = Written;
                    if (h != null) try { h(e); } catch { }
                }
            }
            catch { /* the log must never stop a write */ }
        }

        /// <summary>
        /// A run of devices written in one call (22 October: a PLC backup written back): ONE line - its
        /// first and last device and how many ("D2500 - D3499", "(1000 words)" after its source) - the
        /// values themselves are in the backup it came from.
        /// </summary>
        public static void RecordBlock(int station, string first, int words, bool bits, int result, DateTime time)
        {
            try
            {
                int points = bits ? words * 16 : words;
                string a = (first ?? "").Trim().ToUpperInvariant();
                int devices = PlcAddressHelper.IsDoubleWordDevice(a) ? (words + 1) / 2 : points;   // a 32-bit device's two words
                string prefix; long n;
                string last = PlcAddressHelper.TrySplit(a, out prefix, out n) ? PlcAddressHelper.Compose(prefix, n + Math.Max(0, devices - 1)) : a;
                var e = new Entry
                {
                    Time = time, Station = station, Controller = ControllerName(station),
                    Device = devices > 1 ? a + " - " + last : a, Result = result,
                    User = SecurityManager.CurrentUser ?? "Guest",
                    Source = (CurrentSource ?? Caller()) + " (" + points.ToString(CultureInfo.InvariantCulture) + (bits ? (points == 1 ? " bit" : " bits") : (points == 1 ? " word" : " words")) + ")"
                };
                Append(e);
                var h = Written;
                if (h != null) try { h(e); } catch { }
            }
            catch { /* the log must never stop a write */ }
        }

        private static readonly Regex DigitsRx = new Regex(@"^K([1-8])([A-Z]+[0-9A-F]+)$");
        private static readonly Regex BitOfWordRx = new Regex(@"^([A-Z]+[0-9A-F]+)\.([0-9A-F]+)$");

        /// <summary>
        /// The devices a write changes, each with its new value (null: a word of which
        /// only a bit was written). K4M100 = 243 is M100 ... M115; D100.3 = 1 is the
        /// bit and its word; anything else is itself.
        /// </summary>
        public static List<KeyValuePair<string, int?>> Devices(string address, int value)
        {
            var list = new List<KeyValuePair<string, int?>>();
            var a = (address ?? "").Trim().ToUpperInvariant();
            var digits = DigitsRx.Match(a);
            if (digits.Success)
            {
                int bits = int.Parse(digits.Groups[1].Value, CultureInfo.InvariantCulture) * 4;
                string first = digits.Groups[2].Value;
                for (int i = 0; i < bits; i++)
                    list.Add(new KeyValuePair<string, int?>(PlcAddressHelper.OffsetAddress(first, i), (value >> i) & 1));
                return list;
            }
            var bit = BitOfWordRx.Match(a);
            if (bit.Success)
            {
                list.Add(new KeyValuePair<string, int?>(a, value != 0 ? 1 : 0));
                list.Add(new KeyValuePair<string, int?>(bit.Groups[1].Value, null));
                return list;
            }
            list.Add(new KeyValuePair<string, int?>(a, value));
            return list;
        }

        private static string ControllerName(int station)
        {
            try
            {
                var s = PlcManager.Instance.GetStation(station);
                if (s == null) return "PLC " + station;
                return PlcManager.DescribeStation(s.OwnerModuleId, s.LocalNumber > 0 ? s.LocalNumber : station);
            }
            catch { return "PLC " + station; }
        }

        /// <summary>The code that called the driver, when no control named itself: "AlarmReset.Send".</summary>
        private static string Caller()
        {
            try
            {
                var trace = new System.Diagnostics.StackTrace(1, false);
                foreach (var frame in trace.GetFrames() ?? new System.Diagnostics.StackFrame[0])
                {
                    var m = frame.GetMethod();
                    var t = m != null ? m.DeclaringType : null;
                    if (t == null) continue;
                    string ns = t.Namespace ?? "";
                    if (t == typeof(PlcWriteLog) || t == typeof(LoggedDriver) || t == typeof(BoundedDriver) ||
                        ns.StartsWith("System", StringComparison.Ordinal) || ns.StartsWith("Microsoft", StringComparison.Ordinal)) continue;
                    // A lambda's class is nested in the one that wrote it.
                    while (t.DeclaringType != null && t.Name.IndexOf('<') >= 0) t = t.DeclaringType;
                    string method = m.Name;
                    int lt = method.IndexOf('<'), gt = method.IndexOf('>');
                    if (lt >= 0 && gt > lt) method = method.Substring(lt + 1, gt - lt - 1);
                    return t.Name + "." + method;
                }
            }
            catch { }
            return "";
        }

        // WRITTEN ON A THREAD OF ITS OWN. A write is made on the UI thread (a
        // button, a keypad); opening and appending a file there for every device
        // made a refused write on a dead link take tens of milliseconds instead of
        // none. The line is queued in the order written and the writer appends
        // what is queued; Flush waits for it (the viewer, a test, the program's end).
        private static readonly Queue<Entry> _pending = new Queue<Entry>();
        private static System.Threading.Thread _writer;
        private static int _inFlight;

        private static void Append(Entry e)
        {
            lock (_pending)
            {
                _pending.Enqueue(e);
                if (_writer == null)
                {
                    _writer = new System.Threading.Thread(WriteLoop) { IsBackground = true, Name = "PLC write log" };
                    _writer.Start();
                    // Whatever is queued when the program ends (or dies) is written first.
                    AppDomain.CurrentDomain.ProcessExit += (s, a) => Flush(2000);
                    AppDomain.CurrentDomain.UnhandledException += (s, a) => Flush(2000);
                }
                System.Threading.Monitor.PulseAll(_pending);
            }
        }

        private static void WriteLoop()
        {
            while (true)
            {
                List<Entry> batch;
                lock (_pending)
                {
                    while (_pending.Count == 0) System.Threading.Monitor.Wait(_pending);
                    batch = new List<Entry>(_pending);
                    _pending.Clear();
                    _inFlight = batch.Count;
                }
                WriteBatch(batch);
                lock (_pending)
                {
                    _inFlight = 0;
                    System.Threading.Monitor.PulseAll(_pending);
                }
            }
        }

        private static void WriteBatch(List<Entry> batch)
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(AuditLogger.LogDirectory);
                    foreach (var day in batch.GroupBy(x => x.Time.Date))
                    {
                        string file = FileFor(day.Key);
                        bool fresh = !File.Exists(file);
                        using (var fs = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                        using (var w = new StreamWriter(fs, new UTF8Encoding(true)))
                        {
                            if (fresh) w.WriteLine(Header);
                            foreach (var e in day) w.WriteLine(e.ToCsv());
                        }
                    }
                    if ((DateTime.Now - _lastCleanup).TotalHours > 24)
                    {
                        _lastCleanup = DateTime.Now;
                        foreach (var old in Directory.GetFiles(AuditLogger.LogDirectory, FilePrefix + "*.csv"))
                            if (File.GetLastWriteTime(old) < DateTime.Now.AddDays(-AuditLogger.RetainDays)) File.Delete(old);
                    }
                }
                catch { /* a full disk must not stop the machine */ }
            }
        }

        /// <summary>
        /// Waits until every write logged so far is in the file (at most
        /// <paramref name="timeoutMs"/>). True when it is.
        /// </summary>
        public static bool Flush(int timeoutMs = 2000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            lock (_pending)
            {
                while (_pending.Count > 0 || _inFlight > 0)
                {
                    var left = until - DateTime.UtcNow;
                    if (left <= TimeSpan.Zero) return false;
                    System.Threading.Monitor.Wait(_pending, left);
                }
            }
            return true;
        }

        // ---- reading back -----------------------------------------------------

        private sealed class FileCache
        {
            public long Offset;
            public readonly List<Entry> Entries = new List<Entry>();
        }

        private static readonly Dictionary<string, FileCache> _cache = new Dictionary<string, FileCache>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every device written from <paramref name="from"/> to <paramref name="to"/>, oldest first.</summary>
        public static List<Entry> Read(DateTime from, DateTime to)
        {
            Flush(500);                 // what was just written is read too
            var result = new List<Entry>();
            if (to < from) return result;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Files are kept RetainDays; a range reaching further back ("everything")
            // looks at no more than a year and a bit before its end.
            var first = from.Date;
            if (to.Date > DateTime.MinValue.AddDays(400) && first < to.Date.AddDays(-400)) first = to.Date.AddDays(-400);
            for (var day = first; day <= to.Date; day = day.AddDays(1))
            {
                string file = FileFor(day);
                used.Add(file);
                // under the lock: read from more than one thread (the Dashboard's own)
                lock (_cache)
                    foreach (var e in EntriesOf(file))
                        if (e.Time >= from && e.Time <= to) result.Add(e);
            }
            lock (_cache)
                foreach (var stale in _cache.Keys.Where(k => !used.Contains(k)).ToList()) _cache.Remove(stale);
            return result;
        }

        private static List<Entry> EntriesOf(string file)
        {
            lock (_cache)
            {
                try
                {
                    FileCache c;
                    if (!File.Exists(file)) { _cache.Remove(file); return new List<Entry>(); }
                    long length = new FileInfo(file).Length;
                    if (!_cache.TryGetValue(file, out c) || length < c.Offset) _cache[file] = c = new FileCache();
                    if (length > c.Offset)
                    {
                        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        {
                            fs.Seek(c.Offset, SeekOrigin.Begin);
                            var bytes = new byte[length - c.Offset];
                            int read = 0, n;
                            while (read < bytes.Length && (n = fs.Read(bytes, read, bytes.Length - read)) > 0) read += n;
                            // Whole lines only: a line being written stays for the next read.
                            int end = Array.LastIndexOf(bytes, (byte)'\n', read - 1 >= 0 ? read - 1 : 0);
                            if (end >= 0)
                            {
                                var text = Encoding.UTF8.GetString(bytes, 0, end + 1).TrimStart('﻿');
                                foreach (var line in text.Split('\n'))
                                {
                                    Entry e;
                                    if (Entry.TryParse(line.TrimEnd('\r'), out e)) c.Entries.Add(e);
                                }
                                c.Offset += end + 1;
                            }
                        }
                    }
                    return c.Entries;
                }
                catch { return new List<Entry>(); }
            }
        }
    }
}
