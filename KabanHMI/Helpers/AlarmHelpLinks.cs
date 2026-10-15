using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Settings;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// Which unit on the machine an alarm belongs to - learned from the units.
    ///
    /// THE MODEL
    /// ---------
    /// A CylinderUnitHMI IS a cylinder: its valve and its feedback sensor are
    /// facts about that physical unit, and its two error bits are the alarms it
    /// can raise. An AxisSettingHMI IS a servo axis, with one error bit. So the
    /// knowledge lives on the control, set once where the unit is designed,
    /// instead of being re-typed on every alarm the unit can raise - where two
    /// alarms of one cylinder could quietly be given different valves.
    ///
    /// At start-up every machine page is built off-screen once, each unit's
    /// error bits are collected, and an alarm on the same station and device is
    /// linked to that unit. Alarms that belong to no unit - emergency stop, air
    /// pressure - carry their help as plain helper links.
    /// </summary>
    public static class AlarmHelpLinks
    {
        public sealed class Link
        {
            public TC.ErrorType Kind;
            public int Station;
            public string Address = "";
            public int Valve;
            public int Feedback;
            /// <summary>The unit or axis as it is named on its page.</summary>
            public string Unit = "";
            /// <summary>The unit's own help files, listed after its manual.</summary>
            public List<string> HelpFiles = new List<string>();

            /// <summary>
            /// An axis's three drive code words (Axis Settings, last three
            /// columns). Read when the alarm's help is opened, so the list starts
            /// with the entry for the code the drive is showing now.
            /// </summary>
            public string ServoAlarm = "", AxisAlarm = "", AxisWarning = "";

            /// <summary>
            /// A cylinder's error bit is one side's: the movement it reports and
            /// that movement's devices, named at the top of the tailored manual.
            /// </summary>
            public TC.MovementInfo Movement;

            /// <summary>
            /// The movement as its unit sees it NOW - with the states of its devices
            /// when the error bit came on - asked when the help is opened; Movement
            /// when there is no unit (or it is gone).
            /// </summary>
            public Func<TC.MovementInfo> MovementNow;

            public TC.MovementInfo CurrentMovement()
            {
                if (MovementNow != null)
                    try { var m = MovementNow(); if (m != null) return m; } catch { }
                return Movement;
            }

            public bool HasCodes
            {
                get { return ServoAlarm.Length > 0 || AxisAlarm.Length > 0 || AxisWarning.Length > 0; }
            }
        }

        private static readonly Dictionary<string, Link> _links =
            new Dictionary<string, Link>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> _conflicts = new List<string>();

        public static int Count { get { return _links.Count; } }
        public static IList<string> Conflicts { get { return _conflicts.AsReadOnly(); } }
        public static TimeSpan LastDiscovery { get; private set; }

        private static string Key(int station, string address)
        {
            // "M915@2": PLC 2's error bit, whichever unit names it (12 October)
            PlcAddressHelper.Resolve(station, (address ?? "").Trim(), out station, out address);
            return station + "|" + (address ?? "").Trim().ToUpperInvariant();
        }

        public static void Clear()
        {
            SimulationMotion.Clear();      // collected in the same pass over the pages
            AlarmGeneration.Clear();       // so are the movements the HMI times itself
            _links.Clear();
            _conflicts.Clear();
            _axes.Clear();
            _axisList.Clear();
            _encoders.Clear();
        }

        // ---- cylinders with an encoder (9 October) ------------------------------
        //
        // Every CylinderEncoderSettingHMI row seen by discovery, so a
        // CylinderEncoderUnitHMI on a manual page can mirror it by number - copied,
        // as the axis rows are.

        public sealed class EncoderInfo
        {
            public int Station, Number, Decimals, Digits;
            public string NameKey = "", Back = "", Forward = "", BackSensor = "", ForwardSensor = "",
                          ErrorBack = "", ErrorForward = "", HelpBack = "", HelpForward = "", Position = "", Suffix = "";
            public Kaban.Core.Enums.DataTypeEnum PositionType;
            public Kaban.Core.Enums.UnitType UnitType;
            public double Divisor;
            public CylinderValve Valve;
            public CylinderFeedback Feedback;
            public bool Vertical;
            public Kaban.Core.Enums.SecurityLevel Security = Kaban.Core.Enums.SecurityLevel.Operator;
        }

        private static readonly Dictionary<string, EncoderInfo> _encoders = new Dictionary<string, EncoderInfo>();

        /// <summary>The encoder rows found, by station and number.</summary>
        public static IList<EncoderInfo> AllEncoders { get { return _encoders.Values.ToList(); } }

        public static EncoderInfo FindEncoder(int station, int number)
        {
            EncoderInfo e;
            return _encoders.TryGetValue(station + "|" + number, out e) ? e : null;
        }

        private static void RegisterEncoder(CylinderEncoderSettingHMI row)
        {
            if (row == null || row.EncoderNumber <= 0) return;
            var info = row.Info();
            var key = info.Station + "|" + info.Number;
            EncoderInfo seen;
            if (_encoders.TryGetValue(key, out seen))
            {
                // the same row searched twice is no conflict; two rows with one number are
                if (string.Equals(seen.Position, info.Position, StringComparison.OrdinalIgnoreCase) && seen.Back == info.Back && seen.Forward == info.Forward) return;
                var msg = string.Format("PLC {0} encoder {1} is numbered on two rows", info.Station, info.Number);
                if (!_conflicts.Contains(msg)) { _conflicts.Add(msg); AuditLogger.Log("encoder conflict - " + msg); }
                return;
            }
            _encoders[key] = info;
        }

        // ---- axes ------------------------------------------------------------
        //
        // Every AxisSettingHMI row seen by discovery, so an AxisUnitHMI on a manual
        // page can mirror it by number. Copied, not referenced: the page the row
        // was found on is disposed straight after.

        public sealed class AxisInfo
        {
            public int Station, Number, Decimals, Digits;
            public string NameKey = "", Back = "", Forward = "", Position = "", Suffix = "",
                          ReferenceSensor = "", Error = "",
                          ServoAlarm = "", AxisAlarm = "", AxisWarning = "";
            public AxisReferenceSide ReferenceSide;
            public Kaban.Core.Enums.DataTypeEnum PositionType;
            public double Divisor;
            public bool Vertical;
            /// <summary>For the Axis Monitor: the current meter and when the row is shown.</summary>
            public string Current = "", RealPosition = "";
            public double CurrentDivisor = 1, CurrentFullScale = 100;
            public Kaban.Core.Enums.SystemCondition Visibility;
            /// <summary>When its jogs may be used (the row's, and each jog's own added) - an AxisUnitHMI's too.</summary>
            public Kaban.Core.Enums.SystemCondition BackEnable, ForwardEnable;
            public Kaban.Core.Models.PlcTrigger BackTrigger, ForwardTrigger;
            /// <summary>Every setting of the row, so the Axis Monitor can show it again (an Admin's PLC axis items).</summary>
            public Dictionary<string, object> Row;
        }

        private static readonly List<AxisInfo> _axisList = new List<AxisInfo>();

        /// <summary>
        /// Every axis row discovery saw, in page order, numbered or not - the Axis
        /// Monitor's list. The same row seen twice (a page built twice) once.
        /// </summary>
        public static IList<AxisInfo> AllAxes { get { return _axisList.AsReadOnly(); } }

        private static readonly Dictionary<string, AxisInfo> _axes = new Dictionary<string, AxisInfo>();
        private static bool _discovered, _discovering;

        /// <summary>True while discovery is building pages only to search them.</summary>
        public static bool IsDiscovering { get { return _discovering; } }

        public static AxisInfo FindAxis(int station, int number)
        {
            AxisInfo a;
            return _axes.TryGetValue(station + "|" + number, out a) ? a : null;
        }

        /// <summary>Runs discovery once, if it has not run yet.</summary>
        public static void EnsureDiscovered()
        {
            if (_discovered || _discovering) return;
            if (_pending == null) StartDiscovery();
            while (_pending != null && _pending.Count > 0) RunOne();
            if (_pending != null) Finish();
        }

        private static void RegisterAxis(AxisSettingHMI a)
        {
            if (a == null) return;
            var info = new AxisInfo
            {
                Station = a.PlcNumber, Number = a.HMIAxisIndex, NameKey = a.AxisNameKey ?? "",
                Back = a.CommandBackAddress ?? "", Forward = a.CommandForwardAddress ?? "",
                Position = a.PositionAddress ?? "", PositionType = a.PositionDataType,
                Divisor = a.PositionScale, Decimals = a.DecimalPlaces, Digits = a.Digits, Suffix = a.Suffix ?? "",
                ReferenceSensor = a.ReferenceSensorAddress ?? "", ReferenceSide = a.ReferenceSensorSide,
                Error = a.ErrorAddress ?? "", Vertical = a.VerticalMovement,
                ServoAlarm = a.ServoAlarmAddress ?? "", AxisAlarm = a.AxisAlarmAddress ?? "",
                AxisWarning = a.AxisWarningAddress ?? "",
                Current = a.CurrentAddress ?? "", CurrentDivisor = a.CurrentDivisor, CurrentFullScale = a.CurrentFullScale,
                RealPosition = a.LoadEncoderValueAddress ?? "", Visibility = a.VisibilityConditions,
                BackEnable = a.EnableConditions | a.BackEnableConditions, ForwardEnable = a.EnableConditions | a.ForwardEnableConditions,
                BackTrigger = a.BackEnableTrigger, ForwardTrigger = a.ForwardEnableTrigger,
                Row = a.Snapshot()
            };
            if (!string.IsNullOrWhiteSpace(info.Position) &&
                !_axisList.Any(x => x.Station == info.Station && string.Equals(x.Position, info.Position, StringComparison.OrdinalIgnoreCase)))
                _axisList.Add(info);

            if (a.HMIAxisIndex <= 0) return;
            var key = a.PlcNumber + "|" + a.HMIAxisIndex;
            AxisInfo seen;
            if (_axes.TryGetValue(key, out seen))
            {
                // THE SAME ROW TWICE is not a conflict. Built while someone with
                // the level for Axis Settings is signed in, Machine Parameters
                // opens on that tab (its first) and shows its rows - and discovery
                // then builds the tab again on its own. Since a restart keeps the
                // user signed in, the log listed all 22 axes as "numbered on two
                // rows" after every restart. Two DIFFERENT rows - another
                // position - with one number still are.
                if (string.Equals(seen.Position, info.Position, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(seen.NameKey, info.NameKey, StringComparison.Ordinal)) return;
                var msg = string.Format("PLC {0} axis {1} is numbered on two rows", a.PlcNumber, a.HMIAxisIndex);
                if (!_conflicts.Contains(msg)) { _conflicts.Add(msg); AuditLogger.Log("axis conflict - " + msg); }
                return;
            }
            _axes[key] = info;
        }

        /// <summary>
        /// Adds a unit's error bit. The same unit shown on two pages is normal and
        /// ignored; two units describing ONE error bit differently is a design
        /// mistake, and it is reported rather than settled silently - otherwise the
        /// operator's help would depend on which page happened to be built first.
        /// </summary>
        public static void Register(Link link)
        {
            if (link == null || link.Station <= 0 || string.IsNullOrWhiteSpace(link.Address)) return;
            var key = Key(link.Station, link.Address);

            Link existing;
            if (_links.TryGetValue(key, out existing))
            {
                // The axis row knows the code words; a manual-page unit of the same
                // axis may have been seen first without them.
                if (!existing.HasCodes && link.HasCodes)
                {
                    existing.ServoAlarm = link.ServoAlarm;
                    existing.AxisAlarm = link.AxisAlarm;
                    existing.AxisWarning = link.AxisWarning;
                }
                if (existing.Kind != link.Kind || existing.Valve != link.Valve || existing.Feedback != link.Feedback)
                {
                    var msg = string.Format("PLC {0} {1}: '{2}' and '{3}' describe this error differently",
                                            link.Station, link.Address.Trim(), existing.Unit, link.Unit);
                    if (!_conflicts.Contains(msg))
                    {
                        _conflicts.Add(msg);
                        AuditLogger.Log("help link conflict - " + msg);
                    }
                }
                return;
            }
            _links[key] = link;
        }

        public static Link Find(int station, string address)
        {
            Link l;
            return _links.TryGetValue(Key(station, address), out l) ? l : null;
        }

        /// <summary>Collects every unit under a control, pages inside settings screens included.</summary>
        public static void Harvest(Control root)
        {
            if (root == null) return;

            var unit = root as CylinderUnitHMI;
            if (unit != null) { HarvestUnit(unit); return; }

            var axis = root as AxisSettingHMI;
            if (axis != null) { HarvestAxis(axis); return; }

            // an encoder's row: known by its number - its unit inside is harvested below
            var enc = root as CylinderEncoderSettingHMI;
            if (enc != null) RegisterEncoder(enc);

            // A settings screen builds its tabs only when they are opened; its
            // pages are built here one at a time instead.
            var settings = root as SettingsScreen;
            if (settings != null)
                foreach (var page in settings.AllPages) HarvestPage(page);

            foreach (Control c in root.Controls) Harvest(c);
        }

        private static void HarvestControls(Control root)
        {
            if (root == null) return;
            var unit = root as CylinderUnitHMI;
            if (unit != null) { HarvestUnit(unit); return; }
            var axis = root as AxisSettingHMI;
            if (axis != null) { HarvestAxis(axis); return; }
            var enc = root as CylinderEncoderSettingHMI;
            if (enc != null) RegisterEncoder(enc);
            foreach (Control c in root.Controls) HarvestControls(c);
        }

        private static void HarvestUnit(CylinderUnitHMI unit)
        {
            foreach (var l in unit.HelpLinks()) Register(l);
            SimulationMotion.Register(unit);
            // its movements, for the alarms the HMI raises itself
            try { AlarmGeneration.Register(unit.BuildWatch()); }
            catch (Exception ex) { AuditLogger.Log("alarm generation skipped unit '" + unit.Name + "': " + ex.Message); }
        }

        private static void HarvestAxis(AxisSettingHMI axis)
        {
            Register(axis.HelpLink());
            RegisterAxis(axis);
            SimulationMotion.Register(axis);
            // its drive's alarm codes (the Axis Warning is only a warning: not watched)
            AlarmGeneration.Register(new AlarmGeneration.Axis
            {
                Station = axis.PlcNumber,
                Number = axis.HMIAxisIndex,
                Name = string.IsNullOrEmpty(axis.AxisNameKey) ? "Axis " + axis.HMIAxisIndex : LanguageManager.GetString(axis.AxisNameKey, axis.AxisNameKey),
                ErrorAddress = (axis.ErrorAddress ?? "").Trim(),
                ServoAlarm = (axis.ServoAlarmAddress ?? "").Trim(),
                AxisAlarm = (axis.AxisAlarmAddress ?? "").Trim()
            });
        }

        private static void HarvestPage(ISettingsPage page)
        {
            Control view = null;
            try
            {
                view = page.GetControl();
                Harvest(view);
            }
            catch (Exception ex)
            {
                AuditLogger.Log("help discovery skipped page '" + page.TitleFallback + "': " + ex.Message);
            }
            finally
            {
                Discard(view);
            }
        }

        // ---- discovery, in idle-time steps -----------------------------------
        //
        // Every machine page is built off-screen once, searched, and disposed.
        // Done in one go it held the UI thread for seconds on a cold first start -
        // the wait before the user name could be typed. Now each page is its own
        // step, run while the program is idle, about 25 ms at a time; anything that
        // needs the result early (an axis unit, an alarm's help) finishes the rest.

        private static Queue<Action> _pending;
        private static readonly Stopwatch _work = new Stopwatch();

        /// <summary>Queues discovery to run in idle time. Returns at once.</summary>
        public static void StartDiscovery()
        {
            if (_discovered || _pending != null) return;
            Clear();
            _work.Reset();
            _pending = new Queue<Action>();
            foreach (var kv in MachineRegistry.AllScreens())
            {
                var screen = kv.Value;
                if (screen.CreateView == null) continue;
                if (screen.Role == ScreenRoles.Alarms || screen.Role == ScreenRoles.HmiSettings) continue;
                _pending.Enqueue(() => HarvestScreen(screen));
            }
            Application.Idle += OnIdle;
        }

        private static bool _inIdle;

        private static void OnIdle(object sender, EventArgs e)
        {
            // NOT RE-ENTERED. A step builds pages, page building can process
            // messages, and a second Idle then ran inside the first: it finished
            // the queue and cleared it, and the outer one fell over the cleared
            // queue (NullReferenceException in OnIdle).
            if (_inIdle) return;
            if (_pending == null) { Application.Idle -= OnIdle; return; }
            _inIdle = true;
            try
            {
                var slice = Stopwatch.StartNew();
                while (_pending != null && _pending.Count > 0 && slice.ElapsedMilliseconds < 25) RunOne();
                if (_pending != null && _pending.Count == 0) Finish();
            }
            finally { _inIdle = false; }
        }

        private static void RunOne()
        {
            var step = _pending.Dequeue();
            _discovering = true;
            _work.Start();
            try { step(); }
            catch (Exception ex) { AuditLogger.Log("help discovery step failed: " + ex.Message); }
            finally { _work.Stop(); _discovering = false; }
        }

        private static void Finish()
        {
            Application.Idle -= OnIdle;
            _pending = null;
            _discovered = true;
            LastDiscovery = _work.Elapsed;
            AuditLogger.Log(string.Format("help links: {0} error bit(s) linked to units in {1} ms of idle time{2}",
                _links.Count, (int)_work.ElapsedMilliseconds,
                _conflicts.Count == 0 ? "" : ", " + _conflicts.Count + " conflict(s)"));
        }

        private static void HarvestScreen(MachineScreen screen)
        {
            Control view = null;
            try
            {
                view = screen.CreateView() as Control;
                // A settings screen's tabs become steps of their own.
                var settings = view as SettingsScreen;
                if (settings != null)
                    foreach (var page in settings.AllPages) { var pg = page; _pending.Enqueue(() => HarvestPage(pg)); }
                HarvestControls(view);
            }
            catch (Exception ex)
            {
                AuditLogger.Log("help discovery skipped '" + screen.TitleFallback + "': " + ex.Message);
            }
            finally { Discard(view); }
        }

        /// <summary>
        /// Disposes a page built only to be searched - after removing its controls
        /// from the shared tooltip. WinForms never removes a disposed control from
        /// a tooltip, and a tooltip full of dead off-screen controls throws when it
        /// next needs its window (the login's NullReferenceException).
        /// </summary>
        public static void Discard(Control view)
        {
            if (view == null) return;
            var tips = UiStyle.Tips();
            var stack = new Stack<Control>(); stack.Push(view);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                if (!string.IsNullOrEmpty(tips.GetToolTip(c))) tips.SetToolTip(c, null);
                foreach (Control k in c.Controls) stack.Push(k);
            }
            view.Dispose();
        }

        /// <summary>Discovery all at once - for tests, and for callers that need it finished.</summary>
        public static TimeSpan DiscoverAll()
        {
            _discovered = false;
            _pending = null;
            StartDiscovery();
            while (_pending != null && _pending.Count > 0) RunOne();
            if (_pending != null) Finish();
            return LastDiscovery;
        }

        /// <summary>
        /// The documents for an alarm: its unit's manual first - tailored to that
        /// cylinder's valve and feedback - then the alarm's own helper files.
        /// </summary>
        public static List<TC.HelpDocument> DocumentsFor(AlarmDefinition def)
        {
            return def == null ? new List<TC.HelpDocument>()
                               : DocumentsFor(def.PlcNumber, def.Address, def.HelperKeys);
        }

        /// <summary>
        /// The documents for an alarm on the list. One the HMI raised itself: the
        /// step-by-step page about the signal that failed (or the entry for the
        /// drive's code) first, then what its device's alarm offers - without that
        /// alarm's own step-by-step page, which is about whatever the PLC's bit saw.
        /// </summary>
        public static List<TC.HelpDocument> DocumentsFor(AlarmItem item)
        {
            if (item == null) return new List<TC.HelpDocument>();
            var list = DocumentsOf(item);
            // the step-by-step page is titled with the alarm and dated with its time
            foreach (var d in list)
                if (d.StepByStep)
                {
                    d.AlarmTime = item.TriggerTime;
                    d.AlarmText = item.Description ?? "";
                }
            return list;
        }

        private static List<TC.HelpDocument> DocumentsOf(AlarmItem item)
        {
            if (!item.Generated) return DocumentsFor(AlarmManager.FindByAddressOrCode(item.AlarmCode));
            var docs = new List<TC.HelpDocument>();
            var steps = AlarmGeneration.StepsFor(item);
            if (steps != null) docs.Add(steps);
            var code = AlarmGeneration.CodeDocumentFor(item);
            if (code != null) docs.Add(code);
            var def = string.IsNullOrEmpty(item.BaseCode) ? null : AlarmManager.FindByCode(item.BaseCode);
            foreach (var d in DocumentsFor(item.Station, item.Address, def != null ? def.HelperKeys : null))
            {
                if (d.StepByStep) continue;                           // the PLC bit's view of the movement
                if (code != null && !string.IsNullOrEmpty(d.Code)) continue;   // the code's entry is first already
                docs.Add(d);
            }
            return docs.Where(d => d.Exists).ToList();
        }

        public static List<TC.HelpDocument> DocumentsFor(int station, string address, IEnumerable<string> helperFiles)
        {
            var docs = new List<TC.HelpDocument>();

            var link = Find(station, address);

            // An axis: the entry for each code its drive shows NOW comes first -
            // the answer to this fault, not the manual it is somewhere in.
            if (link != null && link.HasCodes)
                docs.AddRange(CodeDocuments(link));

            string file;
            if (link != null && TC.DocumentForType.TryGetValue(link.Kind, out file) && !string.IsNullOrEmpty(file))
            {
                bool cylinder = link.Kind == TC.ErrorType.CylinderMovement;
                // A unit's movement: its step-by-step troubleshooter first, the manual after it.
                var movement = cylinder ? link.CurrentMovement() : null;
                if (movement != null)
                    docs.Add(new TC.HelpDocument
                    {
                        FileName = file,
                        StepByStep = true,
                        Title = LanguageManager.GetString("Hlp_CylinderSteps", "Step by step") +
                                (string.IsNullOrEmpty(link.Unit) ? "" : " - " + link.Unit) + " - " + TC.MovementName(movement),
                        Valve = TC.CanonicalValve(link.Valve),
                        Feedback = TC.CanonicalFeedback(link.Feedback),
                        Movement = movement
                    });
                docs.Add(new TC.HelpDocument
                {
                    FileName = file,
                    Title = (cylinder ? LanguageManager.GetString("Hlp_Cylinder", "Cylinder troubleshooting")
                                      : LanguageManager.GetString("Hlp_Servo", "Servo drive troubleshooting"))
                            + (string.IsNullOrEmpty(link.Unit) ? "" : " - " + link.Unit),
                    Valve = cylinder ? TC.CanonicalValve(link.Valve) : 0,
                    Feedback = cylinder ? TC.CanonicalFeedback(link.Feedback) : 0,
                    Movement = movement
                });
            }

            // The unit's own help next: what the unit IS, beside what went wrong.
            if (link != null && link.HelpFiles != null)
                foreach (var n in link.HelpFiles)
                    docs.Add(new TC.HelpDocument { FileName = n, Title = Path.GetFileNameWithoutExtension(n) });

            if (helperFiles != null)
                foreach (var entry in helperFiles.Where(f => !string.IsNullOrWhiteSpace(f)))
                    foreach (var name in entry.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var n = name.Trim();
                        if (n.Length == 0) continue;
                        var d = new TC.HelpDocument { FileName = n };
                        d.Title = d.IsWebLink ? n : Path.GetFileNameWithoutExtension(n);
                        docs.Add(d);
                    }

            // One entry per file: an alarm helper link that repeats a unit file
            // would list the same document twice.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return docs.Where(d => d.Exists && (d.Valve != 0 || d.Feedback != 0 || d.Movement != null ||
                                                !string.IsNullOrEmpty(d.Code) || seen.Add(d.FileName))).ToList();
        }

        /// <summary>
        /// Reads a code word once, for an alarm's help (null when it cannot be
        /// read). Replaceable so a test can supply the values. Monitoring access:
        /// looking a code up is not commanding the machine.
        /// </summary>
        public static Func<int, string, int?> ReadCodeNow = (station, address) =>
        {
            try
            {
                if (ConnectionMonitor.IsKnownDown(station)) return null;
                var drv = PlcManager.Instance.GetDriverForMonitoring(station);
                int v;
                if (drv != null && drv.TryRead(address, out v)) return v & 0xFFFF;
            }
            catch (Exception ex) { AuditLogger.Log("could not read code word " + address + ": " + ex.Message); }
            return null;
        };

        /// <summary>One document per code of the axis that is not zero right now.</summary>
        public static List<TC.HelpDocument> CodeDocuments(Link link)
        {
            var docs = new List<TC.HelpDocument>();
            if (link == null) return docs;
            foreach (var kv in new[]
            {
                new KeyValuePair<AxisCodeKind, string>(AxisCodeKind.ServoAlarm, link.ServoAlarm),
                new KeyValuePair<AxisCodeKind, string>(AxisCodeKind.AxisError, link.AxisAlarm),
                new KeyValuePair<AxisCodeKind, string>(AxisCodeKind.AxisWarning, link.AxisWarning)
            })
            {
                if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                var v = ReadCodeNow(link.Station, kv.Value.Trim());
                if (v == null || v.Value == 0) continue;
                var d = AxisCodeHelp.Document(kv.Key, v.Value, link.Unit);
                if (d != null) docs.Add(d);
            }
            return docs;
        }

        /// <summary>
        /// A document from the program's own Help folder is opened from a COPY in
        /// the temp folder. A PDF viewer keeps the file it shows locked, and the
        /// one it locked was the program's: building the program again while a
        /// manual was open failed ("could not copy MR_J4_TroubleShooting.pdf ...
        /// locked by Adobe Acrobat"), and so could an update. The copy is made
        /// once and refreshed only when the manual changes. The HTML pages the
        /// program writes are in the temp folder already.
        /// </summary>
        internal static string ViewerCopy(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return path;
                var help = Path.GetFullPath(TC.HelpFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var full = Path.GetFullPath(path);
                if (!full.StartsWith(help, StringComparison.OrdinalIgnoreCase)) return path;
                if (full.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || full.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                    return path;          // a page may link files beside it by name: opened where it is
                var dir = Path.Combine(Path.GetTempPath(), "KabanHMI_Help");
                Directory.CreateDirectory(dir);
                var copy = Path.Combine(dir, Path.GetFileName(full));
                var src = new FileInfo(full);
                var dst = new FileInfo(copy);
                if (!dst.Exists || dst.Length != src.Length || dst.LastWriteTimeUtc < src.LastWriteTimeUtc)
                {
                    try { File.Copy(full, copy, true); }
                    catch (IOException) { if (!dst.Exists) return path; }   // the old copy is open: show it
                }
                return copy;
            }
            catch { return path; }
        }

        /// <summary>Opens a document - tailored or sectioned where that applies.</summary>
        public static void Open(IWin32Window owner, TC.HelpDocument doc)
        {
            if (doc == null) return;
            string target = ViewerCopy(TC.ExtractSection(doc));
            try
            {
                AuditLogger.Log("opened help document " + doc.FileName + ".");
                Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not open help document: " + ex.Message);
                MessageBox.Show(owner,
                    LanguageManager.GetString("Msg_HelpOpenFailed", "Could not open the help document.") +
                    "\r\n" + target + "\r\n\r\n" + ex.Message,
                    LanguageManager.GetString("Alarm_Col_Help", "Help"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}

