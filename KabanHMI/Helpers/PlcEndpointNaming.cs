using System;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Screens;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// Turns a controller into the caption the operator reads: "FA2010 PLC1",
    /// "Screwing PLC1".
    ///
    /// It lives in the UI layer, not in Kaban.Core with the endpoint itself,
    /// because the machine's human name comes from IMachineDisplayName - a UI
    /// interface. Core deliberately knows only the module id.
    ///
    /// ONE implementation on purpose: the PLC Connection page and the
    /// simulation monitor's tabs must name the same controller identically, or the
    /// operator cannot tell that the tab they are watching is the one they just
    /// configured.
    /// </summary>
    public static class PlcEndpointNaming
    {
        /// <summary>"FA2010 PLC1" - machine name then the machine's OWN PLC number.</summary>
        public static string Caption(PlcConnectionSettings.Endpoint endpoint)
        {
            if (endpoint == null) return "";

            // THE MACHINE NAME, and the controller number ONLY when the machine has
            // more than one.
            //
            // "Screwing & Holing PLC1" told the operator something true and useless:
            // that machine has one controller and always will. Printing the suffix
            // everywhere also meant it stopped carrying information on the machines
            // where it matters - a reader stops seeing it.
            return (MachineName(endpoint) + PlcSuffix(endpoint)).Trim();
        }

        /// <summary>" (PLC2)" when the machine has several controllers, else "".</summary>
        public static string PlcSuffix(PlcConnectionSettings.Endpoint endpoint)
        {
            if (endpoint == null) return "";

            int onThisMachine = PlcConnectionSettings.Endpoints()
                .Count(e => string.Equals(e.ModuleId, endpoint.ModuleId,
                                          StringComparison.OrdinalIgnoreCase));

            // JUST THE NUMBER. "(PLC2)" repeats a word the column heading already
            // says, and "(PLC1)" was printed on machines that only ever have one -
            // information that is true and useless.
            return onThisMachine > 1 ? " (" + endpoint.LocalPlcNumber + ")" : "";
        }

        /// <summary>The machine's human name, falling back to the module id.</summary>
        public static string MachineName(PlcConnectionSettings.Endpoint endpoint)
        {
            if (endpoint == null) return "";

            var mounted = MachineRegistry.Modules.FirstOrDefault(m =>
                string.Equals(m.Context.ModuleId, endpoint.ModuleId, StringComparison.OrdinalIgnoreCase));

            if (mounted != null)
            {
                // A sub-machine supplies its own name ("Screwing"); the main machine
                // usually does not, so its module id is the honest label - it is what
                // the machine is called on the shop floor.
                string name = MachineTabScreen.DisplayNameOf(mounted);
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }

            if (!string.IsNullOrWhiteSpace(endpoint.ModuleId)) return endpoint.ModuleId;

            // Legacy exe with no modules: fall back to the registered station name.
            return string.IsNullOrWhiteSpace(endpoint.StationName)
                ? LanguageManager.GetString("Lbl_Station", "Station") + " " + endpoint.StationId
                : endpoint.StationName;
        }

        /// <summary>
        /// "FA2010 PLC1  (station 3)" - for tooltips and headings, where the
        /// deployment's station id is the thing being diagnosed and hiding it would
        /// be unhelpful.
        /// </summary>
        public static string CaptionWithStation(PlcConnectionSettings.Endpoint endpoint)
        {
            if (endpoint == null) return "";
            return Caption(endpoint) + "  (" +
                   LanguageManager.GetString("Lbl_StationLower", "station") + " " + endpoint.StationId + ")";
        }
    }
}

