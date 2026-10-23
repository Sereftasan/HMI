using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// PLC CONNECTIONS: one page for everything about the controllers - it was two tabs, "PLC Connection"
    /// (what each controller is connected through) and "PLC Diagnostics" (whether it answers), with a table
    /// each and the same controllers in both. One row per controller: its driver, logical station and address
    /// (edited by an Admin, used from the next start), and beside them how it is doing now - state, last answer,
    /// drops today, the last error in words, the program version - and its data's Read and Write
    /// (StationConnectionControl). 23 October: no tabs under it any more - "We should remove Connection events
    /// and MX Component tabs on PLC Connections page, they are not useful at all ... put all the page right
    /// inside PLC Connections itself": the connection events are in Logs and Reports' log viewer.
    ///
    /// Opens for Maintenance: reading the state is for them too.
    /// </summary>
    public static class PlcConnectionScreen
    {
        public static UserControl Build()
        {
            return new StationConnectionControl { Dock = DockStyle.Fill };
        }
    }

    /// <summary>
    /// How each controller is doing - for the Controllers table (and the tests):
    /// the live state from ConnectionMonitor, the connection from the drivers
    /// actually running (not from the settings file, which takes effect at the
    /// next start), and how often it dropped today, counted from the log.
    /// </summary>
    public static class ControllerStatus
    {
        /// <summary>A controller's row: what the page shows for it.</summary>
        public sealed class Row
        {
            public int Station;
            public string Machine = "", Driver = "", Address = "", State = "", Last = "", Error = "", Version = "";
            /// <summary>The CPU's model, as the controller reported it (IPlcIdentity) - "" when unknown.</summary>
            public string Identity = "";
            public int Drops;
            public bool Live, Registered;
        }

        /// <summary>The rows as of now - public for the tests.</summary>
        public static List<Row> BuildRows(IDictionary<int, int> drops)
        {
            var rows = new List<Row>();
            var seen = new HashSet<int>();
            List<PlcConnectionSettings.Endpoint> endpoints;
            try { endpoints = PlcConnectionSettings.Endpoints(); }
            catch { endpoints = new List<PlcConnectionSettings.Endpoint>(); }

            foreach (var ep in endpoints)
            {
                if (!seen.Add(ep.StationId)) continue;
                rows.Add(RowFor(ep.StationId, ep.StationName, ep, drops));
            }
            // A station the program registered without an endpoint (a legacy exe).
            foreach (var st in ConnectionMonitor.Stations.ToList())
                if (seen.Add(st.StationId)) rows.Add(RowFor(st.StationId, st.Name, null, drops));
            return rows.OrderBy(r => r.Station).ToList();
        }

        public static Row RowFor(int station, string name, PlcConnectionSettings.Endpoint ep, IDictionary<int, int> drops)
        {
            var r = new Row { Station = station };
            var state = ConnectionMonitor.Get(station);
            r.Machine = !string.IsNullOrWhiteSpace(name) ? name : state.Name;
            int d; r.Drops = drops != null && drops.TryGetValue(station, out d) ? d : 0;

            var registered = PlcManager.Instance.GetDriverForMonitoring(station);
            var drv = Unwrapped(registered);
            r.Registered = drv != null;
            if (drv == null)
            {
                r.Driver = LanguageManager.GetString("Sta_NotRegistered", "not registered");
                r.State = LanguageManager.GetString("Diag_NotConnected", "not connected (none set)");
                return r;
            }

            var kind = KindOf(drv);
            r.Driver = kind != null ? kind.DisplayName : drv.GetType().Name;
            r.Address = AddressOf(drv, ep, kind);

            bool connected = false;
            try { connected = registered.IsConnected; } catch { }   // the wrapper's: it never blocks
            r.Live = connected && state.IsLive;
            var stale = state.StaleFor;
            if (r.Live) r.State = "● " + LanguageManager.GetString("Sta_Connected", "Connected");
            else if (!state.EverConnected)
                r.State = "● " + LanguageManager.GetString("Diag_NeverAnswered", "has not answered since the start");
            else
                r.State = "● " + string.Format(LanguageManager.GetString("Diag_NoAnswerFor", "no answer for {0}"), Span(stale));

            r.Last = state.EverConnected && stale != TimeSpan.MaxValue
                ? Kaban.Core.Helpers.ViewPreferences.TimeText(DateTime.Now.Subtract(stale)) +
                  (stale.TotalDays >= 1 ? "  (" + Kaban.Core.Helpers.ViewPreferences.ShortDateText(DateTime.Now.Subtract(stale)) + ")" : "")
                : "—";
            if (!r.Live && !string.IsNullOrEmpty(state.ErrorCode))
                r.Error = state.ErrorCode + "  " + ConnectionMonitor.DescribeCode(state.ErrorCode);
            else if (!r.Live) r.Error = state.LastError;
            r.Version = state.Version;
            var identity = drv as Kaban.Core.Interfaces.IPlcIdentity;
            try { r.Identity = identity != null ? (identity.Identity ?? "").Trim() : ""; } catch { r.Identity = ""; }
            // CONNECTED TO WHAT: "Connected - FX5UCPU (Version: 1.41)" - the CPU's
            // model and the program version, once the connection is up, beside
            // "Connected", where the eye already is.
            if (r.Live)
            {
                if (r.Identity.Length > 0) r.State += " - " + r.Identity;
                if (!string.IsNullOrEmpty(r.Version))
                    r.State += " (" + string.Format(LanguageManager.GetString("Diag_ProgramVersion", "Version: {0}"), r.Version) + ")";
            }
            return r;
        }

        /// <summary>
        /// The driver itself: a driver that could block the screen is registered
        /// inside a BoundedDriver (PlcManager.RegisterDriver), whose class name
        /// says nothing about the controller.
        /// </summary>
        public static IPlcDriver Unwrapped(IPlcDriver drv)
        {
            return PlcManager.Unwrap(drv);      // the logging and the bounding wrappers
        }

        /// <summary>"12 s", "4 min", "2 h 5 min" - how long a controller has been silent.</summary>
        public static string Span(TimeSpan t)
        {
            if (t == TimeSpan.MaxValue) return "—";
            if (t.TotalSeconds < 90) return Math.Max(0, (int)t.TotalSeconds) + " s";
            if (t.TotalMinutes < 90) return (int)t.TotalMinutes + " min";
            if (t.TotalHours < 48) return (int)t.TotalHours + " h " + t.Minutes + " min";
            return (int)t.TotalDays + " d";
        }

        /// <summary>The catalogue entry of a running driver, found by its class name.</summary>
        private static PlcDriverCatalog.DriverKind KindOf(IPlcDriver drv)
        {
            string tn = drv.GetType().Name;
            return PlcDriverCatalog.Kinds.FirstOrDefault(k => tn.IndexOf(k.Id, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string AddressOf(IPlcDriver drv, PlcConnectionSettings.Endpoint ep, PlcDriverCatalog.DriverKind kind)
        {
            // MX Component: the logical station the RUNNING driver opens.
            var p = drv.GetType().GetProperty("LogicalStation");
            if (p != null)
            {
                try { return LanguageManager.GetString("Diag_LogicalStation", "logical station") + " " + Convert.ToString(p.GetValue(drv, null)); }
                catch { }
            }
            if (kind != null && kind.UsesNetworkEndpoint && ep != null && PlcConnectionSettings.IsConfigured(ep))
                return PlcConnectionSettings.For(ep).Endpoint;
            return "";
        }

        /// <summary>
        /// How often each station was lost today, counted from the log's
        /// "station N lost" entries - read at most every 30 s.
        /// </summary>
        public static Dictionary<int, int> DropsToday()
        {
            var result = new Dictionary<int, int>();
            foreach (var e in AuditLogReader.Read(DateTime.Today, DateTime.Now))
            {
                if (e.Kind != AuditKind.Connection) continue;
                var m = System.Text.RegularExpressions.Regex.Match(e.Text, @"^station (\d+) lost");
                int n;
                if (m.Success && int.TryParse(m.Groups[1].Value, out n))
                {
                    int c; result.TryGetValue(n, out c); result[n] = c + 1;
                }
            }
            return result;
        }
    }
}
