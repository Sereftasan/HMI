using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kaban.Profiles.Geometry;

namespace Kaban.Profiles.Machining
{
    /// <summary>One step of a milling operation as the PLC takes it (FA2010: 16 words each, R0 / R12800).</summary>
    public sealed class PlcStep
    {
        /// <summary>
        /// The movement code (FA2010_DeviceList, "hareket kodları"): 1 a straight move without the end check (in and out of the profile),
        /// 11 one with it (cutting), 2 / 3 an arc clockwise / counter-clockwise at 0 and 180 degrees (X, Y, I, J), 4 / 5 at 90 and 270
        /// (X, Z, I, K); 0 - after the last step - the end.
        /// </summary>
        public int Code;
        /// <summary>From where the step before ended (mm); I, J, K the arc's centre from where it starts.</summary>
        public double X, Y, Z, I, J, K;
        /// <summary>m/min.</summary>
        public double Speed;
        public string What = "";

        public override string ToString()
        {
            var ci = CultureInfo.InvariantCulture;
            return string.Format(ci, "{0} X{1:0.##} Y{2:0.##} Z{3:0.##}{4} F{5:0.##}{6}", Code, X, Y, Z,
                Code >= 2 && Code <= 5 ? string.Format(ci, " I{0:0.##} J{1:0.##} K{2:0.##}", I, J, K) : "", Speed, What.Length > 0 ? "  (" + What + ")" : "");
        }
    }

    /// <summary>
    /// A MILLING OPERATION'S STEPS FOR THE PLC (6 November: the movement codes of FA2010_DeviceList - "0 means the last line"): the
    /// tool's path the side program makes (SideProgram.Path - a hole, a slot's lanes, a pocket and its outline), its depth passes, as the
    /// old program's GCodeConverter wrote them:
    ///
    ///   - every step a move from where the one before ended (incremental), its arcs' centres from where they start;
    ///   - in and out of the profile without the end check (code 1), cutting with it (11), arcs 2 / 3 at 0 and 180 degrees, 4 / 5 at 90 and 270;
    ///   - the speed in m/min (the old program's milling speed: teeth x feed per tooth x rpm / 1000) - the feeds of the library are mm/min.
    ///
    /// Where it starts (the operation's APPROACH point - its milling parameters' Profile Approach Y / Z): over the operation's centre
    /// along the bar (its position in the job list), across the face at the operation's place, 1 mm above the first material the tool
    /// meets (the side program's rapid level). Then: to where the path starts, down into the profile pass by pass (the side program's peck
    /// depth), along the path each pass, out to the approach level. The face's angle turns the face's frame (along the bar, across, into
    /// the profile) into the machine's X, Y, Z as the old program turned its steps (Machine).
    ///
    /// To be tried on a test piece (Trial): the old program's steps came from its own drawings of the accessories; these from the library's
    /// macros - where an operation starts and which way each axis runs are to be seen on the machine.
    /// </summary>
    public static class PlcSteps
    {
        public sealed class Result
        {
            public readonly List<PlcStep> Steps = new List<PlcStep>();
            /// <summary>The approach point in the section (mm): the operation's Profile Approach Y / Z.</summary>
            public double ApproachY, ApproachZ;
            public string Problem = "";
            public bool Ok { get { return Problem.Length == 0; } }
        }

        /// <summary>
        /// The PLC's area of a unit (R0 / R12800, FA2010_DeviceList's "ÜNİTE1 FREZE ADIMLARI" 1 - 400) takes 400 steps: the last place is the
        /// end's (code 0).
        /// </summary>
        public const int MaxSteps = 399;
        /// <summary>The old program wrote 25 steps (400 words) whatever the operation - at least that much is written, the rest 0.</summary>
        public const int MinRoom = 25;
        public const int WordsPerStep = 16;
        private const double Rad = Math.PI / 180;

