using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Kaban.Core.Helpers;

namespace Kaban.Core.Labels
{
    /// <summary>A picture as the printer takes it: rows of bits, the first bit of a byte the leftmost dot, 1 a black dot.</summary>
    public sealed class LabelRaster
    {
        public int WidthBytes, Height;
        public byte[] Bits = new byte[0];
    }

    /// <summary>
    /// A LABEL IN THE PRINTER'S OWN LANGUAGE (the old ZebraPrinterSender / LabelHeaderProvider.ZebraPrinterOutput):
    ///   EPL - as the old program wrote it, line by line: "I8,&lt;character set&gt;,001", N, D15 (darkness), OD (no ribbon) or O,
    ///         ZB, R&lt;left margin&gt;,0 (the label in the middle of the 104 mm head), a line a field - A (a text), B (a Code 128
    ///         barcode, its height 40 + 10 x the font), b (a QR code) - and P1. A picture goes with the label itself (GW - the
    ///         old one stored it in the printer first as a PCX file, GM / GG).
    ///   ZPL - the same label for the newer Zebra printers (^XA ... ^XZ, UTF-8).
    /// 8 dots a mm (203 dpi), as the old one.
    /// </summary>
    public static class LabelWriter
    {
        public const int DotsPerMm = 8, HeadWidthMm = 104;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>Loads a picture's file into dots (Kaban.UI sets it - System.Drawing); null: no pictures.</summary>
        public static Func<string, int, int, LabelRaster> LoadPicture;

        /// <summary>The old program's character sets (EPL's I8 p2) and their Windows code pages, by the label's language.</summary>
        private static readonly Dictionary<string, string[]> Sets = new Dictionary<string, string[]>
        {
            { "A", new[] { "afrikaans", "basque", "catalan", "danish", "dutch", "english", "faroese", "finnish", "french", "galician", "german", "icelandic",
                           "indonesian", "italian", "malay", "norwegian", "portuguese", "spanish", "swahili", "swedish" } },
            { "B", new[] { "albanian", "croatian", "czech", "hungarian", "polish", "romanian", "serbian", "slovak", "slovenian" } },
            { "C", new[] { "azeri", "belarusian", "bulgarian", "macedonian", "kazakh", "kyrgyz", "mongolian", "russian", "tatar", "ukrainian" } },
            { "D", new[] { "greek" } },
            { "E", new[] { "turkish", "uzbek" } },
            { "F", new[] { "hebrew" } }
        };
        private static readonly Dictionary<string, int> CodePages = new Dictionary<string, int> { { "A", 1252 }, { "B", 1250 }, { "C", 1251 }, { "D", 1253 }, { "E", 1254 }, { "F", 1255 } };

        /// <summary>The languages a label's text can be in.</summary>
        public static List<string> Languages { get { return Sets.Values.SelectMany(x => x).OrderBy(x => x).ToList(); } }

        /// <summary>EPL's character set of a language (the old GetLanguageCode: Turkish when not known).</summary>
        public static string SetOf(string language)
        {
            var l = (language ?? "").Trim().ToLowerInvariant();
            foreach (var kv in Sets) if (kv.Value.Contains(l)) return kv.Key;
            return "E";
        }

        /// <summary>The text's encoding (the language's Windows code page; Latin-1 where the system has none).</summary>
        public static Encoding EncodingOf(string language)
        {
            try { return Encoding.GetEncoding(CodePages[SetOf(language)]); }
            catch { return Encoding.GetEncoding(28591); }
        }

        /// <summary>What a field prints with these values (the old GetData): a fixed text, a value, Yes / No's texts, a date.</summary>
        public static string DataOf(LabelField f, LabelValues v, DateTime now)
        {
            string result;
            if (!f.IsDataField) result = f.Text ?? "";
            else
            {
                string value = v != null ? v[f.Key] : "";
                if (f.Type == LabelFieldType.YesNo)
                {
                    var s = (value ?? "").Trim().ToLowerInvariant();
                    result = s == "1" || s == "true" || s == "on" || s == "yes" ? f.ValueOnTrue : f.ValueOnFalse;
                }
                else if (f.Type == LabelFieldType.DateTime || f.Key == LabelKeys.PrintTime || f.Key == LabelKeys.ManufactureDate)
                {
                    DateTime d;
                    if (string.IsNullOrWhiteSpace(value)) d = now;
                    else if (!DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out d) &&
                             !DateTime.TryParse(value, CI, DateTimeStyles.None, out d)) d = now;
                    string fmt = !string.IsNullOrWhiteSpace(f.ValueOnTrue) ? f.ValueOnTrue : f.Key == LabelKeys.PrintTime ? "yyyy-MM-dd HH:mm:ss" : "d";
                    try { result = d.ToString(fmt, CultureInfo.CurrentCulture); } catch { result = d.ToString("d", CultureInfo.CurrentCulture); }
                }
                else result = value ?? "";
                // a data field's own text in front of its value ("L: 1250.5") - a label of the old program had them as fields of their own
                if (f.Type != LabelFieldType.Barcode && f.Type != LabelFieldType.QRCode && !string.IsNullOrEmpty(f.Text)) result = f.Text + result;
            }
            // the printer's font 5 has upper case letters only (the old note)
            if (f.FontSize == 5) result = result.ToUpper(CultureInfo.CurrentCulture);
            return result;
        }

