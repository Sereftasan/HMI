using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// WHAT A MITSUBISHI SIMPLE MOTION MODULE (FX5-40SSC-S / FX5-80SSC-S) SAYS
    /// ABOUT AN AXIS, read straight from its buffer memory (Un\G...): the axis's
    /// error and warning codes, the servo alarm, the operation status, the
    /// positions and speeds, the motor's speed and current, the flags (home
    /// request, servo ready / on, in position, busy ...) and the parameters set
    /// in it (travel per revolution, backlash, the stroke limits ...).
    ///
    /// The addresses are the module's own, from Mitsubishi's buffer memory list
    /// (80SSC_Devicelist.xls, FX5-SSC-S column): an axis's monitor data is
    /// 2400 + 100 x (axis - 1), its parameters 0 + 150 x (axis - 1), BUSY one bit
    /// per axis of 31501.
    ///
    /// THE PARAMETERS ARE AN ADMIN'S TO CHANGE (22 October: "Simple motion write device should
    /// be changed to numerical input ... some parameters need EPROM write as well"). From the
    /// module's manual (FX5 Simple Motion Module User's Manual (Application), 10.9 Memory
    /// Configuration and Data Process): a value written to the buffer memory is used AT ONCE for
    /// the axis's next start (parameter area (b): Pr.8 - Pr.10, Pr.25 - Pr.42, Pr.84), or when the
    /// "[Cd.190] PLC READY signal" turns on again (area (a): Pr.1 - Pr.7, Pr.11 - Pr.24, Pr.43 -
    /// Pr.57 ...). Either way the module loads its FLASH ROM at power on, so a value not written
    /// into it ("[Cd.1] Flash ROM write request", G5900 = 1, at most 25 times per power on) is gone
    /// at the next power off: see <see cref="WriteFlashRom"/>.
    ///
    /// An AxisSettingHMI with its MotionModuleNumber (the n of Un\G, as GX Works3
    /// numbers the module) and its ModuleAxisIndex (1 - 8) shows the ones picked
    /// on the Axis Settings page as columns of its row.
    /// </summary>
    public static class SimpleMotion
    {
        /// <summary>How an item's words are shown.</summary>
        public enum Kind
        {
            /// <summary>A code, hexadecimal (an error number): 0 = none.</summary>
            Code,
            /// <summary>The axis operation status (Md.26), as words.</summary>
            Status,
            /// <summary>One bit of a word: a lamp.</summary>
            Flag,
            /// <summary>A position in the axis's unit (Pr.1): 0.1 um, 0.00001 inch / degree, pulses.</summary>
            Position,
            /// <summary>A speed in the axis's unit: 0.01 mm/min, 0.001 inch or degree/min, pulse/s.</summary>
            Speed,
            /// <summary>A count of pulses.</summary>
            Pulses,
            /// <summary>Tenths of a percent (0.1 %).</summary>
            Permille,
            /// <summary>Whole percent.</summary>
            Percent,
            /// <summary>Hundredths of r/min.</summary>
            Rpm,
            /// <summary>Milliseconds.</summary>
            Milliseconds,
            /// <summary>The unit setting (Pr.1): mm, inch, degree, pulse.</summary>
            Unit
        }

        public sealed class Item
        {
            /// <summary>The key kept in View Preferences and used for the column ("AxisError").</summary>
            public string Key = "";
            /// <summary>Translation key and English text of its heading and its tooltip.</summary>
            public string TitleKey = "", Title = "";
            /// <summary>Mitsubishi's name for it: "Md.23", "Pr.3".</summary>
            public string Ref = "";
            /// <summary>Its buffer memory address for axis 1.</summary>
            public int Base;
            /// <summary>Between one axis and the next (100 for monitor data, 150 for parameters, 0: one word for every axis).</summary>
            public int Stride;
            /// <summary>Two words (32 bit, low word first) or one.</summary>
            public bool Double;
            public bool Signed;
            /// <summary>The bit of a flag; for a word shared by the axes (BUSY), -1 + the axis is the bit.</summary>
            public int Bit = -1;
            public bool BitPerAxis;
            public Kind Kind;
            /// <summary>The pick list's group: Alarms, Positions, Loads, Flags, Parameters.</summary>
            public string Group = "";
            /// <summary>A parameter an Admin may type in (written to the module's buffer memory).</summary>
            public bool Writable;
            /// <summary>Its setting range, in the module's own counts (the manual's "value set with a program").</summary>
            public long Min, Max;
            /// <summary>
            /// Taken by the module when its PLC READY signal (Cd.190) turns on (parameter area (a));
            /// false: at the axis's next start (area (b)).
            /// </summary>
            public bool AtPlcReady;
        }

        private static Item I(string key, string title, string reference, int b, int stride, Kind kind, string group,
                              bool dbl = false, bool signed = false, int bit = -1, bool bitPerAxis = false)
        {
            return new Item
            {
                Key = key, TitleKey = "Mot_" + key, Title = title, Ref = reference, Base = b, Stride = stride, Kind = kind,
                Group = group, Double = dbl, Signed = signed, Bit = bit, BitPerAxis = bitPerAxis
            };
        }

        /// <summary>A parameter: typed in by an Admin, within its range, used at PLC READY or at the next start.</summary>
        private static Item P(string key, string title, string reference, int b, Kind kind, long min, long max, bool atPlcReady,
                              bool dbl = false, bool signed = false)
        {
            var i = I(key, title, reference, b, 150, kind, "Parameters", dbl, signed);
            i.Writable = true; i.Min = min; i.Max = max; i.AtPlcReady = atPlcReady;
            return i;
        }

        /// <summary>Every value the Axis Settings page can show, in the pick list's order.</summary>
        public static readonly IList<Item> Items = new List<Item>
        {
            // ---- alarms and status ----
            I("AxisError", "Axis error No.", "Md.23", 2406, 100, Kind.Code, "Alarms"),
            I("AxisWarning", "Axis warning No.", "Md.24", 2407, 100, Kind.Code, "Alarms"),
            I("ServoAlarm", "Servo alarm", "Md.114", 2488, 100, Kind.Code, "Alarms"),
            I("OperationStatus", "Axis operation status", "Md.26", 2409, 100, Kind.Status, "Alarms", signed: true),
            // ---- flags ----
            I("HomeRequest", "Home request", "Md.31 b3", 2417, 100, Kind.Flag, "Flags", bit: 3),
            I("HomeComplete", "Home complete", "Md.31 b4", 2417, 100, Kind.Flag, "Flags", bit: 4),
            I("ErrorDetected", "Error detection", "Md.31 b13", 2417, 100, Kind.Flag, "Flags", bit: 13),
            I("WarningDetected", "Warning detection", "Md.31 b9", 2417, 100, Kind.Flag, "Flags", bit: 9),
            I("PositioningComplete", "Positioning complete", "Md.31 b15", 2417, 100, Kind.Flag, "Flags", bit: 15),
            I("CommandInPosition", "Command in-position", "Md.31 b2", 2417, 100, Kind.Flag, "Flags", bit: 2),
            I("Busy", "BUSY", "Md.141", 31501, 0, Kind.Flag, "Flags", bitPerAxis: true),
            I("ServoReady", "Servo READY", "Md.108 b0", 2477, 100, Kind.Flag, "Flags", bit: 0),
            I("ServoOn", "Servo ON", "Md.108 b1", 2477, 100, Kind.Flag, "Flags", bit: 1),
            I("InPosition", "In-position", "Md.108 b12", 2477, 100, Kind.Flag, "Flags", bit: 12),
            I("TorqueLimiting", "Torque limit", "Md.108 b13", 2477, 100, Kind.Flag, "Flags", bit: 13),
            I("AbsoluteLost", "Absolute position lost", "Md.108 b14", 2477, 100, Kind.Flag, "Flags", bit: 14),
            I("ServoWarning", "Servo warning", "Md.108 b15", 2477, 100, Kind.Flag, "Flags", bit: 15),
            I("LowerLimit", "Lower limit signal", "Md.30 b0", 2416, 100, Kind.Flag, "Flags", bit: 0),
            I("UpperLimit", "Upper limit signal", "Md.30 b1", 2416, 100, Kind.Flag, "Flags", bit: 1),
            I("ProximityDog", "Proximity dog signal", "Md.30 b6", 2416, 100, Kind.Flag, "Flags", bit: 6),
            // ---- positions and speeds ----
            I("FeedCurrentValue", "Feed current value", "Md.20", 2400, 100, Kind.Position, "Positions", dbl: true, signed: true),
            I("RealCurrentValue", "Real current value", "Md.101", 2450, 100, Kind.Position, "Positions", dbl: true, signed: true),
            I("DeviationCounter", "Deviation counter", "Md.102", 2452, 100, Kind.Pulses, "Positions", dbl: true, signed: true),
            I("TargetValue", "Target value", "Md.32", 2418, 100, Kind.Position, "Positions", dbl: true, signed: true),
            I("Feedrate", "Feedrate", "Md.22", 2404, 100, Kind.Speed, "Positions", dbl: true),
            I("CurrentSpeed", "Current speed", "Md.27", 2410, 100, Kind.Speed, "Positions", dbl: true),
            I("MotorSpeed", "Motor rotation speed", "Md.103", 2454, 100, Kind.Rpm, "Positions", dbl: true, signed: true),
            // ---- loads ----
            I("MotorCurrent", "Motor current", "Md.104", 2456, 100, Kind.Permille, "Loads", signed: true),
            I("TorqueLimitValue", "Torque limit stored value", "Md.35", 2426, 100, Kind.Permille, "Loads"),
            I("RegenerativeLoad", "Regenerative load ratio", "Md.109", 2478, 100, Kind.Percent, "Loads", signed: true),
            I("EffectiveLoad", "Effective load torque", "Md.110", 2479, 100, Kind.Percent, "Loads", signed: true),
            I("PeakTorque", "Peak torque ratio", "Md.111", 2480, 100, Kind.Percent, "Loads", signed: true),
            // ---- parameters (as set in the module; an Admin types them in) - ranges from the manual's 10.3 / 10.4 ----
            P("UnitSetting", "Unit setting", "Pr.1", 0, Kind.Unit, 0, 3, true),
            P("PulsesPerRevolution", "Pulses per rotation", "Pr.2", 2, Kind.Pulses, 1, 200000000, true, dbl: true),
            P("TravelPerRevolution", "Movement amount per rotation", "Pr.3", 4, Kind.Position, 1, 200000000, true, dbl: true),
            P("Backlash", "Backlash compensation", "Pr.11", 17, Kind.Position, 0, 65535, true),
            P("SoftLimitUpper", "Software stroke limit, upper", "Pr.12", 18, Kind.Position, int.MinValue, int.MaxValue, true, dbl: true, signed: true),
            P("SoftLimitLower", "Software stroke limit, lower", "Pr.13", 20, Kind.Position, int.MinValue, int.MaxValue, true, dbl: true, signed: true),
            P("SpeedLimit", "Speed limit value", "Pr.8", 10, Kind.Speed, 1, 2000000000, false, dbl: true),
            P("AccelerationTime", "Acceleration time 0", "Pr.9", 12, Kind.Milliseconds, 1, 8388608, false, dbl: true),
            P("DecelerationTime", "Deceleration time 0", "Pr.10", 14, Kind.Milliseconds, 1, 8388608, false, dbl: true),
            P("HomeAddress", "Home position address", "Pr.45", 72, Kind.Position, int.MinValue, int.MaxValue, true, dbl: true, signed: true),
            P("HomingSpeed", "Home return speed", "Pr.46", 74, Kind.Speed, 1, 2000000000, true, dbl: true)
        }.AsReadOnly();

        /// <summary>The unit setting (Pr.1) of an axis: 0 mm, 1 inch, 2 degree, 3 pulse.</summary>
        public static readonly Item UnitItem = Items.First(i => i.Key == "UnitSetting");

        /// <summary>
        /// THE AXIS'S POSITIONS (22 October: "Feed current value and Real current value should be
        /// removed from the Motion monitor's list - they show the same values"): the feed current
        /// value is the axis's Position, the real current value its "Servo encoder value" - both
        /// chosen in the Position column's own list (the axis pages' Position heading), not here.
        /// </summary>
        public static bool IsAxisPosition(string key)
        {
            return string.Equals(key, "FeedCurrentValue", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "RealCurrentValue", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The Motion monitor list's values: every one but the axis's positions.</summary>
        public static IEnumerable<Item> MonitorItems { get { return Items.Where(i => !IsAxisPosition(i.Key)); } }

        /// <summary>The servo motor encoder's position (Md.101): an axis's "Servo encoder value".</summary>
        public static Item ServoEncoderItem { get { return Find("RealCurrentValue"); } }

        public static Item Find(string key)
        {
            return Items.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The groups in the pick list's order, with their English names.</summary>
        public static readonly IList<KeyValuePair<string, string>> Groups = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Alarms", "Alarms and status"),
            new KeyValuePair<string, string>("Flags", "Flags"),
            new KeyValuePair<string, string>("Positions", "Positions and speeds"),
            new KeyValuePair<string, string>("Loads", "Loads"),
            new KeyValuePair<string, string>("Parameters", "Parameters")
        }.AsReadOnly();

        /// <summary>The highest axis a module has (FX5-80SSC-S: 8).</summary>
        public const int MaxAxes = 8;

        /// <summary>The device of a buffer memory word: "U1\G2406".</summary>
        public static string Device(int module, int address)
        {
            return "U" + module.ToString(CultureInfo.InvariantCulture) + "\\G" + address.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The word an item is in for an axis (1-based).</summary>
        public static int AddressOf(Item item, int axis)
        {
            return item.Base + item.Stride * (Math.Max(1, axis) - 1);
        }

        /// <summary>The devices an item reads for an axis: its word (and the next for 32 bit) - in that order.</summary>
        public static IList<string> DevicesOf(Item item, int module, int axis)
        {
            int a = AddressOf(item, axis);
            var list = new List<string> { Device(module, a) };
            if (item.Double) list.Add(Device(module, a + 1));
            return list;
        }

        /// <summary>The unit setting's device for an axis (the scale of positions and speeds).</summary>
        public static string UnitDevice(int module, int axis) { return Device(module, AddressOf(UnitItem, axis)); }

        /// <summary>True when the item's value is scaled by the axis's unit.</summary>
        public static bool NeedsUnit(Item item) { return item.Kind == Kind.Position || item.Kind == Kind.Speed; }

        /// <summary>True for an address of a module's buffer memory: "U1\G2406" (also "U3E0\G10").</summary>
        public static bool IsModuleAddress(string address)
        {
            return !string.IsNullOrEmpty(address) && Regex.IsMatch(address.Trim(), @"^U[0-9A-Fa-f]+\\G\d+$", RegexOptions.IgnoreCase);
        }

        /// <summary>A module address moved on by some words ("U1\G2400" + 1 = "U1\G2401"); null when it is none.</summary>
        public static string OffsetModuleAddress(string address, int offset)
        {
            var m = Regex.Match((address ?? "").Trim(), @"^(U[0-9A-Fa-f]+\\G)(\d+)$", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            long n = long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) + offset;
            return m.Groups[1].Value.ToUpperInvariant() + n.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The raw value of an item from its words (low, high), as a number: signed or not, one or two words.</summary>
        public static long Raw(Item item, int low, int high)
        {
            if (item.Double)
            {
                long v = (long)(ushort)low | ((long)(ushort)high << 16);
                return item.Signed ? (long)(int)(uint)v : v;
            }
            return item.Signed ? (long)(short)low : (long)(ushort)low;
        }

        /// <summary>For a flag: whether it is on, for an axis.</summary>
        public static bool FlagOn(Item item, int word, int axis)
        {
            int bit = item.BitPerAxis ? Math.Max(1, axis) - 1 : item.Bit;
            return bit >= 0 && ((word >> bit) & 1) == 1;
        }

        /// <summary>The unit's name for the unit setting: mm, inch, degree, pulse.</summary>
        public static string UnitName(int unit)
        {
            switch (unit) { case 0: return "mm"; case 1: return "inch"; case 2: return "\u00B0"; case 3: return "pulse"; default: return "?"; }
        }

        /// <summary>How many of a position's raw counts make one unit (Pr.1): 10 000 per mm, 100 000 per inch or degree, 1 per pulse.</summary>
        public static double PositionScale(int unit)
        {
            switch (unit) { case 0: return 10000.0; case 1: return 100000.0; case 2: return 100000.0; default: return 1.0; }
        }

        /// <summary>How many of a speed's raw counts make one unit per minute (pulse: per second).</summary>
        public static double SpeedScale(int unit)
        {
            switch (unit) { case 0: return 100.0; case 1: return 1000.0; case 2: return 1000.0; default: return 1.0; }
        }

        public static string SpeedUnit(int unit)
        {
            switch (unit) { case 0: return "mm/min"; case 1: return "inch/min"; case 2: return "\u00B0/min"; default: return "pulse/s"; }
        }

        /// <summary>
        /// The axis operation status (Md.26) in words, with its number - the
        /// module's manual names them; a number not listed shows as itself.
        /// </summary>
        public static string StatusText(int status)
        {
            string text;
            switch (status)
            {
                case -4: text = "Step error"; break;
                case -3: text = "Step stopped"; break;
                case -2: text = "Step standby"; break;
                case -1: text = "Error"; break;
                case 0: text = "Standby"; break;
                case 1: text = "Stopped"; break;
                case 2: text = "Interpolation"; break;
                case 3: text = "JOG"; break;
                case 4: text = "Manual pulse"; break;
                case 5: text = "Analyzing"; break;
                case 6: text = "Special start standby"; break;
                case 7: text = "Home return"; break;
                case 8: text = "Position control"; break;
                case 9: text = "Speed control"; break;
                case 10: text = "Speed (speed-position)"; break;
                case 11: text = "Position (speed-position)"; break;
                case 12: text = "Position (position-speed)"; break;
                case 13: text = "Speed (position-speed)"; break;
                default: return status.ToString(CultureInfo.InvariantCulture);
            }
            return LanguageManager.GetString("MotSt_" + (status < 0 ? "m" + (-status) : status.ToString(CultureInfo.InvariantCulture)), text) +
                   " (" + status.ToString(CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>
        /// How a row shows the module's positions and speeds: as the module's unit
        /// setting names them (Module); as a LENGTH, in the HMI's length unit
        /// (Users and Preferences - mm, cm, inch ...), as the row's own position;
        /// as an ANGLE, in degrees - a rotary axis is often set to mm in the module,
        /// the figures being its degrees.
        /// </summary>
        public enum RowUnit { Module, Length, Angle }

        /// <summary>
        /// An item's value as text, from its words and the axis's unit setting
        /// (for positions and speeds): "123.46 mm", "1A18", "Standby (0)", "ON".
        /// <paramref name="decimals"/>: a position's decimals (the row's own).
        /// </summary>
        public static string Format(Item item, int low, int high, int unit, int axis, int decimals = 2)
        {
            return Format(item, low, high, unit, axis, decimals, RowUnit.Module);
        }

        /// <summary>
        /// As <see cref="Format(Item,int,int,int,int,int)"/>, positions and speeds
        /// shown in the row's unit (<paramref name="row"/>): the module's figures
        /// read in its unit setting (0.1 um per mm, 0.00001 inch / degree), then
        /// named - and for a length converted - as the row's position is.
        /// A module set to pulses, or to a unit the row cannot take (degrees on a
        /// length row, inches on an angle row), is shown as the module says.
        /// </summary>
        public static string Format(Item item, int low, int high, int unit, int axis, int decimals, RowUnit row)
        {
            var nf = ViewPreferences.NumberFormat;
            long raw = Raw(item, low, high);
            switch (item.Kind)
            {
                case Kind.Code:
                    return raw == 0 ? "0" : ((ushort)low).ToString("X4", CultureInfo.InvariantCulture);
                case Kind.Status:
                    return StatusText((int)raw);
                case Kind.Flag:
                    return FlagOn(item, low, axis) ? "ON" : "OFF";
                case Kind.Position:
                    {
                        if (unit == 3) return raw.ToString(CultureInfo.InvariantCulture) + " pulse";
                        double v = raw / PositionScale(unit);
                        int d = item.Key == "Backlash" || item.Key == "TravelPerRevolution" ? Math.Max(decimals, unit == 0 ? 4 : 5) : decimals;
                        string name = UnitName(unit);
                        if (row == RowUnit.Angle && unit != 1) name = UnitName(2);
                        else if (row == RowUnit.Length && unit != 2)
                        {
                            v = UnitManager.ToDisplay(Enums.UnitType.Length, unit == 1 ? v * 25.4 : v);
                            name = UnitManager.GetActiveSuffix(Enums.UnitType.Length);
                            d += UnitManager.GetActiveExtraDecimals(Enums.UnitType.Length);
                        }
                        return v.ToString("F" + Math.Min(9, d), nf) + " " + name;
                    }
                case Kind.Speed:
                    {
                        if (unit == 3) return raw.ToString(CultureInfo.InvariantCulture) + " pulse/s";
                        double v = raw / SpeedScale(unit);
                        int d = unit == 0 ? 2 : 3;
                        string name = SpeedUnit(unit);
                        if (row == RowUnit.Angle && unit != 1) name = SpeedUnit(2);
                        else if (row == RowUnit.Length && unit != 2)
                        {
                            v = UnitManager.ToDisplay(Enums.UnitType.Length, unit == 1 ? v * 25.4 : v);
                            name = UnitManager.GetActiveSuffix(Enums.UnitType.Length) + "/min";
                            d = 2 + UnitManager.GetActiveExtraDecimals(Enums.UnitType.Length);
                        }
                        return v.ToString("F" + Math.Min(9, d), nf) + " " + name;
                    }
                case Kind.Pulses:
                    return raw.ToString(CultureInfo.InvariantCulture) + " pulse";
                case Kind.Permille:
                    return (raw / 10.0).ToString("F1", nf) + " %";
                case Kind.Percent:
                    return raw.ToString(CultureInfo.InvariantCulture) + " %";
                case Kind.Rpm:
                    return (raw / 100.0).ToString("F2", nf) + " r/min";
                case Kind.Milliseconds:
                    return raw.ToString(CultureInfo.InvariantCulture) + " ms";
                case Kind.Unit:
                    return UnitName((int)raw);
                default:
                    return raw.ToString(CultureInfo.InvariantCulture);
            }
        }

        // ---- AN ADMIN'S VALUE, BACK INTO THE MODULE'S COUNTS ----------------------------------------------------

        /// <summary>
        /// The number a cell shows for a raw value, in the cell's unit - the keypad's starting value
        /// (Format's figure without its unit): a position or a speed as the row shows it (the HMI's
        /// length unit on a length row), 0.1 % a tenth, r/min a hundredth; the rest as counted.
        /// </summary>
        public static double Shown(Item item, long raw, int unit, RowUnit row)
        {
            switch (item.Kind)
            {
                case Kind.Position:
                case Kind.Speed:
                    {
                        if (unit == 3) return raw;
                        double v = raw / (item.Kind == Kind.Position ? PositionScale(unit) : SpeedScale(unit));
                        if (row == RowUnit.Length && unit != 2 && unit != 1)
                            v = UnitManager.ToDisplay(Enums.UnitType.Length, v);
                        else if (row == RowUnit.Length && unit == 1)
                            v = UnitManager.ToDisplay(Enums.UnitType.Length, v * 25.4);
                        return v;
                    }
                case Kind.Permille: return raw / 10.0;
                case Kind.Rpm: return raw / 100.0;
                default: return raw;
            }
        }

        /// <summary>The decimal places a cell's number has (the keypad's): as <see cref="Format(Item,int,int,int,int,int,RowUnit)"/> shows it.</summary>
        public static int ShownDecimals(Item item, int unit, int decimals, RowUnit row)
        {
            switch (item.Kind)
            {
                case Kind.Position:
                    {
                        if (unit == 3) return 0;
                        int d = item.Key == "Backlash" || item.Key == "TravelPerRevolution" ? Math.Max(decimals, unit == 0 ? 4 : 5) : decimals;
                        if (row == RowUnit.Length && unit != 2) d += UnitManager.GetActiveExtraDecimals(Enums.UnitType.Length);
                        return Math.Min(9, d);
                    }
                case Kind.Speed:
                    {
                        if (unit == 3) return 0;
                        int d = unit == 0 ? 2 : 3;
                        if (row == RowUnit.Length && unit != 2) d = 2 + UnitManager.GetActiveExtraDecimals(Enums.UnitType.Length);
                        return Math.Min(9, d);
                    }
                case Kind.Permille: return 1;
                case Kind.Rpm: return 2;
                default: return 0;
            }
        }

        /// <summary>
        /// The module's raw count for a number typed in the cell's unit - <see cref="Shown"/> the other
        /// way round, rounded to the module's resolution. False when it is outside the parameter's
        /// setting range (<paramref name="raw"/> is then the nearest it allows).
        /// </summary>
        public static bool TryToRaw(Item item, double shown, int unit, RowUnit row, out long raw)
        {
            double v = shown;
            switch (item.Kind)
            {
                case Kind.Position:
                case Kind.Speed:
                    if (unit != 3)
                    {
                        if (row == RowUnit.Length && unit != 2)
                        {
                            double mm = UnitManager.ToReference(Enums.UnitType.Length, shown);
                            v = unit == 1 ? mm / 25.4 : mm;
                        }
                        v *= item.Kind == Kind.Position ? PositionScale(unit) : SpeedScale(unit);
                    }
                    break;
                case Kind.Permille: v = shown * 10.0; break;
                case Kind.Rpm: v = shown * 100.0; break;
            }
            double r = Math.Round(v, MidpointRounding.AwayFromZero);
            long min = item.Writable ? item.Min : item.Double ? (item.Signed ? (long)int.MinValue : 0L) : (item.Signed ? (long)short.MinValue : 0L);
            long max = item.Writable ? item.Max : item.Double ? (item.Signed ? (long)int.MaxValue : (long)uint.MaxValue) : (item.Signed ? (long)short.MaxValue : (long)ushort.MaxValue);
            if (double.IsNaN(r) || r < min) { raw = min; return false; }
            if (r > max) { raw = max; return false; }
            raw = (long)r;
            return true;
        }

        /// <summary>A raw value as the words written: the low word, and the high one of a 32-bit value (0 for one word).</summary>
        public static void ToWords(Item item, long raw, out int low, out int high)
        {
            low = (short)(raw & 0xFFFF);
            high = item.Double ? (short)((raw >> 16) & 0xFFFF) : 0;
        }

        // ---- THE MODULE'S FLASH ROM (22 October) ------------------------------------------------------------------

        /// <summary>"[Cd.1] Flash ROM write request": 1 asks the module to write its flash ROM; it sets 0 when done.</summary>
        public const int FlashWriteRequestAddress = 5900;
        /// <summary>"[Cd.190] PLC READY signal": the module writes its flash ROM only while it is off (else warning 0905H).</summary>
        public const int PlcReadyAddress = 5950;
        /// <summary>"[Md.19] Number of write accesses to flash ROM" since power on (two words).</summary>
        public const int FlashWritesAddress = 4224;
        /// <summary>A program may write the flash ROM 25 times per power on; the 26th is error 1080H.</summary>
        public const int MaxFlashWritesPerPowerOn = 25;
        /// <summary>The module's writing takes up to 5 s (the manual): waited for a little longer.</summary>
        public const int FlashWriteTimeoutMs = 8000;

        public enum FlashResult { Written, NotConnected, TooManyWrites, Refused, TimedOut }

        /// <summary>
        /// Writes a module's flash ROM ("execution data backup": its parameters, positioning data,
        /// block start data and servo parameters as they are in its buffer memory now) - 23 October:
        /// "You just need to write 1 to Un\G5900 ... that is all": G5900 = 1 (unless the 25 writes a
        /// power on allows are used up - the 26th is error 1080H), and the module sets it back to 0 when
        /// it has written. <paramref name="writes"/>: Md.19 afterwards. <paramref name="wait"/> sleeps
        /// between the polls (a test passes its own).
        /// </summary>
        public static FlashResult WriteFlashRom(Interfaces.IPlcDriver driver, int module, out int writes, Action<int> wait = null,
                                                int timeoutMs = FlashWriteTimeoutMs)
        {
            writes = -1;
            if (driver == null || !driver.IsConnected) return FlashResult.NotConnected;
            wait = wait ?? (ms => System.Threading.Thread.Sleep(ms));
            int low, high;
            if (driver.TryRead(Device(module, FlashWritesAddress), out low) && driver.TryRead(Device(module, FlashWritesAddress + 1), out high))
            {
                writes = (ushort)low | ((ushort)high << 16);
                if (writes >= MaxFlashWritesPerPowerOn) return FlashResult.TooManyWrites;
            }
            if (driver.Write(Device(module, FlashWriteRequestAddress), 1) != 0) return FlashResult.Refused;
            int waited = 0;
            while (true)
            {
                int request;
                if (driver.TryRead(Device(module, FlashWriteRequestAddress), out request) && request == 0) break;
                if (waited >= timeoutMs) return FlashResult.TimedOut;
                wait(100);
                waited += 100;
            }
            if (driver.TryRead(Device(module, FlashWritesAddress), out low) && driver.TryRead(Device(module, FlashWritesAddress + 1), out high))
                writes = (ushort)low | ((ushort)high << 16);
            return FlashResult.Written;
        }

        /// <summary>A column's width at factor 1, for its kind.</summary>
        public static int ColumnWidth(Item item)
        {
            switch (item.Kind)
            {
                case Kind.Flag: return 72;
                case Kind.Code: return 72;
                case Kind.Unit: return 64;
                case Kind.Status: return 150;
                case Kind.Milliseconds: case Kind.Percent: case Kind.Permille: return 86;
                default: return 120;
            }
        }
    }
}
