using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Kaban.Core.Models;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// A MAINTENANCE TASK (the old program's Cleaning Settings - "show a cleaning alert" after so many bars - for any task): its
    /// name, its machine, how often - every so many days and / or every so many counts of a PLC counter (the machine's cycles,
    /// its bars, its saw's cuts) - when it was done last and the counter then.
    /// </summary>
    public sealed class MaintenanceTask
    {
        public string Id = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string Name = "", Machine = "", Notes = "", DoneBy = "";
        public int EveryDays;
        /// <summary>The PLC's counter ("D500"; "D500@2" another PLC of the machine) and how many counts between two.</summary>
        public string Counter = "";
        public bool CounterDouble = true;
        public long EveryCount;
        public DateTime LastDone = DateTime.Now;
        public long CountAtDone;
        public bool Enabled = true;

        /// <summary>The counter as read last (null: not read - no counter, the PLC not connected).</summary>
        public long? CounterNow;
        internal AlarmItem Raised;
        internal DateTime SnoozedUntil = DateTime.MinValue;

        public MaintenanceTask Clone()
        {
            var c = (MaintenanceTask)MemberwiseClone();
            c.Raised = null;
            return c;
        }
    }

    /// <summary>
    /// THE MAINTENANCE PLAN (17 November - from the old program's Cleaning Settings: "check over the program and add the other tools
    /// you find useful"): the tasks of the panel's machines, kept in C:\ProgramData\KabanHMI\Maintenance.cfg. Looked at every
    /// minute: a task that is due is on the Alarms page as a warning ("Maintenance due: ...") until it is marked done (HMI Settings >
    /// Maintenance) - acknowledged there, it comes back after 4 hours while it is still not done. Every task done is in the log.
    /// </summary>
    public static class MaintenancePlan
    {
        public static string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Maintenance.cfg");
        public const string AlarmPrefix = "HMI.MAINT.";
        /// <summary>How long an acknowledged reminder stays away while the task is still not done.</summary>
        public static TimeSpan Snooze = TimeSpan.FromHours(4);
        /// <summary>A machine's PLC (its module id -> its first station); set by the program (Kaban.UI).</summary>
        public static Func<string, int> StationOf = m => 1;

        private static readonly object Gate = new object();
        private static readonly List<MaintenanceTask> _tasks = new List<MaintenanceTask>();
        private static bool _loaded;
        private static System.Threading.Timer _timer;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        public static event EventHandler Changed;

        public static List<MaintenanceTask> All { get { lock (Gate) { Load(); return _tasks.Select(t => t.Clone()).ToList(); } } }

        /// <summary>Reads the file again (tests).</summary>
        public static void Reload() { lock (Gate) { ClearAlarms(); _loaded = false; Load(); } }

        /// <summary>The tasks kept as given (the page); their reminders looked at again.</summary>
        public static void Save(IEnumerable<MaintenanceTask> tasks)
        {
            lock (Gate)
            {
                Load();
                var old = _tasks.ToDictionary(t => t.Id);
                ClearAlarms();
                _tasks.Clear();
                foreach (var t in tasks ?? new MaintenanceTask[0])
                {
                    var c = t.Clone();
                    MaintenanceTask was;
                    if (old.TryGetValue(c.Id, out was)) c.SnoozedUntil = was.SnoozedUntil;
                    else if (c.EveryCount > 0 && c.CountAtDone == 0)
                    {
                        // a new task counts from now: the counter as it is
                        ReadCounter(c);
                        if (c.CounterNow.HasValue) c.CountAtDone = c.CounterNow.Value;
                    }
                    _tasks.Add(c);
                }
                Write();
            }
            AuditLogger.Log("[MAINT] the maintenance plan saved: " + _tasks.Count + " task(s).");
            Notify();
            Check(DateTime.Now);
        }

        /// <summary>A task done now: its date and counter kept, its reminder off.</summary>
        public static void MarkDone(string id, string user, DateTime? at = null)
        {
            MaintenanceTask t;
            lock (Gate)
            {
                Load();
                t = _tasks.FirstOrDefault(x => x.Id == id);
                if (t == null) return;
                ReadCounter(t);
                t.LastDone = at ?? DateTime.Now;
                t.CountAtDone = t.CounterNow ?? t.CountAtDone;
                t.DoneBy = user ?? "";
                t.SnoozedUntil = DateTime.MinValue;
                if (t.Raised != null) { AlarmManager.ClearGenerated(t.Raised); t.Raised = null; }
                Write();
            }
            AuditLogger.Log(string.Format(CI, "[MAINT] {0} done by {1}{2}.", t.Name, string.IsNullOrEmpty(user) ? "-" : user,
                                          t.CounterNow.HasValue ? " (counter " + t.CounterNow.Value + ")" : ""));
            Notify();
        }

        private static void Notify() { var h = Changed; if (h != null) h(null, EventArgs.Empty); }

        // ------------------------------------------------------------------ due

        /// <summary>How far a task is: its days left (negative: overdue) and its counts left (null: not by that).</summary>
        public static void Left(MaintenanceTask t, DateTime now, out double? days, out long? counts)
        {
            days = null; counts = null;
            if (t.EveryDays > 0) days = t.EveryDays - (now - t.LastDone).TotalDays;
            if (t.EveryCount > 0 && t.CounterNow.HasValue)
            {
                // a counter set back to 0 (a new shift, the PLC's reset): counted from 0
                long since = t.CounterNow.Value >= t.CountAtDone ? t.CounterNow.Value - t.CountAtDone : t.CounterNow.Value;
                counts = t.EveryCount - since;
            }
        }

        public static bool IsDue(MaintenanceTask t, DateTime now)
        {
            if (!t.Enabled) return false;
            double? days; long? counts;
            Left(t, now, out days, out counts);
            return (days.HasValue && days.Value <= 0) || (counts.HasValue && counts.Value <= 0);
        }

        /// <summary>What is left in words: "in 5 days", "320 counts left", "due (2 days over)".</summary>
        public static string Describe(MaintenanceTask t, DateTime now)
        {
            if (!t.Enabled) return LanguageManager.GetString("Mnt_Off", "off");
            double? days; long? counts;
            Left(t, now, out days, out counts);
            var parts = new List<string>();
            if (days.HasValue)
                parts.Add(days.Value <= 0 ? string.Format(LanguageManager.GetString("Mnt_DaysOver", "{0} day(s) over"), Math.Floor(-days.Value).ToString(CI))
                                          : string.Format(LanguageManager.GetString("Mnt_DaysLeft", "in {0} day(s)"), Math.Ceiling(days.Value).ToString(CI)));
            if (counts.HasValue)
                parts.Add(counts.Value <= 0 ? string.Format(LanguageManager.GetString("Mnt_CountsOver", "{0} count(s) over"), (-counts.Value).ToString(CI))
                                            : string.Format(LanguageManager.GetString("Mnt_CountsLeft", "{0} count(s) left"), counts.Value.ToString(CI)));
            else if (t.EveryCount > 0) parts.Add(LanguageManager.GetString("Mnt_NoCounter", "the counter not read"));
            var text = string.Join(", ", parts);
            return IsDue(t, now) ? LanguageManager.GetString("Mnt_Due", "DUE") + " - " + text : text;
        }

        // ------------------------------------------------------------------ looked at every minute

        public static void Start()
        {
            lock (Gate)
            {
                if (_timer != null) return;
                _timer = new System.Threading.Timer(_ => { try { Check(DateTime.Now); } catch (Exception ex) { AuditLogger.Log("[MAINT] " + ex.Message); } }, null, 15000, 60000);
            }
        }

        public static void Stop()
        {
            lock (Gate) { if (_timer != null) _timer.Dispose(); _timer = null; }
        }

        /// <summary>The counters read, a due task's reminder raised (or kept), a done one's cleared.</summary>
        public static void Check(DateTime now)
        {
            lock (Gate)
            {
                Load();
                foreach (var t in _tasks)
                {
                    ReadCounter(t);
                    bool due = IsDue(t, now);
                    if (!due)
                    {
                        if (t.Raised != null) { AlarmManager.ClearGenerated(t.Raised); t.Raised = null; }
                        continue;
                    }
                    if (t.Raised != null && AlarmManager.IsOnList(t.Raised)) continue;
                    if (t.Raised != null) { t.SnoozedUntil = now + Snooze; t.Raised = null; }   // acknowledged: back later
                    if (now < t.SnoozedUntil) continue;
                    var item = new AlarmItem(AlarmPrefix + t.Id, string.Format(LanguageManager.GetString("Mnt_Alarm", "Maintenance due: {0}"), t.Name), AlarmSeverity.Warning)
                    {
                        Generated = true,
                        Station = StationOfTask(t),
                        Detail = Describe(t, now) + (t.Notes.Length > 0 ? " - " + t.Notes : ""),
                        TriggerTime = now
                    };
                    t.Raised = item;
                    AlarmManager.RaiseGenerated(item);
                }
            }
        }

        private static int StationOfTask(MaintenanceTask t)
        {
            int st = 1;
            try { st = StationOf(t.Machine ?? ""); } catch { }
            return st;
        }

        private static void ReadCounter(MaintenanceTask t)
        {
            if (string.IsNullOrWhiteSpace(t.Counter) || t.EveryCount <= 0) { t.CounterNow = null; return; }
            try
            {
                int st = PlcAddressHelper.StationOf(StationOfTask(t), t.Counter.Trim());
                string dev = PlcAddressHelper.DeviceOf(t.Counter.Trim());
                var driver = PlcManager.Instance.GetDriverForMonitoring(st);
                if (driver == null || !driver.IsConnected) return;
                var devices = t.CounterDouble ? new[] { dev, PlcAddressHelper.OffsetAddress(dev, 1) } : new[] { dev };
                int[] v;
                if (!driver.TryReadMulti(devices, out v) || v == null || v.Length < devices.Length) return;
                t.CounterNow = t.CounterDouble ? (long)(uint)((v[0] & 0xFFFF) | ((v[1] & 0xFFFF) << 16)) : (v[0] & 0xFFFF);
            }
            catch { }
        }

        private static void ClearAlarms()
        {
            foreach (var t in _tasks)
                if (t.Raised != null) { AlarmManager.ClearGenerated(t.Raised); t.Raised = null; }
        }

        // ------------------------------------------------------------------ the file

        private static string Esc(string s) { return (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " "); }

        private static void Write()
        {
            var sb = new StringBuilder("# Kaban HMI - the maintenance plan (HMI Settings > Maintenance)\r\n");
            foreach (var t in _tasks)
                sb.Append(string.Join("\t", "task", t.Id, Esc(t.Name), Esc(t.Machine), t.EveryDays.ToString(CI), Esc(t.Counter), t.CounterDouble ? "32" : "16",
                                      t.EveryCount.ToString(CI), t.LastDone.ToString("yyyy-MM-dd HH:mm:ss", CI), t.CountAtDone.ToString(CI),
                                      t.Enabled ? "on" : "off", Esc(t.DoneBy), Esc(t.Notes))).Append("\r\n");
            try { SafeFile.WriteAllText(SettingsPath, sb.ToString()); }
            catch (Exception ex) { AuditLogger.Log("[MAINT] " + SettingsPath + " could not be written: " + ex.Message); }
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _tasks.Clear();
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    var p = line.Split('\t');
                    if (p.Length < 13 || p[0] != "task") continue;
                    var t = new MaintenanceTask { Id = p[1], Name = p[2], Machine = p[3], Counter = p[5], CounterDouble = p[6] != "16", Enabled = p[10] == "on", DoneBy = p[11], Notes = p[12] };
                    int n; long l; DateTime d;
                    if (int.TryParse(p[4], NumberStyles.Integer, CI, out n)) t.EveryDays = n;
                    if (long.TryParse(p[7], NumberStyles.Integer, CI, out l)) t.EveryCount = l;
                    if (DateTime.TryParseExact(p[8], "yyyy-MM-dd HH:mm:ss", CI, DateTimeStyles.None, out d)) t.LastDone = d;
                    if (long.TryParse(p[9], NumberStyles.Integer, CI, out l)) t.CountAtDone = l;
                    _tasks.Add(t);
                }
            }
            catch (Exception ex) { AuditLogger.Log("[MAINT] " + SettingsPath + " could not be read: " + ex.Message); }
        }
    }
}
