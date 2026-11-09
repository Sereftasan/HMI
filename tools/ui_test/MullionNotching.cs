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
using Kaban.UI.Profiles;

// THE PROFILE'S MULLION NOTCHING STEPS (7 November, the answer "Yes it should and yes values per profile" - the old program's
// ProfileSeryNotchSetting / WriteNotchInfo): the Profiles page's tab Mullion notching - steps typed on the group's template, a profile of
// the group showing them as its group's, a change making them its own, "Use the group's steps" giving them back, nothing changed below
// Maintenance - and a bar of the profile sent: R25700 the steps x100 (10 words each, 210 in all) before the profile's parameters.
class MN {
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
    static readonly List<string> said = new List<string>();

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "mnotch_" + Guid.NewGuid().ToString("N"));
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
        var notchType = asm.GetType("Kaban.Machines.FA2010.Jobs.MullionNotch");
        string code = (string)Get(notchType, "Code");

        // the setting's text
        var parsed = (List<double[]>)Call(notchType, "Parse", "1,2,3,4,5,6,0.3,40|7,7.5");
        Check("the setting's text: a step a '|', its fields by ',' (what is missing 0)", parsed.Count == 2 && parsed[0][6] == 0.3 && parsed[0][7] == 40 &&
              parsed[1][1] == 7.5 && parsed[1][7] == 0, string.Join(" | ", parsed.Select(x => string.Join(",", x))));
        Check("  written back the same; none is \"-\" (the group's are not taken then)", (string)Call(notchType, "Format", parsed) == "1,2,3,4,5,6,0.3,40|7,7.5,0,0,0,0,0,0" &&
              (string)Call(notchType, "Format", new List<double[]>()) == "-" && ((List<double[]>)Call(notchType, "Parse", "-")).Count == 0);

        string why;
        Library.Open(() => true, out why);
        var pst = asm.GetType("Kaban.Machines.FA2010.Screens.ProfilesScreen");
        long inst = Library.EnsureMachine("FA2010", "FA2010", (KeyValuePair<string, int>[])Get(pst, "ToolPlaces"), (Kaban.Profiles.Machining.Face[])Call(pst, "MachineFaces"));
        var frame = Library.Profiles().First(p => p.Code == "ML8-F-4501");
        long template = Library.GroupTemplateOf(frame.Id);
        var other = Library.Profiles().FirstOrDefault(p => p.GroupId == frame.GroupId && p.Id != template);
        Check("the frame's group: a template and another profile", template > 0 && other != null, template + " / " + (other != null ? other.Code : "none"));

        // ---- the Profiles page ----
        var pform = new Form { ClientSize = new Size(1500, 900) };
        var prof = new Kaban.Machines.FA2010.Screens.ProfilesScreen();
        pform.Controls.Add(prof);
        pform.Show();
        Pump(200);
        Call(prof, "EnsureLibrary");
        Pump(100);
        var tabs = (TabControl)Get(prof, "_detailTabs");
        var tab = (TabPage)Get(prof, "_notchTab");
        var g = (DataGridView)Get(prof, "_notchGrid");
        var head = (Label)Get(prof, "_notchHead");
        Check("the profile's details: a tab Mullion notching after Machine settings", tabs.TabPages.Contains(tab) && tabs.TabPages.IndexOf(tab) == tabs.TabPages.IndexOf((TabPage)Get(prof, "_machineTab")) + 1 &&
              tab.Text == "Mullion notching", string.Join(", ", tabs.TabPages.Cast<TabPage>().Select(t => t.Text)));
        Check("  its table: No, X1 X2 Z1 Z2 Y1 Y2, Step, Speed - the structure's comments as the columns' tips",
              string.Join(",", g.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText)) == "No,X1,X2,Z1,Z2,Y1,Y2,Step,Speed" && g.Columns["X1"].ToolTipText.Contains("plunge depth"),
              string.Join(",", g.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText)) + " / " + g.Columns["X1"].ToolTipText);
        tabs.SelectedTab = tab;
        Call(prof, "SelectProfile", template); Pump(150);
        Check("  the template: none yet - nothing goes to R25700", g.Rows.Count == 0 && head.Text.Contains("R25700") && head.Text.Contains("No mullion notching steps"), head.Text);
        Call(prof, "NotchAdd"); Pump(100);
        Check("+ Step: a step of zeros, kept at once as the profile's own", g.Rows.Count == 1 && Library.MachineSettings(template, inst)[code].Own &&
              Library.MachineSettings(template, inst)[code].Value == "0,0,0,0,0,0,0,0", Library.MachineSettings(template, inst)[code].Value);
        // typed as a user does: into the cell's box, kept as it is left
        Action<int, string, string> type = (r, col, v) =>
        {
            g.CurrentCell = g.Rows[r].Cells[col];
            g.BeginEdit(true);
            g.EditingControl.Text = v;
            g.EndEdit();
            Pump(60);
        };
        type(0, "X1", "12.5");
        type(0, "X2", "12,75");
        type(0, "Z1", "-3");
        type(0, "Z2", "20");
        type(0, "Y2", "70");
        type(0, "Step", "4");
        type(0, "Speed", "35");
        Check("  typed in (a comma taken as the point)", Library.MachineSettings(template, inst)[code].Value == "12.5,12.75,-3,20,0,70,4,35", Library.MachineSettings(template, inst)[code].Value);
        g.CurrentCell = g.Rows[0].Cells["Z1"];
        Call(prof, "NotchAdd"); Pump(100);
        Check("+ Step again: after the selected one, as it (most steps differ in a value or two)", g.Rows.Count == 2 && (string)g.Rows[1].Cells["X1"].Value == "12.5" &&
              (string)g.Rows[1].Cells["no"].Value == "2" && g.CurrentCell.RowIndex == 1,
              g.Rows.Count + " " + (g.Rows.Count > 1 ? g.Rows[1].Cells["X1"].Value + " " + g.Rows[1].Cells["no"].Value : "") + " cur " + (g.CurrentCell != null ? g.CurrentCell.RowIndex.ToString() : "null"));
        type(1, "Y1", "35");
        type(1, "Speed", "abc");
        Check("  a word: not kept (said so)", Library.MachineSettings(template, inst)[code].Value == "12.5,12.75,-3,20,0,70,4,35|12.5,12.75,-3,20,35,70,4,35" &&
              ((Label)Get(prof, "_lblStatus")).Text.Contains("is not a number"), Library.MachineSettings(template, inst)[code].Value + " / " + ((Label)Get(prof, "_lblStatus")).Text);
        type(1, "Speed", "400");
        Check("  400: x100 does not fit the PLC's word - not kept", Library.MachineSettings(template, inst)[code].Value.EndsWith(",35") && ((Label)Get(prof, "_lblStatus")).Text.Contains("327.67"),
              ((Label)Get(prof, "_lblStatus")).Text);
        g.CurrentCell = g.Rows[1].Cells["Y1"];
        Call(prof, "NotchMove", -1); Pump(60);
        Check("▲: the second step first", Library.MachineSettings(template, inst)[code].Value.StartsWith("12.5,12.75,-3,20,35,70") && g.CurrentCell.RowIndex == 0);
        Call(prof, "NotchMove", 1); Pump(60);
        Check("▼: back", Library.MachineSettings(template, inst)[code].Value == "12.5,12.75,-3,20,0,70,4,35|12.5,12.75,-3,20,35,70,4,35");
        var btnGroup = (Button)Get(prof, "_btnNotchGroup");
        Check("  the template: no \"Use the group's steps\" (its own are the group's)", !btnGroup.Enabled && head.Text.Contains("this profile's own"), head.Text);

        // a profile of the group: the group's
        Call(prof, "SelectProfile", other.Id); Pump(150);
        Check("another profile of the group: the group's steps shown as its group's", g.Rows.Count == 2 && head.Text.Contains("group's steps") &&
              !Library.MachineSettings(other.Id, inst)[code].Own && !btnGroup.Enabled, head.Text);
        type(0, "Step", "4.2");
        Check("  a change: they become its own (both steps), the group's stay", Library.MachineSettings(other.Id, inst)[code].Own &&
              Library.MachineSettings(other.Id, inst)[code].Value == "12.5,12.75,-3,20,0,70,4.2,35|12.5,12.75,-3,20,35,70,4,35" &&
              Library.MachineSettings(template, inst)[code].Value.Contains(",4,35|") && btnGroup.Enabled, Library.MachineSettings(other.Id, inst)[code].Value);
        g.CurrentCell = g.Rows[1].Cells["X1"];
        Call(prof, "NotchRemove"); Pump(60);
        string afterOne = g.Rows.Count + " cur " + (g.CurrentCell != null ? g.CurrentCell.RowIndex.ToString() : "null") + " " + Library.MachineSettings(other.Id, inst)[code].Value;
        Call(prof, "NotchRemove"); Pump(60);
        Check("− Step twice: none of its own - \"-\", the group's not taken", g.Rows.Count == 0 && Library.MachineSettings(other.Id, inst)[code].Value == "-" &&
              head.Text.Contains("no mullion notching steps of its own"), afterOne + " / " + head.Text);
        Call(prof, "NotchUseGroup"); Pump(60);
        Check("Use the group's steps: its own forgotten, the group's again", g.Rows.Count == 2 && !Library.MachineSettings(other.Id, inst)[code].Own && !btnGroup.Enabled);
        string log = string.Join("\n", Directory.GetFiles(AuditLogger.LogDirectory).Select(File.ReadAllText));
        Check("  the log keeps each change", log.Contains("mullion notching steps added") && log.Contains("mullion notching steps moved") && log.Contains("its own forgotten"));

        SecurityManager.Login("op", SecurityLevel.Operator);
        Pump(150);
        Call(prof, "ApplyAccess");
        Check("below Maintenance: shown, nothing changed", g.ReadOnly && !((Button)Get(prof, "_btnNotchAdd")).Enabled && !((Button)Get(prof, "_btnNotchRemove")).Enabled && g.Rows.Count == 2);
        Call(prof, "NotchAdd"); Pump(60);
        Check("  (+ Step called anyway: not kept)", Library.MachineSettings(template, inst)[code].Value.Split('|').Length == 2);
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        Pump(150);
        Call(prof, "ApplyAccess");
        pform.Close();

        // ---- a bar of the frame sent ----
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
        Pump(50);
        string job = Path.Combine(dir, "Notch.kbn");
        File.WriteAllLines(job, new[] {
            "Bar;1;" + other.Code + ";Frame;1;White;60000;0;",
            "Profile;10000;10000;0900;0900;1;1;O1;1;C;A;;1;;0;B1;;;;;;",
        });
        Call(page, "OpenJob", job);
        Pump(100);
        var plan = ((IList)Get(page, "Plans"))[0];
        var problems = ((IList)Get(plan, "Problems")).Cast<string>().ToList();
        var notch = (List<double[]>)Get(plan, "Notch");
        Check("the bar planned: the profile's (its group's) 2 steps with it", problems.Count == 0 && notch.Count == 2 && notch[1][4] == 35, string.Join(" / ", problems) + " / " + notch.Count);
        string notes = ((TextBox)Get(page, "_notes")).Text;
        Check("  the notes list them", notes.Contains("Mullion notching: 2 step(s)") && notes.Contains("12.5  12.75  -3  20  35  70  4  35"), notes);
        var sender = asm.GetType("Kaban.Machines.FA2010.Jobs.JobSender");
        var blocks = ((IList)Call(sender, "Blocks", plan, 1, new List<string>())).Cast<object>().ToList();
        Check("  written first, before the profile's parameters (the old program's WriteNotchInfo): R25700, 210 words",
              (string)Get(blocks[0], "Address") == "R25700" && ((short[])Get(blocks[0], "Words")).Length == 210 && (string)Get(blocks[1], "Address") == "R25600",
              string.Join(", ", blocks.Take(3).Select(b => Get(b, "Address") + ":" + ((short[])Get(b, "Words")).Length)));
        for (int i = 0; i <= 210; i++) sim.Write("R" + (25700 + i), 9);      // R25910 past the area
        said.Clear();
        Check("sent (the question names the steps)", (bool)Call(page, "SendSelected") && said.Any(s => s.Contains("2 mullion notching step(s)")), string.Join(" / ", said));
        short[] w;
        sim.TryReadBlock("R25700", 210, out w);
        Check("  R25700: X1 X2 Z1 Z2 Y1 Y2 Step Speed x100, two spare words a step",
              w.Take(10).SequenceEqual(new short[] { 1250, 1275, -300, 2000, 0, 7000, 400, 3500, 0, 0 }) && w[14] == 3500 && w[10] == 1250, string.Join(",", w.Take(20)));
        Check("  the rest of the 210 words 0 (what a profile of more steps left there cleared) - R25910 past them not touched", w.Skip(20).All(x => x == 0) && sim.Read("R25910") == 9,
              "R25910=" + sim.Read("R25910"));

        // a profile without steps: R25700 not written
        Library.SetMachineSetting(other.Id, inst, code, "-");
        Call(page, "Replan");
        plan = ((IList)Get(page, "Plans"))[0];
        sim.Write("R25700", 77);
        said.Clear();
        Check("none of its own (\"-\"): nothing to R25700, the question says nothing of it", ((List<double[]>)Get(plan, "Notch")).Count == 0 && (bool)Call(page, "SendSelected") &&
              sim.Read("R25700") == 77 && !said.Any(s => s.Contains("mullion")), string.Join(" / ", said));

        form.Close();
        Call(milling, "Reset");
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nMULLION NOTCHING ALL PASS" : "\nMULLION NOTCHING " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
