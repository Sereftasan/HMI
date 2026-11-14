using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Kaban.Core.Helpers;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// THE TRANSLATIONS' AI PROMPT (HMI Settings > Translation, 🤖; 22 October: "a tool to use AI
    /// to translate the missing cells and to check the other translations - an AI prompt with the
    /// translation files and a description of each key, so the AI translates them best: not just
    /// the words, the best translation for each key's use in each language - and return the files"):
    /// the program and who reads its texts, the languages, the rules of the texts (their place on
    /// the screen, the placeholders, no ';'), every row asked about in its file - its key, WHERE IT
    /// IS SHOWN (TextUses: the page and the control, the menu, a message box, a tooltip ...) and its
    /// texts - then what to do and the files to give back: the same names, only the cells changed,
    /// a note on each row. TranslationAiAnswer reads them back.
    /// </summary>
    public static class TranslationAiPrompt
    {
        /// <summary>Translate the empty cells; or check every text (and translate the empty ones).</summary>
        public enum Mode { Missing, Check }

        /// <summary>Above this many keys the page asks first (an AI answers a few hundred at a time).</summary>
        public const int ManyKeys = 300;

        /// <summary>The file a owner's rows are given in, and read back from: CommonTranslations.csv, FA2010_Translations.csv.</summary>
        public static string FileNameOf(string owner)
        {
            return string.Equals(owner, TranslationEdits.General, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(owner)
                ? "CommonTranslations.csv" : Clean(owner).Replace(' ', '_') + "_Translations.csv";
        }

        /// <summary>Whose a file given back is (FileNameOf's names, "(1)" and such after them too); null: not one of them.</summary>
        public static string OwnerOfFile(string name)
        {
            var n = (name ?? "").Trim();
            n = n.Substring(Math.Max(n.LastIndexOf('\\'), n.LastIndexOf('/')) + 1);
            if (n.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 4);
            n = n.Trim();
            if (n.StartsWith("CommonTranslations", StringComparison.OrdinalIgnoreCase)) return TranslationEdits.General;
            var m = Regex.Match(n, @"^(?<o>[A-Za-z0-9.]+)[_ \-](Translations|Alarms)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups["o"].Value : null;
        }

        private static string Clean(string s) { return (s ?? "").Replace(";", ",").Replace("\r", " ").Replace("\n", " ").Trim(); }

        /// <summary>A text as the files write it: no ';', a new line as \r\n.</summary>
        public static string AsFile(string text)
        {
            return TranslationEdits.Clean(text).Replace("\r\n", "\\r\\n").Replace("\n", "\\n");
        }

        /// <summary>
        /// The prompt. <paramref name="textOf"/>: a row's text in a language as the table has it
        /// (typed texts too); <paramref name="uses"/>: where each key is shown (null: found here).
        /// </summary>
        public static string Build(IList<TranslationEdits.Row> rows, IList<string> languages, Mode mode,
                                   Func<TranslationEdits.Row, string, string> textOf, Dictionary<string, List<TextUses.Use>> uses = null)
        {
            rows = rows ?? new List<TranslationEdits.Row>();
            var langs = (languages ?? new List<string>()).Select(l => l.ToUpperInvariant()).Distinct().ToList();
            if (!langs.Contains("EN")) langs.Insert(0, "EN");
            else { langs.Remove("EN"); langs.Insert(0, "EN"); }
            if (uses == null) uses = TextUses.FindAll(rows.Select(r => r.Key));
            string panel = AlarmAiPrompt.LanguageName(LanguageManager.CurrentLanguage ?? "EN");
            var others = langs.Where(l => l != "EN").ToList();
            Func<IEnumerable<string>, string> names = ls => string.Join(", ", ls.Select(l => AlarmAiPrompt.LanguageName(l) + " (" + l + ")"));
            int missing = rows.Sum(r => langs.Count(l => (textOf(r, l) ?? "").Trim().Length == 0));

            var sb = new StringBuilder();
            sb.AppendLine(mode == Mode.Missing ? "# Translate the missing texts of an industrial machine's operator panel"
                                               : "# Check and improve the translations of an industrial machine's operator panel");
            sb.AppendLine();
            sb.AppendLine("I keep the translations of Kaban HMI, the operator panel program of Kaban Makina's aluminium / PVC profile processing machines " +
                          "(cutting, milling, drilling, end milling, welding and cleaning centres). This briefing, made by the program itself, holds its translation " +
                          "files - the rows I ask about - and where the program shows each text. Read all of it: what I ask you is at the end, under \"What I ask you\". " +
                          "Write your notes in " + panel + ".");
            sb.AppendLine();

            sb.AppendLine("## Who reads these texts");
            sb.AppendLine("- Machine operators, maintenance technicians and automation engineers, at the machine, on the panel PC's touch screen (and with a mouse). " +
                          "The machine runs with a Mitsubishi PLC; the panel shows its pages (manual movements, parameters, alarms, monitors), messages and help.");
            sb.AppendLine("- A text sits on a button, a tab, a menu entry, a table's heading, a label beside a value, a tooltip, a status line, a message box or an alarm. " +
                          "Where each one is shown is in the \"Use\" column of the files below - translate for that place, not word for word.");
            sb.AppendLine();

            sb.AppendLine("## The languages");
            sb.AppendLine("- English (EN) is the program's own language - the source of every text. The others: " + (others.Count > 0 ? names(others) : "none yet") + ".");
            sb.AppendLine("- On the screen a missing text shows in English.");
            sb.AppendLine();

            sb.AppendLine("## The rules of the texts");
            sb.AppendLine("- Say it the way a good machine manual and a good touch panel in that language say it: the industry's own terms (what machine builders and " +
                          "maintenance people use), clear, short, polite but direct. Not a literal translation of the English words.");
            sb.AppendLine("- Mind the place: a button, a tab or a menu entry is short (about as long as the English - it must fit), an action as the language's usual " +
                          "form for buttons; a table's heading or a label is a noun phrase; a tooltip, a message or a help line is a full sentence.");
            sb.AppendLine("- The same English term always the same word in a language (axis, clamp, unit, station, parameter, reference ...), across all the files.");
            sb.AppendLine("- Keep exactly: placeholders {0} {1} ... (in the order the sentence needs), \\r\\n (a new line - written like that), emoji and symbols, " +
                          "units (mm, mm/s, %, s, bar, °), PLC devices and codes (D100, X12@2, M330, B0), machine names (FA2010, H500 ...), product names.");
            sb.AppendLine("- Never a ';' inside a text - it separates the columns (write ',' instead). A text is one line of the file.");
            sb.AppendLine();

            sb.AppendLine("## The files");
            sb.AppendLine("Each file below is one of the program's translation files - only the rows I ask about - in its own format: `Key;Use;" + string.Join(";", langs) + "`. " +
                          "Key: the program's name for the text (never change it). Use: where the program shows it (a page and its control, the menu, a message box, a tooltip, " +
                          "an alarm ...) - for you, not a language. An empty cell: no text in that language yet." +
                          " " + rows.Count + " keys, " + missing + " empty cell(s).");
            sb.AppendLine();
            foreach (var g in rows.GroupBy(r => r.Owner, StringComparer.OrdinalIgnoreCase)
                                  .OrderBy(g => string.Equals(g.Key, TranslationEdits.General, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                bool general = string.Equals(g.Key, TranslationEdits.General, StringComparison.OrdinalIgnoreCase);
                sb.AppendLine("### " + FileNameOf(g.Key) + (general ? " - the program's own texts (every machine)" : " - machine " + g.Key + "'s texts (its pages, its units, its alarms)"));
                sb.AppendLine("```csv");
                sb.AppendLine("Key;Use;" + string.Join(";", langs));
                foreach (var r in g)
                {
                    List<TextUses.Use> u;
                    sb.Append(r.Key).Append(';').Append(Describe(r, uses.TryGetValue(r.Key, out u) ? u : null));
                    foreach (var l in langs) sb.Append(';').Append(AsFile(textOf(r, l)));
                    sb.AppendLine();
                }
                sb.AppendLine("```");
                sb.AppendLine();
            }

            sb.AppendLine("## What I ask you");
            if (mode == Mode.Missing)
                sb.AppendLine("1. Fill EVERY EMPTY CELL of " + (others.Count > 0 ? names(others) : "the languages") + ": the best text for that key's use in that language, " +
                              "as the rules say - from the English, the other languages' texts and the Use. Do not change the texts that are there.");
            else
            {
                sb.AppendLine("1. Check EVERY TEXT of " + (others.Count > 0 ? names(others) : "the languages") + " for its use: where it is wrong, unclear, too literal, " +
                              "too long for its place, not the industry's term, or not the same word as elsewhere, give the best text instead. Fill the empty cells too.");
                sb.AppendLine("2. The English only where it is wrong or unclear (rarely) - it is the source.");
            }
            sb.AppendLine((mode == Mode.Missing ? "2" : "3") + ". Give me back THE SAME FILES (the same names), with the columns `Key;" + string.Join(";", langs) + ";Note` - " +
                          "without the Use column, and ONLY THE ROWS YOU CHANGED OR FILLED, in them only the cells you changed or filled (leave the others empty: an empty cell " +
                          "changes nothing). Note: what you did in that row and why, in a few words in " + panel + ".");
            sb.AppendLine((mode == Mode.Missing ? "3" : "4") + ". As .csv files to download if you can make files; otherwise each file in its own ```csv block with its name " +
                          "on the line above it. If it is too long for one answer, stop at the end of a file, ask me to continue and go on when I say so.");
            sb.AppendLine("I import your files into the program: it shows me every text you changed, with your note, to check before it is saved.");
            return sb.ToString();
        }

        /// <summary>A key's Use: where the program shows it, in a few words (no ';').</summary>
        public static string Describe(TranslationEdits.Row row, List<TextUses.Use> uses)
        {
            var parts = new List<string>();
            foreach (var u in uses ?? new List<TextUses.Use>())
            {
                string p;
                switch (u.Kind)
                {
                    case TextUses.KindMenu: p = "the menu's page button"; break;
                    case TextUses.KindTab: p = "a tab of " + u.Place; break;
                    case TextUses.KindPage: p = (u.ByText ? "(its text) " : "") + u.What + " on " + u.Place; break;
                    case TextUses.KindAlarm: p = "an alarm's message (the alarm list, the alarm bar): " + u.What; break;
                    case TextUses.KindCode: p = CodeName(u.Code) + " in " + Path.GetFileNameWithoutExtension((u.Place ?? "").Replace('\\', '/')); break;
                    default: p = u.What; break;
                }
                p = Clean(p);
                if (p.Length > 0 && !parts.Contains(p, StringComparer.OrdinalIgnoreCase)) parts.Add(p);
            }
            if ((string.Equals(row.File, "Alarms.json", StringComparison.OrdinalIgnoreCase) || AlarmDefinitionLoader.TagOf(row.Key) != null) &&
                !parts.Any(p => p.StartsWith("an alarm's message", System.StringComparison.Ordinal)))
                parts.Insert(0, "an alarm's message (the alarm list, the alarm bar)");
            if (parts.Count == 0)
            {
                var guess = OfPrefix(row.Key);
                if (guess != null) parts.Add(guess);
            }
            var text = string.Join(" | ", parts.Take(4)) + (parts.Count > 4 ? " | +" + (parts.Count - 4) + " more" : "");
            return text.Length > 300 ? text.Substring(0, 297) + "..." : text;
        }

        private static string CodeName(string code)
        {
            switch (code)
            {
                case "Message": return "message box";
                case "Status": return "status line";
                case "Heading": return "table heading";
                case "Tooltip": return "tooltip";
                case "Menu": return "menu entry";
                default: return "text";
            }
        }

        /// <summary>What a key's prefix says it is (the program's naming), or null.</summary>
        internal static string OfPrefix(string key)
        {
            var at = (key ?? "").IndexOf('_');
            if (at <= 0) return null;
            switch (key.Substring(0, at).ToLowerInvariant())
            {
                case "btn": return "a button";
                case "lbl": return "a label";
                case "msg": return "a message";
                case "tip": return "a tooltip";
                case "col": case "lcol": return "a table's column heading";
                case "ttl": return "a window's title";
                case "tab": return "a tab";
                case "mnu": return "a menu entry";
                case "unit": return "a machine unit's name";
                case "alarm": return "an alarm's message";
                case "hlp": return "a help text";
                case "hdr": return "a heading";
                case "chk": return "a check box";
                case "scr": return "a page's name (the menu)";
                default: return null;
            }
        }
    }

    /// <summary>
    /// THE AI'S ANSWER read back (HMI Settings > Translation, 📥): its files, or its answer copied -
    /// the blocks of rows under a "Key;EN;TR ..." heading, each with the file name above it (a
    /// heading, a bold line) or the file's own name. Code fences, prose between the files and
    /// quoted cells are taken as they come.
    /// </summary>
    public static class TranslationAiAnswer
    {
        public sealed class Block
        {
            /// <summary>The file it is (CommonTranslations.csv ...), null: not named.</summary>
            public string FileName;
            public string[] Header = new string[0];
            public readonly List<string[]> Rows = new List<string[]>();
        }

        /// <summary>The blocks of a file or a text; <paramref name="name"/>: the file's own name (null for a text).</summary>
        public static List<Block> Parse(string name, string content)
        {
            var blocks = new List<Block>();
            string named = name != null && Regex.IsMatch(Path.GetFileName(name), @"\.csv$", RegexOptions.IgnoreCase) ? Path.GetFileName(name) : null;
            string pendingName = null;
            Block current = null;
            foreach (var raw in (content ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var s = raw.Trim().TrimStart('﻿');
                if (s.StartsWith("```", System.StringComparison.Ordinal)) { current = null; continue; }
                if (IsHeader(s))
                {
                    current = new Block { FileName = pendingName ?? named, Header = Split(s).Select(h => h.Trim()).ToArray() };
                    blocks.Add(current);
                    pendingName = null;
                    continue;
                }
                if (current != null && s.Contains(";")) { current.Rows.Add(Split(raw.Trim())); continue; }
                if (s.Length == 0) continue;
                current = null;
                var m = Regex.Match(s, @"([A-Za-z0-9_.\-]+\.csv)", RegexOptions.IgnoreCase);
                if (m.Success && !s.Contains(";")) pendingName = m.Groups[1].Value;
            }
            return blocks;
        }

        private static bool IsHeader(string s)
        {
            var f = Split(s);
            return f.Length >= 2 && string.Equals(f[0].Trim(), "Key", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A line's cells (';' between them; a cell in quotes may hold one, "" a quote).</summary>
        public static string[] Split(string line)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            line = line ?? "";
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else cell.Append(c);
                }
                else if (c == '"' && cell.ToString().Trim().Length == 0) { cell.Clear(); quoted = true; }
                else if (c == ';') { cells.Add(cell.ToString()); cell.Clear(); }
                else cell.Append(c);
            }
            cells.Add(cell.ToString());
            return cells.ToArray();
        }

        /// <summary>A text as the program keeps it (\r\n back to a new line).</summary>
        public static string FromFile(string cell)
        {
            return (cell ?? "").Trim().Replace("\\r\\n", "\r\n").Replace("\\n", "\n");
        }

        /// <summary>The placeholders of a text ({0}, {1:0.0} ...: their numbers).</summary>
        public static HashSet<string> Placeholders(string text)
        {
            var set = new HashSet<string>();
            foreach (Match m in Regex.Matches(text ?? "", @"\{(\d+)(?:[,:][^}]*)?\}")) set.Add(m.Groups[1].Value);
            return set;
        }
    }
}
