namespace Kaban.Machines.FA2010.Screens.Shared
{
    /// <summary>
    /// FA2010's axes' manual movement bar: their speed (D409), JOG or a step of 10 /
    /// 1 / 0.1 / 0.01 mm, and Safety Bypass - designed once, shown on the manual pages
    /// with axes, above Machine Parameters > Axis and on the Axis Monitor
    /// (MachineBarHMI; declared in FA2010Module as ScreenRoles.AxisBar).
    /// </summary>
    public partial class AxisJogBar : Kaban.UI.Controls.MachineBarHMI
    {
        public AxisJogBar()
        {
            InitializeComponent();
        }
    }
}
