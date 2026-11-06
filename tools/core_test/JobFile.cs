using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kaban.Profiles.Jobs;

// THE JOB FILE (.kbn) AND THE BAR'S LAYOUT (6 November): the window software's job list read as the old program's KabanFileReader
// did (tenths, a bar of quantity N as N bars), and every cut and accessory of a bar where the old program's ProfileProcess put it.
class JF {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    static bool Near(double a, double b, double tol = 0.001) { return Math.Abs(a - b) <= tol; }
    static string Steps(BarLayoutResult r) { return string.Join(" | ", r.Ordered.Select(s => s.Kind + " " + s.Position.ToString("0.###") + " #" + s.CutCode)); }

    static void Main() {
        var text = new[] {
            "# made by the test",
            "// a line left out",
            "Bar;1;ML8-F-4501;Functional outer frame 77;1;White;65000;0;",
            "Profile;12000;14000;0450;0450;1;1;S2026-001;1;KABAN TEST;ALT;Su Tahliye;1;;0;BC-01;1;;;;KABAN;",
            "Rec;GEN-DRN-5X30;1500;",
            "Rec;GEN-DRN-5X30;6000;",
            "VCut;250;3;",
            "",
            "Bar;2;ML8-M-4520;Functional mullion;2;White;65000;0;",
            "Profile;13100;13100;0900;0900;1;2;S2026-001;1;KABAN TEST;ORTA;;1;;0;BC-02;2;;;;KABAN;",
            "Rec;X;abc;",
            "Profile;13100;13100;2000;0900;1;3;S2026-001;1;KABAN TEST;ORTA;;1;;0;BC-03;3;;;;KABAN;",
            "Rec;Y;100;",
            "Profile;9000;9000;0450;0450;1;4;S2026-001;1;KABAN TEST;ORTA;;1;;0;BC-04;4;;;;KABAN;",
            "Bar;3;;No code;1;White;65000;0;",
            "Profile;9000;9000;0450;0450;1;5;S2026-001;1;KABAN TEST;ORTA;;1;;0;BC-05;5;;;;KABAN;",
            "Rec;Z;100;",
        };
        var job = KbnFile.Parse(text, "test");
        Check("three bars: the second's quantity 2 is two bars", job.Bars.Count == 3 && job.Bars[1].No == 2 && job.Bars[2].No == 2 && job.Bars[2].Copy == 2,
              string.Join(",", job.Bars.Select(b => b.Title)));
        var b1 = job.Bars[0];
        Check("the bar: code, name, colour, its length in mm (the file has tenths)", b1.Code == "ML8-F-4501" && b1.Name == "Functional outer frame 77" && b1.Color == "White" && b1.Length == 6500);
        var p1 = b1.Pieces[0];
        Check("the piece: lengths and angles in mm and degrees, its order, customer, position, barcode",
              p1.CutLength == 1200 && p1.OtherLength == 1400 && p1.LeftAngle == 45 && p1.RightAngle == 45 && p1.OrderNo == "S2026-001" &&
              p1.Customer == "KABAN TEST" && p1.Position == "ALT" && p1.Accessories == "Su Tahliye" && p1.Barcode == "BC-01");
        Check("its accessories from the piece's start, in mm", p1.Recs.Count == 2 && p1.Recs[0].Code == "GEN-DRN-5X30" && p1.Recs[0].Position == 150 && p1.Recs[1].Position == 600);
        // 6 November: the V cuts and the special cuts taken (the old program's VCutting: position in tenths, depth as written)
        Check("a VCut line: the piece's V cut at 25 mm, depth 3", p1.VCuts.Count == 1 && p1.VCuts[0].Position == 25 && p1.VCuts[0].Depth == 3 && p1.Other.Count == 0,
              p1.VCuts.Count + " " + p1.Other.Count);
        Check("the comment kept, the // line left out", job.Comments.Count == 1 && job.Comments[0] == "# made by the test");
        Check("an accessory at 'abc', an angle of 200, a bar with no code: three problems, with their lines - what was under the bad piece and bar left out with them",
              job.Problems.Count == 3 && job.Problems[0].StartsWith("line 11:") && job.Problems[1].StartsWith("line 12:") && job.Problems[2].StartsWith("line 15:"),
              string.Join(" / ", job.Problems));
        Check("  the piece after them read", job.Bars[1].Pieces.Count == 2 && job.Bars[1].Pieces[1].CutLength == 900 && job.Bars[1].Pieces.All(x => x.Recs.Count == 0));

        // ---- written and read again ----
        var again = KbnFile.Parse(KbnFile.Format(job.Bars).Split('\n').Select(l => l.TrimEnd('\r')), "again");
        Check("written and read again: the same bars, pieces and accessories", again.Problems.Count == 0 && again.Bars.Count == 3 &&
              again.Bars[0].Pieces[0].Recs.Count == 2 && again.Bars[0].Pieces[0].LeftAngle == 45 && again.Bars[2].Pieces.Count == 2 &&
              again.Bars[0].Pieces[0].Barcode == "BC-01" && again.Bars[0].Pieces[0].OtherLength == 1400);
        Check("  its V cut written and read back", again.Bars[0].Pieces[0].VCuts.Count == 1 && again.Bars[0].Pieces[0].VCuts[0].Position == 25);
        Check("  angles as the window software writes them (0450), CRLF", KbnFile.Format(job.Bars).Contains(";0450;0450;") && KbnFile.Format(job.Bars).Contains("\r\n"));

        // ---- the library's sample job ----
        string sample = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JobSample_Library.kbn");
        if (!File.Exists(sample)) sample = "/home/claude/work/docs/fa2010/JobSample_Library.kbn";
        var sj = KbnFile.Read(sample);
        Check("the library's sample job (docs/fa2010): read with no problem, 7 bars (the mullions' quantity 2)", sj.Problems.Count == 0 && sj.Bars.Count == 7 && sj.Name == "JobSample_Library",
              sj.Bars.Count + " " + string.Join(" / ", sj.Problems));
        Check("  its sash bar's hinges, handle and lock", sj.Bars[1].Code == "ML8-S-4510" && sj.Bars[1].Pieces[2].Recs.All(x => x.Code == "NX-HNG-STD") &&
              sj.Bars[1].Pieces[3].Recs.Select(x => x.Code).SequenceEqual(new[] { "NX-ESP-STD", "NX-HDL-92", "NX-LCK-35" }));

        // ---- the layout (saw 4 mm, no vertical share, profile 70 high) ----
        var cs = new CutSettings { SawThickness = 4, SawVerticalShare = 0, ProfileHeight = 70, MinimumLeftover = 300 };
        Func<double, double, double, KbnPiece> P = (l, a, b) => new KbnPiece { CutLength = l, LeftAngle = a, RightAngle = b };
        var bar = new KbnBar { No = 1, Code = "X", Length = 6000 };
        var one = P(1000, 90, 90);
        one.Recs.Add(new KbnRec { Code = "H", Position = 100 });
        bar.Pieces.Add(one);
        var r = BarLayout.Of(bar, cs);
        Check("one piece 1000 square: the left cut at 0 (1900), the accessory at 2 + 100 (half the blade), the right cut at 1004 (1900)",
              r.Ordered.Count == 3 && r.Ordered[0].Kind == StepKind.LeftCut && Near(r.Ordered[0].Position, 0) && r.Ordered[0].CutCode == 1900 &&
              r.Ordered[1].Kind == StepKind.Accessory && Near(r.Ordered[1].Position, 102) && r.Ordered[2].Kind == StepKind.RightCut && Near(r.Ordered[2].Position, 1004) &&
              r.Ordered[2].CutCode == 1900, Steps(r));
        Check("  the leftover 6000 - 1004 - 2, not squared (the last cut is square)", Near(r.Leftover, 4994) && r.Problems.Count == 0, r.Leftover.ToString());

        bar.Pieces.Clear();
        bar.Pieces.Add(P(1000, 45, 45));
        bar.Pieces.Add(P(1000, 45, 45));
        r = BarLayout.Of(bar, cs);
        double h45 = 2 / Math.Sin(Math.PI / 4);
        Check("two pieces 45/45: cuts at 0, 1005.657 (right 2350, the next left 1450), 2011.314, the leftover squared at 2012.142",
              r.Ordered.Count == 5 && Near(r.Ordered[0].Position, 0) && r.Ordered[0].CutCode == 1450 && Near(r.Ordered[1].Position, 1000 + 2 * h45) && r.Ordered[1].CutCode == 2350 &&
              r.Ordered[2].Kind == StepKind.LeftCut && Near(r.Ordered[2].Position, 1000 + 2 * h45) && r.Ordered[2].CutCode == 1450 &&
              Near(r.Ordered[3].Position, 2000 + 4 * h45) && r.Ordered[4].Kind == StepKind.LeftoverCut && Near(r.Ordered[4].Position, 2000 + 4 * h45 + (h45 - 2)) &&
              r.Ordered[4].CutCode == 1900, Steps(r));
        Check("  the leftover after it", Near(r.Leftover, 6000 - (2000 + 4 * h45 + (h45 - 2) + 2) - 2), r.Leftover.ToString("0.###"));

        bar.Pieces.Clear();
        bar.Pieces.Add(P(1000, 90, 45));
        var two = P(1000, 135, 90);
        two.Recs.Add(new KbnRec { Code = "H", Position = 500 });
        bar.Pieces.Add(two);
        r = BarLayout.Of(bar, cs);
        Check("45 then 135: one cut for both ends (the right one, marked so), the second piece 930 along the bar (70 high at 135)",
              r.Ordered.Count(s => s.IsCut) == 3 && r.Ordered[1].Kind == StepKind.RightCut && r.Ordered[1].Shared && Near(r.Ordered[1].Position, 1002 + h45) &&
              Near(r.Ordered.Last().Position, 1002 + h45 + h45 + 930 + 2), Steps(r));
        var acc = r.Ordered.First(s => s.Kind == StepKind.Accessory);
        Check("  its accessory moved back by the slip (500 - 70 from where the piece starts)", Near(acc.Position, 1002 + 2 * h45 + 500 - 70), acc.Position.ToString("0.###"));

        bar.Pieces.Clear();
        bar.Pieces.Add(P(1000, 90, 90));
        r = BarLayout.Of(bar, new CutSettings { SawThickness = 4, ProfileHeight = 70, MinimumLeftover = 300, MaximumCutLength = 2000 });
        Check("the longest piece 2000: the leftover cut at 2008 (4994 % 1996 first) and 4008, 1988 left",
              r.Ordered.Count == 4 && r.Ordered[2].Kind == StepKind.LeftoverCut && Near(r.Ordered[2].Position, 2008) && Near(r.Ordered[3].Position, 4008) && Near(r.Leftover, 1988), Steps(r));

        bar.Length = 1000;
        r = BarLayout.Of(bar, cs);
        Check("a piece the bar cannot take: a problem", r.Problems.Count == 1 && r.Problems[0].Contains("1000"), string.Join(" / ", r.Problems));

        bar.Length = 6000;
        bar.Pieces.Clear();
        bar.Pieces.Add(P(1000, 90, 90));
        bar.Pieces.Add(P(1000, 90, 90));
        r = BarLayout.Of(bar, cs);
        Check("two square pieces: the second's left end is the first's right cut (not cut twice)", r.Ordered.Count == 3 && r.Ordered[1].Shared, Steps(r));

        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
