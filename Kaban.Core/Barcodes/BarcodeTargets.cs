using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;

namespace Kaban.Core.Barcodes
{
    /// <summary>
    /// A MACHINE THAT TAKES A PROFILE'S SIZE FROM A CODE (the old MK2010's and MD3030's FillByBarcode / FeedBarcode): a code read with
    /// a width and a height in it writes them into the machine's job values - MK2010's Width X / Width Y (D1200 / D1202, x10, two words
    /// each), MD3020's Frame Width (X) / Frame Depth (Y) (the same devices). Registered by the machine (its RegisterPlc).
    /// </summary>
    public sealed class BarcodeTarget
    {
        public string ModuleId = "", Name = "";
        public int Station = 1;
        public string WidthAddress = "", HeightAddress = "";
        /// <summary>The value the PLC keeps is the size times this (tenths of a mm: 10).</summary>
        public double Scale = 10;
        /// <summary>The value in two words (a 32-bit value, the low word first) - the old Int2Short.</summary>
        public bool DoubleWord = true;
        /// <summary>The wider side always the width (MK2010: "if (X &lt; Y) swap").</summary>
        public bool WiderFirst;
        /// <summary>While this bit is on the job values may not change (MK2010's M26 - its job running): the code is refused.</summary>
        public string BusyBit = "";
    }

    /// <summary>The machines' targets for a profile's size read from a code: the newest machine that can take it does.</summary>
    public static class BarcodeTargets
    {
        private static readonly object Gate = new object();
        private static readonly List<BarcodeTarget> _all = new List<BarcodeTarget>();
        private static readonly Dictionary<BarcodeTarget, IDisposable> _taking = new Dictionary<BarcodeTarget, IDisposable>();
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>What a target did with a code: the text shown to the operator, whether it was a warning.</summary>
        public static event Action<BarcodeTarget, BarcodeScan, string, bool> Done;

        public static IList<BarcodeTarget> All { get { lock (Gate) return _all.ToList(); } }

        public static void Register(BarcodeTarget t)
        {
            if (t == null) return;
            lock (Gate)
            {
                _all.RemoveAll(x => x.ModuleId == t.ModuleId && x.Station == t.Station);
                _all.Add(t);
                IDisposable old;
                if (_taking.TryGetValue(t, out old)) old.Dispose();
                _taking[t] = BarcodeReaders.Take((t.Name.Length > 0 ? t.Name : t.ModuleId) + " job size", scan => Take(t, scan));
            }
        }

        /// <summary>Every target forgotten (tests).</summary>
        public static void Clear()
        {
            lock (Gate)
            {
                foreach (var d in _taking.Values) d.Dispose();
                _taking.Clear();
                _all.Clear();
            }
        }

        /// <summary>Writes the code's width and height (its own reader's machine only); false - not this machine's code.</summary>
        public static bool Take(BarcodeTarget t, BarcodeScan scan)
        {
            if (t == null || scan == null) return false;
            if (scan.Machine.Length > 0 && !string.Equals(scan.Machine, t.ModuleId, StringComparison.OrdinalIgnoreCase)) return false;
            if (!scan.Width.HasValue && !scan.Height.HasValue) return false;
            string who = t.Name.Length > 0 ? t.Name : t.ModuleId;
            if (!scan.Width.HasValue || !scan.Height.HasValue || scan.Width.Value <= 0 || scan.Height.Value <= 0)
            {
                Tell(t, scan, string.Format(LanguageManager.GetString("Bc_SizeMissing", "{0}: the code {1} has no width and height (HMI Settings > Barcodes: the parts of a code)."), who, scan.Code), true);
                return true;
            }
            double w = scan.Width.Value, h = scan.Height.Value;
            if (t.WiderFirst && w < h) { var x = w; w = h; h = x; }
            var driver = PlcManager.Instance.GetDriverForWrite(t.Station);
            if (driver == null || !driver.IsConnected)
            {
                Tell(t, scan, string.Format(LanguageManager.GetString("Bc_NotConnected", "{0}: the PLC is not connected - {1} not written."), who, scan.Code), true);
                return true;
            }
            if (!PlcManager.Instance.CheckPermission(t.Station))
            {
                Tell(t, scan, string.Format(LanguageManager.GetString("Bc_NoRight", "{0}: your user level may not write to this PLC - {1} not written."), who, scan.Code), true);
                return true;
            }
            try
            {
                if (t.BusyBit.Length > 0)
                {
                    int[] v;
                    if (driver.TryReadMulti(new[] { t.BusyBit }, out v) && v != null && v.Length > 0 && v[0] != 0)
                    {
                        Tell(t, scan, string.Format(LanguageManager.GetString("Bc_Busy", "{0}: a job is running ({1} on) - the size of {2} is not taken now."), who, t.BusyBit, scan.Code), true);
                        return true;
                    }
                }
                int rc;
                using (PlcWriteLog.Source("Barcode " + scan.Code + (scan.Source.Length > 0 ? " (" + scan.Source + ")" : "")))
                {
                    rc = WriteValue(driver, t.WidthAddress, w * t.Scale, t.DoubleWord);
                    if (rc == 0) rc = WriteValue(driver, t.HeightAddress, h * t.Scale, t.DoubleWord);
                }
                if (rc != 0)
                {
                    Tell(t, scan, string.Format(LanguageManager.GetString("Bc_Refused", "{0}: the PLC refused the size of {1} (code {2})."), who, scan.Code, rc), true);
                    return true;
                }
                Tell(t, scan, string.Format(LanguageManager.GetString("Bc_SizeTaken", "{0}: {1} - width {2} mm, height {3} mm."), who,
                                            scan.ProfileCode.Length > 0 ? scan.ProfileCode : scan.Code, w.ToString("0.#", CI), h.ToString("0.#", CI)), false);
            }
            catch (Exception ex)
            {
                Tell(t, scan, who + ": " + scan.Code + " - " + ex.Message, true);
            }
            return true;
        }

        /// <summary>A value into one word or two (the low word first).</summary>
        internal static int WriteValue(IPlcDriver driver, string address, double value, bool doubleWord)
        {
            long n = (long)Math.Round(value);
            if (!doubleWord) return driver.Write(address, (short)n);
            int rc = driver.Write(address, (short)(n & 0xFFFF));
            if (rc != 0) return rc;
            return driver.Write(PlcAddressHelper.OffsetAddress(address, 1), (short)((n >> 16) & 0xFFFF));
        }

        private static void Tell(BarcodeTarget t, BarcodeScan scan, string text, bool warning)
        {
            AuditLogger.Log("[BARCODE] " + text);
            var d = Done;
            if (d != null) try { d(t, scan, text, warning); } catch { }
        }
    }
}
