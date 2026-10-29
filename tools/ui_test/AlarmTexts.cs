using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Settings;

// 22 October: the alarm texts in Alarms.json, "inherited from the Industry 4 program, kept as they are for now ...
// The two alarm handlings - Industry 4 logging and the HMI's - are to be combined when Industry 4 logging comes into
// this program; the alarm texts go to the translation files then." - 30 October: "Yes". The texts are rows of the
// machine's Translations.csv (with the Industry 4 program's Turkish), Alarms.json keeps the alarm and where the PLC's
// log records it.
class AT {
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

    [STAThread]
    static void Main() {
        Console.WriteLine("--- FA2010's alarms: their texts in its Translations.csv ---");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        int n = AlarmDefinitionLoader.Load("Alarms.json", true, "FA2010");
        LanguageManager.LoadMachineTranslations("Translations.csv");
        AlarmDefinitionLoader.ReapplyTranslations();
        var defs = AlarmManager.GetAllDefinitions().Where(d => d.ModuleId == "FA2010").ToList();
        Check("157 alarms, Alarms.json without a text of its own", n == 157 && defs.Count == 157 &&
              AlarmDefinitionLoader.LoadedEntries.All(e => e.Translations == null) && !File.ReadAllText("Alarms.json").Contains("\"Translations\""), n + "");
        Check("  each one's text a row of Translations.csv: Alarm_FA2010.<TagCode>, English and Turkish", defs.All(d =>
              d.TranslationKey == "Alarm_" + d.AlarmCode && LanguageManager.InMachineFile(d.TranslationKey, "EN") && LanguageManager.InMachineFile(d.TranslationKey, "TR")),
              string.Join(", ", defs.Where(d => !LanguageManager.InMachineFile(d.TranslationKey, "TR")).Select(d => d.TranslationKey).Take(5)));
        var m801 = AlarmManager.FindByCode("FA2010.Robot_X_Error");
        Check("M801 in Turkish: the Industry 4 program's words (Robot X Arıza, not the PLC's tag Eksen1_Ariza); English as the HMI had it",
              LanguageManager.GetStringIn("TR", m801.TranslationKey) == "Robot X Arıza" && LanguageManager.GetStringIn("EN", m801.TranslationKey) == "Robot X Error",
              LanguageManager.GetStringIn("TR", m801.TranslationKey));
        var m855 = defs.First(d => d.Address == "M855");
        Check("  an alarm the HMI spelled better keeps its English (Length, not Lenght), takes the Industry 4 Turkish",
              LanguageManager.GetStringIn("EN", m855.TranslationKey) == "Bar Length is Too Short to Operate" && LanguageManager.GetStringIn("TR", m855.TranslationKey) != "",
              LanguageManager.GetStringIn("TR", m855.TranslationKey));
        Check("where the PLC's log records it: M801 - state 4, First 32 Errors (1), bit 0 (every FA2010 alarm has its place)",
              m801.Log != null && m801.Log.Code == 4 && m801.Log.Value == "First 32 Errors (1)" && m801.Log.Bit == 0 && defs.All(d => d.Log != null),
              m801.Log == null ? "none" : m801.Log.ToString());
        Check("  written in Alarms.json as \"Log\" (the comment kept)", File.ReadAllText("Alarms.json").Contains("\"Log\": {") &&
              File.ReadAllText("Alarms.json").Contains("\"Comment\": \"StateCode 4 / First 32 Errors (1) bit 0\""));
        Check("  the Industry 4 dashboard's files as they were (its reports unchanged)",
              File.ReadAllText(Path.Combine("Dashboard", "Translation_JSON.csv")).Contains("Robot X Error;Robot X Error;Robot X Arıza"));
        foreach (var sub in new[] { "H500", "H800" })
        {
            var f = Path.Combine(sub, "Alarms.json");
            if (!File.Exists(f)) continue;
            AlarmDefinitionLoader.Load(f, false, sub);
            LanguageManager.AddMachineTranslations(Path.Combine(sub, "Translations.csv"));
        }
        AlarmDefinitionLoader.ReapplyTranslations();
        var h500 = AlarmManager.GetAllDefinitions().Where(d => d.ModuleId == "H500").ToList();
        Check("H500's alarms too (its own Translations.csv): no clash with FA2010's of the same address", h500.Count > 0 && h500.All(d => LanguageManager.InMachineFile(d.TranslationKey, "EN")) &&
              LanguageManager.GetStringIn("TR", m801.TranslationKey) == "Robot X Arıza", h500.Count + " alarms");

        Console.WriteLine("--- an older installation's Alarms.json, its texts still in it ---");
        var dir = Path.Combine(Path.GetTempPath(), "at_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var common = Path.Combine(dir, "CommonTranslations.csv");
        var main = Path.Combine(dir, "Translations.csv");
        var alarms = Path.Combine(dir, "Alarms.json");
        File.WriteAllLines(common, new[] { "Key;EN;TR;RU", "Btn_Ok;OK;Tamam;OK" });
        File.WriteAllLines(main, new[] { "Key;EN;TR;RU", "Unit_Clamp;Clamp;Kıskaç;Zazhim", "Alarm_MAIN.Door;Door open;Kapı açık;" });
        File.WriteAllText(alarms, "[\r\n  {\r\n    \"PlcAddress\": \"M801\",\r\n    \"TagCode\": \"Door\",\r\n" +
                                  "    \"Translations\": { \"EN\": \"Door (old)\", \"TR\": \"Kapi (eski)\", \"RU\": \"Dver\" }\r\n  }\r\n]\r\n");
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod { Id = "MAIN" }, null, dir);
        TranslationEdits.Folder = Path.Combine(dir, "data");
        TranslationEdits.ProgramFolder = dir;
        LanguageManager.LoadCommonTranslations(common);
        AlarmDefinitionLoader.Load(alarms, true, "MAIN");
        LanguageManager.LoadMachineTranslations(main);
        AlarmDefinitionLoader.ReapplyTranslations();
        Check("the translation file's texts win (EN, TR); a language it lacks from the older file (RU)",
              LanguageManager.GetStringIn("EN", "Alarm_MAIN.Door") == "Door open" && LanguageManager.GetStringIn("TR", "Alarm_MAIN.Door") == "Kapı açık" &&
              LanguageManager.GetStringIn("RU", "Alarm_MAIN.Door") == "Dver",
              LanguageManager.GetStringIn("EN", "Alarm_MAIN.Door") + " / " + LanguageManager.GetStringIn("TR", "Alarm_MAIN.Door"));

        Console.WriteLine("--- HMI Settings > Translation: an alarm's text is its Translations.csv's ---");
        File.WriteAllText(alarms, "[\r\n  {\r\n    \"PlcAddress\": \"M801\",\r\n    \"TagCode\": \"Door\",\r\n    \"Severity\": \"Fault\"\r\n  }\r\n]\r\n");
        LanguageManager.LoadCommonTranslations(common);
        AlarmDefinitionLoader.Load(alarms, true, "MAIN");
        LanguageManager.LoadMachineTranslations(main);
        AlarmDefinitionLoader.ReapplyTranslations();
        TranslationEdits.Apply();
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        Kaban.UI.Profiles.Ask.Hook = (title, prompt, initial) => "yes";
        var form = new Form { Width = 1200, Height = 700 };
        var c = new TranslationsControl();
        form.Controls.Add(c);
        form.Show(); Application.DoEvents();
        Func<string, TranslationEdits.Row> row = k => ((IEnumerable<TranslationEdits.Row>)Get(c, "_rows")).First(r => r.Key == k);
        Check("the alarm's row: MAIN's, its file Translations.csv (once - not again as Alarms.json)", row("Alarm_MAIN.Door").Owner == "MAIN" &&
              row("Alarm_MAIN.Door").File == "Translations.csv" && ((IEnumerable<TranslationEdits.Row>)Get(c, "_rows")).Count(r => r.Key == "Alarm_MAIN.Door") == 1,
              row("Alarm_MAIN.Door").File);
        Call(c, "Type", row("Alarm_MAIN.Door"), "TR", "Kapı açık!");
        Call(c, "Type", row("Alarm_MAIN.Door"), "RU", "Dver otkryta");
        Call(c, "Save");
        Check("typed and saved: into Translations.csv - Alarms.json untouched", File.ReadAllLines(main).Contains("Alarm_MAIN.Door;Door open;Kapı açık!;Dver otkryta") &&
              !File.ReadAllText(alarms).Contains("Translations") && LanguageManager.GetStringIn("TR", "Alarm_MAIN.Door") == "Kapı açık!",
              string.Join("|", File.ReadAllLines(main)));
        Check("  its AI prompt says what it is: an alarm's message", Kaban.UI.Helpers.TranslationAiPrompt.Describe(row("Alarm_MAIN.Door"), new List<Kaban.UI.Helpers.TextUses.Use>()).StartsWith("an alarm's message"),
              Kaban.UI.Helpers.TranslationAiPrompt.Describe(row("Alarm_MAIN.Door"), new List<Kaban.UI.Helpers.TextUses.Use>()));

        Console.WriteLine("--- an older installation's translations: its alarm texts from its Alarms.json ---");
        var old = Path.Combine(dir, "older"); Directory.CreateDirectory(old);
        File.WriteAllLines(Path.Combine(old, "CommonTranslations.csv"), new[] { "Key;EN;TR;RU", "Btn_Ok;OK;Tamam;OK" });
        File.WriteAllLines(Path.Combine(old, "Translations.csv"), new[] { "Key;EN;TR;RU", "Unit_Clamp;Clamp;Kıskaç;Zazhim" });
        File.WriteAllText(Path.Combine(old, "Alarms.json"), "[ { \"PlcAddress\": \"M801\", \"TagCode\": \"Door\", \"Translations\": { \"EN\": \"Door open\", \"TR\": \"Kapı açık (eski)\" } } ]");
        List<TranslationReviewForm.Item> items = null;
        TranslationReviewForm.Hook = f => { items = ((List<TranslationReviewForm.Item>)Get(f, "Items")).ToList(); return true; };
        var res = Call(c, "ImportFolder", old);
        Check("the older Alarms.json's texts offered for the alarm's row (in Translations.csv now)", items != null &&
              items.Any(x => x.Row.Key == "Alarm_MAIN.Door" && x.Lang == "TR" && x.New == "Kapı açık (eski)"),
              items == null ? "no review (" + Get(res, "Blocks") + " files)" : string.Join(" | ", items.Select(x => x.Row.Key + " " + x.Lang + " " + x.New)));
        items = null;
        res = Call(c, "ImportFiles", new List<string> { Path.Combine(old, "Alarms.json") });
        Check("  an Alarms.json picked: the same, by the alarm's TagCode", items != null && items.Any(x => x.Row.Key == "Alarm_MAIN.Door" && x.New == "Kapı açık (eski)"),
              items == null ? "no review" : string.Join(" | ", items.Select(x => x.Row.Key + " " + x.New)));
        form.Close();

        Console.WriteLine(fail == 0 ? "ALARM TEXTS ALL PASS" : "ALARM TEXTS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
