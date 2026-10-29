namespace Kaban.Machines.FA2010.Screens.Shared
{
    /// <summary>
    /// FA2010's axes' manual movement bar: their speed (D409), JOG or a step of 10 /
    /// 1 / 0.1 / 0.01 mm, Safety Bypass and Automatic ON/OFF (a cylinder back and forth, UnitCycling - called
    /// Cylinder FW/Back until 30 October; 17 October: Maintenance and
    /// Admin only, both off when the user changes) - designed once, shown on the manual pages
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
