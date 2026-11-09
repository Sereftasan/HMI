using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.Profiles.Geometry;
using Kaban.Profiles.Jobs;
using Kaban.Profiles.Machining;

namespace Kaban.Machines.FA2010.Jobs
{
    /// <summary>The cutting settings of the machine (the library's machine_setting) - the old program's saw settings and machine parameters.</summary>
    internal sealed class JobSettings
    {
        internal const string SawThicknessCode = "job_saw_thickness_mm", VerticalShareCode = "job_saw_vertical_share_mm",
                              SquareFromCode = "job_square_leftover_from_mm", LongestCode = "job_longest_piece_mm",
                              VSawsCode = "job_v_saws_distance_mm", RecipesCode = "job_special_cut_recipes";

        /// <summary>null: not entered - nothing can be laid out.</summary>
        public double? SawThickness;
        public double VerticalShare, SquareFrom = 300, Longest;
        /// <summary>The V saws' distance (mm) - the special cut recipes' D.</summary>
        public double VSawsDistance;
        /// <summary>The special cut recipes ("&lt;&gt;" cuts - the old program's saw receipts).</summary>
        public List<SpecialCutRecipe> Recipes = new List<SpecialCutRecipe>();

        public static JobSettings Of(long instance)
        {
            var s = new JobSettings();
            if (instance <= 0) return s;
            s.SawThickness = Num(Library.Setting(instance, SawThicknessCode, ""));
            s.VerticalShare = Num(Library.Setting(instance, VerticalShareCode, "")) ?? 0;
            s.SquareFrom = Num(Library.Setting(instance, SquareFromCode, "")) ?? 300;
            s.Longest = Num(Library.Setting(instance, LongestCode, "")) ?? 0;
            s.VSawsDistance = Num(Library.Setting(instance, VSawsCode, "")) ?? 0;
            s.Recipes = SpecialCuts.Parse(Library.Setting(instance, RecipesCode, ""));
            return s;
        }

        /// <summary>The special cut recipes kept (the Jobs page's editor).</summary>
        public static void SaveRecipes(long instance, IEnumerable<SpecialCutRecipe> recipes)
        {
            Library.SetSetting(instance, RecipesCode, SpecialCuts.Format(recipes));
        }

        public void Save(long instance)
        {
            var ci = CultureInfo.InvariantCulture;
            Library.SetSetting(instance, SawThicknessCode, SawThickness.HasValue ? SawThickness.Value.ToString("0.###", ci) : "");
            Library.SetSetting(instance, VerticalShareCode, VerticalShare.ToString("0.###", ci));
            Library.SetSetting(instance, SquareFromCode, SquareFrom.ToString("0.###", ci));
            Library.SetSetting(instance, LongestCode, Longest.ToString("0.###", ci));
            Library.SetSetting(instance, VSawsCode, VSawsDistance.ToString("0.###", ci));
        }

        internal static double? Num(string s)
        {
            double v;
            return double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : (double?)null;
        }
    }

    /// <summary>
    /// One milling operation of the bar: an operation of an accessory of the profile - its PLC code (1 - 100), and what goes to its milling
    /// parameters (PC_FrezeIslemParametreleri, R26000 + 10 x (code - 1)): the tool, its angle, the operation's length, where it enters the
    /// profile, its speed.
    /// </summary>
    internal sealed class MillingOp
    {
        public int Index;
        public string Accessory = "", Macro = "", MacroText = "", Side = "TOP", Status = "";
        public int Seq;
        /// <summary>At the far end of its piece, the operation mirrored (its placement's Mirror - a hinge's other end).</summary>
        public bool Mirrored;
        public Tool Tool;
        public Face Face;
        public int ToolNo;
        /// <summary>Its approach point in the section (Profile Approach Y / Z), the length the tool goes along it, its speed (m/min).</summary>
        public double Y, Z, Length, Speed;
        /// <summary>The milling steps the PLC asks for (L101 / L103) - PlcSteps.</summary>
        public List<PlcStep> Steps = new List<PlcStep>();
        public string Problem = "", Warning = "";
        public bool Confirmed { get { return Status == "CONFIRMED" || Status == "OVERRIDDEN"; } }
        public string Label { get { return Accessory + " op" + Seq + " " + Macro + (Mirrored ? " (mirrored)" : ""); } }
    }

