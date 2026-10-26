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
using Kaban.UI.Screens;

// 6 October: "I don't want to use right click (touch screens). I want the users to be able to
// add a helper, translation and comment for each control - a second page: while it is open any
// control hovered or clicked is shown on it (the tooltips first, then translations, then a helper
// entry - a text and a file - then comments). I should get them with one click; they should be
// able to ask the HMI to make a file of their entries to send us as feedback."
// Answers: a tap only picks (nothing is operated); translations and helpers of Maintenance and
// Admin are in force at once, an Operator's are proposals; anyone signed in writes.
class NF {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static bool Pump0() { Pump(150); return true; }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }
    static void Snap(Control c, string path) {
        Pump(150);
        var at = c.PointToScreen(Point.Empty);
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("import",
            "-window root -crop " + c.Width + "x" + c.Height + "+" + at.X + "+" + at.Y + " " + path) { UseShellExecute = false });
        p.WaitForExit(10000);
    }
    // a tap as Windows sends it, through the notes page's filter
    static bool Tap(NotesPage page, Control c, int msg, int wparam = 0) {
        var picker = typeof(NotesPage).GetField("_picker", Any).GetValue(page) as IMessageFilter;
        if (picker == null) return false;
        var m = Message.Create(c.Handle, msg, new IntPtr(wparam), IntPtr.Zero);
        return picker.PreFilterMessage(ref m);
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "nf_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        NotesStore.Folder = Path.Combine(dir, "Notes");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        NotesPage.FeedbackFolder = Path.Combine(dir, "Feedback");
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.AddTranslation("EN", "Btn_TestStart", "Start");
        LanguageManager.AddTranslation("TR", "Btn_TestStart", "Ba\u015Flat");
        SecurityManager.Logout();

        Console.WriteLine("--- the notes, kept ---");
        Check("nobody signed in: nothing written", NotesStore.Add(new Note { Kind = NoteKind.Comment, Text = "x", ControlKey = "k" }) == null);
        SecurityManager.Login("op1", SecurityLevel.Operator);
        var c1 = NotesStore.Add(new Note { Kind = NoteKind.Comment, Text = "The start button is too small", ControlKey = "page/start", ControlName = "Start" });
        Check("an Operator's comment: kept under his name", c1 != null && c1.Author == "op1" && c1.State == NoteState.Applied && c1.Level == "Operator");
        int changed = 0;
        EventHandler onLang = (s, e) => changed++;
        LanguageManager.LanguageChanged += onLang;
        var t1 = NotesStore.Add(new Note { Kind = NoteKind.Translation, TranslationKey = "Btn_TestStart", ControlKey = "page/start",
                                           Texts = new List<NoteText> { new NoteText { Lang = "TR", Text = "\u00C7al\u0131\u015Ft\u0131r" } } });
        LanguageManager.ChangeLanguage("TR");
        Check("  his translation: a proposal - the screen keeps its text", t1.State == NoteState.Proposed && LanguageManager.GetString("Btn_TestStart", "") == "Ba\u015Flat");
        var h1 = NotesStore.Add(new Note { Kind = NoteKind.Helper, Text = "Press it twice", ControlKey = "page/start" });
        Check("  his helper: a proposal too", h1.State == NoteState.Proposed);
        Check("  he cannot accept them", !NotesStore.Accept(t1));
        SecurityManager.Login("op2", SecurityLevel.Operator);
        Check("another Operator may not remove his note", !NotesStore.CanRemove(c1));
        SecurityManager.Login("mt1", SecurityLevel.Maintenance);
        changed = 0;
        Check("Maintenance accepts the translation: on the screen at once, the pages told", NotesStore.Accept(t1) &&
              LanguageManager.GetString("Btn_TestStart", "") == "\u00C7al\u0131\u015Ft\u0131r" && changed > 0 && t1.AcceptedBy == "mt1");
        Check("  and the helper: in the control's list", NotesStore.Accept(h1) && NotesStore.Of("page/start").First(n => n.Id == h1.Id).State == NoteState.Applied);
        var t2 = NotesStore.Add(new Note { Kind = NoteKind.Translation, TranslationKey = "Btn_TestStart", ControlKey = "page/start",
                                           Texts = new List<NoteText> { new NoteText { Lang = "DE", Text = "Starten" } } });
        Check("  his own translation: in force at once", t2.State == NoteState.Applied && LanguageManager.GetStringIn("DE", "Btn_TestStart") == "Starten");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        Check("an Admin may remove anyone's note", NotesStore.CanRemove(c1));
        LanguageManager.LanguageChanged -= onLang;

        // a fresh start: read back, the translations put on the screen again
        NotesStore.Folder = NotesStore.Folder;
        LanguageManager.AddTranslation("TR", "Btn_TestStart", "Ba\u015Flat");
        Check("kept on the panel (Notes.xml): read back at the next start", NotesStore.All.Count == 4 && File.Exists(Path.Combine(NotesStore.Folder, "Notes.xml")),
              NotesStore.All.Count.ToString());
        NotesStore.ApplyTranslations();
        Check("  the translations in force put on the screen again", LanguageManager.GetString("Btn_TestStart", "") == "\u00C7al\u0131\u015Ft\u0131r");
        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine("--- the page: a tap picks, nothing is operated ---");
        SecurityManager.Login("op1", SecurityLevel.Operator);
        var form = new Form { ClientSize = new Size(1200, 760), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0), BackColor = Color.White };
        var machine = new Panel { Bounds = new Rectangle(0, 0, 660, 760), BackColor = Color.White };
        form.Controls.Add(machine);
        int pressed = 0;
        var start = new ButtonHMI { Location = new Point(30, 30), Size = new Size(140, 50), TranslationKey = "Btn_TestStart", FallbackText = "Start", Name = "btnStart" };
        start.Click += (s, e) => pressed++;
        machine.Controls.Add(start);
        var speed = new NumericInputHMI { Location = new Point(200, 110), Size = new Size(120, 30), DescriptionText = "Speed", Name = "numSpeed", Address = "D100" };
        machine.Controls.Add(speed);
        var row = new AxisSettingHMI { AxisNameKey = "Robot X", PositionAddress = "W0", MinLimitAddress = "D2", MaxLimitAddress = "D4", DecimalPlaces = 2, Name = "axRobotX" };
        row.Location = new Point(0, 200);
        var rowHost = new Panel { Bounds = new Rectangle(0, 300, 660, 200), AutoScroll = true, Name = "axisPage" };
        rowHost.Controls.Add(row);
        machine.Controls.Add(rowHost);
        // a table: a heading, a cell shown, a cell typed in
        var table = new DataGridView { Bounds = new Rectangle(340, 20, 300, 120), Name = "gridTest", AllowUserToAddRows = false, RowHeadersVisible = false };
        table.Columns.Add("axis", "Axis"); table.Columns.Add("pos", "Position"); table.Columns.Add("set", "Set value");
        table.Columns[0].ReadOnly = true; table.Columns[1].ReadOnly = true;
        table.Rows.Add("X", "12.5", "10"); table.Rows.Add("Y", "3.0", "4");
        machine.Controls.Add(table);
        // a page button of the menu (picked, not used - 22 October)
        var nav = new Button { Bounds = new Rectangle(30, 600, 140, 40), Text = "Manual", Name = "navManual" };
        int navClicks = 0;
        nav.Click += (s, e) => navClicks++;
        machine.Controls.Add(nav);
        var host = new Panel { Bounds = new Rectangle(660, 0, 540, 760) };
        form.Controls.Add(host);
        // passed through: the menu's button, and every tab strip (as the main window's rule: a page's tabs)
        var notes = new NotesPage(c => c != null && (machine.Contains(c)), c => c == nav ? "Opens: Manual Movement" : "Test machine / Page", () => "TEST", c => c == nav || c is TabControl);
        host.Controls.Add(notes);
        form.Show(); Pump(400);
        // 26 October: "We should make the page just like our other page designs ... remove 'Picking: on' button, create 'Add'
        // button instead ... We add the current 'General note', 'Make the feedback file' and 'Show the feedback file' buttons and
        // ... view, edit, find and remove on top of the notes table ... Open the selected note to the next tab"
        var views = Tree(notes).OfType<TabControl>().First();
        Func<string, Control> any = n => Tree(notes).FirstOrDefault(c => c.Name == n);
        Check("open: the notes' table first, picking off", !notes.Picking && views.SelectedTab.Name == "notesList" && views.TabPages[1].Name == "notesDetails" &&
              views.TabPages[0].Text == "Notes" && views.TabPages[1].Text == "Details");
        Check("  on top of it: Add, a general note, View, Edit, Find, Remove, the feedback file - no 'Picking' button",
              new[] { "notesAdd", "notesGeneral", "notesView", "notesEdit", "notesFindNote", "notesRemoveNote", "notesExport", "notesFolder" }.All(n => any(n) is Button &&
              any(n).Parent == any("notesAdd").Parent) && any("notesPick") == null);
        Check("  and the filters: a search, the kind, the state, who", any("notesSearch") is TextBox && any("notesKindFilter") is ComboBox &&
              any("notesStateFilter") is ComboBox && any("notesWhoFilter") is ComboBox);
        // Add: on top of the notes, and on the details' own bar (the one in sight)
        Action add = () => ((Button)(any("notesAdd").Visible ? any("notesAdd") : any("notesAddMore"))).PerformClick();
        Func<string> status = () => (string)typeof(NotesPage).GetProperty("StatusText", Any).GetValue(notes, null);
        Func<string> mode = () => typeof(NotesPage).GetProperty("Mode", Any).GetValue(notes, null).ToString();
        Action clearFilters = () => typeof(NotesPage).GetMethod("ClearFilters", Any).Invoke(notes, null);
        add();
        Check("Add: picking - the button marked, the page says what to do", notes.Picking && any("notesAdd").BackColor != Color.White &&
              status().StartsWith("Tap or click the control"), status());
        Check("a press on the button is kept from it", Tap(notes, start, 0x0201) && pressed == 0);
        Check("  its release picks it - not pressed; one control an Add: picking off, the details shown",
              Tap(notes, start, 0x0202) && Pump0() && pressed == 0 && notes.PickedName == "Start" && !notes.Picking && views.SelectedTab.Name == "notesDetails",
              notes.PickedName ?? "");
        Check("  its place: the page and the way down by names", notes.PickedKey == "Panel#0/btnStart", notes.PickedKey);
        add();
        Check("  the keys that press a button (Space, Enter) are kept from it as well",
              Tap(notes, start, 0x0100, (int)Keys.Space) && Tap(notes, start, 0x0101, (int)Keys.Space) && Pump0() && pressed == 0 &&
              notes.PickedName == "Start" && !notes.Picking);
        add();
        Check("  another key is the control's", !Tap(notes, start, 0x0100, (int)Keys.A));
        Check("Esc: picking given up", Tap(notes, start, 0x0100, (int)Keys.Escape) && Pump0() && !notes.Picking);
        add();
        var ownLabel = Tree(notes).OfType<Label>().First(l => l.Visible && notes.Contains(l));
        Check("a click on this page: picking off, the click its own", !Tap(notes, ownLabel, 0x0201) && Pump0() && !notes.Picking);
        add();
        Check("  Add pressed again: picking stays", !Tap(notes, any("notesAdd"), 0x0201) && Pump0() && notes.Picking);
        Tap(notes, start, 0x0100, (int)Keys.Escape); Pump0();
        var body = (Control)typeof(NotesPage).GetProperty("Body", Any).GetValue(notes, null);
        Func<string, Control> named = n => Tree(body).FirstOrDefault(c => c.Name == n);
        Func<DataGridView> texts = () => (DataGridView)named("notesTexts");
        Func<DataGridView, string> langs = g => string.Join(",", g.Rows.Cast<DataGridViewRow>().Select(r => Convert.ToString(r.Cells[0].Value)));
        Check("its texts in a table: language, now, your suggestion, reason - the screen's language and English when none is chosen",
              texts() != null && texts().Columns.Count == 4 && texts().Columns[2].HeaderText == "Your suggestion" && texts().Columns[3].HeaderText == "Reason" && langs(texts()) == "EN" && Convert.ToString(texts().Rows[0].Cells[1].Value) == "Start",
              texts() == null ? "none" : langs(texts()));
        ViewPreferences.SetList(ViewPreferences.NoteLanguages, new[] { "EN", "TR", "IT" });
        Pump(100);
        Check("  his chosen languages only (kept as his)", texts() != null && langs(texts()) == "EN,TR,IT" &&
              Convert.ToString(texts().Rows[1].Cells[1].Value) == "\u00C7al\u0131\u015Ft\u0131r" && ViewPreferences.UserValue("op1", ViewPreferences.NoteLanguages) == "EN,TR,IT",
              texts() == null ? "none" : langs(texts()));
        Check("  the comment closed until something is required", texts().Rows.Cast<DataGridViewRow>().All(r => r.Cells[3].ReadOnly) && !texts().Rows[0].Cells[2].ReadOnly);
        var caption = Tree(machine).OfType<Label>().FirstOrDefault(l => l.Text.StartsWith("Speed"));
        Check("a tap on a caption: its field", caption != null && NoteTarget.Notable(caption) == speed, caption == null ? "no caption" : NoteTarget.Notable(caption).GetType().Name);
        Pump(200);
        var minField = row.Controls.Cast<Control>().FirstOrDefault(c => row.ColumnOf(c) == "Min");
        Check("a field of an axis row: named by its axis and its column", minField != null && NoteTarget.NameOf(minField) == "Robot X - Min",
              minField == null ? "none" : NoteTarget.NameOf(minField));
        Check("  its place by the column (the same whatever the PLC axis items)", minField != null && NoteTarget.KeyOf(minField).EndsWith("axRobotX/col:Min:" + minField.GetType().Name),
              minField == null ? "" : NoteTarget.KeyOf(minField));
        Check("  no texts of its own (a value: its heading is translated on the heading)", minField != null && NoteTarget.KeysOf(minField).Count == 0,
              minField == null ? "" : string.Join(",", NoteTarget.KeysOf(minField).Select(k => k.Key)));
        add();
        Check("the notes page itself is not picked", !Tap(notes, notes, 0x0202));
        Tap(notes, start, 0x0100, (int)Keys.Escape); Pump0();

        Console.WriteLine("--- writing on the page ---");
        add(); Tap(notes, start, 0x0201); Tap(notes, start, 0x0202); Pump(50);
        var tg = texts();
        tg.Rows[2].Cells[2].Value = "Avvia";
        Check("  required written: its comment opens", !tg.Rows[2].Cells[3].ReadOnly);
        tg.Rows[2].Cells[3].Value = "the Italian panel says Avvia";
        // 26 October: "Clicking to a control discards everything on the notes page directly and it causes to data loss.
        // We should warn the user before discarding the previous one"
        string asked0 = null;
        Kaban.UI.Profiles.Ask.Hook = (t, q, i) => { asked0 = q; return "no"; };
        add(); Tap(notes, speed, 0x0201); Tap(notes, speed, 0x0202); Pump(100);
        Check("another control picked with a suggestion typed: asked first - No: the suggestion still there",
              asked0 != null && asked0.Contains("not kept yet") && notes.PickedName == "Start" && texts() != null &&
              Convert.ToString(texts().Rows[2].Cells[2].Value) == "Avvia" && Convert.ToString(texts().Rows[2].Cells[3].Value) == "the Italian panel says Avvia" && !notes.Picking,
              asked0 + " / " + notes.PickedName);
        Kaban.UI.Profiles.Ask.Hook = null;
        ((TextBox)named("notesCommentText")).Text = "Half typed";
        ViewPreferences.SetList(ViewPreferences.NoteLanguages, new[] { "EN", "TR", "IT" });     // the page fills again
        Pump(100);
        Check("  the page filling again (a preference, a note kept) keeps what is typed", ((TextBox)named("notesCommentText")).Text == "Half typed" &&
              Convert.ToString(texts().Rows[2].Cells[2].Value) == "Avvia");
        ((TextBox)named("notesCommentText")).Text = "";
        ((Button)named("notesKeepTexts")).PerformClick(); Pump(50);
        var mine = NotesStore.Of(notes.PickedKey).Where(n => n.Kind == NoteKind.Translation && n.Author == "op1").ToList();
        Check("an Operator's texts: proposed, only the one changed, with what it was and why", mine.Count == 1 && mine[0].Texts.Count == 1 && mine[0].TextIn("IT") == "Avvia" &&
              mine[0].Texts[0].Comment == "the Italian panel says Avvia" && mine[0].Texts[0].Now == "" &&
              mine[0].State == NoteState.Proposed && LanguageManager.GetStringIn("IT", "Btn_TestStart") == null, string.Join(",", mine.Select(n => n.Texts.Count)));
        Check("  the control's type and designer path on the note (for the builder)", mine[0].ControlType == typeof(ButtonHMI).FullName && mine[0].ControlPath.EndsWith("btnStart"),
              mine[0].ControlType + " / " + mine[0].ControlPath);
        Check("  an Operator sees no settings table", named("notesProps") == null);
        var helperFile = Path.Combine(dir, "start_button.txt");
        File.WriteAllText(helperFile, "the start button");
        notes.ChooseFile(helperFile);
        typeof(NotesPage).GetMethod("KeepHelper", Any, null, new[] { typeof(string) }, null).Invoke(notes, new object[] { "Hold it for a second" });
        var helper = NotesStore.Of(notes.PickedKey).LastOrDefault(n => n.Kind == NoteKind.Helper);
        Check("a helper with a file: its file kept in the notes", helper != null && helper.Text == "Hold it for a second" && NotesStore.PathOf(helper) != null &&
              helper.FileName == "start_button.txt");
        typeof(NotesPage).GetMethod("AddComment", Any).Invoke(notes, new object[] { "Works well" });
        Pump(50);
        Check("a comment, and the page shows it", NotesStore.Of(notes.PickedKey).Any(n => n.Kind == NoteKind.Comment && n.Text == "Works well") &&
              Tree(body).OfType<Label>().Any(l => l.Text == "Works well"));
        Check("  the Operator's proposal there, no Accept for him", Tree(body).OfType<Label>().Any(l => l.Text.Contains("proposed")) && named("notesAccept") == null);
        Snap(form, "/tmp/notes_page.png");

        Console.WriteLine("--- a table: its heading, a cell shown, a cell typed in ---");
        var headAt = table.GetCellDisplayRectangle(1, -1, true);
        Func<int, int, IntPtr> lp = (x, y) => new IntPtr((y << 16) | (x & 0xFFFF));
        Func<IMessageFilter> picker = () => typeof(NotesPage).GetField("_picker", Any).GetValue(notes) as IMessageFilter;
        Func<int, Point, bool> tapAt = (msg, pt) => { var m = Message.Create(table.Handle, msg, IntPtr.Zero, lp(pt.X, pt.Y)); var f = picker(); return f != null && f.PreFilterMessage(ref m); };
        var hp = new Point(headAt.X + 5, headAt.Y + 5);
        add();
        Check("a heading: picked (the tap kept from the table)", tapAt(0x0201, hp) && tapAt(0x0202, hp) && Pump0() && notes.PickedKind == NotesPage.PickKind.Heading &&
              notes.PickedName.StartsWith("Column heading: Position"), notes.PickedName ?? "");
        var shownAt = table.GetCellDisplayRectangle(1, 0, true);
        var sp = new Point(shownAt.X + 5, shownAt.Y + 5);
        add();
        Check("a cell shown: picked as a value", tapAt(0x0201, sp) && tapAt(0x0202, sp) && Pump0() && notes.PickedKind == NotesPage.PickKind.Cell &&
              notes.PickedName.Contains("Position") && notes.PickedName.Contains("(X)"), notes.PickedName ?? "");
        Check("  its value: now, what it should be, why", named("notesValueRequired") != null && Tree(body).OfType<Label>().Any(l => l.Text.EndsWith(": 12.5")));
        typeof(NotesPage).GetMethod("KeepValue", Any).Invoke(notes, new object[] { "12.0", "the sensor is offset" });
        var value = NotesStore.Of(notes.PickedKey).LastOrDefault(n => n.Kind == NoteKind.Value);
        Check("  kept: now 12.5, required 12.0, why", value != null && value.Values.Count == 1 && value.Values[0].Now == "12.5" && value.Values[0].Required == "12.0" &&
              value.Values[0].Comment == "the sensor is offset");
        var entryAt = table.GetCellDisplayRectangle(2, 0, true);
        var ep = new Point(entryAt.X + 5, entryAt.Y + 5);
        add();
        Check("a cell typed in: the table's own (not picked)", !tapAt(0x0201, ep) && !tapAt(0x0202, ep) && Pump0() && notes.PickedKind == NotesPage.PickKind.Cell);

        Console.WriteLine("--- a page button of the menu: picked, not used ---");
        // 22 October: "the navigation buttons work as function buttons even with Picking on - they should
        // turn to feedback mode as well; the user changes the picking mode to change the screen"
        Check("its press and release kept from it: the page stays", Tap(notes, nav, 0x0201) && Tap(notes, nav, 0x0202) && Tap(notes, nav, 0x0101, (int)Keys.Space) &&
              Pump0() && navClicks == 0);
        Check("  picked, with the page it opens", notes.PickedKind == NotesPage.PickKind.Menu && notes.PickedName.StartsWith("Menu button") &&
              notes.PickedPlace == "Opens: Manual Movement", notes.PickedName + " / " + notes.PickedPlace);
        var shadeObj = typeof(NotesPage).GetField("_shade", Any).GetValue(notes);
        add();
        Check("  pointed at: let through (its tooltip), bright in the shade", !Tap(notes, nav, 0x0200) && shadeObj.GetType().GetProperty("Hole", Any).GetValue(shadeObj, null) == nav);
        Tap(notes, start, 0x0100, (int)Keys.Escape); Pump0();

        Console.WriteLine("--- a page's tabs: picked, not opened ---");
        var tabsCtl = new TabControl { Bounds = new Rectangle(340, 300, 300, 160), Name = "tabsTest" };
        tabsCtl.TabPages.Add(new TabPage("One") { Name = "tpOne" });
        tabsCtl.TabPages.Add(new TabPage("Two") { Name = "tpTwo" });
        var inTab = new Button { Bounds = new Rectangle(10, 10, 100, 30), Text = "In a tab", Name = "btnInTab" };
        int inTabClicks = 0;
        inTab.Click += (s, e) => inTabClicks++;
        tabsCtl.TabPages[0].Controls.Add(inTab);
        machine.Controls.Add(tabsCtl);
        Pump(150);
        var r2 = tabsCtl.GetTabRect(1);
        Func<int, bool> tabTap = msg => { var mm = Message.Create(tabsCtl.Handle, msg, IntPtr.Zero, lp(r2.X + r2.Width / 2, r2.Y + r2.Height / 2)); return picker().PreFilterMessage(ref mm); };
        add();
        Check("a tap on a tab: kept from it - the open tab stays", tabTap(0x0201) && tabTap(0x0202) && Pump0() && tabsCtl.SelectedIndex == 0);
        Check("  the tab tapped picked - not the open one", notes.PickedKind == NotesPage.PickKind.Tab && notes.PickedName == "Tab: Two" && notes.PickedKey.EndsWith("#tab:tpTwo"),
              notes.PickedName + " / " + notes.PickedKey);
        add();
        Check("a control on a tab's page: picked as itself, not as the tabs", Tap(notes, inTab, 0x0201) && Tap(notes, inTab, 0x0202) && Pump0() &&
              notes.PickedKind == NotesPage.PickKind.Control && notes.PickedName.Contains("In a tab") && inTabClicks == 0, notes.PickedKind + " " + notes.PickedName);

        Console.WriteLine("--- a general note, with a file ---");
        views.SelectedIndex = 0; Pump0();                 // on top of the notes
        ((Button)Tree(notes).First(c => c.Name == "notesGeneral")).PerformClick();
        Pump(50);
        Check("the general note's page", notes.PickedKind == NotesPage.PickKind.General && named("notesGeneralText") != null);
        var drawing = Path.Combine(dir, "layout_idea.txt");
        File.WriteAllText(drawing, "a sketch");
        notes.ChooseFile(drawing);
        typeof(NotesPage).GetMethod("KeepGeneral", Any).Invoke(notes, new object[] { "A second start button at the exit would help." });
        var general = NotesStore.All.LastOrDefault(n => n.IsGeneral);
        Check("  kept on no control, its file with it", general != null && general.Text.StartsWith("A second start") && NotesStore.PathOf(general) != null);
        add(); Tap(notes, start, 0x0201); Tap(notes, start, 0x0202); Pump(50);
        SecurityManager.Login("mt1", SecurityLevel.Maintenance);
        Pump(100);
        Check("Maintenance sees Accept on it", named("notesAccept") != null);

        Console.WriteLine("--- an Admin: the control's settings, the helpers on the screen ---");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        Pump(100);
        var props = (DataGridView)named("notesProps");
        var rows = props == null ? new List<DataGridViewRow>() : props.Rows.Cast<DataGridViewRow>().ToList();
        Func<string, DataGridViewRow> prop = n => rows.FirstOrDefault(r => Convert.ToString(r.Tag) == n);
        Check("its settings in a table: the program's own (the key, the PLC ...), now", props != null && prop("TranslationKey") != null &&
              Convert.ToString(prop("TranslationKey").Cells[1].Value) == "Btn_TestStart" && prop("PlcNumber") != null && prop("BackColor") == null,
              props == null ? "none" : string.Join(",", rows.Take(12).Select(r => r.Tag)));
        prop("PlcNumber").Cells[2].Value = "2";
        prop("PlcNumber").Cells[3].Value = "the start is on the second PLC";
        ((Button)named("notesKeepProps")).PerformClick(); Pump(50);
        var asked = NotesStore.Of(notes.PickedKey).LastOrDefault(n => n.Kind == NoteKind.Property);
        Check("  asked for: kept for the builder (the program not changed)", asked != null && asked.Values.Count == 1 && asked.Values[0].Name == "PlcNumber" &&
              asked.Values[0].Now == "1" && asked.Values[0].Required == "2" && start.PlcNumber == 1);
        typeof(NotesPage).GetMethod("KeepHelper", Any, null, new[] { typeof(string), typeof(string) }, null).Invoke(notes, new object[] { "Press it once the guard is closed.", "operators asked" });
        Pump(50);
        NoteHelpers.Refresh(machine);
        var mark = NoteHelpers.MarkOf(start);
        Check("an Admin's helper: on the screen at once - a \"?\" on the button's corner", mark != null && mark.Visible && mark.Parent == machine &&
              mark.Left >= start.Right - mark.Width && mark.Top <= start.Top, mark == null ? "no mark" : mark.Bounds + " / " + start.Bounds);
        Check("  it opens the helper (a page of its words)", NoteHelpers.DocumentsFor(start).Count >= 1 &&
              NoteHelpers.DocumentsFor(start).All(d => File.Exists(d.FileName)), string.Join(" | ", NoteHelpers.DocumentsFor(start).Select(d => d.Title)));
        var unit = new CylinderUnitHMI { Name = "unitClamp", Location = new Point(30, 520), CommandAAddress = "M10", CommandBAddress = "M11", TitleText = "Clamp" };
        machine.Controls.Add(unit); Pump(100);
        var unitField = Tree(unit).First(c => c is ButtonHMI);
        NotesStore.Add(new Note { Kind = NoteKind.Helper, Text = "Clamp: the valve is under the cover", ControlKey = NoteTarget.KeyOf(unitField), ControlName = "Clamp" });
        Pump(100);
        Check("a helper on a unit's button: in the unit's \"?\"", unit.UnitHelpDocuments().Any(d => d.Title.StartsWith("Clamp: the valve")),
              string.Join(" | ", unit.UnitHelpDocuments().Select(d => d.Title)));
        // 8 October: "it doesn't find TitleTextKey for the cylinder unit (e.g. 'Profile Clamping 6')"
        var frame = unit.Controls.Cast<Control>().First(c => c.GetType().Name == "RoundedGroupBox");
        Check("a tap on a unit's frame (its title, its border): the unit", NoteTarget.Notable(frame) == unit, NoteTarget.Notable(frame) == null ? "none" : NoteTarget.Notable(frame).GetType().Name);
        add();
        Check("  picked: named by its title, the title its first text", Tap(notes, frame, 0x0201) && Tap(notes, frame, 0x0202) && Pump0() && notes.PickedName == "Clamp" &&
              named("notesTexts") != null && body.Controls.Cast<Control>().Any(c => c.Text.StartsWith("Title   (Clamp)")), notes.PickedName);
        Check("  its buttons offer the unit's title too", NoteTarget.KeysOf(unitField).Any(k => k.What == "Unit title" && k.Key == "Clamp") &&
              NoteTarget.KeysOf(unitField).All(k => k.Key.Any(char.IsLetter)), string.Join(", ", NoteTarget.KeysOf(unitField).Select(k => k.What + "=" + k.Key)));
        // 8 October: "any click on the page shakes it for a few seconds and it turns to something else"
        var own = Tree(body).OfType<Label>().First(l => l.Visible);
        string before = notes.PickedKey;
        add();
        Check("a tap on the notes page itself is its own: not noted as one of its tabs", !Tap(notes, own, 0x0201) && !Tap(notes, own, 0x0202) & Pump0() && notes.PickedKey == before,
              notes.PickedName);
        SecurityManager.Login("op1", SecurityLevel.Operator);
        Pump(50);

        Console.WriteLine("--- picking off: the page works as usual ---");
        Check("off: no filter, a tap is the control's again", !notes.Picking && !Tap(notes, start, 0x0201));
        add();
        host.Visible = false; Pump(50);
        Check("the page out of sight (the panel closed): no picking", !notes.Picking);
        host.Visible = true; Pump(50);
        Check("  in sight again: still off - Add turns it on", !notes.Picking);

        Console.WriteLine("--- the feedback file: one click ---");
        notes.Export();
        var zip = notes.LastExport;
        Check("made in the feedback folder", zip != null && File.Exists(zip) && zip.StartsWith(NotesPage.FeedbackFolder) && Path.GetFileName(zip).StartsWith("Feedback_TEST_"), zip ?? "none");
        if (zip != null)
        {
            var names = NotesStore.EntriesOf(zip);
            Check("  Notes.html to read, Notes.csv, Notes.xml, the helpers' files", names.Contains("Notes.html") && names.Contains("Notes.csv") &&
                  names.Contains("Notes.xml") && names.Any(n => n.StartsWith("Files/") && n.EndsWith("start_button.txt")), string.Join(", ", names));
            Check("  for the builder: README, Translations.csv in the translation file's form, Properties.csv, Values.csv, the general note's file",
                  names.Contains("README.txt") && names.Contains("Translations.csv") && names.Contains("Properties.csv") && names.Contains("Values.csv") &&
                  names.Any(n => n.EndsWith("layout_idea.txt")), string.Join(", ", names));
            var tcsv = NotesStore.TextIn(zip, "Translations.csv") ?? "";
            Check("  Translations.csv: Key;EN;TR;RU;DE;IT;ES then why and who - the row of Btn_TestStart with its IT and DE",
                  tcsv.StartsWith("Key;EN;TR;RU;DE;IT;ES;State;Reason;Who") && tcsv.Contains("Btn_TestStart;;") && tcsv.Contains(";Starten;Avvia;") && tcsv.Contains("IT: the Italian panel says Avvia"),
                  tcsv.Replace("\r\n", " / "));
            var pcsv = NotesStore.TextIn(zip, "Properties.csv") ?? "";
            Check("  Properties.csv: the control, its type and path, now -> required, why", pcsv.Contains("\"PlcNumber\";\"1\";\"2\";\"the start is on the second PLC\"") &&
                  pcsv.Contains(typeof(ButtonHMI).FullName), pcsv.Replace("\r\n", " / "));
            string html = NotesStore.TextIn(zip, "Notes.html") ?? "";
            Check("  the page and the control in it, every note", html.Contains("Test machine / Page") && html.Contains("Works well") && html.Contains("Avvia") &&
                  html.Contains("proposed"));
            File.Copy(zip, "/tmp/notes_feedback_sample.zip", true);
        }
        views.SelectedIndex = 0; Pump(50);
        var all = Tree(notes).OfType<DataGridView>().First(g => g.Name == "notesAll");
        Check("every note in a table", all.Rows.Count == NotesStore.All.Count, all.Rows.Count + " / " + NotesStore.All.Count);
        Func<IEnumerable<Note>> rowsShown = () => all.Rows.Cast<DataGridViewRow>().Select(r => (Note)r.Tag);

        // 26 October: "Add some filter elements for the table as well btw so I can check though the notes easier"
        Console.WriteLine("--- the filters ---");
        var kindBox = (ComboBox)any("notesKindFilter");
        var stateBox = (ComboBox)any("notesStateFilter");
        var whoBox = (ComboBox)any("notesWhoFilter");
        typeof(NotesPage).GetProperty("SearchText", Any).SetValue(notes, "avvia", null);
        Check("a search: every word, any case - in the texts, the control, the page, who", rowsShown().Count() == 1 && rowsShown().First().TextIn("IT") == "Avvia",
              string.Join(" | ", rowsShown().Select(n => n.Kind.ToString())));
        typeof(NotesPage).GetProperty("SearchText", Any).SetValue(notes, "start op1", null);
        Check("  two words: both somewhere in a note", rowsShown().Any() && rowsShown().All(n => n.Author == "op1"), rowsShown().Count().ToString());
        clearFilters();
        kindBox.SelectedIndex = kindBox.Items.Cast<object>().ToList().FindIndex(o => o.ToString() == "Helper");
        Check("the kind: the helpers only", rowsShown().Any() && rowsShown().All(n => n.Kind == NoteKind.Helper));
        kindBox.SelectedIndex = kindBox.Items.Cast<object>().ToList().FindIndex(o => o.ToString() == "General");
        Check("  the general notes only", rowsShown().Count() == 1 && rowsShown().First().IsGeneral);
        kindBox.SelectedIndex = 0;
        stateBox.SelectedIndex = 1;
        Check("the state: the proposals to accept", rowsShown().Any() && rowsShown().All(n => n.State == NoteState.Proposed && (n.Kind == NoteKind.Translation || n.Kind == NoteKind.Helper)));
        stateBox.SelectedIndex = 0;
        Check("who: everyone, mine, each author", whoBox.Items.Count >= 3 && whoBox.Items[0].ToString() == "Everyone" && whoBox.Items[1].ToString() == "Mine (op1)" &&
              whoBox.Items.Cast<object>().Any(o => o.ToString() == "admin"), string.Join(", ", whoBox.Items.Cast<object>()));
        whoBox.SelectedIndex = 1;
        var count = (Label)typeof(NotesPage).GetField("_count", Any).GetValue(notes);
        Check("  mine: op1's only - and how many of how many", rowsShown().Any() && rowsShown().All(n => n.Author == "op1") &&
              count.Text == rowsShown().Count() + " of " + NotesStore.All.Count + " notes", count.Text);
        clearFilters();
        Check("cleared: every note", all.Rows.Count == NotesStore.All.Count && count.Text == NotesStore.All.Count + " notes on this panel", count.Text);

        Console.WriteLine("--- a note viewed, edited, found, removed ---");
        Func<Note, bool> choose = n =>
        {
            var r = all.Rows.Cast<DataGridViewRow>().FirstOrDefault(x => ((Note)x.Tag).Id == n.Id);
            if (r == null) return false;
            all.ClearSelection(); all.CurrentCell = null; Pump0();
            all.CurrentCell = r.Cells[0]; r.Selected = true; Pump0();
            return true;
        };
        var comment = NotesStore.All.First(n => n.Kind == NoteKind.Comment && n.Text == "Works well");
        var adminHelper = NotesStore.All.Last(n => n.Kind == NoteKind.Helper && n.Author == "admin");
        Check("op1's own note chosen: View, Edit, Find, Remove", choose(comment) && any("notesView").Enabled && any("notesEdit").Enabled && any("notesFindNote").Enabled == false &&
              any("notesRemoveNote").Enabled);
        Check("  an Admin's note: View only (an Operator is below him)", choose(adminHelper) && any("notesView").Enabled && !any("notesEdit").Enabled && !any("notesRemoveNote").Enabled);
        choose(comment);
        ((Button)any("notesView")).PerformClick(); Pump0();
        Check("View: the note in the next tab - what, where, who, its words; Edit and Remove there", views.SelectedTab.Name == "notesDetails" &&
              mode() == "View" && Tree(body).OfType<Label>().Any(l => l.Text == "Works well") && Tree(body).OfType<Label>().Any(l => l.Text.StartsWith("Comment  ·  op1 (Operator)")) &&
              named("notesEditOne") != null && named("notesRemoveOne") != null && named("notesEditText") == null, mode());
        Snap(form, "/tmp/notes_view.png");
        ((Button)named("notesEditOne")).PerformClick(); Pump0();
        var editBox = (TextBox)named("notesEditText");
        Check("Edit: its words to change", mode() == "Edit" && editBox != null && editBox.Text == "Works well" && named("notesSave") != null);
        editBox.Text = "Works well, but slow";
        string asked1 = null;
        Kaban.UI.Profiles.Ask.Hook = (t, q, i) => { asked1 = q; return "no"; };
        views.SelectedIndex = 0; choose(adminHelper); ((Button)any("notesView")).PerformClick(); Pump0();
        Check("  another note viewed while it is changed: asked first - No: still editing, the words kept",
              asked1 != null && mode() == "Edit" && ((TextBox)named("notesEditText")).Text == "Works well, but slow" && views.SelectedTab.Name == "notesDetails");
        Kaban.UI.Profiles.Ask.Hook = null;
        ((Button)named("notesSave")).PerformClick(); Pump0();
        var edited1 = NotesStore.All.First(n => n.Id == comment.Id);
        Check("Save: the note changed - its author and time kept, who changed it and when", edited1.Text == "Works well, but slow" && edited1.Author == "op1" &&
              edited1.Time == comment.Time && edited1.EditedBy == "op1" && edited1.EditedAt.HasValue && mode() == "View" &&
              Tree(body).OfType<Label>().Any(l => l.Text.StartsWith("Changed by op1")));
        var proposal = NotesStore.All.First(n => n.Kind == NoteKind.Translation && n.Author == "op1" && n.TextIn("IT") == "Avvia");
        views.SelectedIndex = 0; choose(proposal); ((Button)any("notesEdit")).PerformClick(); Pump0();
        var editTexts = (DataGridView)named("notesEditTexts");
        Check("his proposed texts edited: the languages, what was there, his text and why", editTexts != null && editTexts.Rows.Count == 1 &&
              Convert.ToString(editTexts.Rows[0].Cells[2].Value) == "Avvia" && !editTexts.Rows[0].Cells[2].ReadOnly);
        Snap(form, "/tmp/notes_edit.png");
        editTexts.Rows[0].Cells[2].Value = "Avvio";
        ((Button)named("notesSave")).PerformClick(); Pump0();
        Check("  saved: a proposal still (he is an Operator)", NotesStore.All.First(n => n.Id == proposal.Id).TextIn("IT") == "Avvio" &&
              NotesStore.All.First(n => n.Id == proposal.Id).State == NoteState.Proposed && LanguageManager.GetStringIn("IT", "Btn_TestStart") == null);

        Note sought = null;
        notes.ShowNoted = n => { sought = n; return ""; };
        views.SelectedIndex = 0; choose(proposal); Pump0();
        ((Button)any("notesFindNote")).PerformClick(); Pump0();
        Check("Find: the main window shows the note's control - the page says so", sought != null && sought.Id == proposal.Id && status().Contains("flashing"),
              status());
        notes.ShowNoted = n => "Its page is not open: open it (x) and Find again.";
        ((Button)any("notesFindNote")).PerformClick(); Pump0();
        Check("  or why it cannot", status().StartsWith("Its page is not open"));
        notes.ShowNoted = null;
        Check("the note's place kept for Find (the main window's: none here)", proposal.ControlKey == "Panel#0/btnStart");

        Console.WriteLine("--- who may change or remove a note ---");
        Func<string, SecurityLevel, Note> noteBy = (who, level) => new Note { Author = who, Level = level.ToString(), Kind = NoteKind.Comment, Text = "x", ControlKey = "k" };
        SecurityManager.Login("op2", SecurityLevel.Operator);
        Check("an Operator: his own only - not another Operator's", NotesStore.CanChange(noteBy("op2", SecurityLevel.Operator)) && !NotesStore.CanChange(noteBy("op1", SecurityLevel.Operator)));
        SecurityManager.Login("mt1", SecurityLevel.Maintenance);
        Check("Maintenance: an Operator's too - not another Maintenance's, not an Admin's", NotesStore.CanChange(noteBy("op1", SecurityLevel.Operator)) &&
              !NotesStore.CanChange(noteBy("mt2", SecurityLevel.Maintenance)) && !NotesStore.CanChange(noteBy("admin", SecurityLevel.Admin)));
        SecurityManager.Login("admin2", SecurityLevel.Admin);
        Check("an Admin: Maintenance's and an Operator's too - not another Admin's", NotesStore.CanChange(noteBy("mt1", SecurityLevel.Maintenance)) &&
              NotesStore.CanRemove(noteBy("op1", SecurityLevel.Operator)) && !NotesStore.CanChange(noteBy("admin", SecurityLevel.Admin)));
        SecurityManager.Login("mt1", SecurityLevel.Maintenance);
        Pump0();
        views.SelectedIndex = 0; choose(edited1);
        Kaban.UI.Profiles.Ask.Hook = (t, q, i) => "yes";
        int before2 = NotesStore.All.Count;
        ((Button)any("notesRemoveNote")).PerformClick(); Pump0();
        Kaban.UI.Profiles.Ask.Hook = null;
        Check("Remove (Maintenance, an Operator's note): asked, gone from the table", NotesStore.All.Count == before2 - 1 && !NotesStore.All.Any(n => n.Id == edited1.Id) &&
              all.Rows.Count == NotesStore.All.Count);
        Check("  its details tab told", views.SelectedTab.Name == "notesList" || mode() == "None");
        SecurityManager.Login("op1", SecurityLevel.Operator);
        Pump0();
        Snap(form, "/tmp/notes_list.png");

        // 22 October: "the picking mode should turn off automatically when the notes page is closed"
        Console.WriteLine("--- the page closed: picking off ---");
        add();
        Check("Add: picking", notes.Picking);
        host.Visible = false; Pump(100);
        Check("its panel closed: picking off, no tap taken", !notes.Picking && typeof(NotesPage).GetField("_picker", Any).GetValue(notes) == null &&
              !(bool)typeof(NotesPage).GetField("_picking", Any).GetValue(notes));
        host.Visible = true; Pump(100);
        Check("  opened again: off until Add", !notes.Picking);
        add();
        host.Controls.Remove(notes); Pump(100);
        Check("taken out of its window (the window closed): picking off", !notes.Picking && typeof(NotesPage).GetField("_picker", Any).GetValue(notes) == null &&
              !(bool)typeof(NotesPage).GetField("_picking", Any).GetValue(notes));
        host.Controls.Add(notes); Pump(100);
        form.Close();

        Console.WriteLine("--- the main window ---");
        var shell = new Kaban.UI.Forms.MainForm();
        var pnlContent = (Panel)typeof(Kaban.UI.Forms.MainForm).GetField("pnlContent", Any).GetValue(shell);
        var pnlMenu = (Panel)typeof(Kaban.UI.Forms.MainForm).GetField("pnlMenu", Any).GetValue(shell);
        var onPage = new Button(); pnlContent.Controls.Add(onPage);
        var inMenu = new Button(); pnlMenu.Controls.Add(inMenu);
        var pickable = (Func<Control, bool>)Delegate.CreateDelegate(typeof(Func<Control, bool>), shell, typeof(Kaban.UI.Forms.MainForm).GetMethod("IsPickable", Any));
        Check("a control of the page is picked, the menu is not (every page can be reached)", pickable(onPage) && !pickable(inMenu));
        typeof(Kaban.UI.Forms.MainForm).GetMethod("RegisterBuiltInSidePages", Any).Invoke(shell, null);
        var side = SidePages.Find("Notes");
        Check("a side page: Notes and feedback, for anyone (writing needs a sign-in)", side != null && side.RequiredLevel == SecurityLevel.None && side.Title == "Notes and feedback");
        var pnlTop = (Control)typeof(Kaban.UI.Forms.MainForm).GetField("pnlTop", Any).GetValue(shell);
        using (var np = (NotesPage)side.Create())
            Check("  darker while picking: the page, the caption bar and the menu (its buttons are picked too)",
                  np.ShadeAreas().Contains(pnlContent) && np.ShadeAreas().Contains(pnlTop) && np.ShadeAreas().Contains(pnlMenu));
        shell.Dispose();

        Console.WriteLine("--- translations ---");
        Func<string, bool> has = k => LanguageManager.GetString(k, "##") != "##";
        var keys = new[] { "Ttl_Notes", "Nts_TipAdd", "Nts_StatusAdd", "Nts_DetailsHow", "Nts_SecTip", "Nts_SecTexts", "Nts_SecHelpers",
                           "Nts_SecComments", "Nts_KeepTexts", "Nts_ProposeTexts", "Nts_KeepHelper", "Nts_ProposeHelper", "Nts_AddComment", "Nts_Accept",
                           "Nts_TipExport", "Nts_Exported", "Nts_SignIn", "Nts_Proposed", "Nts_InForce", "Nts_InList", "Nts_TabNotes", "Nts_TabDetails",
                           "Nts_AskDiscard", "Nts_TipView", "Nts_TipEdit", "Nts_TipFindNote", "Nts_TipRemoveNote", "Tip_NtsSearch", "Nts_Everyone" };
        Check("the page's texts in the file", keys.All(has), string.Join(",", keys.Where(k => !has(k))));
        SecurityManager.Logout();
        Console.WriteLine(fail == 0 ? "\nNOTES AND FEEDBACK ALL PASS" : "\nNOTES AND FEEDBACK " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
