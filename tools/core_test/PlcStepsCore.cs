using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Profiles.Machining;

// A MILLING OPERATION'S STEPS FOR THE PLC (6 November: the movement codes of FA2010_DeviceList - 1 in / out without the end check,
// 11 cutting with it, 2 / 3 arcs at 0 and 180 degrees, 4 / 5 at 90 and 270, "0 means the last line"): incremental moves from the
// operation's approach point, the speed in m/min, 16 words a step.
class PS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-6; }
    static string All(PlcSteps.Result r) { return string.Join(" | ", r.Steps.Select(s => s.ToString())) + (r.Ok ? "" : " / " + r.Problem); }

    static SideOperation Op(string type, string json, string side, double y, double z) {
        var m = new MacroShape { Code = "M", Type = type, Params = FlatJson.ParseDictionary(json) };
        return new SideOperation { Accessory = "A", Seq = 1, Macro = m, Side = side, Y = y, Z = z, Status = "CONFIRMED" };
    }

    static void Main() {
        var feeds = new List<Feed> { new Feed { MillingClass = "PVC_OUTER_WALL", ToolType = "END_MILL", Entry = 1500, Cut = 4000, Plunge = 700 } };
        var s = new ProgramSettings { PeckDepth = 4, ApproachFeed = 3000, Breakthrough = 0.5, Material = "PVC" };
        var mill10 = new Tool { Position = "U1-T2", Type = "END_MILL", Diameter = 10, Unit = 1 };
        var mill5 = new Tool { Position = "U1-T1", Type = "END_MILL", Diameter = 5, Unit = 1 };
        Func<double, Face> F = a => new Face { Unit = 1, Side = "TOP", Angle = a };

        // ---- a slot 25 x 10 x 8 on the top face of a profile 77 x 65, its entry point at Y 22 on the top (Z 65) ----
        var slot = Op("SLOT", "{\"length_mm\":25,\"width_mm\":10,\"depth_mm\":8}", "TOP", 22, 65);
        var r = PlcSteps.For(slot, false, null, 77, 65, mill10, F(0), feeds, s);
        Check("a slot on the top face: made", r.Ok, All(r));
        Check("  its approach point: Y 22 across, 1 mm above the top (Z 66)", Near(r.ApproachY, 22) && Near(r.ApproachZ, 66), r.ApproachY + " " + r.ApproachZ);
        Check("  first to where the path starts (7.5 back along the bar: X +7.5 - the old program's X runs the other way; code 11, the approach feed 3 m/min)",
              r.Steps[0].Code == 11 && Near(r.Steps[0].X, 7.5) && Near(r.Steps[0].Y, 0) && Near(r.Steps[0].Z, 0) && Near(r.Steps[0].Speed, 3), All(r));
        Check("  in without the end check (code 1): Z +5 into the profile (1 in the air, a 4 mm pass - the old program's in is +Z), the entry feed 1.5 m/min",
              r.Steps[1].Code == 1 && Near(r.Steps[1].Z, 5) && Near(r.Steps[1].X, 0) && Near(r.Steps[1].Speed, 1.5), All(r));
        Check("  along the slot with the end check (code 11), the cutting feed 4 m/min", r.Steps[2].Code == 11 && Near(r.Steps[2].X, -15) && Near(r.Steps[2].Speed, 4), All(r));
        Check("  passes down to 9 mm (8 deep + the 1 above), then out (code 1)", Math.Abs(r.Steps.Where(x => x.Code == 1).Sum(x => x.Z)) < 1e-9 &&
              Near(r.Steps.Where(x => x.Code == 1 && x.Z > 0).Sum(x => x.Z), 9) && r.Steps.Last().Code == 1 && Near(r.Steps.Last().Z, -9), All(r));
        Check("  each pass back to the start before the next", r.Steps.Count(x => x.What == "back to the start") == r.Steps.Count(x => x.What == "in") - 1, All(r));

        // ---- the same from the bottom face (180): down is up, across turned ----
        var bottom = Op("SLOT", "{\"length_mm\":25,\"width_mm\":10,\"depth_mm\":8}", "BOTTOM", 22, 0);
        r = PlcSteps.For(bottom, false, null, 77, 65, mill10, new Face { Unit = 1, Side = "BOTTOM", Angle = 180 }, feeds, s);
        Check("from below (180): in is -Z, its approach 1 mm under the bottom (Z -1)", r.Ok && r.Steps.First(x => x.Code == 1).Z < 0 && Near(r.ApproachZ, -1), All(r));

        // ---- a hole bigger than the tool: a full circle ----
        var hole = Op("ROUND_HOLE", "{\"diameter_mm\":10,\"depth_mm\":12}", "TOP", 22, 65);
        r = PlcSteps.For(hole, false, null, 77, 65, mill5, F(0), feeds, s);
        var arc = r.Steps.FirstOrDefault(x => x.Code >= 2 && x.Code <= 5);
        Check("a 10 mm hole with a 5 mm mill from the top: a full circle, clockwise - code 3 as the old program wrote it at 0 degrees; its centre 2.5 back (I +2.5, J 0)",
              r.Ok && arc != null && arc.Code == 3 && Near(arc.X, 0) && Near(arc.Y, 0) && Near(arc.I, 2.5) && Near(arc.J, 0), All(r));
        var sideHole = Op("ROUND_HOLE", "{\"diameter_mm\":10,\"depth_mm\":12}", "OUTSIDE", 0, 30);
        r = PlcSteps.For(sideHole, false, null, 77, 65, mill5, new Face { Unit = 1, Side = "OUTSIDE", Angle = 90 }, feeds, s);
        arc = r.Steps.FirstOrDefault(x => x.Code >= 2 && x.Code <= 5);
        Check("  from the outside face (90): in along +Y, the arc in X, Z (code 5, as the old program at 90 / 270)",
              r.Ok && r.Steps.First(x => x.Code == 1).Y > 0 && Near(r.Steps.First(x => x.Code == 1).Z, 0) && arc != null && arc.Code == 5 && Near(arc.K, 0), All(r));
        r = PlcSteps.For(Op("ROUND_HOLE", "{\"diameter_mm\":10,\"depth_mm\":12}", "INSIDE", 77, 30), false, null, 77, 65, mill5,
                         new Face { Unit = 1, Side = "INSIDE", Angle = 270 }, feeds, s);
        arc = r.Steps.FirstOrDefault(x => x.Code >= 2 && x.Code <= 5);
        Check("  from the inside face (270): in along -Y, the arc code 5", r.Ok && r.Steps.First(x => x.Code == 1).Y < 0 && arc != null && arc.Code == 5, All(r));
        r = PlcSteps.For(Op("ROUND_HOLE", "{\"diameter_mm\":10,\"depth_mm\":12}", "BOTTOM", 22, 0), false, null, 77, 65, mill5,
                         new Face { Unit = 1, Side = "BOTTOM", Angle = 180 }, feeds, s);
        arc = r.Steps.FirstOrDefault(x => x.Code >= 2 && x.Code <= 5);
        Check("  from below (180): the arc code 3", r.Ok && arc != null && arc.Code == 3, All(r));

        // ---- a drilled hole the tool's size: in and out ----
        var drill = new Tool { Position = "U2-T1", Type = "DRILL", Diameter = 5, Unit = 2 };
        feeds.Add(new Feed { MillingClass = "PVC_OUTER_WALL", ToolType = "DRILL", Entry = 900, Cut = 1800, Plunge = 400 });
        r = PlcSteps.For(Op("ROUND_HOLE", "{\"diameter_mm\":5,\"depth_mm\":12}", "TOP", 22, 65), false, null, 77, 65, drill, F(0), feeds, s);
        Check("a 5 mm hole with a 5 mm drill: in at the drill's plunge feed 0.4 m/min (13 mm), out", r.Ok && r.Steps.Count == 2 && r.Steps[0].Code == 1 &&
              Near(r.Steps[0].Z, 13) && Near(r.Steps[0].Speed, 0.4) && r.Steps[1].Code == 1 && Near(r.Steps[1].Z, -13), All(r));

        // ---- the PLC's words ----
        r = PlcSteps.For(slot, false, null, 77, 65, mill10, F(0), feeds, s);
        var w = PlcSteps.Words(r.Steps, 25);
        Func<int, int> dw = i => (w[i] & 0xFFFF) | (w[i + 1] << 16);
        Check("16 words a step: the code, X Y Z I J K and the speed x100 as double words", w.Length == 400 && w[0] == 11 && dw(1) == 750 && dw(13) == 300 &&
              w[16] == 1 && dw(16 + 5) == 500 && dw(16 + 13) == 150);
        Check("  after the last step code 0 - the end", w[r.Steps.Count * 16] == 0 && w.Skip(r.Steps.Count * 16).All(x => x == 0));

        // ---- too many steps; an angle the PLC has no arcs for ----
        var deep = Op("POCKET", "{\"length_mm\":60,\"width_mm\":40,\"depth_mm\":40,\"corner_r_mm\":2}", "TOP", 22, 65);
        r = PlcSteps.For(deep, false, null, 77, 65, mill10, F(0), feeds, new ProgramSettings { PeckDepth = 4, ApproachFeed = 3000, Material = "PVC" });
        Check("a pocket 60 x 40 x 40 in 4 mm passes: its lanes and outline each pass, within the PLC's 399 steps", r.Ok && r.Steps.Count > 25 && r.Steps.Count <= 399, r.Steps.Count + " " + r.Problem);
        var words = PlcSteps.Words(r.Steps);
        Check("  its words: every step and the end (more than the old program's 25 steps)", words.Length == (r.Steps.Count + 1) * 16 && words[r.Steps.Count * 16] == 0);
        Check("  a short operation: the old program's 25 steps written (the rest 0)", PlcSteps.Words(PlcSteps.For(slot, false, null, 77, 65, mill10, F(0), feeds, s).Steps).Length == 400);
        r = PlcSteps.For(deep, false, null, 77, 65, mill10, F(0), feeds, new ProgramSettings { PeckDepth = 0.5, ApproachFeed = 3000, Material = "PVC" });
        Check("  in 0.5 mm passes: more steps than the PLC takes - said so", !r.Ok && r.Problem.Contains("the PLC takes 399"), r.Problem);
        r = PlcSteps.For(slot, false, null, 77, 65, mill10, F(45), feeds, s);
        Check("a face at 45 degrees: not made (the PLC's arcs are at 0, 90, 180, 270)", !r.Ok && r.Problem.Contains("45"), r.Problem);
        r = PlcSteps.For(slot, false, null, 77, 65, mill10, F(0), new List<Feed>(), s);
        Check("no feed for the material: not made, said where to set it", !r.Ok && r.Problem.Contains("Feeds"), r.Problem);

        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
