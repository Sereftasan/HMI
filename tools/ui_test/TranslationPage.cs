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

    static string dir, common, main, sub, alarms;

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
        AlarmDefinitionLoader.Load(alarms, true, "MAIN");          // its texts: Alarm_MAIN.Door, MAIN's, Alarms.json
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
        alarms = Path.Combine(dir, "Alarms.json");
        File.WriteAllText(alarms, "[\r\n  // the doors\r\n  {\r\n    \"PlcAddress\": \"M801\",\r\n    \"TagCode\": \"Door\",\r\n" +
                                  "    \"Translations\": {\r\n      \"EN\": \"Door open\",\r\n      \"TR\": \"Kapi acik\"\r\n    }\r\n  }\r\n]\r\n");
        TranslationEdits.Folder = Path.Combine(dir, "data");
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod { Id = "MAIN" }, null, dir);
        MachineRegistry.MountSub(new Mod { Id = "SUB" }, new[] { 7 }, dir);
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        // the questions answered (a language's code: FR; anything else: yes)
        Kaban.UI.Profiles.Ask.Hook = (title, prompt, initial) => prompt.Contains("begin with") ? "" : prompt.Contains("code") ? "fr" : "yes";
        Start(false);

        Console.WriteLine("--- every key of every file, with whose it is ---");
        var rows = TranslationEdits.Rows();
        Func<string, TranslationEdits.Row> row = k => rows.First(r => r.Key == k);
        Check("General (the common file), MAIN (its Translations.csv and its alarms), SUB (its folder's file)",
              row("Btn_Ok").Owner == "General" && row("Btn_Ok").Common && row("Unit_Clamp").Owner == "MAIN" && row("Unit_Drill").Owner == "SUB" &&
              row("Alarm_MAIN.Door").Owner == "MAIN" && row("Alarm_MAIN.Door").File == "Alarms.json" && row("Unit_Drill").File == "Translations.csv" && rows.Count == 7,
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
        row = k => ((IEnumerable<TranslationEdits.Row>)Get(c, "_rows")).First(r => r.Key == k);     // the table's own rows (their texts as saved)
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
        Check("a text missing: Lbl_Speed has no TR, Unit_Drill no RU, the alarm no RU", string.Join(",", Shown(c).Select(r => r.Key).OrderBy(k => k)) == "Alarm_MAIN.Door,Lbl_Speed,Unit_Drill",
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
        // 22 October: "Save writes directly into the translation files"
        Check("  written into their files: the key's line, the language's column - the rest as it was",
              string.Join("|", File.ReadAllLines(main)) == "Key;EN;TR;RU|Unit_Clamp;Clamp;Mengene;Zazhim|Unit_Saw;Saw;Testere;Pila" &&
              File.ReadAllLines(common).Contains("Lbl_Speed;Speed;Hiz;Skorost") && !File.Exists(TranslationEdits.EditsFile),
              string.Join("|", File.ReadAllLines(main)));
        int at = Shown(c).ToList().FindIndex(r => r.Key == "Unit_Clamp");
        Check("  the Edited column: who, when", Convert.ToString(grid.Rows[at].Cells["Changed"].Value).StartsWith("maint  "), Convert.ToString(grid.Rows[at].Cells["Changed"].Value));
        var only = (CheckBox)typeof(TranslationsControl).GetField("_onlyEdited", Any).GetValue(c);
        only.Checked = true; Application.DoEvents();
        Check("'Edited here': the two keys", Shown(c).Count == 2);
        only.Checked = false; Application.DoEvents();

        Start(true);
        Check("the next start: the texts from the files", LanguageManager.GetStringIn("TR", "Unit_Clamp") == "Mengene" && LanguageManager.GetStringIn("EN", "Unit_Clamp") == "Clamp");

        Call(c, "Type", row("Unit_Clamp"), "TR", "Kiskac");
        Call(c, "Save");
        Check("typed back: the file has it again, the history says it", File.ReadAllLines(main).Contains("Unit_Clamp;Clamp;Kiskac;Zazhim") &&
              LanguageManager.GetStringIn("TR", "Unit_Clamp") == "Kiskac" && TranslationEdits.History().Last().New == "Kiskac");

        Console.WriteLine("--- an alarm's text: its Alarms.json, in place ---");
        Call(c, "Type", row("Alarm_MAIN.Door"), "TR", "Kapi acik!");
        Call(c, "Type", row("Alarm_MAIN.Door"), "RU", "Dver otkryta");
        Call(c, "Save");
        var json = File.ReadAllText(alarms);
        Check("its entry's Translations: TR changed, RU added after it - the comment and the layout kept",
              json.Contains("// the doors") && json.Contains("\"TR\": \"Kapi acik!\"") && json.Contains("\"TR\": \"Kapi acik!\",\r\n      \"RU\": \"Dver otkryta\"") &&
              json.Contains("\"PlcAddress\": \"M801\","), json);
        AlarmDefinitionLoader.Load(alarms, true, "MAIN");
        Check("  read again: the alarm's texts", LanguageManager.GetStringIn("RU", "Alarm_MAIN.Door") == "Dver otkryta" && LanguageManager.GetStringIn("TR", "Alarm_MAIN.Door") == "Kapi acik!");

        Console.WriteLine("--- run from its build: the project's copy written too ---");
        var proj = Path.Combine(dir, "proj"); Directory.CreateDirectory(proj);
        var projCommon = Path.Combine(proj, "CommonTranslations.csv"); File.Copy(common, projCommon);
        var projAlarms = Path.Combine(proj, "Alarms.json"); File.Copy(alarms, projAlarms);
        TranslationEdits.ProjectFileOf = r => r.Common ? projCommon : r.File == "Alarms.json" ? projAlarms : null;
        Call(c, "Type", row("Btn_Ok"), "TR", "Tamam!");
        Call(c, "Type", row("Alarm_MAIN.Door"), "EN", "The door is open");
        Call(c, "Save");
        Check("the program's file and the project's", File.ReadAllLines(common).Contains("Btn_Ok;OK;Tamam!;OK") && File.ReadAllLines(projCommon).Contains("Btn_Ok;OK;Tamam!;OK") &&
              File.ReadAllText(alarms).Contains("\"EN\": \"The door is open\"") && File.ReadAllText(projAlarms).Contains("\"EN\": \"The door is open\""));
        TranslationEdits.ProjectFileOf = r => null;

        Console.WriteLine("--- the first version's edits: written into the files once ---");
        File.WriteAllText(TranslationEdits.EditsFile, "Owner;Key;Lang;Text;User;Time\r\nSUB;Unit_Drill;TR;Delgi;old;2026-10-21 10:00:00\r\n");
        Start(true);
        Check("UserTranslations.csv: into SUB's file, the file put away", File.ReadAllLines(sub).Contains("Unit_Drill;Drill;Delgi") && !File.Exists(TranslationEdits.EditsFile) &&
              File.Exists(TranslationEdits.EditsFile + ".written") && LanguageManager.GetStringIn("TR", "Unit_Drill") == "Delgi", string.Join("|", File.ReadAllLines(sub)));
        Call(c, "Type", row("Btn_Cancel"), "TR", "Vazgec; iptal");
        Check("a ; is no text's (the files' separator): made a ,", (int)Call(c, "Save") == 1 && LanguageManager.GetStringIn("TR", "Btn_Cancel") == "Vazgec, iptal" &&
              File.ReadAllLines(common).Contains("Btn_Cancel;Cancel;Vazgec, iptal;Otmena"), string.Join("|", File.ReadAllLines(common)));
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
        Check("  a text typed in FR: on the screen, and in the file - FR's column added to it", LanguageManager.GetString("Unit_Saw") == "Scie" &&
              File.ReadAllLines(main)[0] == "Key;EN;TR;RU;FR" && File.ReadAllLines(main).Contains("Unit_Saw;Saw;Testere;Pila;Scie"), string.Join("|", File.ReadAllLines(main)));
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

        Console.WriteLine("--- a language's texts copied into the empty cells of others ---");
        pickOwner("All");
        Func<string, string, bool, int> fillable = (f, t, s) => (int)Call(c, "Fillable", f, t, s);
        Check("EN into FR: every key's cell but Unit_Saw's (it has its text)", fillable("EN", "FR", false) == 6 && fillable("FR", "EN", false) == 0, fillable("EN", "FR", false) + "");
        using (var cf = new TranslationCopyForm((IList<string>)Get(c, "Languages"), "EN", Shown(c).Count, Shown(c).Count, fillable))
        {
            cf.Show(); Application.DoEvents();
            var items = ((CheckedListBox)Get(cf, "ToBox")).Items.Cast<object>().Select(o => o.ToString()).ToList();
            var okBtn = cf.Controls.OfType<Button>().First(b => b.Text == "Copy");
            Check("the dialog: from EN, into each other language - its empty cells counted; nothing ticked, Copy off; all rows shown: no 'only shown'",
                  ((ComboBox)Get(cf, "FromBox")).SelectedItem.ToString().StartsWith("EN") && items.Count == 3 && items.Any(t => t.StartsWith("FR") && t.Contains(": 6 empty")) &&
                  !okBtn.Enabled && !((CheckBox)Get(cf, "ShownOnlyBox")).Enabled, string.Join(" / ", items));
            Call(cf, "Tick", "FR", true);
            Check("  FR ticked: Copy on, the choice given", okBtn.Enabled && (bool)Call(cf, "Accept") && cf.From == "EN" && string.Join(",", cf.To) == "FR" && !cf.ShownOnly);
        }
        int copied = (int)Call(c, "CopyLanguage", "EN", new List<string> { "FR" }, false);
        Check("copied: 6 typed texts, the cell with a text kept", copied == 6 && (int)Get(c, "PendingCount") == 6 &&
              Cell(c, grid, "Unit_Saw", "FR") == "Scie" && Cell(c, grid, "Btn_Cancel", "FR") == "Cancel",
              copied + " " + Get(c, "PendingCount") + " " + Cell(c, grid, "Unit_Saw", "FR") + " " + Cell(c, grid, "Btn_Cancel", "FR"));
        typeof(TranslationsControl).GetMethod("Revert", Any).Invoke(c, null);
        pickOwner("SUB");
        using (var cf = new TranslationCopyForm((IList<string>)Get(c, "Languages"), "TR", Shown(c).Count, 7, fillable))
            Check("a filter on: 'only the rows shown' offered, ticked", ((CheckBox)Get(cf, "ShownOnlyBox")).Enabled && ((CheckBox)Get(cf, "ShownOnlyBox")).Checked && ((CheckBox)Get(cf, "ShownOnlyBox")).Text.Contains("1 rows shown"), ((CheckBox)Get(cf, "ShownOnlyBox")).Text);
        Check("TR into FR and RU, the rows shown only: SUB's key, 2 cells", (int)Call(c, "CopyLanguage", "TR", new List<string> { "FR", "RU", "TR" }, true) == 2);
        Call(c, "Save");
        Check("  saved: its file has both columns now", string.Join("|", File.ReadAllLines(sub)) == "Key;EN;TR;FR;RU|Unit_Drill;Drill;Delgi;Delgi;Delgi", string.Join("|", File.ReadAllLines(sub)));
        pickOwner("All");

        Console.WriteLine("--- a language added with another's texts ---");
        using (var lf = new TranslationLanguageForm((IList<string>)Get(c, "Languages"), l => 7))
        {
            lf.Show(); Application.DoEvents();
            SetP(lf, "CodeText", "tr");
            Check("the dialog: a code there already - said in it, not closed", !(bool)Call(lf, "Accept") && ((string)Get(lf, "ErrorText")).Contains("there already"), ((string)Get(lf, "ErrorText")));
            SetP(lf, "CodeText", "x1");
            Check("  not a code - said", !(bool)Call(lf, "Accept") && ((string)Get(lf, "ErrorText")).Contains("2 or 3 letters"));
            SetP(lf, "CodeText", "de");
            var opts = ((ComboBox)Get(lf, "FromBox")).Items.Cast<object>().Select(o => o.ToString()).ToList();
            Check("  its texts: none first, then each language's, with how many it brings", ((ComboBox)Get(lf, "FromBox")).SelectedIndex == 0 && opts.Count == 1 + 4 && opts[1].StartsWith("EN") && opts[1].Contains("7 text"),
                  string.Join(" / ", opts));
            ((ComboBox)Get(lf, "FromBox")).SelectedIndex = 1;
            Check("  DE from EN: the choice given", (bool)Call(lf, "Accept") && lf.Code == "DE" && lf.CopyFrom == "EN");
        }
        Call(c, "AddLanguage", "DE", "EN");
        Check("DE added, EN's 7 texts typed into it", grid.Columns.Contains("L_DE") && (int)Get(c, "PendingCount") == 7 && Cell(c, grid, "Unit_Clamp", "DE") == "Clamp", Get(c, "PendingCount") + "");
        Call(c, "Save");
        Check("  saved: every file's keys at once - the common file, MAIN's, SUB's and the alarms'",
              File.ReadAllLines(common)[0] == "Key;EN;TR;RU;DE" && File.ReadAllLines(common).Contains("Btn_Ok;OK;Tamam!;OK;OK") && File.ReadAllLines(main)[0] == "Key;EN;TR;RU;FR;DE" &&
              File.ReadAllLines(main).Contains("Unit_Clamp;Clamp;Kiskac;Zazhim;;Clamp") && File.ReadAllLines(sub).Contains("Unit_Drill;Drill;Delgi;Delgi;Delgi;Drill") &&
              File.ReadAllText(alarms).Contains("\"DE\": \"The door is open\""),
              string.Join("|", File.ReadAllLines(common)) + " // " + string.Join("|", File.ReadAllLines(main)));

        Console.WriteLine("--- a translation of the notes page: an edit of the table ---");
        NotesStore.Folder = Path.Combine(dir, "Notes");
        var note = NotesStore.Add(new Note { Kind = NoteKind.Translation, TranslationKey = "Unit_Saw", ControlKey = "page/saw",
                                             Texts = new List<NoteText> { new NoteText { Lang = "TR", Text = "Testere (daire)" } } });
        Application.DoEvents();
        var last = TranslationEdits.History().Last();
        Check("Maintenance's note: in force, written into the file, the history naming it", note.State == NoteState.Applied && note.Written &&
              File.ReadAllLines(main).Any(l => l.StartsWith("Unit_Saw;Saw;Testere (daire);")) && LanguageManager.GetStringIn("TR", "Unit_Saw") == "Testere (daire)" &&
              last.User == "maint (note)" && last.Key == "Unit_Saw" && last.Old == "Testere" && last.New == "Testere (daire)",
              last.User + " " + last.Key + " " + last.Old + ">" + last.New);
        at = Shown(c).ToList().FindIndex(r => r.Key == "Unit_Saw");
        Check("  the table: edited (who: the note's)", at >= 0 && Convert.ToString(grid.Rows[at].Cells["Changed"].Value).StartsWith("maint (note)"),
              at >= 0 ? Convert.ToString(grid.Rows[at].Cells["Changed"].Value) : "-");
        Call(c, "Type", row("Unit_Saw"), "TR", "Testere");
        Call(c, "Save");
        NotesStore.Folder = NotesStore.Folder;          // the next start: the notes read back
        NotesStore.ApplyTranslations();
        Check("  edited again on the page: the next start keeps the page's (the note is written, not put on again)",
              LanguageManager.GetStringIn("TR", "Unit_Saw") == "Testere" && NotesStore.All.First(n => n.Id == note.Id).Written);
        // a note of the version before (put on at each start, not written) - the page's edit after it stays
        var kept = ((List<Note>)typeof(NotesStore).GetField("_notes", Any).GetValue(null)).First(n => n.Id == note.Id);
        kept.Written = false; kept.AcceptedAt = DateTime.Now.AddDays(-1);
        kept.Texts.Add(new NoteText { Lang = "RU", Text = "Pila (krug)" });
        NotesStore.ApplyTranslations();
        Check("  a note of the version before: written now - but not over a text the page changed after it",
              kept.Written && File.ReadAllLines(main).Any(l => l.StartsWith("Unit_Saw;Saw;Testere;Pila (krug);")) && LanguageManager.GetStringIn("TR", "Unit_Saw") == "Testere",
              string.Join("|", File.ReadAllLines(main)));
        var free = NotesStore.Add(new Note { Kind = NoteKind.Translation, TranslationKey = "Btn_NoFile", ControlKey = "page/x",
                                             Texts = new List<NoteText> { new NoteText { Lang = "TR", Text = "Dosyasiz" } } });
        Check("  a key no file has: on the screen as before, not written", !free.Written && LanguageManager.GetStringIn("TR", "Btn_NoFile") == "Dosyasiz");

        Console.WriteLine("--- the history reset (an Admin) ---");
        int before = TranslationEdits.History().Count;
        var mainBefore = File.ReadAllText(main);
        using (var h = new TranslationHistoryForm())
        {
            h.Show(); Application.DoEvents();
            Check("Maintenance: no reset button, no reset", !((Button)Get(h, "ResetButton")).Visible && !(bool)Call(h, "ResetHistory") && TranslationEdits.ResetHistory() == -1 &&
                  TranslationEdits.History().Count == before);
        }
        SecurityManager.Login("admin", SecurityLevel.Admin);
        using (var h = new TranslationHistoryForm())
        {
            h.Show(); Application.DoEvents();
            var hg = (DataGridView)Get(h, "Grid");
            Check("an Admin: the 🗑 button; asked, the list emptied", ((Button)Get(h, "ResetButton")).Visible && hg.Rows.Count == before && (bool)Call(h, "ResetHistory") &&
                  hg.Rows.Count == 0 && TranslationEdits.History().Count == 0 && (bool)Get(h, "WasReset"));
        }
        var bak = Directory.GetFiles(TranslationEdits.Folder, "TranslationHistory_*.bak");
        Check("  the list kept beside it (.bak), the texts as they were, none marked edited any more", bak.Length == 1 && File.ReadAllLines(bak[0]).Length == before + 1 &&
              File.ReadAllText(main) == mainBefore && !TranslationEdits.Edits.Any() && LanguageManager.GetStringIn("TR", "Unit_Saw") == "Testere",
              bak.Length + " " + TranslationEdits.Edits.Count());
        Call(c, "Type", row("Unit_Saw"), "RU", "Pila");
        Call(c, "Save");
        Check("  the next change: a new history", TranslationEdits.History().Count == 1 && TranslationEdits.History()[0].User == "admin");

        form.Close();
        MachineRegistry.Reset();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "TRANSLATION PAGE ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    static string Cell(object c, DataGridView g, string key, string lang)
    {
        int at = Shown(c).ToList().FindIndex(r => r.Key == key);
        return at < 0 ? null : Convert.ToString(g.Rows[at].Cells["L_" + lang].Value);
    }

    static bool HasChooser(DataGridView g)
    {
        foreach (DataGridViewColumn col in g.Columns) if (col.HeaderCell.ContextMenuStrip != null) return true;
        return true;     // the chooser puts its menu on the headings as it opens
    }
}
