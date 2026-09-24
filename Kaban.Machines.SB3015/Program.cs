using System;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Drivers.Simulation;
using Kaban.UI;
using Kaban.UI.Forms;

namespace Kaban.Machines.SB3015
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
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
            PlcLabelTable.Load(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));   // every unit family starts with a real unit
            // First line of every run: which bitness ACTUALLY started. MX Component
            // logical stations are per-bitness (64-bit utility -> 64-bit registry),
            // so a 32-bit HMI cannot see stations configured by the 64-bit utility.
            Kaban.Core.Helpers.AuditLogger.Log(
                $"[PLC DIAG] HMI started as {(Environment.Is64BitProcess ? "64" : "32")}-bit process on {(Environment.Is64BitOperatingSystem ? "64" : "32")}-bit Windows.");
            // Global exception handling: an unhandled exception must not take the
            // machine's HMI down without a trace (see AppBootstrap in Kaban.UI).
            AppBootstrap.InstallGlobalExceptionHandlers();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // This executable must register a driver or every
            // GetDriver() call returned null and no screen could read a tag.
            // This project only references the Simulation driver, so that's what
            // we register; swap in the real Mitsubishi/Siemens driver when this
            // machine profile goes to hardware.
            PlcManager.Instance.RegisterDriver(1, new SimulationDriver(1));

            // WHERE THE PLC RESETS ITS ALARMS - pulsed by the Alarms page's Reset
            // and the caption bar's alarm menu. NOT M102 (FA2010's). Until the
            // right bit is written here, the HMI offers no alarm reset for this
            // machine rather than pulsing the wrong device.
            // PlcManager.Instance.SetAlarmResetDevice(1, "M...");

            // Create the universal shell from the UI library
            string here = AppDomain.CurrentDomain.BaseDirectory;
            int loaded = AlarmDefinitionLoader.Load(System.IO.Path.Combine(here, "Alarms.json"));
            LanguageManager.LoadMachineTranslations(System.IO.Path.Combine(here, "Translations.csv"));
            AuditLogger.Log($"loaded {loaded} alarm definitions for SB3015.");

            MainForm shell = new MainForm();

            // Inject the screens that live in this specific executable
            shell.AddMenuButton("Btn_Home", "Home", null, new Screens.HomeScreen());
            shell.AddMenuButton("Btn_Alarms", "Alarms", null, new Screens.AlarmScreen());

            Application.Run(shell);
        }
    }
}

