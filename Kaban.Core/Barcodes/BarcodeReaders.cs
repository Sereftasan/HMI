using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using Kaban.Core.Helpers;

namespace Kaban.Core.Barcodes
{
    /// <summary>How a reader reaches the PC: a serial (COM) port - the old program's only kind - or as a keyboard (a USB scanner typing the code).</summary>
    public enum BarcodeReaderKind { Serial, Keyboard }

    /// <summary>A barcode reader (the old program's BarcodeReaderConfiguration: name, port, baud rate, parity, stop bits, active).</summary>
    public sealed class BarcodeReaderConfig
    {
        public string Name = "Reader";
        public BarcodeReaderKind Kind = BarcodeReaderKind.Serial;
        /// <summary>"COM3" (the old program kept the number only - "3" is read as COM3).</summary>
        public string Port = "COM1";
        public int BaudRate = 9600, DataBits = 8;
        public Parity Parity = Parity.None;
        public StopBits StopBits = StopBits.One;
        public bool Enabled = true;
        /// <summary>The machine (its module id) its codes are for - "" any: with two machines on the panel, the reader of each.</summary>
        public string Machine = "";

        public string PortName
        {
            get
            {
                var p = (Port ?? "").Trim();
                int n;
                return int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? "COM" + n : p;
            }
        }

        public BarcodeReaderConfig Clone() { return (BarcodeReaderConfig)MemberwiseClone(); }
    }

    /// <summary>What a part of the code is (the old program's BarcodeFieldType: profile code, X, Y - and more).</summary>
    public enum BarcodeFieldKind { ProfileCode = 1, Width = 2, Height = 3, Quantity = 4, OrderNo = 5, Other = 9 }

    /// <summary>
    /// A part of a read code (MD3030's barcode settings - its MDBarcode table: the field, where it starts, how long): the code
    /// "P1234 01200 00800" read as the profile code (from 1, 5 long), the width (from 7, 5 long), the height (from 13, 5 long).
    /// </summary>
    public sealed class BarcodeFieldConfig
    {
        public string Name = "";
        public BarcodeFieldKind Kind = BarcodeFieldKind.Other;
        /// <summary>Where the part starts: 1 is the first character (as the old page).</summary>
        public int Start = 1, Length = 1;
        /// <summary>A number read divided by this (a width of tenths: 10).</summary>
        public double Divisor = 1;

        public BarcodeFieldConfig Clone() { return (BarcodeFieldConfig)MemberwiseClone(); }
    }

    /// <summary>A code read: the text as it came, cleaned, its parts.</summary>
    public sealed class BarcodeScan
    {
        public string Raw = "", Code = "", Source = "";
        /// <summary>The machine the reader is for ("" any).</summary>
        public string Machine = "";
        public DateTime At = DateTime.Now;
        /// <summary>Its parts by name (the barcode fields' layout), and the known kinds' values.</summary>
        public readonly Dictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string ProfileCode = "", OrderNo = "";
        public double? Width, Height, Quantity;
        /// <summary>Why a part could not be read ("too short for Width").</summary>
        public readonly List<string> Problems = new List<string>();
        /// <summary>The page that took it ("" none).</summary>
        public string TakenBy = "";
    }

    /// <summary>
    /// THE BARCODE READERS (17 November: "take barcode reading and writing from KabanMachineSystem as well") - the old program's
    /// BarcodeReaderSettings / ucTestBarcode / InitBarcodeReader of each machine's main form, for every machine of the HMI:
    ///
    ///   the readers      a serial reader (COMn, its baud rate, parity, stop bits - as the old page) is opened while it is enabled;
    ///                    a keyboard reader (a USB scanner typing) is caught by the program's window (Kaban.UI BarcodeHost: keys
    ///                    coming much faster than a person types, ended by Enter).
    ///   the code         cleaned as the old program did (CR, LF, STX, ETX taken out), a prefix / suffix the scanner adds taken off,
    ///                    then split into its parts by the barcode fields (MD3030's: the profile code, X, Y - from where, how long).
    ///   who takes it     the pages that want codes register a handler - the newest first: a machine's Home fills its job (MK2010,
    ///                    MD3020: the profile's width and height), FA2010's Jobs page finds the piece. A code no page takes is still
    ///                    in Recent (HMI Settings > Barcodes shows the last ones) and in the log.
    ///
    /// Kept in C:\ProgramData\KabanHMI\Barcodes.cfg (for every machine of the PC - the readers are the PC's).
    /// </summary>
    public static class BarcodeReaders
    {
        /// <summary>Where the settings are kept. Replaceable (tests).</summary>
        public static string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Barcodes.cfg");

