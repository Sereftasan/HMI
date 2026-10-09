using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// THE AI ENTRIES OF A HELP LIST (9 October) - an alarm's help list (the Alarms
    /// page's Help column, a unit's "?") and the caption bar's "?":
    ///   Create AI prompt          - on the clipboard and in a file, to paste anywhere
    ///   Ask Claude                - the same, and the user's preferred AI chat opened
    ///   Ask another AI  >         - the panel's other chats
    /// The prompt is made when an entry is clicked (the first alarm prompt looks
    /// through every page: the pointer waits).
    /// </summary>
    public static class AiMenu
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>"Create AI prompt".</summary>
        public static string CreateCaption { get { return T("Hlp_AiPromptItem", "Create AI prompt"); } }

        /// <summary>"Ask Claude" - the user's chat (9 October: "just 'Ask Claude', the rest is too much").</summary>
        public static string AskCaption(AiChats.Chat chat)
        {
            return string.Format(T("Hlp_AiAskItem", "Ask {0}"), chat != null ? chat.Name : "AI");
        }

        /// <summary>The entries, to add to a menu after its documents.</summary>
        public static List<ToolStripItem> Items(Func<string> prompt, AlarmAiPrompt.Subject subject, IWin32Window owner)
        {
            var items = new List<ToolStripItem>();
            if (prompt == null) return items;
            bool alarm = subject == AlarmAiPrompt.Subject.Alarm;
            var create = new ToolStripMenuItem("📋  " + CreateCaption) { Name = "aiPrompt" };
            create.ToolTipText = alarm
                ? T("Tip_AiPrompt", "Copies a prompt for an AI chat (Claude, ChatGPT, Gemini, Grok...) with everything about this alarm, its unit and the machine, and the HMI's step-by-step page as the format: paste it there, the AI writes you helper pages for each level and answers your questions.")
                : T("Tip_HmiPrompt", "Copies a prompt for an AI chat with the HMI, its machines, where you are and its whole manual: paste it there and ask it anything about the HMI.");
            create.Click += (s, e) => Run(prompt, subject, owner, null);
            items.Add(create);

            var chats = AiChats.All;
            var mine = AiChats.Preferred;
            var ask = new ToolStripMenuItem("🤖  " + AskCaption(mine)) { Name = "aiAsk", Enabled = mine != null };
            ask.ToolTipText = T("Tip_AiAsk", "Copies the prompt and opens your AI chat in the browser: paste it there (Ctrl+V). Your AI: Users and Preferences > Help and AI.");
            // the other chats, each its own "Ask ..." under "Ask another AI"
            ask.Click += (s, e) => Run(prompt, subject, owner, AiChats.Preferred);
            items.Add(ask);
            var others = chats.Where(c => mine == null || !string.Equals(c.Name, mine.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (others.Count > 0)
            {
                var other = new ToolStripMenuItem(T("Hlp_AiAskOther", "Ask another AI")) { Name = "aiAskOther" };
                foreach (var c in others)
                {
                    var chat = c;
                    var one = new ToolStripMenuItem(chat.Name) { ToolTipText = chat.Link, Name = "aiAsk:" + chat.Name };
                    one.Click += (s, e) => Run(prompt, subject, owner, chat);
                    other.DropDownItems.Add(one);
                }
                items.Add(other);
            }
            return items;
        }

        /// <summary>The prompt made (the pointer waits), then copied - and the chat opened when one is given.</summary>
        public static bool Run(Func<string> prompt, AlarmAiPrompt.Subject subject, IWin32Window owner, AiChats.Chat chat)
        {
            string text = null;
            var was = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try { text = prompt(); }
            catch (Exception ex) { AuditLogger.Log("AI prompt failed: " + ex.Message); }
            finally { Cursor.Current = was; }
            if (string.IsNullOrEmpty(text)) return false;
            return AlarmAiPrompt.Deliver(owner, text, subject, chat);
        }
    }
}
