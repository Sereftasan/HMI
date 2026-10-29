using System;
using System.Windows.Forms;
using Kaban.Core.Enums;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// The settings that belong to the HMI PROGRAM rather than to a machine -
    /// users and their preferences, logging, diagnostics, about. Grouped so the Settings entry can
    /// read "HMI Settings | Machine Settings | Screwing Settings ...", the same
    /// shape as Home and Alarms.
    /// </summary>
    public class HmiSettingsPage : ISettingsPage
    {
        public string TitleKey { get { return "Tab_HmiSettings"; } }
        public string TitleFallback { get { return "HMI Settings"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.None; } }

        public UserControl GetControl()
        {
            return new SettingsScreen(new ISettingsPage[]
            {
                // The accounts and how the screen shows things - units of measure
                // included - for everyone or per user (it was three tabs: Unit
                // Settings, User Settings and View Preferences).
                // 9 October, the order asked for: Users and Preferences, Logs and
                // Reports, PLC Connections, Config Import, AI.
                new UsersPreferencesPage(),
                new LogSettingsPage(),
                // One tab for the controllers: their connections and state, their data's
                // Read and Write (it was two; no tabs under it since 23 October).
                new StationConnectionPage(),
                new ImportSettingsPage(),
                new AiPromptPage(),
                // 21 October: every translation, edited on the panel, languages added and deleted (Maintenance)
                new TranslationsPage(),
                // 30 October: the HMI's optional parts on and off - Industry 4 logging, the touch screen (Admin)
                new FeaturesPage()
                // No "Manual" or "About" tab: both are the caption bar's "?" (9 October:
                // "it is not a setting thing at all and the user's eyes don't catch it there")
            });
        }
    }



    /// <summary>An ISettingsPage built from a caption and a factory.</summary>
    public class SimplePage : ISettingsPage
    {
        private readonly Func<UserControl> _factory;

        public SimplePage(string titleKey, string titleFallback, SecurityLevel level, Func<UserControl> factory)
        {
            TitleKey = titleKey;
            TitleFallback = titleFallback;
            RequiredLevel = level;
            _factory = factory;
        }

        public string TitleKey { get; private set; }
        public string TitleFallback { get; private set; }
        public SecurityLevel RequiredLevel { get; private set; }
        public UserControl GetControl() { return _factory(); }
    }
}

