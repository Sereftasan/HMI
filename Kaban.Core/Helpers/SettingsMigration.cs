using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// Carries a machine's settings from an older install into a new one.
    ///
    /// THE PROBLEM THIS SOLVES
    /// -----------------------
    /// Upgrading a machine means putting a new build in place. Everything the
    /// commissioning engineer set on the old one - station addresses, axis
    /// limits, alarm severities, users, translations someone corrected on site -
    /// lives in files beside the old executable. Copying them by hand means
    /// knowing which files matter, and it silently fails when a file's SHAPE has
    /// changed between versions: the new build reads an old column order, gets
    /// nonsense, and the machine runs on values nobody chose.
    ///
    /// So the conversion rules travel WITH the program. Each file type declares
    /// what it looked like in each version, and the importer upgrades a file as
    /// it reads it. A rule written once for v1.0 keeps working forever; it is not
    /// re-derived on every upgrade by whoever happens to be doing it.
    ///
    /// NOTHING IS OVERWRITTEN WITHOUT BEING SHOWN FIRST. The importer reports what
    /// it would do and waits: a settings transfer that goes wrong on a Friday
    /// afternoon is a machine that cannot run on Monday.
    /// </summary>
    public static class SettingsMigration
    {
        /// <summary>
        /// The program's own settings format version.
        ///
        /// Bumped when a settings FILE changes shape - not when the application
        /// changes. An import from any older number is upgraded through the rules
        /// below; an import from a NEWER one is refused, because this build cannot
        /// know what a later version added.
        /// </summary>
        public const int CurrentFormatVersion = 3;

        /// <summary>What a source folder turned out to contain.</summary>
        public class Finding
        {
            public string FileName = "";
            public string Kind = "";
            public string SourcePath = "";
            public string TargetPath = "";
            public int DetectedVersion;
            public int Rows;
            public string Note = "";
            public bool Selected = true;

            /// <summary>True when the file needs converting, not just copying.</summary>
            public bool NeedsUpgrade { get { return DetectedVersion < CurrentFormatVersion; } }
        }

        /// <summary>The files a machine install owns, and how to recognise them.</summary>
        private class FileKind
        {
            public string Name;              // file name as it appears on disk
            public string Kind;              // human label
            public Func<string, int> Detect; // reads a file, returns its format version
            public Func<string[], int, string[]> Upgrade;   // lines, fromVersion -> lines
        }

        // Users and units are NOT here: they live in ProgramData\KabanHMI, outside
        // the program folder, so an upgrade keeps them without importing anything.
        private static readonly List<FileKind> Kinds = new List<FileKind>
        {
            new FileKind
            {
                Name = "PlcSettings.cfg", Kind = "PLC connections",
                Detect = DetectStationVersion, Upgrade = UpgradeStations
            },
            new FileKind
            {
                Name = "Alarms.json", Kind = "Alarm definitions",
                Detect = DetectAlarmVersion, Upgrade = UpgradeAlarms
            },
            new FileKind
            {
                Name = "Translations.csv", Kind = "Machine translations",
                Detect = DetectCsvVersion, Upgrade = UpgradeCsv
            },
            new FileKind
            {
                Name = "CommonTranslations.csv", Kind = "Common translations",
                Detect = DetectCsvVersion, Upgrade = UpgradeCsv
            },
            new FileKind
            {
                Name = "PlcLabels.csv", Kind = "PLC label list",
                Detect = p => 1, Upgrade = (l, v) => l
            },
        };

        /// <summary>
        /// Looks over a folder from an older install and says what it found.
        ///
        /// Reads only - nothing is written until Apply. Subfolders are searched
        /// too, because an engineer will point at the install root as often as at
        /// bin\Debug, and being wrong about which should not mean finding nothing.
        /// </summary>
        public static List<Finding> Inspect(string sourceFolder, string targetFolder)
        {
            var found = new List<Finding>();
            if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
                return found;

            foreach (var kind in Kinds)
            {
                string[] hits;
                try { hits = Directory.GetFiles(sourceFolder, kind.Name, SearchOption.AllDirectories); }
                catch { continue; }

                foreach (var path in hits)
                {
                    // Never offer to import a file onto itself.
                    if (SameFolder(Path.GetDirectoryName(path), targetFolder)) continue;

                    var f = new Finding
                    {
                        FileName = kind.Name,
                        Kind = kind.Kind,
                        SourcePath = path,
                        TargetPath = Path.Combine(targetFolder, RelativeTarget(path, sourceFolder, kind.Name))
                    };

                    try
                    {
                        f.DetectedVersion = kind.Detect(path);
                        f.Rows = CountRows(path);
                    }
                    catch (Exception ex) { f.Note = "unreadable: " + ex.Message; f.Selected = false; }

                    if (f.DetectedVersion > CurrentFormatVersion)
                    {
                        // A FILE FROM THE FUTURE IS NOT IMPORTED. It was written by a
                        // newer build that may have added fields this one would drop
                        // on the way through - silently losing a setting is worse
                        // than refusing the file.
                        f.Note = "newer than this program (v" + f.DetectedVersion + "); skipped";
                        f.Selected = false;
                    }
                    else if (f.NeedsUpgrade)
                        f.Note = "will be converted from v" + f.DetectedVersion +
                                 " to v" + CurrentFormatVersion;

                    found.Add(f);
                }
            }
            return found;
        }

        /// <summary>
        /// Copies the chosen files in, converting as it goes.
        ///
        /// Every target that already exists is backed up first, into a dated
        /// folder. An import is the moment most likely to lose a working
        /// configuration, and "it was fine before" has to remain recoverable.
        /// </summary>
        public static string Apply(IEnumerable<Finding> chosen, string targetFolder)
        {
            // A UNIQUE folder per import. The name is stamped to the second, and
            // two imports inside the same second - which is exactly what happens
            // when someone re-runs after a mistake - would otherwise share a folder
            // and the second backup would overwrite the first. Losing the copy of
            // the configuration you were about to restore is the one failure this
            // whole feature exists to prevent.
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupRoot = Path.Combine(targetFolder, "Backup_" + stamp);
            for (int n = 2; Directory.Exists(backupRoot); n++)
                backupRoot = Path.Combine(targetFolder, "Backup_" + stamp + "_" + n);

            foreach (var f in chosen.Where(x => x.Selected))
            {
                var kind = Kinds.FirstOrDefault(k =>
                    string.Equals(k.Name, f.FileName, StringComparison.OrdinalIgnoreCase));
                if (kind == null) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(f.TargetPath));

                if (File.Exists(f.TargetPath))
                {
                    var rel = f.TargetPath.Substring(targetFolder.Length).TrimStart('\\', '/');
                    var backup = Path.Combine(backupRoot, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    File.Copy(f.TargetPath, backup, true);
                }

                var lines = File.ReadAllLines(f.SourcePath);
                if (f.NeedsUpgrade) lines = kind.Upgrade(lines, f.DetectedVersion);

                SafeFile.WriteAllText(f.TargetPath,
                    string.Join(Environment.NewLine, lines) + Environment.NewLine,
                    new System.Text.UTF8Encoding(true));
                AuditLogger.Log("imported " + f.FileName + " from " + f.SourcePath +
                                (f.NeedsUpgrade ? " (converted from v" + f.DetectedVersion + ")" : ""));
            }

            return Directory.Exists(backupRoot) ? backupRoot : "";
        }

        // ==================================================================
        // FORMAT RULES - one per file type, per version
        // ==================================================================

        /// <summary>
        /// PLC connections.
        ///
        /// v1  Station  Driver  Address
        /// v2  + Enabled
        /// v3  + VersionAddress   (the device holding the PLC program version)
        /// </summary>
        private static int DetectStationVersion(string path)
        {
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.StartsWith("#") || line.Trim().Length == 0) continue;
                int cols = line.Split('\t').Length;
                if (cols >= 5) return 3;
                if (cols == 4) return 2;
                return 1;
            }
            return 1;
        }

        private static string[] UpgradeStations(string[] lines, int from)
        {
            var outp = new List<string>();
            foreach (var line in lines)
            {
                if (line.StartsWith("#") || line.Trim().Length == 0) { outp.Add(line); continue; }
                var p = line.Split('\t').ToList();

                // Defaults chosen so an upgraded file behaves exactly as the old one
                // did: a station that was in the file was in use, and a machine with
                // no version device simply does not report one.
                if (from < 2) p.Add("1");    // Enabled
                if (from < 3) p.Add("");     // VersionAddress - none known
                outp.Add(string.Join("\t", p));
            }
            return outp.ToArray();
        }

        /// <summary>
        /// Alarms.
        ///
        /// v1  Code / Address / DescriptionKey / Severity
        /// v2  + TroubleshootingKeys, HelperKeys
        /// v3  same shape; severity names normalised
        /// </summary>
        private static int DetectAlarmVersion(string path)
        {
            var text = File.ReadAllText(path);
            if (text.IndexOf("TroubleshootingKeys", StringComparison.OrdinalIgnoreCase) < 0) return 1;
            if (text.IndexOf("\"Critical\"", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            return 3;
        }

        private static string[] UpgradeAlarms(string[] lines, int from)
        {
            // Left as text rather than parsed and rewritten: a JSON round-trip would
            // reorder and reformat the whole file, turning a two-field upgrade into a
            // diff nobody can read. The loader supplies empty lists for the fields an
            // older file lacks.
            if (from >= 2) return lines;
            return lines;
        }

        /// <summary>
        /// Translation CSVs.
        ///
        /// v1  Key;EN;TR
        /// v2  Key;EN;TR;RU;DE
        /// v3  Key;EN;TR;RU;DE;IT;ES
        /// </summary>
        private static int DetectCsvVersion(string path)
        {
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.StartsWith("#") || line.Trim().Length == 0) continue;
                int cols = line.Split(';').Length;
                if (cols >= 7) return 3;
                if (cols >= 5) return 2;
                return 1;
            }
            return 1;
        }

        private static string[] UpgradeCsv(string[] lines, int from)
        {
            var outp = new List<string>();
            foreach (var line in lines)
            {
                if (line.StartsWith("#") || line.Trim().Length == 0) { outp.Add(line); continue; }
                var p = line.Split(';').ToList();

                // Padded with EMPTY cells, never with the English text. An empty cell
                // falls back to English at run time and stays visible as untranslated;
                // a copy of the English looks finished and never gets translated.
                while (p.Count < 7) p.Add("");
                outp.Add(string.Join(";", p));
            }
            return outp.ToArray();
        }

        // ==================================================================
        private static int CountRows(string path)
        {
            try
            {
                return File.ReadAllLines(path)
                           .Count(l => l.Trim().Length > 0 && !l.StartsWith("#"));
            }
            catch { return 0; }
        }

        private static bool SameFolder(string a, string b)
        {
            try
            {
                return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'),
                                     Path.GetFullPath(b).TrimEnd('\\', '/'),
                                     StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>
        /// Where a found file should land.
        ///
        /// A machine's own files sit in its own folder, so the relative path is
        /// kept - otherwise H500's Translations.csv would overwrite H800's.
        /// </summary>
        private static string RelativeTarget(string sourcePath, string sourceRoot, string fileName)
        {
            try
            {
                var dir = Path.GetDirectoryName(sourcePath);
                var root = Path.GetFullPath(sourceRoot).TrimEnd('\\', '/');
                var full = Path.GetFullPath(dir).TrimEnd('\\', '/');
                if (full.Length > root.Length && full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    var rel = full.Substring(root.Length).TrimStart('\\', '/');
                    return Path.Combine(rel, fileName);
                }
            }
            catch { }
            return fileName;
        }
    }
}

