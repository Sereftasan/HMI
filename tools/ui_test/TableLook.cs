using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// EVERY TABLE AS THE PLC MONITOR'S (2 October): the same look and the same menu on
// the headings - the columns, their widths, fit, "Save as the default", "Default
// column settings" (the saved default, else the table's own) and, once a default
// is saved, "Original column settings".
// 6 October: "All the tables should have a thin outside border line in their heading's background colour."
class TL {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static T Field<T>(object o, string n) { return (T)o.GetType().GetField(n, Any).GetValue(o); }
    static List<string> Tags(ColumnChooser c) { return c.MenuItems().Select(i => Convert.ToString(i.Tag)).Where(t => t.Length > 0).ToList(); }
    static string Texts(ColumnChooser c) { return string.Join(" | ", c.MenuItems().OfType<ToolStripMenuItem>().Select(i => i.Text)); }
    static bool Looks(DataGridView g) {
        return g.Font.Name == UiStyle.Body.Name && Math.Abs(g.Font.Size - UiStyle.Body.Size) < 0.01 && g.RowTemplate.Height == 26 &&
               g.ColumnHeadersDefaultCellStyle.BackColor == UiStyle.GridHeadBack && g.ColumnHeadersDefaultCellStyle.Font.Bold &&
               g.DefaultCellStyle.SelectionBackColor == UiStyle.GridSelection && g.GridColor == UiStyle.GridRule &&
               g.CellBorderStyle == DataGridViewCellBorderStyle.SingleHorizontal && g.BackgroundColor == Color.White;
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "tablelook_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ColumnChooser.Folder = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var host = new Form { ClientSize = new Size(1000, 600) };

        Console.WriteLine("--- a table with the table tools ---");
        var g = new DataGridView { Dock = DockStyle.Fill };
        g.Columns.Add("Code", "Code"); g.Columns.Add("Text", "Text"); g.Columns.Add("Time", "Time");
        g.Columns["Code"].Width = 80; g.Columns["Text"].Width = 300; g.Columns["Time"].Width = 150;
        host.Controls.Add(g); host.Show(); Application.DoEvents();
        UiStyle.EnableTableTools(g, "TestTable");
        Check("the PLC Monitor's look", Looks(g));
        // 6 October: the frame of the same day taken back ("the previous one was much better")
        Check("  no frame round it, as before", g.BorderStyle == BorderStyle.None);
        var c = UiStyle.ChooserOf(g);
        Check("the PLC Monitor's menu on its headings", c != null && g.Columns.Cast<DataGridViewColumn>().All(x => x.HeaderCell.ContextMenuStrip == c.Menu));
        Check("  the columns, widths, fit, save as the default, default column settings", Tags(c).SequenceEqual(new[] { "Code", "Text", "Time", "widths", "fit", "savedefault", "default" }),
              string.Join(",", Tags(c)));
        Check("  named as asked", Texts(c).Contains("Save as the default") && Texts(c).Contains("Default column settings") && !Texts(c).Contains("Original"), Texts(c));

        // the user's own arrangement, kept as the default
        g.Columns["Code"].Visible = false; g.Columns["Text"].Width = 420; g.Columns["Time"].DisplayIndex = 0;
        Check("Save as the default", c.SaveDefault() && File.Exists(Path.Combine(dir, "TestTable.default")));
        Check("  then the original is offered too", Tags(c).Contains("original") && Texts(c).Contains("Original column settings"));
        g.Columns["Code"].Visible = true; g.Columns["Text"].Width = 200; g.Columns["Time"].DisplayIndex = 2;
        c.ToDefault();
        // (Mono numbers a hidden column's place unlike .NET: the order is checked on .NET only)
        bool mono = Type.GetType("Mono.Runtime") != null;
        Check("Default column settings: back to the saved one - shown, order and widths", !g.Columns["Code"].Visible && g.Columns["Text"].Width == 420 &&
              (mono || g.Columns["Time"].DisplayIndex == 0), g.Columns["Code"].Visible + " " + g.Columns["Text"].Width + " " + g.Columns["Time"].DisplayIndex);
        c.ToOriginal();
        Check("Original column settings: the table as it was made", g.Columns["Code"].Visible && g.Columns["Text"].Width == 300 && g.Columns["Code"].DisplayIndex == 0 &&
              g.Columns["Time"].DisplayIndex == 2);
        Check("  each kept as the table's layout (Layouts, for this panel)", File.Exists(Path.Combine(dir, "TestTable.cols")));

        Console.WriteLine("--- a table with no default saved: its own ---");
        var g2 = new DataGridView();
        g2.Columns.Add("A", "A"); g2.Columns.Add("B", "B");
        g2.Columns["A"].Width = 70;
        host.Controls.Add(g2);
        UiStyle.EnableTableTools(g2, "TestTable2");
        g2.Columns["A"].Width = 222; g2.Columns["B"].Visible = false;
        UiStyle.ChooserOf(g2).ToDefault();
        Check("Default column settings with none saved: the table's own", g2.Columns["A"].Width == 70 && g2.Columns["B"].Visible);

        Console.WriteLine("--- the program's tables ---");
        var alarmSet = new Kaban.UI.Settings.AlarmSettingsControl("T");
        var aGrid = Field<DataGridView>(alarmSet, "_grid");
        Check("Alarm Settings", Looks(aGrid) && UiStyle.ChooserOf(aGrid) != null && UiStyle.ChooserOf(aGrid).DefaultKey == "AlarmSettings");
        var alarms = new Kaban.UI.Screens.MachineAlarmScreen(null);
        var alGrid = Field<DataGridView>(alarms, "_grid");
        Check("the alarm list", Looks(alGrid) && UiStyle.ChooserOf(alGrid) != null);
        var logs = new Kaban.UI.Settings.LogViewerControl(null, Kaban.UI.Settings.LogViewerControl.Period.Today);
        var lGrid = Field<DataGridView>(logs, "_grid");
        Check("the log", Looks(lGrid) && UiStyle.ChooserOf(lGrid) != null);
        var users = new Kaban.UI.Settings.UsersPreferencesControl();
        var uGrid = Field<DataGridView>(users, "_grid");
        Check("the users", Looks(uGrid) && UiStyle.ChooserOf(uGrid) != null);
        var import = new Kaban.UI.Settings.ImportSettingsControl();
        var iGrid = Field<DataGridView>(import, "_grid");
        Check("Import Settings", Looks(iGrid) && UiStyle.ChooserOf(iGrid) != null);
        var table = new Kaban.UI.Dashboard.DashboardScreen.TableGrid(200, "TestDash");
        table.SetColumns(new Kaban.UI.Dashboard.DashboardScreen.TableGrid.Col("#", 50, "0"), new Kaban.UI.Dashboard.DashboardScreen.TableGrid.Col("Text", 0, null));
        Check("the dashboard's tables", Looks(table.Grid) && UiStyle.ChooserOf(table.Grid) != null && table.Grid.Columns[0].Name == "c0");

        var stack = new Stack<Control>();
        Console.WriteLine("--- buttons with words ---");
        var hl = new Kaban.UI.Settings.HelperLinksForm("x", new string[0]);
        var buttons = new List<Button>();
        stack.Push(hl);
        while (stack.Count > 0) { var x = stack.Pop(); if (x is Button) buttons.Add((Button)x); foreach (Control k in x.Controls) stack.Push(k); }
        Check("the helper links' buttons: rounded, the program's font, their words translated",
              buttons.Count == 7 && buttons.All(b => b.FlatStyle == FlatStyle.Flat && b.Font.Name == UiStyle.Body.Name) &&
              buttons.Any(b => b.Text == "Add files...") && buttons.Any(b => b.Text == "OK"), string.Join(",", buttons.Select(b => b.Text)));
        LanguageManager.ChangeLanguage("TR");
        var hlTr = new Kaban.UI.Settings.HelperLinksForm("x", new string[0]);
        Check("  in Turkish", hlTr.Text.StartsWith("Yard\u0131mc\u0131 ba\u011flant\u0131lar"), hlTr.Text);
        LanguageManager.ChangeLanguage("EN");

        host.Close();
        Console.WriteLine(fail == 0 ? "\nTABLE LOOK ALL PASS" : "\nTABLE LOOK " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
