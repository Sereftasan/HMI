using System;
using System.Collections.Generic;
using Kaban.Core.Enums;
using Kaban.Core.Interfaces;

namespace Kaban.Core.Helpers
{
    public class PlcManager
    {
        private static PlcManager _instance;
        public static PlcManager Instance => _instance ?? (_instance = new PlcManager());

        private Dictionary<int, IPlcDriver> _drivers = new Dictionary<int, IPlcDriver>();

        // ==================================================================
        // DECLARATIVE STATION PERMISSIONS
        // ------------------------------------------------------------------
        // Every station REGISTERS the level required to command it; the core
        // never special-cases a station number. It cannot: the same sub-machine
        // is mounted on a different station on every host, so "station 3 needs
        // Maintenance" is a fact about one deployment, not about the program.
        // Modules declare their own stations - see IMachineModule.RegisterPlc.
        // ==================================================================
        private readonly Dictionary<int, PlcStationInfo> _stations = new Dictionary<int, PlcStationInfo>();
        private readonly Dictionary<int, bool> _overrides = new Dictionary<int, bool>();

        /// <summary>Level required when a station has not been registered explicitly.</summary>
        public SecurityLevel DefaultRequiredLevel { get; set; } = SecurityLevel.Operator;

        public class PlcStationInfo
        {
            public int StationId { get; set; }
            public string Name { get; set; } = "";
            public string OwnerModuleId { get; set; } = "";
            public SecurityLevel RequiredLevel { get; set; } = SecurityLevel.Operator;

            /// <summary>
            /// The OWNING MODULE'S own PLC number - the "1" or "2" the engineer says
            /// out loud ("FA2010 PLC2"), as opposed to StationId, which is where this
            /// deployment happens to have mounted it. A sub-machine's PLC1 may sit on
            /// station 3; the operator still calls it PLC1, so both the Station
            /// Connection page and the simulation monitor's tabs label it that way.
            /// 0 means the module did not say (legacy call site).
            /// </summary>
            public int LocalNumber { get; set; }
        }

        /// <summary>
        /// Declares a PLC station and the security level needed to WRITE to it.
        /// Called by machine modules at startup.
        ///
        /// localNumber is the module's OWN PLC number. Pass it: it is what names the
        /// controller everywhere the operator sees it, and it cannot be recovered
        /// from stationId once a station map has been applied.
        /// </summary>
        public void RegisterStation(int stationId, string name, SecurityLevel requiredLevel,
                                    string ownerModuleId = "", int localNumber = 0)
        {
            _stations[stationId] = new PlcStationInfo
            {
                StationId = stationId,
                Name = name ?? "",
                OwnerModuleId = ownerModuleId ?? "",
                RequiredLevel = requiredLevel,
                LocalNumber = localNumber
            };
        }

        // ---- the machine's alarm reset bit ---------------------------------
        //
        // A PROPERTY OF THE MACHINE'S LADDER, declared by its module next to its
        // program-version device - not a setting, and NOT a default. M102 is the
        // reset of FA2010's PLC1 only; every other machine resets on a bit of its
        // own. It used to be M102 everywhere: the Alarms page of a mounted H500
        // pulsed H500's M102, which there is MAN_Lubrication.

        private readonly Dictionary<int, string> _alarmReset = new Dictionary<int, string>();
        private readonly Dictionary<int, BitActionType> _alarmResetAction = new Dictionary<int, BitActionType>();

        /// <summary>
        /// Declares the bit a station's PLC resets its alarms on, and how it is
        /// driven: Momentary (on, then off - a push button) or SET (on only - the
        /// PLC switches it off itself once every device is reset, as FA2010's does).
        /// </summary>
        public void SetAlarmResetDevice(int stationId, string address, BitActionType action = BitActionType.Momentary)
        {
            if (string.IsNullOrWhiteSpace(address)) { _alarmReset.Remove(stationId); _alarmResetAction.Remove(stationId); }
            else
            {
                _alarmReset[stationId] = address.Trim().ToUpperInvariant();
                _alarmResetAction[stationId] = action == BitActionType.SET ? BitActionType.SET : BitActionType.Momentary;
            }
        }

