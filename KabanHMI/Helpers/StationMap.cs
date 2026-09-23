using System.Reflection;
using System.Windows.Forms;
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
            var prop = c.GetType().GetProperty("PlcNumber", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(int) && prop.CanRead && prop.CanWrite)
            {
                int local = (int)prop.GetValue(c, null);
                if (local >= 1)
                {
                    int host = ctx.Station(local);
                    if (host != local) prop.SetValue(c, host, null);
                }

                // A COMPOSITE passes its station down to its own parts. Walking into
                // them as well would translate them a second time - 1 to 3, then 3
                // through the map again.
                if (c is FunctionUnitHMI || c is AxisSettingHMI || c is AxisUnitHMI) return;
            }

            foreach (Control child in c.Controls) Walk(ctx, child);
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

