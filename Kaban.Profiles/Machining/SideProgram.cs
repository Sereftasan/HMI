using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Kaban.Profiles.Geometry;

namespace Kaban.Profiles.Machining
{
    /// <summary>A cutter mounted on the machine (machine layer: mounted_tool).</summary>
    public sealed class Tool
    {
        public long Id;
        public string Position = "";        // U1-T1
        public string Code = "";
        public string Type = "END_MILL";    // DRILL, END_MILL
        public double? Diameter;
        public double? UsableLength;
        public double? MaxRpm;
        public int Unit = 1;
        public bool Active = true;
        public override string ToString()
        {
            return Position + " " + Type + (Diameter.HasValue ? " D" + Diameter.Value.ToString("0.##", CultureInfo.InvariantCulture) : " (no diameter)");
        }
    }

    /// <summary>Which angle of which unit machines which face (machine layer: machine_face).</summary>
    public sealed class Face
    {
        public int Unit = 1;
        public string Side = "TOP";
        public double Angle;
        public bool Enabled = true;
    }

    /// <summary>Feeds for a milling class and a tool type (machine layer: feed_strategy).</summary>
    public sealed class Feed
    {
        public string MillingClass = "";
        public string ToolType = "";
        public double Entry, Cut;
        public double? Rpm, Plunge;
        public double ThinFactor = 1, ThinMm = 1.5;
    }

    /// <summary>A macro's shape (library: macro).</summary>
    public sealed class MacroShape
    {
        public string Code = "", Type = "";
        public Dictionary<string, object> Params = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public int ThroughProfile;
        public double? P(string key) { return FlatJson.Number(Params, key); }
        public string Describe()
        {
            switch (Type)
            {
                case "ROUND_HOLE": return "hole D" + F(P("diameter_mm")) + " x " + F(P("depth_mm"));
                case "SLOT": return "slot " + F(P("length_mm")) + " x " + F(P("width_mm")) + " x " + F(P("depth_mm"));
                case "POCKET": return "pocket " + F(P("length_mm")) + " x " + F(P("width_mm")) + " x " + F(P("depth_mm"));
                default: return (FlatJson.Text(Params, "path") ?? "contour").ToLowerInvariant() + " x " + F(P("depth_mm"));
            }
        }
        private static string F(double? v) { return v.HasValue ? v.Value.ToString("0.##", CultureInfo.InvariantCulture) : "?"; }
    }

    /// <summary>One resolved accessory operation on the profile, ready to machine.</summary>
    public sealed class SideOperation
    {
        public string Accessory = "";
        public int Seq;
        public MacroShape Macro = new MacroShape();
        public string Side = "TOP";
        /// <summary>The entry point in the section (Y across, Z up).</summary>
        public double Y, Z;
        public string Status = "AUTO";
        public string XStrategy = "SINGLE";
        public double? XBase, XPitch;
        public int? XCount;
        public double Rotation;
        public bool Mirror;
        public int DepthMode;
        public string Label { get { return Accessory + " op" + Seq + " " + Macro.Code; } }
    }

    /// <summary>Machine-wide settings of the side program.</summary>
    public sealed class ProgramSettings
    {
        public double SafeZ = 30;              // above the face the tool enters by
        public double ApproachFeed = 3000;     // through air inside the profile
        public double PeckDepth = 4;           // most a tool goes down in one pass
        public bool Incremental = true;        // G91 moves
        public string Material = "PVC";        // the series' material: PVC, ALU
        public double Breakthrough = 0.5;      // past the wall when cutting through
        /// <summary>DXF layer -> milling class (layer_milling_map of the series).</summary>
        public Dictionary<string, string> LayerClass = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Which tool, on which unit at which angle, and why.</summary>
    public sealed class ToolChoice
    {
        public Tool Tool;
        public Face Face;
        public string Why = "";
        public string Warning = "";
        public string Problem = "";
        public bool Ok { get { return Tool != null && Face != null && Problem.Length == 0; } }
    }

