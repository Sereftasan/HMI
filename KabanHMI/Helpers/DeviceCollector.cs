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
            var fu = c as FunctionUnitHMI;
            if (fu != null) return Tr(fu.TitleTextKey);
            var ax = c as AxisSettingHMI;
            if (ax != null) return Tr(ax.AxisNameKey);
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

        private static void Add(List<Device> list, HashSet<string> seen, int station, string address,
                                string dataType, string owner, string role)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            var a = address.Trim().ToUpperInvariant();
            if (!seen.Add(a)) return;
            string usedAs = owner.Length > 0 ? owner + (role.Length > 0 ? " - " + role : "") : role;
            list.Add(new Device { Station = station, Address = a, DataType = dataType, UsedAs = usedAs });
        }

        private static void Collect(Control c, int station, List<Device> list, HashSet<string> seen, string owner)
        {
            int st = StationOf(c);
            if (st != station) return;
            string own = owner;
            string text = (c.Text ?? "").Trim();
            string self = own.Length > 0 ? "" : (text.Length > 1 && text.Any(char.IsLetter) ? text : c.Name ?? "");
            if (own.Length == 0) own = self;

            var num = c as BaseNumericHMI;
            if (num != null)
            {
                Add(list, seen, st, PlcAddressHelper.OffsetAddress(num.Address, StaticOffset(num.AddressOffset)),
                    num.DataType == DataTypeEnum.Bin_32 ? DoubleWord : Word, own,
                    c is NumericInputHMI ? "setting" : "value");
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
            if (cb != null) { Add(list, seen, st, cb.Address, Bit, own, "selection"); return; }

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
                foreach (var a in badge.ExtraAddresses) Add(list, seen, st, a, Word, own, "drive code");
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
                if (screen.Role == ScreenRoles.HmiSettings) continue;
                string screenTitle = Tr(screen.TitleKey).Length > 0 ? Tr(screen.TitleKey) : screen.TitleFallback;
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
