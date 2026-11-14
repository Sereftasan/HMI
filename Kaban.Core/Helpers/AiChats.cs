using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// THE AI CHATS THE HMI OPENS (9 October: "is it possible and sensible if we
    /// enter the AI links somewhere in the application and add 'Preferred AI' to
    /// the Preferences of each user? Then 'Ask AI for help' opens an AI
    /// automatically, as well as 'Create AI prompt'").
    ///
    /// A list of chats - a name and its web address - kept for the panel (an
    /// Admin edits it). Every chat of it is offered, in its order: "Ask Claude",
    /// "Ask ChatGPT" ... (12 October: no user's choice among them any more - every
    /// user picked from all of them anyway). "Ask &lt;AI&gt;" puts the prompt on the
    /// clipboard and in a file and opens that chat in the browser, to paste it into: a prompt of a hundred thousand characters does
    /// not travel in a web address (the chats' "?q=" links take a few thousand at
    /// most), so it is never sent that way.
    ///
    /// Kept in AiChats.cfg beside the other HMI settings, a chat a line:
    ///   &lt;name&gt;;&lt;link&gt;          offered
    ///   &lt;name&gt;;&lt;link&gt;;off      kept, not offered (16 October: an "Active" column, so a
    ///                         chat can leave the help lists without losing its address)
    /// No file: the list below.
    /// </summary>
    public static class AiChats
    {
        public sealed class Chat
        {
            public string Name = "";
            public string Link = "";
            /// <summary>Offered in the help lists ("Ask &lt;name&gt;"); not active: kept in the table only.</summary>
            public bool Active = true;
            public Chat() { }
            public Chat(string name, string link, bool active = true) { Name = name ?? ""; Link = link ?? ""; Active = active; }
            public override string ToString() { return Name; }
        }

        /// <summary>The list a panel starts with (and goes back to with "the usual list").</summary>
        public static IList<Chat> Defaults
        {
            get
            {
                return new List<Chat>
                {
                    new Chat("Claude", "https://claude.ai/new"),
                    new Chat("ChatGPT", "https://chatgpt.com/"),
                    new Chat("Gemini", "https://gemini.google.com/app"),
                    new Chat("Microsoft Copilot", "https://copilot.microsoft.com/"),
                    new Chat("Grok", "https://grok.com/"),
                    new Chat("DeepSeek", "https://chat.deepseek.com/"),
                    new Chat("Perplexity", "https://www.perplexity.ai/")
                };
            }
        }

        /// <summary>Where the list is kept. Replaceable so a test can use its own file.</summary>
        public static string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "AiChats.cfg");

        /// <summary>Raised when the list changed.</summary>
        public static event EventHandler Changed;

        /// <summary>The chats the help lists offer: the active ones, in the list's order.</summary>
        public static IList<Chat> Offered
        {
            get { return All.Where(c => c.Active).ToList(); }
        }

        /// <summary>The panel's chats, in its order, active or not (a copy).</summary>
        public static IList<Chat> All
        {
            get
            {
                try
                {
                    if (!File.Exists(SettingsPath)) return Defaults;
                    var list = new List<Chat>();
                    foreach (var raw in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#", System.StringComparison.Ordinal)) continue;
                        int cut = line.IndexOf(';');
                        if (cut <= 0) continue;
                        // ";off" at the end: kept, not offered
                        bool active = true;
                        var rest = line.Substring(cut + 1).Trim();
                        var flag = System.Text.RegularExpressions.Regex.Match(rest, @";\s*(off|on)\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (flag.Success)
                        {
                            active = !flag.Groups[1].Value.Equals("off", StringComparison.OrdinalIgnoreCase);
                            rest = rest.Substring(0, flag.Index).Trim();
                        }
                        var chat = new Chat(line.Substring(0, cut).Trim(), rest, active);
                        if (chat.Name.Length > 0 && IsLink(chat.Link) && !list.Any(c => Same(c.Name, chat.Name))) list.Add(chat);
                    }
                    return list;
                }
                catch (Exception ex)
                {
                    AuditLogger.Log("could not read the AI chats (" + SettingsPath + "): " + ex.Message);
                    return Defaults;
                }
            }
        }

        /// <summary>Keeps the panel's list (names without ';', web links only; the same name once).</summary>
        public static void Save(IEnumerable<Chat> chats)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Kaban HMI - the AI chats 'Ask AI for help' opens: <name>;<link>, in the order they are offered; <name>;<link>;off is kept but not offered.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in chats ?? new Chat[0])
            {
                if (c == null) continue;
                var name = (c.Name ?? "").Replace(";", ",").Replace("\r", " ").Replace("\n", " ").Trim();
                var link = (c.Link ?? "").Trim();
                if (name.Length == 0 || !IsLink(link) || !seen.Add(name)) continue;
                sb.AppendLine(name + ";" + link + (c.Active ? "" : ";off"));
            }
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, sb.ToString(), new UTF8Encoding(false));
            AuditLogger.Log("the AI chats were changed (" + seen.Count + ").");
            var h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }

        /// <summary>A web address the browser opens: http or https only (nothing run on the panel).</summary>
        public static bool IsLink(string link)
        {
            Uri u;
            return !string.IsNullOrWhiteSpace(link) && Uri.TryCreate(link.Trim(), UriKind.Absolute, out u) &&
                   (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);
        }

        private static bool Same(string a, string b) { return string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase); }

        /// <summary>The chat of that name, or null.</summary>
        public static Chat Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return All.FirstOrDefault(c => Same(c.Name, name));
        }

        /// <summary>Opens a web address in the browser. Replaceable so a test sees what would open.</summary>
        public static Func<string, bool> OpenLink = link =>
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = link, UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not open " + link + ": " + ex.Message);
                return false;
            }
        };

        /// <summary>Opens a chat in the browser; false when there is no link or the browser did not open.</summary>
        public static bool Open(Chat chat)
        {
            if (chat == null || !IsLink(chat.Link)) return false;
            AuditLogger.Log("opened the AI chat " + chat.Name + " (" + chat.Link + ").");
            return OpenLink(chat.Link.Trim());
        }
    }
}
