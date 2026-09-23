using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Interfaces;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// The DRIVER KINDS this build of the HMI can talk to - what the Driver combo
    /// box on Settings -> HMI Settings -> PLC Connection offers.
    ///
    /// Registration is EXPLICIT (the machine's Program.cs calls Register for each
    /// driver assembly it references) rather than reflected out of the loaded
    /// assemblies. Two reasons:
    ///
    ///   1. Kaban.Core must not reference a driver project - the drivers reference
    ///      Core, not the other way round. A reflected lookup would also miss a
    ///      driver assembly that the CLR has not lazily loaded yet, so the combo
    ///      box would be populated differently depending on what the operator had
    ///      already opened.
    ///   2. A machine exe is deliberately built against the drivers it is allowed
    ///      to use. Registering them by hand keeps that decision in the machine
    ///      project, where the 32-bit MX Component constraint already lives.
    ///
    /// A kind that fails to construct is NOT removed from the list - the operator
    /// must be able to select "Mitsubishi MX Component" on a PC where MX Component
    /// is missing and see the failure reported, rather than wonder why the option
    /// vanished.
    /// </summary>
    public static class PlcDriverCatalog
    {
        /// <summary>Id stored in the config file for the built-in simulator.</summary>
        public const string SimulationId = "Simulation";

        /// <summary>Id stored in the config file for MX Component / ActUtlType.</summary>
        public const string MitsubishiActUtlId = "MitsubishiActUtl";

        /// <summary>Selected when a controller is deliberately not connected at all.</summary>
        public const string NoneId = "None";

        public class DriverKind
        {
            /// <summary>Stable id written to the config file. Never localise this.</summary>
            public string Id = "";

            /// <summary>Translation key for the combo box caption.</summary>
            public string DisplayNameKey = "";

            /// <summary>Literal caption when the key is not in the translation files.</summary>
            public string DisplayNameFallback = "";

            /// <summary>
            /// True when the station number on the settings page is meaningful. For
            /// ActUtlType it is the MX Component LOGICAL STATION NUMBER, which has
            /// nothing to do with the HMI's own station id and is what the
            /// Communication Setting Utility stores. The simulator ignores it.
            /// </summary>
            public bool UsesLogicalStation = true;

            /// <summary>
            /// True when the driver reaches its PLC over the NETWORK and therefore
            /// needs an address rather than a station number. Mitsubishi is
            /// configured outside the HMI (in the MX utility) and so needs only a
            /// number; a Siemens or Delta CPU is addressed directly, so the panel
            /// has to be told where it is.
            /// </summary>
            public bool UsesNetworkEndpoint;

            /// <summary>Placeholder shown in the endpoint box, e.g. "192.168.0.1:0:1".</summary>
            public string EndpointHint = "";

            /// <summary>
            /// Builds a driver. logicalStation is meaningful only when
            /// UsesLogicalStation; endpoint only when UsesNetworkEndpoint. Both are
            /// always passed so a kind can use either without a second factory shape.
            /// </summary>
            public Func<int, string, IPlcDriver> Create;

            public string DisplayName
            {
                get { return LanguageManager.GetString(DisplayNameKey, DisplayNameFallback); }
            }

            public override string ToString() { return DisplayName; }
        }

        private static readonly List<DriverKind> _kinds = new List<DriverKind>();

        /// <summary>Every registered kind, in registration order.</summary>
        public static IReadOnlyList<DriverKind> Kinds { get { return _kinds; } }

        /// <summary>
        /// Declares a driver kind. Re-registering the same id REPLACES the entry
        /// rather than adding a duplicate, so a machine may override a default.
        /// </summary>
        public static void Register(string id, string displayNameKey, string displayNameFallback,
                                    Func<int, string, IPlcDriver> create,
                                    bool usesLogicalStation = true,
                                    bool usesNetworkEndpoint = false,
                                    string endpointHint = "")
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            var kind = new DriverKind
            {
                Id = id.Trim(),
                DisplayNameKey = displayNameKey ?? "",
                DisplayNameFallback = string.IsNullOrEmpty(displayNameFallback) ? id : displayNameFallback,
                Create = create,
                UsesLogicalStation = usesLogicalStation,
                UsesNetworkEndpoint = usesNetworkEndpoint,
                EndpointHint = endpointHint ?? ""
            };

            int existing = _kinds.FindIndex(k => string.Equals(k.Id, kind.Id, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0) _kinds[existing] = kind;
            else _kinds.Add(kind);
        }

        /// <summary>
        /// Adds the "not connected" option. Always offered, so a machine built with
        /// two PLCs can run with the second one physically absent without the HMI
        /// retrying a station that will never answer.
        /// </summary>
        public static void RegisterNone()
        {
            Register(NoneId, "Drv_None", "(not connected)", null, false, false);
        }

        public static DriverKind Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return _kinds.FirstOrDefault(k => string.Equals(k.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>True when the id names a simulator, which the monitor may edit freely.</summary>
        public static bool IsSimulation(string id)
        {
            return string.Equals(id, SimulationId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds a driver for a kind. Returns null (and logs) rather than throwing:
        /// a PC without MX Component must still start the HMI and show comm-lost,
        /// which is the behaviour Program.cs already relied on.
        /// </summary>
        public static IPlcDriver Create(string id, int logicalStation, string endpoint)
        {
            var kind = Find(id);
            if (kind == null)
            {
                AuditLogger.Log("[PLC DIAG] no driver kind registered under id '" + id + "'.");
                return null;
            }
            if (kind.Create == null) return null;    // "None" is not an error

            try
            {
                return kind.Create(logicalStation, endpoint ?? "");
            }
            catch (Exception ex)
            {
                AuditLogger.Log("[PLC DIAG] driver '" + kind.Id + "' could not be created for "
                                + (kind.UsesNetworkEndpoint ? endpoint : "logical station " + logicalStation)
                                + ": " + ex.Message);
                return null;
            }
        }
    }
}

