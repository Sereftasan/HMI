using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Machines;
using Kaban.UI.Controls;

namespace Kaban.Machines.H500.Screens
{
    /// <summary>
    /// The sub-machine's own page. It is built in code from the mount context so
    /// every PLC address lands on whatever station the host mapped this module to -
    /// the same screen works on whichever station the host mounted it onto.
    /// </summary>
    public class H500Screen : UserControl
    {
        private readonly IMachineMountContext _ctx;

        public H500Screen(IMachineMountContext ctx)
        {
            _ctx = ctx;
            this.BackColor = Color.White;
            this.Dock = DockStyle.Fill;
            BuildUi();
        }

        private void BuildUi()
        {
            int station = _ctx.Station(1);   // resolved at mount time

            var title = new Label
            {
                Text = "H500",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 12)
            };
            Controls.Add(title);

            var unit1 = new CylinderUnitHMI
            {
                TitleTextKey = "Fnc_H500_Unit1",
                PlcNumber = station,
                CommandAAddress = "M10",
                OutputAAddress = "Y0",
                Lamp1AAddress = "X10",
                Lamp1ATipKey = "Tip_H500_AtHome",
                ButtonATextKey = "Btn_Down",
                CommandBAddress = "M11",
                OutputBAddress = "Y1",
                ButtonBTextKey = "Btn_Up",
                RequiredSecurity = SecurityLevel.Operator,
                EnableConditions = SystemCondition.ManualMode,
                Location = new Point(16, 48),
                Size = new Size(360, 74)
            };
            Controls.Add(unit1);

            var unit2 = new CylinderUnitHMI
            {
                TitleTextKey = "Fnc_H500_Unit2",
                PlcNumber = station,
                CommandAAddress = "M12",
                OutputAAddress = "Y2",
                ButtonATextKey = "Btn_Start",
                PositionAddress = "D10",
                PositionDigits = 4,
                PositionDecimalPlaces = 1,
                PositionSuffix = "mm",
                RequiredSecurity = SecurityLevel.Operator,
                EnableConditions = SystemCondition.ManualMode,
                Location = new Point(16, 136),
                Size = new Size(360, 74)
            };
            Controls.Add(unit2);
        }
    }
}

