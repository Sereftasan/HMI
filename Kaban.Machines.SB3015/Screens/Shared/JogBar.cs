namespace Kaban.Machines.SB3015.Screens.Shared
{
    /// <summary>
    /// SB3015's manual movement bar (17 November, from the old program's _SB3015: its manual pages'
    /// top bar, ucHeadControlButtons): the axes' manual speed (D409), how far a press moves them -
    /// 10 mm (M170), 1 mm (M173), 0.1 mm (M172), 0.01 mm (M171), each setting its own bit as the old
    /// buttons did, and JOG, which clears them (the old JOG wrote nothing itself; its lamp M174) -,
    /// Reset (M102, lit by M800) and Security ByPass (M175 on / off; the old bar showed it to its
    /// Developer, Service and Admin users only - an Admin's here). Shown above the Manual tabs
    /// (StandardScreens Options.ManualBar).
    /// </summary>
    public partial class JogBar : Kaban.UI.Controls.MachineBarHMI
    {
        public JogBar()
        {
            InitializeComponent();
        }
    }
}
