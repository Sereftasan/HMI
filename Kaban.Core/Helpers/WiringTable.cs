using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// WHERE A POINT IS ON THE MACHINE'S DRAWINGS - loaded from each machine's
    /// Wiring.csv (tools/build_wiring.py transcribes the electrical and
    /// pneumatic drawings into it).
    ///
    ///     Point;Read;Commands;Tag;Wire;Sheet;Relay;RelaySheet;Coil;CoilWire;CoilSheet;Valve;Cylinder;Hoses;Hose;PnSheet;Text;Plc;Card;Via;Measure;Pressure
    ///     X34;M212;;SN46;119;12.6;;;;;;;;;;Input Conveyor 6. Clamping Cylinder Back Sensor;1
    ///     Y12;M362;M688;;210;18.6;K13;18.6;Y.7;308;25.5;Y20/Y21 (...);P12 ...;16 (A) / 96 (B);2 E3;...;1
    ///
    /// A movement that failed is looked for on paper: the sensor that did not
    /// come on, the valve that did not switch. The help (TroubleshootingCatalog.
    /// MovementBox) and the Inputs / Outputs page say where each is - the sensor's
    /// tag and wire, the output's relay and coil with their sheets, the valve,
    /// hoses and cylinder on the pneumatic drawing.
    ///
    /// A point is found by its PLC address (X34, Y12), by the relay the HMI reads
    /// it at (M212, M362) - which is what the screens are bound to - and, for an
    /// output, by the manual command bit that drives it (M688).
    /// </summary>
    public static class WiringTable
    {
        public sealed class Point
        {
            public int Station = 1;
            /// <summary>The PLC address: X34, Y12.</summary>
            public string Address = "";
            /// <summary>The relay the HMI reads it at (M212), or "".</summary>
            public string Read = "";
            public readonly List<string> Commands = new List<string>();
            /// <summary>An input's device on the drawing: SN46, SW3, SB5.</summary>
            public string Tag = "";
            /// <summary>The wire number at the PLC terminal.</summary>
            public string Wire = "";
            /// <summary>"12.6": page 12, column 6 of the electrical drawing - the PLC terminal.</summary>
            public string Sheet = "";
            public string Relay = "", RelaySheet = "";
            /// <summary>What the relay switches: a valve solenoid (Y.7), a contactor (K3).</summary>
            public string Coil = "", CoilWire = "", CoilSheet = "";
            /// <summary>The pneumatic valve, as the pneumatic drawing names it, with its type.</summary>
            public string Valve = "";
            public string Cylinder = "", Hoses = "";
            /// <summary>The hose this output puts air into ("96"), or "" - the step-by-step drawings put its coil and movement on that side.</summary>
            public string Hose = "";
            /// <summary>"2 E3": page 2, zone E3 of the pneumatic drawing.</summary>
            public string PnSheet = "";
            /// <summary>The drawing's own words for it.</summary>
            public string Text = "";
            /// <summary>The PLC card and its own terminal name: "U11 FX5-16EYT-ESS, terminal Y2".</summary>
            public string Card = "";
            /// <summary>
            /// A sensor the drawing wires to a module, not to an X input ("DI1 of the
            /// 2nd simple motion module"): the point is then the relay the program
            /// copies it into (M345).
            /// </summary>
            public string Via = "";
            /// <summary>An output's cylinder: how its position is measured (an encoder, an analogue sensor).</summary>
            public string Measure = "";
            /// <summary>An output's cylinder: its clamping pressure selection and pressure switches.</summary>
            public string Pressure = "";

            /// <summary>A text column in another language - "Measure.TR" - as the file gives it.</summary>
            public readonly Dictionary<string, string> Translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>A column in a language ("Measure", "TR"): its own column when the file has one, else "".</summary>
            public string Localized(string column, string language)
            {
                string v;
                return Translations.TryGetValue(column + "." + language, out v) ? v : "";
            }

            public bool IsInput { get { return Address.StartsWith("X", StringComparison.OrdinalIgnoreCase) || Via.Length > 0; } }


            /// <summary>
            /// The electrical line: "X34 - sensor SN46, wire 119, sheet 12.6" or
            /// "Y12 - wire 210 - relay K13 (sheet 18.6) - coil Y.7, wire 308 (sheet 25.5)".
            /// </summary>
            public string Electrical()
            {
                var parts = new List<string>();
                if (IsInput)
                {
                    var p = new List<string>();
                    if (Tag.Length > 0) p.Add((Tag.StartsWith("S", StringComparison.OrdinalIgnoreCase) && !Tag.StartsWith("SB", StringComparison.OrdinalIgnoreCase)
                                               ? "sensor " : "") + Tag);
                    if (Wire.Length > 0) p.Add("wire " + Wire);
                    if (Sheet.Length > 0) p.Add("sheet " + Sheet);
                    if (Via.Length > 0) p.Add("read through " + Via);
                    parts.Add(Address + (p.Count > 0 ? " - " + string.Join(", ", p) : ""));
                }
                else
                {
                    parts.Add(Address + (Wire.Length > 0 ? " - wire " + Wire : "") +
                              (Relay.Length == 0 && Sheet.Length > 0 ? " (sheet " + Sheet + ")" : ""));
                    if (Relay.Length > 0) parts.Add("relay " + Relay + (RelaySheet.Length > 0 ? " (sheet " + RelaySheet + ")" : ""));
                    if (Coil.Length > 0)
                        parts.Add((Coil.StartsWith("Y", StringComparison.OrdinalIgnoreCase) ? "coil " : "") + Coil +
                                  (CoilWire.Length > 0 ? ", wire " + CoilWire : "") +
                                  (CoilSheet.Length > 0 ? " (sheet " + CoilSheet + ")" : ""));
                    else if (CoilSheet.Length > 0 && Relay.Length == 0)
                        parts.Add("continues on sheet " + CoilSheet);
                }
                return string.Join(" - ", parts);
            }

            /// <summary>The air line: "valve Y20/Y21 (...), hoses 16 (A) / 96 (B), P12 ... - pneumatic 2 E3". Empty for none.</summary>
            public string Pneumatic()
            {
                if (Valve.Length == 0 && Cylinder.Length == 0) return "";
                var parts = new List<string>();
                if (Valve.Length > 0) parts.Add("valve " + Valve);
                if (Hoses.Length > 0) parts.Add((Hoses.Contains("/") ? "hoses " : "hose ") + Hoses);
                if (Cylinder.Length > 0) parts.Add(Cylinder);
                return string.Join(", ", parts) + (PnSheet.Length > 0 ? " - pneumatic sheet " + PnSheet : "");
            }
        }

        /// <summary>
        /// The supply wires of a machine's drawing, from its Wiring.csv's directive
        /// lines ("#! ZeroVoltWire=21", "#! SensorSupplyWire=20"): what a voltage
        /// is measured against. "" when the file does not say.
        /// </summary>
        public sealed class Supply
        {
            public string ZeroVoltWire = "";
            public string SensorSupplyWire = "";
        }

        private static readonly Dictionary<int, Supply> _supply = new Dictionary<int, Supply>();

        /// <summary>A station's supply wires (never null).</summary>
        public static Supply SupplyOf(int station)
        {
            EnsureLoaded();
            lock (_lock)
            {
                Supply s;
                return _supply.TryGetValue(station, out s) ? s : new Supply();
            }
        }

        private static readonly Dictionary<int, Dictionary<string, Point>> _byAddress = new Dictionary<int, Dictionary<string, Point>>();
        private static readonly Dictionary<int, Dictionary<string, Point>> _byCommand = new Dictionary<int, Dictionary<string, Point>>();
        private static readonly object _lock = new object();
        private static bool _loaded;

        /// <summary>The number of points known, all stations.</summary>
        public static int Count
        {
            get { lock (_lock) return _byAddress.Values.SelectMany(d => d.Values).Distinct().Count(); }
        }

        /// <summary>Forgets every table (for a test).</summary>
        public static void Clear()
        {
            lock (_lock) { _byAddress.Clear(); _byCommand.Clear(); _supply.Clear(); _loaded = false; }
        }

        /// <summary>
        /// Reads one machine's Wiring.csv. <paramref name="station"/> turns the
        /// file's own PLC number (1 for the machine's first controller) into the
        /// HMI's station. A missing or unreadable file adds nothing.
        /// </summary>
        public static int Load(string path, Func<int, int> station)
        {
            int n = 0;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;
                string[] head = null;
                var supply = new Supply();
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    // "#! ZeroVoltWire=21": a directive, not a comment.
                    if (line.StartsWith("#!", System.StringComparison.Ordinal))
                    {
                        var kv = line.Substring(2).Split(new[] { '=' }, 2);
                        if (kv.Length == 2)
                        {
                            var key = kv[0].Trim();
                            if (key.Equals("ZeroVoltWire", StringComparison.OrdinalIgnoreCase)) supply.ZeroVoltWire = kv[1].Trim();
                            else if (key.Equals("SensorSupplyWire", StringComparison.OrdinalIgnoreCase)) supply.SensorSupplyWire = kv[1].Trim();
                        }
                        continue;
                    }
                    if (line.Length == 0 || line.StartsWith("#", System.StringComparison.Ordinal)) continue;
                    var f = line.Split(';');
                    if (head == null && f[0].Trim().Equals("Point", StringComparison.OrdinalIgnoreCase)) { head = f.Select(h => h.Trim()).ToArray(); continue; }
                    if (head == null) continue;
                    Func<string, string> get = name =>
                    {
                        int i = Array.FindIndex(head, h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
                        return i >= 0 && i < f.Length ? f[i].Trim() : "";
                    };
                    int plc;
                    if (!int.TryParse(get("Plc"), out plc) || plc < 1) plc = 1;
                    var p = new Point
                    {
                        Station = station != null ? station(plc) : plc,
                        Address = get("Point").ToUpperInvariant(),
                        Read = get("Read").ToUpperInvariant(),
                        Tag = get("Tag"), Wire = get("Wire"), Sheet = get("Sheet"),
                        Relay = get("Relay"), RelaySheet = get("RelaySheet"),
                        Coil = get("Coil"), CoilWire = get("CoilWire"), CoilSheet = get("CoilSheet"),
                        Valve = get("Valve"), Cylinder = get("Cylinder"), Hoses = get("Hoses"), Hose = get("Hose"), PnSheet = get("PnSheet"),
                        Text = get("Text"), Card = get("Card"), Via = get("Via"), Measure = get("Measure"), Pressure = get("Pressure")
                    };
                    if (p.Address.Length == 0) continue;
                    // "Measure.TR": a text of the drawings in another language (the step-by-step help's)
                    for (int i = 0; i < head.Length && i < f.Length; i++)
                        if (head[i].IndexOf('.') > 0 && f[i].Trim().Length > 0) p.Translations[head[i]] = f[i].Trim();
                    p.Commands.AddRange(get("Commands").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(c => c.ToUpperInvariant()));
                    lock (_lock)
                    {
                        var addresses = Table(_byAddress, p.Station);
                        addresses[p.Address] = p;
                        if (p.Read.Length > 0) addresses[p.Read] = p;
                        // A command of another controller ("M543@1" for a PLC 2 output
                        // the main PLC's manual bit drives): found from that one's bit.
                        foreach (var c in p.Commands)
                        {
                            int at; string bit;
                            if (PlcAddressHelper.TrySplitStation(c, out bit, out at))
                                Table(_byCommand, station != null ? station(at) : at)[bit] = p;
                            else Table(_byCommand, p.Station)[c] = p;
                        }
                    }
                    n++;
                }
                // The file's own controller 1 is where its supply applies; every
                // station it names shares the drawing.
                if (supply.ZeroVoltWire.Length > 0 || supply.SensorSupplyWire.Length > 0)
                    lock (_lock)
                        foreach (var st in _byAddress.Keys.ToList().Concat(new[] { station != null ? station(1) : 1 }).Distinct())
                            if (!_supply.ContainsKey(st)) _supply[st] = supply;
            }
            catch (Exception ex) { AuditLogger.Log("could not read the wiring list " + path + ": " + ex.Message); }
            return n;
        }

        private static Dictionary<string, Point> Table(Dictionary<int, Dictionary<string, Point>> all, int station)
        {
            Dictionary<string, Point> t;
            if (!all.TryGetValue(station, out t)) all[station] = t = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
            return t;
        }

        /// <summary>
        /// The mounted machines' Wiring.csv files, read once - each machine's own
        /// file for its own stations. Nothing mounted (a test): nothing read.
        /// </summary>
        public static void EnsureLoaded()
        {
            lock (_lock)
            {
                // Not before a machine is mounted: asked too early, it would stay empty.
                if (_loaded || Kaban.Core.Machines.MachineRegistry.Modules.Count == 0) return;
                _loaded = true;
            }
            try
            {
                var read = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in Kaban.Core.Machines.MachineRegistry.Modules.OrderByDescending(x => x.IsMain))
                {
                    string path;
                    try { path = m.Context.ResolveFile("Wiring.csv"); } catch { continue; }
                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                    // A mounted machine's OWN file only: ResolveFile falls back to the
                    // host's, whose points are not on the mounted machine's controller.
                    if (!m.IsMain && !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), m.Context.ModuleId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!read.Add(Path.GetFullPath(path))) continue;
                    var ctx = m.Context;
                    Load(path, local => ctx.Station(local));
                }
            }
            catch { /* no registry yet - nothing to show */ }
        }

        /// <summary>The point at an address - X34, Y12 or the relay it is read at (M212) - on a station; null when the drawings do not show it.</summary>
        public static Point Find(int station, string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            // "Y0@2": on that controller (12 October)
            PlcAddressHelper.Resolve(station, address.Trim(), out station, out address);
            EnsureLoaded();
            lock (_lock)
            {
                Dictionary<string, Point> t;
                Point p;
                return _byAddress.TryGetValue(station, out t) && t.TryGetValue(address.Trim(), out p) ? p : null;
            }
        }

        /// <summary>Every point of a station, each once (for a search by the drawing's words).</summary>
        public static List<Point> Points(int station)
        {
            EnsureLoaded();
            lock (_lock)
            {
                Dictionary<string, Point> t;
                return _byAddress.TryGetValue(station, out t) ? t.Values.Distinct().ToList() : new List<Point>();
            }
        }

        /// <summary>
        /// The output a manual command bit drives (M688 -> Y12), null when none is
        /// known - on its own controller or another (the point's Station: the back
        /// moulds' M543 on PLC 1 drives Y1 on PLC 2).
        /// </summary>
        public static Point OutputFor(int station, string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            PlcAddressHelper.Resolve(station, command.Trim(), out station, out command);
            EnsureLoaded();
            lock (_lock)
            {
                Dictionary<string, Point> t;
                Point p;
                return _byCommand.TryGetValue(station, out t) && t.TryGetValue(command.Trim(), out p) ? p : null;
            }
        }
    }
}
