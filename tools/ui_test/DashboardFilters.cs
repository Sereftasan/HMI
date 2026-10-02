using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.UI.Dashboard;

// THE DASHBOARD'S FILTERS (2 October): the shifts, log types, users, PLCs and
// sources chosen on the left - several of each at once - hold for every table;
// the machine's log counts as admin's, on its first PLC, from the machine; only
// what they let through is read. And "There is an error (2)" - the record that
// continues an error with the next 32 bits - is an alarm, not an event.
class DF {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }
    static bool Until(Func<bool> ok, int seconds = 30) {
        var until = DateTime.Now.AddSeconds(seconds);
        while (!ok() && DateTime.Now < until) Pump(20);
        return ok();
    }

    static NativeSqliteConnection _c;
    static void Exec(string sql, params object[] args) {
        using (var cmd = _c.CreateCommand()) {
            cmd.CommandText = sql;
            for (int i = 0; i < args.Length; i++) { var p = cmd.CreateParameter(); p.ParameterName = "@p" + i; p.Value = args[i] ?? DBNull.Value; cmd.Parameters.Add(p); }
            cmd.ExecuteNonQuery();
        }
    }
    static void Row(DateTime utc, int code, params string[] details) {
        Exec("INSERT INTO main_log(state_date, state_code) VALUES (@p0, @p1)", utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), (long)code);
        foreach (var d in details)
            Exec("INSERT INTO log_details(main_log_id, log_description, deger, str_deger) VALUES (last_insert_rowid(), @p0, 1, NULL)", d);
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "dfilt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        Directory.CreateDirectory(AuditLogger.LogDirectory);
        DashboardConfig.FolderOverride = Path.Combine(dir, "settings");
        DashboardService.AutoReports = false;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        var map = StateMap.Load(Path.Combine("Dashboard", "StateMap.json"), Path.Combine("Dashboard", "Translation_JSON.csv"));
        var utc = DashboardTime.Find("UTC");
        var day = new DateTime(2026, 9, 28);

        Console.WriteLine("--- \"There is an error (2)\": an alarm, not an event ---");
        Check("the map: code 14 continues code 4", map.Find(14) != null && map.Find(14).PreviousCode == 4 && map.RawDescription(14) == "There is an error (2)");
        Check("  so the alarm codes are 4 and 14", DashboardData.AlarmCodesOf(map).OrderBy(x => x).SequenceEqual(new[] { 4, 14 }));
        string db = Path.Combine(dir, "database.sqlite");
        _c = new NativeSqliteConnection { ConnectionString = "Data Source=" + db };
        _c.Open();
        Exec("CREATE TABLE main_log(id INTEGER PRIMARY KEY AUTOINCREMENT, state_date TEXT, state_code INTEGER)");
        Exec("CREATE TABLE log_details(id INTEGER PRIMARY KEY AUTOINCREMENT, main_log_id INTEGER, log_description TEXT, deger REAL, str_deger TEXT)");
        Row(day.AddHours(6), 2);
        Row(day.AddHours(7), 8, "Process ID");
        Row(day.AddHours(8), 4, "Robot X Error");
        Row(day.AddHours(8).AddSeconds(1), 14, "Cutting Left Front Clamping Alarm");     // written with it: joins it
        Row(day.AddHours(9), 2);
        Row(day.AddHours(12), 14, "Exit Robot Top Clamping Alarm");                       // alone: an alarm of its own
        Row(day.AddHours(13), 14);                                                        // alone and empty: nothing to list
        Row(day.AddHours(17), 13, "Cylinder No");
        Row(day.AddHours(18), 2);
        _c.Close();
        var data = DashboardData.Load(db, day, day.AddDays(1), utc, map, "English");
        var v = DashboardData.Build(data, false, null);
        Check("no \"There is an error (2)\" among the events (the cut and the cylinder)", v.Events.Count == 2 &&
              !v.EventCounts.Keys.Any(k => k.Contains("error")), string.Join(" | ", v.EventCounts.Keys));
        Check("the alarms: the error with the bits it continues with, and the lone one",
              v.Alarms.Count == 2 && v.Alarms[0].Details == "Robot X Error; Cutting Left Front Clamping Alarm" && v.Alarms[1].Details == "Exit Robot Top Clamping Alarm",
              string.Join(" | ", v.Alarms.Select(a => a.Details)));
        Check("  the alarm filter counts the continued bits too", v.AlarmCounts["Cutting Left Front Clamping Alarm"] == 1 && v.AlarmCounts["Exit Robot Top Clamping Alarm"] == 1);
        Check("  the states are as before (code 14 is no state)", v.States.Select(s => s.Code).SequenceEqual(new[] { 2, 4, 2 }));

        Console.WriteLine("--- only what is asked for is read ---");
        Check("the SQL for the states alone, the alarms alone, the events alone",
              DashboardData.CodeCondition(DashboardData.Parts.States, DashboardData.AlarmCodesOf(map)) == " AND (state_code IN (0,1,2,3,4,5))" &&
              DashboardData.CodeCondition(DashboardData.Parts.Alarms, DashboardData.AlarmCodesOf(map)) == " AND (state_code IN (4,14))" &&
              DashboardData.CodeCondition(DashboardData.Parts.Events, DashboardData.AlarmCodesOf(map)) == " AND (state_code NOT IN (0,1,2,3,4,5,14))" &&
              DashboardData.CodeCondition(DashboardData.Parts.All, DashboardData.AlarmCodesOf(map)) == "",
              DashboardData.CodeCondition(DashboardData.Parts.Events, DashboardData.AlarmCodesOf(map)));
        var onlyStates = DashboardData.Load(db, day, day.AddDays(1), utc, map, "English", null, DashboardData.Parts.States);
        var vs = DashboardData.Build(onlyStates, false, null, DashboardData.Parts.States);
        Check("the states alone: their rows, no details read", onlyStates.Rows.All(r => StateMap.IsState(r.Code)) && onlyStates.Rows.All(r => r.Details == "") &&
              vs.States.Count == 3 && vs.Events.Count == 0 && vs.Alarms.Count == 0, onlyStates.Rows.Count + "");
        var onlyAlarms = DashboardData.Load(db, day, day.AddDays(1), utc, map, "English", null, DashboardData.Parts.Alarms);
        var va = DashboardData.Build(onlyAlarms, false, null, DashboardData.Parts.Alarms);
        Check("the alarms alone: the error rows (no states made of them), the same alarms", onlyAlarms.Rows.All(r => r.Code == 4 || r.Code == 14) &&
              va.States.Count == 0 && va.Events.Count == 0 && va.Alarms.Select(a => a.Details).SequenceEqual(v.Alarms.Select(a => a.Details)));
        var onlyEvents = DashboardData.Load(db, day, day.AddDays(1), utc, map, "English", null, DashboardData.Parts.Events);
        var ve = DashboardData.Build(onlyEvents, false, null, DashboardData.Parts.Events);
        Check("the events alone: the same events", ve.Events.Select(e => e.Code).SequenceEqual(v.Events.Select(e => e.Code)) && ve.States.Count == 0 && ve.Alarms.Count == 0);
        var asked = DashboardData.Build(data, false, null, DashboardData.Parts.States);
        Check("a read of everything serves a page asking for less", asked.States.Count == 3 && asked.Events.Count == 0 && asked.Alarms.Count == 0 &&
              asked.Parts == DashboardData.Parts.States);

        Console.WriteLine("--- several shifts ---");
        var morning = new DashboardConfig.Shift { Start = TimeSpan.FromHours(6), End = TimeSpan.FromHours(14) };
        var noon = new DashboardConfig.Shift { Start = TimeSpan.FromHours(12), End = TimeSpan.FromHours(20) };
        var night = new DashboardConfig.Shift { Start = TimeSpan.FromHours(22), End = TimeSpan.FromHours(6) };
        var w = DashboardData.Windows(day, day.AddDays(1), new[] { morning, noon });
        Check("overlapping shifts: one window (06:00 - 20:00)", w.Count == 1 && w[0].Key == day.AddHours(6) && w[0].Value == day.AddHours(20),
              string.Join(" ", w.Select(x => x.Key.ToString("HH:mm") + "-" + x.Value.ToString("HH:mm"))));
        var w2 = DashboardData.Windows(day, day.AddDays(1), new[] { morning, night });
        Check("meeting shifts joined: night to 06:00 then morning (00:00 - 14:00, 22:00 - 24:00)", w2.Count == 2 &&
              w2[0].Key == day && w2[0].Value == day.AddHours(14) && w2[1].Key == day.AddHours(22) && w2[1].Value == day.AddDays(1),
              string.Join(" ", w2.Select(x => x.Key.ToString("dd HH:mm") + "-" + x.Value.ToString("dd HH:mm"))));
        var vm = DashboardData.Build(data, false, new[] { morning, noon }, DashboardData.Parts.All);
        Check("an event in both shifts is listed once", vm.Events.Count == 2 && vm.Alarms.Count == 2 && Math.Abs(vm.States.Sum(s => s.Minutes) - 14 * 60) < 1e-9);
        Check("in one of the shifts", DashboardData.InShifts(TimeSpan.FromHours(23), new[] { morning, night }) &&
              !DashboardData.InShifts(TimeSpan.FromHours(15), new[] { morning, night }) && DashboardData.InShifts(TimeSpan.FromHours(15), new DashboardConfig.Shift[0]));

        Console.WriteLine("--- what the filters let through ---");
        var f = new DashboardFilter { MachineSource = "M1", MainStation = 2 };
        Check("nothing chosen: everything, the machine's log and the HMI's", f.MachineParts == DashboardData.Parts.All && f.HmiWanted);
        f.Kinds.Add(AuditKind.MachineAlarm);
        Check("Machine alarm only: the machine's alarms, not the HMI's files", f.MachineParts == DashboardData.Parts.Alarms && !f.HmiWanted);
        f.Kinds.Add(AuditKind.SignIn);
        Check("  and Logins: the HMI's files too", f.HmiWanted && f.Passes(new AuditEntry { Kind = AuditKind.SignIn, User = "x" }) &&
              !f.Passes(new AuditEntry { Kind = AuditKind.Settings }));
        f.Kinds.Clear();
        f.Users.Add("SETTER");
        Check("a user that is not admin: the machine's log left out, the user's entries (in any case) in",
              f.MachineParts == DashboardData.Parts.None && f.Passes(new AuditEntry { User = "setter" }) && !f.Passes(new AuditEntry { User = "other" }));
        f.Users.Add("Admin");
        Check("  admin too: the machine's log back", f.MachineParts == DashboardData.Parts.All);
        f.Users.Clear();
        f.Stations.Add(1);
        Check("PLC 1 only, the machine on PLC 2: left out; an entry naming no PLC too", f.MachineParts == DashboardData.Parts.None &&
              f.Passes(new AuditEntry { Station = 1 }) && !f.Passes(new AuditEntry()));
        f.Stations.Clear();
        f.Sources.Add("");
        Check("Source HMI: the machine's log left out, the HMI's in", f.MachineParts == DashboardData.Parts.None && f.HmiWanted);
        f.Sources.Clear();
        f.Sources.Add("M1");
        Check("Source the machine: the HMI's files not read", f.MachineParts == DashboardData.Parts.All && !f.HmiWanted);
        f.Sources.Clear();
        f.Shifts.Add(morning);
        Check("a shift: the HMI's entries of its hours", f.Passes(new AuditEntry { Time = day.AddHours(7) }) && !f.Passes(new AuditEntry { Time = day.AddHours(15) }));

        Console.WriteLine("--- the left bar's lists ---");
        var host = new Form { ClientSize = new Size(400, 300) };
        var pick = new PickList(null) { Width = 200, Height = 24, AllText = "All users" };
        host.Controls.Add(pick);
        host.Show();
        int changes = 0;
        pick.Changed += (s, e) => changes++;
        pick.SetItems(new[] { new PickList.Item("admin", "admin"), new PickList.Item("setter", "setter") }, true);
        Check("nothing ticked: reads All ...", pick.Shown == "All users" && pick.Selected.Count == 0 && changes == 0);
        pick.Select(new[] { "setter", "nobody" });
        Check("ticked: reads what is ticked (a value not in the list is not ticked)", pick.Shown == "setter" && pick.Selected.Count == 1 && changes == 1);
        pick.Select(new[] { "admin", "setter" });
        Check("several: all of them, in the list's order", pick.Shown == "admin, setter" && changes == 2);
        pick.SetItems(new[] { new PickList.Item("admin", "admin") }, true);
        Check("a list without a ticked value keeps it (a user the period lacks)", pick.Items.Count == 2 && pick.Selected.Contains("setter") && changes == 2);
        pick.SetItems(new[] { new PickList.Item("admin", "admin") }, false);
        Check("  or lets it go (a shift taken out of the settings)", pick.Items.Count == 1 && !pick.Selected.Contains("setter") && pick.Shown == "admin");
        host.Close();

        Console.WriteLine("--- on the page ---");
        File.WriteAllText(Path.Combine(AuditLogger.LogDirectory, AuditLogger.FilePrefix + day.ToString("yyyy-MM-dd") + ".txt"),
            "[2026-09-28 07:30:00] - User 'setter' logged in.\r\n" +
            "[2026-09-28 08:30:00] - User 'setter' wrote 5 to D100 (PLC 1)\r\n" +
            "[2026-09-28 15:00:00] - User 'operator' logged in.\r\n");
        var machine = DashboardService.Register("FILT", Path.Combine("Dashboard", "config.json"),
                                                Path.Combine("Dashboard", "StateMap.json"), Path.Combine("Dashboard", "Translation_JSON.csv"));
        var cfg = machine.Config.Clone();
        cfg.DatabasePath = db;
        cfg.TimeZoneName = "UTC";
        cfg.BarcodedCuttingOnly = false;
        machine.Replace(cfg);
        var form = new Form { ClientSize = new Size(1400, 900) };
        var page = new DashboardScreen(machine);
        form.Controls.Add(page);
        form.Show();
        Until(() => page.View != null && !page.Loading);
        page.SetRange(day, day.AddDays(1));
        Until(() => !page.Loading && page.View != null && page.View.Alarms.Count == 2);
        Check("the events tab without \"There is an error (2)\"; the alarms tab with its bits",
              page.EventFilter.Options.All(o => !o.Contains("error")) && page.AlarmGrid.RowCount == 2 &&
              ((string)page.AlarmGrid.Cell(0, 2)).Contains("Cutting Left Front Clamping Alarm"), string.Join(" | ", page.EventFilter.Options));
        Check("the users: the log's and admin", page.UserPick.Items.Select(i => i.Key).OrderBy(x => x).SequenceEqual(new[] { "admin", "operator", "setter" }),
              string.Join(",", page.UserPick.Items.Select(i => i.Key)));
        page.SelectTab("log_all");
        Pump(100);
        var log = page.LogTable;
        Check("All logs: the HMI's three and the machine's", log.RowCount == 3 + 3 + 2 + 2, log.RowCount + "");
        page.ShiftPick.Select(new[] { machine.Config.Shifts[0].Text });     // 08:00 - 16:00
        Until(() => !page.Loading);
        var times = Enumerable.Range(0, log.RowCount).Select(r => TimeSpan.Parse(log.CellText(r, 0).Substring(12))).ToList();
        Check("a shift: every table of its hours - the HMI's entries too", times.All(t => t >= TimeSpan.FromHours(8) && t < TimeSpan.FromHours(16)) &&
              Enumerable.Range(0, log.RowCount).Any(r => log.CellText(r, 5) == "HMI") && page.StateGrid.RowCount == 2 && page.AlarmGrid.RowCount == 2,
              string.Join(",", times));
        page.ShiftPick.Select(new string[0]);
        Until(() => !page.Loading);
        page.UserPick.Select(new[] { "operator" });
        Until(() => !page.Loading);
        Check("User operator: one entry here; the machine's tabs say the filters leave them out", log.RowCount == 1 && log.CellText(0, 1) == "operator" &&
              page.EventGrid.EmptyText.Contains("filters on the left") && page.EventBars.EmptyText.Contains("filters on the left") &&
              page.BarText.Contains("1 entries shown"), page.BarText);
        page.SelectTab("events");
        Pump(50);
        Check("  the info bar on the events tab: none shown", page.BarText.Contains("0 entries shown"), page.BarText);
        page.UserPick.Select(new string[0]);
        Until(() => !page.Loading);
        Check("  all users: the machine's tabs back", page.EventGrid.RowCount == 2 && page.View != null);
        form.Close();

        Console.WriteLine(fail == 0 ? "\nDASHBOARD FILTERS ALL PASS" : "\nDASHBOARD FILTERS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
