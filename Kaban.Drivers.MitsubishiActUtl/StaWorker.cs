using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using System.Windows.Forms;

namespace Kaban.Drivers.MitsubishiActUtl
{
    /// <summary>
    /// A thread of MX Component's own: STA, with a message loop, and nothing else
    /// ever touches the COM object.
    ///
    /// WHAT WAS WRONG
    /// --------------
    /// ActUtlType is an apartment-threaded COM object. It was created on the UI
    /// thread and called from there, which caused two separate failures:
    ///
    ///  1. FREEZES. Open() and the device calls BLOCK until MX Component gives up
    ///     - seconds, with no PLC on the wire - and the UI thread blocked with
    ///     them. Clicking a control on a dead station stopped the whole panel.
    ///
    ///  2. RaceOnRCWCleanup. A blocking COM call pumps messages while it waits, so
    ///     a timer tick could run INSIDE Open() - and that tick's Connect() called
    ///     Marshal.ReleaseComObject on the very object Open() was still executing
    ///     on. The debugger reports it ("an RCW that is in use"); outside the
    ///     debugger the check is simply switched off, the corruption is not.
    ///     Adding a thread-pool heartbeat made it more frequent, but old builds
    ///     had it too.
    ///
    /// WHAT THIS DOES
    /// --------------
    /// The object is created on this thread and only ever called on this thread,
    /// so it is never released while in use - calls here run one after another,
    /// and nothing on this thread can interrupt one with another. It has a real
    /// message loop, because MX Component is an ActiveX control and should get
    /// the same environment it had on the UI thread.
    ///
    /// Every call from outside WAITS A BOUNDED TIME. A controller that does not
    /// answer costs the operator at most that long, once, instead of freezing
    /// the screen until MX Component's own timeout.
    /// </summary>
    internal sealed class StaWorker
    {
        private readonly Thread _thread;
        private Control _invoker;
        private int _busy;              // 1 while a call is queued or running

        public StaWorker(string name)
        {
            var ready = new ManualResetEvent(false);

            _thread = new Thread(() =>
            {
                // The marshalling window is created HERE, so BeginInvoke lands on
                // this thread and not on whichever thread built the driver.
                _invoker = new Control();
                var handle = _invoker.Handle;
                ready.Set();
                Application.Run();                  // this thread's own message loop
            });
            _thread.IsBackground = true;            // never holds the process open
            _thread.Name = name;
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();

            ready.WaitOne(10000);
        }

        /// <summary>True while a call is queued or running on the worker.</summary>
        public bool IsBusy { get { return Volatile.Read(ref _busy) != 0; } }

        /// <summary>True when the caller is already on the worker thread.</summary>
        public bool IsWorkerThread { get { return Thread.CurrentThread == _thread; } }

        /// <summary>
        /// Runs work on the worker and waits at most timeoutMs for it.
        ///
        /// If another call is in flight it waits its turn, within the same
        /// deadline. Returns false if the deadline passes - the work, if it was
        /// started, carries on in the background and its result is discarded; the
        /// caller has already moved on, which is the point. A caller that must
        /// never wait behind a slow call (Connect) checks IsBusy first.
        ///
        /// Called FROM the worker (a driver method calling another), the work runs
        /// inline - queueing onto yourself and waiting would deadlock.
        /// </summary>
        public bool TryRun<T>(Func<T> work, int timeoutMs, out T result)
        {
            result = default(T);

            if (IsWorkerThread) { result = work(); return true; }
            if (_invoker == null) return false;

            // WAIT FOR THE SLOT, within the same deadline, instead of refusing.
            //
            // Refusing whenever another call was in flight turned an ordinary
            // overlap into a failure: a scan read was running, the operator's click
            // read the limit tags, found the thread busy, and got "could not read a
            // live value - write cancelled" from a PLC that was answering
            // perfectly. A normal call takes milliseconds; waiting for it is right.
            // A call that is STUCK still costs at most timeoutMs, because the slot
            // wait and the call share one deadline.
            //
            // Sleep, not a wait handle: Sleep does not pump messages, so nothing
            // else can run on the UI thread while it waits here.
            var deadline = Environment.TickCount + timeoutMs;
            while (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            {
                if (Environment.TickCount - deadline >= 0) return false;
                Thread.Sleep(1);
            }

            // One kernel event per call, previously never disposed (left to the
            // finalizer - thousands of handles a minute at the scan rate). On a
            // timeout the worker still Set()s it after the caller has gone, so it
            // cannot be disposed straight after the wait: both sides hold a
            // reference and whichever finishes second disposes it.
            var done = new ManualResetEvent(false);
            int refs = 2;
            Action release = () => { if (Interlocked.Decrement(ref refs) == 0) done.Dispose(); };
            var box = new Box<T>();
            try
            {
                _invoker.BeginInvoke((MethodInvoker)(() =>
                {
                    try { box.Value = work(); box.Ok = true; }
                    catch (Exception ex) { box.Error = ex; }
                    finally
                    {
                        Volatile.Write(ref _busy, 0);
                        done.Set();
                        release();
                    }
                }));
            }
            catch
            {
                Volatile.Write(ref _busy, 0);
                done.Dispose();
                return false;
            }

            try
            {
                int remaining = Math.Max(0, deadline - Environment.TickCount);
                if (!WaitWithoutPumping(done, remaining)) return false;
            }
            finally { release(); }
            if (!box.Ok) return false;

            result = box.Value;
            return true;
        }

        /// <summary>
        /// Waits WITHOUT dispatching window messages.
        ///
        /// WaitOne on an STA thread keeps pumping messages while it waits - so a
        /// mouse click, a timer tick, a paint could all run INSIDE a PLC read. That
        /// re-entry is what put the operator's click in the middle of a scan: the
        /// click then found the MX thread busy and its read failed. The old direct
        /// COM calls never pumped, and code throughout the program was written
        /// assuming they did not.
        ///
        /// WaitForSingleObject blocks without pumping, restoring that. It is safe
        /// here because nothing on the MX thread ever calls back into the UI
        /// thread, and the wait is bounded - the screen pauses for at most the
        /// call's timeout, exactly as it did before.
        ///
        /// Outside Windows (the test machine) it falls back to WaitOne.
        /// </summary>
        private static bool WaitWithoutPumping(ManualResetEvent handle, int timeoutMs)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                return WaitForSingleObject(handle.SafeWaitHandle, (uint)Math.Max(0, timeoutMs)) == WAIT_OBJECT_0;
            return handle.WaitOne(timeoutMs);
        }

        private const uint WAIT_OBJECT_0 = 0;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(SafeWaitHandle handle, uint milliseconds);

        private sealed class Box<TValue>
        {
            public TValue Value;
            public bool Ok;
            public Exception Error;
        }
    }
}

