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
using Kaban.UI.Controls;
using Kaban.UI.Dashboard;

// 5 November: "These are not our client requirements so we are not currently taking such logs but I think we should start it to
// have a better competence. I want you make a search about what logs would be useful for these controls so we could apply them" -
// then, of the plan's records: "Your suggestion"; "this same HMI program is gonna be deployed to the currently operating old
// machines as well as the new producing ones. So it should be selected at the HMI setting's option page"; and for a long stop:
// "Yes, we could ask it to enter the reason".
// THE HMI'S OWN RECORDS OF A MACHINE (MachineRecords): taken for a new machine, written into its log database as its own records.
class HR {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static IEnumerable<Control> All(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var x in All(k)) yield return x; } }
    static void Shot(Control c, string name) {
        var folder = Environment.GetEnvironmentVariable("PAGE_SHOTS");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        Pump(200);
        var at = c.PointToScreen(Point.Empty);
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("python3",
            "-c \"from PIL import ImageGrab; ImageGrab.grab(bbox=(" + at.X + "," + at.Y + "," + (at.X + c.Width) + "," + (at.Y + c.Height) + "), xdisplay=':99').save('" +
            Path.Combine(folder, name + ".png") + "')\"") { UseShellExecute = false });
        p.WaitForExit(10000);
    }

    static Dictionary<string, int> mem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    static int Read(string a) { int v; return mem.TryGetValue(a, out v) ? v : 0; }
    static DateTime now = new DateTime(2026, 11, 5, 9, 0, 0);
    static void Scan() { now = now.AddSeconds(0.1); AlarmGeneration.Evaluate(1, Read, now); }
    static void Run(double s) { for (double t = 0; t < s - 1e-9; t += 0.1) Scan(); }

    static string _db;
    static List<object[]> Rows(string sql) {
        var list = new List<object[]>();
        if (!File.Exists(_db)) return list;
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
            .Select(r => r[0] + ":" + (r[2] is DBNull ? (r[1] is DBNull ? "-" : Convert.ToDouble(r[1], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)) : r[2].ToString())));
    }
    static List<object[]> Records(int code) { return Rows("SELECT id, state_date FROM main_log WHERE state_code = " + code + " ORDER BY id"); }

    // what MachineRecords took (its event), as it came
    static readonly List<string> taken = new List<string>();
    static readonly Dictionary<int, DateTime> takenAt = new Dictionary<int, DateTime>();

    // a cylinder: Front Clamping, Clamp (Y32 -> M240 ON, M225 OFF) and Release (Y33 -> M225 ON, M240 OFF)
    static AlarmGeneration.Cylinder cyl;
    static void Move(bool clamp, double seconds) {
        mem["Y32"] = clamp ? 1 : 0; mem["Y33"] = clamp ? 0 : 1;
        Scan();
        Run(Math.Max(0, seconds - 0.1));
        mem["M240"] = clamp ? 1 : 0; mem["M225"] = clamp ? 0 : 1;
        Scan();
    }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        var dir = Path.Combine(Path.GetTempPath(), "HmiRecords");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        AlarmGeneration.SettingsPath = Path.Combine(dir, "AlarmGeneration.cfg");
        HmiFeatures.SettingsPath = Path.Combine(dir, "Features.cfg");
        HmiFeatures.Reload();
        MachineRecords.Normals.Path = Path.Combine(dir, "MovementNormals.cfg");
        MachineRecords.Normals.Reload();
        DashboardConfig.FolderOverride = Path.Combine(dir, "settings");
        DashboardService.AutoReports = false;
        _db = Path.Combine(dir, "Industry4", "database.sqlite");
        var board = DashboardService.Register("FA2010", Path.Combine("Dashboard", "config.json"), Path.Combine("Dashboard", "StateMap.json"),
                                              Path.Combine("Dashboard", "Translation_JSON.csv"), "R27000");
        board.Change(c => { c.DatabasePath = _db; c.TimeZoneName = "UTC"; });
        MachineRecords.Install();
        MachineRecords.Recorded += (m, code, d) => { taken.Add(m + ":" + code); if (!takenAt.ContainsKey(code)) takenAt[code] = now; };

        cyl = new AlarmGeneration.Cylinder { Station = 1, ModuleId = "FA2010", Unit = "Front Clamping" };
        var clamp = new AlarmGeneration.CylinderSide { Side = "B", Name = "Clamp", Code = "M844", ErrorAddress = "M844", Drive = "Y32", OtherDrive = "Y33" };
        clamp.MustOn.Add(new AlarmGeneration.Signal { Address = "M240", Name = "SN83 (X70)" }); clamp.MustOff.Add(new AlarmGeneration.Signal { Address = "M225", Name = "SN82 (X67)" });
        var release = new AlarmGeneration.CylinderSide { Side = "A", Name = "Release", Code = "M845", ErrorAddress = "M845", Drive = "Y33", OtherDrive = "Y32" };
        release.MustOn.Add(new AlarmGeneration.Signal { Address = "M225", Name = "SN82 (X67)" }); release.MustOff.Add(new AlarmGeneration.Signal { Address = "M240", Name = "SN83 (X70)" });
        cyl.Sides.Add(release); cyl.Sides.Add(clamp);
        AlarmGeneration.Register(cyl);
        string clampKey = MachineRecords.KeyOf(cyl, clamp);

        Console.WriteLine("--- an older machine: nothing of the HMI's ---");
        HmiFeatures.Set(HmiFeatures.Industry4Logging, true);
        Check("the HMI's cylinder alarms off (FA2010: the PLC raises them), the option off: its cylinder not even read",
              !AlarmGeneration.EnabledFor("FA2010") && !HmiFeatures.RecordsOn && !AlarmGeneration.Addresses().Any(a => a.Value == "Y32"));
        mem["M225"] = 1; Scan(); Move(true, 1.0); Move(false, 1.0);
        Check("  its movements not timed, nothing taken", taken.Count == 0 && MachineRecords.Normals.Learning(clampKey) == 0 && MachineRecords.Normals.Of(clampKey) == null);

        Console.WriteLine("--- a new machine: the HMI times its cylinders for its records ---");
        HmiFeatures.Set(HmiFeatures.MachineRecords, true);
        Scan();         // a movement commanded before it was watched is not timed: from the next scan
        Check("on (HMI Settings > Options): its outputs and sensors read, with the HMI's alarms still off",
              HmiFeatures.RecordsOn && MachineRecords.On("FA2010") && AlarmGeneration.Addresses().Any(a => a.Value == "Y32") &&
              AlarmGeneration.Addresses().Any(a => a.Value == "M240"));
        for (int i = 0; i < 19; i++) { Move(true, 1.0 + (i % 3) * 0.1); Move(false, 0.8); }
        Check("its normal learned from its first 20 movements: 19 so far, none yet", MachineRecords.Normals.Learning(clampKey) == 19 &&
              MachineRecords.Normals.Of(clampKey) == null, MachineRecords.Normals.Learning(clampKey).ToString());
        Move(true, 1.0); Move(false, 0.8);
        double? normal = MachineRecords.Normals.Of(clampKey);
        Check("  the 20th: the median of them (1.0 - 1.2 s: 1.1 s), kept in MovementNormals.cfg",
              normal.HasValue && Math.Abs(normal.Value - 1.1) < 0.051 && File.ReadAllText(MachineRecords.Normals.Path).Contains(clampKey + "=1.1"),
              normal.HasValue ? normal.Value.ToString(CultureInfo.InvariantCulture) : "none");
        Check("  nothing raised meanwhile, nothing recorded", !AlarmManager.ActiveAlarms.Any() && !taken.Contains("FA2010:1101") && !taken.Contains("FA2010:1102"));

        Console.WriteLine("--- a slow movement ---");
        Move(true, 1.4); Move(false, 0.8);
        Check("30 % slower than its normal: not slow (the limit: 50 %)", !taken.Contains("FA2010:1101"));
        Move(true, 1.8); Move(false, 0.8);
        Check("70 % slower: recorded", taken.Count(x => x == "FA2010:1101") == 1, string.Join(",", taken));
        Check("  into the machine's log database", MachineRecords.Flush() && Records(1101).Count == 1, MachineRecords.Problem);
        var slowId = Convert.ToInt64(Records(1101)[0][0]);
        Check("  its values: the unit, the movement, its time, its normal, how much slower, how many were slow since the last",
              Details(slowId) == "Unit:Front Clamping | Movement:Clamp | Time (s):1.8 | Normal (s):1.1 | Slower (%):64 | Slow since the last record:1",
              Details(slowId));
        Check("  its time: the panel's (the scan's), in UTC as the machine's records", Convert.ToString(Records(1101)[0][1]) ==
              TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(takenAt[1101], DateTimeKind.Unspecified), TimeZoneInfo.Local).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
              Convert.ToString(Records(1101)[0][1]));
        Move(true, 1.8); Move(false, 0.8); Move(true, 1.9); Move(false, 0.8);
        Check("slow again within 10 minutes: not recorded again", taken.Count(x => x == "FA2010:1101") == 1);
        now = now.AddMinutes(10);
        Move(true, 1.8); Move(false, 0.8);
        MachineRecords.Flush();
        Check("  10 minutes on: recorded, with the 3 slow ones since the last", taken.Count(x => x == "FA2010:1101") == 2 &&
              Details(Convert.ToInt64(Records(1101)[1][0])).EndsWith("Slow since the last record:3"), Details(Convert.ToInt64(Records(1101)[1][0])));

        Console.WriteLine("--- not confirmed ---");
        mem["Y32"] = 1; mem["Y33"] = 0; Scan(); mem["M225"] = 0; Run(5.1);
        Check("its sensor not ON within the delay (5 s): recorded - which signal failed - and nothing raised (the HMI's alarms off)",
              taken.Count(x => x == "FA2010:1102") == 1 && !AlarmManager.ActiveAlarms.Any(), string.Join(",", taken));
        MachineRecords.Flush();
        var nc = Details(Convert.ToInt64(Records(1102)[0][0]));
        Check("  its values: the unit, the movement, the time waited, the failure (its sub-code, in words)",
              nc.StartsWith("Unit:Front Clamping | Movement:Clamp | Waited (s):5") && nc.Contains("Failure:SN83 (X70) did not come ON"), nc);
        mem["M240"] = 1; mem["M225"] = 0; Scan();
        Move(false, 0.8);

        Console.WriteLine("--- the shift's times ---");
        board.Change(c => c.Shifts = new List<DashboardConfig.Shift> {
            new DashboardConfig.Shift { Start = TimeSpan.FromHours(6), End = TimeSpan.FromHours(14) },
            new DashboardConfig.Shift { Start = TimeSpan.FromHours(14), End = TimeSpan.FromHours(22) } });
        var local = new DateTime(2026, 11, 5, 10, 0, 0);
        var utc10 = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local);
        var end = MachineRecords.NextShiftEnd(board.Config, local);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(end, DateTimeKind.Unspecified), TimeZoneInfo.Local);
        Check("the shift's end: the next end of the dashboard's shifts, in its time zone (UTC here)",
              endUtc == (utc10.Hour < 14 ? utc10.Date.AddHours(14) : utc10.Hour < 22 ? utc10.Date.AddHours(22) : utc10.Date.AddDays(1).AddHours(14)),
              end.ToString("yyyy-MM-dd HH:mm") + " local, " + endUtc.ToString("yyyy-MM-dd HH:mm") + " UTC");
        Check("  none: midnight there", MachineRecords.NextShiftEnd(DashboardConfig.FromText("{ \"time_zone\": \"UTC\", \"shifts\": [] }"), local) ==
              TimeZoneInfo.ConvertTimeFromUtc(utc10.Date.AddDays(1), TimeZoneInfo.Local));
        MachineRecords.ResetForTests();
        var shiftEnd = MachineRecords.NextShiftEnd(board.Config, now);
        Move(true, 1.0); Move(false, 0.8); Move(true, 1.2); Move(false, 0.6);
        Check("before the shift's end: none written", !taken.Contains("FA2010:1103"));
        now = shiftEnd.AddSeconds(-0.05); Scan(); Scan();
        MachineRecords.Flush();
        var times = Records(1103);
        Check("at its end: a record a movement - its count, average, longest, normal and where the period started",
              times.Count == 2, times.Count.ToString());
        var td = times.Select(r => Details(Convert.ToInt64(r[0]))).ToList();
        Check("  Clamp: 2 movements, 1.1 s on average, 1.2 s the longest, its normal 1.1; Release: 0.7 s, 0.8 s, its normal 0.8",
              td.Any(d => d.StartsWith("Unit:Front Clamping | Movement:Clamp | Movements:2 | Average (s):1.1 | Longest (s):1.2 | Normal (s):1.1 | From:")) &&
              td.Any(d => d.StartsWith("Unit:Front Clamping | Movement:Release | Movements:2 | Average (s):0.7 | Longest (s):0.8 | Normal (s):0.8 | From:")), string.Join(" // ", td));
        Check("  dated at the shift's end", times.All(r => Convert.ToString(r[1]) == TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(shiftEnd, DateTimeKind.Unspecified),
              TimeZoneInfo.Local).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)), Convert.ToString(times[0][1]));
        Move(true, 1.0); Move(false, 0.8);
        now = now.AddHours(9); Scan(); Scan(); MachineRecords.Flush();
        Check("  the next shift: its own", Records(1103).Count == 4);

        Console.WriteLine("--- the dashboard lists them as the machine's events ---");
        var map = board.Map;
        Check("the state map names them (EN, TR) - not records the PLC can be told to log",
              map.RawDescription(1101) == "Slow cylinder movement" && map.Translate("Slow cylinder movement", "Türkçe") == "Yavaş silindir hareketi" &&
              map.Translate("Unit", "Türkçe") == "Ünite" && !map.Switchable.Any(e => e.Code >= 1100), map.Translate("Slow cylinder movement", "Türkçe"));
        var zone = DashboardTime.Find("UTC");
        var loaded = DashboardData.Load(_db, new DateTime(2026, 11, 1), new DateTime(2026, 11, 10), zone, map, "English");
        var view = DashboardData.Build(loaded, false, (DashboardConfig.Shift)null);
        var ev = view.Events.FirstOrDefault(e => e.Code == 1101);
        Check("  in its events, described, with their values", ev != null && ev.Description == "Slow cylinder movement" &&
              ev.Details.StartsWith("Unit: Front Clamping; Movement: Clamp; Time (s): 1.8") && view.EventCounts.ContainsKey("Cylinder movement times"),
              ev == null ? string.Join(",", view.Events.Select(e => e.Code)) : ev.Details);
        var tr = DashboardData.Build(DashboardData.Load(_db, new DateTime(2026, 11, 1), new DateTime(2026, 11, 10), zone, map, "Türkçe"), false, (DashboardConfig.Shift)null);
        var evTr = tr.Events.FirstOrDefault(e => e.Code == 1101);
        Check("  in Turkish", evTr != null && evTr.Description == "Yavaş silindir hareketi" && evTr.Details.StartsWith("Ünite: Front Clamping; Hareket: Clamp"),
              evTr == null ? "" : evTr.Description + " / " + evTr.Details);

        Console.WriteLine("--- the machine's Industry 4 Logging frame ---");
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        var frame = new Panel { Name = "frame", BackColor = Color.White, Width = 474 };
        var page = new MachineLogSettings("FA2010") { Location = new Point(12, 10) };
        frame.Controls.Add(page);
        page.SizeChanged += (s, e) => frame.Height = page.Bottom + 8;
        frame.Height = page.Bottom + 8;
        var form = new Form { ClientSize = new Size(500, 900), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        form.Controls.Add(frame);
        form.Show(); Pump(300);
        var slowSw = page.RecordSwitch("cylinder_slow");
        var slowLim = page.LimitField("slow_percent");
        Check("a new machine: under its settings, what the HMI takes - a switch row a record (on), a row for its limit (50 %)",
              slowSw != null && slowSw.Visible && slowSw.Checked && slowSw.Width == 450 && page.RecordSwitch("cylinder_not_confirmed").Checked &&
              page.RecordSwitch("cylinder_times").Checked && slowLim != null && slowLim.Visible && slowLim.Value == 50 &&
              slowSw.DescriptionText == "Slow cylinder movement");
        Check("  'Learn the normal times again', how many are learned under it", All(page).OfType<Button>().Any(b => b.Name == "learnNormals" && b.Visible) &&
              page.NormalsText == "Normal times learned: 2.", page.NormalsText);
        Shot(form, "records_frame");
        slowLim.Value = 20; Pump(50);
        Check("the limit changed: kept at once", board.Config.RecordLimit("slow_percent", 50) == 20);
        int before = taken.Count(x => x == "FA2010:1101");
        now = now.AddMinutes(11);
        Move(true, 1.5); Move(false, 0.8);
        Check("  36 % slower is slow now", taken.Count(x => x == "FA2010:1101") == before + 1);
        slowSw.Checked = false; Pump(50);
        now = now.AddMinutes(11);
        Move(true, 2.4); Move(false, 0.8);
        Check("switched off: kept at once, not recorded any more", !board.Config.RecordOn("cylinder_slow") && taken.Count(x => x == "FA2010:1101") == before + 1);
        slowSw.Checked = true; slowLim.Value = 50; Pump(50);
        All(page).OfType<Button>().First(b => b.Name == "learnNormals").PerformClick(); Pump(50);
        Check("'Learn the normal times again': the machine's forgotten, learned from the next movements",
              MachineRecords.Normals.Of(clampKey) == null && MachineRecords.Normals.CountOf("FA2010") == 0 &&
              page.NormalsText.StartsWith("No normal time learned yet"), page.NormalsText);
        HmiFeatures.Set(HmiFeatures.MachineRecords, false); Pump(100);
        Check("an older machine: none of it shown", !slowSw.Visible && !slowLim.Visible && !All(page).OfType<Button>().Any(b => b.Name == "learnNormals" && b.Visible));
        HmiFeatures.Set(HmiFeatures.MachineRecords, true); Pump(100);
        form.Close();

        MachineRecords.Stop();
        HmiFeatures.Set(HmiFeatures.Industry4Logging, false);
        Console.WriteLine(fail == 0 ? "HMI RECORDS ALL PASS" : "HMI RECORDS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
