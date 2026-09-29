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

// The PLC Monitor (29 September): an ARRAY is one item that expands to its
// elements; every row has a DISPLAY FORMAT (decimal, hexadecimal, binary);
// Word and Double Word unsigned, Float (single precision) and String are data
// types - read, shown and written as such.
class PA {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
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
    static object Row(string address, string type, int count = 1, string format = "") {
        var r = Activator.CreateInstance(RowType);
        Set(r, "Address", address); Set(r, "DataTypeOverride", type); Set(r, "LabelOverride", "V");
        Set(r, "ArrayCount", count); Set(r, "DisplayFormat", format); Set(r, "Station", 23);
        return r;
    }
    static string Shown(object mon, object row) {
        var args = new object[] { mon, row, false };
        return (string)MonType.GetMethod("FormatValue", Any).Invoke(null, args);
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "mona_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        var sim = new Kaban.Drivers.Simulation.SimulationDriver(23);
        PlcManager.Instance.RegisterDriver(23, sim);
        PlcManager.Instance.RegisterStation(23, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(23, true);

        Console.WriteLine("--- the data types ---");
        var known = (string[])typeof(PlcRowEditorForm).GetField("KnownDataTypes").GetValue(null);
        Check("Bit, Word and Double Word signed and unsigned, Float [Single Precision], String",
              known.SequenceEqual(new[] { "Bit", "Word [Signed]", "Word [Unsigned]", "Double Word [Signed]", "Double Word [Unsigned]", "Float [Single Precision]", "String" }),
              string.Join(" | ", known));
        Check("  a float takes two words, a string of 10 characters five",
              PlcRowEditorForm.ElementAddress("D0", "Float [Single Precision]", 3) == "D6" && PlcRowEditorForm.ElementAddress("D0", "String", 2, 10) == "D10");

        var form = new SimulationMonitorForm();
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 23);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        var sh = Call(form, "EnsureSheet", mon);
        var grid = (DataGridView)Get(sh, "Grid");

        sim.Write("D100", -2);                     // 0xFFFE
        var w = Row("D100", "Word [Signed]"); var wu = Row("D100", "Word [Unsigned]");
        var wh = Row("D100", "Word [Unsigned]", 1, "Hexadecimal"); var wb = Row("D100", "Word [Signed]", 1, "Binary");
        sim.Write("D110", 0x5678); sim.Write("D111", 0x1234);
        var dw = Row("D110", "Double Word [Unsigned]"); var dwh = Row("D110", "Double Word [Signed]", 1, "Hexadecimal");
        var fa = new object[] { 12.5f, 0, 0 };
        MonType.GetMethod("FromFloat", Any).Invoke(null, fa);
        int lo = (int)fa[1], hi = (int)fa[2];
        sim.Write("D120", lo); sim.Write("D121", hi);
        var fl = Row("D120", "Float [Single Precision]");
        sim.Write("D130", 'O' | ('K' << 8)); sim.Write("D131", '!');
        var st = Row("D130", "String"); Set(st, "StringLength", 4);
        foreach (var r in new[] { w, wu, wh, wb, dw, dwh, fl, st }) Rows(sh).Insert(Rows(sh).Count - 1, r);
        Call(form, "RefreshValues"); Application.DoEvents();

        Console.WriteLine("--- shown in the row's type and display format ---");
        Check("word signed: -2", Shown(mon, w) == "-2", Shown(mon, w));
        Check("word unsigned: 65534", Shown(mon, wu) == "65534", Shown(mon, wu));
        Check("hexadecimal: 0xFFFE", Shown(mon, wh) == "0xFFFE", Shown(mon, wh));
        Check("binary: 1111 1111 1111 1110", Shown(mon, wb) == "1111 1111 1111 1110", Shown(mon, wb));
        Check("double word unsigned: 0x12345678 = 305419896", Shown(mon, dw) == "305419896", Shown(mon, dw));
        Check("double word in hexadecimal: 0x12345678", Shown(mon, dwh) == "0x12345678", Shown(mon, dwh));
        Check("float: 12.5 over two words", Shown(mon, fl) == "12.5", Shown(mon, fl));
        Check("string: \"OK!\" (two characters a word, low byte first)", Shown(mon, st) == "\"OK!\"", Shown(mon, st));
        int wi = Rows(sh).IndexOf(wh);
        Check("the Display Format column says so", Convert.ToString(grid.Rows[wi].Cells["Format"].Value) == "Hexadecimal",
              Convert.ToString(grid.Rows[wi].Cells["Format"].Value));

        Console.WriteLine("--- written from what is typed, in that format ---");
        Call(form, "WriteValue", mon, wh, "1F"); int v; sim.TryRead("D100", out v);
        Check("'1F' in a hexadecimal row writes 31", v == 31, v.ToString());
        Call(form, "WriteValue", mon, wb, "0000 0101"); sim.TryRead("D100", out v);
        Check("'0000 0101' in a binary row writes 5", v == 5, v.ToString());
        Call(form, "WriteValue", mon, w, "0x10"); sim.TryRead("D100", out v);
        Check("'0x10' works in any row", v == 16, v.ToString());
        Call(form, "WriteValue", mon, fl, "-3.25"); Call(form, "RefreshValues");
        Check("a float: -3.25 written over both words", Shown(mon, fl) == "-3.25", Shown(mon, fl));
        Call(form, "WriteValue", mon, st, "Hi"); Call(form, "RefreshValues");
        Check("a string: \"Hi\"", Shown(mon, st) == "\"Hi\"", Shown(mon, st));
        sim.Write("D100", 7);
        Call(form, "WriteValue", mon, wu, "70000"); sim.TryRead("D100", out v);
        Check("70000 is too much for a word: nothing written", v == 7, v.ToString());
        Call(form, "WriteValue", mon, wh, "XYZ"); sim.TryRead("D100", out v);
        Check("'XYZ' is not hexadecimal: nothing written", v == 7, v.ToString());

        Console.WriteLine("--- an array is one item ---");
        foreach (var r in new[] { w, wu, wh, wb, dw, dwh, fl, st }) Rows(sh).Remove(r);
        for (int i = 0; i < 10; i++) sim.Write("D" + (200 + i), i * 10);
        var arr = Row("D200", "Word [Signed]", 10);
        Set(arr, "LabelOverride", "Speeds");
        Rows(sh).Insert(Rows(sh).Count - 1, arr);
        Call(form, "RefreshValues"); Application.DoEvents();
        int at = Rows(sh).IndexOf(arr);
        var els = ((IList)Get(arr, "Elements")).Cast<object>().ToList();
        Check("D200 x 10: its ten elements right under it, D200 ... D209",
              els.Count == 10 && Rows(sh).IndexOf(els[0]) == at + 1 && (string)Get(els[9], "Address") == "D209");
        Check("  named Speeds[0] ... Speeds[9]", (string)Get(els[3], "EffectiveLabel") == "Speeds[3]", (string)Get(els[3], "EffectiveLabel"));
        Check("folded: one line, its first values in it", !grid.Rows[at + 1].Visible && Shown(mon, arr).StartsWith("0, 10, 20"),
              Shown(mon, arr));
        Check("  the label shows the fold mark and the count", Convert.ToString(grid.Rows[at].Cells["Label"].Value) == "▸ Speeds   [10]",
              Convert.ToString(grid.Rows[at].Cells["Label"].Value));
        Call(form, "ToggleArray", mon, sh, arr);
        Check("unfolded: the elements show", grid.Rows[at + 1].Visible && grid.Rows[at + 10].Visible);
        Call(form, "WriteValue", mon, els[4], "444"); sim.TryRead("D204", out v);
        Check("an element's value is written like any row's", v == 444, v.ToString());

        Console.WriteLine("--- kept, moved and removed as one ---");
        var lines = (List<string>)Call(MonType, "Serialize", mon);
        Check("kept as one line (n=10, open) - the elements are not stored",
              lines.Count(l => l.Contains("D20")) == 1 && lines.Any(l => l.Contains(";D200;") && l.Contains("n=10") && l.Contains("open=1")),
              string.Join(" / ", lines.Where(l => l.Contains("D20"))));
        var other = Row("M10", "Bit");
        Rows(sh).Insert(0, other);
        Call(form, "Render", mon, sh);
        grid.ClearSelection(); grid.Rows[Rows(sh).IndexOf(els[2])].Selected = true;
        Call(form, "MoveSelected", -1);
        at = Rows(sh).IndexOf(arr);
        Check("an element selected and moved up: its whole array moves above M10",
              at == 0 && Rows(sh).IndexOf(other) == 11 && Rows(sh).IndexOf(els[0]) == 1, at + " / " + Rows(sh).IndexOf(other));
        grid.ClearSelection(); grid.Rows[Rows(sh).IndexOf(els[5])].Selected = true;
        Call(form, "CopySelected");
        grid.ClearSelection(); grid.Rows[Rows(sh).IndexOf(arr)].Selected = true;
        Call(form, "RemoveSelected");
        Call(form, "RefreshValues");
        Check("removed: the array and all its elements - and they stay off the simulator's list", !Rows(sh).Cast<object>().Any(r => ((string)Get(r, "Address")).StartsWith("D20")),
              string.Join(",", Rows(sh).Cast<object>().Select(r => (string)Get(r, "Address"))));
        Call(form, "Undo");
        sh = Get(mon, "Current");
        var back = Rows(sh).Cast<object>().FirstOrDefault(r => (string)Get(r, "Address") == "D200" && (bool)Get(r, "IsArray"));
        Check("undo: the array back, with its 10 elements", back != null && ((IList)Get(back, "Elements")).Count == 10);

        Console.WriteLine("--- the list read back ---");
        var mon2 = Activator.CreateInstance(MonType.GetNestedType("MonitorTab", Any));
        Call(MonType, "Deserialize", mon2, new List<string> { "#sheet:Main", "Label;DataType;Address;Value", "Speeds;Word [Signed];D200;;fmt=Hexadecimal|n=4|open=1", "Old;Bit;M1;" });
        var s2 = ((IList)Get(mon2, "Sheets"))[0];
        var r0 = Rows(s2)[0];
        Check("a kept array: count, fold and format back", (int)Get(r0, "ArrayCount") == 4 && (bool)Get(r0, "Expanded") && (string)Get(r0, "DisplayFormat") == "Hexadecimal");
        Check("  a line from before reads as ever", (string)Get(Rows(s2)[1], "Address") == "M1" && (int)Get(Rows(s2)[1], "ArrayCount") == 1);

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nPLC MONITOR ARRAYS ALL PASS" : "\nPLC MONITOR ARRAYS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
