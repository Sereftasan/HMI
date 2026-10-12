using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Kaban.Core.Helpers;

// The help for ONE axis code: printed as the manual prints it, found in the
// right document, and opened as just that code's entry.
class AC {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static int Sections(string html) { return Regex.Matches(html, "<section class=\"code\"").Count; }

    static void Main() {
        Console.WriteLine("--- printed as the manuals print them ---");
        Check("servo 0x163 is 16.3 (the amplifier's display)", AxisCodeHelp.Format(AxisCodeKind.ServoAlarm, 0x163) == "16.3");
        Check("servo 0xE61 is E6.1", AxisCodeHelp.Format(AxisCodeKind.ServoAlarm, 0xE61) == "E6.1");
        Check("axis error 0x1904 is 1904H (it printed 190.4, which no manual has)",
              AxisCodeHelp.Format(AxisCodeKind.AxisError, 0x1904) == "1904H", AxisCodeHelp.Format(AxisCodeKind.AxisError, 0x1904));
        Check("axis warning 0x0900 is 0900H", AxisCodeHelp.Format(AxisCodeKind.AxisWarning, 0x0900) == "0900H");
        Check("zero prints nothing", AxisCodeHelp.Format(AxisCodeKind.AxisError, 0) == "" && AxisCodeHelp.Format(AxisCodeKind.ServoAlarm, 0) == "");
        Check("a plain code word keeps the servo form", AxisCodeHelp.Format(AxisCodeKind.None, 0x502) == "50.2");

        Console.WriteLine("--- the document's keys ---");
        Check("servo key 16.3, group 16", AxisCodeHelp.Key(AxisCodeKind.ServoAlarm, 0x163) == "16.3" &&
                                          AxisCodeHelp.GroupKey(AxisCodeKind.ServoAlarm, 0x163) == "16");
        Check("servo key keeps two alarm digits (0x0E61 = E6.1, 0x0101 = 10.1)",
              AxisCodeHelp.Key(AxisCodeKind.ServoAlarm, 0x0E61) == "E6.1" && AxisCodeHelp.Key(AxisCodeKind.ServoAlarm, 0x101) == "10.1");
        Check("axis key 1904", AxisCodeHelp.Key(AxisCodeKind.AxisError, 0x1904) == "1904");

        Console.WriteLine("--- the documents are there and complete ---");
        foreach (var kv in new[] { Tuple.Create(AxisCodeKind.ServoAlarm, 350), Tuple.Create(AxisCodeKind.AxisError, 180),
                                    Tuple.Create(AxisCodeKind.AxisWarning, 70) })
        {
            var path = TroubleshootingCatalog.ResolveLanguageFile(AxisCodeHelp.DocumentFor(kv.Item1));
            bool there = File.Exists(path);
            int n = there ? Sections(File.ReadAllText(path)) : 0;
            Check(kv.Item1 + ": " + Path.GetFileName(path) + " in the Help folder, one section per code", there && n >= kv.Item2, n + " sections");
        }

        Console.WriteLine("--- named from the document ---");
        var e = AxisCodeHelp.Find(AxisCodeKind.ServoAlarm, 0x163);
        Check("16.3 is the encoder's initial communication error", e != null && e.Name.Contains("Encoder initial communication"), e == null ? "" : e.Name);
        e = AxisCodeHelp.Find(AxisCodeKind.AxisError, 0x1904);
        Check("1904H is the hardware stroke limit (+)", e != null && e.Name == "Hardware stroke limit (+)", e == null ? "" : e.Name);
        e = AxisCodeHelp.Find(AxisCodeKind.AxisWarning, 0x0900);
        Check("0900H is start during operation", e != null && e.Name == "Start during operation", e == null ? "" : e.Name);
        Check("E6.1 (forced stop) is a WARNING, 16.3 is not",
              AxisCodeHelp.IsWarning(AxisCodeKind.ServoAlarm, 0xE61) && !AxisCodeHelp.IsWarning(AxisCodeKind.ServoAlarm, 0x163));
        Check("an axis warning is a warning, an axis alarm is not",
              AxisCodeHelp.IsWarning(AxisCodeKind.AxisWarning, 0x0900) && !AxisCodeHelp.IsWarning(AxisCodeKind.AxisError, 0x1904));
        string d160 = AxisCodeHelp.Describe(AxisCodeKind.ServoAlarm, 0x160);
        Check("an unlisted detail (16.0) is named by its alarm", d160.StartsWith("Servo alarm 16.0 - Encoder initial communication error 1") && !d160.Contains("Receive data"), d160);

        Console.WriteLine("--- opened as just that code's entry ---");
        var doc = AxisCodeHelp.Document(AxisCodeKind.ServoAlarm, 0x163, "Robot X");
        Check("a document for 16.3", doc != null && doc.Code == "16.3" && doc.Title.Contains("16.3") && doc.Title.Contains("Robot X"),
              doc == null ? "" : doc.Title);
        string page = TroubleshootingCatalog.ExtractSection(doc);
        string html = File.Exists(page) ? File.ReadAllText(page) : "";
        Check("written to its own page", page.StartsWith(Path.GetTempPath()) && html.Length > 0, page);
        Check("  holding only 16.3", Sections(html) == 1 && html.Contains("data-code=\"16.3\""), Sections(html) + " sections");
        Check("  nothing above it: no title, no general notes (no CAUTION, no 'Work down the table')",
              !html.Contains("CAUTION") && !html.Contains("Work down") && !html.Contains("<h1") &&
              html.IndexOf("<body>") < html.IndexOf("<section") && !Regex.IsMatch(html.Substring(html.IndexOf("<body>"), html.IndexOf("<section") - html.IndexOf("<body>")), "<(p|div|h\\d)"));
        Check("  the alarm first ('Alarm 16: ...'), then the detail ('AL. 16.3 - ...')",
              html.IndexOf("<h2>Alarm 16:") > 0 && html.IndexOf("<h2>Alarm 16:") < html.IndexOf("<h3 id=\"AL16.3\">AL. 16.3"));
        Check("  one table, one heading row, no Detail No./Detail name columns",
              Regex.Matches(html, "<table>").Count == 1 && Regex.Matches(html, "<tr><th").Count == 1 && !html.Contains("<div>Detail"));
        var split = AxisCodeHelp.Document(AxisCodeKind.ServoAlarm, 0x521);
        var s52 = File.ReadAllText(TroubleshootingCatalog.ExtractSection(split));
        Check("52.1, two pages of the manual: ONE table all the same, causes (1) to (14) in it",
              Regex.Matches(s52, "<table>").Count == 1 && Regex.Matches(s52, "<tr><th").Count == 1 &&
              s52.Contains("<div>(1)</div>") && s52.Contains("<div>(12)</div>") && s52.Contains("<div>(14)</div>"));
        Check("  and a base pointing at the Help folder (the link to the PDF works from the copy)",
              html.Contains("<base href=\"file:") && html.Contains("MR_J4_TroubleShooting.pdf#page="));

        doc = AxisCodeHelp.Document(AxisCodeKind.ServoAlarm, 0x160);
        html = File.ReadAllText(TroubleshootingCatalog.ExtractSection(doc));
        int details16 = Regex.Matches(File.ReadAllText(TroubleshootingCatalog.ResolveLanguageFile(AxisCodeHelp.ServoAlarmDocument)),
                                      "data-group=\"16\"").Count;
        Check("an unlisted detail shows every detail of its alarm, and says so",
              Sections(html) == details16 && html.Contains("has no entry for"), Sections(html) + " of " + details16);
        Check("  the alarm's caption once, above the first of them",
              Regex.Matches(html, "<h2>Alarm 16:").Count == 1 && Regex.Matches(html, "<h3 id=\"AL16\\.").Count == details16);

        doc = AxisCodeHelp.Document(AxisCodeKind.AxisError, 0x1904);
        html = File.ReadAllText(TroubleshootingCatalog.ExtractSection(doc));
        Check("1904H: one section, both of its cause/remedy rows",
              Sections(html) == 1 && Regex.Matches(html, "<tr><td>").Count == 2);
        Check("  and the link to the module manual's page", html.Contains("FX5_40SSC_Troubleshooting.pdf#page="));

        doc = AxisCodeHelp.Document(AxisCodeKind.AxisError, 0x1FFF);
        html = File.ReadAllText(TroubleshootingCatalog.ExtractSection(doc));
        Check("a code the manual does not list says so, with the manual to look in",
              Sections(html) == 0 && html.Contains("is not listed") && html.Contains(".pdf"));

        Check("no code, no document", AxisCodeHelp.Document(AxisCodeKind.ServoAlarm, 0) == null &&
                                      AxisCodeHelp.Document(AxisCodeKind.None, 0x163) == null);

        LanguageManager.ChangeLanguage("TR");
        Check("Turkish, no Turkish file yet: the English one",
              TroubleshootingCatalog.ResolveLanguageFile(AxisCodeHelp.AxisErrorDocument).EndsWith("Axis_Error_Codes.en.html"));
        LanguageManager.ChangeLanguage("EN");

        Console.WriteLine(fail == 0 ? "\nAXIS CODES ALL PASS" : "\nAXIS CODES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
