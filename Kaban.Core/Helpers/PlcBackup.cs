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
    /// WHAT IS READ: every device of the CPU (23 October: "I need all the device data of the moment"
    /// - an FX5's X, Y, M, L, B, F, SB, S, the timers' and counters' contacts, coils and current
    /// values, the long counters, D, W, SW, R, Z, LZ, SM, SD), each as far as the CPU's parameters
    /// give it - a real CPU refuses a device beyond them, so each size is found by asking (the
    /// simulator answers anything: it takes GX Works3's defaults). And each simple motion module
    /// of the PLC (its axes on the Axis Settings pages): what the module keeps in its flash ROM -
    /// the parameters, positioning data No.1 - 100 with their interpolation axes, block start data,
    /// servo parameters, the PLC CPU memo area, the mark detection settings - from the buffer memory
    /// list of the FX5 Simple Motion Module manual (Application, 10.2 and 10.9) - and its synchronous
    /// control parameters (ModuleAreas).
    ///
    /// THE FILE: text, one header line a fact, then one [area] after another, its words 16 a line
    /// (decimal; a bit area's in hexadecimal, 16 bits a word, the first device the lowest bit). In
    /// ProgramData\KabanHMI\PlcBackups, or wherever it was copied to.
    ///
    /// WHAT IS WRITTEN BACK: the areas (or parts of them) ticked, each to its own devices or - typed
    /// beside it - to others of the same kind (bits to bits, words to words, 32-bit to 32-bit). Never
    /// to a real PLC's inputs and outputs (X is its own reading of its inputs, Y would switch outputs),
    /// the contacts and coils (they follow their current values) or the system's devices; to the
    /// simulator every one (23 October: "write inputs and outputs as well if the selected PLC driver
    /// is simulator so we could make the simulator more useful").
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
            /// <summary>2 for a 32-bit device (LCN, LZ): two words a device, the low one first.</summary>
            public int WordsPerPoint = 1;
            public string TitleKey = "", Title = "";
            /// <summary>
            /// Other names of the device, tried when the CPU does not answer the first (23 October): MX Component's
            /// list names the retentive timers SS, SC, SN where GX Works3 says STS, STC, STN.
            /// </summary>
            public string[] Aliases = new string[0];
        }

        private static DeviceKind K(string prefix, bool bits, int dflt, int max, bool writable, string title, int wordsPerPoint = 1, params string[] aliases)
        {
            return new DeviceKind { Prefix = prefix, Bits = bits, Default = dflt, Max = max, Writable = writable, TitleKey = "Bak_Dev" + prefix,
                                    Title = title, WordsPerPoint = wordsPerPoint, Aliases = aliases ?? new string[0] };
        }

        /// <summary>
        /// A MELSEC iQ-F FX5 CPU's devices, its default sizes (GX Works3) and the most its parameters
        /// can give - what is read, in this order: every one (23 October: "I need all the device data of
        /// the moment") - the timers' and counters' contacts and coils, the long counters, the index
        /// registers and the system's SM, SD, SB, SW too. Not written back to a real PLC: the inputs and
        /// outputs, the contacts and coils (they follow the current values), the system's devices.
        /// </summary>
        public static readonly IList<DeviceKind> Fx5 = new List<DeviceKind>
        {
            K("X", true, 1024, 1024, false, "Inputs (X)"),
            K("Y", true, 1024, 1024, false, "Outputs (Y)"),
            K("M", true, 7680, 32768, true, "Internal relays (M)"),
            K("L", true, 7680, 32768, true, "Latch relays (L)"),
            K("B", true, 256, 32768, true, "Link relays (B)"),
            K("F", true, 128, 32768, true, "Annunciators (F)"),
            K("SB", true, 512, 32768, false, "Link special relays (SB)"),
            K("S", true, 4096, 4096, true, "Step relays (S)"),
            K("TS", true, 512, 1024, false, "Timers, contacts (T)"),
            K("TC", true, 512, 1024, false, "Timers, coils (T)"),
            K("TN", false, 512, 1024, true, "Timers, current values (T)"),
            K("STS", true, 16, 1024, false, "Retentive timers, contacts (ST)", 1, "SS"),
            K("STC", true, 16, 1024, false, "Retentive timers, coils (ST)", 1, "SC"),
            K("STN", false, 16, 1024, true, "Retentive timers, current values (ST)", 1, "SN"),
            K("CS", true, 256, 1024, false, "Counters, contacts (C)"),
            K("CC", true, 256, 1024, false, "Counters, coils (C)"),
            K("CN", false, 256, 1024, true, "Counters, current values (C)"),
            K("LCS", true, 64, 1024, false, "Long counters, contacts (LC)"),
            K("LCC", true, 64, 1024, false, "Long counters, coils (LC)"),
            K("LCN", false, 64, 1024, true, "Long counters, current values (LC)", 2),
            K("D", false, 8000, 65536, true, "Data registers (D)"),
            K("W", false, 512, 32768, true, "Link registers (W)"),
            K("SW", false, 512, 32768, false, "Link special registers (SW)"),
            K("R", false, 32768, 32768, true, "File registers (R)"),
            K("Z", false, 20, 24, true, "Index registers (Z)"),
            K("LZ", false, 2, 12, true, "Long index registers (LZ)", 2),
            K("SM", true, 10000, 10000, false, "Special relays (SM)"),
            K("SD", false, 12000, 12000, false, "Special registers (SD)")
        }.AsReadOnly();

        /// <summary>The kind of a device's letters ("D", or another name of it: "SN"), or null (a module's buffer memory, a device not read).</summary>
        public static DeviceKind KindOf(string prefix)
        {
            return Fx5.FirstOrDefault(k => string.Equals(k.Prefix, prefix, StringComparison.OrdinalIgnoreCase) ||
                                           k.Aliases.Any(a => string.Equals(a, prefix, StringComparison.OrdinalIgnoreCase)));
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
            /// <summary>
            /// A CAM (23 October): its number (0 - no cam), its data format (1 stroke ratio, 2 coordinate), its
            /// resolution (stroke ratio) or coordinate number, its starting point - its <see cref="Words"/> the
            /// cam data as the module's cam data operation gives them ([Cd.607]: a stroke ratio point's two
            /// words from point 1 on, a coordinate's four from point 0 on). <see cref="First"/> is the module's
            /// [Cd.607] (U1\G45008): the cam is read and written through the cam data operation, never as a block.
            /// </summary>
            public int Cam, CamFormat, CamResolution, CamStart;
            public bool IsCam { get { return Cam > 0; } }
            /// <summary>2 for a 32-bit device's area (LCN, LZ): two words a device, the low one first.</summary>
            public int WordsPerPoint = 1;
            /// <summary>The words read: a word area's one a device (a 32-bit one's two), a bit area's 16 devices a word.</summary>
            public short[] Words = new short[0];

            public string Prefix { get { string p; long n; PlcAddressHelper.TrySplit(First, out p, out n); return p; } }
            public long Start { get { string p; long n; PlcAddressHelper.TrySplit(First, out p, out n); return n; } }
            /// <summary>Its device <paramref name="i"/> (0: the first).</summary>
            public string Device(long i) { return PlcAddressHelper.Compose(Prefix, Start + i); }
            public string Last { get { return Device(Math.Max(0, Points - 1)); } }
            /// <summary>"D0 - D7999".</summary>
            public string Range
            {
                get
                {
                    if (IsCam) return string.Format(LanguageManager.GetString("Bak_CamRange", "cam No.{0}"), Cam);
                    return Points > 1 ? First + " - " + Last : First;
                }
            }
            public bool IsModule { get { return SimpleMotion.IsModuleAddress(First); } }
            /// <summary>
            /// Written back to a real PLC: not the inputs and outputs, the contacts and coils, the system's
            /// devices.
            /// </summary>
            public bool Writable { get { return WhyNotWritten == null; } }

            /// <summary>Why it is not written back to a real PLC (null: it is).</summary>
            public string WhyNotWritten
            {
                get
                {
                    if (IsModule) return null;
                    var k = KindOf(Prefix);
                    if (k == null || k.Writable) return null;
                    if (k.Prefix == "X" || k.Prefix == "Y") return LanguageManager.GetString("Msg_BakNotWritable", "Inputs and outputs are not written back.");
                    if (k.Prefix == "SM" || k.Prefix == "SD" || k.Prefix == "SB" || k.Prefix == "SW")
                        return LanguageManager.GetString("Msg_BakSystemNotWritten", "The system's devices are not written back.");
                    return LanguageManager.GetString("Msg_BakContactsNotWritten", "Contacts and coils follow their current values - not written back.");
                }
            }

            public int WordCount { get { return Bits ? (Points + 15) / 16 : Points * WordsPerPoint; } }

            /// <summary>Device <paramref name="i"/>'s value: a bit's 0 / 1, a word's as read (signed), a 32-bit one's of its two words.</summary>
            public int Value(long i)
            {
                if (WordsPerPoint == 2) return 2 * i + 1 < Words.Length ? (ushort)Words[2 * i] | (Words[2 * i + 1] << 16) : 0;
                if (!Bits) return i < Words.Length ? Words[i] : 0;
                long w = i / 16;
                return w < Words.Length ? (Words[w] >> (int)(i % 16)) & 1 : 0;
            }
        }

        public sealed class Backup
        {
            public string Machine = "", Plc = "", Cpu = "", Serial = "", User = "", Description = "";
            /// <summary>The cam numbers typed in the Read window ("1 - 10, 35"), offered again for the PLC's next Read (23 October).</summary>
            public string CamsTyped = "";
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
        /// THE CONNECTION A READ OR A WRITE GOES THROUGH (23 October). Its block calls, and what a failed one
        /// means: made while the link is up, the controller's answer (a device it does not have); made while
        /// the link is down, nothing - the link is waited for (asked to connect again, up to
        /// <see cref="ReconnectWaitMs"/>) and the call made again. Not back in time: <see cref="Lost"/>, and
        /// the work stops. The first backup made on GX Simulator3 held none of the CPU's devices: the link had
        /// gone down while they were read (the reads had kept the screen's own from the driver's thread - see
        /// StaWorker.TryRunPolite), and every one of them was taken for refused.
        /// </summary>
        public sealed class Link
        {
            public readonly IPlcDriver Driver;
            public readonly IPlcBlockAccess Block;
            /// <summary>How long a lost link is waited for.</summary>
            public int ReconnectWaitMs = 15000;
            /// <summary>The link went down and did not come back in time.</summary>
            public bool Lost { get; private set; }

            public Link(IPlcDriver driver, IPlcBlockAccess block)
            {
                Driver = driver;
                Block = block ?? driver as IPlcBlockAccess;
            }

            public bool RefusesOutOfRange { get { return Block != null && Block.RefusesOutOfRange; } }

            public bool Connected
            {
                get { try { return Driver != null && Driver.IsConnected; } catch { return false; } }
            }

            /// <summary>
            /// <paramref name="count"/> words from <paramref name="first"/>. Refused while connected: asked once more
            /// after a moment (a block waits its turn behind the screen's reads, and can miss it) - unless
            /// <paramref name="once"/> (a size asked: refusals are expected).
            /// </summary>
            public bool TryRead(string first, int count, out short[] values, bool once = false)
            {
                values = null;
                if (Block == null) return false;
                for (int attempt = 0; attempt < 6 && !Lost; attempt++)
                {
                    short[] got;
                    bool ok;
                    try { ok = Block.TryReadBlock(first, count, out got); } catch { ok = false; got = null; }
                    if (ok && got != null && got.Length == count) { values = got; return true; }
                    if (Connected)
                    {
                        if (once || attempt > 0) return false;
                        System.Threading.Thread.Sleep(100);
                        continue;
                    }
                    if (!WaitForLink()) return false;
                }
                return false;
            }

            /// <summary>Words written from <paramref name="first"/>: 0, or the refusal; a link lost meanwhile is waited for and the words written again.</summary>
            public int Write(string first, short[] values)
            {
                int rc = -1;
                if (Block == null) return rc;
                for (int attempt = 0; attempt < 4 && !Lost; attempt++)
                {
                    try { rc = Block.WriteBlock(first, values); } catch { rc = -1; }
                    if (rc == 0 || Connected) return rc;
                    if (!WaitForLink()) return rc;
                }
                return rc;
            }

            /// <summary>One device written (a bit after a bit area's last whole word), as <see cref="Write(string, short[])"/>.</summary>
            public int WriteOne(string device, int value)
            {
                int rc = -1;
                for (int attempt = 0; attempt < 4 && !Lost; attempt++)
                {
                    try { rc = Driver.Write(device, value); } catch { rc = -1; }
                    if (rc == 0 || Connected) return rc;
                    if (!WaitForLink()) return rc;
                }
                return rc;
            }

            /// <summary>One device read (a module's control word), as <see cref="WriteOne"/>.</summary>
            public bool ReadOne(string device, out int value)
            {
                value = 0;
                for (int attempt = 0; attempt < 4 && !Lost; attempt++)
                {
                    bool ok;
                    try { ok = Driver.TryRead(device, out value); } catch { ok = false; }
                    if (ok) return true;
                    if (Connected) return false;
                    if (!WaitForLink()) return false;
                }
                return false;
            }

            /// <summary>Waits for the link (asking it to connect); false - and <see cref="Lost"/> - when it is not back in time.</summary>
            public bool WaitForLink()
            {
                int deadline = Environment.TickCount + ReconnectWaitMs;
                while (!Connected)
                {
                    if (Environment.TickCount - deadline >= 0) { Lost = true; return false; }
                    try { Driver.Connect(); } catch { }
                    if (Connected) break;
                    System.Threading.Thread.Sleep(200);
                }
                return true;
            }
        }

        /// <summary>
        /// The CPU's devices to read, each as far as the CPU has it: asked with block calls when the driver's
        /// controller refuses a device beyond its range, else GX Works3's defaults. A bit device's count is whole
        /// words (16). A device the CPU does not answer at all is named in <paramref name="notAnswered"/> (it was
        /// left out without a word). Null: cancelled, or the link lost (<see cref="Link.Lost"/>).
        /// </summary>
        public static List<Area> PlanDevices(Link link, List<string> notAnswered, Progress progress = null)
        {
            var list = new List<Area>();
            bool ask = link.RefusesOutOfRange;
            int i = 0;
            foreach (var k in Fx5)
            {
                string title = LanguageManager.GetString(k.TitleKey, k.Title);
                if (progress != null && ask &&
                    !progress(i++, Fx5.Count, string.Format(LanguageManager.GetString("Msg_BakAsking", "Asking the CPU how many it has: {0}..."), title)))
                    return null;
                string name = k.Prefix;
                int n = k.Default;
                if (ask)
                {
                    n = Probe(link, k, out name);
                    if (n < 0) return null;
                }
                if (k.Bits) n -= n % 16;
                if (n <= 0)
                {
                    if (notAnswered != null) notAnswered.Add(title + " - " + LanguageManager.GetString("Msg_BakNotAnswered", "the CPU does not answer it"));
                    continue;
                }
                list.Add(new Area { First = name + "0", Points = n, Bits = k.Bits, WordsPerPoint = k.WordsPerPoint, Title = title });
            }
            return list;
        }

        /// <summary>For the tests and the simple callers: the driver both ways.</summary>
        public static List<Area> PlanDevices(IPlcDriver driver, IPlcBlockAccess block)
        {
            return PlanDevices(new Link(driver, block), null);
        }

        /// <summary>
        /// How many of a device the CPU has - with block calls of its last word (a bit device's last whole word, a
        /// 32-bit one's two words), never a single device's read: a refused GetDevice counts against MX Component's
        /// link (IsHealthy). Its name the CPU answers (<paramref name="name"/>: the first, or another of the kind's).
        /// GX Works3's default first - most CPUs keep it: three questions then. 0: none answered; -1: the link lost.
        /// </summary>
        public static int Probe(Link link, DeviceKind k, out string name)
        {
            name = k.Prefix;
            int unit = k.Bits ? 16 : 1;
            Func<string, long, bool> has = (nm, n) =>
            {
                short[] v;
                return n >= unit && link.TryRead(PlcAddressHelper.Compose(nm, n - unit), k.WordsPerPoint, out v, once: true);
            };
            string found = null;
            foreach (var nm in new[] { k.Prefix }.Concat(k.Aliases))
            {
                if (has(nm, unit)) { found = nm; break; }
                if (link.Lost) return -1;
            }
            if (found == null) return 0;
            name = found;
            Func<long, bool> h = n => has(found, n);
            long lo, hi;   // lo: answered, hi: not - multiples of the unit
            long d = k.Default;
            if (h(d))
            {
                if (k.Max <= d || !h(d + unit)) return link.Lost ? -1 : (int)d;
                if (h(k.Max)) return link.Lost ? -1 : k.Max;
                lo = d + unit; hi = k.Max;
            }
            else { lo = unit; hi = d; }
            while (hi - lo > unit)
            {
                long mid = (lo + hi) / 2 / unit * unit;
                if (mid <= lo) mid = lo + unit;
                if (h(mid)) lo = mid; else hi = mid;
            }
            return link.Lost ? -1 : (int)lo;
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
        /// SYNCHRONOUS CONTROL (23 October: "I need all the device data of the moment"), from the FX5 Simple
        /// Motion Module User's Manual (Advanced Synchronous Control), IB-0300255, Appendix 1: the servo input
        /// axis parameters [Pr.300] - [Pr.304], 32800 - 32805 + 10n; the synchronous encoder axis parameters
        /// [Pr.320] - [Pr.329], 34720 - 34735 + 20j (j: encoder axis - 1, 4 of them); the synchronous
        /// parameters [Pr.400] - [Pr.468] (main and auxiliary shaft, clutches, speed change gear, output axis,
        /// initial position), 36400 - 36513 + 200n - the words the manual names written back (KnownPieces).
        /// The command generation axis parameters are not in the buffer memory (the manual), and the cams are
        /// read through the module's cam data operation ([Cd.600]) - neither is in a backup.
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
                list.Add(M(module, 32800 + 10 * n, 6, ax + t("Bak_ModServoInput", "synchronous control: servo input axis parameters (Pr.300 - Pr.304)")));
                list.Add(M(module, 36400 + 200 * n, SynchronousWords, ax + t("Bak_ModSynchronous", "synchronous control: synchronous parameters (Pr.400 - Pr.468)")));
            }
            for (int j = 1; j <= SynchronousEncoderAxes; j++)
                list.Add(M(module, 34720 + 20 * (j - 1), EncoderWords, mod + ", " + string.Format(t("Bak_EncoderAxis", "synchronous encoder axis {0}"), j) + ": " +
                                                                       t("Bak_ModEncoder", "parameters (Pr.320 - Pr.329)")));
            foreach (var a in list) if (a.Pieces == null) a.Pieces = KnownPieces(a.First, a.Points);
            list.Add(M(module, 30000, 100, mod + ": " + t("Bak_ModMemo", "PLC CPU memo area")));
            list.Add(M(module, 54000, 320, mod + ": " + t("Bak_ModMark", "mark detection settings")));
            return list;
        }

        // ---- a module's data in groups ----------------------------------------------------------------------
        //
        // 23 October: "It could be useful to group the write selection so the user could select them multiple and
        // easier (the attached image show GX Works way for it)" - GX Works3's Write to the module: positioning data,
        // block start data, parameters, servo parameters, mark detection, synchronous control parameters, each
        // ticked whole or for the axes (and positioning data numbers) chosen in its details.

        /// <summary>A simple motion module's data in the groups GX Works3 writes it by (and the PLC CPU memo area).</summary>
        public enum ModuleGroup { Other, Positioning, BlockStart, Parameters, Servo, MarkDetection, Synchronous, Memo, Cam }

        /// <summary>The groups in GX Works3's order.</summary>
        public static readonly ModuleGroup[] ModuleGroups =
        {
            ModuleGroup.Positioning, ModuleGroup.BlockStart, ModuleGroup.Parameters, ModuleGroup.Servo,
            ModuleGroup.MarkDetection, ModuleGroup.Synchronous, ModuleGroup.Cam, ModuleGroup.Memo
        };

        /// <summary>Where a module's area belongs.</summary>
        public sealed class ModulePlace
        {
            public int Module;
            public ModuleGroup Group;
            /// <summary>Its axis (1 - 8); 0 - the module's own (common parameters, memo, mark detection), or an encoder axis's.</summary>
            public int Axis;
            /// <summary>Its synchronous encoder axis (1 - 4), else 0.</summary>
            public int Encoder;
            /// <summary>The positioning data's axes to be interpolated (71000 + 1000n): written with the positioning data.</summary>
            public bool Interpolation;
            /// <summary>A cam's number (the cam data group), else 0.</summary>
            public int Cam;
        }

        /// <summary>Where an area belongs: by its first address - a cam to the cam data group.</summary>
        public static ModulePlace PlaceOf(Area a)
        {
            var place = a == null ? null : PlaceOf(a.First);
            if (place != null && a.IsCam) { place.Group = ModuleGroup.Cam; place.Cam = a.Cam; place.Axis = 0; }
            return place;
        }

        /// <summary>A positioning data's words (Da.1 - Da.10), and the data an axis keeps in the buffer memory (No.1 - 100).</summary>
        public const int PositioningWords = 10, PositioningData = 100;

        /// <summary>Where a module's area belongs, by its first address; null for a CPU device.</summary>
        public static ModulePlace PlaceOf(string first)
        {
            string p; long g;
            if (!SimpleMotion.IsModuleAddress(first) || !PlcAddressHelper.TrySplit(first, out p, out g)) return null;
            int module;
            int.TryParse(p.Substring(1, p.Length - 3), NumberStyles.None, CultureInfo.InvariantCulture, out module);
            var place = new ModulePlace { Module = module, Group = ModuleGroup.Other };
            int axes = SimpleMotion.MaxAxes;
            Func<long, long, int, int> on = (start, step, count) => g >= start && g < start + step * count && (g - start) % step == 0 ? (int)((g - start) / step) + 1 : 0;
            int n;
            if (g == 33 || g == 58000) place.Group = ModuleGroup.Parameters;
            else if ((n = on(0, 150, axes)) > 0) { place.Group = ModuleGroup.Parameters; place.Axis = n; }
            else if ((n = on(6000, 1000, axes)) > 0) { place.Group = ModuleGroup.Positioning; place.Axis = n; }
            else if ((n = on(71000, 1000, axes)) > 0) { place.Group = ModuleGroup.Positioning; place.Axis = n; place.Interpolation = true; }
            else if ((n = on(22000, 400, axes)) > 0) { place.Group = ModuleGroup.BlockStart; place.Axis = n; }
            else if ((n = Math.Max(on(28400, 100, axes), Math.Max(on(64400, 70, axes), on(65520, 340, axes)))) > 0) { place.Group = ModuleGroup.Servo; place.Axis = n; }
            else if ((n = Math.Max(on(32800, 10, axes), on(36400, 200, axes))) > 0) { place.Group = ModuleGroup.Synchronous; place.Axis = n; }
            else if ((n = on(34720, 20, SynchronousEncoderAxes)) > 0) { place.Group = ModuleGroup.Synchronous; place.Encoder = n; }
            else if (g == 30000) place.Group = ModuleGroup.Memo;
            else if (g == 54000) place.Group = ModuleGroup.MarkDetection;
            return place;
        }

        /// <summary>A group's name, as GX Works3 has it.</summary>
        public static string GroupTitle(ModuleGroup group)
        {
            switch (group)
            {
                case ModuleGroup.Positioning: return LanguageManager.GetString("Bak_GrpPositioning", "Positioning data");
                case ModuleGroup.BlockStart: return LanguageManager.GetString("Bak_GrpBlockStart", "Block start data");
                case ModuleGroup.Parameters: return LanguageManager.GetString("Bak_GrpParameters", "Parameters");
                case ModuleGroup.Servo: return LanguageManager.GetString("Bak_GrpServo", "Servo parameters");
                case ModuleGroup.MarkDetection: return LanguageManager.GetString("Bak_GrpMark", "Mark detection");
                case ModuleGroup.Synchronous: return LanguageManager.GetString("Bak_GrpSynchronous", "Synchronous control parameters");
                case ModuleGroup.Memo: return LanguageManager.GetString("Bak_GrpMemo", "PLC CPU memo area");
                case ModuleGroup.Cam: return LanguageManager.GetString("Bak_GrpCam", "Cam data");
                default: return LanguageManager.GetString("Bak_GrpOther", "Other data");
            }
        }

        /// <summary>A module's address in another module: "U1\G6000", 2 - "U2\G6000".</summary>
        public static string InModule(string address, int module)
        {
            string p; long g;
            if (!PlcAddressHelper.TrySplit(address, out p, out g)) return address;
            return SimpleMotion.Device(module, (int)g);
        }

        /// <summary>A module's synchronous encoder axes (801 - 804 as an input axis: the manual).</summary>
        public const int SynchronousEncoderAxes = 4;
        /// <summary>An axis's synchronous parameters' words: 36400 - 36513 + 200n.</summary>
        public const int SynchronousWords = 114;
        /// <summary>A synchronous encoder axis's parameters' words: 34720 - 34735 + 20j.</summary>
        public const int EncoderWords = 16;

        /// <summary>
        /// The words of a module's area that are written back, where the manuals leave some unnamed (maker
        /// settings, the FX5-SSC-G's own): (offset, words) runs; null - every word. By the area's first address,
        /// so a backup made before an area got them is written back the same way (Load). The synchronous ones
        /// from the Advanced Synchronous Control manual's Appendix 1 (23 October): the encoder axis's 34729 is
        /// unnamed; of the synchronous parameters 36403, 36424 - 36429, 36452 - 36459, 36466 - 36469, 36471
        /// (the FX5-SSC-G's [Pr.442]), 36475, 36478 - 36481, 36487 - 36499 and 36505.
        /// </summary>
        public static List<int[]> KnownPieces(string first, int points)
        {
            string p; long g;
            if (!SimpleMotion.IsModuleAddress(first) || !PlcAddressHelper.TrySplit(first, out p, out g)) return null;
            int[][] runs = null;
            if (g == 33) runs = new[] { new[] { 0, 1 }, new[] { 2, 1 }, new[] { 34, 1 }, new[] { 72, 2 } };
            else if (g < 150 * SimpleMotion.MaxAxes && g % 150 == 0) runs = new[] { new[] { 0, 16 }, new[] { 17, 75 }, new[] { 100, 50 } };
            else if (g >= 71000 && g < 71000 + 1000 * SimpleMotion.MaxAxes && g % 1000 == 0)
                runs = Enumerable.Range(0, 100).Select(k => new[] { 10 * k, 2 }).ToArray();
            else if (g >= 34720 && g < 34720 + 20 * SynchronousEncoderAxes && (g - 34720) % 20 == 0)
                runs = new[] { new[] { 0, 9 }, new[] { 10, 6 } };
            else if (g >= 36400 && g < 36400 + 200 * SimpleMotion.MaxAxes && (g - 36400) % 200 == 0)
                runs = new[]
                {
                    new[] { 0, 3 }, new[] { 4, 20 },          // main shaft [Pr.400] - [Pr.414]
                    new[] { 30, 22 },                         // auxiliary shaft [Pr.418] - [Pr.431]
                    new[] { 60, 6 },                          // speed change gear [Pr.434] - [Pr.437]
                    new[] { 70, 1 }, new[] { 72, 3 }, new[] { 76, 2 }, new[] { 82, 5 },   // output axis [Pr.438] - [Pr.447]
                    new[] { 100, 5 }, new[] { 106, 8 }        // initial position [Pr.460] - [Pr.468]
                };
            if (runs == null) return null;
            return runs.Where(r => r[0] < points).Select(r => new[] { r[0], Math.Min(r[1], points - r[0]) }).ToList();
        }

        /// <summary>
        /// Reads <paramref name="areas"/> into <paramref name="backup"/>, <see cref="Chunk"/> words a
        /// call: an area the PLC refuses is left out and named in <see cref="Backup.Skipped"/>. False
        /// when cancelled or the link lost (<see cref="Link.Lost"/>) - the backup then holds what was
        /// read so far, not to be saved.
        /// </summary>
        public static bool ReadAreas(Link link, Backup backup, IList<Area> areas, Progress progress)
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
                    string first = area.Bits ? area.Device(16L * at) : area.Device(at / area.WordsPerPoint);
                    ok = link.TryRead(first, n, out got);
                    if (ok) Array.Copy(got, 0, words, at, n);
                    done += n;
                }
                if (link.Lost) return false;
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

        /// <summary>For the tests and the simple callers: a driver that is its own block access.</summary>
        public static bool ReadAreas(IPlcBlockAccess block, Backup backup, IList<Area> areas, Progress progress)
        {
            return ReadAreas(new Link(block as IPlcDriver, block), backup, areas, progress);
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
            if (!string.IsNullOrWhiteSpace(b.CamsTyped)) sb.AppendLine("Cams;" + Line(b.CamsTyped.Trim()).Replace(";", ","));
            if (b.Skipped.Count > 0) sb.AppendLine("Skipped;" + Line(string.Join(" | ", b.Skipped)));
            foreach (var a in b.Areas)
            {
                sb.Append('[').Append(a.First).Append(';').Append(a.Points.ToString(CultureInfo.InvariantCulture)).Append(';')
                  .Append(a.Bits ? "Bits" : a.WordsPerPoint == 2 ? "DoubleWords" : "Words").Append(';').Append(Line(a.Title).Replace(";", ",").Replace("]", ")"));
                if (a.Pieces != null || a.IsCam) sb.Append(';').Append(a.Pieces == null ? "" : string.Join(" ", a.Pieces.Select(p => p[0] + "+" + p[1])));
                // a cam: its number, data format, resolution or coordinate number, starting point
                if (a.IsCam) sb.Append(";cam=").Append(string.Join(",", new[] { a.Cam, a.CamFormat, a.CamResolution, a.CamStart }.Select(x => x.ToString(CultureInfo.InvariantCulture))));
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
                    if (line.Length == 0 || line.StartsWith("#", System.StringComparison.Ordinal)) continue;
                    if (line.StartsWith("[", System.StringComparison.Ordinal))
                    {
                        if (headerOnly) break;
                        close();
                        var f = line.Trim('[', ']').Split(';');
                        if (f.Length < 4) return null;
                        area = new Area
                        {
                            First = f[0].Trim(), Points = int.Parse(f[1], CultureInfo.InvariantCulture),
                            Bits = f[2].Trim() == "Bits", WordsPerPoint = f[2].Trim() == "DoubleWords" ? 2 : 1, Title = Unline(f[3])
                        };
                        if (f.Length > 4 && f[4].Trim().Length > 0)
                            area.Pieces = f[4].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(p => p.Split('+').Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray()).ToList();
                        // a backup made before the area's unnamed words were known (the synchronous parameters, 23 October)
                        else area.Pieces = KnownPieces(area.First, area.Points);
                        if (f.Length > 5 && f[5].Trim().StartsWith("cam=", System.StringComparison.Ordinal))
                        {
                            var c = f[5].Trim().Substring(4).Split(',').Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                            area.Cam = c[0]; area.CamFormat = c[1]; area.CamResolution = c[2]; area.CamStart = c.Length > 3 ? c[3] : 0;
                            area.Pieces = null;
                        }
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
                        case "Cams": b.CamsTyped = value.Trim(); break;
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

        // ---- cams ---------------------------------------------------------------------------------------------
        //
        // 23 October ("GX Works also writes cam data ... Should I add cam data to the backup and the Write tree?" -
        // "Yes please"): a module's cams through its cam data operation (the FX5 Simple Motion Module manual
        // (Advanced Synchronous Control), 3.2): [Cd.601] the cam's number, [Cd.602] its first point, [Cd.603]
        // how many, then [Cd.600] = 1 reads (the cam open area), 2 writes (the cam storage area and the open area);
        // the module sets [Cd.600] back to 0 when done. A stroke ratio cam's points 1 - resolution, two words each,
        // 4096 at a time; a coordinate cam's points 0 - n-1, four words each, 2048 at a time - in [Cd.607]
        // (45008 - 53199). Its data format, resolution / coordinate number and starting point: [Cd.604] - [Cd.606].
        // A cam number with no cam is answered with warning 0C41H on the module's axis 1 - so the cams read are the
        // ones an axis uses ([Pr.440] Cam No.) and those typed in the Read window, never a search through 1 - 256.

        public const int CamRequest = 45000, CamNumber = 45001, CamFirst = 45002, CamPoints = 45003,
                         CamFormatWord = 45004, CamResolutionWord = 45005, CamStartWord = 45006, CamData = 45008;
        /// <summary>Points a cam data operation takes: stroke ratio, coordinate.</summary>
        public const int StrokeChunk = 4096, CoordinateChunk = 2048;
        /// <summary>The cam numbers ([Cd.601]: 1 - 256, the cam open area's).</summary>
        public const int MaxCam = 256;
        /// <summary>How long a cam data operation may take.</summary>
        public static int CamWaitMs = 5000;

        /// <summary>The cams the module's axes use: [Pr.440] Cam No. (36474 + 200n) in the backup's synchronous parameters.</summary>
        public static List<int> CamsInUse(Backup b, int module)
        {
            var list = new SortedSet<int>();
            foreach (var a in b.Areas)
            {
                var place = PlaceOf(a.First);
                if (a.IsCam || place == null || place.Module != module || place.Group != ModuleGroup.Synchronous || place.Axis == 0) continue;
                string p; long g;
                if (!PlcAddressHelper.TrySplit(a.First, out p, out g) || g < 36400) continue;
                int cam = a.Value(74);
                if (cam >= 1 && cam <= MaxCam) list.Add(cam);
            }
            return list.ToList();
        }

        /// <summary>"1 - 10, 35" as cam numbers (1 - 256); false when it is not.</summary>
        public static bool TryParseCams(string text, out List<int> cams)
        {
            cams = new List<int>();
            var t = Regex.Replace(text ?? "", @"\s*(-|\u2013|\.\.)\s*", "-");
            foreach (var bit in t.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var m = Regex.Match(bit, @"^(\d+)(?:-(\d+))?$");
                if (!m.Success) return false;
                int a = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                int z = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : a;
                if (a < 1 || z > MaxCam || z < a) return false;
                for (int i = a; i <= z; i++) if (!cams.Contains(i)) cams.Add(i);
            }
            cams.Sort();
            return true;
        }

        /// <summary>Waits until the module's cam data operation is idle ([Cd.600] 0).</summary>
        private static bool CamIdle(Link link, int module)
        {
            int deadline = Environment.TickCount + CamWaitMs;
            while (true)
            {
                int v;
                if (!link.ReadOne(SimpleMotion.Device(module, CamRequest), out v)) return false;
                if (v == 0) return true;
                if (Environment.TickCount - deadline >= 0) return false;
                System.Threading.Thread.Sleep(20);
            }
        }

        /// <summary>One cam data operation: its number, first point and points set, [Cd.600] = request, done when it is 0 again.</summary>
        private static bool CamOperation(Link link, int module, int request, int cam, int first, int points)
        {
            Func<int, string> g = a => SimpleMotion.Device(module, a);
            if (link.WriteOne(g(CamNumber), cam) != 0 || link.WriteOne(g(CamFirst), first) != 0 || link.WriteOne(g(CamPoints), points) != 0) return false;
            if (link.WriteOne(g(CamRequest), request) != 0) return false;
            return CamIdle(link, module);
        }

        /// <summary>
        /// Reads module <paramref name="module"/>'s cams <paramref name="cams"/> into <paramref name="backup"/>: an
        /// area each; a cam the module does not have (or will not give: a read password) named in Skipped. False
        /// when cancelled or the link lost.
        /// </summary>
        public static bool ReadCams(Link link, Backup backup, int module, IList<int> cams, Progress progress)
        {
            Func<int, string> g = a => SimpleMotion.Device(module, a);
            string mod = string.Format(LanguageManager.GetString("Bak_Module", "Simple motion module {0}"), module);
            int i = 0;
            foreach (int cam in cams)
            {
                string name = mod + ": " + string.Format(LanguageManager.GetString("Bak_CamRange", "cam No.{0}"), cam);
                if (progress != null && !progress(i++, cams.Count, string.Format(LanguageManager.GetString("Msg_BakReadingCam", "Reading {0}..."), name)))
                    return false;
                int format = 0, resolution = 0, start = 0;
                bool ok = CamIdle(link, module) &&
                          // its format and size first: the words cleared, then one point read (point 1 is in both formats)
                          link.WriteOne(g(CamFormatWord), 0) == 0 && link.WriteOne(g(CamResolutionWord), 0) == 0 &&
                          CamOperation(link, module, 1, cam, 1, 1) &&
                          link.ReadOne(g(CamFormatWord), out format) && link.ReadOne(g(CamResolutionWord), out resolution) &&
                          link.ReadOne(g(CamStartWord), out start);
                if (link.Lost) return false;
                if (!ok || (format != 1 && format != 2) || resolution <= 0)
                {
                    backup.Skipped.Add(name + " - " + LanguageManager.GetString("Msg_BakNoCam", "not in the module (or read-protected)"));
                    continue;
                }
                bool stroke = format == 1;
                int wpp = stroke ? 2 : 4, chunk = stroke ? StrokeChunk : CoordinateChunk;
                int firstPoint = stroke ? 1 : 0, points = resolution;
                var words = new short[points * wpp];
                for (int at = 0; at < points && ok; at += chunk)
                {
                    int n = Math.Min(chunk, points - at);
                    short[] got;
                    got = null;
                    ok = CamOperation(link, module, 1, cam, firstPoint + at, n) && link.TryRead(g(CamData), n * wpp, out got);
                    if (ok) Array.Copy(got, 0, words, at * wpp, n * wpp);
                }
                if (link.Lost) return false;
                if (!ok)
                {
                    backup.Skipped.Add(name + " (" + LanguageManager.GetString("Msg_BakCamFailed", "its data could not be read") + ")");
                    continue;
                }
                backup.Areas.Add(new Area
                {
                    First = g(CamData), Points = words.Length, Words = words, Cam = cam, CamFormat = format, CamResolution = resolution, CamStart = start,
                    Title = name + " (" + (stroke ? string.Format(LanguageManager.GetString("Bak_CamStroke", "stroke ratio data, resolution {0}"), resolution)
                                                  : string.Format(LanguageManager.GetString("Bak_CamCoordinate", "coordinate data, {0} points"), resolution)) + ")"
                });
            }
            return true;
        }

        /// <summary>
        /// A cam written into module <paramref name="module"/>'s cam storage area (kept at power off; its cam open
        /// area too), as many operations as it needs; null when done, else why not - a warning of the module's
        /// axis 1 (0C40H - 0C4BH: the storage area full, a write password ...).
        /// </summary>
        public static string WriteCam(Link link, Area cam, int module)
        {
            Func<int, string> g = a => SimpleMotion.Device(module, a);
            bool stroke = cam.CamFormat == 1;
            int wpp = stroke ? 2 : 4, chunk = stroke ? StrokeChunk : CoordinateChunk;
            int firstPoint = stroke ? 1 : 0, points = cam.Words.Length / wpp;
            int before;
            if (!CamIdle(link, module)) return LanguageManager.GetString("Msg_BakCamBusy", "the module's cam data operation did not answer");
            link.ReadOne(SimpleMotion.Device(module, SimpleMotion.AddressOf(SimpleMotion.Items.First(x => x.Key == "AxisWarning"), 1)), out before);
            if (link.WriteOne(g(CamFormatWord), cam.CamFormat) != 0 || link.WriteOne(g(CamResolutionWord), cam.CamResolution) != 0 ||
                (stroke && link.WriteOne(g(CamStartWord), cam.CamStart) != 0))
                return LanguageManager.GetString("Msg_BakRefused", "refused");
            for (int at = 0; at < points; at += chunk)
            {
                int n = Math.Min(chunk, points - at);
                var words = new short[n * wpp];
                Array.Copy(cam.Words, at * wpp, words, 0, words.Length);
                if (link.Write(g(CamData), words) != 0 || !CamOperation(link, module, 2, cam.Cam, firstPoint + at, n))
                    return LanguageManager.GetString("Msg_BakCamBusy", "the module's cam data operation did not answer");
                int warning;
                if (link.ReadOne(SimpleMotion.Device(module, SimpleMotion.AddressOf(SimpleMotion.Items.First(x => x.Key == "AxisWarning"), 1)), out warning) &&
                    warning != before && (warning & 0xFFF0) == 0x0C40)
                    return string.Format(LanguageManager.GetString("Msg_BakCamWarning", "the module's warning {0:X4}H"), warning);
            }
            return null;
        }

        // ---- one device of a backup (23 October: the PLC Monitor's labels written from a backup) -------------

        /// <summary>
        /// The backup's area a device is in, and its place there ("D105" in D0 - D7999: 105; "U1\G6003": its word
        /// in U1\G6000's area); null when the backup does not have it. A cam is never one.
        /// </summary>
        public static Area AreaOf(Backup b, string device, out int index)
        {
            index = -1;
            string p; long n;
            if (b == null || !PlcAddressHelper.TrySplit(device, out p, out n)) return null;
            foreach (var a in b.Areas)
            {
                if (a.IsCam) continue;
                string ap = a.Prefix;
                bool same = string.Equals(ap, p, StringComparison.OrdinalIgnoreCase);
                if (!same)
                {
                    // the same device by another name (STN / SN)
                    var k = KindOf(ap);
                    same = k != null && ReferenceEquals(k, KindOf(p));
                }
                if (!same || n < a.Start || n >= a.Start + a.Points) continue;
                int at = (int)(n - a.Start);
                // a module's word the manual does not name: not read, not in the backup
                if (a.Pieces != null && !a.Bits && !a.Pieces.Any(r => at * a.WordsPerPoint >= r[0] && at * a.WordsPerPoint < r[0] + r[1])) return null;
                index = at;
                return a;
            }
            return null;
        }

        /// <summary>
        /// The parts that write <paramref name="devices"/> back from <paramref name="b"/>, each to itself - devices
        /// next to each other in one part; <paramref name="missing"/>: those the backup does not have.
        /// </summary>
        public static List<WriteRange> PartsFor(Backup b, IEnumerable<string> devices, out List<string> missing)
        {
            missing = new List<string>();
            var found = new List<KeyValuePair<Area, int>>();
            foreach (var d in devices.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                int i;
                var a = AreaOf(b, d, out i);
                if (a == null) { missing.Add(d); continue; }
                if (!found.Any(f => ReferenceEquals(f.Key, a) && f.Value == i)) found.Add(new KeyValuePair<Area, int>(a, i));
            }
            var parts = new List<WriteRange>();
            foreach (var g in found.GroupBy(f => f.Key))
            {
                var at = g.Select(f => f.Value).OrderBy(x => x).ToList();
                for (int k = 0; k < at.Count; )
                {
                    int j = k;
                    while (j + 1 < at.Count && at[j + 1] == at[j] + 1) j++;
                    parts.Add(new WriteRange { Source = g.Key, From = at[k], Count = at[j] - at[k] + 1, Target = g.Key.Device(at[k]) });
                    k = j + 1;
                }
            }
            return parts;
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
                if (Source.IsCam)
                {
                    var into = PlaceOf(Target);
                    var from = PlaceOf(Source.First);
                    return Source.Range + (into != null && from != null && into.Module != from.Module ? " \u2192 U" + into.Module : "");
                }
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
        /// GX Works3's "Points + Start" (24 October: "the user would need to select two start address (both the
        /// source and target start address) and one data length"): the first device read, "R1000" (empty: the
        /// area's first), and how many, "1000" (empty: all from there to the area's end) - as devices of
        /// <paramref name="area"/>: its <paramref name="from"/> and <paramref name="count"/>.
        /// </summary>
        public static bool TryParseStartPoints(Area area, string start, string points, out int from, out int count, out string error)
        {
            from = 0; count = area.Points; error = "";
            start = (start ?? "").Trim();
            points = (points ?? "").Trim();
            if (start.Length > 0)
            {
                string p; long n;
                if (RangeRx.IsMatch(start) || !PlcAddressHelper.TrySplit(start, out p, out n))
                {
                    error = LanguageManager.GetString("Msg_BakNotDevice", "Not a device - write the first one read, as R1000.");
                    return false;
                }
                if (!string.Equals(p, area.Prefix, StringComparison.OrdinalIgnoreCase) || n < area.Start || n >= area.Start + area.Points)
                {
                    error = string.Format(LanguageManager.GetString("Msg_BakOutsideArea", "Within {0} only."), area.Range);
                    return false;
                }
                from = (int)(n - area.Start);
            }
            int left = area.Points - from;
            if (points.Length == 0) { count = left; return true; }
            // "1,000", "1.000", "1 000": a count has no fractions - every separator is a thousands' one
            var digits = Regex.Replace(points, @"[\s,.'\u00A0\u202F]", "");
            long c;
            if (!long.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out c) || c < 1 || c > left)
            {
                error = string.Format(LanguageManager.GetString("Msg_BakPointsWithin", "Points: 1 to {0} from {1}."), left.ToString("N0"), area.Device(from));
                return false;
            }
            count = (int)c;
            return true;
        }

        /// <summary>
        /// Where a part is written: empty - its own devices; "D2500", or "D2500 - D3499" (as many devices
        /// as read). Bits to bits, words to words, 32-bit ones to 32-bit ones; a module's data to a module's
        /// buffer memory. On a real PLC never to what is not written back (the inputs and outputs, the
        /// contacts and coils, the system's devices); <paramref name="simulator"/> (23 October: "write
        /// inputs and outputs as well if the selected PLC driver is simulator so we could make the
        /// simulator more useful"): every device - the simulator is no machine.
        /// </summary>
        public static bool TryParseTarget(Area area, int from, int count, string text, out string first, out string error, bool simulator = false)
        {
            first = area.Device(from); error = "";
            text = (text ?? "").Trim();
            if (text.Length == 0)
            {
                if (!simulator && !area.Writable) { error = area.WhyNotWritten; return false; }
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
                if (!simulator && !k.Writable)
                {
                    error = new Area { First = pa + "0" }.WhyNotWritten;
                    return false;
                }
                if (k.Bits != area.Bits)
                {
                    error = area.Bits ? LanguageManager.GetString("Msg_BakBitsToBits", "Bits are written to bit devices (M, L, B, F, S).")
                                      : LanguageManager.GetString("Msg_BakWordsToWords", "Words are written to word devices (D, R, W, TN, CN ...).");
                    return false;
                }
                if (k.WordsPerPoint != area.WordsPerPoint)
                {
                    error = area.WordsPerPoint == 2 ? LanguageManager.GetString("Msg_BakDoubleToDouble", "32-bit values are written to 32-bit devices (LCN, LZ).")
                                                    : LanguageManager.GetString("Msg_BakSingleToSingle", "16-bit words are written to 16-bit devices (D, R, W, TN, CN ...).");
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
            /// <summary>The link went down and did not come back: the parts after it were not written.</summary>
            public bool LinkLost;
            public bool Ok { get { return Failed.Count == 0 && !Cancelled && !LinkLost; } }
        }

        /// <summary>
        /// Writes the parts: a word part in blocks (a module area's named words only); a bit part's whole
        /// words in blocks and its last bits one by one. Logged as the writer's - one line a block. The link
        /// lost meanwhile is waited for and the block written again (the same words: harmless twice); not back
        /// in time, the write stops (<see cref="WriteResult.LinkLost"/>).
        /// </summary>
        public static WriteResult Write(IPlcBlockAccess block, IPlcDriver driver, IList<WriteRange> parts, Progress progress)
        {
            return Write(new Link(driver, block), parts, progress);
        }

        public static WriteResult Write(Link link, IList<WriteRange> parts, Progress progress)
        {
            var result = new WriteResult();
            long total = parts.Sum(p => (long)(p.Source.Bits ? (p.Count + 15) / 16 : p.Count * p.Source.WordsPerPoint)), done = 0;
            foreach (var part in parts)
            {
                if (progress != null && !progress(done, total, string.Format(LanguageManager.GetString("Msg_BakWriting", "Writing {0}..."), part)))
                {
                    result.Cancelled = true;
                    break;
                }
                string prefix; long target;
                if (!PlcAddressHelper.TrySplit(part.Target, out prefix, out target)) { result.Failed.Add(part + ": ?"); continue; }
                if (part.Source.IsCam)
                {
                    // a cam through the module's cam data operation, into its cam storage area
                    string why = WriteCam(link, part.Source, PlaceOf(part.Target).Module);
                    done += part.Count;
                    if (link.Lost) { result.LinkLost = true; result.Failed.Add(part + " (" + LanguageManager.GetString("Msg_BakLinkLostShort", "the connection was lost") + ")"); break; }
                    if (why != null) result.Failed.Add(part + " (" + why + ")");
                    else result.Points += part.Count;
                    continue;
                }
                var a = part.Source;
                int rc = 0;
                if (!a.Bits)
                {
                    // the runs to write, in words: the area's named words (all of them for most areas) within the
                    // part; a 32-bit area's two words a device, written from a device's first word on
                    int wpp = a.WordsPerPoint, wFrom = part.From * wpp, wEnd = (part.From + part.Count) * wpp;
                    var runs = (a.Pieces ?? new List<int[]> { new[] { 0, a.Points * wpp } })
                        .Select(p => new[] { Math.Max(p[0], wFrom), Math.Min(p[0] + p[1], wEnd) })
                        .Where(r => r[1] > r[0]).ToList();
                    foreach (var r in runs)
                    {
                        for (int at = r[0]; at < r[1] && rc == 0; at += Chunk)
                        {
                            int n = Math.Min(Chunk, r[1] - at);
                            var words = new short[n];
                            Array.Copy(a.Words, at, words, 0, n);
                            rc = link.Write(PlcAddressHelper.Compose(prefix, target + (at - wFrom) / wpp), words);
                        }
                        if (rc != 0) break;
                    }
                    done += part.Count * wpp;
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
                        rc = link.Write(PlcAddressHelper.Compose(prefix, target + 16L * w), words);
                    }
                    // the bits after the last whole word, one by one: a word would change the devices after the part
                    for (int i = whole * 16; i < part.Count && rc == 0; i++)
                        rc = link.WriteOne(PlcAddressHelper.Compose(prefix, target + i), a.Value(part.From + i));
                    done += (part.Count + 15) / 16;
                }
                if (link.Lost)
                {
                    result.LinkLost = true;
                    result.Failed.Add(part + " (" + LanguageManager.GetString("Msg_BakLinkLostShort", "the connection was lost") + ")");
                    break;
                }
                if (rc != 0) result.Failed.Add(part + " (" + (rc == -1 ? "refused" : "0x" + rc.ToString("X8")) + ")");
                else result.Points += part.Count;
            }
            if (progress != null) progress(total, total, "");
            return result;
        }
    }
}
