namespace Kaban.Machines.MK2010.Screens.Shared
{
    /// <summary>
    /// MK2010's manual movement bar (11 November, from the old program's _MK2010: the manual
    /// pages' top bar, ucHeadControlButtons): the axes' manual speed (D409). Shown above the
    /// Manual Movement tabs (StandardScreens Options.ManualBar).
    /// </summary>
    public partial class JogBar : Kaban.UI.Controls.MachineBarHMI
    {
        public JogBar()
        {
            InitializeComponent();
        }
    }
}
