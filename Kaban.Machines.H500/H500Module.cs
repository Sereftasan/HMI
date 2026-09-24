using System.Collections.Generic;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Screens;

namespace Kaban.Machines.H500
{
    /// <summary>
    /// H500 - profile edge cleaning centre.
    ///
    /// The machine supplies only its OWN content; the left bar, its icons and
    /// the settings structure all come from Kaban.UI, so this machine cannot end
    /// up with a different or partial menu from any other.
    /// </summary>
    public class H500Module : IMachineModule, IMachineDisplayName
    {
        public string ModuleId { get { return "H500"; } }
        public string DisplayNameKey { get { return "Machine_H500"; } }

        public string DisplayName
        {
            get { return LanguageManager.GetString(DisplayNameKey, ModuleId); }
        }

        public SecurityLevel RequiredLevel { get { return SecurityLevel.Operator; } }

        public void RegisterPlc(IMachineMountContext ctx)
        {
            PlcManager.Instance.RegisterStation(ctx.Station(1), "H500 PLC",
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

        /// <summary>
        /// The home screen is described, not hand-laid - see MachineHomeScreen.
        /// Addresses come from this machine's own label list.
        /// </summary>
        private MachineHomeScreen.MachineHomeSpec HomeSpec(IMachineMountContext ctx)
        {
            var spec = new MachineHomeScreen.MachineHomeSpec
            {
                MachineName = "H500",
                // This machine's OWN controller: station 3 when H500 is mounted
                // under FA2010. "1" showed FA2010's PLC on the sub-machine's home.
                PlcNumber = ctx.Station(1),
            };
            return spec;
        }

        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx)
        {
            return StandardScreens.Build(ctx, new StandardScreens.Options
            {
                Home = () => new MachineHomeScreen(HomeSpec(ctx)),
                ManualPages = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Kaban.UI.Settings.SimplePage("Tab_H500_ManualOther", "Other",
                        SecurityLevel.Operator, () => new Screens.ManualOther()),
                    new Kaban.UI.Settings.SimplePage("Tab_H500_ManualProfile_Rotation", "Profile Rotation",
                        SecurityLevel.Operator, () => new Screens.ManualProfile_Rotation())
                },
                MachineSettings = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Kaban.UI.Settings.SimplePage("Tab_H500_MachineParameters", "Parameters",
                        SecurityLevel.Maintenance, () => new Screens.MachineParameters())
                }
            });
        }

        public Kaban.UI.Settings.ISettingsPage[] GetSettingsPages()
        {
            return new Kaban.UI.Settings.ISettingsPage[0];
        }
    }
}

