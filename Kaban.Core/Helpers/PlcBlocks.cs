using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Interfaces;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// A RUN OF DEVICES DEVICE BY DEVICE, for a driver without block calls of its own (22 October):
    /// read in batches of single devices (TryReadMulti), a bit device's 16 bits packed into each
    /// word as a block read gives them; written one device at a time.
    /// </summary>
    public static class PlcBlocks
    {
        /// <summary>Devices asked for in one batch.</summary>
        private const int Batch = 120;

        /// <summary>The devices of a run: a word device's <paramref name="count"/>, a bit device's 16 x as many, a 32-bit one's half.</summary>
        public static List<string> Devices(string first, int count, out bool bits)
        {
            bits = PlcAddressHelper.IsBitDevice(first);
            string prefix; long start;
            var list = new List<string>();
            if (!PlcAddressHelper.TrySplit(first, out prefix, out start)) return list;
            long n = bits ? 16L * count : PlcAddressHelper.IsDoubleWordDevice(first) ? (count + 1) / 2 : count;
            for (long i = 0; i < n; i++) list.Add(PlcAddressHelper.Compose(prefix, start + i));
            return list;
        }

        public static bool ReadByDevice(IPlcDriver driver, string first, int count, out short[] values)
        {
            values = null;
            bool bits;
            var devices = Devices(first, count, out bits);
            if (driver == null || devices.Count == 0 && count > 0) return false;
            var read = new int[devices.Count];
            for (int at = 0; at < devices.Count; at += Batch)
            {
                var batch = devices.Skip(at).Take(Batch).ToArray();
                int[] got;
                if (!driver.TryReadMulti(batch, out got) || got == null || got.Length != batch.Length) return false;
                Array.Copy(got, 0, read, at, got.Length);
            }
            values = new short[count];
            bool dbl = PlcAddressHelper.IsDoubleWordDevice(first);
            for (int i = 0; i < count; i++)
            {
                if (dbl) { values[i] = (short)(i % 2 == 0 ? read[i / 2] & 0xFFFF : (read[i / 2] >> 16) & 0xFFFF); continue; }
                if (!bits) { values[i] = (short)read[i]; continue; }
                int w = 0;
                for (int b = 0; b < 16; b++) if (read[16 * i + b] != 0) w |= 1 << b;
                values[i] = (short)w;
            }
            return true;
        }

        /// <summary>
        /// MANY DEVICES IN FEW CALLS (6 November: the PLC Monitor's job list and milling tables - 500 double words each - read with
        /// every refresh): the runs of a word device (D, R, ZR ... decimal), <paramref name="minRun"/> devices or more with at most
        /// <paramref name="maxGap"/> between them, read as blocks (<paramref name="maxBlock"/> words a call at most); the rest - and a
        /// block the controller refuses - in the random read as before. A random read of thousands of devices took several calls
        /// of 128 and seconds on GX Simulator; a block of 960 words is one call.
        /// </summary>
        public static bool ReadMany(IPlcDriver driver, string[] addresses, out int[] values, int minRun = 32, int maxGap = 4, int maxBlock = 960)
        {
            values = new int[addresses == null ? 0 : addresses.Length];
            if (driver == null || addresses == null) return false;
            var block = driver as IPlcBlockAccess;
            if (block == null || addresses.Length < minRun) return driver.TryReadMulti(addresses, out values);
            // the word devices by their letters: number -> the places it is asked at
            var byPrefix = new Dictionary<string, SortedDictionary<long, List<int>>>(StringComparer.OrdinalIgnoreCase);
            var rest = new List<int>();
            for (int i = 0; i < addresses.Length; i++)
            {
                string prefix; long n;
                string a = addresses[i] ?? "";
                if (a.Contains(".") || a.Contains("@") || !PlcAddressHelper.TrySplit(a, out prefix, out n) || PlcAddressHelper.IsBitDevice(a) ||
                    PlcAddressHelper.IsDoubleWordDevice(a) || prefix.EndsWith("\\G") || PlcAddressHelper.BaseOf(prefix) != 10)
                { rest.Add(i); continue; }
                SortedDictionary<long, List<int>> nums;
                if (!byPrefix.TryGetValue(prefix, out nums)) byPrefix[prefix] = nums = new SortedDictionary<long, List<int>>();
                List<int> at;
                if (!nums.TryGetValue(n, out at)) nums[n] = at = new List<int>();
                at.Add(i);
            }
            var result = values;
            bool ok = true;
            foreach (var kv in byPrefix)
            {
                var nums = kv.Value.Keys.ToList();
                int s = 0;
                while (s < nums.Count)
                {
                    int e = s;
                    while (e + 1 < nums.Count && nums[e + 1] - nums[e] <= maxGap + 1 && nums[e + 1] - nums[s] < maxBlock) e++;
                    long first = nums[s], span = nums[e] - first + 1;
                    bool read = false;
                    if (e - s + 1 >= minRun)
                    {
                        short[] w;
                        bool got;
                        try { got = block.TryReadBlock(PlcAddressHelper.Compose(kv.Key, first), (int)span, out w); } catch { got = false; w = null; }
                        if (got && w != null && w.Length >= span)
                        {
                            for (int k = s; k <= e; k++)
                                foreach (int i in kv.Value[nums[k]]) result[i] = w[nums[k] - first];
                            read = true;
                        }
                    }
                    if (!read) for (int k = s; k <= e; k++) rest.AddRange(kv.Value[nums[k]]);
                    s = e + 1;
                }
            }
            if (rest.Count > 0)
            {
                int[] got;
                bool r = driver.TryReadMulti(rest.Select(i => addresses[i]).ToArray(), out got);
                if (got != null) for (int k = 0; k < rest.Count && k < got.Length; k++) result[rest[k]] = got[k];
                ok &= r;
            }
            return ok;
        }

        public static int WriteByDevice(IPlcDriver driver, string first, short[] values)
        {
            if (driver == null || values == null) return -1;
            bool bits;
            var devices = Devices(first, values.Length, out bits);
            if (devices.Count == 0 && values.Length > 0) return -1;
            bool dbl = PlcAddressHelper.IsDoubleWordDevice(first);
            for (int i = 0; i < devices.Count; i++)
            {
                int v = bits ? (values[i / 16] >> (i % 16)) & 1
                      : dbl ? (ushort)values[2 * i] | ((2 * i + 1 < values.Length ? values[2 * i + 1] : 0) << 16)
                      : values[i];
                int rc = driver.Write(devices[i], v);
                if (rc != 0) return rc;
            }
            return 0;
        }
    }
}
