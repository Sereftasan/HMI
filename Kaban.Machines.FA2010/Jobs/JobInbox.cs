using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Kaban.Core.Helpers;

namespace Kaban.Machines.FA2010.Jobs
{
    /// <summary>
    /// THE TRANSFER FOLDER (the old program's KabanFileSystemWatcher - "C:\KabanTransfer": the window software writes the job file
    /// there and the machine's job list has it at once, "New job received"): a folder set on the Jobs page is watched; a new job
    /// file (.kbn) in it - once the window software has finished writing it - comes to the top of the Jobs page's list, said in a
    /// note. A file is announced once (JobInbox.cfg keeps the ones seen, by name and time); the folder is also looked through
    /// every 10 s (a network folder's changes are not always told).
    /// </summary>
    internal static class JobInbox
    {
        internal static string StatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "JobInbox.cfg");
        /// <summary>How long a file may stay locked (still being written) before it is said to be unreadable.</summary>
        internal static int MaxWaitSeconds = 60;
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, long> _seen = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> _new = new List<string>();
        private static string _folder = "";
        /// <summary>Job files older than this many days moved into the folder's Archive (0: none - the old KabanTransferCleaner emptied it).</summary>
        private static int _archiveDays;
        private static bool _loaded;
        private static FileSystemWatcher _watcher;
        private static System.Threading.Timer _timer;
        private static readonly Dictionary<string, DateTime> _waiting = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>A new job file arrived (on a worker thread).</summary>
        internal static event Action<string> Arrived;

        internal static string Folder { get { lock (Gate) { Load(); return _folder; } } }
        internal static int ArchiveDays { get { lock (Gate) { Load(); return _archiveDays; } } }

        /// <summary>How old a job file gets before it is moved into the Archive subfolder (0: never).</summary>
        internal static void SetArchiveDays(int days)
        {
            lock (Gate) { Load(); _archiveDays = Math.Max(0, days); Save(); }
            AuditLogger.Log("[JOB] the transfer folder's job files " + (days > 0 ? "older than " + days + " day(s) go into its Archive" : "stay where they are"));
        }

        /// <summary>
        /// The job files older than ArchiveDays moved into Archive\yyyy-MM under the transfer folder (19 November - the old program's
        /// KabanTransferCleaner, which deleted them). Returns how many moved.
        /// </summary>
        internal static int ArchiveOld(DateTime now)
        {
            string folder = Folder;
            int days = ArchiveDays;
            if (days <= 0 || folder.Length == 0 || !Directory.Exists(folder)) return 0;
            int moved = 0;
            foreach (var f in Directory.GetFiles(folder, "*.kbn"))
            {
                try
                {
                    var at = File.GetLastWriteTime(f);
                    if ((now - at).TotalDays < days) continue;
                    var dir = Path.Combine(folder, "Archive", at.ToString("yyyy-MM", CI));
                    Directory.CreateDirectory(dir);
                    var target = Path.Combine(dir, Path.GetFileName(f));
                    for (int i = 2; File.Exists(target); i++) target = Path.Combine(dir, Path.GetFileNameWithoutExtension(f) + " (" + i + ")" + Path.GetExtension(f));
                    File.Move(f, target);
                    moved++;
                    lock (Gate) { _seen.Remove(f); _new.RemoveAll(p => string.Equals(p, f, StringComparison.OrdinalIgnoreCase)); }
                }
                catch (Exception ex) { AuditLogger.Log("[JOB] " + f + " not archived: " + ex.Message); }
            }
            if (moved > 0) { lock (Gate) Save(); AuditLogger.Log("[JOB] " + moved + " job file(s) older than " + days + " day(s) moved into " + Path.Combine(folder, "Archive")); }
            return moved;
        }

        /// <summary>The files arrived and not opened yet, newest first.</summary>
        internal static List<string> New { get { lock (Gate) { Load(); return _new.Where(File.Exists).ToList(); } } }

        /// <summary>The transfer folder set (the page; "" none): watched from now on.</summary>
        internal static void SetFolder(string folder)
        {
            lock (Gate)
            {
                Load();
                _folder = (folder ?? "").Trim();
                Save();
            }
            AuditLogger.Log("[JOB] the transfer folder: " + (Folder.Length > 0 ? Folder : "none"));
            Restart();
        }