    /// <summary>A step of a special cut as the PLC gets it (its formulas worked out for the joint).</summary>
    internal sealed class SpecialStepValues
    {
        public int Command;
        public double Robot1, Robot2, Angle, VCutY;
        public bool LeftClamp, RightClamp;
    }

    /// <summary>
    /// One special cut of the bar: its number (1 - 10, the PLC's code 1000 + it), the joint and its recipe, what goes to its parameters
    /// (PC_KesimIslemParametreleri, R25650 + 5 x (number - 1)) and the steps the saw asks for (L105 with the code in D65 -> R25700).
    /// </summary>
    internal sealed class SpecialCutPlan
    {
        public int Number;
        public SpecialJoint Joint;
        public SpecialCutRecipe Recipe;
        /// <summary>The cut's angle, the V saw's Y approach (the profile's V Cut Y Position), the exit robot's holding position, the robot's X offset.</summary>
        public double StartingAngle, VCutY, Holding, XOffset;
        public readonly List<SpecialStepValues> Steps = new List<SpecialStepValues>();
        /// <summary>Its steps' words for R25700 (JobSender - 30 steps of 10 words).</summary>
        public short[] Words;
        public int Code { get { return 1000 + Number; } }
        public string Label { get { return "special cut " + Number + ": recipe " + Recipe.Title; } }
    }

    /// <summary>A step of the job list the PLC gets: its ID (D5000), position (D6000), code (D7000).</summary>
    internal sealed class PlanStep
    {
        public int No, Code;
        public double Position;
        public string What = "";
        public int Piece = -1;
        public LayoutStep Step;
        public MillingOp Op;
        public SpecialCutPlan Special;
    }

    /// <summary>Everything that goes to the PLC for one bar, and why it cannot (yet).</summary>
    internal sealed class BarPlan
    {
        public KbnBar Bar;
        public Library.ProfileInfo Profile;
        public BarLayoutResult Layout;
        public readonly List<PlanStep> Steps = new List<PlanStep>();
        public readonly List<MillingOp> Milling = new List<MillingOp>();
        /// <summary>Its special cuts, by number.</summary>
        public readonly List<SpecialCutPlan> Special = new List<SpecialCutPlan>();
        /// <summary>The profile's mullion notching steps (R25700, before its parameters): X1 X2 Z1 Z2 Y1 Y2 Step Speed each (mm).</summary>
        public List<double[]> Notch = new List<double[]>();
        /// <summary>The profile's parameters (R25600): member -> value as entered (mm).</summary>
        public Dictionary<string, string> ProfileValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>What keeps the bar from being sent.</summary>
        public readonly List<string> Problems = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public int FirstNo, LastNo;
        /// <summary>A milling operation's position nobody has confirmed: only a trial sends it.</summary>
        public bool NeedsTrial { get { return Milling.Any(m => !m.Confirmed); } }
        public bool Ok { get { return Problems.Count == 0; } }
    }

