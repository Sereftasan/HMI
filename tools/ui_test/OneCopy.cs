using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Kaban.Core.Helpers;
using Kaban.UI.Helpers;

// 5 November: "It is better to not left two copies of the program run at the same time anyhow I think, it might cause
// other problems as well". Every machine's exe holds one mutex: a second start brings the running one to the front, says
// why and ends; a restart's new instance waits for it (the old one lets it go first - nav_test RestartOrder).
class OC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
    static void Set(string n, object v) { typeof(SingleInstance).GetField(n, Any).SetValue(null, v); }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "onecopy_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        var waits = new List<int>();
        bool free = true;
        string told = null; IntPtr front = IntPtr.Zero;
        Tuple<string, DateTime?, IntPtr> other = null;
        Set("TryHold", (Func<int, bool>)(ms => { waits.Add(ms); return free; }));
        Set("FindOther", (Func<Tuple<string, DateTime?, IntPtr>>)(() => other));
        Set("BringToFront", (Action<IntPtr>)(h => front = h));
        Set("Tell", (Action<string, string>)((t, c) => told = c + " | " + t));

        Console.WriteLine("--- no other copy ---");
        Check("this copy runs", SingleInstance.Take(new string[0]) && told == null);
        Check("  not waiting for one", waits.Count == 1 && waits[0] == 0, string.Join(",", waits));

        Console.WriteLine("--- started again while one runs ---");
        free = false; waits.Clear();
        other = Tuple.Create("Kaban.Machines.FA2010", (DateTime?)new DateTime(2026, 11, 5, 17, 10, 0), new IntPtr(4242));
        bool runs = SingleInstance.Take(new string[0]);
        Check("this one ends", !runs);
        Check("  the running one brought to the front", front == new IntPtr(4242));
        Check("  said: which, since when, and why only one",
              told != null && told.Contains("already running (Kaban.Machines.FA2010, opened at 17:10)") && told.Contains("Only one copy of the HMI runs at a time"), told);
        string log = File.ReadAllText(AuditLogger.TodayFile);
        Check("  in the log", log.Contains("[ONE COPY] the HMI was started while another copy runs (Kaban.Machines.FA2010)"));

        Console.WriteLine("--- the other still starting or closing (no window yet) ---");
        told = null; front = IntPtr.Zero;
        other = Tuple.Create("Kaban.Machines.H500", (DateTime?)null, IntPtr.Zero);
        Check("this one ends, told to wait a few seconds", !SingleInstance.Take(new string[0]) && told.Contains("still closing") && front == IntPtr.Zero, told);

        Console.WriteLine("--- in Turkish ---");
        LanguageManager.ChangeLanguage("TR");
        told = null;
        other = Tuple.Create("Kaban.Machines.FA2010", (DateTime?)new DateTime(2026, 11, 5, 9, 5, 0), new IntPtr(7));
        SingleInstance.Take(new string[0]);
        Check("the message in Turkish", told != null && told.Contains("zaten çalışıyor") && told.Contains("09:05") && told.Contains("yalnız bir kopyası"), told);
        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine("--- a restart's new instance ---");
        waits.Clear(); told = null;
        free = true;
        Check("waits for the old one to let go (up to 15 s), then runs",
              SingleInstance.Take(new[] { "--await-exit", "1234" }) && waits.Count == 1 && waits[0] == 15000 && told == null, string.Join(",", waits));
        // 6 November: "We have to make sure the application get closed before opening again" - never beside another copy
        free = false; waits.Clear();
        other = Tuple.Create("Kaban.Machines.FA2010", (DateTime?)new DateTime(2026, 11, 6, 9, 0, 0), new IntPtr(99));
        Check("  another copy still holds it after the wait: this one ends too (was: ran anyway)",
              !SingleInstance.Take(new[] { "--await-exit", "1234" }) && told != null);

        Console.WriteLine("--- a start by hand while a copy is closing (no window any more) ---");
        told = null; waits.Clear();
        other = Tuple.Create("Kaban.Machines.FA2010", (DateTime?)null, IntPtr.Zero);
        int tries = 0;
        Set("TryHold", (Func<int, bool>)(ms => { waits.Add(ms); return ++tries > 1; }));
        Check("waits for it to close (up to 10 s), then runs", SingleInstance.Take(new string[0]) && told == null &&
              waits.Count == 2 && waits[0] == 0 && waits[1] == 10000, string.Join(",", waits));

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "ONE COPY ALL PASS" : "ONE COPY " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
