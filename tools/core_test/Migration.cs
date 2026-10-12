using System;
using System.IO;
using System.Linq;
using Kaban.Core.Helpers;

class M {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    static void Main(){
        string src = "/tmp/mig/old", dst = "/tmp/mig/new";
        // Start clean: backups from an earlier run would make the count meaningless.
        if (Directory.Exists(dst)) Directory.Delete(dst, true);
        Directory.CreateDirectory(dst);
        var found = SettingsMigration.Inspect(src, dst);
        Console.WriteLine("found " + found.Count + " files");
        foreach (var f in found)
            Console.WriteLine("    {0,-22} v{1}  {2,3} rows  {3}", f.FileName, f.DetectedVersion, f.Rows, f.Note);

        Check("the station file is seen as v1",
              found.Any(f => f.FileName == "PlcSettings.cfg" && f.DetectedVersion == 1));
        Check("the 3-column CSV is v1",
              found.Any(f => f.SourcePath.EndsWith("old/Translations.csv") && f.DetectedVersion == 1));
        Check("the 5-column CSV is v2",
              found.Any(f => f.SourcePath.Contains("Sub") && f.DetectedVersion == 2));
        Check("an alarms file without help keys is v1",
              found.Any(f => f.FileName == "Alarms.json" && f.DetectedVersion == 1));
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

        var csv = File.ReadAllLines(Path.Combine(dst, "Translations.csv"))
                      .Where(l => l.Trim().Length > 0).ToArray();
        Check("the CSV gained all six language columns",
              csv.All(l => l.Split(';').Length == 7), csv[1]);
        Check("  padded with EMPTY cells, not copies of English",
              csv[1].Split(';')[3] == "" && csv[1].Split(';')[1] == "Start", csv[1]);

        // a second import must back up what is there
        var before = Directory.GetDirectories(dst, "Backup_*").Length;
        SettingsMigration.Apply(found, dst);
        var after = Directory.GetDirectories(dst, "Backup_*").Length;
        Check("re-importing backs up the current files first", after > before,
              before + " -> " + after);

        Console.WriteLine(fail==0?"\nMIGRATION ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

