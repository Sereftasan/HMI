using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Kaban.Core.Helpers;

namespace Kaban.UI.Forms
{
    // ======================================================================
    // THE PLC MONITOR'S VALUES: data types, display formats, arrays
    // ----------------------------------------------------------------------
    // Data types as GX Works names them - Bit, Word and Double Word, signed or
    // unsigned, Float (single precision) and String - each shown in the row's
    // DISPLAY FORMAT (decimal, hexadecimal or binary for the whole numbers) and
    // written back from what is typed in that format.
    //
    // AN ARRAY IS ONE ITEM: "D100, 10 of them" is one row that expands to its
    // ten elements (D100 ... D109) and collapses to one line again. The
    // elements are rows of the list too - so everything that works on a row
    // (reading, writing a value, finding a device) works on them - but they
    // are never stored: they are made from the array's first address, its data
    // type and its count, and follow their array wherever it is moved, copied,
    // sorted or pasted.
    // ======================================================================
    public partial class SimulationMonitorForm
    {
        public const string FormatDecimal = "Decimal", FormatHex = "Hexadecimal", FormatBinary = "Binary";
        public static readonly string[] DisplayFormats = { FormatDecimal, FormatHex, FormatBinary };

        internal static bool IsFloat(string dataType)
        {
            var d = (dataType ?? "").ToLowerInvariant();
            return (d.Contains("float") || d.Contains("real") || d.Contains("single precision")) && !d.Contains("double precision");
        }

