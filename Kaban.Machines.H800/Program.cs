using System;
using System.IO;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.Drivers.MitsubishiActUtl;
using Kaban.Drivers.Simulation;
using Kaban.UI;
using Kaban.UI.Forms;

namespace Kaban.Machines.H800
{
    /// <summary>
    /// STANDALONE entry point for H800.
    ///
    /// This project is a full machine in its own right: it builds its own .exe and
    /// its own bin\Debug folder, and can be run and debugged on its own. When run
    /// this way it mounts itself as the MAIN machine.
    ///
    /// The SAME assembly is also referenced by a bigger machine (e.g. FA2010),
    /// which ignores this entry point and mounts H800Module as a SUB
    /// machine to form a combo machine. Nothing below affects that case.
    /// </summary>
    internal static class Program
    {
        /// <summary>Run against in-memory simulated PLCs (no hardware needed).</summary>
        private const bool UseSimulation = true;

        [STAThread]
        static void Main(string[] args)
        {
            // A RESTART must not open a PLC station the outgoing instance still
            // holds - MX Component refuses the second opener. Waits only when
            // this process was actually started by a restart.
            Kaban.UI.AppBootstrap.AwaitPreviousInstance(args);
            // one copy of the HMI at a time: another running is brought to the front, this one ends
            if (!Kaban.UI.Helpers.SingleInstance.Take(args)) return;
            Kaban.UI.Forms.MainForm.EndAfterClose = true;     // its window closed: the process ends (the next start waits for it)

            UnitManager.EnsureDefaults();
            // the engineer's device list: labels for the monitor and the AI prompt,
            // and the X/Y -> M mirror the screens must bind through
            PlcLabelTable.Load(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));   // every unit family starts with a real unit
            // First line of every run: which bitness ACTUALLY started. MX Component
            // logical stations are per-bitness (64-bit utility -> 64-bit registry),
            // so a 32-bit HMI cannot see stations configured by the 64-bit utility.
            Kaban.Core.Helpers.AuditLogger.Log(
                $"[PLC DIAG] HMI started as {(Environment.Is64BitProcess ? "64" : "32")}-bit process on {(Environment.Is64BitOperatingSystem ? "64" : "32")}-bit Windows.");
            AppBootstrap.InstallGlobalExceptionHandlers();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Standalone: this machine is the MAIN machine and owns its own PLC1.
            RegisterDriverKinds();

            MachineRegistry.Reset();
            MachineRegistry.MountMain(new H800Module());

            
            // Watch every station from here on, whatever page is open. Without this
            // the connection state only updated while a machine page happened to be
            // scanning, so Station Connection showed everything as down.
            PlcManager.Instance.StartConnectionWatch();
// Same three sections as FA2010's entry point, for the same reason: the
            // deployment is described HERE and nowhere downstream. Mounted as a SUB
            // by a host machine, none of this runs - the host describes it instead.
            PlcConnectionSettings.ApplyAll(FallbackConnection);
            SeedSimulatedStations();

            MainForm shell = new MainForm();
            shell.BuildNavigation();
            Application.Run(shell);
        }

        /// <summary>
        /// The drivers this exe offers in Settings -> HMI Settings -> Station
        /// Connection. Comment a line out and that driver can no longer be picked.
        /// </summary>
        private static void RegisterDriverKinds()
        {
            PlcDriverCatalog.RegisterNone();

            PlcDriverCatalog.Register(
                PlcDriverCatalog.SimulationId, "Drv_Simulation", "Simulation (in-memory)",
                (station, endpoint) => new SimulationDriver(station),
                usesLogicalStation: false);

            // 32-bit COM only; the constructor is defensive, so a PC without MX
            // Component gets a disconnected driver rather than a crash.
            PlcDriverCatalog.Register(
                PlcDriverCatalog.MitsubishiActUtlId, "Drv_Mitsubishi", "Mitsubishi (MX Component)",
                (station, endpoint) => new MitsubishiActUtl(station));

            // NOT YET TESTED AGAINST HARDWARE - see tools/protocol_tests/README.md.
            PlcDriverCatalog.Register(
                "SiemensS7", "Drv_Siemens", "Siemens S7-1200/1500 (TCP)",
                (station, endpoint) => new Kaban.Drivers.Siemens.SiemensS7(endpoint),
                usesLogicalStation: false, usesNetworkEndpoint: true,
                endpointHint: "192.168.0.1:0:1");

            PlcDriverCatalog.Register(
                "DeltaModbusTcp", "Drv_Delta", "Delta AS/DVP (Modbus TCP)",
                (station, endpoint) => new Kaban.Drivers.Delta.DeltaModbusTcp(endpoint),
                usesLogicalStation: false, usesNetworkEndpoint: true,
                endpointHint: "192.168.1.5:502:1");
        }

        /// <summary>
        /// What a controller gets on a PC with no saved settings. UseSimulation
        /// keeps the old bench default; once the Station Connection page has been
        /// used, the saved choice wins and this is not consulted again.
        /// </summary>
        private static PlcConnectionSettings.Connection FallbackConnection(
            PlcConnectionSettings.Endpoint endpoint)
        {
            if (!UseSimulation)
            {
                MxDiagnostics.Report();   // says exactly where the COM class is registered
                return new PlcConnectionSettings.Connection
                {
                    DriverId = PlcDriverCatalog.MitsubishiActUtlId,
                    LogicalStation = endpoint.StationId
                };
            }
            return new PlcConnectionSettings.Connection
            {
                DriverId = PlcDriverCatalog.SimulationId,
                LogicalStation = endpoint.StationId
            };
        }

        /// <summary>
        /// Bench state for whatever ended up SIMULATED - so pointing this machine at
        /// a real CPU on the settings page quietly stops seeding it.
        /// </summary>
        private static void SeedSimulatedStations()
        {
            foreach (var endpoint in PlcConnectionSettings.Endpoints())
            {
                var sim = PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(endpoint.StationId)) as SimulationDriver;
                if (sim == null) continue;

                sim.LoadSeedFile(Path.Combine(Application.StartupPath,
                                              "SimSeed.Station" + endpoint.StationId + ".txt"));
                sim.Seed("X10", 1).Seed("M40", 1)     // at-home + ready
                   .Seed("X20", 1).Seed("M50", 1);
                sim.AddMirror("M10", "X10").AddMirror("M11", "X11")
                   .AddMirror("M20", "X20").AddMirror("M21", "X21");
                sim.AddRamp("D10", 0, 2500, 120);
                sim.AddRamp("D20", 0, 2500, 120);
            }
        }
    }
}

