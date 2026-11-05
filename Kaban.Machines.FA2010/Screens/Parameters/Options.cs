using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kaban.Machines.FA2010.Screens.Parameters
{
    public partial class Options : UserControl
    {
        /// <summary>
        /// The machine's log settings (its PLC log area, its logger's state, the KMSystem connection), in the Industry 4 Logging
        /// frame under the records' switches (5 November: "create a group box inside 'Options' page ... at the right end column
        /// and put these controls and database connection setting inside it").
        ///
        /// PUT IN ITS PLACE WHEN THE PAGE IS MADE, as the Axis page's jog bar (CLAUDE.md, 23 October): the designer shows a plain
        /// panel where it goes (industry4SettingsPlace: move or size it there). The frame follows it, and is shown only while
        /// HMI Settings > Options has the Industry 4 logging in the HMI on.
        /// </summary>
        internal Kaban.UI.Dashboard.MachineLogSettings machineLog;

        public Options()
        {
            InitializeComponent();
            machineLog = new Kaban.UI.Dashboard.MachineLogSettings("FA2010")
            {
                Location = industry4SettingsPlace.Location, Width = industry4SettingsPlace.Width, TabIndex = industry4SettingsPlace.TabIndex
            };
            grpIndustry4Logging.Controls.Remove(industry4SettingsPlace);
            industry4SettingsPlace.Dispose();
            grpIndustry4Logging.Controls.Add(machineLog);
            // the frame as tall as its rows and the settings under them
            machineLog.SizeChanged += (s, e) => grpIndustry4Logging.Height = machineLog.Bottom + grpIndustry4Logging.Padding.Bottom;
            Kaban.UI.Dashboard.MachineLogSettings.ShownWhileLogging(grpIndustry4Logging);
        }
    }
}
