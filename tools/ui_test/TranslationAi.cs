using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 22 October: "a tool to use AI to translate the missing cells and to check the other translations - a button to
// ask the AI; an AI prompt including the translation files with a description for each key so the AI can translate
// them best (not just the words: the best translation for each key's use in each language) and return the files;
// and an import file button to update the current translation files with them".
class TA {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { d = d ?? ""; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    class Mod : IMachineModule {
        public string Id;
        public string ModuleId { get { return Id; } }
        public string DisplayNameKey { get { return Id; } }
        public void RegisterPlc(IMachineMountContext ctx) { ctx.Station(1); }
        public void RegisterConditions(IMachineMountContext ctx) { }
        public void RegisterData(IMachineMountContext ctx) { }
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx) { yield break; }
    }
    static object Get(object o, string n) { var t = o.GetType(); var pr = t.GetProperty(n, Any); return pr != null ? pr.GetValue(o, null) : t.GetField(n, Any).GetValue(o); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length).Invoke(o, a); }
    static IList<TranslationEdits.Row> Shown(object c) { return (IList<TranslationEdits.Row>)Get(c, "Shown"); }
    static KeyValuePair<string, string> Src(string name, string text) { return new KeyValuePair<string, string>(name, text); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "trai_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var common = Path.Combine(dir, "CommonTranslations.csv");
        var main = Path.Combine(dir, "Translations.csv");
        var alarms = Path.Combine(dir, "Alarms.json");
        File.WriteAllLines(common, new[] { "Key;EN;TR;RU", "Btn_Ok;OK;Tamam;OK", "Btn_Stop;Stop;Dur;Stop", "Lbl_Speed;Speed;;Skorost", "Msg_Saved;{0} saved.;{0} kaydedildi.;" });
        File.WriteAllLines(main, new[] { "Key;EN;TR;RU", "Unit_Clamp;Clamp;Kiskac;Zazhim", "Btn_Stop;Stop the unit;Uniteyi durdur;Ostanovit", "Ref_Home;Reference;;" });
        File.WriteAllText(alarms, "[\r\n  {\r\n    \"PlcAddress\": \"M801\",\r\n    \"TagCode\": \"Door\",\r\n    \"Translations\": {\r\n      \"EN\": \"Door open\",\r\n      \"TR\": \"Kapi acik\"\r\n    }\r\n  }\r\n]\r\n");
        TranslationEdits.Folder = Path.Combine(dir, "data");
        AlarmAiPrompt.SaveFolder = Path.Combine(dir, "AI prompts");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        AiChats.SettingsPath = Path.Combine(dir, "AiChats.cfg");
        AiChats.OpenLink = link => true;
        AlarmAiPrompt.CopiedNotice = w => { };
        TranslationEdits.ProjectFileOf = x => null;
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod { Id = "MAIN" }, null, dir);
        LanguageManager.LoadCommonTranslations(common);
        LanguageManager.LoadMachineTranslations(main);
        AlarmDefinitionLoader.Load(alarms, true, "MAIN");
        TranslationEdits.Apply();
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        string told = null;
        Kaban.UI.Profiles.Ask.Hook = (title, prompt, initial) => { told = prompt; return "yes"; };
        // the program's lines: the list shipped beside it
        var index = Path.Combine(dir, "TranslationUses.csv");
        File.WriteAllLines(index, new[] { "Key;File;Line;Kind;Text", "Msg_Saved;KabanHMI\\Settings\\ParamsPage.cs;80;Status;SetStatus(string.Format(T(\"Msg_Saved\", \"{0} saved.\"), n));",
                                          "Btn_Stop;KabanHMI\\Screens\\Manual.cs;12;Text;_stop.Text = T(\"Btn_Stop\", \"Stop\");" });
        TextUses.SourceRoot = () => null;
        TextUses.IndexFile = () => index;

        var form = new Form { Width = 1300, Height = 700 };
        var c = new TranslationsControl();
        form.Controls.Add(c);
        form.Show(); Application.DoEvents();
        var grid = (DataGridView)Get(c, "Grid");
        var bar = grid.Parent.Controls.OfType<FlowLayoutPanel>().First();
        var tips = bar.Controls.OfType<Button>().Select(b => b.Text).ToList();
        Check("the toolbar: 🤖 (ask an AI) and 📥 (import its answer)", tips.Contains(char.ConvertFromUtf32(0x1F916)) && tips.Contains(char.ConvertFromUtf32(0x1F4E5)), string.Join(" ", tips));

        Console.WriteLine("--- the rows asked about: the rows shown ---");
        var missing = (List<TranslationEdits.Row>)Call(c, "AiRows", TranslationAiPrompt.Mode.Missing);
        Check("the missing texts: the rows shown with an empty cell", string.Join(",", missing.Select(x => x.Key).OrderBy(k => k)) == "Alarm_MAIN.Door,Lbl_Speed,Msg_Saved,Ref_Home",
              string.Join(",", missing.Select(x => x.Key)));
        Check("  checking: every row shown", ((List<TranslationEdits.Row>)Call(c, "AiRows", TranslationAiPrompt.Mode.Check)).Count == Shown(c).Count && Shown(c).Count == 8);

        Console.WriteLine("--- the prompt ---");
        var p = ((string)Call(c, "AiPrompt", TranslationAiPrompt.Mode.Missing)).Replace("\r\n", "\n");
        Check("the program and who reads its texts, the languages, the rules - what I ask at the end", p.StartsWith("# Translate the missing texts") &&
              p.Contains("Kaban HMI") && p.Contains("Turkish (TR)") && p.Contains("Russian (RU)") && p.Contains("{0} {1}") && p.Contains("Never a ';'") &&
              p.IndexOf("## What I ask you") > p.IndexOf("## The files") && p.Contains("Fill EVERY EMPTY CELL"));
        Check("  each owner's file by name: CommonTranslations.csv, MAIN_Translations.csv - its rows as Key;Use;EN;TR;RU",
              p.Contains("### CommonTranslations.csv") && p.Contains("### MAIN_Translations.csv") && p.Contains("```csv\nKey;Use;EN;TR;RU\n") &&
              p.IndexOf("### CommonTranslations.csv") < p.IndexOf("### MAIN_Translations.csv"));
        var lines = p.Split('\n');
        Func<string, string> line = k => lines.FirstOrDefault(l => l.StartsWith(k + ";"));
        Check("  where each is shown: a program line's kind and its file", line("Msg_Saved") == "Msg_Saved;status line in ParamsPage;{0} saved.;{0} kaydedildi.;", line("Msg_Saved"));
        Check("  else what its key says it is (Lbl_: a label), an alarm's: its message", line("Lbl_Speed") == "Lbl_Speed;a label;Speed;;Skorost" &&
              line("Alarm_MAIN.Door").StartsWith("Alarm_MAIN.Door;an alarm's message (the alarm list, the alarm bar): MAIN.Door  (M801);Door open"), line("Lbl_Speed") + " / " + line("Alarm_MAIN.Door"));
        Check("  only the rows asked about (the missing ones): not Btn_Ok", line("Btn_Ok") == null);
        Check("  given back: the same files, Key;EN;TR;RU;Note, only the cells changed, a note", p.Contains("`Key;EN;TR;RU;Note`") && p.Contains("ONLY THE ROWS YOU CHANGED OR FILLED"));
        var pc = (string)Call(c, "AiPrompt", TranslationAiPrompt.Mode.Check);
        Check("checking: every text for its use, the English only where wrong", pc.StartsWith("# Check and improve") && pc.Contains("Check EVERY TEXT") &&
              pc.Contains("The English only where it is wrong") && pc.Contains("Btn_Ok;a button;OK;Tamam;OK"));
        Check("the panel's own names for its files read back", TranslationAiPrompt.OwnerOfFile("CommonTranslations (2).csv") == TranslationEdits.General &&
              TranslationAiPrompt.OwnerOfFile(@"C:\Downloads\FA2010_Translations-1.csv") == "FA2010" && TranslationAiPrompt.OwnerOfFile("notes.csv") == null);

        Console.WriteLine("--- asked: the briefing as a file, the chat ---");
        var menu = (ContextMenuStrip)Call(c, "AiMenu");
        var modes = menu.Items.OfType<ToolStripMenuItem>().Where(i => i.DropDownItems.Count > 0).ToList();
        Check("the 🤖 menu: the rows shown counted; translate the missing / check every one - each: Create AI prompt, Ask <each chat>",
              menu.Items[0].Text.Contains("8 rows shown") && menu.Items[0].Text.Contains("4 with a text missing") && modes.Count == 2 &&
              modes.All(m => m.DropDownItems[0].Name == "aiPrompt" && m.DropDownItems.Count == 1 + AiChats.Offered.Count), menu.Items[0].Text);
        ((ToolStripMenuItem)modes[0].DropDownItems[0]).PerformClick();
        Check("  Create AI prompt: saved as a file (AI prompt translations ...), the prompt of the missing texts",
              AlarmAiPrompt.LastSaved != null && Path.GetFileName(AlarmAiPrompt.LastSaved).StartsWith("AI prompt translations") &&
              File.ReadAllText(AlarmAiPrompt.LastSaved).StartsWith("# Translate the missing texts"), AlarmAiPrompt.LastSaved);
        Check("  the chat's first message: what the file is", AlarmAiPrompt.KickOff(AlarmAiPrompt.Subject.Translation, "x.txt").Contains("translation files"));

        Console.WriteLine("--- the AI's answer imported: the empty cells at once, the changes confirmed ---");
        // 22 October: "apply the AI translations to the file with a confirmation system for the changes of any cell -
        // only the empty cells should be copied to the translation file directly"
        var original = new[] { File.ReadAllText(common), File.ReadAllText(main), File.ReadAllText(alarms) };
        TranslationReviewForm shownForm = null;
        Func<TranslationReviewForm, bool> answerWith = null;
        TranslationReviewForm.Hook = f => { shownForm = f; return answerWith(f); };
        var answer = string.Join("\r\n", new[] {
            "Here are the files.", "", "### CommonTranslations.csv", "```csv", "Key;EN;TR;RU;Note",
            "Lbl_Speed;;Hız;;the label's usual word", "Msg_Saved;;Kaydedildi.;;placeholder forgotten", "Btn_Ok;;Tamam;;", "```", "",
            "**MAIN_Translations.csv**", "```csv", "Key;EN;TR;RU;FR;Note",
            "Unit_Clamp;;Kıskaç;\"Zazhim; tiski\";Pince;the clamp of a profile", "Nope_Key;;x;;;", "```",
            "Btn_Stop could mean both files - here it is without a name:", "```", "Key;TR", "Btn_Stop;Durdur", "```", "Tell me if you want more." });
        string okText = null, before = null;
        List<TranslationReviewForm.Item> items = null;
        answerWith = f =>
        {
            items = ((List<TranslationReviewForm.Item>)Get(f, "Items")).ToList();
            okText = (string)Get(f, "OkText");
            before = string.Join(" | ", items.Select(x => x.Row.Key + " " + x.Lang + " " + x.Now + ">" + x.New + " " + x.Take + " " + x.Why));
            f.Show(); Application.DoEvents();
            Call(f, "Tick", 1, false);                                              // RU: not taken
            ((DataGridView)Get(f, "Grid")).Rows[0].Cells["New"].Value = "Kıskaç (profil)";   // TR: corrected, taken
            return true;
        };
        var r = Call(c, "ImportAnswer", new List<KeyValuePair<string, string>> { Src(null, answer) });
        var notFound = (List<string>)Get(r, "NotFound");
        var unknown = (List<string>)Get(r, "Unknown");
        Check("the answer copied: its three blocks - 1 empty cell, 2 changes, 1 the same, 1 refused (its {0} lost)",
              (int)Get(r, "Blocks") == 3 && (int)Get(r, "Filled") == 1 && (int)Get(r, "Changed") == 2 && (int)Get(r, "Same") == 1 && (int)Get(r, "Refused") == 1,
              Get(r, "Blocks") + " " + Get(r, "Filled") + " " + Get(r, "Changed") + " " + Get(r, "Same") + " " + Get(r, "Refused"));
        Check("  the changes to confirm: each with now, new and the AI's note - ticked; the button counts what is written",
              before == "Unit_Clamp TR Kiskac>Kıskaç True AI: the clamp of a profile | Unit_Clamp RU Zazhim>Zazhim, tiski True AI: the clamp of a profile" &&
              okText == "Write: 1 empty cell(s) + 2 change(s) ticked", okText + " / " + before);
        Check("  one unticked, one corrected in its cell: the button says 1 + 1", (string)Get(shownForm, "OkText") == "Write: 1 empty cell(s) + 1 change(s) ticked",
              (string)Get(shownForm, "OkText"));
        Check("  a key the table has not, a key of two files without the file's name: left out", notFound.Contains("Nope_Key") && notFound.Any(n => n.StartsWith("Btn_Stop")),
              string.Join(", ", notFound));
        Check("  a language the table has not: said", unknown.SequenceEqual(new[] { "FR" }), string.Join(",", unknown));
        var hist = TranslationEdits.History();
        Check("written: the empty cell and the change taken (corrected) - not the one unticked; the history says the AI",
              File.ReadAllLines(common).Contains("Lbl_Speed;Speed;Hız;Skorost") && File.ReadAllLines(main).Contains("Unit_Clamp;Clamp;Kıskaç (profil);Zazhim") &&
              LanguageManager.GetStringIn("TR", "Lbl_Speed") == "Hız" && hist.Count == 2 && hist.All(h => h.User == "maint (AI)") && (int)Get(c, "PendingCount") == 0,
              string.Join("|", File.ReadAllLines(main)) + " / " + string.Join(", ", hist.Select(h => h.User + " " + h.Key)));
        var report = (string)typeof(TranslationsControl).GetMethod("Report", Any).Invoke(null, new[] { r, false });
        Check("  the report: written, the same, refused (which), left out, the language to add", report.Contains("1 empty cell(s) filled, 1 of 2 change(s) taken") &&
              report.Contains("Msg_Saved TR: {0}") && report.Contains("Nope_Key") && report.Contains("FR"), report);

        shownForm = null;
        answerWith = f => false;
        r = Call(c, "ImportAnswer", new List<KeyValuePair<string, string>> { Src("MAIN_Translations.csv", "Key;TR;RU\r\nRef_Home;Referans;\r\nBtn_Stop;Durdur;\r\n") });
        Check("the review cancelled: nothing written - not the empty cells either", (bool)Get(r, "Cancelled") && File.ReadAllLines(main).Contains("Ref_Home;Reference;;") &&
              ((string)typeof(TranslationsControl).GetMethod("Report", Any).Invoke(null, new[] { r, false })).StartsWith("Cancelled"));
        shownForm = null;
        r = Call(c, "ImportAnswer", new List<KeyValuePair<string, string>> { Src("MAIN_Translations.csv", "Key;RU\r\nRef_Home;Referens\r\n") });
        Check("only empty cells: written at once, nothing to confirm", shownForm == null && (int)Get(r, "Filled") == 1 && File.ReadAllLines(main).Contains("Ref_Home;Reference;;Referens"),
              string.Join("|", File.ReadAllLines(main)));
        r = Call(c, "ImportAnswer", new List<KeyValuePair<string, string>> { Src(null, "Sorry, I cannot do that.") });
        Check("nothing like a translation file: said so", (int)Get(r, "Blocks") == 0 &&
              ((string)typeof(TranslationsControl).GetMethod("Report", Any).Invoke(null, new[] { r, false })).StartsWith("Nothing to import"));
        var importMenu = (ContextMenuStrip)Call(c, "ImportMenu");
        Check("📥: an older installation, translation files, the AI's answer (files, copied) - the 🤖 menu imports nothing",
              importMenu.Items.OfType<ToolStripMenuItem>().Count() == 4 && !menu.Items.OfType<ToolStripMenuItem>().Any(i => i.Text.Contains("Import")),
              string.Join(" / ", importMenu.Items.OfType<ToolStripMenuItem>().Select(i => i.Text)));

        Console.WriteLine("--- an older installation's translations ---");
        // 22 October: "the user's translation file is in the previously created folder ... import the language titles from the
        // file and only the available translation keys - the older file might have unused keys"
        TranslationEdits.ProgramFolder = dir;
        var old = Path.Combine(dir, "older"); var oldBin = Path.Combine(old, "bin", "Debug"); Directory.CreateDirectory(oldBin);
        File.WriteAllLines(Path.Combine(oldBin, "CommonTranslations.csv"), new[] { "Key;EN;TR;RU;PL", "Btn_Ok;OK;Tamam;OK;OK", "Btn_Stop;Stop;Durdur;Stop;Stop",
                                                                                "Old_Gone;Gone;Yok;;", "Msg_Saved;{0} saved.;{0} kaydedildi.;{0} sokhraneno.;{0} zapisano." });
        File.WriteAllLines(Path.Combine(oldBin, "Translations.csv"), new[] { "Key;EN;TR;RU", "Unit_Clamp;Clamp;Mengene;Zazhim", "Btn_Stop;Stop the unit;Üniteyi durdur;Ostanovit",
                                                                          "Ref_Home;Reference;Referans;" });
        File.WriteAllText(Path.Combine(oldBin, "Alarms.json"), original[2].Replace("Kapi acik", "Kapı açık"));
        // this panel's history: the older installation's Btn_Stop (MAIN) was typed here
        File.AppendAllText(TranslationEdits.HistoryFile, "2026-10-20 10:00:00;op1;MAIN;Btn_Stop;TR;Uniteyi durdur;Üniteyi durdur\r\n");
        string asked = null;
        Kaban.UI.Profiles.Ask.Hook = (title, prompt, initial) => { told = prompt; if (prompt.Contains("languages this program has not")) asked = prompt; return "yes"; };
        answerWith = f => { items = ((List<TranslationReviewForm.Item>)Get(f, "Items")).ToList(); okText = (string)Get(f, "OkText"); return true; };
        r = Call(c, "ImportFolder", old);
        Check("the install's root picked: its files found under it (bin\\Debug)", (int)Get(r, "Blocks") == 3, Get(r, "Blocks") + "");
        Check("  a language it has that this one has not: asked, added with its name", asked != null && asked.Contains("PL (Polish)") &&
              ((List<string>)Get(r, "Added")).SequenceEqual(new[] { "PL" }) && grid.Columns.Contains("L_PL") && TranslationEdits.NameOf("PL") == "Polish", asked);
        Check("  its key no longer used: left out", (int)Get(r, "OldKeys") == 1 && !File.ReadAllText(common).Contains("Old_Gone"));
        Func<string, string, TranslationReviewForm.Item> itemOf = (k, l) => items.FirstOrDefault(x => x.Row.Key == k && x.Lang == l && (k != "Btn_Stop" || x.Row.Owner == "MAIN"));
        Check("  the changes: ticked only what this panel's history says was typed here (who, when)",
              items.Count == 4 && itemOf("Btn_Stop", "TR").Take && itemOf("Btn_Stop", "TR").Why.StartsWith("entered on the panel: op1") &&
              !itemOf("Unit_Clamp", "TR").Take && itemOf("Unit_Clamp", "TR").Why == "not entered on this panel" && !itemOf("Alarm_MAIN.Door", "TR").Take &&
              items.Count(x => x.Row.Key == "Btn_Stop" && x.Row.Owner == "General" && !x.Take) == 1,
              string.Join(" | ", items.Select(x => x.Row.Owner + "/" + x.Row.Key + " " + x.Lang + " " + x.Now + ">" + x.New + " " + x.Take + " " + x.Why)));
        Check("  written: the empty cells (PL, RU, TR) and the ticked change - the others as they were",
              File.ReadAllLines(main).Contains("Btn_Stop;Stop the unit;Üniteyi durdur;Ostanovit") && File.ReadAllLines(main).Contains("Unit_Clamp;Clamp;Kıskaç (profil);Zazhim") &&
              File.ReadAllLines(main).Contains("Ref_Home;Reference;Referans;Referens") && File.ReadAllLines(common)[0] == "Key;EN;TR;RU;PL" &&
              File.ReadAllLines(common).Contains("Btn_Stop;Stop;Dur;Stop;Stop") && File.ReadAllLines(common).Contains("Msg_Saved;{0} saved.;{0} kaydedildi.;{0} sokhraneno.;{0} zapisano.") &&
              File.ReadAllText(alarms).Contains("Kapi acik") && TranslationEdits.History().Last().User == "maint (import)",
              string.Join("|", File.ReadAllLines(common)) + " // " + string.Join("|", File.ReadAllLines(main)));
        r = Call(c, "ImportFolder", Path.Combine(dir, "AI prompts"));
        Check("a folder without this program's files: said so", (bool)Get(r, "NoFiles") &&
              ((string)typeof(TranslationsControl).GetMethod("Report", Any).Invoke(null, new[] { r, false })).StartsWith("No translation file"));

        Console.WriteLine("--- translation files picked ---");
        var other = Path.Combine(dir, "other"); Directory.CreateDirectory(other);
        File.WriteAllLines(Path.Combine(other, "Translations.csv"), new[] { "Key;EN;TR", "Ref_Home;Reference;Referans noktası", "No_More;x;y" });
        File.WriteAllText(Path.Combine(other, "Alarms.json"), original[2].Replace("Kapi acik", "Kapı açık!"));
        answerWith = f => { items = ((List<TranslationReviewForm.Item>)Get(f, "Items")).ToList(); return true; };
        r = Call(c, "ImportFiles", new List<string> { Path.Combine(other, "Translations.csv"), Path.Combine(other, "Alarms.json") });
        Check("a Translations.csv (its folder no machine's: the main machine's), an Alarms.json - by key, by the alarm's TagCode",
              items.Count == 2 && items.Any(x => x.Row.Key == "Ref_Home" && x.New == "Referans noktası" && !x.Take) && items.Any(x => x.Row.Key == "Alarm_MAIN.Door" && x.New == "Kapı açık!") &&
              (int)Get(r, "OldKeys") == 1, string.Join(" | ", items.Select(x => x.Row.Key + " " + x.New)));

        Console.WriteLine("--- the panel's texts into the project's files ---");
        // 22 October: "a tool to import these translations to the project file with a confirmation system of the user entry translations"
        var proj = Path.Combine(dir, "project"); Directory.CreateDirectory(proj);
        string pCommon = Path.Combine(proj, "CommonTranslations.csv"), pMain = Path.Combine(proj, "Translations.csv"), pAlarms = Path.Combine(proj, "Alarms.json");
        File.WriteAllText(pCommon, original[0]);
        File.WriteAllText(pMain, original[1].Replace("Ref_Home;Reference;;", "Ref_Home;Reference point;;"));     // the developer's newer English
        File.WriteAllText(pAlarms, original[2]);
        var programFiles = new[] { File.ReadAllText(common), File.ReadAllText(main), File.ReadAllText(alarms) };
        int historyBefore = TranslationEdits.History().Count;
        TranslationEdits.ProjectFileOf = x => x.Common ? pCommon : x.File == "Alarms.json" ? pAlarms : pMain;
        answerWith = f => { items = ((List<TranslationReviewForm.Item>)Get(f, "Items")).ToList(); okText = (string)Get(f, "OkText"); return true; };
        r = Call(c, "IntoProject");
        Check("the changes: the texts entered on this panel ticked, the project's newer one not",
              itemOf("Unit_Clamp", "TR") != null && itemOf("Unit_Clamp", "TR").Take && itemOf("Unit_Clamp", "TR").Now == "Kiskac" &&
              itemOf("Btn_Stop", "TR") != null && itemOf("Btn_Stop", "TR").Take && itemOf("Ref_Home", "EN") != null && !itemOf("Ref_Home", "EN").Take &&
              itemOf("Ref_Home", "EN").Why.Contains("project's may be newer") && okText.StartsWith("Write into the project:"),
              okText + " / " + string.Join(" | ", items.Select(x => x.Row.Key + " " + x.Lang + " " + x.Now + ">" + x.New + " " + x.Take)));
        Check("  written into the project: the empty cells and the ticked - its newer text kept",
              File.ReadAllLines(pMain).Contains("Unit_Clamp;Clamp;Kıskaç (profil);Zazhim") && File.ReadAllLines(pMain).Contains("Ref_Home;Reference point;Referans;Referens") &&
              File.ReadAllLines(pCommon)[0] == "Key;EN;TR;RU;PL" && File.ReadAllLines(pCommon).Contains("Lbl_Speed;Speed;Hız;Skorost") &&
              File.ReadAllLines(pMain).Contains("Btn_Stop;Stop the unit;Üniteyi durdur;Ostanovit"),
              string.Join("|", File.ReadAllLines(pMain)) + " // " + string.Join("|", File.ReadAllLines(pCommon)));
        Check("  the program's own files and its history untouched", File.ReadAllText(common) == programFiles[0] && File.ReadAllText(main) == programFiles[1] &&
              File.ReadAllText(alarms) == programFiles[2] && TranslationEdits.History().Count == historyBefore);
        TranslationEdits.ProjectFileOf = x => null;
        r = Call(c, "IntoProject");
        Check("not run from its build (no project): said so", (bool)Get(r, "NoFiles") &&
              ((string)typeof(TranslationsControl).GetMethod("Report", Any).Invoke(null, new[] { r, true })).StartsWith("The project's files are not found"));
        TranslationReviewForm.Hook = null;

        Console.WriteLine("--- a filter: the part asked about ---");
        var owner = (ComboBox)typeof(TranslationsControl).GetField("_owner", Any).GetValue(c);
        for (int i = 0; i < owner.Items.Count; i++) if (owner.Items[i].ToString() == "MAIN") owner.SelectedIndex = i;
        Application.DoEvents();
        p = (string)Call(c, "AiPrompt", TranslationAiPrompt.Mode.Check);
        Check("MAIN's rows only: its file alone", p.Contains("### MAIN_Translations.csv") && !p.Contains("### CommonTranslations.csv") && p.Contains(" 4 keys, "),
              p.Substring(p.IndexOf("## The files"), 400));

        form.Close();
        MachineRegistry.Reset();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "TRANSLATION AI ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
