using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Kaban.UI.Controls;
using Kaban.Core.Enums;
using Kaban.Core.Models;
using Kaban.Core.Helpers;

namespace Kaban.UI.Plc
{
    public class PlcScanner
    {
        private Timer _timer;
        private Control _targetControl;

        private List<BaseNumericHMI> _numerics = new List<BaseNumericHMI>();
        private List<LampHMI> _lamps = new List<LampHMI>();
        private List<ButtonHMI> _buttons = new List<ButtonHMI>();
        private readonly System.Collections.Generic.List<CheckListHMI> _checkLists = new System.Collections.Generic.List<CheckListHMI>();
        private readonly System.Collections.Generic.List<CheckBoxHMI> _checkBoxes = new System.Collections.Generic.List<CheckBoxHMI>();
        private readonly System.Collections.Generic.List<ComboBoxHMI> _combos = new System.Collections.Generic.List<ComboBoxHMI>();

        /// <summary>
        /// Every OTHER IHmiControl on the page - meters, hex codes, group boxes,
        /// and anything added later.
        ///
        /// The walk used to test a fixed list of six types, so a control that was
        /// not one of them was never discovered, never contributed its address to
        /// the batch read and never had UpdateState called. It simply sat there
        /// empty, which is exactly what a new MeterHMI or HexCodeHMI did - and the
        /// failure is silent, because an unread control looks the same as one whose
        /// device happens to read zero.
        ///
        /// Matching the INTERFACE instead means a new control works as soon as it
        /// implements IHmiControl, with no change here.
        /// </summary>
        private readonly System.Collections.Generic.List<IHmiControl> _others = new System.Collections.Generic.List<IHmiControl>();

        // PER-STATION scan state. The scanner previously read ONLY station 1 and fed
        // every control from that single batch - controls' PlcNumber was ignored, so
        // when station 1 was down NOTHING updated, simulation stations included.
        private readonly System.Collections.Generic.Dictionary<int, string[]> _plcBatch = new System.Collections.Generic.Dictionary<int, string[]>();
        private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<string, int>> _plcIndexMap = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<string, int>>();
        private readonly System.Collections.Generic.Dictionary<int, int[]> _plcResults = new System.Collections.Generic.Dictionary<int, int[]>();
        private readonly System.Collections.Generic.Dictionary<int, int[]> _plcLastGood = new System.Collections.Generic.Dictionary<int, int[]>();
        private readonly System.Collections.Generic.Dictionary<int, int> _plcReconnectTicks = new System.Collections.Generic.Dictionary<int, int>();
        private readonly System.Collections.Generic.Dictionary<int, bool> _plcHealthy = new System.Collections.Generic.Dictionary<int, bool>();

        // Addresses discovered at runtime (a control bound after the batch was
        // built). Kept per station so a rebuild picks them up without the screen
        // having to re-register them.
        private Dictionary<int, HashSet<string>> _dynamicAddresses = new Dictionary<int, HashSet<string>>();
        private bool _needsRebuild = false;

        // ==========================================================================
        // COMMUNICATION HEALTH (fix: a failed read used to look exactly like a
        // genuine 0 on screen). We keep the last *good* scan so a comm glitch
        // shows stale-but-real values plus a "comm lost" state, instead of
        // silently rendering every tag as zero.
        // ==========================================================================
        public bool CommunicationHealthy { get; private set; } = true;
        public event EventHandler CommunicationStateChanged;

        public PlcScanner(Control targetControl)
        {
            _targetControl = targetControl;
            _timer = new Timer();
            _timer.Interval = 100;
            _timer.Tick += ScanLoop;

            // re-read the tag list whenever a screen swaps its content in place
            _contentChanged = (s2, e2) =>
            {
                if (_targetControl == null || _targetControl.IsDisposed) return;
                try
                {
                    if (_targetControl.IsHandleCreated)
                        _targetControl.BeginInvoke(new Action(() => RefreshTags()));
                    else RefreshTags();
                }
                catch { }
            };
            ContentChanged += _contentChanged;
        }

        private readonly EventHandler _contentChanged;

        /// <summary>
        /// Other places whose controls are read with the page's: the side panel
        /// and a side page's own window. Read only while they are on show, like
        /// the page; the page's alarms and banner stay the page scanner's.
        /// </summary>
        private readonly List<Control> _extraTargets = new List<Control>();

        public void AddTarget(Control target)
        {
            if (target != null && !_extraTargets.Contains(target)) _extraTargets.Add(target);
        }

        public void RemoveTarget(Control target)
        {
            _extraTargets.Remove(target);
        }

        public void SetInterval(int interval) { if (interval > 0) _timer.Interval = interval; }

