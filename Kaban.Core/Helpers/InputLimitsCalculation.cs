using System;
using Kaban.Core.Enums;
using Kaban.Core.Models;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// Restores the "effective limits" calculation that used to back
    /// NumericInputHMI's keypad - resolves MinLimit/MaxLimit (which can be a
    /// fixed number, a live PLC tag, or an HMI internal variable) into concrete
    /// numbers you can validate a typed value against.
    ///
    /// When a limit comes from a PLC tag, readFormula lets you re-scale the raw
    /// integer the same way the value being entered is scaled (e.g. dividing by
    /// 10, or something more complex via NCalc) so the comparison is apples-to-apples.
    /// </summary>
    public static class InputLimitsCalculation
    {
        public struct EffectiveLimits
        {
            public double Min;
            public double Max;
        }

        public static EffectiveLimits GetEffectiveLimits(
            DataTypeEnum dataType,
            DisplayFormatEnum format,
            DynamicValue minLimit,
            DynamicValue maxLimit,
            string readFormula,
            Func<string, int> tagReader,
            Func<string, double, double> formulaEvaluator,
            double tagDivisor = 1.0)
        {
            return new EffectiveLimits
            {
                Min = ResolveOne(minLimit, readFormula, tagReader, formulaEvaluator,
                                 double.MinValue, tagDivisor, dataType, format),
                Max = ResolveOne(maxLimit, readFormula, tagReader, formulaEvaluator,
                                 double.MaxValue, tagDivisor, dataType, format)
            };
        }

        /// <summary>
        /// True when a formula leaves the raw value alone - blank, or the default
        /// "val". Anything else is assumed to do its own scaling.
        /// </summary>
        private static bool IsIdentityFormula(string formula)
        {
            // "A" too: it is the value's own name in the formulas (NumericFormula).
            return NumericFormula.IsIdentity(formula);
        }

        private static double ResolveOne(
            DynamicValue limit,
            string readFormula,
            Func<string, int> tagReader,
            Func<string, double, double> formulaEvaluator,
            double defaultValue,
            double tagDivisor,
            DataTypeEnum dataType,
            DisplayFormatEnum format)
        {
            if (limit == null || limit.Type == LimitSourceType.None) return defaultValue;

            if (limit.Type == LimitSourceType.Static) return limit.StaticValue;

            if (limit.Type == LimitSourceType.HmiVariable) return HmiInternalVariables.Get(limit.TagAddress);

            if (limit.Type == LimitSourceType.Tag)
            {
                if (string.IsNullOrEmpty(limit.TagAddress)) return defaultValue;

                // READ IT THE WAY THE FIELD READS ITS OWN VALUE.
                //
                // A single readTag call returns ONE 16-bit word. A travel limit of
                // 8200.00 mm is 82,000,000 in a 32-bit device, whose low word is
                // 14464 - which is exactly the nonsense the keypad was offering
                // ("Enter (9312.00 - 14464.00)"). The high word has to be read too,
                // and the pair converted with the field's own data type.
                int low = tagReader(limit.TagAddress);
                int high = dataType == DataTypeEnum.Bin_32
                           ? tagReader(PlcAddressHelper.GetNextAddress(limit.TagAddress))
                           : 0;
                double raw = PlcValueConverter.ConvertToDouble(low, high, dataType, format, 0);

                // SCALED BY THE FIELD'S OWN DIVISOR.
                //
                // A limit word holds the same kind of raw value the field does: a
                // travel limit of 8200.00 mm is stored as 82,000,000 with a divisor
                // of 10000. Returning the raw number compared an 8200.00 display
                // value against 82,000,000 - so every value looked in range, and
                // the keypad offered nonsense like "Enter (9312.00 - 14464.00)".
                //
                // A read formula, when one is set, is the deliberate override for a
                // limit word that is scaled differently from its field.
                double value = formulaEvaluator(readFormula, raw);

                // The divisor is applied unless a formula is doing the scaling
                // ITSELF. "val" is the default formula and means "the raw value
                // unchanged" - testing only for a non-empty string treated that
                // identity formula as a custom scaling and skipped the divisor,
                // which is why the keypad still offered raw counts.
                if (IsIdentityFormula(readFormula) && tagDivisor != 0 && tagDivisor != 1.0)
                    value = value / tagDivisor;
                return value;
            }

            return defaultValue;
        }
    }
}

