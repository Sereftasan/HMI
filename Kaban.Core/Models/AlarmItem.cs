using System;

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

        // This property will dynamically grab the translated text for the Grid!
        public string Description
        {
            get { return Kaban.Core.Helpers.LanguageManager.GetString(TranslationKey, TranslationKey); }
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
