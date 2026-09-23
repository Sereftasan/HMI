using System.Windows.Forms;
using System;
using System.Collections.Generic;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;

namespace Kaban.Machines.FA2010
{
    /// <summary>
    /// FA2010 as a MACHINE MODULE.
    ///
    /// This class contains no assumption about being the main machine - it uses
    /// ctx.Station() for every PLC reference and ctx.AllocateMachineBit() for every
    /// custom condition. That is what allows FA2010 to be mounted as a SUB machine
    /// inside a bigger line later without touching this file.
    ///
    /// Program.cs still exists so FA2010 can also run standalone as its own exe.
    /// </summary>
    public class FA2010Module : IMachineModule, Kaban.UI.Screens.IMachineDisplayName
    {
        public string ModuleId => "FA2010";
        public string DisplayNameKey => "Machine_FA2010";

        /// <summary>
        /// The name the operator reads on tabs, on the Station Connection page and
        /// on the monitor's tabs ("Processing Center", "Isleme Merkezi", ...).
        /// A MAIN machine needs this as much as a sub-machine does: without it the
        /// project id "FA2010" leaks onto the screen, and it is the model number
        /// rather than what the machine is called.
        /// </summary>
        public string DisplayName
        {
            get
            {
                // GetString WITH a fallback, never Localize: Localize returns the
                // raw KEY itself when a lookup fails (by design, for the separate
                // "this text IS either a key or literal wording" case elsewhere in
                // the program) - which is exactly how a missing translation once
                // showed up as the literal string "Machine_" + ModuleId on a tab. ModuleId
                // is always present and always readable, so it is the fallback here.
                return Kaban.Core.Helpers.LanguageManager.GetString(DisplayNameKey, ModuleId);
            }
        }

        public void RegisterPlc(IMachineMountContext ctx)
        {
            // Declare the stations this machine owns and what level may COMMAND them.
            PlcManager.Instance.RegisterStation(ctx.Station(1), "FA2010 Main PLC",
                SecurityLevel.Operator, ctx.ModuleId, 1);

            // WHERE THE PLC KEEPS ITS PROGRAM VERSION.
            //
            // A property of the machine's ladder, not a setting: it is fixed when
            // the program is written and never changes in the field. Putting it on
            // a settings page invited someone to edit it, and an edited version
            // device reports the wrong version or none at all.
            ConnectionMonitor.SetVersionDevice(ctx.Station(1), "D36");
            PlcManager.Instance.RegisterStation(ctx.Station(2), "FA2010 Aux PLC",
                SecurityLevel.Operator, ctx.ModuleId, 2);

            // Drivers are registered by the entry point (Program.cs), because only
            // it knows whether this deployment talks to real hardware or the
            // simulator. The module just declares which stations must exist.
        }

