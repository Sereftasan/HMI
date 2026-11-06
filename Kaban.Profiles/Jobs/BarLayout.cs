using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Kaban.Profiles.Jobs
{
    /// <summary>What the cutting of a bar takes besides the pieces: the saw, the profile, the machine's limits.</summary>
    public sealed class CutSettings
    {
        /// <summary>The main saw blade's thickness (mm): half of it, at the cut's angle, on each side of a cut.</summary>
        public double SawThickness;
        /// <summary>The old program's saw "vertical share" (mm): added at an angled cut, over its tangent.</summary>
        public double SawVerticalShare;
        /// <summary>The profile's height (mm): an end cut over 90 degrees slips it over its tangent.</summary>
        public double ProfileHeight;
        /// <summary>The profile's ear allowance / mould difference (mm) - its machine setting.</summary>
        public double MouldDifference;
        /// <summary>A leftover this long or longer is squared (a 90 degree cut) when the last cut is not.</summary>
        public double MinimumLeftover;
        /// <summary>0: none. A longer leftover is cut into pieces of at most this.</summary>
        public double MaximumCutLength;
        /// <summary>The machine's special cut recipes (none: a special end cannot be cut).</summary>
        public List<SpecialCutRecipe> Recipes = new List<SpecialCutRecipe>();
        /// <summary>The V saws' distance (the formulas' D) and the profile's width (W: its Cut Full Width).</summary>
        public double VSawsDistance, ProfileWidth;

        /// <summary>The values of the recipes' formulas for a joint (T the blade, O the vertical share, D, W; L and R the joint's).</summary>
        public FormulaValues Values(SpecialJoint j)
        {
            return new FormulaValues { T = SawThickness, O = SawVerticalShare, D = VSawsDistance, W = ProfileWidth, L = j != null ? j.LeftParam : 0, R = j != null ? j.RightParam : 0 };
        }
    }

    public enum StepKind { LeftCut, RightCut, Accessory, LeftoverCut, VCut }

    /// <summary>One step on the bar: a cut, or an accessory at its place.</summary>
    public sealed class LayoutStep
    {
        public StepKind Kind;
        /// <summary>Along the bar from its start (mm).</summary>
        public double Position;
        /// <summary>A cut's angle (degrees).</summary>
        public double Angle;
        /// <summary>The piece (its index in the bar) - -1 for the leftover's cuts.</summary>
        public int Piece = -1;
        public KbnRec Rec;
        /// <summary>The order it was made in (the old program's item number order).</summary>
        public int Order;
        /// <summary>A right cut that is also the next piece's left end (their angles add up to 180, or both are square).</summary>
        public bool Shared;
        /// <summary>A cut a special cut makes (its recipe's): the joint.</summary>
        public SpecialJoint Joint;
        /// <summary>A V cut: the job file's line.</summary>
        public KbnVCut VCut;

        public bool IsCut { get { return Kind != StepKind.Accessory && Kind != StepKind.VCut; } }

        /// <summary>
        /// The PLC's code of a cut (FA2010_DeviceList: "Kesim: 1000 + angle x 10"): the saw at the left end of a piece turns to its angle,
        /// at the right end to 180 minus it - 1000 + 10 x angle, 2800 - 10 x angle (the old program's GetAngleForPLC).
        /// </summary>
        public int CutCode
        {
            get
            {
                if (!IsCut) return 0;
                return Kind == StepKind.RightCut ? (int)Math.Round(2800 - Angle * 10) : (int)Math.Round(1000 + Angle * 10);
            }
        }
    }

    public sealed class BarLayoutResult
    {
        /// <summary>The steps as they were made.</summary>
        public readonly List<LayoutStep> Steps = new List<LayoutStep>();
        /// <summary>Along the bar (the order the PLC gets them).</summary>
        public List<LayoutStep> Ordered = new List<LayoutStep>();
        /// <summary>The bar's length the pieces and the saw take (mm).</summary>
        public double Used;
        /// <summary>What is left at the end (mm).</summary>
        public double Leftover;
        public readonly List<string> Problems = new List<string>();
        public readonly List<string> Notes = new List<string>();
        /// <summary>The bar's joints a special cut makes, in order (the bar's start, between the pieces, its end).</summary>
        public readonly List<SpecialJoint> Joints = new List<SpecialJoint>();
    }

    /// <summary>
    /// WHERE EVERY CUT AND ACCESSORY OF A BAR GOES - the old program's ProfileProcess (its normal cutting, LeftCutting / RightCutting /
    /// AddAccessory / Calc_LeftOverLenght), with the same numbers:
    ///
    ///   - a cut is at the saw's centre line; half the blade, over the sine of the cut's angle, on each side of it;
    ///   - at an angled cut the saw's vertical share (and the profile's mould difference) over the angle's tangent - taken off over 90 degrees;
    ///   - a piece's left cut that its neighbour's right cut makes already (their angles add up to 180, or both are square) is not cut again;
    ///   - an end over 90 degrees slips the profile's height over its tangent: the length along the bar and the accessories move by it;
    ///   - an accessory at its position from the piece's start; the leftover squared when the last cut is not square, and cut into pieces
    ///     the machine can take.
    ///
    /// One difference: for a right cut of 90 degrees or less, the old program passed the cut's ANGLE where the profile's height goes
    /// (ProfileProcess.RightCutting: barItem.CutlengthRobot(angle)) - which changes only a piece whose left end is over 90 degrees. The
    /// height is used here.
    /// </summary>
    public static class BarLayout
    {
        private const double Rad = Math.PI / 180;

        public static BarLayoutResult Of(KbnBar bar, CutSettings s)
        {
            var r = new BarLayoutResult();
            s = s ?? new CutSettings();
            var st = new State(bar, s, r);
            var ci = CultureInfo.InvariantCulture;
            // the joints special cuts make: before the first piece, between the pieces, after the last (each with its recipe)
            int n = bar.Pieces.Count;
            var joints = new SpecialJoint[n + 1];
            for (int k = 0; k <= n && n > 0; k++)
            {
                var j = k < n ? SpecialCuts.LeftOf(bar, k) : SpecialCuts.RightOf(bar, n - 1);
                if (j == null) continue;
                j.Recipe = SpecialCuts.Find(s.Recipes, j);
                if (j.Recipe == null)
                {
                    r.Problems.Add(string.Format(ci, "{0}: no special cut recipe for it ({1}) - nor one of each end to merge (the Jobs page's special cut recipes)",
                        Where(j), j.Describe()));
                    continue;      // cut as an ordinary end: the layout still shown
                }
                joints[k] = j;
                r.Joints.Add(j);
            }
            for (int i = 0; i < n; i++)
            {
                var p = bar.Pieces[i];
                var lj = joints[i];
                var rj = joints[i + 1];
                if (lj != null) st.SpecialLeftCut(p.LeftAngle, i, Half(lj, s, r), lj);
                else st.LeftCut(p.LeftAngle, i, StepKind.LeftCut);
                double slip = p.LeftAngle > 90 ? Math.Abs(st.H / Math.Tan(p.LeftAngle * Rad)) : 0;
                foreach (var rec in p.Recs)
                {
                    if (rec.Position < 0 || rec.Position > p.CutLength)
                        r.Notes.Add(string.Format(ci, "piece {0}: {1} at {2:0.#} mm is outside the piece ({3:0.#} mm)", i + 1, rec.Code, rec.Position, p.CutLength));
                    st.Add(StepKind.Accessory, st.Length + rec.Position - slip, 0, i, rec);
                }
                // its V cuts (the old program's AddVCutting: after the accessories, the same slip)
                foreach (var v in p.VCuts)
                {
                    if (v.Position < 0 || v.Position > p.CutLength)
                        r.Notes.Add(string.Format(ci, "piece {0}: a V cut at {1:0.#} mm is outside the piece ({2:0.#} mm)", i + 1, v.Position, p.CutLength));
                    st.Add(StepKind.VCut, st.Length + v.Position - slip, 0, i, null).VCut = v;
                }
                if (rj != null) st.SpecialRightCut(p, i, Half(rj, s, r), rj);
                // a piece with a special end: its ordinary right cut without the mould difference (the old program's AddSpecialCutting)
                else st.RightCut(p, i, lj == null);
            }
            // a bar that ends in a special cut: its leftover from the recipe's angle, moved by the robot's X offset
            if (n > 0 && joints[n] != null) st.SpecialLeftover(joints[n], s);
            else if (n > 0) st.Leftover();
            r.Used = st.Length;
            r.Leftover = bar.Pieces.Count > 0 ? st.LastLeftover : bar.Length;
            if (bar.Pieces.Count > 0 && r.Leftover < -1e-6)
                r.Problems.Add(string.Format(CultureInfo.InvariantCulture, "the pieces and the saw take {0:0.#} mm - the bar is {1:0.#} mm", bar.Length - r.Leftover, bar.Length));
            r.Ordered = r.Steps.OrderBy(x => x.Position).ThenBy(x => x.Order).ToList();
            return r;
        }

        /// <summary>The joint in words: "piece 2's left end", "between pieces 1 and 2", "piece 3's right end".</summary>
        private static string Where(SpecialJoint j)
        {
            var ci = CultureInfo.InvariantCulture;
            if (j.Before < 0) return string.Format(ci, "piece {0}'s left end", j.After + 1);
            if (j.After < 0) return string.Format(ci, "piece {0}'s right end", j.Before + 1);
            return string.Format(ci, "between pieces {0} and {1}", j.Before + 1, j.After + 1);
        }

        /// <summary>Half the width the saw takes at a special joint (its recipe's saw dust width).</summary>
        private static double Half(SpecialJoint j, CutSettings s, BarLayoutResult r)
        {
            double w;
            string why;
            if (!SpecialCuts.Eval(j.Recipe.SawDustWidth, s.Values(j), out w, out why))
                r.Problems.Add(string.Format(CultureInfo.InvariantCulture, "{0}: recipe {1}'s saw dust width - {2}", Where(j), j.Recipe.Title, why));
            return w / 2;
        }

        private sealed class State
        {
            private readonly KbnBar _bar;
            private readonly CutSettings _s;
            private readonly BarLayoutResult _r;
            public double Length;              // CuttingLenght
            private double _prev;              // PrevAnle
            private double _lastRightHalf;     // LastRight_Cutting_HalfSawShare
            private LayoutStep _lastCut;
            public double H { get { return _s.ProfileHeight; } }

            public State(KbnBar bar, CutSettings s, BarLayoutResult r) { _bar = bar; _s = s; _r = r; }

            private double Half(double a) { return Math.Abs(_s.SawThickness / 2 / Math.Sin(a * Rad)); }

            private double VerticalShare(double a)
            {
                if (a >= 90) a = 180 - a;
                return (_s.SawVerticalShare + _s.MouldDifference) / Math.Tan(a * Rad);
            }

            private double Slip(double a) { return H / Math.Tan(a * Rad); }

            public LayoutStep Add(StepKind kind, double position, double angle, int piece, KbnRec rec)
            {
                var step = new LayoutStep { Kind = kind, Position = position, Angle = angle, Piece = piece, Rec = rec, Order = _r.Steps.Count };
                _r.Steps.Add(step);
                if (step.IsCut) _lastCut = step;
                return step;
            }

            private static double LeftOf(double a) { return -1 * Math.Sign(a) * (90 - Math.Abs(a)); }
            private static double RightOf(double a) { return Math.Sign(a) * (90 - Math.Abs(a)); }
            private static bool Square(double a) { return a == 90 || a == 180; }

            /// <summary>CutMethod: the cut at the current length - none for a left end its neighbour's right cut makes already.</summary>
            private LayoutStep Cut(double angle, StepKind kind, int piece)
            {
                if (kind != StepKind.RightCut && _prev != 0)
                {
                    if (Math.Abs(LeftOf(angle) - RightOf(_prev)) < 1e-9 || (Square(_prev) && Square(angle)))
                    {
                        if (_lastCut != null && _lastCut.Kind == StepKind.RightCut) _lastCut.Shared = true;
                        return null;
                    }
                }
                _prev = angle;
                return Add(kind, Length, angle, piece, null);
            }

            /// <summary>An ordinary left cut; what the profile's height slips more than the cut before it (the leftover's offset limit).</summary>
            public double LeftCut(double angle, int piece, StepKind kind, double? prevAngle = null)
            {
                double half = Half(angle);
                double prev = prevAngle ?? (_prev == 0 ? 90 : _prev);
                if (prevAngle.HasValue) _prev = prevAngle.Value;
                double additional = 0;
                if (_lastRightHalf > half) Length += _lastRightHalf - half;
                if (angle + prev > 180) { additional = Math.Abs(Math.Abs(Slip(angle)) - Slip(prev)); Length += additional; }
                Cut(angle, kind, piece);
                if (angle > 90) Length -= VerticalShare(angle);
                else Length += VerticalShare(angle);
                Length += half;
                _lastRightHalf = 0;
                return additional;
            }

            /// <summary>SpecialLeftCutting: the cut, then half the recipe's saw dust width - no vertical share, no slip.</summary>
            public void SpecialLeftCut(double angle, int piece, double half, SpecialJoint j)
            {
                var c = Cut(angle, StepKind.LeftCut, piece);
                if (c != null) c.Joint = j;
                Length += half;
            }

            /// <summary>SpecialRightCutting: half the saw dust width, the piece's whole length (its ends' slip not taken off), the cut.</summary>
            public void SpecialRightCut(KbnPiece p, int piece, double half, SpecialJoint j)
            {
                Length += half;
                Length += p.CutLength;
                _lastRightHalf = half;
                var c = Cut(p.RightAngle, StepKind.RightCut, piece);
                if (c != null) c.Joint = j;
            }

            /// <summary>
            /// The leftover after a special right end (the old program's AddBar): squared as from the recipe's angle (180 - its starting
            /// angle), then the robot's X offset added (no less than what the height slips) - the last cut moved there.
            /// </summary>
            public void SpecialLeftover(SpecialJoint j, CutSettings s)
            {
                double angle = 180 - j.Recipe.StartingAngle;
                double additional = 0;
                if (_lastCut != null && LastLeftover >= _s.MinimumLeftover && !Square(angle))
                    additional = LeftCut(90, -1, StepKind.LeftoverCut, angle);
                PieceUp();
                double offset;
                string why;
                if (!SpecialCuts.Eval(j.Recipe.RobotXOffset, s.Values(j), out offset, out why))
                    _r.Problems.Add("the bar's last special cut: recipe " + j.Recipe.Title + "'s robot X offset - " + why);
                if (offset < -additional) offset = -additional;
                Length += offset;
                if (_r.Steps.Count > 0) _r.Steps[_r.Steps.Count - 1].Position = Length;
            }

            /// <summary>The piece's length along the bar (CutlengthRobot): an end over 90 degrees takes the slip of the height off.</summary>
            private double Along(KbnPiece p, double extra)
            {
                double l = p.CutLength;
                if (p.LeftAngle > 90) l -= Math.Abs((H + extra) / Math.Tan(p.LeftAngle * Rad));
                if (p.RightAngle > 90) l -= Math.Abs((H + extra) / Math.Tan(p.RightAngle * Rad));
                return l;
            }

            public void RightCut(KbnPiece p, int piece, bool mould = true)
            {
                double angle = p.RightAngle;
                if (angle > 90) Length += Along(p, _s.SawVerticalShare);
                else
                {
                    Length += mould ? VerticalShare(angle) : _s.SawVerticalShare / Math.Tan((angle >= 90 ? 180 - angle : angle) * Rad);
                    Length += Along(p, 0);
                }
                _lastRightHalf = Half(angle);
                Length += _lastRightHalf;
                Cut(angle, StepKind.RightCut, piece);
            }

            public double LastLeftover
            {
                get { return _bar.Length - Length - (_lastCut != null ? Half(_lastCut.Angle) : 0); }
            }

            /// <summary>Calc_LeftOverLenght: the leftover squared, and cut into pieces the machine can take.</summary>
            public void Leftover()
            {
                double angle = _lastCut != null ? _lastCut.Angle : 90;
                if (_lastCut != null && LastLeftover >= _s.MinimumLeftover && !Square(angle))
                    LeftCut(90, -1, StepKind.LeftoverCut);
                PieceUp();
            }

            /// <summary>The leftover cut into pieces the machine can take.</summary>
            private void PieceUp()
            {
                double max = _s.MaximumCutLength;
                if (max > 0) max -= _s.SawThickness;
                if (max > 0 && LastLeftover > max) Piece(LastLeftover % max);
                int guard = 0;
                while (max > 0 && LastLeftover > max && guard++ < 1000) Piece(max);
            }

            /// <summary>LeftCutting_LeftOver: a piece of the leftover cut off.</summary>
            private void Piece(double length)
            {
                Length += length;
                _prev = 0;
                double half = Half(90);
                Length += half;
                Cut(90, StepKind.LeftoverCut, -1);
                Length += half;
            }
        }

        /// <summary>A step in words: "left cut 45°", "NX-HNG-STD".</summary>
        public static string Describe(LayoutStep s)
        {
            var ci = CultureInfo.InvariantCulture;
            switch (s.Kind)
            {
                case StepKind.LeftCut: return "left cut " + s.Angle.ToString("0.#", ci) + "°";
                case StepKind.RightCut: return "right cut " + s.Angle.ToString("0.#", ci) + "°" + (s.Shared ? " (also the next piece's left end)" : "");
                case StepKind.LeftoverCut: return "leftover cut " + s.Angle.ToString("0.#", ci) + "°";
                case StepKind.VCut: return "V cut" + (s.VCut != null && s.VCut.Depth != 0 ? " (depth " + s.VCut.Depth.ToString("0.#", ci) + ")" : "");
                default: return s.Rec != null ? s.Rec.Code : "accessory";
            }
        }
    }
}
