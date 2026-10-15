using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

// 15 October: "It doesn't work when I add visibility condition still" - Visual Studio wrote a machine's own
// condition (the Mullion Notching option, bit 20) into Options.Designer.cs as
// "Kaban.Core.Enums.SystemCondition.1048576", which does not compile (CS0119, CS0201, CS1002): it writes a
// flag enum's value by its members' NAMES, and the machine bits had none. They are named now
// (MachineBit16 ... MachineBit63); what each means is still the machine's, named by MachineConditionMap.
// The serializer is the one Visual Studio uses for an enum (System.Design's EnumCodeDomSerializer).
class CDC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }

    // what the designer writes for a value
    static string Code(SystemCondition v) {
        var design = Assembly.Load("System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        var managerType = design.GetType("System.ComponentModel.Design.Serialization.DesignerSerializationManager");
        var serType = design.GetType("System.ComponentModel.Design.Serialization.EnumCodeDomSerializer");
        var manager = Activator.CreateInstance(managerType);
        using ((IDisposable)managerType.GetMethod("CreateSession").Invoke(manager, null)) {
            var ser = Activator.CreateInstance(serType, true);
            var e = (CodeExpression)serType.GetMethod("Serialize", new[] { typeof(System.ComponentModel.Design.Serialization.IDesignerSerializationManager), typeof(object) })
                                           .Invoke(ser, new object[] { manager, v });
            var w = new StringWriter();
            new Microsoft.CSharp.CSharpCodeProvider().GenerateCodeFromExpression(e, w, new CodeGeneratorOptions());
            return w.ToString();
        }
    }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        MachineConditionMap.Clear();
        MachineConditionMap.Register(SystemCondition.ManualMode, 1, "M100", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        MachineConditionMap.RegisterMachineBit(1L << 19, "FA2010.ClampingMouldOption", "Clamping Mould Option", 1, "L27", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 1);
        MachineConditionMap.RegisterMachineBit(1L << 20, "FA2010.MullionNotchingOption", "Mullion Notching Option", 1, "L5", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 1);

        Console.WriteLine("--- what Visual Studio writes into a page ---");
        var one = Code((SystemCondition)(1L << 20));
        Check("a machine's condition by name: SystemCondition.MachineBit20 (it was SystemCondition.1048576)",
              one == "Kaban.Core.Enums.SystemCondition.MachineBit20", one);
        var mixed = Code(SystemCondition.ManualMode | (SystemCondition)((1L << 19) | (1L << 20)));
        Check("  several together, a shared one among them: their names or-ed", mixed.Contains("SystemCondition.ManualMode") &&
              mixed.Contains("SystemCondition.MachineBit19") && mixed.Contains("SystemCondition.MachineBit20") && mixed.Contains("|"), mixed);
        var bad = new List<string>();
        for (int b = 0; b < 64; b++) {
            var c = Code((SystemCondition)(1L << b));
            if (!System.Text.RegularExpressions.Regex.IsMatch(c, @"^Kaban\.Core\.Enums\.SystemCondition\.[A-Za-z]\w*$") && !(b >= 5 && b < 16)) bad.Add(b + ": " + c);
        }
        Check("  every machine bit 16 - 63 a name (no number where a name must be)", bad.Count == 0, string.Join(" | ", bad));
        Check("  None as before", Code(SystemCondition.None) == "Kaban.Core.Enums.SystemCondition.None", Code(SystemCondition.None));

        Console.WriteLine("--- the names do not make them shared conditions ---");
        Check("the shared ones: SafetyOK, ManualMode, AutoMode, AdminUser, MachineIsRunning",
              string.Join(",", ConditionBits.Shared) == "SafetyOK,ManualMode,AutoMode,AdminUser,MachineIsRunning", string.Join(",", ConditionBits.Shared));
        Check("  MachineBit16 and MachineBit63 are a machine's; ManualMode is not",
              ConditionBits.IsMachineBit(SystemCondition.MachineBit16) && ConditionBits.IsMachineBit(SystemCondition.MachineBit63) &&
              !ConditionBits.IsMachineBit(SystemCondition.ManualMode) && !ConditionBits.IsShared(SystemCondition.MachineBit20));
        Check("named by the machine: 'Mullion Notching Option', not the member's description",
              MachineConditionMap.NameOf(SystemCondition.MachineBit20) == "Mullion Notching Option", MachineConditionMap.NameOf(SystemCondition.MachineBit20));
        var text = new Kaban.UI.Design.ConditionTextConverter().ConvertTo(null, null, SystemCondition.ManualMode | SystemCondition.MachineBit20, typeof(string)) as string;
        Check("  the Properties window: 'Manual Mode Active, Mullion Notching Option'", text == "Manual Mode Active, Mullion Notching Option", text);
        var said = MachineConditionMap.Describe(SystemCondition.MachineBit19);
        Check("  an Admin's tooltip: its name and its rule (L27 = 1)", said.StartsWith("Clamping Mould Option (") && said.Contains("L27"), said);
        Check("a bit the machine never registered: 'Condition <n>' as before", MachineConditionMap.NameOf(SystemCondition.MachineBit40) == "Condition " + (1L << 40),
              MachineConditionMap.NameOf(SystemCondition.MachineBit40));
        Check("the registered conditions: the shared one, then the two machine ones by bit",
              string.Join(",", MachineConditionMap.All().Select(e => e.Name)) == "Manual Mode Active,Clamping Mould Option,Mullion Notching Option",
              string.Join(",", MachineConditionMap.All().Select(e => e.Name)));

        Console.WriteLine("--- FA2010's pages as written now ---");
        var fa = Assembly.LoadFrom("Kaban.Machines.FA2010.exe");
        Func<string, Control> page = n => (Control)Activator.CreateInstance(fa.GetType("Kaban.Machines.FA2010.Screens." + n));
        Func<Control, IEnumerable<Control>> all = null;
        all = c => c.Controls.Cast<Control>().SelectMany(k => new[] { k }.Concat(all(k)));
        var opts = all(page("Parameters.Options")).OfType<CheckBoxHMI>().Where(c => c.EnableConditions != SystemCondition.None).ToList();
        Check("Options: the five 'with blade' switches enabled by the Mullion Notching option (MachineBit20)",
              opts.Count == 5 && opts.All(c => c.EnableConditions == SystemCondition.MachineBit20), opts.Count + ": " + string.Join(",", opts.Select(c => c.Name)));
        var moulds = all(page("Manual.ManualClamping_Moulds")).OfType<PneumaticPositioningHMI>().Where(r => r.VisibilityConditions != SystemCondition.None).ToList();
        Check("Clamping Moulds: the 10 back moulds shown by the Clamping Mould option (MachineBit19)",
              moulds.Count == 10 && moulds.All(r => r.VisibilityConditions == SystemCondition.MachineBit19 && r.TitleTextKey.Contains("Back")), moulds.Count.ToString());

        Console.WriteLine(fail == 0 ? "CONDITION DESIGNER CODE ALL PASS" : "CONDITION DESIGNER CODE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
