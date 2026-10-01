using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
    /// </summary>
    public static partial class TroubleshootingCatalog
    {
        /// <summary>Writes the movement's step-by-step page to a temporary file and returns its path ("" when there is no movement).</summary>
        public static string WriteSteps(HelpDocument doc)
        {
            if (doc == null || doc.Movement == null) return "";
            try
            {
                var html = BuildSteps(doc.Movement, (ValveType)CanonicalValve(doc.Valve), (FeedbackType)CanonicalFeedback(doc.Feedback), doc.Failure);
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
        /// (a button usually shows only a triangle).
        /// </summary>
        public static string MovementName(MovementInfo mv)
        {
            if (mv == null) return "";
            try { return new StepsBuilder(Chain.Build(mv, ValveType.Unspecified, FeedbackType.Unspecified)).MoveName; }
            catch { return mv.Name ?? ""; }
        }

        /// <summary>
        /// The page, as HTML (public for tests). With a failure (an alarm the HMI
        /// raised itself, HelpDocument.Failure) only the steps about the signal that
        /// failed are on it.
        /// </summary>
        public static string BuildSteps(MovementInfo mv, ValveType valve, FeedbackType feedback, int failure = 0)
        {
            var b = new StepsBuilder(Chain.Build(mv, valve, feedback), failure);
            return b.Page();
        }

        // =====================================================================

        private sealed class Outcome
        {
            /// <summary>What was seen ("It stays OFF").</summary>
            public string When = "";
            /// <summary>What it means and what to do - "" when it only sends on.</summary>
            public string Fix = "";
            /// <summary>The step to go to (with no Fix) or to go on with when the fix did not help ("" = the last step).</summary>
            public string Next = "";
            /// <summary>The expected answer (green).</summary>
            public bool Good;
        }

        private sealed class Step
        {
            public string Key = "";
            public string Title = "";
            /// <summary>"look", "A", "B", "C", "end".</summary>
            public string Part = "";
            public readonly List<string> Do = new List<string>();
            public readonly List<Outcome> Outcomes = new List<Outcome>();
            /// <summary>Where the general manual covers it ("1.6 (2)").</summary>
            public string Manual = "";
            public int Number;
            /// <summary>What a C step checks: "target", "start", "reference", "position", "window", "time", "none" ("" for the others).</summary>
            public string Role = "";
        }

        /// <summary>A sensor of the movement with its drawing point (null when not drawn).</summary>
        private sealed class Sensor
        {
            public MovementInfo.Device Device;
            public WiringTable.Point Point;
            public bool IsPressureSwitch;

            /// <summary>The address the Inputs / Outputs page shows it by (X70), or the one the unit reads.</summary>
            public string Io { get { return Point != null ? Point.Address : Device.Address; } }
        }

        private sealed class StepsBuilder
        {
            private readonly Chain c;
            private readonly MovementInfo mv;
            private readonly List<Step> _steps = new List<Step>();
            private readonly WiringTable.Supply _supply;

            // what this movement is
            private readonly bool _spring;          // a spring valve's return: the coil must go OFF
            private readonly bool _coil;            // this movement's own coil drives it
            private readonly bool _fiveThree, _double;
            private readonly List<Sensor> _mustOn = new List<Sensor>(), _mustOff = new List<Sensor>();
            private readonly Sensor _switch;        // the clamp's pressure switch, when it is one of the lamps
            private readonly bool _endNotSensed;    // the movement ends where no sensor is (a single sensor's far end)
            private readonly bool _position;        // confirmed by a position reading (analogue / encoder)
            /// <summary>The signal that failed when the HMI raised the alarm itself (AlarmGeneration's sub-code), 0 when not known.</summary>
            private readonly int _failure;

            public StepsBuilder(Chain chain, int failure = 0)
            {
                c = chain;
                _failure = failure;
                mv = chain.Mv;
                _supply = WiringTable.SupplyOf(mv.Station);
                _double = c.DoubleCoil;
                _fiveThree = c.Valve == ValveType.ClosedCentre || c.Valve == ValveType.OpenCentre;
                _spring = c.Valve == ValveType.SingleSolenoidSpring && c.OutDevice == null && c.OtherDevice != null;
                _coil = !_spring;

                var on = c.On.Select(s => ToSensor(s)).ToList();
                var off = c.Off.Select(s => ToSensor(s)).ToList();
                _switch = on.Concat(off).FirstOrDefault(s => s.IsPressureSwitch);
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
                return System.Text.RegularExpressions.Regex.IsMatch(text, "(?<![A-Za-z0-9.])" + System.Text.RegularExpressions.Regex.Escape(word) + "(?![A-Za-z0-9])",
                                                                     System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // ---- words ----------------------------------------------------------

            private string Unit { get { return string.IsNullOrWhiteSpace(mv.Unit) ? "this unit" : mv.Unit.Trim(); } }

            private string _move, _otherMove, _glyph;

            /// <summary>The movement in words: "Clamp", "Up" - from the button when it has words, else from the drawing's name of its valve output.</summary>
            private string Move { get { return _move ?? (_move = MovementWord(true)); } }
            private string OtherMove { get { return _otherMove ?? (_otherMove = MovementWord(false)); } }

            /// <summary>The button's own sign (a glyph such as a triangle), or "" when it has words.</summary>
            private string Glyph { get { return _glyph ?? (_glyph = IsGlyph(mv.Name) ? mv.Name.Trim() : ""); } }

            /// <summary>"the [triangle] button" (its sign, in bold), or "the Clamp button".</summary>
            private string Button { get { return "the <b>" + E(Glyph.Length > 0 ? Glyph : Move) + "</b> button"; } }
            /// <summary>"the [triangle] button (Clamp)" - to press it.</summary>
            private string PressIt { get { return Button + (Glyph.Length > 0 ? " (" + MoveB + ")" : ""); } }

            /// <summary>The movement has a button of its own (a spring return usually has none: it happens when the coil's movement ends).</summary>
            private bool HasButton { get { return mv.Command != null && !string.IsNullOrWhiteSpace(mv.Command.Address); } }

            /// <summary>The other movement's button: "the [triangle] button (Release)", or "the Release button".</summary>
            private string OtherButton
            {
                get
                {
                    var g = IsGlyph(mv.OtherName) ? mv.OtherName.Trim() : "";
                    return "the <b>" + E(g.Length > 0 ? g : OtherMove) + "</b> button" + (g.Length > 0 ? " (<b>" + E(OtherMove) + "</b>)" : "");
                }
            }

            /// <summary>What starts the movement: "press the [triangle] button (Clamp)" - or, with no button of its own, "switch off ... (Up)".</summary>
            private string StartIt
            {
                get { return HasButton ? "press " + PressIt : "end " + E(OtherMove) + " (switch off or let go of " + OtherButton + ")"; }
            }
            /// <summary>The movement in words, for a title.</summary>
            public string MoveName { get { return Move; } }

            /// <summary>"<b>Clamp</b>": the movement.</summary>
            private string MoveB { get { return "<b>" + E(Move) + "</b>"; } }

            private static bool IsGlyph(string text)
            {
                var t = (text ?? "").Trim();
                return t.Length > 0 && t.Length <= 3 && !t.Any(char.IsLetterOrDigit);
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
                var t = System.Text.RegularExpressions.Regex.Replace(p.Text, "\\(.*?\\)", "").Trim();
                if (t.EndsWith(" Valve", StringComparison.OrdinalIgnoreCase)) t = t.Substring(0, t.Length - 6).Trim();
                return t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }

            private string MovementWord(bool mine)
            {
                // the button's own words (not a glyph)
                var own = mine ? mv.Name : mv.OtherName;
                if (!string.IsNullOrWhiteSpace(own) && !IsGlyph(own)) return own.Trim();
                // the two outputs' names on the drawing differ in the movement's word
                var a = OutputWords(c.Out);
                var b = OutputWords(c.OtherOut);
                if (a.Length > 0 && b.Length > 0)
                {
                    int k = 0;
                    while (k < a.Length && k < b.Length && string.Equals(a[k], b[k], StringComparison.OrdinalIgnoreCase)) k++;
                    var rest = string.Join(" ", (mine ? a : b).Skip(k));
                    if (rest.Length > 0 && k > 0) return rest;
                }
                // one output (a spring valve): its last word, the spring's is the opposite
                var coilWords = a.Length > 0 ? a : b;
                if (coilWords.Length > 0)
                {
                    var word = coilWords[coilWords.Length - 1];
                    bool coilIsMine = a.Length > 0;
                    if (mine == coilIsMine) return word;
                    var opposite = Opposite(word);
                    if (opposite.Length > 0) return opposite;
                }
                // the button's sign
                var glyph = (mine ? mv.Name : mv.OtherName ?? "").Trim();
                switch (glyph)
                {
                    case "\u25B2": return "Up";
                    case "\u25BC": return "Down";
                    case "\u25C0": return "Left";
                    case "\u25B6": return "Right";
                }
                bool left = (mv.Side == "A") == mine;
                return left ? "the left button's movement" : "the right button's movement";
            }

            private string Cyl
            {
                get
                {
                    var n = CylinderShort;
                    return n.Length > 0 ? "cylinder <b>" + E(n) + "</b>" : "the cylinder";
                }
            }

            /// <summary>The cylinder's name on the drawing, its remarks in brackets left out.</summary>
            private string CylinderShort
            {
                get { return System.Text.RegularExpressions.Regex.Replace(CylinderName(c.Air), "\\s*\\(.*?\\)", "").Trim(); }
            }

            private string PnSheet { get { return c.Air != null && c.Air.PnSheet.Length > 0 ? " (pneumatic sheet " + E(c.Air.PnSheet) + ")" : ""; } }
            private string ValveName { get { return c.Air != null && c.Air.Valve.Length > 0 ? "valve <b>" + E(c.Air.Valve) + "</b>" : "the valve"; } }

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
                int t = p.Card.IndexOf(", terminal ", StringComparison.OrdinalIgnoreCase);
                if (t < 0) { card = p.Card; return; }
                card = p.Card.Substring(0, t).Trim();
                terminal = p.Card.Substring(t + 11).Trim();
            }

            /// <summary>"card <b>U4 ...</b>, the LED of terminal <b>Y4</b> (Y44)" - where on the card an output's LED is.</summary>
            private string CardLed(WiringTable.Point p, string address)
            {
                string card, terminal;
                CardParts(p, out card, out terminal);
                var where = card.Length > 0 ? "PLC card <b>" + E(card) + "</b>" : "the PLC's output card";
                if (terminal.Length > 0 && !string.Equals(terminal, address, StringComparison.OrdinalIgnoreCase))
                    return where + ", the LED of its terminal <b>" + E(terminal) + "</b> (" + Code(address) + ")";
                return where + ", the LED of " + Code(address);
            }

            private string ZeroVolt
            {
                get { return _supply.ZeroVoltWire.Length > 0 ? "0 V (wire " + E(_supply.ZeroVoltWire) + ")" : "0 V"; }
            }

            /// <summary>This movement's coil output (Y32), or the other side's for a spring return.</summary>
            private WiringTable.Point OutPoint { get { return _spring ? c.OtherOut : c.Out; } }
            private MovementInfo.Device OutDev { get { return _spring ? c.OtherDevice : c.OutDevice; } }
            private string OutCode { get { return OutDev != null ? Code(OutDev.Address) : "the valve output"; } }

            /// <summary>The cylinder's hoses as the pneumatic drawing numbers them ("hoses <b>23 (A) / 24 (B)</b>"), or "".</summary>
            private string Hose(bool thisSide)
            {
                var h = c.Air != null ? c.Air.Hoses : "";
                if (h.Length == 0) return "";
                return (h.Contains("/") ? "hoses" : "hose") + " <b>" + E(h) + "</b>";
            }

            private string BothHoses
            {
                get { return c.Air != null && c.Air.Hoses.Length > 0 ? "both hoses (<b>" + E(c.Air.Hoses) + "</b>)" : "both sides"; }
            }

            private static string SensorName(Sensor s)
            {
                var p = s.Point;
                var sb = new StringBuilder();
                if (s.IsPressureSwitch) sb.Append("pressure switch ");
                else if (p != null && p.Tag.Length > 0 && !p.Tag.StartsWith("SW", StringComparison.OrdinalIgnoreCase)) sb.Append("sensor ");
                if (p != null && p.Tag.Length > 0) sb.Append("<b>" + E(p.Tag) + "</b> (" + Code(p.Address) + ")");
                else sb.Append(Code(s.Device.Address));
                return sb.ToString();
            }

            /// <summary>Where a sensor is: its wire, sheet and card terminal; the HMI's relay for it.</summary>
            private string SensorWhere(Sensor s)
            {
                var p = s.Point;
                if (p == null) return "";
                var parts = new List<string>();
                if (p.Wire.Length > 0) parts.Add("wire " + E(p.Wire));
                if (p.Sheet.Length > 0) parts.Add("sheet " + E(p.Sheet));
                if (p.Card.Length > 0) parts.Add(E(p.Card));
                if (p.Via.Length > 0) parts.Add("read through " + E(p.Via));
                return parts.Count == 0 ? "" : " &ndash; " + string.Join(", ", parts);
            }

            private string Sensors(IEnumerable<Sensor> list, string glue = " and ")
            {
                return string.Join(glue, list.Select(SensorName));
            }

            // ---- the steps --------------------------------------------------------

            private Step Add(string key, string part, string title, string manual = "")
            {
                var s = new Step { Key = key, Part = part, Title = title, Manual = manual };
                _steps.Add(s);
                return s;
            }

            private static Outcome Go(string when, string next, bool good = false)
            {
                return new Outcome { When = when, Next = next, Good = good };
            }

            private static Outcome Fix(string when, string fix, string next = "")
            {
                return new Outcome { When = when, Fix = fix, Next = next };
            }

            private void Build()
            {
                Look();
                if (_spring) NotMovedSpring(); else NotMovedCoil();
                StoppedOnTheWay();
                Arrived();
                var end = Add("end", "end", "Still not solved?");
                end.Do.Add("Stop here and call maintenance or the Kaban Machine Technical Service. Tell them: <b>" + E(Unit) + "</b>, the movement " + MoveB + (HasButton ? " (" + Button + ")" : "") +
                           (string.IsNullOrWhiteSpace(mv.Error) ? "" : ", alarm " + Code(mv.Error)) +
                           (mv.AlarmTime.HasValue ? " at " + mv.AlarmTime.Value.ToString("HH:mm:ss") : "") +
                           ", the step you reached and what you saw. Print or save this page for them.");
                end.Do.Add("After any repair, test the movement slowly in manual mode first" +
                           (c.Feedback == FeedbackType.EncoderReference ? "; after an encoder or reference sensor was changed, reference (home) the unit and check the position." : "."));
                for (int i = 0; i < _steps.Count; i++) _steps[i].Number = i + 1;
            }

            /// <summary>Step 1: where is the cylinder? - the three searches.</summary>
            private void Look()
            {
                var s = Add("look", "look", "Where is the cylinder now?");
                s.Do.Add("Stand clear of it and <b>look</b> at " + Cyl + PnSheet + ". Do not touch anything yet.");
                string target = _position ? " (the position reading" + (string.IsNullOrWhiteSpace(mv.Position) ? "" : " " + Code(mv.Position)) + " at its target)"
                              : _mustOn.Count > 0 ? " (" + Sensors(_mustOn) + " ON" + (_mustOff.Count > 0 ? ", " + Sensors(_mustOff) + " OFF" : "") + ")"
                              : _endNotSensed && _mustOff.Count > 0 ? " (no sensor there: " + Sensors(_mustOff) + " must be OFF)" : "";
                s.Do.Add("Where it should be: at the end of " + MoveB + target + ". Where it started from: the end of <b>" + E(OtherMove) + "</b>.");
                if (_mustOn.Concat(_mustOff).Any())
                    s.Do.Add("Cannot see it? The sensors tell (the side bar's <i>Inputs / Outputs</i> page, or the lamps on the unit's buttons): " + LookBySensors() + ".");
                if (_position && !string.IsNullOrWhiteSpace(mv.Position))
                    s.Do.Add("The unit shows its position at " + Code(mv.Position) + ": does the number change when it moves?");
                s.Outcomes.Add(Go("It has <b>not moved</b> at all &ndash; still where it started.", "A1"));
                s.Outcomes.Add(Go("It moved but <b>stopped on the way</b>.", "B1"));
                s.Outcomes.Add(Go("It <b>arrived</b> at the end of " + MoveB + ", yet the alarm came.", "C1", good: true));
            }

            private string LookBySensors()
            {
                var parts = new List<string>();
                var start = _mustOff.Where(x => !x.IsPressureSwitch).ToList();
                if (start.Count > 0) parts.Add(Sensors(start, " / ") + " still ON = not moved");
                if (_mustOn.Count > 0) parts.Add(Sensors(_mustOn, " / ") + " ON = arrived");
                if (start.Count > 0 && _mustOn.Count > 0) parts.Add("both OFF = on the way (or stopped)");
                if (_endNotSensed && _mustOff.Count > 0) parts.Add(Sensors(_mustOff, " / ") + " OFF = it has left; nothing sees the far end");
                return string.Join("; ", parts);
            }

            // ---- A: it did not move (its own coil) -------------------------------------

            private void NotMovedCoil()
            {
                var o = OutPoint;
                // A1: is the output switched on?
                var a1 = Add("A1", "A", "Does the PLC switch on " + OutCode + "?", "1.6 (1)" + (_double ? ", 1.4" : ""));
                a1.Do.Add("Reset the alarm. In <b>manual mode</b>, open the side bar's <i>Inputs / Outputs</i> page and find " + OutCode +
                          (o != null && o.Text.Length > 0 ? " (" + E(o.Text) + ")" : "") + ".");
                a1.Do.Add("Press " + PressIt + " on " + E(Unit) + " and watch " + OutCode + "." +
                          (_fiveThree ? " (A 5/3 valve moves only while its coil is ON: if the cylinder moves only while the button is held, keep holding it.)" : "") +
                          (_double && c.OtherDevice != null ? " The other coil's output " + Code(c.OtherDevice.Address) + " (" + E(OtherMove) + ") must stay <b>OFF</b>." : ""));
                a1.Outcomes.Add(Go(OutCode + " stays <b>OFF</b> (the button may be grey).", "A2"));
                if (_double && c.OtherDevice != null)
                    a1.Outcomes.Add(Fix(OutCode + " and " + Code(c.OtherDevice.Address) + " are <b>both ON</b>.",
                        "With both coils on, the valve cannot switch. Let go of " + OtherButton + "; if both still come on together, the PLC program commands both " +
                        "(or a wire is crossed) &ndash; ask the PLC programmer to interlock them.", "A3"));
                a1.Outcomes.Add(Go(OutCode + " comes <b>ON</b>" + (_double && c.OtherDevice != null ? " and " + Code(c.OtherDevice.Address) + " stays OFF" : "") + ".", "A3", good: true));

                // A2: why not?
                var a2 = Add("A2", "A", "Why does the PLC not switch it on?", "1.6 (1)");
                a2.Do.Add("Rest the pointer on " + Button + ": a grey button says what it is waiting for.");
                if (mv.Conditions.Count > 0)
                    a2.Do.Add("It waits for: " + string.Join("; ", mv.Conditions.Select(ConditionText)) + ".");
                a2.Do.Add("Look for what stops a movement: a guard door open, an emergency stop pressed, the air pressure switch, " +
                          "another unit in the way that must move first, the machine not in manual mode.");
                a2.Outcomes.Add(Fix("Something it waits for is <b>not met</b>.",
                    "Make it right (close the guard, release the emergency stop, move the other unit first). <b>Never bypass a safety interlock.</b> Then press " + Button + " again."));
                a2.Outcomes.Add(Fix("Everything is met, " + OutCode + " still stays OFF.",
                    "The PLC program holds it: in the PLC (online monitor) watch " + OutCode + "'s rung while " + Button +
                    " is pressed" + (mv.Command != null ? " (the HMI's command is " + Code(mv.Command.Address) + ")" : "") +
                    " &ndash; the contact that stays open is the reason. This needs the PLC programmer or maintenance."));

                // A3: the card
                var a3 = Add("A3", "A", "Does the output card switch " + OutCode + "?", "1.6 (2)");
                a3.Do.Add("Keep " + Button + " pressed. On " + CardLed(o, OutDev != null ? OutDev.Address : "") + " must be lit.");
                if (o != null && o.Wire.Length > 0)
                    a3.Do.Add("Measure: <b>24 V DC</b> between the terminal (wire <b>" + E(o.Wire) + "</b>" + (o.Sheet.Length > 0 ? ", sheet " + E(o.Sheet) : "") + ") and " + ZeroVolt + ".");
                string afterCard = Relay(o) ? "A4" : "A5";
                a3.Outcomes.Add(Fix("The LED is off, or there is no voltage.",
                    "The output channel does not switch: check the card's fuse and its supply (the common terminal); replace the fuse or the card. " +
                    "A fuse that blows again means a short in the wiring or a shorted coil.", afterCard));
                a3.Outcomes.Add(Go("The LED is on and 24 V is there.", afterCard, good: true));

                // A4: the relay
                if (Relay(o))
                {
                    var a4 = Add("A4", "A", "Does relay " + E(o.Relay) + " pull in?", "1.5");
                    a4.Do.Add("Relay <b>" + E(o.Relay) + "</b>" + (o.RelaySheet.Length > 0 ? " (sheet " + E(o.RelaySheet) + ")" : "") +
                              ", in the cabinet: with " + Button + " pressed its LED must light, and it clicks.");
                    if (o.Wire.Length > 0) a4.Do.Add("Measure its coil: <b>24 V</b> on wire <b>" + E(o.Wire) + "</b> against " + ZeroVolt + ".");
                    a4.Outcomes.Add(Fix("No voltage at the relay.",
                        (o.Wire.Length > 0 ? "Wire " + E(o.Wire) + " is" : "The wire is") + " broken between the card terminal and the relay (or a terminal on the way): repair it.", "A5"));
                    a4.Outcomes.Add(Fix("Voltage is there but it does not pull in.", "The relay's coil is open: replace relay " + E(o.Relay) + ".", "A5"));
                    a4.Outcomes.Add(Go("It pulls in.", "A5", good: true));
                }
                else if (o != null)
                {
                    // no relay: A4 does not exist - A3's good answer goes to A5
                }

                // A5: the coil
                string coil = o != null && o.Coil.Length > 0 ? "coil <b>" + E(o.Coil) + "</b>" : "the valve coil";
                var a5 = Add("A5", "A", "Does " + StripB(coil) + " get power?", "1.3 (1)" + (Relay(o) ? ", 1.5 (2)" : ""));
                a5.Do.Add("Find " + coil + " on " + ValveName + PnSheet + ". With " + Button + " pressed its LED must be lit.");
                if (o != null && o.CoilWire.Length > 0)
                    a5.Do.Add("Measure: <b>24 V DC</b> between wire <b>" + E(o.CoilWire) + "</b>" + (o.CoilSheet.Length > 0 ? " (sheet " + E(o.CoilSheet) + ")" : "") + " and " + ZeroVolt + ".");
                a5.Outcomes.Add(Fix("No voltage on " + (o != null && o.CoilWire.Length > 0 ? "wire " + E(o.CoilWire) : "the coil") + ".",
                    Relay(o) ? "The relay's contact does not pass it (burnt) or the wire to the coil is broken: across the closed contact there should be about 0 V; " +
                               "replace relay " + E(o.Relay) + " or repair the wire."
                             : "The wire between the card and the coil is broken: repair it.", "A6"));
                a5.Outcomes.Add(Fix("24 V is there but the coil's LED stays off.", "The connector or the coil: seat the connector again; if it still stays off, replace the coil.", "A6"));
                a5.Outcomes.Add(Go("24 V and the LED is on.", "A6", good: true));

                // A6: the valve
                var a6 = Add("A6", "A", "Does the valve switch?", "1.3 (2)");
                a6.Do.Add("Make sure nobody is near " + Cyl + ". Press the <b>manual override</b> of " + coil + " on " + ValveName + ".");
                a6.Outcomes.Add(Fix("With the override the cylinder <b>moves</b>.",
                    "The valve is fine but the coil does not shift it although it has power: replace " + StripB(coil) + ".", "A7"));
                a6.Outcomes.Add(Go("It does not move with the override either.", "A7"));

                AirAndPath("A7", "A8", thisSide: true);
            }

            // ---- A: it did not move (a spring valve's return) ----------------------------

            private void NotMovedSpring()
            {
                var o = OutPoint;          // the OTHER movement's coil
                string coil = o != null && o.Coil.Length > 0 ? "coil <b>" + E(o.Coil) + "</b>" : "the valve coil";

                var a1 = Add("A1", "A", "Does the PLC switch " + OutCode + " OFF?", "1.4 (2), 1.6");
                a1.Do.Add("This movement is made by the valve's <b>spring</b>: " + E(OtherMove) + "'s coil output " + OutCode +
                          " must go <b>OFF</b> and stay OFF, then the spring switches the valve back.");
                a1.Do.Add("Reset the alarm. In <b>manual mode</b> open the side bar's <i>Inputs / Outputs</i> page and find " + OutCode + ". " + Cap(StartIt) + " and watch it.");
                a1.Outcomes.Add(Go(OutCode + " stays <b>ON</b>.", "A2"));
                a1.Outcomes.Add(Go(OutCode + " goes <b>OFF</b>.", "A3", good: true));

                var a2 = Add("A2", "A", "Why does the PLC keep it ON?", "1.6 (1)");
                a2.Do.Add("Is " + E(OtherMove) + " still commanded" + (mv.OtherCommand != null ? " (" + Code(mv.OtherCommand.Address) + ", " + OtherButton + ")" : "") +
                          "? Is the machine in manual mode, and is nothing else holding the movement (a sequence step in automatic)?");
                if (HasButton && mv.Conditions.Count > 0) a2.Do.Add(Cap(Button) + " waits for: " + string.Join("; ", mv.Conditions.Select(ConditionText)) + ".");
                a2.Outcomes.Add(Fix("Something still commands it.", "End that command (let go of the button, stop the cycle)."));
                a2.Outcomes.Add(Fix("Nothing should command it, it stays ON.",
                    "The PLC program holds it: in the PLC (online monitor) watch " + OutCode + "'s rung &ndash; this needs the PLC programmer or maintenance."));

                var a3 = Add("A3", "A", "Does the output card switch it off?", "1.6 (2)");
                a3.Do.Add("On " + CardLed(o, OutDev != null ? OutDev.Address : "") + " must be <b>off</b>" +
                          (o != null && o.Wire.Length > 0 ? ", and wire <b>" + E(o.Wire) + "</b> must have <b>no</b> voltage against " + ZeroVolt : "") + ".");
                a3.Outcomes.Add(Fix("The LED or the voltage stays on while the HMI shows it OFF.", "The output channel is stuck on: replace the card (or its channel).", Relay(o) ? "A4" : "A5"));
                a3.Outcomes.Add(Go("Off.", Relay(o) ? "A4" : "A5", good: true));

                if (Relay(o))
                {
                    var a4 = Add("A4", "A", "Does relay " + E(o.Relay) + " drop out?", "1.5");
                    a4.Do.Add("Relay <b>" + E(o.Relay) + "</b>" + (o.RelaySheet.Length > 0 ? " (sheet " + E(o.RelaySheet) + ")" : "") + ": its LED must go off.");
                    a4.Outcomes.Add(Fix("It stays pulled in with no voltage on its coil.", "The relay sticks: replace relay " + E(o.Relay) + ".", "A5"));
                    a4.Outcomes.Add(Go("It drops out.", "A5", good: true));
                }

                var a5 = Add("A5", "A", "Is " + StripB(coil) + " without power?", "1.3");
                a5.Do.Add("On " + ValveName + PnSheet + ": the LED of " + coil + " must be off" +
                          (o != null && o.CoilWire.Length > 0 ? ", and wire <b>" + E(o.CoilWire) + "</b> must have no voltage against " + ZeroVolt : "") + ".");
                a5.Outcomes.Add(Fix("There is still voltage on the coil.",
                    Relay(o) ? "The relay's contact is welded, or the coil wire touches +24 V: replace relay " + E(o.Relay) + " / repair the wire."
                             : "The coil wire touches +24 V: repair the wiring.", "A6"));
                a5.Outcomes.Add(Go("No voltage.", "A6", good: true));

                var a6 = Add("A6", "A", "Does the valve spring back?", "1.3 (2)");
                a6.Do.Add("Look at the <b>manual override</b> of " + coil + " on " + ValveName + ": a turned (locked) override keeps the valve switched.");
                a6.Do.Add("With no power and the override released, the spool must return by its spring &ndash; you hear the exhaust.");
                a6.Outcomes.Add(Fix("The override was locked.", "Release it.", "A7"));
                a6.Outcomes.Add(Fix("The valve does not return.", "The spool sticks or the spring is broken: clean or replace " + StripB(ValveName) + "; check the air quality (filter, water, oil).", "A7"));
                a6.Outcomes.Add(Go("It returns, the cylinder still does not move.", "A7"));

                AirAndPath("A7", "A8", thisSide: true);
            }

            /// <summary>The air to the cylinder, then its path - the end of "it did not move".</summary>
            private void AirAndPath(string airKey, string pathKey, bool thisSide)
            {
                var air = Add(airKey, "A", "Is there air to move it?", "1.2");
                air.Do.Add("Read the pressure gauge at the air unit: it must show the machine's working pressure (at least 5 bar).");
                if (c.MainAir != null)
                    air.Do.Add("The main air switch " + (c.MainAir.Tag.Length > 0 ? "<b>" + E(c.MainAir.Tag) + "</b> " : "") + "(" + Code(c.MainAir.Address) +
                               (c.MainAir.Text.Length > 0 ? ", " + E(c.MainAir.Text) : "") + ") must be <b>ON</b> on the <i>Inputs / Outputs</i> page.");
                if (c.Air != null && c.Air.Pressure.Length > 0)
                    air.Do.Add("This cylinder's pressure: " + E(c.Air.Pressure) + " &ndash; with the low pressure selected it may not have the force.");
                var hose = Hose(thisSide);
                air.Do.Add("Command the movement again (or hold the override): air must come out of " + ValveName +
                           (hose.Length > 0 ? " into " + hose : "") + " towards " + Cyl + ". Listen and feel along the hoses for leaks and kinks; " +
                           "the flow controls at the cylinder's ports must not be closed.");
                air.Outcomes.Add(Fix("The pressure is low, or there is no air.", "Restore the supply: open the shut-off valve, check the compressor, change a clogged filter, repair leaks.", pathKey));
                air.Outcomes.Add(Fix("Air reaches the valve, but none comes out" + (hose.Length > 0 ? " into " + hose : "") + ".",
                    "The valve's spool is stuck: clean or replace " + StripB(ValveName) + "; check the air quality (filter, water, oil).", pathKey));
                air.Outcomes.Add(Fix("A hose leaks, is kinked or off; a flow control is closed.", "Repair or replace the hose or fitting; open the flow control to its mark.", pathKey));
                air.Outcomes.Add(Go("Air reaches the cylinder, it still does not move.", pathKey));

                var path = Add(pathKey, "A", "Is the cylinder free to move?", "1.1");
                path.Do.Add("<b>Lock out</b> the machine (electrical and air) and exhaust " + BothHoses + " at " + ValveName + ": a cylinder can move suddenly when air comes back.");
                path.Do.Add("Look along the whole stroke: a jammed profile, a chip, a fallen part, the tooling. Then move " + Cyl + " <b>by hand</b> through its stroke.");
                path.Outcomes.Add(Fix("Something blocks it, or it is stiff / notchy.",
                    "Remove the obstruction; align, lubricate or replace the guide or bearing; look for a bent rod. Check the tooling before starting again."));
                path.Outcomes.Add(Fix("It moves freely by hand.",
                    "Then the force is too small: the load grew, the pressure is set too low" + (c.Air != null && c.Air.Pressure.Length > 0 ? " (see the clamping pressure above)" : "") +
                    ", or the cylinder's seals leak (air blows through from one side to the other). Set the regulator within its limit or have the cylinder repaired."));
            }

            // ---- B: it stopped on the way ------------------------------------------------

            private void StoppedOnTheWay()
            {
                var b1 = Add("B1", "B", _spring ? "Does " + OutCode + " stay OFF the whole way?" : "Does " + OutCode + " stay ON the whole way?",
                             _spring ? "1.4 (2)" : _fiveThree ? "1.4 (3)" : c.Valve == ValveType.SingleSolenoidSpring ? "1.4 (2)" : "1.4 (1)");
                b1.Do.Add("Watch " + OutCode + " on the <i>Inputs / Outputs</i> page while " + MoveB + " is carried out, from the start until the cylinder stops.");
                if (_fiveThree)
                    b1.Do.Add("A <b>5/3 valve</b> moves the cylinder only while the coil is ON: when the signal ends early, the valve goes to its centre and the cylinder stops where it is" +
                              (ClosedCentre == true ? " (its closed centre keeps the air trapped in the cylinder)" : ClosedCentre == false ? " (its open centre lets the cylinder drift)" : "") + ".");
                else if (c.Valve == ValveType.SingleSolenoidSpring && !_spring)
                    b1.Do.Add("A <b>spring valve</b> needs its coil ON for the whole stroke and while it holds: when it drops, the spring pulls the cylinder back.");
                else if (_spring)
                    b1.Do.Add("Any moment " + OutCode + " comes ON again, the valve switches back towards " + E(OtherMove) + ".");
                else if (_double && c.OtherDevice != null)
                    b1.Do.Add("A <b>5/2 double solenoid</b> valve stays where it was switched, but " + E(OtherMove) + "'s output " + Code(c.OtherDevice.Address) +
                              " must not come ON on the way.");
                b1.Do.Add("Wiggle the coil's connector and the cable while it moves: a loose contact drops the signal.");
                b1.Outcomes.Add(Fix(_spring ? "It comes ON on the way." : "It goes OFF too early" + (_double && c.OtherDevice != null && !_fiveThree ? ", or the other output comes ON" : "") + ".",
                    "A loose connector or wire: repair it. If the PLC switches it itself, the program must keep the signal until " +
                    (_mustOn.Count > 0 ? Sensors(_mustOn) + " confirms the end" : "the end is reached") + " &ndash; ask the PLC programmer." +
                    (_fiveThree ? " In manual mode, keep " + Button + " held until the cylinder arrives." : ""), "B2"));
                b1.Outcomes.Add(Go("It stays as it should.", "B2", good: true));

                var b2 = Add("B2", "B", "Does the air pressure hold while it moves?", "1.2");
                b2.Do.Add("Watch the pressure gauge while the cylinder moves: a large drop means too little air reaches it.");
                var hose = Hose(true);
                b2.Do.Add("Check " + (hose.Length > 0 ? hose : "the hoses") + " for leaks and kinks, and the flow controls at the cylinder: nearly closed, the cylinder creeps and stops.");
                if (c.Air != null && c.Air.Pressure.Length > 0) b2.Do.Add("Its pressure: " + E(c.Air.Pressure) + ".");
                b2.Outcomes.Add(Fix("The pressure drops, a hose leaks, a flow control is closed.", "Repair the supply or the hose; set the flow control to its mark.", "B3"));
                b2.Outcomes.Add(Go("The air is fine.", "B3", good: true));

                var b3 = Add("B3", "B", "Is something in its way?", "1.1");
                b3.Do.Add("<b>Lock out</b> and exhaust " + BothHoses + ". Look where it stopped: a profile, a chip, the tooling, a clamp jaw. Move " + Cyl + " by hand past that point.");
                b3.Outcomes.Add(Fix("It is blocked or stiff there.", "Remove the cause; align, lubricate or repair the guide; check the rod.", "B4"));
                b3.Outcomes.Add(Go("It is free.", "B4", good: true));

                var b4 = Add("B4", "B", "Is it only too slow?", "1.8");
                b4.Do.Add("Time the stroke: from " + OutCode + " switching to " +
                          (_mustOn.Count > 0 ? Sensors(_mustOn) + " ON" : _endNotSensed && _mustOff.Count > 0 ? "the end (" + Sensors(_mustOff) + " goes OFF on the way)" : "the end") +
                          ". The PLC Monitor's <i>Modified</i> column shows when each changed.");
                b4.Do.Add("Compare it with " + TimeAllowed + (string.IsNullOrWhiteSpace(mv.Error) ? "" : " before alarm " + Code(mv.Error)) +
                          (_failure > 0 ? "." : " (in the PLC program)."));
                b4.Outcomes.Add(Fix("It needs about as long as allowed, or longer.", "Open the flow controls to the right speed; if the stroke is right and only the time is short, " +
                    (_failure > 0 ? "give this alarm a longer delay in Alarm Settings." : "have the monitoring time raised (after checking it is safe).")));
                b4.Outcomes.Add(Go("It is well within the time.", "end"));
            }

            // ---- C: it arrived ---------------------------------------------------------------

            private void Arrived()
            {
                switch (c.Feedback)
                {
                    case FeedbackType.AnalogueMagnetic:
                        ArrivedAnalogue();
                        break;
                    case FeedbackType.EncoderReference:
                        ArrivedEncoder();
                        break;
                    default:
                        ArrivedSensors();
                        break;
                }
            }

            private void ArrivedSensors()
            {
                int n = 1;
                bool none = _mustOn.Count == 0 && _mustOff.Count == 0;
                string time = "C" + (1 + _mustOn.Count + (_mustOff.Count > 0 ? 1 : 0) + (none ? 1 : 0));
                // each sensor that must come ON
                for (int i = 0; i < _mustOn.Count; i++)
                {
                    var s = _mustOn[i];
                    string next = i + 1 < _mustOn.Count ? "C" + (n + 1) : _mustOff.Count > 0 ? "C" + (n + 1) : time;
                    if (s.IsPressureSwitch) PressureSwitchOn("C" + n, s, next);
                    else SensorOn("C" + n, s, next);
                    n++;
                }
                // the ones that must go OFF, together
                if (_mustOff.Count > 0)
                {
                    var key = "C" + n;
                    string title = _endNotSensed ? "Has " + Sensors(_mustOff) + " gone OFF?"
                                                 : "Has " + Sensors(_mustOff) + " at the other end gone OFF?";
                    var st = Add(key, "C", title, "1.7 (2)");
                    st.Role = "start";
                    foreach (var s in _mustOff)
                        st.Do.Add(SensorName(s) + SensorWhere(s) + ": at the end of " + MoveB + " it must be <b>OFF</b> &ndash; its LED off, " + Code(s.Io) + " OFF on the <i>Inputs / Outputs</i> page.");
                    if (_endNotSensed)
                        st.Do.Add("Nothing sees the end of " + MoveB + ": the PLC takes the sensor going OFF, and the time, as arrived. Make sure the cylinder really reached its end (step 1).");
                    var sw = _mustOff.FirstOrDefault(x => x.IsPressureSwitch);
                    if (sw != null)
                        st.Outcomes.Add(Fix(SensorName(sw) + " stays ON.",
                            "The clamp line keeps its pressure: the valve did not exhaust it" + (_fiveThree && ClosedCentre == true ? " (a closed-centre valve traps the air)" : "") +
                            ", or the switch is stuck &ndash; check with a gauge; replace a stuck switch."));
                    if (_mustOff.Any(x => !x.IsPressureSwitch))
                        st.Outcomes.Add(Fix(Sensors(_mustOff.Where(x => !x.IsPressureSwitch)) + " stays ON although the cylinder has left.",
                            "The sensor is stuck (a welded reed contact) or its cable is shorted" +
                            (c.Feedback == FeedbackType.SingleMagnetic || c.Feedback == FeedbackType.TwoMagnetic ? "; on a short cylinder the magnet may reach it from the far end &ndash; move the sensor" : "") +
                            ". Replace the sensor or repair the cable."));
                    st.Outcomes.Add(Go("It is OFF.", time, good: true));
                    n++;
                }
                if (_mustOn.Count == 0 && _mustOff.Count == 0)
                {
                    var st = Add("C1", "C", "What confirms the end?", "1.7");
                    st.Role = "none";
                    st.Do.Add("The unit has no sensor for " + MoveB + " on the HMI. The PLC confirms it by time" +
                              (c.Feedback == FeedbackType.Unspecified ? " or by a sensor the HMI does not show &ndash; see the electrical drawing" : "") + ".");
                    st.Outcomes.Add(Go("Go on with the time allowed.", time));
                }
                TimeStep(time);
            }

            private void SensorOn(string key, Sensor s, string next)
            {
                var st = Add(key, "C", "Does " + SensorName(s) + " see it?", "1.7 (1)");
                st.Role = "target";
                st.Do.Add(SensorName(s) + SensorWhere(s) + (s.Point != null && s.Point.Text.Length > 0 ? " &ndash; " + E(s.Point.Text) : "") + ".");
                st.Do.Add("With the cylinder at the end of " + MoveB + ": the sensor's own <b>LED</b> must be lit, and " + Code(s.Io) +
                          " must be <b>ON</b> on the <i>Inputs / Outputs</i> page (and the lamp on " + Button + ")." +
                          (s.Point != null && !string.Equals(s.Point.Address, s.Device.Address, StringComparison.OrdinalIgnoreCase)
                              ? " The PLC copies it into " + Code(s.Device.Address) + "." : ""));
                st.Outcomes.Add(Fix("The sensor's LED is <b>off</b>.",
                    c.Feedback == FeedbackType.SingleInductive
                        ? "Check the gap between the sensor and its metal flag and that the flag is not bent; set the gap within the sensing distance; replace the sensor if it never switches."
                        : "Loosen its clamp, slide the sensor along the cylinder until its LED lights reliably, tighten it. If it never lights, replace the sensor.", next));
                st.Outcomes.Add(Fix("The LED is on, but " + Code(s.Io) + " stays OFF on the HMI.",
                    "The signal does not reach the PLC: the sensor's cable and connector" + (s.Point != null && s.Point.Wire.Length > 0 ? ", wire " + E(s.Point.Wire) : "") +
                    (s.Point != null && s.Point.Card.Length > 0 ? ", the input " + E(s.Point.Card) + " (its LED)" : ", the input card's LED") +
                    (_supply.SensorSupplyWire.Length > 0 ? ", the sensors' +24 V (wire " + E(_supply.SensorSupplyWire) + ")" : ", the input common") + ". Repair what is broken.", next));
                st.Outcomes.Add(Go("It is ON.", next, good: true));
            }

            private void PressureSwitchOn(string key, Sensor s, string next)
            {
                var st = Add(key, "C", "Does " + SensorName(s) + " switch?", "1.7 (pressure switch)");
                st.Role = "target";
                st.Do.Add(SensorName(s) + SensorWhere(s) + (s.Point != null && s.Point.Text.Length > 0 ? " &ndash; " + E(s.Point.Text) : "") +
                          ": when the cylinder has clamped, the pressure in the clamp line switches it <b>ON</b>" +
                          (_mustOff.Count > 0 ? " (and " + Sensors(_mustOff.Where(x => !x.IsPressureSwitch)) + " at the back must be OFF)" : "") + ".");
                if (c.Air != null && c.Air.Pressure.Length > 0) st.Do.Add("On the drawings: " + E(c.Air.Pressure) + ".");
                st.Do.Add("Put a gauge on the clamp line" + (Hose(true).Length > 0 ? " (" + Hose(true) + ")" : "") + " while it is clamped.");
                st.Outcomes.Add(Fix("The pressure is low.", "The clamp line leaks (a hose, a fitting, the cylinder's seal) or the low pressure is selected: find and repair the leak.", next));
                st.Outcomes.Add(Fix("The pressure is right, the switch stays OFF.", "The switch's setpoint is too high or has drifted (set it just under the working pressure), or the switch has failed, or it sits on the wrong line: set or replace it.", next));
                st.Outcomes.Add(Fix("The switch is ON, but " + Code(s.Io) + " stays OFF on the HMI.", "Its cable, connector or the input channel: repair it.", next));
                st.Outcomes.Add(Fix("It switches ON and OFF while it holds.", "The setpoint is too close to the pressure, or the supply swings: give the setpoint a margin, steady the supply (a short delay in the PLC helps).", next));
                st.Outcomes.Add(Go("It is ON and steady.", next, good: true));
            }

            private void ArrivedAnalogue()
            {
                var st = Add("C1", "C", "Does the position reading reach the target?", "1.7 (analogue)");
                st.Role = "position";
                if (!string.IsNullOrWhiteSpace(mv.Position)) st.Do.Add("The unit shows the position at " + Code(mv.Position) + ".");
                if (c.Air != null && c.Air.Measure.Length > 0) st.Do.Add("Measured by: " + E(c.Air.Measure) + ".");
                st.Do.Add("Move the cylinder slowly and watch the number: it must change evenly with the movement, and at the end of " + MoveB +
                          " stand at its target within the window the PLC allows.");
                st.Outcomes.Add(Fix("It does not change, or reads zero / the lowest value.", "The sensor's signal is lost (below 4 mA): a broken cable or a failed sensor &ndash; repair the cable or replace the sensor."));
                st.Outcomes.Add(Fix("It stays at the highest value.", "The signal is above 20 mA: a short circuit in the wiring &ndash; repair it."));
                st.Outcomes.Add(Fix("It changes, but the end values are wrong.", "The scaling does not fit any more (the sensor or the mechanics moved): teach the two ends again and check the window."));
                st.Outcomes.Add(Fix("It jumps about, or counts the wrong way.", "Noise (the cable beside power cables, the shield not grounded) or a reversed direction: route and ground the cable; correct the scaling."));
                st.Outcomes.Add(Go("It reaches the target.", "C2", good: true));
                TimeStep("C2");
            }

            private void ArrivedEncoder()
            {
                var reference = _mustOn.Concat(_mustOff).FirstOrDefault();
                var c1 = Add("C1", "C", "Is the unit referenced?", "1.7 (encoder) (3)");
                c1.Role = "reference";
                c1.Do.Add("After switching on, an encoder's position is valid only after the reference (homing) run" +
                          (reference != null ? ", at " + SensorName(reference) + SensorWhere(reference) : "") + ".");
                if (reference != null)
                    c1.Do.Add("At the reference end " + Code(reference.Io) + " must be ON on the <i>Inputs / Outputs</i> page, and its LED lit; away from it OFF.");
                c1.Outcomes.Add(Fix("The reference run does not finish, or the sensor does not switch.",
                    "Adjust or replace the reference sensor (as a magnetic sensor: slide it until its LED lights at the end), then reference the unit again."));
                c1.Outcomes.Add(Go("It is referenced.", "C2", good: true));

                var c2 = Add("C2", "C", "Does the position count with the movement?", "1.7 (encoder) (1), (2)");
                c2.Role = "position";
                if (!string.IsNullOrWhiteSpace(mv.Position)) c2.Do.Add("The unit shows the position at " + Code(mv.Position) + ".");
                if (c.Air != null && c.Air.Measure.Length > 0) c2.Do.Add("Measured by: " + E(c.Air.Measure) + ".");
                c2.Do.Add("Move the cylinder (the button or the override) and watch the number: it must count evenly, up one way and down the other.");
                c2.Outcomes.Add(Fix("It does not change.", "The encoder's cable or connector, the encoder itself, or its coupling slipped: repair, fix or replace it, then reference the unit again."));
                c2.Outcomes.Add(Fix("It counts the wrong way, or drifts.", "The A/B channels are swapped, the coupling slips, or noise (the shield, the cable beside power cables): correct the wiring or the direction; ground the shield; tighten the coupling."));
                c2.Outcomes.Add(Go("It counts right.", "C3", good: true));

                var c3 = Add("C3", "C", "Does it stop inside its target window?", "1.7 (encoder) (4)");
                c3.Role = "window";
                c3.Do.Add("Compare the position where it stops with the target of " + MoveB + " and the window the PLC allows.");
                c3.Outcomes.Add(Fix("Close to the target, just outside the window.", "Teach the target again, or widen the window to a sensible value."));
                c3.Outcomes.Add(Fix("Far from the target.", "It did not really get there: go through the air and the path (steps B2 and B3)."));
                c3.Outcomes.Add(Go("Inside the window.", "C4", good: true));
                TimeStep("C4");
            }

            private void TimeStep(string key)
            {
                var st = Add(key, "C", "Did it arrive in time?", "1.8");
                st.Role = "time";
                st.Do.Add("The alarm comes when the end is not confirmed within " + TimeAllowed +
                          (string.IsNullOrWhiteSpace(mv.Error) ? "" : " (alarm " + Code(mv.Error) + (c.ErrorText.Length > 0 ? " " + E(c.ErrorText) : "") + ")") + ".");
                st.Do.Add("Time the stroke from " + OutCode + " switching to the end being confirmed (the PLC Monitor's <i>Modified</i> column shows when each device changed).");
                st.Outcomes.Add(Fix("It takes about as long as allowed, or longer.", "Set the flow controls back to the right speed; if the movement is right and only slow by design, " +
                    (_failure > 0 ? "give this alarm a longer delay in Alarm Settings." : "have the monitoring time raised (after checking it is safe).")));
                st.Outcomes.Add(Go("It is well within the time.", "end"));
            }

            // ---- helpers -------------------------------------------------------------

            /// <summary>Whose time it is: the HMI's own (its alarm, Alarm Settings) or the PLC's.</summary>
            private string TimeAllowed
            {
                get { return _failure > 0 ? "the time the HMI allows (Alarm Settings: the alarm's delay)" : "the time the PLC allows"; }
            }

            private static bool Relay(WiringTable.Point o) { return o != null && o.Relay.Length > 0; }

            /// <summary>The first letter up (outside a tag): "pressure switch ..." -> "Pressure switch ...".</summary>
            private static string Cap(string html)
            {
                if (string.IsNullOrEmpty(html)) return html;
                int i = 0;
                while (i < html.Length && html[i] == '<') { int close = html.IndexOf('>', i); if (close < 0) break; i = close + 1; }
                return i < html.Length && char.IsLower(html[i]) ? html.Substring(0, i) + char.ToUpperInvariant(html[i]) + html.Substring(i + 1) : html;
            }

            private static string StripB(string html) { return html.Replace("<b>", "").Replace("</b>", ""); }

            private static string ConditionText(MovementInfo.Condition k)
            {
                var sb = new StringBuilder("<b>" + E(k.Name) + "</b>");
                if (!string.IsNullOrEmpty(k.Rule)) sb.Append(" (" + E(k.Rule) + ")");
                if (k.Met == true) sb.Append(" <span class=\"st-on\">met</span>");
                else if (k.Met == false) sb.Append(" <span class=\"st-bad\">NOT met</span>");
                return sb.ToString();
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

            private List<Sensor> StartSensors { get { return _mustOff.Where(x => !x.IsPressureSwitch).ToList(); } }

            private void Focus()
            {
                if (_failure <= 0) return;
                var order = _steps.ToList();
                Func<string, IEnumerable<Step>> role = r => order.Where(x => x.Role == r);
                var fromA3 = order.Where(x => x.Part == "A" && x.Key != "A1" && x.Key != "A2").ToList();
                var bPart = order.Where(x => x.Part == "B").ToList();
                var startSteps = role("start").Concat(role("reference")).ToList();
                var keep = new List<Step>();
                var look = order.FirstOrDefault(x => x.Key == "look");
                Func<IEnumerable<Step>, string> first = list => list.Select(x => x.Key).FirstOrDefault() ?? "end";
                string startNames = _mustOff.Count > 0 ? Sensors(_mustOff) : "the start sensor";

                switch (_failure)
                {
                    case 1:     // the start sensor stayed ON
                        keep.Add(look); keep.AddRange(fromA3); keep.AddRange(startSteps);
                        Relook(look, "Has the cylinder left its start?",
                               startNames + " at the start stayed <b>ON</b>: either the cylinder did not leave, or it left and the sensor did not go OFF.",
                               Go("No &ndash; it is still at the start.", first(fromA3)),
                               Go("Yes &ndash; it moved.", first(startSteps), good: true));
                        break;
                    case 2:     // the target did not come ON
                    {
                        bool unseenStart = StartSensors.Count == 0;
                        keep.Add(look);
                        if (unseenStart) keep.AddRange(fromA3);
                        keep.AddRange(bPart); keep.AddRange(role("target")); keep.AddRange(role("time"));
                        var outs = new List<Outcome>();
                        if (unseenStart) outs.Add(Go("It has <b>not moved</b> at all.", first(fromA3)));
                        outs.Add(Go("It moved but <b>stopped on the way</b>.", first(bPart)));
                        outs.Add(Go("It <b>arrived</b> at the end of " + MoveB + ".", first(role("target")), good: true));
                        Relook(look, "Where is the cylinder now?",
                               (_mustOn.Count > 0 ? Sensors(_mustOn) : "The end") + " did not come <b>ON</b>" +
                               (unseenStart ? "" : " although " + Sensors(StartSensors) + " went OFF (it left its start)") + ".",
                               outs.ToArray());
                        break;
                    }
                    case 3:     // the target came ON, the start stayed ON
                        keep.AddRange(startSteps);
                        break;
                    case 4:     // both coils ON
                        keep.AddRange(order.Where(x => x.Key == "A1" || x.Key == "A2"));
                        break;
                    case 5:     // the position did not change
                        keep.Add(look); keep.AddRange(fromA3); keep.AddRange(role("position"));
                        Relook(look, "Has the cylinder moved?",
                               "The position reading" + (string.IsNullOrWhiteSpace(mv.Position) ? "" : " " + Code(mv.Position)) + " did not change.",
                               Go("It has <b>not moved</b>.", first(fromA3)),
                               Go("It moved, but the number did not change.", first(role("position")), good: true));
                        break;
                    case 6:     // the position was still changing
                        keep.Add(look); keep.AddRange(bPart); keep.AddRange(role("position")); keep.AddRange(role("window")); keep.AddRange(role("time"));
                        Relook(look, "Where is the cylinder now?",
                               "The position reading" + (string.IsNullOrWhiteSpace(mv.Position) ? "" : " " + Code(mv.Position)) + " was still changing when the time ran out.",
                               Go("It is still creeping, or it stopped on the way.", first(bPart)),
                               Go("It <b>arrived</b>, the number does not settle.", first(role("position")), good: true));
                        break;
                    default:
                        return;
                }
                keep = keep.Where(x => x != null).Distinct().ToList();
                if (keep.Count == 0) return;
                var end = order.FirstOrDefault(x => x.Key == "end");
                if (end != null) keep.Add(end);
                // an answer leading to a step left out goes on to the next one kept
                Func<string, string> resolve = key =>
                {
                    var target = string.IsNullOrEmpty(key) ? "end" : key;
                    int i = order.FindIndex(x => x.Key == target);
                    if (i < 0) return target;
                    for (int j = i; j < order.Count; j++) if (keep.Contains(order[j])) return order[j].Key;
                    return "end";
                };
                foreach (var st in keep)
                    foreach (var o in st.Outcomes)
                        if (o.Next.Length > 0 || o.Fix.Length == 0) o.Next = resolve(o.Next);
                _steps.Clear();
                _steps.AddRange(order.Where(keep.Contains));
                for (int i = 0; i < _steps.Count; i++) _steps[i].Number = i + 1;
            }

            private void Relook(Step look, string title, string what, params Outcome[] outcomes)
            {
                if (look == null) return;
                look.Title = title;
                look.Do.Insert(0, what);
                // the sensors' meaning for the whole movement is not the question any more
                look.Do.RemoveAll(d => d.StartsWith("Cannot see it?"));
                look.Outcomes.Clear();
                look.Outcomes.AddRange(outcomes.Where(o => o.Next != "end" || o.Fix.Length > 0));
            }

            /// <summary>Where to start for a failure the HMI saw: the first step, and the failure in words.</summary>
            private string FocusStart(out string why)
            {
                var od = OutDev;
                var start = StartSensors;
                switch (_failure)
                {
                    case 1: why = "The HMI timed " + MoveB + ": " + (_mustOff.Count > 0 ? Sensors(_mustOff) : "the start sensor") + " at the start stayed <b>ON</b>."; break;
                    case 2: why = "The HMI timed " + MoveB + ": " + (_mustOn.Count > 0 ? Sensors(_mustOn) : "the end") + " did not come <b>ON</b>" +
                                  (start.Count > 0 ? " although " + Sensors(start) + " went OFF" : "") + "."; break;
                    case 3: why = "The HMI timed " + MoveB + ": " + Sensors(_mustOn) + " came ON, but " + (_mustOff.Count > 0 ? Sensors(_mustOff) : "the start sensor") +
                                  " at the start stayed <b>ON</b> &ndash; that sensor did not go OFF."; break;
                    case 4: why = "Both coils' outputs were <b>ON</b>" + (od != null && c.OtherDevice != null ? " (" + OutCode + " and " + Code(c.OtherDevice.Address) + ")" : "") +
                                  ": the valve cannot switch."; break;
                    case 5: why = "The HMI timed " + MoveB + ": the position reading did not change."; break;
                    case 6: why = "The HMI timed " + MoveB + ": the position reading was still changing when the time ran out."; break;
                    default: why = ""; break;
                }
                return _steps.Count > 0 ? _steps[0].Key : "";
            }

            // ---- where to start -----------------------------------------------------

            /// <summary>From the states just before the alarm: the step to start at, and why ("" when not known).</summary>
            private string StartAt(out string why)
            {
                why = "";
                var od = OutDev;
                int? outAtAlarm = od != null ? od.AtAlarm : null;
                var start = _mustOff.Where(s => !s.IsPressureSwitch && s.Device.AtAlarm.HasValue).ToList();
                bool stillAtStart = start.Count > 0 && start.Any(s => s.Device.AtAlarm == 1);
                bool leftStart = start.Count > 0 && start.All(s => s.Device.AtAlarm == 0);
                bool arrived = _mustOn.Count > 0 && _mustOn.All(s => s.Device.AtAlarm == 1);
                var stuck = _mustOff.Where(s => s.Device.AtAlarm == 1).ToList();

                if (_spring)
                {
                    if (outAtAlarm == 1)
                    {
                        why = OutCode + " was still <b>ON</b> when the alarm came: the PLC kept the coil on, so the spring could not switch the valve back.";
                        return "A1";
                    }
                }
                else
                {
                    if (outAtAlarm == 0)
                    {
                        why = OutCode + " was <b>OFF</b> when the alarm came: the PLC did not drive the valve.";
                        return "A1";
                    }
                    if (_double && outAtAlarm == 1 && c.OtherDevice != null && c.OtherDevice.AtAlarm == 1)
                    {
                        why = "Both coils' outputs were <b>ON</b> (" + OutCode + " and " + Code(c.OtherDevice.Address) + "): the valve cannot switch.";
                        return "A1";
                    }
                }
                if (arrived && stuck.Count == 0)
                {
                    why = Sensors(_mustOn) + " " + (_mustOn.Count > 1 ? "were" : "was") + " <b>ON</b>: it arrived, but too late or not confirmed in time.";
                    return Key("Did it arrive in time?");
                }
                if (arrived && stuck.Count > 0)
                {
                    why = Sensors(_mustOn) + " was ON, but " + Sensors(stuck) + " was <b>still ON</b> as well.";
                    return Key(stuck.Any(s => s.IsPressureSwitch) || _mustOff.Count > 0 ? "C" + (_mustOn.Count + 1) : "C1");
                }
                if (stillAtStart && (outAtAlarm == 1 || (_spring && outAtAlarm == 0)))
                {
                    why = OutCode + " was " + (_spring ? "OFF" : "ON") + ", but " + Sensors(start.Where(s => s.Device.AtAlarm == 1)) +
                          " at the start was <b>still ON</b>: the cylinder did not leave its start. The PLC did its part &ndash; follow the signal from the card.";
                    return "A3";
                }
                if (stillAtStart)
                {
                    why = Sensors(start.Where(s => s.Device.AtAlarm == 1)) + " at the start was <b>still ON</b>: the cylinder did not leave its start.";
                    return "A1";
                }
                if (leftStart && _mustOn.Count > 0)
                {
                    why = "It left its start (" + Sensors(start) + " OFF), but " + Sensors(_mustOn) +
                          " did not come on: it stopped on the way, or the sensor does not see it &ndash; step 1 tells which.";
                    return "look";
                }
                if (leftStart && _endNotSensed)
                {
                    why = "It left " + Sensors(start) + ": nothing sees the far end, so the PLC waited for the time &ndash; check it arrived (step 1) and the time.";
                    return "look";
                }
                return "";
            }

            private string Key(string titleOrKey)
            {
                var s = _steps.FirstOrDefault(x => x.Key == titleOrKey) ?? _steps.FirstOrDefault(x => x.Title == titleOrKey);
                return s != null ? s.Key : "look";
            }

            // ---- the page -----------------------------------------------------------------

            public string Page()
            {
                Build();
                Focus();
                string why;
                var start = _failure > 0 && _steps.Count > 0 ? FocusStart(out why) : StartAt(out why);
                var startStep = _steps.FirstOrDefault(s => s.Key == start);

                var sb = new StringBuilder();
                sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + E(Unit + " - " + Move) + " - step by step</title><style>" + Style + "</style></head><body>");
                sb.Append("<h1>" + E(Unit) + " &mdash; &ldquo;" + E(Move) + "&rdquo; did not complete</h1>");
                var sub = new List<string> { "Step by step, for this movement on this machine" };
                if (!string.IsNullOrWhiteSpace(mv.Error))
                    sub.Add("alarm " + Code(mv.Error + (_failure > 0 ? "." + _failure : "")) + (c.ErrorText.Length > 0 ? " " + E(c.ErrorText) : ""));
                if (_failure > 0) sub.Add("raised by the HMI");
                if (mv.AlarmTime.HasValue) sub.Add(mv.AlarmTime.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.Append("<p class=\"sub\">" + string.Join(" &middot; ", sub) + "</p>");

                sb.Append(WhatShouldHappen());
                sb.Append(WhatWasSeen());
                if (startStep != null && why.Length > 0)
                    sb.Append("<div class=\"hint\"><b>Where to start:</b> " + why + " &rarr; <a href=\"#" + startStep.Key + "\">Step " + startStep.Number + "</a>" +
                              (startStep.Key != "look" ? " (step 1 is still worth a look)." : ".") + "</div>");

                sb.Append("<div class=\"safety\"><b>Safety first.</b> Before you touch the cylinder, the valve or anything that moves: <b>lock out</b> the machine (electrical and air) " +
                          "and exhaust both sides of the cylinder &ndash; it can move suddenly when air returns, and a closed-centre valve or a clamp keeps air trapped. " +
                          "Never bypass a guard or an emergency stop to clear an alarm. Measuring in the cabinet, changing the PLC program or replacing parts is for trained, authorized people.</div>");

                // the map: three searches after step 1 (those that are on the page)
                var branches = new List<string>();
                if (Range("A").Length > 0) branches.Add("<span class=\"branch a\">not moved: " + Range("A") + "</span>");
                if (Range("B").Length > 0) branches.Add("<span class=\"branch b\">stopped on the way: " + Range("B") + "</span>");
                if (Range("C").Length > 0) branches.Add("<span class=\"branch c\">arrived, not confirmed: " + Range("C") + "</span>");
                if (branches.Count > 1 && Link("look").Length > 0)
                    sb.Append("<div class=\"map\"><b>How this page goes:</b> " + Link("look") + " where the cylinder is &rarr; " + string.Join(" ", branches) + "</div>");

                string part = "";
                foreach (var s in _steps)
                {
                    if (s.Part != part)
                    {
                        part = s.Part;
                        var head = part == "A" ? "A &mdash; It has not moved" + (_spring ? " (the spring must switch the valve back)" : ": follow the signal from the PLC to the cylinder")
                                 : part == "B" ? "B &mdash; It stopped on the way"
                                 : part == "C" ? "C &mdash; It arrived, but the end was not confirmed"
                                 : "";
                        if (head.Length > 0) sb.Append("<h2 class=\"part " + part.ToLowerInvariant() + "\">" + head + "</h2>");
                    }
                    sb.Append(Render(s, s == startStep));
                }

                sb.Append(Parts());
                sb.Append("<p class=\"ref\">The general cylinder manual explains every check in more depth; each step here names its detail (&ldquo;manual 1.6&rdquo;).</p>");
                sb.Append("</body></html>");
                return sb.ToString();
            }

            private string Link(string key)
            {
                var s = _steps.FirstOrDefault(x => x.Key == key);
                return s == null ? "" : "<a href=\"#" + s.Key + "\">Step " + s.Number + "</a>";
            }

            private string Range(string part)
            {
                var list = _steps.Where(s => s.Part == part).ToList();
                if (list.Count == 0) return "";
                return list.Count == 1 ? Link(list[0].Key)
                    : "<a href=\"#" + list[0].Key + "\">steps " + list[0].Number + "&ndash;" + list[list.Count - 1].Number + "</a>";
            }

            private string Render(Step s, bool start)
            {
                var sb = new StringBuilder();
                sb.Append("<div class=\"step" + (start ? " start" : "") + (s.Part == "end" ? " end" : "") + "\" id=\"" + s.Key + "\">");
                sb.Append("<div class=\"num\">" + s.Number + "</div><div class=\"body\">");
                sb.Append("<h3>" + s.Title + (start ? " <span class=\"here\">start here</span>" : "") + "</h3>");
                if (s.Do.Count > 0) sb.Append("<ul>" + string.Join("", s.Do.Select(d => "<li>" + Cap(d) + "</li>")) + "</ul>");
                if (s.Outcomes.Count > 0)
                {
                    sb.Append("<table class=\"out\">");
                    foreach (var o in s.Outcomes)
                    {
                        string then;
                        if (o.Fix.Length == 0) then = "&rarr; " + Link(o.Next.Length > 0 ? o.Next : "end");
                        else
                        {
                            var next = o.Next.Length > 0 ? o.Next : "end";
                            then = o.Fix + " <span class=\"again\">Test " + MoveB + " again. Still failing &rarr; " + Link(next) + ".</span>";
                        }
                        sb.Append("<tr class=\"" + (o.Good ? "good" : o.Fix.Length > 0 ? "fault" : "go") + "\"><td class=\"when\">" + Cap(o.When) + "</td><td>" + then + "</td></tr>");
                    }
                    sb.Append("</table>");
                }
                if (s.Manual.Length > 0) sb.Append("<p class=\"man\">manual " + E(s.Manual) + "</p>");
                sb.Append("</div></div>");
                return sb.ToString();
            }

            /// <summary>The movement as a chain: command, output, relay, coil, valve, cylinder, sensors.</summary>
            private string WhatShouldHappen()
            {
                var o = OutPoint;
                var chain = new List<string>();
                if (mv.Command != null) chain.Add(Box("HMI command", Code(mv.Command.Address), "A1"));
                if (_spring)
                {
                    chain.Add(Box("PLC output OFF", OutCode, "A1"));
                    if (Relay(o)) chain.Add(Box("relay drops out", "<b>" + E(o.Relay) + "</b>", "A4"));
                    chain.Add(Box("coil without power", o != null && o.Coil.Length > 0 ? "<b>" + E(o.Coil) + "</b>" : "coil", "A5"));
                    chain.Add(Box("spring switches the valve", c.Air != null && c.Air.Valve.Length > 0 ? "<b>" + E(c.Air.Valve) + "</b>" : "valve", "A6"));
                }
                else
                {
                    chain.Add(Box("PLC output ON", OutCode, "A1"));
                    if (Relay(o)) chain.Add(Box("relay", "<b>" + E(o.Relay) + "</b>", "A4"));
                    chain.Add(Box("valve coil", o != null && o.Coil.Length > 0 ? "<b>" + E(o.Coil) + "</b>" : "coil", "A5"));
                    chain.Add(Box("valve switches", c.Air != null && c.Air.Valve.Length > 0 ? "<b>" + E(c.Air.Valve) + "</b>" : "valve", "A6"));
                }
                var hose = Hose(true);
                chain.Add(Box("air" + (hose.Length > 0 ? " through " + StripB(hose) : ""), "", "A7"));
                chain.Add(Box("cylinder moves", CylinderShort.Length > 0 ? "<b>" + E(CylinderShort) + "</b>" : "", "A8"));
                if (_position) chain.Add(Box("confirmed", "position" + (string.IsNullOrWhiteSpace(mv.Position) ? "" : " " + Code(mv.Position)) +
                                                                  (_mustOn.Count > 0 ? "<br>" + Sensors(_mustOn) + " ON" : ""), "C1"));
                else if (_mustOn.Count > 0) chain.Add(Box("confirmed", string.Join("<br>", _mustOn.Select(s => SensorName(s) + " ON")), "C1"));
                else if (_endNotSensed && _mustOff.Count > 0) chain.Add(Box("confirmed", Sensors(_mustOff) + " OFF + time", "C1"));

                var sb = new StringBuilder("<div class=\"what\"><p><b>What should happen:</b> " + Sentence() + "</p>");
                sb.Append("<div class=\"chain\">" + string.Join("<span class=\"arrow\">&rarr;</span>", chain) + "</div>");
                sb.Append(ValveSentence());
                sb.Append("</div>");
                return sb.ToString();
            }

            private string Box(string caption, string value, string key)
            {
                var s = _steps.FirstOrDefault(x => x.Key == key);
                var inner = "<span class=\"cap\">" + caption + "</span>" + (value.Length > 0 ? "<span class=\"val\">" + value + "</span>" : "");
                return s == null ? "<span class=\"box\">" + inner + "</span>" : "<a class=\"box\" href=\"#" + key + "\" title=\"Step " + s.Number + "\">" + inner + "</a>";
            }

            private string Sentence()
            {
                var o = OutPoint;
                var sb = new StringBuilder();
                sb.Append(HasButton ? "pressing " + PressIt + ", " : "when " + E(OtherMove) + " ends (" + OtherButton + " off), ");
                if (_spring)
                    sb.Append("the PLC switches " + OutCode + " <b>OFF</b>" + (Relay(o) ? ", relay " + E(o.Relay) + " drops out" : "") +
                              (o != null && o.Coil.Length > 0 ? ", coil " + E(o.Coil) + " loses its power" : "") + " and the spring switches " + ValveName + " back");
                else
                    sb.Append("the PLC switches " + OutCode + " <b>ON</b>" + (Relay(o) ? ", relay " + E(o.Relay) + " pulls in" : "") +
                              (o != null && o.Coil.Length > 0 ? ", coil " + E(o.Coil) + " gets 24 V" : "") + " and " + ValveName + " switches");
                var hose = Hose(true);
                sb.Append("; air goes" + (hose.Length > 0 ? " through " + hose : "") + " into " + Cyl + PnSheet + ", which moves to the end of " + MoveB);
                if (_mustOn.Count > 0) sb.Append("; there " + Sensors(_mustOn) + " must come <b>ON</b>");
                if (_mustOff.Count > 0) sb.Append((_mustOn.Count > 0 ? " and " : "; ") + Sensors(_mustOff) + " must go <b>OFF</b>");
                if (_endNotSensed) sb.Append(" &ndash; nothing sees that end: the sensor going OFF and the time confirm it");
                if (_position) sb.Append("; the position reading" + (string.IsNullOrWhiteSpace(mv.Position) ? "" : " (" + Code(mv.Position) + ")") + " must reach its target");
                sb.Append(".");
                return sb.ToString();
            }

            private string ValveSentence()
            {
                switch (c.Valve)
                {
                    case ValveType.DoubleSolenoid:
                        return "<p class=\"ref\">" + Cap(ValveName) + " is a 5/2 double solenoid valve: a short signal switches it and it stays; its two coils must never be ON together.</p>";
                    case ValveType.SingleSolenoidSpring:
                        return "<p class=\"ref\">" + Cap(ValveName) + " is a 5/2 spring return valve: its coil makes " + (_spring ? E(OtherMove) : E(Move)) +
                               " and must stay ON the whole time; the spring makes " + (_spring ? E(Move) : E(OtherMove)) + " when the coil goes OFF.</p>";
                    case ValveType.ClosedCentre:
                    case ValveType.OpenCentre:
                        return "<p class=\"ref\">" + Cap(ValveName) + " is a 5/3 valve: the cylinder moves only while a coil is ON; with both off it stops in the centre position" +
                               (ClosedCentre == true ? ", holding the air in the cylinder (closed centre)" : ClosedCentre == false ? ", both sides open to exhaust (open centre)" : "") + ".</p>";
                    default:
                        return "";
                }
            }

            /// <summary>The devices with what they should be, at the alarm and now.</summary>
            private string WhatWasSeen()
            {
                var rows = new List<string>();
                Action<string, MovementInfo.Device, bool> row = (what, d, wantOn) =>
                {
                    if (d == null || string.IsNullOrWhiteSpace(d.Address)) return;
                    rows.Add("<tr><td>" + what + "</td><td>" + (wantOn ? "ON" : "OFF") + "</td><td>" + State(d.AtAlarm, wantOn) + "</td><td>" + State(d.Now, wantOn) + "</td></tr>");
                };
                // With the failure known, only the signals it is about (the output that
                // started the time, the sensors that decided it).
                bool all = _failure <= 0;
                bool outputs = all || _failure != 3;
                bool targets = all || _failure == 2 || _failure == 3 || _failure >= 5;
                bool starts = all || _failure != 4;
                if (all) row("Command " + Code(mv.Command != null ? mv.Command.Address : ""), mv.Command, true);
                if (_spring) { if (outputs) row("Coil output " + OutCode + " (" + E(OtherMove) + ")", OutDev, false); }
                else
                {
                    if (outputs) row("Valve output " + OutCode, OutDev, true);
                    if ((all || _failure == 4) && _double && c.OtherDevice != null) row("Other coil " + Code(c.OtherDevice.Address) + " (" + E(OtherMove) + ")", c.OtherDevice, false);
                }
                if (targets) foreach (var s in _mustOn) row(SensorName(s), s.Device, true);
                if (starts) foreach (var s in _mustOff) row(SensorName(s), s.Device, false);
                bool known = rows.Count > 0 && (_mustOn.Concat(_mustOff).Any(s => s.Device.AtAlarm.HasValue || s.Device.Now.HasValue) ||
                                                (OutDev != null && (OutDev.AtAlarm.HasValue || OutDev.Now.HasValue)));
                if (!known) return "";
                var sb = new StringBuilder("<div class=\"seen\"><p><b>What the HMI saw</b> (red: not as it should be at the end of the movement)</p>");
                sb.Append("<table class=\"states\"><tr><th></th><th>should be</th><th>at the alarm</th><th>now</th></tr>" + string.Join("", rows) + "</table>");
                if (_failure > 0)
                    sb.Append("<p class=\"ref\">&ldquo;At the alarm&rdquo;: when the HMI's time for the movement ran out" +
                              (mv.AlarmTime.HasValue ? " (" + mv.AlarmTime.Value.ToString("HH:mm:ss") + ")" : "") + ".</p></div>");
                else
                    sb.Append("<p class=\"ref\">&ldquo;At the alarm&rdquo;: the last reading before the error bit came on" +
                              (mv.AlarmTime.HasValue ? " (" + mv.AlarmTime.Value.ToString("HH:mm:ss") + ")" : "") +
                              (mv.SeenRising ? "" : " &ndash; the alarm was already on when the page started reading, so these are from just after it") + ".</p></div>");
                return sb.ToString();
            }

            /// <summary>The parts list: every device of the movement with where it is on the drawings.</summary>
            private string Parts()
            {
                var rows = new List<string>();
                Action<string, string> row = (a, b) => { if (!string.IsNullOrEmpty(b)) rows.Add("<tr><td>" + a + "</td><td>" + b + "</td></tr>"); };
                if (HasButton) row("HMI command", Code(mv.Command.Address) + " &ndash; " + PressIt);
                Action<string, WiringTable.Point, MovementInfo.Device> output = (caption, p, d) =>
                {
                    if (p != null) row(caption, E(p.Electrical()) + (p.Card.Length > 0 ? " &ndash; " + E(p.Card) : "") + (p.Text.Length > 0 ? " &ndash; " + E(p.Text) : ""));
                    else if (d != null) row(caption, Code(d.Address));
                };
                output(_spring ? "Coil output (" + E(OtherMove) + ")" : "Valve output", OutPoint, OutDev);
                if (!_spring && _double) output("Other coil (" + E(OtherMove) + ")", c.OtherOut, c.OtherDevice);
                if (c.Air != null && c.Air.Pneumatic().Length > 0) row("Valve, hoses, cylinder", E(c.Air.Pneumatic()));
                foreach (var s in _mustOn.Concat(_mustOff))
                    row(s.IsPressureSwitch ? "Pressure switch" : "Sensor", SensorName(s) + SensorWhere(s) + (s.Point != null && s.Point.Text.Length > 0 ? " &ndash; " + E(s.Point.Text) : ""));
                if (c.Air != null && c.Air.Measure.Length > 0) row("Position measured by", E(c.Air.Measure) + (string.IsNullOrWhiteSpace(mv.Position) ? "" : " &ndash; shown at " + Code(mv.Position)));
                else if (!string.IsNullOrWhiteSpace(mv.Position)) row("Position", "shown at " + Code(mv.Position));
                if (c.Air != null && c.Air.Pressure.Length > 0) row("Pressure", E(c.Air.Pressure));
                if (c.MainAir != null) row("Main air", E(c.MainAir.Electrical()) + (c.MainAir.Text.Length > 0 ? " &ndash; " + E(c.MainAir.Text) : ""));
                if (!string.IsNullOrWhiteSpace(mv.Error)) row("Alarm", Code(mv.Error) + (c.ErrorText.Length > 0 ? " " + E(c.ErrorText) : ""));
                if (rows.Count == 0) return "";
                var sb = new StringBuilder("<h2>The parts of this movement</h2><table class=\"parts\">" + string.Join("", rows) + "</table>");
                bool drawn = OutPoint != null || _mustOn.Concat(_mustOff).Any(s => s.Point != null);
                if (drawn)
                    sb.Append("<p class=\"ref\">Sheet 12.6 is page 12, column 6 of the electrical drawing; pneumatic sheet 2 E3 is page 2, zone E3 of the pneumatic drawing." +
                              (_supply.ZeroVoltWire.Length > 0 ? " 0 V is wire " + E(_supply.ZeroVoltWire) + "." : "") +
                              (_supply.SensorSupplyWire.Length > 0 ? " The sensors' +24 V is wire " + E(_supply.SensorSupplyWire) + "." : "") + "</p>");
                return sb.ToString();
            }

            private const string Style =
                "body{font-family:'Segoe UI',Arial,sans-serif;font-size:15px;color:#1e2329;margin:18px 22px;max-width:1020px;line-height:1.4}" +
                "h1{font-size:1.45em;margin:.1em 0}.sub{color:#5a6270;margin:.1em 0 .9em}" +
                "h2{font-size:1.15em;margin:1.3em 0 .5em;padding:.25em .5em;border-radius:4px;background:#eef1f5}" +
                "h2.a{background:#fde9e7}h2.b{background:#fff3d6}h2.c{background:#e5f3e8}" +
                "code{font-family:Consolas,monospace;font-weight:bold;color:#1f4e79;font-size:1.02em}" +
                ".what,.seen{border:1px solid #d0d7e2;border-radius:6px;padding:.5em .8em;margin:.6em 0;background:#fafbfd}" +
                ".what p{margin:.2em 0 .5em}.chain{display:flex;flex-wrap:wrap;align-items:center;gap:4px;margin:.3em 0}" +
                ".box{display:inline-flex;flex-direction:column;border:1px solid #9fb3cc;border-radius:5px;padding:.2em .5em;background:#fff;text-decoration:none;color:inherit;min-width:70px}" +
                "a.box:hover{border-color:#2e75b6;background:#eef5fb}.box .cap{font-size:.78em;color:#5a6270}.box .val{font-size:.95em}.arrow{color:#8a94a3;font-weight:bold}" +
                "table.states{border-collapse:collapse;margin:.2em 0}table.states td,table.states th{border:1px solid #d0d7e2;padding:.2em .6em;text-align:left}" +
                "table.states th{background:#eef1f5;font-weight:600}" +
                ".st-on{color:#1a7f37;font-weight:bold}.st-off{color:#444;font-weight:bold}.st-bad{color:#c00000;font-weight:bold}" +
                ".hint{background:#fff7d6;border:1px solid #e0c060;border-radius:6px;padding:.5em .8em;margin:.6em 0}" +
                ".safety{background:#fdecea;border:1px solid #e3a6a0;border-radius:6px;padding:.5em .8em;margin:.6em 0}" +
                ".map{margin:.8em 0}.branch{display:inline-block;margin:.15em .2em;padding:.1em .5em;border-radius:10px;font-size:.93em}" +
                ".branch.a{background:#fde9e7}.branch.b{background:#fff3d6}.branch.c{background:#e5f3e8}" +
                ".step{display:flex;gap:.8em;border:1px solid #d0d7e2;border-radius:8px;padding:.55em .8em;margin:.55em 0;background:#fff}" +
                ".step.start{border:2px solid #d4a017;box-shadow:0 0 0 3px #fff3c4}.step.end{background:#f6f7f9}" +
                ".num{flex:0 0 auto;width:2em;height:2em;border-radius:50%;background:#1f4e79;color:#fff;font-weight:bold;display:flex;align-items:center;justify-content:center;margin-top:.1em}" +
                ".body{flex:1}.body h3{margin:.15em 0 .35em;font-size:1.05em}.body ul{margin:.2em 0 .4em 1.1em;padding:0}.body li{margin:.15em 0}" +
                ".here{font-size:.72em;background:#ffd966;border-radius:3px;padding:.05em .4em;vertical-align:middle}" +
                "table.out{border-collapse:collapse;width:100%;margin:.3em 0 .1em}table.out td{border-top:1px solid #e3e7ee;padding:.3em .5em;vertical-align:top}" +
                "table.out td.when{width:36%}tr.good td.when{border-left:4px solid #1a7f37}tr.fault td.when{border-left:4px solid #c0504d}tr.go td.when{border-left:4px solid #8a94a3}" +
                ".again{display:block;color:#5a6270;font-size:.9em;margin-top:.15em}" +
                ".man{margin:.2em 0 0;color:#8a94a3;font-size:.82em;text-align:right}" +
                "table.parts{border-collapse:collapse}table.parts td{border-top:1px solid #e3e7ee;padding:.25em .7em .25em 0;vertical-align:top}table.parts td:first-child{font-weight:600;white-space:nowrap}" +
                ".ref{color:#5a6270;font-size:.9em}a{color:#1f5fa8}" +
                "@media print{.step{break-inside:avoid}}";
        }
    }
}
