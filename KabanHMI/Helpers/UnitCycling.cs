using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// CYLINDER FW/BACK (17 October: "another function just like clutching, for the
    /// maintenance and admin users, for forward / backward sequential movement: a
    /// 'Cylinder FW/Back' button next to 'Safety Bypass' ... when it is TRUE, the
    /// activated cylinder and pneumatic positioning control change direction every 5
    /// seconds until the control buttons are clicked again or this button is turned
    /// FALSE; the clutching should work with it as well. The direction change only
    /// switches the output ON / OFF for the single way valves, and switches the
    /// opposite side's coil the opposite way for the double way valves").
    ///
    /// While the function is on (a ButtonHMI with HmiFunction = CylinderForwardBack), a
    /// press of a cylinder unit's button (a cylinder, a pneumatic positioning, an
    /// encoder cylinder - not an axis, not a motor) starts its back-and-forth movement
    /// instead of moving it while held: that side's command ON and the other side's OFF
    /// for 5 s (Period), then the other way round, and so on. A side with no command (a
    /// single valve's spring side) is just the other one OFF. A coupled unit's partners
    /// move with it, checked as a coupled press is. A press of a button of a unit moving
    /// so stops it (its partners too): both sides OFF. The function switched off, another
    /// user signing in, a unit leaving the screen or a button no longer able to act stop
    /// it as well. Maintenance and Admin only.
    ///
    /// The commands are the buttons' own (ButtonHMI.HoldAsCycle / ReleaseCycle): each
    /// with its conditions, rights and log; a momentary command held, another clicked.
    /// </summary>
    public static class UnitCycling
    {
        static UnitCycling()
        {
            SecurityManager.UserChanged += (s, e) => { if (Active) SetActive(false, T("Cyc_UserChanged", "another user signed in")); };
        }

        /// <summary>How long each way lasts (ms). Replaceable for the tests.</summary>
        public static int PeriodMs = 5000;

        /// <summary>The level the function asks for.</summary>
        public const SecurityLevel RequiredLevel = SecurityLevel.Maintenance;

        /// <summary>The function is on (the "Cylinder FW/Back" button).</summary>
        public static bool Active { get; private set; }

        /// <summary>Raised when the function is switched on or off.</summary>
        public static event EventHandler Changed;

        /// <summary>Said when a unit's movement starts or stops (the unit, the words); the unit shows it.</summary>
        public static event Action<CylinderUnitHMI, string> Notice;

        /// <summary>The time now. Replaceable for the tests.</summary>
        public static Func<DateTime> Now = () => DateTime.Now;

        private sealed class Run
        {
            public CylinderUnitHMI Unit;
            public bool SideA;
            public DateTime Next;
            public int Group;
        }

        private static readonly List<Run> _runs = new List<Run>();
        private static int _groups;
        private static System.Windows.Forms.Timer _timer;

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>Switches the function on (Maintenance and Admin only) or off - stopping every unit moving.</summary>
        public static void SetActive(bool on, string why = null)
        {
            if (on && !SecurityManager.HasAccess(RequiredLevel)) return;
            if (Active == on) return;
            Active = on;
            AuditLogger.Log("Cylinder FW/Back " + (on ? "ON" : "OFF") + (string.IsNullOrEmpty(why) ? "" : ": " + why) + ".");
            if (!on) StopAll(why);
            var h = Changed;
            if (h != null) try { h(null, EventArgs.Empty); } catch (Exception ex) { AuditLogger.Log("Cylinder FW/Back changed: " + ex.Message); }
        }

        /// <summary>True when the unit moves back and forth now.</summary>
        public static bool IsCycling(CylinderUnitHMI unit) { return unit != null && _runs.Any(r => r.Unit == unit); }

        /// <summary>The units moving back and forth now.</summary>
        public static IList<CylinderUnitHMI> Cycling { get { return _runs.Select(r => r.Unit).ToList(); } }

        /// <summary>The side a unit moves to now (true: A), null when it is not moving so.</summary>
        public static bool? SideOf(CylinderUnitHMI unit)
        {
            var r = _runs.FirstOrDefault(x => x.Unit == unit);
            return r == null ? (bool?)null : r.SideA;
        }

        // ---- Manual Movement only (17 October) ------------------------------------------

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<System.Windows.Forms.Control, object> _manualPages =
            new System.Runtime.CompilerServices.ConditionalWeakTable<System.Windows.Forms.Control, object>();

        /// <summary>Marks the Manual Movement page (its screen): the function's button is shown on it only.</summary>
        public static void MarkManualPage(System.Windows.Forms.Control page)
        {
            object o;
            if (page != null && !_manualPages.TryGetValue(page, out o)) _manualPages.Add(page, null);
        }

        /// <summary>True when the control is on the Manual Movement page (itself, or inside it).</summary>
        public static bool IsOnManualPage(System.Windows.Forms.Control c)
        {
            object o;
            for (var p = c; p != null; p = p.Parent)
                if (_manualPages.TryGetValue(p, out o)) return true;
            return false;
        }

        /// <summary>
        /// True when the Manual Movement page is shown in this one: it, inside it, or holding
        /// it a few levels down (the machine's tabs around a page), and in sight.
        /// </summary>
        public static bool ShowsManualPage(System.Windows.Forms.Control root)
        {
            if (root == null) return false;
            if (IsOnManualPage(root)) return true;
            var level = new List<System.Windows.Forms.Control> { root };
            for (int depth = 0; depth < 5 && level.Count > 0; depth++)
            {
                var next = new List<System.Windows.Forms.Control>();
                foreach (var c in level)
                    foreach (System.Windows.Forms.Control k in c.Controls)
                    {
                        object o;
                        if (_manualPages.TryGetValue(k, out o)) { if (k.Visible) return true; continue; }
                        next.Add(k);
                    }
                level = next;
            }
            return false;
        }

        /// <summary>True while Manual Movement is the page shown. The main window answers it; true without one.</summary>
        public static Func<bool> ManualPageShown = () => true;

        /// <summary>Off when Manual Movement is closed (the window calls it when the page changes).</summary>
        public static void CheckPage()
        {
            if (!Active) return;
            bool shown;
            try { shown = ManualPageShown(); } catch { shown = true; }
            if (!shown) SetActive(false, T("Cyc_PageLeft", "Manual Movement was closed"));
        }

        /// <summary>A unit that can move back and forth: a cylinder with a command (not an axis, not a motor).</summary>
        public static bool CanCycle(CylinderUnitHMI unit)
        {
            return unit != null && unit.Kind == UnitKind.Cylinder && unit.HasCommands;
        }

        /// <summary>
        /// A user's press or click of a unit's button (ButtonHMI.Intercept): true when the
        /// function takes it over - the movement starts, or stops.
        /// </summary>
        internal static bool Handle(CylinderUnitHMI unit, bool sideA, ButtonHMI pressed, ButtonHMI.UserAction action)
        {
            if (!Active || !CanCycle(unit)) return false;
            if (action == ButtonHMI.UserAction.Release) return IsCycling(unit);
            var run = _runs.FirstOrDefault(r => r.Unit == unit);
            if (run != null)
            {
                Stop(_runs.Where(r => r.Group == run.Group).Select(r => r.Unit).ToList(),
                     string.Format(T("Cyc_Pressed", "'{0}' pressed again"), unit.CouplingName), unit);
                return true;
            }
            Start(unit, sideA);
            return true;
        }

        /// <summary>Starts the unit (and the units coupled with it, when they can move with it) to the side pressed.</summary>
        private static void Start(CylinderUnitHMI unit, bool sideA)
        {
            var units = new List<CylinderUnitHMI> { unit };
            if (UnitCoupling.IsCoupled(unit))
            {
                var others = UnitCoupling.Coupled(unit.Kind).Where(u => u != unit && !IsCycling(u)).ToList();
                var why = UnitCoupling.WhyNotTogether(unit, sideA, others);
                if (why != null)
                    UnitCoupling.Dissolve(unit.Kind, unit, why + " - " + T("Cpl_OnlyThis", "only the unit pressed moves."));
                else units.AddRange(others);
            }
            var first = sideA ? unit.ButtonA : unit.ButtonB;
            if (first.HasActions && !first.CanAct) return;          // it may not do that now: nothing starts
            int group = ++_groups;
            var next = Now().AddMilliseconds(PeriodMs);
            foreach (var u in units) _runs.Add(new Run { Unit = u, SideA = sideA, Next = next, Group = group });
            AuditLogger.Log("back and forth every " + (PeriodMs / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s: " +
                            string.Join(", ", units.Select(u => "'" + u.CouplingName + "'")) + ", first '" + unit.SideText(sideA) + "'.");
            foreach (var r in _runs.Where(x => x.Group == group).ToList()) Apply(r);
            Say(unit, string.Format(T("Cyc_Started", "Back and forth every {0} s - press a button of it again to stop."),
                                    (PeriodMs / 1000.0).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture)));
            EnsureTimer();
        }

        /// <summary>The run's side ON, the other side OFF. False when its side cannot act now (it is then stopped).</summary>
        private static bool Apply(Run r)
        {
            var on = r.SideA ? r.Unit.ButtonA : r.Unit.ButtonB;
            var off = r.SideA ? r.Unit.ButtonB : r.Unit.ButtonA;
            off.ReleaseCycle();
            if (!on.HasActions) return true;                        // a single valve's spring side: the other OFF is the movement
            if (on.HoldAsCycle()) return true;
            Stop(new List<CylinderUnitHMI> { r.Unit },
                 string.Format(T("Cpl_CannotAct", "'{0}' cannot do '{1}' now"), r.Unit.CouplingName, r.Unit.SideText(r.SideA)), r.Unit);
            return false;
        }

        private static void EnsureTimer()
        {
            if (_timer == null)
            {
                _timer = new System.Windows.Forms.Timer { Interval = 200 };
                _timer.Tick += (s, e) => { try { Tick(); } catch (Exception ex) { AuditLogger.Log("back and forth: " + ex.Message); } };
            }
            if (!_timer.Enabled) _timer.Start();
        }

        /// <summary>Each unit whose time is up goes the other way; one gone from the screen stops.</summary>
        public static void Tick()
        {
            var now = Now();
            foreach (var r in _runs.ToList())
            {
                if (!_runs.Contains(r)) continue;
                if (r.Unit.IsDisposed || !r.Unit.Visible || !r.Unit.IsHandleCreated || r.Unit.FindForm() == null)
                {
                    Stop(new List<CylinderUnitHMI> { r.Unit }, T("Cpl_LeftScreen", "it left the screen"), null);
                    continue;
                }
                if (now < r.Next) continue;
                r.SideA = !r.SideA;
                r.Next = now.AddMilliseconds(PeriodMs);
                Apply(r);
            }
            if (_runs.Count == 0 && _timer != null) _timer.Stop();
        }

        /// <summary>Stops units: both sides OFF.</summary>
        public static void Stop(IList<CylinderUnitHMI> units, string why, CylinderUnitHMI shownOn)
        {
            var gone = _runs.Where(r => units.Contains(r.Unit)).ToList();
            if (gone.Count == 0) return;
            foreach (var r in gone)
            {
                _runs.Remove(r);
                if (!r.Unit.IsDisposed) { r.Unit.ButtonA.ReleaseCycle(); r.Unit.ButtonB.ReleaseCycle(); }
            }
            AuditLogger.Log("back and forth stopped: " + string.Join(", ", gone.Select(r => "'" + r.Unit.CouplingName + "'")) +
                            (string.IsNullOrEmpty(why) ? "." : " (" + why + ")."));
            if (shownOn != null) Say(shownOn, T("Cyc_Stopped", "Back and forth stopped") + (string.IsNullOrEmpty(why) ? "." : ": " + why + "."));
            if (_runs.Count == 0 && _timer != null) _timer.Stop();
        }

        /// <summary>Stops one unit (it left the screen).</summary>
        public static void Stop(CylinderUnitHMI unit, string why) { Stop(new List<CylinderUnitHMI> { unit }, why, null); }

        /// <summary>Stops every unit moving back and forth.</summary>
        public static void StopAll(string why)
        {
            if (_runs.Count == 0) return;
            Stop(_runs.Select(r => r.Unit).ToList(), why, null);
        }

        private static void Say(CylinderUnitHMI unit, string text)
        {
            var h = Notice;
            if (h != null && unit != null) try { h(unit, text); } catch { }
        }

        /// <summary>Everything stopped and switched off (tests).</summary>
        public static void ResetForTests()
        {
            StopAll(null);
            Active = false;
            PeriodMs = 5000;
            Now = () => DateTime.Now;
        }
    }
}
