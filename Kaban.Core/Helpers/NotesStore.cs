using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Kaban.Core.Enums;

namespace Kaban.Core.Helpers
{
    /// <summary>What a note is: a comment, a translation of a text, a helper (a text and a file).</summary>
    public enum NoteKind { Comment, Translation, Helper }

    /// <summary>
    /// Proposed: an Operator's translation or helper, waiting for Maintenance or an
    /// Admin. Applied: in force - a translation on the screen, a helper in the
    /// control's helper list, a comment as written.
    /// </summary>
    public enum NoteState { Proposed, Applied }

    /// <summary>A text of a translation in one language.</summary>
    public sealed class NoteText
    {
        [XmlAttribute] public string Lang { get; set; }
        [XmlText] public string Text { get; set; }
    }

    /// <summary>One note on one control of the HMI.</summary>
    public sealed class Note
    {
        public string Id { get; set; }
        public NoteKind Kind { get; set; }
        public NoteState State { get; set; }
        /// <summary>The control it is about - its place in the program (see the notes page).</summary>
        public string ControlKey { get; set; }
        /// <summary>The control as it was shown when the note was written ("Robot X - Position").</summary>
        public string ControlName { get; set; }
        /// <summary>The page it is on ("FA2010 / Machine Parameters / Axis Settings").</summary>
        public string Page { get; set; }
        public string Author { get; set; }
        public string Level { get; set; }
        public DateTime Time { get; set; }
        /// <summary>The language on screen when it was written.</summary>
        public string Language { get; set; }
        /// <summary>A translation: the key, and the texts by language.</summary>
        public string TranslationKey { get; set; }
        public List<NoteText> Texts { get; set; }
        /// <summary>A comment's or a helper's text.</summary>
        public string Text { get; set; }
        /// <summary>A helper's file: as kept in the notes' Files folder, and its own name.</summary>
        public string File { get; set; }
        public string FileName { get; set; }
        public string AcceptedBy { get; set; }
        public DateTime? AcceptedAt { get; set; }

        public Note() { Texts = new List<NoteText>(); }

        public string TextIn(string lang)
        {
            var t = Texts.FirstOrDefault(x => string.Equals(x.Lang, lang, StringComparison.OrdinalIgnoreCase));
            return t != null ? t.Text : null;
        }
    }

    /// <summary>
    /// THE NOTES the panel's users leave on the HMI's controls - comments,
    /// translations, helpers - kept for the panel (ProgramData\KabanHMI\Notes),
    /// whoever writes them, and sent to the machine's builder as one file
    /// (Export). Anyone signed in writes notes; a translation or a helper from
    /// Maintenance or an Admin is in force at once (on the screen, in the
    /// control's helper list), an Operator's is a proposal until Maintenance or
    /// an Admin accepts it.
    /// </summary>
    public static class NotesStore
    {
        private static readonly object _lock = new object();
        private static List<Note> _notes;
        private static string _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Notes");

        /// <summary>A note was added, accepted or removed.</summary>
        public static event EventHandler Changed;

        /// <summary>Where the notes are kept (replaceable for tests): Notes.xml and the Files folder.</summary>
        public static string Folder
        {
            get { return _folder; }
            set { lock (_lock) { _folder = value; _notes = null; } }
        }

        private static string FilePath { get { return Path.Combine(_folder, "Notes.xml"); } }
        public static string FilesFolder { get { return Path.Combine(_folder, "Files"); } }

        /// <summary>Every note, oldest first.</summary>
        public static IList<Note> All
        {
            get { lock (_lock) { EnsureLoaded(); return _notes.ToList(); } }
        }

        /// <summary>The notes on one control, oldest first.</summary>
        public static IList<Note> Of(string controlKey)
        {
            lock (_lock)
            {
                EnsureLoaded();
                return _notes.Where(n => string.Equals(n.ControlKey, controlKey, StringComparison.Ordinal)).ToList();
            }
        }

        /// <summary>True when the one signed in puts translations and helpers in force (Maintenance, Admin).</summary>
        public static bool CanApply { get { return SecurityManager.HasAccess(SecurityLevel.Maintenance); } }

