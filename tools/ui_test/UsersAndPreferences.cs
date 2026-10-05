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
        date.SelectedIndex = IndexOf(date, "02.10.2026");
        Check("  a change: Save", page.SaveButton.Enabled);
        Check("  and the preview shows it: an alarm row in 02.10.2026", page.Preview.Key == ViewPreferences.DateFormat && page.Preview.Value == "European",
              page.Preview.Key + " " + page.Preview.Value);
        Snap(page, page.Height, "/tmp/users_prefs_dates.png");
        page.SaveButton.PerformClick(); Pump(30);
        Check("  saved as op1's own", ViewPreferences.UserValue("op1", ViewPreferences.DateFormat) == "European" && !page.SaveButton.Enabled);

        Console.WriteLine("--- read from ---");
        ViewPreferences.SetMany("ad2", new Dictionary<string, string> { { ViewPreferences.TimeFormat, "H12" }, { ViewPreferences.TableRows, "Large" } });
        DisplaySettings.SetUserFactor("ad2", 1.25);
        Check("under the account, before the preferences", page.ReadFrom.Visible && page.ReadFrom.Top < page.ComboOf(ViewPreferences.CaptionAlign).Top &&
              page.ReadFrom.Top > page.LevelBox.Top);
        Check("  everyone and every other user to read from", IndexOf(page.ReadFrom, "Everyone") > 0 && IndexOf(page.ReadFrom, "ad2") > 0 && IndexOf(page.ReadFrom, "op1") < 0);
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
        var blink = page.ComboOf(ViewPreferences.Blinking);
        blink.SelectedIndex = IndexOf(blink, "Steady"); Pump(20);
        Check("  the preview: the top bar's marks, steady", page.Preview.Key == ViewPreferences.Blinking && page.Preview.Value == "Off" &&
              page.Preview.Description.Contains("top bar"));
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
        tip.Focus(); Pump(20);
        tip.SelectedIndex = IndexOf(tip, "Long"); Pump(20);
        Check("tooltips: try it on the box - 2 s", page.Preview.Key == ViewPreferences.TooltipDelay && page.Preview.TryDelay == 2000, page.Preview.TryDelay.ToString());
        var sound = page.ComboOf(ViewPreferences.AlarmSound);
        sound.Focus(); Pump(20);
        var play = page.Preview.Controls.OfType<Button>().FirstOrDefault();
        int rang = 0;
        Kaban.UI.Helpers.AlarmSound.Play = () => rang++;
        Check("the alarm sound: a button to hear it", page.Preview.Key == ViewPreferences.AlarmSound && play != null && play.Visible);
        play.PerformClick();
        Check("  it plays", rang == 1);
        var caps = page.ComboOf(ViewPreferences.CaptionAlign);
        caps.Focus(); Pump(20);
        caps.SelectedIndex = IndexOf(caps, "Left"); Pump(20);
        Check("captions: one preference for the fields and the axis names", page.Preview.Key == ViewPreferences.CaptionAlign &&
              page.Preview.Description.Contains("Axis Monitor") && ViewPreferences.All.All(p => p.Key != "AxisNameAlign"));
        Snap(page, page.Height, "/tmp/users_prefs_captions.png");
        page.ScaleBox.Focus(); Pump(20);
        Check("the screen scale too", page.Preview.Key == UsersPreferencesControl.ScaleKey);
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

        Console.WriteLine("--- translations ---");
        var keys = new[] { "Usr_SecAccount", "Usr_SecPrefs", "VP_ReadFrom", "Tip_ReadFrom", "VP_SecScreen", "Usr_PageNote", "Usr_NewUser", "Usr_NoteEveryone",
                           "Usr_NoteNotYours", "Usr_CreatedBy", "Usr_ReadDone", "Usr_MsgCreated", "Usr_MsgSaved", "Usr_MsgDeleted", "Lvl_Operator", "Lvl_Maintenance",
                           "Lvl_Admin", "PV_Title", "PV_Captions", "PV_Dates", "PV_Blink", "PV_Tooltip", "PV_Scale", "VP_CaptionAlign", "Tip_UsrNew", "Tip_UsrPrefSave" };
        Check("every text of the page in the file", keys.All(Has), string.Join(",", keys.Where(k => !Has(k))));
        Check("  the combined caption's name", LanguageManager.GetString("VP_CaptionAlign", "") == "Captions and axis names");

        SecurityManager.Logout();
        foreach (var n in made) UserStore.RemoveUser(n);
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nUSERS AND PREFERENCES ALL PASS" : "\nUSERS AND PREFERENCES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