        private static readonly object Gate = new object();
        private static readonly List<BarcodeReaderConfig> _readers = new List<BarcodeReaderConfig>();
        private static readonly List<BarcodeFieldConfig> _fields = new List<BarcodeFieldConfig>();
        private static string _prefix = "", _suffix = "";
        private static bool _loaded;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>A code read and cleaned (on the thread Start was called on - the program's window - when there is one).</summary>
        public static event Action<BarcodeScan> Scanned;
        /// <summary>The settings saved.</summary>
        public static event EventHandler Changed;

        // ------------------------------------------------------------------ settings

        public static List<BarcodeReaderConfig> Readers { get { lock (Gate) { Load(); return _readers.Select(r => r.Clone()).ToList(); } } }
        public static List<BarcodeFieldConfig> Fields { get { lock (Gate) { Load(); return _fields.Select(f => f.Clone()).ToList(); } } }

        /// <summary>The parts of a code used while none is set (HMI Settings > Barcodes): a machine's own (MK2010's old fixed layout).</summary>
        public static List<BarcodeFieldConfig> FieldsInUse
        {
            get
            {
                var f = Fields;
                if (f.Count > 0) return f;
                lock (Gate) return _defaults.Select(x => x.Clone()).ToList();
            }
        }

        private static readonly List<BarcodeFieldConfig> _defaults = new List<BarcodeFieldConfig>();

        /// <summary>A machine's parts of a code, used while none is set (the first machine's that gives them).</summary>
        public static void DefaultFields(IEnumerable<BarcodeFieldConfig> fields)
        {
            lock (Gate)
            {
                if (_defaults.Count > 0 || fields == null) return;
                _defaults.AddRange(fields.Where(f => f != null && f.Start > 0 && f.Length > 0).Select(f => f.Clone()));
            }
        }

        /// <summary>The standard layout of a profile's code (MK2010's FillByBarcode): the profile code 1 - 10, the width 11 - 14, the height 15 - 18 (mm).</summary>
        public static BarcodeFieldConfig[] ProfileSizeLayout()
        {
            return new[]
            {
                new BarcodeFieldConfig { Name = "Profile", Kind = BarcodeFieldKind.ProfileCode, Start = 1, Length = 10 },
                new BarcodeFieldConfig { Name = "Width", Kind = BarcodeFieldKind.Width, Start = 11, Length = 4 },
                new BarcodeFieldConfig { Name = "Height", Kind = BarcodeFieldKind.Height, Start = 15, Length = 4 }
            };
        }
        /// <summary>Taken off the front / the end of every code (a scanner set to add "ABC" - MD3030 took it out).</summary>
        public static string Prefix { get { lock (Gate) { Load(); return _prefix; } } }
        public static string Suffix { get { lock (Gate) { Load(); return _suffix; } } }

        /// <summary>A keyboard reader is enabled: the program's window watches the keys.</summary>
        public static bool KeyboardReading { get { return Readers.Any(r => r.Enabled && r.Kind == BarcodeReaderKind.Keyboard); } }

        public static void Save(IEnumerable<BarcodeReaderConfig> readers, IEnumerable<BarcodeFieldConfig> fields, string prefix, string suffix)
        {
            lock (Gate)
            {
                _loaded = true;
                _readers.Clear();
                _readers.AddRange((readers ?? new BarcodeReaderConfig[0]).Select(r => r.Clone()));
                _fields.Clear();
                _fields.AddRange((fields ?? new BarcodeFieldConfig[0]).Where(f => f != null && f.Length > 0 && f.Start > 0).Select(f => f.Clone()));
                _prefix = prefix ?? "";
                _suffix = suffix ?? "";
                var sb = new StringBuilder();
                sb.AppendLine("# Kaban HMI - the barcode readers and the parts of a code (HMI Settings > Barcodes)");
                sb.AppendLine("prefix\t" + Esc(_prefix));
                sb.AppendLine("suffix\t" + Esc(_suffix));
                foreach (var r in _readers)
                    sb.AppendLine(string.Join("\t", "reader", Esc(r.Name), r.Kind.ToString(), Esc(r.Port), r.BaudRate.ToString(CI), r.DataBits.ToString(CI),
                                              r.Parity.ToString(), r.StopBits.ToString(), r.Enabled ? "on" : "off", Esc(r.Machine)));
                foreach (var f in _fields)
                    sb.AppendLine(string.Join("\t", "field", Esc(f.Name), f.Kind.ToString(), f.Start.ToString(CI), f.Length.ToString(CI), f.Divisor.ToString("0.####", CI)));
                try { SafeFile.WriteAllText(SettingsPath, sb.ToString()); }
                catch (Exception ex) { AuditLogger.Log("[BARCODE] " + SettingsPath + " could not be written: " + ex.Message); }
            }
            AuditLogger.Log("[BARCODE] the barcode settings saved: " + _readers.Count + " reader(s), " + _fields.Count + " part(s) of a code.");
            var h = Changed;
            if (h != null) h(null, EventArgs.Empty);
            if (_started) Restart();
        }

        /// <summary>Reads the file again (tests).</summary>
        public static void Reload() { lock (Gate) { _loaded = false; Load(); } }

        private static string Esc(string s) { return (s ?? "").Replace("\t", " ").Replace("\r", "").Replace("\n", " "); }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _readers.Clear();
            _fields.Clear();
            _prefix = _suffix = "";
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    if (line.StartsWith("#", StringComparison.Ordinal)) continue;
                    var t = line.Split('\t');
                    if (t.Length >= 2 && t[0] == "prefix") _prefix = t[1];
                    else if (t.Length >= 2 && t[0] == "suffix") _suffix = t[1];
                    else if (t.Length >= 9 && t[0] == "reader")
                    {
                        var r = new BarcodeReaderConfig { Name = t[1], Port = t[3], Enabled = t[8] == "on" };
                        BarcodeReaderKind k; Parity p; StopBits sb; int n;
                        if (Enum.TryParse(t[2], out k)) r.Kind = k;
                        if (int.TryParse(t[4], NumberStyles.Integer, CI, out n) && n > 0) r.BaudRate = n;
                        if (int.TryParse(t[5], NumberStyles.Integer, CI, out n) && n >= 5 && n <= 8) r.DataBits = n;
                        if (Enum.TryParse(t[6], out p)) r.Parity = p;
                        if (Enum.TryParse(t[7], out sb)) r.StopBits = sb;
                        if (t.Length >= 10) r.Machine = t[9].Trim();
                        _readers.Add(r);
                    }
                    else if (t.Length >= 5 && t[0] == "field")
                    {
                        var f = new BarcodeFieldConfig { Name = t[1] };
                        BarcodeFieldKind k; int n; double d;
                        if (Enum.TryParse(t[2], out k)) f.Kind = k;
                        if (int.TryParse(t[3], NumberStyles.Integer, CI, out n)) f.Start = n;
                        if (int.TryParse(t[4], NumberStyles.Integer, CI, out n)) f.Length = n;
                        if (t.Length >= 6 && double.TryParse(t[5], NumberStyles.Float, CI, out d) && d > 0) f.Divisor = d;
                        if (f.Start > 0 && f.Length > 0) _fields.Add(f);
                    }
                }
            }
            catch (Exception ex) { AuditLogger.Log("[BARCODE] " + SettingsPath + " could not be read: " + ex.Message); }
        }

        // ------------------------------------------------------------------ a code

        /// <summary>The code as the old program cleaned it: CR, LF, STX (\u0002), ETX (\u0003) out, the prefix / suffix off, trimmed.</summary>
        public static string Clean(string raw)
        {
            var s = (raw ?? "").Replace("\r", "").Replace("\n", "").Replace("\u0002", "").Replace("\u0003", "").Trim();
            string pre = Prefix, suf = Suffix;
            if (pre.Length > 0 && s.StartsWith(pre, StringComparison.Ordinal)) s = s.Substring(pre.Length);
            if (suf.Length > 0 && s.EndsWith(suf, StringComparison.Ordinal)) s = s.Substring(0, s.Length - suf.Length);
            return s.Trim();
        }

        /// <summary>A code split into its parts by the fields given (the settings' when null) - MD3030's FillByBarcode.</summary>
        public static BarcodeScan Parse(string raw, IEnumerable<BarcodeFieldConfig> fields = null, string source = "")
        {
            var scan = new BarcodeScan { Raw = raw ?? "", Code = Clean(raw), Source = source ?? "" };
            foreach (var f in (fields ?? FieldsInUse))
            {
                if (f == null || f.Start <= 0 || f.Length <= 0) continue;
                int start = f.Start - 1;
                if (start + f.Length > scan.Code.Length)
                {
                    scan.Problems.Add(string.Format(CI, "the code is too short for {0} ({1} - {2})", f.Name.Length > 0 ? f.Name : f.Kind.ToString(), f.Start, f.Start + f.Length - 1));
                    continue;
                }
                string part = scan.Code.Substring(start, f.Length).Trim();
                scan.Fields[f.Name.Length > 0 ? f.Name : f.Kind.ToString()] = part;
                double v;
                bool num = double.TryParse(part.Replace(',', '.'), NumberStyles.Float, CI, out v);
                if (num && f.Divisor > 0) v /= f.Divisor;
                switch (f.Kind)
                {
                    case BarcodeFieldKind.ProfileCode: scan.ProfileCode = part; break;
                    case BarcodeFieldKind.OrderNo: scan.OrderNo = part; break;
                    case BarcodeFieldKind.Width: if (num) scan.Width = v; else scan.Problems.Add("the width is not a number: " + part); break;
                    case BarcodeFieldKind.Height: if (num) scan.Height = v; else scan.Problems.Add("the height is not a number: " + part); break;
                    case BarcodeFieldKind.Quantity: if (num) scan.Quantity = v; else scan.Problems.Add("the quantity is not a number: " + part); break;
                }
            }
            return scan;
        }

        // ------------------------------------------------------------------ who takes it

        private sealed class Handler { public string Owner; public Func<BarcodeScan, bool> Take; }
        private static readonly List<Handler> _handlers = new List<Handler>();
        private static readonly List<BarcodeScan> _recent = new List<BarcodeScan>();
        public const int RecentKept = 40;

        /// <summary>A page that wants the codes: it returns true when it took one. The newest is asked first. Dispose to stop.</summary>
        public static IDisposable Take(string owner, Func<BarcodeScan, bool> take)
        {
            var h = new Handler { Owner = owner ?? "", Take = take };
            lock (Gate) _handlers.Add(h);
            return new Releaser(() => { lock (Gate) _handlers.Remove(h); });
        }

        private sealed class Releaser : IDisposable
        {
            private Action _a;
            public Releaser(Action a) { _a = a; }
            public void Dispose() { var a = Interlocked.Exchange(ref _a, null); if (a != null) a(); }
        }

        /// <summary>The last codes read, newest first.</summary>
        public static List<BarcodeScan> Recent { get { lock (Gate) return _recent.ToList(); } }

        /// <summary>
        /// A code read (a reader, the keyboard, the test page): cleaned, split, offered to the pages - the newest first - and told
        /// to everyone. Returns the scan.
        /// </summary>
        public static BarcodeScan Feed(string raw, string source, string machine = "")
        {
            var scan = Parse(raw, null, source);
            scan.Machine = machine ?? "";
            if (scan.Code.Length == 0) return scan;
            List<Handler> hs;
            lock (Gate) hs = _handlers.ToList();
            for (int i = hs.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (hs[i].Take != null && hs[i].Take(scan)) { scan.TakenBy = hs[i].Owner; break; }
                }
                catch (Exception ex) { AuditLogger.Log("[BARCODE] " + hs[i].Owner + " could not take " + scan.Code + ": " + ex.Message); }
            }
            lock (Gate)
            {
                _recent.Insert(0, scan);
                if (_recent.Count > RecentKept) _recent.RemoveRange(RecentKept, _recent.Count - RecentKept);
            }
            AuditLogger.Log("[BARCODE] " + (scan.Source.Length > 0 ? scan.Source + ": " : "") + scan.Code +
                            (scan.TakenBy.Length > 0 ? " - taken by " + scan.TakenBy : " - no page took it") +
                            (scan.Problems.Count > 0 ? " (" + string.Join("; ", scan.Problems) + ")" : ""));
            var e = Scanned;
            if (e != null)
                foreach (Action<BarcodeScan> each in e.GetInvocationList())
                {
                    try { each(scan); } catch (Exception ex) { AuditLogger.Log("[BARCODE] a listener failed: " + ex.Message); }
                }
            return scan;
        }

        // ------------------------------------------------------------------ the serial readers

        private sealed class Open
        {
            public BarcodeReaderConfig Config;
            public SerialPort Port;
            public readonly StringBuilder Buffer = new StringBuilder();
            public Timer Idle;
            public string Error = "";
        }

        private static readonly List<Open> _open = new List<Open>();
        private static SynchronizationContext _ui;
        private static bool _started;

        /// <summary>How long a reader's characters may pause before what came is a code (a reader that sends no CR / LF).</summary>
        public static int IdleMs = 120;

        /// <summary>Replaceable (tests): opens a port.</summary>
        public static Func<BarcodeReaderConfig, SerialPort> OpenPort = c =>
        {
            var p = new SerialPort(c.PortName, c.BaudRate, c.Parity, c.DataBits, c.StopBits) { ReadTimeout = 500, Encoding = Encoding.ASCII };
            p.Open();
            p.DiscardInBuffer();
            p.DiscardOutBuffer();
            return p;
        };

        /// <summary>The serial readers opened (once the program's window is up); a reader that cannot be opened is said in the log and on the page.</summary>
        public static void Start()
        {
            lock (Gate)
            {
                _ui = SynchronizationContext.Current;
                _started = true;
            }
            Restart();
        }

        /// <summary>Every reader closed (the program ending).</summary>
        public static void Stop()
        {
            lock (Gate) _started = false;
            CloseAll();
        }

        /// <summary>The state of each serial reader: "COM3 open", "COM4: access denied".</summary>
        public static List<string> State
        {
            get
            {
                lock (Gate)
                    return _open.Select(o => o.Config.Name + " (" + o.Config.PortName + "): " + (o.Port != null && o.Port.IsOpen ? "open" : o.Error)).ToList();
            }
        }

        private static void CloseAll()
        {
            List<Open> was;
            lock (Gate) { was = _open.ToList(); _open.Clear(); }
            foreach (var o in was)
            {
                try { if (o.Idle != null) o.Idle.Dispose(); } catch { }
                try { if (o.Port != null) { o.Port.DataReceived -= DataReceived; if (o.Port.IsOpen) o.Port.Close(); o.Port.Dispose(); } } catch { }
            }
        }

        private static void Restart()
        {
            CloseAll();
            foreach (var c in Readers.Where(r => r.Enabled && r.Kind == BarcodeReaderKind.Serial))
            {
                var o = new Open { Config = c };
                try
                {
                    o.Port = OpenPort(c);
                    o.Port.DataReceived += DataReceived;
                    AuditLogger.Log("[BARCODE] reader " + c.Name + " opened on " + c.PortName + " (" + c.BaudRate + " baud).");
                }
                catch (Exception ex)
                {
                    o.Error = ex.Message;
                    AuditLogger.Log("[BARCODE] reader " + c.Name + " could not be opened on " + c.PortName + ": " + ex.Message);
                }
                o.Idle = new Timer(_ => Flush(o), null, Timeout.Infinite, Timeout.Infinite);
                lock (Gate) _open.Add(o);
            }
        }

        private static void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Open o;
            lock (Gate) o = _open.FirstOrDefault(x => ReferenceEquals(x.Port, sender));
            if (o == null) return;
            string got;
            try { got = o.Port.ReadExisting(); } catch (Exception ex) { AuditLogger.Log("[BARCODE] " + o.Config.PortName + ": " + ex.Message); return; }
            Received(o, got);
        }

        /// <summary>Characters from a reader: a code at each CR / LF, or after a pause.</summary>
        private static void Received(Open o, string got)
        {
            var codes = new List<string>();
            lock (o.Buffer)
            {
                foreach (char ch in got ?? "")
                {
                    if (ch == '\r' || ch == '\n')
                    {
                        if (o.Buffer.Length > 0) { codes.Add(o.Buffer.ToString()); o.Buffer.Length = 0; }
                    }
                    else o.Buffer.Append(ch);
                }
                if (o.Buffer.Length > 0) o.Idle.Change(IdleMs, Timeout.Infinite);
            }
            foreach (var c in codes) Deliver(c, o.Config.Name, o.Config.Machine);
        }

        private static void Flush(Open o)
        {
            string code;
            lock (o.Buffer) { code = o.Buffer.ToString(); o.Buffer.Length = 0; }
            if (code.Length > 0) Deliver(code, o.Config.Name, o.Config.Machine);
        }

        /// <summary>A code given to the program's window's thread (the pages' handlers are its controls).</summary>
        private static void Deliver(string raw, string source, string machine)
        {
            var ui = _ui;
            if (ui != null) ui.Post(_ => Feed(raw, source, machine), null);
            else Feed(raw, source, machine);
        }

        /// <summary>Characters as a reader sends them (tests: a code split over two reads, no CR at the end).</summary>
        public static void Simulate(BarcodeReaderConfig c, string got)
        {
            Open o;
            lock (Gate)
            {
                o = _open.FirstOrDefault(x => x.Config.Name == c.Name);
                if (o == null) { o = new Open { Config = c }; o.Idle = new Timer(_ => Flush(o), null, Timeout.Infinite, Timeout.Infinite); _open.Add(o); }
            }
            Received(o, got);
        }
    }
}
