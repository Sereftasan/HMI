using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Settings;

// 19 November ("analyze KabanMachineSystem allover and add everything you find useful"): the old program's Backup Settings - the HMI's
// data as one zip, daily, restored - and its KabanTransferCleaner - the transfer folder's old job files into its Archive.
class DBA {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "dba_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        Console.WriteLine("--- the HMI's data backed up ---");
        var data = Path.Combine(dir, "KabanHMI");
        DataBackup.DataFolder = data;
        Directory.CreateDirectory(Path.Combine(data, "Labels"));
        Directory.CreateDirectory(Path.Combine(data, "AuditLogs"));
        File.WriteAllText(Path.Combine(data, "Barcodes.cfg"), "prefix\tA\r\n");
        File.WriteAllText(Path.Combine(data, "Labels", "Piece.label"), "name\tPiece\r\n");
        File.WriteAllText(Path.Combine(data, "AuditLogs", "today.log"), "x");
        File.WriteAllBytes(Path.Combine(data, "Kaban.db"), new byte[] { 1, 2, 3 });
        DataBackup.Reload();
        string zip;
        using (var open = new FileStream(Path.Combine(data, "Kaban.db"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            zip = DataBackup.Create("test");
        var inside = DataBackup.Contents(zip);
        Check("one zip in Backups: the settings, the labels, the library (open in the program) - not the audit logs",
              File.Exists(zip) && zip.StartsWith(Path.Combine(data, "Backups")) && inside.Contains("Barcodes.cfg") && inside.Contains("Labels/Piece.label") &&
              inside.Contains("Kaban.db") && !inside.Any(x => x.StartsWith("AuditLogs")) && !inside.Any(x => x.StartsWith("Backups")), string.Join(", ", inside));
        File.WriteAllText(Path.Combine(data, "Barcodes.cfg"), "prefix\tCHANGED\r\n");
        File.Delete(Path.Combine(data, "Labels", "Piece.label"));
        int n = DataBackup.Restore(zip);
        Check("restored: the files as they were in the backup", n == 3 && File.ReadAllText(Path.Combine(data, "Barcodes.cfg")).Contains("prefix\tA") &&
              File.Exists(Path.Combine(data, "Labels", "Piece.label")), n.ToString());
        var before = DataBackup.List().FirstOrDefault(b => b.Name.Contains("_beforerestore"));
        Check("  the data as it was before backed up first", before != null && DataBackup.Contents(before.Path).Contains("Barcodes.cfg"));
        DataBackup.SetSettings(Path.Combine(dir, "Stick"), true, 2);
        DataBackup.Reload();
        Check("the settings kept: another folder, daily, 2 kept", DataBackup.Folder == Path.Combine(dir, "Stick") && DataBackup.Daily && DataBackup.Keep == 2);
        Check("  daily: one made today, not a second", DataBackup.DailyNow(DateTime.Now) && !DataBackup.DailyNow(DateTime.Now) && DataBackup.List().Count == 1);
        DataBackup.Create("a"); System.Threading.Thread.Sleep(1100); DataBackup.Create("b"); System.Threading.Thread.Sleep(1100); DataBackup.Create("c");
        Check("  the newest 2 kept", DataBackup.List().Count == 2 && DataBackup.List()[0].Name.Contains("_c"), string.Join(", ", DataBackup.List().Select(b => b.Name)));

        Console.WriteLine("--- HMI Settings > Backup ---");
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        var page = new DataBackupControl();
        var form = new Form { ClientSize = new Size(1000, 500) };
        form.Controls.Add(page);
        form.Show();
        Application.DoEvents();
        int rows = page.Grid.Rows.Count;
        page.Now();
        Check("Back up now: one more in the list, said", page.Grid.Rows.Count == Math.Min(2, rows + 1) && page.Status.StartsWith("Backed up"), page.Status);
        File.WriteAllText(Path.Combine(data, "Barcodes.cfg"), "prefix\tLATER\r\n");
        DataBackupControl.Confirm = (o, t) => false;
        page.Grid.CurrentCell = page.Grid.Rows[0].Cells[0];
        page.RestoreSelectedNow();
        Check("  restore asked - no: nothing changed", File.ReadAllText(Path.Combine(data, "Barcodes.cfg")).Contains("LATER"));
        Kaban.UI.Forms.MainForm.RestartNotice = (o, w) => { };
        DataBackupControl.Confirm = (o, t) => true;
        page.Grid.CurrentCell = page.Grid.Rows[0].Cells[0];
        page.RestoreSelectedNow();
        Check("  yes: restored, a restart asked for", File.ReadAllText(Path.Combine(data, "Barcodes.cfg")).Contains("prefix\tA") && page.Status.Contains("restored"), page.Status);
        form.Close();

        Console.WriteLine("--- FA2010: the transfer folder's old job files into its Archive ---");
        var asm = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly;
        var inbox = asm.GetType("Kaban.Machines.FA2010.Jobs.JobInbox");
        inbox.GetField("StatePath", Any).SetValue(null, Path.Combine(dir, "JobInbox.cfg"));
        inbox.GetMethod("Reset", Any).Invoke(null, null);
        var transfer = Path.Combine(dir, "KabanTransfer");
        Directory.CreateDirectory(transfer);
        var oldJob = Path.Combine(transfer, "Old.kbn");
        var newJob = Path.Combine(transfer, "New.kbn");
        File.WriteAllText(oldJob, "x"); File.WriteAllText(newJob, "y");
        File.SetLastWriteTime(oldJob, DateTime.Now.AddDays(-40));
        inbox.GetMethod("SetFolder", Any).Invoke(null, new object[] { transfer });
        Func<int> archive = () => (int)inbox.GetMethod("ArchiveOld", Any).Invoke(null, new object[] { DateTime.Now });
        Check("archiving off: nothing moved", archive() == 0 && File.Exists(oldJob));
        inbox.GetMethod("SetArchiveDays", Any).Invoke(null, new object[] { 30 });
        int moved = archive();
        var archived = Path.Combine(transfer, "Archive", DateTime.Now.AddDays(-40).ToString("yyyy-MM"), "Old.kbn");
        Check("older than 30 days: moved into Archive\\yyyy-MM, the new one stays", moved == 1 && File.Exists(archived) && !File.Exists(oldJob) && File.Exists(newJob), moved.ToString());
        inbox.GetField("_loaded", Any).SetValue(null, false);
        Check("  the days kept (JobInbox.cfg)", (int)inbox.GetProperty("ArchiveDays", Any).GetValue(null, null) == 30);

        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
