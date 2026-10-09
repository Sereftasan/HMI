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

// The magnifying factor: kept (panel, user, start), everything drawn at 1.5 -
// once, never twice - and the sign-in handed over a restart.
class MG {
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
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static bool Near(float a, float b) { return Math.Abs(a - b) < 0.2f; }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "mag_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");

        Console.WriteLine("--- the factor, kept ---");
        Check("0.8 .. 1.5 in steps of 0.01 (1%)", DisplaySettings.Clamp(0.5) == 0.8 && DisplaySettings.Clamp(3) == 1.5 && DisplaySettings.Clamp(1.234) == 1.23 && DisplaySettings.Clamp(double.NaN) == 1);
        Check("  a factor kept from before the range narrowed (2, 0.75) is brought within it", DisplaySettings.Clamp(2) == 1.5 && DisplaySettings.Clamp(0.75) == 0.8);
        Check("  typed outside it: refused (0.75, 1.75, 2)", DisplaySettings.Parse("0.75") == null && DisplaySettings.Parse("1.75") == null && DisplaySettings.Parse("2") == null);
        Check("typed: 1.5, 1,5, 150% - and not 3 or 'big'", DisplaySettings.Parse("1.5") == 1.5 && DisplaySettings.Parse("1,5") == 1.5 &&
              DisplaySettings.Parse("150%") == 1.5 && DisplaySettings.Parse("3") == null && DisplaySettings.Parse("big") == null);
        Check("nothing kept yet: 1 everywhere", DisplaySettings.PanelFactor == 1 && DisplaySettings.StartFactor == 1 && DisplaySettings.UserFactor("ayse") == null);
        DisplaySettings.PanelFactor = 1.1;
        DisplaySettings.StartFactor = 1.25;
        DisplaySettings.SetUserFactor("ayse", 1.5);
        DisplaySettings.SetUserFactor("Name with = sign", 0.9);
        DisplaySettings.Reload();
        Check("kept and read back: panel, start, users", DisplaySettings.PanelFactor == 1.1 && DisplaySettings.StartFactor == 1.25 &&
              DisplaySettings.UserFactor("AYSE") == 1.5 && DisplaySettings.UserFactor("Name with = sign") == 0.9,
              File.ReadAllText(DisplaySettings.SettingsPath).Replace("\r\n", " | "));
        DisplaySettings.SetUserFactor("ayse", null);
        Check("a user's own factor removed: back to the screen's", DisplaySettings.UserFactor("ayse") == null);

        Console.WriteLine("--- the sign-in handed over a restart ---");
        typeof(LoginHandoff).GetField("Folder", Any).SetValue(null, Path.Combine(dir, "handoff"));
        string nonce = LoginHandoff.Write("maintenance");
        string user;
        Check("the note is sealed: no user name in the file", !File.ReadAllText(Directory.GetFiles(Path.Combine(dir, "handoff"))[0]).Contains("maintenance"));
        Check("the restarted program is given the user", LoginHandoff.TryConsume(new[] { "x.exe", "--await-exit", "12", LoginHandoff.Switch, nonce }, out user) && user == "maintenance", user);
        Check("  once: the note is gone", !LoginHandoff.TryConsume(new[] { LoginHandoff.Switch, nonce }, out user) && Directory.GetFiles(Path.Combine(dir, "handoff")).Length == 0);
        string other = LoginHandoff.Write("admin");
        Check("  another number on the command line: refused", !LoginHandoff.TryConsume(new[] { LoginHandoff.Switch, "0123456789abcdef0123456789abcdef" }, out user));
        Check("  not a number at all: refused", !LoginHandoff.TryConsume(new[] { LoginHandoff.Switch, "..\\..\\x" }, out user));
        LoginHandoff.TryConsume(new[] { LoginHandoff.Switch, other }, out user);
        typeof(LoginHandoff).GetProperty("Lifetime", Any).SetValue(null, TimeSpan.FromMilliseconds(-1), null);
        string late = LoginHandoff.Write("admin");
        Check("  out of date: refused", !LoginHandoff.TryConsume(new[] { LoginHandoff.Switch, late }, out user));
        typeof(LoginHandoff).GetProperty("Lifetime", Any).SetValue(null, TimeSpan.FromSeconds(60), null);

        Console.WriteLine("--- at 1.5: a window scaled once ---");
        UiScale.Override(1.5);
        AtOneAndAHalf(dir);
    }

    // A method of its own: the UI types it uses are set up when it is compiled,
    // after the factor is set.
    static void AtOneAndAHalf(string dir) {
        Check("UiStyle's fonts are at 1.5 (and known as scaled)", Near(UiStyle.Body.Size, 9.5f * 1.5f) && UiScale.IsScaled(UiStyle.Body), UiStyle.Body.Size.ToString());
        var f = new Form { ClientSize = new Size(400, 300), Font = new Font("Segoe UI", 9F) };
        var label = new Label { Text = "Speed", Location = new Point(10, 20), Size = new Size(100, 20) };
        var own = new Button { Text = "Go", Location = new Point(120, 20), Size = new Size(80, 30), Font = new Font("Segoe UI", 12F) };
        var shared = new Button { Text = "B", Location = new Point(210, 20), Size = new Size(44, 32), Font = UiStyle.Body };
        var grid = new DataGridView { Location = new Point(10, 60), Size = new Size(300, 100) };
        grid.Columns.Add("a", "A"); grid.Columns[0].Width = 120;
        var list = new ListView { View = View.Details, Location = new Point(10, 170), Size = new Size(200, 60) };
        list.Columns.Add("x", 90);
        var strip = new ToolStrip { AutoSize = false, Height = 40, Dock = DockStyle.None, Location = new Point(220, 170), Width = 150 };
        var hosted = new Button { Size = new Size(44, 32) };
        var host = new ToolStripControlHost(hosted) { AutoSize = false, Size = hosted.Size };
        strip.Items.Add(host);
        f.Controls.AddRange(new Control[] { label, own, shared, grid, list, strip });
        f.Show();
        UiScale.Apply(f);
        Check("positions and sizes x1.5", label.Location == new Point(15, 30) && own.Size == new Size(120, 45), label.Location + " " + own.Size);
        Check("its own font x1.5", Near(own.Font.Size, 18f), own.Font.Size.ToString());
        Check("an inherited font follows the window's (scaled once)", Near(label.Font.Size, 13.5f) && ReferenceEquals(label.Font, f.Font), label.Font.Size.ToString());
        Check("a font already at the factor is not scaled again", ReferenceEquals(shared.Font, UiStyle.Body));
        Check("grid columns and list columns x1.5", grid.Columns[0].Width == 180 && list.Columns[0].Width == 135, grid.Columns[0].Width + " / " + list.Columns[0].Width);
        Check("a toolbar's hosted button and its slot x1.5", hosted.Size == new Size(66, 48) && host.Size == new Size(66, 48), hosted.Size + " / " + host.Size);
        UiScale.Apply(f);
        Check("applied twice: nothing changes", own.Size == new Size(120, 45) && Near(own.Font.Size, 18f));

        Console.WriteLine("--- added later: scaled as it is added, once ---");
        var later = new Button { Location = new Point(20, 250), Size = new Size(100, 30) };
        f.Controls.Add(later);
        Check("a button added to the scaled window: x1.5", later.Size == new Size(150, 45) && later.Location == new Point(30, 375), later.Bounds.ToString());
        f.Controls.Remove(later); f.Controls.Add(later);
        Check("  removed and added again: still x1.5, not x2.25", later.Size == new Size(150, 45));
        var panel = new Panel { Location = new Point(0, 0), Size = new Size(100, 100) };
        var inner = new TextBox { Location = new Point(10, 10), Width = 60 };
        panel.Controls.Add(inner);
        f.Controls.Add(panel);
        Check("a panel added with its contents: all x1.5", panel.Width == 150 && Math.Abs(inner.Width - 90) <= 3 && inner.Location == new Point(15, 15), panel.Width + " / " + inner.Width);
        int innerWidth = inner.Width;
        var moved = new Form { ClientSize = new Size(200, 200) };
        f.Controls.Remove(panel);
        moved.Controls.Add(panel);
        moved.Show();
        UiScale.Apply(moved);
        Check("a scaled part moved into a new window is not scaled a second time", panel.Width == 150 && inner.Width == innerWidth, panel.Width + " / " + inner.Width);
        Check("  the new window is", moved.ClientSize.Width >= 290, moved.ClientSize.ToString());
        Check("Px / Logical / For", UiScale.Px(100) == 150 && UiScale.Logical(150) == 100 && UiScale.For(panel, 10) == 15 && UiScale.For(new Label(), 10) == 10);
        moved.Close(); f.Close();

        Console.WriteLine("--- a dialog: scaled as it loads, kept on its screen ---");
        var dlg = new Form { StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(300, 200) };
        dlg.Controls.Add(new Button { Location = new Point(200, 150), Size = new Size(80, 30) });
        UiScale.ApplyOnLoad(dlg);
        dlg.Show(); Pump(50);
        var area = Screen.FromControl(dlg).WorkingArea;
        Check("x1.5 before it is seen, and centred again", dlg.ClientSize.Width >= 440 && Math.Abs((dlg.Left + dlg.Width / 2) - (area.Left + area.Width / 2)) < 30,
              dlg.Bounds + " in " + area);
        dlg.Close();
        var huge = new Form { ClientSize = new Size(1200, 900), StartPosition = FormStartPosition.Manual, Location = new Point(10, 10) };
        UiScale.ApplyOnLoad(huge);
        huge.Show(); Pump(50);
        Check("a window that would outgrow the screen stays on it", huge.Width <= area.Width && huge.Height <= area.Height && huge.Right <= area.Right, huge.Bounds + " in " + area);
        huge.Close();

        Console.WriteLine("--- the real FA2010 program at 1.5 ---");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 })
            PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));
        var shell = new MainForm();
        shell.BuildNavigation();
        shell.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        shell.Show();
        Pump(200);
        var top = (Panel)Get(shell, "pnlTop");
        Check("the caption bar: 31 -> 46 or 47", top.Height >= 45 && top.Height <= 48, top.Height.ToString());
        var nav = ((System.Collections.IDictionary)Get(shell, "_menuButtons")).Keys.Cast<Control>().First();
        Check("a menu button: 45 -> 67 or 68", nav.Height >= 66 && nav.Height <= 69, nav.Height.ToString());
        var menu = (Panel)Get(shell, "pnlMenu");
        Check("the menu: 204 wide -> 306", menu.Width == 306, menu.Width.ToString());
        shell.SetNavCollapsed(true);
        Check("  folded: 56 -> 84", menu.Width == 84, menu.Width.ToString());
        shell.SetNavCollapsed(false);
        Check("  and open again 306 (not 204)", menu.Width == 306, menu.Width.ToString());

        var uses = DeviceCollector.UsesOf(1);
        string why;
        // (9 October: M672's unit on Cutting is its encoder's - a CylinderEncoderUnitHMI, the unit's size)
        var unit = shell.ShowDeviceUse(DeviceFindForm.Match(uses, 1, "M672").First(u => u.PageTitle.EndsWith("Cutting")), out why);
        Pump(100);
        Check("a page opened after start: its unit at 1.5 (360 x 64 -> 540 x 96)", unit != null && unit.Width == 540 && unit.Height == 96,
              unit == null ? why : unit.Size.ToString());
        var home = ((System.Collections.IDictionary)Get(shell, "_menuButtons")).Values.Cast<UserControl>().Last();
        shell.LoadScreen(home);
        shell.ShowDeviceUse(DeviceFindForm.Match(uses, 1, "M672").First(u => u.PageTitle.EndsWith("Cutting")), out why);
        Pump(50);
        Check("  shown, left and shown again: still 540 (scaled once)", unit.Width == 540, unit.Width.ToString());

        Console.WriteLine("--- everything reachable at 1.5 ---");
        var aBtn = (Control)Get(unit is CylinderEncoderUnitHMI ? ((CylinderEncoderUnitHMI)unit).Unit : unit, "_btnA");
        Check("the unit's buttons grow with it (36 at factor 1: a thin row in a tall frame before)", aBtn.Height > 40, aBtn.Height.ToString());
        shell.WindowState = FormWindowState.Normal;
        shell.Bounds = new Rectangle(0, 0, 1200, 740);
        Pump(150);
        var menuPanel = (Panel)Get(shell, "pnlMenu");
        var navBody = (Panel)Get(shell, "_navBody");
        var histRow = ((Control)Get(shell, "_btnHistBack")).Parent;
        Check("Back / Forward stay at the bottom of the menu, in sight", histRow.Visible && histRow.Top > 0 && histRow.Bottom <= menuPanel.ClientSize.Height,
              histRow.Bounds + " in " + menuPanel.ClientSize);
        var mstrip = navBody.Controls.OfType<Kaban.UI.Screens.MachineSelectorStrip>().First();
        Check("the machine list is in sight - or the menu scrolls to it", mstrip.Visible && (mstrip.Bottom <= navBody.ClientSize.Height || navBody.VerticalScroll.Visible),
              mstrip.Bounds + " in " + navBody.ClientSize + " scroll=" + navBody.VerticalScroll.Visible);
        var gap = (Panel)Get(shell, "_navSpacer");
        Check("  the gap above it gave way first (3 buttons' height at most)", gap.Height < UiScale.Px(135), gap.Height.ToString());
        var groupsOf = (System.Collections.IDictionary)Get(shell, "_screenGroups");
        var homePage = groupsOf.Keys.Cast<UserControl>().First(k => ((MachineRegistry.RoleGroup)groupsOf[k]).Role == ScreenRoles.Home);
        shell.LoadScreen(homePage);
        Pump(150);
        home = homePage;
        Func<Control, IEnumerable<Control>> all = null;
        all = c => new[] { c }.Concat(c.Controls.Cast<Control>().SelectMany(x => all(x)));
        var scrolling = all(home).OfType<ScrollableControl>().FirstOrDefault(c => c.AutoScroll && c.HorizontalScroll.Visible);
        Check("the page larger than its room scrolls (it was cut off, with no scroll bar)", scrolling != null,
              string.Join(",", all(home).OfType<ScrollableControl>().Where(c => c.AutoScroll).Select(c => c.GetType().Name)));

        var monitor = shell.ShowSimulationMonitor();
        Pump(100);
        var bar = monitor.Controls.OfType<ToolStrip>().First();
        var firstButton = bar.Items.OfType<ToolStripControlHost>().First().Control;
        Check("the monitor's buttons at 1.5 (44 x 32 -> 66 x 48)", firstButton.Size == new Size(66, 48), firstButton.Size.ToString());
        var sideBar = (Panel)Get(shell, "_sideBar");
        Check("the side bar at 1.5 (48 -> 72)", sideBar.Width == 72, sideBar.Width.ToString());
        var sidePanel = (Panel)Get(shell, "_sidePanel");
        sidePanel.Width = 900;
        var splitter = (Splitter)Get(shell, "_sideSplitter");
        typeof(Splitter).GetMethod("OnSplitterMoved", Any).Invoke(splitter, new object[] { new SplitterEventArgs(0, 0, 0, 0) });
        shell.CloseSidePanel();
        Check("the panel's width is kept at factor 1: 900 on screen -> 600 (a restart at another factor keeps it in proportion)",
              File.ReadAllText(Path.Combine(dir, "SidePanel.cfg")).Contains("Width=600"), File.ReadAllText(Path.Combine(dir, "SidePanel.cfg")).Replace("\r\n", " | "));

        Console.WriteLine("--- a user's own factor at sign-in ---");
        SecurityManager.Login("operator", SecurityLevel.Operator);
        Check("no factor of their own: nothing to offer", MainForm.MagnifierToOffer() == null);
        DisplaySettings.SetUserFactor("operator", 1.5);
        Check("their own = the screen's: nothing to offer", MainForm.MagnifierToOffer() == null);
        DisplaySettings.SetUserFactor("operator", 1.25);
        Check("their own is another: offered", MainForm.MagnifierToOffer() == 1.25);
        SecurityManager.Logout();
        Check("signed out: nothing", MainForm.MagnifierToOffer() == null);
        UserStore.AddOrUpdateUser("temp_mag", "pw", SecurityLevel.Operator);
        DisplaySettings.SetUserFactor("temp_mag", 1.4);
        UserStore.RemoveUser("temp_mag");
        Check("a user deleted takes their factor along", DisplaySettings.UserFactor("temp_mag") == null);

        shell.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nMAGNIFIER ALL PASS" : "\nMAGNIFIER " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
