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
            /// <summary>What the page on show shows, one line a control (DescribePage) - filled by the main window.</summary>
            public List<string> OnScreen = new List<string>();
            /// <summary>The machine's bar above the page's tabs (Manual Movement's axes' speed and jog steps), as OnScreen; empty without one.</summary>
            public List<string> Bar = new List<string>();
        }

        /// <summary>
        /// The user levels as both briefings tell them (26 October: the prompts brought up to the
        /// program as it is - coupling, Cylinder FW/Back, the translations, the PLC data backups,
        /// the menus' greyed entries).
        /// </summary>
        public const string Levels =
            "The levels: Operator (runs the machine: the manual movements, the alarms, notes), Maintenance (also the machine parameters, calibration, " +
            "coupling units to move together, moving a cylinder back and forth to test it, the translations, a backup of a PLC's data, everyone's preferences), " +
            "Admin (everything: the users, the PLC connections, the PLC Monitor, writing a backup into a PLC, the simple motion modules' parameters). " +
            "What a level may not do is not shown, or shown greyed - and its tooltip says why.";

        /// <summary>How many controls of the page on show are described, at most.</summary>
        public const int OnScreenMax = 150;

        /// <summary>
        /// The page on show as the briefing tells it (14 October: "create more detailed document
        /// as the AI prompt ... enough for the AI to start chatting with the user and give them
        /// proper answers"): its headings, units, axes, motors, buttons, values, lamps and options,
        /// in the order they are on the page (top to bottom, left to right), with what each shows
        /// now and the device it works. Hidden controls are left out; at most OnScreenMax lines.
        /// </summary>
        public static List<string> DescribePage(System.Windows.Forms.Control page)
        {
            var lines = new List<string>();
            if (page == null) return lines;
            try { Describe(page, lines, 0); } catch (Exception ex) { AuditLogger.Log("could not describe the page for the AI prompt: " + ex.Message); }
            if (lines.Count > OnScreenMax) { lines = lines.Take(OnScreenMax).ToList(); lines.Add("(... more on the page)"); }
            return lines;
        }

        private static string Tr(string key) { return string.IsNullOrWhiteSpace(key) ? "" : LanguageManager.GetString(key, key).Trim(); }

        /// <summary>What a unit is doing besides its own buttons: coupled to others, moving back and forth.</summary>
        private static string UnitState(Kaban.UI.Controls.CylinderUnitHMI u)
        {
            if (u == null) return "";
            var s = "";
            try
            {
                if (UnitCoupling.IsCoupled(u)) s += " - COUPLED: a press of its button presses the same button of the units coupled with it";
                if (UnitCycling.IsCycling(u)) s += " - MOVING BACK AND FORTH by itself (Automatic ON/OFF)";
            }
            catch { }
            return s;
        }

        private static string Addr(string address) { return string.IsNullOrWhiteSpace(address) ? "" : " (" + address.Trim() + ")"; }

        private static string Commands(Kaban.UI.Controls.ButtonHMI b)
        {
            if (b != null && b.HmiFunction == Kaban.UI.Controls.HmiButtonFunction.CylinderForwardBack)
                return " (a function of the HMI, no PLC bit: while on, a cylinder's button starts it moving back and forth by itself - " + (b.HmiFunctionOn ? "ON now" : "off now") + ")";
            if (b == null || b.BitActions == null) return "";
            var a = b.BitActions.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Address)).Select(x => x.Address.Trim() + " " + x.Action).ToList();
            return a.Count == 0 ? "" : " (" + string.Join(", ", a) + ")";
        }

        private static void Describe(System.Windows.Forms.Control parent, List<string> lines, int depth)
        {
            if (depth > 12 || lines.Count > OnScreenMax) return;
            var kids = parent.Controls.Cast<System.Windows.Forms.Control>().Where(c => c.Visible)
                             .OrderBy(c => c.Top / 20).ThenBy(c => c.Left).ToList();
            foreach (var c in kids)
            {
                if (lines.Count > OnScreenMax) return;
                var inv = c as Kaban.UI.Controls.InverterUnitHMI;
                var unit = c as Kaban.UI.Controls.CylinderUnitHMI;
                var axisUnit = c as Kaban.UI.Controls.AxisUnitHMI;
                if (inv != null)
                {
                    var code = inv.InverterFaultCode;
                    lines.Add("Motor (inverter) \"" + Tr(inv.TitleTextKey) + "\": " + UnitButtons(inv) +
                              (string.IsNullOrWhiteSpace(inv.PositionAddress) ? "" : "; its value " + inv.PositionDisplay.Text.Trim() + " (" + inv.PositionAddress + ")") +
                              (string.IsNullOrWhiteSpace(inv.RunLampAddress) ? "" : "; run lamp " + (inv.RunLamp.IsOn ? "ON" : "off") + " (" + inv.RunLampAddress + ")") +
                              (code != 0 ? "; INVERTER FAULT " + AxisCodeHelp.Describe(AxisCodeKind.InverterFault, code) + " - not shown on the unit: both its buttons blink red, its ? has the fault's help" : "") +
                              UnitState(inv));
                    continue;
                }
                if (axisUnit != null)
                {
                    var u = axisUnit.Unit;
                    lines.Add("Axis \"" + Tr(u.TitleTextKey) + "\": " + UnitButtons(u) + "; position " + u.PositionDisplay.Text.Trim() +
                              (string.IsNullOrWhiteSpace(u.PositionAddress) ? "" : " (" + u.PositionAddress + ")") + UnitState(u));
                    continue;
                }
                // a pneumatic positioning or an encoder cylinder's settings: one row, its unit then its sections (26 October)
                var cylRow = c as Kaban.UI.Controls.CylinderRowHMI;
                if (cylRow != null)
                {
                    var ru = cylRow.Unit;
                    var parts = new List<string>();
                    foreach (var key in cylRow.SectionKeys.Where(k => k != Kaban.UI.Controls.CylinderRowHMI.ManualKey))
                    {
                        var field = cylRow.FieldOf(key);
                        var cmd = cylRow.CommandOf(key);
                        if ((field == null || !field.Visible || string.IsNullOrWhiteSpace(field.Address)) && (cmd == null || !cmd.Visible)) continue;
                        parts.Add(cylRow.SectionTitle(key) + (field != null && field.Visible && !string.IsNullOrWhiteSpace(field.Address) ? " " + field.Text.Trim() + Addr(field.Address) : "") +
                                  (cmd != null && cmd.Visible && cmd.BitActions != null && cmd.BitActions.Any(a => a != null && !string.IsNullOrWhiteSpace(a.Address))
                                      ? ", its button \u25B6| beside it" + Commands(cmd) + (cmd.Enabled ? "" : " - greyed now") : ""));
                    }
                    lines.Add((cylRow is Kaban.UI.Controls.PneumaticPositioningHMI ? "Pneumatic positioning (a cylinder the PLC drives to a position) \""
                                                                                    : "Cylinder with an encoder, its settings row \"") + Tr(cylRow.TitleTextKey) + "\": " +
                              (ru != null ? UnitButtons(ru) + (string.IsNullOrWhiteSpace(ru.PositionAddress) ? "" : "; position " + ru.PositionDisplay.Text.Trim() + Addr(ru.PositionAddress)) : "") +
                              (parts.Count > 0 ? "; " + string.Join("; ", parts) : "") + UnitState(ru));
                    continue;
                }
                if (unit != null)
                {
                    lines.Add("Unit \"" + Tr(unit.TitleTextKey) + "\": " + UnitButtons(unit) +
                              (string.IsNullOrWhiteSpace(unit.PositionAddress) || !unit.PositionDisplay.Visible ? "" : "; position " + unit.PositionDisplay.Text.Trim() + " (" + unit.PositionAddress + ")") +
                              UnitState(unit));
                    continue;
                }
                var row = c as Kaban.UI.Controls.AxisSettingHMI;
                if (row != null)
                {
                    var kidsOfRow = row.Controls.Cast<System.Windows.Forms.Control>().ToList();
                    var pos = kidsOfRow.OfType<Kaban.UI.Controls.NumericDisplayHMI>().FirstOrDefault(d => d.Visible && !string.IsNullOrWhiteSpace(row.PositionAddress) &&
                                                                                                     string.Equals(d.Address, row.PositionAddress, StringComparison.OrdinalIgnoreCase));
                    var current = kidsOfRow.OfType<Kaban.UI.Controls.MeterHMI>().FirstOrDefault(m => m.Visible && !string.IsNullOrWhiteSpace(row.CurrentAddress) &&
                                                                                               string.Equals(m.Address, row.CurrentAddress, StringComparison.OrdinalIgnoreCase));
                    var codes = kidsOfRow.OfType<Kaban.UI.Controls.HexCodeHMI>().Where(h => h.Visible && h.HasHelp).Select(h => h.CodeKind.ToString()).ToList();
                    lines.Add("Axis row \"" + Tr(row.AxisNameKey) + "\" (axis " + row.HMIAxisIndex + ")" +
                              (pos != null ? ": position " + pos.Text.Trim() + Addr(row.PositionAddress) : "") +
                              (current != null ? "; motor current " + current.DisplayValue.ToString("0.0", CultureInfo.InvariantCulture) + " %" + Addr(row.CurrentAddress) : "") +
                              (codes.Count > 0 ? "; A DRIVE CODE SHOWS (" + string.Join(", ", codes) + ") - its cell opens its help" : ""));
                    continue;
                }
                var speed = c as Kaban.UI.Controls.SpeedOverrideHMI;
                if (speed != null)
                {
                    lines.Add("Speed slider \"" + LanguageManager.GetString(speed.TranslationKey ?? "", speed.FallbackText ?? "").Trim().TrimEnd(':') + "\": " +
                              (speed.Current.HasValue ? speed.Current.Value + " %" : "not read yet") + Addr(speed.Address) + (speed.Enabled ? "" : " - greyed now"));
                    continue;
                }
                var combo = c as Kaban.UI.Controls.ComboBoxHMI;
                if (combo != null)
                {
                    lines.Add("Choice \"" + Tr(combo.DescriptionText).TrimEnd(':') + "\": " + (combo.Text ?? "").Trim() + Addr(combo.Address) + (combo.Enabled ? "" : " - greyed now"));
                    continue;
                }
                var heading = c as Kaban.UI.Controls.LabelHMI;
                if (heading != null) { if (heading.Text.Trim().Length > 0) lines.Add("Heading: " + heading.Text.Trim()); continue; }
                var btn = c as Kaban.UI.Controls.ButtonHMI;
                if (btn != null)
                {
                    lines.Add("Button \"" + ButtonWord(btn.DisplayText, btn.DisplayText) + "\"" + Commands(btn) + (btn.Enabled ? "" : " - greyed now") +
                              (string.IsNullOrWhiteSpace(btn.Lamp1Address) ? "" : "; its lamp " + btn.Lamp1Address));
                    continue;
                }
                var num = c as Kaban.UI.Controls.BaseNumericHMI;
                if (num != null)
                {
                    lines.Add((num is Kaban.UI.Controls.NumericInputHMI ? "Value to enter" : "Value") + " \"" + Tr(num.DescriptionText).TrimEnd(':') + "\": " + num.Text.Trim() +
                              (string.IsNullOrWhiteSpace(num.Address) ? "" : " (" + num.Address + ")"));
                    continue;
                }
                var lamp = c as Kaban.UI.Controls.LampHMI;
                if (lamp != null)
                {
                    lines.Add("Lamp \"" + Tr(lamp.DescriptionText) + "\": " + (lamp.IsOn ? "ON" : "off") + (string.IsNullOrWhiteSpace(lamp.Address) ? "" : " (" + lamp.Address + ")"));
                    continue;
                }
                var box = c as Kaban.UI.Controls.CheckBoxHMI;
                if (box != null)
                {
                    lines.Add("Option \"" + Tr(box.DescriptionText) + "\": " + (box.Checked ? "on" : "off") + (string.IsNullOrWhiteSpace(box.Address) ? "" : " (" + box.Address + ")"));
                    continue;
                }
                var group = c as Kaban.UI.Controls.GroupBoxHMI;
                if (group != null && Tr(group.TitleTextKey).Length > 0) lines.Add("Group \"" + Tr(group.TitleTextKey) + "\":");
                if (c.HasChildren) Describe(c, lines, depth + 1);
            }
        }

        /// <summary>A button's caption as words: the arrows the units draw are named ("◀" is "back (left arrow)").</summary>
        private static string ButtonWord(string caption, string shown)
        {
            var t = (string.IsNullOrWhiteSpace(caption) ? shown ?? "" : caption).Trim();
            switch (t)
            {
                case "\u25C0": return "◀ back (left arrow)";
                case "\u25B6": return "▶ forward (right arrow)";
                case "\u25B2": return "▲ up";
                case "\u25BC": return "▼ down";
                case "\u21E4": return "⇤ open";
                case "\u21E5": return "⇥ close";
                default: return t;
            }
        }

        private static string UnitButtons(Kaban.UI.Controls.CylinderUnitHMI u)
        {
            var parts = new List<string>();
            foreach (var side in new[] { "A", "B" })
            {
                var b = side == "A" ? u.ButtonA : u.ButtonB;
                var cmd = side == "A" ? u.CommandAAddress : u.CommandBAddress;
                if (string.IsNullOrWhiteSpace(cmd)) continue;
                var lamps = new[] { side == "A" ? u.Lamp1AAddress : u.Lamp1BAddress, side == "A" ? u.Lamp2AAddress : u.Lamp2BAddress }
                            .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                parts.Add((side == "A" ? "left" : "right") + " button \"" + ButtonWord(Tr(side == "A" ? u.ButtonATextKey : u.ButtonBTextKey), b.Text) + "\" (" + cmd.Trim() + ")" +
                          (lamps.Count > 0 ? " with its sensor lamp " + string.Join(", ", lamps) : "") + (b.Enabled ? "" : " - greyed now") +
                          (b.AlertBlink ? " - its border BLINKS RED (an error)" : ""));
            }
            return parts.Count == 0 ? "no buttons" : string.Join("; ", parts);
        }

        public static string Build(Context ctx)
        {
            ctx = ctx ?? new Context();
            string lang = LanguageManager.CurrentLanguage ?? "EN";
            string language = AlarmAiPrompt.LanguageName(lang);
            var level = SecurityManager.CurrentLevel;
            string levelName = level == SecurityLevel.None ? "nobody signed in" : level.ToString();
            var machines = Safe(() => Kaban.UI.Settings.HmiManual.RunningMachines(), new List<Kaban.UI.Settings.HmiManual.ManualMachine>());
            string manual = ManualText();
            var sb = new StringBuilder();

            // ---- what this is -------------------------------------------------------------
            sb.AppendLine("# Help me use an industrial machine's HMI (operator panel)");
            sb.AppendLine();
            sb.AppendLine("I work with the HMI described below: Kaban HMI, the operator panel program of Kaban Makina's aluminium / PVC profile processing machines. " +
                          "This briefing, made by the HMI itself, holds the program and the machines it runs, who I am on it, where I am on the screen and what that page shows now, what is going on now" +
                          (manual.Length > 0 ? ", and the HMI's manual whole (its text), with this panel's machines in it" : "") +
                          ". Read all of it: what I ask you is at the end, under \"What I ask you\" - we are going to talk. Answer in " + language + ".");
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
                    bool off = Safe(() => PlcConnectionSettings.RunsInactive(s.StationId), false);
                    sb.AppendLine("  - Station " + s.StationId + (string.IsNullOrWhiteSpace(s.Name) ? "" : " (" + s.Name.Trim() + ")") +
                                  (string.IsNullOrWhiteSpace(s.OwnerModuleId) ? "" : ", of " + s.OwnerModuleId) +
                                  (off ? " - NOT ACTIVE (switched off on HMI Settings > PLC Connections: not read; a machine with no active controller has no pages)"
                                       : down ? " - NOT ANSWERING now" : ""));
                }
            }
            sb.AppendLine();

            // ---- me ---------------------------------------------------------------------------
            sb.AppendLine("## Me, and where I am");
            sb.AppendLine("- Signed in: " + (level == SecurityLevel.None ? "nobody (the panel's own view: what anyone may see)"
                                                                         : (SecurityManager.CurrentUser ?? "") + ", level " + levelName) + ".");
            sb.AppendLine("- " + Levels);
            if (!string.IsNullOrWhiteSpace(ctx.Place)) sb.AppendLine("- The page on my screen: " + ctx.Place.Trim());
            if (!string.IsNullOrWhiteSpace(ctx.SidePage)) sb.AppendLine("- Open beside it (the side panel): " + ctx.SidePage.Trim());
            sb.AppendLine();
            if ((ctx.OnScreen != null && ctx.OnScreen.Count > 0) || (ctx.Bar != null && ctx.Bar.Count > 0))
            {
                sb.AppendLine("## What that page shows now (top to bottom, left to right; device numbers in brackets)");
                if (ctx.Bar != null && ctx.Bar.Count > 0)
                {
                    sb.AppendLine("- The machine's bar above the page's tabs (the same over every tab):");
                    foreach (var l in ctx.Bar) sb.AppendLine("  - " + l);
                }
                if (ctx.OnScreen != null) foreach (var l in ctx.OnScreen) sb.AppendLine("- " + l);
                sb.AppendLine();
            }

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
            var coupled = Safe(() => UnitCoupling.All.Where(u => u != null).ToList(), new List<Kaban.UI.Controls.CylinderUnitHMI>());
            if (coupled.Count > 0)
                sb.AppendLine("- Units coupled to move together now (the caption bar's blue chain): " + string.Join(", ", coupled.Select(u => "\"" + Tr(u.TitleTextKey) + "\"")) +
                              " - a press of one's button presses the same button of each.");
            if (Safe(() => UnitCycling.Active, false))
            {
                var cycling = Safe(() => UnitCycling.Cycling.Where(u => u != null).ToList(), new List<Kaban.UI.Controls.CylinderUnitHMI>());
                sb.AppendLine("- Automatic ON/OFF is ON: a press of a cylinder's button starts it moving back and forth by itself" +
                              (cycling.Count > 0 ? "; moving now: " + string.Join(", ", cycling.Select(u => "\"" + Tr(u.TitleTextKey) + "\"")) : "") + ".");
            }
            sb.AppendLine("- The prompt was made at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ".");
            sb.AppendLine();

            // ---- the manual -------------------------------------------------------------------
            if (manual.Length > 0)
            {
                sb.AppendLine("## The HMI's manual (its text, whole, for this panel's machines)");
                sb.AppendLine();
                sb.AppendLine(manual);
                sb.AppendLine();
            }

            // ---- what is asked ----------------------------------------------------------------
            sb.AppendLine("## What I ask you");
            sb.AppendLine();
            sb.AppendLine("1. Be my helper for THIS HMI, in " + language + ", at my level (" + levelName + "), in a conversation. " +
                          "Start now, in a few short lines: what the page I am on is for and what I can do there at my level" +
                          (alarms.Count > 0 ? "; that there are alarms on the list (name the first) and that you can walk me through it" : "") +
                          "; then ask me what I want to do or what is not clear to me. Keep each answer short - it is read on the panel or a phone beside the machine.");
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
        /// chapters filled in - without the panel's own folder in its links and without
        /// its script (a path on the panel, a page's code, say nothing to an AI). Whole up to ManualChars; beyond, without its
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
                html = Regex.Replace(html, @"<script\b.*?</script>\s*", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                try
                {
                    var root = new Uri(TroubleshootingCatalog.HelpFolder.TrimEnd('\\', '/') + Path.DirectorySeparatorChar).AbsoluteUri;
                    html = html.Replace(root, "");
                }
                catch { }
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

        /// <summary>
        /// The manual as the briefing carries it (14 October): its TEXT - headings, paragraphs,
        /// lists, a table's cells between " | " - without its markup, styles, drawings and
        /// script: what an AI reads to answer from, at a fraction of the HTML's length.
        /// Whole up to ManualChars, then cut. "" when there is none.
        /// </summary>
        public static string ManualText()
        {
            var html = ManualHtml();
            if (html.Length == 0) return "";
            var text = AlarmAiPrompt.HtmlToText(html);
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"\n\s*\n\s*\n+", "\n\n").Trim();
            if (text.Length > ManualChars) text = text.Substring(0, ManualChars) + "\n[... shortened]";
            return text;
        }

        private static T Safe<T>(Func<T> f, T fallback) { try { var v = f(); return v == null ? fallback : v; } catch { return fallback; } }
    }
}
