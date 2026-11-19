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

// 19 November ("analyze KabanMachineSystem allover and add everything you find useful"): the old FA2010 job screen's handshake and
// automatic run (_ZC2010 ucJobPreparations: SendBar - L0 off, L1 on, L10 another bar; Check_NextJob - L0 while M21 asks for the next
// bar, D91 its time), the production log (the old Customer Report / General Information) and the job file's Screw / Hole lines.
class JR {
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
        var milling = asm.GetType("Kaban.Machines.FA2010.Jobs.JobMilling");
        milling.GetField("StatePath", Any).SetValue(null, Path.Combine(dir, "JobMilling.cfg"));
        Call(milling, "Reset");

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
        Pump(300);
        string job = Path.Combine(dir, "JobSample_Library.kbn");
        File.Copy("/home/claude/work/docs/fa2010/JobSample_Library.kbn", job);

                ProductionLog.Folder = Path.Combine(dir, "Production");
        var run = asm.GetType("Kaban.Machines.FA2010.Jobs.JobRun");
        Call(run, "Reset");
        ((NumericUpDown)Get(page, "_numSaw")).Value = 4.2M;
        Pump(50);
        Call(page, "OpenJob", job);
        Pump(100);
        var plans = Plans(page);
        var grid = (DataGridView)Get(page, "_bars");

        // ================================================================ a bar by hand: the handshake
        Console.WriteLine("--- a bar sent by hand: its handshake ---");
        sim.Write("L0", 1); sim.Write("L10", 1);
        Call(page, "Select", 0);
        said.Clear();
        Check("bar 1 sent", (bool)Call(page, "SendSelected"), string.Join(" / ", said));
        Check("  then L10 off (no bar after it), L0 off (the request answered), L1 on (Is Geldi - the bar has come)",
              sim.Read("L10") == 0 && sim.Read("L0") == 0 && sim.Read("L1") == 1, sim.Read("L10") + " " + sim.Read("L0") + " " + sim.Read("L1"));
        var prod = ProductionLog.Read(DateTime.Today, DateTime.Today);
        Check("  the production log: the bar sent - its job, profile, length, pieces, the user", prod.Count == 1 && prod[0].Kind == ProductionKind.BarSent &&
              prod[0].Bar == "1" && prod[0].Profile == "ML8-F-4501" && prod[0].LengthMm == 6500 && prod[0].Pieces > 0 && prod[0].User == "setter" && !prod[0].Automatic,
              prod.Count == 0 ? "none" : prod[0].Kind + " " + prod[0].Bar + " " + prod[0].Profile + " " + prod[0].LengthMm + " " + prod[0].User);
        // the machine finishes it: L0 while M21
        sim.Write("D91", 95); sim.Write("D92", 0);
        Call(run, "Poll");
        sim.Write("L0", 1);
        sim.Write("M21", 0);
        Call(run, "Poll");
        Check("  the machine not active (M21 off): nothing taken", ProductionLog.Read(DateTime.Today, DateTime.Today).Count == 1);
        sim.Write("M21", 1);
        Call(run, "Poll");
        Pump(100);
        prod = ProductionLog.Read(DateTime.Today, DateTime.Today);
        Check("the PLC asks for the next bar (L0, M21 on): bar 1 finished, its time 95 s (D91)", prod.Count == 2 && prod[1].Kind == ProductionKind.BarFinished &&
              prod[1].Seconds == 95 && prod[1].Bar == "1", prod.Count + " " + (prod.Count > 1 ? prod[1].Kind + " " + prod[1].Seconds : ""));
        Check("  the bar's state: finished", Convert.ToString(grid.Rows[0].Cells["state"].Value).Contains("finished"), Convert.ToString(grid.Rows[0].Cells["state"].Value));
        Call(run, "Poll");
        Check("  nothing sent after a bar sent by hand", ProductionLog.Read(DateTime.Today, DateTime.Today).Count == 2);

