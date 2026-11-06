using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Forms;

// 6 November: "create label structure for the profile parameter and a label on the PLC monitor page so I can monitor what HMI
// writes to the PLC as the profile data. I want you to do this for all the devices the HMI writes to the PLC"
// (was: 5 November: "It had deleted the labels I created on the PLC Monitor page when I changed the view scale the other day, there
// should be a bug for it". What the panel's log and the lists' files showed: the screen scale restarts the program; a name
// typed and not yet confirmed (its cell still open - the top bar's scale menu and its question do not take the focus from the
// cell) was not kept, and a second instance (one restarted outside Visual Studio, another started from it) wrote the list it
// had over the other's.
class MSent {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
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

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "monsent_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        int n = PlcLabelTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));
        Check("the FA2010 label list read", n > 60, n.ToString());
        var pp = PlcLabelTable.ByLabel("PC_ProfilParametreleri");
        Check("  the profile's parameters: a structured label at R25600, under its heading",
              pp != null && pp.Address == "R25600" && pp.DataType == "Profile Parameters" && pp.Heading.StartsWith("The job and the profile"));
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(22);
        PlcManager.Instance.RegisterDriver(22, sim);
        PlcManager.Instance.RegisterStation(22, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(22, true);
        var said = new List<string>();
        MonType.GetField("ReportHook", Any).SetValue(null, (Action<string>)(t => said.Add(t)));
        var form = new SimulationMonitorForm();
        form.Show(); Pump(200);
        var mon = ((IList)Get(form, "_monitors")).Cast<object>().First(m => (int)Get(m, "Station") == 22);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = ((IList)Get(form, "_monitors")).IndexOf(mon);
        Pump(100);
        // 6 November: "I don't see any label created on the label list, I want you to create them for me"
        var made = ((IList)Get(mon, "Sheets")).Cast<object>().FirstOrDefault(x => (string)Get(x, "Name") == "HMI -> PLC");
        string file = File.ReadAllText(Directory.GetFiles(dir, "*.csv").First());
        Check("made by itself when the monitor opens (once: kept in the list's file)", made != null && file.Contains("#made:sent") && file.Contains("#sheet:HMI -> PLC"));
        Call(form, "CollectSent"); Pump(100);
        var sheets = ((IList)Get(mon, "Sheets")).Cast<object>().ToList();
        var sh = sheets.FirstOrDefault(x => (string)Get(x, "Name") == "HMI -> PLC");
        Check("a tab of its own: HMI -> PLC", sh != null, string.Join(", ", sheets.Select(x => (string)Get(x, "Name"))));
        var rows = ((IList)Get(sh, "Rows")).Cast<object>().ToList();
        Func<object, string> addr = r => (string)Get(r, "Address") ?? "";
        Func<object, string> comment = r => (string)Get(r, "Comment") ?? "";
        var heads = rows.Select(comment).Where(c => c.Length > 0).ToList();
        Check("  under the list's headings: the job and the profile, the options, the job's other devices, what the PLC gives back",
              heads.Count >= 4 && heads[0].StartsWith("The job and the profile") && heads.Any(h => h.StartsWith("The machine's options")) &&
              heads.Any(h => h.StartsWith("What the PLC gives back")), string.Join(" | ", heads));
        Check("  the profile's parameters, the job list, the milling and cutting steps, the options, the G-code numbers",
              new[] { "R25600", "D5000", "D6000", "D7000", "R26000", "R25650", "R0", "R12800", "R25700", "L24", "D60", "D386" }.All(a => rows.Any(r => addr(r) == a)),
              string.Join(",", rows.Select(addr).Where(a => a.Length > 0).Take(30)));
        Check("  not the label list's other devices (MAN_, IN_ ...)", !rows.Any(r => addr(r) == "M604") && !rows.Any(r => addr(r) == "M200"));
        var swr = System.Diagnostics.Stopwatch.StartNew();
        Call(form, "RefreshValues"); Pump(50);
        swr.Stop();
        Check("  its values read quickly (arrays folded)", swr.ElapsedMilliseconds < 3000, swr.ElapsedMilliseconds + " ms");
        int count = rows.Count;
        Call(form, "CollectSent"); Pump(100);
        Check("again: the same tab, nothing twice", ((IList)Get(sh, "Rows")).Count == count && ((IList)Get(mon, "Sheets")).Count == sheets.Count);
        form.Close();
        Console.WriteLine(fail == 0 ? "MONITOR SENT ALL PASS" : "MONITOR SENT " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
