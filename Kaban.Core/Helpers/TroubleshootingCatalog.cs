using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// Which document answers an alarm, and which part of it.
    ///
    /// THE PROBLEM
    /// -----------
    /// The cylinder troubleshooting manual is one document that covers every
    /// cylinder on every machine, and it asks the reader questions the machine
    /// already knows the answer to: which valve is fitted, which feedback sensor.
    /// An operator standing at a stopped machine should not be choosing between
    /// "double solenoid" and "single solenoid with spring return" - the builder
    /// knew that when the machine was assembled.
    ///
    /// THE CODE
    /// --------
    /// A three-digit code carries those answers with the alarm:
    ///
    ///     1 . valve . feedback     cylinder movement fault
    ///     2 . axis  . 0            servo fault  (axis number, then the drive's
    ///                              own alarm code is read from the axis page)
    ///     3 . 0 . 0                air pressure
    ///     4 . 0 . 0                safety circuit
    ///     5 . 0 . 0                inverter / motor
    ///     9 . 0 . 0                general - the document with no parameters
    ///
    /// So 1-2-1 is "cylinder, 5/2 double solenoid, single magnetic sensor", and
    /// the viewer opens the manual at the sections that apply to exactly that
    /// combination instead of all of them.
    ///
    /// WHY DIGITS RATHER THAN A DOCUMENT PATH PER ALARM
    /// ------------------------------------------------
    /// A machine has dozens of cylinders and one cylinder manual. Naming the file
    /// on every alarm means dozens of copies of one fact and dozens of places to
    /// edit when the manual is revised. The code says what KIND of fault it is;
    /// the catalogue below says which file currently answers that kind.
    /// </summary>
    public static partial class TroubleshootingCatalog
    {
        public enum ErrorType
        {
            None = 0,
            CylinderMovement = 1,
            ServoFault = 2,
            AirPressure = 3,
            SafetyCircuit = 4,
            InverterMotor = 5,
            General = 9
        }

        /// <summary>Second digit when the error type is CylinderMovement.</summary>
        public enum ValveType
        {
            Unspecified = 0,
            DoubleSolenoid = 1,          // 5/2, two coils, stays where it was left
            SingleSolenoidSpring = 2,    // 5/2, one coil, springs back
            ClosedCentre = 3,            // 5/3 double solenoid, centre position
            OpenCentre = 4               // LEGACY: read as 3 - the manual has one 5/3 variant
        }

        /// <summary>Third digit when the error type is CylinderMovement.</summary>
        public enum FeedbackType
        {
            Unspecified = 0,
            SingleMagnetic = 1,          // one reed/magnetic sensor
            TwoMagnetic = 2,             // both ends sensed
            SingleInductive = 3,
            AnalogueMagnetic = 4,        // 4-20 mA position
            PressureSwitch = 5,          // magnetic sensor + pressure switch (clamping)
            EncoderReference = 6,        // incremental encoder + reference sensor
            None = 9                     // LEGACY: no variant in the manual - shows all
        }

        // ------------------------------------------------------------------
        /// <summary>
        /// What an error type's two parameters MEAN, so one pair of columns can
        /// serve every type.
        ///
        /// A cylinder fault wants a valve and a sensor; a servo fault wants an axis
        /// number and nothing else; air pressure wants neither. Giving each type
        /// its own named columns would mean a grid that grows a pair every time a
        /// type is added, most of them blank on most rows. Two GENERIC columns that
        /// relabel themselves cover all of it - and because they are ordinary
        /// cells, a row can be copied and pasted onto another like any other.
        /// </summary>
        public class ParameterSpec
        {
            public string Caption = "";
            /// <summary>Choices, in value order. Empty means free numeric entry.</summary>
            public string[] Options = new string[0];

            /// <summary>
            /// The digit each option stores, position for position.
            ///
            /// EXPLICIT, not the option's index. The digits are already saved in
            /// machines' Alarms.json files, and an index-based mapping renumbers
            /// every later option the moment one is added or dropped - quietly
            /// changing what an existing alarm's code means.
            /// </summary>
            public int[] Values = new int[0];
            public bool Used { get { return !string.IsNullOrEmpty(Caption); } }
        }

        public static ParameterSpec Parameter1(ErrorType type)
        {
            switch (type)
            {
                case ErrorType.CylinderMovement:
                    // The three valve variants of detail 1.4, in the manual's order.
                    return Spec("Valve", new[] { ValveType.Unspecified, ValveType.DoubleSolenoid,
                                                 ValveType.SingleSolenoidSpring, ValveType.ClosedCentre },
                                v => (int)v, v => Describe(v));
                case ErrorType.ServoFault:
                    return new ParameterSpec { Caption = "Axis" };   // free number
                case ErrorType.InverterMotor:
                    return new ParameterSpec { Caption = "Drive" };
                default:
                    return new ParameterSpec();
            }
        }

        public static ParameterSpec Parameter2(ErrorType type)
        {
            switch (type)
            {
                case ErrorType.CylinderMovement:
                    // The six feedback variants of detail 1.7, in the manual's order.
                    return Spec("Feedback", new[] { FeedbackType.Unspecified, FeedbackType.TwoMagnetic,
                                                    FeedbackType.SingleMagnetic, FeedbackType.SingleInductive,
                                                    FeedbackType.PressureSwitch, FeedbackType.AnalogueMagnetic,
                                                    FeedbackType.EncoderReference },
                                v => (int)v, v => Describe(v));
                default:
                    return new ParameterSpec();
            }
        }

        private static ParameterSpec Spec<T>(string caption, T[] items, Func<T, int> digit, Func<T, string> text)
        {
            return new ParameterSpec
            {
                Caption = caption,
                Options = items.Select(text).ToArray(),
                Values = items.Select(digit).ToArray()
            };
        }

        /// <summary>
        /// A stored digit as the manual knows it. Old codes stay valid: "5/3 open
        /// centre" (4) is the manual's one 5/3 variant (3), and "no feedback" (9)
        /// has no variant at all, so it filters nothing.
        /// </summary>
        public static int CanonicalValve(int v) { return v == (int)ValveType.OpenCentre ? (int)ValveType.ClosedCentre : v; }
        public static int CanonicalFeedback(int f) { return f == (int)FeedbackType.None ? 0 : f; }

        /// <summary>Which option in the list stands for a stored digit (0 if none).</summary>
        public static int IndexOfValue(ParameterSpec spec, int value)
        {
            if (spec == null || spec.Values.Length == 0) return Math.Max(0, value);
            int i = Array.IndexOf(spec.Values, value);
            return i < 0 ? 0 : i;
        }

        /// <summary>The stored digit for a chosen option, or a typed number.</summary>
        public static int ValueOf(ParameterSpec spec, string chosen)
        {
            if (spec == null || string.IsNullOrWhiteSpace(chosen)) return 0;
            if (spec.Options.Length == 0)
            {
                int n;
                return int.TryParse(chosen.Trim(), out n) ? n : 0;
            }
            int idx = Array.IndexOf(spec.Options, chosen);
            if (idx < 0) return 0;
            return spec.Values.Length == spec.Options.Length ? spec.Values[idx] : idx;
        }

        /// <summary>The option text for a stored digit.</summary>
        public static string TextOf(ParameterSpec spec, int value)
        {
            if (spec == null || !spec.Used) return "";
            if (spec.Options.Length == 0) return value == 0 ? "" : value.ToString();
            if (spec.Values.Length == spec.Options.Length)
            {
                int i = Array.IndexOf(spec.Values, value);
                return i < 0 ? "" : spec.Options[i];
            }
            return (value >= 0 && value < spec.Options.Length) ? spec.Options[value] : "";
        }

        /// <summary>Builds a code from its parts. 0 for a part that does not apply.</summary>
        public static string Compose(ErrorType type, int param1 = 0, int param2 = 0)
        {
            return string.Format("{0}.{1}.{2}", (int)type, param1, param2);
        }

        public static string Compose(ValveType valve, FeedbackType feedback)
        {
            return Compose(ErrorType.CylinderMovement, (int)valve, (int)feedback);
        }

        /// <summary>
        /// Reads a code. Accepts "1.2.1", "121" and "1-2-1", because all three get
        /// typed and none of them is wrong enough to reject.
        /// </summary>
        public static bool TryParse(string code, out ErrorType type, out int p1, out int p2)
        {
            type = ErrorType.None; p1 = 0; p2 = 0;
            if (string.IsNullOrWhiteSpace(code)) return false;

            var digits = new string(code.Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return false;

            type = (ErrorType)(digits[0] - '0');
            if (digits.Length > 1) p1 = digits[1] - '0';
            if (digits.Length > 2) p2 = digits[2] - '0';
            return Enum.IsDefined(typeof(ErrorType), type);
        }

        // ------------------------------------------------------------------
        /// <summary>
        /// The movement of a cylinder unit that failed - one side of it: the
        /// button that commands it, the output that drives the valve, the sensors
        /// that should confirm it and the ones that should go off.
        ///
        /// The manual speaks of "the output" and "the target feedback"; the unit
        /// knows which devices those are for THIS movement, so the page can name
        /// them instead of leaving the operator to find them in the drawings.
        /// </summary>
        public class MovementInfo
        {
            public sealed class Device
            {
                public string Address = "";
                /// <summary>What the page calls it (a lamp's tooltip), or "".</summary>
                public string Name = "";
                /// <summary>ON (1) or OFF (0) just before the error bit came on; null when not known.</summary>
                public int? AtAlarm;
                /// <summary>ON (1) or OFF (0) at the last scan; null when not known.</summary>
                public int? Now;
            }

            /// <summary>The error bit that reported the movement, or "".</summary>
            public string Error = "";
            /// <summary>The unit's position reading (an encoder's or an analogue sensor's), or "".</summary>
            public string Position = "";
            /// <summary>When the error bit came on (the states in AtAlarm are the scan before), or null.</summary>
            public DateTime? AlarmTime;
            /// <summary>
            /// False when the error bit was already on at the page's first scan:
            /// the states are then from after the fault, not from the moment it happened.
            /// </summary>
            public bool SeenRising;

            public int Station;
            public string Unit = "";
            /// <summary>"A" (left button) or "B" (right button).</summary>
            public string Side = "";
            /// <summary>The button's caption: "Clamp", "Forward"...</summary>
            public string Name = "";
            /// <summary>The other button's caption: "Release", "Back"... ("" when the unit has one button).</summary>
            public string OtherName = "";

            /// <summary>One condition the movement's button waits for (the button is grey while one is not met).</summary>
            public sealed class Condition
            {
                public string Name = "";
                /// <summary>What it asks of the PLC: "M100 = 0", or "".</summary>
                public string Rule = "";
                /// <summary>Met at the last scan; null when not known.</summary>
                public bool? Met;
            }

            /// <summary>What the button waits for before it may be pressed (its enable conditions).</summary>
            public readonly List<Condition> Conditions = new List<Condition>();
            public Device Command, Output;
            /// <summary>The other side's command and output - the second coil of a double solenoid valve.</summary>
            public Device OtherCommand, OtherOutput;
            /// <summary>Sensors that turn ON when this movement completes.</summary>
            public readonly List<Device> Feedback = new List<Device>();
            /// <summary>Sensors of the other end, which go OFF as the cylinder leaves it.</summary>
            public readonly List<Device> OppositeFeedback = new List<Device>();
        }

        // ------------------------------------------------------------------
        /// <summary>A document to open, and where in it to start.</summary>
        public class HelpDocument
        {
            /// <summary>File name as configured, resolved against the help folder.</summary>
            public string FileName = "";

            /// <summary>Human title for the menu when an alarm has several.</summary>
            public string Title = "";

            /// <summary>Section to jump to, e.g. "1.4". Empty opens page one.</summary>
            public string Section = "";

            /// <summary>
            /// Valve and feedback digits to tailor the document by (0 = all).
            /// Set for a cylinder fault: the one manual is shown with only the
            /// matching variants of details 1.4 and 1.7.
            /// </summary>
            public int Valve;
            public int Feedback;

            /// <summary>
            /// For a code document (see AxisCodeHelp): the code whose sections
            /// to show - "16.3", "1904" - and the servo alarm they fall back to
            /// when the manual does not list that detail ("16").
            /// </summary>
            public string Code = "";
            public string CodeGroup = "";

            /// <summary>
            /// For a cylinder fault raised by a unit: WHICH movement failed and the
            /// devices it involves. Null when no movement is known (the manual
            /// opened without an alarm).
            /// </summary>
            public MovementInfo Movement;

            /// <summary>
            /// The STEP-BY-STEP troubleshooter for the movement (BuildSteps), not the
            /// manual: what to look at, press and measure on this machine, one step
            /// after the other. Needs Movement.
            /// </summary>
            public bool StepByStep;

            /// <summary>
            /// For a movement alarm the HMI raised itself (AlarmGeneration): WHICH
            /// signal failed - 1 the start sensor stayed ON, 2 the target sensor did
            /// not come ON, 3 both came ON, 4 both coils were ON, 5 the position did
            /// not change, 6 it was still changing. The step-by-step page then asks
            /// only about that signal. 0: not known (the PLC's alarm bit).
            /// </summary>
            public int Failure;

            public string FullPath
            {
                get { return IsWebLink ? FileName.Trim() : ResolveLanguageFile(FileName); }
            }

            /// <summary>An alarm's helper link to a web page (http / https): opened in the browser as it is.</summary>
            public bool IsWebLink
            {
                get
                {
                    var f = (FileName ?? "").Trim();
                    return f.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                           f.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                }
            }

            public bool Exists
            {
                get
                {
                    try { return !string.IsNullOrEmpty(FileName) && (IsWebLink || File.Exists(FullPath)); }
                    catch { return false; }
                }
            }
        }

        /// <summary>
        /// Where the documents live. Beside the machine's own files, so a machine
        /// carries its manuals with it rather than depending on a network share
        /// that is unreachable exactly when something has gone wrong.
        /// </summary>
/// <summary>
        /// The file to open for a help document, IN THE HMI'S CURRENT LANGUAGE.
        ///
        /// A document is referred to by ONE name everywhere - in the designer, in
        /// Alarms.json - and kept on disk with a language suffix:
        ///
        ///     Unit_Robot_Top_Clamping.en.html      English
        ///     Unit_Robot_Top_Clamping.tr.html      Turkish
        ///     Unit_Robot_Top_Clamping.html         no language (a drawing, a photo)
        ///
        /// Asked for "Unit_Robot_Top_Clamping.html" it returns the current
        /// language's file, then English, then the plain name. So a translator is
        /// handed one whole file, a machine can ship with only some languages
        /// translated, and nobody has to re-point anything when one arrives: the
        /// operator simply stops getting English.
        /// </summary>
        /// <remarks>
        /// NAMED ResolveLanguageFile, not Resolve: it used to share that name with
        /// Resolve(code, title = ""), so a one-argument Resolve("1.2.1") quietly
        /// bound to THIS method and returned a path string instead of the code's
        /// documents - which is why four of the help tests no longer compiled.
        /// </remarks>
        public static string ResolveLanguageFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "";
            var plain = Path.Combine(HelpFolder, fileName);
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);

            // A name that already names its language is taken exactly as given.
            if (Regex.IsMatch(name, @"\.[A-Za-z]{2}$")) return plain;

            foreach (var lang in new[] { LanguageManager.CurrentLanguage, "EN" })
            {
                if (string.IsNullOrEmpty(lang)) continue;
                foreach (var code in new[] { lang.ToLowerInvariant(), lang.ToUpperInvariant() })
                {
                    var candidate = Path.Combine(HelpFolder, name + "." + code + ext);
                    try { if (File.Exists(candidate)) return candidate; }
                    catch { }
                }
            }
            return plain;
        }

                public static string HelpFolder =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Help");

        /// <summary>
        /// Which file answers which kind of fault. One entry per error type, set
        /// once for the machine - not per alarm.
        /// </summary>
        public static readonly Dictionary<ErrorType, string> DocumentForType =
            new Dictionary<ErrorType, string>
            {
                { ErrorType.CylinderMovement, "Cylinder_Troubleshooting.html" },
                { ErrorType.ServoFault,       "MR_J4_TroubleShooting.pdf" },
                { ErrorType.AirPressure,      "Cylinder_Troubleshooting.html" },
                { ErrorType.SafetyCircuit,    "" },
                { ErrorType.InverterMotor,    "" },
                { ErrorType.General,          "" }
            };

        /// <summary>
        /// The sections of the cylinder manual that always apply, plus the two
        /// that depend on the fitted parts.
        ///
        /// 1.4 is the valve section and 1.7 the feedback section - those are the
        /// ones the manual asks the reader to choose between, and the ones the
        /// code answers.
        /// </summary>
        public static IEnumerable<HelpDocument> Resolve(string code, string title = "")
        {
            ErrorType type; int p1, p2;
            if (!TryParse(code, out type, out p1, out p2)) yield break;

            string file;
            if (!DocumentForType.TryGetValue(type, out file) || string.IsNullOrEmpty(file))
                yield break;

            switch (type)
            {
                case ErrorType.CylinderMovement:
                    // ONE document. The valve and feedback digits do not choose
                    // between documents - they tell the program which variant rows
                    // of the one manual apply, so it can leave the rest out. The
                    // list exists for further files attached to the alarm, not
                    // for these parameters.
                    yield return new HelpDocument
                    {
                        FileName = file,
                        Title = LanguageManager.GetString("Hlp_Cylinder", "Cylinder troubleshooting"),
                        Valve = CanonicalValve(p1),
                        Feedback = CanonicalFeedback(p2)
                    };
                    break;

                case ErrorType.ServoFault:
                    // The AXIS is in the code; the drive's own alarm number is on
                    // the axis page, which is why that page shows it in the
                    // manual's own hexadecimal form.
                    yield return new HelpDocument
                    {
                        FileName = file,
                        Title = p1 > 0 ? "Servo drive - axis " + p1 : "Servo drive",
                        Section = ""
                    };
                    break;

                default:
                    yield return new HelpDocument
                    {
                        FileName = file,
                        Title = string.IsNullOrEmpty(title) ? type.ToString() : title,
                        Section = ""
                    };
                    break;
            }
        }

