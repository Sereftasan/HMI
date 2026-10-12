using System;
using System.Collections.Generic;

namespace Kaban.Core.Models
{
    public enum AlarmSeverity
    {
        Warning,
        Fault,
        Critical
    }

    public class AlarmItem
    {
        public string AlarmCode { get; set; }
        public string TranslationKey { get; set; }
        public AlarmSeverity Severity { get; set; }
        public DateTime TriggerTime { get; set; }

        // ---- an alarm the HMI raised itself (AlarmGeneration) --------------------

        /// <summary>Raised by the HMI from what it read (a movement not confirmed in time, a drive's code), not by a PLC bit.</summary>
        public bool Generated { get; set; }
        /// <summary>The station it is about.</summary>
        public int Station { get; set; }
        /// <summary>The device it is named by: the movement's error bit (or its command bit), the axis's error bit.</summary>
        public string Address { get; set; } = "";
        /// <summary>WHICH failure: "1", "2"... for a cylinder movement, the drive's code ("16.3", "1904") for an axis.</summary>
        public string SubCode { get; set; } = "";
        /// <summary>
        /// The PLC device the HMI SETs when it raises the alarm - the movement's or
        /// the axis's error bit (M844, M801) - so the PLC knows about it; "" when the
        /// unit or axis has none (its command bit is never written for an alarm).
        /// </summary>
        public string Device { get; set; } = "";
        /// <summary>What failed, in words, after the alarm's own text (as it was raised).</summary>
        public string Detail { get; set; } = "";
        /// <summary>The same in the language on show now, when the raiser can say it again; else Detail.</summary>
        public Func<string> DetailText { get; set; }
        /// <summary>The definition's code it belongs to (its PLC bit's alarm), or "".</summary>
        public string BaseCode { get; set; } = "";
        /// <summary>The movement's devices when it was raised (address upper-case, 1 on / 0 off).</summary>
        public Dictionary<string, int> Snapshot { get; set; }
        /// <summary>The cylinder side ("A" / "B") a generated movement alarm is about, or "".</summary>
        public string Side { get; set; } = "";

        /// <summary>A generated alarm's code without what failed - its device's alarm code, else the unit's or axis's ("FA2010.Axis_16").</summary>
        public string GroupCode { get; set; } = "";

        // ---- the alarm lists' columns (8 October: the list and its history alike) ----

        /// <summary>
        /// The Code column: the alarm's code, the same kind for every alarm - a PLC
        /// bit's (FA2010.Robot_X_Error), and a generated one's device's or unit's
        /// (what failed is the Cause, its device and sub-number the Address).
        /// </summary>
        public string ListCode
        {
            get
            {
                if (!Generated) return AlarmCode;
                if (!string.IsNullOrEmpty(BaseCode)) return BaseCode;
                return !string.IsNullOrEmpty(GroupCode) ? GroupCode : AlarmCode;
            }
        }

        /// <summary>The Alarm column: the alarm's own text, without what failed.</summary>
        public string Text { get { return Kaban.Core.Helpers.LanguageManager.GetString(TranslationKey, TranslationKey); } }

        /// <summary>The Cause column: what failed, in the language on show ("" for a PLC bit's alarm).</summary>
        public string Cause
        {
            get
            {
                string detail = Detail;
                if (DetailText != null) try { detail = DetailText() ?? Detail; } catch { detail = Detail; }
                return detail ?? "";
            }
        }

        /// <summary>
        /// The Address column (Admin): the PLC device - for a generated alarm its
        /// error bit alone (M801), not the code of what failed (8 October: "52.1 is
        /// just the alarm code, it is nothing to do with the device address").
        /// </summary>
        public string AddressShown
        {
            get { return Generated ? (!string.IsNullOrEmpty(Device) ? Device : (Address ?? "")) : (Address ?? ""); }
        }

        /// <summary>The code the lists show: "M840.2" for a generated alarm, the alarm's code otherwise.</summary>
        public string ShownCode
        {
            get { return Generated && !string.IsNullOrEmpty(Address) ? Address + (string.IsNullOrEmpty(SubCode) ? "" : "." + SubCode) : AlarmCode; }
        }

        // This property will dynamically grab the translated text for the Grid!
        public string Description
        {
            get
            {
                var text = Kaban.Core.Helpers.LanguageManager.GetString(TranslationKey, TranslationKey);
                string detail = Detail;
                if (DetailText != null) try { detail = DetailText() ?? Detail; } catch { detail = Detail; }
                return string.IsNullOrEmpty(detail) ? text : (string.IsNullOrEmpty(text) ? detail : text + " - " + detail);
            }
        }

        public AlarmItem(string code, string transKey, AlarmSeverity severity)
        {
            AlarmCode = code;
            TranslationKey = transKey;
            Severity = severity;
            TriggerTime = DateTime.Now;
        }
    }
}
