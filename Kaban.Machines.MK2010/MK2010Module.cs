using System.Collections.Generic;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Screens;

namespace Kaban.Machines.MK2010
{
    /// <summary>
    /// MK2010 - the machine the old program's _MK2010 project runs (11 November: "can you add
    /// "MK2010" project from "_MK2010" as well"): a welding machine of four heads - heater
    /// plates, supporting / back plates, top press, gasket pressing; its X, Y1, Y2 axes.
    ///
    /// Its pages come from the old program's (tools/legacy_mk2010_pages.py, from
    /// tools/legacy/MK2010): Home (the job: the heads' temperatures, the axes, the profile's
    /// melting and welding, width X / Y and quantity), Manual Control and References, and the
    /// old Machine Parameters' tabs. The machine supplies only its OWN content; the left bar,
    /// its icons and the settings structure all come from Kaban.UI.
    /// </summary>
    public class MK2010Module : IMachineModule, IMachineDisplayName
    {
        public string ModuleId { get { return "MK2010"; } }
        public string DisplayNameKey { get { return "Machine_MK2010"; } }

        public string DisplayName
        {
            get { return LanguageManager.GetString(DisplayNameKey, ModuleId); }
        }

        public SecurityLevel RequiredLevel { get { return SecurityLevel.Operator; } }

        public void RegisterPlc(IMachineMountContext ctx)
        {
            PlcManager.Instance.RegisterStation(ctx.Station(1), "MK2010 PLC",
                RequiredLevel, ctx.ModuleId, 1);

            // Where the PLC keeps its program version: not known yet (the old program read none).
            ConnectionMonitor.SetVersionDevice(ctx.Station(1), "");

            // Where the PLC resets its alarms: not known yet - the old program's alarm words start
            // at M600 (3 words) and its texts are in its database; until the right bit is written
            // here the Alarms page has no reset rather than one that pulses the wrong device.
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
                // the old Home > Job Preparations page's values (its profile list and barcode come
                // with the profile recipes)
                Home = () => Kaban.UI.Helpers.StationMap.Apply(ctx, new Screens.Home.HomeJob()),
                ManualPages = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Kaban.UI.Settings.SimplePage("Tab_MK2010_ManualControl", "Manual Control",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualControl()),
                    new Kaban.UI.Settings.SimplePage("Tab_MK2010_ManualReference", "References",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualReference())
                },
                // the old manual pages' bar: the axes' speed
                ManualBar = () => new Screens.Shared.JogBar(),
                // the old Machine Parameters' tabs; System Parameters 2 and Reports were behind the
                // old program's password: an Admin's here
                MachineSettings = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Kaban.UI.Settings.SimplePage("Tab_MK2010_ParamProduction", "Production Parameters",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamProduction()),
                    new Kaban.UI.Settings.SimplePage("Tab_MK2010_ParamCalibration", "Calibration",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamCalibration()),
                    new Kaban.UI.Settings.SimplePage("Tab_MK2010_ParamSystem", "System Parameters",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamSystem()),
                    new Kaban.UI.Settings.SimplePage("Tab_MK2010_ParamSystem2", "System Parameters 2",
                        SecurityLevel.Admin, () => new Screens.Parameters.ParamSystem2()),
                    new Kaban.UI.Settings.SimplePage("Tab_MK2010_ParamDemo", "Demo",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamDemo()),
                    new Kaban.UI.Settings.SimplePage("Tab_MK2010_ParamReports", "Reports",
                        SecurityLevel.Admin, () => new Screens.Parameters.ParamReports())
                }
            });
        }

        public Kaban.UI.Settings.ISettingsPage[] GetSettingsPages()
        {
            return new Kaban.UI.Settings.ISettingsPage[0];
        }
    }
}
