using System.Collections.Generic;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Screens;

namespace Kaban.Machines.SB3015
{
    /// <summary>
    /// SB3015 - the machine the old program's _SB3015 project runs (17 November: "Can you complete
    /// SB3015 ... using KabanMachineSystem project"): a profile machining centre - the entry
    /// conveyor and robot (X), profile recognition, two milling units on a turning angle (A, Y1 Z1,
    /// Y2 Z2), the saw on its angle and Z, the exit robot (X) and the exit conveyor.
    ///
    /// Its pages come from the old program's (tools/legacy_sb3015_pages.py, from tools/legacy/SB3015):
    /// the manual tabs, the parameters' tabs and the manual pages' bar; its cylinders are cylinder
    /// units, its axes axis units and axis settings rows, its motors on an inverter inverter units.
    /// The machine supplies only its OWN content; the left bar, its icons and the settings structure
    /// all come from Kaban.UI.
    /// </summary>
    public class SB3015Module : IMachineModule, IMachineDisplayName
    {
        public string ModuleId { get { return "SB3015"; } }
        public string DisplayNameKey { get { return "Machine_SB3015"; } }

        public string DisplayName
        {
            get { return LanguageManager.GetString(DisplayNameKey, ModuleId); }
        }

        public SecurityLevel RequiredLevel { get { return SecurityLevel.Operator; } }

        public void RegisterPlc(IMachineMountContext ctx)
        {
            PlcManager.Instance.RegisterStation(ctx.Station(1), "SB3015 PLC",
                RequiredLevel, ctx.ModuleId, 1);

            // Where the PLC keeps its program version: not known yet (the old program read none).
            ConnectionMonitor.SetVersionDevice(ctx.Station(1), "");

            // WHERE THE PLC RESETS ITS ALARMS - pulsed by the Alarms page's Reset and the caption
            // bar's alarm menu: M102, the old program's Reset on its manual pages' bar
            // (ucHeadControlButtons: btnReset M102, lit by M800).
            PlcManager.Instance.SetAlarmResetDevice(ctx.Station(1), "M102");
        }

        public void RegisterConditions(IMachineMountContext ctx) { }

        public void RegisterData(IMachineMountContext ctx)
        {
            LanguageManager.AddMachineTranslations(ctx.ResolveFile("Translations.csv"));
            AlarmDefinitionLoader.Load(ctx.ResolveFile("Alarms.json"),
                replaceExisting: false, moduleId: ctx.ModuleId, stationRemap: ctx.Station);
        }

        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx)
        {
            return StandardScreens.Build(ctx, new StandardScreens.Options
            {
                // the old Home is the job preparation (the old program's shared job list and profile
                // library - not brought over yet): here the axes' positions and the machine's state
                Home = () => Kaban.UI.Helpers.StationMap.Apply(ctx, new Screens.Home.HomeOverview()),
                // the old Manual's tabs (Clamping Moulds and YZ Cross Rollers were hidden or empty there;
                // its Servo Status is on Axis Settings)
                ManualPages = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ManualEntryConveyor", "Entry Conveyor",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualEntryConveyor()),
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ManualRobot", "Robot",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualRobot()),
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ManualProfileRecognition", "Profile Recognition",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualProfileRecognition()),
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ManualMilling", "Milling",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualMilling()),
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ManualCutting", "Cutting",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualCutting()),
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ManualExit", "Exit",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualExit()),
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ManualOther", "Other",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualOther())
                },
                // the old manual pages' bar: the axes' speed, how far a press moves them, Reset, Security ByPass
                ManualBar = () => new Screens.Shared.JogBar(),
                // the old Parameters' tabs (they were behind the old program's parameter password: a
                // Maintenance user's here)
                MachineSettings = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ParamAxisSettings", "Axis Settings",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamAxisSettings()),
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ParamAxisSpeeds", "Axis Speeds",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamAxisSpeeds()),
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ParamEncoders", "Encoders",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamEncoders()),
                    // the old Encoders tab's two front verticals: positioned by their analogue sensors
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ParamPositioning", "Positioning",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamPositioning()),
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ParamEntry", "Entry",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamEntry()),
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ParamProcess", "Process",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamProcess()),
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ParamOther", "Other",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamOther()),
                    new Kaban.UI.Settings.SimplePage("Tab_SB3015_ParamRobotAxisCalibration", "Robot Axis Calibration",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamRobotAxisCalibration())
                }
            });
        }

        public Kaban.UI.Settings.ISettingsPage[] GetSettingsPages()
        {
            return new Kaban.UI.Settings.ISettingsPage[0];
        }
    }
}
