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
                Trigger = Copy(c.Trigger)
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
                if (!d.HasTrigger || !Taken(d)) { d.Was = null; continue; }
                bool on;
                try { on = d.Trigger.Check(lookup); } catch { continue; }
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
            MachineRecords.Add(d.ModuleId, d.Code, now, details);
        }
    }
}
