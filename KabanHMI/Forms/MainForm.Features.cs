using System;
using Kaban.Core.Helpers;

namespace Kaban.UI.Forms
{
    // ======================================================================
    // THE HMI'S OPTIONAL PARTS (30 October: "We need to make the Industry 4
    // service and touchscreen mode optional, and we need to add some buttons
    // to active and passive them on HMI setting") - HMI Settings > Options.
    // The main window follows them as they are switched: the logging starts or
    // stops, the mouse / touch button comes or goes.
    // ======================================================================
    public partial class MainForm
    {
        /// <summary>The machines' loggers and the HMI's log in their database - while the feature is on.</summary>
        private static void StartLogging()
        {
            if (!HmiFeatures.LoggingOn) return;
            try { Kaban.UI.Dashboard.MachineLoggers.Start(); }
            catch (Exception ex) { AuditLogger.Log("Machine log: could not start: " + ex.Message); }
            try { if (Kaban.UI.Dashboard.HmiLogStore.AutoStart) Kaban.UI.Dashboard.HmiLogStore.Start(); }
            catch (Exception ex) { AuditLogger.Log("HMI log: the log database could not be used: " + ex.Message); }
            // the HMI's own records of the machines (a new machine: HMI Settings > Options, 5 November) - they decide as they come
            Kaban.UI.Dashboard.MachineRecords.Install();
        }

        /// <summary>Both stopped: the Windows service logs again, the HMI's log is in its files.</summary>
        private static void StopLogging()
        {
            try { Kaban.UI.Dashboard.MachineLoggers.StopAll(); } catch { }
            try { Kaban.UI.Dashboard.HmiLogStore.Stop(); } catch { }
            try { Kaban.UI.Dashboard.MachineRecords.Stop(); } catch { }
        }

        /// <summary>The mouse / touch button on the top bar only while the touch screen is switched on.</summary>
        private void ShowInputButton()
        {
            if (_btnInput == null || _btnInput.IsDisposed) return;
            _btnInput.Visible = HmiFeatures.TouchOn;
            LayoutTopBar();
        }
    }
}
