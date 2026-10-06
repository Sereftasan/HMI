using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
                Font = Kaban.UI.Controls.UiStyle.Title,
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
                Font = Kaban.UI.Controls.UiStyle.Body,
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

            Kaban.UI.Controls.UiStyle.SetTip(UiStyle.Tips(), open, LanguageManager.GetString("Tip_OpenManual",
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

        // ==================================================================
        // THE MANUAL FOR THIS PROGRAM
        // ------------------------------------------------------------------
        // The manual file names no machine: one file serves FA2010, MD3020 and
        // the rest, so it can be written and translated once. Opened from here,
        // it is filled in with the machines THIS program runs - the main machine
        // and the sub-machines mounted with it - and nothing about any other:
        // their names, their controllers, their own pages, and each machine's
        // own chapter (Help\Manual_<ModuleId>.<language>.html) when it has one.
        // The result is written to the temp folder and opened from there.
        // ==================================================================

        /// <summary>One machine as the manual describes it.</summary>
        public sealed class ManualMachine
        {
            public string Id = "", Name = "";
            public int Station;
            public bool IsMain;
            public List<string> Pages = new List<string>();
            /// <summary>The machine's own chapter: the inside of its chapter file's body ("" when none).</summary>
            public string Chapter = "";

            public string Label
            {
                get
                {
                    return string.IsNullOrEmpty(Name) || string.Equals(Name, Id, StringComparison.OrdinalIgnoreCase)
                        ? Id : Id + " \u2013 " + Name;
                }
            }
        }

        /// <summary>The machines this program runs, main machine first.</summary>
        public static List<ManualMachine> RunningMachines()
        {
            var list = new List<ManualMachine>();
            foreach (var m in MachineRegistry.Modules.OrderByDescending(x => x.IsMain))
            {
                var mm = new ManualMachine
                {
                    Id = m.Context.ModuleId,
                    Name = Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(m),
                    Station = m.Context.Station(1),
                    IsMain = m.IsMain
                };
                foreach (var sc in m.Screens.OrderBy(x => x.SortOrder))
                {
                    var t = LanguageManager.GetString(sc.TitleKey ?? "", sc.TitleFallback ?? "");
                    if (!string.IsNullOrWhiteSpace(t) && !mm.Pages.Contains(t)) mm.Pages.Add(t);
                }
                mm.Chapter = ChapterBody(TroubleshootingCatalog.ResolveLanguageFile("Manual_" + mm.Id + ".html"));
                list.Add(mm);
            }
            return list;
        }

        /// <summary>A chapter file's body: what goes into chapter 13 ("" when there is no file).</summary>
        public static string ChapterBody(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
                // Comments out first: a note that MENTIONS the body tag is not it.
                var html = Regex.Replace(File.ReadAllText(path), "<!--.*?-->", "", RegexOptions.Singleline);
                var m = Regex.Match(html, "<body[^>]*>(.*)</body>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                return (m.Success ? m.Groups[1].Value : html).Trim();
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not read manual chapter " + path + ": " + ex.Message);
                return "";
            }
        }

        private static string Enc(string s) { return WebUtility.HtmlEncode(s ?? ""); }

        /// <summary>Replaces the text between &lt;!--name--&gt; and &lt;!--/name--&gt;, markers kept.</summary>
        private static string Fill(string html, string name, string content)
        {
            string open = "<!--" + name + "-->", close = "<!--/" + name + "-->";
            // In the BODY only: a note in the head describing the markers must
            // never be taken for them.
            int body = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
            int a = html.IndexOf(open, Math.Max(0, body), StringComparison.Ordinal);
            int b = a < 0 ? -1 : html.IndexOf(close, a, StringComparison.Ordinal);
            if (a < 0 || b < 0) return html;
            return html.Substring(0, a + open.Length) + content + html.Substring(b);
        }

        /// <summary>
        /// The manual with the given machines filled in. Pure - it reads no files
        /// and no registry - so a test can compose it for any machine set.
        /// </summary>
        public static string ComposeManual(string template, IList<ManualMachine> machines)
        {
            if (string.IsNullOrEmpty(template) || machines == null || machines.Count == 0) return template;
            var main = machines.FirstOrDefault(m => m.IsMain) ?? machines[0];
            var subs = machines.Where(m => m != main).ToList();

            string line = "For <b>" + Enc(main.Label) + "</b>";
            if (subs.Count > 0)
                line += ", with " + (subs.Count == 1 ? "the sub-machine " : "the sub-machines ") +
                        string.Join(" and ", subs.Select(m => "<b>" + Enc(m.Label) + "</b>")) + " mounted with it";
            var html = Fill(template, "machines", line + ".");

            html = Fill(html, "svg-title", "KABAN &nbsp; " + Enc(main.Label) + " &nbsp;&middot;&nbsp; user");

            // The machine list of the drawing: the machines themselves, the first
            // one highlighted as on screen. Room for three.
            var svg = new StringBuilder();
            var shown = new[] { main }.Concat(subs).Take(3).ToList();
            for (int i = 0; i < shown.Count; i++)
            {
                int y = 320 + 32 * i;
                if (i == 0) svg.Append("<rect x=\"16\" y=\"300\" width=\"178\" height=\"30\" rx=\"4\" fill=\"#d1d6dd\"/>");
                svg.Append("<text class=\"t\" x=\"40\" y=\"" + y + "\">" + Enc(string.IsNullOrEmpty(shown[i].Name) ? shown[i].Id : shown[i].Name) + "</text>");
            }
            html = Fill(html, "svg-machines", svg.ToString());

            var ch = new StringBuilder();
            ch.Append("<p>This program runs ").Append(line.Substring(4)).Append(".</p>\r\n");
            ch.Append("<table>\r\n<tr><th>Machine</th><th>Controller</th><th>Its pages</th></tr>\r\n");
            foreach (var m in new[] { main }.Concat(subs))
                ch.Append("<tr><td>").Append(Enc(m.Label)).Append(m.IsMain ? "" : " <i>(sub-machine)</i>")
                  .Append("</td><td>PLC station ").Append(m.Station)
                  .Append("</td><td>").Append(Enc(string.Join(", ", m.Pages))).Append("</td></tr>\r\n");
            ch.Append("</table>\r\n");
            foreach (var m in new[] { main }.Concat(subs).Where(x => !string.IsNullOrWhiteSpace(x.Chapter)))
            {
                if (subs.Count > 0) ch.Append("<h3>").Append(Enc(m.Label)).Append("</h3>\r\n");
                ch.Append(m.Chapter).Append("\r\n");
            }
            return Fill(html, "machine-chapter", ch.ToString());
        }

        /// <summary>
        /// Writes the manual for this program to the temp folder and returns its
        /// path - the plain manual when it cannot. A &lt;base&gt; pointing at the
        /// Help folder goes in, so pictures and links named in the manual still
        /// resolve from the copy.
        /// </summary>
        public static string PrepareManual()
        {
            string template = ManualPath();
            try
            {
                if (string.IsNullOrEmpty(template) || !File.Exists(template)) return template;
                var machines = RunningMachines();
                if (machines.Count == 0) return template;
                var html = ComposeManual(File.ReadAllText(template), machines);
                string baseTag = "<base href=\"" + new Uri(TroubleshootingCatalog.HelpFolder.TrimEnd('\\', '/') +
                                                             Path.DirectorySeparatorChar).AbsoluteUri + "\">";
                int head = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
                html = head >= 0 ? html.Insert(head + 6, baseTag) : baseTag + html;

                var main = machines.FirstOrDefault(m => m.IsMain) ?? machines[0];
                var path = Path.Combine(Path.GetTempPath(),
                    "Kaban_HMI_Manual_" + main.Id + "_" + Path.GetFileNameWithoutExtension(template).Split('.').Last() + ".html");
                File.WriteAllText(path, html, new UTF8Encoding(true));
                return path;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not prepare the manual for this machine: " + ex.Message);
                return template;
            }
        }

        private void OpenManual()
        {
            string template = ManualPath();
            if (string.IsNullOrEmpty(template) || !File.Exists(template))
            {
                _status.Text = LanguageManager.GetString("Msg_ManualMissing", "The manual was not found:") + " " + template;
                return;
            }
            string path = PrepareManual();
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
