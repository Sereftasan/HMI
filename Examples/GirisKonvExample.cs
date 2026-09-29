// =============================================================================
// EXAMPLE: Giriş Konveyör manual page built from CylinderUnitHMI (merged style)
// -----------------------------------------------------------------------------
// Reference snippet (not compiled into any project). Copy CreateSikmaUnits into
// the screen that hosts the manual controls and call it after
// InitializeComponent(). PlcScanner discovers every child automatically.
//
// Each unit renders per the sketch: title on top, SIK/AC buttons stacked and
// merged in one frame. Button shading: MAN_ relay (M) ON -> slightly darker;
// OUT_ output (Y) ON -> darker + white text. The end-position sensors (X) are
// the two small lamps embedded in each button's outer corner.
//
// BaseOffset = row index; addresses step per device numbering (X/Y octal,
// M decimal) - verified against the real symbol table for Sikma 1..7.
// =============================================================================

using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.UI.Controls;

namespace Kaban.Machines.SB3012.Screens // adapt to your machine project
{
    public static class GirisKonvExample
    {
        /// <summary>Creates the 7 clamp units and adds them to the given host.</summary>
        public static void CreateSikmaUnits(Control host)
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(8)
            };
            host.Controls.Add(flow);

            for (int i = 0; i < 7; i++)
            {
                var unit = new CylinderUnitHMI
                {
                    // identity
                    TitleText = $"Sıkma {i + 1}",
                    TranslationKey = $"Fnc_GirisKonv_Sikma{i + 1}",
                    TitleTip = "Tip_GirisKonv_Sikma",

                    // THE ONLY PER-UNIT NUMBER:
                    BaseOffset = i,

                    // Action A = SIK (LEFT button) - row 0 absolute addresses:
                    CommandAAddress = "M683",   // MAN_GirisKonv_Sikma1_SIK (relay defaults to this)
                    OutputAAddress = "Y5",      // OUT_GirisKonv_Sikma1_SIK -> darker + white text
                    Lamp1AAddress = "X36",      // IN_GirisKonv_Sikma1_Ileride
                    Lamp1ATip = "Silindir ileride (sıkılı)",
                    ButtonAText = "SIK ▲",

                    // Action B = AC (RIGHT button):
                    CommandBAddress = "M690",   // MAN_GirisKonv_Sikma1_AC
                    OutputBAddress = "Y14",     // OUT_GirisKonv_Sikma1_AC
                    Lamp1BAddress = "X27",      // IN_GirisKonv_Sikma1_Geride
                    Lamp1BTip = "Silindir geride (açık)",
                    ButtonBText = "AÇ ▼",

                    // behavior
                    RequiredSecurity = SecurityLevel.Operator,
                    EnableConditions = SystemCondition.ManualMode,
                    LogAction = true,

                    Size = new System.Drawing.Size(320, 84),
                    Margin = new Padding(6)
                };

                flow.Controls.Add(unit);
            }
        }
    }
}

