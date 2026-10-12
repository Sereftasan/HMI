using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Drivers.Simulation;
using Kaban.UI.Controls;

// 12 October: "upgrade FA2010 home page from _FA2010 and H500 home ... (Don't work on image and table kind
// controls on Home pages, I need you to take just Start, Pause, Cancel buttons and speed settings from home
// pages, nothing else)". FA2010: the old main control panel's Start (M21), Pause (M111), Cancel (M24, asked
// while the machine works) and speed (D408, the track bar's steps).
class HC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }
    static void Click(ButtonHMI b) { typeof(ButtonHMI).GetMethod("OnClick", Any).Invoke(b, new object[] { EventArgs.Empty }); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "hc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        PlcManager.Instance.RegisterDriver(1, new SimulationDriver(1));
        var sim = (SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(1));
        SecurityManager.Login("op", SecurityLevel.Operator);
        var asked = new List<string>();
        DialogResult answer = DialogResult.No;
        typeof(ButtonHMI).GetField("AskConfirmation", Any).SetValue(null, new Func<string, DialogResult>(q => { asked.Add(q); return answer; }));

        Console.WriteLine("--- FA2010's home: the old main control panel's buttons and speed ---");
        var form = new Form { ClientSize = new Size(900, 200), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var home = new Kaban.Machines.FA2010.Screens.HomeScreen { Dock = DockStyle.Fill };
        form.Controls.Add(home);
        var scanner = new Kaban.UI.Plc.PlcScanner(home);
        scanner.RefreshTags();
        form.Show(); Pump(400);
        var buttons = Tree(home).OfType<ButtonHMI>().ToList();
        var speed = Tree(home).OfType<SpeedOverrideHMI>().FirstOrDefault();
        Check("Start, Pause, Cancel and the speed - nothing else (the demo controls are gone)",
              string.Join(",", buttons.Select(b => b.FallbackText)) == "Start,Pause,Cancel" && speed != null &&
              Tree(home).Count(c => c is IHmiControl) == 4, string.Join(",", Tree(home).Select(c => c.GetType().Name)));
        var start = buttons[0]; var pause = buttons[1]; var cancel = buttons[2];
        sim.Write("M21", 0); sim.Write("M111", 0); sim.Write("M24", 0); sim.Write("D408", 50);
        Pump(600);
        Click(start); Pump(100);
        Check("Start sets M21 (the machine works), lit by it", sim.Read("M21") == 1 && start.Lamp1Address == "M21");
        Click(pause); Pump(100);
        Check("Pause M111 alternate: on", sim.Read("M111") == 1);
        Click(pause); Pump(100);
        Check("  and off again", sim.Read("M111") == 0);
        Pump(500);
        answer = DialogResult.No;
        Click(cancel); Pump(100);
        Check("Cancel while the machine works (M21 on): asked first - No, nothing written", asked.Count == 1 && sim.Read("M24") == 0,
              string.Join(" | ", asked));
        answer = DialogResult.Yes;
        Click(cancel); Pump(100);
        Check("  Yes: M24 set (the old Constants.MachineCancel_Device)", asked.Count == 2 && sim.Read("M24") == 1);
        sim.Write("M24", 0); sim.Write("M21", 0);
        Pump(600);
        asked.Clear();
        Click(cancel); Pump(100);
        Check("Cancel while it does not work: at once, nothing asked", asked.Count == 0 && sim.Read("M24") == 1);

        Console.WriteLine("--- the speed: D408 in the old track bar's steps ---");
        Check("read from the PLC: 50%", speed.Current == 50, speed.Current + "");
        Check("+ : 60", speed.Step(+1) && sim.Read("D408") == 60);
        Check("- - : 40", speed.Step(-1) && speed.Step(-1) && sim.Read("D408") == 40);
        sim.Write("D408", 3); Pump(600);
        Check("a value set elsewhere (3%) shown as it is, + takes the next step up (4), - the one below (2)",
              speed.Current == 3 && speed.NextValue(+1) == 4 && speed.NextValue(-1) == 2);
        sim.Write("D408", 100); Pump(600);
        Check("at 100: + writes nothing", !speed.Step(+1) && sim.Read("D408") == 100);
        sim.Write("D408", 0); Pump(600);
        Check("at 0: - writes nothing; + : 1", !speed.Step(-1) && speed.Step(+1) && sim.Read("D408") == 1);
        SecurityManager.Logout(); Pump(600);
        Check("nobody signed in: the speed is not changed", !speed.Step(+1) && sim.Read("D408") == 1);
        SecurityManager.Login("op", SecurityLevel.Operator); Pump(600);

        scanner.Stop();
        form.Close();
        Console.WriteLine(fail == 0 ? "\nHOME CYCLE ALL PASS" : "\nHOME CYCLE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
