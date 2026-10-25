using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// A MAIN PAGE IN THE SIDE PANEL (8 October: "It can be useful to create a way
    /// to open any main page in the second page if it is not too complex or
    /// unsafe"): a machine's Home, Manual Movement, Machine Parameters, Alarms or
    /// Dashboard opened beside the page on screen - its own copy of the page,
    /// built as the main one is, read and commanded as it is (the panel is read
    /// by the scanner; every button keeps its rights and its log).
    ///
    /// How: a right-click on a page button of the menu ("Open in the side
    /// panel"), or the side bar's + button (a list of the pages, for a touch
    /// panel). The page then has a button on the side bar of its own; a
    /// right-click on it takes it off again. Kept between runs.
    ///
    /// Not offered: HMI Settings (one set of files, and its own pages are edited
    /// in place) and the pages without a role (FA2010's Profiles: a library
    /// edited and saved - two editors of one file would overwrite each other).
    /// </summary>
    public partial class MainForm
    {
        internal const string MainPagePrefix = "Main:";

        /// <summary>The roles whose pages may be opened in the side panel.</summary>
        internal static readonly string[] SideRoles =
        {
            ScreenRoles.Home, ScreenRoles.Manual, ScreenRoles.MachineParameters, ScreenRoles.Alarms, ScreenRoles.Dashboard
        };

        /// <summary>The main pages put on the side bar, in the order they were put there.</summary>
        private readonly List<string> _mainPagesInSide = new List<string>();

        public static bool CanOpenInSide(MachineRegistry.RoleGroup group)
        {
            return group != null && group.Pages.Count > 0 && SideRoles.Contains(group.Role ?? "", StringComparer.OrdinalIgnoreCase);
        }

        public static string MainPageKey(MachineRegistry.RoleGroup group, MachineRegistry.MountedModule machine)
        {
            return MainPagePrefix + group.Role + ":" + (machine.Context != null ? machine.Context.ModuleId : machine.Module.ModuleId);
        }

        /// <summary>The page's group and machine from its key, or false.</summary>
        private static bool FindMainPage(string key, out MachineRegistry.RoleGroup group, out KeyValuePair<MachineRegistry.MountedModule, MachineScreen> page)
        {
            group = null;
            page = default(KeyValuePair<MachineRegistry.MountedModule, MachineScreen>);
            if (string.IsNullOrEmpty(key) || !key.StartsWith(MainPagePrefix, StringComparison.OrdinalIgnoreCase)) return false;
            foreach (var g in MachineRegistry.NavigationGroups())
            {
                if (!CanOpenInSide(g)) continue;
                foreach (var p in g.Pages)
                    if (string.Equals(MainPageKey(g, p.Key), key, StringComparison.OrdinalIgnoreCase)) { group = g; page = p; return true; }
            }
            return false;
        }

        /// <summary>The page registered as a side page (once), or null.</summary>
        private SidePage RegisterMainPage(string key)
        {
            var known = SidePages.Find(key);
            if (known != null) return known;
            MachineRegistry.RoleGroup group;
            KeyValuePair<MachineRegistry.MountedModule, MachineScreen> page;
            if (!FindMainPage(key, out group, out page)) return null;
            var screen = page.Value;
            var side = new SidePage
            {
                Key = key,
                TitleKey = group.TitleKey,
                TitleFallback = group.TitleFallback,
                // the machine's name before the page's when more than one machine has it
                MachineName = group.IsTabbed ? Screens.MachineTabScreen.DisplayNameOf(page.Key) : "",
                Icon = group.Icon,
                Glyph = DrawPageGlyph,
                Create = () => screen.CreateView() as Control,
                RequiredLevel = screen.RequiredLevel > group.RequiredLevel ? screen.RequiredLevel : group.RequiredLevel,
                SortOrder = 500 + _mainPagesInSide.Count
            };
            SidePages.Register(side);
            if (!_mainPagesInSide.Contains(key, StringComparer.OrdinalIgnoreCase)) _mainPagesInSide.Add(key);
            return side;
        }

        /// <summary>Opens a machine's main page in the side panel (its button put on the side bar). Its view, or null.</summary>
        public Control OpenMainPageInSide(MachineRegistry.RoleGroup group, MachineRegistry.MountedModule machine)
        {
            if (!CanOpenInSide(group) || machine == null) return null;
            string key = MainPageKey(group, machine);
            bool added = SidePages.Find(key) == null;
            var side = RegisterMainPage(key);
            if (side == null) return null;
            if (added)
            {
                AuditLogger.Log("put '" + side.Title + "' on the side bar.");
                SaveSidePanelState();
                RefreshSideBar();
            }
            return ShowSidePage(key);
        }

        /// <summary>Takes a main page off the side bar (closed, its copy let go).</summary>
        public void RemoveMainPageFromSide(string key)
        {
            var side = SidePages.Find(key);
            if (side == null || !key.StartsWith(MainPagePrefix, StringComparison.OrdinalIgnoreCase)) return;
            if (OpenSidePage != null && string.Equals(OpenSidePage.Key, key, StringComparison.OrdinalIgnoreCase)) CloseSidePanel();
            SidePageWindow window;
            if (_sideWindows.TryGetValue(key, out window) && !window.IsDisposed) window.Close();
            _sideWantsWindow.Remove(key);
            Control view;
            if (_sideViews.TryGetValue(key, out view))
            {
                _sideViews.Remove(key);
                if (view.Parent != null) view.Parent.Controls.Remove(view);
                view.Dispose();
            }
            SidePages.Unregister(key);
            _mainPagesInSide.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            AuditLogger.Log("took '" + side.Title + "' off the side bar.");
            SaveSidePanelState();
            RefreshSideBar();
            if (_scanner != null) _scanner.RefreshTags();
        }

        /// <summary>The main pages kept on the side bar from the last run, registered again.</summary>
        private void RegisterKeptMainPages(IEnumerable<string> keys)
        {
            foreach (var key in keys ?? new string[0])
                try { RegisterMainPage(key.Trim()); } catch (Exception ex) { AuditLogger.Log("side page '" + key + "': " + ex.Message); }
        }

        /// <summary>The menu of a page button: its page in the side panel, per machine.</summary>
        public ContextMenuStrip SideMenuFor(MachineRegistry.RoleGroup group)
        {
            var menu = UiStyle.Menu();
            if (!CanOpenInSide(group)) return null;
            menu.Items.Add(UiStyle.MenuHeader(LanguageManager.GetString("Side_OpenHere", "Open in the side panel")));
            foreach (var p in group.Pages)
            {
                var machine = p.Key;
                string text = group.IsTabbed ? Screens.MachineTabScreen.DisplayNameOf(machine)
                                             : LanguageManager.GetString(group.TitleKey ?? "", group.TitleFallback ?? "");
                var item = new ToolStripMenuItem(text) { Name = "side:" + MainPageKey(group, machine), Checked = SidePages.Find(MainPageKey(group, machine)) != null };
                item.Click += (s, e) => OpenMainPageInSide(group, machine);
                menu.Items.Add(item);
                // a page the user's level may not open: there, greyed, saying whose it is (24 October)
                UiStyle.Unavailable(item, SecurityManager.HasAccess(p.Value.RequiredLevel), LevelNeeded(p.Value.RequiredLevel));
            }
            return menu.Items.Count > 1 ? menu : null;
        }

        /// <summary>The side bar's + button: every page that may be opened there.</summary>
        public ContextMenuStrip SidePagesMenu()
        {
            var menu = UiStyle.Menu();
            menu.Items.Add(UiStyle.MenuHeader(LanguageManager.GetString("Side_OpenHere", "Open in the side panel")));
            foreach (var group in MachineRegistry.NavigationGroups().Where(CanOpenInSide))
            {
                string title = LanguageManager.GetString(group.TitleKey ?? "", group.TitleFallback ?? "");
                foreach (var p in group.Pages)
                {
                    var level = (SecurityLevel)Math.Max((int)group.RequiredLevel, (int)p.Value.RequiredLevel);
                    var g = group;
                    var machine = p.Key;
                    string key = MainPageKey(g, machine);
                    var item = new ToolStripMenuItem(group.IsTabbed ? title + " - " + Screens.MachineTabScreen.DisplayNameOf(machine) : title)
                    {
                        Name = "side:" + key, Checked = SidePages.Find(key) != null,
                        Image = g.Icon != null ? UiScale.Picture(g.Icon() as Image) : null
                    };
                    item.Click += (s, e) => OpenMainPageInSide(g, machine);
                    menu.Items.Add(item);
                    // a page the user's level may not open: there, greyed, saying whose it is (24 October)
                    UiStyle.Unavailable(item, SecurityManager.HasAccess(level), LevelNeeded(level));
                }
            }
            return menu;
        }

        /// <summary>A greyed entry's why: "For Maintenance and higher - sign in as one".</summary>
        internal static string LevelNeeded(SecurityLevel level)
        {
            return string.Format(LanguageManager.GetString("Why_LevelNeeded", "For {0} and higher - sign in as one"), Kaban.UI.Settings.PrefTexts.LevelText(level));
        }

        /// <summary>A page button's right-click: the side panel menu for its page.</summary>
        private void OfferSideMenu(Controls.ButtonHMI button, UserControl view)
        {
            button.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                MachineRegistry.RoleGroup group;
                if (!_screenGroups.TryGetValue(view, out group)) return;
                var menu = SideMenuFor(group);
                if (menu != null) UiStyle.ShowPopup(menu, button, e.Location);
            };
        }

        /// <summary>A side bar button's right-click: a main page put there is taken off again.</summary>
        private void OfferRemove(Button b, SidePage page)
        {
            if (!page.Key.StartsWith(MainPagePrefix, StringComparison.OrdinalIgnoreCase)) return;
            b.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                var menu = UiStyle.Menu();
                var item = new ToolStripMenuItem(LanguageManager.GetString("Side_Remove", "Take it off the side bar")) { Name = "sideRemove" };
                item.Click += (s2, e2) => BeginInvoke(new Action(() => RemoveMainPageFromSide(page.Key)));
                menu.Items.Add(item);
                UiStyle.ShowPopup(menu, b, e.Location);
            };
        }

        /// <summary>A main page's picture on the side bar when its group has none: a page.</summary>
        internal static void DrawPageGlyph(Graphics g, Rectangle r, Color colour)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            using (var pen = new Pen(colour, 2f))
            {
                g.DrawRectangle(pen, cx - 8, cy - 10, 16, 20);
                for (int i = 0; i < 3; i++) g.DrawLine(pen, cx - 4, cy - 4 + i * 5, cx + 4, cy - 4 + i * 5);
            }
        }

        /// <summary>The side bar's + : a plus in a circle.</summary>
        internal static void DrawAddGlyph(Graphics g, Rectangle r, Color colour)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            using (var pen = new Pen(colour, 2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
            {
                g.DrawEllipse(pen, cx - 10, cy - 10, 20, 20);
                g.DrawLine(pen, cx - 5, cy, cx + 5, cy);
                g.DrawLine(pen, cx, cy - 5, cx, cy + 5);
            }
        }
    }
}
