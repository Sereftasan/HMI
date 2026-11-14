using System.Collections;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;
using Kaban.UI.Settings;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// Gives a sub-machine's designer pages their HOST station numbers.
    ///
    /// THE BUG THIS FIXES
    /// ------------------
    /// A machine's pages are designed for the machine on its own: every control
    /// says PlcNumber = 1. Mounted under FA2010, H500's controller is station 3 -
    /// but nothing translated the pages, so H500's manual and parameter pages read
    /// and wrote FA2010's PLC 1. H500's alarms were translated (the alarm loader
    /// does it), so the pages and the alarms disagreed about which controller
    /// they belonged to, and operating a mounted H500 moved nothing on it.
    ///
    /// Every control is translated ONCE, as its page is built, through the same
    /// ctx.Station() the alarms use. A number the station map does not list is
    /// left as it is, exactly as the alarm loader does.
    /// </summary>
    public static class StationMap
    {
        public static T Apply<T>(IMachineMountContext ctx, T root) where T : Control
        {
            if (ctx == null || root == null || ctx.IsMainMachine) return root;
            Walk(ctx, root);
            return root;
        }

        private static void Walk(IMachineMountContext ctx, Control c)
        {
            MapConditions(ctx, c);
            var prop = c.GetType().GetProperty("PlcNumber", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(int) && prop.CanRead && prop.CanWrite)
            {
                int local = (int)prop.GetValue(c, null);
                if (local >= 1)
                {
                    int host = ctx.Station(local);
                    if (host != local) prop.SetValue(c, host, null);
                }
                // and a device it names on another of the machine's controllers ("X12@2")
                MapAddresses(ctx, c, 0);

                // A COMPOSITE passes its station down to its own parts. Walking into
                // them as well would translate them a second time - 1 to 3, then 3
                // through the map again.
                if (c is CylinderUnitHMI || c is AxisSettingHMI || c is AxisUnitHMI || c is CylinderRowHMI || c is CylinderEncoderUnitHMI) return;
            }

            foreach (Control child in c.Controls) Walk(ctx, child);
        }

        /// <summary>
        /// THE MACHINE'S OWN CONDITIONS, as this mounting numbered them (14 October):
        /// a page designed with the machine on its own names its first condition bit
        /// 16; mounted under FA2010 that bit is FA2010's. Every SystemCondition
        /// property of the control (VisibilityConditions, EnableConditions, a button's
        /// own...) - a composite passes them on to its parts itself.
        /// </summary>
        private static void MapConditions(IMachineMountContext ctx, Control c)
        {
            var mount = ctx as MachineRegistry.MountContext;
            if (mount == null) return;
            foreach (var p in c.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.PropertyType != typeof(Kaban.Core.Enums.SystemCondition) || !p.CanRead || !p.CanWrite || p.GetIndexParameters().Length > 0) continue;
                try
                {
                    var v = (Kaban.Core.Enums.SystemCondition)p.GetValue(c, null);
                    var m = mount.MapConditions(v);
                    if (m != v) p.SetValue(c, m, null);
                }
                catch { }
            }
        }

        /// <summary>
        /// A device of another of the machine's controllers ("X12@2", 12 October)
        /// names the machine's own PLC 2: given the host's number for it, as the
        /// control's PlcNumber is. Every "...Address" text of the control, of its
        /// actions and of its limits and triggers.
        /// </summary>
        private static void MapAddresses(IMachineMountContext ctx, object o, int depth)
        {
            if (o == null || depth > 2) return;
            foreach (var p in o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                if (p.PropertyType == typeof(string))
                {
                    if (!p.CanWrite || p.Name.IndexOf("Address", System.StringComparison.Ordinal) < 0) continue;
                    string value;
                    try { value = p.GetValue(o, null) as string; } catch { continue; }
                    string device; int local;
                    if (!PlcAddressHelper.TrySplitStation(value, out device, out local)) continue;
                    int host = ctx.Station(local);
                    if (host != local) try { p.SetValue(o, device + PlcAddressHelper.StationMark + host, null); } catch { }
                    continue;
                }
                var ns = p.PropertyType.Namespace ?? "";
                bool models = ns.StartsWith("Kaban.Core.Models", System.StringComparison.Ordinal);
                bool list = typeof(IList).IsAssignableFrom(p.PropertyType) && p.PropertyType.IsGenericType &&
                            (p.PropertyType.GetGenericArguments()[0].Namespace ?? "").StartsWith("Kaban.Core.Models", System.StringComparison.Ordinal);
                if (!models && !list) continue;
                object inner;
                try { inner = p.GetValue(o, null); } catch { continue; }
                if (list) { foreach (var item in (IList)inner ?? new object[0]) MapAddresses(ctx, item, depth + 1); }
                else MapAddresses(ctx, inner, depth + 1);
            }
        }
    }

    /// <summary>A module's page, translated to host stations when it is built.</summary>
    public sealed class StationMappedPage : ISettingsPage
    {
        private readonly ISettingsPage _inner;
        private readonly IMachineMountContext _ctx;

        public StationMappedPage(ISettingsPage inner, IMachineMountContext ctx)
        {
            _inner = inner;
            _ctx = ctx;
        }

        public string TitleKey { get { return _inner.TitleKey; } }
        public string TitleFallback { get { return _inner.TitleFallback; } }
        public Kaban.Core.Enums.SecurityLevel RequiredLevel { get { return _inner.RequiredLevel; } }

        public UserControl GetControl() { return StationMap.Apply(_ctx, _inner.GetControl()); }
    }
}

