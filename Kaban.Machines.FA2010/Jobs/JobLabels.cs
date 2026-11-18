using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Kaban.Core.Helpers;
using Kaban.Core.Labels;
using Kaban.Profiles.Jobs;

namespace Kaban.Machines.FA2010.Jobs
{
    /// <summary>
    /// THE LABELS OF THE BARS SENT (17 November: "take barcode reading and writing from KabanMachineSystem as well") - the old
    /// program's LabelData rows: when a bar goes to the machine, each of its steps' IDs (D5000 - the job list) is kept with its
    /// piece's values (the job file's: the profile, its lengths and angles, the order, the customer, the trolley ...). The PLC asks
    /// for a piece's label with its ID (PC_Kesim_Barkod_Id D386) - this gives that piece's values; a leftover's label (PC_FireBarkod_Bas
    /// L24, its length PC_BarkodFireBoyu D388) the values of the bar sent last. Kept in C:\ProgramData\KabanHMI\JobLabels.cfg (a
    /// restart in the middle of a bar loses no label).
    /// </summary>
    internal static class JobLabels
    {
        internal static string StatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "JobLabels.cfg");
        /// <summary>How many IDs are kept (the oldest forgotten): a job of 4000 steps.</summary>
        internal const int Kept = 4000;
        private static readonly object Gate = new object();
        private static readonly Dictionary<int, LabelValues> _byId = new Dictionary<int, LabelValues>();
        private static readonly List<int> _order = new List<int>();
        private static LabelValues _lastBar;
        private static bool _loaded;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>The machine's name on its labels (FA2010Module sets it).</summary>
        internal static string MachineName = "FA2010";

        /// <summary>A piece's values: its bar's and its own, as the job file has them.</summary>
        internal static LabelValues ValuesOf(KbnJob job, KbnBar bar, int pieceIndex)
        {
            var v = BarValues(job, bar);
            if (bar == null || pieceIndex < 0 || pieceIndex >= bar.Pieces.Count) return v;
            var p = bar.Pieces[pieceIndex];
            v[LabelKeys.Customer] = p.Customer;
            v[LabelKeys.DealerName] = p.Dealer;
            v[LabelKeys.OrderNo] = p.OrderNo;
            v[LabelKeys.OrderLineNo] = p.OrderLineNo;
            v[LabelKeys.CutLength] = p.CutLength.ToString("0.#", CI);
            v[LabelKeys.OtherLength] = p.OtherLength.ToString("0.#", CI);
            v[LabelKeys.LeftAngle] = p.LeftAngle.ToString("0.#", CI);
            v[LabelKeys.RightAngle] = p.RightAngle.ToString("0.#", CI);
            v[LabelKeys.ProfilePosition] = p.Position;
            v[LabelKeys.Accessories] = p.Accessories;
            v[LabelKeys.TrolleyName] = p.TrolleyName;
            v[LabelKeys.TrolleySection] = p.TrolleySection;
            v[LabelKeys.ReinforcementCode] = p.ReinforcementCode;
            v[LabelKeys.ReinforcementLength] = p.ReinforcementLength > 0 ? p.ReinforcementLength.ToString("0.#", CI) : "";
            v[LabelKeys.FrameNo] = p.FrameNo;
            v[LabelKeys.UserBarcode] = p.Barcode;
            v[LabelKeys.UserComment1] = p.Comment1;
            v[LabelKeys.UserComment2] = p.Comment2;
            v[LabelKeys.UserComment3] = p.Comment3;
            v[LabelKeys.Picture] = p.Picture;
            v[LabelKeys.KabanBarcode] = LabelKeys.KabanCode(bar.Code, bar.No, pieceIndex + 1);
            return v;
        }

        /// <summary>The bar's values (a leftover's label: the profile, its colour).</summary>
        internal static LabelValues BarValues(KbnJob job, KbnBar bar)
        {
            var v = new LabelValues();
            v[LabelKeys.MachineName] = MachineName;
            if (job != null)
            {
                v[LabelKeys.JobName] = job.Name;
                try { if (!string.IsNullOrEmpty(job.Path)) v.PictureFolder = Path.GetDirectoryName(job.Path) ?? ""; } catch { }
            }
            if (bar != null)
            {
                v[LabelKeys.BarNo] = bar.Title;
                v[LabelKeys.ProfileCode] = bar.Code;
                v[LabelKeys.ProfileDescription] = bar.Name;
                v[LabelKeys.Color] = bar.Color;
            }
            return v;
        }

        /// <summary>The bar just sent: its steps' IDs with their pieces' values (the IDs of a bar sent again replaced).</summary>
        internal static void Take(BarPlan plan, KbnJob job)
        {
            if (plan == null) return;
            lock (Gate)
            {
                Load();
                foreach (var s in plan.Steps)
                {
                    if (s.Piece < 0) continue;
                    var v = ValuesOf(job, plan.Bar, s.Piece);
                    if (_byId.ContainsKey(s.No)) _order.Remove(s.No);
                    _byId[s.No] = v;
                    _order.Add(s.No);
                }
                while (_order.Count > Kept) { _byId.Remove(_order[0]); _order.RemoveAt(0); }
                _lastBar = BarValues(job, plan.Bar);
                Save();
            }
        }

        /// <summary>A piece's values by its ID (the PLC's D386); null - not a piece of a bar sent from this HMI.</summary>
        internal static LabelValues Piece(double id)
        {
            lock (Gate)
            {
                Load();
                LabelValues v;
                return _byId.TryGetValue((int)Math.Round(id), out v) ? v.Clone() : null;
            }
        }

        /// <summary>A leftover's values: the bar sent last (its profile, colour, job).</summary>
        internal static LabelValues Leftover(double lengthMm)
        {
            lock (Gate)
            {
                Load();
                return _lastBar != null ? _lastBar.Clone() : new LabelValues();
            }
        }

        internal static int Count { get { lock (Gate) { Load(); return _byId.Count; } } }

        /// <summary>Everything forgotten (tests).</summary>
        internal static void Reset()
        {
            lock (Gate) { _byId.Clear(); _order.Clear(); _lastBar = null; _loaded = true; }
        }

        private static void Save()
        {
            var sb = new StringBuilder("# Kaban HMI - the labels of the pieces of the bars sent (FA2010 Jobs)\r\n");
            if (_lastBar != null) sb.Append("bar\t").Append(_lastBar.Pack()).Append("\r\n");
            foreach (var id in _order) sb.Append(id.ToString(CI)).Append('\t').Append(_byId[id].Pack()).Append("\r\n");
            try { SafeFile.WriteAllText(StatePath, sb.ToString()); }
            catch (Exception ex) { AuditLogger.Log("[LABEL] " + StatePath + " could not be written: " + ex.Message); }
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(StatePath)) return;
                foreach (var line in File.ReadAllLines(StatePath, Encoding.UTF8))
                {
                    int tab = line.IndexOf('\t');
                    if (tab <= 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    string head = line.Substring(0, tab), rest = line.Substring(tab + 1);
                    int id;
                    if (head == "bar") _lastBar = LabelValues.Unpack(rest);
                    else if (int.TryParse(head, NumberStyles.Integer, CI, out id) && !_byId.ContainsKey(id))
                    {
                        _byId[id] = LabelValues.Unpack(rest);
                        _order.Add(id);
                    }
                }
            }
            catch (Exception ex) { AuditLogger.Log("[LABEL] " + StatePath + " could not be read: " + ex.Message); }
        }
    }
}
