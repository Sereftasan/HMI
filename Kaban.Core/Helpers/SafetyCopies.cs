using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// A FILE'S FIRST VERSION OF THE DAY, KEPT BEFORE IT IS CHANGED (23 October: "I think we should make sure the
    /// previous data is backed up as the safe heaven before we are doing the imports (automatically take the backup
    /// ...)"): before the program writes a file of its configuration - a translation file saved from HMI Settings >
    /// Translation (an import's texts, an AI's, a text typed) - the file as it is now is copied, the first time that
    /// day, into ProgramData\KabanHMI\SafetyCopies\&lt;what&gt;\&lt;yyyy-MM-dd&gt;\ (its path under the program's
    /// folder kept). The day's later changes are in the translation history; the morning's file is here.
    /// </summary>
    public static class SafetyCopies
    {
        /// <summary>Where the copies are (replaceable for the tests).</summary>
        public static string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "SafetyCopies");

        /// <summary>The program's folder: a file under it is kept by its path from there.</summary>
        public static string ProgramFolder = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>Today's folder of copies of a kind ("Translations").</summary>
        public static string TodayFolder(string what)
        {
            return Path.Combine(Folder, what ?? "Files", DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Where a file's copy of today is (it may not be there yet).</summary>
        public static string CopyOf(string what, string path)
        {
            string full;
            try { full = Path.GetFullPath(path); } catch { full = path ?? ""; }
            string root = string.IsNullOrEmpty(ProgramFolder) ? "" : Path.GetFullPath(ProgramFolder).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string rel = root.Length > 0 && full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(root.Length)
                : "_other" + Path.DirectorySeparatorChar + full.Replace(":", "").TrimStart('\\', '/');
            return Path.Combine(TodayFolder(what), rel);
        }

        /// <summary>
        /// The file copied before it is written - the first time today (true: copied now). A file not there yet has
        /// nothing to keep. A copy that fails does not stop the write: it is in the log.
        /// </summary>
        public static bool FirstOfDay(string what, string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                var copy = CopyOf(what, path);
                if (File.Exists(copy)) return false;
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.Copy(path, copy, false);
                AuditLogger.Log("safety copy of " + path + " (before its first change today): " + copy);
                return true;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("safety copy of " + path + " not made: " + ex.Message);
                return false;
            }
        }
    }
}
