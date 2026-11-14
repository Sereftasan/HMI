using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// THE HMI'S OPTIONAL PARTS, switched on and off on HMI Settings > Features (30 October: "We need
    /// to make the Industry 4 service and touchscreen mode optional, and we need to add some buttons
    /// to active and passive them on HMI setting"). Both are off until an Admin switches them on:
    ///
    ///   Industry4Logging  the HMI takes the machines' logs from their PLCs (the Industry 4 service's
    ///                     job) and keeps its own log in the same database. Off: the Windows service
    ///                     logs, the HMI's log is in its files - as before 29 October.
    ///   MachineRecords    (5 November: "this same HMI program is gonna be deployed to the currently operating
    ///                     old machines as well as the new producing ones. So it should be selected at the HMI
    ///                     setting's option page") a new machine: the HMI takes the machine's details itself
    ///                     (MachineRecords - its cylinders' times, its axes' alarms and overloads, its inverters'
    ///                     faults and loads, why it stood still). Off: an older machine - its PLC logs its details
    ///                     (the records switched on its Options page). Only with Industry4Logging on.
    ///   TouchScreen       the screen may be used by touch: the 🖱 / 👆 button on the top bar and the
    ///                     "Screen used with" preference. Off: a mouse, neither shown.
    ///
    /// The panel's, for everyone - kept in Features.cfg beside the other HMI settings ("Key=On").
    /// </summary>
    public static class HmiFeatures
    {
        public const string Industry4Logging = "Industry4Logging";
        public const string MachineRecords = "MachineRecords";
        public const string TouchScreen = "TouchScreen";

        /// <summary>Every feature, in the order the page lists them.</summary>
        public static readonly IList<string> All = new[] { Industry4Logging, MachineRecords, TouchScreen };

        /// <summary>Where they are kept. Replaceable so a test can use its own file.</summary>
        public static string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Features.cfg");

        /// <summary>Raised when one is switched (on the thread that switched it).</summary>
        public static event EventHandler Changed;

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, bool> _on = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        /// <summary>The HMI takes the machines' logs from their PLCs and keeps its own log beside them.</summary>
        public static bool LoggingOn { get { return IsOn(Industry4Logging); } }

        /// <summary>A new machine: the HMI takes the machines' details itself - only while it does the Industry 4 logging.</summary>
        public static bool RecordsOn { get { return LoggingOn && IsOn(MachineRecords); } }

        /// <summary>The screen may be used by touch.</summary>
        public static bool TouchOn { get { return IsOn(TouchScreen); } }

        public static bool IsOn(string feature)
        {
            lock (Gate)
            {
                Load();
                bool on;
                return feature != null && _on.TryGetValue(feature, out on) && on;
            }
        }

        /// <summary>Switches a feature on or off - kept at once, written in the log, everyone told.</summary>
        public static void Set(string feature, bool on)
        {
            if (string.IsNullOrWhiteSpace(feature)) return;
            lock (Gate)
            {
                Load();
                bool was;
                if (_on.TryGetValue(feature, out was) && was == on) return;
                if (!_on.ContainsKey(feature) && !on) return;
                _on[feature] = on;
                Save();
            }
            AuditLogger.Log("feature " + feature + " switched " + (on ? "on" : "off") + ".");
            var h = Changed;
            if (h == null) return;
            foreach (EventHandler each in h.GetInvocationList())
            {
                try { each(null, EventArgs.Empty); }
                catch (Exception ex) { AuditLogger.Log("a feature switch could not be applied: " + ex.Message); }
            }
        }

        /// <summary>Reads the file again (tests).</summary>
        public static void Reload()
        {
            lock (Gate) { _loaded = false; Load(); }
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _on.Clear();
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var raw in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (line.Length == 0 || line.StartsWith("#", System.StringComparison.Ordinal) || eq <= 0) continue;
                    var key = line.Substring(0, eq).Trim();
                    var value = line.Substring(eq + 1).Trim();
                    _on[key] = value.Equals("On", StringComparison.OrdinalIgnoreCase) || value == "1" ||
                               value.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex) { AuditLogger.Log("Features.cfg could not be read: " + ex.Message); }
        }

        private static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var lines = new List<string> { "# The HMI's optional parts (HMI Settings > Features): On or Off" };
                lines.AddRange(_on.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Key + "=" + (kv.Value ? "On" : "Off")));
                SafeFile.WriteAllText(SettingsPath, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(false));
            }
            catch (Exception ex) { AuditLogger.Log("Features.cfg could not be written: " + ex.Message); }
        }
    }
}
