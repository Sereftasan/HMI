using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;

// 15 October: Visual Studio could not open FA2010's manual pages - "Could not find type
// 'Kaban.Machines.FA2010.Screens.Shared.AxisJogBar'". The bar was a control of the machine's PROGRAM,
// and the program is built 32-bit (Prefer 32-bit, for MX Component: CLR flags 0x20003), which the
// 64-bit Visual Studio cannot load a control from; Kaban.UI's controls load (a library: Any CPU).
// 16 October, simpler: no page holds the bar - it is shown above the Manual Movement tabs and on the
// Axis Monitor by the program, and the Axis page has a copy of its controls. Every machine's pages: no
// control on them may come from a machine's program (.exe).
class PD {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static IEnumerable<Control> All(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in All(k)) yield return d; } }

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
                    if (c.GetType().Assembly.Location.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        bad.Add(asm.GetName().Name + ": " + t.Name + "." + c.Name + " is a " + c.GetType().FullName + " (" + Path.GetFileName(c.GetType().Assembly.Location) + ")");
                page.Dispose();
            }
        }
        Check("every page's controls come from libraries, none from a machine's program - " + pages + " pages", pages > 20 && bad.Count == 0,
              string.Join(" | ", bad.Take(6)));
        var fa = programs.FirstOrDefault(a => a.GetName().Name == "Kaban.Machines.FA2010");
        var bar = fa == null ? null : fa.GetType("Kaban.Machines.FA2010.Screens.Shared.AxisJogBar");
        Check("FA2010's jog bar: in the machine's program again (no library of its own), held by no page",
              bar != null && !File.Exists("Kaban.Machines.FA2010.Shared.dll"));
        Console.WriteLine(fail == 0 ? "PAGES DESIGNABLE ALL PASS" : "PAGES DESIGNABLE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
