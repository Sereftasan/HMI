using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// HMI Settings > AI (Admin): the panel's AI CHATS - the list the caption bar's
    /// "?" and every alarm's help list offer ("Ask &lt;AI&gt;"), as a table
    /// (AiChatsEditor).
    ///
    /// The page-designer prompt that was under it is gone (9 October: "it doesn't
    /// seem very useful"): a page is designed on the developer's computer, not on
    /// the machine's panel, and a fixed prompt could not show the program's real
    /// controls and pages. Its place is taken by a guide file kept with the source
    /// (docs/AI_Page_Design_Guide.md) - every control, its properties and the
    /// pages' conventions, made from the program itself - to attach to an AI chat.
    /// </summary>
    public class AiPromptControl : UserControl, IRebuildForLanguage
    {
        // its texts are put on when it is built: built again in another language (SettingsScreen)
        object IRebuildForLanguage.Place { get { return null; } }
        void IRebuildForLanguage.ReturnTo(object place) { }

        /// <summary>The panel's AI chats (tests).</summary>
        public AiChatsEditor Chats { get; private set; }

        public AiPromptControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            AutoScroll = true;
            Chats = new AiChatsEditor();
            Controls.Add(Chats);
        }
    }

    /// <summary>The AI chats as a tab of HMI Settings (Admin).</summary>
    public class AiPromptPage : ISettingsPage
    {
        public string TitleKey { get { return "Set_AiPrompt"; } }
        public string TitleFallback { get { return "AI"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Admin; } }
        public UserControl GetControl() { return new AiPromptControl(); }
    }
}
