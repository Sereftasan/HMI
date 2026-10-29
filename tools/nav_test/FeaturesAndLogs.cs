using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Dashboard;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 30 October: "We need to make the Industry 4 service and touchscreen mode optional, and we need to add some
// buttons to active and passive them on HMI setting" - and: "'R27000' logging device is configured in a JSON
// file, different machines have different device addresses in the PLC so we should create a setting inside
// 'Machine Parameters' for each machine as well btw (visible when HMI industry 4 logging activated). We can change
// the alarms setting page name to 'Alarms and Logs' and put the setting on the page I think." - on the real
// FA2010 program.
class FL {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static object Get(object o, string n) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, Any); if (f != null) return f.GetValue(o); } return null; }
    static IEnumerable<Control> All(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; } }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "fl_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        ViewPreferences.Reload();
        HmiFeatures.SettingsPath = Path.Combine(dir, "Features.cfg");
        HmiFeatures.Reload();
        DashboardConfig.FolderOverride = Path.Combine(dir, "settings");
        DashboardService.AutoReports = false;
        NotesStore.Folder = Path.Combine(dir, "Notes");
        string db = Path.Combine(dir, "Industry4", "database.sqlite");
        HmiLogStore.PathOverride = () => db;
        MachineLogger.ServiceRunning = () => false;
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 }) PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        var bounds = new Rectangle(0, 0, 1270, 900);
        form.StartPosition = FormStartPosition.Manual; form.Bounds = bounds;
        form.Load += (s, e) => { form.WindowState = FormWindowState.Normal; form.Bounds = bounds; SecurityManager.Login("admin", SecurityLevel.Admin); };
        form.Show();
        Pump(600);

        Console.WriteLine("--- Industry 4 logging in the HMI: off at first ---");
        Check("off: no machine logger, the HMI's log in its files (the Windows service logs, as before 29 October)",
              !HmiFeatures.LoggingOn && MachineLoggers.All.Count == 0 && AuditLogReader.Store == null && !File.Exists(db));
        var features = new FeaturesControl();
        var host = new Form { ClientSize = new Size(900, 700), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        features.Dock = DockStyle.Fill; host.Controls.Add(features); host.Show(); Pump(100);
        features.SwitchOf(HmiFeatures.Industry4Logging, true).PerformClick(); Pump(600);
        var fa = MachineLoggers.Of("FA2010");
        Check("switched on: a logger for the machine with a log database (FA2010), the HMI's log into the same database - at once",
              HmiFeatures.LoggingOn && fa != null && fa.Running && AuditLogReader.Store != null && HmiLogStore.Flush() && File.Exists(db),
              MachineLoggers.All.Count + " loggers, store " + (AuditLogReader.Store != null) + ", " + HmiLogStore.Problem);
        Pump(1200);
        Check("  the Features tab says what it does now: the machine and its logger's state",
              features.NowText(HmiFeatures.Industry4Logging).StartsWith("Processing Center: Taking the records from PLC station 1 (R27000)"),
              features.NowText(HmiFeatures.Industry4Logging));
        features.SwitchOf(HmiFeatures.Industry4Logging, false).PerformClick(); Pump(300);
        Check("switched off: the loggers stopped, the HMI's log back in its files", !HmiFeatures.LoggingOn && MachineLoggers.All.Count == 0 && !fa.Running &&
              AuditLogReader.Store == null, MachineLoggers.All.Count + " loggers");
        host.Close(); host.Dispose();

        Console.WriteLine(fail == 0 ? "FEATURES AND LOGS ALL PASS" : "FEATURES AND LOGS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
