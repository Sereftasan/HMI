using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 6 October: "We should wire the application buttons with keyboard shortcuts (e.g. Delete
// buttons should be the Delete key) and add them tooltips showing their shortcut on the top
// line and the function on the second."
class KS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static bool KeyDown(Form f, Keys k) {
        var e = new KeyEventArgs(k);
        typeof(Control).GetMethod("OnKeyDown", Any).Invoke(f, new object[] { e });
        return e.Handled;
    }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine("--- the usual keys ---");
        Check("the bin: Delete; the floppy: Ctrl+S; +: Insert", Shortcuts.Usual("\uD83D\uDDD1") == Keys.Delete &&
              Shortcuts.Usual("\uD83D\uDCBE") == (Keys.Control | Keys.S) && Shortcuts.Usual("\u2795") == Keys.Insert);
        Check("  export Ctrl+E, import Ctrl+I, folder Ctrl+O, pencil F2, read again F5, clear Esc",
              Shortcuts.Usual("\uD83D\uDCE4") == (Keys.Control | Keys.E) && Shortcuts.Usual("\uD83D\uDCE5") == (Keys.Control | Keys.I) &&
              Shortcuts.Usual("\uD83D\uDCC2") == (Keys.Control | Keys.O) && Shortcuts.Usual("\u270F\uFE0F") == Keys.F2 &&
              Shortcuts.Usual("\u21BA") == Keys.F5 && Shortcuts.Usual("\u2716") == Keys.Escape);
        Check("  none for the alarm reset, acknowledging, the magnifier (find here, zoom there)",
              Shortcuts.Usual("\uD83D\uDD04") == Keys.None && Shortcuts.Usual("\u2714") == Keys.None && Shortcuts.Usual("\uD83D\uDD0D") == Keys.None);

        Console.WriteLine("--- the tooltip: the key first, then what it does ---");
        var tips = UiStyle.Tips();
        var bin = UiStyle.ToolButton("\uD83D\uDDD1", "Delete the selected user", tips);
        Check("a bin button: \"Del\" on the first line", tips.GetToolTip(bin) == "Del\nDelete the selected user" && Shortcuts.Of(bin) == Keys.Delete,
              tips.GetToolTip(bin).Replace("\n", " | "));
        var save = UiStyle.ToolButton("\uD83D\uDCBE", "Save the settings", tips);
        Check("  the floppy: Ctrl+S", tips.GetToolTip(save) == "Ctrl+S\nSave the settings");
        var plain = UiStyle.ToolButton("\u2699", "Make the program", tips);
        Check("  a button without a usual key: as before", tips.GetToolTip(plain) == "Make the program" && Shortcuts.Of(plain) == Keys.None);
        Check("  the arrows as on the keys", Shortcuts.Text(Keys.Alt | Keys.Left) == "Alt+\u2190" && Shortcuts.Text(Keys.Control | Keys.Shift | Keys.C) == "Ctrl+Shift+C");
        LanguageManager.ChangeLanguage("DE");
        Check("  in German as German keyboards say: Strg+S, Entf", Shortcuts.Text(Keys.Control | Keys.S) == "Strg+S" && Shortcuts.Text(Keys.Delete) == "Entf",
              Shortcuts.Text(Keys.Control | Keys.S) + " " + Shortcuts.Text(Keys.Delete));
        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine("--- pressed from the keyboard ---");
        var form = new Form { ClientSize = new Size(800, 400), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        Shortcuts.Attach(form);
        var pageA = new Panel { Bounds = new Rectangle(0, 0, 400, 400) };
        var pageB = new Panel { Bounds = new Rectangle(400, 0, 400, 400) };
        form.Controls.Add(pageA); form.Controls.Add(pageB);
        var neutral = new Button { Bounds = new Rectangle(760, 380, 30, 18) };     // on the window, on neither page
        form.Controls.Add(neutral); neutral.BringToFront();
        int deleted = 0, savedA = 0, savedB = 0, offPress = 0;
        var del = UiStyle.ToolButton("\uD83D\uDDD1", "Delete", tips, (s, e) => deleted++);
        var saveA = UiStyle.ToolButton("\uD83D\uDCBE", "Save A", tips, (s, e) => savedA++);
        var saveB = UiStyle.ToolButton("\uD83D\uDCBE", "Save B", tips, (s, e) => savedB++);
        var box = new TextBox { Location = new Point(10, 60), Width = 200 };
        var gridA = new DataGridView { Bounds = new Rectangle(10, 100, 300, 120), ReadOnly = true, AllowUserToAddRows = false };
        gridA.Columns.Add("a", "A"); gridA.Rows.Add("x");
        del.Location = new Point(10, 10); saveA.Location = new Point(60, 10); saveB.Location = new Point(10, 10);
        pageA.Controls.Add(del); pageA.Controls.Add(saveA); pageA.Controls.Add(box); pageA.Controls.Add(gridA);
        var otherBox = new TextBox { Location = new Point(10, 60), Width = 200 };
        pageB.Controls.Add(saveB); pageB.Controls.Add(otherBox);
        form.Show(); Pump(150);

        gridA.Focus(); Pump(30);
        Check("Delete with the table focused: the bin pressed", KeyDown(form, Keys.Delete) && deleted == 1, deleted.ToString());
        box.Focus(); Pump(30);
        Check("  typing in a box: Delete is the box's", !KeyDown(form, Keys.Delete) && deleted == 1);
        Check("Ctrl+S from the box: its page's floppy (not the other page's)", KeyDown(form, Keys.Control | Keys.S) && savedA == 1 && savedB == 0);
        otherBox.Focus(); Pump(30);
        Check("  from the other page: that page's", KeyDown(form, Keys.Control | Keys.S) && savedB == 1 && savedA == 1);
        neutral.Focus(); Pump(30);
        Check("  focus on neither: two equally near - nothing pressed (no guess)", !KeyDown(form, Keys.Control | Keys.S) && savedA == 1 && savedB == 1);
        gridA.Focus(); Pump(30);
        del.Enabled = false;
        Check("a disabled button is not pressed", !KeyDown(form, Keys.Delete) && deleted == 1);
        del.Enabled = true;
        pageA.Visible = false;
        Check("  nor one out of sight", !KeyDown(form, Keys.Delete) && deleted == 1);
        pageA.Visible = true; Pump(30);
        var shown = new Button { Location = new Point(200, 10) };
        shown.Click += (s, e) => offPress++;
        pageA.Controls.Add(shown);
        Shortcuts.Set(shown, Keys.Control | Keys.F, tips, "Find", dispatch: false);
        gridA.Focus(); Pump(30);
        Check("a key the window handles itself: shown, not pressed here", !KeyDown(form, Keys.Control | Keys.F) && offPress == 0 &&
              tips.GetToolTip(shown) == "Ctrl+F\nFind");
        var editable = new DataGridView { Bounds = new Rectangle(10, 230, 300, 120), AllowUserToDeleteRows = true, AllowUserToAddRows = false };
        editable.Columns.Add("b", "B"); editable.Rows.Add("y");
        pageA.Controls.Add(editable);
        editable.Focus(); Pump(30);
        Check("Delete in a table whose rows the user deletes: the table's", !KeyDown(form, Keys.Delete) && deleted == 1);

        Console.WriteLine("--- on the program's pages ---");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var users = new UsersPreferencesControl();
        Check("Users and Preferences: the bin Del, the floppy Ctrl+S, + Insert, the curl F5",
              tips.GetToolTip(users.DeleteButton).StartsWith("Del\n") && tips.GetToolTip(users.SaveButton).StartsWith("Ctrl+S\n") &&
              tips.GetToolTip(users.NewButton).StartsWith("Insert\n"), tips.GetToolTip(users.DeleteButton).Replace("\n", " | "));
        users.Dispose();
        var monitor = new Kaban.UI.Forms.SimulationMonitorForm();
        var monTips = (ToolTip)typeof(Kaban.UI.Forms.SimulationMonitorForm).GetField("_tips", Any).GetValue(monitor);
        Func<string, Control> monByTip = start => Tree(monitor).FirstOrDefault(c => c is Button && (monTips.GetToolTip(c) ?? "").Contains(start));
        var copy = monByTip("Copy the selected rows");
        Check("the PLC Monitor: Ctrl+C on the first line, the text without it", copy != null && monTips.GetToolTip(copy) == "Ctrl+C\nCopy the selected rows",
              copy == null ? "none" : monTips.GetToolTip(copy).Replace("\n", " | "));
        var monDel = monByTip("Delete: the selected rows");
        Check("  its bin: Del (pressed by the monitor itself, in its table)", monDel != null && monTips.GetToolTip(monDel).StartsWith("Del\n"));
        monitor.Dispose();

        Console.WriteLine("--- translations ---");
        Func<string, bool> has = k => LanguageManager.GetString(k, "##") != "##";
        var keys = new[] { "Key_Ctrl", "Key_Shift", "Key_Alt", "Key_Delete", "Key_Insert", "Key_Enter", "Key_Space", "Tip_NavBack", "Tip_NavForward" };
        Check("the keys' names and the new tips", keys.All(has), string.Join(",", keys.Where(k => !has(k))));
        Check("  the monitor's tips without the keys in them", !LanguageManager.GetString("Tip_MonCopy", "").Contains("Ctrl"));

        SecurityManager.Logout();
        Console.WriteLine(fail == 0 ? "\nKEY SHORTCUTS ALL PASS" : "\nKEY SHORTCUTS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    static System.Collections.Generic.IEnumerable<Control> Tree(Control c) {
        foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; }
        var strip = c as ToolStrip;
        if (strip != null)
            foreach (var host in strip.Items.OfType<ToolStripControlHost>()) { yield return host.Control; foreach (var d in Tree(host.Control)) yield return d; }
    }
}
