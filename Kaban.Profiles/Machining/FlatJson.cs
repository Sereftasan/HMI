using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Kaban.Profiles.Machining
{
    /// <summary>
    /// The macros' parameters are one flat JSON object ({"length_mm":30,"width_mm":5,
    /// "path":"LINE"}): numbers and strings, nothing nested. This reads and writes
    /// exactly that, keeping the keys in their order.
    /// </summary>
    public static class FlatJson
    {
        public static List<KeyValuePair<string, object>> Parse(string json)
        {
            var list = new List<KeyValuePair<string, object>>();
            var s = (json ?? "").Trim();
            if (s.Length < 2 || s[0] != '{') return list;
            int i = 1;
            while (i < s.Length)
            {
                Skip(s, ref i);
                if (i >= s.Length || s[i] == '}') break;
                if (s[i] == ',') { i++; continue; }
                string key = ReadString(s, ref i);
                Skip(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                Skip(s, ref i);
                object value;
                if (i < s.Length && s[i] == '"') value = ReadString(s, ref i);
                else
                {
                    int start = i;
                    while (i < s.Length && s[i] != ',' && s[i] != '}') i++;
                    var raw = s.Substring(start, i - start).Trim();
                    double d;
                    if (raw == "true") value = 1.0;
                    else if (raw == "false") value = 0.0;
                    else if (raw == "null") value = null;
                    else if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) value = d;
                    else value = raw;
                }
                if (key != null) list.Add(new KeyValuePair<string, object>(key, value));
            }
            return list;
        }

        public static Dictionary<string, object> ParseDictionary(string json)
        {
            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in Parse(json)) d[kv.Key] = kv.Value;
            return d;
        }

        public static string Write(IEnumerable<KeyValuePair<string, object>> values)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kv in values)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Escape(kv.Key)).Append("\":");
                if (kv.Value == null) sb.Append("null");
                else if (kv.Value is double || kv.Value is float || kv.Value is int || kv.Value is long || kv.Value is decimal)
                    sb.Append(Convert.ToDouble(kv.Value, CultureInfo.InvariantCulture).ToString("0.######", CultureInfo.InvariantCulture));
                else sb.Append('"').Append(Escape(Convert.ToString(kv.Value, CultureInfo.InvariantCulture))).Append('"');
            }
            return sb.Append('}').ToString();
        }

        public static double? Number(IDictionary<string, object> d, string key)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return null;
            if (v is double) return (double)v;
            double x;
            return double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out x) ? x : (double?)null;
        }

        public static string Text(IDictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;
        }

        private static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    switch (s[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 < s.Length) { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                            break;
                        default: sb.Append(s[i]); break;
                    }
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }

        private static string Escape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }
    }
}
