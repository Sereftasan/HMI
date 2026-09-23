using System;
using System.Collections.Generic;
using System.ComponentModel;
using Kaban.Core.Enums;

namespace Kaban.Core.Models
{
    [TypeConverter(typeof(PlcTriggerSorter))]
    public class PlcTrigger
    {
        public TriggerType TriggerType { get; set; } = TriggerType.None;
        public string DescriptionKey { get; set; } = "";
        public string MonitorAddress { get; set; } = "";
        public DataTypeEnum MonitorDataType { get; set; } = DataTypeEnum.Bin_16;

        public double ValA { get; set; } = 0;
        public ComparisonOperator OpA { get; set; } = ComparisonOperator.None;
        public ComparisonOperator OpB { get; set; } = ComparisonOperator.None;
        public double ValB { get; set; } = 0;

        public override string ToString()
        {
            if (TriggerType == TriggerType.None || string.IsNullOrEmpty(MonitorAddress)) return "None";
            if (TriggerType == TriggerType.ON) return MonitorAddress + " is True";
            if (TriggerType == TriggerType.OFF) return MonitorAddress + " is False";

            if (TriggerType == TriggerType.Range)
            {
                // Equation: 0 < d150 <= 25
                string part1 = OpA != ComparisonOperator.None ? $"{ValA} {GetOpSign(OpA)} " : "";
                string part2 = OpB != ComparisonOperator.None ? $" {GetOpSign(OpB)} {ValB}" : "";
                return $"{part1}{MonitorAddress}{part2}";
            }
            return "None";
        }

        private string GetOpSign(ComparisonOperator op)
        {
            switch (op)
            {
                case ComparisonOperator.Greater: return ">";
                case ComparisonOperator.GreaterOrEqual: return ">=";
                case ComparisonOperator.Less: return "<";
                case ComparisonOperator.LessOrEqual: return "<=";
                case ComparisonOperator.Equal: return "==";
                case ComparisonOperator.NotEqual: return "!=";
                default: return "";
            }
        }

        public bool Check(Func<string, int> lookup)
        {
            if (TriggerType == TriggerType.None || string.IsNullOrEmpty(MonitorAddress)) return true;

            int val = lookup(MonitorAddress);

            // ON triggers if value is not zero, OFF triggers if value is zero
            if (TriggerType == TriggerType.ON) return val != 0;
            if (TriggerType == TriggerType.OFF) return val == 0;

            if (TriggerType == TriggerType.Range)
            {
                bool resultA = true;
                if (OpA != ComparisonOperator.None) resultA = Compare(ValA, OpA, val);

                bool resultB = true;
                if (OpB != ComparisonOperator.None) resultB = Compare(val, OpB, ValB);

                return resultA && resultB;
            }

            return true;
        }

        private bool Compare(double v1, ComparisonOperator op, double v2)
        {
            switch (op)
            {
                case ComparisonOperator.Greater: return v1 > v2;
                case ComparisonOperator.GreaterOrEqual: return v1 >= v2;
                case ComparisonOperator.Less: return v1 < v2;
                case ComparisonOperator.LessOrEqual: return v1 <= v2;
                case ComparisonOperator.Equal: return v1 == v2;
                case ComparisonOperator.NotEqual: return v1 != v2;
                default: return true;
            }
        }
    }

    public class PlcTriggerSorter : ExpandableObjectConverter
    {
        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
        {
            var props = base.GetProperties(context, value, attributes);
            string[] orderedList = { "TriggerType", "ValA", "OpA", "MonitorAddress", "MonitorDataType", "OpB", "ValB", "DescriptionKey" };
            return props.Sort(orderedList);
        }
    }
}   
