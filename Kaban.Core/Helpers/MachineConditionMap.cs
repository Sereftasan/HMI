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

        /// <summary>
        /// A DEVICE COMPARED WITH ANOTHER (10 November: "Sometime the conditions could be entered as D100 >= D700 maybe (an axis position is at
        /// one side of the parameter or not)"): the device whose value <see cref="Address"/> is compared with - read in the same data type
        /// (a 32-bit one with its next device too), on the same PLC - instead of <see cref="RequiredValue"/>. Empty: the number.
        /// </summary>
        public string CompareAddress { get; set; } = "";

        /// <summary>Compared with another device (<see cref="CompareAddress"/>), not with a number.</summary>
        public bool ComparesDevices { get { return !string.IsNullOrWhiteSpace(CompareAddress); } }

        /// <summary>
        /// A CONDITION OF SEVERAL DEVICES (7 November: "Each machine condition might have multiple device address so it should be able to
        /// show all of their values"): the further devices it reads, each with its own test - on the condition's PLC (MachineConditionMap
        /// .AddDevice). Met while every device's test is (AnyDevice: while one is).
        /// </summary>
        public List<ConditionRule> Also { get; } = new List<ConditionRule>();

        /// <summary>Met while ANY of its devices' tests is (not all of them).</summary>
        public bool AnyDevice { get; set; }

        /// <summary>Its devices: this one, then the further ones.</summary>
        public IEnumerable<ConditionRule> Devices
        {
            get
            {
                yield return this;
                foreach (var d in Also) if (d != null) yield return d;
            }
        }
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

        /// <summary>A system condition met while <paramref name="addr"/> compares so with <paramref name="compareAddr"/> ("D100 >= D700").</summary>
        public static void Register(SystemCondition cond, int plc, string addr, DataTypeEnum type, ComparisonOperator op, string compareAddr)
        {
            _map[cond] = new ConditionRule { PlcNumber = plc, Address = addr, DataType = type, Operator = op, RequiredValue = 0, CompareAddress = compareAddr ?? "" };
        }

        // Machine: (Bit, Tag, Name, PLC, Address)
        public static void RegisterMachineBit(long bit, string tag, string name, int plc, string addr)
            => RegisterMachineBit(bit, tag, name, plc, addr, DataTypeEnum.Bin_16, ComparisonOperator.Equal, 1);

        /// <summary>
        /// A machine's own condition met while <paramref name="addr"/> compares so with the device <paramref name="compareAddr"/> - an axis's
        /// position on one side of a parameter: RegisterMachineBit(bit, tag, "Robot X past its reference", plc1, "W0", DataTypeEnum.Bin_32,
        /// ComparisonOperator.GreaterOrEqual, "D316") is W0 &gt;= D316.
        /// </summary>
        public static void RegisterMachineBit(long bit, string tag, string name, int plc, string addr, DataTypeEnum type, ComparisonOperator op, string compareAddr)
        {
            RegisterMachineBit(bit, tag, name, plc, addr, type, op, 0);
            _machineBits[bit].CompareAddress = compareAddr ?? "";
        }

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

        /// <summary>A further device a system condition reads (registered after the condition): <see cref="ConditionRule.Also"/>.</summary>
        public static bool AddDevice(SystemCondition cond, string addr, DataTypeEnum type, ComparisonOperator op, int val)
        {
            ConditionRule rule;
            if (!_map.TryGetValue(cond, out rule) || rule == null) return false;
            rule.Also.Add(new ConditionRule { PlcNumber = rule.PlcNumber, Address = addr, DataType = type, Operator = op, RequiredValue = val });
            return true;
        }

        /// <summary>A further device a system condition reads, compared with another device (<see cref="ConditionRule.CompareAddress"/>).</summary>
        public static bool AddDevice(SystemCondition cond, string addr, DataTypeEnum type, ComparisonOperator op, string compareAddr)
        {
            if (!AddDevice(cond, addr, type, op, 0)) return false;
            _map[cond].Also[_map[cond].Also.Count - 1].CompareAddress = compareAddr ?? "";
            return true;
        }

        /// <summary>A further device a machine's own condition reads, compared with another device (<see cref="ConditionRule.CompareAddress"/>).</summary>
        public static bool AddMachineBitDevice(long bit, string addr, DataTypeEnum type, ComparisonOperator op, string compareAddr)
        {
            if (!AddMachineBitDevice(bit, addr, type, op, 0)) return false;
            _machineBits[bit].Also[_machineBits[bit].Also.Count - 1].CompareAddress = compareAddr ?? "";
            return true;
        }

        /// <summary>
        /// Every device a rule's test reads: each of its devices, the one each is compared with, a 32-bit one's next device - what the scanner
        /// reads for it.
        /// </summary>
        public static IEnumerable<string> AddressesOf(ConditionRule rule)
        {
            if (rule == null) yield break;
            foreach (var d in rule.Devices)
            {
                foreach (var a in new[] { d.Address, d.CompareAddress })
                {
                    if (string.IsNullOrWhiteSpace(a)) continue;
                    yield return a.Trim();
                    if (d.DataType == DataTypeEnum.Bin_32) yield return PlcAddressHelper.GetNextAddress(a.Trim());
                }
            }
        }

        /// <summary>A further device a machine's own condition reads (registered after the condition): <see cref="ConditionRule.Also"/>.</summary>
        public static bool AddMachineBitDevice(long bit, string addr, DataTypeEnum type, ComparisonOperator op, int val)
        {
            ConditionDefinition def;
            if (!_machineBits.TryGetValue(bit, out def) || def == null) return false;
            def.Also.Add(new ConditionRule { PlcNumber = def.PlcNumber, Address = addr, DataType = type, Operator = op, RequiredValue = val });
            return true;
        }

        /// <summary>One device's test on the values read (a 32-bit device reads the next one as its high word).</summary>
        public static bool CheckDevice(ConditionRule device, Func<string, int> lookup)
        {
            if (device == null || string.IsNullOrWhiteSpace(device.Address)) return false;
            Func<string, double> value = a =>
            {
                int low = lookup(a);
                if (device.DataType != DataTypeEnum.Bin_32) return low;
                int high = lookup(PlcAddressHelper.GetNextAddress(a));
                return PlcValueConverter.ConvertToDouble(low, high, device.DataType, DisplayFormatEnum.Signed_Decimal, 0);
            };
            double currentVal = value(device.Address);
            // compared with another device's value (D100 >= D700), else with the number
            double required = device.ComparesDevices ? value(device.CompareAddress.Trim()) : device.RequiredValue;
            return ComparisonHelper.Compare(currentVal, device.Operator, required);
        }

        /// <summary>A rule's test on the values read: every device's (AnyDevice: one's).</summary>
        public static bool Check(ConditionRule rule, Func<string, int> lookup)
        {
            if (rule == null) return false;
            bool any = false;
            foreach (var d in rule.Devices)
            {
                bool met = CheckDevice(d, lookup);
                if (rule.AnyDevice) { if (met) return true; }
                else if (!met) return false;
                any = true;
            }
            return !rule.AnyDevice && any;
        }

        // Inside MachineConditionMap.cs
        public static bool CheckMachineBit(long bitValue, Func<string, int> lookup)
        {
            if (!_machineBits.TryGetValue(bitValue, out var def)) return true;
            return Check(def, lookup);
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

        // ---- conditions judged at any time (30 October: a valve an option changes - FA2010's L35) ----

        /// <summary>
        /// The program's judgement of a set of conditions NOW, on the PLC values read last (the scanner sets
        /// it on every scan); null before the first scan.
        /// </summary>
        public static Func<SystemCondition, bool> Now { get; set; }

        private static long _readAlways;

        /// <summary>
        /// Conditions read on every scan whatever page is on the screen - for what is judged off its page
        /// (a unit's help opened from the alarm list, the HMI's own alarms): MetNow can answer for them.
        /// </summary>
        public static void ReadAlways(SystemCondition set) { _readAlways |= (long)set; }

        /// <summary>The conditions read on every scan (ReadAlways).</summary>
        public static SystemCondition AlwaysRead { get { return (SystemCondition)_readAlways; } }

        /// <summary>Whether a set of conditions is met now (None: yes; nothing read yet: no).</summary>
        public static bool MetNow(SystemCondition set)
        {
            if (set == SystemCondition.None) return true;
            var now = Now;
            if (now == null) return false;
            try { return now(set); } catch { return false; }
        }

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
            // its own device's value; every device's test (a condition of several - AnyDevice: one's)
            bool? met = EvaluateDevice(rule, lookup, out value);
            if (rule == null || rule.Also.Count == 0) return met;
            bool unread = !met.HasValue, anyMet = met == true, anyNot = met == false;
            foreach (var d in rule.Also)
            {
                double v;
                var m = EvaluateDevice(d, lookup, out v);
                if (!m.HasValue) unread = true;
                else if (m.Value) anyMet = true;
                else anyNot = true;
            }
            // all: one not met decides; any: one met decides - else a device not read yet leaves it open
            if (rule.AnyDevice) return anyMet ? true : unread ? (bool?)null : false;
            return anyNot ? false : unread ? (bool?)null : true;
        }

        /// <summary>One device's test, from the values read (null: not read yet) - its further devices not asked.</summary>
        public static bool? EvaluateDevice(ConditionRule device, Func<string, int?> lookup, out double value)
        {
            value = 0;
            if (device == null || string.IsNullOrWhiteSpace(device.Address) || lookup == null) return null;
            double? current = ReadValue(device, device.Address, lookup);
            if (!current.HasValue) return null;
            value = current.Value;
            double required = device.RequiredValue;
            if (device.ComparesDevices)
            {
                // compared with another device (D100 >= D700): that one read too
                var other = ReadValue(device, device.CompareAddress.Trim(), lookup);
                if (!other.HasValue) return null;
                required = other.Value;
            }
            return ComparisonHelper.Compare(value, device.Operator, required);
        }

        /// <summary>A device's value in a rule's data type (a 32-bit one with its next device as the high word); null: not read yet.</summary>
        public static double? ReadValue(ConditionRule rule, string address, Func<string, int?> lookup)
        {
            if (rule == null || string.IsNullOrWhiteSpace(address) || lookup == null) return null;
            int? low = lookup(address.Trim());
            if (!low.HasValue) return null;
            if (rule.DataType != DataTypeEnum.Bin_32) return low.Value;
            int? high = lookup(PlcAddressHelper.GetNextAddress(address.Trim()));
            if (!high.HasValue) return null;
            return PlcValueConverter.ConvertToDouble(low.Value, high.Value, rule.DataType, DisplayFormatEnum.Signed_Decimal, 0);
        }

        /// <summary>"M100 = 0", "D200 (32-bit) > 0" - what a rule asks; of several devices "M22 = 0 and M23 = 0" (AnyDevice: "or").</summary>
        public static string RuleText(ConditionRule rule)
        {
            if (rule == null) return "";
            if (rule.Also.Count == 0) return DeviceText(rule);
            var texts = new List<string>();
            foreach (var d in rule.Devices) texts.Add(DeviceText(d));
            return string.Join(rule.AnyDevice ? " or " : " and ", texts);
        }

        /// <summary>One device's test: "M100 = 0" (its further devices not said).</summary>
        public static string DeviceText(ConditionRule rule)
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
                   (rule.ComparesDevices ? rule.CompareAddress.Trim() : rule.RequiredValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
