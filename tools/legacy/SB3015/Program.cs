using System;
using System.Collections.Generic;
using KabanDevBase.Extensions;
using KabanMachinesSystem;
using KabanMachinesSystem.Business.Helpers;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Enums;
using System;
using System.Threading;
using System.Windows.Forms;

namespace SB3015
{
    static class Program
    {
        [STAThread]
        static void Main()
        {

            bool blnFirstInstance;
            Mutex objMutex = new Mutex(false, "SB3015", out blnFirstInstance);
            if (!blnFirstInstance)
            {
                KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.ProgramAlreadyRunning), KabanDevBase.Enums.MessageBoxType.Warning);
                return;
            }

            GC.KeepAlive(objMutex);

            CultureHelper.ChangeProgramCulture();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var loginForm = new frmLogin();
            if (loginForm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    Application.Run(new frmSB3015());
                }
                catch (Exception exp)
                {
                    throw;
                }
            }
        }
    }
}
