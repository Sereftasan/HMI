using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// HMI Settings > About: what this program is, which machines it is running,
    /// and the button that opens the HMI MANUAL.
    ///
    /// THE MANUAL IS AN HTML FILE, Help\Kaban_HMI_Manual.&lt;language&gt;.html, opened
    /// in the browser. It used to be a plain-text tab of its own ("Manual"),
    /// generated from strings in the code: no pictures, no tables, and every
    /// change to it meant a new build. An HTML file can carry drawings, photos
    /// and charts, can be edited and translated by someone who does not build
    /// the program, and is picked in the language on screen like every other
    /// help document (TroubleshootingCatalog.ResolveLanguageFile).
    /// </summary>
    public class AboutControl : UserControl
    {
        /// <summary>The manual's name as help documents are named: the language is added when found.</summary>
        public const string ManualFile = "Kaban_HMI_Manual.html";

        private readonly Label _status = new Label();

        public AboutControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Padding = new Padding(6, 10, 6, 6);

            var title = new Label
            {
                Text = "Kaban Machines HMI",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(6, 12)
            };
            Controls.Add(title);

            var info = new Label
            {
                Text = DescribeProgram(),
                Font = UiStyle.Body,
                ForeColor = Color.FromArgb(70, 76, 86),
                AutoSize = true,
                Location = new Point(8, 46)
            };
            Controls.Add(info);

            var open = new Button
            {
                Text = "📖  " + LanguageManager.GetString("Btn_OpenManual", "Open the HMI manual"),
                Font = new Font("Segoe UI", 10.5F),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 4, 10, 4),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(245, 246, 248),
                Cursor = Cursors.Hand
            };
            open.FlatAppearance.BorderColor = UiStyle.BorderColor;
            UiStyle.Round(open);
            open.Click += (s, e) => OpenManual();
            Controls.Add(open);

            _status.AutoSize = true;
            _status.Font = UiStyle.Small;
            _status.ForeColor = Color.FromArgb(160, 40, 40);
            Controls.Add(_status);

            // Below the text, whatever its height in this language.
            Layout += (s, e) =>
            {
                open.Location = new Point(8, info.Bottom + 18);
                _status.Location = new Point(10, open.Bottom + 8);
            };

            UiStyle.Tips().SetToolTip(open, LanguageManager.GetString("Tip_OpenManual",
                "Opens the operator and engineer manual in the browser, in the language on screen."));
        }

        /// <summary>The program, its version and the machines it is running.</summary>
        private static string DescribeProgram()
        {
            var sb = new System.Text.StringBuilder();
            var exe = Assembly.GetEntryAssembly() ?? typeof(AboutControl).Assembly;
            sb.AppendLine(LanguageManager.GetString("About_Program", "Program") + ": " +
                          Path.GetFileNameWithoutExtension(exe.Location) + "  " + exe.GetName().Version);
            sb.AppendLine(LanguageManager.GetString("About_Library", "HMI library") + ": " +
                          typeof(AboutControl).Assembly.GetName().Version);

            string machines = "";
            try
            {
                foreach (var m in MachineRegistry.Modules)
                {
                    if (machines.Length > 0) machines += ", ";
                    machines += Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(m) +
                                " (" + m.Context.ModuleId + ")";
                }
            }
            catch { }
            if (machines.Length > 0)
                sb.AppendLine(LanguageManager.GetString("About_Machines", "Machines") + ": " + machines);
            return sb.ToString().TrimEnd();
        }

        /// <summary>Full path of the manual in the language on screen (English, then the plain name, as fallbacks).</summary>
        public static string ManualPath()
        {
            return TroubleshootingCatalog.ResolveLanguageFile(ManualFile);
        }

        private void OpenManual()
        {
            string path = ManualPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _status.Text = LanguageManager.GetString("Msg_ManualMissing", "The manual was not found:") + " " + path;
                return;
            }
            try
            {
                AuditLogger.Log("opened the HMI manual.");
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                _status.Text = "";
            }
            catch (Exception ex)
            {
                _status.Text = LanguageManager.GetString("Msg_ManualOpenFailed", "Could not open the manual:") + " " + ex.Message;
            }
        }
    }
}
