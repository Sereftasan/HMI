using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// JSON as the dashboard's files use it: objects (keys kept in their order),
    /// arrays, strings, numbers, true / false / null. Small on purpose - the UI
    /// project carries no JSON package, and these files are a few kilobytes.
    /// Objects are <see cref="Dictionary{TKey,TValue}"/> of string to object,
    /// arrays <see cref="List{T}"/> of object, numbers double.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            int i = 0;
            var v = Value(text ?? "", ref i);
            Skip(text, ref i);
            if (i < text.Length) throw new FormatException("unexpected text after the JSON value at " + i);
            return v;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("the JSON ends too soon");
            char c = s[i];
            if (c == '{')
            {
                i++;
                var o = new Dictionary<string, object>(StringComparer.Ordinal);
                Skip(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return o; }
                while (true)
                {
                    Skip(s, ref i);
                    string key = Str(s, ref i);
                    Skip(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw new FormatException("':' expected at " + i);
                    i++;
                    o[key] = Value(s, ref i);
                    Skip(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; return o; }
                    throw new FormatException("',' or '}' expected at " + i);
                }
            }
            if (c == '[')
            {
                i++;
                var a = new List<object>();
                Skip(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return a; }
                while (true)
                {
                    a.Add(Value(s, ref i));
                    Skip(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; return a; }
                    throw new FormatException("',' or ']' expected at " + i);
                }
            }
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            double d;
            if (i == start || !double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw new FormatException("a value expected at " + start);
            return d;
        }

        private static string Str(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException("'\"' expected at " + i);
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("a string is not closed");
        }

        /// <summary>One value, compact ("{"start": "08:00", "end": "16:00"}").</summary>
        public static string Write(object v)
        {
            var sb = new StringBuilder();
            WriteTo(sb, v);
            return sb.ToString();
        }

        private static void WriteTo(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is string) { Quote(sb, (string)v); return; }
            if (v is double || v is float || v is decimal || v is int || v is long)
            {
                double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                sb.Append(Math.Abs(d % 1) < 1e-12 && Math.Abs(d) < 1e15 ? ((long)d).ToString(CultureInfo.InvariantCulture) : d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            var dict = v as IDictionary<string, object>;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    Quote(sb, kv.Key);
                    sb.Append(": ");
                    WriteTo(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            var list = v as System.Collections.IEnumerable;
            if (list != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in list)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    WriteTo(sb, item);
                }
                sb.Append(']');
                return;
            }
            Quote(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        private static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---- reading helpers -----------------------------------------------------------

        public static string Text(IDictionary<string, object> o, string key, string fallback = "")
        {
            object v;
            return o != null && o.TryGetValue(key, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : fallback;
        }

        public static bool Bool(IDictionary<string, object> o, string key, bool fallback)
        {
            object v;
            if (o == null || !o.TryGetValue(key, out v) || v == null) return fallback;
            if (v is bool) return (bool)v;
            if (v is double) return (double)v != 0;
            bool b;
            return bool.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out b) ? b : fallback;
        }

        public static double? Number(IDictionary<string, object> o, string key)
        {
            object v;
            if (o == null || !o.TryGetValue(key, out v) || v == null) return null;
            if (v is double) return (double)v;
            double d;
            return double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : (double?)null;
        }

        public static List<object> List(IDictionary<string, object> o, string key)
        {
            object v;
            return o != null && o.TryGetValue(key, out v) ? v as List<object> : null;
        }

        public static IDictionary<string, object> Object(IDictionary<string, object> o, string key)
        {
            object v;
            return o != null && o.TryGetValue(key, out v) ? v as IDictionary<string, object> : null;
        }
    }
}
