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

        [Description("Robot is at Safe Position")]
        RobotIsSafe = 1L << 5,

        [Description("Milling Unit is at Back")]
        MillingUnitIsBack = 1L << 6 
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