/// <summary>
        /// The document for a CONNECTION error code (C01..C04).
        ///
        /// Separate from the alarm codes because a lost controller is not an alarm
        /// - the machine is not reporting a fault, it has stopped reporting at all
        /// - and the person who fixes it is looking at cables and switches rather
        /// than at the machine.
        /// </summary>
        public static HelpDocument ResolveConnection(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            return new HelpDocument
            {
                FileName = "Connection_Troubleshooting.html",
                Title = "Connection " + code,
                Section = code
            };
        }

        /// <summary>
        /// Writes a copy of the document holding ONLY the requested section, and
        /// returns its path. Falls back to the whole document if it cannot.
        ///
        /// WHY NOT "file.html#C02": that is a URL, not a path. Handed to the shell
        /// as a file name, Windows looks for a file literally called
        /// "Connection_Troubleshooting.html#C02", finds nothing and fails - and the
        /// failure was caught and logged, so clicking the row simply did nothing.
        /// Fragments are also dropped by several browsers when a local file is
        /// opened through the shell, so even a correct URL would not land reliably.
        ///
        /// A page containing just that section needs no fragment at all, and it is
        /// what the operator wanted anyway: the answer to THEIR code, not four.
        /// The introduction is kept, because it carries the safety warning.
        /// </summary>
        public static string ExtractSection(HelpDocument doc)
        {
            // An axis code: only that code's entry of the manual (AxisCodeHelp).
            if (doc != null && doc.Exists && !string.IsNullOrEmpty(doc.Code))
                return AxisCodeHelp.Extract(doc);

            // A movement's step-by-step troubleshooter (TroubleshootingCatalog.Steps.cs).
            if (doc != null && doc.StepByStep && doc.Movement != null)
                return WriteSteps(doc);

            if (doc != null && doc.Exists && (doc.Valve != 0 || doc.Feedback != 0 || doc.Movement != null) &&
                doc.FileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                return Tailor(doc);

            if (doc == null || !doc.Exists) return doc == null ? "" : doc.FullPath;
            if (string.IsNullOrEmpty(doc.Section)) return doc.FullPath;
            if (!doc.FileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase) &&
                !doc.FileName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                return doc.FullPath;

            try
            {
                var html = File.ReadAllText(doc.FullPath);

                int start = html.IndexOf("<h2 id=\"" + doc.Section + "\"", StringComparison.OrdinalIgnoreCase);
                if (start < 0) return doc.FullPath;

                int end = html.IndexOf("<h2", start + 3, StringComparison.OrdinalIgnoreCase);
                if (end < 0) end = html.IndexOf("</body>", start, StringComparison.OrdinalIgnoreCase);
                if (end < 0) end = html.Length;

                // Everything before the first section: head, styles, title, the
                // introduction and the safety note.
                int firstSection = html.IndexOf("<h2", StringComparison.OrdinalIgnoreCase);
                if (firstSection < 0 || firstSection > start) firstSection = start;

                var page = html.Substring(0, firstSection) +
                           html.Substring(start, end - start) +
                           "\r\n</body></html>\r\n";

                var safe = new string(doc.Section.Where(char.IsLetterOrDigit).ToArray());
                var name = Path.GetFileNameWithoutExtension(doc.FullPath) + "_" + safe + ".html";
                var path = Path.Combine(Path.GetTempPath(), name);
                File.WriteAllText(path, page, new System.Text.UTF8Encoding(true));
                return path;
            }
            catch
            {
                return doc.FullPath;
            }
        }

        /// <summary>
        /// Writes a copy of the manual holding only this cylinder's valve and
        /// feedback variants, as ONE chart, and returns its path.
        ///
        /// The manual's own instructions: details 1.4 and 1.7 are ALTERNATIVES -
        /// identify the valve and the feedback type, then read only the matching
        /// rows. The unit knows both, so the program does the identifying: the
        /// other variants are left out, and the details are joined into one table
        /// under one heading row - 1.1, 1.2 ... 1.8, read top to bottom - instead
        /// of a table per detail with the heading repeated between them.
        ///
        /// With the MOVEMENT that failed (see MovementInfo) the manual stays the
        /// general view; a line at its top leads to the movement's step-by-step
        /// troubleshooter (TroubleshootingCatalog.Steps.cs), which names the
        /// devices and says what to do one step after the other. (Notes added to
        /// every check of the chart, 30 September, made the manual harder to read,
        /// not easier: they were replaced by that page.)
        ///
        /// A digit the manual has no block for filters NOTHING: hiding every
        /// variant would leave the operator with less than the full manual.
        /// </summary>
        public static string Tailor(HelpDocument doc)
        {
            try
            {
                // One chart: a manual still made of a table per block (an older
                // copy, a translation made from one) is joined first.
                var html = JoinBlocks(File.ReadAllText(doc.FullPath));
                bool byValve = doc.Valve != 0 && html.Contains("data-variant=\"valve:" + doc.Valve + "\"");
                bool byFeedback = doc.Feedback != 0 && html.Contains("data-variant=\"feedback:" + doc.Feedback + "\"");
                if (!byValve && !byFeedback && doc.Movement == null) return doc.FullPath;

                html = Regex.Replace(html,
                    "<tbody class=\"block\"[^>]*data-variant=\"(valve|feedback):(\\d+)\"[^>]*>.*?</tbody>\\s*",
                    m =>
                    {
                        int n = int.Parse(m.Groups[2].Value);
                        if (m.Groups[1].Value == "valve" && byValve && n != doc.Valve) return "";
                        if (m.Groups[1].Value == "feedback" && byFeedback && n != doc.Feedback) return "";
                        return m.Value;
                    },
                    RegexOptions.Singleline);

                if (byValve || byFeedback)
                {
                    // "(2) Single solenoid 5/2": the number told the alternatives
                    // apart; with one left it only reads like a step number.
                    html = Regex.Replace(html, "(<strong class=\"vt\">)\\(\\d\\)\\s*", "$1");
                }

                var chosen = new List<string>();
                if (byValve) chosen.Add("<b>Valve:</b> " + WebUtility.HtmlEncode(Describe((ValveType)doc.Valve)));
                if (byFeedback) chosen.Add("<b>Feedback:</b> " + WebUtility.HtmlEncode(Describe((FeedbackType)doc.Feedback)));
                var note = new System.Text.StringBuilder();
                note.Append("<div class=\"tailored\">");
                if (chosen.Count > 0)
                    note.Append("<p>Prepared for this cylinder - " + string.Join(" &middot; ", chosen) +
                                ". Other types in details " + (byValve && byFeedback ? "1.4 and 1.7" : byValve ? "1.4" : "1.7") +
                                " are left out; they do not apply to it.</p>");
                if (doc.Movement != null)
                {
                    // The movement's own page, written beside this one.
                    var steps = WriteSteps(new HelpDocument
                    {
                        FileName = doc.FileName, StepByStep = true, Valve = doc.Valve, Feedback = doc.Feedback, Movement = doc.Movement
                    });
                    if (!string.IsNullOrEmpty(steps))
                        note.Append("<p class=\"steps\"><b>" + WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(doc.Movement.Unit) ? "This cylinder" : doc.Movement.Unit) +
                                    "</b>: for the movement that failed, work through <a href=\"" + WebUtility.HtmlEncode(Path.GetFileName(steps)) +
                                    "\">the step-by-step troubleshooter</a> &ndash; it names this machine's parts and says what to do, one step after the other. " +
                                    "This manual is the general view behind it.</p>");
                }
                note.Append("</div>");

                int h1 = html.IndexOf("</h1>", StringComparison.OrdinalIgnoreCase);
                html = h1 >= 0 ? html.Insert(h1 + 5, note.ToString()) : html.Replace("<body>", "<body>" + note);
                html = html.Replace("</style>", TailoredStyle + "</style>");

                var name = Path.GetFileNameWithoutExtension(doc.FullPath) + "_v" + doc.Valve + "_f" + doc.Feedback +
                           (doc.Movement == null ? "" : "_" + SafeName(doc.Movement.Unit) + "_" + doc.Movement.Side) + ".html";
                var path = Path.Combine(Path.GetTempPath(), name);
                File.WriteAllText(path, html, new System.Text.UTF8Encoding(true));
                return path;
            }
            catch
            {
                return doc.FullPath;
            }
        }

        private const string TailoredStyle =
            " .tailored p{margin:.1em 0 .4em}" +
            " table.movement{width:auto;margin:.3em 0 .2em}" +
            " table.movement td{border:none;padding:.15em .8em .15em 0;vertical-align:top}" +
            " table.movement td:first-child{font-weight:bold;white-space:nowrap}" +
            " table.movement code{font-size:1.05em;font-weight:bold;color:#1f4e79}" +
            " .tailored .ref{color:#555;font-size:.92em}" +
            " .tailored .steps{background:#fff7d6;border:1px solid #e0c060;padding:.4em .6em;margin:.4em 0}\n";

        /// <summary>
        /// The detail tables joined into one: the first table's column widths and
        /// heading row, then every detail's rows in order - each detail its own
        /// &lt;tbody class="block"&gt;, so it can still be told apart (data-detail,
        /// data-variant). The per-detail sections were one table each, and the
        /// repeated heading between them broke the chart into pieces. The manual
        /// is written as one chart now (tools/convert_cylinder_manual.py); this
        /// joins a copy still in the old shape, and leaves one already joined as
        /// it is.
        /// </summary>
        public static string JoinBlocks(string html)
        {
            var blocks = Regex.Matches(html, "<section class=\"block\"([^>]*)>(.*?)</section>\\s*", RegexOptions.Singleline);
            if (blocks.Count < 2) return html;

            var first = blocks[0].Groups[2].Value;
            int tbody = first.IndexOf("<tbody", StringComparison.OrdinalIgnoreCase);
            if (tbody < 0) return html;
            var head = first.Substring(0, tbody);           // <table ...><colgroup>...</colgroup><thead>...</thead>

            var sb = new System.Text.StringBuilder();
            sb.Append("<section class=\"chart\">").Append(head);
            foreach (Match b in blocks)
            {
                // The section's own attributes move to its rows: data-detail, and
                // data-variant on the valve and feedback blocks.
                var attributes = b.Groups[1].Value.Trim();
                var body = Regex.Match(b.Groups[2].Value, "<tbody>(.*?)</tbody>", RegexOptions.Singleline);
                if (!body.Success) return html;              // not the shape expected: leave the manual as it is
                sb.Append("<tbody class=\"block\" ").Append(attributes).Append(">").Append(body.Groups[1].Value).Append("</tbody>\n");
            }
            sb.Append("</table></section>\n");

            int from = blocks[0].Index;
            var last = blocks[blocks.Count - 1];
            int to = last.Index + last.Length;
            return html.Substring(0, from) + sb + html.Substring(to);
        }

        private static string SafeName(string s)
        {
            var t = new string((s ?? "").Where(char.IsLetterOrDigit).ToArray());
            return t.Length == 0 ? "unit" : t.Length > 40 ? t.Substring(0, 40) : t;
        }

        public static string Describe(ValveType v)
        {
            switch (v)
            {
                // The manual's own names, so the picker, the tooltip and the page
                // the operator is handed all say the same thing.
                case ValveType.DoubleSolenoid:       return "Double solenoid 5/2 (impulse / memory)";
                case ValveType.SingleSolenoidSpring: return "Single solenoid 5/2 (spring return)";
                case ValveType.ClosedCentre:
                case ValveType.OpenCentre:           return "Double solenoid 5/3 (center position)";
                default:                             return "unspecified";
            }
        }

        public static string Describe(FeedbackType f)
        {
            switch (f)
            {
                case FeedbackType.SingleMagnetic:   return "Single magnetic sensor (short stroke)";
                case FeedbackType.TwoMagnetic:      return "Two end magnetic sensors";
                case FeedbackType.SingleInductive:  return "Single inductive sensor (short stroke)";
                case FeedbackType.AnalogueMagnetic: return "Analogue magnetic sensor (4-20 mA)";
                case FeedbackType.PressureSwitch:   return "Magnetic sensor + pressure switch (clamping)";
                case FeedbackType.EncoderReference: return "Incremental encoder + reference sensor";
                case FeedbackType.None:             return "no feedback (timed)";
                default:                            return "unspecified";
            }
        }

        /// <summary>A one-line summary of a code, for a tooltip or a cell.</summary>
        public static string Explain(string code)
        {
            ErrorType type; int p1, p2;
            if (!TryParse(code, out type, out p1, out p2)) return "";

            switch (type)
            {
                case ErrorType.CylinderMovement:
                    return "Cylinder movement - " + Describe((ValveType)p1) +
                           ", " + Describe((FeedbackType)p2);
                case ErrorType.ServoFault:
                    return p1 > 0 ? "Servo fault - axis " + p1 : "Servo fault";
                default:
                    return type.ToString();
            }
        }
    }
}

