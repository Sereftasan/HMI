using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.UI.Controls;

// "It should always show the hand when an enabled button or combobox is
// hovered, on every page" - including the caption bar, its dropdown menus
// (connection, alarms, login, language) and anything added after a page opened.
class HC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    // Idle is what runs the sweep in the program; Mono's DoEvents does not raise
    // it, so the test calls the same method the Idle handler calls.
    static void Pump() { for (int i = 0; i < 5; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } UiStyle.SweepOpenForms(); }

    static void Move(ToolStrip ts, Point p) {
        typeof(ToolStrip).GetMethod("OnMouseMove", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(ts, new object[] { new MouseEventArgs(MouseButtons.None, 0, p.X, p.Y, 0) });
    }

    [STAThread]
    static void Main() {
        UiStyle.EnableHandCursorsEverywhere();

        Console.WriteLine("--- any window, any time ---");
        var form = new Form { ClientSize = new Size(400, 300) };
        var panel = new Panel { Dock = DockStyle.Fill };
        var btn = new Button { Text = "OK", Location = new Point(10, 10) };
        var combo = new ComboBox { Location = new Point(10, 50), DropDownStyle = ComboBoxStyle.DropDownList };
        var drag = new Button { Text = "drag", Location = new Point(10, 90), Cursor = Cursors.SizeAll };
        panel.Controls.Add(btn); panel.Controls.Add(combo); panel.Controls.Add(drag);
        form.Controls.Add(panel);
        form.Show(); Pump();
        Check("a plain button in a window nobody swept: hand", btn.Cursor == Cursors.Hand, btn.Cursor.ToString());
        Check("a combo box: hand", combo.Cursor == Cursors.Hand, combo.Cursor.ToString());
        Check("a cursor chosen on purpose is kept", drag.Cursor == Cursors.SizeAll, drag.Cursor.ToString());
        var late = new Button { Text = "late", Location = new Point(10, 130) };
        panel.Controls.Add(late); Pump();
        Check("a button added AFTER the window opened: hand", late.Cursor == Cursors.Hand, late.Cursor.ToString());
        btn.Enabled = false;
        Check("disabled: back to the arrow", btn.Cursor == Cursors.Default, btn.Cursor.ToString());
        btn.Enabled = true;
        Check("enabled again: hand", btn.Cursor == Cursors.Hand);

        Console.WriteLine("--- dropdown menus: hand over items, arrow over headings ---");
        var menu = UiStyle.Menu();
        menu.Items.Add(UiStyle.MenuHeader("Heading"));
        menu.Items.Add(new ToolStripSeparator());
        var item = new ToolStripMenuItem("Log out");
        var off = new ToolStripMenuItem("Not now") { Enabled = false };
        menu.Items.Add(item); menu.Items.Add(off);
        UiStyle.ShowPopup(menu, btn, new Point(0, btn.Height)); Pump();
        var itemCentre = new Point(item.Bounds.Left + item.Bounds.Width / 2, item.Bounds.Top + item.Bounds.Height / 2);
        Move(menu, itemCentre);
        Check("over an item: hand", menu.Cursor == Cursors.Hand, menu.Cursor + " at " + itemCentre);
        var head = menu.Items[0];
        Move(menu, new Point(head.Bounds.Left + head.Bounds.Width / 2, head.Bounds.Top + head.Bounds.Height / 2));
        Check("over the heading: arrow", menu.Cursor == Cursors.Default, menu.Cursor.ToString());
        Move(menu, new Point(off.Bounds.Left + off.Bounds.Width / 2, off.Bounds.Top + off.Bounds.Height / 2));
        Check("over a disabled item: arrow", menu.Cursor == Cursors.Default, menu.Cursor.ToString());
        menu.Close();

        Console.WriteLine("--- the next menu for the same button disposes the last one ---");
        var menu2 = UiStyle.Menu(); menu2.Items.Add(new ToolStripMenuItem("again"));
        UiStyle.ShowPopup(menu2, btn, new Point(0, btn.Height)); Pump();
        Check("previous menu disposed", menu.IsDisposed);
        menu2.Close();
        form.Close(); Pump();
        Check("the last one goes with its button", menu2.IsDisposed);

        Console.WriteLine(fail == 0 ? "\nHAND ALL PASS" : "\nHAND " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
