using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// COUPLED UNITS FOR MANUAL MOVEMENT (8 October: "clatch multiple same type of
    /// controls (cylinder or axis) together for manual movement - selecting them to
    /// clatch first (they have to be at the same position to be clatched ...), then
    /// clicking any one of them activates all of those controls same side buttons,
    /// just like all of them clicked at the same time").
    ///
    /// The chain on a unit's frame couples it (and uncouples it). Units are coupled
    /// with units of their own kind - cylinders with cylinders, axes with axes - and
    /// only when they stand where the others stand: a cylinder at the same end (its
    /// end sensor lit, the other not), an axis at the same position (within
    /// <see cref="AxisTolerance"/>). A press of a coupled unit's button presses the
    /// same side's button of every other one; its release releases them all, however
    /// the press ended. Every button still acts with its own conditions, rights and
    /// log.
    ///
    /// SAFETY: before each coupled movement the units are checked again - all at the
    /// same place, every same-side button able to act. If not, the coupling is let
    /// go (and said so) and only the pressed unit moves, as it would on its own. A
    /// unit that leaves the screen (another page or tab) leaves the coupling: only
    /// units in sight move together.
    ///
    /// WHICH ONES CAN JOIN, AND STARTING AGAIN (16 October: "when the user selects one
    /// to clutch and forgets it ... give them an easy way to cancel active clutching;
    /// most of the other units are not suitable for the coupling, so make the chains of
    /// the ones that can join blink while a coupling is on, and ask the user whether to
    /// cancel the previous coupling when they click a chain that does not blink"):
    /// while units of a kind are coupled, the chains of the units of that kind in sight
    /// that stand where they stand blink. A click on a chain that does not blink asks
    /// whether to let the coupling go and couple that unit instead. A unit uncoupled
    /// leaves no unit coupled alone.
    /// </summary>
    public static class UnitCoupling
    {
        static UnitCoupling()
        {
            // another user (or nobody): what the last one coupled is let go
            SecurityManager.UserChanged += (s, e) => { if (_coupled.Count > 0) Clear(); };
        }

        /// <summary>How far apart two axes may be and still be "at the same position" (in their display unit: mm, degrees).</summary>
        public static double AxisTolerance = 0.5;

        private static readonly List<CylinderUnitHMI> _coupled = new List<CylinderUnitHMI>();
        /// <summary>The buttons a coupled press holds, per pressed button (released with it).</summary>
        private static readonly Dictionary<ButtonHMI, List<ButtonHMI>> _held = new Dictionary<ButtonHMI, List<ButtonHMI>>();

        /// <summary>Said when the coupling is refused or let go (the unit, the reason); the unit shows it.</summary>
        public static event Action<CylinderUnitHMI, string> Notice;

        /// <summary>
        /// Asks whether to let the coupling now go and couple the unit clicked instead (Yes: let go).
        /// Replaceable so a test answers.
        /// </summary>
        public static Func<string, bool> AskLetGo = q =>
            System.Windows.Forms.MessageBox.Show(q, T("Ttl_Coupling", "Coupled units"), System.Windows.Forms.MessageBoxButtons.YesNo,
                                                 System.Windows.Forms.MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes;

        /// <summary>The coupled units of a kind.</summary>
        public static IList<CylinderUnitHMI> Coupled(UnitKind kind)
        {
            _coupled.RemoveAll(u => u.IsDisposed);
            return _coupled.Where(u => u.Kind == kind).ToList();
        }

        public static bool IsCoupled(CylinderUnitHMI unit) { return unit != null && _coupled.Contains(unit); }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>Couples the unit with the others of its kind, or uncouples it. Null, or why it could not.</summary>
        public static string Toggle(CylinderUnitHMI unit)
        {
            if (unit == null) return null;
            if (IsCoupled(unit)) { Remove(unit, null); return null; }
            if (!unit.HasCommands) return T("Cpl_NoCommands", "This unit has no movement to couple.");
            var group = Coupled(unit.Kind);
            string place = PlaceOf(unit);
            if (place == null)
                return unit.Kind == UnitKind.Axis ? T("Cpl_NoPosition", "Its position is not known: it cannot be coupled.")
                                                  : T("Cpl_NoEnd", "It is not at one of its ends: only a unit at an end can be coupled.");
            if (group.Count > 0 && !SamePlace(group[0], unit))
            {
                // not where they are (its chain does not blink): start again with it?
                string names = string.Join(", ", group.Select(u => "'" + u.CouplingName + "'"));
                string q = string.Format(T("Cpl_AskLetGo", "Coupled now: {0} ({1}). '{2}' is not where they are ({3}), so it cannot join them.\n\nLet that coupling go and start a new one with '{2}'?"),
                                         names, Describe(group[0]), unit.CouplingName, Describe(unit));
                if (!AskLetGo(q)) return null;
                foreach (var u in group) _coupled.Remove(u);
                AuditLogger.Log("coupling of " + names + " let go by the user, to couple '" + unit.CouplingName + "' instead.");
                group.Clear();
            }
            _coupled.Add(unit);
            AuditLogger.Log("coupled '" + unit.CouplingName + "' (" + Describe(unit) + ") with " +
                            (group.Count == 0 ? "nothing yet" : string.Join(", ", group.Select(u => "'" + u.CouplingName + "'"))) + ".");
            Refresh();
            return null;
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
            RefreshInvitations();
        }

        // ---- the chains that can join blink (16 October) ----------------------------------

        private static System.Windows.Forms.Timer _blink;

        /// <summary>True when the unit could join the units of its kind coupled now (in sight, where they stand, with a movement).</summary>
        public static bool CanJoin(CylinderUnitHMI unit)
        {
            if (unit == null || unit.IsDisposed || IsCoupled(unit) || !unit.HasCommands || unit.Kind == UnitKind.Inverter) return false;
            if (!unit.Visible || !unit.IsHandleCreated) return false;
            var group = Coupled(unit.Kind);
            return group.Count > 0 && PlaceOf(unit) != null && SamePlace(group[0], unit);
        }

        /// <summary>
        /// Every chain in sight: blinking when its unit could join, still when not. Run by
        /// the blink's timer while a coupling is on (the units move: who can join changes).
        /// </summary>
        internal static void RefreshInvitations()
        {
            bool any = _coupled.Any(u => !u.IsDisposed);
            foreach (var u in CylinderUnitHMI.Live) u.CoupleChain.Inviting = any && CanJoin(u);
            if (any)
            {
                if (_blink == null)
                {
                    _blink = new System.Windows.Forms.Timer { Interval = 500 };
                    _blink.Tick += (s, e) =>
                    {
                        CoupleBadge.Phase = !CoupleBadge.Phase;
                        RefreshInvitations();
                        foreach (var u in CylinderUnitHMI.Live) if (u.CoupleChain.Inviting) u.CoupleChain.Invalidate();
                    };
                }
                if (!_blink.Enabled) _blink.Start();
            }
            else if (_blink != null && _blink.Enabled)
            {
                _blink.Stop();
                CoupleBadge.Phase = true;
            }
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

        /// <summary>True when the two stand at the same place.</summary>
        internal static bool SamePlace(CylinderUnitHMI x, CylinderUnitHMI y)
        {
            if (x.Kind != y.Kind) return false;
            if (x.Kind == UnitKind.Cylinder && x.HasEndSensors && y.HasEndSensors)
            {
                var px = PlaceOf(x);
                return px != null && px == PlaceOf(y);
            }
            double? a = x.PositionValue, b = y.PositionValue;
            return a.HasValue && b.HasValue && Math.Abs(a.Value - b.Value) <= AxisTolerance + 1e-9;
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
            string why = null;
            foreach (var o in others)
            {
                if (!o.Visible || !o.IsHandleCreated) { why = string.Format(T("Cpl_Gone", "'{0}' is not on the screen"), o.CouplingName); break; }
                if (!SamePlace(unit, o)) { why = string.Format(T("Cpl_Moved", "'{0}' is not where '{1}' is ({2} / {3})"), o.CouplingName, unit.CouplingName, Describe(o), Describe(unit)); break; }
                var b = sideA ? o.ButtonA : o.ButtonB;
                if (!b.CanAct) { why = string.Format(T("Cpl_CannotAct", "'{0}' cannot do '{1}' now"), o.CouplingName, o.SideText(sideA)); break; }
            }
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
