using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Kaban.Core.Helpers
{
    /// <summary>A backup made: its file, when, how large.</summary>
    public sealed class DataBackupFile
    {
        public string Path = "";
        public DateTime At;
        public long Bytes;
        public string Name { get { return System.IO.Path.GetFileName(Path); } }
    }

    /// <summary>
    /// THE HMI'S DATA BACKED UP (19 November - the old program's Backup Settings: its database, its program and its languages copied to
    /// C:\KabanMachine\DB_Backups, restored from there): everything the HMI keeps in C:\ProgramData\KabanHMI - the profile library
    /// (Kaban.db), the users, the settings (*.cfg), the labels, the notes, the translations edited, the production log - in one zip a
    /// backup, in a folder of its own choosing (an USB stick or a network folder). Daily by itself when switched on, the newest kept.
    /// Restored: the current data backed up first ("before restore"), then the zip's files written back - the program restarts after.
    /// Not in it: the audit logs, the PLC backups and safety copies (they have their own folders) and the backups themselves.
    /// </summary>
    public static class DataBackup
    {
        /// <summary>The folder backed up (tests replace it).</summary>
        public static string DataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI");
        public static string SettingsPath { get { return Path.Combine(DataFolder, "DataBackup.cfg"); } }
        public static readonly string[] LeftOut = { "Backups", "AuditLogs", "PlcBackups", "SafetyCopies" };
        private static readonly object Gate = new object();
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private static System.Threading.Timer _timer;

        private static string _folder;
        private static bool _daily, _loaded;
        private static int _keep = 14;

        public static string Folder { get { lock (Gate) { Load(); return string.IsNullOrWhiteSpace(_folder) ? Path.Combine(DataFolder, "Backups") : _folder; } } }
        public static bool Daily { get { lock (Gate) { Load(); return _daily; } } }
        public static int Keep { get { lock (Gate) { Load(); return _keep; } } }

        public static void SetSettings(string folder, bool daily, int keep)
        {
            lock (Gate)
            {
                Load();
                _folder = (folder ?? "").Trim();
                _daily = daily;
                _keep = Math.Max(1, Math.Min(365, keep));
                try { SafeFile.WriteAllText(SettingsPath, "folder\t" + _folder + "\r\ndaily\t" + (_daily ? "on" : "off") + "\r\nkeep\t" + _keep.ToString(CI) + "\r\n"); }
                catch (Exception ex) { AuditLogger.Log("[BACKUP] " + ex.Message); }
            }
            AuditLogger.Log("[BACKUP] the settings: " + Folder + ", daily " + (daily ? "on" : "off") + ", the newest " + Keep + " kept.");
        }

        /// <summary>Reads the settings again (tests).</summary>
        public static void Reload() { lock (Gate) { _loaded = false; Load(); } }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _folder = ""; _daily = false; _keep = 14;
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    var t = line.Split('\t');
                    int n;
                    if (t.Length < 2) continue;
                    if (t[0] == "folder") _folder = t[1].Trim();
                    else if (t[0] == "daily") _daily = t[1] == "on";
                    else if (t[0] == "keep" && int.TryParse(t[1], NumberStyles.Integer, CI, out n) && n > 0) _keep = n;
                }
            }
            catch { }
        }

        /// <summary>The backups in the folder, newest first.</summary>
        public static List<DataBackupFile> List()
        {
            var list = new List<DataBackupFile>();
            try
            {
                var dir = Folder;
                if (!Directory.Exists(dir)) return list;
                foreach (var f in Directory.GetFiles(dir, "KabanHMI_Data_*.zip"))
                {
                    var fi = new FileInfo(f);
                    list.Add(new DataBackupFile { Path = f, At = fi.LastWriteTime, Bytes = fi.Length });
                }
            }
            catch (Exception ex) { AuditLogger.Log("[BACKUP] " + ex.Message); }
            return list.OrderByDescending(b => b.At).ToList();
        }

        private static bool IsLeftOut(string relative)
        {
            var first = relative.Split(new[] { '\\', '/' }, 2)[0];
            return LeftOut.Any(x => string.Equals(x, first, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>A backup now (its name says when and why); its file. Throws when it cannot be written.</summary>
        public static string Create(string why = "")
        {
            lock (Gate)
            {
                var dir = Folder;
                Directory.CreateDirectory(dir);
                string tag = string.IsNullOrWhiteSpace(why) ? "" : "_" + new string(why.Where(char.IsLetterOrDigit).Take(20).ToArray());
                string path = Path.Combine(dir, "KabanHMI_Data_" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CI) + tag + ".zip");
                int files = 0;
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    if (Directory.Exists(DataFolder))
                        foreach (var f in Directory.GetFiles(DataFolder, "*", SearchOption.AllDirectories))
                        {
                            string rel = f.Substring(DataFolder.Length).TrimStart('\\', '/');
                            if (IsLeftOut(rel)) continue;
                            if (Path.GetFullPath(f).StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase)) continue;
                            try
                            {
                                // a file open in the program (the library's database): read beside it
                                using (var src = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                                {
                                    var e = zip.CreateEntry(rel.Replace('\\', '/'), CompressionLevel.Optimal);
                                    e.LastWriteTime = File.GetLastWriteTime(f);
                                    using (var dst = e.Open()) src.CopyTo(dst);
                                    files++;
                                }
                            }
                            catch (Exception ex) { AuditLogger.Log("[BACKUP] " + f + " left out: " + ex.Message); }
                        }
                }
                AuditLogger.Log("[BACKUP] the HMI's data backed up: " + path + " (" + files + " files)" + (why.Length > 0 ? " - " + why : ""));
                Prune();
                return path;
            }
        }

        /// <summary>The oldest backups beyond the number kept deleted (the ones made before a restore stay).</summary>
        private static void Prune()
        {
            var daily = List().Where(b => !b.Name.Contains("_beforerestore")).ToList();
            foreach (var old in daily.Skip(Keep))
                try { File.Delete(old.Path); AuditLogger.Log("[BACKUP] the old backup " + old.Name + " deleted (the newest " + Keep + " kept)."); } catch { }
        }

        /// <summary>The files in a backup (its look before a restore).</summary>
        public static List<string> Contents(string zipPath)
        {
            using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                return zip.Entries.Where(e => e.Name.Length > 0).Select(e => e.FullName).ToList();
        }

        /// <summary>
        /// The backup's files written back over the HMI's data - the current data backed up first. Returns the files written; throws
        /// when the zip cannot be read (nothing written then).
        /// </summary>
        public static int Restore(string zipPath)
        {
            var entries = Contents(zipPath);                    // read before anything is changed
            if (entries.Count == 0) throw new InvalidDataException("the backup holds no file");
            Create("beforerestore");
            int n = 0;
            lock (Gate)
            {
                using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                    foreach (var e in zip.Entries)
                    {
                        if (e.Name.Length == 0) continue;
                        var rel = e.FullName.Replace('/', Path.DirectorySeparatorChar);
                        // the backup's own settings stay as they are now (where the backups go)
                        if (rel.Contains("..") || IsLeftOut(rel) || string.Equals(rel, "DataBackup.cfg", StringComparison.OrdinalIgnoreCase)) continue;
                        var target = Path.Combine(DataFolder, rel);
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            using (var src = e.Open())
                            using (var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                                src.CopyTo(dst);
                            n++;
                        }
                        catch (Exception ex) { AuditLogger.Log("[BACKUP] " + target + " not restored: " + ex.Message); }
                    }
                _loaded = false;
            }
            AuditLogger.Log("[BACKUP] restored from " + zipPath + ": " + n + " files - the program restarts to use them.");
            return n;
        }

        /// <summary>A daily backup when it is due (looked at when the program starts and every hour).</summary>
        public static void Start()
        {
            lock (Gate)
            {
                if (_timer != null) return;
                _timer = new System.Threading.Timer(_ => { try { DailyNow(DateTime.Now); } catch (Exception ex) { AuditLogger.Log("[BACKUP] " + ex.Message); } }, null, 60000, 3600000);
            }
        }

        public static void Stop() { lock (Gate) { if (_timer != null) _timer.Dispose(); _timer = null; } }

        /// <summary>The day's backup made when daily is on and there is none of today yet; true when made.</summary>
        public static bool DailyNow(DateTime now)
        {
            if (!Daily) return false;
            if (List().Any(b => b.At.Date == now.Date && !b.Name.Contains("_beforerestore"))) return false;
            Create("daily");
            return true;
        }
    }
}
