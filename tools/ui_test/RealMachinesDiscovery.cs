using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

class R {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    // Follows NESTED settings screens into their pages too: a settings screen
    // builds a tab only when it is opened, so walking its controls alone reached
    // nothing on the Machine Parameters screen.
    static IEnumerable<int> Stations(Control c){
        var p = c.GetType().GetProperty("PlcNumber", BindingFlags.Public|BindingFlags.Instance);
        if (p != null && p.PropertyType == typeof(int)) yield return (int)p.GetValue(c, null);
        var ss = c as SettingsScreen;
        if (ss != null)
            foreach (var pg in ss.AllPages) {
                var built = pg.GetControl();
                foreach (var s in Stations(built)) yield return s;
                built.Dispose();
            }
        foreach (Control k in c.Controls) foreach (var s in Stations(k)) yield return s;
    }

    static void Who(Control c, int host, string ind){
        var p = c.GetType().GetProperty("PlcNumber", BindingFlags.Public|BindingFlags.Instance);
        if (p != null && p.PropertyType == typeof(int) && (int)p.GetValue(c, null) != host)
            Console.WriteLine(ind + "on station " + p.GetValue(c, null) + ": " + c.GetType().Name + " '" + c.Name + "' parent=" + (c.Parent == null ? "-" : c.Parent.GetType().Name));
        foreach (Control k in c.Controls) Who(k, host, ind);
    }

    [STAThread] static void Main(){
        // FA2010's own start-up, step by step, exactly as Program.Main runs it.
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic|BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        var fallback = m("FallbackConnection");
        var apply = typeof(PlcConnectionSettings).GetMethod("ApplyAll");
        apply.Invoke(null, new object[]{ Delegate.CreateDelegate(apply.GetParameters()[0].ParameterType, fallback) });
        m("SeedSimulatedStations").Invoke(null, null);

        var modules = MachineRegistry.Modules.Select(x => x.Context.ModuleId + (x.IsMain ? " (main)" : "")).ToList();
        Console.WriteLine("mounted: " + string.Join(", ", modules));

        Console.WriteLine("--- start-up discovery over every real page ---");
        var cold = AlarmHelpLinks.DiscoverAll();
        var warm = AlarmHelpLinks.DiscoverAll();
        Console.WriteLine("  first run (cold, as at start-up): {0} ms", (int)cold.TotalMilliseconds);
        Console.WriteLine("  second run (warm):                {0} ms", (int)warm.TotalMilliseconds);
        Console.WriteLine("  error bits linked: {0}   conflicts: {1}", AlarmHelpLinks.Count, AlarmHelpLinks.Conflicts.Count);
        Check("discovery completes over all three machines", true);

        Console.WriteLine("--- a mounted H500's pages now talk to station 3 ---");
        foreach (var kv in MachineRegistry.AllScreens().Where(k => !k.Key.IsMain))
        {
            var sub = kv.Key; int host = sub.Context.Station(1);
            var view = kv.Value.CreateView == null ? null : kv.Value.CreateView() as Control;
            Console.WriteLine("    [{0}] '{1}' role={2} view={3}", sub.Context.ModuleId, kv.Value.TitleFallback, kv.Value.Role,
                              view == null ? "null" : view.GetType().Name);
            if (view == null) continue;
            var found = Stations(view).ToList();
            if (kv.Value.Role == "Home") Who(view, host, "      ");
            view.Dispose();
            if (found.Count == 0) continue;
            var distinct = found.Distinct().OrderBy(x => x).ToList();
            Check(string.Format("{0} '{1}': {2} controls, all on station {3}", sub.Context.ModuleId, kv.Value.TitleFallback, found.Count, host),
                  distinct.All(s => s == host), "stations seen: " + string.Join(",", distinct));
        }

        Console.WriteLine(fail==0?"\nREAL MACHINES ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

