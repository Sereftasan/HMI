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
    // A finger's messages, as Windows makes them of a touch: the filter first, then the control (unless taken).
    static readonly MethodInfo WndProcOf = typeof(Control).GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic);
    static IntPtr LP(Point p) { return (IntPtr)(int)(((p.Y & 0xFFFF) << 16) | (p.X & 0xFFFF)); }
    static bool Finger(Control c, int msg, Point client, int keys) {
        var m = Message.Create(c.Handle, msg, (IntPtr)keys, LP(client));
        bool taken = TouchInput.Handle(ref m);
        if (!taken) WndProcOf.Invoke(c, new object[] { m });
        return taken;
    }
    static bool Down(Control c, Point p) { return Finger(c, 0x201, p, 1); }
    static bool MoveTo(Control c, Point p) { return Finger(c, 0x200, p, 1); }
    static bool Up(Control c, Point p) { return Finger(c, 0x202, p, 0); }
    static bool Tap(Control c, Point p) { bool a = Down(c, p); bool b = Up(c, p); Pump(60); return a || b; }
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
        HmiFeatures.SettingsPath = Path.Combine(dir, "Features.cfg");
        HmiFeatures.Reload();
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

        // 30 October: "We need to make the Industry 4 service and touchscreen mode optional, and we need to add
        // some buttons to active and passive them on HMI setting" - both off until an Admin switches them on.
        Console.WriteLine("--- the touch screen switched off (HMI Settings > Options): a mouse ---");
        var inputOff = (Button)Get(form, "_btnInput");
        Check("off at first: no mouse / touch button on the top bar, a mouse in force", !HmiFeatures.TouchOn && !inputOff.Visible && !TouchInput.On);
        ViewPreferences.SetPanel(ViewPreferences.InputMode, "Touch"); Pump(50);
        Check("  the panel's preference touch: still a mouse while the feature is off", !ViewPreferences.TouchMode && !TouchInput.On && !TouchInput.IsPreparedForTouch(form));
        var offPage = new UsersPreferencesControl();
        var ohost = new Form { ClientSize = new Size(1000, 760), StartPosition = FormStartPosition.Manual, Location = new Point(40, 40) };
        offPage.Dock = DockStyle.Fill; ohost.Controls.Add(offPage); ohost.Show(); Pump(150);
        Func<List<Control>> prefFields = () => All(offPage).Where(c => c.Name.StartsWith("vp") && c.Visible).ToList();
        int fieldsOff = prefFields().Count;
        Check("  Users and Preferences: no 'Screen used with' row", offPage.ComboOf(ViewPreferences.InputMode) == null &&
              !prefFields().Any(c => c.Name == "vp" + ViewPreferences.InputMode));
        ViewPreferences.SetPanel(ViewPreferences.InputMode, null); Pump(50);
        var hmiTabs = (SettingsScreen)new HmiSettingsPage().GetControl();
        // (30 October: "rename 'Features' page name in 'HMI setting' to 'Options' to match to the one on machine settings
        // (and less translation)")
        Check("HMI Settings has an Options tab, last, for an Admin - the machine's Options tab's key and words",
              hmiTabs.AllPages.Last().TitleFallback == "Options" && hmiTabs.AllPages.Last().TitleKey == "Tab_Options" &&
              hmiTabs.AllPages.Last().RequiredLevel == SecurityLevel.Admin && LanguageManager.GetString("Tab_Options", "") == "Options",
              string.Join(",", hmiTabs.AllPages.Select(x => x.TitleFallback)));
        LanguageManager.ChangeLanguage("TR");
        Check("  in Turkish: Seçenekler, as the machine's", LanguageManager.GetString("Tab_Options", "") == "Seçenekler");
        LanguageManager.ChangeLanguage("EN");
        var features = new FeaturesControl();
        var fhost = new Form { ClientSize = new Size(900, 700), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        features.Dock = DockStyle.Fill; fhost.Controls.Add(features); fhost.Show(); Pump(150);
        Shot(fhost, "features_off");
        // (30 October: "redesign the items (selection toggles) as the attached image as well and remove the description
        // notes for the items (we should put them into the manuals instead, they look too much on the screen)")
        Check("  its two features, each a row: the name, what it does now in grey under it, its switch Off at the right end",
              !features.SwitchOf(HmiFeatures.Industry4Logging).Checked && !features.SwitchOf(HmiFeatures.TouchScreen).Checked &&
              features.NowText(HmiFeatures.TouchScreen) == "Now: used with a mouse." && features.NowText(HmiFeatures.Industry4Logging).Contains("Industry 4 service logs"),
              features.NowText(HmiFeatures.Industry4Logging));
        var touchRow = features.RowOf(HmiFeatures.TouchScreen); var logRow = features.RowOf(HmiFeatures.Industry4Logging);
        var touchSw = features.SwitchOf(HmiFeatures.TouchScreen);
        Check("  the rows one under the other, the switches at the same right end, in the middle of their rows",
              touchRow.Top == logRow.Bottom && touchRow.Left == logRow.Left && touchSw.Right == touchRow.Width - 4 &&
              touchSw.Left == features.SwitchOf(HmiFeatures.Industry4Logging).Left && Math.Abs(touchSw.Top + touchSw.Height / 2 - touchRow.Height / 2) <= 1);
        Check("  no description notes: a row's name and its line under it only",
              touchRow.Controls.OfType<Label>().Count() == 2 && logRow.Controls.OfType<Label>().Count() == 2 &&
              !features.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).OfType<Label>().Any() &&
              touchRow.Controls.OfType<Label>().First(l => l.Name == "title").Text == "Touch screen");
        // a click on the row's name turns it over, as on its switch
        var onTitle = touchRow.Controls.OfType<Label>().First(l => l.Name == "title");
        typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(onTitle, new object[] { EventArgs.Empty }); Pump(100);
        Check("touch screen On (a click on its row): kept, the button on the top bar at once (a mouse until someone chooses touch)",
              HmiFeatures.TouchOn && inputOff.Visible && !TouchInput.On && touchSw.Checked &&
              features.NowText(HmiFeatures.TouchScreen).StartsWith("Now: used with a mouse - the button"), features.NowText(HmiFeatures.TouchScreen));
        Check("  written in the log", Directory.GetFiles(dir, "HmiAudit_*.txt").Any(f => File.ReadAllText(f).Contains("feature TouchScreen switched on")));
        // (30 October: "'Screen used with' selection needs restart to appear, it should be activated by just the option selection")
        Pump(150);
        var usedWith = offPage.ComboOf(ViewPreferences.InputMode);
        var fieldsOn = prefFields();
        Check("  Users and Preferences, open meanwhile: 'Screen used with' there at once - no restart - in a row of its own",
              usedWith != null && usedWith.Visible && fieldsOn.Count == fieldsOff + 1 &&
              !fieldsOn.Any(c => c != usedWith && c.Bounds.IntersectsWith(usedWith.Bounds)), fieldsOff + " -> " + fieldsOn.Count);
        ohost.Close(); ohost.Dispose();
        Shot(fhost, "features_touch_on");
        fhost.Close(); fhost.Dispose();
        HmiFeatures.Reload();
        Check("  kept in Features.cfg", HmiFeatures.TouchOn && File.ReadAllText(Path.Combine(dir, "Features.cfg")).Contains("TouchScreen=On"));

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

        Console.WriteLine("--- a finger held, a tap on something greyed, a closed list, the row's ⋮ ---");
        Mode("Touch");
        var gf = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(30, 30), ClientSize = new Size(900, 640), Text = "gestures" };
        var rowGrid = new DataGridView { Location = new Point(10, 10), Size = new Size(520, 300), AllowUserToAddRows = false, RowHeadersVisible = false,
                                         SelectionMode = DataGridViewSelectionMode.FullRowSelect, ReadOnly = true };
        UiStyle.GridLook(rowGrid);
        rowGrid.Columns.Add("name", "Name"); rowGrid.Columns.Add("value", "Value");
        rowGrid.Columns[0].Width = 200; rowGrid.Columns[1].Width = 280;
        for (int i = 0; i < 20; i++) rowGrid.Rows.Add("D" + (100 + i), i * 10);
        var rowMenu = new ContextMenuStrip(); rowMenu.Items.Add(new ToolStripMenuItem("Copy") { Name = "rmCopy" });
        rowGrid.ContextMenuStrip = rowMenu;
        int rightRow = -1;
        rowGrid.CellMouseDown += (s, e) => { if (e.Button == MouseButtons.Right && e.RowIndex >= 0) { rightRow = e.RowIndex; rowGrid.CurrentCell = rowGrid.Rows[e.RowIndex].Cells[0]; } };
        var plain = new Button { Text = "Plain", Location = new Point(560, 10), Size = new Size(120, 44) };
        int plainClicks = 0; plain.Click += (s, e) => plainClicks++;
        var jog = new ButtonHMI { Location = new Point(560, 70), Size = new Size(120, 44) };
        jog.BitActions.Add(new Kaban.Core.Models.BitAction());
        var own = new Panel { Location = new Point(560, 130), Size = new Size(120, 44), BackColor = Color.LightGray };
        int ownRight = 0; own.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) ownRight++; };
        TouchInput.HasMenu(own);
        var holder = new Panel { Location = new Point(560, 190), Size = new Size(300, 60) };
        var off = new Button { Text = "Off", Location = new Point(10, 8), Size = new Size(120, 44), Enabled = false };
        holder.Controls.Add(off);
        var gtips = UiStyle.CreateTips();
        UiStyle.SetTip(gtips, off, "Only while the machine stands still");
        var soft = new ButtonHMI { Location = new Point(560, 260), Size = new Size(120, 44) };
        soft.BitActions.Add(new Kaban.Core.Models.BitAction());
        var list = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(560, 320), Width = 200 };
        list.Items.AddRange(new object[] { "Every day", "Every week", "Every month" }); list.SelectedIndex = 0;
        int committed = 0; list.SelectionChangeCommitted += (s, e) => committed++;
        gf.Controls.Add(rowGrid); gf.Controls.Add(plain); gf.Controls.Add(jog); gf.Controls.Add(own); gf.Controls.Add(holder); gf.Controls.Add(soft); gf.Controls.Add(list);
        gf.Show(); Pump(200); TouchInput.Sweep(); Pump(100);

        var cellAt = rowGrid.GetCellDisplayRectangle(0, 3, false);
        var press = new Point(cellAt.Left + 30, cellAt.Top + cellAt.Height / 2);
        bool tookDown = Down(rowGrid, press);
        Pump(TouchInput.LongPressMs + 250);
        Check("held still on a table row with a menu: the menu opens - the row chosen as a right-click chooses it", tookDown && TouchInput.LastHeld == rowGrid &&
              rightRow == 3 && rowMenu.Visible, "held " + (TouchInput.LastHeld == null ? "-" : TouchInput.LastHeld.GetType().Name) + ", row " + rightRow + ", menu " + rowMenu.Visible);
        Check("  the finger lifted: taken - nothing chosen in the menu, nothing clicked", MoveTo(rowGrid, press) && Up(rowGrid, press));
        rowMenu.Close(); Pump(50);
        Down(plain, new Point(20, 20)); Pump(TouchInput.LongPressMs + 200);
        bool plainUpTaken = Up(plain, new Point(20, 20)); Pump(50);
        Check("held on a button without a menu: nothing opens - let go, it is a click as always", TouchInput.LastHeld == rowGrid && !plainUpTaken && plainClicks == 1, plainClicks + " clicks");
        Down(jog, new Point(20, 20)); Pump(TouchInput.LongPressMs + 200); Up(jog, new Point(20, 20));
        Check("held on a jog button: the press is the work - never a menu", TouchInput.LastHeld == rowGrid);
        Down(own, new Point(20, 20)); Pump(TouchInput.LongPressMs + 200); Up(own, new Point(20, 20)); Pump(50);
        Check("held on a control whose right-click makes its own menu (a side bar button, a motion value): its right-click given", TouchInput.LastHeld == own && ownRight == 1, ownRight + "");
        Down(rowGrid, press); MoveTo(rowGrid, new Point(press.X, press.Y + 60)); Pump(TouchInput.LongPressMs + 200); Up(rowGrid, new Point(press.X, press.Y + 60));
        Check("a finger that moved is not held", TouchInput.LastHeld == own && !rowMenu.Visible);

        Toast.Show(gf, "-");
        Tap(holder, new Point(off.Left + 20, off.Top + 20));
        Check("a tap on a greyed button: why, beside the finger (its tooltip)", TouchInput.LastReason == "Only while the machine stands still" &&
              Toast.LastText == "Only while the machine stands still", TouchInput.LastReason);
        typeof(ButtonHMI).GetField("_softEnabled", Any).SetValue(soft, false);
        typeof(ButtonHMI).GetField("_disabledReason", Any).SetValue(soft, "Action Disabled.\nRequired Machine State:\n  • Manual mode");
        Tap(soft, new Point(20, 20));
        Check("a tap on a button the program holds back: the conditions it waits for", TouchInput.LastReason != null && TouchInput.LastReason.Contains("Manual mode"), TouchInput.LastReason);
        var greyMenu = UiStyle.Menu();
        greyMenu.Items.Add(new ToolStripMenuItem("Delete") { Name = "gmDelete" });
        greyMenu.Items.Add(UiStyle.Unavailable(new ToolStripMenuItem("Write to the PLC") { Name = "gmWrite" }, false, "For an Admin (sign in as one)"));
        UiStyle.ShowPopup(greyMenu, gf, new Point(300, 350)); Pump(100);
        var gi = Item(greyMenu, "gmWrite");
        Tap(greyMenu, new Point(gi.Bounds.Left + 20, gi.Bounds.Top + gi.Bounds.Height / 2));
        Check("a tap on a greyed menu entry: its reason", TouchInput.LastReason == "For an Admin (sign in as one)", TouchInput.LastReason);
        greyMenu.Close(); Pump(50);

        bool listDown = Down(list, new Point(30, 10)), listUp = Up(list, new Point(30, 10)); Pump(100);
        var touchList = TouchInput.LastList;
        Check("a tap on a closed list: its entries as a menu, a finger's height each, the one chosen ticked", listDown && listUp && touchList != null && touchList.Visible &&
              touchList.Items.Count == 3 && ((ToolStripMenuItem)touchList.Items[0]).Checked && TouchInput.EntryHeight(touchList.Items[1]) >= TouchInput.MenuEntryHeight,
              touchList == null ? "none" : touchList.Items.Count + " entries, " + TouchInput.EntryHeight(touchList.Items[1]) + " px");
        Shot(gf, "touch_list");
        ((ToolStripMenuItem)touchList.Items[2]).PerformClick(); Pump(50);
        Check("  one chosen: chosen in the list, as a user's choice (SelectionChangeCommitted)", list.SelectedIndex == 2 && committed == 1, list.SelectedIndex + " / " + committed);
        touchList.Close(); Pump(50);

        rowGrid.CurrentCell = rowGrid.Rows[5].Cells[0]; Pump(50);
        var dots = TouchInput.RowMenuOf(rowGrid);
        var row5 = rowGrid.GetRowDisplayRectangle(5, true);
        Check("a table with a row menu: a ⋮ at the end of its current row", dots != null && dots.Visible && dots.Top >= row5.Top && dots.Bottom <= row5.Bottom &&
              dots.Right <= row5.Right && dots.Right >= row5.Right - 10, dots == null ? "none" : dots.Bounds + " in " + row5);
        Shot(gf, "row_dots", new Rectangle(0, 0, 540, 320));
        rightRow = -1;
        typeof(Control).GetMethod("OnClick", Any).Invoke(dots, new object[] { EventArgs.Empty }); Pump(100);
        Check("  tapped: the row's menu, for that row", rightRow == 5 && rowMenu.Visible, "row " + rightRow);
        rowMenu.Close(); Pump(50);
        Mode("Mouse"); Pump(50);
        Check("a mouse again: no ⋮, a press held is a press", !dots.Visible);
        Down(rowGrid, press); Pump(TouchInput.LongPressMs + 200); Up(rowGrid, press);
        Check("  no menu from holding", !rowMenu.Visible);
        gf.Close(); gf.Dispose();

        Console.WriteLine("--- the keyboard on the screen, the keypad for numbers ---");
        Mode("Touch");
        var kf = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(20, 20), ClientSize = new Size(900, 900), Text = "typing" };
        var name = new TextBox { Name = "name", Location = new Point(20, 20), Width = 300 };
        var low = new TextBox { Name = "low", Location = new Point(20, 860), Width = 300 };
        var shown = new TextBox { Location = new Point(340, 20), Width = 200, ReadOnly = true, Text = "read only" };
        var nud = new NumericUpDown { Location = new Point(560, 20), Width = 100, Minimum = -10, Maximum = 100, DecimalPlaces = 1, Value = 5 };
        var cells = new DataGridView { Location = new Point(20, 80), Size = new Size(600, 240), AllowUserToAddRows = false, RowHeadersVisible = false };
        cells.Columns.Add(new DataGridViewTextBoxColumn { Name = "label", HeaderText = "Label" });
        cells.Columns.Add(new DataGridViewTextBoxColumn { Name = "count", HeaderText = "Count", ValueType = typeof(int) });
        for (int i = 0; i < 4; i++) cells.Rows.Add("item " + i, i);
        kf.Controls.Add(name); kf.Controls.Add(low); kf.Controls.Add(shown); kf.Controls.Add(nud); kf.Controls.Add(cells);
        kf.Show(); Pump(200); TouchInput.Sweep();
        name.Focus(); Pump(30);
        Tap(name, new Point(30, 8)); Pump(80);
        var kb = TouchKeyboard.Current;
        Check("a tap on a text box: the keyboard on the screen, for that box - never taking its focus",
              TouchKeyboard.IsShown && kb.Target == name && kb.Name == "touchKeyboard", kb == null ? "none" : "target " + (kb.Target == null ? "-" : kb.Target.Name));
        Shot(kb, "keyboard");
        foreach (var k in new[] { "ş", "e", "k", "e", "r" }) kb.Press(k);
        kb.Press("Space");
        kb.Press("Shift"); kb.Press("i"); kb.Press("z"); kb.Press("m");
        kb.Press("Shift"); kb.Press("ı");
        Check("typed: Turkish letters; Shift for the next letter only, i - İ and ı - I as Turkish writes them", name.Text == "şeker İzmI", name.Text);
        kb.Press("Back");
        kb.Press("Signs"); kb.Press("@"); kb.Press("Signs");
        Check("  backspace; the page of signs and back", name.Text == "şeker İzm@" && !kb.Signs, name.Text);
        kb.Press("Shift"); kb.Press("Shift");
        foreach (var k in new[] { "ç", "ö" }) kb.Press(k);
        Check("  Shift twice: all capitals until tapped again", name.Text.EndsWith("ÇÖ") && kb.CapsLock, name.Text);
        kb.Press("Shift");
        var kbBounds = kb.Bounds;
        Check("  at the bottom of the screen, the box seen above it", kbBounds.Bottom >= Screen.PrimaryScreen.WorkingArea.Bottom - 10 &&
              !kbBounds.IntersectsWith(new Rectangle(name.PointToScreen(Point.Empty), name.Size)), kbBounds.ToString());
        Tap(low, new Point(30, 8)); Pump(80);
        Check("a box down where the keyboard would be: the keyboard at the top of the screen", TouchKeyboard.IsShown && kb.Target == low &&
              kb.Top <= Screen.PrimaryScreen.WorkingArea.Top + 10, kb.Bounds + " target " + (kb.Target == null ? "-" : kb.Target.Name) + " box " + new Rectangle(low.PointToScreen(Point.Empty), low.Size));
        kb.Press("Enter"); Pump(50);
        Check("  Enter in a one-line box: done with - the keyboard goes", !TouchKeyboard.IsShown);
        Tap(shown, new Point(30, 8)); Pump(80);
        Check("a box that is only read: no keyboard", !TouchKeyboard.IsShown);
        Tap(name, new Point(30, 8)); Pump(80);
        kb.Press("Hide"); Pump(30);
        Check("✕ puts it away", !TouchKeyboard.IsShown);

        NumpadForm asked = null;
        string answer = "12.5";
        TouchInput.AskNumber = (pad, owner) => { asked = pad; typeof(NumpadForm).GetField("_text", Any).SetValue(pad, answer); return DialogResult.OK; };
        Tap(nud, new Point(20, 8)); Pump(80);
        Check("a number box with arrows: the keypad, its limits - its value set", asked != null && asked.Minimum == -10 && asked.Maximum == 100 && asked.AllowDecimal &&
              nud.Value == 12.5m, nud.Value + "");
        asked = null; answer = "42";
        cells.CurrentCell = cells.Rows[2].Cells["count"]; cells.BeginEdit(true); Pump(50);
        Tap(cells.EditingControl, new Point(10, 8)); Pump(120);
        Check("a table's number cell being edited: the keypad, its value in it - OK writes it as Enter would", asked != null && !asked.AllowDecimal &&
              !cells.IsCurrentCellInEditMode && Convert.ToInt32(cells.Rows[2].Cells["count"].Value) == 42,
              Convert.ToString(cells.Rows[2].Cells["count"].Value) + " asked " + (asked != null) + " decimal " + (asked != null && asked.AllowDecimal) + " editing " + cells.IsCurrentCellInEditMode);
        cells.CurrentCell = cells.Rows[1].Cells["label"]; cells.BeginEdit(true); Pump(50);
        Tap(cells.EditingControl, new Point(10, 8)); Pump(120);
        Check("  a text cell: the keyboard, for its editor", TouchKeyboard.IsShown && kb.Target == cells.EditingControl);
        kb.Press("x");
        Check("    typed into the cell", ((TextBox)cells.EditingControl).Text.Contains("x"), ((TextBox)cells.EditingControl).Text);
        cells.EndEdit(); TouchKeyboard.HideNow();
        TouchInput.NumberColumns(cells, "label");
        Check("  a table that keeps its numbers as text says which cells are numbers", TouchInput.IsNumberCell(cells, cells.Rows[0].Cells["label"]) &&
              !TouchInput.IsNumberCell(cells, cells.Rows[0].Cells["count"]));
        Mode("Mouse");
        Tap(name, new Point(30, 8)); Pump(80);
        Check("a mouse: no keyboard", !TouchKeyboard.IsShown);
        TouchInput.AskNumber = (pad, owner) => pad.ShowDialog(owner);
        kf.Close(); kf.Dispose();

        Console.WriteLine("--- a finger drag scrolls, a tap chooses ---");
        Mode("Touch");
        var pf = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(10, 10), ClientSize = new Size(1200, 900), Text = "scrolling" };
        var long1 = new DataGridView { Name = "longTable", Location = new Point(10, 10), Size = new Size(600, 400), AllowUserToAddRows = false, RowHeadersVisible = false,
                                       SelectionMode = DataGridViewSelectionMode.FullRowSelect, ReadOnly = true, MultiSelect = false };
        UiStyle.GridLook(long1);
        for (int c = 0; c < 6; c++) long1.Columns.Add("c" + c, "Column " + c);
        foreach (DataGridViewColumn col in long1.Columns) col.Width = 250;
        for (int i = 0; i < 100; i++) long1.Rows.Add("row " + i, i, i * 2, i * 3, i * 4, i * 5);
        int clicks = 0; long1.CellClick += (s, e) => clicks++;
        var tall = new Panel { Name = "tallPage", Location = new Point(630, 10), Size = new Size(400, 400), AutoScroll = true, BorderStyle = BorderStyle.FixedSingle };
        var inPage = new Button { Text = "In the tall", Location = new Point(20, 20), Size = new Size(160, 44) };
        int pageClicks = 0; inPage.Click += (s, e) => pageClicks++;
        var slider = new TrackBar { Location = new Point(20, 100), Width = 300, Maximum = 100 };
        var jog2 = new ButtonHMI { Location = new Point(20, 180), Size = new Size(120, 44) };
        jog2.BitActions.Add(new Kaban.Core.Models.BitAction());
        tall.Controls.Add(inPage); tall.Controls.Add(slider); tall.Controls.Add(jog2);
        tall.Controls.Add(new Label { Text = "end", Location = new Point(20, 1900), AutoSize = true });
        var lb = new ListBox { Name = "longList", Location = new Point(10, 430), Size = new Size(300, 300), IntegralHeight = false };
        for (int i = 0; i < 100; i++) lb.Items.Add("entry " + i);
        pf.Controls.Add(long1); pf.Controls.Add(tall); pf.Controls.Add(lb);
        pf.Show(); Pump(200); TouchInput.Sweep(); Pump(50);
        long1.CurrentCell = long1.Rows[0].Cells[0]; Pump(30);
        var r2 = long1.GetCellDisplayRectangle(0, 2, false);
        var start = new Point(r2.Left + 40, r2.Top + r2.Height / 2);
        bool d0 = Down(long1, start);
        Check("a finger down on a table's row: the row not chosen yet", d0 && long1.CurrentCell.RowIndex == 0);
        MoveTo(long1, new Point(start.X, start.Y - 5));
        int rowH2 = long1.Rows[0].Height;
        bool m1 = MoveTo(long1, new Point(start.X, start.Y - 5 * rowH2));
        Check("dragged up five rows: the table scrolled five rows, the rows not chosen", m1 && long1.FirstDisplayedScrollingRowIndex == 5 &&
              long1.CurrentCell.RowIndex == 0 && TouchInput.LastScrolled == "longTable", long1.FirstDisplayedScrollingRowIndex + " first, row " + long1.CurrentCell.RowIndex);
        MoveTo(long1, new Point(start.X, start.Y - 9 * rowH2));
        MoveTo(long1, new Point(start.X, start.Y - 7 * rowH2));
        Check("  and back down two: it follows the finger", long1.FirstDisplayedScrollingRowIndex == 7, long1.FirstDisplayedScrollingRowIndex + "");
        bool u1 = Up(long1, new Point(start.X, start.Y - 7 * rowH2)); Pump(50);
        Check("  lifted: nothing clicked", u1 && clicks == 0 && long1.CurrentCell.RowIndex == 0);
        var r9 = long1.GetCellDisplayRectangle(1, 9, false);
        Tap(long1, new Point(r9.Left + 20, r9.Top + r9.Height / 2)); Pump(50);
        Check("a tap: that row chosen, a click", long1.CurrentCell.RowIndex == 9 && clicks == 1, "row " + long1.CurrentCell.RowIndex + ", " + clicks + " clicks");
        var mid = new Point(400, 200);
        Down(long1, mid); MoveTo(long1, new Point(mid.X - 300, mid.Y + 2)); Up(long1, new Point(mid.X - 300, mid.Y + 2));
        Check("dragged left: the columns scroll", long1.HorizontalScrollingOffset == 300, long1.HorizontalScrollingOffset + "");
        long1.HorizontalScrollingOffset = 0; Pump(30);
        int firstBefore = long1.FirstDisplayedScrollingRowIndex;
        var head = new Point(100, long1.ColumnHeadersHeight / 2);
        bool hd = Down(long1, head), hm = MoveTo(long1, new Point(100, head.Y + 150)); Up(long1, new Point(100, head.Y + 150));
        Check("from the headings: no scroll (their widths, their order)", !hd && !hm && long1.FirstDisplayedScrollingRowIndex == firstBefore);

        bool pd = Down(inPage, new Point(30, 20));
        bool pm = MoveTo(inPage, new Point(30, 20 - 300));
        bool pu = Up(inPage, new Point(30, -280)); Pump(50);
        Check("a page too tall: a drag from a button in it scrolls it - the button not clicked", !pd && pm && pu && -tall.AutoScrollPosition.Y == 300 && pageClicks == 0,
              -tall.AutoScrollPosition.Y + " / " + pageClicks + " clicks, scrolled " + TouchInput.LastScrolled);
        tall.AutoScrollPosition = new Point(0, 0); Pump(30);
        Down(slider, new Point(20, 10)); bool sm = MoveTo(slider, new Point(20, 10 - 200)); Up(slider, new Point(20, -190));
        Down(jog2, new Point(20, 20)); bool jm = MoveTo(jog2, new Point(20, 20 - 200)); Up(jog2, new Point(20, -180));
        Check("  not from a slider, nor from a jog button: their drag is theirs", !sm && !jm && tall.AutoScrollPosition.Y == 0);
        Down(lb, new Point(40, 200)); MoveTo(lb, new Point(40, 200 - 10 * lb.ItemHeight)); Up(lb, new Point(40, 200 - 10 * lb.ItemHeight));
        Check("a list: its entries scroll", lb.TopIndex == 10 && lb.SelectedIndex < 0, lb.TopIndex + " top, chosen " + lb.SelectedIndex);
        Mode("Mouse");
        long1.FirstDisplayedScrollingRowIndex = 0; Pump(30);
        bool md = Down(long1, start), mm = MoveTo(long1, new Point(start.X, start.Y - 5 * rowH2)); Up(long1, new Point(start.X, start.Y - 5 * rowH2));
        Check("a mouse: a drag is a drag (it chooses rows), nothing scrolls by itself", !md && !mm && long1.FirstDisplayedScrollingRowIndex == 0);
        pf.Close(); pf.Dispose();

        UserStore.RemoveUser("tmop");                   // the store is shared with the other tests
        Console.WriteLine(fail == 0 ? "TOUCH MODE ALL PASS" : "TOUCH MODE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
