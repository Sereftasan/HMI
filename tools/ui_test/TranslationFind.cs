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
        var index = Path.Combine(dir, "TranslationUses.csv");
        TextUses.IndexFile = () => index;
        Check("  without the source beside the program, nor its list: no lines (the rest still)", TextUses.Find("Msg_TestDone").All(u => u.Kind != "Code") && TextUses.Find("Msg_TestDone").Count == 1);
        // 22 October: "ship a list of the program's lines" (tools\translation_uses.py)
        File.WriteAllLines(index, new[] { "Key;File;Line;Kind;Text", "Msg_TestDone;Proj\\Forms\\Main.cs;6;Status;_status.Text = T(\"Msg_TestDone\", \"All done\");",
                                          "Msg_Other;Proj\\Forms\\Main.cs;9;Text;x" }, new System.Text.UTF8Encoding(true));
        code = TextUses.Find("Msg_TestDone").Where(u => u.Kind == "Code").ToList();
        Check("  the list shipped beside it: the key's lines, their kind in the panel's language, the line as it is (its ';' kept)",
              code.Count == 1 && code[0].Place == "Proj\\Forms\\Main.cs" && code[0].Line == 6 && code[0].What == "status line: _status.Text = T(\"Msg_TestDone\", \"All done\");" &&
              TextUses.LinesFrom() == "list", string.Join(" | ", code.Select(u => u.Place + ":" + u.Line + ":" + u.What)));
        TextUses.SourceRoot = () => src;
        Check("  the source there: read from it", TextUses.LinesFrom() == "source" && TextUses.Find("Msg_TestDone").Count(u => u.Kind == "Code") == 2);

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
        Check("a key shown from elsewhere: every row listed, the machine's row (the one on the screen) marked and chosen in the screen's language",
              table.ShowKey("Unit_Clamp") && (string)Get(table, "Marked") == "TST\u0001Unit_Clamp" &&
              ((IList<TranslationEdits.Row>)Get(table, "Shown"))[table.Grid().CurrentCell.RowIndex].Owner == "TST" &&
              table.Grid().Columns[table.Grid().CurrentCell.ColumnIndex].Name == "L_EN", (string)Get(table, "Marked"));
        Check("  no such key: nothing", !table.ShowKey("No_Such_Key"));
        form.Close();

        Console.WriteLine("--- the notes page: darker while picking, its texts' link ---");
        var shell = new Form { Width = 1300, Height = 800, StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var top = new Panel { Bounds = new Rectangle(0, 0, 1300, 40), Name = "top" };
        var pageArea = new Panel { Bounds = new Rectangle(120, 40, 640, 720), Name = "content" };
        var menu = new Panel { Bounds = new Rectangle(0, 40, 120, 720), Name = "menu" };
        var box = new KeyedBox { Bounds = new Rectangle(20, 20, 200, 80), CaptionKey = "Unit_Clamp", Name = "box" };
        pageArea.Controls.Add(box);
        var side = new Panel { Bounds = new Rectangle(760, 40, 540, 720) };
        shell.Controls.AddRange(new Control[] { top, menu, pageArea, side });
        string opened = null;
        var notes = new NotesPage(c => c != null && (pageArea.Contains(c) || top.Contains(c)), c => "Test page", () => "TEST")
        {
            ShadeAreas = () => new Control[] { top, pageArea },
            OpenTranslationTable = k => { opened = k; return ""; }
        };
        side.Controls.Add(notes);
        shell.Show(); Pump(400);
        var shade = (PickShade)Get(notes, "Shade");
        Check("picking: the page and the caption bar darker - not the menu, not the notes page", notes.Picking && shade.On && shade.Shaded.Count == 2 &&
              shade.Shaded.Contains(pageArea.RectangleToScreen(pageArea.ClientRectangle)) && shade.Shaded.Contains(top.RectangleToScreen(top.ClientRectangle)),
              string.Join(";", shade.Shaded));
        Call(notes, "Pointing", box);
        Check("  the control under the pointer bright in it", shade.HoleRect == Rectangle.Inflate(box.RectangleToScreen(box.ClientRectangle), 5, 5), shade.HoleRect.ToString());
        pageArea.Width = 500; shade.Sync();
        Check("  it follows the page (a side panel narrowing it)", shade.Shaded.Contains(pageArea.RectangleToScreen(pageArea.ClientRectangle)));
        // the window itself: click-through, never active
        PickShade.CanShow = true;
        shade.Sync();
        var window = Application.OpenForms.Cast<Form>().FirstOrDefault(w => w.Name == "notesPickShade");
        int ex = window == null ? 0 : ((CreateParams)typeof(Form).GetProperty("CreateParams", Any).GetValue(window, null)).ExStyle;
        Check("  a see-through window over them that takes no tap (layered, transparent, no activation)", window != null && shade.WindowShown &&
              (ex & 0x80000) != 0 && (ex & 0x20) != 0 && (ex & 0x08000000) != 0 && window.Opacity < 0.5 && !window.ShowInTaskbar, "0x" + ex.ToString("X"));
        notes.Pick(box); Pump(50);
        var link = Tree(notes).OfType<Button>().FirstOrDefault(b => b.Name == "notesOpenTranslation");
        Check("a picked control's texts: the link to the translation table (Maintenance)", link != null && link.Visible, link == null ? "none" : link.Text);
        link.PerformClick(); Pump(50);
        Check("  it opens the key there - picking off, the shade gone", opened == "Unit_Clamp" && !notes.Picking && !shade.On && !shade.WindowShown);
        typeof(NotesPage).GetMethod("SetPicking", Any).Invoke(notes, new object[] { true }); Pump(50);
        Check("picking again: the shade again", shade.On && shade.WindowShown);
        notes.Visible = false; Pump(50);
        Check("  the notes page closed: gone", !shade.On && !shade.WindowShown);
        PickShade.CanShow = false;
        SecurityManager.Login("op", SecurityLevel.Operator);
        notes.Visible = true; Pump(50);
        notes.Pick(box); Pump(50);
        Check("an Operator: no link (the page is for Maintenance and above)", Tree(notes).OfType<Button>().All(b => b.Name != "notesOpenTranslation"));
        shell.Close();

        MachineRegistry.Reset();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "TRANSLATION FIND ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}

static class TFX { public static DataGridView Grid(this TranslationsControl c) { return (DataGridView)typeof(TranslationsControl).GetProperty("Grid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(c, null); } }
