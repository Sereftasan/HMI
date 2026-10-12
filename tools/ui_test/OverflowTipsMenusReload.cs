using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Settings;

// 8 October:
//  "The extended toolbar buttons (shown at the additional toolbar which appears down way arrow) doesn't show tooltips"
//  "The table column edition right-click dropdown menu doesn't open at the first right-click"
//  "Clicking "Discard changes and re-read the saved file" in "PLC Connection" it flutters for a few seconds"
class OTM {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); Thread.Sleep(20); } }
    static object Get(object o, string n) {
        for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, Any); if (f != null) return f.GetValue(o); }
        return null;
    }
    static void Reset() { foreach (var f in new[] { "LastShownTip", "LastShownOn", "LastShownFor" }) typeof(UiStyle).GetField(f, Any).SetValue(null, null); }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "otm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        Kaban.UI.Helpers.ColumnChooser.Folder = Path.Combine(dir, "Layouts");
        typeof(UiStyle).GetField("LayoutFolder", Any)?.SetValue(null, Path.Combine(dir, "Layouts"));

        Console.WriteLine("--- a toolbar's overflow: the buttons that went under the arrow ---");
        var tips = UiStyle.Tips();
        var form = new Form { ClientSize = new Size(260, 200), StartPosition = FormStartPosition.Manual, Location = new Point(40, 40) };
        var bar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, CanOverflow = true,
                                  LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow, AutoSize = false, Height = 40 };
        var buttons = new List<Button>();
        for (int i = 0; i < 8; i++)
        {
            var b = UiStyle.ToolButton("✔", "Button " + i + " (F" + (i + 1) + ")", tips, (s, e) => { });
            b.Name = "b" + i;
            buttons.Add(b);
            bar.Items.Add(new ToolStripControlHost(b) { AutoSize = false, Size = b.Size });
        }
        form.Controls.Add(bar);
        form.Show();
        Pump(200);
        var hosts = bar.Items.OfType<ToolStripControlHost>().ToList();
        var under = hosts.LastOrDefault(h => h.IsOnOverflow);
        Check("the narrow toolbar puts some under its arrow", under != null, string.Join(",", hosts.Select(h => h.Placement.ToString())));
        bar.OverflowButton.ShowDropDown();
        Pump(300);
        var btn = under == null ? null : (Button)under.Control;
        Control root = btn;
        while (root != null && root.Parent != null) root = root.Parent;
        Check("  the arrow opened: the button is on the dropdown's window", root is ToolStripDropDown, root == null ? "none" : root.GetType().Name);
        Reset();
        typeof(Control).GetMethod("OnMouseEnter", Any).Invoke(btn, new object[] { EventArgs.Empty });
        typeof(Control).GetMethod("OnMouseMove", Any).Invoke(btn, new object[] { new MouseEventArgs(MouseButtons.None, 0, 12, 9, 0) });
        typeof(UiStyle).GetField("LastTipWindow", Any).SetValue(null, null);
        Pump(UiStyle.TooltipDelayMs + 400);
        // (Windows shows nothing on a button whose window is not the active one - the
        // dropdown never is; Mono's ToolTip, the other way round, takes a form only:
        // here the window it is meant for is what can be checked)
        var meant = typeof(UiStyle).GetField("LastTipWindow", Any).GetValue(null);
        Check("its tip is shown on the dropdown's window (one ToolTip lets through: it never becomes the active one)",
              meant == root && typeof(UiStyle).GetField("LastShownFor", Any).GetValue(null) == btn,
              meant == null ? "nothing" : meant.GetType().Name);
        var at = new Point(5, 30);
        var args = new object[] { btn, at };
        var w = typeof(UiStyle).GetMethod("TipWindowFor", Any).Invoke(null, args);
        var moved = (Point)args[1];
        Check("  at the same point of the screen", w == root && root.PointToScreen(moved) == btn.PointToScreen(new Point(5, 30)), moved.ToString());
        var inPlace = hosts.First(h => !h.IsOnOverflow).Control;
        Reset();
        typeof(Control).GetMethod("OnMouseEnter", Any).Invoke(inPlace, new object[] { EventArgs.Empty });
        typeof(Control).GetMethod("OnMouseMove", Any).Invoke(inPlace, new object[] { new MouseEventArgs(MouseButtons.None, 0, 12, 9, 0) });
        Pump(UiStyle.TooltipDelayMs + 400);
        Check("  a button on the bar itself: on the button, as before", typeof(UiStyle).GetField("LastShownOn", Any).GetValue(null) == inPlace &&
              typeof(UiStyle).GetField("LastTipWindow", Any).GetValue(null) == inPlace &&
              (string)typeof(UiStyle).GetField("LastShownTip", Any).GetValue(null) == "Button 0 (F1)");
        bar.OverflowButton.HideDropDown();
        form.Close();

        Console.WriteLine("--- a table's headings menu, the first right-click ---");
        var host = new Form { ClientSize = new Size(500, 300) };
        var grid = new DataGridView { Dock = DockStyle.Fill };
        grid.Columns.Add("A", "Alpha"); grid.Columns.Add("B", "Beta"); grid.Columns.Add("C", "Gamma");
        host.Controls.Add(grid);
        host.Show();
        UiStyle.EnableTableTools(grid, "otm_table");
        Pump(100);
        var menu = grid.Columns[0].HeaderCell.ContextMenuStrip;
        Check("the headings have their menu, empty until it opens", menu != null && menu.Items.Count == 0);
        // WinForms opens an EMPTY menu with Cancel already set (nothing to show)
        var opening = new CancelEventArgs(true);
        typeof(ToolStripDropDown).GetMethod("OnOpening", Any).Invoke(menu, new object[] { opening });
        Check("the first opening fills it and lets it open (Cancel taken back)", !opening.Cancel && menu.Items.Count > 3,
              "cancel=" + opening.Cancel + ", " + menu.Items.Count + " items");
        Check("  its columns ticked", menu.Items.OfType<ToolStripMenuItem>().Take(3).Select(i => i.Text).SequenceEqual(new[] { "Alpha", "Beta", "Gamma" }));
        // a menu made for one showing, for the same table, does not take the headings' menu with it
        UiStyle.ShowPopupAt(menu, grid, grid.PointToScreen(new Point(10, 10)));
        Pump(100);
        menu.Close();
        var other = UiStyle.Menu();
        other.Items.Add("x");
        UiStyle.ShowPopup(other, grid, new Point(20, 20));
        Pump(100);
        other.Close();
        Check("  a later popup on the table does not dispose the headings' menu", !menu.IsDisposed);
        host.Close();

        Console.WriteLine("--- PLC Connection: discard and re-read ---");
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var page = new Form { ClientSize = new Size(1200, 500) };
        var ctl = new StationConnectionControl();
        page.Controls.Add(ctl);
        page.Show();
        Pump(300);
        var table = (TableLayoutPanel)Get(ctl, "_table");
        var rows = (System.Collections.IList)Get(ctl, "_rows");
        var save = (Button)Get(ctl, "_btnSave");
        var before = table.Controls.Cast<Control>().ToList();
        var row0 = rows[0];
        var address = (TextBox)Get(row0, "Address");
        var driver = (ComboBox)Get(row0, "Driver");
        var savedAddress = address.Text;
        var savedDriver = driver.SelectedItem;
        driver.SelectedIndex = (driver.SelectedIndex + 1) % driver.Items.Count;
        address.Text = "10.0.0.99:502:1";
        Pump(50);
        Check("a change lights Save", save.Enabled);
        int layouts = 0;
        table.Layout += (s, e) => layouts++;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        typeof(StationConnectionControl).GetMethod("Reload", Any).Invoke(ctl, null);
        long ms = sw.ElapsedMilliseconds;
        Pump(50);
        var after = table.Controls.Cast<Control>().ToList();
        Check("re-read: the same rows, nothing built again (no flutter)", before.SequenceEqual(after) && after.All(c => !c.IsDisposed),
              before.Count + " -> " + after.Count + " controls");
        Check("  the saved values back in them, Save off", driver.SelectedItem == savedDriver && address.Text == savedAddress && !save.Enabled,
              driver.Text + " / " + address.Text + " / save " + save.Enabled);
        Check("  quick, and a handful of layouts at most", ms < 500 && layouts <= 3, ms + " ms, " + layouts + " layouts");
        page.Close();

        Console.WriteLine(fail == 0 ? "\nOVERFLOW TIPS / MENUS / RELOAD ALL PASS" : "\nOVERFLOW TIPS / MENUS / RELOAD " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
