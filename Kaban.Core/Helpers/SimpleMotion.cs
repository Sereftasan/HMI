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
    /// per axis of 31501. Read only - a parameter written here would need the
    /// module's flash ROM written to keep it, which is the module tool's work.
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
            // ---- parameters (as set in the module; read only) ----
            I("UnitSetting", "Unit setting", "Pr.1", 0, 150, Kind.Unit, "Parameters"),
            I("PulsesPerRevolution", "Pulses per rotation", "Pr.2", 2, 150, Kind.Pulses, "Parameters", dbl: true),
            I("TravelPerRevolution", "Movement amount per rotation", "Pr.3", 4, 150, Kind.Position, "Parameters", dbl: true),
            I("Backlash", "Backlash compensation", "Pr.11", 17, 150, Kind.Position, "Parameters"),
            I("SoftLimitUpper", "Software stroke limit, upper", "Pr.12", 18, 150, Kind.Position, "Parameters", dbl: true, signed: true),
            I("SoftLimitLower", "Software stroke limit, lower", "Pr.13", 20, 150, Kind.Position, "Parameters", dbl: true, signed: true),
            I("SpeedLimit", "Speed limit value", "Pr.8", 10, 150, Kind.Speed, "Parameters", dbl: true),
            I("AccelerationTime", "Acceleration time 0", "Pr.9", 12, 150, Kind.Milliseconds, "Parameters", dbl: true),
            I("DecelerationTime", "Deceleration time 0", "Pr.10", 14, 150, Kind.Milliseconds, "Parameters", dbl: true),
            I("HomeAddress", "Home position address", "Pr.45", 72, 150, Kind.Position, "Parameters", dbl: true, signed: true),
            I("HomingSpeed", "Home return speed", "Pr.46", 74, 150, Kind.Speed, "Parameters", dbl: true)
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
            new KeyValuePair<string, string>("Parameters", "Parameters (read only)")
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
