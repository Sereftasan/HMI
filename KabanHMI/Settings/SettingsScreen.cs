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
        private readonly FlowLayoutPanel _tabStrip = new FlowLayoutPanel();
        private readonly Panel _host = new Panel();
        private readonly Dictionary<ISettingsPage, Button> _tabButtons = new Dictionary<ISettingsPage, Button>();
        private readonly Dictionary<ISettingsPage, UserControl> _instances = new Dictionary<ISettingsPage, UserControl>();
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
            _tabStrip.WrapContents = false;
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
        /// True when this strip is itself a page of an OUTER strip. The outer one
        /// has already inset the content; adding our own would compound at every
        /// level.
        /// </summary>
        private bool IsNestedInsideAnotherStrip
        {
            get
            {
                for (Control p = Parent; p != null; p = p.Parent)
                    if (p is SettingsScreen) return true;
                return false;
            }
        }

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
            _host.Padding = (IsNestedInsideAnotherStrip || !_tabStrip.Visible)
                ? new Padding(0)
                : new Padding(8, 6, 8, 8);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            ApplyContentPadding();
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
            get { return !_tabStrip.Visible; }
            set { _tabStrip.Visible = !value; ApplyContentPadding(); }
        }

        /// <summary>Switches to the Nth VISIBLE page - what the machine strip calls.</summary>
        public void ActivateByIndex(int index)
        {
            var visible = VisiblePages();
            if (index < 0 || index >= visible.Count) return;
            Activate(visible[index]);
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
                    Font = new Font("Segoe UI", 10F),
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
                btn.Click += (s, e) => Activate(captured);
                btn.Resize += (s, e) => ShapeTab(btn);   // rounded-top region on size only
                ShapeTab(btn);

                _tabStrip.Controls.Add(btn);
                _tabButtons[page] = btn;
            }

            if (_active == null || !visible.Contains(_active))
                _active = visible.Count > 0 ? visible[0] : null;

            if (_active != null) Activate(_active);
            else _host.Controls.Clear();
        }

        /// <summary>Rounded-TOP region (square bottom merges into the white page).</summary>
        private static void ShapeTab(Button btn)
        {
            var gp = new System.Drawing.Drawing2D.GraphicsPath();
            int r = 6, w = Math.Max(1, btn.Width), h = Math.Max(1, btn.Height);
            gp.AddArc(0, 0, r * 2, r * 2, 180, 90);
            gp.AddArc(w - r * 2 - 1, 0, r * 2, r * 2, 270, 90);
            gp.AddLine(w - 1, h, 0, h);
            gp.CloseFigure();
            btn.Region = new Region(gp);
        }

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
                    var old = kv.Value.Font;
                    kv.Value.Font = new Font(old.FontFamily, old.Size, want);
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
            Activate(page);
            return true;
        }

        private void Activate(ISettingsPage page)
        {
            if (page == null) return;
            _active = page;

            UserControl view;
            if (!_instances.TryGetValue(page, out view))
            {
                view = page.GetControl();
                if (view != null)
                {
                    view.Dock = DockStyle.Fill;
                    _instances[page] = view;
                }
            }

            _host.Controls.Clear();
            if (view != null)
            {
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

            // history: a settings TAB change is a navigation step too. The restore
            // action re-shows this settings screen first, then the tab.
            // Only record REAL visits: BuildTabs also calls Activate during
            // construction and on login/language rebuilds, before/without the
            // screen being shown - those must not pollute the history.
            if (Parent == null) return;

            var capturedPage = page;
            Kaban.UI.Helpers.NavigationHistory.Record(() =>
            {
                var mf = FindForm() as Kaban.UI.Forms.MainForm;
                mf?.LoadScreen(this);
                Activate(capturedPage);
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SecurityManager.UserChanged -= _onUserChanged;
                LanguageManager.LanguageChanged -= _onLanguageChanged;
            }
            base.Dispose(disposing);
        }
    }
}