    /// <summary>
    /// THE SIDE MACHINING PROGRAM of one bar: every confirmed accessory operation of
    /// the profile, at its places along the bar, with the best mounted tool, its
    /// depth passes and its feeds following the walls the tool goes through.
    ///
    ///   Coordinates of each operation are in its FACE's frame: X along the bar,
    ///   Y across the face, Z0 the face of the profile's box the tool comes in by,
    ///   Z- into the profile. Which unit and angle machine that face is the
    ///   machine layer's (machine_face).
    ///
    ///   Plunging: rapid down to 1 mm above the first wall, then through each
    ///   WALL at its entry speed (slower still for a thin wall) and through the
    ///   chambers between at the approach feed. Cutting sideways: the feed of the
    ///   wall at that depth. That is "the entry speed from the drawing's levels":
    ///   the walls come from the profile's DXF, the speeds from the machine's feeds.
    ///
    /// A preview and export - nothing here is sent to the controller.
    /// </summary>
    public static class SideProgram
    {
        public sealed class Result
        {
            public string Code = "";
            public readonly List<string> Problems = new List<string>();
            public readonly List<string> Notes = new List<string>();
            public int Operations, Cuts;
            public bool Ok { get { return Problems.Count == 0; } }
        }

        // ---- where along the bar ----------------------------------------------

        /// <summary>
        /// The X positions of an operation on a bar of <paramref name="length"/>,
        /// with whether each is the mirrored one (the far end of FROM_BOTH_ENDS).
        /// </summary>
        public static List<KeyValuePair<double, bool>> Positions(SideOperation op, double length)
        {
            var list = new List<KeyValuePair<double, bool>>();
            double b = op.XBase ?? 0;
            int n = Math.Max(1, op.XCount ?? 1);
            switch ((op.XStrategy ?? "SINGLE").ToUpperInvariant())
            {
                case "FROM_END": list.Add(new KeyValuePair<double, bool>(length - b, op.Mirror)); break;
                case "CENTRED": list.Add(new KeyValuePair<double, bool>(length / 2 + b, false)); break;
                case "FROM_BOTH_ENDS":
                {
                    if (length - 2 * b <= 0) { list.Add(new KeyValuePair<double, bool>(length / 2, false)); break; }
                    int count = Math.Max(2, n);
                    for (int i = 0; i < count; i++)
                    {
                        double x = b + (length - 2 * b) * i / (count - 1);
                        list.Add(new KeyValuePair<double, bool>(x, op.Mirror && i == count - 1));
                    }
                    break;
                }
                case "DISTRIBUTED":
                {
                    double span = length - 2 * b;
                    if (span <= 0) { list.Add(new KeyValuePair<double, bool>(length / 2, false)); break; }
                    int count = n;
                    if (op.XPitch.HasValue && op.XPitch.Value > 0) count = Math.Max(count, (int)Math.Ceiling(span / op.XPitch.Value) + 1);
                    if (count < 2) { list.Add(new KeyValuePair<double, bool>(length / 2, false)); break; }
                    for (int i = 0; i < count; i++) list.Add(new KeyValuePair<double, bool>(b + span * i / (count - 1), false));
                    break;
                }
                default: list.Add(new KeyValuePair<double, bool>(b, false)); break;   // SINGLE, FROM_START
            }
            return list.Where(p => p.Key >= 0 && p.Key <= length).ToList();
        }

        // ---- which tool ----------------------------------------------------------

