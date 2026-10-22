using System;
using System.IO;
using System.Linq;
using Kaban.Core.Helpers;

class M {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    static void Main(){
        var root = Path.Combine(Path.GetTempPath(), "mig_" + Guid.NewGuid().ToString("N"));
        string src = Path.Combine(root, "old"), dst = Path.Combine(root, "new");
        Directory.CreateDirectory(Path.Combine(src, "Sub"));
        Directory.CreateDirectory(dst);
        // an older install: its stations (3 columns: the first format), its alarms, its translation files
        File.WriteAllLines(Path.Combine(src, "PlcSettings.cfg"), new[] { "# stations", "1\tDrv_Mitsubishi\t192.168.1.31" });
        File.WriteAllText(Path.Combine(src, "Alarms.json"), "[ { \"PlcAddress\": \"M1\", \"TagCode\": \"A\" } ]");
        File.WriteAllText(Path.Combine(src, "Sub", "Alarms.json"), "[ { \"PlcAddress\": \"M2\", \"TagCode\": \"B\" } ]");
        File.WriteAllLines(Path.Combine(src, "Translations.csv"), new[] { "Key;EN;TR", "Btn_Start;Start;Basla" });
        File.WriteAllLines(Path.Combine(src, "CommonTranslations.csv"), new[] { "Key;EN;TR", "Btn_Ok;OK;Tamam" });

        var found = SettingsMigration.Inspect(src, dst);
        Console.WriteLine("found " + found.Count + " files");
        foreach (var f in found)
            Console.WriteLine("    {0,-22} v{1}  {2,3} rows  {3}", f.FileName, f.DetectedVersion, f.Rows, f.Note);

        Check("the station file is seen as v1",
              found.Any(f => f.FileName == "PlcSettings.cfg" && f.DetectedVersion == 1));
        Check("an alarms file without help keys is v1",
              found.Any(f => f.FileName == "Alarms.json" && f.DetectedVersion == 1));
        // 22 October: the translations are imported on HMI Settings > Translation, key by key
        Check("the translation files are not offered here (the Translation page imports them)",
              !found.Any(f => f.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) && f.FileName.Contains("Translations")),
              string.Join(", ", found.Select(f => f.FileName)));
        Check("everything old is flagged for conversion",
              found.Where(f => f.DetectedVersion < SettingsMigration.CurrentFormatVersion)
                   .All(f => f.NeedsUpgrade));

        // a machine's own folder must not collide with another's
        var sub = found.First(f => f.SourcePath.Contains("Sub"));
        Check("a sub-folder file keeps its folder", sub.TargetPath.Contains("Sub"), sub.TargetPath);

        SettingsMigration.Apply(found, dst);

        var st = File.ReadAllLines(Path.Combine(dst, "PlcSettings.cfg"))
                     .Where(l => !l.StartsWith("#") && l.Trim().Length > 0).ToArray();
        Check("stations gained the two new columns",
              st.All(l => l.Split('\t').Length == 5), st[0].Replace("\t"," | "));
        Check("  and the existing values are untouched",
              st[0].StartsWith("1\tDrv_Mitsubishi\t192.168.1.31"), st[0].Replace("\t"," | "));
        Check("  with the station left enabled", st[0].Split('\t')[3] == "1");
        Check("no translation file copied over this program's", !File.Exists(Path.Combine(dst, "Translations.csv")) && !File.Exists(Path.Combine(dst, "CommonTranslations.csv")));

        // a second import must back up what is there
        var before = Directory.GetDirectories(dst, "Backup_*").Length;
        SettingsMigration.Apply(found, dst);
        var after = Directory.GetDirectories(dst, "Backup_*").Length;
        Check("re-importing backs up the current files first", after > before,
              before + " -> " + after);

        try { Directory.Delete(root, true); } catch { }
        Console.WriteLine(fail==0?"\nMIGRATION ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}
