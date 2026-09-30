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

// The side panel on the REAL FA2010 program: the bar at the right edge, the PLC
// Monitor and Inputs / Outputs beside the page (never over it), a window of
// their own and back, Find inside the monitor, a machine's own side page, and
// the frame Find puts round a unit - on the page, in sight.
class SP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
        return t.GetProperty(n, Any).GetValue(o is Type ? null : o, null);
    }
    static IEnumerable<Control> AllOf(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in AllOf(c)) yield return d; } }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }

    // A machine with a side page of its own (an order's details, say).
    class OrderModule : IMachineModule {
        public string ModuleId { get { return "TST"; } }
        public string DisplayNameKey { get { return "TST"; } }
        public void RegisterPlc(IMachineMountContext ctx) { }
        public void RegisterConditions(IMachineMountContext ctx) { }
        public void RegisterData(IMachineMountContext ctx) { }
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx) {
            yield return new MachineScreen {
                TitleFallback = "Order", Role = ScreenRoles.SidePage, SortOrder = 50,
                CreateView = () => {
                    var page = new UserControl { Name = "orderPage" };
                    page.Controls.Add(new LampHMI { Name = "lampOrder", PlcNumber = 1, Address = "M777", Location = new Point(20, 20) });
                    return page;
                }
            };
        }
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "side_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));

        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        MachineRegistry.MountSub(new OrderModule(), new[] { 1 });
        foreach (int st in new[] { 1, 2, 3, 4 })
            PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));
        PlcLabelTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));

        Console.WriteLine("--- a machine's side page is on the bar, not in the menu ---");
        Check("ScreenRoles.SidePage is left out of the left menu", !MachineRegistry.NavigationGroups().Any(g => g.Pages.Any(p => p.Value.Role == ScreenRoles.SidePage)));
        Check("  and listed as a side page, named by its machine", SidePages.All().Any(p => p.Key == "TST.Order" && p.Title.EndsWith("Order")),
              string.Join(", ", SidePages.All().Select(p => p.Key + "=" + p.Title)));

        var sim = (Kaban.Drivers.Simulation.SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(1));
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show();
        Pump(200);

        Console.WriteLine("--- the bar ---");
        var bar = (Panel)Get(form, "_sideBar");
        var top = (Panel)Get(form, "pnlTop");
        var content = (Panel)Get(form, "pnlContent");
        var buttons = (Dictionary<string, Button>)Get(form, "_sideButtons");
        Check("at the right edge, from under the caption to the bottom", bar.Visible && bar.Right == form.ClientSize.Width && bar.Top == top.Bottom,
              bar.Bounds + " in " + form.ClientSize + ", caption bottom " + top.Bottom);
        Check("  the admin's pages: Inputs / Outputs, the Axis Monitor, the machine's Order, the PLC Monitor, in that order",
              string.Join(",", buttons.Keys) == "IoMonitor,AxisMonitor,TST.Order,PlcMonitor", string.Join(",", buttons.Keys));
        var tips = (ToolTip)Get(form, "_sideTips");
        Check("  each says what it opens", buttons.All(kv => (tips.GetToolTip(kv.Value) ?? "").Length > 0), tips.GetToolTip(buttons["PlcMonitor"]));

        Console.WriteLine("--- the PLC Monitor beside the page, not over it ---");
        int pageWidth = content.Width;
        var monitor = form.ShowSimulationMonitor();
        Pump(100);
        var panel = (Panel)Get(form, "_sidePanel");
        var split = (Splitter)Get(form, "_sideSplitter");
        Check("it opens in the side panel", monitor != null && monitor.Embedded && !monitor.TopLevel && panel.Visible && monitor.Visible,
              monitor == null ? "null" : "embedded=" + monitor.Embedded);
        Check("  the page NARROWS to make room - nothing is covered", content.Right <= split.Left && split.Right <= panel.Left && content.Width < pageWidth,
              "page " + content.Bounds + ", border " + split.Bounds + ", panel " + panel.Bounds);
        Check("  and the bar stays at the edge", panel.Right == bar.Left, panel.Right + " vs " + bar.Left);
        Check("  the border can be dragged", split.Visible && split.Dock == DockStyle.Right);
        Check("  its button is marked", form.OpenSidePage != null && form.OpenSidePage.Key == "PlcMonitor");
        var header = (Panel)Get(form, "_sideHeader");
        Check("  its header carries the page's own picture (the side bar's) before its name",
              header.Controls.Cast<Control>().Any(c => c.GetType().Name == "SidePageIcon" && c.Visible && c.Width > 0));
        var sheetGrid = AllOf(monitor).OfType<DataGridView>().First();
        int needed = (int)Math.Ceiling(sheetGrid.ColumnHeadersDefaultCellStyle.Font.GetHeight()) + sheetGrid.ColumnHeadersDefaultCellStyle.Padding.Vertical;
        Check("  the lists' heading is tall enough for its captions (they were cut through)",
              sheetGrid.ColumnHeadersHeightSizeMode == DataGridViewColumnHeadersHeightSizeMode.AutoSize && sheetGrid.ColumnHeadersHeight >= needed,
              sheetGrid.ColumnHeadersHeight + " for " + needed);

        Console.WriteLine("--- the page beside it scrolls; Find brings a unit into sight ---");
        var groupsOf = (System.Collections.IDictionary)Get(form, "_screenGroups");
        var homePage = groupsOf.Keys.Cast<UserControl>().First(k => ((MachineRegistry.RoleGroup)groupsOf[k]).Role == ScreenRoles.Home);
        var sizeBefore = form.Bounds;
        form.WindowState = FormWindowState.Normal;
        form.Bounds = new Rectangle(0, 0, 1180, 820);
        form.LoadScreen(homePage);
        Pump(150);
        var leaf = AllOf(homePage).OfType<ScrollableControl>().FirstOrDefault(c => c.AutoScroll && !(c is Kaban.UI.Settings.SettingsScreen));
        Check("the page scrolls when the panel narrows it (it was cut off, no scroll bar)", leaf != null && leaf.HorizontalScroll.Visible,
              leaf == null ? "no scrolling page" : leaf.GetType().Name + " scroll=" + leaf.HorizontalScroll.Visible);
        var d110 = DeviceFindForm.Match(DeviceCollector.UsesOf(2), 2, "D110").FirstOrDefault();
        string why0;
        var far = d110 == null ? null : form.ShowDeviceUse(d110, out why0);
        Pump(100);
        if (far != null && leaf != null)
        {
            var seen = leaf.RectangleToScreen(leaf.ClientRectangle);
            var at = far.Parent.RectangleToScreen(far.Bounds);
            Check("  a unit at the far side found by Find: scrolled into sight, beside the panel", seen.Contains(at) && -leaf.AutoScrollPosition.X > 0,
                  at + " in " + seen + ", scrolled " + leaf.AutoScrollPosition);
        }
        else Check("  a unit at the far side found by Find", false, d110 == null ? "D110 not found" : "not shown");
        form.Bounds = sizeBefore;
        Pump(50);

        Console.WriteLine("--- Find inside the monitor, the unit framed on the page ---");
        var find = (DeviceFindForm)monitor.GetType().GetMethod("OpenFind", Any).Invoke(monitor, new object[] { "M672" });
        Pump(100);
        Check("Find opens INSIDE the monitor, not as a window over the page", find != null && !find.TopLevel && find.Parent != null && monitor.Contains(find),
              find == null ? "null" : "toplevel=" + find.TopLevel);
        var uses = DeviceCollector.UsesOf(1);
        var use = DeviceFindForm.Match(uses, 1, "M672").First();
        string why;
        var target = form.ShowDeviceUse(use, out why);
        Pump(100);
        Check("the unit's page opens beside the panel", target != null && target.Visible, why);
        if (target != null) {
            var unitOnScreen = target.RectangleToScreen(target.ClientRectangle);
            var panelOnScreen = panel.RectangleToScreen(panel.ClientRectangle);
            var pageOnScreen = content.RectangleToScreen(content.ClientRectangle);
            Check("  the unit is in sight: its left edge on the page, which the panel does not overlap",
                  pageOnScreen.Contains(new Point(unitOnScreen.Left + 1, unitOnScreen.Top + 1)) && !pageOnScreen.IntersectsWith(panelOnScreen),
                  unitOnScreen + " on page " + pageOnScreen + " / panel " + panelOnScreen);
            ControlHighlight.Flash(target, 1200);
            Pump(50);
            var barsOnPage = target.Parent.Controls.OfType<Panel>().Count(p => p.BackColor.ToArgb() == ControlHighlight.Blue.ToArgb());
            Check("  framed by bars ON THE PAGE, beside the unit (no window over the screen)", ControlHighlight.IsShowing && barsOnPage == 4, barsOnPage + " bars");
            Pump(1700);
            Check("  and they go by themselves", !ControlHighlight.IsShowing &&
                  target.Parent.Controls.OfType<Panel>().Count(p => p.BackColor.ToArgb() == ControlHighlight.Blue.ToArgb()) == 0);
        }
        find.GetType().GetMethod("CloseEmbedded", Any).Invoke(find, null);
        Check("Find's close hides it inside the monitor", !((Panel)Get(monitor, "_findHost")).Visible);

        Console.WriteLine("--- closed, and opened again as it was left ---");
        form.ToggleSidePage("PlcMonitor");
        Pump(50);
        Check("the button closes the panel; the page takes the room back", !panel.Visible && !split.Visible && content.Width == pageWidth,
              content.Width + " vs " + pageWidth);
        var again = form.ShowSimulationMonitor();
        Check("  opened again: the same monitor (tabs, undo, Find kept)", ReferenceEquals(again, monitor));

        Console.WriteLine("--- a window of its own, and back ---");
        form.GetType().GetMethod("PopOutSidePage", Any).Invoke(form, null);
        Pump(100);
        var windows = (System.Collections.IDictionary)Get(form, "_sideWindows");
        var win = windows.Contains("PlcMonitor") ? (SidePageWindow)windows["PlcMonitor"] : null;
        Check("pop out: in a window of its own, the panel closed", win != null && win.Visible && win.View == monitor && monitor.Parent == win && !panel.Visible);
        Check("  opening it again brings that window forward, not a second monitor", form.ShowSidePage("PlcMonitor") == monitor && windows.Count == 1);
        Pump(50);
        var titles = AllOf(win).Where(c => c.GetType().Name == "HeaderTitle").ToList();
        Check("  ONE title: no Windows frame over the header, the page's name once", win.FormBorderStyle == FormBorderStyle.None &&
              titles.Count == 1 && titles[0].Text == SidePages.Find("PlcMonitor").Title,
              titles.Count + " titles");
        // On the title bar itself (Find's pane inside the monitor has a "Close" of its own).
        var captionBar = win.Controls.Cast<Control>().FirstOrDefault(c => c != win.View);
        Func<string, Control> capBtn = n => captionBar == null ? null : captionBar.Controls.Cast<Control>().FirstOrDefault(c => c is Button && c.AccessibleName == n);
        var dockB = capBtn("Dock back"); var minB = capBtn("Minimize"); var maxB = capBtn("Maximize"); var closeB = capBtn("Close");
        Check("  its buttons on the title bar: back into the panel, minimise, maximise, close - in that order",
              dockB != null && minB != null && maxB != null && closeB != null && dockB.Parent == closeB.Parent &&
              dockB.Left < minB.Left && minB.Left < maxB.Left && maxB.Left < closeB.Left && closeB.Right == closeB.Parent.ClientSize.Width,
              dockB == null ? "no dock button" : dockB.Left + "," + (minB == null ? -1 : minB.Left) + "," + (maxB == null ? -1 : maxB.Left) + "," + (closeB == null ? -1 : closeB.Left));
        Check("  the page's picture on the title bar, and as the window's icon", AllOf(win).Any(c => c.GetType().Name == "SidePageIcon") && win.Icon != null);
        var cfg = File.ReadAllText(Path.Combine(dir, "SidePanel.cfg"));
        Check("  remembered: it opens in a window next time", cfg.Contains("InWindow=PlcMonitor"), cfg.Replace("\r\n", " | "));
        win.GetType().GetField("DockBack", Any);
        var dockBack = (EventHandler)typeof(SidePageWindow).GetField("DockBack", Any).GetValue(win);
        dockBack(win, EventArgs.Empty);
        Pump(100);
        Check("back into the panel", panel.Visible && monitor.Parent == Get(form, "_sideHost") && windows.Count == 0);
        Check("  and remembered", !File.ReadAllText(Path.Combine(dir, "SidePanel.cfg")).Contains("InWindow=PlcMonitor"));

        Console.WriteLine("--- Inputs / Outputs ---");
        var ioList = IoList.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IoList.csv"), n => n);
        var x20 = ioList.FirstOrDefault(p => p.Address == "X20");
        var x104 = ioList.FirstOrDefault(p => p.Address == "X104");
        Check("FA2010's list: 77 inputs, 110 outputs", ioList.Count(p => p.IsInput) == 77 && ioList.Count(p => !p.IsInput) == 110,
              ioList.Count(p => p.IsInput) + "/" + ioList.Count(p => !p.IsInput));
        Check("  X20 is read at M200, the relay the PLC copies it into", x20 != null && x20.Read == "M200" && x20.Label == "IN_AcilStop");
        Check("  X104 at its own address: its relay is written by X110 too", x104 != null && x104.Read == "X104");
        sim.Write("M200", 1); sim.Write("M350", 1); sim.Write("M201", 0);
        var io = form.ShowSidePage("IoMonitor") as IoMonitorPage;
        Pump(700);
        var lists = io == null ? new List<IoListHMI>() : ((IEnumerable<IoListHMI>)Get(io, "Lists")).ToList();
        var inputs = lists.FirstOrDefault(l => l.Shown.Any(p => p.Address == "X20"));
        var outputs = lists.FirstOrDefault(l => l.Shown.Any(p => p.Address == "Y0"));
        Check("the page replaces the monitor in the panel", io != null && io.Visible && !monitor.Visible && form.OpenSidePage.Key == "IoMonitor");
        Check("  read by the page scanner: X20 lit, X21 not, Y0 lit",
              inputs != null && outputs != null && inputs.IsOn(inputs.Shown.First(p => p.Address == "X20")) == true &&
              inputs.IsOn(inputs.Shown.First(p => p.Address == "X21")) == false && outputs.IsOn(outputs.Shown.First(p => p.Address == "Y0")) == true,
              inputs == null ? "no inputs list" : "X20=" + inputs.IsOn(inputs.Shown.First(p => p.Address == "X20")));
        inputs.SetFilter("x2", false);
        Check("  Find: 'x2' keeps X2x only", inputs.Shown.Count > 0 && inputs.Shown.All(p => p.Address.StartsWith("X2")), inputs.Shown.Count + " rows");
        inputs.SetFilter("", true);
        Check("the host's list is not shown again for a mounted machine without one of its own",
              lists.Count == 2 && inputs.Shown.Count(p => p.Address == "X20") == 1, lists.Count + " lists");
        Check("  'only the ones on': X20 among them, and nothing that is off",
              inputs.Shown.Any(p => p.Address == "X20") && inputs.Shown.All(p => inputs.IsOn(p) == true) && !inputs.Shown.Any(p => p.Address == "X21"),
              string.Join(",", inputs.Shown.Select(p => p.Address)));
        inputs.SetFilter("", false);

        Console.WriteLine("--- a machine's own side page ---");
        sim.Write("M777", 1);
        var order = form.ShowSidePage("TST.Order");
        Pump(600);
        var lamp = order == null ? null : order.Controls.Find("lampOrder", true).FirstOrDefault() as LampHMI;
        Check("it opens in the panel, its lamps read like a page's", order != null && lamp != null && (bool)Get(lamp, "_isOn"),
              lamp == null ? "no lamp" : "on=" + Get(lamp, "_isOn"));
        var orderUse = DeviceCollector.UsesOf(1).FirstOrDefault(u => u.Address == "M777" && u.Screen != null && u.Screen.Role == ScreenRoles.SidePage);
        form.CloseSidePanel();
        var found = orderUse == null ? null : form.ShowDeviceUse(orderUse, out why);
        Check("Find opens a side page's device in the panel", orderUse != null && found == lamp && form.OpenSidePage != null && form.OpenSidePage.Key == "TST.Order", why);

        Console.WriteLine("--- the level decides ---");
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        Pump(50);
        Check("Maintenance: Inputs / Outputs, Axis Monitor and Order, no PLC monitor", string.Join(",", buttons.Keys) == "IoMonitor,AxisMonitor,TST.Order", string.Join(",", buttons.Keys));
        form.ShowSidePage("IoMonitor");
        SecurityManager.Logout();
        Pump(50);
        Check("signed out: the Axis Monitor and the Order page stay (they ask for no level); Inputs / Outputs is closed",
              string.Join(",", buttons.Keys) == "AxisMonitor,TST.Order" && form.OpenSidePage == null && bar.Visible, string.Join(",", buttons.Keys));

        Console.WriteLine("--- the bar folds to the edge ---");
        form.GetType().GetMethod("SetSideBarFolded", Any).Invoke(form, new object[] { true });
        Check("folded: a thin strip", bar.Width < 20 && !((Panel)Get(form, "_sideBarList")).Visible, bar.Width.ToString());
        form.GetType().GetMethod("SetSideBarFolded", Any).Invoke(form, new object[] { false });
        Check("  and back", bar.Width >= 40);

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nSIDE PANEL ALL PASS" : "\nSIDE PANEL " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
