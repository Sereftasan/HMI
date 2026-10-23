using System;
using System.Collections.Generic;
using System.Linq;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// TWO TEXTS SIDE BY SIDE (23 October, Config Import: "We need a compare tool here I think, so the user could see
    /// what is different in between the old and new one of the files"): the lines of two files paired - the same, one
    /// changed into the other, only in the first, only in the second - as a compare program shows them.
    ///
    /// The lines found in both files once each hold the two together (patience diff); between them the longest
    /// common run of lines (a table of their sizes, for parts up to two million pairs - larger, all of one replaced by
    /// all of the other). A line taken away next to one put in is a line CHANGED.
    /// </summary>
    public static class TextDiff
    {
        public enum Kind { Same, Changed, OnlyLeft, OnlyRight }

        /// <summary>A row of the two side by side: a line of each (-1: none on that side).</summary>
        public sealed class Row
        {
            public Kind Kind;
            public int Left = -1, Right = -1;
            public string LeftText = "", RightText = "";
        }

        public sealed class Summary
        {
            public int Same, Changed, OnlyLeft, OnlyRight;
            public bool Identical { get { return Changed == 0 && OnlyLeft == 0 && OnlyRight == 0; } }
            public int Differences { get { return Changed + OnlyLeft + OnlyRight; } }
        }

        private const long MaxTable = 2000000;

        public static List<Row> Compare(IList<string> left, IList<string> right)
        {
            left = left ?? new string[0];
            right = right ?? new string[0];
            var ops = new List<KeyValuePair<int, int>>();      // matched pairs (left, right), in order
            Match(left, 0, left.Count, right, 0, right.Count, ops);

            var rows = new List<Row>();
            int li = 0, ri = 0;
            foreach (var m in ops.Concat(new[] { new KeyValuePair<int, int>(left.Count, right.Count) }))
            {
                // the gap before the pair: changed lines side by side, the rest only on one side
                int ln = m.Key - li, rn = m.Value - ri, both = Math.Min(ln, rn);
                for (int k = 0; k < both; k++)
                    rows.Add(new Row { Kind = Kind.Changed, Left = li + k, Right = ri + k, LeftText = left[li + k], RightText = right[ri + k] });
                for (int k = both; k < ln; k++) rows.Add(new Row { Kind = Kind.OnlyLeft, Left = li + k, LeftText = left[li + k] });
                for (int k = both; k < rn; k++) rows.Add(new Row { Kind = Kind.OnlyRight, Right = ri + k, RightText = right[ri + k] });
                if (m.Key < left.Count && m.Value < right.Count)
                    rows.Add(new Row { Kind = Kind.Same, Left = m.Key, Right = m.Value, LeftText = left[m.Key], RightText = right[m.Value] });
                li = m.Key + 1; ri = m.Value + 1;
            }
            return rows;
        }

        public static Summary Count(IEnumerable<Row> rows)
        {
            var s = new Summary();
            foreach (var r in rows)
                switch (r.Kind)
                {
                    case Kind.Same: s.Same++; break;
                    case Kind.Changed: s.Changed++; break;
                    case Kind.OnlyLeft: s.OnlyLeft++; break;
                    default: s.OnlyRight++; break;
                }
            return s;
        }

        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.Ordinal); }

        /// <summary>The matched pairs of left[lo1..hi1) and right[lo2..hi2), in order, into <paramref name="ops"/>.</summary>
        private static void Match(IList<string> a, int lo1, int hi1, IList<string> b, int lo2, int hi2, List<KeyValuePair<int, int>> ops)
        {
            // the same lines at the start and at the end
            while (lo1 < hi1 && lo2 < hi2 && Same(a[lo1], b[lo2])) { ops.Add(new KeyValuePair<int, int>(lo1, lo2)); lo1++; lo2++; }
            var tail = new List<KeyValuePair<int, int>>();
            while (hi1 > lo1 && hi2 > lo2 && Same(a[hi1 - 1], b[hi2 - 1])) { hi1--; hi2--; tail.Add(new KeyValuePair<int, int>(hi1, hi2)); }
            if (lo1 < hi1 && lo2 < hi2)
            {
                var anchors = UniqueAnchors(a, lo1, hi1, b, lo2, hi2);
                if (anchors.Count > 0)
                {
                    int p1 = lo1, p2 = lo2;
                    foreach (var an in anchors)
                    {
                        Match(a, p1, an.Key, b, p2, an.Value, ops);
                        ops.Add(an);
                        p1 = an.Key + 1; p2 = an.Value + 1;
                    }
                    Match(a, p1, hi1, b, p2, hi2, ops);
                }
                else if ((long)(hi1 - lo1) * (hi2 - lo2) <= MaxTable) Lcs(a, lo1, hi1, b, lo2, hi2, ops);
                // larger and nothing in common to hold on to: all of one replaced by all of the other
            }
            tail.Reverse();
            ops.AddRange(tail);
        }

        /// <summary>The lines once in each part, in an order both keep (the longest such run).</summary>
        private static List<KeyValuePair<int, int>> UniqueAnchors(IList<string> a, int lo1, int hi1, IList<string> b, int lo2, int hi2)
        {
            var countA = new Dictionary<string, int>(StringComparer.Ordinal);
            var posA = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = lo1; i < hi1; i++) { int n; countA[a[i]] = countA.TryGetValue(a[i], out n) ? n + 1 : 1; posA[a[i]] = i; }
            var countB = new Dictionary<string, int>(StringComparer.Ordinal);
            var posB = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int j = lo2; j < hi2; j++) { int n; countB[b[j]] = countB.TryGetValue(b[j], out n) ? n + 1 : 1; posB[b[j]] = j; }
            var pairs = new List<KeyValuePair<int, int>>();
            for (int j = lo2; j < hi2; j++)
            {
                int ca, cb;
                if (countB.TryGetValue(b[j], out cb) && cb == 1 && countA.TryGetValue(b[j], out ca) && ca == 1)
                    pairs.Add(new KeyValuePair<int, int>(posA[b[j]], j));          // in the right's order
            }
            if (pairs.Count == 0) return pairs;
            // the longest run increasing in the left's order (patience sorting)
            var tails = new List<int>();
            var prev = new int[pairs.Count];
            for (int i = 0; i < pairs.Count; i++)
            {
                int x = pairs[i].Key, lo = 0, hi = tails.Count;
                while (lo < hi) { int mid = (lo + hi) / 2; if (pairs[tails[mid]].Key < x) lo = mid + 1; else hi = mid; }
                prev[i] = lo > 0 ? tails[lo - 1] : -1;
                if (lo == tails.Count) tails.Add(i); else tails[lo] = i;
            }
            var run = new List<KeyValuePair<int, int>>();
            for (int i = tails[tails.Count - 1]; i >= 0; i = prev[i]) run.Add(pairs[i]);
            run.Reverse();
            return run;
        }

        /// <summary>The longest common run of lines of two small parts (a table of their lengths).</summary>
        private static void Lcs(IList<string> a, int lo1, int hi1, IList<string> b, int lo2, int hi2, List<KeyValuePair<int, int>> ops)
        {
            int n = hi1 - lo1, m = hi2 - lo2;
            var t = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
                for (int j = m - 1; j >= 0; j--)
                    t[i, j] = Same(a[lo1 + i], b[lo2 + j]) ? t[i + 1, j + 1] + 1 : Math.Max(t[i + 1, j], t[i, j + 1]);
            int x = 0, y = 0;
            while (x < n && y < m)
            {
                if (Same(a[lo1 + x], b[lo2 + y])) { ops.Add(new KeyValuePair<int, int>(lo1 + x, lo2 + y)); x++; y++; }
                else if (t[x + 1, y] >= t[x, y + 1]) x++;
                else y++;
            }
        }
    }
}
