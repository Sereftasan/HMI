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
    static bool Has(object o, string n) { try { Get(o, n); return true; } catch (MissingMemberException) { return false; } }
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
        page.SelectTab("states");
        Pump(100);
        var tl = page.Timeline;
        var mid = new Point(tl.Width / 2, tl.Height / 2);
        tl.WheelAt(mid, -120, false);
        Check("  the wheel alone does not zoom (the page scrolls)", tl.ViewFrom == Day && tl.ViewTo == Day.AddDays(1));
        tl.WheelAt(mid, -120, true);
        double span1 = (tl.ViewTo - tl.ViewFrom).TotalHours;
        Check("  Ctrl + the wheel turned back: in (0.8 of the span)", Math.Abs(span1 - 24 * 0.8) < 0.01, span1 + " h");
        tl.WheelAt(mid, -360, true);
        Check("  three notches at once: three steps", Math.Abs((tl.ViewTo - tl.ViewFrom).TotalHours - 24 * Math.Pow(0.8, 4)) < 0.01);
        tl.WheelAt(mid, 120, true);
        Check("  turned forward: out again", Math.Abs((tl.ViewTo - tl.ViewFrom).TotalHours - 24 * Math.Pow(0.8, 3)) < 0.01);
        tl.ResetView();
        tl.ZoomTo(Day.AddHours(9), Day.AddHours(9).AddMinutes(30));
        Check("  an interval marked (right-drag): exactly it", tl.ViewFrom == Day.AddHours(9) && tl.ViewTo == Day.AddHours(9).AddMinutes(30));
        tl.ZoomTo(Day.AddHours(9), Day.AddHours(9).AddSeconds(10));
        Check("  never less than a minute", Math.Abs((tl.ViewTo - tl.ViewFrom).TotalSeconds - 60) < 0.5);
        tl.ResetView();
        // 2 November: "zoom in / out function does not work on Dashboard / Machine Performance / Timeline chart" - buttons too
        tl.Invalidate(); tl.Update(); Pump(50);
        var zb = tl.ZoomButtons;
        var wndProc = typeof(Control).GetMethod("WndProc", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Action<int, Point> mouse = (msg, p) => wndProc.Invoke(tl, new object[] { Message.Create(tl.Handle, msg, (IntPtr)(msg == 0x202 ? 0 : 1), (IntPtr)((p.Y << 16) | (p.X & 0xFFFF))) });
        Func<Rectangle, Point> centre = r => new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
        Check("  three zoom buttons at its top right (out, in, the whole range), the pointer's size with a mouse",
              zb.All(r => r.Height == Kaban.UI.Controls.UiScale.Px(24) && r.Width == r.Height && r.Top < Kaban.UI.Controls.UiScale.Px(8)) && zb[0].Right < zb[1].Left && zb[1].Right < zb[2].Left &&
              zb[2].Right <= tl.Width && tl.TipFor(centre(zb[1])) == "Zoom in: half as much shown, around its middle" && tl.TipFor(centre(zb[2])) == "The whole range",
              string.Join(" ", zb.Select(r => r.ToString())));
        mouse(0x201, centre(zb[1])); mouse(0x202, centre(zb[1])); Pump(30);
        Check("  + : half as much shown, around its middle", tl.ViewFrom == Day.AddHours(6) && tl.ViewTo == Day.AddHours(18), tl.ViewFrom + " - " + tl.ViewTo);
        mouse(0x203, centre(zb[1])); mouse(0x202, centre(zb[1])); Pump(30);
        Check("  + pressed again at once (a double click): in once more - not the whole range", Math.Abs((tl.ViewTo - tl.ViewFrom).TotalHours - 6) < 0.01 &&
              tl.ViewFrom == Day.AddHours(9), tl.ViewFrom + " - " + tl.ViewTo);
        mouse(0x201, centre(zb[0])); mouse(0x202, centre(zb[0])); Pump(30);
        Check("  - : twice as much", Math.Abs((tl.ViewTo - tl.ViewFrom).TotalHours - 12) < 0.01, tl.ViewFrom + " - " + tl.ViewTo);
        mouse(0x201, centre(zb[1])); mouse(0x202, new Point(5, tl.Height - 5)); Pump(30);
        Check("  pressed and let go elsewhere: nothing", Math.Abs((tl.ViewTo - tl.ViewFrom).TotalHours - 12) < 0.01);
        mouse(0x201, centre(zb[2])); mouse(0x202, centre(zb[2])); Pump(30);
        Check("  the four corners: the whole range", tl.ViewFrom == Day && tl.ViewTo == Day.AddDays(1));

        Console.WriteLine("-- the page's look (2 October)");
        Check("no title above the tabs; the tabs' names without pictures (Machine Performance, Production Events, Alarms, All logs)", !Has(page, "_title") &&
              page.TabKeys.Select(t => t.Text).SequenceEqual(new[] { "Machine Performance", "Production Events", "Alarms", "All logs" }) &&
              page.TabKeys.All(t => t.Text.All(ch => ch < 0x2190)), string.Join(",", page.TabKeys.Select(t => t.Text)));
        var captions = ((System.Collections.IEnumerable)Get(page, "_sideCaptions")).Cast<Label>().Select(l => l.Text).ToList();
        Check("the filters' captions without pictures; Log Type between Shift Selection and User", captions.All(t => t.All(ch => ch < 0x2190)) &&
              captions.Skip(4).SequenceEqual(new[] { "Shift Selection", "Log Type", "User", "PLC", "Source" }),
              string.Join(" | ", captions));
        Check("  the sections' headings: Time Distribution, Timeline, Logs - Quantities, Cutting Details, Logs",
              new[] { "state_pie", "state_timeline", "state_logs", "event_quantity", "event_logs" }.Select(k => page.SectionOf(k).Title)
              .SequenceEqual(new[] { "\u23f1\ufe0f Time Distribution", "\U0001F552 Timeline", "\U0001F4DC Logs", "\U0001F4C8 Quantities", "\U0001F4CB Logs" }),
              string.Join(" | ", new[] { "state_pie", "state_timeline", "state_logs", "event_quantity", "event_logs" }.Select(k => page.SectionOf(k).Title)));
        Check("a narrower filter bar", page.FilterBarWidth <= 240, page.FilterBarWidth + "");
        Check("no refresh beside Latest, no shift buttons, no settings button", !Has(page, "_refresh") && !Has(page, "_editShifts") &&
              !Has(page, "_settings") && !Has(page, "_metrics"));
        var foot = page.ExportButton.Parent;
        Check("Export Report at the foot of the bar, the folder beside it", foot.Dock == DockStyle.Bottom && page.FolderButton.Parent == foot &&
              page.FolderButton.Left > page.ExportButton.Right && page.ExportButton.Text == "Export Report" && page.FolderButton.Text == "\U0001F4C2");
        Check("  Export Report in the usual button colours (not red)", page.ExportButton.BackColor == Color.White && page.ExportButton.ForeColor != Color.White &&
              !page.ExportButton.Font.Bold, page.ExportButton.BackColor + " / " + page.ExportButton.ForeColor);
        Check("  the folder button usable (Maintenance)", page.FolderButton.Enabled);
        Check("the info bar: the interval and the entries the tab shows", page.BarText.StartsWith("Log interval:  2026-09-28 00:00  -  2026-09-29 00:00") &&
              page.BarText.Contains("11 entries shown, 11 in the period") && !page.BarIsRed && !page.BarIsBold, page.BarText);
        page.SelectTab("events");
        Pump(50);
        Check("  the events tab: its table's", page.BarText.Contains("6 entries shown, 6 in the period"), page.BarText);
        var heads = new[] { "state_pie", "state_logs", "event_logs" }.Select(k => page.SectionOf(k).Head).ToList();
        Check("the sections' headings: on the left, as wide as their words", heads.All(h => h.Left == Kaban.UI.Controls.UiScale.Px(4) && h.Width < 500 && h.Width > 100),
              string.Join(",", heads.Select(h => h.Left + "/" + h.Width)));
        // 2 November: the Options page's group on the parted pages - "dashboard machine performance and production events"
        var framed = new[] { "state_pie", "state_timeline", "state_logs", "event_quantity", "event_logs" }.Select(k => page.SectionOf(k)).Where(s => s.Open).ToList();
        Check("  an open section is a frame, its heading on the frame's top line; its contents inside the frame's sides",
              framed.Count >= 3 && framed.All(s => s.Framed && s.Padding.Left > 0 && s.Padding.Right > 0 && s.Padding.Bottom > 0 &&
                                                  s.Head.Parent.Left == s.Padding.Left) && !page.SectionOf("alarm_logs").Framed,
              string.Join(" ", framed.Select(s => s.Key + ":" + s.Padding)));
        var shotDir = Environment.GetEnvironmentVariable("PAGE_SHOTS");
        if (!string.IsNullOrEmpty(shotDir)) {
            page.SelectTab("states"); Pump(300);
            Directory.CreateDirectory(shotDir);
            var at = page.PointToScreen(Point.Empty);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("python3", "-c \"from PIL import ImageGrab; ImageGrab.grab(bbox=(" + at.X + "," + at.Y + "," +
                (at.X + page.Width) + "," + (at.Y + page.Height) + "), xdisplay=':99').save('" + Path.Combine(shotDir, "dashboard_states.png") + "')\"") { UseShellExecute = false }).WaitForExit(10000);
            page.SelectTab("events"); Pump(100);
        }
        Check("  space between the sections", page.SectionOf("state_pie").Margin.Bottom >= 12);
        Check("the event filter under its heading, on the left", page.EventFilter.Left == 0 && page.EventFilter.Parent.Top > page.SectionOf("event_logs").Head.Parent.Top);
        Check("the alarms tab is its table: no heading, the filter at the left", page.SectionOf("alarm_logs").Head == null && page.SectionOf("alarm_logs").Open &&
              page.AlarmFilter.Left == 0 && page.SectionOf("alarm_logs").Dock == DockStyle.Fill);
        page.SelectTab("states");
        Pump(50);
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

        page.ShiftPick.Select(new[] { "08:00 - 16:00" });
        WaitFor(page);
        Check("a shift chosen: its states only", page.View.Shift != null && page.View.States.Count == 4 && page.StateGrid.RowCount == 4);
        Check("  the info bar names it", page.BarText.Contains("Active Shift: 08:00 - 16:00") && page.BarText.Contains("4 entries shown"), page.BarText);
        page.ShiftPick.Select(new[] { "08:00 - 16:00", "16:00 - 00:00" });
        WaitFor(page);
        Check("two shifts chosen: the states of both (08:00 - 00:00, joined)", page.View.States.Sum(s => s.Minutes) == 960 &&
              page.View.States.First().Start == Day.AddHours(8) && page.BarText.Contains("Active Shift: 08:00 - 16:00, 16:00 - 00:00"),
              page.View.States.Sum(s => s.Minutes) + " / " + page.BarText);
        page.ShiftPick.Select(new string[0]);
        WaitFor(page);
        var shiftRows = DashboardData.Metrics(page.View, machine.Config.Shifts);
        Check("shift metrics (the reports'): a row per shift", shiftRows.Count == 3 && Math.Abs(shiftRows[0].ActivePercent - 92.6) < 0.05);

        Check("the history list: the report times of the log", page.HistoryBox.Items.Count == 3 &&
              (string)page.HistoryBox.Items[1] == "2026-09-28 18:00", string.Join(" | ", page.HistoryBox.Items.Cast<object>()));
        page.HistoryBox.SelectedIndex = 1;
        WaitFor(page);
        Check("a report chosen: from the report before it to it", page.RangeStart == Day.AddHours(8) && page.RangeEnd == Day.AddHours(18));
        page.LatestBox.SelectedIndex = page.LatestBox.Items.IndexOf("Hourly");
        Check("while the log is read the bar says so, in bold black (not red)", page.Loading && !page.BarIsRed && page.BarIsBold && page.BarText == "Data loading...", page.BarText);
        WaitFor(page);
        Check("Latest hour: an hour up to now", page.RangeEnd - page.RangeStart == TimeSpan.FromHours(1) && page.HistoryBox.SelectedIndex == 0);
        Check("  then plain again", !page.BarIsBold && page.BarText.StartsWith("Log interval:"), page.BarText);
        page.SetRange(Day, Day.AddDays(1), latest: "Hourly");
        WaitFor(page);
        page.ChooseLatestAgain();
        WaitFor(page);
        Check("the same Latest chosen again: up to now (no refresh button needed)", page.RangeEnd - page.RangeStart == TimeSpan.FromHours(1) &&
              page.RangeEnd > Day.AddDays(2));

        page.SetRange(Day, Day.AddDays(1));
        WaitFor(page);
        var exported = page.ExportNow();
        Check("Export Report writes the files of what is shown", exported.Count == 3 &&
              File.Exists(Path.Combine(dir, "page", "state_log_20260928_0000_20260928_2359.csv")), string.Join(" ", exported.Select(Path.GetFileName)));
        Check("the export is in the audit log", File.ReadAllText(Directory.GetFiles(AuditLogger.LogDirectory).First()).Contains("exported 3 report file"));
        Check("  and in the info bar's note", page.StatusText.Contains("3 report files exported"), page.StatusText);

        Console.WriteLine("-- in Turkish");
        LanguageManager.ChangeLanguage("TR");
        WaitFor(page);
        Check("the descriptions follow the HMI's language", page.View.States.Any(s => s.Description == "\u00c7al\u0131\u015fma Aktif"));
        Check("the page's texts too", page.TabKeys[1].Text == "\u00dcretim Olaylar\u0131" && page.TabKeys[0].Text == "Makine Performans\u0131" &&
              page.SectionOf("state_pie").Title.Contains("S\u00fcre Da\u011f\u0131l\u0131m\u0131") && page.TypePick.Shown == "T\u00fcm t\u00fcrler" &&
              page.SectionOf("alarm_logs").Title.Contains("Kay\u0131tlar") && (string)page.LatestBox.Items[5] == "G\u00fcnl\u00fck" &&
              page.BarText.StartsWith("Kay\u0131t aral\u0131\u011f\u0131:"), page.TabKeys[1].Text + " / " + page.BarText);
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
        Check("below Maintenance: no folder; Export Report stays", !page.FolderButton.Enabled && page.ExportButton.Enabled);
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        Pump(100);
        Check("  signed in again: the folder again", page.FolderButton.Enabled);
        // the page made while nobody was signed in, the sign-in before it is first on screen
        SecurityManager.Login("operator", SecurityLevel.Operator);
        var early = new DashboardScreen(machine);
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        var earlyForm = new Form { ClientSize = new Size(1200, 800) };
        earlyForm.Controls.Add(early);
        earlyForm.Show();
        Pump(100);
        Check("  a page made before the sign-in: the folder usable once it is on screen", early.FolderButton.Enabled);
        earlyForm.Close();

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
        Check("the sections listed as the page has them: under their tab, with their titles",
              set.SectionBox("state_pie").Text == "Time Distribution" && set.SectionBox("state_pie").Parent is Panel &&
              set.SectionBox("event_quantity").Text == "Quantities" && set.SectionBox("event_logs").Text == "Logs" && set.SectionBox("alarm_logs").Text == "Logs" &&
              ((Panel)set.SectionBox("state_pie").Parent).Controls.Cast<Control>().Any(c => c.Text == "Machine Performance:") &&
              ((Panel)set.SectionBox("event_quantity").Parent).Controls.Cast<Control>().Any(c => c.Text == "Production Events:") &&
              ((Panel)set.SectionBox("alarm_logs").Parent).Controls.Cast<Control>().Any(c => c.Text == "Alarms:"),
              string.Join(" | ", ((Panel)set.SectionBox("state_pie").Parent).Controls.Cast<Control>().Select(c => c.Text)));
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
        set.ZoneBox.Text = "Europe/Istanbul";

        Console.WriteLine("-- the shifts, in the settings");
        Check("the shifts are listed", set.ShiftTexts.SequenceEqual(new[] { "08:00 - 16:00", "16:00 - 00:00", "00:00 - 08:00" }), string.Join(" | ", set.ShiftTexts));
        set.RemoveShift(2);
        set.ShiftPickers(1)[1].Value = DateTime.Today.AddHours(23).AddMinutes(30);
        set.AddShift(TimeSpan.FromHours(23.5), TimeSpan.FromHours(8));
        Check("taken away, changed, added - and kept", set.Save() &&
              machine.Config.Shifts.Select(x => x.Text).SequenceEqual(new[] { "08:00 - 16:00", "16:00 - 23:30", "23:30 - 08:00" }),
              string.Join(" | ", machine.Config.Shifts.Select(x => x.Text)));
        Pump(300);
        Check("  the dashboard's Shift Selection follows", page.ShiftPick.Items.Count == 3 && page.ShiftPick.Items[2].Text == "23:30 - 08:00",
              string.Join(" | ", page.ShiftPick.Items.Select(i => i.Text)));
        set.AddShift(TimeSpan.FromHours(10), TimeSpan.FromHours(10));
        Check("  a shift that starts and ends at once is refused", !set.Save() && set.ProblemText.Contains("10:00 - 10:00"), set.ProblemText);

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
