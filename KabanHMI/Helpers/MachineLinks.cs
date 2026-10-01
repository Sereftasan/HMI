using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Machines;

namespace Kaban.UI.Helpers
{
    /// <summary>One machine as the HMI's own pages see it.</summary>
    public sealed class MachineLink
    {
        public string ModuleId = "";
        /// <summary>As the machine strip names it ("FA2010 Processing Center").</summary>
        public string Name = "";
        public bool IsMain;
        /// <summary>The HMI stations of its controllers, its first PLC first.</summary>
        public List<int> Stations = new List<int>();
        /// <summary>Its log database and reports, or null when it has none.</summary>
        public Kaban.UI.Dashboard.MachineDashboard Dashboard;

        /// <summary>The station of its first PLC (1 when none was mapped).</summary>
        public int MainStation { get { return Stations.Count > 0 ? Stations[0] : 1; } }

        public override string ToString() { return Name; }
    }

    /// <summary>
    /// THE MACHINES, FOR THE HMI'S OWN PAGES (2 October). A machine brings its pages,
    /// its PLCs and its log database; the HMI's pages - Logs and Reports, and
    /// whatever else is the HMI's rather than one machine's - need to know every
    /// machine that is mounted: its name, its stations, its log database. This is
    /// that one list, so such a page shows each machine's part side by side (a
    /// machine to choose, a Source filter) instead of each machine putting its own
    /// copy of the page among its Machine Parameters.
    /// </summary>
    public static class MachineLinks
    {
        /// <summary>The mounted machines, the main one first - and any log database registered without one (tests).</summary>
        public static List<MachineLink> All()
        {
            var list = new List<MachineLink>();
            try
            {
                foreach (var m in MachineRegistry.Modules)
                {
                    if (m == null || m.Module == null) continue;
                    var id = m.Context != null && !string.IsNullOrEmpty(m.Context.ModuleId) ? m.Context.ModuleId : m.Module.ModuleId ?? "";
                    var link = new MachineLink
                    {
                        ModuleId = id,
                        Name = Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(m),
                        IsMain = m.IsMain,
                        Dashboard = Kaban.UI.Dashboard.DashboardService.Find(id)
                    };
                    if (m.Context != null)
                        foreach (var kv in m.Context.MappedStations.OrderBy(k => k.Key))
                            if (!link.Stations.Contains(kv.Value)) link.Stations.Add(kv.Value);
                    if (link.Stations.Count == 0 && m.IsMain) link.Stations.Add(1);
                    list.Add(link);
                }
            }
            catch { }
            foreach (var d in Kaban.UI.Dashboard.DashboardService.All)
                if (!list.Any(l => string.Equals(l.ModuleId, d.ModuleId, StringComparison.OrdinalIgnoreCase)))
                    list.Add(new MachineLink { ModuleId = d.ModuleId, Name = d.ModuleId, Dashboard = d, Stations = { 1 } });
            return list.OrderBy(l => l.IsMain ? 0 : 1).ToList();
        }

        /// <summary>The machines that have a log database.</summary>
        public static List<MachineLink> WithLogs()
        {
            return All().Where(l => l.Dashboard != null).ToList();
        }

        public static MachineLink Find(string moduleId)
        {
            return All().FirstOrDefault(l => string.Equals(l.ModuleId, moduleId ?? "", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The machine a station belongs to, or null.</summary>
        public static MachineLink OfStation(int station)
        {
            return All().FirstOrDefault(l => l.Stations.Contains(station));
        }

        /// <summary>A machine's name for a list, or its id when it is not mounted.</summary>
        public static string NameOf(string moduleId)
        {
            var l = Find(moduleId);
            return l != null ? l.Name : moduleId ?? "";
        }
    }
}
