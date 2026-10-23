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
