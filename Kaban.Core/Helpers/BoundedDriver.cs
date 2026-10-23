using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using Kaban.Core.Interfaces;
using Microsoft.Win32.SafeHandles;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// Gives a blocking driver its own thread, so no call to it can hold the UI.
    ///
    /// The connection poll and the page scanner call drivers from the UI thread.
    /// A Siemens or Delta driver opens TCP connections and waits on them there -
    /// up to 1.5 s per attempt against a PLC that is not answering, repeated every
    /// second by the poll. The screen was frozen more often than not; the login
    /// could not be reached.
    ///
    /// Here every call runs on the driver's own thread and the caller waits a
    /// BOUNDED time. A dead link is answered at once; a connect already in flight
    /// is not queued behind; a read that stops answering gives up in a second and
    /// marks the link down. Same rules as the MX Component driver's own thread.
    /// </summary>
    public sealed class BoundedDriver : IPlcDriver, INonBlockingDriver, IPlcBlockAccess, IDisposable
    {
        private readonly IPlcDriver _inner;
        private readonly Thread _thread;
        private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();
        private int _busy;
        /// <summary>Callers waiting for the thread - a bulk call (a block) lets them go first.</summary>
        private int _waiting;
        private volatile bool _connected, _healthy;

        /// <summary>How long the UI waits for a connect: a PLC on the LAN answers well inside it.</summary>
        public const int ConnectWaitMs = 50;
        /// <summary>How long the UI waits for a read or write.</summary>
        public const int CallWaitMs = 1000;

        public BoundedDriver(IPlcDriver inner)
        {
            if (inner == null) throw new ArgumentNullException("inner");
            _inner = inner;
            _thread = new Thread(() => { foreach (var work in _queue.GetConsumingEnumerable()) work(); })
            {
                IsBackground = true,
                Name = "PLC driver " + inner.GetType().Name
            };
            _thread.Start();
        }

        /// <summary>The wrapped driver, for code that needs its concrete type.</summary>
        public IPlcDriver Inner { get { return _inner; } }

        public bool IsConnected { get { return _connected; } }
        public bool IsHealthy { get { return _healthy; } }
        public bool IsBusy { get { return Volatile.Read(ref _busy) != 0; } }

        public bool Connect()
        {
            if (Thread.CurrentThread == _thread) return ConnectHere();
            if (IsBusy) return _connected;                      // an attempt is already in flight
            bool ok;
            return Run(ConnectHere, ConnectWaitMs, out ok) ? ok : _connected;
        }

        private bool ConnectHere()
        {
            bool ok;
            try { ok = _inner.Connect(); } catch { ok = false; }
            Refresh();
            return ok;
        }

        public void Disconnect()
        {
            bool ignored;
            Run(() => { try { _inner.Disconnect(); } catch { } Refresh(); return true; }, CallWaitMs, out ignored);
        }

        public bool TryRead(string address, out int value)
        {
            value = 0;
            if (!_connected) return false;
            Tuple<bool, int> r;
            if (!Run(() => { int v; bool ok = _inner.TryRead(address, out v); Refresh(); return Tuple.Create(ok, v); }, CallWaitMs, out r))
            { Stalled(); return false; }
            value = r.Item2;
            return r.Item1;
        }

        public bool TryReadMulti(string[] addresses, out int[] values)
        {
            values = null;
            if (!_connected) return false;
            Tuple<bool, int[]> r;
            if (!Run(() => { int[] v; bool ok = _inner.TryReadMulti(addresses, out v); Refresh(); return Tuple.Create(ok, v); }, CallWaitMs, out r))
            { Stalled(); return false; }
            values = r.Item2;
            return r.Item1;
        }

        public int Read(string address) { int v; TryRead(address, out v); return v; }

        public int[] ReadMulti(string[] addresses)
        {
            int[] v;
            return TryReadMulti(addresses, out v) && v != null ? v : new int[addresses == null ? 0 : addresses.Length];
        }

        public int Write(string address, int value)
        {
            if (!_connected) return -1;
            int rc;
            if (!Run(() => { int x = _inner.Write(address, value); Refresh(); return x; }, CallWaitMs, out rc))
            { Stalled(); return -1; }
            return rc;
        }

        // ---- A RUN OF DEVICES (23 October: the PLC Connection page's Read and Write) ----------------------
        //
        // On the driver's own thread in ONE turn: its controller's block call when it has one, else device
        // by device (PlcBlocks) right there - and the turn is taken politely: while anyone else waits for
        // the thread, a block waits. Back to back, the device-by-device batches used to go through the
        // slot one at a time and kept the scanner and the connection beat out until they gave up.

        /// <summary>How long a block waits for its turn and its answer (device by device it is many round trips).</summary>
        public const int BlockWaitMs = 10000;

        public bool RefusesOutOfRange
        {
            get { var b = _inner as IPlcBlockAccess; return b != null && b.RefusesOutOfRange; }
        }

        public bool TryReadBlock(string first, int count, out short[] values)
        {
            values = null;
            if (!_connected) return false;
            Tuple<bool, short[]> r;
            bool started;
            if (!Run(() =>
                {
                    short[] v;
                    var b = _inner as IPlcBlockAccess;
                    bool ok = b != null ? b.TryReadBlock(first, count, out v) : PlcBlocks.ReadByDevice(_inner, first, count, out v);
                    Refresh();
                    return Tuple.Create(ok, v);
                }, BlockWaitMs, true, out r, out started))
            {
                if (started) Stalled();
                return false;
            }
            values = r.Item2;
            return r.Item1;
        }

        public int WriteBlock(string first, short[] values)
        {
            if (!_connected) return -1;
            int rc;
            bool started;
            if (!Run(() =>
                {
                    var b = _inner as IPlcBlockAccess;
                    int x = b != null ? b.WriteBlock(first, values) : PlcBlocks.WriteByDevice(_inner, first, values);
                    Refresh();
                    return x;
                }, BlockWaitMs, true, out rc, out started))
            {
                if (started) Stalled();
                return -1;
            }
            return rc;
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            var d = _inner as IDisposable;
            if (d != null) try { d.Dispose(); } catch { }
        }

        // The flags are read by the UI thread and set only here, on the worker,
        // right after the driver itself has been asked - never by touching the
        // driver's own objects from another thread.
        private void Refresh()
        {
            try { _connected = _inner.IsConnected; _healthy = _inner.IsHealthy; }
            catch { _connected = _healthy = false; }
        }

        private void Stalled() { _connected = false; _healthy = false; }

        private bool Run<T>(Func<T> work, int timeoutMs, out T result)
        {
            bool started;
            return Run(work, timeoutMs, false, out result, out started);
        }

        /// <param name="polite">A bulk call: it waits while anyone else is waiting for the thread.</param>
        /// <param name="started">False when it never had its turn within the time: busy, not dead.</param>
        private bool Run<T>(Func<T> work, int timeoutMs, bool polite, out T result, out bool started)
        {
            result = default(T);
            started = false;
            if (Thread.CurrentThread == _thread) { started = true; result = work(); return true; }

            // Wait for the slot within the same deadline - a normal call takes
            // milliseconds; refusing it would fail good reads.
            int deadline = Environment.TickCount + timeoutMs;
            bool counted = false;
            try
            {
                while (true)
                {
                    if (!(polite && Volatile.Read(ref _waiting) > 0) && Interlocked.CompareExchange(ref _busy, 1, 0) == 0) break;
                    if (!polite && !counted) { Interlocked.Increment(ref _waiting); counted = true; }
                    if (Environment.TickCount - deadline >= 0) return false;
                    Thread.Sleep(1);
                }
            }
            finally { if (counted) Interlocked.Decrement(ref _waiting); }
            started = true;

            // ONE KERNEL EVENT PER CALL, and it used to be left for the finalizer:
            // at a 100 ms scan plus the 1 s beat that is thousands of undisposed
            // handles a minute per station, freed only when the GC gets round to
            // them. It cannot simply be disposed after the wait, because on a
            // timeout the worker still Set()s it later. So both sides hold a
            // reference and whichever finishes second disposes it.
            var done = new ManualResetEvent(false);
            int refs = 2;
            Action release = () => { if (Interlocked.Decrement(ref refs) == 0) done.Dispose(); };
            T value = default(T);
            bool ok = false;
            try
            {
                _queue.Add(() =>
                {
                    try { value = work(); ok = true; }
                    catch { ok = false; }
                    // Order matters: free the slot, THEN signal - the caller's next
                    // call must find the slot free (see StaWorker for the history).
                    finally { Volatile.Write(ref _busy, 0); done.Set(); release(); }
                });
            }
            catch { Volatile.Write(ref _busy, 0); done.Dispose(); return false; }

            try
            {
                if (!WaitWithoutPumping(done, Math.Max(0, deadline - Environment.TickCount))) return false;
            }
            finally { release(); }
            result = value;
            return ok;
        }

        /// <summary>
        /// Waits without dispatching window messages, so no click or timer can run
        /// inside a PLC call (see StaWorker in the MX driver for the full story).
        /// </summary>
        private static bool WaitWithoutPumping(ManualResetEvent handle, int timeoutMs)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                return WaitForSingleObject(handle.SafeWaitHandle, (uint)timeoutMs) == 0;
            return handle.WaitOne(timeoutMs);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(SafeWaitHandle handle, uint milliseconds);
    }
}

