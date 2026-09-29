using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;

// A RESTART, on the real FA2010 program (29 September): the new instance is
// started only once every station is closed, through the shell (it inherits
// no handle - no MX Component connection - of the old one); and the pages
// searched while someone is signed in report no axis "numbered on two rows".
class RO {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static Type Ctl = typeof(ActUtlTypeLib.ActUtlType);
    static FieldInfo CF(string n) { return Ctl.GetField(n, BindingFlags.Public | BindingFlags.Static); }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "ro_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);

        // Station 1 on MX Component - the scripted stand-in when there is one,
        // so the connection itself can be seen closed; the others simulated.
        bool scripted = CF("Registered") != null;
        if (scripted) {
            Ctl.GetMethod("Reset").Invoke(null, null);
            CF("Registered").SetValue(null, true);
            typeof(Kaban.Drivers.MitsubishiActUtl.MxDiagnostics).GetField("_scanned", Any).SetValue(null, true);
            PlcManager.Instance.RegisterDriver(1, new Kaban.Drivers.MitsubishiActUtl.MitsubishiActUtl(1));
        } else PlcManager.Instance.RegisterDriver(1, new Kaban.Drivers.Simulation.SimulationDriver(1));
        foreach (int st in new[] { 2, 3, 4 }) PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));

        Console.WriteLine("--- pages searched while someone is signed in ---");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        AlarmHelpLinks.DiscoverAll();
        var axisConflicts = AlarmHelpLinks.Conflicts.Where(c => c.Contains("numbered on two rows")).ToList();
        Check("no axis 'numbered on two rows' (the same row seen on its tab and on its own)", axisConflicts.Count == 0,
              axisConflicts.Count + " " + axisConflicts.FirstOrDefault());
        Check("  every FA2010 and H800 axis still known by its number",
              Enumerable.Range(1, 16).All(n => AlarmHelpLinks.FindAxis(1, n) != null) && Enumerable.Range(1, 6).All(n => AlarmHelpLinks.FindAxis(4, n) != null));
        var motion = typeof(SimulationMotion).GetField("_axes", Any).GetValue(null) as System.Collections.IList;
        var positions = motion.Cast<object>().Select(a => a.GetType().GetField("Station", Any).GetValue(a) + "|" + a.GetType().GetField("Position", Any).GetValue(a)).ToList();
        Check("  each simulated axis moves once per step (not twice)", positions.Count == positions.Distinct().Count(),
              positions.Count + " / " + positions.Distinct().Count());

        Console.WriteLine("--- Restart: the new instance comes last ---");
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show();
        Pump(400);
        if (scripted) Check("station 1 open before the restart", PlcManager.Instance.GetDriverForMonitoring(1).IsConnected && (int)CF("Live").GetValue(null) == 1);

        var started = new List<ProcessStartInfo>();
        int liveAtStart = -1;
        string logAtStart = "";
        var seam = typeof(MainForm).GetField("StartProcess", Any);
        Check("the start goes through one place (MainForm.StartProcess)", seam != null);
        seam.SetValue(null, (Action<ProcessStartInfo>)(psi => {
            started.Add(psi);
            liveAtStart = scripted ? (int)CF("Live").GetValue(null) : 0;
            logAtStart = File.ReadAllText(AuditLogger.TodayFile);
        }));
        form.RestartApplication();
        Pump(200);
        Check("one new instance started", started.Count == 1, started.Count.ToString());
        var p = started.FirstOrDefault() ?? new ProcessStartInfo();
        Check("  through the shell: it inherits no handle of the old one", p.UseShellExecute);
        Check("  told to wait for this process, and who is signed in",
              p.Arguments.StartsWith("--await-exit " + Process.GetCurrentProcess().Id) && p.Arguments.Contains("--handoff"), p.Arguments);
        Check("  started AFTER every station was closed ('all 4 station(s) closed' already in the log)",
              logAtStart.Contains("[PLC DIAG] all 4 station(s) closed."));
        if (scripted)
            Check("  with no MX Component connection left open at that moment", liveAtStart == 0, liveAtStart.ToString());
        string log = File.ReadAllText(AuditLogger.TodayFile);
        int iClosed = log.LastIndexOf("all 4 station(s) closed"), iStarted = log.LastIndexOf("[RESTART] new instance started");
        Check("the log: stations closed, then the new instance started", iClosed >= 0 && iStarted > iClosed);
        Check("the old window is gone", form.IsDisposed);

        if (scripted) Ctl.GetMethod("Reset").Invoke(null, null);
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nRESTART ORDER ALL PASS" : "\nRESTART ORDER " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
