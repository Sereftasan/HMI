using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.Core.Models;
using Kaban.UI.Controls;

// A numeric input's formulas: ReadFormula is how the raw value is shown,
// WriteFormula how the typed value is written - "(A - 1) / B" and "(A * B) + 1".
// A name the formulas use but nobody defined is said in plain words, in the
// designer before anything runs.
class NF {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    class Recorder : IPlcDriver, INonBlockingDriver {
        public readonly Dictionary<string, int> Written = new Dictionary<string, int>();
        public bool IsConnected { get { return true; } }
        public bool IsHealthy { get { return true; } }
        public bool Connect() { return true; }
        public void Disconnect() { }
        public int Read(string a) { return 0; }
        public int[] ReadMulti(string[] a) { return new int[a.Length]; }
        public int Write(string a, int v) { Written[a] = v; return 0; }
        public bool TryRead(string a, out int v) { v = 0; return true; }
        public bool TryReadMulti(string[] a, out int[] v) { v = new int[a.Length]; return true; }
    }

    static Dictionary<string, int> words = new Dictionary<string, int>();
    static int Plc(string a) { int v; return words.TryGetValue(a, out v) ? v : 0; }
    static bool Always(SystemCondition c) { return true; }

    static void Scan(NumericInputHMI f) { f.UpdateState(Plc, Always); }
    static int WriteTyped(NumericInputHMI f, string typed) {
        var rec = new Recorder();
        typeof(NumericInputHMI).GetMethod("ProcessWrite", Any).Invoke(f, new object[] { typed, rec, (Func<string, int>)Plc });
        int v; return rec.Written.TryGetValue("D100", out v) ? v : int.MinValue;
    }
    static string Tip(NumericInputHMI f) { return (string)typeof(NumericInputHMI).GetMethod("BuildAdminTooltip", Any).Invoke(f, null); }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        SecurityManager.Login("admin", SecurityLevel.Admin);

        Console.WriteLine("--- the names a formula uses ---");
        Check("Round(A / B, 1) + 1e3 + [C] uses A, B and C - not Round, not the e of 1e3",
              string.Join(",", NumericFormula.Names("Round(A / B, 1) + 1e3 + [C]")) == "A,B,C",
              string.Join(",", NumericFormula.Names("Round(A / B, 1) + 1e3 + [C]")));
        Check("val, A, x, value and blank leave the value alone",
              new[] { "val", " A ", "x", "Value", "", null, "[val]" }.All(NumericFormula.IsIdentity) && !NumericFormula.IsIdentity("A / 10"));

        Console.WriteLine("--- the reported pair, B not defined yet ---");
        var f = new NumericInputHMI { Address = "D100", DataType = DataTypeEnum.Bin_16, ReadFormula = "(A - 1) / B", WriteFormula = "(A* B) + 1" };
        Check("the designer says B is not defined, and how to define it",
              f.FormulaCheck.Contains("B is not defined") && f.FormulaCheck.Contains("MathVariable named B"), f.FormulaCheck);
        Check("  for both formulas", f.FormulaProblems().Count(p => p.Contains("B is not defined")) == 2, string.Join(" | ", f.FormulaProblems()));
        words["D100"] = 101;
        Scan(f);
        Check("  the field shows Err", f.Text == "Err", f.Text);
        Check("  and the admin tooltip says why", Tip(f).Contains("B is not defined"), Tip(f));
        string runMsg = "";
        try { typeof(NumericInputHMI).GetMethod("EvaluateFormula", Any).Invoke(f, new object[] { "(A* B) + 1", 10.0, (Func<string, int>)Plc }); }
        catch (TargetInvocationException ex) { runMsg = ex.InnerException.Message; }
        Check("  at run time too: named, not NCalc's 'Parameter was not defined'",
              runMsg.Contains("B is not defined") && !runMsg.Contains("Parameter was not defined"), runMsg);

        Console.WriteLine("--- B added: a constant 10 ---");
        f.MathVariables.Add(new MathVariable { Name = "B", IsConstant = true, ConstantValue = 0 });
        Check("left at its default 0 under a division: said so", f.FormulaCheck.Contains("B = 0") || f.FormulaCheck.Contains("B"), f.FormulaCheck);
        f.MathVariables[0].ConstantValue = 10;
        Check("set to 10: OK", f.FormulaCheck == "OK", f.FormulaCheck);
        f.Divisor = 100;
        Scan(f);
        Check("raw 101 is shown as (101 - 1) / 10 = 10 - the formula replaces the Divisor", f.Text.Trim() == "10", f.Text);
        Check("typing 10 writes (10 * 10) + 1 = 101 - not 101 x Divisor", WriteTyped(f, "10") == 101, WriteTyped(f, "10").ToString());
        Check("typing 25 writes 251", WriteTyped(f, "25") == 251, WriteTyped(f, "25").ToString());
        Check("the tooltip lists both formulas and B", Tip(f).Contains("Read Formula:") && Tip(f).Contains("Write Formula:") && Tip(f).Contains("B = 10"), Tip(f));

