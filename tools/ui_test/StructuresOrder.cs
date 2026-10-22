using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Forms;

// 23 October: "It would be nice if I could be able to change structured data types list, it could be too
// messy when added too many" (up / down arrows drawn beside the list). And the user's renamed types
// ("Milling Step" - "Milling Steps"): what named the old name still finds the structure.
class SO {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        d = d ?? "";
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        var t = o.GetType();
        var f = t.GetField(n, Any); if (f != null) return f.GetValue(o);
        return t.GetProperty(n, Any).GetValue(o, null);
    }
    static StructuredTypes.Type Make(string name)
    {
        var t = new StructuredTypes.Type { Name = name, Size = 2 };
        t.Members.Add(new StructuredTypes.Member { Name = "Value", DataType = "Word [Signed]", Offset = 0 });
        return t;
    }
    static string Names() { return string.Join(", ", StructuredTypes.All().Select(t => t.Name)); }
    static string Listed(StructuredTypesForm f) { return string.Join(", ", ((ListBox)Get(f, "_types")).Items.Cast<object>().Select(Convert.ToString)); }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "so_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        StructuredTypes.FilePath = Path.Combine(dir, "StructuredTypes.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        StructuredTypes.Save(new[] { Make("Milling Step"), Make("Profile"), Make("Cutting Step") });

        Console.WriteLine("--- the list's order ---");
        using (var f = new StructuredTypesForm("Profile"))
        {
            f.Show(); Application.DoEvents();
            var up = (Button)Get(f, "_typeUp"); var down = (Button)Get(f, "_typeDown");
            var bin = (Button)Get(f, "_delete");
            Check("up and down beside the list's buttons, on its line", up.Top == bin.Top && up.Left > bin.Right && down.Left > up.Right &&
                  down.Right <= ((ListBox)Get(f, "_types")).Right + 2, up.Bounds + " " + down.Bounds + " " + ((ListBox)Get(f, "_types")).Bounds);
            Check("  the middle one chosen: both on", up.Enabled && down.Enabled);
            up.PerformClick(); Application.DoEvents();
            Check("up: Profile first, still the one chosen and shown", Listed(f) == "Profile, Milling Step, Cutting Step" &&
                  ((StructuredTypes.Type)Get(f, "Current")).Name == "Profile" && ((TextBox)Get(f, "_name")).Text == "Profile", Listed(f));
            Check("  at the top: up off, down on", !up.Enabled && down.Enabled);
            down.PerformClick(); down.PerformClick(); Application.DoEvents();
            Check("down twice: Profile last - down off", Listed(f) == "Milling Step, Cutting Step, Profile" && up.Enabled && !down.Enabled, Listed(f));
            Check("  nothing kept before OK", Names() == "Milling Step, Profile, Cutting Step", Names());
            f.DialogResult = DialogResult.OK;
            f.Close();
        }
        StructuredTypes.Reload();
        Check("OK: kept in that order - the data type lists offer them so", Names() == "Milling Step, Cutting Step, Profile", Names());

        Console.WriteLine("--- a structure renamed ---");
        using (var f = new StructuredTypesForm("Milling Step"))
        {
            f.Show(); Application.DoEvents();
            ((TextBox)Get(f, "_name")).Text = "Milling Steps";
            ((Button)Get(f, "_copy")).PerformClick(); Application.DoEvents();
            f.DialogResult = DialogResult.OK;
            f.Close();
        }
        StructuredTypes.Reload();
        var milling = StructuredTypes.Find("Milling Steps");
        Check("renamed: its old name kept with it", milling != null && milling.FormerNames.SequenceEqual(new[] { "Milling Step" }),
              milling == null ? "-" : string.Join(",", milling.FormerNames));
        Check("  a label (a monitor row, a saved tab) of the old name still finds it - an array of it too",
              ReferenceEquals(StructuredTypes.Find("Milling Step"), milling) && ReferenceEquals(StructuredTypes.Find("Milling Step(0..399)"), milling));
        var copy = StructuredTypes.All().FirstOrDefault(t => t.Name == "Milling Steps_Copy");
        Check("  its copy was never called anything else", copy != null && copy.FormerNames.Count == 0);
        var lines = File.ReadAllLines(StructuredTypes.FilePath);
        int at = Array.IndexOf(lines, "[Milling Steps];2;");
        Check("  in the file: '#formerly;Milling Step' under its line (a program of before reads it as a comment)",
              at >= 0 && lines[at + 1] == "#formerly;Milling Step", at < 0 ? string.Join(" / ", lines) : lines[at + 1]);
        Check("  read by a program of before: the same structures", StructuredTypes.Parse(lines.Where(l => !l.StartsWith("#formerly"))).Count == StructuredTypes.All().Count);

        using (var f = new StructuredTypesForm("Profile"))
        {
            f.Show(); Application.DoEvents();
            ((TextBox)Get(f, "_name")).Text = "Milling Step";          // a new structure takes the old name
            f.DialogResult = DialogResult.OK;
            f.Close();
        }
        StructuredTypes.Reload();
        Check("a structure named as another one was before: the name is its own now, the other's former name dropped",
              StructuredTypes.Find("Milling Step").Name == "Milling Step" && StructuredTypes.Find("Milling Steps").FormerNames.Count == 0 &&
              StructuredTypes.Find("Profile").Name == "Milling Step",
              string.Join(",", StructuredTypes.Find("Milling Steps").FormerNames));
        using (var f = new StructuredTypesForm("Milling Steps"))
        {
            f.Show(); Application.DoEvents();
            ((TextBox)Get(f, "_name")).Text = "Milling";
            f.DialogResult = DialogResult.OK;
            f.Close();
        }
        StructuredTypes.Reload();
        Check("renamed again: both names of before kept? only the last one's (the first was taken by another)",
              StructuredTypes.Find("Milling").FormerNames.SequenceEqual(new[] { "Milling Steps" }), string.Join(",", StructuredTypes.Find("Milling").FormerNames));

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nSTRUCTURES ORDER ALL PASS" : "\nSTRUCTURES ORDER " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
