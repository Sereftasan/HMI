using System;
using System.ComponentModel;

namespace Kaban.Core.Enums
{
    [Flags]
    public enum SystemCondition : long
    {
        [Description("None (Always Active)")]
        None = 0,

        [Description("Safety Relays OK")]
        SafetyOK = 1,

        [Description("Manual Mode Active")]
        ManualMode = 1L << 1,

        [Description("Auto Mode Active")]
        AutoMode = 1L << 2,

        [Description("Admin User Logged In")]
        AdminUser = 1L << 3,

        [Description("Machine is Running")]
        MachineIsRunning = 1L << 4,

        /// <summary>A Maintenance or Admin user is signed in (17 October: Safety Bypass, Cylinder FW/Back).</summary>
        [Description("Maintenance User Logged In")]
        MaintenanceUser = 1L << 5,

        /// <summary>
        /// The axes jog by a step (10 / 1 / 0.1 / 0.01 mm), not JOG - the machine's rule
        /// (FA2010: the JOG lamp M174 OFF). Axes are coupled only then (17 October).
        /// </summary>
        [Description("Axes in Step Mode (not JOG)")]
        AxisStepMode = 1L << 6,

        // ---- the machines' own conditions: bits 16 - 63 ----
        // NAMED ONLY SO VISUAL STUDIO CAN WRITE THEM (15 October). The designer writes a
        // flag enum's value by its members' names, and a bit with no member came out as
        // "Kaban.Core.Enums.SystemCondition.1048576" - code that does not compile, so a
        // page lost every machine condition put on it (the Clamping Mould and Mullion
        // Notching options). What each bit means is the machine's: its module allocates
        // them in order (ctx.AllocateMachineBit - FA2010's first is MachineBit16) and
        // MachineConditionMap names them; the Properties window shows those names.
        // Never test these by Enum.IsDefined: ConditionBits says which are shared.

        [Description("Machine condition (bit 16)")]
        MachineBit16 = 1L << 16,

        [Description("Machine condition (bit 17)")]
        MachineBit17 = 1L << 17,

        [Description("Machine condition (bit 18)")]
        MachineBit18 = 1L << 18,

        [Description("Machine condition (bit 19)")]
        MachineBit19 = 1L << 19,

        [Description("Machine condition (bit 20)")]
        MachineBit20 = 1L << 20,

        [Description("Machine condition (bit 21)")]
        MachineBit21 = 1L << 21,

        [Description("Machine condition (bit 22)")]
        MachineBit22 = 1L << 22,

        [Description("Machine condition (bit 23)")]
        MachineBit23 = 1L << 23,

        [Description("Machine condition (bit 24)")]
        MachineBit24 = 1L << 24,

        [Description("Machine condition (bit 25)")]
        MachineBit25 = 1L << 25,

        [Description("Machine condition (bit 26)")]
        MachineBit26 = 1L << 26,

        [Description("Machine condition (bit 27)")]
        MachineBit27 = 1L << 27,

        [Description("Machine condition (bit 28)")]
        MachineBit28 = 1L << 28,

        [Description("Machine condition (bit 29)")]
        MachineBit29 = 1L << 29,

        [Description("Machine condition (bit 30)")]
        MachineBit30 = 1L << 30,

        [Description("Machine condition (bit 31)")]
        MachineBit31 = 1L << 31,

        [Description("Machine condition (bit 32)")]
        MachineBit32 = 1L << 32,

        [Description("Machine condition (bit 33)")]
        MachineBit33 = 1L << 33,

        [Description("Machine condition (bit 34)")]
        MachineBit34 = 1L << 34,

        [Description("Machine condition (bit 35)")]
        MachineBit35 = 1L << 35,

        [Description("Machine condition (bit 36)")]
        MachineBit36 = 1L << 36,

        [Description("Machine condition (bit 37)")]
        MachineBit37 = 1L << 37,

        [Description("Machine condition (bit 38)")]
        MachineBit38 = 1L << 38,

        [Description("Machine condition (bit 39)")]
        MachineBit39 = 1L << 39,

        [Description("Machine condition (bit 40)")]
        MachineBit40 = 1L << 40,

