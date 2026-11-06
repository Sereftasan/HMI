using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// THE LOG RECORDS THE HMI TAKES WHEN THEIR TRIGGERS COME (LogRecordHMI, 5 November) - found as the pages are discovered
    /// (AlarmHelpLinks), their trigger and their values read by the scanner while they are taken, each written as the machine's
    /// record (MachineRecords) the scan its trigger becomes true:
    ///
    ///   taken      a new machine (MachineRecords.On: HMI Settings > Options, its log database) and the record's switch on
    ///              (its row, the machine's "hmi_records": record_16).
    ///   its values in order, each named by its TextKey's English text - or, empty, the state map's name of the value at that
    ///              place (the PLC's record it replaces) - a value not read is left out.
    /// </summary>
    public static class RecordTriggers
    {
        /// <summary>A record as found on its page (a copy: the page itself is gone after discovery).</summary>
        public sealed class Definition
        {
            public string ModuleId = "";
            public int Station = 1, Code;
            public string TextKey = "";
            public PlcTrigger Trigger = new PlcTrigger();
            public readonly List<RecordValue> Values = new List<RecordValue>();
            /// <summary>The trigger the scan before (null: not read yet - a trigger already true at the start is not a record).</summary>
            internal bool? Was;
            /// <summary>The HMI turns the trigger off once the record is in the database (the PLC waits for it).</summary>
            public bool ResetTrigger = true;
            /// <summary>The handshake: 0 waiting for the trigger, 1 taken (being written), 2 written - the trigger being turned off.</summary>
            internal volatile int Stage;
            internal DateTime ResetAsked;

            /// <summary>A bit that comes ON - the only trigger the HMI can turn off again.</summary>
            public bool Resettable
            {
                get { return ResetTrigger && HasTrigger && Trigger.TriggerType == Kaban.Core.Enums.TriggerType.ON; }
            }

            public bool HasTrigger { get { return Trigger != null && Trigger.TriggerType != Kaban.Core.Enums.TriggerType.None && !string.IsNullOrWhiteSpace(Trigger.MonitorAddress); } }
        }

        private static readonly object Gate = new object();
        private static readonly List<Definition> _all = new List<Definition>();

        public static IList<Definition> All { get { lock (Gate) return _all.ToList(); } }

        /// <summary>A record found on a page (the same code of the same machine is added once).</summary>
        public static Definition Register(LogRecordHMI c)
        {
            if (c == null || c.RecordCode <= 0) return null;
            var d = new Definition
            {
                ModuleId = c.ModuleId ?? "", Station = c.PlcNumber, Code = c.RecordCode, TextKey = c.TextKey ?? "",
                Trigger = Copy(c.Trigger), ResetTrigger = c.ResetTrigger
            };
            foreach (var v in c.Values)
            {
                var n = v as RecordNumber;
                var l = v as RecordLamp;
                if (n != null) d.Values.Add(new RecordNumber(n.TextKey, n.Address, n.DataType, n.Divisor));
                else if (l != null) d.Values.Add(new RecordLamp(l.TextKey, l.Address));
            }
            lock (Gate)
            {
                if (_all.Any(x => x.Code == d.Code && string.Equals(x.ModuleId, d.ModuleId, StringComparison.OrdinalIgnoreCase))) return null;
                _all.Add(d);
            }
            NameInMap(d);
            AlarmGeneration.RecordsChanged();
            return d;
        }

        private static PlcTrigger Copy(PlcTrigger t)
        {
            if (t == null) return new PlcTrigger();
            return new PlcTrigger
            {
                TriggerType = t.TriggerType, DescriptionKey = t.DescriptionKey, MonitorAddress = (t.MonitorAddress ?? "").Trim(),
                MonitorDataType = t.MonitorDataType, ValA = t.ValA, OpA = t.OpA, OpB = t.OpB, ValB = t.ValB
            };
        }

        /// <summary>Forgets them (a new discovery).</summary>
        public static void Clear()
        {
            lock (Gate) _all.Clear();
        }

        private static string English(string keyOrText)
        {
            if (string.IsNullOrWhiteSpace(keyOrText)) return "";
            return LanguageManager.GetStringIn("EN", keyOrText) ?? keyOrText;
        }

        /// <summary>The names the dashboard shows: the record's own (its code not the map's) and its values', in each language.</summary>
        private static void NameInMap(Definition d)
        {
            try
            {
                var board = DashboardService.Find(d.ModuleId);
                if (board == null) return;
                var map = board.Map;
                if (map.Find(d.Code) == null && d.TextKey.Length > 0) map.AddNamed(d.Code, English(d.TextKey), d.TextKey);
                foreach (var v in d.Values)
                    if (!string.IsNullOrWhiteSpace(v.TextKey)) map.AddText(English(v.TextKey), v.TextKey);
            }
            catch (Exception ex) { AuditLogger.Log("machine records: record " + d.Code + " not named in the state map - " + ex.Message); }
        }

        /// <summary>Taken now for its machine.</summary>
        public static bool Taken(Definition d)
        {
            if (d == null || !MachineRecords.On(d.ModuleId)) return false;
            var board = DashboardService.Find(d.ModuleId);
            return board != null && board.Config.RecordOn(LogRecordHMI.SettingKey(d.Code));
        }

        /// <summary>The devices to read, per station - of the records taken now.</summary>
        public static IEnumerable<KeyValuePair<int, string>> Addresses()
        {
            foreach (var d in All)
            {
                if (!Taken(d)) continue;
                if (d.HasTrigger) yield return new KeyValuePair<int, string>(d.Station, d.Trigger.MonitorAddress.Trim());
                foreach (var v in d.Values)
                    foreach (var a in v.Devices()) yield return new KeyValuePair<int, string>(d.Station, a);
            }
        }

        /// <summary>One scan of a station: a record whose trigger has become true is taken.</summary>
        public static void Scan(int station, Func<string, int> lookup, DateTime now)
        {
            foreach (var d in All)
            {
                if (d.Station != station) continue;
                if (!d.HasTrigger || !Taken(d)) { d.Was = null; d.Stage = 0; continue; }
                bool on;
                try { on = d.Trigger.Check(lookup); } catch { continue; }
                if (d.Resettable)
                {
                    // THE HANDSHAKE (6 November: "The log recording triggers should be reset by the HMI after the recording so the
                    // PLC knows the HMI recorded it and doesn't start the next operation until the HMI record completed"): the PLC
                    // holds the bit; the HMI takes the record while it is on (on its first scan too - the PLC is waiting), turns it
                    // off once the record is in the database, and takes the next only after it has seen it off.
                    if (d.Stage == 0 && on) { d.Stage = 1; Take(d, lookup, now); }
                    else if (d.Stage == 2)
                    {
                        if (!on) d.Stage = 0;
                        else if ((DateTime.UtcNow - d.ResetAsked).TotalSeconds >= 2) ResetNow(d);    // the write did not take: again
                    }
                    d.Was = on;
                    continue;
                }
                bool rising = d.Was.HasValue && !d.Was.Value && on;
                d.Was = on;
                if (rising) Take(d, lookup, now);
            }
        }

        /// <summary>The record written with its values as they are now.</summary>
        public static void Take(Definition d, Func<string, int> lookup, DateTime now)
        {
            var board = DashboardService.Find(d.ModuleId);
            StateMap.Entry entry = board != null ? board.Map.Find(d.Code) : null;
            var details = new List<LogDatabase.Detail>();
            for (int i = 0; i < d.Values.Count; i++)
            {
                var v = d.Values[i];
                var value = v.ReadFrom(lookup);
                if (!value.HasValue) continue;
                string name = English(v.TextKey);
                if (name.Length == 0 && entry != null && i < entry.Parameters.Count) name = entry.Parameters[i];
                if (name.Length == 0) name = (v.Address ?? "").Trim();
                details.Add(new LogDatabase.Detail(name, Math.Round(value.Value, 6), null));
            }
            if (!d.Resettable) { MachineRecords.Add(d.ModuleId, d.Code, now, details); return; }
            var def = d;
            int done = 0;
            Action<bool> finished = written => { if (System.Threading.Interlocked.Exchange(ref done, 1) == 0) Recorded(def, written); };
            MachineRecords.AddDated(d.ModuleId, d.Code, MachineRecords.ToUtc(now), details, finished);
            // the database not answering: the record stays queued, the machine is not held for it
            if (System.Threading.Volatile.Read(ref done) == 0)
                new System.Threading.Timer(_ =>
                {
                    if (System.Threading.Interlocked.Exchange(ref done, 1) != 0) return;
                    AuditLogger.Log("Machine records (" + def.ModuleId + "): record " + def.Code + " not in the database after " + WriteWaitMs / 1000 +
                                    " s - kept to be written; " + def.Trigger.MonitorAddress + " turned off so the machine goes on.");
                    Recorded(def, true);
                }, null, WriteWaitMs, System.Threading.Timeout.Infinite);
        }

        /// <summary>How long the PLC waits for a record the database does not take (it stays queued) before the trigger is turned off anyway.</summary>
        internal static int WriteWaitMs = 5000;

        /// <summary>The trigger turned off - the record written (or kept queued, its database not answering).</summary>
        private static void Recorded(Definition d, bool written)
        {
            if (d.Stage != 1) return;
            d.Stage = 2;
            if (!written) AuditLogger.Log("Machine records (" + d.ModuleId + "): record " + d.Code + " not written (no log database set) - " + d.Trigger.MonitorAddress + " turned off so the machine goes on.");
            ResetNow(d);
        }

        /// <summary>Writes the trigger's bit 0 (PLC <c>station</c>).</summary>
        internal static Func<int, string, bool> WriteOff = (station, address) =>
        {
            var drv = PlcManager.Instance.GetDriverForWrite(station);
            if (drv == null) return false;
            using (Kaban.UI.Helpers.WriteSource.For("Machine records: the record taken, its trigger turned off"))
                return drv.Write(address, 0) == 0;
        };

        private static void ResetNow(Definition d)
        {
            d.ResetAsked = DateTime.UtcNow;
            string a = d.Trigger.MonitorAddress.Trim();
            int st = PlcAddressHelper.StationOf(d.Station, a);
            bool ok = false;
            try { ok = WriteOff(st, PlcAddressHelper.DeviceOf(a)); } catch { }
            if (!ok) AuditLogger.Log("Machine records (" + d.ModuleId + "): record " + d.Code + " taken, but " + a + " could not be turned off - tried again.");
        }
    }
}
