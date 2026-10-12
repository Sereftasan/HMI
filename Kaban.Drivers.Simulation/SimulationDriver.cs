using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Kaban.Core.Interfaces;

namespace Kaban.Drivers.Simulation
{
    /// <summary>
    /// In-memory PLC simulator, usable as a dummy station for ANY station number
    /// (e.g. register one as station 1 and another as station 2 to exercise the
    /// multi-CPU / multi-station paths without hardware).
    ///
    /// Each instance owns its own memory, so SimulationDriver(1) and
    /// SimulationDriver(2) are genuinely independent CPUs - writing D100 on one
    /// does not affect D100 on the other.
    ///
    /// Test features beyond plain storage:
    ///   Seed(...)          preset values so enable-conditions/triggers can be met
    ///   LoadSeedFile(...)  same, from a text file, so no recompile between tests
    ///   AddMirror(...)     make a command bit drive a feedback bit (M10 -> X10),
    ///                      so lamps and conditions react to button presses
    ///   AddRamp(...)       make a word advance over time, so position readouts move
    ///
    /// THREAD SAFETY: the PLC scanner reads from a background timer thread while
    /// the UI writes from the main thread. The previous implementation used a bare
    /// Dictionary, which can corrupt or throw under that access pattern; all state
    /// is now guarded by a lock.
    /// </summary>
    public class SimulationDriver : IPlcDriver, Kaban.Core.Interfaces.INonBlockingDriver
    {
        private readonly int _station;
        private readonly object _gate = new object();
        private readonly Dictionary<string, int> _memory =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // command bit -> feedback bits it should drive (button press lights a lamp)
        private readonly Dictionary<string, List<string>> _mirrors =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // addresses whose value advances with time (moving position displays)
        private class Ramp
        {
            public int Min, Max, StepPerSecond;
            public bool PingPong;
        }
        private readonly Dictionary<string, Ramp> _ramps =
            new Dictionary<string, Ramp>(StringComparer.OrdinalIgnoreCase);
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public SimulationDriver(int station)
        {
            _station = station;
        }

        /// <summary>Station this instance simulates (useful when debugging multi-CPU setups).</summary>
        public int Station { get { return _station; } }

        public bool IsConnected { get; private set; }

        // The simulator never fails a read/write once connected.
        public bool IsHealthy { get { return IsConnected; } }

