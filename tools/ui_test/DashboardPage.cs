using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.Profiles.Data;
using Kaban.UI.Dashboard;

// THE MACHINE DASHBOARD (30 September): the Industry 4 dashboard as an HMI page.
// A day of a machine's log, made here so every answer is known: the states and
// their minutes, the events, the alarms, the cuts, a shift, the shift metrics,
// the report files, the automatic report, the settings - and the page showing
// all of it, in two languages, with its filters, sections and access.
//
// (The data layer was also compared file for file with the user's own
// DashboardApp3.py on a generated three-day log - see REVIEW 4u.)
class DP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        var t = o.GetType();
        while (t != null) {
            var f = t.GetField(n, Any); if (f != null) return f.GetValue(o);
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o, null);
            t = t.BaseType;
        }
        throw new MissingMemberException(n);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static void WaitFor(DashboardScreen page) {
        var until = DateTime.Now.AddSeconds(20);
        Pump(60);
        while (page.Loading && DateTime.Now < until) Pump(30);
        Pump(60);
    }

    // ---- the log: local times (Istanbul, UTC+3) written in UTC
    static NativeSqliteConnection _c;
    static long Row(string local, int code, params object[][] details) {
        var t = DateTime.ParseExact(local, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture).AddHours(-3);
        using (var cmd = _c.CreateCommand()) {
            cmd.CommandText = "INSERT INTO main_log(state_date, state_code) VALUES (@a, @b)";
            var a = cmd.CreateParameter(); a.ParameterName = "@a"; a.Value = t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); cmd.Parameters.Add(a);
            var b = cmd.CreateParameter(); b.ParameterName = "@b"; b.Value = (long)code; cmd.Parameters.Add(b);
            cmd.ExecuteNonQuery();
        }
        long id;
        using (var cmd = _c.CreateCommand()) { cmd.CommandText = "SELECT last_insert_rowid()"; id = Convert.ToInt64(cmd.ExecuteScalar()); }
        foreach (var d in details) {
            using (var cmd = _c.CreateCommand()) {
                cmd.CommandText = "INSERT INTO log_details(main_log_id, log_description, deger, str_deger) VALUES (@m, @d, @v, @s)";
                var m = cmd.CreateParameter(); m.ParameterName = "@m"; m.Value = id; cmd.Parameters.Add(m);
                var p1 = cmd.CreateParameter(); p1.ParameterName = "@d"; p1.Value = d[0]; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@v"; p2.Value = d[1] ?? DBNull.Value; cmd.Parameters.Add(p2);
                var p3 = cmd.CreateParameter(); p3.ParameterName = "@s"; p3.Value = d[2] ?? DBNull.Value; cmd.Parameters.Add(p3);
                cmd.ExecuteNonQuery();
            }
        }
        return id;
    }
    static object[] D(string desc, double? v, string s) { return new object[] { desc, v, s }; }

    static void MakeLog(string path) {
        _c = new NativeSqliteConnection { ConnectionString = "Data Source=" + path };
        _c.Open();
        foreach (var sql in new[] {
            "CREATE TABLE main_log(id INTEGER PRIMARY KEY AUTOINCREMENT, state_date TEXT, state_code INTEGER)",
            "CREATE TABLE log_details(id INTEGER PRIMARY KEY AUTOINCREMENT, main_log_id INTEGER, log_description TEXT, deger REAL, str_deger TEXT)" })
            using (var cmd = _c.CreateCommand()) { cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
        Row("2026-09-27 23:00:00", 0);                               // in force at the start of the 28th
        Row("2026-09-28 06:00:00", 1);
        Row("2026-09-28 06:30:00", 2);
        Row("2026-09-28 06:30:30", 8, D("Process ID", 1, null), D("Profile Data", null, "P1;12345;X;450;900;END"));
        Row("2026-09-28 06:35:00", 8, D("Process ID", 2, null), D("Profile Data", null, "P2;12345;X;450;900;END"));
        Row("2026-09-28 06:40:00", 8, D("Process ID", 3, null));      // no barcode
        Row("2026-09-28 06:45:00", 8, D("Process ID", 4, null), D("Profile Data", null, "P4;8000")); // short record: the total only
        Row("2026-09-28 07:00:00", 2);                               // a repeat: dropped
        Row("2026-09-28 08:00:00", 4, D("Robot X Error", 1, null), D("Milling 1 X Error", 1, null));
        Row("2026-09-28 08:10:00", 2);
        Row("2026-09-28 09:00:00", 13, D("Cylinder No", 3, null), D("Overtime Amount", 0, null));
        Row("2026-09-28 12:00:00", 3);
        Row("2026-09-28 12:30:00", 2);
        Row("2026-09-28 16:30:00", 6, D("Profile Code", null, "PRF-1"));
        Row("2026-09-28 17:00:00", 4, D("Robot X Error", 1, null));
        Row("2026-09-28 17:05:00", 5);
        Row("2026-09-28 17:20:00", 1);
        Row("2026-09-28 22:00:00", 0);
        Row("2026-09-29 01:00:00", 1);                               // after the range
        _c.Close();
    }

    static readonly DateTime Day = new DateTime(2026, 9, 28);

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "dash_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        DashboardConfig.FolderOverride = Path.Combine(dir, "settings");
        DashboardService.AutoReports = false;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        string db = Path.Combine(dir, "database.sqlite");
        MakeLog(db);
        string shipped = Path.Combine("Dashboard", "config.json");
        var map = StateMap.Load(Path.Combine("Dashboard", "StateMap.json"), Path.Combine("Dashboard", "Translation_JSON.csv"));
        var zone = DashboardTime.Find("Europe/Istanbul");
        Func<int, string> en = code => map.Translate(map.RawDescription(code), "English");

        Console.WriteLine("-- the files the machine ships");
        Check("the state map is read (codes 0 to 157)", map.Count > 20 && map.Find(8) != null && map.Find(157) != null, map.Count.ToString());
        Check("descriptions in each language (code 2: Operation is active / \u00c7al\u0131\u015fma Aktif)",
              en(2) == "Operation is active" && map.Translate("Operation is active", "T\u00fcrk\u00e7e") == "\u00c7al\u0131\u015fma Aktif");
        Check("a code the map does not have is shown as its number", map.RawDescription(99) == "99");
        var shippedCfg = DashboardConfig.FromFile(shipped);
        Check("the default settings are the Industry 4 program's", shippedCfg != null && shippedCfg.TimeZoneName == "Europe/Istanbul" &&
              shippedCfg.Shifts.Count == 3 && shippedCfg.Schedule.Type == "Daily" && shippedCfg.Schedule.Times.SequenceEqual(new[] { "08:00", "18:00" }));
        Check("the time zone is found (Europe/Istanbul is UTC+3)", zone.GetUtcOffset(Day) == TimeSpan.FromHours(3), zone.Id);
        Check("the Latest list: the file's periods and five more, shortest first",
              string.Join(",", shippedCfg.AllPeriods.Select(p => p.Label)) == "15-Minute,30-Minute,Hourly,8-Hour,Daily,Weekly,Monthly,3-Month,Annual");

        Console.WriteLine("-- a day of the log");
        var data = DashboardData.Load(db, Day, Day.AddDays(1), zone, map, "English");
        var v = DashboardData.Build(data, false, null);
        Check("the state in force at the start is read too", data.Rows.Count == 18 && data.Rows[0].Time == new DateTime(2026, 9, 27, 23, 0, 0), data.Rows.Count.ToString());
        var mins = v.States.Select(s => Math.Round(s.Minutes, 3)).ToArray();
        Check("eleven states, the repeat dropped, clipped to the day",
              v.States.Count == 11 && string.Join(",", mins) == "360,30,90,10,230,30,270,5,15,280,120", string.Join(",", mins));
        Check("the states fill the day: 1440 minutes", Math.Abs(v.States.Sum(s => s.Minutes) - 1440) < 1e-9);
        Check("the first state starts at the range start (closed since 23:00 the day before)",
              v.States[0].Start == Day && v.States[0].Code == 0 && v.States[0].Description == en(0));
        Check("events: the cuts, the cylinder and the profile change (6)", v.Events.Count == 6, v.Events.Count.ToString());
        var cyl = v.Events.First(e => e.Code == 13);
        Check("details: \"desc: value\" - a value of 0.0 gives the description alone", cyl.Details == "Cylinder No: 3.0; Overtime Amount", cyl.Details);
        Check("details: a text value as it is", v.Events.First(e => e.Code == 6).Details == "Profile Code: PRF-1");
        Check("alarms: the two errors, the bit names without \": 1.0\"",
              v.Alarms.Count == 2 && v.Alarms[0].Details == "Robot X Error; Milling 1 X Error" && v.Alarms[1].Details == "Robot X Error",
              string.Join(" | ", v.Alarms.Select(a => a.Details)));
        Check("the alarm filter counts each name", v.AlarmCounts["Robot X Error"] == 2 && v.AlarmCounts["Milling 1 X Error"] == 1);
        Check("the event filter counts each kind", v.EventCounts[en(8)] == 4 && v.EventCounts[en(13)] == 1 && v.EventCounts[en(6)] == 1);
        Check("cuts: 2 x 1234.5 mm at 45/90, the short record in the total only (3269 mm)",
              v.Cuts.Count == 1 && v.Cuts[0].Length == 1234.5 && v.Cuts[0].LeftAngle == 45 && v.Cuts[0].RightAngle == 90 &&
              v.Cuts[0].Quantity == 2 && Math.Abs(v.TotalLengthMm - 3269) < 1e-9, v.TotalLengthMm.ToString(CultureInfo.InvariantCulture));
        var vb = DashboardData.Build(data, true, null);
        Check("barcoded cuts only: the cut without a barcode is left out", vb.Events.Count == 5 && vb.EventCounts[en(8)] == 3);

        Console.WriteLine("-- a shift");
        var day = new DashboardConfig.Shift { Start = TimeSpan.FromHours(8), End = TimeSpan.FromHours(16) };
        var vs = DashboardData.Build(data, true, day);
        Check("08:00 - 16:00: its states only (480 minutes)",
              vs.States.Count == 4 && Math.Abs(vs.States.Sum(s => s.Minutes) - 480) < 1e-9 && vs.States[0].Start == Day.AddHours(8) &&
              vs.States[3].End == Day.AddHours(16), vs.States.Count + " / " + vs.States.Sum(s => s.Minutes));
        Check("08:00 - 16:00: its event and its alarm", vs.Events.Count == 1 && vs.Events[0].Code == 13 && vs.Alarms.Count == 1);
        var night = new DashboardConfig.Shift { Start = TimeSpan.FromHours(22), End = TimeSpan.FromHours(6) };
        var vn = DashboardData.Build(DashboardData.Load(db, Day.AddHours(3), Day.AddHours(12), zone, map, "English"), true, night);
        Check("a night shift that began the day before still counts (22:00 - 06:00 from 03:00: 03:00 - 06:00)",
              vn.States.Count == 1 && vn.States[0].Start == Day.AddHours(3) && vn.States[0].End == Day.AddHours(6));
        var metrics = DashboardData.Metrics(vb, shippedCfg.Shifts);
        Check("shift metrics: active share by the start of each state",
              metrics.Select(m => m.ActivePercent.ToString("0.0", CultureInfo.InvariantCulture)).SequenceEqual(new[] { "92.6", "0.0", "18.8" }),
              string.Join(",", metrics.Select(m => m.ActivePercent.ToString("0.0", CultureInfo.InvariantCulture))));
        Check("shift metrics: alarms and events per shift",
              metrics[0].Alarms == 1 && metrics[1].Alarms == 1 && metrics[2].Alarms == 0 &&
              metrics[0].Events[en(13)] == 1 && metrics[1].Events[en(6)] == 1 && metrics[2].Events[en(8)] == 3);

        Console.WriteLine("-- the report files");
        var cfg = DashboardConfig.FromFile(shipped);
        cfg.ExportDirectory = Path.Combine(dir, "reports");
        cfg.DatabasePath = db;
        cfg.Set("database_table_report", true);
        var files = DashboardReports.Save(cfg, v, DashboardReports.Stamp(v.Start), DashboardReports.ManualEndStamp(v.End), map, db, zone);
        var names = files.Select(Path.GetFileName).OrderBy(x => x).ToArray();
        Check("five files, named as before (a report from the page ends at 23:59)",
              string.Join(",", names) == "alarm_log_20260928_0000_20260928_2359.csv,event_log_20260928_0000_20260928_2359.csv," +
              "log_details_20260928_0000_20260928_2359.csv,main_log_20260928_0000_20260928_2359.csv,state_log_20260928_0000_20260928_2359.csv",
              string.Join(",", names));
        var stateLines = File.ReadAllLines(files.First(f => f.Contains("state_log")));
        Check("state log: every field in quotes, minutes as Python wrote them",
              stateLines[0] == "\"Timestamp\",\"EndTime\",\"Description\",\"Duration\"" &&
              stateLines[1] == "\"2026-09-28 00:00:00\",\"2026-09-28 06:00:00\",\"" + en(0) + "\",\"360.0\"", stateLines[1]);
        var mainLines = File.ReadAllLines(files.First(f => f.Contains("main_log")));
        Check("database table: codes as their descriptions, dates day first",
              mainLines.Length == 19 && mainLines[1] == "\"1\",\"Machine is closed\",\"27.09.2026 23:00:00\"", mainLines[1]);
        var detLines = File.ReadAllLines(files.First(f => f.Contains("log_details")));
        Check("database table of details: whole numbers without .0", detLines.Any(l => l == "\"1\",\"4\",\"Process ID\",\"1\",\"\""), detLines.Length > 1 ? detLines[1] : "");
        Check("the numbers as Python wrote them", DashboardData.PyFloat(52.550000000000004) == "52.550000000000004" &&
              DashboardData.PyFloat(1.0) == "1.0" && DashboardData.PyFloat(1e16) == "1e+16" && DashboardData.PyFloat(0.00001) == "1e-05" &&
              DashboardData.PyFloat(77.56666666666666) == "77.56666666666666");

        Console.WriteLine("-- the schedule and the automatic report");
        var sched = cfg.Schedule;
        var now = new DateTime(2026, 9, 29, 19, 30, 0);
        Check("the report before 19:30 is 18:00, the next 08:00 tomorrow",
              sched.Previous(now) == new DateTime(2026, 9, 29, 18, 0, 0) && sched.Next(now) == new DateTime(2026, 9, 30, 8, 0, 0));
        var weekly = ReportSchedule.From((IDictionary<string, object>)Json.Parse("{\"type\": \"Weekly\", \"weekdays\": [\"Sunday\", \"Wednesday\"], \"time\": \"06:30\"}"));
        Check("weekly: Sunday and Wednesday 06:30", weekly.Previous(now) == new DateTime(2026, 9, 27, 6, 30, 0) && weekly.Next(now) == new DateTime(2026, 9, 30, 6, 30, 0));
        var hist = sched.History(new DateTime(2026, 9, 27, 23, 0, 0), new DateTime(2026, 9, 29, 1, 0, 0));
        Check("the history: the report times between the first and the last log, newest first",
              hist.Count == 2 && hist[0] == new DateTime(2026, 9, 28, 18, 0, 0) && hist[1] == new DateTime(2026, 9, 28, 8, 0, 0));
        cfg.ExportDirectory = Path.Combine(dir, "auto");
        cfg.Set("database_table_report", false);
        cfg.RequiredReportEnd = new DateTime(2026, 9, 28, 18, 0, 0);
        var run = DashboardReports.RunDue(cfg, map, now);
        Check("a missed report is made first, then the latest (3 files each)",
              run.Files.Count == 6 && run.Files.Any(f => f.EndsWith("event_log_20260928_0800_20260928_1800.csv")) &&
              run.Files.Any(f => f.EndsWith("event_log_20260929_0800_20260929_1800.csv")), string.Join(" ", run.Files.Select(Path.GetFileName)));
        Check("then the next one due is kept (tomorrow 08:00)", cfg.RequiredReportEnd == new DateTime(2026, 9, 30, 8, 0, 0));
        Check("the settings file is written as the old program wrote it (one key a line, lists an item a line)",
              cfg.ToText().Contains("\n    \"shifts\": [\n        {\"start\": \"08:00\", \"end\": \"16:00\"},\n") &&
              cfg.ToText().Contains("\"required_report_end_date\": \"2026-09-30 08:00\","));

        Console.WriteLine("-- the page");
        var machine = DashboardService.Register("TEST", shipped, Path.Combine("Dashboard", "StateMap.json"), Path.Combine("Dashboard", "Translation_JSON.csv"));
        var live = DashboardConfig.FromFile(shipped);
        live.DatabasePath = db;
        live.ExportDirectory = Path.Combine(dir, "page");
        live.BarcodedCuttingOnly = false;
        typeof(DashboardConfig).GetProperty("FilePath").SetValue(live, Path.Combine(DashboardConfig.FolderOverride, "TEST.json"), null);
        machine.Replace(live);
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        var form = new Form { ClientSize = new Size(1500, 950) };
        var page = new DashboardScreen(machine);
        form.Controls.Add(page);
        form.Show();
        WaitFor(page);
        Check("it opens on the last day", (page.RangeEnd - page.RangeStart) == TimeSpan.FromDays(1) &&
              (string)page.LatestBox.SelectedItem == "Daily", page.RangeStart + " - " + page.RangeEnd);
        page.SetRange(Day, Day.AddDays(1));
        WaitFor(page);
        Check("a range chosen: what the data layer gives", page.View != null && page.View.States.Count == 11 && page.View.Events.Count == 6);
        Check("the Latest list goes back to its first line", page.LatestBox.SelectedIndex == 0);
        Check("the pie: a slice per state, the minutes of each", page.Pie.Slices.Count == 6 &&
              Math.Abs(page.Pie.Slices.First(s => s.Label == en(2)).Value - 590) < 1e-9);
        Check("the pie's colours are the old program's", page.Pie.Slices.First(s => s.Label == en(2)).Color == ColorTranslator.FromHtml("#2ecc71") &&
              page.Pie.Slices.First(s => s.Label == en(5)).Color == ColorTranslator.FromHtml("#ff0000"));
        Check("the time line: a bar per state, a row per kind", page.Timeline.Bars.Count == 11);
        page.Timeline.Zoom(Day.AddHours(12), 0.5);
        bool zoomed = Math.Abs((page.Timeline.ViewTo - page.Timeline.ViewFrom).TotalHours - 12) < 0.01;
        page.Timeline.ResetView();
        Check("the time line zooms and comes back", zoomed && page.Timeline.ViewFrom == Day && page.Timeline.ViewTo == Day.AddDays(1));
        Check("the event bars: one per kind", page.EventBars.Items.Count == 3 && page.EventBars.Items[0].Label == en(8) && page.EventBars.Items[0].Value == 4);
        Check("the state log: #, time, description, minutes", page.StateGrid.RowCount == 11 && (int)page.StateGrid.Cell(0, 0) == 1 &&
              (DateTime)page.StateGrid.Cell(0, 1) == Day && (double)page.StateGrid.Cell(0, 3) == 360);
        page.StateGrid.Sort(3);
        page.StateGrid.Sort(3);
        Check("a click on a heading sorts (twice: longest first), the # stays with its row",
              (double)page.StateGrid.Cell(0, 3) == 360 && (int)page.StateGrid.Cell(0, 0) == 1 && (double)page.StateGrid.Cell(1, 3) == 280 && (int)page.StateGrid.Cell(1, 0) == 10);
        var cutSection = page.SectionOf("cutting_details");
        Check("the cut section says the total length (3.27 m)", cutSection.Title.Contains("Total Length: 3.27 m"), cutSection.Title);
        Check("the cuts table", page.CutGrid.RowCount == 1 && (double)page.CutGrid.Cell(0, 1) == 1234.5 && (int)page.CutGrid.Cell(0, 4) == 2);
        Check("the event filter offers each kind with its count", page.EventFilter.Options.Contains(en(8) + " (4)") && page.EventFilter.Options.Count == 3,
              string.Join(" | ", page.EventFilter.Options));
        page.EventFilter.Select(new[] { en(13) });
        Pump(50);
        Check("an event kind ticked: only its rows", page.EventGrid.RowCount == 1 && (string)page.EventGrid.Cell(0, 2) == en(13));
        Check("ticking opens the section", page.SectionOf("event_logs").Open);
        page.EventFilter.Select(new string[0]);
        Check("nothing ticked: every event", page.EventGrid.RowCount == 6);
        page.AlarmFilter.Select(new[] { "Milling 1 X Error" });
        Check("an alarm ticked: the alarms that have it", page.AlarmGrid.RowCount == 1);
        page.AlarmFilter.Select(new string[0]);

        var stateLogs = page.SectionOf("state_logs");
        bool wasOpen = stateLogs.Open;
        stateLogs.ClickHead();
        var saved = File.ReadAllText(Path.Combine(DashboardConfig.FolderOverride, "TEST.json"));
        Check("a section opened or closed stays so (written to the settings)", stateLogs.Open != wasOpen &&
              saved.Contains("\"state_logs_show\": " + (stateLogs.Open ? "true" : "false")));

        page.ShiftBox.SelectedIndex = 1;   // 08:00 - 16:00
        WaitFor(page);
        Check("a shift chosen: its states only", page.View.Shift != null && page.View.States.Count == 4 && page.StateGrid.RowCount == 4);
        page.ShiftBox.SelectedIndex = 0;
        WaitFor(page);
        page.ShowMetrics();
        Pump(50);
        Check("shift metrics: a row per shift, a column per event kind", page.MetricsGrid.Rows.Count == 3 && page.MetricsGrid.Columns.Count == 6 &&
              (string)page.MetricsGrid.Rows[0].Cells[1].Value == 92.6.ToString("0.0", CultureInfo.CurrentCulture) + "%");

        Check("the history list: the report times of the log", page.HistoryBox.Items.Count == 3 &&
              (string)page.HistoryBox.Items[1] == "2026-09-28 18:00", string.Join(" | ", page.HistoryBox.Items.Cast<object>()));
        page.HistoryBox.SelectedIndex = 1;
        WaitFor(page);
        Check("a report chosen: from the report before it to it", page.RangeStart == Day.AddHours(8) && page.RangeEnd == Day.AddHours(18));
        page.LatestBox.SelectedIndex = page.LatestBox.Items.IndexOf("Hourly");
        WaitFor(page);
        Check("Latest hour: an hour up to now", page.RangeEnd - page.RangeStart == TimeSpan.FromHours(1) && page.HistoryBox.SelectedIndex == 0);

        page.SetRange(Day, Day.AddDays(1));
        WaitFor(page);
        var exported = page.ExportNow();
        Check("Export Report writes the files of what is shown", exported.Count == 3 &&
              File.Exists(Path.Combine(dir, "page", "state_log_20260928_0000_20260928_2359.csv")), string.Join(" ", exported.Select(Path.GetFileName)));
        Check("the export is in the audit log", File.ReadAllText(Directory.GetFiles(AuditLogger.LogDirectory).First()).Contains("exported 3 report file"));

        Console.WriteLine("-- in Turkish");
        LanguageManager.ChangeLanguage("TR");
        WaitFor(page);
        Check("the descriptions follow the HMI's language", page.View.States.Any(s => s.Description == "\u00c7al\u0131\u015fma Aktif"));
        Check("the page's texts too", ((Label)Get(page, "_title")).Text.Contains("Makine G\u00f6sterge Paneli") &&
              page.SectionOf("alarm_logs").Title.Contains("Ar\u0131za Kay\u0131tlar\u0131") && (string)page.LatestBox.Items[5] == "G\u00fcnl\u00fck",
              ((Label)Get(page, "_title")).Text);
        LanguageManager.ChangeLanguage("EN");
        WaitFor(page);

        Console.WriteLine("-- what can go wrong");
        DashboardData.MaxRows = 5;
        page.Reload(true);
        WaitFor(page);
        Check("a range too long for the panel says so", page.InfoText.Contains("too many"), page.InfoText);
        DashboardData.MaxRows = 1500000;
        var missing = machine.Config.Clone();
        missing.DatabasePath = Path.Combine(dir, "none.sqlite");
        machine.Replace(missing);
        WaitFor(page);
        Check("no database: the page says where it looked", page.InfoText.Contains("not found") && page.InfoText.Contains("none.sqlite"), page.InfoText);
        var back = machine.Config.Clone();
        back.DatabasePath = db;
        machine.Replace(back);
        WaitFor(page);
        Check("the database back: the page reads again", page.View != null && page.View.States.Count > 0 && page.InfoText == "");

        SecurityManager.Login("operator", SecurityLevel.Operator);
        Pump(100);
        Check("below Maintenance: no settings, no shift editing, no folder", !((Button)Get(page, "_settings")).Enabled &&
              !((Button)Get(page, "_editShifts")).Enabled && !((Button)Get(page, "_openFolder")).Enabled && ((Button)Get(page, "_export")).Enabled);
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        Pump(100);

        Console.WriteLine("-- the settings");
        var set = new DashboardSettingsControl(machine);
        var sform = new Form { ClientSize = new Size(800, 700) };
        sform.Controls.Add(set);
        sform.Show();
        Pump(100);
        Check("the settings show what is kept", set.DatabaseBox.Text == db && set.ZoneBox.Text == "Europe/Istanbul" && (string)set.PeriodBox.SelectedItem == "Daily" &&
              set.TimesBox.Text == "08:00, 18:00");
        set.TimesBox.Text = "25:00";
        Check("times that cannot be read are refused", !set.Save() && set.ProblemText.Contains("times"), set.ProblemText);
        set.TimesBox.Text = "7:5, 18:00";
        set.SectionBox("event_quantity").Checked = false;
        Check("kept", set.Save() && set.ProblemText == "");
        Check("the times are cleaned (07:05, 18:00)", machine.Config.Schedule.Times.SequenceEqual(new[] { "07:05", "18:00" }));
        Pump(200);
        Check("a section switched off leaves the page", !page.SectionOf("event_quantity").Shown && page.SectionOf("event_logs").Shown);
        set.PeriodBox.SelectedItem = "Weekly";
        Pump(50);
        Check("weekly: the weekdays and one time are asked, not the day's times",
              ((Panel)Get(set, "_weekdaysRow")).Visible && ((Panel)Get(set, "_timeRow")).Visible && !((Panel)Get(set, "_timesRow")).Visible);
        set.TimeBox.Text = "06:30";
        for (int i = 0; i < set.WeekdaysBox.Items.Count; i++) set.WeekdaysBox.SetItemChecked(i, i == 2);
        Check("weekly kept (Wednesday 06:30)", set.Save() && machine.Config.Schedule.Type == "Weekly" &&
              machine.Config.Schedule.Weekdays.SequenceEqual(new[] { "Wednesday" }) && machine.Config.Schedule.Time == "06:30");
        set.ZoneBox.Text = "Mars/Olympus";
        Check("a time zone this PC does not know is refused", !set.Save() && set.ProblemText.Contains("Mars/Olympus"));

        Console.WriteLine("-- on the machine");
        var ctx = new Ctx();
        var screens = Kaban.UI.Screens.StandardScreens.Build(ctx, new Kaban.UI.Screens.StandardScreens.Options
        {
            Home = () => new UserControl(),
            Dashboard = new DashboardDefinition()
        }).ToList();
        var dash = screens.FirstOrDefault(s => s.TitleKey == "Btn_Dashboard");
        Check("a machine with a dashboard gets the Dashboard page (after Alarms, before Manual Movement)",
              dash != null && dash.Role == ScreenRoles.Dashboard && dash.SortOrder > 20 && dash.SortOrder < 40 && dash.RequiredLevel == SecurityLevel.None);
        Check("its picture is drawn", dash != null && dash.Icon() is Image);
        Check("its files are found through the machine's folder", DashboardService.Find("DASHTEST") != null &&
              DashboardService.Find("DASHTEST").ShippedConfig.EndsWith(Path.Combine("Dashboard", "config.json")));
        var noDash = Kaban.UI.Screens.StandardScreens.Build(new Ctx { Id = "PLAIN" }, new Kaban.UI.Screens.StandardScreens.Options { Home = () => new UserControl() }).ToList();
        Check("a machine without one gets none", !noDash.Any(s => s.TitleKey == "Btn_Dashboard"));
        Check("the texts are translated", LanguageManager.GetString("Btn_Dashboard", "") == "Dashboard" &&
              LanguageManager.GetString("Dsh_Title", "") == "Local Machine Dashboard");

        form.Close();
        sform.Close();
        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    class Ctx : IMachineMountContext {
        public string Id = "DASHTEST";
        public string ModuleId { get { return Id; } }
        public bool IsMainMachine { get { return true; } }
        public int Station(int n) { return n; }
        public string Scoped(string localId) { return Id + "." + localId; }
        public long AllocateMachineBit(string localTag) { return 16; }
        public string ResolveFile(string fileName) { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName); }
    }
}
