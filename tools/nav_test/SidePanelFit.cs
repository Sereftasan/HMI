using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;
using Kaban.UI.Screens;

// THE SIDE PANEL'S BORDER (30 September): a double-click makes the panel just
// wide enough for all of the side page, a sliver of the page left to take the
// border back by; the width kept is the one the panel opens with next time,
// whatever size the window had while it opened; the page reopened waits for
// the sign-in it needs; a side window left on the taskbar opens there again.
class SF {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        for (var t = o.GetType(); t != null; t = t.BaseType) {
            var f = t.GetField(n, Any); if (f != null) return f.GetValue(o);
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o, null);
        }
        return null;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static IEnumerable<Control> All(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; } }

    static MainForm Start(string user, SecurityLevel level, Rectangle bounds) {
        SecurityManager.Logout();
        var form = new MainForm { RememberPlace = true };
        form.BuildNavigation();
        form.StartPosition = FormStartPosition.Manual;
        form.Bounds = bounds;
        form.Load += (s, e) => { form.WindowState = FormWindowState.Normal; form.Bounds = bounds; if (user != null) SecurityManager.Login(user, level); };
        form.Show();
        Pump(400);
        return form;
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "sfit_" + Guid.NewGuid().ToString("N"));
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
        foreach (int st in new[] { 1, 2, 3, 4 }) PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));

        var wide = new Rectangle(0, 0, 1260, 900);
        var a = Start("admin", SecurityLevel.Admin, wide);
        var panel = (Panel)Get(a, "_sidePanel");
        var split = (Splitter)Get(a, "_sideSplitter");
        var content = (Panel)Get(a, "pnlContent");
        int sliver = UiScale.For(panel, (int)typeof(MainForm).GetField("PageSliver", Any).GetValue(null));

        Console.WriteLine("--- the PLC Monitor: a double-click on the border ---");
        var monitor = a.ShowSimulationMonitor();
        Pump(150);
        var grid = All(monitor).OfType<DataGridView>().First(g => g.Visible);
        // narrow columns, so all of it fits in this window
        foreach (DataGridViewColumn c in grid.Columns) if (c.Visible) c.Width = 70;
        panel.Width = UiScale.For(panel, 260);
        a.PerformLayout(); Pump(50);
        Check("narrow first: the table does not show all its columns", ((ScrollBar)Get(grid, "HorizontalScrollBar")).Visible || grid.DisplayRectangle.Width < 70 * grid.Columns.Cast<DataGridViewColumn>().Count(c => c.Visible),
              panel.Width.ToString());
        // the double-click itself, as the splitter gets it
        // (the handlers only: the splitter's own dragging is the toolkit's, and
        // Mono's takes the wrong neighbour for a click made up in code)
        Action<string, int> raise = (what, clicks) => {
            var events = (System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component).GetProperty("Events", Any).GetValue(split, null);
            object key = null;
            foreach (var name in new[] { "Event" + what, what + "Event" }) { var f = typeof(Control).GetField(name, Any); if (f != null) { key = f.GetValue(null); break; } }
            var h = key == null ? null : events[key] as MouseEventHandler;
            if (h != null) h(split, new MouseEventArgs(MouseButtons.Left, clicks, 2, 100, 0));
            else Console.WriteLine("  (no " + what + " handler found)");
        };
        raise("MouseDown", 1); raise("MouseUp", 1);
        raise("MouseDown", 2); raise("MouseUp", 2);
        Pump(200);
        var bar = All(monitor).OfType<ToolStrip>().First(t => t.Dock == DockStyle.Top);
        int columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width) + (grid.RowHeadersVisible ? grid.RowHeadersWidth : 0);
        Check("double-clicked: the panel widens to show it all", panel.Width > UiScale.For(panel, 400), panel.Width.ToString());
        Check("  every column in sight (no scrolling sideways)", !((ScrollBar)Get(grid, "HorizontalScrollBar")).Visible && grid.DisplayRectangle.Width >= columns,
              grid.DisplayRectangle.Width + " for " + columns);
        Check("  every button of the toolbar on it (none under the >>)", bar.Items.Cast<ToolStripItem>().Where(i => i.Available).All(i => i.Placement == ToolStripItemPlacement.Main),
              string.Join(",", bar.Items.Cast<ToolStripItem>().Where(i => i.Placement != ToolStripItemPlacement.Main).Select(i => i.AccessibleName ?? i.Text)));
        Check("  and no wider than that (not the whole window)", content.Width > sliver + UiScale.For(panel, 40), "page " + content.Width);
        var cfg = WindowLayout.Read(Path.Combine(dir, "SidePanel.cfg"));
        Check("  the width is kept, as a dragged one is", cfg.ContainsKey("Width") && cfg["Width"] == UiScale.Logical(panel.Width).ToString(),
              (cfg.ContainsKey("Width") ? cfg["Width"] : "-") + " vs " + UiScale.Logical(panel.Width));
        var tips = (ToolTip)Get(a, "_sideTips");
        Check("  the border says so in its tooltip", (Kaban.UI.Controls.UiStyle.TipOf(split) ?? "").Contains("Double-click"), Kaban.UI.Controls.UiStyle.TipOf(split));

        Console.WriteLine("--- more than there is room for: a sliver of the page left ---");
        foreach (DataGridViewColumn c in grid.Columns) if (c.Visible) c.Width = 400;
        a.FitSidePanel();
        Pump(100);
        Check("the page keeps a sliver to take the border back by", Math.Abs(content.Width - sliver) <= 2 && split.Visible && split.Width > 0,
              "page " + content.Width + ", sliver " + sliver);
        Check("  the border never goes further by dragging either (MinExtra)", split.MinExtra == sliver, split.MinExtra.ToString());
        foreach (DataGridViewColumn c in grid.Columns) if (c.Visible) c.Width = 70;
        a.FitSidePanel();
        Pump(100);
        int fitted = panel.Width;

        Console.WriteLine("--- the window made smaller and larger again ---");
        a.Bounds = new Rectangle(0, 0, 900, 800);
        Pump(150);
        Check("smaller: the panel gives way, the page keeps its sliver", panel.Width < fitted && content.Width >= sliver - 2, panel.Width + " / page " + content.Width);
        a.Bounds = wide;
        Pump(150);
        Check("  larger again: back at the width kept", panel.Width == fitted, panel.Width + " vs " + fitted);

        Console.WriteLine("--- Inputs / Outputs ---");
        var io = a.ShowSidePage("IoMonitor") as IoMonitorPage;
        Pump(200);
        a.FitSidePanel();
        Pump(200);
        var lists = ((IEnumerable<IoListHMI>)Get(io, "Lists")).ToList();
        int longest = lists.Max(l => l.PreferredWidth);
        Check("fitted: the inputs and outputs side by side", lists.Count == 2 && lists[1].Left > lists[0].Right, string.Join(" | ", lists.Select(l => l.Bounds.ToString())));
        Check("  every name whole", lists.All(l => l.Width >= longest), string.Join(",", lists.Select(l => l.Width)) + " for " + longest);
        Check("  the page beside it still there", content.Width > sliver, content.Width.ToString());
        int ioWidth = panel.Width;

        Console.WriteLine("--- a machine's side page, measured from its controls ---");
        var sample = new UserControl();
        sample.Controls.Add(new Label { AutoSize = false, Location = new Point(10, 10), Size = new Size(300, 20) });
        sample.Controls.Add(new Button { Location = new Point(20, 40), Size = new Size(120, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right });
        var strip = new Panel { Dock = DockStyle.Top, Height = 30 };
        strip.Controls.Add(new TextBox { Location = new Point(400, 4), Width = 80 });
        sample.Controls.Add(strip);
        int w = (int)typeof(MainForm).GetMethod("ContentWidth", Any).Invoke(null, new object[] { sample });
        Check("the furthest right of what is placed: 480 (a strip on top), + the button kept at the right", w >= 480 && w <= 480 + 130 + 10, w.ToString());

        // 8 October: "the required second page width should be calculated with the selected items as well,
        // not these selection combos' width - 'Motion monitor items' is still there with no member selected"
        Console.WriteLine("--- the Axis Monitor: as wide as its rows, not its pick lists ---");
        var axm = a.ShowSidePage("AxisMonitor") as AxisMonitorPage;
        Pump(400);
        var axBar = axm == null ? null : (AxisPickBar)Get(axm, "_bar");
        Check("open, with its lists (an Admin)", axm != null && axBar != null && axBar.Visible && axBar.MotionPick.Visible);
        if (axBar != null)
        {
            ViewPreferences.SetList(ViewPreferences.MonitorMotion, new string[0]);
            Pump(300);
            a.FitSidePanel(); Pump(200);
            int withLists = panel.Width;
            int lists2 = (int)typeof(MainForm).GetMethod("ContentWidth", Any).Invoke(null, new object[] { axBar });
            axBar.Visible = false; Pump(100);
            a.FitSidePanel(); Pump(200);
            int withoutLists = panel.Width;
            axBar.Visible = true; Pump(100);
            Check("  nothing ticked in Motion monitor items: fitted to the rows - the lists add nothing", withLists == withoutLists && lists2 == 0,
                  withLists + " / " + withoutLists);
            var axRows = All(axm).Where(c => (c is AxisMonitorRow || c is AxisSettingHMI) && c.Visible).ToList();
            int rightmost = axRows.Count == 0 ? 0 : axRows.Max(c => axm.PointToClient(c.PointToScreen(new Point(c.Width, 0))).X);
            Check("  every row whole in it", axRows.Count > 0 && rightmost <= axm.ClientSize.Width + 2, axRows.Count + " rows, " + rightmost + " <= " + axm.ClientSize.Width);
        }
        // Inputs / Outputs as it was, for the next start's checks
        a.ShowSidePage("IoMonitor");
        Pump(200);
        a.FitSidePanel();
        Pump(200);

        a.Close();
        Pump(200);

        Console.WriteLine("--- the next start: the panel as it was left ---");
        var b = Start("admin", SecurityLevel.Admin, new Rectangle(0, 0, 820, 700));
        var panelB = (Panel)Get(b, "_sidePanel");
        var openB = Get(b, "_sideOpen");
        Check("Inputs / Outputs open again", openB != null && (string)openB.GetType().GetField("Key").GetValue(openB) == "IoMonitor" && panelB.Visible);
        Check("  in the small window it opened in: as wide as the room allows", panelB.Width < ioWidth, panelB.Width + " vs " + ioWidth);
        b.Bounds = wide;   // as the program maximises itself
        Pump(200);
        Check("  the window grown: the width it had", panelB.Width == ioWidth, panelB.Width + " vs " + ioWidth);

        Console.WriteLine("--- a side page the user may not open yet waits for the sign-in ---");
        b.ShowSimulationMonitor();
        Pump(100);
        b.Close();
        Pump(200);
        var c2 = Start("op", SecurityLevel.Operator, wide);
        Check("an operator: the PLC Monitor is not opened (no refusal shown)", c2.OpenSidePage == null, c2.OpenSidePage == null ? "" : c2.OpenSidePage.Key);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        Pump(300);
        Check("  the admin signs in: it opens, as it was left", c2.OpenSidePage != null && c2.OpenSidePage.Key == "PlcMonitor",
              c2.OpenSidePage == null ? "none" : c2.OpenSidePage.Key);

        Console.WriteLine("--- a side window on the taskbar opens there again ---");
        c2.ShowSidePage("IoMonitor");
        c2.GetType().GetMethod("PopOutSidePage", Any).Invoke(c2, null);
        Pump(150);
        var windows = (System.Collections.IDictionary)Get(c2, "_sideWindows");
        var win = (Form)windows["IoMonitor"];
        // Mono without a window manager cannot put a window on the taskbar: the
        // program's own two hooks stand in for it
        var down = new HashSet<Form>();
        typeof(MainForm).GetField("IsMinimized", Any).SetValue(null, new Func<Form, bool>(f => down.Contains(f)));
        typeof(MainForm).GetField("Minimize", Any).SetValue(null, new Action<Form>(f => down.Add(f)));
        down.Add(win);
        var place = (Dictionary<string, string>)c2.GetType().GetMethod("CapturePlace", Any).Invoke(c2, null);
        Check("kept as down on the taskbar", place.ContainsKey("WindowsMin") && place["WindowsMin"] == "IoMonitor",
              string.Join(" | ", place.Select(kv => kv.Key + "=" + kv.Value)));
        c2.Close();
        Pump(300);
        var saved = WindowLayout.Read(Path.Combine(dir, "LastPage.cfg"));
        Check("  still there after the program closed (its window closing after it does not drop it)",
              saved.ContainsKey("Windows") && saved["Windows"].Contains("IoMonitor") && saved.ContainsKey("WindowsMin"),
              string.Join(" | ", saved.Select(kv => kv.Key + "=" + kv.Value)));
        var d = Start("admin", SecurityLevel.Admin, wide);
        var winsD = (System.Collections.IDictionary)Get(d, "_sideWindows");
        var winD = winsD.Contains("IoMonitor") ? (Form)winsD["IoMonitor"] : null;
        Check("  the next start: in its window, on the taskbar", winD != null && down.Contains(winD),
              winD == null ? "no window" : winD.WindowState.ToString());
        Check("  and not in the panel as well", d.OpenSidePage == null, d.OpenSidePage == null ? "" : d.OpenSidePage.Key);
        d.GetType().GetMethod("PopOutSidePage", Any);
        winD.Close();
        Pump(150);
        d.Close();
        Pump(200);
        saved = WindowLayout.Read(Path.Combine(dir, "LastPage.cfg"));
        Check("its window closed before the program: not reopened next time (the file written whole)",
              !saved.ContainsKey("Windows") && !saved.ContainsKey("WindowsMin") && !saved.ContainsKey("Side"),
              string.Join(" | ", saved.Select(kv => kv.Key + "=" + kv.Value)));

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nSIDE PANEL FIT ALL PASS" : "\nSIDE PANEL FIT " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
