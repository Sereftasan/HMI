using System;
using System.ComponentModel;
using System.Globalization;
using Kaban.UI.Controls;

namespace Kaban.UI.Design
{
    /// <summary>
    /// The valve types as they are named on the screen: the two 5/3 valves are
    /// "3WayClosedCenter" and "3WayOpenCenter" (8 October: "we don't need the
    /// DoubleSolenoid_ prefix, all 3 way valves are double solenoid anyway"). A
    /// C# name cannot begin with a digit, so in code they are ThreeWayClosedCenter
    /// and ThreeWayOpenCenter; the Properties window, the type check and every
    /// text the program writes use these names, and every earlier name is read.
    /// A valve island's are "IslandDoubleSolenoid" ... "Island3WayOpenCenter" (12 November).
    /// </summary>
    public class CylinderValveConverter : EnumConverter
    {
        public CylinderValveConverter() : base(typeof(CylinderValve)) { }

        /// <summary>The valve's name on the screen.</summary>
        public static string Name(CylinderValve v)
        {
            // "3WayClosedCenter", "Island3WayOpenCenter" (12 November: the valves of a valve island)
            return v.ToString().Replace("ThreeWay", "3Way");
        }

        /// <summary>A valve from its name: the screen's, the code's or an earlier one ("DoubleSolenoid_3Way...").</summary>
        public static bool TryParse(string text, out CylinderValve v)
        {
            v = CylinderValve.Unspecified;
            string t = (text ?? "").Trim();
            if (t.Length == 0) return false;
            if (t.StartsWith("DoubleSolenoid_", StringComparison.OrdinalIgnoreCase)) t = t.Substring("DoubleSolenoid_".Length);
            int at = t.IndexOf("3Way", StringComparison.OrdinalIgnoreCase);
            if (at >= 0) t = t.Substring(0, at) + "ThreeWay" + t.Substring(at + 4);
            if (t.Equals("ThreeWay", StringComparison.OrdinalIgnoreCase)) t = "ThreeWayClosedCenter";   // the one 5/3 before 8 October
            int n;
            if (int.TryParse(t, out n)) { if (!Enum.IsDefined(typeof(CylinderValve), n)) return false; v = (CylinderValve)n; return true; }
            foreach (CylinderValve c in Enum.GetValues(typeof(CylinderValve)))
                if (c.ToString().Equals(t, StringComparison.OrdinalIgnoreCase)) { v = c; return true; }
            return false;
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is CylinderValve) return Name((CylinderValve)value);
            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var s = value as string;
            CylinderValve v;
            if (s != null && TryParse(s, out v)) return v;
            return base.ConvertFrom(context, culture, value);
        }
    }
}
