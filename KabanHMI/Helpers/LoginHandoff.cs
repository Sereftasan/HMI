using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Kaban.Core.Helpers;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// Keeps a user signed in across ONE restart - every restart the program
    /// makes: from the close menu, after a setting, for the magnifying factor
    /// (MainForm.RestartApplication). Without it they would see the program
    /// restart and have to sign in again.
    ///
    /// What makes it safe to hand over a sign-in at all:
    ///   * the note names the user and nothing else - no password, not even a
    ///     hash; the level comes from the account as it is at the new start;
    ///   * it is encrypted for THIS Windows account on THIS PC (DPAPI): another
    ///     account, another PC, or a copy of the file cannot read it;
    ///   * it is valid for two minutes, once: read, it is deleted - and a note
    ///     that does not match the number the old program passed on its command
    ///     line is refused;
    ///   * every use is written to the audit log.
    /// </summary>
    public static class LoginHandoff
    {
        /// <summary>Command-line switch carrying the note's number to the restarted program.</summary>
        public const string Switch = "--handoff";

        /// <summary>How long a note is good for.</summary>
        public static TimeSpan Lifetime { get { return _lifetime; } internal set { _lifetime = value; } }
        // Two minutes: the old program may take a few seconds to let its
        // controllers go, and the new one waits for it before it starts.
        private static TimeSpan _lifetime = TimeSpan.FromSeconds(120);

        /// <summary>Where notes are kept (the Windows user's temp folder). Replaceable for tests.</summary>
        internal static string Folder = Path.Combine(Path.GetTempPath(), "KabanHMI_Handoff");

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Kaban HMI sign-in hand-over");

        private static string PathFor(string nonce) { return System.IO.Path.Combine(Folder, "handoff_" + nonce + ".bin"); }

        /// <summary>Writes the note for this user; returns its number for the command line.</summary>
        public static string Write(string user)
        {
            if (string.IsNullOrWhiteSpace(user)) throw new ArgumentException("user");
            var bytes = new byte[16];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(bytes);
            string nonce = string.Concat(bytes.Select(b => b.ToString("x2")));
            string body = user.Trim() + "\n" + DateTime.UtcNow.Add(Lifetime).Ticks + "\n" + nonce;
            var sealedNote = ProtectedData.Protect(Encoding.UTF8.GetBytes(body), Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Folder);
            // Notes a failed restart left behind are of no use to anyone.
            try
            {
                foreach (var old in Directory.GetFiles(Folder, "handoff_*.bin"))
                    if (File.GetLastWriteTimeUtc(old) < DateTime.UtcNow.AddMinutes(-10)) File.Delete(old);
            }
            catch { }
            File.WriteAllBytes(PathFor(nonce), sealedNote);
            return nonce;
        }

        /// <summary>
        /// The user a restart was made for, when this program was started with a
        /// valid note; the note is deleted whatever it held.
        /// </summary>
        public static bool TryConsume(string[] args, out string user)
        {
            user = null;
            if (args == null) return false;
            string nonce = null;
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], Switch, StringComparison.OrdinalIgnoreCase)) nonce = args[i + 1];
            if (string.IsNullOrEmpty(nonce) || nonce.Any(ch => !Uri.IsHexDigit(ch))) return false;

            string path = PathFor(nonce);
            byte[] sealedNote;
            try
            {
                if (!File.Exists(path)) { AuditLogger.Log("[RESTART] sign-in hand-over: no note - sign in again."); return false; }
                sealedNote = File.ReadAllBytes(path);
            }
            catch (Exception ex) { AuditLogger.Log("[RESTART] sign-in hand-over unreadable: " + ex.Message); return false; }
            finally { try { File.Delete(path); } catch { } }

            try
            {
                var lines = Encoding.UTF8.GetString(ProtectedData.Unprotect(sealedNote, Entropy, DataProtectionScope.CurrentUser)).Split('\n');
                long ticks;
                if (lines.Length != 3 || lines[2] != nonce || !long.TryParse(lines[1], out ticks)) return false;
                if (DateTime.UtcNow.Ticks > ticks) { AuditLogger.Log("[RESTART] sign-in hand-over expired - sign in again."); return false; }
                user = lines[0];
                return user.Length > 0;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("[RESTART] sign-in hand-over refused: " + ex.Message);
                return false;
            }
        }
    }
}
