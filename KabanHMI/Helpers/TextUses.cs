using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;
using Kaban.UI.Settings;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// WHERE A TRANSLATION IS USED (21 October: "a tool to find where the translation line is
    /// seen - on the screen, in a message box ... a button on top, Ctrl+F, which opens the list
    /// of where it is used"):
    ///   - the menu's page buttons and the pages' tabs titled by it;
    ///   - the controls of every page whose text keys name it (a button's text, a unit's title,
    ///     a tooltip ...), or that show its text without a key of their own - with the way to
    ///     the page, to open it and point at the control (the PLC Monitor's Find's);
    ///   - the alarms it is the text of;
    ///   - the program's lines naming it - message boxes, status lines, tables' headings - when
    ///     the program runs from its build (the source beside it).
    /// The pages are built once, off-screen, at the first search (a moment), and kept.
    /// </summary>
    public static class TextUses
    {
        public const string KindMenu = "Menu", KindTab = "Tab", KindPage = "Page", KindAlarm = "Alarm", KindCode = "Code";

        public sealed class Use
        {
            /// <summary>Menu, Tab, Page, Alarm or Code.</summary>
            public string Kind = "";
            /// <summary>The page ("Manual Movement / Cutting"), the alarm list, the file.</summary>
            public string Place = "";
            /// <summary>What uses it: "Left Top Clamping - Title", "message box: ..."; a program line as it is.</summary>
            public string What = "";
            /// <summary>A program line's: its line number.</summary>
            public int Line;
            /// <summary>Found by its text, not its key (a control made with its text).</summary>
            public bool ByText;
            /// <summary>The way to the page and to the control (null: none to open).</summary>
            public DeviceCollector.Use Way;
        }

        private sealed class Found
        {
            public string Key, Text, Kind, Place, What;
            public DeviceCollector.Use Way;
        }

        private static List<Found> _index;
        private static readonly object _lock = new object();

        /// <summary>Forgets the pages walked (the next search builds them again).</summary>
        public static void Forget() { lock (_lock) _index = null; }

        /// <summary>Every use of a key: the menu, the pages, the alarms, the program's lines.</summary>
        public static List<Use> Find(string key)
        {
            var list = new List<Use>();
            if (string.IsNullOrWhiteSpace(key)) return list;
            key = key.Trim();
            List<Found> index;
            lock (_lock) { if (_index == null) _index = Walk(); index = _index; }
            // its texts, for the controls made with their text (no key of their own)
            var texts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var l in new[] { LanguageManager.CurrentLanguage, "EN" })
            {
                var t = Norm(LanguageManager.GetStringIn(l, key));
                if (t.Length > 1) texts.Add(t);
            }
            foreach (var f in index)
            {
                bool byKey = f.Key != null && string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase);
                bool byText = !byKey && f.Key == null && f.Text != null && texts.Contains(f.Text);
                if (!byKey && !byText) continue;
                list.Add(new Use { Kind = f.Kind, Place = f.Place, What = f.What, Way = f.Way, ByText = byText });
            }
            foreach (var def in AlarmManager.GetAllDefinitions().Where(d => d != null && string.Equals(d.TranslationKey ?? "", key, StringComparison.OrdinalIgnoreCase)))
                list.Add(new Use { Kind = KindAlarm, Place = LanguageManager.GetString("Btn_Alarms", "Alarms"),
                                   What = (def.AlarmCode ?? "") + (string.IsNullOrWhiteSpace(def.Address) ? "" : "  (" + def.Address.Trim() + ")") });
            list.AddRange(InCode(key));
            return list;
        }

        private static string Norm(string text)
        {
            var t = (text ?? "").Replace("&", "").Trim().TrimEnd(':').Trim();
            return t;
        }

        // ---- the pages -------------------------------------------------------------------

        private static List<Found> Walk()
        {
            var found = new List<Found>();
            foreach (var kv in MachineRegistry.AllScreens())
            {
                var screen = kv.Value;
                string title = LanguageManager.GetString(screen.TitleKey ?? "", screen.TitleFallback ?? "");
                if (!kv.Key.IsMain) title = Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(kv.Key) + " / " + title;
                if (!string.IsNullOrEmpty(screen.TitleKey))
                    found.Add(new Found { Key = screen.TitleKey, Kind = KindMenu, Place = LanguageManager.GetString("Fnd_Menu", "Menu"),
                                          What = LanguageManager.GetString("Fnd_PageButton", "page button") + ": " + title,
                                          Way = new DeviceCollector.Use { Screen = screen, PageTitle = title } });
                if (screen.CreateView == null || screen.Role == ScreenRoles.AxisBar) continue;
                Control view = null;
                try
                {
                    view = screen.CreateView() as Control;
                    InView(found, screen, view, title, new List<int>(), 0);
                }
                catch (Exception ex) { AuditLogger.Log("text find skipped '" + title + "': " + ex.Message); }
                finally { AlarmHelpLinks.Discard(view); }
            }
            return found;
        }

        private static void InView(List<Found> found, MachineScreen screen, Control view, string title, List<int> pagePath, int depth)
        {
            if (view == null) return;
            var settings = view as SettingsScreen;
            if (settings != null && depth < 4)
            {
                for (int i = 0; i < settings.AllPages.Count; i++)
                {
                    var page = settings.AllPages[i];
                    string pageTitle = title + " / " + LanguageManager.GetString(page.TitleKey ?? "", page.TitleFallback ?? "");
                    var path = new List<int>(pagePath) { i };
                    if (!string.IsNullOrEmpty(page.TitleKey))
                        found.Add(new Found { Key = page.TitleKey, Kind = KindTab, Place = title, What = LanguageManager.GetString("Fnd_Tab", "tab") + ": " +
                                              LanguageManager.GetString(page.TitleKey, page.TitleFallback ?? ""),
                                              Way = new DeviceCollector.Use { Screen = screen, PageTitle = pageTitle, PagePath = path } });
                    // the translation table itself: not walked (it lists every text)
                    if (page is TranslationsPage) continue;
                    Control pv = null;
                    try
                    {
                        pv = page.GetControl();
                        InView(found, screen, pv, pageTitle, path, depth + 1);
                    }
                    catch (Exception ex) { AuditLogger.Log("text find skipped a page: " + ex.Message); }
                    finally { AlarmHelpLinks.Discard(pv); }
                }
                return;
            }
            foreach (Control child in view.Controls)
                InControl(found, screen, title, pagePath, child, new List<string> { Step(view, child) }, null, "");
        }

        private static readonly Dictionary<Type, PropertyInfo[]> _keyProperties = new Dictionary<Type, PropertyInfo[]>();

        private static PropertyInfo[] KeyProperties(Type t)
        {
            PropertyInfo[] ps;
            lock (_keyProperties)
            {
                if (_keyProperties.TryGetValue(t, out ps)) return ps;
                ps = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                      .Where(p => p.PropertyType == typeof(string) && p.Name.EndsWith("Key") && p.Name != "Key" && p.CanRead && p.GetIndexParameters().Length == 0)
                      .ToArray();
                _keyProperties[t] = ps;
            }
            return ps;
        }

        private static void InControl(List<Found> found, MachineScreen screen, string title, List<int> pagePath,
                                      Control c, List<string> path, List<string> unitPath, string owner)
        {
            var axisRow = c as AxisSettingHMI;
            if (axisRow != null && axisRow.Controls.Count == 0) { try { axisRow.BuildRow(); } catch { } }
            if (unitPath == null && (c is CylinderUnitHMI || c is AxisUnitHMI || c is AxisSettingHMI || c is CylinderRowHMI || c is CylinderEncoderUnitHMI)) unitPath = path;
            string name = NoteTarget.NameOf(c);
            if (c is CylinderUnitHMI || c is AxisSettingHMI || c is CylinderRowHMI) owner = name;
            string what = owner.Length > 0 && owner != name ? owner + " - " + name : name;
            var way = new DeviceCollector.Use { Screen = screen, PageTitle = title, PagePath = pagePath, ControlPath = path };

            bool keyed = false;
            foreach (var p in KeyProperties(c.GetType()))
            {
                string v;
                try { v = p.GetValue(c, null) as string; } catch { v = null; }
                if (string.IsNullOrWhiteSpace(v)) continue;
                keyed = true;
                found.Add(new Found { Key = v.Trim(), Kind = KindPage, Place = title, What = what + "  (" + Property(p.Name) + ")", Way = way });
            }
            // a table's headings: their keys (Tag) or their texts
            var grid = c as DataGridView;
            if (grid != null)
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    var k = col.Tag as string;
                    if (!string.IsNullOrWhiteSpace(k)) found.Add(new Found { Key = k.Trim(), Kind = KindPage, Place = title, What = what + "  (" + Heading(col) + ")", Way = way });
                    else if (!string.IsNullOrWhiteSpace(col.HeaderText)) found.Add(new Found { Text = Norm(col.HeaderText.Replace("▲", "").Replace("▼", "")), Kind = KindPage, Place = title,
                                                                                             What = what + "  (" + Heading(col) + ")", Way = way });
                }
            // made with its text (a page built in code): found by its text
            if (!keyed && (c is Label || c is ButtonBase || c is GroupBox || c is TabPage) && !string.IsNullOrWhiteSpace(c.Text) && c.Text.Any(char.IsLetter))
                found.Add(new Found { Text = Norm(c.Text), Kind = KindPage, Place = title, What = what, Way = way });
            foreach (Control child in c.Controls)
                InControl(found, screen, title, pagePath, child, new List<string>(path) { Step(c, child) }, unitPath, owner);
        }

        private static string Heading(DataGridViewColumn col)
        {
            return LanguageManager.GetString("Fnd_Heading", "column heading") + " " + (col.HeaderText ?? "").Replace("▲", "").Replace("▼", "").Trim();
        }

        private static string Property(string name)
        {
            switch (name)
            {
                case "TextKey": case "TranslationKey": return LanguageManager.GetString("Fnd_Text", "its text");
                case "TitleTextKey": case "TitleKey": return LanguageManager.GetString("Fnd_Title", "its title");
                case "ToolTipTextKey": case "TipKey": case "ToolTipKey": return LanguageManager.GetString("Fnd_Tip", "its tooltip");
                case "AxisNameKey": return LanguageManager.GetString("Fnd_Axis", "the axis name");
                default: return name.Substring(0, name.Length - 3);
            }
        }

        /// <summary>A child's step in a path - as the PLC Monitor's Find makes it (DeviceCollector.Locate follows it).</summary>
        private static string Step(Control parent, Control child)
        {
            var name = child.Name ?? "";
            if (name.Length > 0)
            {
                int same = 0;
                foreach (Control x in parent.Controls) if (x.Name == name) same++;
                if (same == 1) return name;
            }
            return "#" + parent.Controls.GetChildIndex(child);
        }

        // ---- the program's lines ---------------------------------------------------------

        /// <summary>Where the program's source is (the solution's folder: KabanHMI\KabanHMI.sln in it), found above the build; null on a panel without it. Replaceable for the tests.</summary>
        public static Func<string> SourceRoot = FindSourceRoot;

        private static string FindSourceRoot()
        {
            try
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                for (int i = 0; dir != null && i < 6; i++, dir = dir.Parent)
                    if (File.Exists(Path.Combine(dir.FullName, "KabanHMI", "KabanHMI.sln"))) return dir.FullName;
            }
            catch { }
            return null;
        }

        private sealed class SourceFile { public DateTime Stamp; public string[] Lines; }
        private static readonly Dictionary<string, SourceFile> _files = new Dictionary<string, SourceFile>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every line of the program naming the key ("Btn_Ok" in quotes), what kind of text it is.</summary>
        public static List<Use> InCode(string key)
        {
            var list = new List<Use>();
            var root = SourceRoot();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return list;
            string quoted = "\"" + key + "\"";
            foreach (var path in SourceFiles(root))
            {
                var lines = Lines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].IndexOf(quoted, StringComparison.Ordinal) < 0) continue;
                    var line = lines[i].Trim();
                    if (line.StartsWith("//") || line.StartsWith("///")) continue;
                    list.Add(new Use
                    {
                        Kind = KindCode, Place = MakeRelative(root, path), Line = i + 1,
                        What = KindOfLine(lines, i) + ": " + (line.Length > 180 ? line.Substring(0, 177) + "..." : line)
                    });
                }
            }
            return list;
        }

        /// <summary>The projects' .cs files (a folder with its .csproj), not their builds.</summary>
        private static IEnumerable<string> SourceFiles(string root)
        {
            IEnumerable<string> projects;
            try { projects = Directory.GetDirectories(root).Where(d => Directory.GetFiles(d, "*.csproj").Length > 0).ToList(); }
            catch { yield break; }
            foreach (var p in projects)
            {
                string[] files;
                try { files = Directory.GetFiles(p, "*.cs", SearchOption.AllDirectories); } catch { continue; }
                foreach (var f in files)
                {
                    var rel = f.Substring(p.Length);
                    if (Regex.IsMatch(rel, @"[\\/](bin|obj|\.vs|packages)[\\/]", RegexOptions.IgnoreCase)) continue;
                    yield return f;
                }
            }
        }

        private static string[] Lines(string path)
        {
            try
            {
                var stamp = File.GetLastWriteTimeUtc(path);
                lock (_files)
                {
                    SourceFile f;
                    if (_files.TryGetValue(path, out f) && f.Stamp == stamp) return f.Lines;
                    f = new SourceFile { Stamp = stamp, Lines = File.ReadAllLines(path) };
                    _files[path] = f;
                    return f.Lines;
                }
            }
            catch { return new string[0]; }
        }

        private static string MakeRelative(string root, string path)
        {
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length).TrimStart('\\', '/') : path;
        }

        /// <summary>What a program line shows the text as: a message, the status line, a heading, a tooltip, a menu entry - a text.</summary>
        internal static string KindOfLine(string[] lines, int at)
        {
            // the statement may begin a line or two above (a long call broken over lines)
            var text = string.Join(" ", lines.Skip(Math.Max(0, at - 2)).Take(Math.Min(3, at + 1)));
            if (Regex.IsMatch(text, @"MessageBox\.Show|Ask\.(Tell|YesNo|Text)|\.Tell\(")) return LanguageManager.GetString("Fnd_Message", "message box");
            if (Regex.IsMatch(text, @"SetStatus|_status\.Text|Status\s*\(")) return LanguageManager.GetString("Fnd_Status", "status line");
            if (lines[at].Contains("HeaderText")) return LanguageManager.GetString("Fnd_ColHeading", "table heading");
            if (Regex.IsMatch(lines[at], @"SetTip|ToolTip|Tip_")) return LanguageManager.GetString("Fnd_Tooltip", "tooltip");
            if (Regex.IsMatch(lines[at], @"ToolStripMenuItem|Item\(")) return LanguageManager.GetString("Fnd_MenuEntry", "menu entry");
            return LanguageManager.GetString("Fnd_TextOf", "text");
        }
    }
}
