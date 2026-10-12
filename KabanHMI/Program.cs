using System;
using System.Windows.Forms;
using Kaban.UI.Forms;

namespace Kaban.UI
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Global exception handling: an unhandled exception must not take the
            // machine's HMI down without a trace (see AppBootstrap).
            AppBootstrap.InstallGlobalExceptionHandlers();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}

