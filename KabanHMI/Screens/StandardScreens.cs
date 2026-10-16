using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Machines;
using Kaban.UI.Settings;

namespace Kaban.UI.Screens
{
    /// <summary>
    /// The navigation every machine shares - Home, Manual Movement, Alarms and
    /// Settings - built ONCE here instead of being re-declared (and drifting) in
    /// every machine project.
    ///
    /// A module supplies only what is genuinely its own: the view for its Home
    /// page, its manual-movement pages, and any machine settings. Everything else
    /// - the icons, the roles, the sort order, the security levels, the HMI
    /// settings, the alarm page - comes from here, so a new machine gets a
    /// complete, consistent left bar with a few lines of code.
    ///
    /// Screens declare a Role, so when sub-machines are mounted their pages become
    /// TABS of the main machine's entries rather than extra buttons.
    /// </summary>
    public static class StandardScreens
    {
        /// <summary>What a machine contributes to the shared navigation.</summary>
        public class Options
        {
            /// <summary>The machine's own Home view. Required.</summary>
            public Func<UserControl> Home;

            /// <summary>Manual-movement pages, shown as tabs. Optional.</summary>
            public ISettingsPage[] ManualPages;

            /// <summary>
            /// A bar shown above the Manual Movement tabs, over every one of them (FA2010:
            /// the axes' speed and jog step, Screens\Shared\AxisJogBar - 16 October).
            /// Optional; made when the Manual Movement page is.
            /// </summary>
            public Func<Control> ManualBar;

            /// <summary>This machine's settings pages (parameters, barcode...). Optional.</summary>
            public ISettingsPage[] MachineSettings;

            /// <summary>Extra machine-specific entries (FA2010's Profiles, say).</summary>
            public IEnumerable<MachineScreen> Extra;

            /// <summary>Set false on a sub-machine mounted inside a host.</summary>
            public bool IncludeHmiSettings = true;

            /// <summary>
            /// The machine's DASHBOARD (the Industry 4 dashboard's files it ships:
            /// Dashboard\config.json, StateMap.json, Translation_JSON.csv). Given, the
            /// machine gets a Dashboard page and a Dashboard tab in Machine
            /// Parameters, and its automatic reports are made. Optional.
            /// </summary>
            public Kaban.UI.Dashboard.DashboardDefinition Dashboard;
        }

        /// <summary>
        /// A sub-machine's designer pages, translated to host stations as they are
        /// built - see StationMap. Home and Extra screens are custom code that asks
        /// ctx.Station() itself, so they are left alone rather than translated twice.
        /// </summary>
        private static ISettingsPage[] MapPages(IMachineMountContext ctx, ISettingsPage[] pages)
        {
            if (ctx == null || ctx.IsMainMachine || pages == null) return pages;
            var mapped = new ISettingsPage[pages.Length];
            for (int i = 0; i < pages.Length; i++)
                mapped[i] = pages[i] == null ? null : new Kaban.UI.Helpers.StationMappedPage(pages[i], ctx);
            return mapped;
        }

