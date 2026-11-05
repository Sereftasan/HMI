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

// 29 October: "This the current industry 4 logging program, the form in Industry4Test reads the logs
// from PLC and writes them to database which we currently read to show them on the dashboard. It uses
// Industry4Library project for this purpose. I want you to read this program and create the same logger
// tool to our application as well so we won't need the windows service anymore to do the job."
// What Industry4Library's ServiceClass does, record for record: the PLC's log area (R27000: waiting,
// busy / answer, records from +10, ten words each), main_log and log_details as it wrote them.
class ML {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }

    // the PLC: words by address (R27000 ...), and what the logger wrote
    sealed class FakePlc : MachineLogger.ILink {
        public readonly Dictionary<string, int> Mem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Writes = new List<string>();
        public bool Refuse;
        public bool Connected { get { return true; } }
        public bool Read(string first, int count, out short[] words) {
            words = new short[count];
            for (int i = 0; i < count; i++) {
                int v;
                words[i] = Mem.TryGetValue(PlcAddressHelper.OffsetAddress(first, i), out v) ? unchecked((short)v) : (short)0;
            }
            return true;
        }
        public int Write(string device, int value) {
            Writes.Add(device + "=" + value);
            if (Refuse && device == "R27001") return -1;
            Mem[device] = value;
            // the PLC: the records answered are taken off, and its +1 back to 0 (ready)
            if (device == "R27001") { Mem["R27000"] = Math.Max(0, Get("R27000") - value); Mem["R27001"] = 0; }
            return 0;
        }
        public int Get(string a) { int v; return Mem.TryGetValue(a, out v) ? v : 0; }
        // a record at record slot n (0 = R27010): its code, its date, its words
        public void Record(int slot, int code, DateTime t, params int[] words) {
            int at = 27010 + slot * 10;
            Mem["R" + at] = code;
            var date = new[] { t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second };
            for (int i = 0; i < 6; i++) Mem["R" + (at + 1 + i)] = date[i];
            for (int i = 0; i < words.Length; i++) Mem["R" + (at + 7 + i)] = words[i];
        }
        public void Continuation(int slot, int code, params int[] words) {
            int at = 27010 + slot * 10;
            Mem["R" + at] = code;
            for (int i = 0; i < words.Length; i++) Mem["R" + (at + 1 + i)] = words[i];
        }
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
    static string Details(long id) {
        return string.Join(" | ", Rows("SELECT log_description, deger, str_deger FROM log_details WHERE main_log_id = " + id + " ORDER BY id")
            .Select(r => r[0] + ":" + (r[1] is DBNull ? "-" : Convert.ToDouble(r[1], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)) +
                         (r[2] is DBNull ? "" : ":" + r[2])));
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "machlog_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        DashboardConfig.FolderOverride = Path.Combine(dir, "settings");
        DashboardService.AutoReports = false;
        MachineLogger.AutoStart = false;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        _db = Path.Combine(dir, "Industry4", "database.sqlite");
        var map = StateMap.Load(Path.Combine("Dashboard", "StateMap.json"), Path.Combine("Dashboard", "Translation_JSON.csv"));
        Check("FA2010's state map read with what the logger needs: ActiveDevice, Active, the values' words, bits and multipliers",
              map.Find(2).ActiveDevice == "L1002" && map.Find(2).Active && !map.Find(10).Active &&
              map.Find(4).Params.Count == 2 && map.Find(4).Params[0].Bit32 && map.Find(4).Params[0].Bits.Count == 32 &&
              map.Find(9).Params[0].Multiplier == 10 && map.Find(14).PreviousCode == 4,
              map.Count + " entries");

        // 5 November: "'12' state coded log is 'Axis overload' actually"; "20 Profile not recognised" among FA2010's logs
        Check("code 12 is Axis overload; code 20, Profile not recognised (L1020: measured width and height, the required SW state)",
              map.Find(12).Description == "Axis overload" && map.Find(20) != null && map.Find(20).ActiveDevice == "L1020" && map.Find(20).Params.Count == 3 &&
              map.Translate("Axis overload", "T\u00fcrk\u00e7e") == "Eksen a\u015f\u0131r\u0131 y\u00fck\u00fc");

        Console.WriteLine("--- the PLC told what to log ---");
        var plc = new FakePlc();
        string said = MachineLogger.WriteActive(plc, map);
        Check("each state that starts a record: its ActiveDevice = its Active (L1002 = 1, L1010 = 0), none for a continuation",
              plc.Writes.Contains("L1002=1") && plc.Writes.Contains("L1010=0") && plc.Writes.Contains("L1000=1") &&
              !plc.Writes.Any(w => w.StartsWith("=")) && plc.Writes.Count == map.Entries.Count(e => !e.PreviousCode.HasValue && e.ActiveDevice.Length > 0),
              said + ": " + string.Join(",", plc.Writes.Take(6)));
        // 5 November: "add the controls for the selection of which events to be logged" - a record's own choice in the
        // machine's settings stands before the state map's Active
        var chosen = DashboardConfig.FromText("{}");
        chosen.SetLoggedRecords(new Dictionary<int, bool> { { 2, false }, { 10, true } });
        var again = DashboardConfig.FromText(chosen.ToText());
        plc.Writes.Clear();
        MachineLogger.WriteActive(plc, map, again);
        Check("a record chosen otherwise (Operation is active off, Welding done on): its bit so - the others as the map has them",
              plc.Writes.Contains("L1002=0") && plc.Writes.Contains("L1010=1") && plc.Writes.Contains("L1000=1") && plc.Writes.Contains("L1006=0") &&
              again.LoggedRecord(2) == false && again.LoggedRecord(10) == true && again.LoggedRecord(0) == null && chosen.ToText().Contains("\"logged_records\""),
              string.Join(",", plc.Writes.Take(12)));
        Check("  the records that can be switched: those that start one and have a bit (25 of FA2010's 35) - a continuation logged with its first",
              map.Switchable.Count() == 25 && !map.Switchable.Any(e => e.PreviousCode.HasValue) && map.ContinuationsOf(4).Single().Code == 14 &&
              map.ContinuationsOf(100).Single().Code == 150, map.Switchable.Count() + "");
        chosen.SetLoggedRecords(new Dictionary<int, bool>());
        Check("  none chosen otherwise: kept nowhere", !chosen.ToText().Contains("logged_records"));

        Console.WriteLine("--- nothing waiting ---");
        bool opened = false;
        Func<System.Data.Common.DbConnection> openDb = () => { opened = true; return _open ?? (_open = LogDatabase.OpenForWriting(_db)); };
        plc.Writes.Clear();
        var r = MachineLogger.PollOnce(plc, openDb, map, "R27000");
        Check("R27000 = 0: nothing written, nothing answered, the database not even opened", r.Written == 0 && plc.Writes.Count == 0 && !opened && !File.Exists(_db));

        Console.WriteLine("--- a state: Operation is active ---");
        var t1 = new DateTime(2026, 10, 29, 6, 15, 2);
        plc.Mem["R27000"] = 1;
        plc.Record(0, 2, t1);
        r = MachineLogger.PollOnce(plc, openDb, map, "R27000");
        var main = Rows("SELECT id, state_code, state_date FROM main_log ORDER BY id");
        Check("written: main_log (2, \"2026-10-29 06:15:02\" - the PLC's date as it was sent), no details", r.Written == 1 && main.Count == 1 &&
              Convert.ToInt32(main[0][1]) == 2 && (string)main[0][2] == "2026-10-29 06:15:02" && Details(Convert.ToInt64(main[0][0])) == "",
              main.Count + " rows");
        Check("  the database made where it is set, with the service's tables (and str_deger)", File.Exists(_db) &&
              Rows("SELECT name FROM sqlite_master WHERE type='table'").Any(x => (string)x[0] == "log_details") &&
              Rows("PRAGMA table_info(log_details)").Any(x => (string)x[1] == "str_deger"));
        Check("  answered: R27001 = 1 (the records taken)", plc.Writes.Contains("R27001=1") && plc.Get("R27000") == 0, string.Join(",", plc.Writes));

        Console.WriteLine("--- a cut with its Process ID (32 bits), looked up in KMSystem ---");
        plc.Writes.Clear();
        plc.Mem["R27000"] = 1;
        int pid = 2580330;
        plc.Record(0, 8, t1.AddSeconds(30), pid & 0xFFFF, pid >> 16);
        r = MachineLogger.PollOnce(plc, openDb, map, "R27000",
            id => id == pid ? new MachineLogger.KmSystem.Label { ProfileCode = "136091", ProfileData = "Profile;04860;08560" } : null);
        main = Rows("SELECT id, state_code, state_date FROM main_log ORDER BY id");
        Check("Process ID 2580330 from its two words; Profile Code and Profile Data after it, as the service added them",
              Details(Convert.ToInt64(main.Last()[0])) == "Process ID:2580330 | Profile Code:-:136091 | Profile Data:-:Profile;04860;08560",
              Details(Convert.ToInt64(main.Last()[0])));

        Console.WriteLine("--- a cut of the cutting machine: values / 10 ---");
        plc.Mem["R27000"] = 1;
        plc.Record(0, 9, t1.AddMinutes(1), 12345, 450, -450);
        r = MachineLogger.PollOnce(plc, openDb, map, "R27000");
        main = Rows("SELECT id, state_code FROM main_log ORDER BY id");
        Check("Length 1234.5, Left Angle 45, Right Angle -45 (a signed word)",
              Details(Convert.ToInt64(main.Last()[0])) == "Length:1234.5 | Left Angle:45 | Right Angle:-45", Details(Convert.ToInt64(main.Last()[0])));

        Console.WriteLine("--- an error with the record continuing it ---");
        plc.Writes.Clear();
        plc.Mem["R27000"] = 1;            // only one of the two there yet
        // First 32 Errors: Robot X Error (bit 0) and Milling Collision Alarm (bit 15); Following 16: Emergency Stop (bit 0), Low Air Pressure (bit 1)
        plc.Record(0, 4, t1.AddMinutes(2), (1 << 0) | (1 << 15), 0, 3);
        // (3): bit 31 of the first 32-bit word
        plc.Continuation(1, 14, 0, unchecked((short)0x8000), 0, 0, 0, 0, 0, 0);
        r = MachineLogger.PollOnce(plc, openDb, map, "R27000");
        Check("waiting 1, the chain 2 (4 and its continuation 14): not taken yet", r.Written == 0 && plc.Writes.Count == 0);
        plc.Mem["R27000"] = 2;
        plc.Mem["R27001"] = 1;            // the PLC busy
        r = MachineLogger.PollOnce(plc, openDb, map, "R27000");
        Check("  the PLC busy (+1 not 0): not taken", r.Written == 0 && plc.Writes.Count == 0);
        plc.Mem["R27001"] = 0;
        r = MachineLogger.PollOnce(plc, openDb, map, "R27000");
        main = Rows("SELECT id, state_code FROM main_log ORDER BY id");
        string err = Details(Convert.ToInt64(main.Last()[0]));
        Check("ready: ONE main_log row (4), its details the bits that are on, of both records; answered 2",
              r.Written == 1 && Convert.ToInt32(main.Last()[1]) == 4 &&
              err == "Robot X Error:1 | Milling Collision Alarm:1 | Emergency Stop:1 | Low Air Pressure:1 | " + map.Find(14).Params[0].Bits[31] + ":1" &&
              plc.Writes.Contains("R27001=2"), err + " / " + string.Join(",", plc.Writes));

        Console.WriteLine("--- what the service left out ---");
        int before = main.Count;
        plc.Writes.Clear();
        plc.Mem["R27000"] = 1;
        plc.Record(0, 999, t1.AddMinutes(3));
        r = MachineLogger.PollOnce(plc, openDb, map, "R27000");
        Check("a code the state map does not know: answered (1), nothing written, said", r.Skipped == 1 && plc.Writes.Contains("R27001=1") &&
              Rows("SELECT id FROM main_log").Count == before && r.Problem.Contains("999"), r.Problem);
        plc.Writes.Clear();
        plc.Mem["R27000"] = 1;
        plc.Record(0, 2, t1);
        plc.Mem["R27012"] = 13;           // month 13
        r = MachineLogger.PollOnce(plc, openDb, map, "R27000");
        Check("a date that cannot be (month 13): answered, nothing written", r.Skipped == 1 && plc.Writes.Contains("R27001=1") &&
              Rows("SELECT id FROM main_log").Count == before, r.Problem);

        Console.WriteLine("--- the answer refused ---");
        plc.Writes.Clear();
        plc.Refuse = true;
        plc.Mem["R27000"] = 1;
        plc.Record(0, 3, t1.AddMinutes(4));
        r = MachineLogger.PollOnce(plc, openDb, map, "R27000");
        Check("R27001 refused: the record rolled back - the PLC sends it again", r.Written == 0 && Rows("SELECT id FROM main_log").Count == before &&
              r.Problem.Contains("refused"), r.Problem);
        plc.Refuse = false;
        r = MachineLogger.PollOnce(plc, openDb, map, "R27000");
        Check("  answered the next time: written once", r.Written == 1 && Rows("SELECT id FROM main_log WHERE state_code = 3").Count == 1);
        _open.Dispose(); _open = null;

        Console.WriteLine("--- the dashboard reads what was written ---");
        var zone = TimeZoneInfo.Utc;
        var data = DashboardData.Load(_db, new DateTime(2026, 10, 29), new DateTime(2026, 10, 30), zone, map, "English");
        var view = DashboardData.Build(data, false, null);
        Check("states, events (the cuts) and the alarm as written", view.States.Any(s => s.Code == 2) && view.Events.Any(e => e.Code == 8 && e.Details.Contains("2580330")) &&
              view.Alarms.Count == 1 && view.Alarms[0].Details.Contains("Robot X Error"),
              view.States.Count + " states, " + view.Events.Count + " events, " + view.Alarms.Count + " alarms");

        Console.WriteLine("--- the logger on the HMI's own driver (the simulator) ---");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(1);
        PlcManager.Instance.RegisterDriver(1, sim);
        sim.Connect();
        var board = DashboardService.Register("FA2010", Path.Combine("Dashboard", "config.json"), Path.Combine("Dashboard", "StateMap.json"),
                                              Path.Combine("Dashboard", "Translation_JSON.csv"), "R27000");
        board.Change(c => c.DatabasePath = _db);
        MachineLogger.ServiceRunning = () => false;
        var logger = new MachineLogger(board, 1);
        logger.Start();
        Pump(800);
        Check("started: the PLC told what to log (L1002 = 1 in the simulator), said in the audit log",
              sim.Read("L1002") == 1 && sim.Read("L1010") == 0 && File.ReadAllText(AuditLogger.TodayFile).Contains("Machine log (FA2010): taken from PLC station 1, log area R27000"),
              logger.Status);
        foreach (var kv in new Dictionary<string, int> { { "R27010", 5 }, { "R27011", 2026 }, { "R27012", 10 }, { "R27013", 29 }, { "R27014", 9 }, { "R27015", 0 }, { "R27016", 0 } })
            sim.Write(kv.Key, kv.Value);
        sim.Write("R27001", 0);
        sim.Write("R27000", 1);
        var until = DateTime.Now.AddSeconds(5);
        while (DateTime.Now < until && Rows("SELECT id FROM main_log WHERE state_code = 5").Count == 0) Pump(100);
        Check("a record in the simulator's log area: in the database, answered (R27001 = 1), said so",
              Rows("SELECT state_date FROM main_log WHERE state_code = 5").Select(x => (string)x[0]).FirstOrDefault() == "2026-10-29 09:00:00" &&
              sim.Read("R27001") == 1 && logger.Records == 1 && logger.Status.Contains("1 written"),
              logger.Status);
        Check("  its writes are not in the PLC write log (the logger's handshake, not a user's)",
              !PlcWriteLog.Read(DateTime.Today, DateTime.Now.AddMinutes(1)).Any(w => w.Device == "R27001" || w.Device == "L1002"));
        MachineLogger.ServiceRunning = () => true;
        until = DateTime.Now.AddSeconds(12);
        while (DateTime.Now < until && !logger.Status.Contains("Industry 4 service")) Pump(100);
        Check("the Industry 4 service running on the PC: the HMI waits, says why", logger.Status.Contains("Industry 4 service runs on this PC"), logger.Status);
        board.Change(c => c.PlcLogging = false);
        MachineLogger.ServiceRunning = () => false;
        until = DateTime.Now.AddSeconds(5);
        while (DateTime.Now < until && !logger.Status.StartsWith("Switched off")) Pump(100);
        Check("switched off in the settings: says so", logger.Status.StartsWith("Switched off"), logger.Status);
        logger.Stop();

        // 30 October: "we should create a setting inside 'Machine Parameters' for each machine as well btw (visible
        // when HMI industry 4 logging activated). We can change the alarms setting page name to 'Alarms and Logs' and
        // put the setting on the page"
        Console.WriteLine("--- its settings on Machine Parameters > Alarms and Logs ---");
        HmiFeatures.SettingsPath = Path.Combine(dir, "Features.cfg");
        HmiFeatures.Reload();
        board.Change(c => { c.PlcLogging = true; c.PlcLogArea = ""; });
        var page = new MachineLogSettings("FA2010");
        var form = new Form { Width = 1200, Height = 800 };
        form.Controls.Add(page);
        form.Show();
        Pump(200);
        Check("hidden while the HMI does not log (HMI Settings > Features off)", !page.Visible);
        HmiFeatures.Set(HmiFeatures.Industry4Logging, true);
        Pump(100);
        Check("  shown once it does", page.Visible);
        var on = All(page).OfType<CheckBox>().First(b => b.Name == "plcLogging");
        var area = All(page).OfType<TextBox>().First(b => b.Name == "logArea");
        Check("the switch ticked, the area the machine's own (R27000)", on.Checked && area.Text == "R27000", area.Text);
        int edits = 0;
        page.Edited += (s, e) => edits++;
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        area.Text = "D7000";
        string problem;
        Check("another area kept (D7000) - the page told of the edit", edits > 0 && page.Save(out problem) && board.Config.PlcLogArea == "D7000" &&
              MachineLogger.Area(board.Config, board) == "D7000");
        area.Text = "M100";
        Check("  M100: refused, said, nothing written", !page.Save(out problem) && problem.Contains("word device") && board.Config.PlcLogArea == "D7000", problem);
        area.Text = "R27000";
        Check("  the machine's own again: kept as \"\" (follows the machine)", page.Save(out problem) && board.Config.PlcLogArea == "");
        on.Checked = false;
        page.Save(out problem);
        Check("  the machine not logged: kept", !board.Config.PlcLogging);
        board.Change(c => c.PlcLogging = true);
        page.Fill();
        Check("  filled again from the settings (the page's revert)", on.Checked);
        form.Close();
        var none = new MachineLogSettings("H500");
        Check("a machine with no log database: says so", none.Machine == null && All(none).OfType<Label>().Any(l => l.Name == "noLogDatabase" && l.Text.Contains("no log database")));
        HmiFeatures.Set(HmiFeatures.Industry4Logging, false);

        Console.WriteLine(fail == 0 ? "MACHINE LOG ALL PASS" : "MACHINE LOG " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    static System.Data.Common.DbConnection _open;
    static IEnumerable<Control> All(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; } }
}
