using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Forms;
using Kaban.UI.Settings;

// The screen scale on the caption bar, between the user and the language: the
// panel's scale, whoever is signed in; a user's own request only on User
// Settings (the same menu, "No specific request"), never overwritten from the
// caption bar.
class ZM {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        for (var t = o.GetType(); t != null; t = t.BaseType) {
            var f = t.GetField(n, Any); if (f != null) return f.GetValue(o);
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o, null);
        }
        return null;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static IEnumerable<Control> All(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; } }
    static ContextMenuStrip Menu(MainForm f) { return (ContextMenuStrip)typeof(MainForm).GetMethod("BuildZoomMenu", Any).Invoke(f, null); }
    static string Checked(ToolStripItemCollection items) {
        return string.Join(",", items.OfType<ToolStripMenuItem>().Where(i => i.Checked).Select(i => i.Text));
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "zm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        DisplaySettings.Reload();

        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show();
        Pump(300);
        string asked = null;
        typeof(MainForm).GetField("ConfirmZoomRestart", Any).SetValue(form, (Func<string, bool>)(q => { asked = q; return false; }));

        Console.WriteLine("--- on the caption bar ---");
        var zoom = (Button)Get(form, "_btnZoom");
        var login = (Button)Get(form, "_btnLogin");
        var lang = (Button)Get(form, "_btnLang");
        Check("the glass sits between the user and the language", zoom != null && zoom.Visible && login.Right < zoom.Left && zoom.Right < lang.Left,
              zoom == null ? "none" : login.Bounds + " | " + zoom.Bounds + " | " + lang.Bounds);

        Console.WriteLine("--- signed in as Admin ---");
        var menu = Menu(form);
        var heading = menu.Items.OfType<ToolStripLabel>().Select(l => l.Text).ToList();
        Check("one heading, 'Screen scale' with the size on screen at its end: 'Screen scale (100%)'", heading.Count == 1 && heading[0] == "Screen scale (100%)",
              string.Join(" | ", heading));
        var factors = menu.Items.OfType<ToolStripMenuItem>().Select(i => i.Text).ToList();
        Check("percentages 80% to 150% - and nothing else in the list", string.Join(" ", factors) == "80% 90% 100% 110% 120% 125% 130% 140% 150%", string.Join(" ", factors));
        Check("  the screen's size ticked", Checked(menu.Items) == "100%", Checked(menu.Items));
        Check("  no 'For everyone', no 'The panel's', no 'Your own' line",
              !menu.Items.OfType<ToolStripItem>().Any(i => (i.Text ?? "").Contains("everyone") || (i.Text ?? "").Contains("panel's") || (i.Text ?? "").StartsWith("Your own")));
        Check("the glass is named 'Screen scale'", zoom.AccessibleName == "Screen scale");
        var host = menu.Items.OfType<ToolStripControlHost>().FirstOrDefault();
        var stepper = host == null ? null : host.Control;
        Check("a row to set any percent: - 100% + and a tick", stepper != null && stepper.Controls.OfType<Button>().Count() == 3 &&
              stepper.Controls.OfType<Label>().Any(l => l.Text == "100%"));
        if (stepper != null) {
            var step = stepper.GetType().GetMethod("Step");
            for (int i = 0; i < 37; i++) step.Invoke(stepper, new object[] { 1 });
            Check("  1% at a time: 137%", stepper.Controls.OfType<Label>().First().Text == "137%");
            for (int i = 0; i < 40; i++) step.Invoke(stepper, new object[] { 1 });
            Check("  never above 150%", stepper.Controls.OfType<Label>().First().Text == "150%");
            for (int i = 0; i < 20; i++) step.Invoke(stepper, new object[] { -1 });
            menu.Show(form, 0, 0); Pump(50);
            stepper.Controls.OfType<Button>().First(b => b.AccessibleName == "Apply").PerformClick();
            Pump(50);
            Check("  the tick keeps 130% as the PANEL'S scale", DisplaySettings.PanelFactor == 1.30 && DisplaySettings.StartFactor == 1.30,
                  DisplaySettings.PanelFactor + " / " + DisplaySettings.StartFactor);
            Check("  and leaves admin's own request alone (there is none)", !DisplaySettings.UserFactor("admin").HasValue);
            menu.Close(); menu.Dispose(); Pump(50);
        }

        DisplaySettings.SetUserFactor("admin", 1.2);
        form.ChooseFactor(1.37);
        Check("choosing 137% makes it the panel's and the start scale", DisplaySettings.PanelFactor == 1.37 && DisplaySettings.StartFactor == 1.37);
        Check("  a signed-in user's own request is NOT changed by it (120% stays)", DisplaySettings.UserFactor("admin") == 1.2, "" + DisplaySettings.UserFactor("admin"));
        Check("  and offers the restart in percent (declined here: nothing restarts)", asked != null && asked.Contains("137%") && asked.Contains("screen scale") && !form.IsDisposed, asked ?? "not asked");
        Check("  the file keeps it: Panel=1.37, Start=1.37, admin's 1.2", File.ReadAllText(Path.Combine(dir, "Display.cfg")).Contains("Panel=1.37") &&
              File.ReadAllText(Path.Combine(dir, "Display.cfg")).Contains("User=admin;1.2"));
        menu = Menu(form);
        Check("  no preset ticked for 137% (the row shows it)", Checked(menu.Items) == "" &&
              menu.Items.OfType<ToolStripControlHost>().First().Control.Controls.OfType<Label>().First().Text == "137%");
        Check("percent typed: '137%', '137' and '1.37' are all 1.37", DisplaySettings.Parse("137%") == 1.37 && DisplaySettings.Parse("137") == 1.37 &&
              DisplaySettings.Parse("1.37") == 1.37 && DisplaySettings.Parse("160%") == null);

        Console.WriteLine("--- nobody signed in: the same ---");
        SecurityManager.Logout();
        form.ChooseFactor(0.9);
        Check("choosing sets the panel's scale (and the start)", DisplaySettings.PanelFactor == 0.9 && DisplaySettings.StartFactor == 0.9 &&
              DisplaySettings.UserFactor("admin") == 1.2);

        Console.WriteLine("--- every restart keeps the user signed in ---");
        typeof(Kaban.UI.Helpers.LoginHandoff).GetField("Folder", Any).SetValue(null, Path.Combine(dir, "handoff"));
        var args = (string)typeof(MainForm).GetMethod("RestartArguments", Any).Invoke(form, new object[] { null });
        Check("signed out: no hand-over", !args.Contains("--handoff") && args.StartsWith("--await-exit "), args);
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        args = (string)typeof(MainForm).GetMethod("RestartArguments", Any).Invoke(form, new object[] { null });
        string user;
        Check("signed in: the close menu's Restart hands the user over", args.Contains("--handoff") &&
              Kaban.UI.Helpers.LoginHandoff.TryConsume(args.Split(' '), out user) && user == "maint", args);
        Check("  a note lives two minutes", Kaban.UI.Helpers.LoginHandoff.Lifetime == TimeSpan.FromMinutes(2));

        Console.WriteLine("--- Users and Preferences: the user's own request ---");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var users = new UsersPreferencesControl();
        var texts = All(users).OfType<Label>().Select(l => l.Text).ToList();
        Check("no explanation on the page (no panel block, no note)", !texts.Any(t => t.Contains("of this panel") || t.StartsWith("For everyone") ||
              t.StartsWith("The user's own size") || t.StartsWith("0.8 to 1.5")), string.Join(" | ", texts));
        Check("  the field is called 'Screen scale'", texts.Any(t => t.StartsWith("Screen scale")));
        var chooser = All(users).OfType<Button>().First(b => b.AccessibleName == "Screen scale");
        var grid = All(users).OfType<DataGridView>().First();
        var nameBox = All(users).OfType<TextBox>().First();
        Func<string, string> select = name => {
            foreach (DataGridViewRow r in grid.Rows) if ((string)r.Cells[0].Value == name) { grid.ClearSelection(); r.Selected = true; }
            return chooser.Text;
        };
        UserStore.AddOrUpdateUser("oper2", "1234", SecurityLevel.Operator);
        typeof(UsersPreferencesControl).GetMethod("RefreshList", Any).Invoke(users, null);
        Check("a user with no request: 'No specific request'", select("oper2") == "No specific request", chooser.Text);
        Check("admin: their own 120% - not the 90% the caption bar chose", select("admin") == "120%", chooser.Text);
        var cmenu = (ContextMenuStrip)chooser.GetType().GetMethod("BuildMenu", Any).Invoke(chooser, null);
        var citems = cmenu.Items.OfType<ToolStripMenuItem>().Select(i => i.Text).ToList();
        Check("its menu: 'No specific request', then 80% ... 150%", citems.First() == "No specific request" && citems.Skip(1).First() == "80%" && citems.Last() == "150%",
              string.Join(" ", citems));
        Check("  120% ticked", Checked(cmenu.Items) == "120%", Checked(cmenu.Items));
        var cstep = cmenu.Items.OfType<ToolStripControlHost>().First().Control;
        Check("  and the same 1% row, starting at 120%", cstep.Controls.OfType<Label>().First().Text == "120%");
        cstep.GetType().GetMethod("Step").Invoke(cstep, new object[] { 3 });
        cmenu.Show(users, 0, 0); Pump(30);
        cstep.Controls.OfType<Button>().First(b => b.AccessibleName == "Apply").PerformClick();
        Pump(30);
        Check("  applying 123% sets the field", chooser.Text == "123%", chooser.Text);
        cmenu.Close(); cmenu.Dispose(); Pump(30);
        cmenu = (ContextMenuStrip)chooser.GetType().GetMethod("BuildMenu", Any).Invoke(chooser, null);
        cmenu.Items.OfType<ToolStripMenuItem>().First().PerformClick();
        Check("  'No specific request' clears it", chooser.Text == "No specific request");
        cmenu.Dispose();
        UserStore.RemoveUser("oper2");

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nZOOM MENU ALL PASS" : "\nZOOM MENU " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