        public static IEnumerable<MachineScreen> Build(IMachineMountContext ctx, Options options)
        {
            if (options == null) yield break;

            // ---------------- HOME ----------------
            if (options.Home != null)
            {
                yield return new MachineScreen
                {
                    TitleKey = "Btn_Home",
                    TitleFallback = "Home",
                    Role = ScreenRoles.Home,
                    Icon = () => Properties.Resources.home_24,
                    SortOrder = 10,
                    CreateView = () => options.Home()
                };
            }

            // ---------------- ALARMS ----------------
            yield return new MachineScreen
            {
                TitleKey = "Btn_Alarms",
                TitleFallback = "Alarms",
                Role = ScreenRoles.Alarms,
                Icon = () => Properties.Resources.error_24,
                SortOrder = 20,
                CreateView = () => new MachineAlarmScreen(ctx)
            };

            // ---------------- MANUAL MOVEMENT ----------------
            if (options.ManualPages != null && options.ManualPages.Length > 0)
            {
                var pages = MapPages(ctx, options.ManualPages);
                yield return new MachineScreen
                {
                    TitleKey = "Btn_ManualMovement",
                    TitleFallback = "Manual Movement",
                    Role = ScreenRoles.Manual,
                    Icon = () => Properties.Resources.manual_move_24,
                    SortOrder = 40,
                    RequiredLevel = SecurityLevel.Operator,
                    CreateView = () =>
                    {
                        // with a bar above the tabs, a single page gets a screen too (the bar over it)
                        // marked: Cylinder FW/Back works (and its button is shown) on this page only (17 October)
                        if (pages.Length == 1 && options.ManualBar == null)
                        {
                            var single = (UserControl)pages[0].GetControl();
                            Kaban.UI.Helpers.UnitCycling.MarkManualPage(single);
                            return single;
                        }
                        var screen = new SettingsScreen(pages);
                        if (options.ManualBar != null) screen.TopBar = ManualBarFor(ctx, options.ManualBar);
                        Kaban.UI.Helpers.UnitCycling.MarkManualPage(screen);
                        return screen;
                    }
                };
            }

            // ---------------- DASHBOARD ----------------
            // What the machine did over a day, a week, a shift: from the Industry 4
            // log. Anyone may look. Its settings are the HMI's (HMI Settings > Logs and
            // Reports, the machine's \u2699) and the page's own \u2699 - not a Machine
            // Parameters tab: the machine's log and the HMI's are one subject.
            Kaban.UI.Dashboard.MachineDashboard dashboard = null;
            if (options.Dashboard != null)
            {
                var def = options.Dashboard;
                dashboard = Kaban.UI.Dashboard.DashboardService.Register(ctx.ModuleId,
                    ctx.ResolveFile(def.ConfigFile), ctx.ResolveFile(def.StateMapFile), ctx.ResolveFile(def.TranslationFile));
                var board = dashboard;
                yield return new MachineScreen
                {
                    TitleKey = "Btn_Dashboard",
                    TitleFallback = "Dashboard",
                    Role = ScreenRoles.Dashboard,
                    Icon = () => Kaban.UI.Controls.NavGlyphs.Dashboard(),
                    SortOrder = 25,
                    CreateView = () => new Kaban.UI.Dashboard.DashboardScreen(board)
                };
            }

            // ---------------- machine-specific entries ----------------
            if (options.Extra != null)
                foreach (var extra in options.Extra)
                    yield return extra;

            // ---------------- MACHINE PARAMETERS ----------------
            // Role-grouped like Home and Alarms, so mounting a sub-machine adds a
            // TAB here (named after the machine) rather than another left-bar button.
            var machineSettings = MapPages(ctx, options.MachineSettings ?? new ISettingsPage[0]);
            yield return new MachineScreen
            {
                TitleKey = "Btn_MachineParameters",
                TitleFallback = "Machine Parameters",
                Role = ScreenRoles.MachineParameters,
                // THE GEAR: the Machine Parameters picture in every other Kaban
                // HMI, so an operator who knows those finds it here at once. (It
                // was HMI Settings' picture, with sliders here - the two were
                // swapped against what the operators were used to.)
                Icon = () => Properties.Resources.settings_24,
                SortOrder = 900,
                RequiredLevel = SecurityLevel.Maintenance,
                CreateView = () => BuildMachineParameters(ctx, machineSettings)
            };

            // ---------------- HMI SETTINGS ----------------
            // Units, users, logs, PLC connections, the manual: these belong to
            // the HMI PROGRAM, not to a machine, and the same values are used by
            // every machine in the shell. So exactly ONE entry is emitted, by the
            // main machine only.
            //
            // Emitting it per machine and letting the role grouping collapse it would
            // produce a tab strip reading "Processing Center | Screwing_Holing" over
            // identical content - inviting the operator to wonder which machine's
            // units they were setting, when there is only one set.
            if (ctx.IsMainMachine && options.IncludeHmiSettings)
            {
                yield return new MachineScreen
                {
                    TitleKey = "Btn_HmiSettings",
                    TitleFallback = "HMI Settings",
                    Role = ScreenRoles.HmiSettings,
                    // A screen with a small gear: the settings of the program on
                    // this panel, not of a machine. The gear alone is Machine
                    // Parameters' picture (as in the other Kaban HMIs).
                    Icon = () => Kaban.UI.Controls.NavGlyphs.HmiSettings(),
                    SortOrder = 910,
                    CreateView = () => new HmiSettingsPage().GetControl()
                };
            }
        }

