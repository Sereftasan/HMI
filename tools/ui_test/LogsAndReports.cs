using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.Profiles.Data;
using Kaban.UI.Dashboard;
using Kaban.UI.Settings;

// LOGS AND REPORTS (2 October): the dashboard's settings are the HMI's - HMI
// Settings' log page, SETTINGS ONLY: the HMI log's switches, a machine to choose
// and its log database and reports; not a Machine Parameters tab. The logs are
// read on the Dashboard page, in tabs: the machine's states, events and alarms,
// then the logs by kind, then all of them - ONE table with the HMI's log and the
// machine's, its filters on the left. The empty dashboard says what the database
// holds, or why it cannot be read.
class LR {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static bool Until(Func<bool> ok, int seconds = 20) {
        var until = DateTime.Now.AddSeconds(seconds);
        while (!ok() && DateTime.Now < until) Pump(50);
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
    static void MakeLog(string path, bool withMain = true, string dateText = null) {
        _c = new NativeSqliteConnection { ConnectionString = "Data Source=" + path };
        _c.Open();
        if (withMain) {
            Exec("CREATE TABLE main_log(id INTEGER PRIMARY KEY AUTOINCREMENT, state_date TEXT, state_code INTEGER)");
            Exec("CREATE TABLE log_details(id INTEGER PRIMARY KEY AUTOINCREMENT, main_log_id INTEGER, log_description TEXT, deger REAL, str_deger TEXT)");
        } else Exec("CREATE TABLE something_else(id INTEGER)");
        if (dateText != null) Exec("INSERT INTO main_log(state_date, state_code) VALUES (@p0, 2)", dateText);
        _c.Close();
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "logsrep_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        DashboardConfig.FolderOverride = Path.Combine(dir, "settings");
        DashboardService.AutoReports = false;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        AuditLogger.Log("changed a parameter on the HMI.");

        // a machine's log: today, in UTC (this PC's zone is the panel's: UTC)
        var now = DateTime.Now;
        string db = Path.Combine(dir, "database.sqlite");
        MakeLog(db);
        _c.Open();
        Row(now.AddHours(-3), 2);
        Row(now.AddHours(-2), 8);
        Row(now.AddMinutes(-70), 4, "Robot X Error");
        Row(now.AddMinutes(-50), 2);
        _c.Close();

        var machine = DashboardService.Register("LOGTEST", Path.Combine("Dashboard", "config.json"),
                                                Path.Combine("Dashboard", "StateMap.json"), Path.Combine("Dashboard", "Translation_JSON.csv"));
        var cfg = machine.Config.Clone();
        cfg.DatabasePath = db;
        cfg.TimeZoneName = "UTC";
        machine.Replace(cfg);
        var utc = DashboardTime.Find("UTC");

        Console.WriteLine("--- what a database holds ---");
        var p = DashboardData.Check(db, utc);
        Check("the machine's log: its logs, the first and the last", p.Ok && p.Logs == 4 && p.Last.HasValue &&
              Math.Abs((p.Last.Value - now.AddMinutes(-50)).TotalSeconds) < 2 && p.Describe().StartsWith("4 logs, "), p.Describe());
        string wrong = Path.Combine(dir, "other.sqlite");
        MakeLog(wrong, withMain: false);
        Check("another program's database: says it is not a machine log", !DashboardData.Check(wrong, utc).Ok &&
              DashboardData.Check(wrong, utc).Problem.Contains("main_log") && DashboardData.Check(wrong, utc).Problem.Contains("something_else"),
              DashboardData.Check(wrong, utc).Problem);
        Check("a file that is not there", DashboardData.Check(Path.Combine(dir, "none.sqlite"), utc).Problem.Contains("not found"));
        Check("none set", DashboardData.Check("", utc).Problem == "No database is set.");
        string empty = Path.Combine(dir, "empty.sqlite");
        MakeLog(empty);
        Check("no logs yet", DashboardData.Check(empty, utc).Problem == "The database holds no logs yet.");
        string dotted = Path.Combine(dir, "dotted.sqlite");
        MakeLog(dotted, dateText: "01.10.2026 08:00:00");
        Check("dates it cannot read: says how they are written", DashboardData.Check(dotted, utc).Problem.Contains("\"01.10.2026 08:00:00\""),
              DashboardData.Check(dotted, utc).Problem);
        string notDb = Path.Combine(dir, "notes.sqlite");
        File.WriteAllText(notDb, "this is not a database at all, just some text written into a file with a database's name");
        Check("a file that is no database at all: could not be opened, and why", DashboardData.Check(notDb, utc).Problem.StartsWith("The database could not be opened:"),
              DashboardData.Check(notDb, utc).Problem);

        Console.WriteLine("--- the machines, for the HMI's pages ---");
        var links = Kaban.UI.Helpers.MachineLinks.WithLogs();
        Check("the machines with a log database", links.Count == 1 && links[0].ModuleId == "LOGTEST" && links[0].Dashboard == machine && links[0].MainStation == 1);

        Console.WriteLine("--- Logs and Reports: the settings only ---");
        var page = new LogSettingsPage();
        Check("HMI Settings' log page is Logs and Reports", LanguageManager.GetString(page.TitleKey, page.TitleFallback) == "Logs and Reports" &&
              page.TitleFallback == "Logs and Reports");
        var host = new Form { ClientSize = new Size(1200, 700) };
        var ls = (LogSettingsControl)page.GetControl();
        host.Controls.Add(ls);
        host.Show();
        Pump(100);
        Check("a machine to choose", ls.MachineBox.Items.Count == 1 && ls.MachineBox.Text == "LOGTEST");
        Check("  its log database and reports on the page (the dashboard's settings, no window)", ls.MachineSettings != null &&
              ls.MachineSettings.Parent != null && ls.MachineSettings.DatabaseBox.Text == db);
        Check("  what its database holds, tested as the page opens", Until(() => ls.MachineSettings.LastProbe != null) &&
              ls.MachineSettings.LastProbe.Ok && ls.MachineSettings.LastProbe.Logs == 4);
        Check("no log table here: the logs are read on the Dashboard page", ls.Viewer == null);

        Console.WriteLine("--- the Dashboard page: the logs in tabs ---");
        AuditLogger.Log("logged in as setter.");
        var tabsDash = new DashboardScreen(machine);
        var tform = new Form { ClientSize = new Size(1400, 900) };
        tform.Controls.Add(tabsDash);
        tform.Show();
        Pump(200);
        var keys = tabsDash.TabKeys.Select(t => t.Key).ToList();
        Check("the tabs: states, events, alarms, then the logs by kind, then all of them",
              keys.SequenceEqual(new[] { "states", "events", "alarms", "log_signin", "log_connection", "log_writes", "log_settings",
                                         "log_problems", "log_other", "log_all" }), string.Join(",", keys));
        Check("  named", tabsDash.TabKeys[0].Text.EndsWith("Machine states") && tabsDash.TabKeys[1].Text.EndsWith("Operation events") &&
              tabsDash.TabKeys[2].Text.EndsWith("Alarms") && tabsDash.TabKeys.Last().Text.EndsWith("All logs"),
              string.Join(",", tabsDash.TabKeys.Select(t => t.Text)));
        Check("  the charts and tables on their tabs", tabsDash.SectionOf("state_pie").Parent.Parent.Tag as string == "states" &&
              tabsDash.SectionOf("cutting_details").Parent.Parent.Tag as string == "events" &&
              tabsDash.SectionOf("alarm_logs").Parent.Parent.Tag as string == "alarms");
        Check("the log filters on the left: hidden on a machine tab", tabsDash.SelectedTabKey == "states" && !tabsDash.LogTable.SearchBox.Parent.Visible);
        // the range around the test's rows (the dashboard's range is in the machine's zone)
        tabsDash.SetRange(now.AddHours(-4), now.AddMinutes(5));
        Check("the machine's log read first", Until(() => !tabsDash.Loading && tabsDash.View != null && tabsDash.View.Alarms.Count == 1));
        tabsDash.SelectTab("log_all");
        Pump(100);
        var v = tabsDash.LogTable;
        Check("  shown on a log tab", tabsDash.LogTable.SearchBox.Parent.Visible && v.Parent != null && (string)v.Parent.Tag == "log_all");
        Check("All logs: a Source column and filter (the HMI and this machine)", v.SourceColumnShown &&
              v.SourceChoices.SequenceEqual(new[] { "All sources", "HMI", "LOGTEST" }) && v.SourceBox.Visible, string.Join(",", v.SourceChoices));
        Func<int, IEnumerable<int>> rows = n => Enumerable.Range(0, n);
        Check("  the machine's log in it", Until(() => rows(v.RowCount).Any(r => v.CellText(r, 5) == "LOGTEST")), v.StatusText);
        var sources = rows(v.RowCount).Select(r => v.CellText(r, 5)).Distinct().ToList();
        Check("  ONE table: the HMI's entries and the machine's", sources.Contains("HMI") && sources.Contains("LOGTEST"), string.Join(",", sources));
        var alarm = rows(v.RowCount).FirstOrDefault(r => v.CellText(r, 3) == "Machine alarm");
        Check("its alarm with the bits that were on", alarm >= 0 && v.CellText(alarm, 4).Contains("Robot X Error") && v.CellText(alarm, 2) == "1",
              alarm < 0 ? "" : v.CellText(alarm, 4));
        var states = rows(v.RowCount).Where(r => v.CellText(r, 3) == "Machine state").Select(r => v.CellText(r, 4)).ToList();
        Check("its states with how long", states.Count >= 2 && states.All(st => st.Contains(" min")), string.Join(" | ", states));
        Check("  newest first, mixed by time", DateTime.Parse(v.CellText(0, 0).Replace("  ", " ")) >= DateTime.Parse(v.CellText(v.RowCount - 1, 0).Replace("  ", " ")));

        v.SetSource("LOGTEST");
        Pump(50);
        Check("Source: the machine - its log only", v.RowCount > 0 && rows(v.RowCount).All(r => v.CellText(r, 5) == "LOGTEST"));
        v.SetSource("");
        Pump(50);
        Check("Source: HMI - the HMI's log only", v.RowCount > 0 && rows(v.RowCount).All(r => v.CellText(r, 5) == "HMI"));
        v.SetSource(null);
        v.SearchText = "Robot";
        Check("the search (on the left) finds the machine's entries too", v.RowCount == 1 && v.CellText(0, 5) == "LOGTEST");
        v.SearchText = "";
        v.SelectRow(0);
        Check("the entry in full names its source", v.DetailText.Contains("LOGTEST") || v.DetailText.Contains("HMI"), v.DetailText);
        var shifts = machine.Config.Shifts;
        int away = Enumerable.Range(0, shifts.Count).First(i => !DashboardData.InShift(DateTime.Now.TimeOfDay, shifts[i]));
        tabsDash.ShiftBox.SelectedIndex = away + 1;
        Check("a shift chosen: the HMI's entries of its hours only, as the machine's", Until(() => !tabsDash.Loading && v.RowCount >= 0 &&
              !rows(v.RowCount).Any(r => v.CellText(r, 5) == "HMI")), v.StatusText);
        tabsDash.ShiftBox.SelectedIndex = 0;
        Check("  all shifts: all of them again", Until(() => rows(v.RowCount).Any(r => v.CellText(r, 5) == "HMI")));

        tabsDash.SelectTab("log_signin");
        Pump(100);
        Check("a kind's tab: that kind only", v.Parent != null && (string)v.Parent.Tag == "log_signin" && v.RowCount >= 1 &&
              rows(v.RowCount).All(r => v.CellText(r, 3) == "Sign-in, users") && rows(v.RowCount).Any(r => v.CellText(r, 4).Contains("logged in as setter")),
              string.Join(" | ", rows(v.RowCount).Select(r => v.CellText(r, 3) + ": " + v.CellText(r, 4))));
        Check("  the Source filter on the all-logs tab only", !v.SourceBox.Visible);
        tabsDash.SelectTab("log_other");
        Pump(100);
        Check("  Other: the HMI's parameter change, none of the machine's", rows(v.RowCount).Any(r => v.CellText(r, 4).Contains("changed a parameter")) &&
              rows(v.RowCount).All(r => v.CellText(r, 3) == "Other"));
        tabsDash.SelectTab("alarms");
        Pump(50);
        Check("back on a machine tab: the log filters go", !tabsDash.LogTable.SearchBox.Parent.Visible);

        var shown = new List<AuditEntry>
        {
            new AuditEntry { Time = now, User = "setter", Text = "a", Kind = AuditKind.Settings },
            new AuditEntry { Time = now, Text = "Robot X Error", Kind = AuditKind.MachineAlarm, Station = 1, Source = "LOGTEST" }
        };
        var csv = LogViewerControl.ExportText(shown, true, v.SourceName);
        Check("the export has the Source", csv.StartsWith("Time;Source;User;PLC;Kind;Entry\r\n") && csv.Contains(";LOGTEST;;1;Machine alarm;Robot X Error"), csv);
        Check("  and, without the machines, is as it was", LogViewerControl.ExportText(shown, true).StartsWith("Time;User;PLC;Kind;Entry\r\n"));
        var txt = LogViewerControl.ExportText(shown, false, v.SourceName);
        Check("  the text file names the machine and the kind", txt.Contains("] - LOGTEST - Machine alarm: Robot X Error"), txt);

        Console.WriteLine("--- the machine's database changed in its settings ---");
        tabsDash.SelectTab("log_all");
        var c2 = machine.Config.Clone();
        c2.DatabasePath = wrong;
        machine.Replace(c2);
        Check("the page says why it cannot be read, and the machine's entries go", Until(() => !tabsDash.Loading && tabsDash.InfoText.Contains("main_log") &&
              !rows(v.RowCount).Any(r => v.CellText(r, 5) == "LOGTEST")), tabsDash.InfoText);
        var c3 = machine.Config.Clone();
        c3.DatabasePath = db;
        machine.Replace(c3);
        Check("back", Until(() => rows(v.RowCount).Any(r => v.CellText(r, 5) == "LOGTEST")));
        tform.Close();

        Console.WriteLine("--- the dashboard's settings ---");
        var set = new DashboardSettingsControl(machine);
        var sform = new Form { ClientSize = new Size(800, 700) };
        sform.Controls.Add(set);
        sform.Show();
        set.TestDatabase();
        Check("\U0001F50D beside the database: what it holds", Until(() => set.LastProbe != null) && set.LastProbe.Ok && set.LastProbe.Logs == 4);
        set.DatabaseBox.Text = wrong;
        set.TestDatabase();
        Check("  another file: why not", Until(() => set.LastProbe != null && !set.LastProbe.Ok) && set.LastProbe.Problem.Contains("main_log"));
        sform.Close();

        Console.WriteLine("--- the dashboard page, nothing in its range ---");
        var dash = new DashboardScreen(machine);
        var dform = new Form { ClientSize = new Size(1200, 800) };
        dform.Controls.Add(dash);
        dform.Show();
        Pump(200);
        dash.SetRange(new DateTime(2020, 1, 1), new DateTime(2020, 1, 2));
        Check("it says what the database holds", Until(() => !dash.Loading && dash.InfoText.Contains("The database holds 4 logs")), dash.InfoText);
        dform.Close();

        Console.WriteLine("--- not a Machine Parameters tab any more ---");
        var screens = Kaban.UI.Screens.StandardScreens.Build(new Ctx(), new Kaban.UI.Screens.StandardScreens.Options
        {
            Home = () => new UserControl(),
            Dashboard = new DashboardDefinition()
        }).ToList();
        var mp = screens.First(s => s.Role == ScreenRoles.MachineParameters);
        var view = mp.CreateView() as Control;
        var titles = new List<string>();
        // one page left: the page itself, not a tab strip of one
        if (view is Kaban.UI.Settings.SettingsScreen) titles.AddRange(((Kaban.UI.Settings.SettingsScreen)view).AllPages.Select(pg => pg.TitleFallback));
        else if (view is AlarmSettingsControl) titles.Add("Alarm Settings");
        Check("Machine Parameters: no Dashboard tab", titles.Count > 0 && !titles.Contains("Dashboard") && titles.Contains("Alarm Settings"), string.Join(",", titles));
        Check("  the Dashboard page itself stays", screens.Any(s => s.TitleKey == "Btn_Dashboard"));

        host.Close();
        ls.Dispose();
        Console.WriteLine(fail == 0 ? "\nLOGS AND REPORTS ALL PASS" : "\nLOGS AND REPORTS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    class Ctx : IMachineMountContext {
        public string ModuleId { get { return "DASHTEST2"; } }
        public bool IsMainMachine { get { return true; } }
        public int Station(int n) { return n; }
        public string Scoped(string localId) { return "DASHTEST2." + localId; }
        public long AllocateMachineBit(string localTag) { return 16; }
        public string ResolveFile(string fileName) { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName); }
    }
}
