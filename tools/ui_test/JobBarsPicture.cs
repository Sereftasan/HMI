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

// THE JOB'S BARS DRAWN (10 November: "the job info given in 'Jobs' page is at the home page at the old program (it draws a bar image for each
// job bar with the cut and accessory operation points marked, can you create such a thing as well, it gets easier to see than reading it what
// is on the job)"): the Jobs page draws every bar of the job - its pieces, the leftover, a numbered dot at each accessory operation, the V
// cuts and the special cuts - and what the numbers are; a click on a bar selects it, the bar selected in the table is the one marked.
class JBP {
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

    [STAThread]
    static void Main(string[] args) {
        var dir = Path.Combine(Path.GetTempPath(), "jbp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        Db.ConnectionTarget = Path.Combine(dir, "Kaban.db");
        Db.SqliteChoice = Db.SqliteLibrary.Native;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        PlcLabelTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));
        StructuredTypes.FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StructuredTypes.csv");
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        Ask.Hook = (title, prompt, initial) => initial == "yes" ? "yes" : initial;
        var asm = typeof(Kaban.Machines.FA2010.Screens.JobsScreen).Assembly;
        typeof(Kaban.Machines.FA2010.Screens.JobsScreen).GetField("StatePath", Any).SetValue(null, Path.Combine(dir, "Jobs.cfg"));
        var milling = asm.GetType("Kaban.Machines.FA2010.Jobs.JobMilling");
        milling.GetField("StatePath", Any).SetValue(null, Path.Combine(dir, "JobMilling.cfg"));
        Call(milling, "Reset");
        string why;
        Library.Open(() => true, out why);
        var pst = asm.GetType("Kaban.Machines.FA2010.Screens.ProfilesScreen");
        long inst = Library.EnsureMachine("FA2010", "FA2010", (KeyValuePair<string, int>[])Get(pst, "ToolPlaces"), (Kaban.Profiles.Machining.Face[])Call(pst, "MachineFaces"));
        Library.Exec("UPDATE mounted_tool SET diameter_mm = CASE position WHEN 'U1-T1' THEN 5 WHEN 'U1-T2' THEN 10 WHEN 'U2-T1' THEN 6 ELSE 8 END WHERE instance_id=@p0", inst);
        foreach (var p in Library.Profiles().Where(x => x.Code == "ML8-F-4501" || x.Code == "ML8-S-4510")) {
            Library.SetReference(p.Id, "OUTSIDE_SURFACE", 0, 30, "MANUAL", 1, "setter");
            Library.SetReference(p.Id, "HARDWARE_AXIS", 22, p.EffectiveHeight - 10, "MANUAL", 1, "setter");
            Library.SetReference(p.Id, "INSIDE_SURFACE", p.EffectiveWidth, 30, "MANUAL", 1, "setter");
            Library.Resolve(p.Id);
            foreach (var x in Library.Placements(p.Id)) Library.ConfirmPlacement(x.Id, "setter", "test");
        }
        var form = new Form { ClientSize = new Size(1260, 980), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var page = new Kaban.Machines.FA2010.Screens.JobsScreen();
        form.Controls.Add(page);
        form.Show();
        Pump(300);
        Call(page, "EnsureLibrary");
        ((NumericUpDown)Get(page, "_numSaw")).Value = 4.2M;
        Pump(50);
        string job = Path.Combine(dir, "JobSample_Library.kbn");
        File.Copy("/home/claude/work/docs/fa2010/JobSample_Library.kbn", job);
        Call(page, "OpenJob", job);
        Pump(150);
        var view = (Control)Get(page, "_pictures");
        var plans = (IList)Get(page, "Plans");
        var rows = (IList<Rectangle>)Get(view, "Rows");
        Check("the Jobs page draws the job's bars: a strip each (7), across the page above the tables", rows.Count == plans.Count && plans.Count == 7 &&
              view.Width > 1200 && view.Parent is SplitterPanel, rows.Count + " " + view.Width);
        view.Refresh();
        Application.DoEvents();
        var drawn = (List<string>)Get(view, "Drawn");
        Check("  bar 1: its pieces with their lengths, its leftover", drawn.Any(d => d.StartsWith("bar 1 piece 1 ")) && drawn.Any(d => d.StartsWith("bar 1 leftover")),
              string.Join(" | ", drawn.Take(12)));
        var steps = ((IList)Get(plans[0], "Steps")).Cast<object>().Where(s => Get(s, "Op") != null).ToList();
        Check("  a numbered dot at each accessory operation (the milling operation's number)", steps.Count > 0 &&
              steps.All(s => drawn.Any(d => d.StartsWith("bar 1 op " + Get(Get(s, "Op"), "Index") + " @"))),
              steps.Count + " ops: " + string.Join(" | ", drawn.Where(d => d.Contains(" op "))));
        var grid = (DataGridView)Get(page, "_bars");
        Check("the bar selected in the table is marked (1)", (int)Get(view, "Selected") == 0);
        Call(page, "Select", 2);
        Pump(50);
        Check("  bar 3 selected in the table: marked in the drawing", (int)Get(view, "Selected") == 2);
        // a click on bar 5's strip: the table selects it
        var r5 = rows[4];
        Call(view, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 40, r5.Top + 30 + ((Point)Get(view, "AutoScrollPosition")).Y, 0));
        Pump(50);
        Check("a click on bar 5's drawing: bar 5 selected in the table, its pieces shown", grid.CurrentRow != null && grid.CurrentRow.Index == 4 && (int)Get(view, "Selected") == 4,
              grid.CurrentRow != null ? grid.CurrentRow.Index.ToString() : "none");

