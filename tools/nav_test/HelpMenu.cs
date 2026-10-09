using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 9 October: "We should open a dropdown menu in between language and screen scale selection for the HMI helper
// ('?' sign could be used as the emoji) and move 'HMI Settings / About' page here ... The first element should be
// about (the version info and stuff), second 'Open the HMI manual', third 'Ask AI for Help'. We should create a
// prompt for the AI (explaining the HMI entirely and our current already created manual attached) ... Is it
// possible and sensible if we enter the AI links somewhere at the application and add 'Preferred AI' to
// 'Preferences' of each user. Then we can add another member as 'Ask AI for help' to open an AI automatically
// as well as 'Create AI prompt'." - on the real FA2010 program.
class HM {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static object Get(object o, string n) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, Any); if (f != null) return f.GetValue(o); } return null; }
    static IEnumerable<Control> All(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; } }
    static ToolStripMenuItem Item(ContextMenuStrip m, string name) { return m.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Name == name); }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "hm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        ViewPreferences.Reload();
        AiChats.SettingsPath = Path.Combine(dir, "AiChats.cfg");
        AlarmAiPrompt.SaveFolder = Path.Combine(dir, "AI prompts");
        NotesStore.Folder = Path.Combine(dir, "Notes");
        var opened = new List<string>();
        AiChats.OpenLink = link => { opened.Add(link); return true; };
        string openedFile = null;
        AboutControl.OpenFile = path => { openedFile = path; return true; };
        string noticed = null;
        AlarmAiPrompt.CopiedNotice = w => noticed = AlarmAiPrompt.LastMessage;
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 }) PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        var bounds = new System.Drawing.Rectangle(0, 0, 1270, 900);
        form.StartPosition = FormStartPosition.Manual; form.Bounds = bounds;
        form.Load += (s, e) => { form.WindowState = FormWindowState.Normal; form.Bounds = bounds; SecurityManager.Login("admin", SecurityLevel.Admin); };
        form.Show();
        Pump(500);

        Console.WriteLine("--- the ? on the caption bar ---");
        var help = (Button)Get(form, "_btnHelp");
        var zoom = (Button)Get(form, "_btnZoom");
        var lang = (Button)Get(form, "_btnLang");
        Check("between the screen scale and the language", help != null && help.Visible && zoom.Right < help.Left && help.Right < lang.Left,
              help == null ? "none" : zoom.Bounds + " " + help.Bounds + " " + lang.Bounds);
        Check("  F1 opens it, said in its tooltip", Shortcuts.Of(help) == Keys.F1 && (UiStyle.TipOf(help) ?? "").StartsWith("F1\nHelp"), UiStyle.TipOf(help));
        var menu = form.BuildHelpMenu();
        var names = menu.Items.Cast<ToolStripItem>().Select(i => i.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();
        Check("the menu: About, the manual, then Create AI prompt, Ask AI for help, Ask another AI - and the chats' links for an Admin",
              string.Join(",", names) == "helpAbout,helpManual,aiPrompt,aiAsk,aiAskOther,helpAiChats", string.Join(",", names));
        Check("  their words", Item(menu, "helpAbout").Text.EndsWith("About Kaban HMI") && Item(menu, "helpManual").Text.EndsWith("Open the HMI manual") &&
              Item(menu, "aiPrompt").Text.EndsWith("Create AI prompt") && Item(menu, "aiAsk").Text.EndsWith("Ask AI for help (Claude)") &&
              Item(menu, "aiAskOther").Text == "Ask another AI" && Item(menu, "aiAskOther").DropDownItems.Count == AiChats.Defaults.Count - 1,
              string.Join(" | ", menu.Items.Cast<ToolStripItem>().Select(i => i.Text)));
        var hmi = (Kaban.UI.Settings.SettingsScreen)new HmiSettingsPage().GetControl();
        Check("HMI Settings has no About tab any more", !hmi.AllPages.Any(x => x.TitleKey == "Set_About"), string.Join(",", hmi.AllPages.Select(x => x.TitleFallback)));

        Console.WriteLine("--- About ---");
        var about = form.ShowAbout(false);
        Pump(300);
        var info = about == null ? null : All(about).OfType<Label>().FirstOrDefault(l => l.Text.StartsWith("Program"));
        Check("a window with the program, its version and its machines, and the manual's button", about != null && about.Visible && info != null &&
              info.Text.Contains("HMI library") && info.Text.Contains("FA2010") && All(about).OfType<Button>().Any(b => b.Text.Contains("Open the HMI manual")),
              info == null ? "no text" : info.Text);
        if (about != null) about.Close();
        Pump(100);

        Console.WriteLine("--- the manual ---");
        Item(menu, "helpManual").PerformClick();
        Pump(100);
        Check("opened in the browser: the manual with this panel's machines", openedFile != null && openedFile.EndsWith(".html") && File.Exists(openedFile) &&
              File.ReadAllText(openedFile).Contains("FA2010"), openedFile ?? "nothing opened");

        Console.WriteLine("--- Create AI prompt: the HMI and its manual ---");
        var nav = ((System.Collections.IDictionary)Get(form, "_menuButtons")).Keys.Cast<ButtonHMI>().First(b => (b.FallbackText ?? "").Contains("Manual"));
        nav.PerformClick();
        Pump(500);
        form.ToggleSidePage("IoMonitor");
        Pump(300);
        menu = form.BuildHelpMenu();
        Item(menu, "aiPrompt").PerformClick();
        Pump(100);
        string clip = null;
        try { clip = Clipboard.GetText(); } catch { }
        var p = clip ?? "";
        int ask = p.IndexOf("## What I ask you", StringComparison.Ordinal), html = p.IndexOf("```html", StringComparison.Ordinal);
        Check("on the clipboard, and said so", p.StartsWith("# Help me use an industrial machine's HMI (operator panel)") && noticed != null &&
              noticed.Contains("the HMI, its machines, where you are and its whole manual"), noticed ?? "no message");
        Check("  the program, its machines and their stations, its controllers", p.Contains("- Kaban HMI: ") && p.Contains("HMI library") &&
              p.Contains("FA2010") && p.Contains("the main machine, PLC station 1") && p.Contains("a sub-machine mounted with it") && p.Contains("- Station 1"),
              p.Length > 1500 ? p.Substring(0, 1500) : p);
        Check("  who is signed in, the page on screen and the side page beside it",
              p.Contains("Signed in: admin, level Admin") && p.Contains("The page on my screen: ") && p.Contains("Manual Movement") && p.Contains("Open beside it (the side panel): "));
        Check("  the alarms now", p.Contains("## Now") && (p.Contains("No alarm on the list.") || p.Contains("Alarms on the list")));
        string manual = html < 0 ? "" : p.Substring(html, Math.Max(0, ask - html));
        Check("  the manual whole (HTML), with this panel's machines, no local path in it",
              manual.Contains("<title>Kaban HMI - Operator and Engineer Manual</title>") && manual.Contains("</html>") && manual.Contains("FA2010") &&
              !manual.Contains("<base"), manual.Length + " characters");
        Check("  what is asked comes last: be the helper at my level, in my language; say what it can help with first",
              ask > html && html > 0 && p.Contains("Be my helper for THIS HMI, in English, at my level (Admin)") && p.Contains("say what you can help me with on this panel"));
        Check("  from the manual, step by step, the names as on the screen; safety; nothing invented; a helper page when asked; explain again",
              p.Contains("Answer from the manual above") && p.Contains("Step by step") && p.Contains("Safety first") && p.Contains("do not invent pages") &&
              p.Contains("write me a short helper page") && p.Contains("explain it again, more simply"));
        var saved = AlarmAiPrompt.LastSaved;
        Check("  saved as a file to attach", saved != null && Path.GetFileName(saved).StartsWith("AI prompt HMI ") && File.ReadAllText(saved) == p, saved ?? "none");
        Check("  not opened anywhere (only copied)", opened.Count == 0 && AlarmAiPrompt.LastOpened == null);

        Console.WriteLine("--- Ask AI for help: the user's AI opened ---");
        ViewPreferences.SetUser("admin", ViewPreferences.PreferredAi, "ChatGPT");
        menu = form.BuildHelpMenu();
        Check("his preferred AI in the entry", Item(menu, "aiAsk").Text.EndsWith("Ask AI for help (ChatGPT)") &&
              Item(menu, "aiAskOther").DropDownItems.Cast<ToolStripItem>().All(i => i.Text != "ChatGPT"), Item(menu, "aiAsk").Text);
        noticed = null;
        Item(menu, "aiAsk").PerformClick();
        Pump(100);
        Check("  copied, saved, and ChatGPT opened in the browser", opened.Count == 1 && opened[0] == "https://chatgpt.com/" &&
              AlarmAiPrompt.LastOpened != null && AlarmAiPrompt.LastOpened.Name == "ChatGPT" &&
              noticed != null && noticed.StartsWith("ChatGPT is opening in the browser. The prompt is on the clipboard: paste it") && noticed.Contains("attach this file"),
              string.Join(",", opened) + " / " + noticed);
        var gemini = Item(menu, "aiAskOther").DropDownItems.Cast<ToolStripItem>().First(i => i.Text == "Gemini");
        gemini.PerformClick();
        Pump(100);
        Check("  another AI from the list", opened.Count == 2 && opened[1] == "https://gemini.google.com/app", string.Join(",", opened));
        AiChats.OpenLink = link => false;
        Item(menu, "aiAsk").PerformClick();
        Pump(100);
        Check("  no browser, no internet: said so, the prompt still on the clipboard and in its file",
              noticed != null && noticed.StartsWith("ChatGPT could not be opened (https://chatgpt.com/)") && noticed.Contains("another computer or a phone") &&
              noticed.Contains(AlarmAiPrompt.LastSaved ?? "?"), noticed);
        AiChats.OpenLink = link => { opened.Add(link); return true; };

        Console.WriteLine("--- the AI chats: the panel's, an Admin's ---");
        Check("no file: the usual list, Claude first", AiChats.All.Select(c => c.Name).SequenceEqual(AiChats.Defaults.Select(c => c.Name)) && AiChats.All[0].Name == "Claude");
        using (var edit = new AiChatsForm())
        {
            edit.Show(); Pump(100);
            var g = (DataGridView)typeof(AiChatsForm).GetProperty("Grid", Any).GetValue(edit, null);
            Check("the editor: a name and a web address a row", g.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow) == AiChats.Defaults.Count &&
                  g.Columns[0].HeaderText == "Name" && g.Columns[1].HeaderText == "Web address");
            g.Rows.Add("Our AI", "file:///C:/Windows/notepad.exe");
            bool kept = (bool)typeof(AiChatsForm).GetMethod("Keep", Any).Invoke(edit, null);
            Check("  only web addresses: a file is refused", !kept && !File.Exists(AiChats.SettingsPath));
            g.Rows.Cast<DataGridViewRow>().First(r => Convert.ToString(r.Cells[0].Value) == "Our AI").Cells[1].Value = "https://ai.example.com/chat";
            g.Rows.RemoveAt(0);   // Claude out
            kept = (bool)typeof(AiChatsForm).GetMethod("Keep", Any).Invoke(edit, null);
            var why = ((Label)typeof(AiChatsForm).GetField("_status", Any).GetValue(edit)).Text;
            Check("  kept: the panel's list (AiChats.cfg)", kept && AiChats.All.Count == AiChats.Defaults.Count && AiChats.All[0].Name == "ChatGPT" &&
                  AiChats.All.Last().Name == "Our AI" && AiChats.Find("our ai").Link == "https://ai.example.com/chat" && AiChats.Find("Claude") == null,
                  why + " / " + string.Join(",", AiChats.All.Select(c => c.Name)));
            edit.Close();
        }
        ViewPreferences.SetUser("admin", ViewPreferences.PreferredAi, "Claude");
        Check("a preferred AI no longer in the list: the first of it", AiChats.Preferred.Name == "ChatGPT", AiChats.Preferred.Name);
        ViewPreferences.SetUser("admin", ViewPreferences.PreferredAi, null);

        Console.WriteLine("--- Preferred AI in Users and Preferences ---");
        var page = new UsersPreferencesControl { Dock = DockStyle.Fill };
        var host = new Form { ClientSize = new System.Drawing.Size(1200, 800) };
        host.Controls.Add(page);
        host.Show(); Pump(200);
        var combo = page.ComboOf(ViewPreferences.PreferredAi);
        var items = combo == null ? new List<string>() : combo.Items.Cast<object>().Select(o => o.ToString()).ToList();
        Check("its own row under 'Help and AI': as for everyone, then the panel's chats", combo != null && items.Count == AiChats.All.Count + 1 &&
              items[0] == "As for everyone  (" + AiChats.All[0].Name + ")" && items.Skip(1).SequenceEqual(AiChats.All.Select(c => c.Name)) &&
              page.PreferencesColumn.Controls.OfType<Label>().Any(l => l.Text == "Help and AI") &&
              page.PreferencesColumn.Controls.OfType<Label>().Any(l => l.Text.StartsWith("Preferred AI")),
              string.Join(" | ", items));
        combo.SelectedIndex = items.IndexOf("Our AI");
        Pump(50);
        bool savedPrefs = (bool)typeof(UsersPreferencesControl).GetMethod("Save", Any).Invoke(page, null);
        Check("  chosen and kept as his own: the menu asks Our AI", savedPrefs && ViewPreferences.UserValue("admin", ViewPreferences.PreferredAi) == "Our AI" &&
              Item(form.BuildHelpMenu(), "aiAsk").Text.EndsWith("Ask AI for help (Our AI)"), ViewPreferences.UserValue("admin", ViewPreferences.PreferredAi) ?? "none");
        host.Close();

        Console.WriteLine("--- an Operator ---");
        SecurityManager.Login("op", SecurityLevel.Operator);
        Pump(100);
        menu = form.BuildHelpMenu();
        Check("the same menu, without the chats' links", Item(menu, "helpAbout") != null && Item(menu, "aiAsk") != null && Item(menu, "helpAiChats") == null);
        Check("  his AI: everyone's (none chosen: the first of the list)", Item(menu, "aiAsk").Text.EndsWith("Ask AI for help (ChatGPT)"), Item(menu, "aiAsk").Text);
        Item(menu, "aiPrompt").PerformClick();
        Pump(100);
        try { clip = Clipboard.GetText(); } catch { }
        Check("  his prompt: his level, the helper at it", clip != null && clip.Contains("level Operator") && clip.Contains("at my level (Operator)"));

        Check("the texts in every language", new[] { "Ttl_Help", "Tip_Help", "Hlp_About", "Hlp_AiPromptItem", "Hlp_AiAskItem", "Hlp_AiAskOther", "Tip_AiAsk",
              "Tip_HmiPrompt", "Msg_HmiPromptCopied", "Msg_AiAsked", "Msg_AiAskFailed", "Hlp_AiChats", "Tip_AiChats", "AiC_Note", "AiC_Name", "AiC_Link",
              "AiC_Save", "AiC_Usual", "AiC_BadLink", "VP_SecHelp", "VP_PreferredAi", "PV_Ai" }
              .All(k => new[] { "EN", "TR", "RU", "DE", "IT", "ES" }.All(l => !string.IsNullOrEmpty(LanguageManager.GetStringIn(l, k)))));
        form.Close();
        Console.WriteLine(fail == 0 ? "\nHELP MENU ALL PASS" : "\nHELP MENU " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
