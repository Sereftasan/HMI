using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Kaban.Core.Helpers
{
    /// <summary>What happened to the production (a line of the log).</summary>
    public enum ProductionKind { BarSent, BarFinished, Piece, Leftover, RunStarted, RunEnded }

    /// <summary>A line of the production log: when, which machine, what, the job / bar / profile, the piece's customer and order.</summary>
    public sealed class ProductionEntry
    {
        public DateTime At = DateTime.Now;
        public string Machine = "", Job = "", Bar = "", Profile = "", Customer = "", Order = "", User = "", Note = "";
        public ProductionKind Kind;
        public double LengthMm, Seconds;
        public int Pieces;
        public bool Trial, Automatic;
    }

    /// <summary>A machine's counter shown with the production (the old General Information: its power-on and working time).</summary>
    public sealed class ProductionCounter
    {
        public string Machine = "", Key = "", Text = "", Address = "";
        public int Station = 1;
        public bool DoubleWord = true;
        /// <summary>The value is seconds (shown as hours and minutes).</summary>
        public bool Seconds = true;
    }

    /// <summary>
    /// THE PRODUCTION LOG (19 November - the old program's CuttingListHeader / CustomerOrderJob tables and its Customer Report: what was
    /// sent, finished and cut, when, by whom, for which customer and order): a line an event, a file a month
    /// (C:\ProgramData\KabanHMI\Production\yyyy-MM.csv - opens in Excel). HMI Settings > Production shows and exports it.
    /// </summary>
    public static class ProductionLog
    {
        public static string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Production");
        public const string Header = "Time;Machine;Event;Job;Bar;Profile;Length mm;Pieces;Seconds;Customer;Order;User;Trial;Automatic;Note";
        private static readonly object Gate = new object();
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private static readonly List<ProductionCounter> _counters = new List<ProductionCounter>();

        /// <summary>A line written (any thread).</summary>
        public static event Action<ProductionEntry> Added;

        public static string FileFor(DateTime at) { return Path.Combine(Folder, at.ToString("yyyy-MM", CI) + ".csv"); }

        private static string F(string s) { return (s ?? "").Replace(";", ",").Replace("\r", " ").Replace("\n", " "); }

        public static void Add(ProductionEntry e)
        {
            if (e == null) return;
            var line = string.Join(";", e.At.ToString("yyyy-MM-dd HH:mm:ss", CI), F(e.Machine), e.Kind.ToString(), F(e.Job), F(e.Bar), F(e.Profile),
                                   e.LengthMm.ToString("0.#", CI), e.Pieces.ToString(CI), e.Seconds.ToString("0", CI), F(e.Customer), F(e.Order),
                                   F(e.User), e.Trial ? "1" : "0", e.Automatic ? "1" : "0", F(e.Note));
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Folder);
                    var f = FileFor(e.At);
                    if (!File.Exists(f)) File.WriteAllText(f, Header + "\r\n", new UTF8Encoding(true));
                    File.AppendAllText(f, line + "\r\n", new UTF8Encoding(false));
                }
            }
            catch (Exception ex) { AuditLogger.Log("[PRODUCTION] " + ex.Message); }
            var h = Added;
            if (h != null) try { h(e); } catch { }
        }

        /// <summary>The lines from .. to (both days included), oldest first.</summary>
        public static List<ProductionEntry> Read(DateTime from, DateTime to)
        {
            var list = new List<ProductionEntry>();
            from = from.Date; to = to.Date.AddDays(1);
            for (var m = new DateTime(from.Year, from.Month, 1); m < to; m = m.AddMonths(1))
            {
                var f = FileFor(m);
                string[] lines;
                try { lock (Gate) { if (!File.Exists(f)) continue; lines = File.ReadAllLines(f, Encoding.UTF8); } }
                catch (Exception ex) { AuditLogger.Log("[PRODUCTION] " + f + ": " + ex.Message); continue; }
                foreach (var l in lines.Skip(1))
                {
                    var e = Parse(l);
                    if (e != null && e.At >= from && e.At < to) list.Add(e);
                }
            }
            return list;
        }

        internal static ProductionEntry Parse(string line)
        {
            var t = (line ?? "").Split(';');
            if (t.Length < 15) return null;
            DateTime at;
            ProductionKind k;
            if (!DateTime.TryParseExact(t[0], "yyyy-MM-dd HH:mm:ss", CI, DateTimeStyles.None, out at) || !Enum.TryParse(t[2], out k)) return null;
            double len, sec; int pcs;
            double.TryParse(t[6], NumberStyles.Float, CI, out len);
            int.TryParse(t[7], NumberStyles.Integer, CI, out pcs);
            double.TryParse(t[8], NumberStyles.Float, CI, out sec);
            return new ProductionEntry
            {
                At = at, Machine = t[1], Kind = k, Job = t[3], Bar = t[4], Profile = t[5], LengthMm = len, Pieces = pcs, Seconds = sec,
                Customer = t[9], Order = t[10], User = t[11], Trial = t[12] == "1", Automatic = t[13] == "1", Note = t[14]
            };
        }

        /// <summary>The lines as a CSV file (the export).</summary>
        public static void Export(IEnumerable<ProductionEntry> entries, string path)
        {
            var sb = new StringBuilder(Header + "\r\n");
            foreach (var e in entries)
                sb.Append(string.Join(";", e.At.ToString("yyyy-MM-dd HH:mm:ss", CI), F(e.Machine), e.Kind.ToString(), F(e.Job), F(e.Bar), F(e.Profile),
                                      e.LengthMm.ToString("0.#", CI), e.Pieces.ToString(CI), e.Seconds.ToString("0", CI), F(e.Customer), F(e.Order),
                                      F(e.User), e.Trial ? "1" : "0", e.Automatic ? "1" : "0", F(e.Note))).Append("\r\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        // ------------------------------------------------------------------ the machines' counters

        public static void Counter(ProductionCounter c)
        {
            if (c == null) return;
            lock (Gate)
            {
                _counters.RemoveAll(x => x.Machine == c.Machine && x.Key == c.Key);
                _counters.Add(c);
            }
        }

        public static List<ProductionCounter> Counters { get { lock (Gate) return _counters.ToList(); } }

        /// <summary>A counter's value now (null: not read - the PLC not connected).</summary>
        public static long? ValueOf(ProductionCounter c)
        {
            try
            {
                int st = PlcAddressHelper.StationOf(c.Station, c.Address);
                string dev = PlcAddressHelper.DeviceOf(c.Address);
                var driver = PlcManager.Instance.GetDriverForMonitoring(st);
                if (driver == null || !driver.IsConnected) return null;
                var devices = c.DoubleWord ? new[] { dev, PlcAddressHelper.OffsetAddress(dev, 1) } : new[] { dev };
                int[] v;
                if (!driver.TryReadMulti(devices, out v) || v == null || v.Length < devices.Length) return null;
                return c.DoubleWord ? (long)(uint)((v[0] & 0xFFFF) | ((v[1] & 0xFFFF) << 16)) : (v[0] & 0xFFFF);
            }
            catch { return null; }
        }

        /// <summary>Seconds as "1234 h 05 min".</summary>
        public static string Hours(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return string.Format(CI, "{0} h {1:00} min", (long)ts.TotalHours, ts.Minutes);
        }
    }
}