        // a job of special ends and a V cut: the V cut green, the special cut red
        File.WriteAllLines(Path.Combine(dir, "Special.kbn"), new[] {
            "Bar;1;ML8-F-4501;Frame;1;White;60000;0;",
            "Profile;10000;10000;0450;0900;1;1;O1;1;C;A;;1;;0;B1;;;;;;",
            "VCut;2500;0;",
            "Profile;10000;10000;0900;1350;1;2;O1;1;C;A;;1;;0;B2;;;;;;",
        });
        Call(page, "OpenJob", Path.Combine(dir, "Special.kbn"));
        Pump(150);
        view.Refresh();
        Application.DoEvents();
        drawn = (List<string>)Get(view, "Drawn");
        Check("a V cut drawn at its place", drawn.Any(d => d.StartsWith("bar 1 vcut V @")), string.Join(" | ", drawn));
        Call(page, "OpenJob", job);
        Pump(150);
        // ---- the Home page: the same job's bars (11 November: "Yes") ----
        var hform = new Form { ClientSize = new Size(1266, 818), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var home = new Kaban.Machines.FA2010.Screens.HomeScreen();
        hform.Controls.Add(home);
        hform.Show();
        Pump(200);
        var hview = (Control)Get(home, "_jobBars");
        var hhead = (Label)Get(home, "_jobHead");
        Check("the Home page draws the opened job's bars (7) under its name", ((IList<Rectangle>)Get(hview, "Rows")).Count == 7 && hhead.Text.Contains("JobSample_Library") &&
              hview.Parent.Name == "jobBarsPlace", hhead.Text);
        Call(page, "Select", 5);
        Pump(100);
        Check("  the bar selected on the Jobs page (6) marked there", (int)Get(hview, "Selected") == 5, Get(hview, "Selected").ToString());
        var hr = ((IList<Rectangle>)Get(hview, "Rows"))[1];
        Call(hview, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 40, hr.Top + 30 + ((Point)Get(hview, "AutoScrollPosition")).Y, 0));
        Pump(150);
        Check("  bar 2 clicked on the Home page: the Jobs page selects it", grid.CurrentRow != null && grid.CurrentRow.Index == 1 && (int)Get(view, "Selected") == 1,
              grid.CurrentRow != null ? grid.CurrentRow.Index.ToString() : "none");
        if (args.Length > 1) {
            hform.Activate(); Pump(300);
            using (var bmp = new Bitmap(hform.ClientSize.Width, hform.ClientSize.Height)) {
                using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(hform.PointToScreen(Point.Empty), Point.Empty, bmp.Size);
                bmp.Save(args[1]);
            }
        }
        hform.Close();
        if (args.Length > 0) {
            using (var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) {
                form.Activate(); Pump(300);
                using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, bmp.Size);
                bmp.Save(args[0]);
            }
            Console.WriteLine("shot " + args[0]);
        }
        form.Close();
        Call(milling, "Reset");
        Console.WriteLine(fail == 0 ? "\nJOB BARS PICTURE ALL PASS" : "\nJOB BARS PICTURE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
