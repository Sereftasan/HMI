using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.Core.Machines;
using Kaban.UI.Forms;
using Kaban.UI.Settings;

// The PLC Connection page (connections and their state in one table, the events,
// the MX tools), the communication banner below the page, the user menu without
// the monitor - on the real FA2010 program - and the controllers closed at exit.
class CP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        for (; t != null; t = t.BaseType) {
            var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o is Type ? null : o, null);
        }
        return null;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); Thread.Sleep(20); } }
    static IEnumerable<Control> All(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; } }

    // A controller slow to let go (an MX station waiting for an Open still running).
    class SlowDriver : IPlcDriver, INonBlockingDriver, IDisposable {
        public bool Disposed;
        public bool IsConnected { get { return true; } }
        public bool IsHealthy { get { return true; } }
        public bool Connect() { return true; }
        public void Disconnect() { Thread.Sleep(700); }
        public void Dispose() { Disposed = true; }
        public int Read(string a) { return 0; }
        public int[] ReadMulti(string[] a) { return new int[a.Length]; }
        public int Write(string a, int v) { return 0; }
        public bool TryRead(string a, out int v) { v = 0; return true; }
        public bool TryReadMulti(string[] a, out int[] v) { v = new int[a.Length]; return true; }
    }

    static object ShowPage(SettingsScreen tabs, string titleContains) {
        var page = tabs.AllPages.First(p => p.TitleFallback.Contains(titleContains));
        typeof(SettingsScreen).GetMethod("ShowPage", Any).Invoke(tabs, new object[] { page });
        Pump(50);
        return typeof(SettingsScreen).GetMethod("ViewOf", Any).Invoke(tabs, new object[] { page });
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "cp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");

        Console.WriteLine("--- an MX Component station is released at exit, its thread ended ---");
        var mx = new Kaban.Drivers.MitsubishiActUtl.MitsubishiActUtl(1);
        var worker = Get(mx, "_worker");
        var thread = (Thread)Get(worker, "_thread");
        Check("its MX thread runs while it is in use", thread != null && thread.IsAlive);
        var sw = Stopwatch.StartNew();
        mx.Dispose();
        Check("  disposed: the thread has ended (COM shut down for it), within the wait", !thread.IsAlive && sw.ElapsedMilliseconds < 6000,
              "alive=" + thread.IsAlive + " " + sw.ElapsedMilliseconds + " ms");
        int v;
        sw.Restart();
        Check("  and later calls answer at once, harmlessly", !mx.Connect() && !mx.TryRead("D0", out v) && sw.ElapsedMilliseconds < 500, sw.ElapsedMilliseconds + " ms");
        mx.Dispose();
        Check("  disposing twice is harmless", true);

        Console.WriteLine("--- the real FA2010 program ---");
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 })
            PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show();
        Pump(200);

        Console.WriteLine("--- the user menu ---");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var menu = (ContextMenuStrip)typeof(MainForm).GetMethod("BuildLogoutMenu", Any).Invoke(form, null);
        Check("no Simulation / PLC Monitor entry (the side bar has it, and F12)",
              !menu.Items.Cast<ToolStripItem>().Any(i => (i.Text ?? "").IndexOf("Monitor", StringComparison.OrdinalIgnoreCase) >= 0),
              string.Join(" | ", menu.Items.Cast<ToolStripItem>().Select(i => i.Text)));

        Console.WriteLine("--- the communication banner takes its own room ---");
        var banner = (Label)Get(form, "_lblComm");
        var content = (Panel)Get(form, "pnlContent");
        int before = content.Height;
        banner.Visible = true;
        form.PerformLayout();
        Pump(50);
        Check("shown: the page ends where the banner starts - its scroll bar stays in sight",
              content.Bottom <= banner.Top && content.Height == before - banner.Height,
              "page " + content.Bounds + ", banner " + banner.Bounds);
        Check("  under the page only, the width of the content column", banner.Left == content.Left && banner.Width == content.Width,
              banner.Bounds + " vs " + content.Bounds);
        banner.Visible = false;
        form.PerformLayout();
        Pump(50);
        Check("  gone: the page takes the room back", content.Height == before, content.Height + " vs " + before);

        Console.WriteLine("--- PLC Connection: one tab ---");
        var groups = (System.Collections.IDictionary)Get(form, "_screenGroups");
        var hmi = (SettingsScreen)groups.Keys.Cast<UserControl>().First(k => ((MachineRegistry.RoleGroup)groups[k]).Role == ScreenRoles.HmiSettings);
        form.LoadScreen(hmi);
        Pump(50);
        var settings = All(hmi).OfType<SettingsScreen>().FirstOrDefault(sc => sc.AllPages.Any(p => p.TitleKey == "Set_StationConn")) ?? hmi;
        var titles = settings.AllPages.Select(p => p.TitleFallback).ToList();
        Check("HMI Settings has PLC Connection, and no separate PLC Diagnostics", titles.Contains("PLC Connection") && !titles.Any(t => t.Contains("Diagnostics")),
              string.Join(", ", titles));
        var conn = ShowPage(settings, "PLC Connection") as SettingsScreen;
        Check("  its tabs: Controllers, Connection events, MX Component (Admin)",
              conn != null && string.Join(",", conn.AllPages.Select(p => p.TitleFallback)) == "Controllers,Connection events,MX Component" &&
              conn.AllPages.Last().RequiredLevel == SecurityLevel.Admin,
              conn == null ? "not a tab host" : string.Join(",", conn.AllPages.Select(p => p.TitleFallback)));
        Check("  open to Maintenance", settings.AllPages.First(p => p.TitleKey == "Set_StationConn").RequiredLevel == SecurityLevel.Maintenance);
        var ctl = conn == null ? null : ShowPage(conn, "Controllers") as StationConnectionControl;
        Pump(1200);
        var table = ctl == null ? null : (TableLayoutPanel)Get(ctl, "_table");
        var header = table == null ? new List<string>() : Enumerable.Range(0, table.ColumnCount).Select(c => table.GetControlFromPosition(c, 0)).Where(c => c != null).Select(c => c.Text).ToList();
        Check("the Controllers table: the connection and, beside it, how it is doing (the program version in the State)",
              header.Count == 8 && header[4] == "State" && header[5] == "Last answer" && header[6] == "Drops today" && header[7] == "Last error",
              string.Join(" | ", header));
        Check("  every column as wide as what it holds (no fixed widths)",
              table != null && table.ColumnStyles.Cast<ColumnStyle>().All(cs => cs.SizeType == SizeType.AutoSize));
        var rows = ctl == null ? null : (System.Collections.IList)Get(ctl, "_rows");
        var first = rows != null && rows.Count > 0 ? rows[0] : null;
        var state = first == null ? null : (Label)Get(first, "Status");
        Check("  a simulated controller that answers: Connected, with its last answer", state != null && state.Text.Contains("Connected") &&
              ((Label)Get(first, "Last")).Text.Contains(":"), state == null ? "no row" : state.Text + " / " + ((Label)Get(first, "Last")).Text);
        Kaban.Core.Helpers.ConnectionMonitor.ReportVersion(1, 102);
        var stRow = Kaban.UI.Settings.ControllerStatus.RowFor(1, "", null, null);
        Check("  connected: the program version beside it - 'Connected (Version: 1.02)'", stRow.State.EndsWith("Connected (Version: 1.02)"), stRow.State);
        var driverBox = first == null ? null : (ComboBox)Get(first, "Driver");
        var save = ctl == null ? null : (Button)Get(ctl, "_btnSave");
        Check("  an Admin can change the connections", driverBox != null && driverBox.Enabled && save.Visible);
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        Pump(50);
        Check("  Maintenance reads it, cannot change it (and is told so)", !driverBox.Enabled && !save.Visible && ((Label)Get(ctl, "_adminNote")).Visible);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var events = ShowPage(conn, "Connection events") as LogViewerControl;
        Check("Connection events: the log viewer, with its controller filter", events != null && events.PlcChoices.Count > 1,
              events == null ? "none" : string.Join(" | ", events.PlcChoices));

        form.Close();
        Pump(100);

        Console.WriteLine("--- every station closed at exit - all at once ---");
        // (the form's closing closed the simulated ones; these two stand for slow MX stations)
        var a = new SlowDriver(); var b = new SlowDriver();
        PlcManager.Instance.RegisterDriver(91, a);
        PlcManager.Instance.RegisterDriver(92, b);
        sw.Restart();
        PlcManager.Instance.DisconnectAll();
        Check("two stations each 0.7 s to let go: closed together, not one after the other", sw.ElapsedMilliseconds < 1300, sw.ElapsedMilliseconds + " ms");
        Check("  and released (disposed)", a.Disposed && b.Disposed);

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nCONNECTION PAGE ALL PASS" : "\nCONNECTION PAGE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
