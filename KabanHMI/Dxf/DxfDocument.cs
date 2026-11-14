using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Kaban.UI.Dxf
{
    /// <summary>
    /// A profile drawing as the Drawing pane edits it: every entity as a polyline
    /// (arcs in steps of 10 degrees), each with its LAYER, plus the points and texts
    /// on it (Markers). Read by the profile library's DXF reader
    /// (Kaban.Profiles.Geometry.DxfReader): lines, arcs, circles, ellipses, both
    /// kinds of polyline with their bulges, splines, inserted blocks.
    ///
    /// Paths on reference layers (REF_...), annotation layers (dimensions, texts,
    /// hatching) and the profile's ignored layers are drawn but are not MATERIAL:
    /// the drawing's extent - and so its origin - is the material's.
    /// </summary>
    public class DxfDocument
    {
        public List<List<PointD>> Polylines { get; } = new List<List<PointD>>();

        public struct PointD { public double X, Y; public PointD(double x, double y) { X = x; Y = y; } }

        /// <summary>A point or text on the drawing (a reference can be written on it).</summary>
        public sealed class Marker
        {
            public string Kind = "", Text = "", Layer = "0";
            public double X, Y;
        }

        public List<Marker> Markers { get; private set; } = new List<Marker>();

        public static DxfDocument Load(string path) { using (var r = new StreamReader(path)) return Parse(r); }

        public static DxfDocument Parse(TextReader reader)
        {
            var d = Kaban.Profiles.Geometry.DxfReader.Read(reader);
            var doc = new DxfDocument();
            foreach (var p in d.Paths)
            {
                if (p.Points.Count < 2) continue;
                var pts = new List<PointD>();
                foreach (var q in p.Points) pts.Add(new PointD(q.X, q.Y));
                doc.Polylines.Add(pts);
                doc.Meta.Add(new ElementMeta
                {
                    Layer = p.Layer,
                    Material = !Kaban.Profiles.Geometry.Section.IsReferenceLayer(p.Layer) &&
                               !Kaban.Profiles.Geometry.Section.LooksLikeAnnotation(p.Layer)
                });
            }
            foreach (var m in d.Markers) doc.Markers.Add(new Marker { Kind = m.Kind, Text = m.Text, Layer = m.Layer, X = m.X, Y = m.Y });
            return doc;
        }

        /// <summary>
        /// The drawing of a profile SECTION as the library sees it - turned, placed
        /// at 0,0, ignored layers marked - with X across (the section's Y) and Y up
        /// (its Z), so what is drawn is what the references and the rays measure.
        /// </summary>
        public static DxfDocument FromSection(Kaban.Profiles.Geometry.Section section)
        {
            var doc = new DxfDocument();
            if (section == null) return doc;
            foreach (var p in section.Paths)
            {
                var pts = new List<PointD>();
                foreach (var q in p.Points) pts.Add(new PointD(q.Y, q.Z));
                doc.Polylines.Add(pts);
                doc.Meta.Add(new ElementMeta { Layer = p.Layer, Material = p.IsMaterial });
            }
            foreach (var m in section.Markers) doc.Markers.Add(new Marker { Kind = m.Kind, Text = m.Text, Layer = m.Layer, X = m.Y, Y = m.Z });
            return doc;
        }

        /// <summary>Is the element part of the profile (not a reference, a dimension or an ignored layer)?</summary>
        public bool IsMaterial(int i)
        {
            EnsureMeta();
            return i >= 0 && i < Meta.Count && Meta[i].Material;
        }

        public string LayerOf(int i)
        {
            EnsureMeta();
            return i >= 0 && i < Meta.Count ? Meta[i].Layer : "0";
        }

        public void GetBounds(out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = minY = double.MaxValue; maxX = maxY = double.MinValue;
            EnsureMeta();
            bool anyMaterial = Meta.Exists(m => m.Material);
            for (int i = 0; i < Polylines.Count; i++)
                foreach (var pt in Polylines[i])
                {
                    if (anyMaterial && !Meta[i].Material) continue;     // the profile's extent, not its dimensions'

                    if (pt.X < minX) minX = pt.X; if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y < minY) minY = pt.Y; if (pt.Y > maxY) maxY = pt.Y;
                }
            if (minX == double.MaxValue) { minX = minY = maxX = maxY = 0; }
        }

        /// <summary>Rotate all geometry 90 deg CW about the origin.</summary>
        public void Rotate90CW()
        {
            for (int i = 0; i < Polylines.Count; i++)
                for (int j = 0; j < Polylines[i].Count; j++)
                { var pt = Polylines[i][j]; Polylines[i][j] = new PointD(pt.Y, -pt.X); }
            foreach (var m in Markers) { var pt = new PointD(m.X, m.Y); var q = new PointD(pt.Y, -pt.X); m.X = q.X; m.Y = q.Y; }
        }

        /// <summary>Rotate all geometry 90 deg CCW about the origin.</summary>
        public void Rotate90()
        {
            for (int i = 0; i < Polylines.Count; i++)
                for (int j = 0; j < Polylines[i].Count; j++)
                { var pt = Polylines[i][j]; Polylines[i][j] = new PointD(-pt.Y, pt.X); }
            foreach (var m in Markers) { var pt = new PointD(m.X, m.Y); var q = new PointD(-pt.Y, pt.X); m.X = q.X; m.Y = q.Y; }
        }

        /// <summary>Mirror all geometry over the Y axis.</summary>
        public void MirrorX()
        {
            for (int i = 0; i < Polylines.Count; i++)
                for (int j = 0; j < Polylines[i].Count; j++)
                { var pt = Polylines[i][j]; Polylines[i][j] = new PointD(-pt.X, pt.Y); }
            foreach (var m in Markers) { var pt = new PointD(m.X, m.Y); var q = new PointD(-pt.X, pt.Y); m.X = q.X; m.Y = q.Y; }
        }

        public static double Distance(PointD a, PointD b)
        { double dx = b.X - a.X, dy = b.Y - a.Y; return Math.Sqrt(dx * dx + dy * dy); }

        /// <summary>
        /// Machining route along the outer border: the longest CLOSED polyline
        /// (the profile's outer contour) as an ordered point list a head can follow.
        /// </summary>
        /// <summary>
        /// Per-element bookkeeping, kept index-aligned with Polylines.
        ///   Added   - drawn by the operator, not present in the source DXF
        ///   Deleted - removed from the cut but still SHOWN faintly, so the operator
        ///             can see what was taken out (and undo it)
        ///   Depth   - optional per-element cutting depth (null = use the global one)
        ///   Included- false leaves the element in the drawing but out of the route
        /// </summary>
        public class ElementMeta
        {
            public bool Added;
            public bool Deleted;

            /// <summary>False leaves the element in the drawing but out of the cut.</summary>
            public bool Included = true;

            /// <summary>Cut depth at the START of the element (null = the global one).</summary>
            public double? Depth;

            /// <summary>Cut depth at the END, so a cut can taper along an element.</summary>
            public double? DepthEnd;

            /// <summary>
            /// Start/end OFFSET along the element: negative begins the cut earlier
            /// (before the element's own start), positive later. The end offset
            /// works the same way at the far end, so a cut can overrun or stop short.
            /// </summary>
            public double StartOffset;
            public double EndOffset;

            /// <summary>The DXF layer it was drawn on.</summary>
            public string Layer = "0";

            /// <summary>Part of the profile (false: a reference, a dimension, an ignored layer).</summary>
            public bool Material = true;

            public ElementMeta Copy()
            {
                return new ElementMeta
                {
                    Added = Added, Deleted = Deleted, Included = Included,
                    Depth = Depth, DepthEnd = DepthEnd,
                    StartOffset = StartOffset, EndOffset = EndOffset,
                    Layer = Layer, Material = Material
                };
            }
        }

        public List<ElementMeta> Meta { get; private set; } = new List<ElementMeta>();

        /// <summary>Keeps Meta the same length as Polylines (files load without it).</summary>
        public void EnsureMeta()
        {
            while (Meta.Count < Polylines.Count) Meta.Add(new ElementMeta());
            while (Meta.Count > Polylines.Count) Meta.RemoveAt(Meta.Count - 1);
        }

        public bool IsAdded(int i) { EnsureMeta(); return i >= 0 && i < Meta.Count && Meta[i].Added; }
        public bool IsDeleted(int i) { EnsureMeta(); return i >= 0 && i < Meta.Count && Meta[i].Deleted; }

        /// <summary>Elements that take part in the cut.</summary>
        public bool IsActive(int i)
        {
            EnsureMeta();
            return i >= 0 && i < Meta.Count && !Meta[i].Deleted && Meta[i].Included && Meta[i].Material;
        }

        /// <summary>Sets (or clears with null) the cutting depth of one element.</summary>
        public void SetElementDepth(int i, double? depth)
        {
            EnsureMeta();
            if (i >= 0 && i < Meta.Count) Meta[i].Depth = depth;
        }

        /// <summary>Deep copy - the unit of work for undo/redo snapshots.</summary>
        public DxfDocument Clone()
        {
            var d = new DxfDocument();
            foreach (var pl in Polylines) d.Polylines.Add(new List<PointD>(pl));
            EnsureMeta();
            d.Meta = new List<ElementMeta>();
            foreach (var m in Meta) d.Meta.Add(m.Copy());
            if (ExplicitBorder != null) d.ExplicitBorder = new List<PointD>(ExplicitBorder);
            foreach (var m in Markers) d.Markers.Add(new Marker { Kind = m.Kind, Text = m.Text, Layer = m.Layer, X = m.X, Y = m.Y });
            return d;
        }

        /// <summary>
        /// Places the profile against the machine reference: X0 at the LEFT edge and
        /// Z0 at the BOTTOM edge. Re-applied after every rotate/mirror so the drawing
        /// always reads in machine coordinates rather than the CAD file's origin.
        /// </summary>
        public void NormalizeToOrigin()
        {
            if (Polylines.Count == 0) return;
            double minX, minY, maxX, maxY;
            GetBounds(out minX, out minY, out maxX, out maxY);
            for (int i = 0; i < Polylines.Count; i++)
                for (int j = 0; j < Polylines[i].Count; j++)
                {
                    var p = Polylines[i][j];
                    Polylines[i][j] = new PointD(p.X - minX, p.Y - minY);
                }
            foreach (var m in Markers) { m.X -= minX; m.Y -= minY; }
        }

        /// <summary>Removes several elements at once (region cleanup).</summary>
        public int RemoveElements(IEnumerable<int> indices)
        {
            var ordered = new List<int>(indices);
            ordered.Sort();
            ordered.Reverse();                       // remove from the back so indices stay valid
            int n = 0;
            foreach (var i in ordered) if (RemoveElement(i)) n++;
            return n;
        }

        /// <summary>Indices of elements lying ENTIRELY inside a rectangle (rubber-band select).</summary>
        public List<int> FindElementsInRect(double minX, double minY, double maxX, double maxY)
        {
            var hits = new List<int>();
            for (int i = 0; i < Polylines.Count; i++)
            {
                bool all = Polylines[i].Count > 0;
                foreach (var p in Polylines[i])
                    if (p.X < minX || p.X > maxX || p.Y < minY || p.Y > maxY) { all = false; break; }
                if (all) hits.Add(i);
            }
            return hits;
        }

        /// <summary>
        /// Elements that INTERSECT a rectangle (CAD "crossing" selection). Dragging
        /// right-to-left uses this, so partially covered elements are caught too -
        /// selecting only fully-enclosed items made cleaning a busy drawing hard.
        /// </summary>
        public List<int> FindElementsCrossingRect(double minX, double minY, double maxX, double maxY)
        {
            var hits = new List<int>();
            for (int i = 0; i < Polylines.Count; i++)
            {
                bool hit = false;
                var pl = Polylines[i];
                for (int j = 0; j < pl.Count && !hit; j++)
                {
                    var p = pl[j];
                    if (p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY) hit = true;
                    else if (j > 0)
                    {
                        // segment vs rectangle: sample a few points along it
                        var a = pl[j - 1];
                        for (int t = 1; t < 8 && !hit; t++)
                        {
                            double f = t / 8.0;
                            double x = a.X + (p.X - a.X) * f, y = a.Y + (p.Y - a.Y) * f;
                            if (x >= minX && x <= maxX && y >= minY && y <= maxY) hit = true;
                        }
                    }
                }
                if (hit) hits.Add(i);
            }
            return hits;
        }

        /// <summary>
        /// Every element connected to the given one, following shared endpoints.
        /// This is how an operator grabs a whole contour (or a whole offcut) in one
        /// click instead of picking dozens of segments by hand.
        /// </summary>
        public List<int> FindConnectedElements(int start, double tolerance = 0.1)
        {
            var found = new List<int>();
            if (start < 0 || start >= Polylines.Count) return found;
            var queue = new Queue<int>();
            var seen = new HashSet<int>();
            queue.Enqueue(start); seen.Add(start);

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                found.Add(cur);
                var a = Polylines[cur];
                if (a.Count == 0) continue;
                var aEnds = new[] { a[0], a[a.Count - 1] };

                for (int i = 0; i < Polylines.Count; i++)
                {
                    if (seen.Contains(i) || Polylines[i].Count == 0) continue;
                    var b = Polylines[i];
                    var bEnds = new[] { b[0], b[b.Count - 1] };
                    bool touches = false;
                    foreach (var pa in aEnds)
                        foreach (var pb in bEnds)
                            if (Distance(pa, pb) <= tolerance) touches = true;
                    if (touches) { seen.Add(i); queue.Enqueue(i); }
                }
            }
            return found;
        }

        /// <summary>
        /// The elements forming the shortest connected run BETWEEN two elements,
        /// inclusive - "select from here to there" along a contour.
        /// </summary>
        public List<int> FindPathBetween(int from, int to, double tolerance = 0.1)
        {
            var result = new List<int>();
            if (from < 0 || to < 0 || from >= Polylines.Count || to >= Polylines.Count) return result;
            if (from == to) { result.Add(from); return result; }

            var prev = new Dictionary<int, int>();
            var seen = new HashSet<int> { from };
            var queue = new Queue<int>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                if (cur == to) break;
                var a = Polylines[cur];
                if (a.Count == 0) continue;
                var aEnds = new[] { a[0], a[a.Count - 1] };
                for (int i = 0; i < Polylines.Count; i++)
                {
                    if (seen.Contains(i) || Polylines[i].Count == 0) continue;
                    var b = Polylines[i];
                    var bEnds = new[] { b[0], b[b.Count - 1] };
                    bool touches = false;
                    foreach (var pa in aEnds)
                        foreach (var pb in bEnds)
                            if (Distance(pa, pb) <= tolerance) touches = true;
                    if (!touches) continue;
                    seen.Add(i); prev[i] = cur; queue.Enqueue(i);
                }
            }
            if (!prev.ContainsKey(to) && from != to) return result;   // not connected

            int walk = to;
            result.Add(walk);
            while (walk != from && prev.ContainsKey(walk)) { walk = prev[walk]; result.Add(walk); }
            result.Reverse();
            return result;
        }

        /// <summary>Endpoints of every element - the snap targets when joining elements.</summary>
        public bool TrySnapToEndpoint(PointD p, double tolerance, out PointD snapped)
        {
            snapped = p;
            double best = tolerance;
            bool found = false;
            foreach (var pl in Polylines)
            {
                if (pl.Count == 0) continue;
                var candidates = new[] { pl[0], pl[pl.Count - 1] };
                foreach (var c in candidates)
                {
                    double d = Distance(p, c);
                    if (d <= best) { best = d; snapped = c; found = true; }
                }
            }
            return found;
        }

        /// <summary>
        /// Reads a G-code program back into a screen toolpath, so the drawing can
        /// show the ACTUAL stored program (green) rather than a re-derived guess.
        /// Understands the G0/G1 X/Y words this HMI generates.
        /// </summary>
        public static List<PointD> ParseToolpath(string gcode)
        {
            var path = new List<PointD>();
            if (string.IsNullOrWhiteSpace(gcode)) return path;
            double x = 0, y = 0; bool have = false;
            foreach (var raw in gcode.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("(", System.StringComparison.Ordinal)) continue;
                if (!(line.StartsWith("G0", System.StringComparison.Ordinal) || line.StartsWith("G1", System.StringComparison.Ordinal))) continue;
                bool moved = false;
                foreach (var tok in line.Split(' '))
                {
                    if (tok.Length < 2) continue;
                    double v;
                    if (!double.TryParse(tok.Substring(1), System.Globalization.NumberStyles.Float,
                                         System.Globalization.CultureInfo.InvariantCulture, out v)) continue;
                    if (tok[0] == 'X') { x = v; moved = true; }
                    else if (tok[0] == 'Y') { y = v; moved = true; }
                }
                if (moved) { path.Add(new PointD(x, y)); have = true; }
            }
            return have ? path : new List<PointD>();
        }

        /// <summary>Distance from a point to the nearest segment of a polyline.</summary>
        private static double DistToPolyline(List<PointD> pl, PointD p)
        {
            double best = double.MaxValue;
            for (int i = 1; i < pl.Count; i++)
            {
                double ax = pl[i - 1].X, ay = pl[i - 1].Y, bx = pl[i].X, by = pl[i].Y;
                double dx = bx - ax, dy = by - ay;
                double len2 = dx * dx + dy * dy;
                double t = len2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((p.X - ax) * dx + (p.Y - ay) * dy) / len2));
                double px = ax + t * dx, py = ay + t * dy;
                double d = Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Index of the element nearest to a point (within tolerance), else -1.</summary>
        public int FindNearestElement(PointD p, double tolerance)
        {
            int best = -1; double bestD = tolerance;
            for (int i = 0; i < Polylines.Count; i++)
            {
                if (Polylines[i].Count < 2) continue;
                double d = DistToPolyline(Polylines[i], p);
                if (d <= bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// SOFT delete: the element leaves the cut but stays in the drawing, shown
        /// faintly, so it is obvious what was removed and it can be brought back.
        /// </summary>
        public bool RemoveElement(int index)
        {
            EnsureMeta();
            if (index < 0 || index >= Polylines.Count) return false;
            Meta[index].Deleted = true;
            ExplicitBorder = null;          // the contour changed - rebuild it
            return true;
        }

        public bool RestoreElement(int index)
        {
            EnsureMeta();
            if (index < 0 || index >= Polylines.Count) return false;
            Meta[index].Deleted = false;
            ExplicitBorder = null;
            return true;
        }

        /// <summary>
        /// Splits an element at the point nearest p, giving TWO elements that can be
        /// treated separately - the usual way to cut only part of a contour.
        /// Returns the index of the second half, or -1 when the split is not possible.
        /// </summary>
        public int SplitElement(int index, PointD p)
        {
            EnsureMeta();
            if (index < 0 || index >= Polylines.Count) return -1;
            var pl = Polylines[index];
            if (pl.Count < 2) return -1;

            // find the segment nearest p and where along it the split falls
            int bestSeg = -1; double bestDist = double.MaxValue, bestT = 0;
            for (int i = 1; i < pl.Count; i++)
            {
                double ax = pl[i - 1].X, ay = pl[i - 1].Y;
                double dx = pl[i].X - ax, dy = pl[i].Y - ay;
                double len2 = dx * dx + dy * dy;
                double t = len2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((p.X - ax) * dx + (p.Y - ay) * dy) / len2));
                double px = ax + t * dx, py = ay + t * dy;
                double d = Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
                if (d < bestDist) { bestDist = d; bestSeg = i; bestT = t; }
            }
            if (bestSeg < 0) return -1;

            var cut = new PointD(pl[bestSeg - 1].X + (pl[bestSeg].X - pl[bestSeg - 1].X) * bestT,
                                 pl[bestSeg - 1].Y + (pl[bestSeg].Y - pl[bestSeg - 1].Y) * bestT);

            var first = pl.GetRange(0, bestSeg);
            first.Add(cut);
            var second = new List<PointD> { cut };
            second.AddRange(pl.GetRange(bestSeg, pl.Count - bestSeg));
            if (first.Count < 2 || second.Count < 2) return -1;

            Polylines[index] = first;
            Polylines.Insert(index + 1, second);
            Meta.Insert(index + 1, Meta[index].Copy());     // the halves inherit the settings
            ExplicitBorder = null;
            return index + 1;
        }

        /// <summary>Adds a straight line between two picked points (recipe editing).</summary>
        public void AddLine(PointD a, PointD b)
        {
            EnsureMeta();
            Polylines.Add(new List<PointD> { a, b });
            Meta.Add(new ElementMeta { Added = true });
            ExplicitBorder = null;          // let the new line take part in the contour
        }

        /// <summary>A generated program plus the geometry each motion line came from.</summary>
        public class GCodeProgram
        {
            public string Code = "";
            /// <summary>For every line of Code: the route point index it moves to (-1 = not a move).</summary>
            public List<int> LineToRoutePoint = new List<int>();
            public List<PointD> Route = new List<PointD>();
        }

        /// <summary>
        /// G-code around the border route, emitting real CIRCULAR moves.
        /// DXF arcs are flattened to points when the file is read, so the route is
        /// re-fitted here: runs of points lying on a common circle become G02/G03
        /// (with I/J offsets), everything else stays G01. Without this every radius
        /// on the profile came out as a long chain of tiny G01 moves.
        /// The tool radius is applied through the controller's cutter compensation
        /// (G41 D1 ... G40), so one program serves every tool diameter.
        /// </summary>
        public GCodeProgram GenerateBorderProgram(double toolRadius, double cuttingDepth,
                                                  double feed = 800, double safeZ = 5)
        {
            return GenerateBorderProgram(toolRadius, cuttingDepth, feed, safeZ, true);
        }

        /// <summary>
        /// Builds the border program.
        ///
        /// The route is walked ELEMENT BY ELEMENT: the tool enters at an element's
        /// start, cuts through to its END at that element's depth, and only then
        /// travels to the next element's start. Previously the path ran start-to-
        /// start, so the body of each element was never cut.
        ///
        /// Per element the operator can set:
        ///   Included            leave it out of the cut entirely
        ///   Depth / DepthEnd    how deep to cut, optionally tapering along it
        ///   StartOffset/EndOffset  begin earlier or later, overrun or stop short
        ///
        /// The depth is RADIAL - how far the cut reaches into the profile - and the
        /// contour is offset inward by it, with G41 applying the tool radius, so the
        /// cutter's outer edge lands exactly that far inside the border.
        /// All values are millimetres, as everywhere else in this page.
        /// </summary>
        public GCodeProgram GenerateBorderProgram(double toolRadius, double cutDepth,
                                                  double feed, double safeZ, bool perElement)
        {
            var prog = new GCodeProgram();
            EnsureMeta();

            var borderRoute = GetBorderRoute();
            var owners = RouteElementOfPoint;
            if (borderRoute.Count < 2)
            {
                prog.Code = "(Kaban HMI - border route program)\r\n(NO CLOSED BORDER FOUND)\r\n";
                return prog;
            }

            // per-point bite, honouring each element's own depth (and taper)
            var bites = new List<double>();
            for (int i = 0; i < borderRoute.Count; i++)
                bites.Add(DepthForPoint(owners, i, cutDepth, borderRoute, i));
            var route = OffsetPolylineVariable(borderRoute, bites);
            prog.Route = route;

            var sb = new System.Text.StringBuilder();
            Action<string, int> emit = (line, routeIdx) =>
            {
                sb.AppendLine(line);
                prog.LineToRoutePoint.Add(routeIdx);
            };
            Func<double, string> F = v => v.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

            emit("(Kaban HMI - border route program)", -1);
            emit(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                 "(tool radius {0:F3} mm  cut depth {1:F3} mm)", toolRadius, cutDepth), -1);
            emit("G21 G90", -1);
            emit("G0 Z" + F(safeZ), -1);

            // --- walk the route in ELEMENT runs ---
            int p = 0;
            bool cutting = false;
            int prevElement = -1;
            while (p < route.Count)
            {
                int el = (owners != null && p < owners.Count) ? owners[p] : -1;
                int runEnd = p;
                while (runEnd + 1 < route.Count &&
                       (owners == null || runEnd + 1 >= owners.Count || owners[runEnd + 1] == el))
                    runEnd++;

                bool include = el < 0 || el >= Meta.Count || Meta[el].Included;
                if (!include)
                {
                    // skipped element: lift, and resume at the next included one
                    if (cutting) { emit("G40", -1); emit("G0 Z" + F(safeZ), -1); cutting = false; }
                    p = runEnd + 1;
                    continue;
                }

                if (!cutting)
                {
                    // entering the material: rapid to the start, plunge, compensate
                    emit("G0 X" + F(route[p].X) + " Y" + F(route[p].Y), p);
                    emit("G1 Z" + F(-Math.Abs(CutZ(el, cutDepth))) + " F" + F(feed / 2), p);
                    emit("G41 D1 (radius " + F(toolRadius) + ")", p);
                    cutting = true;
                }
                else
                {
                    // already cutting: reach this element's START, then run through it.
                    // Chaining shares endpoints, so an element often contributes a
                    // single new point - without this move nothing was emitted for it
                    // and the tool appeared to jump from start to start.
                    emit("G1 X" + F(route[p].X) + " Y" + F(route[p].Y) + " F" + F(feed), p);
                    double want = CutZ(el, cutDepth);
                    if (Math.Abs(want - CutZ(prevElement, cutDepth)) > 1e-6)
                        emit("G1 Z" + F(-Math.Abs(want)) + " F" + F(feed / 2), p);
                }
                prevElement = el;

                // cut THROUGH this element, start to end
                int i2 = p + 1;
                while (i2 <= runEnd)
                {
                    int arcEnd; PointD centre; bool cw;
                    double maxArcRadius = MaxArcRadius();
                    if (TryFitArc(route, i2 - 1, maxArcRadius, out arcEnd, out centre, out cw) && arcEnd <= runEnd)
                    {
                        var st = route[i2 - 1];
                        emit(string.Format("{0} X{1} Y{2} I{3} J{4} F{5}",
                                cw ? "G02" : "G03", F(route[arcEnd].X), F(route[arcEnd].Y),
                                F(centre.X - st.X), F(centre.Y - st.Y), F(feed)), arcEnd);
                        i2 = arcEnd + 1;
                    }
                    else
                    {
                        emit("G1 X" + F(route[i2].X) + " Y" + F(route[i2].Y) + " F" + F(feed), i2);
                        i2++;
                    }
                }
                p = runEnd + 1;
            }

            if (cutting) emit("G40", -1);
            emit("G0 Z" + F(safeZ), -1);
            emit("M30", -1);
            prog.Code = sb.ToString();
            return prog;
        }

        private double MaxArcRadius()
        {
            double a, b, c, d;
            GetBounds(out a, out b, out c, out d);
            return 1.5 * Math.Max(c - a, d - b);
        }

        /// <summary>Z depth used while cutting a given element.</summary>
        private double CutZ(int element, double fallback)
        {
            EnsureMeta();
            if (element < 0 || element >= Meta.Count) return fallback;
            return Meta[element].Depth ?? fallback;
        }

        /// <summary>Depth of the element that owns a route point (falls back to the global one).</summary>
        private double DepthForPoint(List<int> owners, int pointIndex, double fallback,
                                     List<PointD> route = null, int idx = -1)
        {
            if (owners == null || pointIndex < 0 || pointIndex >= owners.Count) return fallback;
            int el = owners[pointIndex];
            EnsureMeta();
            if (el < 0 || el >= Meta.Count) return fallback;

            double start = Meta[el].Depth ?? fallback;
            double? endDepth = Meta[el].DepthEnd;
            if (endDepth == null || route == null || idx < 0) return start;

            // TAPER: interpolate between the start and end depth across this element
            int first = pointIndex, last = pointIndex;
            while (first > 0 && owners[first - 1] == el) first--;
            while (last + 1 < owners.Count && owners[last + 1] == el) last++;
            if (last <= first) return start;
            double t = (double)(pointIndex - first) / (last - first);
            return start + (endDepth.Value - start) * t;
        }

        /// <summary>
        /// Offsets a contour sideways by one tool radius, towards the inside, so the
        /// drawn path shows where the CUTTER EDGE runs rather than its centre line.
        /// </summary>
        public static List<PointD> OffsetPolyline(List<PointD> pts, double distance)
        {
            var uniform = new List<double>();
            for (int i = 0; i < (pts == null ? 0 : pts.Count); i++) uniform.Add(distance);
            return OffsetPolylineVariable(pts, uniform);
        }

        /// <summary>
        /// Offsets a contour inward by a DIFFERENT amount at each point, which is how
        /// one element can be cut deeper into the profile than its neighbours.
        /// </summary>
        public static List<PointD> OffsetPolylineVariable(List<PointD> pts, List<double> distances)
        {
            var result = new List<PointD>();
            if (pts == null || pts.Count < 2 || distances == null || distances.Count < pts.Count) return pts ?? result;
            bool allZero = true;
            foreach (var dd in distances) if (Math.Abs(dd) > 1e-9) { allZero = false; break; }
            if (allZero) return pts;

            // A closed contour usually repeats its first point at the end; work on the
            // RING without that duplicate so every point has real neighbours.
            bool closed = Distance(pts[0], pts[pts.Count - 1]) < 1e-6;
            var ring = closed ? pts.GetRange(0, pts.Count - 1) : pts;
            int n = ring.Count;
            if (n < 3) return pts;

            double area = 0;
            for (int i = 0; i < n; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % n];
                area += a.X * b.Y - b.X * a.Y;
            }
            double sign = area >= 0 ? 1 : -1;

            for (int i = 0; i < n; i++)
            {
                var prev = ring[(i - 1 + n) % n];
                var cur = ring[i];
                var next = ring[(i + 1) % n];
                double dist = distances[i];

                // Unit directions of the two segments meeting at this point.
                double ax = cur.X - prev.X, ay = cur.Y - prev.Y;
                double bx = next.X - cur.X, by = next.Y - cur.Y;
                double la = Math.Sqrt(ax * ax + ay * ay), lb = Math.Sqrt(bx * bx + by * by);
                if (la < 1e-9 && lb < 1e-9) { result.Add(cur); continue; }
                if (la < 1e-9) { ax = bx; ay = by; la = lb; }
                if (lb < 1e-9) { bx = ax; by = ay; lb = la; }
                ax /= la; ay /= la; bx /= lb; by /= lb;

                // Inward normals of each segment.
                double n1x = -ay * sign, n1y = ax * sign;
                double n2x = -by * sign, n2y = bx * sign;

                // MITER join: intersect the two offset segments instead of averaging
                // the normals - averaging pulls corners short by cos(half-angle),
                // so a 90 degree corner only reached 0.71 of the requested depth.
                double p1x = prev.X + n1x * dist, p1y = prev.Y + n1y * dist;
                double p2x = cur.X + n2x * dist, p2y = cur.Y + n2y * dist;
                double cross = ax * by - ay * bx;
                if (Math.Abs(cross) < 1e-9)
                {
                    result.Add(new PointD(cur.X + n1x * dist, cur.Y + n1y * dist));   // straight through
                    continue;
                }
                double t = ((p2x - p1x) * by - (p2y - p1y) * bx) / cross;
                double mx = p1x + ax * t, my = p1y + ay * t;

                // guard against a runaway spike on a very sharp corner
                double miter = Math.Sqrt((mx - cur.X) * (mx - cur.X) + (my - cur.Y) * (my - cur.Y));
                double limit = Math.Abs(dist) * 4 + 1e-6;
                if (miter > limit)
                {
                    double sx = (n1x + n2x), sy = (n1y + n2y);
                    double sl = Math.Sqrt(sx * sx + sy * sy);
                    if (sl > 1e-9) { mx = cur.X + sx / sl * dist; my = cur.Y + sy / sl * dist; }
                }
                result.Add(new PointD(mx, my));
            }
            if (closed) result.Add(result[0]);        // restore the closing point
            return result;
        }

        /// <summary>Back-compatible wrapper returning just the program text.</summary>
        public string GenerateBorderGCode(double toolRadius, double cuttingDepth,
                                          double feed = 800, double safeZ = 5)
        {
            return GenerateBorderProgram(toolRadius, cuttingDepth, feed, safeZ).Code;
        }

        /// <summary>
        /// Greedily fits a circular arc starting at route[from]. Succeeds only when
        /// at least three following points share a circle within tolerance and keep
        /// turning the same way - that is what distinguishes a real radius from a
        /// polyline corner.
        /// </summary>
        private static bool TryFitArc(List<PointD> route, int from, double maxRadius,
                                      out int end, out PointD centre, out bool clockwise)
        {
            end = from; centre = new PointD(0, 0); clockwise = false;
            if (from + 3 >= route.Count) return false;

            PointD c;
            if (!Circumcentre(route[from], route[from + 1], route[from + 2], out c)) return false;
            double r = Distance(c, route[from]);
            if (r < 0.05 || r > maxRadius) return false;

            // Tight, mostly ABSOLUTE tolerance: a proportional one (r * 0.002) let a
            // near-straight run be "fitted" as a 330 mm arc on an 80 mm profile.
            double tol = Math.Max(0.01, Math.Min(0.05, r * 0.001));
            bool cw = Cross(route[from], route[from + 1], route[from + 2]) < 0;

            int last = from + 2;
            for (int k = from + 3; k < route.Count; k++)
            {
                if (Math.Abs(Distance(c, route[k]) - r) > tol) break;
                if ((Cross(route[k - 2], route[k - 1], route[k]) < 0) != cw) break;
                last = k;
            }
            if (last - from < 3) return false;      // too short to be a genuine arc

            // A real radius must also SWEEP: without this, a slightly bowed straight
            // section fits a huge circle and would be cut as a wrong arc.
            double a0 = Math.Atan2(route[from].Y - c.Y, route[from].X - c.X);
            double a1 = Math.Atan2(route[last].Y - c.Y, route[last].X - c.X);
            double sweep = Math.Abs(a1 - a0);
            if (sweep > Math.PI) sweep = 2 * Math.PI - sweep;
            if (sweep < 8.0 * Math.PI / 180.0) return false;

            end = last; centre = c; clockwise = cw;
            return true;
        }

        private static double Cross(PointD a, PointD b, PointD c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }

        private static bool Circumcentre(PointD a, PointD b, PointD c, out PointD centre)
        {
            centre = new PointD(0, 0);
            double d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
            if (Math.Abs(d) < 1e-9) return false;                 // collinear
            double ax2 = a.X * a.X + a.Y * a.Y, bx2 = b.X * b.X + b.Y * b.Y, cx2 = c.X * c.X + c.Y * c.Y;
            centre = new PointD((ax2 * (b.Y - c.Y) + bx2 * (c.Y - a.Y) + cx2 * (a.Y - b.Y)) / d,
                                (ax2 * (c.X - b.X) + bx2 * (a.X - c.X) + cx2 * (b.X - a.X)) / d);
            return true;
        }


        /// <summary>
        /// Explicit border set by the operator (null = derive it by chaining).
        /// Heuristics cannot know which contour is meant once a drawing has been
        /// edited, so an explicit choice always wins.
        /// </summary>
        public List<PointD> ExplicitBorder { get; set; }

        /// <summary>The elements an explicit border was built from (so it can be rebuilt).</summary>
        private List<int> _explicitBorderElements = new List<int>();

        /// <summary>Chains the given elements into one route and fixes it as the border.</summary>
        public int SetBorderFromElements(IEnumerable<int> indices)
        {
            var picked = new List<List<PointD>>();
            var owners = new List<int>();
            foreach (var i in indices)
                if (i >= 0 && i < Polylines.Count && Polylines[i].Count >= 2)
                {
                    picked.Add(new List<PointD>(Polylines[i]));
                    owners.Add(i);
                }
            _chainOwners = owners;
            _explicitBorderElements = new List<int>(owners);   // so exclusions can rebuild it
            if (picked.Count == 0) { ExplicitBorder = null; return 0; }
            ExplicitBorder = ChainLongest(picked);
            return picked.Count;
        }

        public List<PointD> GetBorderRoute()
        {
            // An EXPLICIT border (set from a selection) is remembered as a point list,
            // so it knew nothing about later exclusions - marking an element "do not
            // cut" appeared to do nothing at all. Rebuild it from its own elements
            // whenever any of them has been excluded or deleted since.
            if (_explicitBorderElements != null && _explicitBorderElements.Count > 0)
            {
                EnsureMeta();
                bool anyInactive = _explicitBorderElements.Exists(i => !IsActive(i));
                if (anyInactive)
                {
                    var still = _explicitBorderElements.FindAll(IsActive);
                    if (still.Count == 0) { ExplicitBorder = null; }
                    else
                    {
                        var keep = new List<int>(_explicitBorderElements);
                        SetBorderFromElements(still);
                        _explicitBorderElements = keep;      // remember the original pick
                        if (ExplicitBorder != null && ExplicitBorder.Count > 1) return ExplicitBorder;
                    }
                }
            }
            if (ExplicitBorder != null && ExplicitBorder.Count > 1) return ExplicitBorder;
            // Profile exports often draw the outer contour as MANY separate
            // segments (lines + arcs), not one closed polyline - so the border is
            // CHAINED: polylines whose endpoints coincide (within a tolerance) are
            // joined end to end, and the longest resulting loop/chain is the route.
            // ACTIVE elements only - and manually added lines count exactly like the
            // ones from the file, which is what lets an edited contour drive the program.
            EnsureMeta();
            var all = new List<List<PointD>>();
            var owner = new List<int>();
            for (int i = 0; i < Polylines.Count; i++)
            {
                if (Polylines[i].Count < 2 || !IsActive(i)) continue;
                all.Add(new List<PointD>(Polylines[i]));
                owner.Add(i);
            }
            _chainOwners = owner;
            return ChainLongest(all);
        }

        private List<int> _chainOwners = new List<int>();

        /// <summary>Maps a pool slot back to the real element index.</summary>
        private int OwnerOf(int poolIndex)
        {
            return poolIndex >= 0 && poolIndex < _chainOwners.Count ? _chainOwners[poolIndex] : -1;
        }

        /// <summary>Element index that produced each point of the last built route.</summary>
        public List<int> RouteElementOfPoint { get; private set; } = new List<int>();

        /// <summary>Joins segments whose endpoints coincide and returns the best contour.</summary>
        private List<PointD> ChainLongest(List<List<PointD>> pool)
        {
            // A hand-drawn line snaps to an endpoint, but a DXF exported from CAD can
            // still leave sub-tenth gaps. 0.05 was tight enough that added lines
            // silently failed to join the contour.
            const double eps = 0.2;

            List<PointD> best = null; double bestScore = 0;
            List<int> bestOwners = null;
            var used = new bool[pool.Count];

            for (int seed = 0; seed < pool.Count; seed++)
            {
                if (used[seed]) continue;
                var chain = new List<PointD>(pool[seed]);
                var chainOwn = new List<int>();
                for (int q = 0; q < pool[seed].Count; q++) chainOwn.Add(OwnerOf(seed));
                used[seed] = true;

                bool extended = true;
                while (extended)
                {
                    extended = false;
                    for (int i = 0; i < pool.Count; i++)
                    {
                        if (used[i]) continue;
                        var seg = pool[i];
                        var head = chain[0]; var tail = chain[chain.Count - 1];

                        if (Distance(tail, seg[0]) < eps)
                        {
                            chain.AddRange(seg.GetRange(1, seg.Count - 1));
                            for (int q = 1; q < seg.Count; q++) chainOwn.Add(OwnerOf(i));
                            used[i] = true; extended = true;
                        }
                        else if (Distance(tail, seg[seg.Count - 1]) < eps)
                        {
                            var r = new List<PointD>(seg); r.Reverse();
                            chain.AddRange(r.GetRange(1, r.Count - 1));
                            for (int q = 1; q < r.Count; q++) chainOwn.Add(OwnerOf(i));
                            used[i] = true; extended = true;
                        }
                        else if (Distance(head, seg[seg.Count - 1]) < eps)
                        {
                            var pre = new List<PointD>(seg); pre.RemoveAt(pre.Count - 1);
                            chain.InsertRange(0, pre);
                            var own = new List<int>();
                            for (int q = 0; q < pre.Count; q++) own.Add(OwnerOf(i));
                            chainOwn.InsertRange(0, own);
                            used[i] = true; extended = true;
                        }
                        else if (Distance(head, seg[0]) < eps)
                        {
                            var r = new List<PointD>(seg); r.Reverse(); r.RemoveAt(r.Count - 1);
                            chain.InsertRange(0, r);
                            var own = new List<int>();
                            for (int q = 0; q < r.Count; q++) own.Add(OwnerOf(i));
                            chainOwn.InsertRange(0, own);
                            used[i] = true; extended = true;
                        }
                    }
                }

                double len = 0;
                for (int i = 1; i < chain.Count; i++) len += Distance(chain[i - 1], chain[i]);

                // A border is a CLOSED contour, so closed chains always beat open
                // ones. Without this the original outline stayed "longest" and edits
                // (deleting a segment, adding a bridging line) could never reroute it.
                bool closed = chain.Count > 2 && Distance(chain[0], chain[chain.Count - 1]) < eps * 4;
                double score = len + (closed ? 1e6 : 0);
                if (score > bestScore) { bestScore = score; best = chain; bestOwners = chainOwn; }
            }

            if (best == null) { RouteElementOfPoint = new List<int>(); return new List<PointD>(); }
            RouteElementOfPoint = bestOwners ?? new List<int>();
            // close the loop visually when the ends meet
            if (Distance(best[0], best[best.Count - 1]) < eps * 4 &&
                Distance(best[0], best[best.Count - 1]) > 1e-12)
            {
                best.Add(best[0]);
                RouteElementOfPoint.Add(RouteElementOfPoint.Count > 0 ? RouteElementOfPoint[0] : -1);
            }
            return best;
        }
    }
}