        /// <summary>
        /// Raised by any screen that swaps its visible content WITHOUT a full page
        /// load - a settings/parameter TAB change, for instance. The scanner only
        /// scans the controls it can currently see, so a new tab's controls had no
        /// addresses in the batch and simply never updated.
        /// </summary>
        public static event EventHandler ContentChanged;

        /// <summary>Call after swapping content in place; the active scanner re-reads its tags.</summary>
        public static void NotifyContentChanged()
        {
            var h = ContentChanged;
            if (h != null) h(null, EventArgs.Empty);
        }

        private static int _generationVersion;
        private int _generationSeen;

        static PlcScanner()
        {
            AlarmGeneration.WatchesChanged += (s, e) => System.Threading.Interlocked.Increment(ref _generationVersion);
        }

        public void RefreshTags(bool clearDynamicCache = false)
        {
            _generationSeen = _generationVersion;
            if (clearDynamicCache) _dynamicAddresses.Clear();

            _timer.Stop();

            _numerics.Clear(); _lamps.Clear(); _buttons.Clear(); _checkLists.Clear(); _checkBoxes.Clear(); _combos.Clear(); _others.Clear();
            _plcIndexMap.Clear();

            FindControls(_targetControl);
            _extraTargets.RemoveAll(t => t == null || t.IsDisposed);
            foreach (var t in _extraTargets)
                if (t.Visible) FindControls(t);

            // Addresses are collected PER STATION: curPlc is set to each control's
            // PlcNumber before its addresses are added, so every station gets its
            // own batch instead of everything landing on station 1.
            var perPlc = new Dictionary<int, HashSet<string>>();
            int curPlc = 1;

            // --- DEEP EXTRACTION HELPERS ---
            Action<int, string> AddTo = (station, s) =>
            {
                if (string.IsNullOrWhiteSpace(s)) return;
                // "X12@2": read in PLC 2's batch, whichever control names it (12 October)
                int st; string device;
                PlcAddressHelper.Resolve(station, s.Trim(), out st, out device);
                if (!perPlc.ContainsKey(st)) perPlc[st] = new HashSet<string>();
                perPlc[st].Add(device.Trim().ToUpper());
            };
            Action<string> Add = (s) => AddTo(curPlc, s);

            Action<DynamicValue, DataTypeEnum> AddDynamic = (dv, dataType) =>
            {
                if (dv != null && dv.Type == LimitSourceType.Tag && !string.IsNullOrEmpty(dv.TagAddress))
                {
                    Add(dv.TagAddress);
                    if (dataType == DataTypeEnum.Bin_32) Add(PlcAddressHelper.GetNextAddress(dv.TagAddress));
                }
            };

            Action<string, DynamicValue, DataTypeEnum> AddEffective = (baseAddr, offsetVal, dataType) =>
            {
                if (string.IsNullOrEmpty(baseAddr)) return;
                int offset = 0;
                if (offsetVal != null && offsetVal.Type == LimitSourceType.Static) offset = (int)offsetVal.StaticValue;

                string effAddr = PlcAddressHelper.OffsetAddress(baseAddr, offset);
                Add(effAddr);
                if (dataType == DataTypeEnum.Bin_32) Add(PlcAddressHelper.GetNextAddress(effAddr));
            };

            Action<PlcTrigger> AddTrigger = (pt) => { if (pt != null && !string.IsNullOrEmpty(pt.MonitorAddress)) Add(pt.MonitorAddress); };

            // A condition is read from the station IT is registered on, not from
            // the control's: MullionNotching lives on PLC2, and a PLC1 button
            // gated by it used to read X25 from PLC1 - a different input entirely.
            Action<SystemCondition> AddConditions = (cond) =>
            {
                if (cond == SystemCondition.None) return;
                long val = (long)cond;

                // 1. Extract Enum Bits (0-15)
                foreach (var flag in ConditionFlags)
                {
                    if (((long)flag & val) == 0) continue;
                    var rule = MachineConditionMap.GetRule(flag);
                    if (rule != null && !string.IsNullOrEmpty(rule.Address))
                    {
                        int st = AlarmManager.StationOf(rule);
                        AddTo(st, rule.Address);
                        if (rule.DataType == DataTypeEnum.Bin_32) AddTo(st, PlcAddressHelper.GetNextAddress(rule.Address));
                    }
                }

                // 2. Extract Machine Bits (16-63)
                foreach (var bit in MachineConditionMap.GetAllMachineBits())
                {
                    if ((val & bit.Key) != 0)
                    {
                        int st = AlarmManager.StationOf(bit.Value);
                        AddTo(st, bit.Value.Address);
                        // Automatically poll the next address if the hardware tag is 32-bit
                        if (bit.Value.DataType == DataTypeEnum.Bin_32)
                        {
                            AddTo(st, PlcAddressHelper.GetNextAddress(bit.Value.Address));
                        }
                    }
                }
            };

            foreach (var num in _numerics)
            {
                curPlc = num.PlcNumber;
                AddEffective(num.Address, num.AddressOffset, num.DataType);
                AddDynamic(num.AddressOffset, DataTypeEnum.Bin_16);
                AddConditions(num.VisibilityConditions);
                // The value it is compared with (an axis's real position), same width.
                AddEffective((num.ComparedAddress ?? "").Trim(), null, num.DataType);

                var inp = num as NumericInputHMI;
                if (inp != null)
                {
                    AddDynamic(inp.MinLimit, num.DataType);
                    AddDynamic(inp.MaxLimit, num.DataType);
                    AddConditions(inp.EnableConditions);
                    AddTrigger(inp.EnableTrigger);

                    // RESTORED: poll any Tag-based math variables used by ReadFormula/WriteFormula
                    // so limit re-scaling has live data available in the scan cache too.
                    if (inp.MathVariables != null)
                    {
                        foreach (var mv in inp.MathVariables)
                        {
                            if (mv != null && !mv.IsConstant && !string.IsNullOrEmpty(mv.AddressOrValue))
                                Add(mv.AddressOrValue);
                        }
                    }
                }
            }

            foreach (var cl in _checkLists)
            {
                curPlc = cl.PlcNumber;
                foreach (var a in cl.Addresses) Add(a);
            }
            foreach (var cb in _checkBoxes)
            {
                curPlc = cb.PlcNumber; Add(cb.Address); Add((cb.AlertAddress ?? "").Trim());
                AddConditions(cb.VisibilityConditions); AddConditions(cb.EnableConditions); AddTrigger(cb.EnableTrigger);
            }
            foreach (var cmb in _combos) { curPlc = cmb.PlcNumber; Add(cmb.Address); }
            foreach (var b in _buttons)
            {
                curPlc = b.PlcNumber;
                int totalStaticOffset = 0;
                if (b.DisplayOffset != null && b.DisplayOffset.Type == LimitSourceType.Static) totalStaticOffset += (int)b.DisplayOffset.StaticValue;

                string effDisplay = PlcAddressHelper.OffsetAddress(b.DisplayAddress, totalStaticOffset);
                Add(effDisplay);

                AddDynamic(b.DisplayOffset, DataTypeEnum.Bin_16);

                // State-visualization bits (relay shading, output shading, sensor lamps)
                Add(b.RelayAddress);
                Add(b.OutputAddress);
                Add(b.Lamp1Address);
                Add(b.Lamp2Address);
                Add(b.ConfirmationAddress);          // asked while it is on (FA2010's Cancel)

                AddConditions(b.VisibilityConditions);
                AddConditions(b.EnableConditions);
                AddConditions(b.BlinkCondition);
                AddTrigger(b.EnableTrigger);

                if (b.BitActions != null)
                {
                    foreach (var act in b.BitActions)
                    {
                        if (act == null) continue;
                        AddEffective(act.Address, act.AddressOffset, DataTypeEnum.Bin_16);
                    }
                }

                if (b.WordActions != null)
                {
                    foreach (var act in b.WordActions)
                    {
                        if (act == null) continue;
                        AddEffective(act.Address, act.AddressOffset, DataTypeEnum.Bin_16);
                        AddDynamic(act.Value, DataTypeEnum.Bin_16);
                    }
                }
            }

            foreach (var l in _lamps)
            {
                curPlc = l.PlcNumber;
                AddEffective(l.Address, l.AddressOffset, DataTypeEnum.Bin_16);
                AddDynamic(l.AddressOffset, DataTypeEnum.Bin_16);
                AddConditions(l.VisibilityConditions);
            }

            // Anything else that binds a device. Read as a 32-bit pair: a meter or
            // a code word may be either width, and reading one word too many is
            // harmless where reading one too few silently truncates the value.
            foreach (var o in _others)
            {
                var conditional = o as IConditionalControl;
                if (conditional != null) AddConditions(conditional.VisibilityConditions);
                var bound = o as IPlcBoundControl;
                if (bound == null) continue;
                curPlc = bound.PlcNumber;
                if (!string.IsNullOrWhiteSpace(bound.Address))
                {
                    Add(bound.Address);
                    Add(PlcAddressHelper.GetNextAddress(bound.Address));
                }
                // Code words beside the error bit (the axis "?"): single words.
                var extra = o as IPlcExtraAddresses;
                if (extra != null)
                    foreach (var a in extra.ExtraAddresses)
                        if (!string.IsNullOrWhiteSpace(a)) Add(a);
            }

            // Stations whose values are ON THE PAGE. The "communication lost" banner
            // speaks for these only: alarm-only stations below are watched for
            // alarms, and their loss is already shown by the connection warning.
            _displayStations.Clear();
            foreach (var st in perPlc.Keys) _displayStations.Add(st);
            foreach (var kvDyn in _dynamicAddresses) _displayStations.Add(kvDyn.Key);

            // Every registered alarm condition is polled on each scan, so
            // AlarmManager evaluates real PLC bits rather than a cached snapshot.
            //
            // EACH ON ITS OWN STATION. All of them used to be read from station 1,
            // so H500's and H800's alarms (remapped to stations 3 and 4, same M801...
            // addresses) were raised and cleared by FA2010's bits.
            AddTo(1, Kaban.UI.Forms.MainForm.MachineErrorAddress);   // machine-error summary bit (main PLC)
            foreach (var alarmDef in AlarmManager.GetAllDefinitions())
            {
                if (string.IsNullOrEmpty(alarmDef.Address)) continue;
                int st = AlarmManager.StationOf(alarmDef);
                AddTo(st, alarmDef.Address);
                if (alarmDef.DataType == DataTypeEnum.Bin_32) AddTo(st, PlcAddressHelper.GetNextAddress(alarmDef.Address));
            }
            // The alarms the HMI raises itself: the outputs, sensors and drive codes
            // they time and read, whichever page is on show.
            foreach (var gen in AlarmGeneration.Addresses()) AddTo(gen.Key, gen.Value);
            // The PLC's messages to the operator: each machine's trigger (FA2010: D71).
            foreach (var pop in PlcPopups.Addresses()) AddTo(pop.Key, pop.Value);

            // Fold in the addresses discovered at runtime (see _dynamicAddresses).
            foreach (var kvDyn in _dynamicAddresses)
            {
                curPlc = kvDyn.Key;
                foreach (var dyn in kvDyn.Value) Add(dyn);
            }

            _plcBatch.Clear();
            foreach (var kv in perPlc)
            {
                var arr = kv.Value.ToArray();
                _plcBatch[kv.Key] = arr;
                var map = new Dictionary<string, int>();
                for (int i = 0; i < arr.Length; i++) map[arr[i]] = i;
                _plcIndexMap[kv.Key] = map;
            }

            _needsRebuild = false; // RESET the flag
            _timer.Start();
        }