        /// <summary>
        /// The best mounted tool for a macro from a face: a drill of the hole's own
        /// size first, else an end mill - the largest that fits the slot or pocket
        /// (fewer passes), on a unit that can machine that face, long enough.
        /// </summary>
        public static ToolChoice Choose(MacroShape m, string side, IList<Tool> tools, IList<Face> faces, double depth)
        {
            var c = new ToolChoice();
            var units = faces.Where(f => f.Enabled && string.Equals(f.Side, side, StringComparison.OrdinalIgnoreCase)).ToList();
            if (units.Count == 0) { c.Problem = "no unit machines the " + side.ToLowerInvariant() + " face (Library > Machine faces)"; return c; }
            var usable = tools.Where(t => t.Active && units.Any(u => u.Unit == t.Unit)).ToList();
            if (usable.Count == 0) { c.Problem = "no active tool on the units that machine the " + side.ToLowerInvariant() + " face"; return c; }
            var missing = usable.Where(t => !t.Diameter.HasValue || t.Diameter.Value <= 0).ToList();
            usable = usable.Where(t => t.Diameter.HasValue && t.Diameter.Value > 0).ToList();
            if (usable.Count == 0)
            {
                c.Problem = "the tools' diameters are not set: " + string.Join(", ", missing.Select(t => t.Position)) + " (Library > Machine tools)";
                return c;
            }

            double best = double.MaxValue;
            string why = "", warn = "";
            foreach (var t in usable)
            {
                double d = t.Diameter.Value;
                double score; string w = "", y;
                bool drill = string.Equals(t.Type, "DRILL", StringComparison.OrdinalIgnoreCase);
                switch (m.Type)
                {
                    case "ROUND_HOLE":
                    {
                        double hole = m.P("diameter_mm") ?? 0;
                        if (hole <= 0) { c.Problem = m.Code + " has no diameter_mm"; return c; }
                        if (Math.Abs(d - hole) <= 0.05) { score = drill ? 0 : 1; y = (drill ? "a drill" : "an end mill") + " of the hole's size"; }
                        else if (!drill && d < hole) { score = 2 + (hole - d) / hole; y = "circular milling with the largest smaller end mill"; }
                        else continue;
                        break;
                    }
                    case "SLOT":
                    case "POCKET":
                    {
                        if (drill) continue;
                        double width = m.P("width_mm") ?? 0;
                        if (width <= 0) { c.Problem = m.Code + " has no width_mm"; return c; }
                        if (d > width + 0.01) continue;
                        score = Math.Abs(d - width) <= 0.05 ? 0 : 1 + (width - d) / width;
                        y = Math.Abs(d - width) <= 0.05 ? "an end mill of the " + m.Type.ToLowerInvariant() + "'s width" : "the largest end mill that fits the width";
                        double r = m.P("corner_r_mm") ?? 0;
                        if (m.Type == "POCKET" && r > 0 && d > 2 * r + 0.01) { score += 0.5; w = "its corners come out R" + (d / 2).ToString("0.##", CultureInfo.InvariantCulture) + ", not R" + r.ToString("0.##", CultureInfo.InvariantCulture); }
                        break;
                    }
                    default:
                    {
                        if (drill) continue;
                        double width = m.P("width_mm") ?? 0;
                        if (width > 0 && d > width + 0.01) continue;
                        score = width > 0 ? (Math.Abs(d - width) <= 0.05 ? 0 : 1 + (width - d) / width) : 1 + 1 / d;
                        y = width > 0 ? "the largest end mill that fits the groove" : "an end mill (the groove is as wide as the tool)";
                        break;
                    }
                }
                if (t.UsableLength.HasValue && t.UsableLength.Value > 0 && t.UsableLength.Value < depth)
                {
                    score += 10;
                    w = (w.Length > 0 ? w + "; " : "") + "its usable length " + t.UsableLength.Value.ToString("0.#", CultureInfo.InvariantCulture) + " mm is less than the depth " + depth.ToString("0.#", CultureInfo.InvariantCulture) + " mm";
                }
                if (score < best) { best = score; c.Tool = t; why = y; warn = w; }
            }
            if (c.Tool == null)
            {
                c.Problem = "no mounted tool can make " + m.Describe() + " from the " + side.ToLowerInvariant() + " face";
                return c;
            }
            c.Face = units.First(u => u.Unit == c.Tool.Unit);
            c.Why = why;
            c.Warning = warn;
            if (best >= 10) c.Problem = c.Warning;
            return c;
        }

        // ---- how deep, through what ----------------------------------------------

        /// <summary>Where an operation's cut starts and ends, and the walls on the way.</summary>
        public sealed class DepthInfo
        {
            /// <summary>Across the face: Y for the top and bottom, Z for the outside and inside.</summary>
            public double Across;
            /// <summary>From the face of the profile's box the tool comes in by: the first material, and the bottom of the cut.</summary>
            public double Surface, Bottom;
            /// <summary>Material and air along the tool's way (null: no drawing).</summary>
            public List<Section.Interval> Walls;
            public string Problem = "";
            public bool Ok { get { return Problem.Length == 0; } }

            /// <summary>"from 5 to 15 mm; walls 5-7.8 (2.8), 30-32.5 (2.5)" - for the page.</summary>
            public string Text
            {
                get
                {
                    if (!Ok) return Problem;
                    var t = "from " + N(Surface) + " to " + N(Bottom) + " mm";
                    if (Walls != null)
                    {
                        var cut = Walls.Where(w => w.Material && w.Start < Bottom && w.End > Surface).ToList();
                        t += cut.Count == 0 ? "; no wall" : "; " + (cut.Count == 1 ? "wall " : cut.Count + " walls ") +
                             string.Join(", ", cut.Select(w => N(w.Start) + "-" + N(Math.Min(w.End, Bottom)) + " (" + N(w.Thickness) + ")"));
                    }
                    return t;
                }
            }
        }

