using System;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI
{
    /// <summary>
    /// Shared startup helper for every machine executable (FA2010, SB3012, SB3015...).
    /// Installs global exception handlers so an unhandled exception in any UI event
    /// (bad PLC response, null reference in a screen, etc.) logs the error and shows
    /// a friendly message instead of crashing the whole HMI or dumping a raw .NET
    /// stack trace on the factory floor.
    ///
    /// Call AppBootstrap.InstallGlobalExceptionHandlers() as the FIRST line of Main().
    /// </summary>
    public static class AppBootstrap
    {
        /// <summary>Command-line flag carrying the PID of the instance being replaced.</summary>
        public const string AwaitExitSwitch = "--await-exit";

        /// <summary>
        /// When this process was started by a RESTART, waits for the OLD instance to
        /// actually exit before returning. Call it FIRST in Main - before any driver
        /// is opened.
        ///
        /// This is what makes "Restart" equivalent to closing and reopening by hand.
        /// Without it the new process opened its PLC stations while the old one still
        /// held them, and MX Component hands the second opener an error rather than
        /// sharing the channel - so the restarted HMI came up with the connection
        /// dead and the new setting looking like it had not applied. Closing and
        /// starting again manually worked simply because the human pause was long
        /// enough.
        ///
        /// Waits on the real process handle rather than sleeping a guessed interval:
        /// how long the old instance takes to release depends on scanner threads
        /// finishing their current PLC round trip, which is not a fixed number.
        /// </summary>
        public static void AwaitPreviousInstance(string[] args)
        {
            if (args == null) return;

            int pid = -1;
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], AwaitExitSwitch, StringComparison.OrdinalIgnoreCase))
                    int.TryParse(args[i + 1], out pid);

            if (pid <= 0) return;

            try
            {
                var previous = System.Diagnostics.Process.GetProcessById(pid);
                Kaban.Core.Helpers.AuditLogger.Log(
                    "[RESTART] waiting for the previous instance (pid " + pid + ") to exit.");

                // Generous ceiling: a stuck predecessor must not leave the new one
                // hanging forever on a machine that has to come back up.
                if (!previous.WaitForExit(15000))
                    Kaban.Core.Helpers.AuditLogger.Log(
                        "[RESTART] previous instance still running after 15s; continuing anyway.");
                else
                    Kaban.Core.Helpers.AuditLogger.Log("[RESTART] previous instance exited.");
            }
            catch (ArgumentException)
            {
                // already gone - the normal, fast case
            }
            catch (Exception ex)
            {
                Kaban.Core.Helpers.AuditLogger.Log("[RESTART] could not wait for the previous instance: " + ex.Message);
            }
        }

        public static void InstallGlobalExceptionHandlers()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => HandleFatal(e.Exception, "UI thread");
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                HandleFatal(e.ExceptionObject as Exception, "background");
        }

        private static void HandleFatal(Exception ex, string source)
        {
            try
            {
                AuditLogger.Log($"UNHANDLED {source} EXCEPTION: {ex}");
            }
            catch { /* logging must never crash the crash handler */ }

            MessageBox.Show(
                "An unexpected error occurred. The action was cancelled.\n\n" +
                "Details have been written to the audit log. If this keeps happening, " +
                "please contact maintenance.\n\n" + (ex != null ? ex.Message : "Unknown error"),
                "HMI Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