        public void Dispose()
        {
            ContentChanged -= _contentChanged;
            _timer.Stop();
        }

        public void Stop() => _timer.Stop();

        /// <summary>
        /// Greys every bound control whose station is not answering.
        ///
        /// Driven by ConnectionMonitor, so one rule decides it for the whole
        /// program: if the values are stale, the controls that write them are not
        /// safe to press.
        /// </summary>
        private void ApplyConnectionState()
        {
            // Buttons, numeric fields and checkboxes are NOT handled here. They grey
            // through their own soft enable, which reads ConnectionMonitor directly
            // - so it keeps their hover tooltip working, which a real
            // Enabled = false would switch off (the checkboxes had no tip at all).
            var real = new List<Control>();
            real.AddRange(_checkBoxes.Cast<Control>());
            real.AddRange(_combos.Cast<Control>());
            // NOT _others. Those are display controls - meters, code cells - and
            // Enabled = false switched off their tooltip and hover, which is what
            // left the axis meters looking dead. They wash themselves out through
            // ConnectionMonitor instead, and keep both.

            foreach (var c in real)
            {
                if (c is ButtonHMI || c is NumericInputHMI) continue;
                if (c is CheckBoxHMI) continue;

                var bound = c as IPlcBoundControl;
                int plc = bound != null ? bound.PlcNumber : PlcNumberOf(c);
                if (plc <= 0) continue;

                bool down = ConnectionMonitor.IsKnownDown(plc);
                ConnSaved saved;
                bool wasDisabledByUs = _connSaved.TryGetValue(c, out saved);

                if (down && !wasDisabledByUs)
                {
                    // Remember exactly what we are overriding, so recovery puts back
                    // the control's own state and tooltip rather than guessing.
                    _connSaved.Remove(c);
                    _connSaved.Add(c, new ConnSaved { Enabled = c.Enabled, Tip = _tips.GetToolTip(c) });
                    c.Enabled = false;
                    c.Cursor = Cursors.Default;
                    _tips.SetToolTip(c, ConnectionMonitor.ReasonFor(plc));
                }
                else if (!down && wasDisabledByUs)
                {
                    c.Enabled = saved.Enabled;
                    c.Cursor = saved.Enabled ? Cursors.Hand : Cursors.Default;
                    _tips.SetToolTip(c, saved.Tip ?? "");
                    _connSaved.Remove(c);
                }
            }
        }

