using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Forms;

// One check per UI fix from the September 2026 code review.
class RU {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static object Call(object o, string name, params object[] args) {
        var t = o.GetType();
        while (t != null) {
            var m = t.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (m != null) return m.Invoke(o, args);
            t = t.BaseType;
        }
        throw new MissingMethodException(name);
    }
    static int Subscribers(Type owner, string evt) {
        var f = owner.GetField(evt, BindingFlags.NonPublic | BindingFlags.Static);
        var d = f == null ? null : f.GetValue(null) as Delegate;
        return d == null ? 0 : d.GetInvocationList().Length;
    }

    [STAThread]
    static void Main() {
        Console.WriteLine("--- keypad on a Turkish / German Windows (decimal comma) ---");
        Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
        var pad = new NumpadForm("Input", "12.50", 0, 100, true, true, false);
        Check("starts on the value as given", pad.Value == "12.50", pad.Value);
        Call(pad, "Accept");
        Check("12.50 is ACCEPTED (it was refused: parsed with the regional decimal comma)",
              pad.DialogResult == DialogResult.OK, pad.DialogResult.ToString());
        var pad2 = new NumpadForm("Input", "12,50", 0, 100, true, true, false);
        Check("a regional '12,50' is turned into the keypad's '12.50'", pad2.Value == "12.50", pad2.Value);
        var pad3 = new NumpadForm("Input", "150.5", 0, 100, true, true, false);
        Call(pad3, "Accept");
        Check("the range is still enforced", pad3.DialogResult != DialogResult.OK);

        var field = new NumericInputHMI { DecimalPlaces = 2 };
        typeof(BaseNumericHMI).GetField("_lastNumericValue", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(field, 12.5);
        string kt = (string)Call(field, "KeypadText");
        Check("the field hands the keypad '12.50', not '12,50'", kt == "12.50", kt);
        var hex = new NumericInputHMI { DataFormat = DisplayFormatEnum.Hexadecimal };
        typeof(BaseNumericHMI).GetField("_lastNumericValue", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hex, 255.0);
        kt = (string)Call(hex, "KeypadText");
        Check("a hex field hands the keypad 'FF' (it handed '255', read back as 0x255)", kt == "FF", kt);
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        Console.WriteLine("--- static language event: disposed controls let go ---");
        int before = Subscribers(typeof(LanguageManager), "LanguageChanged");
        for (int i = 0; i < 40; i++) {
            new NumericDisplayHMI().Dispose();
            new NumericInputHMI().Dispose();
            new LabelHMI().Dispose();
            new ButtonHMI().Dispose();
        }
        int after = Subscribers(typeof(LanguageManager), "LanguageChanged");
        Check("160 controls created and disposed leave no subscription behind", after == before, before + " -> " + after);

        Console.WriteLine("--- momentary (jog) bits are released however the press ends ---");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(15);
        PlcManager.Instance.RegisterDriver(15, sim);
        PlcManager.Instance.RegisterStation(15, "jog test", SecurityLevel.Operator);
        SecurityManager.Login("op", SecurityLevel.Operator);
        ConnectionMonitor.ReportRead(15, true);
        var form = new Form();
        var jog = new ButtonHMI { PlcNumber = 15 };
        jog.BitActions.Add(new BitAction { Address = "M500", Action = BitActionType.Momentary });
        form.Controls.Add(jog); form.Show(); Application.DoEvents();
        jog.UpdateState(a => 0, c => true);                           // enabled
        Func<int> bit = () => { int v; sim.TryRead("M500", out v); return v; };
        var down = new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0);

        Call(jog, "OnMouseDown", down);
        Check("pressed: the bit is ON", bit() == 1);
        Call(jog, "OnMouseUp", down);
        Check("released: OFF", bit() == 0);

        Call(jog, "OnMouseDown", down);
        Call(jog, "OnMouseCaptureChanged", EventArgs.Empty);
        Check("the mouse taken away mid-press (a message box): OFF", bit() == 0);

        Call(jog, "OnMouseDown", down);
        jog.Visible = false;
        Check("hidden mid-press (condition dropped): OFF", bit() == 0);
        jog.Visible = true;

        Call(jog, "OnMouseDown", down);
        SecurityManager.Logout();
        Call(jog, "OnMouseUp", down);
        Check("logged out mid-press: the release is STILL written (it went through a permission check)", bit() == 0);
        SecurityManager.Login("op", SecurityLevel.Operator);
        jog.UpdateState(a => 0, c => true);

        Call(jog, "OnMouseDown", down);
        jog.UpdateState(a => 0, c => false);                          // enable condition drops
        Check("the enable condition drops mid-press: OFF", bit() == 0);
        jog.UpdateState(a => 0, c => true);

        Call(jog, "OnMouseDown", down);
        form.Controls.Remove(jog); jog.Dispose();
        Check("disposed mid-press: OFF", bit() == 0);

        var right = new ButtonHMI { PlcNumber = 15 };
        right.BitActions.Add(new BitAction { Address = "M501", Action = BitActionType.Momentary });
        form.Controls.Add(right); right.UpdateState(a => 0, c => true);
        Call(right, "OnMouseDown", new MouseEventArgs(MouseButtons.Right, 1, 5, 5, 0));
        int v501; sim.TryRead("M501", out v501);
        Check("a RIGHT click does not start a jog", v501 == 0);

        form.Close();
        Console.WriteLine(fail == 0 ? "\nUI REVIEW FIXES ALL PASS" : "\nUI REVIEW FIXES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
