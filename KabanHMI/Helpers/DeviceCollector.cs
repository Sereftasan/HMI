using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Settings;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// Every PLC device a page uses - address, data type and a name - for the
    /// PLC Monitor's "add the devices of this screen / of the whole program".
    ///
    /// The devices are read off the CONTROLS, the same places the scanner reads
    /// them from (commands, relays, outputs, sensor lamps, values, code words),
    /// on the station each control is on. So a watch list built this way is
    /// exactly what the page talks to - including the devices nobody wrote
    /// into PlcLabels.csv, which are the ones worth watching when a page
    /// misbehaves.
    ///
    /// The name is the PLC label list's when it has one; otherwise it says
    /// where the device is used ("Left Top Clamping - command A"), which beats
    /// an empty cell when the list is read later.
    /// </summary>
    public static class DeviceCollector
    {
        public const string Bit = "Bit";
        public const string Word = "Word [Signed]";
        public const string DoubleWord = "Double Word [Signed]";

        public sealed class Device
        {
            public int Station;
            public string Address = "";
            public string DataType = Bit;
            /// <summary>Where the device is used, for a device the label list does not name.</summary>
            public string UsedAs = "";
        }

        /// <summary>One page and the devices it uses.</summary>
        public sealed class PageDevices
        {
            public string Title = "";
            public List<Device> Devices = new List<Device>();
        }

        // ---- one control tree ---------------------------------------------

        /// <summary>
        /// The devices under a control on one station, in the order they are
        /// met, each address once. Only the SHOWN tab of a tab control is
        /// walked: "this screen" means what is on it.
        /// </summary>
        public static List<Device> FromControl(Control root, int station)
        {
            var list = new List<Device>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Walk(root, station, list, seen, "");
            return list;
        }

        private static void Walk(Control c, int station, List<Device> list, HashSet<string> seen, string owner)
        {
            if (c == null) return;

            // An axis row lays itself out when it gets a handle; one built only
            // to be searched never gets one.
            var axisRow = c as AxisSettingHMI;
            if (axisRow != null && axisRow.Controls.Count == 0) axisRow.BuildRow();

            string here = OwnerName(c);
            if (here.Length > 0) owner = here;

            Collect(c, station, list, seen, owner);

            var tabs = c as TabControl;
            if (tabs != null)
            {
                if (tabs.SelectedTab != null) Walk(tabs.SelectedTab, station, list, seen, owner);
                return;
            }
            foreach (Control child in c.Controls) Walk(child, station, list, seen, owner);
        }

        /// <summary>The unit or axis a control belongs to, for the "used as" name.</summary>
        private static string OwnerName(Control c)
        {
            var fu = c as CylinderUnitHMI;
            if (fu != null) return Tr(fu.TitleTextKey);
            var ax = c as AxisSettingHMI;
            if (ax != null) return Tr(ax.AxisNameKey);
            var row = c as CylinderRowHMI;
            if (row != null) return Tr(row.TitleTextKey);
            return "";
        }

        private static string Tr(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "";
            return LanguageManager.GetString(key, key);
        }

        private static int StationOf(Control c)
        {
            var p = c.GetType().GetProperty("PlcNumber");
            if (p == null || p.PropertyType != typeof(int)) return 0;
            try { return (int)p.GetValue(c, null); } catch { return 0; }
        }

        private static int StaticOffset(DynamicValue dv)
        {
            return dv != null && dv.Type == LimitSourceType.Static ? (int)dv.StaticValue : 0;
        }

        /// <summary>The station a collection is for (Collect sets it): a control's device of another controller ("X12@2") is that one's.</summary>
        [ThreadStatic] private static int _wanted;

        private static void Add(List<Device> list, HashSet<string> seen, int station, string address,
                                string dataType, string owner, string role)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            // "X12@2" on a PLC 1 control is a device of PLC 2 (12 October)
            string device;
            PlcAddressHelper.Resolve(station, address.Trim(), out station, out device);
            if (_wanted > 0 && station != _wanted) return;
            var a = device.Trim().ToUpperInvariant();
            if (!seen.Add(a)) return;
            string usedAs = owner.Length > 0 ? owner + (role.Length > 0 ? " - " + role : "") : role;
            list.Add(new Device { Station = station, Address = a, DataType = dataType, UsedAs = usedAs });
        }

        private static void Collect(Control c, int station, List<Device> list, HashSet<string> seen, string owner)
        {
            // every control on a station - and any other naming a device of it ("X12@2")
            int st = StationOf(c);
            if (st <= 0) return;
            _wanted = station;
            string own = owner;
            string text = (c.Text ?? "").Trim();
            string self = own.Length > 0 ? "" : (text.Length > 1 && text.Any(char.IsLetter) ? text : c.Name ?? "");
            if (own.Length == 0) own = self;

            // an axis's load-side encoder: its own, whether its Load Position column is shown or not
            var axis = c as AxisSettingHMI;
            if (axis != null && !string.IsNullOrWhiteSpace(axis.LoadSideEncoderAddress))
                Add(list, seen, st, axis.LoadSideEncoderAddress.Trim(), axis.PositionDataType == DataTypeEnum.Bin_32 ? DoubleWord : Word, own, "load-side encoder");

            var num = c as BaseNumericHMI;
            if (num != null)
            {
                Add(list, seen, st, PlcAddressHelper.OffsetAddress(num.Address, StaticOffset(num.AddressOffset)),
                    num.DataType == DataTypeEnum.Bin_32 ? DoubleWord : Word, own,
                    c is NumericInputHMI ? "setting" : "value");
                Add(list, seen, st, num.ComparedAddress, num.DataType == DataTypeEnum.Bin_32 ? DoubleWord : Word, own, "load-side encoder");
                return;
            }

            var btn = c as ButtonHMI;
            if (btn != null)
            {
                if (btn.BitActions != null)
                    foreach (var act in btn.BitActions.Where(x => x != null))
                        Add(list, seen, st, PlcAddressHelper.OffsetAddress(act.Address, StaticOffset(act.AddressOffset)),
                            Bit, own, "command");
                if (btn.WordActions != null)
                    foreach (var act in btn.WordActions.Where(x => x != null))
                        Add(list, seen, st, PlcAddressHelper.OffsetAddress(act.Address, StaticOffset(act.AddressOffset)),
                            Word, own, "word command");
                Add(list, seen, st, btn.RelayAddress, Bit, own, "relay");
                Add(list, seen, st, btn.OutputAddress, Bit, own, "output");
                Add(list, seen, st, btn.Lamp1Address, Bit, own, "sensor");
                Add(list, seen, st, btn.Lamp2Address, Bit, own, "sensor 2");
                Add(list, seen, st, PlcAddressHelper.OffsetAddress(btn.DisplayAddress, StaticOffset(btn.DisplayOffset)),
                    Word, own, "display");
                return;
            }

            var lamp = c as LampHMI;
            if (lamp != null)
            {
                Add(list, seen, st, PlcAddressHelper.OffsetAddress(lamp.Address, StaticOffset(lamp.AddressOffset)), Bit, own, "lamp");
                return;
            }

            var cb = c as CheckBoxHMI;
            if (cb != null)
            {
                Add(list, seen, st, cb.Address, Bit, own, "selection");
                if (!string.IsNullOrWhiteSpace(cb.AlertAddress)) Add(list, seen, st, cb.AlertAddress.Trim(), Bit, own, "request");
                return;
            }

            var cmb = c as ComboBoxHMI;
            if (cmb != null) { Add(list, seen, st, cmb.Address, Word, own, "choice"); return; }

            var cl = c as CheckListHMI;
            if (cl != null)
            {
                foreach (var a in cl.Addresses) Add(list, seen, st, a, Bit, own, "option");
                return;
            }

            var badge = c as AlarmHelpBadge;
            if (badge != null)
            {
                Add(list, seen, st, badge.Address, Bit, own, "error");
                foreach (var a in badge.CodeAddresses) Add(list, seen, st, a, Word, own, "drive code");
                return;
            }

            var bound = c as IPlcBoundControl;
            if (bound != null) Add(list, seen, st, bound.Address, Word, own, "value");
        }

        // ---- the whole program ----------------------------------------------

        /// <summary>
        /// Every page of every mounted machine, built off-screen once, with the
        /// devices each uses on one station - pages that use none are left out.
        /// A settings screen's tabs are pages of their own ("Machine Parameters /
        /// Axis Settings"). Slow on a big machine (every page is built), so it is
        /// an explicit action, never done in the background.
        /// </summary>
        public static List<PageDevices> FromProgram(int station)
        {
            var pages = new List<PageDevices>();
            foreach (var kv in MachineRegistry.AllScreens())
            {
                var screen = kv.Value;
                if (screen.CreateView == null) continue;
                // (a machine's axes' bar is on its pages: found there)
                if (screen.Role == ScreenRoles.HmiSettings || screen.Role == ScreenRoles.AxisBar) continue;
                string screenTitle = LanguageManager.GetString(screen.TitleKey ?? "", screen.TitleFallback ?? "");
                if (!kv.Key.IsMain) screenTitle = Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(kv.Key) + " / " + screenTitle;

                Control view = null;
                try
                {
                    view = screen.CreateView() as Control;
                    var settings = view as SettingsScreen;
                    if (settings != null)
                    {
                        foreach (var page in settings.AllPages)
                        {
                            Control pv = null;
                            try
                            {
                                pv = page.GetControl();
                                AddPage(pages, screenTitle + " / " + LanguageManager.GetString(page.TitleKey ?? "", page.TitleFallback ?? ""),
                                        FromControlAllTabs(pv, station));
                            }
                            catch (Exception ex) { AuditLogger.Log("device list skipped a page: " + ex.Message); }
                            finally { AlarmHelpLinks.Discard(pv); }
                        }
                    }
                    else AddPage(pages, screenTitle, FromControlAllTabs(view, station));
                }
                catch (Exception ex) { AuditLogger.Log("device list skipped '" + screenTitle + "': " + ex.Message); }
                finally { AlarmHelpLinks.Discard(view); }
            }

            // The alarm bits are devices of the program too - one of the first
            // things looked at when an alarm will not clear.
            var alarms = new PageDevices { Title = LanguageManager.GetString("Btn_Alarms", "Alarms") };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _wanted = station;
            foreach (var def in AlarmManager.GetAllDefinitions().Where(d => d != null && d.PlcNumber == station))
                Add(alarms.Devices, seen, station, def.Address,
                    def.DataType == DataTypeEnum.Bin_32 ? DoubleWord : IsBitDevice(def.Address) ? Bit : Word, "",
                    LanguageManager.GetString(def.TranslationKey ?? "", def.AlarmCode ?? ""));
            if (alarms.Devices.Count > 0) pages.Add(alarms);
            return pages;
        }

        /// <summary>Like FromControl, but every tab of a tab control: a page built off-screen shows none of them.</summary>
        private static List<Device> FromControlAllTabs(Control root, int station)
        {
            var list = new List<Device>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            WalkAll(root, station, list, seen, "");
            return list;
        }

        private static void WalkAll(Control c, int station, List<Device> list, HashSet<string> seen, string owner)
        {
            if (c == null) return;
            var axisRow = c as AxisSettingHMI;
            if (axisRow != null && axisRow.Controls.Count == 0) axisRow.BuildRow();
            string here = OwnerName(c);
            if (here.Length > 0) owner = here;
            Collect(c, station, list, seen, owner);
            foreach (Control child in c.Controls) WalkAll(child, station, list, seen, owner);
        }

        private static void AddPage(List<PageDevices> pages, string title, List<Device> devices)
        {
            if (devices == null || devices.Count == 0) return;
            var same = pages.FirstOrDefault(p => p.Title == title);
            if (same == null) pages.Add(new PageDevices { Title = title, Devices = devices });
            else
                foreach (var d in devices.Where(d => !same.Devices.Any(x => string.Equals(x.Address, d.Address, StringComparison.OrdinalIgnoreCase))))
                    same.Devices.Add(d);
        }

        /// <summary>True for a bit device (M100, X20, Y7...), false for a word (D100, W4...).</summary>
        public static bool IsBitDevice(string address)
        {
            var a = (address ?? "").Trim().ToUpperInvariant();
            foreach (var p in new[] { "SM", "M", "X", "Y", "L", "B", "F", "S" })
                if (a.StartsWith(p) && a.Length > p.Length && Uri.IsHexDigit(a[p.Length])) return !a.StartsWith("SD") && !a.StartsWith("SW");
            return false;
        }

        // ---- where a device is used ------------------------------------------
        //
        // The PLC Monitor's Find: every place in the program a device is used -
        // each USE, so a device on two buttons of one page is listed twice - with
        // the way back to it: the screen, the tab chosen on each tab strip on the
        // way down (PagePath), and the path of controls to the one that uses it
        // (ControlPath). Recorded to the outermost unit (a CylinderUnitHMI, an
        // axis unit or row) so the page can point at the whole unit.

        /// <summary>One use of a device on one page.</summary>
        public sealed class Use
        {
            public int Station;
            public string Address = "";
            public string DataType = Bit;
            /// <summary>What uses it: "Left Top Clamping - command".</summary>
            public string UsedAs = "";
            /// <summary>"Manual Movement / Cutting" (with the machine for a sub-machine).</summary>
            public string PageTitle = "";
            /// <summary>The screen, as registered by its machine; null for an alarm definition.</summary>
            public MachineScreen Screen;
            /// <summary>The page chosen on each nested tab strip of the screen, outermost first.</summary>
            public List<int> PagePath = new List<int>();
            /// <summary>From the page to the control: a child's Name, or "#index" when it has none.</summary>
            public List<string> ControlPath = new List<string>();
            /// <summary>True for an alarm definition's bit - listed, but not on a page to open.</summary>
            public bool IsAlarm;
        }

        private static readonly Dictionary<int, List<Use>> _uses = new Dictionary<int, List<Use>>();

        /// <summary>Forgets the uses found, so the next Find builds the pages again.</summary>
        public static void ClearUses() { _uses.Clear(); }

        /// <summary>
        /// Every use of every device of a station in the program. Built once per
        /// station (every page is built off-screen, which takes a moment) and kept:
        /// the pages do not change while the program runs.
        /// </summary>
        public static List<Use> UsesOf(int station)
        {
            List<Use> list;
            if (_uses.TryGetValue(station, out list)) return list;
            list = new List<Use>();
            foreach (var kv in MachineRegistry.AllScreens())
            {
                var screen = kv.Value;
                if (screen.CreateView == null || screen.Role == ScreenRoles.HmiSettings || screen.Role == ScreenRoles.AxisBar) continue;
                // The key's translation, else the screen's own wording - never the bare key.
                string title = LanguageManager.GetString(screen.TitleKey ?? "", screen.TitleFallback ?? "");
                if (!kv.Key.IsMain) title = Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(kv.Key) + " / " + title;
                Control view = null;
                try
                {
                    view = screen.CreateView() as Control;
                    UsesInView(list, station, screen, view, title, new List<int>(), 0);
                }
                catch (Exception ex) { AuditLogger.Log("find skipped '" + title + "': " + ex.Message); }
                finally { AlarmHelpLinks.Discard(view); }
            }
            foreach (var def in AlarmManager.GetAllDefinitions().Where(d => d != null && d.PlcNumber == station && !string.IsNullOrWhiteSpace(d.Address)))
                list.Add(new Use
                {
                    Station = station, Address = def.Address.Trim().ToUpperInvariant(), IsAlarm = true,
                    DataType = def.DataType == DataTypeEnum.Bin_32 ? DoubleWord : IsBitDevice(def.Address) ? Bit : Word,
                    PageTitle = LanguageManager.GetString("Btn_Alarms", "Alarms"),
                    UsedAs = LanguageManager.GetString(def.TranslationKey ?? "", def.AlarmCode ?? "")
                });
            _uses[station] = list;
            return list;
        }

        private static void UsesInView(List<Use> list, int station, MachineScreen screen, Control view,
                                       string title, List<int> pagePath, int depth)
        {
            if (view == null) return;
            var settings = view as SettingsScreen;
            if (settings != null && depth < 4)
            {
                for (int i = 0; i < settings.AllPages.Count; i++)
                {
                    var page = settings.AllPages[i];
                    Control pv = null;
                    try
                    {
                        pv = page.GetControl();
                        UsesInView(list, station, screen, pv,
                                   title + " / " + LanguageManager.GetString(page.TitleKey ?? "", page.TitleFallback ?? ""),
                                   new List<int>(pagePath) { i }, depth + 1);
                    }
                    catch (Exception ex) { AuditLogger.Log("find skipped a page: " + ex.Message); }
                    finally { AlarmHelpLinks.Discard(pv); }
                }
                return;
            }
            foreach (Control child in view.Controls)
                UsesWalk(list, station, screen, title, pagePath, child, PathOf(view, child), null, "");
        }

        private static void UsesWalk(List<Use> list, int station, MachineScreen screen, string title, List<int> pagePath,
                                     Control c, List<string> path, List<string> unitPath, string owner)
        {
            var axisRow = c as AxisSettingHMI;
            if (axisRow != null && axisRow.Controls.Count == 0) axisRow.BuildRow();
            string here = OwnerName(c);
            if (here.Length > 0) owner = here;
            // The OUTERMOST unit around the control is what the page points at.
            if (unitPath == null && (c is CylinderUnitHMI || c is AxisUnitHMI || c is AxisSettingHMI || c is CylinderRowHMI || c is CylinderEncoderUnitHMI)) unitPath = path;

            var found = new List<Device>();
            Collect(c, station, found, new HashSet<string>(StringComparer.OrdinalIgnoreCase), owner);
            foreach (var d in found)
                list.Add(new Use
                {
                    Station = station, Address = d.Address, DataType = d.DataType, UsedAs = d.UsedAs,
                    PageTitle = title, Screen = screen, PagePath = pagePath, ControlPath = unitPath ?? path
                });
            foreach (Control child in c.Controls)
                UsesWalk(list, station, screen, title, pagePath, child, new List<string>(path) { Step(c, child) }, unitPath, owner);
        }

        private static List<string> PathOf(Control parent, Control child) { return new List<string> { Step(parent, child) }; }

        /// <summary>A child's step in a path: its Name when that is unique among its siblings, else its position.</summary>
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

        /// <summary>
        /// Follows a ControlPath down a LIVE page and returns the control, choosing
        /// the tab of every tab control on the way so it is on show. Null when the
        /// page no longer has it.
        /// </summary>
        public static Control Locate(Control root, IList<string> path)
        {
            Control c = root;
            foreach (var step in path ?? new List<string>())
            {
                if (c == null) return null;
                Control next = null;
                if (step.StartsWith("#"))
                {
                    int i;
                    if (int.TryParse(step.Substring(1), out i) && i >= 0 && i < c.Controls.Count) next = c.Controls[i];
                }
                else
                    foreach (Control x in c.Controls) if (x.Name == step) { next = x; break; }
                if (next == null) return null;
                var tabs = c as TabControl;
                var page = next as TabPage;
                if (tabs != null && page != null && tabs.SelectedTab != page) tabs.SelectedTab = page;
                c = next;
            }
            return c;
        }

        /// <summary>Every device the station's PLC label list names, in its order.</summary>
        public static List<Device> FromLabelList(int station)
        {
            var list = new List<Device>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in PlcLabelTable.EntriesFor(station))
            {
                if (string.IsNullOrWhiteSpace(e.Address) || !seen.Add(e.Address)) continue;
                list.Add(new Device
                {
                    Station = station,
                    Address = e.Address,
                    DataType = e.IsBit ? Bit : e.Is32Bit ? DoubleWord : (string.IsNullOrWhiteSpace(e.DataType) ? Word : e.DataType)
                });
            }
            return list;
        }
    }
}
