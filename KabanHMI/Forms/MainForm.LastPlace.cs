using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

namespace Kaban.UI.Forms
{
    // ======================================================================
    // BACK WHERE IT WAS LEFT
    // ----------------------------------------------------------------------
    // The program reopens on the page it was on - the machine and every tab on
    // the way down - with the side page and the side windows that were open.
    // A restart for the magnifying factor, a settings restart, a power cut:
    // the operator finds the screen as they left it instead of on Home.
    //
    // Kept in LastPage.cfg beside the side panel's settings, written whenever
    // the place changes (a power cut gives no chance to write at exit). A page
    // the user now signed in may not open waits for the first sign-in, unless
    // the operator has gone somewhere else by then.
    // ======================================================================
    public partial class MainForm
    {
        /// <summary>
        /// True for the program itself: a test builds the shell too, and must
        /// neither be put on a page left by an earlier run nor leave one behind.
        /// </summary>
        public bool RememberPlace { get; set; } = IsMachineProgram();

        private static bool IsMachineProgram()
        {
            try
            {
                var entry = System.Reflection.Assembly.GetEntryAssembly();
                return entry != null && entry.GetName().Name.StartsWith("Kaban.", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>Where the place is kept: beside SidePanel.cfg.</summary>
        internal static string LastPlacePath
        {
            get { return Path.Combine(Path.GetDirectoryName(SidePanelPath) ?? "", "LastPage.cfg"); }
        }

        private Dictionary<string, string> _pendingPlace;
        private bool _placeSettled;      // the start-up place has been dealt with: saving may begin

        /// <summary>Where the operator is: the nav page, the tab path, the side page, the side windows.</summary>
        internal Dictionary<string, string> CapturePlace()
        {
            var v = new Dictionary<string, string>();
            Core.Machines.MachineRegistry.RoleGroup grp;
            if (_currentScreen != null && _screenGroups.TryGetValue(_currentScreen, out grp))
            {
                v["Page"] = GroupKey(grp);
                v["Tabs"] = string.Join("|", SettingsScreen.CaptureChain(_currentScreen)
                    .Select(step => step.Key.AllPages.IndexOf(step.Value) + ":" + PageKey(step.Value)));
            }
            if (_sidePanel != null && _sidePanel.Visible && _sideOpen != null) v["Side"] = _sideOpen.Key;
            var windows = _sideWindows.Where(kv => kv.Value != null && !kv.Value.IsDisposed && kv.Value.Visible).ToList();
            if (windows.Count > 0) v["Windows"] = string.Join(",", windows.Select(kv => kv.Key));
            // The ones down on the taskbar open there again.
            var down = windows.Where(kv => IsMinimized(kv.Value)).Select(kv => kv.Key).ToList();
            if (down.Count > 0) v["WindowsMin"] = string.Join(",", down);
            return v;
        }

        private static string GroupKey(Core.Machines.MachineRegistry.RoleGroup g)
        {
            return (g.Role ?? "") + "/" + (g.TitleKey ?? g.TitleFallback ?? "");
        }

        private static string PageKey(ISettingsPage p)
        {
            return (p.TitleKey ?? p.TitleFallback ?? "").Replace("|", "").Replace(":", "");
        }

        /// <summary>Writes the place now (once the start-up place is settled).</summary>
        internal void SavePlace()
        {
            if (!RememberPlace || !_placeSettled || _placeFrozen || IsDisposed) return;
            // The whole file: a side page closed since must not stay in it.
            try { WindowLayout.Write(LastPlacePath, CapturePlace(), replace: true); }
            catch { /* a place not kept is only a place not kept */ }
        }

        /// <summary>At start-up, once the sign-in step is over: the place of the last run.</summary>
        private void RestoreLastPlace()
        {
            if (!RememberPlace) return;
            try
            {
                var v = WindowLayout.Read(LastPlacePath);
                if (v.Count > 0 && !RestorePlace(v)) _pendingPlace = v;
            }
            catch (Exception ex) { AuditLogger.Log("the last page could not be reopened: " + ex.Message); }
            finally
            {
                _placeSettled = true;
                if (_pendingPlace == null) SavePlace();
            }
        }

        /// <summary>
        /// Goes to a captured place. True when the page was reached (the tabs as
        /// far as they still exist and may be opened); false when the page is
        /// above the user's level - it can be tried again after a sign-in.
        /// </summary>
        internal bool RestorePlace(Dictionary<string, string> v)
        {
            _restoringPlace = true;
            try { return RestorePlaceCore(v); }
            finally { _restoringPlace = false; }
        }

        private void OnHistoryChanged(object sender, EventArgs e) { PlaceChangedByOperator(); }

        private bool RestorePlaceCore(Dictionary<string, string> v)
        {
            string text;
            bool reached = true;
            if (v.TryGetValue("Page", out text) && text.Length > 0)
            {
                var nav = _screenGroups.FirstOrDefault(kv => GroupKey(kv.Value) == text).Key;
                if (nav != null)
                {
                    Kaban.Core.Enums.SecurityLevel needed;
                    if (_screenLevels.TryGetValue(nav, out needed) && !SecurityManager.HasAccess(needed)) reached = false;
                    else
                    {
                        LoadScreen(nav);
                        reached = ReferenceEquals(_currentScreen, nav);
                        if (reached && v.TryGetValue("Tabs", out text)) RestoreTabs(nav, text);
                        SyncMachineStrip();
                        RecordLocation();
                    }
                }
            }
            // A side page above the user's level waits for the sign-in, as the page does.
            if (v.TryGetValue("Side", out text) && text.Length > 0 && (_sideOpen == null || _sideOpen.Key != text || !_sidePanel.Visible))
                if (!TryShowSide(text)) reached = false;
            string minimizedText;
            var minimized = new HashSet<string>((v.TryGetValue("WindowsMin", out minimizedText) ? minimizedText : "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()), StringComparer.OrdinalIgnoreCase);
            if (v.TryGetValue("Windows", out text))
                foreach (var key in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()))
                {
                    SidePageWindow open;
                    if (_sideWindows.TryGetValue(key, out open) && !open.IsDisposed && open.Visible) continue;
                    if (!TryShowSide(key)) { reached = false; continue; }
                    if (minimized.Contains(key) && _sideWindows.TryGetValue(key, out open) && !open.IsDisposed)
                        Minimize(open);
                }
            return reached;
        }

        /// <summary>Reopens a side page; false when the user may not open it (yet) - a page no longer there counts as done.</summary>
        private bool TryShowSide(string key)
        {
            var page = SidePages.All().FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            if (page == null) return true;
            if (!SecurityManager.HasAccess(page.RequiredLevel)) return false;
            try { ShowSidePage(page.Key); }
            catch (Exception ex) { AuditLogger.Log("side page '" + key + "' not reopened: " + ex.Message); }
            return true;
        }

        /// <summary>A window down on the taskbar - and putting one there (replaceable for the tests: Mono without a window manager cannot).</summary>
        internal static Func<Form, bool> IsMinimized = f => f.WindowState == FormWindowState.Minimized;
        internal static Action<Form> Minimize = f => f.WindowState = FormWindowState.Minimized;

        /// <summary>
        /// Set once the program is closing and its place is written: the side
        /// windows closing after it would otherwise write a place without them.
        /// </summary>
        private bool _placeFrozen;

        /// <summary>The tab path "2:Set_StationConn|0:Controllers" - by position, checked by name.</summary>
        private static void RestoreTabs(Control top, string path)
        {
            Control c = top;
            foreach (var step in path.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var s = SettingsScreen.TabsIn(c);
                if (s == null) return;
                int colon = step.IndexOf(':');
                int index;
                string name = colon >= 0 ? step.Substring(colon + 1) : "";
                if (colon < 0 || !int.TryParse(step.Substring(0, colon), out index)) index = -1;
                ISettingsPage page = index >= 0 && index < s.AllPages.Count && PageKey(s.AllPages[index]) == name
                    ? s.AllPages[index]
                    : s.AllPages.FirstOrDefault(p => PageKey(p) == name);
                if (page == null || !SecurityManager.HasAccess(page.RequiredLevel)) return;
                s.ShowPage(page);
                c = s.ViewOf(page);
                if (c == null) return;
            }
        }

        /// <summary>After a sign-in: the place of the last run, when it waited for one and nothing else was opened meanwhile.</summary>
        private void RestorePendingPlace()
        {
            var v = _pendingPlace;
            if (v == null) return;
            if (RestorePlace(v)) _pendingPlace = null;
            SavePlace();
        }

        /// <summary>The operator went somewhere themselves: the waiting place is dropped.</summary>
        private void PlaceChangedByOperator()
        {
            if (!_restoringPlace) _pendingPlace = null;
            SavePlace();
        }

        private bool _restoringPlace;
    }
}
