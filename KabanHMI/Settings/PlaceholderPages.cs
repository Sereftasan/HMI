using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// Body for a settings page that has no content of its own yet. Only About
    /// uses it now - User and Log Settings have real controls, and the Machine
    /// Parameter Records and Barcode placeholders were removed because nothing
    /// ever navigated to them.
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
        public string TitleFallback => "Log Settings";
        public SecurityLevel RequiredLevel => SecurityLevel.Maintenance;
        public UserControl GetControl() => new LogSettingsControl();
    }



    public class AboutPage : ISettingsPage
    {
        public string TitleKey => "Set_About";
        public string TitleFallback => "About";
        public SecurityLevel RequiredLevel => SecurityLevel.None;
        public UserControl GetControl() =>
            new PlaceholderControl(TitleKey, TitleFallback,
                "Kaban Machines HMI\nVersion information and support contact here.");
    }
}

