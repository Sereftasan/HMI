using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using Kaban.UI.Screens;
using Kaban.UI.Settings;

// 21 October: "a tool to find where the translation line is seen on the screen (or a message box, etc) -
// a button on top (Ctrl+F) which opens the list of where it is used"; "the Notes and feedback page should
// make the rest of the screen darker when Picking is on"; "a link from its translation table to the
// translation page - the item highlighted there, to edit it directly".
class TF {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { d = d ?? ""; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) { var t = o.GetType(); var p = t.GetProperty(n, Any); return p != null ? p.GetValue(o, null) : t.GetField(n, Any).GetValue(o); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length).Invoke(o, a); }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }

    /// <summary>A control with a text key, as the program's controls have them.</summary>
    public class KeyedBox : Panel { public string CaptionKey { get; set; } public string ToolTipTextKey { get; set; } }

    class Mod : IMachineModule {
        public string ModuleId { get { return "TST"; } }
        public string DisplayNameKey { get { return "TST"; } }
        public void RegisterPlc(IMachineMountContext ctx) { ctx.Station(1); }
        public void RegisterConditions(IMachineMountContext ctx) { }
        public void RegisterData(IMachineMountContext ctx) { }
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx)
        {
            yield return new MachineScreen
            {
                TitleKey = "Scr_TestPage", TitleFallback = "Test page", Role = "TestRole",
                CreateView = () =>
                {
                    var page = new UserControl { Name = "page" };
                    var unit = new KeyedBox { Name = "unitA", CaptionKey = "Unit_Clamp", ToolTipTextKey = "Tip_Clamp" };
                    unit.Controls.Add(new Label { Name = "made", Text = "Speed:" });          // made with its text, no key
                    page.Controls.Add(unit);
                    var grid = new DataGridView { Name = "table" };
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c1", HeaderText = "Position", Tag = "Col_Position" });
                    page.Controls.Add(grid);
                    return page;
                }
            };
            yield return new MachineScreen
            {
                TitleKey = "Scr_TestSettings", TitleFallback = "Test settings", Role = "TestRole2",
                CreateView = () => new SettingsScreen(new ISettingsPage[]
                {
                    new SimplePage("Tab_TestOne", "One", SecurityLevel.None, () => new UserControl()),
                    new SimplePage("Tab_TestTwo", "Two", SecurityLevel.None, () => { var u = new UserControl(); u.Controls.Add(new KeyedBox { Name = "deep", CaptionKey = "Unit_Clamp" }); return u; })
                })
            };
        }
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "tf_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        TranslationEdits.Folder = Path.Combine(dir, "data");
        NotesStore.Folder = Path.Combine(dir, "Notes");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        AuditLogger.LogDirectory = dir;
        var common = Path.Combine(dir, "CommonTranslations.csv");
        File.WriteAllLines(common, new[] { "Key;EN;TR", "Lbl_Speed;Speed;Hiz", "Col_Position;Position;Konum", "Msg_TestDone;All done;Bitti", "Unit_Clamp;Clamp (general);Kiskac (genel)" });
        var main = Path.Combine(dir, "Translations.csv");
        File.WriteAllLines(main, new[] { "Key;EN;TR", "Unit_Clamp;Clamp;Kiskac", "Scr_TestPage;Test page;Test sayfasi" });
        LanguageManager.LoadCommonTranslations(common);
        LanguageManager.LoadMachineTranslations(main);
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod(), null, dir);
        AlarmManager.RegisterDefinition("TST.A1", "Msg_TestDone", AlarmSeverity.Warning, 1, "M100");
        // the program's source: a project with a line naming the key in a message box (its build not searched)
        var src = Path.Combine(dir, "src");
        Directory.CreateDirectory(Path.Combine(src, "KabanHMI"));
        File.WriteAllText(Path.Combine(src, "KabanHMI", "KabanHMI.sln"), "");
        Directory.CreateDirectory(Path.Combine(src, "Proj", "Forms"));
        Directory.CreateDirectory(Path.Combine(src, "Proj", "bin", "Debug"));
        File.WriteAllText(Path.Combine(src, "Proj", "Proj.csproj"), "<Project />");
        File.WriteAllLines(Path.Combine(src, "Proj", "Forms", "Main.cs"), new[] {
            "class A {", "  void Done() {", "    MessageBox.Show(this,", "        LanguageManager.GetString(\"Msg_TestDone\", \"All done\"));",
            "    // LanguageManager.GetString(\"Msg_TestDone\") in a comment", "    _status.Text = T(\"Msg_TestDone\", \"All done\");", "  }", "}" });
        File.WriteAllText(Path.Combine(src, "Proj", "bin", "Debug", "Copy.cs"), "var x = \"Msg_TestDone\";");
        TextUses.SourceRoot = () => src;
        SecurityManager.Login("maint", SecurityLevel.Maintenance);

        Console.WriteLine("--- where a key is used ---");
        var uses = TextUses.Find("Unit_Clamp");
        Check("on a page: the control naming it (its caption key), with the way to it", uses.Any(u => u.Kind == "Page" && u.Place == "Test page" && u.What.Contains("Caption") &&
              u.Way != null && u.Way.ControlPath.SequenceEqual(new[] { "unitA" })), string.Join(" | ", uses.Select(u => u.Kind + ":" + u.Place + ":" + u.What)));
        Check("  and on a settings page's tab, two tabs down: the tab's index on the way", uses.Any(u => u.Kind == "Page" && u.Place == "Test settings / Two" && u.Way.PagePath.SequenceEqual(new[] { 1 })));
        uses = TextUses.Find("Lbl_Speed");
        Check("a label made with its text (no key): found by its text", uses.Any(u => u.Kind == "Page" && u.ByText && u.Way.ControlPath.SequenceEqual(new[] { "unitA", "made" })),
              string.Join(" | ", uses.Select(u => u.Kind + ":" + u.What + ":" + u.ByText)));
        uses = TextUses.Find("Col_Position");
        Check("a table's heading by its key", uses.Any(u => u.Kind == "Page" && u.What.Contains("Position") && !u.ByText));
        uses = TextUses.Find("Scr_TestPage");
        Check("a page button of the menu", uses.Any(u => u.Kind == "Menu" && u.Way != null && u.Way.Screen != null && u.Way.ControlPath.Count == 0));
        uses = TextUses.Find("Tab_TestTwo");
        Check("a tab of a page", uses.Any(u => u.Kind == "Tab" && u.Place == "Test settings" && u.Way.PagePath.SequenceEqual(new[] { 1 })));
        uses = TextUses.Find("Msg_TestDone");
        var code = uses.Where(u => u.Kind == "Code").ToList();
        Check("an alarm's text", uses.Any(u => u.Kind == "Alarm" && u.What.StartsWith("TST.A1") && u.Way == null));
        Check("the program's lines: a message box (its call two lines up), a status line - not a comment, not a build's file",
              code.Count == 2 && code[0].Place.Replace('\\', '/') == "Proj/Forms/Main.cs" && code[0].Line == 4 && code[0].What.StartsWith("message box:") &&
              code[1].Line == 6 && code[1].What.StartsWith("status line:"),
              string.Join(" | ", code.Select(u => u.Place + ":" + u.Line + ":" + u.What)));
        TextUses.SourceRoot = () => null;
        Check("  without the source beside the program: no lines (the rest still)", TextUses.Find("Msg_TestDone").All(u => u.Kind != "Code") && TextUses.Find("Msg_TestDone").Count == 1);
        TextUses.SourceRoot = () => src;

        using (var f = new TranslationUsesForm("Msg_TestDone", "All done", TextUses.Find("Msg_TestDone")))
        {
            f.Show(); Application.DoEvents();
            var g = (DataGridView)Get(f, "Grid");
            Check("the list: where, the page or file, what uses it, the line", g.Rows.Count == 3 && Convert.ToString(g.Rows[0].Cells["Kind"].Value) == "Alarm" &&
                  Convert.ToString(g.Rows[1].Cells["Kind"].Value) == "Program" && Convert.ToString(g.Rows[1].Cells["Line"].Value) == "4", g.Rows.Count + "");
            Call(f, "Open", 1);
            Check("  a program line is not opened: said where it is", f.Chosen == null && f.DialogResult != DialogResult.OK);
        }

        Console.WriteLine("--- the Translation page: Ctrl+F, a key shown ---");
        var form = new Form { Width = 1200, Height = 700 };
        var table = new TranslationsControl();
        form.Controls.Add(table);
        form.Show(); Pump(100);
        var find = (Button)Get(table, "_btnFind");
        Check("its button on top, Ctrl+F", find.Parent != null && Shortcuts.Of(find) == (Keys.Control | Keys.F));
        form.Close();


        MachineRegistry.Reset();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "TRANSLATION FIND ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}

static class TFX { public static DataGridView Grid(this TranslationsControl c) { return (DataGridView)typeof(TranslationsControl).GetProperty("Grid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(c, null); } }
