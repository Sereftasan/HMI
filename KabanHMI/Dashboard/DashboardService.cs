using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Kaban.Core.Helpers;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// What a machine ships for its dashboard, next to its Alarms.json: the default
    /// settings (the Industry 4 program's config.json), the state map (its
    /// sample.json) and the texts of the state map in each language
    /// (Translation_JSON.csv). Given to StandardScreens.Options.Dashboard.
    /// </summary>
    public sealed class DashboardDefinition
    {
        public string ConfigFile = Path.Combine("Dashboard", "config.json");
        public string StateMapFile = Path.Combine("Dashboard", "StateMap.json");
        public string TranslationFile = Path.Combine("Dashboard", "Translation_JSON.csv");
    }

    /// <summary>One machine's dashboard: its settings (shared by the page, the settings page and the reports) and its state map.</summary>
    public sealed class MachineDashboard
    {
        public string ModuleId = "";
        public string ShippedConfig = "", StateMapPath = "", TranslationPath = "";

        /// <summary>Held while the settings are changed or written.</summary>
        public readonly object Sync = new object();

        private DashboardConfig _config;
        private StateMap _map;

        /// <summary>Raised (on any thread) when the settings were replaced from the settings page.</summary>
        public event EventHandler ConfigChanged;

        /// <summary>Raised (on the report thread) when automatic reports were made.</summary>
        public event EventHandler<DashboardReports.RunResult> ReportsMade;

        public DashboardConfig Config
        {
            get
            {
                lock (Sync)
                {
                    if (_config == null) _config = DashboardConfig.Load(ModuleId, ShippedConfig);
                    return _config;
                }
            }
        }

        public StateMap Map
        {
            get
            {
                lock (Sync)
                {
                    if (_map == null) _map = StateMap.Load(StateMapPath, TranslationPath);
                    return _map;
                }
            }
        }

        /// <summary>Changes the live settings and writes them.</summary>
        public void Change(Action<DashboardConfig> change)
        {
            lock (Sync)
            {
                change(Config);
                Config.Save();
            }
        }

        /// <summary>Replaces the settings (the settings page's Save) and tells the page.</summary>
        public bool Replace(DashboardConfig c)
        {
            bool ok;
            lock (Sync)
            {
                _config = c;
                ok = c.Save();
            }
            var h = ConfigChanged;
            if (h != null) h(this, EventArgs.Empty);
            return ok;
        }

        internal void OnReports(DashboardReports.RunResult r)
        {
            var h = ReportsMade;
            if (h != null) h(this, r);
        }

        private int _running;

        /// <summary>
        /// Makes the automatic reports when they are switched on and one is due (on
        /// the calling thread). Null when nothing was due.
        /// </summary>
        public DashboardReports.RunResult RunDueReports()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1) return null;
            try
            {
                DashboardConfig work;
                lock (Sync) work = Config.Clone();
                var zone = DashboardTime.Find(work.TimeZoneName);
                var now = DashboardTime.Now(zone);
                if (!DashboardReports.Due(work, now)) return null;
                if (string.IsNullOrWhiteSpace(work.DatabasePath) || !File.Exists(work.DatabasePath)) return null;

                var r = DashboardReports.RunDue(work, Map, now);
                if (r.NextDue.HasValue)
                {
                    Change(c => c.RequiredReportEnd = r.NextDue);
                    try
                    {
                        AuditLogger.Log("Dashboard (" + ModuleId + "): automatic report, " + r.Files.Count + " file(s) in " +
                                        work.ExportDirectory + "; next due " +
                                        r.NextDue.Value.ToString(DashboardConfig.DateFormatUi, CultureInfo.InvariantCulture) +
                                        (r.Notes.Count > 0 ? " (" + string.Join("; ", r.Notes) + ")" : ""));
                    }
                    catch { }
                    OnReports(r);
                }
                return r;
            }
            catch (Exception ex)
            {
                try { AuditLogger.Log("Dashboard (" + ModuleId + "): the automatic report failed: " + ex.Message); } catch { }
                return null;
            }
            finally { Interlocked.Exchange(ref _running, 0); }
        }
    }

    /// <summary>
    /// THE DASHBOARDS OF THE MOUNTED MACHINES, and the clock of their automatic
    /// reports: once a minute, on a thread of its own, each machine's due report is
    /// made - whether or not the page was ever opened, as the old program's
    /// scheduled task did.
    /// </summary>
    public static class DashboardService
    {
        private static readonly Dictionary<string, MachineDashboard> _machines =
            new Dictionary<string, MachineDashboard>(StringComparer.OrdinalIgnoreCase);
        private static Timer _timer;

        /// <summary>False stops the clock (tests, and the page designer).</summary>
        public static bool AutoReports = true;

        public static MachineDashboard Register(string moduleId, string shippedConfig, string stateMap, string translations)
        {
            MachineDashboard m;
            lock (_machines)
            {
                if (!_machines.TryGetValue(moduleId ?? "", out m))
                    _machines[moduleId ?? ""] = m = new MachineDashboard { ModuleId = moduleId ?? "" };
                m.ShippedConfig = shippedConfig ?? "";
                m.StateMapPath = stateMap ?? "";
                m.TranslationPath = translations ?? "";
                if (_timer == null)
                    _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));
            }
            return m;
        }

        public static MachineDashboard Find(string moduleId)
        {
            lock (_machines)
            {
                MachineDashboard m;
                return _machines.TryGetValue(moduleId ?? "", out m) ? m : null;
            }
        }

        public static List<MachineDashboard> All
        {
            get { lock (_machines) return _machines.Values.ToList(); }
        }

        private static void Tick()
        {
            if (!AutoReports) return;
            foreach (var m in All) m.RunDueReports();
        }
    }
}