        private sealed class ConnSaved { public bool Enabled; public string Tip; }

        /// <summary>
        /// What was overridden, kept for the CONTROL rather than for this scanner.
        ///
        /// Each page has its own scanner. Held per scanner, a control disabled on
        /// one visit was never re-enabled on the next - the new scanner had no
        /// record of disabling it. Static and weak: it survives scanners, and it
        /// never keeps a disposed control alive.
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, ConnSaved> _connSaved =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Control, ConnSaved>();

        private static int PlcNumberOf(Control c)
        {
            var p = c.GetType().GetProperty("PlcNumber");
            if (p == null) return 0;
            try { return Convert.ToInt32(p.GetValue(c, null)); } catch { return 0; }
        }

        private readonly ToolTip _tips = Kaban.UI.Controls.UiStyle.Tips();

        private void FindControls(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is BaseNumericHMI num) _numerics.Add(num);
                else if (c is LampHMI l) _lamps.Add(l);
                else if (c is ButtonHMI b) _buttons.Add(b);
                else if (c is CheckListHMI cl) _checkLists.Add(cl);
                else if (c is CheckBoxHMI cb) _checkBoxes.Add(cb);
                else if (c is ComboBoxHMI cmb) _combos.Add(cmb);
                else if (c is IHmiControl other) _others.Add(other);

