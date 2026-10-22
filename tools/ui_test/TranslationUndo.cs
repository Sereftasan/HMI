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
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 22 October: "Is it possible to discard changes after the AI translation file import with the 'Ctrl+Z' button on
// top of the screen, the AI results might be too bad to get on with".
class TU {
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
    static object Get(object o, string n) { var t = o as Type ?? o.GetType(); var pr = t.GetProperty(n, Any); return pr != null ? pr.GetValue(o is Type ? null : o, null) : t.GetField(n, Any).GetValue(o is Type ? null : o); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length).Invoke(o, a); }
    static KeyValuePair<string, string> Src(string name, string text) { return new KeyValuePair<string, string>(name, text); }
    static IList Steps(string n) { return (IList)Get(typeof(TranslationsControl), n); }
    static string Last(string n) { var l = Steps(n); return l.Count == 0 ? "-" : (string)Get(l[l.Count - 1], "What") + " " + ((IList)Get(l[l.Count - 1], "Cells")).Count; }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "trun_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var common = Path.Combine(dir, "CommonTranslations.csv");
        var main = Path.Combine(dir, "Translations.csv");
        File.WriteAllLines(common, new[] { "Key;EN;TR;RU", "Btn_Ok;OK;Tamam;OK", "Lbl_Speed;Speed;;Skorost", "Btn_Stop;Stop;Dur;Stop" });
        File.WriteAllLines(main, new[] { "Key;EN;TR;RU", "Unit_Clamp;Clamp;Kiskac;Zazhim", "Ref_Home;Reference;;" });
        TranslationEdits.Folder = Path.Combine(dir, "data");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        TranslationEdits.ProjectFileOf = x => null;
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod { Id = "MAIN" }, null, dir);
        LanguageManager.LoadCommonTranslations(common);
        LanguageManager.LoadMachineTranslations(main);
        TranslationEdits.Apply();
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        string asked = null, answer = "yes";
        Kaban.UI.Profiles.Ask.Hook = (title, prompt, initial) => { asked = prompt; return answer; };
        TranslationReviewForm.Hook = f => true;          // the changes listed: taken as ticked

        var form = new Form { Width = 1300, Height = 700 };
        var c = new TranslationsControl();
        form.Controls.Add(c);
        form.Show(); Application.DoEvents();
        var grid = (DataGridView)Get(c, "Grid");
        var bar = grid.Parent.Controls.OfType<FlowLayoutPanel>().First();
        var undo = bar.Controls.OfType<Button>().FirstOrDefault(b => b.AccessibleName == "Undo");
        var redo = bar.Controls.OfType<Button>().FirstOrDefault(b => b.AccessibleName == "Redo");
        Check("on top: Undo (Ctrl+Z) and Redo (Ctrl+Y), off while there is nothing to undo", undo != null && redo != null && !undo.Enabled && !redo.Enabled &&
              Shortcuts.Of(undo) == (Keys.Control | Keys.Z) && Shortcuts.Of(redo) == (Keys.Control | Keys.Y));
        Func<string, string, TranslationEdits.Row> row = (owner, key) => ((IList<TranslationEdits.Row>)Get(c, "Shown")).First(r => r.Key == key && r.Owner == owner);

        Console.WriteLine("--- the AI's answer imported, then undone ---");
        var ai = string.Join("\r\n", new[] { "### CommonTranslations.csv", "```csv", "Key;EN;TR;RU", "Lbl_Speed;;Hız;", "Btn_Ok;;Tamam!!;", "Btn_Stop;;Dur;", "```",
                                             "### MAIN_Translations.csv", "```csv", "Key;TR;RU", "Ref_Home;Referans;Referens", "```" });
        var commonBefore = File.ReadAllText(common);
        var mainBefore = File.ReadAllText(main);
        Call(c, "ImportAnswer", new List<KeyValuePair<string, string>> { Src(null, ai) });
        Check("the AI's texts written: 3 empty cells, 1 change", File.ReadAllLines(common).Contains("Lbl_Speed;Speed;Hız;Skorost") && File.ReadAllLines(common).Contains("Btn_Ok;OK;Tamam!!;OK") &&
              File.ReadAllLines(main).Contains("Ref_Home;Reference;Referans;Referens"), string.Join(" | ", File.ReadAllLines(common)));
        Check("  one step: \"the AI's translations\", its 4 texts - Undo on, its tip says it", Last("UndoSteps") == "the AI's translations 4" && undo.Enabled,
              Last("UndoSteps"));
        answer = "no";
        Check("Undo asked first - No: nothing undone", !(bool)Call(c, "Undo") && asked.Contains("the AI's translations") && File.ReadAllText(common) == commonBefore.Replace("Lbl_Speed;Speed;;", "Lbl_Speed;Speed;Hız;").Replace("Tamam;", "Tamam!!;") &&
              Steps("UndoSteps").Count == 1, asked);
        answer = "yes";
        Check("  Yes: every text it wrote back as it was - the files as before the import", (bool)Call(c, "Undo") &&
              File.ReadAllLines(common).Contains("Lbl_Speed;Speed;;Skorost") && File.ReadAllLines(common).Contains("Btn_Ok;OK;Tamam;OK") &&
              File.ReadAllLines(main).Contains("Ref_Home;Reference;;") && LanguageManager.GetStringIn("TR", "Btn_Ok") == "Tamam" &&
              row("General", "Lbl_Speed").FileTexts["TR"] == "",
              string.Join(" | ", File.ReadAllLines(common)) + " / " + string.Join(" | ", File.ReadAllLines(main)));
        Check("  the history says it: the undo's changes by \"maint (undo)\"", TranslationEdits.History().Count(h => h.User == "maint (undo)") == 4,
              string.Join(", ", TranslationEdits.History().Select(h => h.User + " " + h.Key + " " + h.Lang)));
        Check("  Undo off, Redo on", !undo.Enabled && redo.Enabled && Last("RedoSteps") == "the AI's translations 4");
        Check("Redo: the AI's texts again", (bool)Call(c, "Redo") && File.ReadAllLines(common).Contains("Btn_Ok;OK;Tamam!!;OK") &&
              File.ReadAllLines(main).Contains("Ref_Home;Reference;Referans;Referens") && undo.Enabled && !redo.Enabled);
        Call(c, "Undo");

        Console.WriteLine("--- a text typed, a Save ---");
        Call(c, "Type", row("General", "Lbl_Speed"), "TR", "Hiz");
        Call(c, "Type", row("General", "Lbl_Speed"), "TR", "Hız");
        Check("typed twice: two steps, the second typed text kept until Save", Last("UndoSteps").StartsWith("the text typed in Lbl_Speed TR") &&
              (int)Get(c, "PendingCount") == 1 && !redo.Enabled, Last("UndoSteps"));
        Call(c, "Undo");
        var pending = (IDictionary)Get(c, "_pending");
        Check("Undo: the first typed text again (not asked: nothing was written)", pending.Values.Cast<string>().SequenceEqual(new[] { "Hiz" }), string.Join(",", pending.Values.Cast<string>()));
        Call(c, "Undo");
        Check("  Undo again: nothing typed - the cell as it is in force", pending.Count == 0 && File.ReadAllLines(common).Contains("Lbl_Speed;Speed;;Skorost"));
        Call(c, "Redo"); Call(c, "Redo");
        Check("Redo twice: typed again", pending.Values.Cast<string>().SequenceEqual(new[] { "Hız" }));
        Call(c, "Save");
        Check("Save: one step for it (the typing in it)", Last("UndoSteps") == "the Save 1" && File.ReadAllLines(common).Contains("Lbl_Speed;Speed;Hız;Skorost"), Last("UndoSteps"));
        Call(c, "Undo");
        Check("  undone: the file as before the Save", File.ReadAllLines(common).Contains("Lbl_Speed;Speed;;Skorost") && pending.Count == 0);

        Console.WriteLine("--- a language copied into the empty cells ---");
        int n = (int)Call(c, "CopyLanguage", "EN", new[] { "TR" }, false);
        Check("EN into TR's empty cells: one step of them all", n == 2 && Last("UndoSteps") == "EN copied into the empty cells of TR 2" && pending.Count == 2, Last("UndoSteps") + " " + n);
        Call(c, "Undo");
        Check("  undone at once", pending.Count == 0);

        form.Close();
        Kaban.UI.Profiles.Ask.Hook = null;
        TranslationReviewForm.Hook = null;
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nTRANSLATION UNDO ALL PASS" : "\nTRANSLATION UNDO " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
