using System;
using System.Collections.Generic;
using Kaban.Core.Enums;

namespace Kaban.Core.Machines
{
    /// <summary>
    /// A MACHINE MODULE - one machine's complete definition: its PLC stations,
    /// condition bits, alarms, translations and screens.
    ///
    /// The same class serves both roles:
    ///   MAIN machine  - hosts the HMI shell (has its own Program.cs / exe).
    ///   SUB machine   - mounted into another machine's shell (a screwdriving
    ///                   unit, a connection robot, ...) and contributes its
    ///                   screens to the host's navigation.
    ///
    /// Nothing in a module assumes which role it is playing, so a sub-machine can
    /// be promoted to a main machine later by adding an entry-point project, and a
    /// main machine can be mounted into a bigger line without changing its code.
    ///
    /// EVERYTHING a module registers is namespaced by ModuleId, so two modules can
    /// use the same alarm codes, condition names or translation keys without
    /// colliding - which is what makes "add any sub-machine to any main machine"
    /// safe.
    /// </summary>
    public interface IMachineModule
    {
        /// <summary>Stable unique id, e.g. "FA2010", "H500". Used to namespace everything.</summary>
        string ModuleId { get; }

        /// <summary>Translation key for the machine's display name (falls back to literal).</summary>
        string DisplayNameKey { get; }

        /// <summary>
        /// Declare PLC stations and the drivers for them. The host supplies a
        /// context that maps the module's LOGICAL station numbers onto real ones,
        /// so a sub-machine written against "station 1" can be mounted on station 3
        /// of the host without editing the module.
        /// </summary>
        void RegisterPlc(IMachineMountContext ctx);

        /// <summary>Declare condition bits/rules. Use ctx.Station()/ctx.AllocateMachineBit().</summary>
        void RegisterConditions(IMachineMountContext ctx);

        /// <summary>
        /// Declare data files (Alarms.json, Translations.csv, ...). Paths are
        /// resolved relative to the module's own folder by the context.
        /// </summary>
        void RegisterData(IMachineMountContext ctx);

        /// <summary>The navigation entries this module contributes to the shell.</summary>
        IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx);
    }

    /// <summary>One navigation entry (a page + its menu button).</summary>
    public class MachineScreen
    {
        /// <summary>Translation key for the button caption.</summary>
        public string TitleKey { get; set; } = "";
        /// <summary>Literal fallback if the key is not in the translation files.</summary>
        public string TitleFallback { get; set; } = "";
        /// <summary>
        /// Icon FACTORY (may be null). Lazy on purpose: resolving an image at mount
        /// time would load UI resources for pages the operator may never open, and
        /// makes mounting fail in headless/test contexts where resources are absent.
        /// </summary>
        public Func<object> Icon { get; set; }
        /// <summary>
        /// Factory rather than an instance, so screens are built only when the
        /// module is actually mounted (and can be rebuilt if a page is reset).
        /// </summary>
        public Func<object> CreateView { get; set; }
        /// <summary>Lower numbers appear higher in the menu.</summary>
        public int SortOrder { get; set; } = 100;
        /// <summary>Minimum level required to open the page.</summary>
        public SecurityLevel RequiredLevel { get; set; } = SecurityLevel.None;

        /// <summary>
        /// Screens sharing a ROLE become TABS of one navigation entry instead of a
        /// button each. This is what keeps the left bar identical on every machine:
        /// a main machine and its sub-machines all contribute a "Home", and the
        /// operator gets one Home button with a tab per machine. Empty = its own
        /// button (a machine-specific page such as FA2010's Profiles).
        /// </summary>
        public string Role { get; set; } = "";
    }

    /// <summary>The roles every machine is expected to provide.</summary>
    public static class ScreenRoles
    {
        public const string Home = "Home";
        public const string Manual = "Manual";
        public const string Alarms = "Alarms";

        /// <summary>The machine dashboard (the Industry 4 log): a tab per machine that has one.</summary>
        public const string Dashboard = "Dashboard";

        /// <summary>Per-machine parameters: becomes a tab per mounted machine.</summary>
        public const string MachineParameters = "MachineParameters";

        /// <summary>
        /// Settings of the HMI PROGRAM. Emitted by the main machine ONLY, so it is a
        /// single button with no tab strip however many machines are mounted - the
        /// same units, users and station connections serve all of them.
        /// </summary>
        public const string HmiSettings = "HmiSettings";

        /// <summary>
        /// A SIDE PAGE: not a button of the left menu but an icon on the bar at the
        /// right edge of the window. It opens BESIDE the page on display - the page
        /// narrows to make room, nothing is covered - for what is watched or done
        /// alongside the machine's own pages: inputs and outputs, axis positions,
        /// power, the order being run, second-level operations. Designed in Visual
        /// Studio like any page; its HMI controls are read like a page's.
        /// </summary>
        public const string SidePage = "SidePage";

        /// <summary>
        /// THE MACHINE'S AXES' MANUAL MOVEMENT BAR (14 October): the speed and the jog
        /// step - designed once (a MachineBarHMI of the machine's own). The program shows
        /// it above the Manual Movement tabs (StandardScreens Options.ManualBar); a page
        /// shows it from its constructor, in the place of a plain panel of its designer
        /// file (FA2010's Axis Settings - never held in a designer file, 23 October);
        /// declared here, the Axis Monitor - made by the program - shows it too. Not in the menu.
        /// </summary>
        public const string AxisBar = "AxisBar";
    }

    /// <summary>
    /// What a module is given when it is mounted. Provides station mapping,
    /// namespaced ids, condition-bit allocation and file resolution, so a module
    /// never has to know whether it is the main machine or a mounted sub-machine.
    /// </summary>
    public interface IMachineMountContext
    {
        string ModuleId { get; }

        /// <summary>True when this module hosts the shell; false when mounted into another.</summary>
        bool IsMainMachine { get; }

        /// <summary>
        /// Maps a module-local station number to the real one for this deployment.
        /// A module always talks about its own station 1, 2, ...; the host decides
        /// what those actually are.
        /// </summary>
        int Station(int localStationNumber);

        /// <summary>Prefixes an id with the module id, e.g. "H500.UnitFault".</summary>
        string Scoped(string localId);

        /// <summary>
        /// Reserves a machine-condition bit for this module. Modules must NOT
        /// hardcode bit numbers - two modules would otherwise claim the same bit.
        /// </summary>
        long AllocateMachineBit(string localTag);

        /// <summary>Resolves a file that ships with this module (Alarms.json etc.).</summary>
        string ResolveFile(string fileName);
    }
}