        /// <summary>The steps of <paramref name="op"/> with <paramref name="tool"/> from <paramref name="face"/>.</summary>
        public static Result For(SideOperation op, bool mirrored, Section section, double width, double height, Tool tool, Face face,
                                 IList<Feed> feeds, ProgramSettings s)
        {
            var r = new Result();
            if (tool == null || face == null || !tool.Diameter.HasValue) { r.Problem = "no tool"; return r; }
            double a = face.Angle % 360;
            if (a < 0) a += 360;
            if (Math.Abs(a % 90) > 1e-6) { r.Problem = "the face's angle " + face.Angle.ToString("0.#", CultureInfo.InvariantCulture) + " is not 0, 90, 180 or 270"; return r; }
            var dep = SideProgram.Depths(op, section, width, height, s.Breakthrough);
            if (!dep.Ok) { r.Problem = dep.Problem; return r; }
            var path = SideProgram.Path(op, tool.Diameter.Value, mirrored);
            if (path == null) { r.Problem = op.Macro.Type + " (" + (FlatJson.Text(op.Macro.Params, "path") ?? "") + ") is not made into steps"; return r; }

            // the approach point: 1 mm above the first material (outside the profile's box when the material is its face), across the
            // face at the operation's place
            double above = dep.Surface - 1;
            switch (Section.ParseSide(op.Side))
            {
                case Section.Side.Top: r.ApproachY = dep.Across; r.ApproachZ = height - above; break;
                case Section.Side.Bottom: r.ApproachY = dep.Across; r.ApproachZ = above; break;
                case Section.Side.Outside: r.ApproachY = above; r.ApproachZ = dep.Across; break;
                default: r.ApproachY = width - above; r.ApproachZ = dep.Across; break;
            }

            // the feeds (mm/min in the library -> m/min)
            string material = string.IsNullOrEmpty(s.Material) ? "PVC" : s.Material;
            var feed = feeds.FirstOrDefault(f => string.Equals(f.MillingClass, material + "_OUTER_WALL", StringComparison.OrdinalIgnoreCase) &&
                                                 string.Equals(f.ToolType, tool.Type, StringComparison.OrdinalIgnoreCase));
            if (feed == null) { r.Problem = "no feed for " + material + " with a " + tool.Type + " (Library > Feeds)"; return r; }
            double cut = feed.Cut / 1000, idle = s.ApproachFeed / 1000;
            double plunge = (string.Equals(tool.Type, "DRILL", StringComparison.OrdinalIgnoreCase) && feed.Plunge.HasValue ? feed.Plunge.Value : feed.Entry) / 1000;
            if (cut <= 0 || plunge <= 0) { r.Problem = "the feed for " + material + " with a " + tool.Type + " is 0"; return r; }

            // in the face's frame: u along the bar, v across, w into the profile from the approach level
            double u = 0, v = 0, w = 0;
            Action<int, double, double, double, double, double, double, string> step = (code, nu, nv, nw, ci, cj, speed, what) =>
            {
                double du = nu - u, dv = nv - v, dw = nw - w;
                if (code != 2 && Math.Abs(du) < 1e-6 && Math.Abs(dv) < 1e-6 && Math.Abs(dw) < 1e-6) return;
                r.Steps.Add(Machine(code, du, dv, dw, ci, cj, a, speed, what));
                u = nu; v = nv; w = nw;
            };
            double total = dep.Bottom - dep.Surface + (dep.Surface - above);   // from the approach level to the bottom
            double peck = Math.Max(0.1, s.PeckDepth);
            bool drill = path.Moves.Count == 0;
            if (Math.Abs(path.Start.X) > 1e-6 || Math.Abs(path.Start.Y) > 1e-6) step(11, path.Start.X, path.Start.Y, 0, 0, 0, idle, "to the start");
            double level = 0;
            // through the air to 1 mm above the material at the approach speed, then pass by pass
            int guard = 0;
            while (level < total - 1e-6 && guard++ < 200)
            {
                double next = drill ? total : Math.Min(total, (level < 1 ? 1 : level) + peck);
                step(1, path.Start.X, path.Start.Y, next, 0, 0, plunge, "in");
                level = next;
                foreach (var m in path.Moves)
                {
                    if (m.Arc) step(2, m.X, m.Y, w, m.I, m.J, cut, "arc");     // the side program's G2: clockwise in the face's frame
                    else step(11, m.X, m.Y, w, 0, 0, cut, "cut");
                }
                if (level < total - 1e-6 && path.Moves.Count > 0) step(11, path.Start.X, path.Start.Y, w, 0, 0, cut, "back to the start");
            }
            step(1, u, v, 0, 0, 0, idle, "out");
            if (r.Steps.Count > MaxSteps)
                r.Problem = r.Steps.Count + " steps - the PLC takes " + MaxSteps + " (a deeper peck depth, the side program's settings, makes fewer passes)";
            return r;
        }

