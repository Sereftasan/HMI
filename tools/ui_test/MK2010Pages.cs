using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.Machines.MK2010.Screens.Home;
using Kaban.Machines.MK2010.Screens.Manual;
using Kaban.Machines.MK2010.Screens.Parameters;

// 11 November: "can you add "MK2010" project from "_MK2010" as well" - the old program's MK2010 pages
// (tools/legacy_mk2010_pages.py, its --check against tools/legacy/MK2010): Home (the job), Manual Control,
// References and the Machine Parameters tabs; the project builds its own MK2010 program.
class MKP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }
    static string Bits(ButtonHMI b) { return string.Join(",", b.BitActions.Select(a => a.Address + ":" + a.Action)); }
    static ButtonHMI Btn(Control page, string addr) { return Tree(page).OfType<ButtonHMI>().FirstOrDefault(b => b.BitActions.Any(a => a.Address == addr)); }

    static void Shot(Control page, string path) {
        var f = new Form { ClientSize = new Size(Math.Min(1270, page.Width), Math.Min(1000, page.Height)), StartPosition = FormStartPosition.Manual,
                           Location = new Point(0, 0) };
        f.Controls.Add(page);
        f.Show();
        var until = DateTime.Now.AddMilliseconds(500);
        while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
        using (var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height)) {
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(f.PointToScreen(Point.Empty), Point.Empty, bmp.Size);
            bmp.Save(path);
        }
        f.Controls.Remove(page);
        f.Close();
    }

    [STAThread]
    static void Main(string[] args) {
        AuditLogger.LogDirectory = Path.Combine(Path.GetTempPath(), "mkp_" + Guid.NewGuid().ToString("N"));
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();

        Console.WriteLine("--- Home (ucJobNew) ---");
        var home = new HomeJob();
        var disp = Tree(home).OfType<NumericDisplayHMI>().ToList();
        Check("the heads' temperatures D215 ... D218 and their actual D381 ... D384 (raw / 10, °C)",
              new[] { "D215", "D218", "D381", "D384" }.All(a => disp.Any(d => d.Address == a && d.Divisor == 10 && d.Suffix == "°C")));
        Check("  the axes' target D32 / D34 / D36 and actual D50 / D52 / D54; melting D349 / TN58, welding D350 / TN6",
              new[] { "D32", "D34", "D36", "D50", "D52", "D54", "D349", "TN58", "D350", "TN6" }.All(a => disp.Any(d => d.Address == a)));
        var nums = Tree(home).OfType<NumericInputHMI>().ToList();
        var wx = nums.FirstOrDefault(n => n.Address == "D1200");
        Check("the job's Width X D1200 (32-bit / 10), its limits the PLC's D144 / D146; Width Y D1202 (D148 / D150); Quantity D334",
              wx != null && wx.Divisor == 10 && wx.DataType == DataTypeEnum.Bin_32 && wx.MinLimit.Type == LimitSourceType.Tag && wx.MinLimit.TagAddress == "D144" &&
              wx.MaxLimit.TagAddress == "D146" && nums.Any(n => n.Address == "D1202" && n.MaxLimit.TagAddress == "D150") && nums.Any(n => n.Address == "D334"));
        Check("  an operator's to type", nums.All(n => n.RequiredSecurity == SecurityLevel.Operator));

        Console.WriteLine("--- Manual Control, References ---");
        var man = new ManualControl();
        Check("Axis X: Back M41, Forward M40 (momentary, lit by themselves), position D50 / 10",
              Bits(Btn(man, "M41")) == "M41:Momentary" && Btn(man, "M41").Lamp1Address == "M41" && Btn(man, "M41").Direction == ButtonGlyphs.Direction.Left &&
              Btn(man, "M40") != null && Tree(man).OfType<NumericDisplayHMI>().Any(d => d.Address == "D50" && d.Divisor == 10));
        var lim = Tree(man).OfType<LampHMI>().FirstOrDefault(l => l.Address == "X0006");
        Check("  its limit X0006 lit while the input is OFF (the old negative lamp)", lim != null && lim.OffColor.G > lim.OffColor.R && lim.OnColor.R == lim.OnColor.G);
        Check("the units: Supporting Plate M101, Heater Plates M104, Conveyor M106 ... alternate", new[] { "M101", "M102", "M103", "M104", "M196", "M34", "M105", "M100", "M106", "M109", "M108" }
              .All(a => Btn(man, a) != null && Bits(Btn(man, a)) == a + ":Alternate"));
        var lamps = Tree(man).OfType<LampHMI>().Select(l => l.Address).ToList();
        Check("  their sensors, head by head (X0031 ... X0034, X0011 ... X0014, X0035 ... X0040 ...)",
              new[] { "X0031", "X0034", "X0011", "X0014", "X0035", "X0040", "X0025", "X0030", "X0054", "X0057", "X0064", "X0062" }.All(lamps.Contains));
        var refs = new ManualReference();
        var all = Tree(refs).OfType<ButtonHMI>().FirstOrDefault(b => b.FallbackText == "Reference All");
        Check("References: X / Y1 / Y2 M30 / M31 / M32, Reference All SETs the three, OK lamps M22 / M20 / M21",
              Btn(refs, "M30") != null && all != null && Bits(all) == "M30:SET,M31:SET,M32:SET" &&
              new[] { "M22", "M20", "M21" }.All(a => Tree(refs).OfType<LampHMI>().Any(l => l.Address == a)), all == null ? "-" : Bits(all));

        Console.WriteLine("--- Machine Parameters ---");
        var cal = new ParamCalibration();
        var calAll = Tree(cal).OfType<ButtonHMI>().FirstOrDefault(b => b.FallbackText == "Calibrate All");
        Check("Calibration: real values D154 / D156 / D158, Calibrate M91 / M92 / M93, Calibrate All the three",
              new[] { "D154", "D156", "D158" }.All(a => Tree(cal).OfType<NumericInputHMI>().Any(n => n.Address == a)) && calAll != null &&
              Bits(calAll) == "M91:SET,M92:SET,M93:SET");
        var sys = new ParamSystem();
        var opts = Tree(sys).OfType<CheckBoxHMI>().Select(o => o.Address).ToList();
        Check("System Parameters: 16 values, the options M1022 M1000 M1026 M1002 M1032 M1024 M1016 M1028",
              Tree(sys).OfType<NumericInputHMI>().Count() == 16 && string.Join(",", opts) == "M1022,M1000,M1026,M1002,M1032,M1024,M1016,M1028", string.Join(",", opts));
        var sys2 = new ParamSystem2();
        Check("System Parameters 2: 24 X / Y values, an Admin's (the old password page)", Tree(sys2).OfType<NumericInputHMI>().Count() == 24 &&
              Tree(sys2).OfType<NumericInputHMI>().All(n => n.RequiredSecurity == SecurityLevel.Admin), Tree(sys2).OfType<NumericInputHMI>().Count().ToString());
        var rep = new ParamReports();
        Check("Reports: the hours D296 / D226 as they are, the minutes D298 / D228 (raw / 60)",
              Tree(rep).OfType<NumericDisplayHMI>().Any(d => d.Address == "D296" && d.Divisor == 1) &&
              Tree(rep).OfType<NumericDisplayHMI>().Any(d => d.Address == "D298" && d.Divisor == 60));
        var prod = new ParamProduction();
        Check("Production Parameters: the temperatures D206, D211 ... D214, the options M1008 M1001 M1004 M1034 M1030",
              Tree(prod).OfType<NumericInputHMI>().Count() == 6 && Tree(prod).OfType<CheckBoxHMI>().Count() == 5);

        var pages = new Control[] { home, man, refs, prod, cal, sys, sys2, new ParamDemo(), rep };
        var overlaps = new List<string>();
        foreach (var pg in pages) {
            var cs = pg.Controls.Cast<Control>().ToList();
            for (int i = 0; i < cs.Count; i++)
                for (int j = i + 1; j < cs.Count; j++)
                    if (cs[i].Bounds.IntersectsWith(cs[j].Bounds)) overlaps.Add(pg.Name + ": " + cs[i].Name + " / " + cs[j].Name);
        }
        Check("no two controls of a page on each other; every page fits the window", overlaps.Count == 0 && pages.All(p => p.Width <= 1180),
              string.Join("; ", overlaps.Take(4)) + " " + string.Join(" ", pages.Select(p => p.Name + " " + p.Width)));

        Console.WriteLine("--- in Turkish ---");
        LanguageManager.LoadMachineTranslations(Path.Combine("MK2010", "Translations.csv"));
        var keys = new List<string>();
        foreach (var pg in pages)
            foreach (var c in Tree(pg))
                foreach (var pn in new[] { "TranslationKey" }) {
                    var pi = c.GetType().GetProperty(pn);
                    var v = pi == null ? null : pi.GetValue(c, null) as string;
                    if (!string.IsNullOrWhiteSpace(v) && !System.Text.RegularExpressions.Regex.IsMatch(v, "^[XYZ][12]?$")) keys.Add(v);
                }
        keys.AddRange(new[] { "Machine_MK2010", "Tab_MK2010_ManualControl", "Tab_MK2010_ManualReference", "Tab_MK2010_ParamProduction",
                              "Tab_MK2010_ParamCalibration", "Tab_MK2010_ParamSystem", "Tab_MK2010_ParamSystem2", "Tab_MK2010_ParamDemo",
                              "Tab_MK2010_ParamReports" });
        var missing = keys.Distinct().Where(k => LanguageManager.GetStringIn("TR", k) == null).ToList();
        Check("every caption of the pages, their tabs and the machine's name has its Turkish (" + keys.Distinct().Count() + ")", missing.Count == 0,
              string.Join(", ", missing));

        if (args.Length > 0) {
            Shot(new HomeJob(), Path.Combine(args[0], "mk_home.png"));
            Shot(new ManualControl(), Path.Combine(args[0], "mk_manual.png"));
        }
        Console.WriteLine(fail == 0 ? "\nMK2010 PAGES ALL PASS" : "\nMK2010 PAGES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