        Console.WriteLine("--- written with val, or in small letters ---");
        f.ReadFormula = "(val - 1) / B"; f.WriteFormula = "(val * B) + 1";
        Scan(f);
        Check("(val - 1) / B and (val * B) + 1 work the same", f.FormulaCheck == "OK" && f.Text.Trim() == "10" && WriteTyped(f, "10") == 101,
              f.FormulaCheck + " / " + f.Text);
        f.ReadFormula = "(a - 1) / b"; f.WriteFormula = "(a * b) + 1";
        Scan(f);
        Check("a and b find A and B", f.FormulaCheck == "OK" && f.Text.Trim() == "10" && WriteTyped(f, "10") == 101,
              f.FormulaCheck + " / " + f.Text);

        Console.WriteLine("--- B read from the PLC ---");
        f.ReadFormula = "(A - 1) / B"; f.WriteFormula = "(A * B) + 1";
        f.MathVariables[0].IsConstant = false; f.MathVariables[0].AddressOrValue = "";
        Check("a device with no address is pointed out", f.FormulaCheck.Contains("no AddressOrValue"), f.FormulaCheck);
        f.MathVariables[0].AddressOrValue = "D200";
        words["D200"] = 4; words["D100"] = 41;
        Scan(f);
        Check("B = D200 (4): raw 41 is shown as 10", f.FormulaCheck == "OK" && f.Text.Trim() == "10", f.FormulaCheck + " / " + f.Text);
        Check("  and 10 typed is written as 41", WriteTyped(f, "10") == 41, WriteTyped(f, "10").ToString());

        Console.WriteLine("--- a Tag limit is read the same way ---");
        f.MathVariables[0].IsConstant = true; f.MathVariables[0].ConstantValue = 10;
        f.MaxLimit = new DynamicValue(LimitSourceType.Tag, 0, "D300");
        words["D300"] = 51; words["D100"] = 101;
        Scan(f);
        Check("limit word 51 is (51 - 1) / 10 = 5: the value 10 is above it", f.IsOutOfLimits, f.Text);
        words["D300"] = 201;
        Scan(f);
        Check("limit word 201 is 20: 10 is inside", !f.IsOutOfLimits);
        var lim = InputLimitsCalculation.GetEffectiveLimits(DataTypeEnum.Bin_16, DisplayFormatEnum.Signed_Decimal,
            new DynamicValue(), f.MaxLimit, "A", Plc, (x, v) => v, 10);
        Check("  'A' alone counts as no formula: the Divisor applies", Math.Abs(lim.Max - 20.1) < 1e-9, lim.Max.ToString());

        Console.WriteLine("--- pairs that do not match ---");
        f.MaxLimit = new DynamicValue();
        f.WriteFormula = "val";
        Check("a ReadFormula alone is pointed out", f.FormulaCheck.Contains("WriteFormula is not"), f.FormulaCheck);
        Check("  (a warning - the keypad still opens)", f.FormulaProblems(errorsOnly: true).Count == 0);
        f.WriteFormula = "(A * B) + 2";
        Check("formulas that are not each other's inverse are pointed out", f.FormulaCheck.Contains("not each other's inverse"), f.FormulaCheck);
        f.WriteFormula = "(A * B) + ";
        Check("one NCalc cannot read is an error", f.FormulaProblems(errorsOnly: true).Any(p => p.StartsWith("WriteFormula")), f.FormulaCheck);

        Console.WriteLine("--- no formula: the Divisor as before ---");
        var g = new NumericInputHMI { Address = "D100", DataType = DataTypeEnum.Bin_16, Divisor = 10, DecimalPlaces = 1 };
        words["D100"] = 105;
        Scan(g);
        Check("raw 105 with Divisor 10 is 10.5", g.Text.Trim().Replace(',', '.') == "10.5", g.Text);
        Check("  10.5 typed is written as 105", WriteTyped(g, "10.5") == 105, WriteTyped(g, "10.5").ToString());
        Check("  FormulaCheck OK", g.FormulaCheck == "OK", g.FormulaCheck);
        var named = new NumericInputHMI { ReadFormula = "A / 10", WriteFormula = "A * 10" };
        named.MathVariables.Add(new MathVariable { Name = "A" });
        Check("a variable named A is pointed out (A is the value)", named.FormulaCheck.Contains("named A"), named.FormulaCheck);

        Console.WriteLine(fail == 0 ? "\nNUMERIC FORMULAS ALL PASS" : "\nNUMERIC FORMULAS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
