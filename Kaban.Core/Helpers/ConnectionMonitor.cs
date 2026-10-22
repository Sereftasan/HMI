using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Interfaces;
using System.Threading;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// Whether each controller is actually answering, right now.
    ///
    /// WHY THIS EXISTS
    /// ---------------
    /// A driver's IsConnected tells you whether a socket is open, which is not the
    /// same question. A cable pulled out of a switch leaves the socket believing it
    /// is fine for as long as the operating system takes to notice - often tens of
    /// seconds - and during that time the HMI keeps showing the last values it
    /// read. An axis position frozen at 412.50 mm looks exactly like an axis that
    /// is not moving, and somebody presses a jog button.
    ///
    /// So the scanner reports the OUTCOME of every read to this class, and a
    /// station is "live" only while reads are actually succeeding. Nothing else
    /// decides it - not a flag, not a connect() that returned true once.
    ///
    /// The stale marking matters as much as the alarm: a value that has not been
    /// refreshed is not a value, and the controls showing it say so.
    /// </summary>
    public static class ConnectionMonitor
    {
        /// <summary>
        /// How long a station may go without a successful read before it counts as
        /// down. Three scan cycles: one missed read is a hiccup, three in a row is
        /// a fault - and waiting for three keeps a busy PLC from flickering the
        /// warning on and off in front of the operator.
        /// </summary>
        public static TimeSpan GraceTime = TimeSpan.FromSeconds(3);

        public class StationState
        {
            public int StationId;
            public string Name = "";
            public string ModuleId = "";
            public int LocalNumber;

            /// <summary>
            /// Guards the timestamps and ReportedDown. ReportRead is called from the
            /// scanner's pool thread AND from Beat() on the UI thread, and a DateTime
            /// is 64 bits: in this 32-bit process it can be read half-written.
            /// </summary>
            internal readonly object Sync = new object();

            // UTC, NOT LOCAL TIME. When the clocks go back an hour in autumn,
            // "now - last good read" computed in local time comes out an hour
            // SHORT, so a station that had just died still counted as live for up
            // to an hour - controls enabled, frozen values shown as current. UTC
            // never jumps. (Nothing displays these as a clock time; only the
            // differences are used.)
            public DateTime LastGoodRead = DateTime.MinValue;
            public DateTime LastAttempt = DateTime.MinValue;

            /// <summary>Last failure text from the driver, e.g. "not connected".</summary>
            public string LastError = "";

            /// <summary>Code shown in the warning list and used to find help.</summary>
            public string ErrorCode = "";

            /// <summary>Program version read from the station's version device.</summary>
            public string Version = "";

            public bool EverConnected { get { return LastGoodRead != DateTime.MinValue; } }

            /// <summary>The known-down state last announced through ConnectionChanged.</summary>
            public bool ReportedDown;

            /// <summary>
            /// True while reads are succeeding. A station that has NEVER answered is
            /// down, not unknown: on a machine, "we have not heard from it" and "it
            /// is not there" call for the same response.
            /// </summary>
            public bool IsLive
            {
                get { lock (Sync) return DateTime.UtcNow - LastGoodRead <= GraceTime; }
            }

            /// <summary>Tried at least once (read under the lock, see Sync).</summary>
            internal bool Attempted
            {
                get { lock (Sync) return LastAttempt != DateTime.MinValue; }
            }

            /// <summary>How long the values on screen have been standing still.</summary>
            public TimeSpan StaleFor
            {
                get
                {
                    lock (Sync)
                        return LastGoodRead == DateTime.MinValue
                            ? TimeSpan.MaxValue : DateTime.UtcNow - LastGoodRead;
                }
            }
        }

        private static readonly Dictionary<int, StationState> _states =
            new Dictionary<int, StationState>();

        /// <summary>Raised when a station goes down or comes back.</summary>
        public static event EventHandler ConnectionChanged;

        public static IEnumerable<StationState> Stations
        {
            get { lock (_states) return _states.Values.ToList(); }
        }

        public static StationState Get(int stationId)
        {
            lock (_states)
            {
                StationState s;
                if (!_states.TryGetValue(stationId, out s))
                {
                    var info = PlcManager.Instance.GetStation(stationId);
                    s = new StationState
                    {
                        StationId = stationId,
                        Name = info != null ? info.Name : "PLC " + stationId,
                        ModuleId = info != null ? info.OwnerModuleId : "",
                        LocalNumber = info != null ? info.LocalNumber : 0
                    };
                    _states[stationId] = s;
                }
                return s;
            }
        }

/// <summary>
        /// Polls every station on its own timer, whatever page is open.
        ///
        /// THIS IS WHY EVERYTHING READ "disconnected". The monitor was fed only by
        /// the page scanner, and the scanner reads the addresses the OPEN PAGE
        /// binds. Sitting on PLC Connection - which binds none - nothing was
        /// ever reported, so every station looked dead including the simulations
        /// that were plainly running. A connection state that depends on which page
        /// you are looking at is not a connection state.
        ///
        /// It also gives reconnection somewhere to happen: a station that dropped
        /// while nobody was on a machine page now comes back by itself instead of
        /// waiting for someone to navigate.
        /// </summary>
        public static void Start(Func<int, IPlcDriver> driverFor, IEnumerable<int> stations)
        {
            if (_started) return;
            _started = true;
            _driverFor = driverFor;
            _stationsFor = stations;

            // THE GRACE MUST OUTLAST THE BEAT.
            //
            // A station is judged down when it has not answered within GraceTime.
            // If that is shorter than the interval between beats, a perfectly
            // healthy controller falls "down" in the gap and comes back on the next
            // beat - the warning flickers and the operator learns to ignore it.
            // Three beats, so one missed answer is still a hiccup rather than a
            // fault, and the two settings cannot be configured into conflict.
            var minimum = TimeSpan.FromMilliseconds(BeatMs * 3);
            if (GraceTime < minimum) GraceTime = minimum;

            // NO TIMER OF ITS OWN. The first version polled from a thread-pool
            // timer, which called the MX Component COM object from a thread that
            // does not own it - one of the two routes to RaceOnRCWCleanup. The UI
            // layer now drives Beat() from a Forms timer, and the Mitsubishi driver
            // carries each call to its own thread, so no COM object is ever touched
            // from the pool.
        }

        /// <summary>How often each station is polled, in milliseconds.</summary>
        public const int BeatMs = 1000;

        private static bool _started;
        private static bool _beating;
        private static Func<int, IPlcDriver> _driverFor;
        private static IEnumerable<int> _stationsFor;

        /// <summary>
        /// One poll of every station. Driven by the UI's timer.
        ///
        /// Re-entry is refused: a slow station must not let the next tick start a
        /// second poll over the top of the first.
        /// </summary>
        public static void Beat()
        {
            if (_driverFor == null || _stationsFor == null) return;
            if (_beating) return;
            _beating = true;
            try { BeatOnce(); }
            finally { _beating = false; }
        }

        private static void BeatOnce()
        {

            foreach (var id in _stationsFor.ToList())
            {
                // a PLC not active (HMI Settings > PLC Connection): not watched - it is not there
                if (PlcConnectionSettings.RunsInactive(id)) continue;
                IPlcDriver drv = null;
                try { drv = _driverFor(id); } catch { }

                if (drv == null) { ReportRead(id, false, "no driver registered"); continue; }

                // RECONNECT FIRST, then prove it with a read. Connect() returning
                // true only means the attempt did not throw; a station is up when
                // it has answered something.
                if (!drv.IsConnected)
                {
                    try { drv.Connect(); } catch { }
                    if (!drv.IsConnected) { ReportRead(id, false, "not connected"); continue; }
                }

                // The version device when one is set, otherwise any cheap read. The
                // point is the round trip, not the value.
                var probe = GetVersionDevice(id);
                bool ok = false;
                int value = 0;
                try { ok = drv.TryRead(string.IsNullOrEmpty(probe) ? "D0" : probe, out value); }
                catch { ok = false; }

                ReportRead(id, ok, ok ? "" : "read failed");
                if (ok && !string.IsNullOrEmpty(probe)) ReportVersion(id, value);
            }
        }

        /// <summary>The scanner calls this after every read attempt.</summary>
        public static void ReportRead(int stationId, bool succeeded, string error = "")
        {
            var s = Get(stationId);
            bool changed, down;
            string lastError;
            lock (s.Sync)
            {
                var now = DateTime.UtcNow;
                s.LastAttempt = now;
                if (succeeded)
                {
                    s.LastGoodRead = now;
                    s.LastError = "";
                    s.ErrorCode = "";
                }
                else
                {
                    s.LastError = error ?? "";
                    s.ErrorCode = CodeFor(error);
                }

                down = !(now - s.LastGoodRead <= GraceTime);
                changed = down != s.ReportedDown;
                if (changed) s.ReportedDown = down;
                lastError = s.LastError;
            }

            // COMPARED WITH WHAT WAS LAST ANNOUNCED, not with the moment before.
            // Going down happens by TIME passing between reports - a station falls
            // out of its grace period while nothing is being reported - so a
            // before/after comparison inside one report never saw it: the event
            // fired for recoveries only, and anything listening for a loss waited
            // for ever.
            //
            // Decided under the lock (so the scanner thread and the UI beat cannot
            // both announce the same change), announced outside it (so a handler
            // can never deadlock against a reader).
            if (changed)
            {
                AuditLogger.Log("station " + stationId +
                                (down ? " lost: " + lastError : " reconnected."));
                var handler = ConnectionChanged;
                if (handler != null) handler(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Records the program version read from a station's version device.
        ///
        /// The PLC holds it as a plain integer - 102 for 1.02 - because a word is
        /// all a ladder can easily keep. The dot goes in here rather than in the
        /// controller, so a machine builder does not have to encode formatting into
        /// the program.
        /// </summary>
        public static void ReportVersion(int stationId, int raw)
        {
            Get(stationId).Version = raw <= 0
                ? "" : (raw / 100).ToString() + "." + (raw % 100).ToString("00");
        }

        /// <summary>Device each station reports its version in, if any.</summary>
        private static readonly Dictionary<int, string> _versionDevices =
            new Dictionary<int, string>();

        public static void SetVersionDevice(int stationId, string address)
        {
            lock (_versionDevices)
            {
                if (string.IsNullOrWhiteSpace(address)) _versionDevices.Remove(stationId);
                else _versionDevices[stationId] = address.Trim();
            }
        }

        public static string GetVersionDevice(int stationId)
        {
            lock (_versionDevices)
            {
                string a;
                return _versionDevices.TryGetValue(stationId, out a) ? a : "";
            }
        }

        /// <summary>Stations that are not answering.</summary>
        public static IEnumerable<StationState> Down
        {
            // Only stations that have been TRIED. One never polled is not a failure
            // - listing it as one showed "Connection failed" with no code for
            // stations nobody had asked.
            get { return Stations.Where(s => s.Attempted && !s.IsLive)
                                 .OrderBy(s => s.StationId); }
        }

        public static bool AnyDown { get { return Down.Any(); } }

        /// <summary>
        /// True when a station has been TRIED and is not answering.
        ///
        /// Stricter than !IsLive on purpose: a station nobody has polled yet - the
        /// first second after start, or a build that never starts the watch - is
        /// not refused, because refusing on no evidence would lock controls that
        /// work perfectly well.
        /// </summary>
        public static bool IsKnownDown(int stationId)
        {
            if (stationId <= 0) return false;
            var s = Get(stationId);
            return s.Attempted && !s.IsLive;
        }

        /// <summary>The sentence a disabled control shows on hover.</summary>
        public static string ReasonFor(int stationId)
        {
            var s = Get(stationId);
            var text = LanguageManager.GetString("Sta_Failed", "Connection failed");
            if (!string.IsNullOrEmpty(s.ErrorCode))
                text += " (" + LanguageManager.GetString("Conn_ErrorCode", "Error Code") + ": " + s.ErrorCode + ")";
            var detail = DescribeCode(s.ErrorCode);
            return string.IsNullOrEmpty(detail) ? text : text + "\r\n" + detail;
        }

        /// <summary>
        /// Turns a driver message into a stable code, so the help system has
        /// something to key on and the operator has something to quote down the
        /// phone. Free text changes between driver versions; a code does not.
        ///
        ///   C01  nothing registered for that station
        ///   C02  the link is not open
        ///   C03  the link is open but reads fail - cable, switch, or a PLC
        ///        that has stopped answering
        ///   C04  the station answered with something unusable
        /// </summary>
        public static string CodeFor(string error)
        {
            if (string.IsNullOrEmpty(error)) return "";
            var e = error.ToLowerInvariant();
            if (e.Contains("no driver")) return "C01";
            if (e.Contains("not connected")) return "C02";
            if (e.Contains("read failed")) return "C03";
            return "C04";
        }

        public static string DescribeCode(string code)
        {
            switch (code)
            {
                case "C01": return LanguageManager.GetString("Conn_C01", "No driver configured for this station");
                case "C02": return LanguageManager.GetString("Conn_C02", "Link not open - check cable, power and address");
                case "C03": return LanguageManager.GetString("Conn_C03", "Link open but no reply - check network and PLC run state");
                case "C04": return LanguageManager.GetString("Conn_C04", "Unexpected reply from the controller");
                default:    return "";
            }
        }
    }
}

