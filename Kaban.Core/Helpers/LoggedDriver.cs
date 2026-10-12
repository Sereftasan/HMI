using System;
using System.Collections.Generic;
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
    ///
    /// A DEVICE OF ANOTHER CONTROLLER ("X12@2", 12 October) is sent to that
    /// controller's own driver - read, or written with its permission and
    /// connection checks and in its log - and a device marked with this one's
    /// number is read without the mark: no driver below ever sees an "@".
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

        /// <summary>
        /// The controller a device is on: this one (null, <paramref name="device"/>
        /// without the mark) or another ("X12@2": its station).
        /// </summary>
        private int? Elsewhere(string address, out string device)
        {
            int st;
            if (!PlcAddressHelper.TrySplitStation(address, out device, out st)) { device = address; return null; }
            return st == _station ? (int?)null : st;
        }

        /// <summary>Another controller's driver for reading (not permission-gated, as the scanner's).</summary>
        private static IPlcDriver ReaderOf(int station)
        {
            return PlcManager.Instance.GetDriverForMonitoring(station);
        }

        public int Read(string address)
        {
            string device;
            var other = Elsewhere(address, out device);
            if (other == null) return _inner.Read(device);
            var d = ReaderOf(other.Value);
            return d != null ? d.Read(device) : 0;
        }

        public bool TryRead(string address, out int value)
        {
            string device;
            var other = Elsewhere(address, out device);
            if (other == null) return _inner.TryRead(device, out value);
            var d = ReaderOf(other.Value);
            value = 0;
            return d != null && d.TryRead(device, out value);
        }

        public int[] ReadMulti(string[] addresses)
        {
            int[] values;
            if (!Mixed(addresses)) return _inner.ReadMulti(Plain(addresses));
            TryReadMulti(addresses, out values);
            return values;
        }

        public bool TryReadMulti(string[] addresses, out int[] values)
        {
            if (!Mixed(addresses)) return _inner.TryReadMulti(Plain(addresses), out values);

            // one batch per controller, the values put back in the order asked
            values = new int[addresses.Length];
            var groups = new Dictionary<int, List<int>>();
            var devices = new string[addresses.Length];
            for (int i = 0; i < addresses.Length; i++)
            {
                var other = Elsewhere(addresses[i], out devices[i]);
                int st = other ?? _station;
                List<int> g;
                if (!groups.TryGetValue(st, out g)) groups[st] = g = new List<int>();
                g.Add(i);
            }
            bool all = true;
            foreach (var kv in groups)
            {
                var d = kv.Key == _station ? _inner : ReaderOf(kv.Key);
                var batch = kv.Value.ConvertAll(i => devices[i]).ToArray();
                int[] got;
                if (d == null || !d.TryReadMulti(batch, out got) || got == null || got.Length != batch.Length) { all = false; continue; }
                for (int k = 0; k < batch.Length; k++) values[kv.Value[k]] = got[k];
            }
            return all;
        }

        /// <summary>True when a batch names a device of another controller.</summary>
        private bool Mixed(string[] addresses)
        {
            if (addresses == null) return false;
            foreach (var a in addresses)
            {
                string device;
                if (a != null && a.IndexOf(PlcAddressHelper.StationMark) >= 0 && Elsewhere(a, out device) != null) return true;
            }
            return false;
        }

        /// <summary>The batch without this controller's own marks ("M5@1" on PLC 1 is M5).</summary>
        private static string[] Plain(string[] addresses)
        {
            if (addresses == null) return null;
            string[] copy = null;
            for (int i = 0; i < addresses.Length; i++)
            {
                var a = addresses[i];
                if (a == null || a.IndexOf(PlcAddressHelper.StationMark) < 0) continue;
                if (copy == null) copy = (string[])addresses.Clone();
                copy[i] = PlcAddressHelper.DeviceOf(a);
            }
            return copy ?? addresses;
        }

        public int Write(string address, int value)
        {
            string device;
            var other = Elsewhere(address, out device);
            if (other != null)
            {
                // that controller's own driver: its permission, its connection, its log
                var d = PlcManager.Instance.GetDriverForWrite(other.Value);
                if (d == null)
                {
                    PlcWriteLog.Record(other.Value, device, value, -1, DateTime.Now);
                    return -1;
                }
                return d.Write(device, value);
            }
            address = device;
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
