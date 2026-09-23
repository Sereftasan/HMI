using System;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.Machines.SB3012
{
    public class MachineConfig
    {
        // The static constructor runs automatically when the class is first accessed
        static MachineConfig()
        {
            //System.Windows.Forms.MessageBox.Show("Config Loaded!");

            // 1. Common System Conditions
            //MachineConditionMap.Register(SystemCondition.SafetyOK, 1, "X0");
            MachineConditionMap.Register(SystemCondition.ManualMode, 1, "M100",
                    DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
            MachineConditionMap.Register(SystemCondition.RobotIsSafe, 1, "X20");
            MachineConditionMap.Register(SystemCondition.MillingUnitIsBack, 2, "X25");

            // 2. Machine-Specific Conditions (Added PLC Number '1' as first argument)
            MachineConditionMap.RegisterMachineBit(1L << 16, "RobotIsOutside", "Robot is at Outside", 1, "X50");
            MachineConditionMap.RegisterMachineBit(1L << 17, "MillingUnitInside", "Milling Unit is NOT Inside", 1, "M22",
                    DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        }

        public static void Initialize() { }
    }
}