        /// <summary>A file opened on the page: no longer new.</summary>
        internal static void Opened(string path)
        {
            lock (Gate)
            {
                if (_new.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) > 0) Save();
            }
        }

        internal static void Start()
        {
            lock (Gate)
            {
                Load();
                if (_timer != null) return;
                _timer = new System.Threading.Timer(_ => { try { Scan(); } catch (Exception ex) { AuditLogger.Log("[JOB] the transfer folder: " + ex.Message); } }, null, 1000, 10000);
            }
            Restart();
        }

        internal static void Stop()
        {
            lock (Gate)
            {
                if (_timer != null) _timer.Dispose();
                _timer = null;
                if (_watcher != null) { _watcher.EnableRaisingEvents = false; _watcher.Dispose(); _watcher = null; }
            }
        }

        private static void Restart()
        {
            lock (Gate)
            {
                if (_watcher != null) { _watcher.EnableRaisingEvents = false; _watcher.Dispose(); _watcher = null; }
                if (_folder.Length == 0 || _timer == null) return;
                try
                {
                    if (!Directory.Exists(_folder)) Directory.CreateDirectory(_folder);
                    _watcher = new FileSystemWatcher(_folder, "*.kbn") { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
                    FileSystemEventHandler h = (s, e) => { try { Scan(); } catch { } };
                    _watcher.Created += h;
                    _watcher.Changed += h;
                    _watcher.Renamed += (s, e) => { try { Scan(); } catch { } };
                    _watcher.EnableRaisingEvents = true;
                }
                catch (Exception ex) { AuditLogger.Log("[JOB] the transfer folder " + _folder + " cannot be watched: " + ex.Message + " (looked through every 10 s)"); }
            }
        }

        private static readonly object ScanGate = new object();
        private static DateTime _lastArchive = DateTime.MinValue;

        /// <summary>The folder looked through now: a new, finished job file announced (tests call it).</summary>
        internal static void Scan()
        {
            if (!System.Threading.Monitor.TryEnter(ScanGate)) return;
            try
            {
                string folder = Folder;
                if (folder.Length == 0 || !Directory.Exists(folder)) return;
                // the old ones out of the way first (once an hour is plenty)
                if ((DateTime.UtcNow - _lastArchive).TotalMinutes >= 60) { _lastArchive = DateTime.UtcNow; ArchiveOld(DateTime.Now); }
                var arrived = new List<string>();
                foreach (var f in Directory.GetFiles(folder, "*.kbn").OrderBy(x => File.GetLastWriteTimeUtc(x)))
                {
                    long stamp = File.GetLastWriteTimeUtc(f).Ticks;
                    lock (Gate)
                    {
                        long was;
                        if (_seen.TryGetValue(f, out was) && was == stamp) continue;
                    }
                    if (!Finished(f))
                    {
                        // still being written: looked at again - said once it has waited too long
                        lock (Gate)
                        {
                            DateTime first;
                            if (!_waiting.TryGetValue(f, out first)) _waiting[f] = first = DateTime.UtcNow;
                            if ((DateTime.UtcNow - first).TotalSeconds < MaxWaitSeconds) continue;
                            _waiting.Remove(f);
                        }
                        AuditLogger.Log("[JOB] the transfer folder: " + f + " cannot be opened - another program keeps it.");
                        lock (Gate) { _seen[f] = stamp; Save(); }
                        continue;
                    }
                    lock (Gate)
                    {
                        _waiting.Remove(f);
                        _seen[f] = stamp;
                        _new.RemoveAll(p => string.Equals(p, f, StringComparison.OrdinalIgnoreCase));
                        _new.Insert(0, f);
                        while (_new.Count > 30) _new.RemoveAt(_new.Count - 1);
                        // the files seen that are gone forgotten
                        foreach (var gone in _seen.Keys.Where(k => !File.Exists(k)).ToList()) _seen.Remove(gone);
                        Save();
                    }
                    arrived.Add(f);
                }
                foreach (var f in arrived)
                {
                    AuditLogger.Log("[JOB] new job received: " + f);
                    var h = Arrived;
                    if (h != null) try { h(f); } catch (Exception ex) { AuditLogger.Log("[JOB] " + ex.Message); }
                }
            }
            finally { System.Threading.Monitor.Exit(ScanGate); }
        }

        /// <summary>The window software has finished writing it (the old FileUploadCompleted: it opens on its own).</summary>
        private static bool Finished(string path)
        {
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) return true;
            }
            catch { return false; }
        }

        /// <summary>Everything forgotten (tests).</summary>
        internal static void Reset()
        {
            Stop();
            lock (Gate) { _seen.Clear(); _new.Clear(); _waiting.Clear(); _folder = ""; _archiveDays = 0; _loaded = true; }
            _lastArchive = DateTime.MinValue;
        }

        private static void Save()
        {
            var sb = new StringBuilder("# Kaban HMI - FA2010's transfer folder and the job files seen in it\r\n");
            sb.Append("folder\t").Append(_folder).Append("\r\n");
            sb.Append("archive\t").Append(_archiveDays.ToString(CI)).Append("\r\n");
            foreach (var p in _new) sb.Append("new\t").Append(p).Append("\r\n");
            foreach (var kv in _seen) sb.Append("seen\t").Append(kv.Key).Append('\t').Append(kv.Value.ToString(CI)).Append("\r\n");
            try { SafeFile.WriteAllText(StatePath, sb.ToString()); }
            catch (Exception ex) { AuditLogger.Log("[JOB] " + StatePath + " could not be written: " + ex.Message); }
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _seen.Clear(); _new.Clear(); _folder = ""; _archiveDays = 0;
            try
            {
                if (!File.Exists(StatePath)) return;
                foreach (var line in File.ReadAllLines(StatePath, Encoding.UTF8))
                {
                    var t = line.Split('\t');
                    long n;
                    int days;
                    if (t.Length >= 2 && t[0] == "folder") _folder = t[1].Trim();
                    else if (t.Length >= 2 && t[0] == "archive" && int.TryParse(t[1], NumberStyles.Integer, CI, out days)) _archiveDays = Math.Max(0, days);
                    else if (t.Length >= 2 && t[0] == "new") _new.Add(t[1]);
                    else if (t.Length >= 3 && t[0] == "seen" && long.TryParse(t[2], NumberStyles.Integer, CI, out n)) _seen[t[1]] = n;
                }
            }
            catch (Exception ex) { AuditLogger.Log("[JOB] " + StatePath + " could not be read: " + ex.Message); }
        }
    }
}
