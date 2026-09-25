using System;
using System.Globalization;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// Whether a value is within its limits - with ANGLES done properly.
    ///
    /// A length's limits are a plain range: minimum &lt;= value &lt;= maximum.
    /// An angle's are not always. An axis that travels from 270 degrees through
    /// 360 (= 0) on to 45 has the minimum 270 and the maximum 45 - a minimum
    /// ABOVE the maximum - and every plain min &lt;= value &lt;= max test refuses
    /// every value of it. That is why the angle rows had their limits switched
    /// off altogether, which left them with no limit at all.
    ///
    /// For an angle:
    ///   * the value is never below 0 or above 360, limits or not;
    ///   * minimum &lt;= maximum: a plain range, as for a length (within 0 - 360);
    ///   * minimum &gt; maximum: the range runs THROUGH zero - from the minimum up
    ///     to 360, or from 0 up to the maximum (270 ... 360 / 0 ... 45).
    /// </summary>
    public static class NumericLimits
    {
        public const double FullTurn = 360.0;

        /// <summary>True for an angle whose minimum is above its maximum: a range through zero.</summary>
        public static bool Wraps(double? min, double? max, bool angle)
        {
            return angle && min.HasValue && max.HasValue && min.Value > max.Value;
        }

        /// <summary>Whether the value may be entered / is within its limits.</summary>
        public static bool Allows(double value, double? min, double? max, bool angle)
        {
            if (double.IsNaN(value)) return false;
            if (angle)
            {
                if (value < 0 || value > FullTurn) return false;
                if (Wraps(min, max, true)) return value >= min.Value || value <= max.Value;
            }
            if (min.HasValue && value < min.Value) return false;
            if (max.HasValue && value > max.Value) return false;
            return true;
        }

        /// <summary>
        /// The limits as the keypad shows them: "10.0 &lt;= ... &lt;= 250.0";
        /// for an angle through zero "270.0 &lt;= ... or ... &lt;= 45.0"; an angle
        /// with no limits "0 &lt;= ... &lt;= 360". Empty when there is nothing to say.
        /// </summary>
        public static string Describe(double? min, double? max, bool angle, int decimals)
        {
            string fmt = "F" + Math.Max(0, decimals);
            Func<double, string> f = v => v.ToString(fmt, CultureInfo.InvariantCulture);
            const string le = " ≤ ", dots = "…";
            if (Wraps(min, max, angle))
                return f(min.Value) + le + dots + "  " + LanguageManager.GetString("Lbl_Or", "or") + "  " + dots + le + f(max.Value);
            if (angle)
            {
                double lo = Math.Max(0, min ?? 0), hi = Math.Min(FullTurn, max ?? FullTurn);
                return f(lo) + le + dots + le + f(hi);
            }
            if (min.HasValue && max.HasValue) return f(min.Value) + le + dots + le + f(max.Value);
            if (min.HasValue) return f(min.Value) + le + dots;
            if (max.HasValue) return dots + le + f(max.Value);
            return "";
        }
    }
}
