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

// THE SPECIAL CUTS AND THE V CUTS ON THE JOBS PAGE (6 November: "L105/L106 with D65 (the <> special-cut recipes) and V-cuts aren't done yet
// ... Should I do them next? yes please"): the recipes entered in their editor (and the old program's taken from CSV), a job of special
// ends and a V cut planned - the joints' codes 1001, 1002 and the V cut's 3000 in the job list, the special cuts' parameters at R25650 +
// 5 each - and the saw's request: L105 with the code in D65 -> the recipe's steps in R25700, L106 on; L105 off -> L106 off.
class JS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        object inst = o is Type ? null : o;
        while (t != null) {
            var f = t.GetField(n, Any); if (f != null) return f.GetValue(inst);
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(inst, null);
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
    static readonly List<string> said = new List<string>();
    static int Dw(short[] w, int i) { return (w[i] & 0xFFFF) | (w[i + 1] << 16); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "jspec_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        Db.ConnectionTarget = Path.Combine(dir, "Kaban.db");
        Db.SqliteChoice = Db.SqliteLibrary.Native;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        PlcLabelTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));
        StructuredTypes.FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StructuredTypes.csv");
        StructuredTypes.Reload();
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        Ask.Hook = (title, prompt, initial) => { said.Add(prompt); return initial == "yes" ? "yes" : initial; };
        var asm = typeof(Kaban.Machines.FA2010.Screens.JobsScreen).Assembly;
        typeof(Kaban.Machines.FA2010.Screens.JobsScreen).GetField("StatePath", Any).SetValue(null, Path.Combine(dir, "Jobs.cfg"));
        var milling = asm.GetType("Kaban.Machines.FA2010.Jobs.JobMilling");
        milling.GetField("StatePath", Any).SetValue(null, Path.Combine(dir, "JobMilling.cfg"));
        Call(milling, "Reset");
        var settingsType = asm.GetType("Kaban.Machines.FA2010.Jobs.JobSettings");

        string why;
        Library.Open(() => true, out why);
        var pst = asm.GetType("Kaban.Machines.FA2010.Screens.ProfilesScreen");
        long inst = Library.EnsureMachine("FA2010", "FA2010", (KeyValuePair<string, int>[])Get(pst, "ToolPlaces"), (Kaban.Profiles.Machining.Face[])Call(pst, "MachineFaces"));
        var frame = Library.Profiles().First(p => p.Code == "ML8-F-4501");
        Library.SetMachineSetting(frame.Id, inst, "Cut Full Width", "70");
        Library.SetMachineSetting(frame.Id, inst, "V Cut Y Position", "12.5");

        var sim = new Kaban.Drivers.Simulation.SimulationDriver(1);
        PlcManager.Instance.RegisterDriver(1, sim);
        PlcManager.Instance.RegisterStation(1, "FA2010", SecurityLevel.Operator);
        sim.Connect();

        var form = new Form { ClientSize = new Size(1500, 900) };
        var page = new Kaban.Machines.FA2010.Screens.JobsScreen();
        form.Controls.Add(page);
        form.Show();
        Pump(300);
        Call(page, "EnsureLibrary");
        ((NumericUpDown)Get(page, "_numSaw")).Value = 4.2M;
        ((NumericUpDown)Get(page, "_numVSaws")).Value = 50M;
        Pump(50);
        Check("the V saws' distance kept with the cutting settings", Library.Setting(inst, "job_v_saws_distance_mm", "") == "50", Library.Setting(inst, "job_v_saws_distance_mm", ""));

        // ---- the recipes' editor ----
        var dlgType = asm.GetType("Kaban.Machines.FA2010.Screens.SpecialCutsDialog");
        var d = (Form)Activator.CreateInstance(dlgType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
                                               new object[] { new List<SpecialCutRecipe>(), true }, null);
        d.Show();
        Pump(100);
        var rg = (DataGridView)Get(d, "_recipes");
        var sg = (DataGridView)Get(d, "_steps");
        Call(d, "AddRecipe");
        var rec = (List<SpecialCutRecipe>)Get(d, "Recipes");
        Check("a recipe added: number 1, a line in the table", rec.Count == 1 && rec[0].No == 1 && rg.Rows.Count == 1);
        Action<string, object> setR = (col, val) => rg.Rows[0].Cells[col].Value = val;
        setR("name", "Kasa <> 90");
        setR("left", "3");
        setR("right", "90");
        setR("angle", "90");
        setR("holding", "W/2");
        setR("offset", "-T");
        setR("dust", "T*2");
        Check("  typed in: its codes, angle and formulas", rec[0].LeftCode == 3 && rec[0].RightCode == 90 && rec[0].HoldingPosition == "W/2" && rec[0].SawDustWidth == "T*2" &&
              rec[0].Name == "Kasa <> 90");
        Call(d, "AddStep");
        Call(d, "AddStep");
        sg.Rows[1].Cells["command"].Value = sg.Columns["command"] is DataGridViewComboBoxColumn ? ((DataGridViewComboBoxColumn)sg.Columns["command"]).Items[3] : null;
        sg.Rows[1].Cells["sangle"].Value = "45";
        Check("two steps: a clamp (both clamps), a normal cut at 45", rec[0].Steps.Count == 2 && rec[0].Steps[0].Command == 1 && rec[0].Steps[0].LeftClamp && rec[0].Steps[0].RightClamp &&
              rec[0].Steps[1].Command == 4 && rec[0].Steps[1].Angle == 45, string.Join(",", rec[0].Steps.Select(s => s.Command + ":" + s.Angle)));
        Check("  a normal cut's robots and V saw greyed (its command does not use them)", sg.Rows[1].Cells["robot1"].ReadOnly && sg.Rows[1].Cells["vcut"].ReadOnly && !sg.Rows[1].Cells["sangle"].ReadOnly);
        setR("dust", "T*2+X");
        Check("  a formula with X: not saved - said why", !(bool)Call(d, "Check") && ((Label)Get(d, "_problem")).Text.Contains("'X'"), ((Label)Get(d, "_problem")).Text);
        setR("dust", "T*2");
        Check("  put right: it can be saved", (bool)Call(d, "Check"));
        // the old program's tables (CSV) - a second recipe of the bar's right end (5, any) taken with them
        string rc = Path.Combine(dir, "sawreceipts.csv"), dc = Path.Combine(dir, "sawreceiptdetails.csv");
        File.WriteAllText(rc, "Id,No,JobName,HoldingPosition,RobotXOffset,SawDustWidth,LeftCode,RightCode,StartingAngle,WithOffsetLeft,WithOffsetRight\n" +
                              "a1,1,Kasa <> 90,W/2,-T,T*2,3,90,90,false,false\nb2,2,Son <> 5,,10,6,0,5,45,false,false\n");
        File.WriteAllText(dc, "Id,CommandNo,LeftClamp,RightClamp,Robot1,Robot2,Angle,VCutting_Y,SawReceiptId\n" +
                              "x1,1,true,true,,,0,,a1\nx2,4,false,false,,,45,,a1\nx3,5,false,false,,,0,W/2,b2\nx4,3,false,false,L+100,W-10,0,,b2\n");
        said.Clear();
        Check("the old program's recipes imported (asked before the one here is replaced)", (bool)Call(d, "ImportFrom", rc, dc) && said.Any(s => s.Contains("Replace")) &&
              ((List<SpecialCutRecipe>)Get(d, "Recipes")).Count == 2 && rg.Rows.Count == 2, string.Join(" / ", said));
        rec = (List<SpecialCutRecipe>)Get(d, "Recipes");
        Call(settingsType, "SaveRecipes", inst, rec);
        d.Close();
        Check("  kept in the library (the machine's settings)", Library.Setting(inst, "job_special_cut_recipes", "").Contains("recipe;2;Son <> 5"));

        // ---- a job of special ends and a V cut ----
        string job = Path.Combine(dir, "Special.kbn");
        File.WriteAllLines(job, new[] {
            "Bar;1;ML8-F-4501;Frame;1;White;60000;0;",
            "Profile;10000;10000;0900;0900;1;1;O1;1;C;A;;1;;0;B1;;;;;;",
            "VCut;2500;0;",
            "Profile;10000;10000;0900;0900;1;2;O1;1;C;A;;1;;0;B2;;;;;;",
            "SpecialCut;3;0;0;0",
            "Profile;10000;10000;0900;0900;1;3;O1;1;C;A;;1;;0;B3;;;;;;",
            "SpecialCut;0;5;0;0",
        });
        Call(page, "OpenJob", job);
        Pump(100);
        var plan = ((IList)Get(page, "Plans"))[0];
        var problems = ((IList)Get(plan, "Problems")).Cast<string>().ToList();
        var steps = ((IList)Get(plan, "Steps")).Cast<object>().ToList();
        var codes = steps.Select(s => (int)Get(s, "Code")).ToList();
        Check("the bar planned: no problems", problems.Count == 0, string.Join(" / ", problems));
        Check("  the job list: the left cut 1900, the V cut 3000, special cut 1 (pieces 1 and 2) 1001, piece 2's right cut 1900, special cut 2 (the bar's end) 1002 - and the leftover's cut",
              codes.Take(5).SequenceEqual(new[] { 1900, 3000, 1001, 1900, 1002 }), string.Join(",", codes));
        Check("  the V cut at 2.1 + 250 mm, the special joint at 2.1 + 4.2 + 1000 (half its saw dust width T*2)",
              Math.Abs((double)Get(steps[1], "Position") - 252.1) < 1e-6 && Math.Abs((double)Get(steps[2], "Position") - 1006.3) < 1e-6,
              Get(steps[1], "Position") + " " + Get(steps[2], "Position"));
        var special = (IList)Get(plan, "Special");
        Check("  its special cuts: 1 recipe 1, 2 recipe 2", special.Count == 2 && ((SpecialCutRecipe)Get(special[0], "Recipe")).No == 1 && ((SpecialCutRecipe)Get(special[1], "Recipe")).No == 2);
        string notes = ((TextBox)Get(page, "_notes")).Text;
        Check("  the notes list them with their steps", notes.Contains("special cut 1: recipe 1") && notes.Contains("Normal cut") && notes.Contains("V cut"), notes.Length > 400 ? notes.Substring(0, 400) : notes);

        said.Clear();
        Check("sent (the question names the special cuts)", (bool)Call(page, "SendSelected") && said.Any(s => s.Contains("2 special cut(s)")), string.Join(" / ", said));
        short[] w;
        sim.TryReadBlock("R25650", 10, out w);
        Check("  R25650: special cut 1 - its angle x10, the V saw's Y approach 12.5 x100, holding W/2 = 35 x100, X offset -T = -4.2 x100",
              w[0] == 900 && w[1] == 1250 && w[2] == 3500 && w[3] == -420, string.Join(",", w.Take(5)));
        Check("  R25655: special cut 2 - 45 degrees, X offset 10", w[5] == 450 && w[6] == 1250 && w[8] == 1000, string.Join(",", w.Skip(5)));
        sim.TryReadBlock("D7000", 10, out w);
        Check("  D7000: the codes as planned", Enumerable.Range(0, 5).All(i => Dw(w, 2 * i) == codes[i]), string.Join(",", Enumerable.Range(0, 5).Select(i => Dw(w, 2 * i))));

        // ---- the saw asks: L105 with the code in D65 ----
        sim.Write("D65", 1001);
        sim.Write("L105", 1);
        Call(milling, "Poll");
        sim.TryReadBlock("R25700", 30, out w);
        Check("the saw asks for 1001 (L105, D65): recipe 1's steps in R25700 - a clamp (both: bits 0 and 1), a normal cut at 45 x10 - and L106 on",
              w[0] == 1 && w[5] == 3 && w[10] == 4 && w[13] == 450 && w[20] == 0 && sim.Read("L106") == 1, string.Join(",", w.Take(21)) + " L106=" + sim.Read("L106"));
        sim.Write("L105", 0);
        Call(milling, "Poll");
        Check("  L105 off: L106 off", sim.Read("L106") == 0);
        sim.Write("D65", 1002);
        sim.Write("L105", 1);
        Call(milling, "Poll");
        sim.TryReadBlock("R25700", 30, out w);
        Check("1002: recipe 2's - the V saw at W/2 = 35 x100, the robots L+100 = 100 and W-10 = 60 x100; recipe 1's second step cleared",
              w[0] == 5 && w[4] == 3500 && w[10] == 3 && w[11] == 10000 && w[12] == 6000 && w[13] == 0 && w[20] == 0 && sim.Read("L106") == 1, string.Join(",", w.Take(21)));
        sim.Write("L105", 0);
        Call(milling, "Poll");
        sim.Write("D65", 1);
        sim.Write("L105", 1);
        sim.Write("R25700", 0);
        Call(milling, "Poll");
        string log = string.Join("\n", Directory.GetFiles(AuditLogger.LogDirectory).Select(File.ReadAllText));
        Check("the saw asking for 1 (a milling operation's number): not answered - L106 stays off, the log says why",
              sim.Read("L106") == 0 && sim.Read("R25700") == 0 && log.Contains("the saw asked") && log.Contains("not answered"));
        sim.Write("L105", 0);
        Call(milling, "Poll");
        Call(milling, "Reset");
        Call(milling, "Start");
        Check("  kept over a restart: both special cuts read back", (int)Get(milling, "Count") == 2, Get(milling, "Count").ToString());

        // ---- no recipe ----
        Call(settingsType, "SaveRecipes", inst, new List<SpecialCutRecipe>());
        Call(page, "Replan");
        plan = ((IList)Get(page, "Plans"))[0];
        problems = ((IList)Get(plan, "Problems")).Cast<string>().ToList();
        Check("no recipes: the bar cannot go - each joint says it has none", !(bool)Get(plan, "Ok") && problems.Count(p => p.Contains("no special cut recipe")) == 2, string.Join(" / ", problems));

        form.Close();
        Call(milling, "Reset");
        Console.WriteLine(fail == 0 ? "\nJOBS SPECIAL ALL PASS" : "\nJOBS SPECIAL " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
