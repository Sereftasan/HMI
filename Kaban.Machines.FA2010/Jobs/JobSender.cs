using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;

namespace Kaban.Machines.FA2010.Jobs
{
    /// <summary>
    /// "SEND TO THE MACHINE" (the old program's TransferProvider_FXCpu.Transfer): what one bar writes into the PLC, in the label list's
    /// devices (PlcLabels.csv - the PLC Monitor's tab HMI -> PLC shows each), the structures as StructuredTypes.csv lays them out:
    ///
    ///   PC_ProfilParametreleri     R25600   the profile's parameters (its machine settings in the library)
    ///   PC_FrezeIslemParametreleri R26000   each milling operation's parameters, R26000 + 10 x (code - 1)
    ///   PC_IsListesi_IslemID       D5000    the steps' IDs          (a double word a step, 500 steps)
    ///   PC_IsListesi_Pozisyon      D6000    the steps' positions x100, then 999999 - the end of the bar
    ///   PC_IsListesi_IslemKodu     D7000    the steps' codes: a cut 1000 + 10 x angle (2800 - 10 x angle at a right end), a milling
    ///                                       operation its number 1 - 100
    ///
    /// The job list is written whole (500 steps): what a longer bar before left behind is cleared. The milling steps themselves (the
    /// G-code the PLC asks for with L101 / L103, R0 / R12800) are not sent yet.
    /// </summary>
    internal static class JobSender
    {
        internal const string ProfileLabel = "PC_ProfilParametreleri", MillingLabel = "PC_FrezeIslemParametreleri",
                              IdsLabel = "PC_IsListesi_IslemID", PositionsLabel = "PC_IsListesi_Pozisyon", CodesLabel = "PC_IsListesi_IslemKodu";
        internal const string ProfileType = "Profile Parameters", MillingType = "Milling Parameters";
        internal const int ListSteps = 500;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        internal sealed class Block
        {
            public string Address = "", What = "";
            public short[] Words;
        }

        /// <summary>The label's device in the station's label list (its fallback when the list has none).</summary>
        internal static string AddressOf(int station, string label, string fallback)
        {
            var l = PlcLabelTable.ByLabel(station, label);
            return l != null && !string.IsNullOrWhiteSpace(l.Address) ? l.Address.Trim() : fallback;
        }

        /// <summary>The device <paramref name="words"/> on from <paramref name="address"/> ("R26000" + 20 = "R26020"; decimal devices).</summary>
        internal static string Offset(string address, int words)
        {
            var m = Regex.Match(address ?? "", @"^([A-Za-z]+)(\d+)$");
            if (!m.Success) throw new FormatException("\"" + address + "\" is not a word device");
            return m.Groups[1].Value.ToUpperInvariant() + (long.Parse(m.Groups[2].Value, CI) + words).ToString(CI);
        }

        /// <summary>A structure's words from its members' values (as entered: mm, degrees; x the member's divisor into the PLC).</summary>
        internal static short[] Encode(StructuredTypes.Type t, Func<StructuredTypes.Member, string> value, List<string> problems)
        {
            var w = new short[Math.Max(t.Size, t.MinimumSize)];
            foreach (var m in t.Members)
            {
                string text = value(m);
                double? v = JobSettings.Num(text);
                if (!v.HasValue || v.Value == 0) continue;
                if (m.IsBit)
                {
                    if (m.Offset < w.Length) w[m.Offset] = (short)(w[m.Offset] | (1 << m.Bit));
                    continue;
                }
                double scaled = Math.Round(v.Value * (m.Divisor == 0 ? 1 : m.Divisor), MidpointRounding.AwayFromZero);
                if (m.Words >= 2)
                {
                    if (scaled < int.MinValue || scaled > int.MaxValue) { problems.Add(t.Name + " / " + m.Name + ": " + text + " is out of a double word's range"); continue; }
                    int i = (int)scaled;
                    if (m.Offset + 1 < w.Length) { w[m.Offset] = (short)(i & 0xFFFF); w[m.Offset + 1] = (short)(i >> 16); }
                }
                else
                {
                    if (scaled < short.MinValue || scaled > short.MaxValue) { problems.Add(t.Name + " / " + m.Name + ": " + text + " is out of a word's range (-32768 - 32767 in the PLC)"); continue; }
                    if (m.Offset < w.Length) w[m.Offset] = (short)scaled;
                }
            }
            return w;
        }

        /// <summary>A milling operation's member's value: by the member's name (Tool No, Tool Angle, Operation Length, Profile Approach Y / Z, Milling Speed).</summary>
        internal static string MillingValue(MillingOp op, StructuredTypes.Member m)
        {
            string n = (m.Name ?? "").ToLowerInvariant();
            double v;
            if (n.Contains("tool") && n.Contains("no")) v = op.ToolNo;
            else if (n.Contains("angle")) v = op.Face != null ? op.Face.Angle : 0;
            else if (n.Contains("length")) v = op.Length;
            else if (n.EndsWith(" y") || n.Contains("approach y")) v = op.Y;
            else if (n.EndsWith(" z") || n.Contains("approach z")) v = op.Z;
            else if (n.Contains("speed")) v = op.Speed;
            else return "";
            return v.ToString("R", CI);
        }

