using System;
using System.IO;
using Kaban.Core.Helpers;

// 23 October: "I think we should make sure the previous data is backed up as the safe heaven before we are doing the
// imports (automatically take the backup or ward the user to take it if there isn't any backup file in it's directory)" -
// a translation file's first version of the day kept before it is written.
class SC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "sc_" + Guid.NewGuid().ToString("N"));
        string prog = Path.Combine(dir, "prog"), keep = Path.Combine(dir, "keep");
        Directory.CreateDirectory(Path.Combine(prog, "H500"));
        AuditLogger.LogDirectory = dir;
        SafetyCopies.Folder = keep;
        SafetyCopies.ProgramFolder = prog;
        var common = Path.Combine(prog, "CommonTranslations.csv");
        var machine = Path.Combine(prog, "H500", "Translations.csv");
        File.WriteAllText(common, "morning");
        File.WriteAllText(machine, "machine morning");
        Check("the first write of the day: the file as it was kept, by its path under the program's folder",
              SafetyCopies.FirstOfDay("Translations", common) && SafetyCopies.FirstOfDay("Translations", machine) &&
              File.ReadAllText(Path.Combine(keep, "Translations", DateTime.Today.ToString("yyyy-MM-dd"), "CommonTranslations.csv")) == "morning" &&
              File.ReadAllText(Path.Combine(keep, "Translations", DateTime.Today.ToString("yyyy-MM-dd"), "H500", "Translations.csv")) == "machine morning");
        File.WriteAllText(common, "noon");
        Check("  a later write the same day: the morning's copy left as it is", !SafetyCopies.FirstOfDay("Translations", common) &&
              File.ReadAllText(SafetyCopies.CopyOf("Translations", common)) == "morning");
        Check("  a file not there yet: nothing to keep", !SafetyCopies.FirstOfDay("Translations", Path.Combine(prog, "none.csv")));
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nSAFETY COPIES ALL PASS" : "\nSAFETY COPIES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