    /// <summary>
    /// THE JOB'S BARS FOR THE PLC (6 November: "It finishes with '999999' and it includes the whole profile"): each bar of the job file laid
    /// out (BarLayout - the old program's cut positions), its accessories turned into the milling operations of the profile in the library,
    /// numbered as the PLC takes them - the job list (ID, position, code; 999999 after the last position), the milling operations'
    /// parameters, the profile's parameters.
    ///
    /// An accessory of the file ("Rec;code;position") is an accessory of the profile's group in the library: every operation of it (a
    /// hinge's slot and its holes) is a step at that position, each operation its own PLC code - the same accessory again on the bar takes
    /// the same codes (the old program's receipt numbers, the same way). Its tool, angle and entry point come from the profile's PLACEMENT
    /// of it: a placement nobody has confirmed goes to the machine only in a TRIAL (the profile library's rule - the side program's too).
    /// </summary>
    internal sealed class JobPlanner
    {
        private readonly long _instance;
        private readonly JobSettings _settings;
        private readonly List<Tool> _tools;
        private readonly List<Face> _faces;
        private readonly List<Feed> _feeds;
        private readonly Dictionary<string, Library.ProfileInfo> _byCode = new Dictionary<string, Library.ProfileInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<long, List<Library.PlacementInfo>> _placements = new Dictionary<long, List<Library.PlacementInfo>>();
        private readonly Dictionary<long, Section> _sections = new Dictionary<long, Section>();
        private readonly Dictionary<long, List<string>> _resolveProblems = new Dictionary<long, List<string>>();
        private readonly Dictionary<long, HashSet<string>> _accessories = new Dictionary<long, HashSet<string>>();
        private readonly Dictionary<long, Dictionary<string, Library.MachineValue>> _machine = new Dictionary<long, Dictionary<string, Library.MachineValue>>();
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>The PLC takes 500 steps (D5000 - D5999, two devices a step) - the last place is the end mark's.</summary>
        internal const int MaxSteps = 499;
        internal const int MaxMilling = 100;
        internal const int EndMark = 999999;
        internal const string MouldMember = "Ear Allowance / Mould Difference", WidthMember = "Cut Full Width", VCutYMember = "V Cut Y Position";
        /// <summary>The job list's code of a V cut (the old program's JobConstants.VCuttingCode).</summary>
        internal const int VCutCode = 3000;

        public JobPlanner(long instance, JobSettings settings)
        {
            _instance = instance;
            _settings = settings ?? new JobSettings();
            _tools = instance > 0 ? Library.Tools(instance) : new List<Tool>();
            _faces = instance > 0 ? Library.Faces(instance) : new List<Face>();
            _feeds = instance > 0 ? Library.Feeds(instance) : new List<Feed>();
            // active profiles first: an inactive one of the same code (a copy put aside) is found only when no active one has it
            foreach (var p in Library.Profiles(true).OrderBy(p => p.Active ? 0 : 1))
                if (!_byCode.ContainsKey(p.Code.Trim())) _byCode[p.Code.Trim()] = p;
        }

        public Library.ProfileInfo ProfileOf(string code)
        {
            Library.ProfileInfo p;
            return _byCode.TryGetValue((code ?? "").Trim(), out p) ? p : null;
        }

        /// <summary>The plans of every bar of the job, numbered one after another (each bar's IDs follow the one before it).</summary>
        public List<BarPlan> Plan(KbnJob job)
        {
            var list = new List<BarPlan>();
            int no = 1;
            foreach (var bar in job.Bars)
            {
                var p = Plan(bar, no);
                list.Add(p);
                if (p.Steps.Count > 0) no = p.LastNo + 1;
            }
            return list;
        }

