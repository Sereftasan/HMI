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
// helper document" - on the real FA2010 program. And the second round: "give as much data as possible ...
// ask it to create a helper just like we create for the specific error ('Step by step') ... you can attach
// the whole file and ask the AI to create similar files for different level users, then the user asks it
// which part they don't understand and the AI explains that part more detailed".
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
        AlarmAiPrompt.SaveFolder = Path.Combine(dir, "AI prompts");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        ViewPreferences.Reload();
        AiChats.SettingsPath = Path.Combine(dir, "AiChats.cfg");
        AiChats.OpenLink = link => true;
        AlarmAiPrompt.ReadNow = (st, a) => st == 1 && a == "D16" ? 1234 : st == 1 && a == "Y55" ? 1 : (int?)null;
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
        // 9 October: the Cutting page's Right Front Clamping is a cylinder with an encoder now - the HMI watches it, and
        // names its alarms, by its encoder's row (Machine Parameters > Encoders): "Cutting Right Front"
        var cyl = AlarmGeneration.Cylinders.First(c => c.Unit == "Cutting Right Front");
        var side = cyl.Sides.First(x => x.ErrorAddress == "M852");
        NotesStore.Add(new Note { Kind = NoteKind.Comment, ControlKey = "x", ControlName = "Cutting Right Front - Back", Page = "FA2010 / Manual Movement / Cutting",
                                  Author = "mt1", Level = "Maintenance", Time = new DateTime(2026, 10, 7, 15, 0, 0), Text = "The encoder coupling was tightened on 7 October." });
        sim[1].Write("M852", 1);
        AlarmManager.Evaluate(1, a => a == "M852" ? 1 : 0);
        Pump(300);
        var item = AlarmManager.ActiveAlarms.FirstOrDefault(a => a.Station == 1 && a.Address == "M852");
        Check("its alarm on the list", item != null, string.Join(",", AlarmManager.ActiveAlarms.Select(a => a.Address)));
        if (item == null) { Environment.Exit(1); return; }
        var docs = AlarmHelpLinks.DocumentsFor(item);
        var p = AlarmAiPrompt.Build(item, null, docs);
        int ask = p.IndexOf("## What I ask you", StringComparison.Ordinal);
        Check("the prompt says what it holds first and asks LAST, after the data and the example",
              p.StartsWith("# Help me with an alarm on an industrial machine") && p.Contains("What I ask you is at the end") &&
              ask > p.IndexOf("```html", StringComparison.Ordinal) && ask > p.IndexOf("## The devices involved", StringComparison.Ordinal) && p.IndexOf("```html", StringComparison.Ordinal) > 0);
        Check("  helper documents for THIS alarm on THIS machine, in the HMI's language, as HTML files in the step-by-step page's format",
              p.Contains("Write me helper documents for THIS alarm on THIS machine, in English: complete HTML files in the same format, style and colours as the HMI's step-by-step page above"));
        Check("  one file for each reader: operator, maintenance technician, automation engineer",
              p.Contains("one file for each of these readers") && p.Contains("1. Operator - ") && p.Contains("2. Maintenance technician - ") && p.Contains("3. Automation engineer - "));
        Check("  the page's parts: the signal path, safety, numbered steps with good / bad results, drawings, the symptom table, self-contained",
              p.Contains("the signal path") && p.Contains("a safety box") && p.Contains("what a good and a bad result means") && p.Contains("inline SVG drawings") &&
              p.Contains("symptom - cause - remedy") && p.Contains("no external files, scripts or fonts"));
        Check("  safety first, the names as given, the most likely cause first, its questions",
              p.Contains("safety first") && p.Contains("do not invent device numbers") && p.Contains("most likely cause given the device states at the alarm") && p.Contains("your questions"));
        Check("  then it stays on: explains any part in more detail and asks what is seen",
              p.Contains("Then stay with me") && p.Contains("explain that part in more detail") && p.Contains("ask me what I see"));
        Check("  too long for one answer: the operator's first, then \"next\"", p.Contains("write the operator's first and go on with the next when I say \"next\""));
        Check("the machine: FA2010 Processing Center, the others on the panel, the PLC and its cards from the drawing",
              p.Contains("Machine: Processing Center - model FA2010") && p.Contains("3D Hinge Drilling (H800)") &&
              p.Contains("U4 FX5U-64MT-ESS") && p.Contains("U19 FX5-8AD") && p.Contains("PLC station 1 (FA2010 Main PLC)"));
        Check("  the HMI and its pages", p.Contains("Manual Movement (each cylinder is a unit") && p.Contains("Machine Parameters"));
        Check("  how the alarms are raised (the PLC's, the HMI's own with its delay and the SET, the sub-codes)",
              p.Contains("## How the alarms are raised") && p.Contains("within 5 s") && p.Contains("SETs that bit in the PLC") && p.Contains(".2 the target sensor did not come ON"));
        Check("the alarm: its text, code, device, time", p.Contains("\"Cutting Right Front Clamp Releasing Alarm\"") && p.Contains("PLC device of the alarm: M852") &&
              p.Contains("Came at: ") && p.Contains("Still active: yes"));
        Check("the unit: its name, valve, feedback, the movement, its output, its sensor, its position",
              p.Contains("Name on the HMI: Cutting Right Front") && p.Contains("closed center (3WayClosedCenter)") && p.Contains("Incremental encoder + reference sensor") &&
              p.Contains("The movement that failed: \"Back\"") && p.Contains("Its valve output: Y55") && p.Contains("Position reading: D16"));
        var sideLines = p.Split('\n').Where(l => l.StartsWith("  - Left button (A)") || l.StartsWith("  - Right button (B)")).ToList();
        Check("  BOTH its movements as the HMI watches them: outputs, sensors, error bits with their alarms", sideLines.Count == 2 &&
              sideLines.Any(l => l.Contains("driven by output Y55 ON") && l.Contains("error bit M852") && l.Contains("Releasing Alarm")) &&
              sideLines.Any(l => l.Contains("driven by output Y54 ON") && l.Contains("error bit M851")), string.Join(" // ", sideLines));
        int devs = p.IndexOf("## The devices involved", StringComparison.Ordinal);
        string devText = devs < 0 ? "" : p.Substring(devs, p.IndexOf("\n## ", devs + 5, StringComparison.Ordinal) - devs);
        Check("every device involved: its wiring from the drawings (output, relay, coil, card, air line), its PLC label",
              devText.Contains("- Y55 - ") && devText.Contains("relay K45") && devText.Contains("drives output Y55 (on the drawings; its line below)") && devText.Contains("PLC card: U") && devText.Contains("air: valve") &&
              devText.Contains("- M852 - ") && devText.Contains("- Y55 - Back: its valve output | PLC label OUT_Cikis_OnAC") &&
              devText.Contains("PLC label IN_CikisOn_Acili_Geride (X60)"), devText.Length > 600 ? devText.Substring(0, 600) : devText);
        Check("  its states read now (Y55 ON, D16 = 1234) and where on the HMI it is",
              devText.Contains("now ON") && devText.Contains("now 1234") && devText.Contains("on the HMI: ") && devText.Contains("Manual Movement"));
        Check("  the sensor with its input and its sensor tag", devText.Contains("- M232 - ") && devText.Contains("X60") && devText.Contains("SN76"));
        Check("  the note a user left on it", p.Contains("The encoder coupling was tightened on 7 October.") && p.Contains("Notes the machine's users left on this unit"));
        Check("the HMI's own help listed, and its step-by-step page's words in it (the wiring numbers)",
              p.Contains("Step by step - Cutting Right Front - Back") && p.Contains("Does the PLC switch on <code>Y55</code>?") && p.Contains("relay K45"));
        int html = p.IndexOf("```html", StringComparison.Ordinal), htmlEnd = html < 0 ? -1 : p.IndexOf("```", html + 7, StringComparison.Ordinal);
        string page = html < 0 || htmlEnd < 0 ? "" : p.Substring(html + 7, htmlEnd - html - 7);
        Check("  the step-by-step page IN FULL as the format: its HTML whole, with its style and drawings",
              page.TrimStart().StartsWith("<!DOCTYPE html>") && page.TrimEnd().EndsWith("</html>") && page.Contains("<style>") && page.Contains("<svg") &&
              page.Contains("Does the PLC switch on <code>Y55</code>?") && !page.Contains("{{") && p.Contains("in full below, as the format to follow"), page.Length + " characters");
        Check("  the other document (the cylinder manual) as text, shortened", p.Contains("### Cylinder troubleshooting - Cutting Right Front (its text)"));
        Check("  big, but a chat takes it (under 200 000 characters)", p.Length > 60000 && p.Length < 200000, p.Length + " characters");
        // the HMI's own alarm for the movement ("M873.1" in the feedback): what it saw, the states at the alarm
        var gen = new AlarmItem(item.AlarmCode + ".2", item.TranslationKey, AlarmSeverity.Fault)
        {
            Generated = true, Station = 1, Address = "M852", Device = "M852", SubCode = "2", Side = "A", BaseCode = item.AlarmCode, GroupCode = item.AlarmCode,
            Detail = "SN76 (X60) did not come ON - it stopped on the way, or the sensor does not see it", TriggerTime = DateTime.Now,
            Snapshot = new Dictionary<string, int> { { "Y55", 1 }, { "Y54", 0 }, { "M232", 0 } }
        };
        var gdocs = AlarmHelpLinks.DocumentsFor(gen);
        var gp = AlarmAiPrompt.Build(gen, null, gdocs);
        Check("the HMI's own alarm (M852.2): its code, raised by the HMI, what it saw fail, the states at the alarm",
              gp.Contains("- Code: M852.2") && gp.Contains("Raised by the HMI itself") && gp.Contains("What the HMI saw fail: SN76 (X60) did not come ON") &&
              gp.Contains("- Y55 - Back: its valve output") && gp.Contains("at the alarm ON") && gp.Contains("- M232 - ") && gp.Contains("at the alarm OFF"),
              string.Join(" / ", gp.Split('\n').Where(l => l.StartsWith("- Code") || l.StartsWith("- What") || l.StartsWith("- Y55") || l.StartsWith("- M232"))));
        Check("  its step-by-step page in full as the format", gdocs.Any(d => d.StepByStep) && gp.Contains("```html") && gp.Contains("as the format to follow"),
              string.Join(" / ", gdocs.Select(d => d.Title)));
        LanguageManager.ChangeLanguage("TR");
        var tr = AlarmAiPrompt.Build(item, null, AlarmHelpLinks.DocumentsFor(item));
        Check("in Turkish when the HMI is in Turkish (asked for, and the Turkish texts beside the English)",
              tr.Contains("in Turkish") && tr.Contains("Answer in Turkish.") && tr.Contains("(\"İşleme Merkezi\" on the panel)"));
        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine("--- the help list's last entry ---");
        var menu = MachineAlarmScreen.BuildHelpMenu(docs, d => { }, () => AlarmAiPrompt.Build(item, null, docs), form);
        var aiNames = menu.Items.OfType<ToolStripMenuItem>().Skip(docs.Count).Select(i => i.Name).ToList();
        var last = menu.Items.OfType<ToolStripMenuItem>().First(i => i.Name == "aiPrompt");
        // 9 October: "another member as 'Ask AI for help' to open an AI automatically as well as 'Create AI prompt'"
        // 12 October: every AI of the list, in its order - no "Ask another AI"; pictures as the user menu's
        Check("after the documents: Create AI prompt, then Ask <AI> for every chat of the list (Ask Claude, Ask ChatGPT ...)",
              string.Join(",", aiNames) == "aiPrompt," + string.Join(",", AiChats.All.Select(c => "aiAsk:" + c.Name)) && last.Text == "Create AI prompt" &&
              menu.Items.OfType<ToolStripMenuItem>().First(i => i.Name == "aiAsk:Claude").Text == "Ask Claude" &&
              menu.Items.OfType<ToolStripMenuItem>().Skip(docs.Count).All(i => i.Image != null),
              string.Join(" | ", menu.Items.OfType<ToolStripMenuItem>().Select(i => i.Text)));
        string noticed = null;
        AlarmAiPrompt.CopiedNotice = w => noticed = "copied";
        last.PerformClick();
        Pump(50);
        // 9 October: "is it not possible to copy the prompt file instead of the text? ... we could remove this long text popup"
        var saved = AlarmAiPrompt.LastSaved;
        System.Collections.Specialized.StringCollection clipFiles = null;
        try { clipFiles = Clipboard.GetFileDropList(); } catch { }
        Check("a click: the prompt FILE on the clipboard (Ctrl+V in a chat attaches it), named by its code",
              noticed == "copied" && AlarmAiPrompt.LastAsFile && saved != null && File.Exists(saved) && Path.GetFileName(saved).StartsWith("AI prompt FA2010.") &&
              saved.EndsWith(".txt") && File.ReadAllText(saved).StartsWith("# Help me with an alarm") && File.ReadAllText(saved).Contains("M852") &&
              clipFiles != null && clipFiles.Count == 1 && clipFiles[0] == saved,
              (clipFiles == null ? "no file list" : string.Join(",", clipFiles.Cast<string>())) + " / " + (saved ?? "not saved"));
        Check("  said in one short line - no long message", AlarmAiPrompt.LastMessage == "The AI prompt is on the clipboard as a file: paste it into an AI chat (Ctrl+V) and send it.",
              AlarmAiPrompt.LastMessage);
        ViewPreferences.SetPanel(ViewPreferences.AiPromptAs, "Text");
        last.PerformClick();
        Pump(50);
        string clip = null;
        try { clip = Clipboard.GetText(); } catch { }
        Check("as text when that is the user's choice (AI prompt copied as: Its text)", !AlarmAiPrompt.LastAsFile && clip != null && clip.StartsWith("# Help me with an alarm") &&
              clip.Contains("M852") && AlarmAiPrompt.LastMessage == "The AI prompt is on the clipboard: paste it into an AI chat (Ctrl+V) and send it.",
              clip == null ? "no clipboard" : clip.Length + " characters");
        ViewPreferences.SetPanel(ViewPreferences.AiPromptAs, null);

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
              Convert.ToString(bareRow.Cells["Help"].FormattedValue) == "Create AI prompt",
              bareRow == null ? "no row" : Convert.ToString(bareRow.Cells["Help"].FormattedValue));
        Check("the texts in every language", new[] { "Hlp_AiPromptItem", "Hlp_AiPrompt", "Tip_AiPrompt", "Msg_AiPromptFile", "Msg_AiPromptText", "Msg_AiPromptFailed", "Hlp_AiAskItem", "Msg_AiAsked", "Msg_AiAskedFile", "VP_AiPromptAs", "VP_AiAsFile", "VP_AiAsText", "PV_AiAs" }
              .All(k => new[] { "EN", "TR", "RU", "DE", "IT", "ES" }.All(l => !string.IsNullOrEmpty(LanguageManager.GetStringIn(l, k)))));
        host.Close();
        form.Close();
        Console.WriteLine(fail == 0 ? "\nAI PROMPT ALL PASS" : "\nAI PROMPT " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
