using System.Collections.Generic;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Screens;

namespace Kaban.Machines.H800
{
    /// <summary>
    /// H800 - profile edge cleaning centre.
    ///
    /// The machine supplies only its OWN content; the left bar, its icons and
    /// the settings structure all come from Kaban.UI, so this machine cannot end
    /// up with a different or partial menu from any other.
    /// </summary>
    public class H800Module : IMachineModule, IMachineDisplayName, IMachineIcon
    {
        public string ModuleId { get { return "H800"; } }
        public string DisplayNameKey { get { return "Machine_H800"; } }

        /// <summary>What the machine does, in plain ink: a twist drill bit.</summary>
        public System.Drawing.Image Icon
        {
            get { return Kaban.UI.Controls.NavGlyphs.HingeDrilling(); }
        }

        public string DisplayName
        {
            get { return LanguageManager.GetString(DisplayNameKey, ModuleId); }
        }

        public SecurityLevel RequiredLevel { get { return SecurityLevel.Operator; } }

        public void RegisterPlc(IMachineMountContext ctx)
        {
            PlcManager.Instance.RegisterStation(ctx.Station(1), "H800 PLC",
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
                MachineName = "H800",
                // This machine's OWN controller: station 3 when H500 is mounted
                // under FA2010. "1" showed FA2010's PLC on the sub-machine's home.
                PlcNumber = ctx.Station(1),
                StartAddress = "B21",
                PauseAddress = "M25",
                CancelAddress = "M22",
                ResetAddress = "M621",
                AlarmLamp = "M28",
                OverrideAddress = "D408",
                TrendAddress = "W0",
                TrendCaption = "X 1 Actual Position trend",
            };
            spec.Axes.Add(new MachineHomeScreen.AxisSpec { Caption = "X 1", Address = "W0", DataType = DataTypeEnum.Bin_32, Divisor = 10000, DecimalPlaces = 1, Suffix = "mm", HomedAddress = "M1211" });
            spec.Axes.Add(new MachineHomeScreen.AxisSpec { Caption = "Y 1", Address = "W10", DataType = DataTypeEnum.Bin_32, Divisor = 10000, DecimalPlaces = 1, Suffix = "mm", HomedAddress = "M1212" });
            spec.Axes.Add(new MachineHomeScreen.AxisSpec { Caption = "Z 1", Address = "W20", DataType = DataTypeEnum.Bin_32, Divisor = 10000, DecimalPlaces = 1, Suffix = "mm", HomedAddress = "M1213" });
            spec.Axes.Add(new MachineHomeScreen.AxisSpec { Caption = "X 2", Address = "W30", DataType = DataTypeEnum.Bin_32, Divisor = 10000, DecimalPlaces = 1, Suffix = "mm", HomedAddress = "M1214" });
            spec.Axes.Add(new MachineHomeScreen.AxisSpec { Caption = "Y 2", Address = "W40", DataType = DataTypeEnum.Bin_32, Divisor = 10000, DecimalPlaces = 1, Suffix = "mm", HomedAddress = "M1215" });
            spec.Axes.Add(new MachineHomeScreen.AxisSpec { Caption = "Z 2", Address = "W50", DataType = DataTypeEnum.Bin_32, Divisor = 10000, DecimalPlaces = 1, Suffix = "mm", HomedAddress = "M1216" });
            return spec;
        }

        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx)
        {
            return StandardScreens.Build(ctx, new StandardScreens.Options
            {
                Home = () => new MachineHomeScreen(HomeSpec(ctx)),
                ManualPages = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Kaban.UI.Settings.SimplePage("Tab_H800_ManualAxes", "Axes",
                        SecurityLevel.Operator, () => new Screens.ManualAxes()),
                    new Kaban.UI.Settings.SimplePage("Tab_H800_ManualConveyor", "Conveyor",
                        SecurityLevel.Operator, () => new Screens.ManualConveyor()),
                    new Kaban.UI.Settings.SimplePage("Tab_H800_ManualDischarge", "Discharge",
                        SecurityLevel.Operator, () => new Screens.ManualDischarge()),
                    new Kaban.UI.Settings.SimplePage("Tab_H800_ManualDrilling", "Drilling",
                        SecurityLevel.Operator, () => new Screens.ManualDrilling()),
                    new Kaban.UI.Settings.SimplePage("Tab_H800_ManualOther", "Other",
                        SecurityLevel.Operator, () => new Screens.ManualOther()),
                    new Kaban.UI.Settings.SimplePage("Tab_H800_ManualProfile_Rotation", "Profile Rotation",
                        SecurityLevel.Operator, () => new Screens.ManualProfile_Rotation())
                },
                MachineSettings = new Kaban.UI.Settings.ISettingsPage[]
                {
                    new Kaban.UI.Settings.SimplePage("Tab_H800_AxisSettings", "Axis Settings",
                        SecurityLevel.Maintenance, () => new Screens.Parameters.AxisSettings()),
                    new Kaban.UI.Settings.SimplePage("Tab_H800_MachineOptions", "Options",
                        SecurityLevel.Maintenance, () => new Screens.MachineOptions()),
                    new Kaban.UI.Settings.SimplePage("Tab_H800_MachineParameters", "Parameters",
                        SecurityLevel.Maintenance, () => new Screens.MachineParameters()),
                    new Kaban.UI.Settings.SimplePage("Tab_H800_MachineParameters2", "Parameters2",
                        SecurityLevel.Maintenance, () => new Screens.MachineParameters2()),
                    new Kaban.UI.Settings.SimplePage("Tab_H800_MachineStatus", "Status",
                        SecurityLevel.Maintenance, () => new Screens.MachineStatus())
                }
            });
        }

        public Kaban.UI.Settings.ISettingsPage[] GetSettingsPages()
        {
            return new Kaban.UI.Settings.ISettingsPage[0];
        }
    }
}