        public BarPlan Plan(KbnBar bar, int firstNo)
        {
            var plan = new BarPlan { Bar = bar, FirstNo = firstNo, LastNo = firstNo - 1 };
            plan.Profile = ProfileOf(bar.Code);
            if (plan.Profile == null)
            {
                plan.Problems.Add(string.Format("the profile {0} is not in the profile library", bar.Code));
                return plan;
            }
            if (!plan.Profile.Active) plan.Warnings.Add(string.Format("the profile {0} is put aside (inactive) in the library", bar.Code));
            if (bar.Pieces.Count == 0) plan.Problems.Add("the bar has no pieces");

            var machine = MachineOf(plan.Profile.Id);
            foreach (var kv in machine) plan.ProfileValues[kv.Key] = kv.Value.Value;
            if (machine.Count == 0) plan.Warnings.Add("the profile has no machine settings (its parameters go as 0)");
            Library.MachineValue notch;
            if (machine.TryGetValue(MullionNotch.Code, out notch) && notch != null) plan.Notch = MullionNotch.Parse(notch.Value);
            if (plan.Notch.Count > MullionNotch.MaxSteps)
                plan.Warnings.Add(string.Format("the profile has {0} mullion notching steps - the PLC takes the first {1}", plan.Notch.Count, MullionNotch.MaxSteps));

            if (!_settings.SawThickness.HasValue || _settings.SawThickness.Value <= 0)
                plan.Problems.Add("the saw blade's thickness is not entered (the cutting settings above)");
            double height = plan.Profile.EffectiveHeight;
            if (height <= 0) plan.Problems.Add(string.Format("the profile {0} has no height in the library", bar.Code));
            string mouldText;
            double mould = machine.ContainsKey(MouldMember) && (mouldText = machine[MouldMember].Value) != null ? JobSettings.Num(mouldText) ?? 0 : 0;
            var cs = new CutSettings
            {
                SawThickness = _settings.SawThickness ?? 0, SawVerticalShare = _settings.VerticalShare, ProfileHeight = height,
                MouldDifference = mould, MinimumLeftover = _settings.SquareFrom, MaximumCutLength = _settings.Longest,
                Recipes = _settings.Recipes, VSawsDistance = _settings.VSawsDistance, ProfileWidth = MachineNum(machine, WidthMember)
            };
            plan.Layout = BarLayout.Of(bar, cs);
            plan.Problems.AddRange(plan.Layout.Problems);
            plan.Warnings.AddRange(plan.Layout.Notes);
            foreach (var p in bar.Pieces.Where(x => x.Other.Count > 0))
                plan.Problems.Add(string.Format("piece {0}: {1} - not taken by the HMI yet", bar.Pieces.IndexOf(p) + 1,
                    string.Join(", ", p.Other.Select(o => o.Split(';')[0]).Distinct())));
            // the special cuts: a number each different joint (the old program's dynamic receipt numbers), their formulas worked out
            foreach (var j in plan.Layout.Joints)
            {
                if (plan.Special.Any(x => x.Joint.Key == j.Key)) { j.Number = plan.Special.First(x => x.Joint.Key == j.Key).Number; continue; }
                j.Number = plan.Special.Count + 1;
                plan.Special.Add(Special(j, cs, machine, plan.Problems));
            }
            if (plan.Special.Count > SpecialCuts.MaxPerBar)
                plan.Problems.Add(string.Format("{0} different special cuts - the PLC takes {1} a bar", plan.Special.Count, SpecialCuts.MaxPerBar));

            // the accessories: the group's, each operation a milling operation of its own
            var group = AccessoriesOf(plan.Profile);
            var placements = PlacementsOf(plan.Profile);
            var byKey = new Dictionary<string, MillingOp>(StringComparer.OrdinalIgnoreCase);
            int no = firstNo;
            SpecialJoint lastJoint = null;
            foreach (var s in plan.Layout.Ordered)
            {
                if (s.IsCut)
                {
                    if (s.Joint != null)
                    {
                        // a special cut: one step for its joint (the right end's and the left end's cuts are one recipe - the old program's)
                        if (s.Joint == lastJoint) continue;
                        lastJoint = s.Joint;
                        var sp = plan.Special.First(x => x.Number == s.Joint.Number);
                        plan.Steps.Add(new PlanStep { No = no++, Position = s.Position, Code = sp.Code, Piece = s.Piece, Step = s, Special = sp,
                                                      What = sp.Label + " (" + s.Joint.Describe() + ")" });
                        continue;
                    }
                    lastJoint = null;
                    plan.Steps.Add(new PlanStep { No = no++, Position = s.Position, Code = s.CutCode, What = BarLayout.Describe(s), Piece = s.Piece, Step = s });
                    continue;
                }
                if (s.Kind == StepKind.VCut)
                {
                    plan.Steps.Add(new PlanStep { No = no++, Position = s.Position, Code = VCutCode, What = BarLayout.Describe(s), Piece = s.Piece, Step = s });
                    continue;
                }
                string code = s.Rec.Code;
                if (!group.Contains(code))
                {
                    plan.Problems.Add(string.Format("piece {0}: the accessory {1} is not one of the group {2}'s", s.Piece + 1, code, plan.Profile.Group));
                    continue;
                }
                var ops = placements.Where(x => string.Equals(x.Accessory, code, StringComparison.OrdinalIgnoreCase) && x.Status != "REJECTED")
                                    .OrderBy(x => x.Seq).ToList();
                if (ops.Count == 0)
                {
                    plan.Problems.Add(string.Format("piece {0}: the accessory {1} has no placement on the profile {2} (the Profiles page places it)", s.Piece + 1, code, plan.Profile.Code));
                    continue;
                }
                var piece = s.Piece >= 0 && s.Piece < bar.Pieces.Count ? bar.Pieces[s.Piece] : null;
                foreach (var pl in ops)
                {
                    // an operation that is mirrored at the far end of its piece (a hinge's other end) is mirrored past the piece's middle
                    bool mirrored = pl.Mirror && piece != null && s.Rec.Position > piece.CutLength / 2;
                    string key = pl.Accessory + "|" + pl.Seq + "|" + (mirrored ? "m" : "");
                    MillingOp op;
                    if (!byKey.TryGetValue(key, out op))
                    {
                        op = Milling(pl, plan.Profile, mirrored);
                        op.Index = plan.Milling.Count + 1;
                        byKey[key] = op;
                        plan.Milling.Add(op);
                        if (op.Problem.Length > 0) plan.Problems.Add(op.Label + ": " + op.Problem);
                        if (op.Warning.Length > 0) plan.Warnings.Add(op.Label + ": " + op.Warning);
                    }
                    plan.Steps.Add(new PlanStep
                    {
                        No = no++, Position = s.Position, Code = op.Index, Piece = s.Piece, Step = s, Op = op,
                        What = op.Label + (op.Tool != null ? " - " + op.Tool.Position + " at " + op.Face.Angle.ToString("0.#", CI) + "°" : "")
                    });
                }
            }
            List<string> rp;
            if (_resolveProblems.TryGetValue(plan.Profile.Id, out rp)) plan.Warnings.AddRange(rp);
            if (plan.Milling.Count > MaxMilling) plan.Problems.Add(string.Format("{0} milling operations - the PLC takes {1}", plan.Milling.Count, MaxMilling));
            if (plan.Steps.Count > MaxSteps) plan.Problems.Add(string.Format("{0} steps - the PLC's job list takes {1}", plan.Steps.Count, MaxSteps));
            if (plan.Steps.Any(x => x.Position < 0)) plan.Problems.Add("a step before the bar's start");
            int unconfirmed = plan.Milling.Count(m => !m.Confirmed);
            if (unconfirmed > 0)
                plan.Warnings.Add(string.Format("{0} milling operation(s) are not confirmed on the profile - they go to the machine only as a trial", unconfirmed));
            plan.LastNo = no - 1;
            return plan;
        }

