using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.UI.Controls;
using Kaban.UI.Design;

// THE PROPERTIES WINDOW IN THE ORDER OF THE WORK (29 September): every HMI
// control's own properties in its layout's categories and order, the
// Windows ones after them and unmixed, the names in code unchanged - and
// the formula variables list starting with A.
class PO {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    // As the Properties window sorts: by the text shown, ignoring case, in the panel's culture.
    static int Cmp(string a, string b) { return string.Compare(a, b, true, CultureInfo.CurrentCulture); }

    [STAThread]
    static void Main() {
        var asm = typeof(ButtonHMI).Assembly;
        var types = new[] { typeof(NumericDisplayHMI), typeof(NumericInputHMI), typeof(ButtonHMI), typeof(CheckBoxHMI), typeof(CheckListHMI),
                            typeof(ComboBoxHMI), typeof(GroupBoxHMI), typeof(HexCodeHMI), typeof(LabelHMI), typeof(LampHMI), typeof(MeterHMI),
                            typeof(MeterInputHMI), typeof(AxisUnitHMI), typeof(AxisSettingHMI), typeof(CylinderUnitHMI) };

        foreach (var t in types)
        {
            Console.WriteLine("--- " + t.Name + " ---");
            var c = (Control)Activator.CreateInstance(t);
            var props = TypeDescriptor.GetProperties(c).Cast<PropertyDescriptor>().Where(p => p.IsBrowsable).ToList();
            var ours = props.Where(p => p.ComponentType.Assembly == asm).ToList();
            var windows = props.Where(p => p.ComponentType.Assembly != asm).ToList();

            var unplaced = ours.Where(p => !p.Category.StartsWith("\t")).Select(p => p.Name).ToList();
            Check("every property of ours has its place in the layout", unplaced.Count == 0, string.Join(", ", unplaced));
            Check("  the names in code are unchanged (the designer files still load)",
                  ours.All(p => t.GetProperty(p.Name, BindingFlags.Public | BindingFlags.Instance) != null));
            Check("  the Windows properties keep their own categories, none of ours among them",
                  windows.All(p => !p.Category.StartsWith("\t")) &&
                  !ours.Any(p => windows.Any(w => w.Category == p.Category)));

            // Categories as the window lists them: ours first, in the layout's order.
            var cats = props.Select(p => p.Category).Distinct().ToList();
            cats.Sort(Cmp);
            var ourCats = cats.Where(x => x.StartsWith("\t")).Select(x => x.TrimStart('\t')).ToList();
            var layout = PropertyOrder.LayoutFor(t);
            var expectCats = layout.Select(l => l.Key).OrderBy(k => Array.IndexOf(PropertyOrder.CategoryOrder, k)).ToList();
            Check("  categories: " + string.Join(" > ", expectCats), ourCats.SequenceEqual(expectCats), string.Join(" > ", ourCats));
            Check("  then the Windows ones", cats.Take(ourCats.Count).All(x => x.StartsWith("\t")) && cats.Skip(ourCats.Count).All(x => !x.StartsWith("\t")));

            bool orderOk = true; string bad = "";
            foreach (var l in layout)
            {
                var inCat = ours.Where(p => p.Category.TrimStart('\t') == l.Key).ToList();
                inCat.Sort((a, b) => Cmp(a.DisplayName, b.DisplayName));
                var shown = inCat.Select(p => p.Name).ToList();
                var want = l.Value.Where(n => shown.Contains(n)).ToList();
                if (!shown.Take(want.Count).SequenceEqual(want)) { orderOk = false; bad = l.Key + ": " + string.Join(", ", shown); }
            }
            Check("  inside each category the layout's order", orderOk, bad);
            c.Dispose();
        }

        Console.WriteLine("--- the numeric input's formulas: the variables first ---");
        var nip = TypeDescriptor.GetProperties(new NumericInputHMI()).Cast<PropertyDescriptor>()
                                .Where(p => p.Category.TrimStart('\t') == "Formula").ToList();
        nip.Sort((a, b) => Cmp(a.DisplayName, b.DisplayName));
        Check("MathVariables, ReadFormula, WriteFormula, FormulaCheck", string.Join(",", nip.Select(p => p.Name)) == "MathVariables,ReadFormula,WriteFormula,FormulaCheck",
              string.Join(",", nip.Select(p => p.Name)));

        Console.WriteLine("--- the variables list: A first, then B, C ... ---");
        // By name: the editor is a designer class (System.Design), not referenced here.
        var edType = typeof(ValueVariable).Assembly.GetType("Kaban.UI.Design.MathVariablesEditor");
        Func<ICollection<string>, string> next = tk => (string)edType.GetMethod("NextName").Invoke(null, new object[] { tk });
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A" };
        var names = new List<string>();
        for (int i = 0; i < 4; i++) { var n = next(taken); taken.Add(n); names.Add(n); }
        Check("new variables are named B, C, D, E", string.Join(",", names) == "B,C,D,E", string.Join(",", names));
        Check("  a letter taken is skipped (B and C there: D next)", next(new HashSet<string> { "A", "B", "C" }) == "D");
        var ed = Activator.CreateInstance(edType, typeof(List<Kaban.Core.Models.MathVariable>));
        var list = new List<Kaban.Core.Models.MathVariable> { new Kaban.Core.Models.MathVariable { Name = "B", ConstantValue = 10 } };
        var items = (object[])edType.GetMethod("GetItems", Any).Invoke(ed, new object[] { list });
        Check("the list shows A (the value itself) first, then B", items.Length == 2 && items[0] is ValueVariable && ((Kaban.Core.Models.MathVariable)items[1]).Name == "B");
        Check("  A cannot be removed", !(bool)edType.GetMethod("CanRemoveInstance", Any).Invoke(ed, new object[] { items[0] }) &&
                                       (bool)edType.GetMethod("CanRemoveInstance", Any).Invoke(ed, new object[] { items[1] }));
        var aProps = TypeDescriptor.GetProperties(items[0]).Cast<PropertyDescriptor>().ToList();
        Check("  A shows its name and meaning, read only", aProps.Count == 2 && aProps.All(p => p.IsReadOnly) &&
              (string)aProps.First(p => p.Name == "Meaning").GetValue(items[0]) == ValueVariable.Meaning);
        edType.GetMethod("SetItems", Any).Invoke(ed, new object[] { list, items });
        Check("  and A is never stored: the field keeps only B", list.Count == 1 && list[0].Name == "B", string.Join(",", list.Select(v => v.Name)));

        Console.WriteLine(fail == 0 ? "\nPROPERTY ORDER ALL PASS" : "\nPROPERTY ORDER " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
