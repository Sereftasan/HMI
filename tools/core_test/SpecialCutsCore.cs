using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kaban.Profiles.Jobs;

// THE SPECIAL CUTS AND THE V CUTS (6 November: "L105/L106 with D65 (the <> special-cut recipes) and V-cuts ... yes please"), as the old
// program made them: the job file's SpecialCut / VCut lines, the joints of special ends and their recipes (one of both ends, or one of each
// end merged), the recipes' formulas (T D W O L R), where the cuts go on the bar (the recipe's saw dust width instead of the blade, the
// leftover after a special last end from its angle and the robot's X offset), the old program's tables read from CSV.
class SC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-6; }
    static double E(string f, FormulaValues v) { double r; string why; return SpecialCuts.Eval(f, v, out r, out why) ? r : double.NaN; }
    static string Steps(BarLayoutResult r) { return string.Join(" | ", r.Ordered.Select(s => BarLayout.Describe(s) + " @" + s.Position.ToString("0.###") + (s.Joint != null ? " J" : ""))); }

    static void Main() {
        // ---- the formulas ----
        var v = new FormulaValues { T = 4.2, D = 50, W = 70, O = 0.5, L = 10, R = 12 };
        Check("W/2+T = 39.2, -(L+O) = -10.5, 2*(W-L)/4 = 30, R-L*2 = -8", Near(E("W/2+T", v), 39.2) && Near(E("-(L+O)", v), -10.5) && Near(E("2*(W-L)/4", v), 30) && Near(E("R-L*2", v), -8));
        Check("  a number as it is (12,5 too), empty 0, D the V saws' distance", Near(E("12.5", v), 12.5) && Near(E("12,5", v), 12.5) && Near(E("", v), 0) && Near(E("d+1", v), 51));
        Check("  rounded to 0.01 as the old program (W/3 = 23.33)", Near(E("W/3", v), 23.33));
        double xv; string why;
        Check("  X, a bracket not closed, 2W: not worked out - and why", !SpecialCuts.Eval("W+X", v, out xv, out why) && why.Contains("'X'") &&
              !SpecialCuts.Eval("(W", v, out xv, out why) && !SpecialCuts.Eval("2W", v, out xv, out why) && !SpecialCuts.Eval("W/0", v, out xv, out why), why);

        // ---- the job file's lines ----
        var job = KbnFile.Parse(new[] {
            "Bar;1;P1;Test;1;White;60000;0;",
            "Profile;10000;10000;0900;0900;1;1;O1;1;C;A;;1;;0;B1;;;;;;",
            "VCut;2500;3;",
            "Profile;10000;10000;0900;0900;1;2;O1;1;C;A;;1;;0;B2;;;;;;",
            "SpecialCut;3;0;150;0",
            "Profile;10000;10000;0900;0900;1;3;O1;1;C;A;;1;;0;B3;;;;;;",
            "SpecialCut;0;5;0;0",
            "SpecialCut;1;1;0;0",
            "SpecialCut;x;0;0;0",
        }, "t");
        var bar = job.Bars[0];
        var p2 = bar.Pieces[1];
        Check("SpecialCut;3;0;150;0: the piece's left end special (3), its right ordinary, L = 15 mm (tenths)", p2.Special != null && p2.Special.LeftCode == "3" &&
              p2.Special.RightCode == "0" && p2.Special.LeftSpecial && !p2.Special.RightSpecial && Near(p2.Special.LeftParam, 15));
        Check("  a second SpecialCut on a piece, a code that is no number: problems with their lines", job.Problems.Count == 2 &&
              job.Problems[0].StartsWith("line 8:") && job.Problems[1].StartsWith("line 9:"), string.Join(" / ", job.Problems));
        Check("VCut;2500;3: the V cut at 250 mm, depth 3", bar.Pieces[0].VCuts.Count == 1 && Near(bar.Pieces[0].VCuts[0].Position, 250) && Near(bar.Pieces[0].VCuts[0].Depth, 3));

        // ---- the joints ----
        Check("no joint at the first piece's left end (nothing special there)", SpecialCuts.LeftOf(bar, 0) == null);
        var j1 = SpecialCuts.LeftOf(bar, 1);
        Check("between pieces 1 and 2: left code 3 (L 15), right the piece before's angle 90", j1 != null && j1.LeftCode == 3 && j1.RightCode == 90 && Near(j1.LeftParam, 15) &&
              j1.Before == 0 && j1.After == 1, j1 != null ? j1.Describe() : "none");
        var r0 = SpecialCuts.RightOf(bar, 0);
        Check("  the same joint seen from piece 1's right end", r0 != null && r0.Key == j1.Key);
        Check("between pieces 2 and 3: both ordinary (codes 0) - no joint", SpecialCuts.RightOf(bar, 1) == null && SpecialCuts.LeftOf(bar, 2) == null);
        var j3 = SpecialCuts.RightOf(bar, 2);
        Check("piece 3's right end special (5), nothing after it: left code 0, the bar's end", j3 != null && j3.RightCode == 5 && j3.LeftCode == 0 && j3.Before == 2 && j3.After == -1);

        // ---- the recipes ----
        var A = new SpecialCutRecipe { No = 1, Name = "A", LeftCode = 3, RightCode = 90, SawDustWidth = "T*2", StartingAngle = 90 };
        A.Steps.Add(new SpecialCutStep { Command = 1, LeftClamp = true, RightClamp = true });
        A.Steps.Add(new SpecialCutStep { Command = 4, Angle = 45 });
        var B = new SpecialCutRecipe { No = 2, Name = "B", LeftCode = 0, RightCode = 5, SawDustWidth = "6", StartingAngle = 45, RobotXOffset = "10" };
        B.Steps.Add(new SpecialCutStep { Command = 5, VCutY = "W/2" });
        var C = new SpecialCutRecipe { No = 3, Name = "C", LeftCode = 3, RightCode = 0, WithOffsetLeft = true, RobotXOffset = "L+1" };
        C.Steps.Add(new SpecialCutStep { Command = 3, Robot1 = "L", Robot2 = "W" });
        var D = new SpecialCutRecipe { No = 4, Name = "D", LeftCode = 0, RightCode = 45 };
        D.Steps.Add(new SpecialCutStep { Command = 2 });
        var recipes = new List<SpecialCutRecipe> { A, B, C, D };
        var plain = new SpecialJoint { LeftCode = 3, RightCode = 90 };
        Check("a joint of 3 and 90: the recipe of both (A)", SpecialCuts.Find(recipes, plain) == A);
        Check("  with an L value: A has none for it - C (3, any) and a right one for 90: none - no recipe",
              SpecialCuts.Find(recipes, new SpecialJoint { LeftCode = 3, RightCode = 90, LeftParam = 15 }) == null);
        var m = SpecialCuts.Find(recipes, new SpecialJoint { LeftCode = 3, RightCode = 45, LeftParam = 15 });
        Check("a joint of 3 (L 15) and 45: none has both - D (any, 45) merged with C (3, any)", m != null && m.MergedOf == "4 + 3" && m.Steps.Count == 3 &&
              m.Steps[0].Command == 2 && m.Steps[1].Command == 7 && m.Steps[1].Robot1 == "L+1" && m.Steps[2].Command == 3,
              m != null ? m.MergedOf + ": " + string.Join(",", m.Steps.Select(s => s.Command)) : "none");
        Check("  the recipes themselves left as they were (a copy merged)", D.Steps.Count == 1 && C.Steps.Count == 1);
        Check("a joint of 5 and the bar's end (0): B exactly", SpecialCuts.Find(recipes, j3) == B);

        // ---- kept in the library ----
        var back = SpecialCuts.Parse(SpecialCuts.Format(recipes));
        Check("written and read back: the recipes, their formulas, steps and clamps", back.Count == 4 && back[0].SawDustWidth == "T*2" && back[0].Steps.Count == 2 &&
              back[0].Steps[0].LeftClamp && back[0].Steps[1].Angle == 45 && back[2].WithOffsetLeft && back[2].Steps[0].Robot2 == "W" && back[1].StartingAngle == 45);

        // ---- the layout ----
        var cs = new CutSettings { SawThickness = 4, ProfileHeight = 70, MinimumLeftover = 300, Recipes = recipes, ProfileWidth = 70 };
        var two = KbnFile.Parse(new[] {
            "Bar;1;P1;Test;1;White;60000;0;",
            "Profile;10000;10000;0900;0900;1;1;O1;1;C;A;;1;;0;B1;;;;;;",
            "Profile;10000;10000;0900;0900;1;2;O1;1;C;A;;1;;0;B2;;;;;;",
            "SpecialCut;3;0;0;0",
        }, "two").Bars[0];
        var r = BarLayout.Of(two, cs);
        var st = r.Ordered;
        Check("pieces 1 and 2 joined by recipe A: its saw dust width (T*2 = 8) instead of the blade - the right cut at 4 + 1000 + 2, not 1004",
              r.Problems.Count == 0 && r.Joints.Count == 1 && st.Count == 3 && Near(st[0].Position, 0) && Near(st[1].Position, 1006) && st[1].Joint != null &&
              st[1].Kind == StepKind.RightCut && Near(st[2].Position, 2012), Steps(r) + " / " + string.Join(" / ", r.Problems));
        Check("  piece 2's left end the same cut (square on square): one step for the joint", st.Count(x => x.Joint != null) == 1);
        Check("  the leftover 6000 - 2012 - 2", Near(r.Leftover, 3986), r.Leftover.ToString());

        var last = KbnFile.Parse(new[] {
            "Bar;1;P1;Test;1;White;60000;0;",
            "Profile;10000;10000;0900;0900;1;1;O1;1;C;A;;1;;0;B1;;;;;;",
            "VCut;2500;0;",
            "SpecialCut;0;5;0;0",
        }, "last").Bars[0];
        r = BarLayout.Of(last, cs);
        st = r.Ordered;
        Check("a V cut at 250 mm: a step 2 + 250 on the bar", st.Any(x => x.Kind == StepKind.VCut && Near(x.Position, 252)), Steps(r));
        Check("the bar ends in recipe B (any, 5): its right cut at 2 + 3 + 1000", st.Any(x => x.Kind == StepKind.RightCut && x.Joint != null && Near(x.Position, 1005)), Steps(r));
        Check("  the leftover squared as after B's angle (180 - 45): the height's slip (70) taken, then the robot's X offset 10 - the cut at 1088",
              st.Last().Kind == StepKind.LeftoverCut && Near(st.Last().Position, 1088), Steps(r));

        var none = BarLayout.Of(last, new CutSettings { SawThickness = 4, ProfileHeight = 70, MinimumLeftover = 300 });
        Check("no recipes: the bar says which joint has none (and is cut as ordinary ends)", none.Problems.Any(p => p.Contains("piece 1's right end") && p.Contains("no special cut recipe")) &&
              none.Joints.Count == 0, string.Join(" / ", none.Problems));

        // ---- the old program's tables (pgAdmin's CSV) ----
        var dir = Path.Combine(Path.GetTempPath(), "sc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string rc = Path.Combine(dir, "sawreceipts.csv"), dc = Path.Combine(dir, "sawreceiptdetails.csv");
        File.WriteAllText(rc, "\"Id\",\"No\",\"JobName\",\"HoldingPosition\",\"RobotXOffset\",\"SawDustWidth\",\"LeftCode\",\"RightCode\",\"StartingAngle\",\"WithOffsetLeft\",\"WithOffsetRight\"\n" +
                              "\"a1\",2,\"Kanat <> 45\",\"W/2\",\"-T\",\"T*2\",3,45,\"67.5\",true,false\n" +
                              "\"b2\",1,\"Kasa, ok\",\"\",\"\",\"T\",0,90,90,false,false\n");
        File.WriteAllText(dc, "\"Id\",\"CommandNo\",\"LeftClamp\",\"RightClamp\",\"Robot1\",\"Robot2\",\"Angle\",\"VCutting_Y\",\"SawReceiptId\"\n" +
                              "\"x1\",1,true,true,\"\",\"\",0,\"0\",\"a1\"\n\"x2\",4,false,false,\"\",\"\",45,\"\",\"a1\"\n\"x3\",3,false,false,\"L\",\"W-10\",0,\"\",\"b2\"\n" +
                              "\"x4\",1,true,false,\"\",\"\",0,\"\",\"zz\"\n");
        var probs = new List<string>();
        var imp = SpecialCuts.ImportOld(rc, dc, probs);
        Check("the old program's recipes read: by number, their codes, angle, formulas, offsets", imp.Count == 2 && imp[0].No == 1 && imp[1].No == 2 && imp[1].Name == "Kanat <> 45" &&
              imp[1].LeftCode == 3 && imp[1].RightCode == 45 && Near(imp[1].StartingAngle, 67.5) && imp[1].HoldingPosition == "W/2" && imp[1].WithOffsetLeft && !imp[1].WithOffsetRight &&
              imp[0].Name == "Kasa, ok", string.Join(" | ", imp.Select(i => i.No + " " + i.Name)));
        Check("  their steps in the file's order; a step of no recipe said", imp[1].Steps.Count == 2 && imp[1].Steps[0].LeftClamp && imp[1].Steps[1].Angle == 45 &&
              imp[0].Steps.Count == 1 && imp[0].Steps[0].Robot2 == "W-10" && probs.Count == 1, string.Join(" / ", probs));
        try { Directory.Delete(dir, true); } catch { }

        Console.WriteLine(fail == 0 ? "\nSPECIAL CUTS ALL PASS" : "\nSPECIAL CUTS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
