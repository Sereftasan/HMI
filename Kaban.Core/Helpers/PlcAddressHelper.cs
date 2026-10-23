using System;
using System.Collections.Generic;
using System.Globalization;
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
            if (string.IsNullOrEmpty(address)) return "";
            // a device of another controller says which: "X12 (PLC 2)"
            int st; string device;
            if (TrySplitStation(address, out device, out st))
                return st == plcNumber ? device : device + " (PLC " + st + ")";
            return address;
        }

        // ==================================================================
        // A DEVICE OF ANOTHER CONTROLLER (12 October)
        // ------------------------------------------------------------------
        // "X12@2" is X12 of PLC 2, whatever the control's own PlcNumber: a
        // property can name a device of the machine's other controller - the
        // back moulds' reference sensors are wired to PLC 2 because PLC 1 had no
        // room left for their encoders. Without the mark a device is on the
        // control's own controller, as always.
        //
        // "@", not ".": a dot is already part of addresses - Siemens M10.3 (bit
        // 3 of byte 10) and DB10.DBX4.2, Mitsubishi D100.5 (bit 5 of a word).
        //
        // Read in one place (here), and kept to the ends of the program's paths:
        // the drivers (LoggedDriver sends it to that controller), the scanner
        // (it reads it in that controller's batch), the wiring table, the help
        // links and the device lists split it; everything between passes the
        // address on as it is.
        // ==================================================================

        /// <summary>The mark between a device and its controller's number: "X12@2".</summary>
        public const char StationMark = '@';

        /// <summary>
        /// "X12@2" -> true, "X12", 2. False for an address without a controller
        /// (the device is then the address itself).
        /// </summary>
        public static bool TrySplitStation(string address, out string device, out int station)
        {
            device = address;
            station = 0;
            if (string.IsNullOrEmpty(address)) return false;
            int at = address.LastIndexOf(StationMark);
            if (at <= 0 || at >= address.Length - 1) return false;
            int st;
            if (!int.TryParse(address.Substring(at + 1).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out st) || st < 1) return false;
            var d = address.Substring(0, at).Trim();
            if (d.Length == 0) return false;
            device = d;
            station = st;
            return true;
        }

        /// <summary>
        /// Where a control's device is: <paramref name="station"/> - the
        /// address's own controller, else the control's - and the device there.
        /// </summary>
        public static void Resolve(int controlStation, string address, out int station, out string device)
        {
            int st;
            if (TrySplitStation(address, out device, out st)) { station = st; return; }
            station = controlStation;
            device = address;
        }

        /// <summary>The controller an address is on: its own ("X12@2": 2), else <paramref name="controlStation"/>.</summary>
        public static int StationOf(int controlStation, string address)
        {
            int st; string device;
            return TrySplitStation(address, out device, out st) ? st : controlStation;
        }

        /// <summary>The device of an address, without its controller ("X12@2": "X12").</summary>
        public static string DeviceOf(string address)
        {
            int st; string device;
            return TrySplitStation(address, out device, out st) ? device : address;
        }

        /// <summary>
        /// A device as a control of <paramref name="controlStation"/> names it:
        /// plain on its own controller, "X12@2" on another.
        /// </summary>
        public static string WithStation(string device, int station, int controlStation)
        {
            if (string.IsNullOrEmpty(device)) return device;
            device = DeviceOf(device);
            return station == controlStation || station < 1 ? device : device + StationMark + station.ToString(CultureInfo.InvariantCulture);
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

            // "D100@2" + 1 = "D101@2": the device moves on, on its own controller
            int station; string device;
            if (TrySplitStation(baseAddress, out device, out station))
                return OffsetAddress(device, offset) + StationMark + station.ToString(CultureInfo.InvariantCulture);

            // A module's buffer memory word, "U1\G2400": the G number moves on.
            var module = SimpleMotion.OffsetModuleAddress(baseAddress, offset);
            if (module != null) return module;

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

        // ---- A DEVICE AS ITS LETTERS AND ITS NUMBER (22 October: the PLC backup) ----------------------------

        /// <summary>The bit devices of a MELSEC CPU (the rest are words): their block reads give 16 to a word.</summary>
        private static readonly HashSet<string> BitDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "X", "Y", "M", "L", "B", "F", "SB", "S", "V", "SM", "TS", "TC", "STS", "STC", "SS", "SC", "CS", "CC", "LCS", "LCC", "DX", "DY"
        };

        /// <summary>True for a bit device ("M100", "X17"); false for a word one or a module's buffer memory.</summary>
        public static bool IsBitDevice(string address)
        {
            string prefix; long number;
            return TrySplit(address, out prefix, out number) && BitDevices.Contains(prefix);
        }

        /// <summary>The 32-bit devices: a block of them gives two words a device, the low one first.</summary>
        private static readonly HashSet<string> DoubleWordDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LCN", "LZ", "LTN", "LSTN" };

        /// <summary>True for a 32-bit device ("LCN3", "LZ0"): two words a device in a block.</summary>
        public static bool IsDoubleWordDevice(string address)
        {
            string prefix; long number;
            return TrySplit(address, out prefix, out number) && DoubleWordDevices.Contains(prefix);
        }

        /// <summary>The number base a device is counted in: X, Y octal (as the FX numbers them), B, W, SB, SW hexadecimal, the rest decimal.</summary>
        public static int BaseOf(string prefix) { return GetBase(prefix ?? ""); }

        /// <summary>
        /// "D2500" - "D", 2500; "X17" - "X", 15 (octal); "U1\G6000" - "U1\G", 6000. False for anything
        /// else (a bit of a word "D100.5", a digit-count "K4M0", another controller's "X12@2").
        /// </summary>
        public static bool TrySplit(string address, out string prefix, out long number)
        {
            prefix = ""; number = 0;
            var a = (address ?? "").Trim().ToUpperInvariant();
            var module = Regex.Match(a, @"^(U[0-9A-F]+\\G)(\d+)$");
            if (module.Success)
            {
                prefix = module.Groups[1].Value;
                return long.TryParse(module.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
            }
            // the longest of the CPU's device letters the rest reads as a number of ("SB1F" is SB 1F,
            // "STN3" is STN 3, "B1F" is B 1F), else letters and decimal digits
            foreach (var p in Prefixes)
            {
                if (!a.StartsWith(p, StringComparison.Ordinal) || a.Length == p.Length) continue;
                string n = a.Substring(p.Length);
                int b = GetBase(p);
                if (!Regex.IsMatch(n, b == 16 ? "^[0-9A-F]+$" : b == 8 ? "^[0-7]+$" : "^[0-9]+$")) continue;
                try { number = b == 10 ? long.Parse(n, CultureInfo.InvariantCulture) : Convert.ToInt64(n, b); }
                catch { continue; }
                prefix = p;
                return true;
            }
            var m = Regex.Match(a, @"^([A-Z]+)([0-9]+)$");
            if (!m.Success) return false;
            prefix = m.Groups[1].Value;
            return long.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        /// <summary>A MELSEC CPU's device letters, the longest first.</summary>
        private static readonly string[] Prefixes =
        {
            "STN", "STS", "STC", "LCN", "LCS", "LCC", "SB", "SW", "SM", "SD", "SS", "SC", "SN", "TN", "TS", "TC", "CN", "CS", "CC", "DX", "DY", "ZR", "LZ",
            "X", "Y", "M", "L", "B", "F", "S", "V", "D", "W", "R", "Z"
        };

        /// <summary>A device from its letters and number, in its base: "X", 15 - "X17"; "W", 255 - "WFF".</summary>
        public static string Compose(string prefix, long number)
        {
            prefix = (prefix ?? "").ToUpperInvariant();
            if (prefix.EndsWith("\\G")) return prefix + number.ToString(CultureInfo.InvariantCulture);
            int b = GetBase(prefix);
            return prefix + (b == 16 ? number.ToString("X", CultureInfo.InvariantCulture)
                           : b == 8 ? Convert.ToString(number, 8)
                           : number.ToString(CultureInfo.InvariantCulture));
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
