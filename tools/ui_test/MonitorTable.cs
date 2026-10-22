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

// 22 October: "create a 2-dimensional table view for the structural array data type monitor (or same-
// dimensional multiple array labels): the structure members as the columns (the data type attached to
// the label name as the column header), the lines only the array's quantity - like '<> KESİM ADIMLARI'
// on the Profiles & Operations sheet. A button changes the values shown (calculated with the divisor or
// the pure ones). Columns can be added at the end as new labels of the same array quantity, to watch
// two arrays together (the 'İŞ LİSTESİ' kind)".
class MT {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        d = d ?? "";
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
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
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length >= a.Length &&
                                              m.GetParameters().Skip(a.Length).All(p => p.IsOptional));
        var args = a.Concat(mi.GetParameters().Skip(a.Length).Select(p => p.DefaultValue)).ToArray();
        return mi.Invoke(o is Type ? null : o, args);
    }
    static IList Rows(object sheet) { return (IList)Get(sheet, "Rows"); }
    static object Row(string address, string type, int count, string label) {
        var r = Activator.CreateInstance(RowType);
        Set(r, "Address", address); Set(r, "DataTypeOverride", type); Set(r, "LabelOverride", label);
        Set(r, "ArrayCount", count); Set(r, "Station", 23);
        return r;
    }
    static List<string> Headers(DataGridView g) { return g.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText.Replace("\n", " | ")).ToList(); }
    static string Cell(DataGridView g, int r, int c) { return Convert.ToString(g.Rows[r].Cells[c].Value); }

    /// <summary>The sheet's '<> KESİM ADIMLARI': a 10-word step - a code, two robot positions (xxx.x), an angle and V cut Y (xxx.xx), two clamp bits.</summary>
    static StructuredTypes.Type Cutting()
    {
        var t = new StructuredTypes.Type { Name = "CuttingStep", Size = 10 };
        t.Members.Add(new StructuredTypes.Member { Name = "Code", DataType = "Word [Signed]", Offset = 0 });
        t.Members.Add(new StructuredTypes.Member { Name = "Robot1", DataType = "Word [Signed]", Offset = 1, Divisor = 10, Decimals = 1 });
        t.Members.Add(new StructuredTypes.Member { Name = "Angle", DataType = "Word [Signed]", Offset = 3, Divisor = 100, Decimals = 2 });
        t.Members.Add(new StructuredTypes.Member { Name = "LeftClamp", DataType = "Bit", Offset = 5, Bit = 0 });
        t.Members.Add(new StructuredTypes.Member { Name = "RightClamp", DataType = "Bit", Offset = 5, Bit = 1 });
        return t;
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "mont_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        StructuredTypes.FilePath = Path.Combine(dir, "StructuredTypes.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        StructuredTypes.Save(new[] { Cutting() });

        var sim = new Kaban.Drivers.Simulation.SimulationDriver(23);
        PlcManager.Instance.RegisterDriver(23, sim);
        PlcManager.Instance.RegisterStation(23, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(23, true);
        for (int i = 0; i < 30; i++)
        {
            int b = 25700 + 10 * i;
            for (int k = 0; k < 10; k++) sim.Write("R" + (b + k), 0);
            if (i < 3) { sim.Write("R" + b, i + 1); sim.Write("R" + (b + 1), 1500 + 25 * i); sim.Write("R" + (b + 3), 4500); sim.Write("R" + (b + 5), i == 1 ? 2 : 1); }
        }
        for (int i = 0; i < 1000; i++) { sim.Write("D" + (5000 + i), 0); sim.Write("D" + (6000 + i), 0); }
        for (int i = 0; i < 5; i++) { sim.Write("D" + (5000 + 2 * i), 1001 + i); sim.Write("D" + (6000 + 2 * i), 100 * (i + 1)); }

        var form = new SimulationMonitorForm();
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 23);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        var sh = Call(form, "EnsureSheet", mon);

        Console.WriteLine("--- an array of structures: a line per step, a column per member ---");
        var steps = Row("R25700", "CuttingStep", 30, "CuttingSteps");
        Rows(sh).Insert(Rows(sh).Count - 1, steps);
        Call(form, "RefreshValues"); Application.DoEvents();
        Call(form, "ShowTable", mon, sh, true); Application.DoEvents();
        var g = (DataGridView)Get(sh, "TableGrid");
        var list = (DataGridView)Get(sh, "Grid");
        Check("the tab as a table: the table shown, its list not", g != null && g.Visible && !list.Visible && ((Control)Get(sh, "TableView")).Visible);
        Call(form, "RefreshValues"); Application.DoEvents();
        var heads = Headers(g);
        Check("the columns: #, then each member - its name over its data type", string.Join(", ", heads) ==
              "#, Code | (Word [Signed]), Robot1 | (Word [Signed]), Angle | (Word [Signed]), LeftClamp | (Bit), RightClamp | (Bit)", string.Join(", ", heads));
        Check("  30 lines - the steps, numbered as their labels ([0] ...)", g.RowCount == 30 && Cell(g, 0, 0) == "0" && Cell(g, 29, 0) == "29", g.RowCount.ToString());
        Check("step 1 calculated: code 2, Robot1 1525 ÷ 10 = 152.5, Angle 4500 ÷ 100 = 45.00, the clamps 0 / 1",
              Cell(g, 1, 1) == "2" && Cell(g, 1, 2) == "152.5" && Cell(g, 1, 3) == "45.00" && Cell(g, 1, 4) == "0" && Cell(g, 1, 5) == "1",
              string.Join(" ", Enumerable.Range(1, 5).Select(c => Cell(g, 1, c))));
        Check("  a step never written: 0, 0.0 ...", Cell(g, 10, 2) == "0.0" && Cell(g, 10, 4) == "0");
        Check("  the note: 30 lines, 1 array", ((Label)Get(sh, "TableNote")).Text.StartsWith("30 lines") && ((Label)Get(sh, "TableNote")).Text.Contains("1 array"),
              ((Label)Get(sh, "TableNote")).Text);

        Console.WriteLine("--- the values shown: calculated or the PLC's ---");
        var view = (Control)Get(sh, "TableView");
        Func<string, Button> button = n => view.Controls.Find(n, true).OfType<Button>().First();
        button("tableRaw").PerformClick(); Application.DoEvents();
        Check("PLC values: Robot1 1525, Angle 4500", Cell(g, 1, 2) == "1525" && Cell(g, 1, 3) == "4500" && (bool)Get(sh, "TableRaw"));
        var lines = (List<string>)Call(MonType, "Serialize", mon);
        Check("  kept with the tab: '#table:raw' under its '#sheet:'", lines.IndexOf("#table:raw") == lines.FindIndex(l => l.StartsWith("#sheet:")) + 1, string.Join(" / ", lines.Take(8)));
        button("tableCalc").PerformClick(); Application.DoEvents();
        Check("  back to calculated", Cell(g, 1, 2) == "152.5" && !(bool)Get(sh, "TableRaw"));

        Console.WriteLine("--- typed into a cell ---");
        int v;
        g.Rows[2].Cells[2].Value = "160.4";
        sim.TryRead("R25721", out v);
        Check("160.4 into step 2's Robot1 (shown calculated): 1604 written", v == 1604, v.ToString());
        button("tableRaw").PerformClick();
        g.Rows[2].Cells[3].Value = "9000";
        sim.TryRead("R25723", out v);
        Check("  9000 into its Angle (shown as the PLC's): 9000 written", v == 9000, v.ToString());
        g.Rows[2].Cells[5].Value = "1";
        sim.TryRead("R25725", out v);
        Check("  its RightClamp (R25725.1) set: the word's other bit kept", v == 3, v.ToString());
        button("tableCalc").PerformClick();
        Call(form, "RefreshValues"); Application.DoEvents();
        Check("  shown at once", Cell(g, 2, 2) == "160.4" && Cell(g, 2, 3) == "90.00", Cell(g, 2, 2) + " " + Cell(g, 2, 3));
        var begin = new DataGridViewCellCancelEventArgs(0, 1);
        typeof(DataGridView).GetMethod("OnCellBeginEdit", Any).Invoke(g, new object[] { begin });
        Check("  the # column is not typed into", begin.Cancel);

        Console.WriteLine("--- an array of the same length added: its columns at the end ---");
        var job1 = Row("D5000", "Double Word [Signed]", 30, "Operation1");
        Call(form, "AddToTable", mon, sh, job1); Application.DoEvents();
        Call(form, "RefreshValues"); Application.DoEvents();
        heads = Headers(g);
        Check("Operation1's column after the steps' members; the members named with their array now",
              heads.Count == 7 && heads[6] == "Operation1 | (Double Word [Signed])" && heads[1] == "CuttingSteps.Code | (Word [Signed])", string.Join(", ", heads));
        Check("  the same index together: step 3 and Operation1[3] = 1004", Cell(g, 3, 6) == "1004", Cell(g, 3, 6));
        var other = Row("D6000", "Word [Signed]", 20, "Short");
        Call(form, "AddToTable", mon, sh, other); Application.DoEvents();
        Call(form, "RefreshValues"); Application.DoEvents();
        Check("an array of 20 (not 30): in the tab's list, not in the table - named under it",
              Headers(g).Count == 7 && ((Label)Get(sh, "TableNote")).Text.Contains("Short [20]") && Rows(sh).Contains(other), ((Label)Get(sh, "TableNote")).Text);
        Call(form, "RemoveFromTable", mon, sh, other);
        Check("  taken off again", !Rows(sh).Contains(other) && !((Label)Get(sh, "TableNote")).Text.Contains("Short"));

        Console.WriteLine("--- what Add an array... offers ---");
        var sh2 = Call(form, "AddSheet", mon, "Jobs", 1);
        var job2 = Row("D6000", "Double Word [Signed]", 30, "Operation2");
        var other2 = Row("D7000", "Word [Signed]", 12, "Twelve");
        Rows(sh2).Insert(0, job2); Rows(sh2).Insert(1, other2);
        Call(form, "Render", mon, sh2);
        File.WriteAllLines(Path.Combine(dir, "labels.csv"), new[] { "Label;DataType;Address", "Kesim;CuttingStep(0..29);R26100", "Ten;CuttingStep(0..9);R27000" });
        PlcLabelTable.LoadStation(Path.Combine(dir, "labels.csv"), 23);
        var tabs = (TabControl)Get(mon, "SheetTabs");
        tabs.SelectedIndex = 0; Application.DoEvents();
        var menu = (ContextMenuStrip)Call(form, "TableAddMenu", mon, sh);
        var texts = menu.Items.Cast<ToolStripItem>().Select(i => i.Text).ToList();
        Check("arrays of 30: Operation2 (another tab), Kesim (the label list) - not Twelve, not Ten, not those in the table; a new one",
              texts[0] == "Arrays of 30:" && texts.Any(t => t.StartsWith("Operation2") && t.Contains("(Jobs)")) && texts.Any(t => t.StartsWith("Kesim") && t.Contains("label list")) &&
              !texts.Any(t => t.StartsWith("Twelve") || t.StartsWith("Ten") || t.StartsWith("Operation1")) && texts.Last() == "New array of 30...",
              string.Join(" | ", texts));
        menu.Items.Cast<ToolStripItem>().First(i => i.Text.StartsWith("Operation2")).PerformClick();
        Call(form, "RefreshValues"); Application.DoEvents();
        Check("  Operation2 chosen: its column at the end, Operation2[2] = 300", Headers(g).Count == 8 && Headers(g)[7].StartsWith("Operation2") && Cell(g, 2, 7) == "300",
              string.Join(", ", Headers(g)));

        Console.WriteLine("--- list and table, undone, kept ---");
        Call(form, "ShowTable", mon, sh, false); Application.DoEvents();
        Check("the tab as a list again: its arrays its rows", list.Visible && !view.Visible && !(bool)Get(sh, "IsTable"));
        Call(form, "Undo"); Application.DoEvents();
        var shNow = ((IList)Get(mon, "Sheets"))[0];
        Check("  undone: a table again (the tab made anew)", (bool)Get(shNow, "IsTable") && ((DataGridView)Get(shNow, "TableGrid")).Visible);
        lines = (List<string>)Call(MonType, "Serialize", mon);
        var mon2 = Activator.CreateInstance(MonType.GetNestedType("MonitorTab", Any));
        Call(MonType, "Deserialize", mon2, lines);
        var back = ((IList)Get(mon2, "Sheets")).Cast<object>().ToList();
        Check("read back: the first tab a table of its 3 arrays, 'Jobs' a list", (bool)Get(back[0], "IsTable") && !(bool)Get(back[1], "IsTable") &&
              Rows(back[0]).Cast<object>().Count(r => (bool)Get(r, "IsArray")) == 3);

        Console.WriteLine("--- an array shown as a table from its row ---");
        tabs.SelectedIndex = 1; Application.DoEvents();
        var jobs = ((IList)Get(mon, "Sheets"))[1];
        var row2 = Rows(jobs).Cast<object>().First(r => (string)Get(r, "Address") == "D7000");
        var made = Call(form, "OpenAsTable", mon, row2);
        Check("a new tab named after it, shown as a table of it, chosen", made != null && (string)Get(made, "Name") == "Twelve" && (bool)Get(made, "IsTable") &&
              ((IList)Get(mon, "Sheets")).Count == 3 && Get(mon, "Current") == made);
        Call(form, "RefreshValues"); Application.DoEvents();
        var g3 = (DataGridView)Get(made, "TableGrid");
        Check("  12 lines, one column: Twelve (Word [Signed])", g3.RowCount == 12 && Headers(g3).Count == 2 && Headers(g3)[1] == "Twelve | (Word [Signed])", string.Join(", ", Headers(g3)));
        Check("  the list of Jobs as it was", Rows(jobs).Contains(row2));

        Console.WriteLine("--- the simulator's own devices: into a tab shown as a list ---");
        sim.Write("M999", 1);
        Call(form, "RefreshValues"); Application.DoEvents();
        var all = ((IList)Get(mon, "Sheets")).Cast<object>().ToList();
        Check("M999 appended to Jobs (the first list), not to the table before it",
              Rows(all[1]).Cast<object>().Any(r => (string)Get(r, "Address") == "M999") && !Rows(all[0]).Cast<object>().Any(r => (string)Get(r, "Address") == "M999"));

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "MONITOR TABLE ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
