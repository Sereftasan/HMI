using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

// THE ALARMS THE HMI RAISES ITSELF, on the screens (1 October): switched on and
// timed in Alarm Settings (the activation, the general delay, a Delay column);
// a unit's "?" and its step-by-step page for the signal that failed; the alarm
// list showing M844.2 and opening that page.
class AGU {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static T Field<T>(object o, string n) { return (T)o.GetType().GetField(n, Any).GetValue(o); }
    static Dictionary<string, int> mem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    static int Read(string a) { int v; return mem.TryGetValue(a, out v) ? v : 0; }
    static DateTime now = new DateTime(2026, 10, 1, 9, 0, 0);
    static CylinderUnitHMI u;
    static void Scan() {
        now = now.AddSeconds(0.1);
        AlarmGeneration.Evaluate(1, Read, now);
        AlarmManager.Evaluate(1, Read);
        Field<AlarmHelpBadge>(u, "_helpA").UpdateState(Read, c => true);
        Field<AlarmHelpBadge>(u, "_helpB").UpdateState(Read, c => true);
        Application.DoEvents();
    }
    static void Run(double s) { for (double t = 0; t < s - 1e-9; t += 0.1) Scan(); }
    static List<string> Titles(string html) {
        return Regex.Matches(html, "<h3>(.*?)</h3>").Cast<Match>().Select(m => Regex.Replace(m.Groups[1].Value, "<[^>]+>", "").Replace(" start here", "")).ToList();
    }
    static IEnumerable<Control> All(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var x in All(k)) yield return x; } }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        WiringTable.Clear();
        WiringTable.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wiring.csv"), plc => plc);
        var dir = Path.Combine(Path.GetTempPath(), "AlarmGenUi"); Directory.CreateDirectory(dir);
        AlarmGeneration.SettingsPath = Path.Combine(dir, "AlarmGeneration.cfg");
        if (File.Exists(AlarmGeneration.SettingsPath)) File.Delete(AlarmGeneration.SettingsPath);
        Kaban.UI.Helpers.AlarmHelpLinks.DiscoverAll();       // nothing mounted: done at once
        AlarmManager.RegisterDefinition("T.Clamp", "Front clamping did not clamp", AlarmSeverity.Fault, plcNumber: 1, address: "M844");
        AlarmManager.RegisterDefinition("T.Release", "Front clamping did not release", AlarmSeverity.Fault, plcNumber: 1, address: "M845");
        AlarmManager.RegisterDefinition("T.Door", "Door open", AlarmSeverity.Warning, plcNumber: 1, address: "M900");
        SecurityManager.Login("m", SecurityLevel.Maintenance);
        var host = new Form { ClientSize = new Size(1000, 600) };

        Console.WriteLine("--- a unit's movements ---");
        u = new CylinderUnitHMI {
            PlcNumber = 1, TitleTextKey = "Front Clamping", Name = "unitFC", ButtonBText = "Clamp", ButtonAText = "Release",
            CommandAAddress = "M646", Lamp1AAddress = "M225", CommandBAddress = "M645", Lamp1BAddress = "M240",
            ErrorAAddress = "M845", ErrorBAddress = "M844",
            ValveType = CylinderValve.ThreeWayClosedCenter, FeedbackType = CylinderFeedback.MagneticPlusPressureSwitch };
        u.SetBounds(10, 10, 360, 64); host.Controls.Add(u); host.Show(); Application.DoEvents();
        var w = u.BuildWatch();
        Check("its watch: each movement by the output its command drives on the drawings, confirmed by its end's sensor, the other end's OFF",
              w != null && w.Sides.Count == 2 &&
              w.Sides.Single(s => s.Side == "B").Drive == "Y32" && w.Sides.Single(s => s.Side == "B").OtherDrive == "Y33" &&
              w.Sides.Single(s => s.Side == "B").MustOn.Single().Address == "M240" && w.Sides.Single(s => s.Side == "B").MustOff.Single().Address == "M225" &&
              w.Sides.Single(s => s.Side == "A").Drive == "Y33" && w.Sides.Single(s => s.Side == "B").Code == "M844",
              w == null ? "null" : string.Join(" | ", w.Sides.Select(s => s.Side + ":" + s.Drive + "/" + s.OtherDrive + " on " +
                  string.Join(",", s.MustOn.Select(x => x.Address)) + " off " + string.Join(",", s.MustOff.Select(x => x.Address)))));
        Check("  the sensors named as the drawings name them", w.Sides.Single(s => s.Side == "B").MustOn.Single().Name.Contains("X70"),
              w.Sides.Single(s => s.Side == "B").MustOn.Single().Name);
        w.ModuleId = "T";
        AlarmGeneration.Register(w);
        foreach (var l in u.HelpLinks()) Kaban.UI.Helpers.AlarmHelpLinks.Register(l);   // as discovery does

        Console.WriteLine("--- Alarm Settings ---");
        var set = new Kaban.UI.Settings.AlarmSettingsControl("T") { Dock = DockStyle.None, Bounds = new Rectangle(0, 100, 1000, 480) };
        host.Controls.Add(set); Application.DoEvents();
        var on = All(set).OfType<CheckBox>().FirstOrDefault(c => c.Name == "GenerationOn");
        var delay = All(set).OfType<NumericUpDown>().FirstOrDefault(c => c.Name == "GenerationDelay");
        var grid = All(set).OfType<DataGridView>().First();
        var axesOn = All(set).OfType<CheckBox>().FirstOrDefault(c => c.Name == "GenerationAxes");
        var delayCaption = All(set).OfType<Label>().FirstOrDefault(l => l.Text.EndsWith("delay (s):"));
        Check("the cylinders' activation (off) and their delay (5 s) above the table", on != null && !on.Checked && delay != null && delay.Value == 5 &&
              on.Text == "The HMI raises the cylinder alarms itself" && delayCaption != null && delayCaption.Text == "Cylinder alarm delay (s):",
              (on == null ? "" : on.Text) + " / " + (delayCaption == null ? "no caption" : delayCaption.Text));
        Check("  the axes' own switch, on unless switched off", axesOn != null && axesOn.Checked && axesOn.Text == "The HMI raises the axis and inverter alarms itself");
        Check("  a Cylinder delay column", grid.Columns.Contains("Delay") && grid.Columns["Delay"].HeaderText == "Cylinder delay (s)" &&
              grid.Columns["Delay"].Index == grid.Columns["Severity"].Index + 1);
        Func<string, DataGridViewRow> row = a => grid.Rows.Cast<DataGridViewRow>().First(r => Convert.ToString(r.Cells["Address"].Value) == a);
        Check("  editable for a movement's alarm (M844, M845), grey for any other (M900)",
              !row("M844").Cells["Delay"].ReadOnly && !row("M845").Cells["Delay"].ReadOnly && row("M900").Cells["Delay"].ReadOnly &&
              row("M900").Cells["Delay"].ToolTipText.Contains("does not time it"));
        on.Checked = true; delay.Value = 4; row("M845").Cells["Delay"].Value = "2.5";
        Check("  the edits make it unsaved", Field<DirtyTracker>(set, "_dirty").IsDirty);
        set.GetType().GetMethod("Save", Any).Invoke(set, null);
        Check("saved: on, 4 s, M845 2.5 s, M844 the general one",
              AlarmGeneration.EnabledFor("T") && AlarmGeneration.For("T").DelaySeconds == 4 &&
              AlarmManager.FindByCode("T.Release").GenerationDelay == 2.5 && AlarmManager.FindByCode("T.Clamp").GenerationDelay == null);
        set.GetType().GetMethod("LoadRows", Any).Invoke(set, null);
        Check("  read back", on.Checked && delay.Value == 4 && Convert.ToString(row("M845").Cells["Delay"].Value) == "2.5" &&
              Convert.ToString(row("M844").Cells["Delay"].Value) == "");
        host.Controls.Remove(set); set.Dispose();

        Console.WriteLine("--- the clamp did not clamp: M844.2 ---");
        mem["M225"] = 1; Scan();
        mem["Y32"] = 1; Scan(); mem["M225"] = 0; Run(3.8);
        Check("nothing before its delay (4 s)", !AlarmManager.ActiveAlarms.Any());
        Run(0.4);
        var g = AlarmManager.ActiveAlarms.FirstOrDefault(a => a.Generated);
        Check("then the clamp's alarm with the signal that failed: the pressure switch did not come ON", g != null && g.ShownCode == "M844.2" &&
              g.Description.StartsWith("Front clamping did not clamp - ") && g.Description.Contains("X70"), g == null ? "" : g.ShownCode + " " + g.Description);
        var hb = Field<AlarmHelpBadge>(u, "_helpB");
        Check("the unit knows: its clamp side is in error (no PLC bit needed), the '?' red",
              hb.IsErrorSet && hb.GeneratedItem == g && Field<UnitHelpBadge>(u, "_help").State == UnitHelpBadge.BadgeState.Error &&
              !Field<AlarmHelpBadge>(u, "_helpA").IsErrorSet);
        Check("  its tip names the alarm with its sub-number", hb.TipText.StartsWith("M844.2 Front clamping did not clamp"), hb.TipText);
        var docs = u.HelpDocuments();
        Check("  the '?' opens the step-by-step page for that failure first", docs.Count >= 2 && docs[0].StepByStep && docs[0].Failure == 2 &&
              docs[0].Title.EndsWith("(M844.2)") && docs[0].Movement.Feedback[0].AtAlarm == 0 && docs[0].Movement.Output.AtAlarm == 1,
              string.Join(" | ", docs.Select(d => d.Title)));
        var html = File.ReadAllText(TC.WriteSteps(docs[0]));
        var t = Titles(html);
        Check("  only about the switch that did not come ON: where is it, the way, the switch, the time - not the valve, not the back sensor",
              t[0] == "Where is the cylinder now?" && t.Any(x => x.Contains("SN83") && x.Contains("switch?")) && t.Any(x => x.Contains("whole way")) &&
              !t.Any(x => x.StartsWith("Does the output card")) && !t.Any(x => x.Contains("gone OFF")), string.Join(" | ", t));
        Check("  what the HMI saw: the output and the two sensors only, when its time ran out",
              Regex.Matches(Regex.Match(html, "<table class=\"states\">.*?</table>", RegexOptions.Singleline).Value, "<tr>").Count == 4 &&
              html.Contains("when the HMI's time for the movement ran out"));

        mem["M844"] = 1; Scan();
        Check("the PLC's own M844 meanwhile: one line, not two", AlarmManager.ActiveAlarms.Count == 1);

        Console.WriteLine("--- the alarm list ---");
        var screen = new Kaban.UI.Screens.MachineAlarmScreen(null) { Dock = DockStyle.None, Bounds = new Rectangle(0, 100, 1000, 480) };
        host.Controls.Add(screen); Application.DoEvents();
        var ag = Field<DataGridView>(screen, "_grid");
        // 8 October: "Alarm and alarm history pages have different columns ... both table show same values. Then we
        // add 'Address' value to both of the tables ... (seen by just admin level users). Also the new HMI created alarms
        // has extra info after the alarm ... another column to keep and show this data ... 'Cause' or something"
        Func<DataGridView, int, string, string> cell = (gr, r, c) => Convert.ToString(gr.Rows[r].Cells[c].FormattedValue);
        Check("its code: the alarm's own (T.Clamp, as its PLC bit's), the text alone, what failed in Cause",
              ag.Rows.Count == 1 && cell(ag, 0, "AlarmCode") == "T.Clamp" && cell(ag, 0, "Description") == "Front clamping did not clamp" &&
              cell(ag, 0, "Cause").Length > 0 && !cell(ag, 0, "Description").Contains(cell(ag, 0, "Cause")),
              ag.Rows.Count == 0 ? "" : cell(ag, 0, "AlarmCode") + " | " + cell(ag, 0, "Description") + " | " + cell(ag, 0, "Cause"));
        Check("  the columns: Code, Alarm, Cause, Severity, Time, Address, Help",
              string.Join(",", ag.Columns.Cast<DataGridViewColumn>().OrderBy(c => c.DisplayIndex).Select(c => c.Name)) ==
              "AlarmCode,Description,Cause,Severity,TriggerTime,Address,Help");
        // 8 October (evening): the device alone - "the .2 is the alarm's code, nothing to do with the device address"
        Check("  the Address (M844, the device alone) for an Admin only: Maintenance does not see it", !ag.Columns["Address"].Visible && cell(ag, 0, "Address") == "M844",
              cell(ag, 0, "Address"));
        SecurityManager.Login("boss", SecurityLevel.Admin); Application.DoEvents();
        Check("  an Admin does", ag.Columns["Address"].Visible);
        SecurityManager.Login("m", SecurityLevel.Maintenance); Application.DoEvents();
        var listDocs = (List<TC.HelpDocument>)screen.GetType().GetMethod("HelpDocumentsFor", Any).Invoke(screen, new object[] { ag.Rows[0].DataBoundItem });
        Check("  its help: that failure's step-by-step page first, then the manual (not the PLC bit's step-by-step)",
              listDocs.Count >= 2 && listDocs[0].StepByStep && listDocs[0].Failure == 2 && listDocs[0].Title.Contains("Front Clamping") &&
              !listDocs.Skip(1).Any(d => d.StepByStep) && listDocs[1].Valve != 0,
              string.Join(" | ", listDocs.Select(d => d.Title)));
        var hist = (UserControl)screen.GetType().GetMethod("BuildHistory", Any).Invoke(screen, new object[] { null });
        host.Controls.Add(hist); hist.Bounds = new Rectangle(0, 100, 1000, 300); Application.DoEvents();
        var hg = All(hist).OfType<DataGridView>().First();
        int hr = hg.Rows.Cast<DataGridViewRow>().Select(r => r.Index).FirstOrDefault(i => cell(hg, i, "Address") == "M844");
        Check("  the history: the same columns, the same values", string.Join(",", hg.Columns.Cast<DataGridViewColumn>().OrderBy(c => c.DisplayIndex).Select(c => c.Name)) ==
              "AlarmCode,Description,Cause,Severity,TriggerTime,Address,Help" && cell(hg, hr, "AlarmCode") == "T.Clamp" &&
              cell(hg, hr, "Cause") == cell(ag, 0, "Cause") && !hg.Columns["Address"].Visible && cell(hg, hr, "Help").StartsWith("Step by step"),
              cell(hg, hr, "AlarmCode") + " | " + cell(hg, hr, "Help"));
        var csv = Kaban.UI.Screens.MachineAlarmScreen.HistoryCsv(new[] { (AlarmItem)hg.Rows[hr].DataBoundItem });
        Check("  its file the same (no Address below Admin)", csv.StartsWith("Time;Code;Alarm;Cause;Severity\r\n") && csv.Contains(";T.Clamp;Front clamping did not clamp;"), csv);
        hist.Dispose();

        Console.WriteLine("--- it clamps after all ---");
        mem["M844"] = 0; mem["M240"] = 1; Scan(); Application.DoEvents();
        Check("the alarm goes, the unit's error with it", !AlarmManager.ActiveAlarms.Any() && !hb.IsErrorSet &&
              Field<UnitHelpBadge>(u, "_help").State != UnitHelpBadge.BadgeState.Error);

        Console.WriteLine("--- the release's own delay (2.5 s) ---");
        mem["Y32"] = 0; Scan(); mem["Y33"] = 1; Run(2.6);
        g = AlarmManager.ActiveAlarms.FirstOrDefault(a => a.Generated);
        Check("M845.1: the pressure switch stayed ON (the clamp did not let go) after 2.5 s", g != null && g.ShownCode == "M845.1", g == null ? "" : g.ShownCode);
        Check("  its page asks whether it left, then the valve onwards or the switch",
              Titles(File.ReadAllText(TC.WriteSteps(u.HelpDocuments()[0])))[0] == "Has the cylinder left its start?");

        Console.WriteLine("--- switched off ---");
        set = new Kaban.UI.Settings.AlarmSettingsControl("T");
        on = All(set).OfType<CheckBox>().First(c => c.Name == "GenerationOn");
        on.Checked = false;
        set.GetType().GetMethod("Save", Any).Invoke(set, null);
        Scan();
        Check("its alarms go, nothing is timed", !AlarmManager.ActiveAlarms.Any() && !AlarmGeneration.Addresses().Any() && !hb.IsErrorSet);
        set.Dispose(); hist.Dispose();

        host.Close();
        Console.WriteLine(fail == 0 ? "\nALARM GENERATION UI ALL PASS" : "\nALARM GENERATION UI " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
