using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Settings;

// 23 October: "another member to 'Add the devices of the screen ...' dropdown menu in PLC Monitor to select the control
// on the page (as the first item). It should be called 'all under the next control / page - into this tab' and it should
// turn the rest of the screen darker (just like it does on Notes and Feedback page) and everything under the first clicked
// control or page navigation button or tab title should be added to the active tab (there should be a blinking cancelling
// button appeared on PLC Monitor page then so the user could give up the label adding process (ESC keyboard shortcut as well))"
class MP {
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
    static object Call(object o, string n, params object[] a) {
        var mi = o.GetType().GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length);
        return mi.Invoke(o, a);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static IEnumerable<Control> All(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; } }
    static UserControl Nav(MainForm f, string role) {
        var groups = (IDictionary)Get(f, "_screenGroups");
        return groups.Keys.Cast<UserControl>().First(k => ((MachineRegistry.RoleGroup)groups[k]).Role == role);
    }
    static object Candidate(MainForm f, Control hit, System.Drawing.Point at) {
        return f.GetType().GetMethod("CandidateAt", Any).Invoke(f, new object[] { hit, at, 1 });
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "mpk_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        ViewPreferences.Reload();
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        foreach (int st in new[] { 1, 2, 3, 4 }) { PlcManager.Instance.RegisterDriver(st, new Kaban.Drivers.Simulation.SimulationDriver(st)); ConnectionMonitor.ReportRead(st, true); }

        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show();
        Pump(400);
        form.LoadScreen(Nav(form, ScreenRoles.Manual));
        Pump(300);
        var mon = (SimulationMonitorForm)form.ShowSidePage("PlcMonitor");
        Pump(300);

        Console.WriteLine("--- the menu ---");
        var menu = (ContextMenuStrip)Call(mon, "CollectMenu");
        var items = menu.Items.OfType<ToolStripMenuItem>().ToList();
        Check("its first item: all under the next control / page - into this tab", items.Count > 0 && items[0].Text == "all under the next control / page - into this tab" && items[0].Enabled,
              items.Count > 0 ? items[0].Text : "none");

        Console.WriteLine("--- picking ---");
        var monitors = (IList)Get(mon, "_monitors");
        var tab = monitors.Cast<object>().First(x => (int)Get(x, "Station") == 1);
        ((TabControl)Get(mon, "_tabs")).SelectedIndex = monitors.IndexOf(tab);
        Pump(50);
        Call(mon, "PickUnder");
        Pump(100);
        var bar = (Panel)Get(mon, "_pickBar");
        var cancel = (Button)Get(mon, "_pickCancel");
        var shade = Get(form, "DevicePickShade");
        Check("on: the main window picks, the rest of it darker (as the notes page's), the monitor's bar with its cancel button",
              form.DevicePicking && (bool)Get(mon, "PickingUnder") && bar.Visible && cancel.Visible && cancel.Text.Contains("Esc") &&
              shade != null && (bool)Get(shade, "On"));
        var c0 = cancel.BackColor;
        Pump(600);
        Check("  the button blinks", cancel.BackColor != c0, c0 + " -> " + cancel.BackColor);

        var page = (Control)form.GetType().GetMethod("CurrentView", Any).Invoke(form, null);
        var unit = All(page).OfType<CylinderUnitHMI>().FirstOrDefault(u => Kaban.UI.Helpers.DeviceCollector.UnderControl(u, 1).Count > 1);
        Check("a cylinder unit on the page to pick", unit != null);
        var frame = unit == null ? null : unit.Controls.Cast<Control>().FirstOrDefault(k => k.GetType().Name == "RoundedGroupBox");
        var byFrame = frame == null ? null : Candidate(form, frame, new System.Drawing.Point(5, 5));
        Check("  its title (frame): the unit - bright, every device under it", byFrame != null && ReferenceEquals(Get(byFrame, "Bright"), unit),
              byFrame == null ? "none" : Convert.ToString(Get(byFrame, "Bright")));
        var label = All(page).OfType<Label>().FirstOrDefault(l => Kaban.UI.Helpers.DeviceCollector.UnderControl(l, 1).Count == 0 && l.Parent != null &&
                                                                   Kaban.UI.Helpers.DeviceCollector.UnderControl(l.Parent, 1).Count > 0);
        var byLabel = label == null ? null : Candidate(form, label, new System.Drawing.Point(1, 1));
        Check("  a label (no device of its own): the nearest control around it with devices",
              byLabel != null && Kaban.UI.Helpers.DeviceCollector.UnderControl((Control)Get(byLabel, "Bright"), 1).Count > 0 && !ReferenceEquals(Get(byLabel, "Bright"), label));
        var menuButtons = (IDictionary)Get(form, "_menuButtons");
        var manualButton = menuButtons.Keys.Cast<ButtonHMI>().First(b => ReferenceEquals(menuButtons[b], Nav(form, ScreenRoles.MachineParameters)));
        var byNav = Candidate(form, manualButton, new System.Drawing.Point(5, 5));
        Check("  a page button of the menu: its page (every tab of it)", byNav != null && ReferenceEquals(Get(byNav, "Bright"), manualButton) &&
              ((string)Get(byNav, "Title")).Length > 0);
        var tabTitle = All(form.CurrentPage).FirstOrDefault(SettingsScreen.IsTab);
        var byTab = tabTitle == null ? null : Candidate(form, tabTitle, new System.Drawing.Point(5, 5));
        Check("  a tab's title: its page", byTab != null && ReferenceEquals(Get(byTab, "Bright"), tabTitle), tabTitle == null ? "no tab" : tabTitle.Text);
        var ownBar = Candidate(form, cancel, new System.Drawing.Point(5, 5));
        Check("  the PLC Monitor itself: not picked (its button works)", ownBar == null);

        var sheetsBefore = ((IList)Get(tab, "Sheets")).Count;
        form.GetType().GetMethod("DevicePicked", Any).Invoke(form, new object[] { byFrame });
        Pump(100);
        var sheet = Get(tab, "Current");
        var unitAddr = Kaban.UI.Helpers.DeviceCollector.UnderControl(unit, 1).Select(d => d.Address).ToList();
        Func<int> onceEach = () => unitAddr.Count(a => ((IList)Get(sheet, "Rows")).Cast<object>().Count(r => (bool)Get(r, "IsDevice") && string.Equals((string)Get(r, "Address"), a, StringComparison.OrdinalIgnoreCase)) == 1);
        int unitDevices = unitAddr.Count;
        Check("the unit picked: its devices into the tab on show (each once), picking over, the shade gone", sheet != null && onceEach() == unitDevices && !form.DevicePicking &&
              !(bool)Get(mon, "PickingUnder") && !(bool)Get(shade, "On") && !cancel.Visible, (sheet == null ? 0 : onceEach()) + " of " + unitDevices);
        Check("  the bar says so a while", bar.Visible && ((Label)Get(mon, "_pickText")).Text.Contains(unitDevices + " device(s) added"), ((Label)Get(mon, "_pickText")).Text);

        Console.WriteLine("--- given up ---");
        Call(mon, "PickUnder");
        Pump(50);
        form.CancelDevicePick();
        Pump(50);
        Check("the cancel (or Esc): picking over, the bar gone, nothing added", !form.DevicePicking && !bar.Visible && onceEach() == unitDevices);
        Call(mon, "PickUnder");
        Pump(50);
        var esc = new Message { Msg = 0x0100, WParam = (IntPtr)(int)Keys.Escape, HWnd = form.Handle };
        var filter = (IMessageFilter)Get(form, "_devicePick");
        bool kept = filter.PreFilterMessage(ref esc);
        Pump(100);
        Check("  Esc: the same", kept && !form.DevicePicking && !bar.Visible);

        // 23 October: "The label names are absurd (50.0 mm - setting), could be the description text of the control instead"
        Console.WriteLine("--- the labels' names ---");
        var groups = (IDictionary)Get(form, "_screenGroups");
        var parameters = ((MachineRegistry.RoleGroup)groups[Nav(form, ScreenRoles.MachineParameters)]).Pages[0].Value;
        var named = Kaban.UI.Helpers.DeviceCollector.FromScreen(parameters, 1);
        var gripper = named.FirstOrDefault(d => d.Address == "D506");
        Check("a field's devices named by its caption (Robot: Gripper Length), not its value", gripper != null && gripper.UsedAs == "Gripper Length" &&
              !named.Any(d => System.Text.RegularExpressions.Regex.IsMatch(d.UsedAs ?? "", @"^-?[0-9.]+ ?(mm|sec|s|%|°)?\b - ")),
              gripper == null ? "no D506" : gripper.UsedAs + " | " + string.Join(", ", named.Take(6).Select(d => d.Address + "=" + d.UsedAs)));

        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nMONITOR PICK ALL PASS" : "\nMONITOR PICK " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
