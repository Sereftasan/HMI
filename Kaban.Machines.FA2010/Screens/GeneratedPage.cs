using System;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.UI.Settings;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// Adapts a generated screen to ISettingsPage, so Manual Movement and Machine
    /// Parameters use the SAME horizontal tab strip as the Settings page instead of
    /// a second, different-looking menu. One tab implementation for the whole
    /// program keeps every page consistent.
    ///
    /// The factory is called only when the tab is first opened, so pages the
    /// operator never visits cost nothing - important here, because each generated
    /// page brings dozens of PLC-bound controls into the scan batch.
    /// </summary>
    public class GeneratedPage : ISettingsPage
    {
        private readonly Func<UserControl> _factory;

        public GeneratedPage(string titleKey, string titleFallback,
                             SecurityLevel requiredLevel, Func<UserControl> factory)
        {
            TitleKey = titleKey;
            TitleFallback = titleFallback;
            RequiredLevel = requiredLevel;
            _factory = factory;
        }

        public string TitleKey { get; private set; }
        public string TitleFallback { get; private set; }
        public SecurityLevel RequiredLevel { get; private set; }

        public UserControl GetControl() { return _factory(); }
    }
}

