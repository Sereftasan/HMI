using System;
using System.Text.RegularExpressions;

namespace Kaban.Core.Helpers
{
    public static class PlcAddressHelper
    {
        /// <summary>
        /// A device address, written plainly. Always just "D215".
        ///
        /// The station is NOT folded in here. Several controls bind more than one
        /// device (a button has a relay, an output and two lamps), and prefixing
        /// every one of them with the same station number is four copies of one
        /// fact. It goes on its own line instead - see StationHeader.
        /// </summary>
        public static string Describe(int plcNumber, string address)
        {
            return string.IsNullOrEmpty(address) ? "" : address;
        }

        /// <summary>
        /// The line that names the controller, or "" for the main PLC.
        ///
        /// Keyed on THIS CONTROL'S number, not on how many stations exist. A
        /// machine with an auxiliary PLC still has almost all its controls on PLC
        /// 1, and printing "PLC 1" on every one of those tooltips is a constant
        /// repeated thousands of times to flag the handful that are not.
        ///
        /// So the main PLC says nothing and an auxiliary one announces itself,
        /// which is the only case where the number is news.
        /// </summary>
        public static string StationHeader(int plcNumber)
        {
            return plcNumber > 1 ? "PLC " + plcNumber + Environment.NewLine : "";
        }

        // Wrapper for single step
        public static string GetNextAddress(string currentAddress)
        {
            return OffsetAddress(currentAddress, 1);
        }

        // --- MAIN OFFSET LOGIC ---
        public static string OffsetAddress(string baseAddress, int offset)
        {
            if (string.IsNullOrEmpty(baseAddress) || offset == 0) return baseAddress;

            // Regex: Separate Letters (Type) from Numbers (Value)
            var match = Regex.Match(baseAddress, @"^([A-Za-z]+)([0-9A-Fa-f]+)$");
            if (!match.Success) return baseAddress;

            string type = match.Groups[1].Value.ToUpper();
            string numberString = match.Groups[2].Value;

            try
            {
                int currentVal;
                int baseSystem = GetBase(type);

                // 1. Convert String -> Integer
                if (baseSystem == 16) currentVal = Convert.ToInt32(numberString, 16);
                else if (baseSystem == 8) currentVal = Convert.ToInt32(numberString, 8);
                else currentVal = int.Parse(numberString);

                // 2. Add Offset
                currentVal += offset;

                // 3. Convert Integer -> String
                string newNumberPart;
                if (baseSystem == 16) newNumberPart = currentVal.ToString("X");
                else if (baseSystem == 8) newNumberPart = Convert.ToString(currentVal, 8); // To Octal String
                else newNumberPart = currentVal.ToString();

                return type + newNumberPart;
            }
            catch
            {
                return baseAddress; // Fail safe
            }
        }

        private static int GetBase(string type)
        {
            string t = type.ToUpper();

            // OCTAL Types (As per your request)
            if (t == "X" || t == "Y" || t == "DX" || t == "DY") return 8;

            // HEX Types
            if (t == "B" || t == "W" || t == "SB" || t == "SW") return 16;

            // DECIMAL Types (D, M, T, C, etc.)
            return 10;
        }
    }
}
