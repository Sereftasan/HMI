using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Kaban.Profiles.Geometry
{
    /// <summary>
    /// Reads a profile drawing (DXF, ASCII) into paths with their LAYER, and the
    /// points and texts on it - what the profile library needs from a vendor's
    /// section drawing:
    ///
    ///   LINE, ARC, CIRCLE, ELLIPSE, LWPOLYLINE and POLYLINE (with bulges), SPLINE
    ///   (through its fit or control points) - every one as a polyline, arcs in
    ///   steps of 10 degrees or less;
    ///   INSERT - the block's entities placed, scaled and turned (entities on layer
    ///   0 take the insert's layer, as in CAD); a block nobody inserts is not drawn;
    ///   POINT, TEXT, MTEXT and circle centres - as MARKERS: where a reference
    ///   such as "HARDWARE_AXIS" can be written on the drawing;
    ///   an entity drawn with its extrusion pointing down (many exports do that to
    ///   arcs) - mirrored back into place.
    ///
    /// DIMENSION, HATCH and LEADER are left out: they are not the profile.
    /// </summary>
    public static class DxfReader
    {
        public sealed class Drawing
        {
            public readonly List<Path> Paths = new List<Path>();
            public readonly List<Marker> Markers = new List<Marker>();
            public readonly List<string> Warnings = new List<string>();
        }

        public sealed class Path
        {
            public readonly List<Pt> Points = new List<Pt>();
            public string Layer = "0";
            public bool Closed;
            /// <summary>LINE, ARC, CIRCLE, LWPOLYLINE, POLYLINE, SPLINE, ELLIPSE.</summary>
            public string Kind = "";
        }

        /// <summary>A point, a text's insertion point or a circle's centre.</summary>
        public sealed class Marker
        {
            public string Kind = "";      // POINT, TEXT, CIRCLE
            public double X, Y;
            public string Text = "";
            public string Layer = "0";
            public double Radius;
        }

        public struct Pt
        {
            public double X, Y;
            public Pt(double x, double y) { X = x; Y = y; }
        }

        public static Drawing Load(string file)
        {
            using (var r = new StreamReader(file, Encoding.Default, true)) return Read(r);
        }

        private struct Pair { public int Code; public string Value; }

        private sealed class Entity
        {
            public string Type;
            public readonly List<Pair> Fields = new List<Pair>();
            public readonly List<Entity> Vertices = new List<Entity>();   // POLYLINE
            public string Get(int code, string def = null)
            {
                foreach (var f in Fields) if (f.Code == code) return f.Value;
                return def;
            }
            public double D(int code, double def = 0)
            {
                var s = Get(code);
                double v;
                return s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def;
            }
            public int I(int code, int def = 0)
            {
                var s = Get(code);
                int v;
                return s != null && int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def;
            }
            public IEnumerable<string> All(int code)
            {
                foreach (var f in Fields) if (f.Code == code) yield return f.Value;
            }
        }

        private sealed class Block
        {
            public string Name;
            public double BaseX, BaseY;
            public readonly List<Entity> Entities = new List<Entity>();
        }

        public static Drawing Read(TextReader reader)
        {
            var pairs = new List<Pair>();
            string codeLine;
            while ((codeLine = reader.ReadLine()) != null)
            {
                string val = reader.ReadLine();
                if (val == null) break;
                int code;
                if (int.TryParse(codeLine.Trim(), out code)) pairs.Add(new Pair { Code = code, Value = val.Trim() });
            }

            var blocks = new Dictionary<string, Block>(StringComparer.OrdinalIgnoreCase);
            var model = new List<Entity>();
            string section = null;
            Block current = null;
            for (int i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Code != 0) continue;
                string t = pairs[i].Value;
                if (t == "SECTION")
                {
                    section = i + 1 < pairs.Count && pairs[i + 1].Code == 2 ? pairs[i + 1].Value : null;
                    continue;
                }
                if (t == "ENDSEC") { section = null; current = null; continue; }
                if (t == "EOF") break;

                if (section == "BLOCKS")
                {
                    if (t == "BLOCK")
                    {
                        var head = ReadEntity(pairs, ref i, t);
                        current = new Block { Name = head.Get(2, ""), BaseX = head.D(10), BaseY = head.D(20) };
                        blocks[current.Name] = current;
                        continue;
                    }
                    if (t == "ENDBLK") { current = null; SkipFields(pairs, ref i); continue; }
                    if (current == null) continue;
                    var e = ReadEntity(pairs, ref i, t);
                    if (e != null) current.Entities.Add(e);
                }
                else if (section == "ENTITIES" || section == null)
                {
                    var e = ReadEntity(pairs, ref i, t);
                    if (e != null) model.Add(e);
                }
            }

            var drawing = new Drawing();
            foreach (var e in model) Emit(drawing, e, Transform.Identity, blocks, 0, null);
            return drawing;
        }

        private static void SkipFields(List<Pair> p, ref int i)
        {
            while (i + 1 < p.Count && p[i + 1].Code != 0) i++;
        }

        /// <summary>Reads one entity from its "0 TYPE" to the next "0" (a POLYLINE with its vertices).</summary>
        private static Entity ReadEntity(List<Pair> p, ref int i, string type)
        {
            var e = new Entity { Type = type };
            while (i + 1 < p.Count && p[i + 1].Code != 0) { i++; e.Fields.Add(p[i]); }
            if (type == "POLYLINE")
            {
                while (i + 1 < p.Count && p[i + 1].Code == 0 && p[i + 1].Value == "VERTEX")
                {
                    i++;
                    var v = new Entity { Type = "VERTEX" };
                    while (i + 1 < p.Count && p[i + 1].Code != 0) { i++; v.Fields.Add(p[i]); }
                    e.Vertices.Add(v);
                }
                if (i + 1 < p.Count && p[i + 1].Code == 0 && p[i + 1].Value == "SEQEND")
                {
                    i++;
                    SkipFields(p, ref i);
                }
            }
            return e;
        }

        // ---- placing ------------------------------------------------------------

        private struct Transform
        {
            public double A, B, C, D, E, F;     // x' = A x + B y + E ; y' = C x + D y + F
            public static readonly Transform Identity = new Transform { A = 1, D = 1 };
            public Pt Apply(double x, double y) { return new Pt(A * x + B * y + E, C * x + D * y + F); }
            public Transform Then(Transform o)  // o after this
            {
                return new Transform
                {
                    A = o.A * A + o.B * C, B = o.A * B + o.B * D,
                    C = o.C * A + o.D * C, D = o.C * B + o.D * D,
                    E = o.A * E + o.B * F + o.E, F = o.C * E + o.D * F + o.F
                };
            }
            public bool Mirrors { get { return A * D - B * C < 0; } }
            public double Scale { get { return Math.Sqrt(Math.Abs(A * D - B * C)); } }
        }

        /// <summary>An entity drawn with its extrusion pointing down is mirrored in X.</summary>
        private static Transform Ocs(Entity e)
        {
            return e.D(230, 1) < 0 ? new Transform { A = -1, D = 1 } : Transform.Identity;
        }

        private static void Emit(Drawing d, Entity e, Transform t, Dictionary<string, Block> blocks, int depth, string insertLayer)
        {
            string layer = e.Get(8, "0");
            if (insertLayer != null && layer == "0") layer = insertLayer;
            var ocs = Ocs(e);
            var tt = ocs.Then(t);
            switch (e.Type)
            {
                case "LINE":
                {
                    var path = new Path { Layer = layer, Kind = "LINE" };
                    path.Points.Add(t.Apply(e.D(10), e.D(20)));
                    path.Points.Add(t.Apply(e.D(11), e.D(21)));
                    d.Paths.Add(path);
                    break;
                }
                case "ARC":
                {
                    var path = new Path { Layer = layer, Kind = "ARC" };
                    double a0 = e.D(50) * Math.PI / 180, a1 = e.D(51) * Math.PI / 180;
                    if (a1 < a0) a1 += 2 * Math.PI;
                    AddArc(path.Points, tt, e.D(10), e.D(20), e.D(40), a0, a1);
                    d.Paths.Add(path);
                    break;
                }
                case "CIRCLE":
                {
                    var path = new Path { Layer = layer, Kind = "CIRCLE", Closed = true };
                    AddArc(path.Points, tt, e.D(10), e.D(20), e.D(40), 0, 2 * Math.PI);
                    d.Paths.Add(path);
                    var c = tt.Apply(e.D(10), e.D(20));
                    d.Markers.Add(new Marker { Kind = "CIRCLE", X = c.X, Y = c.Y, Layer = layer, Radius = e.D(40) * tt.Scale });
                    break;
                }
                case "ELLIPSE":
                {
                    var path = new Path { Layer = layer, Kind = "ELLIPSE" };
                    double cx = e.D(10), cy = e.D(20), mx = e.D(11), my = e.D(21), ratio = e.D(40, 1);
                    double p0 = e.D(41, 0), p1 = e.D(42, 2 * Math.PI);
                    if (p1 < p0) p1 += 2 * Math.PI;
                    double nx = -my * ratio, ny = mx * ratio;           // minor axis
                    int steps = Math.Max(8, (int)Math.Ceiling((p1 - p0) / (Math.PI / 18)));
                    for (int s = 0; s <= steps; s++)
                    {
                        double u = p0 + (p1 - p0) * s / steps;
                        path.Points.Add(tt.Apply(cx + mx * Math.Cos(u) + nx * Math.Sin(u), cy + my * Math.Cos(u) + ny * Math.Sin(u)));
                    }
                    path.Closed = Math.Abs(p1 - p0 - 2 * Math.PI) < 1e-6;
                    d.Paths.Add(path);
                    break;
                }
                case "LWPOLYLINE":
                {
                    var pts = new List<Pt>(); var bulges = new List<double>();
                    bool haveX = false; double px = 0;
                    foreach (var f in e.Fields)
                    {
                        if (f.Code == 10) { px = Num(f.Value); haveX = true; }
                        else if (f.Code == 20 && haveX) { pts.Add(new Pt(px, Num(f.Value))); bulges.Add(0); haveX = false; }
                        else if (f.Code == 42 && bulges.Count > 0) bulges[bulges.Count - 1] = Num(f.Value);
                    }
                    d.Paths.Add(PolylinePath(pts, bulges, (e.I(70) & 1) != 0, tt, layer, "LWPOLYLINE"));
                    break;
                }
                case "POLYLINE":
                {
                    int flags = e.I(70);
                    if ((flags & (16 | 64)) != 0) { d.Warnings.Add("a 3D mesh POLYLINE on layer " + layer + " was left out"); break; }
                    var pts = new List<Pt>(); var bulges = new List<double>();
                    foreach (var v in e.Vertices)
                    {
                        if ((v.I(70) & 16) != 0) continue;           // spline frame point
                        pts.Add(new Pt(v.D(10), v.D(20)));
                        bulges.Add(v.D(42));
                    }
                    d.Paths.Add(PolylinePath(pts, bulges, (flags & 1) != 0, tt, layer, "POLYLINE"));
                    break;
                }
                case "SPLINE":
                {
                    var path = new Path { Layer = layer, Kind = "SPLINE", Closed = (e.I(70) & 1) != 0 };
                    var fit = Points(e, 11, 21);
                    var ctrl = Points(e, 10, 20);
                    var use = fit.Count >= 2 ? fit : ctrl;
                    foreach (var p in use) path.Points.Add(t.Apply(p.X, p.Y));
                    if (path.Points.Count >= 2)
                    {
                        d.Paths.Add(path);
                        if (fit.Count < 2) d.Warnings.Add("a SPLINE on layer " + layer + " is drawn through its control points (approximate)");
                    }
                    break;
                }
                case "POINT":
                {
                    var p = t.Apply(e.D(10), e.D(20));
                    d.Markers.Add(new Marker { Kind = "POINT", X = p.X, Y = p.Y, Layer = layer });
                    break;
                }
                case "TEXT":
                case "MTEXT":
                {
                    var p = tt.Apply(e.D(10), e.D(20));
                    string text = e.Type == "MTEXT" ? CleanMText(string.Concat(e.All(3)) + e.Get(1, "")) : e.Get(1, "");
                    d.Markers.Add(new Marker { Kind = "TEXT", X = p.X, Y = p.Y, Text = text.Trim(), Layer = layer });
                    break;
                }
                case "INSERT":
                {
                    Block b;
                    if (!blocks.TryGetValue(e.Get(2, ""), out b)) break;
                    if (depth > 8) { d.Warnings.Add("blocks nested too deep: " + b.Name); break; }
                    double sx = e.D(41, 1), sy = e.D(42, 1), rot = e.D(50) * Math.PI / 180;
                    var ins = ocs.Apply(e.D(10), e.D(20));
                    double cos = Math.Cos(rot), sin = Math.Sin(rot);
                    // local: minus the base point, scaled, turned, moved to the insertion point
                    var local = new Transform
                    {
                        A = cos * sx, B = -sin * sy, C = sin * sx, D = cos * sy,
                        E = ins.X - (cos * sx * b.BaseX - sin * sy * b.BaseY),
                        F = ins.Y - (sin * sx * b.BaseX + cos * sy * b.BaseY)
                    };
                    var mirrorBack = e.D(230, 1) < 0 ? new Transform { A = -1, D = 1 } : Transform.Identity;
                    var total = local.Then(mirrorBack).Then(t);
                    foreach (var child in b.Entities) Emit(d, child, total, blocks, depth + 1, layer);
                    break;
                }
            }
        }

        private static double Num(string s)
        {
            double v;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        private static List<Pt> Points(Entity e, int xCode, int yCode)
        {
            var list = new List<Pt>();
            bool haveX = false; double x = 0;
            foreach (var f in e.Fields)
            {
                if (f.Code == xCode) { x = Num(f.Value); haveX = true; }
                else if (f.Code == yCode && haveX) { list.Add(new Pt(x, Num(f.Value))); haveX = false; }
            }
            return list;
        }

        private static Path PolylinePath(List<Pt> pts, List<double> bulges, bool closed, Transform t, string layer, string kind)
        {
            var path = new Path { Layer = layer, Kind = kind, Closed = closed };
            int n = pts.Count;
            for (int k = 0; k < n; k++)
            {
                path.Points.Add(t.Apply(pts[k].X, pts[k].Y));
                bool last = k == n - 1;
                if (last && !closed) break;
                double bulge = bulges[k];
                if (Math.Abs(bulge) > 1e-9) AddBulge(path.Points, t, pts[k], pts[(k + 1) % n], bulge);
            }
            if (closed && path.Points.Count > 0) path.Points.Add(path.Points[0]);
            return path;
        }

        /// <summary>The arc between two vertices of a polyline, without its end points.</summary>
        private static void AddBulge(List<Pt> outPts, Transform t, Pt a, Pt b, double bulge)
        {
            double theta = 4 * Math.Atan(bulge);
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            if (chord < 1e-12) return;
            double radius = Math.Abs(chord / (2 * Math.Sin(theta / 2)));
            double mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2;
            double h = Math.Sqrt(Math.Max(0, radius * radius - chord * chord / 4));
            double ux = -dy / chord, uy = dx / chord;
            // the centre lies on the side the bulge's sign says, nearer for a small arc
            double side = Math.Abs(theta) > Math.PI ? -1 : 1;
            double cx = mx + Math.Sign(theta) * side * h * ux;
            double cy = my + Math.Sign(theta) * side * h * uy;
            double a0 = Math.Atan2(a.Y - cy, a.X - cx);
            int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(theta) / (Math.PI / 18)));
            for (int s = 1; s < steps; s++)
            {
                double u = a0 + theta * s / steps;
                outPts.Add(t.Apply(cx + radius * Math.Cos(u), cy + radius * Math.Sin(u)));
            }
        }

        private static void AddArc(List<Pt> outPts, Transform t, double cx, double cy, double r, double a0, double a1)
        {
            int steps = Math.Max(4, (int)Math.Ceiling((a1 - a0) / (Math.PI / 18)));
            for (int s = 0; s <= steps; s++)
            {
                double u = a0 + (a1 - a0) * s / steps;
                outPts.Add(t.Apply(cx + r * Math.Cos(u), cy + r * Math.Sin(u)));
            }
        }

        /// <summary>MTEXT without its formatting: "\P" is a new line, "{\fArial;X}" is X.</summary>
        private static string CleanMText(string s)
        {
            s = s.Replace("\\P", " ").Replace("\\~", " ");
            s = Regex.Replace(s, @"\\[A-Za-z][^;\\{}]*;", "");
            s = s.Replace("{", "").Replace("}", "");
            return Regex.Replace(s, @"\s+", " ");
        }
    }
}
