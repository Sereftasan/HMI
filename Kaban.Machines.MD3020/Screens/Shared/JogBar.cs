namespace Kaban.Machines.MD3020.Screens.Shared
{
    /// <summary>
    /// MD3020's manual movement bar (11 November, from the old program's _MD3030: its manual and
    /// parameter pages' top bar, ucHeadControlButtons): the axes' manual speed (D409) and how far
    /// a press moves them - JOG (M90), 1 mm (M2), 0.1 mm (M1), 0.01 mm (M0), each setting its own
    /// bit as the old buttons did. Shown above the Manual Movement tabs (StandardScreens
    /// Options.ManualBar).
    /// </summary>
    public partial class JogBar : Kaban.UI.Controls.MachineBarHMI
    {
        public JogBar()
        {
            InitializeComponent();
        }
    }
}
