using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;

// 15 October: Visual Studio could not open FA2010's manual pages - "Could not find type
// 'Kaban.Machines.FA2010.Screens.Shared.AxisJogBar'". Taken then for the machine's program being 32-bit; it is not, to
// a loader ("Any CPU", MSIL - its 32-bit preference is for running it): the designer says so when the machine was not
// built, or an older build is still loaded. 16 October: the Axis page holds the bar itself again. A page holds
// controls of libraries and of its own machine's program - never another machine's.
class PD {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static IEnumerable<Control> All(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in All(k)) yield return d; } }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    // a control as Visual Studio's designer has it: sited, in design mode
    class DesignSite : System.ComponentModel.ISite {
        public System.ComponentModel.IComponent Component { get; set; }
        public System.ComponentModel.IContainer Container { get { return null; } }
        public bool DesignMode { get { return true; } }
        public string Name { get; set; }
        public object GetService(Type t) { return null; }
    }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        var programs = Directory.GetFiles(".", "Kaban.Machines.*.exe").Select(f => Assembly.LoadFrom(f)).ToList();
        Check("the machines' programs found", programs.Count >= 3, string.Join(", ", programs.Select(a => a.GetName().Name)));
        int pages = 0;
        var bad = new List<string>();
        foreach (var asm in programs) {
            foreach (var t in asm.GetTypes().Where(t => typeof(UserControl).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)) {
                Control page;
                try { page = (Control)Activator.CreateInstance(t); } catch { continue; }
                pages++;
                foreach (var c in All(page))
                    if (c.GetType().Assembly.Location.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && c.GetType().Assembly != asm)
                        bad.Add(asm.GetName().Name + ": " + t.Name + "." + c.Name + " is a " + c.GetType().FullName + " (" + Path.GetFileName(c.GetType().Assembly.Location) + ")");
                page.Dispose();
            }
        }
        Check("every page's controls come from libraries or its own machine's program, none from another's - " + pages + " pages", pages > 20 && bad.Count == 0,
              string.Join(" | ", bad.Take(6)));
        var fa = programs.FirstOrDefault(a => a.GetName().Name == "Kaban.Machines.FA2010");
        var bar = fa == null ? null : fa.GetType("Kaban.Machines.FA2010.Screens.Shared.AxisJogBar");
        Check("FA2010's jog bar: in the machine's program (no library of its own), loadable as Any CPU (MSIL)",
              bar != null && !File.Exists("Kaban.Machines.FA2010.Shared.dll") &&
              AssemblyName.GetAssemblyName(fa.Location).ProcessorArchitecture == ProcessorArchitecture.MSIL,
              fa == null ? "-" : AssemblyName.GetAssemblyName(fa.Location).ProcessorArchitecture.ToString());

        // 16 October: opened in Visual Studio, the Axis page's lists (PLC axis items, Motion monitor) were made
        // at design time too, moving the page's controls down 40 - and the designer saved the moved places: the
        // column names went from 52 to 92, Robot X onto Robot Y. Designed, a page of axis rows is left as it is.
        var axisType = fa == null ? null : fa.GetType("Kaban.Machines.FA2010.Screens.Parameters.AxisSettings");
        var axisPage = (UserControl)Activator.CreateInstance(axisType);
        axisPage.Site = new DesignSite { Component = axisPage, Name = "AxisSettings" };
        foreach (var c in All(axisPage).ToList()) c.Site = new DesignSite { Component = c, Name = c.Name };
        var before = axisPage.Controls.Cast<Control>().ToDictionary(c => c, c => c.Location);
        var win = new Form { ClientSize = new System.Drawing.Size(1300, 700) };
        axisPage.Dock = DockStyle.Fill;
        win.Controls.Add(axisPage); win.Show(); Pump(800);
        var moved = before.Where(kv => kv.Key.Location != kv.Value).Select(kv => kv.Key.Name + " " + kv.Value.Y + "->" + kv.Key.Location.Y).ToList();
        var lists = All(axisPage).OfType<Kaban.UI.Controls.AxisPickBar>().Count();
        Check("the Axis page in the designer: nothing moved, no lists made (the designer would save them)", moved.Count == 0 && lists == 0,
              lists + " lists; " + string.Join(", ", moved.Take(5)));
        var heads = axisPage.Controls.OfType<Label>().Where(l => l.Name.StartsWith("lblHead")).ToList();
        var rows = axisPage.Controls.OfType<Kaban.UI.Controls.AxisSettingHMI>().OrderBy(r => r.Top).ToList();
        // (the bar 76 high since its captions went onto two lines - 5dd9f35: the names at 76, Robot X at 100)
        Check("  as saved: the jog bar at the top, the column names at 76, Robot X at 100, the rows one under the other",
              heads.All(h => h.Top == 76) && rows[0].Name == "axRobot_X" && rows[0].Top == 100 && rows.Zip(rows.Skip(1), (x, y) => y.Top - x.Bottom).All(g => g >= 0) &&
              axisPage.Controls.Cast<Control>().Single(c => c.GetType() == bar).Top == 0,
              string.Join(" ", rows.Take(3).Select(r => r.Name + "@" + r.Top)));
        win.Close();

        Console.WriteLine(fail == 0 ? "PAGES DESIGNABLE ALL PASS" : "PAGES DESIGNABLE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
