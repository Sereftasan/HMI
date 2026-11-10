using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.Profiles.Jobs;
using Kaban.UI.Profiles;

// THE SPECIAL CUT TYPES, THE SAMPLES' CODES, THE STARTING ANGLE (10 November, the answers: "'left/right special-cut codes' are the special
// cut codes which is taken from the cutting job ... 'Right 90 Left Arrow' means the previous pieces right cut ... is 90 degrees normal cut and
// the following cut pieces left cut is an arrow special cut"; "'starting angle' is the first '4' coded cut operation angle value at the
// recipe"; the device list's 42 "means 4.2mm"): the types (the old program's specialcuttings - a job file's code and its name) beside the
// recipes, a code shown with its name, the samples' codes from their names (the job files' code asked once), their saw dust width 4.2, a
// recipe's starting angle its first normal cut's.
class SCT {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        while (t != null) {
            var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o is Type ? null : o, null);
            t = t.BaseType;
        }
        throw new MissingMemberException(n);
    }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length);
        return mi.Invoke(o is Type ? null : o, a);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static readonly List<string> asked = new List<string>();
    static readonly Dictionary<string, string> answers = new Dictionary<string, string>();

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "sctypes_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        Db.ConnectionTarget = Path.Combine(dir, "Kaban.db");
        Db.SqliteChoice = Db.SqliteLibrary.Native;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        PlcLabelTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));
        StructuredTypes.FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StructuredTypes.csv");
        StructuredTypes.Reload();
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        Ask.Hook = (title, prompt, initial) =>
        {
            asked.Add(prompt);
            foreach (var kv in answers) if (prompt.Contains(kv.Key)) return kv.Value;
            return initial == "yes" ? "yes" : initial;
        };

        Console.WriteLine("--- the model ---");
        var types = SpecialCuts.ParseTypes("type;3;Back Open Y\ntype;1;Arrow\nrecipe;1;x;0;0;0;;;;0;0\n");
        Check("the types kept as type;code;name lines (in code order)", types.Count == 2 && types[0].No == 1 && types[0].Name == "Arrow" &&
              SpecialCuts.FormatTypes(types) == "type;1;Arrow\ntype;3;Back Open Y\n", SpecialCuts.FormatTypes(types));
        Check("  a code in words: 1 Arrow, an angle 90, any 0", SpecialCuts.CodeText(1, types) == "1 Arrow" && SpecialCuts.CodeText(90, types) == "90" && SpecialCuts.CodeText(0, types) == "0");
        var rr = new SpecialCutRecipe { No = 1, StartingAngle = 30, LeftCode = 0, RightCode = 5 };
        rr.Steps.Add(new SpecialCutStep { Command = 1 });
        Check("a recipe without a normal cut: its starting angle as entered (30)", rr.EffectiveStartingAngle == 30 && !rr.FirstCutAngle.HasValue);
        rr.Steps.Add(new SpecialCutStep { Command = 4, Angle = 45 });
        rr.Steps.Add(new SpecialCutStep { Command = 4, Angle = 135 });
        Check("  with normal cuts: its first one's (45)", rr.EffectiveStartingAngle == 45);
        var ll = new SpecialCutRecipe { No = 2, LeftCode = 1, RightCode = 0 };
        ll.Steps.Add(new SpecialCutStep { Command = 4, Angle = 90 });
        var merged = SpecialCuts.Find(new List<SpecialCutRecipe> { rr, ll }, new SpecialJoint { LeftCode = 1, RightCode = 5 });
        Check("  a merged recipe's: the right one's first (its steps go first)", merged != null && merged.MergedOf == "1 + 2" && merged.EffectiveStartingAngle == 45);
        var noRecipe = BarLayout.Of(KbnFile.Parse(new[] { "Bar;1;P1;Test;1;White;60000;0;", "Profile;10000;10000;0900;0900;1;1;O1;1;C;A;;1;;0;B1;;;;;;", "SpecialCut;1;0;0;0" }, "t").Bars[0],
                                    new CutSettings { SawThickness = 4, ProfileHeight = 70, MinimumLeftover = 300, Types = types });
        Check("  a joint without a recipe says its codes with their names (left end 1 Arrow)", noRecipe.Problems.Any(p => p.Contains("left end 1 Arrow")), string.Join(" / ", noRecipe.Problems));
        var samples = SpecialCuts.Parse(File.ReadAllText("/home/claude/work/Kaban.Machines.FA2010/SpecialCutSamples.csv"));
        var s5 = samples.First(x => x.Name == "Right 90 Left Arrow");
        Check("the samples' codes from their names: Right 90 Left Arrow - right 90, left the type Arrow (by its name)", s5.RightCode == 90 && s5.LeftType == "Arrow" && s5.LeftCode == 0 && s5.RightType == "");
        Check("  First End Arrow: left Arrow at the bar's start (right 0); Right Arrow Left Arrow: both", samples[0].LeftType == "Arrow" && samples[0].RightCode == 0 && samples[0].RightType == "" &&
              samples.First(x => x.Name == "Right Arrow Left Arrow").RightType == "Arrow");
        Check("  the saw dust width 4.2 (the device list's 42) - not on First End Arrow (none there)", samples.Skip(1).All(x => x.SawDustWidth == "4.2") && samples[0].SawDustWidth == "",
              string.Join(",", samples.Select(x => x.SawDustWidth)));
        Check("  their starting angles their first normal cuts' (45, 90, 90, 90, 90, 45)",
              samples.Select(x => x.EffectiveStartingAngle).SequenceEqual(new double[] { 45, 90, 90, 90, 90, 45 }) && samples.All(x => x.StartingAngle == x.EffectiveStartingAngle),
              string.Join(",", samples.Select(x => x.EffectiveStartingAngle)));

        Console.WriteLine("--- the Profiles page ---");
        var asm = typeof(Kaban.Machines.FA2010.Screens.JobsScreen).Assembly;
        string why;
        Library.Open(() => true, out why);
        var pst = asm.GetType("Kaban.Machines.FA2010.Screens.ProfilesScreen");
        long inst = Library.EnsureMachine("FA2010", "FA2010", (KeyValuePair<string, int>[])Get(pst, "ToolPlaces"), (Kaban.Profiles.Machining.Face[])Call(pst, "MachineFaces"));
        var dlgType = asm.GetType("Kaban.Machines.FA2010.Screens.SpecialCutsEditor");
        dlgType.GetField("SamplesPath", Any).SetValue(null, "/home/claude/work/Kaban.Machines.FA2010/SpecialCutSamples.csv");
        var pform = new Form { ClientSize = new Size(1500, 900) };
        var prof = new Kaban.Machines.FA2010.Screens.ProfilesScreen();
        pform.Controls.Add(prof);
        pform.Show();
        Pump(200);
        Call(prof, "EnsureLibrary");
        Pump(100);
        var d = (Control)Get(prof, "_specialCuts");
        ((TabControl)Get(prof, "_accTabs")).SelectedIndex = 1;
        Pump(100);
        var tg = (DataGridView)Get(d, "_types");
        var rg = (DataGridView)Get(d, "_recipes");
        var sg = (DataGridView)Get(d, "_steps");
        Check("the special cut types beside the recipes: Code, Special cut", tg.Columns.Count == 2 && tg.Columns["no"].HeaderText == "Code" && tg.Visible && tg.Parent.Dock == DockStyle.Right);

        answers["\"Arrow\""] = "1";            // the job files' code of Arrow; the Y ones not known yet
        asked.Clear();
        int added = (int)Call(d, "AddSamples");
        var rec = (List<SpecialCutRecipe>)Get(d, "Recipes");
        var ty = (List<SpecialCutType>)Get(d, "Types");
        Check("the samples added: Arrow's code asked once (1), the type added; Back Open Y / Front Open Y's asked, not given",
              added == 6 && asked.Count(a => a.Contains("\"Arrow\"")) == 1 && asked.Any(a => a.Contains("\"Back Open Y\"")) && ty.Count == 1 && ty[0].No == 1 && ty[0].Name == "Arrow" &&
              tg.Rows.Count == 1, string.Join(" / ", asked));
        Check("  their codes: Right 90 Left Arrow left 1 right 90, First End Arrow 1 / 0, Back Open Y 0 / 0 (said so)",
              rec.First(r => r.Name == "Right 90 Left Arrow").LeftCode == 1 && rec.First(r => r.Name == "Right 90 Left Arrow").RightCode == 90 &&
              rec.First(r => r.Name == "First End Arrow").LeftCode == 1 && rec.First(r => r.Name == "Back Open Y").LeftCode == 0 &&
              rec.All(r => r.LeftType == "" && r.RightType == "") && asked.Last().Contains("Without a code (0): Back Open Y, Front Open Y"), asked.Last());
        int i5 = rec.FindIndex(r => r.Name == "Right 90 Left Arrow");
        Check("  a code shown with its type's name (1 Arrow), an angle as it is (90)", Convert.ToString(rg.Rows[i5].Cells["left"].FormattedValue) == "1 Arrow" &&
              Convert.ToString(rg.Rows[i5].Cells["right"].FormattedValue) == "90" && Convert.ToString(rg.Rows[i5].Cells["left"].Value) == "1",
              rg.Rows[i5].Cells["left"].FormattedValue + " / " + rg.Rows[i5].Cells["right"].FormattedValue);
        Check("  the starting angle its first normal cut's, greyed (not typed in)", Convert.ToString(rg.Rows[i5].Cells["angle"].Value) == "90" && rg.Rows[i5].Cells["angle"].ReadOnly);
        // its first normal cut changed: the angle follows
        rg.CurrentCell = rg.Rows[i5].Cells["name"];
        Pump(50);
        int k4 = rec[i5].Steps.FindIndex(st => st.Command == 4);
        sg.Rows[k4].Cells["sangle"].Value = "88";
        Pump(50);
        Check("its first normal cut at 88: the starting angle 88", Convert.ToString(rg.Rows[i5].Cells["angle"].Value) == "88" && rec[i5].EffectiveStartingAngle == 88 && rec[i5].StartingAngle == 88,
              Convert.ToString(rg.Rows[i5].Cells["angle"].Value));
        // a recipe without a normal cut: typed in
        Call(d, "AddRecipe");
        int iNew = rec.Count - 1;
        Check("  a new recipe (no steps): its starting angle typed in", !rg.Rows[iNew].Cells["angle"].ReadOnly);
        rg.Rows[iNew].Cells["angle"].Value = "60";
        Check("    kept (60)", rec[iNew].StartingAngle == 60 && rec[iNew].EffectiveStartingAngle == 60);

        // a type renamed and added
        tg.Rows[0].Cells["name"].Value = "Arrow (Ok)";
        Check("a type renamed: the codes show its new name", Convert.ToString(rg.Rows[i5].Cells["left"].FormattedValue) == "1 Arrow (Ok)" && ty[0].Name == "Arrow (Ok)");
        Call(d, "AddType");
        ty = (List<SpecialCutType>)Get(d, "Types");
        Check("+ Type: code 2", ty.Count == 2 && ty[1].No == 2 && tg.Rows.Count == 2);
        tg.Rows[1].Cells["no"].Value = "1";
        Check("  code 1 twice: not saved, said why", !(bool)Call(d, "Check") && ((Label)Get(d, "_problem")).Text.Contains("special cut code 1 twice"), ((Label)Get(d, "_problem")).Text);
        tg.Rows[1].Cells["no"].Value = "3";
        tg.Rows[1].Cells["name"].Value = "Back Open Y";
        Check("  put right (3 Back Open Y)", (bool)Call(d, "Check") && ty[1].No == 3 && ty[1].Name == "Back Open Y");
        Check("Save: the types kept with the recipes (the machine's settings)", (bool)Call(d, "Save") &&
              Library.Setting(inst, "job_special_cut_types", "") == "type;1;Arrow (Ok)\ntype;3;Back Open Y\n" &&
              Library.Setting(inst, "job_special_cut_recipes", "").Contains("recipe;5;Right 90 Left Arrow;1;90;88;"),
              Library.Setting(inst, "job_special_cut_types", ""));
        tg.Rows[0].Cells["name"].Value = "changed";
        Call(d, "Revert");
        ty = (List<SpecialCutType>)Get(d, "Types");
        Check("  Undo the changes: the kept types again", ty.Count == 2 && ty[0].Name == "Arrow (Ok)" && tg.Rows.Count == 2);
        // the samples again with the types there: nothing asked
        ty[0].Name = "Arrow";                   // found by its name
        Call(d, "LoadRecipes", new List<SpecialCutRecipe>(), ty);
        asked.Clear();
        Call(d, "AddSamples");
        rec = (List<SpecialCutRecipe>)Get(d, "Recipes");
        Check("the samples added again with the types here: their codes from them - only Front Open Y's asked (no type of it yet)",
              asked.Count(a => a.Contains("What is its code")) == 1 && asked.Any(a => a.Contains("\"Front Open Y\"")) && rec.First(r => r.Name == "Back Open Y").LeftCode == 3 && rec.First(r => r.Name == "Back Open Y").RightCode == 3 &&
              rec.First(r => r.Name == "Right Arrow Left 90").RightCode == 1 && rec.First(r => r.Name == "Right Arrow Left 90").LeftCode == 90 &&
              rec.First(r => r.Name == "Front Open Y").LeftCode == 0, string.Join(" / ", asked));
        // the old program's specialcuttings
        string rc = Path.Combine(dir, "sawreceipts.csv"), dc = Path.Combine(dir, "sawreceiptdetails.csv"), tc = Path.Combine(dir, "specialcuttings.csv");
        File.WriteAllText(rc, "Id,No,JobName,HoldingPosition,RobotXOffset,SawDustWidth,LeftCode,RightCode,StartingAngle,WithOffsetLeft,WithOffsetRight\n" +
                              "a1,1,Kasa <> 90,W/2,-T,T*2,7,90,90,false,false\n");
        File.WriteAllText(dc, "Id,CommandNo,LeftClamp,RightClamp,Robot1,Robot2,Angle,VCutting_Y,SawReceiptId\nx1,1,true,true,,,0,,a1\nx2,4,false,false,,,45,,a1\n");
        File.WriteAllText(tc, "\"Id\",\"No\",\"JobName\"\n\"g1\",\"7\",\"Ok\"\n\"g2\",\"8\",\"Kırlangıç\"\n");
        asked.Clear();
        Check("the old program's tables with its specialcuttings: the types replaced too (asked)", (bool)Call(d, "ImportFrom", rc, dc, tc) &&
              asked.Any(a => a.Contains("2 special cut type(s) instead of the 2 here")), string.Join(" / ", asked));
        ty = (List<SpecialCutType>)Get(d, "Types");
        rec = (List<SpecialCutRecipe>)Get(d, "Recipes");
        Check("  7 Ok, 8 Kırlangıç; the recipe's left code 7 shown as 7 Ok, its starting angle its first normal cut's (45, not the table's 90)",
              ty.Count == 2 && ty[0].No == 7 && ty[0].Name == "Ok" && ty[1].Name == "Kırlangıç" && Convert.ToString(rg.Rows[0].Cells["left"].FormattedValue) == "7 Ok" &&
              Convert.ToString(rg.Rows[0].Cells["angle"].Value) == "45" && rec[0].EffectiveStartingAngle == 45,
              string.Join(",", ty.Select(t => t.No + " " + t.Name)) + " / " + rg.Rows[0].Cells["left"].FormattedValue + " / " + rg.Rows[0].Cells["angle"].Value);

        SecurityManager.Login("op", SecurityLevel.Operator);
        Pump(100);
        Call(prof, "ApplyAccess");
        Check("below Maintenance: the types shown, not changed", tg.ReadOnly);
        pform.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nSPECIAL CUT TYPES ALL PASS" : "\nSPECIAL CUT TYPES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
