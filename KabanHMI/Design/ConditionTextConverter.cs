using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Design
{
    public class ConditionTextConverter : TypeConverter
    {
        // 1. THE LOCK: Disables text typing in the Property Grid
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string)) return false;
            return base.CanConvertFrom(context, sourceType);
        }

        // 2. THE DESIGNER FILE: not this converter's. Visual Studio writes an enum
        // property with the ENUM's serializer, by its members' names - a property's
        // converter is not asked (its InstanceDescriptor here was never used, and the
        // "System.Enum.ToObject(...)" it described would not have compiled without a
        // cast). The machine bits are named for it (SystemCondition.MachineBit16...,
        // 15 October): "SystemCondition.1048576" was what came out before.

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            // 3. HUMAN UI: The Property Grid text
            if (destinationType == typeof(string) && value != null)
            {
                long currentVal = Convert.ToInt64(value);
                if (currentVal == 0) return "None";

                var names = new List<string>();

                // the shared ones by their descriptions; the machine bits (named MachineBit16...
                // in the enum) by their machine's names below
                foreach (SystemCondition item in ConditionBits.Shared)
                {
                    long itemBit = (long)item;

                    if ((currentVal & itemBit) == itemBit)
                    {
                        names.Add(EnumHelper.GetDescription(item));
                        currentVal &= ~itemBit;
                    }
                }

                foreach (var bit in MachineConditionMap.GetAllMachineBits())
                {
                    if ((currentVal & bit.Key) == bit.Key)
                    {
                        names.Add(bit.Value.Name);
                        currentVal &= ~bit.Key;
                    }
                }

                if (currentVal != 0) names.Add($"Unknown({currentVal})");

                return string.Join(", ", names);
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
