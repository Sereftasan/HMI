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
        Kaban.UI.Helpers.ColumnChooser.Folder = Path.Combine(dir, "Layouts");
        var opened = new List<string>();
        AiChats.OpenLink = link => { opened.Add(link); return true; };
        string openedFile = null;
        HmiManual.OpenFile = path => { openedFile = path; return true; };
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
        // 12 October: "list all the AIs as on the list directly (rather than with 'Ask another AI') ... in the order
        // set in HMI Settings / AI as 'Ask Claude', 'Ask ChatGPT', etc"
        Check("the menu: About, the manual, Create AI prompt, then Ask <AI> for every chat of the list, in its order - no Ask another AI",
              string.Join(",", names) == "helpAbout,helpManual,aiPrompt," + string.Join(",", AiChats.Defaults.Select(c => "aiAsk:" + c.Name)), string.Join(",", names));
        Check("  their words: Ask Claude, Ask ChatGPT ...", Item(menu, "helpAbout").Text == "About Kaban HMI" && Item(menu, "helpManual").Text == "Open the HMI manual" &&
              Item(menu, "aiPrompt").Text == "Create AI prompt" && Item(menu, "aiAsk:Claude").Text == "Ask Claude" && Item(menu, "aiAsk:ChatGPT").Text == "Ask ChatGPT" &&
              AiChats.Defaults.All(c => Item(menu, "aiAsk:" + c.Name).Text == "Ask " + c.Name),
              string.Join(" | ", menu.Items.Cast<ToolStripItem>().Select(i => i.Text)));
        // 12 October: "modify Help dropdown menu emojis so the menu gets to the same format as login dropdown"
        var entries = menu.Items.OfType<ToolStripMenuItem>().ToList();
        var logoutMenu = (ContextMenuStrip)typeof(MainForm).GetMethod("BuildLogoutMenu", Any).Invoke(form, null);
        var logoutEntries = logoutMenu.Items.OfType<ToolStripMenuItem>().ToList();
        Check("  as the user menu: a picture in the picture column, the words alone (no emoji in them)",
              entries.All(i => i.Image != null && i.Image.Width == MenuGlyphs.Size) && entries.All(i => !i.Text.Any(ch => char.IsSurrogate(ch) || ch >= 0x2100)) &&
              logoutEntries.All(i => i.Image != null) && menu.ShowImageMargin && menu.ImageScalingSize == logoutMenu.ImageScalingSize &&
              menu.Renderer.GetType() == logoutMenu.Renderer.GetType(),
              string.Join(" | ", entries.Select(i => i.Text + (i.Image == null ? " (no picture)" : ""))));
        Check("  each chat's tooltip names it", (Item(menu, "aiAsk:Gemini").ToolTipText ?? "").StartsWith("Opens Gemini in the browser and pastes the briefing into it"),
              Item(menu, "aiAsk:Gemini").ToolTipText);
        var hmi = (Kaban.UI.Settings.SettingsScreen)new HmiSettingsPage().GetControl();
        Check("HMI Settings has no About tab any more", !hmi.AllPages.Any(x => x.TitleKey == "Set_About"), string.Join(",", hmi.AllPages.Select(x => x.TitleFallback)));

        Console.WriteLine("--- About ---");
        // 9 October: "remove the title, 'Open the HMI manual' and 'Close' - just a clean message box is enough"
        string aboutText = null;
        MainForm.AboutNotice = t => aboutText = t;
        Item(menu, "helpAbout").PerformClick();
        Pump(100);
        MainForm.AboutNotice = null;
        Check("a plain message box: the program, its version, its machines - no title line, no buttons of its own",
              aboutText != null && aboutText.StartsWith("About Kaban HMI\nProgram: ") && aboutText.Contains("HMI library") && aboutText.Contains("FA2010") &&
              !aboutText.Contains("Kaban Machines HMI") && !aboutText.Contains("Open the HMI manual"), aboutText ?? "nothing");

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
        System.Collections.Specialized.StringCollection clipFiles = null;
        try { clipFiles = Clipboard.GetFileDropList(); } catch { }
        var p = AlarmAiPrompt.LastSaved != null && File.Exists(AlarmAiPrompt.LastSaved) ? File.ReadAllText(AlarmAiPrompt.LastSaved) : "";
        int ask = p.IndexOf("## What I ask you", StringComparison.Ordinal), html = p.IndexOf("## The HMI's manual (its text, whole", StringComparison.Ordinal);
        Check("on the clipboard as a file, said in one short line", p.StartsWith("# Help me use an industrial machine's HMI (operator panel)") &&
              clipFiles != null && clipFiles.Count == 1 && clipFiles[0] == AlarmAiPrompt.LastSaved &&
              noticed == "The AI prompt is on the clipboard as a file: paste it into an AI chat (Ctrl+V) and send it.", noticed ?? "no message");
        Check("  the program, its machines and their stations, its controllers", p.Contains("- Kaban HMI: ") && p.Contains("HMI library") &&
              p.Contains("FA2010") && p.Contains("the main machine, PLC station 1") && p.Contains("a sub-machine mounted with it") && p.Contains("- Station 1"),
              p.Length > 1500 ? p.Substring(0, 1500) : p);
        Check("  who is signed in, the page on screen and the side page beside it",
              p.Contains("Signed in: admin, level Admin") && p.Contains("The page on my screen: ") && p.Contains("Manual Movement") && p.Contains("Open beside it (the side panel): "));
        Check("  the alarms now", p.Contains("## Now") && (p.Contains("No alarm on the list.") || p.Contains("Alarms on the list")));
        Check("  how an alarm leaves the list: held until reset, not gone with its cause", p.Contains("- " + HmiAiPrompt.AlarmListRule));
        string manual = html < 0 ? "" : p.Substring(html, Math.Max(0, ask - html));
        // 14 October: a briefing an AI talks from - the manual's text, not its markup
        Check("  the manual whole as TEXT, with this panel's machines - no markup, script, or local path in it",
              manual.Contains("Contents") && manual.Contains("6. Alarms") && manual.Contains("13. This machine") && manual.Contains("FA2010") &&
              !manual.Contains("<base") && !manual.Contains("<div") && !manual.Contains("<script") && !manual.Contains("addEventListener") && !manual.Contains("file:///"),
              manual.Length + " characters");
        int shows = p.IndexOf("## What that page shows now", StringComparison.Ordinal);
        string onPage = shows < 0 ? "" : p.Substring(shows, Math.Max(0, p.IndexOf("\n## ", shows + 5, StringComparison.Ordinal) - shows));
        Check("  what the page on screen shows now: its units with their buttons and devices, top to bottom",
              onPage.Contains("- Unit \"") && onPage.Contains("button \"") && System.Text.RegularExpressions.Regex.IsMatch(onPage, @"\((M|Y)\d+\)"),
              onPage.Length > 400 ? onPage.Substring(0, 400) : onPage);
        // 26 October: the prompt brought up to the program as it is
        Check("  a button by its caption on the screen, never an empty one", !onPage.Contains("Button \"\"") &&
              System.Text.RegularExpressions.Regex.IsMatch(onPage, "- (Unit|Motor \\(inverter\\)) \"[^\"]+\""),
              onPage.Length > 400 ? onPage.Substring(0, 400) : onPage);
        Check("  the machine's bar above the tabs: the axes' speed slider, JOG and the steps, Automatic ON/OFF an HMI function",
              onPage.Contains("- The machine's bar above the page's tabs (the same over every tab):") && onPage.Contains("  - Speed slider \"") &&
              onPage.Contains("Button \"JOG\"") && onPage.Contains("Button \"10 mm\"") && onPage.Contains("Automatic ON/OFF\" (a function of the HMI, no PLC bit"),
              onPage.Length > 900 ? onPage.Substring(0, 900) : onPage);
        Check("  the levels as they are now (coupling, translations, backups; greyed with why)", p.Contains("- " + HmiAiPrompt.Levels));
        Check("  coupled units and Automatic ON/OFF told under Now only while there are", !p.Contains("Units coupled to move together now") && !p.Contains("Automatic ON/OFF is ON"));
        Check("  what is asked comes last: be the helper at my level, in my language, in a conversation that starts at once",
              ask > html && html > 0 && p.Contains("Be my helper for THIS HMI, in English, at my level (Admin), in a conversation") &&
              p.Contains("Start now, in a few short lines: what the page I am on is for") && p.Contains("Keep each answer short"));
        Check("  from the manual, step by step, the names as on the screen; safety; nothing invented; a helper page when asked; explain again",
              p.Contains("Answer from the manual above") && p.Contains("Step by step") && p.Contains("Safety first") && p.Contains("do not invent pages") &&
              p.Contains("write me a short helper page") && p.Contains("explain it again, more simply"));
        var saved = AlarmAiPrompt.LastSaved;
        Check("  the file: AI prompt HMI <time>.txt", saved != null && Path.GetFileName(saved).StartsWith("AI prompt HMI ") && saved.EndsWith(".txt"), saved ?? "none");
        Check("  not opened anywhere (only copied)", opened.Count == 0 && AlarmAiPrompt.LastOpened == null);
        UnitCycling.SetActive(true);
        Item(form.BuildHelpMenu(), "aiPrompt").PerformClick();
        Pump(100);
        var pc = AlarmAiPrompt.LastSaved != null && File.Exists(AlarmAiPrompt.LastSaved) ? File.ReadAllText(AlarmAiPrompt.LastSaved) : "";
        Check("  Automatic ON/OFF on: told under Now, its button ON in the bar", pc.Contains("- Automatic ON/OFF is ON: a press of a cylinder's button starts it moving back and forth by itself") &&
              pc.Contains("moving back and forth by itself - ON now)"));
        UnitCycling.SetActive(false);

        Console.WriteLine("--- Ask <AI>: that AI opened ---");
        menu = form.BuildHelpMenu();
        noticed = null;
        Item(menu, "aiAsk:ChatGPT").PerformClick();
        Pump(100);
        Check("  copied, saved, and ChatGPT opened in the browser", opened.Count == 1 && opened[0] == "https://chatgpt.com/" &&
              AlarmAiPrompt.LastOpened != null && AlarmAiPrompt.LastOpened.Name == "ChatGPT" &&
              noticed == "ChatGPT is opening: the prompt goes into the chat by itself in a few seconds - check it and send it (Enter). If it does not, paste it there (Ctrl+V).",
              string.Join(",", opened) + " / " + noticed);
        // 14 October: "paste it to the AI page directly with the ask AI button (all AI pages support Ctrl+V)"
        var pressed = new List<string>(); var put = new List<string>(); bool done = false;
        AiPaste.OtherWindowInFront = () => true;
        AiPaste.Press = k => pressed.Add(k);
        AiPaste.PutDocument = (f, t) => put.Add(f != null ? "file:" + Path.GetFileName(f) : "text:" + (t ?? "").Length);
        AiPaste.PutText = t => put.Add("message:" + t);
        AiPaste.Delay = TimeSpan.FromMilliseconds(50); AiPaste.Between = TimeSpan.FromMilliseconds(50);
        EventHandler onDone = (s2, e2) => done = true;
        AiPaste.Done += onDone;
        Item(menu, "aiAsk:Claude").PerformClick();
        for (int i = 0; i < 100 && !done; i++) Pump(20);
        Check("  a moment later Ctrl+V in the chat: the briefing file, then the message to read it and start", done && pressed.SequenceEqual(new[] { "^v", "^v" }) &&
              put.Count == 3 && put[0] == "file:" + Path.GetFileName(AlarmAiPrompt.LastSaved) && put[1].StartsWith("message:The attached file \"" + Path.GetFileName(AlarmAiPrompt.LastSaved) + "\" is a briefing from Kaban HMI") &&
              put[1].Contains("start as it asks at its end") && put[1].Contains("Answer in English.") && put[2] == put[0] && AiPaste.LastResult == "pasted+message",
              string.Join(" | ", put) + " / " + string.Join(",", pressed) + " / " + AiPaste.LastResult);
        Check("  sending left to the user: no Enter pressed", !pressed.Any(k => k.Contains("{ENTER}") || k.Contains("~")));
        done = false; pressed.Clear(); put.Clear();
        AiPaste.OtherWindowInFront = () => false;
        Item(menu, "aiAsk:Claude").PerformClick();
        for (int i = 0; i < 100 && !done; i++) Pump(20);
        Check("  the HMI still in front (no browser came up): nothing pressed - the prompt stays on the clipboard", done && pressed.Count == 0 &&
              AiPaste.LastResult == "skipped: the HMI in front", AiPaste.LastResult);
        done = false; pressed.Clear(); put.Clear();
        AiPaste.OtherWindowInFront = () => true;
        ViewPreferences.SetPanel(ViewPreferences.AiPromptAs, "Text");
        Item(menu, "aiAsk:Claude").PerformClick();
        for (int i = 0; i < 100 && !done; i++) Pump(20);
        Check("  as text (the user's choice): the briefing's text pasted once - it ends with what is asked", done && pressed.Count == 1 && put.Count == 1 &&
              put[0].StartsWith("text:") && AiPaste.LastResult == "pasted", string.Join(" | ", put));
        ViewPreferences.SetPanel(ViewPreferences.AiPromptAs, null);
        done = false; pressed.Clear(); put.Clear();
        ViewPreferences.SetPanel(ViewPreferences.AiPaste, "Off");
        Item(menu, "aiAsk:Claude").PerformClick();
        Pump(200);
        Check("  switched off in Preferences: nothing pasted, the user told to paste it", pressed.Count == 0 && AiPaste.LastResult == "off" &&
              noticed == "Claude is opening: paste the prompt file into the chat (Ctrl+V) and send it.", noticed);
        ViewPreferences.SetPanel(ViewPreferences.AiPaste, null);
        AiPaste.Done -= onDone;
        Item(menu, "aiAsk:Gemini").PerformClick();
        Pump(100);
        Check("  another AI of the list, straight from the menu", opened.Count >= 2 && opened.Last() == "https://gemini.google.com/app", string.Join(",", opened));
        AiChats.OpenLink = link => false;
        Item(menu, "aiAsk:ChatGPT").PerformClick();
        Pump(100);
        Check("  no browser, no internet: said so, the prompt still on the clipboard and in its file",
              noticed != null && noticed.StartsWith("ChatGPT could not be opened (no browser or no internet?)") && noticed.Contains("Documents\\Kaban HMI\\AI prompts"), noticed);
        AiChats.OpenLink = link => { opened.Add(link); return true; };

        Console.WriteLine("--- the AI chats: the panel's, an Admin's ---");
        Check("no file: the usual list, Claude first", AiChats.All.Select(c => c.Name).SequenceEqual(AiChats.Defaults.Select(c => c.Name)) && AiChats.All[0].Name == "Claude");
        // 9 October: "move this part on top of the HMI Settings / AI Prompt page as a table, formatted the same as
        // the other tables (the buttons' style as well), and add 'Add' and 'Remove' buttons"
        var hmiTabs = (Kaban.UI.Settings.SettingsScreen)new HmiSettingsPage().GetControl();
        var aiTab = hmiTabs.AllPages.First(x => x.TitleKey == "Set_AiPrompt");
        Check("the chats are on HMI Settings > AI Prompt (an Admin's)", aiTab.RequiredLevel == SecurityLevel.Admin);
        var aiPage = (AiPromptControl)aiTab.GetControl();
        var holder = new Form { ClientSize = new System.Drawing.Size(1100, 800) };
        holder.Controls.Add(aiPage);
        holder.Show(); Pump(200);
        var ed = aiPage.Chats;
        var g = ed.Grid;
        Check("  the page is the chats alone (the page-designer prompt is gone)", ed.Parent == aiPage && ed.Top == 0 &&
              aiPage.Controls.Count == 1 && aiTab.TitleFallback == "AI", ed.Bounds.ToString());
        var aiNote = ed.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "aiChatsNote");
        Check("  no 'AI chats' title; its note: the Help menu and the alarms' help lists offer each, as 'Ask <name>', in this order",
              !ed.Controls.OfType<Label>().Any(l => l.Text == "AI chats") && aiNote != null &&
              aiNote.Text.StartsWith("The AI chats the Help menu (?) and every alarm's help list offer, each as 'Ask <name>', in this order") &&
              !aiNote.Text.Contains("Users and Preferences"), aiNote == null ? "no note" : aiNote.Text);
        Check("  a table as the others: a name and a web address a row, the usual list", g.Rows.Count == AiChats.Defaults.Count &&
              g.Columns[0].HeaderText == "Name" && g.Columns[1].HeaderText == "Web address" && !g.RowHeadersVisible && !g.AllowUserToAddRows &&
              Convert.ToString(g.Rows[0].Cells[0].Value) == "Claude");
        var barButtons = ed.Controls.OfType<FlowLayoutPanel>().First().Controls.OfType<Button>().ToList();
        Check("  its bar as every settings page's: keep, throw away, add, remove, up, down, the usual list",
              barButtons.Count == 7 && barButtons[0] == ed.SaveButton && barButtons[2] == ed.AddButton && barButtons[3] == ed.RemoveButton &&
              barButtons.All(b => b.Width == barButtons[0].Width) && !ed.SaveButton.Enabled && ed.AddButton.Enabled &&
              (UiStyle.TipOf(ed.AddButton) ?? "").Contains("Add an AI chat") && (UiStyle.TipOf(ed.RemoveButton) ?? "").Contains("Remove the selected"),
              string.Join(",", barButtons.Select(b => b.Text + "/" + b.Width)));
        ed.AddButton.PerformClick();
        Pump(50);
        g.EndEdit();
        int added = g.Rows.Count - 1;
        Check("Add: a new row at the end, 💾 ready", g.Rows.Count == AiChats.Defaults.Count + 1 && ed.SaveButton.Enabled &&
              Convert.ToString(g.Rows[added].Cells[1].Value) == "https://");
        g.Rows[added].Cells[0].Value = "Our AI";
        g.Rows[added].Cells[1].Value = "file:///C:/Windows/notepad.exe";
        ed.SaveButton.PerformClick();
        Pump(50);
        Check("  only web addresses: a file is refused, said why", !File.Exists(AiChats.SettingsPath) && ed.Status.Contains("Our AI: its web address must start with https://"), ed.Status);
        g.Rows[added].Cells[1].Value = "https://ai.example.com/chat";
        g.CurrentCell = g.Rows[0].Cells[0];
        ed.RemoveButton.PerformClick();      // Claude out
        Pump(50);
        Check("Remove: the selected row out", g.Rows.Count == AiChats.Defaults.Count && Convert.ToString(g.Rows[0].Cells[0].Value) == "ChatGPT");
        ed.SaveButton.PerformClick();
        Pump(50);
        Check("  kept: the panel's list (AiChats.cfg)", File.Exists(AiChats.SettingsPath) && AiChats.All.Count == AiChats.Defaults.Count && AiChats.All[0].Name == "ChatGPT" &&
              AiChats.All.Last().Name == "Our AI" && AiChats.Find("our ai").Link == "https://ai.example.com/chat" && AiChats.Find("Claude") == null &&
              !ed.SaveButton.Enabled && ed.Status == "The list is kept.",
              ed.Status + " / " + string.Join(",", AiChats.All.Select(c => c.Name)));
        holder.Close();
        menu = form.BuildHelpMenu();
        Check("the menu follows the list: Ask ChatGPT first, Ask Our AI last, no Ask Claude",
              string.Join(",", menu.Items.Cast<ToolStripItem>().Select(i => i.Name).Where(n => n.StartsWith("aiAsk:"))) ==
              string.Join(",", AiChats.All.Select(c => "aiAsk:" + c.Name)) && Item(menu, "aiAsk:ChatGPT") != null && Item(menu, "aiAsk:Our AI") != null &&
              Item(menu, "aiAsk:Claude") == null, string.Join(",", menu.Items.Cast<ToolStripItem>().Select(i => i.Name)));

        // 16 October: "add an Activate column to the HMI Settings / AI table so the user could remove them from the AI
        // helper lists (to make the combos not look too dirty) without deleting their web addresses from this table"
        Console.WriteLine("--- Active: out of the lists, kept in the table ---");
        var ed2 = new Kaban.UI.Settings.AiChatsEditor();
        var holder2 = new Form { ClientSize = new System.Drawing.Size(1100, 600) };
        holder2.Controls.Add(ed2); holder2.Show(); Pump(150);
        var g2 = ed2.Grid;
        Check("an 'Active' column of ticks, every chat ticked", g2.Columns.Count == 3 && g2.Columns[2].HeaderText == "Active" &&
              g2.Columns[2] is DataGridViewCheckBoxColumn && g2.Rows.Cast<DataGridViewRow>().All(r => (bool)r.Cells[2].Value),
              string.Join(",", g2.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText)));
        int gpt = g2.Rows.Cast<DataGridViewRow>().First(r => Convert.ToString(r.Cells[0].Value) == "ChatGPT").Index;
        g2.CurrentCell = g2.Rows[gpt].Cells[2];
        g2.Rows[gpt].Cells[2].Value = false;
        Check("  unticked: 💾 ready", ed2.SaveButton.Enabled);
        ed2.SaveButton.PerformClick(); Pump(50);
        var kept = File.ReadAllLines(AiChats.SettingsPath).FirstOrDefault(l => l.StartsWith("ChatGPT;"));
        Check("  kept with its web address, marked not active (ChatGPT;https://chatgpt.com/;off)", kept == "ChatGPT;https://chatgpt.com/;off" &&
              AiChats.Find("ChatGPT") != null && !AiChats.Find("ChatGPT").Active && AiChats.Find("ChatGPT").Link == "https://chatgpt.com/", kept);
        menu = form.BuildHelpMenu();
        Check("  the Help menu: no Ask ChatGPT, the others as before", Item(menu, "aiAsk:ChatGPT") == null && Item(menu, "aiAsk:Our AI") != null &&
              AiChats.Offered.Count == AiChats.All.Count - 1, string.Join(",", menu.Items.Cast<ToolStripItem>().Select(i => i.Name).Where(n => n.StartsWith("aiAsk:"))));
        var alarmItems = AiMenu.Items(() => "x", AlarmAiPrompt.Subject.Alarm, form);
        Check("  an alarm's help list too", !alarmItems.Any(i => i.Name == "aiAsk:ChatGPT") && alarmItems.Any(i => i.Name == "aiAsk:Our AI"));
        Check("  still in the table, unticked, after it was read again", new Kaban.UI.Settings.AiChatsEditor().Grid.Rows.Cast<DataGridViewRow>()
              .Any(r => Convert.ToString(r.Cells[0].Value) == "ChatGPT" && Convert.ToString(r.Cells[1].Value) == "https://chatgpt.com/" && !(bool)r.Cells[2].Value));
        g2.Rows[gpt].Cells[2].Value = true;
        ed2.SaveButton.PerformClick(); Pump(50);
        Check("  ticked again: offered again", AiChats.Find("ChatGPT").Active && Item(form.BuildHelpMenu(), "aiAsk:ChatGPT") != null &&
              File.ReadAllLines(AiChats.SettingsPath).Contains("ChatGPT;https://chatgpt.com/"));
        holder2.Close();

        // 3 November: "there is no horizontal scroll to see the last column of the table ('Active' column)" - widths kept from
        // a wider window turned the table's filling off; Active was out of sight
        Console.WriteLine("--- the AI table: its columns fill it, whatever widths were kept ---");
        Directory.CreateDirectory(Kaban.UI.Helpers.ColumnChooser.Folder);
        File.WriteAllLines(Path.Combine(Kaban.UI.Helpers.ColumnChooser.Folder, "AiChats.cols"),
                           new[] { "col\tname\t532\t0\t1", "col\tlink\t1300\t1\t1", "col\tactive\t120\t2\t1" });
        var ed3 = new Kaban.UI.Settings.AiChatsEditor();
        var holder3 = new Form { ClientSize = new System.Drawing.Size(1100, 600) };
        holder3.Controls.Add(ed3); holder3.Show(); Pump(200);
        var g3 = ed3.Grid;
        var activeAt = g3.GetColumnDisplayRectangle(2, false);
        Check("kept widths wider than the window: the table still fills its width, Active in sight at its right end",
              g3.AutoSizeColumnsMode == DataGridViewAutoSizeColumnsMode.Fill && activeAt.Width > 20 && activeAt.Right <= g3.ClientSize.Width + 2 &&
              g3.Columns.Cast<DataGridViewColumn>().Sum(c => c.Width) <= g3.ClientSize.Width + 2,
              g3.AutoSizeColumnsMode + " " + activeAt + " in " + g3.ClientSize.Width);
        Check("  the kept widths its columns' shares (the web address the widest), a scroll bar along the bottom when they cannot fit",
              g3.Columns[1].Width > g3.Columns[0].Width && g3.Columns[0].Width > g3.Columns[2].Width && g3.ScrollBars == ScrollBars.Both,
              string.Join(" ", g3.Columns.Cast<DataGridViewColumn>().Select(c => c.Width)));
        holder3.Close();

        Console.WriteLine("--- Users and Preferences: no Preferred AI any more ---");
        var page = new UsersPreferencesControl { Dock = DockStyle.Fill };
        var host = new Form { ClientSize = new System.Drawing.Size(1200, 800) };
        host.Controls.Add(page);
        host.Show(); Pump(200);
        Check("no Preferred AI row (every AI is in the menu); 'Help and AI' keeps how the prompt is copied",
              page.ComboOf("PreferredAi") == null && !page.PreferencesColumn.Controls.OfType<Label>().Any(l => l.Text.StartsWith("Preferred AI")) &&
              page.PreferencesColumn.Controls.OfType<Label>().Any(l => l.Text == "Help and AI") && page.ComboOf(ViewPreferences.AiPromptAs) != null);
        host.Close();

        Console.WriteLine("--- an Operator ---");
        SecurityManager.Login("op", SecurityLevel.Operator);
        Pump(100);
        menu = form.BuildHelpMenu();
        Check("the same menu, every AI of the list", Item(menu, "helpAbout") != null && AiChats.All.All(c => Item(menu, "aiAsk:" + c.Name) != null));
        Item(menu, "aiPrompt").PerformClick();
        Pump(100);
        var clip = File.ReadAllText(AlarmAiPrompt.LastSaved);
        Check("  his prompt: his level, the helper at it", clip.Contains("level Operator") && clip.Contains("at my level (Operator)"));

        // the Help and AI section: Preferred AI, then how the prompt is copied
        var page2 = new UsersPreferencesControl { Dock = DockStyle.Fill };
        var host2 = new Form { ClientSize = new System.Drawing.Size(1200, 800) };
        host2.Controls.Add(page2);
        host2.Show(); Pump(150);
        var asCombo = page2.ComboOf(ViewPreferences.AiPromptAs);
        var asItems = asCombo == null ? new List<string>() : asCombo.Items.Cast<object>().Select(o => o.ToString()).ToList();
        Check("'AI prompt copied as': a file (Ctrl+V attaches it) - the first - or its text", asCombo != null &&
              asItems.Skip(Math.Max(0, asItems.Count - 2)).SequenceEqual(new[] { "A file (Ctrl+V attaches it)", "Its text" }) &&
              (asItems.Count == 2 || asItems[0] == "As for everyone  (A file (Ctrl+V attaches it))"), string.Join(" | ", asItems));
        host2.Close();
        Check("the texts in every language", new[] { "Ttl_Help", "Tip_Help", "Hlp_About", "Hlp_AiPromptItem", "Hlp_AiAskItem", "Tip_AiAsk",
              "Tip_HmiPrompt", "Msg_AiPromptFile", "Msg_AiAsked", "Msg_AiAskedFile", "Msg_AiAskFailed", "AiC_Note", "AiC_Note2", "AiC_Name", "AiC_Link",
              "AiC_Save", "AiC_Revert", "AiC_Add", "AiC_Remove", "AiC_Kept", "AiC_Usual", "AiC_BadLink", "VP_SecHelp" }
              .All(k => new[] { "EN", "TR", "RU", "DE", "IT", "ES" }.All(l => !string.IsNullOrEmpty(LanguageManager.GetStringIn(l, k)))));
        Check("  the gone ones gone (Ask another AI, AI chats, Preferred AI)", new[] { "Hlp_AiAskOther", "AiC_Head", "VP_PreferredAi", "PV_Ai" }
              .All(k => string.IsNullOrEmpty(LanguageManager.GetStringIn("EN", k))));
        form.Close();
        Console.WriteLine(fail == 0 ? "\nHELP MENU ALL PASS" : "\nHELP MENU " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
