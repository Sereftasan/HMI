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
        /// On a restart's command line when the program restarted was being debugged (30 October: "Unable to copy
        /// file ... Kaban.Machines.H500.exe ... The file is locked by: Kaban.Machine.FA2010"): the new instance runs
        /// outside Visual Studio, which stops only the one it started - left open, it holds the exes and a build cannot
        /// replace them. The new instance says so (MainForm).
        /// </summary>
        public const string FromDebuggerSwitch = "--restarted-from-debugger";

        /// <summary>Whether this instance is a restart of one Visual Studio was debugging.</summary>
        public static bool RestartedFromDebugger(string[] args)
        {
            if (args == null) return false;
            foreach (var a in args) if (string.Equals(a, FromDebuggerSwitch, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

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

                // 6 November: "We have to make sure the application get closed before opening again" - the old instance
                // closed its window, saved its lists and settings and let its stations go BEFORE it started this one (MainForm
                // starts it at FormClosed); what still holds it is threads ending. Waited for, and ENDED if it lingers - never
                // run beside it (two copies wrote the same files: the PLC Monitor's labels were lost so).
                if (!previous.WaitForExit(15000))
                {
                    Kaban.Core.Helpers.AuditLogger.Log("[RESTART] previous instance still running after 15 s, its window closed: it is ended.");
                    try { previous.Kill(); previous.WaitForExit(5000); }
                    catch (Exception ex) { Kaban.Core.Helpers.AuditLogger.Log("[RESTART] could not end the previous instance: " + ex.Message); }
                }
                else
                    Kaban.Core.Helpers.AuditLogger.Log("[RESTART] previous instance exited.");
            }
            catch (ArgumentException)
            {
                // Already gone - the normal case now that the new instance is
                // started only after the old one has closed its stations.
                Kaban.Core.Helpers.AuditLogger.Log("[RESTART] the previous instance (pid " + pid + ") had already exited.");
            }
            catch (Exception ex)
            {
                Kaban.Core.Helpers.AuditLogger.Log("[RESTART] could not wait for the previous instance: " + ex.Message);
            }
        }

        /// <summary>
        /// THE PROCESS ENDS once its window has closed (6 November): everything is saved by then (FormClosing / FormClosed); a
        /// thread still running (a PLC call, a log writer) must not keep a closed HMI alive - the next start waits for it. Ended
        /// after <paramref name="graceMs"/> if it is still there.
        /// </summary>
        public static void EndSoonAfterClose(int graceMs = 5000)
        {
            var t = new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(graceMs);
                try { Kaban.Core.Helpers.AuditLogger.Log("[EXIT] the window closed " + graceMs / 1000 + " s ago and the process was still running: it is ended."); } catch { }
                Environment.Exit(0);
            }) { IsBackground = true, Name = "End after close" };
            t.Start();
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