        /// <summary>
        /// The depth of an operation, the way the program cuts it: from the first
        /// material the tool meets going straight in at the placement, to the
        /// macro's depth below it - or through the first wall, or the whole
        /// profile (the operation's depth mode) plus the breakthrough.
        /// </summary>
        public static DepthInfo Depths(SideOperation op, Section section, double width, double height, double breakthrough)
        {
            var d = new DepthInfo();
            var side = Section.ParseSide(op.Side);
            d.Across = side == Section.Side.Top || side == Section.Side.Bottom ? op.Y : op.Z;
            // the entry point's distance from the face of the box it enters by
            double nominal;
            switch (side)
            {
                case Section.Side.Top: nominal = height - op.Z; break;
                case Section.Side.Bottom: nominal = op.Z; break;
                case Section.Side.Outside: nominal = op.Y; break;
                default: nominal = width - op.Y; break;
            }
            d.Walls = section != null ? section.Ray(side, d.Across) : null;
            var mat = d.Walls != null ? d.Walls.Where(w => w.Material).ToList() : null;
            if (d.Walls != null && mat.Count == 0)
            {
                d.Problem = "the " + (op.Side ?? "TOP").ToLowerInvariant() + " face has no material at " + N(d.Across) + " mm - the operation is off the profile";
                return d;
            }
            d.Surface = mat != null ? mat[0].Start : Math.Max(0, nominal);
            double depthMacro = op.Macro.P("depth_mm") ?? 0;
            int mode = op.DepthMode > 0 ? op.DepthMode : (op.Macro.ThroughProfile == 1 ? 1 : 0);
            if (mode == 1 || mode == 2)
            {
                if (mat == null) { d.Problem = "cutting through the " + (mode == 1 ? "first wall" : "profile") + " needs the profile's drawing"; return d; }
                d.Bottom = (mode == 1 ? mat[0].End : mat[mat.Count - 1].End) + breakthrough;
            }
            else
            {
                if (depthMacro <= 0) { d.Problem = op.Macro.Code + " has no depth_mm"; return d; }
                d.Bottom = d.Surface + depthMacro;
            }
            return d;
        }

        // ---- the program ---------------------------------------------------------

