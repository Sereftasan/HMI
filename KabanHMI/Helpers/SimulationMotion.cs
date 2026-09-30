using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// Makes a simulated machine MOVE, so pages can be tried without a PLC.
    ///
    /// The simulation driver only stores numbers: it has no idea that one bit is a
    /// jog command and another is the sensor that should answer it. The units and
    /// axis rows do know - a CylinderUnitHMI carries its two commands and their
    /// lamps, an axis row its jog bits and its position - so that knowledge is
    /// collected with everything else at start-up and replayed here.
    ///
    /// WHAT IT DOES, on simulated stations only:
    ///
    ///   Axis      holding the forward (right) button counts the position UP,
    ///             holding back (left) counts it DOWN, at a steady rate.
    ///
    ///   Unit      pressing one side switches the OTHER side's lamp off at once -
    ///             the cylinder has left that end - and lights this side's lamp
    ///             after the movement time, one second, of continuous pressing.
    ///             Let go early and neither lamp is lit: the cylinder is mid
    ///             stroke, which is exactly what the real machine would report.
    ///
    ///   SET, RESET and ALTERNATE commands are not held down - one press latches
    ///   them - so for those the movement completes immediately.
    /// </summary>
    public static class SimulationMotion
    {
        /// <summary>How long a momentary press must be held before the far sensor answers.</summary>
        public const int StrokeMs = 1000;

        /// <summary>Display units travelled per second while a jog button is held.</summary>
        public const double AxisSpeedPerSecond = 25.0;

        public sealed class Unit
        {
            public int Station;
            public string CommandA = "", CommandB = "", LampA = "", LampB = "";
            public BitActionType ActionA, ActionB;
            internal DateTime? HeldA, HeldB;
            internal bool DoneA, DoneB;
        }

        public sealed class Axis
        {
            public int Station;
            public string Back = "", Forward = "", Position = "";
            public DataTypeEnum PositionType;
            public double Divisor = 1;
            internal DateTime? LastTick;
        }

        private static readonly List<Unit> _units = new List<Unit>();
        private static readonly List<Axis> _axes = new List<Axis>();
        private static Timer _timer;

        public static int UnitCount { get { return _units.Count; } }
        public static int AxisCount { get { return _axes.Count; } }

        public static void Clear()
        {
            _units.Clear();
            _axes.Clear();
        }

        public static void Register(CylinderUnitHMI unit)
        {
            if (unit == null) return;
            if (string.IsNullOrWhiteSpace(unit.CommandAAddress) && string.IsNullOrWhiteSpace(unit.CommandBAddress)) return;
            // The same unit on a second page, or a page searched twice: once.
            string a = (unit.CommandAAddress ?? "").Trim(), b = (unit.CommandBAddress ?? "").Trim();
            if (_units.Exists(u => u.Station == unit.PlcNumber && u.CommandA == a && u.CommandB == b)) return;
            _units.Add(new Unit
            {
                Station = unit.PlcNumber,
                CommandA = (unit.CommandAAddress ?? "").Trim(), CommandB = (unit.CommandBAddress ?? "").Trim(),
                LampA = (unit.Lamp1AAddress ?? "").Trim(), LampB = (unit.Lamp1BAddress ?? "").Trim(),
                ActionA = unit.CommandAAction, ActionB = unit.CommandBAction
            });
        }

        public static void Register(AxisSettingHMI axis)
        {
            if (axis == null) return;
            if (string.IsNullOrWhiteSpace(axis.PositionAddress)) return;
            // The same row searched twice would move its axis twice per step.
            string pos = axis.PositionAddress.Trim();
            if (_axes.Exists(x => x.Station == axis.PlcNumber && string.Equals(x.Position, pos, StringComparison.OrdinalIgnoreCase))) return;
            _axes.Add(new Axis
            {
                Station = axis.PlcNumber,
                Back = (axis.CommandBackAddress ?? "").Trim(), Forward = (axis.CommandForwardAddress ?? "").Trim(),
                Position = (axis.PositionAddress ?? "").Trim(), PositionType = axis.PositionDataType,
                Divisor = axis.Divisor <= 0 ? 1 : axis.Divisor
            });
        }

        /// <summary>Starts the movement clock. Harmless when nothing is simulated.</summary>
        public static void Start()
        {
            if (_timer != null) return;
            _timer = new Timer { Interval = 100 };
            _timer.Tick += (s, e) =>
            {
                try { Tick(DateTime.Now); }
                catch (Exception ex) { AuditLogger.Log("simulated movement: " + ex.Message); }
            };
            _timer.Start();
        }

        /// <summary>One step of simulated movement. Public so a test can drive the clock.</summary>
        public static void Tick(DateTime now)
        {
            foreach (var u in _units)
            {
                var drv = Simulated(u.Station);
                if (drv == null) continue;

                bool a = Bit(drv, u.CommandA), b = Bit(drv, u.CommandB);
                Side(drv, now, a, u.ActionA, u.LampA, u.LampB, ref u.HeldA, ref u.DoneA);
                Side(drv, now, b, u.ActionB, u.LampB, u.LampA, ref u.HeldB, ref u.DoneB);
            }

            foreach (var x in _axes)
            {
                var drv = Simulated(x.Station);
                if (drv == null) { x.LastTick = null; continue; }

                bool back = Bit(drv, x.Back), forward = Bit(drv, x.Forward);
                if (back == forward) { x.LastTick = now; continue; }   // neither, or both: standing still

                var since = x.LastTick ?? now;
                x.LastTick = now;
                double seconds = (now - since).TotalSeconds;
                if (seconds <= 0 || seconds > 1) continue;             // first tick, or the clock jumped

                double step = AxisSpeedPerSecond * seconds * x.Divisor * (forward ? 1 : -1);
                long value = (long)Math.Round(ReadPosition(drv, x) + step);
                WritePosition(drv, x, value);
            }
        }

        /// <summary>One side of a unit: its lamp answers its own command.</summary>
        private static void Side(IPlcDriver drv, DateTime now, bool pressed, BitActionType action,
                                 string ownLamp, string otherLamp, ref DateTime? held, ref bool done)
        {
            if (!pressed)
            {
                held = null;
                done = false;
                return;
            }

            if (held == null)
            {
                held = now;
                // Leaving this end is instant: the sensor at the far end lets go
                // as soon as the cylinder starts moving.
                if (!string.IsNullOrEmpty(otherLamp)) drv.Write(otherLamp, 0);
            }

            if (done) return;

            bool latched = action != BitActionType.Momentary;      // SET, RESET, ALTERNATE: one press is the whole press
            if (latched || (now - held.Value).TotalMilliseconds >= StrokeMs)
            {
                if (!string.IsNullOrEmpty(ownLamp)) drv.Write(ownLamp, 1);
                done = true;
            }
        }

        private static IPlcDriver Simulated(int station)
        {
            // The simulator's own driver, not the logging one PlcManager hands out:
            // this movement plays the PLC program's part, and its writes are the
            // PLC's, not the HMI's - they must not appear in the PLC write log.
            var drv = PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(station));
            // Only a simulated station is moved by this: a real PLC moves itself.
            return drv != null && drv.GetType().Name == "SimulationDriver" && drv.IsConnected ? drv : null;
        }

        private static bool Bit(IPlcDriver drv, string address)
        {
            int v;
            return !string.IsNullOrEmpty(address) && drv.TryRead(address, out v) && v != 0;
        }

        private static long ReadPosition(IPlcDriver drv, Axis x)
        {
            int low;
            if (!drv.TryRead(x.Position, out low)) return 0;
            if (x.PositionType != DataTypeEnum.Bin_32) return (short)low;

            int high;
            drv.TryRead(PlcAddressHelper.GetNextAddress(x.Position), out high);
            return (int)(((uint)(high & 0xFFFF) << 16) | (uint)(low & 0xFFFF));
        }

        private static void WritePosition(IPlcDriver drv, Axis x, long value)
        {
            if (x.PositionType != DataTypeEnum.Bin_32)
            {
                drv.Write(x.Position, (int)Math.Max(short.MinValue, Math.Min(short.MaxValue, value)));
                return;
            }
            drv.Write(x.Position, (int)(value & 0xFFFF));
            drv.Write(PlcAddressHelper.GetNextAddress(x.Position), (int)((value >> 16) & 0xFFFF));
        }
    }
}

