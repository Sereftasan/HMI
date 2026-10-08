using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// HOW THE HMI SHOWS THINGS, as each user likes it (HMI Settings > View
    /// Preferences): where a field's caption sits, how a number, a date and a
    /// time are written, how large a table's rows and letters are, where a
    /// sign-in starts, whether a new alarm sounds, whether the attention marks
    /// blink, how soon a tooltip comes.
    ///
    /// Each one has a value for EVERYONE (the panel's) and, optionally, a user's
    /// own - taken when that user signs in, as their Screen scale is. Nothing
    /// here touches the machine: they are the screen's manners only.
    ///
    /// Kept in View.cfg beside the other HMI settings:
    ///   Panel.&lt;Key&gt;=&lt;value&gt;          everyone's
    ///   User=&lt;name&gt;;&lt;Key&gt;=&lt;value&gt;    a user's own (a name never holds ';' - see UserStore)
    /// </summary>
    public static class ViewPreferences
    {
        /// <summary>One preference: its key, the values it takes (the first is the default unless Default says otherwise), its group on the page.</summary>
        public sealed class Preference
        {
            public string Key = "";
            public string[] Values = new string[0];
            public string Default = "";
            /// <summary>The page's section: Captions, Numbers, Tables, SignIn.</summary>
            public string Group = "";
            /// <summary>Kept by the program itself (a remembered choice, a place) - not on the page.</summary>
            public bool Hidden;
        }

        // ---- the keys -----------------------------------------------------------
        /// <summary>Field captions AND axis names (Axis Settings, the Axis Monitor): one preference, the same look.</summary>
        public const string CaptionAlign = "CaptionAlign";          // Right | Left
        public const string DecimalSeparator = "DecimalSeparator";  // Windows | Point | Comma
        public const string DateFormat = "DateFormat";              // Iso | European | Us
        public const string TimeFormat = "TimeFormat";              // H24 | H12
        public const string TableRows = "TableRows";                // Compact | Normal | Large
        public const string TableFont = "TableFont";                // Small | Normal | Large
        public const string StartPage = "StartPage";                // Stay | Home | Last
        public const string AlarmSound = "AlarmSound";              // Off | On
        public const string Blinking = "Blinking";                  // On | Off
        public const string TooltipDelay = "TooltipDelay";          // Short | Normal | Long
        /// <summary>The simple motion values picked on the Axis Settings page (comma list of keys).</summary>
        public const string MotionColumns = "MotionColumns";
        /// <summary>Where a user was last (for StartPage = Last): key=value pairs, tab separated.</summary>
        public const string LastPlace = "LastPlace";
        /// <summary>The Axis Settings page's PLC axis items (comma list; "*" or empty: all).</summary>
        public const string AxisItems = "AxisItems";
        /// <summary>The Axis Monitor's PLC axis items (an Admin's choice; default: the torque beside the position).</summary>
        public const string MonitorItems = "MonitorItems";
        /// <summary>The Axis Monitor's Motion monitor values (comma list of keys).</summary>
        public const string MonitorMotion = "MonitorMotion";
        /// <summary>The axes shown on the Axis Settings pages (comma list of "name@station"; "*" or empty: all).</summary>
        public const string AxesShown = "AxesShown";
        /// <summary>The axes shown on the Axis Monitor (same form).</summary>
        public const string MonitorAxes = "MonitorAxes";
        /// <summary>The lists shown on the Inputs / Outputs side page ("station:I" / "station:O"; "*" or empty: all).</summary>
        public const string IoShown = "IoShown";
        /// <summary>The languages the notes page lists a text in (comma list of codes; empty: the screen's and English).</summary>
        public const string NoteLanguages = "NoteLanguages";

        public static readonly IList<Preference> All = new List<Preference>
        {
            P(CaptionAlign, "Captions", "Right", "Left"),
            P(DecimalSeparator, "Numbers", "Windows", "Point", "Comma"),
            P(DateFormat, "Numbers", "Iso", "European", "Us"),
            P(TimeFormat, "Numbers", "H24", "H12"),
            P(TableRows, "Tables", "Normal", "Compact", "Large"),
            P(TableFont, "Tables", "Normal", "Small", "Large"),
            P(StartPage, "SignIn", "Stay", "Home", "Last"),
            P(AlarmSound, "SignIn", "Off", "On"),
            P(Blinking, "SignIn", "On", "Off"),
            P(TooltipDelay, "SignIn", "Normal", "Short", "Long"),
            new Preference { Key = MotionColumns, Group = "", Hidden = true },
            new Preference { Key = LastPlace, Group = "", Hidden = true },
            new Preference { Key = AxisItems, Group = "", Hidden = true },
            new Preference { Key = MonitorItems, Group = "", Hidden = true, Default = "Torque" },
            new Preference { Key = MonitorMotion, Group = "", Hidden = true },
            new Preference { Key = AxesShown, Group = "", Hidden = true },
            new Preference { Key = MonitorAxes, Group = "", Hidden = true },
            new Preference { Key = IoShown, Group = "", Hidden = true },
            new Preference { Key = NoteLanguages, Group = "", Hidden = true },
            // A user's own units of measure ("Unit_Length" = inch): taken at sign-in by
            // UnitManager. Everyone's are UnitManager's own (Units.cfg), not kept here.
            new Preference { Key = "Unit_" + Enums.UnitType.Length, Group = "Units", Hidden = true },
            new Preference { Key = "Unit_" + Enums.UnitType.Weight, Group = "Units", Hidden = true },
            new Preference { Key = "Unit_" + Enums.UnitType.Temperature, Group = "Units", Hidden = true },
            new Preference { Key = "Unit_" + Enums.UnitType.Pressure, Group = "Units", Hidden = true },
            new Preference { Key = "Unit_" + Enums.UnitType.Time, Group = "Units", Hidden = true }
        }.AsReadOnly();

        private static Preference P(string key, string group, params string[] values)
        {
            return new Preference { Key = key, Group = group, Values = values, Default = values[0] };
        }

        public static Preference Find(string key)
        {
            return All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Where the values are kept. Replaceable so a test can use its own file.</summary>
        public static string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "View.cfg");

        /// <summary>Raised when what is in force changed: a value set, or another user signed in.</summary>
        public static event EventHandler Changed;

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, string> _panel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Dictionary<string, string>> _users =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        static ViewPreferences()
        {
            // A sign-in brings that user's own values (and a sign-out everyone's).
            SecurityManager.UserChanged += (s, e) => Raise();
        }

        /// <summary>The user whose own values are in force: the one signed in, or none.</summary>
        public static string ActiveUser
        {
            get
            {
                return SecurityManager.CurrentLevel != Enums.SecurityLevel.None && !string.IsNullOrWhiteSpace(SecurityManager.CurrentUser)
                    ? SecurityManager.CurrentUser.Trim() : null;
            }
        }

        /// <summary>The value in force: the signed-in user's own, else everyone's, else the default.</summary>
        public static string Get(string key)
        {
            // Asked many times a scan (the blink, every number): kept until something changes.
            var user = ActiveUser;
            string v;
            lock (Gate)
            {
                if (!string.Equals(_effectiveUser, user, StringComparison.OrdinalIgnoreCase)) { _effective.Clear(); _effectiveUser = user; }
                if (_effective.TryGetValue(key, out v)) return v;
            }
            var own = user != null ? UserValue(user, key) : null;
            v = own ?? PanelValue(key);
            lock (Gate) { if (string.Equals(_effectiveUser, user, StringComparison.OrdinalIgnoreCase)) _effective[key] = v; }
            return v;
        }

        private static readonly Dictionary<string, string> _effective = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static string _effectiveUser;

        /// <summary>Everyone's value (the default when none was set).</summary>
        public static string PanelValue(string key)
        {
            lock (Gate)
            {
                Load();
                string v;
                if (_panel.TryGetValue(key, out v) && Valid(key, v)) return v;
            }
            var p = Find(key);
            return p != null ? p.Default : "";
        }

        /// <summary>A user's own value, or null when they take everyone's.</summary>
        public static string UserValue(string user, string key)
        {
            if (string.IsNullOrWhiteSpace(user)) return null;
            lock (Gate)
            {
                Load();
                Dictionary<string, string> own;
                string v;
                return _users.TryGetValue(user.Trim(), out own) && own.TryGetValue(key, out v) && Valid(key, v) ? v : null;
            }
        }

        /// <summary>Sets everyone's value (null: back to the default).</summary>
        public static void SetPanel(string key, string value) { SetMany(null, new Dictionary<string, string> { { key, value } }); }

        /// <summary>Sets a user's own value (null: they take everyone's).</summary>
        public static void SetUser(string user, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(user)) return;
            SetMany(user, new Dictionary<string, string> { { key, value } });
        }

        /// <summary>
        /// Several values at once - one write, one Changed: for <paramref name="user"/>
        /// (null: everyone's). A null value removes it (the default / everyone's).
        /// </summary>
        public static void SetMany(string user, IDictionary<string, string> values)
        {
            if (values == null || values.Count == 0) return;
            lock (Gate)
            {
                Load();
                Dictionary<string, string> target;
                if (user == null) target = _panel;
                else if (!_users.TryGetValue(user.Trim(), out target))
                    _users[user.Trim()] = target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in values)
                {
                    if (kv.Value == null || !Valid(kv.Key, kv.Value)) target.Remove(kv.Key);
                    else target[kv.Key] = Clean(kv.Value);
                }
                if (user != null && target.Count == 0) _users.Remove(user.Trim());
                Save();
            }
            Raise();
        }

        /// <summary>A user deleted: their own values go with them.</summary>
        public static void RemoveUser(string user)
        {
            if (string.IsNullOrWhiteSpace(user)) return;
            lock (Gate)
            {
                Load();
                if (!_users.Remove(user.Trim())) return;
                Save();
            }
            Raise();
        }

        /// <summary>The users who have values of their own.</summary>
        public static IList<string> UsersWithOwn()
        {
            lock (Gate) { Load(); return _users.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList(); }
        }

        /// <summary>Reads the file again (tests; another part of the program changed it).</summary>
        public static void Reload()
        {
            lock (Gate) { _loaded = false; Load(); }
            Raise();
        }

        /// <summary>Tells everyone that what is in force may have changed.</summary>
        public static void Raise()
        {
            var h = Changed;
            if (h == null) return;
            foreach (EventHandler each in h.GetInvocationList())
            {
                try { each(null, EventArgs.Empty); }
                catch (Exception ex) { AuditLogger.Log("a view preference could not be applied: " + ex.Message); }
            }
        }

        // ---- what they mean ---------------------------------------------------------

        public static bool CaptionsLeft { get { return Get(CaptionAlign) == "Left"; } }
        /// <summary>The axis names follow the captions (one preference since 5 October).</summary>
        public static bool AxisNamesLeft { get { return CaptionsLeft; } }
        public static bool BlinkingOn { get { return Get(Blinking) != "Off"; } }
        public static bool AlarmSoundOn { get { return Get(AlarmSound) == "On"; } }

        /// <summary>How a number is written on screen: the decimal separator chosen (or Windows').</summary>
        public static NumberFormatInfo NumberFormat
        {
            get
            {
                switch (Get(DecimalSeparator))
                {
                    case "Point": return NumberFormatInfo.InvariantInfo;
                    case "Comma": return CommaFormat;
                    default: return CultureInfo.CurrentCulture.NumberFormat;
                }
            }
        }

        private static readonly NumberFormatInfo CommaFormat = MakeComma();
        private static NumberFormatInfo MakeComma()
        {
            var nf = (NumberFormatInfo)NumberFormatInfo.InvariantInfo.Clone();
            nf.NumberDecimalSeparator = ",";
            nf.NumberGroupSeparator = ".";
            return NumberFormatInfo.ReadOnly(nf);
        }

        /// <summary>The decimal separator on screen: "." or ",".</summary>
        public static string DecimalMark { get { return NumberFormat.NumberDecimalSeparator; } }

        /// <summary>"2026-10-02", "02.10.2026" or "10/02/2026".</summary>
        public static string DatePattern { get { return DatePatternOf(Get(DateFormat)); } }

        /// <summary>The pattern of a Date value (Iso, European, Us) - the page's preview draws a value not yet in force.</summary>
        public static string DatePatternOf(string value)
        {
            switch (value)
            {
                case "European": return "dd.MM.yyyy";
                case "Us": return "MM'/'dd'/'yyyy";
                default: return "yyyy-MM-dd";
            }
        }

        /// <summary>The day and month only: "10-02", "02.10" or "10/02".</summary>
        public static string ShortDatePattern
        {
            get
            {
                switch (Get(DateFormat))
                {
                    case "European": return "dd.MM";
                    case "Us": return "MM'/'dd";
                    default: return "MM-dd";
                }
            }
        }

        /// <summary>
        /// "HH:mm:ss" or "h:mm:ss tt" - without the seconds when not wanted, with
        /// <paramref name="fraction"/> digits of the second after them.
        /// </summary>
        public static string TimePattern(bool seconds = true, int fraction = 0) { return TimePatternOf(Get(TimeFormat), seconds, fraction); }

        /// <summary>The pattern of a Time value (H24, H12).</summary>
        public static string TimePatternOf(string value, bool seconds = true, int fraction = 0)
        {
            bool h12 = value == "H12";
            return (h12 ? "h:mm" : "HH:mm") + (seconds ? ":ss" + (fraction > 0 ? "." + new string('f', Math.Min(fraction, 7)) : "") : "") + (h12 ? " tt" : "");
        }

        public static string DateTimePattern(bool seconds = true, int fraction = 0) { return DatePattern + " " + TimePattern(seconds, fraction); }

        public static string DateText(DateTime t) { return t.ToString(DatePattern, CultureInfo.InvariantCulture); }
        public static string ShortDateText(DateTime t) { return t.ToString(ShortDatePattern, CultureInfo.InvariantCulture); }
        public static string TimeText(DateTime t, bool seconds = true, int fraction = 0) { return t.ToString(TimePattern(seconds, fraction), CultureInfo.InvariantCulture); }
        public static string DateTimeText(DateTime t, bool seconds = true, int fraction = 0) { return t.ToString(DateTimePattern(seconds, fraction), CultureInfo.InvariantCulture); }

        /// <summary>A table's row height at factor 1: 22, 26 or 32 pixels.</summary>
        public static int TableRowHeight { get { return RowHeightOf(Get(TableRows)); } }

        public static int RowHeightOf(string value)
        {
            switch (value) { case "Compact": return 22; case "Large": return 32; default: return 26; }
        }

        /// <summary>A table's letters, in points: 8.5, 9.5 (the program's body) or 11.</summary>
        public static float TableFontSize { get { return FontSizeOf(Get(TableFont)); } }

        public static float FontSizeOf(string value)
        {
            switch (value) { case "Small": return 8.5f; case "Large": return 11f; default: return 9.5f; }
        }

        /// <summary>How soon a tooltip comes, in milliseconds: 400, 1000 or 2000.</summary>
        public static int TooltipDelayMs { get { return DelayOf(Get(TooltipDelay)); } }

        public static int DelayOf(string value)
        {
            switch (value) { case "Short": return 400; case "Long": return 2000; default: return 1000; }
        }

        /// <summary>The simple motion values picked on the Axis Settings page (their keys).</summary>
        public static IList<string> MotionColumnKeys { get { return ListOf(MotionColumns); } }

        /// <summary>Keeps the pick: as the signed-in user's own, or everyone's when nobody is signed in.</summary>
        public static void SetMotionColumnKeys(IEnumerable<string> keys) { SetList(MotionColumns, keys); }

        /// <summary>A kept list of keys (the Motion monitor's values).</summary>
        public static IList<string> ListOf(string key)
        {
            return (Get(key) ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(k => k.Trim()).Where(k => k.Length > 0 && k != "*").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Keeps a list of keys: the signed-in user's own, or everyone's when nobody is signed in.</summary>
        public static void SetList(string key, IEnumerable<string> keys)
        {
            var text = string.Join(",", (keys ?? new string[0]).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()));
            if (ActiveUser != null) SetUser(ActiveUser, key, text);       // "" = their own: none
            else SetPanel(key, text.Length > 0 ? text : null);
        }

        /// <summary>
        /// A pick of items where nothing ticked means ALL (the PLC axis items):
        /// null for all, else the keys ticked.
        /// </summary>
        public static ISet<string> PickOf(string key)
        {
            var v = (Get(key) ?? "").Trim();
            if (v.Length == 0 || v == "*") return null;
            return new HashSet<string>(ListOf(key), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Keeps such a pick ("*": all - none ticked).</summary>
        public static void SetPick(string key, IEnumerable<string> keys)
        {
            var text = string.Join(",", (keys ?? new string[0]).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()));
            if (text.Length == 0) text = "*";
            if (ActiveUser != null) SetUser(ActiveUser, key, text);
            else SetPanel(key, text);
        }

        /// <summary>Where a user was last (page, tabs, side page), or an empty set.</summary>
        public static IDictionary<string, string> UserPlace(string user)
        {
            var d = new Dictionary<string, string>();
            var text = UserValue(user, LastPlace);
            if (string.IsNullOrEmpty(text)) return d;
            foreach (var part in text.Split('\t'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0) d[part.Substring(0, eq)] = part.Substring(eq + 1);
            }
            return d;
        }

        /// <summary>Keeps where a user is - quietly: nothing on screen depends on it.</summary>
        public static void SetUserPlace(string user, IDictionary<string, string> place)
        {
            if (string.IsNullOrWhiteSpace(user) || place == null) return;
            var text = string.Join("\t", place.Where(kv => !string.IsNullOrEmpty(kv.Value))
                                              .Select(kv => kv.Key + "=" + kv.Value.Replace("\t", " ")));
            lock (Gate)
            {
                Load();
                Dictionary<string, string> own;
                if (!_users.TryGetValue(user.Trim(), out own))
                    _users[user.Trim()] = own = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string before;
                if (own.TryGetValue(LastPlace, out before) && before == text) return;
                if (text.Length == 0) own.Remove(LastPlace); else own[LastPlace] = text;
                Save();
            }
        }

        // ---- the file ------------------------------------------------------------------

        private static bool Valid(string key, string value)
        {
            if (value == null) return false;
            var p = Find(key);
            if (p == null) return false;
            if (p.Values.Length == 0) return true;          // free text (a list, a place)
            return p.Values.Contains(value);
        }

        private static string Clean(string value) { return value.Replace("\r", " ").Replace("\n", " "); }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _effective.Clear();
            _panel.Clear();
            _users.Clear();
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var raw in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    var line = raw.TrimEnd('\r');
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("Panel.", StringComparison.OrdinalIgnoreCase))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 6) continue;
                        string key = line.Substring(6, eq - 6).Trim(), value = line.Substring(eq + 1).Trim();
                        if (Valid(key, value)) _panel[key] = value;
                    }
                    else if (line.StartsWith("User=", StringComparison.OrdinalIgnoreCase))
                    {
                        var rest = line.Substring(5);
                        int semi = rest.IndexOf(';');
                        if (semi <= 0) continue;
                        string user = rest.Substring(0, semi).Trim(), pair = rest.Substring(semi + 1);
                        int eq = pair.IndexOf('=');
                        if (eq <= 0 || user.Length == 0) continue;
                        string key = pair.Substring(0, eq).Trim(), value = pair.Substring(eq + 1);
                        if (!Valid(key, value)) continue;
                        Dictionary<string, string> own;
                        if (!_users.TryGetValue(user, out own))
                            _users[user] = own = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        own[key] = value;
                    }
                }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the view preferences: " + ex.Message); }
        }

        private static void Save()
        {
            _effective.Clear();
            try
            {
                var lines = new List<string>
                {
                    "# Kaban HMI view preferences (HMI Settings > View Preferences).",
                    "# Panel.<Key>=<value>: everyone's; User=<name>;<Key>=<value>: a user's own, taken at their sign-in."
                };
                lines.AddRange(_panel.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => "Panel." + kv.Key + "=" + kv.Value));
                foreach (var u in _users.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                    lines.AddRange(u.Value.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => "User=" + u.Key + ";" + kv.Key + "=" + kv.Value));
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                SafeFile.WriteAllText(SettingsPath, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(false));
            }
            catch (Exception ex) { AuditLogger.Log("could not save the view preferences: " + ex.Message); }
        }
    }
}
