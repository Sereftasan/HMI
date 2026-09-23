using System;
using Kaban.Core.Enums;

namespace Kaban.Core.Helpers
{
    public static class PlcValueConverter
    {
        public static double ConvertToDouble(int lowWord, int highWord, DataTypeEnum dataType, DisplayFormatEnum format, int decimalPlaces)
        {
            double result = 0;

            // 1. COMBINE WORDS
            if (dataType == DataTypeEnum.Bin_32)
            {
                // Combine Low/High into a 32-bit Integer pattern
                // We mask lowWord to ensure negative short values don't corrupt the OR operation
                int combined = (highWord << 16) | (lowWord & 0xFFFF);

                if (format == DisplayFormatEnum.Signed_Decimal)
                {
                    result = combined; // Preserves Sign (e.g. -1 remains -1)
                }
                else if (format == DisplayFormatEnum.Unsigned_Decimal ||
                         format == DisplayFormatEnum.Hexadecimal ||
                         format == DisplayFormatEnum.Binary)
                {
                    result = (uint)combined; // Forces Positive (e.g. -1 becomes 4294967295)
                }
                else if (format == DisplayFormatEnum.Real)
                {
                    byte[] bytes = BitConverter.GetBytes(combined);
                    result = BitConverter.ToSingle(bytes, 0);
                }
            }
            else // Bin_16
            {
                if (format == DisplayFormatEnum.Signed_Decimal)
                    result = (short)lowWord;
                else
                    result = (ushort)lowWord;
            }

            // 2. APPLY DECIMAL POINT
            if (decimalPlaces > 0 && format != DisplayFormatEnum.Real)
            {
                result = result / Math.Pow(10, decimalPlaces);
            }

            return result;
        }

        public static void ConvertFromDouble(double value, DataTypeEnum dataType, DisplayFormatEnum format, int decimalPlaces, out short low, out short high)
        {
            low = 0;
            high = 0;

            // 1. REMOVE DECIMAL POINT
            if (decimalPlaces > 0 && format != DisplayFormatEnum.Real)
            {
                value = Math.Round(value * Math.Pow(10, decimalPlaces));
            }

            // 2. SPLIT WORDS
            if (dataType == DataTypeEnum.Bin_32)
            {
                if (format == DisplayFormatEnum.Real)
                {
                    float fVal = (float)value;
                    byte[] bytes = BitConverter.GetBytes(fVal);
                    int rawInt = BitConverter.ToInt32(bytes, 0);

                    low = (short)(rawInt & 0xFFFF);
                    high = (short)((rawInt >> 16) & 0xFFFF);
                }
                else if (format == DisplayFormatEnum.Unsigned_Decimal ||
                         format == DisplayFormatEnum.Hexadecimal)
                {
                    // --- EXPLICIT UNSIGNED HANDLING ---
                    // Cast to uint first to handle values > 2.14 Billion safely
                    uint uVal = (uint)value;

                    low = (short)(uVal & 0xFFFF);
                    high = (short)((uVal >> 16) & 0xFFFF);
                }
                else
                {
                    // Signed Logic
                    int iVal = (int)value;

                    low = (short)(iVal & 0xFFFF);
                    high = (short)((iVal >> 16) & 0xFFFF);
                }
            }
            else // Bin_16
            {
                // Just cast to short
                low = (short)value;
            }
        }

        public static string FormatString(double val, DataTypeEnum dataType, DisplayFormatEnum format, int decimalPlaces, int digits, bool fillZero)
        {
            if (format == DisplayFormatEnum.Hexadecimal)
            {
                string hexFormat = (dataType == DataTypeEnum.Bin_32) ? "X8" : "X4";
                if (dataType == DataTypeEnum.Bin_32) return ((uint)val).ToString(hexFormat); // Use uint for Hex 32
                return ((ushort)val).ToString(hexFormat);
            }

            string fmt = "F" + decimalPlaces;
            string text = val.ToString(fmt);

            if (fillZero && digits > 0)
            {
                bool isNeg = val < 0;
                string absText = Math.Abs(val).ToString(fmt);
                while (absText.Length < digits) absText = "0" + absText;
                text = (isNeg ? "-" : "") + absText;
            }
            return text;
        }
    }
}
