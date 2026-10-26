using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Kaban.UI.Settings;

namespace Kaban.UI.Screens
{
    /// <summary>
    /// NOTES AND FEEDBACK, a side page: while it is open a tap or a click on any
    /// control of the page picks that control - nothing is operated, no jog, no
    /// write to the PLC - and the page shows it: what it is and where, its
    /// tooltip (a touch screen has no pointer to rest on a control), its value
    /// (a value shown: what it should be), its texts in the languages the user
    /// chose, an Admin's table of its settings, its helpers and its comments.
    /// Anyone signed in writes notes, each with its reason; Maintenance's and an
    /// Admin's translations and helpers are in force at once (on the screen, in
    /// the control's "?" or helper mark), an Operator's are proposals that
    /// Maintenance or an Admin accepts. One button makes the feedback file of
    /// every note on the panel, with the helpers' files, to send to the builder.
    ///
    /// 8 October:
    ///  - a table's heading picks the heading (its text to translate); a cell
    ///    shown only picks its value; a cell to type in is typed in, not picked;
    ///  - the menu's page buttons and a page's tabs are picked like the rest, not
    ///    used (22 October: "the navigation buttons work as function buttons even
    ///    with Picking on - they should turn to feedback mode as well; the user
    ///    changes the picking mode to change the screen"); the caption bar is
    ///    picked as the page is;
    ///  - texts in a table: now / your suggestion / reason (the reason once
    ///    something is suggested), in the user's chosen languages only;
    ///  - an Admin's table of the control's settings (addresses ...): now /
    ///    required / comment, for the builder;
    ///  - general notes, on no control, with a file.
    /// </summary>
    public class NotesPage : UserControl, ISidePageWidth
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private readonly Func<Control, bool> _pickable;
        private readonly Func<Control, bool> _passThrough;
        private readonly Func<Control, string> _placeOf;
        private readonly Func<string> _panelName;
        private readonly ToolTip _tips = UiStyle.Tips();

        // 26 October: "We should make the page just like our other page designs" - the notes in a table with
        // the buttons on top of it (Add, a general note, View, Edit, Find, Remove, the feedback file), a note
        // or the control picked in the next tab
        private readonly Button _add, _addMore, _view, _edit, _findNote, _remove;
        private readonly Label _status = new Label { AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(70, 76, 86) };
        private readonly TabControl _views = new TabControl { Dock = DockStyle.Fill };
        private readonly TabPage _listTab, _detailsTab;

