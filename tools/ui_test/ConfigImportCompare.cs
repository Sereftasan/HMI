using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 23 October: "I couldn't quite understand what 'Look for settings' button do in 'Config import' page. We need a
// compare tool here I think, so the user could see what is different in between the old and new one of the files"
class CIC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { d = d ?? ""; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) { var t = o.GetType(); var pr = t.GetProperty(n, Any); return pr != null ? pr.GetValue(o, null) : t.GetField(n, Any).GetValue(o); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length).Invoke(o, a); }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "cic_" + Guid.NewGuid().ToString("N"));
        var here = Path.Combine(dir, "program"); var old = Path.Combine(dir, "old", "bin");
        Directory.CreateDirectory(here); Directory.CreateDirectory(old);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        File.WriteAllLines(Path.Combine(here, "PlcSettings.cfg"), new[] { "# stations", "1\tMX\t1\t1\tD9000", "2\tMX\t2\t1\tD9000", "3\tSIM\t0\t1\t" });
        File.WriteAllLines(Path.Combine(old, "PlcSettings.cfg"), new[] { "# stations", "1\tMX\t1\t1\tD9000", "2\tMX\t5\t1\tD9000", "3\tSIM\t0\t1\t" });
        File.WriteAllLines(Path.Combine(here, "PlcLabels.csv"), new[] { "Label;Type;Address", "Start;Bit;M0", "Speed;Word;D100" });
        File.WriteAllLines(Path.Combine(old, "PlcLabels.csv"), new[] { "Label;Type;Address", "Start;Bit;M0", "Speed;Word;D100" });
        File.WriteAllLines(Path.Combine(old, "Alarms.json"), new[] { "[", "  { \"Code\": 1, \"TroubleshootingKeys\": [] }", "]" });
        File.WriteAllText(Path.Combine(old, "KabanHMI.exe"), "");                  // the older program's own folder
        Directory.CreateDirectory(Path.Combine(old, "H500"));
        File.WriteAllLines(Path.Combine(old, "H500", "PlcLabels.csv"), new[] { "Label;Type;Address", "Lube;Bit;M7" });
        ImportSettingsControl.TargetFolder = here;

        var page = new ImportSettingsControl();
        var host = new Form { Width = 1100, Height = 600 };
        host.Controls.Add(page);
        host.Show(); Application.DoEvents();
        var about = Tree(page).FirstOrDefault(c => c.Name == "impAbout");
        var scan = Tree(page).OfType<Button>().FirstOrDefault(b => b.Name == "impScan");
        Check("the page says what it does, step by step; the button says what it does", about != null && about.Text.Contains("1.") && about.Text.Contains("Compare") &&
              scan != null && scan.Text == "List its settings files" && !Tree(page).OfType<Button>().Any(b => b.Text == "Look for settings"), scan == null ? "" : scan.Text);

        Console.WriteLine("--- listed: each compared ---");
        page.GetType().GetProperty("Source", Any).SetValue(page, Path.Combine(dir, "old"), null);
        Call(page, "Scan");
        var found = (List<SettingsMigration.Finding>)Get(page, "Found");
        Func<string, SettingsMigration.Finding> file = n => found.First(f => f.FileName == n && Path.GetDirectoryName(f.TargetPath) == here);
        Func<string, string> diffOf = n => (string)typeof(ImportSettingsControl).GetMethod("DiffText", Any).Invoke(null, new object[] { file(n) });
        Check("four files found (the whole installation chosen: the program in a folder under it)", found.Count == 4, string.Join(", ", found.Select(f => f.FileName)));
        Check("  each to its place in this program's folder - a sub-machine's in its own", found.Count(f => f.TargetPath == Path.Combine(here, "PlcSettings.cfg")) == 1 &&
              found.Count(f => f.TargetPath == Path.Combine(here, "H500", "PlcLabels.csv")) == 1, string.Join(", ", found.Select(f => f.TargetPath.Substring(here.Length))));
        Check("  PLC connections: 1 line changed - ticked", diffOf("PlcSettings.cfg") == "1 line(s) changed" && file("PlcSettings.cfg").Selected, diffOf("PlcSettings.cfg"));
        Check("  the label list: the same - unticked, nothing to import", diffOf("PlcLabels.csv").StartsWith("the same") && !file("PlcLabels.csv").Selected, diffOf("PlcLabels.csv"));
        Check("  the alarms: new here", diffOf("Alarms.json").StartsWith("new here"), diffOf("Alarms.json"));
        Check("  said", ((string)Get(page, "StatusText")).Contains("4 file(s) found, 3 different"), (string)Get(page, "StatusText"));
        var grid = (DataGridView)Get(page, "Grid");
        Check("  the table's column says so", grid.Columns.Contains("Diff") && Convert.ToString(grid.Rows[found.IndexOf(file("PlcSettings.cfg"))].Cells["Diff"].Value) == "1 line(s) changed");

        Console.WriteLine("--- compared ---");
        SettingsCompareForm shown = null;
        SettingsCompareForm.Showing = f => {
            shown = f;
            f.Show(); Application.DoEvents();
            int at = (int)Call(f, "Step", 1);
            var rows = (List<TextDiff.Row>)Get(f, "Shown");
            Check("this program's file beside the older one's: the line changed found, the lines round it", at >= 0 && rows[at].Kind == TextDiff.Kind.Changed &&
                  rows[at].LeftText.Contains("\t2\t") && rows[at].RightText.Contains("\t5\t") && rows.Count == 4,
                  rows.Count + " rows: " + string.Join(" | ", rows.Where(r => r != null).Select(r => r.Kind.ToString())));
            Check("  counted", ((string)Get(f, "SummaryText")).StartsWith("1 line(s) changed, 0 only in this program's, 0 only in the older one's"), (string)Get(f, "SummaryText"));
            ((CheckBox)Tree(f).First(c => c.Name == "cmpOnlyDiff")).Checked = false;
            Check("  every line", ((List<TextDiff.Row>)Get(f, "Shown")).Count == 4);
            f.Close();
        };
        Call(page, "Compare", found.IndexOf(file("PlcSettings.cfg")));
        Check("  (opened from the row)", shown != null);
        shown = null;
        SettingsCompareForm.Showing = f => shown = f;
        Call(page, "Compare", found.IndexOf(file("Alarms.json")));
        Check("a file this program has none of: all of it added", shown != null && ((TextDiff.Summary)Get(shown, "Counted")).OnlyRight == 3);

        Console.WriteLine("--- imported ---");
        string asked = null;
        Kaban.UI.Profiles.Ask.Hook = (t, q, i) => { if (i == "yes") asked = q; return "yes"; };
        Call(page, "Apply");
        var backups = Directory.GetDirectories(here, "Backup_*");
        Check("asked first, with what changes in each", asked != null && asked.Contains("PlcSettings.cfg: 1 line(s) changed") && asked.Contains("PlcLabels.csv: new here") && !asked.Contains("the same"), asked);
        Check("  the files written, this program's kept first (Backup_<date>)", File.ReadAllText(Path.Combine(here, "PlcSettings.cfg")).Contains("\t5\t") &&
              File.Exists(Path.Combine(here, "Alarms.json")) && backups.Length == 1 && File.ReadAllText(Path.Combine(backups[0], "PlcSettings.cfg")).Contains("2\tMX\t2\t"));
        found = (List<SettingsMigration.Finding>)Get(page, "Found");
        Check("  listed again: the same now", found.All(f => f.Diff != null && f.Diff.Identical), string.Join(", ", found.Select(f => f.FileName + " " + diffOf(f.FileName))));
        Kaban.UI.Profiles.Ask.Hook = null;
        SettingsCompareForm.Showing = null;

        Console.WriteLine("--- beside a side page ---");
        var narrow = new ImportSettingsControl();
        PageScroll.Enable(narrow);
        Check("its least width fits beside a side page (it was 898)", narrow.AutoScrollMinSize.Width <= 560, narrow.AutoScrollMinSize.ToString());

        host.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nCONFIG IMPORT COMPARE ALL PASS" : "\nCONFIG IMPORT COMPARE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
