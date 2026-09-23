using System;
using System.ComponentModel;
using System.Globalization;
using System.ComponentModel.Design.Serialization;
using System.Reflection;
using System.Linq;
using System.IO;
using Kaban.Core.Enums;

namespace Kaban.Core.Models
{
    [Serializable]
    [TypeConverter(typeof(DynamicValueConverter))]
    public class DynamicValue
    {
        [Browsable(false)] public LimitSourceType Type { get; set; } = LimitSourceType.None;
        [Browsable(false)] public double StaticValue { get; set; } = 0;
        [Browsable(false)] public string TagAddress { get; set; } = "";

        public DynamicValue() { }
        public DynamicValue(LimitSourceType type, double staticValue, string tagAddress)
        {
            Type = type; StaticValue = staticValue; TagAddress = tagAddress;
        }

        public override string ToString()
        {
            switch (Type)
            {
                case LimitSourceType.None: return "None";
                case LimitSourceType.Static: return StaticValue.ToString(CultureInfo.InvariantCulture);
                case LimitSourceType.HmiVariable: return TagAddress;
                default: return TagAddress;
            }
        }
    }

    public class DynamicValueConverter : TypeConverter
    {

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string) || sourceType.FullName.EndsWith("DynamicValue")) return true;
            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value == null) return null;

            if (value is string str)
            {
                str = str.Trim();
                // A TYPED DEVICE NAME IS A PLC TAG.
                //
                // THIS is why tag-based limits never worked while constants did:
                // anything non-numeric became an HmiVariable, so typing "D200" as a
                // limit read an HMI variable of that name - which does not exist -
                // and the limit silently did nothing. The value never came from the
                // PLC at all.
                //
                // "@name" still means an HMI variable. A bare device name is a tag,
                // which is what an engineer typing into a limit field means.
                bool isNumber = double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
                bool isHmiVar = str.StartsWith("@");

                string enumString = string.IsNullOrEmpty(str) || str.Equals("None", StringComparison.OrdinalIgnoreCase) ? "None"
                                  : isNumber ? "Static"
                                  : isHmiVar ? "HmiVariable"
                                  : "Tag";

                double statVal = enumString == "Static" ? double.Parse(str, CultureInfo.InvariantCulture) : 0.0;
                string tagVal = (enumString == "Tag" || enumString == "HmiVariable")
                                ? (isHmiVar ? str.Substring(1) : str) : "";

                Type destType = context?.PropertyDescriptor?.PropertyType ?? typeof(DynamicValue);
                var ctorStr = destType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 3);

                if (ctorStr != null)
                {
                    return ctorStr.Invoke(new object[] { Enum.Parse(ctorStr.GetParameters()[0].ParameterType, enumString), statVal, tagVal });
                }
            }

            if (value.GetType().FullName.EndsWith("DynamicValue"))
            {
                Type destType = context?.PropertyDescriptor?.PropertyType ?? typeof(DynamicValue);
                var typeVal = value.GetType().GetProperty("Type")?.GetValue(value)?.ToString() ?? "None";
                var staticVal = value.GetType().GetProperty("StaticValue")?.GetValue(value) ?? 0.0;
                var tagVal = value.GetType().GetProperty("TagAddress")?.GetValue(value)?.ToString() ?? "";

                var ctorObj = destType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 3);
                if (ctorObj != null)
                {
                    return ctorObj.Invoke(new object[] { Enum.Parse(ctorObj.GetParameters()[0].ParameterType, typeVal), Convert.ToDouble(staticVal), tagVal });
                }
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (value == null) return null;

            if (value.GetType().FullName.EndsWith("DynamicValue"))
            {
                var typeVal = value.GetType().GetProperty("Type")?.GetValue(value)?.ToString() ?? "None";
                var staticVal = value.GetType().GetProperty("StaticValue")?.GetValue(value) ?? 0.0;
                var tagVal = value.GetType().GetProperty("TagAddress")?.GetValue(value)?.ToString() ?? "";

                if (destinationType == typeof(string))
                {
                    if (typeVal == "None") return "None";
                    if (typeVal == "Static") return Convert.ToDouble(staticVal).ToString(CultureInfo.InvariantCulture);
                    return tagVal;
                }

                if (destinationType == typeof(InstanceDescriptor))
                {
                    Type destType = context?.PropertyDescriptor?.PropertyType ?? value.GetType();
                    var ctor = destType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 3);

                    if (ctor != null)
                    {
                        Type enumType = ctor.GetParameters()[0].ParameterType;
                        object safeEnum = Enum.Parse(enumType, typeVal);
                        return new InstanceDescriptor(ctor, new object[] { safeEnum, Convert.ToDouble(staticVal), tagVal });
                    }
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
