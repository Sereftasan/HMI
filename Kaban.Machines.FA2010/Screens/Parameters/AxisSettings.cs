using System.Windows.Forms;

namespace Kaban.Machines.FA2010.Screens.Parameters
{
    public partial class AxisSettings : UserControl
    {
        /// <summary>
        /// The machine's jog bar (Screens\Shared\AxisJogBar), at the top of the page.
        ///
        /// PUT IN ITS PLACE WHEN THE PAGE IS MADE, not by the designer (23 October: "I can't open the
        /// page still" - "Method not found: SpeedOverrideHMI.set_RequiredSecurity(SecurityLevel)", then
        /// "The variable 'axisJogBar' is either undeclared or was never assigned"). To show a control
        /// made in the page's own project (this .exe) Visual Studio's designer runs that control's
        /// compiled code - and there it could not find SpeedOverrideHMI.RequiredSecurity, which every
        /// build of Kaban.UI has had since the control was made (12 October): evidently the designer
        /// had a second copy of Kaban.UI / Kaban.Core loaded for the bar, whose SecurityLevel is not
        /// the page's. The bar was not made, and the page did not open.
        /// The designer now shows a plain panel where the bar goes (axisJogBarPlace: move or size it
        /// there); the page puts the bar into its place, with its bounds, when it is made.
        /// </summary>
        internal Kaban.Machines.FA2010.Screens.Shared.AxisJogBar axisJogBar;

        public AxisSettings()
        {
            InitializeComponent();
            axisJogBar = new Kaban.Machines.FA2010.Screens.Shared.AxisJogBar
            {
                Name = "axisJogBar", Bounds = axisJogBarPlace.Bounds, BackColor = axisJogBarPlace.BackColor,
                Anchor = axisJogBarPlace.Anchor, TabIndex = axisJogBarPlace.TabIndex
            };
            int at = Controls.GetChildIndex(axisJogBarPlace);
            Controls.Remove(axisJogBarPlace);
            axisJogBarPlace.Dispose();
            Controls.Add(axisJogBar);
            Controls.SetChildIndex(axisJogBar, at);
        }

    }
}

