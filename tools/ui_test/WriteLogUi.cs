using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Settings;

// THE PLC WRITE LOG from the screens (30 September): each control names itself
// in the log - page, control, caption, what was done - and the PLC Monitor's
// Last change column tells a change the HMI made from one the PLC program made.
class TestPage : UserControl { }

class WL {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static Type MonType = typeof(SimulationMonitorForm);
    static Type RowType = MonType.GetNestedType("WatchRow", Any);
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
        var p = t.GetProperty(n, Any); return p.GetValue(o is Type ? null : o, null);
    }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, Any).SetValue(o, v); }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length >= a.Length &&
                                              m.GetParameters().Skip(a.Length).All(p => p.IsOptional));
        var args = a.Concat(mi.GetParameters().Skip(a.Length).Select(p => p.DefaultValue)).ToArray();
        return mi.Invoke(o is Type ? null : o, args);
    }
    static IList Rows(object sheet) { return (IList)Get(sheet, "Rows"); }
    static object Row(string address, string type, string label) {
        var r = Activator.CreateInstance(RowType);
        Set(r, "Address", address); Set(r, "DataTypeOverride", type); Set(r, "LabelOverride", label); Set(r, "Station", 23);
        return r;
    }
    static List<string[]> Lines() {
        PlcWriteLog.Flush();                     // written on its own thread
        var f = PlcWriteLog.FileFor(DateTime.Now);
        return File.Exists(f) ? File.ReadAllLines(f).Skip(1).Select(l => l.Split(';')).ToList() : new List<string[]>();
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "wlui_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        var sim = new Kaban.Drivers.Simulation.SimulationDriver(23);
        PlcManager.Instance.RegisterDriver(23, sim);
        PlcManager.Instance.RegisterStation(23, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("ayse", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(23, true);

        Console.WriteLine("--- a button on a page names itself ---");
        var form = new Form();
        var page = new TestPage { Name = "page1", Dock = DockStyle.Fill };
        var group = new GroupBoxHMI { Name = "grpSaw", Text = "Saw" };
        var jog = new ButtonHMI { Name = "btnJog", Text = "Jog forward", PlcNumber = 23 };
        jog.BitActions.Add(new BitAction { Address = "M500", Action = BitActionType.Momentary });
        var set = new ButtonHMI { Name = "btnStart", Text = "Start", PlcNumber = 23 };
        set.BitActions.Add(new BitAction { Address = "M501", Action = BitActionType.SET });
        group.Controls.Add(jog); group.Controls.Add(set); page.Controls.Add(group);
        form.Controls.Add(page); form.Show(); Application.DoEvents();
        jog.UpdateState(a => 0, c => true); set.UpdateState(a => 0, c => true);
        var down = new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0);
        Call(jog, "OnMouseDown", down);
        Call(jog, "OnMouseUp", down);
        var l = Lines();
        Check("a press and a release: two lines", l.Count == 2 && l[0][3] == "M500" && l[0][4] == "1" && l[1][4] == "0",
              string.Join(" | ", l.Select(x => string.Join(";", x))));
        Check("the page, the control and its caption", l.Count > 0 && l[0][9] == "TestPage.btnJog [Jog forward]: momentary press", l.Count > 0 ? l[0][9] : "");
        Check("the release says so", l.Count > 1 && l[1][9].EndsWith(": momentary release"), l.Count > 1 ? l[1][9] : "");
        Check("the user", l.Count > 0 && l[0][8] == "ayse");
        Call(set, "OnClick", EventArgs.Empty);
        l = Lines();
        Check("a SET button", l.Count == 3 && l[2][3] == "M501" && l[2][9] == "TestPage.btnStart [Start]: SET", l.Count > 2 ? l[2][9] : "");

        Console.WriteLine("--- a 32-bit numeric entry: both words ---");
        var num = new NumericInputHMI { Name = "numLength", Address = "D300", DataType = DataTypeEnum.Bin_32, PlcNumber = 23 };
        page.Controls.Add(num);
        typeof(NumericInputHMI).GetMethod("ProcessWrite", Any).Invoke(num,
            new object[] { "70000", PlcManager.Instance.GetDriver(23), (Func<string, int>)(a => 0) });
        l = Lines();
        var words = l.Skip(3).ToList();
        Check("two lines, low and high word", words.Count == 2 && words[0][3] == "D300" && words[0][4] == (70000 & 0xFFFF).ToString() &&
              words[1][3] == "D301" && words[1][4] == "1", string.Join(" | ", words.Select(x => x[3] + "=" + x[4])));
        Check("  both from the entry", words.Count == 2 && words.All(x => x[9] == "TestPage.numLength: entered 70000"), words.Count > 0 ? words[0][9] : "");

        Console.WriteLine("--- the bench simulation's movement is the PLC's, not logged ---");
        var motion = typeof(Kaban.UI.Helpers.WriteSource).Assembly.GetType("Kaban.UI.Helpers.SimulationMotion");
        var simulated = (Kaban.Core.Interfaces.IPlcDriver)motion.GetMethod("Simulated", Any).Invoke(null, new object[] { 23 });
        Check("it moves the simulator through its own driver", simulated != null && !(simulated is LoggedDriver) && ReferenceEquals(simulated, sim));

        Console.WriteLine("--- the PLC Monitor: Last change ---");
        var mf = new SimulationMonitorForm();
        mf.Show(); Application.DoEvents();
        var monitors = (IList)Get(mf, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 23);
        ((TabControl)Get(mf, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        var sh = Call(mf, "EnsureSheet", mon);
        var grid = (DataGridView)Get(sh, "Grid");
        var cols = grid.Columns.Cast<DataGridViewColumn>().OrderBy(c => c.DisplayIndex).Select(c => c.Name).ToList();
        Check("a Modified (last change) column after Value", cols.IndexOf("Changed") == cols.IndexOf("Value") + 1, string.Join(",", cols));
        Check("  headed Modified", grid.Columns["Changed"].HeaderText == "Modified", grid.Columns["Changed"].HeaderText);
        Check("  read-only", grid.Columns["Changed"].ReadOnly);

        sim.Write("D100", 1);
        var speed = Row("D100", "Word [Signed]", "Speed");
        var len = Row("D110", "Double Word [Signed]", "Length");
        Rows(sh).Insert(Rows(sh).Count - 1, speed); Rows(sh).Insert(Rows(sh).Count - 1, len);
        Call(mf, "RefreshValues"); Application.DoEvents();
        int si = Rows(sh).IndexOf(speed), li = Rows(sh).IndexOf(len);
        Func<int, string> cell = i => Convert.ToString(grid.Rows[i].Cells["Changed"].Value);
        Check("no change seen yet: '-'", cell(si) == "-", cell(si));

        System.Threading.Thread.Sleep(30);
        Call(mf, "WriteValue", mon, speed, "42");
        Call(mf, "RefreshValues"); Application.DoEvents();
        Check("the HMI wrote it: HMI", cell(si).EndsWith("HMI") && cell(si).Contains(DateTime.Now.ToString("HH:mm")), cell(si));
        string tip = grid.Rows[si].Cells["Changed"].ToolTipText;
        Check("  the tooltip: from, to, what wrote it and who", tip.Contains("from 1 to 42") && tip.Contains("ayse") && tip.Contains("PLC Monitor: typed 42 into D100 [Speed]"), tip);
        var last = Lines().Last();
        Check("the monitor's write is logged with the value before", last[3] == "D100" && last[4] == "42" && last[6] == "1" && last[9] == "PLC Monitor: typed 42 into D100 [Speed]",
              string.Join(";", last));

        System.Threading.Thread.Sleep(1200);   // past the slack a late reading is allowed
        sim.Write("D100", 7);                  // the ladder
        Call(mf, "RefreshValues"); Application.DoEvents();
        Check("the PLC changed it: PLC", cell(si).EndsWith("PLC"), cell(si));
        tip = grid.Rows[si].Cells["Changed"].ToolTipText;
        Check("  the tooltip says the HMI did not write it, and when the HMI last did", tip.Contains("from 42 to 7") && tip.Contains("did not write") && tip.Contains("last wrote it"), tip);

        int before = Lines().Count;
        Call(mf, "WriteValue", mon, len, "100000");
        Call(mf, "RefreshValues"); Application.DoEvents();
        var dw = Lines().Skip(before).ToList();
        Check("a double word typed in: its two words in the log", dw.Count == 2 && dw[0][3] == "D110" && dw[1][3] == "D111", string.Join(",", dw.Select(x => x[3])));
        Check("  its row: HMI", cell(li).EndsWith("HMI"), cell(li));

        var cmp = MonType.GetMethod("CompareRows", Any);
        int cr = (int)cmp.Invoke(null, new object[] { mon, len, speed, "Changed" });
        Check("sorting on it puts the latest change first", cr < 0, cr.ToString());

        Console.WriteLine("--- the log viewer: PLC device writes ---");
        Check("a kind of its own", LogViewerControl.KindName(AuditKind.DeviceWrite) == "PLC device writes");
        var viewer = new LogViewerControl(AuditKind.DeviceWrite, LogViewerControl.Period.Today);
        var vf = new Form(); viewer.Dock = DockStyle.Fill; vf.Controls.Add(viewer); vf.Show(); Application.DoEvents();
        var shown = (IList)Get(viewer, "_shown");
        Check("the viewer lists every device written", shown != null && shown.Count == Lines().Count, (shown == null ? "null" : shown.Count.ToString()) + " vs " + Lines().Count);

        mf.Close(); form.Close(); vf.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nWRITELOG UI ALL PASS" : "\n" + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
