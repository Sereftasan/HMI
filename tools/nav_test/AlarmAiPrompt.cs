using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.Core.Models;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;
using Kaban.UI.Screens;

// 8 October: "It is possible to create an AI prompt for each alarm as the last combo member. Then the
// click creates the best prompt for an AI and copies to the clipboard and the user opens any AI ... and
// paste to it straight ... the prompt should conclude everything useful for the AI to create them a useful
// helper document" - on the real FA2010 program.
class AAP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "aap_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        AlarmGeneration.SettingsPath = Path.Combine(dir, "AlarmGeneration.cfg");
        NotesStore.Folder = Path.Combine(dir, "Notes");
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        var sim = new Dictionary<int, Kaban.Drivers.Simulation.SimulationDriver>();
        foreach (int st in new[] { 1, 2, 3, 4 }) { sim[st] = new Kaban.Drivers.Simulation.SimulationDriver(st); PlcManager.Instance.RegisterDriver(st, sim[st]); }
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show();
        Pump(300);
        AlarmHelpLinks.EnsureDiscovered();
        Pump(300);

        Console.WriteLine("--- an alarm on the list: the PLC's bit for a cylinder's movement ---");
        var cyl = AlarmGeneration.Cylinders.First(c => c.Unit == "Right Front Clamping");
        var side = cyl.Sides.First(x => x.ErrorAddress == "M852");
        NotesStore.Add(new Note { Kind = NoteKind.Comment, ControlKey = "x", ControlName = "Right Front Clamping - Back", Page = "FA2010 / Manual Movement / Cutting",
                                  Author = "mt1", Level = "Maintenance", Time = new DateTime(2026, 10, 7, 15, 0, 0), Text = "The encoder coupling was tightened on 7 October." });
        sim[1].Write("M852", 1);
        AlarmManager.Evaluate(1, a => a == "M852" ? 1 : 0);
        Pump(300);
        var item = AlarmManager.ActiveAlarms.FirstOrDefault(a => a.Station == 1 && a.Address == "M852");
        Check("its alarm on the list", item != null, string.Join(",", AlarmManager.ActiveAlarms.Select(a => a.Address)));
        if (item == null) { Environment.Exit(1); return; }
        var docs = AlarmHelpLinks.DocumentsFor(item);
        var p = AlarmAiPrompt.Build(item, null, docs);
        Check("the prompt asks for a helper document for this alarm on this machine, in the HMI's language",
              p.StartsWith("# Help me with an alarm on an industrial machine") && p.Contains("write me a practical helper document for THIS alarm on THIS machine, in English"));
        Check("  in three levels: operator, maintenance technician, automation engineer",
              p.Contains("1. Operator - ") && p.Contains("2. Maintenance technician - ") && p.Contains("3. Automation engineer - "));
        Check("  safety first, the names as given, a symptom table, its questions at the end",
              p.Contains("safety first") && p.Contains("do not invent device numbers") && p.Contains("symptom - cause - remedy") && p.Contains("questions"));
        Check("the machine: FA2010 Processing Center, the others on the panel, the PLC and its cards from the drawing",
              p.Contains("Machine: Processing Center - model FA2010") && p.Contains("3D Hinge Drilling (H800)") &&
              p.Contains("U4 FX5U-64MT-ESS") && p.Contains("U19 FX5-8AD") && p.Contains("PLC station 1 (FA2010 Main PLC)"));
        Check("  the HMI and its pages", p.Contains("Manual Movement (each cylinder is a unit") && p.Contains("Machine Parameters"));
        Check("the alarm: its text, code, device, time", p.Contains("\"Cutting Right Front Clamp Releasing Alarm\"") && p.Contains("PLC device of the alarm: M852") &&
              p.Contains("Came at: ") && p.Contains("Still active: yes"));
        Check("the unit: its name, valve, feedback, the movement, its output, its sensor, its position",
              p.Contains("Name on the HMI: Right Front Clamping") && p.Contains("closed center (3WayClosedCenter)") && p.Contains("Incremental encoder + reference sensor") &&
              p.Contains("The movement that failed: \"Back\"") && p.Contains("Its valve output: Y55") && p.Contains("Position reading: D16"));
        Check("  the note a user left on it", p.Contains("The encoder coupling was tightened on 7 October.") && p.Contains("Notes the machine's users left on this unit"));
        Check("the HMI's own help listed, and its step-by-step page's words in it (the wiring numbers)",
              p.Contains("Step by step - Right Front Clamping - Back") && p.Contains("Does the PLC switch on Y55?") && p.Contains("relay K45"));
        Check("  as text: no HTML, no drawings", !p.Contains("<div") && !p.Contains("<svg") && !p.Contains("{{"));
        Check("  not a book (a chat takes it)", p.Length < 40000, p.Length + " characters");
        LanguageManager.ChangeLanguage("TR");
        var tr = AlarmAiPrompt.Build(item, null, AlarmHelpLinks.DocumentsFor(item));
        Check("in Turkish when the HMI is in Turkish (asked for, and the Turkish texts beside the English)",
              tr.Contains("in Turkish") && tr.Contains("Answer in Turkish.") && tr.Contains("(\"İşleme Merkezi\" on the panel)"));
        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine("--- the help list's last entry ---");
        var menu = MachineAlarmScreen.BuildHelpMenu(docs, d => { }, () => AlarmAiPrompt.Build(item, null, docs), form);
        var last = menu.Items[menu.Items.Count - 1];
        Check("the AI prompt, last, after the documents", last.Name == "aiPrompt" && last.Text == "AI prompt - copy it for an AI chat" &&
              menu.Items.OfType<ToolStripMenuItem>().Count() == docs.Count + 1, last.Text);
        string noticed = null;
        AlarmAiPrompt.CopiedNotice = w => noticed = "copied";
        last.PerformClick();
        Pump(50);
        string clip = null;
        try { clip = Clipboard.GetText(); } catch { }
        Check("a click: on the clipboard, and said so", noticed == "copied" && clip != null && clip.StartsWith("# Help me with an alarm") && clip.Contains("M852"),
              clip == null ? "no clipboard" : clip.Length + " characters");

        Console.WriteLine("--- the alarm list ---");
        var screen = MachineRegistry.AllScreens().First(kv => kv.Key.Context.ModuleId == "FA2010" && kv.Value.Role == ScreenRoles.Alarms).Value;
        var alarms = (Control)screen.CreateView();
        var host = new Form { ClientSize = new System.Drawing.Size(1200, 500) };
        alarms.Dock = DockStyle.Fill;
        host.Controls.Add(alarms);
        host.Show();
        Pump(300);
        var grid = (DataGridView)typeof(MachineAlarmScreen).GetField("_grid", Any).GetValue(alarms);
        var row = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.DataBoundItem == item);
        Check("its row: the Help cell shows its first document", row != null && Convert.ToString(row.Cells["Help"].FormattedValue).StartsWith("Step by step"),
              row == null ? "no row" : Convert.ToString(row.Cells["Help"].FormattedValue));
        // an alarm with no document of its own: its AI prompt all the same
        var bare = new AlarmItem("FA2010.Bare", "Bare alarm", AlarmSeverity.Warning) { Generated = true, Station = 1, Address = "M999", TriggerTime = DateTime.Now };
        AlarmManager.RaiseGenerated(bare);
        Pump(300);
        var bareRow = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.DataBoundItem == bare);
        Check("an alarm with no document: its Help cell offers the AI prompt", bareRow != null &&
              Convert.ToString(bareRow.Cells["Help"].FormattedValue) == "AI prompt - copy it for an AI chat",
              bareRow == null ? "no row" : Convert.ToString(bareRow.Cells["Help"].FormattedValue));
        Check("the texts in every language", new[] { "Hlp_AiPromptItem", "Hlp_AiPrompt", "Tip_AiPrompt", "Msg_AiPromptCopied", "Msg_AiPromptFailed" }
              .All(k => new[] { "EN", "TR", "RU", "DE", "IT", "ES" }.All(l => !string.IsNullOrEmpty(LanguageManager.GetStringIn(l, k)))));
        host.Close();
        form.Close();
        Console.WriteLine(fail == 0 ? "\nAI PROMPT ALL PASS" : "\nAI PROMPT " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
