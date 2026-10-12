using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Kaban.Profiles.Geometry
{
    /// <summary>
    /// A profile section in a few numbers, to find the CLOSEST profile already in
    /// the library when a new one is added - its references become the new one's
    /// starting proposals (ReferenceFinder.Template).
    ///
    /// Size, how full the outline is, how long its edges are, how many closed
    /// outlines, and the shape of its top and of its outside face, each sampled
    /// at 16 places. Cheap and deterministic: no training, and it can say why
    /// two profiles look alike.
    /// </summary>
    public sealed class Fingerprint
    {
        public const int Samples = 16;
        public double Width, Height, Area, Perimeter;
        public int ClosedLoops, Lines, Arcs;
        /// <summary>The top's height across the width, 0..1 of the height.</summary>
        public double[] Top = new double[Samples];
        /// <summary>How deep the first wall is from the outside face, up the height, 0..1 of the width.</summary>
        public double[] Side = new double[Samples];

        public static Fingerprint Of(Section s)
        {
            var f = new Fingerprint { Width = s.Width, Height = s.Height, Perimeter = s.Perimeter, Area = s.MaterialArea(0.5) };
            foreach (var p in s.Paths)
            {
                if (!p.IsMaterial) continue;
                bool closed = p.Closed || (p.Points.Count > 2 && Section.SegmentDistance(p.Points[0], p.Points[p.Points.Count - 1], p.Points[p.Points.Count - 1]) < 1e-6);
                if (closed) f.ClosedLoops++;
                if (p.Points.Count == 2) f.Lines++; else f.Arcs++;
            }
            for (int i = 0; i < Samples; i++)
            {
                double y = s.Width * (i + 0.5) / Samples;
                var top = s.FirstMaterial(Section.Side.Top, y);
                f.Top[i] = top.HasValue && s.Height > 0 ? (s.Height - top.Value) / s.Height : 0;
                double z = s.Height * (i + 0.5) / Samples;
                var side = s.FirstMaterial(Section.Side.Outside, z);
                f.Side[i] = side.HasValue && s.Width > 0 ? side.Value / s.Width : 1;
            }
            return f;
        }

        /// <summary>0..1: 1 is the same outline. Size counts as much as shape.</summary>
        public double Similarity(Fingerprint o)
        {
            if (o == null) return 0;
            Func<double, double, double> rel = (a, b) => Math.Abs(a - b) / Math.Max(1e-6, Math.Max(Math.Abs(a), Math.Abs(b)));
            double size = (rel(Width, o.Width) + rel(Height, o.Height)) / 2;
            double fill = Math.Abs(Fill - o.Fill);
            double edges = rel(Perimeter / Math.Max(1e-6, Width + Height), o.Perimeter / Math.Max(1e-6, o.Width + o.Height));
            double shape = 0;
            for (int i = 0; i < Samples; i++) shape += Math.Abs(Top[i] - o.Top[i]) + Math.Abs(Side[i] - o.Side[i]);
            shape /= 2 * Samples;
            double loops = Math.Min(1, Math.Abs(ClosedLoops - o.ClosedLoops) / 5.0);
            double d = 2.0 * size + 1.5 * fill + 0.5 * edges + 3.0 * shape + 0.3 * loops;
            return Math.Exp(-d);
        }

        public double Fill { get { return Width * Height > 0 ? Area / (Width * Height) : 0; } }

        /// <summary>The fingerprint as text, for the profile_fingerprint.signature column.</summary>
        public string Pack()
        {
            var c = CultureInfo.InvariantCulture;
            return "v1|" + Width.ToString("0.###", c) + "|" + Height.ToString("0.###", c) + "|" + Area.ToString("0.##", c) + "|" +
                   Perimeter.ToString("0.##", c) + "|" + ClosedLoops + "|" + Lines + "|" + Arcs + "|" +
                   string.Join(",", Top.Select(x => x.ToString("0.###", c))) + "|" + string.Join(",", Side.Select(x => x.ToString("0.###", c)));
        }

        public static Fingerprint Unpack(string s)
        {
            try
            {
                var f = (s ?? "").Split('|');
                if (f.Length < 10 || f[0] != "v1") return null;
                var c = CultureInfo.InvariantCulture;
                Func<string, double> d = x => double.Parse(x, NumberStyles.Float, c);
                var r = new Fingerprint
                {
                    Width = d(f[1]), Height = d(f[2]), Area = d(f[3]), Perimeter = d(f[4]),
                    ClosedLoops = int.Parse(f[5], c), Lines = int.Parse(f[6], c), Arcs = int.Parse(f[7], c)
                };
                r.Top = f[8].Split(',').Select(d).ToArray();
                r.Side = f[9].Split(',').Select(d).ToArray();
                if (r.Top.Length != Samples || r.Side.Length != Samples) return null;
                return r;
            }
            catch { return null; }
        }
    }
}
