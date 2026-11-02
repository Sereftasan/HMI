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
using Kaban.UI.Forms;
using Kaban.UI.Helpers;

// GRIPS A FINGER CAN HOLD ON THE PAGES' BORDERS (2 November: "holding the border of the pages to extend or reduce the
// dimension of the sub pages is quite hard to do, should we add a controls to the center and corner of each side of the
// pages for the user to able to hold it with the finger touch easier").
class SG {
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
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Mode(string v) { ViewPreferences.SetPanel(ViewPreferences.InputMode, v); Pump(80); }
    // TOUCH_SHOTS=<folder>: pictures of what is checked (a person looks at them)
    static void Shot(Control c, string name) {
        var folder = Environment.GetEnvironmentVariable("TOUCH_SHOTS");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        Pump(200);
        var at = c.PointToScreen(Point.Empty);
        if (c is Form) at = ((Form)c).Location;
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("python3",
            "-c \"from PIL import ImageGrab; ImageGrab.grab(bbox=(" + at.X + "," + at.Y + "," + (at.X + c.Width) + "," + (at.Y + c.Height) + "), xdisplay=':99').save('" +
            Path.Combine(folder, name + ".png") + "')\"") { UseShellExecute = false });
        p.WaitForExit(10000);
    }
    // A finger's messages, as Windows makes them of a touch: the filter first, then the control (unless taken).
    static readonly MethodInfo WndProcOf = typeof(Control).GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic);
    static IntPtr LP(Point p) { return (IntPtr)(int)(((p.Y & 0xFFFF) << 16) | (p.X & 0xFFFF)); }
    static bool Finger(Control c, int msg, Point screen, int keys) {
        var m = Message.Create(c.Handle, msg, (IntPtr)keys, LP(c.PointToClient(screen)));
        bool taken = TouchInput.Handle(ref m);
        if (!taken) WndProcOf.Invoke(c, new object[] { m });
        return taken;
    }
    /// <summary>A finger on the grip's middle, dragged by (dx, dy) in a few steps, lifted.</summary>
    static void DragBy(Control grip, int dx, int dy) {
        var from = grip.PointToScreen(new Point(grip.Width / 2, grip.Height / 2));
        if (grip is Grip && ((Grip)grip).IsCorner) {
            // a corner's L: on its arm, at the very corner
            bool left = (((Grip)grip).Edges & SizeGrips.Edge.Left) != 0, top = (((Grip)grip).Edges & SizeGrips.Edge.Top) != 0;
            int t = Math.Min(grip.Width, grip.Height) / 2;
            from = grip.PointToScreen(new Point(left ? t : grip.Width - t, top ? t : grip.Height - t));
        }
        Finger(grip, 0x201, from, 1);
        for (int i = 1; i <= 4; i++) { Finger(grip, 0x200, new Point(from.X + dx * i / 4, from.Y + dy * i / 4), 1); Pump(20); }
        Finger(grip, 0x202, new Point(from.X + dx, from.Y + dy), 0);
        Pump(60);
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "sg_" + Guid.NewGuid().ToString("N"));
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
        HmiFeatures.Set(HmiFeatures.TouchScreen, true);
        UserStore.CreatorsPath = Path.Combine(dir, "UserCreators.cfg");
        UserStore.ReloadCreators();
        NotesStore.Folder = Path.Combine(dir, "Notes");
        Kaban.UI.Dashboard.MachineLogger.AutoStart = false;
        Kaban.UI.Dashboard.HmiLogStore.AutoStart = false;
        TouchInput.Install();
        Mode("Mouse");

        Console.WriteLine("--- a border between two parts: the splitter ---");
        var f = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(20, 20), ClientSize = new Size(1000, 640), Text = "split" };
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var side = new Panel { Dock = DockStyle.Right, Width = 300, BackColor = Color.FromArgb(240, 244, 250) };
        var split = new Splitter { Dock = DockStyle.Right, Width = 6, MinSize = 200, MinExtra = 150, BackColor = Color.FromArgb(214, 218, 224) };
        int moved = 0; split.SplitterMoved += (s, e) => moved++;
        f.Controls.Add(page); f.Controls.Add(split); f.Controls.Add(side);
        SizeGrips.ForSplitter(split, 6);
        f.Show(); Pump(150);
        var grips = SizeGrips.Of(split);
        Check("a mouse: the border as thin as before, no grips", grips.Count == 3 && split.Width == UiScale.For(split, 6) && grips.All(g => !g.Visible),
              split.Width + " / " + grips.Count(g => g.Visible));
        Mode("Touch");
        int wide = UiScale.For(split, SizeGrips.TouchSplitter);
        Check("touch: the border wider, three grips on it - its middle and near each end, centred on it, over the parts beside it",
              split.Width == wide && grips.All(g => g.Visible && g.Parent == f) &&
              Math.Abs(grips[0].Left + grips[0].Width / 2 - (split.Left + split.Width / 2)) <= 1 &&
              Math.Abs(grips[0].Top + grips[0].Height / 2 - (split.Top + split.Height / 2)) <= 1 &&
              grips[1].Top < split.Top + split.Height / 4 && grips[2].Bottom > split.Bottom - split.Height / 4 &&
              grips.All(g => g.Width > split.Width && f.Controls.GetChildIndex(g) < f.Controls.GetChildIndex(side)),
              split.Width + " " + string.Join(" ", grips.Select(g => g.Bounds.ToString())));
        Shot(f, "grips_splitter");
        int before = side.Width; moved = 0;
        DragBy(grips[0], -120, 15);
        Check("the middle grip dragged 120 px to the left: the side part 120 px wider, as the border's own drag does (it says it moved)",
              side.Width == before + 120 && moved > 0 && grips[0].Left + grips[0].Width / 2 == split.Left + split.Width / 2,
              before + " -> " + side.Width + ", moved " + moved);
        before = side.Width;
        DragBy(grips[2], 2000, 0);
        Check("  an end's grip dragged far to the right: no narrower than the border lets it (its MinSize)", side.Width == split.MinSize,
              before + " -> " + side.Width);
        DragBy(grips[1], -3000, 0);
        Check("  far to the left: the page keeps what the border leaves it (its MinExtra)", page.Width >= split.MinExtra - 1 && page.Width <= split.MinExtra + wide,
              "page " + page.Width + ", side " + side.Width);
        f.ClientSize = new Size(1000, 200); Pump(80);
        Check("  a short border: its middle grip only", grips[0].Visible && !grips[1].Visible && !grips[2].Visible);
        f.ClientSize = new Size(1000, 640); Pump(80);
        Mode("Mouse");
        Check("a mouse again: thin, no grips", split.Width == UiScale.For(split, 6) && grips.All(g => !g.Visible));
        f.Close(); f.Dispose();

        Console.WriteLine("--- a page in a window of its own ---");
        var w = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = new Point(200, 120), Size = new Size(700, 520),
                           MinimumSize = new Size(320, 240), Padding = new Padding(5), BackColor = Color.FromArgb(238, 240, 243), Text = "window" };
        var cap = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(238, 240, 243) };
        var closeBtn = new Button { Dock = DockStyle.Right, Width = 46, Text = "X", FlatStyle = FlatStyle.Flat };
        cap.Controls.Add(closeBtn);
        var view = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        w.Controls.Add(view); w.Controls.Add(cap);
        int sized = 0;
        SizeGrips.ForWindow(w, 5, () => sized++);
        w.Show(); Pump(150);
        var wg = SizeGrips.Of(w);
        Check("a mouse: its frame as before, no grips", wg.Count == 12 && w.Padding.Left == UiScale.For(w, 5) && wg.All(g => !g.Visible), w.Padding + "");
        Mode("Touch");
        int frame = UiScale.For(w, SizeGrips.TouchFrame);
        Func<string, Grip> grip = n => (Grip)wg.First(g => g.Name == "sizeGrip" + n);
        var c = w.ClientSize;
        Check("touch: a wider frame; a grip at each corner (two arms along the frame: an L) and at the middle of each side",
              w.Padding.Left == frame && wg.All(g => g.Visible) && wg.Count(g => ((Grip)g).IsCorner) == 8 &&
              grip("LeftTopH").Bounds == new Rectangle(0, 0, UiScale.For(w, SizeGrips.CornerArm), frame) &&
              grip("LeftTopV").Bounds == new Rectangle(0, 0, frame, UiScale.For(w, SizeGrips.CornerArm)) &&
              grip("RightBottomH").Right == c.Width && grip("RightBottomH").Bottom == c.Height && grip("RightBottomV").Right == c.Width && grip("RightBottomV").Bottom == c.Height &&
              grip("Left").Left == 0 && Math.Abs(grip("Left").Top + grip("Left").Height / 2 - c.Height / 2) <= 1 &&
              grip("Top").Top == 0 && Math.Abs(grip("Top").Left + grip("Top").Width / 2 - c.Width / 2) <= 1 &&
              grip("Right").Right == c.Width && grip("Bottom").Bottom == c.Height,
              w.Padding + " " + string.Join(" ", wg.Select(g => g.Name + g.Bounds)));
        // the caption's close button stays clear of the corner's L: its arms are the frame's width
        var closeAt = closeBtn.RectangleToScreen(closeBtn.ClientRectangle);
        Check("  the top right corner's arms do not cover the caption's close button",
              new[] { "TopRightH", "TopRightV" }.All(n => !closeAt.IntersectsWith(grip(n).RectangleToScreen(grip(n).ClientRectangle))),
              closeAt + " / " + grip("TopRightH").Bounds + " " + grip("TopRightV").Bounds);
        Shot(w, "grips_window");
        var b0 = w.Bounds; sized = 0;
        DragBy(grip("RightBottomV"), 40, 30);
        Check("the bottom right corner dragged (40, 30): 40 wider, 30 taller, where it was", w.Bounds == new Rectangle(b0.X, b0.Y, b0.Width + 40, b0.Height + 30) && sized == 1,
              b0 + " -> " + w.Bounds);
        b0 = w.Bounds;
        DragBy(grip("Left"), 50, 20);
        Check("  the left side's grip dragged 50 to the right: the left side 50 in, the right side where it was, the height as it was",
              w.Left == b0.Left + 50 && w.Right == b0.Right && w.Top == b0.Top && w.Height == b0.Height, b0 + " -> " + w.Bounds);
        b0 = w.Bounds;
        DragBy(grip("Top"), 0, 2000);
        Check("  the top dragged down past the bottom: no lower than its least height", w.Height == w.MinimumSize.Height && w.Bottom == b0.Bottom,
              b0 + " -> " + w.Bounds);
        b0 = w.Bounds;
        DragBy(grip("LeftTopH"), -60, -80);
        Check("  the top left corner: both of its sides", w.Left == b0.Left - 60 && w.Top == b0.Top - 80 && w.Right == b0.Right && w.Bottom == b0.Bottom,
              b0 + " -> " + w.Bounds);
        Check("  the grips follow the window's sides", grip("RightBottomH").Right == w.ClientSize.Width && grip("Bottom").Bottom == w.ClientSize.Height &&
              Math.Abs(grip("Right").Top + grip("Right").Height / 2 - w.ClientSize.Height / 2) <= 1);
        w.WindowState = FormWindowState.Maximized; Pump(80);
        // (Mono without a window manager keeps a window Normal: then there is nothing to see here)
        Check("maximised: no grips", w.WindowState != FormWindowState.Maximized || wg.All(g => !g.Visible), w.WindowState.ToString());
        w.WindowState = FormWindowState.Normal; Pump(80);
        Mode("Mouse");
        Check("a mouse again: its thin frame, no grips", w.Padding.Left == UiScale.For(w, 5) && wg.All(g => !g.Visible), w.Padding + "");
        w.Close(); w.Dispose();

        Console.WriteLine("--- the program's own: the side page's border, its window ---");
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> pm = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        pm("RegisterDriverKinds").Invoke(null, null);
        pm("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 }) PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));
        SecurityManager.Logout();
        var main = new MainForm();
        main.BuildNavigation();
        var bounds = new Rectangle(0, 0, 1260, 860);
        main.StartPosition = FormStartPosition.Manual; main.Bounds = bounds;
        main.Load += (s, e) => { main.WindowState = FormWindowState.Normal; main.Bounds = bounds; SecurityManager.Login("admin", SecurityLevel.Admin); };
        main.Show(); Pump(400);
        Mode("Touch");
        main.ShowSidePage("IoMonitor"); Pump(300);
        var sideSplit = (Splitter)Get(main, "_sideSplitter");
        var sideGrips = SizeGrips.Of(sideSplit);
        Check("the side page's border: wider, three grips on it", sideSplit.Visible && sideSplit.Width == UiScale.For(sideSplit, SizeGrips.TouchSplitter) &&
              sideGrips.Count == 3 && sideGrips[0].Visible, sideSplit.Width + " / " + sideGrips.Count);
        Shot(main, "grips_side_page");
        var sidePanel = (Panel)Get(main, "_sidePanel");
        int sw = sidePanel.Width;
        DragBy(sideGrips[0], -80, 0);
        Check("  its middle grip dragged 80 to the left: the side page 80 wider", sidePanel.Width == sw + 80, sw + " -> " + sidePanel.Width);
        main.GetType().GetMethod("PopOutSidePage", Any).Invoke(main, null); Pump(300);
        var windows = (System.Collections.IDictionary)Get(main, "_sideWindows");
        var win = windows["IoMonitor"] as Form;
        var winGrips = win == null ? new Grip[0] : SizeGrips.Of(win);
        Check("the page in a window of its own: its wider frame with eight grips", win != null && win.Padding.Left == UiScale.For(win, SizeGrips.TouchFrame) &&
              winGrips.Count == 12 && winGrips.All(g => g.Visible), win == null ? "no window" : win.Padding + " " + winGrips.Count(g => g.Visible));
        if (win != null) Shot(win, "grips_side_window");
        if (win != null) {
            var wb = win.Bounds;
            DragBy(winGrips.First(g => g.Name == "sizeGripRightBottomH"), 60, 40);
            Check("  its corner dragged: the window larger by as much", win.Width == wb.Width + 60 && win.Height == wb.Height + 40, wb + " -> " + win.Bounds);
            Mode("Mouse");
            Check("  a mouse: its frame thin again, no grips", win.Padding.Left == UiScale.For(win, 5) && winGrips.All(g => !g.Visible), win.Padding + "");
            win.Close(); Pump(100);
        }
        main.Close(); Pump(100);

        Console.WriteLine(fail == 0 ? "SIZE GRIPS ALL PASS" : "SIZE GRIPS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
