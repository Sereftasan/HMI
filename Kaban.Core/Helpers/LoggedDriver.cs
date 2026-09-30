using System;
using Kaban.Core.Interfaces;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// The driver PlcManager hands out: every write through it is in the PLC write
    /// log (PlcWriteLog), device by device, whether it succeeded or not (30 September).
    ///
    /// Outermost, so no write can pass the HMI's drivers without it: a button, a
    /// numeric entry, a dialog, the PLC Monitor, a helper written next year - all
    /// of them get their driver from PlcManager. Reads pass straight through.
    ///
    /// Code that needs the controller's own driver (its class, its memory dump)
    /// asks PlcManager.Unwrap for it; the bench simulation's movement writes
    /// through that one on purpose, being the PLC program's stand-in.
    /// </summary>
    public sealed class LoggedDriver : IPlcDriver, INonBlockingDriver, IPlcIdentity, IDisposable
    {
        private readonly IPlcDriver _inner;
        private readonly int _station;

        public LoggedDriver(int station, IPlcDriver inner)
        {
            if (inner == null) throw new ArgumentNullException("inner");
            _station = station;
            _inner = inner;
            PlcWriteLog.Warm(station);
        }

        /// <summary>The driver it logs for (a BoundedDriver, or the controller's own driver).</summary>
        public IPlcDriver Inner { get { return _inner; } }

        /// <summary>The station its writes are logged under.</summary>
        public int Station { get { return _station; } }

        public bool IsConnected { get { return _inner.IsConnected; } }
        public bool IsHealthy { get { return _inner.IsHealthy; } }

        public string Identity
        {
            get
            {
                var id = PlcManager.Unwrap(_inner) as IPlcIdentity;
                return id != null ? id.Identity ?? "" : "";
            }
        }

        public bool Connect() { return _inner.Connect(); }
        public void Disconnect() { _inner.Disconnect(); }
        public int Read(string address) { return _inner.Read(address); }
        public int[] ReadMulti(string[] addresses) { return _inner.ReadMulti(addresses); }
        public bool TryRead(string address, out int value) { return _inner.TryRead(address, out value); }
        public bool TryReadMulti(string[] addresses, out int[] values) { return _inner.TryReadMulti(addresses, out values); }

        public int Write(string address, int value)
        {
            var time = DateTime.Now;
            int rc;
            try { rc = _inner.Write(address, value); }
            catch
            {
                PlcWriteLog.Record(_station, address, value, -1, time);
                throw;
            }
            PlcWriteLog.Record(_station, address, value, rc, time);
            return rc;
        }

        public void Dispose()
        {
            var d = _inner as IDisposable;
            if (d != null) d.Dispose();
        }
    }
}
