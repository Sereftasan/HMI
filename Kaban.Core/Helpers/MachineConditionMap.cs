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

        /// <summary>One registered condition, as the PLC Monitor lists it.</summary>
        public sealed class Entry
        {
            /// <summary>"S:ManualMode" for a system condition, "B:FA2010.RobotIsOutside" for a machine's own.</summary>
            public string Key = "";
            public string Name = "";
            public ConditionRule Rule;
        }

        /// <summary>
        /// Every registered condition that reads a device: the system ones in their
        /// order, then the machines' own by bit.
        /// </summary>
        public static List<Entry> All()
        {
            var list = new List<Entry>();
            foreach (SystemCondition c in ConditionBits.Shared)
            {
                ConditionRule r;
                if (c == SystemCondition.None || !_map.TryGetValue(c, out r) || r == null || string.IsNullOrWhiteSpace(r.Address)) continue;
                list.Add(new Entry { Key = "S:" + c, Name = EnumHelper.GetDescription(c), Rule = r });
            }
            var bits = new List<long>(_machineBits.Keys);
            bits.Sort();
            foreach (var b in bits)
            {
                var d = _machineBits[b];
                if (d == null || string.IsNullOrWhiteSpace(d.Address)) continue;
                string tag = string.IsNullOrWhiteSpace(d.TagName) ? b.ToString(System.Globalization.CultureInfo.InvariantCulture) : d.TagName;
                list.Add(new Entry { Key = "B:" + tag, Name = string.IsNullOrWhiteSpace(d.Name) ? tag : d.Name, Rule = d });
            }
            return list;
        }

        /// <summary>A condition by its key (null when it is not registered - another machine's, or renamed).</summary>
        public static Entry Find(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            foreach (var e in All())
                if (string.Equals(e.Key, key.Trim(), StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        /// <summary>
        /// Whether a rule is met, from the values read (null: a device it needs is not
        /// read yet). The same test the running program makes: a 32-bit rule reads the
        /// device after its address as the high word.
        /// </summary>
        public static bool? Evaluate(ConditionRule rule, Func<string, int?> lookup, out double value)
        {
            value = 0;
            if (rule == null || string.IsNullOrWhiteSpace(rule.Address) || lookup == null) return null;
            int? low = lookup(rule.Address);
            if (!low.HasValue) return null;
            double current = low.Value;
            if (rule.DataType == DataTypeEnum.Bin_32)
            {
                int? high = lookup(PlcAddressHelper.GetNextAddress(rule.Address));
                if (!high.HasValue) return null;
                current = PlcValueConverter.ConvertToDouble(low.Value, high.Value, rule.DataType, DisplayFormatEnum.Signed_Decimal, 0);
            }
            value = current;
            return ComparisonHelper.Compare(current, rule.Operator, rule.RequiredValue);
        }

        /// <summary>"M100 = 0", "D200 (32-bit) > 0" - what a rule asks.</summary>
        public static string RuleText(ConditionRule rule)
        {
            if (rule == null) return "";
            string op;
            switch (rule.Operator)
            {
                case ComparisonOperator.Equal: op = "="; break;
                case ComparisonOperator.NotEqual: op = "\u2260"; break;
                case ComparisonOperator.Greater: op = ">"; break;
                case ComparisonOperator.GreaterOrEqual: op = "\u2265"; break;
                case ComparisonOperator.Less: op = "<"; break;
                case ComparisonOperator.LessOrEqual: op = "\u2264"; break;
                default: op = rule.Operator.ToString(); break;
            }
            return (rule.Address ?? "") + (rule.DataType == DataTypeEnum.Bin_32 ? " (32-bit)" : "") + " " + op + " " +
                   rule.RequiredValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>True when nothing is registered (the designer before a machine is loaded).</summary>
        public static bool IsEmpty { get { return _map.Count == 0 && _machineBits.Count == 0; } }

        /// <summary>Forgets every registration (the designer, before loading another machine's).</summary>
        public static void Clear()
        {
            _map.Clear();
            _machineBits.Clear();
        }

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
            if (ConditionBits.IsShared(condition)) return EnumHelper.GetDescription(condition);
            ConditionDefinition def;
            return _machineBits.TryGetValue((long)condition, out def) && !string.IsNullOrEmpty(def.Name)
                ? def.Name : "Condition " + (long)condition;
        }

        /// <summary>
        /// A set of conditions in words, each with what it reads (14 October: several
        /// conditions together "not working" - an Admin's tooltip printed the set as a
        /// number, 524290, so which one held a control back could not be seen):
        /// "Manual Mode Active (M100 = 0) + Clamping Mould Option (L27 = 1)". With
        /// <paramref name="check"/>, each is marked met or not. A condition this program
        /// has no rule for says so: it is never met.
        /// </summary>
        public static string Describe(SystemCondition set, Func<SystemCondition, bool> check = null)
        {
            if (set == SystemCondition.None) return "None";
            var parts = new List<string>();
            foreach (var part in Parts(set))
            {
                string text = NameOf(part);
                ConditionRule rule = GetRule(part);
                if (rule == null)
                {
                    ConditionDefinition def;
                    if (_machineBits.TryGetValue((long)part, out def)) rule = def;
                }
                if (part == SystemCondition.AdminUser) text += " (signed in as Admin)";
                else if (part == SystemCondition.MaintenanceUser) text += " (signed in as Maintenance or Admin)";
                else if (rule != null && !string.IsNullOrWhiteSpace(rule.Address)) text += " (" + RuleText(rule) + (rule.PlcNumber > 1 ? ", PLC " + rule.PlcNumber : "") + ")";
                else text += " (no rule on this machine - never met)";
                if (check != null) text = (check(part) ? "✓ " : "✗ ") + text;
                parts.Add(text);
            }
            return string.Join(" + ", parts);
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
