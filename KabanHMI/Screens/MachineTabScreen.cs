using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Machines;
using Kaban.UI.Settings;

namespace Kaban.UI.Screens
{
    /// <summary>
    /// Presents one navigation ROLE as tabs - one per machine that provides it.
    /// So "Home" is a single left-bar button whose tabs are Main, Screwing,
    /// Holing... instead of a button per machine, which is what keeps the left bar
    /// identical whether or not sub-machines are mounted.
    ///
    /// Built on the same SettingsScreen tab strip as everything else, so there is
    /// exactly one tab implementation in the program.
    /// </summary>
    public static class MachineTabScreen
    {
        /// <summary>
        /// Builds the view for a role group: the page itself when only one machine
        /// provides it, or a tab strip when several do.
        /// </summary>
        public static UserControl Build(MachineRegistry.RoleGroup group)
        {
            if (group == null || group.Pages.Count == 0) return null;

            // A single machine needs no tab strip - one tab would be noise.
            if (!group.IsTabbed)
                return group.Pages[0].Value.CreateView() as UserControl;

            var pages = new List<ISettingsPage>();
            foreach (var pair in group.Pages)
                pages.Add(new MachinePage(pair.Key, pair.Value));

            // The tab strip is HIDDEN: machine selection is the second-level menu in
            // the left bar now (MachineSelectorStrip). SettingsScreen still owns the
            // pages and the switching - only the row of tabs goes, because offering
            // the same choice twice means two "you are here" marks to keep in step.
            return new SettingsScreen(pages.ToArray()) { TabStripHidden = true };
        }

        /// <summary>One machine's contribution to a role, as a tab.</summary>
        private class MachinePage : ISettingsPage
        {
            private readonly MachineRegistry.MountedModule _mounted;
            private readonly MachineScreen _screen;

            public MachinePage(MachineRegistry.MountedModule mounted, MachineScreen screen)
            {
                _mounted = mounted;
                _screen = screen;
            }

            /// <summary>
            /// The tab is named after the MACHINE, not the page: under "Home" the
            /// tabs read Processing Center / Screwing_Holing / 3D_HingeDrilling.
            ///
            /// The MAIN machine is named the same way as any other. It used to read
            /// a literal "Main", which told the operator nothing - on a line with
            /// three machines, "Main" is the one word that does not identify one.
            /// </summary>
            public string TitleKey
            {
                get { return _mounted.Module.DisplayNameKey ?? ""; }
            }

            public string TitleFallback
            {
                get { return DisplayNameOf(_mounted); }
            }

            public SecurityLevel RequiredLevel { get { return _screen.RequiredLevel; } }

            public UserControl GetControl() { return _screen.CreateView() as UserControl; }
        }

        /// <summary>
        /// A machine's own name - "Processing Center", "Screwing_Holing" - resolved
        /// through the translation files, with the module id as the fallback so a
        /// machine that has not been given a name still shows something useful.
        /// </summary>
        public static string DisplayNameOf(MachineRegistry.MountedModule mounted)
        {
            if (mounted == null) return "";
            var named = mounted.Module as IMachineDisplayName;
            if (named != null && !string.IsNullOrWhiteSpace(named.DisplayName))
                return named.DisplayName;
            return mounted.Context.ModuleId;
        }
    }

    /// <summary>
    /// Implemented by a module that wants a human name in the tabs ("Screwing")
    /// instead of its module id.
    ///
    /// Implement DisplayName as
    ///     LanguageManager.GetString(DisplayNameKey, ModuleId)
    /// and NOT as LanguageManager.Localize(DisplayNameKey). Localize returns the
    /// raw KEY itself when the lookup fails - by design, it is meant for text
    /// that might be either a translation key or already-literal wording - and a
    /// tab showing "Machine_H500" instead of "Screwing_Holing" traced back to
    /// exactly that: a translation file that failed to load for an unrelated
    /// reason left nothing between the operator and the raw key. ModuleId is
    /// always present and always readable, so GetString's fallback belongs there.
    /// </summary>
    public interface IMachineDisplayName
    {
        string DisplayName { get; }
    }
}

