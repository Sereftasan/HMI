using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// A MACHINE'S ALARM HISTORY FROM ITS LOG (30 October: the two alarm handlings combined - "Yes, from
    /// the log": the Alarms page's History "shows the machine's alarms from the log database, so it
    /// survives restarts").
    ///
    /// The PLC's error records in the machine's log database (state 4 and its continuations - written by
    /// the HMI or by the Industry 4 service) with each bit that was on: an alarm of the HMI's list where
    /// the bit is one (AlarmDefinition.Log - its text in the screen's language, severity, address, help),
    /// else the bit's own name in the screen's language. With them the alarms the HMI raised itself (a
    /// cylinder movement not confirmed, a drive's code - their cause the log does not have) and those of
    /// the HMI's list the log does not record, from the HMI's own history; a raised alarm's device seen in
    /// the log at the same moment is listed once (the raised one, with its cause).
    /// </summary>
    public static class MachineAlarmLog
    {
        /// <summary>The most records read.</summary>
        public const int MaxRecords = 500;

        /// <summary>The database the history was read from (tests, the page's line); "" when none.</summary>
        public sealed class Result
        {
            public List<AlarmItem> Items = new List<AlarmItem>();
            public string Database = "";
            /// <summary>Records read from the log (the rest of the items: the HMI's own).</summary>
            public int FromLog;
            public string Problem = "";
        }

        /// <summary>
        /// The machine's history: from its log when it keeps one that is there (null otherwise - the
        /// page shows the HMI's own history then), newest first.
        /// </summary>
        public static Result Read(string moduleId, IEnumerable<AlarmItem> hmiHistory)
        {
            var machine = string.IsNullOrEmpty(moduleId) ? null : DashboardService.Find(moduleId);
            if (machine == null) return null;
            var cfg = machine.Config;
            string path = cfg.DatabasePath ?? "";
            if (path.Length == 0 || !File.Exists(path)) return null;
            var res = new Result { Database = path };
            var map = machine.Map;
            var zone = DashboardTime.Find(cfg.TimeZoneName);
            var codes = DashboardData.AlarmCodesOf(map);

            // a bit's name - where the log has it (the record's code, its value, the bit)
            var places = new Dictionary<string, AlarmDefinitionLoader.LogPlace>(StringComparer.Ordinal);
            foreach (var e in map.Entries.Where(x => codes.Contains(x.Code)))
                foreach (var p in e.Params)
                    for (int i = 0; i < p.Bits.Count; i++)
                    {
                        var name = p.Bits[i] ?? "";
                        if (name.Trim().Length > 0 && !places.ContainsKey(name))
                            places[name] = new AlarmDefinitionLoader.LogPlace { Code = e.Code, Value = p.Description, Bit = i };
                    }
            // the HMI's alarm at each place
            var defs = new Dictionary<string, AlarmDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in AlarmManager.GetAllDefinitions())
                if (d.Log != null && string.Equals(d.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                    defs[Key(d.Log.Code, d.Log.Value, d.Log.Bit)] = d;

            var fromLog = new List<AlarmItem>();
            try
            {
                using (var c = DashboardData.Open(path))
                {
                    var records = new List<KeyValuePair<long, DateTime>>();
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.CommandText = "SELECT id, state_date FROM main_log WHERE state_code IN (" +
                                          string.Join(",", codes.Select(x => x.ToString(CultureInfo.InvariantCulture))) +
                                          ") ORDER BY state_date DESC LIMIT " + MaxRecords.ToString(CultureInfo.InvariantCulture);
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                            {
                                var t = DashboardData.ParseDb(r.GetValue(1));
                                if (t.HasValue) records.Add(new KeyValuePair<long, DateTime>(Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture), t.Value));
                            }
                    }
                    if (records.Count > 0)
                    {
                        var bits = new Dictionary<long, List<string>>();
                        using (var cmd = c.CreateCommand())
                        {
                            cmd.CommandText = "SELECT main_log_id, log_description, deger FROM log_details WHERE main_log_id IN (" +
                                              string.Join(",", records.Select(x => x.Key.ToString(CultureInfo.InvariantCulture))) + ") ORDER BY id";
                            using (var r = cmd.ExecuteReader())
                                while (r.Read())
                                {
                                    long id = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                                    var name = r.IsDBNull(1) ? "" : Convert.ToString(r.GetValue(1), CultureInfo.InvariantCulture);
                                    double v = r.IsDBNull(2) ? 0 : Convert.ToDouble(r.GetValue(2), CultureInfo.InvariantCulture);
                                    if (name.Length == 0 || v == 0 || !places.ContainsKey(name)) continue;   // a bit that was on
                                    List<string> list;
                                    if (!bits.TryGetValue(id, out list)) bits[id] = list = new List<string>();
                                    list.Add(name);
                                }
                        }
                        foreach (var rec in records)
                        {
                            List<string> on;
                            if (!bits.TryGetValue(rec.Key, out on)) continue;
                            var local = DashboardTime.ToLocal(rec.Value, zone);
                            foreach (var name in on)
                            {
                                var place = places[name];
                                AlarmDefinition def;
                                defs.TryGetValue(Key(place.Code, place.Value, place.Bit), out def);
                                fromLog.Add(def != null
                                    ? new AlarmItem(def.AlarmCode, def.TranslationKey, def.Severity) { TriggerTime = local, Address = def.Address ?? "", Station = AlarmManager.StationOf(def) }
                                    : new AlarmItem(moduleId + "." + place.Value + "." + place.Bit, BitText(map, name), AlarmSeverity.Fault) { TriggerTime = local });
                                if (fromLog.Count >= MaxRecords) break;
                            }
                            if (fromLog.Count >= MaxRecords) break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                res.Problem = ex.Message;
                return null;
            }

            // the HMI's own: what it raised itself, and its list's alarms the log does not record
            var own = (hmiHistory ?? Enumerable.Empty<AlarmItem>()).Where(a => a != null && (a.Generated || !Logged(a))).ToList();
            foreach (var g in own.Where(a => a.Generated && !string.IsNullOrEmpty(a.Device)))
                fromLog.RemoveAll(x => string.Equals(x.Address, g.Device, StringComparison.OrdinalIgnoreCase) && Math.Abs((x.TriggerTime - g.TriggerTime).TotalSeconds) <= 10);
            res.FromLog = fromLog.Count;
            res.Items = fromLog.Concat(own).OrderByDescending(a => a.TriggerTime).Take(MaxRecords).ToList();
            return res;
        }

        private static string Key(int code, string value, int bit)
        {
            return code.ToString(CultureInfo.InvariantCulture) + "|" + (value ?? "").Trim() + "|" + bit.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>An alarm of the HMI's list the PLC's log records (its place known).</summary>
        private static bool Logged(AlarmItem a)
        {
            var d = AlarmManager.FindByCode(a.AlarmCode);
            return d != null && d.Log != null;
        }

        /// <summary>A bit the HMI's list has no alarm for: its name in the screen's language (the dashboard's translations).</summary>
        private static string BitText(StateMap map, string name)
        {
            var column = StateMap.ColumnOf(LanguageManager.CurrentLanguage);
            return map.Translate(name, column);
        }
    }
}
