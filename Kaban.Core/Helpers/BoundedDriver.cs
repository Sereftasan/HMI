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
    public sealed class BoundedDriver : IPlcDriver, INonBlockingDriver, IDisposable
    {
        private readonly IPlcDriver _inner;
        private readonly Thread _thread;
        private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();
        private int _busy;
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
            result = default(T);
            if (Thread.CurrentThread == _thread) { result = work(); return true; }

            // Wait for the slot within the same deadline - a normal call takes
            // milliseconds; refusing it would fail good reads.
            int deadline = Environment.TickCount + timeoutMs;
            while (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            {
                if (Environment.TickCount - deadline >= 0) return false;
                Thread.Sleep(1);
            }

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

