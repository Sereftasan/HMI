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
        var props = TypeDescriptor.GetProperties(typeof(CylinderUnitHMI));
        // The category as shown carries the tabs that order it (Kaban.UI.Design.PropertyOrder).
        string vf = props["ValveType"] == null ? "" : props["ValveType"].Category;
        Check("ValveType and FeedbackType head the Properties window (Valve & Feedback, first category)",
              props["ValveType"] != null && props["ValveType"].IsBrowsable && vf.TrimStart('\t') == "Valve & Feedback" &&
              props["FeedbackType"] != null && props["FeedbackType"].IsBrowsable && props["FeedbackType"].Category == vf &&
              props.Cast<PropertyDescriptor>().Where(p => p.IsBrowsable).All(p => string.CompareOrdinal(p.Category, vf) >= 0),
              vf);
        Check("  the old names are hidden aliases (not listed, not written by the designer)",
              !props["Valve"].IsBrowsable && !props["Feedback"].IsBrowsable &&
              props["Valve"].SerializationVisibility == DesignerSerializationVisibility.Hidden);
        Check("  the valve names read as the manual's", Enum.GetNames(typeof(CylinderValve)).SequenceEqual(
              new[] { "Unspecified", "DoubleSolenoid", "SingleSolenoid", "DoubleSolenoid_3Way" }));
        Check("  with the manual's numbers kept", (int)CylinderValve.DoubleSolenoid == 1 && (int)CylinderValve.SingleSolenoid == 2 &&
              (int)CylinderValve.DoubleSolenoid_3Way == 3 && (int)CylinderFeedback.EncoderPlusReference == 6);

        var unit = new CylinderUnitHMI {
            PlcNumber = 4, TitleTextKey = "Left Top Clamping",
            CommandAAddress = "M604", ButtonATextKey = "Clamp", OutputAAddress = "Y24", Lamp1AAddress = "X30", Lamp1ATipKey = "Clamp closed",
            CommandBAddress = "M605", ButtonBTextKey = "Release", OutputBAddress = "Y25", Lamp1BAddress = "X31", Lamp1BTipKey = "Clamp open",
            ErrorAAddress = "M810", ErrorBAddress = "M811",
            ValveType = CylinderValve.SingleSolenoid, FeedbackType = CylinderFeedback.TwoEndMagnetic };
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
        Check("the '?' offers the fault's help - and stays blue", mark.State == UnitHelpBadge.BadgeState.Error && mark.MarkColor.ToArgb() == UnitHelpBadge.Blue.ToArgb());
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
        var details = Regex.Matches(html, "<tbody class=\"block\" data-detail=\"([^\"]*)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
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
        Check("every variant is kept - in ONE chart, one heading row (no heading between the details)",
              Regex.Matches(full, "<tbody class=\"block\"").Count == 15 && Regex.Matches(full, "<thead>").Count == 1 &&
              Regex.Matches(full, "<table class=\"detail\"").Count == 1 && full.Contains("Movement that failed"));
        var manualFile = File.ReadAllText(Path.Combine(TC.HelpFolder, "Cylinder_Troubleshooting.html"));
        Check("  the manual itself is one chart too, its heading row fixed at the top while it scrolls",
              Regex.Matches(manualFile, "<thead>").Count == 1 && Regex.Matches(manualFile, "<tbody class=\"block\"").Count == 15 &&
              manualFile.Contains("thead th{position:sticky;top:0"));
        Check("  an old copy made of a table per detail is joined into one",
              Regex.Matches(TC.JoinBlocks("<section class=\"block\" data-detail=\"1.1\"><table><thead><tr><th>h</th></tr></thead><tbody><tr><td>a</td></tr></tbody></table></section>" +
                                          "<section class=\"block\" data-detail=\"1.2\"><table><thead><tr><th>h</th></tr></thead><tbody><tr><td>b</td></tr></tbody></table></section>"),
                            "<thead>").Count == 1);
        var untyped = new CylinderUnitHMI { ErrorAAddress = "M900", CommandAAddress = "M1" };
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
        Func<Control, List<Control>> units = p => p.Controls.Cast<Control>().Where(c => c is CylinderUnitHMI || c is AxisUnitHMI).ToList();
        // 29 September: a single-function unit became a button or a value (CylinderUnitHMI is
        // for cylinders) - in the same slot of the page's grid.
        Func<Control, List<Control>> singles = p => p.Controls.Cast<Control>().Where(c => c is ButtonHMI || c is NumericDisplayHMI).ToList();
        var cu = units(cutting);
        var cs = singles(cutting);
        Check("20 places: the units every one 360 x 64, the single functions a button or a value",
              cu.Count + cs.Count == 20 && cu.All(c => c.Size == new Size(360, 64)) && cs.Count > 0,
              cu.Count + " + " + cs.Count + ": " + string.Join(",", cu.Select(c => c.Size).Distinct()));
        var mx = units(milling).Select(c => c.Left).Distinct().OrderBy(x => x).ToList();
        Func<Control, int> column = c => mx.LastOrDefault(x => x <= c.Left);
        Check("  on the Milling page's columns", cu.Select(c => c.Left).Distinct().All(mx.Contains) &&
              cs.All(c => mx.Any(x => x <= c.Left && c.Right <= x + 360)), string.Join(",", mx));
        var all = cu.Concat(cs).ToList();
        bool overlap = all.Any(a => all.Any(b => a != b && a.Bounds.IntersectsWith(b.Bounds)));
        Check("  none overlapping", !overlap);
        Check("  rows 74 apart as on the other pages (a button or value inside its row's 64)",
              cu.All(c => (c.Top - 16) % 74 == 0) && cs.All(c => (c.Top - 16) % 74 + c.Height <= 64),
              string.Join(",", cs.Select(c => c.Name + "@" + c.Top)));

        Console.WriteLine("--- valve and sensor types on the FA2010 pages ---");
        var valves = new HashSet<CylinderValve>(); int typed = 0;
        var byName = new Dictionary<string, CylinderUnitHMI>();
        var pagesBuilt = new List<Control>();
        foreach (var name in new[] { "ManualExit", "ManualMilling", "ManualCutting", "ManualClamping_Moulds", "ManualEntry_Conveyor", "ManualProfile_Recognition" })
        {
            var pg = (Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens.Manual." + name));
            pagesBuilt.Add(pg);
            foreach (var u in pg.Controls.OfType<CylinderUnitHMI>().Where(u => u.ValveType != CylinderValve.Unspecified))
            { typed++; valves.Add(u.ValveType); byName[name + "/" + u.TitleTextKey] = u; }
        }
        Check("nine units carry a valve and a feedback type, every valve among them", typed == 9 && valves.Count == 3, typed + " units, " + valves.Count + " valves");
        Func<string, CylinderValve, CylinderFeedback, bool> asDrawn = (key, v, f) =>
            byName.ContainsKey(key) && byName[key].ValveType == v && byName[key].FeedbackType == f && byName[key].TypeCheck == "OK";
        Check("Milling Top Clamping as drawn: 5/3 (Y33/Y34), up sensor + encoder",
              asDrawn("ManualMilling/Top Clamping", CylinderValve.DoubleSolenoid_3Way, CylinderFeedback.EncoderPlusReference),
              byName.ContainsKey("ManualMilling/Top Clamping") ? byName["ManualMilling/Top Clamping"].TypeCheck : "missing");
        Check("Exit Robot Top Clamping as drawn: 5/3 (Y45/Y46), up sensor + encoder",
              asDrawn("ManualExit/Robot Top Clamping", CylinderValve.DoubleSolenoid_3Way, CylinderFeedback.EncoderPlusReference));
        Check("Exit Short Profile Support as drawn: 5/2 double solenoid (Y71/Y72), a sensor at each end",
              asDrawn("ManualExit/Short Profile Support", CylinderValve.DoubleSolenoid, CylinderFeedback.TwoEndMagnetic));
        Check("Profile Recognition Front Clamping as drawn: 5/3 (Y28/Y29), back sensor + pressure switch (and its measuring position)",
              asDrawn("ManualProfile_Recognition/Front Clamping", CylinderValve.DoubleSolenoid_3Way, CylinderFeedback.MagneticPlusPressureSwitch));
        Check("Cutting Left Top Small Clamping: one up sensor (SN64); Right Front Clamping: back sensor + encoder",
              asDrawn("ManualCutting/Left Top Small Clamping", CylinderValve.DoubleSolenoid, CylinderFeedback.SingleMagnetic) &&
              asDrawn("ManualCutting/Right Front Clamping", CylinderValve.DoubleSolenoid_3Way, CylinderFeedback.EncoderPlusReference));
        // Left Profile Recognition Front Vertical: its "lamps" are D750 / D150, words the
        // drawings do not show - its feedback is for the machine's builder to say.
        var open = byName.Where(kv => kv.Value.TypeCheck != "OK").Select(kv => kv.Key).ToList();
        Check("every other typed unit's wiring suits its types (one left open: Left Profile Recognition Front Vertical)",
              open.Count == 1 && open[0] == "ManualClamping_Moulds/Left Profile Recognition Front Vertical", string.Join(" | ", open));
        foreach (var pg in pagesBuilt) pg.Dispose();

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
