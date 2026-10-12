using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// Every page can be scrolled, and every tab name stays in sight: tabs that do
// not fit on one line wrap onto more rows and the strip grows to hold them.
class PT {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }

    class Page : ISettingsPage {
        readonly string _title;
        public Page(string t) { _title = t; }
        public string TitleKey { get { return "PT_" + _title; } }
        public string TitleFallback { get { return _title; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.None; } }
        public UserControl GetControl() { return new UserControl(); }
    }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        SecurityManager.Login("admin", SecurityLevel.Admin);

        Console.WriteLine("--- tab names wrap onto rows ---");
        var names = new[] { "Unit Settings", "User Settings", "Import Settings", "Log Settings", "PLC Connection", "AI Prompt", "About" };
        var screen = new SettingsScreen(names.Select(n => (ISettingsPage)new Page(n)));
        var form = new Form { ClientSize = new Size(1400, 500) };
        form.Controls.Add(screen);
        form.Show();
        Pump(150);
        var strip = (Control)typeof(SettingsScreen).GetField("_tabStrip", Any).GetValue(screen);
        int oneRow = strip.Height;
        Check("wide: one row", screen.TabRows == 1, "rows " + screen.TabRows);

        form.ClientSize = new Size(420, 500);
        Pump(150);
        Check("narrow: the tabs take more rows", screen.TabRows > 1, "rows " + screen.TabRows);
        var buttons = strip.Controls.Cast<Control>().Where(c => c.Visible).ToList();
        Check("  every tab inside the strip - none cut off at the right or the bottom",
              buttons.All(b => b.Right <= strip.ClientSize.Width && b.Bottom <= strip.ClientSize.Height),
              string.Join(" ", buttons.Select(b => b.Bounds.ToString())) + " strip " + strip.ClientSize);
        Check("  the strip grew to hold them", strip.Height > oneRow, oneRow + " -> " + strip.Height);

        form.ClientSize = new Size(1400, 500);
        Pump(150);
        Check("wide again: back to one row, the usual height", screen.TabRows == 1 && strip.Height == oneRow, screen.TabRows + " / " + strip.Height);
        form.Close();

        Console.WriteLine("--- pages scroll ---");
        var docked = new UserControl();
        docked.Controls.Add(new DataGridView { Dock = DockStyle.Fill });
        docked.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 40 });
        PageScroll.Enable(docked);
        Check("a page made of docked parts scrolls below its least size", docked.AutoScroll &&
              docked.AutoScrollMinSize == new Size(Kaban.UI.Controls.UiScale.Px(PageScroll.MinDockedWidth), Kaban.UI.Controls.UiScale.Px(PageScroll.MinDockedHeight)),
              docked.AutoScrollMinSize.ToString());
        var placed = new UserControl();
        placed.Controls.Add(new Button { Location = new Point(1200, 10) });
        PageScroll.Enable(placed);
        Check("a page of placed parts scrolls to its farthest part (no least size needed)", placed.AutoScroll && placed.AutoScrollMinSize.IsEmpty);
        var side = new UserControl();
        side.Controls.Add(new DataGridView { Dock = DockStyle.Fill });
        PageScroll.Enable(side, minimum: false);
        Check("a side page scrolls but keeps its narrow width", side.AutoScroll && side.AutoScrollMinSize.IsEmpty);
        var own = new UserControl { AutoScrollMinSize = new Size(300, 200) };
        own.Controls.Add(new Panel { Dock = DockStyle.Fill });
        PageScroll.Enable(own);
        Check("a page's own least size is kept", own.AutoScrollMinSize == new Size(300, 200));

        Console.WriteLine(fail == 0 ? "\nPAGE SCROLL AND TABS ALL PASS" : "\nPAGE SCROLL AND TABS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