        /// <summary>How the station's reset bit is driven (Momentary unless declared SET).</summary>
        public BitActionType AlarmResetAction(int stationId)
        {
            BitActionType a;
            return _alarmResetAction.TryGetValue(stationId, out a) ? a : BitActionType.Momentary;
        }

        /// <summary>The station's alarm reset bit, or null when the machine has not declared one.</summary>
        public string AlarmResetDevice(int stationId)
        {
            string a;
            return _alarmReset.TryGetValue(stationId, out a) ? a : null;
        }

        public PlcStationInfo GetStation(int stationId)
        {
            PlcStationInfo info;
            return _stations.TryGetValue(stationId, out info) ? info : null;
        }

        public IEnumerable<PlcStationInfo> Stations => _stations.Values;

        /// <summary>
        /// How many stations are mounted. Used to decide whether a tooltip needs
        /// to name the station at all - on a single-PLC machine it never does.
        /// </summary>
        public int StationCount => _stations.Count;

        /// <summary>
        /// Begins watching every registered station.
        ///
        /// Called once the machine modules have mounted, so the monitor knows which
        /// stations exist. It runs for the life of the program, independently of
        /// which page is open.
        /// </summary>
/// <summary>
        /// The driver to WRITE through, or null when the station is known to be
        /// down.
        ///
        /// Every control's write path asks this rather than GetDriver. Pressing a
        /// button on a dead station used to go straight into a driver call that
        /// blocked until the communication timed out - the freeze. Refusing here
        /// costs nothing and the caller already handles null.
        /// </summary>
        public IPlcDriver GetDriverForWrite(int plcNumber)
        {
            if (ConnectionMonitor.IsKnownDown(plcNumber)) return null;
            return GetDriver(plcNumber);
        }

                public void StartConnectionWatch()
        {
            // THE LIVE KEY COLLECTION, not a copy. A snapshot taken here missed
            // every station registered afterwards - and FA2010 started the watch
            // before mounting H500 and H800, so their stations were never polled,
            // never connected, and their simulations could not be operated.
            ConnectionMonitor.Start(GetDriverForMonitoring, _stations.Keys);
        }

        /// <summary>
        /// How a station is named on every page, menu and document.
        ///
        /// "Processing Center" when the machine has ONE controller; "Processing
        /// Center (PLC2)" only when it has more than one. Naming every station
        /// "... PLC1" told the operator something true and useless on the machines
        /// that only ever have one, and the suffix stopped meaning anything.
        ///
        /// One rule in one place, so a page, a dropdown and a help file cannot
        /// disagree about what a controller is called.
        /// </summary>
        public static string DescribeStation(string moduleId, int localNumber)
        {
            var machine = string.IsNullOrEmpty(moduleId)
                ? LanguageManager.GetString("Sys_Machine", "Machine")
                : LanguageManager.GetString("Machine_" + moduleId, moduleId);

            return machine + StationSuffix(moduleId, localNumber);
        }

