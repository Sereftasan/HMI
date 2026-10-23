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
    public class SimulationDriver : IPlcDriver, Kaban.Core.Interfaces.INonBlockingDriver, Kaban.Core.Interfaces.IPlcBlockAccess
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
                _cams.Clear();
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

                // A SIMPLE MOTION MODULE'S FLASH ROM WRITE (22 October): "[Cd.1] Flash ROM write
                // request" (Un\G5900 = 1) is answered as the module does - at once here: the request
                // back to 0 and "[Md.19] Number of write accesses to flash ROM" (G4224) one more.
                // Only while "[Cd.190] PLC READY signal" (G5950) is off; on, the request stays.
                var flash = FlashRequest.Match(key);
                if (flash.Success && value == 1)
                {
                    int ready;
                    _memory.TryGetValue(flash.Groups[1].Value + "5950", out ready);
                    if ((ready & 1) == 0)
                    {
                        _memory[key] = 0;
                        int count;
                        _memory.TryGetValue(flash.Groups[1].Value + "4224", out count);
                        _memory[flash.Groups[1].Value + "4224"] = count + 1;
                    }
                }

                // A SIMPLE MOTION MODULE'S CAM DATA OPERATION (23 October): "[Cd.600] Cam data operation
                // request" (Un\G45000) = 1 reads a cam into [Cd.604] - [Cd.607], 2 or 3 writes one from
                // them - done at once here, the request back to 0; a cam the module does not have answered
                // as the module does: warning 0C41H on axis 1 ([Md.24], G2407).
                var cam = CamRequest.Match(key);
                if (cam.Success && value >= 1 && value <= 3) CamOperation(cam.Groups[1].Value, value);

                // Drive any mirrored feedback bits so the HMI reacts to the write.
                List<string> targets;
                if (_mirrors.TryGetValue(key, out targets))
                    foreach (var t in targets) _memory[t] = value;
            }
            return 0;
        }

        // ==================================================================
        // A RUN OF DEVICES (22 October: the PLC Connection page's Read and Write)
        // ==================================================================

        /// <summary>The simulator answers any device: a backup of it takes the CPU's default sizes.</summary>
        public bool RefusesOutOfRange { get { return false; } }

        /// <summary>Words from <paramref name="first"/>: a bit device's 16 bits packed into each, the first the lowest.</summary>
        public bool TryReadBlock(string first, int count, out short[] values)
        {
            values = null;
            string prefix; long start;
            if (!IsConnected || count < 0 || !Kaban.Core.Helpers.PlcAddressHelper.TrySplit(first, out prefix, out start)) return false;
            bool bits = Kaban.Core.Helpers.PlcAddressHelper.IsBitDevice(first);
            bool dbl = Kaban.Core.Helpers.PlcAddressHelper.IsDoubleWordDevice(first);
            var words = new short[count];
            lock (_gate)
            {
                for (int i = 0; i < count; i++)
                {
                    // a 32-bit device (LCN): its two words, the low one first
                    if (dbl) { int v = Stored(Kaban.Core.Helpers.PlcAddressHelper.Compose(prefix, start + i / 2)); words[i] = (short)(i % 2 == 0 ? v & 0xFFFF : (v >> 16) & 0xFFFF); continue; }
                    if (!bits) { words[i] = (short)Stored(Kaban.Core.Helpers.PlcAddressHelper.Compose(prefix, start + i)); continue; }
                    int w = 0;
                    for (int b = 0; b < 16; b++)
                        if (Stored(Kaban.Core.Helpers.PlcAddressHelper.Compose(prefix, start + 16L * i + b)) != 0) w |= 1 << b;
                    words[i] = (short)w;
                }
            }
            values = words;
            return true;
        }

        public int WriteBlock(string first, short[] values)
        {
            string prefix; long start;
            if (!IsConnected || values == null || !Kaban.Core.Helpers.PlcAddressHelper.TrySplit(first, out prefix, out start)) return -1;
            bool bits = Kaban.Core.Helpers.PlcAddressHelper.IsBitDevice(first);
            bool dbl = Kaban.Core.Helpers.PlcAddressHelper.IsDoubleWordDevice(first);
            lock (_gate)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (dbl)
                    {
                        if (i % 2 == 1) continue;
                        int high = i + 1 < values.Length ? values[i + 1] : 0;
                        _memory[Kaban.Core.Helpers.PlcAddressHelper.Compose(prefix, start + i / 2)] = (ushort)values[i] | (high << 16);
                        continue;
                    }
                    if (!bits) { _memory[Kaban.Core.Helpers.PlcAddressHelper.Compose(prefix, start + i)] = values[i]; continue; }
                    for (int b = 0; b < 16; b++)
                        _memory[Kaban.Core.Helpers.PlcAddressHelper.Compose(prefix, start + 16L * i + b)] = (values[i] >> b) & 1;
                }
            }
            return 0;
        }

        /// <summary>A device's value as stored (a ramp's as it reads now); 0 for one never written. Under the lock.</summary>
        private int Stored(string key)
        {
            Ramp ramp;
            if (_ramps.TryGetValue(key, out ramp)) return Evaluate(ramp);
            int v;
            return _memory.TryGetValue(key, out v) ? v : 0;
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        /// <summary>
        /// Case/whitespace-insensitive address key, so "m100", "M100" and " M100 "
        /// address the same cell. Real drivers treat these the same; the previous
        /// version created separate entries and reads appeared to lose writes.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex FlashRequest =
            new System.Text.RegularExpressions.Regex(@"^(U[0-9A-F]+\\G)5900$");
        private static readonly System.Text.RegularExpressions.Regex CamRequest =
            new System.Text.RegularExpressions.Regex(@"^(U[0-9A-F]+\\G)45000$");

        /// <summary>A module's cam as the simulator keeps it: its format, resolution / coordinate number, starting point, words.</summary>
        private sealed class CamData { public int Format, Resolution, Start; public int[] Words = new int[0]; }
        private readonly Dictionary<string, CamData> _cams = new Dictionary<string, CamData>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The cams the simulator holds, by module ("U1\G") and number - for the tests.</summary>
        public int CamCount { get { lock (_gate) return _cams.Count; } }

        /// <summary>The module's cam data operation, under the lock: 1 read, 2 / 3 write ([Cd.601] - [Cd.607], 45001 - 53199).</summary>
        private void CamOperation(string module, int request)
        {
            Func<int, int> word = a => { int v; _memory.TryGetValue(module + a.ToString(System.Globalization.CultureInfo.InvariantCulture), out v); return v; };
            Action<int, int> set = (a, v) => _memory[module + a.ToString(System.Globalization.CultureInfo.InvariantCulture)] = v;
            int no = word(45001), first = word(45002), points = word(45003);
            string id = module + "#" + no;
            CamData c;
            if (request == 1)
            {
                if (no < 1 || no > 256 || !_cams.TryGetValue(id, out c)) { set(2407, 0x0C41); set(45000, 0); return; }
                set(45004, c.Format); set(45005, c.Resolution); set(45006, c.Start);
                bool stroke = c.Format == 1;
                int wpp = stroke ? 2 : 4, at = stroke ? first - 1 : first;
                for (int i = 0; i < points * wpp && at * wpp + i < c.Words.Length; i++) set(45008 + i, c.Words[at * wpp + i]);
            }
            else
            {
                int format = word(45004), resolution = word(45005);
                if (format != 1 && format != 2) { set(2407, 0x0C44); set(45000, 0); return; }
                bool stroke = format == 1;
                int wpp = stroke ? 2 : 4, at = stroke ? first - 1 : first;
                if (!_cams.TryGetValue(id, out c) || c.Format != format || c.Resolution != resolution)
                    _cams[id] = c = new CamData { Format = format, Resolution = resolution, Words = new int[resolution * wpp] };
                c.Start = word(45006);
                for (int i = 0; i < points * wpp && at * wpp + i < c.Words.Length; i++) c.Words[at * wpp + i] = word(45008 + i);
            }
            set(45000, 0);
        }

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

