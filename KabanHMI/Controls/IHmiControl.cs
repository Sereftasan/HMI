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
}
