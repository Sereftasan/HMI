using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Settings;

class S {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}
    [STAThread] static void Main(){
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic|BindingFlags.Static);
        UnitManager.EnsureDefaults();
        var labels = prog.GetMethod("Main", BindingFlags.NonPublic|BindingFlags.Static);  // not run; FA2010 labels loaded below as Program does
        PlcLabelTable.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));
        m("RegisterDriverKinds").Invoke(null, null); m("MountMachines").Invoke(null, null);

        Console.WriteLine("--- each machine's Alarm Settings lists its own alarms ---");
        foreach (var mod in MachineRegistry.Modules) {
            var id = mod.Context.ModuleId;
            var ctl = new AlarmSettingsPage(mod.Context).GetControl();
            var grid = ctl.Controls.OfType<DataGridView>().FirstOrDefault() ?? FindGrid(ctl);
            int total = AlarmManager.GetAllDefinitions().Count(d => (d.AlarmCode ?? "").StartsWith(id + "."));
            Check(string.Format("{0}: {1} rows, its own {2}", id, grid.Rows.Count, total), grid.Rows.Count == total);
            ctl.Dispose();
        }

        Console.WriteLine("--- each station's labels ---");
        var fa = PlcLabelTable.ByAddress(1, "D318"); var h8 = PlcLabelTable.ByAddress(4, "D318");
        Console.WriteLine("  FA2010 D318: {0}   H800 D318 (station 4): {1}", fa == null ? "-" : fa.DataType, h8 == null ? "-" : h8.DataType);
        Check("H800's D318 has H800's own data type", h8 != null && h8.DataType.IndexOf("Double", StringComparison.OrdinalIgnoreCase) >= 0, h8 == null ? "none" : h8.DataType);
        Console.WriteLine(fail==0?"\nSCOPING ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
    static DataGridView FindGrid(Control c){ foreach (Control k in c.Controls){ var g = k as DataGridView ?? FindGrid(k); if (g != null) return g; } return null; }
}

