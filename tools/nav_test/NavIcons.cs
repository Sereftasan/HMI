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
using Kaban.UI.Forms;
using Kaban.UI.Screens;

// The left bar's pictures: the gear is Machine Parameters (as in the other Kaban
// HMIs), HMI Settings has its own; each machine shows what it does; and the
// two bar toggles draw the same chevron.
class NI {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }

    static bool Same(Image a, Image b) {
        if (a == null || b == null) return false;
        var x = new Bitmap(a); var y = new Bitmap(b);
        if (x.Width != y.Width || x.Height != y.Height) return false;
        for (int i = 0; i < x.Width; i++) for (int j = 0; j < x.Height; j++) if (x.GetPixel(i, j) != y.GetPixel(i, j)) return false;
        return true;
    }

    // The chevron's ink: its size and which way it points.
    static Rectangle Ink(Control c, out int leftMass, out int rightMass) {
        var bmp = new Bitmap(c.Width, c.Height);
        c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
        if (Environment.GetEnvironmentVariable("NI_DUMP") != null) bmp.Save("/tmp/claude-0/ink_" + c.AccessibleName.Replace(" ", "") + "_" + DateTime.Now.Ticks + ".png");
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
        var pts = new List<Point>();
        for (int i = 0; i < bmp.Width; i++) for (int j = 0; j < bmp.Height; j++) {
            var p = bmp.GetPixel(i, j);
            if (p.R < 140 && p.G < 140 && p.B < 150) { pts.Add(new Point(i, j)); x0 = Math.Min(x0, i); y0 = Math.Min(y0, j); x1 = Math.Max(x1, i); y1 = Math.Max(y1, j); }
        }
        leftMass = rightMass = 0;
        if (x1 < 0) return Rectangle.Empty;
        // A chevron's arms start on the side AWAY from its point: the top rows of
        // ">>" lie left of the middle of the whole glyph, those of "<<" right of it.
        double mid = (x0 + x1) / 2.0;
        var top = pts.Where(p => p.Y <= y0 + 2).ToList();
        double avg = top.Average(p => (double)p.X);
        if (avg > mid) leftMass = 1; else rightMass = 1;
        return new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "ni_" + Guid.NewGuid().ToString("N"));
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

        Console.WriteLine("--- the gear is Machine Parameters ---");
        var groups = MachineRegistry.NavigationGroups().ToList();
        var mp = groups.First(g => g.Role == ScreenRoles.MachineParameters);
        var hmi = groups.First(g => g.Role == ScreenRoles.HmiSettings);
        var gear = Kaban.UI.Properties.Resources.settings_24;
        Check("Machine Parameters shows the gear", Same(mp.Icon() as Image, gear));
        var hmiIcon = hmi.Icon() as Image;
        Check("HMI Settings has a picture of its own - not the gear, not the sliders",
              hmiIcon != null && !Same(hmiIcon, gear) && !Same(hmiIcon, Kaban.UI.Properties.Resources.parameters_24));
        Check("  24 x 24 like the others", hmiIcon != null && hmiIcon.Width == 24 && hmiIcon.Height == 24);

        Console.WriteLine("--- each machine shows what it does ---");
        var mods = MachineRegistry.Modules.ToDictionary(x => x.Module.ModuleId, x => x);
        foreach (var id in new[] { "FA2010", "H500", "H800" })
            Check(id + " draws its own picture", mods.ContainsKey(id) && mods[id].Module is IMachineIcon && ((IMachineIcon)mods[id].Module).Icon != null);
        var pics = new[] { "FA2010", "H500", "H800" }.Where(mods.ContainsKey).Select(id => MachineIcon.For(mods[id])).ToList();
        Check("  three different pictures", pics.Count == 3 && !Same(pics[0], pics[1]) && !Same(pics[1], pics[2]) && !Same(pics[0], pics[2]));
        foreach (var pic in pics.Select((p, i) => new { p, i }))
        {
            var bmp = new Bitmap(pic.p);
            int inked = 0, coloured = 0;
            for (int x = 0; x < bmp.Width; x++)
                for (int y = 0; y < bmp.Height; y++)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.A < 40) continue;
                    inked++;
                    if (Math.Max(c.R, Math.Max(c.G, c.B)) > 60 || Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B)) > 8) coloured++;
                }
            Check("  " + new[] { "FA2010", "H500", "H800" }[pic.i] + ": plain black ink on nothing - no tile, no colour",
                  inked > 40 && coloured == 0 && bmp.GetPixel(0, 0).A == 0 && bmp.GetPixel(23, 23).A == 0 && bmp.GetPixel(0, 23).A == 0,
                  inked + " inked, " + coloured + " coloured");
        }

        Console.WriteLine("--- the two bar toggles draw the same chevron ---");
        foreach (int st in new[] { 1, 2, 3, 4 })
            PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show();
        Pump(200);
        var nav = (Button)typeof(MainForm).GetField("_btnNavToggle", Any).GetValue(form);
        var side = (Button)typeof(MainForm).GetField("_sideBarToggle", Any).GetValue(form);
        Check("the navigation toggle has no text glyph (drawn, like the side bar's)", nav.Text == "", "'" + nav.Text + "'");
        int nl, nr, sl, sr;
        var navInk = Ink(nav, out nl, out nr);
        var sideInk = Ink(side, out sl, out sr);
        Check("  the same size of chevron", navInk.Width > 4 && Math.Abs(navInk.Width - sideInk.Width) <= 1 && Math.Abs(navInk.Height - sideInk.Height) <= 1,
              navInk.Size + " vs " + sideInk.Size);
        Check("  pointing away: navigation left (<<), side bar right (>>)", nl > nr && sr > sl, nl + "/" + nr + "  " + sl + "/" + sr);
        form.SetNavCollapsed(true);
        Pump(100);
        var folded = Ink(nav, out nl, out nr);
        Check("collapsed: the same chevron pointing right (>>) - no hamburger", nr > nl && Math.Abs(folded.Width - sideInk.Width) <= 1,
              nl + "/" + nr + " " + folded.Size);
        Console.WriteLine("--- the menu's tooltips: only when folded to its pictures ---");
        var navButtons = ((System.Collections.IDictionary)typeof(MainForm).GetField("_menuButtons", Any).GetValue(form)).Keys
                         .Cast<Kaban.UI.Controls.ButtonHMI>().ToList();
        var tipOf = typeof(Kaban.UI.Controls.ButtonHMI).GetMethod("BuildMainTooltip", Any);
        form.SetNavCollapsed(false);
        Check("widened (labels showing): no tooltip on the menu's buttons",
              navButtons.All(b => ((string)tipOf.Invoke(b, new object[] { true })).Length == 0),
              string.Join(" | ", navButtons.Select(b => (string)tipOf.Invoke(b, new object[] { true }))));
        form.SetNavCollapsed(true);
        Check("narrowed (pictures only): each says what it is",
              navButtons.All(b => ((string)tipOf.Invoke(b, new object[] { true })).Length > 0) &&
              navButtons.Any(b => (string)tipOf.Invoke(b, new object[] { true }) == "Home"),
              string.Join(" | ", navButtons.Select(b => (string)tipOf.Invoke(b, new object[] { true }))));
        form.SetNavCollapsed(false);
        form.Close();

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nNAV ICONS ALL PASS" : "\nNAV ICONS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
