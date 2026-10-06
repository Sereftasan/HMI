using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Screens
{
    /// <summary>
    /// NOTES AND FEEDBACK, a side page: while it is open a tap or a click on any
    /// control of the page picks that control - nothing is operated, no jog, no
    /// write to the PLC - and the page shows it: what it is and where, its
    /// tooltip (a touch screen has no pointer to rest on a control), its texts in
    /// every language, its helpers and its comments. Anyone signed in writes
    /// notes: a comment; a translation of a text; a helper (a text, a file).
    /// Maintenance's and an Admin's translations are on the screen at once and
    /// their helpers in the control's list; an Operator's are proposals that
    /// Maintenance or an Admin accepts. One button makes the feedback file of
    /// every note on the panel, to send to the machine's builder.
    /// </summary>
    public class NotesPage : UserControl, ISidePageWidth
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private readonly Func<Control, bool> _pickable;
        private readonly Func<Control, string> _placeOf;
        private readonly Func<string> _panelName;
        private readonly ToolTip _tips = UiStyle.Tips();

        private readonly Button _pick;
        private readonly Label _status = new Label { AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(70, 76, 86) };
        private readonly TabControl _views = new TabControl { Dock = DockStyle.Fill };
        private readonly FlowLayoutPanel _body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            BackColor = Color.White, Padding = new Padding(8, 6, 8, 12)
        };
        private readonly DataGridView _all = new DataGridView { Dock = DockStyle.Fill, Name = "notesAll" };
        private readonly Button _export, _folder;
        private readonly Label _count = new Label { AutoSize = true, ForeColor = Color.FromArgb(110, 116, 124), Margin = new Padding(10, 12, 0, 0) };

        private bool _picking = true;
        private Picker _picker;

        // the control picked: as it was when picked (it may be gone since - a page built again)
        private Control _picked;
        private string _key, _name, _place, _tip;
        private IList<NoteTarget.TextKey> _keys = new NoteTarget.TextKey[0];
        private string _file;          // a helper's file, chosen, not yet kept
        private string _lastExport;

        /// <summary>The feedback files' folder (Documents\Kaban HMI\Feedback); replaceable for tests.</summary>
        public static string FeedbackFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Kaban HMI", "Feedback");

        public NotesPage(Func<Control, bool> pickable, Func<Control, string> placeOf, Func<string> panelName)
        {
            _pickable = pickable ?? (c => false);
            _placeOf = placeOf ?? (c => "");
            _panelName = panelName ?? (() => "HMI");
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;

            // ---- the bar: picking on or off, what is under the pointer ----
            var top = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.White, Padding = new Padding(6, 6, 6, 4) };
            _pick = new Button { Dock = DockStyle.Left, Width = 170, FlatStyle = FlatStyle.Flat, Name = "notesPick", Cursor = Cursors.Hand };
            _pick.FlatAppearance.BorderColor = Color.FromArgb(160, 166, 176);
            _pick.Click += (s, e) => SetPicking(!_picking);
            _status.Dock = DockStyle.Fill;
            _status.Padding = new Padding(8, 0, 0, 0);
            top.Controls.Add(_status);
            top.Controls.Add(_pick);

            // ---- the foot: the feedback file ----
            var foot = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, BackColor = Color.White, Padding = new Padding(4, 6, 4, 0), WrapContents = false };
            _export = UiStyle.ToolButton("\uD83D\uDCE4", T("Nts_TipExport", "Make the feedback file: every note of this panel, with the helpers' files, in one .zip to send"), _tips, (s, e) => Export());
            _export.Name = "notesExport";
            _folder = UiStyle.ToolButton("\uD83D\uDCC2", T("Nts_TipFolder", "Show the feedback file in its folder (Maintenance, Admin)"), _tips, (s, e) => ShowFolder());
            _folder.Name = "notesFolder";
            var exportText = new Label { AutoSize = true, Text = T("Nts_Export", "Feedback file"), Margin = new Padding(2, 12, 6, 0), Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold) };
            foot.Controls.Add(_export);
            foot.Controls.Add(exportText);
            foot.Controls.Add(_folder);
            foot.Controls.Add(_count);

            // ---- the two views: the control picked, every note ----
            var one = new TabPage(T("Nts_TabControl", "The control")) { BackColor = Color.White, Name = "notesOne" };
            one.Controls.Add(_body);
            var every = new TabPage(T("Nts_TabAll", "Every note")) { BackColor = Color.White, Name = "notesEvery" };
            every.Controls.Add(_all);
            _views.TabPages.Add(one);
            _views.TabPages.Add(every);
            _views.SelectedIndexChanged += (s, e) => { if (_views.SelectedTab == every) FillAll(); };
            BuildAllTable();

            Controls.Add(_views);
            Controls.Add(foot);
            Controls.Add(top);

            _body.Resize += (s, e) => FitWidths();
            NotesStore.Changed += OnNotesChanged;
            SecurityManager.UserChanged += OnUserChanged;
            VisibleChanged += (s, e) => UpdatePicker();
            ParentChanged += (s, e) => UpdatePicker();
            HandleCreated += (s, e) => UpdatePicker();
            Disposed += (s, e) =>
            {
                NotesStore.Changed -= OnNotesChanged;
                SecurityManager.UserChanged -= OnUserChanged;
                RemovePicker();
            };
            SetPicking(true);
            Fill();
        }

        public int PreferredWidth(int available) { return Math.Min(available, UiScale.For(this, 540)); }

        // ==================================================================
        // PICKING
        // ==================================================================

        /// <summary>True while a tap picks a control (tests).</summary>
        public bool Picking { get { return _picking && _picker != null && Shown(); } }

        /// <summary>The picked control's key and name (tests).</summary>
        public string PickedKey { get { return _key; } }
        public string PickedName { get { return _name; } }

        private void SetPicking(bool on)
        {
            _picking = on;
            _pick.Text = on ? "\uD83D\uDC46  " + T("Nts_PickOn", "Picking: on") : "\u270B  " + T("Nts_PickOff", "Picking: off");
            _pick.BackColor = on ? Color.FromArgb(218, 232, 250) : Color.White;
            Kaban.UI.Controls.UiStyle.SetTip(_tips, _pick, on
                ? T("Nts_TipPickOn", "A tap or a click on a control of the page shows it here - it does not operate it. Tap here to use the page again.")
                : T("Nts_TipPickOff", "The page works as usual. Tap here to pick controls again."));
            _status.Text = on ? T("Nts_StatusPick", "Tap or click a control of the page.") : T("Nts_StatusOff", "The page works as usual.");
            UpdatePicker();
        }

        private bool Shown()
        {
            if (IsDisposed || !IsHandleCreated) return false;
            for (Control p = this; p != null; p = p.Parent) if (!p.Visible) return false;
            return true;
        }

        private void UpdatePicker()
        {
            if (_picking && Shown()) { if (_picker == null) { _picker = new Picker(this); Application.AddMessageFilter(_picker); } }
            else RemovePicker();
        }

        private void RemovePicker()
        {
            if (_picker == null) return;
            Application.RemoveMessageFilter(_picker);
            _picker = null;
        }

        /// <summary>The control under the pointer (shown in the bar; tests).</summary>
        internal void Pointing(Control c)
        {
            if (c == null) return;
            _status.Text = T("Nts_Pointing", "Under the pointer") + ": " + NoteTarget.NameOf(c);
        }

        /// <summary>A control picked (a tap; tests call it): shown on the page.</summary>
        public void Pick(Control c)
        {
            if (c == null) return;
            _picked = c;
            _key = NoteTarget.KeyOf(c);
            _name = NoteTarget.NameOf(c);
            _place = _placeOf(c);
            _tip = UiStyle.TipOf(c);
            _keys = NoteTarget.KeysOf(c);
            _file = null;
            _status.Text = T("Nts_Picked", "Picked") + ": " + _name;
            try { ControlHighlight.Flash(c, 1500); } catch { }
            _views.SelectedIndex = 0;
            Fill();
        }

        /// <summary>
        /// The taps while picking: a press, a release, a double click or the keys
        /// that press a button, on a control of the page, are kept from it; the
        /// release picks it. The pointer moving is let through (its tooltip, its
        /// border) and shown in the bar.
        /// </summary>
        private sealed class Picker : IMessageFilter
        {
            private const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_LBUTTONDBLCLK = 0x0203,
                              WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_RBUTTONDBLCLK = 0x0206,
                              WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208, WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;
            private readonly NotesPage _page;
            private Control _last;

            public Picker(NotesPage page) { _page = page; }

            public bool PreFilterMessage(ref Message m)
            {
                switch (m.Msg)
                {
                    case WM_MOUSEMOVE: case WM_LBUTTONDOWN: case WM_LBUTTONUP: case WM_LBUTTONDBLCLK:
                    case WM_RBUTTONDOWN: case WM_RBUTTONUP: case WM_RBUTTONDBLCLK: case WM_MBUTTONDOWN: case WM_MBUTTONUP:
                    case WM_KEYDOWN: case WM_KEYUP:
                        break;
                    default:
                        return false;
                }
                // the page out of sight (the panel closed, another page in it): taps are the controls' own
                if (_page.IsDisposed || !_page.Shown()) return false;
                var hit = Control.FromChildHandle(m.HWnd);
                if (hit == null) return false;
                if (m.Msg == WM_KEYDOWN || m.Msg == WM_KEYUP)
                {
                    var key = (Keys)(int)(long)m.WParam;
                    if (key != Keys.Space && key != Keys.Enter) return false;
                    var focused = NoteTarget.Notable(hit);
                    if (focused == null || !_page._pickable(focused)) return false;
                    if (m.Msg == WM_KEYUP) _page.Pick(focused);
                    return true;
                }
                var target = NoteTarget.Notable(hit);
                if (target == null || !_page._pickable(target)) return false;
                if (m.Msg == WM_MOUSEMOVE)
                {
                    if (target != _last) { _last = target; _page.Pointing(target); }
                    return false;
                }
                if (m.Msg == WM_LBUTTONUP) _page.Pick(target);
                return true;
            }
        }

        // ==================================================================
        // THE CONTROL PICKED
        // ==================================================================

        private void OnNotesChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnNotesChanged), sender, e); } catch { } return; }
            Fill();
            if (_views.SelectedIndex == 1) FillAll();
        }

        private void OnUserChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnUserChanged), sender, e); } catch { } return; }
            Fill();
        }

        // room for the vertical scroll bar always: the content never needs a sideways one
        private int InnerWidth { get { return Math.Max(UiScale.For(this, 240), _body.ClientSize.Width - _body.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - UiScale.For(this, 6)); } }

        private void FitWidths()
        {
            int w = InnerWidth;
            foreach (Control c in _body.Controls)
            {
                if (c.Tag as string == "full") c.Width = w;
                var l = c as Label;
                if (l != null && l.AutoSize) l.MaximumSize = new Size(w, 0);
            }
        }

        private Label AddText(string text, bool bold = false, Color? ink = null, int top = 2)
        {
            var l = new Label
            {
                AutoSize = true, Text = text ?? "", MaximumSize = new Size(InnerWidth, 0), Margin = new Padding(0, top, 0, 2),
                ForeColor = ink ?? Color.FromArgb(25, 28, 34), Font = bold ? UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold) : UiStyle.Body
            };
            _body.Controls.Add(l);
            return l;
        }

        private void Head(string text)
        {
            var l = AddText(text, true, Color.FromArgb(40, 46, 56), 14);
            l.Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);
            var rule = new Panel { Height = 1, BackColor = UiStyle.GridRule, Margin = new Padding(0, 0, 0, 4), Tag = "full", Width = InnerWidth };
            _body.Controls.Add(rule);
        }

        private TextBox Box(string text, bool multi, bool enabled, string name)
        {
            var b = new TextBox
            {
                Text = text ?? "", Multiline = multi, Height = multi ? UiScale.For(this, 64) : UiScale.For(this, 26), Width = InnerWidth,
                Tag = "full", Enabled = enabled, Name = name, Margin = new Padding(0, 2, 0, 4), ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None
            };
            _body.Controls.Add(b);
            return b;
        }

        private FlowLayoutPanel Row(bool wrap = true)
        {
            var r = new FlowLayoutPanel { AutoSize = true, WrapContents = wrap, Margin = new Padding(0, 1, 0, 1), MaximumSize = new Size(InnerWidth, 0) };
            _body.Controls.Add(r);
            return r;
        }

        private Button Act(Control row, string glyph, string text, string tip, EventHandler click, string name, bool enabled = true)
        {
            var b = new Button
            {
                Text = (glyph + "  " + text).Trim(), AutoSize = true, MinimumSize = new Size(0, UiScale.For(this, 34)), FlatStyle = FlatStyle.Flat,
                BackColor = Color.White, Cursor = Cursors.Hand, Name = name, Enabled = enabled, Margin = new Padding(0, 2, 8, 2), Padding = new Padding(6, 0, 6, 0)
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(160, 166, 176);
            if (click != null) b.Click += click;
            if (!string.IsNullOrEmpty(tip)) Kaban.UI.Controls.UiStyle.SetTip(_tips, b, tip);
            row.Controls.Add(b);
            return b;
        }

        private static string When(Note n)
        {
            return n.Time.ToString(ViewPreferences.DatePattern + " " + ViewPreferences.TimePattern(false), CultureInfo.InvariantCulture);
        }

        private static string Who(Note n) { return n.Author + " (" + n.Level + "), " + When(n); }

        /// <summary>The page again: the control picked and its notes, or what to do.</summary>
        private void Fill()
        {
            _body.SuspendLayout();
            try
            {
                foreach (Control c in _body.Controls.Cast<Control>().ToList()) { _body.Controls.Remove(c); c.Dispose(); }
                bool write = NotesStore.CanWrite, apply = NotesStore.CanApply;
                _count.Text = string.Format(T("Nts_Count", "{0} notes on this panel"), NotesStore.All.Count);
                _folder.Enabled = _lastExport != null && SecurityManager.HasAccess(SecurityLevel.Maintenance);
                if (_key == null)
                {
                    AddText(T("Nts_None", "No control picked yet."), true, null, 6);
                    AddText(T("Nts_How", "Tap or click a control of the page - a field, a button, a lamp, a caption, a column heading. It is not operated while picking: " +
                                     "what it is, its tooltip, its texts in every language, its helpers and its comments appear here, and you can add yours."),
                         false, Color.FromArgb(90, 96, 106));
                    if (!write) AddText(T("Nts_SignIn", "Sign in to write notes."), false, Color.FromArgb(176, 106, 0), 10);
                    return;
                }

                // ---- what and where ----
                AddText(_name, true, null, 4).Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);
                if (!string.IsNullOrEmpty(_place)) AddText(_place, false, Color.FromArgb(110, 116, 124));
                if (!write) AddText(T("Nts_SignIn", "Sign in to write notes."), false, Color.FromArgb(176, 106, 0), 6);

                Head(T("Nts_SecTip", "Its tooltip"));
                AddText(string.IsNullOrWhiteSpace(_tip) ? T("Nts_NoTip", "It has no tooltip.") : _tip, false,
                     string.IsNullOrWhiteSpace(_tip) ? Color.FromArgb(130, 136, 146) : (Color?)null);

                var notes = NotesStore.Of(_key);
                FillTexts(notes, write, apply);
                FillHelpers(notes, write, apply);
                FillComments(notes, write);
            }
            finally
            {
                _body.ResumeLayout(true);
            }
            FitWidths();
        }

        // ---- the texts in every language --------------------------------------------

        private static IList<string> Languages
        {
            get
            {
                var langs = LanguageManager.AvailableLanguages.Select(l => l.ToUpperInvariant()).Distinct().ToList();
                foreach (var l in new[] { "EN", "TR", "RU", "DE", "IT", "ES" }) if (!langs.Contains(l)) langs.Add(l);
                return langs;
            }
        }

        private void FillTexts(IList<Note> notes, bool write, bool apply)
        {
            Head(T("Nts_SecTexts", "Its texts in every language"));
            if (_keys.Count == 0) { AddText(T("Nts_NoTexts", "It has no text to translate."), false, Color.FromArgb(130, 136, 146)); return; }
            foreach (var k in _keys)
            {
                var key = k;
                AddText(key.What + "   (" + key.Key + ")", true, Color.FromArgb(60, 66, 76), 6);
                var boxes = new Dictionary<string, TextBox>();
                foreach (var lang in Languages)
                {
                    var row = Row(false);
                    row.Controls.Add(new Label { Text = lang, AutoSize = false, Width = UiScale.For(this, 34), Height = UiScale.For(this, 24), TextAlign = ContentAlignment.MiddleLeft,
                                                 Margin = new Padding(0, 2, 2, 0), ForeColor = Color.FromArgb(90, 96, 106) });
                    var box = new TextBox
                    {
                        Text = LanguageManager.GetStringIn(lang, key.Key) ?? "", Width = InnerWidth - UiScale.For(this, 44), Enabled = write,
                        Name = "txt" + lang, Margin = new Padding(0, 2, 0, 0)
                    };
                    row.Controls.Add(box);
                    boxes[lang] = box;
                }
                var acts = Row();
                Act(acts, "\uD83D\uDCBE", apply ? T("Nts_KeepTexts", "Keep the texts") : T("Nts_ProposeTexts", "Propose the texts"),
                    apply ? T("Nts_TipKeepTexts", "On the screen at once, for everyone on this panel - and in the feedback file")
                          : T("Nts_TipProposeTexts", "A proposal: Maintenance or an Admin puts it on the screen - and in the feedback file"),
                    (s, e) => KeepTexts(key, boxes), "notesKeepTexts", write);
                foreach (var n in notes.Where(n => n.Kind == NoteKind.Translation && n.TranslationKey == key.Key).OrderByDescending(n => n.Time))
                    NoteLine(n, string.Join("   ", n.Texts.Where(t => !string.IsNullOrWhiteSpace(t.Text)).Select(t => t.Lang + ": " + t.Text)), apply);
            }
        }

        private void KeepTexts(NoteTarget.TextKey key, Dictionary<string, TextBox> boxes)
        {
            var texts = new List<NoteText>();
            foreach (var kv in boxes)
            {
                string now = LanguageManager.GetStringIn(kv.Key, key.Key) ?? "";
                string typed = (kv.Value.Text ?? "").Trim();
                if (typed.Length > 0 && typed != now.Trim()) texts.Add(new NoteText { Lang = kv.Key, Text = typed });
            }
            if (texts.Count == 0) { _status.Text = T("Nts_NothingChanged", "No text changed."); return; }
            var n = NotesStore.Add(new Note { Kind = NoteKind.Translation, TranslationKey = key.Key, Texts = texts, ControlKey = _key, ControlName = _name, Page = _place });
            if (n != null)
                _status.Text = n.State == NoteState.Applied ? T("Nts_TextsKept", "The texts are on the screen.") : T("Nts_TextsProposed", "The texts are proposed.");
        }

        // ---- the helpers ------------------------------------------------------------------

        private void FillHelpers(IList<Note> notes, bool write, bool apply)
        {
            Head(T("Nts_SecHelpers", "Its helpers"));
            var helpers = notes.Where(n => n.Kind == NoteKind.Helper).OrderBy(n => n.State).ThenByDescending(n => n.Time).ToList();
            if (helpers.Count == 0) AddText(T("Nts_NoHelpers", "No helper yet."), false, Color.FromArgb(130, 136, 146));
            foreach (var n in helpers) NoteLine(n, n.Text, apply);
            var box = Box("", true, write, "notesHelperText");
            var acts = Row();
            var fileLabel = new Label { AutoSize = true, Margin = new Padding(0, 10, 8, 0), ForeColor = Color.FromArgb(90, 96, 106),
                                        Text = _file != null ? Path.GetFileName(_file) : "" };
            Act(acts, "\uD83D\uDCCE", T("Nts_AddFile", "Add a file"), T("Nts_TipAddFile", "A document, a picture or a video for this control"), (s, e) =>
            {
                using (var dlg = new OpenFileDialog { Title = T("Nts_AddFile", "Add a file"), Multiselect = false })
                    if (dlg.ShowDialog(FindForm()) == DialogResult.OK) { _file = dlg.FileName; fileLabel.Text = Path.GetFileName(_file); }
            }, "notesAddFile", write);
            Act(acts, "\uD83D\uDCBE", apply ? T("Nts_KeepHelper", "Keep the helper") : T("Nts_ProposeHelper", "Propose the helper"),
                apply ? T("Nts_TipKeepHelper", "In this control's helper list at once - and in the feedback file")
                      : T("Nts_TipProposeHelper", "A proposal: Maintenance or an Admin puts it in the list - and in the feedback file"),
                (s, e) => KeepHelper(box.Text), "notesKeepHelper", write);
            acts.Controls.Add(fileLabel);
        }

        /// <summary>A helper kept: its text and the file chosen (tests set the file with <see cref="ChooseFile"/>).</summary>
        internal void KeepHelper(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 && _file == null) { _status.Text = T("Nts_HelperEmpty", "Write the helper or add a file."); return; }
            string kept = null;
            try { if (_file != null) kept = NotesStore.KeepFile(_file); }
            catch (Exception ex) { _status.Text = T("Nts_FileFailed", "The file could not be kept") + ": " + ex.Message; return; }
            var n = NotesStore.Add(new Note
            {
                Kind = NoteKind.Helper, Text = text, File = kept, FileName = _file != null ? Path.GetFileName(_file) : null,
                ControlKey = _key, ControlName = _name, Page = _place
            });
            _file = null;
            if (n != null) _status.Text = n.State == NoteState.Applied ? T("Nts_HelperKept", "The helper is in the list.") : T("Nts_HelperProposed", "The helper is proposed.");
        }

        public void ChooseFile(string path) { _file = path; }

        // ---- the comments ------------------------------------------------------------------

        private void FillComments(IList<Note> notes, bool write)
        {
            Head(T("Nts_SecComments", "Comments"));
            var comments = notes.Where(n => n.Kind == NoteKind.Comment).OrderByDescending(n => n.Time).ToList();
            if (comments.Count == 0) AddText(T("Nts_NoComments", "No comment yet."), false, Color.FromArgb(130, 136, 146));
            foreach (var n in comments) NoteLine(n, n.Text, false);
            var box = Box("", true, write, "notesCommentText");
            var acts = Row();
            Act(acts, "\uD83D\uDCAC", T("Nts_AddComment", "Add the comment"), T("Nts_TipAddComment", "What you think of this control, of this page, of the machine"),
                (s, e) => AddComment(box.Text), "notesAddComment", write);
        }

        internal void AddComment(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) { _status.Text = T("Nts_CommentEmpty", "Write the comment first."); return; }
            var n = NotesStore.Add(new Note { Kind = NoteKind.Comment, Text = text, ControlKey = _key, ControlName = _name, Page = _place });
            if (n != null) _status.Text = T("Nts_CommentKept", "The comment is kept.");
        }

        /// <summary>A note: who and when, its state, its text, its file; accept and remove where allowed.</summary>
        private void NoteLine(Note n, string body, bool canAccept)
        {
            string state = n.Kind == NoteKind.Comment ? ""
                         : n.State == NoteState.Proposed ? "  \u00B7  " + T("Nts_Proposed", "proposed")
                         : "  \u00B7  " + (n.Kind == NoteKind.Translation ? T("Nts_InForce", "in force") : T("Nts_InList", "in the list"));
            AddText(Who(n) + state, false, n.State == NoteState.Proposed && n.Kind != NoteKind.Comment ? Color.FromArgb(176, 106, 0) : Color.FromArgb(110, 116, 124), 6);
            if (!string.IsNullOrWhiteSpace(body)) AddText(body);
            var path = NotesStore.PathOf(n);
            bool accept = canAccept && n.State == NoteState.Proposed && n.Kind != NoteKind.Comment;
            if (path == null && !accept && !NotesStore.CanRemove(n)) return;
            var acts = Row();
            if (path != null)
                Act(acts, "\uD83D\uDCC4", n.FileName ?? n.File, T("Nts_TipOpenFile", "Open the file"), (s, e) =>
                {
                    try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception ex) { _status.Text = ex.Message; }
                }, "notesOpenFile");
            if (accept)
                Act(acts, "\u2714", T("Nts_Accept", "Accept"), n.Kind == NoteKind.Translation ? T("Nts_TipAcceptText", "Put these texts on the screen")
                                                                                         : T("Nts_TipAcceptHelper", "Put this helper in the control's list"),
                    (s, e) => NotesStore.Accept(n), "notesAccept");
            if (NotesStore.CanRemove(n))
                Act(acts, "\u2716", T("Nts_Remove", "Remove"), T("Nts_TipRemove", "Take this note away (yours, or as an Admin)"), (s, e) =>
                {
                    if (Kaban.UI.Profiles.Ask.YesNo(FindForm(), T("Ttl_Notes", "Notes and feedback"), T("Nts_AskRemove", "Take this note away?"))) NotesStore.Remove(n);
                }, "notesRemove");
        }

        // ==================================================================
        // EVERY NOTE, AND THE FEEDBACK FILE
        // ==================================================================

        private void BuildAllTable()
        {
            _all.ReadOnly = true;
            _all.AllowUserToAddRows = false;
            _all.AllowUserToDeleteRows = false;
            _all.RowHeadersVisible = false;
            _all.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _all.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            foreach (var c in new[] { new[] { "time", T("Nts_ColTime", "Time"), "120" }, new[] { "who", T("Nts_ColWho", "Who"), "110" },
                                      new[] { "kind", T("Nts_ColKind", "Kind"), "90" }, new[] { "state", T("Nts_ColState", "State"), "90" },
                                      new[] { "control", T("Nts_ColControl", "Control"), "200" }, new[] { "text", T("Nts_ColText", "Text"), "300" },
                                      new[] { "page", T("Nts_ColPage", "Page"), "220" } })
            {
                int i = _all.Columns.Add(c[0], c[1]);
                _all.Columns[i].Width = int.Parse(c[2], CultureInfo.InvariantCulture);
            }
            UiStyle.EnableTableTools(_all, "Notes");
        }

        private string KindText(Note n)
        {
            switch (n.Kind)
            {
                case NoteKind.Translation: return T("Nts_KindText", "Texts");
                case NoteKind.Helper: return T("Nts_KindHelper", "Helper");
                default: return T("Nts_KindComment", "Comment");
            }
        }

        private void FillAll()
        {
            _all.Rows.Clear();
            foreach (var n in NotesStore.All.OrderByDescending(x => x.Time))
            {
                string body = n.Kind == NoteKind.Translation
                    ? string.Join("   ", n.Texts.Where(t => !string.IsNullOrWhiteSpace(t.Text)).Select(t => t.Lang + ": " + t.Text))
                    : n.Text + (string.IsNullOrEmpty(n.FileName) ? "" : "   [" + n.FileName + "]");
                string state = n.Kind == NoteKind.Comment ? "" : n.State == NoteState.Proposed ? T("Nts_Proposed", "proposed")
                             : n.Kind == NoteKind.Translation ? T("Nts_InForce", "in force") : T("Nts_InList", "in the list");
                _all.Rows.Add(When(n), n.Author, KindText(n), state, n.ControlName, body, n.Page);
            }
        }

        /// <summary>The feedback file made (tests read it): its path, or null.</summary>
        public string LastExport { get { return _lastExport; } }

        /// <summary>One click: every note of the panel, with the helpers' files, in one .zip in the feedback folder.</summary>
        public void Export()
        {
            try
            {
                _lastExport = NotesStore.Export(FeedbackFolder, _panelName());
                _status.Text = T("Nts_Exported", "Feedback file made") + ": " + _lastExport;
                Kaban.UI.Controls.UiStyle.SetTip(_tips, _status, _lastExport);
            }
            catch (Exception ex)
            {
                _status.Text = T("Nts_ExportFailed", "The feedback file could not be made") + ": " + ex.Message;
            }
            _folder.Enabled = _lastExport != null && SecurityManager.HasAccess(SecurityLevel.Maintenance);
        }

        private void ShowFolder()
        {
            if (_lastExport == null || !SecurityManager.HasAccess(SecurityLevel.Maintenance)) return;
            try { Process.Start("explorer.exe", "/select,\"" + _lastExport + "\""); } catch (Exception ex) { _status.Text = ex.Message; }
        }

        /// <summary>The bar's text (tests).</summary>
        internal string StatusText { get { return _status.Text; } }
        /// <summary>The page's content (tests).</summary>
        internal Control Body { get { return _body; } }
    }
}
