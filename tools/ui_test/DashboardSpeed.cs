using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.UI.Controls;
using Kaban.UI.Dashboard;

// THE DASHBOARD'S SPEED (2 October). Its tables opened slowly even with little
// machine data: a busy day of the HMI's own log is 15 000 lines (connection
// retries), and every tab change, filter or search emptied the table by setting
// its row count to 0 - which removes the rows ONE BY ONE, seconds for thousands
// (3 to 28 s for a log tab on the test box, 10 s for a search). Now the rows are
// cleared at once (UiStyle.SetRowCount). The machine log's dates are read by hand
// and turned to the zone's time by the hour. The limits below are generous (this
// box is slow); the old code missed every one of them several times over.
class DS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }
    static bool Until(Func<bool> ok, int seconds = 60) {
        var until = DateTime.Now.AddSeconds(seconds);
        while (!ok() && DateTime.Now < until) Pump(20);
        return ok();
    }
    static double Ms(Action a) { var w = Stopwatch.StartNew(); a(); Application.DoEvents(); return w.Elapsed.TotalMilliseconds; }

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
    static void MakeLog(string path) {
        _c = new NativeSqliteConnection { ConnectionString = "Data Source=" + path };
        _c.Open();
        Exec("CREATE TABLE main_log(id INTEGER PRIMARY KEY AUTOINCREMENT, state_date TEXT, state_code INTEGER)");
        Exec("CREATE TABLE log_details(id INTEGER PRIMARY KEY AUTOINCREMENT, main_log_id INTEGER, log_description TEXT, deger REAL, str_deger TEXT)");
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "dspeed_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        Directory.CreateDirectory(AuditLogger.LogDirectory);
        DashboardConfig.FolderOverride = Path.Combine(dir, "settings");
        DashboardService.AutoReports = false;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        SecurityManager.Login("setter", SecurityLevel.Maintenance);

        Console.WriteLine("--- a virtual table emptied at once ---");
        var host = new Form { ClientSize = new Size(900, 600) };
        var g = new DataGridView { Dock = DockStyle.Fill, VirtualMode = true, AllowUserToAddRows = false, RowHeadersVisible = false };
        g.Columns.Add("a", "a");
        g.CellValueNeeded += (s, e) => e.Value = e.RowIndex;
        host.Controls.Add(g);
        host.Show();
        UiStyle.SetRowCount(g, 30000);
        double clear = Ms(() => UiStyle.SetRowCount(g, 10));
        Check("30 000 rows down to 10 at once", g.RowCount == 10 && clear < 1500, clear.ToString("0") + " ms");
        UiStyle.SetRowCount(g, 20);
        Check("  and up again", g.RowCount == 20);
        host.Close();

        Console.WriteLine("--- a busy day of the HMI's log: 20 000 lines ---");
        var today = DateTime.Today;
        var sb = new StringBuilder();
        for (int i = 0; i < 20000; i++) {
            var t = today.AddSeconds(i * 3);
            string text = i % 5 == 0 ? "PLC COMMUNICATION LOST." : i % 5 == 1 ? "PLC communication restored." : i % 5 == 2 ? "[PLC DIAG] station 2 scan FAILED (0x01808201)" :
                          i % 5 == 3 ? "wrote " + i + " to D100 (PLC 1)" : (i % 50 == 4 ? "logged in." : "set option " + i + ".");
            sb.Append("[" + t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "] - User 'admin' " + text + "\r\n");
        }
        File.WriteAllText(Path.Combine(AuditLogger.LogDirectory, AuditLogger.FilePrefix + today.ToString("yyyy-MM-dd") + ".txt"), sb.ToString());

        string db = Path.Combine(dir, "database.sqlite");
        MakeLog(db);
        Exec("BEGIN");
        for (int i = 0; i < 20000; i++) Row(today.AddSeconds(i * 4), i % 10 == 0 ? 2 + i % 4 : i % 10 == 5 ? 13 : 8, i % 10 == 2 ? new[] { "Robot X Error" } : new string[0]);
        Exec("COMMIT");
        _c.Close();
        var machine = DashboardService.Register("SPEED", Path.Combine("Dashboard", "config.json"),
                                                Path.Combine("Dashboard", "StateMap.json"), Path.Combine("Dashboard", "Translation_JSON.csv"));
        var cfg = machine.Config.Clone();
        cfg.DatabasePath = db;
        cfg.TimeZoneName = "UTC";
        cfg.BarcodedCuttingOnly = false;
        machine.Replace(cfg);

        var form = new Form { ClientSize = new Size(1400, 900) };
        var dash = new DashboardScreen(machine);
        form.Controls.Add(dash);
        form.Show();
        Until(() => dash.View != null && !dash.Loading);
        dash.SetRange(today, today.AddDays(1).AddSeconds(-1));
        Until(() => !dash.Loading && dash.View != null && dash.View.Events.Count > 10000);
        Check("the machine's day read", dash.View.Events.Count == 18000, dash.View.Events.Count + "");

        double all = Ms(() => dash.SelectTab("log_all"));
        var log = dash.LogTable;
        Check("All logs: the HMI's day and the machine's, listed", log.RowCount == 20000 && all < 8000, log.RowCount + " rows, " + all.ToString("0") + " ms");
        double logins = Ms(() => dash.SelectTab("log_signin"));
        Check("  to Logins (20 000 rows down to 400)", log.RowCount == 400 && logins < 2000, log.RowCount + " rows, " + logins.ToString("0") + " ms");
        double back = Ms(() => dash.SelectTab("log_all"));
        Check("  back to All logs", log.RowCount == 20000 && back < 2000, back.ToString("0") + " ms");
        double search = Ms(() => log.SearchText = "D100");
        Check("  a search (20 000 rows down to 4 000)", log.RowCount == 4000 && search < 2000, log.RowCount + " rows, " + search.ToString("0") + " ms");
        double none = Ms(() => log.SearchText = "");
        Check("  the search cleared", log.RowCount == 20000 && none < 2000, none.ToString("0") + " ms");
        log.SetUser("admin");
        double user = Ms(() => log.SetUser(null));
        Check("  a user chosen and cleared", user < 2000, user.ToString("0") + " ms");

        dash.SelectTab("events");
        Pump(50);
        var few = dash.View.EventCounts.OrderBy(kv => kv.Value).First();
        double filt = Ms(() => dash.EventFilter.Select(new[] { few.Key }));
        Check("the event table filtered to one kind (18 000 rows down to 2 000)", dash.EventGrid.RowCount == 2000 && filt < 2000,
              dash.EventGrid.RowCount + " rows, " + filt.ToString("0") + " ms");
        double unfilt = Ms(() => dash.EventFilter.Select(new string[0]));
        Check("  and back", dash.EventGrid.RowCount == 18000 && unfilt < 2000, unfilt.ToString("0") + " ms");
        form.Close();

        Console.WriteLine("--- the machine log's dates, read by hand ---");
        foreach (var s in new[] { "2026-10-02 07:05:09", "2024-02-29 23:59:59", "2026-12-31 00:00:00" })
            Check("  " + s, DashboardData.ParseDb(s) == DateTime.ParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        Check("  other forms still read", DashboardData.ParseDb("2026-10-02T07:05:09") == new DateTime(2026, 10, 2, 7, 5, 9) &&
              DashboardData.ParseDb("2026-10-02 07:05:09.250") == new DateTime(2026, 10, 2, 7, 5, 9, 250) &&
              DashboardData.ParseDb("2026-10-02 07:05") == new DateTime(2026, 10, 2, 7, 5, 0));
        Check("  not a date: none", DashboardData.ParseDb("2026-13-02 07:05:09") == null && DashboardData.ParseDb("01.10.2026 08:00:00") == null);

        Console.WriteLine("--- to the zone's time by the hour: across a clock change ---");
        var berlin = DashboardTime.Find("Europe/Berlin");
        string dst = Path.Combine(dir, "dst.sqlite");
        MakeLog(dst);
        var utcRows = new List<DateTime>();
        for (int m = 0; m < 240; m += 7) { var u = new DateTime(2026, 3, 29, 0, 0, 0).AddMinutes(m); utcRows.Add(u); Row(u, 2); }
        for (int m = 0; m < 240; m += 7) { var u = new DateTime(2026, 10, 25, 0, 0, 0).AddMinutes(m); utcRows.Add(u); Row(u, 2); }
        _c.Close();
        var loaded = DashboardData.Load(dst, new DateTime(2026, 3, 28), new DateTime(2026, 10, 26), berlin, machine.Map, "EN");
        var want = utcRows.Select(u => DashboardTime.ToLocal(u, berlin)).ToList();
        var got = loaded.Rows.Select(r => r.Time).ToList();
        Check("each row's time as converted one by one (spring and autumn)", got.SequenceEqual(want),
              got.Count + " / " + want.Count + (got.Count == want.Count ? "" : ""));

        Console.WriteLine(fail == 0 ? "\nDASHBOARD SPEED ALL PASS" : "\nDASHBOARD SPEED " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
