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

// 17 October: "Cylinder FW/Back" next to "Safety Bypass" (both for Maintenance and Admin, both FALSE when the
// user changes): while TRUE, a cylinder or pneumatic positioning started changes direction every 5 s (23 October: just
// under its alarm delay) until its
// buttons are clicked again or the function is FALSE - with the coupling too. A single valve: its output ON / OFF;
// a double valve: the opposite coil the opposite way.
class FB {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static Kaban.Drivers.Simulation.SimulationDriver plc;
    static int R(string a) { int v; return plc.TryRead(a, out v) ? v : -1; }
    static List<CylinderUnitHMI> units = new List<CylinderUnitHMI>();
    static List<ButtonHMI> extra = new List<ButtonHMI>();
    static DateTime now = new DateTime(2026, 10, 17, 9, 0, 0);
    static void Scan() {
        Func<string, int> lk = a => R(a);
        foreach (var u in units) { u.ButtonA.UpdateState(lk, c => true); u.ButtonB.UpdateState(lk, c => true); u.PositionDisplay.UpdateState(lk, c => true); }
        foreach (var b in extra) b.UpdateState(lk, c => true);
        Pump(20);
    }
    static void Wait(double s) { now = now.AddSeconds(s); UnitCycling.Tick(); Pump(10); }
    static CylinderUnitHMI Cyl(string name, string a, string b, BitActionType action, string la, string lb, int y, CylinderValve valve = CylinderValve.DoubleSolenoid) {
        var u = new CylinderUnitHMI { Name = name, TitleText = name, PlcNumber = 1, Location = new Point(10, y), Size = new Size(360, 64), ValveType = valve };
        if (a != null) { u.CommandAAddress = a; u.CommandAAction = action; }
        if (b != null) { u.CommandBAddress = b; u.CommandBAction = action; }
        if (la != null) u.Lamp1AAddress = la;
        if (lb != null) u.Lamp1BAddress = lb;
        u.ButtonAText = "Back"; u.ButtonBText = "Forward";
        units.Add(u);
        return u;
    }
    static void Down(ButtonHMI b) { typeof(Control).GetMethod("OnMouseDown", Any).Invoke(b, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0) }); }
    static void Up(ButtonHMI b) { typeof(Control).GetMethod("OnMouseUp", Any).Invoke(b, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0) }); }
    static void Press(ButtonHMI b) { Down(b); Up(b); Pump(20); }
    static void Click(ButtonHMI b) { typeof(Button).GetMethod("OnClick", Any).Invoke(b, new object[] { EventArgs.Empty }); Pump(20); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "fb_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        plc = new Kaban.Drivers.Simulation.SimulationDriver(1);
        PlcManager.Instance.RegisterDriver(1, plc);
        SecurityManager.Login("m", SecurityLevel.Maintenance);
        UnitCycling.Now = () => now;
        var form = new Form { ClientSize = new Size(900, 700) };
        var page = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        UnitCycling.MarkManualPage(page);                  // as StandardScreens marks Manual Movement
        var dbl = Cyl("Clamp", "M100", "M101", BitActionType.Momentary, "X0", "X1", 10);
        var spring = Cyl("Ejector", null, "M120", BitActionType.Momentary, "X2", "X3", 80, CylinderValve.SingleSolenoid);
        var c1 = Cyl("Stop 1", "M130", "M131", BitActionType.Momentary, "X4", "X5", 150);
        var c2 = Cyl("Stop 2", "M132", "M133", BitActionType.Momentary, "X6", "X7", 220);
        var set = Cyl("Gate", "M140", "M141", BitActionType.SET, "X8", "X9", 290);
        var axis = new CylinderUnitHMI { Name = "Axis", TitleText = "Axis", PlcNumber = 1, Kind = UnitKind.Axis, Location = new Point(10, 360), Size = new Size(360, 64),
                                         CommandAAddress = "M300", CommandAAction = BitActionType.Momentary, CommandBAddress = "M301", CommandBAction = BitActionType.Momentary,
                                         PositionAddress = "D500" };
        units.Add(axis);
        var fwBack = new ButtonHMI { Name = "BtnCylinderFwBack", ButtonText = "Automatic ON/OFF", HmiFunction = HmiButtonFunction.CylinderForwardBack,
                                     RequiredSecurity = SecurityLevel.Maintenance, VisibilityConditions = SystemCondition.MaintenanceUser, Location = new Point(400, 10), Size = new Size(124, 42) };
        var bypass = new ButtonHMI { Name = "BtnSafetyBypass", ButtonText = "Safety Bypass", ResetOnUserChange = true, Lamp1Address = "M175",
                                     VisibilityConditions = SystemCondition.MaintenanceUser, Location = new Point(400, 60), Size = new Size(109, 42) };
        bypass.BitActions.Add(new BitAction { Action = BitActionType.Alternate, Address = "M175" });
        extra.Add(fwBack); extra.Add(bypass);
        foreach (var u in units) page.Controls.Add(u);
        page.Controls.Add(fwBack); page.Controls.Add(bypass);
        form.Show();
        Pump(300);
        foreach (var kv in new Dictionary<string, int> { { "X0", 1 }, { "X2", 1 }, { "X4", 1 }, { "X6", 1 }, { "X8", 1 } }) plc.Write(kv.Key, kv.Value);
        Scan();

        Console.WriteLine("--- the two buttons: Maintenance and Admin only ---");
        var scanner = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Kaban.UI.Plc.PlcScanner));
        var check = typeof(Kaban.UI.Plc.PlcScanner).GetMethod("CheckGlobalCondition", Any);
        Func<int, Func<string, int>> noData = st => null;
        Func<bool> maint = () => (bool)check.Invoke(scanner, new object[] { SystemCondition.MaintenanceUser, noData });
        Check("MaintenanceUser met for a Maintenance user", maint());
        SecurityManager.Login("a", SecurityLevel.Admin);
        Check("  and an Admin", maint());
        SecurityManager.Login("op", SecurityLevel.Operator);
        Check("  not for an operator", !maint());
        SecurityManager.Logout();
        Check("  nor with nobody signed in", !maint());
        Check("  described as such", MachineConditionMap.Describe(SystemCondition.MaintenanceUser).Contains("Maintenance or Admin"));
        SecurityManager.Login("m", SecurityLevel.Maintenance);
        Scan();

        Console.WriteLine("--- Manual Movement only (17 October) ---");
        Check("its button shown on the Manual Movement page", fwBack.Visible);
        var elsewhere = new Panel { Dock = DockStyle.Right, Width = 150 };
        form.Controls.Add(elsewhere);
        var fwOther = new ButtonHMI { HmiFunction = HmiButtonFunction.CylinderForwardBack, VisibilityConditions = SystemCondition.MaintenanceUser, Size = new Size(70, 50) };
        elsewhere.Controls.Add(fwOther);
        fwOther.UpdateState(a => 0, c => true);
        Check("  the same button on another page (the Axis page's bar, the Axis Monitor): hidden", !fwOther.Visible);
        bool manualShown = true;
        UnitCycling.ManualPageShown = () => manualShown;
        UnitCycling.SetActive(true);
        manualShown = false;
        UnitCycling.CheckPage();
        Check("  on, then Manual Movement closed: off", !UnitCycling.Active);
        manualShown = true;

        Console.WriteLine("--- Cylinder FW/Back on and off ---");
        Check("off at the start, its lamp off", !UnitCycling.Active && !(bool)typeof(ButtonHMI).GetField("_lamp1On", Any).GetValue(fwBack));
        Click(fwBack);
        Check("clicked: on, its lamp on", UnitCycling.Active && (bool)typeof(ButtonHMI).GetField("_lamp1On", Any).GetValue(fwBack));
        Click(fwBack);
        Check("clicked again: off", !UnitCycling.Active);
        Click(fwBack);
        plc.Write("M175", 1);
        SecurityManager.Login("m2", SecurityLevel.Maintenance);
        Pump(50);
        Check("another user signs in: Cylinder FW/Back FALSE, and Safety Bypass's M175 written 0", !UnitCycling.Active && R("M175") == 0, "M175=" + R("M175"));
        SecurityManager.Login("op", SecurityLevel.Operator);
        UnitCycling.SetActive(true);
        Check("an operator cannot switch it on", !UnitCycling.Active);
        SecurityManager.Login("m", SecurityLevel.Maintenance);
        Scan();
        UnitCycling.SetActive(true);

        Console.WriteLine("--- a double valve: one coil ON, the other OFF, swapped every 5 s ---");
        Press(dbl.ButtonB);
        Check("Forward pressed and let go: Forward held ON (not just while pressed), Back OFF", R("M101") == 1 && R("M100") == 0, "M100=" + R("M100") + " M101=" + R("M101"));
        Check("  moving back and forth", UnitCycling.IsCycling(dbl) && UnitCycling.SideOf(dbl) == false);
        Wait(4.0);
        Check("4 s: still Forward", R("M101") == 1 && R("M100") == 0);
        Wait(1.1);
        Check("5 s: Back ON, Forward OFF", R("M100") == 1 && R("M101") == 0, "M100=" + R("M100") + " M101=" + R("M101"));
        Wait(5.1);
        Check("10 s: Forward again", R("M101") == 1 && R("M100") == 0);
        Press(dbl.ButtonA);
        Check("a button of it pressed again: stopped, both OFF - the press itself moved nothing", !UnitCycling.IsCycling(dbl) && R("M100") == 0 && R("M101") == 0,
              "M100=" + R("M100") + " M101=" + R("M101"));
        Wait(6);
        Check("  and stays stopped", R("M100") == 0 && R("M101") == 0);

        Console.WriteLine("--- a single valve: its output ON / OFF ---");
        Press(spring.ButtonB);
        Check("Forward: its coil ON", R("M120") == 1);
        Wait(5.1);
        Check("5 s: the coil OFF (the spring brings it back)", R("M120") == 0 && UnitCycling.IsCycling(spring));
        Wait(5.1);
        Check("10 s: ON again", R("M120") == 1);
        Click(fwBack);
        Check("Cylinder FW/Back switched off: stopped, the coil OFF", !UnitCycling.Active && !UnitCycling.IsCycling(spring) && R("M120") == 0);
        Press(spring.ButtonB);
        Pump(20);
        Check("  off: the button is a plain momentary one again (ON only while pressed)", R("M120") == 0);
        Click(fwBack);

        Console.WriteLine("--- coupled units move back and forth together ---");
        Check("Stop 1 and Stop 2 coupled", UnitCoupling.Toggle(c1) == null && UnitCoupling.Toggle(c2) == null);
        Press(c1.ButtonB);
        Check("Stop 1's Forward: both Forward", R("M131") == 1 && R("M133") == 1 && R("M130") == 0 && R("M132") == 0);
        Check("  both moving", UnitCycling.IsCycling(c1) && UnitCycling.IsCycling(c2));
        Wait(5.1);
        Check("5 s: both Back", R("M130") == 1 && R("M132") == 1 && R("M131") == 0 && R("M133") == 0);
        Press(c2.ButtonA);
        Check("Stop 2 pressed: both stopped, all OFF", !UnitCycling.IsCycling(c1) && !UnitCycling.IsCycling(c2) &&
              R("M130") + R("M131") + R("M132") + R("M133") == 0);
        UnitCoupling.Clear();

        Console.WriteLine("--- a click command (SET) ---");
        Click(set.ButtonB);
        Check("Forward clicked: its SET written", R("M141") == 1 && UnitCycling.IsCycling(set));
        Wait(5.1);
        Check("5 s: Back's SET written", R("M140") == 1);
        Click(set.ButtonA);
        Check("clicked again: stopped", !UnitCycling.IsCycling(set));

        Console.WriteLine("--- an axis is not one ---");
        Down(axis.ButtonB);
        Check("an axis jogs as ever while pressed", R("M301") == 1 && !UnitCycling.IsCycling(axis));
        Up(axis.ButtonB);
        Check("  and stops when let go", R("M301") == 0);

        Console.WriteLine("--- stopped when it leaves the screen, or the user changes ---");
        Press(dbl.ButtonB);
        dbl.Visible = false;
        Pump(30);
        Check("hidden (another tab): stopped, both OFF", !UnitCycling.IsCycling(dbl) && R("M101") == 0 && R("M100") == 0);
        dbl.Visible = true;
        Scan();
        Press(dbl.ButtonB);
        Check("moving again", UnitCycling.IsCycling(dbl) && R("M101") == 1);
        SecurityManager.Login("a", SecurityLevel.Admin);
        Pump(30);
        Check("another user: off, stopped, both OFF", !UnitCycling.Active && !UnitCycling.IsCycling(dbl) && R("M101") == 0 && R("M100") == 0);

        // 23 October: "Cylinder FW/Back function direction changing time delay should be calculated according to the
        // selected control's alarm delays instead of fixed 5 second (0.5 second less than minimum value of each active
        // control's Cylinder delay (s) value if entered for the selected direction errors and Cylinder alarm delay (s))"
        Console.WriteLine("--- the time each way: just under the alarm delays ---");
        AlarmGeneration.SettingsPath = Path.Combine(dir, "AlarmGeneration.cfg");
        AlarmGeneration.Save(AlarmGeneration.ModuleOfStation(1), new AlarmGeneration.Settings { Enabled = true, DelaySeconds = 3, AxisAlarms = true });
        dbl.ErrorAAddress = "M840"; dbl.ErrorBAddress = "M841";
        AlarmManager.RegisterDefinition("T.ClampBack", "Clamp did not go back", AlarmSeverity.Warning, plcNumber: 1, address: "M840");
        AlarmManager.FindByCode("T.ClampBack").GenerationDelay = 2;
        Check("a unit's way with a Cylinder delay of its own (2 s): 1.5 s; the other way, the Cylinder alarm delay's (3 s): 2.5 s",
              UnitCycling.PeriodFor(new[] { dbl }, true) == 1500 && UnitCycling.PeriodFor(new[] { dbl }, false) == 2500,
              UnitCycling.PeriodFor(new[] { dbl }, true) + " / " + UnitCycling.PeriodFor(new[] { dbl }, false));
        Check("  units moving together: the shortest of them", UnitCycling.PeriodFor(new[] { c1, dbl }, true) == 1500 && UnitCycling.PeriodFor(new[] { c1, dbl }, false) == 2500 &&
              UnitCycling.PeriodFor(new[] { c1 }, true) == 2500);
        AlarmManager.FindByCode("T.ClampBack").GenerationDelay = 0.6;
        Check("  a delay under a second: never under half a second", UnitCycling.PeriodFor(new[] { dbl }, true) == UnitCycling.ShortestMs);
        AlarmManager.FindByCode("T.ClampBack").GenerationDelay = 2;
        UnitCycling.SetActive(true);
        Scan();
        Press(dbl.ButtonA);
        Check("started: Back", UnitCycling.IsCycling(dbl) && R("M100") == 1 && R("M101") == 0);
        Wait(1.4);
        Check("  1.4 s: still Back", R("M100") == 1);
        Wait(0.2);
        Check("  1.6 s: Forward - before Back's alarm (2 s)", R("M101") == 1 && R("M100") == 0);
        Wait(2.4);
        Check("  2.4 s later: still Forward", R("M101") == 1);
        Wait(0.2);
        Check("  2.6 s: Back - before Forward's alarm (3 s)", R("M100") == 1 && R("M101") == 0);
        Press(dbl.ButtonA);
        UnitCycling.SetActive(false);

        var log = string.Join("\n", Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Check("in the log: switched on / off, the movement, stopped", log.Contains("Automatic ON/OFF (a cylinder back and forth) switched on") && log.Contains("back and forth, 4.5 s") &&
              log.Contains("back and forth, 1.5 s 'Back' and 2.5 s back (the alarm delays)") &&
              log.Contains("back and forth stopped") && log.Contains("Automatic ON/OFF (a cylinder back and forth) switched off"));
        form.Close();
        Console.WriteLine(fail == 0 ? "\nCYLINDER FW/BACK ALL PASS" : "\nCYLINDER FW/BACK " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
