using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Forms;

// 6 November: "create label structure for the profile parameter and a label on the PLC monitor page so I can monitor what HMI
// writes to the PLC as the profile data. I want you to do this for all the devices the HMI writes to the PLC"
// (was: 5 November: "It had deleted the labels I created on the PLC Monitor page when I changed the view scale the other day, there
// should be a bug for it". What the panel's log and the lists' files showed: the screen scale restarts the program; a name
// typed and not yet confirmed (its cell still open - the top bar's scale menu and its question do not take the focus from the
// cell) was not kept, and a second instance (one restarted outside Visual Studio, another started from it) wrote the list it
// had over the other's.
class MSent {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static Type MonType = typeof(SimulationMonitorForm);
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
        var p = t.GetProperty(n, Any); return p.GetValue(o is Type ? null : o, null);
    }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length >= a.Length &&
                                              m.GetParameters().Skip(a.Length).All(p => p.IsOptional));
        var args = a.Concat(mi.GetParameters().Skip(a.Length).Select(p => p.DefaultValue)).ToArray();
        return mi.Invoke(o is Type ? null : o, args);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "monsent_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        int n = PlcLabelTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));
        StructuredTypes.FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StructuredTypes.csv");     // FA2010's (no machine mounted here)
        StructuredTypes.Reload();
        Check("the FA2010 label list read", n > 60, n.ToString());
        var pp = PlcLabelTable.ByLabel("PC_ProfilParametreleri");
        Check("  the profile's parameters: a structured label at R25600, under its heading",
              pp != null && pp.Address == "R25600" && pp.DataType == "Profile Parameters" && pp.Heading.StartsWith("The job and the profile"));
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(22);
        PlcManager.Instance.RegisterDriver(22, sim);
        PlcManager.Instance.RegisterStation(22, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(22, true);
        var said = new List<string>();
        MonType.GetField("ReportHook", Any).SetValue(null, (Action<string>)(t => said.Add(t)));
        var form = new SimulationMonitorForm();
        form.Show(); Pump(200);
        var mon = ((IList)Get(form, "_monitors")).Cast<object>().First(m => (int)Get(m, "Station") == 22);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = ((IList)Get(form, "_monitors")).IndexOf(mon);
        Pump(100);
        // 6 November: "I don't see any label created on the label list, I want you to create them for me"
        var made = ((IList)Get(mon, "Sheets")).Cast<object>().FirstOrDefault(x => (string)Get(x, "Name") == "HMI -> PLC");
        string file = File.ReadAllText(Directory.GetFiles(dir, "*.csv").First());
        Check("made by itself when the monitor opens (once: kept in the list's file)", made != null && file.Contains("#made:sent") && file.Contains("#sheet:HMI -> PLC"));
        Call(form, "CollectSent"); Pump(100);
        var sheets = ((IList)Get(mon, "Sheets")).Cast<object>().ToList();
        var sh = sheets.FirstOrDefault(x => (string)Get(x, "Name") == "HMI -> PLC");
        Check("a tab of its own: HMI -> PLC", sh != null, string.Join(", ", sheets.Select(x => (string)Get(x, "Name"))));
        var rows = ((IList)Get(sh, "Rows")).Cast<object>().ToList();
        Func<object, string> addr = r => (string)Get(r, "Address") ?? "";
        Func<object, string> comment = r => (string)Get(r, "Comment") ?? "";
        var heads = rows.Select(comment).Where(c => c.Length > 0).ToList();
        // 7 November: "The machine condition labels should not be added to the HMI write labels either since they are not HMI writes"
        Check("  under the list's headings: the job and the profile, the job's other devices, what the PLC gives back - not the machine's options",
              heads.Count >= 3 && heads[0].StartsWith("The job and the profile") && !heads.Any(h => h.StartsWith("The machine's options")) &&
              heads.Any(h => h.StartsWith("What the PLC gives back")), string.Join(" | ", heads));
        var tables = ((IList)Get(Get(sh, "Tables"), "Rows")).Cast<object>().ToList();
        Check("  in its list: the profile's parameters, the G-code numbers, the barcode - not the options (L24, L33)",
              new[] { "R25600", "D60", "D386" }.All(a => rows.Any(r => addr(r) == a)) && !rows.Any(r => addr(r) == "L24" || addr(r) == "L33"),
              string.Join(",", rows.Select(addr).Where(a => a.Length > 0).Take(30)));
        // 6 November: "PC job list labels should be an array (the length is 500) and they should come together in one table view"
        Check("  the arrays as tables, not in the list: the job list, the milling and cutting parameters and steps",
              new[] { "D5000", "D6000", "D7000", "R26000", "R25650", "R0", "R12800", "R25700" }.All(a => tables.Any(r => addr(r) == a) && !rows.Any(r => addr(r) == a)),
              string.Join(",", tables.Where(r => !(bool)Get(r, "IsElement") && !(bool)Get(r, "IsMember")).Select(addr).Where(a => a.Length > 0).Distinct()));
        var groups = ((System.Collections.IEnumerable)MonType.GetMethod("TableGroups", Any).Invoke(null, new[] { sh })).Cast<IList>().ToList();
        var job = groups.FirstOrDefault(gr => addr(gr[0]) == "D5000");
        Check("  the job list ONE table: ID, position, code - 500 lines", job != null && job.Count == 3 && addr(job[1]) == "D6000" && addr(job[2]) == "D7000" &&
              (int)Get(job[0], "EffectiveCount") == 500, job == null ? "none" : string.Join("+", job.Cast<object>().Select(addr)));
        var stepsTable = groups.FirstOrDefault(gr => addr(gr[0]) == "R0");
        Check("  the milling steps (400 x 16 words): their first 25 lines (the old program's 400 words), the milling parameters all 100",
              stepsTable != null && (int)Get(stepsTable[0], "EffectiveCount") == 25 && (int)Get(groups.First(gr => addr(gr[0]) == "R26000")[0], "EffectiveCount") == 100);
        Check("  no heading line among the tables (\"The comment line is not needed\")", !tables.Any(r => comment(r).Length > 0), string.Join(" | ", tables.Select(comment).Where(c => c.Length > 0)));
        // 7 November: "The label names should be shown as the natural words ... work job positions should have their divisor and decimal quantity"
        var cols = ((System.Collections.IEnumerable)MonType.GetMethod("TableColumnsOf", Any).Invoke(null, new object[] { job })).Cast<object>().Select(c => (string)Get(c, "Header")).ToList();
        Check("  the job list's columns in words: PC Is Listesi Islem ID, PC Is Listesi Pozisyon, PC Is Listesi Islem Kodu", cols.SequenceEqual(new[] { "PC Is Listesi Islem ID", "PC Is Listesi Pozisyon", "PC Is Listesi Islem Kodu" }),
              string.Join(" | ", cols));
        Check("  its positions divided by 100 with 2 places (the label list's), the IDs and codes as they are",
              (double)Get(job[1], "EffectiveDivisor") == 100 && (int)Get(job[1], "EffectiveDecimals") == 2 && (double)Get(job[0], "EffectiveDivisor") == 1,
              Get(job[1], "EffectiveDivisor") + " " + Get(job[1], "EffectiveDecimals"));
        var d106 = rows.First(r => addr(r) == "D106");
        Check("  the bar's length (D106, x10000): 10000 and 2 places", (double)Get(d106, "EffectiveDivisor") == 10000 && (int)Get(d106, "EffectiveDecimals") == 2);

        // a structure's members by their own names ("Tool No" is enough)
        var pr = rows.First(r => addr(r) == "R25600");
        pr.GetType().GetField("Expanded", Any).SetValue(pr, true);
        Call(form, "Render", mon, sh); Pump(50);
        var grid = (DataGridView)Get(sh, "Grid");
        var labels = grid.Rows.Cast<DataGridViewRow>().Select(r => Convert.ToString(r.Cells["Label"].Value)).ToList();
        var prRow = grid.Rows[rows.IndexOf(pr)];
        Check("the labels in words: PC_ProfilParametreleri shown 'PC Profil Parametreleri', its own name in its tooltip",
              Convert.ToString(prRow.Cells["Label"].Value).Contains("PC Profil Parametreleri") && prRow.Cells["Label"].ToolTipText == "PC_ProfilParametreleri",
              Convert.ToString(prRow.Cells["Label"].Value) + " / " + prRow.Cells["Label"].ToolTipText);
        Check("a structure's members under it by their own names: 'Conveyor Speed', not 'PC_ProfilParametreleri.Conveyor Speed'",
              labels.Any(l => l.Trim() == "Conveyor Speed") && !labels.Any(l => l.Contains("PC_ProfilParametreleri.")),
              string.Join(" | ", labels.Take(8)));
        var memberRow = grid.Rows.Cast<DataGridViewRow>().First(r => Convert.ToString(r.Cells["Label"].Value).Trim() == "Conveyor Speed");
        Check("  its whole name in the tooltip", memberRow.Cells["Label"].ToolTipText == "PC_ProfilParametreleri.Conveyor Speed", memberRow.Cells["Label"].ToolTipText);
        pr.GetType().GetField("Expanded", Any).SetValue(pr, false);

        // a tab made before the arrays were tables: its job list a single row of the list - put right
        var tRows = (IList)Get(Get(sh, "Tables"), "Rows");
        tRows.Clear();
        var listRows = (IList)Get(sh, "Rows");
        var old = Activator.CreateInstance(rows[0].GetType());
        old.GetType().GetField("Station").SetValue(old, 22);
        old.GetType().GetField("Address").SetValue(old, "D5000");
        old.GetType().GetField("DataTypeOverride").SetValue(old, "Double Word [Signed](0..499)");
        listRows.Insert(0, old);
        Call(form, "FillSentTab", mon, false); Pump(50);
        var after = ((IList)Get(sh, "Rows")).Cast<object>().ToList();
        groups = ((System.Collections.IEnumerable)MonType.GetMethod("TableGroups", Any).Invoke(null, new[] { sh })).Cast<IList>().ToList();
        Check("a tab made before (the job list a single row): the row gone, the job list a table again", !after.Any(r => addr(r) == "D5000") &&
              groups.Any(gr => addr(gr[0]) == "D5000" && gr.Count == 3), string.Join(",", after.Select(addr).Where(a => a.Length > 0).Take(10)));
        Check("  not the label list's other devices (MAN_, IN_ ...)", !rows.Any(r => addr(r) == "M604") && !rows.Any(r => addr(r) == "M200"));

        // a tab made before 7 November: the options in its list under their heading, a heading line among its tables - put right once
        var listNow = (IList)Get(sh, "Rows");
        var rowType = rows[0].GetType();
        Func<string, string, object> mk = (a, c) => { var x = Activator.CreateInstance(rowType); rowType.GetField("Station").SetValue(x, 22);
                                                       rowType.GetField("Address").SetValue(x, a); rowType.GetField("Comment").SetValue(x, c); return x; };
        listNow.Insert(listNow.Count - 1, mk("", "The machine's options the HMI writes (L)"));
        listNow.Insert(listNow.Count - 1, mk("L24", ""));
        listNow.Insert(listNow.Count - 1, mk("L33", ""));
        var tNow = (IList)Get(Get(sh, "Tables"), "Rows");
        tNow.Insert(0, mk("", "The job and the profile the HMI sends (Profiles & Operations)"));
        Call(form, "PutRightSentTab", mon); Pump(50);
        var rowsNow = ((IList)Get(sh, "Rows")).Cast<object>().ToList();
        Check("  the options' rows and their heading taken off the list, the heading line off the tables",
              !rowsNow.Any(r => addr(r) == "L24" || addr(r) == "L33" || comment(r).StartsWith("The machine's options")) &&
              !((IList)Get(Get(sh, "Tables"), "Rows")).Cast<object>().Any(r => comment(r).Length > 0) && rowsNow.Any(r => addr(r) == "D60"));
        // a device a machine condition reads: not the HMI's either
        MachineConditionMap.RegisterMachineBit(1L << 50, "T.Test", "Test condition", 22, "D386");
        var sentNow = (System.Collections.IList)MonType.GetMethod("SentEntries", Any).Invoke(null, new object[] { 22 });
        Check("  a device a machine condition reads (D386 here): left out", !sentNow.Cast<PlcLabelTable.Entry>().Any(e => e.Address == "D386") &&
              sentNow.Cast<PlcLabelTable.Entry>().Any(e => e.Address == "D60"));
        MachineConditionMap.Clear();
        var swr = System.Diagnostics.Stopwatch.StartNew();
        Call(form, "RefreshValues"); Pump(50);
        swr.Stop();
        Check("  its values read quickly (arrays folded)", swr.ElapsedMilliseconds < 3000, swr.ElapsedMilliseconds + " ms");
        int count = rows.Count;
        Call(form, "CollectSent"); Pump(100);
        Check("again: the same tab, nothing twice", ((IList)Get(sh, "Rows")).Count == count && ((IList)Get(mon, "Sheets")).Count == sheets.Count &&
              ((IList)Get(Get(sh, "Tables"), "Rows")).Count == tables.Count);
        form.Close();
        Console.WriteLine(fail == 0 ? "MONITOR SENT ALL PASS" : "MONITOR SENT " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
