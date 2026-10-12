using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.Machines.H500.Screens
{
    /// <summary>
    /// H500's HOME: the old program's main control panel for the H500 FX5
    /// (tools/legacy/H500/ucMainControlPanelH500FX, ucScrewingMainControlPanel.cs) - its
    /// Start, Pause and Cancel, nothing else (12 October). Its PLC numbers are the
    /// machine's own: H500Module gives it the host's station (StationMap).
    /// </summary>
    public partial class HomeScreen : UserControl
    {
        /// <summary>The job-finished flag Start clears first (the old Constants.ScrewingJobFinish).</summary>
        public const string JobFinishedAddress = "M60";

        public HomeScreen()
        {
            InitializeComponent();
            // As the old Start: the job-finished flag cleared FIRST, then the start pulse
            // (M2, on while the button is held) - its press, before the button writes.
            btnStart.UserActed += (b, action) =>
            {
                if (action != ButtonHMI.UserAction.Press) return;
                var drv = PlcManager.Instance.GetDriverForWrite(b.PlcNumber);
                if (drv == null) return;
                using (Kaban.UI.Helpers.WriteSource.For(b, "start: the job-finished flag cleared"))
                    drv.Write(JobFinishedAddress, 0);
            };
        }
    }
}
