using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kaban.Core.Enums;

namespace Kaban.Core.Machines
{
    /// <summary>
    /// Composes one MAIN machine plus any number of SUB machines into a single HMI.
    ///
    /// Usage from a machine's Program.cs:
    ///     MachineRegistry.MountMain(new FA2010Module());
    ///     MachineRegistry.MountSub(new H500Module(), stationMap: new[]{ 3 });
    ///     ... then shell.BuildNavigation();
    ///
    /// The registry hands every module a mount context that:
    ///   - translates the module's LOCAL station numbers to real ones,
    ///   - allocates condition bits so two modules never claim the same bit,
    ///   - namespaces ids with the module id,
    ///   - resolves the module's own data files.
    /// </summary>
    public static class MachineRegistry
    {
        private static readonly List<MountedModule> _modules = new List<MountedModule>();
        private static int _nextMachineBit = 16; // bits 0-15 belong to SystemCondition

        public class MountedModule
        {
            public IMachineModule Module { get; set; }
            public MountContext Context { get; set; }
            public bool IsMain { get; set; }
            public List<MachineScreen> Screens { get; set; } = new List<MachineScreen>();
        }

        public static IReadOnlyList<MountedModule> Modules => _modules;
        public static MountedModule Main => _modules.FirstOrDefault(m => m.IsMain);

        /// <summary>Clears all mounts (test / restart support).</summary>
        public static void Reset()
        {
            _modules.Clear();
            _nextMachineBit = 16;
        }

        /// <summary>Mounts the machine that owns this HMI.</summary>
        public static MountedModule MountMain(IMachineModule module, int[] stationMap = null, string baseFolder = null)
            => Mount(module, true, stationMap, baseFolder);

        /// <summary>
        /// Mounts a sub-machine into the host HMI. stationMap maps the module's
        /// local stations (1,2,...) onto this deployment's real station numbers,
        /// e.g. new[]{3} means "the module's station 1 is physically station 3".
        /// </summary>
        public static MountedModule MountSub(IMachineModule module, int[] stationMap = null, string baseFolder = null)
            => Mount(module, false, stationMap, baseFolder);

