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

        Console.WriteLine("--- Machine Parameters > Alarms and Logs: the machine's PLC log area ---");
        var fa2010 = Kaban.Core.Machines.MachineRegistry.Modules.First(x => x.IsMain);
        var alarmsPage = new AlarmSettingsPage(fa2010.Context);
        Check("the Alarms tab is 'Alarms and Logs' now", alarmsPage.TitleFallback == "Alarms and Logs" && LanguageManager.GetString(alarmsPage.TitleKey, "") != "");
        var alarms = (AlarmSettingsControl)alarmsPage.GetControl();
        var ahost = new Form { ClientSize = new Size(1260, 760), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        alarms.Dock = DockStyle.Fill; ahost.Controls.Add(alarms); ahost.Show(); Pump(200);
        Check("  Industry 4 logging off: the log settings not shown", !alarms.MachineLog.Visible && alarms.MachineLog.Machine != null);
        HmiFeatures.Set(HmiFeatures.Industry4Logging, true); Pump(400);
        var areaBox = All(alarms.MachineLog).OfType<TextBox>().First(b => b.Name == "logArea");
        Check("  on: shown under the alarms the HMI raises - FA2010's log area R27000, its logger's state", alarms.MachineLog.Visible && areaBox.Text == "R27000" &&
              alarms.MachineLog.Top > ((Control)Get(alarms, "_genPanel")).Top, areaBox.Text + " / " + alarms.MachineLog.StateText);
        var save = (Control)Get(alarms, "_btnSave");
        areaBox.Text = "R28000"; Pump(50);
        Check("  an edit: the page's Save comes on", save.Enabled);
        typeof(AlarmSettingsControl).GetMethod("Save", Any).Invoke(alarms, null); Pump(100);
        var board = DashboardService.Find("FA2010");
        Check("  saved with the alarms: FA2010's log area R28000 - the logger takes it", board.Config.PlcLogArea == "R28000" &&
              MachineLogger.Area(board.Config, board) == "R28000");
        Pump(1500);
        Check("    its state says so", alarms.MachineLog.StateText.Contains("(R28000)"), alarms.MachineLog.StateText);
        var shotDir = Environment.GetEnvironmentVariable("TOUCH_SHOTS");
        if (!string.IsNullOrEmpty(shotDir))
        {
            Directory.CreateDirectory(shotDir);
            var at = ahost.PointToScreen(Point.Empty);
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("python3",
                "-c \"from PIL import ImageGrab; ImageGrab.grab(bbox=(" + at.X + "," + at.Y + "," + (at.X + 1260) + "," + (at.Y + 420) + "), xdisplay=':99').save('" +
                Path.Combine(shotDir, "alarms_and_logs.png") + "')\"") { UseShellExecute = false });
            p.WaitForExit(10000);
        }
        areaBox.Text = "M5"; Pump(50);
        typeof(AlarmSettingsControl).GetMethod("Save", Any).Invoke(alarms, null); Pump(50);
        Check("  a bit device: not saved, said on the page", board.Config.PlcLogArea == "R28000" &&
              ((Label)Get(alarms, "_status")).Text.Contains("word device"), ((Label)Get(alarms, "_status")).Text);
        board.Change(c => c.PlcLogArea = "");
        HmiFeatures.Set(HmiFeatures.Industry4Logging, false); Pump(200);
        Check("  logging off again: hidden", !alarms.MachineLog.Visible);
        ahost.Close(); ahost.Dispose();
        var logs = new LogSettingsControl();
        var lhost = new Form { ClientSize = new Size(1200, 800) };
        logs.Dock = DockStyle.Fill; lhost.Controls.Add(logs); lhost.Show(); Pump(300);
        Check("Logs and Reports: the log area no more - where it is now, said", logs.MachineSettings == null ||
              (!All(logs.MachineSettings).OfType<TextBox>().Any(b => b.Text == "R27000") &&
               All(logs.MachineSettings).OfType<Label>().Any(l => l.Text == "Its PLC log area: Machine Parameters > Alarms and Logs.")));
        lhost.Close(); lhost.Dispose();

        Console.WriteLine(fail == 0 ? "FEATURES AND LOGS ALL PASS" : "FEATURES AND LOGS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