        // 26 October: "Add some filter elements for the table as well btw so I can check though the notes easier"
        private readonly TextBox _search = new TextBox { Name = "notesSearch" };
        private readonly ComboBox _kindFilter = new ComboBox { Name = "notesKindFilter", DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _stateFilter = new ComboBox { Name = "notesStateFilter", DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _whoFilter = new ComboBox { Name = "notesWhoFilter", DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Timer _typing = new Timer { Interval = 300 };
        private bool _fillingFilters;

        /// <summary>A filter's choice: what it keeps, what it reads.</summary>
        private sealed class Choice
        {
            public readonly string Value, Text;
            public Choice(string value, string text) { Value = value; Text = text; }
            public override string ToString() { return Text; }
        }
        private readonly FlowLayoutPanel _body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            BackColor = Color.White, Padding = new Padding(8, 6, 8, 12)
        };
        private readonly DataGridView _all = new DataGridView { Dock = DockStyle.Fill, Name = "notesAll" };
        private readonly Button _export, _folder, _general;
        private readonly Label _count = new Label { AutoSize = true, ForeColor = Color.FromArgb(110, 116, 124), Margin = new Padding(10, 12, 0, 0) };

        private bool _picking;
        private Picker _picker;
        private readonly PickShade _shade;

        /// <summary>The parts of the window a tap picks in - darker while picking (the page on show, the caption bar). Null: no shade.</summary>
        public Func<IEnumerable<Control>> ShadeAreas;

        /// <summary>Opens HMI Settings > Translation on a key ("" or why not) - the texts' link. Null: no link.</summary>
        public Func<string, string> OpenTranslationTable;

        /// <summary>Opens a use's page and points at it ("" or why not) - Find's link. Null: Find lists only.</summary>
        public Func<DeviceCollector.Use, string> OpenUse;

        /// <summary>Where a control's page is, kept on its note (the main window's place) - the note's Find goes there. Null: none.</summary>
        public Func<Control, string> LocationOf;

        /// <summary>A note's control shown: its page opened, the control in sight and flashing ("" or why not). Null: no Find.</summary>
        public Func<Note, string> ShowNoted;

        /// <summary>Tests: Find's window given to it instead of shown.</summary>
        public static Func<NotesFindForm, DialogResult> FindShowing;

        private readonly Button _find;

        /// <summary>The shade while picking (tests).</summary>
        internal PickShade Shade { get { return _shade; } }

        /// <summary>What was picked: a control, a table's heading or cell, a page button of the menu, a tab - or a general note.</summary>
        public enum PickKind { None, Control, Heading, Cell, Menu, Tab, General }

        // the control picked: as it was when picked (it may be gone since - a page built again)
        private PickKind _kind = PickKind.None;
        private Control _picked;
        private string _key, _name, _place, _tip, _value;
        private bool _valueShown;
        private IList<NoteTarget.TextKey> _keys = new NoteTarget.TextKey[0];
        private string _file;          // a helper's (a general note's) file, chosen, not yet kept
        private string _lastExport;

        /// <summary>What the details tab shows: nothing yet, the control picked, a general note, a note viewed or edited.</summary>
        internal enum DetailMode { None, Control, General, View, Edit }
        private DetailMode _mode = DetailMode.None;
        private Note _note;            // the note viewed or edited

        /// <summary>
        /// WHAT IS TYPED, kept while the page fills again (a note kept, a language chosen, another user) - by the
        /// box's name, a table's cell by its table, row and column - and what each was when it was made (26 October:
        /// "Clicking to a control discards everything on the notes page directly and it causes to data loss").
        /// </summary>
        private readonly Dictionary<string, string> _drafts = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _originals = new Dictionary<string, string>();
        private bool _fileChanged;     // an edited note's file changed (or taken off)

        /// <summary>The details tab's mode (tests).</summary>
        internal DetailMode Mode { get { return _mode; } }
        /// <summary>The note viewed or edited (tests).</summary>
        internal Note NoteShown { get { return _note; } }

        /// <summary>True when something typed here is not kept yet.</summary>
        internal bool Dirty
        {
            get
            {
                if (_file != null || _fileChanged) return true;
                foreach (var kv in _drafts)
                {
                    string was;
                    if (!_originals.TryGetValue(kv.Key, out was)) was = "";
                    if ((kv.Value ?? "").Trim() != (was ?? "").Trim()) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Before the details show something else: what was typed and not kept is thrown away only when the
        /// user says so (26 October: "We should warn the user before discarding the previous one"). True: go on.
        /// </summary>
        internal bool MayDiscard()
        {
            if (!Dirty) { ForgetDrafts(); return true; }
            // 27 October: "Change ... to 'There is unsaved note entry' and add a button so the user could save it directly
            // from that message box as well (without needing to go back to 'Details' page)"
            int answer = Kaban.UI.Profiles.Ask.Choose(FindForm(), T("Ttl_Notes", "Notes and feedback"),
                T("Nts_AskUnsaved", "There is an unsaved note entry."),
                T("Nts_BtnSaveIt", "Save it"), T("Nts_BtnDiscardIt", "Discard it"), T("Btn_Cancel", "Cancel"));
            if (answer == 0)
            {
                if (!SaveDrafts())
                {
                    _views.SelectedTab = _detailsTab;
                    if (!_status.Text.StartsWith(T("Nts_NotAllSaved", "Not everything typed could be kept"), StringComparison.Ordinal))
                        _status.Text = T("Nts_NotAllSaved", "Not everything typed could be kept") + " - " + _status.Text;
                    return false;
                }
                ForgetDrafts();
                return true;
            }
            if (answer == 1)
            {
                ForgetDrafts();
                Fill();                          // the boxes empty again (an edited note: as it is)
                return true;
            }
            _status.Text = T("Nts_Kept", "What you typed is still there - keep it, or discard it.");
            _views.SelectedTab = _detailsTab;
            return false;
        }

        /// <summary>
        /// Everything typed in the details kept as notes (the message's Save it): the control's value, texts,
        /// settings, helper and comment; a general note; an edited note. True when nothing is left unsaved.
        /// </summary>
        internal bool SaveDrafts()
        {
            if (_mode == DetailMode.Edit) return _saveEdit != null && _saveEdit();
            string v;
            Func<string, string> d = k => _drafts.TryGetValue(k, out v) ? (v ?? "").Trim() : "";
            if (_mode == DetailMode.General)
            {
                if (d("box:notesGeneralText").Length > 0 || _file != null) KeepGeneral(d("box:notesGeneralText"));
                return !Dirty;
            }
            if (_mode != DetailMode.Control) return !Dirty;
            // every table's entries taken first: each note kept fills the page again
            var texts = _textGrids.Where(kv => !kv.Value.IsDisposed).Select(kv => new KeyValuePair<string, List<string[]>>(kv.Key, Asked(kv.Value))).ToList();
            var props = _propsGrid != null && !_propsGrid.IsDisposed ? Asked(_propsGrid) : new List<string[]>();
            string valueRequired = d("box:notesValueRequired"), valueWhy = d("box:notesValueComment");
            string helper = d("box:notesHelperText"), helperWhy = d("box:notesHelperComment"), comment = d("box:notesCommentText");
            bool helperFile = _file != null;
            if (valueRequired.Length > 0) KeepValue(valueRequired, valueWhy);
            foreach (var t in texts) if (t.Value.Any(a => a[2] != a[1].Trim())) KeepTextsRows(t.Key, t.Value);
            if (props.Any(a => a[2] != a[1])) KeepPropertiesRows(props);
            if (helper.Length > 0 || helperFile) KeepHelper(helper, helperWhy);
            if (comment.Length > 0) AddComment(comment);
            return !Dirty;
        }

        // the tables of the control picked, for Save it (filled with the page)
        private readonly Dictionary<string, DataGridView> _textGrids = new Dictionary<string, DataGridView>();
        private DataGridView _propsGrid;
        private Func<bool> _saveEdit;

        private void ForgetDrafts()
        {
            _drafts.Clear();
            _originals.Clear();
            _file = null;
            _fileChanged = false;
        }

        /// <summary>The drafts of a box or a table kept: forgotten (they are a note now).</summary>
        private void Kept(params string[] prefixes)
        {
            foreach (var k in _drafts.Keys.Where(k => prefixes.Any(p => k.StartsWith(p, StringComparison.Ordinal))).ToList()) { _drafts.Remove(k); _originals.Remove(k); }
        }

        /// <summary>The feedback files' folder (Documents\Kaban HMI\Feedback); replaceable for tests.</summary>
        public static string FeedbackFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Kaban HMI", "Feedback");

        public NotesPage(Func<Control, bool> pickable, Func<Control, string> placeOf, Func<string> panelName, Func<Control, bool> passThrough = null)
        {
            _pickable = pickable ?? (c => false);
            _passThrough = passThrough ?? (c => false);
            _placeOf = placeOf ?? (c => "");
            _panelName = panelName ?? (() => "HMI");
            _shade = new PickShade(() => ShadeAreas != null ? ShadeAreas() : null);
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;

            // ---- the notes: the buttons on top of their table ----
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
                Padding = new Padding(2, 4, 2, 4), BackColor = Color.White
            };
            // "We should remove 'Picking: on' button, create 'Add' button instead, when this button clicked the screen
            // should turn to picking on mode ... 'Esc' or other clicks on this menu turns the picking mode to 'off'"
            _add = UiStyle.ToolButton(E(0x2795), T("Nts_TipAdd", "Add a note: tap or click the control of the page it is about - it is not operated (Esc: give up)"),
                                      _tips, (s, e) => StartAdd());
            _add.Name = "notesAdd";
            _general = UiStyle.ToolButton(E(0x1F4DD), T("Nts_TipGeneral", "A general note - on no control: an idea, a request, a file to send"), _tips, (s, e) => PickGeneral());
            _general.Name = "notesGeneral";
            _view = UiStyle.ToolButton(E(0x1F441), T("Nts_TipView", "View the note chosen - its details in the next tab (a double click on it too)"), _tips, (s, e) => ViewNote(SelectedNote, false));
            _view.Name = "notesView";
            _edit = UiStyle.ToolButton(E(0x270F), T("Nts_TipEdit", "Edit the note chosen - yours, or of a level below yours"), _tips, (s, e) => ViewNote(SelectedNote, true));
            _edit.Name = "notesEdit";
            _findNote = UiStyle.ToolButton(E(0x1F50D), T("Nts_TipFindNote", "Find its control: the page of the note chosen opened, the control shown"), _tips, (s, e) => FindNoted(SelectedNote));
            _findNote.Name = "notesFindNote";
            // 27 October: "it should remove all the selected ones"
            _remove = UiStyle.ToolButton(E(0x1F5D1), T("Nts_TipRemoveNotes", "Remove the notes chosen - yours, or of a level below yours"), _tips, (s, e) => RemoveNotes(SelectedNotes));
            _remove.Name = "notesRemoveNote";
            // the bin's key (Delete) is not taken here: a box of the page is typed in
            Kaban.UI.Helpers.Shortcuts.Set(_remove, Keys.None, _tips, T("Nts_TipRemoveNotes", "Remove the notes chosen - yours, or of a level below yours"));
            _export = UiStyle.ToolButton(E(0x1F4E4), T("Nts_TipExport", "Make the feedback file: every note of this panel, with the helpers' files, in one .zip to send"), _tips, (s, e) => Export());
            _export.Name = "notesExport";
            _folder = UiStyle.ToolButton(E(0x1F4C2), T("Nts_TipFolder", "Show the feedback file in its folder (Maintenance, Admin)"), _tips, (s, e) => ShowFolder());
            _folder.Name = "notesFolder";
            foreach (var b in new Control[] { _add, _general, Gap(), _view, _edit, _findNote, _remove, Gap(), _export, _folder })
            {
                if (b is Button) b.Margin = new Padding(2, 1, 2, 1);
                bar.Controls.Add(b);
            }
            // ---- the filters, under the buttons ----
            var filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
                Padding = new Padding(2, 0, 2, 4), BackColor = Color.White, Name = "notesFilters"
            };
            _search.Width = UiScale.For(this, 170);
            _search.Margin = new Padding(0, 4, 6, 2);
            UiStyle.SetTip(_tips, _search, T("Tip_NtsSearch", "Every word must appear - in the control, the text, the page, who wrote it, in any case"));
            _search.TextChanged += (s, e) => { if (_fillingFilters) return; _typing.Stop(); _typing.Start(); };
            _typing.Tick += (s, e) => { _typing.Stop(); FillAll(); };
            foreach (var box in new[] { _kindFilter, _stateFilter, _whoFilter })
            {
                box.Margin = new Padding(0, 4, 6, 2);
                box.SelectedIndexChanged += (s, e) => { if (!_fillingFilters) FillAll(); };
            }
            _kindFilter.Width = UiScale.For(this, 120);
            _stateFilter.Width = UiScale.For(this, 130);
            _whoFilter.Width = UiScale.For(this, 120);
            UiStyle.SetTip(_tips, _kindFilter, T("Tip_NtsKindFilter", "Only the notes of one kind: comments, general notes, texts, helpers, values, settings"));
            UiStyle.SetTip(_tips, _stateFilter, T("Tip_NtsStateFilter", "Only the proposals to accept, or the texts and helpers in force"));
            UiStyle.SetTip(_tips, _whoFilter, T("Tip_NtsWhoFilter", "Only the notes of one user - or yours"));
            _fillingFilters = true;
            try
            {
                foreach (var c in new[] { new Choice(null, T("Nts_AllKinds", "Every kind")), new Choice("Comment", T("Nts_KindComment", "Comment")),
                                          new Choice("General", T("Nts_KindGeneral", "General")), new Choice("Translation", T("Nts_KindText", "Texts")),
                                          new Choice("Helper", T("Nts_KindHelper", "Helper")), new Choice("Value", T("Nts_KindValue", "Value")),
                                          new Choice("Property", T("Nts_KindProperty", "Settings")) })
                    _kindFilter.Items.Add(c);
                foreach (var c in new[] { new Choice(null, T("Nts_AllStates", "Every state")), new Choice("Proposed", T("Nts_StateProposed", "Proposed - to accept")),
                                          new Choice("Applied", T("Nts_StateInForce", "In force, in the list")) })
                    _stateFilter.Items.Add(c);
                _kindFilter.SelectedIndex = 0;
                _stateFilter.SelectedIndex = 0;
            }
            finally { _fillingFilters = false; }
            var clear = UiStyle.ToolButton("✖", T("Tip_NtsClear", "Clear the search and the filters"), _tips, (s, e) => ClearFilters());
            clear.Name = "notesClearFilters";
            clear.Margin = new Padding(2, 2, 2, 1);
            filters.Controls.Add(new Label { Text = "🔍", AutoSize = true, Margin = new Padding(2, 7, 2, 2), ForeColor = Color.FromArgb(90, 96, 106) });
            filters.Controls.Add(_search);
            filters.Controls.Add(_kindFilter);
            filters.Controls.Add(_stateFilter);
            filters.Controls.Add(_whoFilter);
            filters.Controls.Add(clear);

            _all.DoubleClick += (s, e) => { if (_all.CurrentRow != null && _all.CurrentRow.Index >= 0) ViewNote(SelectedNote, false); };
            _all.SelectionChanged += (s, e) => UpdateButtons();

            // ---- the details: a note viewed or edited, the control picked ----
            var detailsBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false,
                                                   Padding = new Padding(2, 4, 2, 2), BackColor = Color.White };
            var back = UiStyle.ToolButton(E(0x21A9), T("Nts_TipBack", "Back to the notes"), _tips, (s, e) => _views.SelectedTab = _listTab);
            back.Name = "notesBack";
            back.Margin = new Padding(2, 1, 2, 1);
            // another note from here too: Add on the details' own bar
            _addMore = UiStyle.ToolButton(E(0x2795), T("Nts_TipAdd", "Add a note: tap or click the control of the page it is about - it is not operated (Esc: give up)"),
                                          _tips, (s, e) => StartAdd());
            _addMore.Name = "notesAddMore";
            _addMore.Margin = new Padding(2, 1, 2, 1);
            // 23 October: where the picked control's devices and texts are used (Ctrl+F)
            _find = UiStyle.ToolButton(E(0x1F50E), T("Nts_TipFind", "Where is it used? The picked control's devices and texts - or any device or translation key (Ctrl+F)"),
                                       _tips, (s, e) => OpenFind());
            _find.Name = "notesFind";
            _find.Margin = new Padding(2, 1, 2, 1);
            detailsBar.Controls.Add(back);
            detailsBar.Controls.Add(_addMore);
            detailsBar.Controls.Add(_find);

            _listTab = new TabPage(T("Nts_TabNotes", "Notes")) { BackColor = Color.White, Name = "notesList" };
            _listTab.Controls.Add(_all);
            _listTab.Controls.Add(filters);
            _listTab.Controls.Add(bar);
            _detailsTab = new TabPage(T("Nts_TabDetails", "Details")) { BackColor = Color.White, Name = "notesDetails" };
            _detailsTab.Controls.Add(_body);
            _detailsTab.Controls.Add(detailsBar);
            _views.TabPages.Add(_listTab);
            _views.TabPages.Add(_detailsTab);
            BuildAllTable();

            // ---- the foot: what is going on, how many notes ----
            var foot = new Panel { Dock = DockStyle.Bottom, Height = UiScale.For(this, 30), BackColor = Color.White, Padding = new Padding(6, 0, 6, 2) };
            _status.Dock = DockStyle.Fill;
            _count.AutoSize = false;
            _count.Dock = DockStyle.Right;
            _count.Width = UiScale.For(this, 150);
            _count.TextAlign = ContentAlignment.MiddleRight;
            _count.Margin = Padding.Empty;
            foot.Controls.Add(_status);
            foot.Controls.Add(_count);

            Controls.Add(_views);
            Controls.Add(foot);

            _body.Resize += (s, e) => FitWidths();
            NotesStore.Changed += OnNotesChanged;
            SecurityManager.UserChanged += OnUserChanged;
            ViewPreferences.Changed += OnPreferencesChanged;
            VisibleChanged += (s, e) => UpdatePicker();
            ParentChanged += (s, e) => { WatchParents(); UpdatePicker(); };
            HandleCreated += (s, e) => UpdatePicker();
            Disposed += (s, e) =>
            {
                NotesStore.Changed -= OnNotesChanged;
                SecurityManager.UserChanged -= OnUserChanged;
                ViewPreferences.Changed -= OnPreferencesChanged;
                _typing.Dispose();
                RemovePicker();
                foreach (var w in _watched) { w.VisibleChanged -= OnAncestorChanged; w.ParentChanged -= OnAncestorMoved; }
                _shade.Dispose();
            };
            SetPicking(false);
            FillAll();
            Fill();
        }

        private static string E(int codePoint) { return char.ConvertFromUtf32(codePoint); }

        private static Control Gap() { return new Label { Width = 12, Height = 8, Margin = Padding.Empty }; }

        public int PreferredWidth(int available) { return Math.Min(available, UiScale.For(this, 560)); }

        // ==================================================================
        // PICKING
        // ==================================================================

        /// <summary>True while a tap picks a control (tests).</summary>
        public bool Picking { get { return _picking && _picker != null && Shown(); } }

        /// <summary>The picked control's key and name, and what it is (tests).</summary>
        public string PickedKey { get { return _key; } }
        public string PickedName { get { return _name; } }
        public PickKind PickedKind { get { return _kind; } }
        public string PickedPlace { get { return _place; } }

        /// <summary>
        /// Add: an unsaved entry asked about first - saved, thrown away or kept (27 October: "We should open this
        /// message box right when '+' button clicked to add a note btw, no need to wait until the user select the
        /// control as well") - then picking on.
        /// </summary>
        internal void StartAdd()
        {
            if (_picking) { SetPicking(true); return; }
            if (!MayDiscard()) return;
            SetPicking(true);
        }

        /// <summary>
        /// Picking on (Add) or off (a control picked, Esc, a click on this page, the page closed). While on, a
        /// tap on a control of the page picks it - it is not operated - and the rest of the window is darker.
        /// </summary>
        internal void SetPicking(bool on)
        {
            bool was = _picking;
            _picking = on;
            _add.BackColor = _addMore.BackColor = on ? Color.FromArgb(218, 232, 250) : Color.White;
            if (on) _status.Text = T("Nts_StatusAdd", "Tap or click the control the note is about (Esc: give up).");
            else if (was) _status.Text = T("Nts_StatusOff", "The page works as usual.");
            UpdatePicker();
        }

        /// <summary>On a window on show: the page and every one it is in visible (a page taken out of its window - its window closed - is not).</summary>
        private bool Shown()
        {
            if (IsDisposed || !IsHandleCreated) return false;
            Control p = this;
            for (; p.Parent != null; p = p.Parent) if (!p.Visible) return false;
            return p is Form && p.Visible && !p.IsDisposed;
        }

        private bool _wasShown;
        private readonly List<Control> _watched = new List<Control>();

        /// <summary>The panels and the window the page is in, watched: one hidden or closed closes the page (not every system tells the page itself).</summary>
        private void WatchParents()
        {
            foreach (var w in _watched) { w.VisibleChanged -= OnAncestorChanged; w.ParentChanged -= OnAncestorMoved; }
            _watched.Clear();
            for (var p = Parent; p != null; p = p.Parent)
            {
                p.VisibleChanged += OnAncestorChanged;
                p.ParentChanged += OnAncestorMoved;
                _watched.Add(p);
            }
        }

        private void OnAncestorChanged(object sender, EventArgs e) { UpdatePicker(); }
        private void OnAncestorMoved(object sender, EventArgs e) { WatchParents(); UpdatePicker(); }

        /// <summary>
        /// The page closed - its side panel closed, another page in it, its window closed - picking off (22
        /// October: "the picking mode should turn off automatically when the notes page is closed"); opened,
        /// it stays off until Add (26 October).
        /// </summary>
        private void UpdatePicker()
        {
            bool shown = Shown();
            if (shown != _wasShown)
            {
                _wasShown = shown;
                if (!shown && _picking) { SetPicking(false); return; }
            }
            if (_picking && Shown()) { if (_picker == null) { _picker = new Picker(this); Application.AddMessageFilter(_picker); } }
            else RemovePicker();
            // the rest of the window darker while a tap picks (21 October)
            _shade.On = _picking && Shown() && ShadeAreas != null;
        }

        private void RemovePicker()
        {
            if (_shade != null) _shade.On = false;
            if (_picker == null) return;
            Application.RemoveMessageFilter(_picker);
            _picker = null;
        }

        /// <summary>The control under the pointer (shown in the bar; tests).</summary>
        internal void Pointing(Control c)
        {
            if (c == null) return;
            _status.Text = T("Nts_Pointing", "Under the pointer") + ": " + NoteTarget.NameOf(c);
            _shade.Hole = c;                 // bright in the shade
        }

        private void Reset()
        {
            _note = null;
            _picked = null;
            _key = _name = _place = _tip = _value = null;
            _valueShown = false;
            _keys = new NoteTarget.TextKey[0];
            _file = null;
        }

        /// <summary>A control picked (a tap; tests call it): shown on the page.</summary>
        public void Pick(Control c)
        {
            if (c == null) return;
            // one control an Add: picking off, the page usable again
            SetPicking(false);
            if (!MayDiscard()) return;
            Reset();
            _picked = c;
            _key = NoteTarget.KeyOf(c);
            _name = NoteTarget.NameOf(c);
            _place = _placeOf(c);
            _tip = UiStyle.TipOf(c);
            _keys = NoteTarget.KeysOf(c);
            _kind = PickKind.Control;
            // A VALUE SHOWN: what it should be is noted (a field to type in is typed in)
            if (IsShownValue(c)) { _valueShown = true; _value = ValueOf(c); }
            if (SettingsScreen.IsTab(c)) PickedTab(c);
            ShowPicked(c);
        }

        /// <summary>A page button of the menu, or a tab (of a TabControl: the one at tabIndex, -1: the open one), picked - not used.</summary>
        internal void PickPassed(Control c, int tabIndex = -1)
        {
            if (c == null || c.IsDisposed) return;
            SetPicking(false);
            if (!MayDiscard()) return;
            Reset();
            _picked = c;
            _key = NoteTarget.KeyOf(c);
            _tip = UiStyle.TipOf(c);
            _keys = NoteTarget.KeysOf(c);
            var tabs = c as TabControl;
            if (SettingsScreen.IsTab(c)) { _name = NoteTarget.NameOf(c); PickedTab(c); }
            else if (tabs != null)
            {
                _kind = PickKind.Tab;
                int index = tabIndex >= 0 && tabIndex < tabs.TabPages.Count ? tabIndex : tabs.SelectedIndex;
                var page = index >= 0 && index < tabs.TabPages.Count ? tabs.TabPages[index] : null;
                _name = T("Nts_Tab", "Tab") + ": " + (page != null ? page.Text : "");
                _key += page != null ? "#tab:" + (string.IsNullOrEmpty(page.Name) ? index.ToString(CultureInfo.InvariantCulture) : page.Name) : "";
                _keys = new NoteTarget.TextKey[0];
            }
            else
            {
                _kind = PickKind.Menu;
                _name = T("Nts_MenuButton", "Menu button") + ": " + NoteTarget.NameOf(c);
            }
            _place = _placeOf(c);
            ShowPicked(c);
        }

        private void PickedTab(Control tab)
        {
            _kind = PickKind.Tab;
            var page = SettingsScreen.PageOfTab(tab);
            _name = T("Nts_Tab", "Tab") + ": " + (page != null ? LanguageManager.GetString(page.TitleKey ?? "", page.TitleFallback ?? "") : tab.Text);
            if (page != null && !string.IsNullOrEmpty(page.TitleKey))
                _keys = new[] { new NoteTarget.TextKey { What = T("Nts_KeyTitle", "Title"), Key = page.TitleKey } };
        }

        /// <summary>A table's heading or cell picked (a tap on it; tests call it).</summary>
        public void PickCell(DataGridView grid, int column, int row)
        {
            if (grid == null || column < 0 || column >= grid.Columns.Count) return;
            SetPicking(false);
            if (!MayDiscard()) return;
            Reset();
            var col = grid.Columns[column];
            string heading = PlainHeading(col.HeaderText);
            string table = NoteTarget.NameOf(grid);
            _picked = grid;
            _place = _placeOf(grid);
            string colName = string.IsNullOrEmpty(col.Name) ? "c" + column.ToString(CultureInfo.InvariantCulture) : col.Name;
            if (row < 0)
            {
                // THE HEADING: its text to translate
                _kind = PickKind.Heading;
                _key = NoteTarget.KeyOf(grid) + "#col:" + colName;
                _name = T("Nts_Heading", "Column heading") + ": " + heading + "  (" + table + ")";
                _tip = col.ToolTipText;
                var key = col.Tag as string;
                if (string.IsNullOrEmpty(key) || !LanguageManager.HasKey(key)) key = LanguageManager.KeyOfText(heading);
                _keys = string.IsNullOrEmpty(key) ? new NoteTarget.TextKey[0] : new[] { new NoteTarget.TextKey { What = T("Nts_KeyHeading", "Column heading"), Key = key } };
            }
            else
            {
                // A CELL SHOWN: its value as it should be (the row named by its first cell)
                _kind = PickKind.Cell;
                var r = grid.Rows[row];
                string first = "";
                foreach (DataGridViewColumn c in grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex))
                {
                    var v = Convert.ToString(r.Cells[c.Index].FormattedValue, CultureInfo.CurrentCulture);
                    if (!string.IsNullOrWhiteSpace(v) && c.Index != column) { first = v; break; }
                }
                _key = NoteTarget.KeyOf(grid) + "#col:" + colName + "#row:" + (first.Length > 0 ? first : row.ToString(CultureInfo.InvariantCulture));
                _name = table + " - " + heading + (first.Length > 0 ? " (" + first + ")" : " (" + T("Nts_Row", "row") + " " + (row + 1) + ")");
                _value = Convert.ToString(r.Cells[column].FormattedValue, CultureInfo.CurrentCulture);
                _valueShown = true;
                _tip = r.Cells[column].ToolTipText;
            }
            ShowPicked(grid);
        }

        /// <summary>A general note (on no control).</summary>
        public void PickGeneral()
        {
            SetPicking(false);
            if (!MayDiscard()) return;
            Reset();
            _kind = PickKind.General;
            _mode = DetailMode.General;
            _key = "";
            _name = T("Nts_General", "General note");
            _status.Text = T("Nts_GeneralHint", "A note on no control: an idea, a request - with a file if you like.");
            _views.SelectedTab = _detailsTab;
            Fill();
        }

        private void ShowPicked(Control flash)
        {
            _status.Text = T("Nts_Picked", "Picked") + ": " + _name;
            if (flash != null && !flash.IsDisposed) _shade.Hole = flash;
            try { if (flash != null && !flash.IsDisposed) ControlHighlight.Flash(flash, 1500); } catch { }
            _mode = DetailMode.Control;
            _views.SelectedTab = _detailsTab;
            Fill();
        }

        private static string PlainHeading(string text)
        {
            return (text ?? "").Replace("▲", "").Replace("▼", "").Trim();
        }

        /// <summary>A control that only shows a value (not one typed in): a display, a meter, a lamp.</summary>
        internal static bool IsShownValue(Control c)
        {
            if (c == null) return false;
            if (c is NumericInputHMI || c is TextBoxBase || c is ButtonBase || c is ListControl) return false;
            return c is NumericDisplayHMI || c is UiStyle.IDisplayOnly || c is BaseNumericHMI;
        }

        private static string ValueOf(Control c)
        {
            var text = (c.Text ?? "").Trim();
            if (text.Length > 0) return text;
            var acc = c.AccessibleDescription;
            return string.IsNullOrEmpty(acc) ? "" : acc;
        }

        /// <summary>True when a table's cell is typed in (it is not picked: the tap is the table's).</summary>
        internal static bool IsEntryCell(DataGridView grid, int column, int row)
        {
            if (grid == null || row < 0 || column < 0 || column >= grid.Columns.Count || row >= grid.Rows.Count) return false;
            if (grid.ReadOnly || !grid.Enabled) return false;
            var col = grid.Columns[column];
            if (col.ReadOnly) return false;
            var cell = grid.Rows[row].Cells[column];
            return !cell.ReadOnly;
        }

        /// <summary>
        /// The taps while picking: a press, a release, a double click or the keys
        /// that press a button, on a control of the page, are kept from it; the
        /// release picks it - the menu's page buttons and a page's tabs too, not
        /// used (22 October). A table's cell to type in is let through. The pointer
        /// moving is let through (its tooltip, its border) and shown in the bar.
        /// </summary>
        private sealed class Picker : IMessageFilter
        {
            private const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_LBUTTONDBLCLK = 0x0203,
                              WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_RBUTTONDBLCLK = 0x0206,
                              WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208, WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;
            private readonly NotesPage _page;
            private Control _last;

            public Picker(NotesPage page) { _page = page; }

            private static Point PointOf(Message m)
            {
                long l = (long)m.LParam;
                return new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF));
            }

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
                // Esc: picking given up (26 October)
                if ((m.Msg == WM_KEYDOWN || m.Msg == WM_KEYUP) && (Keys)(int)(long)m.WParam == Keys.Escape)
                {
                    if (m.Msg == WM_KEYDOWN) Later(() => _page.SetPicking(false));
                    return true;
                }
                var hit = Control.FromChildHandle(m.HWnd);
                if (hit == null) return false;
                // a click on this page (but on Add): picking off - the click is the page's own
                if (m.Msg == WM_LBUTTONDOWN || m.Msg == WM_RBUTTONDOWN)
                    for (var c = hit; c != null; c = c.Parent)
                    {
                        if (c == _page._add || c == _page._addMore) break;
                        if (c == _page) { Later(() => _page.SetPicking(false)); return false; }
                    }

