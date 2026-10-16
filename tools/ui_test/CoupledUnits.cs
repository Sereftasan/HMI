using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// 8 October: "clatch multiple same type of controls (cylinder or axis) together for manual movement
// (selecting them to clatch first (they have to be at the same position to be clatched, cylinder
// controls all at the back or all at the forward or axis controls are all at the same position), then
// clicking any one of them activates all of those controls same side buttons (just like all of them
// clicked at the same time))". 17 October: a chain that cannot join is disabled; axes only in step
// mode and on one page; positioning cylinders by their feedback type; Maintenance users only.
class CU {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static Kaban.Drivers.Simulation.SimulationDriver plc;
    static int R(string a) { int v; return plc.TryRead(a, out v) ? v : -1; }
    static List<CylinderUnitHMI> units = new List<CylinderUnitHMI>();
    static void Scan() {
        Func<string, int> lk = a => R(a);
        foreach (var u in units) {
            u.ButtonA.UpdateState(lk, c => true);
            u.ButtonB.UpdateState(lk, c => true);
            u.PositionDisplay.UpdateState(lk, c => true);
        }
        Pump(30);
    }
    static CylinderUnitHMI Cyl(string name, int n, BitActionType action, int x, int y) {
        var u = new CylinderUnitHMI { Name = name, TitleText = name, PlcNumber = 1, Location = new Point(x, y), Size = new Size(360, 64) };
        u.CommandAAddress = "M" + (100 + 2 * n); u.CommandAAction = action;
        u.CommandBAddress = "M" + (101 + 2 * n); u.CommandBAction = action;
        u.Lamp1AAddress = "X" + (2 * n); u.Lamp1BAddress = "X" + (2 * n + 1);
        u.ButtonAText = "Back"; u.ButtonBText = "Forward";
        units.Add(u);
        return u;
    }
    static CylinderUnitHMI Axis(string name, int n, int x, int y) {
        var u = new CylinderUnitHMI { Name = name, TitleText = name, PlcNumber = 1, Kind = UnitKind.Axis, Location = new Point(x, y), Size = new Size(360, 64) };
        u.CommandAAddress = "M" + (300 + 2 * n); u.CommandAAction = BitActionType.Momentary;
        u.CommandBAddress = "M" + (301 + 2 * n); u.CommandBAction = BitActionType.Momentary;
        u.PositionAddress = "D" + (500 + n); u.PositionDivisor = 10; u.PositionDecimalPlaces = 1;
        units.Add(u);
        return u;
    }
    static CylinderUnitHMI Pos(string name, int n, CylinderFeedback fb, int x, int y) {
        var u = new CylinderUnitHMI { Name = name, TitleText = name, PlcNumber = 1, Location = new Point(x, y), Size = new Size(360, 64) };
        u.CommandAAddress = "M" + (400 + 2 * n); u.CommandAAction = BitActionType.Momentary;
        u.CommandBAddress = "M" + (401 + 2 * n); u.CommandBAction = BitActionType.Momentary;
        u.FeedbackType = fb; u.ValveType = CylinderValve.ThreeWayClosedCenter;
        u.PositionAddress = "D" + (600 + n); u.PositionDivisor = 10; u.PositionDecimalPlaces = 1;
        units.Add(u);
        return u;
    }
    static CoupleBadge Chain(CylinderUnitHMI u) { return (CoupleBadge)typeof(CylinderUnitHMI).GetProperty("CoupleChain", Any).GetValue(u, null); }
    static string Notice(CylinderUnitHMI u) { return (string)typeof(CylinderUnitHMI).GetProperty("LastCouplingNotice", Any).GetValue(u, null); }
    static Color Frame(CylinderUnitHMI u) { var g = typeof(CylinderUnitHMI).GetField("_grp", Any).GetValue(u); return (Color)g.GetType().GetProperty("BorderColor").GetValue(g, null); }
    static void Down(ButtonHMI b) { typeof(Control).GetMethod("OnMouseDown", Any).Invoke(b, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0) }); }
    static void Up(ButtonHMI b) { typeof(Control).GetMethod("OnMouseUp", Any).Invoke(b, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0) }); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "cu_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        plc = new Kaban.Drivers.Simulation.SimulationDriver(1);
        PlcManager.Instance.RegisterDriver(1, plc);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        bool? step = true;
        UnitCoupling.AxesInStepMode = () => step;
        var form = new Form { ClientSize = new Size(1200, 640) };
        var page = new Panel { Dock = DockStyle.Fill };
        var other = new Panel { Dock = DockStyle.Right, Width = 380 };       // another page beside it
        form.Controls.Add(page);
        form.Controls.Add(other);
        var c1 = Cyl("Clamp 1", 0, BitActionType.Momentary, 10, 10);
        var c2 = Cyl("Clamp 2", 1, BitActionType.Momentary, 10, 80);
        var c3 = Cyl("Clamp 3", 2, BitActionType.Momentary, 10, 150);
        var s1 = Cyl("Stop 1", 3, BitActionType.SET, 400, 10);
        var s2 = Cyl("Stop 2", 4, BitActionType.SET, 400, 80);
        var a1 = Axis("Axis 1", 0, 10, 240);
        var a2 = Axis("Axis 2", 1, 10, 310);
        var a3 = Axis("Axis 3", 2, 10, 380);
        var a4 = Axis("Axis 4", 3, 10, 10);
        var p1 = Pos("Positioning 1", 0, CylinderFeedback.EncoderPlusReference, 400, 240);
        var p2 = Pos("Positioning 2", 1, CylinderFeedback.EncoderPlusReference, 400, 310);
        var p3 = Pos("Positioning 3", 2, CylinderFeedback.AnalogueMagnetic, 400, 380);
        foreach (var u in units) (u == a4 ? other : page).Controls.Add(u);
        form.Show();
        Pump(300);
        // the clamps 1 and 2 at their back end, 3 forward; the stops at the back; the axes and positionings apart
        foreach (var kv in new Dictionary<string, int> { { "X0", 1 }, { "X1", 0 }, { "X2", 1 }, { "X3", 0 }, { "X4", 0 }, { "X5", 1 }, { "X6", 1 }, { "X7", 0 }, { "X8", 1 }, { "X9", 0 },
                                                         { "D500", 1000 }, { "D501", 1003 }, { "D502", 1200 }, { "D503", 50 }, { "D600", 120 }, { "D601", 3400 }, { "D602", 900 } })
            plc.Write(kv.Key, kv.Value);
        Scan();
        Action avail = () => typeof(UnitCoupling).GetMethod("RefreshAvailability", Any).Invoke(null, null);
        Func<CylinderUnitHMI[], string> faded = list => string.Join(",", list.Where(u => !Chain(u).Available).Select(u => u.Name));

        Console.WriteLine("--- the chain ---");
        Check("on every unit with a movement, on its frame's top line", units.All(u => Chain(u).Visible && Chain(u).Top < 12 && Chain(u).Right <= u.Width),
              string.Join(",", units.Select(u => Chain(u).Bounds.ToString())));
        Check("  open while uncoupled", units.All(u => !Chain(u).On));
        Check("  at the right end of the line, where it stays (16 October: the \"?\" left of it)",
              units.All(u => Chain(u).Right == u.ClientSize.Width - 12), string.Join(",", units.Select(u => Chain(u).Right + "/" + u.ClientSize.Width)));
        avail();
        Check("  nothing coupled: every chain can be clicked (the axes in step mode)", units.All(u => Chain(u).Available), faded(units.ToArray()));

        Console.WriteLine("--- coupling: the ones that do not match are disabled ---");
        Check("Clamp 1 coupled: the chain closed, the frame blue", UnitCoupling.Toggle(c1) == null && Chain(c1).On &&
              Frame(c1) == (Color)typeof(CylinderUnitHMI).GetField("CoupledFrame", Any).GetValue(null));
        avail();
        Check("  Clamp 3 (forward) and the positionings (no end sensors) faded; Clamp 2, the stops (at the back) and the axes (their own kind) not",
              !Chain(c3).Available && !Chain(p1).Available && Chain(c2).Available && Chain(s1).Available && Chain(s2).Available && Chain(c1).Available && Chain(a1).Available,
              faded(units.ToArray()));
        var why = UnitCoupling.Toggle(c3);
        Check("  Clamp 3 refused, and why: not where the coupled ones are", why != null && why.Contains("Back") && !UnitCoupling.IsCoupled(c3), why);
        Check("  its tip says so too", UnitCoupling.WhyNot(c3) != null);
        Check("Clamp 2 coupled (at the back as Clamp 1)", UnitCoupling.Toggle(c2) == null && UnitCoupling.Coupled(UnitKind.Cylinder).Count == 2);
        Check("  an axis is not coupled with cylinders: its own group", UnitCoupling.Toggle(a1) == null && UnitCoupling.Coupled(UnitKind.Cylinder).Count == 2 &&
              UnitCoupling.Coupled(UnitKind.Axis).Count == 1);

        Console.WriteLine("--- a press on one: both ---");
        Down(c1.ButtonB);
        Pump(50);
        Check("Clamp 1's Forward held: Clamp 2's Forward too", R("M101") == 1 && R("M103") == 1 && R("M105") == 0, "M101=" + R("M101") + " M103=" + R("M103"));
        Up(c1.ButtonB);
        Pump(50);
        Check("  released: both released", R("M101") == 0 && R("M103") == 0);
        Down(c2.ButtonA);
        Check("pressed on Clamp 2 (Back): Clamp 1's Back too", R("M100") == 1 && R("M102") == 1);
        typeof(Control).GetMethod("OnMouseCaptureChanged", Any).Invoke(c2.ButtonA, new object[] { EventArgs.Empty });
        Check("  the press lost (another window took the mouse): both released", R("M100") == 0 && R("M102") == 0);

        Console.WriteLine("--- three or more coupled: this one or all of them ---");
        Func<CylinderUnitHMI, ContextMenuStrip> menuOf = u => (ContextMenuStrip)typeof(CylinderUnitHMI).GetMethod("UncoupleMenu", Any).Invoke(u, null);
        Check("Stop 1 joins the clamps (at the back too): three coupled", UnitCoupling.Toggle(s1) == null && UnitCoupling.Coupled(UnitKind.Cylinder).Count == 3);
        var m3 = menuOf(s1);
        Check("  its chain offers: uncouple 'Stop 1', uncouple all 3",
              m3.Items.Count == 2 && m3.Items[0].Text.Contains("Stop 1") && m3.Items[1].Text.Contains("all 3"), string.Join(" | ", m3.Items.Cast<ToolStripItem>().Select(i => i.Text)));
        m3.Items["uncoupleThis"].PerformClick();
        Check("  'Stop 1' only: out, the clamps still coupled", !UnitCoupling.IsCoupled(s1) && UnitCoupling.IsCoupled(c1) && UnitCoupling.IsCoupled(c2));
        UnitCoupling.Toggle(s1); UnitCoupling.Toggle(s2);
        Check("all four coupled", UnitCoupling.Coupled(UnitKind.Cylinder).Count == 4);
        menuOf(c2).Items["uncoupleAll"].PerformClick();
        avail();
        Check("  'Uncouple all 4' from any chain: none coupled, every chain open, Clamp 3 can be clicked again",
              UnitCoupling.Coupled(UnitKind.Cylinder).Count == 0 && new[] { c1, c2, s1, s2 }.All(u => !Chain(u).On) && Chain(c3).Available);
        Check("  the axes' coupling not touched", UnitCoupling.IsCoupled(a1));
        UnitCoupling.Toggle(c1); UnitCoupling.Toggle(c2);

        Console.WriteLine("--- one not where the other is ---");
        plc.Write("X2", 0); plc.Write("X3", 1);                    // Clamp 2 forward now
        Scan();
        Down(c1.ButtonB);
        Pump(50);
        Check("Clamp 1 pressed: the coupling let go, said so; Clamp 1 alone moves", R("M101") == 1 && R("M103") == 0 && !UnitCoupling.IsCoupled(c1) && !UnitCoupling.IsCoupled(c2) &&
              Notice(c1) != null && Notice(c1).Contains("Clamp 2"), Notice(c1));
        Up(c1.ButtonB);
        Check("  released", R("M101") == 0);
        Check("  the chains open, the frames as before", !Chain(c1).On && !Chain(c2).On);

        Console.WriteLine("--- a click (a SET command) ---");
        Check("the stops coupled", UnitCoupling.Toggle(s1) == null && UnitCoupling.Toggle(s2) == null);
        typeof(Button).GetMethod("OnClick", Any).Invoke(s2.ButtonB, new object[] { EventArgs.Empty });
        Pump(50);
        Check("Stop 2's Forward clicked: Stop 1's Forward set too", R("M109") == 1 && R("M107") == 1 && R("M106") == 0, "M107=" + R("M107") + " M109=" + R("M109"));

        Console.WriteLine("--- a unit leaving the screen ---");
        s2.Visible = false;
        Pump(50);
        Check("Stop 2 hidden (another tab): out, and Stop 1 alone is no coupling", !UnitCoupling.IsCoupled(s2) && !UnitCoupling.IsCoupled(s1));
        s2.Visible = true;

        Console.WriteLine("--- pneumatic positioning: by the feedback type, wherever they stand ---");
        avail();
        Check("nothing of theirs coupled: every positioning can be clicked", Chain(p1).Available && Chain(p2).Available && Chain(p3).Available);
        Check("Positioning 1 (encoder, at 12.0) and Positioning 2 (encoder, at 340.0) coupled", UnitCoupling.Toggle(p1) == null && UnitCoupling.Toggle(p2) == null &&
              UnitCoupling.IsCoupled(p1) && UnitCoupling.IsCoupled(p2));
        avail();
        why = UnitCoupling.Toggle(p3);
        Check("  Positioning 3 (an analogue sensor): faded and refused - another feedback type", !Chain(p3).Available && why != null && why.Contains("AnalogueMagnetic") &&
              !UnitCoupling.IsCoupled(p3), why);
        Check("  the clamps (end sensors) do not match them either", !Chain(c1).Available && !Chain(c3).Available, faded(new[] { c1, c3 }));
        Down(p2.ButtonA);
        Pump(50);
        Check("Positioning 2 pressed: Positioning 1 moves too", R("M402") == 1 && R("M400") == 1);
        Up(p2.ButtonA);
        Check("  both stop", R("M402") == 0 && R("M400") == 0);
        UnitCoupling.Toggle(p1);
        Check("Positioning 1 uncoupled: Positioning 2 alone is no coupling", !UnitCoupling.IsCoupled(p1) && !UnitCoupling.IsCoupled(p2));

        Console.WriteLine("--- axes: in step mode, on one page ---");
        avail();
        Check("Axis 1 coupled: Axis 2 and 3 can join wherever they stand (100.3 / 120.0); Axis 4 on the other page faded",
              Chain(a2).Available && Chain(a3).Available && !Chain(a4).Available, faded(new[] { a2, a3, a4 }));
        why = UnitCoupling.Toggle(a4);
        Check("  Axis 4 refused: not on the coupled axes' page", why != null && why.Contains("page") && !UnitCoupling.IsCoupled(a4), why);
        Check("Axis 2 and Axis 3 join", UnitCoupling.Toggle(a2) == null && UnitCoupling.Toggle(a3) == null && UnitCoupling.Coupled(UnitKind.Axis).Count == 3);
        Down(a2.ButtonB);
        Check("a step of Axis 2: Axis 1 and 3 step too", R("M303") == 1 && R("M301") == 1 && R("M305") == 1);
        Up(a2.ButtonB);
        Check("  all stop", R("M303") == 0 && R("M301") == 0 && R("M305") == 0);
        step = false;
        Down(a2.ButtonB);
        Pump(50);
        Check("JOG chosen meanwhile: the next press lets the coupling go, said so; Axis 2 alone moves",
              R("M303") == 1 && R("M301") == 0 && UnitCoupling.Coupled(UnitKind.Axis).Count == 0 && Notice(a2) != null && Notice(a2).Contains("step"), Notice(a2));
        Up(a2.ButtonB);
        avail();
        Check("  in JOG every axis chain is faded, and a click says why", new[] { a1, a2, a3, a4 }.All(u => !Chain(u).Available) &&
              UnitCoupling.Toggle(a1) != null && UnitCoupling.Toggle(a1).Contains("JOG"), faded(new[] { a1, a2, a3, a4 }));
        step = null;
        Check("  a machine with no step mode rule: axes are not coupled", UnitCoupling.Toggle(a1) != null);
        step = true;
        avail();
        Check("  step mode again: the axes can be clicked", new[] { a1, a2, a3, a4 }.All(u => Chain(u).Available));

        Console.WriteLine("--- rights: Maintenance and Admin only ---");
        Check("coupled now: Axis 1 and 2", UnitCoupling.Toggle(a1) == null && UnitCoupling.Toggle(a2) == null);
        SecurityManager.Login("op", SecurityLevel.Operator);
        Pump(80);
        Check("an operator signed in: every coupling let go, no chain shown", UnitCoupling.All.Count == 0 && units.All(u => !Chain(u).Visible),
              string.Join(",", units.Where(u => Chain(u).Visible).Select(u => u.Name)));
        Check("  and none can be made", UnitCoupling.Toggle(c1) != null && UnitCoupling.Toggle(c1).Contains("Maintenance"));
        SecurityManager.Login("m", SecurityLevel.Maintenance);
        Pump(80);
        Check("a Maintenance user: the chains back", units.All(u => Chain(u).Visible));
        Check("  and coupling works", UnitCoupling.Toggle(c3) == null);
        SecurityManager.Logout();
        Pump(50);
        Check("signed out: every coupling let go", UnitCoupling.All.Count == 0);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var log = string.Join("\n", Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Check("in the log: coupled, the coupled presses, let go", log.Contains("coupled 'Clamp 1'") && log.Contains("coupled press of") && log.Contains("let go"));
        form.Close();
        Console.WriteLine(fail == 0 ? "\nCOUPLED UNITS ALL PASS" : "\nCOUPLED UNITS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
