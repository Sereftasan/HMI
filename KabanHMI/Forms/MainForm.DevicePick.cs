using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Helpers;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// THE PLC MONITOR'S PICK (23 October: "another member to 'Add the devices of the screen ...' dropdown
    /// menu in PLC Monitor to select the control on the page (as the first item). It should be called 'all
    /// under the next control / page - into this tab' and it should turn the rest of the screen darker (just
    /// like it does on Notes and Feedback page) and everything under the first clicked control or page
    /// navigation button or tab title should be added to the active tab"): while it is on, a click on the page,
    /// a page button of the menu, a machine of the machine list or a tab's title is not used - it picks.
    /// The page, the caption bar and the menu darker, the thing under the pointer bright (PickShade, as the
    /// notes page's); Esc - or the PLC Monitor's blinking button - gives up. The PLC Monitor itself (in the
    /// side panel or its own window) works as ever.
    ///
    /// What a click takes: a page button - every page it opens (each machine's, every tab); a machine of the
    /// list - that machine's page; a tab's title - its page, every tab of it; a tab of a page's tab control -
    /// that tab; a control - everything under it (a unit's title: the unit; a field: the field; the empty
    /// part of a group: the group). A control with no device of the PLC under it (a label): the nearest one
    /// around it that has some.
    /// </summary>
    public partial class MainForm
    {
        /// <summary>What a click picks: the control left bright, its name, its devices (worked out on the click).</summary>
        internal sealed class PickCandidate
        {
            public Control Bright;
            public string Title = "";
            public Func<List<DeviceCollector.Device>> Devices;
        }

        private DevicePickFilter _devicePick;
        private PickShade _devicePickShade;

        /// <summary>True while a click picks for the PLC Monitor.</summary>
        public bool DevicePicking { get { return _devicePick != null; } }

        /// <summary>The shade while picking (tests).</summary>
        internal PickShade DevicePickShade { get { return _devicePickShade; } }

        /// <summary>
        /// Picking on: the next click on the page, a page button, a machine or a tab picks what is under it for
        /// <paramref name="station"/> - <paramref name="picked"/> gets its name and devices (false back: nothing
        /// of the PLC there - picking goes on); <paramref name="ended"/> when it is over (true: given up).
        /// </summary>
        public void BeginDevicePick(int station, Func<string, List<DeviceCollector.Device>, bool> picked, Action<bool> ended)
        {
            EndDevicePick(true);
            _devicePick = new DevicePickFilter(this, station, picked, ended);
            Application.AddMessageFilter(_devicePick);
            if (_devicePickShade == null) _devicePickShade = new PickShade(() => new Control[] { pnlTop, pnlMenu, pnlContent });
            _devicePickShade.On = true;
        }

        /// <summary>Picking given up (the PLC Monitor's button, Esc).</summary>
        public void CancelDevicePick() { EndDevicePick(true); }

        private void EndDevicePick(bool cancelled)
        {
            var f = _devicePick;
            if (f == null) return;
            _devicePick = null;
            Application.RemoveMessageFilter(f);
            if (_devicePickShade != null) { _devicePickShade.Hole = null; _devicePickShade.On = false; }
            if (f.Ended != null) f.Ended(cancelled);
        }

        /// <summary>A click on <paramref name="c"/> picked: its devices to the PLC Monitor; picking over unless it had none.</summary>
        internal void DevicePicked(PickCandidate c)
        {
            var f = _devicePick;
            if (f == null || c == null) return;
            List<DeviceCollector.Device> devices;
            var cursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try { devices = c.Devices(); }
            catch (Exception ex) { AuditLogger.Log("PLC Monitor pick: " + ex.Message); devices = new List<DeviceCollector.Device>(); }
            finally { Cursor.Current = cursor; }
            bool taken = f.Picked != null && f.Picked(c.Title, devices);
            if (taken) EndDevicePick(false);
        }

        /// <summary>
        /// What a click on <paramref name="hit"/> (at <paramref name="client"/> in it) picks, or null - not a
        /// place that picks (the PLC Monitor itself, another window): the click is its own.
        /// </summary>
        internal PickCandidate CandidateAt(Control hit, Point client, int station)
        {
            if (hit == null || hit.IsDisposed) return null;
            for (var p = hit; p != null; p = p.Parent) if (p is SimulationMonitorForm) return null;
            bool inMenu = pnlMenu.Contains(hit) || hit == pnlMenu, inContent = pnlContent.Contains(hit), inTop = pnlTop.Contains(hit);
            if (!inMenu && !inContent && !inTop) return null;

            // A PAGE BUTTON of the menu: every page it opens - each machine's, every tab
            for (var p = hit; p != null && inMenu; p = p.Parent)
            {
                var b = p as Controls.ButtonHMI;
                if (b == null) continue;
                UserControl view;
                if (_menuButtons.TryGetValue(b, out view))
                {
                    MachineRegistry.RoleGroup group;
                    _screenGroups.TryGetValue(view, out group);
                    var screens = group == null ? new List<MachineScreen>() : group.Pages.Select(kv => kv.Value).ToList();
                    return new PickCandidate
                    {
                        Bright = b, Title = MenuTarget(b),
                        Devices = () => Merge(screens.Select(s => DeviceCollector.FromScreen(s, station)))
                    };
                }
                // A MACHINE of the machine list: that machine's page
                if (_machineStrip != null && _machineStrip.Contains(b))
                {
                    int i = _machineStrip.IndexOfButton(b);
                    var g = _machineStrip.Group;
                    if (g == null || i < 0 || i >= g.Pages.Count) return null;
                    var screen = g.Pages[i].Value;
                    return new PickCandidate { Bright = b, Title = MenuTarget(b), Devices = () => DeviceCollector.FromScreen(screen, station) };
                }
            }
            if (inMenu) return null;                        // the menu's other parts: the bar's own

            // A TAB'S TITLE (a strip of the HMI's pages): its page, every tab of it
            for (var p = hit; p != null; p = p.Parent)
            {
                if (!Settings.SettingsScreen.IsTab(p)) continue;
                var page = Settings.SettingsScreen.PageOfTab(p);
                Settings.SettingsScreen strip = null;
                for (var q = p.Parent; q != null && strip == null; q = q.Parent) strip = q as Settings.SettingsScreen;
                if (page == null || strip == null) return null;
                var shown = strip.ViewOf(page);
                return new PickCandidate
                {
                    Bright = p, Title = LanguageManager.GetString(page.TitleKey ?? "", page.TitleFallback ?? ""),
                    Devices = () => DeviceCollector.FromPage(page, shown, station)
                };
            }
            // A TAB of a page's tab control (its row of tabs): that tab
            var tabs = hit as TabControl;
            if (tabs != null)
            {
                for (int i = 0; i < tabs.TabCount; i++)
                    if (tabs.GetTabRect(i).Contains(client))
                    {
                        var tp = tabs.TabPages[i];
                        return new PickCandidate { Bright = tabs, Title = tp.Text, Devices = () => DeviceCollector.UnderControl(tp, station) };
                    }
            }
            // A CONTROL: everything under it - or, nothing of the PLC there, under the nearest around it that has some
            var target = NoteTarget.Notable(hit) ?? hit;
            for (var p = target; p != null; p = p.Parent)
            {
                if (p == pnlContent || p == pnlTop || p is Form) break;
                if (p is ScrollBar) return null;
                if (DeviceCollector.UnderControl(p, station).Count == 0) continue;
                var found = p;
                return new PickCandidate { Bright = found, Title = Describe(found), Devices = () => DeviceCollector.UnderControl(found, station) };
            }
            return null;
        }

        /// <summary>A picked control's name: as the notes page names it, where it is.</summary>
        private string Describe(Control c)
        {
            if (c == _currentScreen || c.Parent == pnlContent) return CurrentPlace();
            var name = NoteTarget.NameOf(c);
            return string.IsNullOrWhiteSpace(name) ? PlaceOf(c) : name;
        }

        private static List<DeviceCollector.Device> Merge(IEnumerable<List<DeviceCollector.Device>> lists)
        {
            var all = new List<DeviceCollector.Device>();
            foreach (var l in lists)
                foreach (var d in l)
                    if (!all.Any(x => x.Station == d.Station && string.Equals(x.Address, d.Address, StringComparison.OrdinalIgnoreCase))) all.Add(d);
            return all;
        }

        /// <summary>
        /// The clicks while picking: a press, a release, a double click on a place that picks are kept from it,
        /// the release picks; the pointer moving shows (bright) what a click would take; Esc gives up.
        /// </summary>
        private sealed class DevicePickFilter : IMessageFilter
        {
            private const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_LBUTTONDBLCLK = 0x0203,
                              WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_RBUTTONDBLCLK = 0x0206, WM_KEYDOWN = 0x0100;
            private readonly MainForm _shell;
            private readonly int _station;
            public readonly Func<string, List<DeviceCollector.Device>, bool> Picked;
            public readonly Action<bool> Ended;
            private Control _lastHit;
            private PickCandidate _last;

            public DevicePickFilter(MainForm shell, int station, Func<string, List<DeviceCollector.Device>, bool> picked, Action<bool> ended)
            {
                _shell = shell; _station = station; Picked = picked; Ended = ended;
            }

            private static Point PointOf(Message m)
            {
                long l = (long)m.LParam;
                return new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF));
            }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg == WM_KEYDOWN)
                {
                    if ((Keys)(int)(long)m.WParam != Keys.Escape) return false;
                    try { _shell.BeginInvoke(new Action(_shell.CancelDevicePick)); } catch { }
                    return true;
                }
                if (m.Msg != WM_MOUSEMOVE && m.Msg != WM_LBUTTONDOWN && m.Msg != WM_LBUTTONUP && m.Msg != WM_LBUTTONDBLCLK &&
                    m.Msg != WM_RBUTTONDOWN && m.Msg != WM_RBUTTONUP && m.Msg != WM_RBUTTONDBLCLK) return false;
                if (_shell.IsDisposed) return false;
                var hit = Control.FromChildHandle(m.HWnd);
                PickCandidate c;
                // the same control under the pointer: what it picks worked out once (a tab control: by the tab)
                if (hit != null && hit == _lastHit && !(hit is TabControl)) c = _last;
                else
                {
                    try { c = _shell.CandidateAt(hit, PointOf(m), _station); } catch { c = null; }
                    _lastHit = hit; _last = c;
                }
                if (m.Msg == WM_MOUSEMOVE)
                {
                    if (_shell._devicePickShade != null) _shell._devicePickShade.Hole = c == null ? null : c.Bright;
                    return false;
                }
                if (c == null) return false;                          // the PLC Monitor's own, another window's
                if (m.Msg == WM_LBUTTONUP)
                {
                    var picked = c;
                    try { _shell.BeginInvoke(new Action(() => _shell.DevicePicked(picked))); } catch { }
                }
                return true;                                          // kept from the control: not used
            }
        }
    }
}
