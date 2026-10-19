using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Kaban.Core.Helpers
{
    /// <summary>What an axis was doing when its alarm came (16 October).</summary>
    public enum AxisMotion { Unknown, Standing, Homing, Manual, Automatic }

    /// <summary>The kind of fault an axis code is, for the help: what to look at first.</summary>
    public enum AxisFault { Other, Overload, SoftLimit, HardLimit, HomeSensor, HomeFirst, HomeAtHome, HomeParameters, AbsoluteLost, Encoder, Supply }

    /// <summary>
    /// AN AXIS AT THE MOMENT OF ITS ALARM (16 October: "AxisSettingHMI's helper could be
    /// extended according to the movement state at the error moment - not moving, homing,
    /// manual movement and automatic movement - just like the CylinderUnitHMI helpers: the
    /// axis overload should probably be caused by the reference sensor if it starts moving
    /// (BUSY ON) and gets stuck afterwards during the homing process, but it should probably
    /// be a mechanical crash or an axis limit setting error at the other movement states").
    ///
    /// Taken by the HMI's axis watch (AlarmGeneration.Axis) from the scan BEFORE the code
    /// came - the module's operation status (Md.26), BUSY (Md.141), the jog buttons, the
    /// machine running, the reference sensor, the position and the limits - and from the
    /// movement until then: did it move since it started, and did it stand still on the
    /// way before the alarm. Its help page (AxisSteps) says what the axis was doing, the
    /// most likely cause for that code at that moment, and the checks in that order - the
    /// reference sensor with the drawing's numbers (Wiring.csv) when it is the suspect.
    /// </summary>
    public sealed class AxisMoment
    {
        public int Station = 1;
        public string Unit = "";
        public AxisCodeKind Kind;
        public int Code;
        public DateTime? AlarmTime;
        /// <summary>False when the code was already there at the first scan: the readings are from after it.</summary>
        public bool SeenRising = true;

        public AxisMotion Motion;
        /// <summary>The module's operation status (Md.26) the scan before, when it is read.</summary>
        public int? Status;
        public bool? Busy, JogBack, JogForward, Running, ManualMode, HomeRequest;
        /// <summary>The PLC's homing in progress: the row's "go to reference" (FA2010 B0...: ON until the homing is complete).</summary>
        public bool? Homing;
        /// <summary>The way it moved: +1 forward (the position growing), -1 back, 0 not known.</summary>
        public int Direction;
        /// <summary>The position changed since the movement started.</summary>
        public bool Moved;
        /// <summary>It moved, then stood still on the way - this long before the alarm (ms).</summary>
        public bool Stuck;
        public int StuckMs;
        /// <summary>How long the movement had been going (ms), 0 when it was not moving.</summary>
        public int MovingMs;

        public double? Position, PositionStart, MinLimit, MaxLimit;
        /// <summary>The motion module's own software stroke limits (Pr.13 lower, Pr.12 upper), in the axis's unit.</summary>
        public double? ModuleMin, ModuleMax;

        /// <summary>The Axis page's limits are both there and equal (0 and 0): not set.</summary>
        public bool PageLimitsUnset { get { return MinLimit.HasValue && MaxLimit.HasValue && MinLimit.Value == MaxLimit.Value; } }
        /// <summary>The module's limits are equal: its software stroke limit is switched off.</summary>
        public bool ModuleLimitsOff { get { return ModuleMin.HasValue && ModuleMax.HasValue && ModuleMin.Value == ModuleMax.Value; } }
        /// <summary>The module's limits are read and in use.</summary>
        public bool ModuleLimitsOn { get { return ModuleMin.HasValue && ModuleMax.HasValue && ModuleMin.Value != ModuleMax.Value; } }

        /// <summary>The limits the axis stops at: the module's when in use, else the Axis page's when set.</summary>
        public double? LimitMin { get { return ModuleLimitsOn ? ModuleMin : (PageLimitsUnset ? null : MinLimit); } }
        public double? LimitMax { get { return ModuleLimitsOn ? ModuleMax : (PageLimitsUnset ? null : MaxLimit); } }
        public int Decimals = 2;
        public string Suffix = "";

        /// <summary>The reference sensor as the page reads it (M330 / X100) and its state the scan before.</summary>
        public string RefAddress = "";
        public bool? RefOn;
        /// <summary>The reference sensor is on the forward side (the jog button it lights).</summary>
        public bool RefForward;
        /// <summary>The drawing's reference sensor (Wiring.csv): tag, input, card, wire, sheet.</summary>
        public WiringTable.Point RefPoint;
        public bool Vertical;

        /// <summary>What kind of fault the code is.</summary>
        public AxisFault Fault { get { return AxisSteps.Classify(Kind, Code); } }

        /// <summary>The module's state the scan before, in words ("Home return (7)"), or "".</summary>
        public string StatusText { get { return Status.HasValue ? SimpleMotion.StatusText(Status.Value) : ""; } }

        /// <summary>A position or limit in the axis's unit ("1234.56 mm"), or "".</summary>
        public string Shown(double? v)
        {
            if (!v.HasValue) return "";
            var t = v.Value.ToString("F" + Math.Max(0, Math.Min(6, Decimals)), CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(Suffix) ? t : t + " " + Suffix;
        }

        /// <summary>At or past its minimum / maximum (a hair's width counts: the axis stops on the limit).</summary>
        public bool AtMin { get { return Position.HasValue && LimitMin.HasValue && Position.Value <= LimitMin.Value + Tolerance; } }
        public bool AtMax { get { return Position.HasValue && LimitMax.HasValue && Position.Value >= LimitMax.Value - Tolerance; } }
        private double Tolerance { get { return Math.Pow(10, -Math.Max(0, Decimals)) * 5; } }

        /// <summary>The reference sensor's side was moved away from: homing the wrong way.</summary>
        public bool AwayFromReference { get { return Direction != 0 && RefKnown && (Direction > 0) != RefForward; } }
        public bool RefKnown { get { return RefPoint != null || !string.IsNullOrWhiteSpace(RefAddress); } }

        /// <summary>A module state that is a movement: interpolation, JOG, manual pulse, home return, positioning.</summary>
        /// <summary>One jog command ON: both at once is no jog (the PLC starts neither).</summary>
        public static bool OneJog(bool? jogBack, bool? jogForward) { return (jogBack == true) != (jogForward == true); }

        /// <summary>
        /// A jog command was ON but it did not move the axis: the motion module stood, or
        /// both commands were ON at once.
        /// </summary>
        public bool JogIdle
        {
            get
            {
                if (JogBack != true && JogForward != true) return false;
                return (JogBack == true && JogForward == true) || ModuleStood;
            }
        }

        /// <summary>The motion module was read and it stood: not BUSY, not moving.</summary>
        public bool ModuleStood
        {
            get { return (Status.HasValue || Busy.HasValue) && !(Busy == true || (Status.HasValue && IsMoving(Status.Value))); }
        }

        public static bool IsMoving(int status) { return status == 2 || status == 3 || status == 4 || (status >= 6 && status <= 13); }

        /// <summary>
        /// What the axis was doing, from what was read the scan before:
        ///   the PLC's homing in progress (the row's "go to reference"),
        ///   or the module's home return (7)                                 homing
        ///   the module in JOG / manual pulse (3, 4)                         manual
        ///   the module standing (not BUSY, not moving): standing - a jog
        ///   command ON did not move it (17 October)                         standing
        ///   one jog command ON (both at once is no jog)                     manual
        ///   moving while the axis asks to be referenced (the PLC's or
        ///   the module's home request)                                      homing
        ///   moving otherwise: the machine running - automatic; in manual
        ///   mode (a "go to" of a manual page) - manual; else automatic
        ///   not moving                                                      standing
        /// Nothing read at all: not known.
        /// </summary>
        public static AxisMotion MotionOf(int? status, bool? busy, bool? jogBack, bool? jogForward, bool? running,
                                          bool? manualMode = null, bool? homeRequest = null, bool? homing = null)
        {
            // the PLC's homing in progress: whatever the module does meanwhile (19 October: B0 "stays on until the home process completed")
            if (homing == true) return AxisMotion.Homing;
            if (status == 7) return AxisMotion.Homing;
            if (status == 3 || status == 4) return AxisMotion.Manual;
            bool moving = busy == true || (status.HasValue && IsMoving(status.Value));
            // the motion module says what it did: it stood (17 October - "jogged" while it stood was not true)
            if ((status.HasValue || busy.HasValue) && !moving) return AxisMotion.Standing;
            if (OneJog(jogBack, jogForward)) return AxisMotion.Manual;
            if (moving)
            {
                if (homeRequest == true) return AxisMotion.Homing;
                if (running == true) return AxisMotion.Automatic;
                if (manualMode == true) return AxisMotion.Manual;
                return AxisMotion.Automatic;
            }
            if (status.HasValue || busy.HasValue || jogBack.HasValue || jogForward.HasValue) return AxisMotion.Standing;
            // only the machine's state is known: running is all it says
            if (running == true) return AxisMotion.Automatic;
            if (running.HasValue) return AxisMotion.Standing;
            return AxisMotion.Unknown;
        }
    }

    /// <summary>
    /// THE AXIS'S STEP-BY-STEP PAGE (16 October): the moment of the alarm, the most likely
    /// cause for its code at that moment, and the checks - in a template a person can read
    /// and change (Help/Templates/EN/Axis_Steps.html; a language of its own beside it, as
    /// the cylinders' pages: TR/Axis_Steps.html). This code decides WHAT applies (the fault,
    /// the hints and the checks, by their names); the words are the template's phrases.
    /// </summary>
    public static class AxisSteps
    {
        public const string TemplateFile = "Axis_Steps.html";

        /// <summary>What kind of fault a code is.</summary>
        public static AxisFault Classify(AxisCodeKind kind, int raw)
        {
            if (raw <= 0) return AxisFault.Other;
            if (kind == AxisCodeKind.ServoAlarm)
            {
                int alarm = (raw >> 4) & 0xFF, detail = raw & 0xF;     // 0x501: 50.1
                switch (alarm)
                {
                    // overloads, excess droop (the motor cannot follow), deviation from the load-side
                    // encoder - and their warnings before them (E1, EC, 9B)
                    case 0x50: case 0x51: case 0x52: case 0x42: case 0xE1: case 0xEC: case 0x9B: return AxisFault.Overload;
                    // 69.1 / 69.2 / 69.5 / 69.6 software limits, 69.3 / 69.4 stroke ends
                    case 0x69: return detail == 3 || detail == 4 ? AxisFault.HardLimit : AxisFault.SoftLimit;
                    case 0x98: return AxisFault.SoftLimit;
                    case 0x99: return AxisFault.HardLimit;
                    case 0x90: return AxisFault.HomeFirst;
                    case 0x25: case 0x92: case 0x9F: return AxisFault.AbsoluteLost;
                    case 0x16: case 0x1E: case 0x1F: case 0x20: case 0x21: case 0x2A: case 0x2B:
                    case 0x70: case 0x71: case 0x72: case 0x34: case 0x36: return AxisFault.Encoder;
                    case 0x10: case 0x33: return AxisFault.Supply;
                }
                return AxisFault.Other;
            }
            if (kind == AxisCodeKind.AxisError)
            {
                switch (raw)
                {
                    case 0x1904: case 0x1906: return AxisFault.HardLimit;
                    case 0x1993: case 0x1995: case 0x1A18: case 0x1A1A: return AxisFault.SoftLimit;
                    // dog detection timing, the dog too close for the count method, the zero point not passed
                    case 0x1941: case 0x1944: case 0x197A: return AxisFault.HomeSensor;
                    case 0x1945: case 0x1946: case 0x19A6: return AxisFault.HomeFirst;
                    case 0x1940: return AxisFault.HomeAtHome;
                    case 0x1978: return AxisFault.HomeParameters;
                    case 0x1900: case 0x1902: return AxisFault.Supply;
                }
                if (raw >= 0x1B00 && raw <= 0x1B11) return AxisFault.HomeParameters;
            }
            return AxisFault.Other;
        }

        /// <summary>
        /// The hint (the most likely cause) for the moment: one phrase of the template,
        /// "H_" + the name returned.
        /// </summary>
        public static string HintOf(AxisMoment m)
        {
            switch (m.Fault)
            {
                case AxisFault.Overload:
                    switch (m.Motion)
                    {
                        case AxisMotion.Homing:
                            if (m.Stuck) return "HomeStuck";                 // moved, then stood still: the sensor not seen
                            if (m.Position.HasValue && !m.Moved && m.MovingMs > 0) return "HomeNoMove";
                            return "HomeMoving";
                        case AxisMotion.Manual: return "ManualCrash";
                        case AxisMotion.Automatic: return "AutoCrash";
                        case AxisMotion.Standing: return m.Vertical ? "StandingVertical" : "Standing";
                        default: return "Overload";
                    }
                case AxisFault.SoftLimit: return m.Motion == AxisMotion.Homing ? "SoftLimitHoming" : "SoftLimit";
                case AxisFault.HardLimit: return "HardLimit";
                case AxisFault.HomeSensor: return "HomeSensor";
                case AxisFault.HomeFirst: return "HomeFirst";
                case AxisFault.HomeAtHome: return "HomeAtHome";
                case AxisFault.HomeParameters: return "HomeParameters";
                case AxisFault.AbsoluteLost: return "AbsoluteLost";
                case AxisFault.Encoder: return "Encoder";
                case AxisFault.Supply: return "Supply";
                default: return "Other";
            }
        }

        /// <summary>The checks, in order: phrases "S_" + each name.</summary>
        public static List<string> StepsOf(AxisMoment m)
        {
            var s = new List<string>();
            switch (HintOf(m))
            {
                case "HomeStuck": s.AddRange(m.AwayFromReference ? new[] { "HomeDirection", "RefSensor", "Mechanics" }
                                                                 : new[] { "RefSensor", "HomeDirection", "Mechanics" }); break;
                case "HomeNoMove": s.AddRange(m.Vertical ? new[] { "Brake", "Mechanics", "HomeDirection" } : new[] { "Mechanics", "HomeDirection" }); break;
                case "HomeMoving": s.AddRange(new[] { "RefSensor", "Mechanics", "Dynamics" }); break;
                case "ManualCrash": s.AddRange(new[] { "Mechanics", "Limits" }); if (m.Vertical) s.Add("Brake"); break;
                case "AutoCrash": s.AddRange(new[] { "Mechanics", "Limits", "Dynamics" }); break;
                case "StandingVertical": s.AddRange(new[] { "Brake", "Mechanics" }); break;
                case "Standing": s.Add("Mechanics"); break;
                case "Overload": s.AddRange(new[] { "Mechanics", "RefSensor", "Limits" }); if (m.Vertical) s.Add("Brake"); break;
                case "SoftLimitHoming": s.AddRange(new[] { "Limits", "RefSensor" }); break;
                case "SoftLimit": s.Add("Limits"); break;
                case "HardLimit": s.AddRange(new[] { "LimitSwitch", "Limits" }); break;
                case "HomeSensor": s.AddRange(new[] { "RefSensor", "HomeDirection" }); break;
                case "HomeFirst": s.Add("HomeAgain"); break;
                case "HomeAtHome": s.Add("HomeDirection"); break;
                case "HomeParameters": s.Add("Parameters"); break;
                case "AbsoluteLost": s.AddRange(new[] { "Battery", "HomeAgain" }); break;
                case "Encoder": s.Add("EncoderCable"); break;
                case "Supply": s.Add("Supply"); break;
            }
            // a holding brake only on a vertical axis - the up / down arrows (19 October)
            if (!m.Vertical) s.Remove("Brake");
            return s;
        }

        // ---- the reference sensor on the drawings -------------------------------------

        /// <summary>
        /// The drawing's reference sensor of an axis: the point at the address the page
        /// reads it at (X115, or its relay M257) - else the point the drawing names for that
        /// axis ("Input Robot X Axis Reference Sensor" for Robot X), when exactly one fits best.
        /// </summary>
        public static WiringTable.Point FindReferenceSensor(int station, string address, string unit)
        {
            var p = string.IsNullOrWhiteSpace(address) ? null : WiringTable.Find(station, address);
            if (p != null) return p;
            var want = Words(unit);
            want.ExceptWith(Generic);
            if (want.Count == 0) return null;
            var refs = WiringTable.Points(station)
                .Where(q => q.IsInput && q.Text.IndexOf("reference", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            // every word of the axis's name ("Robot X": robot, x) - else every word but its letter ("Exit X": exit)
            return Best(refs, want) ?? (want.Any(w => w.Length == 1) ? Best(refs, new HashSet<string>(want.Where(w => w.Length > 1))) : null);
        }

        private static WiringTable.Point Best(List<WiringTable.Point> refs, HashSet<string> want)
        {
            if (want.Count == 0) return null;
            WiringTable.Point best = null;
            int bestExtra = int.MaxValue;
            bool tie = false;
            foreach (var q in refs)
            {
                var have = Words(q.Text);
                // "Cutting Angle" is "Cutting A Axis" on the drawing
                if (!want.All(w => have.Contains(w) || (w == "angle" && have.Contains("a")))) continue;
                int extra = have.Count(h => !want.Contains(h) && !Generic.Contains(h) && !(h == "a" && want.Contains("angle")));
                if (extra < bestExtra) { best = q; bestExtra = extra; tie = false; }
                else if (extra == bestExtra) tie = true;
            }
            return tie ? null : best;
        }

        private static readonly HashSet<string> Generic = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "axis", "reference", "sensor", "input", "in", "ref", "sn", "the" };

        /// <summary>"Milling 1X" -> milling, 1, x; "1. Milling X Axis" -> 1, milling, x, axis.</summary>
        private static HashSet<string> Words(string text)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(text ?? "", @"[0-9]+|[A-Za-z]+"))
                set.Add(m.Value.ToLowerInvariant());
            return set;
        }

        // ---- the page ------------------------------------------------------------------

        /// <summary>
        /// The template in a language: its own, else English - from the Help folder, else the
        /// copy built into the program (17 October: a Help folder without it - a project not
        /// built again after the file was added - left the axes with no hint at all).
        /// </summary>
        public static string TemplatePath(string language)
        {
            foreach (var lang in new[] { (language ?? "").ToUpperInvariant(), "EN" })
            {
                if (lang.Length == 0) continue;
                var p = Path.Combine(TroubleshootingCatalog.TemplateFolder, lang, TemplateFile);
                try { if (File.Exists(p)) return p; } catch { }
            }
            return BuiltInTemplate();
        }

        /// <summary>The program's own copy of the English template, written to the temp folder once ("" when it has none).</summary>
        internal static string BuiltInTemplate()
        {
            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "KabanHMI_Templates", "EN");
                var path = Path.Combine(dir, TemplateFile);
                using (var s = typeof(AxisSteps).Assembly.GetManifestResourceStream("Kaban.Core.Axis_Steps.html"))
                {
                    if (s == null) return "";
                    byte[] data;
                    using (var ms = new MemoryStream()) { s.CopyTo(ms); data = ms.ToArray(); }
                    if (!File.Exists(path) || new FileInfo(path).Length != data.Length || !File.ReadAllBytes(path).SequenceEqual(data))
                    {
                        Directory.CreateDirectory(dir);
                        File.WriteAllBytes(path, data);
                    }
                }
                return path;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("the axis help's built-in template could not be written: " + ex.Message);
                return "";
            }
        }

        private static string H(string s) { return WebUtility.HtmlEncode(s ?? ""); }

        /// <summary>Writes the moment's page to a temporary file and returns its path ("" when it cannot).</summary>
        public static string Write(TroubleshootingCatalog.HelpDocument doc)
        {
            if (doc == null || doc.AxisMoment == null) return "";
            return WritePage(doc.AxisMoment, doc.AlarmTime, doc.AlarmText);
        }

        /// <summary>The page as HTML ("" without a template).</summary>
        public static string Build(AxisMoment m, string language, DateTime? alarmTime = null, string alarmText = "")
        {
            var path = TemplatePath(language);
            var tpl = TroubleshootingCatalog.LoadTemplate(path);
            if (tpl == null) return "";
            return Build(m, tpl, alarmTime, alarmText);
        }

        /// <summary>The page from a template (tests give their own).</summary>
        public static string Build(AxisMoment m, StepTemplate tpl, DateTime? alarmTime = null, string alarmText = "")
        {
            return tpl.Render(ModelOf(m, tpl, alarmTime, alarmText));
        }

        /// <summary>The moment's step-by-step page written to the temp folder; its path ("" when it cannot).</summary>
        public static string WritePage(AxisMoment m, DateTime? alarmTime = null, string alarmText = "")
        {
            if (m == null) return "";
            try
            {
                var html = Build(m, LanguageManager.CurrentLanguage, alarmTime, alarmText);
                if (string.IsNullOrEmpty(html)) return "";
                var name = "Steps_Axis_" + new string((m.Unit ?? "").Where(char.IsLetterOrDigit).ToArray()) + "_" +
                           AxisCodeHelp.Key(m.Kind, m.Code).Replace(".", "") + ".html";
                var path = Path.Combine(Path.GetTempPath(), name);
                File.WriteAllText(path, html, new UTF8Encoding(true));
                return path;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("the axis step-by-step help could not be written: " + ex.Message);
                return "";
            }
        }

        private static Dictionary<string, object> ModelOf(AxisMoment m, StepTemplate tpl, DateTime? alarmTime, string alarmText)
        {
            bool admin = false;
            try { admin = TroubleshootingCatalog.AdminView != null && TroubleshootingCatalog.AdminView(); } catch { }
            var on = tpl.Phrase("On");
            var off = tpl.Phrase("Off");
            Func<bool?, string> state = b => !b.HasValue ? "" : b.Value ? on : off;

            var model = new Dictionary<string, object>(StringComparer.Ordinal);
            var time = alarmTime ?? m.AlarmTime;
            model["Title"] = H(string.IsNullOrWhiteSpace(alarmText) ? m.Unit : alarmText);
            model["Unit"] = H(m.Unit);
            model["Code"] = H(AxisCodeHelp.Describe(m.Kind, m.Code));
            // the alarm's text names the code already ("Robot X - Servo alarm 50.1 - ..."): not twice
            model["CodeShown"] = !string.IsNullOrWhiteSpace(alarmText) && alarmText.IndexOf(AxisCodeHelp.Format(m.Kind, m.Code), StringComparison.OrdinalIgnoreCase) >= 0
                                 ? "" : H(AxisCodeHelp.Describe(m.Kind, m.Code));
            model["AlarmDate"] = time.HasValue ? H(time.Value.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture)) : "";
            model["Created"] = H(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture));
            model["SeenRising"] = m.SeenRising;
            model["Admin"] = admin;

            // what it was doing
            string motion = m.Motion.ToString();
            model["Motion"] = tpl.Phrase("M_" + motion);
            model["Is" + motion] = true;
            model["Status"] = H(m.StatusText);
            model["Busy"] = state(m.Busy);
            model["JogBack"] = state(m.JogBack);
            model["JogForward"] = state(m.JogForward);
            model["JogBackOn"] = m.JogBack == true;
            model["JogForwardOn"] = m.JogForward == true;
            model["JogIdle"] = m.JogIdle;
            model["ModuleStood"] = m.ModuleStood;
            model["Running"] = state(m.Running);
            model["HomingBit"] = state(m.Homing);
            model["Moved"] = m.Moved;
            model["Stuck"] = m.Stuck;
            model["StuckSeconds"] = H((m.StuckMs / 1000.0).ToString("0.0", CultureInfo.InvariantCulture));
            model["MovingSeconds"] = H((m.MovingMs / 1000.0).ToString("0.0", CultureInfo.InvariantCulture));
            model["WasMoving"] = m.MovingMs > 0;
            model["Vertical"] = m.Vertical;

            // where it was
            model["Position"] = H(m.Shown(m.Position));
            model["PositionStart"] = m.Moved ? H(m.Shown(m.PositionStart)) : "";
            model["MinLimit"] = H(m.Shown(m.MinLimit));
            model["MaxLimit"] = H(m.Shown(m.MaxLimit));
            model["HasLimits"] = m.MinLimit.HasValue || m.MaxLimit.HasValue;
            model["PageLimitsUnset"] = m.PageLimitsUnset;
            model["ModuleMin"] = H(m.Shown(m.ModuleMin));
            model["ModuleMax"] = H(m.Shown(m.ModuleMax));
            model["HasModuleLimits"] = m.ModuleMin.HasValue || m.ModuleMax.HasValue;
            model["ModuleLimitsOff"] = m.ModuleLimitsOff;
            model["LimitMin"] = H(m.Shown(m.LimitMin));
            model["LimitMax"] = H(m.Shown(m.LimitMax));
            model["AtMin"] = m.AtMin;
            model["AtMax"] = m.AtMax;

            // the reference sensor, with the drawing's numbers
            var p = m.RefPoint;
            bool refKnown = m.RefKnown;
            model["HasRef"] = refKnown;
            model["RefState"] = refKnown ? state(m.RefOn) : "";
            model["RefWasOn"] = refKnown && m.RefOn == true;
            model["RefWasOff"] = refKnown && m.RefOn == false;
            model["RefForward"] = m.RefForward;
            model["AwayFromRef"] = m.AwayFromReference;
            // the PLC's internal relay only for an Admin; a real input always
            string refShown = p != null ? p.Address : (m.RefAddress ?? "").Trim();
            if (p == null && !admin && !Regex.IsMatch(refShown, "^X", RegexOptions.IgnoreCase)) refShown = "";
            model["RefInput"] = H(refShown);
            model["RefRead"] = admin && p != null && !string.IsNullOrWhiteSpace(m.RefAddress) &&
                               !string.Equals(p.Address, m.RefAddress.Trim(), StringComparison.OrdinalIgnoreCase) ? H(m.RefAddress.Trim()) : "";
            model["RefDrawn"] = p != null;
            if (p != null)
            {
                model["RefTag"] = H(p.Tag);
                model["RefText"] = H(p.Text);
                model["RefCard"] = H(p.Card);
                model["RefWire"] = H(p.Wire);
                model["RefSheet"] = H(p.Sheet);
                model["RefVia"] = H(p.Via);
                var supply = WiringTable.SupplyOf(m.Station);
                model["ZeroWire"] = supply != null ? H(supply.ZeroVoltWire) : "";
                model["SensorWire"] = supply != null ? H(supply.SensorSupplyWire) : "";
            }

            // the code's own entry of the drive's manual: the last step (18 October - one page per code)
            string manualStyle;
            var entry = AxisCodeHelp.EntryHtml(m.Kind, m.Code, ".manual", out manualStyle);
            model["ManualEntry"] = entry;
            model["ManualStyle"] = entry.Length > 0 && manualStyle.Length > 0 ? "<style>\n" + manualStyle + "</style>" : "";

            // the checks - each a phrase, numbered - then the most likely cause
            var steps = new List<IDictionary<string, object>>();
            int n = 0;
            foreach (var s in StepsOf(m))
            {
                if (s == "RefSensor" && !refKnown) continue;
                if (s == "Limits" && !(m.MinLimit.HasValue || m.MaxLimit.HasValue || m.ModuleMin.HasValue || m.ModuleMax.HasValue) && !m.Position.HasValue) continue;
                var body = tpl.Phrase("S_" + s, model);
                if (string.IsNullOrWhiteSpace(body)) continue;
                n++;
                steps.Add(new Dictionary<string, object> { { "N", n.ToString(CultureInfo.InvariantCulture) }, { "Id", "S" + n.ToString(CultureInfo.InvariantCulture) }, { "First", n == 1 },
                                                           { "Name", s }, { "Html", body },
                                                           { "Caption", tpl.HasPhrase("C_" + s) ? tpl.Phrase("C_" + s, model) : "" } });
            }
            if (entry.Length > 0 && tpl.HasPhrase("S_Manual"))
            {
                n++;
                model["ManualN"] = n.ToString(CultureInfo.InvariantCulture);
                steps.Add(new Dictionary<string, object> { { "N", n.ToString(CultureInfo.InvariantCulture) }, { "Id", "manual" }, { "First", n == 1 },
                                                           { "Name", "Manual" }, { "Html", tpl.Phrase("S_Manual", model) },
                                                           { "Caption", tpl.HasPhrase("C_Manual") ? tpl.Phrase("C_Manual", model) : "" } });
            }
            else model["ManualN"] = "";
            model["Steps"] = steps;
            model["HasSteps"] = steps.Count > 0;
            var hint = HintOf(m);
            model["Hint"] = tpl.Phrase("H_" + hint, model);
            return model;
        }
    }
}
