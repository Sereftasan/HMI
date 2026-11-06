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
        // 6 November: the old program's profile parameters (R25600 ...), R25615 with them
        var inputs = (IDictionary)Get(page, "_machineInputs");
        Check("the Machine settings tab: a field per member of Profile Parameters (the PC_ProfilParametreleri structure), R25615 among them",
              inputs.Count > 40 && inputs.Contains("Profile Correction Position") && inputs.Contains("Conveyor Speed") && inputs["Tool Cooling Active"] is CheckBox, inputs.Count.ToString());
        var all = Library.Profiles();
        var sash = all.First(p => p.Code == "0001000104");
        long instance = (long)Get(page, "_instance");
        Library.Exec("INSERT INTO profile (group_id, code, dxf_status, is_active, created_at) VALUES (@p0,'NEW-OF-GROUP','NONE',1,'2026-11-06')", sash.GroupId);
        all = Library.Profiles();
        long template = Convert.ToInt64(Library.Scalar("SELECT COALESCE(reference_profile_id, (SELECT MIN(q.id) FROM profile q WHERE q.group_id = g.id AND q.is_active = 1)) FROM profile_group g WHERE g.id=@p0", sash.GroupId));
        var other = all.First(p => p.GroupId == sash.GroupId && p.Id != template);
        Call(page, "SelectProfile", template); Pump(150);
        Call(page, "KeepMachineValue", "Conveyor Speed", "80");
        Call(page, "KeepMachineValue", "Tool Cooling Active", "1");
        Check("typed on the group's template profile: its own", Library.MachineSettings(template, instance)["Conveyor Speed"].Own &&
              Library.MachineSettings(template, instance)["Conveyor Speed"].Value == "80");
        Call(page, "SelectProfile", other.Id); Pump(150);
        var speed = (TextBox)inputs["Conveyor Speed"];
        var from = (Label)((IDictionary)Get(page, "_machineFrom"))["Conveyor Speed"];
        Check("another profile of the group: the template's value, grey, saying where from", speed.Text == "80" && speed.ForeColor == Color.Gray && from.Text.Contains(Library.Profiles().First(p => p.Id == template).Code),
              speed.Text + " / " + from.Text);
        Check("  a switch too", ((CheckBox)inputs["Tool Cooling Active"]).Checked);
        Call(page, "KeepMachineValue", "Conveyor Speed", "65");
        Check("typed there: its own, the template keeps 80", Library.MachineSettings(other.Id, instance)["Conveyor Speed"].Value == "65" &&
              Library.MachineSettings(other.Id, instance)["Conveyor Speed"].Own && Library.MachineSettings(template, instance)["Conveyor Speed"].Value == "80");
        Call(page, "KeepMachineValue", "Conveyor Speed", "");
        Check("emptied: the template's again", Library.MachineSettings(other.Id, instance)["Conveyor Speed"].Value == "80" && !Library.MachineSettings(other.Id, instance)["Conveyor Speed"].Own);
        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "PROFILE MACHINE SETTINGS ALL PASS" : "PROFILE MACHINE SETTINGS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
