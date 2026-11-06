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

// 22 October: "a 2-dimensional table view for the structural array data type monitor (or same-dimensional
// multiple array labels): the structure members as the columns (the data type with the label name as the
// column header), the lines the array's quantity - like '<> KESİM ADIMLARI'. A button changes the values
// shown. Labels of the same quantity added as columns at the end (the 'İŞ LİSTESİ' kind)."
// And that evening: "make the table view and list view label based: 'Show this label in table view' moves
// the label on top of the tab as a table - back to list view, it gets back to the list at the bottom. The
// tables always at the top (their order changed with up / down), the list always at the bottom. The label's
// description the table's caption, the view buttons under it, the table under them."
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
                                              m.GetParameters().Skip(a.Length).All(p => p.IsOptional) &&
                                              m.GetParameters().Take(a.Length).Select((p, i) => a[i] == null || p.ParameterType.IsInstanceOfType(a[i])).All(x => x));
        var args = a.Concat(mi.GetParameters().Skip(a.Length).Select(p => p.DefaultValue)).ToArray();
        return mi.Invoke(o is Type ? null : o, args);
    }
    static IList Rows(object sheet) { return (IList)Get(sheet, "Rows"); }
    static IList TableRows(object sheet) { return Rows(Get(sheet, "Tables")); }
    static object Row(string address, string type, int count, string label) {
        var r = Activator.CreateInstance(RowType);
        Set(r, "Address", address); Set(r, "DataTypeOverride", type); Set(r, "LabelOverride", label);
        Set(r, "ArrayCount", count); Set(r, "Station", 23);
        return r;
    }
    static List<object> Blocks(object sheet) { return ((IList)Get(sheet, "Blocks")).Cast<object>().ToList(); }
    static DataGridView GridOf(object block) { return (DataGridView)Get(block, "Grid"); }
    static string Caption(object block) { return (string)Get(block, "Title"); }   // (its caption without the fold mark)
    static Button Btn(object block, string n) { return (Button)Get(block, n); }
    static List<string> Headers(DataGridView g) { return g.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText.Replace("\n", " | ")).ToList(); }
    static string Cell(DataGridView g, int r, int c) { return Convert.ToString(g.Rows[r].Cells[c].Value); }

    /// <summary>The sheet's '<> KESİM ADIMLARI': a 10-word step - a code, two robot positions, an angle (xxx.xx), two clamp bits.</summary>
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
        for (int i = 0; i < 100; i++) { sim.Write("D" + (5000 + i), 0); sim.Write("D" + (6000 + i), 0); sim.Write("D" + (7000 + i), 0); }
        for (int i = 0; i < 5; i++) { sim.Write("D" + (5000 + 2 * i), 1001 + i); sim.Write("D" + (6000 + 2 * i), 100 * (i + 1)); }
        sim.Write("M5", 1);

        var form = new SimulationMonitorForm { Width = 1200, Height = 900 };
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 23);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        var sh = Call(form, "EnsureSheet", mon);
        var list = (DataGridView)Get(sh, "Grid");

        Console.WriteLine("--- a label of the list shown as a table ---");
        var steps = Row("R25700", "CuttingStep", 30, "Cutting Steps");
        var single = Row("M5", "Bit", 1, "Lamp");
        Rows(sh).Insert(Rows(sh).Count - 1, single);
        Rows(sh).Insert(Rows(sh).Count - 1, steps);
        Call(form, "RefreshValues"); Application.DoEvents();
        var page = (TabPage)Get(sh, "Page");
        var views = (TabControl)Get(sh, "ViewTabs");
        var host = (Control)Get(sh, "TablesHost");
        Check("a tab of list labels only: the list on the tab itself, no tabs under it", list.Parent == page && views.Parent == null);
        var cms = list.ContextMenuStrip;
        Call(MonType, "Select", sh, new[] { Rows(sh).IndexOf(steps) });
        list.CurrentCell = list.Rows[Rows(sh).IndexOf(steps)].Cells[0];
        typeof(ToolStripDropDown).GetMethod("OnOpening", Any).Invoke(cms, new object[] { new System.ComponentModel.CancelEventArgs() });
        var item = cms.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Text == "Show this label in table view");
        Check("an array label's menu: 'Show this label in table view'", item != null, string.Join(" | ", cms.Items.OfType<ToolStripItem>().Select(i => i.Text)));
        Call(MonType, "Select", sh, new[] { Rows(sh).IndexOf(single) });
        list.CurrentCell = list.Rows[Rows(sh).IndexOf(single)].Cells[0];
        typeof(ToolStripDropDown).GetMethod("OnOpening", Any).Invoke(cms, new object[] { new System.ComponentModel.CancelEventArgs() });
        // 24 October: "The disabled dropdown menu items should still be seen on the menu as disabled instead of removed"
        var greyed = cms.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Text == "Show this label in table view");
        var expand = cms.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Text == "Expand the array or structure");
        Check("  a single label's: there, greyed, saying why - Expand too", greyed != null && !greyed.Enabled && greyed.ToolTipText.StartsWith("An array label's") &&
              expand != null && !expand.Enabled && expand.ToolTipText.Length > 0 && cms.ShowItemToolTips,
              string.Join(" | ", cms.Items.OfType<ToolStripItem>().Select(i => i.Text + (i.Enabled ? "" : " (off)"))));
        item.PerformClick();
        Call(form, "RefreshValues"); Application.DoEvents();
        var blocks = Blocks(sh);
        // 22 October: "the table viewed on top and the list viewed in the bottom don't fit on the page so well - when a
        // tab has both, two tabs under it: 'List viewed labels' first, 'Table viewed labels' second; removed when either gets empty"
        Check("the label off the list into a table: the tab has both now - two tabs under it, the list's first, the tables' second",
              !Rows(sh).Contains(steps) && TableRows(sh).Contains(steps) && Rows(sh).Contains(single) && blocks.Count == 1 &&
              views.Parent == page && views.TabCount == 2 && views.TabPages[0].Text == "List viewed labels" && views.TabPages[1].Text == "Table viewed labels" &&
              list.Parent == views.TabPages[0] && host.Parent == views.TabPages[1],
              blocks.Count + " " + (views.Parent == null ? "-" : views.Parent.Name) + " " + string.Join(",", views.TabPages.Cast<TabPage>().Select(t => t.Text)));
        Check("  the tables' tab shown (where the label went); the tables fill it", views.SelectedTab == views.TabPages[1] && host.Dock == DockStyle.Fill);
        list.Rows[Rows(sh).IndexOf(single)].Selected = true;
        Check("  while it is shown no list row is acted on unseen (a Delete, a paste): the Lamp selected in the list counts as none",
              ((IList)Call(MonType, "SelectedIndices", sh)).Count == 0);
        views.SelectedTab = views.TabPages[0]; Application.DoEvents();
        Check("  the list's tab chosen: it counts", ((IList)Call(MonType, "SelectedIndices", sh)).Count == 1);
        views.SelectedTab = views.TabPages[1]; Application.DoEvents();
        Call(MonType, "Select", sh, new[] { Rows(sh).IndexOf(single) });
        Check("  a list row asked for (Find, a paste, one added): its tab shown", views.SelectedTab == views.TabPages[0]);
        views.SelectedTab = views.TabPages[1]; Application.DoEvents();
        var b0 = blocks[0];
        var g = GridOf(b0);
        var cap = (Label)Get(b0, "Caption");
        Check("  the caption as tall as its font needs (not cut)", cap.Height >= TextRenderer.MeasureText("Xg", cap.Font).Height + 6 && cap.Top >= 0,
              cap.Height + " " + TextRenderer.MeasureText("Xg", cap.Font).Height);
        Check("  a table alone fills the tab (more of its 30 lines in sight than 10)",
              ((Control)Get(b0, "Panel")).Height >= host.Height - 10 && g.DisplayedRowCount(false) > 10,
              ((Control)Get(b0, "Panel")).Height + " / " + host.Height + " - " + g.DisplayedRowCount(false) + " lines; row " + g.RowTemplate.Height + "/" + g.Rows[0].Height +
              " head " + g.ColumnHeadersHeight + " cap " + cap.Height + " bar " + ((Control)Get(b0, "Bar")).Height + " grid " + g.Height + " " + g.Bounds);
        Check("  its caption the label's description, the view buttons under it, the table under them",
              Caption(b0) == "Cutting Steps" && ((Label)Get(b0, "Caption")).Bottom <= Btn(b0, "ShowHmi").Parent.Top && Btn(b0, "ShowHmi").Parent.Bottom <= g.Top &&
              Btn(b0, "ShowHmi").Text == "Value (HMI)" && Btn(b0, "ShowPlc").Text == "Value (PLC)", Caption(b0));
        var heads = Headers(g);
        Check("the columns: #, then each member - its name over its data type", string.Join(", ", heads) ==
              "#, Code | (Word [Signed]), Robot1 | (Word [Signed]), Angle | (Word [Signed]), LeftClamp | (Bit), RightClamp | (Bit)", string.Join(", ", heads));
        Check("  30 lines, numbered as the labels' elements", g.RowCount == 30 && Cell(g, 0, 0) == "0" && Cell(g, 29, 0) == "29");
        Check("step 1 as the HMI shows it: code 2, Robot1 152.5, Angle 45.00, the clamps 0 / 1",
              Cell(g, 1, 1) == "2" && Cell(g, 1, 2) == "152.5" && Cell(g, 1, 3) == "45.00" && Cell(g, 1, 4) == "0" && Cell(g, 1, 5) == "1",
              string.Join(" ", Enumerable.Range(1, 5).Select(c => Cell(g, 1, c))));
        Btn(b0, "ShowPlc").PerformClick();
        Check("Value (PLC): Robot1 1525, Angle 4500 - kept with the label", Cell(g, 1, 2) == "1525" && Cell(g, 1, 3) == "4500" && (bool)Get(steps, "TableRaw"));
        var lines = (List<string>)Call(MonType, "Serialize", mon);
        Check("  kept: 'view=table', 'vals=plc' in its extras, before the list's rows",
              lines.FindIndex(l => l.Contains(";R25700;") && l.Contains("view=table") && l.Contains("vals=plc")) < lines.FindIndex(l => l.Contains(";M5")) &&
              lines.Any(l => l.Contains(";R25700;") && l.Contains("view=table")), string.Join(" / ", lines.Where(l => l.Contains(";R2") || l.Contains(";M5"))));
        Btn(b0, "ShowHmi").PerformClick();

        Console.WriteLine("--- typed into a cell ---");
        int v;
        g.Rows[2].Cells[2].Value = "160.4";
        sim.TryRead("R25721", out v);
        Check("160.4 into step 2's Robot1 (shown as the HMI shows it): 1604 written", v == 1604, v.ToString());
        g.Rows[2].Cells[5].Value = "1";
        sim.TryRead("R25725", out v);
        Check("  its RightClamp (R25725.1) set, the word's other bit kept", v == 3, v.ToString());

        Console.WriteLine("--- labels of the same length joined to a table ---");
        var job1 = Row("D5000", "Double Word [Signed]", 30, "Operation 1");
        var job2 = Row("D6000", "Double Word [Signed]", 30, "Operation 2");
        var shortOne = Row("D7000", "Word [Signed]", 20, "Short");
        foreach (var r in new[] { job1, job2, shortOne }) Rows(sh).Insert(Rows(sh).Count - 1, r);
        Call(form, "Render", mon, sh);
        Call(form, "ShowInTable", mon, sh, job1);
        Call(form, "RefreshValues"); Application.DoEvents();
        blocks = Blocks(sh);
        var b1 = blocks[1];
        var menu = (ContextMenuStrip)Call(form, "TableAddMenu", mon, sh, job1);
        var texts = menu.Items.Cast<ToolStripItem>().Select(i => i.Text).ToList();
        Check("Add an array...: the labels of 30 elements in this tab's list (not Short's 20), a new one",
              texts[0] == "Labels of 30 elements:" && texts.Any(t => t.StartsWith("Operation 2") && t.Contains("this tab's list")) && !texts.Any(t => t.StartsWith("Short")) &&
              texts.Last() == "New array of 30...", string.Join(" | ", texts));
        menu.Items.Cast<ToolStripItem>().First(i => i.Text.StartsWith("Operation 2")).PerformClick();
        Call(form, "RefreshValues"); Application.DoEvents();
        blocks = Blocks(sh);
        b1 = blocks[1];
        var g1 = GridOf(b1);
        Check("Operation 2 moved from the list into Operation 1's table: its column at the end, the same index together",
              !Rows(sh).Contains(job2) && (bool)Get(job2, "Joined") && Caption(b1) == "Operation 1  +  Operation 2" &&
              string.Join(", ", Headers(g1)) == "#, Operation 1 | (Double Word [Signed]), Operation 2 | (Double Word [Signed])" && Cell(g1, 3, 1) == "1004" && Cell(g1, 3, 2) == "400",
              Caption(b1) + " / " + string.Join(", ", Headers(g1)));
        Call(form, "JoinTable", mon, sh, job1, shortOne);
        Check("  a label of another length: kept in the list, not in the table", Rows(sh).Contains(shortOne) && !(bool)Get(shortOne, "Joined"));

        Console.WriteLine("--- the tables' order, up and down ---");
        Check("the first table's Up off, the last's Down off", !Btn(blocks[0], "Up").Enabled && Btn(blocks[0], "Down").Enabled &&
              Btn(blocks[1], "Up").Enabled && !Btn(blocks[1], "Down").Enabled);
        Btn(blocks[1], "Up").PerformClick();
        Call(form, "RefreshValues"); Application.DoEvents();
        blocks = Blocks(sh);
        Check("Up: Operation 1's table first, its joined label with it", Caption(blocks[0]) == "Operation 1  +  Operation 2" && Caption(blocks[1]) == "Cutting Steps" &&
              ((Control)Get(blocks[0], "Panel")).Top < ((Control)Get(blocks[1], "Panel")).Top,
              string.Join(" / ", blocks.Select(Caption)));

        Console.WriteLine("--- back to the list ---");
        Call(form, "TakeOffTable", mon, sh, job1);
        Application.DoEvents();
        blocks = Blocks(sh);
        // (checked before the simulator's own devices are appended to the list by a refresh)
        Check("Operation 1 taken off its table: at the list's end; Operation 2 leads the table now", Rows(sh).Contains(job1) && Caption(blocks[0]) == "Operation 2" &&
              !(bool)Get(job2, "Joined") && LastItem(sh) == job1, string.Join(" / ", blocks.Select(Caption)));
        Btn(blocks[1], "ToList").PerformClick();
        Application.DoEvents();
        Check("List view: Cutting Steps at the end of the list again - the list's tab shown, where it went",
              Rows(sh).Contains(steps) && !TableRaw(sh, steps) && Blocks(sh).Count == 1 && LastItem(sh) == steps && views.SelectedTab == views.TabPages[0],
              Blocks(sh).Count.ToString());
        Call(form, "Undo"); Application.DoEvents();
        var shNow = ((IList)Get(mon, "Sheets"))[0];
        Check("undone: its table again", Blocks(shNow).Count == 2 && TableRows(shNow).Cast<object>().Any(r => (string)Get(r, "Address") == "R25700"));

        Console.WriteLine("--- a tab with one kind only: no tabs under it ---");
        var shL = ((IList)Get(mon, "Sheets"))[0];
        var keep = Rows(shL).Cast<object>().Where(r => !(bool)Get(r, "IsBlank")).ToList();
        foreach (var r in keep) Rows(shL).Remove(r);
        Call(form, "Render", mon, shL);          // (no refresh between: the simulator's devices would be appended to the list)
        var viewsL = (TabControl)Get(shL, "ViewTabs");
        var pageL = (TabPage)Get(shL, "Page");
        Check("the list emptied: the tabs under the tab gone, the tables on the tab itself", viewsL.Parent == null && ((Control)Get(shL, "TablesHost")).Parent == pageL &&
              Blocks(shL).Count == 2);
        Rows(shL).Insert(Rows(shL).Count - 1, keep[0]);
        Call(form, "Render", mon, shL); Application.DoEvents();
        Check("  a label in the list again: the two tabs back, the list's shown (where it came)",
              viewsL.Parent == pageL && viewsL.SelectedTab == viewsL.TabPages[0] && ((Control)Get(shL, "Grid")).Parent == viewsL.TabPages[0]);
        foreach (var r in keep.Skip(1)) Rows(shL).Insert(Rows(shL).Count - 1, r);
        Call(form, "Render", mon, shL); Application.DoEvents();

        Console.WriteLine("--- kept and read back; a tab of before shown whole as a table ---");
        lines = (List<string>)Call(MonType, "Serialize", mon);
        var mon2 = Activator.CreateInstance(MonType.GetNestedType("MonitorTab", Any));
        Call(MonType, "Deserialize", mon2, lines);
        var back = ((IList)Get(mon2, "Sheets"))[0];
        Check("read back: the tables' labels in Tables, in their order; the list's in Rows",
              string.Join(",", TableRows(back).Cast<object>().Select(r => (string)Get(r, "Address"))) == "D6000,R25700" &&
              Rows(back).Cast<object>().Any(r => (string)Get(r, "Address") == "D5000"),
              string.Join(",", TableRows(back).Cast<object>().Select(r => (string)Get(r, "Address"))));
        var mon3 = Activator.CreateInstance(MonType.GetNestedType("MonitorTab", Any));
        Call(MonType, "Deserialize", mon3, new List<string> { "#sheet:Jobs", "#table:raw", "Label;DataType;Address;Value",
            "A;Double Word [Signed];D5000;;n=30", "B;Double Word [Signed];D6000;;n=30", "C;Word [Signed];D7000;;n=20", "Lamp;Bit;M5;" });
        var old = ((IList)Get(mon3, "Sheets"))[0];
        Check("a tab of before ('#table:'): its arrays of the first's length one table (B joined), the rest its list",
              string.Join(",", TableRows(old).Cast<object>().Select(r => (string)Get(r, "Address") + ((bool)Get(r, "Joined") ? "+" : ""))) == "D5000,D6000+" &&
              (bool)Get(TableRows(old)[0], "TableRaw") && Rows(old).Count == 2,
              string.Join(",", TableRows(old).Cast<object>().Select(r => (string)Get(r, "Address"))));

        Console.WriteLine("--- the simulator's own devices: into the list ---");
        sim.Write("M999", 1);
        Call(form, "RefreshValues"); Application.DoEvents();
        shNow = ((IList)Get(mon, "Sheets"))[0];
        Check("M999 appended to the list", Rows(shNow).Cast<object>().Any(r => (string)Get(r, "Address") == "M999") &&
              !TableRows(shNow).Cast<object>().Any(r => (string)Get(r, "Address") == "M999"));

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "MONITOR TABLE ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    static bool TableRaw(object sheet, object row) { return TableRows(sheet).Contains(row); }
    /// <summary>The list's last label (its elements and the empty line after it left out).</summary>
    static object LastItem(object sheet) { return Rows(sheet).Cast<object>().LastOrDefault(r => !(bool)Get(r, "IsElement") && !(bool)Get(r, "IsBlank")); }
}
