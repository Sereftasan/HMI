using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// Body for a page that has no content of its own - a machine with no
    /// parameter pages (StandardScreens). About has its own control now.
    /// </summary>
    internal class PlaceholderControl : UserControl
    {
        public PlaceholderControl(string titleKey, string titleFallback, string descr)
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            // No heading: the (bold) tab above already names this page.
            var body = new Label
            {
                Text = descr,
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(6, 44)
            };
            Controls.Add(body);
        }
    }

    public class UserSettingsPage : ISettingsPage
    {
        public string TitleKey => "Set_Users";
        public string TitleFallback => "User Settings";
        public SecurityLevel RequiredLevel => SecurityLevel.Admin;   // account management is admin-only
        public UserControl GetControl() => new UserSettingsControl();
    }

    /// <summary>
    /// Carrying a machine's settings onto an upgraded build.
    ///
    /// ADMIN ONLY: it overwrites station addresses, users and alarm definitions in
    /// one action, which is the whole point and also why it is not a page an
    /// operator should reach.
    /// </summary>
    public class ImportSettingsPage : ISettingsPage
    {
        public string TitleKey => "Set_Import";
        public string TitleFallback => "Import Settings";
        public SecurityLevel RequiredLevel => SecurityLevel.Admin;
        public UserControl GetControl() => new ImportSettingsControl();
    }

    public class LogSettingsPage : ISettingsPage
    {
        public string TitleKey => "Set_Logs";
        public string TitleFallback => "Logs and Reports";
        public SecurityLevel RequiredLevel => SecurityLevel.Maintenance;
        public UserControl GetControl() => new LogSettingsControl();
    }



    public class AboutPage : ISettingsPage
    {
        public string TitleKey => "Set_About";
        public string TitleFallback => "About";
        public SecurityLevel RequiredLevel => SecurityLevel.None;
        // Program, version, machines - and the button that opens the HMI manual,
        // which replaced the plain-text "Manual" tab (see AboutControl).
        public UserControl GetControl() => new AboutControl();
    }
}

