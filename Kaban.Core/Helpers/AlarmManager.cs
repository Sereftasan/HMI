using System;
using System.Collections.Generic;
using System.ComponentModel;
using Kaban.Core.Enums;
using Kaban.Core.Models;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// Definition of one monitored alarm condition: which PLC bit/word to watch,
    /// what value trips it, and how it should be displayed.
    /// </summary>
    public class AlarmDefinition : ConditionRule
    {
        public string AlarmCode { get; set; }
        public string TranslationKey { get; set; }
        public AlarmSeverity Severity { get; set; }

        /// <summary>
        /// Troubleshooting codes for this alarm - see TroubleshootingCatalog.
        ///
        /// A LIST, because one alarm can have more than one thing worth reading:
        /// a cylinder that will not extend is a valve question AND a sensor
        /// question, and the operator should be offered both rather than whichever
        /// one somebody happened to enter first.
        ///
        /// The JSON loader has always carried these; they simply never reached the
        /// alarm the screen displays.
        /// </summary>
        public List<string> TroubleshootingKeys { get; set; }

        /// <summary>
        /// Documents named outright on this alarm, for the cases the codes do not
        /// cover - a drawing, a supplier note, a one-off procedure.
        /// </summary>
        public List<string> HelperKeys { get; set; }

        /// <summary>
        /// How long (seconds) a movement may take before the HMI raises this
        /// alarm itself (AlarmGeneration) - for a cylinder unit's error bit; null:
        /// the machine's general delay.
        /// </summary>
        public double? GenerationDelay { get; set; }
    }

    /// <summary>
    /// Central, shared alarm engine.
    ///
    /// Previously each machine's AlarmScreen generated three hardcoded fake
    /// alarms and never looked at the PLC at all. This class replaces that:
    /// register real alarm conditions once (see RegisterDefinition), then call
    /// Evaluate(...) on every scan tick (already wired into PlcScanner) to
    /// raise/clear alarms from live PLC data. AlarmScreen now just displays
    /// ActiveAlarms instead of inventing its own list.
    ///
    /// *** IMPORTANT ***
    /// The alarm conditions themselves (which PLC address maps to which fault)
    /// are specific to your machine's PLC program, which isn't available from
    /// the HMI source alone. RegisterDefaultDefinitions() below only contains
    /// clearly-marked EXAMPLE entries - replace them with your real alarm bit
    /// addresses before relying on this in production.
    /// </summary>
    public static class AlarmManager
    {
        private static readonly Dictionary<string, AlarmDefinition> _definitions =
            new Dictionary<string, AlarmDefinition>(StringComparer.OrdinalIgnoreCase);

        // Currently tripped alarms - bind grids directly to this.
        public static BindingList<AlarmItem> ActiveAlarms { get; } = new BindingList<AlarmItem>();

        // Bounded history of everything that has ever fired, most recent first.
        public static List<AlarmItem> History { get; } = new List<AlarmItem>();
        private const int MaxHistory = 500;

        public static event EventHandler AlarmsChanged;

        /// <summary>
        /// A PLC-bit alarm kept off the list while the HMI shows its own alarm for
        /// the same device (AlarmGeneration): one fault, one line - the HMI's names
        /// which signal failed. Null: nothing kept off.
        /// </summary>
        public static Func<AlarmDefinition, bool> Suppressed;

        /// <summary>Adds an alarm the HMI raised itself (AlarmGeneration) to the list and the history.</summary>
        public static void RaiseGenerated(AlarmItem item)
        {
            if (item == null) return;
            item.Generated = true;
            for (int i = ActiveAlarms.Count - 1; i >= 0; i--)
                if (ActiveAlarms[i].AlarmCode == item.AlarmCode) ActiveAlarms.RemoveAt(i);
            ActiveAlarms.Add(item);
            AddToHistory(item);
            AuditLogger.Log("HMI alarm " + item.ShownCode + " raised: " + item.Description);
            AlarmsChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Takes a generated alarm off the list (its movement succeeded, its code cleared).</summary>
        public static void ClearGenerated(AlarmItem item)
        {
            if (item == null) return;
            bool any = false;
            for (int i = ActiveAlarms.Count - 1; i >= 0; i--)
                if (ReferenceEquals(ActiveAlarms[i], item)) { ActiveAlarms.RemoveAt(i); any = true; }
            if (any) AlarmsChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>True while the item is on the active list (an acknowledgement takes it off).</summary>
        public static bool IsOnList(AlarmItem item)
        {
            if (item == null) return false;
            foreach (var a in ActiveAlarms) if (ReferenceEquals(a, item)) return true;
            return false;
        }

        public static void RegisterDefinition(string alarmCode, string translationKey, AlarmSeverity severity,
            int plcNumber, string address, DataTypeEnum dataType = DataTypeEnum.Bin_16,
            ComparisonOperator op = ComparisonOperator.Equal, int requiredValue = 1,
            List<string> troubleshootingKeys = null, List<string> helperKeys = null)
        {
            _definitions[alarmCode] = new AlarmDefinition
            {
                AlarmCode = alarmCode,
                TranslationKey = translationKey,
                Severity = severity,
                PlcNumber = plcNumber,
                Address = address,
                DataType = dataType,
                Operator = op,
                RequiredValue = requiredValue,
                TroubleshootingKeys = troubleshootingKeys,
                HelperKeys = helperKeys
            };
        }

        public static IEnumerable<AlarmDefinition> GetAllDefinitions() => _definitions.Values;

        /// <summary>
        /// The definition registered under a (scoped) code, or null. Use this rather
        /// than FindByAddress wherever the code is known: FA2010, H500 and H800 use
        /// the SAME addresses (M801...) on different stations, so an address alone
        /// finds whichever machine's alarm happens to come first.
        /// </summary>
        public static AlarmDefinition FindByCode(string alarmCode)
        {
            if (string.IsNullOrEmpty(alarmCode)) return null;
            AlarmDefinition def;
            return _definitions.TryGetValue(alarmCode, out def) ? def : null;
        }

        /// <summary>
        /// Changes the severity an alarm is reported with. Engineers tune this on
        /// site, so it is applied to the live definition rather than requiring the
        /// alarm file to be regenerated.
        /// </summary>
/// <summary>
        /// The definition behind a raised alarm, found by its CODE.
        ///
        /// A raised alarm carries the scoped code ("FA2010.Emergency_Stop"); the
        /// definition is stored under that same key. Screens showing live alarms
        /// need this to reach anything configured against the alarm - severity
        /// overrides, troubleshooting codes, helper files.
        /// </summary>
        public static AlarmDefinition FindByAddressOrCode(string codeOrAddress)
        {
            if (string.IsNullOrEmpty(codeOrAddress)) return null;

            AlarmDefinition def;
            if (_definitions.TryGetValue(codeOrAddress, out def)) return def;

            return FindByAddress(codeOrAddress);
        }

        /// <summary>The definition bound to a device address, or null.</summary>
        public static AlarmDefinition FindByAddress(string plcAddress)
        {
            foreach (var def in _definitions.Values)
                if (string.Equals(def.Address, plcAddress, StringComparison.OrdinalIgnoreCase))
                    return def;
            return null;
        }

        /// <summary>
        /// Stores the troubleshooting code and helper files on the DEFINITION,
        /// which is what gets written back to Alarms.json - so a setting and the
        /// alarm it belongs to are one record rather than two that can drift.
        /// </summary>
        /// <summary>The definition for a device on a given station, or null.</summary>
        public static AlarmDefinition FindByStationAndAddress(int station, string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            // "M915@2": PLC 2's alarm, whichever control names it
            PlcAddressHelper.Resolve(station, address.Trim(), out station, out address);
            foreach (var def in _definitions.Values)
                if (def.PlcNumber == station &&
                    string.Equals((def.Address ?? "").Trim(), address.Trim(), StringComparison.OrdinalIgnoreCase))
                    return def;
            return null;
        }

        /// <summary>Stores an alarm's helper files (";"-separated), leaving anything else alone.</summary>
        public static void SetHelperLinks(string plcAddress, string helperFiles)
        {
            var def = FindByAddress(plcAddress);
            if (def == null) return;
            def.HelperKeys = string.IsNullOrWhiteSpace(helperFiles)
                ? new List<string>() : new List<string> { helperFiles.Trim() };
        }

        public static void SetTroubleshooting(string plcAddress, string code, string helperFiles)
        {
            var def = FindByAddress(plcAddress);
            if (def == null) return;

            def.TroubleshootingKeys = string.IsNullOrWhiteSpace(code)
                ? new List<string>() : new List<string> { code.Trim() };
            def.HelperKeys = string.IsNullOrWhiteSpace(helperFiles)
                ? new List<string>() : new List<string> { helperFiles.Trim() };
        }

        public static bool SetSeverity(string plcAddress, AlarmSeverity severity)
        {
            if (string.IsNullOrWhiteSpace(plcAddress)) return false;
            foreach (var def in _definitions.Values)
                if (string.Equals(def.Address, plcAddress, StringComparison.OrdinalIgnoreCase))
                {
                    def.Severity = severity;
                    return true;
                }
            return false;
        }

        /// <summary>
        /// Re-checks every registered alarm condition against live PLC data.
        /// `lookup` should resolve a PLC address to its last-scanned raw value
        /// (the same delegate PlcScanner already builds for its controls).
        ///
        /// Evaluates EVERY definition against the one lookup, whatever station it
        /// belongs to - only right on a single-PLC machine. The scanner uses the
        /// per-station overload below.
        /// </summary>
        public static void Evaluate(Func<string, int> lookup)
        {
            EvaluateWhere(lookup, null);
        }

        /// <summary>
        /// Re-checks only the alarms that live on ONE station, against that
        /// station's data.
        ///
        /// WHY: the scanner used to read every alarm address from station 1 and
        /// evaluate every definition against it. A mounted sub-machine's alarms
        /// are remapped to their own station (H500 -> 3, H800 -> 4) and share
        /// addresses with the host (M801...), so they were raised and cleared by
        /// the HOST's bits: false alarms on one side, missed alarms on the other.
        /// </summary>
        public static void Evaluate(int station, Func<string, int> lookup)
        {
            EvaluateWhere(lookup, station);
        }

        /// <summary>The station an alarm or condition is read from (0 or less means the main PLC).</summary>
        public static int StationOf(ConditionRule rule)
        {
            return rule == null || rule.PlcNumber <= 0 ? 1 : rule.PlcNumber;
        }

        private static void EvaluateWhere(Func<string, int> lookup, int? station)
        {
            if (lookup == null || _definitions.Count == 0) return;

            // ONE REFRESH PER SCAN, not one per alarm. An E-stop trips a dozen bits
            // in the same scan; each Add/Remove used to repaint every grid bound to
            // this list and fire AlarmsChanged, a dozen times over, on the UI thread.
            bool changed = false;
            bool raise = ActiveAlarms.RaiseListChangedEvents;
            ActiveAlarms.RaiseListChangedEvents = false;
            try
            {
                foreach (var def in _definitions.Values)
                {
                    if (station.HasValue && StationOf(def) != station.Value) continue;

                    // The HMI's own alarm for this device is on show: this line would repeat it.
                    bool kept = false;
                    try { kept = Suppressed != null && Suppressed(def); } catch { kept = false; }
                    if (kept)
                    {
                        if (IsCurrentlyActive(def.AlarmCode)) { RemoveActive(def.AlarmCode); changed = true; }
                        continue;
                    }

                    bool isActive = false;
                    try
                    {
                        int low = lookup(def.Address);
                        double currentVal = low;

                        if (def.DataType == DataTypeEnum.Bin_32)
                        {
                            int high = lookup(PlcAddressHelper.GetNextAddress(def.Address));
                            currentVal = PlcValueConverter.ConvertToDouble(low, high, def.DataType, DisplayFormatEnum.Signed_Decimal, 0);
                        }

                        isActive = ComparisonHelper.Compare(currentVal, def.Operator, def.RequiredValue);
                    }
                    catch
                    {
                        // If we can't evaluate the condition (comm failure, bad address, etc.)
                        // don't silently pretend the alarm is cleared - leave its previous
                        // state alone rather than mask a real problem.
                        continue;
                    }

                    bool currentlyShown = IsCurrentlyActive(def.AlarmCode);

                    if (isActive && !currentlyShown)
                    {
                        // its station and device on the item too (8 October): the lists' Address
                        // column, the AI prompt and the "?" find their alarm by them
                        var item = new AlarmItem(def.AlarmCode, def.TranslationKey, def.Severity)
                        {
                            Station = StationOf(def), Address = (def.Address ?? "").Trim()
                        };
                        ActiveAlarms.Add(item);
                        AddToHistory(item);
                        changed = true;
                    }
                    else if (!isActive && currentlyShown)
                    {
                        RemoveActive(def.AlarmCode);
                        changed = true;
                    }
                }
            }
            finally
            {
                ActiveAlarms.RaiseListChangedEvents = raise;
            }

            if (changed)
            {
                if (raise) ActiveAlarms.ResetBindings();
                AlarmsChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>Operator/Maintenance acknowledgement - clears the visible list without touching history.</summary>
        public static bool AcknowledgeAll(SecurityLevel minimumLevel = SecurityLevel.Operator)
        {
            if (!SecurityManager.HasAccess(minimumLevel)) return false;

            ActiveAlarms.Clear();
            AuditLogger.Log("acknowledged all active alarms.");
            AlarmsChanged?.Invoke(null, EventArgs.Empty);
            return true;
        }

/// <summary>
        /// Clears the active alarms belonging to ONE machine.
        ///
        /// Matched on the "ModuleId." prefix the loader puts on every code. A
        /// machine with sub-machines can have several in alarm at once, and
        /// clearing all of them because one was dealt with hides the rest.
        /// </summary>
        public static bool AcknowledgeMachine(string moduleId,
                                              SecurityLevel minimumLevel = SecurityLevel.Operator)
        {
            if (!SecurityManager.HasAccess(minimumLevel)) return false;
            if (string.IsNullOrEmpty(moduleId)) return false;

            string prefix = moduleId + ".";
            bool any = false;
            // One list refresh for the lot, as in Evaluate.
            bool raise = ActiveAlarms.RaiseListChangedEvents;
            ActiveAlarms.RaiseListChangedEvents = false;
            try
            {
                for (int i = ActiveAlarms.Count - 1; i >= 0; i--)
                {
                    var code = ActiveAlarms[i].AlarmCode ?? "";
                    if (!code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    ActiveAlarms.RemoveAt(i);
                    any = true;
                }
            }
            finally { ActiveAlarms.RaiseListChangedEvents = raise; }
            if (any)
            {
                if (raise) ActiveAlarms.ResetBindings();
                AuditLogger.Log("acknowledged active alarms for " + moduleId + ".");
                AlarmsChanged?.Invoke(null, EventArgs.Empty);
            }
            return any;
        }

        private static bool IsCurrentlyActive(string alarmCode)
        {
            foreach (var a in ActiveAlarms) if (a.AlarmCode == alarmCode) return true;
            return false;
        }

        private static void RemoveActive(string alarmCode)
        {
            for (int i = ActiveAlarms.Count - 1; i >= 0; i--)
            {
                if (ActiveAlarms[i].AlarmCode == alarmCode) ActiveAlarms.RemoveAt(i);
            }
        }

        private static void AddToHistory(AlarmItem item)
        {
            History.Insert(0, item);
            if (History.Count > MaxHistory) History.RemoveAt(History.Count - 1);
        }
    }
}

