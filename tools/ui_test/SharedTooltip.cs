using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

class Q {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    // Controls registered with tip text; "dead" = disposed ones, the dangerous kind:
    // a tooltip looking for its parent window among them threw in CreateHandle.
    static void Count(ToolTip t, out int live, out int dead){
        live = dead = 0;
        foreach (var f in typeof(ToolTip).GetFields(BindingFlags.NonPublic|BindingFlags.Instance)) {
            if (!typeof(IDictionary).IsAssignableFrom(f.FieldType)) continue;
            var d = f.GetValue(t) as IDictionary; if (d == null) continue;
            foreach (DictionaryEntry e in d) {
                if (e.Value == null || e.Value.ToString() == "") continue;
                var c = e.Key as Control; if (c == null) continue;
                if (c.IsDisposed) dead++; else live++;
            }
            return;
        }
    }

    [STAThread] static void Main(){
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic|BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null); m("MountMachines").Invoke(null, null);
        var fb = m("FallbackConnection"); var apply = typeof(PlcConnectionSettings).GetMethod("ApplyAll");
        apply.Invoke(null, new object[]{ Delegate.CreateDelegate(apply.GetParameters()[0].ParameterType, fb) });

        var shared = UiStyle.Tips();
        bool disposed = false; shared.Disposed += (s, e) => disposed = true;

        Console.WriteLine("--- the cause: a numeric field disposed the SHARED tooltip ---");
        var field = new NumericInputHMI(); UiStyle.Tips().SetToolTip(field, "D100"); field.Dispose();
        Check("disposing a numeric field leaves the shared tooltip alive", !disposed);
        shared.Dispose();
        Check("even an explicit Dispose cannot end it", !disposed);

        Console.WriteLine("--- start-up discovery over every page ---");
        AlarmHelpLinks.DiscoverAll();
        int live, dead; Count(shared, out live, out dead);
        Check("no disposed control is left in the shared tooltip", dead == 0,
              dead + " dead; " + live + " live (e.g. the Profiles toolbar, built unplaced - harmless)");
        Check("the shared tooltip is intact", !disposed);

        Console.WriteLine("--- the login bar's tip, afterwards ---");
        var form = new Form(); var login = new Button(); form.Controls.Add(login); form.Show(); Application.DoEvents();
        var own = UiStyle.CreateTips();
        Exception err = null;
        try { own.SetToolTip(login, "Logout"); shared.SetToolTip(login, "Logout"); } catch (Exception ex) { err = ex; }
        Check("setting tips after discovery works", err == null, err == null ? "" : err.GetType().Name);
        Check("the main window's tooltip is its own", !ReferenceEquals(own, shared));
        form.Close();

        Console.WriteLine(fail==0?"\nTOOLTIP ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

