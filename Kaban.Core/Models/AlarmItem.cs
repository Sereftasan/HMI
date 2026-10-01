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
