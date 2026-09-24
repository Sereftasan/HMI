using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// The machine's PLC LABEL LIST - the engineer's own names for every device,
    /// loaded from PlcLabels.csv next to the exe.
    ///
    ///     Label;DataType;Address
    ///     MAN_GirisRobotuX_Ileri;Bit;M604
    ///     IN_AcilStop;Bit;M200
    ///
    /// Every address in this file is already an address the HMI can read. Physical
    /// X/Y points are TRANSLATED to the internal relay the PLC copies them into
    /// (its ST interface block) while the file is BUILT - see
    /// tools/build_plc_labels.py. That translation belongs to the build step, not
    /// to the running program: by the time the HMI sees the list there is no such
    /// thing as a physical address, only devices it can bind to.
    ///
    /// It lets everything speak in LABELS: the simulator monitor shows a name
    /// beside each address, admin tooltips can name a device, and the AI prompt can
    /// hand an assistant the real device list so generated pages use true addresses
    /// instead of invented ones.
    /// </summary>
    public static class PlcLabelTable
    {
        public class Entry
        {
            public string Label = "";
            public string DataType = "Bit";
            public string Address = "";

            public bool IsBit
            {
                get { return (DataType ?? "").Trim().StartsWith("Bit", StringComparison.OrdinalIgnoreCase); }
            }

            /// <summary>True for a 32-bit device (a Double Word pair).</summary>
            public bool Is32Bit
            {
                get
                {
                    var t = (DataType ?? "").ToLowerInvariant();
                    return t.Contains("double") || t.Contains("32");
                }
            }

        }

        private static readonly List<Entry> _entries = new List<Entry>();
        private static readonly Dictionary<string, Entry> _byLabel =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Entry> _byAddress =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<Entry> Entries { get { return _entries; } }
        public static int Count { get { return _entries.Count; } }

        /// <summary>Loads (or reloads) the label list. Missing file is not an error.</summary>
        // ---- per station ------------------------------------------------------
        //
        // Load() holds ONE machine's labels, keyed by address alone. With H500 and
        // H800 mounted under FA2010 every machine has its own D318 - and every
        // lookup found FA2010's: H800's double word showed as FA2010's single word.
        // A mounted machine's labels are kept for its own station; a lookup that
        // names the station finds that machine's, and falls back to the default.

        private static readonly Dictionary<int, Dictionary<string, Entry>> _byStation =
            new Dictionary<int, Dictionary<string, Entry>>();

        /// <summary>Loads one station's labels beside the default table, leaving it untouched.</summary>
        public static int LoadStation(string path, int station)
        {
            var entries = new List<Entry>(_entries);
            var byLabel = new Dictionary<string, Entry>(_byLabel, StringComparer.OrdinalIgnoreCase);
            var byAddress = new Dictionary<string, Entry>(_byAddress, StringComparer.OrdinalIgnoreCase);
            int n = Load(path);
            _byStation[station] = new Dictionary<string, Entry>(_byAddress, StringComparer.OrdinalIgnoreCase);
            _entries.Clear(); _entries.AddRange(entries);
            _byLabel.Clear(); foreach (var kv in byLabel) _byLabel[kv.Key] = kv.Value;
            _byAddress.Clear(); foreach (var kv in byAddress) _byAddress[kv.Key] = kv.Value;
            return n;
        }

        /// <summary>The label of a device on a given station (the default table when that station has none).</summary>
        public static Entry ByAddress(int station, string address)
        {
            Dictionary<string, Entry> table;
            Entry e;
            if (station > 0 && _byStation.TryGetValue(station, out table))
                return table.TryGetValue((address ?? "").Trim(), out e) ? e : null;
            return ByAddress(address);
        }

        /// <summary>
        /// A label on a given station - a mounted machine's OWN list, so asking
        /// H500's station for "PC_AlarmReset" cannot answer with FA2010's. A
        /// station without a list of its own uses the default (main machine's).
        /// </summary>
        public static Entry ByLabel(int station, string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return null;
            Dictionary<string, Entry> table;
            if (station > 0 && _byStation.TryGetValue(station, out table))
                return table.Values.FirstOrDefault(e => string.Equals(e.Label, label.Trim(), StringComparison.OrdinalIgnoreCase));
            return ByLabel(label);
        }

        public static int Load(string path)
        {
            _entries.Clear(); _byLabel.Clear(); _byAddress.Clear();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return 0;

            try
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    // tolerate ; or TAB - the list is pasted out of GX Works and Excel
                    var f = line.Split(line.Contains(";") ? ';' : '\t');
                    if (f.Length < 3) continue;
                    if (f[0].Trim().Equals("Label", StringComparison.OrdinalIgnoreCase)) continue;  // header

                    var e = new Entry
                    {
                        Label = f[0].Trim(),
                        DataType = f[1].Trim(),
                        Address = f[2].Trim().ToUpperInvariant()
                    };
                    // A row with no address is a placeholder in the engineer's list
                    // (a device not assigned yet) - keep it visible but unusable.
                    _entries.Add(e);
                    if (e.Label.Length > 0 && !_byLabel.ContainsKey(e.Label)) _byLabel[e.Label] = e;
                    if (e.Address.Length > 0 && !_byAddress.ContainsKey(e.Address)) _byAddress[e.Address] = e;
                }
                AuditLogger.Log("loaded " + _entries.Count + " PLC labels from " + Path.GetFileName(path) + ".");
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not load PLC labels: " + ex.Message);
            }
            return _entries.Count;
        }

        public static Entry ByLabel(string label)
        {
            Entry e;
            return (!string.IsNullOrWhiteSpace(label) && _byLabel.TryGetValue(label.Trim(), out e)) ? e : null;
        }

        public static Entry ByAddress(string address)
        {
            Entry e;
            return (!string.IsNullOrWhiteSpace(address) &&
                    _byAddress.TryGetValue(address.Trim().ToUpperInvariant(), out e)) ? e : null;
        }

        /// <summary>Turns a label into its address; an address passes straight through.</summary>
        public static string ResolveAddress(string labelOrAddress)
        {
            if (string.IsNullOrWhiteSpace(labelOrAddress)) return labelOrAddress;
            var e = ByLabel(labelOrAddress) ?? ByAddress(labelOrAddress);
            return e != null ? e.Address : labelOrAddress.Trim();
        }

        /// <summary>The engineer's name for an address, for tooltips and the monitor.</summary>
        public static string LabelOf(string address)
        {
            var e = ByAddress(address);
            return e != null ? e.Label : "";
        }

        /// <summary>Every entry whose label starts with the given prefix (MAN_, IN_, OUT_...).</summary>
        public static IEnumerable<Entry> WithPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return _entries;
            return _entries.Where(e => e.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
    }
}

