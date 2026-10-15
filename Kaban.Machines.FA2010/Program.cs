using System;
using System.IO;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.Drivers.MitsubishiActUtl;
using Kaban.Drivers.Simulation;
using Kaban.UI;
using Kaban.UI.Forms;

namespace Kaban.Machines.FA2010
{
    /// <summary>
    /// FA2010 as the MAIN machine.
    ///
    /// This file is the ONE place a deployment is described. Everything that
    /// changes between the bench, a customer site and a debugging session is a
    /// line here that can be commented out:
    ///
    ///   section 1  which DRIVERS this exe can offer   -> the Station Connection combo box
    ///   section 2  which MACHINES are mounted         -> the navigation, and the monitor's tabs
    ///   section 3  where each controller actually is  -> read from the saved settings
    ///
    /// Nothing downstream hardcodes any of it. Comment out a MountSub line and the
    /// sub-machine's tabs, alarms, translations, settings pages and monitor tab all
    /// disappear together; put it back and they all return. That is what makes any
    /// machine promotable to main, or demotable to sub, without editing the machine
    /// project itself - a module never knows which role it is playing.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // A RESTART must not open a PLC station the outgoing instance still
            // holds - MX Component refuses the second opener. Waits only when
            // this process was actually started by a restart.
            Kaban.UI.AppBootstrap.AwaitPreviousInstance(args);

            UnitManager.EnsureDefaults();