        /// <summary>
        /// A step from the face's frame into the machine's, AS THE OLD PROGRAM TURNED ITS STEPS (GCodeConverter: x *= -1, I *= -1;
        /// GCodeProgramCreator.CodeProgramByAngle; the surface strategies) - the PLC was made with what it wrote:
        ///
        ///   X = - along the bar
        ///   Y = depth sin a + across cos a,  Z = depth cos a - across sin a   (depth into the profile, positive; a the unit's angle)
        ///   at 0 degrees Y the other way (its UpStrategy), at 270 Z the other way (its LeftStrategy)
        ///   an arc: I = - its centre along, J (0 / 180) or K (90 / 270) = its centre across; clockwise in the face's frame 3 (0 / 180)
        ///   or 5 (90 / 270), counter-clockwise 2 or 4 - as it wrote them.
        /// </summary>
        private static PlcStep Machine(int code, double du, double dv, double dw, double ci, double cj, double angle, double speed, string what)
        {
            double sa = Math.Round(Math.Sin(angle * Rad), 12), ca = Math.Round(Math.Cos(angle * Rad), 12);
            bool side = Math.Abs(sa) > 0.5;          // 90, 270: arcs in X, Z
            double y = dw * sa + dv * ca, z = dw * ca - dv * sa;
            if (Math.Abs(angle) < 1e-6) y = -y;
            if (Math.Abs(angle - 270) < 1e-6) z = -z;
            var p = new PlcStep { X = -du, Y = y, Z = z, Speed = speed, What = what, Code = code };
            if (code == 2)
            {
                // the path's arcs are clockwise in the face's frame
                p.Code = side ? 5 : 3;
                p.I = -ci;
                if (side) p.K = cj; else p.J = cj;
            }
            return p;
        }

        /// <summary>The words a unit's area gets for these steps: the steps and the end (at least the old program's 25 steps).</summary>
        public static short[] Words(IList<PlcStep> steps)
        {
            return Words(steps, Math.Min(MaxSteps + 1, Math.Max(MinRoom, steps.Count + 1)));
        }

        /// <summary>The steps as the PLC's words: 16 a step (code; X, Y, Z, I, J, K, speed x100 as double words), the rest 0 - the end.</summary>
        public static short[] Words(IList<PlcStep> steps, int stepsRoom)
        {
            var w = new short[Math.Max(1, stepsRoom) * WordsPerStep];
            int n = 0;
            foreach (var s in steps.Take(stepsRoom - 1))
            {
                int o = n * WordsPerStep;
                w[o] = (short)s.Code;
                Dw(w, o + 1, s.X); Dw(w, o + 3, s.Y); Dw(w, o + 5, s.Z);
                Dw(w, o + 7, s.I); Dw(w, o + 9, s.J); Dw(w, o + 11, s.K);
                Dw(w, o + 13, s.Speed);
                n++;
            }
            return w;
        }

        private static void Dw(short[] w, int at, double v)
        {
            if (Math.Abs(v) < 1e-6) v = 0;
            int i = (int)Math.Round(v * 100, MidpointRounding.AwayFromZero);
            w[at] = (short)(i & 0xFFFF);
            w[at + 1] = (short)(i >> 16);
        }
    }
}
