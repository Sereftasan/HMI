using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Kaban.Core.Helpers;
using Kaban.Core.Labels;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// A PLC's request for a label (the old cutting machines' Check_PrintBarcode / CheckLeftOverBarcode): the PLC sets the bit
    /// (the old PrintBarcode L2 / LeftOverBacode L24) with a value beside it (the piece's ID D386 / the leftover's length D388, in
    /// tenths) - the HMI reads the value, turns the bit off and prints: the piece's label (its values from the machine: Resolve)
    /// or the leftover's. Registered by the machine with its devices; each is off until switched on (HMI Settings > Labels), its
    /// devices changeable there.
    /// </summary>
    public sealed class LabelTrigger
    {
        public string ModuleId = "", Name = "";
        public int Station = 1;
        public LabelUse Use = LabelUse.Piece;
        /// <summary>The PLC's request bit and the value it gives with it (a piece's ID, a leftover's length).</summary>
        public string Bit = "", Value = "";
        public bool DoubleWord = true;
        /// <summary>The value read divided by this (a length in tenths: 10).</summary>
        public double Divisor = 1;
        /// <summary>The label's values for the value read (a piece's ID; a leftover's length in mm) - the machine's (FA2010: the bar sent).</summary>
        public Func<double, LabelValues> Resolve;

        // as the machine registered it (the page's "the machine's devices")
        internal string DefaultBit = "", DefaultValue = "";
        internal double DefaultDivisor = 1;
        internal bool On, Armed = true;

        public string Key { get { return ModuleId + "|" + Station.ToString(CultureInfo.InvariantCulture) + "|" + Use; } }
        /// <summary>The devices the machine gave (the page shows them beside the ones set).</summary>
        public string DefaultBitText() { return DefaultBit; }
        public string DefaultValueText()
        {
            return DefaultValue + (DefaultDivisor != 1 ? " / " + DefaultDivisor.ToString("0.###", CultureInfo.InvariantCulture) : "");
        }
        public bool IsOn { get { return On; } }
    }

    /// <summary>The machines' label requests: watched every 250 ms while one is on.</summary>
    public static class LabelTriggers
    {
        /// <summary>Where the switches and devices are kept. Replaceable (tests).</summary>
        public static string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "LabelTriggers.cfg");
        public const int PollMs = 250;
        private static readonly object Gate = new object(), PollGate = new object();
        private static readonly List<LabelTrigger> _all = new List<LabelTrigger>();
        private static System.Threading.Timer _timer;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        public static event EventHandler Changed;
        public static IList<LabelTrigger> All { get { lock (Gate) return _all.ToList(); } }

        /// <summary>A machine's request (its RegisterPlc): its devices as given, switched and changed as the settings say.</summary>
        public static void Register(LabelTrigger t)
        {
            if (t == null) return;
            t.DefaultBit = t.Bit; t.DefaultValue = t.Value; t.DefaultDivisor = t.Divisor;
            lock (Gate)
            {
                _all.RemoveAll(x => x.Key == t.Key);
                _all.Add(t);
                ApplySaved(t);
            }
            if (t.On) Start();
        }

        /// <summary>Every request forgotten (tests).</summary>
        public static void Clear() { lock (Gate) _all.Clear(); }

        /// <summary>A request switched and its devices set (the page); kept for the next start.</summary>
        public static void Set(LabelTrigger t, bool on, string bit, string value, double divisor)
        {
            lock (Gate)
            {
                t.On = on;
                t.Bit = (bit ?? "").Trim().ToUpperInvariant();
                t.Value = (value ?? "").Trim().ToUpperInvariant();
                t.Divisor = divisor > 0 ? divisor : 1;
                t.Armed = true;
                Save();
            }
            AuditLogger.Log("[LABEL] " + Describe(t) + (on ? " on" : " off") + ".");
            if (on) Start();
            var h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }

        public static string Describe(LabelTrigger t)
        {
            return string.Format(CI, "{0} {1} label: {2} with {3}{4}", t.Name.Length > 0 ? t.Name : t.ModuleId, t.Use == LabelUse.Leftover ? "leftover" : "piece",
                                 t.Bit, t.Value, t.Divisor != 1 ? " / " + t.Divisor.ToString("0.###", CI) : "");
        }

        private static void Save()
        {
            var sb = new StringBuilder("# Kaban HMI - the PLC's label requests (HMI Settings > Labels)\r\n");
            foreach (var t in _all)
                sb.Append(string.Join("\t", "trigger", t.Key, t.On ? "on" : "off", t.Bit, t.Value, t.Divisor.ToString("0.####", CI))).Append("\r\n");
            // the ones of machines not mounted now kept as they were
            foreach (var line in SavedLines().Where(l => !_all.Any(t => t.Key == l[1])))
                sb.Append(string.Join("\t", line)).Append("\r\n");
            try { SafeFile.WriteAllText(SettingsPath, sb.ToString()); }
            catch (Exception ex) { AuditLogger.Log("[LABEL] " + SettingsPath + " could not be written: " + ex.Message); }
        }

        private static List<string[]> SavedLines()
        {
            var list = new List<string[]>();
            try
            {
                if (!File.Exists(SettingsPath)) return list;
                foreach (var line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    var p = line.Split('\t');
                    if (p.Length >= 6 && p[0] == "trigger") list.Add(p);
                }
            }
            catch { }
            return list;
        }

        private static void ApplySaved(LabelTrigger t)
        {
            var p = SavedLines().FirstOrDefault(l => l[1] == t.Key);
            if (p == null) return;
            t.On = p[2] == "on";
            if (p[3].Trim().Length > 0) t.Bit = p[3].Trim();
            if (p[4].Trim().Length > 0) t.Value = p[4].Trim();
            double d;
            if (double.TryParse(p[5], NumberStyles.Float, CI, out d) && d > 0) t.Divisor = d;
        }

        // ------------------------------------------------------------------ watching

        public static void Start()
        {
            lock (Gate)
            {
                if (_timer != null) return;
                _timer = new System.Threading.Timer(_ => Tick(), null, 500, PollMs);
            }
        }

        public static void Stop()
        {
            lock (Gate)
            {
                if (_timer != null) _timer.Dispose();
                _timer = null;
            }
        }

        private static void Tick()
        {
            if (!System.Threading.Monitor.TryEnter(PollGate)) return;
            try { PollNow(); }
            finally { System.Threading.Monitor.Exit(PollGate); }
        }

        /// <summary>One look at the requests now (a test calls it).</summary>
        public static void Poll() { lock (PollGate) PollNow(); }

        /// <summary>
        /// The bit is also a record's handshake (a LogRecordHMI of the machine with ResetTrigger - FA2010's L24, the waste profile
        /// emerged, is one): that record turns it off, so the label is printed when IT sees the bit (EdgeSeen), not watched here.
        /// </summary>
        public static bool HandledElsewhere(LabelTrigger t)
        {
            string dev = Norm(t.Station, t.Bit);
            try
            {
                return Kaban.UI.Dashboard.RecordTriggers.All.Any(d => d.Station == t.Station && d.Resettable && d.HasTrigger &&
                                                                     Kaban.UI.Dashboard.RecordTriggers.Taken(d) &&
                                                                     Norm(d.Station, d.Trigger.MonitorAddress) == dev);
            }
            catch { return false; }
        }

        private static string Norm(int station, string address)
        {
            var a = (address ?? "").Trim();
            if (a.Length == 0) return "";
            return PlcAddressHelper.StationOf(station, a).ToString(CI) + ":" + PlcAddressHelper.DeviceOf(a).ToUpperInvariant();
        }

        private static void PollNow()
        {
            foreach (var t in All)
            {
                if (!t.On || t.Bit.Length == 0 || HandledElsewhere(t)) continue;
                try
                {
                    // the devices on the bit's PLC ("L2@2": PLC 2's)
                    int st = PlcAddressHelper.StationOf(t.Station, t.Bit);
                    string bit = PlcAddressHelper.DeviceOf(t.Bit), val = PlcAddressHelper.DeviceOf(t.Value);
                    var driver = PlcManager.Instance.GetDriverForWrite(st);
                    if (driver == null || !driver.IsConnected) continue;
                    var devices = new List<string> { bit };
                    if (val.Length > 0) { devices.Add(val); if (t.DoubleWord) devices.Add(PlcAddressHelper.OffsetAddress(val, 1)); }
                    int[] v;
                    if (!driver.TryReadMulti(devices.ToArray(), out v) || v == null || v.Length < devices.Count) continue;
                    if (v[0] == 0) { t.Armed = true; continue; }
                    if (!t.Armed) continue;          // printed, the bit not seen off since (its reset did not take yet)
                    t.Armed = false;
                    // the old order: the value read, the bit turned off (the PLC goes on), then the label
                    int rc;
                    using (PlcWriteLog.Source("Labels: the " + (t.Use == LabelUse.Leftover ? "leftover" : "piece") + " label taken, its request turned off"))
                        rc = driver.Write(bit, 0);
                    if (rc != 0) AuditLogger.Log("[LABEL] " + Describe(t) + ": " + t.Bit + " could not be turned off (code " + rc + ").");
                    Handle(t, ValueOf(v, 1, t));
                }
                catch (Exception ex) { AuditLogger.Log("[LABEL] " + Describe(t) + ": " + ex.Message); }
            }
        }

        private static double ValueOf(int[] v, int i, LabelTrigger t)
        {
            if (t.Value.Length == 0 || v.Length <= i) return 0;
            long n = t.DoubleWord && v.Length > i + 1 ? (int)((v[i] & 0xFFFF) | ((v[i + 1] & 0xFFFF) << 16)) : (short)v[i];
            return n / (t.Divisor > 0 ? t.Divisor : 1);
        }

        /// <summary>A record's handshake saw the bit come (RecordTriggers): the label of a request on that bit printed.</summary>
        public static void EdgeSeen(int station, string address)
        {
            string dev = Norm(station, address);
            foreach (var t in All)
            {
                if (!t.On || Norm(t.Station, t.Bit) != dev) continue;
                try
                {
                    double value = 0;
                    if (t.Value.Length > 0)
                    {
                        var driver = PlcManager.Instance.GetDriverForWrite(PlcAddressHelper.StationOf(t.Station, t.Bit));
                        string val = PlcAddressHelper.DeviceOf(t.Value);
                        int[] v;
                        var devices = t.DoubleWord ? new[] { val, PlcAddressHelper.OffsetAddress(val, 1) } : new[] { val };
                        if (driver != null && driver.IsConnected && driver.TryReadMulti(devices, out v) && v != null && v.Length >= devices.Length)
                            value = ValueOf(new[] { 0 }.Concat(v).ToArray(), 1, t);
                    }
                    Handle(t, value);
                }
                catch (Exception ex) { AuditLogger.Log("[LABEL] " + Describe(t) + ": " + ex.Message); }
            }
        }

        /// <summary>The label of the value read printed (the request's machine's labels on for it).</summary>
        internal static void Handle(LabelTrigger t, double value)
        {
            LabelValues values = null;
            try { if (t.Resolve != null) values = t.Resolve(value); }
            catch (Exception ex) { AuditLogger.Log("[LABEL] " + Describe(t) + ": the label's values: " + ex.Message); }
            if (values == null) values = new LabelValues();
            string source;
            if (t.Use == LabelUse.Leftover)
            {
                values[LabelKeys.LeftOverLength] = value.ToString("0.#", CI);
                values[LabelKeys.LeftOverBarcode] = LabelKeys.LeftoverCode(values[LabelKeys.ProfileCode], value);
                source = string.Format(CI, "{0}: leftover {1} mm ({2})", t.Name.Length > 0 ? t.Name : t.ModuleId, value.ToString("0.#", CI), t.Bit);
            }
            else
            {
                if (values[LabelKeys.LabelNo].Length == 0) values[LabelKeys.LabelNo] = value.ToString("0", CI);
                source = string.Format(CI, "{0}: piece {1} ({2}){3}", t.Name.Length > 0 ? t.Name : t.ModuleId, value.ToString("0", CI), t.Bit,
                                       values[LabelKeys.ProfileCode].Length == 0 ? " - not a piece of a bar sent from this HMI" : "");
            }
            LabelService.Print(t.Use, t.ModuleId, values, source);
        }
    }
}
