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

        Console.WriteLine("--- the AI's answer imported ---");
        var answer = string.Join("\r\n", new[] {
            "Here are the files.", "", "### CommonTranslations.csv", "```csv", "Key;EN;TR;RU;Note",
            "Lbl_Speed;;Hız;;the label's usual word", "Msg_Saved;;Kaydedildi.;;placeholder forgotten", "Btn_Ok;;Tamam;;", "```", "",
            "**MAIN_Translations.csv**", "```csv", "Key;EN;TR;RU;FR;Note",
            "Unit_Clamp;;Kıskaç;\"Zazhim; tiski\";Pince;the clamp of a profile", "Nope_Key;;x;;;", "```",
            "Btn_Stop could mean both files - here it is without a name:", "```", "Key;TR", "Btn_Stop;Durdur", "```", "Tell me if you want more." });
        var r = Call(c, "ImportAnswer", new List<KeyValuePair<string, string>> { Src(null, answer) });
        int typed = (int)Get(r, "Typed"), same = (int)Get(r, "Same"), refused = (int)Get(r, "Refused");
        var notFound = (List<string>)Get(r, "NotFound");
        var unknown = (List<string>)Get(r, "Unknown");
        Check("the answer copied: its three blocks; 3 texts typed, 1 the same, 1 refused (its {0} lost)", (int)Get(r, "Blocks") == 3 && typed == 3 && same == 1 && refused == 1,
              Get(r, "Blocks") + " " + typed + " " + same + " " + refused);
        Check("  a key the table has not, a key of two files without the file's name: left out", notFound.Contains("Nope_Key") && notFound.Any(n => n.StartsWith("Btn_Stop")),
              string.Join(", ", notFound));
        Check("  a language the table has not: said", unknown.SequenceEqual(new[] { "FR" }), string.Join(",", unknown));
        Check("  typed, not saved: orange until Save, the files as they were", (int)Get(c, "PendingCount") == 3 && LanguageManager.GetStringIn("TR", "Lbl_Speed") != "Hız" &&
              File.ReadAllLines(common).Contains("Lbl_Speed;Speed;;Skorost"));
        var rows = (List<TranslationEdits.Row>)Get(c, "_rows");
        Func<string, string, TranslationEdits.Row> rowOf = (o, k) => rows.First(x => x.Owner == o && x.Key == k);
        var tip = (string)Call(c, "TipOf", rowOf("MAIN", "Unit_Clamp"), "RU");
        Check("  its cell's tooltip: the AI's note, the file's text", tip == "AI: the clamp of a profile\r\nIn the file: Zazhim", tip);
        Check("  a ';' in a text (quoted): made a ','", Convert.ToString(grid.Rows[Shown(c).ToList().FindIndex(x => x.Key == "Unit_Clamp")].Cells["L_RU"].Value) == "Zazhim, tiski");
        var report = (string)typeof(TranslationsControl).GetMethod("Report", Any).Invoke(null, new[] { r });
        Check("  the report: typed, the same, refused (which), left out, the language to add", report.Contains("3 text(s) of the AI typed") && report.Contains("Msg_Saved TR: {0}") &&
              report.Contains("Nope_Key") && report.Contains("FR"), report);
        Call(c, "Save");
        Check("Save: written into their files", File.ReadAllLines(common).Contains("Lbl_Speed;Speed;Hız;Skorost") && File.ReadAllLines(main).Contains("Unit_Clamp;Clamp;Kıskaç;Zazhim, tiski") &&
              TranslationEdits.History().Count == 3 && (int)Get(c, "PendingCount") == 0 && Call(c, "TipOf", rowOf("MAIN", "Unit_Clamp"), "RU") == null,
              string.Join("|", File.ReadAllLines(main)));

        Console.WriteLine("--- its files imported ---");
        var file = Path.Combine(dir, "MAIN_Translations (1).csv");
        File.WriteAllText(file, "\uFEFFKey;TR;RU;Note\r\nBtn_Stop;Üniteyi durdur;;the unit's stop\r\nRef_Home;Referans;Referens;\r\n");
        r = Call(c, "ImportAnswer", new List<KeyValuePair<string, string>> { Src(Path.GetFileName(file), File.ReadAllText(file)) });
        Check("a file of the AI: its name says whose (MAIN's Btn_Stop, not the common one)", (int)Get(r, "Typed") == 3 &&
              Call(c, "TipOf", rowOf("MAIN", "Btn_Stop"), "TR") != null && Call(c, "TipOf", rowOf("General", "Btn_Stop"), "TR") == null);
        Call(c, "Revert");
        Check("  Revert: thrown away, notes too", (int)Get(c, "PendingCount") == 0 && Call(c, "TipOf", rowOf("MAIN", "Btn_Stop"), "TR") == null);
        r = Call(c, "ImportAnswer", new List<KeyValuePair<string, string>> { Src(null, "Sorry, I cannot do that.") });
        Check("nothing like a translation file: said so", (int)Get(r, "Blocks") == 0 &&
              ((string)typeof(TranslationsControl).GetMethod("Report", Any).Invoke(null, new[] { r })).StartsWith("Nothing to import"));

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
