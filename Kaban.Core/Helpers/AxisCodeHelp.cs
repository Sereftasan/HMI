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
    /// <summary>The three codes an axis reports (Axis Settings, last three columns).</summary>
    public enum AxisCodeKind
    {
        /// <summary>A plain code word, printed the servo way ("50.2"), with no help behind it.</summary>
        None = 0,
        /// <summary>[Md.114] Servo alarm: the amplifier's display, "16.3" = alarm 16, detail 3.</summary>
        ServoAlarm = 1,
        /// <summary>[Md.23] Axis error No. of the Simple Motion module, "1904H".</summary>
        AxisError = 2,
        /// <summary>[Md.24] Axis warning No. of the Simple Motion module, "0900H".</summary>
        AxisWarning = 3,
        /// <summary>
        /// An inverter's fault: the DATA CODE its fault is read as (FR-A800 manual,
        /// 6.4 "List of fault displays"), in decimal - 16 is E.OC1 (H10). Answered by
        /// Inverter_Fault_Codes.html (6.5 "Causes and corrective actions"), with the
        /// whole manual after it (InverterUnitHMI, 14 October).
        /// </summary>
        InverterFault = 4
    }

    /// <summary>
    /// The help for ONE axis code: which document, which part of it, and what
    /// the code is called.
    ///
    /// THE PROBLEM
    /// -----------
    /// The servo manual and the motion module's manual answer every code - in
    /// hundreds of pages. An operator with a stopped axis and "16.3" on the
    /// screen was handed the whole servo PDF at page one. The answer to THEIR
    /// code is one table, and the program already knows the code.
    ///
    /// THE DOCUMENTS
    /// -------------
    /// tools/build_code_help.py reads the manuals' code tables once into HTML,
    /// one &lt;section class="code" data-code="16.3" data-group="16"
    /// data-kind="alarm" data-alarm="..." data-name="..."&gt; per code. Extract() copies the
    /// matching sections - the code's caption and its table of causes and
    /// remedies, nothing else - into a page of their own. Each section links back to its page of
    /// the original PDF, so the whole manual stays one click away.
    ///
    /// A servo DETAIL the manual does not list ("16.0") falls back to every
    /// detail of its alarm (16); a code the document does not have at all says
    /// so, with the link to the manual, rather than opening something unrelated.
    /// </summary>
    public static class AxisCodeHelp
    {
        public const string ServoAlarmDocument = "Servo_Alarm_Codes.html";
        public const string AxisErrorDocument = "Axis_Error_Codes.html";
        public const string AxisWarningDocument = "Axis_Warning_Codes.html";
        public const string InverterFaultDocument = "Inverter_Fault_Codes.html";

        /// <summary>
        /// The inverter's whole manual, the second entry of an inverter fault's help
        /// (14 October: "give the whole document as the second helper element, so there
        /// is one helper at least even when the code is not found on the list"): the PDF
        /// when it is in the Help folder, the manufacturer's link otherwise.
        /// </summary>
        public const string InverterManualFile = "FR-A800_Instruction_Manual.pdf";
        public const string InverterManualUrl = "https://dl.mitsubishielectric.com/dl/fa/document/manual/inv/ib0600503eng/ib0600503engn.pdf";

        /// <summary>The document answering a kind of code ("" for None).</summary>
        public static string DocumentFor(AxisCodeKind kind)
        {
            switch (kind)
            {
                case AxisCodeKind.ServoAlarm: return ServoAlarmDocument;
                case AxisCodeKind.AxisError: return AxisErrorDocument;
                case AxisCodeKind.AxisWarning: return AxisWarningDocument;
                case AxisCodeKind.InverterFault: return InverterFaultDocument;
                default: return "";
            }
        }

        /// <summary>
        /// The whole manual behind a kind of code, as a help entry - the inverter's
        /// (the local PDF, else the link to it); null for the axis codes, whose manuals
        /// their alarms list already (TroubleshootingCatalog.DocumentForType).
        /// </summary>
        public static TroubleshootingCatalog.HelpDocument ManualDocument(AxisCodeKind kind, string unit = "")
        {
            if (kind != AxisCodeKind.InverterFault) return null;
            var title = LanguageManager.GetString("Hlp_InverterManual", "Inverter manual (FR-A800)") +
                        (string.IsNullOrEmpty(unit) ? "" : " - " + unit);
            var local = new TroubleshootingCatalog.HelpDocument { FileName = InverterManualFile, Title = title };
            if (local.Exists) return local;
            return new TroubleshootingCatalog.HelpDocument { FileName = InverterManualUrl, Title = title };
        }

        /// <summary>
        /// A code as the manual prints it. Zero is "" - no fault is the normal
        /// state, and a column of zeros is noise to read past.
        ///
        /// Servo: 0x163 is "16.3" - the amplifier's display, the last hex digit
        /// being the detail. Axis error and warning: 0x1904 is "1904H", the
        /// module manual's form. Printed the servo way it read "190.4", which is
        /// in no manual at all.
        /// </summary>
        public static string Format(AxisCodeKind kind, int raw)
        {
            if (raw == 0) return "";
            if (raw < 0) return "-";
            // the inverter's data code as the manual's table gives it: decimal
            if (kind == AxisCodeKind.InverterFault) return (raw & 0xFFFF).ToString(CultureInfo.InvariantCulture);
            if (kind == AxisCodeKind.AxisError || kind == AxisCodeKind.AxisWarning)
                return (raw & 0xFFFF).ToString("X4") + "H";
            string hex = raw.ToString("X");
            if (hex.Length == 1) return "0." + hex;
            return hex.Substring(0, hex.Length - 1) + "." + hex.Substring(hex.Length - 1);
        }

        /// <summary>The code's section key in its document: "16.3", "1904".</summary>
        public static string Key(AxisCodeKind kind, int raw)
        {
            if (raw <= 0) return "";
            if (kind == AxisCodeKind.InverterFault) return (raw & 0xFFFF).ToString(CultureInfo.InvariantCulture);
            if (kind == AxisCodeKind.AxisError || kind == AxisCodeKind.AxisWarning)
                return (raw & 0xFFFF).ToString("X4");
            // The alarm number is two digits on the display: 0x0E61 is E6.1.
            return ((raw >> 4) & 0xFF).ToString("X2") + "." + (raw & 0xF).ToString("X");
        }

        /// <summary>The section group: the servo ALARM ("16") of a detail; the code itself otherwise.</summary>
        public static string GroupKey(AxisCodeKind kind, int raw)
        {
            var key = Key(kind, raw);
            if (kind == AxisCodeKind.ServoAlarm && key.Length > 2) return key.Substring(0, 2);
            return key;
        }

        /// <summary>What the code is, for a menu: "Servo alarm", "Axis alarm", "Axis warning".</summary>
        public static string Caption(AxisCodeKind kind)
        {
            switch (kind)
            {
                case AxisCodeKind.ServoAlarm: return LanguageManager.GetString("Code_ServoAlarm", "Servo alarm");
                case AxisCodeKind.AxisError: return LanguageManager.GetString("Code_AxisAlarm", "Axis alarm");
                case AxisCodeKind.AxisWarning: return LanguageManager.GetString("Code_AxisWarning", "Axis warning");
                case AxisCodeKind.InverterFault: return LanguageManager.GetString("Code_InverterFault", "Inverter fault");
                default: return "";
            }
        }

        // ---- the documents' index ----------------------------------------

        /// <summary>One code as its document lists it.</summary>
        public sealed class Entry
        {
            /// <summary>Name is the code's own ("Receive data error 3"); Alarm its servo alarm's ("Encoder initial communication error 1").</summary>
            public string Code = "", Group = "", Name = "", Alarm = "";
            public bool IsWarning;
            public int Page;
        }

        private sealed class Index
        {
            public DateTime Stamp;
            /// <summary>When the file's date was last looked at - at most every few seconds.</summary>
            public DateTime Checked;
            public Dictionary<string, Entry> ByCode = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, Entry> FirstOfGroup = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        }

        // Read once per file and kept until the file changes: the "?" asks for a
        // code's name every time its tooltip is refreshed.
        private static readonly Dictionary<string, Index> _indexes = new Dictionary<string, Index>(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex SectionTag = new Regex("<section\\s+class=\"code\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Attr = new Regex("data-(\\w+)=\"([^\"]*)\"", RegexOptions.Compiled);

        private static Index IndexOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            lock (_indexes)
            {
                Index known;
                if (_indexes.TryGetValue(path, out known) && (DateTime.UtcNow - known.Checked).TotalSeconds < 5)
                    return known;
            }
            DateTime stamp;
            try
            {
                if (!File.Exists(path)) return null;
                stamp = File.GetLastWriteTimeUtc(path);
            }
            catch { return null; }

            lock (_indexes)
            {
                Index idx;
                if (_indexes.TryGetValue(path, out idx) && idx.Stamp == stamp) { idx.Checked = DateTime.UtcNow; return idx; }
                idx = new Index { Stamp = stamp, Checked = DateTime.UtcNow };
                try
                {
                    foreach (Match m in SectionTag.Matches(File.ReadAllText(path)))
                    {
                        var e = new Entry();
                        foreach (Match a in Attr.Matches(m.Value))
                        {
                            var v = WebUtility.HtmlDecode(a.Groups[2].Value);
                            switch (a.Groups[1].Value.ToLowerInvariant())
                            {
                                case "code": e.Code = v; break;
                                case "group": e.Group = v; break;
                                case "name": e.Name = v; break;
                                case "alarm": e.Alarm = v; break;
                                case "kind": e.IsWarning = v.Equals("warning", StringComparison.OrdinalIgnoreCase); break;
                                case "page": int.TryParse(v, out e.Page); break;
                            }
                        }
                        if (e.Code.Length == 0) continue;
                        if (!idx.ByCode.ContainsKey(e.Code)) idx.ByCode[e.Code] = e;
                        if (e.Group.Length > 0 && !idx.FirstOfGroup.ContainsKey(e.Group)) idx.FirstOfGroup[e.Group] = e;
                    }
                }
                catch (Exception ex)
                {
                    AuditLogger.Log("could not read code help " + Path.GetFileName(path) + ": " + ex.Message);
                }
                _indexes[path] = idx;
                return idx;
            }
        }

        /// <summary>The document's entry for a code - or, for an unlisted servo detail, its alarm's first.</summary>
        public static Entry Find(AxisCodeKind kind, int raw)
        {
            var idx = IndexOf(TroubleshootingCatalog.ResolveLanguageFile(DocumentFor(kind)));
            if (idx == null || raw <= 0) return null;
            Entry e;
            if (idx.ByCode.TryGetValue(Key(kind, raw), out e)) return e;
            if (idx.FirstOfGroup.TryGetValue(GroupKey(kind, raw), out e)) return e;
            return null;
        }

        /// <summary>
        /// True when the code is a WARNING - the axis still runs - so its mark is
        /// drawn amber, not red. An axis warning always is; a servo code is when
        /// the manual lists it as one (E6.1 forced stop, 9x, Ex...).
        /// </summary>
        public static bool IsWarning(AxisCodeKind kind, int raw)
        {
            if (kind == AxisCodeKind.AxisWarning) return true;
            if (kind != AxisCodeKind.ServoAlarm || raw <= 0) return false;
            var e = Find(kind, raw);
            return e != null && e.IsWarning;
        }

        /// <summary>
        /// The inverter's own name for a fault, as its panel shows it ("E.OC1"), or the
        /// data code when the document does not list it.
        /// </summary>
        public static string Display(AxisCodeKind kind, int raw)
        {
            if (kind != AxisCodeKind.InverterFault) return Format(kind, raw);
            var e = Find(kind, raw);
            return e != null && e.Alarm.Length > 0 ? e.Alarm : Format(kind, raw);
        }

        /// <summary>"Servo alarm 16.3 - Encoder initial communication - Receive data error 3"; "Inverter fault E.OC1 (16) - Overcurrent trip during acceleration".</summary>
        public static string Describe(AxisCodeKind kind, int raw)
        {
            var e = Find(kind, raw);
            var text = Caption(kind) + " " + Format(kind, raw);
            if (kind == AxisCodeKind.InverterFault)
            {
                text = Caption(kind) + " " + (e != null && e.Alarm.Length > 0 ? e.Alarm + " (" + Format(kind, raw) + ")" : Format(kind, raw));
                if (e != null && e.Name.Length > 0) text += " - " + e.Name;
                return text;
            }
            if (e != null)
            {
                // A servo detail missing from the manual is named by its ALARM:
                // the name of a neighbouring detail would be a wrong answer.
                bool exact = e.Code.Equals(Key(kind, raw), StringComparison.OrdinalIgnoreCase);
                var name = exact ? e.Name : (e.Alarm.Length > 0 ? e.Alarm : e.Name);
                if (name.Length > 0) text += " - " + name;
            }
            return text;
        }

        /// <summary>The help document for one code (null when there is no code or no document).</summary>
        public static TroubleshootingCatalog.HelpDocument Document(AxisCodeKind kind, int raw, string unit = "")
        {
            if (kind == AxisCodeKind.None || raw <= 0) return null;
            var doc = new TroubleshootingCatalog.HelpDocument
            {
                FileName = DocumentFor(kind),
                Code = Key(kind, raw),
                CodeGroup = GroupKey(kind, raw),
                Title = Describe(kind, raw) + (string.IsNullOrEmpty(unit) ? "" : "  (" + unit + ")")
            };
            return doc.Exists ? doc : null;
        }

        // ---- the page for one code -----------------------------------------

        /// <summary>
        /// Writes a page holding only the code's sections, and returns its path;
        /// the whole document when it cannot. Nothing of the document above its
        /// first section is copied but its head: the page is the code's caption
        /// and its table of causes and remedies, nothing else to read first.
        ///
        /// A &lt;base&gt; pointing at the Help folder goes into the copy: it is
        /// written to the temp folder, and without it the link to the PDF and any
        /// picture the document refers to by name would point into nothing.
        /// </summary>
        public static string Extract(TroubleshootingCatalog.HelpDocument doc)
        {
            if (doc == null) return "";
            string full = doc.FullPath;
            try
            {
                var html = File.ReadAllText(full);
                var bodies = Sections(html);
                int first = html.IndexOf("<section class=\"code\"", StringComparison.OrdinalIgnoreCase);
                if (first < 0) return full;

                var chosen = bodies.Where(s => s.Key.Code.Equals(doc.Code, StringComparison.OrdinalIgnoreCase)).ToList();
                bool fallback = false;
                if (chosen.Count == 0 && !string.IsNullOrEmpty(doc.CodeGroup))
                {
                    chosen = bodies.Where(s => s.Key.Group.Equals(doc.CodeGroup, StringComparison.OrdinalIgnoreCase)).ToList();
                    fallback = chosen.Count > 0;
                }

                // Just the code: the document's head (its style) and the sections -
                // not its title or anything else above the first section.
                int body = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
                body = body < 0 ? first : Math.Min(first, html.IndexOf('>', body) + 1);
                var page = new StringBuilder(html.Substring(0, body)).Append("\r\n");
                if (fallback)
                    page.Append("<div class=\"missing\">The manual has no entry for <b>" + WebUtility.HtmlEncode(doc.Code) +
                                "</b>. These are the entries of alarm <b>" + WebUtility.HtmlEncode(doc.CodeGroup) +
                                "</b>; the one that matches the amplifier's display applies.</div>\r\n");
                if (chosen.Count == 0)
                    page.Append("<div class=\"missing\">Code <b>" + WebUtility.HtmlEncode(doc.Code) +
                                "</b> is not listed in this document. Look it up in the manual itself: " +
                                ManualLinks(html) + "</div>\r\n");
                // Several details of one alarm: the alarm's caption once, above the first.
                string shownGroup = null;
                foreach (var s in chosen)
                {
                    var section = s.Value;
                    var group = GroupCaption.Match(section);
                    if (group.Success && group.Value == shownGroup) section = section.Remove(group.Index, group.Length);
                    else if (group.Success) shownGroup = group.Value;
                    page.Append(section).Append("\r\n");
                }
                page.Append("</body></html>\r\n");

                string result = page.ToString();
                string baseTag = "<base href=\"" + new Uri(TroubleshootingCatalog.HelpFolder.TrimEnd('\\', '/') +
                                                           Path.DirectorySeparatorChar).AbsoluteUri + "\">";
                int head = result.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
                result = head >= 0 ? result.Insert(head + 6, baseTag) : baseTag + result;

                var safe = new string(doc.Code.Where(char.IsLetterOrDigit).ToArray());
                var path = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(full) + "_" + safe + ".html");
                File.WriteAllText(path, result, new UTF8Encoding(true));
                return path;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not prepare the help for code " + doc.Code + ": " + ex.Message);
                return full;
            }
        }

        /// <summary>A servo section's alarm caption: &lt;header class="alarm"&gt;...&lt;/header&gt;.</summary>
        private static readonly Regex GroupCaption = new Regex("<header\\s+class=\"alarm\"[^>]*>.*?</header>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex SectionBlock = new Regex("<section\\s+class=\"code\"[^>]*>.*?</section>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static List<KeyValuePair<Entry, string>> Sections(string html)
        {
            var list = new List<KeyValuePair<Entry, string>>();
            foreach (Match m in SectionBlock.Matches(html))
            {
                var tag = SectionTag.Match(m.Value).Value;
                var e = new Entry();
                foreach (Match a in Attr.Matches(tag))
                {
                    var v = WebUtility.HtmlDecode(a.Groups[2].Value);
                    var n = a.Groups[1].Value.ToLowerInvariant();
                    if (n == "code") e.Code = v;
                    else if (n == "group") e.Group = v;
                }
                list.Add(new KeyValuePair<Entry, string>(e, m.Value));
            }
            return list;
        }

        /// <summary>The PDF the document was made from, as a link - taken from its own sections.</summary>
        private static string ManualLinks(string html)
        {
            var m = Regex.Match(html, "href=\"([^\"#]+\\.pdf)", RegexOptions.IgnoreCase);
            return m.Success ? "<a href=\"" + m.Groups[1].Value + "\">" + WebUtility.HtmlEncode(m.Groups[1].Value) + "</a>" : "";
        }
    }
}
