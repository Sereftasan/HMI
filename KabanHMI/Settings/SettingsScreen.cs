using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// The PROGRAM SETTINGS page - reached from a left-hand navigation button like
    /// any machine page. Its sub-pages (Unit Settings, User Settings, Log Settings,
    /// Machine Parameter Records, Barcode Settings, About, ...) are a horizontal
    /// TAB STRIP along the top; clicking a tab shows that page below.
    ///
    /// Tabs (not a second vertical column) because second-level navigation must not
    /// eat screen width - and a third level inside a sub-page can use the same
    /// pattern again without the layout collapsing.
    ///
    /// A page whose RequiredLevel the current user lacks is hidden (e.g. User
    /// Settings is Admin-only); the strip refreshes on login and language changes.
    /// </summary>
    public class SettingsScreen : UserControl
    {
        private readonly List<ISettingsPage> _pages;
        private readonly TabRowStrip _tabStrip = new TabRowStrip();

        /// <summary>The tab strip: tells when it has placed its tabs, so it can be made as tall as their rows.</summary>
        private sealed class TabRowStrip : FlowLayoutPanel
        {
            public Action Placed;
            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);
                var placed = Placed;
                if (placed != null) placed();
            }
        }
        private readonly Panel _host = new Panel();
        private readonly Dictionary<ISettingsPage, Button> _tabButtons = new Dictionary<ISettingsPage, Button>();
        private readonly Dictionary<ISettingsPage, UserControl> _instances = new Dictionary<ISettingsPage, UserControl>();
        // the language each page was built in (IRebuildForLanguage: built again in another)
        private readonly Dictionary<ISettingsPage, string> _builtIn = new Dictionary<ISettingsPage, string>();
        private ISettingsPage _active;

        // stored so Dispose can actually unsubscribe (a lambda in -= removes nothing)
        private readonly EventHandler _onUserChanged;
        private readonly EventHandler _onLanguageChanged;

        private static readonly Color ColStrip = Color.FromArgb(245, 246, 248);
        private static readonly Color ColHover = Color.FromArgb(228, 232, 238);
        private static readonly Color ColText = Color.FromArgb(40, 44, 52);
        private static readonly Color ColTextDim = Color.FromArgb(110, 116, 124);

        public SettingsScreen(IEnumerable<ISettingsPage> pages)
        {
            _pages = new List<ISettingsPage>(pages ?? new ISettingsPage[0]);

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _tabStrip.Dock = DockStyle.Top;
            _tabStrip.Height = 32;   // label + descenders + the active rule beneath them
            _tabStrip.BackColor = ColStrip;
            // EVERY TAB NAME IN SIGHT: tabs that do not fit on one line go on to a
            // second, and the strip grows to hold them (FitTabRows). On one line,
            // the tabs past the page's right edge were simply cut off - at a larger
            // magnifying factor, or beside a side page, their names could not be
            // read or reached.
            _tabStrip.WrapContents = true;
            _tabStrip.Placed = FitTabRows;
            _tabStrip.FlowDirection = FlowDirection.LeftToRight;
            _tabStrip.Padding = new Padding(8, 2, 8, 0);   // tight: a nested strip must not drift away

            _host.Dock = DockStyle.Fill;
            _host.BackColor = Color.White;
            // Breathing room so a grid does not butt against the navigation bar on
            // the left or sit welded to the tab line above it. The left inset lines
            // the content up with the first tab's left edge (_tabStrip's own 8px
            // padding + a tab's 2px margin + its 12px text inset would put the
            // caption at 22, but matching the strip's 8px reads as one column and
            // keeps the widest possible table).
            // Nested strips still get ZERO, or every level would add another inset
            // and the third level would start halfway across the screen.
            ApplyContentPadding();   // re-evaluated once we are parented (see OnParentChanged)

            Controls.Add(_host);
            Controls.Add(_tabStrip);

            _onUserChanged = (s, e) => BuildTabs();
            _onLanguageChanged = (s, e) => BuildTabs();
            SecurityManager.UserChanged += _onUserChanged;
            LanguageManager.LanguageChanged += _onLanguageChanged;
            BuildTabs();
        }

        /// <summary>
        /// True when this strip is itself a page of an OUTER strip that has
        /// already inset the content; adding our own would compound at every
        /// level.
        ///
        /// Only an outer strip that is SHOWN counts. A machine's pages are wrapped
        /// in a SettingsScreen whose tabs are hidden (the left bar picks the
        /// machine) and which therefore insets nothing - counting it left Machine
        /// Parameters with no inset at all, its tables welded to the navigation
        /// bar and to the tab line.
        /// </summary>
        private bool IsNestedInsideAnotherStrip
        {
            get
            {
                for (Control p = Parent; p != null; p = p.Parent)
                {
                    var outer = p as SettingsScreen;
                    if (outer != null && outer._stripShown) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Whether this strip's tabs are meant to be seen. Kept here rather than
        /// read from _tabStrip.Visible, which is false for every control of a page
        /// that is not on screen yet - and would have called every strip hidden
        /// while the pages were being built.
        /// </summary>
        private bool _stripShown = true;

        /// <summary>
        /// Content inset, decided by DEPTH. Called again from OnParentChanged
        /// because Parent is still null while the constructor runs, so the nesting
        /// test cannot be answered there - a nested strip would otherwise keep the
        /// outer inset it was born with and every level would compound.
        /// </summary>
        private void ApplyContentPadding()
        {
            // A HIDDEN tab strip must add NOTHING. A machine page is wrapped in this
            // class purely to hold the per-machine pages; a single-machine page is
            // the raw view with no wrapper at all. Any inset here would push the
            // wrapped one down and the two would start at different heights - which
            // is exactly the gap that was left behind when machine selection moved
            // to the left bar.
            //
            // A strip INSIDE a hidden one is the first level that shows tabs, so it
            // insets its content as a top-level strip would - Machine Parameters >
            // Alarm Settings is such a page.
            // In pixels once this page has been magnified (UiScale).
            var want = (IsNestedInsideAnotherStrip || !_stripShown)
                ? new Padding(0)
                : new Padding(Kaban.UI.Controls.UiScale.For(this, 8), Kaban.UI.Controls.UiScale.For(this, 6),
                              Kaban.UI.Controls.UiScale.For(this, 8), Kaban.UI.Controls.UiScale.For(this, 8));
            if (_host.Padding != want) _host.Padding = want;
        }

        private static IEnumerable<SettingsScreen> NestedStrips(Control root)
        {
            foreach (Control c in root.Controls)
            {
                var ss = c as SettingsScreen;
                if (ss != null) yield return ss;
                foreach (var deeper in NestedStrips(c)) yield return deeper;
            }
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            ApplyContentPadding();
        }

        /// <summary>
        /// Again once on a form: a strip built inside a page that is attached to
        /// the outer strip only later saw no outer strip when its parent was set.
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyContentPadding();
        }

        // ---- a machine's bar above the tabs ---------------------------------------------

        private Panel _topBarHolder;
        private Control _topBar;

        /// <summary>
        /// A BAR ABOVE THE TABS, shown over every one of them: FA2010's Manual Movement
        /// has the axes' speed and jog step there (16 October: "put this control set
        /// somewhere shared by all Manual Movement pages - this set, and the sub page tabs
        /// just under it"). The machine designs it once in Visual Studio as a UserControl
        /// of its own (a MachineBarHMI: its parts go on in rows when the screen is
        /// narrower than they are) and gives it to StandardScreens (Options.ManualBar);
        /// no page holds it, so the designer never has to load it into another page.
        /// </summary>
        public Control TopBar
        {
            get { return _topBar; }
            set
            {
                if (ReferenceEquals(_topBar, value)) return;
                if (_topBarHolder == null)
                {
                    _topBarHolder = new Panel { Dock = DockStyle.Top, BackColor = Color.White, Name = "settingsTopBar" };
                    _topBarHolder.Resize += (s, e) => FitTopBar();
                }
                if (_topBar != null) _topBarHolder.Controls.Remove(_topBar);
                _topBar = value;
                if (value == null)
                {
                    Controls.Remove(_topBarHolder);
                    return;
                }
                value.Dock = DockStyle.None;
                value.Margin = Padding.Empty;
                _topBarHolder.Controls.Add(value);
                // added after the strip: docked before it - at the very top, the tabs under it
                if (_topBarHolder.Parent != this) Controls.Add(_topBarHolder);
                FitTopBar();
            }
        }

        /// <summary>
        /// The bar in its place, its parts in rows when the screen is narrower. The room
        /// around its parts is the bar's own (FA2010's: 14 from the left, 13 above and
        /// below, as the Home page's speed - 16 October), nothing added here.
        /// </summary>
        private void FitTopBar()
        {
            if (_topBar == null || _topBarHolder == null) return;
            int left = 0, top = 0, bottom = 0;
            _topBar.Location = new Point(left, top);
            int h = _topBar.Height;
            var bar = _topBar as Kaban.UI.Controls.MachineBarHMI;
            // fitted only once it has its real size (magnified with the screen): a layout
            // kept before it would be put back unmagnified
            if (bar != null && _topBarHolder.Width > 0 && (!Kaban.UI.Controls.UiScale.Active || Kaban.UI.Controls.UiScale.IsScaled(bar)))
                h = bar.FitTo(Math.Max(60, _topBarHolder.ClientSize.Width - 2 * left));
            int want = top + h + bottom;
            if (_topBarHolder.Height != want) _topBarHolder.Height = want;
        }

        /// <summary>
        /// Hides this strip's OWN tabs because something else is driving the choice.
        ///
        /// Used for machine selection: that moved to the second-level menu in the
        /// left bar, and leaving the top strip in place as well would offer the same
        /// choice twice, in two shapes, with two "you are here" marks to keep in
        /// step. The pages and the switching logic are still this class's.
        /// </summary>
        public bool TabStripHidden
        {
            get { return !_stripShown; }
            set
            {
                _stripShown = !value;
                _tabStrip.Visible = !value;
                ApplyContentPadding();
                // Strips inside decide their inset by this one: tell them.
                foreach (var inner in NestedStrips(_host)) inner.ApplyContentPadding();
            }
        }

        /// <summary>
        /// Switches to the Nth page - what the machine strip calls.
        ///
        /// Counted over ALL pages, the same list the strip draws one button per
        /// machine from. It used to count only the pages the user may open, so
        /// with one machine's page above the user's level every button after it
        /// opened the NEXT machine's page.
        /// </summary>
        public void ActivateByIndex(int index)
        {
            if (index < 0 || index >= _pages.Count) return;
            var page = _pages[index];
            if (!SecurityManager.HasAccess(page.RequiredLevel))
            {
                MessageBox.Show(LanguageManager.GetString("Sys_ReqSec", "Requires Security Level:") + " " + page.RequiredLevel,
                                LanguageManager.GetString("Sys_Disabled", "Action Disabled."),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RaiseActivePageChanged();          // put the strip's mark back where the page is
                return;
            }
            Activate(page, record: true);
        }

        /// <summary>The page on show.</summary>
        public ISettingsPage ActivePage { get { return _active; } }

        /// <summary>The page a tab of a strip opens (the notes page: a tab tapped is noted with its page), or null.</summary>
        public static ISettingsPage PageOfTab(Control tab)
        {
            for (var p = tab != null ? tab.Parent : null; p != null; p = p.Parent)
            {
                var screen = p as SettingsScreen;
                if (screen == null) continue;
                foreach (var kv in screen._tabButtons) if (ReferenceEquals(kv.Value, tab)) return kv.Key;
                return null;
            }
            return null;
        }

        /// <summary>The page on show (its view), or null.</summary>
        public Control ActiveView
        {
            get { UserControl v; return _active != null && _instances.TryGetValue(_active, out v) ? v : null; }
        }

        /// <summary>True for a tab of a strip (a button of the row of tabs).</summary>
        public static bool IsTab(Control c) { return c != null && c.Parent is TabRowStrip; }

        /// <summary>Index of the page on show among ALL pages (see ActivateByIndex), or -1.</summary>
        public int ActiveIndex { get { return _active == null ? -1 : _pages.IndexOf(_active); } }

        /// <summary>
        /// Raised whenever the page on show changes - by a click, by the machine
        /// strip, by a login rebuild or by Back/Forward - so the shell can keep
        /// the machine strip's "you are here" mark on the machine actually shown.
        /// </summary>
        public event EventHandler ActivePageChanged;

        private void RaiseActivePageChanged()
        {
            var h = ActivePageChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        /// <summary>The view built for a page, or null when it has not been shown yet.</summary>
        internal UserControl ViewOf(ISettingsPage page)
        {
            UserControl view;
            return page != null && _instances.TryGetValue(page, out view) ? view : null;
        }

        /// <summary>Shows a page WITHOUT recording a history step (Back/Forward replay).</summary>
        internal void ShowPage(ISettingsPage page)
        {
            if (page == null || !_pages.Contains(page)) return;
            if (!SecurityManager.HasAccess(page.RequiredLevel)) return;
            Activate(page, record: false);
        }

        /// <summary>
        /// Where the operator is inside a page: every tab strip on the way down,
        /// outermost first, with the page each one shows. Back/Forward restore this
        /// whole path - restoring only the outer page brought back whatever tab it
        /// happened to have open by then.
        /// </summary>
        internal static List<KeyValuePair<SettingsScreen, ISettingsPage>> CaptureChain(Control top)
        {
            var chain = new List<KeyValuePair<SettingsScreen, ISettingsPage>>();
            Control c = top;
            for (int depth = 0; c != null && depth < 8; depth++)
            {
                var s = c as SettingsScreen ?? FindFirstIn(c);
                if (s == null || s._active == null) break;
                chain.Add(new KeyValuePair<SettingsScreen, ISettingsPage>(s, s._active));
                UserControl view;
                c = s._instances.TryGetValue(s._active, out view) ? view : null;
            }
            return chain;
        }

        /// <summary>The tab strip a view is, or the first one inside it; null when it has none.</summary>
        internal static SettingsScreen TabsIn(Control c)
        {
            return c == null ? null : c as SettingsScreen ?? FindFirstIn(c);
        }

        private static SettingsScreen FindFirstIn(Control root)
        {
            var queue = new Queue<Control>();
            foreach (Control ch in root.Controls) queue.Enqueue(ch);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                var s = c as SettingsScreen;
                if (s != null) return s;
                foreach (Control ch in c.Controls) queue.Enqueue(ch);
            }
            return null;
        }

        private List<ISettingsPage> VisiblePages()
        {
            var visible = new List<ISettingsPage>();
            foreach (var p in _pages)
                if (SecurityManager.HasAccess(p.RequiredLevel)) visible.Add(p);
            return visible;
        }

        private void BuildTabs()
        {
            if (IsDisposed) return;

            // The old tab buttons are DISPOSED, not just taken off the strip: removed
            // but alive, they stayed registered with the shared tooltip - one set
            // more on every login, logout and language change.
            foreach (var old in _tabButtons.Values)
            {
                Kaban.UI.Controls.UiStyle.Tips().SetToolTip(old, null);
                old.Dispose();
            }
            _tabStrip.Controls.Clear();
            _tabButtons.Clear();

            var visible = VisiblePages();

            foreach (var page in visible)
            {
                var btn = new Button
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Height = 22,
                    FlatStyle = FlatStyle.Flat,
                    Font = TabFont,                 // shared, not one per tab per rebuild
                    ForeColor = ColTextDim,
                    BackColor = ColStrip,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(2, 0, 2, 0),
                    Padding = new Padding(12, 1, 12, 6),   // descenders + the active rule
                    Text = LanguageManager.GetString(page.TitleKey, page.TitleFallback)
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = ColHover;

                var captured = page;
                btn.Click += (s, e) => Activate(captured, record: true);
                btn.Resize += (s, e) => ShapeTab(btn);   // rounded-top region on size only
                ShapeTab(btn);

                _tabStrip.Controls.Add(btn);
                _tabButtons[page] = btn;
            }

            if (_active == null || !visible.Contains(_active))
                _active = visible.Count > 0 ? visible[0] : null;

            // NOT a visit: a rebuild after a login or a language change must not
            // add history steps (it added one per parented settings screen).
            if (_active != null) Activate(_active, record: false);
            else { _host.Controls.Clear(); RaiseActivePageChanged(); }
        }

        private bool _fittingRows;

        /// <summary>The strip as tall as its rows of tabs: one row at its usual height, more when they wrap.</summary>
        private void FitTabRows()
        {
            if (_fittingRows || _tabStrip.Controls.Count == 0) return;
            _fittingRows = true;
            try
            {
                int bottom = 0;
                foreach (Control c in _tabStrip.Controls)
                    if (c.Visible) bottom = Math.Max(bottom, c.Bottom + c.Margin.Bottom);
                int oneRow = Kaban.UI.Controls.UiScale.For(_tabStrip, 32);
                int want = Math.Max(oneRow, bottom + _tabStrip.Padding.Bottom);
                if (_tabStrip.Height != want) _tabStrip.Height = want;
            }
            finally { _fittingRows = false; }
        }

        /// <summary>The number of rows the tabs take - 1 when they fit on one line.</summary>
        public int TabRows
        {
            get
            {
                var tops = new HashSet<int>();
                foreach (Control c in _tabStrip.Controls) if (c.Visible) tops.Add(c.Top);
                return Math.Max(1, tops.Count);
            }
        }

        /// <summary>Rounded-TOP region (square bottom merges into the white page).</summary>
        private static void ShapeTab(Button btn)
        {
            // Disposed: this runs on every resize of every tab.
            using (var gp = new System.Drawing.Drawing2D.GraphicsPath())
            {
                int r = 6, w = Math.Max(1, btn.Width), h = Math.Max(1, btn.Height);
                gp.AddArc(0, 0, r * 2, r * 2, 180, 90);
                gp.AddArc(w - r * 2 - 1, 0, r * 2, r * 2, 270, 90);
                gp.AddLine(w - 1, h, 0, h);
                gp.CloseFigure();
                btn.Region = new Region(gp);
            }
        }

        private static readonly Font TabFont = Kaban.UI.Controls.UiScale.NewFont("Segoe UI", 10F);

        /// <summary>
        /// Applies active/inactive colours OUTSIDE of any paint cycle. Doing this
        /// inside a Paint handler invalidated layout mid-paint and left the tab
        /// captions invisible until first click.
        /// </summary>
        private static readonly Color ColActiveBorder = Color.FromArgb(150, 158, 170);
        private static readonly Color ColIdleBorder = Color.FromArgb(214, 218, 224);

        /// <summary>
        /// The "you are here" rule under the active tab. BLACK, not blue: blue now
        /// means one thing only - the border of whatever the pointer is over
        /// (UiStyle.HoverBorderColor) - so it cannot also mean "selected".
        /// </summary>
        private static readonly Color ColActiveRule = Color.FromArgb(25, 28, 34);
        private readonly System.Collections.Generic.HashSet<Button> _bordered =
            new System.Collections.Generic.HashSet<Button>();

        private void StyleTabs()
        {
            foreach (var kv in _tabButtons)
            {
                bool isActive = kv.Key == _active;
                if (!_bordered.Contains(kv.Value))
                {
                    _bordered.Add(kv.Value);
                    var btn = kv.Value;
                    btn.Paint += (s2, e2) =>
                    {
                        bool active = _active != null && _tabButtons.ContainsKey(_active) &&
                                      _tabButtons[_active] == btn;
                        using (var pen = new Pen(active ? ColActiveBorder : ColIdleBorder, 1f))
                        {
                            int w = btn.Width - 1, h = btn.Height;
                            e2.Graphics.DrawLine(pen, 0, h, 0, 1);          // left
                            e2.Graphics.DrawLine(pen, 0, 0, w, 0);          // top
                            e2.Graphics.DrawLine(pen, w, 1, w, h);          // right
                        }
                        if (active)
                        {
                            // The rule hugs the CAPTION, not the tab: measured from the
                            // text width and drawn just below the descenders. Spanning
                            // the full tab left it floating clear of the word, so the
                            // two did not read as one object.
                            // Placed from the FONT's line box, not a fixed offset -
                            // see UiStyle.DrawActiveCaptionRule. The old
                            // "btn.Height - 2" sliced p/g/q tails at some sizes.
                            Kaban.UI.Controls.UiStyle.DrawActiveCaptionRule(
                                e2.Graphics, btn.ClientRectangle, btn.Text, btn.Font, ColActiveRule);
                        }
                    };
                }
                // The SAME appearance at every level: an active tab is white and
                // merges into its page. Shading the outer levels darker made the top
                // strip read as a separate navigation bar rather than as the tab of
                // the page below it.
                kv.Value.BackColor = isActive ? Color.White : ColStrip;

                // Outline drawn by us on the LEFT, TOP and RIGHT only: a tab is open
                // at the bottom where it meets its page, and a full box around it
                // read as a floating button rather than a tab.
                kv.Value.FlatAppearance.BorderSize = 0;
                kv.Value.ForeColor = isActive ? ColText : ColTextDim;

                // BOLD the active tab: it is now the page's only heading, since the
                // duplicate caption inside each page has been removed.
                // BOLD only. A font underline sits on the baseline and cuts through
                // descenders (g, p, y); the active tab instead gets a RULE drawn
                // beneath it, clear of the letters - see the Paint handler above.
                var want = isActive ? FontStyle.Bold : FontStyle.Regular;
                if (kv.Value.Font.Style != want)
                {
                    // Shared variant: a new Font on every tab switch was never disposed.
                    kv.Value.Font = Kaban.UI.Controls.UiStyle.FontVariant(kv.Value.Font, want);
                    // the bold caption is wider - let the tab grow with it
                    kv.Value.Width = TextRenderer.MeasureText(kv.Value.Text, kv.Value.Font).Width + 26;
                }
            }
        }

        /// <summary>Every page this screen was built with, regardless of the current user.</summary>
        public IList<ISettingsPage> AllPages { get { return _pages.AsReadOnly(); } }

        /// <summary>True when this screen offers the page (for the current user).</summary>
        public bool HasPage(string titleKey)
        {
            return VisiblePages().Exists(p => p.TitleKey == titleKey);
        }

        /// <summary>Brings a tab forward by its key - for shortcuts from elsewhere.</summary>
        public bool ActivateByKey(string titleKey)
        {
            var page = VisiblePages().Find(p => p.TitleKey == titleKey);
            if (page == null) return false;
            Activate(page, record: true);
            return true;
        }

        private void Activate(ISettingsPage page, bool record)
        {
            if (page == null) return;
            _active = page;

            UserControl view;
            // BUILT IN ANOTHER LANGUAGE: a page that puts its texts on when it is built
            // is built again (the one on show at a language change, the others now that
            // they are shown), on the place the operator was looking at.
            object place = null;
            bool rebuilt = false;
            string builtIn;
            if (_instances.TryGetValue(page, out view) && view is IRebuildForLanguage &&
                _builtIn.TryGetValue(page, out builtIn) &&
                !string.Equals(builtIn, LanguageManager.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
            {
                try { place = ((IRebuildForLanguage)view).Place; } catch { }
                _instances.Remove(page);
                if (view.Parent != null) view.Parent.Controls.Remove(view);
                view.Dispose();
                view = null;
                rebuilt = true;
            }
            if (!_instances.TryGetValue(page, out view))
            {
                view = page.GetControl();
                if (view != null)
                {
                    view.Dock = DockStyle.Fill;
                    _instances[page] = view;
                    _builtIn[page] = LanguageManager.CurrentLanguage;
                    var again = view as IRebuildForLanguage;
                    if (rebuilt && again != null && place != null)
                        try { again.ReturnTo(place); } catch { }
                }
            }

            _host.Controls.Clear();
            if (view != null)
            {
                // The page scrolls when it does not fit (a side page beside it,
                // the magnifying factor) - see PageScroll.
                Kaban.UI.Helpers.PageScroll.Enable(view);
                _host.Controls.Add(view);
                // Tab pages are built on FIRST SELECTION, so they are not on screen
                // when MainForm walks the page - they have to be given the cues here.
                Kaban.UI.Controls.UiStyle.ApplyInteractionCues(view);
            }

            // The scanner only reads controls it can SEE. Without this the second
            // and later tabs showed no values at all, because their addresses were
            // never added to the scan batch.
            Kaban.UI.Plc.PlcScanner.NotifyContentChanged();

            StyleTabs();
            RaiseActivePageChanged();

            // history: a settings TAB change is a navigation step too.
            //
            // THE SHELL RECORDS IT, as a whole location: the page in the content
            // area plus every tab on the way down. The step recorded here used to
            // re-show "this" through FindForm() - which is null once the page has
            // been navigated away from, so Back re-activated a tab on a screen
            // nobody could see and appeared to do nothing. And a nested tab strip
            // handed ITSELF to LoadScreen, pulling it out of its outer page.
            // Only real visits: a rebuild (login, language) or a replay passes false.
            if (!record || Parent == null) return;
            var mf = FindForm() as Kaban.UI.Forms.MainForm;
            if (mf != null) mf.RecordLocation();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SecurityManager.UserChanged -= _onUserChanged;
                LanguageManager.LanguageChanged -= _onLanguageChanged;

                // The page on show goes with the host; the cached ones that are NOT
                // on show belong to nobody else, so they are disposed here.
                foreach (var view in _instances.Values)
                    if (view != null && !view.IsDisposed && view.Parent == null) view.Dispose();
                _instances.Clear();
                _builtIn.Clear();
            }
            base.Dispose(disposing);
        }
    }
}