        public static Result Generate(string profileCode, double barLength, IList<SideOperation> ops, Section section,
                                      double width, double height, IList<Tool> tools, IList<Face> faces, IList<Feed> feeds,
                                      ProgramSettings s, bool trial)
        {
            var r = new Result();
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            var take = ops.Where(o => trial ? o.Status != "REJECTED" : (o.Status == "CONFIRMED" || o.Status == "OVERRIDDEN")).ToList();
            int left = ops.Count(o => o.Status != "REJECTED") - take.Count;

            sb.AppendLine("(KABAN - SIDE MACHINING PROGRAM)");
            sb.AppendLine("(PROFILE " + Clean(profileCode) + "   BAR " + N(barLength) + " MM   " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", ci) + ")");
            if (trial) sb.AppendLine("(TRIAL PROGRAM - IT INCLUDES POSITIONS NOBODY HAS CONFIRMED - FOR A TEST PIECE ONLY)");
            else if (left > 0) r.Notes.Add(left + " operation(s) are not confirmed yet and are not in the program (a trial program includes them).");
            sb.AppendLine("(EACH OPERATION IN ITS FACE'S FRAME: X ALONG THE BAR, Y ACROSS THE FACE, Z0 AT THE FACE, Z- INTO THE PROFILE)");
            sb.AppendLine(s.Incremental ? "(INCREMENTAL: EVERY MOVE FROM WHERE THE LAST ENDED; THE PROGRAM STARTS AT X0 Y0 AT SAFE HEIGHT)" : "(ABSOLUTE)");
            sb.AppendLine("G21 G17");
            sb.AppendLine(s.Incremental ? "G91" : "G90");
            if (take.Count == 0) r.Notes.Add("Nothing to machine" + (trial ? "." : ": no operation is confirmed."));
            if (section == null) r.Notes.Add("No drawing: every operation's depth is measured from its entry point, and its feeds cannot follow the walls.");

            var pos = new double[3];          // x, y, z (z relative to the face)
            string lastFrame = null;
            int n = 0;
            foreach (var op in take)
            {
                n++;
                var dep = Depths(op, section, width, height, s.Breakthrough);
                if (!dep.Ok) { r.Problems.Add(op.Label + ": " + dep.Problem); continue; }
                double across = dep.Across, surface = dep.Surface, bottom = dep.Bottom;
                var walls = dep.Walls;
                var mat = walls != null ? walls.Where(w => w.Material).ToList() : null;

                var choice = Choose(op.Macro, op.Side.ToUpperInvariant(), tools, faces, bottom - surface);
                if (!choice.Ok) { r.Problems.Add(op.Label + ": " + choice.Problem); continue; }
                var tool = choice.Tool;
                double D = tool.Diameter.Value;

                // feeds of each wall the tool meets
                Func<Section.Interval, int, Feed> feedOf = (iv, index) =>
                {
                    string cls = null;
                    if (iv != null && !string.IsNullOrEmpty(iv.Layer)) s.LayerClass.TryGetValue(iv.Layer, out cls);
                    if (cls == null) cls = s.Material + (index == 0 ? "_OUTER_WALL" : "_CHAMBER");
                    return feeds.FirstOrDefault(f => string.Equals(f.MillingClass, cls, StringComparison.OrdinalIgnoreCase) &&
                                                     string.Equals(f.ToolType, tool.Type, StringComparison.OrdinalIgnoreCase))
                        ?? feeds.FirstOrDefault(f => string.Equals(f.MillingClass, s.Material + "_OUTER_WALL", StringComparison.OrdinalIgnoreCase) &&
                                                     string.Equals(f.ToolType, tool.Type, StringComparison.OrdinalIgnoreCase));
                };
                var bands = new List<Band>();
                if (mat != null)
                {
                    for (int i = 0; i < walls.Count; i++)
                    {
                        var iv = walls[i];
                        if (iv.End <= surface - 1e-9) continue;
                        int index = mat.IndexOf(iv);
                        var f = iv.Material ? feedOf(iv, index) : null;
                        bands.Add(new Band { Start = Math.Max(surface, iv.Start), End = iv.End, Material = iv.Material, Feed = f, Wall = iv.Thickness, Layer = iv.Layer });
                    }
                    if (bands.Count == 0 || bands[bands.Count - 1].End < bottom)
                        bands.Add(new Band { Start = bands.Count > 0 ? bands[bands.Count - 1].End : surface, End = bottom + 1, Material = false });
                }
                else bands.Add(new Band { Start = surface, End = bottom + 1, Material = true, Feed = feedOf(null, 0), Wall = double.MaxValue });
                if (bands.Any(b => b.Material && b.Start < bottom && b.Feed == null))
                {
                    r.Problems.Add(op.Label + ": no feed for " + s.Material + " with a " + tool.Type + " (Library > Feeds)");
                    continue;
                }
                var firstFeed = bands.First(b => b.Material).Feed;
                double rpm = firstFeed.Rpm ?? 0;
                if (tool.MaxRpm.HasValue && tool.MaxRpm.Value > 0 && rpm > tool.MaxRpm.Value) rpm = tool.MaxRpm.Value;

                sb.AppendLine();
                sb.AppendLine("(" + n + ": " + Clean(op.Label) + " - " + Clean(op.Macro.Describe()) + (op.Status == "CONFIRMED" ? "" : " - " + op.Status) + ")");
                sb.AppendLine("(   " + op.Side.ToUpperInvariant() + " FACE, UNIT " + choice.Face.Unit + " AT " + N(choice.Face.Angle) + " DEG, " + Clean(tool.ToString()) + " - " + Clean(choice.Why).ToUpperInvariant() + ")");
                if (choice.Warning.Length > 0) { sb.AppendLine("(   WARNING: " + Clean(choice.Warning).ToUpperInvariant() + ")"); r.Notes.Add(op.Label + ": " + choice.Warning); }
                sb.AppendLine("(   SURFACE Z-" + N(surface) + ", BOTTOM Z-" + N(bottom) + (mat != null ? ", WALLS " + string.Join(" ", bands.Where(b => b.Material).Select(b => N(b.Start) + "-" + N(b.End))) : "") + ")");

                string frame = op.Side.ToUpperInvariant() + "|" + choice.Face.Unit + "|" + tool.Position;
                if (frame != lastFrame)
                {
                    // a new face or tool: from safe height, as the program starts
                    if (lastFrame != null) Move(sb, s, pos, "G0", null, null, s.SafeZ, 0);
                    sb.AppendLine("M3 S" + ((int)Math.Round(rpm)).ToString(ci));
                    lastFrame = frame;
                }

                foreach (var p in Positions(op, barLength))
                {
                    var path = Path(op, D, p.Value);
                    if (path == null) { r.Problems.Add(op.Label + ": " + op.Macro.Type + " (" + (FlatJson.Text(op.Macro.Params, "path") ?? "") + ") is not generated - draw it as its own program"); break; }
                    r.Cuts++;
                    sb.AppendLine("(   AT X" + N(p.Key) + (p.Value ? " MIRRORED" : "") + ")");
                    double cx = p.Key, cy = across;
                    // above the start of the path, at safe height
                    Move(sb, s, pos, "G0", cx + path.Start.X, cy + path.Start.Y, s.SafeZ, 0);
                    Move(sb, s, pos, "G0", null, null, -(surface - 1), 0);
                    double level = surface - 1;
                    double step = Math.Max(0.1, s.PeckDepth);
                    while (level < bottom - 1e-6)
                    {
                        double next = Math.Min(bottom, Math.Max(surface, level) + step);
                        if (level < surface) next = Math.Min(bottom, surface + step);
                        // down through air and walls, each at its own speed
                        foreach (var b in bands)
                        {
                            double a = Math.Max(level, b.Start), e = Math.Min(next, b.End);
                            if (e <= a + 1e-9) continue;
                            double f = b.Material ? Plunge(b, tool) : s.ApproachFeed;
                            Move(sb, s, pos, "G1", null, null, -e, f);
                        }
                        level = next;
                        // sideways at this depth: the wall's cutting feed
                        var at = bands.FirstOrDefault(b => b.Material && b.Start < level + 1e-9 && level <= b.End + 1e-9)
                                 ?? bands.LastOrDefault(b => b.Material && b.Start <= level) ?? bands.First(b => b.Material);
                        double cut = at.Feed.Cut * (at.Wall < at.Feed.ThinMm ? at.Feed.ThinFactor : 1);
                        foreach (var seg in path.Moves)
                        {
                            if (seg.Arc)
                                Arc(sb, s, pos, cx + seg.X, cy + seg.Y, seg.I, seg.J, cut);
                            else Move(sb, s, pos, "G1", cx + seg.X, cy + seg.Y, null, cut);
                        }
                        // back to the start of the path for the next pass (a slot ends at its far end)
                        if (level < bottom - 1e-6 && path.Moves.Count > 0)
                            Move(sb, s, pos, "G1", cx + path.Start.X, cy + path.Start.Y, null, cut);
                    }
                    Move(sb, s, pos, "G0", null, null, s.SafeZ, 0);
                }
                r.Operations++;
            }
            sb.AppendLine();
            sb.AppendLine("M5");
            if (s.Incremental) Move(sb, s, pos, "G0", 0, 0, s.SafeZ, 0);
            else sb.AppendLine("G0 X0 Y0");
            sb.AppendLine("M30");
            r.Code = sb.ToString();
            return r;
        }