        private static double MachineNum(Dictionary<string, Library.MachineValue> machine, string member)
        {
            Library.MachineValue v;
            return machine.TryGetValue(member, out v) && v != null ? JobSettings.Num(v.Value) ?? 0 : 0;
        }

        /// <summary>A joint's special cut: its parameters and steps with the recipe's formulas worked out (T, D, W, O and the joint's L, R).</summary>
        private static SpecialCutPlan Special(SpecialJoint j, CutSettings cs, Dictionary<string, Library.MachineValue> machine, List<string> problems)
        {
            var sp = new SpecialCutPlan { Number = j.Number, Joint = j, Recipe = j.Recipe, StartingAngle = j.Recipe.StartingAngle, VCutY = MachineNum(machine, VCutYMember) };
            var v = cs.Values(j);
            Func<string, string, double> eval = (what, f) =>
            {
                double r;
                string why;
                if (!SpecialCuts.Eval(f, v, out r, out why)) problems.Add(sp.Label + ", " + what + ": " + why);
                return r;
            };
            sp.Holding = eval("the holding position", j.Recipe.HoldingPosition);
            sp.XOffset = eval("the robot's X offset", j.Recipe.RobotXOffset);
            int n = 0;
            foreach (var st in j.Recipe.Steps)
            {
                n++;
                sp.Steps.Add(new SpecialStepValues
                {
                    Command = st.Command, LeftClamp = st.LeftClamp, RightClamp = st.RightClamp, Angle = st.Angle,
                    Robot1 = eval("step " + n + " robot 1", st.Robot1), Robot2 = eval("step " + n + " robot 2", st.Robot2), VCutY = eval("step " + n + " V cut Y", st.VCutY)
                });
            }
            if (sp.Steps.Count == 0) problems.Add(sp.Label + ": the recipe has no steps");
            if (sp.Steps.Count > SpecialCuts.MaxSteps) problems.Add(string.Format("{0}: {1} steps - the PLC takes {2}", sp.Label, sp.Steps.Count, SpecialCuts.MaxSteps));
            return sp;
        }

