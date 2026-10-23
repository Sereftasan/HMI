using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// 23 October: "most of the control tooltips (apart from the HMI controls) doesn't have translation keys so they are
// shown in English only"
class TL {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { d = d ?? ""; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>The tip a control shows now (as its hover shows it).</summary>
    static string Shown(Control c) {
        var table = typeof(UiStyle).GetField("_ownTips", Any).GetValue(null);
        var args = new object[] { c, null };
        table.GetType().GetMethod("TryGetValue").Invoke(table, args);
        return (string)typeof(UiStyle).GetMethod("ShownTip", Any).Invoke(null, new object[] { c, args[1] });
    }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        var tips = UiStyle.Tips();

        Console.WriteLine("--- the keys in the translation file ---");
        Check("the tooltips the program writes with a key of their own are in the file (they were not: English only, nothing to translate)",
              new[] { "Tip_TrnSave", "Tip_TrnFind", "Tip_MonBackup", "Nts_TipFind", "Tip_TrnFlag", "Tip_PaneMin", "Tip_SimMonitor" }.All(LanguageManager.HasKey));
        Check("  with their English", LanguageManager.GetStringIn("EN", "Tip_TrnFind") == "Where is it used? The pages, the menu, the alarms and the program's lines using the selected row's key",
              LanguageManager.GetStringIn("EN", "Tip_TrnFind"));

        Console.WriteLine("--- a plain control's tooltip ---");
        var add = new Button();
        UiStyle.SetTip(tips, add, LanguageManager.GetString("Tip_MonAdd", "Add a device or a comment line"));
        var copy = new Button();
        Shortcuts.Set(copy, Keys.Control | Keys.C, tips, LanguageManager.GetString("Tip_MonCopy", "Copy the selected rows"));
        var made = new Button();
        UiStyle.SetTip(tips, made, "A text of no key 12345");
        Check("its key found again by its text", UiStyle.TipKeyOf(add) == "Tip_MonAdd" && UiStyle.TipKeyOf(copy) == "Tip_MonCopy" && UiStyle.TipKeyOf(made) == null,
              UiStyle.TipKeyOf(add) + " / " + UiStyle.TipKeyOf(copy));
        Check("  the notes page lists it with the control's texts", NoteTarget.KeysOf(add).Any(k => k.Key == "Tip_MonAdd"));

        string en = Shown(add);
        LanguageManager.ChangeLanguage("TR");
        string tr = LanguageManager.GetStringIn("TR", "Tip_MonAdd");
        Check("the language changed (the page not built again): the tip in the language shown", tr != null && Shown(add) == tr && Shown(add) != en, Shown(add));
        Check("  a key's tip: its key's line in the language, then the text", Shown(copy) == Shortcuts.Text(Keys.Control | Keys.C) + "\n" + LanguageManager.GetStringIn("TR", "Tip_MonCopy"),
              Shown(copy).Replace("\n", " | "));
        Check("  a text of no key: as it is", Shown(made) == "A text of no key 12345");
        LanguageManager.ChangeLanguage("EN");
        Check("  back: English", Shown(add) == en);

        Console.WriteLine(fail == 0 ? "\nTOOLTIP LANGUAGE ALL PASS" : "\nTOOLTIP LANGUAGE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