        private sealed class Band
        {
            public double Start, End, Wall;
            public bool Material;
            public Feed Feed;
            public string Layer;
        }

        private static double Plunge(Band b, Tool t)
        {
            double f = string.Equals(t.Type, "DRILL", StringComparison.OrdinalIgnoreCase) && b.Feed.Plunge.HasValue ? b.Feed.Plunge.Value : b.Feed.Entry;
            return b.Wall < b.Feed.ThinMm ? f * b.Feed.ThinFactor : f;
        }

        internal struct Pt2 { public double X, Y; public Pt2(double x, double y) { X = x; Y = y; } }
        /// <summary>A move to (X, Y) - an arc (G2, clockwise in the face's frame) about the point I, J from where it starts.</summary>
        internal sealed class Seg { public double X, Y, I, J; public bool Arc; }
        internal sealed class ToolPath { public Pt2 Start; public readonly List<Seg> Moves = new List<Seg>(); }

        /// <summary>
        /// The tool centre's path of one pass, relative to the operation's centre
        /// (X along the bar, Y across the face): a hole is a point or a circle, a
        /// slot and a pocket are lines side by side (stepping 60 % of the tool) and
        /// a pocket's outline.
        /// </summary>
        internal static ToolPath Path(SideOperation op, double D, bool mirrored)
        {
            var m = op.Macro;
            double rot = (op.Rotation + (m.P("angle_deg") ?? 0)) * Math.PI / 180;
            if (mirrored) rot = Math.PI - rot;
            Func<double, double, Pt2> T = (u, v) => new Pt2(u * Math.Cos(rot) - v * Math.Sin(rot), u * Math.Sin(rot) + v * Math.Cos(rot));
            var p = new ToolPath();
            switch (m.Type)
            {
                case "ROUND_HOLE":
                {
                    double hole = m.P("diameter_mm") ?? D;
                    double rad = (hole - D) / 2;
                    if (rad <= 0.025) { p.Start = new Pt2(0, 0); return p; }
                    p.Start = new Pt2(rad, 0);
                    p.Moves.Add(new Seg { X = rad, Y = 0, I = -rad, J = 0, Arc = true });   // a full circle
                    return p;
                }
                case "SLOT":
                case "POCKET":
                {
                    double len = m.P("length_mm") ?? D, wid = m.P("width_mm") ?? D;
                    double lc = Math.Max(0, len - D) / 2, wc = Math.Max(0, wid - D) / 2;
                    int lanes = wc <= 0.01 ? 1 : (int)Math.Ceiling(2 * wc / (0.6 * D)) + 1;
                    var pts = new List<Pt2>();
                    for (int i = 0; i < lanes; i++)
                    {
                        double v = lanes == 1 ? 0 : -wc + 2 * wc * i / (lanes - 1);
                        if (i % 2 == 0) { pts.Add(T(-lc, v)); pts.Add(T(lc, v)); }
                        else { pts.Add(T(lc, v)); pts.Add(T(-lc, v)); }
                    }
                    if (m.Type == "POCKET" && wc > 0.01 && lc > 0.01)
                    {
                        // the outline once more, clean
                        pts.Add(T(-lc, -wc)); pts.Add(T(lc, -wc)); pts.Add(T(lc, wc)); pts.Add(T(-lc, wc)); pts.Add(T(-lc, -wc));
                    }
                    p.Start = pts[0];
                    foreach (var q in pts.Skip(1)) p.Moves.Add(new Seg { X = q.X, Y = q.Y });
                    return p;
                }
                default:
                {
                    string path = (FlatJson.Text(m.Params, "path") ?? "").ToUpperInvariant();
                    if (path != "LINE") return null;
                    double len = m.P("length_mm") ?? 0;
                    if (len <= 0) len = D;
                    var a = T(-len / 2, 0); var b = T(len / 2, 0);
                    p.Start = a;
                    p.Moves.Add(new Seg { X = b.X, Y = b.Y });
                    return p;
                }
            }
        }

