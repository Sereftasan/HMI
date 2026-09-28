using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.UI.Settings;

// THE AUDIT LOG READ BACK (AuditLogReader), the log viewer's filters, the Log
// Settings switches and the PLC Diagnostics page's pieces.
class L {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    class FakeMx : IPlcDriver {
        public int LogicalStation { get; set; }
        public bool IsConnected { get { return true; } }
        public bool IsHealthy { get { return true; } }
        public bool Connect() { return true; }
        public void Disconnect() { }
        public int Read(string a) { return 0; }
        public int[] ReadMulti(string[] a) { return new int[a.Length]; }
        public int Write(string a, int v) { return 0; }
        public bool TryRead(string a, out int v) { v = 0; return true; }
        public bool TryReadMulti(string[] a, out int[] v) { v = new int[a.Length]; return true; }
    }

    static string Line(DateTime t, string user, string text) {
        return "[" + t.ToString("yyyy-MM-dd HH:mm:ss") + "] - User '" + user + "' " + text + Environment.NewLine;
    }

    [STAThread] static void Main(){
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        string dir = Path.Combine(Path.GetTempPath(), "log_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        AuditLogReader.ClearCache();

        var today = DateTime.Today;
        var y = today.AddDays(-1);
        var old = today.AddDays(-3);
        File.WriteAllText(AuditLogger.FileFor(old), Line(old.AddHours(9), "admin", "logged in (level: Admin)."));
        File.WriteAllText(AuditLogger.FileFor(y),
            Line(y.AddHours(8), "op", "logged in (level: Operator).") +
            Line(y.AddHours(10), "op", "wrote 120 to D210 (PLC 1).") +
            Line(y.AddHours(11), "op", "station 1 lost: read failed") +
            Line(y.AddHours(11).AddMinutes(1), "op", "station 1 reconnected."));
        var now = DateTime.Now;
        var t0 = today.AddSeconds(Math.Max(1, (now - today).TotalSeconds - 600));   // ten minutes ago, still today
        File.WriteAllText(AuditLogger.FileFor(today),
            Line(t0, "admin", "[PLC DIAG] scanning logical stations 0..3 (configured station 1 failed):") +
            "    station 1: OPEN OK" + Environment.NewLine +
            "  --> USE LOGICAL STATION: 1" + Environment.NewLine +
            Line(t0.AddSeconds(1), "admin", "station 2 lost: not connected") +
            Line(t0.AddSeconds(2), "admin", "station 2 lost: not connected") +
            Line(t0.AddSeconds(3), "maint", "wrote 5 to D210 (PLC 2).") +
            Line(t0.AddSeconds(4), "maint", "FAILED to load alarm definitions from 'x.json': bad") +
            Line(t0.AddSeconds(5), "admin", "saved PLC connection settings (2 controllers)."));

        Console.WriteLine("--- reading ---");
        var all = AuditLogReader.Read(old, DateTime.Now);
        Check("every entry of every day in the range", all.Count == 1 + 4 + 6, all.Count.ToString());
        Check("oldest first", all.First().Time == old.AddHours(9) && all.Last().User == "admin");
        var yd = AuditLogReader.Read(y, y.AddDays(1).AddSeconds(-1));
        Check("yesterday alone", yd.Count == 4, yd.Count.ToString());
        Check("a time range inside a day", AuditLogReader.Read(y.AddHours(9), y.AddHours(11)).Count == 2);
        var scan = all.First(e => e.Text.StartsWith("[PLC DIAG] scanning"));
        Check("a report's further lines belong to its entry", scan.Text.Contains("station 1: OPEN OK") && scan.Text.Contains("USE LOGICAL STATION"), scan.Text);
        Check("the oldest day kept", AuditLogReader.OldestDay() == old);

        Console.WriteLine("--- kinds ---");
        Func<string, AuditKind> k = AuditLogReader.Classify;
        Check("sign-in", k("logged in (level: Admin).") == AuditKind.SignIn && k("FAILED login attempt for user 'x'") == AuditKind.SignIn);
        Check("connection", k("station 3 lost: read failed") == AuditKind.Connection && k("station 3 reconnected.") == AuditKind.Connection &&
                            k("[PLC DIAG] Mitsubishi station 1 connect failed") == AuditKind.Connection);
        Check("PLC writes", k("wrote 12 to D210 (PLC 1).") == AuditKind.PlcWrite && k("set bit M102 (PLC 1) to 1") == AuditKind.PlcWrite &&
                            k("alarm reset SET from the alarm menu") == AuditKind.PlcWrite);
        Check("problems", k("UNHANDLED UI EXCEPTION: x") == AuditKind.Problem && k("could not save the display settings: x") == AuditKind.Problem);
        Check("settings", k("saved PLC connection settings (2)") == AuditKind.Settings && k("switched audit logging OFF.") == AuditKind.Settings);
        Check("the rest", k("opened the HMI manual.") == AuditKind.Other);

        Console.WriteLine("--- the controller an entry is about ---");
        Func<string, int?> st = AuditLogReader.StationOf;
        Check("a write names it: (PLC 2) / (PLC 2, control tag ...)", st("wrote 5 to D210 (PLC 2).") == 2 && st("set bit M10 (PLC 3, control tag 'M10') to 1.") == 3);
        Check("a connection entry by its station", st("station 4 lost: read failed") == 4 && st("[PLC DIAG] station 1 scan OK (169 tags).") == 1 &&
              st("attempted to connect to unregistered PLC station 5.") == 5);
        Check("a controller's caption, in any language: (station 2) / (istasyon 2)", st("monitor wrote 1 to M5 on FA2010 PLC2  (station 2).") == 2 &&
              st("monitor wrote 1 to M5 on FA2010 PLC2  (istasyon 2).") == 2);
        Check("not MX Component's logical station numbers", st("[PLC DIAG] Mitsubishi station 7 connect failed") == null &&
              st("test of logical station 3: opens: 3") == null);
        Check("none for an entry about no controller", st("logged in (level: Admin).") == null);

        Console.WriteLine("--- search ---");
        var w = all.First(e => e.Text.StartsWith("wrote 5"));
        Check("every word, any case, user included", AuditLogReader.Matches(w, "d210 MAINT") && !AuditLogReader.Matches(w, "d210 admin"));
        Check("an empty search passes all", AuditLogReader.Matches(w, "  "));

        Console.WriteLine("--- following the file ---");
        var before = AuditLogReader.Read(today, DateTime.Now.AddMinutes(1));
        File.AppendAllText(AuditLogger.FileFor(today), Line(DateTime.Now, "admin", "opened the HMI manual.") + "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] - User 'admin' half a li");
        var after = AuditLogReader.Read(today, DateTime.Now.AddMinutes(1));
        Check("a new entry is read", after.Count == before.Count + 1, before.Count + " -> " + after.Count);
        Check("  the ones already read are the same objects (read on, not again)", ReferenceEquals(before[0], after[0]));
        Check("  a line still being written is not", !after.Any(e => e.Text.Contains("half a li")));
        File.AppendAllText(AuditLogger.FileFor(today), "ne." + Environment.NewLine);
        var done = AuditLogReader.Read(today, DateTime.Now.AddMinutes(1));
        Check("  and is once it is finished", done.Any(e => e.Text == "half a line."), done.Last().Text);

        Console.WriteLine("--- the viewer ---");
        var v = new LogViewerControl(null, LogViewerControl.Period.Today);
        var host = new Form { Width = 1200, Height = 800 };
        host.Controls.Add(v);
        host.Show(); Application.DoEvents();
        int todayCount = done.Count;
        Check("today's entries listed", v.RowCount == todayCount, v.RowCount + " / " + todayCount);
        Check("newest first", v.CellText(0, 4) == "half a line.", v.CellText(0, 4));
        Check("a report shows its first line in the table", Enumerable.Range(0, v.RowCount).Any(r => v.CellText(r, 4).StartsWith("[PLC DIAG] scanning") && v.CellText(r, 4).EndsWith("…")));
        v.SearchText = "d210";
        Check("search narrows", v.RowCount == 1 && v.CellText(0, 1) == "maint", v.RowCount.ToString());
        v.SelectRow(0);
        Check("  the selected entry in full below", v.DetailText.Contains("wrote 5 to D210"), v.DetailText);
        v.SearchText = "";
        v.SetKind(AuditKind.Connection);
        Check("kind narrows (connection: the scan and two losses)", v.RowCount == 3, v.RowCount.ToString());
        v.SetKind(null);
        v.SetUser("maint");
        Check("user narrows", v.RowCount == 2, v.RowCount.ToString());
        v.SetUser(null);
        Check("the controllers named in the period are offered (PLC 2)", v.PlcChoices.Any(c => c.StartsWith("PLC 2")), string.Join(" | ", v.PlcChoices));
        v.SetStation(2);
        Check("controller narrows: PLC 2's two losses and its write", v.RowCount == 3 &&
              Enumerable.Range(0, v.RowCount).All(r => v.CellText(r, 2) == "2"), v.RowCount + " rows");
        v.SetStation(null);
        v.SetPeriod(LogViewerControl.Period.Yesterday);
        Check("yesterday", v.RowCount == 4, v.RowCount.ToString());
        v.SetPeriod(LogViewerControl.Period.Last7Days);
        Check("the last 7 days", v.RowCount == 1 + 4 + todayCount, v.RowCount.ToString());
        v.SetCustom(y.AddHours(9), y.AddHours(11));
        Check("From - to", v.CurrentPeriod == LogViewerControl.Period.Custom && v.RowCount == 2, v.RowCount.ToString());
        Check("the status line counts", v.StatusText.Contains("2"), v.StatusText);
        var some = AuditLogReader.Read(y, y.AddDays(1));
        string csv = LogViewerControl.ExportText(some, true);
        Check("CSV export: a heading and a row each", csv.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).Length == 5 && csv.StartsWith("Time;User;PLC;Kind;Entry"));
        string txt = LogViewerControl.ExportText(some, false);
        Check("log export: the log's own lines", txt.StartsWith("[" + y.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss") + "] - User 'op' logged in"));
        host.Close();

        Console.WriteLine("--- Log Settings: the switches are an Admin's ---");
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        var ls = new LogSettingsControl();
        var chk = (CheckBox)typeof(LogSettingsControl).GetField("_chkEnabled", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ls);
        var num = (NumericUpDown)typeof(LogSettingsControl).GetField("_numRetain", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ls);
        var off = (Label)typeof(LogSettingsControl).GetField("_off", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ls);
        Check("Maintenance may read, not switch", !chk.Enabled && !num.Enabled);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        Check("an Admin may", chk.Enabled && num.Enabled);
        AuditLogger.Enabled = false;
        var ls2 = new LogSettingsControl();
        var off2 = (Label)typeof(LogSettingsControl).GetField("_off", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ls2);
        var host2 = new Form(); host2.Controls.Add(ls2); host2.Show(); Application.DoEvents();
        Check("logging off: the page says so", off2.Visible);
        AuditLogger.Enabled = true;
        host2.Close();
        ls.Dispose();

        Console.WriteLine("--- PLC Diagnostics ---");
        var drops = ControllerStatus.DropsToday();    // before station 2 below logs a loss of its own
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(1);
        sim.Connect();
        PlcManager.Instance.RegisterDriver(1, sim);
        PlcManager.Instance.RegisterStation(1, "Main", SecurityLevel.Operator);
        ConnectionMonitor.ReportRead(1, true);
        PlcManager.Instance.RegisterDriver(2, new FakeMx { LogicalStation = 3 });
        PlcManager.Instance.RegisterStation(2, "Aux", SecurityLevel.Operator);
        ConnectionMonitor.ReportRead(2, false, "not connected");
        Check("drops today counted from the log", drops.ContainsKey(2) && drops[2] == 2 && !drops.ContainsKey(1),
              string.Join(",", drops.Select(p => p.Key + "=" + p.Value)));
        var rows = ControllerStatus.BuildRows(drops);
        var r1 = rows.FirstOrDefault(r => r.Station == 1);
        var r2 = rows.FirstOrDefault(r => r.Station == 2);
        Check("a row per controller", r1 != null && r2 != null, rows.Count.ToString());
        Check("  station 1 live", r1 != null && r1.Live && r1.State.Contains("Connected"), r1 == null ? "" : r1.State);
        Check("  station 2 not, with its drops", r2 != null && !r2.Live && r2.Drops == 2, r2 == null ? "" : r2.State + " / " + r2.Drops);
        Check("  the running driver's logical station shown", r2 != null && r2.Address.EndsWith("3"), r2 == null ? "" : r2.Address);
        Check("silence in words", ControllerStatus.Span(TimeSpan.FromSeconds(12)) == "12 s" &&
                                  ControllerStatus.Span(TimeSpan.FromMinutes(4)) == "4 min" &&
                                  ControllerStatus.Span(TimeSpan.FromMinutes(125)) == "2 h 5 min");
        var used = MxToolsView.StationsInUse();
        Check("the MX tools know which logical station the machine uses", used.ContainsKey(3) && used[3] == 2,
              string.Join(",", used.Select(p => p.Key + "->" + p.Value)));

        var mx = new MxToolsView();
        var host3 = new Form { Width = 1000, Height = 600 }; host3.Controls.Add(mx); host3.Show(); Application.DoEvents();
        var start = typeof(MxToolsView).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        start.Invoke(mx, new object[] { new[] { 3, 4 }, true });
        var until = DateTime.Now.AddSeconds(20);
        while (mx.Running && DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
        for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
        Check("the scan runs in the background and ends", !mx.Running);
        Check("  the machine's own station is reported, not opened", mx.Output.Contains("station 3: in use by PLC 2"), mx.Output);
        Check("  another is tried (no MX Component here: its code)", mx.Output.Contains("station 4: 0x80040154"), mx.Output);
        host3.Close();

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail==0?"\nLOG AND DIAGNOSTICS ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}