        /// <summary>The tool, its angle and the rest of a placement's milling operation.</summary>
        private MillingOp Milling(Library.PlacementInfo pl, Library.ProfileInfo profile, bool mirrored)
        {
            var op = new MillingOp
            {
                Accessory = pl.Accessory, Seq = pl.Seq, Macro = pl.Macro, Side = pl.Side, Status = pl.Status, Y = pl.Y, Z = pl.Z, Mirrored = mirrored
            };
            var so = pl.ToOperation();
            op.MacroText = so.Macro.Describe();
            Section section = SectionOf(profile);
            var settings = SettingsOf(profile);
            double w = section != null ? section.Width : profile.EffectiveWidth, h = section != null ? section.Height : profile.EffectiveHeight;
            var dep = SideProgram.Depths(so, section, w, h, settings.Breakthrough);
            if (!dep.Ok) { op.Problem = dep.Problem; return op; }
            var choice = SideProgram.Choose(so.Macro, (pl.Side ?? "TOP").ToUpperInvariant(), _tools, _faces, dep.Bottom - dep.Surface);
            if (!choice.Ok) { op.Problem = choice.Problem; return op; }
            op.Tool = choice.Tool;
            op.Face = choice.Face;
            op.Warning = choice.Warning ?? "";
            op.ToolNo = ToolNo(choice.Tool);
            if (op.ToolNo <= 0) op.Problem = "the tool " + choice.Tool.Position + " is not one of U1-T1, U1-T2, U2-T1, U2-T2 (the PLC's tool 1 - 4)";
            double d = choice.Tool.Diameter ?? 0;
            switch ((pl.MacroType ?? "").ToUpperInvariant())
            {
                case "SLOT":
                case "POCKET":
                    op.Length = Math.Max(0, (so.Macro.P("length_mm") ?? 0) - d);
                    break;
                default:
                    op.Length = 0;
                    break;
            }
            // its steps, and where they start: its approach point
            var steps = PlcSteps.For(so, mirrored, section, w, h, choice.Tool, choice.Face, _feeds, settings);
            if (!steps.Ok) { if (op.Problem.Length == 0) op.Problem = steps.Problem; return op; }
            op.Steps = steps.Steps;
            op.Y = steps.ApproachY;
            op.Z = steps.ApproachZ;
            // the speed in m/min (the steps', the old program's): the cutting feed of the profile's material
            string material = string.IsNullOrEmpty(settings.Material) ? "PVC" : settings.Material;
            var feed = _feeds.FirstOrDefault(f => string.Equals(f.MillingClass, material + "_OUTER_WALL", StringComparison.OrdinalIgnoreCase) &&
                                                  string.Equals(f.ToolType, choice.Tool.Type, StringComparison.OrdinalIgnoreCase));
            op.Speed = feed != null ? feed.Cut / 1000 : 0;
            return op;
        }

