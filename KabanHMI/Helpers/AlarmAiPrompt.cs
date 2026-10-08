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
    /// list. A click puts on the clipboard everything an AI chat needs to write a
    /// helper for THIS alarm on THIS machine - the machine and its control system,
    /// the HMI and where the unit is on it, the alarm (its text, code, device, when,
    /// what the HMI saw), the unit (its valve, its feedback, its devices and their
    /// states, its wiring as the help pages know it), what the HMI's own help
    /// documents already say, and the notes the panel's users left on the unit -
    /// then what to write. Pasted into any AI (Claude, ChatGPT, Gemini, Grok ...)
    /// as it is, nothing else to add; the operator can go on asking there.
    ///
    /// ONE DOCUMENT IN THREE LEVELS rather than three documents: the operator's
    /// quick checks first (nothing opened, no tools), then maintenance's diagnosis
    /// with a meter and the drawings' numbers, then the automation engineer's (the
    /// PLC program, the drive's parameters). Whoever reads it stops where their
    /// level ends, and the levels follow one another as a fault is chased down - so
    /// they belong in one place, not in three files that drift apart.
    /// </summary>
    public static class AlarmAiPrompt
    {
        /// <summary>How much of a help document's own text is put in (characters): enough for the steps, not a book.</summary>
        public const int DocumentChars = 9000;
        /// <summary>All the documents' texts together, at most.</summary>
        public const int AllDocumentsChars = 24000;

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
            var sb = new StringBuilder();

            // ---- what is asked --------------------------------------------------------
            sb.AppendLine("# Help me with an alarm on an industrial machine");
            sb.AppendLine();
            sb.AppendLine("I work at the machine described below. The alarm below is on its HMI (the operator panel). " +
                          "Using everything below, write me a practical helper document for THIS alarm on THIS machine, " +
                          "in " + LanguageName(lang) + ".");
            sb.AppendLine();
            sb.AppendLine("Write it in three levels, each a section of its own, so each reader stops where their level ends:");
            sb.AppendLine("1. Operator - quick, safe checks from the panel and around the machine, no tools, nothing opened (what to look at on the HMI, what to try once, when to call maintenance).");
            sb.AppendLine("2. Maintenance technician - the diagnosis with a multimeter and the machine's drawings: the real device numbers given below (outputs, relays, coils, valves, hoses, sensors, cards, wires, sheets), what each reading means, the likely causes in order of probability, how to fix each.");
            sb.AppendLine("3. Automation engineer - what to check in the PLC program, the drive or module parameters, timings and settings, and how to prevent it coming back.");
            sb.AppendLine();
            sb.AppendLine("Rules: safety first (stop the machine, lockout / tagout, air pressure released before touching a cylinder, never bridge a safety circuit). " +
                          "Use the names and numbers exactly as given here - do not invent device numbers; where something is not given, say what to look for. " +
                          "Start each level with the most likely cause given what the HMI saw. Short numbered steps, each with what a good and a bad result means. " +
                          "End with a short table: symptom - cause - remedy. If something important is missing, list your questions for me at the end.");
            sb.AppendLine();

            // ---- the machine ----------------------------------------------------------
            sb.AppendLine("## The machine");
            string moduleId = AlarmGeneration.ModuleOfStation(station);
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
            sb.AppendLine("- The HMI (Kaban HMI) reads the PLC every scan. Its pages: Manual Movement (each cylinder is a unit with two buttons - one per movement - " +
                          "and lamps for its sensors), Machine Parameters (one row per servo axis: jog buttons, position, current, limits, reference, its alarm codes), " +
                          "Alarms (this list, with help documents for each alarm), and side pages (Inputs / Outputs, Axis Monitor, PLC Monitor).");
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
                sb.AppendLine("- Came at: " + item.TriggerTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                sb.AppendLine("- Still active: " + (AlarmManager.IsOnList(item) ? "yes" : "no (acknowledged or gone)"));
            }
            if (address.Length > 0) sb.AppendLine("- PLC device of the alarm: " + address + " (station " + station + ")");
            if (item != null && item.Generated)
            {
                sb.AppendLine("- Raised by the HMI itself (not by the PLC program): the HMI timed the movement or read the drive's code.");
                var detail = item.DetailText != null ? Safe(item.DetailText) : item.Detail;
                if (!string.IsNullOrWhiteSpace(detail)) sb.AppendLine("- What the HMI saw fail: " + detail.Trim());
            }
            else if (item != null && !string.IsNullOrWhiteSpace(item.Detail))
                sb.AppendLine("- Detail: " + item.Detail.Trim());
            if (item != null && item.Snapshot != null && item.Snapshot.Count > 0)
                sb.AppendLine("- Device states the scan before it came (1 = ON): " +
                              string.Join(", ", item.Snapshot.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).Select(k => k.Key + "=" + k.Value)));
            sb.AppendLine();

            // ---- the unit -------------------------------------------------------------
            if (link != null)
            {
                sb.AppendLine("## The unit it belongs to");
                sb.AppendLine("- Name on the HMI: " + (link.Unit.Length > 0 ? link.Unit : "(none)"));
                if (link.Kind == TC.ErrorType.CylinderMovement)
                {
                    sb.AppendLine("- Kind: pneumatic cylinder");
                    sb.AppendLine("- Valve: " + TC.Describe((TC.ValveType)TC.CanonicalValve(link.Valve)));
                    sb.AppendLine("- Feedback: " + TC.Describe((TC.FeedbackType)TC.CanonicalFeedback(link.Feedback)));
                    var mv = link.CurrentMovement();
                    if (mv != null) AppendMovement(sb, mv);
                }
                else if (link.Kind == TC.ErrorType.ServoFault)
                {
                    sb.AppendLine("- Kind: servo axis (simple motion module + servo amplifier)");
                    if (link.ServoAlarm.Length > 0) sb.AppendLine("- Servo alarm code word: " + link.ServoAlarm);
                    if (link.AxisAlarm.Length > 0) sb.AppendLine("- Axis error code word: " + link.AxisAlarm);
                    if (link.AxisWarning.Length > 0) sb.AppendLine("- Axis warning code word: " + link.AxisWarning);
                }
                else sb.AppendLine("- Kind: " + link.Kind);
                sb.AppendLine();
            }

            // ---- the notes the panel's users left on it -------------------------------
            var notes = NotesOn(link != null ? link.Unit : "");
            if (notes.Count > 0)
            {
                sb.AppendLine("## Notes the machine's users left on this unit");
                foreach (var n in notes) sb.AppendLine("- " + n);
                sb.AppendLine();
            }

            // ---- what the HMI's help already says --------------------------------------
            if (docs != null && docs.Count > 0)
            {
                sb.AppendLine("## The HMI's own help for this alarm");
                foreach (var d in docs) sb.AppendLine("- " + d.Title + (string.IsNullOrEmpty(d.Section) ? "" : " (section " + d.Section + ")") + " - " + Path.GetFileName(d.FileName ?? ""));
                sb.AppendLine();
                int budget = AllDocumentsChars;
                foreach (var d in docs)
                {
                    if (budget <= 500) break;
                    var body = DocumentText(d, Math.Min(DocumentChars, budget));
                    if (body.Length == 0) continue;
                    budget -= body.Length;
                    sb.AppendLine("### " + d.Title);
                    sb.AppendLine(body);
                    sb.AppendLine();
                }
            }

            sb.AppendLine("---");
            sb.AppendLine("(Made by Kaban HMI on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                          ". Answer in " + LanguageName(lang) + ".)");
            return sb.ToString();
        }

        private static void AppendMovement(StringBuilder sb, TC.MovementInfo mv)
        {
            sb.AppendLine("- The movement that failed: \"" + TC.MovementName(mv) + "\"" +
                          (string.IsNullOrEmpty(mv.OtherName) ? "" : " (the other button: \"" + mv.OtherName + "\")") +
                          (string.IsNullOrEmpty(mv.Side) ? "" : ", button " + mv.Side));
            if (mv.Command != null && !string.IsNullOrWhiteSpace(mv.Command.Address))
                sb.AppendLine("- Its button's command bit: " + mv.Command.Address);
            if (mv.Output != null && !string.IsNullOrWhiteSpace(mv.Output.Address))
                sb.AppendLine("- Its valve output: " + Device(mv.Output));
            if (mv.OtherOutput != null && !string.IsNullOrWhiteSpace(mv.OtherOutput.Address))
                sb.AppendLine("- The other movement's output: " + Device(mv.OtherOutput));
            if (mv.Feedback.Count > 0)
                sb.AppendLine("- Sensors that must come ON at its end: " + string.Join(", ", mv.Feedback.Select(Device)));
            if (mv.OppositeFeedback.Count > 0)
                sb.AppendLine("- Sensors of the other end (go OFF as it leaves): " + string.Join(", ", mv.OppositeFeedback.Select(Device)));
            if (!string.IsNullOrWhiteSpace(mv.Position)) sb.AppendLine("- Position reading: " + mv.Position);
            if (mv.Conditions.Count > 0)
                sb.AppendLine("- Its button waits for: " + string.Join("; ", mv.Conditions.Select(c => c.Name + (string.IsNullOrEmpty(c.Rule) ? "" : " (" + c.Rule + ")") +
                                                                                      (c.Met.HasValue ? (c.Met.Value ? " - met" : " - NOT met") : ""))));
        }

        private static string Device(TC.MovementInfo.Device d)
        {
            var s = d.Address + (string.IsNullOrWhiteSpace(d.Name) ? "" : " \"" + d.Name.Trim() + "\"");
            var states = new List<string>();
            if (d.AtAlarm.HasValue) states.Add("at the alarm " + (d.AtAlarm.Value != 0 ? "ON" : "OFF"));
            if (d.Now.HasValue) states.Add("now " + (d.Now.Value != 0 ? "ON" : "OFF"));
            return states.Count > 0 ? s + " (" + string.Join(", ", states) + ")" : s;
        }

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

        /// <summary>Puts the prompt on the clipboard and says so; false when the clipboard was not free.</summary>
        public static bool Copy(IWin32Window owner, string prompt)
        {
            try
            {
                Clipboard.SetText(prompt ?? "");
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not copy the AI prompt: " + ex.Message);
                MessageBox.Show(owner, LanguageManager.GetString("Msg_AiPromptFailed", "The prompt could not be put on the clipboard - try again."),
                                LanguageManager.GetString("Hlp_AiPrompt", "AI prompt"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            AuditLogger.Log("copied the AI prompt for an alarm (" + (prompt ?? "").Length + " characters).");
            if (CopiedNotice != null) CopiedNotice(owner);
            else
                MessageBox.Show(owner, LanguageManager.GetString("Msg_AiPromptCopied",
                    "The AI prompt is on the clipboard. Open any AI chat (Claude, ChatGPT, Gemini, Grok...) and paste it (Ctrl+V): it has everything the AI needs to write you a helper for this alarm - add your own notes if you wish. You can go on asking it questions there."),
                    LanguageManager.GetString("Hlp_AiPrompt", "AI prompt"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        /// <summary>Replaces the message after copying (tests).</summary>
        public static Action<IWin32Window> CopiedNotice;
    }
}