        /// <summary>The machine's bar for its Manual Movement page - a sub-machine's on the host's stations.</summary>
        private static Control ManualBarFor(IMachineMountContext ctx, Func<Control> make)
        {
            try
            {
                var bar = make();
                if (bar != null && ctx != null && !ctx.IsMainMachine) Kaban.UI.Helpers.StationMap.Apply(ctx, bar);
                return bar;
            }
            catch (Exception ex)
            {
                Kaban.Core.Helpers.AuditLogger.Log("the Manual Movement bar of " + (ctx != null ? ctx.ModuleId : "?") + " could not be made: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// THIS machine's parameter pages. One machine's worth only: the role
        /// grouping turns the set of them into tabs, so this must not enumerate the
        /// registry itself or every tab would contain every machine.
        /// </summary>
        private static UserControl BuildMachineParameters(IMachineMountContext ctx,
                                                          ISettingsPage[] machineSettings)
        {
            var mounted = FindMounted(ctx);
            // A MOUNTED machine keeps the settings pages it declared - H800's Axis
            // Settings used to vanish under FA2010, because only a settings
            // provider was asked for. These arrive already translated to the host
            // stations (MapPages); a provider's pages are translated here too.
            var pages = mounted != null && !mounted.IsMain
                ? (machineSettings != null && machineSettings.Length > 0
                    ? Combine(machineSettings, new AlarmSettingsPage(ctx))
                    : MapPages(ctx, SettingsOf(mounted)))
                : Combine(machineSettings, new AlarmSettingsPage(ctx));

            if (pages.Length == 0)
                return new PlaceholderControl("Btn_MachineParameters", "Machine Parameters",
                                              "No parameters for this machine yet.");
            if (pages.Length == 1) return pages[0].GetControl();
            return new SettingsScreen(pages);
        }

        private static MachineRegistry.MountedModule FindMounted(IMachineMountContext ctx)
        {
            foreach (var m in MachineRegistry.Modules)
                if (ReferenceEquals(m.Context, ctx)) return m;
            return null;
        }

        private static ISettingsPage[] Combine(ISettingsPage[] pages, params ISettingsPage[] more)
        {
            var list = new List<ISettingsPage>(pages ?? new ISettingsPage[0]);
            list.AddRange(more);
            return list.ToArray();
        }

        /// <summary>
        /// A mounted sub-machine's own settings. Every machine gets at least the
        /// alarm settings tab, so severities and helper links can be set for it.
        /// </summary>
        private static ISettingsPage[] SettingsOf(MachineRegistry.MountedModule mounted)
        {
            var provider = mounted.Module as IMachineSettingsProvider;
            var own = provider != null ? provider.GetSettingsPages() : null;
            return Combine(own ?? new ISettingsPage[0], new AlarmSettingsPage(mounted.Context));
        }
    }

    /// <summary>
    /// Implemented by a module that has settings of its own to show under
    /// Settings > Machine Parameters > (that machine).
    /// </summary>
    public interface IMachineSettingsProvider
    {
        ISettingsPage[] GetSettingsPages();
    }
}

