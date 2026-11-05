using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kaban.Core.Helpers;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// WHY THE MACHINE STOOD STILL (5 November: "Yes, we could ask it to enter the reason" - the plan's "Downtime reason: the
    /// machine stops longer than a limit - the operator picks a reason on the screen", one of the HMI's records of a new machine).
    ///
    /// A STOP: the machine's PLC records its states (0 - 5, always logged: MachineLogger) - a stop starts when it leaves
    /// "Operation is active" (2) for another state, and ends when it is active again (or closed, 0). Its start and its end are the
    /// PLC's dates of those records (as the dashboard reads them).
    ///
    /// ASKED: once a stop has lasted the limit (5 minutes: the machine's Industry 4 Logging frame) the main window shows a window of
    /// its own (StopReasonHost, not modal: the pages go on working behind it) - "Why did the machine stop?", the reasons as large
    /// buttons. Answered during the stop, the reason waits for its end; answered after, it is written at once. "Later" hides it
    /// until the stop ends (then it is asked again), or - the stop over - writes it without a reason. A stop shorter than the limit
    /// is not recorded. A new stop while the last one still waits for its answer: the last one is written without a reason.
    ///
    /// RECORDED (MachineRecords, "Machine stop", code 1131) with the stop's start as its date: how long it lasted, the state it
    /// stood in, the reason and its kind (planned, setup, unplanned - availability in OEE), who answered.
    /// </summary>
    public static class StopReasons
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        public const double DefaultMinutes = 5;

        /// <summary>The kind of a stop for OEE: planned stops are not losses; setup and unplanned stops are availability losses.</summary>
        public const int Planned = 1, Setup = 2, Unplanned = 3;

        public sealed class Reason
        {
            public int Number, Category;
            public string Key = "", Text = "";
            public string Shown { get { return T(Key, Text); } }
        }

        /// <summary>The reasons, in the order the window shows them (their numbers are kept in the records: never renumbered).</summary>
        public static readonly IList<Reason> All = new List<Reason>
        {
            new Reason { Number = 1, Category = Planned, Key = "Stp_Break", Text = "Break / meal" },
            new Reason { Number = 2, Category = Planned, Key = "Stp_Maintenance", Text = "Planned maintenance / cleaning" },
            new Reason { Number = 3, Category = Setup, Key = "Stp_Setup", Text = "Setup / profile change" },
            new Reason { Number = 4, Category = Setup, Key = "Stp_Tool", Text = "Saw blade / tool change" },
            new Reason { Number = 5, Category = Unplanned, Key = "Stp_Material", Text = "Waiting for material" },
            new Reason { Number = 6, Category = Unplanned, Key = "Stp_Fault", Text = "Machine fault" },
            new Reason { Number = 7, Category = Unplanned, Key = "Stp_Operator", Text = "Waiting for the operator" },
            new Reason { Number = 8, Category = Unplanned, Key = "Stp_Quality", Text = "Quality check" },
            new Reason { Number = 9, Category = Unplanned, Key = "Stp_Other", Text = "Other" },
        };

        public static string CategoryText(int category)
        {
            switch (category)
            {
                case Planned: return T("Stp_Planned", "Planned stop");
                case Setup: return T("Stp_SetupKind", "Setup");
                case Unplanned: return T("Stp_Unplanned", "Unplanned stop");
                default: return "";
            }
        }

        /// <summary>One stop of a machine.</summary>
        public sealed class Stop
        {
            public string ModuleId = "";
            /// <summary>The state it stood in (the first after it left "active").</summary>
            public int State;
            /// <summary>The PLC's dates of its start and its end (null: it goes on).</summary>
            public DateTime Start;
            public DateTime? End;
            /// <summary>When the HMI heard of its start (the panel's clock), and how old it was then.</summary>
            public DateTime Heard;
            public double LateSeconds;
            /// <summary>Its window shown; hidden by "Later" until its end.</summary>
            public bool Asked, Later;
            public Reason Given;
            public string User = "";

            /// <summary>How long it has lasted, in minutes (to its end, or to now).</summary>
            public double Minutes(DateTime now)
            {
                if (End.HasValue) return Math.Max(0, (End.Value - Start).TotalMinutes);
                return Math.Max(0, ((now - Heard).TotalSeconds + LateSeconds) / 60.0);
            }
        }

        /// <summary>Raised to show (or refresh) a stop's window; <see cref="Closed"/> to close it - on any thread.</summary>
        public static event Action<Stop> Asking, Closed;

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, int> _state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Stop> _open = new Dictionary<string, Stop>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> _latest = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The panel's clock (tests set their own).</summary>
        public static Func<DateTime> Clock = () => DateTime.Now;

        /// <summary>Asked for this machine: the HMI's records on, its Machine stop record switched on.</summary>
        public static bool AsksFor(string moduleId) { return MachineRecords.Taken(moduleId, MachineRecords.MachineStop); }

        public static double LimitMinutes(string moduleId) { return MachineRecords.Limit(moduleId, "stop_minutes", DefaultMinutes); }

        /// <summary>The stop of a machine going on or waiting for its answer (null: none).</summary>
        public static Stop Of(string moduleId)
        {
            lock (Gate) { Stop s; return _open.TryGetValue(moduleId ?? "", out s) ? s : null; }
        }

        /// <summary>A state record of the machine's PLC written (MachineLogger, on its thread): a stop starts or ends.</summary>
        public static void StateLogged(string moduleId, int code, DateTime plcDate)
        {
            if (!StateMap.IsState(code)) return;
            var tell = new List<KeyValuePair<Action<Stop>, Stop>>();
            lock (Gate)
            {
                moduleId = moduleId ?? "";
                int was;
                bool known = _state.TryGetValue(moduleId, out was);
                _state[moduleId] = code;
                DateTime latest;
                if (!_latest.TryGetValue(moduleId, out latest) || plcDate > latest) _latest[moduleId] = plcDate;
                Stop open;
                _open.TryGetValue(moduleId, out open);
                bool asks = AsksFor(moduleId);
                if (open != null && !open.End.HasValue && (code == StateMap.Active || code == StateMap.Closed))
                {
                    // the stop ended
                    open.End = plcDate;
                    if (!asks || open.Minutes(Clock()) < LimitMinutes(moduleId))
                    {
                        _open.Remove(moduleId);
                        if (open.Asked) tell.Add(Pair(Closed, open));
                    }
                    else if (open.Given != null) { Write(open); _open.Remove(moduleId); tell.Add(Pair(Closed, open)); }
                    else { open.Asked = true; open.Later = false; tell.Add(Pair(Asking, open)); }
                }
                else if (known && was == StateMap.Active && code != StateMap.Active && code != StateMap.Closed && asks)
                {
                    // a new stop - the last one, still waiting for its answer, written without it
                    if (open != null) { Write(open); _open.Remove(moduleId); tell.Add(Pair(Closed, open)); }
                    var now = Clock();
                    _open[moduleId] = new Stop
                    {
                        ModuleId = moduleId, State = code, Start = plcDate, Heard = now,
                        LateSeconds = Math.Max(0, (_latest[moduleId] - plcDate).TotalSeconds)
                    };
                }
            }
            foreach (var t in tell) Raise(t.Key, t.Value);
        }

        private static KeyValuePair<Action<Stop>, Stop> Pair(Action<Stop> h, Stop s) { return new KeyValuePair<Action<Stop>, Stop>(h, s); }

        private static void Raise(Action<Stop> h, Stop s)
        {
            if (h == null) return;
            foreach (Action<Stop> each in h.GetInvocationList())
                try { each(s); } catch (Exception ex) { AuditLogger.Log("stop reasons: " + ex.Message); }
        }

        /// <summary>Now and then (the main window's clock): a stop that has lasted its limit is asked.</summary>
        public static void Check()
        {
            var ask = new List<Stop>();
            var now = Clock();
            lock (Gate)
                foreach (var s in _open.Values)
                    if (!s.Asked && !s.End.HasValue && AsksFor(s.ModuleId) && s.Minutes(now) >= LimitMinutes(s.ModuleId)) { s.Asked = true; ask.Add(s); }
            foreach (var s in ask) Raise(Asking, s);
        }

        /// <summary>The operator's answer: written at once when the stop is over, else at its end.</summary>
        public static void Answer(Stop s, Reason r)
        {
            if (s == null || r == null) return;
            bool close;
            lock (Gate)
            {
                s.Given = r;
                s.User = SecurityManager.CurrentUser ?? "";
                close = true;
                if (s.End.HasValue) { Write(s); _open.Remove(s.ModuleId); }
            }
            AuditLogger.Log("Machine stop (" + s.ModuleId + "): the reason given - " + r.Text + ".");
            if (close) Raise(Closed, s);
        }

        /// <summary>"Later": hidden until the stop ends; the stop over, written without a reason.</summary>
        public static void Later(Stop s)
        {
            if (s == null) return;
            lock (Gate)
            {
                if (s.End.HasValue) { Write(s); _open.Remove(s.ModuleId); }
                else s.Later = true;
            }
            Raise(Closed, s);
        }

        /// <summary>The stop into the machine's log database (its start as its date).</summary>
        private static void Write(Stop s)
        {
            if (!MachineRecords.Taken(s.ModuleId, MachineRecords.MachineStop)) return;
            var m = DashboardService.Find(s.ModuleId);
            string state = "";
            if (m != null)
            {
                var map = m.Map;
                state = map.Translate(map.RawDescription(s.State), StateMap.ColumnOf(LanguageManager.CurrentLanguage));
            }
            var details = new List<LogDatabase.Detail>
            {
                new LogDatabase.Detail(MachineRecords.DDurationMin, Math.Round(s.Minutes(Clock()), 1), null),
                new LogDatabase.Detail(MachineRecords.DState, s.State, state)
            };
            if (s.Given != null)
            {
                details.Add(new LogDatabase.Detail(MachineRecords.DReason, s.Given.Number, s.Given.Shown));
                details.Add(new LogDatabase.Detail(MachineRecords.DKind, s.Given.Category, CategoryText(s.Given.Category)));
                if (!string.IsNullOrEmpty(s.User)) details.Add(new LogDatabase.Detail(MachineRecords.DUser, null, s.User));
            }
            else details.Add(new LogDatabase.Detail(MachineRecords.DReason, 0, T("Stp_NotGiven", "Not given")));
            MachineRecords.AddDated(s.ModuleId, MachineRecords.MachineStop, s.Start, details);
        }

        /// <summary>Forgets every machine's state (tests).</summary>
        public static void ResetForTests()
        {
            lock (Gate) { _state.Clear(); _open.Clear(); _latest.Clear(); }
        }
    }
}
