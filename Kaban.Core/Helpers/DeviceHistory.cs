using System;
using System.Collections.Generic;
using System.Linq;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// WHEN A DEVICE LAST CHANGED, AND WHETHER THE HMI DID IT (30 September).
    ///
    /// Every value the HMI reads - the page scanner, the PLC Monitor - is shown
    /// here as an observation; every value it writes (PlcWriteLog) as a write. A
    /// change seen between two readings is the HMI's when the HMI wrote that very
    /// value in between; otherwise the PLC program (or the machine) changed it.
    /// That is the question the write log exists to answer: "did the HMI set
    /// this, or the ladder?"
    ///
    /// Known only while something reads the device: a change before the first
    /// reading of it is not known (the PLC Monitor shows "-"). Thread-safe: the
    /// scanner observes on its worker thread, the monitor on its own.
    /// </summary>
    public static class DeviceHistory
    {
        /// <summary>A write made up to this long before the earlier reading may still be the one it missed.</summary>
        private static readonly TimeSpan Slack = TimeSpan.FromSeconds(1);

        /// <summary>What is known of one device.</summary>
        public sealed class Info
        {
            public int? Value;
            /// <summary>When it was last read.</summary>
            public DateTime? Seen;
            /// <summary>When a reading first showed its present value (null: no change seen since the first reading).</summary>
            public DateTime? Changed;
            /// <summary>Its value before that change.</summary>
            public int? From;
            /// <summary>True: the HMI wrote the new value. False: nothing on the HMI wrote it - the PLC program did.</summary>
            public bool ByHmi;
            /// <summary>For an HMI change: when it was written, by whom and from where.</summary>
            public DateTime? WriteTime;
            public string WriteUser = "", WriteSource = "";
            /// <summary>The HMI's last write to it, whatever came of it (null: none since the program started).</summary>
            public DateTime? LastWrite;
            public int? LastWriteValue;
        }

        private sealed class Write
        {
            public DateTime Time;
            public int? Value;          // null: the device was changed as a whole word (a bit of it written)
            public string User = "", Source = "";
        }

        private sealed class Record
        {
            public int? Value;
            public DateTime ReadStart, Seen;
            public DateTime? Changed, WriteTime;
            public int? From;
            public bool ByHmi;
            public string WriteUser = "", WriteSource = "";
            public readonly List<Write> Writes = new List<Write>();
        }

        private static readonly Dictionary<string, Record> _records = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        private static string Key(int station, string device)
        {
            return station + "|" + (device ?? "").Trim().ToUpperInvariant();
        }

        /// <summary>Forgets everything (a test).</summary>
        public static void Clear() { lock (_lock) _records.Clear(); }

        /// <summary>
        /// A reading: <paramref name="values"/>[i] of <paramref name="devices"/>[i],
        /// read between <paramref name="started"/> and <paramref name="finished"/>.
        /// A reading older than the last one taken (a slow read finishing late) is
        /// ignored - it would flip the value back.
        /// </summary>
        public static void Observe(int station, IList<string> devices, IList<int> values, DateTime started, DateTime finished)
        {
            if (devices == null || values == null) return;
            int n = Math.Min(devices.Count, values.Count);
            var names = new List<string>(n);
            var read = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                names.Add(devices[i]); read.Add(values[i]);
                // A digit-specified device (K4M100) is its bits as well: a write logs them one by one.
                var d = (devices[i] ?? "").Trim();
                if (d.Length > 2 && (d[0] == 'K' || d[0] == 'k') && char.IsDigit(d[1]))
                    foreach (var bit in PlcWriteLog.Devices(d, values[i]))
                        if (bit.Value.HasValue) { names.Add(bit.Key); read.Add(bit.Value.Value); }
            }
            lock (_lock)
            {
                for (int i = 0; i < names.Count; i++)
                {
                    var device = names[i];
                    if (string.IsNullOrWhiteSpace(device)) continue;
                    var key = Key(station, device);
                    Record r;
                    if (!_records.TryGetValue(key, out r)) _records[key] = r = new Record();
                    if (r.Value.HasValue && started < r.ReadStart) continue;
                    int v = read[i];
                    if (r.Value.HasValue && r.Value.Value != v)
                    {
                        // The change happened after the last reading began and before
                        // this one ended. Was it the HMI's write of this value?
                        var from = r.ReadStart - Slack;
                        var w = r.Writes.LastOrDefault(x => x.Time > from && x.Time <= finished && (!x.Value.HasValue || x.Value.Value == v));
                        r.From = r.Value;
                        r.Changed = finished;
                        r.ByHmi = w != null;
                        r.WriteTime = w != null ? w.Time : (DateTime?)null;
                        r.WriteUser = w != null ? w.User : "";
                        r.WriteSource = w != null ? w.Source : "";
                    }
                    r.Value = v;
                    r.ReadStart = started;
                    r.Seen = finished;
                }
            }
        }

        /// <summary>One device read once, now.</summary>
        public static void Observe(int station, string device, int value)
        {
            var now = DateTime.Now;
            Observe(station, new[] { device }, new[] { value }, now, now);
        }

        /// <summary>
        /// A write from the HMI that succeeded (PlcWriteLog). <paramref name="value"/>
        /// null when only part of the device was written (a bit of a word).
        /// </summary>
        public static void RecordWrite(int station, string device, int? value, DateTime time, string user, string source)
        {
            if (string.IsNullOrWhiteSpace(device)) return;
            lock (_lock)
            {
                var key = Key(station, device);
                Record r;
                if (!_records.TryGetValue(key, out r)) _records[key] = r = new Record();
                r.Writes.Add(new Write { Time = time, Value = value, User = user ?? "", Source = source ?? "" });
                if (r.Writes.Count > 16) r.Writes.RemoveAt(0);
            }
        }

        /// <summary>What is known of a device, or null when it was never read nor written.</summary>
        public static Info Get(int station, string device)
        {
            if (string.IsNullOrWhiteSpace(device)) return null;
            lock (_lock)
            {
                Record r;
                if (!_records.TryGetValue(Key(station, device), out r)) return null;
                var last = r.Writes.Count > 0 ? r.Writes[r.Writes.Count - 1] : null;
                return new Info
                {
                    Value = r.Value,
                    Seen = r.Value.HasValue ? r.Seen : (DateTime?)null,
                    Changed = r.Changed,
                    From = r.From,
                    ByHmi = r.ByHmi,
                    WriteTime = r.WriteTime,
                    WriteUser = r.WriteUser,
                    WriteSource = r.WriteSource,
                    LastWrite = last != null ? last.Time : (DateTime?)null,
                    LastWriteValue = last != null ? last.Value : null
                };
            }
        }

        /// <summary>The device's value as last read, or null.</summary>
        public static int? Value(int station, string device)
        {
            var i = Get(station, device);
            return i != null ? i.Value : null;
        }
    }
}
