
using System.Windows.Forms;

namespace Kaban.Machines.FA2010.Screens
{
    public partial class HomeScreen : UserControl
    {
        public HomeScreen()
        {
            InitializeComponent();

            // This line does nothing at runtime, but it forces the Visual Studio Designer 
            // to load the Machine project and run the MachineConfig constructor.
            if (DesignMode || System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime)
            {
                MachineConfig.Initialize();
            }
        }
    }
}

