using System;
using System.Linq;
using Kaban.Core.Interfaces;
using ActUtlTypeLib;

namespace Kaban.Drivers.MitsubishiActUtl // <-- Updated Namespace
{
    public class MitsubishiActUtl : IPlcDriver, Kaban.Core.Interfaces.INonBlockingDriver, Kaban.Core.Interfaces.IPlcIdentity,
                                    Kaban.Core.Interfaces.IPlcBlockAccess, IDisposable
    {
        // The actual MX Component Object
        private ActUtlType _plc;              // touched ONLY on _worker's thread

        /// <summary>MX Component's own thread - see StaWorker for why.</summary>
        private readonly StaWorker _worker;

        /// <summary>
        /// How long the UI waits for a Connect before carrying on. Short: a PLC
        /// that is there opens well inside it, and one that is not must not stall
        /// the screen. The attempt continues in the background either way.
        /// </summary>
        private const int ConnectWaitMs = 150;

        /// <summary>How long the UI waits for a read or write.</summary>
        private const int CallWaitMs = 1000;

        /// <summary>
        /// How long shutdown waits for the control to be closed and released - longer
        /// than a read: an Open() still running (a reconnect attempt) has to finish
        /// before its connection can be closed at all.
        /// </summary>
        public const int ShutdownWaitMs = 4000;

        private volatile bool _opened;     // Open() succeeded and Close() has not been called since
        private bool _disposed;
        private int _stationNumber;
        private volatile bool _isConnected = false;
        private volatile bool _needsFreshControl;
        private int _failCount;
        private DateTime _lastFailLog = DateTime.MinValue;
        private DateTime _firstFailAt;
        private bool _lastOpOk = true;

        // PACED RECONNECTS. After a failed Open the next one waits: half a second,
        // then 1, 2, 4 and at most 5 s. Every caller - the scanner, the connection
        // poll - asked on its own schedule, so a controller that refused was asked
        // again one to two times a second, each time with a brand-new control, for
        // as long as it refused (44 minutes once). Paced, a controller that is
        // back is still found within 5 s.
        private volatile bool _pacing;
        private int _nextAttemptTick;

        /// <summary>Close()'s answer when the connection was closed (Disconnect), for the exit log.</summary>
        private int? _closedRc;

        public MitsubishiActUtl(int stationNumber)
        {
            _stationNumber = stationNumber;

            // Created on its own thread and used only there, from the first moment.
            // Building it here on the UI thread and moving it later would leave the
            // object tied to the wrong apartment for its whole life.
            _worker = new StaWorker("MX Component station " + stationNumber);
            bool created;
            _worker.TryRun(() => { CreateInitialControl(); return true; }, 15000, out created);
        }

        private void CreateInitialControl()
        {

            // The COM object is created DEFENSIVELY. On a PC without MX Component
            // (or with it registered for a different bitness) this throws
            // REGDB_E_CLASSNOTREG 0x80040154, which previously escaped as an
            // UNHANDLED exception and killed the whole HMI at startup. A missing
            // driver must degrade to "not connected", never take the machine down.
            try
            {
                _plc = new ActUtlType();
                Kaban.Core.Helpers.AuditLogger.Log(
                    "[PLC DIAG] ActUtlType interop: " + typeof(ActUtlTypeLib.ActUtlType).Assembly.Location +
                    " | COM created OK | process " + (Environment.Is64BitProcess ? "64" : "32") + "-bit.");
            }
            catch (Exception ex)
            {
                _plc = null;
                LastConnectError = ex.Message;
                Kaban.Core.Helpers.AuditLogger.Log(
                    "[PLC DIAG] ActUtlType COM creation FAILED (" + (Environment.Is64BitProcess ? "64" : "32") +
                    "-bit process): " + ex.Message);
                MxDiagnostics.Report();     // says exactly WHERE the class is registered
            }
        }

        /// <summary>
        /// Releases the current control and creates a new one. Used before every
        /// reconnect attempt that follows a failure, because MX Component does not
        /// recover a control whose Open() has failed.
        /// </summary>
        private void RecreateControl()
        {
            try
            {
                if (_plc != null)
                {
                    try { _plc.Close(); } catch { /* was never open */ }
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(_plc);
                }
            }
            catch { }
            _plc = null;
            _opened = false;

            try { _plc = new ActUtlType(); }
            catch (Exception ex)
            {
                LastConnectError = ex.Message;
                Kaban.Core.Helpers.AuditLogger.Log("[PLC DIAG] could not recreate ActUtlType: " + ex.Message);
            }
        }

        // --- PUBLIC SURFACE: every call is carried to the MX thread ---------------
        //
        // Each one answers AT ONCE when there is nothing to wait for - no link, or
        // another call still in flight - and otherwise waits a bounded time. The
        // screen can be slowed by a failing controller for at most CallWaitMs, once;
        // it can no longer be frozen by one.

        public bool Connect()
        {
            // ALREADY OPEN: nothing to do. MX Component answers a second Open() on
            // an open control with 0xF0000003 ("already open"); this driver took
            // that for a spent control, reported the link down and rebuilt it -
            // closing the connection that was working. It happened on every start
            // (the startup check called Connect again right after the station had
            // opened) and whenever the scanner and the connection poll reconnected
            // at the same moment: a second connection, a station scan, a close and
            // a third Open within three seconds, for nothing.
            if (IsOpenAndWell) return true;
            if (_worker.IsWorkerThread) return ConnectDirect();
            if (!AttemptDue) return false;              // paced after a failure

            // CONNECT ALONE REFUSES WHEN BUSY. The call in flight is most likely
            // an Open() to a PLC that is not there, which can take seconds; waiting
            // behind it every poll is exactly the stall the worker exists to avoid.
            if (_worker.IsBusy) return _isConnected;

            bool ok;
            // Not finished in time? It carries on in the background and sets
            // _isConnected itself; the next poll sees the result.
            if (!_worker.TryRun(ConnectDirect, ConnectWaitMs, out ok)) return _isConnected;
            return ok;
        }

        public void Disconnect()
        {
            if (_worker.IsWorkerThread) { DisconnectDirect(); return; }
            bool ignored;
            _worker.TryRun(() => { DisconnectDirect(); return true; }, CallWaitMs, out ignored);
        }

        public bool TryRead(string address, out int value)
        {
            value = 0;
            if (_worker.IsWorkerThread) return TryReadDirect(address, out value);
            if (!_isConnected) return false;              // no link: answer at once

            ReadOne r;
            bool started;
            if (!_worker.TryRun(() =>
                {
                    int v;
                    bool ok = TryReadDirect(address, out v);
                    return new ReadOne { Ok = ok, Value = v };
                }, CallWaitMs, out r, out started))
            {
                if (started) Stalled("a read of " + address);    // it ran and did not come back: the link; never ran: only busy
                return false;
            }
            value = r.Value;
            return r.Ok;
        }

        public bool TryReadMulti(string[] addresses, out int[] values)
        {
            values = null;
            if (_worker.IsWorkerThread) return TryReadMultiDirect(addresses, out values);
            if (!_isConnected) return false;              // no link: answer at once

            // A MODULE'S BUFFER MEMORY IN A CALL OF ITS OWN (5 November, found on the panel: PLC 1 on GX Simulator3 "lost"
            // every minute or two on the Machine Parameters pages and with the machine details on - the axes' loads, U0\G2478 ...).
            // A connection that does not take them in a random read reads them one by one, and that took the whole call past
            // its second: the link was taken down for words the PLC's own devices did not need. Now the PLC's devices are one
            // call, judged as before; the module's words another, given longer and never taking the link down.
            if (addresses != null && addresses.Length > 1)
            {
                var plainIdx = new System.Collections.Generic.List<int>();
                var moduleIdx = new System.Collections.Generic.List<int>();
                for (int i = 0; i < addresses.Length; i++) (IsModule(addresses[i]) ? moduleIdx : plainIdx).Add(i);
                if (moduleIdx.Count > 0 && plainIdx.Count > 0)
                {
                    int[] a, b;
                    bool okA = TryReadMulti(plainIdx.Select(i => addresses[i]).ToArray(), out a);
                    values = new int[addresses.Length];
                    if (a != null) for (int k = 0; k < plainIdx.Count; k++) values[plainIdx[k]] = a[k];
                    if (!okA) return false;
                    if (ReadModuleWords(moduleIdx.Select(i => addresses[i]).ToArray(), out b))
                        for (int k = 0; k < moduleIdx.Count; k++) values[moduleIdx[k]] = b[k];
                    return true;
                }
            }

            ReadMany r;
            bool started;
            if (!_worker.TryRun(() =>
                {
                    int[] v;
                    bool ok = TryReadMultiDirect(addresses, out v);
                    return new ReadMany { Ok = ok, Values = v };
                }, CallWaitMs, out r, out started))
            {
                if (started) Stalled("a read of " + addresses.Length + " device(s)");   // it ran and did not come back: the link; never ran: only busy
                return false;
            }
            values = r.Values;
            return r.Ok;
        }

        public int Write(string address, int value)
        {
            if (_worker.IsWorkerThread) return WriteDirect(address, value);
            // A write to a dead link is refused immediately - this is the path a
            // button press takes, and it is the one that froze the panel. A write
            // to a LIVE link that finds a scan read in flight waits for it; that is
            // milliseconds, and refusing it cancelled good writes.
            if (!_isConnected) return -1;

            int rc;
            bool started;
            if (!_worker.TryRun(() => WriteDirect(address, value), CallWaitMs, out rc, out started))
            {
                if (started) Stalled("a write of " + address);
                return -1;
            }
            return rc;
        }

        /// <summary>
        /// A call that did not come back in time: treat the link as down.
        ///
        /// A controller that cannot answer within a second is not one the HMI can
        /// work with, whatever the socket believes. The next Connect runs on the
        /// worker once the stuck call has finished, so the fresh control is built
        /// - and the old one released - when nothing is using it.
        /// </summary>
        private void Stalled(string what = "a call")
        {
            Kaban.Core.Helpers.AuditLogger.Log($"[PLC DIAG] Mitsubishi station {_stationNumber}: {what} did not come back in {CallWaitMs} ms - the connection is opened again.");
            _isConnected = false;
            _needsFreshControl = true;
            _lastOpOk = false;
        }

        private struct ReadOne { public bool Ok; public int Value; }
        private struct ReadMany { public bool Ok; public int[] Values; }

        // --- A RUN OF DEVICES IN ONE CALL (22 October: the PLC Connection page's Read and Write) ---
        //
        // ReadDeviceBlock2 / WriteDeviceBlock2: words (a bit device 16 to a word). The backup asks
        // for at most 960 words a call (an MC protocol frame's batch), so each call is milliseconds
        // and the scanner's reads, which wait their turn on the same thread, are not held up for long.
        // 23 October: and they go FIRST - a block call waits while any other call is waiting for the
        // thread (StaWorker.TryRunPolite). Back to back, the backup's calls kept the thread from the
        // scanner and the connection beat until they gave up and took the link down; and the CPU's
        // sizes are now found with block calls too, not GetDevice - a refused device no longer counts
        // against the link's health (IsHealthy).

        /// <summary>How long a block call waits for its turn and its answer.</summary>
        private const int BlockWaitMs = 5000;

        /// <summary>The CPU refuses a device beyond its parameters' range: the backup asks it for each device's size.</summary>
        public bool RefusesOutOfRange { get { return true; } }

        public bool TryReadBlock(string first, int count, out short[] values)
        {
            values = null;
            if (count <= 0 || string.IsNullOrWhiteSpace(first)) { values = new short[0]; return count == 0; }
            if (!_isConnected) return false;
            // A 32-BIT DEVICE (LCN, LZ, LTN, LSTN) DEVICE BY DEVICE (23 October): ReadDeviceBlock2 counts them as
            // devices, not words - on GX Simulator3 the CPU's LCN0 - LCN63 were sized one short and refused as
            // an area (126 "words" taken for 126 counters), and a call that answered would have written twice the
            // words asked into the buffer. The random read gives each one's 32 bits.
            if (Kaban.Core.Helpers.PlcAddressHelper.IsDoubleWordDevice(first))
                return Kaban.Core.Helpers.PlcBlocks.ReadByDevice(this, first, count, out values);
            var buffer = new short[count];
            int rc;
            Func<int> call = () =>
            {
                if (!_isConnected || _plc == null) return -1;
                try { return _plc.ReadDeviceBlock2(first, count, out buffer[0]); }
                catch { return -1; }
            };
            bool started;
            if (_worker.IsWorkerThread) rc = call();
            else if (!_worker.TryRunPolite(call, BlockWaitMs, out rc, out started))
            {
                // never had its turn (the screen's reads went first): busy, not dead
                if (started) Stalled();
                return false;
            }
            if (rc != 0) return false;
            values = buffer;
            return true;
        }

        public int WriteBlock(string first, short[] values)
        {
            if (values == null || values.Length == 0) return 0;
            if (!_isConnected) return -1;
            // a 32-bit device one by one (SetDevice takes its 32 bits) - see TryReadBlock
            if (Kaban.Core.Helpers.PlcAddressHelper.IsDoubleWordDevice(first))
                return Kaban.Core.Helpers.PlcBlocks.WriteByDevice(this, first, values);
            var buffer = (short[])values.Clone();
            int rc;
            Func<int> call = () =>
            {
                if (!_isConnected || _plc == null) return -1;
                try { return _plc.WriteDeviceBlock2(first, buffer.Length, ref buffer[0]); }
                catch { return -1; }
            };
            if (_worker.IsWorkerThread) return call();
            bool started;
            if (!_worker.TryRunPolite(call, BlockWaitMs, out rc, out started)) { if (started) Stalled(); return -1; }
            return rc;
        }

        // --- INTERFACE PROPERTIES ---
        public bool IsConnected => _isConnected;

        /// <summary>
        /// The MX Component logical station this driver opens - for the PLC
        /// Diagnostics page, which must not open a second control on a station
        /// the live connection is using.
        /// </summary>
        public int LogicalStation => _stationNumber;
        public bool IsHealthy => _isConnected && _lastOpOk;

        /// <summary>
        /// Last connection failure, e.g. "0xF0000003" (logical station not defined
        /// in MX Component's Communication Setup Utility, or the target/simulator
        /// not running yet). Kept for diagnostics instead of throwing.
        /// </summary>
        public string LastConnectError { get; private set; } = "";

        /// <summary>Open, not stalled, not waiting to be rebuilt: Connect has nothing to do.</summary>
        private bool IsOpenAndWell { get { return _isConnected && _opened && !_needsFreshControl; } }

        /// <summary>No failure being paced, or its pause is over.</summary>
        private bool AttemptDue { get { return !_pacing || unchecked(Environment.TickCount - _nextAttemptTick) >= 0; } }

        /// <summary>The pause after the n-th failure in a row: 0.5, 1, 2, 4, then 5 s.</summary>
        internal static int RetryDelayMs(int failures)
        {
            if (failures <= 1) return 500;
            if (failures >= 5) return 5000;
            return 500 << (failures - 1);
        }

        private bool ConnectDirect()
        {
            // Two callers can queue a connect at the same moment (the scanner's
            // thread and the connection poll): the second finds it open.
            if (IsOpenAndWell) return true;
            if (!AttemptDue) return false;

            // CONTRACT: IPlcDriver.Connect() reports failure by returning false -
            // it must NOT throw. The previous version threw, which surfaced as a
            // "Startup Error" popup whenever the PLC/simulator was not reachable at
            // launch (e.g. MX Component code 0xF0000003). A failed connection is a
            // NORMAL state at startup: the scanner shows comm-lost and retries every
            // ~2 seconds, so the HMI comes alive the moment the PLC does.
            // THE FIX (proved by the on-site station scan): a FRESH ActUtlType opens
            // logical station 1 without trouble, while the control that already
            // failed once keeps returning 0xF0000003 for ever. MX Component leaves
            // the control unusable after a failed Open, so every attempt now starts
            // from a brand-new control instead of reusing the broken one.
            if (_plc == null || _needsFreshControl)
            {
                RecreateControl();
                _needsFreshControl = false;
            }
            if (_plc == null)
            {
                LastConnectError = "MX Component ActUtlType is not available in this process.";
                return false;                      // stays disconnected; scanner retries
            }

            try
            {
                _plc.ActLogicalStationNumber = _stationNumber;
                int returnCode = _plc.Open();

                if (returnCode == 0)
                {
                    _opened = true;
                    _isConnected = true;
                    _lastOpOk = true;
                    LastConnectError = "";
                    if (_failCount > 0)
                        Kaban.Core.Helpers.AuditLogger.Log(
                            $"[PLC DIAG] Mitsubishi station {_stationNumber} opened after {_failCount} failed attempt(s) " +
                            $"in {(int)(DateTime.Now - _firstFailAt).TotalSeconds} s.");
                    _failCount = 0;
                    _pacing = false;
                    ReadCpuType();
                    return true;
                }

                _isConnected = false;
                _needsFreshControl = true;         // this control is spent - rebuild it next time
                LastConnectError = $"0x{returnCode:X8}";
                if (_failCount == 0) _firstFailAt = DateTime.Now;
                _pacing = true;
                _nextAttemptTick = unchecked(Environment.TickCount + RetryDelayMs(_failCount + 1));
                // Log the first failure in full, then at most once a minute - the
                // 1 Hz retry was burying everything else in the audit log.
                if (_failCount++ == 0 || (DateTime.Now - _lastFailLog).TotalSeconds >= 60)
                {
                    _lastFailLog = DateTime.Now;
                    Kaban.Core.Helpers.AuditLogger.Log(
                        $"[PLC DIAG] Mitsubishi station {_stationNumber} connect failed with {LastConnectError} " +
                        $"({MxDiagnostics.DescribeOpenError(returnCode)}; process is {(Environment.Is64BitProcess ? "64" : "32")}-bit)" +
                        (_failCount > 1 ? $" [repeated {_failCount} times]" : ""));
                }
                if (_failCount == 1)
                {
                    MxDiagnostics.ReportOnce();    // registry facts, once
                    MxDiagnostics.AutoScanOnce(_stationNumber);   // find a station that DOES answer
                }
                return false;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                LastConnectError = ex.Message;
                Kaban.Core.Helpers.AuditLogger.Log(
                    $"[PLC DIAG] Mitsubishi station {_stationNumber} connect exception: {ex.Message} (process is {(Environment.Is64BitProcess ? "64" : "32")}-bit).");
                return false;
            }
        }

        /// <summary>The CPU's model, as MX Component reports it once the station is open ("FX5U-64MT/ES").</summary>
        public string Identity { get { return _cpuName; } }
        private volatile string _cpuName = "";

        /// <summary>
        /// Asks the open station what CPU it is (ActUtlType.GetCpuType) - on this
        /// worker thread, right after Open. Called by name: an MX Component
        /// installation whose interop lacks it simply leaves the model unknown.
        /// </summary>
        private void ReadCpuType()
        {
            try
            {
                var method = _plc.GetType().GetMethod("GetCpuType");
                if (method == null) return;
                var args = new object[] { null, 0 };
                object rc = method.Invoke(_plc, args);
                if (rc is int && (int)rc == 0 && args[0] is string)
                {
                    var name = ((string)args[0]).Trim().TrimEnd('\0').Trim();
                    if (name.Length > 0 && name != _cpuName)
                    {
                        _cpuName = name;
                        Kaban.Core.Helpers.AuditLogger.Log($"[PLC DIAG] Mitsubishi station {_stationNumber}: CPU {name} (code 0x{Convert.ToInt32(args[1]):X}).");
                    }
                }
            }
            catch { /* the model is a courtesy: the connection is what matters */ }
        }

        private void DisconnectDirect()
        {
            bool wasOpen = _opened;
            int rc = 0;
            try
            {
                if (_plc != null) rc = _plc.Close();
            }
            catch { rc = -1; } // Ignore close errors
            finally
            {
                // Kept for the exit log: Disconnect runs before Dispose, so the
                // log only ever said "control released" and never whether the
                // connection had been closed.
                if (wasOpen) _closedRc = rc;
                _opened = false;
                _isConnected = false;
            }
        }

        /// <summary>
        /// SHUTDOWN, PROPERLY: the connection closed, the control RELEASED and its
        /// thread ended - before the program exits.
        ///
        /// Closing was all the program did, and only if the MX thread was free
        /// within a second; the control itself was never released and its thread
        /// was simply killed with the process. With GX Works' simulator that is
        /// what left the next start unable to connect until the simulator itself
        /// was restarted: MX Component was never told the program had gone. Now an
        /// Open() still running is waited for (a reconnect attempt), the connection
        /// is closed, the control released on its own thread, and the thread's
        /// message loop ended so COM is shut down for it.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            bool done;
            if (_worker.IsWorkerThread) { ShutdownDirect(); done = true; }
            else if (!_worker.TryRun(() => { ShutdownDirect(); return true; }, ShutdownWaitMs, out done)) done = false;
            if (!done)
                Kaban.Core.Helpers.AuditLogger.Log(
                    $"[PLC DIAG] Mitsubishi station {_stationNumber}: a call to the controller was still running after " +
                    $"{ShutdownWaitMs / 1000} s - the connection could not be closed before exit.");
            _worker.Stop(1000);
        }