            // the engineer's device list: labels for the monitor and the AI prompt,
            // and the X/Y -> M mirror the screens must bind through
            PlcLabelTable.Load(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));

            // First line of every run: which bitness ACTUALLY started. MX Component
            // logical stations are per-bitness (64-bit utility -> 64-bit registry),
            // so a 32-bit HMI cannot see stations configured by the 64-bit utility.
            AuditLogger.Log(
                $"[PLC DIAG] HMI started as {(Environment.Is64BitProcess ? "64" : "32")}-bit process on {(Environment.Is64BitOperatingSystem ? "64" : "32")}-bit Windows.");
            AppBootstrap.InstallGlobalExceptionHandlers();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            RegisterDriverKinds();
            MountMachines();

            // --- 3. Connections ------------------------------------------------
            // Creates a driver per controller from Settings -> HMI Settings ->
            // Station Connection. Must run AFTER the machines are mounted, because
            // the controller list is built from what they declared.
            //
            // The fallback is what a PC with no saved settings gets. It is
            // deliberately the old hardcoded arrangement, so a fresh install behaves
            // exactly as this exe always did rather than silently simulating the
            // real CPU.
            PlcConnectionSettings.ApplyAll(FallbackConnection);
            SeedSimulatedStations();

            // --- 4. Shell -------------------------------------------------------
            MainForm shell = new MainForm();
            shell.BuildNavigation();   // menu built from every mounted module

            Application.Run(shell);
        }

        // ==================================================================
        // 1. DRIVER KINDS - what the Driver combo box offers
        // ==================================================================

        /// <summary>
        /// Every driver this exe is built against. Registering a kind here is what
        /// puts it in the Station Connection combo box; comment a line out and that
        /// driver can no longer be selected on this machine.
        ///
        /// Declared here rather than discovered by reflection for two reasons: the
        /// UI and Core projects must not reference a driver assembly, and a machine
        /// exe is deliberately built against the drivers it is ALLOWED to use.
        /// </summary>
        private static void RegisterDriverKinds()
        {
            // ---- always available -------------------------------------------
            PlcDriverCatalog.RegisterNone();

            PlcDriverCatalog.Register(
                PlcDriverCatalog.SimulationId, "Drv_Simulation", "Simulation (in-memory)",
                (station, endpoint) => new SimulationDriver(station),
                usesLogicalStation: false);

            // ---- Mitsubishi, via MX Component -------------------------------
            // 32-bit COM only. The constructor is already defensive: a PC without MX
            // Component gets a driver that stays disconnected rather than an
            // exception that takes the HMI down.
            PlcDriverCatalog.Register(
                PlcDriverCatalog.MitsubishiActUtlId, "Drv_Mitsubishi", "Mitsubishi (MX Component)",
                (station, endpoint) => new MitsubishiActUtl(station));

            // ---- Siemens S7-1200/1500, over ISO-on-TCP ----------------------
            // NOT YET TESTED AGAINST HARDWARE. On the CPU: clear "Optimized block
            // access" on any DB you read, and permit PUT/GET communication under
            // Protection - it is off by default and refuses an otherwise correct
            // connection.
            PlcDriverCatalog.Register(
                "SiemensS7", "Drv_Siemens", "Siemens S7-1200/1500 (TCP)",
                (station, endpoint) => new Kaban.Drivers.Siemens.SiemensS7(endpoint),
                usesLogicalStation: false, usesNetworkEndpoint: true,
                endpointHint: "192.168.0.1:0:1");

            // ---- Delta AS/DVP, over Modbus TCP ------------------------------
            // NOT YET TESTED AGAINST HARDWARE. Modbus TCP is on by default on the AS
            // series; the device-to-Modbus address bases are the part to check first
            // if a value reads back wrong.
            PlcDriverCatalog.Register(
                "DeltaModbusTcp", "Drv_Delta", "Delta AS/DVP (Modbus TCP)",
                (station, endpoint) => new Kaban.Drivers.Delta.DeltaModbusTcp(endpoint),
                usesLogicalStation: false, usesNetworkEndpoint: true,
                endpointHint: "192.168.1.5:502:1");
        }

        // ==================================================================
        // 2. MACHINES - the shell renders whatever is mounted here
        // ==================================================================

        /// <summary>
        /// The main machine and its sub-machines. stationMap maps a module's OWN
        /// station numbers onto this deployment's, so the same H500 assembly can be
        /// mounted on any host by changing only the numbers in this file.
        ///
        /// Screens that share a Role collapse into one navigation button with a tab
        /// per machine, which is why mounting a sub-machine adds tabs rather than
        /// buttons. HMI Settings is the exception: it lives in Kaban.UI and is
        /// common to every machine, so it appears once no matter how many are
        /// mounted.
        /// </summary>
        private static void MountMachines()
        {
            MachineRegistry.Reset();

            // The MAIN machine. Swap this line with one of the MountSub lines below
            // and the roles genuinely exchange - neither module contains any
            // assumption about which one it is.
            MachineRegistry.MountMain(new FA2010Module());
// Sub-machines. Comment a line out to run without that unit; its tabs,
            // alarms, translations, settings and monitor tab all go with it.
            MachineRegistry.MountSub(new Kaban.Machines.H500.H500Module(),
                                     stationMap: new[] { 3 });   // H500 PLC1 -> station 3
            MachineRegistry.MountSub(new Kaban.Machines.H800.H800Module(),
                                     stationMap: new[] { 4 });   // H800 PLC1 -> station 4

            // Watch every station from here on, whatever page is open - AFTER every
            // mount, so the first poll already knows all the stations. (The watch
            // reads the live station list anyway; the order is for the reader.)
            PlcManager.Instance.StartConnectionWatch();
        }

        // ==================================================================
        // 3. CONNECTIONS
        // ==================================================================

        /// <summary>
        /// What a controller gets when the settings file has nothing for it - the
        /// arrangement this exe used before Station Connection existed:
        ///   FA2010 PLC1  Mitsubishi on logical station 1  (watch in GX Works)
        ///   FA2010 PLC2  simulated                        (watch with F12)
        ///   H500 / H800  simulated
        /// </summary>
        private static PlcConnectionSettings.Connection FallbackConnection(
            PlcConnectionSettings.Endpoint endpoint)
        {
            bool mitsubishi = endpoint.ModuleId == "FA2010" && endpoint.LocalPlcNumber == 1;

            if (mitsubishi)
            {
                MxDiagnostics.Report();      // says exactly where the COM class is registered
                return new PlcConnectionSettings.Connection
                {
                    DriverId = PlcDriverCatalog.MitsubishiActUtlId,
                    LogicalStation = 1
                };
            }

            return new PlcConnectionSettings.Connection
            {
                DriverId = PlcDriverCatalog.SimulationId,
                LogicalStation = endpoint.StationId
            };
        }

        // ==================================================================
        // BENCH SETUP
        // ==================================================================

        /// <summary>
        /// Gives every SIMULATED station enough state to be useful: a seed file if
        /// present, feedback mirrors so buttons visibly do something, and a moving
        /// value for the position readouts.
        ///
        /// Runs over whatever ended up simulated rather than over a fixed list of
        /// station numbers, so it follows the Station Connection page: point PLC2 at
        /// a real CPU and this quietly stops seeding it.
        /// </summary>
        private static void SeedSimulatedStations()
        {
            foreach (var endpoint in PlcConnectionSettings.Endpoints())
            {
                var sim = PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(endpoint.StationId)) as SimulationDriver;
                if (sim == null) continue;

                // Optional per-station seed file, e.g. "SimSeed.Station2.txt" next to
                // the exe. Lets you change a test scenario without recompiling.
                sim.LoadSeedFile(Path.Combine(Application.StartupPath,
                                              "SimSeed.Station" + endpoint.StationId + ".txt"));

                if (endpoint.ModuleId == "FA2010" && endpoint.LocalPlcNumber == 1)
                {
                    // FA2010 CPU1 conditions (see FA2010Module.RegisterConditions).
                    sim.Seed("M21", 0)    // ManualMode  == 0 -> active
                       .Seed("M21", 1)    // AutoMode    <= 3 -> active
                       .Seed("D200", 0)    // MachineIsRunning > 0 -> inactive
                       .Seed("X20", 1)     // RobotIsSafe -> ON so commands are allowed
                       .Seed("X50", 1)     // RobotIsOutside
                       .Seed("M22", 0);    // MillingUnitInside
                }
                else if (endpoint.ModuleId == "FA2010" && endpoint.LocalPlcNumber == 2)
                {
                    // FA2010 CPU2 - the multi-CPU test target. Seed EVERY address the
                    // PLC2 showcase controls use, so they all appear in the monitor
                    // immediately and are wired end to end (otherwise an address only
                    // shows up after its control writes it).
                    sim.Seed("X25", 1)     // lamp + MullionNotching (PLC2)
                       .Seed("M23", 0)     // MillingUnitInside_PLC2
                       .Seed("M10", 0)     // demo button relay / unit fwd command
                       .Seed("M11", 0)     // unit reverse command
                       .Seed("Y0", 0).Seed("Y1", 0)     // outputs
                       .Seed("X10", 0).Seed("X11", 0)   // sensor feedback (driven by mirrors)
                       .Seed("D100", 0)    // numeric display
                       .Seed("D110", 0)    // numeric input
                       .Seed("D10", 0);    // function-unit position (ramped below)
                }
                else
                {
                    // Sub-machine stations: home + ready so their pages are operable.
                    sim.Seed("X10", 1).Seed("M40", 1)    // H500 addresses
                       .Seed("X20", 1).Seed("M50", 1);   // H800 addresses
                }

                // Pressing a command bit drives its feedback bit, so lamps and
                // conditions react instead of the screen looking dead.
                sim.AddMirror("M10", "X10").AddMirror("M11", "X11")
                   .AddMirror("M20", "X20").AddMirror("M21", "X21");

                // Make position readouts move so formatting/limits are visible.
                sim.AddRamp("D10", 0, 2500, 120);
                sim.AddRamp("D20", 0, 2500, 120);
            }
        }
    }
}

