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
// in between - and put down when the pointer leaves or presses.
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
        form.Controls.AddRange(new Control[] { input, display, lamp });
        form.Show();
        Pump(50);

        foreach (var c in new Control[] { input, display, lamp })
        {
            Console.WriteLine("--- " + c.GetType().Name + " ---");
            Reset();
            Enter(c); Move(c, 10, 8);
            Pump(300);
            Check("nothing before the pointer has rested a moment", Last == null);
            Pump(1100);
            Check("then the tip, on this control", LastOn == c && !string.IsNullOrEmpty(Last), (Last ?? "").Split('\n').FirstOrDefault());
            Check("  saying what it is (the address for an admin)", (Last ?? "").Contains(c is LampHMI ? "M5" : c == input ? "D100" : "D102"));
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

        form.Close();
        Console.WriteLine(fail == 0 ? "\nHOVER TIPS ALL PASS" : "\nHOVER TIPS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
