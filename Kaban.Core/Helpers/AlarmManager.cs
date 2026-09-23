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
        /// </summary>
        public static void Evaluate(Func<string, int> lookup)
        {
            if (lookup == null || _definitions.Count == 0) return;

            foreach (var def in _definitions.Values)
            {
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
                    var item = new AlarmItem(def.AlarmCode, def.TranslationKey, def.Severity);
                    ActiveAlarms.Add(item);
                    AddToHistory(item);
                    AlarmsChanged?.Invoke(null, EventArgs.Empty);
                }
                else if (!isActive && currentlyShown)
                {
                    RemoveActive(def.AlarmCode);
                    AlarmsChanged?.Invoke(null, EventArgs.Empty);
                }
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
            for (int i = ActiveAlarms.Count - 1; i >= 0; i--)
            {
                var code = ActiveAlarms[i].AlarmCode ?? "";
                if (!code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                ActiveAlarms.RemoveAt(i);
                any = true;
            }
            if (any)
            {
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

