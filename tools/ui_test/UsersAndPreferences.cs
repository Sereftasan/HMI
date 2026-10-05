using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Settings;

// 5 October:
// - "Move User Settings to View Preferences, they have the same content" (one tab).
// - "A Read from combo under Preferences for, to copy another user's preferences."
// - "Field caption and Axis names have the same target: one, and it should set the Axis Monitor too."
// - "Date and Time don't affect alarms and alarm history, the dashboard filters, the Shifts of
//   Logs and Reports (whose delete button should be the program's)."
// - "Attention marks don't affect the alarm and connection buttons on the top bar."
// - "A small window on the right to preview the active combo's selection."
// - "Screen scale moved here as well."
// - "Admin changes everyone's (not other admins'); a user edits and deletes the users under
//   their level and creates users at their level or lower" - and the creator keeps a
//   same-level account.
// 5 October (later):
// - "Unit Settings is still a tab: remove it, its members into Users and Preferences."
// - "The preview only while a combo is open and one of its members is hovered, removed when
//   the hovering ends; next to the open list, 50 px to its right. Read from: the hovered
//   user's preferences."
// - "The account just under the user table; the preferences on the right, top to bottom."
class UAP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static string Missing = "##missing##";
    static bool Has(string key) { return LanguageManager.GetString(key, Missing) != Missing; }
    static T Field<T>(object o, string n) { return (T)o.GetType().GetField(n, Any).GetValue(o); }
    static void Snap(Control c, int height, string path) {
        Pump(150);
        var at = c.PointToScreen(Point.Empty);
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("import",
            "-window root -crop " + c.Width + "x" + height + "+" + at.X + "+" + at.Y + " " + path) { UseShellExecute = false });
        p.WaitForExit(10000);
    }
    static void Pick(UsersPreferencesControl page, string name) {
        foreach (DataGridViewRow r in page.Users.Rows)
            if (string.Equals(r.Tag as string, name, StringComparison.OrdinalIgnoreCase)) { page.Users.ClearSelection(); r.Selected = true; }
        Pump(30);
    }
    static int IndexOf(ComboBox c, string text) {
        for (int i = 0; i < c.Items.Count; i++) if (c.Items[i].ToString() == text || c.Items[i].ToString().StartsWith(text)) return i;
        return -1;
    }
    // The lists as the operator uses them: open, the pointer on an entry, closed.
    static void Open(UsersPreferencesControl page, ComboBox c, string key) {
        typeof(UsersPreferencesControl).GetMethod("OpenList", Any).Invoke(page, new object[] { c, key }); Pump(20);
    }
    static void Hover(UsersPreferencesControl page, ComboBox c, string key, int index) {
        typeof(UsersPreferencesControl).GetMethod("HoverItem", Any).Invoke(page, new object[] { c, key, index }); Pump(20);
    }
    static void Shut(UsersPreferencesControl page, ComboBox c) {
        typeof(UsersPreferencesControl).GetMethod("CloseList", Any).Invoke(page, new object[] { c }); Pump(20);
    }
    static Rectangle OnScreen(Control c) { return c.RectangleToScreen(c.ClientRectangle); }
    static void Add(string name, SecurityLevel level, string creator) {
        UserStore.AddOrUpdateUser(name, "1", level);
        UserStore.SetCreatedBy(name, creator);
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "uap_" + Guid.NewGuid().ToString("N"));
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

        var made = new[] { "ad2", "ad3", "mt1", "mt2", "op1", "op2", "op9" };
        foreach (var n in made) UserStore.RemoveUser(n);
        Add("ad2", SecurityLevel.Admin, "admin");
        Add("ad3", SecurityLevel.Admin, "");
        Add("mt1", SecurityLevel.Maintenance, "admin");
        Add("mt2", SecurityLevel.Maintenance, "mt1");
        Add("op1", SecurityLevel.Operator, "mt1");
        Add("op2", SecurityLevel.Operator, "op1");

        Console.WriteLine("--- who may change which account ---");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        Check("Admin: Maintenance and Operators", UserRights.CanManage("mt1") && UserRights.CanManage("op2"));
        Check("  another Admin: no - unless he created it", !UserRights.CanManage("ad3") && UserRights.CanManage("ad2"));
        Check("  himself: yes, but not his level and not a delete", UserRights.CanManage("admin") && !UserRights.CanSetLevel("admin", SecurityLevel.Maintenance) &&
              !UserRights.CanDelete("admin"));
        Check("  creates any level, changes everyone's", UserRights.CanCreate(SecurityLevel.Admin) && UserRights.LevelsToGive().Count == 3 && UserRights.CanEditEveryone);
        SecurityManager.Login("mt1", SecurityLevel.Maintenance);
        Check("Maintenance: the Operators", UserRights.CanManage("op1") && UserRights.CanManage("op2") && UserRights.CanDelete("op1"));
        Check("  the Maintenance account he created, not the others", UserRights.CanManage("mt2") && !UserRights.CanManage("maintenance"));
        Check("  no Admin", !UserRights.CanManage("ad2") && !UserRights.CanManage("admin"));
        Check("  creates Operator and Maintenance, not Admin", UserRights.CanCreate(SecurityLevel.Maintenance) && !UserRights.CanCreate(SecurityLevel.Admin));
        Check("  raises an Operator to Maintenance, not to Admin", UserRights.CanSetLevel("op1", SecurityLevel.Maintenance) && !UserRights.CanSetLevel("op1", SecurityLevel.Admin));
        Check("  changes everyone's", UserRights.CanEditEveryone);
        SecurityManager.Login("op1", SecurityLevel.Operator);
        Check("Operator: the Operator he created, not another", UserRights.CanManage("op2") && UserRights.CanDelete("op2") && !UserRights.CanManage("operator"));
        Check("  nobody above", !UserRights.CanManage("mt1"));
        Check("  creates an Operator only; not everyone's", UserRights.CanCreate(SecurityLevel.Operator) && !UserRights.CanCreate(SecurityLevel.Maintenance) &&
              !UserRights.CanEditEveryone);
        SecurityManager.Logout();
        Check("signed out: nothing", !UserRights.CanManage("op2") && !UserRights.CanCreate(SecurityLevel.Operator));
        UserStore.ReloadCreators();
        Check("who created what, kept in UserCreators.cfg (beside Users.dat, not in it)", UserStore.CreatedBy("op2") == "op1" &&
              File.ReadAllText(UserStore.CreatorsPath).Contains("op2;op1"));

        Console.WriteLine("--- one tab: Users and Preferences ---");
        var tab = new UsersPreferencesPage();
        Check("for anyone signed in, in six languages", tab.TitleKey == "Set_UsersPrefs" && tab.RequiredLevel == SecurityLevel.Operator && Has("Set_UsersPrefs"));
        var hmi = (SettingsScreen)new HmiSettingsPage().GetControl();
        var titles = hmi.AllPages.Select(p => p.TitleFallback).ToList();
        Check("HMI Settings: User Settings and View Preferences are one tab", titles.Contains("Users and Preferences") &&
              !titles.Contains("User Settings") && !titles.Contains("View Preferences"), string.Join(", ", titles));
        Check("  and no Unit Settings tab (the units are on it)", !titles.Contains("Unit Settings") && titles[0] == "Users and Preferences", string.Join(", ", titles));

        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new Form { ClientSize = new Size(1260, 900), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var page = new UsersPreferencesControl { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        form.Show(); Pump(200);
        var rows = page.Users.Rows.Cast<DataGridViewRow>().ToList();
        Check("the list: Everyone first, then the accounts", rows[0].Tag as string == UsersPreferencesControl.Everyone &&
              rows.Skip(1).Any(r => r.Tag as string == "op1") && rows.Count == UserStore.GetAllUsers().Count() + 1);
        Check("  himself chosen at first", page.Selected == "admin" && page.NameBox.Text == "admin" && page.NameBox.ReadOnly);
        Check("  his own level not his to change, nor a delete", !page.LevelBox.Enabled && !page.DeleteButton.Enabled);
        Check("  the screen scale on the page", page.ScaleBox.Visible && page.ScaleBox.Text == "No specific request");
        var grid = OnScreen(page.Users);
        var acc = OnScreen(page.AccountColumn);
        var prefs = OnScreen(page.PreferencesColumn);
        Check("the user table top left, the account just under it", grid.Left == acc.Left && OnScreen(page.NameBox).Top > grid.Bottom &&
              OnScreen(page.NameBox).Top - grid.Bottom < 70 && OnScreen(page.LevelBox).Bottom < acc.Bottom &&
              OnScreen(page.NameBox).Right <= grid.Right + 2, grid + " " + OnScreen(page.NameBox));
        Check("  the preferences on the right, from the top", prefs.Left >= acc.Right && prefs.Top <= grid.Top + 2 &&
              OnScreen(page.ReadFrom).Left > grid.Right && OnScreen(page.ReadFrom).Top < OnScreen(page.ComboOf(ViewPreferences.CaptionAlign)).Top,
              prefs + " " + acc);
        Check("no preview while no list is open", !page.Preview.Visible);
        Pick(page, "ad3");
        Check("another Admin: seen, not changed (grey in the list, a note)",
              !page.ComboOf(ViewPreferences.DateFormat).Enabled && !page.PasswordBox.Enabled && !page.DeleteButton.Enabled &&
              page.NoteText.Contains("ad3") && rows.First(r => r.Tag as string == "ad3").DefaultCellStyle.ForeColor != Color.Empty, page.NoteText);
        Pick(page, "ad2");
        Check("the Admin he created: his to change", page.ComboOf(ViewPreferences.DateFormat).Enabled && page.DeleteButton.Enabled);
        Pick(page, "op1");
        Check("an Operator: every level to give", page.LevelBox.Enabled && page.LevelBox.Items.Count == 3, page.LevelBox.Items.Count.ToString());
        Check("  'created by mt1' beside the level", Field<Label>(page, "_createdBy").Text.Contains("mt1") && Field<Label>(page, "_createdBy").Visible);
        var date = page.ComboOf(ViewPreferences.DateFormat);
        Check("  each preference may follow everyone's", date.Items[0].ToString().StartsWith("As for everyone") && date.SelectedIndex == 0);
        Check("  Save waits for a change", !page.SaveButton.Enabled);
        date.Focus(); Pump(20);
        Open(page, date, ViewPreferences.DateFormat);
        Check("  its list open: the preview of the entry chosen, beside it", page.Preview.Visible && page.Preview.Key == ViewPreferences.DateFormat);
        Hover(page, date, ViewPreferences.DateFormat, IndexOf(date, "02.10.2026"));
        Check("  the pointer on 02.10.2026: an alarm row in 02.10.2026, not chosen yet", page.Preview.Value == "European" && date.SelectedIndex == 0,
              page.Preview.Key + " " + page.Preview.Value);
        var pv = OnScreen(page.Preview);
        int listRight = date.PointToScreen(Point.Empty).X + Math.Max(date.DropDownWidth, date.Width);
        Check("  50 px to the right of the list, level with it", pv.Left - listRight == 50 && Math.Abs(pv.Top - date.PointToScreen(Point.Empty).Y) <= 2,
              (pv.Left - listRight) + " px, top " + pv.Top + " / " + date.PointToScreen(Point.Empty).Y);
        Snap(page, page.Height, "/tmp/users_prefs_dates.png");
        Shut(page, date);
        Check("  the list closed: gone", !page.Preview.Visible);
        date.SelectedIndex = IndexOf(date, "02.10.2026"); Pump(20);
        Check("  a change: Save (and no preview)", page.SaveButton.Enabled && !page.Preview.Visible);
        page.SaveButton.PerformClick(); Pump(30);
        Check("  saved as op1's own", ViewPreferences.UserValue("op1", ViewPreferences.DateFormat) == "European" && !page.SaveButton.Enabled);

        Console.WriteLine("--- read from ---");
        ViewPreferences.SetMany("ad2", new Dictionary<string, string> { { ViewPreferences.TimeFormat, "H12" }, { ViewPreferences.TableRows, "Large" } });
        DisplaySettings.SetUserFactor("ad2", 1.25);
        Check("first of the preferences", page.ReadFrom.Visible && page.ReadFrom.Top < page.ComboOf(ViewPreferences.CaptionAlign).Top);
        Check("  everyone and every other user to read from", IndexOf(page.ReadFrom, "Everyone") > 0 && IndexOf(page.ReadFrom, "ad2") > 0 && IndexOf(page.ReadFrom, "op1") < 0);
        Open(page, page.ReadFrom, null);
        Hover(page, page.ReadFrom, null, IndexOf(page.ReadFrom, "ad2"));
        var lines = page.Preview.Lines;
        Func<string, PreferencePreview.Line> lineOf = n => lines.FirstOrDefault(l => l.Name == n);
        Check("  the pointer on ad2: ad2's preferences in the preview", page.Preview.Visible && page.Preview.Key == PreferencePreview.SummaryKey &&
              lineOf("Time") != null && lineOf("Time").Own && lineOf("Time").Value.StartsWith("12 hours") &&
              lineOf("Table rows").Value == "Large" && lineOf("Screen scale").Value == "125%" &&
              !lineOf("Date").Own && lineOf("Date").Value == "As for everyone" && lineOf("Length unit") != null && lineOf("Time unit") != null,
              string.Join(" | ", lines.Select(l => l.Name + "=" + l.Value + (l.Own ? "*" : ""))));
        Snap(page, page.Height, "/tmp/users_prefs_readfrom.png");
        Hover(page, page.ReadFrom, null, IndexOf(page.ReadFrom, "Everyone"));
        Check("  on Everyone: everyone's, all of them set", page.Preview.Lines.All(l => l.Own) && page.Preview.Lines.Count == lines.Count);
        Shut(page, page.ReadFrom);
        Check("  closed: gone, nothing read", !page.Preview.Visible && page.ComboOf(ViewPreferences.TimeFormat).SelectedIndex == 0);
        page.ReadFrom.SelectedIndex = IndexOf(page.ReadFrom, "ad2"); Pump(20);
        Check("ad2's into op1's page: 12 hours, large rows, 125%, the date as for everyone",
              page.ComboOf(ViewPreferences.TimeFormat).SelectedItem.ToString().StartsWith("12 hours") &&
              page.ComboOf(ViewPreferences.TableRows).SelectedItem.ToString() == "Large" &&
              page.ComboOf(ViewPreferences.DateFormat).SelectedIndex == 0 && page.ScaleBox.Text == "125%",
              page.ComboOf(ViewPreferences.TimeFormat).SelectedItem + " | " + page.ScaleBox.Text);
        Check("  not kept yet: Save", page.SaveButton.Enabled && ViewPreferences.UserValue("op1", ViewPreferences.TimeFormat) == null &&
              page.StatusText.Contains("ad2"), page.StatusText);
        page.SaveButton.PerformClick(); Pump(30);
        Check("  kept", ViewPreferences.UserValue("op1", ViewPreferences.TimeFormat) == "H12" && ViewPreferences.UserValue("op1", ViewPreferences.DateFormat) == null &&
              DisplaySettings.UserFactor("op1") == 1.25);
        page.ReadFrom.SelectedIndex = IndexOf(page.ReadFrom, "Everyone"); Pump(20);
        page.SaveButton.PerformClick(); Pump(30);
        Check("from everyone: op1 follows everyone's again", ViewPreferences.UserValue("op1", ViewPreferences.TimeFormat) == null &&
              !DisplaySettings.UserFactor("op1").HasValue);

        Console.WriteLine("--- everyone's ---");
        Pick(page, UsersPreferencesControl.Everyone);
        Check("no account, no 'as for everyone', the scale without 'no request'", !page.NameBox.Visible && !page.PasswordBox.Visible &&
              !page.ComboOf(ViewPreferences.Blinking).Items[0].ToString().StartsWith("As for") && !page.ScaleBox.OfferNone);
        Check("  the read-from list without everyone", IndexOf(page.ReadFrom, "Everyone") < 0);
        Check("  what it is: in force while nobody is signed in, followed by 'As for everyone'", page.InfoText.Contains("nobody is signed in") &&
              page.InfoText.Contains("As for everyone"), page.InfoText);
        var blink = page.ComboOf(ViewPreferences.Blinking);
        Open(page, blink, ViewPreferences.Blinking);
        Hover(page, blink, ViewPreferences.Blinking, IndexOf(blink, "Steady"));
        Check("  the preview: the top bar's marks, steady", page.Preview.Key == ViewPreferences.Blinking && page.Preview.Value == "Off" &&
              page.Preview.Description.Contains("top bar"));
        Shut(page, blink);
        blink.SelectedIndex = IndexOf(blink, "Steady"); Pump(20);
        page.SaveButton.PerformClick(); Pump(30);
        Check("  saved for everyone", ViewPreferences.PanelValue(ViewPreferences.Blinking) == "Off");

        Console.WriteLine("--- the top bar's marks follow (Attention marks) ---");
        var shell = new MainForm();
        typeof(MainForm).GetMethod("SetupAlarmIndicator", Any).Invoke(shell, null);
        typeof(MainForm).GetMethod("SetupConnectionIndicator", Any).Invoke(shell, null);
        var alarmTimer = Field<Timer>(shell, "_alarmTimer");
        var onTick = typeof(Timer).GetMethod("OnTick", Any);
        var phases = new List<bool>();
        for (int i = 0; i < 4; i++) { onTick.Invoke(alarmTimer, new object[] { EventArgs.Empty }); phases.Add(Field<bool>(shell, "_alarmPhase")); }
        Check("steady: the alarm mark stays red", phases.All(p => p), string.Join(",", phases));
        ViewPreferences.SetPanel(ViewPreferences.Blinking, null);
        phases.Clear();
        for (int i = 0; i < 4; i++) { onTick.Invoke(alarmTimer, new object[] { EventArgs.Empty }); phases.Add(Field<bool>(shell, "_alarmPhase")); }
        Check("blink: on and off", phases.Distinct().Count() == 2, string.Join(",", phases));
        Check("the connection mark has the same rule", Field<Timer>(shell, "_connTimer") != null);
        shell.Dispose();

        Console.WriteLine("--- a new user ---");
        page.NewButton.PerformClick(); Pump(30);
        Check("an empty account to fill", page.Selected == null && page.NameBox.Text == "" && !page.NameBox.ReadOnly && page.NameBox.Visible);
        page.NameBox.Text = "op9";
        page.SaveButton.PerformClick(); Pump(20);
        Check("no password: said, not made", !UserStore.GetAllUsers().Any(u => u.Username == "op9") && page.StatusText.Contains("password"), page.StatusText);
        page.PasswordBox.Text = "9";
        page.LevelBox.SelectedIndex = 0;
        page.ComboOf(ViewPreferences.TableFont).SelectedIndex = IndexOf(page.ComboOf(ViewPreferences.TableFont), "Large");
        page.SaveButton.PerformClick(); Pump(30);
        var op9 = UserStore.GetAllUsers().FirstOrDefault(u => u.Username == "op9");
        Check("made: an Operator, created by admin, with its preference", op9 != null && op9.Level == SecurityLevel.Operator &&
              UserStore.CreatedBy("op9") == "admin" && ViewPreferences.UserValue("op9", ViewPreferences.TableFont) == "Large" && page.Selected == "op9");
        page.Confirm = q => true;
        page.DeleteButton.PerformClick(); Pump(30);
        Check("deleted (asked first), its preferences and creator with it", !UserStore.GetAllUsers().Any(u => u.Username == "op9") &&
              ViewPreferences.UserValue("op9", ViewPreferences.TableFont) == null && UserStore.CreatedBy("op9") == "");

        Console.WriteLine("--- the preview ---");
        Pick(page, "op1");
        var tip = page.ComboOf(ViewPreferences.TooltipDelay);
        Open(page, tip, ViewPreferences.TooltipDelay);
        Hover(page, tip, ViewPreferences.TooltipDelay, IndexOf(tip, "Long"));
        Check("tooltips: the delay played out - 2 s", page.Preview.Key == ViewPreferences.TooltipDelay && page.Preview.TryDelay == 2000, page.Preview.TryDelay.ToString());
        Shut(page, tip);
        Check("  played out in the drawing, nothing in the preview to rest the pointer on", page.Preview.Controls.Count == 0);
        var sound = page.ComboOf(ViewPreferences.AlarmSound);
        int rang = 0;
        Kaban.UI.Helpers.AlarmSound.Play = () => rang++;
        Open(page, sound, ViewPreferences.AlarmSound);
        Check("the alarm sound: not at the list's opening", rang == 0 && page.Preview.Key == ViewPreferences.AlarmSound);
        Hover(page, sound, ViewPreferences.AlarmSound, IndexOf(sound, "On"));
        Hover(page, sound, ViewPreferences.AlarmSound, IndexOf(sound, "On"));
        Check("  the pointer onto On: heard once", rang == 1, rang.ToString());
        Hover(page, sound, ViewPreferences.AlarmSound, IndexOf(sound, "Off"));
        Check("  onto Off: not", rang == 1);
        Shut(page, sound);
        var caps = page.ComboOf(ViewPreferences.CaptionAlign);
        Open(page, caps, ViewPreferences.CaptionAlign);
        Hover(page, caps, ViewPreferences.CaptionAlign, IndexOf(caps, "Left"));
        Check("captions: one preference for the fields and the axis names", page.Preview.Key == ViewPreferences.CaptionAlign &&
              page.Preview.Description.Contains("Axis Monitor") && ViewPreferences.All.All(p => p.Key != "AxisNameAlign"));
        Snap(page, page.Height, "/tmp/users_prefs_captions.png");
        Shut(page, caps);
        page.ScaleBox.PerformClick(); Pump(60);
        var menu = page.ScaleBox.OpenMenu;
        var size125 = menu == null ? null : menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Text == "125%");
        if (size125 != null) typeof(ToolStripItem).GetMethod("OnMouseEnter", Any).Invoke(size125, new object[] { EventArgs.Empty });
        Pump(20);
        Check("the screen scale: its menu's size under the pointer", menu != null && page.Preview.Visible && page.Preview.Key == UsersPreferencesControl.ScaleKey &&
              page.Preview.Value == "125%", page.Preview.Value);
        if (menu != null) menu.Close();
        Pump(30);
        Check("  the menu closed: gone", !page.Preview.Visible);

        Console.WriteLine("--- the units of measure ---");
        var length = page.ComboOf(UnitManager.UnitKey(UnitType.Length));
        Check("one for each family, under Units of measure", length != null && UnitManager.Families.All(t => page.ComboOf(UnitManager.UnitKey(t)) != null) &&
              length.Items[0].ToString().StartsWith("As for everyone") && IndexOf(length, "inch") > 0);
        Open(page, length, UnitManager.UnitKey(UnitType.Length));
        Hover(page, length, UnitManager.UnitKey(UnitType.Length), IndexOf(length, "inch"));
        Check("  the preview: a value in inches, what the PLC keeps", page.Preview.Key == "Unit_Length" && page.Preview.Value == "inch" &&
              page.Preview.Description.Contains("PLC"), page.Preview.Description);
        Snap(page, page.Height, "/tmp/users_prefs_units.png");
        Shut(page, length);
        page.Fill(); Pump(20);
        Pick(page, UsersPreferencesControl.Everyone);
        length = page.ComboOf(UnitManager.UnitKey(UnitType.Length));
        length.SelectedIndex = IndexOf(length, "inch"); Pump(20);
        page.SaveButton.PerformClick(); Pump(30);
        Check("everyone's: the panel's unit (as Unit Settings did) - in force for admin, who has none of his own",
              UnitManager.PanelUnitName(UnitType.Length) == "inch" && UnitManager.GetActive(UnitType.Length).Name == "inch");
        Pick(page, "op1");
        length = page.ComboOf(UnitManager.UnitKey(UnitType.Length));
        length.SelectedIndex = IndexOf(length, "cm"); Pump(20);
        page.SaveButton.PerformClick(); Pump(30);
        Check("op1's own: cm, kept with his preferences", ViewPreferences.UserValue("op1", "Unit_Length") == "cm" &&
              UnitManager.GetActive(UnitType.Length).Name == "inch");
        int told = 0;
        EventHandler count = (s, e) => told++;
        UnitManager.UnitChanged += count;
        SecurityManager.Login("op1", SecurityLevel.Operator); Pump(50);
        Check("  op1 signs in: cm, the pages told", UnitManager.GetActive(UnitType.Length).Name == "cm" && told > 0);
        SecurityManager.Login("admin", SecurityLevel.Admin); Pump(50);
        Check("  another signs in: the panel's inch again", UnitManager.GetActive(UnitType.Length).Name == "inch");
        UnitManager.UnitChanged -= count;
        ViewPreferences.SetUser("op1", "Unit_Length", null);
        UnitManager.SetActive(UnitType.Length, "mm");
        Check("  back to mm", UnitManager.GetActive(UnitType.Length).Name == "mm");
        page.Fill(); Pump(20);

        Console.WriteLine("--- an Operator on the page ---");
        SecurityManager.Login("op1", SecurityLevel.Operator);
        Pump(100);
        Check("himself first, his to change, an Operator to make", page.Selected == "op1" && page.ComboOf(ViewPreferences.DateFormat).Enabled &&
              page.NewButton.Enabled && !page.LevelBox.Enabled);
        Pick(page, "op2");
        Check("the Operator he created: his", page.ComboOf(ViewPreferences.DateFormat).Enabled && page.PasswordBox.Enabled && page.DeleteButton.Enabled);
        Pick(page, "operator");
        Check("another Operator: seen only", !page.ComboOf(ViewPreferences.DateFormat).Enabled && !page.DeleteButton.Enabled);
        Pick(page, UsersPreferencesControl.Everyone);
        Check("everyone's: seen only, Maintenance changes it", !page.ComboOf(ViewPreferences.DateFormat).Enabled && page.NoteText.Contains("Maintenance"), page.NoteText);
        page.NewButton.PerformClick(); Pump(20);
        Check("a new one: Operator only", page.LevelBox.Items.Count == 1 && page.LevelBox.Items[0].ToString() == "Operator");
        SecurityManager.Login("mt1", SecurityLevel.Maintenance);
        Pump(100);
        Pick(page, UsersPreferencesControl.Everyone);
        Check("Maintenance: everyone's to change", page.ComboOf(ViewPreferences.DateFormat).Enabled);
        form.Close();

        Console.WriteLine("--- the Axis Monitor's names ---");
        var row = new Kaban.UI.Screens.AxisMonitorRow(new Kaban.UI.Helpers.AlarmHelpLinks.AxisInfo { NameKey = "Robot Y", Position = "W0", Divisor = 1 });
        Check("right by default", row.NameLabel.TextAlign == ContentAlignment.MiddleRight);
        ViewPreferences.SetPanel(ViewPreferences.CaptionAlign, "Left");
        Check("  left with the captions", row.NameLabel.TextAlign == ContentAlignment.MiddleLeft);
        ViewPreferences.SetPanel(ViewPreferences.CaptionAlign, null);
        row.Dispose();

        Console.WriteLine("--- dates and times where they were missed ---");
        var alarms = new Kaban.UI.Screens.MachineAlarmScreen(null);
        var aGrid = Field<DataGridView>(alarms, "_grid");
        Check("the alarm list's time: as View Preferences say", aGrid.Columns["TriggerTime"].DefaultCellStyle.Format == "yyyy-MM-dd HH:mm:ss",
              aGrid.Columns["TriggerTime"].DefaultCellStyle.Format);
        ViewPreferences.SetMany(null, new Dictionary<string, string> { { ViewPreferences.DateFormat, "European" }, { ViewPreferences.TimeFormat, "H12" } });
        Check("  and the open list follows", aGrid.Columns["TriggerTime"].DefaultCellStyle.Format == "dd.MM.yyyy h:mm:ss tt",
              aGrid.Columns["TriggerTime"].DefaultCellStyle.Format);
        var item = new AlarmItem("T_HIST", "x", AlarmSeverity.Fault) { TriggerTime = new DateTime(2026, 10, 2, 14, 31, 7) };
        AlarmManager.RaiseGenerated(item);
        var history = (UserControl)typeof(Kaban.UI.Screens.MachineAlarmScreen).GetMethod("BuildHistory", Any).Invoke(null, new object[] { null });
        var hGrid = history.Controls.OfType<DataGridView>().First();
        Func<string> first = () => hGrid.Rows.Cast<DataGridViewRow>().Select(r => (string)r.Cells[0].Value).FirstOrDefault(v => v != null && v.Contains("2026"));
        Check("the history: 02.10.2026 2:31:07 PM", first() == "02.10.2026 2:31:07 PM", first());
        ViewPreferences.SetMany(null, new Dictionary<string, string> { { ViewPreferences.DateFormat, null }, { ViewPreferences.TimeFormat, null } });
        Check("  written again when they change", first() == "2026-10-02 14:31:07", first());
        AlarmManager.ClearGenerated(item);
        history.Dispose();
        ViewPreferences.SetPanel(ViewPreferences.TimeFormat, "H12");
        var shift = new Kaban.UI.Dashboard.DashboardConfig.Shift { Start = TimeSpan.FromHours(6), End = TimeSpan.FromHours(14) };
        Check("a shift in the dashboard's filter: 6:00 AM - 2:00 PM (its key stays 06:00 - 14:00)", shift.Shown == "6:00 AM - 2:00 PM" && shift.Text == "06:00 - 14:00", shift.Shown);
        var settings = new Kaban.UI.Dashboard.DashboardSettingsControl(new Kaban.UI.Dashboard.MachineDashboard());
        settings.AddShift(TimeSpan.FromHours(6), TimeSpan.FromHours(14));
        var pickers = settings.ShiftPickers(0);
        Check("Logs and Reports' shifts: 12 hours", pickers[0].CustomFormat == "h:mm tt", pickers[0].CustomFormat);
        ViewPreferences.SetPanel(ViewPreferences.TimeFormat, null);
        Check("  back to 24 hours, at once", pickers[0].CustomFormat == "HH:mm", pickers[0].CustomFormat);
        var del = pickers[0].Parent.Controls.OfType<Button>().FirstOrDefault();
        Check("  the shift's delete button: the program's bin", del != null && del.Text == "\uD83D\uDDD1", del == null ? "none" : del.Text);
        var logs = new LogViewerControl(null, LogViewerControl.Period.Today);
        var from = Field<DateTimePicker>(logs, "_from");
        ViewPreferences.SetPanel(ViewPreferences.DateFormat, "Us");
        Check("the log's pickers follow too", from.CustomFormat.StartsWith("MM'/'dd'/'yyyy"), from.CustomFormat);
        ViewPreferences.SetPanel(ViewPreferences.DateFormat, null);

        Console.WriteLine("--- signed in without an account here ---");
        SecurityManager.Login("ghost", SecurityLevel.Admin);
        UsersPreferencesControl ghostPage = null;
        try { ghostPage = new UsersPreferencesControl(); } catch (Exception ex) { Console.WriteLine(ex); }
        Check("the page opens (it is HMI Settings' first tab now), on Everyone", ghostPage != null && ghostPage.Selected == UsersPreferencesControl.Everyone);
        if (ghostPage != null) ghostPage.Dispose();

        Console.WriteLine("--- translations ---");
        var keys = new[] { "Usr_SecAccount", "Usr_SecPrefs", "VP_ReadFrom", "Tip_ReadFrom", "VP_SecScreen", "Usr_PageNote", "Usr_NewUser", "Usr_NoteEveryone",
                           "Usr_NoteNotYours", "Usr_CreatedBy", "Usr_ReadDone", "Usr_MsgCreated", "Usr_MsgSaved", "Usr_MsgDeleted", "Lvl_Operator", "Lvl_Maintenance",
                           "Lvl_Admin", "PV_Title", "PV_Captions", "PV_Dates", "PV_Blink", "PV_Tooltip", "PV_Scale", "VP_CaptionAlign", "Tip_UsrNew", "Tip_UsrPrefSave",
                           "VP_SecUnits", "Usr_InfoEveryone", "PV_SoundHeard", "PV_SummaryTitle", "PV_SummaryHint", "PV_Unit", "PV_UnitPlc", "PV_UnitOf", "Unit_Family_Length", "Unit_Family_Time" };
        Check("every text of the page in the file", keys.All(Has), string.Join(",", keys.Where(k => !Has(k))));
        Check("  the combined caption's name", LanguageManager.GetString("VP_CaptionAlign", "") == "Captions and axis names");

        SecurityManager.Logout();
        foreach (var n in made) UserStore.RemoveUser(n);
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nUSERS AND PREFERENCES ALL PASS" : "\nUSERS AND PREFERENCES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
