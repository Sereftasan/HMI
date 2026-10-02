using System;
using System.Windows.Forms;
using Kaban.Core.Enums;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// The settings that belong to the HMI PROGRAM rather than to a machine -
    /// units, users, logging, diagnostics, about. Grouped so the Settings entry can
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
                new UnitSettingsPage(),
                new UserSettingsPage(),
                // How the screen shows things - for everyone or per user.
                new ViewPreferencesPage(),
                new ImportSettingsPage(),
                new LogSettingsPage(),
                // One tab for the controllers: connections, their state, the
                // connection events and the MX Component tools (it was two).
                new StationConnectionPage(),
                new AiPromptPage(),
                // No "Manual" tab: the manual is an HTML file opened from About.
                new AboutPage()
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

