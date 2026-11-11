using System.Collections.Generic;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Screens;

namespace Kaban.Machines.MD3020
{
    /// <summary>
    /// MD3020 - the machine the old program's _MD3030 project runs (11 November): four heads,
    /// each with its X, X2, Y1, Y2, Z1, Z2 servos, resistance, melting and welding, milling
    /// cutters, clampings; the body's X1 / X2 and Y1 / Y2 servos and the conveyor.
    ///
    /// The machine supplies only its OWN content; the left bar, its icons and
    /// the settings structure all come from Kaban.UI, so this machine cannot end
    /// up with a different or partial menu from any other.
    /// </summary>
    public class MD3020Module : IMachineModule, IMachineDisplayName
    {
        public string ModuleId { get { return "MD3020"; } }
        public string DisplayNameKey { get { return "Machine_MD3020"; } }

        public string DisplayName
        {
            get { return LanguageManager.GetString(DisplayNameKey, ModuleId); }
        }

        public SecurityLevel RequiredLevel { get { return SecurityLevel.Operator; } }

        public void RegisterPlc(IMachineMountContext ctx)
        {
            PlcManager.Instance.RegisterStation(ctx.Station(1), "MD3020 PLC",
                RequiredLevel, ctx.ModuleId, 1);

            // WHERE THE PLC KEEPS ITS PROGRAM VERSION.
            //
            // A property of the machine's ladder, not a setting: it is fixed when
            // the program is written and never changes in the field. Putting it on
            // a settings page invited someone to edit it, and an edited version
            // device reports the wrong version or none at all.
            ConnectionMonitor.SetVersionDevice(ctx.Station(1), "");

            // WHERE THE PLC RESETS ITS ALARMS - pulsed by the Alarms page's Reset
            // and the caption bar's alarm menu. NOT M102: that is FA2010's reset,
            // and on this machine it is another device. Until the right bit is
            // written below, this machine has no alarm reset on the HMI (the
            // button says so) rather than one that pulses the wrong device.
            // PlcManager.Instance.SetAlarmResetDevice(ctx.Station(1), "M...");
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
                // the old Home > Job Preparations (11 November: "can you update this main page as well"):
                // Start / Pause / Cancel, the speed, the frame's sizes and the active job's values
                // (tools/legacy_md3030_pages.py) - this machine's own controller when mounted under another
                Home = () => Kaban.UI.Helpers.StationMap.Apply(ctx, new Screens.Home.HomeJob()),
                // The old program's (_MD3030) pages, 11 November - tools/legacy_md3030_pages.py makes them
                // from its designers (tools/legacy/MD3030): Manual - General, Manual Movement (its
                // Input Output Watch tab showed old placeholder addresses: not brought over) and its
                // Servo Situations window; Parameters - Axis Calibration, Machine Parameters (General,
                // Position Definition), Speed, Process and Offset.
                ManualPages = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Kaban.UI.Settings.SimplePage("Tab_MD3020_ManualGeneral", "General",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualGeneral()),
                    new Kaban.UI.Settings.SimplePage("Tab_MD3020_ManualMovement", "Manual Movement",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualMovement()),
                    new Kaban.UI.Settings.SimplePage("Tab_MD3020_ManualServos", "Servos",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualServos())
                },
                // the old manual and parameter pages' bar: the axes' speed and JOG / 1 / 0.1 / 0.01 mm
                ManualBar = () => new Screens.Shared.JogBar(),
                MachineSettings = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Kaban.UI.Settings.SimplePage("Tab_MD3020_ParamAxisCalibration", "Axis Calibration",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamAxisCalibration()),
                    new Kaban.UI.Settings.SimplePage("Tab_MD3020_ParamGeneral", "General",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamGeneral()),
                    new Kaban.UI.Settings.SimplePage("Tab_MD3020_ParamPositionDefinition", "Position Definition",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamPositionDefinition()),
                    new Kaban.UI.Settings.SimplePage("Tab_MD3020_ParamSpeed", "Speed Parameters",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamSpeed()),
                    new Kaban.UI.Settings.SimplePage("Tab_MD3020_ParamProcess", "Process Parameters",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamProcess()),
                    new Kaban.UI.Settings.SimplePage("Tab_MD3020_ParamOffset", "Offset",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamOffset())
                }
            });
        }

        public Kaban.UI.Settings.ISettingsPage[] GetSettingsPages()
        {
            return new Kaban.UI.Settings.ISettingsPage[0];
        }
    }
}