        /// <summary>The label as the printer takes it.</summary>
        public static byte[] Build(LabelTemplate t, LabelValues v, DateTime? at = null)
        {
            var now = at ?? DateTime.Now;
            return t.PrinterLanguage == LabelPrinterLanguage.ZPL ? Zpl(t, v, now) : Epl(t, v, now);
        }

        /// <summary>The label's commands to read (a picture's dots left out).</summary>
        public static string Text(LabelTemplate t, LabelValues v, DateTime? at = null)
        {
            var bytes = Build(t, v, at);
            var enc = t.PrinterLanguage == LabelPrinterLanguage.ZPL ? Encoding.UTF8 : EncodingOf(t.Language);
            var s = enc.GetString(bytes);
            var sb = new StringBuilder();
            foreach (var line in s.Split('\n'))
            {
                var l = line.TrimEnd('\r');
                if (l.StartsWith("GW", StringComparison.Ordinal))
                {
                    int c = 0, i = 0;
                    for (; i < l.Length && c < 4; i++) if (l[i] == ',') c++;
                    l = l.Substring(0, Math.Max(0, i - 1)) + ",<picture>";
                }
                else if (l.Contains("^GFA,"))
                {
                    int i = l.IndexOf("^GFA,", StringComparison.Ordinal);
                    l = l.Substring(0, i) + "^GFA,<picture>^FS";
                }
                sb.Append(l).Append("\r\n");
            }
            return sb.ToString().TrimEnd() + "\r\n";
        }

