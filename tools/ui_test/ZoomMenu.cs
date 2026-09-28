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

// The magnifying glass on the caption bar, between the user and the language:
// a user's own factor, the panel's (Admin), the start factor with nobody
// signed in - and User Settings without its panel-factor block.
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
        var factors = menu.Items.OfType<ToolStripMenuItem>().Where(i => i.Tag is double).Select(i => i.Text).ToList();
        Check("0.80 to 1.50 offered", string.Join(" ", factors) == "0.80 0.90 1.00 1.10 1.20 1.25 1.30 1.40 1.50", string.Join(" ", factors));
        Check("  the screen's factor ticked (no own one yet)", Checked(menu.Items) == "1.00", Checked(menu.Items));
        var everyone = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i => i.DropDownItems.Count > 0);
        Check("  an Admin also has 'For everyone on this panel'", everyone != null && everyone.Text == "For everyone on this panel" && everyone.DropDownItems.Count == 9);

        form.ChooseFactor(1.25, false);
        Check("choosing 1.25 keeps it as admin's own, and as the start factor",
              DisplaySettings.UserFactor("admin") == 1.25 && DisplaySettings.StartFactor == 1.25);
        Check("  and offers the restart (declined here: nothing restarts)", asked != null && asked.Contains("1.25") && !form.IsDisposed, asked ?? "not asked");
        menu = Menu(form);
        Check("  ticked now; 'The panel's (1.00)' offered to drop it", Checked(menu.Items) == "1.25" &&
              menu.Items.OfType<ToolStripMenuItem>().Any(i => i.Text == "The panel's (1.00)"), Checked(menu.Items));
        form.ChooseFactor(null, false);
        Check("'The panel's' drops the own factor", !DisplaySettings.UserFactor("admin").HasValue && DisplaySettings.StartFactor == 1.00);

        asked = null;
        form.ChooseFactor(1.10, true);
        Check("for everyone: the panel's factor is 1.10", DisplaySettings.PanelFactor == 1.10 && asked != null, "panel " + DisplaySettings.PanelFactor);

        Console.WriteLine("--- an operator, nobody ---");
        SecurityManager.Login("op", SecurityLevel.Operator);
        menu = Menu(form);
        Check("an operator: no 'For everyone'", !menu.Items.OfType<ToolStripMenuItem>().Any(i => i.DropDownItems.Count > 0));
        SecurityManager.Logout();
        menu = Menu(form);
        Check("nobody signed in: the list says it is this panel's start", menu.Items.OfType<ToolStripLabel>().Any(l => l.Text.StartsWith("This panel")));
        form.ChooseFactor(0.9, false);
        Check("  and choosing sets only the start factor", DisplaySettings.StartFactor == 0.9 && DisplaySettings.PanelFactor == 1.10 &&
              !DisplaySettings.UserFactor("op").HasValue);

        Console.WriteLine("--- User Settings: the user's own factor only ---");
        var users = new UserSettingsControl();
        var texts = All(users).OfType<Label>().Select(l => l.Text).ToList();
        Check("no 'Magnifying factor of this panel' block", !texts.Any(t => t.Contains("of this panel") || t.StartsWith("For everyone") || t.StartsWith("0.8 to 1.5")),
              string.Join(" | ", texts));
        Check("  the user's own factor is still there", texts.Any(t => t.StartsWith("Magnifying factor")));

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nZOOM MENU ALL PASS" : "\nZOOM MENU " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
