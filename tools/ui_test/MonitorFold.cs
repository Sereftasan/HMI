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
using Kaban.UI.Forms;

// FOLDING IN THE PLC MONITOR (7 November: "Is it also possible to add '>' arrow to each table label left end to extend them to show/hide
// when required ... It would be useful for the comment lines to have this function on both list view and table view pages so the user
// could show/hide the labels until the next comment line. Also a little space in between each table in the table view page would make
// them look clearer (and just before the comment lines ...)"): a comment line of the list folds the rows under it until the next one; a
// table folds to its caption; a comment line among the tables folds the tables under it; kept with the list; a gap between the tables,
// a bigger one before a comment line.
class MF {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (string.IsNullOrEmpty(d) ? "" : "   [" + d + "]")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static Type MonType = typeof(SimulationMonitorForm);
    static Type RowType = MonType.GetNestedType("WatchRow", Any);
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
        var p = t.GetProperty(n, Any); return p.GetValue(o is Type ? null : o, null);
    }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, Any).SetValue(o, v); }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length);
        return mi.Invoke(o is Type ? null : o, a);
    }
    static IList Rows(object sheet) { return (IList)Get(sheet, "Rows"); }
    static object Row(string address, string type, int count, string label) {
        var r = Activator.CreateInstance(RowType);
        Set(r, "Address", address); Set(r, "DataTypeOverride", type); Set(r, "LabelOverride", label); Set(r, "ArrayCount", count); Set(r, "Station", 24);
        return r;
    }
    static object Note(string text) { var r = Activator.CreateInstance(RowType); Set(r, "Comment", text); Set(r, "Station", 24); return r; }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "monf_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(24);
        PlcManager.Instance.RegisterDriver(24, sim);
        PlcManager.Instance.RegisterStation(24, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(24, true);
        var form = new SimulationMonitorForm { Width = 1200, Height = 900 };
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 24);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        var sh = Call(form, "EnsureSheet", mon);
        var grid = (DataGridView)Get(sh, "Grid");

        Console.WriteLine("--- the list: a comment line folds the rows under it ---");
        var a = Note("Group A");
        var x = Row("D100", "Word [Signed]", 1, "Xa");
        var y = Row("D101", "Word [Signed]", 1, "Ya");
        var b = Note("Group B");
        var z = Row("D102", "Word [Signed]", 1, "Zb");
        foreach (var r in new[] { a, x, y, b, z }) Rows(sh).Insert(Rows(sh).Count - 1, r);
        Call(form, "RefreshValues"); Application.DoEvents();
        Func<object, DataGridViewRow> gr = r => grid.Rows[Rows(sh).IndexOf(r)];
        Check("a comment line's fold mark before its text (open)", Convert.ToString(gr(a).Cells["Label"].Value) == "▾ Group A", Convert.ToString(gr(a).Cells["Label"].Value));
        Call(form, "ToggleComment", mon, sh, a);
        Check("folded: its rows hidden until the next comment line, the mark turned", !gr(x).Visible && !gr(y).Visible && gr(b).Visible && gr(z).Visible &&
              Convert.ToString(gr(a).Cells["Label"].Value) == "▸ Group A" && grid.Rows[grid.Rows.Count - 1].Visible);
        var lines = ((IEnumerable<string>)Call(MonType, "Serialize", mon)).ToList();
        Check("  kept with the list (fold=1)", lines.Any(l => l.StartsWith("Group A;") && l.Contains("fold=1")) && lines.Any(l => l.StartsWith("Group B;") && !l.Contains("fold")),
              string.Join(" / ", lines.Where(l => l.StartsWith("Group"))));
        var copy = Activator.CreateInstance(MonType.GetNestedType("MonitorTab", Any), true);
        MonType.GetMethod("Deserialize", Any).Invoke(null, new object[] { copy, lines });
        var back = Rows(((IList)Get(copy, "Sheets"))[0]).Cast<object>().First(r => (string)Get(r, "Comment") == "Group A");
        Check("  read back folded", (bool)Get(back, "Folded"));
        Call(MonType, "Select", sh, new[] { Rows(sh).IndexOf(y) });
        Check("a row under it asked for (Find): the comment line unfolds", !(bool)Get(a, "Folded") && gr(y).Visible);

        Console.WriteLine("--- the tables: a table folds to its caption, a comment line the tables under it ---");
        var t1 = Row("D5000", "Double Word [Signed]", 20, "Table One");
        var t2 = Row("D6000", "Double Word [Signed]", 20, "Table Two");
        var c = Note("More tables");
        var t3 = Row("D7000", "Word [Signed]", 20, "Table Three");
        var tables = Rows(Get(sh, "Tables"));
        foreach (var r in new[] { t1, t2, c, t3 }) tables.Add(r);
        Call(form, "RefreshValues"); Application.DoEvents();
        ((TabControl)Get(sh, "ViewTabs")).SelectedIndex = 1;
        Application.DoEvents();
        Call(MonType, "LayoutTables", sh);
        var blocks = ((IList)Get(sh, "Blocks")).Cast<object>().ToList();
        Func<int, Panel> panel = i => (Panel)Get(blocks[i], "Panel");
        Func<int, Label> caption = i => (Label)Get(blocks[i], "Caption");
        Check("four items: three tables, a comment line", blocks.Count == 4 && (bool)Get(blocks[2], "IsComment"));
        Check("  each table's caption with its fold mark", caption(0).Text.StartsWith("▾") && caption(0).Text.Contains("Table One") && caption(2).Text.StartsWith("▾"),
              caption(0).Text + " / " + caption(2).Text);
        int gapTables = panel(1).Top - panel(0).Bottom, gapComment = panel(2).Top - panel(1).Bottom;
        Check("  a gap between the tables, a bigger one before the comment line", gapTables >= 8 && gapComment > gapTables, gapTables + " / " + gapComment);
        Check("  the comment line one line tall (no big box)", panel(2).Height <= caption(0).Height + 6, panel(2).Height + " / " + caption(0).Height);
        int fullHeight = panel(0).Height;
        Call(form, "ToggleTableFold", mon, sh, blocks[0]);
        Check("table one folded: its caption alone, its grid hidden, the mark turned", panel(0).Height < fullHeight / 2 && !((DataGridView)Get(blocks[0], "Grid")).Visible &&
              caption(0).Text.StartsWith("▸") && (bool)Get(t1, "Folded") && panel(1).Visible, panel(0).Height + " / " + fullHeight);
        Call(form, "ToggleTableFold", mon, sh, blocks[0]);
        Check("  unfolded again", ((DataGridView)Get(blocks[0], "Grid")).Visible && !(bool)Get(t1, "Folded"));
        // a comment line first among the tables, folded
        tables.Insert(0, Note("First tables"));
        Call(form, "RefreshValues"); Application.DoEvents();
        blocks = ((IList)Get(sh, "Blocks")).Cast<object>().ToList();
        Call(form, "ToggleTableFold", mon, sh, blocks[0]);
        Check("a comment line among the tables folded: the tables under it hidden until the next comment line",
              !panel(1).Visible && !panel(2).Visible && panel(3).Visible && panel(4).Visible && panel(3).Top > panel(0).Bottom, string.Join(",", Enumerable.Range(0, 5).Select(i => panel(i).Visible)));
        lines = ((IEnumerable<string>)Call(MonType, "Serialize", mon)).ToList();
        Check("  kept (view=table|fold=1)", lines.Any(l => l.StartsWith("First tables;") && l.Contains("view=table") && l.Contains("fold=1")), string.Join(" / ", lines.Where(l => l.Contains("view=table"))));

        form.Close();
        Console.WriteLine(fail == 0 ? "\nMONITOR FOLD ALL PASS" : "\nMONITOR FOLD " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