                // the menu's page buttons, a page's tabs: picked, not used - the page stays (picking
                // off goes to another page)
                var passed = PassedOf(hit);
                if (passed != null)
                {
                    switch (m.Msg)
                    {
                        case WM_MOUSEMOVE:
                            if (passed != _last) { _last = passed; _page.Pointing(passed); }
                            return false;
                        case WM_KEYDOWN: case WM_KEYUP:
                            var k = (Keys)(int)(long)m.WParam;
                            if (k != Keys.Space && k != Keys.Enter) return false;
                            if (m.Msg == WM_KEYUP) Later(passed, -1);
                            return true;
                        case WM_LBUTTONUP:
                            var tabs = passed as TabControl;
                            int tab = tabs != null ? TabAt(tabs, PointOf(m)) : -1;
                            if (tabs == null || tab >= 0) Later(passed, tab);
                            return true;
                        default:
                            return true;                    // its press, a double click: kept from it
                    }
                }

                if (m.Msg == WM_KEYDOWN || m.Msg == WM_KEYUP)
                {
                    var key = (Keys)(int)(long)m.WParam;
                    // Ctrl+F while picking: the notes page's Find, wherever the keyboard is
                    if (key == Keys.F && (Control.ModifierKeys & Keys.Control) != 0 && (Control.ModifierKeys & Keys.Alt) == 0)
                    {
                        if (m.Msg == WM_KEYDOWN) { try { _page.BeginInvoke(new Action(_page.OpenFind)); } catch { } }
                        return true;
                    }
                    if (key != Keys.Space && key != Keys.Enter) return false;
                    var focused = NoteTarget.Notable(hit);
                    if (focused == null || !_page._pickable(focused)) return false;
                    if (focused is DataGridView) return false;            // a table: its own keys
                    if (m.Msg == WM_KEYUP) Later(() => _page.Pick(focused));
                    return true;
                }
                var target2 = NoteTarget.Notable(hit);
                if (target2 == null || !_page._pickable(target2)) return false;
                if (m.Msg == WM_MOUSEMOVE)
                {
                    if (target2 != _last) { _last = target2; _page.Pointing(target2); }
                    return false;
                }
                // A TABLE: its heading or a cell shown is picked, a cell to type in is the table's
                var grid = target2 as DataGridView;
                if (grid != null && hit == grid)
                {
                    var at = PointOf(m);
                    var info = grid.HitTest(at.X, at.Y);
                    if (info.Type == DataGridViewHitTestType.Cell && IsEntryCell(grid, info.ColumnIndex, info.RowIndex)) return false;
                    if (info.Type == DataGridViewHitTestType.ColumnHeader || info.Type == DataGridViewHitTestType.Cell)
                    {
                        int col = info.ColumnIndex, row = info.Type == DataGridViewHitTestType.Cell ? info.RowIndex : -1;
                        if (m.Msg == WM_LBUTTONUP) Later(() => _page.PickCell(grid, col, row));
                        return true;
                    }
                    // the scroll bars, the empty part: the table's own
                    return false;
                }
                if (grid != null) return false;                    // its editing box: typed in
                // after the filter (26 October): the pick may ask whether to discard what was typed
                if (m.Msg == WM_LBUTTONUP) Later(() => _page.Pick(target2));
                return true;
            }

            /// <summary>
            /// The page button or tab a tap is on, or null. Never one of the notes
            /// page's own (8 October: "any click on the page shakes it for a few
            /// seconds and it turns to something else" - a tap on the notes page's
            /// own fields met its own tab strip first, on its way up, and was noted
            /// as a tab of the page: the page filled again and showed that tab).
            /// </summary>
            private Control PassedOf(Control hit)
            {
                for (var c = hit; c != null; c = c.Parent)
                    if (c is NotesPage) return null;
                // a TabControl's own: its row of tabs (a control on one of its pages is the page's)
                for (var c = hit; c != null; c = c.Parent)
                    if (_page._passThrough(c) && (!(c is TabControl) || c == hit)) return c;
                return null;
            }

            private void Later(Control c, int tab) { Later(() => _page.PickPassed(c, tab)); }

            private void Later(Action a)
            {
                try { _page.BeginInvoke(a); } catch { }
            }

            /// <summary>The tab at a point of its TabControl (-1: none).</summary>
            internal static int TabAt(TabControl tabs, Point at)
            {
                for (int i = 0; i < tabs.TabCount; i++)
                    if (tabs.GetTabRect(i).Contains(at)) return i;
                return -1;
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
            FillAll();
        }

        private void OnUserChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnUserChanged), sender, e); } catch { } return; }
            Fill();
            UpdateButtons();
        }

        private void OnPreferencesChanged(object sender, EventArgs e)
        {
            if (IsDisposed || _fillingLanguages) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnPreferencesChanged), sender, e); } catch { } return; }
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
                var row = c as FlowLayoutPanel;
                if (row != null) row.MaximumSize = new Size(w, 0);
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
            // what was typed in it before the page filled again (26 October)
            string key = "box:" + name, draft;
            _originals[key] = text ?? "";
            var b = new TextBox
            {
                Text = _drafts.TryGetValue(key, out draft) ? draft : text ?? "", Multiline = multi, Height = multi ? UiScale.For(this, 64) : UiScale.For(this, 26), Width = InnerWidth,
                Tag = "full", Enabled = enabled, Name = name, Margin = new Padding(0, 2, 0, 4), ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None
            };
            b.TextChanged += (s, e) => _drafts[key] = b.Text;
            _body.Controls.Add(b);
            return b;
        }

        /// <summary>A caption and a box beside it (the comment of a note: why).</summary>
        private TextBox CommentBox(string name, bool enabled)
        {
            AddText(T("Nts_Why", "Reason - for the builder:"), false, Color.FromArgb(90, 96, 106), 4);
            return Box("", false, enabled, name);
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
                foreach (Control c in _body.Controls.Cast<Control>().ToList()) { EndEdits(c); _body.Controls.Remove(c); c.Dispose(); }
                bool write = NotesStore.CanWrite, apply = NotesStore.CanApply;
                _folder.Enabled = _lastExport != null && SecurityManager.HasAccess(SecurityLevel.Maintenance);
                _find.Visible = _mode == DetailMode.Control;
                _textGrids.Clear();
                _propsGrid = null;
                _saveEdit = null;
                if (_mode == DetailMode.View || _mode == DetailMode.Edit) { FillNote(_mode == DetailMode.Edit); return; }
                if (_mode == DetailMode.General) { FillGeneral(write); return; }
                if (_mode == DetailMode.None || _key == null)
                {
                    AddText(T("Nts_DetailsNone", "Nothing chosen yet."), true, null, 6);
                    AddText(T("Nts_DetailsHow", "A note of the list: View or Edit it. A new one: Add, then tap or click the control it is about - a field, a button, a lamp, " +
                                                "a caption, a column heading. It is not operated: what it is, its tooltip, its texts in every language, its helpers " +
                                                "and its comments appear here, and you can add yours."),
                            false, Color.FromArgb(90, 96, 106));
                    AddText(T("Nts_HowPicking", "While picking, the menu's page buttons and a page's tabs are picked too, not used. A table's cell to type in is " +
                                                "typed in. Esc, or a click on this page, gives picking up. A general note, on no control: the notepad above the list."),
                            false, Color.FromArgb(90, 96, 106), 6);
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
                if (_valueShown) FillValue(notes, write);
                FillTexts(notes, write, apply);
                if (SecurityManager.HasAccess(SecurityLevel.Admin) && _picked != null && (_kind == PickKind.Control || _kind == PickKind.Menu))
                    FillProperties(notes, write);
                if (_kind != PickKind.Heading && _kind != PickKind.Cell) FillHelpers(notes, write, apply);
                FillComments(notes, write);
            }
            finally
            {
                _body.ResumeLayout(true);
            }
            FitWidths();
        }

        /// <summary>
        /// A table of the page being typed in: its editing ended before the page fills again.
        /// 12 October, the panel's audit log (10:24:27): a suggestion was being typed when another
        /// control was tapped - .NET's DataGridView, thrown away with its editing box open, fell over
        /// (ArgumentOutOfRangeException in PositionEditingControl) and the HMI restarted.
        /// </summary>
        private static void EndEdits(Control c)
        {
            var grid = c as DataGridView;
            if (grid != null)
            {
                try { if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) grid.CancelEdit(); } catch { }
                try { grid.CurrentCell = null; } catch { }
                try { if (grid.IsCurrentCellInEditMode) { grid.CancelEdit(); grid.CurrentCell = null; } } catch { }
            }
            foreach (Control k in c.Controls) EndEdits(k);
        }

        // ---- a table: now / your suggestion / reason --------------------------------------

        /// <summary>
        /// A small table of entries - texts, settings - with what they are now, what
        /// is required and why: the comment cell opens once something is required.
        /// </summary>
        private DataGridView EntryGrid(string name, string firstHeading, IEnumerable<string[]> rows, bool write, int maxRows, string draft = null)
        {
            string id = "grid:" + (draft ?? name) + ":";
            var grid = new DataGridView
            {
                Name = name, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = false, Tag = "full", Width = InnerWidth,
                Margin = new Padding(0, 2, 0, 4), EditMode = DataGridViewEditMode.EditOnEnter, ScrollBars = ScrollBars.Vertical,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White
            };
            grid.Columns.Add("what", firstHeading);
            grid.Columns.Add("now", T("Nts_ColNow", "Now"));
            // 9 October: "change it to 'Your suggestion' instead, and 'Comment' to 'Reason'"
            grid.Columns.Add("required", T("Nts_ColRequired", "Your suggestion"));
            grid.Columns.Add("comment", T("Nts_ColReason", "Reason"));
            grid.Columns[0].FillWeight = 22; grid.Columns[1].FillWeight = 30; grid.Columns[2].FillWeight = 30; grid.Columns[3].FillWeight = 30;
            grid.Columns[0].ReadOnly = true;
            grid.Columns[1].ReadOnly = true;
            grid.Columns[2].ReadOnly = !write;
            grid.Columns[0].DefaultCellStyle.ForeColor = Color.FromArgb(70, 76, 86);
            grid.Columns[1].DefaultCellStyle.ForeColor = Color.FromArgb(90, 96, 106);
            foreach (DataGridViewColumn c in grid.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            // a row: what, now, its tag, and - an edited note's - what was asked and why
            foreach (var r in rows)
            {
                string tag = r.Length > 2 ? r[2] : r[0];
                string required = r.Length > 3 ? r[3] ?? "" : "", reason = r.Length > 4 ? r[4] ?? "" : "", d;
                _originals[id + tag + ":2"] = required;
                _originals[id + tag + ":3"] = reason;
                if (_drafts.TryGetValue(id + tag + ":2", out d)) required = d;
                if (_drafts.TryGetValue(id + tag + ":3", out d)) reason = d;
                int i = grid.Rows.Add(r[0], r[1], required, reason);
                grid.Rows[i].Tag = tag;
                bool asked = required.Trim().Length > 0;
                grid.Rows[i].Cells[3].ReadOnly = !asked || !write;
                grid.Rows[i].Cells[3].Style.BackColor = asked ? Color.White : Color.FromArgb(245, 246, 248);
            }
            // the comment once something is required; what is typed kept while the page fills again
            grid.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0 || (e.ColumnIndex != 2 && e.ColumnIndex != 3)) return;
                var row = grid.Rows[e.RowIndex];
                _drafts[id + Convert.ToString(row.Tag) + ":" + e.ColumnIndex.ToString(CultureInfo.InvariantCulture)] = Convert.ToString(row.Cells[e.ColumnIndex].Value) ?? "";
                if (e.ColumnIndex != 2) return;
                bool asked = !string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[2].Value));
                var cell = row.Cells[3];
                cell.ReadOnly = !asked || !write;
                cell.Style.BackColor = asked ? Color.White : Color.FromArgb(245, 246, 248);
            };
            int rowsShown = Math.Max(1, Math.Min(maxRows, grid.Rows.Count));
            grid.Height = grid.ColumnHeadersHeight + rowsShown * grid.RowTemplate.Height + 3;
            UiStyle.StyleGrid(grid);
            grid.Height = grid.ColumnHeadersHeight + rowsShown * grid.RowTemplate.Height + 3;
            _body.Controls.Add(grid);
            return grid;
        }

        /// <summary>The entries asked for in a table: what, now, required, comment.</summary>
        internal static List<string[]> Asked(DataGridView grid)
        {
            if (grid.IsCurrentCellInEditMode) grid.EndEdit();
            var list = new List<string[]>();
            foreach (DataGridViewRow r in grid.Rows)
            {
                string required = Convert.ToString(r.Cells[2].Value) ?? "";
                if (required.Trim().Length == 0) continue;
                list.Add(new[] { Convert.ToString(r.Tag) ?? "", Convert.ToString(r.Cells[1].Value) ?? "", required.Trim(), (Convert.ToString(r.Cells[3].Value) ?? "").Trim() });
            }
            return list;
        }

        // ---- a value shown -----------------------------------------------------------------

        private void FillValue(IList<Note> notes, bool write)
        {
            Head(T("Nts_SecValue", "Its value"));
            AddText(T("Nts_ValueNow", "Shown now") + ": " + (string.IsNullOrEmpty(_value) ? "-" : _value));
            AddText(T("Nts_ValueRequired", "Your suggestion (what it should be):"), false, Color.FromArgb(90, 96, 106), 4);
            var required = Box("", false, write, "notesValueRequired");
            var comment = CommentBox("notesValueComment", false);
            comment.Enabled = write && required.Text.Trim().Length > 0;
            required.TextChanged += (s, e) => comment.Enabled = write && required.Text.Trim().Length > 0;
            var acts = Row();
            Act(acts, "💾", T("Nts_KeepValue", "Keep the note"), T("Nts_TipKeepValue", "For the builder: this value as it should be - in the feedback file"),
                (s, e) => KeepValue(required.Text, comment.Text), "notesKeepValue", write);
            foreach (var n in notes.Where(n => n.Kind == NoteKind.Value).OrderByDescending(n => n.Time))
                NoteLine(n, NotesStore.BodyOf(n), false);
        }

        /// <summary>A value asked for (tests call it).</summary>
        internal void KeepValue(string required, string comment)
        {
            required = (required ?? "").Trim();
            if (required.Length == 0) { _status.Text = T("Nts_ValueEmpty", "Write what it should be first."); return; }
            Kept("box:notesValueRequired", "box:notesValueComment");
            var n = NotesStore.Add(Describe(new Note
            {
                Kind = NoteKind.Value,
                Values = new List<NoteValue> { new NoteValue { Name = _name, Now = _value ?? "", Required = required, Comment = (comment ?? "").Trim() } }
            }));
            if (n != null) _status.Text = T("Nts_ValueKept", "The note is kept - for the feedback file.");
        }

        /// <summary>The control's names on a note: its key, name, page, type and its way down by designer names.</summary>
        private Note Describe(Note n)
        {
            n.ControlKey = _key;
            n.ControlName = _name;
            n.Page = _place;
            if (_picked != null && !_picked.IsDisposed)
            {
                n.ControlType = _picked.GetType().FullName;
                n.ControlPath = DesignerPath(_picked);
                // its page, for the note's Find (26 October)
                if (LocationOf != null) try { n.Location = LocationOf(_picked); } catch { }
            }
            return n;
        }

        /// <summary>The control's way down by the designer's names: "AxisSettings / axRobot_X / numMin".</summary>
        internal static string DesignerPath(Control c)
        {
            var parts = new List<string>();
            for (var p = c; p != null; p = p.Parent)
            {
                if (p is Form && p.Parent == null) break;
                if (!string.IsNullOrEmpty(p.Name)) parts.Add(p.Name);
                if (p is UserControl && p.GetType().Namespace != null && p.GetType().Namespace.StartsWith("Kaban.Machines", StringComparison.Ordinal))
                {
                    parts[parts.Count - 1] = p.GetType().Name + (string.IsNullOrEmpty(p.Name) || p.Name == p.GetType().Name ? "" : "(" + p.Name + ")");
                    break;
                }
            }
            parts.Reverse();
            return string.Join(" / ", parts);
        }

        // ---- the texts in the user's languages -------------------------------------------

        /// <summary>The languages a user lists here (8 October): chosen, or theirs and English.</summary>
        public static IList<string> Languages
        {
            get
            {
                var chosen = ViewPreferences.ListOf(ViewPreferences.NoteLanguages).Select(l => l.ToUpperInvariant()).Distinct().ToList();
                if (chosen.Count > 0) return chosen;
                var langs = new List<string> { (LanguageManager.CurrentLanguage ?? "EN").ToUpperInvariant() };
                if (!langs.Contains("EN")) langs.Add("EN");
                return langs;
            }
        }

        /// <summary>Every language the program knows (the list to choose from).</summary>
        public static IList<string> AllLanguages
        {
            get
            {
                var langs = LanguageManager.AvailableLanguages.Select(l => l.ToUpperInvariant()).Distinct().ToList();
                foreach (var l in new[] { "EN", "TR", "RU", "DE", "IT", "ES" }) if (!langs.Contains(l)) langs.Add(l);
                return langs;
            }
        }

        private bool _fillingLanguages;

        private void FillTexts(IList<Note> notes, bool write, bool apply)
        {
            Head(T("Nts_SecTexts", "Its texts"));
            // the languages listed: the user's own choice (kept with their preferences)
            var langRow = Row(false);
            langRow.Controls.Add(new Label { AutoSize = true, Text = T("Nts_Languages", "Languages:"), Margin = new Padding(0, 6, 6, 0), ForeColor = Color.FromArgb(90, 96, 106) });
            var pick = new Kaban.UI.Dashboard.PickList(_tips) { Name = "notesLanguages", Size = new Size(UiScale.For(this, 220), UiScale.For(this, 26)), ApplyOnClose = true,
                                                               AllText = string.Join(", ", Languages) + "  " };
            _fillingLanguages = true;
            try
            {
                pick.SetItems(AllLanguages.Select(l => new Kaban.UI.Dashboard.PickList.Item(l, l + " - " + LanguageName(l))), false);
                pick.Select(ViewPreferences.ListOf(ViewPreferences.NoteLanguages));
            }
            finally { _fillingLanguages = false; }
            pick.Changed += (s, e) =>
            {
                if (_fillingLanguages) return;
                var keep = pick.Selected.ToList();
                BeginInvoke(new Action(() => ViewPreferences.SetList(ViewPreferences.NoteLanguages, AllLanguages.Where(keep.Contains))));
            };
            Kaban.UI.Controls.UiStyle.SetTip(_tips, pick, T("Nts_TipLanguages", "The languages listed here - yours (nothing ticked: the screen's and English)"));
            langRow.Controls.Add(pick);

            if (_keys.Count == 0) { AddText(T("Nts_NoTexts", "It has no text to translate."), false, Color.FromArgb(130, 136, 146)); return; }
            foreach (var k in _keys)
            {
                var key = k;
                AddText(key.What + "   (" + key.Key + ")", true, Color.FromArgb(60, 66, 76), 6);
                var grid = EntryGrid("notesTexts", T("Nts_ColLanguage", "Language"),
                                     Languages.Select(l => new[] { l, LanguageManager.GetStringIn(l, key.Key) ?? "", l }), write, 8, "notesTexts:" + key.Key);
                _textGrids[key.Key] = grid;
                var acts = Row();
                Act(acts, "💾", apply ? T("Nts_KeepTexts", "Keep the texts") : T("Nts_ProposeTexts", "Propose the texts"),
                    apply ? T("Nts_TipKeepTexts", "On the screen at once, for everyone on this panel - and in the feedback file")
                          : T("Nts_TipProposeTexts", "A proposal: Maintenance or an Admin puts it on the screen - and in the feedback file"),
                    (s, e) => KeepTexts(key, grid), "notesKeepTexts", write);
                // 21 October: "a link to the translation table: the item highlighted there, to edit it directly"
                if (OpenTranslationTable != null && LanguageManager.HasKey(key.Key) && SecurityManager.HasAccess(SecurityLevel.Maintenance))
                    Act(acts, "\uD83C\uDF10", T("Nts_OpenTranslation", "In the translation table"),
                        T("Nts_TipOpenTranslation", "HMI Settings > Translation on this key - its texts in every language, to edit there (picking goes off)"),
                        (s, e) => OpenTranslation(key.Key), "notesOpenTranslation");
                foreach (var n in notes.Where(n => n.Kind == NoteKind.Translation && n.TranslationKey == key.Key).OrderByDescending(n => n.Time))
                    NoteLine(n, NotesStore.BodyOf(n), apply);
            }
        }

        private static string LanguageName(string code)
        {
            switch ((code ?? "").ToUpperInvariant())
            {
                case "EN": return "English";
                case "TR": return "Türkçe";
                case "RU": return "Русский";
                case "DE": return "Deutsch";
                case "IT": return "Italiano";
                case "ES": return "Español";
                default:
                    try { return new CultureInfo(code.ToLowerInvariant()).NativeName; } catch { return code; }
            }
        }

        // ---- FIND (23 October) -------------------------------------------------------------

        /// <summary>The picked control's devices (on every controller, "X12@2" another's) and its texts' keys.</summary>
        internal List<NotesFindForm.Element> Elements()
        {
            var list = new List<NotesFindForm.Element>();
            foreach (var k in _keys)
                if (!string.IsNullOrWhiteSpace(k.Key) && !list.Any(e => e.IsText && e.Name == k.Key))
                    list.Add(new NotesFindForm.Element { IsText = true, Name = k.Key, Shown = k.Key });
            if (_picked == null || _picked.IsDisposed || (_kind != PickKind.Control && _kind != PickKind.Cell && _kind != PickKind.Heading)) return list;
            int main = MainStation();
            foreach (int st in PlcConnectionSettings.Endpoints().Select(ep => ep.StationId).Distinct())
            {
                List<DeviceCollector.Device> devices;
                try { devices = DeviceCollector.UnderControl(_picked, st); }
                catch { continue; }
                foreach (var d in devices.Take(40))
                    if (!list.Any(e => !e.IsText && e.Station == d.Station && string.Equals(e.Name, d.Address, StringComparison.OrdinalIgnoreCase)))
                        list.Add(new NotesFindForm.Element { Name = d.Address, Station = d.Station,
                                                             Shown = d.Station == main ? d.Address : d.Address + PlcAddressHelper.StationMark + d.Station });
            }
            return list;
        }

        private static int MainStation()
        {
            var first = PlcConnectionSettings.Endpoints().FirstOrDefault();
            return first != null ? first.StationId : 1;
        }

        /// <summary>
        /// Find: where the picked control's devices and texts are used - or another one typed; a use chosen,
        /// its page opened (picking off: the page is to be looked at).
        /// </summary>
        internal void OpenFind()
        {
            using (var f = new NotesFindForm(_kind == PickKind.General ? "" : _name ?? "", Elements(), MainStation()))
            {
                var r = FindShowing != null ? FindShowing(f) : f.ShowDialog(FindForm());
                if (r != DialogResult.OK || f.Chosen == null || OpenUse == null) return;
                SetPicking(false);
                var why = OpenUse(f.Chosen) ?? "";
                _status.Text = why.Length > 0 ? why : string.Format(T("Nts_FindOpened", "{0}: its page opened - picking is off."), f.Chosen.UsedAs);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F)) { OpenFind(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>The key's row of HMI Settings > Translation, picking off (the page is to be typed in).</summary>
        internal void OpenTranslation(string key)
        {
            if (OpenTranslationTable == null) return;
            SetPicking(false);
            var why = OpenTranslationTable(key) ?? "";
            _status.Text = why.Length > 0 ? why : string.Format(T("Nts_TranslationOpened", "{0} in the translation table - picking is off."), key);
        }

        private void KeepTexts(NoteTarget.TextKey key, DataGridView grid) { KeepTextsRows(key.Key, Asked(grid)); }

        private void KeepTextsRows(string key, List<string[]> asked)
        {
            var texts = new List<NoteText>();
            foreach (var a in asked)
                if (a[2] != a[1].Trim()) texts.Add(new NoteText { Lang = a[0], Text = a[2], Now = a[1], Comment = a[3].Length > 0 ? a[3] : null });
            if (texts.Count == 0) { _status.Text = T("Nts_NothingChanged", "No text changed."); return; }
            Kept("grid:notesTexts:" + key + ":");
            var n = NotesStore.Add(Describe(new Note { Kind = NoteKind.Translation, TranslationKey = key, Texts = texts }));
            if (n != null)
                _status.Text = n.State == NoteState.Applied ? T("Nts_TextsKept", "The texts are on the screen.") : T("Nts_TextsProposed", "The texts are proposed.");
        }

        // ---- an Admin's table of the control's settings -----------------------------------

        /// <summary>
        /// The control's settings an Admin may ask for (8 October: "the selected
        /// control's properties should be listed on a table ... so my colleagues
        /// could enter the right values (e.g. address property values)"): the
        /// program's own properties of the control - and of its unit, when it is a
        /// field of an axis row or a cylinder unit - that read and write as text.
        /// </summary>
        internal static List<string[]> PropertyRows(Control c)
        {
            var rows = new List<string[]>();
            if (c == null) return rows;
            AddProperties(rows, c, "");
            for (var p = c.Parent; p != null; p = p.Parent)
                if (p is AxisSettingHMI || p is CylinderUnitHMI || p is CylinderRowHMI)
                {
                    // the manual movement of a cylinder row, the unit inside an encoder's or
                    // an axis's unit: the owner's properties (what its page sets), not the
                    // unit's inside it
                    if (p is CylinderUnitHMI && (p.Parent is CylinderRowHMI || p.Parent is CylinderEncoderUnitHMI)) p = p.Parent;
                    string unit = NoteTarget.NameOf(p);
                    AddProperties(rows, p, (string.IsNullOrEmpty(unit) ? p.Name : unit) + ": ");
                    break;
                }
            return rows;
        }

        private static void AddProperties(List<string[]> rows, object o, string prefix)
        {
            foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(o, new Attribute[] { BrowsableAttribute.Yes }))
            {
                if (pd.IsReadOnly) continue;
                var owner = pd.ComponentType;
                bool ours = owner != null && owner.Assembly.FullName.StartsWith("Kaban", StringComparison.Ordinal);
                if (!ours && pd.Name != "Name" && pd.Name != "Text") continue;
                var conv = pd.Converter;
                if (conv == null || conv is ExpandableObjectConverter || !conv.CanConvertFrom(typeof(string))) continue;
                var t = Nullable.GetUnderlyingType(pd.PropertyType) ?? pd.PropertyType;
                if (!(t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal))) continue;
                string now;
                try { now = conv.ConvertToInvariantString(pd.GetValue(o)) ?? ""; } catch { continue; }
                rows.Add(new[] { prefix + pd.Name, now, prefix + pd.Name });
            }
            rows.Sort((a, b) => string.CompareOrdinal(a[0], b[0]));
        }

        private void FillProperties(IList<Note> notes, bool write)
        {
            Head(T("Nts_SecProps", "Its settings (Admin)"));
            AddText(T("Nts_PropsHint", "What a setting should be (an address, a number ...), and why: for the builder, in the feedback file - the program is not changed."),
                    false, Color.FromArgb(110, 116, 124));
            var rows = PropertyRows(_picked);
            if (rows.Count == 0) { AddText(T("Nts_NoProps", "It has no settings of the program's own."), false, Color.FromArgb(130, 136, 146)); return; }
            var grid = EntryGrid("notesProps", T("Nts_ColProperty", "Setting"), rows, write, 10);
            _propsGrid = grid;
            var acts = Row();
            Act(acts, "💾", T("Nts_KeepProps", "Keep the settings asked for"), T("Nts_TipKeepProps", "For the builder: these settings as they should be - in the feedback file"),
                (s, e) => KeepProperties(grid), "notesKeepProps", write);
            foreach (var n in notes.Where(n => n.Kind == NoteKind.Property).OrderByDescending(n => n.Time))
                NoteLine(n, NotesStore.BodyOf(n, "\n"), false);
        }

        private void KeepProperties(DataGridView grid) { KeepPropertiesRows(Asked(grid)); }

        private void KeepPropertiesRows(List<string[]> rows)
        {
            var asked = rows.Where(a => a[2] != a[1]).ToList();
            if (asked.Count == 0) { _status.Text = T("Nts_NothingChanged", "No text changed."); return; }
            Kept("grid:notesProps:");
            var n = NotesStore.Add(Describe(new Note
            {
                Kind = NoteKind.Property,
                Values = asked.Select(a => new NoteValue { Name = a[0], Now = a[1], Required = a[2], Comment = a[3].Length > 0 ? a[3] : null }).ToList()
            }));
            if (n != null) _status.Text = T("Nts_PropsKept", "The settings asked for are kept - for the feedback file.");
        }

        // ---- the helpers ------------------------------------------------------------------

        private void FillHelpers(IList<Note> notes, bool write, bool apply)
        {
            Head(T("Nts_SecHelpers", "Its helpers"));
            var helpers = notes.Where(n => n.Kind == NoteKind.Helper).OrderBy(n => n.State).ThenByDescending(n => n.Time).ToList();
            if (helpers.Count == 0) AddText(T("Nts_NoHelpers", "No helper yet."), false, Color.FromArgb(130, 136, 146));
            else AddText(T("Nts_HelpersShown", "The ones in the list are offered with the control: its \"?\" or its helper mark."), false, Color.FromArgb(110, 116, 124));
            foreach (var n in helpers) NoteLine(n, NotesStore.BodyOf(n), apply);
            var box = Box("", true, write, "notesHelperText");
            var acts = Row();
            var fileLabel = new Label { AutoSize = true, Margin = new Padding(0, 10, 8, 0), ForeColor = Color.FromArgb(90, 96, 106),
                                        Text = _file != null ? Path.GetFileName(_file) : "" };
            Act(acts, "📎", T("Nts_AddFile", "Add a file"), T("Nts_TipAddFile", "A document, a picture or a video for this control"), (s, e) =>
            {
                using (var dlg = new OpenFileDialog { Title = T("Nts_AddFile", "Add a file"), Multiselect = false })
                    if (dlg.ShowDialog(FindForm()) == DialogResult.OK) { _file = dlg.FileName; fileLabel.Text = Path.GetFileName(_file); }
            }, "notesAddFile", write);
            acts.Controls.Add(fileLabel);
            var why = CommentBox("notesHelperComment", write);
            var keep = Row();
            Act(keep, "💾", apply ? T("Nts_KeepHelper", "Keep the helper") : T("Nts_ProposeHelper", "Propose the helper"),
                apply ? T("Nts_TipKeepHelper", "In this control's helper list at once - and in the feedback file")
                      : T("Nts_TipProposeHelper", "A proposal: Maintenance or an Admin puts it in the list - and in the feedback file"),
                (s, e) => KeepHelper(box.Text, why.Text), "notesKeepHelper", write);
        }

        /// <summary>A helper kept: its text and the file chosen (tests set the file with <see cref="ChooseFile"/>).</summary>
        internal void KeepHelper(string text) { KeepHelper(text, null); }

        internal void KeepHelper(string text, string comment)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 && _file == null) { _status.Text = T("Nts_HelperEmpty", "Write the helper or add a file."); return; }
            string kept = null;
            try { if (_file != null) kept = NotesStore.KeepFile(_file); }
            catch (Exception ex) { _status.Text = T("Nts_FileFailed", "The file could not be kept") + ": " + ex.Message; return; }
            Kept("box:notesHelperText", "box:notesHelperComment");
            var chosen = _file;
            _file = null;                    // kept: the page filling again shows none chosen
            var n = NotesStore.Add(Describe(new Note
            {
                Kind = NoteKind.Helper, Text = text, File = kept, FileName = chosen != null ? Path.GetFileName(chosen) : null,
                Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()
            }));
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
            Act(acts, "💬", T("Nts_AddComment", "Add the comment"), T("Nts_TipAddComment", "What you think of this control, of this page, of the machine"),
                (s, e) => AddComment(box.Text), "notesAddComment", write);
        }

        internal void AddComment(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) { _status.Text = T("Nts_CommentEmpty", "Write the comment first."); return; }
            Kept("box:notesCommentText");
            var n = NotesStore.Add(Describe(new Note { Kind = NoteKind.Comment, Text = text }));
            if (n != null) _status.Text = T("Nts_CommentKept", "The comment is kept.");
        }

        // ---- a general note ----------------------------------------------------------------

        private void FillGeneral(bool write)
        {
            AddText(_name, true, null, 4).Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);
            AddText(T("Nts_GeneralHint", "A note on no control: an idea, a request - with a file if you like."), false, Color.FromArgb(110, 116, 124));
            if (!write) AddText(T("Nts_SignIn", "Sign in to write notes."), false, Color.FromArgb(176, 106, 0), 6);
            var box = Box("", true, write, "notesGeneralText");
            box.Height = UiScale.For(this, 110);
            var acts = Row();
            var fileLabel = new Label { AutoSize = true, Margin = new Padding(0, 10, 8, 0), ForeColor = Color.FromArgb(90, 96, 106), Text = _file != null ? Path.GetFileName(_file) : "" };
            Act(acts, "📎", T("Nts_AddFile", "Add a file"), T("Nts_TipGeneralFile", "A document, a picture or a video with the note"), (s, e) =>
            {
                using (var dlg = new OpenFileDialog { Title = T("Nts_AddFile", "Add a file"), Multiselect = false })
                    if (dlg.ShowDialog(FindForm()) == DialogResult.OK) { _file = dlg.FileName; fileLabel.Text = Path.GetFileName(_file); }
            }, "notesGeneralFile", write);
            acts.Controls.Add(fileLabel);
            var keep = Row();
            Act(keep, "💾", T("Nts_KeepGeneral", "Keep the note"), T("Nts_TipKeepGeneral", "Kept on this panel - and in the feedback file"),
                (s, e) => KeepGeneral(box.Text), "notesKeepGeneral", write);
            Head(T("Nts_SecGeneral", "General notes"));
            var general = NotesStore.All.Where(n => n.IsGeneral).OrderByDescending(n => n.Time).ToList();
            if (general.Count == 0) AddText(T("Nts_NoGeneral", "No general note yet."), false, Color.FromArgb(130, 136, 146));
            foreach (var n in general) NoteLine(n, n.Text, false);
        }

        /// <summary>A general note kept, with the file chosen (tests call it).</summary>
        internal void KeepGeneral(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 && _file == null) { _status.Text = T("Nts_CommentEmpty", "Write the comment first."); return; }
            string kept = null;
            try { if (_file != null) kept = NotesStore.KeepFile(_file); }
            catch (Exception ex) { _status.Text = T("Nts_FileFailed", "The file could not be kept") + ": " + ex.Message; return; }
            Kept("box:notesGeneralText");
            var chosen = _file;
            _file = null;
            var n = NotesStore.Add(new Note
            {
                Kind = NoteKind.Comment, Text = text, ControlKey = "", ControlName = T("Nts_General", "General note"), Page = "",
                File = kept, FileName = chosen != null ? Path.GetFileName(chosen) : null
            });
            _file = null;
            if (n != null) _status.Text = T("Nts_GeneralKept", "The general note is kept.");
        }

        /// <summary>A note: who and when, its state, its text, its file; accept and remove where allowed.</summary>
        private void NoteLine(Note n, string body, bool canAccept)
        {
            bool asks = n.Kind == NoteKind.Comment || n.Kind == NoteKind.Value || n.Kind == NoteKind.Property;
            string state = asks ? ""
                         : n.State == NoteState.Proposed ? "  ·  " + T("Nts_Proposed", "proposed")
                         : "  ·  " + (n.Kind == NoteKind.Translation ? T("Nts_InForce", "in force") : T("Nts_InList", "in the list"));
            AddText(Who(n) + state, false, n.State == NoteState.Proposed && !asks ? Color.FromArgb(176, 106, 0) : Color.FromArgb(110, 116, 124), 6);
            if (!string.IsNullOrWhiteSpace(body)) AddText(body);
            if (n.Kind == NoteKind.Helper && !string.IsNullOrWhiteSpace(n.Comment)) AddText("(" + n.Comment + ")", false, Color.FromArgb(110, 116, 124));
            var path = NotesStore.PathOf(n);
            bool accept = canAccept && n.State == NoteState.Proposed && !asks;
            if (path == null && !accept && !NotesStore.CanRemove(n)) return;
            var acts = Row();
            if (path != null)
                Act(acts, "📄", n.FileName ?? n.File, T("Nts_TipOpenFile", "Open the file"), (s, e) =>
                {
                    try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception ex) { _status.Text = ex.Message; }
                }, "notesOpenFile");
            if (accept)
                Act(acts, "✔", T("Nts_Accept", "Accept"), n.Kind == NoteKind.Translation ? T("Nts_TipAcceptText", "Put these texts on the screen")
                                                                                         : T("Nts_TipAcceptHelper", "Put this helper in the control's list"),
                    (s, e) => NotesStore.Accept(n), "notesAccept");
            if (NotesStore.CanChange(n))
                Act(acts, E(0x270F), T("Nts_Edit", "Edit"), T("Nts_TipEditOne", "Change this note - in the next tab"), (s, e) => ViewNote(n, true), "notesEditLine");
            if (NotesStore.CanRemove(n))
                Act(acts, "✖", T("Nts_Remove", "Remove"), T("Nts_TipRemove2", "Take this note away (yours, or of a level below yours)"), (s, e) =>
                {
                    if (Kaban.UI.Profiles.Ask.YesNo(FindForm(), T("Ttl_Notes", "Notes and feedback"), T("Nts_AskRemove", "Take this note away?"))) NotesStore.Remove(n);
                }, "notesRemove");
        }

        // ==================================================================
        // A NOTE VIEWED OR EDITED (26 October)
        // ==================================================================

        /// <summary>
        /// A note of the list in the details tab: viewed, or edited (yours, or of a level below yours) - what was
        /// typed elsewhere there thrown away only when the user says so.
        /// </summary>
        internal void ViewNote(Note n, bool edit)
        {
            if (n == null) return;
            if (edit && !NotesStore.CanChange(n))
            {
                _status.Text = T("Nts_NotYours", "Only its author, or a level above the author's, changes it.");
                return;
            }
            bool same = _note != null && _note.Id == n.Id && (_mode == DetailMode.View || _mode == DetailMode.Edit);
            if (!(same && _mode == DetailMode.View) && !MayDiscard()) return;
            SetPicking(false);
            Reset();
            _note = n;
            _kind = PickKind.None;
            _mode = edit ? DetailMode.Edit : DetailMode.View;
            _views.SelectedTab = _detailsTab;
            _status.Text = edit ? T("Nts_Editing", "Change the note, then Save.") : "";
            Fill();
        }

        private void FillNote(bool edit)
        {
            var n = _note == null ? null : NotesStore.All.FirstOrDefault(x => x.Id == _note.Id);
            if (n == null)
            {
                _note = null;
                _mode = DetailMode.None;
                AddText(T("Nts_NoteGone", "The note is not there any more - it was removed."), true, Color.FromArgb(130, 136, 146), 6);
                return;
            }
            _note = n;
            if (edit && !NotesStore.CanChange(n)) { edit = false; _mode = DetailMode.View; }
            bool asks = n.Kind == NoteKind.Comment || n.Kind == NoteKind.Value || n.Kind == NoteKind.Property;

            // ---- what, where, who ----
            AddText(n.IsGeneral ? T("Nts_General", "General note") : n.ControlName, true, null, 4).Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);
            if (!string.IsNullOrEmpty(n.Page)) AddText(n.Page, false, Color.FromArgb(110, 116, 124));
            string state = asks ? "" : n.State == NoteState.Proposed ? "  ·  " + T("Nts_Proposed", "proposed")
                         : "  ·  " + (n.Kind == NoteKind.Translation ? T("Nts_InForce", "in force") : T("Nts_InList", "in the list"));
            AddText(KindText(n) + "  ·  " + Who(n) + state, false, n.State == NoteState.Proposed && !asks ? Color.FromArgb(176, 106, 0) : Color.FromArgb(110, 116, 124), 4);
            if (!asks && n.State == NoteState.Applied && !string.IsNullOrEmpty(n.AcceptedBy) && n.AcceptedBy != n.Author && n.AcceptedAt.HasValue)
                AddText(string.Format(T("Nts_AcceptedBy", "Accepted by {0}, {1}"), n.AcceptedBy, WhenOf(n.AcceptedAt.Value)), false, Color.FromArgb(110, 116, 124));
            if (n.EditedAt.HasValue)
                AddText(string.Format(T("Nts_EditedBy", "Changed by {0}, {1}"), n.EditedBy, WhenOf(n.EditedAt.Value)), false, Color.FromArgb(110, 116, 124));
            if (n.Kind == NoteKind.Translation && !string.IsNullOrEmpty(n.TranslationKey))
                AddText(T("Nts_KeyIs", "Translation key") + ": " + n.TranslationKey, false, Color.FromArgb(110, 116, 124));

            // ---- its words ----
            TextBox text = null, reason = null, required = null;
            DataGridView grid = null;
            switch (n.Kind)
            {
                case NoteKind.Translation:
                    Head(T("Nts_SecTheTexts", "The texts"));
                    grid = EntryGrid("notesEditTexts", T("Nts_ColLanguage", "Language"),
                                     n.Texts.Select(t => new[] { t.Lang, t.Now ?? "", t.Lang, t.Text ?? "", t.Comment ?? "" }), edit, 8);
                    break;
                case NoteKind.Property:
                    Head(T("Nts_SecTheSettings", "The settings asked for"));
                    grid = EntryGrid("notesEditValues", T("Nts_ColProperty", "Setting"),
                                     n.Values.Select(v => new[] { v.Name, v.Now ?? "", v.Name, v.Required ?? "", v.Comment ?? "" }), edit, 10);
                    break;
                case NoteKind.Value:
                    var v0 = n.Values.FirstOrDefault() ?? new NoteValue();
                    Head(T("Nts_SecValue", "Its value"));
                    AddText(T("Nts_ValueThen", "Shown then") + ": " + (string.IsNullOrEmpty(v0.Now) ? "-" : v0.Now));
                    AddText(T("Nts_ValueRequired", "Your suggestion (what it should be):"), false, Color.FromArgb(90, 96, 106), 4);
                    if (edit) required = Box(v0.Required ?? "", false, true, "notesEditRequired");
                    else AddText(v0.Required ?? "");
                    AddText(T("Nts_Why", "Reason - for the builder:"), false, Color.FromArgb(90, 96, 106), 4);
                    var why0 = string.IsNullOrWhiteSpace(v0.Comment) ? n.Comment ?? "" : v0.Comment;
                    if (edit) reason = Box(why0, false, true, "notesEditReason");
                    else AddText(why0.Length > 0 ? why0 : "-");
                    break;
                default:
                    Head(n.Kind == NoteKind.Helper ? T("Nts_SecTheHelper", "The helper") : T("Nts_SecTheNote", "The note"));
                    if (edit) { text = Box(n.Text ?? "", true, true, "notesEditText"); text.Height = UiScale.For(this, 96); }
                    else AddText(string.IsNullOrWhiteSpace(n.Text) ? "-" : n.Text);
                    if (n.Kind == NoteKind.Helper)
                    {
                        AddText(T("Nts_Why", "Reason - for the builder:"), false, Color.FromArgb(90, 96, 106), 4);
                        if (edit) reason = Box(n.Comment ?? "", false, true, "notesEditReason");
                        else AddText(string.IsNullOrWhiteSpace(n.Comment) ? "-" : n.Comment);
                    }
                    break;
            }

            // ---- its file ----
            if (n.Kind == NoteKind.Comment || n.Kind == NoteKind.Helper)
            {
                var files = Row();
                var path = _fileChanged ? null : NotesStore.PathOf(n);
                string shown = _fileChanged ? (_file != null ? Path.GetFileName(_file) : T("Nts_NoFile", "(no file)"))
                             : path != null ? n.FileName ?? n.File : edit ? T("Nts_NoFile", "(no file)") : null;
                if (path != null)
                    Act(files, "📄", shown, T("Nts_TipOpenFile", "Open the file"), (s, e) =>
                    {
                        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception ex) { _status.Text = ex.Message; }
                    }, "notesOpenFile");
                else if (shown != null)
                    files.Controls.Add(new Label { AutoSize = true, Margin = new Padding(0, 10, 8, 0), ForeColor = Color.FromArgb(90, 96, 106), Text = shown });
                if (edit)
                {
                    Act(files, "📎", T("Nts_OtherFile", "Another file"), T("Nts_TipOtherFile", "A document, a picture or a video instead of this one"), (s, e) =>
                    {
                        using (var dlg = new OpenFileDialog { Title = T("Nts_AddFile", "Add a file"), Multiselect = false })
                            if (dlg.ShowDialog(FindForm()) == DialogResult.OK) { _file = dlg.FileName; _fileChanged = true; Fill(); }
                    }, "notesEditFile");
                    if (path != null || _file != null)
                        Act(files, "✖", T("Nts_NoFileBtn", "No file"), T("Nts_TipNoFile", "The note without a file"), (s, e) => { _file = null; _fileChanged = true; Fill(); }, "notesEditNoFile");
                }
            }

            // ---- what can be done with it ----
            var acts = Row();
            if (edit)
            {
                _saveEdit = () => SaveEdit(n, text, reason, required, grid);
                Act(acts, E(0x1F4BE), T("Nts_Save", "Save"), T("Nts_TipSave", "Keep the note as changed here"),
                    (s, e) => SaveEdit(n, text, reason, required, grid), "notesSave");
                Act(acts, "↩", T("Nts_Cancel", "Cancel"), T("Nts_TipCancel", "Back to the note as it is"), (s, e) => ViewNote(n, false), "notesCancel");
                return;
            }
            if (NotesStore.CanChange(n)) Act(acts, E(0x270F), T("Nts_Edit", "Edit"), T("Nts_TipEditOne", "Change this note - in the next tab"), (s, e) => ViewNote(n, true), "notesEditOne");
            if (!asks && n.State == NoteState.Proposed && NotesStore.CanApply)
                Act(acts, "✔", T("Nts_Accept", "Accept"), n.Kind == NoteKind.Translation ? T("Nts_TipAcceptText", "Put these texts on the screen")
                                                                                         : T("Nts_TipAcceptHelper", "Put this helper in the control's list"),
                    (s, e) => NotesStore.Accept(n), "notesAccept");
            if (!n.IsGeneral && ShowNoted != null)
                Act(acts, E(0x1F50D), T("Nts_FindControl", "Find its control"), T("Nts_TipFindNote", "Find its control: the page of the note chosen opened, the control shown"),
                    (s, e) => FindNoted(n), "notesFindOne");
            if (NotesStore.CanRemove(n)) Act(acts, "✖", T("Nts_Remove", "Remove"), T("Nts_TipRemove2", "Take this note away (yours, or of a level below yours)"), (s, e) => RemoveNote(n), "notesRemoveOne");
        }

        private static string WhenOf(DateTime t)
        {
            return t.ToString(ViewPreferences.DatePattern + " " + ViewPreferences.TimePattern(false), CultureInfo.InvariantCulture);
        }

        /// <summary>The note as changed in the details tab kept (tests call it with the page's own boxes).</summary>
        private bool SaveEdit(Note n, TextBox text, TextBox reason, TextBox required, DataGridView grid)
        {
            var edited = new Note
            {
                Id = n.Id, Text = n.Text, Comment = n.Comment, File = n.File, FileName = n.FileName,
                Texts = n.Texts.Select(t => new NoteText { Lang = t.Lang, Now = t.Now, Text = t.Text, Comment = t.Comment }).ToList(),
                Values = n.Values.Select(v => new NoteValue { Name = v.Name, Now = v.Now, Required = v.Required, Comment = v.Comment }).ToList()
            };
            Func<string, string> clean = s => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            switch (n.Kind)
            {
                case NoteKind.Translation:
                    if (grid.IsCurrentCellInEditMode) grid.EndEdit();
                    edited.Texts.Clear();
                    foreach (DataGridViewRow r in grid.Rows)
                    {
                        var was = n.Texts.FirstOrDefault(t => t.Lang == Convert.ToString(r.Tag));
                        var typed = clean(Convert.ToString(r.Cells[2].Value));
                        if (typed != null) edited.Texts.Add(new NoteText { Lang = Convert.ToString(r.Tag), Now = was != null ? was.Now : "", Text = typed, Comment = clean(Convert.ToString(r.Cells[3].Value)) });
                    }
                    if (edited.Texts.Count == 0) { _status.Text = T("Nts_NothingLeft", "Nothing is left in it - Remove the note instead."); return false; }
                    break;
                case NoteKind.Property:
                    if (grid.IsCurrentCellInEditMode) grid.EndEdit();
                    edited.Values.Clear();
                    foreach (DataGridViewRow r in grid.Rows)
                    {
                        var typed = clean(Convert.ToString(r.Cells[2].Value));
                        if (typed != null) edited.Values.Add(new NoteValue { Name = Convert.ToString(r.Tag), Now = Convert.ToString(r.Cells[1].Value), Required = typed, Comment = clean(Convert.ToString(r.Cells[3].Value)) });
                    }
                    if (edited.Values.Count == 0) { _status.Text = T("Nts_NothingLeft", "Nothing is left in it - Remove the note instead."); return false; }
                    break;
                case NoteKind.Value:
                    if (clean(required.Text) == null) { _status.Text = T("Nts_ValueEmpty", "Write what it should be first."); return false; }
                    if (edited.Values.Count == 0) edited.Values.Add(new NoteValue { Name = n.ControlName });
                    edited.Values[0].Required = required.Text.Trim();
                    edited.Values[0].Comment = clean(reason.Text);
                    edited.Comment = null;
                    break;
                default:
                    edited.Text = (text.Text ?? "").Trim();
                    if (reason != null) edited.Comment = clean(reason.Text);
                    bool hasFile = _fileChanged ? _file != null : NotesStore.PathOf(n) != null;
                    if (edited.Text.Length == 0 && !hasFile) { _status.Text = T("Nts_CommentEmpty", "Write the comment first."); return false; }
                    break;
            }
            if (_fileChanged)
            {
                try { edited.File = _file != null ? NotesStore.KeepFile(_file) : null; }
                catch (Exception ex) { _status.Text = T("Nts_FileFailed", "The file could not be kept") + ": " + ex.Message; return false; }
                edited.FileName = _file != null ? Path.GetFileName(_file) : null;
            }
            ForgetDrafts();
            _mode = DetailMode.View;
            if (!NotesStore.Update(edited)) { _status.Text = T("Nts_NotYours", "Only its author, or a level above the author's, changes it."); Fill(); return false; }
            var now = NotesStore.All.FirstOrDefault(x => x.Id == n.Id);
            _status.Text = now != null && now.State == NoteState.Proposed && (n.Kind == NoteKind.Translation || n.Kind == NoteKind.Helper)
                ? T("Nts_ChangedProposed", "The note is changed - a proposal again, for Maintenance or an Admin to accept.")
                : T("Nts_Changed", "The note is changed.");
            return true;
        }

        /// <summary>A note taken away, after a question (yours, or of a level below yours).</summary>
        internal void RemoveNote(Note n)
        {
            if (n == null) return;
            if (!NotesStore.CanRemove(n)) { _status.Text = T("Nts_NotYours", "Only its author, or a level above the author's, changes it."); return; }
            if (!Kaban.UI.Profiles.Ask.YesNo(FindForm(), T("Ttl_Notes", "Notes and feedback"), T("Nts_AskRemove", "Take this note away?"))) return;
            if (_note != null && _note.Id == n.Id) { ForgetDrafts(); _note = null; _mode = DetailMode.None; }
            if (NotesStore.Remove(n)) _status.Text = T("Nts_Removed", "The note is taken away.");
        }

        /// <summary>
        /// The notes chosen taken away, after one question (27 October: "it should remove all the selected ones"):
        /// the ones the user may remove (yours, or of a level below yours) - the others stay, said in the question.
        /// </summary>
        internal void RemoveNotes(IList<Note> notes)
        {
            if (notes == null || notes.Count == 0) return;
            if (notes.Count == 1) { RemoveNote(notes[0]); return; }
            var mine = notes.Where(NotesStore.CanRemove).ToList();
            if (mine.Count == 0) { _status.Text = T("Nts_NoneYours", "None of them is yours to remove - their authors, or a level above theirs, remove them."); return; }
            string question = string.Format(T("Nts_AskRemoveMany", "Take the {0} notes chosen away?"), mine.Count);
            if (mine.Count < notes.Count)
                question += "\r\n\r\n" + string.Format(T("Nts_SomeNotYours", "{0} of the notes chosen stay: they are not yours to remove (their authors, or a level above theirs)."),
                                                         notes.Count - mine.Count);
            if (!Kaban.UI.Profiles.Ask.YesNo(FindForm(), T("Ttl_Notes", "Notes and feedback"), question)) return;
            if (_note != null && mine.Any(n => n.Id == _note.Id)) { ForgetDrafts(); _note = null; _mode = DetailMode.None; }
            int gone = NotesStore.Remove(mine);
            _status.Text = string.Format(T("Nts_RemovedMany", "{0} notes taken away."), gone);
        }

        /// <summary>A note's control: its page opened, the control shown (the main window does it).</summary>
        internal void FindNoted(Note n)
        {
            if (n == null || n.IsGeneral || ShowNoted == null) return;
            string why;
            try { why = ShowNoted(n) ?? ""; }
            catch (Exception ex) { why = ex.Message; }
            _status.Text = why.Length > 0 ? why : string.Format(T("Nts_FoundControl", "{0}: its page is open, the control flashing."), n.ControlName);
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
            _all.MultiSelect = true;                 // several notes chosen: Remove takes them all (27 October)
        }

        private string KindText(Note n)
        {
            switch (n.Kind)
            {
                case NoteKind.Translation: return T("Nts_KindText", "Texts");
                case NoteKind.Helper: return T("Nts_KindHelper", "Helper");
                case NoteKind.Value: return T("Nts_KindValue", "Value");
                case NoteKind.Property: return T("Nts_KindProperty", "Settings");
                default: return n.IsGeneral ? T("Nts_KindGeneral", "General") : T("Nts_KindComment", "Comment");
            }
        }

        /// <summary>The note chosen in the table, or null.</summary>
        internal Note SelectedNote
        {
            get
            {
                var r = _all.CurrentRow ?? (_all.SelectedRows.Count > 0 ? _all.SelectedRows[0] : null);
                return r != null ? r.Tag as Note : null;
            }
        }

        /// <summary>Every note chosen in the table (several rows chosen: all of them).</summary>
        internal List<Note> SelectedNotes
        {
            get
            {
                var list = _all.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index).Select(r => r.Tag as Note).Where(n => n != null).ToList();
                if (list.Count == 0 && SelectedNote != null) list.Add(SelectedNote);
                return list;
            }
        }

        /// <summary>The table's buttons for the note chosen: View it, Edit and Remove it (yours, or of a level below yours), Find its control.</summary>
        private void UpdateButtons()
        {
            var n = SelectedNote;
            _view.Enabled = n != null;
            _edit.Enabled = n != null && NotesStore.CanChange(n);
            _remove.Enabled = SelectedNotes.Any(NotesStore.CanRemove);
            _findNote.Enabled = n != null && !n.IsGeneral && ShowNoted != null;
            _folder.Enabled = _lastExport != null && SecurityManager.HasAccess(SecurityLevel.Maintenance);
        }

        /// <summary>The notes the search and the filters let through.</summary>
        private IEnumerable<Note> Filtered(IEnumerable<Note> notes)
        {
            var kind = _kindFilter.SelectedItem as Choice;
            var state = _stateFilter.SelectedItem as Choice;
            var who = _whoFilter.SelectedItem as Choice;
            var words = (_search.Text ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var n in notes)
            {
                bool asks = n.Kind == NoteKind.Comment || n.Kind == NoteKind.Value || n.Kind == NoteKind.Property;
                if (kind != null && kind.Value != null)
                {
                    string k = n.Kind == NoteKind.Comment && n.IsGeneral ? "General" : n.Kind.ToString();
                    if (k != kind.Value) continue;
                }
                if (state != null && state.Value != null && (asks || n.State.ToString() != state.Value)) continue;
                if (who != null && who.Value != null && !string.Equals(n.Author, who.Value, StringComparison.OrdinalIgnoreCase)) continue;
                if (words.Length > 0)
                {
                    string all = string.Join(" ", new[] { n.Author, n.Level, KindText(n), n.ControlName, n.Page, NotesStore.BodyOf(n), n.FileName, n.TranslationKey, When(n) });
                    if (!words.All(w => all.IndexOf(w, StringComparison.CurrentCultureIgnoreCase) >= 0)) continue;
                }
                yield return n;
            }
        }

        /// <summary>The users who wrote a note - in the Who filter, yours first ("Mine").</summary>
        private void FillWhoFilter(IList<Note> notes)
        {
            var chosen = _whoFilter.SelectedItem as Choice;
            string me = SecurityManager.CurrentUser;
            var authors = notes.Select(n => n.Author ?? "").Where(a => a.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
                               .OrderBy(a => a, StringComparer.CurrentCultureIgnoreCase).ToList();
            var want = new List<Choice> { new Choice(null, T("Nts_Everyone", "Everyone")) };
            if (!string.IsNullOrEmpty(me)) want.Add(new Choice(me, T("Nts_Mine", "Mine") + " (" + me + ")"));
            want.AddRange(authors.Where(a => !string.Equals(a, me, StringComparison.OrdinalIgnoreCase)).Select(a => new Choice(a, a)));
            if (_whoFilter.Items.Count == want.Count && _whoFilter.Items.Cast<Choice>().Select(c => c.Text).SequenceEqual(want.Select(c => c.Text))) return;
            _fillingFilters = true;
            try
            {
                _whoFilter.Items.Clear();
                foreach (var c in want) _whoFilter.Items.Add(c);
                int at = chosen == null || chosen.Value == null ? 0 : want.FindIndex(c => string.Equals(c.Value, chosen.Value, StringComparison.OrdinalIgnoreCase));
                _whoFilter.SelectedIndex = Math.Max(0, at);
            }
            finally { _fillingFilters = false; }
        }

        /// <summary>The search and the filters cleared: every note.</summary>
        internal void ClearFilters()
        {
            _fillingFilters = true;
            try
            {
                _search.Text = "";
                _kindFilter.SelectedIndex = 0;
                _stateFilter.SelectedIndex = 0;
                if (_whoFilter.Items.Count > 0) _whoFilter.SelectedIndex = 0;
            }
            finally { _fillingFilters = false; }
            _typing.Stop();
            FillAll();
        }

        /// <summary>The search typed now, the table at once (tests).</summary>
        internal string SearchText { get { return _search.Text; } set { _search.Text = value; _typing.Stop(); FillAll(); } }

        private void FillAll()
        {
            var chosen = SelectedNote;
            var all = NotesStore.All;
            FillWhoFilter(all);
            var shown = Filtered(all.OrderByDescending(x => x.Time)).ToList();
            _all.Rows.Clear();
            int select = -1;
            foreach (var n in shown)
            {
                string body = NotesStore.BodyOf(n) + (string.IsNullOrEmpty(n.FileName) ? "" : "   [" + n.FileName + "]");
                bool asks = n.Kind == NoteKind.Comment || n.Kind == NoteKind.Value || n.Kind == NoteKind.Property;
                string state = asks ? "" : n.State == NoteState.Proposed ? T("Nts_Proposed", "proposed")
                             : n.Kind == NoteKind.Translation ? T("Nts_InForce", "in force") : T("Nts_InList", "in the list");
                int i = _all.Rows.Add(When(n), n.Author, KindText(n), state, n.IsGeneral ? T("Nts_General", "General note") : n.ControlName, body, n.Page);
                _all.Rows[i].Tag = n;
                if (n.State == NoteState.Proposed && !asks) _all.Rows[i].DefaultCellStyle.ForeColor = Color.FromArgb(176, 106, 0);
                if (chosen != null && n.Id == chosen.Id) select = i;
            }
            _all.ClearSelection();
            if (select >= 0) { _all.CurrentCell = _all.Rows[select].Cells[0]; _all.Rows[select].Selected = true; }
            else if (_all.Rows.Count > 0) { _all.CurrentCell = _all.Rows[0].Cells[0]; _all.Rows[0].Selected = true; }
            _count.Text = shown.Count == all.Count ? string.Format(T("Nts_Count", "{0} notes on this panel"), all.Count)
                                                   : string.Format(T("Nts_CountShown", "{0} of {1} notes"), shown.Count, all.Count);
            UpdateButtons();
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
