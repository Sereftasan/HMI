using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 12 October: "I also want you to add language selection to the preferences as well (under 'Screen'
// category) so the selection turns to the user's preference if it is selected (just like 'Screen scale')".
// A user's own language is put on the screen when they sign in (at once - no restart), everyone's again
// when they sign out; everyone's is the flag's on the top bar, kept now (a restart came back in English).
class LP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Pick(UsersPreferencesControl page, string name) {
        foreach (DataGridViewRow r in page.Users.Rows)
            if (string.Equals(r.Tag as string, name, StringComparison.OrdinalIgnoreCase)) { page.Users.ClearSelection(); r.Selected = true; }
        Pump(30);
    }
    static int IndexOf(ComboBox c, string text) {
        for (int i = 0; i < c.Items.Count; i++) if (c.Items[i].ToString().StartsWith(text)) return i;
        return -1;
    }
    static string Lang { get { return LanguageManager.CurrentLanguage; } }
    static UsersPreferencesControl _page;
    static bool Save() { return (bool)typeof(UsersPreferencesControl).GetMethod("Save", Any).Invoke(_page, null); }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "lp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        ViewPreferences.Reload();
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        DisplaySettings.Reload();
        UserStore.CreatorsPath = Path.Combine(dir, "UserCreators.cfg");
        UserStore.ReloadCreators();
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        SecurityManager.Logout();
        foreach (var n in new[] { "lpop", "lpmt" }) UserStore.RemoveUser(n);
        UserStore.AddOrUpdateUser("lpop", "1", SecurityLevel.Operator); UserStore.SetCreatedBy("lpop", "admin");
        UserStore.AddOrUpdateUser("lpmt", "1", SecurityLevel.Maintenance); UserStore.SetCreatedBy("lpmt", "admin");
        LanguageManager.ChangeLanguage("EN");
        UserLanguage.Attach();                          // as the main window does once the texts are read
        Check("started: nothing chosen yet - the screen keeps its language", Lang == "EN" && ViewPreferences.PanelChosen(ViewPreferences.Language) == null);

        Console.WriteLine("--- the page: Screen > Screen scale, Language ---");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new Form { ClientSize = new Size(1200, 800), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var page = new UsersPreferencesControl();
        _page = page;
        form.Controls.Add(page);
        form.Show(); Pump(300);
        var lang = page.ComboOf(ViewPreferences.Language);
        Check("a Language list on the page", lang != null);
        var caps = page.PreferencesColumn.Controls.OfType<Label>().ToList();
        var screenHead = caps.FirstOrDefault(l => l.Text == "Screen");
        var langCap = caps.FirstOrDefault(l => l.Text.StartsWith("Language") && Math.Abs(l.Top - lang.Top) < 8);
        Check("  under Screen, right below the screen scale - one Screen heading", screenHead != null && langCap != null &&
              lang.Top > page.ScaleBox.Top && lang.Top - page.ScaleBox.Top < 50 && screenHead.Top < page.ScaleBox.Top &&
              caps.Count(l => l.Text == "Screen") == 1 &&
              !page.PreferencesColumn.Controls.OfType<Control>().Any(c => c != lang && c != page.ScaleBox && c.Top > page.ScaleBox.Top && c.Top < lang.Top - 4 && !(c is Label)),
              "scale " + page.ScaleBox.Top + ", language " + lang.Top + (screenHead == null ? ", no heading" : ", heading " + screenHead.Top) +
              (langCap == null ? ", no caption" : "") + ", Screen headings " + caps.Count(l => l.Text == "Screen"));
        if (Environment.GetEnvironmentVariable("SHOT_DIR") != null) {
            Pump(150);
            var at = form.PointToScreen(Point.Empty);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("import", "-window root -crop " + form.ClientSize.Width + "x500+" + at.X + "+" + at.Y + " " +
                Path.Combine(Environment.GetEnvironmentVariable("SHOT_DIR"), "language_page.png")) { UseShellExecute = false }).WaitForExit(10000);
        }
        Pick(page, UsersPreferencesControl.Everyone);
        var entries = lang.Items.Cast<object>().Select(o => o.ToString()).ToList();
        Check("everyone's: the languages read, each by its own name - no 'As for everyone'",
              entries.Count == LanguageManager.AvailableLanguages.Count && entries.Contains("TR  Türkçe") && entries.Contains("DE  Deutsch") &&
              !entries.Any(e => e.StartsWith("As for everyone")) && lang.SelectedItem.ToString().StartsWith("EN"), string.Join(" | ", entries));
        Pick(page, "lpop");
        Check("a user's: 'As for everyone (EN English)' first, chosen", lang.Items[0].ToString() == "As for everyone  (EN  English)" && lang.SelectedIndex == 0,
              lang.Items[0].ToString());

        Console.WriteLine("--- an Operator's own language ---");
        lang.SelectedIndex = IndexOf(lang, "TR");
        Check("  kept for him", Save() && ViewPreferences.UserValue("lpop", ViewPreferences.Language) == "TR");
        Check("  the Admin signed in, not him: the screen stays English", Lang == "EN");
        SecurityManager.Login("lpop", SecurityLevel.Operator); Pump(50);
        Check("he signs in: the screen in Turkish at once", Lang == "TR");
        SecurityManager.Logout(); Pump(50);
        Check("he signs out: back to English (the one before his)", Lang == "EN");
        SecurityManager.Login("lpmt", SecurityLevel.Maintenance); Pump(50);
        Check("one with none of his own signs in: English stays", Lang == "EN");

        Console.WriteLine("--- the flag on the top bar: everyone's ---");
        UserLanguage.ChooseForPanel("de");
        Check("German chosen: on the screen, kept as everyone's", Lang == "DE" && ViewPreferences.PanelChosen(ViewPreferences.Language) == "DE" &&
              File.ReadAllText(ViewPreferences.SettingsPath).Contains("Language=DE"));
        SecurityManager.Logout(); Pump(50);
        Check("  signed out: German (everyone's)", Lang == "DE");
        SecurityManager.Login("lpop", SecurityLevel.Operator); Pump(50);
        Check("  the Operator: his own Turkish", Lang == "TR");
        SecurityManager.Logout(); Pump(50);
        Check("  and German again after him", Lang == "DE");
        ViewPreferences.Reload();
        LanguageManager.ChangeLanguage("EN");
        UserLanguage.Apply();
        Check("  kept over a restart (View.cfg read again)", Lang == "DE");

        Console.WriteLine("--- your own, changed on the page: at once ---");
        SecurityManager.Login("lpop", SecurityLevel.Operator); Pump(100);
        Check("the Operator's page shows himself, Turkish", page.Selected == "lpop" && lang.SelectedItem.ToString().StartsWith("TR"), page.Selected + " " + lang.SelectedItem);
        lang.SelectedIndex = IndexOf(lang, "IT");
        Check("  Italian kept: the screen Italian at once", Save() && Lang == "IT");
        lang.SelectedIndex = 0;
        Check("  'As for everyone' kept: everyone's German at once", Save() && Lang == "DE" && ViewPreferences.UserValue("lpop", ViewPreferences.Language) == null);
        SecurityManager.Login("admin", SecurityLevel.Admin); Pump(100);
        Pick(page, UsersPreferencesControl.Everyone);
        Check("everyone's on the page: German", lang.SelectedItem.ToString().StartsWith("DE"));
        lang.SelectedIndex = IndexOf(lang, "ES");
        Check("  Spanish kept for everyone: the Admin follows everyone's - Spanish at once", Save() && Lang == "ES" &&
              ViewPreferences.PanelChosen(ViewPreferences.Language) == "ES");

        Console.WriteLine("--- read from, the preview ---");
        ViewPreferences.SetUser("lpmt", ViewPreferences.Language, "RU");
        Pick(page, "lpop");
        var read = page.ReadFrom;
        read.SelectedIndex = IndexOf(read, "lpmt");
        Pump(50);
        Check("read from the Maintenance user: his Russian into the page", lang.SelectedItem.ToString().StartsWith("RU"), lang.SelectedItem.ToString());
        typeof(UsersPreferencesControl).GetMethod("OpenList", Any).Invoke(page, new object[] { lang, ViewPreferences.Language });
        typeof(UsersPreferencesControl).GetMethod("HoverItem", Any).Invoke(page, new object[] { lang, ViewPreferences.Language, IndexOf(lang, "TR") });
        Pump(100);
        Check("the list open, Turkish under the pointer: its preview beside it", page.Preview.Visible && page.Preview.Key == ViewPreferences.Language);
        var shot = Path.Combine(Environment.GetEnvironmentVariable("SHOT_DIR") ?? dir, "language_preview.png");
        using (var bmp = new Bitmap(page.Preview.Width, page.Preview.Height)) { page.Preview.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size)); bmp.Save(shot); }
        Console.WriteLine("    (" + shot + ")");
        typeof(UsersPreferencesControl).GetMethod("CloseList", Any).Invoke(page, new object[] { lang });

        form.Close();
        foreach (var n in new[] { "lpop", "lpmt" }) UserStore.RemoveUser(n);
        SecurityManager.Logout();
        Console.WriteLine(fail == 0 ? "\nLANGUAGE PREFERENCE ALL PASS" : "\nLANGUAGE PREFERENCE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
