using System.Collections.Generic;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Screens;

namespace Kaban.Machines.H500
{
    /// <summary>
    /// H500 - the screwing and holing machine, its H500 FX5 version (12 October: its
    /// pages from the program the machine runs today - tools/legacy_h500_pages.py).
    ///
    /// The machine supplies only its OWN content; the left bar, its icons and
    /// the settings structure all come from Kaban.UI, so this machine cannot end
    /// up with a different or partial menu from any other.
    /// </summary>
    public class H500Module : IMachineModule, IMachineDisplayName, IMachineIcon
    {
        public string ModuleId { get { return "H500"; } }
        public string DisplayNameKey { get { return "Machine_H500"; } }

        /// <summary>What the machine does, in plain ink: a countersunk cross-head screw.</summary>
        public System.Drawing.Image Icon
        {
            get { return Kaban.UI.Controls.NavGlyphs.ScrewingHoling(); }
        }

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

        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx)
        {
            return StandardScreens.Build(ctx, new StandardScreens.Options
            {
                // The old H500 FX5 main control panel's Start, Pause and Cancel (12 October), on
                // this machine's OWN controller: station 3 when H500 is mounted under FA2010.
                Home = () => Kaban.UI.Helpers.StationMap.Apply(ctx, new Screens.HomeScreen()),
                // The old program's H500 FX5 pages (12 October): Entry Conveyor and Heads,
                // and its Parameters tab - not its Configuration tab (the profile tables).
                ManualPages = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Kaban.UI.Settings.SimplePage("Tab_H500_ManualEntry_Conveyor", "Entry Conveyor",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualEntry_Conveyor()),
                    new Kaban.UI.Settings.SimplePage("Tab_H500_ManualHeads", "Heads",
                        SecurityLevel.Operator, () => new Screens.Manual.ManualHeads())
                },
                MachineSettings = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Kaban.UI.Settings.SimplePage("Tab_H500_ParamMachine", "Parameters",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.ParamMachine())
                }
            });
        }

        public Kaban.UI.Settings.ISettingsPage[] GetSettingsPages()
        {
            return new Kaban.UI.Settings.ISettingsPage[0];
        }
    }
}