        private void ShutdownDirect()
        {
            var plc = _plc;
            _plc = null;
            bool wasOpen = _opened;
            _opened = false;
            _isConnected = false;
            if (plc == null) return;
            int rc = 0;
            try { if (wasOpen) rc = plc.Close(); } catch (Exception ex) { rc = -1; LastConnectError = ex.Message; }
            try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(plc); } catch { }
            int? closed = wasOpen ? rc : _closedRc;
            _closedRc = null;
            Kaban.Core.Helpers.AuditLogger.Log(
                $"[PLC DIAG] Mitsubishi station {_stationNumber}: " +
                (closed.HasValue ? $"connection closed (Close returned 0x{closed.Value:X8}) and " : "not connected; ") +
                "MX Component control released.");
        }

        public int Read(string address)
        {
            int value;
            TryRead(address, out value);
            return value; // preserved for backward compatibility - returns 0 on failure, same as before.
        }

        /// <summary>Real fix for issue: read failures used to be indistinguishable from a genuine 0.</summary>
        private bool TryReadDirect(string address, out int value)
        {
            value = 0;

            if (!_isConnected)
            {
                _lastOpOk = false;
                return false;
            }

            int result = _plc.GetDevice(address, out value);

            if (result != 0)
            {
                // Communication/read failure - do NOT pretend this was a real 0.
                value = 0;
                _lastOpOk = false;
                return false;
            }

            _lastOpOk = true;
            return true;
        }

        public int[] ReadMulti(string[] addresses)
        {
            int[] values;
            TryReadMulti(addresses, out values);
            return values; // preserved for backward compatibility.
        }

        private bool TryReadMultiDirect(string[] addresses, out int[] values)
        {
            if (addresses == null) { values = new int[0]; return true; }
            values = new int[addresses.Length];
            if (addresses.Length == 0) return true;
            if (!_isConnected) { _lastOpOk = false; return false; }

            // A MODULE'S BUFFER MEMORY (U1\G2406 - the simple motion values on Axis
            // Settings) is read in a batch of its own: should this connection not
            // take it in a random read, only those words fall back to one call
            // each - the page's other devices keep their single round trip.
            int modules = 0;
            for (int i = 0; i < addresses.Length; i++) if (IsModule(addresses[i])) modules++;
            if (modules > 0 && modules < addresses.Length)
            {
                var plainIdx = new System.Collections.Generic.List<int>();
                var moduleIdx = new System.Collections.Generic.List<int>();
                for (int i = 0; i < addresses.Length; i++) (IsModule(addresses[i]) ? moduleIdx : plainIdx).Add(i);
                int[] a, b;
                bool okA = TryReadMultiDirect(plainIdx.Select(i => addresses[i]).ToArray(), out a);
                bool okB = TryReadMultiDirect(moduleIdx.Select(i => addresses[i]).ToArray(), out b);
                for (int k = 0; k < plainIdx.Count; k++) values[plainIdx[k]] = a[k];
                for (int k = 0; k < moduleIdx.Count; k++) values[moduleIdx[k]] = b[k];
                _lastOpOk = okA;                  // the connection is judged by the PLC's own devices
                return okA;
            }

            // ONE COM CALL PER ADDRESS WAS THE PROBLEM.
            // With the real alarm list the batch grew to ~170 devices; at the ~15 ms
            // round trip this PLC reports that is over two seconds per scan, which
            // starved the whole HMI. ReadDeviceRandom fetches the entire list in a
            // SINGLE call instead, so a scan costs one round trip rather than 170.
            // THE DEVICES A RANDOM READ REFUSES (5 November, found on the panel: "a read of 553 device(s) did not come back in
            // 1000 ms" on GX Simulator3, every minute or two). One device the random read refuses failed the whole list, and
            // the fallback read all 553 one by one - past the second, the link taken down. The refused ones are found once
            // (the list halved until they are), said in the log, kept for this connection's life, and read one by one; the
            // rest stay ONE call.
            var refused = new System.Collections.Generic.List<int>();
            var asked = new System.Collections.Generic.List<int>();
            for (int i = 0; i < addresses.Length; i++) (_randomRefused.Contains(addresses[i]) ? refused : asked).Add(i);
            bool allOk = true;
            if (asked.Count > 0)
            {
                int[] got;
                int rc = RandomRead(asked.Select(i => addresses[i]).ToArray(), out got);
                if (rc == 0)
                    for (int k = 0; k < asked.Count; k++) values[asked[k]] = got[k];
                else
                {
                    var found = new System.Collections.Generic.List<string>();
                    FindRefused(asked.Select(i => addresses[i]).ToList(), found, 0);
                    foreach (var a in found) _randomRefused.Add(a);
                    Kaban.Core.Helpers.AuditLogger.Log($"[PLC DIAG] Mitsubishi station {_stationNumber}: a random read of {asked.Count} device(s) was refused " +
                        $"(0x{rc:X8}); " + (found.Count == 0 ? "no single device found - read one by one this time." :
                        $"refused in it: {string.Join(", ", found.Take(20))}{(found.Count > 20 ? " ..." : "")} - read one by one from now on, the rest in one call."));
                    if (found.Count == 0)
                    {
                        foreach (int i in asked) { int v; allOk &= TryRead(addresses[i], out v); values[i] = v; }
                        return allOk;
                    }
                    var rest = asked.Where(i => !_randomRefused.Contains(addresses[i])).ToList();
                    refused.AddRange(asked.Where(i => _randomRefused.Contains(addresses[i])));
                    if (rest.Count > 0)
                    {
                        if (RandomRead(rest.Select(i => addresses[i]).ToArray(), out got) == 0)
                            for (int k = 0; k < rest.Count; k++) values[rest[k]] = got[k];
                        else foreach (int i in rest) { int v; allOk &= TryRead(addresses[i], out v); values[i] = v; }
                    }
                }
            }
            // the refused ones: one by one (the PLC's answer for each - a device it does not have stays a failed read)
            foreach (int i in refused) { int v; TryRead(addresses[i], out v); values[i] = v; }
            _lastOpOk = allOk;
            return allOk;
        }

        /// <summary>The devices a random read refuses on this connection (found once, kept).</summary>
        private readonly System.Collections.Generic.HashSet<string> _randomRefused =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>One random read: 0, or MX Component's code (-1 when it threw).</summary>
        private int RandomRead(string[] list, out int[] values)
        {
            values = new int[list.Length];
            if (list.Length == 0) return 0;
            try
            {
                var buffer = new int[list.Length];
                int rc = _plc.ReadDeviceRandom(string.Join("\n", list), list.Length, out buffer[0]);
                if (rc == 0) values = buffer;
                return rc;
            }
            catch { return -1; }
        }

        /// <summary>The list halved until the devices it refuses are found (a few calls, once).</summary>
        private void FindRefused(System.Collections.Generic.List<string> list, System.Collections.Generic.List<string> found, int depth)
        {
            if (list.Count == 0 || depth > 12 || found.Count > 50) return;
            int[] v;
            if (RandomRead(list.ToArray(), out v) == 0) return;
            if (list.Count == 1) { found.Add(list[0]); return; }
            int half = list.Count / 2;
            FindRefused(list.GetRange(0, half), found, depth + 1);
            FindRefused(list.GetRange(half, list.Count - half), found, depth + 1);
        }

        /// <summary>How long a module's words may take (read one by one where the random read refuses them).</summary>
        private const int ModuleWaitMs = 5000;
        /// <summary>Until when a module's words are not asked again (they did not come back in time).</summary>
        private int _modulesRestUntil;

        /// <summary>
        /// A module's buffer words, in a call of their own: slow or refused, they are only missing (zeros, false) - the link
        /// is judged by the PLC's own devices. One that did not come back in time rests 30 s, so the scans do not queue behind it.
        /// </summary>
        private bool ReadModuleWords(string[] addresses, out int[] values)
        {
            values = new int[addresses.Length];
            if (_modulesRestUntil != 0 && Environment.TickCount - _modulesRestUntil < 0) return false;
            _modulesRestUntil = 0;
            ReadMany r;
            bool started;
            int t0 = Environment.TickCount;
            if (!_worker.TryRun(() =>
                {
                    int[] v;
                    bool ok = TryReadMultiDirect(addresses, out v);
                    return new ReadMany { Ok = ok, Values = v };
                }, ModuleWaitMs, out r, out started))
            {
                if (started)
                {
                    _modulesRestUntil = Environment.TickCount + 30000;
                    Kaban.Core.Helpers.AuditLogger.Log($"[PLC DIAG] Mitsubishi station {_stationNumber}: {addresses.Length} word(s) of a module's " +
                        $"buffer ({addresses[0]} ...) did not come back in {ModuleWaitMs / 1000} s - not asked again for 30 s; the connection kept.");
                }
                return false;
            }
            int took = Environment.TickCount - t0;
            if (took > CallWaitMs && !_slowModulesSaid)
            {
                _slowModulesSaid = true;
                Kaban.Core.Helpers.AuditLogger.Log($"[PLC DIAG] Mitsubishi station {_stationNumber}: {addresses.Length} word(s) of a module's buffer " +
                    $"({addresses[0]} ...) took {took} ms - read in a call of their own, so the connection is not judged by them.");
            }
            if (r.Values != null) values = r.Values;
            return r.Ok;
        }
        private bool _slowModulesSaid;

        private static bool IsModule(string address)
        {
            return address != null && address.Length > 3 && (address[0] == 'U' || address[0] == 'u') && address.IndexOf("\\G", StringComparison.OrdinalIgnoreCase) > 0;
        }

        private int WriteDirect(string address, int value)
        {
            if (!_isConnected)
            {
                _lastOpOk = false;
                return -1;
            }

            // SetDevice returns 0 on success
            int result = _plc.SetDevice(address, value);
            _lastOpOk = (result == 0);
            return result;
        }
    }
}

