using System;
using System.Collections.Generic;
using Kaban.Core.Enums;

namespace Kaban.Core.Helpers
{
    // Functional Model: What the Driver needs
    public class ConditionRule
    {
        public int PlcNumber { get; set; } = 1;
        public string Address { get; set; }
        public DataTypeEnum DataType { get; set; } = DataTypeEnum.Bin_16;
        public ComparisonOperator Operator { get; set; } = ComparisonOperator.Equal;
        public int RequiredValue { get; set; } = 1;
    }

    public class ConditionDefinition : ConditionRule
    {
        public string TagName { get; set; }
        public string Name { get; set; }
    }

    public static class MachineConditionMap
    {
        private static readonly Dictionary<SystemCondition, ConditionRule> _map = new Dictionary<SystemCondition, ConditionRule>();
        private static readonly Dictionary<long, ConditionDefinition> _machineBits = new Dictionary<long, ConditionDefinition>();

        // System: (Enum, PLC, Address)
        public static void Register(SystemCondition cond, int plc, string addr)
            => Register(cond, plc, addr, DataTypeEnum.Bin_16, ComparisonOperator.Equal, 1);

        public static void Register(SystemCondition cond, int plc, string addr, DataTypeEnum type, ComparisonOperator op, int val)
        {
            _map[cond] = new ConditionRule { PlcNumber = plc, Address = addr, DataType = type, Operator = op, RequiredValue = val };
        }

        // Machine: (Bit, Tag, Name, PLC, Address)
        public static void RegisterMachineBit(long bit, string tag, string name, int plc, string addr)
            => RegisterMachineBit(bit, tag, name, plc, addr, DataTypeEnum.Bin_16, ComparisonOperator.Equal, 1);

        public static void RegisterMachineBit(long bit, string tag, string name, int plc, string addr, DataTypeEnum type, ComparisonOperator op, int val)
        {
            _machineBits[bit] = new ConditionDefinition
            {
                TagName = tag,
                Name = name,
                PlcNumber = plc,
                Address = addr,
                DataType = type,
                Operator = op,
                RequiredValue = val
            };
        }

        // Inside MachineConditionMap.cs
        public static bool CheckMachineBit(long bitValue, Func<string, int> lookup)
        {
            if (!_machineBits.TryGetValue(bitValue, out var def)) return true;

            int low = lookup(def.Address);
            double currentVal = low;

            // Handle 32-bit hardware tags
            if (def.DataType == DataTypeEnum.Bin_32)
            {
                int high = lookup(PlcAddressHelper.GetNextAddress(def.Address));
                currentVal = PlcValueConverter.ConvertToDouble(low, high, def.DataType, DisplayFormatEnum.Signed_Decimal, 0);
            }

            // Use the new public helper
            return ComparisonHelper.Compare(currentVal, def.Operator, def.RequiredValue);
        }

        public static string GetMachineBitName(long bitValue)
        {
            return _machineBits.TryGetValue(bitValue, out var def) ? def.Name : "Unknown";
        }

        public static ConditionRule GetRule(SystemCondition condition)
        {
            return _map.TryGetValue(condition, out var rule) ? rule : null;
        }

        public static IEnumerable<KeyValuePair<long, ConditionDefinition>> GetAllMachineBits() => _machineBits;

        /// <summary>The single conditions a set is made of.</summary>
        public static IEnumerable<SystemCondition> Parts(SystemCondition set)
        {
            long v = (long)set;
            for (int i = 0; i < 64; i++)
            {
                long bit = 1L << i;
                if ((v & bit) != 0) yield return (SystemCondition)bit;
            }
        }

        /// <summary>A condition's name: its description, or a machine condition's own name.</summary>
        public static string NameOf(SystemCondition condition)
        {
            if (Enum.IsDefined(typeof(SystemCondition), condition)) return EnumHelper.GetDescription(condition);
            ConditionDefinition def;
            return _machineBits.TryGetValue((long)condition, out def) && !string.IsNullOrEmpty(def.Name)
                ? def.Name : "Condition " + (long)condition;
        }

        /// <summary>
        /// The names of the conditions of a set that are NOT met - what a disabled
        /// control says when it is hovered. The whole set used to be printed, met
        /// and unmet alike, which left the operator to work out which one mattered.
        /// </summary>
        public static List<string> Unmet(SystemCondition set, Func<SystemCondition, bool> check)
        {
            var names = new List<string>();
            if (check == null) return names;
            foreach (var part in Parts(set))
                if (!check(part)) names.Add(NameOf(part));
            return names;
        }
    }
}
