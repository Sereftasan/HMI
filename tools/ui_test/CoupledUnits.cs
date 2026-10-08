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
// clicked at the same time))".
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
        var form = new Form { ClientSize = new Size(800, 560) };
        var page = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        var c1 = Cyl("Clamp 1", 0, BitActionType.Momentary, 10, 10);
        var c2 = Cyl("Clamp 2", 1, BitActionType.Momentary, 10, 80);
        var c3 = Cyl("Clamp 3", 2, BitActionType.Momentary, 10, 150);
        var s1 = Cyl("Stop 1", 3, BitActionType.SET, 400, 10);
        var s2 = Cyl("Stop 2", 4, BitActionType.SET, 400, 80);
        var a1 = Axis("Axis 1", 0, 10, 240);
        var a2 = Axis("Axis 2", 1, 10, 310);
        var a3 = Axis("Axis 3", 2, 10, 380);
        foreach (var u in units) page.Controls.Add(u);
        form.Show();
        Pump(300);
        // the clamps 1 and 2 at their back end, 3 forward; the stops at the back
        foreach (var kv in new Dictionary<string, int> { { "X0", 1 }, { "X1", 0 }, { "X2", 1 }, { "X3", 0 }, { "X4", 0 }, { "X5", 1 }, { "X6", 1 }, { "X7", 0 }, { "X8", 1 }, { "X9", 0 },
                                                         { "D500", 1000 }, { "D501", 1003 }, { "D502", 1200 } })
            plc.Write(kv.Key, kv.Value);
        Scan();

        Console.WriteLine("--- the chain ---");
        Check("on every unit with a movement, on its frame's top line", units.All(u => Chain(u).Visible && Chain(u).Top < 12 && Chain(u).Right <= u.Width),
              string.Join(",", units.Select(u => Chain(u).Bounds.ToString())));
        Check("  open while uncoupled", units.All(u => !Chain(u).On));

        Console.WriteLine("--- coupling ---");
        Check("Clamp 1 coupled: the chain closed, the frame blue", UnitCoupling.Toggle(c1) == null && Chain(c1).On &&
              Frame(c1) == (Color)typeof(CylinderUnitHMI).GetField("CoupledFrame", Any).GetValue(null));
        var why = UnitCoupling.Toggle(c3);
        Check("Clamp 3 refused: it is at its other end", why != null && why.Contains("Back") && !UnitCoupling.IsCoupled(c3), why);
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

        Console.WriteLine("--- axes ---");
        Check("Axis 2 at 100.3 joins Axis 1 at 100.0 (within 0.5)", UnitCoupling.Toggle(a2) == null && UnitCoupling.Coupled(UnitKind.Axis).Count == 2);
        why = UnitCoupling.Toggle(a3);
        Check("Axis 3 at 120 refused", why != null && !UnitCoupling.IsCoupled(a3), why);
        Down(a2.ButtonB);
        Check("a jog of Axis 2: Axis 1 jogs too", R("M303") == 1 && R("M301") == 1);
        Up(a2.ButtonB);
        Check("  both stop", R("M303") == 0 && R("M301") == 0);

        Console.WriteLine("--- rights, sign-out ---");
        SecurityManager.Logout();
        Pump(50);
        Check("signed out: every coupling let go", UnitCoupling.Coupled(UnitKind.Axis).Count == 0 && UnitCoupling.Coupled(UnitKind.Cylinder).Count == 0);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        Check("the texts in every language", new[] { "Tip_Couple", "Tip_Coupled", "Cpl_NoCommands", "Cpl_NoPosition", "Cpl_NoEnd", "Cpl_NotSame", "Cpl_LetGo", "Cpl_Between", "Cpl_Gone", "Cpl_Moved", "Cpl_CannotAct", "Cpl_OnlyThis", "Cpl_LeftScreen" }
              .All(k => new[] { "EN", "TR", "RU", "DE", "IT", "ES" }.All(l => !string.IsNullOrEmpty(LanguageManager.GetStringIn(l, k)))));
        var log = string.Join("\n", Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Check("in the log: coupled, the coupled presses, let go", log.Contains("coupled 'Clamp 1'") && log.Contains("coupled press of") && log.Contains("let go"));
        form.Close();
        Console.WriteLine(fail == 0 ? "\nCOUPLED UNITS ALL PASS" : "\nCOUPLED UNITS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
