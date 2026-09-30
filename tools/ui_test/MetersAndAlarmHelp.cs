using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

class M {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d.Replace("\r\n"," / ")+"]":""));if(!ok)fail++;}
    static Func<bool> Hover(Control c){
        for (var t = c.GetType(); t != null; t = t.BaseType) {
            var f = t.GetField("_hovering", BindingFlags.NonPublic|BindingFlags.Instance);
            if (f != null) return (Func<bool>)f.GetValue(c);
        }
        return null;
    }
    // The tip as the control shows it when the pointer rests on it (UiStyle.HoverTip - not registered with the tooltip).
    sealed class TipReader { public string GetToolTip(Control c) {
        for (var t = c.GetType(); t != null; t = t.BaseType) {
            var m = t.GetMethod("TooltipText", BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance);
            if (m != null) return (string)m.Invoke(c, null);
        }
        return "";
    } }
    static void Enter(Control c, bool on){
        var m = typeof(Control).GetMethod(on ? "OnMouseEnter" : "OnMouseLeave", BindingFlags.NonPublic|BindingFlags.Instance);
        m.Invoke(c, new object[]{ EventArgs.Empty });
    }

    [STAThread] static void Main(){
        ConnectionMonitor.GraceTime = TimeSpan.FromMilliseconds(300);

        Console.WriteLine("--- the change event fires on the way DOWN ---");
        int events = 0;
        ConnectionMonitor.ConnectionChanged += (s, e) => events++;
        ConnectionMonitor.ReportRead(21, true);
        ConnectionMonitor.ReportRead(21, false, "not connected");
        Thread.Sleep(400);                                   // falls out of grace
        ConnectionMonitor.ReportRead(21, false, "not connected");
        Check("a LOSS is announced (was: never)", events == 1, events + " event(s)");
        ConnectionMonitor.ReportRead(21, true);
        Check("and the recovery", events == 2, events + " event(s)");

        Console.WriteLine("--- meters and code cells ---");
        var meter = new MeterHMI { PlcNumber = 22, Address = "D200", Divisor = 1 };
        var code = new HexCodeHMI { PlcNumber = 22, Address = "W30" };
        meter.SetBounds(10, 10, 120, 24); code.SetBounds(10, 40, 60, 24);
        var host = new Form { ClientSize = new Size(300, 120) };
        host.Controls.Add(meter); host.Controls.Add(code);
        host.Show(); Application.DoEvents();
        var tips = new TipReader();

        Check("meter tooltip shows its device", tips.GetToolTip(meter).Contains("D200"), tips.GetToolTip(meter));

        Enter(meter, true);
        Check("meter answers the mouse (blue outline)", Hover(meter) != null && Hover(meter)());
        Enter(meter, false);
        Check("  and lets go", !Hover(meter)());
        Enter(code, true);
        Check("code cell answers the mouse too", Hover(code) != null && Hover(code)());
        Enter(code, false);

        Console.WriteLine("--- a lost PLC: stale, explained, NOT disabled ---");
        ConnectionMonitor.ReportRead(22, false, "read failed");
        Application.DoEvents();
        Check("meter stays enabled, so hover and tooltip keep working", meter.Enabled);
        Check("its tooltip gives the reason first", tips.GetToolTip(meter).StartsWith("Connection failed"), tips.GetToolTip(meter));
        Check("  and still the device", tips.GetToolTip(meter).Contains("D200"));
        Check("code cell likewise", code.Enabled && tips.GetToolTip(code).Contains("C03"), tips.GetToolTip(code));

        ConnectionMonitor.ReportRead(22, true);
        Application.DoEvents();
        // PLC 22 is auxiliary, so its tooltip rightly starts "PLC 22"; what must go
        // on recovery is the failure reason.
        Check("recovered: the reason is gone", !tips.GetToolTip(meter).Contains("Connection failed"), tips.GetToolTip(meter));
        Check("  the device remains", tips.GetToolTip(meter).Contains("D200"));
        host.Close();

        Console.WriteLine("--- alarm help lists ---");
        foreach (var code_ in new[] { "1.1.2", "2.3.0" }) {
            var docs = TC.Resolve(code_).Where(d => d.Exists).ToList();
            TC.HelpDocument opened = null;
            var menu = Kaban.UI.Screens.MachineAlarmScreen.BuildHelpMenu(docs, d => opened = d);
            var items = menu.Items.OfType<ToolStripMenuItem>().ToList();
            Check(code_ + ": every document listed", items.Count == docs.Count && docs.Count > 0,
                  string.Join(" | ", items.Select(i => i.Text)));
            items.Last().PerformClick();
            Check("  choosing one opens THAT document", opened == docs.Last(), opened == null ? "nothing" : opened.Title);
            Check("  which is a real file", System.IO.File.Exists(TC.ExtractSection(opened)), TC.ExtractSection(opened));
        }

        Console.WriteLine(fail==0?"\nMETER+HELP ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