        /// <summary>True when someone is signed in: notes are written under a name.</summary>
        public static bool CanWrite { get { return SecurityManager.CurrentLevel != SecurityLevel.None; } }

        /// <summary>
        /// Keeps a note under the name of the one signed in: in force at once for
        /// a comment, and for a translation or a helper from Maintenance or an
        /// Admin; a proposal for an Operator's. Null when nobody is signed in.
        /// </summary>
        public static Note Add(Note note)
        {
            if (note == null || !CanWrite) return null;
            note.Id = Guid.NewGuid().ToString("N");
            note.Author = SecurityManager.CurrentUser ?? "";
            note.Level = SecurityManager.CurrentLevel.ToString();
            note.Time = DateTime.Now;
            note.Language = LanguageManager.CurrentLanguage;
            note.State = note.Kind == NoteKind.Comment || CanApply ? NoteState.Applied : NoteState.Proposed;
            if (note.State == NoteState.Applied && note.Kind != NoteKind.Comment)
            {
                note.AcceptedBy = note.Author;
                note.AcceptedAt = note.Time;
            }
            lock (_lock)
            {
                EnsureLoaded();
                _notes.Add(note);
                Save();
            }
            if (note.Kind == NoteKind.Translation && note.State == NoteState.Applied) Apply(note, raise: true);
            Raise();
            return note;
        }

        /// <summary>An Operator's proposal put in force by Maintenance or an Admin. False when not theirs to accept.</summary>
        public static bool Accept(Note note)
        {
            if (note == null || note.State != NoteState.Proposed || !CanApply) return false;
            lock (_lock)
            {
                EnsureLoaded();
                var kept = _notes.FirstOrDefault(n => n.Id == note.Id);
                if (kept == null) return false;
                kept.State = NoteState.Applied;
                kept.AcceptedBy = SecurityManager.CurrentUser ?? "";
                kept.AcceptedAt = DateTime.Now;
                note.State = kept.State; note.AcceptedBy = kept.AcceptedBy; note.AcceptedAt = kept.AcceptedAt;
                Save();
            }
            if (note.Kind == NoteKind.Translation) Apply(note, raise: true);
            Raise();
            return true;
        }

