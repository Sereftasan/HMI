using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Forms;

// SHIFT+ENTER on the PLC Monitor (30 September): a Bit row is switched over,
// any other row opens its value with the number selected - from any cell.
class SE {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static Type MonType = typeof(SimulationMonitorForm);
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
        var p = t.GetProperty(n, Any); return p.GetValue(o is Type ? null : o, null);
    }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length >= a.Length &&
                                              m.GetParameters().Skip(a.Length).All(p => p.IsOptional));
        var args = a.Concat(mi.GetParameters().Skip(a.Length).Select(p => p.DefaultValue)).ToArray();
        return mi.Invoke(o is Type ? null : o, args);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static bool Key(Form f, Keys k) {
        var msg = new Message();
        var mi = typeof(SimulationMonitorForm).GetMethod("ProcessCmdKey", Any);
        return (bool)mi.Invoke(f, new object[] { msg, k });
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "monse_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        var sim = new Kaban.Drivers.Simulation.SimulationDriver(21);
        PlcManager.Instance.RegisterDriver(21, sim);
        PlcManager.Instance.RegisterStation(21, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(21, true);
        sim.Write("M5", 1); sim.Write("D10", 1234);
        var ep = PlcConnectionSettings.Endpoints().First(e => e.StationId == 21);
        File.WriteAllLines((string)Call(MonType, "WatchPath", ep), new[] { "Label;DataType;Address;Value", "Start;Bit;M5;", "Speed;Word [Signed];D10;" });

        var form = new SimulationMonitorForm();
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 21);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        Call(form, "RefreshValues"); Pump(100);
        var sh = Get(mon, "Current");
        var grid = (DataGridView)Get(sh, "Grid");
        grid.Focus(); Pump(50);
        Check("the list has the focus", grid.ContainsFocus);

        grid.CurrentCell = grid.Rows[0].Cells["Label"];
        Check("Shift+Enter on the bit's label: handled", Key(form, Keys.Shift | Keys.Enter));
        Check("  M5 switched from 1 to 0", sim.Read("M5") == 0, sim.Read("M5").ToString());
        Call(form, "RefreshValues"); Pump(50);
        Key(form, Keys.Shift | Keys.Enter);
        Check("  and back to 1", sim.Read("M5") == 1, sim.Read("M5").ToString());
        Check("  not left editing", !grid.IsCurrentCellInEditMode);
        Check("  the write says how it was made", File.Exists(PlcWriteLog.FileFor(DateTime.Now)) &&
              File.ReadAllText(PlcWriteLog.FileFor(DateTime.Now)).Contains("Shift+Enter"));

        grid.CurrentCell = grid.Rows[1].Cells["Address"];
        Key(form, Keys.Shift | Keys.Enter);
        Pump(50);
        var box = grid.EditingControl as TextBox;
        Check("Shift+Enter on the word's address: its Value opens", grid.IsCurrentCellInEditMode && grid.CurrentCell.OwningColumn.Name == "Value");
        Check("  the number selected, to be typed over", box != null && box.Text == "1234" && box.SelectionLength == box.Text.Length,
              box == null ? "no box" : box.Text + " / " + box.SelectionStart + "+" + box.SelectionLength);
        grid.CancelEdit(); grid.EndEdit();

        Console.WriteLine("--- a bit typed as TRUE / FALSE ---");
        var row = ((IList)Get(sh, "Rows"))[0];
        string problem;
        var words = (int[])MonType.GetMethod("ParseValue", Any).Invoke(null, new object[] { row, "FALSE", null });
        Check("FALSE is 0", words != null && words[0] == 0);
        words = (int[])MonType.GetMethod("ParseValue", Any).Invoke(null, new object[] { row, "true", null });
        Check("true is 1", words != null && words[0] == 1);
        var word = ((IList)Get(sh, "Rows"))[1];
        words = (int[])MonType.GetMethod("ParseValue", Any).Invoke(null, new object[] { word, "TRUE", null });
        Check("  a word does not take TRUE", words == null);

        form.Close();
        Console.WriteLine(fail == 0 ? "\nSHIFT ENTER ALL PASS" : "\nSHIFT ENTER " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