        // ================================================================ automatic
        Console.WriteLine("--- automatic: the bars one after the other ---");
        ((CheckBox)Get(page, "_chkAuto")).Checked = true;
        Call(page, "Select", 2);
        said.Clear();
        sim.Write("L0", 1);
        Check("Automatic: bar 3 sent now, the question saying the others follow", (bool)Call(page, "SendSelected") && said.Any(s => s.Contains("AUTOMATIC")),
              string.Join(" / ", said));
        Check("  L10 on: another bar follows (bar 4); bar 5 has problems - the run stops before it",
              sim.Read("L10") == 1 && sim.Read("L1") == 1 && sim.Read("L0") == 0 && ((List<int>)Call(run, "get_Queue")).SequenceEqual(new[] { 3 }),
              sim.Read("L10") + " " + string.Join(",", (List<int>)Call(run, "get_Queue")));
        Pump(100);
        Check("  the Stop button shown", ((Button)Get(page, "_btnStopRun")).Visible);
        short[] w;
        var ms3 = Steps(plans[3]);
        string t3 = ((KbnBar)Get(plans[2], "Bar")).Title, t4 = ((KbnBar)Get(plans[3], "Bar")).Title;
        sim.Write("L1", 0);
        Call(run, "Poll");
        sim.Write("D91", 120);
        sim.Write("L0", 1);
        Call(run, "Poll");
        Pump(100);
        sim.TryReadBlock("D6000", 2 * (ms3.Count + 1), out w);
        prod = ProductionLog.Read(DateTime.Today, DateTime.Today);
        var last = prod.Last();
        Check("the PLC asks (L0): bar 3 finished (120 s), bar 4 sent by itself - its job list in D6000, L10 off (the last), L1 on",
              prod.Any(e => e.Kind == ProductionKind.BarFinished && e.Bar == t3 && e.Seconds == 120) && last.Kind == ProductionKind.BarSent && last.Bar == t4 &&
              last.Automatic && Dw(w, 2 * ms3.Count) == 999999 && sim.Read("L10") == 0 && sim.Read("L1") == 1,
              string.Join(" | ", prod.Select(e => e.Kind + " " + e.Bar)) + " L10=" + sim.Read("L10"));
        Check("  both bars shown finished / sent", Convert.ToString(grid.Rows[2].Cells["state"].Value).Contains("finished") &&
              Convert.ToString(grid.Rows[3].Cells["state"].Value).Contains("sent"),
              Convert.ToString(grid.Rows[2].Cells["state"].Value) + " | " + Convert.ToString(grid.Rows[3].Cells["state"].Value));
        Call(run, "Poll");
        sim.Write("L0", 1);
        Call(run, "Poll");
        Pump(100);
        prod = ProductionLog.Read(DateTime.Today, DateTime.Today);
        Check("the last bar finished: the run ends", !(bool)Call(run, "get_On") && prod.Any(e => e.Kind == ProductionKind.BarFinished && e.Bar == t4) &&
              prod.Last().Kind == ProductionKind.RunEnded, prod.Last().Kind.ToString());
        Check("  the Stop button hidden again", !((Button)Get(page, "_btnStopRun")).Visible);

        // stopped by the operator
        Call(run, "Reset");
        var jobObj = Get(page, "Job");
        var planList = Plans(page).Cast<object>().ToList();
        var typed = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(planList[0].GetType()));
        foreach (var p in planList) typed.Add(p);
        sim.Write("L0", 1);
        var whyRun = (string)run.GetMethod("Start", Any).Invoke(null, new object[] { jobObj, typed, 2, new List<int>(), 1, false });
        Check("a run from bar 3 again (bar 4 after it)", whyRun == "" && (bool)Call(run, "get_On") && sim.Read("L10") == 1, whyRun);
        Call(run, "Stop", "stopped by the operator");
        Check("  stopped: L10 off - the bar in the machine is the last", !(bool)Call(run, "get_On") && sim.Read("L10") == 0);

        // ================================================================ the production page
        Console.WriteLine("--- HMI Settings > Production ---");
        ProductionLog.Counter(new ProductionCounter { Machine = "FA2010", Key = "Prd_PowerOn", Text = "Power on", Station = 1, Address = "D93" });
        sim.Write("D93", 3600 * 5 + 120); sim.Write("D94", 0);
        var prodPage = new Kaban.UI.Settings.ProductionControl();
        Check("the log's lines, newest first; the sums", prodPage.Grid.Rows.Count == ProductionLog.Read(DateTime.Today, DateTime.Today).Count &&
              prodPage.Sums.Contains("finished 3") && prodPage.Sums.Contains("Bars sent"), prodPage.Sums);
        Check("  the machine's counters (D93: 5 h 02 min with power on)", prodPage.CountersText.Contains("5 h 02 min"), prodPage.CountersText);
        prodPage.SearchBox.Text = "ML8-M";
        Check("  found by a profile", prodPage.Grid.Rows.Count > 0 && prodPage.Grid.Rows.Cast<DataGridViewRow>().All(r => Convert.ToString(r.Cells["profile"].Value).StartsWith("ML8-M")),
              prodPage.Grid.Rows.Count.ToString());
        var csv = Path.Combine(dir, "export.csv");
        ProductionLog.Export(ProductionLog.Read(DateTime.Today, DateTime.Today), csv);
        Check("  exported as CSV (Excel)", File.ReadAllLines(csv)[0].TrimStart('﻿') == ProductionLog.Header && File.ReadAllLines(csv).Length == ProductionLog.Read(DateTime.Today, DateTime.Today).Count + 1);

        // ================================================================ the screwing machine's lines
        Console.WriteLine("--- the job file's Screw / Hole lines ---");
        var sj = KbnFile.Parse(new[] { "Bar;1;ML8-F-4501;F;1;W;65000;;", "Profile;12505;11605;450;450;T;1;O;1;C;L;;1;;0;;;;;;;0;0;0;1;;0",
                                       "Screw;100;250;0;400;", "Hole2;150;" }, "screws");
        var pc = sj.Bars[0].Pieces[0];
        Check("read: Screw 100, 250, 400 (0 none), Hole2 150 - nothing left as an unknown line", sj.Problems.Count == 0 && pc.Screws.Count == 2 && pc.Screws[0].Count == 3 &&
              pc.Screws[1].IsHole && pc.Other.Count == 0, string.Join(" | ", sj.Problems));
        Check("  written back as read", KbnFile.Format(sj.Bars, null).Contains("Screw;100;250;0;400;\r\nHole2;150;"));

        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
