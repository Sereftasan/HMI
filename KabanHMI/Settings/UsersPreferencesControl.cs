using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>HMI Settings > Users and Preferences (it was two tabs: User Settings and View Preferences).</summary>
    public class UsersPreferencesPage : ISettingsPage
    {
        public string TitleKey => "Set_UsersPrefs";
        public string TitleFallback => "Users and Preferences";
        // Anyone signed in: their own password, scale and preferences; more by level (UserRights).
        public SecurityLevel RequiredLevel => SecurityLevel.Operator;
        public UserControl GetControl() => new UsersPreferencesControl();
    }

    /// <summary>
    /// USERS AND THEIR PREFERENCES, ON ONE PAGE.
    ///
    /// At the top left the accounts, "Everyone (this panel)" first, and under them
    /// the account chosen (name, password, level, who created it). On the right,
    /// top to bottom, its screen scale, view preferences and units of measure -
    /// for a user each may stay "As for everyone"; "Read from" copies another
    /// user's (or everyone's) into the page, kept with the save.
    ///
    /// THE PREVIEW comes only while a list is open: beside it (50 px to its
    /// right), for the entry under the pointer - what that value would look like
    /// - and goes when the list closes. Read from's list previews the hovered
    /// user's preferences; the screen scale's menu its sizes.
    ///
    /// "Everyone (this panel)" is not "what is on screen now": it is what is in
    /// force while nobody is signed in, and what each user's "As for everyone"
    /// follows. A user sees everyone's value, or their own.
    ///
    /// Who may change what: UserRights (their own; below their level; their
    /// level when they created it; everyone's from Maintenance).
    /// </summary>
    public class UsersPreferencesControl : UserControl
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        public const string Everyone = "\u0001everyone";
        public const string ScaleKey = "ScreenScale";

        private readonly DataGridView _grid = new DataGridView();
        private readonly Panel _left = new Panel { Dock = DockStyle.Left, BackColor = Color.White };
        private readonly Panel _account = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
        private readonly Panel _body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
        private readonly PreferencePreview _preview = new PreferencePreview { Visible = false, Size = new Size(300, 360) };
        private readonly Label _who = new Label { AutoSize = false, Height = 28 };
        private readonly Label _info = new Label { AutoSize = false, ForeColor = Color.FromArgb(110, 116, 124) };
        private readonly Label _note = new Label { AutoSize = false, ForeColor = Color.FromArgb(150, 60, 20) };
        private readonly Label _status = new Label { AutoSize = true, ForeColor = Color.FromArgb(60, 66, 76) };
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _pass = new TextBox { UseSystemPasswordChar = true };
        private readonly ComboBox _level = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label _createdBy = new Label { AutoSize = true, ForeColor = Color.FromArgb(110, 116, 124), Font = UiStyle.Small };
        private readonly ComboBox _readFrom = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Name = "readFrom" };
        private readonly ScaleChooser _scale = new ScaleChooser();
        private readonly Dictionary<string, ComboBox> _combos = new Dictionary<string, ComboBox>();
        private readonly List<Control[]> _rows = new List<Control[]>();        // caption, field, the row's section
        private readonly DirtyTracker _dirty = new DirtyTracker();
        private Button _save, _revert, _new, _delete;
        private Label _pageNote;
        private readonly List<Control[]> _accountRows = new List<Control[]>();   // caption, field
        private string _selected;          // a user, Everyone, or null while a new one is made
        private bool _creating, _filling;
        private ComboBox _openList;        // the list open now (its preview beside it)
        private int _hovered = -1;         // its entry previewed (a list is drawn again and again)

        /// <summary>Asks before a delete (tests answer instead).</summary>
        public Func<string, bool> Confirm;
        /// <summary>Offers the restart for a new scale of one's own (tests answer instead).</summary>
        public Action<double> OfferRestart;

        // ---- for the tests ----------------------------------------------------------
        public ComboBox ComboOf(string key) { ComboBox c; return _combos.TryGetValue(key, out c) ? c : null; }
        public DataGridView Users { get { return _grid; } }
        public ComboBox ReadFrom { get { return _readFrom; } }
        public Button SaveButton { get { return _save; } }
        public Button NewButton { get { return _new; } }
        public Button DeleteButton { get { return _delete; } }
        public TextBox NameBox { get { return _name; } }
        public TextBox PasswordBox { get { return _pass; } }
        public ComboBox LevelBox { get { return _level; } }
        public ScaleChooser ScaleBox { get { return _scale; } }
        public PreferencePreview Preview { get { return _preview; } }
        public string StatusText { get { return _status.Text; } }
        public string NoteText { get { return _note.Visible ? _note.Text : ""; } }
        public string InfoText { get { return _info.Visible ? _info.Text : ""; } }
        public string Selected { get { return _creating ? null : _selected; } }
        /// <summary>The left column (the accounts and the account) and the right one (the preferences) - tests.</summary>
        public Control AccountColumn { get { return _left; } }
        public Control PreferencesColumn { get { return _body; } }

        public UsersPreferencesControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;
            var tips = UiStyle.Tips();

            // ---- the bar ----
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, BackColor = Color.White, WrapContents = false, Padding = new Padding(4, 4, 0, 0) };
            _save = UiStyle.ToolButton("\uD83D\uDCBE", T("Tip_UsrPrefSave", "Keep the account, its screen scale and its preferences"), tips, (s, e) => Save());
            _revert = UiStyle.ToolButton("\u21BA", T("Tip_ViewRevert", "Throw the changes away and show the kept preferences"), tips, (s, e) => Fill());
            _new = UiStyle.ToolButton("\u2795", T("Tip_UsrNew", "A new user: at your level or lower"), tips, (s, e) => StartNew());
            _delete = UiStyle.ToolButton("\uD83D\uDDD1", T("Tip_UsrDelete", "Delete the selected user"), tips, (s, e) => DeleteUser());
            foreach (var b in new[] { _save, _revert, _new, _delete }) { b.Margin = new Padding(2, 0, 2, 0); bar.Controls.Add(b); }
            _status.Margin = new Padding(14, 9, 0, 0);
            bar.Controls.Add(_status);
            _dirty.Guard(_save);

            // ---- the accounts, top left ----
            _grid.Dock = DockStyle.Top;
            _grid.Height = 300;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add("user", T("Usr_Name", "User"));
            _grid.Columns.Add("level", T("Usr_Level", "Level"));
            _grid.Columns.Add("factor", T("Usr_Factor", "Screen scale"));
            UiStyle.EnableAutoFit(_grid);
            UiStyle.EnableTableTools(_grid, "Users", sorting: false);
            // rows taller or lower (Table rows): the list follows, the account with it
            _grid.RowHeightChanged += (s, e) => FitGrid();
            _grid.ColumnHeadersHeightChanged += (s, e) => FitGrid();
            // "Everyone (this panel)" whole in the first column.
            int[] widths = { 150, 86, 74 };
            for (int i = 0; i < widths.Length; i++) _grid.Columns[i].Width = Math.Max(_grid.Columns[i].MinimumWidth, widths[i]);
            _left.Width = Math.Max(360, _grid.Columns.Cast<DataGridViewColumn>().Sum(c => c.Width) + 8);
            // THE GRID PICKS ITS FIRST ROW when it is first shown (Everyone): that is
            // not the operator choosing - the one chosen is put back once it settles.
            _grid.HandleCreated += (s, e) =>
            {
                _gridSettled = false;
                try { _grid.BeginInvoke(new Action(() => { Reselect(); _gridSettled = true; })); } catch { _gridSettled = true; }
            };
            _grid.SelectionChanged += (s, e) =>
            {
                if (_filling || _grid.SelectedRows.Count == 0) return;
                if (!_gridSettled && _grid.IsHandleCreated) { Reselect(); return; }
                var name = _grid.SelectedRows[0].Tag as string;
                if (name == null || (!_creating && name == _selected)) return;
                _creating = false;
                _selected = name;
                Fill();
            };

            BuildAccount();
            BuildBody();

            _left.Controls.Add(_account);
            _left.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UiStyle.GridRule });
            _left.Controls.Add(_grid);
            var line = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = UiStyle.GridRule };
            Controls.Add(_body);
            Controls.Add(line);
            Controls.Add(_left);
            Controls.Add(bar);
            Controls.Add(_preview);          // floats over the page, beside the list open
            _preview.BringToFront();

            _selected = StartUser();
            RefreshList();
            Fill();
            SecurityManager.UserChanged += OnUserChanged;
            Disposed += (s, e) => SecurityManager.UserChanged -= OnUserChanged;
        }

        /// <summary>The one shown first: the one signed in - if they have an account here (a trusted sign-in may not) - else Everyone.</summary>
        private static string StartUser()
        {
            var actor = UserRights.Actor;
            return actor != null && AccountOf(actor) != null ? actor : Everyone;
        }

        private void OnUserChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new EventHandler(OnUserChanged), sender, e); return; }
            _creating = false;
            _selected = StartUser();
            RefreshList();
            Fill();
        }

        // ==================================================================
        // THE PAGE
        // ==================================================================

        // The account, under the list (narrow); the preferences, on the right.
        private const int ACapX = 8, ACapW = 96, AFieldX = 110, AFieldW = 230;
        private const int CapX = 12, CapW = 210, FieldX = 228, FieldW = 300;

        private Label Head(Control parent, string text, int width)
        {
            var l = new Label
            {
                Text = text, AutoSize = false, Size = new Size(width, 24),
                Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), ForeColor = Color.FromArgb(40, 46, 56)
            };
            parent.Controls.Add(l);
            return l;
        }

        /// <summary>The list's tallest (its rows beyond scroll in it).</summary>
        private int GridMax { get { return UiScale.For(this, 300); } }

        private void BuildAccount()
        {
            // No heading: the user, the password and the level stand under the list
            // where its tallest would end, 40 px below (8 October) - not moving up and
            // down with the number of accounts.
            foreach (var pair in new[] { new { Caption = T("Usr_Name", "User"), Field = (Control)_name },
                                         new { Caption = T("Usr_Pass", "Password"), Field = (Control)_pass },
                                         new { Caption = T("Usr_Level", "Level"), Field = (Control)_level } })
            {
                var cap = UiStyle.CaptionLabel(pair.Caption, ACapX, 0, ACapW);
                _account.Controls.Add(cap);
                pair.Field.SetBounds(AFieldX, 0, AFieldW, pair.Field.Height);
                _account.Controls.Add(pair.Field);
                _accountRows.Add(new[] { cap, pair.Field });
            }
            _account.Controls.Add(_createdBy);
            LayoutAccount();
            _name.TextChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };
            _pass.TextChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };
            _level.SelectedIndexChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };
            Kaban.UI.Controls.UiStyle.SetTip(UiStyle.Tips(), _pass, T("Tip_UsrPass", "A new password, empty keeps the one it has"));
        }

        private void Row(string section, string key, string caption, Control field)
        {
            var cap = UiStyle.CaptionLabel(caption, CapX, 0, CapW);
            field.Left = FieldX;
            field.Width = FieldW;
            _body.Controls.Add(cap);
            _body.Controls.Add(field);
            _rows.Add(new Control[] { cap, field, new Label { Tag = section } });
        }

        /// <summary>A preference's list: drawn by the page, so the entry under the pointer is known (the preview).</summary>
        private ComboBox PrefCombo(string key)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, Name = "vp" + key,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = UiScale.For(this, 20)
            };
            combo.DrawItem += (s, e) => DrawEntry(combo, key, e);
            combo.DropDown += (s, e) => OpenList(combo, key);
            combo.DropDownClosed += (s, e) => CloseList(combo);
            combo.SelectedIndexChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };
            return combo;
        }

        private void BuildBody()
        {
            // (no line naming the user above the preferences: the list shows who is chosen - 8 October)
            _body.Controls.Add(_info);
            _body.Controls.Add(_note);

            Row("Prefs", null, T("VP_ReadFrom", "Read from"), _readFrom);
            _readFrom.DrawMode = DrawMode.OwnerDrawFixed;
            _readFrom.ItemHeight = UiScale.For(this, 20);
            _readFrom.DrawItem += (s, e) => DrawEntry(_readFrom, null, e);
            _readFrom.DropDown += (s, e) => OpenList(_readFrom, null);
            _readFrom.DropDownClosed += (s, e) => CloseList(_readFrom);
            _readFrom.SelectedIndexChanged += (s, e) => { if (!_filling) ReadFromChosen(); };
            Kaban.UI.Controls.UiStyle.SetTip(UiStyle.Tips(), _readFrom, T("Tip_ReadFrom", "Copies another user's screen scale and preferences (or everyone's) into this page, \uD83D\uDCBE keeps them"));

            Row("Screen", ScaleKey, T("Usr_FactorOwn", "Screen scale"), _scale);
            _scale.ValueChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };
            // its menu's sizes previewed as the pointer moves over them
            _scale.Hovered += (s, f) => ShowScaleBeside(_scale.OpenMenu, f);
            _scale.MenuClosed += (s, e) => HidePreview();

            foreach (var group in new[] { "Captions", "Numbers", "Units", "Tables", "SignIn", "Help" })
            {
                if (group == "Units")
                {
                    // The units of measure (they had a tab of their own: Unit Settings).
                    foreach (var type in UnitManager.Families)
                    {
                        string key = UnitManager.UnitKey(type);
                        var combo = PrefCombo(key);
                        Row(group, key, PrefTexts.NameOf(key), combo);
                        _combos[key] = combo;
                    }
                    continue;
                }
                foreach (var p in ViewPreferences.All.Where(x => x.Group == group && !x.Hidden))
                {
                    var combo = PrefCombo(p.Key);
                    Row(group, p.Key, PrefTexts.NameOf(p.Key), combo);
                    _combos[p.Key] = combo;
                }
            }

            _pageNote = new Label
            {
                AutoSize = false, Size = new Size(FieldX + FieldW - CapX, 62), ForeColor = Color.FromArgb(110, 116, 124),
                Text = T("Usr_PageNote", "A user's own values are taken when they sign in, 'As for everyone' follows the panel's. A new screen scale needs a restart (offered at once for your own).")
            };
            _body.Controls.Add(_pageNote);
        }

        private static readonly Dictionary<string, string[]> SectionHeads = new Dictionary<string, string[]>
        {
            { "Prefs", new[] { "Usr_SecPrefs", "Preferences" } },
            { "Screen", new[] { "VP_SecScreen", "Screen" } },
            { "Captions", new[] { "VP_SecCaptions", "Captions" } },
            { "Numbers", new[] { "VP_SecNumbers", "Numbers and dates" } },
            { "Units", new[] { "VP_SecUnits", "Units of measure" } },
            { "Tables", new[] { "VP_SecTables", "Tables" } },
            { "SignIn", new[] { "VP_SecSignIn", "Sign-in and alerts" } },
            { "Help", new[] { "VP_SecHelp", "Help and AI" } }
        };
        private readonly Dictionary<string, Label> _sectionLabels = new Dictionary<string, Label>();

        /// <summary>The preferences from the top, the sections' headings in light grey; the account shown for a user only.</summary>
        private void LayoutBody(bool account)
        {
            _account.Visible = account;
            _createdBy.Visible = account && _createdBy.Text.Length > 0;
            _body.SuspendLayout();
            // IN THE PANEL'S UNSCROLLED TERMS: laid out while it was scrolled down (another
            // user picked lower on the page), every place was off by the scroll - a band of
            // white above the preferences once scrolled back (8 October). And at the
            // screen scale: the page may be laid out again after it was scaled.
            Func<int, int> px = v => UiScale.For(this, v);
            int oy = _body.AutoScrollPosition.Y;
            int x = px(CapX), w = px(FieldX + FieldW - CapX);
            int y = px(4);
            if (_info.Visible) { _info.SetBounds(x, y + oy, w, px(36)); y += px(40); }
            if (_note.Visible) { _note.SetBounds(x, y + oy, w, px(36)); y += px(40); }
            string section = null;
            foreach (var r in _rows)
            {
                string sec = (string)r[2].Tag;
                if (sec != section)
                {
                    section = sec;
                    if (section != _rows[0][2].Tag as string || _info.Visible || _note.Visible) y += px(6);
                    Label h;
                    bool main = sec == "Prefs";
                    if (!_sectionLabels.TryGetValue(sec, out h))
                    {
                        h = new Label
                        {
                            Text = T(SectionHeads[sec][0], SectionHeads[sec][1]), AutoSize = false,
                            ForeColor = main ? Color.FromArgb(40, 46, 56) : Color.FromArgb(110, 116, 124),
                            Font = main ? UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold) : UiStyle.Small
                        };
                        _body.Controls.Add(h);
                        _sectionLabels[sec] = h;
                    }
                    int hh = px(main ? 24 : 20);
                    h.SetBounds(x, y + oy, w, hh);
                    y += hh + px(2);
                }
                r[0].Top = y + px(2) + oy;
                r[1].Top = y + oy;
                y += px(UiStyle.RowPitch);
            }
            _pageNote.Top = y + px(8) + oy;
            _pageNote.Left = x;
            _body.ResumeLayout();
        }

        // ==================================================================
        // THE LIST
        // ==================================================================

        private void RefreshList()
        {
            _filling = true;
            try
            {
                _grid.Rows.Clear();
                int i = _grid.Rows.Add(T("VP_Everyone", "Everyone (this panel)"), "", DisplaySettings.Percent(DisplaySettings.PanelFactor));
                _grid.Rows[i].Tag = Everyone;
                _grid.Rows[i].DefaultCellStyle.Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Italic);
                if (!UserRights.CanEditEveryone) _grid.Rows[i].DefaultCellStyle.ForeColor = Color.FromArgb(130, 136, 146);
                foreach (var u in UserStore.GetAllUsers().OrderByDescending(u => u.Level).ThenBy(u => u.Username, StringComparer.OrdinalIgnoreCase))
                {
                    var f = DisplaySettings.UserFactor(u.Username);
                    i = _grid.Rows.Add(u.Username, PrefTexts.LevelText(u.Level), f.HasValue ? DisplaySettings.Percent(f.Value) : "");
                    _grid.Rows[i].Tag = u.Username;
                    // The ones this user cannot change, in grey: seen, not changed.
                    if (!UserRights.CanManage(u.Username)) _grid.Rows[i].DefaultCellStyle.ForeColor = Color.FromArgb(130, 136, 146);
                }
                SelectRow(_creating ? null : _selected);
            }
            finally { _filling = false; }
            FitGrid();
        }

        /// <summary>The list as tall as its rows (at most 300); the account where its tallest would end.</summary>
        private void FitGrid()
        {
            int rows = _grid.Rows.GetRowsHeight(DataGridViewElementStates.Visible);
            int want = (_grid.ColumnHeadersVisible ? _grid.ColumnHeadersHeight : 0) + rows + 3;
            if (_grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible) > _grid.ClientSize.Width)
                want += SystemInformation.HorizontalScrollBarHeight;
            int h = Math.Max(UiScale.For(this, 120), Math.Min(GridMax, want));
            if (_grid.Height != h) _grid.Height = h;
            LayoutAccount();
        }

        /// <summary>The user, the password and the level: 40 px under where the list at its tallest ends.</summary>
        private void LayoutAccount()
        {
            if (_accountRows.Count == 0) return;
            // the account panel starts under the list (and its rule): the rest of the list's room, then 40
            int y = Math.Max(0, GridMax - _grid.Height - 1) + UiScale.For(this, 40) + _account.AutoScrollPosition.Y;
            foreach (var r in _accountRows)
            {
                if (r[1].Top != y) r[1].Top = y;
                if (r[0].Top != y + UiScale.For(this, 2)) r[0].Top = y + UiScale.For(this, 2);
                y += UiScale.For(this, UiStyle.RowPitch);
            }
            _createdBy.Location = new Point(_name.Left + UiScale.For(this, 2), y - UiScale.For(this, 6));
        }

        private bool _gridSettled = true;

        private void Reselect()
        {
            _filling = true;
            try
            {
                // From the top, Everyone in sight (the grid had scrolled to its first pick).
                if (_grid.IsHandleCreated && _grid.Rows.Count > 0) try { _grid.FirstDisplayedScrollingRowIndex = 0; } catch { }
                SelectRow(_creating ? null : _selected);
            }
            finally { _filling = false; }
        }

        private void SelectRow(string name)
        {
            _grid.ClearSelection();
            if (name == null) return;
            foreach (DataGridViewRow r in _grid.Rows)
                if (string.Equals(r.Tag as string, name, StringComparison.OrdinalIgnoreCase))
                {
                    r.Selected = true;
                    try
                    {
                        // Not before the grid exists on screen: set then, Mono's grid scrolled a row down.
                        if (_grid.IsHandleCreated) _grid.CurrentCell = r.Cells[0];
                    }
                    catch { }
                    return;
                }
        }

        // ==================================================================
        // THE ONE CHOSEN
        // ==================================================================

        private sealed class Choice
        {
            public string Value, Text;
            public SecurityLevel Level;
            public override string ToString() { return Text; }
        }

        private bool IsEveryone { get { return !_creating && _selected == Everyone; } }
        private string EditedUser { get { return _creating || _selected == Everyone ? null : _selected; } }

        /// <summary>May the one signed in change what the page shows?</summary>
        private bool CanEdit
        {
            get
            {
                if (_creating) return UserRights.LevelsToGive().Count > 0;
                if (_selected == Everyone) return UserRights.CanEditEveryone;
                return UserRights.CanManage(_selected);
            }
        }

        private static UserAccount AccountOf(string name)
        {
            return name == null ? null : UserStore.GetAllUsers().FirstOrDefault(u => string.Equals(u.Username, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>A unit family's key, or None.</summary>
        private static UnitType UnitOf(string key)
        {
            foreach (var t in UnitManager.Families) if (UnitManager.UnitKey(t) == key) return t;
            return UnitType.None;
        }

        /// <summary>Everyone's value of a preference (the units: UnitManager's own).</summary>
        internal static string PanelOf(string key)
        {
            var unit = UnitOf(key);
            return unit != UnitType.None ? UnitManager.PanelUnitName(unit) : ViewPreferences.PanelValue(key);
        }

        /// <summary>A preference's values (the units: the family's).</summary>
        private static IEnumerable<string> ValuesOf(string key)
        {
            var unit = UnitOf(key);
            if (unit != UnitType.None) return UnitManager.GetAvailable(unit).Select(u => u.Name);
            var p = ViewPreferences.Find(key);
            return p != null ? p.Values : new string[0];
        }

        /// <summary>The page from what is kept for the one chosen.</summary>
        public void Fill()
        {
            HidePreview();
            _filling = true;
            try
            {
                if (!_creating && _selected != Everyone && AccountOf(_selected) == null) _selected = StartUser();
                bool edit = CanEdit;
                string user = EditedUser;
                var account = AccountOf(user);
                bool self = UserRights.IsSelf(UserRights.Actor, user);

                // ---- who ----
                _who.Text = _creating ? T("Usr_NewUser", "New user")
                          : _selected == Everyone ? T("VP_Everyone", "Everyone (this panel)")
                          : user + (account != null ? "  \u00B7  " + PrefTexts.LevelText(account.Level) : "") + (self ? "  " + T("VP_Me", "(me)") : "");
                _info.Text = IsEveryone ? T("Usr_InfoEveryone", "In force while nobody is signed in, and followed by every user's 'As for everyone'.") : "";
                _info.Visible = _info.Text.Length > 0;
                _note.Text = edit ? "" : _selected == Everyone
                    ? T("Usr_NoteEveryone", "Everyone's preferences are changed from Maintenance up.")
                    : string.Format(T("Usr_NoteNotYours", "You can see {0}'s settings. Only a higher level, or the one who created the account, can change them."), user);
                _note.Visible = _note.Text.Length > 0;

                // ---- the account ----
                _name.Text = _creating ? "" : user ?? "";
                _name.ReadOnly = !_creating;
                _name.Enabled = _creating || account != null;
                _pass.Text = "";
                _pass.Enabled = edit && !IsEveryone;
                _level.Items.Clear();
                var levels = _creating ? UserRights.LevelsToGive()
                           : account != null && edit && !self ? UserRights.LevelsToGive().Where(l => UserRights.CanSetLevel(user, l) || l == account.Level).ToList()
                           : account != null ? new List<SecurityLevel> { account.Level } : new List<SecurityLevel>();
                if (account != null && !levels.Contains(account.Level)) levels.Insert(0, account.Level);
                foreach (var l in levels) _level.Items.Add(new Choice { Level = l, Text = PrefTexts.LevelText(l) });
                int li = 0;
                for (int i = 0; i < _level.Items.Count; i++) if (account != null && ((Choice)_level.Items[i]).Level == account.Level) li = i;
                if (_level.Items.Count > 0) _level.SelectedIndex = li;
                _level.Enabled = edit && _level.Items.Count > 1;
                string creator = account != null ? UserStore.CreatedBy(account.Username) : "";
                _createdBy.Text = creator.Length > 0 ? string.Format(T("Usr_CreatedBy", "created by {0}"), creator) : "";

                // ---- read from ----
                _readFrom.Items.Clear();
                _readFrom.Items.Add(new Choice { Value = null, Text = "\u2013" });
                if (_selected != Everyone || _creating) _readFrom.Items.Add(new Choice { Value = Everyone, Text = T("VP_Everyone", "Everyone (this panel)") });
                foreach (var u in UserStore.GetAllUsers().Select(a => a.Username).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                    if (_creating || !string.Equals(u, _selected, StringComparison.OrdinalIgnoreCase))
                        _readFrom.Items.Add(new Choice { Value = u, Text = u });
                _readFrom.SelectedIndex = 0;
                _readFrom.Enabled = edit;

                // ---- the screen scale ----
                _scale.OfferNone = !IsEveryone;
                _scale.Value = IsEveryone ? DisplaySettings.PanelFactor : user != null ? DisplaySettings.UserFactor(user) : (double?)null;
                _scale.Enabled = edit;

                // ---- the preferences and the units ----
                foreach (var kv in _combos)
                {
                    var combo = kv.Value;
                    combo.Items.Clear();
                    if (!IsEveryone)
                        combo.Items.Add(new Choice
                        {
                            Value = null,
                            Text = T("VP_AsEveryone", "As for everyone") + "  (" + PrefTexts.ValueText(kv.Key, PanelOf(kv.Key)) + ")"
                        });
                    foreach (var v in ValuesOf(kv.Key)) combo.Items.Add(new Choice { Value = v, Text = PrefTexts.ValueText(kv.Key, v) });
                    string current = IsEveryone ? PanelOf(kv.Key) : user != null ? ViewPreferences.UserValue(user, kv.Key) : null;
                    SelectValue(combo, current);
                    combo.Enabled = edit;
                }

                _delete.Enabled = !_creating && user != null && UserRights.CanDelete(user);
                _new.Enabled = UserRights.LevelsToGive().Count > 0;
                LayoutBody(!IsEveryone);
                _dirty.Reset();
            }
            finally { _filling = false; }
        }

        private static void SelectValue(ComboBox combo, string value)
        {
            int at = 0;
            for (int i = 0; i < combo.Items.Count; i++)
                if (((Choice)combo.Items[i]).Value == value) at = i;
            if (combo.Items.Count > 0) combo.SelectedIndex = at;
        }

        /// <summary>The value a combo shows, "As for everyone" read as everyone's.</summary>
        internal string ShownValue(string key)
        {
            ComboBox c;
            if (!_combos.TryGetValue(key, out c)) return PanelOf(key);
            var ch = c.SelectedItem as Choice;
            return ch != null && ch.Value != null ? ch.Value : PanelOf(key);
        }

        // ==================================================================
        // THE PREVIEW: beside the list open, for the entry under the pointer
        // ==================================================================

        /// <summary>The preview's subject (tests).</summary>
        internal string ActiveKey { get { return _preview.Visible ? _preview.Key : null; } }

        /// <summary>A list's entry, drawn by the page; the one under the pointer of an open list is previewed.</summary>
        private void DrawEntry(ComboBox combo, string key, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index >= 0 && e.Index < combo.Items.Count)
            {
                bool selected = (e.State & DrawItemState.Selected) != 0;
                var ink = !combo.Enabled ? SystemColors.GrayText : selected ? SystemColors.HighlightText : SystemColors.WindowText;
                TextRenderer.DrawText(e.Graphics, combo.Items[e.Index].ToString(), e.Font ?? combo.Font, e.Bounds, ink,
                                      TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                // the edit part of the box is drawn too (ComboBoxEdit): only the open list counts
                if (selected && combo.DroppedDown && (e.State & DrawItemState.ComboBoxEdit) == 0) HoverItem(combo, key, e.Index);
            }
            e.DrawFocusRectangle();
        }

        /// <summary>A list opened: the preview of what it shows now, beside it.</summary>
        internal void OpenList(ComboBox combo, string key)
        {
            _openList = combo;
            _hovered = -1;
            HoverItem(combo, key, combo.SelectedIndex);
        }

        /// <summary>The list closed: the preview goes.</summary>
        internal void CloseList(ComboBox combo)
        {
            if (_openList == combo) _openList = null;
            _hovered = -1;
            HidePreview();
        }

        /// <summary>The entry <paramref name="index"/> of an open list is under the pointer (or the keys).</summary>
        internal void HoverItem(ComboBox combo, string key, int index)
        {
            if (index < 0 || index >= combo.Items.Count) return;
            if (combo == _openList && index == _hovered && _preview.Visible) return;
            var choice = combo.Items[index] as Choice;
            if (choice == null) return;
            bool moved = combo == _openList && _hovered >= 0;      // the pointer onto another entry (not the list opening)
            if (combo == _openList) _hovered = index;
            if (combo == _readFrom)
            {
                if (choice.Value == null) { HidePreview(); return; }
                _preview.ShowSummary(choice.Text, SummaryOf(choice.Value));
            }
            else
            {
                string hovered = choice.Value ?? PanelOf(key);
                _preview.ShowPreference(key, k => k == key ? hovered : ShownValue(k), choice.Value == null);
                // the alarm sound heard as the pointer comes onto "On" (a button could not be
                // clicked while the list is open: a click outside it closes it)
                if (moved && key == ViewPreferences.AlarmSound && hovered == "On")
                    try { Kaban.UI.Helpers.AlarmSound.Play(); } catch { }
            }
            PlaceBeside(ListBounds(combo), combo.PointToScreen(Point.Empty).Y);
        }

        /// <summary>Where an open list is on the screen (below its box, as tall as its entries).</summary>
        private static Rectangle ListBounds(ComboBox combo)
        {
            var at = combo.PointToScreen(new Point(0, combo.Height));
            int rows = Math.Max(1, Math.Min(combo.Items.Count, combo.MaxDropDownItems));
            return new Rectangle(at.X, at.Y, Math.Max(combo.DropDownWidth, combo.Width), rows * combo.ItemHeight + 2);
        }

        /// <summary>
        /// The preview 50 px to the right of the list (on screen), level with its
        /// box; on the left of it when the page has no room on the right; inside
        /// the page always.
        /// </summary>
        private void PlaceBeside(Rectangle list, int topScreen)
        {
            int gap = UiScale.For(this, 50);
            var size = new Size(UiScale.For(this, 300), UiScale.For(this, _preview.WantedHeight));
            var tl = PointToClient(new Point(list.Right + gap, topScreen));
            var listLeft = PointToClient(new Point(list.Left, topScreen)).X;
            int x = tl.X;
            if (x + size.Width > ClientSize.Width) x = listLeft - gap - size.Width;
            if (x < 0) x = Math.Max(0, ClientSize.Width - size.Width);
            int y = Math.Max(0, Math.Min(tl.Y, ClientSize.Height - size.Height));
            _preview.SetBounds(x, y, size.Width, size.Height);
            _preview.Visible = true;
            _preview.BringToFront();
        }

        private void HidePreview()
        {
            if (_preview.Visible) _preview.Visible = false;
        }

        /// <summary>The screen scale's menu: the size under the pointer, beside the menu.</summary>
        private void ShowScaleBeside(ToolStripDropDown menu, double? factor)
        {
            _preview.ShowScale(factor, DisplaySettings.PanelFactor);
            var bounds = menu != null && menu.Visible ? menu.Bounds : ListBounds(_readFrom);
            PlaceBeside(bounds, _scale.PointToScreen(Point.Empty).Y);
        }

        /// <summary>Test hook: a size of the scale menu under the pointer.</summary>
        internal void HoverScale(double? factor) { ShowScaleBeside(null, factor); }

        /// <summary>A user's (or everyone's) preferences, line by line: the name, the value, their own or not.</summary>
        private List<PreferencePreview.Line> SummaryOf(string who)
        {
            var lines = new List<PreferencePreview.Line>();
            bool everyone = who == Everyone;
            double? f = everyone ? DisplaySettings.PanelFactor : DisplaySettings.UserFactor(who);
            lines.Add(new PreferencePreview.Line
            {
                Name = PrefTexts.NameOf(ScaleKey), Own = everyone || f.HasValue,
                Value = f.HasValue ? DisplaySettings.Percent(f.Value) : Kaban.UI.Forms.MainForm.NoScaleRequest
            });
            foreach (var kv in _combos)
            {
                string own = everyone ? PanelOf(kv.Key) : ViewPreferences.UserValue(who, kv.Key);
                // "Time" twice (the clock and the unit): the units say so
                string name = PrefTexts.NameOf(kv.Key);
                if (UnitOf(kv.Key) != UnitType.None) name = string.Format(T("PV_UnitOf", "{0} unit"), name);
                lines.Add(new PreferencePreview.Line
                {
                    Name = name, Own = own != null,
                    Value = own != null ? PrefTexts.ValueText(kv.Key, own) : T("VP_AsEveryone", "As for everyone")
                });
            }
            return lines;
        }

        // ==================================================================
        // READ FROM
        // ==================================================================

        /// <summary>
        /// Another's scale and preferences into the page. From a user into a user:
        /// their own values (theirs "As for everyone" stays so); from everyone into
        /// a user: "As for everyone"; from a user into everyone: what is in force
        /// for them. Nothing is kept until the save.
        /// </summary>
        private void ReadFromChosen()
        {
            var src = _readFrom.SelectedItem as Choice;
            if (src == null || src.Value == null || !CanEdit) return;
            bool fromEveryone = src.Value == Everyone;
            _filling = true;
            try
            {
                foreach (var kv in _combos)
                {
                    string v = fromEveryone ? null : ViewPreferences.UserValue(src.Value, kv.Key);
                    if (IsEveryone && v == null) v = PanelOf(kv.Key);
                    SelectValue(kv.Value, v);
                }
                double? f = fromEveryone ? (double?)null : DisplaySettings.UserFactor(src.Value);
                if (IsEveryone && !f.HasValue) f = DisplaySettings.PanelFactor;
                _scale.Value = f;
            }
            finally { _filling = false; }
            _dirty.MarkDirty();
            Say(string.Format(T("Usr_ReadDone", "{0}'s preferences read: \uD83D\uDCBE keeps them."), src.Text));
        }

        // ==================================================================
        // NEW, SAVE, DELETE
        // ==================================================================

        private void StartNew()
        {
            if (UserRights.LevelsToGive().Count == 0) return;
            _creating = true;
            _filling = true;
            try { _grid.ClearSelection(); } finally { _filling = false; }
            Fill();
            Say(T("Usr_NewHint", "Name, password and level, then \uD83D\uDCBE."));
            _name.Focus();
        }

        private Dictionary<string, string> CollectPreferences()
        {
            var values = new Dictionary<string, string>();
            foreach (var kv in _combos)
            {
                var c = kv.Value.SelectedItem as Choice;
                values[kv.Key] = c != null ? c.Value : null;
            }
            return values;
        }

        private static string Shown(Dictionary<string, string> values)
        {
            return string.Join(", ", values.Select(kv => kv.Key + "=" + (kv.Value ?? "everyone's")));
        }

        /// <summary>Keeps the page: the account (new, password, level), the screen scale, the preferences, the units.</summary>
        internal bool Save()
        {
            if (!CanEdit) return false;
            var prefs = CollectPreferences();
            var levelChoice = _level.SelectedItem as Choice;

            if (_creating)
            {
                string name = _name.Text.Trim();
                if (name.Length == 0) return Fail(T("Usr_MsgName", "Enter a user name."));
                if (!UserStore.IsValidUsername(name)) return Fail(T("Usr_MsgSemicolon", "A user name may not contain a semicolon."));
                if (AccountOf(name) != null) return Fail(string.Format(T("Usr_MsgExists", "There is already a user '{0}'."), name));
                if (_pass.Text.Length == 0) return Fail(T("Usr_MsgPassword", "A new user needs a password."));
                if (levelChoice == null || !UserRights.CanCreate(levelChoice.Level)) return Fail(T("Usr_MsgLevel", "You cannot give that level."));
                try { UserStore.AddOrUpdateUser(name, _pass.Text, levelChoice.Level); }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) { return Fail(ex.Message); }
                UserStore.SetCreatedBy(name, UserRights.Actor);
                AuditLogger.Log("created user '" + name + "' (" + levelChoice.Level + ").");
                DisplaySettings.SetUserFactor(name, _scale.Value);
                if (prefs.Values.Any(v => v != null)) ViewPreferences.SetMany(name, prefs);
                _creating = false;
                _selected = name;
                RefreshList();
                Fill();
                Say(string.Format(T("Usr_MsgCreated", "User '{0}' created."), name));
                return true;
            }

            if (IsEveryone)
            {
                // The units are the panel's (UnitManager, Units.cfg); the rest View.cfg's.
                foreach (var kv in prefs.Where(p => UnitOf(p.Key) != UnitType.None && p.Value != null).ToList())
                    if (kv.Value != UnitManager.PanelUnitName(UnitOf(kv.Key))) UnitManager.SetActive(UnitOf(kv.Key), kv.Value);
                var rest = prefs.Where(p => UnitOf(p.Key) == UnitType.None).ToDictionary(p => p.Key, p => p.Value);
                ViewPreferences.SetMany(null, rest);
                AuditLogger.Log("view preferences for everyone kept: " + Shown(prefs));
                double before = DisplaySettings.PanelFactor;
                bool scaleChanged = _scale.Value.HasValue && Math.Abs(_scale.Value.Value - before) > 0.0005;
                RefreshList();
                Fill();
                Say(T("Usr_MsgEveryoneSaved", "Everyone's preferences kept."));
                if (scaleChanged)
                {
                    // As the glass on the caption bar: the panel's scale, offered as a restart.
                    var shell = FindForm() as Kaban.UI.Forms.MainForm;
                    if (shell != null) shell.ChooseFactor(_scale.Value.Value);
                    else { DisplaySettings.PanelFactor = _scale.Value.Value; DisplaySettings.StartFactor = _scale.Value.Value; }
                    RefreshList();
                    Fill();
                }
                return true;
            }

            string user = _selected;
            var account = AccountOf(user);
            if (account == null) return Fail(string.Format(T("Usr_MsgNoUser", "No user '{0}'."), user));
            var level = levelChoice != null ? levelChoice.Level : account.Level;
            if (level != account.Level && !UserRights.CanSetLevel(user, level))
                return Fail(UserStore.WouldRemoveLastAdmin(user, level) ? T("Usr_MsgLastAdmin", "The last Admin account cannot be given a lower level.")
                                                                       : T("Usr_MsgLevel", "You cannot give that level."));
            try
            {
                if (_pass.Text.Length > 0)
                {
                    UserStore.AddOrUpdateUser(account.Username, _pass.Text, level);
                    AuditLogger.Log("password of user '" + account.Username + "' changed" + (level != account.Level ? ", level " + level : "") + ".");
                }
                else if (level != account.Level)
                {
                    UserStore.SetLevel(account.Username, level);
                    AuditLogger.Log("changed level of user '" + account.Username + "' to " + level + ".");
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) { return Fail(ex.Message); }

            var beforeScale = DisplaySettings.UserFactor(account.Username);
            DisplaySettings.SetUserFactor(account.Username, _scale.Value);
            if (beforeScale != _scale.Value)
                AuditLogger.Log("screen scale of user '" + account.Username + "': " +
                                (_scale.Value.HasValue ? DisplaySettings.Percent(_scale.Value.Value) : "no specific request") + ".");
            ViewPreferences.SetMany(account.Username, prefs);
            AuditLogger.Log("view preferences of " + account.Username + " kept: " + Shown(prefs));
            RefreshList();
            Fill();
            Say(string.Format(T("Usr_MsgSaved", "User '{0}' saved."), account.Username));

            // Your own scale changed: offered at once, not at the next sign-in.
            var f = _scale.Value;
            if (f.HasValue && UserRights.IsSelf(UserRights.Actor, account.Username) && beforeScale != f &&
                Math.Abs(f.Value - UiScale.Factor) > 0.001)
            {
                if (OfferRestart != null) OfferRestart(f.Value);
                else AskRestart(f.Value);
            }
            return true;
        }

        private void Say(string message)
        {
            _status.ForeColor = Color.FromArgb(60, 66, 76);
            _status.Text = message;
        }

        private bool Fail(string message)
        {
            _status.Text = message;
            _status.ForeColor = Color.Firebrick;
            var t = new Timer { Interval = 4000 };
            t.Tick += (s, e) => { t.Dispose(); if (!IsDisposed) _status.ForeColor = Color.FromArgb(60, 66, 76); };
            t.Start();
            return false;
        }

        private void AskRestart(double factor)
        {
            var shell = FindForm() as Kaban.UI.Forms.MainForm;
            string question = string.Format(T("Msg_FactorRestartNow",
                "Restart the HMI now with the screen scale {0}? The machine keeps running, the screen is back in a few seconds, and you stay signed in."),
                DisplaySettings.Percent(factor));
            if (shell == null ||
                MessageBox.Show(this, question, T("Ttl_Magnifier", "Screen scale"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            shell.RestartWithFactor(factor, keepSignedIn: true);
        }

        internal bool DeleteUser()
        {
            string user = EditedUser;
            if (user == null) return false;
            if (UserRights.IsSelf(UserRights.Actor, user)) return Fail(T("Usr_MsgSelf", "You cannot delete the account you are signed in with."));
            if (UserStore.WouldRemoveLastAdmin(user, null)) return Fail(T("Usr_MsgLastAdminDelete", "The last Admin account cannot be deleted."));
            if (!UserRights.CanDelete(user)) return Fail(T("Usr_MsgCannotDelete", "Only a higher level, or the one who created it, can delete this user."));
            string q = string.Format(T("Usr_MsgConfirmDelete", "Delete user '{0}'?"), user);
            bool yes = Confirm != null ? Confirm(q)
                     : MessageBox.Show(this, q, T("Usr_TtlDelete", "Delete user"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            if (!yes) return false;
            UserStore.RemoveUser(user);
            AuditLogger.Log("deleted user '" + user + "'.");
            _selected = StartUser();
            RefreshList();
            Fill();
            Say(string.Format(T("Usr_MsgDeleted", "User '{0}' deleted."), user));
            return true;
        }
    }

    /// <summary>The names on the page and in the preview.</summary>
    internal static class PrefTexts
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        public static string LevelText(SecurityLevel level)
        {
            switch (level)
            {
                case SecurityLevel.Operator: return T("Lvl_Operator", "Operator");
                case SecurityLevel.Maintenance: return T("Lvl_Maintenance", "Maintenance");
                case SecurityLevel.Admin: return T("Lvl_Admin", "Admin");
                default: return level.ToString();
            }
        }

        public static string NameOf(string key)
        {
            switch (key)
            {
                case ViewPreferences.CaptionAlign: return T("VP_CaptionAlign", "Captions and axis names");
                case ViewPreferences.DecimalSeparator: return T("VP_DecimalSeparator", "Decimal separator");
                case ViewPreferences.DateFormat: return T("VP_DateFormat", "Date");
                case ViewPreferences.TimeFormat: return T("VP_TimeFormat", "Time");
                case ViewPreferences.TableRows: return T("VP_TableRows", "Table rows");
                case ViewPreferences.TableFont: return T("VP_TableFont", "Table text");
                case ViewPreferences.StartPage: return T("VP_StartPage", "After sign-in");
                case ViewPreferences.AlarmSound: return T("VP_AlarmSound", "Sound for a new alarm");
                case ViewPreferences.Blinking: return T("VP_Blinking", "Attention marks");
                case ViewPreferences.TooltipDelay: return T("VP_TooltipDelay", "Tooltips appear after");
                case ViewPreferences.AiPromptAs: return T("VP_AiPromptAs", "AI prompt copied as");
                case UsersPreferencesControl.ScaleKey: return T("Usr_FactorOwn", "Screen scale");
                default:
                    // a unit family: "Unit_Length" -> Length
                    if (key != null && key.StartsWith("Unit_"))
                    {
                        string fam = key.Substring(5);
                        return T("Unit_Family_" + fam, fam);
                    }
                    return key;
            }
        }

        public static string ValueText(string key, string value)
        {
            switch (key + "." + value)
            {
                case "CaptionAlign.Right": return T("VP_CapRight", "Right, against the field");
                case "CaptionAlign.Left": return T("VP_CapLeft", "Left, in a column");
                case "DecimalSeparator.Windows": return T("VP_DecWindows", "As Windows");
                case "DecimalSeparator.Point": return T("VP_DecPoint", "Point") + "  (1234.56)";
                case "DecimalSeparator.Comma": return T("VP_DecComma", "Comma") + "  (1234,56)";
                case "DateFormat.Iso": return "2026-10-02";
                case "DateFormat.European": return "02.10.2026";
                case "DateFormat.Us": return "10/02/2026";
                case "TimeFormat.H24": return T("VP_H24", "24 hours") + "  (14:31)";
                case "TimeFormat.H12": return T("VP_H12", "12 hours") + "  (2:31 PM)";
                case "TableRows.Compact": return T("VP_Compact", "Compact");
                case "TableFont.Small": return T("VP_Small", "Small");
                case "TableRows.Normal": case "TableFont.Normal": return T("VP_Normal", "Normal");
                case "TableRows.Large": case "TableFont.Large": return T("VP_Large", "Large");
                case "StartPage.Stay": return T("VP_Stay", "Stay where the screen is");
                case "StartPage.Home": return T("VP_Home", "Home");
                case "StartPage.Last": return T("VP_Last", "My last page");
                case "AlarmSound.Off": return T("VP_Off", "Off");
                case "AlarmSound.On": return T("VP_On", "On");
                case "Blinking.On": return T("VP_Blink", "Blink");
                case "Blinking.Off": return T("VP_Steady", "Steady");
                case "TooltipDelay.Short": return T("VP_Short", "Short") + "  (0.4 s)";
                case "TooltipDelay.Normal": return T("VP_Normal", "Normal") + "  (1 s)";
                case "TooltipDelay.Long": return T("VP_Long", "Long") + "  (2 s)";
                case "AiPromptAs.File": return T("VP_AiAsFile", "A file (Ctrl+V attaches it)");
                case "AiPromptAs.Text": return T("VP_AiAsText", "Its text");
                default: return value;
            }
        }
    }

    /// <summary>
    /// A user's screen scale, chosen as on the caption bar: looks like the other
    /// fields' boxes; a click opens the same menu - "No specific request" (when
    /// offered), the usual sizes, and the row that sets any percent 1% at a time.
    /// </summary>
    public sealed class ScaleChooser : Button
    {
        private double? _value;

        public ScaleChooser()
        {
            FlatStyle = FlatStyle.Flat;
            BackColor = Color.White;
            TextAlign = ContentAlignment.MiddleLeft;
            Height = 26;
            Cursor = Cursors.Hand;
            AccessibleName = "Screen scale";
            FlatAppearance.BorderColor = Color.FromArgb(160, 166, 176);
            Text = Caption;
        }

        /// <summary>"No specific request" in the menu (not for everyone's: the panel always has one).</summary>
        public bool OfferNone = true;

        public event EventHandler ValueChanged;

        /// <summary>The request; null: no specific request (the panel's).</summary>
        public double? Value
        {
            get { return _value; }
            set
            {
                var v = value.HasValue ? DisplaySettings.Clamp(value.Value) : (double?)null;
                bool changed = v != _value;
                _value = v;
                Text = Caption;
                Invalidate();
                if (changed) { var h = ValueChanged; if (h != null) h(this, EventArgs.Empty); }
            }
        }

        public string Caption
        {
            get { return _value.HasValue ? DisplaySettings.Percent(_value.Value) : Kaban.UI.Forms.MainForm.NoScaleRequest; }
        }

        /// <summary>A size of the open menu under the pointer (null: "No specific request") - the page previews it.</summary>
        public event Action<object, double?> Hovered;
        /// <summary>The menu closed.</summary>
        public event EventHandler MenuClosed;
        /// <summary>The menu open now, or null.</summary>
        public ToolStripDropDown OpenMenu { get; private set; }

        /// <summary>The menu a click opens (tests use it as the operator would).</summary>
        internal ContextMenuStrip BuildMenu()
        {
            var menu = Kaban.UI.Forms.MainForm.BuildScaleMenu(null, _value, OfferNone, v => Value = v);
            foreach (var item in menu.Items.OfType<ToolStripMenuItem>())
            {
                var it = item;
                it.MouseEnter += (s, e) =>
                {
                    var h = Hovered;
                    if (h != null) h(this, it.Tag is double ? (double?)(double)it.Tag : null);
                };
            }
            menu.Closed += (s, e) =>
            {
                OpenMenu = null;
                var h = MenuClosed;
                if (h != null) h(this, EventArgs.Empty);
            };
            return menu;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            var menu = BuildMenu();
            OpenMenu = menu;
            UiStyle.ShowPopup(menu, this, new Point(0, Height));
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            // The drop-down caret, as on a combo box.
            float cx = Width - UiScale.For(this, 12), cy = Height / 2f;
            float d = UiScale.For(this, 4);
            using (var b = new SolidBrush(Color.FromArgb(60, 66, 76)))
                pevent.Graphics.FillPolygon(b, new[] { new PointF(cx - d, cy - d / 2), new PointF(cx + d, cy - d / 2), new PointF(cx, cy + d / 2 + 1) });
        }
    }
}
