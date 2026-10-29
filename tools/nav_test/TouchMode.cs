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
using Kaban.UI.Forms;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 29 October: "I want you to add an emoji dropdown button on top of the screen to switch in between mouse
// and touch screen mode and add it to the view preferences (just like the screen scale) and do all of these
// upgrades for a good touch screen experience. The machines usually have 17 or 19 inches of screen with
// 1920x1080 resolution btw." - the plan: press-and-hold off for jog; menus, rows, check boxes and toolbar
// buttons larger; a long press opens the menu, rows get a "⋮", a tap on a greyed entry shows its reason;
// a keyboard on the screen (the number pad for number cells); a finger drag scrolls, a tap selects.
class TM {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static object Get(object o, string n) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, Any); if (f != null) return f.GetValue(o); } return null; }
    static IEnumerable<Control> All(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; } }
    static ToolStripMenuItem Item(ToolStrip m, string name) { return m.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Name == name); }
    static string PT(string method, params object[] a) { return (string)typeof(UsersPreferencesControl).Assembly.GetType("Kaban.UI.Settings.PrefTexts").GetMethod(method, Any).Invoke(null, a); }
    // TOUCH_SHOTS=<folder>: pictures of what is checked (a person looks at them)
    static void Shot(Control c, string name, Rectangle? part = null) {
        var folder = Environment.GetEnvironmentVariable("TOUCH_SHOTS");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        Pump(150);
        var r = part ?? new Rectangle(0, 0, c.Width, c.Height);
        var at = c.PointToScreen(r.Location);
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("python3",
            "-c \"from PIL import ImageGrab; ImageGrab.grab(bbox=(" + at.X + "," + at.Y + "," + (at.X + r.Width) + "," + (at.Y + r.Height) + "), xdisplay=':99').save('" +
            Path.Combine(folder, name + ".png") + "')\"") { UseShellExecute = false });
        p.WaitForExit(10000);
    }
    static void Mode(string v) { ViewPreferences.SetPanel(ViewPreferences.InputMode, v); Pump(50); }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "tm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.ChangeLanguage("EN");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        ViewPreferences.Reload();
        UserStore.CreatorsPath = Path.Combine(dir, "UserCreators.cfg");
        UserStore.ReloadCreators();
        NotesStore.Folder = Path.Combine(dir, "Notes");
        Kaban.UI.Dashboard.MachineLogger.AutoStart = false;
        Kaban.UI.Dashboard.HmiLogStore.AutoStart = false;
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 }) PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st));
        foreach (var n in new[] { "tmop" }) UserStore.RemoveUser(n);
        UserStore.AddOrUpdateUser("tmop", "1", SecurityLevel.Operator); UserStore.SetCreatedBy("tmop", "admin");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        var bounds = new Rectangle(0, 0, 1270, 900);
        form.StartPosition = FormStartPosition.Manual; form.Bounds = bounds;
        form.Load += (s, e) => { form.WindowState = FormWindowState.Normal; form.Bounds = bounds; SecurityManager.Login("admin", SecurityLevel.Admin); };
        form.Show();
        Pump(500);
        TouchInput.Sweep();

        Console.WriteLine("--- the preference: View Preferences > Screen ---");
        var pref = ViewPreferences.Find(ViewPreferences.InputMode);
        Check("Screen used with: a mouse (the default) or touch, in the Screen group beside the language",
              pref != null && pref.Group == "Screen" && !pref.Hidden && string.Join(",", pref.Values) == "Mouse,Touch" && pref.Default == "Mouse" &&
              !ViewPreferences.TouchMode && !TouchInput.On);
        Check("  its words", PT("NameOf", ViewPreferences.InputMode) == "Screen used with" &&
              PT("ValueText", ViewPreferences.InputMode, "Mouse") == "A mouse" && PT("ValueText", ViewPreferences.InputMode, "Touch") == "Touch (a finger)");
        var page = new UsersPreferencesControl();
        var host = new Form { ClientSize = new Size(1200, 800), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        page.Dock = DockStyle.Fill; host.Controls.Add(page); host.Show(); Pump(200);
        page.Fill(); Pump(100);
        var combo = page.ComboOf(ViewPreferences.InputMode);
        var lang = page.ComboOf(ViewPreferences.Language);
        Check("  on the page: a row under Screen, below the language", combo != null && lang != null && combo.Top > lang.Top &&
              combo.Items.Cast<object>().Select(o => o.ToString()).Where(s => s == "A mouse" || s == "Touch (a finger)").Count() == 2,
              combo == null ? "no combo" : combo.Top + " vs " + lang.Top + ": " + string.Join(" | ", combo.Items.Cast<object>()));
        var preview = new PreferencePreview();
        preview.ShowPreference(ViewPreferences.InputMode, k => k == ViewPreferences.InputMode ? "Touch" : ViewPreferences.PanelValue(k), false);
        Check("  its preview says what touch changes", preview.Description.Contains("long press") && preview.Description.Contains("keyboard"), preview.Description);
        host.Close(); host.Dispose();

        Console.WriteLine("--- the button on the caption bar ---");
        var input = (Button)Get(form, "_btnInput");
        var zoom = (Button)Get(form, "_btnZoom");
        Check("beside the screen scale, on its left", input != null && input.Visible && input.Right < zoom.Left && Math.Abs(input.Top + input.Height / 2 - (zoom.Top + zoom.Height / 2)) <= 1,
              input == null ? "none" : input.Bounds + " / " + zoom.Bounds);
        Shot(form, "caption_mouse", new Rectangle(input.Left - 200, 0, form.Width - input.Left + 200, 32));
        Check("  its tooltip: the way in force, and how to switch", (UiStyle.TipOf(input) ?? "").StartsWith("Used with a mouse") &&
              (UiStyle.TipOf(input) ?? "").Contains("switch"), UiStyle.TipOf(input));
        var menu = form.BuildInputMenu();
        Check("its menu: a mouse ticked, touch - each with its picture; whose choice it is", Item(menu, "inputMouse") != null && Item(menu, "inputMouse").Checked &&
              !Item(menu, "inputTouch").Checked && Item(menu, "inputMouse").Image != null && Item(menu, "inputTouch").Image != null &&
              menu.Items.OfType<ToolStripLabel>().Any(l => l.Name == "inputWhose" && l.Text == "Kept for everyone on this panel"),
              string.Join(" | ", menu.Items.Cast<ToolStripItem>().Select(i => i.Text)));
        int changes = 0;
        EventHandler counted = (s, e) => changes++;
        TouchInput.ModeChanged += counted;
        Item(menu, "inputTouch").PerformClick();
        Pump(100);
        Check("touch chosen: nobody keeps their own - the panel's, at once (no restart), the mode changed once",
              ViewPreferences.PanelChosen(ViewPreferences.InputMode) == "Touch" && TouchInput.On && changes == 1 &&
              (UiStyle.TipOf(input) ?? "").StartsWith("Used by touch"), changes + " changes, tip " + UiStyle.TipOf(input));
        Check("  written in the log", Directory.GetFiles(dir, "HmiAudit_*.txt").Any(f => File.ReadAllText(f).Contains("the screen used with touch (the panel's)")));
        Shot(form, "caption_touch", new Rectangle(input.Left - 200, 0, form.Width - input.Left + 200, 32));
        menu = form.BuildInputMenu();
        Check("  the menu ticks touch now", Item(menu, "inputTouch").Checked && !Item(menu, "inputMouse").Checked);

        Console.WriteLine("--- Windows' press-and-hold and pan: off in touch mode ---");
        var someButton = All(form).OfType<ButtonHMI>().FirstOrDefault(b => b.IsHandleCreated);
        Check("every window of the program prepared for a finger (press-and-hold not a right-click, its own pan off)",
              TouchInput.IsPreparedForTouch(form) && someButton != null && TouchInput.IsPreparedForTouch(someButton) && TouchInput.IsPreparedForTouch(input));
        var later = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(100, 100), Size = new Size(300, 200) };
        var laterBox = new TextBox { Location = new Point(10, 10) };
        later.Controls.Add(laterBox);
        later.Show(); Pump(50); TouchInput.Sweep();
        var added = new Button { Location = new Point(10, 60) };
        later.Controls.Add(added); Pump(50);
        Check("  a window opened later, a control added later", TouchInput.IsPreparedForTouch(later) && TouchInput.IsPreparedForTouch(laterBox) && TouchInput.IsPreparedForTouch(added));

        Console.WriteLine("--- a user's own ---");
        SecurityManager.Login("tmop", SecurityLevel.Operator); Pump(50);
        ViewPreferences.SetUser("tmop", ViewPreferences.InputMode, "Mouse"); Pump(50);
        Check("tmop keeps a mouse of their own: signed in, their screen is used with a mouse", !TouchInput.On && !TouchInput.IsPreparedForTouch(form));
        menu = form.BuildInputMenu();
        Check("  the menu says it is tmop's own", menu.Items.OfType<ToolStripLabel>().Any(l => l.Name == "inputWhose" && l.Text.StartsWith("Kept as tmop's own")));
        Item(menu, "inputTouch").PerformClick(); Pump(50);
        Check("  touch chosen on the bar: tmop's own changed (the panel's stays touch)", ViewPreferences.UserValue("tmop", ViewPreferences.InputMode) == "Touch" &&
              ViewPreferences.PanelChosen(ViewPreferences.InputMode) == "Touch" && TouchInput.On);
        Item(form.BuildInputMenu(), "inputMouse").PerformClick(); Pump(50);
        Check("  and back: tmop's own a mouse, the panel's touch", ViewPreferences.UserValue("tmop", ViewPreferences.InputMode) == "Mouse" &&
              ViewPreferences.PanelChosen(ViewPreferences.InputMode) == "Touch" && !TouchInput.On);
        SecurityManager.Logout(); Pump(50);
        Check("signed out: the panel's - touch", TouchInput.On && TouchInput.IsPreparedForTouch(form));
        SecurityManager.Login("admin", SecurityLevel.Admin); Pump(50);

        TouchInput.ModeChanged -= counted;
        later.Close(); later.Dispose();
        Mode("Mouse");
        Check("a mouse again: the windows as Windows has them", !TouchInput.IsPreparedForTouch(form) && !TouchInput.IsPreparedForTouch(someButton));

        Console.WriteLine("--- a finger's size: menus, rows, boxes, buttons, tabs ---");
        var sz = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(40, 40), ClientSize = new Size(900, 600), Text = "sizes" };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false };
        var tb1 = UiStyle.ToolButton("\u2795", "Add", null);
        var tb2 = UiStyle.ToolButton("\U0001F5D1", "Delete", null);
        bar.Controls.Add(tb1); bar.Controls.Add(tb2);
        var free = new Panel { Location = new Point(0, 50), Size = new Size(400, 200) };
        var tight = UiStyle.ToolButton("\u270F", "Edit", null); tight.Location = new Point(10, 10);
        var under = new TextBox { Location = new Point(10, 46), Width = 200 };
        var roomy = UiStyle.TextButton("Apply"); roomy.Location = new Point(220, 10);
        free.Controls.Add(tight); free.Controls.Add(under); free.Controls.Add(roomy);
        var box = new CheckBox { Text = "Keep the log", AutoSize = true, Location = new Point(420, 60), Checked = true };
        var radio = new RadioButton { Text = "Every day", AutoSize = true, Location = new Point(420, 100), Checked = true };
        var hmiBox = new CheckBoxHMI { Location = new Point(420, 140) };
        var grid = new DataGridView { Location = new Point(0, 260), Size = new Size(500, 200), AllowUserToAddRows = false, RowHeadersVisible = false };
        UiStyle.GridLook(grid);
        grid.Columns.Add("a", "A"); grid.Columns.Add("b", "B");
        for (int i = 0; i < 12; i++) grid.Rows.Add("row " + i, i);
        var tabs = new TabControl { Location = new Point(520, 260), Size = new Size(360, 200) };
        tabs.TabPages.Add("One"); tabs.TabPages.Add("Two");
        sz.Controls.Add(free); sz.Controls.Add(box); sz.Controls.Add(radio); sz.Controls.Add(hmiBox); sz.Controls.Add(grid); sz.Controls.Add(tabs); sz.Controls.Add(bar);
        sz.Show(); Pump(200); TouchInput.Sweep(); Pump(100);
        int tbH = tb1.Height, tightH = tight.Height, roomyH = roomy.Height, barH = bar.Height, rowH = grid.Rows[0].Height, tabPadY = tabs.Padding.Y;
        int boxW = box.Width;
        var cm = UiStyle.Menu();
        cm.Items.Add(new ToolStripMenuItem("Copy") { Name = "cmCopy" });
        cm.Items.Add(new ToolStripMenuItem("Paste") { Name = "cmPaste", Enabled = false });
        var sub = new ToolStripMenuItem("More") { Name = "cmMore" };
        sub.DropDownItems.Add(new ToolStripMenuItem("Deeper") { Name = "cmDeeper" });
        cm.Items.Add(sub);
        UiStyle.ShowPopup(cm, sz, new Point(600, 60)); Pump(100);
        int entryMouse = TouchInput.EntryHeight(Item(cm, "cmCopy"));
        cm.Close(); Pump(50);
        Check("in mouse mode: everything as it was (menu entry " + entryMouse + " px, row " + rowH + ", button " + tbH + ")",
              entryMouse < 30 && rowH == 26 && tbH == 32,
              "tab padding " + tabPadY);
        Mode("Touch"); Pump(100);
        Shot(sz, "sizes_touch");
        UiStyle.ShowPopup(cm, sz, new Point(600, 60)); Pump(100);
        Check("touch: a menu's entries a finger's height - greyed ones too", TouchInput.EntryHeight(Item(cm, "cmCopy")) >= TouchInput.MenuEntryHeight &&
              TouchInput.EntryHeight(Item(cm, "cmPaste")) >= TouchInput.MenuEntryHeight,
              TouchInput.EntryHeight(Item(cm, "cmCopy")) + " / " + TouchInput.EntryHeight(Item(cm, "cmPaste")));
        sub.ShowDropDown(); Pump(100);
        Check("  a submenu's too, its first time", TouchInput.EntryHeight(sub.DropDownItems[0]) >= TouchInput.MenuEntryHeight, TouchInput.EntryHeight(sub.DropDownItems[0]) + "");
        Shot(sz, "menu_touch", null);
        cm.Close(); Pump(50);
        Check("  a table's rows 40 px (Normal), the open table at once", grid.Rows[0].Height == 40 && grid.RowTemplate.Height == 40 &&
              ViewPreferences.TableRowHeight == 40 && ViewPreferences.RowHeightOf("Compact", true) == 34 && ViewPreferences.RowHeightOf("Large", true) == 46,
              grid.Rows[0].Height + "");
        Check("  the toolbar's buttons 40 px high, the bar grown to hold them", tb1.Height == 40 && tb2.Height == 40 && bar.Height >= 40 + tb1.Margin.Vertical,
              tb1.Height + " / bar " + bar.Height);
        Check("  a button that would cover the box under it keeps its height; one with room grows", tight.Height == tightH && roomy.Height == 40,
              tight.Height + " / " + roomy.Height);
        Check("  a tick box's and a round option's mark larger: the text moved over", box.Width >= boxW + TouchInput.BoxSize - 13 && box.Padding.Left > 0 &&
              radio.Padding.Left > 0 && hmiBox.Padding.Left == 0, boxW + " -> " + box.Width);
        Check("  the tabs taller", tabs.Padding.Y == tabPadY + TouchInput.TabExtra, tabs.Padding + "");
        var lateBox = new CheckBox { Text = "Later", AutoSize = true, Location = new Point(420, 200) };
        sz.Controls.Add(lateBox); Pump(50);
        Check("  a box added in touch mode: larger at once", lateBox.Padding.Left > 0);
        Mode("Mouse"); Pump(100);
        UiStyle.ShowPopup(cm, sz, new Point(600, 60)); Pump(100);
        Check("a mouse again: all back - menu, rows, buttons, bar, boxes, tabs", TouchInput.EntryHeight(Item(cm, "cmCopy")) == entryMouse && grid.Rows[0].Height == rowH &&
              tb1.Height == tbH && roomy.Height == roomyH && bar.Height == barH && box.Width == boxW && box.Padding.Left == 0 && lateBox.Padding.Left == 0 &&
              tabs.Padding.Y == tabPadY,
              TouchInput.EntryHeight(Item(cm, "cmCopy")) + " " + grid.Rows[0].Height + " " + tb1.Height + " " + roomy.Height + " " + bar.Height + " " + box.Width + " " + tabs.Padding.Y);
        cm.Close();
        sz.Close(); sz.Dispose();

        Console.WriteLine(fail == 0 ? "TOUCH MODE ALL PASS" : "TOUCH MODE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