        /// <summary>True when the one signed in may take a note away: its author, or an Admin.</summary>
        public static bool CanRemove(Note note)
        {
            if (note == null || !CanWrite) return false;
            return SecurityManager.HasAccess(SecurityLevel.Admin) ||
                   string.Equals(note.Author, SecurityManager.CurrentUser, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Takes a note away (a translation in force stays on screen until the next start).</summary>
        public static bool Remove(Note note)
        {
            if (!CanRemove(note)) return false;
            lock (_lock)
            {
                EnsureLoaded();
                if (_notes.RemoveAll(n => n.Id == note.Id) == 0) return false;
                Save();
            }
            Raise();
            return true;
        }

        /// <summary>The translations in force put on the screen - at start, after the translation files.</summary>
        public static void ApplyTranslations()
        {
            List<Note> list;
            lock (_lock) { EnsureLoaded(); list = _notes.Where(n => n.Kind == NoteKind.Translation && n.State == NoteState.Applied).OrderBy(n => n.Time).ToList(); }
            foreach (var n in list) Apply(n, raise: false);
            if (list.Count > 0) LanguageManager.RaiseLanguageChanged();
        }

        private static void Apply(Note n, bool raise)
        {
            if (string.IsNullOrWhiteSpace(n.TranslationKey)) return;
            foreach (var t in n.Texts)
                if (!string.IsNullOrWhiteSpace(t.Text)) LanguageManager.AddTranslation(t.Lang, n.TranslationKey, t.Text.Trim());
            if (raise) LanguageManager.RaiseLanguageChanged();
        }

        /// <summary>A file for a helper, copied into the notes (its name kept unique). The name it is kept under.</summary>
        public static string KeepFile(string source)
        {
            if (string.IsNullOrEmpty(source) || !System.IO.File.Exists(source)) return null;
            Directory.CreateDirectory(FilesFolder);
            string name = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + Path.GetFileName(source);
            string target = Path.Combine(FilesFolder, name);
            for (int i = 2; System.IO.File.Exists(target); i++)
                target = Path.Combine(FilesFolder, Path.GetFileNameWithoutExtension(name) + "_" + i + Path.GetExtension(name));
            System.IO.File.Copy(source, target);
            return Path.GetFileName(target);
        }

        /// <summary>Where a helper's file is on the panel, or null.</summary>
        public static string PathOf(Note n)
        {
            if (n == null || string.IsNullOrEmpty(n.File)) return null;
            var p = Path.Combine(FilesFolder, n.File);
            return System.IO.File.Exists(p) ? p : null;
        }

        private static void Raise() { var h = Changed; if (h != null) h(null, EventArgs.Empty); }

        // ---- kept ------------------------------------------------------------------

        private static void EnsureLoaded()
        {
            if (_notes != null) return;
            _notes = new List<Note>();
            try
            {
                if (!System.IO.File.Exists(FilePath)) return;
                using (var r = new StreamReader(FilePath, Encoding.UTF8))
                {
                    var list = new XmlSerializer(typeof(List<Note>), new XmlRootAttribute("Notes")).Deserialize(r) as List<Note>;
                    if (list != null) _notes = list;
                }
            }
            catch (Exception ex) { AuditLogger.Log("notes: could not be read (" + ex.Message + ")"); }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(_folder);
                var sb = new StringBuilder();
                using (var w = new StringWriter(sb, CultureInfo.InvariantCulture))
                    new XmlSerializer(typeof(List<Note>), new XmlRootAttribute("Notes")).Serialize(w, _notes);
                SafeFile.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { AuditLogger.Log("notes: could not be kept (" + ex.Message + ")"); }
        }

        /// <summary>Notes.xml as it is kept (the feedback file carries it).</summary>
        internal static string StoreXml()
        {
            lock (_lock)
            {
                EnsureLoaded();
                var sb = new StringBuilder();
                using (var w = new StringWriter(sb, CultureInfo.InvariantCulture))
                    new XmlSerializer(typeof(List<Note>), new XmlRootAttribute("Notes")).Serialize(w, _notes);
                return sb.ToString();
            }
        }

        // ---- the feedback file -----------------------------------------------------

        /// <summary>
        /// THE FEEDBACK FILE: every note of the panel in one .zip - Notes.html to
        /// read (by page and control), Notes.csv for a spreadsheet, Notes.xml as
        /// kept (to read back) and the helpers' files. Written into
        /// <paramref name="folder"/>; its path is returned.
        /// </summary>
        public static string Export(string folder, string panelName)
        {
            Directory.CreateDirectory(folder);
            string safe = new string((panelName ?? "HMI").Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_').ToArray());
            string zip = Path.Combine(folder, "Feedback_" + safe + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + ".zip");
            for (int i = 2; System.IO.File.Exists(zip); i++) zip = Path.Combine(folder, Path.GetFileNameWithoutExtension(zip) + "_" + i + ".zip");
            var notes = All;
            using (var fs = new FileStream(zip, FileMode.CreateNew))
            using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
            {
                Action<string, string> text = (name, content) =>
                {
                    var e = archive.CreateEntry(name);
                    using (var w = new StreamWriter(e.Open(), new UTF8Encoding(true))) w.Write(content);
                };
                text("Notes.html", Html(notes, panelName));
                text("Notes.csv", Csv(notes));
                text("Notes.xml", StoreXml());
                foreach (var n in notes)
                {
                    var p = PathOf(n);
                    if (p == null) continue;
                    var e = archive.CreateEntry("Files/" + n.File);
                    using (var src = System.IO.File.OpenRead(p))
                    using (var dst = e.Open()) src.CopyTo(dst);
                }
            }
            AuditLogger.Log("notes: feedback file " + zip + " (" + notes.Count + " notes)");
            return zip;
        }

        /// <summary>What a feedback file holds, by name (to look into one; tests).</summary>
        public static IList<string> EntriesOf(string zip)
        {
            using (var fs = System.IO.File.OpenRead(zip))
            using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read))
                return archive.Entries.Select(e => e.FullName).ToList();
        }

        /// <summary>A text in a feedback file (Notes.html ...), or null.</summary>
        public static string TextIn(string zip, string entry)
        {
            using (var fs = System.IO.File.OpenRead(zip))
            using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read))
            {
                var e = archive.GetEntry(entry);
                if (e == null) return null;
                using (var r = new StreamReader(e.Open(), Encoding.UTF8)) return r.ReadToEnd();
            }
        }

