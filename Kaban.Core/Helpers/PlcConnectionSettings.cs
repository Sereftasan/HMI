using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kaban.Core.Machines;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// WHICH DRIVER, AND WHICH STATION NUMBER, for every machine controller.
    ///
    /// A deployment is not known at build time: the same FA2010 exe runs on a bench
    /// with everything simulated, and on the machine with CPU1 on MX Component
    /// logical station 1 and CPU2 on 4. That used to be hardcoded in Program.cs, so
    /// changing it meant a rebuild - on a panel PC, in a factory.
    ///
    /// A CONTROLLER is identified by the module that owns it and that module's OWN
    /// PLC number ("FA2010 PLC1", "H500 PLC1"), never by the deployment's
    /// station id. The station id is exactly the thing this file is allowed to
    /// change, so keying on it would make the setting reset itself.
    ///
    /// Two different numbers are involved and confusing them wastes an afternoon:
    ///
    ///   StationId       the HMI's own handle for a controller. Controls carry it
    ///                   as PlcNumber; PlcManager stores drivers under it.
    ///   LogicalStation  the MX Component COMMUNICATION SETTING UTILITY's station
    ///                   number, which is what ActUtlType.ActLogicalStationNumber
    ///                   wants. It is a property of the PC's MX configuration, not
    ///                   of the machine, and the two frequently disagree.
    ///
    /// Stored one line per controller in
    /// CommonAppData\KabanHMI\PlcConnections.cfg:
    ///
    ///     FA2010|1=MitsubishiActUtl;1
    ///     FA2010|2=Simulation;2
    /// </summary>
    public static class PlcConnectionSettings
    {
        /// <summary>One machine controller: "&lt;module&gt; PLC&lt;n&gt;".</summary>
        public class Endpoint
        {
            /// <summary>Owning module, e.g. "FA2010". Empty for a legacy exe with no modules.</summary>
            public string ModuleId = "";

            /// <summary>The module's OWN PLC number - the 1 or 2 the engineer says out loud.</summary>
            public int LocalPlcNumber = 1;

            /// <summary>The station id this controller is mounted on in this deployment.</summary>
            public int StationId = 1;

            /// <summary>True for the main machine, so the UI can list it first.</summary>
            public bool IsMain;

            /// <summary>The name the module registered, e.g. "FA2010 Main PLC".</summary>
            public string StationName = "";

            /// <summary>Stable key for the config file and for UI dictionaries.</summary>
            public string Key
            {
                get { return (ModuleId ?? "") + "|" + LocalPlcNumber.ToString(CultureInfo.InvariantCulture); }
            }

            public override string ToString() { return Key; }
        }

        public class Connection
        {
            public string DriverId = PlcDriverCatalog.SimulationId;

            /// <summary>MX Component logical station. Ignored by the network drivers.</summary>
            public int LogicalStation = 1;

            /// <summary>
            /// Where the CPU is, for the drivers that reach it over the network:
            /// "192.168.0.1:0:1" for Siemens (host:rack:slot), "192.168.1.5:502:1"
            /// for Delta (host:port:unit). Empty for Mitsubishi and the simulator,
            /// which are addressed by number instead.
            /// </summary>
            public string Endpoint = "";
        }

        private static readonly Dictionary<string, Connection> _byKey =
            new Dictionary<string, Connection>(StringComparer.OrdinalIgnoreCase);

        private static bool _loaded;

        public static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "KabanHMI", "PlcConnections.cfg");
            }
        }

        // ==================================================================
        // THE CONTROLLER LIST
        // ==================================================================

        /// <summary>
        /// Every controller in this deployment, MAIN MACHINE FIRST then sub-machines
        /// in mount order, and within a machine by its own PLC number. That is the
        /// order the PLC Connection page and the simulation monitor's tabs use,
        /// so the two always read the same way round.
        ///
        /// Built from what the modules registered. A legacy exe that mounts no
        /// modules (SB3012/SB3015) still gets one endpoint per registered station,
        /// so nothing depends on the module system being in use.
        /// </summary>
        public static List<Endpoint> Endpoints()
        {
            var list = new List<Endpoint>();
            var claimed = new HashSet<int>();

            foreach (var mounted in MachineRegistry.Modules.OrderByDescending(m => m.IsMain))
            {
                // Ask the mount context which of the module's own PLC numbers were
                // actually mapped. RegisterPlc calls ctx.Station(1)/ctx.Station(2),
                // so this is a record of what the module really declared rather
                // than a guess at how many PLCs a machine has.
                foreach (var pair in mounted.Context.MappedStations.OrderBy(p => p.Key))
                {
                    var info = PlcManager.Instance.GetStation(pair.Value);
                    list.Add(new Endpoint
                    {
                        ModuleId = mounted.Context.ModuleId,
                        LocalPlcNumber = pair.Key,
                        StationId = pair.Value,
                        IsMain = mounted.IsMain,
                        StationName = info != null ? info.Name : ""
                    });
                    claimed.Add(pair.Value);
                }
            }

            // Stations registered without a module behind them (legacy entry points).
            foreach (var info in PlcManager.Instance.Stations
                                 .Where(s => !claimed.Contains(s.StationId))
                                 .OrderBy(s => s.StationId))
            {
                list.Add(new Endpoint
                {
                    ModuleId = info.OwnerModuleId ?? "",
                    LocalPlcNumber = info.LocalNumber > 0 ? info.LocalNumber : info.StationId,
                    StationId = info.StationId,
                    IsMain = list.Count == 0,
                    StationName = info.Name
                });
            }

            return list;
        }

        // ==================================================================
        // READ / WRITE
        // ==================================================================

        /// <summary>
        /// The saved connection for a controller, or a sensible default: the
        /// simulator on a logical station equal to the deployment's station id.
        /// Never returns null, so a caller can always dereference it.
        /// </summary>
        public static Connection For(Endpoint endpoint)
        {
            if (endpoint == null) return new Connection();
            EnsureLoaded();

            Connection c;
            if (_byKey.TryGetValue(endpoint.Key, out c)) return c;

            return new Connection
            {
                DriverId = PlcDriverCatalog.SimulationId,
                LogicalStation = endpoint.StationId,
                Endpoint = ""
            };
        }

        /// <summary>True when this controller has been configured explicitly.</summary>
        public static bool IsConfigured(Endpoint endpoint)
        {
            if (endpoint == null) return false;
            EnsureLoaded();
            return _byKey.ContainsKey(endpoint.Key);
        }

        public static void Set(Endpoint endpoint, string driverId, int logicalStation, string address)
        {
            if (endpoint == null) return;
            EnsureLoaded();
            _byKey[endpoint.Key] = new Connection
            {
                DriverId = string.IsNullOrWhiteSpace(driverId) ? PlcDriverCatalog.SimulationId : driverId.Trim(),
                LogicalStation = logicalStation,
                Endpoint = (address ?? "").Trim()
            };
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            Load();
        }

        /// <summary>Re-reads the file. A missing file is not an error.</summary>
        public static void Load()
        {
            _loaded = true;
            _byKey.Clear();
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var raw in File.ReadAllLines(SettingsPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = line.Substring(0, eq).Trim();
                    var parts = line.Substring(eq + 1).Split(';');

                    int station;
                    _byKey[key] = new Connection
                    {
                        DriverId = parts[0].Trim(),
                        LogicalStation = parts.Length > 1 &&
                                         int.TryParse(parts[1].Trim(), NumberStyles.Integer,
                                                      CultureInfo.InvariantCulture, out station)
                                         ? station : 1,
                        // The endpoint may itself contain ':' but never ';', so
                        // everything after the second ';' is the address.
                        Endpoint = parts.Length > 2 ? string.Join(";", parts.Skip(2)).Trim() : ""
                    };
                }
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not read PLC connection settings: " + ex.Message);
            }
        }

        public static bool Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                var lines = new List<string>
                {
                    "# Kaban HMI - PLC connections.",
                    "# <ModuleId>|<the module's own PLC number> = <driver id>;<logical station>;<address>",
                    "# <address> is host:rack:slot (Siemens) or host:port:unit (Delta); blank otherwise.",
                    "# Edited from Settings -> HMI Settings -> PLC Connection.",
                    "# Takes effect at the next start: a driver cannot be swapped while",
                    "# screens are bound to the running one."
                };
                foreach (var kv in _byKey.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    lines.Add(kv.Key + "=" + kv.Value.DriverId + ";" +
                              kv.Value.LogicalStation.ToString(CultureInfo.InvariantCulture) + ";" +
                              (kv.Value.Endpoint ?? ""));

                // All-or-nothing (SafeFile): a half-written file here is a machine
                // that starts with no PLC addresses.
                SafeFile.WriteAllText(SettingsPath,
                    string.Join(Environment.NewLine, lines) + Environment.NewLine,
                    new System.Text.UTF8Encoding(false));
                AuditLogger.Log("saved PLC connection settings (" + _byKey.Count + " controllers).");
                return true;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not save PLC connection settings: " + ex.Message);
                return false;
            }
        }

        // ==================================================================
        // STARTUP
        // ==================================================================

        /// <summary>
        /// Creates and registers a driver for every controller, from the saved
        /// settings. Call it from Program.cs AFTER the modules are mounted (the
        /// endpoint list comes from them) and after the driver kinds are registered.
        ///
        /// A controller with no saved setting falls back to <paramref name="fallback"/>
        /// if supplied, so a fresh install still behaves the way the machine's
        /// Program.cs intends rather than silently simulating everything.
        /// </summary>
        public static int ApplyAll(Func<Endpoint, Connection> fallback = null)
        {
            EnsureLoaded();
            int opened = 0;

            foreach (var endpoint in Endpoints())
            {
                Connection conn = IsConfigured(endpoint)
                    ? For(endpoint)
                    : (fallback != null ? fallback(endpoint) : null) ?? For(endpoint);

                if (string.Equals(conn.DriverId, PlcDriverCatalog.NoneId, StringComparison.OrdinalIgnoreCase))
                {
                    AuditLogger.Log("[PLC DIAG] " + endpoint.Key + " is configured as not connected.");
                    continue;
                }

                var driver = PlcDriverCatalog.Create(conn.DriverId, conn.LogicalStation, conn.Endpoint);
                if (driver == null) continue;

                PlcManager.Instance.RegisterDriver(endpoint.StationId, driver);
                AuditLogger.Log("[PLC DIAG] " + endpoint.Key + " -> station " + endpoint.StationId +
                                " using " + conn.DriverId + " (" +
                                (string.IsNullOrWhiteSpace(conn.Endpoint)
                                    ? "logical station " + conn.LogicalStation
                                    : conn.Endpoint) + ").");
                opened++;
            }
            return opened;
        }
    }
}

