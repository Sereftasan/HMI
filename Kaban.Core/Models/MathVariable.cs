using System.ComponentModel;

namespace Kaban.Core.Models
{
    /// <summary>
    /// A named variable ("A", "B", "C"...) usable inside a WriteFormula/ReadFormula
    /// NCalc expression on NumericInputHMI. Restored after being stripped from the
    /// control - this is what lets a write formula reference other live PLC tags
    /// or fixed constants (e.g. "val * B + C").
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class MathVariable
    {
        [Description("The name used inside the formula, e.g. 'B'.")]
        public string Name { get; set; } = "B";

        [Description("If true, uses ConstantValue. If false, reads AddressOrValue as a live PLC tag.")]
        public bool IsConstant { get; set; } = true;

        [Description("Fixed value used when IsConstant is true.")]
        public double ConstantValue { get; set; } = 0;

        [Description("PLC tag address to read when IsConstant is false (e.g. 'D200').")]
        public string AddressOrValue { get; set; } = "";

        public double GetConstantValue() => ConstantValue;

        public override string ToString() => IsConstant ? $"{Name} = {ConstantValue}" : $"{Name} = [{AddressOrValue}]";
    }
}

