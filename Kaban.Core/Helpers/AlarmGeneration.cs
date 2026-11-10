using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Kaban.Core.Enums;
using Kaban.Core.Interfaces;
using Kaban.Core.Models;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// ALARMS THE HMI RAISES ITSELF (1 October), without the PLC.
    ///
    /// Switched on per machine (Alarm Settings: the activation and the general
    /// delay; a delay of its own per alarm in the table):
    ///
    ///   * every CYLINDER UNIT's movement is timed from the moment the output that
    ///     drives it says so - a double valve's own coil output going ON; a single
    ///     (spring) valve's coil output going ON for the coil's movement and going
    ///     OFF for the spring's - and has to be confirmed by its feedback within its
    ///     delay: the target sensor ON and the start sensor OFF; a single sensor
    ///     going OFF for the end it does not see; the clamp's pressure switch; a
    ///     position reading that moved and settled. Not confirmed in time, the HMI
    ///     raises the movement's alarm with a SUB-CODE naming the signal that
    ///     failed - M840.1 the start sensor stayed ON, M840.2 the target sensor did
    ///     not come ON, M840.3 both came ON, M840.4 both coils were ON, M840.5 the
    ///     position did not change, M840.6 it was still changing - and the helper
    ///     page opens on that failure only. A movement let go of before it was
    ///     confirmed (a 5/3 button released) or reversed is not a failure; one that
    ///     is confirmed later clears its alarm.
    ///   * every AXIS ROW raises its alarm while its Servo Alarm or Axis Alarm word
    ///     is not zero, the code as the sub-code (M801.16.3); an Axis Warning is
    ///     only a warning and raises nothing. AT ONCE, and ON BY ITSELF (8 October:
    ///     the PLC raises its cylinder alarms, so the HMI's were left off - and the
    ///     axis alarms with them): its own switch per machine, on unless switched
    ///     off; the delay is the cylinders' only.
    ///   * every INVERTER UNIT (InverterUnitHMI, 14 October) raises its alarm while
    ///     its fault code word is not zero - the inverter's data code as the
    ///     sub-code, by its panel's name (M820.E.OC1) - at once, with the axes'
    ///     switch, and SETs its error bit like an axis.
    ///
    /// The PLC's own bit for the same device stays off the list while the HMI's
    /// alarm for it is on show: one fault, one line.
    /// </summary>
    public static class AlarmGeneration
    {
        // ---- settings, per machine -------------------------------------------------

        public const double DefaultDelaySeconds = 5.0;
        /// <summary>The shortest delay used: a scan is 100 ms.</summary>
        public const double MinimumDelaySeconds = 0.3;
        /// <summary>A position reading unchanged this long after it moved has arrived.</summary>
        public const int SettleMs = 400;

        public sealed class Settings
        {
            /// <summary>The cylinder alarms (timed movements): off unless switched on.</summary>
            public bool Enabled;
            /// <summary>The cylinder movements' delay.</summary>
            public double DelaySeconds = DefaultDelaySeconds;
            /// <summary>The axis alarms (an Axis Alarm / Servo Alarm code, at once): on unless switched off.</summary>
            public bool AxisAlarms = true;
        }

        /// <summary>Where the settings are kept (a test sets its own).</summary>
        public static string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "AlarmGeneration.cfg");

        private static Dictionary<string, Settings> _settings;
        private static string _settingsReadFrom;

        private static void LoadSettings()
        {
            if (_settings != null && _settingsReadFrom == SettingsPath) return;
            _settings = new Dictionary<string, Settings>(StringComparer.OrdinalIgnoreCase);
            _settingsReadFrom = SettingsPath;
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var raw in File.ReadAllLines(SettingsPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('='), dot = line.LastIndexOf('.', eq < 0 ? line.Length - 1 : eq);
                    if (eq <= 0 || dot <= 0) continue;
                    var module = line.Substring(0, dot).Trim();
                    var key = line.Substring(dot + 1, eq - dot - 1).Trim();
                    var value = line.Substring(eq + 1).Trim();
                    Settings s;
                    if (!_settings.TryGetValue(module, out s)) _settings[module] = s = new Settings();
                    if (key.Equals("Enabled", StringComparison.OrdinalIgnoreCase)) s.Enabled = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    else if (key.Equals("AxisAlarms", StringComparison.OrdinalIgnoreCase)) s.AxisAlarms = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    else if (key.Equals("DelaySeconds", StringComparison.OrdinalIgnoreCase))
                    {
                        double d;
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && d > 0) s.DelaySeconds = d;
                    }
                }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the alarm generation settings: " + ex.Message); }
        }

        /// <summary>A machine's settings (a copy; the defaults when none were saved).</summary>
        public static Settings For(string moduleId)
        {
            LoadSettings();
            Settings s;
            return _settings.TryGetValue(moduleId ?? "", out s) ? new Settings { Enabled = s.Enabled, DelaySeconds = s.DelaySeconds, AxisAlarms = s.AxisAlarms } : new Settings();
        }

        /// <summary>The cylinder alarms switched on for a machine.</summary>
        public static bool EnabledFor(string moduleId) { return Peek(moduleId).Enabled; }

        /// <summary>The axis alarms on for a machine (unless switched off).</summary>
        public static bool AxisAlarmsFor(string moduleId) { return Peek(moduleId).AxisAlarms; }

        private static readonly Settings _defaults = new Settings();

        /// <summary>The stored settings themselves (not a copy): asked on every scan.</summary>
        private static Settings Peek(string moduleId)
        {
            LoadSettings();
            Settings s;
            return _settings.TryGetValue(moduleId ?? "", out s) ? s : _defaults;
        }

        /// <summary>Keeps a machine's settings; switched off, the alarms it raised go.</summary>
        public static void Save(string moduleId, Settings settings)
        {
            LoadSettings();
            _settings[moduleId ?? ""] = new Settings { Enabled = settings.Enabled, DelaySeconds = Math.Max(MinimumDelaySeconds, settings.DelaySeconds),
                                                       AxisAlarms = settings.AxisAlarms };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                var lines = new List<string> { "# Kaban HMI: the alarms the HMI raises itself, per machine: <machine>.Enabled=1 (cylinders), " +
                                               "<machine>.DelaySeconds=5 (cylinders), <machine>.AxisAlarms=1 (axes, at once)" };
                foreach (var kv in _settings.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    lines.Add(kv.Key + ".Enabled=" + (kv.Value.Enabled ? "1" : "0"));
                    lines.Add(kv.Key + ".DelaySeconds=" + kv.Value.DelaySeconds.ToString(CultureInfo.InvariantCulture));
                    lines.Add(kv.Key + ".AxisAlarms=" + (kv.Value.AxisAlarms ? "1" : "0"));
                }
                SafeFile.WriteAllText(SettingsPath, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(false));
            }
            catch (Exception ex) { AuditLogger.Log("could not save the alarm generation settings: " + ex.Message); }
            ClearRaised(moduleId, !settings.Enabled, !settings.AxisAlarms);
            OnWatchesChanged();
        }

        // ---- the HMI's records of a machine (5 November) ------------------------------
        //
        // "So we can create triggers to take logs for each axis unit or other pneumatic controls as well": the
        // watches above see every movement, code and fault - the HMI's records of a machine (KabanHMI's
        // MachineRecords, while HMI Settings > Options has "Machine details from the HMI" on) are told of them.
        // While a machine's records are taken its cylinders are read and timed even with the HMI's cylinder
        // alarms off (FA2010: the PLC raises them) - nothing is raised then, only told.

        /// <summary>What the HMI's records hear from the watches (KabanHMI's MachineRecords).</summary>
        public interface IRecordsObserver
        {
            /// <summary>The HMI takes its records of this machine now.</summary>
            bool On(string moduleId);
            /// <summary>A movement confirmed by its feedback, <paramref name="seconds"/> after it was commanded.</summary>
            void MovementDone(Cylinder c, CylinderSide s, double seconds, DateTime now);
            /// <summary>A movement not confirmed within its delay (<paramref name="sub"/>: which signal failed, as the alarm's sub-code).</summary>
            void MovementTooLong(Cylinder c, CylinderSide s, double seconds, int sub, DateTime now);
            /// <summary>A station's scan evaluated: its axes and inverters as they are now.</summary>
            void Scanned(int station, Func<string, int> lookup, DateTime now);
            /// <summary>The devices the records read themselves (their triggers and values: LogRecordHMI), per station.</summary>
            IEnumerable<KeyValuePair<int, string>> Addresses();
        }

        /// <summary>The HMI's records (null: none - a test, the designer).</summary>
        public static IRecordsObserver Records;

        /// <summary>
        /// A station's values are fresh this scan (the scanner's; null: every station is). A movement with a device of another
        /// station (a sensor X12@2 of a cylinder of PLC 1) is not timed while that station does not answer - 5 November: PLC 2
        /// switched off on a panel, every movement of such a cylinder was "not confirmed".
        /// </summary>
        public static Func<int, bool> StationFresh;

        /// <summary>Every station a movement's devices are on is fresh now.</summary>
        private static bool Fresh(Cylinder c, CylinderSide s)
        {
            var f = StationFresh;
            if (f == null) return true;
            if (s.Stations == null)
            {
                var list = new List<int>();
                foreach (var a in new[] { s.Drive, s.OtherDrive, s.Position }.Concat(s.MustOn.Select(x => x.Address)).Concat(s.MustOff.Select(x => x.Address)))
                {
                    if (string.IsNullOrWhiteSpace(a)) continue;
                    int st = PlcAddressHelper.StationOf(c.Station, a.Trim());
                    if (st != c.Station && !list.Contains(st)) list.Add(st);
                }
                s.Stations = list.ToArray();
            }
            foreach (var st in s.Stations)
                try { if (!f(st)) return false; } catch { return false; }
            return true;
        }

        /// <summary>The HMI takes its records of the machine now.</summary>
        public static bool RecordsOn(string moduleId)
        {
            var r = Records;
            if (r == null) return false;
            try { return r.On(moduleId); } catch { return false; }
        }

        /// <summary>The records switched on or off (HMI Settings > Options): the scanner reads the devices they need, or no longer.</summary>
        public static void RecordsChanged() { OnWatchesChanged(); }

        /// <summary>Tells the records - never stopping the scan.</summary>
        private static void Tell(Action<IRecordsObserver> what)
        {
            var r = Records;
            if (r == null) return;
            try { what(r); }
            catch (Exception ex) { AuditLogger.Log("machine records: " + ex.Message); }
        }

        // ---- what is watched -----------------------------------------------------------

        /// <summary>A sensor of a movement.</summary>
        public sealed class Signal
        {
            public string Address = "";
            /// <summary>As the texts name it: "SN59 (X51)", or the address.</summary>
            public string Name = "";
        }

        /// <summary>One movement of a cylinder unit.</summary>
        public sealed class CylinderSide
        {
            /// <summary>"A" (left button) or "B" (right button).</summary>
            public string Side = "A";
            /// <summary>The movement in words ("Clamp").</summary>
            public string Name = "";
            /// <summary>The device naming its alarm: its error bit, else its command bit.</summary>
            public string Code = "";
            public string ErrorAddress = "";
            /// <summary>The output whose state commands this movement (or the command bit when no output is known).</summary>
            public string Drive = "";
            /// <summary>False: a spring return - commanded by Drive (the coil's output) going OFF.</summary>
            public bool DriveOn = true;
            /// <summary>A double valve's other coil output: both ON cannot switch the valve.</summary>
            public string OtherDrive = "";
            /// <summary>A 5/2 double valve keeps the movement going after a short pulse.</summary>
            public bool KeepsAfterDrop;
            /// <summary>Must be ON at the end (the target sensor, a clamp's pressure switch).</summary>
            public readonly List<Signal> MustOn = new List<Signal>();
            /// <summary>Must be OFF at the end (the start sensor).</summary>
            public readonly List<Signal> MustOff = new List<Signal>();
            /// <summary>Confirmed by the position reading moving and settling (an analogue sensor, an encoder away from its reference).</summary>
            public bool ByPosition;
            public string Position = "";
            /// <summary>The movement's devices for its help page (states filled from the alarm's snapshot).</summary>
            public TroubleshootingCatalog.MovementInfo Movement;

            // ---- while running ----
            /// <summary>The other stations its devices are on (null: not looked at yet).</summary>
            internal int[] Stations;
            internal bool Seen, Commanded;
            internal DateTime? Since;
            internal int? PositionStart, PositionLast;
            internal DateTime PositionChanged;
            internal bool Moved;
            /// <summary>The alarm this movement raised, while it is on the list.</summary>
            public AlarmItem Raised { get; internal set; }

            /// <summary>Monitoring is running (tests).</summary>
            public bool Timing { get { return Since.HasValue; } }
        }

        public sealed class Cylinder
        {
            public int Station = 1;
            public string ModuleId = "";
            public string Unit = "";
            public TroubleshootingCatalog.ValveType Valve;
            public TroubleshootingCatalog.FeedbackType Feedback;
            public readonly List<CylinderSide> Sides = new List<CylinderSide>();
            /// <summary>
            /// Its position is judged only while these machine conditions are met (a SystemCondition
            /// set): the machine has its measuring device (an option), and the device reads THIS
            /// cylinder - FA2010's profile recognition encoders are switched between the left and the
            /// right unit (30 October). A movement started while not met is not timed by its position.
            /// </summary>
            public long PositionWhile;
            /// <summary>
            /// A valve an option changes (30 October: FA2010's Cutting Left Front clamp is a 5/3 open centre with
            /// "Cutting Left Front with Air Exhaust", L35): OptionValve while OptionValveWhile (a SystemCondition set)
            /// is met - MachineConditionMap.MetNow, the PLC's values read last.
            /// </summary>
            public TroubleshootingCatalog.ValveType OptionValve;
            public long OptionValveWhile;

            /// <summary>The valve fitted now.</summary>
            public TroubleshootingCatalog.ValveType ValveNow
            {
                get
                {
                    return OptionValve != 0 && OptionValveWhile != 0 && MachineConditionMap.MetNow((SystemCondition)OptionValveWhile)
                        ? OptionValve : Valve;
                }
            }

            internal string Key
            {
                get { return Station + "|" + string.Join("|", Sides.Select(s => s.Side + ":" + s.Code + ":" + s.Drive + ":" + s.DriveOn)); }
            }
        }

        public sealed class Axis
        {
            public int Station = 1;
            public string ModuleId = "";
            public string Name = "";
            public int Number;
            public string ErrorAddress = "", ServoAlarm = "", AxisAlarm = "";
            internal int LastServo, LastAxis;
            public AlarmItem RaisedServo { get; internal set; }
            public AlarmItem RaisedAxis { get; internal set; }
            /// <summary>The device the alarm is named by: the error bit, or "Axis 3".</summary>
            public string Code { get { return !string.IsNullOrWhiteSpace(ErrorAddress) ? ErrorAddress.Trim() : "Axis " + Number; } }

            // ---- what it was doing when a code came (16 October, AxisMoment) ----------
            /// <summary>Its name as the drawings would have it (English: "Robot X") - to find its reference sensor there.</summary>
            public string DrawingName = "";
            /// <summary>The live position (two words when PositionDouble) and the limits, in the count the Divisor divides.</summary>
            public string Position = "", MinLimit = "", MaxLimit = "";
            public bool PositionDouble = true, LimitsDouble = true;
            public double Divisor = 1;
            public int Decimals = 2;
            public string Suffix = "";
            /// <summary>The jog commands (held while the button is), the reference sensor and its side, the PLC's home request.</summary>
            public string JogBack = "", JogForward = "", RefSensor = "", HomeRequest = "";
            /// <summary>The PLC's homing in progress: the row's "go to reference" bit, ON until the homing is complete (FA2010 B0...).</summary>
            public string Homing = "";
            public bool RefForward, Vertical;
            /// <summary>The motion module's operation status (Md.26) and BUSY (Md.141, one bit per axis).</summary>
            public string Status = "", Busy = "";
            /// <summary>
            /// The motion module's own software stroke limits (Pr.12 upper, Pr.13 lower: two words each)
            /// and its unit setting (Pr.1) - the limits the module stops the axis at (19 October: the
            /// Axis page's D312 / D314 are 0 on FA2010).
            /// </summary>
            public string SoftUpper = "", SoftLower = "", UnitWord = "";
            public int BusyBit = -1;
            /// <summary>The machine's "running" and "manual mode" conditions (on this axis's station only).</summary>
            public ConditionRule Running, ManualMode;
            /// <summary>
            /// The motion module's loads, in % of the rated (5 November, the HMI's records of a new machine - read only for them):
            /// Md.110 effective load torque, Md.111 peak torque ratio, Md.109 regenerative load ratio.
            /// </summary>
            public string EffectiveLoad = "", PeakTorque = "", RegenerativeLoad = "";

            /// <summary>The moment of the code shown now (kept while the code is: acknowledged and back, it is the same moment).</summary>
            public AxisMoment ServoMoment { get; internal set; }
            public AxisMoment AxisMoment { get; internal set; }

            internal Reading Last;
            /// <summary>Its alarms are not raised now (the axis alarms switched off).</summary>
            internal bool Quiet;
            internal DateTime? MoveStart, LastChange;
            internal long? StartRaw, LastRaw;
            internal bool Moved;
            internal int Direction;

            /// <summary>True when anything of its movement is watched.</summary>
            public bool WatchesMotion
            {
                get
                {
                    return new[] { Position, JogBack, JogForward, RefSensor, HomeRequest, Homing, Status, Busy }.Any(a => !string.IsNullOrWhiteSpace(a)) ||
                           Running != null || ManualMode != null;
                }
            }
        }

        /// <summary>One scan's reading of an axis's movement devices (null: not read).</summary>
        internal sealed class Reading
        {
            public int? Status;
            public bool? Busy, JogBack, JogForward, Running, ManualMode, HomeRequest, Ref, Homing;
            public long? Position, Min, Max, SoftUpper, SoftLower;
            public int? Unit;
            /// <summary>The motion module's word when it is read (a jog command it did not act on is no movement); else one jog command.</summary>
            public bool Moving
            {
                get
                {
                    if (Status.HasValue || Busy.HasValue) return Busy == true || (Status.HasValue && AxisMoment.IsMoving(Status.Value));
                    return AxisMoment.OneJog(JogBack, JogForward);
                }
            }
        }

        /// <summary>
        /// How long an axis that moved must have stood still before its code for the help
        /// to call it stuck (it ran into something). Replaceable for the tests.
        /// </summary>
        public static int StuckMs = 500;

        /// <summary>An inverter: its fault's data code word, and the alarm bit the HMI sets for it.</summary>
        public sealed class Inverter
        {
            public int Station = 1;
            public string ModuleId = "";
            /// <summary>The unit as its page names it ("Saw Motor").</summary>
            public string Name = "";
            /// <summary>The word the PLC copies the inverter's fault into: its data code (FR-A800 6.4), 0 = no fault.</summary>
            public string CodeAddress = "";
            /// <summary>The alarm bit SET in the PLC when the fault comes (optional).</summary>
            public string ErrorAddress = "";
            internal int Last;
            public AlarmItem Raised { get; internal set; }
            /// <summary>The data code of the fault on show (0: none).</summary>
            public int Code { get { return Last; } }
            /// <summary>The device the alarm is named by: the error bit, or the code word.</summary>
            public string Device { get { return !string.IsNullOrWhiteSpace(ErrorAddress) ? ErrorAddress.Trim() : (CodeAddress ?? "").Trim(); } }

            // ---- read only for the HMI's records of a new machine (5 November) ----
            /// <summary>The word of its output current (the PLC reads it from the inverter), divided by <see cref="CurrentDivisor"/> to amperes.</summary>
            public string Current = "";
            public double CurrentDivisor = 100;
            /// <summary>It runs (its RUN output, or the PLC's bit for it).</summary>
            public string Running = "";
            /// <summary>ON while it does its work (the saw's feed): its load measured then - else while it runs.</summary>
            public string LoadWhile = "";
            /// <summary>Its value as its unit shows it (a speed, a frequency): the word, what divides it, its decimals and unit.</summary>
            public string Value = "";
            public double ValueDivisor = 1;
            public int ValueDecimals;
            public string ValueSuffix = "";
        }

        private static readonly List<Cylinder> _cylinders = new List<Cylinder>();
        private static readonly List<Axis> _axes = new List<Axis>();
        private static readonly List<Inverter> _inverters = new List<Inverter>();

        /// <summary>Raised when what is read changes (a watch added, a machine switched on or off): the scanner re-reads its list.</summary>
        public static event EventHandler WatchesChanged;

        private static void OnWatchesChanged()
        {
            var h = WatchesChanged;
            if (h != null) h(null, EventArgs.Empty);
        }

        public static IReadOnlyList<Cylinder> Cylinders { get { return _cylinders; } }
        public static IReadOnlyList<Axis> Axes { get { return _axes; } }
        public static IReadOnlyList<Inverter> Inverters { get { return _inverters; } }

        /// <summary>
        /// The machine a station belongs to (the module that mapped it), so a unit
        /// found on any page is switched on and off with its own machine.
        /// </summary>
        public static string ModuleOfStation(int station)
        {
            try
            {
                foreach (var m in Kaban.Core.Machines.MachineRegistry.Modules)
                    if (m.Context != null && m.Context.MappedStations.Any(kv => kv.Value == station)) return m.Context.ModuleId ?? "";
                var main = Kaban.Core.Machines.MachineRegistry.Main;
                return main != null && main.Context != null ? main.Context.ModuleId ?? "" : "";
            }
            catch { return ""; }
        }

        /// <summary>Adds a unit's movements (the same unit on two pages is added once).</summary>
        public static void Register(Cylinder c)
        {
            if (c == null || c.Sides.Count == 0) return;
            if (string.IsNullOrEmpty(c.ModuleId)) c.ModuleId = ModuleOfStation(c.Station);
            HookSuppression();
            var key = c.Key;
            if (_cylinders.Any(x => x.Key == key)) return;
            _cylinders.Add(c);
            OnWatchesChanged();
        }

        public static void Register(Axis a)
        {
            if (a == null || (string.IsNullOrWhiteSpace(a.ServoAlarm) && string.IsNullOrWhiteSpace(a.AxisAlarm))) return;
            if (string.IsNullOrEmpty(a.ModuleId)) a.ModuleId = ModuleOfStation(a.Station);
            HookSuppression();
            if (_axes.Any(x => x.Station == a.Station && string.Equals(x.ServoAlarm, a.ServoAlarm, StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(x.AxisAlarm, a.AxisAlarm, StringComparison.OrdinalIgnoreCase))) return;
            _axes.Add(a);
            OnWatchesChanged();
        }

        /// <summary>Adds an inverter's fault word (the same word on two pages is added once).</summary>
        public static void Register(Inverter v)
        {
            if (v == null || string.IsNullOrWhiteSpace(v.CodeAddress)) return;
            if (string.IsNullOrEmpty(v.ModuleId)) v.ModuleId = ModuleOfStation(v.Station);
            HookSuppression();
            if (_inverters.Any(x => x.Station == v.Station && string.Equals(x.CodeAddress.Trim(), v.CodeAddress.Trim(), StringComparison.OrdinalIgnoreCase))) return;
            _inverters.Add(v);
            OnWatchesChanged();
        }

        /// <summary>Forgets the watches (a new discovery) and takes the alarms they raised off the list - nothing could clear them any more.</summary>
        public static void Clear()
        {
            foreach (var c in _cylinders) foreach (var s in c.Sides) Drop(s);
            foreach (var x in _axes) DropAxis(x);
            foreach (var v in _inverters) DropInverter(v);
            _cylinders.Clear();
            _axes.Clear();
            _inverters.Clear();
            OnWatchesChanged();
        }

        /// <summary>The devices to read, per station - for the machines switched on only.</summary>
        public static IEnumerable<KeyValuePair<int, string>> Addresses()
        {
            foreach (var c in _cylinders)
            {
                if (!EnabledFor(c.ModuleId) && !RecordsOn(c.ModuleId)) continue;
                foreach (var s in c.Sides)
                {
                    foreach (var a in new[] { s.Drive, s.OtherDrive, s.Position }.Concat(s.MustOn.Select(x => x.Address)).Concat(s.MustOff.Select(x => x.Address)))
                        if (!string.IsNullOrWhiteSpace(a)) yield return new KeyValuePair<int, string>(c.Station, a.Trim());
                }
                foreach (var rule in PositionRules(c))
                    foreach (var a in MachineConditionMap.AddressesOf(rule))      // every device of a condition of several, one compared with another
                        yield return new KeyValuePair<int, string>(c.Station, a);
            }
            foreach (var x in _axes)
            {
                // read with the axis alarms off too: the code's help still tells what the axis was doing (17 October)
                if (!string.IsNullOrWhiteSpace(x.ServoAlarm)) yield return new KeyValuePair<int, string>(x.Station, x.ServoAlarm.Trim());
                if (!string.IsNullOrWhiteSpace(x.AxisAlarm)) yield return new KeyValuePair<int, string>(x.Station, x.AxisAlarm.Trim());
                // what it is doing, for the help of a code when it comes (16 October)
                foreach (var a in MotionAddresses(x)) yield return new KeyValuePair<int, string>(x.Station, a);
                // its loads, for the HMI's records of a new machine (5 November)
                if (RecordsOn(x.ModuleId))
                    foreach (var a in new[] { x.EffectiveLoad, x.PeakTorque, x.RegenerativeLoad })
                        if (!string.IsNullOrWhiteSpace(a)) yield return new KeyValuePair<int, string>(x.Station, a.Trim());
            }
            // the records' own triggers and values (LogRecordHMI, 5 November)
            var rec = Records;
            if (rec != null)
            {
                List<KeyValuePair<int, string>> own;
                try { own = rec.Addresses().ToList(); }
                catch (Exception ex) { AuditLogger.Log("machine records: " + ex.Message); own = new List<KeyValuePair<int, string>>(); }
                foreach (var a in own) yield return a;
            }
            foreach (var v in _inverters)
            {
                bool records = RecordsOn(v.ModuleId);
                if (AxisAlarmsFor(v.ModuleId) || records) yield return new KeyValuePair<int, string>(v.Station, v.CodeAddress.Trim());
                // its current, its run, its work and its value, for the HMI's records of a new machine (5 November)
                if (records)
                    foreach (var a in new[] { v.Current, v.Running, v.LoadWhile, v.Value })
                        if (!string.IsNullOrWhiteSpace(a)) yield return new KeyValuePair<int, string>(v.Station, a.Trim());
            }
        }

        /// <summary>The devices an axis's moment is read from (each word of a 32-bit value).</summary>
        public static IEnumerable<string> MotionAddresses(Axis x)
        {
            var list = new List<string>();
            Action<string, bool> add = (a, dbl) =>
            {
                if (string.IsNullOrWhiteSpace(a)) return;
                list.Add(a.Trim());
                if (dbl) { var next = NextWord(a.Trim()); if (next != null) list.Add(next); }
            };
            add(x.Position, x.PositionDouble);
            add(x.MinLimit, x.LimitsDouble);
            add(x.MaxLimit, x.LimitsDouble);
            add(x.JogBack, false);
            add(x.JogForward, false);
            add(x.RefSensor, false);
            add(x.HomeRequest, false);
            add(x.Homing, false);
            add(x.Status, false);
            add(x.Busy, false);
            add(x.SoftUpper, true);
            add(x.SoftLower, true);
            add(x.UnitWord, false);
            foreach (var r in new[] { x.Running, x.ManualMode })
                if (r != null && r.PlcNumber == x.Station)
                    foreach (var d in r.Devices)
                    {
                        add(d.Address, d.DataType == DataTypeEnum.Bin_32);
                        if (d.ComparesDevices) add(d.CompareAddress, d.DataType == DataTypeEnum.Bin_32);
                    }
            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string NextWord(string address)
        {
            try { return SimpleMotion.IsModuleAddress(address) ? SimpleMotion.OffsetModuleAddress(address, 1) : PlcAddressHelper.GetNextAddress(address); }
            catch { return null; }
        }

        // ---- a unit's movements, from what its help knows of them ----------------------

        /// <summary>
        /// The watch for a cylinder unit, from its two movements as its help sees
        /// them (TroubleshootingCatalog.MovementInfo: the command, the output it
        /// drives, the other coil, the sensors of each end) and its valve and
        /// feedback (the unit's numbers). Null when nothing confirms a movement
        /// (no sensor) or nothing commands one.
        ///
        ///   valve 1 (5/2 double): each movement by its own coil output going ON; it
        ///     goes on after a short pulse, until the other coil reverses it.
        ///   valve 2 (5/2 single): the coil's movement by the output going ON, the
        ///     spring's by it going OFF; either is given up when the output changes back.
        ///   valve 3 (5/3): each by its own coil output, given up when it drops.
        ///   unspecified: each commanded movement by its output, given up when it drops.
        ///
        /// With no output known (no OutputAddress, nothing on the drawings) the
        /// command bit stands in for it - then only the buttons' movements are timed.
        /// </summary>
        public static Cylinder Build(int station, string moduleId, string unit, int valve, int feedback,
                                     TroubleshootingCatalog.MovementInfo a, TroubleshootingCatalog.MovementInfo b)
        {
            valve = TroubleshootingCatalog.CanonicalValve(valve);
            if (feedback == 7) return null;                                      // no sensor: nothing confirms
            var c = new Cylinder
            {
                Station = station, ModuleId = moduleId ?? "", Unit = unit ?? "",
                Valve = (TroubleshootingCatalog.ValveType)valve, Feedback = (TroubleshootingCatalog.FeedbackType)feedback
            };
            foreach (var mv in new[] { a, b })
            {
                if (mv == null) continue;
                var side = SideFrom(c, mv, valve, feedback);
                if (side != null) c.Sides.Add(side);
            }
            return c.Sides.Count == 0 ? null : c;
        }

        private static string Addr(TroubleshootingCatalog.MovementInfo.Device d)
        {
            return d == null || string.IsNullOrWhiteSpace(d.Address) ? "" : d.Address.Trim();
        }

        private static CylinderSide SideFrom(Cylinder c, TroubleshootingCatalog.MovementInfo mv, int valve, int feedback)
        {
            string output = Addr(mv.Output), otherOutput = Addr(mv.OtherOutput), cmd = Addr(mv.Command), otherCmd = Addr(mv.OtherCommand);
            bool single = valve == (int)TroubleshootingCatalog.ValveType.SingleSolenoidSpring;
            bool own = output.Length > 0 || cmd.Length > 0;
            var s = new CylinderSide { Side = string.IsNullOrEmpty(mv.Side) ? "A" : mv.Side, Movement = mv };
            if (own)
            {
                s.Drive = output.Length > 0 ? output : cmd;
                s.DriveOn = true;
            }
            else if (single && (otherOutput.Length > 0 || otherCmd.Length > 0))
            {
                // the spring's movement: the coil's output going OFF
                s.Drive = otherOutput.Length > 0 ? otherOutput : otherCmd;
                s.DriveOn = false;
            }
            else return null;

            if (!single && own)
            {
                var other = otherOutput.Length > 0 ? otherOutput : otherCmd;
                if (other.Length > 0 && !string.Equals(other, s.Drive, StringComparison.OrdinalIgnoreCase)) s.OtherDrive = other;
            }
            s.KeepsAfterDrop = valve == (int)TroubleshootingCatalog.ValveType.DoubleSolenoid;

            Func<TroubleshootingCatalog.MovementInfo.Device, Signal> sig = d =>
            {
                var p = WiringTable.Find(c.Station, d.Address);
                return new Signal
                {
                    Address = d.Address.Trim(),
                    // "X12 (PLC 2)" for a device of the other controller
                    Name = p != null && p.Tag.Length > 0
                        ? p.Tag + " (" + PlcAddressHelper.Describe(c.Station, PlcAddressHelper.WithStation(p.Address, p.Station, c.Station)) + ")"
                        : PlcAddressHelper.Describe(c.Station, d.Address.Trim())
                };
            };
            foreach (var d in mv.Feedback) if (!string.IsNullOrWhiteSpace(d.Address)) s.MustOn.Add(sig(d));
            foreach (var d in mv.OppositeFeedback) if (!string.IsNullOrWhiteSpace(d.Address)) s.MustOff.Add(sig(d));
            s.Position = (mv.Position ?? "").Trim();
            bool positional = feedback == (int)TroubleshootingCatalog.FeedbackType.AnalogueMagnetic ||
                              feedback == (int)TroubleshootingCatalog.FeedbackType.EncoderReference ||
                              (feedback == 0 && s.Position.Length > 0);
            // A position reading confirms the movement no sensor of its own sees: an
            // analogue sensor's both, an encoder's away from its reference.
            s.ByPosition = positional && s.Position.Length > 0 && s.MustOn.Count == 0;
            if (!s.ByPosition && s.MustOn.Count == 0 && s.MustOff.Count == 0) return null;

            s.ErrorAddress = (mv.Error ?? "").Trim();
            s.Code = s.ErrorAddress.Length > 0 ? s.ErrorAddress : cmd.Length > 0 ? cmd : s.Drive;
            try { s.Name = TroubleshootingCatalog.MovementName(mv); } catch { s.Name = mv.Name ?? ""; }
            return s;
        }

        // ---- the sub-codes ------------------------------------------------------------------

        public const int StartStayedOn = 1, TargetNotOn = 2, StartStuck = 3, BothCoils = 4, PositionUnchanged = 5, PositionMoving = 6;

        /// <summary>What a sub-code means for a movement, in words (the alarm's detail).</summary>
        public static string Describe(int sub, CylinderSide s)
        {
            Func<IEnumerable<Signal>, string> names = list => string.Join(" / ", list.Select(x => x.Name.Length > 0 ? x.Name : x.Address));
            switch (sub)
            {
                case StartStayedOn:
                    return string.Format(LanguageManager.GetString("Gen_Sub1", "{0} stayed ON - the cylinder did not leave its start"), names(s.MustOff));
                case TargetNotOn:
                    return string.Format(LanguageManager.GetString("Gen_Sub2", "{0} did not come ON - it stopped on the way, or the sensor does not see it"), names(s.MustOn));
                case StartStuck:
                    return string.Format(LanguageManager.GetString("Gen_Sub3", "{0} came ON, but {1} at the start stayed ON"), names(s.MustOn), names(s.MustOff));
                case BothCoils:
                    return string.Format(LanguageManager.GetString("Gen_Sub4", "both coils were ON ({0} and {1})"), s.Drive, s.OtherDrive);
                case PositionUnchanged:
                    return string.Format(LanguageManager.GetString("Gen_Sub5", "the position ({0}) did not change"), s.Position);
                case PositionMoving:
                    return string.Format(LanguageManager.GetString("Gen_Sub6", "the position ({0}) was still changing"), s.Position);
                default:
                    return "";
            }
        }

        // ---- evaluation --------------------------------------------------------------------

        /// <summary>
        /// One scan of a station: every watched movement and axis on it, with the
        /// station's fresh values. Called by the page scanner after the PLC alarms.
        /// </summary>
        public static void Evaluate(int station, Func<string, int> lookup, DateTime now)
        {
            if (lookup == null) return;
            foreach (var c in _cylinders)
            {
                if (c.Station != station) continue;
                var settings = Peek(c.ModuleId);
                // timed for the HMI's alarms, or for its records only (nothing raised then)
                bool alarms = settings.Enabled;
                if (!alarms && !RecordsOn(c.ModuleId)) { foreach (var s in c.Sides) { s.Since = null; s.Seen = false; Drop(s); } continue; }
                if (!alarms) foreach (var s in c.Sides) Drop(s);
                foreach (var s in c.Sides) EvaluateSide(c, s, settings, lookup, now, alarms);
            }
            foreach (var x in _axes)
            {
                if (x.Station != station) continue;
                // the axis alarms off: none raised, the moments still taken for the codes' help (17 October)
                bool raise = AxisAlarmsFor(x.ModuleId);
                if (!raise)
                {
                    if (x.RaisedServo != null) { AlarmManager.ClearGenerated(x.RaisedServo); x.RaisedServo = null; }
                    if (x.RaisedAxis != null) { AlarmManager.ClearGenerated(x.RaisedAxis); x.RaisedAxis = null; }
                    x.Quiet = true;
                }
                else if (x.Quiet) { x.Quiet = false; x.LastServo = x.LastAxis = 0; }   // switched on: the codes shown now raised
                EvaluateAxis(x, lookup, now, raise);
            }
            foreach (var v in _inverters)
            {
                if (v.Station != station) continue;
                if (!AxisAlarmsFor(v.ModuleId)) { DropInverter(v); continue; }
                EvaluateInverter(v, lookup, now);
            }
            // the HMI's records: the station's axes and inverters as they are now
            if (Records != null) Tell(r => r.Scanned(station, lookup, now));
        }

        /// <summary>A word as a signed number (null: no address, or not read).</summary>
        public static int? SignedWord(Func<string, int> lookup, string address)
        {
            if (lookup == null || string.IsNullOrWhiteSpace(address)) return null;
            try { return (short)(lookup(address.Trim()) & 0xFFFF); } catch { return null; }
        }

        /// <summary>An axis's position now, in its unit (its Divisor), or null when it is not watched.</summary>
        public static double? PositionOf(Axis x, Func<string, int> lookup)
        {
            if (x == null || lookup == null || string.IsNullOrWhiteSpace(x.Position)) return null;
            try
            {
                int lo = lookup(x.Position.Trim());
                long raw = x.PositionDouble ? (int)(((uint)(lookup(NextWord(x.Position.Trim())) & 0xFFFF) << 16) | (uint)(lo & 0xFFFF)) : (short)lo;
                return raw / (x.Divisor > 0 ? x.Divisor : 1);
            }
            catch { return null; }
        }

        private static bool On(Func<string, int> lookup, string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return false;
            try { return lookup(address.Trim()) != 0; } catch { return false; }
        }

        private static int Word(Func<string, int> lookup, string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return 0;
            try { return lookup(address.Trim()); } catch { return 0; }
        }

        /// <summary>The end of the movement as its sensors confirm it (not for one confirmed by position).</summary>
        private static bool SensorsConfirm(CylinderSide s, Func<string, int> lookup)
        {
            if (s.MustOn.Count == 0 && s.MustOff.Count == 0) return false;
            return s.MustOn.All(x => On(lookup, x.Address)) && s.MustOff.All(x => !On(lookup, x.Address));
        }

        /// <summary>
        /// How long a movement may take before the HMI raises its alarm: its error's alarm's own Cylinder delay
        /// (HMI Settings > Alarms, the Cylinder delay column) when one is entered, else its machine's Cylinder alarm
        /// delay (23 October: Cylinder FW/Back changes direction just before it).
        /// </summary>
        public static double MovementDelay(int station, string errorAddress)
        {
            var settings = Peek(ModuleOfStation(station));
            int st = station; string device = errorAddress;
            if (!string.IsNullOrWhiteSpace(errorAddress)) PlcAddressHelper.Resolve(station, errorAddress.Trim(), out st, out device);
            var def = string.IsNullOrWhiteSpace(device) ? null : AlarmManager.FindByStationAndAddress(st, device);
            double d = def != null && def.GenerationDelay.HasValue && def.GenerationDelay.Value > 0 ? def.GenerationDelay.Value : settings.DelaySeconds;
            return Math.Max(MinimumDelaySeconds, d);
        }

        private static double DelayOf(Cylinder c, CylinderSide s, Settings settings)
        {
            var def = string.IsNullOrWhiteSpace(s.ErrorAddress) ? null : AlarmManager.FindByStationAndAddress(c.Station, s.ErrorAddress);
            double d = def != null && def.GenerationDelay.HasValue && def.GenerationDelay.Value > 0 ? def.GenerationDelay.Value : settings.DelaySeconds;
            return Math.Max(MinimumDelaySeconds, d);
        }

        private static void Drop(CylinderSide s)
        {
            if (s.Raised != null) { AlarmManager.ClearGenerated(s.Raised); s.Raised = null; }
        }

        private static void EvaluateSide(Cylinder c, CylinderSide s, Settings settings, Func<string, int> lookup, DateTime now, bool alarms = true)
        {
            if (string.IsNullOrWhiteSpace(s.Drive)) return;
            // a device of a station that does not answer now: not judged - and timed again only from a fresh command
            if (!Fresh(c, s)) { s.Since = null; s.Seen = false; return; }
            bool drive = On(lookup, s.Drive);
            bool commanded = s.DriveOn ? drive : !drive;
            // NOT FROM THE FIRST SCAN. What is commanded when the HMI starts (or the
            // generation is switched on) was commanded before it watched - a spring
            // valve's return all the time - and timing it from here would be wrong.
            bool rising = s.Seen && commanded && !s.Commanded;
            s.Seen = true;
            s.Commanded = commanded;

            // An alarm acknowledged on the list is the operator's: not raised again for it.
            if (s.Raised != null && !AlarmManager.IsOnList(s.Raised)) s.Raised = null;

            if (rising)
            {
                // by its position only while the position is this cylinder's
                s.Since = s.ByPosition && !PositionReadable(c, lookup) ? (DateTime?)null : now;
                s.Moved = false;
                s.PositionStart = s.PositionLast = s.ByPosition ? Word(lookup, s.Position) : (int?)null;
                s.PositionChanged = now;
            }

            bool confirmed;
            if (s.ByPosition)
            {
                if (s.Since.HasValue)
                {
                    int v = Word(lookup, s.Position);
                    if (s.PositionLast != v) { s.PositionLast = v; s.PositionChanged = now; if (s.PositionStart != v) s.Moved = true; }
                }
                confirmed = s.Since.HasValue && s.Moved && (now - s.PositionChanged).TotalMilliseconds >= SettleMs &&
                            s.MustOn.All(x => On(lookup, x.Address)) && s.MustOff.All(x => !On(lookup, x.Address));
            }
            else confirmed = SensorsConfirm(s, lookup);

            // Confirmed at any time: its alarm has been dealt with.
            if (confirmed && s.Raised != null && !s.ByPosition) Drop(s);

            if (!s.Since.HasValue) return;
            if (!commanded)
            {
                // Let go of before it arrived (a 5/3 button released, a spring's coil
                // back ON) or reversed: not a failure. A 5/2 double valve keeps going
                // after its pulse - unless its other coil reverses it.
                if (!s.KeepsAfterDrop || On(lookup, s.OtherDrive)) { s.Since = null; return; }
            }
            if (confirmed)
            {
                double took = (now - s.Since.Value).TotalSeconds;
                s.Since = null;
                if (s.Raised != null) Drop(s);
                if (RecordsOn(c.ModuleId)) Tell(r => r.MovementDone(c, s, took, now));
                return;
            }
            double waited = (now - s.Since.Value).TotalSeconds;
            if (waited < DelayOf(c, s, settings)) return;

            s.Since = null;
            int sub = Diagnose(s, lookup, now);
            if (RecordsOn(c.ModuleId)) Tell(r => r.MovementTooLong(c, s, waited, sub, now));
            if (alarms) Raise(c, s, sub, lookup, now);
        }

        /// <summary>The machine conditions of a cylinder's PositionWhile.</summary>
        private static IEnumerable<ConditionDefinition> PositionRules(Cylinder c)
        {
            if (c.PositionWhile == 0) yield break;
            foreach (var part in MachineConditionMap.Parts((SystemCondition)c.PositionWhile))
            {
                if (ConditionBits.IsShared(part)) continue;
                var def = MachineConditionMap.GetAllMachineBits().Where(kv => kv.Key == (long)part).Select(kv => kv.Value).FirstOrDefault();
                if (def != null) yield return def;
            }
        }

        /// <summary>Its position reads this cylinder now (every machine condition of PositionWhile met).</summary>
        internal static bool PositionReadable(Cylinder c, Func<string, int> lookup)
        {
            if (c.PositionWhile == 0) return true;
            foreach (var part in MachineConditionMap.Parts((SystemCondition)c.PositionWhile))
                if (!ConditionBits.IsShared(part) && !MachineConditionMap.CheckMachineBit((long)part, lookup)) return false;
            return true;
        }

        /// <summary>Which signal failed, from the states when the time ran out.</summary>
        internal static int Diagnose(CylinderSide s, Func<string, int> lookup, DateTime now)
        {
            if (s.DriveOn && !string.IsNullOrWhiteSpace(s.OtherDrive) && On(lookup, s.Drive) && On(lookup, s.OtherDrive)) return BothCoils;
            if (s.ByPosition)
            {
                if (!s.Moved) return PositionUnchanged;
                if ((now - s.PositionChanged).TotalMilliseconds < SettleMs) return PositionMoving;
                // moved and settled: a sensor that should have changed did not
            }
            bool startOn = s.MustOff.Any(x => On(lookup, x.Address));
            bool targetOn = s.MustOn.Count > 0 && s.MustOn.All(x => On(lookup, x.Address));
            if (targetOn && startOn) return StartStuck;
            if (startOn) return StartStayedOn;
            if (s.MustOn.Count == 0 && s.ByPosition) return PositionMoving;
            return TargetNotOn;
        }

        private static void Raise(Cylinder c, CylinderSide s, int sub, Func<string, int> lookup, DateTime now)
        {
            var def = string.IsNullOrWhiteSpace(s.ErrorAddress) ? null : AlarmManager.FindByStationAndAddress(c.Station, s.ErrorAddress);
            string baseCode = def != null ? def.AlarmCode : (string.IsNullOrEmpty(c.ModuleId) ? "" : c.ModuleId + ".") + Safe(c.Unit) + "_" + s.Side;
            string text = def != null ? def.TranslationKey : (c.Unit + (string.IsNullOrEmpty(s.Name) ? "" : " - " + s.Name));
            var snapshot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in new[] { s.Drive, s.OtherDrive }.Concat(s.MustOn.Select(x => x.Address)).Concat(s.MustOff.Select(x => x.Address)))
                if (!string.IsNullOrWhiteSpace(a)) snapshot[a.Trim().ToUpperInvariant()] = On(lookup, a) ? 1 : 0;
            if (s.Movement != null && s.Movement.Command != null && !string.IsNullOrWhiteSpace(s.Movement.Command.Address))
                snapshot[s.Movement.Command.Address.Trim().ToUpperInvariant()] = On(lookup, s.Movement.Command.Address) ? 1 : 0;
            var item = new AlarmItem(baseCode + "." + sub, text, def != null ? def.Severity : AlarmSeverity.Fault)
            {
                Generated = true,
                Station = c.Station,
                Address = s.Code,
                SubCode = sub.ToString(CultureInfo.InvariantCulture),
                Detail = Describe(sub, s),
                DetailText = () => Describe(sub, s),
                BaseCode = def != null ? def.AlarmCode : "",
                GroupCode = baseCode,
                Snapshot = snapshot,
                Side = s.Side,
                TriggerTime = now
            };
            if (s.Raised != null) AlarmManager.ClearGenerated(s.Raised);
            s.Raised = item;
            item.Device = s.ErrorAddress;
            AlarmManager.RaiseGenerated(item);
            SetDevice(item);
        }

        // ---- the error bit SET in the PLC ------------------------------------------------

        /// <summary>
        /// The driver an alarm's error bit is written through (a test sets its own);
        /// null - a station known to be down - writes nothing.
        /// </summary>
        public static Func<int, IPlcDriver> DriverForWrite = station => PlcManager.Instance.GetDriverForWrite(station);

        /// <summary>
        /// 8 October: "the M801 device should be SET so the PLC knows about the alarm
        /// (the same for the cylinder movement controls)". Written once, as the alarm
        /// is raised. The HMI does not reset it: the PLC's alarm reset clears its
        /// alarm bits as it clears those it sets itself. Only an error bit is ever
        /// written - a unit or axis without one has nothing set, and its command bit
        /// is never touched.
        /// </summary>
        private static void SetDevice(AlarmItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Device)) return;
            string device = item.Device.Trim();
            IPlcDriver driver = null;
            try { driver = DriverForWrite == null ? null : DriverForWrite(item.Station); } catch { driver = null; }
            if (driver == null)
            {
                AuditLogger.Log("HMI alarm " + item.ShownCode + ": " + device + " not set - station " + item.Station + " does not answer.");
                return;
            }
            try
            {
                int rc;
                using (PlcWriteLog.Source("HMI alarm " + item.ShownCode + ": its error bit set"))
                    rc = driver.Write(device, 1);
                AuditLogger.Log(rc == 0 ? "HMI alarm " + item.ShownCode + ": " + device + " set on station " + item.Station + "."
                                        : "HMI alarm " + item.ShownCode + ": " + device + " could not be set (" + rc + ").");
            }
            catch (Exception ex) { AuditLogger.Log("HMI alarm " + item.ShownCode + ": " + device + " could not be set - " + ex.Message); }
        }

        private static string Safe(string text)
        {
            var t = new string((text ?? "").Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
            return t.Length == 0 ? "Unit" : t;
        }

        private static void DropAxis(Axis x)
        {
            if (x.RaisedServo != null) { AlarmManager.ClearGenerated(x.RaisedServo); x.RaisedServo = null; }
            if (x.RaisedAxis != null) { AlarmManager.ClearGenerated(x.RaisedAxis); x.RaisedAxis = null; }
            x.LastServo = x.LastAxis = 0;
        }

        private static void EvaluateAxis(Axis x, Func<string, int> lookup, DateTime now, bool raise = true)
        {
            int servo = string.IsNullOrWhiteSpace(x.ServoAlarm) ? 0 : Word(lookup, x.ServoAlarm) & 0xFFFF;
            int axis = string.IsNullOrWhiteSpace(x.AxisAlarm) ? 0 : Word(lookup, x.AxisAlarm) & 0xFFFF;
            // what it is doing now; a code that comes is told what it was doing the scan before
            var cur = x.WatchesMotion ? ReadMotion(x, lookup) : null;
            // Acknowledged while the drive still shows the code: back on the list, as
            // a PLC alarm bit that is still on comes back.
            if (x.RaisedServo != null && !AlarmManager.IsOnList(x.RaisedServo)) { x.RaisedServo = null; x.LastServo = 0; }
            if (x.RaisedAxis != null && !AlarmManager.IsOnList(x.RaisedAxis)) { x.RaisedAxis = null; x.LastAxis = 0; }
            if (servo != x.LastServo)
            {
                if (x.RaisedServo != null) { AlarmManager.ClearGenerated(x.RaisedServo); x.RaisedServo = null; }
                if (servo == 0) x.ServoMoment = null;
                else
                {
                    // the same code again (acknowledged, still shown): the moment it first came
                    if (x.ServoMoment == null || x.ServoMoment.Code != servo) x.ServoMoment = MomentOf(x, AxisCodeKind.ServoAlarm, servo, cur, now);
                    if (raise) x.RaisedServo = RaiseAxis(x, AxisCodeKind.ServoAlarm, servo, now, x.ServoMoment);
                }
                x.LastServo = servo;
            }
            if (axis != x.LastAxis)
            {
                if (x.RaisedAxis != null) { AlarmManager.ClearGenerated(x.RaisedAxis); x.RaisedAxis = null; }
                if (axis == 0) x.AxisMoment = null;
                else
                {
                    if (x.AxisMoment == null || x.AxisMoment.Code != axis) x.AxisMoment = MomentOf(x, AxisCodeKind.AxisError, axis, cur, now);
                    if (raise) x.RaisedAxis = RaiseAxis(x, AxisCodeKind.AxisError, axis, now, x.AxisMoment);
                }
                x.LastAxis = axis;
            }
            if (cur != null) TrackMotion(x, cur, now);
        }

        // ---- what an axis was doing (16 October) -----------------------------------------

        private static Reading ReadMotion(Axis x, Func<string, int> lookup)
        {
            Func<string, int?> read = a =>
            {
                if (string.IsNullOrWhiteSpace(a)) return null;
                try { return lookup(a.Trim()); } catch { return null; }
            };
            Func<string, bool?> bit = a => { var v = read(a); return v.HasValue ? v.Value != 0 : (bool?)null; };
            Func<string, bool, long?> number = (a, dbl) =>
            {
                var lo = read(a);
                if (!lo.HasValue) return null;
                if (!dbl) return (short)lo.Value;
                var hi = read(NextWord(a.Trim()));
                if (!hi.HasValue) return null;
                return (int)(((uint)(hi.Value & 0xFFFF) << 16) | (uint)(lo.Value & 0xFFFF));
            };
            Func<ConditionRule, bool?> rule = r =>
            {
                if (r == null || r.PlcNumber != x.Station || string.IsNullOrWhiteSpace(r.Address)) return null;
                double v;
                return MachineConditionMap.Evaluate(r, read, out v);
            };
            var status = read(x.Status);
            var busy = read(x.Busy);
            return new Reading
            {
                Status = status.HasValue ? (short)status.Value : (int?)null,
                Busy = busy.HasValue ? (x.BusyBit >= 0 ? ((busy.Value >> x.BusyBit) & 1) == 1 : busy.Value != 0) : (bool?)null,
                JogBack = bit(x.JogBack),
                JogForward = bit(x.JogForward),
                Ref = bit(x.RefSensor),
                HomeRequest = bit(x.HomeRequest),
                Homing = bit(x.Homing),
                Running = rule(x.Running),
                ManualMode = rule(x.ManualMode),
                Position = number(x.Position, x.PositionDouble),
                Min = number(x.MinLimit, x.LimitsDouble),
                Max = number(x.MaxLimit, x.LimitsDouble),
                SoftUpper = number(x.SoftUpper, true),
                SoftLower = number(x.SoftLower, true),
                Unit = read(x.UnitWord)
            };
        }

        /// <summary>
        /// The smallest change of position that is a movement (in counts): a hundredth of the
        /// axis's unit (0.01 mm), or the last decimal the page shows when it shows fewer -
        /// smaller is the servo holding its place.
        /// </summary>
        private static long Tolerance(Axis x)
        {
            double d = x.Divisor > 0 ? x.Divisor : 1;
            return Math.Max(1, (long)Math.Round(d * Math.Pow(10, -Math.Max(0, Math.Min(2, x.Decimals)))));
        }

        /// <summary>Follows the movement: when it started, from where, whether it moved, its last change.</summary>
        private static void TrackMotion(Axis x, Reading cur, DateTime now)
        {
            bool moving = cur.Moving;
            bool wasMoving = x.Last != null && x.Last.Moving;
            if (moving && !wasMoving)
            {
                x.MoveStart = now;
                x.StartRaw = x.LastRaw = cur.Position;
                x.LastChange = now;
                x.Moved = false;
                x.Direction = 0;
            }
            else if (moving && cur.Position.HasValue)
            {
                long tol = Tolerance(x);
                if (!x.LastRaw.HasValue) { x.LastRaw = cur.Position; x.LastChange = now; }
                else if (Math.Abs(cur.Position.Value - x.LastRaw.Value) >= tol)
                {
                    x.LastRaw = cur.Position;
                    x.LastChange = now;
                }
                if (!x.StartRaw.HasValue) x.StartRaw = cur.Position;
                long d = cur.Position.Value - x.StartRaw.Value;
                if (Math.Abs(d) >= tol) { x.Moved = true; x.Direction = d > 0 ? 1 : -1; }
            }
            else if (!moving)
            {
                x.MoveStart = null;
                x.LastChange = null;
                x.Moved = false;
            }
            x.Last = cur;
        }

        /// <summary>The moment of a code: what was read the scan before (the scan of the code when it was there from the start).</summary>
        private static AxisMoment MomentOf(Axis x, AxisCodeKind kind, int code, Reading cur, DateTime now)
        {
            var before = x.Last ?? cur;
            var m = new AxisMoment
            {
                Station = x.Station, Unit = x.Name, Kind = kind, Code = code, AlarmTime = now,
                SeenRising = x.Last != null,
                Decimals = x.Decimals, Suffix = x.Suffix ?? "",
                RefAddress = (x.RefSensor ?? "").Trim(), RefForward = x.RefForward, Vertical = x.Vertical
            };
            try { m.RefPoint = AxisSteps.FindReferenceSensor(x.Station, x.RefSensor, string.IsNullOrWhiteSpace(x.DrawingName) ? x.Name : x.DrawingName); }
            catch { }
            if (before == null) return m;
            double div = x.Divisor > 0 ? x.Divisor : 1;
            Func<long?, double?> scaled = v => v.HasValue ? v.Value / div : (double?)null;
            m.Status = before.Status;
            m.Busy = before.Busy;
            m.JogBack = before.JogBack;
            m.JogForward = before.JogForward;
            m.Running = before.Running;
            m.ManualMode = before.ManualMode;
            m.HomeRequest = before.HomeRequest;
            m.Homing = before.Homing;
            m.RefOn = before.Ref;
            m.Position = scaled(cur != null && cur.Position.HasValue ? cur.Position : before.Position);
            m.MinLimit = scaled(before.Min);
            m.MaxLimit = scaled(before.Max);
            // the module's own, in its unit (Pr.1: 0.1 um for mm, 0.00001 degree)
            int unit = before.Unit.HasValue && before.Unit.Value >= 0 && before.Unit.Value <= 3 ? before.Unit.Value : ((x.Suffix ?? "").Trim() == "\u00B0" ? 2 : 0);
            double per = SimpleMotion.PositionScale(unit);
            m.ModuleMin = before.SoftLower.HasValue ? before.SoftLower.Value / per : (double?)null;
            m.ModuleMax = before.SoftUpper.HasValue ? before.SoftUpper.Value / per : (double?)null;
            m.Motion = AxisMoment.MotionOf(before.Status, before.Busy, before.JogBack, before.JogForward, before.Running, before.ManualMode, before.HomeRequest, before.Homing);
            if (x.Last != null && x.Last.Moving && x.MoveStart.HasValue)
            {
                m.MovingMs = (int)Math.Max(0, (now - x.MoveStart.Value).TotalMilliseconds);
                m.Moved = x.Moved;
                m.Direction = x.Direction;
                m.PositionStart = scaled(x.StartRaw);
                // moved, then stood still - and still standing at the code
                bool stillNow = cur == null || !cur.Position.HasValue || !x.LastRaw.HasValue ||
                                Math.Abs(cur.Position.Value - x.LastRaw.Value) < Tolerance(x);
                if (x.Moved && stillNow && x.LastChange.HasValue)
                {
                    int ms = (int)Math.Max(0, (now - x.LastChange.Value).TotalMilliseconds);
                    if (ms >= StuckMs) { m.Stuck = true; m.StuckMs = ms; }
                }
            }
            return m;
        }

        /// <summary>
        /// The axis's step-by-step page for a code it raised (the moment it came), or null:
        /// not an axis's alarm, or nothing of its movement watched.
        /// </summary>
        public static TroubleshootingCatalog.HelpDocument AxisStepsFor(AlarmItem item)
        {
            if (item == null || item.AxisMoment == null) return null;
            var doc = AxisStepsDocument(item.AxisMoment);
            if (doc != null) { doc.AlarmTime = item.TriggerTime; doc.AlarmText = item.Description ?? ""; }
            return doc;
        }

        /// <summary>The page for the code an axis's code word shows now (its row's code cell, its "?"), or null.</summary>
        public static TroubleshootingCatalog.HelpDocument AxisStepsForCode(int station, string codeAddress, int code)
        {
            var m = MomentForCode(station, codeAddress, code);
            return m == null ? null : AxisStepsDocument(m);
        }

        /// <summary>What the axis was doing when the code its code word shows now came, or null (not watched, another code).</summary>
        public static AxisMoment MomentForCode(int station, string codeAddress, int code)
        {
            if (string.IsNullOrWhiteSpace(codeAddress) || code <= 0) return null;
            PlcAddressHelper.Resolve(station, codeAddress.Trim(), out station, out codeAddress);
            foreach (var x in _axes)
            {
                if (x.Station != station) continue;
                if (string.Equals((x.ServoAlarm ?? "").Trim(), codeAddress.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    x.ServoMoment != null && x.ServoMoment.Code == (code & 0xFFFF))
                    return x.ServoMoment;
                if (string.Equals((x.AxisAlarm ?? "").Trim(), codeAddress.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    x.AxisMoment != null && x.AxisMoment.Code == (code & 0xFFFF))
                    return x.AxisMoment;
            }
            return null;
        }

        /// <summary>
        /// The code's one help with its moment: "Servo alarm 50.1 - Thermal overload error 1  (Robot X)",
        /// the moment, the checks and the manual's entry on one page (null when there is nothing to tell).
        /// </summary>
        public static TroubleshootingCatalog.HelpDocument AxisStepsDocument(AxisMoment m)
        {
            if (m == null) return null;
            var doc = AxisCodeHelp.Document(m.Kind, m.Code, m.Unit);
            return WithMoment(doc, m) ? doc : null;
        }

        private static AlarmItem RaiseAxis(Axis x, AxisCodeKind kind, int value, DateTime now, AxisMoment moment = null)
        {
            var def = string.IsNullOrWhiteSpace(x.ErrorAddress) ? null : AlarmManager.FindByStationAndAddress(x.Station, x.ErrorAddress);
            string sub = AxisCodeHelp.Format(kind, value);
            string baseCode = def != null ? def.AlarmCode : (string.IsNullOrEmpty(x.ModuleId) ? "" : x.ModuleId + ".") + "Axis_" + x.Number;
            var item = new AlarmItem(baseCode + "." + (kind == AxisCodeKind.ServoAlarm ? "S" : "A") + sub, def != null ? def.TranslationKey : x.Name,
                                     def != null ? def.Severity : AlarmSeverity.Fault)
            {
                Generated = true,
                Station = x.Station,
                Address = x.Code,
                SubCode = sub,
                Detail = AxisCodeHelp.Describe(kind, value),
                DetailText = () => AxisCodeHelp.Describe(kind, value),
                BaseCode = def != null ? def.AlarmCode : "",
                GroupCode = baseCode,
                Device = (x.ErrorAddress ?? "").Trim(),
                TriggerTime = now,
                AxisMoment = moment
            };
            AlarmManager.RaiseGenerated(item);
            SetDevice(item);
            return item;
        }

        private static void DropInverter(Inverter v)
        {
            if (v.Raised != null) { AlarmManager.ClearGenerated(v.Raised); v.Raised = null; }
            v.Last = 0;
        }

        private static void EvaluateInverter(Inverter v, Func<string, int> lookup, DateTime now)
        {
            int code = Word(lookup, v.CodeAddress) & 0xFFFF;
            // acknowledged while the inverter still shows the fault: back on the list
            if (v.Raised != null && !AlarmManager.IsOnList(v.Raised)) { v.Raised = null; v.Last = 0; }
            if (code == v.Last) return;
            if (v.Raised != null) { AlarmManager.ClearGenerated(v.Raised); v.Raised = null; }
            v.Last = code;
            if (code != 0) v.Raised = RaiseInverter(v, code, now);
        }

        private static AlarmItem RaiseInverter(Inverter v, int code, DateTime now)
        {
            var def = string.IsNullOrWhiteSpace(v.ErrorAddress) ? null : AlarmManager.FindByStationAndAddress(v.Station, v.ErrorAddress);
            string baseCode = def != null ? def.AlarmCode : (string.IsNullOrEmpty(v.ModuleId) ? "" : v.ModuleId + ".") + "Inverter_" + Safe(v.Name);
            var item = new AlarmItem(baseCode + "." + code.ToString(CultureInfo.InvariantCulture),
                                     def != null ? def.TranslationKey : v.Name, def != null ? def.Severity : AlarmSeverity.Fault)
            {
                Generated = true,
                Station = v.Station,
                Address = v.Device,
                // as the inverter's panel shows it: "E.OC1"
                SubCode = AxisCodeHelp.Display(AxisCodeKind.InverterFault, code),
                Detail = AxisCodeHelp.Describe(AxisCodeKind.InverterFault, code),
                DetailText = () => AxisCodeHelp.Describe(AxisCodeKind.InverterFault, code),
                BaseCode = def != null ? def.AlarmCode : "",
                GroupCode = baseCode,
                Device = (v.ErrorAddress ?? "").Trim(),
                TriggerTime = now
            };
            AlarmManager.RaiseGenerated(item);
            SetDevice(item);
            return item;
        }

        /// <summary>The inverter whose fault a generated alarm is, or null.</summary>
        public static Inverter InverterOf(AlarmItem item)
        {
            if (item == null || !item.Generated) return null;
            return _inverters.FirstOrDefault(v => ReferenceEquals(v.Raised, item));
        }

        // ---- asked by the units, the alarm list and the PLC alarms -------------------------------

        /// <summary>
        /// The alarm the HMI raised for a movement, while it is on the list: the
        /// unit's station and side, and the devices it may be named by (its error
        /// bit, its command, its outputs) - a page's unit is not the one discovery saw.
        /// </summary>
        public static AlarmItem ActiveFor(int station, string side, params string[] codes)
        {
            if (codes == null || codes.Length == 0) return null;
            bool any = false;
            foreach (var a in AlarmManager.ActiveAlarms) if (a.Generated) { any = true; break; }
            if (!any) return null;                                   // the usual case: nothing to search
            foreach (var c in _cylinders)
            {
                if (c.Station != station) continue;
                foreach (var s in c.Sides)
                {
                    if (s.Raised == null || s.Side != side) continue;
                    foreach (var code in codes)
                        if (!string.IsNullOrWhiteSpace(code) && string.Equals(s.Code, code.Trim(), StringComparison.OrdinalIgnoreCase))
                            return AlarmManager.IsOnList(s.Raised) ? s.Raised : null;
                }
            }
            return null;
        }

        /// <summary>The code's entry, as its one page with the moment when the HMI knows it (18 October).</summary>
        private static TroubleshootingCatalog.HelpDocument WithHint(TroubleshootingCatalog.HelpDocument doc, AlarmItem item)
        {
            if (WithMoment(doc, item.AxisMoment) && item.TriggerTime != default(DateTime)) doc.AlarmTime = item.TriggerTime;
            return doc;
        }

        /// <summary>
        /// ONE HELP PER CODE (18 October: "two different helpers are not very well presented - can we
        /// combine them?"): a code's entry of the manual becomes its step-by-step page when the HMI
        /// knows the moment it came - what the axis was doing, where to start, the checks for that,
        /// and the entry itself as the last step. False (the entry as it is) when there is nothing
        /// of its movement to tell or no template.
        /// </summary>
        public static bool WithMoment(TroubleshootingCatalog.HelpDocument doc, AxisMoment m)
        {
            if (doc == null || m == null || m.Motion == AxisMotion.Unknown && !m.RefKnown) return false;
            if (string.IsNullOrEmpty(AxisSteps.TemplatePath(LanguageManager.CurrentLanguage))) return false;
            doc.AxisMoment = m;
            doc.StepByStep = true;
            if (!doc.AlarmTime.HasValue) doc.AlarmTime = m.AlarmTime;
            return true;
        }

        /// <summary>The help entry for the drive code of a generated axis alarm, or null.</summary>
        public static TroubleshootingCatalog.HelpDocument CodeDocumentFor(AlarmItem item)
        {
            if (item == null || !item.Generated) return null;
            foreach (var x in _axes)
            {
                if (ReferenceEquals(x.RaisedServo, item)) return WithHint(AxisCodeHelp.Document(AxisCodeKind.ServoAlarm, x.LastServo, x.Name), item);
                if (ReferenceEquals(x.RaisedAxis, item)) return WithHint(AxisCodeHelp.Document(AxisCodeKind.AxisError, x.LastAxis, x.Name), item);
            }
            var v = InverterOf(item);
            if (v != null) return AxisCodeHelp.Document(AxisCodeKind.InverterFault, v.Last, v.Name);
            return null;
        }

        /// <summary>The watched movement a generated alarm belongs to (its help page), or null.</summary>
        public static CylinderSide SideOf(AlarmItem item)
        {
            if (item == null || !item.Generated) return null;
            foreach (var c in _cylinders)
                foreach (var s in c.Sides)
                    if (ReferenceEquals(s.Raised, item) ||
                        (c.Station == item.Station && s.Side == item.Side && string.Equals(s.Code, item.Address, StringComparison.OrdinalIgnoreCase)))
                        return s;
            return null;
        }

        /// <summary>
        /// The movement of a generated alarm as its help page needs it: the devices
        /// with their states when the time ran out (the alarm's snapshot). A copy -
        /// the watch's own stays as it was found.
        /// </summary>
        public static TroubleshootingCatalog.MovementInfo MovementAt(AlarmItem item)
        {
            var side = SideOf(item);
            if (side == null || side.Movement == null) return null;
            var src = side.Movement;
            var snap = item.Snapshot ?? new Dictionary<string, int>();
            Func<TroubleshootingCatalog.MovementInfo.Device, TroubleshootingCatalog.MovementInfo.Device> copy = d =>
            {
                if (d == null) return null;
                int v;
                return new TroubleshootingCatalog.MovementInfo.Device
                {
                    Address = d.Address, Name = d.Name,
                    AtAlarm = !string.IsNullOrWhiteSpace(d.Address) && snap.TryGetValue(d.Address.Trim().ToUpperInvariant(), out v) ? v : (int?)null
                };
            };
            var mv = new TroubleshootingCatalog.MovementInfo
            {
                Error = src.Error, Position = src.Position, AlarmTime = item.TriggerTime, SeenRising = true,
                Station = src.Station, Unit = src.Unit, Side = src.Side, Name = src.Name, OtherName = src.OtherName,
                Command = copy(src.Command), Output = copy(src.Output), OtherCommand = copy(src.OtherCommand), OtherOutput = copy(src.OtherOutput)
            };
            foreach (var k in src.Conditions) mv.Conditions.Add(new TroubleshootingCatalog.MovementInfo.Condition { Name = k.Name, Rule = k.Rule });
            foreach (var d in src.Feedback) mv.Feedback.Add(copy(d));
            foreach (var d in src.OppositeFeedback) mv.OppositeFeedback.Add(copy(d));
            return mv;
        }

        /// <summary>The failure of a generated movement alarm (its sub-code), 0 for any other.</summary>
        public static int FailureOf(AlarmItem item)
        {
            int f;
            return item != null && item.Generated && SideOf(item) != null && int.TryParse(item.SubCode, out f) ? f : 0;
        }

        /// <summary>
        /// The step-by-step page for a generated movement alarm - only about the
        /// signal that failed - or null (an axis's alarm, a movement no longer watched).
        /// </summary>
        public static TroubleshootingCatalog.HelpDocument StepsFor(AlarmItem item)
        {
            var mv = MovementAt(item);
            var side = SideOf(item);
            var unit = side == null ? null : CylinderOf(side);
            string file;
            if (mv == null || unit == null ||
                !TroubleshootingCatalog.DocumentForType.TryGetValue(TroubleshootingCatalog.ErrorType.CylinderMovement, out file) || string.IsNullOrEmpty(file))
                return null;
            return new TroubleshootingCatalog.HelpDocument
            {
                FileName = file,
                StepByStep = true,
                Title = LanguageManager.GetString("Hlp_CylinderSteps", "Step by step") +
                        (string.IsNullOrEmpty(unit.Unit) ? "" : " - " + unit.Unit) + " - " + TroubleshootingCatalog.MovementName(mv) +
                        // the alarm's bit is an internal device: named for an Admin only
                        (TroubleshootingCatalog.AdminView() ? " (" + item.ShownCode + ")" : ""),
                Valve = TroubleshootingCatalog.CanonicalValve((int)unit.ValveNow),
                Feedback = TroubleshootingCatalog.CanonicalFeedback((int)unit.Feedback),
                Movement = mv,
                Failure = FailureOf(item)
            };
        }

        /// <summary>The unit a watched movement belongs to.</summary>
        public static Cylinder CylinderOf(CylinderSide side)
        {
            return _cylinders.FirstOrDefault(c => c.Sides.Contains(side));
        }

        private static bool _hooked;

        private static void HookSuppression()
        {
            if (_hooked) return;
            _hooked = true;
            AlarmManager.Suppressed = def =>
            {
                if (def == null || string.IsNullOrEmpty(def.AlarmCode)) return false;
                foreach (var a in AlarmManager.ActiveAlarms)
                    if (a.Generated && string.Equals(a.BaseCode, def.AlarmCode, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            };
        }

        /// <summary>Takes a machine's generated alarms off the list (switched off).</summary>
        private static void ClearRaised(string moduleId, bool cylinders, bool axes)
        {
            if (cylinders)
                foreach (var c in _cylinders.Where(c => string.Equals(c.ModuleId, moduleId ?? "", StringComparison.OrdinalIgnoreCase)))
                    foreach (var s in c.Sides) { s.Since = null; s.Seen = false; Drop(s); }
            if (axes)
            {
                foreach (var x in _axes.Where(x => string.Equals(x.ModuleId, moduleId ?? "", StringComparison.OrdinalIgnoreCase))) DropAxis(x);
                foreach (var v in _inverters.Where(v => string.Equals(v.ModuleId, moduleId ?? "", StringComparison.OrdinalIgnoreCase))) DropInverter(v);
            }
        }

        /// <summary>Every watch and setting forgotten (tests).</summary>
        public static void ResetForTests()
        {
            foreach (var c in _cylinders)
                foreach (var x in c.Sides) { x.Seen = x.Commanded = x.Moved = false; x.Since = null; x.Raised = null; }
            foreach (var a in _axes)
            {
                a.LastServo = a.LastAxis = 0; a.RaisedServo = a.RaisedAxis = null;
                a.ServoMoment = a.AxisMoment = null; a.Last = null; a.MoveStart = a.LastChange = null; a.StartRaw = a.LastRaw = null; a.Moved = false;
            }
            StuckMs = 500;
            foreach (var v in _inverters) { v.Last = 0; v.Raised = null; }
            _cylinders.Clear();
            _axes.Clear();
            _inverters.Clear();
            _settings = null;
        }
    }
}
