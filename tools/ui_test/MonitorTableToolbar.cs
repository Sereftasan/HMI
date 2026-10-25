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

// 24 October: "The labels in 'Table viewed labels' tab doesn't move up/down on 'PLC Monitor' page for some reason and
// both of these up/down button look enabled even when the first or last label selected on the tab. Other buttons at
// this page functions on 'List viewed labels' tab as well actually, this tab doesn't function very well. It should be
// possible to add comment or another table viewed label in between before/after any label on the tab. Also the label
// addition page which opens with 'Add an array' button should disable 'Arrays (how' value edit because this value has
// to be just same as the label to be attached. The rest of '(how' part in 'Arrays (how' is not seen btw"
class MTT {
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
    /// <summary>The tables' tab's items as shown: a table's caption, "# text" for a comment line.</summary>
    static string Shown(object sheet) {
        return string.Join(" / ", Blocks(sheet).Select(b => (bool)Get(b, "IsComment") ? "# " + ((Label)Get(b, "Caption")).Text : ((Label)Get(b, "Caption")).Text));
    }
    static void Pump() { for (int i = 0; i < 5; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "mtt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        StructuredTypes.FilePath = Path.Combine(dir, "StructuredTypes.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(23);
        PlcManager.Instance.RegisterDriver(23, sim);
        PlcManager.Instance.RegisterStation(23, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(23, true);

        var form = new SimulationMonitorForm { Width = 1200, Height = 900 };
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 23);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        var sh = Call(form, "EnsureSheet", mon);
        var list = (DataGridView)Get(sh, "Grid");
        var up = (Button)Get(form, "_btnUp");
        var down = (Button)Get(form, "_btnDown");

        Console.WriteLine("--- the list: up / down as the rows chosen allow ---");
        var lamp = Row("M5", "Bit", 1, "Lamp");
        var other = Row("D7000", "Word [Signed]", 5, "Other array");
        var first = Row("D5000", "Word [Signed]", 5, "Speeds");
        var second = Row("D6000", "Word [Signed]", 5, "Times");
        foreach (var r in new[] { lamp, other, first, second }) Rows(sh).Insert(Rows(sh).Count - 1, r);
        Call(form, "RefreshValues"); Pump();
        Call(MonType, "Select", sh, new[] { Rows(sh).IndexOf(lamp) });
        Call(form, "UpdateToolbar");
        Check("the first row chosen: up off, down on", !up.Enabled && down.Enabled);
        Call(MonType, "Select", sh, new[] { Rows(sh).IndexOf(second) });
        Call(form, "UpdateToolbar");
        Check("  the last row chosen: up on, down off", up.Enabled && !down.Enabled);
        list.ClearSelection(); list.CurrentCell = null;
        Call(form, "UpdateToolbar");
        Check("  none chosen: both off", !up.Enabled && !down.Enabled);

        Console.WriteLine("--- the tables' tab: the toolbar acts on the table chosen ---");
        Call(form, "ShowInTable", mon, sh, first, 0);
        Call(form, "ShowInTable", mon, sh, second, 0);
        Pump();
        var views = (TabControl)Get(sh, "ViewTabs");
        views.SelectedTab = (TabPage)Get(sh, "TablesPage");
        Pump();
        Check("two tables, the tables' tab in sight", (bool)Call(MonType, "TablesShown", sh) && Shown(sh) == "Speeds / Times", Shown(sh));
        Call(form, "SelectTable", sh, first);
        Check("  the first table chosen: up off, down on; its caption marked", !up.Enabled && down.Enabled &&
              ((Label)Get(Blocks(sh)[0], "Caption")).BackColor != ((Label)Get(Blocks(sh)[1], "Caption")).BackColor);
        Call(form, "SelectTable", sh, second);
        Check("  the last chosen: up on, down off", up.Enabled && !down.Enabled);
        Call(form, "MoveSelected", -1);
        Pump();
        Check("the toolbar's up: the table moved up (it did nothing)", Shown(sh) == "Times / Speeds" && !up.Enabled && down.Enabled, Shown(sh));
        Call(form, "MoveSelected", +1);
        Pump();
        Check("  down: back", Shown(sh) == "Speeds / Times", Shown(sh));

        Console.WriteLine("--- a comment line or a table between them ---");
        Call(form, "SelectTable", sh, first);
        var addMenu = (ContextMenuStrip)Call(form, "AddMenu");
        var addTexts = addMenu.Items.OfType<ToolStripMenuItem>().Select(i => i.Text + (i.Enabled ? "" : " (off)")).ToList();
        Check("the Add button's menu on the tables' tab: a table or a comment line above / below the chosen one, at the end; a list's label as a table",
              addTexts.Contains("New table above the chosen one...") && addTexts.Contains("New table below the chosen one...") &&
              addTexts.Contains("Comment line below the chosen one...") && addTexts.Contains("A label of this tab's list as a table"), string.Join(" | ", addTexts));
        Call(form, "AddTableItem", mon, sh, +1, true, "Section B", "", "", 1, "", 16, 1.0, 0);
        Pump();
        Check("  a comment line below the first table", Shown(sh) == "Speeds / # Section B / Times", Shown(sh));
        Call(form, "AddTableItem", mon, sh, -1, false, "Positions", "D8000", "Word [Signed]", 4, "", 16, 1.0, 0);
        Pump();
        Check("  a new table above the comment line (the one chosen)", Shown(sh) == "Speeds / Positions / # Section B / Times", Shown(sh));
        Call(form, "AddTableItem", mon, sh, +1, false, "One", "D8100", "Word [Signed]", 1, "", 16, 1.0, 0);
        Pump();
        Check("  a single device typed: into the list, not a table", Shown(sh) == "Speeds / Positions / # Section B / Times" &&
              Rows(sh).Cast<object>().Any(r => (string)Get(r, "Address") == "D8100"));
        var comment = TableRows(sh).Cast<object>().First(r => (string)Get(r, "Comment") == "Section B");
        Call(form, "SelectTable", sh, comment);
        Call(form, "MoveSelected", -1);
        Pump();
        Check("a comment line moves too", Shown(sh) == "Speeds / # Section B / Positions / Times", Shown(sh));
        Call(form, "ShowInTable", mon, sh, other, +1);
        Pump();
        Check("  a label of the list as a table below the chosen one", Shown(sh) == "Speeds / # Section B / Other array / Positions / Times", Shown(sh));

        Console.WriteLine("--- copy, paste, delete ---");
        Call(form, "SelectTable", sh, first);
        Call(form, "CopySelected");
        Call(form, "SelectTable", sh, second);
        Call(form, "PasteTables", mon, sh, +1);
        Pump();
        Check("the chosen table copied, pasted below another", Shown(sh) == "Speeds / # Section B / Other array / Positions / Times / Speeds", Shown(sh));
        var del = (ContextMenuStrip)Call(form, "DeleteMenu");
        Check("  the delete button's first item: the table chosen", del.Items[0].Text.StartsWith("The table 'Speeds'"), del.Items[0].Text);
        Call(form, "RemoveSelected");
        Pump();
        Check("  deleted", Shown(sh) == "Speeds / # Section B / Other array / Positions / Times", Shown(sh));
        Call(form, "Undo");
        Pump();
        sh = Get(mon, "Current");                         // an undo makes the tabs again
        Call(form, "RefreshValues"); Pump();
        Check("  Ctrl+Z: back", ((IList)Call(MonType, "TableItems", sh)).Count == 6, ((IList)Call(MonType, "TableItems", sh)).Count + " items");

        Console.WriteLine("--- kept ---");
        var lines = (List<string>)Call(MonType, "Serialize", mon);
        var again = Activator.CreateInstance(MonType.GetNestedType("MonitorTab", Any));
        Call(MonType, "Deserialize", again, lines);
        var sheet2 = ((IList)Get(again, "Sheets")).Cast<object>().First();
        var items2 = (IList)Call(MonType, "TableItems", sheet2);
        var names2 = items2.Cast<IList>().Select(i => { var r = i[0]; return (bool)Get(r, "IsComment") ? "# " + Get(r, "Comment") : (string)Get(r, "LabelOverride"); }).ToList();
        Check("the comment line between the tables kept in its place", string.Join(" / ", names2) == "Speeds / # Section B / Other array / Positions / Times / Speeds",
              string.Join(" / ", names2));

        // 25 October: "The PLC monitor table blinks sometimes for some reason when the scroll is moved to the right end of the table"
        Console.WriteLine("--- drawn again only when a value in sight changes ---");
        ((TabControl)Get(sh, "ViewTabs")).SelectedTab = (TabPage)Get(sh, "TablesPage");
        Pump();
        var block = Blocks(sh).First(b => !(bool)Get(b, "IsComment"));
        var tg = (DataGridView)Get(block, "Grid");
        bool buffered = (bool)typeof(Control).GetProperty("DoubleBuffered", Any).GetValue(tg, null);
        int drawn = 0;
        tg.Invalidated += (s, e) => drawn++;
        Call(form, "RefreshValues"); Pump();
        drawn = 0;
        Call(form, "RefreshValues"); Pump();
        Call(form, "RefreshValues"); Pump();
        Check("refreshes that change nothing: the table not drawn again (it was, 4 times a second); drawn into a buffer", drawn == 0 && buffered, drawn + " times");
        var col = (System.Collections.IList)((IList)Call(MonType, "TableItems", sh)).Cast<IList>().First(i => i[0] == Get(block, "Head"));
        sim.Write((string)Get(col[0], "Address"), 4321);
        Call(form, "RefreshValues"); Pump();
        Call(form, "RefreshValues"); Pump();
        Check("  a value in sight changed: drawn", drawn > 0, drawn + " times; " + Get(col[0], "Address") + " shown " + Convert.ToString(tg.Rows[0].Cells[1].Value) +
              ", the tables on show: " + Call(MonType, "TablesShown", sh) + ", grid visible " + tg.Visible + " " + tg.Width + "x" + tg.Height);

        Console.WriteLine("--- the device editor of 'Add an array...' ---");
        var dlg = new PlcRowEditorForm("Bench", "", "Word [Signed]", "", "", "Address", isNew: true, arrayCount: 5, station: 23, lockArrayCount: true);
        dlg.Show(); Application.DoEvents();
        var count = (NumericUpDown)typeof(PlcRowEditorForm).GetField("_count", Any).GetValue(dlg);
        var caption = dlg.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "Array (how many):");
        int needed = caption == null ? 0 : TextRenderer.MeasureText(caption.Text, caption.Font).Width;
        Check("its count the table's, not to be changed; no comment line", count.Value == 5 && !count.Enabled &&
              !dlg.Controls.OfType<CheckBox>().Any(c => c.Visible), count.Value + (count.Enabled ? " on" : " off"));
        Check("  'Array (how many):' seen whole", caption != null && caption.Width >= needed && caption.Right < count.Left, caption == null ? "none" : caption.Width + " >= " + needed);
        dlg.Close();
        var free = new PlcRowEditorForm("Bench", "", "Word [Signed]", "", "", "Address", isNew: true, arrayCount: 5, station: 23);
        var count2 = (NumericUpDown)typeof(PlcRowEditorForm).GetField("_count", Any).GetValue(free);
        Check("  elsewhere: the count typed in", count2.Enabled);
        free.Dispose();

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nMONITOR TABLE TOOLBAR ALL PASS" : "\nMONITOR TABLE TOOLBAR " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
