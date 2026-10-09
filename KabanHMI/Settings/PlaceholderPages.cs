using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// Body for a page that has no content of its own - a machine with no
    /// parameter pages (StandardScreens). About is the caption bar's "?" (HmiManual.AboutText in a message box).
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
                Font = Kaban.UI.Controls.UiStyle.Body,
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(6, 44)
            };
            Controls.Add(body);
        }
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



}

