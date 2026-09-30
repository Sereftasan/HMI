using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kaban.Profiles.Geometry
{
    /// <summary>
    /// FINDS A NEW PROFILE'S REFERENCE POINTS on its drawing - so they need not be
    /// typed in - and says how sure it is of each. Nothing it finds is used for
    /// cutting until a person confirms it (the resolver's PROPOSED status).
    ///
    /// In order of trust:
    ///   1. a LAYER named after the reference (REF_HARDWARE_AXIS, or HARDWARE_AXIS):
    ///      a point, circle or text on it, or the line drawn on it;          1.00
    ///   2. a TEXT naming it - "HARDWARE_AXIS 22 35" gives the coordinates, a
    ///      bare "HARDWARE_AXIS" is where the text stands (several in one text,
    ///      as "OUTSIDE_SURFACE 0 0 INSIDE_SURFACE 70 0 ...", all count);  1.00 / 0.90
    ///   3. the OUTLINE: outside face, inside face, centre and bed from the
    ///      section's extent (right when the drawing is turned the right way); 0.95
    ///   4. the HARDWARE GROOVE seen from above: a dip in the top as wide as the
    ///      group's hardware interface (16 mm for EURO_GROOVE_16, within 15 %); 0.80
    ///   5. the same reference of a SIMILAR PROFILE (the group's reference
    ///      profile, the base profile, or the closest one): kept at the same
    ///      distance from the nearer face.                           0.60 x similarity
    ///
    /// Deterministic geometry, not a trained model: every proposal says how it was
    /// found, and a person confirms or moves it on the drawing. Confirmed
    /// profiles become the templates the next profiles are compared with.
    /// </summary>
    public static class ReferenceFinder
    {
        public static readonly string[] Codes =
        {
            "OUTSIDE_SURFACE", "INSIDE_SURFACE", "CENTER", "HARDWARE_AXIS",
            "GLAZING_REFERENCE", "REBATE_LINE", "BED_DATUM", "CHAMBER_CENTRE"
        };

        public sealed class Found
        {
            public string Code = "";
            public double Y, Z;
            /// <summary>DXF_LAYER, DXF_MARKER, DXF_GEOMETRY or AI_PROPOSED (the reference_type's sources).</summary>
            public string Source = "";
            public double Confidence;
            /// <summary>How it was found, in words.</summary>
            public string How = "";
            public override string ToString()
            {
                return Code + " " + Y.ToString("0.##", CultureInfo.InvariantCulture) + " / " + Z.ToString("0.##", CultureInfo.InvariantCulture) +
                       " (" + Source + " " + Confidence.ToString("0.00", CultureInfo.InvariantCulture) + ": " + How + ")";
            }
        }

        /// <summary>A profile whose references are known, to transfer from.</summary>
        public sealed class Template
        {
            public string ProfileCode = "";
            public double Width, Height;
            public double Similarity = 1;
            public readonly Dictionary<string, Section.Pt> References = new Dictionary<string, Section.Pt>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>A code as written on a drawing ("Hardware axis", "REF-HARDWARE-AXIS") as the library's code, or null.</summary>
        public static string Normalize(string text)
        {
            var t = Regex.Replace((text ?? "").Trim().ToUpperInvariant(), @"[\s\-\.]+", "_");
            if (t.StartsWith("REF_")) t = t.Substring(4);
            if (t == "CENTRE") t = "CENTER";
            if (t == "CHAMBER_CENTER") t = "CHAMBER_CENTRE";
            if (t == "OUTSIDE") t = "OUTSIDE_SURFACE";
            if (t == "INSIDE") t = "INSIDE_SURFACE";
            if (t == "HARDWARE") t = "HARDWARE_AXIS";
            if (t == "GLAZING") t = "GLAZING_REFERENCE";
            if (t == "BED") t = "BED_DATUM";
            return Codes.Contains(t) ? t : null;
        }

        /// <summary>
        /// Every reference found on <paramref name="s"/>. <paramref name="grooveWidth"/>:
        /// the hardware interface's groove (null: not looked for).
        /// </summary>
        public static List<Found> Find(Section s, double? grooveWidth, Template template)
        {
            var found = new Dictionary<string, Found>(StringComparer.OrdinalIgnoreCase);
            Action<Found> offer = f =>
            {
                Found had;
                if (!found.TryGetValue(f.Code, out had) || f.Confidence > had.Confidence) found[f.Code] = f;
            };

            // 1. layers named after a reference
            foreach (var layer in s.Layers.Keys.Concat(s.Markers.Select(m => m.Layer)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var code = Normalize(layer);
                if (code == null) continue;
                var marks = s.Markers.Where(m => string.Equals(m.Layer, layer, StringComparison.OrdinalIgnoreCase)).ToList();
                var point = marks.FirstOrDefault(m => m.Kind == "POINT") ?? marks.FirstOrDefault(m => m.Kind == "CIRCLE");
                if (point != null)
                {
                    offer(new Found { Code = code, Y = point.Y, Z = point.Z, Source = "DXF_LAYER", Confidence = 1, How = "the " + point.Kind.ToLowerInvariant() + " on layer " + layer });
                    continue;
                }
                var paths = s.Paths.Where(p => string.Equals(p.Layer, layer, StringComparison.OrdinalIgnoreCase)).ToList();
                if (paths.Count > 0)
                {
                    double minY = paths.SelectMany(p => p.Points).Min(q => q.Y), maxY = paths.SelectMany(p => p.Points).Max(q => q.Y);
                    double minZ = paths.SelectMany(p => p.Points).Min(q => q.Z), maxZ = paths.SelectMany(p => p.Points).Max(q => q.Z);
                    double y = (minY + maxY) / 2, z = (minZ + maxZ) / 2;
                    string how = "the line on layer " + layer;
                    if (maxY - minY < 0.01) { z = Math.Max(0, minZ); how = "the vertical line on layer " + layer; }        // a plane across: its Y
                    else if (maxZ - minZ < 0.01) { y = Math.Max(0, minY); how = "the horizontal line on layer " + layer; }  // a plane along: its Z
                    offer(new Found { Code = code, Y = y, Z = z, Source = "DXF_LAYER", Confidence = 1, How = how });
                    continue;
                }
                var text = marks.FirstOrDefault(m => m.Kind == "TEXT");
                if (text != null)
                    offer(new Found { Code = code, Y = text.Y, Z = text.Z, Source = "DXF_LAYER", Confidence = 0.95, How = "the text on layer " + layer });
            }

            // 2. texts naming references
            var rx = new Regex(@"(REF[_\- ])?([A-Za-z]+(?:[_\- ][A-Za-z]+)*)\s*[:=]?\s*(-?\d+(?:[.,]\d+)?)?\s*[;/ ,]?\s*(-?\d+(?:[.,]\d+)?)?");
            foreach (var m in s.Markers.Where(x => x.Kind == "TEXT" && !string.IsNullOrWhiteSpace(x.Text)))
            {
                foreach (Match mm in rx.Matches(m.Text))
                {
                    var code = Normalize(mm.Groups[2].Value);
                    if (code == null) continue;
                    if (mm.Groups[3].Success && mm.Groups[4].Success)
                        offer(new Found { Code = code, Y = Num(mm.Groups[3].Value), Z = Num(mm.Groups[4].Value), Source = "DXF_MARKER", Confidence = 1,
                                          How = "written on the drawing: \"" + mm.Value.Trim() + "\"" });
                    else
                        offer(new Found { Code = code, Y = m.Y, Z = m.Z, Source = "DXF_MARKER", Confidence = 0.9,
                                          How = "where the text \"" + m.Text.Trim() + "\" stands" });
                }
            }

            // 3. the outline
            if (s.Width > 0 && s.Height > 0)
            {
                offer(new Found { Code = "OUTSIDE_SURFACE", Y = 0, Z = 0, Source = "DXF_GEOMETRY", Confidence = 0.95, How = "the outline's outside face (left)" });
                offer(new Found { Code = "INSIDE_SURFACE", Y = s.Width, Z = 0, Source = "DXF_GEOMETRY", Confidence = 0.95, How = "the outline's inside face (right)" });
                offer(new Found { Code = "CENTER", Y = s.Width / 2, Z = 0, Source = "DXF_GEOMETRY", Confidence = 0.95, How = "half way between the faces" });
                offer(new Found { Code = "BED_DATUM", Y = 0, Z = 0, Source = "DXF_GEOMETRY", Confidence = 0.95, How = "the outline's bottom" });
            }

            // 4. the hardware groove, seen from above
            Found tAxis = null;
            if (template != null && template.References.ContainsKey("HARDWARE_AXIS"))
                tAxis = Transfer(template, "HARDWARE_AXIS", s.Width, s.Height);
            if (grooveWidth.HasValue && grooveWidth.Value > 0 && !found.ContainsKey("HARDWARE_AXIS"))
            {
                var groove = FindGroove(s, grooveWidth.Value, tAxis != null ? tAxis.Y : (double?)null);
                if (groove != null) offer(groove);
            }

            // 5. from a similar profile
            if (template != null)
                foreach (var kv in template.References)
                {
                    if (found.ContainsKey(kv.Key)) continue;
                    var f = Transfer(template, kv.Key, s.Width, s.Height);
                    if (f != null) offer(f);
                }

            return Codes.Where(found.ContainsKey).Select(c => found[c]).ToList();
        }

        private static double Num(string s)
        {
            return double.Parse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A reference of the template on a profile <paramref name="w"/> x <paramref name="h"/>:
        /// at the same distance from the NEARER face (a groove 22 mm from the outside
        /// stays 22 mm from the outside on a deeper profile of the same family).
        /// </summary>
        public static Found Transfer(Template t, string code, double w, double h)
        {
            Section.Pt p;
            if (!t.References.TryGetValue(code, out p) || t.Width <= 0 || t.Height <= 0) return null;
            double y = p.Y <= t.Width / 2 ? p.Y : w - (t.Width - p.Y);
            double z = p.Z <= t.Height / 2 ? p.Z : h - (t.Height - p.Z);
            if (code == "CENTER") y = w / 2;
            return new Found
            {
                Code = code, Y = y, Z = z, Source = "AI_PROPOSED",
                Confidence = Math.Round(0.6 * Math.Max(0, Math.Min(1, t.Similarity)), 2),
                How = "as on " + t.ProfileCode + " (" + (p.Y <= t.Width / 2 ? "same distance from the outside" : "same distance from the inside") +
                      (p.Z <= t.Height / 2 ? ", from the bed)" : ", from the top)")
            };
        }

        /// <summary>
        /// A dip in the profile's top about <paramref name="width"/> wide (within 15 %:
        /// a 13 mm groove is not a 16 mm one) and at least 1.5 mm deep - the Euro groove - nearest to
        /// <paramref name="near"/> when given. Its axis is the dip's middle, at the
        /// height of its rims.
        /// </summary>
        public static Found FindGroove(Section s, double width, double? near)
        {
            const double step = 0.25;
            var top = s.TopProfile(step);
            if (top.Length < 5) return null;
            var candidates = new List<Found>();
            int i = 1;
            while (i < top.Length - 1)
            {
                // a rim, then lower than it by 1.5 mm or more, then a rim again
                if (double.IsNaN(top[i - 1]) || double.IsNaN(top[i]) || top[i] > top[i - 1] - 1.5) { i++; continue; }
                double rimLeft = top[i - 1];
                int j = i;
                while (j < top.Length && !double.IsNaN(top[j]) && top[j] < rimLeft - 1.0) j++;
                if (j >= top.Length || double.IsNaN(top[j])) { i = j + 1; continue; }
                double rimRight = top[j];
                double rim = Math.Min(rimLeft, rimRight);
                double w = (j - i) * step;
                double depth = rim - top.Skip(i).Take(j - i).Min();
                if (Math.Abs(w - width) <= width * 0.15 && depth >= 1.5)
                {
                    double y = (i - 1 + j) / 2.0 * step + step / 2;
                    candidates.Add(new Found
                    {
                        Code = "HARDWARE_AXIS", Y = Math.Round(y, 2), Z = Math.Round(rim, 2), Source = "DXF_GEOMETRY", Confidence = 0.8,
                        How = "a groove " + w.ToString("0.#", CultureInfo.InvariantCulture) + " mm wide and " +
                              depth.ToString("0.#", CultureInfo.InvariantCulture) + " mm deep in the top"
                    });
                }
                i = j + 1;
            }
            if (candidates.Count == 0) return null;
            return near.HasValue ? candidates.OrderBy(c => Math.Abs(c.Y - near.Value)).First() : candidates.First();
        }

        /// <summary>The groove width of a hardware interface's code: EURO_GROOVE_16 -> 16.</summary>
        public static double? GrooveWidthOf(string interfaceCode)
        {
            var m = Regex.Match(interfaceCode ?? "", @"(\d+(?:\.\d+)?)\s*$");
            return m.Success ? double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : (double?)null;
        }
    }
}
