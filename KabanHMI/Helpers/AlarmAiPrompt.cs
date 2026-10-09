using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// AN AI PROMPT FOR AN ALARM (8 October): the last entry of every alarm's help
    /// list. A click puts on the clipboard everything the HMI knows about THIS
    /// alarm on THIS machine, for any AI chat (Claude, ChatGPT, Gemini, Grok ...):
    /// the machine and its control system, how its alarms are raised, the alarm
    /// and what the HMI saw, the unit with both its movements, every device
    /// involved (its PLC label, its wiring and air line from the drawings, where
    /// it is on the HMI, its state at the alarm and now), the other alarms, the
    /// users' notes, and the HMI's own help - its step-by-step page IN FULL, as
    /// the format to follow. Then what to write.
    ///
    /// THREE FILES IN THE STEP-BY-STEP PAGE'S FORMAT (8 October, second round):
    /// "the AI responses don't get very useful ... give as much data as possible
    /// ... and ask it to create a helper just like we create for the specific
    /// error ('Step by step') ... the user can start chatting with the AI which
    /// already knows the situation". So the request is for complete HTML pages
    /// like the HMI's own - one for the operator, one for the maintenance
    /// technician, one for the automation engineer - and for the AI to stay on
    /// and explain whatever part the reader does not understand. The request
    /// comes LAST, after the data and the example: a chat reads a long text best
    /// with its question at the end.
    /// </summary>
    public static class AlarmAiPrompt
    {
        /// <summary>The format example (the step-by-step page) in full up to this many characters; beyond it, without its drawings.</summary>
        public const int ExampleChars = 160000;
        /// <summary>How much of another help document's own text is put in (characters).</summary>
        public const int DocumentChars = 30000;
        /// <summary>The other documents' texts together, at most.</summary>
        public const int AllDocumentsChars = 50000;
        /// <summary>How many earlier alarms of the station are listed.</summary>
        public const int HistoryCount = 15;
        /// <summary>How many devices are read for their state now.</summary>
        public const int ReadNowMax = 60;

        private static readonly Dictionary<string, string> LanguageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "EN", "English" }, { "TR", "Turkish" }, { "RU", "Russian" }, { "DE", "German" }, { "IT", "Italian" }, { "ES", "Spanish" }
        };

        /// <summary>The language the answer is asked in: the HMI's.</summary>
        public static string LanguageName(string code)
        {
            string n;
            return LanguageNames.TryGetValue(code ?? "", out n) ? n : (string.IsNullOrEmpty(code) ? "English" : code);
        }

        /// <summary>
        /// Reads a device once for its state now (null when it cannot be read).
        /// Monitoring access. Replaceable for the tests.
        /// </summary>
        public static Func<int, string, int?> ReadNow = (station, address) =>
        {
            try
            {
                if (ConnectionMonitor.IsKnownDown(station)) return null;
                var drv = PlcManager.Instance.GetDriverForMonitoring(station);
                int v;
                if (drv != null && drv.TryRead(address, out v)) return v;
            }
            catch { }
            return null;
        };

        /// <summary>The devices the prompt describes, each once, in the order met, with what each is for.</summary>
        private sealed class Devices
        {
            public readonly List<string> Order = new List<string>();
            public readonly Dictionary<string, List<string>> Roles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> AtAlarm = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> Now = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public void Add(string address, string role, string name = null)
            {
                if (string.IsNullOrWhiteSpace(address)) return;
                address = address.Trim().ToUpperInvariant();
                List<string> roles;
                if (!Roles.TryGetValue(address, out roles)) { Roles[address] = roles = new List<string>(); Order.Add(address); }
                if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role)) roles.Add(role);
                if (!string.IsNullOrWhiteSpace(name) && !Names.ContainsKey(address) && !string.Equals(name.Trim(), address, StringComparison.OrdinalIgnoreCase))
                    Names[address] = name.Trim();
            }

            public void Add(TC.MovementInfo.Device d, string role)
            {
                if (d == null || string.IsNullOrWhiteSpace(d.Address)) return;
                Add(d.Address, role, d.Name);
                var a = d.Address.Trim().ToUpperInvariant();
                if (d.AtAlarm.HasValue && !AtAlarm.ContainsKey(a)) AtAlarm[a] = d.AtAlarm.Value;
                if (d.Now.HasValue && !Now.ContainsKey(a)) Now[a] = d.Now.Value;
            }
        }

        /// <summary>The prompt for an alarm on the list (item), or one only known by its definition (def), with its help documents.</summary>
        public static string Build(AlarmItem item, AlarmDefinition def, IList<TC.HelpDocument> docs)
        {
            int station = item != null ? item.Station : def != null ? def.PlcNumber : 1;
            if (station <= 0) station = 1;
            string address = item != null && !string.IsNullOrWhiteSpace(item.Address) ? item.Address.Trim()
                           : def != null ? (def.Address ?? "").Trim() : "";
            if (def == null && item != null)
                def = (address.Length > 0 ? AlarmManager.FindByStationAndAddress(station, address) : null) ??
                      AlarmManager.FindByAddressOrCode(item.Generated && !string.IsNullOrEmpty(item.BaseCode) ? item.BaseCode : item.AlarmCode);
            if (address.Length == 0 && def != null)
            {
                address = (def.Address ?? "").Trim();
                station = AlarmManager.StationOf(def);
            }
            var link = string.IsNullOrEmpty(address) ? null : AlarmHelpLinks.Find(station, address);
            string lang = LanguageManager.CurrentLanguage ?? "EN";
            string language = LanguageName(lang);
            docs = docs ?? new List<TC.HelpDocument>();
            var example = ExampleDocument(docs);
            string exampleHtml = example != null ? ExampleHtml(example, ExampleChars) : "";
            if (exampleHtml.Length == 0) example = null;
            bool stepByStep = example != null && example.StepByStep;
            string moduleId = AlarmGeneration.ModuleOfStation(station);
            var cylinder = CylinderOf(station, address, item, link);
            var axis = AxisOf(station, address, link);
            var devices = new Devices();
            var sb = new StringBuilder();

            // ---- what this is ---------------------------------------------------------
            sb.AppendLine("# Help me with an alarm on an industrial machine");
            sb.AppendLine();
            sb.AppendLine("I work at the machine described below and the alarm below is on its HMI (the operator panel). " +
                          "Below is everything the HMI knows about it: the machine and its control system, how its alarms are raised, the alarm and what the HMI saw when it came, " +
                          "the unit it belongs to with its movements, every device involved with its PLC label, its wiring and air line from the machine's drawings and its state, " +
                          "the other alarms, the notes the machine's users left" +
                          (example != null ? ", and the HMI's own help for this alarm - its " + (stepByStep ? "step-by-step page" : "help page") + " in full, as the format I want" : "") +
                          ". What I ask you is at the end, under \"What I ask you\". Answer in " + language + ".");
            sb.AppendLine();

            // ---- the machine ----------------------------------------------------------
            sb.AppendLine("## The machine");
            string machineName = MachineName(moduleId);
            sb.AppendLine("- Machine: " + (machineName.Length > 0 ? machineName : "(not named)") +
                          (moduleId.Length > 0 ? " - model " + moduleId : "") + ", built by Kaban Makina (aluminium / PVC profile processing machines).");
            var others = Kaban.Core.Machines.MachineRegistry.Modules
                .Where(m => m.Context != null && !string.Equals(m.Context.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                .Select(m => MachineName(m.Context.ModuleId) + " (" + m.Context.ModuleId + ")").ToList();
            if (others.Count > 0) sb.AppendLine("- On the same panel also: " + string.Join(", ", others) + ".");
            var modules = PlcModules(station);
            sb.AppendLine("- Control system: Mitsubishi PLC (MELSEC iQ-F)" + (modules.Count > 0 ? ", its cards and modules (from the electrical drawing): " + string.Join(", ", modules) : "") +
                          ". Servo axes run through Mitsubishi simple motion modules and MR-J4 servo amplifiers where the machine has axes.");
            var stationName = StationName(station);
            sb.AppendLine("- This alarm comes from PLC station " + station + (stationName.Length > 0 ? " (" + stationName + ")" : "") + ".");
            var supply = Safe(() => { var w = WiringTable.SupplyOf(station); return w == null ? "" :
                (w.ZeroVoltWire.Length > 0 ? "0 V is wire " + w.ZeroVoltWire : "") +
                (w.SensorSupplyWire.Length > 0 ? (w.ZeroVoltWire.Length > 0 ? ", " : "") + "the sensors' +24 V is wire " + w.SensorSupplyWire : ""); });
            if (supply.Length > 0) sb.AppendLine("- On the electrical drawing " + supply + ".");
            sb.AppendLine("- The HMI (Kaban HMI) reads the PLC every scan. Its pages: Manual Movement (each cylinder is a unit with two buttons - one per movement - " +
                          "and lamps for its sensors), Machine Parameters (one row per servo axis: jog buttons, position, current, limits, reference, its alarm codes), " +
                          "Alarms (this list, with help documents for each alarm), and side pages (Inputs / Outputs, Axis Monitor, PLC Monitor). " +
                          "The panel's language now: " + language + "; its users sign in as Operator, Maintenance or Admin.");
            sb.AppendLine();

            // ---- how its alarms are raised --------------------------------------------
            sb.AppendLine("## How the alarms are raised");
            sb.AppendLine("- The PLC program raises most alarms: it sets an alarm bit and the HMI lists the alarm while the bit is ON. The PLC's alarm reset clears them.");
            var gen = AlarmGeneration.For(moduleId);
            sb.AppendLine("- The HMI also watches each cylinder's movements itself" + (gen.Enabled ? "" : " (switched off for this machine now)") +
                          ": when a movement's valve output comes ON, its end sensors must confirm it within " + Seconds(gen.DelaySeconds) +
                          " (a movement measured by a position reading: the reading must move and settle). If not, the HMI raises the movement's alarm, named by its error bit, " +
                          "and SETs that bit in the PLC so the program knows; its code says why: .1 the start sensor stayed ON (it did not leave), .2 the target sensor did not come ON, " +
                          ".3 the target came ON but the start sensor stayed ON, .4 both coils were ON, .5 the position did not change, .6 the position was still changing.");
            sb.AppendLine("- For the servo axes the HMI " + (gen.AxisAlarms ? "raises" : "can raise (switched off for this machine now)") +
                          " an alarm as soon as an axis' servo alarm or axis error code is not zero, and SETs the axis' error bit in the PLC.");
            sb.AppendLine();

            // ---- the alarm ------------------------------------------------------------
            sb.AppendLine("## The alarm");
            string text = item != null ? item.Description : def != null ? LanguageManager.Localize(def.TranslationKey) : "";
            if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine("- Text on the HMI: \"" + text.Trim() + "\"");
            string english = def != null ? (LanguageManager.GetStringIn("EN", def.TranslationKey) ?? "") : "";
            if (english.Length > 0 && !string.Equals(english, text, StringComparison.Ordinal)) sb.AppendLine("- In English: \"" + english + "\"");
            if (item != null)
            {
                sb.AppendLine("- Code: " + item.ShownCode + (item.AlarmCode != item.ShownCode ? " (" + item.AlarmCode + ")" : ""));
                sb.AppendLine("- Severity: " + item.Severity);
                sb.AppendLine("- Came at: " + item.TriggerTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                              " (the prompt was made at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + ")");
                sb.AppendLine("- Still active: " + (AlarmManager.IsOnList(item) ? "yes" : "no (acknowledged or gone)"));
            }
            string device = item != null && !string.IsNullOrWhiteSpace(item.Device) ? item.Device.Trim() : address;
            if (device.Length > 0)
            {
                sb.AppendLine("- PLC device of the alarm: " + device + " (station " + station + ")");
                devices.Add(device, "the alarm's device" + (item != null && item.Generated ? " (the HMI SET it)" : ""));
            }
            if (address.Length > 0 && !address.Equals(device, StringComparison.OrdinalIgnoreCase)) devices.Add(address, "the alarm's device");
            if (item != null && item.Generated)
            {
                sb.AppendLine("- Raised by the HMI itself (not by the PLC program): the HMI timed the movement or read the drive's code.");
                var detail = item.DetailText != null ? Safe(item.DetailText) : item.Detail;
                if (!string.IsNullOrWhiteSpace(detail)) sb.AppendLine("- What the HMI saw fail: " + detail.Trim());
            }
            else if (item != null && !string.IsNullOrWhiteSpace(item.Detail))
                sb.AppendLine("- Detail: " + item.Detail.Trim());
            if (def != null && def.GenerationDelay.HasValue && def.GenerationDelay.Value > 0)
                sb.AppendLine("- The HMI waits " + Seconds(def.GenerationDelay.Value) + " for this movement (its own setting).");
            if (item != null && item.Snapshot != null && item.Snapshot.Count > 0)
            {
                sb.AppendLine("- Device states the scan before it came (1 = ON): " +
                              string.Join(", ", item.Snapshot.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).Select(k => k.Key + "=" + k.Value)));
                foreach (var kv in item.Snapshot) { devices.Add(kv.Key, null); devices.AtAlarm[kv.Key.Trim().ToUpperInvariant()] = kv.Value; }
            }
            sb.AppendLine();

            // ---- the unit -------------------------------------------------------------
            string unit = link != null && link.Unit.Length > 0 ? link.Unit : cylinder != null ? cylinder.Unit : axis != null ? axis.Name : "";
            if (link != null || cylinder != null || axis != null)
            {
                sb.AppendLine("## The unit it belongs to");
                sb.AppendLine("- Name on the HMI: " + (unit.Length > 0 ? unit : "(none)"));
                bool isCylinder = (link != null && link.Kind == TC.ErrorType.CylinderMovement) || (link == null && cylinder != null);
                if (isCylinder)
                {
                    int valve = link != null ? link.Valve : (int)cylinder.Valve, feedback = link != null ? link.Feedback : (int)cylinder.Feedback;
                    sb.AppendLine("- Kind: pneumatic cylinder");
                    sb.AppendLine("- Valve: " + TC.Describe((TC.ValveType)TC.CanonicalValve(valve)));
                    sb.AppendLine("- Feedback: " + TC.Describe((TC.FeedbackType)TC.CanonicalFeedback(feedback)));
                    var mv = link != null ? link.CurrentMovement() : null;
                    if (mv != null) AppendMovement(sb, mv, devices);
                    if (cylinder != null && cylinder.Sides.Count > 0)
                    {
                        sb.AppendLine("- Its movements, as the HMI watches them:");
                        foreach (var s in cylinder.Sides) AppendSide(sb, cylinder, s, devices);
                    }
                }
                else if ((link != null && link.Kind == TC.ErrorType.ServoFault) || axis != null)
                {
                    sb.AppendLine("- Kind: servo axis (simple motion module + MR-J4 servo amplifier)");
                    if (axis != null) sb.AppendLine("- Axis number in the motion module: " + axis.Number);
                    AppendCode(sb, devices, station, "Servo alarm code word [Md.114]", AxisCodeKind.ServoAlarm, link != null && link.ServoAlarm.Length > 0 ? link.ServoAlarm : axis != null ? axis.ServoAlarm : "");
                    AppendCode(sb, devices, station, "Axis error code word [Md.23]", AxisCodeKind.AxisError, link != null && link.AxisAlarm.Length > 0 ? link.AxisAlarm : axis != null ? axis.AxisAlarm : "");
                    AppendCode(sb, devices, station, "Axis warning code word [Md.24]", AxisCodeKind.AxisWarning, link != null ? link.AxisWarning : "");
                    if (axis != null) devices.Add(axis.ErrorAddress, "the axis' error bit");
                }
                else if (link != null) sb.AppendLine("- Kind: " + link.Kind);
                sb.AppendLine();
            }

            // ---- every device involved --------------------------------------------------
            var uses = Safe(() => DeviceCollector.UsesOf(station), new List<DeviceCollector.Use>());
            var io = Safe(() => Kaban.UI.Screens.IoList.ForMountedMachines().Where(x => x.Station == station).ToList(), new List<Kaban.UI.Screens.IoPoint>());
            if (devices.Order.Count > 0)
            {
                sb.AppendLine("## The devices involved (PLC label, wiring and air line from the drawings, where on the HMI, states: 1 = ON)");
                int reads = 0;
                foreach (var a in devices.Order)
                {
                    int now;
                    if (!devices.Now.ContainsKey(a) && reads < ReadNowMax)
                    {
                        reads++;
                        var v = Safe(() => ReadNow(station, a), (int?)null);
                        if (v.HasValue) devices.Now[a] = v.Value;
                    }
                    var head = new List<string>();
                    string name;
                    if (devices.Names.TryGetValue(a, out name)) head.Add("\"" + name + "\"");
                    if (devices.Roles[a].Count > 0) head.Add(string.Join("; ", devices.Roles[a]));
                    var label = Safe(() => { var e = PlcLabelTable.ByAddress(station, a); return e != null ? e.Label ?? "" : ""; });
                    if (label.Length == 0)
                    {
                        var pt = io.FirstOrDefault(x => string.Equals(x.Address, a, StringComparison.OrdinalIgnoreCase)) ??
                                 io.FirstOrDefault(x => string.Equals(x.Read, a, StringComparison.OrdinalIgnoreCase));
                        if (pt != null) label = pt.Label + (string.Equals(pt.Address, a, StringComparison.OrdinalIgnoreCase) ? "" : " (" + pt.Address + ")");
                    }
                    if (label.Length > 0) head.Add("PLC label " + label);
                    int at;
                    var states = new List<string>();
                    if (devices.AtAlarm.TryGetValue(a, out at)) states.Add("at the alarm " + State(a, at));
                    if (devices.Now.TryGetValue(a, out now)) states.Add("now " + State(a, now));
                    if (states.Count > 0) head.Add(string.Join(", ", states));
                    sb.AppendLine("- " + a + (head.Count > 0 ? " - " + string.Join(" | ", head) : ""));
                    foreach (var w in WiringLines(station, a)) sb.AppendLine("  - " + w);
                    var on = uses.Where(u => string.Equals(u.Address, a, StringComparison.OrdinalIgnoreCase) && !u.IsAlarm)
                                 .Select(u => u.PageTitle + ": " + u.UsedAs).Distinct().Take(4).ToList();
                    if (on.Count > 0) sb.AppendLine("  - on the HMI: " + string.Join("; ", on));
                }
                sb.AppendLine();
            }

            // ---- the other alarms --------------------------------------------------------
            var active = Safe(() => AlarmManager.ActiveAlarms.Where(a => a != null && a != item).ToList(), new List<AlarmItem>());
            var earlier = Safe(() => AlarmManager.History.Where(a => a != null && a != item && a.Station == station &&
                                                                   a.TriggerTime > DateTime.Now.AddDays(-7)).Take(HistoryCount).ToList(), new List<AlarmItem>());
            if (active.Count > 0 || earlier.Count > 0)
            {
                sb.AppendLine("## The other alarms");
                if (active.Count > 0)
                {
                    sb.AppendLine("On the list now:");
                    foreach (var a in active.Take(30)) sb.AppendLine("- " + AlarmLine(a));
                }
                if (earlier.Count > 0)
                {
                    sb.AppendLine("Earlier on this station (the last " + earlier.Count + " in 7 days, newest first):");
                    foreach (var a in earlier) sb.AppendLine("- " + AlarmLine(a));
                }
                sb.AppendLine();
            }

            // ---- the notes the panel's users left on it -------------------------------
            var notes = NotesOn(unit);
            if (notes.Count > 0)
            {
                sb.AppendLine("## Notes the machine's users left on this unit");
                foreach (var n in notes) sb.AppendLine("- " + n);
                sb.AppendLine();
            }

            // ---- what the HMI's help already says --------------------------------------
            if (docs.Count > 0)
            {
                sb.AppendLine("## The HMI's own help for this alarm");
                foreach (var d in docs)
                {
                    var ext = Path.GetExtension(d.FileName ?? "").ToLowerInvariant();
                    sb.AppendLine("- " + d.Title + (string.IsNullOrEmpty(d.Section) ? "" : " (section " + d.Section + ")") + " - " + Path.GetFileName(d.FileName ?? "") +
                                  (d == example ? " - in full below, as the format to follow" : ext == ".pdf" ? " - a PDF, not included (ask me for a page if you need it)" : ""));
                }
                sb.AppendLine();
                int budget = AllDocumentsChars;
                foreach (var d in docs)
                {
                    if (d == example || budget <= 500) continue;
                    var body = DocumentText(d, Math.Min(DocumentChars, budget));
                    if (body.Length == 0) continue;
                    budget -= body.Length;
                    sb.AppendLine("### " + d.Title + " (its text)");
                    sb.AppendLine(body);
                    sb.AppendLine();
                }
            }

            // ---- the format to follow: the HMI's own page, whole --------------------------
            if (example != null)
            {
                sb.AppendLine("## The format to follow: the HMI's " + (stepByStep ? "step-by-step page" : "help page") + " for this alarm (complete HTML)");
                sb.AppendLine("\"" + example.Title + "\" - the HMI made it from the data above" + (stepByStep ? " and shows it when the alarm's help is opened" : "") + ":");
                sb.AppendLine();
                sb.AppendLine("```html");
                sb.AppendLine(exampleHtml);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            // ---- what is asked --------------------------------------------------------
            sb.AppendLine("## What I ask you");
            sb.AppendLine();
            sb.AppendLine("1. Write me helper documents for THIS alarm on THIS machine, in " + language + ": " +
                          (example != null ? "complete HTML files in the same format, style and colours as the HMI's " + (stepByStep ? "step-by-step" : "help") + " page above" : "complete HTML files, clear and printable") +
                          " - one file for each of these readers:");
            sb.AppendLine("   1. Operator - quick, safe checks from the panel and around the machine, no tools, nothing opened: what to look at on the HMI (which page, which lamp, which value), what to try once, when to stop and call maintenance.");
            sb.AppendLine("   2. Maintenance technician - the diagnosis with a multimeter and the machine's drawings: the real device numbers given above (outputs, relays, coils, valves, hoses, sensors, cards, wires, sheets), what each reading means, the likely causes in order of probability, how to fix each.");
            sb.AppendLine("   3. Automation engineer - what to check in the PLC program (the bits and words above, the conditions, the timings), the drive or module parameters, the HMI's alarm settings, and how to keep it from coming back.");
            sb.AppendLine("2. Each file" + (example != null ? ", like the page above," : "") + " has: the alarm as its title with its device; the signal path (button - command bit - output - relay - coil - valve - hoses - cylinder - sensors or position, or for an axis: module - amplifier - motor - encoder) as a numbered chain linking to its steps; a safety box; " +
                          "then numbered steps, each with one question, what a good and a bad result means and which step comes next; simple inline SVG drawings where they help; " +
                          "at the end a short table symptom - cause - remedy. One self-contained file each - no external files, scripts or fonts - so it opens in any browser and prints well.");
            sb.AppendLine("3. Start each file with the most likely cause given the device states at the alarm and what the HMI saw. " +
                          "Rules: safety first (stop the machine, lockout / tagout, air pressure released before touching a cylinder, never bridge a safety circuit). " +
                          "Use the names and numbers exactly as given here - do not invent device numbers; where something is not given, say what to look for and where.");
            sb.AppendLine("4. If one answer cannot hold all three files in full, write the operator's first and go on with the next when I say \"next\". " +
                          "If something important is missing, list your questions for me after the files.");
            sb.AppendLine("5. Then stay with me: I will ask about the parts I do not understand, or tell you what I find at the machine - explain that part in more detail, step by step and in simple words for my level, ask me what I see, and guide me to the cause.");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("(Made by Kaban HMI on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                          ". Answer in " + language + ".)");
            return sb.ToString();
        }

        private static string Seconds(double s)
        {
            return s.ToString("0.#", CultureInfo.InvariantCulture) + " s";
        }

        private static string State(string address, int v)
        {
            bool bit = Regex.IsMatch(address ?? "", @"^(X|Y|M|L|B|F|SM|S)\d", RegexOptions.IgnoreCase) || address.Contains(".");
            return bit ? (v != 0 ? "ON" : "OFF") : v.ToString(CultureInfo.InvariantCulture);
        }

        private static string AlarmLine(AlarmItem a)
        {
            var dev = !string.IsNullOrWhiteSpace(a.Device) ? a.Device : a.Address ?? "";
            return a.TriggerTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " - " + a.ShownCode +
                   (dev.Length > 0 ? " (" + dev + ", station " + a.Station + ")" : "") + " - \"" + (a.Description ?? "").Trim() + "\" - " + a.Severity +
                   (a.Generated ? " - raised by the HMI" + (string.IsNullOrWhiteSpace(a.Detail) ? "" : ": " + a.Detail.Trim()) : "");
        }

        /// <summary>The cylinder the HMI watches for this alarm: by its error bit, command or output, else by its unit's name.</summary>
        private static AlarmGeneration.Cylinder CylinderOf(int station, string address, AlarmItem item, AlarmHelpLinks.Link link)
        {
            try
            {
                var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var k in new[] { address, item != null ? item.Device : null, item != null ? item.Address : null })
                    if (!string.IsNullOrWhiteSpace(k)) keys.Add(k.Trim());
                var list = AlarmGeneration.Cylinders.Where(c => c.Station == station).ToList();
                return list.FirstOrDefault(c => c.Sides.Any(s => keys.Contains(s.ErrorAddress ?? "") || keys.Contains(s.Code ?? "") || keys.Contains(s.Drive ?? ""))) ??
                       (link != null && link.Unit.Length > 0 ? list.FirstOrDefault(c => string.Equals(c.Unit, link.Unit, StringComparison.OrdinalIgnoreCase)) : null);
            }
            catch { return null; }
        }

        private static AlarmGeneration.Axis AxisOf(int station, string address, AlarmHelpLinks.Link link)
        {
            try
            {
                return AlarmGeneration.Axes.FirstOrDefault(x => x.Station == station &&
                    (string.Equals((x.ErrorAddress ?? "").Trim(), address, StringComparison.OrdinalIgnoreCase) ||
                     (link != null && link.ServoAlarm.Length > 0 && string.Equals(x.ServoAlarm, link.ServoAlarm, StringComparison.OrdinalIgnoreCase))));
            }
            catch { return null; }
        }

        private static void AppendCode(StringBuilder sb, Devices devices, int station, string what, AxisCodeKind kind, string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;
            word = word.Trim();
            var v = Safe(() => ReadNow(station, word), (int?)null);
            string now = v.HasValue ? (v.Value == 0 ? "0 (no code now)" : AxisCodeHelp.Format(kind, v.Value & 0xFFFF) + " - " + Safe(() => AxisCodeHelp.Describe(kind, v.Value & 0xFFFF))) : "(not read)";
            sb.AppendLine("- " + what + ": " + word + ", now " + now);
            devices.Add(word, what);
            if (v.HasValue) devices.Now[word.ToUpperInvariant()] = v.Value;
        }

        private static void AppendMovement(StringBuilder sb, TC.MovementInfo mv, Devices devices)
        {
            sb.AppendLine("- The movement that failed: \"" + TC.MovementName(mv) + "\"" +
                          (string.IsNullOrEmpty(mv.OtherName) ? "" : " (the other button: \"" + mv.OtherName + "\")") +
                          (string.IsNullOrEmpty(mv.Side) ? "" : ", button " + mv.Side));
            string name = TC.MovementName(mv);
            if (mv.Command != null && !string.IsNullOrWhiteSpace(mv.Command.Address))
            {
                sb.AppendLine("- Its button's command bit: " + mv.Command.Address);
                devices.Add(mv.Command, name + ": its button's command bit");
            }
            if (mv.Output != null && !string.IsNullOrWhiteSpace(mv.Output.Address))
            {
                sb.AppendLine("- Its valve output: " + Device(mv.Output));
                devices.Add(mv.Output, name + ": its valve output");
            }
            if (mv.OtherCommand != null && !string.IsNullOrWhiteSpace(mv.OtherCommand.Address)) devices.Add(mv.OtherCommand, "the other movement's command bit");
            if (mv.OtherOutput != null && !string.IsNullOrWhiteSpace(mv.OtherOutput.Address))
            {
                sb.AppendLine("- The other movement's output: " + Device(mv.OtherOutput));
                devices.Add(mv.OtherOutput, "the other movement's valve output");
            }
            if (mv.Feedback.Count > 0)
            {
                sb.AppendLine("- Sensors that must come ON at its end: " + string.Join(", ", mv.Feedback.Select(Device)));
                foreach (var f in mv.Feedback) devices.Add(f, name + ": must be ON at its end");
            }
            if (mv.OppositeFeedback.Count > 0)
            {
                sb.AppendLine("- Sensors of the other end (go OFF as it leaves): " + string.Join(", ", mv.OppositeFeedback.Select(Device)));
                foreach (var f in mv.OppositeFeedback) devices.Add(f, name + ": the start sensor, must go OFF");
            }
            if (!string.IsNullOrWhiteSpace(mv.Position))
            {
                sb.AppendLine("- Position reading: " + mv.Position);
                devices.Add(mv.Position, "the position reading");
            }
            if (!string.IsNullOrWhiteSpace(mv.Error)) devices.Add(mv.Error, name + ": its error bit");
            if (mv.Conditions.Count > 0)
                sb.AppendLine("- Its button waits for: " + string.Join("; ", mv.Conditions.Select(c => c.Name + (string.IsNullOrEmpty(c.Rule) ? "" : " (" + c.Rule + ")") +
                                                                                      (c.Met.HasValue ? (c.Met.Value ? " - met" : " - NOT met") : ""))));
        }

        /// <summary>One movement as the HMI watches it: what drives it, what confirms it, its error bit and alarm.</summary>
        private static void AppendSide(StringBuilder sb, AlarmGeneration.Cylinder c, AlarmGeneration.CylinderSide s, Devices devices)
        {
            string name = s.Name.Length > 0 ? s.Name : s.Side;
            var parts = new List<string>();
            var mv = s.Movement;
            if (mv != null && mv.Command != null && !string.IsNullOrWhiteSpace(mv.Command.Address))
            {
                parts.Add("command bit " + mv.Command.Address);
                devices.Add(mv.Command, name + ": its button's command bit");
            }
            if (s.Drive.Length > 0)
            {
                parts.Add(s.DriveOn ? "driven by output " + s.Drive + " ON" : "a spring return: output " + s.Drive + " going OFF");
                devices.Add(s.Drive, s.DriveOn ? name + ": its valve output" : name + ": the spring returns it when this output goes OFF");
            }
            if (s.OtherDrive.Length > 0)
            {
                parts.Add("the other coil's output " + s.OtherDrive + (s.KeepsAfterDrop ? " (a 5/2 double valve: a short pulse is enough)" : ""));
                devices.Add(s.OtherDrive, null);
            }
            if (s.MustOn.Count > 0)
            {
                parts.Add("must be ON at its end: " + string.Join(", ", s.MustOn.Select(x => x.Name.Length > 0 && x.Name != x.Address ? x.Address + " " + x.Name : x.Address)));
                foreach (var x in s.MustOn) devices.Add(x.Address, name + ": must be ON at its end", x.Name);
            }
            if (s.MustOff.Count > 0)
            {
                parts.Add("must be OFF at its end: " + string.Join(", ", s.MustOff.Select(x => x.Name.Length > 0 && x.Name != x.Address ? x.Address + " " + x.Name : x.Address)));
                foreach (var x in s.MustOff) devices.Add(x.Address, name + ": the start sensor, must go OFF", x.Name);
            }
            if (s.Position.Length > 0)
            {
                parts.Add("position " + s.Position + (s.ByPosition ? " (confirmed by the position moving and settling)" : ""));
                devices.Add(s.Position, "the position reading");
            }
            if (s.ErrorAddress.Length > 0)
            {
                var d = Safe(() => AlarmManager.FindByStationAndAddress(c.Station, s.ErrorAddress), (AlarmDefinition)null);
                string alarm = d != null ? LanguageManager.GetStringIn("EN", d.TranslationKey) ?? "" : "";
                double wait = d != null && d.GenerationDelay.HasValue && d.GenerationDelay.Value > 0 ? d.GenerationDelay.Value : AlarmGeneration.For(c.ModuleId).DelaySeconds;
                parts.Add("error bit " + s.ErrorAddress + (alarm.Length > 0 ? " - alarm \"" + alarm + "\"" : "") + ", the HMI waits " + Seconds(wait));
                devices.Add(s.ErrorAddress, name + ": its error bit");
            }
            sb.AppendLine("  - " + (s.Side == "A" ? "Left" : "Right") + " button (" + s.Side + ") \"" + name + "\": " + string.Join("; ", parts));
        }

        /// <summary>A device's lines from the machine's drawings (the wiring table): its line, its air line, its card, the drawing's words.</summary>
        private static List<string> WiringLines(int station, string address)
        {
            var lines = new List<string>();
            try
            {
                var p = WiringTable.Find(station, address);
                var drives = p == null ? WiringTable.OutputFor(station, address) : null;
                if (p == null && drives != null) { lines.Add("drives output " + drives.Address + " (on the drawings; its line below)"); return lines; }
                if (p == null) return lines;
                var words = new List<string>();
                if (p.Text.Length > 0) words.Add("\"" + p.Text + "\"");
                if (p.Read.Length > 0 && !p.Read.Equals(address, StringComparison.OrdinalIgnoreCase)) words.Add("read at " + p.Read);
                if (p.Commands.Count > 0) words.Add("commanded by " + string.Join(", ", p.Commands));
                lines.Add("drawing: " + (words.Count > 0 ? string.Join(", ", words) + " - " : "") + p.Electrical());
                if (p.Card.Length > 0) lines.Add("PLC card: " + p.Card);
                var air = p.Pneumatic();
                if (air.Length > 0) lines.Add("air: " + air + (p.Hose.Length > 0 ? " - this output puts air into hose " + p.Hose : ""));
                if (p.Measure.Length > 0) lines.Add("measured by: " + p.Measure);
                if (p.Pressure.Length > 0) lines.Add("pressure: " + p.Pressure);
            }
            catch { }
            return lines;
        }

        /// <summary>The document whose page is the format to follow: the step-by-step page, else the first HTML one.</summary>
        private static TC.HelpDocument ExampleDocument(IList<TC.HelpDocument> docs)
        {
            if (docs == null) return null;
            return docs.FirstOrDefault(d => d != null && d.StepByStep) ??
                   docs.FirstOrDefault(d => d != null && Regex.IsMatch(d.FileName ?? "", @"\.html?$", RegexOptions.IgnoreCase));
        }

        /// <summary>A help document's page as HTML: whole up to max characters; beyond it without its drawings, then cut.</summary>
        public static string ExampleHtml(TC.HelpDocument doc, int max)
        {
            try
            {
                var path = TC.ExtractSection(doc);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".html" && ext != ".htm") return "";
                var html = File.ReadAllText(path).TrimStart('﻿').Trim();
                if (html.Length > max)
                    html = Regex.Replace(html, @"<svg\b.*?</svg>", "<!-- a drawing, left out here -->", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (html.Length > max) html = html.Substring(0, max) + "\n<!-- [... shortened] -->";
                return html;
            }
            catch { return ""; }
        }

        private static string Device(TC.MovementInfo.Device d)
        {
            var s = d.Address + (string.IsNullOrWhiteSpace(d.Name) ? "" : " \"" + d.Name.Trim() + "\"");
            var states = new List<string>();
            if (d.AtAlarm.HasValue) states.Add("at the alarm " + (d.AtAlarm.Value != 0 ? "ON" : "OFF"));
            if (d.Now.HasValue) states.Add("now " + (d.Now.Value != 0 ? "ON" : "OFF"));
            return states.Count > 0 ? s + " (" + string.Join(", ", states) + ")" : s;
        }

        private static T Safe<T>(Func<T> f, T fallback) { try { var v = f(); return v == null ? fallback : v; } catch { return fallback; } }

        private static string Safe(Func<string> f) { try { return f() ?? ""; } catch { return ""; } }

        private static string MachineName(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId)) return "";
            var en = LanguageManager.GetStringIn("EN", "Machine_" + moduleId) ?? "";
            var here = LanguageManager.GetStringIn(LanguageManager.CurrentLanguage, "Machine_" + moduleId) ?? "";
            if (en.Length == 0) return here;
            return here.Length > 0 && here != en ? en + " (\"" + here + "\" on the panel)" : en;
        }

        private static string StationName(int station)
        {
            try
            {
                var st = PlcManager.Instance.GetStation(station);
                return st != null ? (st.Name ?? "") : "";
            }
            catch { return ""; }
        }

        /// <summary>The PLC's cards and modules the electrical drawing lists for a station ("U4 FX5U-64MT-ESS").</summary>
        public static List<string> PlcModules(int station)
        {
            var list = new SortedDictionary<int, string>();
            try
            {
                foreach (var p in WiringTable.Points(station))
                {
                    var m = Regex.Match(p.Card ?? "", @"^U(\d+)\s+([^,\s]+)");
                    int n;
                    if (m.Success && int.TryParse(m.Groups[1].Value, out n) && !list.ContainsKey(n)) list[n] = "U" + n + " " + m.Groups[2].Value;
                    var meas = Regex.Match(p.Measure ?? "", @"\b(U(\d+))\s+([A-Z0-9][\w\-/]*)\s+channel", RegexOptions.IgnoreCase);
                    if (meas.Success && int.TryParse(meas.Groups[2].Value, out n) && !list.ContainsKey(n)) list[n] = meas.Groups[1].Value + " " + meas.Groups[3].Value;
                }
            }
            catch { }
            return list.Values.ToList();
        }

        /// <summary>The notes the users left on a unit (comments, helpers, translations), as lines.</summary>
        private static List<string> NotesOn(string unit)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(unit)) return lines;
            try
            {
                foreach (var n in NotesStore.All.Where(n => (n.ControlName ?? "").IndexOf(unit, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    string what = n.Kind == NoteKind.Comment ? "comment" : n.Kind == NoteKind.Helper ? "helper" : "translation";
                    string body = n.Kind == NoteKind.Translation ? string.Join(" / ", n.Texts.Select(t => t.Lang + ": " + t.Text)) : (n.Text ?? "");
                    if (n.Kind == NoteKind.Helper && !string.IsNullOrEmpty(n.FileName)) body += " (file: " + n.FileName + ")";
                    lines.Add(n.Time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ", " + n.Author + " (" + n.Level + "), " + what +
                              " on \"" + n.ControlName + "\": " + body.Trim());
                }
            }
            catch { }
            return lines;
        }

        /// <summary>A help document's own words as plain text (its tailored or generated page), at most max characters; "" for a PDF.</summary>
        public static string DocumentText(TC.HelpDocument doc, int max)
        {
            try
            {
                var path = TC.ExtractSection(doc);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
                var ext = Path.GetExtension(path).ToLowerInvariant();
                string raw = File.ReadAllText(path);
                string text;
                if (ext == ".html" || ext == ".htm") text = HtmlToText(raw);
                else if (ext == ".txt" || ext == ".md" || ext == ".csv") text = raw;
                else return "";
                text = Regex.Replace(text, @"[ \t]+", " ");
                text = Regex.Replace(text, @"\n\s*\n\s*\n+", "\n\n").Trim();
                if (text.Length > max) text = text.Substring(0, Math.Max(0, max - 20)).TrimEnd() + "\n[... shortened]";
                return text;
            }
            catch { return ""; }
        }

        /// <summary>A page's text: no scripts, styles or drawings; a line per block, a cell per " | ".</summary>
        public static string HtmlToText(string html)
        {
            var s = Regex.Replace(html ?? "", @"<(script|style|svg|head)\b.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<!--.*?-->", "", RegexOptions.Singleline);
            s = Regex.Replace(s, @"</t[dh]>\s*<t[dh][^>]*>", " | ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<span class=""arrow""[^>]*>.*?</span>", " -> ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            s = Regex.Replace(s, @"</(span|a)>", " ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<(br|/p|/div|/li|/tr|/h[1-6]|/table|/ul|/ol)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<li\b[^>]*>", "\n- ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<h[1-6]\b[^>]*>", "\n## ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<[^>]+>", "");
            s = WebUtility.HtmlDecode(s);
            s = string.Join("\n", s.Split('\n').Select(l => l.Trim()));
            return s;
        }

        /// <summary>
        /// Where a copied prompt is also saved as a text file - a chat that takes
        /// no text this long takes the file as an attachment: Documents\Kaban HMI\AI prompts.
        /// </summary>
        public static string SaveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Kaban HMI", "AI prompts");

        /// <summary>The file a prompt was saved to last (tests), or null.</summary>
        public static string LastSaved { get; private set; }

        /// <summary>
        /// Saves the prompt as a .txt file named by what it is about (its alarm's
        /// code, or <paramref name="name"/>) and the time; null when it could not.
        /// </summary>
        public static string Save(string prompt, string name = null)
        {
            try
            {
                string code = name;
                if (string.IsNullOrEmpty(code))
                {
                    var m = Regex.Match(prompt ?? "", @"^- Code: (\S+)", RegexOptions.Multiline);
                    code = m.Success ? m.Groups[1].Value : "alarm";
                }
                foreach (var ch in Path.GetInvalidFileNameChars()) code = code.Replace(ch, '_');
                Directory.CreateDirectory(SaveFolder);
                var path = Path.Combine(SaveFolder, "AI prompt " + code + " " + DateTime.Now.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture) + ".txt");
                File.WriteAllText(path, prompt ?? "", new UTF8Encoding(true));
                // keep the folder small: the newest 50
                foreach (var old in new DirectoryInfo(SaveFolder).GetFiles("AI prompt *.txt").OrderByDescending(f => f.LastWriteTimeUtc).Skip(50))
                    try { old.Delete(); } catch { }
                return path;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not save the AI prompt: " + ex.Message);
                return null;
            }
        }

        /// <summary>What a prompt is about: an alarm (its helpers), or the HMI itself (the caption bar's ?).</summary>
        public enum Subject { Alarm, Hmi }

        /// <summary>Puts an alarm's prompt on the clipboard (and in a file) and says so; false when the clipboard was not free.</summary>
        public static bool Copy(IWin32Window owner, string prompt)
        {
            return Deliver(owner, prompt, Subject.Alarm, null);
        }

        /// <summary>
        /// "Create AI prompt" (<paramref name="chat"/> null) and "Ask AI for help"
        /// (the chat to open): the prompt saved as a file and put on the clipboard,
        /// then - asking - the chat opened in the browser to paste it into; the user
        /// told what to do next. False when the clipboard was not free.
        /// </summary>
        public static bool Deliver(IWin32Window owner, string prompt, Subject subject, AiChats.Chat chat)
        {
            LastSaved = Save(prompt, subject == Subject.Hmi ? "HMI" : null);
            LastOpened = null;
            try
            {
                Clipboard.SetText(prompt ?? "");
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not copy the AI prompt: " + ex.Message);
                LastMessage = LanguageManager.GetString("Msg_AiPromptFailed", "The prompt could not be put on the clipboard - try again.");
                if (CopiedNotice != null) CopiedNotice(owner);
                else MessageBox.Show(owner, LastMessage, LanguageManager.GetString("Hlp_AiPrompt", "AI prompt"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            bool opened = false;
            if (chat != null)
            {
                opened = AiChats.Open(chat);
                if (opened) LastOpened = chat;
            }
            AuditLogger.Log((chat != null ? "asked " + chat.Name + (opened ? "" : " (the browser did not open)") + " with" : "copied") +
                            " the AI prompt for " + (subject == Subject.Hmi ? "the HMI" : "an alarm") + " (" + (prompt ?? "").Length + " characters)" +
                            (LastSaved != null ? ", saved as " + LastSaved : "") + ".");
            LastMessage = chat == null ? CopiedMessage(LastSaved, subject) : AskedMessage(chat, opened, LastSaved);
            if (CopiedNotice != null) CopiedNotice(owner);
            else
                MessageBox.Show(owner, LastMessage, LanguageManager.GetString("Hlp_AiPrompt", "AI prompt"), MessageBoxButtons.OK,
                                chat != null && !opened ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            return true;
        }

        /// <summary>The message shown last (tests).</summary>
        public static string LastMessage { get; private set; }
        /// <summary>The chat opened last, or null (tests).</summary>
        public static AiChats.Chat LastOpened { get; private set; }

        /// <summary>What the operator is told after copying: paste it, what comes back, and the file for a chat that says it is too long.</summary>
        public static string CopiedMessage(string saved, Subject subject = Subject.Alarm)
        {
            var msg = subject == Subject.Hmi
                ? LanguageManager.GetString("Msg_HmiPromptCopied",
                    "The AI prompt is on the clipboard: the HMI, its machines, where you are and its whole manual. " +
                    "Open any AI chat (Claude, ChatGPT, Gemini, Grok...) and paste it (Ctrl+V): it tells you what it can help you with on this panel - then ask it anything about the HMI.")
                : LanguageManager.GetString("Msg_AiPromptCopied",
                    "The AI prompt is on the clipboard: everything about this alarm, its unit and the machine, with the HMI's step-by-step page as the format to follow. " +
                    "Open any AI chat (Claude, ChatGPT, Gemini, Grok...) and paste it (Ctrl+V): it writes you helper pages for the operator, the maintenance technician and the automation engineer - " +
                    "then ask it about any part you do not understand, or tell it what you find at the machine.");
            return msg + SavedLine(saved);
        }

        /// <summary>What the operator is told after asking: paste it into the chat now open - or, when the browser did not open, use the file elsewhere.</summary>
        public static string AskedMessage(AiChats.Chat chat, bool opened, string saved)
        {
            string msg = opened
                ? string.Format(LanguageManager.GetString("Msg_AiAsked",
                    "{0} is opening in the browser. The prompt is on the clipboard: paste it into the chat (Ctrl+V) and send it."), chat.Name)
                : string.Format(LanguageManager.GetString("Msg_AiAskFailed",
                    "{0} could not be opened ({1}) - this panel may have no browser or no internet. The prompt is on the clipboard and in the file below: use it in an AI chat on another computer or a phone."),
                    chat.Name, chat.Link);
            return msg + SavedLine(saved);
        }

        private static string SavedLine(string saved)
        {
            return string.IsNullOrEmpty(saved) ? "" : "\n\n" + string.Format(LanguageManager.GetString("Msg_AiPromptSaved",
                "If the chat says the text is too long, attach this file instead (it is the same prompt):\n{0}"), saved);
        }

        /// <summary>Replaces the message after copying (tests).</summary>
        public static Action<IWin32Window> CopiedNotice;
    }
}
