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

// THE JOB LIST (6 November: "They are send to the machine when the 'Send to the machine' button clicked for the job on the job list
// ... you can create it now using our old program"; "It finishes with '999999' and it includes the whole profile"): the library's
// sample job opened, its bars laid out and checked against the library, a bar sent into the PLC (a simulator) - the profile's
// parameters at R25600, the milling operations' at R26000, the job list at D5000 / D6000 / D7000 with 999999 after the last position
// - a bar with unconfirmed milling positions only as a trial, the settings and the trial for the Maintenance level.
class JP {
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
    static IList Plans(object page) { return (IList)Get(page, "Plans"); }
    static IList Steps(object plan) { return (IList)Get(plan, "Steps"); }
    static IList Problems(object plan) { return (IList)Get(plan, "Problems"); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "jobs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        Db.ConnectionTarget = Path.Combine(dir, "Kaban.db");
        Db.SqliteChoice = Db.SqliteLibrary.Native;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        PlcLabelTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));
        StructuredTypes.FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StructuredTypes.csv");
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        Ask.Hook = (title, prompt, initial) => { said.Add(prompt); return initial == "yes" ? "yes" : initial; };
        var asm = typeof(Kaban.Machines.FA2010.Screens.JobsScreen).Assembly;
        var screenType = typeof(Kaban.Machines.FA2010.Screens.JobsScreen);
        screenType.GetField("StatePath", Any).SetValue(null, Path.Combine(dir, "Jobs.cfg"));

        // the example library; its FA2010 tools given diameters, two profiles given the points their accessories are placed from
        string why;
        Library.Open(() => true, out why);
        var pst = asm.GetType("Kaban.Machines.FA2010.Screens.ProfilesScreen");
        long inst = Library.EnsureMachine("FA2010", "FA2010", (KeyValuePair<string, int>[])Get(pst, "ToolPlaces"),
                                          (Kaban.Profiles.Machining.Face[])Call(pst, "MachineFaces"));
        Library.Exec("UPDATE mounted_tool SET diameter_mm = CASE position WHEN 'U1-T1' THEN 5 WHEN 'U1-T2' THEN 10 WHEN 'U2-T1' THEN 6 ELSE 8 END WHERE instance_id=@p0", inst);
        var frame = Library.Profiles().First(p => p.Code == "ML8-F-4501");
        var sash = Library.Profiles().First(p => p.Code == "ML8-S-4510");
        foreach (var p in new[] { frame, sash }) {
            Library.SetReference(p.Id, "OUTSIDE_SURFACE", 0, 30, "MANUAL", 1, "setter");
            Library.SetReference(p.Id, "HARDWARE_AXIS", 22, p.EffectiveHeight - 10, "MANUAL", 1, "setter");
            Library.SetReference(p.Id, "INSIDE_SURFACE", p.EffectiveWidth, 30, "MANUAL", 1, "setter");
        }
        var rf = Library.Resolve(frame.Id);
        var fp = Library.Placements(frame.Id);
        Check("the frame's placements worked out (drainage, striker)", fp.Any(x => x.Accessory == "GEN-DRN-5X30") && fp.Any(x => x.Accessory == "NX-STR-STD"),
              string.Join(", ", fp.Select(x => x.Accessory + " " + x.Status)) + " / " + string.Join(" / ", rf.Problems));
        foreach (var x in fp) Library.ConfirmPlacement(x.Id, "setter", "test");
        Library.SetMachineSetting(frame.Id, inst, "Conveyor Speed", "80");
        Library.SetMachineSetting(frame.Id, inst, "Robot Hold Y Position", "12.5");
        Library.SetMachineSetting(frame.Id, inst, "Tool Cooling Active", "1");

        // the PLC: a simulator
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(1);
        PlcManager.Instance.RegisterDriver(1, sim);
        PlcManager.Instance.RegisterStation(1, "FA2010", SecurityLevel.Operator);
        sim.Connect();

        var form = new Form { ClientSize = new Size(1500, 900) };
        var page = new Kaban.Machines.FA2010.Screens.JobsScreen();
        form.Controls.Add(page);
        form.Show();
        Pump(300);
        string job = Path.Combine(dir, "JobSample_Library.kbn");
        File.Copy("/home/claude/work/docs/fa2010/JobSample_Library.kbn", job);

        // ---- no saw blade yet: nothing can go ----
        Call(page, "OpenJob", job);
        Pump(100);
        var plans = Plans(page);
        Check("the sample job opened: its 7 bars planned", plans.Count == 7 && ((DataGridView)Get(page, "_bars")).Rows.Count == 7, plans.Count.ToString());
        Check("  with no saw blade entered, every bar says so", plans.Cast<object>().All(p => Problems(p).Cast<string>().Any(s => s.Contains("saw blade"))));

        // ---- the cutting settings: the Maintenance level's, kept in the library ----
        ((NumericUpDown)Get(page, "_numSaw")).Value = 4.2M;
        Pump(50);
        Check("the saw blade 4.2 kept in the library", Library.Setting(inst, "job_saw_thickness_mm", "") == "4.2", Library.Setting(inst, "job_saw_thickness_mm", ""));
        plans = Plans(page);
        Func<int, object> plan = i => plans[i];
        Check("planned again: the frame bar (confirmed placements) can go - no trial", (bool)Get(plan(0), "Ok") && !(bool)Get(plan(0), "NeedsTrial"),
              string.Join(" / ", Problems(plan(0)).Cast<string>()));
        Check("  the sash bar: its placements unconfirmed - a trial only", (bool)Get(plan(1), "Ok") && (bool)Get(plan(1), "NeedsTrial"),
              string.Join(" / ", Problems(plan(1)).Cast<string>()));
        Check("  the mullions (no accessories): both bars ready", (bool)Get(plan(2), "Ok") && (bool)Get(plan(3), "Ok") && !(bool)Get(plan(2), "NeedsTrial"));
        Check("  the Elegant frame: its drainage has no placement (no points on the profile) - it cannot go, and says why",
              !(bool)Get(plan(4), "Ok") && Problems(plan(4)).Cast<string>().Any(s => s.Contains("GEN-DRN-6X25") && s.Contains("placement")),
              string.Join(" / ", Problems(plan(4)).Cast<string>()));
        var grid = (DataGridView)Get(page, "_bars");
        Check("  the table's states: ready, trial only, problems", Convert.ToString(grid.Rows[0].Cells["state"].Value) == "ready" &&
              Convert.ToString(grid.Rows[1].Cells["state"].Value).StartsWith("trial") && Convert.ToString(grid.Rows[4].Cells["state"].Value).Contains("problem"),
              string.Join(" | ", grid.Rows.Cast<DataGridViewRow>().Select(r => Convert.ToString(r.Cells["state"].Value))));

        // ---- the frame bar's job list ----
        var steps = Steps(plan(0));
        var first = steps[0];
        Check("the frame bar's first step: ID 1, the left cut 45 at 0 (code 1450)", (int)Get(first, "No") == 1 && (double)Get(first, "Position") == 0 && (int)Get(first, "Code") == 1450);
        var codes = steps.Cast<object>().Select(s => (int)Get(s, "Code")).ToList();
        var ops = (IList)Get(plan(0), "Milling");
        Check("  the drainage and striker operations numbered 1, 2 ... (the same accessory again: the same number)",
              ops.Count >= 2 && codes.Where(c => c < 1000).Distinct().OrderBy(c => c).SequenceEqual(Enumerable.Range(1, ops.Count)),
              string.Join(",", codes));
        Check("  three drainage slots on the bottom piece (one op each), the striker's twice", codes.Count(c => c == 1) == 3, string.Join(",", codes));
        Check("  IDs one after another; the sash bar's follow the frame's", steps.Cast<object>().Select(s => (int)Get(s, "No")).SequenceEqual(Enumerable.Range(1, steps.Count)) &&
              (int)Get(plan(1), "FirstNo") == steps.Count + 1);
        Check("  positions in order along the bar", steps.Cast<object>().Select(s => (double)Get(s, "Position")).SequenceEqual(steps.Cast<object>().Select(s => (double)Get(s, "Position")).OrderBy(v => v)));
        Call(page, "Select", 0);
        var sg = (DataGridView)Get(page, "_steps");
        Check("  the page shows them, 999999 last", sg.Rows.Count == steps.Count + 1 && Convert.ToString(sg.Rows[sg.Rows.Count - 1].Cells["position"].Value) == "999999");

        // ---- sent ----
        said.Clear();
        bool sent = (bool)Call(page, "SendSelected");
        Check("the frame bar sent (the question asked)", sent && said.Any(s => s.Contains("Send bar 1") && s.Contains("step")), string.Join(" / ", said));
        short[] w;
        sim.TryReadBlock("D6000", 2 * (steps.Count + 2), out w);
        Check("  D6000: the positions x100, 999999 after the last", Dw(w, 0) == 0 && Dw(w, 2 * steps.Count) == 999999 &&
              Dw(w, 2) == Convert.ToInt32((double)Get(steps[1], "Position") * 100) && Dw(w, 2 * steps.Count + 2) == 0, Dw(w, 2 * steps.Count).ToString());
        sim.TryReadBlock("D7000", 2 * steps.Count, out w);
        Check("  D7000: the codes (a double word each, the code in its low word)", Enumerable.Range(0, steps.Count).All(i => Dw(w, 2 * i) == codes[i]));
        sim.TryReadBlock("D5000", 2 * steps.Count, out w);
        Check("  D5000: the IDs", Enumerable.Range(0, steps.Count).All(i => Dw(w, 2 * i) == i + 1));
        sim.TryReadBlock("R25600", 50, out w);
        Check("  R25600: the profile's parameters - tool cooling (bit 0), conveyor speed 80, robot hold Y 12.5 x100",
              (w[0] & 1) == 1 && w[1] == 80 && w[4] == 1250, w[0] + " " + w[1] + " " + w[4]);
        var op1 = ops[0];
        sim.TryReadBlock("R26000", 10 * ops.Count, out w);
        Check("  R26000: milling operation 1 - its tool (1 - 4), angle x100, Y / Z x100, speed (the feed, as the old program)",
              w[0] == (int)Get(op1, "ToolNo") && w[0] >= 1 && w[0] <= 4 && w[1] == (short)Math.Round(((Kaban.Profiles.Machining.Face)Get(op1, "Face")).Angle * 100) &&
              w[3] == (short)Math.Round((double)Get(op1, "Y") * 100) && w[4] == (short)Math.Round((double)Get(op1, "Z") * 100) && w[5] == (short)Math.Round((double)Get(op1, "Speed")),
              string.Join(",", w.Take(10)));
        var rights = steps.Cast<object>().Where(x => (int)Get(x, "Code") == 2350).Select(x => (double)Get(x, "Position")).ToList();
        sim.TryReadBlock("D106", 2, out w);
        Check("  D106: the bar's length x10000 (as the old program)", Dw(w, 0) == 65000000, Dw(w, 0).ToString());
        sim.TryReadBlock("D95", 4, out w);
        Check("  D95 / D97: the first and the last right cut x10000 + 1 mm", Dw(w, 0) == (int)(rights[0] * 10000 + 10000) && Dw(w, 2) == (int)(rights.Last() * 10000 + 10000),
              Dw(w, 0) + " " + Dw(w, 2));
        Check("  the bar's state: sent", Convert.ToString(grid.Rows[0].Cells["state"].Value).Contains("sent"));
        var log = string.Join("\n", Directory.GetFiles(AuditLogger.LogDirectory).Select(File.ReadAllText));
        Check("  the audit log has it", log.Contains("[JOB] Jobs: bar 1 of JobSample_Library (ML8-F-4501) sent"), log.Length > 300 ? log.Substring(log.Length - 300) : log);

        // a shorter bar after it: the rest of the list cleared
        Call(page, "Select", 2);
        said.Clear();
        Check("the mullion bar sent after it", (bool)Call(page, "SendSelected"));
        var ms = Steps(plan(2));
        sim.TryReadBlock("D6000", 2 * (steps.Count + 1), out w);
        Check("  its 999999 after its own last step, the frame's longer list cleared behind it", Dw(w, 2 * ms.Count) == 999999 &&
              Enumerable.Range(ms.Count + 1, steps.Count - ms.Count).All(i => Dw(w, 2 * i) == 0));

        // ---- unconfirmed: a trial only ----
        Call(page, "Select", 1);
        said.Clear();
        Check("the sash bar not sent without the trial: says why", !(bool)Call(page, "SendSelected") && said.Any(s => s.Contains("not confirmed")), string.Join(" / ", said));
        ((CheckBox)Get(page, "_chkTrial")).Checked = true;
        said.Clear();
        Check("  sent as a trial, the question saying so", (bool)Call(page, "SendSelected") && said.Any(s => s.Contains("TRIAL")), string.Join(" / ", said));
        Check("  the audit log says TRIAL", string.Join("\n", Directory.GetFiles(AuditLogger.LogDirectory).Select(File.ReadAllText)).Contains("(ML8-S-4510) - TRIAL sent"));

        // ---- a bar with problems ----
        Call(page, "Select", 4);
        said.Clear();
        Check("the Elegant frame not sent: its problems listed", !(bool)Call(page, "SendSelected") && said.Any(s => s.Contains("cannot go") && s.Contains("GEN-DRN-6X25")));

        // ---- the Operator level ----
        SecurityManager.Login("op", SecurityLevel.Operator);
        Pump(100);
        Call(page, "ApplyAccess");
        Check("the Operator: no trial, no cutting settings", !((CheckBox)Get(page, "_chkTrial")).Checked && !((CheckBox)Get(page, "_chkTrial")).Enabled &&
              !((NumericUpDown)Get(page, "_numSaw")).Enabled);
        Call(page, "Select", 0);
        Check("  sends a ready bar", (bool)Call(page, "SendSelected"));
        Call(page, "Select", 1);
        said.Clear();
        Check("  not the trial one", !(bool)Call(page, "SendSelected") && said.Any(s => s.Contains("not confirmed")));
        SecurityManager.Login("setter", SecurityLevel.Maintenance);

        // ---- the next start: the job opened again, the bars sent shown ----
        var form2 = new Form { ClientSize = new Size(1500, 900) };
        var page2 = new Kaban.Machines.FA2010.Screens.JobsScreen();
        form2.Controls.Add(page2);
        form2.Show();
        Pump(300);
        Call(page2, "EnsureLibrary");
        var job2 = Get(page2, "Job") as KbnJob;
        var grid2 = (DataGridView)Get(page2, "_bars");
        Check("the next start: the last job opened again, its bars sent shown so", job2 != null && job2.Path == job &&
              Convert.ToString(grid2.Rows[0].Cells["state"].Value).Contains("sent") && Convert.ToString(grid2.Rows[1].Cells["state"].Value).Contains("sent") &&
              !Convert.ToString(grid2.Rows[3].Cells["state"].Value).Contains("sent"),
              string.Join(" | ", grid2.Rows.Cast<DataGridViewRow>().Select(r => Convert.ToString(r.Cells["state"].Value))));

        // ---- not connected ----
        sim.Disconnect();
        Call(page, "Select", 0);
        said.Clear();
        Check("the PLC not connected: not sent, said so", !(bool)Call(page, "SendSelected") && said.Any(s => s.Contains("not connected")), string.Join(" / ", said));

        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