        public void RegisterConditions(IMachineMountContext ctx)
        {
            int plc1 = ctx.Station(1);
            int plc2 = ctx.Station(2);

            // Standard system conditions (bits 0-15, shared vocabulary).
            MachineConditionMap.Register(SystemCondition.ManualMode, plc1, "M100", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
            MachineConditionMap.Register(SystemCondition.AutoMode, plc1, "D300", DataTypeEnum.Bin_16, ComparisonOperator.LessOrEqual, 3);
            MachineConditionMap.Register(SystemCondition.MachineIsRunning, plc1, "D200", DataTypeEnum.Bin_32, ComparisonOperator.Greater, 0);
            MachineConditionMap.Register(SystemCondition.RobotIsSafe, plc1, "X20");
            MachineConditionMap.Register(SystemCondition.MillingUnitIsBack, plc2, "X25");

            // Machine-specific bits are ALLOCATED, never hardcoded - two modules
            // would otherwise both claim bit 16 and silently corrupt each other.
            MachineConditionMap.RegisterMachineBit(ctx.AllocateMachineBit("RobotIsOutside"),
                ctx.Scoped("RobotIsOutside"), "Robot is at Outside", plc1, "X50");
            MachineConditionMap.RegisterMachineBit(ctx.AllocateMachineBit("MillingUnitInside"),
                ctx.Scoped("MillingUnitInside"), "Milling Unit is NOT Inside", plc1, "M22",
                DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
            MachineConditionMap.RegisterMachineBit(ctx.AllocateMachineBit("MillingUnitInside_PLC2"),
                ctx.Scoped("MillingUnitInside_PLC2"), "Milling Unit is NOT Inside_PLC2", plc2, "M23",
                DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        }

        public void RegisterData(IMachineMountContext ctx)
        {
            // Additive: a mounted sub-machine's alarms/translations must not wipe
            // the host's. The main machine replaces, sub-machines append. Alarm PLC
            // numbers are remapped through the station map (identity for the main
            // machine, but correct if FA2010 is ever itself mounted as a sub).
            AlarmDefinitionLoader.Load(ctx.ResolveFile("Alarms.json"),
                replaceExisting: ctx.IsMainMachine, moduleId: ctx.ModuleId, stationRemap: ctx.Station);

            string csv = ctx.ResolveFile("Translations.csv");
            if (ctx.IsMainMachine) LanguageManager.LoadMachineTranslations(csv);
            else LanguageManager.AddMachineTranslations(csv);
        }

        private static KeyValuePair<string, Func<Control>> Page(string title, Func<Control> factory)
            => new KeyValuePair<string, Func<Control>>(title, factory);

        /// <summary>
        /// FA2010 supplies only what is genuinely its own; the left bar itself -
        /// Home, Alarms, Manual Movement, Machine Parameters, HMI Settings, their
        /// icons, roles, sort order and security levels - comes from
        /// StandardScreens, so this machine cannot drift away from the others.
        ///
        /// It used to hand-build all five entries here, which is how the Settings
        /// entry ended up defined twice with two different shapes.
        ///
        /// The generated Manual and Parameter pages come from the engineering
        /// device list (tools/generate_pages.py); each family is one nav entry with
        /// its groups as tabs, built lazily on first use.
        /// </summary>
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx)
        {
            return Kaban.UI.Screens.StandardScreens.Build(ctx, new Kaban.UI.Screens.StandardScreens.Options
            {
                Home = () => new Screens.HomeScreen(),

                ManualPages = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Screens.GeneratedPage("Tab_ManualClamping_Moulds", "Clamping Moulds", SecurityLevel.Operator, () => new Screens.Manual.ManualClamping_Moulds()),
                    new Screens.GeneratedPage("Tab_ManualCutting", "Cutting", SecurityLevel.Operator, () => new Screens.Manual.ManualCutting()),
                    new Screens.GeneratedPage("Tab_ManualEntry_Conveyor", "Entry Conveyor", SecurityLevel.Operator, () => new Screens.Manual.ManualEntry_Conveyor()),
                    new Screens.GeneratedPage("Tab_ManualExit", "Exit", SecurityLevel.Operator, () => new Screens.Manual.ManualExit()),
                    new Screens.GeneratedPage("Tab_ManualMilling", "Milling", SecurityLevel.Operator, () => new Screens.Manual.ManualMilling()),
                    new Screens.GeneratedPage("Tab_ManualOther", "Other", SecurityLevel.Operator, () => new Screens.Manual.ManualOther()),
                    new Screens.GeneratedPage("Tab_ManualProfile_Recognition", "Profile Recognition", SecurityLevel.Operator, () => new Screens.Manual.ManualProfile_Recognition()),
                    new Screens.GeneratedPage("Tab_ManualRobot", "Robot", SecurityLevel.Operator, () => new Screens.Manual.ManualRobot()),
                    new Screens.GeneratedPage("Tab_ManualYZ_Cross_Rollers", "YZ Cross Rollers", SecurityLevel.Operator, () => new Screens.Manual.ManualYZ_Cross_Rollers())
                },

                MachineSettings = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Screens.GeneratedPage("Tab_AxisSettings", "Axis Settings", SecurityLevel.Maintenance, () => new Screens.Parameters.AxisSettings()),
                    new Screens.GeneratedPage("Tab_ParamCutting", "Cutting", SecurityLevel.Maintenance, () => new Screens.Parameters.ParamCutting()),
                    new Screens.GeneratedPage("Tab_ParamDistances_Beetwen_Units", "Distances Beetwen Units", SecurityLevel.Maintenance, () => new Screens.Parameters.ParamDistances_Beetwen_Units()),
                    new Screens.GeneratedPage("Tab_ParamEntry_Conveyor", "Entry Conveyor", SecurityLevel.Maintenance, () => new Screens.Parameters.ParamEntry_Conveyor()),
                    new Screens.GeneratedPage("Tab_ParamExit_Conveyor", "Exit Conveyor", SecurityLevel.Maintenance, () => new Screens.Parameters.ParamExit_Conveyor()),
                    new Screens.GeneratedPage("Tab_ParamMachine_Lubrication", "Machine Lubrication", SecurityLevel.Maintenance, () => new Screens.Parameters.ParamMachine_Lubrication()),
                    new Screens.GeneratedPage("Tab_ParamMilling", "Milling", SecurityLevel.Maintenance, () => new Screens.Parameters.ParamMilling()),
                    new Screens.GeneratedPage("Tab_ParamRobot", "Robot", SecurityLevel.Maintenance, () => new Screens.Parameters.ParamRobot())
                },

                // Machine-specific: Role = "" keeps its own button (the DXF workbench).
                Extra = new[]
                {
                    new MachineScreen
                    {
                        TitleKey = "Btn_Profiles",
                        TitleFallback = "Profiles",
                        Icon = () => Kaban.UI.Properties.Resources.bar_chart_24,
                        SortOrder = 30,
                        CreateView = () => new Screens.ProfilesScreen()
                    }
                }
            });
        }
    }
}

