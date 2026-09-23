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

        public void RefreshTags(bool clearDynamicCache = false)
        {
            if (clearDynamicCache) _dynamicAddresses.Clear();

            _timer.Stop();

            _numerics.Clear(); _lamps.Clear(); _buttons.Clear(); _checkLists.Clear(); _checkBoxes.Clear(); _combos.Clear(); _others.Clear();
            _plcIndexMap.Clear();

            FindControls(_targetControl);

            // Addresses are collected PER STATION: curPlc is set to each control's
            // PlcNumber before its addresses are added, so every station gets its
            // own batch instead of everything landing on station 1.
            var perPlc = new Dictionary<int, HashSet<string>>();
            int curPlc = 1;

            // --- DEEP EXTRACTION HELPERS ---
            Action<string> Add = (s) =>
            {
                if (string.IsNullOrWhiteSpace(s)) return;
                if (!perPlc.ContainsKey(curPlc)) perPlc[curPlc] = new HashSet<string>();
                perPlc[curPlc].Add(s.Trim().ToUpper());
            };

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

            Action<SystemCondition> AddConditions = (cond) =>
            {
                if (cond == SystemCondition.None) return;
                long val = (long)cond;

                // 1. Extract Enum Bits (0-15)
                foreach (SystemCondition flag in Enum.GetValues(typeof(SystemCondition)))
                {
                    if (flag != SystemCondition.None && cond.HasFlag(flag))
                    {
                        var rule = MachineConditionMap.GetRule(flag);
                        if (rule != null && !string.IsNullOrEmpty(rule.Address))
                        {
                            Add(rule.Address);
                            if (rule.DataType == DataTypeEnum.Bin_32) Add(PlcAddressHelper.GetNextAddress(rule.Address));
                        }
                    }
                }

                // 2. Extract Machine Bits (16-63)
                foreach (var bit in MachineConditionMap.GetAllMachineBits())
                {
                    if ((val & bit.Key) != 0)
                    {
                        Add(bit.Value.Address);
                        // Automatically poll the next address if the hardware tag is 32-bit
                        if (bit.Value.DataType == DataTypeEnum.Bin_32)
                        {
                            Add(PlcAddressHelper.GetNextAddress(bit.Value.Address));
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
            foreach (var cb in _checkBoxes) { curPlc = cb.PlcNumber; Add(cb.Address); }
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
                var bound = o as IPlcBoundControl;
                if (bound == null || string.IsNullOrWhiteSpace(bound.Address)) continue;
                curPlc = bound.PlcNumber;
                Add(bound.Address);
                Add(PlcAddressHelper.GetNextAddress(bound.Address));
            }

            // Every registered alarm condition is polled on each scan, so
            // AlarmManager evaluates real PLC bits rather than a cached snapshot.
            curPlc = 1;   // alarm definitions monitor the main PLC
            Add(Kaban.UI.Forms.MainForm.MachineErrorAddress);   // machine-error summary bit
            foreach (var alarmDef in AlarmManager.GetAllDefinitions())
            {
                if (string.IsNullOrEmpty(alarmDef.Address)) continue;
                Add(alarmDef.Address);
                if (alarmDef.DataType == DataTypeEnum.Bin_32) Add(PlcAddressHelper.GetNextAddress(alarmDef.Address));
            }

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
            // Buttons and numeric fields are NOT handled here. They grey through
            // their own soft enable, which reads ConnectionMonitor directly - so it
            // keeps their hover tooltip working, which a real Enabled = false
            // would switch off.
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

        private bool CheckGlobalCondition(SystemCondition required, Func<string, int> lookup)
        {
            if (required == SystemCondition.None) return true;
            long val = (long)required;
            bool result = true;

            // Tier 1: System Enum Logic (Bits 0-15)
            foreach (SystemCondition flag in Enum.GetValues(typeof(SystemCondition)))
            {
                if (flag != SystemCondition.None && required.HasFlag(flag))
                {
                    if (flag == SystemCondition.AdminUser)
                    {
                        result &= SecurityManager.HasAccess(SecurityLevel.Admin);
                        continue;
                    }

                    var rule = MachineConditionMap.GetRule(flag);
                    if (rule != null && !string.IsNullOrEmpty(rule.Address))
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
                    result &= MachineConditionMap.CheckMachineBit(bit.Key, lookup);
                }
            }

            return result;
        }

        // Reconnect throttling: try roughly every 2 seconds, not on every scan.
        // Counted PER STATION in _plcReconnectTicks - one shared counter made
        // a healthy station's scans reset a failing station's back-off.
        private const int ReconnectEveryTicks = 10;

        private bool _scanBusy;

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
            _scanBusy = true;

            // snapshot the batches so the worker never touches collections the UI edits
            var work = new Dictionary<int, string[]>(_plcBatch);
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                Dictionary<int, int[]> fresh;
                Dictionary<int, string> failed;
                ReadAllStations(work, out fresh, out failed);
                try
                {
                    if (_targetControl != null && !_targetControl.IsDisposed && _targetControl.IsHandleCreated)
                        _targetControl.BeginInvoke(new Action(() =>
                        {
                            try { ApplyScan(work, fresh, failed); }
                            finally { _scanBusy = false; }
                        }));
                    else _scanBusy = false;
                }
                catch { _scanBusy = false; }
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
                try
                {
                    ok = driver.IsConnected && driver.TryReadMulti(batch, out values);
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
                // runs - and it costs one word when it is read at all.
                var verDev = ConnectionMonitor.GetVersionDevice(plc);
                if (ok && !string.IsNullOrEmpty(verDev))
                {
                    int raw;
                    try { if (driver.TryRead(verDev, out raw)) ConnectionMonitor.ReportVersion(plc, raw); }
                    catch { }
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
                if (!ok && failures.ContainsKey(plc) && failures[plc] == "no driver registered")
                {
                    allHealthy = false;
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
                    allHealthy = false;
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

            foreach (var num in _numerics) withLookup(num.PlcNumber, lk => num.UpdateState(lk, checkGlobal: (cond) => CheckGlobalCondition(cond, lk)));
            foreach (var btn in _buttons) withLookup(btn.PlcNumber, lk => btn.UpdateState(lk, checkGlobal: (cond) => CheckGlobalCondition(cond, lk)));
            foreach (var lamp in _lamps) withLookup(lamp.PlcNumber, lk => lamp.UpdateState(lk, checkGlobal: (cond) => CheckGlobalCondition(cond, lk)));
            foreach (var other in _others)
            {
                var bound = other as IPlcBoundControl;
                int plc = bound != null ? bound.PlcNumber : 1;
                var target = other;
                withLookup(plc, lk => target.UpdateState(lk, checkGlobal: (cond) => CheckGlobalCondition(cond, lk)));
            }
            foreach (var cl in _checkLists) withLookup(cl.PlcNumber, lk => cl.UpdateState(lk, (cond) => CheckGlobalCondition(cond, lk)));
            foreach (var cb in _checkBoxes) withLookup(cb.PlcNumber, lk => cb.UpdateState(lk, (cond) => CheckGlobalCondition(cond, lk)));
            foreach (var cmb in _combos) withLookup(cmb.PlcNumber, lk => cmb.UpdateState(lk, (cond) => CheckGlobalCondition(cond, lk)));

            // LAST, after every UpdateState. Run in the middle of the loop, it was
            // undone by the checkboxes and combos updating after it.
            ApplyConnectionState();

            // Alarms watch the MAIN PLC and only ever on FRESH data - never raise or
            // clear an alarm from stale/failed reads.
            if (station1Fresh && lookups.ContainsKey(1))
            {
                AlarmManager.Evaluate(lookups[1]);

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

