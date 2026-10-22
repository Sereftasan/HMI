using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Kaban.Core.Interfaces;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// A PLC'S DATA, KEPT AND WRITTEN BACK (22 October: "I need you to add Read and Write button to
    /// the end of PLC Connections table ... The read button should read all the PLC data (and
    /// connected simple motion data when available) and save it with the machine name, PLC
    /// description, PLC serial number, user name, date and the user's read description ... And Write
    /// is gonna open the read backup list and selection of the devices to be written to the PLC (and
    /// another column for the entry if the read device interval and write device interval is
    /// different, e.g writing to D2500 to D3499 of the read data from R1000 to R1999 ...)").
    ///
    /// WHAT IS READ: the CPU's devices (an FX5's X, Y, M, L, B, F, S, the timers' and counters'
    /// current values, D, W, R - not the system's SM / SD), each as far as the CPU's parameters
    /// give it - a real CPU refuses a device beyond them, so each size is found by asking (the
    /// simulator answers anything: it takes GX Works3's defaults). And each simple motion module
    /// of the PLC (its axes on the Axis Settings pages): what the module keeps in its flash ROM -
    /// the parameters, positioning data No.1 - 100 with their interpolation axes, block start data,
    /// servo parameters, the PLC CPU memo area, the mark detection settings - from the buffer memory
    /// list of the FX5 Simple Motion Module manual (Application, 10.2 and 10.9). A module's
    /// synchronous control parameters (its Advanced Synchronous Control manual) are not in it.
    ///
    /// THE FILE: text, one header line a fact, then one [area] after another, its words 16 a line
    /// (decimal; a bit area's in hexadecimal, 16 bits a word, the first device the lowest bit). In
    /// ProgramData\KabanHMI\PlcBackups, or wherever it was copied to.
    ///
    /// WHAT IS WRITTEN BACK: the areas (or parts of them) ticked, each to its own devices or - typed
    /// beside it - to others of the same kind (bits to bits, words to words). Inputs and outputs are
    /// never written: X is the PLC's own reading of its inputs, Y would switch outputs.
    /// </summary>
    public static class PlcBackup
    {
        // ---- the devices of a CPU ----------------------------------------------------------------------------

        public sealed class DeviceKind
        {
            public string Prefix = "";
            public bool Bits;
            /// <summary>GX Works3's default number of devices; the most its parameters can give.</summary>
            public int Default, Max;
            /// <summary>False: kept, never written back (X, Y).</summary>
            public bool Writable = true;
            public string TitleKey = "", Title = "";
        }

        private static DeviceKind K(string prefix, bool bits, int dflt, int max, bool writable, string title)
        {
            return new DeviceKind { Prefix = prefix, Bits = bits, Default = dflt, Max = max, Writable = writable, TitleKey = "Bak_Dev" + prefix, Title = title };
        }

        /// <summary>
        /// A MELSEC iQ-F FX5 CPU's devices, its default sizes (GX Works3) and the most its parameters
        /// can give - what is read, in this order.
        /// </summary>
        public static readonly IList<DeviceKind> Fx5 = new List<DeviceKind>
        {
            K("X", true, 1024, 1024, false, "Inputs (X)"),
            K("Y", true, 1024, 1024, false, "Outputs (Y)"),
            K("M", true, 7680, 32768, true, "Internal relays (M)"),
            K("L", true, 7680, 32768, true, "Latch relays (L)"),
            K("B", true, 256, 32768, true, "Link relays (B)"),
            K("F", true, 128, 32768, true, "Annunciators (F)"),
            K("S", true, 4096, 4096, true, "Step relays (S)"),
            K("TN", false, 512, 1024, true, "Timers, current values (T)"),
            K("STN", false, 16, 1024, true, "Retentive timers, current values (ST)"),
            K("CN", false, 256, 1024, true, "Counters, current values (C)"),
            K("D", false, 8000, 65536, true, "Data registers (D)"),
            K("W", false, 512, 32768, true, "Link registers (W)"),
            K("R", false, 32768, 32768, true, "File registers (R)")
        }.AsReadOnly();

        /// <summary>The kind of a device's letters ("D"), or null (a module's buffer memory, a device not read).</summary>
        public static DeviceKind KindOf(string prefix)
        {
            return Fx5.FirstOrDefault(k => string.Equals(k.Prefix, prefix, StringComparison.OrdinalIgnoreCase));
        }

        // ---- what a backup holds ------------------------------------------------------------------------------

        /// <summary>A run of devices read: its first, how many, and their words.</summary>
        public sealed class Area
        {
            /// <summary>Its first device: "D0", "M0", "U1\G6000".</summary>
            public string First = "";
            /// <summary>Its devices (a bit area's bits).</summary>
            public int Points;
            public bool Bits;
            public string Title = "";
            /// <summary>
            /// For a module's area with words the manual does not name (maker settings): the runs written
            /// back, each (its offset from <see cref="First"/>, its words). Null: every word.
            /// </summary>
            public List<int[]> Pieces;
            /// <summary>The words read: a word area's one a device, a bit area's 16 devices a word.</summary>
            public short[] Words = new short[0];

            public string Prefix { get { string p; long n; PlcAddressHelper.TrySplit(First, out p, out n); return p; } }
            public long Start { get { string p; long n; PlcAddressHelper.TrySplit(First, out p, out n); return n; } }
            /// <summary>Its device <paramref name="i"/> (0: the first).</summary>
            public string Device(long i) { return PlcAddressHelper.Compose(Prefix, Start + i); }
            public string Last { get { return Device(Math.Max(0, Points - 1)); } }
            /// <summary>"D0 - D7999".</summary>
            public string Range { get { return Points > 1 ? First + " - " + Last : First; } }
            public bool IsModule { get { return SimpleMotion.IsModuleAddress(First); } }
            /// <summary>Written back at all: not inputs or outputs.</summary>
            public bool Writable { get { if (IsModule) return true; var k = KindOf(Prefix); return k == null || k.Writable; } }
            public int WordCount { get { return Bits ? (Points + 15) / 16 : Points; } }

            /// <summary>Device <paramref name="i"/>'s value: a bit's 0 / 1, a word's as read (signed).</summary>
            public int Value(long i)
            {
                if (!Bits) return i < Words.Length ? Words[i] : 0;
                long w = i / 16;
                return w < Words.Length ? (Words[w] >> (int)(i % 16)) & 1 : 0;
            }
        }

        public sealed class Backup
        {
            public string Machine = "", Plc = "", Cpu = "", Serial = "", User = "", Description = "";
            public DateTime Date;
            public int Station;
            /// <summary>Its simple motion modules: the module's number and its axes.</summary>
            public List<KeyValuePair<int, int>> Modules = new List<KeyValuePair<int, int>>();
            public List<Area> Areas = new List<Area>();
            /// <summary>What could not be read (an area the PLC refused), in words.</summary>
            public List<string> Skipped = new List<string>();
            /// <summary>The file it is in (Save / Load).</summary>
            public string Path = "";

            public long WordTotal { get { return Areas.Sum(a => (long)a.WordCount); } }
        }

        // ---- where backups are kept ---------------------------------------------------------------------------

        public const string Extension = ".plcbak";

        /// <summary>ProgramData\KabanHMI\PlcBackups - replaceable for the tests.</summary>
        public static string Folder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "PlcBackups");

        /// <summary>"2026-10-22 183012 Processing Center PLC1.plcbak" - in the backups' folder.</summary>
        public static string FileFor(Backup b)
        {
            string name = b.Date.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture) + " " + (b.Machine + " " + b.Plc).Trim();
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return System.IO.Path.Combine(Folder, name + Extension);
        }

        // ---- reading -------------------------------------------------------------------------------------------

        /// <summary>
        /// How far a read or write has got: done of total words, what now. False from it: cancel - the
        /// work stops at the next area (a write never stops half way through a block).
        /// </summary>
        public delegate bool Progress(long done, long total, string text);

        /// <summary>The most words a block call asks for: an MC protocol frame's batch.</summary>
        public const int Chunk = 960;

        /// <summary>
        /// The CPU's devices to read, each as far as the CPU has it: asked device by device when the
        /// driver's controller refuses a device beyond its range (the last device present found by
        /// halving), else GX Works3's defaults. A bit device's count is whole words (16).
        /// </summary>
        public static List<Area> PlanDevices(IPlcDriver driver, IPlcBlockAccess block)
        {
            var list = new List<Area>();
            bool ask = block != null && block.RefusesOutOfRange;
            foreach (var k in Fx5)
            {
                int n = ask ? Probe(driver, k) : k.Default;
                if (k.Bits) n -= n % 16;
                if (n <= 0) continue;
                list.Add(new Area { First = k.Prefix + "0", Points = n, Bits = k.Bits, Title = LanguageManager.GetString(k.TitleKey, k.Title) });
            }
            return list;
        }

        /// <summary>How many of a device the CPU has: the largest n whose device n - 1 it answers.</summary>
        public static int Probe(IPlcDriver driver, DeviceKind k)
        {
            Func<long, bool> has = n => { int v; return driver.TryRead(PlcAddressHelper.Compose(k.Prefix, n - 1), out v); };
            if (!has(1)) return 0;
            long lo, hi;
            if (has(k.Default))
            {
                if (k.Max <= k.Default || has(k.Max)) return Math.Max(k.Default, k.Max);
                lo = k.Default; hi = k.Max;
            }
            else { lo = 1; hi = k.Default; }
            while (hi - lo > 1)
            {
                long mid = (lo + hi) / 2;
                if (has(mid)) lo = mid; else hi = mid;
            }
            return (int)lo;
        }

        private static Area M(int module, int address, int words, string title, List<int[]> pieces = null)
        {
            return new Area { First = SimpleMotion.Device(module, address), Points = words, Title = title, Pieces = pieces };
        }

        /// <summary>
        /// A simple motion module's data that its flash ROM keeps (the manual's 10.9 table, "Possible"
        /// to write), for <paramref name="axes"/> axes - n is the axis - 1:
        /// common parameters 33 - 106 and 58000 - 58002; parameters 0 + 150n (0 - 15, 17 - 91,
        /// 100 - 149); positioning data 6000 + 1000n (No.1 - 100) and their interpolation axes
        /// 71000 + 1000n (two words of each 10); block start data 22000 + 400n; servo parameters
        /// 28400 + 100n, 64400 + 70n, 65520 + 340n; the PLC CPU memo area 30000; mark detection
        /// settings 54000.
        /// </summary>
        public static List<Area> ModuleAreas(int module, int axes)
        {
            Func<string, string, string> t = (key, en) => LanguageManager.GetString(key, en);
            string mod = string.Format(t("Bak_Module", "Simple motion module {0}"), module);
            var list = new List<Area>
            {
                M(module, 33, 74, mod + ": " + t("Bak_ModCommon", "common parameters"),
                  new List<int[]> { new[] { 0, 1 }, new[] { 2, 1 }, new[] { 34, 1 }, new[] { 72, 2 } }),
                M(module, 58000, 3, mod + ": " + t("Bak_ModCommonInputs", "common parameters (input logic)"))
            };
            for (int a = 1; a <= axes; a++)
            {
                int n = a - 1;
                string ax = mod + ", " + string.Format(t("Bak_Axis", "axis {0}"), a) + ": ";
                list.Add(M(module, 150 * n, 150, ax + t("Bak_ModParameters", "parameters (Pr.)"),
                           new List<int[]> { new[] { 0, 16 }, new[] { 17, 75 }, new[] { 100, 50 } }));
                list.Add(M(module, 6000 + 1000 * n, 1000, ax + t("Bak_ModPositioning", "positioning data No.1 - 100")));
                list.Add(M(module, 71000 + 1000 * n, 1000, ax + t("Bak_ModInterpolation", "positioning data's axes to be interpolated"),
                           Enumerable.Range(0, 100).Select(k => new[] { 10 * k, 2 }).ToList()));
                list.Add(M(module, 22000 + 400 * n, 400, ax + t("Bak_ModBlockStart", "block start data and condition data")));
                list.Add(M(module, 28400 + 100 * n, 96, ax + t("Bak_ModServo1", "servo parameters (series, PA01 - PA18, PB, PC)")));
                list.Add(M(module, 64400 + 70 * n, 65, ax + t("Bak_ModServo2", "servo parameters (PA19 - PA32, PB, PC continued)")));
                list.Add(M(module, 65520 + 340 * n, 224, ax + t("Bak_ModServo3", "servo parameters (PD, PE, PF, Po, PS)")));
            }
            list.Add(M(module, 30000, 100, mod + ": " + t("Bak_ModMemo", "PLC CPU memo area")));
            list.Add(M(module, 54000, 320, mod + ": " + t("Bak_ModMark", "mark detection settings")));
            return list;
        }

        /// <summary>
        /// Reads <paramref name="areas"/> into <paramref name="backup"/>, <see cref="Chunk"/> words a
        /// call: an area the PLC refuses is left out and named in <see cref="Backup.Skipped"/>. False
        /// when cancelled (the backup then holds what was read so far - not to be saved).
        /// </summary>
        public static bool ReadAreas(IPlcBlockAccess block, Backup backup, IList<Area> areas, Progress progress)
        {
            long total = areas.Sum(a => (long)a.WordCount), done = 0;
            foreach (var area in areas)
            {
                if (progress != null && !progress(done, total, string.Format(LanguageManager.GetString("Msg_BakReading", "Reading {0} ({1})..."), area.Title, area.Range)))
                    return false;
                var words = new short[area.WordCount];
                bool ok = true;
                for (int at = 0; at < words.Length && ok; at += Chunk)
                {
                    int n = Math.Min(Chunk, words.Length - at);
                    short[] got;
                    string first = area.Bits ? area.Device(16L * at) : area.Device(at);
                    ok = block.TryReadBlock(first, n, out got) && got != null && got.Length == n;
                    if (ok) Array.Copy(got, 0, words, at, n);
                    done += n;
                    // a moment for the screen's own reads, which wait their turn on the same connection
                    System.Threading.Thread.Sleep(2);
                }
                if (!ok)
                {
                    backup.Skipped.Add(area.Title + " (" + area.Range + ")");
                    continue;
                }
                area.Words = words;
                backup.Areas.Add(area);
            }
            if (progress != null) progress(total, total, "");
            return true;
        }

        // ---- the file ------------------------------------------------------------------------------------------

        private static string Line(string s) { return (s ?? "").Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", " "); }
        private static string Unline(string s) { return (s ?? "").Replace("\\n", "\r\n"); }

        public static void Save(Backup b, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Kaban HMI - a PLC's data (HMI Settings > PLC Connection > Read). [area] lines: first device;devices;Words or Bits;title[;pieces].");
            sb.AppendLine("# Its words 16 a line - decimal; a bit area's hexadecimal, 16 devices a word, the first the lowest bit.");
            sb.AppendLine("Machine;" + Line(b.Machine));
            sb.AppendLine("Plc;" + Line(b.Plc));
            sb.AppendLine("Cpu;" + Line(b.Cpu));
            sb.AppendLine("Serial;" + Line(b.Serial));
            sb.AppendLine("User;" + Line(b.User));
            sb.AppendLine("Date;" + b.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine("Station;" + b.Station.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("Description;" + Line(b.Description));
            sb.AppendLine("Modules;" + string.Join(",", b.Modules.Select(m => m.Key + ":" + m.Value)));
            if (b.Skipped.Count > 0) sb.AppendLine("Skipped;" + Line(string.Join(" | ", b.Skipped)));
            foreach (var a in b.Areas)
            {
                sb.Append('[').Append(a.First).Append(';').Append(a.Points.ToString(CultureInfo.InvariantCulture)).Append(';')
                  .Append(a.Bits ? "Bits" : "Words").Append(';').Append(Line(a.Title).Replace(";", ",").Replace("]", ")"));
                if (a.Pieces != null) sb.Append(';').Append(string.Join(" ", a.Pieces.Select(p => p[0] + "+" + p[1])));
                sb.AppendLine("]");
                for (int i = 0; i < a.Words.Length; i += 16)
                {
                    var row = a.Words.Skip(i).Take(16).Select(w => a.Bits ? ((ushort)w).ToString("X4", CultureInfo.InvariantCulture)
                                                                         : w.ToString(CultureInfo.InvariantCulture));
                    sb.AppendLine(string.Join(",", row));
                }
            }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
            var temp = path + ".tmp";
            File.WriteAllText(temp, sb.ToString(), new UTF8Encoding(true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
            b.Path = path;
        }

        /// <summary>A backup from its file; <paramref name="headerOnly"/>: its facts alone (the list). Null when it is none.</summary>
        public static Backup Load(string path, bool headerOnly = false)
        {
            try
            {
                var b = new Backup { Path = path };
                Area area = null;
                var words = new List<short>();
                Action close = () => { if (area != null) { area.Words = words.ToArray(); b.Areas.Add(area); } area = null; words = new List<short>(); };
                bool any = false;
                foreach (var raw in File.ReadLines(path))
                {
                    var line = raw.TrimEnd();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("["))
                    {
                        if (headerOnly) break;
                        close();
                        var f = line.Trim('[', ']').Split(';');
                        if (f.Length < 4) return null;
                        area = new Area
                        {
                            First = f[0].Trim(), Points = int.Parse(f[1], CultureInfo.InvariantCulture),
                            Bits = f[2].Trim() == "Bits", Title = Unline(f[3])
                        };
                        if (f.Length > 4 && f[4].Trim().Length > 0)
                            area.Pieces = f[4].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(p => p.Split('+').Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray()).ToList();
                        continue;
                    }
                    if (area != null)
                    {
                        foreach (var w in line.Split(','))
                            words.Add(area.Bits ? unchecked((short)ushort.Parse(w.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                                                : short.Parse(w.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture));
                        continue;
                    }
                    int cut = line.IndexOf(';');
                    if (cut <= 0) continue;
                    string key = line.Substring(0, cut), value = Unline(line.Substring(cut + 1));
                    any = true;
                    switch (key)
                    {
                        case "Machine": b.Machine = value; break;
                        case "Plc": b.Plc = value; break;
                        case "Cpu": b.Cpu = value; break;
                        case "Serial": b.Serial = value; break;
                        case "User": b.User = value; break;
                        case "Description": b.Description = value; break;
                        case "Date":
                            DateTime d;
                            if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) b.Date = d;
                            break;
                        case "Station": int st; if (int.TryParse(value, out st)) b.Station = st; break;
                        case "Modules":
                            foreach (var m in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                var p = m.Split(':');
                                int mo, ax;
                                if (p.Length == 2 && int.TryParse(p[0], out mo) && int.TryParse(p[1], out ax)) b.Modules.Add(new KeyValuePair<int, int>(mo, ax));
                            }
                            break;
                        case "Skipped": b.Skipped.AddRange(value.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries)); break;
                    }
                }
                close();
                return any ? b : null;
            }
            catch { return null; }
        }

        /// <summary>The backups in <see cref="Folder"/> (their facts only), the newest first.</summary>
        public static List<Backup> List()
        {
            var list = new List<Backup>();
            if (!Directory.Exists(Folder)) return list;
            foreach (var f in Directory.GetFiles(Folder, "*" + Extension))
            {
                var b = Load(f, headerOnly: true);
                if (b != null) list.Add(b);
            }
            return list.OrderByDescending(b => b.Date).ToList();
        }

        // ---- writing back ------------------------------------------------------------------------------------

        /// <summary>A part of an area written back: its devices <see cref="From"/> .. + <see cref="Count"/>, to <see cref="Target"/> on.</summary>
        public sealed class WriteRange
        {
            public Area Source;
            public int From, Count;
            /// <summary>The first device written; its own when they are the same.</summary>
            public string Target = "";
            /// <summary>"R1000 - R1999 to D2500 - D3499".</summary>
            public override string ToString()
            {
                string read = Source.Device(From) + (Count > 1 ? " - " + Source.Device(From + Count - 1) : "");
                string prefix; long t;
                if (!PlcAddressHelper.TrySplit(Target, out prefix, out t) || string.Equals(Target, Source.Device(From), StringComparison.OrdinalIgnoreCase))
                    return read;
                return read + " \u2192 " + Target + (Count > 1 ? " - " + PlcAddressHelper.Compose(prefix, t + Count - 1) : "");
            }
        }

        private static readonly Regex RangeRx = new Regex(@"^\s*(\S+?)\s*(?:-|\u2013|\.\.|~|\bto\b)\s*(\S+)\s*$", RegexOptions.IgnoreCase);

        /// <summary>
        /// "R1000 - R1999" (also "R1000..R1999", "R1000 to R1999", or one device "R1000") as devices of
        /// <paramref name="area"/>: its <paramref name="from"/> and <paramref name="count"/>. Empty: the whole area.
        /// </summary>
        public static bool TryParseRange(Area area, string text, out int from, out int count, out string error)
        {
            from = 0; count = area.Points; error = "";
            text = (text ?? "").Trim();
            if (text.Length == 0) return true;
            string a = text, b = text;
            var m = RangeRx.Match(text);
            if (m.Success) { a = m.Groups[1].Value; b = m.Groups[2].Value; }
            string pa, pb; long na, nb;
            if (!PlcAddressHelper.TrySplit(a, out pa, out na) || !PlcAddressHelper.TrySplit(b, out pb, out nb))
            {
                error = LanguageManager.GetString("Msg_BakNotDevices", "Not devices - write them as R1000 - R1999.");
                return false;
            }
            if (!string.Equals(pa, area.Prefix, StringComparison.OrdinalIgnoreCase) || !string.Equals(pb, area.Prefix, StringComparison.OrdinalIgnoreCase) ||
                na < area.Start || nb < na || nb >= area.Start + area.Points)
            {
                error = string.Format(LanguageManager.GetString("Msg_BakOutsideArea", "Within {0} only."), area.Range);
                return false;
            }
            from = (int)(na - area.Start);
            count = (int)(nb - na + 1);
            return true;
        }

        /// <summary>
        /// Where a part is written: empty - its own devices; "D2500", or "D2500 - D3499" (as many devices
        /// as read). Bits to bits, words to words; never to inputs or outputs; a module's data to a module's
        /// buffer memory.
        /// </summary>
        public static bool TryParseTarget(Area area, int from, int count, string text, out string first, out string error)
        {
            first = area.Device(from); error = "";
            text = (text ?? "").Trim();
            if (text.Length == 0)
            {
                if (!area.Writable) { error = LanguageManager.GetString("Msg_BakNotWritable", "Inputs and outputs are not written back."); return false; }
                return true;
            }
            string a = text, b = null;
            var m = RangeRx.Match(text);
            if (m.Success) { a = m.Groups[1].Value; b = m.Groups[2].Value; }
            string pa; long na;
            if (!PlcAddressHelper.TrySplit(a, out pa, out na))
            {
                error = LanguageManager.GetString("Msg_BakNotDevices", "Not devices - write them as R1000 - R1999.");
                return false;
            }
            bool module = SimpleMotion.IsModuleAddress(a);
            if (module != area.IsModule)
            {
                error = LanguageManager.GetString("Msg_BakModuleToModule", "A module's data to a module's buffer memory only (U1\\G...), the PLC's devices to the PLC's.");
                return false;
            }
            if (!module)
            {
                var k = KindOf(pa);
                if (k == null) { error = string.Format(LanguageManager.GetString("Msg_BakUnknownDevice", "{0} is not one of the devices written back."), pa); return false; }
                if (!k.Writable) { error = LanguageManager.GetString("Msg_BakNotWritable", "Inputs and outputs are not written back."); return false; }
                if (k.Bits != area.Bits)
                {
                    error = area.Bits ? LanguageManager.GetString("Msg_BakBitsToBits", "Bits are written to bit devices (M, L, B, F, S).")
                                      : LanguageManager.GetString("Msg_BakWordsToWords", "Words are written to word devices (D, R, W, TN, CN ...).");
                    return false;
                }
            }
            if (b != null)
            {
                string pb; long nb;
                if (!PlcAddressHelper.TrySplit(b, out pb, out nb) || !string.Equals(pa, pb, StringComparison.OrdinalIgnoreCase) || nb - na + 1 != count)
                {
                    error = string.Format(LanguageManager.GetString("Msg_BakTargetCount", "As many devices as read: {0}."), count);
                    return false;
                }
            }
            first = PlcAddressHelper.Compose(pa, na);
            return true;
        }

        /// <summary>What a write did.</summary>
        public sealed class WriteResult
        {
            public long Points;
            public List<string> Failed = new List<string>();
            public bool Cancelled;
            public bool Ok { get { return Failed.Count == 0 && !Cancelled; } }
        }

        /// <summary>
        /// Writes the parts: a word part in blocks (a module area's named words only); a bit part's whole
        /// words in blocks and its last bits one by one. Logged as the writer's - one line a block.
        /// </summary>
        public static WriteResult Write(IPlcBlockAccess block, IPlcDriver driver, IList<WriteRange> parts, Progress progress)
        {
            var result = new WriteResult();
            long total = parts.Sum(p => (long)(p.Source.Bits ? (p.Count + 15) / 16 : p.Count)), done = 0;
            foreach (var part in parts)
            {
                if (progress != null && !progress(done, total, string.Format(LanguageManager.GetString("Msg_BakWriting", "Writing {0}..."), part)))
                {
                    result.Cancelled = true;
                    break;
                }
                string prefix; long target;
                if (!PlcAddressHelper.TrySplit(part.Target, out prefix, out target)) { result.Failed.Add(part + ": ?"); continue; }
                var a = part.Source;
                int rc = 0;
                if (!a.Bits)
                {
                    // the runs to write: the area's named words (all of them for most areas) within the part
                    var runs = (a.Pieces ?? new List<int[]> { new[] { 0, a.Points } })
                        .Select(p => new[] { Math.Max(p[0], part.From), Math.Min(p[0] + p[1], part.From + part.Count) })
                        .Where(r => r[1] > r[0]).ToList();
                    foreach (var r in runs)
                    {
                        for (int at = r[0]; at < r[1] && rc == 0; at += Chunk)
                        {
                            int n = Math.Min(Chunk, r[1] - at);
                            var words = new short[n];
                            Array.Copy(a.Words, at, words, 0, n);
                            rc = block.WriteBlock(PlcAddressHelper.Compose(prefix, target + (at - part.From)), words);
                            System.Threading.Thread.Sleep(2);
                        }
                        if (rc != 0) break;
                    }
                    done += part.Count;
                }
                else
                {
                    int whole = part.Count / 16;
                    for (int w = 0; w < whole && rc == 0; w += Chunk)
                    {
                        int n = Math.Min(Chunk, whole - w);
                        var words = new short[n];
                        for (int i = 0; i < n; i++)
                        {
                            int v = 0;
                            for (int bit = 0; bit < 16; bit++) if (a.Value(part.From + 16L * (w + i) + bit) != 0) v |= 1 << bit;
                            words[i] = (short)v;
                        }
                        rc = block.WriteBlock(PlcAddressHelper.Compose(prefix, target + 16L * w), words);
                        System.Threading.Thread.Sleep(2);
                    }
                    // the bits after the last whole word, one by one: a word would change the devices after the part
                    for (int i = whole * 16; i < part.Count && rc == 0; i++)
                        rc = driver.Write(PlcAddressHelper.Compose(prefix, target + i), a.Value(part.From + i));
                    done += (part.Count + 15) / 16;
                }
                if (rc != 0) result.Failed.Add(part + " (" + (rc == -1 ? "refused" : "0x" + rc.ToString("X8")) + ")");
                else result.Points += part.Count;
            }
            if (progress != null) progress(total, total, "");
            return result;
        }
    }
}
