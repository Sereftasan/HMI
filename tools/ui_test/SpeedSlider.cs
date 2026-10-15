using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Drivers.Simulation;
using Kaban.UI.Controls;

// 14 October: "SpeedOverrideHMI is nothing like I described before, it is just a numerical display with a couple
// of buttons to higher and lower it - it should have a thing to drag left / right to lower and higher the value,
// and it should open the numerical display to set it directly to a specific value (compact as well: the current one
// looks each part disconnected)". One frame: caption, a slider through the old track bar's steps, the percentage.
class SS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Mouse(Control c, string what, Point p) {
        typeof(Control).GetMethod("OnMouse" + what, Any).Invoke(c, new object[] { new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0) });
    }
    static T P<T>(object o, string n) { return (T)o.GetType().GetProperty(n, Any).GetValue(o, null); }

    [STAThread]
    static void Main() {
        AuditLogger.LogDirectory = Path.Combine(Path.GetTempPath(), "ss_" + Guid.NewGuid().ToString("N"));
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        PlcManager.Instance.RegisterDriver(1, new SimulationDriver(1));
        var sim = (SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(1));
        SecurityManager.Login("op", SecurityLevel.Operator);
        var form = new Form { ClientSize = new Size(420, 120), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0), BackColor = Color.White };
        var speed = new SpeedOverrideHMI { Address = "D409", Location = new Point(14, 16), Size = new Size(248, 42) };
        form.Controls.Add(speed);
        var scanner = new Kaban.UI.Plc.PlcScanner(form);
        scanner.RefreshTags();
        sim.Write("D409", 50);
        form.Show(); Pump(700);

        Console.WriteLine("--- one control, drawn as one ---");
        Check("no parts of its own (it was a caption, two buttons and a label)", speed.Controls.Count == 0);
        var track = P<Rectangle>(speed, "TrackArea"); var cell = P<Rectangle>(speed, "ValueArea");
        Check("the caption, then the slider, then the percentage at the right end", track.Left > 30 && track.Right < cell.Left && cell.Right <= speed.Width,
              track + " " + cell);
        Check("read from the PLC: 50%", speed.Current == 50);

        Console.WriteLine("--- dragged ---");
        Func<int, Point> at = v => new Point((int)speed.GetType().GetMethod("XOf", Any).Invoke(speed, new object[] { v }), speed.Height / 2);
        Mouse(speed, "Down", at(50));
        Mouse(speed, "Move", at(80));
        Check("held and moved to 80: shown, not written yet", sim.Read("D409") == 50 && (int?)speed.GetType().GetField("_dragging", Any).GetValue(speed) == 80);
        Mouse(speed, "Move", at(10));
        Mouse(speed, "Up", at(10));
        Check("  let go at 10: 10 written, once", sim.Read("D409") == 10 && speed.Current == 10);
        Mouse(speed, "Down", new Point(track.Right + 3, speed.Height / 2)); Mouse(speed, "Up", new Point(track.Right + 3, speed.Height / 2));
        Check("  tapped at the right end of the track: 100", sim.Read("D409") == 100);
        Mouse(speed, "Down", new Point(track.Left, speed.Height / 2)); Mouse(speed, "Up", new Point(track.Left, speed.Height / 2));
        Check("  at the left end: 0", sim.Read("D409") == 0);
        Check("the steps equally apart: 4 halfway between 2 and 6", Math.Abs((int)speed.GetType().GetMethod("XOf", Any).Invoke(speed, new object[] { 4 }) -
              ((int)speed.GetType().GetMethod("XOf", Any).Invoke(speed, new object[] { 2 }) + (int)speed.GetType().GetMethod("XOf", Any).Invoke(speed, new object[] { 6 })) / 2) <= 1);

        Console.WriteLine("--- the percentage tapped: the keypad ---");
        int asked = -1;
        typeof(SpeedOverrideHMI).GetField("EnterValue", Any).SetValue(null, new Func<SpeedOverrideHMI, int, int?>((s, now) => { asked = now; return 37; }));
        Mouse(speed, "Down", new Point(cell.Left + cell.Width / 2, speed.Height / 2));
        Check("asked with the value now (0); 37 typed: 37 written - any value, not only a step", asked == 0 && sim.Read("D409") == 37 && speed.Current == 37);
        typeof(SpeedOverrideHMI).GetField("EnterValue", Any).SetValue(null, new Func<SpeedOverrideHMI, int, int?>((s, now) => 250));
        Mouse(speed, "Down", new Point(cell.Left + cell.Width / 2, speed.Height / 2));
        Check("  250 typed: kept to the last step, 100", sim.Read("D409") == 100);
        typeof(SpeedOverrideHMI).GetField("EnterValue", Any).SetValue(null, new Func<SpeedOverrideHMI, int, int?>((s, now) => null));
        Mouse(speed, "Down", new Point(cell.Left + cell.Width / 2, speed.Height / 2));
        Check("  cancelled: nothing written", sim.Read("D409") == 100);

        Console.WriteLine("--- the keys, a value set elsewhere ---");
        sim.Write("D409", 37); Pump(700);
        Check("37 set by the PLC: shown as it is; Right takes the next step up (40), Left the one below (30)",
              speed.Current == 37 && speed.NextValue(+1) == 40 && speed.NextValue(-1) == 30);
        typeof(Control).GetMethod("OnKeyDown", Any).Invoke(speed, new object[] { new KeyEventArgs(Keys.Right) });
        Check("  Right: 40 written", sim.Read("D409") == 40);

        Console.WriteLine("--- nobody signed in ---");
        SecurityManager.Logout(); Pump(700);
        Mouse(speed, "Down", at(90)); Mouse(speed, "Up", at(90));
        Check("dragging writes nothing", !speed.CanChange && sim.Read("D409") == 40);
        var tip = (string)typeof(SpeedOverrideHMI).GetMethod("TipText", Any).Invoke(speed, null);
        Check("  its tip says why", tip.Contains("Requires"), tip);
        SecurityManager.Login("op", SecurityLevel.Operator); Pump(700);

        sim.Write("D409", 60); Pump(700);
        if (Environment.GetEnvironmentVariable("SHOT_DIR") != null) {
            Pump(200);
            var o = form.PointToScreen(Point.Empty);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("import", "-window root -crop " + form.ClientSize.Width + "x" +
                form.ClientSize.Height + "+" + o.X + "+" + o.Y + " " + Path.Combine(Environment.GetEnvironmentVariable("SHOT_DIR"), "speed.png")) { UseShellExecute = false }).WaitForExit(10000);
        }
        scanner.Stop();
        Console.WriteLine(fail == 0 ? "\nSPEED SLIDER ALL PASS" : "\nSPEED SLIDER " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
