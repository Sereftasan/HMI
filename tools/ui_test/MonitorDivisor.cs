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

// 22 October: "add Divisor and DecimalPlaces properties to the labels and show the value in both units
// (the value in the PLC address as it is and the value calculated with the divisor, with the entered
// decimal quantity) - the user could filter it out if they don't want to see it".
class MD {
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
    static object Row(string address, string type, int count = 1, string label = "Pos") {
        var r = Activator.CreateInstance(RowType);
        Set(r, "Address", address); Set(r, "DataTypeOverride", type); Set(r, "LabelOverride", label);
        Set(r, "ArrayCount", count); Set(r, "Station", 23);
        return r;
    }
    static string Calc(object mon, object row) { return (string)MonType.GetMethod("FormatCalculated", Any).Invoke(null, new object[] { mon, row, false }); }
    static string Value(object mon, object row) { return (string)MonType.GetMethod("FormatValue", Any).Invoke(null, new object[] { mon, row, false }); }
    static List<object> Kids(object r, string n) { var l = (IList)Get(r, n); return l == null ? new List<object>() : l.Cast<object>().ToList(); }

    static StructuredTypes.Type Step()
    {
        var t = new StructuredTypes.Type { Name = "Step", Size = 6 };
        t.Members.Add(new StructuredTypes.Member { Name = "Code", DataType = "Word [Signed]", Offset = 0 });
        t.Members.Add(new StructuredTypes.Member { Name = "X", DataType = "Double Word [Signed]", Offset = 1, Divisor = 100, Decimals = 2 });
        t.Members.Add(new StructuredTypes.Member { Name = "Speed", DataType = "Word [Unsigned]", Offset = 3, Divisor = 10, Decimals = 1 });
        t.Members.Add(new StructuredTypes.Member { Name = "Done", DataType = "Bit", Offset = 4, Bit = 2 });
        return t;
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "mond_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        StructuredTypes.FilePath = Path.Combine(dir, "StructuredTypes.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        Console.WriteLine("--- a structure's member: its divisor and decimal places kept ---");
        StructuredTypes.Save(new[] { Step() });
        var file = File.ReadAllLines(StructuredTypes.FilePath);
        Check("written after the comment, only for a member that has them", file.Contains("X;Double Word [Signed];1;;;100;2") && file.Contains("Speed;Word [Unsigned];3;;;10;1") &&
              file.Contains("Code;Word [Signed];0;;") && file.Contains("Done;Bit;4;2;"), string.Join(" / ", file.Where(l => !l.StartsWith("#"))));
        StructuredTypes.Reload();
        var back = StructuredTypes.Find("Step");
        Check("  read back", back != null && back.Members[1].Divisor == 100 && back.Members[1].Decimals == 2 && back.Members[2].Divisor == 10 &&
              back.Members[0].Divisor == 1 && back.Members[0].Decimals == 0 && back.Members[1].Comment == "");
        var old = StructuredTypes.Parse(new[] { "[Old];4;a step", "A;Word [Signed];0;;xxx.xx mm", "B;Word [Signed];1;;one; two", "C;Word [Signed];2;;mm;1000;3" });
        Check("a file of before: the comment as it was (a ';' in it too), no divisor; one with: divisor 1000, 3 places",
              old[0].Members[0].Comment == "xxx.xx mm" && old[0].Members[0].Divisor == 1 && old[0].Members[1].Comment == "one; two" && old[0].Members[1].Divisor == 1 &&
              old[0].Members[2].Comment == "mm" && old[0].Members[2].Divisor == 1000 && old[0].Members[2].Decimals == 3,
              old[0].Members[1].Comment + " | " + old[0].Members[2].Comment + " " + old[0].Members[2].Divisor);

        var sim = new Kaban.Drivers.Simulation.SimulationDriver(23);
        PlcManager.Instance.RegisterDriver(23, sim);
        PlcManager.Instance.RegisterStation(23, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(23, true);

        Console.WriteLine("--- the row editor ---");
        using (var ed = new PlcRowEditorForm("Bench", "Pos", "Double Word [Signed]", "D100", "", divisor: 100, decimals: 2))
        {
            ed.Show(); Application.DoEvents();
            var div = (TextBox)Get(ed, "_divisor"); var dec = (NumericUpDown)Get(ed, "_decimals");
            Check("Divisor and Decimal Places: a double word's, as the row has them", div.Enabled && dec.Enabled && div.Text == "100" && dec.Value == 2 &&
                  ed.RowDivisor == 100 && ed.RowDecimals == 2, div.Text + " " + dec.Value);
            div.Text = "0,5"; Application.DoEvents();
            Check("  a comma taken as the decimal point", ed.RowDivisor == 0.5, ed.RowDivisor.ToString());
            ((ComboBox)Get(ed, "_dataType")).Text = "Bit"; Application.DoEvents();
            Check("  a bit: none (nothing to calculate)", !div.Enabled && !dec.Enabled && ed.RowDivisor == 1 && ed.RowDecimals == 0);
            ((ComboBox)Get(ed, "_dataType")).Text = "Step"; Application.DoEvents();
            Check("  a structure: none (its members have theirs)", !div.Enabled);
            // OK refuses what TryDivisor does not take (the warning itself would stop the test)
            Func<string, bool> takes = s => (bool)typeof(PlcRowEditorForm).GetMethod("TryDivisor", Any).Invoke(null, new object[] { s, 0.0 });
            Check("  a divisor of 0, or no number: refused", !takes("0") && !takes("abc") && takes("100") && takes("0.01"));
            ed.Close();
        }

        var form = new SimulationMonitorForm();
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 23);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        var sh = Call(form, "EnsureSheet", mon);
        var grid = (DataGridView)Get(sh, "Grid");

        Console.WriteLine("--- the columns ---");
        var shown = grid.Columns.Cast<DataGridViewColumn>().OrderBy(c => c.DisplayIndex).Where(c => c.Visible).Select(c => c.Name).ToList();
        Check("Calculated Value, Divisor, Decimal Places right after Value", string.Join(",", shown) == "Label,DataType,Format,Address,Value,Calculated,Divisor,Decimals,Changed",
              string.Join(",", shown));
        Check("  their headings", grid.Columns["Calculated"].HeaderText == "Calculated Value" && grid.Columns["Divisor"].HeaderText == "Divisor" &&
              grid.Columns["Decimals"].HeaderText == "Decimal Places");

        Console.WriteLine("--- a word, a double word, a float, a bit, a string ---");
        sim.Write("D100", 12345 & 0xFFFF); sim.Write("D101", 0);
        sim.Write("D102", unchecked((short)-2500) & 0xFFFF);
        sim.Write("D110", 2500);
        int lo, hi;
        var fargs = new object[] { 3.25f, 0, 0 }; MonType.GetMethod("FromFloat", Any).Invoke(null, fargs); lo = (int)fargs[1]; hi = (int)fargs[2];
        sim.Write("D120", lo); sim.Write("D121", hi);
        sim.Write("M5", 1);
        var dw = Row("D100", "Double Word [Signed]", 1, "X"); Set(dw, "Divisor", 100.0); Set(dw, "Decimals", 2);
        var w = Row("D102", "Word [Signed]", 1, "Y"); Set(w, "Divisor", 1000.0); Set(w, "Decimals", 3);
        var plain = Row("D110", "Word [Signed]", 1, "Count");
        var fl = Row("D120", "Float [Single Precision]", 1, "Gain");
        var bit = Row("M5", "Bit", 1, "Lamp");
        var str = Row("D130", "String", 1, "Name");
        foreach (var r in new[] { dw, w, plain, fl, bit, str }) Rows(sh).Insert(Rows(sh).Count - 1, r);
        Call(form, "RefreshValues"); Application.DoEvents();
        Func<object, string, string> cell = (r, c) => Convert.ToString(grid.Rows[Rows(sh).IndexOf(r)].Cells[c].Value);
        Check("D100 12345 ÷ 100, 2 places: 123.45 - the value as it is beside it", cell(dw, "Value") == "12345" && cell(dw, "Calculated") == "123.45" &&
              cell(dw, "Divisor") == "100" && cell(dw, "Decimals") == "2", cell(dw, "Value") + " | " + cell(dw, "Calculated"));
        Check("D102 -2500 ÷ 1000, 3 places: -2.500", cell(w, "Calculated") == "-2.500", cell(w, "Calculated"));
        Check("no divisor set: the same number, its divisor 1 and 0 places greyed", cell(plain, "Calculated") == "2500" && cell(plain, "Divisor") == "1" &&
              cell(plain, "Decimals") == "0" && grid.Rows[Rows(sh).IndexOf(plain)].Cells["Divisor"].Style.ForeColor != System.Drawing.Color.Empty &&
              grid.Rows[Rows(sh).IndexOf(dw)].Cells["Divisor"].Style.ForeColor == System.Drawing.Color.Empty);
        Check("a float not scaled: as its value", cell(fl, "Calculated") == "3.25", cell(fl, "Calculated"));
        Check("a bit, a string: nothing to calculate, no divisor", cell(bit, "Calculated") == "" && cell(bit, "Divisor") == "" && cell(str, "Calculated") == "" && cell(str, "Divisor") == "");
        sim.Write("D100", 5); Call(form, "RefreshValues"); Application.DoEvents();
        Check("  follows the value: 5 - 0.05", cell(dw, "Calculated") == "0.05", cell(dw, "Calculated"));

        Console.WriteLine("--- written as the calculated value ---");
        int v, v2;
        Call(form, "WriteValue", mon, dw, "-1234.56", "typed", true);
        sim.TryRead("D100", out v); sim.TryRead("D101", out v2);
        Check("-1234.56 into X (÷100): -123456 into both words", (v | (v2 << 16)) == -123456, v + " " + v2);
        Call(form, "WriteValue", mon, w, "1.2345", "typed", true); sim.TryRead("D102", out v);
        Check("1.2345 into Y (÷1000): rounded, 1235", (short)v == 1235, v.ToString());
        Call(form, "WriteValue", mon, w, "40", "typed", true); sim.TryRead("D102", out v);
        Check("  40 (40000, past a signed word): not written", (short)v == 1235, v.ToString());
        Set(fl, "Divisor", 0.5);
        Call(form, "WriteValue", mon, fl, "2", "typed", true); Call(form, "RefreshValues");
        Check("a float ÷ 0.5: 2 written as 1", Value(mon, fl) == "1", Value(mon, fl));
        Set(fl, "Divisor", 1.0);

        Console.WriteLine("--- an array: its elements take its divisor ---");
        for (int i = 0; i < 4; i++) sim.Write("D" + (200 + i), (i + 1) * 150);
        var arr = Row("D200", "Word [Signed]", 4, "Speeds"); Set(arr, "Divisor", 10.0); Set(arr, "Decimals", 1);
        Rows(sh).Insert(Rows(sh).Count - 1, arr);
        Call(form, "RefreshValues"); Application.DoEvents();
        var els = Kids(arr, "Elements");
        Check("Speeds[2] 450 ÷ 10: 45.0; the array folded: its elements' calculated", Calc(mon, els[2]) == "45.0" && Calc(mon, arr) == "15.0, 30.0, 45.0, 60.0",
              Calc(mon, els[2]) + " | " + Calc(mon, arr));
        Check("  the divisor on the array's line, not on its elements'", cell(arr, "Divisor") == "10" && cell(els[2], "Divisor") == "" &&
              !(bool)Call(MonType, "CanScale", els[2]) && (bool)Call(MonType, "CanScale", arr));

        Console.WriteLine("--- a structure: its members' own ---");
        sim.Write("R0", 7); sim.Write("R1", 98765 & 0xFFFF); sim.Write("R2", 98765 >> 16); sim.Write("R3", 1205); sim.Write("R4", 1 << 2);
        var st = Row("R0", "Step", 1, "Recipe"); Set(st, "Expanded", true);
        Rows(sh).Insert(Rows(sh).Count - 1, st);
        Call(form, "RefreshValues"); Application.DoEvents();
        var mem = Kids(st, "Members");
        Check("Recipe.X 98765 ÷ 100: 987.65; Recipe.Speed 1205 ÷ 10: 120.5; Code as it is; the bit nothing",
              Calc(mon, mem[1]) == "987.65" && Calc(mon, mem[2]) == "120.5" && Calc(mon, mem[0]) == "7" && Calc(mon, mem[3]) == "",
              Calc(mon, mem[1]) + " " + Calc(mon, mem[2]) + " " + Calc(mon, mem[3]));
        Check("  folded in braces, the bit as its value", Calc(mon, st) == "{7, 987.65, 120.5, 1}", Calc(mon, st));
        Check("  each member shows its divisor", cell(mem[1], "Divisor") == "100" && cell(mem[1], "Decimals") == "2" && cell(mem[3], "Divisor") == "" && cell(st, "Divisor") == "");
        Call(form, "SetScale", mon, mem[2], "100", "2"); Call(form, "Render", mon, sh);
        Check("a member's divisor set on the monitor: into the structure itself, kept", StructuredTypes.Find("Step").Members[2].Divisor == 100 &&
              File.ReadAllLines(StructuredTypes.FilePath).Contains("Speed;Word [Unsigned];3;;;100;2") && Calc(mon, Kids(st, "Members")[2]) == "12.05",
              Calc(mon, Kids(st, "Members")[2]));
        Call(form, "SetScale", mon, dw, "0", null);
        Check("  a divisor of 0 refused", (double)Get(dw, "Divisor") == 100);
        Call(form, "SetScale", mon, dw, null, "4");
        Check("  4 places: -1234.5600", (int)Get(dw, "Decimals") == 4 && Calc(mon, dw) == "-1234.5600", Calc(mon, dw));
        Call(form, "SetScale", mon, dw, null, "2");

        Console.WriteLine("--- edited in the table ---");
        bool cancelled;
        Func<object, string, bool> canEdit = (r, c) =>
        {
            var args = new DataGridViewCellCancelEventArgs(grid.Columns[c].Index, Rows(sh).IndexOf(r));
            typeof(DataGridView).GetMethod("OnCellBeginEdit", Any).Invoke(grid, new object[] { args });
            Set(sh, "Editing", false);
            return !args.Cancel;
        };
        Check("the calculated value of a number edited in place; of a bit, an array, a structure not",
              canEdit(dw, "Calculated") && canEdit(els[1], "Calculated") && canEdit(mem[1], "Calculated") && !canEdit(bit, "Calculated") && !canEdit(arr, "Calculated") && !canEdit(st, "Calculated"));
        Check("  the divisor: a number's, an array's, a member's - not an element's, a bit's", canEdit(dw, "Divisor") && canEdit(arr, "Decimals") && canEdit(mem[1], "Divisor") &&
              !canEdit(els[1], "Divisor") && !canEdit(bit, "Divisor") && !canEdit(st, "Divisor"));
        int at = Rows(sh).IndexOf(plain);
        grid.Rows[at].Cells["Divisor"].Value = "4";
        Call(form, "CommitCellEdit", mon, sh, at, grid.Columns["Divisor"].Index);
        Check("a divisor typed: kept, 2500 ÷ 4 = 625", (double)Get(plain, "Divisor") == 4 && cell(plain, "Calculated") == "625" &&
              grid.Rows[at].Cells["Divisor"].Style.ForeColor == System.Drawing.Color.Empty, cell(plain, "Calculated"));
        grid.Rows[at].Cells["Calculated"].Value = "100";
        Call(form, "CommitCellEdit", mon, sh, at, grid.Columns["Calculated"].Index);
        sim.TryRead("D110", out v);
        Check("a calculated value typed: 100 × 4 = 400 written", v == 400, v.ToString());

        Console.WriteLine("--- kept with the list ---");
        var lines = (List<string>)Call(MonType, "Serialize", mon);
        Check("div= and dec= in the row's extras", lines.Any(l => l.Contains(";D100;;") && l.Contains("div=100") && l.Contains("dec=2")) &&
              lines.Any(l => l.Contains(";D110;;") && l.Contains("div=4") && !l.Contains("dec=")) && lines.Any(l => l.Contains(";M5") && !l.Contains("div=")),
              string.Join(" / ", lines.Where(l => l.Contains(";D1"))));
        var mon2 = Activator.CreateInstance(MonType.GetNestedType("MonitorTab", Any));
        Call(MonType, "Deserialize", mon2, lines);
        var again = Rows(((IList)Get(mon2, "Sheets"))[0]).Cast<object>().First(r => (string)Get(r, "Address") == "D100");
        Check("  read back", (double)Get(again, "Divisor") == 100 && (int)Get(again, "Decimals") == 2);
        var copy = Call(Kids(st, "Members")[1], "Clone");
        Check("  a member copied alone takes its divisor with it", (double)Get(copy, "Divisor") == 100 && (int)Get(copy, "Decimals") == 2);

        Console.WriteLine("--- sorted by the calculated value ---");
        var sorted = new List<object> { dw, w, plain };
        sorted.Sort((a, b) => (int)Call(MonType, "CompareRows", mon, a, b, "Calculated"));
        Check("X -1234.56, Y 1.235, Count 100", sorted[0] == dw && sorted[1] == w && sorted[2] == plain);

        Console.WriteLine("--- a column layout kept from before ---");
        form.Close();
        File.WriteAllText(Path.Combine(dir, "Columns.cfg"), "Label=280;0;1\r\nDataType=170;1;1\r\nFormat=110;2;1\r\nAddress=90;4;1\r\nValue=180;3;1\r\nChanged=150;5;1\r\nCondition=130;6;1\r\n");
        foreach (var n in new[] { "_colWidth", "_colOrder", "_colHidden" }) MonType.GetField(n, Any).SetValue(null, null);
        // the order put on the grid (Mono's grid moves a column to the left wrongly: the order itself checked)
        var order = (List<string>)Call(MonType, "ColumnOrder");
        Check("the new columns after Value, wherever Value was moved to", string.Join(",", order) == "Label,DataType,Format,Value,Calculated,Divisor,Decimals,Address,Changed,Condition",
              string.Join(",", order));

        Console.WriteLine("--- the structures' editor ---");
        using (var f = new StructuredTypesForm("Step"))
        {
            f.Show(); Application.DoEvents();
            var members = (DataGridView)Get(f, "_members");
            var cur = (StructuredTypes.Type)Get(f, "Current");
            Check("Divisor and Decimal Places columns: a number's, a bit's read-only and empty",
                  Convert.ToString(members.Rows[1].Cells["Divisor"].Value) == "100" && Convert.ToString(members.Rows[1].Cells["Decimals"].Value) == "2" &&
                  Convert.ToString(members.Rows[0].Cells["Divisor"].Value) == "1" && members.Rows[3].Cells["Divisor"].ReadOnly &&
                  Convert.ToString(members.Rows[3].Cells["Divisor"].Value) == "" && !members.Rows[1].Cells["Divisor"].ReadOnly,
                  Convert.ToString(members.Rows[0].Cells["Divisor"].Value));
            members.Rows[0].Cells["Divisor"].Value = "60";
            members.Rows[0].Cells["Decimals"].Value = "1";
            Check("  typed: the member's", cur.Members[0].Divisor == 60 && cur.Members[0].Decimals == 1);
            members.Rows[0].Cells["Divisor"].Value = "abc";
            Check("  not a number: the one kept shown again", cur.Members[0].Divisor == 60 && Convert.ToString(members.Rows[0].Cells["Divisor"].Value) == "60",
                  Convert.ToString(members.Rows[0].Cells["Divisor"].Value));
            members.Rows[0].Cells["DataType"].Value = "Bit";
            Check("  made a bit: none", cur.Members[0].Divisor == 1 && members.Rows[0].Cells["Divisor"].ReadOnly);
            f.Close();
        }

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "MONITOR DIVISOR ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
