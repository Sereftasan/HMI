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

// 19 October: the PLC Monitor's STRUCTURED DATA TYPE - Mitsubishi's: a recipe step of members,
// each with its own data type and index, the structure its own length (the milling steps: 16
// devices, R0, R16 ...); saved, and chosen like a simple data type.
class PS {
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
    static object Row(string address, string type, int count = 1, string label = "Recipe") {
        var r = Activator.CreateInstance(RowType);
        Set(r, "Address", address); Set(r, "DataTypeOverride", type); Set(r, "LabelOverride", label);
        Set(r, "ArrayCount", count); Set(r, "Station", 23);
        return r;
    }
    static string Shown(object mon, object row) {
        var args = new object[] { mon, row, false };
        return (string)MonType.GetMethod("FormatValue", Any).Invoke(null, args);
    }
    static List<object> Kids(object r, string n) { var l = (IList)Get(r, n); return l == null ? new List<object>() : l.Cast<object>().ToList(); }
    static void Dword(Kaban.Drivers.Simulation.SimulationDriver sim, int at, int v) { sim.Write("R" + at, v & 0xFFFF); sim.Write("R" + (at + 1), (v >> 16) & 0xFFFF); }

    /// <summary>The picture's milling step: a movement code, X Y Z I J K (32 bit), a speed (16 bit) - 16 devices; a bit of word 15 too.</summary>
    static StructuredTypes.Type Milling()
    {
        var t = new StructuredTypes.Type { Name = "FrezeAdim", Size = 16 };
        t.Members.Add(new StructuredTypes.Member { Name = "HareketKodu", DataType = "Word [Signed]", Offset = 0 });
        int o = 1;
        foreach (var n in new[] { "X", "Y", "Z", "I", "J", "K" }) { t.Members.Add(new StructuredTypes.Member { Name = n, DataType = "Double Word [Signed]", Offset = o }); o += 2; }
        t.Members.Add(new StructuredTypes.Member { Name = "Hiz", DataType = "Word [Signed]", Offset = 13 });
        t.Members.Add(new StructuredTypes.Member { Name = "Bitti", DataType = "Bit", Offset = 15, Bit = 3 });
        return t;
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "mons_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        StructuredTypes.FilePath = Path.Combine(dir, "StructuredTypes.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        StructuredTypes.Save(new[] { Milling() });

        var sim = new Kaban.Drivers.Simulation.SimulationDriver(23);
        PlcManager.Instance.RegisterDriver(23, sim);
        PlcManager.Instance.RegisterStation(23, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(23, true);

        Console.WriteLine("--- chosen like a simple data type ---");
        using (var ed = new PlcRowEditorForm("Bench", "Recipe", "FrezeAdim", "R0", "", isNew: true, arrayCount: 3))
        {
            ed.Show(); Application.DoEvents();
            var combo = (ComboBox)Get(ed, "_dataType");
            Check("the data type list: the simple types, then the structures saved", combo.Items.Cast<object>().Select(Convert.ToString).Contains("FrezeAdim") &&
                  Convert.ToString(combo.Items[0]) == "Bit", string.Join(" | ", combo.Items.Cast<object>()));
            Check("  an array of 3: R0 ... R32, every 16th device", ((Label)Get(ed, "_preview")).Text.StartsWith("R0 … R32") && ((Label)Get(ed, "_preview")).Text.Contains("every 16th device"),
                  ((Label)Get(ed, "_preview")).Text);
            Check("  no value of its own (its members have theirs), no string length", !((TextBox)Get(ed, "_value")).Enabled && !((NumericUpDown)Get(ed, "_chars")).Enabled &&
                  ((ComboBox)Get(ed, "_format")).Enabled);
            ((NumericUpDown)Get(ed, "_count")).Value = 1;
            Application.DoEvents();
            Check("  one of them: R0 ... R15, its members and devices", ((Label)Get(ed, "_preview")).Text == "R0 … R15  (9 members, 16 devices)", ((Label)Get(ed, "_preview")).Text);
            Check("  the structures' editor beside the list", ((Button)Get(ed, "_structures")).Visible && ((Button)Get(ed, "_structures")).Text == "Structures...");
            ed.Close();
        }

        var form = new SimulationMonitorForm();
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 23);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        var sh = Call(form, "EnsureSheet", mon);
        var grid = (DataGridView)Get(sh, "Grid");

        Console.WriteLine("--- an array of 3 milling steps at R0 ---");
        for (int i = 0; i < 48; i++) sim.Write("R" + i, 0);
        sim.Write("R0", 1); Dword(sim, 1, 12345); Dword(sim, 3, -500); sim.Write("R13", 2500);
        sim.Write("R16", 2); Dword(sim, 17, 67890); sim.Write("R31", 1 << 3);
        var rec = Row("R0", "FrezeAdim", 3);
        Rows(sh).Insert(Rows(sh).Count - 1, rec);
        Call(form, "RefreshValues"); Application.DoEvents();
        var els = Kids(rec, "Elements");
        Check("its 3 elements at R0, R16, R32 - each the structure", els.Count == 3 && (string)Get(els[1], "Address") == "R16" && (string)Get(els[2], "Address") == "R32" &&
              Get(els[1], "Structure") != null && (string)Get(els[1], "EffectiveLabel") == "Recipe[1]",
              string.Join(",", els.Select(e => (string)Get(e, "Address"))));
        var m1 = Kids(els[1], "Members");
        Check("each with its 9 members under it, in the list right after it", m1.Count == 9 && Rows(sh).IndexOf(m1[0]) == Rows(sh).IndexOf(els[1]) + 1 &&
              Rows(sh).IndexOf(els[2]) == Rows(sh).IndexOf(m1[8]) + 1, m1.Count.ToString());
        Check("  Recipe[1].X at R17, a double word; Recipe[1].Hiz at R29; the bit Recipe[1].Bitti at R31.3",
              (string)Get(m1[1], "Address") == "R17" && (string)Get(m1[1], "EffectiveLabel") == "Recipe[1].X" && (string)Get(m1[1], "EffectiveDataType") == "Double Word [Signed]" &&
              (string)Get(m1[7], "Address") == "R29" && (string)Get(m1[8], "ShownAddress") == "R31.3" && (string)Get(m1[8], "EffectiveDataType") == "Bit",
              (string)Get(m1[1], "EffectiveLabel") + " " + (string)Get(m1[8], "ShownAddress"));
        var m0 = Kids(els[0], "Members");
        Check("the values in their types: step 0's code 1, X 12345, Y -500, speed 2500; step 1's X 67890, its bit ON",
              Shown(mon, m0[0]) == "1" && Shown(mon, m0[1]) == "12345" && Shown(mon, m0[2]) == "-500" && Shown(mon, m0[7]) == "2500" &&
              Shown(mon, m1[1]) == "67890" && Shown(mon, m1[8]) == "1" && Shown(mon, m0[8]) == "0",
              Shown(mon, m0[0]) + " " + Shown(mon, m0[1]) + " " + Shown(mon, m0[2]) + " " + Shown(mon, m1[8]));
        Check("a step folded: its members' values in braces; the array folded: its first two steps",
              Shown(mon, els[0]).StartsWith("{1, 12345, -500, 0") && Shown(mon, rec).StartsWith("{1, 12345") && Shown(mon, rec).EndsWith(", …"), Shown(mon, rec));
        var batchRows = Rows(sh).Cast<object>().Where(r => (bool)Get(r, "IsDevice") && !(bool)Get(r, "IsGroup")).ToList();
        var words = batchRows.SelectMany(r => ((IEnumerable<string>)MonType.GetMethod("WordsOf", Any).Invoke(null, new[] { r }))).ToList();
        Check("what is read: the members' words (a double word both, a bit its word) - not the steps' rows as words",
              words.Contains("R17") && words.Contains("R18") && words.Contains("R31") && !words.Contains("R31.3") && words.Count(a => a == "R16") == 1);

        Console.WriteLine("--- folded and unfolded, level by level ---");
        int at = Rows(sh).IndexOf(rec), e1 = Rows(sh).IndexOf(els[1]), x1 = Rows(sh).IndexOf(m1[1]);
        Check("folded: only the array's line", !grid.Rows[e1].Visible && !grid.Rows[x1].Visible);
        Call(form, "ToggleArray", mon, sh, rec);
        Check("the array unfolded: its steps, each folded", grid.Rows[e1].Visible && !grid.Rows[x1].Visible &&
              Convert.ToString(grid.Rows[e1].Cells["Label"].Value) == "      ▸ Recipe[1]", Convert.ToString(grid.Rows[e1].Cells["Label"].Value));
        Call(form, "ToggleArray", mon, sh, els[1]);
        Check("a step unfolded: its members, indented one more, each with its own type and address",
              grid.Rows[x1].Visible && Convert.ToString(grid.Rows[x1].Cells["Label"].Value) == "            Recipe[1].X" &&
              Convert.ToString(grid.Rows[x1].Cells["DataType"].Value) == "Double Word [Signed]" && Convert.ToString(grid.Rows[x1].Cells["Address"].Value) == "R17" &&
              Convert.ToString(grid.Rows[Rows(sh).IndexOf(m1[8])].Cells["Address"].Value) == "R31.3" && !grid.Rows[Rows(sh).IndexOf(Kids(els[2], "Members")[0])].Visible,
              Convert.ToString(grid.Rows[x1].Cells["Label"].Value));
        Check("  the array's own line: its type and count", Convert.ToString(grid.Rows[at].Cells["Label"].Value) == "▾ Recipe   [3]" &&
              Convert.ToString(grid.Rows[at].Cells["DataType"].Value) == "FrezeAdim", Convert.ToString(grid.Rows[at].Cells["Label"].Value));

        Console.WriteLine("--- written member by member ---");
        int v;
        Call(form, "WriteValue", mon, m1[2], "-1234"); sim.TryRead("R19", out v); int hi; sim.TryRead("R20", out hi);
        Check("Recipe[1].Y = -1234: both its words", (short)v == -1234 && hi == 0xFFFF, v + " " + hi);
        sim.Write("R31", 0x0101);
        Call(form, "RefreshValues");
        Call(form, "WriteValue", mon, m1[8], "1"); sim.TryRead("R31", out v);
        Check("the bit R31.3 set: the rest of its word as it was", v == 0x0109, "0x" + v.ToString("X4"));
        Call(form, "WriteValue", mon, m1[8], "0"); sim.TryRead("R31", out v);
        Check("  and cleared", v == 0x0101, "0x" + v.ToString("X4"));
        Call(form, "WriteValue", mon, els[1], "5"); sim.TryRead("R16", out v);
        Check("a step's own line has no value: nothing written to R16", v == 2, v.ToString());

        Console.WriteLine("--- kept, and read back ---");
        var lines = (List<string>)Call(MonType, "Serialize", mon);
        Check("kept as ONE line - the type's name and the count; steps and members are made from it",
              lines.Count(l => l.Contains(";R")) == 1 && lines.Any(l => l.Contains("FrezeAdim;R0") && l.Contains("n=3")), string.Join(" / ", lines.Where(l => l.Contains(";R"))));
        var mon2 = Activator.CreateInstance(MonType.GetNestedType("MonitorTab", Any));
        Call(MonType, "Deserialize", mon2, new List<string> { "#sheet:Main", "Label;DataType;Address;Value", "Step;FrezeAdim;R100;;open=1" });
        var s2 = ((IList)Get(mon2, "Sheets"))[0];
        Call(MonType, "EnsureElements", s2);
        var one = Rows(s2)[0];
        Check("one structure (no array): its members right under it, from R100", (bool)Get(one, "IsGroup") && !(bool)Get(one, "IsArray") && Kids(one, "Members").Count == 9 &&
              (string)Get(Kids(one, "Members")[1], "Address") == "R101" && (string)Get(Kids(one, "Members")[1], "EffectiveLabel") == "Step.X");

        Console.WriteLine("--- the label list's array of a structure ---");
        PlcLabelTable.LoadStation(WriteLabels(dir), 23);
        var fromList = Row("R200", "", 1, "");
        Rows(sh).Insert(Rows(sh).Count - 1, fromList);
        Call(form, "Render", mon, sh);
        Check("PC_FrezeAdimlari;FrezeAdim(0..9);R200 - an array of 10 steps, R200 ... R344",
              (bool)Get(fromList, "IsArray") && (int)Get(fromList, "EffectiveCount") == 10 && Kids(fromList, "Elements").Count == 10 &&
              (string)Get(Kids(fromList, "Elements")[9], "Address") == "R344" && (string)Get(Kids(fromList, "Elements")[9], "EffectiveLabel") == "PC_FrezeAdimlari[9]",
              Kids(fromList, "Elements").Count + " " + (Kids(fromList, "Elements").Count > 9 ? (string)Get(Kids(fromList, "Elements")[9], "Address") : ""));

        Console.WriteLine("--- the structures' editor ---");
        using (var f = new StructuredTypesForm("FrezeAdim"))
        {
            f.Show(); Application.DoEvents();
            var work = (List<StructuredTypes.Type>)Get(f, "Working");
            var cur = (StructuredTypes.Type)Get(f, "Current");
            var membersGrid = (DataGridView)Get(f, "_members");
            Check("opened on the row's structure: its 9 members listed with their offsets and bits",
                  cur != null && cur.Name == "FrezeAdim" && membersGrid.Rows.Count == 9 && Convert.ToString(membersGrid.Rows[1].Cells["Offset"].Value) == "1" &&
                  Convert.ToString(membersGrid.Rows[8].Cells["Bit"].Value) == "3" && membersGrid.Rows[1].Cells["Bit"].ReadOnly);
            Call(f, "NewType", (object)null);
            cur = (StructuredTypes.Type)Get(f, "Current");
            Call(f, "AddMember"); Call(f, "AddMember");
            Check("a new one: named, its members one after the other (+0, +1)", cur.Name == "Structure" && cur.Members.Count == 2 && cur.Members[1].Offset == 1 && cur.Size == 2,
                  cur.Name + " " + cur.Members.Count + " " + cur.Size);
            membersGrid.Rows[1].Cells["DataType"].Value = "Float [Single Precision]";
            Check("  a member made a float: the structure grows to 3 devices", cur.Members[1].DataType == "Float [Single Precision]" && cur.Size == 3, cur.Size.ToString());
            membersGrid.Rows[1].Cells["Offset"].Value = "0";
            Check("  two members on one device: said, and OK refuses it", ((string)Call(f, "FirstProblem") ?? "").Contains("use the same device"),
                  (string)Call(f, "FirstProblem"));
            membersGrid.Rows[1].Cells["Offset"].Value = "1";
            ((TextBox)Get(f, "_name")).Text = "Kesim";
            Check("  renamed in the list as typed", ((ListBox)Get(f, "_types")).Items.Cast<object>().Select(Convert.ToString).Contains("Kesim"));
            Check("  complete now", Call(f, "FirstProblem") == null, (string)Call(f, "FirstProblem"));
            f.DialogResult = DialogResult.OK;
            f.Close();
        }
        StructuredTypes.Reload();
        Check("OK kept both: offered by every list from now on", StructuredTypes.Find("Kesim") != null && StructuredTypes.Find("FrezeAdim") != null &&
              StructuredTypes.Find("Kesim").Members[1].DataType == "Float [Single Precision]");

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nPLC MONITOR STRUCTURES ALL PASS" : "\nPLC MONITOR STRUCTURES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    static string WriteLabels(string dir)
    {
        var p = Path.Combine(dir, "PlcLabels.csv");
        File.WriteAllLines(p, new[] { "Label;DataType;Address", "PC_FrezeAdimlari;FrezeAdim(0..9);R200" });
        return p;
    }
}