        private static string StateText(Note n)
        {
            if (n.Kind == NoteKind.Comment) return "comment";
            return n.State == NoteState.Applied ? (n.Kind == NoteKind.Translation ? "in force" : "in the helper list") : "proposed";
        }

        private static string Csv(IList<Note> notes)
        {
            var sb = new StringBuilder();
            Func<string, string> q = s => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
            sb.AppendLine("Time;Author;Level;Kind;State;Page;Control;Key;Text;File");
            foreach (var n in notes)
            {
                string body = n.Kind == NoteKind.Translation
                    ? string.Join(" | ", n.Texts.Where(t => !string.IsNullOrWhiteSpace(t.Text)).Select(t => t.Lang + ": " + t.Text))
                    : n.Text;
                sb.AppendLine(string.Join(";", new[]
                {
                    q(n.Time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)), q(n.Author), q(n.Level), q(n.Kind.ToString()), q(StateText(n)),
                    q(n.Page), q(n.ControlName), q(n.TranslationKey), q(body), q(n.FileName)
                }));
            }
            return sb.ToString();
        }

        private static string Html(IList<Note> notes, string panelName)
        {
            Func<string, string> h = s => System.Net.WebUtility.HtmlEncode(s ?? "");
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Notes</title><style>")
              .Append("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#191c22}h1{font-size:20px}h2{font-size:15px;margin-top:28px;border-bottom:1px solid #dde1e6}")
              .Append("h3{font-size:13px;margin:14px 0 4px}table{border-collapse:collapse;width:100%;font-size:13px}td,th{border:1px solid #e4e7eb;padding:4px 6px;text-align:left;vertical-align:top}")
              .Append("th{background:#f5f6f8}.p{color:#b06a00}.a{color:#2a7a2a}</style></head><body>");
            sb.Append("<h1>").Append(h(panelName)).Append(" - notes, ").Append(h(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)))
              .Append("</h1><p>").Append(notes.Count).Append(" notes</p>");
            foreach (var page in notes.GroupBy(n => n.Page ?? "").OrderBy(g => g.Key))
            {
                sb.Append("<h2>").Append(h(page.Key)).Append("</h2>");
                foreach (var control in page.GroupBy(n => n.ControlName ?? "").OrderBy(g => g.Key))
                {
                    sb.Append("<h3>").Append(h(control.Key)).Append("</h3><table><tr><th>Time</th><th>Who</th><th>Kind</th><th>State</th><th>Text</th><th>File</th></tr>");
                    foreach (var n in control.OrderBy(x => x.Time))
                    {
                        string body = n.Kind == NoteKind.Translation
                            ? h(n.TranslationKey) + "<br>" + string.Join("<br>", n.Texts.Where(t => !string.IsNullOrWhiteSpace(t.Text)).Select(t => h(t.Lang) + ": " + h(t.Text)))
                            : h(n.Text).Replace("\n", "<br>");
                        sb.Append("<tr><td>").Append(h(n.Time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))).Append("</td><td>")
                          .Append(h(n.Author)).Append(" (").Append(h(n.Level)).Append(")</td><td>").Append(h(n.Kind.ToString())).Append("</td><td class=\"")
                          .Append(n.State == NoteState.Proposed ? "p" : "a").Append("\">").Append(h(StateText(n))).Append("</td><td>").Append(body).Append("</td><td>")
                          .Append(string.IsNullOrEmpty(n.File) ? "" : "<a href=\"Files/" + h(n.File) + "\">" + h(n.FileName) + "</a>").Append("</td></tr>");
                    }
                    sb.Append("</table>");
                }
            }
            sb.Append("</body></html>");
            return sb.ToString();
        }
    }
}