        private static MountedModule Mount(IMachineModule module, bool isMain, int[] stationMap, string baseFolder)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (_modules.Any(m => string.Equals(m.Module.ModuleId, module.ModuleId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A module with id '{module.ModuleId}' is already mounted.");
            if (isMain && Main != null)
                throw new InvalidOperationException("A main machine is already mounted.");

            var ctx = new MountContext(module.ModuleId, isMain, stationMap, baseFolder);
            var mounted = new MountedModule { Module = module, Context = ctx, IsMain = isMain };

            module.RegisterPlc(ctx);

            // A mounted machine's device labels belong to ITS station, not the host's.
            if (!isMain)
            {
                var labels = ctx.ResolveFile("PlcLabels.csv");
                if (!string.IsNullOrEmpty(labels) && System.IO.File.Exists(labels))
                    Kaban.Core.Helpers.PlcLabelTable.LoadStation(labels, ctx.Station(1));
            }
            module.RegisterConditions(ctx);
            module.RegisterData(ctx);

            var screens = module.GetScreens(ctx);
            if (screens != null) mounted.Screens.AddRange(screens);

            _modules.Add(mounted);
            return mounted;
        }

        /// <summary>All screens from all modules, main machine first, then sub-machines.</summary>
        public static IEnumerable<KeyValuePair<MountedModule, MachineScreen>> AllScreens()
        {
            foreach (var m in _modules.OrderByDescending(m => m.IsMain))
                foreach (var s in m.Screens.OrderBy(s => s.SortOrder))
                    yield return new KeyValuePair<MountedModule, MachineScreen>(m, s);
        }

        /// <summary>The side pages of every mounted machine (ScreenRoles.SidePage), main machine first.</summary>
        public static IEnumerable<KeyValuePair<MountedModule, MachineScreen>> SidePages()
        {
            return AllScreens().Where(kv => string.Equals(kv.Value.Role ?? "", ScreenRoles.SidePage, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>One navigation entry: either a single screen or a set of tabs.</summary>
        public class RoleGroup
        {
            public string Role = "";
            public string TitleKey = "";
            public string TitleFallback = "";
            public Func<object> Icon;
            public int SortOrder = 100;
            public SecurityLevel RequiredLevel = SecurityLevel.None;

            /// <summary>The machines contributing a page, main first.</summary>
            public List<KeyValuePair<MountedModule, MachineScreen>> Pages =
                new List<KeyValuePair<MountedModule, MachineScreen>>();

            /// <summary>True when this entry needs a tab strip (more than one machine).</summary>
            public bool IsTabbed { get { return Pages.Count > 1; } }
        }

        /// <summary>
        /// Collapses screens that share a Role into ONE entry with a page per
        /// machine, so the left bar looks the same on every machine whether or not
        /// sub-machines are mounted. Screens with no Role keep their own button.
        /// </summary>
        public static List<RoleGroup> NavigationGroups()
        {
            var groups = new List<RoleGroup>();
            var byRole = new Dictionary<string, RoleGroup>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in AllScreens())
            {
                var mounted = pair.Key;
                var screen = pair.Value;
                string role = screen.Role ?? "";
                // Side pages are on the right-hand bar, not in the menu (see SidePages).
                if (string.Equals(role, ScreenRoles.SidePage, StringComparison.OrdinalIgnoreCase)) continue;

                if (role.Length == 0)
                {
                    groups.Add(new RoleGroup
                    {
                        Role = "",
                        TitleKey = screen.TitleKey,
                        TitleFallback = screen.TitleFallback,
                        Icon = screen.Icon,
                        SortOrder = screen.SortOrder,
                        RequiredLevel = screen.RequiredLevel,
                        Pages = { new KeyValuePair<MountedModule, MachineScreen>(mounted, screen) }
                    });
                    continue;
                }

                RoleGroup g;
                if (!byRole.TryGetValue(role, out g))
                {
                    // the MAIN machine defines the caption and icon for the role
                    g = new RoleGroup
                    {
                        Role = role,
                        TitleKey = screen.TitleKey,
                        TitleFallback = screen.TitleFallback,
                        Icon = screen.Icon,
                        SortOrder = screen.SortOrder,
                        RequiredLevel = screen.RequiredLevel
                    };
                    byRole[role] = g;
                    groups.Add(g);
                }
                // the strictest level across the machines wins
                if (screen.RequiredLevel > g.RequiredLevel) g.RequiredLevel = screen.RequiredLevel;
                g.Pages.Add(new KeyValuePair<MountedModule, MachineScreen>(mounted, screen));
            }

            groups.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            return groups;
        }

        internal static long NextMachineBit()
        {
            if (_nextMachineBit > 63)
                throw new InvalidOperationException(
                    "Out of machine condition bits (max 64). Too many modules/conditions mounted.");
            return 1L << _nextMachineBit++;
        }

        /// <summary>Concrete mount context handed to each module.</summary>
        public class MountContext : IMachineMountContext
        {
            private readonly int[] _stationMap;
            private readonly string _baseFolder;
            private readonly Dictionary<string, long> _bits = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            public MountContext(string moduleId, bool isMain, int[] stationMap, string baseFolder)
            {
                ModuleId = moduleId;
                IsMainMachine = isMain;
                _stationMap = stationMap;
                _baseFolder = baseFolder ?? AppDomain.CurrentDomain.BaseDirectory;
            }

            public string ModuleId { get; }
            public bool IsMainMachine { get; }

            /// <summary>
            /// Every one of the module's OWN PLC numbers that has been mapped, and
            /// what it resolved to: local -> real station id.
            ///
            /// Recorded rather than configured, because a module never declares "I
            /// have two PLCs" anywhere - it simply calls Station(1) and Station(2)
            /// while registering. Watching those calls is therefore the only honest
            /// inventory of a machine's controllers, and it is what the Station
            /// Connection page and the simulation monitor's tabs are built from.
            /// </summary>
            public IEnumerable<KeyValuePair<int, int>> MappedStations
            {
                get { return _mapped; }
            }

            private readonly Dictionary<int, int> _mapped = new Dictionary<int, int>();

            public int Station(int localStationNumber)
            {
                int real = localStationNumber;
                if (_stationMap != null)
                {
                    int idx = localStationNumber - 1;
                    if (idx >= 0 && idx < _stationMap.Length) real = _stationMap[idx];
                }
                _mapped[localStationNumber] = real;
                return real;
            }

            public string Scoped(string localId) =>
                string.IsNullOrEmpty(localId) ? ModuleId : ModuleId + "." + localId;

            public long AllocateMachineBit(string localTag)
            {
                long bit;
                if (_bits.TryGetValue(localTag, out bit)) return bit;   // stable per tag
                bit = NextMachineBit();
                _bits[localTag] = bit;
                return bit;
            }

            public string ResolveFile(string fileName)
            {
                if (string.IsNullOrEmpty(fileName)) return "";
                // Prefer a per-module subfolder so mounted machines can ship their
                // own Alarms.json/Translations.csv alongside the host's.
                string scoped = Path.Combine(_baseFolder, ModuleId, fileName);
                if (File.Exists(scoped)) return scoped;
                return Path.Combine(_baseFolder, fileName);
            }
        }
    }
}