        private static void Split(int value, short[] into, int at)
        {
            into[at] = (short)(value & 0xFFFF);
            into[at + 1] = (short)(value >> 16);
        }

        /// <summary>A position into the PLC: x100, as the old program (Convert.ToInt32 of the mm x 100).</summary>
        internal static int Hundredths(double mm) { return Convert.ToInt32(mm * 100); }

        /// <summary>Everything the bar writes, in the order it is written; <paramref name="problems"/> gets what cannot be encoded.</summary>
        internal static List<Block> Blocks(BarPlan plan, int station, List<string> problems)
        {
            var list = new List<Block>();
            var pt = StructuredTypes.Find(ProfileType, station);
            if (pt == null) problems.Add("the machine's StructuredTypes.csv has no \"" + ProfileType + "\"");
            else
                list.Add(new Block
                {
                    Address = AddressOf(station, ProfileLabel, "R25600"), What = "the profile's parameters (" + plan.Bar.Code + ")",
                    Words = Encode(pt, m => { string v; return plan.ProfileValues.TryGetValue(m.Name, out v) ? v : ""; }, problems)
                });
            var mt = StructuredTypes.Find(MillingType, station);
            if (plan.Milling.Count > 0 && mt == null) problems.Add("the machine's StructuredTypes.csv has no \"" + MillingType + "\"");
            if (mt != null)
            {
                string first = AddressOf(station, MillingLabel, "R26000");
                int size = Math.Max(mt.Size, mt.MinimumSize);
                foreach (var op in plan.Milling)
                    list.Add(new Block
                    {
                        Address = Offset(first, size * (op.Index - 1)), What = "milling operation " + op.Index + ": " + op.Label,
                        Words = Encode(mt, m => MillingValue(op, m), problems)
                    });
            }
            var ids = new short[ListSteps * 2];
            var pos = new short[ListSteps * 2];
            var codes = new short[ListSteps * 2];
            int n = 0;
            foreach (var s in plan.Steps.Take(ListSteps - 1))
            {
                Split(s.No, ids, 2 * n);
                Split(Hundredths(s.Position), pos, 2 * n);
                Split(s.Code, codes, 2 * n);
                n++;
            }
            Split(JobPlanner.EndMark, pos, 2 * n);
            list.Add(new Block { Address = AddressOf(station, IdsLabel, "D5000"), What = "the steps' IDs", Words = ids });
            list.Add(new Block { Address = AddressOf(station, PositionsLabel, "D6000"), What = "the steps' positions (999999 after the last)", Words = pos });
            list.Add(new Block { Address = AddressOf(station, CodesLabel, "D7000"), What = "the steps' codes", Words = codes });
            return list;
        }

        internal sealed class Result
        {
            public bool Ok;
            public string Message = "";
            public int Blocks, Words;
        }

        /// <summary>The bar into the PLC (the question asked already). Stops at the first write the PLC refuses.</summary>
        internal static Result Send(BarPlan plan, int station, string jobName, bool trial)
        {
            var r = new Result();
            var problems = new List<string>();
            var blocks = Blocks(plan, station, problems);
            if (problems.Count > 0) { r.Message = string.Join("\r\n", problems); return r; }
            var driver = PlcManager.Instance.GetDriverForWrite(station);
            if (driver == null || !driver.IsConnected) { r.Message = "the PLC is not connected"; return r; }
            if (!PlcManager.Instance.CheckPermission(station)) { r.Message = "your user level may not write to this PLC"; return r; }
            var block = driver as IPlcBlockAccess;
            string source = string.Format(CI, "Jobs: bar {0} of {1} ({2}){3}", plan.Bar.Title, jobName, plan.Bar.Code, trial ? " - TRIAL" : "");
            using (PlcWriteLog.Source(source))
            {
                foreach (var b in blocks)
                {
                    int rc;
                    try { rc = block != null ? block.WriteBlock(b.Address, b.Words) : PlcBlocks.WriteByDevice(driver, b.Address, b.Words); }
                    catch (Exception ex) { rc = -1; r.Message = ex.Message; }
                    if (rc != 0)
                    {
                        r.Message = string.Format(CI, "the PLC refused {0} at {1} (code {2}){3}", b.What, b.Address, rc,
                                                  r.Blocks > 0 ? " - " + r.Blocks + " part(s) before it were written" : "") + (r.Message.Length > 0 ? ": " + r.Message : "");
                        AuditLogger.Log("[JOB] " + source + ": " + r.Message);
                        return r;
                    }
                    r.Blocks++;
                    r.Words += b.Words.Length;
                }
            }
            r.Ok = true;
            AuditLogger.Log(string.Format(CI, "[JOB] {0} sent: {1} steps (IDs {2} - {3}), {4} milling operation(s), {5} pieces", source,
                plan.Steps.Count, plan.FirstNo, plan.LastNo, plan.Milling.Count, plan.Bar.Pieces.Count));
            return r;
        }
    }
}
