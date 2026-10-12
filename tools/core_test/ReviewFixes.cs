using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;

// One check per fix from the September 2026 code review (Kaban.Core side).
// Each section names the bug it guards against.
class RF {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }

    static void Main() {
        string tmp = Path.Combine(Path.GetTempPath(), "kaban_review_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);

        Console.WriteLine("--- audit log: the viewer and export read the file the logger writes ---");
        AuditLogger.Log("review-fix marker line");
        Check("TodayFile is the HmiAudit_ file", Path.GetFileName(AuditLogger.TodayFile).StartsWith("HmiAudit_"),
              AuditLogger.TodayFile);
        var tail = AuditLogger.Tail(20);
        Check("Tail() finds today's lines (it read a file nobody wrote)", tail.Any(l => l.Contains("review-fix marker line")),
              tail.Length + " lines");
        using (var hold = new FileStream(AuditLogger.TodayFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            AuditLogger.Log("written while the viewer reads");
        }
        Check("a line logged WHILE the file is open for reading is not lost",
              AuditLogger.Tail(5).Any(l => l.Contains("written while the viewer reads")));

        Console.WriteLine("--- connection monitor: UTC, so the autumn clock change cannot keep a dead station 'live' ---");
        ConnectionMonitor.GraceTime = TimeSpan.FromMilliseconds(300);
        ConnectionMonitor.ReportRead(41, true);
        var st = ConnectionMonitor.Get(41);
        Check("timestamps are UTC", st.LastGoodRead.Kind == DateTimeKind.Utc, st.LastGoodRead.Kind.ToString());
        Check("live straight after a good read", st.IsLive);
        Thread.Sleep(400);
        Check("down after the grace time", !st.IsLive && ConnectionMonitor.IsKnownDown(41));
        ConnectionMonitor.ReportRead(41, false, "read failed");       // the loss is announced here
        int events = 0;
        EventHandler h = (s, e) => Interlocked.Increment(ref events);
        ConnectionMonitor.ConnectionChanged += h;
        var threads = Enumerable.Range(0, 8).Select(i => new Thread(() => { for (int k = 0; k < 200; k++) ConnectionMonitor.ReportRead(41, true); })).ToList();
        threads.ForEach(t => t.Start()); threads.ForEach(t => t.Join());
        ConnectionMonitor.ConnectionChanged -= h;
        Check("many threads reporting at once announce the recovery ONCE", events == 1, events + " events");

        Console.WriteLine("--- bounded driver: every call's wait handle is released ---");
        var slow = new SlowDriver();
        var bd = new BoundedDriver(slow);
        bd.Connect(); Thread.Sleep(50);
        int v;
        for (int i = 0; i < 50; i++) bd.TryRead("D0", out v);
        Check("50 reads still work through the worker", bd.TryRead("D0", out v) && v == 7, v.ToString());
        slow.DelayMs = 1500;                                // a read that outlives the 1 s wait
        bool ok = bd.TryRead("D0", out v);
        Check("a stuck call gives up after the bounded wait", !ok);
        Thread.Sleep(1700);                                 // the worker finishes later, signals a handle nobody waits on
        slow.DelayMs = 0; bd.Connect(); Thread.Sleep(50);
        Check("and the driver is usable afterwards (the late Set did not throw on a disposed handle)",
              bd.TryRead("D0", out v) && v == 7);

        Console.WriteLine("--- alarms: saved to each machine's OWN file, found by CODE ---");
        string fa = Path.Combine(tmp, "FA_Alarms.json"), h500 = Path.Combine(tmp, "H500_Alarms.json");
        File.WriteAllText(fa, "[{\"PlcAddress\":\"M801\",\"TagCode\":\"Robot_X_Error\",\"Severity\":\"Fault\",\"PlcNumber\":1," +
                              "\"Translations\":{\"EN\":\"Robot X Error\",\"TR\":\"Eksen1 Arıza\"},\"Comment\":\"bit 0\"}]");
        File.WriteAllText(h500, "[{\"PlcAddress\":\"M801\",\"TagCode\":\"Head_Fault\",\"Severity\":\"Fault\",\"PlcNumber\":1," +
                                "\"Translations\":{\"EN\":\"Head fault\"},\"Comment\":\"code 1\"}]");
        AlarmDefinitionLoader.Load(fa, true, "FA2010", st2 => st2);
        AlarmDefinitionLoader.Load(h500, false, "H500", st2 => st2 == 1 ? 3 : st2);
        var faDef = AlarmManager.FindByCode("FA2010.Robot_X_Error");
        var hDef = AlarmManager.FindByCode("H500.Head_Fault");
        Check("both machines' M801 alarms are registered", faDef != null && hDef != null);
        Check("  H500's on its own station (3)", hDef != null && hDef.PlcNumber == 3, hDef == null ? "" : hDef.PlcNumber.ToString());
        hDef.Severity = AlarmSeverity.Warning;
        string faBefore = File.ReadAllText(fa);
        string saved = AlarmDefinitionLoader.SaveLoadedEntries("H500");
        Check("save writes H500's file (the 4-arg Load never recorded a path: 'Nothing to save')",
              saved.Contains("H500_Alarms.json"), saved);
        Check("  and ONLY that file", !saved.Contains("FA_Alarms.json") && File.ReadAllText(fa) == faBefore);
        string hJson = File.ReadAllText(h500);
        Check("  with H500's change", hJson.Contains("Warning"), hJson.Length + " chars");
        Check("  keeping the engineer's Comment", hJson.Contains("code 1"));
        AlarmDefinitionLoader.SaveLoadedEntries();
        Check("saving all keeps FA2010's alarm Fault (not H500's Warning by address)",
              File.ReadAllText(fa).Contains("Fault") && !File.ReadAllText(fa).Contains("Warning"));

        Console.WriteLine("--- alarms: each station's alarms from that station's data ---");
        AlarmManager.ActiveAlarms.Clear();
        Func<string, int> station1 = a => a == "M801" ? 1 : 0;       // FA2010 has the fault
        Func<string, int> station3 = a => 0;                          // H500 does not
        int changes = 0;
        EventHandler ch = (s, e) => changes++;
        AlarmManager.AlarmsChanged += ch;
        AlarmManager.Evaluate(1, station1);
        AlarmManager.Evaluate(3, station3);
        AlarmManager.AlarmsChanged -= ch;
        var codes = AlarmManager.ActiveAlarms.Select(a => a.AlarmCode).ToList();
        Check("FA2010's alarm is raised", codes.Contains("FA2010.Robot_X_Error"), string.Join(",", codes));
        Check("H500's is NOT (it shares M801, but on station 3)", !codes.Contains("H500.Head_Fault"));
        Check("one change notification per scan, not per alarm", changes == 1, changes.ToString());

        Console.WriteLine("--- users ---");
        foreach (var u in UserStore.GetAllUsers().ToList())
            if (u.Username.StartsWith("rv_")) UserStore.RemoveUser(u.Username);
        bool threw = false;
        try { UserStore.AddOrUpdateUser("bad;name", "pw", SecurityLevel.Operator); } catch (ArgumentException) { threw = true; }
        Check("a ';' in a user name is refused (it broke Users.dat)", threw);
        var admins = UserStore.GetAllUsers().Where(u => u.Level == SecurityLevel.Admin).Select(u => u.Username).ToList();
        if (admins.Count == 1)
        {
            Check("the last Admin cannot be demoted", !UserStore.SetLevel(admins[0], SecurityLevel.Operator));
            Check("the last Admin cannot be removed", !UserStore.RemoveUser(admins[0]));
        }
        else Check("(setup) exactly one admin expected for this check", false, admins.Count + " admins");
        UserStore.AddOrUpdateUser("rv_op", "secret", SecurityLevel.Operator);
        var acc = UserStore.GetAllUsers().First(u => u.Username == "rv_op");
        acc.Salt = "%%%not base64%%%";
        UserAccount ignored;
        bool loginOk = true;
        try { loginOk = UserStore.TryValidate("rv_op", "secret", out ignored); }
        catch (Exception ex) { Check("a damaged account does not throw out of the login", false, ex.GetType().Name); }
        Check("a damaged account is a failed login, not a crash", !loginOk);
        UserStore.RemoveUser("rv_op");

        Console.WriteLine("--- settings files are replaced whole ---");
        string f = Path.Combine(tmp, "x.cfg");
        SafeFile.WriteAllText(f, "first");
        SafeFile.WriteAllText(f, "second");
        Check("the new content replaces the old", File.ReadAllText(f) == "second");
        Check("no temporary file left behind", !File.Exists(f + ".tmp"));

        try { Directory.Delete(tmp, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nREVIEW FIXES ALL PASS" : "\nREVIEW FIXES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    class SlowDriver : Kaban.Core.Interfaces.IPlcDriver {
        public volatile int DelayMs;
        bool _c;
        public bool IsConnected { get { return _c; } }
        public bool IsHealthy { get { return _c; } }
        public bool Connect() { _c = true; return true; }
        public void Disconnect() { _c = false; }
        public int Read(string a) { int v; TryRead(a, out v); return v; }
        public bool TryRead(string a, out int v) { if (DelayMs > 0) Thread.Sleep(DelayMs); v = 7; return true; }
        public int[] ReadMulti(string[] a) { return new int[a.Length]; }
        public bool TryReadMulti(string[] a, out int[] v) { v = new int[a.Length]; return true; }
        public int Write(string a, int v) { return 0; }
    }
}
