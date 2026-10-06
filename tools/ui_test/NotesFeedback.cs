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
        var host = new Panel { Bounds = new Rectangle(660, 0, 540, 760) };
        form.Controls.Add(host);
        var notes = new NotesPage(c => c != null && (machine.Contains(c)), c => "Test machine / Page", () => "TEST");
        host.Controls.Add(notes);
        form.Show(); Pump(400);
        Check("open and in sight: picking", notes.Picking);
        Check("a press on the button is kept from it", Tap(notes, start, 0x0201) && pressed == 0);
        Check("  its release picks it - not pressed", Tap(notes, start, 0x0202) && pressed == 0 && notes.PickedName == "Start", notes.PickedName ?? "");
        Check("  its place: the page and the way down by names", notes.PickedKey == "Panel#0/btnStart", notes.PickedKey);
        Check("  the keys that press a button (Space, Enter) are kept from it as well",
              Tap(notes, start, 0x0100, (int)Keys.Space) && Tap(notes, start, 0x0101, (int)Keys.Space) && pressed == 0 &&
              !Tap(notes, start, 0x0100, (int)Keys.A));
        var body = (Control)typeof(NotesPage).GetProperty("Body", Any).GetValue(notes, null);
        Func<string, Control> named = n => Tree(body).FirstOrDefault(c => c.Name == n);
        Check("the page: its texts in every language, from the files", named("txtEN") != null && named("txtEN").Text == "Start" &&
              named("txtTR") != null && named("txtTR").Text == "\u00C7al\u0131\u015Ft\u0131r" && named("txtES") != null);
        var caption = Tree(machine).OfType<Label>().FirstOrDefault(l => l.Text.StartsWith("Speed"));
        Check("a tap on a caption: its field", caption != null && NoteTarget.Notable(caption) == speed, caption == null ? "no caption" : NoteTarget.Notable(caption).GetType().Name);
        Pump(200);
        var minField = row.Controls.Cast<Control>().FirstOrDefault(c => row.ColumnOf(c) == "Min");
        Check("a field of an axis row: named by its axis and its column", minField != null && NoteTarget.NameOf(minField) == "Robot X - Min",
              minField == null ? "none" : NoteTarget.NameOf(minField));
        Check("  its place by the column (the same whatever the PLC axis items)", minField != null && NoteTarget.KeyOf(minField).EndsWith("axRobotX/col:Min:" + minField.GetType().Name),
              minField == null ? "" : NoteTarget.KeyOf(minField));
        Check("  its texts: the column heading's key", minField != null && NoteTarget.KeysOf(minField).Any(k => k.Key == "Col_MinLimit"));
        Check("the notes page itself is not picked", !Tap(notes, notes, 0x0202));

        Console.WriteLine("--- writing on the page ---");
        Tap(notes, start, 0x0201); Tap(notes, start, 0x0202); Pump(50);
        ((TextBox)named("txtIT")).Text = "Avvia";
        ((Button)named("notesKeepTexts")).PerformClick(); Pump(50);
        var mine = NotesStore.Of(notes.PickedKey).Where(n => n.Kind == NoteKind.Translation && n.Author == "op1").ToList();
        Check("an Operator's texts: proposed, only the one changed", mine.Count == 1 && mine[0].Texts.Count == 1 && mine[0].TextIn("IT") == "Avvia" &&
              mine[0].State == NoteState.Proposed && LanguageManager.GetStringIn("IT", "Btn_TestStart") == null, string.Join(",", mine.Select(n => n.Texts.Count)));
        var helperFile = Path.Combine(dir, "start_button.txt");
        File.WriteAllText(helperFile, "the start button");
        notes.ChooseFile(helperFile);
        typeof(NotesPage).GetMethod("KeepHelper", Any).Invoke(notes, new object[] { "Hold it for a second" });
        var helper = NotesStore.Of(notes.PickedKey).LastOrDefault(n => n.Kind == NoteKind.Helper);
        Check("a helper with a file: its file kept in the notes", helper != null && helper.Text == "Hold it for a second" && NotesStore.PathOf(helper) != null &&
              helper.FileName == "start_button.txt");
        typeof(NotesPage).GetMethod("AddComment", Any).Invoke(notes, new object[] { "Works well" });
        Pump(50);
        Check("a comment, and the page shows it", NotesStore.Of(notes.PickedKey).Any(n => n.Kind == NoteKind.Comment && n.Text == "Works well") &&
              Tree(body).OfType<Label>().Any(l => l.Text == "Works well"));
        Check("  the Operator's proposal there, no Accept for him", Tree(body).OfType<Label>().Any(l => l.Text.Contains("proposed")) && named("notesAccept") == null);
        Snap(form, "/tmp/notes_page.png");
        SecurityManager.Login("mt1", SecurityLevel.Maintenance);
        Pump(100);
        Check("Maintenance sees Accept on it", named("notesAccept") != null);
        SecurityManager.Login("op1", SecurityLevel.Operator);
        Pump(50);

        Console.WriteLine("--- picking off: the page works as usual ---");
        ((Button)Tree(notes).First(c => c.Name == "notesPick")).PerformClick();
        Check("off: no filter, a tap is the control's again", !notes.Picking && !Tap(notes, start, 0x0201));
        ((Button)Tree(notes).First(c => c.Name == "notesPick")).PerformClick();
        host.Visible = false; Pump(50);
        Check("the page out of sight (the panel closed): no picking", !notes.Picking);
        host.Visible = true; Pump(50);
        Check("  in sight again: picking", notes.Picking);

        Console.WriteLine("--- the feedback file: one click ---");
        notes.Export();
        var zip = notes.LastExport;
        Check("made in the feedback folder", zip != null && File.Exists(zip) && zip.StartsWith(NotesPage.FeedbackFolder) && Path.GetFileName(zip).StartsWith("Feedback_TEST_"), zip ?? "none");
        if (zip != null)
        {
            var names = NotesStore.EntriesOf(zip);
            Check("  Notes.html to read, Notes.csv, Notes.xml, the helpers' files", names.Contains("Notes.html") && names.Contains("Notes.csv") &&
                  names.Contains("Notes.xml") && names.Any(n => n.StartsWith("Files/") && n.EndsWith("start_button.txt")), string.Join(", ", names));
            string html = NotesStore.TextIn(zip, "Notes.html") ?? "";
            Check("  the page and the control in it, every note", html.Contains("Test machine / Page") && html.Contains("Works well") && html.Contains("Avvia") &&
                  html.Contains("proposed"));
            File.Copy(zip, "/tmp/notes_feedback_sample.zip", true);
        }
        var every = Tree(notes).OfType<TabControl>().First();
        every.SelectedIndex = 1; Pump(50);
        var all = Tree(notes).OfType<DataGridView>().First(g => g.Name == "notesAll");
        Check("every note in a table", all.Rows.Count == NotesStore.All.Count, all.Rows.Count + " / " + NotesStore.All.Count);
        every.SelectedIndex = 0;
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
        shell.Dispose();

        Console.WriteLine("--- translations ---");
        Func<string, bool> has = k => LanguageManager.GetString(k, "##") != "##";
        var keys = new[] { "Ttl_Notes", "Nts_PickOn", "Nts_PickOff", "Nts_TipPickOn", "Nts_StatusPick", "Nts_How", "Nts_SecTip", "Nts_SecTexts", "Nts_SecHelpers",
                           "Nts_SecComments", "Nts_KeepTexts", "Nts_ProposeTexts", "Nts_KeepHelper", "Nts_ProposeHelper", "Nts_AddComment", "Nts_Accept", "Nts_Export",
                           "Nts_TipExport", "Nts_Exported", "Nts_SignIn", "Nts_Proposed", "Nts_InForce", "Nts_InList", "Nts_TabControl", "Nts_TabAll" };
        Check("the page's texts in the file", keys.All(has), string.Join(",", keys.Where(k => !has(k))));
        SecurityManager.Logout();
        Console.WriteLine(fail == 0 ? "\nNOTES AND FEEDBACK ALL PASS" : "\nNOTES AND FEEDBACK " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
