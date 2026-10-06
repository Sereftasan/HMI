using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Dashboard;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// THE TWO PICK LISTS above axis rows - on an Axis Settings page and, for an
    /// Admin, on the Axis Monitor:
    ///
    ///   PLC axis items: [the row's groups of columns ...]   Motion monitor: [the module's values ...]
    ///
    /// PLC axis items: what the row shows of the PLC's - Manual movement, Torque
    /// monitor, Axis setting parameters, Reference activation, the Z distance,
    /// Alarm codes, Travel per revolution, Backlash - "All" (nothing ticked) is
    /// the whole row. Motion monitor: the simple motion module's values as more
    /// columns - "None" (nothing ticked) adds none. Both kept as the signed-in
    /// user's own (View Preferences); the rows follow at once.
    ///
    /// On a page the two are two bars: PLC axis items scrolls away with the
    /// columns it hides (they are at the left); the Motion monitor scrolls with
    /// the page until it reaches the page's left side and STAYS THERE while the
    /// page scrolls on (Pin) - its values are ticked where their columns are, at
    /// the right end. On the Axis Monitor one bar has both.
    /// </summary>
    public class AxisPickBar : FlowLayoutPanel
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private readonly string _itemsKey, _motionKey;
        private readonly Label _itemsCaption, _motionCaption;
        private readonly bool _hasItems, _hasMotion;
        private bool _syncing;

        public PickList ItemsPick { get; private set; }
        public PickList MotionPick { get; private set; }

        /// <param name="items">With the PLC axis items list.</param>
        /// <param name="motion">With the Motion monitor list.</param>
        public AxisPickBar(string itemsKey, string motionKey, bool items = true, bool motion = true)
        {
            _itemsKey = itemsKey;
            _motionKey = motionKey;
            _hasItems = items;
            _hasMotion = motion;
            WrapContents = false;
            AutoSize = false;
            Padding = new Padding(0);
            Margin = new Padding(0);
            BackColor = Color.White;

            _itemsCaption = Caption(T("AxIt_Pick", "PLC axis items:"));
            ItemsPick = new PickList(UiStyle.Tips())
            {
                Name = "itemsPick", Size = new Size(300, 28), Margin = new Padding(0, 4, 18, 0),
                AllText = T("AxIt_All", "All"),
                // The rows are laid out again once, when the list closes - not at every tick.
                ApplyOnClose = true
            };
            ItemsPick.Changed += (s, e) => { if (!_syncing) KeepItems(); };

            _motionCaption = Caption(T("Mot_Pick", "Motion monitor items:"));
            MotionPick = new PickList(UiStyle.Tips())
            {
                Name = "motionPick", Size = new Size(420, 28), Margin = new Padding(0, 4, 8, 0),
                AllText = T("Mot_PickNone", "None"),
                ApplyOnClose = true
            };
            MotionPick.SetItems(SimpleMotion.Items.Select(i => new PickList.Item(
                i.Key, LanguageManager.GetString(i.TitleKey, i.Title) + "  (" + i.Ref + ")")), false);
            MotionPick.Changed += (s, e) => { if (!_syncing) KeepMotion(); };

            // both in one bar (the Axis Monitor): the same room between them as on a page
            if (items && motion) _motionCaption.Margin = new Padding(GapAfterItems, 9, 6, 0);
            if (items) { Controls.Add(_itemsCaption); Controls.Add(ItemsPick); }
            if (motion) { Controls.Add(_motionCaption); Controls.Add(MotionPick); }
        }

        /// <summary>As wide as its lists (a bar of one list beside another).</summary>
        public int NaturalWidth { get { return GetPreferredSize(Size.Empty).Width; } }

        private static Label Caption(string text)
        {
            return new Label
            {
                AutoSize = true, Text = text, Font = UiStyle.Body, ForeColor = Color.FromArgb(60, 66, 76), Margin = new Padding(0, 9, 6, 0)
            };
        }

        /// <summary>What the rows have to offer: their items, and whether any is on a module (the Motion monitor).</summary>
        public void Offer(IList<string> items, bool motion)
        {
            _syncing = true;
            try
            {
                ItemsPick.SetItems(AxisSettingHMI.ItemKeys.Where(items.Contains).Select(k => new PickList.Item(k, AxisSettingHMI.ItemText(k))), false);
                _motionCaption.Visible = MotionPick.Visible = motion && _hasMotion;
                _itemsCaption.Visible = ItemsPick.Visible = _hasItems;
            }
            finally { _syncing = false; }
            Sync();
        }

        /// <summary>The ticks as kept (another user signed in, a pick changed elsewhere).</summary>
        public void Sync()
        {
            _syncing = true;
            try
            {
                var picked = ViewPreferences.PickOf(_itemsKey);
                var want = picked == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                          : new HashSet<string>(ItemsPick.Items.Select(i => i.Key).Where(picked.Contains), StringComparer.OrdinalIgnoreCase);
                if (!ItemsPick.Selected.SetEquals(want)) ItemsPick.Select(want);
                var motion = ViewPreferences.ListOf(_motionKey);
                if (!MotionPick.Selected.SetEquals(motion)) MotionPick.Select(motion);
            }
            finally { _syncing = false; }
        }

        private void KeepItems()
        {
            var chosen = ItemsPick.Selected;
            var kept = ViewPreferences.PickOf(_itemsKey);
            var offered = new HashSet<string>(ItemsPick.Items.Select(i => i.Key), StringComparer.OrdinalIgnoreCase);
            // Nothing ticked: All. Otherwise the ticks - and what another page's rows
            // had ticked that these rows do not offer stays ticked for that page.
            IEnumerable<string> keys = chosen.Count == 0 ? new string[0]
                : AxisSettingHMI.ItemKeys.Where(k => chosen.Contains(k) || (kept != null && !offered.Contains(k) && kept.Contains(k)));
            var list = keys.ToList();
            bool same = list.Count == 0 ? kept == null : kept != null && kept.SetEquals(list);
            if (!same) ViewPreferences.SetPick(_itemsKey, list);
        }

        private void KeepMotion()
        {
            var chosen = MotionPick.Selected;
            var keys = SimpleMotion.Items.Where(i => chosen.Contains(i.Key)).Select(i => i.Key).ToList();
            if (keys.SequenceEqual(ViewPreferences.ListOf(_motionKey), StringComparer.OrdinalIgnoreCase)) return;
            ViewPreferences.SetList(_motionKey, keys);
        }

        // ---- in sight while the page scrolls sideways --------------------------------

        private Control _page;                // where the bar is (its width: the room it shows)
        private ScrollableControl _scroller;  // what scrolls (the page, or a frame's body)
        private int _homeX, _stickX;
        private bool _pinning, _hooked;
        private EventHandler _onScroll, _onResize;
        private ScrollEventHandler _onScrollBar;

        /// <summary>
        /// Keeps the bar in sight while the page scrolls sideways, as a heading
        /// that sticks: it scrolls with the page from where it stands
        /// (<paramref name="homeX"/>, in the page's unscrolled terms) until it
        /// reaches the page's left side (<paramref name="stickX"/>, where the
        /// page's content starts) and stays there while the page scrolls on -
        /// at <paramref name="homeX"/> plus the scroll beyond that point. No
        /// wider than the page shows from where it is, so it never stretches
        /// the page.
        /// </summary>
        public void Pin(Control page, int homeX, int stickX)
        {
            Pin(page, page as ScrollableControl, homeX, stickX);
        }

        /// <summary>
        /// As Pin(page ...), the bar being on <paramref name="host"/> and the scroll
        /// that of <paramref name="scroller"/> - a page in a frame (AxisPageFrame):
        /// the bar in the heading, which does not scroll, the rows in the body,
        /// which does (the frame calls Place as it scrolls). Pinned again, it
        /// leaves what it followed before.
        /// </summary>
        public void Pin(Control host, ScrollableControl scroller, int homeX, int stickX)
        {
            if (_page != null && _onResize != null) _page.Resize -= _onResize;
            if (_scroller != null && _onScrollBar != null) _scroller.Scroll -= _onScrollBar;
            _page = host;
            _scroller = scroller;
            _homeX = homeX;
            _stickX = Math.Min(stickX, homeX);
            if (!_hooked)
            {
                _hooked = true;
                // a scroll moves every child (the bar too, on a page): it is put where it belongs
                _onScroll = (s, e) => Place();
                LocationChanged += _onScroll;
            }
            _onResize = (s, e) => Place();
            _onScrollBar = (s, e) => Place();
            host.Resize += _onResize;
            if (scroller != null) scroller.Scroll += _onScrollBar;
            Place();
        }

        private AxisPickBar _follow;

        /// <summary>The room between the PLC axis items list and the Motion monitor items list (factor 1).</summary>
        public const int GapAfterItems = 28;

        /// <summary>
        /// Stands right of <paramref name="left"/> (the PLC axis items list), with
        /// room between (GapAfterItems): its place is taken from that list
        /// whenever it is placed - not kept in pixels, which at a larger screen
        /// scale were the unscaled ones and put it over that list (6 October).
        /// </summary>
        public void StandRightOf(AxisPickBar left)
        {
            _follow = left;
            Place();
        }

        /// <summary>Where the list it stands right of ends, in the page's unscrolled terms, and the room after it.</summary>
        private bool FollowHome(out int home)
        {
            home = 0;
            if (_follow == null || _follow.IsDisposed || _page == null || !_follow.Visible) return false;
            // as wide as what it holds: a bar measured before it was scaled was left too wide
            int natural = _follow.NaturalWidth;
            if (natural > 0 && _follow.Width != natural) _follow.Width = natural;
            int x = 0;
            Control c = _follow;
            for (; c != null && c != _page; c = c.Parent) x += c.Left;
            if (c == null) return false;
            // where it is shown now, plus the scroll: on the page and in a frame's
            // heading (whose content follows the rows) alike, the unscrolled place
            int scrolled = _scroller != null ? -_scroller.AutoScrollPosition.X : 0;
            home = x + natural + scrolled + UiScale.For(_page, GapAfterItems);
            return true;
        }

        /// <summary>Where the bar stands with the page unscrolled, and where it sticks (tests).</summary>
        public int HomeX { get { int h; return FollowHome(out h) ? h : _homeX; } }
        public int StickX { get { return _stickX; } }

        /// <summary>Where the bar belongs now, in the page's window: with the page until the left side, then there.</summary>
        public int PlaceX
        {
            get
            {
                int scrolled = _scroller != null ? -_scroller.AutoScrollPosition.X : 0;
                int home;
                if (!FollowHome(out home)) home = _homeX;
                return Math.Max(Math.Min(_stickX, home), home - scrolled);
            }
        }

        /// <summary>The bar where it belongs now (the page scrolled, or was laid out).</summary>
        public void Place()
        {
            if (_pinning || _page == null || _page.IsDisposed) return;
            int x = PlaceX;
            // the list as designed, narrowed when the window shows less (a larger
            // screen scale): its arrow stays in sight rather than cut off at the edge
            int design = UiScale.For(this, 420);
            bool narrowable = _hasMotion && MotionPick.Visible;
            int full = narrowable ? NaturalWidth - MotionPick.Width + design : NaturalWidth;
            int want = Math.Max(full, UiScale.For(_page, 200));
            int room = _page.ClientSize.Width - x - UiScale.For(_page, 4);
            int w = Math.Max(UiScale.For(_page, 200), Math.Min(want, room));
            if (narrowable)
            {
                int pick = Math.Max(UiScale.For(this, 160), design - Math.Max(0, full - w));
                if (MotionPick.Width != pick) MotionPick.Width = pick;
            }
            if (Left == x && Width == w) return;
            _pinning = true;
            // Without laying the page out again: a layout scrolls a page back to
            // the field that has the focus (seen in Mono), and the bar, inside the
            // window, changes nothing of the page's extent.
            _page.SuspendLayout();
            try { SetBounds(x, Top, w, Height, BoundsSpecified.X | BoundsSpecified.Width); }
            finally { _page.ResumeLayout(false); _pinning = false; }
        }
    }
}
