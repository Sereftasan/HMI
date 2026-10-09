using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// AN AI PROMPT FOR THE HMI ITSELF (9 October: the caption bar's "?" -
    /// "Ask AI for help ... create a prompt for the AI explaining the HMI entirely,
    /// our manual attached, just like the cylinder movement troubleshooting
    /// helper's prompt"). What the program is, the machines it runs and their
    /// controllers, who is signed in and where they are on the screen, what is
    /// going on now (the alarms, a controller not answering), and the HMI's manual
    /// whole - as it opens from the "?" menu, with this panel's machines and their
    /// own chapters. What is asked comes last: be this panel's helper, at the
    /// reader's level and in the screen's language, from the manual - say first
    /// what it can help with, then answer, step by step, and explain again when a
    /// part is not clear.
    /// </summary>
    public static class HmiAiPrompt
    {
        /// <summary>The manual (HTML) in full up to this many characters; beyond it, without its drawings, then cut.</summary>
        public const int ManualChars = 200000;

        /// <summary>Where the user is: filled by the main window (tests fill their own).</summary>
        public sealed class Context
        {
            /// <summary>The page on show, as the menu names it: "Processing Center / Manual Movement / Cutting".</summary>
            public string Place = "";
            /// <summary>The side page open beside it, or "".</summary>
            public string SidePage = "";
            /// <summary>The window's screen scale ("100%").</summary>
            public string Scale = "";
        }

        public static string Build(Context ctx)
        {
            ctx = ctx ?? new Context();
            string lang = LanguageManager.CurrentLanguage ?? "EN";
            string language = AlarmAiPrompt.LanguageName(lang);
            var level = SecurityManager.CurrentLevel;
            string levelName = level == SecurityLevel.None ? "nobody signed in" : level.ToString();
            var machines = Safe(() => Kaban.UI.Settings.HmiManual.RunningMachines(), new List<Kaban.UI.Settings.HmiManual.ManualMachine>());
            string manual = ManualHtml();
            var sb = new StringBuilder();

            // ---- what this is -------------------------------------------------------------
            sb.AppendLine("# Help me use an industrial machine's HMI (operator panel)");
            sb.AppendLine();
            sb.AppendLine("I work with the HMI described below: Kaban HMI, the operator panel program of Kaban Makina's aluminium / PVC profile processing machines. " +
                          "Below is the program and the machines it runs, who I am on it and where I am on the screen, what is going on now" +
                          (manual.Length > 0 ? ", and the HMI's manual whole (HTML), with this panel's machines in it" : "") +
                          ". What I ask you is at the end, under \"What I ask you\". Answer in " + language + ".");
            sb.AppendLine();

            // ---- the program and its machines ------------------------------------------------
            sb.AppendLine("## The program");
            var exe = Assembly.GetEntryAssembly() ?? typeof(HmiAiPrompt).Assembly;
            sb.AppendLine("- Kaban HMI: " + Path.GetFileNameWithoutExtension(exe.Location) + " " + exe.GetName().Version +
                          ", HMI library " + typeof(HmiAiPrompt).Assembly.GetName().Version + " - a Windows program on the machine's panel PC, for touch screens and mouse alike.");
            sb.AppendLine("- Languages on the panel: " + string.Join(", ", LanguageManager.AvailableLanguages.Select(l => l + " (" + AlarmAiPrompt.LanguageName(l) + ")")) +
                          "; on screen now: " + language + ". The manual below is in English: the words on my screen are in " + language + ".");
            if (!string.IsNullOrEmpty(ctx.Scale)) sb.AppendLine("- Screen scale now: " + ctx.Scale + ".");
            if (machines.Count > 0)
            {
                sb.AppendLine("- The machines it runs:");
                foreach (var m in machines)
                    sb.AppendLine("  - " + m.Label + (m.IsMain ? " - the main machine" : " - a sub-machine mounted with it") + ", PLC station " + m.Station +
                                  (m.Pages.Count > 0 ? "; its pages: " + string.Join(", ", m.Pages) : ""));
            }
            var stations = Safe(() => PlcManager.Instance.Stations.OrderBy(s => s.StationId).ToList(), new List<PlcManager.PlcStationInfo>());
            if (stations.Count > 0)
            {
                sb.AppendLine("- Its controllers (Mitsubishi PLCs):");
                foreach (var s in stations)
                {
                    bool down = Safe(() => ConnectionMonitor.IsKnownDown(s.StationId), false);
                    sb.AppendLine("  - Station " + s.StationId + (string.IsNullOrWhiteSpace(s.Name) ? "" : " (" + s.Name.Trim() + ")") +
                                  (string.IsNullOrWhiteSpace(s.OwnerModuleId) ? "" : ", of " + s.OwnerModuleId) + (down ? " - NOT ANSWERING now" : ""));
                }
            }
            sb.AppendLine();

            // ---- me ---------------------------------------------------------------------------
            sb.AppendLine("## Me, and where I am");
            sb.AppendLine("- Signed in: " + (level == SecurityLevel.None ? "nobody (the panel's own view: what anyone may see)"
                                                                         : (SecurityManager.CurrentUser ?? "") + ", level " + levelName) + ".");
            sb.AppendLine("- The levels: Operator (runs the machine: manual movements, alarms, the pages to see), Maintenance (also settings, parameters, " +
                          "calibration, everyone's preferences), Admin (everything: users, the PLC Monitor, the controllers, the pages' settings). " +
                          "What my level may not do is grey or not shown on my screen.");
            if (!string.IsNullOrWhiteSpace(ctx.Place)) sb.AppendLine("- The page on my screen: " + ctx.Place.Trim());
            if (!string.IsNullOrWhiteSpace(ctx.SidePage)) sb.AppendLine("- Open beside it (the side panel): " + ctx.SidePage.Trim());
            sb.AppendLine();

            // ---- now ----------------------------------------------------------------------------
            var alarms = Safe(() => AlarmManager.ActiveAlarms.Where(a => a != null).ToList(), new List<AlarmItem>());
            sb.AppendLine("## Now");
            if (alarms.Count == 0) sb.AppendLine("- No alarm on the list.");
            else
            {
                sb.AppendLine("- Alarms on the list (" + alarms.Count + "):");
                foreach (var a in alarms.Take(30))
                {
                    var dev = !string.IsNullOrWhiteSpace(a.Device) ? a.Device : a.Address ?? "";
                    sb.AppendLine("  - " + a.TriggerTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " - " + a.ShownCode +
                                  (dev.Length > 0 ? " (" + dev + ", station " + a.Station + ")" : "") + " - \"" + (a.Description ?? "").Trim() + "\" - " + a.Severity +
                                  (a.Generated ? " - raised by the HMI" : ""));
                }
                sb.AppendLine("  (Each alarm's own help - and an AI prompt for it alone - is in its Help column on the Alarms page.)");
            }
            sb.AppendLine("- The prompt was made at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ".");
            sb.AppendLine();

            // ---- the manual -------------------------------------------------------------------
            if (manual.Length > 0)
            {
                sb.AppendLine("## The HMI's manual (complete HTML, for this panel's machines)");
                sb.AppendLine();
                sb.AppendLine("```html");
                sb.AppendLine(manual);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            // ---- what is asked ----------------------------------------------------------------
            sb.AppendLine("## What I ask you");
            sb.AppendLine();
            sb.AppendLine("1. Be my helper for THIS HMI, in " + language + ", at my level (" + levelName + "). " +
                          "First, in a few short lines, say what you can help me with on this panel - given my level and the page I am on - and ask me what I want to do or what is not clear to me.");
            sb.AppendLine("2. Answer from the manual above. Name the pages, tabs, buttons and fields as the HMI shows them; my screen is in " + language +
                          ", the manual in English - give the English name in brackets when you are not sure of the word on my screen. " +
                          "Step by step: one action a step, and what I should see after it.");
            sb.AppendLine("3. When something needs a higher level than mine, say so and who to ask (Maintenance or an Admin). " +
                          "Safety first: never tell me to bypass a guard, an emergency stop or a condition the HMI checks; before anything moves the machine, say what moves.");
            sb.AppendLine("4. Where the manual does not say, say so - do not invent pages, buttons, settings or device numbers. For a machine fault, point me to the alarm's own help and AI prompt.");
            sb.AppendLine("5. When I ask for it, write me a short helper page for a task - one complete, self-contained HTML file in the manual's style (no external files or scripts) - that I can keep on the panel.");
            sb.AppendLine("6. Stay with me: when I do not understand a part, explain it again, more simply and in more detail, for my level; ask me what I see on the screen.");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("(Made by Kaban HMI on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ". Answer in " + language + ".)");
            return sb.ToString();
        }

        /// <summary>
        /// The manual as the "?" menu opens it - this panel's machines and their own
        /// chapters filled in - without the local base address (a path on the panel
        /// says nothing to an AI). Whole up to ManualChars; beyond, without its
        /// drawings, then cut. "" when there is none.
        /// </summary>
        public static string ManualHtml()
        {
            try
            {
                var path = Kaban.UI.Settings.HmiManual.PrepareManual();
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
                var html = File.ReadAllText(path).TrimStart('﻿').Trim();
                html = Regex.Replace(html, @"<base\b[^>]*>", "", RegexOptions.IgnoreCase);
                if (html.Length > ManualChars)
                    html = Regex.Replace(html, @"<svg\b.*?</svg>", "<!-- a drawing, left out here -->", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (html.Length > ManualChars) html = html.Substring(0, ManualChars) + "\n<!-- [... shortened] -->";
                return html;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not read the manual for the AI prompt: " + ex.Message);
                return "";
            }
        }

        private static T Safe<T>(Func<T> f, T fallback) { try { var v = f(); return v == null ? fallback : v; } catch { return fallback; } }
    }
}
