using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// ONE COPY OF THE HMI AT A TIME (5 November: "It is better to not left two copies of the program run at the same
    /// time anyhow I think, it might cause other problems as well"). Two copies read and write the same PLCs (MX Component
    /// gives the second opener of a station an error), and save the same files under C:\ProgramData\KabanHMI over each
    /// other's - the PLC Monitor's lists were lost so. Every machine's exe holds the same named mutex while it runs: a
    /// second start (any machine's) brings the running one to the front, says why, and ends. A restart's new instance
    /// (--await-exit) waits for it instead - the old one lets it go just before starting it (MainForm.StartPendingRestart).
    /// </summary>
    public static class SingleInstance
    {
        /// <summary>The mutex every Kaban HMI exe holds - Global: one copy on the PC, whoever signed in to Windows.</summary>
        public const string MutexName = @"Global\Kaban.HMI.OneCopy";

        private static Mutex _held;

        /// <summary>Takes the mutex: false when another copy holds it (not given within <paramref name="waitMs"/>).</summary>
        internal static Func<int, bool> TryHold = waitMs =>
        {
            Mutex m;
            bool created;
            try { m = new Mutex(false, MutexName, out created); }
            catch (UnauthorizedAccessException) { return false; }   // made by an elevated copy: it runs
            bool got;
            try { got = m.WaitOne(waitMs); }
            catch (AbandonedMutexException) { got = true; }          // its holder ended without letting it go: ours now
            if (!got) { m.Dispose(); return false; }
            _held = m;
            return true;
        };

        /// <summary>Lets the mutex go - just before a restart starts its new instance.</summary>
        internal static Action Release = () =>
        {
            var m = _held;
            _held = null;
            if (m == null) return;
            try { m.ReleaseMutex(); } catch { }
            m.Dispose();
        };

        /// <summary>The running copy: its process name and when it started (null: none found - it is starting or closing).</summary>
        internal static Func<Tuple<string, DateTime?, IntPtr>> FindOther = () =>
        {
            int me = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcesses().Where(p => p.Id != me))
            {
                try
                {
                    if (!p.ProcessName.StartsWith("Kaban.Machines.", StringComparison.OrdinalIgnoreCase)) continue;
                    DateTime? started = null;
                    try { started = p.StartTime; } catch { }
                    return Tuple.Create(p.ProcessName, started, p.MainWindowHandle);
                }
                catch { }
            }
            return null;
        };

        /// <summary>Brings the other copy's window to the front (restored if minimised).</summary>
        internal static Action<IntPtr> BringToFront = handle =>
        {
            if (handle == IntPtr.Zero) return;
            try
            {
                if (IsIconic(handle)) ShowWindow(handle, 9 /* SW_RESTORE */);
                SetForegroundWindow(handle);
            }
            catch { }
        };

        /// <summary>How long a start by hand waits for a copy that is closing (no window any more).</summary>
        internal static int ClosingWaitMs = 10000;

        /// <summary>Another copy runs without a window: it is closing (or still starting) - worth waiting for.</summary>
        private static bool ClosingOther()
        {
            var o = FindOther();
            return o != null && o.Item3 == IntPtr.Zero;
        }

        /// <summary>Says so (a test reads it instead).</summary>
        internal static Action<string, string> Tell = (text, caption) =>
            MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);

        /// <summary>
        /// Called in Main right after AppBootstrap.AwaitPreviousInstance: true - this copy runs (it holds the mutex until it
        /// ends); false - another copy runs (it is brought to the front, the operator told why): end Main.
        /// </summary>
        public static bool Take(string[] args)
        {
            bool restart = args != null && args.Any(a => string.Equals(a, AppBootstrap.AwaitExitSwitch, StringComparison.OrdinalIgnoreCase));
            // a restart: the old copy let it go before starting this one, and AwaitPreviousInstance waited for it to end (and
            // ended it if it lingered) - a few seconds more; a start by hand: at once, unless the other copy is still closing
            if (TryHold(restart ? 15000 : 0) || (!restart && ClosingOther() && TryHold(ClosingWaitMs)))
            {
                Application.ApplicationExit += (s, e) => Release();
                return true;
            }

            var other = FindOther();
            // 6 November: "We have to make sure the application get closed before opening again, the created label loss was
            // probably caused by it" - never two copies, a restart's included: it ends as a second start does
            AuditLogger.Log("[ONE COPY] the HMI was started while another copy runs (" + (other == null ? "still starting or closing" : other.Item1) +
                            "): that one is brought to the front, this one ends.");
            Kaban.UI.Forms.MainForm.PrepareLanguageEarly();
            string text;
            if (other == null || other.Item3 == IntPtr.Zero)
                text = LanguageManager.GetString("Msg_OneCopyStarting",
                    "The HMI is already running, or still closing. Wait a few seconds and start it again.");
            else
            {
                string when = other.Item2.HasValue ? other.Item2.Value.ToString("HH:mm") : "?";
                text = string.Format(LanguageManager.GetString("Msg_OneCopyRunning",
                    "The HMI is already running ({0}, opened at {1}) - it is brought to the front."), other.Item1, when);
                BringToFront(other.Item3);
            }
            text += "\n\n" + LanguageManager.GetString("Msg_OneCopyWhy",
                "Only one copy of the HMI runs at a time: two copies would read and write the same PLCs and save the same settings and lists over each other's.");
            Tell(text, LanguageManager.GetString("Ttl_OneCopy", "The HMI is already running"));
            return false;
        }

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    }
}
