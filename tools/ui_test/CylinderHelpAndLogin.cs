using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

// 25 September: the cylinder manual tailored by the unit's ValveType and
// FeedbackType as ONE chart, the failed movement's devices at its top, the
// unit's single "?", the login's user list, the Cutting page's unit sizes and
// the servo help without its Target column.
class CH {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static T Field<T>(object o, string n) { return (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o); }

    [STAThread]
    static void Main() {
        var host = new Form { ClientSize = new Size(900, 400) };

        Console.WriteLine("--- ValveType and FeedbackType on the unit ---");
        var props = TypeDescriptor.GetProperties(typeof(FunctionUnitHMI));
        Check("ValveType and FeedbackType are in the Properties window, under Help",
              props["ValveType"] != null && props["ValveType"].IsBrowsable && props["ValveType"].Category == "Help" &&
              props["FeedbackType"] != null && props["FeedbackType"].IsBrowsable && props["FeedbackType"].Category == "Help");
        Check("  the old names are hidden aliases (not listed, not written by the designer)",
              !props["Valve"].IsBrowsable && !props["Feedback"].IsBrowsable &&
              props["Valve"].SerializationVisibility == DesignerSerializationVisibility.Hidden);
        Check("  the valve names read as the manual's", Enum.GetNames(typeof(CylinderValve)).SequenceEqual(
              new[] { "Unspecified", "DoubleSolenoid_5_2", "SingleSolenoid_5_2", "DoubleSolenoid_5_3" }));

        var unit = new FunctionUnitHMI {
            PlcNumber = 4, TitleTextKey = "Left Top Clamping",
            CommandAAddress = "M604", ButtonATextKey = "Clamp", OutputAAddress = "Y24", Lamp1AAddress = "X30", Lamp1ATipKey = "Clamp closed",
            CommandBAddress = "M605", ButtonBTextKey = "Release", OutputBAddress = "Y25", Lamp1BAddress = "X31", Lamp1BTipKey = "Clamp open",
            ErrorAAddress = "M810", ErrorBAddress = "M811",
            ValveType = CylinderValve.SingleSolenoid_5_2, FeedbackType = CylinderFeedback.TwoEndMagnetic };
        unit.SetBounds(10, 10, 360, 64);
        host.Controls.Add(unit); host.Show(); Application.DoEvents();
        var mark = Field<UnitHelpBadge>(unit, "_help");
        var helpA = Field<AlarmHelpBadge>(unit, "_helpA");
        Check("with its types set, the unit shows a blue '?'", mark.Visible && mark.State == UnitHelpBadge.BadgeState.Info);
        var calm = unit.HelpDocuments();
        Check("  which opens the manual for its valve and feedback, no movement named",
              calm.Count == 1 && calm[0].Valve == 2 && calm[0].Feedback == 2 && calm[0].Movement == null,
              string.Join(" | ", calm.Select(d => d.Title)));

        Console.WriteLine("--- movement A fails ---");
        helpA.UpdateState(a => a == "M810" ? 1 : 0, c => true);
        Check("the '?' turns red", mark.State == UnitHelpBadge.BadgeState.Error);
        var docs = unit.HelpDocuments();
        var doc = docs.FirstOrDefault(d => d.Movement != null);
        Check("its help names movement A", doc != null && doc.Movement.Side == "A" && doc.Movement.Name == "Clamp" &&
              doc.Movement.Command.Address == "M604" && doc.Movement.Output.Address == "Y24",
              doc == null ? "none" : doc.Title);
        Check("  the entry names the movement, not the unit (the '?' is on the unit)",
              doc != null && doc.Title.Contains("Clamp") && !doc.Title.Contains("Left Top Clamping"), doc == null ? "" : doc.Title);
        Check("  sensors: X30 must turn on, X31 must turn off",
              doc != null && doc.Movement.Feedback.Single().Address == "X30" && doc.Movement.OppositeFeedback.Single().Address == "X31");
        string path = TC.ExtractSection(doc);
        string html = File.ReadAllText(path);
        Console.WriteLine("      " + path);
        var details = Regex.Matches(html, "<tbody data-detail=\"([^\"]*)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        Check("ONE chart: one heading row, details 1.1 to 1.8 once each, in order",
              Regex.Matches(html, "<thead>").Count == 1 && string.Join(",", details) == "1.1,1.2,1.3,1.4,1.5,1.6,1.7,1.8",
              Regex.Matches(html, "<thead>").Count + " headings; " + string.Join(",", details));
        Check("  only the single solenoid 5/2 in 1.4", html.Contains("Single solenoid 5/2") &&
              !html.Contains("Double solenoid 5/2 (impulse") && !html.Contains(">(3) Double solenoid 5/3"));
        Check("  only the two end sensors in 1.7", html.Contains("Two end magnetic sensors") && !html.Contains("Analogue magnetic sensor (4"));
        Check("  the variant number is dropped from the one left", !html.Contains("<strong class=\"vt\">(2)"));
        Check("  the movement's devices at the top", html.Contains("Movement that failed") && html.Contains("<code>M604</code>") &&
              html.Contains("<code>Y24</code>") && html.Contains("<code>X30</code> Clamp closed") && html.Contains("<code>X31</code> Clamp open"));
        Check("  and the safety cautions are still there", html.Contains("CAUTION"));

        Console.WriteLine("--- the alarm list's help names the same movement ---");
        AlarmHelpLinks.Clear();
        AlarmHelpLinks.Harvest(unit);
        var listed = AlarmHelpLinks.DocumentsFor(4, "M811", null);
        Check("the error bit of B: movement B, Release", listed.Count >= 1 && listed[0].Movement != null &&
              listed[0].Movement.Side == "B" && listed[0].Movement.Name == "Release" && listed[0].Valve == 2,
              string.Join(" | ", listed.Select(d => d.Title)));
        helpA.UpdateState(a => 0, c => true);
        Check("cleared: blue again", mark.State == UnitHelpBadge.BadgeState.Info);

        Console.WriteLine("--- the full manual when the types are not set ---");
        var raw = new TC.HelpDocument { FileName = "Cylinder_Troubleshooting.html", Movement = unit.MovementFor("A") };
        var full = File.ReadAllText(TC.ExtractSection(raw));
        Check("every variant is kept, as separate tables (they are alternatives)",
              Regex.Matches(full, "<section class=\"block\"").Count == 15 && full.Contains("Movement that failed"));
        var untyped = new FunctionUnitHMI { ErrorAAddress = "M900", CommandAAddress = "M1" };
        untyped.SetBounds(10, 90, 360, 64); host.Controls.Add(untyped); Application.DoEvents();
        Check("a unit with no types and no files shows no '?' until an error", !Field<UnitHelpBadge>(untyped, "_help").Visible);

        Console.WriteLine("--- the servo manual has no Target column ---");
        var servo = File.ReadAllText(Path.Combine(TC.HelpFolder, "Servo_Alarm_Codes.en.html"));
        Check("no Target heading in any table", !servo.Contains("<div>Target</div>"));

        Console.WriteLine("--- the Cutting page: units the size of every other manual page ---");
        var fa = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Kaban.Machines.FA2010") ??
                 Assembly.LoadFrom("Kaban.Machines.FA2010.exe");
        var cutting = (Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens.Manual.ManualCutting"));
        var milling = (Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens.Manual.ManualMilling"));
        Func<Control, List<Control>> units = p => p.Controls.Cast<Control>().Where(c => c is FunctionUnitHMI || c is AxisUnitHMI).ToList();
        var cu = units(cutting);
        Check("20 units, every one 360 x 64", cu.Count == 20 && cu.All(c => c.Size == new Size(360, 64)),
              string.Join(",", cu.Select(c => c.Size).Distinct()));
        var mx = units(milling).Select(c => c.Left).Distinct().OrderBy(x => x).ToList();
        Check("  on the Milling page's columns", cu.Select(c => c.Left).Distinct().All(mx.Contains), string.Join(",", mx));
        bool overlap = cu.Any(a => cu.Any(b => a != b && a.Bounds.IntersectsWith(b.Bounds)));
        Check("  none overlapping", !overlap);
        Check("  rows 74 apart as on the other pages", cu.All(c => (c.Top - 16) % 74 == 0));

        Console.WriteLine("--- demo valve and sensor types on the FA2010 pages ---");
        var valves = new HashSet<CylinderValve>(); var feedbacks = new HashSet<CylinderFeedback>(); int typed = 0;
        foreach (var name in new[] { "ManualExit", "ManualMilling", "ManualCutting", "ManualClamping_Moulds", "ManualEntry_Conveyor", "ManualProfile_Recognition" })
        {
            var pg = (Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens.Manual." + name));
            foreach (var u in pg.Controls.OfType<FunctionUnitHMI>().Where(u => u.ValveType != CylinderValve.Unspecified))
            { typed++; valves.Add(u.ValveType); feedbacks.Add(u.FeedbackType); }
            pg.Dispose();
        }
        Check("nine units carry a valve and a feedback type, every valve and every feedback among them",
              typed == 9 && valves.Count == 3 && feedbacks.Count == 6, typed + " units, " + valves.Count + " valves, " + feedbacks.Count + " feedbacks");

        Console.WriteLine("--- the login: pick the user, type the password ---");
        string dir = Path.Combine(Path.GetTempPath(), "lg_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        typeof(UserStore).GetField("LastUserPath", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, Path.Combine(dir, "LastUser.txt"));
        UserStore.AddOrUpdateUser("lg_bench", "pw1", SecurityLevel.Operator);
        Check("no one has signed in yet: no last user", UserStore.LastUser == "");
        string err;
        Check("signing in", SecurityManager.TryLogin("LG_BENCH", "pw1", out err), err);
        Check("  remembers the name as the account has it", UserStore.LastUser == "lg_bench", UserStore.LastUser);
        SecurityManager.Logout();

        var login = new LoginForm();
        var combo = Field<ComboBox>(login, "cmbUser");
        var pass = Field<TextBox>(login, "txtPassword");
        login.Show(); Application.DoEvents();
        Check("the names are a list, not a text box", combo.DropDownStyle == ComboBoxStyle.DropDownList &&
              combo.Items.Cast<string>().Contains("lg_bench"));
        Check("  the last user already chosen", (string)combo.SelectedItem == "lg_bench");
        Check("  and the password box has the cursor", login.ActiveControl == pass,
              login.ActiveControl == null ? "none" : login.ActiveControl.GetType().Name);
        pass.Text = "wrong";
        Field<Button>(login, "btnOk").PerformClick();
        Check("a wrong password is still refused", SecurityManager.CurrentLevel == SecurityLevel.None);
        login.Close();

        var change = new LoginForm(chooseUser: true);
        var combo2 = Field<ComboBox>(change, "cmbUser");
        change.Show(); Application.DoEvents();
        Check("Change user: the list has the focus (and is opened)", change.ActiveControl == combo2,
              change.ActiveControl == null ? "none" : change.ActiveControl.GetType().Name);
        change.Close();

        UserStore.RemoveUser("lg_bench");
        Check("a removed account is not offered as the last user", UserStore.LastUser == "");
        var after = new LoginForm();
        Check("  and nothing is chosen for it", Field<ComboBox>(after, "cmbUser").SelectedIndex < 0 ||
              (string)Field<ComboBox>(after, "cmbUser").SelectedItem != "lg_bench");
        after.Dispose();
        try { Directory.Delete(dir, true); } catch { }

        host.Close();
        Console.WriteLine(fail == 0 ? "\nCYLINDER HELP AND LOGIN ALL PASS" : "\nCYLINDER HELP AND LOGIN " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
