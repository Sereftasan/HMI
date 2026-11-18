using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Kaban.Core.Helpers;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// REMOTE SUPPORT (the old main form's menu: TeamViewer - Supports\KabanSupport.exe - and AnyDesk - Supports\AnyDesk.exe):
    /// the support program started from the caption bar's "?", for Kaban's service to connect to the panel. Looked for in the
    /// program's Supports folder (as the old program shipped them), then where Windows installs them.
    /// </summary>
    public static class RemoteSupport
    {
        public enum Tool { TeamViewer, AnyDesk }

        /// <summary>Replaceable (tests): starts a program; false - it could not be started.</summary>
        public static Func<string, bool> Start = path =>
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path) ?? "" });
            return true;
        };

        /// <summary>The program's own folder (tests replace it).</summary>
        public static string BaseFolder = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>Where each one is looked for, in this order.</summary>
        public static IEnumerable<string> Candidates(Tool t)
        {
            string supports = Path.Combine(BaseFolder ?? "", "Supports");
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (t == Tool.TeamViewer)
            {
                yield return Path.Combine(supports, "KabanSupport.exe");
                yield return Path.Combine(supports, "TeamViewerQS.exe");
                if (pf.Length > 0) yield return Path.Combine(pf, "TeamViewer", "TeamViewer.exe");
                if (pf86.Length > 0) yield return Path.Combine(pf86, "TeamViewer", "TeamViewer.exe");
            }
            else
            {
                yield return Path.Combine(supports, "AnyDesk.exe");
                if (pf86.Length > 0) yield return Path.Combine(pf86, "AnyDesk", "AnyDesk.exe");
                if (pf.Length > 0) yield return Path.Combine(pf, "AnyDesk", "AnyDesk.exe");
            }
        }

        /// <summary>The program found, or null.</summary>
        public static string Find(Tool t)
        {
            return Candidates(t).FirstOrDefault(p => { try { return File.Exists(p); } catch { return false; } });
        }

        /// <summary>Starts it; "" started, otherwise what to tell the user.</summary>
        public static string Open(Tool t)
        {
            var path = Find(t);
            string name = t == Tool.TeamViewer ? "TeamViewer" : "AnyDesk";
            if (path == null)
            {
                AuditLogger.Log("[SUPPORT] " + name + " not found (" + string.Join(", ", Candidates(t)) + ").");
                return string.Format(LanguageManager.GetString("Sup_NotFound",
                    "{0} is not on this panel. Put {1} into the Supports folder beside the program ({2}) - or install {0}."),
                    name, t == Tool.TeamViewer ? "KabanSupport.exe" : "AnyDesk.exe", Path.Combine(BaseFolder ?? "", "Supports"));
            }
            try
            {
                if (!Start(path)) return string.Format(LanguageManager.GetString("Sup_NotStarted", "{0} could not be started."), path);
                AuditLogger.Log("[SUPPORT] " + name + " started: " + path);
                return "";
            }
            catch (Exception ex)
            {
                AuditLogger.Log("[SUPPORT] " + path + ": " + ex.Message);
                return path + ": " + ex.Message;
            }
        }
    }
}
