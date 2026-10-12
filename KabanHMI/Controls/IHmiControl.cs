using System;
using Kaban.Core.Enums;

namespace Kaban.UI.Controls
{
    public interface IHmiControl
    {
        void UpdateState(Func<string, int> lookup, Func<SystemCondition, bool> checkGlobal);
    }

    /// <summary>
    /// A control that reads ONE device and wants the scanner to fetch it.
    ///
    /// IHmiControl only promises UpdateState - it says nothing about what the
    /// control reads, so the scanner could not know which address to add to the
    /// batch. It compensated with a hardcoded list of six control types, and
    /// anything outside that list was silently never read: discovered by nobody,
    /// never in the batch, UpdateState never called. A MeterHMI bound to a real
    /// device simply stayed blank, which looks identical to a device reading zero.
    ///
    /// Implementing this is all a new control needs to do to be scanned.
    /// </summary>
    public interface IPlcBoundControl : IHmiControl
    {
        int PlcNumber { get; }
        string Address { get; }
    }

    /// <summary>
    /// A bound control that reads MORE devices than its Address - the axis "?"
    /// watches the axis's three code words as well as its error bit. The
    /// scanner adds each of these to the batch too; read as 16-bit words.
    /// </summary>
    public interface IPlcExtraAddresses : IPlcBoundControl
    {
        System.Collections.Generic.IEnumerable<string> ExtraAddresses { get; }
    }

    /// <summary>
    /// A control shown only while its visibility conditions are met, that is
    /// neither a numeric field, a button nor a lamp (whose conditions the scanner
    /// already knows): an axis row, a row of the axis monitor. The scanner reads
    /// the conditions' devices and updates it with its own station's values.
    /// </summary>
    public interface IConditionalControl : IHmiControl
    {
        int PlcNumber { get; }
        SystemCondition VisibilityConditions { get; }
    }
}
