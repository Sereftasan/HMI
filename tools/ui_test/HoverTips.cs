using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

// Tooltips of the numeric fields and the lamps (29 September): SHOWN by the
// control itself once the pointer rests on it - after any number of dialogs
// in between - and put down when the pointer leaves or presses. On 30
// September the checkboxes, meters and code cells too (they were silent), and a
// checkbox whose PLC is not answering keeps its tip (it was disabled).
class HT {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static string Last { get { return (string)typeof(UiStyle).GetField("LastShownTip", Any).GetValue(null); } }
    static Control LastOn { get { return (Control)typeof(UiStyle).GetField("LastShownOn", Any).GetValue(null); } }
    static void Reset() { typeof(UiStyle).GetField("LastShownTip", Any).SetValue(null, null); typeof(UiStyle).GetField("LastShownOn", Any).SetValue(null, null); }
    static void Enter(Control c) { typeof(Control).GetMethod("OnMouseEnter", Any).Invoke(c, new object[] { EventArgs.Empty }); }
    static void Move(Control c, int x, int y) { typeof(Control).GetMethod("OnMouseMove", Any).Invoke(c, new object[] { new MouseEventArgs(MouseButtons.None, 0, x, y, 0) }); }
    static void Leave(Control c) { typeof(Control).GetMethod("OnMouseLeave", Any).Invoke(c, new object[] { EventArgs.Empty }); }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new Form { ClientSize = new Size(400, 200) };
        var input = new NumericInputHMI { PlcNumber = 1, Address = "D100", Location = new Point(20, 20) };
        var display = new NumericDisplayHMI { PlcNumber = 1, Address = "D102", Location = new Point(20, 70) };
        var lamp = new LampHMI { PlcNumber = 1, Address = "M5", Location = new Point(220, 20), DescriptionText = "Door" };
        var box = new CheckBoxHMI { PlcNumber = 1, Address = "M7", Location = new Point(220, 70) };
        var meter = new MeterHMI { PlcNumber = 1, Address = "D104", Location = new Point(20, 120), Size = new Size(160, 26) };
        var code = new HexCodeHMI { PlcNumber = 1, Address = "D106", Location = new Point(220, 120) };
        form.ClientSize = new Size(400, 200);
        form.Controls.AddRange(new Control[] { input, display, lamp, box, meter, code });
        form.Show();
        Pump(50);

        foreach (var c in new Control[] { input, display, lamp, box, meter, code })
        {
            Console.WriteLine("--- " + c.GetType().Name + " ---");
            Reset();
            Enter(c); Move(c, 10, 8);
            Pump(300);
            Check("nothing before the pointer has rested a moment", Last == null);
            Pump(1100);
            Check("then the tip, on this control", LastOn == c && !string.IsNullOrEmpty(Last), (Last ?? "").Split('\n').FirstOrDefault());
            string want = c is LampHMI ? "M5" : c == input ? "D100" : c == display ? "D102" : c == box ? "M7" : c == meter ? "D104" : "D106";
            Check("  saying what it is (the address for an admin)", (Last ?? "").Contains(want), want);
            Leave(c);
            Reset();
            Enter(c); Move(c, 10, 8); Pump(1300);
            Check("left and back to the very same point: the tip again", LastOn == c && !string.IsNullOrEmpty(Last));
            Leave(c);
            Reset();
            // A keypad, a message box: a window opened and closed in between.
            using (var dlg = new Form()) { dlg.Show(); Pump(30); dlg.Close(); }
            Pump(30);
            Enter(c); Move(c, 12, 9); Pump(1300);
            Check("after a dialog came and went: the tip again", LastOn == c && !string.IsNullOrEmpty(Last));
            Leave(c);
            Reset();
            Enter(c); Move(c, 12, 9); Pump(400);
            Leave(c); Pump(1000);
            Check("left before the delay: no tip", Last == null);
            Enter(c); Move(c, 12, 9); Pump(400);
            typeof(Control).GetMethod("OnMouseDown", Any).Invoke(c, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 12, 9, 0) });
            Pump(1000);
            Check("pressed before the delay: no tip over the press", Last == null);
            typeof(Control).GetMethod("OnMouseUp", Any).Invoke(c, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 12, 9, 0) });
            Leave(c);
        }

        Console.WriteLine("--- a checkbox whose PLC is not answering ---");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(1);
        PlcManager.Instance.RegisterDriver(1, sim);
        ConnectionMonitor.ReportRead(1, false); ConnectionMonitor.ReportRead(1, false); ConnectionMonitor.ReportRead(1, false);
        Pump(50);
        bool down = ConnectionMonitor.IsKnownDown(1);
        Check("(the controller is known down)", down);
        var scanner = new Kaban.UI.Plc.PlcScanner(form);
        typeof(Kaban.UI.Plc.PlcScanner).GetMethod("ApplyConnectionState", Any).Invoke(scanner, null);
        Check("the scanner leaves it enabled (a disabled control has no tooltip)", box.Enabled);
        Check("  it says it is stale", box.IsStale);
        bool before = box.Checked;
        typeof(Control).GetMethod("OnClick", Any).Invoke(box, new object[] { EventArgs.Empty });
        Check("  a click does not tick it", box.Checked == before);
        Reset();
        Enter(box); Move(box, 10, 8); Pump(1400);
        Check("  its tip says why, then the device", LastOn == box && (Last ?? "").Contains("M7") &&
              (Last ?? "").StartsWith(ConnectionMonitor.ReasonFor(1).Split('\n')[0].Trim()), Last);
        Leave(box);
        ConnectionMonitor.ReportRead(1, true);
        Pump(50);
        Check("answering again: a click ticks it", !box.IsStale);
        scanner.Dispose();

        form.Close();
        Console.WriteLine(fail == 0 ? "\nHOVER TIPS ALL PASS" : "\nHOVER TIPS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