        private static string Q(string s) { return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\""); }

        private static byte[] Epl(LabelTemplate t, LabelValues v, DateTime now)
        {
            var enc = EncodingOf(t.Language);
            var ms = new MemoryStream();
            Action<string> line = s => { var b = enc.GetBytes(s + "\r\n"); ms.Write(b, 0, b.Length); };
            line("");
            line("I8," + SetOf(t.Language) + ",001");
            line("");
            line("N");
            line("D15");
            line(t.HasRibbon ? "O" : "OD");
            line("ZB");
            int margin = Math.Max(0, (HeadWidthMm - t.WidthMm) / 2) * DotsPerMm;
            line("R" + margin.ToString(CI) + ",0");
            foreach (var f in t.Fields)
            {
                int x = f.X * DotsPerMm, y = f.Y * DotsPerMm;
                int font = Math.Max(1, Math.Min(5, f.FontSize));
                switch (f.Type)
                {
                    case LabelFieldType.Text:
                    case LabelFieldType.YesNo:
                    case LabelFieldType.DateTime:
                        line(string.Format(CI, "A{0},{1},0,{2},1,1,N,\"{3}\"", x, y, font, Q(DataOf(f, v, now))));
                        break;
                    case LabelFieldType.Barcode:
                        {
                            var d = DataOf(f, v, now);
                            if (d.Length > 0) line(string.Format(CI, "B{0},{1},0,1,2,2,{2},B,\"{3}\"", x, y, 40 + font * 10, Q(d)));
                        }
                        break;
                    case LabelFieldType.QRCode:
                        {
                            var d = DataOf(f, v, now);
                            if (d.Length > 0) line(string.Format(CI, "b{0},{1},Q,m2,\"{2}\"", x, y, Q(d)));
                        }
                        break;
                    case LabelFieldType.Picture:
                        {
                            var r = Picture(f, v);
                            if (r == null) break;
                            var head = enc.GetBytes(string.Format(CI, "GW{0},{1},{2},{3},", x, y, r.WidthBytes, r.Height));
                            ms.Write(head, 0, head.Length);
                            // EPL: a 0 bit prints the dot
                            var inv = r.Bits.Select(b => (byte)~b).ToArray();
                            ms.Write(inv, 0, inv.Length);
                            line("");
                        }
                        break;
                }
            }
            line("P1");
            return ms.ToArray();
        }

        private static readonly int[] ZplFont = { 0, 16, 20, 24, 30, 52 };

        private static string Z(string s)
        {
            return (s ?? "").Replace("_", "_5F").Replace("^", "_5E").Replace("~", "_7E");
        }

        private static byte[] Zpl(LabelTemplate t, LabelValues v, DateTime now)
        {
            var sb = new StringBuilder();
            sb.Append("^XA\r\n^CI28\r\n");
            sb.Append("^PW").Append((t.WidthMm * DotsPerMm).ToString(CI)).Append("\r\n");
            sb.Append("^LL").Append((t.HeightMm * DotsPerMm).ToString(CI)).Append("\r\n");
            sb.Append("^LH0,0\r\n^MT").Append(t.HasRibbon ? "T" : "D").Append("\r\n");
            foreach (var f in t.Fields)
            {
                int x = f.X * DotsPerMm, y = f.Y * DotsPerMm;
                int font = Math.Max(1, Math.Min(5, f.FontSize));
                string fo = string.Format(CI, "^FO{0},{1}", x, y);
                switch (f.Type)
                {
                    case LabelFieldType.Text:
                    case LabelFieldType.YesNo:
                    case LabelFieldType.DateTime:
                        sb.Append(fo).Append(string.Format(CI, "^A0N,{0},{0}", ZplFont[font])).Append("^FH_^FD").Append(Z(DataOf(f, v, now))).Append("^FS\r\n");
                        break;
                    case LabelFieldType.Barcode:
                        {
                            var d = DataOf(f, v, now);
                            if (d.Length > 0) sb.Append(fo).Append(string.Format(CI, "^BY2^BCN,{0},Y,N,N", 40 + font * 10)).Append("^FH_^FD").Append(Z(d)).Append("^FS\r\n");
                        }
                        break;
                    case LabelFieldType.QRCode:
                        {
                            var d = DataOf(f, v, now);
                            if (d.Length > 0) sb.Append(fo).Append("^BQN,2,4^FH_^FDQA,").Append(Z(d)).Append("^FS\r\n");
                        }
                        break;
                    case LabelFieldType.Picture:
                        {
                            var r = Picture(f, v);
                            if (r == null) break;
                            int n = r.Bits.Length;
                            var hex = new StringBuilder(n * 2);
                            foreach (var b in r.Bits) hex.Append(b.ToString("X2", CI));
                            sb.Append(fo).Append(string.Format(CI, "^GFA,{0},{0},{1},", n, r.WidthBytes)).Append(hex).Append("^FS\r\n");
                        }
                        break;
                }
            }
            sb.Append("^PQ1\r\n^XZ\r\n");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>A picture field's dots: its value a file (in the job's folder - the old transfer directory - or a whole path).</summary>
        private static LabelRaster Picture(LabelField f, LabelValues v)
        {
            var load = LoadPicture;
            if (load == null || v == null) return null;
            string file = f.IsDataField ? v[f.Key] : f.Text;
            if (string.IsNullOrWhiteSpace(file)) return null;
            try
            {
                string path = Path.IsPathRooted(file) ? file : Path.Combine(v.PictureFolder ?? "", file);
                if (!File.Exists(path)) { AuditLogger.Log("[LABEL] the picture " + path + " is not there - the label printed without it."); return null; }
                return load(path, Math.Max(1, f.Width) * DotsPerMm, Math.Max(1, f.Height) * DotsPerMm);
            }
            catch (Exception ex) { AuditLogger.Log("[LABEL] the picture " + file + ": " + ex.Message); return null; }
        }
    }

    /// <summary>
    /// WHERE A LABEL GOES (the old IsRs232 / SendtoComPort / RawPrinterHelper): "COM3" - a serial printer (its baud rate, 8 N 1);
    /// "192.168.1.50:9100" or "192.168.1.50" - a network printer (port 9100); "file:C:\..." - a file; anything else a Windows
    /// printer's name, the label sent to it as it is (RAW).
    /// </summary>
    public static class LabelPrinters
    {
        /// <summary>Sends a label; "" sent, otherwise why not. Replaceable (tests).</summary>
        public static Func<string, int, byte[], string> Send = SendNow;

        public static bool IsSerial(string printer)
        {
            var p = (printer ?? "").Trim();
            int n;
            return p.StartsWith("COM", StringComparison.OrdinalIgnoreCase) && p.Length <= 6 && int.TryParse(p.Substring(3), out n);
        }

        public static bool IsNetwork(string printer, out string host, out int port)
        {
            host = ""; port = 9100;
            var p = (printer ?? "").Trim();
            if (p.Length == 0 || p.Contains("\\") || p.Contains(" ")) return false;
            int i = p.LastIndexOf(':');
            string h = i > 0 ? p.Substring(0, i) : p;
            if (i > 0 && !int.TryParse(p.Substring(i + 1), out port)) return false;
            System.Net.IPAddress ip;
            if (!System.Net.IPAddress.TryParse(h, out ip) && i < 0) return false;
            host = h;
            return true;
        }

        /// <summary>The serial ports of the PC.</summary>
        public static string[] SerialPorts { get { try { return SerialPort.GetPortNames().OrderBy(x => x.Length).ThenBy(x => x).ToArray(); } catch { return new string[0]; } } }

        private static string SendNow(string printer, int baud, byte[] data)
        {
            var p = (printer ?? "").Trim();
            if (p.Length == 0) return LanguageManager.GetString("Lbl_NoPrinter", "No printer set for the label.");
            try
            {
                if (p.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                {
                    var path = p.Substring(5).Trim();
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllBytes(path, data);
                    return "";
                }
                if (IsSerial(p))
                {
                    using (var port = new SerialPort(p.ToUpperInvariant(), baud > 0 ? baud : 9600, Parity.None, 8, StopBits.One) { WriteTimeout = 5000 })
                    {
                        port.Open();
                        for (int k = 0; k < data.Length; k += 100) port.Write(data, k, Math.Min(100, data.Length - k));
                        // the last bytes out before the port closes
                        var until = DateTime.UtcNow.AddSeconds(5);
                        while (port.BytesToWrite > 0 && DateTime.UtcNow < until) Thread.Sleep(20);
                    }
                    return "";
                }
                string host; int tcp;
                if (IsNetwork(p, out host, out tcp))
                {
                    using (var c = new TcpClient())
                    {
                        var ar = c.BeginConnect(host, tcp, null, null);
                        if (!ar.AsyncWaitHandle.WaitOne(3000)) return host + ":" + tcp + " - " + LanguageManager.GetString("Lbl_NoAnswer", "the printer does not answer.");
                        c.EndConnect(ar);
                        using (var s = c.GetStream()) { s.WriteTimeout = 5000; s.Write(data, 0, data.Length); s.Flush(); }
                    }
                    return "";
                }
                return Raw.Send(p, data);
            }
            catch (Exception ex) { return p + ": " + ex.Message; }
        }

        /// <summary>A Windows printer, the label as it is (the old RawPrinterHelper - winspool).</summary>
        private static class Raw
        {
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private class DOCINFO
            {
                [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
                [MarshalAs(UnmanagedType.LPWStr)] public string pOutputFile;
                [MarshalAs(UnmanagedType.LPWStr)] public string pDataType;
            }

            [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern bool OpenPrinter(string name, out IntPtr h, IntPtr defaults);
            [DllImport("winspool.drv", SetLastError = true)]
            private static extern bool ClosePrinter(IntPtr h);
            [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern bool StartDocPrinter(IntPtr h, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFO di);
            [DllImport("winspool.drv", SetLastError = true)]
            private static extern bool EndDocPrinter(IntPtr h);
            [DllImport("winspool.drv", SetLastError = true)]
            private static extern bool StartPagePrinter(IntPtr h);
            [DllImport("winspool.drv", SetLastError = true)]
            private static extern bool EndPagePrinter(IntPtr h);
            [DllImport("winspool.drv", SetLastError = true)]
            private static extern bool WritePrinter(IntPtr h, IntPtr bytes, int count, out int written);

            public static string Send(string printer, byte[] data)
            {
                IntPtr h;
                if (!OpenPrinter(printer, out h, IntPtr.Zero))
                    return printer + ": " + LanguageManager.GetString("Lbl_NoSuchPrinter", "no such printer (Windows' Printers, or COMn, an address:port, file:path).");
                var mem = Marshal.AllocCoTaskMem(data.Length);
                try
                {
                    Marshal.Copy(data, 0, mem, data.Length);
                    if (!StartDocPrinter(h, 1, new DOCINFO { pDocName = "Kaban HMI label", pDataType = "RAW" })) return printer + ": the printer refused the label (" + Marshal.GetLastWin32Error() + ").";
                    try
                    {
                        int written;
                        StartPagePrinter(h);
                        bool ok = WritePrinter(h, mem, data.Length, out written);
                        EndPagePrinter(h);
                        return ok && written == data.Length ? "" : printer + ": " + written + " of " + data.Length + " bytes taken (" + Marshal.GetLastWin32Error() + ").";
                    }
                    finally { EndDocPrinter(h); }
                }
                finally { Marshal.FreeCoTaskMem(mem); ClosePrinter(h); }
            }
        }
    }

    /// <summary>A label printed (or not): when, which, for what, what came of it.</summary>
    public sealed class LabelPrint
    {
        public DateTime At = DateTime.Now;
        public string Template = "", Source = "", Message = "", Summary = "";
        public LabelUse Use;
        public bool Ok;
    }

    /// <summary>
    /// THE LABELS PRINTED (17 November: "take barcode reading and writing from KabanMachineSystem as well"): a piece cut or a
    /// leftover - by the PLC (LabelTriggers), a page or a test - printed with every label on for it (its machine's or the panel's),
    /// one after the other on a thread of its own (a printer that does not answer holds no page and no PLC scan). Every print is
    /// in the log ([LABEL]) and in Recent (HMI Settings > Labels).
    /// </summary>
    public static class LabelService
    {
        private static readonly object Gate = new object();
        private static readonly List<LabelPrint> _recent = new List<LabelPrint>();
        private static BlockingCollection<Action> _queue;
        public const int RecentKept = 60;

        /// <summary>Printed inline, not on the printing thread (tests).</summary>
        public static bool Synchronous;
        /// <summary>A label printed or not (on the printing thread).</summary>
        public static event Action<LabelPrint> Done;

        public static List<LabelPrint> Recent { get { lock (Gate) return _recent.ToList(); } }

        /// <summary>The labels on for this use and machine.</summary>
        public static List<LabelTemplate> For(LabelUse use, string machine)
        {
            return LabelStore.All().Where(t => t.Enabled && t.Use == use &&
                                               (t.Machine.Length == 0 || string.Equals(t.Machine, machine ?? "", StringComparison.OrdinalIgnoreCase))).ToList();
        }

        /// <summary>Prints the labels on for it; the number of labels queued (0: none is set for it - said in the log).</summary>
        public static int Print(LabelUse use, string machine, LabelValues values, string source)
        {
            var list = For(use, machine);
            if (list.Count == 0)
            {
                AuditLogger.Log("[LABEL] " + source + ": no " + (use == LabelUse.Leftover ? "leftover" : "piece") + " label is on (HMI Settings > Labels) - nothing printed.");
                return 0;
            }
            foreach (var t in list) Queue(t, values, source);
            return list.Count;
        }

        /// <summary>One label printed as soon as the ones before it are.</summary>
        public static void Queue(LabelTemplate t, LabelValues values, string source)
        {
            var tt = t.Clone();
            var vv = values != null ? values.Clone() : new LabelValues();
            Action a = () => PrintNow(tt, vv, source);
            if (Synchronous) { a(); return; }
            lock (Gate)
            {
                if (_queue == null)
                {
                    _queue = new BlockingCollection<Action>();
                    var q = _queue;
                    new Thread(() => { foreach (var x in q.GetConsumingEnumerable()) { try { x(); } catch (Exception ex) { AuditLogger.Log("[LABEL] " + ex.Message); } } })
                    { IsBackground = true, Name = "Kaban labels" }.Start();
                }
                _queue.Add(a);
            }
        }

        /// <summary>The label printed now (a test print); the result.</summary>
        public static LabelPrint PrintNow(LabelTemplate t, LabelValues values, string source)
        {
            var r = new LabelPrint { Template = t.Name, Use = t.Use, Source = source ?? "", Summary = Summary(t, values) };
            try
            {
                var data = LabelWriter.Build(t, values);
                r.Message = LabelPrinters.Send(t.Printer, t.BaudRate, data) ?? "";
                r.Ok = r.Message.Length == 0;
            }
            catch (Exception ex) { r.Message = ex.Message; }
            AuditLogger.Log("[LABEL] " + r.Source + ": label " + t.Name + " (" + r.Summary + ") " + (r.Ok ? "printed on " + t.Printer : "NOT printed: " + r.Message));
            lock (Gate)
            {
                _recent.Insert(0, r);
                if (_recent.Count > RecentKept) _recent.RemoveRange(RecentKept, _recent.Count - RecentKept);
            }
            var d = Done;
            if (d != null) try { d(r); } catch { }
            return r;
        }

        /// <summary>The label's main values in a line: the profile, the length, the order.</summary>
        private static string Summary(LabelTemplate t, LabelValues v)
        {
            if (v == null) return "";
            var parts = new List<string>();
            foreach (var k in new[] { LabelKeys.ProfileCode, LabelKeys.CutLength, LabelKeys.LeftOverLength, LabelKeys.OrderNo, LabelKeys.KabanBarcode })
                if (v[k].Length > 0) parts.Add(v[k]);
            return string.Join(" / ", parts);
        }
    }
}
