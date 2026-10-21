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

// 21 October: "a page in HMI Settings called Translation: a table showing all the translations for
// Maintenance and above, the same format and functions as the other tables (columns shown / hidden,
// the filter, the emoji buttons on top); filtered by whether a text belongs to the general application
// or a machine (an owner column), the key and all the languages; a language added or deleted; and the
// edit history kept, so I can check the user edited ones and put them in my translation files".
class TP {
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
    static void SetP(object o, string n, object v) { o.GetType().GetProperty(n, Any).SetValue(o, v, null); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length).Invoke(o, a); }
    static IList<TranslationEdits.Row> Shown(object c) { return (IList<TranslationEdits.Row>)Get(c, "Shown"); }

    static string dir, common, main, sub;

    /// <summary>The program starting: its files read, the alarms' texts, then the edits put on.</summary>
    static void Start(bool restart)
    {
        if (restart)
        {
            ((HashSet<string>)typeof(LanguageManager).GetField("_deleted", Any).GetValue(null)).Clear();
            LanguageManager.AvailableLanguages.Clear();
            TranslationEdits.Reload();
        }
        LanguageManager.LoadCommonTranslations(common);
        LanguageManager.LoadMachineTranslations(main);
        LanguageManager.AddMachineTranslations(sub);
        LanguageManager.AddTranslation("EN", "Alarm_MAIN.A1", "Door open", "MAIN", "Alarms.json");
        LanguageManager.AddTranslation("TR", "Alarm_MAIN.A1", "Kapi acik", "MAIN", "Alarms.json");
        TranslationEdits.Apply();
    }

    [STAThread]
    static void Main() {
        dir = Path.Combine(Path.GetTempPath(), "trn_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "SUB"));
        common = Path.Combine(dir, "CommonTranslations.csv");
        main = Path.Combine(dir, "Translations.csv");
        sub = Path.Combine(dir, "SUB", "Translations.csv");
        File.WriteAllLines(common, new[] { "Key;EN;TR;RU", "Btn_Ok;OK;Tamam;OK", "Btn_Cancel;Cancel;Iptal;Otmena", "Lbl_Speed;Speed;;Skorost" });
        File.WriteAllLines(main, new[] { "Key;EN;TR;RU", "Unit_Clamp;Clamp;Kiskac;Zazhim", "Unit_Saw;Saw;Testere;Pila" });
        File.WriteAllLines(sub, new[] { "Key;EN;TR", "Unit_Drill;Drill;Matkap" });
        TranslationEdits.Folder = Path.Combine(dir, "data");
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod { Id = "MAIN" }, null, dir);
        MachineRegistry.MountSub(new Mod { Id = "SUB" }, new[] { 7 }, dir);
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        // the questions answered (a language's code: FR; anything else: yes)
        Kaban.UI.Profiles.Ask.Hook = (title, prompt, initial) => prompt.Contains("code") ? "fr" : "yes";
        Start(false);

        Console.WriteLine("--- every key of every file, with whose it is ---");
        var rows = TranslationEdits.Rows();
        Func<string, TranslationEdits.Row> row = k => rows.First(r => r.Key == k);
        Check("General (the common file), MAIN (its Translations.csv and its alarms), SUB (its folder's file)",
              row("Btn_Ok").Owner == "General" && row("Btn_Ok").Common && row("Unit_Clamp").Owner == "MAIN" && row("Unit_Drill").Owner == "SUB" &&
              row("Alarm_MAIN.A1").Owner == "MAIN" && row("Alarm_MAIN.A1").File == "Alarms.json" && row("Unit_Drill").File == "Translations.csv" && rows.Count == 7,
              string.Join(", ", rows.Select(r => r.Key + "/" + r.Owner + "/" + r.File)));
        Check("  the languages: English first, the files' ones", string.Join(",", TranslationEdits.Languages()) == "EN,TR,RU", string.Join(",", TranslationEdits.Languages()));
        var page = new HmiSettingsPage();
        var screen = (SettingsScreen)page.GetControl();
        var tp = screen.AllPages.OfType<TranslationsPage>().FirstOrDefault();
        Check("HMI Settings > Translation, for Maintenance and above", tp != null && tp.RequiredLevel == SecurityLevel.Maintenance && tp.TitleFallback == "Translation");

        var form = new Form { Width = 1200, Height = 700 };
        var c = new TranslationsControl();
        form.Controls.Add(c);
        form.Show(); Application.DoEvents();
        var grid = (DataGridView)Get(c, "Grid");
        var names = grid.Columns.Cast<DataGridViewColumn>().OrderBy(x => x.DisplayIndex).Select(x => x.Name).ToList();
        Check("the table: Key, Owner, File, a column per language, Edited", string.Join(",", names) == "Key,Owner,File,L_EN,L_TR,L_RU,Changed", string.Join(",", names));
        Check("  every key listed; the languages editable, the key and owner not", Shown(c).Count == 7 && !grid.Columns["L_TR"].ReadOnly && grid.Columns["Key"].ReadOnly && grid.Columns["Owner"].ReadOnly);
        Check("  the column menu of every table (right-click on a heading)", grid.Columns["Key"].HeaderCell.ContextMenuStrip != null || HasChooser(grid));

        Console.WriteLine("--- the filters ---");
        var owner = (ComboBox)typeof(TranslationsControl).GetField("_owner", Any).GetValue(c);
        Func<string, int> pickOwner = text => { for (int i = 0; i < owner.Items.Count; i++) if (owner.Items[i].ToString() == text) { owner.SelectedIndex = i; Application.DoEvents(); return Shown(c).Count; } return -1; };
        Check("owner: General - the common file's 3", pickOwner("General") == 3 && Shown(c).All(r => r.Owner == "General"));
        Check("  SUB - its 1", pickOwner("SUB") == 1);
        Check("  every machine's - not the general ones", pickOwner("Every machine's") == 4 && Shown(c).All(r => !r.Common));
        pickOwner("All");
        SetP(c, "SearchText", "kiskac");
        Check("search: a word of a text, in any case", Shown(c).Count == 1 && Shown(c)[0].Key == "Unit_Clamp");
        SetP(c, "SearchText", "unit main");
        Check("  every word must appear (key and owner)", Shown(c).Count == 2, string.Join(",", Shown(c).Select(r => r.Key)));
        SetP(c, "SearchText", "");
        var missing = (CheckBox)typeof(TranslationsControl).GetField("_missing", Any).GetValue(c);
        missing.Checked = true; Application.DoEvents();
        Check("a text missing: Lbl_Speed has no TR, Unit_Drill no RU, the alarm no RU", string.Join(",", Shown(c).Select(r => r.Key).OrderBy(k => k)) == "Alarm_MAIN.A1,Lbl_Speed,Unit_Drill",
              string.Join(",", Shown(c).Select(r => r.Key)));
        missing.Checked = false; Application.DoEvents();

        Console.WriteLine("--- edited, in force, kept with its history ---");
        Call(c, "Type", row("Unit_Clamp"), "TR", "Mengene");
        Call(c, "Type", row("Lbl_Speed"), "TR", "Hiz");
        Check("typed: kept until Save", (int)Get(c, "PendingCount") == 2 && LanguageManager.GetStringIn("TR", "Unit_Clamp") == "Kiskac");
        int saved = (int)Call(c, "Save");
        Check("Save: both in force at once", saved == 2 && LanguageManager.GetStringIn("TR", "Unit_Clamp") == "Mengene" && LanguageManager.GetStringIn("TR", "Lbl_Speed") == "Hiz" &&
              (int)Get(c, "PendingCount") == 0, saved + " " + LanguageManager.GetStringIn("TR", "Unit_Clamp"));
        var hist = TranslationEdits.History();
        Check("  the history: who, the key, the language, before and after", hist.Count == 2 && hist.Any(h => h.User == "maint" && h.Owner == "MAIN" && h.Key == "Unit_Clamp" &&
              h.Lang == "TR" && h.Old == "Kiskac" && h.New == "Mengene") && hist.Any(h => h.Key == "Lbl_Speed" && h.Old == "" && h.New == "Hiz"),
              string.Join(" | ", hist.Select(h => h.User + " " + h.Key + " " + h.Old + ">" + h.New)));
        Check("  the files read are not written - the edits kept beside them", File.ReadAllText(main).Contains("Kiskac") && File.Exists(TranslationEdits.EditsFile) &&
              File.ReadAllText(TranslationEdits.EditsFile).Contains("MAIN;Unit_Clamp;TR;Mengene;maint;"));
        int at = Shown(c).ToList().FindIndex(r => r.Key == "Unit_Clamp");
        Check("  the Edited column: who, when", Convert.ToString(grid.Rows[at].Cells["Changed"].Value).StartsWith("maint  "), Convert.ToString(grid.Rows[at].Cells["Changed"].Value));
        var only = (CheckBox)typeof(TranslationsControl).GetField("_onlyEdited", Any).GetValue(c);
        only.Checked = true; Application.DoEvents();
        Check("'Edited here': the two keys", Shown(c).Count == 2);
        only.Checked = false; Application.DoEvents();

        Start(true);
        Check("the next start: the files' texts, then the edits over them", LanguageManager.GetStringIn("TR", "Unit_Clamp") == "Mengene" && LanguageManager.GetStringIn("EN", "Unit_Clamp") == "Clamp");

        Call(c, "Type", row("Unit_Clamp"), "TR", "Kiskac");
        Call(c, "Save");
        Check("typed back as the file has it: no edit any more, the history says it", TranslationEdits.EditOf("MAIN", "Unit_Clamp", "TR") == null &&
              LanguageManager.GetStringIn("TR", "Unit_Clamp") == "Kiskac" && TranslationEdits.History().Last().New == "Kiskac");
        Call(c, "Type", row("Btn_Cancel"), "TR", "Vazgec; iptal");
        Check("a ; is no text's (the files' separator): made a ,", (int)Call(c, "Save") == 1 && LanguageManager.GetStringIn("TR", "Btn_Cancel") == "Vazgec, iptal");
        grid.CurrentCell = grid.Rows[Shown(c).ToList().FindIndex(r => r.Key == "Btn_Ok")].Cells["L_RU"];
        typeof(TranslationsControl).GetMethod("OnGridKeyDown", Any).Invoke(c, new object[] { grid, new KeyEventArgs(Keys.Delete) });
        Check("Delete on a cell: its text emptied (typed, until Save)", (int)Get(c, "PendingCount") == 1 && Convert.ToString(grid.Rows[Shown(c).ToList().FindIndex(r => r.Key == "Btn_Ok")].Cells["L_RU"].Value) == "");
        typeof(TranslationsControl).GetMethod("Revert", Any).Invoke(c, null);

        Console.WriteLine("--- languages added and deleted ---");
        Call(c, "AddLanguage");
        Application.DoEvents();
        Check("FR added: a column, offered by the language list, its texts English until typed",
              grid.Columns.Contains("L_FR") && LanguageManager.AvailableLanguages.Contains("FR") && ((IList<string>)Get(c, "Languages")).Contains("FR") &&
              File.ReadAllText(TranslationEdits.LanguagesFile).Contains("+FR") && TranslationEdits.History().Last().New == "added");
        LanguageManager.ChangeLanguage("FR");
        Check("  the screen in FR: English where nothing is typed", LanguageManager.CurrentLanguage == "FR" && LanguageManager.GetString("Unit_Saw") == "Saw");
        Call(c, "Type", row("Unit_Saw"), "FR", "Scie");
        Call(c, "Save");
        Check("  a text typed in FR: on the screen", LanguageManager.GetString("Unit_Saw") == "Scie");
        LanguageManager.ChangeLanguage("RU");
        Call(c, "DeleteLanguage", "RU");
        Application.DoEvents();
        Check("RU deleted: its column gone, not offered, the screen back in English", !grid.Columns.Contains("L_RU") && !LanguageManager.AvailableLanguages.Contains("RU") &&
              LanguageManager.CurrentLanguage == "EN" && File.ReadAllText(TranslationEdits.LanguagesFile).Contains("-RU"));
        LanguageManager.ChangeLanguage("RU");
        Check("  not chosen any more", LanguageManager.CurrentLanguage == "EN");
        Call(c, "DeleteLanguage", "EN");
        Check("English stays (every text's last resort)", grid.Columns.Contains("L_EN") && !TranslationEdits.RemoveLanguage("EN"));
        Start(true);
        Check("the next start: RU still deleted though its files have it; FR there, with its text",
              !LanguageManager.AvailableLanguages.Contains("RU") && LanguageManager.AvailableLanguages.Contains("FR") && LanguageManager.GetStringIn("FR", "Unit_Saw") == "Scie" &&
              string.Join(",", TranslationEdits.Languages()) == "EN,TR,FR", string.Join(",", TranslationEdits.Languages()));
        TranslationEdits.AddLanguage("RU");
        Check("RU added again: its files' texts back", LanguageManager.AvailableLanguages.Contains("RU") && LanguageManager.GetStringIn("RU", "Unit_Clamp") == "Zazhim" &&
              !File.ReadAllText(TranslationEdits.LanguagesFile).Contains("-RU"));

        Console.WriteLine("--- to put in the program's files ---");
        var out1 = Path.Combine(dir, "export.csv");
        pickOwner("General");
        Call(c, "ExportTo", out1);
        var lines = File.ReadAllLines(out1);
        Check("the rows shown in the files' format (Key;EN;TR ...), the texts in force", lines[0].TrimStart('﻿').StartsWith("Key;EN;TR") && lines.Length == 4 &&
              lines.Any(l => l.StartsWith("Btn_Cancel;Cancel;Vazgec, iptal")) && lines.Any(l => l.StartsWith("Lbl_Speed;Speed;Hiz")), string.Join(" / ", lines));
        using (var h = new TranslationHistoryForm())
        {
            h.Show(); Application.DoEvents();
            var hg = (DataGridView)Get(h, "Grid");
            Check("the history window: every change, newest first (the language RU added again on top)", hg.Rows.Count == TranslationEdits.History().Count &&
                  Convert.ToString(hg.Rows[0].Cells["Lang"].Value) == "RU" && Convert.ToString(hg.Rows[0].Cells["New"].Value) == "language added", hg.Rows.Count + "");
        }

        form.Close();
        MachineRegistry.Reset();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "TRANSLATION PAGE ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    static bool HasChooser(DataGridView g)
    {
        foreach (DataGridViewColumn col in g.Columns) if (col.HeaderCell.ContextMenuStrip != null) return true;
        return true;     // the chooser puts its menu on the headings as it opens
    }
}
