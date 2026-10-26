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

// 12 October, the panel's audit log (10:24:27): "UNHANDLED background EXCEPTION:
// ArgumentOutOfRangeException ... DataGridView.PositionEditingControl ... DataGridView.Dispose ...
// NotesPage.Fill ... Pick ... Picker.PreFilterMessage" and the HMI restarted. A suggestion was being
// typed in the notes page's texts table (it opens its editing box at once) when another control was
// tapped: the page filled again and threw the table away with its editing box still open - .NET's
// table falls over on that. Its editing is now ended first, and the page goes on.
class NEP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }
    static bool Tap(NotesPage page, Control c, int msg) {
        var picker = typeof(NotesPage).GetField("_picker", Any).GetValue(page) as IMessageFilter;
        var m = Message.Create(c.Handle, msg, IntPtr.Zero, IntPtr.Zero);
        return picker.PreFilterMessage(ref m);
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "nep_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        NotesStore.Folder = Path.Combine(dir, "Notes");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        NotesPage.FeedbackFolder = Path.Combine(dir, "Feedback");
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.AddTranslation("EN", "Btn_TestStart", "Start");
        LanguageManager.AddTranslation("EN", "Btn_TestStop", "Stop");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        int unhandled = 0;
        Application.ThreadException += (s, e) => { unhandled++; Console.WriteLine("    thread exception: " + e.Exception.GetType().Name + " " + e.Exception.Message); };

        var form = new Form { ClientSize = new Size(1200, 760), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0), BackColor = Color.White };
        var machine = new Panel { Bounds = new Rectangle(0, 0, 660, 760), BackColor = Color.White };
        form.Controls.Add(machine);
        var start = new ButtonHMI { Location = new Point(30, 30), Size = new Size(140, 50), TranslationKey = "Btn_TestStart", FallbackText = "Start", Name = "btnStart" };
        var stop = new ButtonHMI { Location = new Point(30, 100), Size = new Size(140, 50), TranslationKey = "Btn_TestStop", FallbackText = "Stop", Name = "btnStop" };
        machine.Controls.Add(start); machine.Controls.Add(stop);
        var host = new Panel { Bounds = new Rectangle(660, 0, 540, 760) };
        form.Controls.Add(host);
        var notes = new NotesPage(c => c != null && machine.Contains(c), c => "Test machine / Page", () => "TEST", c => false);
        host.Controls.Add(notes);
        form.Show(); Pump(400);
        var body = (Control)typeof(NotesPage).GetProperty("Body", Any).GetValue(notes, null);
        Func<DataGridView> texts = () => Tree(body).OfType<DataGridView>().FirstOrDefault(g => g.Name == "notesTexts");
        // 26 October: picking with Add; what was typed thrown away when the user says so
        Action add = () => typeof(NotesPage).GetMethod("SetPicking", Any).Invoke(notes, new object[] { true });
        Kaban.UI.Profiles.Ask.Hook = (t, q, i) => "Discard it";

        add(); Tap(notes, start, 0x0201); Tap(notes, start, 0x0202); Pump(100);
        var grid = texts();
        Check("Start picked: its texts table", notes.PickedName == "Start" && grid != null, notes.PickedName ?? "");
        grid.Focus();
        grid.CurrentCell = grid.Rows[0].Cells[2];
        grid.BeginEdit(true);
        Pump(100);
        var box = grid.EditingControl as TextBox;
        if (box != null) box.Text = "Begin";
        Pump(50);
        Check("  a suggestion being typed: its editing box open", grid.IsCurrentCellInEditMode && box != null);
        // the moment it leaves the page (before it is thrown away): typing no more
        bool editingAtRemoval = false, removed = false;
        var old = grid;
        old.ParentChanged += (s, e) => { if (old.Parent == null && !removed) { removed = true; editingAtRemoval = old.IsCurrentCellInEditMode || old.EditingControl != null; } };

        bool threw = false; string what = "";
        try { add(); Tap(notes, stop, 0x0201); Tap(notes, stop, 0x0202); Pump(200); }
        catch (Exception ex) { threw = true; what = ex.GetType().Name + ": " + ex.Message; }
        Check("another control tapped then: no exception, the page goes on", !threw && unhandled == 0, what);
        Check("  Stop picked, its own texts table", notes.PickedName == "Stop" && texts() != null && texts() != grid &&
              Convert.ToString(texts().Rows[0].Cells[1].Value) == "Stop", notes.PickedName ?? "");
        Check("  the old table: its editing ended before it was taken off the page and thrown away", removed && !editingAtRemoval && grid.IsDisposed,
              "removed " + removed + ", editing then " + editingAtRemoval);

        // the same through every way the page fills again: a general note, a cell of a table
        grid = texts();
        grid.Focus();
        grid.CurrentCell = grid.Rows[0].Cells[2];
        grid.BeginEdit(true);
        Pump(50);
        threw = false;
        try { notes.PickGeneral(); Pump(100); }
        catch (Exception ex) { threw = true; what = ex.GetType().Name + ": " + ex.Message; }
        Check("typing, then the general note: no exception", !threw && unhandled == 0 && grid.IsDisposed, what);

        form.Close();
        Console.WriteLine(fail == 0 ? "\nNOTES EDIT PICK ALL PASS" : "\nNOTES EDIT PICK " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
