using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// COUPLED UNITS FOR MANUAL MOVEMENT (8 October: "clatch multiple same type of
    /// controls (cylinder or axis) together for manual movement - selecting them to
    /// clatch first, then clicking any one of them activates all of those controls same
    /// side buttons, just like all of them clicked at the same time").
    ///
    /// The chain on a unit's frame couples it (and uncouples it). Units are coupled with
    /// units of their own kind - cylinders with cylinders, axes with axes - and only
    /// those that match (17 October: "make it safe to clutch, not unfunctional"):
    ///
    ///   a cylinder with a sensor at each end    at the same end (its sensor lit, the other not)
    ///   a cylinder measured by its position     the same FeedbackType (an encoder with
    ///   (pneumatic positioning, encoder rows)   encoders, an analogue sensor with those) -
    ///                                           wherever each one stands
    ///   an axis                                 only while the axes are in STEP mode (the
    ///                                           bar's 10 / 1 / 0.1 / 0.01 mm, not JOG:
    ///                                           SystemCondition.AxisStepMode), and on the
    ///                                           same page - the user sees every axis moving
    ///
    /// A press of a coupled unit's button presses the same side's button of every other
    /// one; its release releases them all, however the press ended. Every button still
    /// acts with its own conditions, rights and log.
    ///
    /// WHO MAY: Maintenance and Admin only (17 October: "a normal operator does not need
    /// it, and it could be unsafe for them") - an operator sees no chain; another user
    /// signing in lets every coupling go.
    ///
    /// WHICH CHAINS: while units of a kind are coupled, the chains of the units that
    /// could not join them are disabled (drawn faded; a click says why) - the ones left
    /// are the ones that can (17 October: "disable the clutching button if there is no
    /// match ... the possible clutches appear anyway"). With nothing coupled, a chain is
    /// disabled while its unit cannot be coupled at all (a cylinder between its ends,
    /// the axes in JOG). The caption bar's blue chain lists the coupled units
    /// (MainForm): one to uncouple, or all of them.
    ///
    /// SAFETY: before each coupled movement the units are checked again - in sight,
    /// still matching, every same-side button able to act. If not, the coupling is let
    /// go (and said so) and only the pressed unit moves, as it would on its own. A unit
    /// that leaves the screen (another page or tab) leaves the coupling.
    /// </summary>
    public static class UnitCoupling
    {
        static UnitCoupling()
        {
            // another user (or nobody): what the last one coupled is let go
            SecurityManager.UserChanged += (s, e) =>
            {
                if (_coupled.Count > 0) Clear();
                else RefreshAvailability();
            };
        }

        /// <summary>The level coupling asks for (17 October: Maintenance).</summary>
        public const SecurityLevel RequiredLevel = SecurityLevel.Maintenance;

        /// <summary>True when the signed-in user may couple units.</summary>
        public static bool Allowed { get { return SecurityManager.HasAccess(RequiredLevel); } }

        private static readonly List<CylinderUnitHMI> _coupled = new List<CylinderUnitHMI>();
        /// <summary>The buttons a coupled press holds, per pressed button (released with it).</summary>
        private static readonly Dictionary<ButtonHMI, List<ButtonHMI>> _held = new Dictionary<ButtonHMI, List<ButtonHMI>>();

        /// <summary>Said when the coupling is refused or let go (the unit, the reason); the unit shows it.</summary>
        public static event Action<CylinderUnitHMI, string> Notice;

        /// <summary>Raised when units were coupled or uncouped (the caption bar's chain follows).</summary>
        public static event EventHandler Changed;

        /// <summary>
        /// The axes in step mode now: true / false, or null when this machine has no rule
        /// for it (then axes are not coupled). Replaceable so a test answers.
        /// </summary>
        public static Func<bool?> AxesInStepMode = () =>
        {
            if (MachineConditionMap.GetRule(SystemCondition.AxisStepMode) == null) return null;
            var check = Kaban.UI.Plc.PlcScanner.LastConditionCheck;
            if (check == null) return null;
            try { return check(SystemCondition.AxisStepMode); } catch { return null; }
        };

        /// <summary>The coupled units of a kind.</summary>
        public static IList<CylinderUnitHMI> Coupled(UnitKind kind)
        {
            _coupled.RemoveAll(u => u.IsDisposed);
            return _coupled.Where(u => u.Kind == kind).ToList();
        }

        /// <summary>Every coupled unit, of every kind, in the order they were coupled.</summary>
        public static IList<CylinderUnitHMI> All
        {
            get { _coupled.RemoveAll(u => u.IsDisposed); return _coupled.ToList(); }
        }

        public static bool IsCoupled(CylinderUnitHMI unit) { return unit != null && _coupled.Contains(unit); }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>Couples the unit with the others of its kind, or uncouples it. Null, or why it could not.</summary>
        public static string Toggle(CylinderUnitHMI unit)
        {
            if (unit == null) return null;
            if (IsCoupled(unit)) { Remove(unit, null); return null; }
            var why = WhyNot(unit);
            if (why != null) return why;
            var group = Coupled(unit.Kind);
            _coupled.Add(unit);
            AuditLogger.Log("coupled '" + unit.CouplingName + "' (" + Describe(unit) + ") with " +
                            (group.Count == 0 ? "nothing yet" : string.Join(", ", group.Select(u => "'" + u.CouplingName + "'"))) + ".");
            Refresh();
            return null;
        }

        /// <summary>Why the unit cannot be coupled now (with the units of its kind coupled, if any), or null when it can.</summary>
        public static string WhyNot(CylinderUnitHMI unit)
        {
            if (unit == null) return "";
            if (!Allowed) return T("Cpl_Level", "Coupling units is for Maintenance and Admin users.");
            if (!unit.HasCommands || unit.Kind == UnitKind.Inverter) return T("Cpl_NoCommands", "This unit has no movement to couple.");
            var alone = WhyNotAlone(unit);
            if (alone != null) return alone;
            var group = Coupled(unit.Kind).Where(u => u != unit).ToList();
            return group.Count == 0 ? null : WhyNotWith(group[0], unit);
        }

        /// <summary>What keeps a unit from being coupled at all, or null.</summary>
        private static string WhyNotAlone(CylinderUnitHMI unit)
        {
            if (unit.Kind == UnitKind.Axis)
            {
                var step = AxesInStepMode();
                if (step == null) return T("Cpl_NoStepMode", "Axes are coupled only in step mode, and this machine does not say which mode it is in.");
                if (step == false) return T("Cpl_Jog", "Axes are coupled only in step mode: choose a step above (10 / 1 / 0.1 / 0.01 mm), not JOG.");
                return null;
            }
            if (unit.HasEndSensors)
                return PlaceOf(unit) == null ? T("Cpl_NoEnd", "It is not at one of its ends: only a unit at an end can be coupled.") : null;
            if (IsPositioned(unit)) return null;
            return T("Cpl_NoFeedback", "Nothing tells where it is (no sensor at each end, no position): it cannot be coupled.");
        }

        /// <summary>What keeps a unit from joining a coupled one, or null when it matches.</summary>
        private static string WhyNotWith(CylinderUnitHMI coupled, CylinderUnitHMI unit)
        {
            if (coupled.Kind != unit.Kind) return T("Cpl_OtherKind", "Only units of one kind are coupled: cylinders with cylinders, axes with axes.");
            if (unit.Kind == UnitKind.Axis)
                return SamePage(coupled, unit) ? null
                     : string.Format(T("Cpl_OtherPage", "It is not on the page of the coupled axes ('{0}'): only axes in sight together move together."), coupled.CouplingName);
            if (coupled.HasEndSensors && unit.HasEndSensors)
                return SamePlace(coupled, unit) ? null
                     : string.Format(T("Cpl_NotSame", "It is not where the coupled units are ({0}): only units at the same position move together."), Describe(coupled));
            if (IsPositioned(coupled) && IsPositioned(unit) && !coupled.HasEndSensors && !unit.HasEndSensors)
                return coupled.FeedbackType == unit.FeedbackType ? null
                     : string.Format(T("Cpl_OtherFeedback", "It is measured otherwise than the coupled units ({0} / {1}): only units of the same feedback type move together."),
                                     unit.FeedbackType, coupled.FeedbackType);
            return string.Format(T("Cpl_OtherFeedback", "It is measured otherwise than the coupled units ({0} / {1}): only units of the same feedback type move together."),
                                 unit.FeedbackType, coupled.FeedbackType);
        }

        /// <summary>A cylinder measured by its position (pneumatic positioning, an encoder or analogue sensor).</summary>
        private static bool IsPositioned(CylinderUnitHMI unit)
        {
            return unit.Kind == UnitKind.Cylinder &&
                   (unit.FeedbackType == CylinderFeedback.AnalogueMagnetic || unit.FeedbackType == CylinderFeedback.EncoderPlusReference ||
                    (!unit.HasEndSensors && unit.HasPosition));
        }

        /// <summary>The page a unit is on: its tab, else the screen below the window.</summary>
        internal static System.Windows.Forms.Control PageOf(System.Windows.Forms.Control c)
        {
            System.Windows.Forms.Control top = null;
            for (var p = c == null ? null : c.Parent; p != null; p = p.Parent)
            {
                if (p is System.Windows.Forms.TabPage) return p;
                if (p is System.Windows.Forms.Form) break;
                top = p;
            }
            return top;
        }

        private static bool SamePage(CylinderUnitHMI a, CylinderUnitHMI b)
        {
            var pa = PageOf(a);
            return pa != null && pa == PageOf(b);
        }

        /// <summary>True when the unit could join the units of its kind coupled now (in sight, matching).</summary>
        public static bool CanJoin(CylinderUnitHMI unit)
        {
            if (unit == null || unit.IsDisposed || IsCoupled(unit)) return false;
            if (!unit.Visible || !unit.IsHandleCreated) return false;
            return Coupled(unit.Kind).Count > 0 && WhyNot(unit) == null;
        }

        /// <summary>Its chain may be clicked: coupled (to uncouple), or it can be coupled now.</summary>
        public static bool Available(CylinderUnitHMI unit)
        {
            if (unit == null || unit.IsDisposed) return false;
            return IsCoupled(unit) || WhyNot(unit) == null;
        }

        /// <summary>Takes a unit out (the reason said when there is one); one left alone is no coupling.</summary>
        public static void Remove(CylinderUnitHMI unit, string reason)
        {
            if (!_coupled.Remove(unit)) return;
            AuditLogger.Log("uncoupled '" + unit.CouplingName + "'" + (string.IsNullOrEmpty(reason) ? "." : ": " + reason));
            // one left alone is no coupling (16 October: not one forgotten, still coupled)
            var rest = Coupled(unit.Kind);
            if (rest.Count == 1)
            {
                _coupled.Remove(rest[0]);
                AuditLogger.Log("uncoupled '" + rest[0].CouplingName + "': left alone.");
            }
            Refresh();
        }

        /// <summary>Every coupled unit of a kind let go, and why.</summary>
        public static void Dissolve(UnitKind kind, CylinderUnitHMI shownOn, string reason)
        {
            var units = Coupled(kind);
            if (units.Count == 0) return;
            foreach (var u in units) _coupled.Remove(u);
            AuditLogger.Log("coupling of " + string.Join(", ", units.Select(u => "'" + u.CouplingName + "'")) + " let go: " + reason);
            Refresh();
            var h = Notice;
            if (h != null) h(shownOn, T("Cpl_LetGo", "The coupling was let go") + ": " + reason);
        }

        /// <summary>The user lets the whole coupling of a kind go (the chain's "Uncouple all").</summary>
        public static void LetGo(UnitKind kind)
        {
            var units = Coupled(kind);
            if (units.Count == 0) return;
            foreach (var u in units) _coupled.Remove(u);
            AuditLogger.Log("coupling of " + string.Join(", ", units.Select(u => "'" + u.CouplingName + "'")) + " let go by the user.");
            Refresh();
        }

        /// <summary>The user lets every coupling go (the caption bar's "Uncouple all").</summary>
        public static void LetGoAll()
        {
            var units = All;
            if (units.Count == 0) return;
            _coupled.Clear();
            AuditLogger.Log("every coupling let go by the user (" + string.Join(", ", units.Select(u => "'" + u.CouplingName + "'")) + ").");
            Refresh();
        }

        /// <summary>Everything let go (tests, a sign-out).</summary>
        public static void Clear()
        {
            _coupled.Clear();
            _held.Clear();
            Refresh();
        }

        private static void Refresh()
        {
            foreach (var u in _coupled.ToList()) if (!u.IsDisposed) u.ShowCoupled(true);
            foreach (var u in CylinderUnitHMI.Live) if (!_coupled.Contains(u)) u.ShowCoupled(false);
            RefreshAvailability();
            var h = Changed;
            if (h != null) try { h(null, EventArgs.Empty); } catch (Exception ex) { AuditLogger.Log("coupling changed: " + ex.Message); }
        }

        // ---- which chains may be clicked (17 October) -------------------------------------

        private static System.Windows.Forms.Timer _watch;

        /// <summary>
        /// Every chain in sight: available or disabled, as its unit stands now. Run by a
        /// timer while units are on screens (they move: who can be coupled changes).
        /// </summary>
        internal static void RefreshAvailability()
        {
            foreach (var u in CylinderUnitHMI.Live) u.ShowCouplingAvailable(Available(u));
        }

        /// <summary>Starts following the units (a unit with a chain calls it).</summary>
        internal static void Watch()
        {
            if (_watch != null) return;
            _watch = new System.Windows.Forms.Timer { Interval = 400 };
            _watch.Tick += (s, e) => { try { RefreshAvailability(); } catch (Exception ex) { AuditLogger.Log("coupling chains: " + ex.Message); } };
            _watch.Start();
        }

        /// <summary>Where a unit stands: "A" / "B" (a cylinder at an end), a position (an axis, an encoder cylinder), or null (unknown).</summary>
        internal static string PlaceOf(CylinderUnitHMI unit)
        {
            if (unit.Kind == UnitKind.Cylinder && unit.HasEndSensors)
            {
                bool a = unit.ButtonA.Lamp1On, b = unit.ButtonB.Lamp1On;
                return a && !b ? "A" : b && !a ? "B" : null;
            }
            double? pos = unit.PositionValue;
            return pos.HasValue ? pos.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : null;
        }

        /// <summary>True when two cylinders with a sensor at each end stand at the same end.</summary>
        internal static bool SamePlace(CylinderUnitHMI x, CylinderUnitHMI y)
        {
            if (x.Kind != y.Kind || !x.HasEndSensors || !y.HasEndSensors) return false;
            var px = PlaceOf(x);
            return px != null && px == PlaceOf(y);
        }

        /// <summary>Where a unit stands, in words (messages, the log).</summary>
        internal static string Describe(CylinderUnitHMI unit)
        {
            if (unit.Kind == UnitKind.Cylinder && unit.HasEndSensors)
            {
                var p = PlaceOf(unit);
                return p == "A" ? unit.SideText(true) : p == "B" ? unit.SideText(false) : T("Cpl_Between", "between its ends");
            }
            double? pos = unit.PositionValue;
            return pos.HasValue ? pos.Value.ToString("0.###", System.Globalization.CultureInfo.CurrentCulture) : "?";
        }

        /// <summary>
        /// Why the units coupled with one cannot move with it now (null: they can) - in
        /// sight, still matching, the same side's button able to act. Asked before every
        /// coupled movement (and by the back-and-forth movement, UnitCycling).
        /// </summary>
        internal static string WhyNotTogether(CylinderUnitHMI unit, bool sideA, IEnumerable<CylinderUnitHMI> others)
        {
            foreach (var o in others)
            {
                if (!o.Visible || !o.IsHandleCreated) return string.Format(T("Cpl_Gone", "'{0}' is not on the screen"), o.CouplingName);
                var apart = unit.Kind == UnitKind.Axis ? WhyNotAlone(o) ?? WhyNotWith(unit, o) : WhyNotWith(unit, o);
                if (apart != null)
                    return unit.Kind == UnitKind.Cylinder && unit.HasEndSensors && o.HasEndSensors
                        ? string.Format(T("Cpl_Moved", "'{0}' is not where '{1}' is ({2} / {3})"), o.CouplingName, unit.CouplingName, Describe(o), Describe(unit))
                        : "'" + o.CouplingName + "': " + apart;
                var b = sideA ? o.ButtonA : o.ButtonB;
                if (!b.CanAct) return string.Format(T("Cpl_CannotAct", "'{0}' cannot do '{1}' now"), o.CouplingName, o.SideText(sideA));
            }
            return null;
        }

        /// <summary>A coupled unit's button was pressed, released or clicked: the others follow - or the coupling goes.</summary>
        internal static void Acted(CylinderUnitHMI unit, bool sideA, ButtonHMI pressed, ButtonHMI.UserAction action)
        {
            if (action == ButtonHMI.UserAction.Release)
            {
                List<ButtonHMI> followers;
                if (_held.TryGetValue(pressed, out followers))
                {
                    _held.Remove(pressed);
                    foreach (var b in followers) try { b.ReleaseAsCoupled(); } catch (Exception ex) { AuditLogger.Log("coupled release: " + ex.Message); }
                }
                return;
            }
            if (!IsCoupled(unit)) return;
            var others = Coupled(unit.Kind).Where(u => u != unit).ToList();
            if (others.Count == 0) return;

            // checked again before every movement: in sight, at the same place, able to act
            string why = WhyNotTogether(unit, sideA, others);
            if (why != null) { Dissolve(unit.Kind, unit, why + " - " + T("Cpl_OnlyThis", "only the unit pressed moves.")); return; }

            var follow = others.Select(o => sideA ? o.ButtonA : o.ButtonB).ToList();
            AuditLogger.Log("coupled " + (action == ButtonHMI.UserAction.Press ? "press" : "click") + " of '" + unit.SideText(sideA) + "' on '" + unit.CouplingName +
                            "': also " + string.Join(", ", others.Select(o => "'" + o.CouplingName + "'")) + ".");
            if (action == ButtonHMI.UserAction.Press)
            {
                _held[pressed] = follow;
                foreach (var b in follow) try { b.PressAsCoupled(); } catch (Exception ex) { AuditLogger.Log("coupled press: " + ex.Message); }
            }
            else
                foreach (var b in follow) try { b.ClickAsCoupled(); } catch (Exception ex) { AuditLogger.Log("coupled click: " + ex.Message); }
        }
    }
}