        private static void Move(StringBuilder sb, ProgramSettings s, double[] pos, string g, double? x, double? y, double? z, double feed)
        {
            var parts = new List<string> { g };
            Action<int, double?, string> axis = (i, v, name) =>
            {
                if (!v.HasValue) return;
                double target = v.Value;
                if (Math.Abs(target - pos[i]) < 1e-6 && s.Incremental) return;
                parts.Add(name + N(s.Incremental ? target - pos[i] : target));
                pos[i] = target;
            };
            axis(0, x, "X"); axis(1, y, "Y"); axis(2, z, "Z");
            if (parts.Count == 1) return;
            if (g != "G0" && feed > 0) parts.Add("F" + ((int)Math.Round(feed)).ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(string.Join(" ", parts));
        }

        private static void Arc(StringBuilder sb, ProgramSettings s, double[] pos, double x, double y, double i, double j, double feed)
        {
            double dx = s.Incremental ? x - pos[0] : x, dy = s.Incremental ? y - pos[1] : y;
            sb.AppendLine("G2 X" + N(dx) + " Y" + N(dy) + " I" + N(i) + " J" + N(j) + " F" + ((int)Math.Round(feed)).ToString(CultureInfo.InvariantCulture));
            pos[0] = x; pos[1] = y;
        }

        private static string N(double v)
        {
            if (Math.Abs(v) < 0.0005) v = 0;
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Clean(string s)
        {
            return (s ?? "").Replace("(", "[").Replace(")", "]");
        }
    }
}
