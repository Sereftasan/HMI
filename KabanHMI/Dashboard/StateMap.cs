using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// WHAT THE CODES OF THE LOG MEAN - the machine's state map (the Industry 4
    /// program's sample.json, shipped as Dashboard\StateMap.json) and the texts of
    /// its descriptions in each language (Translation_JSON.csv).
    ///
    /// Codes 0 to 5 are the MACHINE STATES; every other code is an EVENT; code 4
    /// (an error) is also an ALARM, its details being the error bits that were on.
    /// </summary>
    public sealed class StateMap
    {
        public sealed class Entry
        {
            public int Code;
            public string Description = "";
            public int? PreviousCode;
            public List<string> Parameters = new List<string>();
        }

        public const int Closed = 0, NotActive = 1, Active = 2, Pause = 3, Error = 4, Emergency = 5;
        public const int CuttingDone = 8;

        public static bool IsState(int code) { return code >= 0 && code <= 5; }

        /// <summary>The colours the old program gave the states.</summary>
        public static Color ColorOf(int code)
        {
            switch (code)
            {
                case Active: return ColorTranslator.FromHtml("#2ecc71");
                case NotActive: return ColorTranslator.FromHtml("#5C7BAA");
                case Pause: return ColorTranslator.FromHtml("#ffff00");
                case Error: return ColorTranslator.FromHtml("#f39c12");
                case Emergency: return ColorTranslator.FromHtml("#ff0000");
                case Closed: return ColorTranslator.FromHtml("#95a5a6");
            }
            return Color.SteelBlue;
        }

        /// <summary>The HMI's language codes and the columns of the translation files.</summary>
        public static readonly Dictionary<string, string> LanguageColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "EN", "English" },
            { "TR", "T\u00fcrk\u00e7e" },
            { "RU", "\u0420\u0443\u0441\u0441\u043a\u0438\u0439" },
            { "DE", "Deutsch" },
            { "IT", "Italiano" },
            { "ES", "Espa\u00f1ol" }
        };

        /// <summary>The column of the HMI's language ("TR" -> "T\u00fcrk\u00e7e"); English for a language the files do not have.</summary>
        public static string ColumnOf(string hmiLanguage)
        {
            string c;
            return hmiLanguage != null && LanguageColumns.TryGetValue(hmiLanguage, out c) ? c : "English";
        }

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

        /// <summary>language column -> reference text -> text.</summary>
        private readonly Dictionary<string, Dictionary<string, string>> _texts =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<Entry> Entries { get { return _entries.Values.OrderBy(e => e.Code); } }
        public int Count { get { return _entries.Count; } }

        public Entry Find(int code)
        {
            Entry e;
            return _entries.TryGetValue(code, out e) ? e : null;
        }

        /// <summary>The description as the map has it ("There is an error (1)"), or the code itself.</summary>
        public string RawDescription(int code)
        {
            var e = Find(code);
            return e != null && e.Description.Length > 0 ? e.Description : code.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A reference text in a language column; the text itself when it has none.</summary>
        public string Translate(string reference, string column)
        {
            if (string.IsNullOrEmpty(reference)) return reference ?? "";
            Dictionary<string, string> map;
            string t;
            if (column != null && _texts.TryGetValue(column, out map) && map.TryGetValue(reference, out t) && t.Length > 0)
                return t;
            return reference;
        }

        /// <summary>The translation table of one language (reference -> text); empty when the file lacks it.</summary>
        public Dictionary<string, string> TableOf(string column)
        {
            Dictionary<string, string> map;
            return column != null && _texts.TryGetValue(column, out map) ? map : new Dictionary<string, string>();
        }

        // ------------------------------------------------------------------ loading

        public static StateMap Load(string stateMapFile, string translationFile)
        {
            var m = new StateMap();
            if (!string.IsNullOrEmpty(stateMapFile) && File.Exists(stateMapFile))
                m.ReadMap(File.ReadAllText(stateMapFile, Encoding.UTF8));
            if (!string.IsNullOrEmpty(translationFile) && File.Exists(translationFile))
                m.ReadTranslations(File.ReadAllText(translationFile, Encoding.UTF8));
            return m;
        }

        public void ReadMap(string json)
        {
            var list = Json.Parse(json) as List<object>;
            if (list == null) return;
            foreach (var o in list)
            {
                var d = o as IDictionary<string, object>;
                var code = Json.Number(d, "StateCode");
                if (d == null || !code.HasValue) continue;
                var e = new Entry
                {
                    Code = (int)code.Value,
                    Description = Json.Text(d, "Description", ""),
                    PreviousCode = Json.Number(d, "PreviousCode").HasValue ? (int?)(int)Json.Number(d, "PreviousCode").Value : null
                };
                var ps = Json.List(d, "Parameters");
                if (ps != null)
                    foreach (var p in ps)
                        e.Parameters.Add(Json.Text(p as IDictionary<string, object>, "Description", ""));
                _entries[e.Code] = e;
            }
        }

        /// <summary>
        /// Translation_JSON.csv: "Reference;English;T\u00fcrk\u00e7e;..." - the heading
        /// names the columns; an empty cell leaves the reference text.
        /// </summary>
        public void ReadTranslations(string csv)
        {
            var lines = csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length == 0) return;
            var head = lines[0].TrimStart('\ufeff').Split(';').Select(h => h.Trim()).ToArray();
            int refCol = Array.FindIndex(head, h => h.Equals("Reference", StringComparison.OrdinalIgnoreCase));
            if (refCol < 0) refCol = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Trim().Length == 0) continue;
                var cells = lines[i].Split(';');
                if (cells.Length <= refCol) continue;
                string reference = cells[refCol];
                for (int c = 0; c < head.Length && c < cells.Length; c++)
                {
                    if (c == refCol || head[c].Length == 0) continue;
                    Dictionary<string, string> map;
                    if (!_texts.TryGetValue(head[c], out map)) _texts[head[c]] = map = new Dictionary<string, string>(StringComparer.Ordinal);
                    map[reference] = cells[c].Length > 0 ? cells[c] : reference;
                }
            }
        }
    }

    /// <summary>
    /// The time zone the log is shown in. The database keeps UTC; the settings name
    /// the zone the way the old program did ("Europe/Istanbul"), which Windows
    /// knows by another name ("Turkey Standard Time") - both are accepted.
    /// </summary>
    public static class DashboardTime
    {
        /// <summary>The zones offered in the settings, with their Windows names.</summary>
        public static readonly string[][] Zones =
        {
            new[] { "UTC", "UTC" },
            new[] { "Europe/Istanbul", "Turkey Standard Time" },
            new[] { "Europe/London", "GMT Standard Time" },
            new[] { "Europe/Lisbon", "GMT Standard Time" },
            new[] { "Europe/Berlin", "W. Europe Standard Time" },
            new[] { "Europe/Rome", "W. Europe Standard Time" },
            new[] { "Europe/Amsterdam", "W. Europe Standard Time" },
            new[] { "Europe/Vienna", "W. Europe Standard Time" },
            new[] { "Europe/Zurich", "W. Europe Standard Time" },
            new[] { "Europe/Paris", "Romance Standard Time" },
            new[] { "Europe/Madrid", "Romance Standard Time" },
            new[] { "Europe/Brussels", "Romance Standard Time" },
            new[] { "Europe/Warsaw", "Central European Standard Time" },
            new[] { "Europe/Prague", "Central Europe Standard Time" },
            new[] { "Europe/Budapest", "Central Europe Standard Time" },
            new[] { "Europe/Athens", "GTB Standard Time" },
            new[] { "Europe/Bucharest", "GTB Standard Time" },
            new[] { "Europe/Sofia", "FLE Standard Time" },
            new[] { "Europe/Kiev", "FLE Standard Time" },
            new[] { "Europe/Kyiv", "FLE Standard Time" },
            new[] { "Europe/Moscow", "Russian Standard Time" },
            new[] { "Africa/Cairo", "Egypt Standard Time" },
            new[] { "Asia/Riyadh", "Arab Standard Time" },
            new[] { "Asia/Tehran", "Iran Standard Time" },
            new[] { "Asia/Dubai", "Arabian Standard Time" },
            new[] { "Asia/Baku", "Azerbaijan Standard Time" },
            new[] { "Asia/Tbilisi", "Georgian Standard Time" },
            new[] { "Asia/Tashkent", "West Asia Standard Time" },
            new[] { "Asia/Almaty", "Central Asia Standard Time" },
            new[] { "Asia/Kolkata", "India Standard Time" },
            new[] { "Asia/Shanghai", "China Standard Time" },
            new[] { "Asia/Tokyo", "Tokyo Standard Time" },
            new[] { "Australia/Sydney", "AUS Eastern Standard Time" },
            new[] { "America/Sao_Paulo", "E. South America Standard Time" },
            new[] { "America/New_York", "Eastern Standard Time" },
            new[] { "America/Chicago", "Central Standard Time" },
            new[] { "America/Mexico_City", "Central Standard Time (Mexico)" },
            new[] { "America/Los_Angeles", "Pacific Standard Time" }
        };

        /// <summary>The zone of a name; the PC's own when the name is not known (never throws).</summary>
        public static TimeZoneInfo Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return TimeZoneInfo.Local;
            name = name.Trim();
            if (name.Equals("UTC", StringComparison.OrdinalIgnoreCase)) return TimeZoneInfo.Utc;
            try { return TimeZoneInfo.FindSystemTimeZoneById(name); } catch { }
            foreach (var z in Zones)
            {
                if (z[0].Equals(name, StringComparison.OrdinalIgnoreCase))
                    try { return TimeZoneInfo.FindSystemTimeZoneById(z[1]); } catch { }
                if (z[1].Equals(name, StringComparison.OrdinalIgnoreCase))
                    try { return TimeZoneInfo.FindSystemTimeZoneById(z[0]); } catch { }
            }
            return TimeZoneInfo.Local;
        }

        /// <summary>True when the name is a zone this PC knows.</summary>
        public static bool Known(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name.Trim().Equals("UTC", StringComparison.OrdinalIgnoreCase)) return true;
            var z = Find(name);
            return !ReferenceEquals(z, TimeZoneInfo.Local) || name.Trim() == TimeZoneInfo.Local.Id;
        }

        public static DateTime ToLocal(DateTime utc, TimeZoneInfo zone)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified), zone);
        }

        /// <summary>A wall-clock time of the zone in UTC (a time the clocks skip is moved on by the skip).</summary>
        public static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
        {
            var t = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(t)) t = t.AddHours(1);
            return TimeZoneInfo.ConvertTimeToUtc(t, zone);
        }

        /// <summary>Now in the zone, to the second.</summary>
        public static DateTime Now(TimeZoneInfo zone)
        {
            var n = ToLocal(DateTime.UtcNow, zone);
            return new DateTime(n.Year, n.Month, n.Day, n.Hour, n.Minute, n.Second);
        }
    }
}
