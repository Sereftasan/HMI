using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Kaban.Core.Enums;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// STEP BY STEP (30 September): the troubleshooter for ONE movement of ONE
    /// cylinder, written as the operator would work through it at the machine.
    ///
    /// The cylinder manual (Cylinder_Troubleshooting.html) is the general view:
    /// what can fail, how to check it, what to do - for any cylinder, with the
    /// valve and feedback types as alternatives. The unit knows which valve and
    /// feedback it has, its command, the output its command drives, and from the
    /// drawings (WiringTable) the card terminal, the wires, the relay, the coil,
    /// the valve, the hoses, the cylinder and the sensors with their tags. So
    /// this page is not the manual with notes added: it is the sequence itself -
    ///
    ///   1. look where the cylinder is: it did not move / it stopped on the way /
    ///      it arrived but was not confirmed - three different searches;
    ///   2. did not move: follow the signal from the PLC to the cylinder - the
    ///      output, the card, the relay, the coil, the valve, the air, the
    ///      mechanics - each a question with what to press, look at or measure,
    ///      and what each answer means;
    ///   3. stopped on the way: the valve's own rule (a 5/3 or a spring valve
    ///      needs the signal the whole way), the air on the way, the path;
    ///   4. arrived: the sensors (or the pressure switch, the position reading,
    ///      the reference), then the time allowed.
    ///
    /// Each answer either names the fault and its remedy - then test again - or
    /// sends to the next step. The two movements of a cylinder are not mirror
    /// images when its valve or its feedback is not: a spring valve's return is
    /// "the coil must switch OFF", a single sensor's far end is "the sensor must
    /// go OFF - and nothing sees the end", a clamp is confirmed by its pressure
    /// switch while its release is confirmed by the back sensor. The states the
    /// unit saw just before the alarm say where to start.
    ///
    /// TEMPLATES (2 October): the words are no longer in this code. Each valve
    /// and feedback pair has its own page template per language -
    /// Help/Templates/EN/Cylinder_5-3_PressureSwitch.html, .../TR/... - which a
    /// person can read and change; this code fills it with the movement's
    /// devices and the drawings' numbers (StepTemplate), leaves out the steps an
    /// alarm the HMI raised itself does not need, numbers the steps and points
    /// each answer at its step. A language without templates falls back to
    /// English.
    /// </summary>
    public static partial class TroubleshootingCatalog
    {
        /// <summary>
        /// A unit's "no sensor" (CylinderFeedback.NoSensor, 7): nothing confirms the
        /// movement but the time. Not one of the manual's variants (FeedbackType), but
        /// a page of its own - not "the feedback is not set".
        /// </summary>
        public const FeedbackType NoSensor = (FeedbackType)7;

        /// <summary>Where the page templates are: Help/Templates/&lt;language&gt;/.</summary>
        public static string TemplateFolder { get { return Path.Combine(HelpFolder, "Templates"); } }

        /// <summary>The template file of a valve and feedback pair: "Cylinder_5-3_PressureSwitch.html".</summary>
        public static string TemplateName(ValveType valve, FeedbackType feedback)
        {
            string v, f;
            switch ((ValveType)CanonicalValve((int)valve))
            {
                case ValveType.DoubleSolenoid: v = "5-2-Double"; break;
                case ValveType.SingleSolenoidSpring: v = "5-2-Spring"; break;
                case ValveType.ClosedCentre: v = "5-3"; break;
                default: v = "Valve-NotSet"; break;
            }
            switch ((FeedbackType)CanonicalFeedback((int)feedback))
            {
                case FeedbackType.SingleMagnetic: f = "OneMagnetic"; break;
                case FeedbackType.TwoMagnetic: f = "TwoMagnetic"; break;
                case FeedbackType.SingleInductive: f = "OneInductive"; break;
                case FeedbackType.AnalogueMagnetic: f = "Analogue"; break;
                case FeedbackType.PressureSwitch: f = "PressureSwitch"; break;
                case FeedbackType.EncoderReference: f = "Encoder"; break;
                case NoSensor: f = "NoSensor"; break;
                default: f = "Feedback-NotSet"; break;
            }
            return "Cylinder_" + v + "_" + f + ".html";
        }

        /// <summary>The template file used for a pair in a language: the language's own, else English ("" when neither is there).</summary>
        public static string TemplatePath(ValveType valve, FeedbackType feedback, string language)
        {
            var name = TemplateName(valve, feedback);
            foreach (var lang in new[] { (language ?? "").ToUpperInvariant(), "EN" })
            {
                if (lang.Length == 0) continue;
                var p = Path.Combine(TemplateFolder, lang, name);
                try { if (File.Exists(p)) return p; } catch { }
            }
            return "";
        }

        private static readonly Dictionary<string, KeyValuePair<DateTime, StepTemplate>> _templates =
            new Dictionary<string, KeyValuePair<DateTime, StepTemplate>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>A template, read again when its file changed (so an edited template shows at once).</summary>
        public static StepTemplate LoadTemplate(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var stamp = File.GetLastWriteTimeUtc(path);
            lock (_templates)
            {
                KeyValuePair<DateTime, StepTemplate> t;
                if (_templates.TryGetValue(path, out t) && t.Key == stamp) return t.Value;
            }
            var lang = Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
            var tpl = StepTemplate.Load(path, CultureOf(lang));
            lock (_templates) _templates[path] = new KeyValuePair<DateTime, StepTemplate>(stamp, tpl);
            return tpl;
        }

        private static CultureInfo CultureOf(string lang)
        {
            try { return string.Equals(lang, "TR", StringComparison.OrdinalIgnoreCase) ? new CultureInfo("tr-TR") : CultureInfo.InvariantCulture; }
            catch { return CultureInfo.InvariantCulture; }
        }

        /// <summary>Writes the movement's step-by-step page to a temporary file and returns its path ("" when there is no movement).</summary>
        public static string WriteSteps(HelpDocument doc)
        {
            if (doc == null || doc.Movement == null) return "";
            try
            {
                var html = BuildSteps(doc.Movement, (ValveType)CanonicalValve(doc.Valve), (FeedbackType)CanonicalFeedback(doc.Feedback), doc.Failure,
                                      LanguageManager.CurrentLanguage, doc.AlarmTime, doc.AlarmText);
                var path = Path.Combine(Path.GetTempPath(), StepsFileName(doc));
                File.WriteAllText(path, html, new UTF8Encoding(true));
                return path;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("the step-by-step help could not be written: " + ex.Message);
                return doc.FullPath;
            }
        }

        private static string StepsFileName(HelpDocument doc)
        {
            var mv = doc.Movement;
            return "Steps_" + SafeName(mv.Unit) + "_" + (string.IsNullOrEmpty(mv.Side) ? "X" : mv.Side) + (doc.Failure > 0 ? "_" + doc.Failure : "") + ".html";
        }

        /// <summary>
        /// A movement's name in words - "Clamp", "Up" - from its button when the
        /// button has words, else from the drawing's names of its valve outputs
        /// (a button usually shows only a triangle); in the HMI's language when its
        /// templates translate the word.
        /// </summary>
        public static string MovementName(MovementInfo mv)
        {
            if (mv == null) return "";
            try
            {
                var tpl = LoadTemplate(TemplatePath(ValveType.Unspecified, FeedbackType.Unspecified, LanguageManager.CurrentLanguage));
                return new StepsBuilder(Chain.Build(mv, ValveType.Unspecified, FeedbackType.Unspecified), tpl, 0).MoveName;
            }
            catch { return mv.Name ?? ""; }
        }

        /// <summary>
        /// The page, as HTML (public for tests), in the HMI's language - or in the
        /// one given. With a failure (an alarm the HMI raised itself,
        /// HelpDocument.Failure) only the steps about the signal that failed are on it.
        /// </summary>
        public static string BuildSteps(MovementInfo mv, ValveType valve, FeedbackType feedback, int failure = 0)
        {
            return BuildSteps(mv, valve, feedback, failure, LanguageManager.CurrentLanguage);
        }

        public static string BuildSteps(MovementInfo mv, ValveType valve, FeedbackType feedback, int failure, string language)
        {
            return BuildSteps(mv, valve, feedback, failure, language, null, null);
        }

        /// <param name="occurred">When the alarm the help was opened from came (the alarm list's time); null: the unit's own record of it, if any.</param>
        /// <param name="alarmText">That alarm as the list shows it - the page's title; null: the movement's error bit's alarm text.</param>
        public static string BuildSteps(MovementInfo mv, ValveType valve, FeedbackType feedback, int failure, string language,
                                        DateTime? occurred, string alarmText)
        {
            var path = TemplatePath(valve, feedback, language);
            if (path.Length == 0) return MissingTemplatePage(valve, feedback);
            return BuildSteps(mv, valve, feedback, failure, LoadTemplate(path), occurred, alarmText);
        }

        /// <summary>The page from a given template (the tests render the master and each pair's file with it).</summary>
        public static string BuildSteps(MovementInfo mv, ValveType valve, FeedbackType feedback, int failure, StepTemplate template)
        {
            return BuildSteps(mv, valve, feedback, failure, template, null, null);
        }

        public static string BuildSteps(MovementInfo mv, ValveType valve, FeedbackType feedback, int failure, StepTemplate template,
                                        DateTime? occurred, string alarmText)
        {
            var b = new StepsBuilder(Chain.Build(mv, (ValveType)CanonicalValve((int)valve), (FeedbackType)CanonicalFeedback((int)feedback)), template, failure)
            {
                Occurred = occurred, AlarmText = alarmText ?? ""
            };
            return b.Page();
        }

        /// <summary>
        /// WHO READS THE PAGE (2 October): the PLC's internal devices - an alarm's
        /// bit (M873), a command (M695), a data register (D100) - are named on the
        /// step-by-step page only for an Admin; everyone sees the controller's real
        /// inputs and outputs (X34, Y21), which are what is checked at the machine.
        /// The tests and the review pages set it.
        /// </summary>
        public static Func<bool> AdminView = () => SecurityManager.HasAccess(SecurityLevel.Admin);

        /// <summary>The time the page is made at (the tests set it).</summary>
        public static Func<DateTime> Clock = () => DateTime.Now;

        /// <summary>A controller's real input or output (X34, Y21) - not an internal device.</summary>
        public static bool IsPhysicalIo(string address)
        {
            return Regex.IsMatch((address ?? "").Trim(), "^[XYxy][0-9A-Fa-f]+$");
        }

        private static string MissingTemplatePage(ValveType valve, FeedbackType feedback)
        {
            var name = TemplateName(valve, feedback);
            return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Step by step</title></head><body style=\"font-family:Segoe UI,Arial,sans-serif\">" +
                   "<h1>The step-by-step help is not installed</h1><p>The template <code>" + E(name) + "</code> was not found in <code>" +
                   E(TemplateFolder) + "</code> (in the HMI's language or in English). Copy the program's <code>Help\\Templates</code> folder " +
                   "beside the program again; the general cylinder manual still opens from the alarm's help.</p></body></html>";
        }

        // =====================================================================

        /// <summary>A sensor of the movement with its drawing point (null when not drawn).</summary>
        private sealed class Sensor
        {
            public MovementInfo.Device Device;
            public WiringTable.Point Point;
            public bool IsPressureSwitch;

            /// <summary>The address the Inputs / Outputs page shows it by (X70), or the one the unit reads.</summary>
            public string Io { get { return Point != null ? Point.Address : Device.Address; } }
        }

        /// <summary>One step of the page as it came out of the template: where it is, and what it is.</summary>
        private sealed class Block
        {
            public string Key = "", Part = "", Role = "";
            public int Start, End;
        }

        private sealed class StepsBuilder
        {
            private readonly Chain c;
            private readonly MovementInfo mv;
            private readonly StepTemplate t;
            private readonly string _lang;
            private readonly WiringTable.Supply _supply;
            private readonly Dictionary<string, string> _dataWords, _moveWords;

            // what this movement is
            private readonly bool _spring;          // a spring valve's return: the coil must go OFF
            private readonly bool _fiveThree, _double;
            private readonly List<Sensor> _mustOn = new List<Sensor>(), _mustOff = new List<Sensor>();
            private readonly bool _endNotSensed;    // the movement ends where no sensor is (a single sensor's far end)
            private readonly bool _position;        // confirmed by a position reading (analogue / encoder)
            /// <summary>The signal that failed when the HMI raised the alarm itself (AlarmGeneration's sub-code), 0 when not known.</summary>
            private readonly int _failure;
            /// <summary>The reader is an Admin: the internal devices are named (AdminView).</summary>
            private readonly bool _admin;

            /// <summary>When the alarm came (the alarm list's time); null: the unit's own record.</summary>
            public DateTime? Occurred;
            /// <summary>The alarm as the list shows it - the title; "": the error bit's alarm text.</summary>
            public string AlarmText = "";

            public StepsBuilder(Chain chain, StepTemplate template, int failure)
            {
                c = chain;
                t = template;
                _failure = failure;
                try { _admin = AdminView != null && AdminView(); } catch { _admin = false; }
                mv = chain.Mv;
                _lang = template != null && template.Path.Length > 0 ? (Path.GetFileName(Path.GetDirectoryName(template.Path)) ?? "") : "";
                _supply = WiringTable.SupplyOf(mv.Station);
                _double = c.DoubleCoil;
                _fiveThree = c.Valve == ValveType.ClosedCentre || c.Valve == ValveType.OpenCentre;
                _spring = c.Valve == ValveType.SingleSolenoidSpring && c.OutDevice == null && c.OtherDevice != null;
                _dataWords = t != null ? t.Table("DataWords") : new Dictionary<string, string>();
                _moveWords = t != null ? t.Table("MoveWords") : new Dictionary<string, string>();

                var on = c.On.Select(s => ToSensor(s)).ToList();
                var off = c.Off.Select(s => ToSensor(s)).ToList();
                _mustOn.AddRange(on);
                _mustOff.AddRange(off);
                bool single = c.Feedback == FeedbackType.SingleMagnetic || c.Feedback == FeedbackType.SingleInductive;
                _endNotSensed = single && on.Count == 0 && off.Count > 0;
                _position = c.Feedback == FeedbackType.AnalogueMagnetic || c.Feedback == FeedbackType.EncoderReference;
            }

            private Sensor ToSensor(KeyValuePair<MovementInfo.Device, WiringTable.Point> s)
            {
                var sensor = new Sensor { Device = s.Key, Point = s.Value };
                if (c.Feedback == FeedbackType.PressureSwitch)
                {
                    var p = s.Value;
                    var pressure = c.Air != null ? c.Air.Pressure : "";
                    var text = ((p != null ? p.Text : "") + " " + (s.Key.Name ?? "")).ToLowerInvariant();
                    sensor.IsPressureSwitch = text.Contains("pressure switch") || text.Contains("empty switch") ||
                                              (p != null && pressure.Length > 0 &&
                                               (Mentions(pressure, p.Address) || (p.Tag.Length > 0 && Mentions(pressure, p.Tag))));
                }
                return sensor;
            }

            private static bool Mentions(string text, string word)
            {
                return Regex.IsMatch(text, "(?<![A-Za-z0-9.])" + Regex.Escape(word) + "(?![A-Za-z0-9])", RegexOptions.IgnoreCase);
            }

            // ---- words: the template's ----------------------------------------------

            private string Phrase(string name, params object[] keyValues)
            {
                if (t == null) return "";
                var ctx = new Dictionary<string, object>(StringComparer.Ordinal);
                for (int i = 0; i + 1 < keyValues.Length; i += 2) ctx[(string)keyValues[i]] = keyValues[i + 1];
                return t.Phrase(name, ctx);
            }

            /// <summary>A text of the drawings in the page's language: its own column (Measure.TR), else the template's DataWords.</summary>
            private string Data(WiringTable.Point p, string column, string value)
            {
                if (string.IsNullOrEmpty(value)) return "";
                if (p != null && _lang.Length > 0)
                {
                    var own = p.Localized(column, _lang);
                    if (!string.IsNullOrEmpty(own)) return own;
                }
                return Words(value);
            }

            /// <summary>The template's DataWords put in ("double solenoid" = "\u00e7ift bobinli"), longest first.</summary>
            private string Words(string value)
            {
                if (string.IsNullOrEmpty(value) || _dataWords.Count == 0) return value ?? "";
                foreach (var kv in _dataWords.OrderByDescending(k => k.Key.Length))
                    value = Regex.Replace(value, "(?<![A-Za-z])" + Regex.Escape(kv.Key) + "(?![A-Za-z])", kv.Value.Replace("$", "$$"), RegexOptions.IgnoreCase);
                return value;
            }

            private string Unit { get { return string.IsNullOrWhiteSpace(mv.Unit) ? Phrase("ThisUnit") : E(mv.Unit.Trim()); } }

            private string _move, _otherMove;

            /// <summary>The movement in words: "Clamp", "Up" - from the button when it has words, else from the drawing's name of its valve output.</summary>
            private string Move { get { return _move ?? (_move = MovementWord(true)); } }
            private string OtherMove { get { return _otherMove ?? (_otherMove = MovementWord(false)); } }

            /// <summary>The movement in words, for a title (plain text).</summary>
            public string MoveName { get { return System.Net.WebUtility.HtmlDecode(Move); } }

            private static bool IsGlyph(string text)
            {
                var tx = (text ?? "").Trim();
                return tx.Length > 0 && tx.Length <= 3 && !tx.Any(char.IsLetterOrDigit);
            }

            private static readonly string[][] Opposites =
            {
                new[] { "Up", "Down" }, new[] { "Forward", "Back" }, new[] { "Forward", "Backward" }, new[] { "Clamp", "Release" },
                new[] { "Open", "Close" }, new[] { "In", "Out" }, new[] { "Left", "Right" }, new[] { "Lock", "Unlock" },
                new[] { "Extend", "Retract" }, new[] { "Push", "Pull" }, new[] { "On", "Off" }, new[] { "Front", "Back" }
            };

            private static string Opposite(string word)
            {
                foreach (var pair in Opposites)
                {
                    if (string.Equals(pair[0], word, StringComparison.OrdinalIgnoreCase)) return pair[1];
                    if (string.Equals(pair[1], word, StringComparison.OrdinalIgnoreCase)) return pair[0];
                }
                return "";
            }

            /// <summary>A valve output's name on the drawing without the valve: "Front Clamping Clamp Valve (with ...)" -> "Front Clamping Clamp".</summary>
            private static string[] OutputWords(WiringTable.Point p)
            {
                if (p == null || p.Text.Length == 0) return new string[0];
                var tx = Regex.Replace(p.Text, "\\(.*?\\)", "").Trim();
                if (tx.EndsWith(" Valve", StringComparison.OrdinalIgnoreCase)) tx = tx.Substring(0, tx.Length - 6).Trim();
                return tx.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }

            /// <summary>A word of the drawings in the page's language (the template's MoveWords), as HTML.</summary>
            private string MoveWord(string english)
            {
                string w;
                return _moveWords.TryGetValue(english.Trim(), out w) ? w : E(english);
            }

            private string MovementWord(bool mine)
            {
                // the button's own words (not a glyph) - as the HMI shows them
                var own = mine ? mv.Name : mv.OtherName;
                if (!string.IsNullOrWhiteSpace(own) && !IsGlyph(own)) return E(own.Trim());
                // the two outputs' names on the drawing differ in the movement's word
                var a = OutputWords(c.Out);
                var b = OutputWords(c.OtherOut);
                if (a.Length > 0 && b.Length > 0)
                {
                    int k = 0;
                    while (k < a.Length && k < b.Length && string.Equals(a[k], b[k], StringComparison.OrdinalIgnoreCase)) k++;
                    var rest = string.Join(" ", (mine ? a : b).Skip(k));
                    if (rest.Length > 0 && k > 0) return MoveWord(rest);
                }
                // one output (a spring valve): its last word, the spring's is the opposite
                var coilWords = a.Length > 0 ? a : b;
                if (coilWords.Length > 0)
                {
                    var word = coilWords[coilWords.Length - 1];
                    bool coilIsMine = a.Length > 0;
                    if (mine == coilIsMine) return MoveWord(word);
                    var opposite = Opposite(word);
                    if (opposite.Length > 0) return MoveWord(opposite);
                }
                // the button's sign
                var glyph = (mine ? mv.Name : mv.OtherName ?? "").Trim();
                string g;
                bool left = (mv.Side == "A") == mine;
                if (t == null)
                {
                    // no templates installed: the menus still name the movement
                    var english = new Dictionary<string, string> { { "\u25B2", "Up" }, { "\u25BC", "Down" }, { "\u25C0", "Left" }, { "\u25B6", "Right" } };
                    return english.TryGetValue(glyph, out g) ? g : left ? "the left button&#39;s movement" : "the right button&#39;s movement";
                }
                if (glyph.Length > 0 && t.Table("GlyphWords").TryGetValue(glyph, out g)) return g;
                return Phrase(left ? "LeftMove" : "RightMove");
            }

            /// <summary>The cylinder's name on the drawing, its remarks in brackets left out.</summary>
            private string CylinderShort
            {
                get { return Regex.Replace(CylinderName(c.Air), "\\s*\\(.*?\\)", "").Trim(); }
            }

            /// <summary>A 5/3 valve's centre as the drawing names it: true closed, false open, null not said.</summary>
            private bool? ClosedCentre
            {
                get
                {
                    var v = (c.Air != null ? c.Air.Valve : "").ToLowerInvariant();
                    if (v.Contains("closed centre") || v.Contains("closed center")) return true;
                    if (v.Contains("open centre") || v.Contains("open center")) return false;
                    return null;
                }
            }

            /// <summary>"U11 FX5-16EYT-ESS, terminal Y4" -> the card and its own terminal name (the LED is labelled with it).</summary>
            private static void CardParts(WiringTable.Point p, out string card, out string terminal)
            {
                card = terminal = "";
                if (p == null || p.Card.Length == 0) return;
                int i = p.Card.IndexOf(", terminal ", StringComparison.OrdinalIgnoreCase);
                if (i < 0) { card = p.Card; return; }
                card = p.Card.Substring(0, i).Trim();
                terminal = p.Card.Substring(i + 11).Trim();
            }

            /// <summary>This movement's coil output (Y32), or the other side's for a spring return.</summary>
            private WiringTable.Point OutPoint { get { return _spring ? c.OtherOut : c.Out; } }
            private MovementInfo.Device OutDev { get { return _spring ? c.OtherDevice : c.OutDevice; } }

            // ---- small pieces, from the template's phrases --------------------------------

            private string SensorName(Sensor s)
            {
                var p = s.Point;
                bool tag = p != null && p.Tag.Length > 0;
                return Phrase("SensorName",
                    "Switch", s.IsPressureSwitch,
                    "SensorWord", tag && !p.Tag.StartsWith("SW", StringComparison.OrdinalIgnoreCase),
                    "Tag", tag ? E(p.Tag) : "",
                    "Io", tag ? Dev(p.Address) : "",
                    "Dev", Dev(s.Device.Address));
            }

            /// <summary>Where a sensor is: its wire, sheet and card terminal; the module it is read through.</summary>
            private string SensorWhere(Sensor s)
            {
                var p = s.Point;
                if (p == null) return "";
                var parts = new List<string>();
                if (p.Wire.Length > 0) parts.Add(Phrase("Wire", "V", E(p.Wire)));
                if (p.Sheet.Length > 0) parts.Add(Phrase("Sheet", "V", E(p.Sheet)));
                if (p.Card.Length > 0) parts.Add(E(Words(p.Card)));
                if (p.Via.Length > 0) parts.Add(Phrase("ReadThrough", "V", E(Data(p, "Via", p.Via))));
                return parts.Count == 0 ? "" : " &ndash; " + string.Join(", ", parts);
            }

            private string Sensors(IEnumerable<Sensor> list, string glue = null)
            {
                return string.Join(glue ?? Phrase("And"), list.Select(SensorName));
            }

            /// <summary>The electrical line: "X34 - sensor SN46, wire 119, sheet 12.6" or "Y12 - wire 210 - relay K13 (sheet 18.6) - coil Y.7, wire 308 (sheet 25.5)".</summary>
            private string Electrical(WiringTable.Point p)
            {
                var parts = new List<string>();
                if (p.IsInput)
                {
                    var q = new List<string>();
                    if (p.Tag.Length > 0) q.Add(p.Tag.StartsWith("S", StringComparison.OrdinalIgnoreCase) && !p.Tag.StartsWith("SB", StringComparison.OrdinalIgnoreCase)
                                                ? Phrase("SensorTag", "V", E(p.Tag)) : E(p.Tag));
                    if (p.Wire.Length > 0) q.Add(Phrase("Wire", "V", E(p.Wire)));
                    if (p.Sheet.Length > 0) q.Add(Phrase("Sheet", "V", E(p.Sheet)));
                    if (p.Via.Length > 0) q.Add(Phrase("ReadThrough", "V", E(Data(p, "Via", p.Via))));
                    parts.Add(E(p.Address) + (q.Count > 0 ? " - " + string.Join(", ", q) : ""));
                }
                else
                {
                    parts.Add(E(p.Address) + (p.Wire.Length > 0 ? " - " + Phrase("Wire", "V", E(p.Wire)) : "") +
                              (p.Relay.Length == 0 && p.Sheet.Length > 0 ? " " + Phrase("SheetParen", "V", E(p.Sheet)) : ""));
                    if (p.Relay.Length > 0) parts.Add(Phrase("Relay", "V", E(p.Relay)) + (p.RelaySheet.Length > 0 ? " " + Phrase("SheetParen", "V", E(p.RelaySheet)) : ""));
                    if (p.Coil.Length > 0)
                        parts.Add((p.Coil.StartsWith("Y", StringComparison.OrdinalIgnoreCase) ? Phrase("CoilWord", "V", E(p.Coil)) : E(p.Coil)) +
                                  (p.CoilWire.Length > 0 ? ", " + Phrase("Wire", "V", E(p.CoilWire)) : "") +
                                  (p.CoilSheet.Length > 0 ? " " + Phrase("SheetParen", "V", E(p.CoilSheet)) : ""));
                    else if (p.CoilSheet.Length > 0 && p.Relay.Length == 0)
                        parts.Add(Phrase("ContinuesOn", "V", E(p.CoilSheet)));
                }
                return string.Join(" - ", parts);
            }

            /// <summary>The air line: "valve Y20/Y21 (...), hoses 16 (A) / 96 (B), P12 ... - pneumatic sheet 2 E3". Empty for none.</summary>
            private string Pneumatic(WiringTable.Point p)
            {
                if (p.Valve.Length == 0 && p.Cylinder.Length == 0) return "";
                var parts = new List<string>();
                if (p.Valve.Length > 0) parts.Add(Phrase("ValveWord", "V", E(Data(p, "Valve", p.Valve))));
                if (p.Hoses.Length > 0) parts.Add(Phrase(p.Hoses.Contains("/") ? "HosesWord" : "HoseWord", "V", E(p.Hoses)));
                if (p.Cylinder.Length > 0) parts.Add(E(p.Cylinder));
                return string.Join(", ", parts) + (p.PnSheet.Length > 0 ? " - " + Phrase("PnSheetWord", "V", E(p.PnSheet)) : "");
            }

            private string State(int? v, bool wantOn)
            {
                if (!v.HasValue) return "";
                bool on = v.Value != 0;
                string cls = on == wantOn ? (on ? "st-on" : "st-off") : "st-bad";
                return "<span class=\"" + cls + "\">" + Phrase(on ? "On" : "Off") + "</span>";
            }

            private string ConditionText(MovementInfo.Condition k)
            {
                var sb = new StringBuilder("<b>" + E(k.Name) + "</b>");
                // the rule names the PLC's devices: for an Admin only
                if (_admin && !string.IsNullOrEmpty(k.Rule)) sb.Append(" (" + E(k.Rule) + ")");
                if (k.Met == true) sb.Append(" <span class=\"st-on\">" + Phrase("Met") + "</span>");
                else if (k.Met == false) sb.Append(" <span class=\"st-bad\">" + Phrase("NotMet") + "</span>");
                return sb.ToString();
            }

            private static string Code(string address) { return "<code>" + E((address ?? "").Trim()) + "</code>"; }

            /// <summary>A device as the page names it: a real input or output for everyone, an internal one (M, D...) for an Admin only; "" when not named.</summary>
            private string Dev(string address)
            {
                if (string.IsNullOrWhiteSpace(address)) return "";
                return _admin || IsPhysicalIo(address) ? Code(address) : "";
            }

            /// <summary>
            /// An LED of a PLC card, as the drawings show it: the card's tag (U4) and
            /// model, the LED's label (21) and the labels beside it in its row - an
            /// FX5U CPU's LEDs run 0-7, 20-27 on the upper row and 10-17, 30-37 on
            /// the lower one; a module's 0-7 and 10-17.
            /// </summary>
            private sealed class Led
            {
                public string Tag = "", Model = "", Label = "", L2 = "", L1 = "", R1 = "", R2 = "";
                public bool Cpu, Top = true, Known;
            }

            private static Led LedOf(WiringTable.Point p, string address)
            {
                var led = new Led();
                string card, terminal;
                CardParts(p, out card, out terminal);
                var cm = Regex.Match(card, @"^(\S+)\s+(.+)$");
                if (cm.Success) { led.Tag = cm.Groups[1].Value; led.Model = cm.Groups[2].Value.Trim(); }
                else led.Tag = card;
                led.Cpu = led.Model.IndexOf("FX5U", StringComparison.OrdinalIgnoreCase) >= 0;
                var dm = Regex.Match((terminal.Length > 0 ? terminal : address) ?? "", @"([0-9]+)$");
                if (!dm.Success || card.Length == 0) return led;
                int n = int.Parse(dm.Groups[1].Value, CultureInfo.InvariantCulture), tens = n / 10, digit = n % 10;
                if (digit > 7) return led;
                var row = new List<int>();
                if (led.Cpu)
                {
                    for (int g = tens % 2; g <= Math.Max(3, tens); g += 2)
                        for (int i = 0; i < 8; i++) row.Add(g * 10 + i);
                    led.Top = tens % 2 == 0;
                }
                else
                {
                    for (int i = 0; i < 8; i++) row.Add(tens * 10 + i);
                    led.Top = tens == 0;
                }
                int at = row.IndexOf(n);
                Func<int, string> label = k => at + k >= 0 && at + k < row.Count ? row[at + k].ToString(CultureInfo.InvariantCulture) : "";
                led.Label = n.ToString(CultureInfo.InvariantCulture);
                led.L2 = label(-2); led.L1 = label(-1); led.R1 = label(1); led.R2 = label(2);
                led.Known = true;
                return led;
            }

            private static void PutLed(IDictionary<string, object> m, string prefix, Led led)
            {
                m[prefix + "CardTag"] = E(led.Tag);
                m[prefix + "CardModel"] = E(led.Model);
                m[prefix + "Cpu"] = led.Cpu;
                m[prefix + "HasLed"] = led.Known;
                m[prefix + "Led"] = led.Label;
                m[prefix + "LedL2"] = led.L2;
                m[prefix + "LedL1"] = led.L1;
                m[prefix + "LedR1"] = led.R1;
                m[prefix + "LedR2"] = led.R2;
                m[prefix + "LedTop"] = led.Top;
            }

            /// <summary>"K20" -> the relays beside it on the rail (K18, K19 | K21, K22), as the drawing shows a row of them.</summary>
            private static void PutRelays(IDictionary<string, object> m, string relay)
            {
                var rm = Regex.Match(relay ?? "", @"^([A-Za-z]+)([0-9]+)$");
                int n = rm.Success ? int.Parse(rm.Groups[2].Value, CultureInfo.InvariantCulture) : -1;
                Func<int, string> k = d => n >= 0 && n + d > 0 ? E(rm.Groups[1].Value + (n + d).ToString(CultureInfo.InvariantCulture)) : "";
                m["RelayL2"] = k(-2); m["RelayL1"] = k(-1); m["RelayR1"] = k(1); m["RelayR2"] = k(2);
            }

            // ---- what the template is filled with -----------------------------------------

            private Dictionary<string, object> SensorItem(Sensor s)
            {
                var p = s.Point;
                var item = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "Name", SensorName(s) },
                    { "Where", SensorWhere(s) },
                    { "Text", p != null ? E(p.Text) : "" },
                    { "Io", Dev(s.Io) },
                    { "Dev", Dev(s.Device.Address) },
                    // the PLC copies it into an internal device: worth saying to an Admin only
                    { "CopiedTo", _admin && p != null && !string.Equals(p.Address, s.Device.Address, StringComparison.OrdinalIgnoreCase) },
                    { "IsSwitch", s.IsPressureSwitch },
                    { "Wire", p != null ? E(p.Wire) : "" },
                    { "Card", p != null ? E(Words(p.Card)) : "" },
                    // for the drawings: the sensor's own tag and the input's LED on its card
                    { "TagPlain", p != null ? E(p.Tag) : "" },
                    { "IoPlain", IsPhysicalIo(s.Io) || _admin ? E(s.Io) : "" },
                    { "Key", "" }, { "Next", "" }, { "Last", false }
                };
                PutLed(item, "In", LedOf(p, s.Io));
                return item;
            }

            private Dictionary<string, object> Model()
            {
                var m = new Dictionary<string, object>(StringComparer.Ordinal);
                var o = OutPoint;
                var od = OutDev;
                var air = c.Air;
                Func<string, string> e = E;

                // the template's own valve and feedback (a pair's file has them written in)
                int v = (int)c.Valve, f = (int)c.Feedback;
                for (int i = 0; i <= 3; i++) m["V" + i] = v == i;
                for (int i = 0; i <= 7; i++) m["F" + i] = f == i;
                m["Double"] = _double;
                m["FiveThree"] = _fiveThree;
                m["SpringValve"] = c.Valve == ValveType.SingleSolenoidSpring;
                m["PositionFb"] = _position;
                m["MagneticFb"] = c.Feedback == FeedbackType.SingleMagnetic || c.Feedback == FeedbackType.TwoMagnetic;
                m["InductiveFb"] = c.Feedback == FeedbackType.SingleInductive;
                m["SwitchFb"] = c.Feedback == FeedbackType.PressureSwitch;

                // the movement
                m["Unit"] = Unit;
                m["Move"] = Move;
                m["OtherMove"] = OtherMove;
                var glyph = IsGlyph(mv.Name) ? E(mv.Name.Trim()) : "";
                var otherGlyph = IsGlyph(mv.OtherName) ? E(mv.OtherName.Trim()) : "";
                m["Glyph"] = glyph;
                m["ButtonSign"] = glyph.Length > 0 ? glyph : Move;
                m["OtherGlyph"] = otherGlyph;
                m["OtherSign"] = otherGlyph.Length > 0 ? otherGlyph : OtherMove;
                m["HasButton"] = mv.Command != null && !string.IsNullOrWhiteSpace(mv.Command.Address);
                // The HMI's command bit is not part of the real operation's signal path -
                // the page never names it (2 October); kept empty for old templates.
                m["Command"] = "";
                m["OtherCommand"] = "";
                m["Spring"] = _spring;
                m["Conditions"] = string.Join("; ", mv.Conditions.Select(ConditionText));

                // the output, the card, the relay, the coil
                m["Out"] = od != null ? Dev(od.Address) : "";
                m["OutText"] = o != null ? e(o.Text) : "";
                m["OutWire"] = o != null ? e(o.Wire) : "";
                m["OutSheet"] = o != null ? e(o.Sheet) : "";
                string card, terminal;
                CardParts(o, out card, out terminal);
                m["OutCard"] = e(Words(card));
                m["OutTerminal"] = e(terminal);
                m["OutTerminalOther"] = terminal.Length > 0 && !string.Equals(terminal, od != null ? od.Address : "", StringComparison.OrdinalIgnoreCase);
                m["OtherOut"] = c.OtherDevice != null ? Dev(c.OtherDevice.Address) : "";
                PutLed(m, "Out", LedOf(o, od != null ? od.Address : ""));
                m["OutPlain"] = od != null && (_admin || IsPhysicalIo(od.Address)) ? e(od.Address) : "";
                m["Relay"] = o != null ? e(o.Relay) : "";
                PutRelays(m, o != null ? o.Relay : "");
                m["RelaySheet"] = o != null ? e(o.RelaySheet) : "";
                m["Coil"] = o != null ? e(o.Coil) : "";
                m["CoilWire"] = o != null ? e(o.CoilWire) : "";
                m["CoilSheet"] = o != null ? e(o.CoilSheet) : "";

                // the valve, the hoses, the cylinder
                m["ValveTag"] = air != null ? e(Data(air, "Valve", air.Valve)) : "";
                m["PnSheet"] = air != null ? e(air.PnSheet) : "";
                m["CylName"] = e(CylinderShort);
                var hoses = air != null ? air.Hoses : "";
                m["Hoses"] = e(hoses);
                m["HosesMany"] = hoses.Contains("/");
                // the hoses one by one, for the drawings: "16 (A) / 96 (B)" -> 16 / A, 96 / B
                var hose = hoses.Split('/').Select(h => h.Trim()).ToList();
                for (int i = 0; i < 2; i++)
                {
                    var h = i < hose.Count ? hose[i] : "";
                    var hm = Regex.Match(h, @"^(.*?)\s*\((.*?)\)\s*$");
                    m["Hose" + (i == 0 ? "A" : "B")] = e(h);
                    m["Hose" + (i == 0 ? "A" : "B") + "No"] = e(hm.Success ? hm.Groups[1].Value : h);
                    m["Hose" + (i == 0 ? "A" : "B") + "Port"] = e(hm.Success ? hm.Groups[2].Value : "");
                }
                m["ClosedCentre"] = ClosedCentre == true;
                m["OpenCentre"] = ClosedCentre == false;
                m["Pressure"] = air != null ? e(Data(air, "Pressure", air.Pressure)) : "";
                m["Measure"] = air != null ? e(Data(air, "Measure", air.Measure)) : "";
                m["Position"] = Dev(mv.Position);
                m["ZeroWire"] = e(_supply.ZeroVoltWire);
                m["SensorWire"] = e(_supply.SensorSupplyWire);
                m["MainAir"] = c.MainAir != null;
                m["MainAirTag"] = c.MainAir != null ? e(c.MainAir.Tag) : "";
                m["MainAirCode"] = c.MainAir != null ? Dev(c.MainAir.Address) : "";
                m["MainAirText"] = c.MainAir != null ? e(c.MainAir.Text) : "";
                m["MainAirLine"] = c.MainAir != null ? Electrical(c.MainAir) + (c.MainAir.Text.Length > 0 ? Phrase("DrawnAs", "V", e(c.MainAir.Text)) : "") : "";

                // the alarm
                // (the alarm's bit is an internal device: named for an Admin only)
                bool error = !string.IsNullOrWhiteSpace(mv.Error);
                m["HasError"] = error;
                m["Admin"] = _admin;
                m["Error"] = error && _admin ? Code(mv.Error) : "";
                m["ErrorShown"] = error && _admin ? Code(mv.Error + (_failure > 0 ? "." + _failure : "")) : "";
                m["ErrorText"] = e(c.ErrorText);
                // the page's title: the alarm as the list shows it, else its bit's alarm text
                m["Title"] = !string.IsNullOrWhiteSpace(AlarmText) ? e(AlarmText.Trim()) : e(c.ErrorText);
                var when = Occurred ?? mv.AlarmTime;
                m["AlarmTime"] = when.HasValue ? when.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "";
                m["AlarmDate"] = when.HasValue ? when.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "";
                DateTime now;
                try { now = Clock(); } catch { now = DateTime.Now; }
                m["Created"] = now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                m["SeenRising"] = mv.SeenRising;
                m["HmiAlarm"] = _failure > 0;
                for (int i = 1; i <= 6; i++) m["Failure" + i] = _failure == i;
                m["LookF"] = _failure == 1 || _failure == 2 || _failure == 5 || _failure == 6;

                // the sensors
                var start = _mustOff.Where(x => !x.IsPressureSwitch).ToList();
                m["HasMustOn"] = _mustOn.Count > 0;
                m["HasMustOff"] = _mustOff.Count > 0;
                m["HasStart"] = start.Count > 0;
                m["HasSensors"] = _mustOn.Count > 0 || _mustOff.Count > 0;
                m["StartAndOn"] = start.Count > 0 && _mustOn.Count > 0;
                m["MustOnMany"] = _mustOn.Count > 1;
                m["EndNotSensed"] = _endNotSensed;
                m["OnNames"] = Sensors(_mustOn);
                m["OnSlash"] = Sensors(_mustOn, " / ");
                m["OffNames"] = Sensors(_mustOff);
                m["OffSlash"] = Sensors(_mustOff, " / ");
                m["StartNames"] = Sensors(start);
                m["StartSlash"] = Sensors(start, " / ");
                var sw = _mustOff.FirstOrDefault(x => x.IsPressureSwitch);
                m["OffSwitch"] = sw != null ? SensorName(sw) : "";

                // the C steps' keys: one per sensor that must come on, one for those that must go off, the time
                bool none = _mustOn.Count == 0 && _mustOff.Count == 0;
                string time = c.Feedback == FeedbackType.AnalogueMagnetic ? "C2" : c.Feedback == FeedbackType.EncoderReference ? "C4"
                            : "C" + (1 + _mustOn.Count + (_mustOff.Count > 0 ? 1 : 0) + (none ? 1 : 0));
                m["TimeKey"] = time;
                m["OffKey"] = "C" + (_mustOn.Count + 1);
                var onItems = new List<IDictionary<string, object>>();
                for (int i = 0; i < _mustOn.Count; i++)
                {
                    var item = SensorItem(_mustOn[i]);
                    item["Key"] = "C" + (i + 1);
                    item["Next"] = i + 1 < _mustOn.Count ? "C" + (i + 2) : _mustOff.Count > 0 ? "C" + (i + 2) : time;
                    item["Last"] = i == _mustOn.Count - 1;
                    onItems.Add(item);
                }
                m["MustOn"] = onItems;
                m["MustOff"] = _mustOff.Select(x => (IDictionary<string, object>)SensorItem(x)).ToList();
                var reference = _mustOn.Concat(_mustOff).FirstOrDefault();
                m["HasRef"] = reference != null;
                m["RefName"] = reference != null ? SensorName(reference) : "";
                m["RefWhere"] = reference != null ? SensorWhere(reference) : "";
                m["RefIo"] = reference != null ? Dev(reference.Io) : "";
                // for the cylinder's drawing: the sensor at the end it moves to, the one at its start
                Func<Sensor, string> tagOf = x => x == null ? "" : x.Point != null && x.Point.Tag.Length > 0 ? e(x.Point.Tag) : (_admin || IsPhysicalIo(x.Io) ? e(x.Io) : "");
                m["OnTag"] = tagOf(_mustOn.FirstOrDefault(x => !x.IsPressureSwitch));
                m["StartTag"] = tagOf(start.FirstOrDefault());
                m["SwitchTag"] = tagOf(_mustOn.Concat(_mustOff).FirstOrDefault(x => x.IsPressureSwitch));

                // what the HMI saw
                SeenModel(m);

                // the parts list
                string outLine = o != null ? Electrical(o) + (o.Card.Length > 0 ? " &ndash; " + e(Words(o.Card)) : "") + (o.Text.Length > 0 ? Phrase("DrawnAs", "V", e(o.Text)) : "")
                               : od != null ? Dev(od.Address) : "";
                var oo = c.OtherOut;
                string otherLine = oo != null ? Electrical(oo) + (oo.Card.Length > 0 ? " &ndash; " + e(Words(oo.Card)) : "") + (oo.Text.Length > 0 ? Phrase("DrawnAs", "V", e(oo.Text)) : "")
                                 : c.OtherDevice != null ? Dev(c.OtherDevice.Address) : "";
                string pneu = air != null ? Pneumatic(air) : "";
                m["OutLine"] = outLine;
                m["OtherLine"] = otherLine;
                m["PneuLine"] = pneu;
                m["PartSensors"] = _mustOn.Concat(_mustOff).Select(x => (IDictionary<string, object>)SensorItem(x)).ToList();
                m["HasParts"] = (bool)m["HasButton"] || outLine.Length > 0 || (!_spring && _double && otherLine.Length > 0) || pneu.Length > 0 ||
                                _mustOn.Count + _mustOff.Count > 0 || ((string)m["Measure"]).Length > 0 || ((string)m["Position"]).Length > 0 ||
                                ((string)m["Pressure"]).Length > 0 || c.MainAir != null || error;
                m["Drawn"] = o != null || _mustOn.Concat(_mustOff).Any(x => x.Point != null);

                // where to start (filled in after the steps are known)
                m["Hint"] = false;
                m["StartKey"] = "";
                m["StepOneNote"] = false;
                foreach (var w in WhyFlags) m[w] = false;
                m["StuckNames"] = m["StillNames"] = m["WhyStartNames"] = "";
                return m;
            }

            private static readonly string[] WhyFlags =
            {
                "WhyF1", "WhyF2", "WhyF3", "WhyF4", "WhyF5", "WhyF6", "WhySpringOn", "WhyOutOff", "WhyBothCoils", "WhyArrived",
                "WhyArrivedStuck", "WhyStartOutOk", "WhyStillAtStart", "WhyLeftStart", "WhyLeftUnseen"
            };

            /// <summary>The devices with what they should be, at the alarm and now.</summary>
            private void SeenModel(Dictionary<string, object> m)
            {
                var rows = new List<IDictionary<string, object>>();
                Action<string, MovementInfo.Device, bool, string, string> row = (kind, d, wantOn, code, name) =>
                {
                    if (d == null || string.IsNullOrWhiteSpace(d.Address)) return;
                    rows.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "IsCommand", kind == "command" }, { "IsSpringCoil", kind == "springcoil" }, { "IsValveOut", kind == "valve" },
                        { "IsOtherCoil", kind == "other" }, { "IsSensor", kind == "sensor" },
                        { "Code", code }, { "Name", name }, { "WantOn", wantOn },
                        { "AtAlarm", State(d.AtAlarm, wantOn) }, { "Now", State(d.Now, wantOn) }
                    });
                };
                // With the failure known, only the signals it is about (the output that
                // started the time, the sensors that decided it).
                bool all = _failure <= 0;
                bool outputs = all || _failure != 3;
                bool targets = all || _failure == 2 || _failure == 3 || _failure >= 5;
                bool starts = all || _failure != 4;
                // (the HMI's command is not listed: it is not part of the real operation)
                if (_spring) { if (outputs) row("springcoil", OutDev, false, "", ""); }
                else
                {
                    if (outputs) row("valve", OutDev, true, "", "");
                    if ((all || _failure == 4) && _double && c.OtherDevice != null) row("other", c.OtherDevice, false, Dev(c.OtherDevice.Address), "");
                }
                if (targets) foreach (var s in _mustOn) row("sensor", s.Device, true, "", SensorName(s));
                if (starts) foreach (var s in _mustOff) row("sensor", s.Device, false, "", SensorName(s));
                bool known = rows.Count > 0 && (_mustOn.Concat(_mustOff).Any(s => s.Device.AtAlarm.HasValue || s.Device.Now.HasValue) ||
                                                (OutDev != null && (OutDev.AtAlarm.HasValue || OutDev.Now.HasValue)));
                m["SeenRows"] = rows;
                m["Seen"] = known;
            }

            // ---- the steps ----------------------------------------------------------------

            /// <summary>The steps in the page, in order (each with its part and role).</summary>
            private static List<Block> Blocks(string html)
            {
                var list = new List<Block>();
                foreach (Match mt in Regex.Matches(html, "<div class=\"step[^\"]*\" id=\"([^\"]+)\"([^>]*)>"))
                {
                    var attrs = mt.Groups[2].Value;
                    var b = new Block
                    {
                        Key = mt.Groups[1].Value,
                        Part = Regex.Match(attrs, "data-part=\"([^\"]*)\"").Groups[1].Value,
                        Role = Regex.Match(attrs, "data-role=\"([^\"]*)\"").Groups[1].Value,
                        Start = mt.Index
                    };
                    // its end: the </div> that closes it
                    int depth = 0, i = mt.Index;
                    while (i < html.Length)
                    {
                        int open = html.IndexOf("<div", i, StringComparison.Ordinal);
                        int close = html.IndexOf("</div>", i, StringComparison.Ordinal);
                        if (close < 0) { i = html.Length; break; }
                        if (open >= 0 && open < close) { depth++; i = open + 4; }
                        else { depth--; i = close + 6; if (depth == 0) break; }
                    }
                    b.End = i;
                    list.Add(b);
                }
                return list;
            }

            /// <summary>Where a step stands in the page's order: 1, A1.., B1.., C1.., then "Still not solved?".</summary>
            private static int Order(string key)
            {
                if (key == "look") return 0;
                if (key == "end") return 1000;
                int n;
                if (key.Length > 1 && int.TryParse(key.Substring(1), out n))
                {
                    if (key[0] == 'A') return 100 + n;
                    if (key[0] == 'B') return 200 + n;
                    if (key[0] == 'C') return 300 + n;
                }
                return 500;
            }

            // ---- only what failed (an alarm the HMI raised itself) ------------------------
            //
            // The HMI knows WHICH signal failed (AlarmGeneration's sub-code), so the
            // page asks only about that signal: a start sensor that stayed ON is
            // "did it leave? - then the valve, the air, the path, or that sensor";
            // a target sensor that did not come ON is "did it stop on the way, or
            // arrive? - then the air and the path, or that sensor"; two coils ON is
            // the PLC's two outputs. The other steps are left out and the answers
            // that led to them go on to the next step that is kept.

            private List<Block> Focus(List<Block> order)
            {
                if (_failure <= 0) return order;
                Func<string, IEnumerable<Block>> role = r => order.Where(x => x.Role == r);
                var fromA3 = order.Where(x => x.Part == "A" && x.Key != "A1" && x.Key != "A2").ToList();
                var bPart = order.Where(x => x.Part == "B").ToList();
                var startSteps = role("start").Concat(role("reference")).ToList();
                var look = order.FirstOrDefault(x => x.Key == "look");
                var keep = new List<Block>();
                switch (_failure)
                {
                    case 1: keep.Add(look); keep.AddRange(fromA3); keep.AddRange(startSteps); break;
                    case 2:
                        keep.Add(look);
                        if (!_mustOff.Any(x => !x.IsPressureSwitch)) keep.AddRange(fromA3);
                        keep.AddRange(bPart); keep.AddRange(role("target")); keep.AddRange(role("time"));
                        break;
                    case 3: keep.AddRange(startSteps); break;
                    case 4: keep.AddRange(order.Where(x => x.Key == "A1" || x.Key == "A2")); break;
                    case 5: keep.Add(look); keep.AddRange(fromA3); keep.AddRange(role("position")); break;
                    case 6: keep.Add(look); keep.AddRange(bPart); keep.AddRange(role("position")); keep.AddRange(role("window")); keep.AddRange(role("time")); break;
                    default: return order;
                }
                keep = keep.Where(x => x != null).Distinct().ToList();
                if (keep.Count == 0) return order;
                var end = order.FirstOrDefault(x => x.Key == "end");
                if (end != null) keep.Add(end);
                return order.Where(keep.Contains).ToList();
            }

            /// <summary>A step named by what it is ("@A3": the card on; "@B"; "@target"; "@start"; "@position") - the first such step; "end" when none.</summary>
            private static string Symbolic(string key, List<Block> order)
            {
                Block b = null;
                switch (key)
                {
                    case "@A3": b = order.FirstOrDefault(x => x.Part == "A" && x.Key != "A1" && x.Key != "A2"); break;
                    case "@B": b = order.FirstOrDefault(x => x.Part == "B"); break;
                    case "@target": b = order.FirstOrDefault(x => x.Role == "target"); break;
                    case "@start": b = order.FirstOrDefault(x => x.Role == "start") ?? order.FirstOrDefault(x => x.Role == "reference"); break;
                    case "@position": b = order.FirstOrDefault(x => x.Role == "position"); break;
                    case "@time": b = order.FirstOrDefault(x => x.Role == "time"); break;
                }
                return b != null ? b.Key : "end";
            }

            // ---- where to start -----------------------------------------------------

            /// <summary>From the states just before the alarm: the step to start at and why (a Why* flag; "" when not known).</summary>
            private string StartAt(List<Block> steps, out string why, Dictionary<string, object> m)
            {
                why = "";
                var od = OutDev;
                int? outAtAlarm = od != null ? od.AtAlarm : null;
                var start = _mustOff.Where(s => !s.IsPressureSwitch && s.Device.AtAlarm.HasValue).ToList();
                bool stillAtStart = start.Count > 0 && start.Any(s => s.Device.AtAlarm == 1);
                bool leftStart = start.Count > 0 && start.All(s => s.Device.AtAlarm == 0);
                bool arrived = _mustOn.Count > 0 && _mustOn.All(s => s.Device.AtAlarm == 1);
                var stuck = _mustOff.Where(s => s.Device.AtAlarm == 1).ToList();
                Func<string, string> key = k => steps.Any(x => x.Key == k) ? k : "look";
                m["StuckNames"] = Sensors(stuck);
                m["StillNames"] = Sensors(start.Where(s => s.Device.AtAlarm == 1));
                m["WhyStartNames"] = Sensors(start);

                if (_spring)
                {
                    if (outAtAlarm == 1) { why = "WhySpringOn"; return "A1"; }
                }
                else
                {
                    if (outAtAlarm == 0) { why = "WhyOutOff"; return "A1"; }
                    if (_double && outAtAlarm == 1 && c.OtherDevice != null && c.OtherDevice.AtAlarm == 1) { why = "WhyBothCoils"; return "A1"; }
                }
                if (arrived && stuck.Count == 0) { why = "WhyArrived"; return key(Symbolic("@time", steps)); }
                if (arrived && stuck.Count > 0) { why = "WhyArrivedStuck"; return key(stuck.Any(s => s.IsPressureSwitch) || _mustOff.Count > 0 ? "C" + (_mustOn.Count + 1) : "C1"); }
                if (stillAtStart && (outAtAlarm == 1 || (_spring && outAtAlarm == 0))) { why = "WhyStartOutOk"; return "A3"; }
                if (stillAtStart) { why = "WhyStillAtStart"; return "A1"; }
                if (leftStart && _mustOn.Count > 0) { why = "WhyLeftStart"; return "look"; }
                if (leftStart && _endNotSensed) { why = "WhyLeftUnseen"; return "look"; }
                return "";
            }

            // ---- the page -----------------------------------------------------------------

            public string Page()
            {
                if (t == null) return "";
                var m = Model();
                // first: which steps the template has for this movement
                var all = Blocks(t.Render(m));
                var kept = Focus(all);
                var keptKeys = new HashSet<string>(kept.Select(x => x.Key));

                // where to start
                string why, start;
                if (_failure > 0 && kept.Count > 0)
                {
                    start = kept[0].Key;
                    why = _failure >= 1 && _failure <= 6 ? "WhyF" + _failure : "";
                }
                else start = StartAt(kept, out why, m);
                var startBlock = kept.FirstOrDefault(x => x.Key == start);
                if (startBlock != null && why.Length > 0)
                {
                    m["Hint"] = true;
                    m[why] = true;
                    m["StartKey"] = start;
                    // "step 1 is still worth a look" - when step 1 is on the page and is not the start
                    m["StepOneNote"] = start != "look" && keptKeys.Contains("look");
                }
                var html = t.Render(m);
                return Finish(html, all, kept, keptKeys, start, startBlock != null);
            }

            /// <summary>The steps left out, numbered, linked; the start marked; the map and the boxes filled.</summary>
            private string Finish(string html, List<Block> order, List<Block> kept, HashSet<string> keptKeys, string start, bool markStart)
            {
                // the steps left out (and the parts' headings with nothing under them)
                var blocks = Blocks(html);
                var sb = new StringBuilder();
                int at = 0;
                foreach (var b in blocks)
                {
                    if (keptKeys.Contains(b.Key)) continue;
                    sb.Append(html, at, b.Start - at);
                    at = b.End;
                    while (at < html.Length && (html[at] == '\n' || html[at] == '\r')) at++;
                }
                sb.Append(html, at, html.Length - at);
                html = sb.ToString();
                html = Regex.Replace(html, "<h2 class=\"part (a|b|c)\">.*?</h2>\n?", mt =>
                    kept.Any(x => x.Part == mt.Groups[1].Value.ToUpperInvariant()) ? mt.Value : "", RegexOptions.Singleline);

                // numbers
                var number = new Dictionary<string, int>();
                for (int i = 0; i < kept.Count; i++) number[kept[i].Key] = i + 1;
                Func<string, string> link = k => "<a href=\"#" + k + "\">" + t.Phrase("StepLink", new Dictionary<string, object> { { "N", number[k].ToString(CultureInfo.InvariantCulture) } }) + "</a>";
                // a step left out: the next one kept
                Func<string, string> resolve = k =>
                {
                    if (keptKeys.Contains(k)) return k;
                    int o = Order(k);
                    var next = kept.Where(x => Order(x.Key) >= o).OrderBy(x => Order(x.Key)).FirstOrDefault();
                    return next != null ? next.Key : "end";
                };

                // answers that name a step by what it is: none such - the answer goes
                html = Regex.Replace(html, "<tr class=\"([a-z]+)\" data-optional>(.*?)</tr>\n?", mt =>
                {
                    var target = Regex.Match(mt.Groups[2].Value, "href=\"#(@[A-Za-z0-9]+)\"");
                    if (target.Success && Symbolic(target.Groups[1].Value, order) == "end") return "";
                    return "<tr class=\"" + mt.Groups[1].Value + "\">" + mt.Groups[2].Value + "</tr>\n";
                }, RegexOptions.Singleline);

                // the map: 1 where it is -> the three searches (those on the page)
                html = Regex.Replace(html, "<div class=\"map\">.*?</div>\n?", mt =>
                {
                    int branches = 0;
                    var map = Regex.Replace(mt.Value, "<span class=\"branch ([abc])\">(.*?)<!--range [ABC]--></span>", br =>
                    {
                        var list = kept.Where(x => x.Part == br.Groups[1].Value.ToUpperInvariant()).ToList();
                        if (list.Count == 0) return "";
                        branches++;
                        var range = list.Count == 1 ? link(list[0].Key)
                            : "<a href=\"#" + list[0].Key + "\">" + t.Phrase("StepsRange", new Dictionary<string, object>
                                { { "A", number[list[0].Key].ToString(CultureInfo.InvariantCulture) }, { "B", number[list[list.Count - 1].Key].ToString(CultureInfo.InvariantCulture) } }) + "</a>";
                        return "<span class=\"branch " + br.Groups[1].Value + "\">" + br.Groups[2].Value + range + "</span>";
                    }, RegexOptions.Singleline);
                    if (branches < 2 || !keptKeys.Contains("look")) return "";
                    return Regex.Replace(Regex.Replace(map, " {2,}", " "), "\\s+</div>", "</div>");
                }, RegexOptions.Singleline);

                // the boxes of the signal path: a link to their step, with its number, or plain when it is left out
                html = Regex.Replace(html, "<a class=\"box\" href=\"#([A-Za-z0-9]+)\">(.*?)</a>", mt =>
                {
                    var k = mt.Groups[1].Value;
                    return keptKeys.Contains(k)
                        ? "<a class=\"box\" href=\"#" + k + "\" title=\"" + t.Phrase("StepLink", new Dictionary<string, object> { { "N", number[k].ToString(CultureInfo.InvariantCulture) } }) + "\">" +
                          "<span class=\"sn\">" + number[k].ToString(CultureInfo.InvariantCulture) + "</span>" + mt.Groups[2].Value + "</a>"
                        : "<span class=\"box\">" + mt.Groups[2].Value + "</span>";
                }, RegexOptions.Singleline);

                // every "go to step" link
                html = Regex.Replace(html, "<a class=\"go\" href=\"#([^\"]*)\"></a>", mt =>
                {
                    var k = mt.Groups[1].Value;
                    if (k.StartsWith("@")) k = Symbolic(k, order);
                    if (k.Length == 0) return "";
                    k = resolve(k);
                    return number.ContainsKey(k) ? link(k) : "";
                });

                // the numbers, the start, capitals; the template's own marks out
                foreach (var b in Blocks(html).OrderByDescending(x => x.Start))
                {
                    if (!number.ContainsKey(b.Key)) continue;
                    var step = html.Substring(b.Start, b.End - b.Start);
                    step = step.Replace("<div class=\"num\"></div>", "<div class=\"num\">" + number[b.Key] + "</div>");
                    if (markStart && b.Key == start)
                    {
                        step = Regex.Replace(step, "^<div class=\"step", "<div class=\"step start");
                        int h3 = step.IndexOf("</h3>", StringComparison.Ordinal);
                        if (h3 >= 0) step = step.Insert(h3, " <span class=\"here\">" + t.Phrase("StartHere") + "</span>");
                    }
                    step = Regex.Replace(step, "<li>(.*?)</li>", li => "<li>" + t.Cap(li.Groups[1].Value) + "</li>", RegexOptions.Singleline);
                    step = Regex.Replace(step, "<h3>(.*?)</h3>", h => "<h3>" + t.Cap(h.Groups[1].Value) + "</h3>", RegexOptions.Singleline);
                    step = Regex.Replace(step, "<td class=\"when\">(.*?)</td>", td => "<td class=\"when\">" + t.Cap(td.Groups[1].Value) + "</td>", RegexOptions.Singleline);
                    html = html.Substring(0, b.Start) + step + html.Substring(b.End);
                }
                html = Regex.Replace(html, " data-(part|role)=\"[^\"]*\"", "");
                html = Regex.Replace(html, "<ul>\\s*</ul>", "");
                return html;
            }
        }
    }
}
