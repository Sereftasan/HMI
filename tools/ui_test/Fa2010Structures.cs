using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Forms;

// 22 October: "Can you also create the structure labels of all the tables on the uploaded excel file
// 'Profil_İşlem_İşListesi' page for me (apart from 'PLC DEVICE LIST' table...)". FA2010's StructuredTypes.csv
// has a structured data type for each table, its PlcLabels.csv a label for each - an array where the table
// has rows. And a new row of the PLC Monitor fills itself in from the label list (its address or its label typed).
class FS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        d = d ?? "";
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static Type MonType = typeof(SimulationMonitorForm);
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
        var p = t.GetProperty(n, Any); return p.GetValue(o is Type ? null : o, null);
    }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length &&
                                              m.GetParameters().Select((p, i) => a[i] == null || p.ParameterType.IsInstanceOfType(a[i])).All(x => x));
        return mi.Invoke(o is Type ? null : o, a);
    }
    static IList Rows(object sheet) { return (IList)Get(sheet, "Rows"); }
    static string Cell(DataGridView g, int r, int c) { return Convert.ToString(g.Rows[r].Cells[c].Value); }

    /// <summary>The devices a label of the list takes: its first and the one after its last (R devices only).</summary>
    static int[] Span(PlcLabelTable.Entry e)
    {
        var t = StructuredTypes.Find(e.DataType);
        int first = int.Parse(e.Address.Substring(1));
        return new[] { first, first + t.Size * Math.Max(1, StructuredTypes.CountIn(e.DataType)) };
    }

    [STAThread]
    static void Main() {
        string bin = AppDomain.CurrentDomain.BaseDirectory;
        string dir = Path.Combine(Path.GetTempPath(), "fs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        Console.WriteLine("--- FA2010's StructuredTypes.csv ---");
        var types = StructuredTypes.Parse(File.ReadAllLines(Path.Combine(bin, "StructuredTypes.csv")));
        var names = types.Select(t => t.Name).ToList();
        Check("the user's Milling Step, then a type for each table of the Profiles & Operations sheet",
              string.Join(", ", names) == "Milling Step, Profile Parameters, Job ID, Job Position, Job Operation Code, " +
                                          "Milling Operation Parameters, Cutting Operation Parameters, Cutting Step, Mullion Notching Step",
              string.Join(", ", names));
        foreach (var t in types)
            Check("  '" + t.Name + "' (" + t.Size + " devices, " + t.Members.Count + " labels): nothing wrong", StructuredTypes.Problem(t, types) == null,
                  StructuredTypes.Problem(t, types));
        var milling = types.First(t => t.Name == "Milling Step");
        Check("Milling Step as the user made it: the bits at 0.1 and 0.5, X 1, Y 3, Z 5, Speed 12 - 16 devices",
              milling.Size == 16 && milling.Members.Count == 6 &&
              milling.Members.Any(m => m.Name == "Cooling Selection" && m.IsBit && m.Offset == 0 && m.Bit == 1) &&
              milling.Members.Any(m => m.Name == "Motor Selection" && m.IsBit && m.Offset == 0 && m.Bit == 5) &&
              milling.Members.Any(m => m.Name == "X Position" && m.Offset == 1) && milling.Members.Any(m => m.Name == "Speed" && m.Offset == 12));
        Check("  each member its own divisor: the positions and the speed / 100, 2 decimals - the bits none",
              milling.Members.Where(m => !m.IsBit).All(m => m.Divisor == 100 && m.Decimals == 2) &&
              milling.Members.Where(m => m.IsBit).All(m => !m.IsScaled));
        var profile = types.First(t => t.Name == "Profile Parameters");
        Check("Profile Parameters: 50 devices (R25600 ... R25649), the 11 mode bits in its first word",
              profile.Size == 50 && profile.Members.Count(m => m.IsBit && m.Offset == 0) == 11 &&
              profile.Members.Any(m => m.Name == "Cut Speed 10" && m.Offset == 49));
        Check("  the conveyor's speed (%) not scaled, the dimensions / 100",
              !profile.Members.First(m => m.Name == "Conveyor Speed").IsScaled &&
              profile.Members.First(m => m.Name == "Profile Recognition Width").Divisor == 100);
        var cutPar = types.First(t => t.Name == "Cutting Operation Parameters");
        Check("Cutting Operation Parameters: 5 devices, its angle xxx.x (/ 10, 1 decimal)",
              cutPar.Size == 5 && cutPar.Members[0].Name == "Cut Angle" && cutPar.Members[0].Divisor == 10 && cutPar.Members[0].Decimals == 1);
        var step = types.First(t => t.Name == "Cutting Step");
        Check("Cutting Step: 10 devices, the clamps bits 5.0 and 5.1",
              step.Size == 10 && step.Members.Any(m => m.Name == "Left Clamp" && m.Offset == 5 && m.Bit == 0) &&
              step.Members.Any(m => m.Name == "Right Clamp" && m.Offset == 5 && m.Bit == 1));
        Check("the 32-bit job list columns: Job ID and Job Position 2 devices (a Double Word)",
              types.First(t => t.Name == "Job ID").Size == 2 && types.First(t => t.Name == "Job Position").Members[0].DataType == "Double Word [Signed]");
        var again = StructuredTypes.Parse(StructuredTypes.Serialize(types));
        Check("saved and read again: the same types, sizes, members and divisors",
              again.Count == types.Count && again.Zip(types, (a, b) => a.Name == b.Name && a.Size == b.Size && a.Members.Count == b.Members.Count &&
                  a.Members.Zip(b.Members, (x, y) => x.Name == y.Name && x.Offset == y.Offset && x.Divisor == y.Divisor && x.Decimals == y.Decimals).All(z => z)).All(z => z));

        Console.WriteLine("--- FA2010's PlcLabels.csv ---");
        File.Copy(Path.Combine(bin, "StructuredTypes.csv"), Path.Combine(dir, "StructuredTypes.cfg"));
        StructuredTypes.FilePath = Path.Combine(dir, "StructuredTypes.cfg");
        StructuredTypes.Reload();
        PlcLabelTable.Load(Path.Combine(bin, "PlcLabels.csv"));
        var pcs = PlcLabelTable.EntriesFor(0).Where(e => StructuredTypes.Find(e.DataType) != null).ToList();
        Check("a label for each table (the milling steps of two units, and their spares)", pcs.Count == 12,
              string.Join(", ", pcs.Select(e => e.Label)));
        var expected = new Dictionary<string, int> {
            { "PC_ProfilParametreleri", 0 }, { "PC_IsListesi_IslemID", 500 }, { "PC_IsListesi_Pozisyon", 500 }, { "PC_IsListesi_IslemKodu", 500 },
            { "PC_FrezeIslemParametreleri", 100 }, { "PC_KesimIslemParametreleri", 10 }, { "PC_Unite1_FrezeAdimlari", 400 },
            { "PC_KesimAdimlari", 30 }, { "PC_OrtakayitKertmeAdimlari", 30 } };
        foreach (var kv in expected)
        {
            var e = PlcLabelTable.ByLabel(kv.Key);
            Check("  " + kv.Key + ": its type found, " + (kv.Value == 0 ? "one" : kv.Value + " elements"),
                  e != null && StructuredTypes.Find(e.DataType) != null && StructuredTypes.CountIn(e.DataType) == kv.Value, e == null ? "missing" : e.DataType);
        }
        var r = pcs.Where(e => e.Address.StartsWith("R") && e.Label != "PC_OrtakayitKertmeAdimlari").Select(e => new { e.Label, S = Span(e) }).OrderBy(x => x.S[0]).ToList();
        var clash = r.Zip(r.Skip(1), (a, b) => a.S[1] > b.S[0] ? a.Label + " / " + b.Label : null).Where(x => x != null).ToList();
        Check("the R devices' tables do not run into one another (the mullion notching steps share the cutting steps' devices)",
              clash.Count == 0, string.Join(", ", clash));
        Check("  R25600 + 50 = the cutting parameters' R25650, + 10 x 5 = the cutting steps' R25700, + 30 x 10 = the milling parameters' R26000",
              Span(PlcLabelTable.ByLabel("PC_ProfilParametreleri"))[1] == 25650 && Span(PlcLabelTable.ByLabel("PC_KesimIslemParametreleri"))[1] == 25700 &&
              Span(PlcLabelTable.ByLabel("PC_KesimAdimlari"))[1] == 26000);

        Console.WriteLine("--- the cutting steps as a table ---");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(23);
        PlcManager.Instance.RegisterDriver(23, sim);
        PlcManager.Instance.RegisterStation(23, "Bench", SecurityLevel.Operator);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        ConnectionMonitor.ReportRead(23, true);
        for (int i = 0; i < 300; i++) sim.Write("R" + (25700 + i), 0);
        sim.Write("R25710", 4); sim.Write("R25711", 15205); sim.Write("R25713", 4500); sim.Write("R25715", 2);
        var form = new SimulationMonitorForm { Width = 1200, Height = 900 };
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 23);
        ((TabControl)Get(form, "_tabs")).SelectedIndex = monitors.IndexOf(mon);
        var sh = Call(form, "EnsureSheet", mon);
        var steps = Call(MonType, "FromLabelList", mon, PlcLabelTable.ByLabel("PC_KesimAdimlari"));
        var notch = Call(MonType, "FromLabelList", mon, PlcLabelTable.ByLabel("PC_OrtakayitKertmeAdimlari"));
        Check("PC_KesimAdimlari from the label list: R25700, Cutting Step, 30 - its own label",
              (string)Get(steps, "Address") == "R25700" && (string)Get(steps, "DataTypeOverride") == "Cutting Step" && (int)Get(steps, "ArrayCount") == 30 &&
              (string)Get(steps, "LabelOverride") == "");
        Check("  PC_OrtakayitKertmeAdimlari at the same devices: its label kept on the row",
              (string)Get(notch, "Address") == "R25700" && (string)Get(notch, "LabelOverride") == "PC_OrtakayitKertmeAdimlari");
        Rows(sh).Insert(Rows(sh).Count - 1, steps);
        Call(form, "ShowInTable", mon, sh, steps);
        Call(form, "RefreshValues"); Application.DoEvents();
        var block = ((IList)Get(sh, "Blocks")).Cast<object>().First();
        var g = (DataGridView)Get(block, "Grid");
        var heads = g.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText.Split('\n')[0]).ToList();
        Check("its table: the step's members as the columns, 30 lines",
              string.Join(", ", heads) == "#, Operation Code, Robot 1, Robot 2, Angle, V Cut Y, Left Clamp, Right Clamp" && g.RowCount == 30, string.Join(", ", heads));
        Check("  step 1: code 4, Robot 1 152.05, the angle 45.00, the right clamp on",
              Cell(g, 1, 1) == "4" && Cell(g, 1, 2) == "152.05" && Cell(g, 1, 4) == "45.00" && Cell(g, 1, 6) == "0" && Cell(g, 1, 7) == "1",
              string.Join(" ", Enumerable.Range(1, 7).Select(c => Cell(g, 1, c))));
        form.Close();

        Console.WriteLine("--- a new row filled in from the label list ---");
        using (var ed = new PlcRowEditorForm("Bench", "", "Bit", "", "", "Address", isNew: true, station: 23))
        {
            var address = (TextBox)Get(ed, "_address");
            address.Text = "R25700";
            Check("R25700 typed: PC_KesimAdimlari, Cutting Step, 30 filled in",
                  ed.RowLabel == "PC_KesimAdimlari" && ed.RowDataType == "Cutting Step" && ed.ArrayCount == 30, ed.RowLabel + " " + ed.RowDataType + " " + ed.ArrayCount);
            address.Text = "R2570";
            Check("  R2570 names no label: the fields back as they were (no label, Bit, 1)",
                  ed.RowLabel == "" && ed.RowDataType == "Bit" && ed.ArrayCount == 1, ed.RowLabel + " " + ed.RowDataType + " " + ed.ArrayCount);
        }
        using (var ed = new PlcRowEditorForm("Bench", "", "Bit", "", "", "Label", isNew: true, station: 23))
        {
            ((TextBox)Get(ed, "_label")).Text = "PC_IsListesi_Pozisyon";
            Check("the label PC_IsListesi_Pozisyon typed: D6000, Job Position, 500 filled in",
                  ed.RowAddress == "D6000" && ed.RowDataType == "Job Position" && ed.ArrayCount == 500, ed.RowAddress + " " + ed.RowDataType + " " + ed.ArrayCount);
        }
        using (var ed = new PlcRowEditorForm("Bench", "", "Bit", "", "", "Address", isNew: true, station: 23))
        {
            ((ComboBox)Get(ed, "_dataType")).Text = "Word [Signed]";
            ((TextBox)Get(ed, "_address")).Text = "D5000";
            Check("a data type chosen by hand stays: only the label filled in",
                  ed.RowLabel == "PC_IsListesi_IslemID" && ed.RowDataType == "Word [Signed]" && ed.ArrayCount == 1, ed.RowLabel + " " + ed.RowDataType + " " + ed.ArrayCount);
        }
        using (var ed = new PlcRowEditorForm("Bench", "PC_AlarmReset", "Bit", "M102", "", "Address", station: 23))
        {
            ((TextBox)Get(ed, "_address")).Text = "R25700";
            Check("an existing row's fields are not changed by it", ed.RowLabel == "PC_AlarmReset" && ed.RowDataType == "Bit", ed.RowLabel + " " + ed.RowDataType);
        }

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nFA2010 STRUCTURES ALL PASS" : "\nFA2010 STRUCTURES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
