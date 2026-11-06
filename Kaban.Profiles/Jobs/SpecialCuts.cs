using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Kaban.Profiles.Jobs
{
    /// <summary>What a step of a special cut does (the PLC's operation code - the old program's CommandEnum).</summary>
    public enum SpecialCutCommand
    {
        Clamp = 1, Unclamp = 2, RobotsMove = 3, NormalCut = 4, VCut = 5, ExitRobotPulls = 6, Merge = 7
    }

    /// <summary>
    /// One step of a special cut recipe (the old program's SawReceiptDetail): what it does, the clamps, where the robots go, the saw's
    /// angle, the V saw's Y. Robot 1, Robot 2 and V cut Y are formulas (<see cref="SpecialCuts.Eval"/>).
    /// </summary>
    public sealed class SpecialCutStep
    {
        public int Command = (int)SpecialCutCommand.NormalCut;
        public bool LeftClamp, RightClamp;
        public string Robot1 = "", Robot2 = "", VCutY = "";
        public double Angle;

        public SpecialCutStep Clone() { return (SpecialCutStep)MemberwiseClone(); }

        /// <summary>The fields a command uses (the old program's editor let only these be typed).</summary>
        public static bool Uses(int command, string field)
        {
            switch ((SpecialCutCommand)command)
            {
                case SpecialCutCommand.Clamp:
                case SpecialCutCommand.Unclamp: return field == "LeftClamp" || field == "RightClamp";
                case SpecialCutCommand.RobotsMove: return field == "Robot1" || field == "Robot2";
                case SpecialCutCommand.NormalCut: return field == "Angle";
                case SpecialCutCommand.VCut: return field == "VCutY";
                case SpecialCutCommand.ExitRobotPulls: return field == "Robot2";
                case SpecialCutCommand.Merge: return field == "Robot1";
                default: return false;
            }
        }
    }

    /// <summary>
    /// A SPECIAL CUT RECIPE (the old program's SawReceipt, "&lt;&gt; KESİM REÇETESİ"): how the saw, the robots and the clamps make the joint
    /// between two pieces whose ends are special - the left piece's right end (<see cref="RightCode"/>) and the right piece's left end
    /// (<see cref="LeftCode"/>). An ordinary end's code is its angle (90, 45 ...); 0 there means "any": a recipe of one special end only,
    /// merged with one of the other end when no recipe has both.
    /// </summary>
    public sealed class SpecialCutRecipe
    {
        public int No;
        public string Name = "";
        public double LeftCode, RightCode;
        /// <summary>The saw's angle at the start (degrees) - the special cut's parameters' Cut Angle.</summary>
        public double StartingAngle;
        /// <summary>Formulas: the exit robot's holding position, the robot's X offset, the width the saw takes at the joint.</summary>
        public string HoldingPosition = "", RobotXOffset = "", SawDustWidth = "";
        /// <summary>The recipe for an end that has a value (L / R not 0) - one without is taken only when the end has none.</summary>
        public bool WithOffsetLeft, WithOffsetRight;
        public List<SpecialCutStep> Steps = new List<SpecialCutStep>();
        /// <summary>Made of a right end's recipe and a left end's (no recipe had both): their numbers.</summary>
        public string MergedOf = "";

        public SpecialCutRecipe Clone()
        {
            var c = (SpecialCutRecipe)MemberwiseClone();
            c.Steps = Steps.Select(s => s.Clone()).ToList();
            return c;
        }

        public string Title
        {
            get
            {
                var ci = CultureInfo.InvariantCulture;
                return (MergedOf.Length > 0 ? MergedOf : No.ToString(ci)) + (Name.Length > 0 ? " " + Name : "");
            }
        }
    }

    /// <summary>
    /// A joint of the bar that a special cut makes: the right end of the piece before it (<see cref="Before"/>, -1 at the bar's start)
    /// and the left end of the piece after it (<see cref="After"/>, -1 at the bar's end) - the codes and values its recipe is found by
    /// (the old program's SpecialCutInfo).
    /// </summary>
    public sealed class SpecialJoint
    {
        public double LeftCode, RightCode, LeftParam, RightParam;
        public int Before = -1, After = -1;
        /// <summary>Its recipe (null: none found - the bar cannot go).</summary>
        public SpecialCutRecipe Recipe;
        /// <summary>The bar's special cut number (1 - 10): the PLC's code 1000 + it, its parameters at R25650 + 5 x (it - 1).</summary>
        public int Number;

        public string Key
        {
            get
            {
                var ci = CultureInfo.InvariantCulture;
                return LeftCode.ToString("R", ci) + "|" + RightCode.ToString("R", ci) + "|" + LeftParam.ToString("R", ci) + "|" + RightParam.ToString("R", ci);
            }
        }

        public string Describe()
        {
            var ci = CultureInfo.InvariantCulture;
            return string.Format(ci, "right end {0}, left end {1}{2}", RightCode.ToString("0.#", ci), LeftCode.ToString("0.#", ci),
                LeftParam != 0 || RightParam != 0 ? string.Format(ci, " (L {0:0.##}, R {1:0.##})", LeftParam, RightParam) : "");
        }
    }

    /// <summary>The values a recipe's formulas take: T, D, W, O, L, R.</summary>
    public sealed class FormulaValues
    {
        /// <summary>T the saw blade's thickness, D the V saws' distance, W the profile's width (Cut Full Width), O the saw's vertical share.</summary>
        public double T, D, W, O;
        /// <summary>L, R: the special cut's left and right values (the job file's).</summary>
        public double L, R;

        public FormulaValues With(double l, double r)
        {
            var c = (FormulaValues)MemberwiseClone();
            c.L = l;
            c.R = r;
            return c;
        }
    }

    /// <summary>
    /// THE SPECIAL CUTS ("&lt;&gt;" - 6 November: "L105/L106 with D65 (the special-cut recipes) and V-cuts ... yes please"), as the old
    /// program made them (SpecialCutInfoGetter, FormulHelper, TransferProvider_FXCpu.WriteSpecialCuttingInfo / WriteSawGCodes_ByNo):
    /// the joints of a bar that are special, the recipe of each, its formulas. The recipes are the machine's (the library's machine
    /// settings), entered on the Jobs page or taken from the old program's tables.
    /// </summary>
    public static class SpecialCuts
    {
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        /// <summary>The PLC takes 10 special cuts a bar (R25650 + 5 x 0 - 9, the codes 1001 - 1010) and 30 steps a cut (R25700 + 10 x 0 - 29).</summary>
        public const int MaxPerBar = 10, MaxSteps = 30;

        // ---- the joints of a bar --------------------------------------------------------------------------

        private static double Num(string code)
        {
            double v;
            return double.TryParse(code ?? "", NumberStyles.Float, CI, out v) ? v : 0;
        }

        /// <summary>
        /// The joint at the LEFT end of piece <paramref name="i"/> when a special cut makes it (GetSpecialCutInfo_Left), else null: the piece's
        /// own special left end and the right end of the piece before it - special or its angle (0 at the bar's start).
        /// </summary>
        public static SpecialJoint LeftOf(KbnBar bar, int i)
        {
            var p = bar.Pieces[i];
            var prev = i > 0 ? bar.Pieces[i - 1] : null;
            var sp = p.Special;
            var pp = prev != null ? prev.Special : null;
            bool prevRight = pp != null && pp.RightSpecial;
            if (sp == null || !sp.LeftSpecial)
            {
                if (!prevRight) return null;
                return new SpecialJoint { RightCode = Num(pp.RightCode), RightParam = pp.RightParam, LeftCode = p.LeftAngle, Before = i - 1, After = i };
            }
            var j = new SpecialJoint { LeftCode = Num(sp.LeftCode), LeftParam = sp.LeftParam, Before = i - 1, After = i };
            if (prevRight) { j.RightCode = Num(pp.RightCode); j.RightParam = pp.RightParam; }
            else j.RightCode = prev != null ? prev.RightAngle : 0;
            return j;
        }

        /// <summary>The joint at the RIGHT end of piece <paramref name="i"/> (GetSpecialCutInfo_Right) - 0 for the left end after the bar's last piece.</summary>
        public static SpecialJoint RightOf(KbnBar bar, int i)
        {
            var p = bar.Pieces[i];
            var next = i + 1 < bar.Pieces.Count ? bar.Pieces[i + 1] : null;
            var sp = p.Special;
            var np = next != null ? next.Special : null;
            bool nextLeft = np != null && np.LeftSpecial;
            if (sp == null || !sp.RightSpecial)
            {
                if (!nextLeft) return null;
                return new SpecialJoint { RightCode = p.RightAngle, LeftCode = Num(np.LeftCode), LeftParam = np.LeftParam, Before = i, After = i + 1 };
            }
            var j = new SpecialJoint { RightCode = Num(sp.RightCode), RightParam = sp.RightParam, Before = i, After = next != null ? i + 1 : -1 };
            if (nextLeft) { j.LeftCode = Num(np.LeftCode); j.LeftParam = np.LeftParam; }
            else j.LeftCode = next != null ? next.LeftAngle : 0;
            return j;
        }

        /// <summary>
        /// The recipe of a joint (FillSpecialCuttingInfo): one with both its codes (an end with a value takes a recipe "with offset" there);
        /// else one of the left end only (right code 0) and one of the right end only (left code 0) MERGED - the right one's steps, a merge
        /// step (its Robot 1 the left recipe's X offset), the left one's steps; the right one's angle, positions and saw width. Null: none.
        /// </summary>
        public static SpecialCutRecipe Find(IList<SpecialCutRecipe> recipes, SpecialJoint j)
        {
            if (recipes == null || j == null) return null;
            Func<SpecialCutRecipe, bool> left = r => j.LeftParam == 0 || r.WithOffsetLeft;
            Func<SpecialCutRecipe, bool> right = r => j.RightParam == 0 || r.WithOffsetRight;
            var both = recipes.FirstOrDefault(r => r.LeftCode == j.LeftCode && r.RightCode == j.RightCode && left(r) && right(r));
            if (both != null) return both;
            var l = recipes.FirstOrDefault(r => r.LeftCode == j.LeftCode && r.RightCode == 0 && left(r));
            var rr = recipes.FirstOrDefault(r => r.LeftCode == 0 && r.RightCode == j.RightCode && right(r));
            if (l == null || rr == null) return null;
            var m = rr.Clone();
            m.MergedOf = rr.No.ToString(CI) + " + " + l.No.ToString(CI);
            m.Name = (rr.Name + " + " + l.Name).Trim(' ', '+');
            m.Steps.Add(new SpecialCutStep { Command = (int)SpecialCutCommand.Merge, Robot1 = l.RobotXOffset, VCutY = "0" });
            m.Steps.AddRange(l.Steps.Select(s => s.Clone()));
            return m;
        }

        // ---- the formulas ---------------------------------------------------------------------------------

        /// <summary>The letters a formula may have (the old program's FormulHelper.ShortCodes) and what each is.</summary>
        public const string Letters = "TDWORL";

        /// <summary>
        /// A recipe's formula: a number, or the letters T D W O L R with + - * / and brackets ("W/2+T", "-(L+O)") - rounded to 0.01 as
        /// the old program did. Empty: 0. False with the reason when it cannot be worked out.
        /// </summary>
        public static bool Eval(string formula, FormulaValues v, out double value, out string problem)
        {
            value = 0;
            problem = "";
            string f = (formula ?? "").Trim();
            if (f.Length == 0) return true;
            double plain;
            if (double.TryParse(f.Replace(',', '.'), NumberStyles.Float, CI, out plain)) { value = plain; return true; }
            try
            {
                var p = new Parser(f.ToUpperInvariant(), v ?? new FormulaValues());
                double r = p.Sum();
                if (!p.AtEnd) throw new FormatException("\"" + f.Substring(p.At) + "\" is not understood");
                if (double.IsNaN(r) || double.IsInfinity(r)) throw new FormatException("it divides by 0");
                value = Math.Round(r, 2, MidpointRounding.AwayFromZero);
                return true;
            }
            catch (FormatException ex)
            {
                problem = "\"" + f + "\": " + ex.Message;
                return false;
            }
        }

        /// <summary>Whether a formula can be worked out at all (the letters and the signs only) - the editor's check.</summary>
        public static bool Valid(string formula, out string problem)
        {
            double v;
            return Eval(formula, new FormulaValues { T = 4.2, D = 50, W = 70, O = 0.5, L = 10, R = 10 }, out v, out problem);
        }

        private sealed class Parser
        {
            private readonly string _s;
            private readonly FormulaValues _v;
            public int At;
            public Parser(string s, FormulaValues v) { _s = s; _v = v; }
            public bool AtEnd { get { Skip(); return At >= _s.Length; } }
            private void Skip() { while (At < _s.Length && char.IsWhiteSpace(_s[At])) At++; }
            private char Peek { get { Skip(); return At < _s.Length ? _s[At] : '\0'; } }

            public double Sum()
            {
                double r = Product();
                while (Peek == '+' || Peek == '-') { char o = _s[At++]; double b = Product(); r = o == '+' ? r + b : r - b; }
                return r;
            }

            private double Product()
            {
                double r = Unary();
                while (Peek == '*' || Peek == '/') { char o = _s[At++]; double b = Unary(); r = o == '*' ? r * b : r / b; }
                return r;
            }

            private double Unary()
            {
                if (Peek == '-') { At++; return -Unary(); }
                if (Peek == '+') { At++; return Unary(); }
                return Atom();
            }

            private double Atom()
            {
                char c = Peek;
                if (c == '(')
                {
                    At++;
                    double r = Sum();
                    if (Peek != ')') throw new FormatException("a bracket is not closed");
                    At++;
                    return r;
                }
                if (char.IsDigit(c) || c == '.' || c == ',')
                {
                    int start = At;
                    while (At < _s.Length && (char.IsDigit(_s[At]) || _s[At] == '.' || _s[At] == ',')) At++;
                    double n;
                    if (!double.TryParse(_s.Substring(start, At - start).Replace(',', '.'), NumberStyles.Float, CI, out n))
                        throw new FormatException("\"" + _s.Substring(start, At - start) + "\" is not a number");
                    return n;
                }
                At++;
                switch (c)
                {
                    case 'T': return _v.T;
                    case 'D': return _v.D;
                    case 'W': return _v.W;
                    case 'O': return _v.O;
                    case 'L': return _v.L;
                    case 'R': return _v.R;
                    case '\0': throw new FormatException("it ends too soon");
                    default: throw new FormatException("'" + c + "' is not one of T D W O L R, a number or + - * / ( )");
                }
            }
        }

        // ---- kept in the library (a machine setting) ------------------------------------------------------

        /// <summary>The recipes as the library keeps them: a "recipe;" line each, its "step;" lines under it.</summary>
        public static string Format(IEnumerable<SpecialCutRecipe> recipes)
        {
            var sb = new StringBuilder();
            Func<string, string> clean = x => (x ?? "").Replace(";", ",").Replace("\r", " ").Replace("\n", " ").Trim();
            foreach (var r in recipes ?? new SpecialCutRecipe[0])
            {
                sb.Append(string.Join(";", new[]
                {
                    "recipe", r.No.ToString(CI), clean(r.Name), r.LeftCode.ToString("R", CI), r.RightCode.ToString("R", CI), r.StartingAngle.ToString("R", CI),
                    clean(r.HoldingPosition), clean(r.RobotXOffset), clean(r.SawDustWidth), r.WithOffsetLeft ? "1" : "0", r.WithOffsetRight ? "1" : "0"
                })).Append("\n");
                foreach (var s in r.Steps)
                    sb.Append(string.Join(";", new[]
                    {
                        "step", s.Command.ToString(CI), s.LeftClamp ? "1" : "0", s.RightClamp ? "1" : "0", clean(s.Robot1), clean(s.Robot2),
                        s.Angle.ToString("R", CI), clean(s.VCutY)
                    })).Append("\n");
            }
            return sb.ToString();
        }

        public static List<SpecialCutRecipe> Parse(string text)
        {
            var list = new List<SpecialCutRecipe>();
            SpecialCutRecipe cur = null;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var t = raw.TrimEnd('\r').Split(';');
                Func<int, string> f = i => i < t.Length ? t[i] : "";
                if (t[0] == "recipe")
                {
                    int no;
                    int.TryParse(f(1), NumberStyles.Integer, CI, out no);
                    cur = new SpecialCutRecipe
                    {
                        No = no, Name = f(2), LeftCode = Num(f(3)), RightCode = Num(f(4)), StartingAngle = Num(f(5)), HoldingPosition = f(6),
                        RobotXOffset = f(7), SawDustWidth = f(8), WithOffsetLeft = f(9) == "1", WithOffsetRight = f(10) == "1"
                    };
                    list.Add(cur);
                }
                else if (t[0] == "step" && cur != null)
                {
                    int cmd;
                    int.TryParse(f(1), NumberStyles.Integer, CI, out cmd);
                    cur.Steps.Add(new SpecialCutStep { Command = cmd, LeftClamp = f(2) == "1", RightClamp = f(3) == "1", Robot1 = f(4), Robot2 = f(5), Angle = Num(f(6)), VCutY = f(7) });
                }
            }
            return list;
        }

        // ---- the old program's tables ---------------------------------------------------------------------

        /// <summary>
        /// The old program's recipes, from its database's tables exported as CSV (pgAdmin: sawreceipts and sawreceiptdetails, with their
        /// headers): the recipes by their columns' names (No, JobName, LeftCode, RightCode, StartingAngle, HoldingPosition, RobotXOffset,
        /// SawDustWidth, WithOffsetLeft, WithOffsetRight, Id), their steps by SawReceiptId in the file's order.
        /// </summary>
        public static List<SpecialCutRecipe> ImportOld(string recipesCsv, string stepsCsv, List<string> problems)
        {
            var list = new List<SpecialCutRecipe>();
            var byId = new Dictionary<string, SpecialCutRecipe>(StringComparer.OrdinalIgnoreCase);
            var rows = Csv(recipesCsv);
            if (rows.Count == 0) { problems.Add("the recipes' file has no lines"); return list; }
            foreach (var r in rows)
            {
                Func<string, string> f = n => { string v; return r.TryGetValue(n.ToLowerInvariant(), out v) ? v : ""; };
                int no;
                if (!int.TryParse(f("No"), NumberStyles.Integer, CI, out no)) { problems.Add("a recipe without its number (No) - left out"); continue; }
                var rec = new SpecialCutRecipe
                {
                    No = no, Name = f("JobName"), LeftCode = Num(f("LeftCode")), RightCode = Num(f("RightCode")), StartingAngle = Num(f("StartingAngle").Replace(',', '.')),
                    HoldingPosition = f("HoldingPosition"), RobotXOffset = f("RobotXOffset"), SawDustWidth = f("SawDustWidth"),
                    WithOffsetLeft = True(f("WithOffsetLeft")), WithOffsetRight = True(f("WithOffsetRight"))
                };
                list.Add(rec);
                string id = f("Id");
                if (id.Length > 0) byId[id] = rec;
            }
            if (!string.IsNullOrEmpty(stepsCsv))
            {
                foreach (var r in Csv(stepsCsv))
                {
                    Func<string, string> f = n => { string v; return r.TryGetValue(n.ToLowerInvariant(), out v) ? v : ""; };
                    SpecialCutRecipe rec;
                    if (!byId.TryGetValue(f("SawReceiptId"), out rec)) { problems.Add("a step of a recipe not in the recipes' file - left out"); continue; }
                    int cmd;
                    int.TryParse(f("CommandNo"), NumberStyles.Integer, CI, out cmd);
                    rec.Steps.Add(new SpecialCutStep
                    {
                        Command = cmd, LeftClamp = True(f("LeftClamp")), RightClamp = True(f("RightClamp")), Robot1 = f("Robot1"), Robot2 = f("Robot2"),
                        Angle = Num(f("Angle").Replace(',', '.')), VCutY = f("VCutting_Y")
                    });
                }
            }
            return list.OrderBy(x => x.No).ToList();
        }

        private static bool True(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            return s == "1" || s == "true" || s == "t" || s == "yes";
        }

        /// <summary>A CSV file's lines by their header's names (lower case): ',' or ';' or a tab between the fields, "quoted" ones too.</summary>
        internal static List<Dictionary<string, string>> Csv(string path)
        {
            var result = new List<Dictionary<string, string>>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;
            var lines = File.ReadAllLines(path, Encoding.UTF8).Where(l => l.Trim().Length > 0).ToList();
            if (lines.Count == 0) return result;
            string head = lines[0].TrimStart('﻿');
            char sep = head.Count(c => c == ';') > head.Count(c => c == ',') ? ';' : head.Contains('\t') && !head.Contains(',') ? '\t' : ',';
            var names = Fields(head, sep).Select(n => n.Trim().ToLowerInvariant()).ToList();
            foreach (var l in lines.Skip(1))
            {
                var v = Fields(l, sep);
                var d = new Dictionary<string, string>();
                for (int i = 0; i < names.Count; i++) d[names[i]] = i < v.Count ? v[i] : "";
                result.Add(d);
            }
            return result;
        }

        private static List<string> Fields(string line, char sep)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (q)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (c == '"') q = false;
                    else sb.Append(c);
                }
                else if (c == '"') q = true;
                else if (c == sep) { list.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            list.Add(sb.ToString());
            return list;
        }
    }
}
