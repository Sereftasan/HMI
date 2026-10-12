using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Kaban.Core.Interfaces;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// THE PLC'S MESSAGES TO THE OPERATOR (8 October): the PLC writes a message's
    /// number to its machine's PopupTriggerAddress (FA2010: D71); the HMI shows that
    /// message (its title, its text, its values, its buttons - the machine's
    /// Popups.json, in every language); a button writes its answer to the machine's
    /// PopupResponseAddress (FA2010: D72) and then 0 to the trigger. A message with
    /// no button stays until the PLC writes 0 itself ("Axes are referencing...").
    ///
    /// The answers: Cancel / No = 0, OK / Yes / Continue = 1, Restart / Retry = 2 -
    /// a button may carry a code of its own.
    ///
    /// Both devices are a property of the machine's ladder, declared by its module
    /// beside its program-version device (RegisterPlc), not a setting.
    /// </summary>
    public static class PlcPopups
    {
        // ---- the machine's devices -------------------------------------------------------

        public sealed class Devices
        {
            public int Station;
            public string ModuleId = "";
            /// <summary>Where the PLC writes the message's number (0: none).</summary>
            public string Trigger = "";
            /// <summary>Where the HMI writes the answer.</summary>
            public string Response = "";
        }

        private static readonly Dictionary<int, Devices> _devices = new Dictionary<int, Devices>();

        /// <summary>Declares a station's message devices (PopupTriggerAddress, PopupResponseAddress).</summary>
        public static void SetDevices(int station, string triggerAddress, string responseAddress, string moduleId = "")
        {
            lock (_devices)
            {
                if (string.IsNullOrWhiteSpace(triggerAddress)) { _devices.Remove(station); return; }
                _devices[station] = new Devices
                {
                    Station = station, ModuleId = moduleId ?? "",
                    Trigger = triggerAddress.Trim().ToUpperInvariant(),
                    Response = (responseAddress ?? "").Trim().ToUpperInvariant()
                };
            }
        }

        public static Devices DevicesOf(int station)
        {
            lock (_devices) { Devices d; return _devices.TryGetValue(station, out d) ? d : null; }
        }

        public static IList<Devices> AllDevices { get { lock (_devices) return _devices.Values.ToList(); } }

        /// <summary>The triggers to read on every scan, per station.</summary>
        public static IEnumerable<KeyValuePair<int, string>> Addresses()
        {
            foreach (var d in AllDevices) yield return new KeyValuePair<int, string>(d.Station, d.Trigger);
        }

        // ---- the messages ---------------------------------------------------------------

        public sealed class Button
        {
            /// <summary>OK, Cancel, Yes, No, Continue, Restart, Retry.</summary>
            public string Kind { get; set; } = "OK";
            /// <summary>The answer written; null: the kind's own (Cancel / No 0, OK / Yes / Continue 1, Restart / Retry 2).</summary>
            public int? Code { get; set; }
            /// <summary>Its caption, per language, when not the kind's own.</summary>
            public Dictionary<string, string> Text { get; set; }
            public int Answer { get { return Code ?? DefaultCode(Kind); } }
        }

        public sealed class Field
        {
            /// <summary>Value (a number: Address, Address2 beside it), Lamp (a bit), Axes (the machine's axes: needs reference / fault).</summary>
            public string Kind { get; set; } = "Value";
            public Dictionary<string, string> Label { get; set; }
            public string Address { get; set; } = "";
            public string Address2 { get; set; } = "";
            public double Divisor { get; set; } = 1;
            public int Decimals { get; set; }
            public string Unit { get; set; } = "";
        }

        public sealed class Popup
        {
            public int Code { get; set; }
            /// <summary>Info, Question, Warning, Error.</summary>
            public string Icon { get; set; } = "Info";
            public Dictionary<string, string> Title { get; set; }
            public Dictionary<string, string> Text { get; set; }
            public List<Button> Buttons { get; set; } = new List<Button>();
            public List<Field> Fields { get; set; } = new List<Field>();
            /// <summary>The values' heading ("Defined / Measured").</summary>
            public Dictionary<string, string> FieldHeads { get; set; }
            public string Comment { get; set; }
            /// <summary>Set when loaded.</summary>
            public int Station;
            public string ModuleId = "";

            public string TitleIn(string lang) { return In(Title, lang); }
            public string TextIn(string lang) { return In(Text, lang); }
            public string HeadsIn(string lang) { return In(FieldHeads, lang); }
        }

        private sealed class FileShape
        {
            public List<Popup> Popups { get; set; }
        }

        /// <summary>A text in a language, else English, else any.</summary>
        public static string In(Dictionary<string, string> texts, string lang)
        {
            if (texts == null || texts.Count == 0) return "";
            string s;
            if (!string.IsNullOrEmpty(lang) && texts.TryGetValue(lang.ToUpperInvariant(), out s) && !string.IsNullOrWhiteSpace(s)) return s;
            if (texts.TryGetValue("EN", out s) && !string.IsNullOrWhiteSpace(s)) return s;
            return texts.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
        }

        /// <summary>The answer of a button kind: Cancel / No 0, OK / Yes / Continue 1, Restart / Retry 2.</summary>
        public static int DefaultCode(string kind)
        {
            switch ((kind ?? "").Trim().ToLowerInvariant())
            {
                case "cancel": case "no": return 0;
                case "restart": case "retry": return 2;
                default: return 1;                                  // ok, yes, continue
            }
        }

        private static readonly Dictionary<int, Dictionary<int, Popup>> _popups = new Dictionary<int, Dictionary<int, Popup>>();

        /// <summary>Reads a machine's Popups.json for a station; the number of messages read.</summary>
        public static int Load(string path, int station, string moduleId = "")
        {
            var read = new Dictionary<int, Popup>();
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var options = new JsonSerializerOptions
                    {
                        ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, PropertyNameCaseInsensitive = true
                    };
                    var shape = JsonSerializer.Deserialize<FileShape>(File.ReadAllText(path), options);
                    foreach (var p in shape != null && shape.Popups != null ? shape.Popups : new List<Popup>())
                    {
                        if (p == null || p.Code == 0) continue;
                        p.Station = station;
                        p.ModuleId = moduleId ?? "";
                        if (p.Buttons == null) p.Buttons = new List<Button>();
                        if (p.Fields == null) p.Fields = new List<Field>();
                        read[p.Code] = p;
                    }
                }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the PLC messages (" + path + "): " + ex.Message); }
            lock (_popups) _popups[station] = read;
            return read.Count;
        }

        /// <summary>A station's message, or null (a number the machine's file does not have).</summary>
        public static Popup Find(int station, int code)
        {
            lock (_popups)
            {
                Dictionary<int, Popup> map;
                Popup p;
                return _popups.TryGetValue(station, out map) && map.TryGetValue(code, out p) ? p : null;
            }
        }

        /// <summary>A station's messages, in number order.</summary>
        public static IList<Popup> All(int station)
        {
            lock (_popups)
            {
                Dictionary<int, Popup> map;
                return _popups.TryGetValue(station, out map) ? map.Values.OrderBy(p => p.Code).ToList() : new List<Popup>();
            }
        }

        // ---- what the PLC asks now --------------------------------------------------------

        /// <summary>The PLC asked for a message (station, number), or took it back (number 0).</summary>
        public static event Action<int, int> Requested;

        private sealed class State
        {
            public int Shown;
            /// <summary>Answered: that number is not shown again until the trigger is seen at 0 (or a while has passed).</summary>
            public int Answered;
            public DateTime AnsweredAt;
        }

        private static readonly Dictionary<int, State> _state = new Dictionary<int, State>();

        /// <summary>How long a number just answered is not shown again while the PLC has not yet seen the 0.</summary>
        public static TimeSpan AnswerGrace = TimeSpan.FromSeconds(3);

        /// <summary>The number on show for a station (0: none).</summary>
        public static int Shown(int station)
        {
            lock (_state) { State s; return _state.TryGetValue(station, out s) ? s.Shown : 0; }
        }

        /// <summary>
        /// One scan of a station (fresh values only): the trigger's number shown, or
        /// taken down when the PLC wrote 0 (or another number).
        /// </summary>
        public static void Evaluate(int station, Func<string, int> lookup, DateTime now)
        {
            var d = DevicesOf(station);
            if (d == null || lookup == null) return;
            int v;
            try { v = lookup(d.Trigger); } catch { return; }
            int changedTo = -1;
            lock (_state)
            {
                State s;
                if (!_state.TryGetValue(station, out s)) _state[station] = s = new State();
                if (v == 0) s.Answered = 0;
                // just answered: the PLC (or this scan) has not seen the 0 yet
                if (v != 0 && v == s.Answered && now - s.AnsweredAt < AnswerGrace) v = 0;
                if (v != s.Shown) { s.Shown = v; changedTo = v; }
            }
            if (changedTo >= 0)
            {
                AuditLogger.Log(changedTo == 0 ? "PLC message on station " + station + " closed."
                                               : "PLC message " + changedTo + " on station " + station + " shown.");
                var h = Requested;
                if (h != null) h(station, changedTo);
            }
        }

        /// <summary>
        /// The operator's answer: written to the response device, then 0 to the
        /// trigger. False (with the reason) when a write failed - the message stays.
        /// </summary>
        public static bool Answer(int station, int number, int answer, IPlcDriver driver, string who, out string error)
        {
            error = "";
            var d = DevicesOf(station);
            if (d == null) { error = "no message devices"; return false; }
            if (driver == null) { error = "the PLC does not answer"; return false; }
            try
            {
                if (d.Response.Length > 0 && driver.Write(d.Response, answer) != 0) { error = "could not write " + d.Response; return false; }
                if (driver.Write(d.Trigger, 0) != 0) { error = "could not write " + d.Trigger; return false; }
            }
            catch (Exception ex) { error = ex.Message; return false; }
            lock (_state)
            {
                State s;
                if (!_state.TryGetValue(station, out s)) _state[station] = s = new State();
                s.Answered = number;
                s.AnsweredAt = DateTime.Now;
                s.Shown = 0;
            }
            AuditLogger.Log("PLC message " + number + " on station " + station + " answered " + answer +
                            (d.Response.Length > 0 ? " (" + d.Response + ")" : "") + " by " + (string.IsNullOrEmpty(who) ? "nobody signed in" : who) + ".");
            return true;
        }

        /// <summary>Forgets what was shown (tests, a restart of the scanner).</summary>
        public static void ResetState() { lock (_state) _state.Clear(); }
    }
}
