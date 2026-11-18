using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 17 November: "check over the program and add the other tools you find useful as well" - from the old KabanMachinesSystem:
// its Cleaning Settings (a reminder after so many bars) as a maintenance plan, its TeamViewer / AnyDesk menu items as the
// "?" menu's remote support, and FA2010's transfer folder (KabanFileSystemWatcher) on the Jobs page.
class OPT {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }

    [STAThread]
    static void Main(string[] args) {
        string shots = args.Length > 0 ? args[0] : "";
        var dir = Path.Combine(Path.GetTempPath(), "opt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(1);
        PlcManager.Instance.RegisterDriver(1, sim);
        PlcManager.Instance.RegisterStation(1, "Test", SecurityLevel.Operator);
        sim.Connect();

        // ================================================================ the maintenance plan
        Console.WriteLine("--- the maintenance plan ---");
        MaintenancePlan.SettingsPath = Path.Combine(dir, "Maintenance.cfg");
        MaintenancePlan.Reload();
        var now = DateTime.Now;
        var byDays = new MaintenanceTask { Name = "Clean the saw", EveryDays = 7, LastDone = now.AddDays(-3), Notes = "the chips under the table" };
        var byCount = new MaintenanceTask { Name = "Grease the robot", Counter = "D500", CounterDouble = true, EveryCount = 1000, CountAtDone = 69000, LastDone = now };
        MaintenancePlan.Save(new[] { byDays, byCount });
        sim.Write("D500", 69500 & 0xFFFF); sim.Write("D501", 69500 >> 16);
        MaintenancePlan.Check(now);
        var all = MaintenancePlan.All;
        Check("kept and read back; nothing due yet: no reminder", all.Count == 2 && !AlarmManager.ActiveAlarms.Any(a => a.AlarmCode.StartsWith(MaintenancePlan.AlarmPrefix)));
        Check("  what is left: in 4 days; 500 counts (the counter D500 / D501 read: 69500)",
              MaintenancePlan.Describe(all[0], now) == "in 4 day(s)" && MaintenancePlan.Describe(all[1], now) == "500 count(s) left" && all[1].CounterNow == 69500,
              MaintenancePlan.Describe(all[0], now) + " | " + MaintenancePlan.Describe(all[1], now));
        sim.Write("D500", 70100 & 0xFFFF); sim.Write("D501", 70100 >> 16);
        MaintenancePlan.Check(now);
        var raised = AlarmManager.ActiveAlarms.Where(a => a.AlarmCode.StartsWith(MaintenancePlan.AlarmPrefix)).ToList();
        Check("the counter past 1000 counts: a warning on the Alarms page, saying how far", raised.Count == 1 && raised[0].Severity == AlarmSeverity.Warning &&
              raised[0].Text.Contains("Grease the robot") && raised[0].Detail.Contains("100 count(s) over"), string.Join(" | ", raised.Select(a => a.Text + " - " + a.Detail)));
        MaintenancePlan.Check(now.AddDays(5));
        raised = AlarmManager.ActiveAlarms.Where(a => a.AlarmCode.StartsWith(MaintenancePlan.AlarmPrefix)).ToList();
        Check("  days past: the saw's too (one each, not again)", raised.Count == 2 && raised.Any(a => a.Text.Contains("Clean the saw") && a.Detail.Contains("the chips")),
              string.Join(" | ", raised.Select(a => a.Text)));
        // acknowledged: back after 4 hours
        AlarmManager.ClearGenerated(raised.First(a => a.Text.Contains("Clean the saw")));
        MaintenancePlan.Check(now.AddDays(5).AddHours(1));
        Check("  acknowledged: away for 4 hours", AlarmManager.ActiveAlarms.Count(a => a.Text.Contains("Clean the saw")) == 0);
        MaintenancePlan.Check(now.AddDays(5).AddHours(5));
        Check("  then back while not done", AlarmManager.ActiveAlarms.Count(a => a.Text.Contains("Clean the saw")) == 1);
        var greaseId = MaintenancePlan.All.First(t => t.Name == "Grease the robot").Id;
        MaintenancePlan.MarkDone(greaseId, "mehmet");
        var grease = MaintenancePlan.All.First(t => t.Id == greaseId);
        Check("done: the counter then kept (70100), its reminder off, by whom", grease.CountAtDone == 70100 && grease.DoneBy == "mehmet" &&
              !AlarmManager.ActiveAlarms.Any(a => a.Text.Contains("Grease the robot")));
        sim.Write("D500", 300); sim.Write("D501", 0);
        MaintenancePlan.Check(DateTime.Now);
        grease = MaintenancePlan.All.First(t => t.Id == greaseId);
        Check("  the PLC set its counter back to 0: counted from 0 (300 counted, 700 left)", MaintenancePlan.Describe(grease, DateTime.Now) == "700 count(s) left",
              MaintenancePlan.Describe(grease, DateTime.Now));
        MaintenancePlan.Reload();
        Check("  kept over a restart (Maintenance.cfg)", MaintenancePlan.All.First(t => t.Id == greaseId).CountAtDone == 70100 &&
              MaintenancePlan.All.First(t => t.Name == "Clean the saw").Notes == "the chips under the table");

        Console.WriteLine("--- HMI Settings > Maintenance ---");
        SecurityManager.Login("op", SecurityLevel.Operator);
        var page = new MaintenancePlanControl();
        var form = new Form { ClientSize = new Size(1100, 500) };
        form.Controls.Add(page);
        form.Show();
        Pump(200);
        Check("the plan's tasks, what is left; an Operator cannot change them (only Done)", page.Grid.Rows.Count == 2 && page.Grid.Columns["name"].ReadOnly &&
              Convert.ToString(page.Grid.Rows[0].Cells["left"].Value).Length > 0, Convert.ToString(page.Grid.Rows[0].Cells["left"].Value));
        page.Grid.CurrentCell = page.Grid.Rows[0].Cells["name"];
        page.Done();
        Check("  Done: the selected task done now (by the user signed in)", MaintenancePlan.All.First(t => t.Name == "Clean the saw").DoneBy == "op" &&
              (DateTime.Now - MaintenancePlan.All.First(t => t.Name == "Clean the saw").LastDone).TotalMinutes < 1, page.Status);
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        form.Close();
        page = new MaintenancePlanControl();
        form = new Form { ClientSize = new Size(1100, 500) };
        form.Controls.Add(page);
        form.Show();
        Pump(100);
        page.Add();
        int last = page.Grid.Rows.Count - 1;
        page.Grid.Rows[last].Cells["days"].Value = "";
        page.Save();
        Check("a task with no interval: not kept, said", page.Status.Contains("every how many"), page.Status);
        page.Grid.Rows[last].Cells["count"].Value = "500";
        page.Save();
        Check("  counts without a counter: said", page.Status.Contains("counter"), page.Status);
        page.Grid.Rows[last].Cells["counter"].Value = "d600";
        page.Grid.Rows[last].Cells["name"].Value = "Change the filter";
        page.Save();
        Check("  a new task kept (its counter D600)", MaintenancePlan.All.Count == 3 && MaintenancePlan.All.Any(t => t.Name == "Change the filter" && t.Counter == "D600" && t.EveryCount == 500), page.Status);
        if (shots.Length > 0) {
            Directory.CreateDirectory(shots);
            using (var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) {
                Pump(300);
                using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, bmp.Size);
                bmp.Save(Path.Combine(shots, "maintenance.png"));
            }
        }
        form.Close();
        foreach (var a in AlarmManager.ActiveAlarms.Where(a => a.AlarmCode.StartsWith(MaintenancePlan.AlarmPrefix)).ToList()) AlarmManager.ClearGenerated(a);

        // ================================================================ remote support
        Console.WriteLine("--- remote support (the ? menu) ---");
        RemoteSupport.BaseFolder = dir;
        var started = new List<string>();
        RemoteSupport.Start = p => { started.Add(p); return true; };
        var why = RemoteSupport.Open(RemoteSupport.Tool.AnyDesk);
        Check("AnyDesk not on the panel: says where to put it (the Supports folder)", why.Contains("AnyDesk.exe") && why.Contains("Supports") && started.Count == 0, why);
        Directory.CreateDirectory(Path.Combine(dir, "Supports"));
        File.WriteAllText(Path.Combine(dir, "Supports", "KabanSupport.exe"), "");
        why = RemoteSupport.Open(RemoteSupport.Tool.TeamViewer);
        Check("  TeamViewer: Supports\\KabanSupport.exe (as the old program shipped it) started", why == "" && started.Count == 1 && started[0].EndsWith("KabanSupport.exe"), why);

        // ================================================================ FA2010's transfer folder
        Console.WriteLine("--- FA2010: the transfer folder ---");
        var asm = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly;
        var inbox = asm.GetType("Kaban.Machines.FA2010.Jobs.JobInbox");
        inbox.GetField("StatePath", Any).SetValue(null, Path.Combine(dir, "JobInbox.cfg"));
        inbox.GetMethod("Reset", Any).Invoke(null, null);
        var arrived = new List<string>();
        var ev = inbox.GetEvent("Arrived", Any);
        Action<string> onArrived = p => { lock (arrived) arrived.Add(p); };
        var add = ev.GetAddMethod(true);
        add.Invoke(null, new object[] { onArrived });
        var transfer = Path.Combine(dir, "KabanTransfer");
        Directory.CreateDirectory(transfer);
        File.WriteAllText(Path.Combine(transfer, "Old.kbn"), "Bar;1;P;N;1;W;65000;;\r\n");
        inbox.GetMethod("SetFolder", Any).Invoke(null, new object[] { transfer });
        Action scan = () => inbox.GetMethod("Scan", Any).Invoke(null, null);
        scan();
        Check("a folder set: the job file already there comes in", arrived.Count == 1 && arrived[0].EndsWith("Old.kbn"), string.Join(", ", arrived));
        scan();
        Check("  once (not again at the next look)", arrived.Count == 1);
        var job2 = Path.Combine(transfer, "Job 12.kbn");
        using (var locked = new FileStream(job2, FileMode.Create, FileAccess.Write, FileShare.None)) {
            locked.WriteByte(65);
            scan();
            Check("a file the window software is still writing: waited for", arrived.Count == 1);
        }
        scan();
        Check("  written: it comes in, newest first in the new ones", arrived.Count == 2 && arrived[1] == job2 &&
              ((List<string>)inbox.GetProperty("New", Any).GetValue(null, null))[0] == job2);
        File.WriteAllText(Path.Combine(transfer, "notes.txt"), "x");
        scan();
        Check("  other files left alone", arrived.Count == 2);
        inbox.GetMethod("Opened", Any).Invoke(null, new object[] { job2 });
        Check("  opened: no longer new", !((List<string>)inbox.GetProperty("New", Any).GetValue(null, null)).Contains(job2));
        inbox.GetField("_loaded", Any).SetValue(null, false);
        ((System.Collections.IDictionary)inbox.GetField("_seen", Any).GetValue(null)).Clear();
        scan();
        Check("  the files seen kept over a restart (JobInbox.cfg): not again", arrived.Count == 2 && (string)inbox.GetProperty("Folder", Any).GetValue(null, null) == transfer);
        File.SetLastWriteTimeUtc(Path.Combine(transfer, "Old.kbn"), DateTime.UtcNow.AddMinutes(1));
        scan();
        Check("  a file written again (the job sent again): comes in again", arrived.Count == 3 && arrived[2].EndsWith("Old.kbn"));
        inbox.GetMethod("Stop", Any).Invoke(null, null);

        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
