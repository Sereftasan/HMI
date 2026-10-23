using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Helpers;

// 23 October, Config Import: "We need a compare tool here I think, so the user could see what is different in between
// the old and new one of the files"
class TDC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { d = d ?? ""; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    static string Show(List<TextDiff.Row> rows) {
        return string.Join(" ", rows.Select(r => (r.Kind == TextDiff.Kind.Same ? "=" : r.Kind == TextDiff.Kind.Changed ? "~" : r.Kind == TextDiff.Kind.OnlyLeft ? "-" : "+") +
                                                  (r.Kind == TextDiff.Kind.OnlyRight ? r.RightText : r.LeftText)));
    }
    static void Main() {
        var a = new[] { "1", "2", "3", "4", "5" };
        Check("the same: every line paired, nothing differs", TextDiff.Count(TextDiff.Compare(a, a)).Identical && TextDiff.Compare(a, a).Count == 5);
        var r1 = TextDiff.Compare(a, new[] { "1", "2", "x", "4", "5" });
        Check("a line changed: side by side", Show(r1) == "=1 =2 ~3 =4 =5" && r1[2].RightText == "x", Show(r1));
        var r2 = TextDiff.Compare(a, new[] { "1", "2", "3", "3b", "4", "5" });
        Check("a line put in: only in the second", Show(r2) == "=1 =2 =3 +3b =4 =5", Show(r2));
        var r3 = TextDiff.Compare(a, new[] { "1", "3", "4", "5" });
        Check("a line taken away: only in the first", Show(r3) == "=1 -2 =3 =4 =5", Show(r3));
        var r4 = TextDiff.Compare(new[] { "a", "b", "c" }, new string[0]);
        Check("against nothing: all only in the first", Show(r4) == "-a -b -c", Show(r4));
        var r5 = TextDiff.Compare(new[] { "x", "A", "y", "B", "z" }, new[] { "B", "y", "A" });
        var c5 = TextDiff.Count(r5);
        Check("moved lines: every line of both once, in their order", r5.Where(r => r.Left >= 0).Select(r => r.Left).SequenceEqual(Enumerable.Range(0, 5)) &&
              r5.Where(r => r.Right >= 0).Select(r => r.Right).SequenceEqual(Enumerable.Range(0, 3)), Show(r5));

        // a big file, a few lines changed: quick, and only those
        var big = Enumerable.Range(0, 20000).Select(i => "line " + i + ";" + (i % 7)).ToList();
        var big2 = big.ToList();
        big2[100] = "changed"; big2.Insert(5000, "new one"); big2.RemoveAt(15000);
        var t0 = DateTime.Now;
        var r6 = TextDiff.Compare(big, big2);
        var c6 = TextDiff.Count(r6);
        Check("20000 lines, 3 changes: found in a moment", c6.Changed == 1 && c6.OnlyRight == 1 && c6.OnlyLeft == 1 && (DateTime.Now - t0).TotalSeconds < 5,
              c6.Changed + " ~ " + c6.OnlyRight + " + " + c6.OnlyLeft + " - in " + (DateTime.Now - t0).TotalMilliseconds + " ms");
        var rep = Enumerable.Repeat("same", 3000).ToList();
        var rep2 = Enumerable.Repeat("same", 2990).Concat(new[] { "other" }).ToList();
        var r7 = TextDiff.Compare(rep, rep2);
        Check("repeated lines (none once in each): still paired", TextDiff.Count(r7).Same == 2990 && TextDiff.Count(r7).Differences == 10, TextDiff.Count(r7).Differences + " differences");

        Console.WriteLine(fail == 0 ? "\nTEXT DIFF ALL PASS" : "\nTEXT DIFF " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
