using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Kaban.Profiles.Geometry
{
    /// <summary>
    /// A profile's CROSS-SECTION in the library's own coordinates:
    ///
    ///     Y  across the profile, 0 at the OUTSIDE face, growing toward the inside
    ///     Z  up, 0 where the profile rests on the bed
    ///
    /// made from a DXF turned the way the profile's record says (quarter turns
    /// clockwise, then a mirror) and moved so its lowest-left corner is 0,0.
    ///
    /// The paths on REF_ layers and on layers the profile ignores (dimensions, a
    /// gasket drawn beside it) are kept for drawing and for finding references,
    /// but they are not MATERIAL: the width, the height and every ray through the
    /// profile count only the others.
    /// </summary>
    public sealed class Section
    {
        public sealed class Path
        {
            public List<Pt> Points = new List<Pt>();
            public string Layer = "0";
            public bool Closed;
            public bool IsMaterial = true;
        }

        public sealed class Marker
        {
            public string Kind = "", Text = "", Layer = "0";
            public double Y, Z, Radius;
        }

        public struct Pt
        {
            public double Y, Z;
            public Pt(double y, double z) { Y = y; Z = z; }
            public override string ToString() { return Y.ToString("0.###", CultureInfo.InvariantCulture) + " / " + Z.ToString("0.###", CultureInfo.InvariantCulture); }
        }

        public readonly List<Path> Paths = new List<Path>();
        public readonly List<Marker> Markers = new List<Marker>();
        public readonly List<string> Warnings = new List<string>();

        public double Width { get; private set; }
        public double Height { get; private set; }

        /// <summary>The layers of the drawing, with how many paths each has.</summary>
        public Dictionary<string, int> Layers
        {
            get
            {
                var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in Paths) { int n; d.TryGetValue(p.Layer, out n); d[p.Layer] = n + 1; }
                return d;
            }
        }

        public static bool IsReferenceLayer(string layer)
        {
            return (layer ?? "").StartsWith("REF_", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// A section from a DXF drawing: turned <paramref name="quarterTurnsCw"/> times
        /// clockwise, then mirrored left to right if <paramref name="mirror"/>, then
        /// placed at 0,0. <paramref name="ignoredLayers"/>: comma-separated layers
        /// that are not material.
        /// </summary>
        public static Section FromDrawing(DxfReader.Drawing d, int quarterTurnsCw, bool mirror, string ignoredLayers)
        {
            var ignored = new HashSet<string>(SplitLayers(ignoredLayers), StringComparer.OrdinalIgnoreCase);
            var s = new Section();
            foreach (var p in d.Paths)
            {
                if (p.Points.Count < 2) continue;
                var path = new Path { Layer = p.Layer, Closed = p.Closed };
                foreach (var q in p.Points) path.Points.Add(new Pt(q.X, q.Y));
                path.IsMaterial = !IsReferenceLayer(p.Layer) && !ignored.Contains(p.Layer) && !LooksLikeAnnotation(p.Layer);
                s.Paths.Add(path);
            }
            foreach (var m in d.Markers)
                s.Markers.Add(new Marker { Kind = m.Kind, Text = m.Text, Layer = m.Layer, Y = m.X, Z = m.Y, Radius = m.Radius });
            s.Warnings.AddRange(d.Warnings);
            s.Orient(quarterTurnsCw, mirror);
            return s;
        }

        public static Section Load(string file, int quarterTurnsCw, bool mirror, string ignoredLayers)
        {
            return FromDrawing(DxfReader.Load(file), quarterTurnsCw, mirror, ignoredLayers);
        }

        public static IEnumerable<string> SplitLayers(string list)
        {
            return (list ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0);
        }

        /// <summary>Dimension, text and hatch layers by their usual names ("DIM", "BEMASSUNG", "TEXT", "HATCH", "Defpoints").</summary>
        public static bool LooksLikeAnnotation(string layer)
        {
            var l = (layer ?? "").ToUpperInvariant();
            return l == "DEFPOINTS" || l.StartsWith("DIM") || l.Contains("DIMENSION") || l.Contains("OLCU") || l.Contains("ÖLÇÜ") ||
                   l.Contains("BEMASS") || l.Contains("HATCH") || l.Contains("TARAMA") || l.Contains("TEXT") || l.Contains("YAZI");
        }

        private void Orient(int quarterTurnsCw, bool mirror)
        {
            int k = ((quarterTurnsCw % 4) + 4) % 4;
            Func<Pt, Pt> f = p =>
            {
                double y = p.Y, z = p.Z;
                for (int i = 0; i < k; i++) { double t = y; y = z; z = -t; }   // clockwise
                if (mirror) y = -y;
                return new Pt(y, z);
            };
            foreach (var p in Paths) for (int i = 0; i < p.Points.Count; i++) p.Points[i] = f(p.Points[i]);
            foreach (var m in Markers) { var q = f(new Pt(m.Y, m.Z)); m.Y = q.Y; m.Z = q.Z; }

            double minY, minZ, maxY, maxZ;
            RawBounds(out minY, out minZ, out maxY, out maxZ);
            foreach (var p in Paths) for (int i = 0; i < p.Points.Count; i++) p.Points[i] = new Pt(p.Points[i].Y - minY, p.Points[i].Z - minZ);
            foreach (var m in Markers) { m.Y -= minY; m.Z -= minZ; }
            Width = maxY - minY;
            Height = maxZ - minZ;
        }

        /// <summary>The material's extent (every path's, when nothing is material).</summary>
        private void RawBounds(out double minY, out double minZ, out double maxY, out double maxZ)
        {
            minY = minZ = double.MaxValue; maxY = maxZ = double.MinValue;
            bool any = Paths.Any(p => p.IsMaterial);
            foreach (var p in Paths)
            {
                if (any && !p.IsMaterial) continue;
                foreach (var q in p.Points)
                {
                    if (q.Y < minY) minY = q.Y; if (q.Y > maxY) maxY = q.Y;
                    if (q.Z < minZ) minZ = q.Z; if (q.Z > maxZ) maxZ = q.Z;
                }
            }
            if (minY == double.MaxValue) { minY = minZ = maxY = maxZ = 0; }
        }

        // ---- rays through the profile ----------------------------------------------

        /// <summary>Which way a tool comes in, from which face of the bounding box.</summary>
        public enum Side { Top, Bottom, Outside, Inside }

        public static Side ParseSide(string s)
        {
            switch ((s ?? "").Trim().ToUpperInvariant())
            {
                case "BOTTOM": return Side.Bottom;
                case "OUTSIDE": return Side.Outside;
                case "INSIDE": return Side.Inside;
                default: return Side.Top;
            }
        }

        /// <summary>One stretch of a ray: material or air, measured from the face it enters by.</summary>
        public sealed class Interval
        {
            public double Start, End;
            public bool Material;
            /// <summary>The layer of the wall (the path the ray entered it through).</summary>
            public string Layer = "";
            public double Thickness { get { return End - Start; } }
            public override string ToString()
            {
                return (Material ? "wall " : "air ") + Start.ToString("0.##", CultureInfo.InvariantCulture) + "-" +
                       End.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// What a tool meets going straight in from <paramref name="side"/> at
        /// <paramref name="across"/> (Y for the top and bottom, Z for the outside
        /// and inside faces): material and air, by distance from that face of the
        /// bounding box, to the far face. Walls closer than 0.01 mm are one wall.
        ///
        /// The drawing's closed outlines decide inside and outside (every crossing
        /// of an outline toggles), so a chamber drawn as its own outline is air and
        /// a steel reinforcement drawn inside it is material again.
        /// </summary>
        public List<Interval> Ray(Side side, double across)
        {
            bool vertical = side == Side.Top || side == Side.Bottom;
            double length = vertical ? Height : Width;
            var hits = new List<KeyValuePair<double, string>>();
            foreach (var p in Paths)
            {
                if (!p.IsMaterial) continue;
                for (int i = 1; i < p.Points.Count; i++)
                {
                    var a = p.Points[i - 1]; var b = p.Points[i];
                    double ca = vertical ? a.Y : a.Z, cb = vertical ? b.Y : b.Z;
                    // half-open: a vertex shared by two segments is counted once
                    if (!((ca <= across && across < cb) || (cb <= across && across < ca))) continue;
                    double t = (across - ca) / (cb - ca);
                    double along = vertical ? a.Z + t * (b.Z - a.Z) : a.Y + t * (b.Y - a.Y);
                    double dist;
                    switch (side)
                    {
                        case Side.Top: dist = Height - along; break;
                        case Side.Bottom: dist = along; break;
                        case Side.Outside: dist = along; break;
                        default: dist = Width - along; break;
                    }
                    hits.Add(new KeyValuePair<double, string>(dist, p.Layer));
                }
            }
            hits.Sort((x, y) => x.Key.CompareTo(y.Key));
            // The same line drawn twice (common in exported drawings) would toggle
            // twice and hide a wall: crossings at the same place count once.
            var clean = new List<KeyValuePair<double, string>>();
            foreach (var h in hits)
                if (clean.Count == 0 || h.Key - clean[clean.Count - 1].Key > 1e-6) clean.Add(h);
                else clean[clean.Count - 1] = new KeyValuePair<double, string>(h.Key, clean[clean.Count - 1].Value);

            var list = new List<Interval>();
            double pos = 0; bool inside = false; string layer = "";
            foreach (var h in clean)
            {
                double at = Math.Max(0, Math.Min(length, h.Key));
                if (at > pos + 1e-9) list.Add(new Interval { Start = pos, End = at, Material = inside, Layer = inside ? layer : "" });
                pos = Math.Max(pos, at);
                inside = !inside;
                layer = h.Value;
            }
            if (length > pos + 1e-9) list.Add(new Interval { Start = pos, End = length, Material = false });
            // merge neighbours of the same kind (walls closer than 0.01 mm are one)
            var merged = new List<Interval>();
            foreach (var iv in list)
            {
                // a sliver of air before the first wall (a surface drawn 0.001 inside the box)
                if (merged.Count == 0 && !iv.Material && iv.Thickness < 0.01) continue;
                if (merged.Count == 0 && iv.Material && iv.Start < 0.01) iv.Start = 0;
                if (merged.Count > 0)
                {
                    var last = merged[merged.Count - 1];
                    if (last.Material == iv.Material && Math.Abs(last.End - iv.Start) < 1e-6) { last.End = iv.End; continue; }
                    if (!iv.Material && iv.Thickness < 0.01 && merged.Count > 0 && last.Material) { last.End = iv.End; continue; }
                }
                merged.Add(new Interval { Start = iv.Start, End = iv.End, Material = iv.Material, Layer = iv.Layer });
            }
            return merged;
        }

        /// <summary>Where the first material is, from that face (null: the ray meets none).</summary>
        public double? FirstMaterial(Side side, double across)
        {
            foreach (var iv in Ray(side, across)) if (iv.Material) return iv.Start;
            return null;
        }

        /// <summary>
        /// The height of the profile's TOP seen from above, sampled every
        /// <paramref name="step"/> mm across its width (NaN where nothing is below).
        /// </summary>
        public double[] TopProfile(double step)
        {
            int n = Math.Max(2, (int)Math.Ceiling(Width / step) + 1);
            var z = new double[n];
            for (int i = 0; i < n; i++)
            {
                double y = Math.Min(Width - 1e-6, Math.Max(1e-6, i * step));
                var first = FirstMaterial(Side.Top, y);
                z[i] = first.HasValue ? Height - first.Value : double.NaN;
            }
            return z;
        }

        /// <summary>Distance, in the section, of a point to the nearest material edge.</summary>
        public double DistanceToEdge(Pt p)
        {
            double best = double.MaxValue;
            foreach (var path in Paths)
            {
                if (!path.IsMaterial) continue;
                for (int i = 1; i < path.Points.Count; i++)
                    best = Math.Min(best, SegmentDistance(p, path.Points[i - 1], path.Points[i]));
            }
            return best;
        }

        public static double SegmentDistance(Pt p, Pt a, Pt b)
        {
            double dy = b.Y - a.Y, dz = b.Z - a.Z;
            double l2 = dy * dy + dz * dz;
            double t = l2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((p.Y - a.Y) * dy + (p.Z - a.Z) * dz) / l2));
            double y = a.Y + t * dy - p.Y, z = a.Z + t * dz - p.Z;
            return Math.Sqrt(y * y + z * z);
        }

        /// <summary>Total length of the material outlines.</summary>
        public double Perimeter
        {
            get
            {
                double sum = 0;
                foreach (var p in Paths)
                    if (p.IsMaterial)
                        for (int i = 1; i < p.Points.Count; i++)
                        {
                            double dy = p.Points[i].Y - p.Points[i - 1].Y, dz = p.Points[i].Z - p.Points[i - 1].Z;
                            sum += Math.Sqrt(dy * dy + dz * dz);
                        }
                return sum;
            }
        }

        /// <summary>
        /// The material's area, by rays: each horizontal line through the section
        /// (every 0.25 mm) adds its walls' length.
        /// </summary>
        public double MaterialArea(double step = 0.25)
        {
            double area = 0;
            for (double z = step / 2; z < Height; z += step)
                foreach (var iv in Ray(Side.Outside, z))
                    if (iv.Material) area += iv.Thickness * step;
            return area;
        }
    }
}
