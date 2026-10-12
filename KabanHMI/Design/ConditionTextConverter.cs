using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.ComponentModel.Design.Serialization; // <--- ADDED for InstanceDescriptor
using System.Reflection; // <--- ADDED for MethodInfo
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

        // 2. NEW: Tells Visual Studio we know how to write safe C# code
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(InstanceDescriptor)) return true;
            return base.CanConvertTo(context, destinationType);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            // 3. HUMAN UI: The Property Grid text
            if (destinationType == typeof(string) && value != null)
            {
                long currentVal = Convert.ToInt64(value);
                if (currentVal == 0) return "None";

                var names = new List<string>();

                foreach (SystemCondition item in Enum.GetValues(typeof(SystemCondition)))
                {
                    if (item == SystemCondition.None) continue;
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

            // 4. VISUAL STUDIO CODE GENERATOR: Safely writes the .Designer.cs file
            if (destinationType == typeof(InstanceDescriptor) && value != null)
            {
                // This forces the designer to generate: System.Enum.ToObject(typeof(SystemCondition), 458754L)
                MethodInfo method = typeof(Enum).GetMethod("ToObject", new Type[] { typeof(Type), typeof(long) });
                if (method != null)
                {
                    return new InstanceDescriptor(method, new object[] { value.GetType(), Convert.ToInt64(value) });
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
