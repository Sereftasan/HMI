using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// THE AI ENTRIES OF A HELP LIST (9 October) - an alarm's help list (the Alarms
    /// page's Help column, a unit's "?") and the caption bar's "?":
    ///   Create AI prompt          - on the clipboard and in a file, to paste anywhere
    ///   Ask Claude                - the same, and that AI chat opened
    ///   Ask ChatGPT ...           - every chat of the panel's list, in its order
    /// (12 October: "we'd better remove user selection preference and list all the
    /// AIs as on the list directly, rather than with 'Ask another AI'"). Pictures in
    /// the menu's picture column, as the user menu's (MenuGlyphs). The prompt is made
    /// when an entry is clicked (the first alarm prompt looks through every page:
    /// the pointer waits).
    /// </summary>
    public static class AiMenu
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>"Create AI prompt".</summary>
        public static string CreateCaption { get { return T("Hlp_AiPromptItem", "Create AI prompt"); } }

        /// <summary>"Ask Claude" (9 October: "just 'Ask Claude', the rest is too much").</summary>
        public static string AskCaption(AiChats.Chat chat)
        {
            return string.Format(T("Hlp_AiAskItem", "Ask {0}"), chat != null ? chat.Name : "AI");
        }

        /// <summary>The name of a chat's entry ("aiAsk:Claude").</summary>
        public static string AskName(AiChats.Chat chat) { return "aiAsk:" + (chat != null ? chat.Name : ""); }

        /// <summary>The entries, to add to a menu after its documents.</summary>
        public static List<ToolStripItem> Items(Func<string> prompt, AlarmAiPrompt.Subject subject, IWin32Window owner)
        {
            var items = new List<ToolStripItem>();
            if (prompt == null) return items;
            bool alarm = subject == AlarmAiPrompt.Subject.Alarm;
            var create = new ToolStripMenuItem(CreateCaption, MenuGlyphs.Clipboard()) { Name = "aiPrompt" };
            create.ToolTipText = subject == AlarmAiPrompt.Subject.Translation
                ? T("Tip_TrnAiPrompt", "Copies a briefing for an AI chat with the translation files of the rows shown and where each text is shown: paste it there - the AI gives the files back, to import with 📥.")
                : alarm
                ? T("Tip_AiPrompt", "Copies a briefing for an AI chat (Claude, ChatGPT, Gemini, Grok...) with everything about this alarm, its unit, its devices and the machine, and the HMI's own troubleshooting for it: paste it there - the AI starts with the likely cause and the first check, and walks you through it step by step. It writes printable helper pages when you ask.")
                : T("Tip_HmiPrompt", "Copies a briefing for an AI chat with the HMI, its machines, where you are and what the page shows, and its whole manual: paste it there and ask it anything about the HMI.");
            create.Click += (s, e) => Run(prompt, subject, owner, null);
            items.Add(create);

            // every ACTIVE chat of the panel's list, in its order (HMI Settings > AI - 16 October:
            // a chat not active stays in the table, out of these lists)
            foreach (var c in AiChats.Offered)
            {
                var chat = c;
                var ask = new ToolStripMenuItem(AskCaption(chat), MenuGlyphs.Chat()) { Name = AskName(chat) };
                ask.ToolTipText = string.Format(T("Tip_AiAsk", "Opens {0} in the browser and pastes the briefing into it by itself (Ctrl+V) - you check it and send it."), chat.Name);
                ask.Tag = chat;
                ask.Click += (s, e) => Run(prompt, subject, owner, chat);
                items.Add(ask);
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
