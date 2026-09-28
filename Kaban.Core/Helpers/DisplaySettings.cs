using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// The MAGNIFYING FACTOR: how large the HMI draws its pages - text, buttons,
    /// fields, the menus - from 0.8 (smaller, more on the screen) to 1.5 (half
    /// as large again, for eyes that need it).
    ///
    /// 0.8 - 1.5, not 0.75 - 2: beyond these the pages - designed for the panel
    /// at 1 - no longer draw well (at 2 a page is mostly scrolling, and below 0.8
    /// the touch targets get too small), and this range covers the eyesight it
    /// is for.
    ///
    /// Three values, kept in Display.cfg beside the other HMI settings:
    ///   Panel  - the factor for everyone who has none of their own (HMI Settings >
    ///            User Settings);
    ///   User   - a user's own factor, optional; when that user signs in and it is
    ///            not the one on screen, the HMI offers to restart with it;
    ///   Start  - the factor the program starts with: the panel's, or the last
    ///            user's it restarted for (so the operator who signed in last finds
    ///            the screen as they left it after a power cut).
    ///
    /// Applied at START-UP only: every page is built and laid out once at its
    /// size, and changing it under a running page would re-lay out everything
    /// the operator is looking at. The restart is a few seconds; the PLC keeps
    /// running the machine meanwhile.
    /// </summary>
    public static class DisplaySettings
    {
        public const double Min = 0.8;
        public const double Max = 1.5;
        public const double Default = 1.0;

        /// <summary>Where the values are kept. Replaceable so a test can use its own file.</summary>
        public static string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Display.cfg");

        private static double _panel = Default, _start = Default;
        private static readonly Dictionary<string, double> _users = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        /// <summary>Within 0.8 .. 1.5, in steps of 0.01 (1%); anything unreadable is 1. A factor kept
        /// from before (up to 2) is brought within it.
        ///
        /// 1% steps: 5% (and 10% in the menu) moved a page by more than an
        /// operator wanted - a column that fitted at 1.37 did not at 1.40.</summary>
        public static double Clamp(double factor)
        {
            if (double.IsNaN(factor) || double.IsInfinity(factor) || factor <= 0) return Default;
            factor = Math.Round(factor * 100.0, MidpointRounding.AwayFromZero) / 100.0;
            return Math.Max(Min, Math.Min(Max, factor));
        }

        /// <summary>"137%" - how the factor is SHOWN (the file keeps "1.37").</summary>
        public static string Percent(double factor)
        {
            return ((int)Math.Round(Clamp(factor) * 100.0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>"1.25" - the way it is written and shown (invariant, two decimals).</summary>
        public static string Format(double factor) { return Clamp(factor).ToString("0.00", CultureInfo.InvariantCulture); }

        /// <summary>A factor typed in: "1.5", "1,5", "150%", "150" - or null when it is not one.</summary>
        public static double? Parse(string text)
        {
            var t = (text ?? "").Trim().Replace(',', '.');
            bool percent = t.EndsWith("%");
            if (percent) t = t.TrimEnd('%').Trim();
            double v;
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return null;
            // "137%" and a bare "137" are percentages; "1.37" is the factor.
            if (percent || v >= 10) v /= 100.0;
            if (v < Min - 0.001 || v > Max + 0.001) return null;
            return Clamp(v);
        }

        /// <summary>The factor for everyone with none of their own.</summary>
        public static double PanelFactor
        {
            get { Load(); return _panel; }
            set { Load(); _panel = Clamp(value); Save(); }
        }

        /// <summary>The factor the program starts with.</summary>
        public static double StartFactor
        {
            get { Load(); return _start; }
            set { Load(); _start = Clamp(value); Save(); }
        }

        /// <summary>A user's own factor, or null when they have none (they get what is on screen).</summary>
        public static double? UserFactor(string user)
        {
            Load();
            double f;
            return !string.IsNullOrWhiteSpace(user) && _users.TryGetValue(user.Trim(), out f) ? f : (double?)null;
        }

        /// <summary>Sets (or, with null, removes) a user's own factor.</summary>
        public static void SetUserFactor(string user, double? factor)
        {
            if (string.IsNullOrWhiteSpace(user)) return;
            Load();
            if (factor.HasValue) _users[user.Trim()] = Clamp(factor.Value);
            else _users.Remove(user.Trim());
            Save();
        }

        /// <summary>Reads the file again (tests; another program part changed it).</summary>
        public static void Reload() { _loaded = false; Load(); }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _panel = _start = Default;
            _users.Clear();
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var raw in File.ReadAllLines(SettingsPath))
                {
                    var line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0 || line.StartsWith("#")) continue;
                    string key = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim();
                    double v;
                    if (key.Equals("Panel", StringComparison.OrdinalIgnoreCase))
                    { if (TryNumber(value, out v)) _panel = Clamp(v); }
                    else if (key.Equals("Start", StringComparison.OrdinalIgnoreCase))
                    { if (TryNumber(value, out v)) _start = Clamp(v); }
                    else if (key.Equals("User", StringComparison.OrdinalIgnoreCase))
                    {
                        // User=<name>;<factor> - a name never holds ';' (see UserStore).
                        int semi = value.LastIndexOf(';');
                        if (semi > 0 && TryNumber(value.Substring(semi + 1), out v))
                            _users[value.Substring(0, semi).Trim()] = Clamp(v);
                    }
                }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the display settings: " + ex.Message); }
        }

        private static bool TryNumber(string text, out double v)
        {
            return double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        private static void Save()
        {
            try
            {
                var lines = new List<string>
                {
                    "# Kaban HMI magnifying factor (0.8 - 1.5). Panel: everyone's; Start: what the program starts with; User=<name>;<factor>",
                    "Panel=" + Format(_panel),
                    "Start=" + Format(_start)
                };
                lines.AddRange(_users.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => "User=" + kv.Key + ";" + Format(kv.Value)));
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                SafeFile.WriteAllText(SettingsPath, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(false));
            }
            catch (Exception ex) { AuditLogger.Log("could not save the display settings: " + ex.Message); }
        }
    }
}
