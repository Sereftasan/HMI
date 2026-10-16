using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
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
        // 16 October: the speed's slider instead of the list of sizes and the 1% row
        Check("no list of sizes any more", !menu.Items.OfType<ToolStripMenuItem>().Any(), string.Join(" ", menu.Items.OfType<ToolStripMenuItem>().Select(i => i.Text)));
        var host = menu.Items.OfType<ToolStripControlHost>().FirstOrDefault();
        var slider = host == null ? null : host.Control as ScaleSlider;
        Check("the screen scale's slider, at the screen's size: 100%", slider != null && slider.Value == 1.0 && slider.Text == "100%");
        Check("  its steps: the usual sizes, 80% to 150%", string.Join(" ", ScaleSlider.PresetSteps) == "80 90 100 110 120 125 130 140 150", string.Join(" ", ScaleSlider.PresetSteps));
        Check("the glass is named 'Screen scale'", zoom.AccessibleName == "Screen scale");
        if (slider != null) {
            menu.Show(form, 0, 0); Pump(80);
            Func<int, MouseEventArgs> at = v => new MouseEventArgs(MouseButtons.Left, 1, (int)slider.GetType().GetMethod("XOf", Any).Invoke(slider, new object[] { v }), slider.Height / 2, 0);
            typeof(Control).GetMethod("OnMouseDown", Any).Invoke(slider, new object[] { at(120) });
            typeof(Control).GetMethod("OnMouseMove", Any).Invoke(slider, new object[] { at(130) });
            Check("  held and moved to 130%: shown, not chosen yet", DisplaySettings.PanelFactor == 1.0 && (int?)Get(slider, "_dragging") == 130);
            typeof(Control).GetMethod("OnMouseUp", Any).Invoke(slider, new object[] { at(130) });
            Pump(50);
            Check("  let go: 130% is the PANEL'S scale, the menu closed", DisplaySettings.PanelFactor == 1.30 && DisplaySettings.StartFactor == 1.30 && !menu.Visible,
                  DisplaySettings.PanelFactor + " / " + DisplaySettings.StartFactor);
            Check("  and leaves admin's own request alone (there is none)", !DisplaySettings.UserFactor("admin").HasValue);
            menu.Dispose(); Pump(30);
            menu = Menu(form);
            slider = (ScaleSlider)menu.Items.OfType<ToolStripControlHost>().First().Control;
            int asked2 = 0;
            typeof(ScaleSlider).GetField("EnterValue", Any).SetValue(null, (Func<ScaleSlider, int, int?>)((s, now) => { asked2 = now; return 137; }));
            menu.Show(form, 0, 0); Pump(50);
            slider.GetType().GetMethod("AskValue", Any).Invoke(slider, null);
            Pump(50);
            Check("the percentage typed (the keypad, from 130%): any size - 137%", asked2 == 130 && DisplaySettings.PanelFactor == 1.37, asked2 + " / " + DisplaySettings.PanelFactor);
            menu.Dispose();
            menu = Menu(form);
            slider = (ScaleSlider)menu.Items.OfType<ToolStripControlHost>().First().Control;
            typeof(ScaleSlider).GetField("EnterValue", Any).SetValue(null, (Func<ScaleSlider, int, int?>)((s, now) => 160));
            menu.Show(form, 0, 0); Pump(50);
            slider.GetType().GetMethod("AskValue", Any).Invoke(slider, null);
            Pump(50);
            Check("  never above 150%", DisplaySettings.PanelFactor == 1.50, DisplaySettings.PanelFactor.ToString());
            typeof(ScaleSlider).GetField("EnterValue", Any).SetValue(null, null);
            menu.Dispose(); Pump(30);
        }

        DisplaySettings.SetUserFactor("admin", 1.2);
        form.ChooseFactor(1.37);
        Check("choosing 137% makes it the panel's and the start scale", DisplaySettings.PanelFactor == 1.37 && DisplaySettings.StartFactor == 1.37);
        Check("  a signed-in user's own request is NOT changed by it (120% stays)", DisplaySettings.UserFactor("admin") == 1.2, "" + DisplaySettings.UserFactor("admin"));
        Check("  and offers the restart in percent (declined here: nothing restarts)", asked != null && asked.Contains("137%") && asked.Contains("screen scale") && !form.IsDisposed, asked ?? "not asked");
        Check("  the file keeps it: Panel=1.37, Start=1.37, admin's 1.2", File.ReadAllText(Path.Combine(dir, "Display.cfg")).Contains("Panel=1.37") &&
              File.ReadAllText(Path.Combine(dir, "Display.cfg")).Contains("User=admin;1.2"));
        menu = Menu(form);
        Check("  the slider shows 137% (between two steps)", ((ScaleSlider)menu.Items.OfType<ToolStripControlHost>().First().Control).Text == "137%");
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
        var chooser = All(users).OfType<ScaleField>().First();
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
        Check("the field: the same slider at 120%, and 'x' (no specific request) beside it",
              chooser.Slider.Value == 1.2 && chooser.OfferNone && chooser.NoneButton.Visible && chooser.NoneButton.Enabled);
        typeof(ScaleSlider).GetField("EnterValue", Any).SetValue(null, (Func<ScaleSlider, int, int?>)((s, now) => 123));
        chooser.Slider.GetType().GetMethod("AskValue", Any).Invoke(chooser.Slider, null);
        typeof(ScaleSlider).GetField("EnterValue", Any).SetValue(null, null);
        Check("  the percentage typed: 123% in the field", chooser.Text == "123%", chooser.Text);
        chooser.NoneButton.PerformClick();
        Check("  'x' clears it: no specific request - the panel's size shown grey, 'x' greyed", chooser.Text == "No specific request" && !chooser.Value.HasValue &&
              !chooser.NoneButton.Enabled, chooser.Text);
        UserStore.RemoveUser("oper2");

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nZOOM MENU ALL PASS" : "\nZOOM MENU " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