        [Description("Machine condition (bit 41)")]
        MachineBit41 = 1L << 41,

        [Description("Machine condition (bit 42)")]
        MachineBit42 = 1L << 42,

        [Description("Machine condition (bit 43)")]
        MachineBit43 = 1L << 43,

        [Description("Machine condition (bit 44)")]
        MachineBit44 = 1L << 44,

        [Description("Machine condition (bit 45)")]
        MachineBit45 = 1L << 45,

        [Description("Machine condition (bit 46)")]
        MachineBit46 = 1L << 46,

        [Description("Machine condition (bit 47)")]
        MachineBit47 = 1L << 47,

        [Description("Machine condition (bit 48)")]
        MachineBit48 = 1L << 48,

        [Description("Machine condition (bit 49)")]
        MachineBit49 = 1L << 49,

        [Description("Machine condition (bit 50)")]
        MachineBit50 = 1L << 50,

        [Description("Machine condition (bit 51)")]
        MachineBit51 = 1L << 51,

        [Description("Machine condition (bit 52)")]
        MachineBit52 = 1L << 52,

        [Description("Machine condition (bit 53)")]
        MachineBit53 = 1L << 53,

        [Description("Machine condition (bit 54)")]
        MachineBit54 = 1L << 54,

        [Description("Machine condition (bit 55)")]
        MachineBit55 = 1L << 55,

        [Description("Machine condition (bit 56)")]
        MachineBit56 = 1L << 56,

        [Description("Machine condition (bit 57)")]
        MachineBit57 = 1L << 57,

        [Description("Machine condition (bit 58)")]
        MachineBit58 = 1L << 58,

        [Description("Machine condition (bit 59)")]
        MachineBit59 = 1L << 59,

        [Description("Machine condition (bit 60)")]
        MachineBit60 = 1L << 60,

        [Description("Machine condition (bit 61)")]
        MachineBit61 = 1L << 61,

        [Description("Machine condition (bit 62)")]
        MachineBit62 = 1L << 62,

        [Description("Machine condition (bit 63)")]
        MachineBit63 = 1L << 63
    }

    /// <summary>
    /// Which conditions are shared by every machine (bits 0 - 15, SafetyOK...MachineIsRunning)
    /// and which are a machine's own (bits 16 - 63, MachineBit16...): both are named in the
    /// enum, so Enum.IsDefined / Enum.GetValues no longer tell them apart.
    /// </summary>
    public static class ConditionBits
    {
        /// <summary>The first machine condition's bit (MachineBit16).</summary>
        public const int FirstMachineBit = 16;

        /// <summary>A single bit of a machine's own (16 - 63).</summary>
        public static bool IsMachineBit(SystemCondition c)
        {
            ulong v = unchecked((ulong)(long)c);
            return (v >> FirstMachineBit) != 0;
        }

        /// <summary>A shared condition: named, not None, below bit 16.</summary>
        public static bool IsShared(SystemCondition c)
        {
            return c != SystemCondition.None && !IsMachineBit(c) && Enum.IsDefined(typeof(SystemCondition), c);
        }

        /// <summary>The shared conditions, in order (not None, not the machine bits).</summary>
        public static readonly SystemCondition[] Shared = SharedFlags();

        private static SystemCondition[] SharedFlags()
        {
            var list = new System.Collections.Generic.List<SystemCondition>();
            foreach (SystemCondition c in Enum.GetValues(typeof(SystemCondition)))
                if (IsShared(c)) list.Add(c);
            return list.ToArray();
        }

        /// <summary>The machine condition of a bit number (16 - 63): MachineBit20 for 20.</summary>
        public static SystemCondition MachineBit(int bit)
        {
            if (bit < FirstMachineBit || bit > 63) throw new ArgumentOutOfRangeException("bit");
            return (SystemCondition)(1L << bit);
        }
    }

    /// <summary>Arrow pair shown on a CylinderUnitHMI's two buttons.</summary>
    public enum ButtonArrowStyle
    {
        LeftRight,
        UpDown,
        DownUp,
        OpenClose,
        Custom
    }

    /// <summary>Which side of a control its attached description sits on.</summary>
    public enum DescriptionSide
    {
        Left,
        Right
    }
}