        /// <summary>" (PLC2)" when the machine has several controllers, else "".</summary>
        public static string StationSuffix(string moduleId, int localNumber)
        {
            int onThisMachine = 0;
            foreach (var s in Instance.Stations)
                if (string.Equals(s.OwnerModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                    onThisMachine++;

            return onThisMachine > 1 ? " (" + localNumber + ")" : "";
        }

        /// <summary>Manual override for a station (hardware key-switch integration).</summary>
        public void SetPermissionOverride(int stationId, bool allowed) => _overrides[stationId] = allowed;

        /// <summary>Removes manual overrides so permissions follow the login level again.</summary>
        public void ClearPermissionOverrides() => _overrides.Clear();

        /// <summary>Security level required to command the given station.</summary>
        public SecurityLevel RequiredLevelFor(int stationId)
        {
            var s = GetStation(stationId);
            return s != null ? s.RequiredLevel : DefaultRequiredLevel;
        }

        // 1. GATEKEEPER LOGIC - now fully declarative.
        public bool CheckPermission(int plcNumber)
        {
            bool ovr;
            if (_overrides.TryGetValue(plcNumber, out ovr)) return ovr;
            return SecurityManager.HasAccess(RequiredLevelFor(plcNumber));
        }

        public void RegisterDriver(int id, IPlcDriver driver)
        {
            // THE UI THREAD CALLS DRIVERS - the connection poll and the scanner
            // both run there. A driver that blocks (a Siemens connect waits up to
            // 1.5 s for TCP) froze the whole screen on every poll while its PLC was
            // unreachable, so tightly the login could not be reached. Such drivers
            // get their own thread and bounded waits, exactly as MX Component has.
            if (driver != null && !(driver is INonBlockingDriver)) driver = new BoundedDriver(driver);

            // EVERY WRITE IS LOGGED, device by device (PlcWriteLog): the driver
            // handed out is the logging one, so no screen can write around it.
            if (driver != null && !(driver is LoggedDriver)) driver = new LoggedDriver(id, driver);

            if (_drivers.ContainsKey(id)) _drivers[id] = driver;
            else _drivers.Add(id, driver);

            // OPEN THE CONNECTION.
            // Nothing else in the application called Connect(), so every driver sat
            // at IsConnected = false: writes were refused (-1) and TryRead reported
            // comm-lost, which left controls permanently fail-closed. The simulator
            // hid this because Seed()/Dump() touch memory directly, so values were
            // VISIBLE in the Simulation Monitor while edits silently did nothing.
            //
            // Failure is not fatal: a real PLC that is not plugged in simply stays
            // disconnected and the existing comm-lost handling applies.
            if (driver == null) return;
            try
            {
                if (!driver.IsConnected) driver.Connect();
            }
            catch (Exception ex)
            {
                AuditLogger.Log($"could not open PLC station {id}: {ex.Message}");
            }
        }

        // 2. ENHANCED GET DRIVER
        public IPlcDriver GetDriver(int id)
        {
            // A. Check Permission First
            if (!CheckPermission(id)) return null; // Block access if no permission

            // B. Return Driver
            if (_drivers.ContainsKey(id)) return _drivers[id];
            return null;
        }

        /// <summary>
        /// The controller's own driver inside the wrappers RegisterDriver puts
        /// around it (LoggedDriver, BoundedDriver) - for code that needs its class
        /// (the simulator's memory, MX Component's logical station). Writes through
        /// it are NOT in the PLC write log: only the bench simulation's movement,
        /// standing in for the PLC program, should write through it.
        /// </summary>
        public static IPlcDriver Unwrap(IPlcDriver driver)
        {
            for (int i = 0; i < 8 && driver != null; i++)
            {
                var logged = driver as LoggedDriver;
                if (logged != null) { driver = logged.Inner; continue; }
                var bounded = driver as BoundedDriver;
                if (bounded != null && bounded.Inner != null) { driver = bounded.Inner; continue; }
                break;
            }
            return driver;
        }

        /// <summary>
        /// Driver access for READ-ONLY monitoring (the PlcScanner).
        /// Deliberately NOT permission-gated: watching the machine is not commanding
        /// it. Previously the scan loop used GetDriver(), so when nobody was logged
        /// in the whole loop bailed out - UpdateState never ran, and every button
        /// kept its default "enabled" state (clickable, with no disabled reason).
        /// Writes still go through GetDriver() and remain permission-checked.
        /// </summary>
        public IPlcDriver GetDriverForMonitoring(int id)
        {
            if (_drivers.ContainsKey(id)) return _drivers[id];
            return null;
        }

        /// <summary>
        /// Why is GetDriver(id) returning null? Lets the UI show an ACCURATE message
        /// (previously everything was reported as "disconnected", even when the real
        /// reason was "not logged in").
        /// </summary>
        public enum AccessResult { Ok, NotLoggedIn, InsufficientLevel, NotRegistered, Disconnected }

        public AccessResult DiagnoseAccess(int id)
        {
            if (SecurityManager.CurrentLevel == SecurityLevel.None) return AccessResult.NotLoggedIn;
            if (!CheckPermission(id)) return AccessResult.InsufficientLevel;
            if (!_drivers.ContainsKey(id)) return AccessResult.NotRegistered;
            if (!_drivers[id].IsConnected) return AccessResult.Disconnected;
            return AccessResult.Ok;
        }

        /// <summary>Human-readable, translatable reason for a failed access.</summary>
        public string DescribeAccess(int id)
        {
            // A station that is known to be down says so first - that is the
            // reason the operator can act on.
            if (ConnectionMonitor.IsKnownDown(id)) return ConnectionMonitor.ReasonFor(id);

            switch (DiagnoseAccess(id))
            {
                case AccessResult.NotLoggedIn:
                    return LanguageManager.GetString("Msg_NotLoggedIn",
                        "Please log in before operating the machine.");
                case AccessResult.InsufficientLevel:
                    return LanguageManager.GetString("Msg_NoPermission",
                        "Your user level is not high enough for this action.");
                case AccessResult.NotRegistered:
                    return LanguageManager.GetString("Msg_PlcNotRegistered",
                        $"PLC station {id} is not configured.");
                case AccessResult.Disconnected:
                    return LanguageManager.GetString("Msg_PlcDisconnected",
                        $"PLC station {id} is disconnected. Check the connection.");
                default:
                    return "";
            }
        }

        // Passthrough methods
        // Non-throwing, but never silent: an unknown station id leaves a trace in
        // the audit log, or a mistyped PlcNumber looks exactly like a dead PLC.
        public bool Connect(int id)
        {
            if (!_drivers.ContainsKey(id))
            {
                AuditLogger.Log($"attempted to connect to unregistered PLC station {id}.");
                return false;
            }
            // Open already (RegisterDriver opened it): nothing to do. Asking again
            // made MX Component answer "already open" on every start.
            if (_drivers[id].IsConnected) return true;
            return _drivers[id].Connect();
        }

        /// <summary>
        /// Closes EVERY station. Call this before the process ends.
        ///
        /// Shutdown used to disconnect station 1 only, leaving 2, 3 and 4 to be
        /// released by process teardown - which is not deterministic: a COM apartment
        /// is torn down on its own schedule and a socket lingers until the handle is
        /// finalised. That is why a restart worked sometimes: the new instance waited
        /// correctly for the old PROCESS, but the old process had not necessarily let
        /// go of the other stations by the time it exited, and MX Component refuses a
        /// second opener on a station it still considers held.
        ///
        /// ALL AT ONCE, and waited for: each station closes on its own thread (an MX
        /// station may first have to wait for an Open() still running), so four
        /// stations take as long as the slowest, not the sum - and the program does
        /// not exit until they are closed and released, or a ceiling is reached.
        /// </summary>
        public void DisconnectAll()
        {
            var ids = new List<int>(_drivers.Keys);
            var closing = new List<System.Threading.Tasks.Task>();
            foreach (var id in ids)
            {
                var driver = _drivers[id];
                int station = id;
                closing.Add(System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        driver.Disconnect();
                        var disposable = driver as IDisposable;
                        if (disposable != null) disposable.Dispose();   // sockets, COM controls and their threads
                    }
                    catch (Exception ex)
                    {
                        AuditLogger.Log("[PLC DIAG] station " + station + " did not close cleanly: " + ex.Message);
                    }
                }));
            }
            bool all = true;
            try { all = System.Threading.Tasks.Task.WaitAll(closing.ToArray(), DisconnectAllCeilingMs); }
            catch (Exception ex) { AuditLogger.Log("[PLC DIAG] closing the stations: " + ex.Message); }
            AuditLogger.Log(all ? "[PLC DIAG] all " + ids.Count + " station(s) closed."
                                : "[PLC DIAG] not every station closed within " + DisconnectAllCeilingMs / 1000 + " s.");
        }

        /// <summary>How long shutdown waits for every station to close.</summary>
        public const int DisconnectAllCeilingMs = 8000;

        public void Disconnect(int id)
        {
            if (!_drivers.ContainsKey(id))
            {
                AuditLogger.Log($"attempted to disconnect unregistered PLC station {id}.");
                return;
            }
            _drivers[id].Disconnect();
        }
    }
}

