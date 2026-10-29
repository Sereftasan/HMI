using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Kaban.Core.Helpers
{
    public static class AuditLogger
    {
        // Anchored to a fixed per-machine location, NOT a relative filename: a
        // relative path makes the log land wherever the process was started from,
        // which on a panel PC means nobody can find it. Daily files, auto-pruned.
        //
        // Settable so a test can log into a folder of its own; the HMI never
        // changes it.
        public static string LogDirectory { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "AuditLogs");

        /// <summary>The daily files are named for their day: HmiAudit_yyyy-MM-dd.txt.</summary>
        public const string FilePrefix = "HmiAudit_";

        /// <summary>The file of a given day (it may not exist).</summary>
        public static string FileFor(DateTime day)
        {
            return Path.Combine(LogDirectory, FilePrefix + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt");
        }

        // Keep this many days of daily log files before deleting the oldest.
        // Settable from the Log Settings page (persisted there).
        public static int RetainDays { get; set; } = 90;

        /// <summary>Master switch - the Log Settings page can pause auditing.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// Today's log file - the ONE place its name is spelled. The viewer and the
        /// "export today" button each built their own name ("yyyy-MM-dd.txt")
        /// while Log() writes "HmiAudit_yyyy-MM-dd.txt", so the viewer was always
        /// empty and the export always said there was nothing to export.
        /// </summary>
        public static string TodayFile
        {
            get { return FileFor(DateTime.Now); }
        }

        /// <summary>Last N lines of today's log (for the on-screen viewer).</summary>
        public static string[] Tail(int lines)
        {
            try
            {
                string f = TodayFile;
                if (!File.Exists(f)) return new string[0];
                // SHARE WRITE: File.ReadAllLines opens with FileShare.Read, which
                // makes Log()'s append fail while the viewer is reading - and Log()
                // swallows that, so the line was silently lost. The viewer polls
                // every 2 s, so this happened in normal use.
                var all = new List<string>();
                using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read,
                                               FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null) all.Add(line);
                }
                int start = Math.Max(0, all.Count - lines);
                return all.GetRange(start, all.Count - start).ToArray();
            }
            catch { return new string[0]; }
        }

        private static readonly object _lock = new object();
        private static DateTime _lastCleanup = DateTime.MinValue;

        /// <summary>
        /// Raised for each entry as it is written (29 October: the HMI's log goes into the
        /// machine's log database too - HmiLogStore copies each entry there). The file stays
        /// the first place it is written, and the one read when the database cannot be.
        /// </summary>
        public static event Action<AuditEntry> Logged;

        public static void Log(string action)
        {
            if (!Enabled) return;
            try
            {
                var now = DateTime.Now;
                string timestamp = now.ToString("yyyy-MM-dd HH:mm:ss");
                string user = SecurityManager.CurrentUser ?? "Guest";
                string logLine = $"[{timestamp}] - User '{user}' {action}";

                lock (_lock)
                {
                    Directory.CreateDirectory(LogDirectory);
                    string file = TodayFile;
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

                var h = Logged;
                if (h != null)
                {
                    var text = (action ?? "").Replace("\r\n", "\n");
                    try
                    {
                        h(new AuditEntry
                        {
                            Time = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second),
                            User = user, Text = text, Kind = AuditLogReader.Classify(text), Station = AuditLogReader.StationOf(text)
                        });
                    }
                    catch { }
                }
            }
            catch { /* Ignore logging failures so we don't crash the HMI */ }
        }
    }

    public static class BlinkHelper
    {
        // Returns true for 500ms, then false for 500ms, synchronized globally.
        // Blinking off (View Preferences): always the active half - the mark lit, steady.
        public static bool IsBlinkActive => !ViewPreferences.BlinkingOn || (Environment.TickCount % 1000) > 500;
    }
}

