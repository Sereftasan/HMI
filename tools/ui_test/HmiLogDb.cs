using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.UI.Dashboard;

// 29 October: "I think we should combine the HMI logs with this database to have a single logging
// database rather than having a database to just keep the PLC logs and another one to keep the HMI
// ones" - in the same tables ("Same file, same tables"), and: "Machine states logs are coded as 1 to 5
// and they are considered differently from the rest of the logs in the industry 4 program as well. So
// we can consider HMI logs as the log in the industry 4 apart from the machine state code (just like
// the event codes inside it)". The HMI's log and its device writes as main_log rows, codes 1000 +
// their kind; the dashboard, its tables and its reports leave them out; the lists read them back.
class HD {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    static string _db;
    static List<object[]> Rows(string sql) {
        var list = new List<object[]>();
        using (var c = new NativeSqliteConnection { ConnectionString = "Data Source=" + _db + ";Mode=ReadOnly" }) {
            c.Open();
            using (var cmd = c.CreateCommand()) {
                cmd.CommandText = sql;
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) { var row = new object[r.FieldCount]; for (int i = 0; i < row.Length; i++) row[i] = r.GetValue(i); list.Add(row); }
            }
        }
        return list;
    }
    static void Exec(string sql) {
        using (var c = new NativeSqliteConnection { ConnectionString = "Data Source=" + _db }) {
            c.Open();
            using (var cmd = c.CreateCommand()) { cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
        }
    }
    static string Line(AuditEntry e) { return e.Time.ToString("HH:mm:ss.fff") + "|" + e.Kind + "|" + e.User + "|" + e.Station + "|" + e.Text; }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "hmilogdb_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        DashboardConfig.FolderOverride = Path.Combine(dir, "settings");
        DashboardService.AutoReports = false;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        _db = Path.Combine(dir, "Industry4", "database.sqlite");

        Console.WriteLine("--- what the files held before: brought in at the start ---");
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        AuditLogger.Log("logged in as Maintenance.");
        AuditLogger.Log("Station 2 lost (PLC 2): no answer for 3 s.");
        PlcWriteLog.Record(1, "D210", 120, 0, DateTime.Now);
        PlcWriteLog.Flush();
        System.Threading.Thread.Sleep(1100);            // the store starts a second later
        var before = AuditLogReader.ReadFiles(DateTime.Today, DateTime.Now, true);
        HmiLogStore.PathOverride = () => _db;
        HmiLogStore.Start();
        Check("the store ready: the database made, the files' entries brought in", HmiLogStore.Flush() && HmiLogStore.Ready && File.Exists(_db),
              HmiLogStore.Problem);
        var hmi = Rows("SELECT id, state_code, state_date FROM main_log ORDER BY id");
        Check("  each entry a main_log row: 1001 the sign-in, 1002 the connection, 1006 the device written",
              hmi.Select(r => Convert.ToInt32(r[1])).Take(3).SequenceEqual(new[] { 1001, 1002, 1006 }), string.Join(",", hmi.Select(r => r[1])));
        Check("  its date in UTC, to the second, as the machine's rows",
              hmi.All(r => DashboardData.ParseDb(r[2]).HasValue && ((string)r[2]).Length == 19) &&
              Math.Abs((DashboardData.ParseDb(hmi[0][2]).Value - DateTime.UtcNow).TotalMinutes) < 2, (string)hmi[0][2]);
        long first = Convert.ToInt64(hmi[0][0]);
        var d1 = Rows("SELECT log_description, deger, str_deger FROM log_details WHERE main_log_id = " + first + " ORDER BY id");
        Check("  its details: User and Text (str_deger)", d1.Count == 2 && (string)d1[0][0] == "User" && (string)d1[0][2] == "setter" &&
              (string)d1[1][0] == "Text" && (string)d1[1][2] == "logged in as Maintenance.");
        var d2 = Rows("SELECT log_description, deger, str_deger FROM log_details WHERE main_log_id = " + hmi[1][0] + " ORDER BY id");
        Check("  the PLC it names as a number (PLC 2)", d2.Any(r => (string)r[0] == "PLC" && Convert.ToDouble(r[1]) == 2));
        var d3 = Rows("SELECT log_description, deger, str_deger FROM log_details WHERE main_log_id = " + hmi[2][0] + " ORDER BY id");
        Check("  a device written: the write log's own line, its value as the number", d3.Count == 1 && (string)d3[0][0] == "Write" &&
              Convert.ToDouble(d3[0][1]) == 120 && ((string)d3[0][2]).Contains(";D210;120;"), d3.Count > 0 ? (string)d3[0][2] : "-");

        Console.WriteLine("--- logged while it runs ---");
        AuditLogger.Log("kept the settings of the dashboard.");
        PlcWriteLog.Record(1, "M645", 1, 0, DateTime.Now);
        Check("a new entry and a new write: in the database within a second or two",
              HmiLogStore.Flush() && Rows("SELECT state_code FROM main_log WHERE state_code IN (1005, 1006)").Count == 3);

        Console.WriteLine("--- read back: the same as the files ---");
        PlcWriteLog.Flush();
        var files = AuditLogReader.ReadFiles(DateTime.Today, DateTime.Now.AddMinutes(1), true);
        var fromDb = AuditLogReader.Read(DateTime.Today, DateTime.Now.AddMinutes(1), true);
        Check("the lists (AuditLogReader.Read) read the database: every entry of the files, the same time, kind, user, PLC and text",
              files.Select(Line).SequenceEqual(fromDb.Select(Line)),
              "files " + files.Count + ", database " + fromDb.Count + "\n" + string.Join("\n", fromDb.Select(Line).Take(8)));
        Check("  without the device writes when they are not asked for",
              AuditLogReader.Read(DateTime.Today, DateTime.Now.AddMinutes(1), false).All(e => e.Kind != AuditKind.DeviceWrite));
        PlcWriteLog.Record(1, "M646", 1, 0, DateTime.Now);
        var live = AuditLogReader.Read(DateTime.Today, DateTime.Now.AddMinutes(1), true);
        Check("  what is still waiting to be written is listed too (a live list does not lag)", live.Any(e => e.Text.StartsWith("M646 = 1")));

        Console.WriteLine("--- the dashboard and the reports leave the HMI's rows out ---");
        var now = DateTime.UtcNow;
        Exec("INSERT INTO main_log (state_code, state_date) VALUES (2, '" + now.AddMinutes(-30).ToString("yyyy-MM-dd HH:mm:ss") + "')");
        Exec("INSERT INTO main_log (state_code, state_date) VALUES (8, '" + now.AddMinutes(-20).ToString("yyyy-MM-dd HH:mm:ss") + "')");
        HmiLogStore.Flush();
        var map = StateMap.Load(Path.Combine("Dashboard", "StateMap.json"), Path.Combine("Dashboard", "Translation_JSON.csv"));
        var data = DashboardData.Load(_db, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(5), TimeZoneInfo.Utc, map, "English");
        var view = DashboardData.Build(data, false, null);
        Check("Load: the machine's rows only (no code 1000 - 1099 among the states, events, alarms or the rows)",
              data.Rows.All(r => r.Code < 1000) && view.Events.Count == 1 && view.Events[0].Code == 8 && view.States.Count == 1,
              data.Rows.Count + " rows: " + string.Join(",", data.Rows.Select(r => r.Code)));
        var probe = DashboardData.Check(_db, TimeZoneInfo.Utc);
        Check("What it holds: 2 logs (the machine's), its first and last the machine's", probe.Ok && probe.Logs == 2 &&
              probe.FirstText == now.AddMinutes(-30).ToString("yyyy-MM-dd HH:mm:ss"), probe.Describe());
        using (var c = DashboardData.Open(_db))
            Check("  a range's count: the machine's", DashboardData.Count(c, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(5)) == 2);

        Console.WriteLine("--- kept as long as the log ---");
        Exec("INSERT INTO main_log (state_code, state_date) VALUES (1001, '2020-01-01 10:00:00'); " +
             "INSERT INTO log_details (main_log_id, log_description, str_deger) VALUES (last_insert_rowid(), 'Text', 'old')");
        Exec("INSERT INTO main_log (state_code, state_date) VALUES (2, '2020-01-01 10:00:00')");
        int removed;
        using (var c = new NativeSqliteConnection { ConnectionString = "Data Source=" + _db }) { c.Open(); removed = HmiLogStore.Clean(c); }
        Check("an HMI row older than the days kept: taken out with its details; the machine's row of that day stays",
              removed == 1 && Rows("SELECT id FROM main_log WHERE state_date = '2020-01-01 10:00:00'").Count == 1 &&
              Rows("SELECT id FROM log_details WHERE str_deger = 'old'").Count == 0, removed + " removed");

        Console.WriteLine("--- stopped, the files written on, started again ---");
        HmiLogStore.Stop();
        Check("stopped: the lists read the files", AuditLogReader.Store == null);
        int rowsBefore = Rows("SELECT id FROM main_log WHERE state_code BETWEEN 1000 AND 1099").Count;
        AuditLogger.Log("restarted the HMI.");
        System.Threading.Thread.Sleep(1100);
        HmiLogStore.Start();
        HmiLogStore.Flush();
        System.Threading.Thread.Sleep(300);
        var after = Rows("SELECT m.id FROM main_log m JOIN log_details d ON d.main_log_id = m.id WHERE d.str_deger LIKE 'restarted the HMI%'");
        Check("started again: only what came while it was stopped is brought in - nothing twice",
              after.Count == 1 && Rows("SELECT id FROM main_log WHERE state_code BETWEEN 1000 AND 1099").Count >= rowsBefore + 1 &&
              Rows("SELECT d.str_deger, COUNT(*) FROM log_details d WHERE d.log_description = 'Text' GROUP BY d.str_deger HAVING COUNT(*) > 1").Count == 0,
              after.Count + " rows of it");

        Console.WriteLine("--- no database: the files ---");
        HmiLogStore.Stop();
        HmiLogStore.PathOverride = () => "";
        HmiLogStore.Start();
        System.Threading.Thread.Sleep(500);
        Check("no machine keeps a log database: not ready, the lists read the files", !HmiLogStore.Ready &&
              AuditLogReader.Read(DateTime.Today, DateTime.Now.AddMinutes(1), true).Count == AuditLogReader.ReadFiles(DateTime.Today, DateTime.Now.AddMinutes(1), true).Count);
        HmiLogStore.Stop();

        Console.WriteLine(fail == 0 ? "HMI LOG DB ALL PASS" : "HMI LOG DB " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
