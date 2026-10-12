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
    ///  - the menu's page buttons and a page's tabs work as ever and are noted
    ///    with the page they open; the caption bar is picked as the page is;
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

        private readonly Button _pick;
        private readonly Label _status = new Label { AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(70, 76, 86) };
        private readonly TabControl _views = new TabControl { Dock = DockStyle.Fill };
        private readonly FlowLayoutPanel _body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            BackColor = Color.White, Padding = new Padding(8, 6, 8, 12)
        };
        private readonly DataGridView _all = new DataGridView { Dock = DockStyle.Fill, Name = "notesAll" };
        private readonly Button _export, _folder, _general;
        private readonly Label _count = new Label { AutoSize = true, ForeColor = Color.FromArgb(110, 116, 124), Margin = new Padding(10, 12, 0, 0) };

        private bool _picking = true;
        private Picker _picker;

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

        /// <summary>The feedback files' folder (Documents\Kaban HMI\Feedback); replaceable for tests.</summary>
        public static string FeedbackFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Kaban HMI", "Feedback");

        public NotesPage(Func<Control, bool> pickable, Func<Control, string> placeOf, Func<string> panelName, Func<Control, bool> passThrough = null)
        {
            _pickable = pickable ?? (c => false);
            _passThrough = passThrough ?? (c => false);
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

            // ---- the foot: a general note, the feedback file ----
            var foot = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, BackColor = Color.White, Padding = new Padding(4, 6, 4, 0), WrapContents = false };
            _general = UiStyle.ToolButton("📝", T("Nts_TipGeneral", "A general note - on no control: an idea, a request, a file to send"), _tips, (s, e) => PickGeneral());
            _general.Name = "notesGeneral";
            _export = UiStyle.ToolButton("📤", T("Nts_TipExport", "Make the feedback file: every note of this panel, with the helpers' files, in one .zip to send"), _tips, (s, e) => Export());
            _export.Name = "notesExport";
            _folder = UiStyle.ToolButton("📂", T("Nts_TipFolder", "Show the feedback file in its folder (Maintenance, Admin)"), _tips, (s, e) => ShowFolder());
            _folder.Name = "notesFolder";
            var exportText = new Label { AutoSize = true, Text = T("Nts_Export", "Feedback file"), Margin = new Padding(2, 12, 6, 0), Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold) };
            foot.Controls.Add(_general);
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
            ViewPreferences.Changed += OnPreferencesChanged;
            VisibleChanged += (s, e) => UpdatePicker();
            ParentChanged += (s, e) => UpdatePicker();
            HandleCreated += (s, e) => UpdatePicker();
            Disposed += (s, e) =>
            {
                NotesStore.Changed -= OnNotesChanged;
                SecurityManager.UserChanged -= OnUserChanged;
                ViewPreferences.Changed -= OnPreferencesChanged;
                RemovePicker();
            };
            SetPicking(true);
            Fill();
        }

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

        private void SetPicking(bool on)
        {
            _picking = on;
            _pick.Text = on ? "👆  " + T("Nts_PickOn", "Picking: on") : "✋  " + T("Nts_PickOff", "Picking: off");
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

        private void Reset()
        {
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
            var grid = c as DataGridView;
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

        /// <summary>A page button of the menu, or a tab, used as ever - and noted with the page it opened.</summary>
        internal void PickPassed(Control c)
        {
            if (c == null || c.IsDisposed) return;
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
                var page = tabs.SelectedTab;
                _name = T("Nts_Tab", "Tab") + ": " + (page != null ? page.Text : "");
                _key += page != null ? "#tab:" + (string.IsNullOrEmpty(page.Name) ? tabs.SelectedIndex.ToString(CultureInfo.InvariantCulture) : page.Name) : "";
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
            Reset();
            _kind = PickKind.General;
            _key = "";
            _name = T("Nts_General", "General note");
            _status.Text = T("Nts_GeneralHint", "A note on no control: an idea, a request - with a file if you like.");
            _views.SelectedIndex = 0;
            Fill();
        }

        private void ShowPicked(Control flash)
        {
            _status.Text = T("Nts_Picked", "Picked") + ": " + _name;
            try { if (flash != null && !flash.IsDisposed) ControlHighlight.Flash(flash, 1500); } catch { }
            _views.SelectedIndex = 0;
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
        /// release picks it. A table's cell to type in, the menu's page buttons and
        /// a page's tabs are let through (a page button and a tab then noted with
        /// the page they opened). The pointer moving is let through (its tooltip,
        /// its border) and shown in the bar.
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
                var hit = Control.FromChildHandle(m.HWnd);
                if (hit == null) return false;

                // the menu's page buttons, a page's tabs: as ever - and noted
                var passed = PassedOf(hit);
                if (passed != null)
                {
                    if (m.Msg == WM_LBUTTONUP)
                    {
                        var target = passed;
                        try { _page.BeginInvoke(new Action(() => _page.PickPassed(target))); } catch { }
                    }
                    return false;
                }

                if (m.Msg == WM_KEYDOWN || m.Msg == WM_KEYUP)
                {
                    var key = (Keys)(int)(long)m.WParam;
                    if (key != Keys.Space && key != Keys.Enter) return false;
                    var focused = NoteTarget.Notable(hit);
                    if (focused == null || !_page._pickable(focused)) return false;
                    if (focused is DataGridView) return false;            // a table: its own keys
                    if (m.Msg == WM_KEYUP) _page.Pick(focused);
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
                        if (m.Msg == WM_LBUTTONUP) _page.PickCell(grid, info.ColumnIndex, info.Type == DataGridViewHitTestType.Cell ? info.RowIndex : -1);
                        return true;
                    }
                    // the scroll bars, the empty part: the table's own
                    return false;
                }
                if (grid != null) return false;                    // its editing box: typed in
                if (m.Msg == WM_LBUTTONUP) _page.Pick(target2);
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
                for (var c = hit; c != null; c = c.Parent)
                    if (_page._passThrough(c)) return c;
                return null;
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
            var b = new TextBox
            {
                Text = text ?? "", Multiline = multi, Height = multi ? UiScale.For(this, 64) : UiScale.For(this, 26), Width = InnerWidth,
                Tag = "full", Enabled = enabled, Name = name, Margin = new Padding(0, 2, 0, 4), ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None
            };
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
                _count.Text = string.Format(T("Nts_Count", "{0} notes on this panel"), NotesStore.All.Count);
                _folder.Enabled = _lastExport != null && SecurityManager.HasAccess(SecurityLevel.Maintenance);
                if (_kind == PickKind.General) { FillGeneral(write); return; }
                if (_key == null)
                {
                    AddText(T("Nts_None", "No control picked yet."), true, null, 6);
                    AddText(T("Nts_How", "Tap or click a control of the page - a field, a button, a lamp, a caption, a column heading. It is not operated while picking: " +
                                     "what it is, its tooltip, its texts in every language, its helpers and its comments appear here, and you can add yours."),
                         false, Color.FromArgb(90, 96, 106));
                    AddText(T("Nts_HowMore", "The menu's page buttons and a page's tabs work as usual and are noted with the page they open - a table's cell to type in is typed in. " +
                                         "📝 below: a general note, on no control."), false, Color.FromArgb(90, 96, 106), 6);
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
        private DataGridView EntryGrid(string name, string firstHeading, IEnumerable<string[]> rows, bool write, int maxRows)
        {
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
            foreach (var r in rows)
            {
                int i = grid.Rows.Add(r[0], r[1], "", "");
                grid.Rows[i].Tag = r.Length > 2 ? r[2] : r[0];
                grid.Rows[i].Cells[3].ReadOnly = true;
                grid.Rows[i].Cells[3].Style.BackColor = Color.FromArgb(245, 246, 248);
            }
            // the comment once something is required
            grid.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex != 2) return;
                bool asked = !string.IsNullOrWhiteSpace(Convert.ToString(grid.Rows[e.RowIndex].Cells[2].Value));
                var cell = grid.Rows[e.RowIndex].Cells[3];
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
                                     Languages.Select(l => new[] { l, LanguageManager.GetStringIn(l, key.Key) ?? "", l }), write, 8);
                var acts = Row();
                Act(acts, "💾", apply ? T("Nts_KeepTexts", "Keep the texts") : T("Nts_ProposeTexts", "Propose the texts"),
                    apply ? T("Nts_TipKeepTexts", "On the screen at once, for everyone on this panel - and in the feedback file")
                          : T("Nts_TipProposeTexts", "A proposal: Maintenance or an Admin puts it on the screen - and in the feedback file"),
                    (s, e) => KeepTexts(key, grid), "notesKeepTexts", write);
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

        private void KeepTexts(NoteTarget.TextKey key, DataGridView grid)
        {
            var texts = new List<NoteText>();
            foreach (var a in Asked(grid))
                if (a[2] != a[1].Trim()) texts.Add(new NoteText { Lang = a[0], Text = a[2], Now = a[1], Comment = a[3].Length > 0 ? a[3] : null });
            if (texts.Count == 0) { _status.Text = T("Nts_NothingChanged", "No text changed."); return; }
            var n = NotesStore.Add(Describe(new Note { Kind = NoteKind.Translation, TranslationKey = key.Key, Texts = texts }));
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
            var acts = Row();
            Act(acts, "💾", T("Nts_KeepProps", "Keep the settings asked for"), T("Nts_TipKeepProps", "For the builder: these settings as they should be - in the feedback file"),
                (s, e) => KeepProperties(grid), "notesKeepProps", write);
            foreach (var n in notes.Where(n => n.Kind == NoteKind.Property).OrderByDescending(n => n.Time))
                NoteLine(n, NotesStore.BodyOf(n, "\n"), false);
        }

        private void KeepProperties(DataGridView grid)
        {
            var asked = Asked(grid).Where(a => a[2] != a[1]).ToList();
            if (asked.Count == 0) { _status.Text = T("Nts_NothingChanged", "No text changed."); return; }
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
            var n = NotesStore.Add(Describe(new Note
            {
                Kind = NoteKind.Helper, Text = text, File = kept, FileName = _file != null ? Path.GetFileName(_file) : null,
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
            var n = NotesStore.Add(new Note
            {
                Kind = NoteKind.Comment, Text = text, ControlKey = "", ControlName = T("Nts_General", "General note"), Page = "",
                File = kept, FileName = _file != null ? Path.GetFileName(_file) : null
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
            if (NotesStore.CanRemove(n))
                Act(acts, "✖", T("Nts_Remove", "Remove"), T("Nts_TipRemove", "Take this note away (yours, or as an Admin)"), (s, e) =>
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
                case NoteKind.Value: return T("Nts_KindValue", "Value");
                case NoteKind.Property: return T("Nts_KindProperty", "Settings");
                default: return n.IsGeneral ? T("Nts_KindGeneral", "General") : T("Nts_KindComment", "Comment");
            }
        }

        private void FillAll()
        {
            _all.Rows.Clear();
            foreach (var n in NotesStore.All.OrderByDescending(x => x.Time))
            {
                string body = NotesStore.BodyOf(n) + (string.IsNullOrEmpty(n.FileName) ? "" : "   [" + n.FileName + "]");
                bool asks = n.Kind == NoteKind.Comment || n.Kind == NoteKind.Value || n.Kind == NoteKind.Property;
                string state = asks ? "" : n.State == NoteState.Proposed ? T("Nts_Proposed", "proposed")
                             : n.Kind == NoteKind.Translation ? T("Nts_InForce", "in force") : T("Nts_InList", "in the list");
                _all.Rows.Add(When(n), n.Author, KindText(n), state, n.IsGeneral ? T("Nts_General", "General note") : n.ControlName, body, n.Page);
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