        /// <summary>The PLC's tool number (1 - 4): unit 1's tools 1, 2, unit 2's 3, 4 ("U2-T1" = 3).</summary>
        internal static int ToolNo(Tool t)
        {
            if (t == null) return 0;
            var m = System.Text.RegularExpressions.Regex.Match(t.Position ?? "", @"^\s*U(\d+)\s*-\s*T(\d+)\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return 0;
            int u = int.Parse(m.Groups[1].Value, CI), n = int.Parse(m.Groups[2].Value, CI);
            if (u < 1 || u > 2 || n < 1 || n > 2) return 0;
            return (u - 1) * 2 + n;
        }

        // ---- the library, once a profile ----------------------------------------------------------------

        private Dictionary<string, Library.MachineValue> MachineOf(long profile)
        {
            Dictionary<string, Library.MachineValue> v;
            if (!_machine.TryGetValue(profile, out v)) _machine[profile] = v = _instance > 0 ? Library.MachineSettings(profile, _instance) : new Dictionary<string, Library.MachineValue>();
            return v;
        }

        private HashSet<string> AccessoriesOf(Library.ProfileInfo p)
        {
            HashSet<string> set;
            if (!_accessories.TryGetValue(p.GroupId, out set))
            {
                set = new HashSet<string>(Library.GroupAccessories(p.GroupId)
                    .Where(r => Convert.ToInt64(r["is_enabled"] ?? 1) != 0).Select(r => Convert.ToString(r["code"])), StringComparer.OrdinalIgnoreCase);
                _accessories[p.GroupId] = set;
            }
            return set;
        }

        /// <summary>The profile's placements - worked out first when it has none yet (what the Profiles page does when it shows the profile).</summary>
        private List<Library.PlacementInfo> PlacementsOf(Library.ProfileInfo p)
        {
            List<Library.PlacementInfo> list;
            if (_placements.TryGetValue(p.Id, out list)) return list;
            list = Library.Placements(p.Id);
            if (list.Count == 0)
            {
                try
                {
                    var r = Library.Resolve(p.Id);
                    if (r != null && r.Problems.Count > 0) _resolveProblems[p.Id] = r.Problems.Select(x => p.Code + ": " + x).ToList();
                }
                catch (Exception ex) { _resolveProblems[p.Id] = new List<string> { p.Code + ": " + ex.Message }; }
                list = Library.Placements(p.Id);
            }
            _placements[p.Id] = list;
            return list;
        }

        private readonly Dictionary<long, ProgramSettings> _programSettings = new Dictionary<long, ProgramSettings>();

        /// <summary>The side program's settings for the profile (peck depth, approach feed, breakthrough, its material).</summary>
        private ProgramSettings SettingsOf(Library.ProfileInfo p)
        {
            ProgramSettings s;
            if (!_programSettings.TryGetValue(p.Id, out s))
                _programSettings[p.Id] = s = _instance > 0 ? Library.ProgramSettings(_instance, p) : new ProgramSettings { Material = string.IsNullOrEmpty(p.Material) ? "PVC" : p.Material };
            return s;
        }

        private Section SectionOf(Library.ProfileInfo p)
        {
            Section s;
            if (_sections.TryGetValue(p.Id, out s)) return s;
            try { s = Library.SectionOf(p); } catch { s = null; }
            _sections[p.Id] = s;
            return s;
        }
    }
}