        public bool Connect()
        {
            IsConnected = true;
            return true;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        // ==================================================================
        // TEST SETUP
        // ==================================================================

        /// <summary>Presets a value (works before or after Connect).</summary>
        public SimulationDriver Seed(string address, int value)
        {
            if (string.IsNullOrWhiteSpace(address)) return this;
            lock (_gate) { _memory[Normalize(address)] = value; }
            return this;
        }

        /// <summary>Presets many values at once.</summary>
        public SimulationDriver Seed(IEnumerable<KeyValuePair<string, int>> values)
        {
            if (values == null) return this;
            lock (_gate)
            {
                foreach (var kv in values)
                    if (!string.IsNullOrWhiteSpace(kv.Key))
                        _memory[Normalize(kv.Key)] = kv.Value;
            }
            return this;
        }

        /// <summary>
        /// Loads seed values from a simple text file so test scenarios can be
        /// changed without recompiling. One entry per line:
        ///     M100 = 0
        ///     X20  = 1      # comments after # or // are ignored
        ///     D200 = 1500
        /// Missing file is ignored (returns 0).
        /// </summary>
        public int LoadSeedFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;
            int n = 0;
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw;
                int cut = line.IndexOf('#');
                if (cut >= 0) line = line.Substring(0, cut);
                cut = line.IndexOf("//", StringComparison.Ordinal);
                if (cut >= 0) line = line.Substring(0, cut);
                line = line.Trim();
                if (line.Length == 0) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string addr = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();

                int v;
                if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) continue;
                Seed(addr, v);
                n++;
            }
            return n;
        }

        /// <summary>
        /// Makes writing <paramref name="commandAddress"/> also drive
        /// <paramref name="feedbackAddress"/>, simulating an actuator reaching its
        /// sensor. Without this, pressing a button changes nothing observable and
        /// lamps/conditions never react.
        /// </summary>
        public SimulationDriver AddMirror(string commandAddress, string feedbackAddress)
        {
            if (string.IsNullOrWhiteSpace(commandAddress) || string.IsNullOrWhiteSpace(feedbackAddress))
                return this;
            lock (_gate)
            {
                string key = Normalize(commandAddress);
                List<string> list;
                if (!_mirrors.TryGetValue(key, out list))
                {
                    list = new List<string>();
                    _mirrors[key] = list;
                }
                string target = Normalize(feedbackAddress);
                if (!list.Contains(target)) list.Add(target);
            }
            return this;
        }

        /// <summary>
        /// Makes an address advance over time so numeric displays visibly move.
        /// Value is derived from elapsed time on READ (no background thread, so it
        /// stays deterministic and race-free).
        /// </summary>
        public SimulationDriver AddRamp(string address, int min, int max, int stepPerSecond, bool pingPong = true)
        {
            if (string.IsNullOrWhiteSpace(address) || max <= min || stepPerSecond == 0) return this;
            lock (_gate)
            {
                _ramps[Normalize(address)] = new Ramp
                {
                    Min = min,
                    Max = max,
                    StepPerSecond = Math.Abs(stepPerSecond),
                    PingPong = pingPong
                };
            }
            return this;
        }

        /// <summary>Clears all memory, mirrors and ramps (test isolation).</summary>
        public void ResetAll()
        {
            lock (_gate)
            {
                _memory.Clear();
                _mirrors.Clear();
                _ramps.Clear();
            }
        }

        /// <summary>Snapshot of current memory, for debugging a test run.</summary>
        public Dictionary<string, int> Dump()
        {
            lock (_gate) { return new Dictionary<string, int>(_memory, StringComparer.OrdinalIgnoreCase); }
        }

        // ==================================================================
        // IPlcDriver
        // ==================================================================

        public int Read(string address)
        {
            int value;
            TryRead(address, out value);
            return value;
        }

        public bool TryRead(string address, out int value)
        {
            value = 0;
            if (!IsConnected) return false;
            if (string.IsNullOrWhiteSpace(address)) return false;

            string key = Normalize(address);
            lock (_gate)
            {
                Ramp ramp;
                if (_ramps.TryGetValue(key, out ramp))
                {
                    value = Evaluate(ramp);
                    return true;
                }
                int stored;
                if (_memory.TryGetValue(key, out stored)) value = stored;
            }
            return true;
        }

        public int[] ReadMulti(string[] addresses)
        {
            int[] values;
            TryReadMulti(addresses, out values);
            return values;
        }

        public bool TryReadMulti(string[] addresses, out int[] values)
        {
            if (addresses == null || addresses.Length == 0)
            {
                values = new int[0];
                return true;
            }

            values = new int[addresses.Length];
            if (!IsConnected) return false;

            bool allOk = true;
            for (int i = 0; i < addresses.Length; i++)
            {
                int v;
                bool ok = TryRead(addresses[i], out v);
                values[i] = v;
                allOk &= ok;
            }
            return allOk;
        }

        public int Write(string address, int value)
        {
            if (!IsConnected) return -1;
            if (string.IsNullOrWhiteSpace(address)) return -1;

            string key = Normalize(address);
            lock (_gate)
            {
                _memory[key] = value;

                // Drive any mirrored feedback bits so the HMI reacts to the write.
                List<string> targets;
                if (_mirrors.TryGetValue(key, out targets))
                    foreach (var t in targets) _memory[t] = value;
            }
            return 0;
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        /// <summary>
        /// Case/whitespace-insensitive address key, so "m100", "M100" and " M100 "
        /// address the same cell. Real drivers treat these the same; the previous
        /// version created separate entries and reads appeared to lose writes.
        /// </summary>
        private static string Normalize(string address)
        {
            return address.Trim().ToUpperInvariant();
        }

        private int Evaluate(Ramp r)
        {
            double seconds = _clock.Elapsed.TotalSeconds;
            int span = r.Max - r.Min;
            long travelled = (long)(seconds * r.StepPerSecond);

            if (!r.PingPong)
                return r.Min + (int)(travelled % (span + 1));

            long cycle = span * 2L;
            long pos = travelled % cycle;
            if (pos > span) pos = cycle - pos;   // coming back down
            return r.Min + (int)pos;
        }
    }
}