        internal static bool IsString(string dataType)
        {
            return (dataType ?? "").TrimStart().StartsWith("string", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBit(string dataType)
        {
            var d = (dataType ?? "").Trim();
            return d.Length == 0 || d.Equals("Bit", StringComparison.OrdinalIgnoreCase) || d.Equals("Bool", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A string's length in characters: "String(32)" says so itself; otherwise the row's own.</summary>
        internal static int StringChars(string dataType, int rowLength)
        {
            var m = System.Text.RegularExpressions.Regex.Match(dataType ?? "", @"\((\d+)\)");
            int n;
            if (m.Success && int.TryParse(m.Groups[1].Value, out n) && n > 0) return n;
            return Math.Max(1, rowLength);
        }

        /// <summary>How many words a value of this type takes: 2 for a double word or a float, ceil(chars / 2) for a string.</summary>
        internal static int WordCount(string dataType, int stringLength)
        {
            if (IsString(dataType)) return (StringChars(dataType, stringLength) + 1) / 2;
            if (IsDoubleWord(dataType) || IsFloat(dataType)) return 2;
            return 1;
        }

        /// <summary>Every device a row reads: its address and, for a wider value, the words after it.</summary>
        internal static IEnumerable<string> WordsOf(WatchRow r)
        {
            if (r == null || !r.IsDevice) yield break;
            string a = r.Address;
            yield return a;
            if (IsBit(r.EffectiveDataType)) yield break;
            int n = WordCount(r.EffectiveDataType, r.EffectiveStringLength);
            for (int i = 1; i < n; i++) { a = PlcAddressHelper.GetNextAddress(a); yield return a; }
        }

        /// <summary>The words of a row as read (false when any is missing).</summary>
        private static bool TryWords(MonitorTab mon, WatchRow r, out int[] words)
        {
            var list = WordsOf(r).ToList();
            words = new int[list.Count];
            for (int i = 0; i < list.Count; i++)
                if (!mon.Values.TryGetValue(list[i], out words[i])) return false;
            return true;
        }

        /// <summary>A whole number in the row's display format.</summary>
        internal static string FormatInteger(long v, int bits, string format)
        {
            ulong mask = bits >= 64 ? ulong.MaxValue : (1UL << bits) - 1;
            ulong raw = (ulong)v & mask;
            if (format == FormatHex)
                return "0x" + raw.ToString(bits > 16 ? "X8" : "X4", CultureInfo.InvariantCulture);
            if (format == FormatBinary)
            {
                var s = Convert.ToString((long)raw, 2).PadLeft(bits, '0');
                var sb = new StringBuilder();
                for (int i = 0; i < s.Length; i++) { if (i > 0 && i % 4 == 0) sb.Append(' '); sb.Append(s[i]); }
                return sb.ToString();
            }
            return v.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Four bytes of two words, low word first, as the PLC holds a single-precision float.</summary>
        internal static float ToFloat(int low, int high)
        {
            uint raw = ((uint)(high & 0xFFFF) << 16) | (uint)(low & 0xFFFF);
            return BitConverter.ToSingle(BitConverter.GetBytes(raw), 0);
        }

        internal static void FromFloat(float f, out int low, out int high)
        {
            uint raw = BitConverter.ToUInt32(BitConverter.GetBytes(f), 0);
            low = (int)(raw & 0xFFFF);
            high = (int)(raw >> 16);
        }

        /// <summary>Words to text: two characters a word, the low byte first, up to the first NUL.</summary>
        internal static string ToText(int[] words, int chars)
        {
            var bytes = new List<byte>();
            foreach (var w in words) { bytes.Add((byte)(w & 0xFF)); bytes.Add((byte)((w >> 8) & 0xFF)); }
            int n = Math.Min(chars, bytes.Count);
            int end = bytes.IndexOf(0);
            if (end >= 0 && end < n) n = end;
            return Encoding.GetEncoding(1252).GetString(bytes.Take(n).ToArray());
        }

        internal static int[] FromText(string text, int chars)
        {
            var bytes = Encoding.GetEncoding(1252).GetBytes(text ?? "");
            int words = (chars + 1) / 2;
            var result = new int[words];
            for (int i = 0; i < words; i++)
            {
                int lo = 2 * i < bytes.Length && 2 * i < chars ? bytes[2 * i] : 0;
                int hi = 2 * i + 1 < bytes.Length && 2 * i + 1 < chars ? bytes[2 * i + 1] : 0;
                result[i] = lo | (hi << 8);
            }
            return result;
        }

        /// <summary>The row's value as shown - in its type and display format - or null when not read yet.</summary>
        internal static string FormatValue(MonitorTab mon, WatchRow r, out bool nonZero)
        {
            nonZero = false;
            if (r == null || !r.IsDevice) return null;
            if (r.IsArray)
            {
                // AN ARRAY, FOLDED: its first values, enough to see it change.
                var parts = new List<string>();
                bool any = false;
                foreach (var e in (r.Elements ?? new List<WatchRow>()).Take(8))
                {
                    bool nz;
                    parts.Add(FormatValue(mon, e, out nz) ?? "-");
                    any |= nz;
                }
                nonZero = any;
                return string.Join(", ", parts) + (r.ArrayCount > 8 ? ", …" : "");
            }
            string type = r.EffectiveDataType, format = r.EffectiveFormat;
            int[] w;
            if (!TryWords(mon, r, out w)) return null;
            if (IsBit(type)) { nonZero = w[0] != 0; return w[0] != 0 ? "1" : "0"; }
            if (IsString(type))
            {
                string text = ToText(w, StringChars(type, r.EffectiveStringLength));
                nonZero = text.Length > 0;
                return "\"" + text + "\"";
            }
            if (IsFloat(type))
            {
                float f = ToFloat(w[0], w[1]);
                nonZero = f != 0;
                return f.ToString("G7", CultureInfo.InvariantCulture);
            }
            bool unsigned = IsUnsigned(type);
            long v = w.Length >= 2 && IsDoubleWord(type) ? CombineDouble(w[0], w[1], unsigned)
                   : unsigned ? (long)(ushort)w[0] : (long)(short)w[0];
            nonZero = v != 0;
            return FormatInteger(v, IsDoubleWord(type) ? 32 : 16, format);
        }

        /// <summary>
        /// What is typed, as the words to write - in the row's type and display
        /// format (a hexadecimal row takes "1F" or "0x1F", a binary one
        /// "0000 0101", any row "0x.." and "0b.."). Null, and why, when it cannot be.
        /// </summary>
        internal static int[] ParseValue(WatchRow r, string text, out string problem)
        {
            problem = null;
            string type = r.EffectiveDataType, format = r.EffectiveFormat;
            string t = (text ?? "").Trim();
            if (IsString(type))
            {
                if (t.Length >= 2 && t.StartsWith("\"") && t.EndsWith("\"")) t = t.Substring(1, t.Length - 2);
                int chars = StringChars(type, r.EffectiveStringLength);
                if (t.Length > chars) { problem = "Msg_MonTooLong"; return null; }
                return FromText(t, chars);
            }
            if (IsFloat(type))
            {
                double d;
                if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d) || double.IsNaN(d) ||
                    Math.Abs(d) > float.MaxValue) { problem = "Msg_MonNotNumber"; return null; }
                int lo, hi;
                FromFloat((float)d, out lo, out hi);
                return new[] { lo, hi };
            }

            // A bit also takes the words GX Works shows it by.
            if (IsBit(type))
            {
                string u = t.ToUpperInvariant();
                if (u == "TRUE" || u == "ON") t = "1";
                else if (u == "FALSE" || u == "OFF") t = "0";
            }
            long v;
            if (!TryParseInteger(t, format, out v)) { problem = "Msg_MonNotNumber"; return null; }
            bool dword = IsDoubleWord(type);
            if (IsBit(type)) { if (v != 0 && v != 1) { problem = "Msg_MonOutOfRange"; return null; } return new[] { (int)v }; }
            if ((!dword && (v < short.MinValue || v > ushort.MaxValue)) || (dword && (v < int.MinValue || v > uint.MaxValue)))
            { problem = "Msg_MonOutOfRange"; return null; }
            if (dword) return new[] { (int)(v & 0xFFFF), (int)((v >> 16) & 0xFFFF) };
            return new[] { (int)v };
        }

        internal static bool TryParseInteger(string t, string format, out long v)
        {
            v = 0;
            string s = (t ?? "").Trim().Replace(" ", "").Replace("_", "");
            if (s.Length == 0) return false;
            try
            {
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) { v = Convert.ToInt64(s.Substring(2), 16); return true; }
                if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) { v = Convert.ToInt64(s.Substring(2), 2); return true; }
                if (s.EndsWith("h", StringComparison.OrdinalIgnoreCase) && format != FormatBinary) { v = Convert.ToInt64(s.Substring(0, s.Length - 1), 16); return true; }
                if (format == FormatHex) { v = Convert.ToInt64(s, 16); return true; }
                if (format == FormatBinary) { v = Convert.ToInt64(s, 2); return true; }
            }
            catch { return false; }
            return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        }

        /// <summary>
        /// The elements under each array, and nowhere else: made (or kept) for
        /// every array in the list, in its place right under it; an element whose
        /// array has gone, moved or shrunk goes with it.
        /// </summary>
        internal static void EnsureElements(Sheet sh)
        {
            var result = new List<WatchRow>(sh.Rows.Count);
            foreach (var r in sh.Rows)
            {
                if (r.IsElement) continue;
                result.Add(r);
                if (!r.IsArray) { r.Elements = null; continue; }
                string type = r.EffectiveDataType;
                int len = r.StringLength;
                if (r.Elements == null || r.Elements.Count != r.ArrayCount) r.Elements = new List<WatchRow>();
                for (int i = 0; i < r.ArrayCount; i++)
                {
                    string address = PlcRowEditorForm.ElementAddress(r.Address, type, i, len);
                    if (i >= r.Elements.Count) r.Elements.Add(new WatchRow());
                    var e = r.Elements[i];
                    e.Head = r; e.ElementIndex = i; e.Station = r.Station; e.Address = address;
                }
                result.AddRange(r.Elements);
            }
            bool same = result.Count == sh.Rows.Count;
            for (int i = 0; same && i < result.Count; i++) same = ReferenceEquals(result[i], sh.Rows[i]);
            if (same) return;
            sh.Rows.Clear();
            sh.Rows.AddRange(result);
        }

        /// <summary>The list's items - arrays as one, their elements not separately.</summary>
        internal static List<WatchRow> Items(Sheet sh) { return sh.Rows.Where(r => !r.IsElement).ToList(); }
    }
}
