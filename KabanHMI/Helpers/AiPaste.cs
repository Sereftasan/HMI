using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// "ASK &lt;AI&gt;" PASTES THE BRIEFING ITSELF (14 October: "create more detailed document
    /// as the AI prompt and paste it to the AI page directly with the ask AI button - all AI
    /// pages support the Ctrl+V shortcut, you can even blindly press the shortcut").
    ///
    /// The chat opens in the browser; a few seconds later - the page has loaded and has put
    /// the cursor in its message box - Ctrl+V is pressed for the user: the briefing goes in
    /// (a file is attached, a text is typed), and with a file a short message after it that
    /// tells the AI to read it and start. Sending is left to the user: they see what goes
    /// out of the panel, and press Enter.
    ///
    /// Blind, as asked, but not reckless: nothing is pressed while the HMI itself is still
    /// in front (the browser did not come up, or the user came back to the panel) - the
    /// prompt is on the clipboard, and the message already said to paste it. Switched off
    /// in Preferences (Help: "Paste into the chat").
    /// </summary>
    public static class AiPaste
    {
        /// <summary>How long the chat page is given to open and load before Ctrl+V.</summary>
        public static TimeSpan Delay = TimeSpan.FromSeconds(6);
        /// <summary>Between the file and the message after it (the chat attaches the file first).</summary>
        public static TimeSpan Between = TimeSpan.FromSeconds(2);

        /// <summary>Pasting into the chat is on (the user's preference; on unless switched off).</summary>
        public static bool Enabled { get { return ViewPreferences.Get(ViewPreferences.AiPaste) != "Off"; } }

        /// <summary>True when another program's window is in front (the browser) - not the HMI's. Replaceable (tests).</summary>
        public static Func<bool> OtherWindowInFront = () =>
        {
            try
            {
                var h = GetForegroundWindow();
                if (h == IntPtr.Zero) return false;
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                return pid != 0 && pid != (uint)Process.GetCurrentProcess().Id;
            }
            catch { return false; }
        };

        /// <summary>Presses keys in the window in front. Replaceable (tests).</summary>
        public static Action<string> Press = keys => SendKeys.SendWait(keys);

        /// <summary>Puts the briefing on the clipboard as the user chose (a file, or its text). Replaceable (tests).</summary>
        public static Action<string, string> PutDocument = (file, text) =>
        {
            if (!string.IsNullOrEmpty(file))
            {
                var files = new StringCollection();
                files.Add(file);
                Clipboard.SetFileDropList(files);
            }
            else Clipboard.SetText(text ?? "");
        };

        /// <summary>Puts a text on the clipboard. Replaceable (tests).</summary>
        public static Action<string> PutText = text => Clipboard.SetText(text ?? "");

        /// <summary>What happened last (tests): "pasted", "pasted+message", "skipped: the HMI in front", "off".</summary>
        public static string LastResult { get; private set; }

        /// <summary>Raised when a paste ran (tests wait for it).</summary>
        public static event EventHandler Done;

        private static Timer _timer;

        /// <summary>
        /// After Delay, pastes the briefing into the chat in front - the file (a chat
        /// attaches it) or the text - and, with a file, <paramref name="message"/> after it.
        /// </summary>
        public static void Schedule(string file, string text, string message)
        {
            if (!Enabled) { LastResult = "off"; return; }
            Cancel();
            int step = 0;
            _timer = new Timer { Interval = Math.Max(1, (int)Delay.TotalMilliseconds) };
            _timer.Tick += (s, e) =>
            {
                try
                {
                    if (step == 0)
                    {
                        if (!OtherWindowInFront())
                        {
                            LastResult = "skipped: the HMI in front";
                            AuditLogger.Log("AI prompt not pasted into the chat: the HMI was still in front (the prompt stays on the clipboard).");
                            Finish();
                            return;
                        }
                        PutDocument(file, text);
                        Press("^v");
                        LastResult = "pasted";
                        bool more = !string.IsNullOrEmpty(file) && !string.IsNullOrWhiteSpace(message);
                        if (!more) { Finish(); return; }
                        step = 1;
                        _timer.Interval = Math.Max(1, (int)Between.TotalMilliseconds);
                        return;
                    }
                    // the message after the file - only into the same window still in front
                    if (OtherWindowInFront())
                    {
                        PutText(message);
                        Press("^v");
                        LastResult = "pasted+message";
                    }
                    // the briefing back on the clipboard: Ctrl+V again pastes it, not the message
                    try { PutDocument(file, text); } catch { }
                    Finish();
                }
                catch (Exception ex)
                {
                    LastResult = "failed: " + ex.Message;
                    AuditLogger.Log("AI prompt could not be pasted into the chat: " + ex.Message);
                    Finish();
                }
            };
            _timer.Start();
        }

        /// <summary>Stops a paste that has not happened yet.</summary>
        public static void Cancel()
        {
            if (_timer == null) return;
            _timer.Stop();
            _timer.Dispose();
            _timer = null;
        }

        private static void Finish()
        {
            Cancel();
            var h = Done;
            if (h != null) h(null, EventArgs.Empty);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
