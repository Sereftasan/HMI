using System;
using System.IO;
using System.Text;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// Writes a settings file so that a power cut can never leave it half-written.
    ///
    /// WHY: File.WriteAllText truncates the file first and then writes it. A
    /// panel PC switched off at the main isolator in that moment - which on a
    /// machine is an ordinary way to stop work - kept an empty or cut-off
    /// Users.dat, PlcConnections.cfg or Alarms.json, and the next start came up
    /// with no users, no PLC addresses or no alarms.
    ///
    /// Here the new content goes to "name.tmp" first, is flushed to the disk, and
    /// only then swapped over the real file in one step. Either the old file or
    /// the new one survives, never a mixture.
    /// </summary>
    public static class SafeFile
    {
        public static void WriteAllText(string path, string contents)
        {
            WriteAllText(path, contents, new UTF8Encoding(true));
        }

        public static void WriteAllText(string path, string contents, Encoding encoding)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string tmp = full + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var w = new StreamWriter(fs, encoding ?? new UTF8Encoding(true)))
            {
                w.Write(contents ?? "");
                w.Flush();
                fs.Flush(true);          // through the OS cache onto the disk
            }

            try
            {
                if (File.Exists(full)) File.Replace(tmp, full, null, true);
                else File.Move(tmp, full);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is PlatformNotSupportedException)
            {
                // Some volumes (network shares, FAT USB sticks) do not support the
                // atomic swap. Fall back to a plain copy - no worse than before.
                File.Copy(tmp, full, true);
                try { File.Delete(tmp); } catch { }
            }
        }
    }
}