                if (c.HasChildren) FindControls(c);
            }
        }

        /// <summary>
        /// The condition flags, looked up once. Enum.GetValues plus HasFlag (which
        /// boxes) ran for every condition of every control on every 100 ms scan.
        /// </summary>
        private static readonly SystemCondition[] ConditionFlags =
            ((SystemCondition[])Enum.GetValues(typeof(SystemCondition)))
                .Where(f => f != SystemCondition.None).ToArray();

        /// <summary>Stations with page controls (see RefreshTags).</summary>
        private readonly HashSet<int> _displayStations = new HashSet<int>();

        /// <summary>
        /// The conditions as the last scan found them - an Admin's tooltip marks each
        /// condition of a control met or not with it (14 October). Null before a scan.
        /// </summary>
        public static Func<SystemCondition, bool> LastConditionCheck { get; private set; }

        /// <summary>A set of conditions in words, each marked met or not by the last scan.</summary>
        public static string DescribeConditions(SystemCondition set)
        {
            return MachineConditionMap.Describe(set, LastConditionCheck);
        }

        /// <summary>
        /// Checks a control's conditions, each against the data of the station the
        /// condition is registered on. A station with no data at all fails the
        /// condition (fail-closed): "we cannot tell if it is in manual" must not
        /// read as "it is in manual".
        /// </summary>
        private bool CheckGlobalCondition(SystemCondition required, Func<int, Func<string, int>> lookupFor)
        {
            if (required == SystemCondition.None) return true;
            long val = (long)required;
            bool result = true;

            // Tier 1: System Enum Logic (Bits 0-15)
            foreach (var flag in ConditionFlags)
            {
                if (((long)flag & val) != 0)
                {
                    if (flag == SystemCondition.AdminUser)
                    {
                        result &= SecurityManager.HasAccess(SecurityLevel.Admin);
                        continue;
                    }

                    var rule = MachineConditionMap.GetRule(flag);
                    var lookup = rule == null ? null : lookupFor(AlarmManager.StationOf(rule));
                    if (rule != null && !string.IsNullOrEmpty(rule.Address) && lookup == null)
                    {
                        result = false;
                    }
                    else if (rule != null && !string.IsNullOrEmpty(rule.Address))
                    {
                        int low = lookup(rule.Address);
                        int currentValue = low;

                        if (rule.DataType == DataTypeEnum.Bin_32)
                        {
                            int high = lookup(PlcAddressHelper.GetNextAddress(rule.Address));
                            currentValue = (int)PlcValueConverter.ConvertToDouble(low, high, rule.DataType, DisplayFormatEnum.Signed_Decimal, 0);
                        }

                        bool rulePassed = false;
                        switch (rule.Operator)
                        {
                            case ComparisonOperator.Equal: rulePassed = (currentValue == rule.RequiredValue); break;
                            case ComparisonOperator.NotEqual: rulePassed = (currentValue != rule.RequiredValue); break;
                            case ComparisonOperator.Greater: rulePassed = (currentValue > rule.RequiredValue); break;
                            case ComparisonOperator.GreaterOrEqual: rulePassed = (currentValue >= rule.RequiredValue); break;
                            case ComparisonOperator.Less: rulePassed = (currentValue < rule.RequiredValue); break;
                            case ComparisonOperator.LessOrEqual: rulePassed = (currentValue <= rule.RequiredValue); break;
                        }
                        result &= rulePassed;
                    }
                    else result = false;
                }
            }

            // Tier 2: Machine Hardware Logic (Bits 16-63)
            foreach (var bit in MachineConditionMap.GetAllMachineBits())
            {
                if ((val & bit.Key) != 0)
                {
                    var lookup = lookupFor(AlarmManager.StationOf(bit.Value));
                    result &= lookup != null && MachineConditionMap.CheckMachineBit(bit.Key, lookup);
                }
            }

            return result;
        }

        // Reconnect throttling: try roughly every 2 seconds, not on every scan.
        // Counted PER STATION in _plcReconnectTicks - one shared counter made
        // a healthy station's scans reset a failing station's back-off.
        private const int ReconnectEveryTicks = 10;

        // Set on the UI thread, cleared from the worker on some paths: volatile so
        // neither side works from a stale copy.
        private volatile bool _scanBusy;

        // When each station's version device was last read (worker thread only).
        private readonly Dictionary<int, int> _versionReadTick = new Dictionary<int, int>();
        private const int VersionReadEveryMs = 1000;

        /// <summary>
        /// UI-thread tick: it no longer TALKS to the PLC. Every read and reconnect
        /// happens on a worker thread and only the results are applied here.
        /// A single COM round trip costs ~15 ms on this machine, so doing 170 of
        /// them inline made the whole HMI unresponsive.
        /// </summary>
        private void ScanLoop(object sender, EventArgs e)
        {
            if (_targetControl == null || _targetControl.IsDisposed) { _timer.Stop(); return; }
            if (_plcBatch.Count == 0) return;
            if (_scanBusy) return;              // previous scan still in flight - skip this tick
            // What the HMI's own alarms read changed (units found, a machine switched
            // on): once per tick however many changed, not once per unit.
            if (_generationSeen != _generationVersion) { _generationSeen = _generationVersion; RefreshTags(); }
            _scanBusy = true;

            // snapshot the batches so the worker never touches collections the UI edits
            var work = new Dictionary<int, string[]>(_plcBatch);
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                // NOTHING MAY ESCAPE THIS THREAD. An exception on a pool thread ends
                // the process, and ReadAllStations raises ConnectionChanged, whose
                // handlers run right here. It also must never leave _scanBusy set,
                // or the screen stops updating for good.
                bool handedOver = false;
                try
                {
                    Dictionary<int, int[]> fresh;
                    Dictionary<int, string> failed;
                    ReadAllStations(work, out fresh, out failed);
                    if (_targetControl != null && !_targetControl.IsDisposed && _targetControl.IsHandleCreated)
                    {
                        _targetControl.BeginInvoke(new Action(() =>
                        {
                            try { ApplyScan(work, fresh, failed); }
                            finally { _scanBusy = false; }
                        }));
                        handedOver = true;
                    }
                }
                catch (Exception ex)
                {
                    AuditLogger.Log("[PLC DIAG] scan worker failed: " + ex.Message);
                }
                finally
                {
                    if (!handedOver) _scanBusy = false;
                }
            });
        }

        /// <summary>WORKER THREAD: connects and reads. Touches no controls.</summary>
        private void ReadAllStations(Dictionary<int, string[]> work,
                                     out Dictionary<int, int[]> fresh,
                                     out Dictionary<int, string> failed)
        {
            fresh = new Dictionary<int, int[]>();
            failed = new Dictionary<int, string>();
            foreach (var kv in work)
            {
                int plc = kv.Key;
                string[] batch = kv.Value;
                if (batch == null || batch.Length == 0) continue;

                var driver = PlcManager.Instance.GetDriverForMonitoring(plc);
                if (driver == null)
                {
                    failed[plc] = "no driver registered";
                    ConnectionMonitor.ReportRead(plc, false, failed[plc]);
                    continue;
                }

                if (!driver.IsConnected)
                {
                    int ticks;
                    lock (_plcReconnectTicks) _plcReconnectTicks.TryGetValue(plc, out ticks);
                    ticks++;
                    if (ticks >= ReconnectEveryTicks)
                    {
                        ticks = 0;
                        try { driver.Connect(); } catch { /* stays down */ }
                    }
                    lock (_plcReconnectTicks) _plcReconnectTicks[plc] = ticks;
                }
                else lock (_plcReconnectTicks) _plcReconnectTicks[plc] = 0;

                int[] values = null;
                bool ok = false;
                bool answered = false;
                var readStarted = DateTime.Now;
                try
                {
                    ok = driver.IsConnected && driver.TryReadMulti(batch, out values);
                    // WHEN EACH DEVICE CHANGED, AND WHETHER THE HMI DID IT (the PLC
                    // Monitor's Last change column). A complete reading only: a batch
                    // with a failed address gives 0 for it, which is not a change.
                    if (ok && values != null) DeviceHistory.Observe(plc, batch, values, readStarted, DateTime.Now);
                    // TryReadMulti returns false if ANY single address failed, and a
                    // page can easily contain one address that does not exist in the
                    // current ladder. Judging the STATION by that condemned a
                    // perfectly healthy controller - which is why the simulation
                    // stations showed "disconnected" while plainly running.
                    //
                    // The station answered if values came back at all. A bad address
                    // is a page problem; a silent controller is a connection problem,
                    // and they must not be reported as the same thing.
                    answered = driver.IsConnected && values != null;
                }
                catch { ok = false; answered = false; }

                if (answered) fresh[plc] = values;
                else failed[plc] = driver.IsConnected ? "read failed" : "not connected";

                // EVERY attempt is reported, good or bad. IsConnected only says a
                // socket is open; a pulled cable leaves it believing that for as
                // long as the OS takes to notice, and meanwhile the screen shows
                // the last values as if they were current. Only a successful READ
                // proves the controller is still there.
                ConnectionMonitor.ReportRead(plc, answered,
                                             answered ? "" : failed.ContainsKey(plc) ? failed[plc] : "read failed");

                // The station's own program version, read on the same pass. Once a
                // second is plenty - the ladder does not change while the machine
                // runs. It WAS read on every 100 ms scan: a second round trip to
                // the controller per station per scan, for a number that never
                // changes. (The connection beat reads it once a second as well.)
                var verDev = ConnectionMonitor.GetVersionDevice(plc);
                if (ok && !string.IsNullOrEmpty(verDev))
                {
                    int now = Environment.TickCount, last;
                    bool due = !_versionReadTick.TryGetValue(plc, out last) || now - last >= VersionReadEveryMs;
                    if (due)
                    {
                        _versionReadTick[plc] = now;
                        int raw;
                        try { if (driver.TryRead(verDev, out raw)) ConnectionMonitor.ReportVersion(plc, raw); }
                        catch { }
                    }
                }
            }
        }

        /// <summary>UI THREAD: applies what the worker read.</summary>
        private void ApplyScan(Dictionary<int, string[]> work,
                               Dictionary<int, int[]> freshValues,
                               Dictionary<int, string> failures)
        {
            if (_targetControl == null || _targetControl.IsDisposed) return;

            // ---- EVERY station with tags is scanned independently ----
            // A failing station only affects its own controls: station 1 being down
            // no longer starves the simulation stations (the old behaviour).
            var lookups = new Dictionary<int, Func<string, int>>();
            bool allHealthy = true;
            bool station1Fresh = false;

            foreach (var kv in work)
            {
                int plc = kv.Key;
                string[] batch = kv.Value;
                if (batch == null || batch.Length == 0) continue;

                int[] fresh;
                bool ok = freshValues.TryGetValue(plc, out fresh);
                // Only stations whose values are on the page decide the banner.
                bool onPage = _displayStations.Contains(plc);
                if (!ok && failures.ContainsKey(plc) && failures[plc] == "no driver registered")
                {
                    if (onPage) allHealthy = false;
                    LogStation(plc, false, "no driver registered");
                    continue;
                }

                if (ok)
                {
                    _plcResults[plc] = fresh;
                    _plcLastGood[plc] = fresh;
                    LogStation(plc, true, batch.Length + " tags");
                    if (plc == 1) station1Fresh = true;
                }
                else
                {
                    if (onPage) allHealthy = false;
                    string why;
                    LogStation(plc, false, failures.TryGetValue(plc, out why) ? why : "read failed");
                    // stale-but-real beats lying zeros; nothing at all -> no update
                    int[] lastGood;
                    if (_plcLastGood.TryGetValue(plc, out lastGood) && lastGood.Length == batch.Length)
                        _plcResults[plc] = lastGood;
                    else
                    {
                        _plcResults.Remove(plc);
                        continue;
                    }
                }

                var capturedPlc = plc;
                lookups[plc] = (addr) =>
                {
                    if (string.IsNullOrWhiteSpace(addr)) return 0;
                    // A DEVICE OF ANOTHER CONTROLLER ("X12@2"): that controller's
                    // values - the control reading it need not know (12 October).
                    int at; string device;
                    if (PlcAddressHelper.TrySplitStation(addr, out device, out at))
                    {
                        if (at != capturedPlc)
                        {
                            Func<string, int> there;
                            if (lookups.TryGetValue(at, out there)) return there(device);
                            // not read there yet: into that station's batch
                            HashSet<string> dynThere;
                            string clean = device.Trim().ToUpper();
                            if (!_dynamicAddresses.TryGetValue(at, out dynThere))
                                _dynamicAddresses[at] = dynThere = new HashSet<string>();
                            if (!dynThere.Contains(clean)) { dynThere.Add(clean); _needsRebuild = true; }
                            return 0;
                        }
                        addr = device;
                    }
                    string cleanAddr = addr.Trim().ToUpper();

                    Dictionary<string, int> map;
                    int[] results;
                    if (_plcIndexMap.TryGetValue(capturedPlc, out map) &&
                        _plcResults.TryGetValue(capturedPlc, out results))
                    {
                        int idx;
                        if (map.TryGetValue(cleanAddr, out idx) && idx < results.Length)
                            return results[idx];
                    }

                    // AUTO-HEALING: unknown tag -> queue for this STATION's batch
                    HashSet<string> dyn;
                    if (!_dynamicAddresses.TryGetValue(capturedPlc, out dyn))
                        _dynamicAddresses[capturedPlc] = dyn = new HashSet<string>();
                    if (!dyn.Contains(cleanAddr)) { dyn.Add(cleanAddr); _needsRebuild = true; }
                    return 0;
                };
            }

            SetCommHealth(allHealthy);

            // ---- update every control from ITS OWN station's data ----
            Action<int, Action<Func<string, int>>> withLookup = (plc, apply) =>
            {
                Func<string, int> lk;
                if (lookups.TryGetValue(plc, out lk)) apply(lk);
                // no data for that station yet -> control keeps its state (fail-closed)
            };

            // Conditions read the station they are registered on (see AddConditions).
            Func<int, Func<string, int>> lookupFor = st =>
            {
                Func<string, int> lk;
                return lookups.TryGetValue(st, out lk) ? lk : null;
            };
            Func<SystemCondition, bool> checkGlobal = cond => CheckGlobalCondition(cond, lookupFor);
            LastConditionCheck = checkGlobal;

            foreach (var num in _numerics) withLookup(num.PlcNumber, lk => num.UpdateState(lk, checkGlobal: checkGlobal));
            foreach (var btn in _buttons) withLookup(btn.PlcNumber, lk => btn.UpdateState(lk, checkGlobal: checkGlobal));
            foreach (var lamp in _lamps) withLookup(lamp.PlcNumber, lk => lamp.UpdateState(lk, checkGlobal: checkGlobal));
            foreach (var other in _others)
            {
                var bound = other as IPlcBoundControl;
                var conditional = other as IConditionalControl;
                int plc = bound != null ? bound.PlcNumber : conditional != null ? conditional.PlcNumber : 1;
                var target = other;
                withLookup(plc, lk => target.UpdateState(lk, checkGlobal: checkGlobal));
            }
            foreach (var cl in _checkLists) withLookup(cl.PlcNumber, lk => cl.UpdateState(lk, checkGlobal));
            foreach (var cb in _checkBoxes) withLookup(cb.PlcNumber, lk => cb.UpdateState(lk, checkGlobal));
            foreach (var cmb in _combos) withLookup(cmb.PlcNumber, lk => cmb.UpdateState(lk, checkGlobal));

            // LAST, after every UpdateState. Run in the middle of the loop, it was
            // undone by the checkboxes and combos updating after it.
            ApplyConnectionState();

            // Alarms only ever on FRESH data - never raise or clear an alarm from
            // stale/failed reads - and each station's alarms from that station.
            foreach (var kvFresh in freshValues)
            {
                Func<string, int> lk;
                if (!lookups.TryGetValue(kvFresh.Key, out lk)) continue;
                // The HMI's own alarms FIRST: the PLC's bit for the same device is then
                // kept off (or let back on) in the same scan, never shown for a scan.
                try { AlarmGeneration.Evaluate(kvFresh.Key, lk, DateTime.Now); }
                catch (Exception ex) { AuditLogger.Log("alarm generation: " + ex.Message); }
                AlarmManager.Evaluate(kvFresh.Key, lk);
                try { PlcPopups.Evaluate(kvFresh.Key, lk, DateTime.Now); }
                catch (Exception ex) { AuditLogger.Log("PLC messages: " + ex.Message); }
            }

            if (station1Fresh && lookups.ContainsKey(1))
            {
                // Machine-error bit: drives the pulsing Alarms menu entry and the
                // caption's reset button. Only ever updated from FRESH station-1 data.
                var shell = _targetControl == null ? null : _targetControl.FindForm() as Kaban.UI.Forms.MainForm;
                if (shell != null)
                    shell.SetMachineError(lookups[1](Kaban.UI.Forms.MainForm.MachineErrorAddress) != 0);
            }

            // If a dynamic offset revealed a missing address, seamlessly rebuild the batch!
            if (_needsRebuild) RefreshTags();
        }

        /// <summary>Per-station scan-state TRANSITION log (never spams a steady state).</summary>
        private void LogStation(int plc, bool healthy, string detail)
        {
            bool prev;
            if (_plcHealthy.TryGetValue(plc, out prev) && prev == healthy) return;
            _plcHealthy[plc] = healthy;
            AuditLogger.Log($"[PLC DIAG] station {plc} scan {(healthy ? "OK" : "FAILED")} ({detail}).");
        }

        private void SetCommHealth(bool healthy)
        {
            if (CommunicationHealthy == healthy) return;
            CommunicationHealthy = healthy;

            AuditLogger.Log(healthy ? "PLC communication restored." : "PLC COMMUNICATION LOST.");
            CommunicationStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

