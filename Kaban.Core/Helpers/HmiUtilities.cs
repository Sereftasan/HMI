using System;
using System.IO;

namespace Kaban.Core.Helpers
{
    public static class AuditLogger
    {
        // Anchored to a fixed per-machine location, NOT a relative filename: a
        // relative path makes the log land wherever the process was started from,
        // which on a panel PC means nobody can find it. Daily files, auto-pruned.
        public static string LogDirectory { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "AuditLogs");

        // Keep this many days of daily log files before deleting the oldest.
        // Settable from the Log Settings page (persisted there).
        public static int RetainDays { get; set; } = 90;

        /// <summary>Master switch - the Log Settings page can pause auditing.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>Last N lines of today's log (for the on-screen viewer).</summary>
        public static string[] Tail(int lines)
        {
            try
            {
                string f = Path.Combine(LogDirectory, DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
                if (!File.Exists(f)) return new string[0];
                var all = File.ReadAllLines(f);
                int start = Math.Max(0, all.Length - lines);
                var outp = new string[all.Length - start];
                Array.Copy(all, start, outp, 0, outp.Length);
                return outp;
            }
            catch { return new string[0]; }
        }

        private static readonly object _lock = new object();
        private static DateTime _lastCleanup = DateTime.MinValue;

        public static void Log(string action)
        {
            if (!Enabled) return;
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string user = SecurityManager.CurrentUser ?? "Guest";
                string logLine = $"[{timestamp}] - User '{user}' {action}";

                lock (_lock)
                {
                    Directory.CreateDirectory(LogDirectory);
                    string file = Path.Combine(LogDirectory, $"HmiAudit_{DateTime.Now:yyyy-MM-dd}.txt");
                    File.AppendAllText(file, logLine + Environment.NewLine);

                    // Rotate: prune old daily files at most once a day.
                    if ((DateTime.Now - _lastCleanup).TotalHours > 24)
                    {
                        _lastCleanup = DateTime.Now;
                        foreach (var old in Directory.GetFiles(LogDirectory, "HmiAudit_*.txt"))
                        {
                            if (File.GetLastWriteTime(old) < DateTime.Now.AddDays(-RetainDays))
                                File.Delete(old);
                        }
                    }
                }

                // Also print to console for debugging
                Console.WriteLine(logLine);
            }
            catch { /* Ignore logging failures so we don't crash the HMI */ }
        }
    }

    public static class BlinkHelper
    {
        // Returns true for 500ms, then false for 500ms, synchronized globally.
        public static bool IsBlinkActive => (Environment.TickCount % 1000) > 500;
    }
}

