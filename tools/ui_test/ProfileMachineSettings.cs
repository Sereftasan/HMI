using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.Profiles.Geometry;
using Kaban.Profiles.Machining;
using Kaban.UI.Controls;
using Kaban.UI.Profiles;

// THE PROFILES PAGE on the profile library (30 September): the table from the
// library, a profile's drawing turned the right way, its reference points found
// and confirmed, an accessory placed on it, moved in a trial and confirmed, the
// programs made - and nothing changed below the Maintenance level.
class PMS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        var t = o.GetType();
        while (t != null) {
            var f = t.GetField(n, Any); if (f != null) return f.GetValue(o);
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o, null);
            t = t.BaseType;
        }
        throw new MissingMemberException(n);
    }
    static object Call(object o, string n, params object[] a) {
        var mi = o.GetType().GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length);
        return mi.Invoke(o, a);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static readonly Queue<string> answers = new Queue<string>();
    static readonly List<string> asked = new List<string>();

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "pms_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        Db.ConnectionTarget = Path.Combine(dir, "Kaban.db");
        Db.SqliteChoice = Db.SqliteLibrary.Native;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        PlcLabelTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcLabels.csv"));
        StructuredTypes.FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StructuredTypes.csv");     // no machine mounted here
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        Ask.Hook = (title, prompt, initial) => initial;
        var form = new Form { ClientSize = new Size(1600, 1000) };
        var page = new Kaban.Machines.FA2010.Screens.ProfilesScreen();
        form.Controls.Add(page);
        form.Show();
        Pump(300);
        Call(page, "EnsureLibrary");
        Pump(100);
        // 6 November: the old program's profile parameters (R25600 ...), R25615 with them - one table (second answer: "The profile details
        // window is not very responsive, it is very hard to set it's borders even")
        var grid = (DataGridView)Get(page, "_machineGrid");
        Func<string, DataGridViewRow> rowOf = n => grid.Rows.Cast<DataGridViewRow>().First(r => (string)r.Cells["name"].Value == n);
        Check("the Machine settings tab: ONE table, a row per member of Profile Parameters (the PC_ProfilParametreleri structure), R25615 among them",
              grid.Rows.Count > 40 && rowOf("Profile Correction Position") != null && rowOf("Tool Cooling Active").Cells["value"] is DataGridViewCheckBoxCell &&
              ((TabPage)Get(page, "_machineTab")).Controls.Count == 2, grid.Rows.Count.ToString());
        var all = Library.Profiles();
        var sash = all.First(p => p.Code == "0001000104");
        long instance = (long)Get(page, "_instance");
        long template = Library.GroupTemplateOf(sash.Id);
        Call(page, "SelectProfile", template); Pump(150);
        Call(page, "KeepMachineValue", "Conveyor Speed", "80");
        Call(page, "KeepMachineValue", "Tool Cooling Active", "1");
        Pump(100);
        Check("typed on the group's template profile: its own", Library.MachineSettings(template, instance)["Conveyor Speed"].Own &&
              Library.MachineSettings(template, instance)["Conveyor Speed"].Value == "80");
        Check("  nothing set shows 0 (not \"not set\")", (string)rowOf("Cut Speed 9").Cells["value"].Value == "0" && (string)rowOf("Cut Speed 9").Cells["group"].Value == "0");
        // a new profile of the group, as the + button makes it
        long id = Library.SaveProfile(new Library.ProfileInfo { GroupId = sash.GroupId, Code = "NEW-OF-GROUP", DxfStatus = "NONE" }, "tester");
        Library.TakeTemplateSettings(id, instance);
        Check("a new profile of the group: the group's values loaded as its own (\"load the group parameter values to the profile when we create a new one\")",
              Library.MachineSettings(id, instance)["Conveyor Speed"].Own && Library.MachineSettings(id, instance)["Conveyor Speed"].Value == "80");
        Call(page, "ReloadAll"); Pump(150);
        Call(page, "SelectProfile", id); Pump(150);
        var sp = rowOf("Conveyor Speed");
        Check("  shown with the group's beside it, the same - not marked", (string)sp.Cells["value"].Value == "80" && (string)sp.Cells["group"].Value == "80" &&
              sp.Cells["value"].Style.Font == null && grid.Columns["group"].HeaderText.Contains(Library.Profiles().First(p => p.Id == template).Code),
              sp.Cells["value"].Value + " / " + sp.Cells["group"].Value + " / " + grid.Columns["group"].HeaderText);
        Check("  a switch too", (string)rowOf("Tool Cooling Active").Cells["value"].Value == "1");
        Call(page, "KeepMachineValue", "Conveyor Speed", "65"); Pump(100);
        sp = rowOf("Conveyor Speed");
        Check("changed: its own, bold blue (\"so the user could see the edited ones\"); the group keeps 80",
              Library.MachineSettings(id, instance)["Conveyor Speed"].Value == "65" && (string)sp.Cells["value"].Value == "65" && (string)sp.Cells["group"].Value == "80" &&
              sp.Cells["value"].Style.Font != null && sp.Cells["value"].Style.Font.Bold && Library.MachineSettings(template, instance)["Conveyor Speed"].Value == "80");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var pane = grid.Parent;
        for (int i = 0; i < 20; i++) { pane.Width += (i % 2 == 0 ? 37 : -37); Application.DoEvents(); }
        sw.Stop();
        Check("  its border moved twenty times quickly", sw.ElapsedMilliseconds < 2000, sw.ElapsedMilliseconds + " ms");
        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "PROFILE MACHINE SETTINGS ALL PASS" : "PROFILE MACHINE SETTINGS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
