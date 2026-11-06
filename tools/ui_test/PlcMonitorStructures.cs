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
using Kaban.Core.Machines;
using Kaban.UI.Forms;

// 19 October: the PLC Monitor's STRUCTURED DATA TYPE - Mitsubishi's: a recipe step of members,
// each with its own data type and index, the structure its own length (the milling steps: 16
// devices, R0, R16 ...); saved, and chosen like a simple data type.
class PS {
    static int fail = 0;
    class Mod : IMachineModule {
        public string Id;
        public string ModuleId { get { return Id; } }
        public string DisplayNameKey { get { return Id; } }
        public void RegisterPlc(IMachineMountContext ctx) { ctx.Station(1); }
        public void RegisterConditions(IMachineMountContext ctx) { }
        public void RegisterData(IMachineMountContext ctx) { }
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx) { yield break; }
    }
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
        // 6 November: "There is no need to mention the label name at each line of their sub properties" - the member's own name, the whole in its tip
        Check("a step unfolded: its members, indented one more, each its own name (the whole in its tip), type and address",
              grid.Rows[x1].Visible && Convert.ToString(grid.Rows[x1].Cells["Label"].Value) == "            X" &&
              grid.Rows[x1].Cells["Label"].ToolTipText == "Recipe[1].X" &&
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

        // 19 October: "a Reset in the labels' right-click menu - we need to reset all the values of a
        // structured label and of an array's devices easily"
        Console.WriteLine("--- reset: 0 into its devices ---");
        Action fill = () =>
        {
            for (int i = 0; i < 48; i++) sim.Write("R" + i, i + 1);
            foreach (var w in new[] { 15, 31, 47 }) sim.Write("R" + w, 0x00FF);
            for (int i = 100; i < 105; i++) sim.Write("D" + i, i);
            Call(form, "RefreshValues"); Application.DoEvents();
        };
        Func<string, int> R = a => { int x; sim.TryRead(a, out x); return x; };
        var words4 = Row("D100", "Word [Signed]", 4, "Counts");
        Rows(sh).Insert(Rows(sh).Count - 1, words4);
        Call(form, "Render", mon, sh);
        fill();
        Call(MonType, "Select", sh, new[] { Rows(sh).IndexOf(rec) });
        var cms = grid.ContextMenuStrip;
        typeof(ToolStripDropDown).GetMethod("OnOpening", Any).Invoke(cms, new object[] { new System.ComponentModel.CancelEventArgs() });
        var resetItem = cms.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Text.StartsWith("Reset"));
        Check("the row menu: Reset", resetItem != null && resetItem.Enabled, string.Join(" | ", cms.Items.OfType<ToolStripItem>().Select(i => i.Text)));
        Call(form, "ResetSelected", mon, sh, false);
        // 19 October: "the words inside a structure no label uses - they should be reset as well"
        Check("the array of 3 steps reset: their whole length 0, R0 ... R47 - the words no label uses (R14, R30, R46) and the bit labels' words too",
              Enumerable.Range(0, 48).All(i => R("R" + i) == 0), string.Join(",", Enumerable.Range(0, 48).Where(i => R("R" + i) != 0).Select(i => "R" + i)));
        Check("  shown at once: step 1's X 0", Shown(mon, m1[1]) == "0", Shown(mon, m1[1]));
        fill();
        Call(MonType, "Select", sh, new[] { Rows(sh).IndexOf(els[1]) });
        Call(form, "ResetSelected", mon, sh, false);
        Check("one step (Recipe[1]): its 16 devices R16 ... R31, the others kept",
              Enumerable.Range(16, 16).All(i => R("R" + i) == 0) && R("R0") == 1 && R("R15") == 0xFF && R("R32") == 33, R("R0") + " " + R("R16") + " " + R("R31"));
        fill();
        Call(MonType, "Select", sh, new[] { Rows(sh).IndexOf(m1[1]), Rows(sh).IndexOf(els[1]) });
        Call(form, "ResetSelected", mon, sh, false);
        Check("  a step and one of its labels chosen: the step once", Enumerable.Range(16, 16).All(i => R("R" + i) == 0) && R("R32") == 33);
        fill();
        Call(MonType, "Select", sh, new[] { Rows(sh).IndexOf(m1[8]) });
        Call(form, "ResetSelected", mon, sh, false);
        Check("a Bit label on its own (Recipe[1].Bitti, R31.3): only its bit - R31 0x00F7, R30 kept", R("R31") == 0xF7 && R("R30") == 31, R("R31").ToString("X"));
        fill();
        Call(MonType, "Select", sh, new[] { Rows(sh).IndexOf(m1[1]) });
        Call(form, "ResetSelected", mon, sh, false);
        Check("one member (Recipe[1].X, a double word): R17 and R18 only", R("R17") == 0 && R("R18") == 0 && R("R16") == 17 && R("R19") == 20);
        fill();
        Call(MonType, "Select", sh, new[] { Rows(sh).IndexOf(words4) });
        Call(form, "ResetSelected", mon, sh, false);
        Check("an array of 4 words (D100): D100 ... D103, not D104", R("D100") == 0 && R("D103") == 0 && R("D104") == 104, R("D100") + " " + R("D104"));
        Rows(sh).Remove(words4);
        Call(form, "Render", mon, sh);

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
            // 19 October: the page redesigned - "remove the Structures and Members captions; New, Copy and
            // Delete on top of the list, Add, Remove, Up and Down on top of the labels, emojis instead of
            // the words; the Member column is Label; Name, Length and Comment each on its own line, the
            // label on the left; and no '... members, ... devices used - in an array ...' line"
            Func<Control, IEnumerable<Control>> all = null;
            all = c => c.Controls.Cast<Control>().SelectMany(k => new[] { k }.Concat(all(k)));
            var texts = all(f).OfType<Label>().Select(l => l.Text).ToList();
            Check("no Structures / Members captions, no layout line", !texts.Contains("Structures") && !texts.Contains("Members") &&
                  !texts.Any(s => s.Contains("devices used")) && f.GetType().GetField("_layout", Any) == null, string.Join("|", texts));
            Check("the first column is Label", membersGrid.Columns["Name"].HeaderText == "Label", membersGrid.Columns["Name"].HeaderText);
            var types = (Control)Get(f, "_types");
            var bNew = (Button)Get(f, "_new"); var bCopy = (Button)Get(f, "_copy"); var bDel = (Button)Get(f, "_delete");
            var bAdd = (Button)Get(f, "_addMember"); var bRem = (Button)Get(f, "_removeMember"); var bUp = (Button)Get(f, "_up"); var bDown = (Button)Get(f, "_down");
            Check("New, Copy, Delete: pictures on top of the list", bNew.Text == "\u2795" && bCopy.Text == "" && bCopy.AccessibleName == "Copy" && bDel.Text == "\U0001F5D1" &&
                  new[] { bNew, bCopy, bDel }.All(b => b.Parent == types.Parent && b.Bottom <= types.Top && b.Left >= types.Left && b.Right <= types.Right),
                  bNew.Text + bCopy.Text + bDel.Text + " " + bNew.Bounds + " " + types.Bounds);
            Check("Add, Remove, Up, Down: pictures on top of the labels", bAdd.Text == "\u2795" && bRem.Text == "\U0001F5D1" && bUp.Text == "" && bDown.Text == "" &&
                  bUp.AccessibleName == "Move up" && bDown.AccessibleName == "Move down" &&
                  new[] { bAdd, bRem, bUp, bDown }.All(b => b.Parent == membersGrid.Parent && b.Bottom <= membersGrid.Top && b.Top > ((Control)Get(f, "_comment")).Bottom),
                  bAdd.Bounds + " " + membersGrid.Bounds);
            Func<string, string, bool> line = (caption, field) =>
            {
                var box = (Control)Get(f, field);
                var l = all(f).OfType<Label>().FirstOrDefault(k => k.Text == caption + ":");
                return l != null && l.Parent == box.Parent && l.Right <= box.Left && l.Top >= box.Top - 2 && l.Bottom <= box.Bottom + 6;
            };
            Check("Name, Length, Comment: each its own line, the label left of its box",
                  line("Name", "_name") && line("Length (devices)", "_size") && line("Comment", "_comment") &&
                  ((Control)Get(f, "_name")).Bottom <= ((Control)Get(f, "_size")).Top && ((Control)Get(f, "_size")).Bottom <= ((Control)Get(f, "_comment")).Top);
            // 19 October: "Up and Down button enabling doesn't work well - they get enabled and disabled
            // at the wrong positions of the selected label" (Windows selects the row before it moves the
            // current cell: the buttons followed the row chosen before)
            var up = (Control)Get(f, "_up"); var down = (Control)Get(f, "_down"); var remove = (Control)Get(f, "_removeMember");
            Func<int, string> pick = i => { membersGrid.ClearSelection(); membersGrid.Rows[i].Selected = true; Application.DoEvents();
                                            return (up.Enabled ? "U" : "-") + (down.Enabled ? "D" : "-") + (remove.Enabled ? "R" : "-"); };
            string first = pick(0), last = pick(8), middle = pick(4);
            Check("Up / Down follow the row selected (the current cell still on another): the first only down, the last only up, between both",
                  first == "-DR" && last == "U-R" && middle == "UDR", first + " " + last + " " + middle);
            Call(f, "MoveMember", -1);
            Check("  Up: the member one higher, still the one chosen", cur.Members[3].Name == "I" && (int)Get(f, "SelectedMember") == 3 && up.Enabled && down.Enabled,
                  cur.Members[3].Name + " " + Get(f, "SelectedMember"));
            pick(1); Call(f, "MoveMember", -1);
            Check("  Up to the top: Up off there", cur.Members[0].Name == "X" && (int)Get(f, "SelectedMember") == 0 && !up.Enabled && down.Enabled);
            Call(f, "MoveMember", +1); pick(3); Call(f, "MoveMember", +1);
            Check("  back down: the order as it was", string.Join(",", cur.Members.Select(m => m.Name)) == "HareketKodu,X,Y,Z,I,J,K,Hiz,Bitti",
                  string.Join(",", cur.Members.Select(m => m.Name)));
            Check("  the rows show the order: X second, its offset and no bit", Convert.ToString(membersGrid.Rows[1].Cells["Name"].Value) == "X" &&
                  Convert.ToString(membersGrid.Rows[1].Cells["Offset"].Value) == "1" && membersGrid.Rows[1].Cells["Bit"].ReadOnly &&
                  Convert.ToString(membersGrid.Rows[8].Cells["Bit"].Value) == "3" && !membersGrid.Rows[8].Cells["Bit"].ReadOnly);

            // 19 October: "it gives an error when I move the label up or down or delete it - a reentrant call
            // to SetCurrentCellAddressCore - and then does it anyway": the buttons were switched inside the
            // table's own change of its current cell, and a button switched off with the focus handed the
            // focus to the table right then
            var inTable = new List<string>();
            EventHandler watch = (s, e) =>
            {
                var st = Environment.StackTrace;
                if (st.Contains("OnCurrentCellChanged") || st.Contains("OnSelectionChanged") || st.Contains("SetCurrentCellAddressCore") || st.Contains("OnRowsRemoved"))
                    inTable.Add(((Control)s).Name + ((Button)s).AccessibleName + ((Button)s).Text);
            };
            foreach (var b in new[] { up, down, remove }) b.EnabledChanged += watch;
            pick(1); up.Focus(); Call(f, "MoveMember", -1); Application.DoEvents();
            bool topOff = !up.Enabled && down.Enabled && cur.Members[0].Name == "X";
            pick(7); down.Focus(); Call(f, "MoveMember", +1); Application.DoEvents();
            bool bottomOff = up.Enabled && !down.Enabled && cur.Members[8].Name == "Hiz";
            pick(8); Call(f, "MoveMember", -1); pick(0); Call(f, "MoveMember", +1); Application.DoEvents();
            Call(f, "AddMember"); Application.DoEvents();
            string past = (string)Call(f, "FirstProblem") ?? "";
            Check("  a label added past the length (+16, of 16): the length stays, the problem names the label",
                  cur.Members.Count == 10 && cur.Size == 16 && past.Contains("its labels reach 17") && past.Contains("'Member10' at +16") &&
                  ((Label)Get(f, "_problem")).Text == past, past);
            remove.Focus(); Call(f, "RemoveMember"); Application.DoEvents();
            Check("Up to the top, Down to the bottom, a label removed: done, the buttons never switched inside the table's change (Windows' reentrant call)",
                  inTable.Count == 0 && topOff && bottomOff && cur.Members.Count == 9 && (int)Get(f, "SelectedMember") == 8 && !down.Enabled && remove.Enabled &&
                  string.Join(",", cur.Members.Select(m => m.Name)) == "HareketKodu,X,Y,Z,I,J,K,Hiz,Bitti" && Call(f, "FirstProblem") == null,
                  string.Join(" | ", inTable) + " " + topOff + bottomOff + " " + string.Join(",", cur.Members.Select(m => m.Name)));
            foreach (var b in new[] { up, down, remove }) b.EnabledChanged -= watch;

            Call(f, "NewType", (object)null);
            cur = (StructuredTypes.Type)Get(f, "Current");
            Call(f, "AddMember"); Call(f, "AddMember");
            Check("a new one: named, its labels one after the other (+0, +1); one device long, so the second is past it: said, the length kept",
                  cur.Name == "Structure" && cur.Members.Count == 2 && cur.Members[1].Offset == 1 && cur.Size == 1 && ((NumericUpDown)Get(f, "_size")).Value == 1 &&
                  ((string)Call(f, "FirstProblem") ?? "").Contains("its labels reach 2"),
                  cur.Name + " " + cur.Members.Count + " " + cur.Size + " " + (string)Call(f, "FirstProblem"));
            ((NumericUpDown)Get(f, "_size")).Value = 3;
            Check("  made 3 long: complete", cur.Size == 3 && Call(f, "FirstProblem") == null, (string)Call(f, "FirstProblem"));
            membersGrid.Rows[1].Cells["DataType"].Value = "Float [Single Precision]";
            Check("  a label made a float (+1, 2 devices): fits, still 3 long", cur.Members[1].DataType == "Float [Single Precision]" && cur.Size == 3 && Call(f, "FirstProblem") == null,
                  cur.Size.ToString());
            membersGrid.Rows[1].Cells["Offset"].Value = "2";
            Check("  moved to +2: past the length - not made longer, said", cur.Size == 3 && ((string)Call(f, "FirstProblem") ?? "").Contains("reach 4 ('Member2' at +2, 2 devices)"),
                  (string)Call(f, "FirstProblem"));
            membersGrid.Rows[1].Cells["Offset"].Value = "0";
            Check("  two labels on one device: said, and OK refuses it", ((string)Call(f, "FirstProblem") ?? "").Contains("use the same device"),
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

        // 19 October: "they should belong to each machine - shipped with its project like the PLC labels"
        Console.WriteLine("--- each machine its own ---");
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod { Id = "MAIN" }, null, Path.Combine(dir, "bin"));
        MachineRegistry.MountSub(new Mod { Id = "SUB" }, new[] { 23 }, Path.Combine(dir, "bin"));
        StructuredTypes.Reload();
        using (var main = new PlcRowEditorForm("Main", "", "Word [Signed]", "D0", "", isNew: true))
        using (var sub = new PlcRowEditorForm("Bench", "", "Word [Signed]", "R0", "", isNew: true, station: 23))
        {
            var mainTypes = ((ComboBox)Get(main, "_dataType")).Items.Cast<object>().Select(Convert.ToString).ToList();
            var subTypes = ((ComboBox)Get(sub, "_dataType")).Items.Cast<object>().Select(Convert.ToString).ToList();
            Check("the main machine's types (the first version's file taken over) on its tabs, not on the mounted machine's (station 23)",
                  mainTypes.Contains("Kesim") && mainTypes.Contains("FrezeAdim") && !subTypes.Contains("Kesim") && !subTypes.Contains("FrezeAdim") && subTypes.Contains("Bit"),
                  string.Join(",", subTypes.Skip(Math.Max(0, subTypes.Count - 3))));
        }
        using (var f = new StructuredTypesForm(null, 23))
            Check("  its editor: that machine's, named in the title", f.Text.EndsWith(" - SUB") && ((List<StructuredTypes.Type>)Get(f, "Working")).Count == 0, f.Text);
        Check("  a row of a type the station's machine does not have is no structure on it",
              Get(Row("R0", "FrezeAdim"), "Structure") == null && StructuredTypes.Find("FrezeAdim", 1) != null);
        MachineRegistry.Reset();
        StructuredTypes.Reload();

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
